"""Player body sounds for The Twelfth Stamp: breathing, cloth rustle, single heartbeats (original synthesis).

One breath call renders one inhale or one exhale:
  * turbulence noise with a time-varying spectral envelope (random-phase overlap-add, dsp.shaped_noise):
      oral path   vocal-tract formants F1..F4 with breath-wide bandwidths (the open glottis damps them)
      nasal path  nasal resonance ~270 Hz + ~1.05 kHz, an anti-resonance ~760 Hz, nostril hiss ~3.4 kHz
      lips        labial friction: the 'f' that ends a running 'hmf'
      teeth       dental hiss on hard inhales and gasps
  * optional voicing: a harmonic glottal source (f0 glide, jitter, shimmer, creak) through the same formants:
    the grunt inside a hard 'hmf', the 'hah' of panting, the ingressive squeak of a gasp, a scared whimper.
    While voiced, the breath noise is pulsed at f0 (breathy phonation)
  * a sample-accurate amplitude envelope (sharp onsets survive the overlap-add smearing), turbulence flutter,
    tremor (fear), catches (the breath stutters), a little chest proximity (low shelf)
Every variant draws its own vowel colour, timing, voicing and noise seed, so no two are alike. Clips are dry:
the game's reverb zone puts them in the room.
"""
import numpy as np

import materials as M
from dsp import (SR, TAU, N, t_axis, place, env_points, shaped_noise, smooth_random, lp, hp, bp, shelf,
                 resonator, crackle, click, glide_tone)

# Male vowel formants (F1..F4, Hz) - Saeed is an adult man.
VOWELS = {
    "schwa": (500, 1480, 2480, 3450), "a": (720, 1100, 2450, 3400), "ae": (660, 1700, 2420, 3350),
    "o": (570, 860, 2420, 3350), "u": (320, 880, 2250, 3300), "i": (290, 2250, 3000, 3700),
    "e": (520, 1820, 2480, 3500), "uh": (640, 1200, 2400, 3400),
}


def pk(x):
    return x / (np.max(np.abs(x)) + 1e-12)


def _bp_mag(f, F, Q):
    """Magnitude of a 2nd-order band-pass (1 at F); arguments broadcast."""
    r = f / np.maximum(F, 1.0)
    return 1.0 / np.sqrt(1.0 + (Q * (r - 1.0 / np.maximum(r, 1e-6))) ** 2)


def vowel(name, rng, spread=0.07, glide=None):
    """Formant tracks [(start, end)] for a vowel (randomised), optionally gliding toward another vowel."""
    a = np.array(VOWELS[name], float) * rng.uniform(1 - spread, 1 + spread, 4)
    b = a if glide is None else np.array(VOWELS[glide], float) * rng.uniform(1 - spread, 1 + spread, 4)
    return [(float(x), float(y)) for x, y in zip(a, b)]


