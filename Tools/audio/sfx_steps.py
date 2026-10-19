"""Player footsteps for The Twelfth Stamp (original synthesis; replaces the earlier Kenney CC0 wood/stone files).

  foot_<surface>_1..6      walking step: heel strike, toe roll-off 60-110 ms later, sometimes a scuff
  foot_run_<surface>_1..4  heel-heavy running layer, played on top of the walking step on running strides:
                           body-weight thump + push-off scuff + the surface's debris (grit, sand spray, grass swish...)
  foot_wet_1..4            splash layer for wet stone (Paris in the rain)
Surfaces: stone, tile, wood, sand, grass, metal (grate). A street shoe with a rubber/leather sole throughout.
Steps are dry (the game's reverb zone supplies the space) and loudness-matched across surfaces, with soft
ground a little quieter than hard: stone 0, tile +0.5, wood 0, metal +1, sand -1.5, grass -2.5 dB.
"""
import numpy as np

import materials as M
from dsp import (SR, TAU, N, t_axis, place, env_points, lp, hp, bp, resonator_bank, crackle, click, stick_slip,
                 glide_tone)

SURFACES = ("stone", "tile", "wood", "sand", "grass", "metal")
OFFSET_DB = {"stone": 0.0, "tile": 0.5, "wood": 0.0, "sand": -1.5, "grass": -2.5, "metal": 1.0}
WALK_TARGET, RUN_TARGET, WET_TARGET = -15.0, -17.0, -19.0


def pk(x):
    return x / (np.max(np.abs(x)) + 1e-12)


def Z(sec):
    return np.zeros(N(sec))


def _tame(x, c=0.45):
    """Soft-clip the first transient (tanh knee at c of the peak): shaves ~6 dB off a 1-3 ms heel spike while the
    body of the step is barely touched, so hard and soft surfaces can sit at the same loudness. The distortion
    lands on noise (inaudible) and adds a little 3rd harmonic to the thump."""
    x = pk(x)
    return c * np.tanh(x / c)


def _grit(rng, dur, rate, lo=2500, hi=9000):
    n = N(dur)
    return crackle(n, rate * np.exp(-np.linspace(0, 5, n)), rng, lo=lo, hi=hi, grain=0.0004, amp_sigma=0.8)


def _scuff(rng, dur, lo, hi, grit=500):
    n = N(dur)
    return M.slide_noise(n, env_points([(0, 0), (dur * 0.3, 1.0), (dur, 0)], n), rng, lo, hi, grit)


def _snap(rng, dur, lo, hi):
    """The heel edge meeting a hard floor: several ms of decaying noise in a 4th-order band (longer than a bare
    click, so it carries the 1-6 kHz 'clack' that makes a hard-soled step read, with a sane crest factor)."""
    n = max(N(dur), 8)
    x = rng.standard_normal(n) * np.exp(-np.linspace(0, 5, n))
    return bp(np.concatenate([x, np.zeros(N(0.012))]), lo, hi, 4)


def _slap(rng, dur, lo, hi):
    """The sole slapping flat after the heel (heel - flat - toe): a short mid-band burst with a 2 ms rise."""
    n = max(N(dur), 8)
    e = np.minimum(np.linspace(0, dur / 0.002, n), 1.0) * np.exp(-np.linspace(0, 5, n))
    return bp(np.concatenate([rng.standard_normal(n) * e, np.zeros(N(0.01))]), lo, hi, 2)


