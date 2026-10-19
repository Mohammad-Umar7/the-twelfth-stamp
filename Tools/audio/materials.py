"""Physically-inspired material generators shared by the SFX, ambience and music scripts.

All functions return mono float arrays at 48 kHz unless stated otherwise.
"""
import numpy as np

from dsp import (SR, TAU, N, t_axis, place, fade, env_points, exp_env, ar_env, colored, shaped_noise,
                 smooth_random, lp, hp, bp, peak_eq, resonator, resonator_bank, tv_lowpass, impulse_train,
                 poisson_clicks, grains, crackle, stick_slip, click, modal, osc, undb)

# thin-shell (glass bowl / wine glass) mode ratios: f_n ∝ n(n²-1)/sqrt(n²+1), n = 2..6
GLASS_RATIOS = [1.0, 2.83, 5.42, 8.77, 12.9]
# free-free bar / tubular chime ratios
BAR_RATIOS = [1.0, 2.756, 5.404, 8.933]


# ----------------------------------------------------------------------------- paper
def paper_tick(rng, dur=0.03, bright=1.0, events=3, spread=0.012):
    n = N(dur)
    x = np.zeros(n)
    ts = np.sort(rng.uniform(0.0005, spread, events))
    for i, t in enumerate(ts):
        g = click(rng, dur=rng.uniform(0.0012, 0.003), lo=1500 * bright, hi=min(7800 * bright, 16000))
        place(x, g, t, (0.65 ** i) * rng.uniform(0.7, 1.0))
    x = x + 0.5 * resonator(x, 3300 * bright, 0.01)
    body = rng.standard_normal(N(0.008)) * np.exp(-np.linspace(0, 5, N(0.008)))
    place(x, hp(lp(body, 600), 150) * 0.25, ts[0])
    return x


def paper_crackle(n, rate, rng, lo=1300, hi=8500, sigma=0.9):
    return crackle(n, rate, rng, lo=lo, hi=hi, grain=0.0007, amp_sigma=sigma)


def paper_swish(n, env, rng, f_start=1800, f_peak=4200, f_end=2400, q=1.2, peak_at=0.5):
    """Sliding paper: band of noise whose centre sweeps start -> peak -> end."""
    def mag(t, f):
        u = np.clip(t / (n / SR), 0, 1)
        fc = np.where(u < peak_at, f_start + (f_peak - f_start) * (u / peak_at),
                      f_peak + (f_end - f_peak) * ((u - peak_at) / (1 - peak_at)))
        bw = fc[None, :] / q
        return np.exp(-0.5 * ((f[:, None] - fc[None, :]) / bw) ** 2) + 0.08 * (f[:, None] > 800)
    x = shaped_noise(n, mag, rng, nfft=1024)
    return x / (np.std(x) + 1e-9) * env


def flap(rng, dur=0.04, cutoff=500):
    """Page / cover landing: a short puff of low air plus a paper slap."""
    n = N(dur)
    air = lp(rng.standard_normal(n), cutoff, 2) * np.exp(-np.linspace(0, 6, n))
    slap = bp(rng.standard_normal(n), 900, 3500) * np.exp(-np.linspace(0, 14, n))
    return 1.0 * air / (np.max(np.abs(air)) + 1e-9) + 0.35 * slap / (np.max(np.abs(slap)) + 1e-9)


def crumple(n, env, rng, rate=3000, lo=500, hi=7000):
    """Paper crunch/crumple: dense bursty crackle."""
    jerk = smooth_random(n, 30, rng) ** 3
    r = rate * env * (0.25 + 1.5 * jerk)
    c = crackle(n, r, rng, lo=lo, hi=hi, grain=0.0012, amp_sigma=1.0)
    c = c + 0.6 * resonator(c, 1100, 0.02) + 0.3 * resonator(c, 2700, 0.012)
    return c


# ----------------------------------------------------------------------------- friction
def leather_creak(n, rate, rng, env=None, body=(330, 720, 1350, 2600), damp=1.0):
    modes = [(body[0], 0.035 * damp, 1.0), (body[1], 0.028 * damp, 0.75), (body[2], 0.02 * damp, 0.45),
             (body[3], 0.012 * damp, 0.18)]
    x = stick_slip(n, rate, rng, modes, jitter=0.3, amp_sigma=0.4, noise=0.1)
    if env is not None:
        x = x * env
    return x


