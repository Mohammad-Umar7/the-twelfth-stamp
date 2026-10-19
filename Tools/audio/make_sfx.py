"""SFX for The Twelfth Stamp: original physically-inspired synthesis only (no samples, no third-party audio).

  python -I Tools/audio/make_sfx.py                 # everything
  python -I Tools/audio/make_sfx.py ui_click pp_open
  python -I Tools/audio/make_sfx.py --prefix breath_ foot_   # every id starting with these

Output: Game/Assets/Resources/Audio/SFX/<id>.wav (48 kHz PCM16, mono unless noted; TPDF dither)
Modules: sfx_steps.py (player footsteps: walk sets, running layers, wet layer), sfx_body.py (breathing,
cloth, heartbeats), sfx_shift.py (world-shift transitions, chase stingers; stereo).
Levels: one-shots are loudness-normalised on their peak short-window loudness (dsp.loud_max: K-weighted
energy of the loudest 100 ms, LUFS-scaled) to a per-category target, true peak <= -2 dBTP (no limiting);
re-levelled older clips move at most -10/+6 dB from where -3 dBTP peak normalisation put them. Sustained
loops are loudness-normalised (LUFS target per loop, peaks <= -3 dBTP) so a hum does not dominate at 1.0.
Level-matched pairs: sunbeam_on uses sunbeam_loop's gain, fridge_stop uses fridge_hum_loop's gain.
Loops: rendered circularly (event tails fold to the start, partial frequencies are multiples of
1/L, noise beds use a correlation-aware equal-power tail->head crossfade), then rotated to start
on an upward zero crossing. Metadata for the manifest: Audio/_work/sfx_meta.json
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402

import materials as M  # noqa: E402
import sfx_body  # noqa: E402
import sfx_shift  # noqa: E402
import sfx_steps  # noqa: E402
from dsp import (SR, TAU, N, t_axis, rng_for, place, fade, env_points, exp_env, ar_env, colored, shaped_noise,  # noqa: E402
                 smooth_random, lp, hp, bp, peak_eq, shelf, resonator, resonator_bank, tv_lowpass, impulse_train,
                 poisson_clicks, grains, crackle, click, modal, glide_tone, osc, additive, bell, membrane, make_ir,
                 convolve, reverb, pan, fold, loop_crossfade, rotate_to_zero_crossing, seam_report, make_loop,
                 lufs, true_peak_db, norm_peak, norm_lufs, loud_max, norm_loud, write_wav, undb, db, mono)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Game", "Assets", "Resources", "Audio", "SFX")
META = os.path.join(ROOT, "Audio", "_work", "sfx_meta.json")

SYNTH_SRC = "synth:Tools/audio/make_sfx.py"
SYNTH_LIC = "Original synthesis for The Twelfth Stamp (project-owned; no third-party material)"


def pk(x):
    return x / (np.max(np.abs(x)) + 1e-12)


def Z(sec):
    return np.zeros(N(sec))


def F(f, L):
    """Quantise a frequency to a whole number of cycles per loop length L (seamless partials)."""
    return round(f * L) / L


def room(rt60, rng, hf=0.45, stereo=False):
    return make_ir(rt60, rng, hf=hf, stereo=stereo, predelay=0.004)


# =============================================================================== UI
def ui_hover(rng):
    x = M.paper_tick(rng, dur=0.08, bright=1.0, events=3, spread=0.010)
    return lp(x, 6500)


def ui_click(rng):
    n = N(0.12)
    exc = np.zeros(n)
    c = click(rng, 0.0025, 500, 9000)
    exc[:len(c)] = c
    modes = resonator_bank(exc, [920, 2150, 3480, 5200], [0.035, 0.024, 0.016, 0.01], [1, .6, .35, .15])
    return 0.8 * pk(modes) + 0.45 * pk(exc) + 0.45 * pk(M.thud(rng, 210, 0.12, 0.05, 1.15, noise=0.2))


def ui_back(rng):
    n = N(0.15)
    x = np.zeros(n)
    for t0, sc, g in [(0.0, 1.0, 1.0), (0.042, 0.82, 0.45)]:
        exc = np.zeros(n)
        place(exc, click(rng, 0.003, 400, 7000), t0)
        modes = resonator_bank(exc, [680 * sc, 1520 * sc, 2650 * sc], [0.04, 0.028, 0.016], [1, .55, .25])
        x += g * (0.8 * pk(modes) + 0.35 * pk(exc))
    x += 0.35 * pk(M.thud(rng, 160, 0.15, 0.06, 1.2, noise=0.15))
    return x


def ui_confirm(rng):
    n = N(0.4)
    x = np.zeros(n)
    for t0, f, g in [(0.0, 392.0, 0.8), (0.075, 587.33, 1.0)]:
        tone = M.warm_tone(f, 0.4, rng, attack=0.003, t60=0.9,
                           partials=((1, 1.0), (2, 0.12), (3.93, 0.10), (9.2, 0.025)), t60_scale=(1.0, 0.5, 0.25, 0.08))
        place(x, pk(tone) * g, t0)
        mallet = lp(rng.standard_normal(N(0.004)), 2500) * np.hanning(N(0.004))
        place(x, 0.06 * pk(mallet), t0)
    return reverb(x, room(0.5, rng), wet=0.18)


def ui_slider_tick(rng):
    n = N(0.05)
    exc = np.zeros(n)
    c = click(rng, 0.0012, 1500, 12000)
    exc[:len(c)] = c
    modes = resonator_bank(exc, [2600, 4700, 7300], [0.01, 0.007, 0.005], [1, .5, .25])
    return 0.7 * pk(modes) + 0.55 * pk(exc) + 0.25 * pk(M.thud(rng, 380, 0.05, 0.012, 1.1, noise=0.1))


# =============================================================================== passport
def pp_open(rng):
    x = Z(0.6)
    nc = N(0.46)
    rate = env_points([(0, 35), (0.12, 70), (0.28, 95), (0.46, 55)], nc)
    env = env_points([(0, 0), (0.05, 0.7), (0.12, 1.0), (0.3, 0.8), (0.46, 0)], nc)
    place(x, pk(M.leather_creak(nc, rate, rng, env=env)), 0.0)
    npg = N(0.42)
    penv = env_points([(0, 0), (0.12, 0.6), (0.22, 1.0), (0.42, 0)], npg)
    pages = M.paper_crackle(npg, 900 * penv + 20, rng)
    sw = M.paper_swish(npg, penv, rng, 1500, 3800, 2200)
    place(x, 0.4 * pk(pages) + 0.35 * pk(sw), 0.14)
    place(x, 0.28 * pk(M.thud(rng, 140, 0.1, 0.04, 1.2, noise=0.3)), 0.5)
    return x


def pp_close(rng):
    x = Z(0.5)
    na = N(0.3)
    air = M.air_rush(na, env_points([(0, 0), (0.25, 1), (0.3, 0.2)], na) ** 1.5, rng, 300, 900)
    place(x, 0.3 * pk(air), 0.0)
    place(x, 1.0 * pk(M.thud(rng, 150, 0.22, 0.07, 1.35, noise=0.4)), 0.27)
    place(x, 0.5 * pk(M.flap(rng, 0.04, 600)), 0.268)
    nc = N(0.12)
    cr = M.leather_creak(nc, env_points([(0, 60), (0.12, 40)], nc), rng, env=env_points([(0, 0), (0.02, 1), (0.12, 0)], nc))
    place(x, 0.12 * pk(cr), 0.3)
    return x


def pp_page(rng):
    x = Z(0.4)
    ns = N(0.3)
    env = env_points([(0, 0), (0.04, 0.35), (0.15, 1.0), (0.26, 0.5), (0.3, 0)], ns)
    sw = M.paper_swish(ns, env, rng, 1600, 3800, 2300, q=1.4, peak_at=0.5)
    cr = M.paper_crackle(ns, 1400 * env + 100, rng)
    place(x, 0.7 * pk(sw) + 0.45 * pk(cr), 0.0)
    place(x, 0.42 * pk(M.flap(rng, 0.05, 450)), 0.31)
    return lp(x, 10000)


def pp_tab(rng):
    x = M.paper_tick(rng, dur=0.1, bright=1.2, events=2, spread=0.006)
    fl = bp(rng.standard_normal(N(0.012)), 3000, 8000) * np.hanning(N(0.012))
    place(x, 0.3 * pk(fl) * np.max(np.abs(x)), 0.008)
    return lp(x, 8500)


def pp_seam_shimmer(rng):
    n = N(1.2)
    t = t_axis(n)
    env = env_points([(0, 0), (0.35, 1.0), (0.7, 0.8), (1.2, 0)], n) ** 1.5
    freqs = [2637.0, 3135.9, 3951.1, 4698.6, 5919.9, 7040.0]
    y = np.zeros(n)
    for i, f in enumerate(freqs):
        am = 0.55 + 0.45 * np.sin(TAU * (6 + 1.7 * i) * t + rng.uniform(0, TAU))
        drift = 1 + 0.002 * np.sin(TAU * 0.7 * t + rng.uniform(0, TAU))
        y += (0.8 ** i) * am * np.sin(TAU * f * np.cumsum(drift) / SR)
    air = lp(hp(colored(n, rng, -1.0), 6000), 13000)
    x = (pk(y) + 0.06 * pk(air)) * env
    return reverb(x, make_ir(0.9, rng, hf=0.8, stereo=False), wet=0.3)


# =============================================================================== tear
def tear_open_slit(rng):
    x = Z(0.8)
    nr = N(0.34)
    env = env_points([(0, 0), (0.012, 1.0), (0.12, 0.75), (0.22, 0.9), (0.34, 0)], nr)
    place(x, pk(lp(M.rip(nr, env, rng, rate=2600, lo=1200, hi=9500, body=(2800,), fibres=4), 8500)), 0.0)
    g = M.glass_ring(740, 0.8, rng, amps=(1, .5, .25, .12, .05), t60=(1.5, .9, .5, .3, .2), split=1.5, attack=0.03)
    place(x, 0.35 * pk(g), 0.01)
    n = N(0.8)
    x += 0.08 * pk(M.air_rush(n, env_points([(0, 0), (0.1, 1), (0.5, 0.3), (0.8, 0)], n), rng, 800, 1600))
    return x


def tear_open_window(rng):
    x = Z(1.2)
    nr = N(0.72)
    env = env_points([(0, 0), (0.015, 1.0), (0.2, 0.7), (0.32, 1.0), (0.5, 0.8), (0.72, 0)], nr)
    place(x, pk(lp(M.rip(nr, env, rng, rate=3600, lo=600, hi=8500, body=(1700, 3400), fibres=10), 7500)), 0.0)
    g = M.glass_ring(520, 1.2, rng, amps=(1, .5, .25, .12, .05), t60=(2.0, 1.2, .7, .4, .25), split=1.3, attack=0.05)
    place(x, 0.38 * pk(g), 0.02)
    n = N(1.15)
    air = M.air_rush(n, env_points([(0, 0), (0.15, 0.6), (0.6, 1.0), (1.15, 0)], n), rng, 600, 2500)
    place(x, 0.22 * pk(air), 0.05)
    t = t_axis(N(1.2))
    sub = np.sin(TAU * 52 * t) * env_points([(0, 0), (0.45, 1), (1.2, 0)], N(1.2)) ** 2
    x += 0.16 * sub
    return x


def tear_open_door(rng):
    x = Z(1.8)
    nr = N(1.1)
    env = env_points([(0, 0), (0.02, 0.9), (0.18, 0.6), (0.35, 1.0), (0.6, 0.75), (0.85, 0.9), (1.1, 0)], nr)
    deep = M.rip(nr, env, rng, rate=4200, lo=280, hi=6500, body=(380, 950, 2300), fibres=16, jerk_hz=18)
    place(x, pk(lp(deep, 6000)), 0.0)
    g = M.glass_ring(330, 1.8, rng, amps=(1, .55, .3, .14, .06), t60=(3.0, 1.8, 1.0, .6, .35), split=1.0, attack=0.08)
    place(x, 0.4 * pk(g), 0.03)
    n = N(1.65)
    air = M.air_rush(n, env_points([(0, 0), (0.25, 0.5), (0.8, 1.0), (1.3, 0.6), (1.65, 0)], n), rng, 400, 3000, curve=0.7)
    place(x, 0.35 * pk(air), 0.12)
    nn = N(1.8)
    t = t_axis(nn)
    sw = env_points([(0, 0), (0.6, 1), (1.8, 0)], nn) ** 2
    x += 0.2 * (np.sin(TAU * 41 * t) + 0.5 * np.sin(TAU * 61.5 * t)) * sw / 1.5
    return x


def tear_seal(rng):
    x = Z(1.0)
    nz = N(0.42)
    rate = env_points([(0, 35), (0.42, 140)], nz)
    teeth = impulse_train(nz, rate, rng, jitter=0.15, amp_sigma=0.3)
    zt = resonator_bank(teeth, [2800, 5100, 7600], [0.008, 0.006, 0.004], [1, .6, .3])
    zt = pk(zt) + 0.4 * pk(bp(grains(teeth, rng, 0.0008), 2000, 9000))
    fric = M.slide_noise(nz, env_points([(0, 0), (0.05, 0.6), (0.42, 1.0)], nz), rng, 1200, 5000, 300)
    zipper = (1.0 * pk(zt) + 0.35 * pk(fric)) * env_points([(0, 0.45), (0.38, 1), (0.42, 0.3)], nz)
    place(x, zipper, 0.0)
    place(x, 0.8 * pk(click(rng, 0.002, 900, 12000)), 0.45)
    place(x, 0.45 * pk(M.glass_ring(2100, 0.4, rng, t60=(0.3, 0.2, 0.12, 0.08, 0.05), split=2)), 0.45)
    place(x, 0.5 * pk(M.thud(rng, 90, 0.3, 0.1, 1.3, noise=0.4)), 0.53)
    wet = lp(rng.standard_normal(N(0.06)), 700) * np.exp(-np.linspace(0, 5, N(0.06)))
    place(x, 0.25 * pk(wet), 0.53)
    nt = N(0.42)
    tail = glide_tone(nt, 880, 830, harmonics=((1, 1), (2.83, 0.3), (5.42, 0.1))) * exp_env(nt, 0.35)
    place(x, 0.12 * pk(tail), 0.47)
    return x


def tear_breach(rng):
    x = Z(1.0)
    place(x, 1.0 * pk(M.glass_crack(rng, 0.8, f0=2300, cascade=0.15)), 0.0)
    place(x, 0.8 * pk(M.thud(rng, 58, 0.6, 0.35, 1.5, noise=0.25)), 0.0)
    low = lp(rng.standard_normal(N(0.2)), 200) * exp_env(N(0.2), 0.15)
    place(x, 0.3 * pk(low), 0.0)
    place(x, 0.28 * pk(M.glass_ring(1250, 1.0, rng, t60=(0.9, 0.5, 0.3, 0.2, 0.1), split=3)), 0.01)
    place(x, M.tinkles(rng, 0.8, pieces=3, t_start=0.18, spread=0.3, level=0.12), 0.0)
    return lp(x, 8500)


def tear_hum_loop(rng):
    L = 6.0
    n = N(L)
    t = t_axis(n)
    core = [(73.5, 1.0), (147.0, 0.32), (220.5, 0.14), (294.0, 0.06)]
    glass = [(73.5 * 2.83, 0.45), (73.5 * 5.42, 0.25), (73.5 * 8.77, 0.12), (73.5 * 12.9, 0.06)]
    am1 = 1 + 0.1 * np.sin(TAU * t / L)
    am2 = 0.75 + 0.25 * np.sin(TAU * 2 * t / L + 1.0)
    chans = []
    ph = rng.uniform(0, TAU, 32)
    for ch in (0, 1):
        y = sum(a * np.sin(TAU * F(f, L) * t + ph[i]) for i, (f, a) in enumerate(core)) * am1
        for i, (f, a) in enumerate(glass):
            y += a * am2 * np.sin(TAU * (F(f, L) + ch * (i + 1) / L) * t + ph[8 + i + 4 * ch])
        chans.append(y)
    tone = np.stack(chans)
    Fx = N(2.0)
    air = np.stack([bp(colored(n + Fx, rng, -2.0), 250, 2500) for _ in range(2)])
    air = loop_crossfade(air, n, Fx)
    x = tone / np.max(np.abs(tone)) + 0.05 * air / np.max(np.abs(air))
    return x - x.mean(axis=1, keepdims=True)


def tear_stretch_loop(rng):
    L = 4.0
    n = N(L)
    buf = np.zeros(N(L + 1.2))
    for t0, d, r0, r1, f0 in [(0.15, 0.7, 18, 45, 420), (1.25, 0.55, 55, 26, 520), (2.05, 0.8, 22, 38, 380),
                              (3.1, 0.6, 40, 20, 470)]:
        nn = N(d)
        rate = env_points([(0, r0), (d, r1)], nn)
        env = env_points([(0, 0), (d * 0.2, 1.0), (d * 0.7, 0.8), (d, 0)], nn)
        modes = [(f0, 0.05, 1.0), (f0 * 2.33, 0.035, 0.6), (f0 * 4.5, 0.025, 0.35), (f0 * 7.4, 0.015, 0.2)]
        from dsp import stick_slip
        cr = stick_slip(nn, rate, rng, modes, jitter=0.3, amp_sigma=0.45, noise=0.12) * env
        place(buf, pk(cr) * rng.uniform(0.7, 1.0), t0)
    ticks = crackle(len(buf), 28, rng, lo=2000, hi=8000, grain=0.0006)
    buf += 0.25 * pk(ticks)
    ev = fold(buf, n)
    Fx = N(1.0)
    nn = n + Fx
    groan_rate = 13 + 3 * np.sin(TAU * np.arange(nn) / nn)
    from dsp import stick_slip
    groan = stick_slip(nn, groan_rate, rng, [(160, 0.08, 1.0), (340, 0.06, 0.6), (720, 0.04, 0.25)], jitter=0.2,
                       amp_sigma=0.3, noise=0.05)
    groan = loop_crossfade(groan[None, :], n, Fx)[0]
    hissb = loop_crossfade(bp(colored(nn, rng, -1.0), 200, 900)[None, :], n, Fx)[0]
    x = pk(ev) + 0.3 * pk(groan) + 0.05 * pk(hissb)
    return x - x.mean()


# =============================================================================== stamp
def _press(rng, weight=1.0):
    x = Z(0.4)
    place(x, 1.0 * pk(M.thud(rng, 120 - 25 * (weight - 1), 0.25, 0.08 + 0.05 * (weight - 1), 1.25, noise=0.3)), 0.005)
    exc = Z(0.25)
    place(exc, click(rng, 0.002, 200, 5000), 0)
    desk = resonator_bank(exc, [185, 410, 760], [0.12, 0.07, 0.04], [1, .6, .3])
    place(x, 0.45 * pk(desk), 0.005)
    thw = bp(rng.standard_normal(N(0.03)), 250, 1400) * np.exp(-np.linspace(0, 6, N(0.03)))
    place(x, 0.55 * weight * pk(thw), 0.004)
    slap = bp(rng.standard_normal(N(0.02)), 1200, 5000) * np.exp(-np.linspace(0, 8, N(0.02)))
    place(x, 0.18 * pk(slap), 0.004)
    return lp(x, 7000)


def stamp_hit(rng):
    x = Z(0.35)
    place(x, _press(rng), 0.0)
    peel = bp(rng.standard_normal(N(0.03)), 2000, 6000) * np.hanning(N(0.03))
    place(x, 0.05 * pk(peel), 0.17)
    return x


def _chime_notes(rng, notes, dur, detune=0.0, sag_cents=0.0, split=0.8, stretch=1.0, t60=1.4):
    n = N(dur)
    t = t_axis(n)
    x = np.zeros(n)
    sag = 2 ** ((sag_cents / 1200) * np.clip(t / dur, 0, 1))
    for (t0, f, g) in notes:
        y = np.zeros(n)
        parts = [(1.0, 1.0, 1.0), (2.0 * stretch, 0.28, 0.6), (3.0 * stretch ** 1.5, 0.11, 0.35), (4.2 * stretch ** 2, 0.05, 0.2)]
        for r, a, s in parts:
            for sgn in (-0.5, 0.5):
                fr = f * r * (1 + detune * rng.uniform(-1, 1)) + sgn * split * r
                ph = TAU * np.cumsum(fr * sag) / SR
                y += 0.5 * a * np.exp(-6.9078 * np.maximum(t - t0, 0) / (t60 * s)) * np.sin(ph + rng.uniform(0, TAU))
        y[t < t0] = 0
        k = N(0.004)
        i0 = N(t0)
        y[i0:i0 + k] *= np.sin(np.linspace(0, np.pi / 2, k)) ** 2
        x += g * y
    return x


def seal_verify(rng):
    x = Z(1.6)
    place(x, 0.55 * pk(_press(rng)), 0.0)
    ch = _chime_notes(rng, [(0.05, 523.25, 1.0), (0.11, 659.26, 0.8), (0.17, 783.99, 0.7)], 1.6, split=0.6, t60=1.5)
    x += 0.9 * pk(ch)
    return reverb(x, room(0.9, rng), wet=0.25)


def seal_counterfeit(rng):
    x = Z(2.2)
    place(x, 0.55 * pk(_press(rng)), 0.0)
    ch = _chime_notes(rng, [(0.05, 523.25, 1.0), (0.12, 739.99, 0.85), (0.2, 932.33 * 0.985, 0.6)], 2.2,
                      detune=0.02, sag_cents=-45, split=4.5, stretch=1.035, t60=1.8)
    n = N(2.2)
    t = t_axis(n)
    under = np.sin(TAU * 261.63 * 2 ** (-30 / 1200) * t) * env_points([(0, 0), (0.3, 1), (2.2, 0)], n) ** 1.5
    rm = ch * (0.8 + 0.2 * np.sin(TAU * 37 * t))
    x += 0.85 * pk(rm) + 0.12 * under
    return reverb(x, room(1.4, rng), wet=0.3)


def stamp_void(rng):
    x = Z(0.6)
    place(x, pk(_press(rng, weight=1.6)), 0.0)
    nc = N(0.35)
    cr = M.crumple(nc, env_points([(0, 1.0), (0.1, 0.6), (0.35, 0)], nc), rng, rate=3200, lo=400, hi=5000)
    place(x, 0.5 * pk(lp(cr, 8000)), 0.01)
    place(x, 0.12 * pk(M.metal_click(rng, 3100, 0.05)), 0.09)
    place(x, 0.08 * pk(M.metal_click(rng, 3700, 0.05)), 0.14)
    return x


# =============================================================================== light
def _sun_layer(n, rng, L=None):
    q = (lambda f: F(f, L)) if L else (lambda f: f)
    t = t_axis(n)
    notes = [110.0, 164.81, 220.0, 277.18, 329.63, 440.0]
    amps = [0.5, 0.45, 0.6, 0.4, 0.35, 0.18]
    y = np.zeros(n)
    for f, a in zip(notes, amps):
        for d in (-0.6, 0.6):
            fd = q(f + d)
            y += a * 0.5 * (np.sin(TAU * fd * t + rng.uniform(0, TAU)) + 0.25 * np.sin(TAU * 2 * fd * t + rng.uniform(0, TAU))
                            + 0.08 * np.sin(TAU * 3 * fd * t))
    lfo = 1 + 0.12 * np.sin(TAU * q(0.25) * t)
    y = y * lfo
    return y


def _sun_air(n, rng):
    return bp(colored(n, rng, -2.0), 600, 4000)


SUN_REF = {}


def sunbeam_loop(rng):
    L = 6.0
    n = N(L)
    tone = _sun_layer(n, rng, L)
    Fx = N(2.0)
    air = loop_crossfade(_sun_air(n + Fx, rng)[None, :], n, Fx)[0]
    x = tone / 4.0 + 0.12 * air / np.std(air) * np.std(tone / 4.0)
    return x - x.mean()


def sunbeam_on(rng):
    n = N(1.5)
    tone = _sun_layer(n, rng) / 4.0
    air = _sun_air(n, rng)
    x = tone + 0.12 * air / np.std(air) * np.std(tone)
    env = env_points([(0, 0), (0.25, 0.08), (0.8, 0.85), (1.0, 1.0), (1.2, 0.8), (1.5, 0)], n)
    env = env ** 1.4
    sh = np.zeros(n)
    t = t_axis(n)
    for i, f in enumerate([1760.0, 2217.5, 2637.0, 3520.0]):
        sh += (0.7 ** i) * np.sin(TAU * f * t + rng.uniform(0, TAU)) * (0.6 + 0.4 * np.sin(TAU * (5 + i) * t))
    sh *= env_points([(0, 0), (0.5, 1), (1.0, 0.3), (1.5, 0)], n) ** 2
    return x * env + 0.05 * np.std(tone) * 6 * pk(sh)


def sun_sizzle(rng):
    n = N(1.0)
    env = env_points([(0, 0), (0.06, 1), (0.4, 0.7), (1.0, 0)], n)
    cr = crackle(n, 500 * env + 30, rng, lo=2500, hi=9000, grain=0.0004, amp_sigma=1.1)
    hiss = lp(hp(colored(n, rng, -1.0), 3500), 10000) * smooth_random(n, 60, rng, 0.2, 1.0) * env
    x = 0.8 * pk(cr) + 0.35 * pk(hiss)
    for t0 in np.sort(rng.uniform(0.02, 0.8, 6)):
        ping = M.glass_ring(rng.uniform(5000, 9500), 0.06, rng, t60=(0.04, 0.03, 0.02, 0.015, 0.01), split=5)
        place(x, 0.25 * pk(ping), t0)
    return lp(x, 11000)


# =============================================================================== passenger
def pass_step(rng, idx):
    x = Z(0.3)
    fh = [1850, 2050, 1720][idx] * rng.uniform(0.98, 1.02)
    heel = M.glass_clink(fh, rng, dur=0.3, hard=1.0)
    toe = M.glass_clink(fh * rng.uniform(1.25, 1.45), rng, dur=0.2, hard=0.7, damp=0.8)
    place(x, pk(heel), 0.0)
    place(x, 0.55 * pk(toe), [0.085, 0.10, 0.075][idx])
    place(x, 0.3 * pk(M.thud(rng, 260, 0.08, 0.03, 1.1, noise=0.2)), 0.0)
    place(x, 0.1 * pk(crackle(N(0.05), 600, rng, 3000, 10000)), 0.0)
    return lp(x, 9000)


def pass_creak(rng, idx):
    n = N(0.8)
    r0, r1, r2 = [(14, 40, 22), (25, 12, 30), (18, 55, 35)][idx]
    rate = env_points([(0, r0), (0.4, r1), (0.8, r2)], n)
    env = env_points([(0, 0), (0.08, 0.8), (0.3, 1.0), (0.6, 0.7), (0.8, 0)], n)
    x = M.glass_creak(n, rate, rng, env=env, f0=[1450, 1180, 1720][idx])
    low = M.wood_creak(n, rate * 0.5, rng, env=env, body=(240, 520, 1100, 2100))
    return pk(x) + 0.12 * pk(low)


def pass_breath(rng):
    n = N(2.0)
    t = t_axis(n)
    env = env_points([(0, 0), (0.3, 0.25), (1.2, 0.85), (1.6, 1.0), (1.85, 0.4), (2.0, 0)], n) ** 1.3
    br = M.breath_noise(n, env, rng, inhale=True, mouth=0.8, nose=0.5)
    fw = 1180 * (1 + 0.06 * (t / 2.0))
    ph = TAU * np.cumsum(fw) / SR
    wh = sum(a * np.sin(r * ph + rng.uniform(0, TAU)) for r, a in zip(M.GLASS_RATIOS[:3], (1, 0.35, 0.15)))
    wh = wh * env ** 1.5 * (0.7 + 0.3 * smooth_random(n, 6, rng))
    x = lp(0.85 * pk(br) + 0.3 * pk(wh), 8000)
    return reverb(x, room(1.2, rng), wet=0.25)


def pass_recoil(rng):
    n = N(0.7)
    t = t_axis(n)
    f = env_points([(0, 1650), (0.08, 2050), (0.3, 1900), (0.7, 1500)], n)
    exc = impulse_train(n, f, rng, jitter=0.08, amp_sigma=0.25)
    res = resonator_bank(exc, [1900, 2690, 4100, 5400], [0.05, 0.04, 0.03, 0.02], [1, .5, .4, .2])
    harm = additive(f, n, [(1, 1.0), (2, 0.35), (3, 0.18), (4, 0.08)])
    env = env_points([(0, 0), (0.012, 1.0), (0.12, 0.8), (0.35, 0.45), (0.7, 0)], n)
    rough = 1 + 0.3 * np.sin(TAU * 37 * t)
    scrape = bp(rng.standard_normal(n), 2500, 7000)
    x = (0.6 * pk(res) + 0.45 * pk(harm) * rough + 0.15 * pk(scrape)) * env
    return lp(x, 8500)


def pass_tap(rng):
    x = Z(1.2)
    times = [0.02, 0.17, 0.29, 0.45, 0.77, 0.92]
    vel = [0.8, 0.6, 0.7, 1.0, 0.65, 0.85]
    modes = np.array([190, 415, 770, 1160, 1720, 2590])
    T = [0.12, 0.09, 0.07, 0.05, 0.04, 0.03]
    for t0, v in zip(times, vel):
        e = np.zeros(N(0.25))
        tap = lp(np.hanning(N(0.0025)), 3000)
        e[:len(tap)] = tap
        pane = resonator_bank(e, list(modes * (1 + rng.normal(0, 0.01, 6))), T, [1, .8, .6, .4, .25, .15])
        y = 0.9 * pk(pane) + 0.35 * pk(e)
        if rng.random() < 0.6:  # fingernail tick on some taps
            c = click(rng, 0.0008, 4000, 12000)
            y[:len(c)] += 0.15 * pk(c)
        place(x, v * y, t0)
    return x


def pass_drone_loop(rng):
    L = 8.0
    n = N(L)
    t = t_axis(n)
    comps = [(32.5, 1.0), (33.0, 0.8), (65.0, 0.45), (65.625, 0.3), (97.5, 0.18), (130.0, 0.1), (162.5, 0.05)]
    y = sum(a * np.sin(TAU * F(f, L) * t + rng.uniform(0, TAU)) for f, a in comps)
    y = y * (1 + 0.15 * np.sin(TAU * t / L))
    y = y / np.max(np.abs(y))
    y = y + 0.35 * np.tanh(2.5 * y)  # odd harmonics (97.5, 162.5, 227.5 Hz ...) so small speakers carry it
    Fx = N(2.0)
    rum = loop_crossfade(lp(colored(n + Fx, rng, -6.0), 90, 2)[None, :], n, Fx)[0]
    x = pk(y) + 0.2 * pk(rum)
    return x - x.mean()


def pass_whisper(rng):
    n = N(2.5)
    env = env_points([(0, 0), (0.15, 1), (2.2, 0.8), (2.5, 0)], n)
    w = M.whisper(n, rng, syll_hz=4.8, level_env=env)
    pap = M.paper_crackle(n, 120 * env, rng, lo=1500, hi=7000)
    x = pk(w) + 0.12 * pk(pap)
    return reverb(x, room(0.8, rng), wet=0.22)


def pass_sting(rng):
    n = N(1.5)
    t = t_axis(n)
    cut = 1.42
    cluster = [(1046.5, 1.0), (1108.7, 0.9), (1174.7, 0.8), (1244.5, 0.7), (523.25, 0.6), (554.37, 0.5), (2217.5, 0.3)]
    y = np.zeros(n)
    for f, a in cluster:
        trem = 1 + 0.3 * np.sin(TAU * rng.uniform(5, 9) * t + rng.uniform(0, TAU))
        drift = 1 + 0.004 * np.sin(TAU * rng.uniform(0.3, 0.8) * t + rng.uniform(0, TAU))
        ph = TAU * np.cumsum(f * drift) / SR
        y += a * trem * (np.sin(ph) + 0.25 * np.sin(2.83 * ph))
    sw = np.clip(t / cut, 0, 1) ** 3
    y = tv_lowpass(y * sw, 1500 + 7500 * np.clip(t / cut, 0, 1) ** 2)
    y[t >= cut] = 0
    k = N(0.012)
    i = N(cut)
    y[i - k:i] *= np.linspace(1, 0, k)
    wet = convolve(y, make_ir(0.6, rng, stereo=False))[:n]
    return pk(y) + 0.18 * pk(wet)


# =============================================================================== player (footsteps, breathing: sfx_steps / sfx_body)
def breath_scared(rng):
    x = Z(3.0)
    seq = [(0.05, 0.42, "in", 0.35, 1.0), (0.55, 0.5, "out", 0.12, 0.6), (1.2, 0.38, "in", 0.45, 0.95),
           (1.62, 0.55, "out", 0.15, 0.55), (2.3, 0.36, "in", 0.4, 0.9), (2.7, 0.28, "out", 0.1, 0.5)]
    for t0, d, kind, shake, g in seq:
        nb = N(d)
        if kind == "in":
            env = env_points([(0, 0), (d * 0.3, 0.8), (d * 0.85, 1.0), (d, 0)], nb)
            u = np.linspace(0, 1, nb)
            catches = 1 - 0.55 * np.clip(np.sin(TAU * rng.uniform(2.2, 3.2) * u + rng.uniform(0, 1)), 0, 1) ** 4
            env = env * catches
        else:
            env = env_points([(0, 0), (d * 0.15, 1.0), (d, 0)], nb) ** 1.5
        b = M.breath_noise(nb, env, rng, inhale=(kind == "in"), mouth=0.9, nose=0.5 if kind == "in" else 0.8,
                           shake=shake, shake_hz=rng.uniform(8, 11))
        place(x, g * pk(b), t0)
    x = lp(x, 9000)
    return reverb(x, room(0.4, rng), wet=0.1)


def heartbeat_loop(rng):
    L = 2.0
    n = N(L)
    buf = np.zeros(N(L + 1.0))
    for bt, g in [(0.05, 1.0), (1.045, 0.93)]:
        lub = M.thud(rng, 52, 0.4, 0.13, 1.35, 0.02, noise=0.15)
        lub = lub + 0.3 * pk(bp(lub, 100, 240))
        dub = M.thud(rng, 68, 0.3, 0.09, 1.3, 0.015, noise=0.12)
        dub = dub + 0.3 * pk(bp(dub, 120, 280))
        place(buf, g * pk(lub), bt)
        place(buf, 0.7 * g * pk(dub), bt + 0.28)
    buf = lp(buf, 420)
    x = fold(buf[None, :], n)[0]
    return x - x.mean()


# =============================================================================== props
def grab(rng):
    x = Z(0.15)
    ns = N(0.08)
    place(x, 0.6 * pk(M.slide_noise(ns, env_points([(0, 0), (0.03, 1), (0.08, 0)], ns), rng, 900, 5000, 500)), 0.0)
    place(x, 0.8 * pk(M.thud(rng, 260, 0.06, 0.03, 1.1, noise=0.5)), 0.055)
    nq = N(0.03)
    place(x, 0.22 * pk(M.slide_noise(nq, np.hanning(nq), rng, 2000, 6000, 800)), 0.06)
    return x


def drop_soft(rng):
    x = Z(0.3)
    place(x, pk(lp(M.thud(rng, 110, 0.3, 0.1, 1.3, noise=0.6), 1500)), 0.0)
    cl = lp(rng.standard_normal(N(0.03)), 1800) * np.exp(-np.linspace(0, 5, N(0.03)))
    place(x, 0.35 * pk(cl), 0.0)
    return x


def drop_hard(rng):
    x = Z(0.4)
    place(x, pk(M.wood_knock(rng, 190, 0.4, hard=0.8, t60=0.18)), 0.0)
    place(x, 0.7 * pk(M.thud(rng, 85, 0.3, 0.1, 1.3, noise=0.3)), 0.0)
    place(x, 0.32 * pk(M.wood_knock(rng, 200, 0.2, hard=0.7, t60=0.1)), 0.12)
    place(x, 0.1 * pk(M.wood_knock(rng, 210, 0.1, hard=0.6, t60=0.05)), 0.19)
    return x


def place_socket(rng):
    x = Z(0.3)
    ns = N(0.05)
    place(x, 0.25 * pk(M.slide_noise(ns, env_points([(0, 0), (0.02, 1), (0.05, 0.3)], ns), rng, 800, 4000, 400)), 0.0)
    place(x, 1.0 * pk(M.mech_click(rng, 0.12, f=(1800, 3300, 5200), t60=(0.03, 0.02, 0.012), body=420)), 0.055)
    place(x, 0.4 * pk(M.metal_click(rng, 3500, 0.08)), 0.056)
    place(x, 0.6 * pk(M.thud(rng, 150, 0.15, 0.05, 1.2, noise=0.3)), 0.055)
    return x


def dial_click(rng):
    x = Z(0.1)
    place(x, pk(M.mech_click(rng, 0.06, f=(1600, 2900, 4600), t60=(0.014, 0.01, 0.007), body=520)), 0.0)
    place(x, 0.25 * pk(M.metal_click(rng, 3300, 0.04)), 0.0)
    return lp(x, 9000)


def lever_clunk(rng):
    x = Z(0.5)
    ns = N(0.17)
    scr = M.slide_noise(ns, env_points([(0, 0), (0.04, 0.8), (0.17, 1.0)], ns), rng, 800, 4000, 600)
    from dsp import stick_slip
    sq = stick_slip(ns, env_points([(0, 160), (0.17, 260)], ns), rng, [(1450, 0.03, 1.0), (2900, 0.02, 0.4)], jitter=0.1)
    place(x, 0.3 * pk(scr) + 0.15 * pk(sq), 0.0)
    place(x, pk(M.metal_clunk(rng, 140, 0.33)), 0.17)
    place(x, 0.15 * pk(M.metal_click(rng, 2700, 0.05)), 0.26)
    place(x, 0.08 * pk(M.metal_click(rng, 3100, 0.05)), 0.31)
    return x


def button_press(rng):
    x = Z(0.15)
    place(x, pk(M.mech_click(rng, 0.05, f=(1500, 2900, 4500), t60=(0.018, 0.012, 0.008), body=300)), 0.0)
    place(x, 0.45 * pk(M.mech_click(rng, 0.05, f=(1800, 3400, 5300), t60=(0.012, 0.009, 0.006), body=0)), 0.085)
    return x


def drawer_open(rng):
    x = Z(0.8)
    ns = N(0.62)
    vel = env_points([(0, 0), (0.08, 0.7), (0.35, 1.0), (0.55, 0.6), (0.62, 0.1)], ns)
    rate = 60 + 80 * vel
    cr = M.wood_creak(ns, rate, rng, env=vel, body=(180, 350, 700, 1500))
    sl = M.slide_noise(ns, vel, rng, 150, 1800, 300)
    place(x, 0.55 * pk(cr) + 0.5 * pk(sl), 0.0)
    place(x, 0.9 * pk(M.wood_knock(rng, 160, 0.18, hard=0.5, t60=0.12)), 0.63)
    for t0 in (0.655, 0.69, 0.72):
        place(x, 0.1 * pk(M.wood_knock(rng, rng.uniform(600, 900), 0.05, hard=0.8, t60=0.03)), t0)
    return x


def door_open(rng):
    x = Z(1.2)
    place(x, 0.8 * pk(M.mech_click(rng, 0.08, f=(1200, 2600, 4100), t60=(0.04, 0.03, 0.02), body=260)), 0.0)
    place(x, 0.5 * pk(M.metal_click(rng, 3800, 0.06)), 0.004)
    place(x, 0.25 * pk(M.metal_click(rng, 2400, 0.12, damp=2.5)), 0.05)
    nh = N(0.78)
    env = env_points([(0, 0), (0.08, 0.8), (0.4, 1.0), (0.65, 0.6), (0.78, 0)], nh)
    rate = env_points([(0, 70), (0.4, 180), (0.78, 120)], nh)
    place(x, 0.55 * pk(M.hinge_creak(nh, rate, rng, env=env)), 0.12)
    na = N(1.0)
    place(x, 0.18 * pk(M.air_rush(na, env_points([(0, 0), (0.4, 1), (1.0, 0)], na), rng, 250, 700)), 0.2)
    return reverb(x, room(0.6, rng), wet=0.2)


def door_close(rng):
    x = Z(1.0)
    na = N(0.42)
    place(x, 0.28 * pk(M.air_rush(na, env_points([(0, 0), (0.38, 1), (0.42, 0.2)], na) ** 1.5, rng, 200, 800)), 0.0)
    place(x, pk(M.thud(rng, 80, 0.5, 0.2, 1.4, noise=0.35)), 0.42)
    place(x, 0.7 * pk(M.wood_knock(rng, 130, 0.4, hard=0.6, t60=0.25)), 0.42)
    place(x, 0.4 * pk(M.metal_click(rng, 3300, 0.07)), 0.43)
    return reverb(x, room(0.7, rng), wet=0.25)


def glasses_unfold(rng):
    x = Z(0.4)
    place(x, pk(M.metal_click(rng, 3400, 0.05)), 0.05)
    ns = N(0.12)
    from dsp import stick_slip
    sq = stick_slip(ns, 300, rng, [(5200, 0.01, 1.0), (7300, 0.008, 0.5)], jitter=0.3) * np.hanning(ns)
    place(x, 0.12 * pk(sq), 0.08)
    place(x, 0.85 * pk(M.metal_click(rng, 3900, 0.05)), 0.22)
    return lp(x, 11000)


def case_open(rng):
    x = Z(0.4)
    snap = M.mech_click(rng, 0.06, f=(2400, 4300, 6500), t60=(0.015, 0.01, 0.007), body=0)
    place(x, pk(snap), 0.015)
    place(x, 0.4 * pk(M.metal_click(rng, 3200, 0.05)), 0.016)
    exc = Z(0.2)
    place(exc, click(rng, 0.002, 200, 3000), 0.0)
    hol = resonator_bank(exc, [420, 950], [0.06, 0.04], [1, .5])
    place(x, 0.45 * pk(hol), 0.015)
    ns = N(0.22)
    place(x, 0.1 * pk(M.slide_noise(ns, np.hanning(ns), rng, 600, 3000, 200)), 0.05)
    return x


def case_close(rng):
    x = Z(0.35)
    ns = N(0.1)
    place(x, 0.12 * pk(M.air_rush(ns, env_points([(0, 0), (0.09, 1), (0.1, 0)], ns), rng, 400, 1200)), 0.0)
    place(x, pk(M.mech_click(rng, 0.06, f=(2100, 3800, 6000), t60=(0.02, 0.012, 0.008), body=0)), 0.11)
    place(x, 0.7 * pk(M.thud(rng, 180, 0.12, 0.04, 1.2, noise=0.4)), 0.11)
    exc = Z(0.2)
    place(exc, click(rng, 0.002, 200, 3000), 0.0)
    place(x, 0.4 * pk(resonator_bank(exc, [400, 900], [0.05, 0.03], [1, .5])), 0.11)
    return x


def glasses_break(rng):
    x = Z(1.2)
    place(x, pk(M.glass_crack(rng, 0.6, f0=3800, cascade=0.08)), 0.0)
    place(x, 0.2 * pk(M.metal_click(rng, 2900, 0.06)), 0.004)
    place(x, M.tinkles(rng, 1.2, pieces=6, t_start=0.16, spread=0.75, fmin=3500, fmax=9500, level=0.28), 0.0)
    return lp(x, 13000)


def coin_token(rng):
    x = Z(0.5)
    coin = [2300, 5600, 9400, 12800]

    def hit(g, dur, t60s):
        y = modal(N(dur), [f * rng.uniform(0.99, 1.01) for f in coin], [1, .7, .45, .25], t60s, rng=rng, phase="rand")
        c = click(rng, 0.001, 2000, 14000)
        y[:len(c)] += 0.5 * c
        return g * pk(y)
    place(x, hit(1.0, 0.45, [0.35, 0.25, 0.15, 0.08]), 0.0)
    place(x, 0.45 * pk(M.wood_knock(rng, 420, 0.12, hard=0.9, t60=0.05)), 0.0)
    place(x, hit(0.5, 0.3, [0.2, 0.14, 0.09, 0.05]), 0.11)
    t, rate = 0.2, 9.0
    a = 0.35
    while t < 0.44:
        place(x, hit(a, 0.12, [0.06, 0.04, 0.03, 0.02]), t)
        t += 1.0 / rate
        rate *= 1.32
        a *= 0.86
    return x


def booth_power(rng):
    x = Z(1.5)
    place(x, pk(M.metal_clunk(rng, 300, 0.25, bright=1.2)), 0.03)
    place(x, 0.4 * pk(crackle(N(0.06), 1500, rng, 2000, 11000)), 0.03)
    n = N(1.42)
    t = t_axis(n)
    env = env_points([(0, 0), (0.15, 0.25), (0.9, 0.95), (1.1, 1.0), (1.3, 0.9), (1.42, 0)], n)
    f50 = 50 * (1 + 0.02 * np.clip(t / 0.8, 0, 1))
    ph = TAU * np.cumsum(f50) / SR
    hum = sum(a * np.sin(k * ph) for k, a in [(1, 0.3), (2, 1.0), (3, 0.3), (4, 0.6), (6, 0.45), (8, 0.3), (10, 0.22),
                                              (12, 0.15), (14, 0.1), (16, 0.08), (20, 0.05)])
    buzz = np.tanh(2.5 * hum) - 0.6 * hum
    mag = bp(rng.standard_normal(n), 400, 3000) * np.abs(np.sin(ph)) ** 4  # magnetostriction buzz
    fan = glide_tone(n, 180, 520, curve=0.6, harmonics=((1, 1.0), (2, 0.3)))
    y = (0.75 * pk(hum) + 0.3 * pk(buzz) + 0.12 * pk(mag) + 0.12 * pk(fan)) * env
    place(x, y, 0.08)
    return x


def noisemaker_loop(rng):
    L = 4.0
    n = N(L)
    t = t_axis(n)
    vib = 22 * np.sin(TAU * F(5.5, L) * t) + 35 * np.sin(TAU * F(0.25, L) * t + 1.0)
    f = F(620, L) + vib
    ph = TAU * np.cumsum(f) / SR
    # make the phase wrap exactly: correct the accumulated phase to a whole number of cycles
    total = ph[-1] + TAU * f[-1] / SR
    ph = ph * (np.round(total / TAU) * TAU / total)
    tone = np.sin(ph) + 0.3 * np.sin(2 * ph) + 0.1 * np.sin(3 * ph)
    tone *= 0.8 + 0.2 * np.sin(TAU * F(0.5, L) * t)
    Fx = N(1.0)
    st = colored(n + Fx, rng, -1.0)
    st = st * (0.6 + 0.4 * smooth_random(n + Fx, 8, rng))
    st = loop_crossfade(st[None, :], n, Fx)[0]
    buf = np.zeros(N(L + 0.5))
    cr = crackle(len(buf), 40, rng, lo=800, hi=6000, grain=0.0015, amp_sigma=1.2)
    crk = fold(cr[None, :], n)[0]
    y = 0.55 * pk(tone) + 0.35 * pk(st) + 0.25 * pk(crk)
    from dsp import circ_sosfilt, sos
    y = circ_sosfilt(sos("bandpass", (300, 4000), 2), y, N(0.5))
    y = np.tanh(1.5 * y / np.max(np.abs(y)))
    return y - y.mean()


def lantern_place(rng):
    x = Z(0.4)
    place(x, 0.5 * pk(M.metal_click(rng, 2600, 0.06)), 0.0)
    place(x, 0.35 * pk(M.metal_click(rng, 3100, 0.06)), 0.035)
    place(x, pk(M.wood_knock(rng, 230, 0.3, hard=0.4, t60=0.12)), 0.02)
    place(x, 0.25 * pk(M.glass_ring(1900, 0.35, rng, t60=(0.3, 0.2, 0.12, 0.08, 0.05), split=3)), 0.02)
    place(x, 0.3 * pk(M.metal_clunk(rng, 520, 0.3)), 0.02)
    return x


def _wingbeat(rng, dur, lo, hi):
    """One downstroke: a band-limited air rush with a soft feather clap under it."""
    n = N(dur)
    air = bp(rng.standard_normal(n), lo, hi)
    env = (1.0 - np.exp(-np.linspace(0, 45, n))) * np.exp(-np.linspace(0, 7.5, n))
    clap = lp(rng.standard_normal(n), 420) * np.exp(-np.linspace(0, 14, n))
    return pk(air * env) + 0.55 * pk(clap)


def birds_flutter(rng, idx):
    """A small flock bursting off the ground: 4-7 birds, each a run of wingbeats (9-13 Hz) that thins as it climbs."""
    L = 1.6
    x = Z(L)
    birds = int(rng.integers(4, 8)) + idx % 2
    for _b in range(birds):
        t = float(rng.uniform(0.0, 0.22))
        rate = float(rng.uniform(9.0, 13.0))
        beats = int(rng.integers(8, 15))
        gain = float(rng.uniform(0.5, 1.0))
        lo, hi = float(rng.uniform(380, 700)), float(rng.uniform(3400, 6500))
        for k in range(beats):
            tt = t + k / rate + float(rng.uniform(-0.006, 0.006))
            if tt > L - 0.12:
                break
            a = gain * (1.0 - k / beats) ** 1.4 * float(rng.uniform(0.7, 1.0))
            place(x, a * _wingbeat(rng, float(rng.uniform(0.035, 0.06)), lo, hi), tt)
    return reverb(pk(x), room(0.6, rng), wet=0.12)


def drum_hit(rng):
    n = N(0.6)
    x = membrane(n, 62, rng, decay=0.55, drop=1.22, drop_time=0.045)
    mal = lp(rng.standard_normal(N(0.003)), 1500)
    place(x, 0.25 * pk(mal) * np.max(np.abs(x)), 0.0)
    thw = bp(rng.standard_normal(N(0.015)), 300, 1200) * np.exp(-np.linspace(0, 6, N(0.015)))
    x = pk(x)
    place(x, 0.45 * pk(thw), 0.0)
    shell = resonator(x, 180, 0.15) + 0.5 * resonator(x, 420, 0.08)
    x = x + 0.25 * pk(shell)
    return reverb(x, room(0.8, rng), wet=0.15)


def clock_chime(rng):
    x = bell(330.0, 6.0, rng, kind="church", doublet=0.7, bright=0.9)
    x = pk(x)
    return reverb(x, make_ir(2.5, rng, stereo=False, hf=0.5), wet=0.3)


def clock_tick(rng):
    x = Z(0.1)
    place(x, pk(M.metal_click(rng, 3600, 0.03)), 0.0)
    exc = Z(0.08)
    place(exc, click(rng, 0.0015, 300, 4000), 0)
    place(x, 0.5 * pk(resonator_bank(exc, [650, 1300], [0.03, 0.02], [1, .5])), 0.0)
    return x


def phone_buzz(rng):
    x = Z(1.0)
    for t0, d in [(0.0, 0.42), (0.55, 0.42)]:
        n = N(d)
        t = t_axis(n)
        f = 165 - 45 * np.exp(-t / 0.03)
        ph = TAU * np.cumsum(f) / SR
        buzz = sum(np.sin(k * ph) / k for k in range(1, 14))
        rattle = bp(rng.standard_normal(n), 800, 5000) * (0.5 + 0.5 * np.sign(np.sin(ph)))
        env = env_points([(0, 0), (0.02, 1.0), (d - 0.05, 0.9), (d, 0)], n)
        y = (pk(buzz) + 0.25 * pk(rattle)) * env
        y = y + 0.6 * pk(resonator_bank(y, [150, 330, 720], [0.08, 0.05, 0.03], [1, .6, .3]))
        place(x, pk(y), t0)
    return x


def voicemail_beep(rng):
    x = Z(0.3)
    n = N(0.25)
    t = t_axis(n)
    tone = np.sin(TAU * 1000 * t) + 0.03 * np.sin(TAU * 2000 * t)
    tone = fade(tone, 0.008, 0.012)
    place(x, tone, 0.02)
    x = bp(x, 300, 3400, 2)
    return np.tanh(1.2 * pk(x))


def _fridge_layer(n, rng, L=None):
    q = (lambda f: F(f, L)) if L else (lambda f: f)
    t = t_axis(n)
    y = np.zeros(n)
    for k, a in [(1, 0.35), (2, 1.0), (3, 0.5), (4, 0.45), (5, 0.2), (6, 0.25), (8, 0.1), (10, 0.05)]:
        y += a * np.sin(TAU * q(60.0 * k) * t + rng.uniform(0, TAU))
    whine = 0.08 * np.sin(TAU * q(375.5) * t) + 0.03 * np.sin(TAU * q(751.0) * t)
    return (y + whine) * (1 + 0.04 * np.sin(TAU * q(1 / 3) * t))


def fridge_hum_loop(rng):
    L = 6.0
    n = N(L)
    hum = _fridge_layer(n, rng, L)
    Fx = N(2.0)
    fan = loop_crossfade(lp(colored(n + Fx, rng, -3.0), 1500)[None, :], n, Fx)[0]
    x = hum / 2.5 + 0.08 * fan / np.std(fan) * np.std(hum / 2.5)
    return x - x.mean()


def fridge_stop(rng):
    n = N(0.6)
    t = t_axis(n)
    hum = _fridge_layer(n, rng) / 2.5
    spin = np.exp(-np.maximum(t - 0.06, 0) / 0.09)
    fan = lp(colored(n, rng, -3.0), 1500)
    x = (hum + 0.08 * fan / np.std(fan) * np.std(hum)) * spin
    # pitch sag during spin-down approximated by a short low wobble from the compressor mount
    wob = np.sin(TAU * 45 * t) * np.exp(-np.maximum(t - 0.06, 0) / 0.08) * (t > 0.06)
    x += 0.4 * np.std(hum) * 2 * wob
    rel = M.mech_click(rng, 0.08, f=(1600, 3200, 5000), t60=(0.03, 0.02, 0.01), body=200)
    place(x, 1.3 * np.max(np.abs(hum)) * pk(rel), 0.06)
    return x


def camera_shutter(rng):
    x = Z(0.3)
    place(x, pk(M.metal_click(rng, 3300, 0.05)), 0.0)
    exc = Z(0.06)
    place(exc, click(rng, 0.002, 200, 2500), 0)
    place(x, 0.6 * pk(resonator_bank(exc, [320, 610], [0.03, 0.02], [1, .6])), 0.012)
    place(x, 0.8 * pk(M.metal_click(rng, 3700, 0.05)), 0.075)
    ns = N(0.09)
    place(x, 0.12 * pk(glide_tone(ns, 2600, 2900) * np.hanning(ns)), 0.02)
    place(x, 0.2 * pk(M.mech_click(rng, 0.06, f=(1400, 2800, 4400), body=0)), 0.11)
    return x


def reflection_sting(rng):
    n = N(2.0)
    t = t_axis(n)
    g = M.glass_ring(880, 1.6, rng, amps=(1, .5, .25, .12, .06), t60=(1.4, .9, .5, .3, .2), split=2.5)
    rev = g[::-1]
    y = np.zeros(n)
    y[:len(rev)] += rev
    tail = M.glass_ring(880 * 2 ** (-40 / 1200), 0.4, rng, t60=(0.35, 0.2, 0.12, 0.08, 0.05), split=6)
    place(y, 0.6 * np.max(np.abs(rev)) * pk(tail), 1.6)
    det = M.glass_ring(880 * 1.012, 1.6, rng, amps=(1, .4, .2, .1, .05), t60=(1.4, .9, .5, .3, .2), split=2)[::-1]
    y[:len(det)] += 0.5 * det
    air = hp(colored(n, rng, -1.0), 2500) * env_points([(0, 0), (1.5, 1), (2.0, 0)], n) ** 2
    y = lp(pk(y) + 0.03 * pk(air), 10000)
    y *= env_points([(0, 1), (1.85, 1), (2.0, 0)], n)
    return reverb(y, make_ir(1.6, rng, stereo=False, hf=0.7), wet=0.3)


def skyline_shift(rng):
    n = N(4.0)
    t = t_axis(n)
    env = env_points([(0, 0), (0.8, 0.7), (2.0, 1.0), (3.2, 0.7), (4.0, 0)], n)
    from dsp import stick_slip
    grind = stick_slip(n, 22 + 18 * smooth_random(n, 1.5, rng), rng,
                       [(160, 0.06, 1.0), (310, 0.05, 0.7), (520, 0.04, 0.4)], jitter=0.5, amp_sigma=0.6, noise=0.4)
    grit = crackle(n, 300 * env, rng, lo=300, hi=2500, grain=0.002)
    rumble = lp(colored(n, rng, -6.0), 120, 2)
    groan = stick_slip(n, 9 + 3 * t / 4, rng, [(250 * 0.97, 0.15, 1.0), (600 * 0.97, 0.1, 0.4)], jitter=0.15)
    drift = glide_tone(n, 70, 58, harmonics=((1, 1.0), (2, 0.4), (3, 0.15)))
    y = (0.65 * pk(grind) + 0.3 * pk(grit) + 0.45 * pk(rumble) + 0.35 * pk(groan) + 0.3 * pk(drift)) * env
    y = lp(y, 2500)
    return reverb(y, make_ir(3.0, rng, stereo=False, hf=0.3), wet=0.45, dry=0.7)


# =============================================================================== registry
def V(fn, idx):
    return lambda rng: fn(rng, idx)


SFX = {}


def reg(i, fn, dur, loop=False, norm=("peak", -3.0), ch=1, bus="SFX", note="", fade_out=None, group="", desc="",
        peak=None, trim=False):
    """peak: seconds into the clip of its dramatic peak (manifest 'peak'; AudioDirector.PlayToPeak lines it up).
    trim: keep the clip's natural length (variable-length variants) instead of padding to dur."""
    SFX[i] = dict(fn=fn, dur=dur, loop=loop, norm=norm, ch=ch, bus=bus, note=note, fade_out=fade_out, group=group,
                  desc=desc, peak=peak, trim=trim)