def _contact(rng, f_thump, t60, snap_band, snap_lvl, snap_dur=0.008, modes=None, mode_lvl=0.0, grit=0.0, noise=0.35,
             thump_lvl=0.7, muffle=1400.0):
    """One shoe contact (heel or toe): muffled sole thump, the shoe/foot body ringing briefly, the edge snap,
    optional surface modes and grit."""
    x = Z(0.3)
    th = lp(M.thud(rng, f_thump, 0.25, t60, 1.28, noise=noise), muffle)
    place(x, thump_lvl * pk(th), 0.0)
    if snap_lvl > 0:
        sn = _snap(rng, snap_dur * rng.uniform(0.8, 1.25), snap_band[0], snap_band[1])
        place(x, snap_lvl * pk(sn), rng.uniform(0.0005, 0.002))
        body = resonator_bank(np.concatenate([sn, np.zeros(N(0.08))]), [rng.uniform(170, 230), rng.uniform(380, 480)],
                              [0.04, 0.025], [1.0, 0.6])
        place(x, 0.3 * snap_lvl * pk(body), 0.001)
        if modes:
            exc = Z(0.2)
            place(exc, sn, 0.0)
            f, t, g = zip(*[(fm * rng.uniform(0.96, 1.04), tm, gm) for fm, tm, gm in modes])
            place(x, mode_lvl * pk(resonator_bank(exc, list(f), list(t), list(g))), 0.001)
    if grit > 0:
        place(x, grit * pk(_grit(rng, 0.04, 4000, hi=7000)), 0.001)
    return x


# =============================================================================== walking steps
def step_stone(rng, k):
    """Paving / cobbles / granite: a muffled sole thump, the heel's clack, the stone's tiny 'tock', grit underfoot."""
    x = Z(0.32)
    s = rng.uniform(0.93, 1.07)
    modes = [(950, 0.016, 1.0), (1750, 0.011, 0.6), (3100, 0.007, 0.35)]
    place(x, _contact(rng, 128 * s, 0.06, (1100, 6500), 0.6, 0.01, modes, 0.4, grit=0.25, thump_lvl=0.85), 0.0)
    flat = rng.uniform(0.028, 0.045)
    place(x, rng.uniform(0.5, 0.65) * pk(_slap(rng, rng.uniform(0.02, 0.03), 350, 3200)), flat)
    toe = rng.uniform(0.075, 0.105)
    place(x, rng.uniform(0.4, 0.55) * pk(_contact(rng, 150 * s, 0.035, (1400, 7000), 0.55, 0.007, modes, 0.35, grit=0.2,
                                                  thump_lvl=0.7)), toe)
    if k % 2 == 0:
        place(x, rng.uniform(0.1, 0.18) * pk(_scuff(rng, 0.05, 1000, 6000, 600)), toe + 0.015)
    return _tame(x)


def step_tile(rng, k):
    """Glazed zellige tile: a bright clack that rings briefly, the hollow tock of tile on mortar."""
    x = Z(0.3)
    s = rng.uniform(0.95, 1.05)
    modes = [(2200 * s, 0.05, 1.0), (3150 * s, 0.035, 0.7), (4700 * s, 0.025, 0.4), (6900 * s, 0.015, 0.2)]
    heel = _contact(rng, 112 * s, 0.05, (900, 8000), 0.7, 0.007, modes, 0.8, grit=0.05, thump_lvl=0.75)
    exc = Z(0.08)
    place(exc, _snap(rng, 0.004, 300, 3000), 0.0)
    place(heel, 0.35 * pk(resonator_bank(exc, [rng.uniform(650, 820)], [0.025], [1.0])), 0.0)  # hollow tile on mortar
    place(x, pk(heel), 0.0)
    flat = rng.uniform(0.025, 0.04)
    place(x, rng.uniform(0.5, 0.6) * pk(_slap(rng, rng.uniform(0.016, 0.024), 500, 4000)), flat)
    toe = rng.uniform(0.07, 0.095)
    place(x, rng.uniform(0.38, 0.5) * pk(_contact(rng, 135 * s, 0.03, (1200, 8500), 0.85, 0.005, modes, 0.55,
                                                   thump_lvl=0.45)), toe)
    if k % 3 == 1:
        place(x, 0.1 * pk(_scuff(rng, 0.03, 1000, 5000, 400)), toe + 0.01)
    return _tame(x)