def wood_creak(n, rate, rng, env=None, body=(180, 420, 950, 1900)):
    modes = [(body[0], 0.06, 1.0), (body[1], 0.045, 0.7), (body[2], 0.03, 0.4), (body[3], 0.018, 0.15)]
    x = stick_slip(n, rate, rng, modes, jitter=0.2, amp_sigma=0.3, noise=0.05)
    return x * env if env is not None else x


def hinge_creak(n, rate, rng, env=None, body=(640, 1310, 2050, 3400)):
    modes = [(body[0], 0.09, 1.0), (body[1], 0.07, 0.8), (body[2], 0.05, 0.45), (body[3], 0.03, 0.2)]
    x = stick_slip(n, rate, rng, modes, jitter=0.12, amp_sigma=0.25, noise=0.04)
    return x * env if env is not None else x


def glass_creak(n, rate, rng, env=None, f0=1450, spread=1.0):
    """Glass under stress: slow stick-slip into narrow, inharmonic glass resonances."""
    fr = [f0, f0 * 1.47 * spread, f0 * 2.31 * spread, f0 * 3.62 * spread]
    modes = [(fr[0], 0.12, 1.0), (fr[1], 0.09, 0.6), (fr[2], 0.06, 0.35), (fr[3], 0.04, 0.15)]
    x = stick_slip(n, rate, rng, modes, jitter=0.35, amp_sigma=0.5, noise=0.02)
    return x * env if env is not None else x


def slide_noise(n, env, rng, lo=300, hi=2500, grit_rate=400):
    """Rubbing/sliding friction: band noise plus a little grit."""
    x = bp(rng.standard_normal(n), lo, hi, 2)
    x = x / (np.std(x) + 1e-9)
    g = crackle(n, grit_rate * env, rng, lo=lo, hi=min(hi * 2, 12000), grain=0.0008, amp_sigma=0.7)
    g = g / (np.std(g) + 1e-9)
    return (x + 0.35 * g) * env


# ----------------------------------------------------------------------------- tearing
def rip(n, env, rng, rate=3000, lo=900, hi=9000, body=(2600,), fibres=8, jerk_hz=25):
    """Tearing paper/fabric: thousands of fibre breaks per second, delivered in jerks."""
    jerk = smooth_random(n, jerk_hz, rng) ** 2.5
    r = rate * env * (0.15 + 1.7 * jerk)
    c = poisson_clicks(n, r, rng, amp_sigma=0.8)
    c = grains(c, rng, 0.0009)
    hiss = rng.standard_normal(n) * env * (0.2 + jerk)
    x = bp(c, lo, hi, 2) / (np.std(bp(c, lo, hi, 2)) + 1e-9) + 0.5 * bp(hiss, lo, hi, 2) / (np.std(hiss) + 1e-9)
    for f in body:
        x = x + 0.5 * resonator(x, f, 0.015)
    # fibre snaps: a few larger clicks
    on = np.where(env > 0.2 * env.max())[0]
    if len(on) and fibres:
        for t in rng.choice(on, size=min(fibres, len(on)), replace=False):
            g = click(rng, dur=0.003, lo=lo * 0.6, hi=hi) * rng.uniform(1.2, 2.2)
            place(x, g, t / SR)
    # soft-clip at ~3 sigma: tearing is dense, not a few spikes
    s = 3.0 * np.std(x[np.abs(x) > 0]) + 1e-9
    return s * np.tanh(x / s)


def air_rush(n, env, rng, f_start=500, f_end=2500, curve=1.0):
    """Air movement / whoosh: low-passed noise with a sweeping cutoff."""
    u = np.linspace(0, 1, n) ** curve
    fc = f_start + (f_end - f_start) * u
    x = colored(n, rng, -2.0)
    x = tv_lowpass(tv_lowpass(x, fc), fc * 1.2)
    return x / (np.std(x) + 1e-9) * env


