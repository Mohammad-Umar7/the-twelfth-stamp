"""Music for The Twelfth Stamp (original synthesis + original composition).

  Tools/.venv/Scripts/python.exe -I Tools/audio/make_music.py            # all six cues
  Tools/.venv/Scripts/python.exe -I Tools/audio/make_music.py mus_menu

Cues
  mus_menu     90 s loop   D minor drone (D2/A2/D3), slow upper harmony, sparse oud-like plucks
  mus_tension  60 s loop   60 bpm lub-dub pulse, dark drone, Eb dissonance, faint bowed glass
  mus_chase    45 s loop   128 bpm (24 bars) staccato low-string ostinato, quarter pulse (no kit),
                           Shepard-Risset riser (1 octave / 15 s), tremolo cluster swelling per phrase
  mus_reveal    8 s        glass cluster swell -> detuned "counterfeit" chime + dark D/Ab/C# bloom (no boom)
  mus_home     45 s        D major: D - G/D - Bm - Asus4-A - D, guitar-like melody, warm pad, fade
  mus_credits  90 s        Dm - Bb - F - C - Dm - Bb - Gm - A ... ending on D major (Picardy), arpeggios + bell melody
Loops are rendered circularly (events fold over the loop point, drones use whole-cycle frequencies,
reverb is a circular convolution, the riser uses the equal-power tail->head crossfade) and start on
a zero crossing. Mastered to -24 LUFS integrated, true peak <= -2 dBTP, encoded libvorbis -q:a 6.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
from scipy.signal import fftconvolve  # noqa: E402

import materials as M  # noqa: E402
from dsp import (SR, TAU, N, t_axis, rng_for, place, fade, env_points, colored, lp, hp, bp, make_ir, pan, fold,  # noqa: E402
                 loop_crossfade, rotate_to_zero_crossing, seam_report, circ_sosfilt, sos, lufs, true_peak_db,
                 norm_lufs, undb, encode_ogg, pluck)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Game", "Assets", "Resources", "Audio", "Music")
WORK = os.path.join(ROOT, "Audio", "_work", "music")
META = os.path.join(ROOT, "Audio", "_work", "music_meta.json")
LIC = "Original composition and synthesis for The Twelfth Stamp (project-owned; no third-party material)"
PCS = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
       "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def m(name):
    """'D3' -> MIDI 50."""
    pc = name[:-1]
    return (int(name[-1]) + 1) * 12 + PCS[pc]


def hz(x):
    if isinstance(x, str):
        x = m(x)
    return 440.0 * 2 ** ((x - 69) / 12)


def P(x):
    return x / (np.max(np.abs(x)) + 1e-12)


def R(x):
    return x / (np.sqrt(np.mean(x ** 2)) + 1e-12)


def Fq(f, L):
    return round(f * L) / L if L else f


# ----------------------------------------------------------------------------- instruments
def pad(f, dur, rng, bright=0.3, attack=2.0, release=3.0, detune=5.0, nh=10, L=None, sustain_env=None):
    """Warm additive pad: three slightly detuned voices (L, centre, R), soft harmonic roll-off."""
    total = dur + release if L is None else dur
    n = N(total)
    t = t_axis(n)
    out = np.zeros((2, n))
    for side, cents, g in [(0, -detune, 1.0), (1, detune, 1.0), (None, 0.0, 0.7)]:
        fd = Fq(f * 2 ** (cents / 1200), L)
        y = np.zeros(n)
        for k in range(1, nh + 1):
            if k * fd > 16000:
                break
            a = (1.0 / k) * np.exp(-(k - 1) * (1.05 - bright) * 0.9)
            y += a * np.sin(TAU * k * fd * t + rng.uniform(0, TAU))
        if side is None:
            out += 0.5 * g * y
        else:
            out[side] += g * y
    if L is None:
        env = np.ones(n)
        a = min(N(attack), n)
        env[:a] = np.sin(np.linspace(0, np.pi / 2, a)) ** 2
        r = N(release)
        s = N(dur)
        env[s:] = np.cos(np.linspace(0, np.pi / 2, n - s)) ** 2 if n > s else env[s:]
        out *= env[None, :]
    if sustain_env is not None:
        out *= sustain_env[None, :]
    return out


def oud(f, rng, vel=0.7, decay=3.0, bright=0.35, dur=None):
    x = pluck(f, dur or decay * 1.2, rng, bright=bright, decay=decay, pos=0.17, inharm=0.0003)
    return P(x) * vel


def guitar(f, rng, vel=0.6, decay=3.5, bright=0.45):
    x = pluck(f, decay * 1.1, rng, bright=bright, decay=decay, pos=0.23, inharm=0.00015)
    return P(x) * vel


def bell_tone(f, rng, vel=0.5, t60=2.6, dur=None):
    x = M.warm_tone(f, dur or t60 * 1.1, rng, attack=0.002, t60=t60,
                    partials=((1, 1.0), (2.0, 0.18), (3.0, 0.06), (4.07, 0.08), (6.2, 0.02)),
                    t60_scale=(1.0, 0.55, 0.35, 0.25, 0.12))
    return P(x) * vel


def bowed_glass(f, dur, rng, vel=0.3, trem=4.5):
    n = N(dur)
    t = t_axis(n)
    env = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 2
    y = np.sin(TAU * f * t) + 0.12 * np.sin(TAU * f * 2.83 * t + 1.0)
    y *= (1 + 0.18 * np.sin(TAU * trem * t + rng.uniform(0, TAU))) * env
    return y * vel


def string_stab(f, rng, vel=0.8, dur=0.2):
    """Staccato low string (bowed spiccato-ish): band-limited saw with fast envelope."""
    n = N(dur + 0.08)
    t = t_axis(n)
    y = np.zeros(n)
    for k in range(1, 26):
        if k * f > 9000:
            break
        y += (1.0 / k) * np.exp(-max(k - 5, 0) * 0.25) * np.sin(TAU * k * f * t + rng.uniform(0, 0.3))
    env = env_points([(0, 0), (0.008, 1.0), (dur * 0.5, 0.55), (dur, 0.25), (dur + 0.08, 0)], n)
    k = N(0.008)
    env[:k] = np.sin(np.linspace(0, np.pi / 2, k)) ** 2  # smooth bow attack (no onset tick)
    y = y * env + 0.12 * np.sin(TAU * f / 2 * t) * env
    return P(y) * vel


def heart(rng, vel=1.0, f0=50):
    x = M.thud(rng, f0, 0.45, 0.14, 1.4, 0.02, noise=0.12)
    x = lp(x, 260) + 0.3 * P(bp(x, 90, 220))
    return P(x) * vel


def shepard(n, octave_seconds, rng, f_lo=55.0, octaves=7):
    """Shepard-Risset glissando: octave-spaced partials rising forever under a fixed spectral bell."""
    t = t_axis(n)
    y = np.zeros(n)
    for k in range(octaves):
        pos = (k + t / octave_seconds) % octaves
        f = f_lo * 2 ** pos
        amp = np.sin(np.pi * pos / octaves) ** 2
        ph = TAU * np.cumsum(f) / SR
        y += amp * (np.sin(ph) + 0.25 * np.sin(2 * ph))
    return y


# ----------------------------------------------------------------------------- helpers
def buf(sec):
    return np.zeros((2, N(sec)))


def put(b, x, t0, p=0.0, g=1.0):
    if x.ndim == 1:
        x = pan(x, p)
    place(b, x * g, t0)


def circ_rev(x, ir, wet):
    w = np.stack([fftconvolve(x[0], ir[0]), fftconvolve(x[1], ir[1])])
    return x + wet * fold(w, x.shape[-1])


def lin_rev(x, ir, wet):
    w = np.stack([fftconvolve(x[0], ir[0]), fftconvolve(x[1], ir[1])])[:, :x.shape[-1]]
    return x + wet * w


# ----------------------------------------------------------------------------- cues
def mus_menu(rng):
    L = 90.0
    n = N(L)
    t = t_axis(n)
    drone = np.zeros((2, n))
    for note, g, br in [("D2", 1.0, 0.25), ("A2", 0.55, 0.25), ("D3", 0.4, 0.3)]:
        drone += g * pad(hz(note), L, rng, bright=br, L=L, nh=9)
    drone *= (1 + 0.15 * np.sin(TAU * t / 30.0))[None, :]
    drone = R(drone)
    up = buf(L + 12)
    secs = [["F3", "A3"], ["G3", "Bb3"], ["F3", "A3", "C4"], ["E3", "G3", "Bb3"]]
    for i, chord in enumerate(secs):
        for note in chord:
            put(up, pad(hz(note), 22.5, rng, bright=0.35, attack=4.0, release=5.0, nh=8), i * 22.5, g=0.5)
    up = R(fold(up, n))
    pl = buf(L + 10)
    phrases = [(3.0, "D4"), (8.5, "F4"), (9.1, "E4"), (9.8, "D4"), (15.5, "A4"), (19.0, "G4"), (19.6, "F4"),
               (25.0, "G4"), (25.6, "Bb4"), (31.0, "A4"), (36.5, "D5"), (37.2, "C5"), (37.9, "Bb4"), (42.0, "G4"),
               (47.5, "A4"), (48.2, "C5"), (54.0, "F4"), (54.6, "E4"), (55.3, "D4"), (61.0, "C5"), (64.5, "A4"),
               (70.0, "Bb4"), (70.7, "A4"), (71.4, "G4"), (76.5, "E4"), (77.1, "Eb4"), (82.0, "D4"), (86.0, "A3")]
    for t0, note in phrases:
        put(pl, oud(hz(note), rng, vel=rng.uniform(0.5, 0.9), decay=rng.uniform(2.5, 3.5)), t0 + rng.normal(0, 0.02),
            p=rng.uniform(-0.35, 0.35))
    pl = fold(pl, n)
    sh = buf(L + 6)
    for t0, note, v in [(12.0, "A5", 0.25), (58.0, "D6", 0.2), (80.0, "F5", 0.2)]:
        put(sh, bell_tone(hz(note), rng, vel=v, t60=3.0), t0, p=rng.uniform(-0.5, 0.5))
    sh = fold(sh, n)
    mix = undb(-4) * drone + undb(-8) * up + undb(-3) * P(pl) * 3.0 + undb(-14) * P(sh) * 3.0
    return circ_rev(mix, make_ir(3.2, rng, hf=0.35, predelay=0.02), 0.35), True


def mus_tension(rng):
    L = 60.0
    n = N(L)
    t = t_axis(n)
    pulse = buf(L + 2)
    for b in range(60):
        acc = 1.0 if b % 4 == 0 else 0.85
        put(pulse, heart(rng, vel=acc * rng.uniform(0.95, 1.05)), b * 1.0)
        put(pulse, heart(rng, vel=0.55 * acc, f0=58), b * 1.0 + 0.27)
    pulse = fold(pulse, n)
    dr = np.zeros((2, n))
    for note, g in [("D1", 0.8), ("D2", 1.0), ("A2", 0.5)]:
        dr += g * pad(hz(note), L, rng, bright=0.12, L=L, nh=8)
    dr *= (1 + 0.2 * np.sin(TAU * t / 20.0))[None, :]
    eb = np.zeros((2, n))
    sw = (0.5 - 0.5 * np.cos(TAU * t / 20.0 + 2.0)) ** 2
    for side, f in [(0, Fq(hz("Eb3"), L)), (1, Fq(hz("Eb3") * 1.003, L))]:
        eb[side] = np.sin(TAU * f * t) * sw
    gl = buf(L + 8)
    for i, t0 in enumerate([8, 23, 38, 53]):
        put(gl, bowed_glass(hz("D6") if i % 2 == 0 else hz("Eb6"), 6.0, rng), t0, p=rng.uniform(-0.6, 0.6))
    gl = fold(gl, n)
    nz = colored(n + N(4), rng, -2.0)
    nz = bp(nz, 200, 800)
    nz = loop_crossfade(np.stack([nz, np.roll(nz, N(0.5))]), n, N(4))
    nz *= (0.5 - 0.5 * np.cos(TAU * t / 15.0))[None, :] ** 2
    mix = P(pulse) * 1.0 + undb(-6) * P(dr) + undb(-16) * P(eb) + undb(-20) * P(gl) + undb(-24) * P(nz)
    return circ_rev(mix, make_ir(2.5, rng, hf=0.35, predelay=0.015), 0.25), True


def mus_chase(rng):
    bpm = 128.0
    beat = 60.0 / bpm
    e8 = beat / 2
    L = 24 * 4 * beat  # 45.0 s
    n = N(L)
    t = t_axis(n)
    roots = ["D2"] * 4 + ["Bb1"] * 4 + ["G1"] * 4 + ["A1"] * 4 + ["D2"] * 4 + ["A1"] * 4
    pat = [0, 0, 12, 0, 7, 0, 12, 7]
    acc = [1.0, 0.6, 0.8, 0.65, 0.95, 0.6, 0.8, 0.7]
    ost = buf(L + 1)
    for bar, root in enumerate(roots):
        r = m(root)
        for i in range(8):
            t0 = bar * 4 * beat + i * e8
            put(ost, string_stab(hz(r + pat[i]), rng, vel=acc[i] * rng.uniform(0.92, 1.0), dur=0.19), t0,
                p=-0.15 if i % 2 else 0.15)
    ost = fold(ost, n)
    pul = buf(L + 1)
    for b in range(96):
        put(pul, heart(rng, vel=1.0 if b % 4 == 0 else 0.7, f0=55), b * beat)
    pul = fold(pul, n)
    Fx = N(3.0)
    sp = shepard(n + Fx, 15.0, rng)
    sp = loop_crossfade(np.stack([sp, np.roll(sp, N(0.013))]), n, Fx)
    sp = lp(sp, 3000)
    phrase = 8 * 4 * beat  # 15 s
    u = (t % phrase) / phrase
    swell = np.where(u < 0.97, u ** 2, (1 - (u - 0.97) / 0.03) * 0.97 ** 2)
    trem = 0.5 + 0.5 * np.sin(TAU * (4 / beat) * t)  # 16th-note tremolo
    cl = np.zeros((2, n))
    for note, p in [("D5", -0.5), ("Eb5", 0.5), ("A5", 0.0)]:
        f = Fq(hz(note), L)
        cl += pan(np.sin(TAU * f * t) + 0.3 * np.sin(TAU * 2 * f * t), p)
    cl *= (swell * trem)[None, :]
    mix = P(ost) + undb(-6) * P(pul) + undb(-11) * P(sp) + undb(-15) * P(cl)
    return circ_rev(mix, make_ir(2.0, rng, hf=0.4, predelay=0.012), 0.2), True


def mus_reveal(rng):
    D = 8.0
    out = buf(D)
    n = N(D)
    t = t_axis(n)
    sw = np.clip(t / 1.6, 0, 1) ** 3 * (t < 1.62)
    gl = np.zeros(n)
    for f in [hz("D6"), hz("Eb6"), hz("A6") * 0.995]:
        gl += (np.sin(TAU * f * t) + 0.15 * np.sin(TAU * 2.83 * f * t)) * (1 + 0.25 * np.sin(TAU * 7.0 * t + rng.uniform(0, TAU)))
    gl *= sw
    k = N(0.02)
    i = N(1.62)
    gl[i - k:i] *= np.linspace(1, 0, k)
    put(out, P(gl) * 0.5, 0.0, p=0.0)
    for t0, f, v in [(1.62, hz("C5"), 1.0), (1.72, hz("F#5") * 0.99, 0.85), (1.84, hz("Bb5") * 0.98, 0.7)]:
        b = bell_tone(f, rng, vel=v, t60=4.0, dur=D - t0)
        b = b * (0.8 + 0.2 * np.sin(TAU * 37 * t_axis(len(b))))
        put(out, b, t0, p=rng.uniform(-0.4, 0.4), g=0.55)
    for note, g in [("D2", 1.0), ("Ab2", 0.7), ("C#3", 0.55), ("Eb5", 0.12)]:
        p_ = pad(hz(note), D - 1.6, rng, bright=0.25, attack=0.35, release=0.01, nh=9)
        e = env_points([(0, 1), (2.0, 0.8), (D - 1.6, 0)], p_.shape[-1]) ** 1.5
        put(out, p_ * e[None, :], 1.6, g=0.28 * g)
    sub = np.sin(TAU * hz("D1") * t) * env_points([(0, 0), (1.4, 0), (2.4, 1), (D, 0)], n) ** 2
    put(out, sub * 0.1, 0.0)
    out = lin_rev(out, make_ir(4.0, rng, hf=0.3, predelay=0.03), 0.4)
    return fade(out, 0.002, 1.5), False


def mus_home(rng):
    D = 45.0
    out = buf(D + 6)
    chords = [(0, 9, ["D3", "F#3", "A3"]), (9, 18, ["D3", "G3", "B3"]), (18, 27, ["B2", "D3", "F#3"]),
              (27, 31.5, ["A2", "D3", "E3"]), (31.5, 36, ["A2", "C#3", "E3"]), (36, 45, ["D3", "F#3", "A3", "D4"])]
    for a, b, notes in chords:
        for note in notes:
            put(out, pad(hz(note), b - a, rng, bright=0.35, attack=2.5, release=3.0, nh=8), a, g=0.18)
    for a, b, note in [(0, 18, "D2"), (18, 27, "B1"), (27, 36, "A1"), (36, 45, "D2")]:
        put(out, pad(hz(note), b - a, rng, bright=0.15, attack=2.0, release=3.0, nh=5), a, g=0.14)
    mel = [(1.5, "F#4"), (3.0, "A4"), (4.5, "D5"), (6.5, "C#5"), (7.5, "A4"),
           (10.0, "B4"), (11.5, "D5"), (13.0, "G4"), (15.0, "A4"), (16.5, "B4"),
           (19.0, "F#4"), (20.5, "B4"), (22.0, "D5"), (24.0, "C#5"), (25.5, "B4"),
           (28.0, "A4"), (29.5, "D5"), (31.0, "E5"), (32.5, "C#5"), (34.0, "A4"),
           (37.0, "F#4"), (38.5, "A4"), (40.0, "D5")]
    for i, (t0, note) in enumerate(mel):
        put(out, guitar(hz(note), rng, vel=rng.uniform(0.45, 0.7)), t0 + rng.normal(0, 0.015), p=rng.uniform(-0.25, 0.25),
            g=0.5)
        if i % 5 == 2:
            put(out, bell_tone(hz(note) * 2, rng, vel=0.25), t0, p=rng.uniform(-0.4, 0.4), g=0.5)
    put(out, bell_tone(hz("A5"), rng, vel=0.2, t60=3.5), 42.0, p=0.3, g=0.5)
    out = lin_rev(out, make_ir(3.0, rng, hf=0.35, predelay=0.02), 0.35)[:, :N(D)]
    fo = env_points([(0, 1), (38.0, 1), (D, 0)], N(D)) ** 1.5
    return fade(out * fo[None, :], 0.01, 0.05), False


def mus_credits(rng):
    D = 90.0
    cd = 5.625
    step = cd / 12
    out = buf(D + 6)
    prog = [("D", "m"), ("Bb", "M"), ("F", "M"), ("C", "M"), ("D", "m"), ("Bb", "M"), ("G", "m"), ("A", "M"),
            ("D", "m"), ("Bb", "M"), ("F", "M"), ("C", "M"), ("G", "m"), ("Bb", "M"), ("A", "M"), ("D", "M")]
    base_oct = {"D": 3, "Bb": 2, "F": 3, "C": 3, "G": 2, "A": 2}
    for ci, (root, q) in enumerate(prog):
        t0 = ci * cd
        r = m(root + str(base_oct[root]))
        third = 3 if q == "m" else 4
        chord = [r, r + third, r + 7]
        last = ci == len(prog) - 1
        for note in chord:
            put(out, pad(hz(note), cd + (3.0 if last else 0.0), rng, bright=0.3, attack=1.8, release=2.5, nh=7), t0, g=0.14)
        put(out, pad(hz(r - 12), cd, rng, bright=0.15, attack=1.2, release=2.0, nh=5), t0, g=0.13)
        arp = [0, 7, 12, 12 + third, 19, 12 + third, 12, 7, 0, 7, 12, 12 + third]
        for k, iv in enumerate(arp):
            if last and k > 3:
                break
            if rng.random() < 0.12 and k not in (0, 6):
                continue
            put(out, guitar(hz(r + iv), rng, vel=(0.55 if k % 3 == 0 else 0.4) * rng.uniform(0.85, 1.0), decay=3.0, bright=0.4),
                t0 + k * step + rng.normal(0, 0.01), p=-0.3 + 0.6 * (k / 11), g=0.45)
    mel = {2: [(0.5, "A4"), (3.0, "C5")], 3: [(0.5, "G4"), (3.0, "E4")], 4: [(0.5, "F4"), (3.0, "D4")],
           5: [(0.5, "D4"), (3.0, "F4")], 6: [(0.5, "Bb4"), (2.5, "A4"), (4.0, "G4")], 7: [(0.5, "E4"), (3.0, "C#5")],
           8: [(0.5, "D5"), (3.0, "A4")], 9: [(0.5, "Bb4"), (3.0, "F4")], 10: [(0.5, "A4"), (3.0, "C5")],
           11: [(0.5, "E5"), (3.0, "G4")], 12: [(0.5, "D5"), (3.0, "Bb4")], 13: [(0.5, "F4"), (3.0, "D5")],
           14: [(0.5, "C#5"), (3.0, "E5")], 15: [(0.5, "F#5"), (2.5, "D5")]}
    for ci, notes in mel.items():
        for dt, note in notes:
            put(out, bell_tone(hz(note), rng, vel=0.55, t60=3.0), ci * cd + dt, p=rng.uniform(-0.2, 0.2), g=0.5)
    out = lin_rev(out, make_ir(3.2, rng, hf=0.35, predelay=0.02), 0.35)[:, :N(D)]
    fo = env_points([(0, 1), (84.0, 1), (D, 0)], N(D)) ** 1.5
    return fade(out * fo[None, :], 0.01, 0.05), False


CUES = {"mus_menu": mus_menu, "mus_tension": mus_tension, "mus_chase": mus_chase, "mus_reveal": mus_reveal,
        "mus_home": mus_home, "mus_credits": mus_credits}
TARGET = -24.0


def main(only):
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(WORK, exist_ok=True)
    meta = {}
    if os.path.exists(META):
        with open(META, encoding="utf-8") as fh:
            meta = json.load(fh)
    for cid, fn in CUES.items():
        if only and cid not in only:
            continue
        rng = rng_for(cid)
        x, loop = fn(rng)
        if loop:
            x = circ_sosfilt(sos("highpass", 25.0, 2), x, N(2.0))
            x = x - x.mean(axis=1, keepdims=True)
            x, _ = rotate_to_zero_crossing(x)
        else:
            x = hp(x, 25.0, 2)
            x = fade(x, 0.003, 0.02)
        y = norm_lufs(x, TARGET, tp_max=-2.0, circular=loop)
        wav = os.path.join(WORK, cid + ".wav")
        sf.write(wav, y.T.astype(np.float32), SR, subtype="FLOAT")
        cmd = encode_ogg(wav, os.path.join(OUT, cid + ".ogg"), 6)
        meta[cid] = {"bus": "Music", "loop": loop, "channels": 2, "seconds": round(y.shape[-1] / SR, 3),
                     "synth_lufs": round(lufs(y), 2), "synth_tp": round(true_peak_db(y), 2),
                     "seam": seam_report(y) if loop else None, "source": "synth:Tools/audio/make_music.py", "license": LIC,
                     "encode": " ".join(os.path.relpath(c, ROOT).replace("\\", "/") if os.path.isabs(c) else c for c in cmd)}
        print(f"{cid:12s} {meta[cid]['seconds']:6.2f}s loop={loop} LUFS={meta[cid]['synth_lufs']:6.2f} "
              f"TP={meta[cid]['synth_tp']:6.2f} seam={meta[cid]['seam']}", flush=True)
    with open(META, "w", encoding="utf-8") as fh:
        json.dump(meta, fh, indent=1)


if __name__ == "__main__":
    main(set(sys.argv[1:]))
