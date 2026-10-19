"""Tileable material textures (albedo + tangent-space normal) for The Twelfth Stamp.

Every generator receives (n, rng) and returns a dict with some of:
  albedo : float32 (n, n, 3) sRGB-encoded colour in [0, 1]
  height : float32 (n, n) height field (normal map is derived with wrap-around Sobel)
  nstr   : normal strength (pixels of relief per 1.0 height unit)
  normal : float32 (n, n, 3) unit normals (overrides height when present)
  mask   : float32 (n, n) for grayscale masks
All operations are periodic so the textures tile in both axes. Layout grids are offset so that
joints never sit exactly on the tile border (keeps the seam check meaningful).
"""
from __future__ import annotations

import math

import numpy as np

from tx_core import (F32, C, SS, blur, clamp01, crack_paths, mix, ramp, relaxed_points, saturate, sd_box,
                     smoothstep, spectral, uv_grid, voronoi, warp)

MATERIALS: dict[str, dict] = {}


def material(name, size=1024, purpose="", method="", kind="std"):
    """kind: std (albedo+normal), normal (normal only), mask (grayscale mask only)."""
    def deco(fn):
        MATERIALS[name] = dict(fn=fn, size=size, purpose=purpose, method=method, kind=kind)
        return fn
    return deco


# --------------------------------------------------------------------------- shared helpers
def speckle(n, rng, density, sigma=0.7):
    """Periodic random dots (approx. unit peak) using a sparse field + wrap blur."""
    s = (rng.random((n, n)) < density).astype(F32)
    b = blur(s, sigma)
    return clamp01(b / (b.max() + 1e-6) * 1.6)


def rows_layout(n, rows, ox=0.0, oy=0.0):
    """Periodic course layout. rows: list of dicts {h, widths, off}; heights sum to n, widths to n.
    Returns per-pixel row, blk (unique id), lx, ly (local px), bw, bh."""
    H = np.array([r["h"] for r in rows], np.float64)
    ys = np.concatenate([[0.0], np.cumsum(H)])
    x = np.arange(n, dtype=np.float64) + 0.5
    y = np.arange(n, dtype=np.float64) + 0.5
    yy = (y - oy) % n
    row_of = np.clip(np.searchsorted(ys, yy, side="right") - 1, 0, len(rows) - 1)
    out = {k: np.zeros((n, n), F32) for k in ("lx", "ly", "bw", "bh")}
    out["row"] = np.zeros((n, n), np.int32)
    out["blk"] = np.zeros((n, n), np.int32)
    base = 0
    for r, R in enumerate(rows):
        sel = np.where(row_of == r)[0]
        ws = np.array(R["widths"], np.float64)
        if len(sel):
            xs = np.concatenate([[0.0], np.cumsum(ws)])
            xx = (x - ox - R["off"]) % n
            b = np.clip(np.searchsorted(xs, xx, side="right") - 1, 0, len(ws) - 1)
            lx = xx - xs[b]
            out["lx"][sel] = lx[None, :]
            out["bw"][sel] = ws[b][None, :]
            out["blk"][sel] = (base + b)[None, :]
            out["ly"][sel] = (yy[sel] - ys[r])[:, None]
            out["bh"][sel] = R["h"]
            out["row"][sel] = r
        base += len(ws)
    out["count"] = base
    return out


def edge_dist(L):
    return np.minimum(np.minimum(L["lx"], L["bw"] - L["lx"]), np.minimum(L["ly"], L["bh"] - L["ly"]))


def random_widths(rng, n, lo, hi):
    ws = []
    while sum(ws) < n:
        ws.append(rng.uniform(lo, hi))
    ws = np.array(ws)
    if len(ws) > 1 and (sum(ws) - n) > ws[-1] * 0.5:
        ws = ws[:-1]
    return list(ws * (n / ws.sum()))


def fibres(n, rng, count, length=(8, 40), width=(0.6, 1.2), curl=0.03, ss=2, value=(150, 255)):
    """Periodic mask of thin curved strands (paper / washi fibres)."""
    cv = SS(n, n, ss=ss, wrap=True)
    for _ in range(count):
        x, y = rng.uniform(0, n, 2)
        a = rng.uniform(0, 2 * math.pi)
        L = rng.uniform(*length)
        k = rng.normal(0, curl)
        steps = max(3, int(L / 3))
        dl = L / steps
        pts = [(x, y)]
        for _ in range(steps):
            a += k * dl + rng.normal(0, 0.04)
            x += math.cos(a) * dl
            y += math.sin(a) * dl
            pts.append((x, y))
        cv.line(pts, rng.uniform(*width), fill=int(rng.uniform(*value)), round_caps=False)
    return cv.cov()


def periodic_disp(n, cx, cy):
    """Wrapped pixel displacement from (cx, cy) on an n-torus."""
    yy, xx = np.mgrid[0:n, 0:n].astype(F32)
    dx = (xx + 0.5 - cx + n / 2) % n - n / 2
    dy = (yy + 0.5 - cy + n / 2) % n - n / 2
    return dx, dy


def wood_rings(n, rng, ring_px=(20, 44), wave_px=7.0, fine_px=1.2, xs=None):
    """Straight vertical growth-ring coordinate (grain along v).

    Rings are explicit vertical stripes with random widths (ring_px) that partition the tile width, so
    the ring index is monotonic across the grain (no closed 'eye' contours). A slow periodic warp of
    +-wave_px and a fine one of +-fine_px make the lines gently wavy (|dw/dx| << 1 keeps them monotonic).
    xs: optional per-pixel x coordinate (px) to use instead of the image column (planks).
    Returns r = ring index + fraction inside the ring."""
    yy, xx = np.mgrid[0:n, 0:n].astype(F32)
    widths = np.array(random_widths(rng, n, *ring_px))
    bounds = np.concatenate([[0.0], np.cumsum(widths)])
    w = (wave_px * spectral((n, n), rng, beta=3.0, fmin=1, fmax=5, elong=(1, 0.6))
         + fine_px * spectral((n, n), rng, beta=2.0, fmin=4, fmax=40, elong=(1, 4)))
    base = (xx + 0.5) if xs is None else xs
    xp = np.mod(base + w, n)
    k = np.clip(np.searchsorted(bounds, xp, side="right") - 1, 0, len(widths) - 1)
    return (k + (xp - bounds[k]) / widths[k]).astype(F32)


def ring_profile(r):
    """Earlywood -> latewood gradient with a thin dark latewood line. Returns (tone 0..1, line 0..1)."""
    p = r - np.floor(r)
    tone = smoothstep(0.0, 0.85, p)
    line = smoothstep(0.78, 0.9, p) * (1 - smoothstep(0.93, 1.0, p))
    return p, tone, line


# =========================================================================== paper / fabric
@material("paper_ivory", 1024, "Passport pages, notes, menu cards: tactile ivory paper with fine fibres.",
          "Fourier-domain fBm 'flocs' + fine grain + ~8k short fibre strands drawn with wrap-around; "
          "height = fibres + flocs.")
def paper_ivory(n, rng):
    form = spectral((n, n), rng, beta=1.6, fmin=8, fmax=140)
    cloud = spectral((n, n), rng, beta=3.0, fmin=1, fmax=6)
    grain = spectral((n, n), rng, beta=0.0, fmax=380)
    fl = fibres(n, rng, 6500, (6, 30), (0.45, 0.9), 0.015)
    fd = fibres(n, rng, 1400, (5, 22), (0.4, 0.8), 0.02)
    base = C((216, 206, 182))
    alb = np.broadcast_to(base, (n, n, 3)).copy()
    alb *= (1 + 0.009 * form + 0.006 * cloud + 0.010 * grain)[..., None]
    alb = mix(alb, C((228, 221, 202)), fl * 0.40)
    alb = mix(alb, C((190, 176, 150)), fd * 0.30)
    inc = speckle(n, rng, 0.000012, 0.8)
    alb = mix(alb, C((150, 134, 112)), inc * 0.35)
    h = 0.5 + 0.05 * form + 0.02 * cloud + 0.035 * grain + 0.22 * fl + 0.14 * fd
    return dict(albedo=alb, height=blur(h, 0.5), nstr=1.8)


@material("paper_shoji", 512, "Japanese shoji / washi paper (light), long kozo fibres.",
          "Soft formation fBm + long, gently curved fibre strands (wrap-around strokes), mostly lighter "
          "than the sheet.")
