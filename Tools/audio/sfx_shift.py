"""World-shift transitions and chase stingers for The Twelfth Stamp (original synthesis, stereo, played 2D).

  shift_rumble         5.0 s  low build: rumble, rising groan, grinding structure, debris; peaks at 4.0 s then cuts
  shift_whoosh         2.0 s  rising rush sweeping across the head; peaks at 1.5 s
  shift_reverse_swell  2.5 s  a reversed reverb tail sucking in toward 2.4 s, then silence (the hit belongs to the swap)
  shift_tear           1.6 s  the fabric of space ripping: deep rip, glassy rip, sub drop, air burst
  shift_arrive         3.5 s  landing: a solid thump, pressure release, debris and air settling
  sting_chase_1/2      2.2 s  chase-start hit: sub drop, dissonant stab, metal, a cracked-air burst, short tail
The peak markers (seconds into the clip) go into the manifest so the game can line a clip's peak up with the swap
(AudioDirector.PlayToPeak).
"""
import numpy as np
from scipy.signal import fftconvolve

import materials as M
from dsp import (SR, TAU, N, t_axis, place, env_points, colored, smooth_random, lp, hp, bp, resonator, tv_lowpass,
                 crackle, click, glide_tone, stick_slip, make_ir, convolve, pan, fade)


def pk(x):
    return x / (np.max(np.abs(x)) + 1e-12)


def R(x):
    return x / (np.sqrt(np.mean(x ** 2)) + 1e-12)


def st(x, p=0.0):
    """Mono -> stereo at pan p (equal power)."""
    return pan(x, p)


def wide(rng, mono_fn):
    """Two decorrelated renders (left, right) of the same recipe."""
    return np.stack([mono_fn(), mono_fn()])