for _i, _d in [("ui_hover", 0.08), ("ui_click", 0.12), ("ui_back", 0.15), ("ui_confirm", 0.4), ("ui_slider_tick", 0.05)]:
    reg(_i, globals()[_i], _d, bus="UI")
reg("pp_open", pp_open, 0.6)
reg("pp_close", pp_close, 0.5)
reg("pp_page", pp_page, 0.4)
reg("pp_tab", pp_tab, 0.1)
reg("pp_seam_shimmer", pp_seam_shimmer, 1.2, fade_out=0.15)
reg("tear_open_slit", tear_open_slit, 0.8, fade_out=0.2)
reg("tear_open_window", tear_open_window, 1.2, fade_out=0.3)
reg("tear_open_door", tear_open_door, 1.8, fade_out=0.4)
reg("tear_seal", tear_seal, 1.0, fade_out=0.2)
reg("tear_breach", tear_breach, 1.0, fade_out=0.25)
reg("tear_hum_loop", tear_hum_loop, 6.0, loop=True, norm=("lufs", -22.0), ch=2, note="stereo")
reg("tear_stretch_loop", tear_stretch_loop, 4.0, loop=True, norm=("lufs", -22.0))
reg("stamp_hit", stamp_hit, 0.35, fade_out=0.05)
reg("seal_verify", seal_verify, 1.6, fade_out=0.3)
reg("seal_counterfeit", seal_counterfeit, 2.2, fade_out=0.4)
reg("stamp_void", stamp_void, 0.6, fade_out=0.1)
reg("sunbeam_loop", sunbeam_loop, 6.0, loop=True, norm=("lufs", -20.0))
reg("sunbeam_on", sunbeam_on, 1.5, norm=("match", "sunbeam_loop"), note="level-matched to sunbeam_loop")
reg("sun_sizzle", sun_sizzle, 1.0, fade_out=0.15)
for _k in range(3):
    reg(f"pass_step_{_k + 1}", V(pass_step, _k), 0.3, fade_out=0.06)
    reg(f"pass_creak_{_k + 1}", V(pass_creak, _k), 0.8, fade_out=0.05)