def paper_shoji(n, rng):
    form = spectral((n, n), rng, beta=2.0, fmin=4, fmax=50)
    fine = spectral((n, n), rng, beta=0.3, fmax=200)
    fl = fibres(n, rng, 520, (40, 220), (0.5, 1.2), 0.006, ss=3)
    fd = fibres(n, rng, 60, (30, 140), (0.4, 0.8), 0.008, ss=3)
    alb = np.broadcast_to(C((222, 217, 202)), (n, n, 3)).copy()
    alb *= (1 + 0.014 * form + 0.006 * fine)[..., None]
    alb = mix(alb, C((234, 231, 220)), fl * 0.55)
    alb = mix(alb, C((200, 192, 172)), fd * 0.25)
    h = 0.5 + 0.04 * form + 0.02 * fine + 0.30 * fl + 0.15 * fd
    return dict(albedo=alb, height=blur(h, 0.6), nstr=1.6)


@material("fabric_linen", 512, "Linen cloth (tablecloths, curtains, upholstery).",
          "Plain weave: 64 warp x 64 weft threads (offset half a thread from the border) with per-thread "
          "tone, slubs that vary only along each thread, over/under height profile, fibre fuzz.")
def fabric_linen(n, rng):
    NT = 64
    T = n / NT
    yy, xx = np.mgrid[0:n, 0:n].astype(F32) + 0.5
    xx = (xx + T / 2) % n
    yy = (yy + T / 2) % n
    i = np.floor(xx / T).astype(int) % NT
    j = np.floor(yy / T).astype(int) % NT
    # along-thread variation: a 1-D noise per thread (rows for warp threads, cols for weft threads)
    slub_w = spectral((NT, n), rng, beta=1.2, fmin=2, fmax=60)      # [thread, y]
    slub_f = spectral((NT, n), rng, beta=1.2, fmin=2, fmax=60)      # [thread, x]
    yi = np.floor(yy).astype(int) % n
    xi = np.floor(xx).astype(int) % n
    sw = slub_w[i, yi]
    sf = slub_f[j, xi]
    tw_w = rng.uniform(0.72, 0.95, NT)[i] * (1 + 0.12 * sw)
    tw_f = rng.uniform(0.72, 0.95, NT)[j] * (1 + 0.12 * sf)
    lxw = (xx - (i + 0.5) * T) / (T * 0.5)
    lyf = (yy - (j + 0.5) * T) / (T * 0.5)
    lyw = (yy - (j + 0.5) * T) / T
    lxf = (xx - (i + 0.5) * T) / T
    prof_w = np.sqrt(clamp01(1 - (lxw / tw_w) ** 2))
    prof_f = np.sqrt(clamp01(1 - (lyf / tw_f) ** 2))
    over_w = ((i + j) % 2 == 0)
    und_w = 0.62 + 0.38 * np.where(over_w, 1, -1) * np.cos(np.pi * lyw)
    und_f = 0.62 + 0.38 * np.where(over_w, -1, 1) * np.cos(np.pi * lxf)
    hw = prof_w * und_w
    hf = prof_f * und_f
    top_w = hw >= hf
    fuzz_v = spectral((n, n), rng, beta=0.5, elong=(1, 8), fmax=220)
    fuzz_h = spectral((n, n), rng, beta=0.5, elong=(8, 1), fmax=220)
    tone_w = rng.normal(0, 0.022, NT)[i] + 0.035 * sw + 0.02 * fuzz_v
    tone_f = rng.normal(0, 0.022, NT)[j] + 0.035 * sf + 0.02 * fuzz_h
    cw = C((190, 176, 150))
    cf = C((180, 165, 139))
    alb = np.where(top_w[..., None], cw * (1 + tone_w)[..., None], cf * (1 + tone_f)[..., None])
    hm = np.maximum(hw, hf)
    alb = alb * (0.90 + 0.10 * hm)[..., None]
    h = hm + 0.04 * np.where(top_w, fuzz_v, fuzz_h)
    return dict(albedo=alb, height=blur(h, 0.6), nstr=1.8)


# =========================================================================== leather / metal / lacquer
@material("leather_navy", 1024, "Passport cover and glasses case: deep-navy pebbled leather.",
          "Lloyd-relaxed periodic Voronoi (~2.6k cells, warped lookup); each cell is a rounded dome "
          "from F1/(F1+edge) with fine creases at the bisectors; wrinkle noise; tops slightly lighter.")
def leather_navy(n, rng):
    cnt = 2600
    pts = relaxed_points(cnt, rng, iters=3, res=512)
    du = spectral((n, n), rng, beta=2.0, fmin=6, fmax=70) * 0.0016
    dv = spectral((n, n), rng, beta=2.0, fmin=6, fmax=70) * 0.0016
    vr = voronoi(n, pts, du, dv)
    e = vr["edge"] * n
    hj = rng.uniform(0.75, 1.0, cnt)[vr["id"]]
    tone = rng.normal(0, 1, cnt)[vr["id"]]
    t = vr["f1"] / (vr["f1"] + vr["edge"] + 1e-6)
    dome = np.sqrt(clamp01(1 - t ** 2)) ** 0.75 * smoothstep(0.0, 2.0, e)
    wrinkle = spectral((n, n), rng, beta=1.0, fmin=40, fmax=260)
    big = spectral((n, n), rng, beta=2.5, fmin=1, fmax=10)
    h = dome * hj + 0.06 * wrinkle * (1.2 - dome) + 0.03 * big
    crease = 1 - smoothstep(0.0, 1.3, e)
    alb = ramp(clamp01(0.15 + 0.85 * dome * hj), [(0, (26, 34, 60)), (0.6, (34, 45, 78)), (1.0, (42, 55, 92))])
    alb *= (1 + 0.025 * tone + 0.04 * big + 0.02 * wrinkle)[..., None]
    wear = smoothstep(0.9, 2.2, spectral((n, n), rng, beta=2.2, fmin=3, fmax=30)) * dome
    alb = mix(alb, C((58, 68, 98)), wear * 0.35)
    alb = mix(alb, C((22, 29, 50)), crease * 0.32)
    return dict(albedo=alb, height=blur(h, 0.7), nstr=2.6)


@material("metal_brushed", 512, "Aged gunmetal for glasses frames, hinges, fittings.",
          "Strongly anisotropic Fourier noise (streaks along X) + ~170 long wrap-around scratches + "
          "soft low-frequency patina and a few faint corrosion spots.")
def metal_brushed(n, rng):
    s1 = spectral((n, n), rng, beta=1.0, fmin=1, elong=(60, 1))
    s2 = spectral((n, n), rng, beta=0.2, elong=(25, 1), fmax=200)
    cv_l = SS(n, n, ss=3, wrap=True)
    cv_d = SS(n, n, ss=3, wrap=True)
    for k in range(170):
        x, y = rng.uniform(0, n, 2)
        L = rng.uniform(30, 320)
        a = rng.normal(0, 0.02)
        pts = [(x, y), (x + math.cos(a) * L, y + math.sin(a) * L)]
        (cv_l if k % 3 else cv_d).line(pts, rng.uniform(0.35, 0.9), fill=int(rng.uniform(120, 255)), round_caps=False)
    sl, sd = cv_l.cov(), cv_d.cov()
    patina = spectral((n, n), rng, beta=3.0, fmin=1, fmax=10)
    spots = smoothstep(2.3, 3.2, spectral((n, n), rng, beta=1.2, fmin=12, fmax=90))
    base = C((112, 114, 118))
    alb = np.broadcast_to(base, (n, n, 3)).copy()
    alb *= (1 + 0.06 * s1 + 0.04 * s2 + 0.10 * sl - 0.10 * sd)[..., None]
    alb = mix(alb, C((92, 90, 86)), smoothstep(-0.5, 2.0, patina) * 0.30)
    alb = mix(alb, C((82, 78, 72)), spots * 0.35)
    h = 0.5 + 0.03 * s1 + 0.05 * s2 - 0.25 * sl - 0.25 * sd - 0.15 * spots
    return dict(albedo=alb, height=blur(h, 0.4), nstr=2.0)


@material("lacquer_red", 512, "Japanese red lacquer (urushi) for trays, rails, shrine accents.",
          "Straight wood-ring field (integer ring count + periodic warp) showing faintly under a red "
          "lacquer; elongated brush noise; orange-peel band-pass noise in the height.")
def lacquer_red(n, rng):
    r = wood_rings(n, rng, (26, 52), wave_px=5.0)
    p, tone, line = ring_profile(r)
    brush = spectral((n, n), rng, beta=1.0, fmin=2, elong=(14, 1))
    peel = spectral((n, n), rng, band=(70, 25))
    cloud = spectral((n, n), rng, beta=2.5, fmin=1, fmax=8)
    alb = np.broadcast_to(C((138, 40, 34)), (n, n, 3)).copy()
    alb *= (1 - 0.03 * line - 0.015 * tone + 0.012 * brush + 0.02 * cloud)[..., None]
    h = 0.5 + 0.08 * line + 0.03 * brush + 0.02 * peel
    return dict(albedo=alb, height=blur(h, 0.8), nstr=2.5)