def step_wood(rng, k):
    """Floorboards: board modes and an under-floor cavity, a leather heel tick, now and then a creak."""
    x = Z(0.34)
    f0 = rng.uniform(135, 185)
    heel = M.wood_knock(rng, f0, 0.3, hard=0.7, t60=rng.uniform(0.09, 0.12))
    heel = pk(heel) + 0.5 * pk(lp(M.thud(rng, 100, 0.3, 0.05, 1.25, noise=0.3), 1200))
    cav = resonator_bank(heel, [rng.uniform(200, 260)], [0.05], [1.0])
    heel = pk(heel) + 0.25 * pk(cav)
    place(x, pk(heel), 0.0)
    sn = _snap(rng, 0.008, 900, 6000)
    place(x, 0.45 * pk(sn), 0.001)
    exc = np.concatenate([sn, Z(0.06)])
    place(x, 0.4 * pk(resonator_bank(exc, [rng.uniform(850, 1000), rng.uniform(1500, 1800), rng.uniform(2500, 2900)],
                                     [0.025, 0.015, 0.009], [1.0, 0.6, 0.3])), 0.001)  # the board's upper modes
    flat = rng.uniform(0.03, 0.045)
    sl = _slap(rng, rng.uniform(0.018, 0.026), 250, 2500)
    place(x, rng.uniform(0.4, 0.5) * pk(sl + 0.5 * pk(resonator_bank(sl, [f0 * 1.58], [0.04], [1.0])) * np.max(np.abs(sl))), flat)
    toe = rng.uniform(0.08, 0.11)
    place(x, rng.uniform(0.35, 0.48) * pk(M.wood_knock(rng, f0 * rng.uniform(1.15, 1.35), 0.2, hard=0.6, t60=0.06)), toe)
    place(x, 0.2 * pk(_snap(rng, 0.004, 1200, 6000)), toe + 0.001)
    if k in (1, 4):  # a loose board creaks under the weight
        nc = N(rng.uniform(0.09, 0.15))
        cr = M.wood_creak(nc, env_points([(0, 70), (nc / SR, 140)], nc), rng,
                          env=env_points([(0, 0), (0.02, 1), (nc / SR, 0)], nc), body=(330, 760, 1450, 2600))
        place(x, rng.uniform(0.18, 0.28) * pk(cr), toe + 0.01)
    return _tame(x)


def step_sand(rng, k):
    """Sand: grains crunch and shift under the heel, a soft muffled thud, the toe pushes off with a hiss."""
    x = Z(0.36)
    s = rng.uniform(0.9, 1.1)
    n1 = N(0.16)
    e1 = env_points([(0, 0), (0.01, 1.0), (0.06, 0.6), (0.16, 0)], n1)
    place(x, pk(lp(crackle(n1, 9000 * e1 * s, rng, lo=400, hi=3800, grain=0.0006, amp_sigma=0.7), 5000)), 0.0)
    place(x, 0.7 * pk(lp(M.thud(rng, 75 * s, 0.15, 0.06, 1.15, noise=0.5), 400)), 0.0)
    n2 = N(0.2)
    e2 = env_points([(0, 0), (0.03, 0.7), (0.12, 0.5), (0.2, 0)], n2)
    c2 = lp(crackle(n2, 5000 * e2, rng, lo=500, hi=4200, grain=0.0005, amp_sigma=0.7), 5500)
    shh = lp(rng.standard_normal(n2), 3200) * e2
    place(x, 0.6 * pk(c2) + 0.25 * pk(shh), rng.uniform(0.08, 0.11))
    return x


def step_grass(rng, k):
    """Grass over soft soil: a muffled thud, blades swishing and crackling around the shoe, a damp squelch."""
    x = Z(0.36)
    s = rng.uniform(0.9, 1.1)
    place(x, 0.8 * pk(lp(M.thud(rng, 85 * s, 0.2, 0.05, 1.2, noise=0.5), 600)), 0.0)
    for t0, d, g in [(0.0, rng.uniform(0.14, 0.2), 1.0), (rng.uniform(0.09, 0.13), rng.uniform(0.12, 0.18), 0.7)]:
        n = N(d)
        e = env_points([(0, 0), (0.015, 1.0), (d * 0.4, 0.5), (d, 0)], n) ** 1.2
        sw = M.paper_swish(n, e, rng, rng.uniform(1800, 2600), rng.uniform(4200, 6000), rng.uniform(2500, 3500), q=0.9)
        cr = crackle(n, 1400 * e + 20, rng, lo=2000, hi=9000, grain=0.0004, amp_sigma=0.8)
        place(x, g * (0.7 * pk(sw) + 0.45 * pk(cr)), t0)
    nq = N(0.05)
    sq = bp(rng.standard_normal(nq), 200, 700, 2) * np.hanning(nq)
    place(x, 0.22 * pk(sq), 0.01)
    return lp(x, 11000)