# ----------------------------------------------------------------------------- glass / metal / wood
def glass_ring(f0, dur, rng, amps=(1.0, 0.45, 0.22, 0.10, 0.05), t60=(2.2, 1.3, 0.7, 0.4, 0.25),
               split=1.2, attack=0.0008, ratios=GLASS_RATIOS, phase_rand=True):
    """Struck glass: shell modes, each a split doublet (gentle beating)."""
    n = N(dur)
    t = t_axis(n)
    x = np.zeros(n)
    for r, a, T in zip(ratios, amps, t60):
        f = f0 * r
        if f > 19000:
            continue
        d = split * rng.uniform(0.4, 1.0) * (1 + 0.3 * r)
        for s in (-0.5, 0.5):
            x += 0.5 * a * np.exp(-6.9078 * t / T) * np.sin(TAU * (f + s * d) * t + (rng.uniform(0, TAU) if phase_rand else 0))
    k = min(N(attack), n)
    if k > 1:
        x[:k] *= np.sin(np.linspace(0, np.pi / 2, k)) ** 2
    return x


def glass_clink(f0, rng, dur=0.25, hard=1.0, damp=1.0):
    """Short glass/porcelain contact: click + quickly damped shell modes."""
    x = glass_ring(f0, dur, rng, amps=(1.0, 0.6, 0.35, 0.2, 0.1),
                   t60=tuple(v * damp for v in (0.35, 0.22, 0.14, 0.09, 0.06)), split=2.0)
    c = click(rng, dur=0.0015, lo=2000, hi=14000) * 0.6 * hard
    x[:len(c)] += c
    return x


def glass_crack(rng, dur=0.4, f0=2300, cascade=0.12, level=1.0):
    n = N(dur)
    x = np.zeros(n)
    x[:N(0.0012)] += click(rng, dur=0.0012, lo=800, hi=16000)[:N(0.0012)] * 3.0
    rate = env_points([(0, 2500), (cascade, 200), (cascade * 1.5, 0), (dur, 0)], n)
    c = poisson_clicks(n, rate, rng, amp_sigma=1.0)
    c = bp(grains(c, rng, 0.0006), 2500, 14000)
    x += c / (np.max(np.abs(c)) + 1e-9) * 0.8
    for i in range(3):
        g = glass_ring(f0 * rng.uniform(0.8, 1.6), dur, rng, amps=(1, 0.6, 0.3, 0.15, 0.08),
                       t60=(0.5, 0.3, 0.18, 0.1, 0.06), split=4.0)
        place(x, g * 0.35, rng.uniform(0, cascade * 0.5))
    flex = resonator(click(rng, 0.002, 60, 800), 210, 0.06) * 6
    place(x, flex, 0)
    return x * level


def tinkles(rng, dur=1.0, pieces=5, t_start=0.15, spread=0.6, fmin=3000, fmax=9000, level=0.3):
    """Small glass fragments landing and bouncing."""
    n = N(dur)
    x = np.zeros(n)
    for p in range(pieces):
        t = t_start + rng.uniform(0, spread) * (p / max(pieces - 1, 1)) ** 0.7
        a = level * rng.uniform(0.4, 1.0) * (0.85 ** p)
        f = rng.uniform(fmin, fmax)
        gap = rng.uniform(0.05, 0.11)
        for b in range(rng.integers(2, 5)):
            if t >= dur - 0.02:
                break
            ping = glass_ring(f * rng.uniform(0.97, 1.03), 0.12, rng, amps=(1, 0.5, 0.25, 0.1, 0.05),
                              t60=(0.09, 0.06, 0.04, 0.03, 0.02), split=6)
            place(x, ping, t, a)
            t += gap
            gap *= 0.62
            a *= 0.55
    return x


def metal_click(rng, f0=4200, dur=0.06, damp=1.0, level=1.0):
    """Small metal part (hinge, latch tooth): click + a few inharmonic high modes."""
    n = N(dur)
    ratios = [1.0, 1.73, 2.61, 3.9]
    x = modal(n, [f0 * r for r in ratios], [1.0, 0.5, 0.3, 0.15],
              [0.03 * damp, 0.02 * damp, 0.014 * damp, 0.01 * damp], rng=rng, phase="rand", attack=0.0002)
    c = click(rng, dur=0.0012, lo=2500, hi=15000)
    x[:len(c)] += c * 0.7
    return x * level