# =========================================================================== plaster
def _trowel(n, rng, cells=60, slope=0.012, ridge=0.02):
    pts = rng.random((cells, 2))
    du = spectral((n, n), rng, beta=2.5, fmin=1, fmax=12) * 0.02
    dv = spectral((n, n), rng, beta=2.5, fmin=1, fmax=12) * 0.02
    vr = voronoi(n, pts, du, dv)
    ang = rng.uniform(0, 2 * math.pi, cells)
    mag = rng.uniform(0.3, 1.0, cells) * slope
    gx, gy = np.cos(ang) * mag, np.sin(ang) * mag
    fac = (gx[vr["id"]] * vr["dx"] + gy[vr["id"]] * vr["dy"]) * n / 100.0
    e = vr["edge"] * n
    rid = np.exp(-(e / 2.5) ** 2) * rng.uniform(0, 1, cells)[vr["id"]] * ridge
    return blur(fac, 4.0), blur(rid, 1.0), vr


@material("plaster_warm", 1024, "Abu Dhabi apartment walls: warm off-white plaster, subtle.",
          "Voronoi trowel facets (random planar slope per cell, blurred) + faint trowel ridges + "
          "cloudy fBm + fine sand grain + sparse pits.")
def plaster_warm(n, rng):
    fac, rid, _ = _trowel(n, rng, 70, 0.010, 0.02)
    cloud = spectral((n, n), rng, beta=2.5, fmin=1, fmax=12)
    grain = spectral((n, n), rng, beta=0.3, fmax=320)
    mid = spectral((n, n), rng, beta=1.8, fmin=10, fmax=80)
    pits = speckle(n, rng, 0.00025, 0.8)
    alb = np.broadcast_to(C((210, 197, 174)), (n, n, 3)).copy()
    alb *= (1 + 0.016 * cloud + 0.010 * mid + 0.012 * grain + 2.0 * fac)[..., None]
    alb = mix(alb, C((180, 165, 142)), pits * 0.4)
    h = 0.5 + 1.2 * fac + rid + 0.012 * grain + 0.012 * mid + 0.02 * cloud - 0.22 * pits
    return dict(albedo=alb, height=blur(h, 0.6), nstr=6.0)


@material("plaster_terracotta", 1024, "Marrakech walls: ochre-red (terracotta) lime plaster.",
          "Trowel facets + broad lime-wash patches (thresholded fBm) + dirt + hairline cracks "
          "(branching random walks drawn with wrap-around) + sand grain.")
def plaster_terracotta(n, rng):
    fac, rid, _ = _trowel(n, rng, 55, 0.016, 0.035)
    mott = spectral((n, n), rng, beta=2.0, fmin=3, fmax=70)
    wash = smoothstep(0.4, 1.8, spectral((n, n), rng, beta=2.6, fmin=1, fmax=10))
    dirt = smoothstep(0.8, 2.4, spectral((n, n), rng, beta=2.2, fmin=2, fmax=24))
    grain = spectral((n, n), rng, beta=0.3, fmax=320)
    pits = speckle(n, rng, 0.0005, 0.9)
    cv = SS(n, n, ss=2, wrap=True)
    starts = [(rng.uniform(0, n), rng.uniform(0, n), rng.uniform(0, 2 * math.pi)) for _ in range(6)]
    for pts, w in crack_paths(rng, starts, steps=36, step_len=5.0, wander=0.38, branch_p=0.07, w0=1.0):
        cv.line(pts, max(0.5, w), fill=230, round_caps=False)
    cr = cv.cov()
    alb = np.broadcast_to(C((176, 100, 72)), (n, n, 3)).copy()
    alb *= (1 + 0.03 * mott + 0.018 * grain + 2.0 * fac)[..., None]
    alb = mix(alb, C((192, 124, 96)), wash * 0.40)
    alb = mix(alb, C((140, 78, 58)), dirt * 0.30)
    alb = mix(alb, C((122, 68, 50)), pits * 0.45)
    alb = mix(alb, C((112, 62, 46)), cr * 0.55)
    h = 0.5 + 1.5 * fac + rid + 0.02 * grain + 0.03 * mott + 0.03 * wash - 0.3 * pits - 0.25 * cr
    return dict(albedo=alb, height=blur(h, 0.6), nstr=6.0)


# =========================================================================== masonry
def _stone_surface(n, rng):
    a = spectral((n, n), rng, beta=0.9, fmin=8, fmax=300)
    b = spectral((n, n), rng, beta=2.0, fmin=4, fmax=60)
    return a, b


@material("limestone_blocks", 1024, "Paris Haussmann facade: cream limestone ashlar, 4 blocks across.",
          "Periodic running-bond course layout (4 x 8 blocks, offset so joints avoid the tile edge); "
          "per-block tone, pores, chipped arrises (noise-perturbed edge distance), soot streaks "
          "(vertical anisotropic noise) and thin mortar joints.")
def limestone_blocks(n, rng):
    cols, rows_n = 4, 8
    bw, bh = n / cols, n / rows_n
    rows = [dict(h=bh, widths=[bw] * cols, off=(bw / 2 if r % 2 else 0) + rng.normal(0, 3)) for r in range(rows_n)]
    L = rows_layout(n, rows, ox=37, oy=29)
    en = spectral((n, n), rng, beta=1.8, fmin=10, fmax=120)
    de = edge_dist(L) + 0.7 * en
    j = 2.3
    mortar = 1 - smoothstep(j - 0.7, j + 0.7, de)
    bevel = smoothstep(j, j + 5.0, de)
    nb = L["count"]
    tone = rng.normal(0, 0.025, nb)[L["blk"]]
    warm = rng.normal(0, 0.010, nb)[L["blk"]]
    surf, mid = _stone_surface(n, rng)
    pores = speckle(n, rng, 0.0012, 0.6)
    shells = speckle(n, rng, 0.0004, 1.2)
    streak = spectral((n, n), rng, beta=1.2, fmin=2, elong=(1, 12))
    soot = smoothstep(0.3, 1.8, streak) * (0.4 + 0.6 * smoothstep(0, 1, L["ly"] / L["bh"]))
    chip = smoothstep(0.9, 1.6, spectral((n, n), rng, beta=1.0, fmin=20, fmax=200)) * (1 - smoothstep(j + 1, j + 9, de))
    alb = np.broadcast_to(C((206, 195, 172)), (n, n, 3)).copy()
    alb[..., 0] *= 1 + warm
    alb[..., 2] *= 1 - warm
    alb *= (1 + tone + 0.025 * mid + 0.02 * surf)[..., None]
    alb = mix(alb, C((168, 160, 144)), soot * 0.30)
    alb = mix(alb, C((140, 130, 112)), pores * 0.45)
    alb = mix(alb, C((222, 214, 196)), shells * 0.4)
    alb = mix(alb, C((186, 176, 156)), chip * 0.5)
    alb = mix(alb, C((190, 184, 168)), mortar)
    h = 0.15 + 0.85 * bevel + 0.02 * surf + 0.02 * mid - 0.3 * pores - 0.25 * chip
    h = np.where(mortar > 0.5, 0.12 + 0.02 * surf, h)
    return dict(albedo=alb, height=blur(h, 0.7), nstr=5.0)


@material("sandstone_blocks", 1024, "Egypt courtyard: weathered sandstone blocks of varying length.",
          "Periodic courses with random block lengths, eroded rounded edges with dark joint shadows, "
          "faint warped cross-bedding per block, honeycomb weathering from a fine periodic Voronoi "
          "masked by fBm, sandy joints.")
def sandstone_blocks(n, rng):
    rows_n = 5
    rows = [dict(h=n / rows_n, widths=random_widths(rng, n, 190, 400), off=rng.uniform(0, n)) for _ in range(rows_n)]
    L = rows_layout(n, rows, ox=0, oy=23)
    en = spectral((n, n), rng, beta=1.6, fmin=6, fmax=90)
    de = edge_dist(L) + 2.2 * en
    j = 3.5
    joint = 1 - smoothstep(j - 1.0, j + 1.0, de)
    bevel = smoothstep(j, j + 12.0, de) ** 0.7
    nb = L["count"]
    phase = rng.uniform(0, 1, nb)[L["blk"]]
    tilt = rng.uniform(-3, 3, nb)[L["blk"]]
    tone = rng.normal(0, 0.035, nb)[L["blk"]]
    u, v = uv_grid(n, n)
    w = spectral((n, n), rng, beta=2.5, fmin=1, fmax=10)
    # bedding: integer cycles in v plus a small per-block integer tilt in u keeps it periodic
    strata = np.sin(2 * np.pi * (30 * v + np.round(tilt) * u + 0.45 * w + phase))
    pts = rng.random((5000, 2))
    vr = voronoi(n, pts)
    cellpit = 1 - smoothstep(0.0, 4.0, vr["edge"] * n)
    hz = smoothstep(1.0, 2.2, spectral((n, n), rng, beta=1.8, fmin=4, fmax=40))
    honey = (1 - cellpit) * hz * (1 - smoothstep(j + 2, j + 40, de) * 0.5)
    surf, mid = _stone_surface(n, rng)
    big = spectral((n, n), rng, beta=2.4, fmin=1, fmax=12)
    alb = ramp(clamp01(0.55 + 0.08 * strata + 0.16 * mid + 0.12 * big),
               [(0, (168, 128, 86)), (0.45, (194, 156, 108)), (0.75, (206, 172, 124)), (1, (214, 186, 140))])
    alb *= (1 + tone + 0.03 * surf)[..., None]
    alb = mix(alb, C((150, 112, 76)), honey * 0.40)
    edge_dark = smoothstep(j + 10, j, de) * (1 - joint)
    alb = mix(alb, C((150, 116, 80)), edge_dark * 0.45)
    alb = mix(alb, C((150, 124, 92)) * (1 + 0.06 * surf)[..., None], joint)
    h = 0.12 + 0.88 * bevel + 0.015 * strata + 0.03 * surf + 0.04 * mid - 0.3 * honey
    h = np.where(joint > 0.5, 0.1 + 0.04 * surf, h)
    return dict(albedo=alb, height=blur(h, 0.8), nstr=6.0)