def _plate_modes(rng, scale=1.0):
    base = [(310, 0.25, 1.0), (740, 0.2, 0.8), (1290, 0.15, 0.6), (1980, 0.12, 0.45), (2870, 0.09, 0.3), (4100, 0.06, 0.18)]
    return [(f * scale * rng.uniform(0.96, 1.04), t, g) for f, t, g in base]


def step_metal(rng, k):
    """Steel grate: the heel rings the plate's inharmonic modes, the grate rattles in its frame."""
    x = Z(0.5)
    sc = rng.uniform(0.9, 1.12)
    modes = _plate_modes(rng, sc)
    exc = Z(0.5)
    place(exc, _snap(rng, 0.004, 400, 8000), 0.0)
    for j, (dt, a) in enumerate([(rng.uniform(0.015, 0.025), 0.45), (rng.uniform(0.035, 0.05), 0.25),
                                 (rng.uniform(0.06, 0.08), 0.12)]):
        place(exc, a * pk(_snap(rng, 0.002, 800, 9000)), dt)  # rattle: the grate bouncing in its frame
    f, t, g = zip(*modes)
    ring = resonator_bank(exc, list(f), list(t), list(g))
    place(x, pk(ring), 0.0)
    place(x, 0.6 * pk(lp(M.thud(rng, 115, 0.25, 0.035, 1.3, noise=0.35), 1500)), 0.0)
    place(x, 0.4 * pk(_snap(rng, 0.004, 1500, 9000)), 0.001)
    toe = rng.uniform(0.07, 0.1)
    exc2 = Z(0.35)
    place(exc2, _snap(rng, 0.003, 600, 9000), 0.0)
    place(exc2, 0.3 * pk(_snap(rng, 0.002, 800, 9000)), rng.uniform(0.015, 0.03))
    ring2 = resonator_bank(exc2, [fm * 1.03 for fm in f], [tm * 0.8 for tm in t], list(g))
    place(x, rng.uniform(0.3, 0.42) * pk(ring2), toe)
    return _tame(lp(x, 10000))


STEP = {"stone": step_stone, "tile": step_tile, "wood": step_wood, "sand": step_sand, "grass": step_grass, "metal": step_metal}


# =============================================================================== running layer / wet
def run_layer(rng, surface, k):
    """Heel-heavy layer for running strides: the body's weight lands, the foot pushes off hard."""
    x = Z(0.42)
    place(x, pk(M.thud(rng, rng.uniform(60, 78), 0.3, rng.uniform(0.06, 0.08), 1.35, noise=0.5)), 0.0)
    push = rng.uniform(0.05, 0.075)
    if surface == "stone":
        place(x, 0.5 * pk(_scuff(rng, 0.06, 900, 6500, 900)), push)
        place(x, 0.3 * pk(_grit(rng, 0.05, 6000)), 0.002)
    elif surface == "tile":
        place(x, 0.4 * pk(_scuff(rng, 0.045, 1000, 6000, 500)), push)
        if k == 2:  # rubber sole squeaks on glazed tile
            ns = N(0.045)
            sq = glide_tone(ns, rng.uniform(1700, 2000), rng.uniform(2300, 2700), harmonics=((1, 1), (2, 0.3))) * np.hanning(ns)
            place(x, 0.22 * pk(sq), push + 0.01)
    elif surface == "wood":
        place(x, 0.55 * pk(M.wood_knock(rng, rng.uniform(105, 130), 0.3, hard=0.4, t60=0.11)), 0.0)
        place(x, 0.3 * pk(_scuff(rng, 0.05, 700, 4500, 400)), push)
        if k % 2 == 1:
            nc = N(0.12)
            cr = M.wood_creak(nc, env_points([(0, 90), (0.12, 160)], nc), rng,
                              env=env_points([(0, 0), (0.02, 1), (0.12, 0)], nc))
            place(x, 0.22 * pk(cr), push + 0.02)
    elif surface == "sand":
        ns = N(0.22)
        e = env_points([(0, 0), (0.02, 1.0), (0.1, 0.5), (0.22, 0)], ns)
        spray = lp(crackle(ns, 7000 * e, rng, lo=600, hi=5500, grain=0.0005, amp_sigma=0.8), 7000)
        shh = bp(rng.standard_normal(ns), 900, 5000, 2) * e
        place(x, 0.85 * (0.7 * pk(spray) + 0.4 * pk(shh)), push - 0.02)
    elif surface == "grass":
        ns = N(0.24)
        e = env_points([(0, 0), (0.015, 1.0), (0.1, 0.45), (0.24, 0)], ns) ** 1.2
        sw = M.paper_swish(ns, e, rng, 1600, rng.uniform(4500, 6500), 2600, q=0.8)
        cr = crackle(ns, 2000 * e + 20, rng, lo=1800, hi=9500, grain=0.0004, amp_sigma=0.8)
        place(x, 0.8 * (0.7 * pk(sw) + 0.45 * pk(cr)), push - 0.03)
    elif surface == "metal":
        exc = Z(0.4)
        place(exc, _snap(rng, 0.004, 400, 8000), 0.0)
        for dt, a in [(rng.uniform(0.012, 0.02), 0.6), (rng.uniform(0.03, 0.045), 0.35), (rng.uniform(0.055, 0.075), 0.2)]:
            place(exc, a * pk(_snap(rng, 0.002, 600, 9000)), dt)
        f, t, g = zip(*_plate_modes(rng, rng.uniform(0.85, 1.0)))
        place(x, 0.6 * pk(resonator_bank(exc, list(f), [tt * 1.2 for tt in t], list(g))), 0.0)
    return _tame(lp(x, 11000), 0.5)