reg("pass_breath", pass_breath, 2.0, fade_out=0.1)
reg("pass_recoil", pass_recoil, 0.7, fade_out=0.05)
reg("pass_tap", pass_tap, 1.2, fade_out=0.1)
reg("pass_drone_loop", pass_drone_loop, 8.0, loop=True, norm=("lufs", -24.0))
reg("pass_whisper", pass_whisper, 2.5, fade_out=0.15)
reg("pass_sting", pass_sting, 1.5, fade_out=0.02)
reg("breath_scared", breath_scared, 3.0, fade_out=0.1)
reg("heartbeat_loop", heartbeat_loop, 2.0, loop=True, norm=("peak", -3.0))
for _i, _d, _fo in [("grab", 0.15, 0.03), ("drop_soft", 0.3, 0.05), ("drop_hard", 0.4, 0.06), ("place_socket", 0.3, 0.06),
                    ("dial_click", 0.1, 0.02), ("lever_clunk", 0.5, 0.08), ("button_press", 0.15, 0.02),
                    ("drawer_open", 0.8, 0.06), ("door_open", 1.2, 0.15), ("door_close", 1.0, 0.2),
                    ("glasses_unfold", 0.4, 0.05), ("case_open", 0.4, 0.06), ("case_close", 0.35, 0.06),
                    ("glasses_break", 1.2, 0.15), ("coin_token", 0.5, 0.05), ("booth_power", 1.5, 0.02),
                    ("lantern_place", 0.4, 0.08), ("drum_hit", 0.6, 0.1), ("clock_chime", 6.0, 1.2),
                    ("clock_tick", 0.1, 0.02), ("phone_buzz", 1.0, 0.03), ("voicemail_beep", 0.3, 0.01),
                    ("fridge_stop", 0.6, 0.05), ("camera_shutter", 0.3, 0.05), ("reflection_sting", 2.0, 0.05),
                    ("skyline_shift", 4.0, 0.3)]:
    reg(_i, globals()[_i], _d, fade_out=_fo)