def metal_clunk(rng, f0=150, dur=0.5, level=1.0, bright=1.0):
    """Heavy metal part (lever/latch) hitting a stop: low inharmonic modes + thud."""
    n = N(dur)
    ratios = [1.0, 2.56, 4.68, 7.33, 10.6]
    x = modal(n, [f0 * r for r in ratios], [1.0, 0.7, 0.45 * bright, 0.25 * bright, 0.12 * bright],
              [0.28, 0.2, 0.12, 0.08, 0.05], rng=rng, phase="rand", attack=0.0004)
    x += 0.8 * thud(rng, f0=f0 * 0.55, dur=dur, t60=0.12, drop=1.3)
    c = click(rng, dur=0.002, lo=800, hi=9000)
    x[:len(c)] += c * 0.8
    return x * level


def wood_knock(rng, f0=200, dur=0.25, hard=0.5, t60=0.12, level=1.0):
    """Wooden board/box impact: inharmonic, strongly damped modes."""
    n = N(dur)
    ratios = [1.0, 1.58, 2.37, 3.31, 4.62]
    amps = [1.0, 0.75, 0.5 * (0.5 + hard), 0.3 * (0.4 + hard), 0.15 * hard]
    T = [t60, t60 * 0.7, t60 * 0.45, t60 * 0.3, t60 * 0.2]
    x = modal(n, [f0 * r * rng.uniform(0.97, 1.03) for r in ratios], amps, T, rng=rng, phase="rand", attack=0.0006)
    c = click(rng, dur=0.002 + 0.002 * (1 - hard), lo=300, hi=3000 + 7000 * hard)
    x[:len(c)] += c * (0.5 + hard)
    return x * level


def thud(rng, f0=90, dur=0.3, t60=0.12, drop=1.35, drop_t=0.025, noise=0.3):
    """Low body thump with a fast pitch drop (heavy contact)."""
    n = N(dur)
    t = t_axis(n)
    f = f0 * (1 + (drop - 1) * np.exp(-t / drop_t))
    x = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-6.9078 * t / t60)
    nz = lp(rng.standard_normal(n), f0 * 6, 2) * np.exp(-t / 0.012)
    x = x + noise * nz / (np.max(np.abs(nz)) + 1e-9)
    k = N(0.002)
    x[:k] *= np.sin(np.linspace(0, np.pi / 2, k)) ** 2  # raised-cosine onset: no slope kink at the attack
    return x


def mech_click(rng, dur=0.05, f=(1300, 2700, 4300), t60=(0.02, 0.014, 0.009), bright=1.0, body=320):
    """Plastic/metal mechanism click (switch, button, detent)."""
    n = N(dur)
    exc = np.zeros(n)
    c = click(rng, dur=0.0015, lo=600, hi=9000 * bright)
    exc[:len(c)] += c
    x = resonator_bank(exc, list(f), list(t60), [1.0, 0.7, 0.4]) * 3 + exc
    if body:
        x += 0.5 * resonator(exc, body, 0.03) * 3
    return x


# ----------------------------------------------------------------------------- bells / chimes
def chime(f0, dur, rng, kind="bar", bright=1.0, t60=1.8, split=0.8):
    """Small struck chime/bell (tubular chime ratios by default)."""
    ratios = BAR_RATIOS if kind == "bar" else GLASS_RATIOS
    amps = [1.0, 0.55 * bright, 0.28 * bright, 0.12 * bright]
    T = [t60, t60 * 0.5, t60 * 0.25, t60 * 0.12]
    return glass_ring(f0, dur, rng, amps=amps, t60=T, split=split, ratios=ratios)


def warm_tone(f0, dur, rng, attack=0.004, t60=1.5, partials=((1, 1.0), (2, 0.25), (3, 0.08), (4.2, 0.04)),
              t60_scale=(1.0, 0.6, 0.4, 0.25)):
    """Soft mallet / warm bell-ish tone (nearly harmonic)."""
    n = N(dur)
    t = t_axis(n)
    x = np.zeros(n)
    for (r, a), s in zip(partials, t60_scale):
        f = f0 * r
        if f > 19000:
            continue
        x += a * np.exp(-6.9078 * t / (t60 * s)) * np.sin(TAU * f * t + rng.uniform(0, 0.5))
    k = N(attack)
    x[:k] *= np.sin(np.linspace(0, np.pi / 2, k)) ** 2
    return x