def _voiced(rng, n, dur, f0, F, B, A, tilt, jitter, shimmer, creak, nasal_mix):
    """Harmonic glottal source shaped by the formant tracks. Returns (signal at unit RMS, glottal phase)."""
    t = t_axis(n)
    f0s = (f0[0] + (f0[1] - f0[0]) * t / dur) * (1.0 + jitter * smooth_random(n, 35.0, rng, -1.0, 1.0))
    ph = TAU * np.cumsum(f0s) / SR
    ctrl = 96
    tcs = np.minimum(np.arange(n // ctrl + 2) * ctrl, n - 1)
    u = tcs / max(n - 1, 1)
    Fk = np.array([s + (e - s) * u for s, e in F])
    idx = np.arange(n)
    y = np.zeros(n)
    kmax = int(6000 / max(min(f0), 60.0))
    for k in range(1, kmax + 1):
        fk = k * f0s[tcs]
        amp = sum(A[j] * _bp_mag(fk, Fk[j], Fk[j] / (0.7 * B[j])) for j in range(len(A)))
        if nasal_mix > 0:
            nas = (_bp_mag(fk, 270.0, 270.0 / 90.0) + 0.4 * _bp_mag(fk, 1050.0, 1050.0 / 240.0)) * \
                  (1.0 - 0.8 * _bp_mag(fk, 760.0, 760.0 / 180.0))
            amp = (1 - nasal_mix) * amp + nasal_mix * 1.4 * nas
        amp = amp * k ** tilt
        amp[fk > 7000] = 0
        y += np.interp(idx, tcs, amp) * np.sin(k * ph + rng.uniform(0, 0.3))
    y *= 1.0 + shimmer * smooth_random(n, 60.0, rng, -1.0, 1.0)
    if creak > 0:  # period doubling + irregular dips: the rasp at the end of a hard breath
        y *= 1.0 - creak * (0.5 + 0.5 * np.sin(0.5 * ph + rng.uniform(0, TAU))) * smooth_random(n, 14.0, rng, 0.3, 1.0)
    return y / (np.std(y) + 1e-12), ph


def breath(rng, dur, env, env_pow=1.0, oral=0.0, F=None, B=(170, 230, 320, 430), A=(1.0, 0.75, 0.45, 0.25),
           nasal=0.0, nostril=0.0, nostril_f=3400.0, nostril_q=1.2, lips=0.0, lips_env=None, lips_band=(900, 6500),
           teeth=0.0, tilt=-2.0, voice=0.0, f0=(115.0, 100.0),
           v_env=None, v_tilt=-1.3, jitter=0.012, shimmer=0.1, creak=0.0, v_nasal=0.0, breathy=0.4, tremor=None,
           flutter=0.22, catches=(), low_db=2.5, lp_hz=9500.0, hp_hz=85.0):
    """One inhale or exhale. env / lips_env / v_env: [(fraction of dur, level), ...]."""
    n = N(dur)
    t = t_axis(n)
    F = F or vowel("schwa", rng)

    def mag(tf, f):
        u = np.clip(tf / dur, 0, 1)
        fc = np.maximum(f[:, None], 1.0)
        out = np.zeros((len(f), len(tf)))
        if oral > 0:
            for k, (s, e) in enumerate(F):
                Fk = (s + (e - s) * u)[None, :]
                out += oral * A[k] * _bp_mag(fc, Fk, Fk / B[k])
        if nasal > 0:
            nas = _bp_mag(fc, 270.0, 270.0 / 150.0) + 0.45 * _bp_mag(fc, 1050.0, 1050.0 / 330.0) + \
                  0.18 * _bp_mag(fc, 2300.0, 2300.0 / 650.0)
            out += nasal * nas * (1.0 - 0.72 * _bp_mag(fc, 760.0, 760.0 / 230.0))
        if nostril > 0:
            out += nostril * _bp_mag(fc, nostril_f, nostril_q)
        if teeth > 0:
            out += teeth * _bp_mag(fc, 6000.0, 1.7)
        shape = (fc / 1000.0) ** (tilt / 6.02)
        shape = shape / np.sqrt(1.0 + (hp_hz / fc) ** 8) / np.sqrt(1.0 + (fc / lp_hz) ** 8)
        return out * shape

    x = shaped_noise(n, mag, rng, nfft=512)
    x = x / (np.std(x) + 1e-12)
    e = env_points([(p * dur, v) for p, v in env], n) ** env_pow
    y = x * (1.0 + flutter * smooth_random(n, 40.0, rng, -1.0, 1.0))
    if lips > 0:
        le = env_points([(p * dur, v) for p, v in (lips_env or env)], n)
        fr = bp(rng.standard_normal(n), lips_band[0], lips_band[1], 2)
        fr = fr + 0.6 * resonator(fr, rng.uniform(0.35, 0.45) * lips_band[1], 0.004)
        y = y + lips * (fr / (np.std(fr) + 1e-12)) * le / np.maximum(e, 0.05)
    if voice > 0:
        v, ph = _voiced(rng, n, dur, f0, F, B, A, v_tilt, jitter, shimmer, creak, v_nasal)
        ve = env_points([(p * dur, val) for p, val in (v_env or env)], n)
        y = y * (1.0 - breathy * ve * 0.5 * (1.0 - np.cos(ph)))  # aspiration pulses with the glottis
        y = y + voice * v * ve / np.maximum(e, 0.05)
    y = y * e
    if tremor is not None:
        rate, depth = tremor[0], tremor[1]
        tenv = env_points([(p * dur, val) for p, val in tremor[2]], n) if len(tremor) > 2 else 1.0
        y *= 1.0 + depth * tenv * np.sin(TAU * rate * (1 + 0.08 * smooth_random(n, 2.0, rng, -1, 1)) * t
                                         + rng.uniform(0, TAU)) * smooth_random(n, 3.0, rng, 0.5, 1.0)
    for c, depth, width in catches:
        y *= 1.0 - depth * np.exp(-0.5 * ((t - c * dur) / (width / 2.0)) ** 2)
    if low_db:
        y = shelf(y, 260.0, low_db, high=False)
    y = hp(y, 60.0, 2)
    return y


# =============================================================================== breath kinds
def calm_out(rng, k):
    """Quiet nasal exhale at a walk."""
    d = rng.uniform(1.0, 1.45)
    env = [(0, 0), (0.18, 0.75), (0.35, 1.0), (0.6, 0.7), (0.85, 0.25), (1, 0)]
    return breath(rng, d, env, env_pow=1.4, oral=0.12, F=vowel("schwa", rng), nasal=1.0, nostril=0.25, tilt=-4.0,
                  flutter=0.15, low_db=3.0, lp_hz=7000.0)


def calm_in(rng, k):
    """Quiet nasal inhale: a soft, slightly whistly sniff of air."""
    d = rng.uniform(0.85, 1.25)
    env = [(0, 0), (0.3, 0.6), (0.7, 1.0), (0.9, 0.6), (1, 0)]
    return breath(rng, d, env, env_pow=1.3, oral=0.08, nasal=0.6, nostril=0.6 + 0.2 * (k % 2), tilt=-2.8,
                  flutter=0.18, low_db=2.0, lp_hz=8000.0)


def run_out(rng, k):
    """'hmf': a forceful exhale through the nose (lips nearly shut) as the foot lands; a short grunt in some."""
    d = rng.uniform(0.27, 0.36)
    a = rng.uniform(0.012, 0.022)
    p = a + rng.uniform(0.012, 0.03)
    env = [(0, 0), (a / d, 0.8), (p / d, 1.0), ((p + 0.055) / d, 0.55), ((p + 0.13) / d, 0.24), (0.85, 0.08), (1, 0)]
    lips_env = [(0, 0), (p / d, 0.2), ((p + 0.07) / d, 0.55), ((p + 0.16) / d, 0.3), (1, 0)]
    voiced = k in (1, 3, 4)
    v_env = [(0, 0), (a / d, 0.6), (p / d, 1.0), ((p + 0.07) / d, 0.35), ((p + 0.13) / d, 0.0), (1, 0)]
    return breath(rng, d, env, env_pow=1.15, oral=rng.uniform(0.2, 0.35), F=vowel(rng.choice(["schwa", "uh", "o"]), rng),
                  B=(200, 260, 340, 450), nasal=rng.uniform(0.85, 1.0), nostril=rng.uniform(0.1, 0.2), nostril_f=2700.0,
                  nostril_q=1.8, lips=rng.uniform(0.12, 0.22), lips_env=lips_env, lips_band=(600, 4200), tilt=-3.4,
                  voice=rng.uniform(0.35, 0.55) if voiced else 0.0, f0=(rng.uniform(108, 126), rng.uniform(90, 102)),
                  v_env=v_env, v_nasal=0.75, jitter=0.02, flutter=0.28, low_db=3.5)


def run_in(rng, k):
    """Quick mouth inhale between strides: rises as the chest opens, then the glottis cuts it off."""
    d = rng.uniform(0.21, 0.32)
    cut = rng.uniform(0.8, 0.9)
    env = [(0, 0), (0.12, 0.4), (0.5, 0.8), (cut, 1.0), (cut + 0.06, 0.4), (1, 0)]
    F = vowel(rng.choice(["a", "ae", "uh"]), rng, glide=rng.choice(["a", "ae"]))
    return breath(rng, d, env, env_pow=1.1, oral=1.0, F=F, B=(220, 280, 360, 460), nasal=0.15, teeth=rng.uniform(0.12, 0.22),
                  tilt=-1.7, flutter=0.3, low_db=1.5)


def hard_out(rng, k):
    """Exhausted exhale: open mouth, voiced 'huh'/'hah' with a rasp at the end; some come as a double push."""
    d = rng.uniform(0.33, 0.46)
    a = rng.uniform(0.008, 0.016)
    if k % 2 == 1:  # 'h-HAH': a quick first push, then the main one
        q = rng.uniform(0.06, 0.09)
        env = [(0, 0), (a / d, 0.55), ((a + 0.025) / d, 0.25), (q / d, 0.3), ((q + a) / d, 1.0), ((q + 0.09) / d, 0.6),
               ((q + 0.2) / d, 0.2), (1, 0)]
        v_env = [(0, 0), (q / d, 0.2), ((q + a) / d, 1.0), ((q + 0.12) / d, 0.6), ((q + 0.2) / d, 0.25), (1, 0)]
    else:
        env = [(0, 0), (a / d, 1.0), (0.12 / d, 0.75), (0.24 / d, 0.38), (0.85, 0.1), (1, 0)]
        v_env = [(0, 0), (a / d, 1.0), (0.13 / d, 0.65), (0.22 / d, 0.3), (0.85, 0.05), (1, 0)]
    return breath(rng, d, env, env_pow=1.1, oral=1.0, F=vowel(rng.choice(["uh", "a", "schwa"]), rng), nasal=0.25,
                  lips=0.15, tilt=-2.0, voice=rng.uniform(0.6, 0.9), f0=(rng.uniform(118, 136), rng.uniform(92, 104)),
                  v_env=v_env, v_tilt=-1.15, jitter=0.03, shimmer=0.18, creak=rng.uniform(0.35, 0.65), v_nasal=0.15,
                  breathy=0.55, flutter=0.32, low_db=3.0)


def hard_in(rng, k):
    """Ragged gasping inhale: loud, raspy, a faint ingressive squeak, the breath catching once or twice."""
    d = rng.uniform(0.26, 0.38)
    cut = rng.uniform(0.82, 0.9)
    env = [(0, 0), (0.08, 0.55), (0.4, 0.85), (cut, 1.0), (cut + 0.05, 0.35), (1, 0)]
    catches = [(rng.uniform(0.3, 0.55), rng.uniform(0.35, 0.6), rng.uniform(0.02, 0.035))]
    if k % 2 == 0:
        catches.append((rng.uniform(0.6, 0.75), rng.uniform(0.25, 0.45), 0.025))
    v_env = [(0, 0), (0.2, 0.0), (0.45, 1.0), (cut, 0.7), (cut + 0.05, 0.0), (1, 0)]
    return breath(rng, d, env, env_pow=1.05, oral=1.0, F=vowel("a", rng, glide="ae"), B=(230, 300, 380, 480), nasal=0.1,
                  teeth=rng.uniform(0.3, 0.45), tilt=-1.0, voice=rng.uniform(0.1, 0.22),
                  f0=(rng.uniform(250, 320), rng.uniform(340, 440)), v_env=v_env, v_tilt=-1.8, jitter=0.06, shimmer=0.3,
                  breathy=0.2, catches=catches, flutter=0.38, low_db=1.0)


def pant_out(rng, k):
    """Recovery panting, out: open-mouth 'hah', short, sometimes lightly voiced."""
    d = rng.uniform(0.17, 0.25)
    env = [(0, 0), (0.07, 1.0), (0.35, 0.6), (0.75, 0.2), (1, 0)]
    voiced = k % 2 == 0
    return breath(rng, d, env, env_pow=1.2, oral=1.0, F=vowel(rng.choice(["a", "ae", "uh"]), rng), nasal=0.12,
                  tilt=-1.8, voice=rng.uniform(0.18, 0.32) if voiced else 0.0, f0=(rng.uniform(112, 124), rng.uniform(98, 108)),
                  v_env=[(0, 0), (0.07, 1.0), (0.4, 0.3), (0.7, 0), (1, 0)], breathy=0.5, flutter=0.25, low_db=2.0)


def pant_in(rng, k):
    """Recovery panting, in: a short open-mouth draw of air."""
    d = rng.uniform(0.15, 0.22)
    env = [(0, 0), (0.2, 0.55), (0.75, 1.0), (0.88, 0.5), (1, 0)]
    return breath(rng, d, env, env_pow=1.1, oral=0.95, F=vowel("a", rng, glide="ae"), nasal=0.08, teeth=0.15,
                  tilt=-1.4, flutter=0.28, low_db=1.0)


def fear_in(rng, k):
    """Scared inhale: sharp, tense ('ee'-coloured), trembling, catching; a faint whimper on some."""
    d = rng.uniform(0.34, 0.58)
    cut = rng.uniform(0.82, 0.92)
    env = [(0, 0), (0.06, 0.7), (0.35, 0.8), (cut, 1.0), (cut + 0.05, 0.3), (1, 0)]
    catches = [(rng.uniform(0.25, 0.45), rng.uniform(0.4, 0.65), rng.uniform(0.025, 0.04)),
               (rng.uniform(0.55, 0.75), rng.uniform(0.3, 0.55), 0.03)]
    voiced = k in (1, 3)
    return breath(rng, d, env, env_pow=1.05, oral=1.0, F=vowel(rng.choice(["i", "e"]), rng, glide="ae"),
                  B=(200, 260, 340, 440), nasal=0.15, teeth=rng.uniform(0.25, 0.4), tilt=-1.2,
                  voice=rng.uniform(0.12, 0.2) if voiced else 0.0, f0=(rng.uniform(220, 260), rng.uniform(280, 340)),
                  v_env=[(0, 0), (0.3, 0.0), (0.55, 1.0), (cut, 0.6), (cut + 0.05, 0), (1, 0)], v_tilt=-1.7,
                  jitter=0.05, shimmer=0.3, breathy=0.2, tremor=(rng.uniform(8.0, 11.0), rng.uniform(0.45, 0.65)),
                  catches=catches, flutter=0.3, low_db=1.0)


def fear_out(rng, k):
    """Scared exhale: a long trembling breath out, a whimper in the tail."""
    d = rng.uniform(0.55, 0.85)
    env = [(0, 0), (0.05, 0.85), (0.15, 1.0), (0.5, 0.6), (0.85, 0.2), (1, 0)]
    return breath(rng, d, env, env_pow=1.1, oral=0.85, F=vowel(rng.choice(["uh", "o", "schwa"]), rng, glide="u"),
                  nasal=0.3, lips=0.1, tilt=-2.2, voice=rng.uniform(0.15, 0.28), f0=(rng.uniform(150, 175), rng.uniform(118, 132)),
                  v_env=[(0, 0), (0.25, 0.0), (0.45, 1.0), (0.8, 0.6), (1, 0)], v_tilt=-1.5, jitter=0.035, shimmer=0.25,
                  creak=0.25, breathy=0.45, tremor=(rng.uniform(6.0, 9.0), rng.uniform(0.45, 0.6)), flutter=0.25, low_db=2.5)


def gasp(rng, k):
    """Startle gasp: an instant, loud ingressive 'ah' through the open mouth, cut off by the glottis."""
    d = rng.uniform(0.32, 0.44)
    a = rng.uniform(0.006, 0.011)
    cut = rng.uniform(0.78, 0.88)
    env = [(0, 0), (a / d, 1.0), (0.18, 0.85), (0.5, 0.7), (cut, 0.65), (cut + 0.04, 0.15), (1, 0)]
    return breath(rng, d, env, env_pow=1.0, oral=1.0, F=vowel(rng.choice(["a", "ae"]), rng, glide="uh"),
                  B=(240, 300, 380, 480), nasal=0.08, teeth=rng.uniform(0.3, 0.45), tilt=-1.0,
                  voice=rng.uniform(0.38, 0.52), f0=(rng.uniform(260, 300), rng.uniform(360, 430)),
                  v_env=[(0, 0), (a / d, 0.7), (0.12, 1.0), (0.55, 0.7), (cut, 0.4), (cut + 0.04, 0), (1, 0)],
                  v_tilt=-1.55, jitter=0.06, shimmer=0.3, creak=0.2, breathy=0.3, flutter=0.3, low_db=1.5)


def relief(rng, k):
    """Long shaky exhale once the danger has passed."""
    d = rng.uniform(1.1, 1.45)
    env = [(0, 0), (0.08, 0.8), (0.2, 1.0), (0.55, 0.7), (0.85, 0.25), (1, 0)]
    return breath(rng, d, env, env_pow=1.15, oral=0.75, F=vowel(rng.choice(["o", "uh"]), rng, glide="u"), nasal=0.4,
                  lips=0.2, tilt=-2.6, voice=0.14, f0=(rng.uniform(106, 114), rng.uniform(92, 98)),
                  v_env=[(0, 0), (0.55, 0.0), (0.75, 1.0), (0.95, 0.4), (1, 0)], v_tilt=-1.4, jitter=0.03, creak=0.35,
                  breathy=0.5, tremor=(rng.uniform(5.5, 7.0), 0.4, [(0, 1.0), (0.5, 0.6), (1.0, 0.1)]), flutter=0.2,
                  low_db=3.0, lp_hz=8000.0)


# =============================================================================== cloth / heart
def cloth(rng, k):
    """Jacket and trousers on a running stride: a swept swish, nylon crinkle, low rub (one with a zip tick)."""
    d = rng.uniform(0.17, 0.26)
    n = N(d)
    env = env_points([(0, 0), (rng.uniform(0.025, 0.05), 1.0), (d * 0.5, 0.45), (d, 0)], n) ** 1.3
    hi = 1.0 if k != 5 else 0.65  # the last variant is denim: lower and rougher
    sw = M.paper_swish(n, env, rng, rng.uniform(900, 1400) * hi, rng.uniform(2600, 3600) * hi, rng.uniform(1500, 2100) * hi,
                       q=0.85, peak_at=rng.uniform(0.3, 0.5))
    cr = crackle(n, (900 if k != 5 else 300) * env + 30, rng, lo=1800 * hi, hi=8000, grain=0.0005, amp_sigma=0.9)
    rub = bp(rng.standard_normal(n), 250, 1100, 2) * env
    x = 0.8 * pk(sw) + (0.35 if k != 5 else 0.15) * pk(cr) + 0.3 * pk(rub)
    if k == 4:  # a zip pull swings and ticks
        tick = M.metal_click(rng, rng.uniform(3600, 4400), 0.04, damp=0.6)
        place(x, 0.18 * pk(tick), rng.uniform(0.05, 0.09))
    return lp(x, 10000)


def heart_beat(rng, k):
    """A single 'lub-dub', felt more than heard: low thumps with a little 100-250 Hz body so headsets carry it."""
    x = np.zeros(N(0.7))
    s = 1.0 + rng.uniform(-0.04, 0.04)
    lub = M.thud(rng, 50.0 * s, 0.4, rng.uniform(0.11, 0.14), 1.32, 0.02, noise=0.14)
    lub = pk(lub) + 0.35 * pk(bp(lub, 90, 240))
    dub = M.thud(rng, rng.uniform(64, 72), 0.3, rng.uniform(0.07, 0.09), 1.28, 0.015, noise=0.12)
    dub = pk(dub) + 0.3 * pk(bp(dub, 110, 280))
    lub = np.tanh(1.6 * pk(lub)) / np.tanh(1.6)  # a little 2nd/3rd harmonic: headset drivers can carry 100-200 Hz
    knock = bp(rng.standard_normal(N(0.025)), 110, 380, 2) * np.exp(-np.linspace(0, 6, N(0.025)))
    place(x, pk(lub), 0.004)
    place(x, 0.4 * pk(knock), 0.004)
    place(x, rng.uniform(0.62, 0.8) * pk(dub), rng.uniform(0.27, 0.31))
    return lp(x, 650, 2)


# =============================================================================== registry
def register(reg):
    """Breath clips are matched within each kind (loudness at clip level, peak short-window K-weighted LUFS);
    PlayerAudio sets the final levels. Targets: running 'hmf' -16, inhale -18, hard -15/-16, panting -18,
    calm -24, fear -17/-19, gasp -14, relief -18, cloth -20, heartbeat -17."""
    kinds = [  # (prefix, fn, count, max seconds, loudness target, description)
        ("breath_calm_out", calm_out, 3, 1.5, -24.0, "quiet nasal exhale (walking)"),
        ("breath_calm_in", calm_in, 3, 1.3, -24.0, "quiet nasal inhale (walking)"),
        ("breath_run_out", run_out, 6, 0.4, -16.0, "running exhale 'hmf' through the nose, some with a grunt"),
        ("breath_run_in", run_in, 6, 0.36, -18.0, "quick mouth inhale between running strides"),
        ("breath_hard_out", hard_out, 4, 0.5, -15.0, "exhausted voiced exhale 'huh', rasping"),
        ("breath_hard_in", hard_in, 4, 0.42, -16.0, "ragged gasping inhale, catching"),
        ("breath_pant_out", pant_out, 4, 0.28, -18.0, "recovery panting, out"),
        ("breath_pant_in", pant_in, 4, 0.25, -18.0, "recovery panting, in"),
        ("breath_fear_in", fear_in, 4, 0.62, -17.0, "scared trembling inhale"),
        ("breath_fear_out", fear_out, 3, 0.9, -19.0, "scared trembling exhale with a whimper"),
        ("breath_gasp", gasp, 3, 0.48, -14.0, "startle gasp"),
        ("breath_relief", relief, 2, 1.5, -18.0, "long shaky exhale of relief"),
        ("cloth_run", cloth, 6, 0.28, -20.0, "jacket/trouser rustle on a running stride"),
        ("heart_beat", heart_beat, 4, 0.7, -17.0, "single heartbeat (lub-dub)"),
    ]
    for prefix, fn, count, dur, target, desc in kinds:
        for i in range(count):
            reg(f"{prefix}_{i + 1}", (lambda f, j: (lambda rng: f(rng, j)))(fn, i), dur, norm=("loud", target, -2.0),
                fade_out=0.01, group="Body", desc=desc, trim=True)