reg("noisemaker_loop", noisemaker_loop, 4.0, loop=True, norm=("lufs", -20.0))
for _k in range(3):
    reg(f"birds_flutter_{_k + 1}", V(birds_flutter, _k), 1.6, fade_out=0.2, group="World",
        desc="a small flock bursting off the ground (AmbientBirds)")
reg("fridge_hum_loop", fridge_hum_loop, 6.0, loop=True, norm=("lufs", -24.0))
SFX["fridge_stop"]["norm"] = ("match", "fridge_hum_loop")
SFX["fridge_stop"]["note"] = "level-matched to fridge_hum_loop"

# Consistent perceived levels for the older one-shots: peak short-window loudness (dsp.loud_max) per category,
# true peak <= -2 dBTP, moving each clip at most -10/+6 dB from its former -3 dBTP peak level. Sustained, dense
# sounds come down (a pure 1 kHz beep at -3 dBTP was ~14 dB hotter than a door); clicks rise only as far as the
# true-peak ceiling allows.
LOUD = {
    "ui_hover": -19, "ui_click": -19, "ui_back": -19, "ui_slider_tick": -19, "ui_confirm": -19,
    "pp_open": -18, "pp_close": -18, "pp_page": -18, "pp_tab": -19, "pp_seam_shimmer": -17,
    "tear_open_slit": -16, "tear_open_window": -15, "tear_open_door": -14, "tear_seal": -16, "tear_breach": -14,
    "stamp_hit": -16, "seal_verify": -15, "seal_counterfeit": -15, "stamp_void": -16, "sun_sizzle": -18,
    "pass_step_1": -17, "pass_step_2": -17, "pass_step_3": -17, "pass_creak_1": -18, "pass_creak_2": -18,
    "pass_creak_3": -18, "pass_breath": -15, "pass_recoil": -15, "pass_tap": -18, "pass_whisper": -16, "pass_sting": -14,
    "breath_scared": -17,
    "grab": -19, "drop_soft": -18, "drop_hard": -17, "place_socket": -18, "dial_click": -19, "lever_clunk": -17,
    "button_press": -19, "drawer_open": -18, "door_open": -16, "door_close": -16, "glasses_unfold": -19, "case_open": -19,
    "case_close": -18, "glasses_break": -15, "coin_token": -17, "booth_power": -16, "lantern_place": -18, "drum_hit": -15,
    "clock_chime": -14, "clock_tick": -20, "phone_buzz": -17, "voicemail_beep": -19, "camera_shutter": -18,
    "reflection_sting": -14, "skyline_shift": -15,
    "birds_flutter_1": -18, "birds_flutter_2": -18, "birds_flutter_3": -18,
}
for _i, _t in LOUD.items():
    SFX[_i]["norm"] = ("loud", float(_t), -2.0, (-10.0, 6.0))