@material("brick_london", 1024, "London brown/red brick in Flemish bond with lime mortar.",
          "Periodic Flemish bond (stretcher+header, 14 courses, 3 repeats across), per-brick colour "
          "from a weighted brown/red palette with darker burnt headers, sandy surface speckle, chipped "
          "arrises, soot fBm, recessed sandy mortar.")
def brick_london(n, rng):
    unit = n / 9.0
    rows_n = 14
    rows = []
    for r in range(rows_n):
        rows.append(dict(h=n / rows_n, widths=[2 * unit, unit] * 3, off=(1.5 * unit if r % 2 else 0.0)))
    L = rows_layout(n, rows, ox=19, oy=11)
    en = spectral((n, n), rng, beta=2.0, fmin=10, fmax=120)
    de = edge_dist(L) + 0.75 * en
    j = 4.6
    mortar = 1 - smoothstep(j - 0.8, j + 0.8, de)
    bevel = smoothstep(j, j + 4.0, de) ** 0.8
    nb = L["count"]
    pal = np.array([C(c) for c in [(150, 72, 54), (138, 64, 48), (122, 78, 58), (104, 64, 50), (164, 92, 62),
                                   (128, 84, 66), (170, 136, 96), (96, 58, 46)]])
    wts = np.array([0.20, 0.17, 0.17, 0.12, 0.12, 0.10, 0.05, 0.07])
    pick = rng.choice(len(pal), nb, p=wts / wts.sum())
    bcol = pal[pick]
    header = (L["bw"] < unit * 1.5)
    burnt = (rng.random(nb) < 0.55)[L["blk"]] & header
    surf = spectral((n, n), rng, beta=0.6, fmin=10, fmax=320)
    mid = spectral((n, n), rng, beta=2.0, fmin=4, fmax=50)
    soot = spectral((n, n), rng, beta=2.6, fmin=1, fmax=10)
    pits = speckle(n, rng, 0.0018, 0.7)
    sand_d = speckle(n, rng, 0.006, 0.5)
    sand_l = speckle(n, rng, 0.004, 0.5)
    chip = smoothstep(0.8, 1.5, spectral((n, n), rng, beta=1.0, fmin=20, fmax=200)) * (1 - smoothstep(j + 1, j + 7, de))
    alb = bcol[L["blk"]].astype(F32)
    alb *= (1 + 0.05 * mid + 0.035 * surf + rng.normal(0, 0.04, nb)[L["blk"]])[..., None]
    alb = np.where(burnt[..., None], alb * np.array([0.66, 0.60, 0.64], F32), alb)
    alb = mix(alb, C((88, 70, 62)), smoothstep(0.0, 2.0, soot) * 0.35)
    alb = mix(alb, alb * 0.75, sand_d * 0.6)
    alb = mix(alb, alb * 1.18, sand_l * 0.5)
    alb = mix(alb, C((84, 60, 50)), pits * 0.5)
    alb = mix(alb, C((168, 128, 100)), chip * 0.4)
    mcol = mix(np.broadcast_to(C((170, 162, 146)), (n, n, 3)), C((140, 132, 120)), smoothstep(-0.5, 2.0, soot) * 0.6)
    mcol = mcol * (1 + 0.05 * surf)[..., None]
    mcol = mix(mcol, mcol * 0.8, sand_d * 0.8)
    alb = mix(alb, mcol, mortar)
    h = 0.18 + 0.82 * bevel + 0.04 * surf + 0.025 * mid - 0.35 * pits - 0.3 * chip - 0.08 * sand_d
    h = np.where(mortar > 0.5, 0.10 + 0.05 * surf - 0.05 * sand_d, h)
    return dict(albedo=alb, height=blur(h, 0.7), nstr=5.0)


# =========================================================================== ground
@material("sand_ripples", 1024, "Egypt ground: wind-rippled desert sand.",
          "Asymmetric ripple profile on a phase field (integer ripple count + periodic fBm warp) with "
          "amplitude modulation, cross micro-ripples, darker heavy grains in troughs, grain speckle.")
def sand_ripples(n, rng):
    u, v = uv_grid(n, n)
    w1 = spectral((n, n), rng, beta=3.0, fmin=1, fmax=6)
    w2 = spectral((n, n), rng, beta=2.0, fmin=4, fmax=24)
    phi = 17 * v + 2 * u + 0.75 * w1 + 0.12 * w2
    s = phi - np.floor(phi)
    a = 0.7
    tri = np.where(s < a, s / a, (1 - s) / (1 - a))
    prof = 0.5 - 0.5 * np.cos(np.pi * tri)
    amp = 0.75 + 0.25 * spectral((n, n), rng, beta=2.5, fmin=1, fmax=8)
    micro_phi = 70 * u - 9 * v + 0.4 * w2
    micro = 0.5 + 0.5 * np.sin(2 * np.pi * micro_phi)
    grain = spectral((n, n), rng, beta=0.0, fmax=420)
    mid = spectral((n, n), rng, beta=2.0, fmin=3, fmax=40)
    h = amp * prof + 0.04 * micro + 0.03 * grain + 0.04 * mid
    trough = 1 - prof
    dark_g = speckle(n, rng, 0.005, 0.55) * (0.25 + 0.75 * trough)
    light_g = speckle(n, rng, 0.004, 0.55) * (0.4 + 0.6 * prof)
    alb = ramp(clamp01(0.45 + 0.55 * (prof - 0.5) * amp + 0.12 * mid),
               [(0, (172, 136, 96)), (0.5, (202, 168, 124)), (1, (218, 190, 146))])
    alb *= (1 + 0.03 * grain)[..., None]
    alb = mix(alb, C((118, 96, 76)), dark_g * 0.5)
    alb = mix(alb, C((232, 216, 188)), light_g * 0.4)
    return dict(albedo=alb, height=blur(h, 0.8), nstr=7.0)


@material("cobble_wet", 1024, "Paris riverside: wet granite setts (cobblestones).",
          "Periodic courses of setts with random widths; rounded-rectangle SDF per stone with noisy "
          "outline, domed height, per-stone grey/brown tone, granite speckle; darkened and slightly "
          "saturated for wetness; muddy joints.")
def cobble_wet(n, rng):
    rows_n = 8
    rows = [dict(h=n / rows_n, widths=random_widths(rng, n, 100, 165), off=rng.uniform(0, n)) for _ in range(rows_n)]
    L = rows_layout(n, rows, ox=0, oy=17)
    en = spectral((n, n), rng, beta=1.8, fmin=6, fmax=80)
    gap = 4.5
    sd = sd_box(L["lx"], L["ly"], L["bw"] / 2, L["bh"] / 2, L["bw"] / 2 - gap, L["bh"] / 2 - gap, 20.0) + 3.0 * en
    inside = smoothstep(0.8, -0.8, sd)
    din = np.maximum(-sd, 0)
    nb = L["count"]
    hj = rng.uniform(0.7, 1.0, nb)[L["blk"]]
    tx = rng.normal(0, 0.0015, nb)[L["blk"]]
    ty = rng.normal(0, 0.0015, nb)[L["blk"]]
    dome = smoothstep(0, 30, din) ** 0.55
    surf = spectral((n, n), rng, beta=0.8, fmin=10, fmax=300)
    pal = np.array([C(c) for c in [(108, 110, 114), (98, 100, 106), (118, 112, 104), (92, 94, 98), (126, 122, 116),
                                   (112, 104, 98)]])
    scol = pal[rng.integers(0, len(pal), nb)][L["blk"]]
    gpts = rng.random((60000, 2))
    gv = voronoi(n, gpts)
    gtone = rng.choice([-0.16, -0.05, 0.0, 0.05, 0.14], 60000, p=[0.15, 0.25, 0.3, 0.2, 0.1])[gv["id"]]
    alb = scol * (1 + 0.5 * blur(gtone, 0.5) + 0.03 * surf + rng.normal(0, 0.03, nb)[L["blk"]])[..., None]
    alb = alb * (0.92 + 0.08 * dome)[..., None]
    jcol = mix(np.broadcast_to(C((60, 56, 50)), (n, n, 3)), C((72, 72, 58)),
               smoothstep(0, 1.5, spectral((n, n), rng, beta=2, fmin=4, fmax=60)))
    alb = mix(jcol, alb, inside)
    alb = saturate(alb * 0.80, 1.12)
    hh = (0.25 + 0.75 * dome * hj + tx * (L["lx"] - L["bw"] / 2) + ty * (L["ly"] - L["bh"] / 2) + 0.03 * surf)
    h = inside * hh + (1 - inside) * (0.05 + 0.03 * surf)
    return dict(albedo=alb, height=blur(h, 0.8), nstr=7.0)


