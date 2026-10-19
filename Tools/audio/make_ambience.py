"""Ambience loops for The Twelfth Stamp (original synthesis, stereo, 48-60 s, seamless).

  python -I Tools/audio/make_ambience.py            # all
  python -I Tools/audio/make_ambience.py amb_shop
  python -I Tools/audio/make_ambience.py --remaster [ids]   # re-run only the mix stage on the float masters

Method: every layer is built as a circular signal of exactly L samples.
  * noise beds: generated for L + 4 s and folded with a correlation-aware equal-power crossfade of
    the tail into the head (dsp.loop_crossfade);
  * slow modulations (gusts, swells, traffic) are periodic functions with period L;
  * tonal partials use frequencies with a whole number of cycles per loop;
  * discrete events (drips, creaks, horns, chimes) are rendered with their tails folded back to the
    start, and reverb is a circular convolution, so tails ring across the loop point;
  * the mix is rotated so the file boundary sits in the most broadband, event-free stretch (best
    masking of Vorbis' independent coding of the file's end and start), then onto an upward zero
    crossing, given the footstep-friendly mix EQ (MIX_EQ), loudness-normalised (BS.1770) and encoded with ffmpeg
    libvorbis -q:a 5, 48 kHz stereo.
Layer levels are explicit: beds are RMS-normalised and events peak-normalised, then mixed at the dB
offsets written in each scene (relative to that scene's main bed RMS).
Intermediate float WAVs: Audio/_work/amb/ ; output: Game/Assets/Resources/Audio/Ambience/<id>.ogg
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
from scipy.signal import fftconvolve  # noqa: E402

import materials as M  # noqa: E402
from dsp import (SR, TAU, N, t_axis, rng_for, place, env_points, colored, shaped_noise, lp, bp, resonator,  # noqa: E402
                 tv_bandpass, impulse_train, poisson_clicks, crackle, additive, make_ir, pan, fold,
                 loop_crossfade, rotate_to_zero_crossing, seam_report, circ_sosfilt, sos, lufs, true_peak_db,
                 norm_lufs, undb, encode_ogg, stick_slip)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Game", "Assets", "Resources", "Audio", "Ambience")
WORK = os.path.join(ROOT, "Audio", "_work", "amb")
META = os.path.join(ROOT, "Audio", "_work", "amb_meta.json")
XF = 4.0  # crossfade seconds for beds
LIC = "Original synthesis for The Twelfth Stamp (project-owned; no third-party material)"


# ----------------------------------------------------------------------------- helpers
def R(x):
    """Unit-RMS (over all channels)."""
    return x / (np.sqrt(np.mean(x ** 2)) + 1e-12)


def P(x):
    """Unit-peak."""
    return x / (np.max(np.abs(x)) + 1e-12)


def rise(ev, ref, rel_db):
    """Scale an event layer so its loudest 100 ms window (300-6000 Hz band, mid) sits rel_db relative to the
    band RMS of the reference bed: events are set by measured short-term level, not by peak."""
    s = sos("bandpass", (300.0, 6000.0), 2)
    from scipy.signal import sosfilt
    em = sosfilt(s, np.atleast_2d(ev).mean(axis=0))
    rm = sosfilt(s, np.atleast_2d(ref).mean(axis=0))
    w = N(0.1)
    k = len(em) // w
    st = np.sqrt(np.mean(em[:k * w].reshape(k, w) ** 2, axis=1))
    return ev * (np.sqrt(np.mean(rm ** 2)) * undb(rel_db) / (st.max() + 1e-12))


def F(f, L):
    """Whole number of cycles per loop."""
    return round(f * L) / L


def periodic(L, n, rate, rng, lo=0.0, hi=1.0, power=1.0):
    """Smooth random control with period L seconds (Fourier series of 1/L harmonics) on n samples."""
    K = max(int(rate * L), 1)
    t = np.arange(n) / SR
    y = np.zeros(n)
    for k in range(1, K + 1):
        y += rng.normal(0, 1) / k ** 0.8 * np.cos(TAU * k * t / L + rng.uniform(0, TAU))
    y = (y - y.min()) / (y.max() - y.min() + 1e-12)
    return lo + (hi - lo) * y ** power


def spec(f, pts):
    """Spectral magnitude from [(Hz, dB), ...], linear in log-frequency."""
    fs = np.log10(np.array([p[0] for p in pts]))
    ds = np.array([p[1] for p in pts])
    return undb(np.interp(np.log10(np.maximum(f, 1.0)), fs, ds))


def bed(L, rng, mag_fn, corr=0.2, nfft=4096):
    """Stereo noise bed (spectral envelope mag_fn(t, f)), seamless loop of L seconds, unit RMS."""
    n, Fx = N(L), N(XF)
    a = shaped_noise(n + Fx, mag_fn, rng, nfft=nfft)
    b = shaped_noise(n + Fx, mag_fn, rng, nfft=nfft)
    l = a * np.sqrt(1 - corr / 2) + b * np.sqrt(corr / 2)
    r = b * np.sqrt(1 - corr / 2) + a * np.sqrt(corr / 2)
    return R(loop_crossfade(np.stack([l, r]), n, Fx))


def static_bed(L, rng, pts, corr=0.2):
    return bed(L, rng, lambda t, f: spec(f, pts)[:, None] * np.ones((1, len(t))), corr=corr)


def loopify(x, L):
    return loop_crossfade(np.atleast_2d(x), N(L), N(XF))


def ev_buf(L, tail=8.0):
    return np.zeros((2, N(L + tail)))


def circ_reverb(x, ir, wet):
    """Circular stereo convolution reverb (tail wraps into the loop start)."""
    w = np.stack([fftconvolve(x[0], ir[0]), fftconvolve(x[1], ir[1])])
    return x + wet * fold(w, x.shape[-1])


def babble(L, rng, voices=10, f_lp=900):
    """Distant wordless murmur: voiced pulses through randomly moving formants, then low-passed."""
    n = N(L + XF)
    out = np.zeros((2, n))
    t = np.arange(n) / SR
    for _ in range(voices):
        f0 = rng.uniform(95, 210) * (1 + 0.08 * np.sin(TAU * rng.uniform(0.1, 0.3) * t + rng.uniform(0, TAU)))
        src = lp(impulse_train(n, f0, rng, jitter=0.02, amp_sigma=0.05), 2500)
        k = int((L + XF) * 4) + 2
        F1 = np.interp(np.linspace(0, k - 1, n), np.arange(k), rng.uniform(300, 750, k))
        F2 = np.interp(np.linspace(0, k - 1, n), np.arange(k), rng.uniform(900, 2000, k))
        y = tv_bandpass(src, F1, 5) + 0.6 * tv_bandpass(src, F2, 7)
        syl = np.clip(np.sin(TAU * rng.uniform(3, 5) * t + rng.uniform(0, TAU)), 0, 1)
        phrase = (periodic(L + XF, n, 0.15, rng) > rng.uniform(0.35, 0.6)).astype(float)
        phrase = np.convolve(phrase, np.ones(N(0.2)) / N(0.2), mode="same")
        out += pan(R(y * syl * phrase + 1e-9) * rng.uniform(0.5, 1.0), rng.uniform(-0.8, 0.8))
    return lp(out, f_lp, 2)


def distant_pass(rng, lo=80, hi=900, dur=7.0):
    n = N(dur)
    env = np.sin(np.linspace(0, np.pi, n)) ** 2
    x = bp(colored(n, rng, -4.0), lo, hi) * env + 0.3 * bp(colored(n, rng, -2.0), hi, hi * 2.5) * env ** 3
    s = rng.choice([-1, 1])
    return pan(x, np.linspace(0.8 * s, -0.3 * s, n))


def wind(L, rng, base=500, spread=700, gust_rate=0.12, gmin=0.25, whistle=None, sand=0.0):
    """Gusting wind bed (+ optional resonant whistle and sand hiss), unit RMS, seamless."""
    n = N(L)
    nn = n + N(XF)
    g = periodic(L, nn, gust_rate, rng, gmin, 1.0, power=1.5)
    c = periodic(L, nn, gust_rate * 1.3, rng, 0, 1)
    ts = np.arange(nn) / SR

    def mag(tt, f):
        G = np.interp(tt, ts, g)[None, :]
        C = base + spread * np.interp(tt, ts, c)[None, :]
        body = np.exp(-0.5 * (np.log2(np.maximum(f[:, None], 20) / C) / 1.3) ** 2)
        floor = spec(f, [(20, -20), (100, -6), (400, 0), (2000, -12), (8000, -30), (20000, -50)])[:, None] * 0.35
        return (body + floor) * G
    w = bed(L, rng, mag, corr=0.35)
    if whistle:
        fw = whistle * (1 + 0.15 * (periodic(L, nn, 0.2, rng) - 0.5))
        wh = tv_bandpass(colored(nn, rng, 0.0), fw, 40) * periodic(L, nn, 0.15, rng, 0, 1, power=3)
        w = w + undb(-14) * R(loopify(pan(wh, 0.2), L))
    if sand:
        s = bp(colored(nn, rng, 0.0), 3000, 11000) * g
        gr = crackle(nn, 1500 * g, rng, lo=2500, hi=10000, grain=0.0003)
        s2 = np.stack([R(s) + 0.5 * R(gr), np.roll(R(s), N(0.37)) + 0.5 * np.roll(R(gr), N(0.21))])
        w = w + sand * R(loopify(s2, L))
    return R(w)


# ----------------------------------------------------------------------------- scenes
def amb_shop(rng, L):
    n = N(L)
    t = t_axis(n)
    room = static_bed(L, rng, [(20, -30), (60, -6), (200, 0), (1000, -6), (3000, -18), (8000, -40), (20000, -60)])
    mur = R(circ_reverb(R(loopify(babble(L, rng, voices=12, f_lp=850), L)), make_ir(1.4, rng, hf=0.3), 0.6))
    blade = F(4.8, L)
    fan = static_bed(L, rng, [(20, -40), (150, -8), (400, 0), (1500, -12), (5000, -40), (20000, -70)], corr=0.8)
    fan = fan * (0.65 + 0.35 * np.cos(TAU * blade * t) ** 2)[None, :]
    motor = np.sin(TAU * F(100.0, L) * t) + 0.4 * np.sin(TAU * F(200.0, L) * t)
    tick = np.zeros(n)
    rev = F(1.6, L)
    for k in range(int(round(L * rev))):
        place(tick, M.wood_knock(rng, 900, 0.05, hard=0.3, t60=0.03), k / rev)
    fan = R(fan) + undb(-16) * pan(R(motor), 0) + undb(-20) * pan(P(tick), 0)
    ev = ev_buf(L)
    for t0 in rng.uniform(3, L - 3, 3):
        nn = N(rng.uniform(0.6, 1.2))
        cr = M.wood_creak(nn, env_points([(0, 25), (nn / SR, 45)], nn), rng,
                          env=env_points([(0, 0), (0.1, 1), (nn / SR, 0)], nn))
        place(ev, pan(P(cr) * 0.5, rng.uniform(-0.7, 0.7)), t0)
    for t0 in rng.uniform(5, L - 10, 2):
        place(ev, P(distant_pass(rng, 90, 700, rng.uniform(5, 8))), t0)
    ev = P(circ_reverb(fold(ev, n), make_ir(0.9, rng, hf=0.4), 0.4))
    base = room + undb(-8) * mur + undb(-6) * fan
    return base + rise(ev, base, +2.0)


def amb_apartment(rng, L):
    n = N(L)
    t = t_axis(n)
    room = static_bed(L, rng, [(20, -30), (50, -8), (150, 0), (600, -8), (2500, -22), (8000, -45), (20000, -70)])
    city = static_bed(L, rng, [(20, -10), (60, 0), (200, -4), (600, -18), (2000, -40), (20000, -80)])
    city = city * periodic(L, n, 0.08, rng, 0.5, 1.0)[None, :]
    ac = static_bed(L, rng, [(20, -40), (120, -6), (500, 0), (1500, -6), (4000, -20), (10000, -45), (20000, -70)], corr=0.6)
    tone = np.sin(TAU * F(120.0, L) * t) + 0.5 * np.sin(TAU * F(240.0, L) * t) + 0.2 * np.sin(TAU * F(360.0, L) * t)
    ac = ac + undb(-14) * pan(R(tone), 0.3)
    ev = ev_buf(L)
    for t0 in sorted(rng.uniform(2, L - 8, 3)):
        place(ev, P(distant_pass(rng, 70, 600, rng.uniform(6, 9))), t0)
    nh = N(1.1)
    horn = additive(207.5, nh, [(1, 1.0), (2, 0.6), (3, 0.3), (4, 0.15)]) * env_points([(0, 0), (0.05, 1), (0.9, 0.9), (1.1, 0)], nh)
    place(ev, pan(P(lp(horn, 1200)) * 0.3, -0.5), rng.uniform(L * 0.3, L * 0.6))
    ev = P(circ_reverb(fold(ev, n), make_ir(2.0, rng, hf=0.3), 0.5))
    base = room + undb(-2) * R(city) + undb(-4) * R(ac)
    return base + rise(ev, base, +2.0)


def amb_apartment_corrupt(rng, L):
    n = N(L)
    t = t_axis(n)
    room = static_bed(L, rng, [(20, -60), (200, -20), (600, -6), (2000, 0), (6000, -10), (14000, -30), (20000, -60)])
    tones = np.zeros((2, n))
    for f, d, a in [(293.66, 2.3, 0.5), (440.0, -2.6, 0.35), (174.6, 1.1, 0.4), (1174.7, 3.7, 0.12)]:
        sw = periodic(L, n, 0.06, rng, 0.0, 1.0, power=2.0)
        tones += np.stack([a * sw * np.sin(TAU * F(f, L) * t + rng.uniform(0, TAU)),
                           a * sw * np.sin(TAU * F(f + d, L) * t + rng.uniform(0, TAU))])
    ev = ev_buf(L)
    for t0 in rng.uniform(4, L - 6, 2):
        g = M.glass_ring(rng.uniform(700, 1100), 2.0, rng, t60=(1.6, 1.0, 0.6, 0.3, 0.2), split=3)[::-1]
        place(ev, pan(P(g), rng.uniform(-0.8, 0.8)), t0)
    ev = P(circ_reverb(fold(ev, n), make_ir(2.5, rng, hf=0.5), 0.6))
    base = room + undb(-6) * R(tones)
    return base + rise(ev, base, +3.0)


def amb_egypt(rng, L):
    return wind(L, rng, base=420, spread=900, gust_rate=0.1, gmin=0.3, sand=undb(-5))


def amb_riyadh_wind(rng, L):
    n = N(L)
    t = t_axis(n)
    w = wind(L, rng, base=600, spread=1200, gust_rate=0.16, gmin=0.35, whistle=1250, sand=undb(-14))
    hum = (np.sin(TAU * F(50.0, L) * t) + 0.6 * np.sin(TAU * F(100.0, L) * t) + 0.25 * np.sin(TAU * F(150.0, L) * t))
    hum = hum * periodic(L, n, 0.05, rng, 0.6, 1.0)
    rum = static_bed(L, rng, [(20, -6), (50, 0), (150, -6), (400, -25), (1000, -50), (20000, -90)])
    return w + undb(-10) * pan(R(hum), 0) + undb(-8) * rum


def amb_paris_rain(rng, L):
    n = N(L)
    nn = n + N(XF)
    hiss = static_bed(L, rng, [(20, -40), (200, -14), (800, -4), (3000, 0), (6000, -2), (12000, -10), (20000, -30)], corr=0.1)
    drops = []
    g = rng.standard_normal(N(0.004)) * np.exp(-np.linspace(0, 6, N(0.004)))
    for _ in range(2):
        c = poisson_clicks(nn, 2400 * periodic(L, nn, 0.05, rng, 0.85, 1.0), rng, amp_sigma=0.9)
        drops.append(bp(fftconvolve(c, g)[:nn], 1200, 12000))
    drops = R(loopify(np.stack(drops), L))
    gut = ev_buf(L, tail=2.0)
    for gx, gp in [(-0.65, 1.0), (0.55, 0.8)]:
        t0 = 0.0
        while t0 < L:
            t0 += rng.exponential(0.09) + (rng.uniform(0.3, 1.2) if rng.random() < 0.15 else 0)
            b = M.bubble(rng, f0=rng.uniform(700, 2200), dur=0.05, rise=0.3)
            place(gut, pan(b * rng.uniform(0.2, 1.0) * gp, gx + rng.normal(0, 0.05)), t0)
    gut = R(circ_reverb(R(fold(gut, n)), make_ir(0.8, rng, hf=0.4), 0.3))
    flow = static_bed(L, rng, [(20, -40), (300, -10), (800, 0), (1600, -4), (4000, -24), (20000, -60)], corr=0.3)
    flow = R(flow * periodic(L, n, 6.0, rng, 0.5, 1.0)[None, :])
    river = static_bed(L, rng, [(20, -20), (80, -4), (300, 0), (700, -8), (2000, -30), (20000, -70)], corr=0.5)
    river = R(river * periodic(L, n, 0.1, rng, 0.7, 1.0)[None, :])
    return hiss + undb(-4) * drops + undb(-9) * gut + undb(-12) * flow + undb(-6) * river


def amb_ny_harbour(rng, L):
    n = N(L)
    lap = static_bed(L, rng, [(20, -30), (120, -6), (400, 0), (1200, -8), (3000, -24), (20000, -60)], corr=0.4)
    lap = R(lap * periodic(L, n, 0.35, rng, 0.35, 1.0, power=1.8)[None, :])
    ev = ev_buf(L, tail=4.0)
    t0 = 0.0
    while t0 < L:
        t0 += rng.uniform(1.8, 4.0)
        ns = N(0.25)
        slap = lp(rng.standard_normal(ns), 900) * np.exp(-np.linspace(0, 6, ns))
        slap = slap + 0.5 * resonator(slap, rng.uniform(180, 320), 0.08)
        place(ev, pan(P(slap) * rng.uniform(0.4, 1.0), rng.uniform(-0.6, 0.6)), t0)
        for _ in range(rng.integers(1, 4)):
            place(ev, pan(M.bubble(rng, f0=rng.uniform(300, 900), dur=0.08, rise=0.15) * 0.25, rng.uniform(-0.6, 0.6)),
                  t0 + rng.uniform(0.02, 0.2))
    ev = P(circ_reverb(fold(ev, n), make_ir(0.6, rng, hf=0.4), 0.3))
    far = ev_buf(L, tail=10.0)
    for t0 in rng.uniform(2, L - 2, 4):
        nn = N(rng.uniform(1.0, 2.0))
        cr = stick_slip(nn, env_points([(0, rng.uniform(6, 10)), (nn / SR, rng.uniform(12, 20))], nn), rng,
                        [(rng.uniform(220, 300), 0.3, 1.0), (rng.uniform(600, 800), 0.2, 0.5), (1450, 0.1, 0.25)],
                        jitter=0.15, amp_sigma=0.3)
        cr = cr * env_points([(0, 0), (0.2, 1), (nn / SR, 0)], nn)
        place(far, pan(P(cr) * 0.5, rng.uniform(-0.9, 0.9)), t0)
    horn_times = [L * 0.12, L * 0.12 + 20.0, L * 0.12 + 40.0]  # a far horn every 20 s
    for t0 in horn_times:
        d = rng.uniform(2.6, 3.4)
        nh = N(d)
        horn = additive(rng.choice([98.0, 87.3, 110.0]), nh, [(1, 1.0), (2, 0.8), (3, 0.5), (4, 0.3), (5, 0.18), (6, 0.1)])
        horn = horn * env_points([(0, 0), (0.35, 1), (d - 0.5, 0.9), (d, 0)], nh)
        place(far, pan(P(lp(horn, 900)), rng.uniform(-0.4, 0.4)), t0)
    far = P(circ_reverb(fold(far, n), make_ir(4.0, rng, hf=0.25), 1.2))
    air = static_bed(L, rng, [(20, -20), (200, -6), (1000, 0), (4000, -10), (12000, -30), (20000, -60)])
    base = lap + undb(-12) * air
    return base + rise(ev, base, +3.0) + rise(far, base, +3.0)


def amb_japan_night(rng, L):
    n = N(L)
    w = wind(L, rng, base=500, spread=400, gust_rate=0.08, gmin=0.15)
    g = periodic(L, n, 0.08, rng, 0.0, 1.0, power=2.0)
    ev = ev_buf(L, tail=6.0)
    notes = [1318.5, 1567.98, 1760.0, 2093.0, 2349.3]
    t0 = 0.0
    while t0 < L:
        t0 += rng.exponential(2.2)
        if rng.random() < 0.25 + 0.75 * g[min(N(t0), n - 1)]:
            for k in range(rng.integers(1, 4)):
                ch = M.chime(rng.choice(notes) * rng.uniform(0.998, 1.002), 2.5, rng, kind="glass", t60=1.6, bright=0.6, split=1.2)
                side = rng.uniform(-0.9, -0.3) if k % 2 else rng.uniform(0.2, 0.8)
                place(ev, pan(P(ch) * rng.uniform(0.3, 1.0), side), t0 + k * rng.uniform(0.08, 0.35))
    ev = P(circ_reverb(fold(ev, n), make_ir(2.8, rng, hf=0.6), 0.9))
    paper = ev_buf(L, tail=2.0)
    for t0 in rng.uniform(1, L - 2, 5):
        nn = N(rng.uniform(0.4, 0.9))
        e = env_points([(0, 0), (0.15, 1), (nn / SR, 0)], nn)
        r = M.paper_crackle(nn, 600 * e, rng, lo=1200, hi=6000) + 0.5 * M.paper_swish(nn, e, rng, 1200, 2600, 1800, q=1.5)
        place(paper, pan(P(r), rng.uniform(-0.8, 0.8)), t0)
    paper = P(circ_reverb(fold(paper, n), make_ir(0.5, rng, hf=0.5), 0.3))
    return w + undb(-4) * ev + undb(-6) * paper


def amb_india_garden(rng, L):
    n = N(L)
    nn = n + N(XF)
    air = static_bed(L, rng, [(20, -30), (80, -10), (300, -4), (1000, 0), (3000, -10), (9000, -30), (20000, -60)])
    g = rng.standard_normal(N(0.006)) * np.exp(-np.linspace(0, 5, N(0.006)))
    fz = []
    for _ in range(2):
        c = poisson_clicks(nn, 900 * periodic(L, nn, 0.5, rng, 0.7, 1.0), rng, amp_sigma=0.7)
        fz.append(bp(fftconvolve(c, g)[:nn], 500, 7000))
    splash = R(loopify(np.stack(fz), L))
    bub = ev_buf(L, tail=2.0)
    t0 = 0.0
    while t0 < L:
        t0 += rng.exponential(0.035)
        place(bub, pan(M.bubble(rng, f0=rng.uniform(500, 2600), dur=0.04, rise=0.25) * rng.uniform(0.1, 0.6),
                       float(np.clip(rng.normal(0.25, 0.15), -1, 1))), t0)
    water = R(circ_reverb(R(splash) * 0.6 + R(fold(bub, n)), make_ir(0.7, rng, hf=0.5), 0.25))
    bells = ev_buf(L, tail=8.0)
    for t0 in sorted(rng.uniform(2, L - 4, 4)):
        for k in range(rng.integers(2, 5)):
            f = rng.choice([880.0, 1046.5, 1174.7, 1318.5]) * rng.uniform(0.99, 1.01)
            b = M.chime(f, 3.0, rng, kind="bar", t60=2.2, bright=0.5)
            place(bells, pan(P(b) * rng.uniform(0.5, 1.0), -0.6 + rng.normal(0, 0.1)), t0 + k * rng.uniform(0.25, 0.5))
    bells = P(circ_reverb(lp(fold(bells, n), 3500), make_ir(3.2, rng, hf=0.4), 1.0))
    base = air + undb(+3) * water
    return base + rise(bells, base, +1.0)


def amb_rio_frozen(rng, L):
    n = N(L)
    t = t_axis(n)
    hum = np.zeros((2, n))
    for f, a, d in [(55.0, 1.0, 0.25), (82.5, 0.5, -0.2), (110.0, 0.25, 0.35), (164.8, 0.08, 0.5),
                    (220.0, 0.06, 0.3), (330.0, 0.03, -0.4), (440.0, 0.015, 0.45)]:
        sw = periodic(L, n, 0.05, rng, 0.5, 1.0)
        hum += np.stack([a * sw * np.sin(TAU * F(f, L) * t + rng.uniform(0, TAU)),
                         a * sw * np.sin(TAU * F(f + d, L) * t + rng.uniform(0, TAU))])
    air = static_bed(L, rng, [(20, -40), (500, -16), (3000, -6), (8000, 0), (16000, -10), (20000, -40)])
    sub = static_bed(L, rng, [(20, -6), (45, 0), (120, -12), (400, -40), (20000, -90)])
    return R(hum) + undb(-30) * air + undb(-14) * sub


def amb_london_river(rng, L):
    n = N(L)
    river = static_bed(L, rng, [(20, -24), (100, -6), (400, 0), (1200, -3), (3000, -14), (8000, -32), (20000, -60)], corr=0.3)
    river = R(river * periodic(L, n, 0.15, rng, 0.6, 1.0)[None, :])
    w = wind(L, rng, base=800, spread=900, gust_rate=0.14, gmin=0.2, whistle=1400)
    ev = ev_buf(L, tail=4.0)
    t0 = 0.0
    while t0 < L:
        t0 += rng.uniform(2.5, 6.0)
        ns = N(0.4)
        sl = lp(rng.standard_normal(ns), 700) * np.exp(-np.linspace(0, 5, ns))
        place(ev, pan(P(sl) * rng.uniform(0.3, 1.0), rng.uniform(-0.8, 0.8)), t0)
    ev = P(circ_reverb(fold(ev, n), make_ir(2.2, rng, hf=0.3), 0.8))
    base = river + undb(-5) * w
    return base + rise(ev, base, +2.0)


def amb_berlin_hall(rng, L):
    n = N(L)
    t = t_axis(n)
    duct = static_bed(L, rng, [(20, -10), (40, 0), (120, -2), (500, -8), (2000, -18), (6000, -36), (20000, -70)], corr=0.5)
    fanh = np.sin(TAU * F(87.0, L) * t) + 0.5 * np.sin(TAU * F(174.0, L) * t) + 0.25 * np.sin(TAU * F(261.0, L) * t)
    ev = ev_buf(L, tail=8.0)
    kinds = ["door", "clank", "steps", "door", "clank"]
    for t0, kind in zip(np.sort(rng.uniform(1, L - 5, len(kinds))), kinds):
        if kind == "door":
            x = M.thud(rng, 75, 0.6, 0.25, 1.3, noise=0.4) + 0.5 * M.wood_knock(rng, 120, 0.6, hard=0.5, t60=0.2)
        elif kind == "clank":
            x = M.metal_clunk(rng, rng.uniform(180, 260), 0.8, bright=0.8)
        else:
            x = np.zeros(N(2.5))
            for k in range(5):  # distant footsteps (hard soles) in the hall
                place(x, 0.6 * M.thud(rng, 140, 0.15, 0.05, 1.2, noise=0.6), k * 0.55)
                place(x, 0.3 * M.glass_clink(2500, rng, dur=0.1, damp=0.3), k * 0.55)
        place(ev, pan(P(lp(x, 2500)) * rng.uniform(0.5, 1.0), rng.uniform(-0.9, 0.9)), t0)
    ev = P(circ_reverb(fold(ev, n) * 0.15, make_ir(4.5, rng, hf=0.35, predelay=0.06), 1.6))
    base = duct + undb(-14) * pan(R(fanh), 0)
    return base + rise(ev, base, +4.0)


def amb_margin(rng, L):
    n = N(L)
    t = t_axis(n)
    y = np.zeros((2, n))
    for f, d, a in [(41.2, 0.25, 1.0), (61.8, 0.183, 0.6), (82.4, 0.317, 0.45), (123.6, 0.15, 0.2), (164.8, 0.4, 0.1)]:
        sw = periodic(L, n, 0.04, rng, 0.6, 1.0)
        y += np.stack([a * sw * np.sin(TAU * F(f, L) * t + rng.uniform(0, TAU)),
                       a * sw * np.sin(TAU * F(f + d, L) * t + rng.uniform(0, TAU))])
    for f, d, a in [(41.2, 0.25, 1.0), (61.8, 0.183, 0.6), (82.4, 0.317, 0.45)]:
        for h, ah in [(3, 0.08), (4, 0.05), (6, 0.025)]:
            sw = periodic(L, n, 0.04, rng, 0.4, 1.0)
            y += np.stack([a * ah * sw * np.sin(TAU * F(h * f, L) * t + rng.uniform(0, TAU)),
                           a * ah * sw * np.sin(TAU * F(h * (f + d), L) * t + rng.uniform(0, TAU))])
    y = P(y)
    y = y + 0.2 * np.tanh(2 * y)
    gl = np.zeros((2, n))
    for f in [523.25 * 1.007, 659.26 * 0.994, 987.77 * 1.004]:
        sw = periodic(L, n, 0.05, rng, 0.0, 1.0, power=4.0)
        gl += pan(sw * np.sin(TAU * F(f, L) * t + rng.uniform(0, TAU)), rng.uniform(-0.8, 0.8))
    air = static_bed(L, rng, [(20, -40), (300, -12), (1500, 0), (5000, -10), (14000, -30), (20000, -60)])
    return R(y) + undb(-22) * R(gl) + undb(-26) * air


def best_seam_rotation(x):
    """Rotate a circular loop so the file boundary lands where the texture is most broadband (most
    2 kHz+ energy relative to its level) and no event is sounding. Vorbis codes the end and the start
    of the file independently, so their tiny coding errors meet at the wrap; broadband texture masks
    that best. Ambience can start anywhere, so this costs nothing musically."""
    m = x.mean(axis=0)
    h = circ_sosfilt(sos("highpass", 2000.0, 2), m[None, :], N(0.5))[0]
    w = N(0.02)
    k = len(m) // w
    hr = np.sqrt(np.mean(h[:k * w].reshape(k, w) ** 2, axis=1))
    tr = np.sqrt(np.mean(m[:k * w].reshape(k, w) ** 2, axis=1)) + 1e-12
    score = hr / tr
    score[tr > np.median(tr) * 1.4] = 0  # keep away from events/transients
    i = int(np.argmax(score))
    return np.roll(x, -(i * w + w // 2), axis=-1)


SCENES = {
    # id: (fn, loop seconds, target integrated LUFS). The broadband outdoor beds sit 1-1.5 dB lower than they first
    # did so the player's footsteps read over rain, wind and water (see MIX_EQ).
    "amb_shop": (amb_shop, 52.0, -28.0),
    "amb_apartment": (amb_apartment, 52.0, -29.0),
    "amb_apartment_corrupt": (amb_apartment_corrupt, 52.0, -30.0),
    "amb_egypt": (amb_egypt, 56.0, -27.5),
    "amb_paris_rain": (amb_paris_rain, 56.0, -27.5),
    "amb_ny_harbour": (amb_ny_harbour, 60.0, -28.0),
    "amb_japan_night": (amb_japan_night, 52.0, -29.0),
    "amb_riyadh_wind": (amb_riyadh_wind, 52.0, -28.0),
    "amb_india_garden": (amb_india_garden, 52.0, -28.0),
    "amb_rio_frozen": (amb_rio_frozen, 48.0, -30.0),
    "amb_london_river": (amb_london_river, 56.0, -28.0),
    "amb_berlin_hall": (amb_berlin_hall, 56.0, -28.0),
    "amb_margin": (amb_margin, 60.0, -28.0),
}

# Footstep-friendly mix: a wide, gentle dip where hard-soled steps carry (1.5-4 kHz) in the broadband beds, applied
# circularly after rotation and before loudness normalisation. [(centre Hz, gain dB, Q)]
MIX_EQ = {
    "amb_paris_rain": [(2600.0, -3.0, 0.7)],
    "amb_egypt": [(2600.0, -3.0, 0.7)],
    "amb_riyadh_wind": [(2600.0, -2.5, 0.7)],
    "amb_london_river": [(2600.0, -2.5, 0.7)],
    "amb_ny_harbour": [(2600.0, -2.0, 0.7)],
    "amb_japan_night": [(2600.0, -2.0, 0.7)],
    "amb_shop": [(2600.0, -2.0, 0.7)],
}


def peak_sos(f, gain_db, q):
    """RBJ peaking biquad as SOS (so it can run circularly with circ_sosfilt)."""
    from scipy.signal import tf2sos
    A = 10 ** (gain_db / 40)
    w = TAU * f / SR
    al = np.sin(w) / (2 * q)
    b = np.array([1 + al * A, -2 * np.cos(w), 1 - al * A])
    a = np.array([1 + al / A, -2 * np.cos(w), 1 - al / A])
    return tf2sos(b / a[0], a / a[0])


def apply_mix(sid, x):
    for f, g, q in MIX_EQ.get(sid, []):
        x = circ_sosfilt(peak_sos(f, g, q), x, N(2.0))
    return x


def main(only):
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(WORK, exist_ok=True)
    meta = {}
    if os.path.exists(META):
        with open(META, encoding="utf-8") as fh:
            meta = json.load(fh)
    for sid, (fn, L, target) in SCENES.items():
        if only and sid not in only:
            continue
        rng = rng_for(sid)
        x = np.atleast_2d(fn(rng, L))
        if x.shape[0] == 1:
            x = np.repeat(x, 2, axis=0)
        assert x.shape[1] == N(L), (sid, x.shape, N(L))
        x = circ_sosfilt(sos("highpass", 22.0, 2), x, N(2.0))
        x = x - x.mean(axis=1, keepdims=True)
        x = best_seam_rotation(x)
        x, _ = rotate_to_zero_crossing(x)
        x = apply_mix(sid, x)
        finish(sid, x, L, target, meta)
    with open(META, "w", encoding="utf-8") as fh:
        json.dump(meta, fh, indent=1)


def finish(sid, x, L, target, meta):
    y = norm_lufs(x, target, tp_max=-3.0, circular=True)
    wav = os.path.join(WORK, sid + ".wav")
    sf.write(wav, y.T.astype(np.float32), SR, subtype="FLOAT")
    ogg = os.path.join(OUT, sid + ".ogg")
    cmd = encode_ogg(wav, ogg, 5)
    rel = lambda c: os.path.relpath(c, ROOT).replace(os.sep, "/") if os.path.isabs(c) else c  # noqa: E731
    meta[sid] = {"bus": "Ambience", "loop": True, "channels": 2, "loop_seconds": L, "target_lufs": target,
                 "mix_eq": MIX_EQ.get(sid, []),
                 "synth_lufs": round(lufs(y), 2), "synth_tp": round(true_peak_db(y), 2), "seam": seam_report(y),
                 "source": "synth:Tools/audio/make_ambience.py", "license": LIC,
                 "encode": " ".join(rel(c) for c in cmd)}
    print(f"{sid:24s} L={L:4.1f}s LUFS={meta[sid]['synth_lufs']:6.2f} TP={meta[sid]['synth_tp']:6.2f} "
          f"eq={meta[sid]['mix_eq']} seam={meta[sid]['seam']}", flush=True)


def remaster(only):
    """Fast path: re-apply the mix stage (MIX_EQ + target) to the existing float masters in Audio/_work/amb instead of
    re-synthesising. Equivalent to a full run (the EQ is linear and circular; masters are already rotated). A master
    whose recorded EQ differs from MIX_EQ is skipped: run the full generator for it."""
    meta = {}
    if os.path.exists(META):
        with open(META, encoding="utf-8") as fh:
            meta = json.load(fh)
    for sid, (fn, L, target) in SCENES.items():
        if only and sid not in only:
            continue
        wav = os.path.join(WORK, sid + ".wav")
        if not os.path.exists(wav):
            print(f"{sid}: no float master, run the full generator", flush=True)
            continue
        x, _ = sf.read(wav, always_2d=True)
        x = x.T.astype(np.float64)
        done = [list(e) for e in meta.get(sid, {}).get("mix_eq", [])]
        want = [list(e) for e in MIX_EQ.get(sid, [])]
        if done and done != want:
            print(f"{sid}: master already carries EQ {done} (wanted {want}); skipped - run the full generator", flush=True)
            continue
        if not done:
            x = apply_mix(sid, x)
        finish(sid, x, L, target, meta)
    with open(META, "w", encoding="utf-8") as fh:
        json.dump(meta, fh, indent=1)


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--remaster" in args:
        remaster(set(a for a in args if a != "--remaster"))
    else:
        main(set(args))