# ----------------------------------------------------------------------------- body sounds
def breath_noise(n, env, rng, inhale=True, mouth=1.0, nose=0.3, shake=0.0, shake_hz=9.0):
    """Breath: turbulent noise shaped by broad vocal-tract resonances."""
    x = colored(n, rng, -1.0)
    m = (peak_eq(x, 520, 9, 1.2) + peak_eq(x, 1450, 7, 1.5) + peak_eq(x, 2600, 6, 1.8)) / 3
    m = bp(m, 250, 7000, 2)
    ns = bp(colored(n, rng, -3.0), 180, 1800, 2)
    ns = peak_eq(ns, 280, 8, 2.0)
    y = mouth * m / (np.std(m) + 1e-9) + nose * ns / (np.std(ns) + 1e-9)
    if inhale:
        y = y + 0.18 * hp(colored(n, rng, 0.0), 3500, 2)
    y = lp(y, 8000, 2)
    if shake > 0:
        s = 1 + shake * np.sin(TAU * shake_hz * t_axis(n) + rng.uniform(0, TAU)) * smooth_random(n, 3, rng, 0.4, 1.0)
        env = env * s
    return y * env


def whisper(n, rng, syll_hz=4.5, level_env=None):
    """Unintelligible whisper: unvoiced noise through randomly moving vowel formants + fricatives."""
    t = t_axis(n)
    # formant tracks hop between random vowel-ish targets every syllable
    k = max(int(n / SR * syll_hz) + 2, 3)
    F1 = np.interp(np.linspace(0, k - 1, n), np.arange(k), rng.uniform(320, 780, k))
    F2 = np.interp(np.linspace(0, k - 1, n), np.arange(k), rng.uniform(950, 2300, k))
    F3 = np.interp(np.linspace(0, k - 1, n), np.arange(k), rng.uniform(2400, 3100, k))
    src = colored(n, rng, -1.5)
    from dsp import tv_bandpass
    v = tv_bandpass(src, F1, 4) + 0.8 * tv_bandpass(src, F2, 6) + 0.4 * tv_bandpass(src, F3, 8)
    v = v / (np.std(v) + 1e-9)
    # syllabic envelope with irregular timing
    ph = np.cumsum(syll_hz * smooth_random(n, 1.5, rng, 0.7, 1.4)) / SR
    syl = np.clip(np.sin(np.pi * (ph % 1.0)), 0, 1) ** 1.5
    gaps = smooth_random(n, 1.2, rng, 0, 1) > 0.18
    syl = syl * (0.25 + 0.75 * gaps)
    # fricatives (s / sh) at some syllable edges
    fr = np.zeros(n)
    for c in rng.uniform(0, n / SR, int(n / SR * syll_hz * 0.45)):
        L = N(rng.uniform(0.05, 0.12))
        band = (4800, 9500) if rng.random() < 0.6 else (2200, 5200)
        b = bp(rng.standard_normal(L), *band) * np.hanning(L)
        place(fr, b / (np.std(b) + 1e-9) * 0.5, c)
    x = v * syl + fr
    if level_env is not None:
        x = x * level_env
    return x


# ----------------------------------------------------------------------------- water
def bubble(rng, f0=None, dur=0.05, rise=0.2, level=1.0):
    """Minnaert bubble: damped sine with rising pitch (drop / trickle sound)."""
    f0 = f0 or rng.uniform(600, 2800)
    n = N(dur)
    t = t_axis(n)
    tau = rng.uniform(0.006, 0.02)
    f = f0 * (1 + rise * t / dur)
    x = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-t / tau)
    x[:N(0.0008)] *= np.linspace(0, 1, N(0.0008))
    return x * level


def drop_splash(rng, level=1.0, bright=1.0):
    """Raindrop on hard ground: tiny click + short splash spray."""
    n = N(0.025)
    x = click(rng, dur=0.0015, lo=1500, hi=12000 * min(bright, 1.3)) * 1.0
    spray = bp(rng.standard_normal(n), 2500, 11000) * np.exp(-np.linspace(0, 8, n))
    y = np.zeros(n)
    y[:len(x)] += x
    y += 0.25 * spray
    return y * level