@material("asphalt_wet", 1024, "NY harbour: wet asphalt with fine aggregate and sealed cracks.",
          "Three scales of periodic Voronoi aggregate (mostly sunk in a bitumen binder), wear/oil fBm "
          "patches, smooth sealed cracks from branching random walks; darkened for wetness.")
def asphalt_wet(n, rng):
    binder = C((60, 61, 64))
    fine = spectral((n, n), rng, beta=0.2, fmax=400)
    alb = np.broadcast_to(binder, (n, n, 3)).copy() * (1 + 0.07 * fine)[..., None]
    hstone = np.zeros((n, n), F32)
    for cnt, keep, e0, e1, lo, hi, hw in [(7000, 0.30, 0.8, 2.6, (76, 76, 78), (112, 110, 106), 0.45),
                                          (30000, 0.40, 0.5, 1.6, (70, 70, 72), (104, 102, 100), 0.30),
                                          (90000, 0.45, 0.3, 1.0, (66, 66, 68), (96, 95, 94), 0.18)]:
        pts = rng.random((cnt, 2))
        vr = voronoi(n, pts)
        keep_m = (rng.random(cnt) < keep)[vr["id"]]
        st = smoothstep(e0, e1, vr["edge"] * n) * keep_m
        b = rng.uniform(0, 1, cnt)[vr["id"]]
        alb = mix(alb, ramp(b, [(0, lo), (1, hi)]), st * 0.85)
        hstone = np.maximum(hstone, st * hw * (0.7 + 0.3 * b))
    patches = spectral((n, n), rng, beta=2.5, fmin=1, fmax=8)
    oil = smoothstep(1.2, 2.4, spectral((n, n), rng, beta=2.2, fmin=3, fmax=30))
    cv = SS(n, n, ss=2, wrap=True)
    starts = [(rng.uniform(0, n), rng.uniform(0, n), rng.uniform(0, 2 * math.pi)) for _ in range(4)]
    for pts, w in crack_paths(rng, starts, steps=55, step_len=7.0, wander=0.16, branch_p=0.03, w0=7.0):
        cv.line(pts, max(3.0, w), fill=255)
    seal = blur(cv.cov(), 0.8)
    alb *= (1 + 0.05 * patches)[..., None]
    alb = mix(alb, C((50, 50, 54)), oil * 0.35)
    alb = mix(alb, C((46, 46, 49)), seal * 0.85)
    alb = alb * 0.9
    h = 0.2 + hstone + 0.03 * fine + 0.02 * patches
    h = h * (1 - seal) + seal * 0.30
    return dict(albedo=alb, height=blur(h, 0.6), nstr=4.0)


@material("concrete_panels", 1024, "Berlin / New York: large precast concrete panels with seams.",
          "2 x 2 periodic panels with chamfered recessed seams, formwork tie holes, bug-holes, drip "
          "streaks below holes and seams (vertical anisotropic noise), per-panel tone, cloudy fBm.")
def concrete_panels(n, rng):
    P = n / 2
    rows = [dict(h=P, widths=[P, P], off=0.0) for _ in range(2)]
    L = rows_layout(n, rows, ox=47, oy=31)
    de = edge_dist(L)
    j = 3.2
    seam = 1 - smoothstep(j - 0.7, j + 0.7, de)
    cham = smoothstep(j, j + 5, de)
    nb = L["count"]
    tone = rng.normal(0, 0.03, nb)[L["blk"]]
    cloud = spectral((n, n), rng, beta=2.3, fmin=2, fmax=40)
    fine = spectral((n, n), rng, beta=0.4, fmax=350)
    bug = speckle(n, rng, 0.0005, 1.0)
    hole = np.zeros((n, n), F32)
    drip = np.zeros((n, n), F32)
    for fx in (0.25, 0.75):
        for fy in (0.25, 0.75):
            dx = L["lx"] - fx * P
            dy = L["ly"] - fy * P
            r = np.sqrt(dx * dx + dy * dy)
            hole = np.maximum(hole, smoothstep(10.5, 8.0, r))
            ln = rng.uniform(60, 170)
            drip = np.maximum(drip, np.exp(-(dx / 6.0) ** 2) * smoothstep(0, 10, dy) * smoothstep(ln, 0, dy))
    streak = spectral((n, n), rng, beta=1.0, fmin=2, elong=(1, 14))
    topstain = smoothstep(80, 0, L["ly"]) * smoothstep(0.0, 1.5, streak)
    alb = np.broadcast_to(C((152, 150, 145)), (n, n, 3)).copy()
    alb *= (1 + tone + 0.03 * cloud + 0.02 * fine)[..., None]
    alb = mix(alb, C((118, 116, 112)), clamp01(drip * (0.6 + 0.4 * streak)) * 0.45)
    alb = mix(alb, C((124, 122, 118)), topstain * 0.4)
    alb = mix(alb, C((96, 94, 92)), bug * 0.6)
    alb = mix(alb, C((70, 70, 70)), hole * 0.85)
    alb = mix(alb, C((110, 108, 104)), seam)
    h = 0.2 + 0.8 * cham + 0.02 * cloud + 0.015 * fine - 0.4 * bug - 0.7 * hole
    h = np.where(seam > 0.5, 0.15, h)
    return dict(albedo=alb, height=blur(h, 0.7), nstr=6.0)


@material("granite_pavers", 1024, "Berlin plaza: grey granite pavers (512x256 px running bond).",
          "Periodic running bond (2 x 4 pavers); granite crystals from a 120k-site periodic Voronoi "
          "with a muted weighted grey/black/white/pink palette; flamed-surface noise; dark joints.")
def granite_pavers(n, rng):
    rows = [dict(h=n / 4, widths=[n / 2, n / 2], off=(n / 4 if r % 2 else 0) + rng.normal(0, 4)) for r in range(4)]
    L = rows_layout(n, rows, ox=21, oy=13)
    de = edge_dist(L) + 0.5 * spectral((n, n), rng, beta=1.8, fmin=10, fmax=120)
    j = 2.6
    joint = 1 - smoothstep(j - 0.7, j + 0.7, de)
    bevel = smoothstep(j, j + 3.5, de)
    cnt = 120000
    pts = rng.random((cnt, 2))
    du = spectral((n, n), rng, beta=2, fmin=20, fmax=200) * 0.0006
    dv = spectral((n, n), rng, beta=2, fmin=20, fmax=200) * 0.0006
    vr = voronoi(n, pts, du, dv)
    pal = np.array([C(c) for c in [(84, 84, 88), (128, 126, 124), (160, 158, 154), (148, 136, 130), (108, 108, 110)]])
    pick = rng.choice(len(pal), cnt, p=[0.16, 0.34, 0.24, 0.10, 0.16])
    alb = blur(pal[pick][vr["id"]], 0.6)
    nb = L["count"]
    alb *= (1 + rng.normal(0, 0.025, nb)[L["blk"]])[..., None]
    rough = spectral((n, n), rng, beta=0.5, fmin=20, fmax=380)
    cloud = spectral((n, n), rng, beta=2.5, fmin=1, fmax=10)
    alb *= (1 + 0.03 * cloud + 0.02 * rough)[..., None]
    alb = mix(alb, C((70, 68, 64)), joint)
    h = 0.15 + 0.85 * bevel + 0.05 * rough + 0.03 * (pick[vr["id"]] == 2)
    h = np.where(joint > 0.5, 0.1 + 0.03 * rough, h)
    return dict(albedo=alb, height=blur(h, 0.6), nstr=4.0)


@material("stone_dark_slate", 1024, "Japan street: dark slate paving slabs.",
          "Random-height courses of random-length slabs (periodic, offset from the border), slate "
          "cleavage from terraced anisotropic noise per slab orientation, blue-grey per-slab tones, "
          "dark gritty joints.")