def tvbp(x, fc, q=1.2, block=128):
    """Time-varying band-pass (block-wise biquad with carried state)."""
    from scipy import signal
    y = np.zeros_like(x)
    zi = np.zeros(2)
    for s in range(0, len(x), block):
        e = min(s + block, len(x))
        f = float(np.clip(fc[(s + e) // 2], 30, SR * 0.45))
        b, a = signal.iirpeak(f, q, fs=SR)
        y[s:e], zi = signal.lfilter(b, a, x[s:e], zi=zi)
    return y


# =============================================================================== shift
def shift_rumble(rng):
    D, P = 5.0, 4.0
    n = N(D)
    t = t_axis(n)
    grow = np.clip(t / P, 0, 1)
    after = np.exp(-np.maximum(t - P, 0) / 0.22)
    env = np.where(t < P, 0.02 + 0.98 * grow ** 2.2, after)
    sub = lp(colored(n, rng, -6.0), 110, 2)
    f = 36 + 22 * grow ** 1.5
    tone = np.sin(TAU * np.cumsum(f) / SR)
    tone = tone + 0.5 * np.sin(2 * TAU * np.cumsum(f * 1.005) / SR + 1.0)
    tone = np.tanh(1.8 * tone) / np.tanh(1.8)  # harmonics so headset drivers carry the low end
    groan = stick_slip(n, 7 + 30 * grow ** 1.4, rng, [(140, 0.12, 1.0), (330, 0.09, 0.6), (610, 0.06, 0.3)], jitter=0.35,
                       amp_sigma=0.5, noise=0.25)
    press = bp(colored(n, rng, -3.0), 140, 460, 2) * (0.6 + 0.4 * smooth_random(n, 3.0, rng))
    mono = (0.8 * R(sub) + 0.55 * R(tone) + 0.45 * R(groan) + 0.35 * R(press)) * env
    debris = wide(rng, lambda: crackle(n, 30 + 1400 * grow ** 2 * (t < P), rng, lo=700, hi=4500, grain=0.003, amp_sigma=0.6))
    debris = debris * env[None, :] * 0.08 / (np.std(debris) + 1e-12)
    x = st(mono) + debris
    return lp(x, 7000)


def shift_whoosh(rng):
    D, P = 2.0, 1.5
    n = N(D)
    t = t_axis(n)
    u = np.clip(t / P, 0, 1)
    env = np.where(t < P, u ** 3, np.exp(-(t - P) / 0.11))
    fc = np.where(t < P, 180 * (4200 / 180) ** (u ** 1.3), 4200 - 2400 * np.clip((t - P) / 0.3, 0, 1))
    chans = []
    for c in range(2):
        nz = colored(n, rng, -1.0)
        y = tvbp(nz, fc * (1.0 + 0.04 * c), q=1.0) + 0.9 * tv_lowpass(tv_lowpass(nz, fc * 0.7), fc * 0.9)
        chans.append(R(y))
    x = np.stack(chans)
    p = np.clip(-0.8 + 1.6 * u ** 1.5, -0.8, 0.8)  # the rush travels across the head
    th = (p + 1) * np.pi / 4
    x = np.stack([x[0] * np.cos(th) * 1.25, x[1] * np.sin(th) * 1.25])
    sh = np.zeros(n)
    for k, f0 in enumerate([220.0, 330.0, 440.0, 660.0]):
        f = f0 * 2 ** (u ** 1.6)  # each partial glides up an octave
        sh += (0.7 ** k) * np.sin(TAU * np.cumsum(f) / SR + rng.uniform(0, TAU))
    x = x + 0.12 * st(R(sh))
    return x * env[None, :]


def shift_reverse_swell(rng):
    D, P = 2.5, 2.4
    n = N(D)
    hit = np.zeros(N(0.6))
    place(hit, pk(M.thud(rng, 46, 0.6, 0.3, 1.4, noise=0.3)), 0.0)
    for f in (587.33, 622.25, 880.0 * 0.995, 1174.7):
        place(hit, 0.35 * pk(M.glass_ring(f, 0.6, rng, t60=(0.8, 0.5, 0.3, 0.2, 0.1), split=2.5)), 0.0)
    place(hit, 0.6 * pk(bp(rng.standard_normal(N(0.05)), 300, 6000) * np.exp(-np.linspace(0, 6, N(0.05)))), 0.0)
    ir = make_ir(4.5, rng, hf=0.45, predelay=0.0, stereo=True, er=0)
    wet = convolve(hit, ir)[:, :N(P)]
    rev = wet[:, ::-1]
    rev = rev * (np.linspace(0, 1, rev.shape[1]) ** 0.6)[None, :]
    x = np.zeros((2, n))
    x[:, :rev.shape[1]] = rev
    k = N(0.015)
    x[:, N(P) - k:N(P)] *= np.linspace(1, 0, k)[None, :]
    return lp(x, 9000)


def shift_tear(rng):
    D = 1.6
    n = N(D)
    t = t_axis(n)
    nr = N(1.05)
    env = env_points([(0, 0), (0.012, 1.0), (0.2, 0.7), (0.4, 0.95), (0.7, 0.6), (1.05, 0)], nr)
    deep = lp(M.rip(nr, env, rng, rate=5200, lo=150, hi=6500, body=(220, 520, 1300), fibres=22, jerk_hz=14), 6000)
    high = M.rip(nr, env ** 1.5, rng, rate=3800, lo=1600, hi=10000, body=(3400,), fibres=12)
    p = np.linspace(-0.7, 0.7, nr)
    th = (p + 1) * np.pi / 4
    rip = np.zeros((2, n))
    rip[0, :nr] = R(deep) * np.cos(th) + 0.35 * R(high) * np.cos(th + 0.3)
    rip[1, :nr] = R(deep) * np.sin(th) + 0.35 * R(high) * np.sin(th - 0.3)
    ns = N(0.8)
    drop = glide_tone(ns, 72, 32, curve=0.6, harmonics=((1, 1.0), (2, 0.35))) * np.exp(-np.linspace(0, 5, ns))
    air = M.air_rush(n, env_points([(0, 0), (0.03, 1.0), (0.5, 0.45), (D, 0)], n), rng, 4000, 300, curve=0.5)
    glass = M.glass_ring(310, D, rng, t60=(1.6, 1.0, 0.6, 0.35, 0.2), split=2.0) + \
        0.6 * M.glass_ring(465, D, rng, t60=(1.3, 0.8, 0.5, 0.3, 0.2), split=2.5)
    x = rip + st(0.55 * pk(np.concatenate([drop, np.zeros(n - ns)]))) + 0.3 * st(R(air) * 0.3) + 0.12 * st(pk(glass))
    return lp(x, 11000)


def shift_arrive(rng):
    D = 3.5
    n = N(D)
    x = np.zeros((2, n))
    thump = pk(M.thud(rng, 48, 0.8, 0.32, 1.45, noise=0.35)) + 0.7 * pk(M.thud(rng, 96, 0.8, 0.14, 1.3, noise=0.45))
    thump = np.tanh(1.4 * pk(thump))  # harmonics: the landing has to carry on headset drivers
    body = lp(rng.standard_normal(N(0.12)), 900, 2) * np.exp(-np.linspace(0, 5, N(0.12)))
    place(x, st(pk(thump)), 0.0)
    place(x, st(0.35 * pk(body)), 0.0)
    na = N(1.6)
    rel = wide(rng, lambda: M.air_rush(na, env_points([(0, 0), (0.02, 1.0), (0.4, 0.5), (1.6, 0)], na), rng, 2600, 220, curve=0.5))
    place(x, 0.6 * rel / (np.max(np.abs(rel)) + 1e-12), 0.0)
    for side in (-0.75, 0.6):
        tk = M.tinkles(rng, 2.6, pieces=4, t_start=0.25, spread=1.8, fmin=2500, fmax=8000, level=0.05)
        place(x, st(tk, side), 0.0)
    ng = N(2.8)
    g = wide(rng, lambda: crackle(ng, 400 * np.exp(-np.linspace(0, 4, ng)), rng, lo=700, hi=6000, grain=0.001, amp_sigma=1.0))
    place(x, 0.1 * g / (np.std(g) * 6 + 1e-12), 0.15)
    tail = wide(rng, lambda: lp(colored(n, rng, -4.0), 400, 2) * np.exp(-np.linspace(0, 4.5, n)))
    x = x + 0.12 * tail / (np.max(np.abs(tail)) + 1e-12)
    ir = make_ir(2.0, rng, hf=0.4, predelay=0.01, stereo=True)
    wet = np.stack([fftconvolve(x[c], ir[c])[:n] for c in range(2)])
    return lp(x + 0.22 * wet / (np.max(np.abs(wet)) + 1e-12) * np.max(np.abs(x)), 10000)


# =============================================================================== chase stingers
def _stab(rng, notes, dur, bright=7000.0):
    """Dissonant brass/string-like stab: saw-ish partials, fast attack, the brightness closing over a second."""
    n = N(dur)
    t = t_axis(n)
    out = np.zeros((2, n))
    for i, (f, p) in enumerate(notes):
        y = np.zeros(n)
        for k in range(1, 20):
            if k * f > 12000:
                break
            y += (1.0 / k) * np.sin(TAU * k * f * (1 + 0.002 * rng.uniform(-1, 1)) * t + rng.uniform(0, TAU))
        out += st(y, p)
    env = env_points([(0, 0), (0.006, 1.0), (0.15, 0.4), (0.6, 0.18), (dur, 0)], n)
    fc = bright * np.exp(-t / 0.35) + 700
    out = np.stack([tv_lowpass(tv_lowpass(out[c], fc), fc * 1.3) for c in range(2)])
    return out * env[None, :]


def _sting(rng, notes, metal_f, glass_f):
    D = 2.2
    n = N(D)
    x = np.zeros((2, n))
    ns = N(1.2)
    sub = glide_tone(ns, 82, 38, curve=0.5, harmonics=((1, 1.0), (2, 0.3), (3, 0.12))) * np.exp(-np.linspace(0, 4.5, ns))
    sub = np.tanh(1.5 * sub)
    place(x, st(0.6 * pk(sub)), 0.0)
    place(x, 1.0 * pk(_stab(rng, notes, 1.6)), 0.0)
    place(x, st(0.45 * pk(M.metal_clunk(rng, metal_f, 1.2, bright=1.4)), -0.3), 0.0)
    place(x, st(0.25 * pk(M.glass_ring(glass_f, 1.4, rng, t60=(1.0, 0.6, 0.35, 0.2, 0.1), split=4.0)), 0.4), 0.004)
    crack = bp(rng.standard_normal(N(0.03)), 1000, 8000) * np.exp(-np.linspace(0, 7, N(0.03)))
    place(x, st(0.5 * pk(crack)), 0.0)
    ir = make_ir(2.2, rng, hf=0.4, predelay=0.012, stereo=True)
    wet = np.stack([fftconvolve(x[c], ir[c])[:n] for c in range(2)])
    y = x + 0.3 * wet / (np.max(np.abs(wet)) + 1e-12) * np.max(np.abs(x))
    return fade(y, 0.0005, 0.3)


def sting_chase_1(rng):
    # D, Eb, A, D, Ab: a minor second and a tritone stacked over the root
    return _sting(rng, [(146.83, -0.4), (155.56, 0.4), (220.0, -0.15), (293.66, 0.15), (415.3, 0.0)], 210, 1245.0)


def sting_chase_2(rng):
    # C#, D, G#, D (octave) - the same family of clash, voiced differently
    return _sting(rng, [(138.59, 0.35), (146.83, -0.35), (207.65, 0.1), (293.66, -0.1), (311.13, 0.0)], 240, 1390.0)


# =============================================================================== registry
def register(reg):
    reg("shift_rumble", shift_rumble, 5.0, norm=("loud", -14.0, -1.5, None, 6.0), ch=2, fade_out=0.2, group="Shift", peak=4.0,
        desc="low build to the swap (rumble, groan, grinding, debris); peak 4.0 s then cuts")
    reg("shift_whoosh", shift_whoosh, 2.0, norm=("loud", -14.0, -1.5), ch=2, fade_out=0.2, group="Shift", peak=1.5,
        desc="rising rush across the head; peak 1.5 s")
    reg("shift_reverse_swell", shift_reverse_swell, 2.5, norm=("loud", -15.0, -1.5), ch=2, fade_out=0.05, group="Shift",
        peak=2.4, desc="reversed reverb tail sucking in to 2.4 s, then silence")
    reg("shift_tear", shift_tear, 1.6, norm=("loud", -13.0, -1.5), ch=2, fade_out=0.25, group="Shift", peak=0.05,
        desc="space tearing open: deep rip, glassy rip, sub drop, air burst")
    reg("shift_arrive", shift_arrive, 3.5, norm=("loud", -14.0, -1.5, None, 6.0), ch=2, fade_out=0.6, group="Shift", peak=0.02,
        desc="landing: thump, pressure release, debris and air settling")
    reg("sting_chase_1", sting_chase_1, 2.2, norm=("loud", -14.0, -1.5), ch=2, fade_out=0.3, group="Threat", peak=0.01,
        desc="chase-start stinger (sub drop, dissonant stab D/Eb/A/Ab, metal, air crack)")
    reg("sting_chase_2", sting_chase_2, 2.2, norm=("loud", -14.0, -1.5), ch=2, fade_out=0.3, group="Threat", peak=0.01,
        desc="chase-start stinger, second voicing (C#/D/G#)")