sfx_steps.register(reg)
sfx_body.register(reg)
sfx_shift.register(reg)


# =============================================================================== render
def finish_oneshot(x, dur, fade_out, trim=False):
    n = N(dur)
    if x.shape[-1] < n and not trim:
        x = np.concatenate([x, np.zeros(x.shape[:-1] + (n - x.shape[-1],))], axis=-1)
    x = x[..., :n]
    if trim:  # drop the silent tail of a variable-length variant (keep 10 ms past the last audible sample)
        a = np.max(np.abs(np.atleast_2d(x)), axis=0)
        last = int(np.nonzero(a > np.max(a) * undb(-60))[0][-1]) if np.any(a > 0) else len(a) - 1
        x = x[..., :min(len(a), last + N(0.01))]
    x = hp(x, 20, 2)
    return fade(x, 0.0003, fade_out if fade_out else 0.01)


def main(only, prefixes=()):
    os.makedirs(OUT, exist_ok=True)
    meta = {}
    if os.path.exists(META):
        with open(META, encoding="utf-8") as fh:
            meta = json.load(fh)
    for sid in [k for k in meta if k not in SFX]:  # ids no longer generated (e.g. the old Kenney imports)
        meta.pop(sid)
    gains = {}
    want = set(only) | {k for k in SFX if any(k.startswith(p) for p in prefixes)}
    sel = bool(want) or bool(prefixes)
    for sid in list(want):  # a level-matched clip needs its reference rendered too (and vice versa)
        if sid in SFX and SFX[sid]["norm"][0] == "match":
            want.add(SFX[sid]["norm"][1])
    for sid, spec in SFX.items():
        if spec["norm"][0] == "match" and spec["norm"][1] in want:
            want.add(sid)
    order = sorted(SFX, key=lambda k: 1 if SFX[k]["norm"][0] == "match" else 0)
    for sid in order:
        spec = SFX[sid]
        if sel and sid not in want:
            continue
        rng = rng_for(sid)
        x = spec["fn"](rng)
        if spec["loop"]:
            if x.ndim == 2 and spec["ch"] == 1:
                x = x.mean(axis=0)
            x, rot = rotate_to_zero_crossing(x)
        else:
            if x.ndim == 2 and spec["ch"] == 1:
                x = x.mean(axis=0)
            x = finish_oneshot(x, spec["dur"], spec["fade_out"], spec["trim"])
        kind, val = spec["norm"][0], spec["norm"][1]
        shift_db = None
        if kind == "peak":
            g = undb(val - true_peak_db(x))
            y = x * g
        elif kind == "lufs":
            y = norm_lufs(x, val, tp_max=-3.0, circular=spec["loop"])
            g = float(np.max(np.abs(y)) / (np.max(np.abs(x)) + 1e-12))
        elif kind == "loud":
            tp = spec["norm"][2] if len(spec["norm"]) > 2 else -2.0
            cl = spec["norm"][3] if len(spec["norm"]) > 3 else None
            gr = spec["norm"][4] if len(spec["norm"]) > 4 else 0.0
            y, gdb = norm_loud(x, val, tp_max=tp, clamp=cl, max_gr=gr)
            g = undb(gdb)
            if cl is not None:  # a re-levelled older clip: how far it moved from the old -3 dBTP peak normalisation
                shift_db = round(gdb - (-3.0 - true_peak_db(x)), 2)
        else:  # match another clip's gain
            if val not in gains:
                raise RuntimeError(f"{sid} needs {val} rendered first")
            g = gains[val]
            y = x * g
            if true_peak_db(y) > -3.0:
                y = norm_peak(y, -3.0)
        gains[sid] = g
        path = os.path.join(OUT, sid + ".wav")
        write_wav(path, y, rng=np.random.default_rng(7))
        m = {"bus": spec["bus"], "loop": spec["loop"], "channels": 1 if y.ndim == 1 else 2,
             "source": SYNTH_SRC, "license": SYNTH_LIC, "norm": list(spec["norm"]), "note": spec["note"],
             "group": spec["group"], "desc": spec["desc"], "peak": spec["peak"],
             "synth_lufs": None if lufs(y) is None else round(lufs(y), 2), "synth_tp": round(true_peak_db(y), 2),
             "loud_max": round(loud_max(y), 2), "seconds": round(y.shape[-1] / SR, 3)}
        if shift_db is not None:
            m["vs_peak3_db"] = shift_db
        if spec["loop"]:
            m["seam"] = seam_report(y)
        meta[sid] = m
        print(f"{sid:22s} {y.shape[-1] / SR:5.2f}s ch={m['channels']} TP={m['synth_tp']:6.2f} Lmax={m['loud_max']:6.1f}"
              f" LUFS={m['synth_lufs']}" + (f" d={shift_db:+.1f}" if shift_db is not None else "")
              + (f" seam={m['seam']}" if spec['loop'] else ""), flush=True)
    with open(META, "w", encoding="utf-8") as fh:
        json.dump(meta, fh, indent=1)


if __name__ == "__main__":
    args = sys.argv[1:]
    pre = []
    if "--prefix" in args:
        i = args.index("--prefix")
        pre = args[i + 1:]
        args = args[:i]
    main(set(args), tuple(pre))