def stone_dark_slate(n, rng):
    hs = random_widths(rng, n, 110, 190)
    rows = [dict(h=hh, widths=random_widths(rng, n, 140, 330), off=rng.uniform(0, n)) for hh in hs]
    L = rows_layout(n, rows, ox=13, oy=27)
    de = edge_dist(L) + 0.8 * spectral((n, n), rng, beta=1.8, fmin=8, fmax=100)
    j = 3.0
    joint = 1 - smoothstep(j - 0.8, j + 0.8, de)
    bevel = smoothstep(j, j + 4.5, de) ** 0.8
    nb = L["count"]
    clv = [spectral((n, n), rng, beta=1.6, fmin=2, fmax=90, elong=(1, 5), angle=a) for a in (0.3, 1.2, 2.2)]
    which = rng.integers(0, 3, nb)[L["blk"]]
    cl = np.choose(which, clv)
    terr = np.floor(cl * 2.2) / 2.2
    terr = blur(terr, 1.2) * 0.6 + cl * 0.4
    pal = np.array([C(c) for c in [(80, 84, 90), (88, 92, 98), (74, 78, 84), (92, 90, 86), (96, 88, 80)]])
    pick = rng.choice(len(pal), nb, p=[0.3, 0.25, 0.25, 0.12, 0.08])
    fine = spectral((n, n), rng, beta=0.4, fmax=360)
    alb = pal[pick][L["blk"]] * (1 + 0.045 * terr + 0.025 * fine + rng.normal(0, 0.03, nb)[L["blk"]])[..., None]
    alb = mix(alb, C((46, 48, 52)), joint)
    h = 0.15 + 0.85 * bevel + 0.06 * terr + 0.015 * fine
    h = np.where(joint > 0.5, 0.08 + 0.03 * fine, h)
    return dict(albedo=alb, height=blur(h, 0.6), nstr=5.0)


@material("plaza_stone", 1024, "Riyadh plaza: warm beige large limestone pavers.",
          "2 x 2 periodic large pavers with fine joints, per-paver tone/hue, fossil flecks, pores, "
          "cloudy mottling and light dust.")
def plaza_stone(n, rng):
    rows = [dict(h=n / 2, widths=[n / 2, n / 2], off=0.0) for _ in range(2)]
    L = rows_layout(n, rows, ox=19, oy=43)
    de = edge_dist(L) + 0.5 * spectral((n, n), rng, beta=1.5, fmin=10, fmax=120)
    j = 2.4
    joint = 1 - smoothstep(j - 0.7, j + 0.7, de)
    bevel = smoothstep(j, j + 3.0, de)
    nb = L["count"]
    tone = rng.normal(0, 0.03, nb)[L["blk"]]
    hue = rng.normal(0, 0.02, nb)[L["blk"]]
    cloud = spectral((n, n), rng, beta=2.4, fmin=2, fmax=30)
    mid = spectral((n, n), rng, beta=1.6, fmin=10, fmax=120)
    fine = spectral((n, n), rng, beta=0.3, fmax=380)
    pores = speckle(n, rng, 0.0009, 0.6)
    fleck = speckle(n, rng, 0.00025, 1.4)
    dust = smoothstep(0.5, 2.0, spectral((n, n), rng, beta=2.5, fmin=1, fmax=8))
    alb = np.broadcast_to(C((206, 184, 150)), (n, n, 3)).copy()
    alb[..., 0] *= 1 + hue
    alb[..., 2] *= 1 - hue
    alb *= (1 + tone + 0.03 * cloud + 0.015 * mid + 0.012 * fine)[..., None]
    alb = mix(alb, C((168, 146, 116)), pores * 0.45)
    alb = mix(alb, C((222, 208, 182)), fleck * 0.5)
    alb = mix(alb, C((214, 198, 170)), dust * 0.25)
    alb = mix(alb, C((176, 166, 148)), joint)
    h = 0.2 + 0.8 * bevel + 0.015 * cloud + 0.015 * mid + 0.01 * fine - 0.25 * pores
    h = np.where(joint > 0.5, 0.12, h)
    return dict(albedo=alb, height=blur(h, 0.6), nstr=4.0)


@material("grass_short", 1024, "India garden lawn: short grass seen from above.",
          "~48k tapered, slightly bent grass blades drawn in five depth layers with wrap-around (later "
          "layers occlude and are lighter), gradient-mapped greens, dry-patch and soil fBm; height from "
          "layer depth.")
def grass_short(n, rng):
    from PIL import Image, ImageDraw
    ss = 2
    col = Image.new("L", (n * ss, n * ss), 20)
    hgt = Image.new("L", (n * ss, n * ss), 0)
    dc, dh = ImageDraw.Draw(col), ImageDraw.Draw(hgt)
    layers = [(12000, 30, 80), (11000, 60, 120), (10000, 90, 160), (9000, 120, 190), (6000, 150, 215)]
    for li, (cnt, vlo, vhi) in enumerate(layers):
        for _ in range(cnt):
            x, y = rng.uniform(0, n, 2)
            a = rng.uniform(0, 2 * math.pi)
            Ln = rng.uniform(5, 18)
            w = rng.uniform(1.6, 3.4)
            c, s = math.cos(a), math.sin(a)
            bend = rng.normal(0, 0.22)
            mx, my = x + c * Ln * 0.55 - s * bend * Ln * 0.3, y + s * Ln * 0.55 + c * bend * Ln * 0.3
            tx, ty = x + c * Ln - s * bend * Ln * 0.5, y + s * Ln + c * bend * Ln * 0.5
            px, py = -s * w / 2, c * w / 2
            poly = [(x + px, y + py), (mx + px * 0.6, my + py * 0.6), (tx, ty), (mx - px * 0.6, my - py * 0.6),
                    (x - px, y - py)]
            v = int(rng.uniform(vlo, vhi))
            hv = int(40 + li * 42 + rng.uniform(0, 30))
            xs = [p[0] for p in poly]
            ys = [p[1] for p in poly]
            for ox in (-n, 0, n):
                if max(xs) + ox < 0 or min(xs) + ox > n:
                    continue
                for oy in (-n, 0, n):
                    if max(ys) + oy < 0 or min(ys) + oy > n:
                        continue
                    pp = [((px_ + ox) * ss, (py_ + oy) * ss) for px_, py_ in poly]
                    dc.polygon(pp, fill=v)
                    dh.polygon(pp, fill=hv)
    b = np.asarray(col, F32).reshape(n, ss, n, ss).mean((1, 3)) / 255
    hh = np.asarray(hgt, F32).reshape(n, ss, n, ss).mean((1, 3)) / 255
    dry = smoothstep(0.6, 2.0, spectral((n, n), rng, beta=2.4, fmin=1, fmax=10))
    soil = smoothstep(1.8, 2.8, spectral((n, n), rng, beta=2.0, fmin=3, fmax=30))
    lush = spectral((n, n), rng, beta=2.2, fmin=2, fmax=20)
    green = ramp(clamp01(b + 0.05 * lush), [(0, (44, 60, 30)), (0.3, (62, 88, 40)), (0.55, (84, 116, 54)),
                                             (0.8, (108, 138, 64)), (1.0, (130, 152, 78))])
    dryc = ramp(b, [(0, (70, 66, 40)), (0.6, (124, 126, 72)), (1.0, (156, 152, 96))])
    alb = mix(green, dryc, dry * 0.40)
    alb = mix(alb, C((84, 70, 50)), soil * (1 - smoothstep(0.3, 0.6, b)) * 0.7)
    return dict(albedo=alb, height=blur(hh, 0.6), nstr=4.0)


# =========================================================================== wood
@material("wood_dark", 1024, "Shop counter: dark walnut-like wood, straight vertical grain.",
          "Straight vertical growth-ring coordinate (integer ring count + noise varying only across the "
          "grain for irregular spacing + gentle long waves); earlywood->latewood gradient with thin dark "
          "lines; vertical pore streaks; figure fBm.")
def wood_dark(n, rng):
    r = wood_rings(n, rng, (18, 46), wave_px=7.0, fine_px=1.0)
    p, tone, line = ring_profile(r)
    pores = smoothstep(1.4, 2.6, spectral((n, n), rng, beta=0.3, fmax=300, elong=(1, 40)))
    pores2 = smoothstep(1.7, 2.9, spectral((n, n), rng, beta=0.2, fmax=420, elong=(1, 25)))
    fig = spectral((n, n), rng, beta=2.5, fmin=1, fmax=8)
    fine = spectral((n, n), rng, beta=0.6, fmax=260, elong=(1, 12))
    alb = ramp(clamp01(0.62 - 0.30 * tone - 0.25 * line + 0.10 * fig + 0.04 * fine),
               [(0, (70, 46, 33)), (0.5, (96, 64, 45)), (1.0, (122, 84, 58))])
    alb = mix(alb, C((58, 39, 29)), (pores * 0.6 + pores2 * 0.4) * 0.55)
    h = 0.5 + 0.05 * line - 0.25 * pores - 0.18 * pores2 + 0.02 * fine
    return dict(albedo=alb, height=blur(h, 0.5), nstr=3.0)