def wet_layer(rng, k):
    """A shoe on wet stone: the water film slaps, a fine spray, a few bubbles pop."""
    x = Z(0.3)
    place(x, 0.5 * pk(click(rng, 0.0015, 1500, 11000)), 0.0)
    ns = N(0.07)
    spray = bp(rng.standard_normal(ns), 2000, 11000, 2) * np.exp(-np.linspace(0, 7, ns))
    place(x, pk(spray), 0.002)
    nw = N(0.035)
    slap = bp(rng.standard_normal(nw), 300, 1300, 2) * np.exp(-np.linspace(0, 5, nw))
    place(x, 0.6 * pk(slap), 0.0)
    for _ in range(rng.integers(2, 5)):  # a few tiny bubbles: short, quiet, high
        b = M.bubble(rng, f0=rng.uniform(1500, 4000), dur=0.02, rise=0.25)
        place(x, rng.uniform(0.02, 0.05) * pk(b * np.hanning(len(b)) ** 0.3), rng.uniform(0.01, 0.09))
    place(x, 0.35 * pk(bp(rng.standard_normal(N(0.06)), 1500, 8000) * np.exp(-np.linspace(0, 6, N(0.06)))),
          rng.uniform(0.07, 0.1))  # toe lifts out of the puddle
    return lp(x, 11500)


# =============================================================================== registry
def register(reg):
    for s in SURFACES:
        for i in range(6):
            reg(f"foot_{s}_{i + 1}", (lambda f, j: (lambda rng: f(rng, j)))(STEP[s], i), 0.5,
                norm=("loud", WALK_TARGET + OFFSET_DB[s], -2.0, None, 8.0), fade_out=0.04, group="Player",
                desc=f"walking footstep, {s}", trim=True)
        for i in range(4):
            reg(f"foot_run_{s}_{i + 1}", (lambda ss, j: (lambda rng: run_layer(rng, ss, j)))(s, i), 0.42,
                norm=("loud", RUN_TARGET + OFFSET_DB[s], -2.0, None, 8.0), fade_out=0.04, group="Player",
                desc=f"heel-heavy running layer, {s}", trim=True)
    for i in range(4):
        reg(f"foot_wet_{i + 1}", (lambda j: (lambda rng: wet_layer(rng, j)))(i), 0.3, norm=("loud", WET_TARGET, -2.0, None, 4.0),
            fade_out=0.04, group="Player", desc="wet-stone splash layer (rain)", trim=True)