@material("wood_oak_floor", 1024, "Apartment floor: oak planks (6 across) with seams and end joints.",
          "Periodic plank layout (6 columns offset from the border, two staggered end joints per "
          "column), per-plank straight ring field with its own offset/scale, ring-porous pores, ray "
          "flecks, per-plank tone, bevelled dark seams.")
def wood_oak_floor(n, rng):
    cols = 6
    W = n / cols
    yy, xx = np.mgrid[0:n, 0:n].astype(F32) + 0.5
    xo = (xx - 23.0) % n
    ci = np.floor(xo / W).astype(int) % cols
    lx = xo - ci * W
    y0 = rng.uniform(0, n, cols)
    Lp = rng.uniform(0.38, 0.62, cols) * n
    yl = (yy - y0[ci]) % n
    second = yl >= Lp[ci]
    pid = ci * 2 + second.astype(int)
    ly = np.where(second, yl - Lp[ci], yl)
    plen = np.where(second, n - Lp[ci], Lp[ci])
    np_ = cols * 2
    off = rng.uniform(0, n, np_)[pid]
    scale = rng.uniform(0.8, 1.25, np_)[pid]
    tone = rng.normal(0, 0.05, np_)[pid]
    hue = rng.normal(0, 0.02, np_)[pid]
    r = wood_rings(n, rng, (16, 40), wave_px=6.0, fine_px=0.8, xs=lx * scale + off)
    p, tone_r, line = ring_profile(r)
    early = 1 - smoothstep(0.0, 0.35, p)
    pores = smoothstep(1.2, 2.4, spectral((n, n), rng, beta=0.3, fmax=300, elong=(1, 30))) * (0.4 + 0.6 * early)
    fleck = smoothstep(2.0, 3.0, spectral((n, n), rng, beta=0.5, fmin=30, fmax=200, elong=(1, 5)))
    fig = spectral((n, n), rng, beta=2.5, fmin=1, fmax=10)
    alb = ramp(clamp01(0.66 - 0.22 * tone_r - 0.22 * line + 0.08 * fig),
               [(0, (126, 88, 56)), (0.5, (160, 118, 78)), (1, (186, 144, 98))])
    alb[..., 0] *= 1 + hue
    alb[..., 2] *= 1 - hue
    alb *= (1 + tone)[..., None]
    alb = mix(alb, C((114, 80, 52)), pores * 0.45)
    alb = mix(alb, C((190, 154, 112)), fleck * 0.25)
    de = np.minimum(np.minimum(lx, W - lx), np.minimum(ly, plen - ly))
    seam = 1 - smoothstep(0.6, 1.6, de)
    bevel = smoothstep(0.8, 3.5, de)
    alb = mix(alb, C((70, 50, 36)), seam * 0.9)
    alb = mix(alb, alb * 0.92, (1 - bevel) * (1 - seam))
    h = 0.2 + 0.8 * bevel + 0.04 * line - 0.15 * pores
    return dict(albedo=alb, height=blur(h, 0.5), nstr=4.0)


# =========================================================================== tiles / stone
@material("tile_zellige", 1024, "Moroccan zellige: 8-point star-and-cross tiles in muted teal/ochre/ivory.",
          "Square lattice (4 x 4 per tile) of octagram SDFs (union of two squares); crosses are the "
          "complement split at the star tips; checkerboard teal/ochre stars, ivory crosses; per-piece "
          "glaze tone, edge pooling, pinholes, tilted/domed piece planes, recessed grout.")
def tile_zellige(n, rng):
    P = n / 4
    yy, xx = np.mgrid[0:n, 0:n].astype(F32) + 0.5
    gx = xx / P
    gy = yy / P
    ci = np.floor(gx).astype(int) % 4
    cj = np.floor(gy).astype(int) % 4
    lx = (gx - np.floor(gx) - 0.5) * P          # star-local (star centred in each cell)
    ly = (gy - np.floor(gy) - 0.5) * P
    a = P / (2 * math.sqrt(2))
    s1 = sd_box(lx, ly, 0, 0, a, a)
    rx = (lx + ly) / math.sqrt(2)
    ry = (ly - lx) / math.sqrt(2)
    s2 = sd_box(rx, ry, 0, 0, a, a)
    star = np.minimum(s1, s2)
    in_star = star < 0
    g = 2.6
    # crosses sit on cell corners; neighbouring crosses meet at the star tips -> split them on lx=0 / ly=0
    split = np.minimum(np.abs(lx), np.abs(ly))
    bound = np.where(in_star, np.abs(star), np.minimum(np.abs(star), split))
    grout = 1 - smoothstep(g - 0.8, g + 0.8, bound)
    xi = np.round(gx).astype(int) % 4
    yi = np.round(gy).astype(int) % 4
    pid = np.where(in_star, ci + 4 * cj, 16 + xi + 4 * yi)
    cx_l = (gx - np.round(gx)) * P                # cross-local coordinates
    cy_l = (gy - np.round(gy)) * P
    plx = np.where(in_star, lx, cx_l)
    ply = np.where(in_star, ly, cy_l)
    teal = C((62, 118, 112))
    ochre = C((184, 142, 74))
    ivory = C((220, 207, 176))
    checker = ((ci + cj) % 2 == 0)
    base = np.where(in_star[..., None], np.where(checker[..., None], teal, ochre), ivory)
    tone = rng.normal(0, 0.045, 32)[pid]
    hue = rng.normal(0, 0.008, 32)[pid]
    pool = smoothstep(12, g, bound)
    glaze = spectral((n, n), rng, beta=2.0, fmin=4, fmax=60)
    fine = spectral((n, n), rng, beta=0.4, fmax=300)
    alb = base * (1 + tone + 0.03 * glaze + 0.01 * fine)[..., None]
    alb[..., 0] *= 1 + hue
    alb[..., 2] *= 1 - hue
    alb = mix(alb, alb * 0.86, pool * 0.5)
    pin = speckle(n, rng, 0.0003, 0.7)
    alb = mix(alb, C((226, 220, 204)), pin * 0.45)
    chip = smoothstep(1.7, 2.5, spectral((n, n), rng, beta=1.0, fmin=20, fmax=200)) * smoothstep(9, g + 1, bound)
    alb = mix(alb, C((176, 110, 80)), chip * 0.6)
    alb = mix(alb, C((196, 190, 176)) * (1 + 0.04 * fine)[..., None], grout)
    tx = rng.normal(0, 0.0025, 32)[pid]
    ty = rng.normal(0, 0.0025, 32)[pid]
    tilt = tx * plx + ty * ply
    dome = smoothstep(g, g + 9, bound)
    h = 0.25 + 0.65 * dome + tilt + 0.03 * glaze - 0.2 * chip
    h = np.where(grout > 0.5, 0.12 + 0.02 * fine, h)
    return dict(albedo=alb, height=blur(h, 0.6), nstr=5.0)


@material("tile_terrazzo", 1024, "Kitchen floor/counter terrazzo: marble chips in a warm cement matrix.",
          "Two scales of shrunken periodic Voronoi cells (lightly warped) used as angular chips with a "
          "muted marble palette, matrix grain, polished (very low) relief.")
def tile_terrazzo(n, rng):
    fine = spectral((n, n), rng, beta=0.5, fmax=400)
    cloud = spectral((n, n), rng, beta=2.5, fmin=1, fmax=12)
    alb = np.broadcast_to(C((204, 197, 184)), (n, n, 3)).copy() * (1 + 0.02 * fine + 0.015 * cloud)[..., None]
    pal = np.array([C(c) for c in [(222, 218, 210), (150, 148, 144), (88, 88, 90), (158, 104, 84), (146, 152, 130),
                                   (190, 160, 110), (186, 172, 150), (120, 122, 124)]])
    chip_h = np.zeros((n, n), F32)
    for cnt, keep, thr, wts, wamp in [(1500, 0.42, 3.0, [0.30, 0.20, 0.10, 0.07, 0.05, 0.06, 0.16, 0.06], 0.0012),
                                      (9000, 0.40, 1.4, [0.32, 0.22, 0.14, 0.06, 0.04, 0.04, 0.12, 0.06], 0.0006)]:
        pts = rng.random((cnt, 2))
        du = spectral((n, n), rng, beta=2.0, fmin=8, fmax=120) * wamp
        dv = spectral((n, n), rng, beta=2.0, fmin=8, fmax=120) * wamp
        vr = voronoi(n, pts, du, dv)
        sel = rng.random(cnt) < keep
        thr_c = thr * rng.uniform(0.7, 1.6, cnt)
        e = vr["edge"] * n
        m = smoothstep(thr_c[vr["id"]] - 0.6, thr_c[vr["id"]] + 0.6, e) * sel[vr["id"]]
        m = m * (1 - chip_h)
        pick = rng.choice(len(pal), cnt, p=np.array(wts) / sum(wts))
        ccol = pal[pick][vr["id"]] * (1 + rng.normal(0, 0.04, cnt)[vr["id"]] + 0.03 * fine)[..., None]
        alb = mix(alb, ccol, m)
        chip_h = np.maximum(chip_h, m)
    pits = speckle(n, rng, 0.0003, 0.6)
    alb = mix(alb, C((150, 144, 132)), pits * 0.5 * (1 - chip_h))
    h = 0.5 + 0.02 * chip_h + 0.006 * fine - 0.2 * pits
    return dict(albedo=alb, height=blur(h, 0.5), nstr=3.0)


@material("marble_white", 1024, "Agra: white marble with soft grey veins (polished).",
          "Zero-crossings of warped, strongly anisotropic Fourier noise give long flowing veins (soft "
          "broad + thin hairlines); cloudy fBm body; near-flat polished relief.")
def marble_white(n, rng):
    wx = spectral((n, n), rng, beta=3.0, fmin=1, fmax=8) * 30
    wy = spectral((n, n), rng, beta=3.0, fmin=1, fmax=8) * 30
    f1 = warp(spectral((n, n), rng, beta=2.4, fmin=1.5, fmax=30, elong=(1, 4.0), angle=0.6), wx, wy, order=3)
    f2 = warp(spectral((n, n), rng, beta=2.2, fmin=2, fmax=40, elong=(1, 7.0), angle=0.55), wx * 0.8, wy * 0.8, order=3)
    soft = np.exp(-(f1 / 0.22) ** 2)
    thin = np.exp(-(f2 / 0.03) ** 2) * smoothstep(-0.3, 1.2, spectral((n, n), rng, beta=2.5, fmin=1, fmax=10))
    cloud = spectral((n, n), rng, beta=2.2, fmin=1, fmax=20)
    alb = np.broadcast_to(C((214, 211, 203)), (n, n, 3)).copy() * (1 + 0.016 * cloud)[..., None]
    alb = mix(alb, C((182, 176, 166)), soft * 0.40)
    alb = mix(alb, C((146, 140, 132)), thin * 0.45)
    h = 0.5 + 0.004 * cloud - 0.02 * thin
    return dict(albedo=alb, height=h, nstr=4.0)


@material("carpet_rug", 1024, "Apartment rug: muted geometric (stepped diamond) pile rug.",
          "Pattern computed on a 128 x 128 knot grid (period 32 knots, offset half a knot from the "
          "border) from Manhattan-distance diamond bands, per-knot jitter, abrash row bands, fibre fuzz, "
          "wear fBm; knot domes for height.")
def carpet_rug(n, rng):
    K = n // 128
    yy, xx = np.mgrid[0:n, 0:n]
    xx = (xx + K // 2) % n
    yy = (yy + K // 2) % n
    ki, kj = xx // K, yy // K
    a = (ki % 32) - 15.5
    b = (kj % 32) - 15.5
    d1 = np.abs(a) + np.abs(b)
    a2 = ((ki + 16) % 32) - 15.5
    b2 = ((kj + 16) % 32) - 15.5
    d2 = np.abs(a2) + np.abs(b2)
    rust = C((146, 78, 60))
    indigo = C((62, 74, 104))
    cream = C((202, 186, 156))
    mustard = C((172, 138, 74))
    brown = C((86, 64, 50))
    alb = np.broadcast_to(rust, (n, n, 3)).copy()
    sel = lambda m, c: np.where(m[..., None], c, alb)
    alb = sel((d1 >= 10) & (d1 < 12), indigo)
    alb = sel((d1 >= 7) & (d1 < 10), cream)
    alb = sel((d1 >= 5) & (d1 < 7), indigo)
    alb = sel((d1 >= 2) & (d1 < 5), mustard)
    alb = sel(d1 < 2, indigo)
    arm = ((np.abs(a) < 1) | (np.abs(b) < 1)) & (d1 >= 7) & (d1 < 10)
    alb = sel(arm, rust)
    alb = sel((d2 < 6) & (d2 >= 4), brown)
    alb = sel(d2 < 4, cream)
    alb = sel(d2 < 1.5, rust)
    hook = (d1 >= 13) & (d2 >= 6) & (((ki + kj) % 4) == 0) & (((ki - kj) % 8) == 0)
    alb = sel(hook, mustard * 0.9 + rust * 0.1)
    nk = 128
    jit = rng.normal(0, 0.03, (nk, nk))[kj, ki]
    abrash = rng.normal(0, 0.025, nk)[kj]
    fuzz = spectral((n, n), rng, beta=0.4, fmax=380)
    wear = smoothstep(0.4, 2.0, spectral((n, n), rng, beta=2.5, fmin=1, fmax=8))
    alb = blur(alb, 1.1)
    alb *= (1 + jit + abrash + 0.04 * fuzz)[..., None]
    alb = mix(alb, saturate(alb, 0.6) * 1.08, wear * 0.45)
    lx = (xx % K - K / 2 + 0.5) / (K / 2)
    ly = (yy % K - K / 2 + 0.5) / (K / 2)
    knot = clamp01(1 - 0.5 * (lx * lx + ly * ly))
    h = 0.5 * knot + 0.125 * fuzz + 0.1 * (1 - wear)
    return dict(albedo=alb, height=blur(h.astype(F32), 0.6), nstr=3.0)


# =========================================================================== special
@material("water_ripple", 1024, "Normal map for rivers, harbour water, pools (no albedo).",
          "Sum of three band-pass Fourier noises (wind-stretched) as height -> wrap-around Sobel normal.",
          kind="normal")
def water_ripple(n, rng):
    h = (0.60 * spectral((n, n), rng, band=(9, 5), elong=(1, 1.8), angle=0.35)
         + 0.30 * spectral((n, n), rng, band=(24, 10), elong=(1, 1.4), angle=0.5)
         + 0.12 * spectral((n, n), rng, band=(60, 25)))
    return dict(height=h * 0.05, nstr=60.0)


@material("glass_smudge", 512, "Grayscale smudge / fingerprint mask for glass (lenses, display cases).",
          "Fingerprint ridges from iso-lines of a whorled elliptical field (integer spiral winding, so "
          "no branch cut) placed with wrap-around displacement, smear arcs from anisotropic noise, faint "
          "haze, dust specks. 0 = clean, 1 = full smudge.", kind="mask")
def glass_smudge(n, rng):
    m = np.zeros((n, n), F32)
    for k in range(5):
        cx, cy = rng.uniform(0, n, 2)
        dx, dy = periodic_disp(n, cx, cy)
        ang = rng.uniform(0, 2 * math.pi)
        c, s = math.cos(ang), math.sin(ang)
        x2 = dx * c + dy * s
        y2 = -dx * s + dy * c
        A, B = rng.uniform(24, 32), rng.uniform(32, 42)
        r = np.sqrt((x2 / A) ** 2 + (y2 / B) ** 2)
        th = np.arctan2(y2, x2)
        wn = spectral((n, n), rng, beta=2.5, fmin=2, fmax=30)
        wind = float(rng.choice([-1, 1]))
        f = r * rng.uniform(9, 12) + wind * th / (2 * math.pi) + 0.25 * wn
        ridges = 0.5 + 0.5 * np.cos(2 * np.pi * f)
        env = smoothstep(1.05, 0.55, r) * smoothstep(-0.6, 0.6, spectral((n, n), rng, beta=2.0, fmin=8, fmax=50) + 0.4)
        m = np.maximum(m, ridges * env * rng.uniform(0.35, 0.6))
    for k in range(4):
        cx, cy = rng.uniform(0, n, 2)
        dx, dy = periodic_disp(n, cx, cy)
        R = rng.uniform(60, 160)
        rr = np.sqrt(dx * dx + dy * dy)
        band = np.exp(-((rr - R) / rng.uniform(8, 16)) ** 2) * smoothstep(0.0, 1.0, np.cos(np.arctan2(dy, dx) - rng.uniform(0, 6.28)))
        streak = clamp01(0.5 + 0.5 * spectral((n, n), rng, beta=1.0, fmin=10, fmax=120, elong=(1, 6), angle=rng.uniform(0, 3.14)))
        m = np.maximum(m, band * streak * rng.uniform(0.15, 0.3))
    haze = clamp01(0.04 + 0.03 * spectral((n, n), rng, beta=2.5, fmin=1, fmax=8))
    m = clamp01(m + haze)
    dust = speckle(n, rng, 0.0006, 0.6)
    m = np.maximum(m, dust * 0.8)
    return dict(mask=blur(m, 0.5))
