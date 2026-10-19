"""2D game graphics for The Twelfth Stamp (Game/Assets/Resources/Art).

Passport pages and cover, calibration acetate, optician notes, lens-contamination overlays, UI pieces and
the app icon live here; stamps are in tx_stamps.py and painted pieces (portraits, passenger photo,
postcards) in tx_paint.py. All original procedural artwork, no lettering (unless stated), fixed seeds.
"""
from __future__ import annotations

import json
import math
from functools import lru_cache
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from tx_core import (F32, C, SS, aa, albedo_guard, blur, clamp01, crack_paths, downsample, height_to_normal,
                     iso_lines, mix, normal_rgb, over, px_grid, rng_for, rgba, sd_box, sd_ellipse, smoothstep,
                     spectral, tile_to, to_u8, save_png)
from tx_materials import MATERIALS

GRAPHICS: dict[str, dict] = {}

# ------------------------------------------------------------------ palette (sRGB)
INK_RED = C("#7a1f1f")
SEA = C((88, 146, 130))
WARM_GREY = C((146, 134, 116))
NAVY = C((32, 42, 74))
GOLD = C((180, 146, 84))
CHARCOAL = C((40, 42, 45))
BLUE_INK = C((34, 50, 104))

PAGE_W, PAGE_H = 1024, 1400
SEAL_ORDER = ["UAE", "Egypt", "France", "USA", "Japan", "SaudiArabia", "India", "Brazil", "UnitedKingdom",
              "Morocco", "Germany"]


def graphic(name, purpose, method):
    """purpose: str or {filename: purpose}."""
    def deco(fn):
        GRAPHICS[name] = dict(fn=fn, purpose=purpose, method=method)
        return fn
    return deco


# ------------------------------------------------------------------ build / preview
def build(name, spec, out_dir: Path):
    rng = rng_for(name)
    outs = spec["fn"](rng)
    recs = []
    for fname, data in outs.items():
        path = out_dir / fname
        purpose = spec["purpose"].get(fname, "") if isinstance(spec["purpose"], dict) else spec["purpose"]
        if fname.endswith(".json"):
            path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
            recs.append(dict(file=fname, w=0, h=0, fmt="JSON layout", purpose=purpose, method=spec["method"], group=name))
            continue
        arr = data if (isinstance(data, np.ndarray) and data.dtype == np.uint8) else to_u8(np.asarray(data))
        save_png(path, arr)
        fmt = {2: "L 8-bit", 3: "RGB sRGB", 4: "RGBA sRGB"}[arr.shape[2] if arr.ndim == 3 else 2]
        rec = dict(file=fname, w=arr.shape[1], h=arr.shape[0], fmt=fmt, purpose=purpose, method=spec["method"], group=name)
        if arr.ndim == 3 and arr.shape[2] == 4:
            a = arr[..., 3]
            rec["alpha_min"] = int(a.min())
            rec["alpha_max"] = int(a.max())
            rec["transparent_frac"] = round(float((a == 0).mean()), 3)
        recs.append(rec)
    return recs


def checker(h, w, s=16):
    yy, xx = np.mgrid[0:h, 0:w]
    c = ((yy // s + xx // s) % 2).astype(F32)
    return np.stack([0.55 + 0.15 * c] * 3, -1)


def flatten_preview(arr_u8):
    a = arr_u8.astype(F32) / 255
    if a.ndim == 2:
        a = np.stack([a] * 3, -1)
    if a.shape[2] == 4:
        bg = checker(a.shape[0], a.shape[1])
        a = over(bg, a[..., :3], a[..., 3])
    return to_u8(a)


def preview(state, art_dir: Path, out_dir: Path, only=None, cell=340, cols=4):
    out_dir.mkdir(parents=True, exist_ok=True)
    files = [r for r in state["graphics"].values()
             if r["fmt"] != "JSON layout" and (only is None or r["group"] in only)]
    sheets = []
    per = cols * 3
    for s in range(0, len(files), per):
        chunk = files[s:s + per]
        rows = (len(chunk) + cols - 1) // cols
        sheet = Image.new("RGB", (cell * cols, cell * rows), (36, 36, 36))
        for i, r in enumerate(chunk):
            im = np.asarray(Image.open(art_dir / r["file"]))
            im = Image.fromarray(flatten_preview(im))
            im.thumbnail((cell - 8, cell - 8), Image.LANCZOS)
            sheet.paste(im, ((i % cols) * cell + 4, (i // cols) * cell + 4))
        fp = out_dir / f"gfx_sheet_{s // per:02d}.png"
        sheet.save(fp)
        sheets.append(fp)
    return sheets


# ------------------------------------------------------------------ shared material sources
@lru_cache(maxsize=None)
def paper_albedo():
    out = MATERIALS["paper_ivory"]["fn"](1024, rng_for("paper_ivory"))
    return albedo_guard(out["albedo"]), out["height"]


@lru_cache(maxsize=None)
def leather_maps():
    out = MATERIALS["leather_navy"]["fn"](1024, rng_for("leather_navy"))
    return albedo_guard(out["albedo"]), out["height"]


def page_paper(h, w, rng, age=0.04, ox=0, oy=0):
    alb, ht = paper_albedo()
    p = tile_to(alb, h, w, ox, oy).copy()
    hh = tile_to(ht, h, w, ox, oy)
    x, y = px_grid(h, w)
    edge = np.minimum(np.minimum(x, w - 1 - x), np.minimum(y, h - 1 - y))
    e = smoothstep(40, 0, edge)
    big = spectral((h, w), rng, beta=3.0, fmin=1, fmax=6)
    p = p * (1 - age * e - 0.012 * big)[..., None]
    p = mix(p, p * C((236, 222, 196)) / 0.9, e * age * 4)
    return clamp01(p), hh


# ------------------------------------------------------------------ guilloche / security print helpers
def weave_lines(h, w, spacing=9.0, amp=12.0, lam=150.0, lam2=420.0, width=0.7, phase=0.6):
    x, y = px_grid(h, w)
    k = 2 * math.pi / lam
    wob = 3.0 * np.sin(y * 2 * math.pi / lam2)
    f1 = y + amp * np.sin(x * k + wob)
    f2 = y - amp * np.sin(x * k + wob + phase)
    return iso_lines(f1, spacing, width), iso_lines(f2, spacing, width)


def rosette_lines(h, w, cx, cy, r_in, r_out, petals=12, spacing=7.0, amp=10.0, width=0.7):
    x, y = px_grid(h, w)
    dx, dy = x - cx, y - cy
    r = np.sqrt(dx * dx + dy * dy) + 1e-3
    th = np.arctan2(dy, dx)
    band = smoothstep(r_in - 14, r_in, r) * smoothstep(r_out + 14, r_out, r)
    f = r + amp * np.sin(petals * th)
    g = r + amp * np.sin(petals * th + math.pi)
    return iso_lines(f, spacing, width) * band, iso_lines(g, spacing, width) * band


def ribbon(h, w, y0, y1, x0, x1, strands=10, lam=64.0, env=360.0, width=0.8, ss=3):
    """Braided guilloche ribbon between y0 and y1 (returns two masks for alternating strands)."""
    ca, cb = SS(w, h, ss=ss), SS(w, h, ss=ss)
    yc, A0 = (y0 + y1) / 2, (y1 - y0) / 2
    xs = np.arange(x0, x1 + 1, 1.5)
    for i in range(strands):
        ph = 2 * math.pi * i / strands
        env_v = 0.55 + 0.45 * np.cos(2 * math.pi * (xs - x0) / env)
        ys = yc + A0 * env_v * np.sin(2 * math.pi * xs / lam + ph)
        (ca if i % 2 == 0 else cb).line(list(zip(xs, ys)), width, round_caps=False)
    return ca.cov(), cb.cov()


def frame_lines(h, w, inset=40, gap=9, ss=3):
    """Double frame lines with a fine wave between them and small corner rosettes."""
    cv = SS(w, h, ss=ss)
    wv = SS(w, h, ss=ss)
    for d in (inset, inset + gap):
        cv.line([(d, d), (w - d, d), (w - d, h - d), (d, h - d), (d, d)], 0.9, round_caps=False)
    m = inset + gap / 2
    A = gap * 0.32
    xs = np.arange(m + 12, w - m - 12, 1.0)
    ys = np.arange(m + 12, h - m - 12, 1.0)
    for ph in (0.0, math.pi):
        wv.line(list(zip(xs, m + A * np.sin(xs * 2 * math.pi / 14 + ph))), 0.6, round_caps=False)
        wv.line(list(zip(xs, h - m + A * np.sin(xs * 2 * math.pi / 14 + ph))), 0.6, round_caps=False)
        wv.line(list(zip(m + A * np.sin(ys * 2 * math.pi / 14 + ph), ys)), 0.6, round_caps=False)
        wv.line(list(zip(w - m + A * np.sin(ys * 2 * math.pi / 14 + ph), ys)), 0.6, round_caps=False)
    for (cx, cy) in ((m, m), (w - m, m), (m, h - m), (w - m, h - m)):
        for r in (11, 7):
            cv.ellipse(cx, cy, r, r, fill=None, outline=255, width=0.8)
    return cv.cov(), wv.cov()


def oval_ring(h, w, cx, cy, rx, ry, width, inset=0.0):
    x, y = px_grid(h, w)
    sd = sd_ellipse(x, y, cx, cy, rx - inset, ry - inset)
    return aa(np.abs(sd) - width / 2)


def oval_fill(h, w, cx, cy, rx, ry, soft=1.0):
    x, y = px_grid(h, w)
    return aa(sd_ellipse(x, y, cx, cy, rx, ry), soft)


# ------------------------------------------------------------------ emblem (compass rose merged with a lens)
def emblem_masks(w, h, cx, cy, R, ss=4):
    """Original emblem: lens rim with knurled bezel, compass rose whose cardinal points pierce the rim,
    glint arcs and an aperture centre. Returns dict of coverage masks + a bevel height field."""
    def P(x, y):
        return (cx + x * R, cy + y * R)

    rim = SS(w, h, ss=ss)
    rim.ellipse(cx, cy, 1.0 * R, 1.0 * R, fill=255)
    rim.ellipse(cx, cy, 0.86 * R, 0.86 * R, fill=0)
    for i in range(48):               # knurl notches cut into the rim
        a = 2 * math.pi * i / 48
        ca, sa = math.cos(a), math.sin(a)
        rim.line([P(0.915 * ca, 0.915 * sa), P(1.02 * ca, 1.02 * sa)], 0.018 * R, fill=0, round_caps=False)
    inner = SS(w, h, ss=ss)
    inner.ellipse(cx, cy, 0.80 * R, 0.80 * R, fill=None, outline=255, width=0.03 * R)
    inner.ellipse(cx, cy, 0.17 * R, 0.17 * R, fill=None, outline=255, width=0.025 * R)
    inner.ellipse(cx, cy, 0.075 * R, 0.075 * R, fill=255)
    glint = SS(w, h, ss=ss)
    for (rr, a0, a1, wd) in ((0.66, 200, 250, 0.04), (0.53, 212, 236, 0.03), (0.66, 30, 52, 0.025)):
        pts = [P(rr * math.cos(math.radians(a)), rr * math.sin(math.radians(a))) for a in np.linspace(a0, a1, 40)]
        glint.line(pts, wd * R)
    light, dark = SS(w, h, ss=ss), SS(w, h, ss=ss)
    for k in range(8):
        a = -math.pi / 2 + k * math.pi / 4
        L = 1.42 if k % 2 == 0 else 0.97
        hw = 0.16 if k % 2 == 0 else 0.11
        if k % 2:
            continue
        _kite(light, dark, P, a, L, hw)
    sl, sd_ = SS(w, h, ss=ss), SS(w, h, ss=ss)
    for k in range(1, 8, 2):
        a = -math.pi / 2 + k * math.pi / 4
        _kite(sl, sd_, P, a, 0.97, 0.11)
    dots = SS(w, h, ss=ss)
    for k in range(4):
        a = -math.pi / 4 + k * math.pi / 2
        px, py = P(1.13 * math.cos(a), 1.13 * math.sin(a))
        r = 0.045 * R
        dots.poly([(px, py - r * 1.4), (px + r, py), (px, py + r * 1.4), (px - r, py)])
    m = {k: v.cov() for k, v in dict(rim=rim, inner=inner, glint=glint, light=light, dark=dark, slight=sl,
                                     sdark=sd_, dots=dots).items()}
    # long points sit above the short ones; the rim sits between them (the points pierce it)
    short = clamp01(m["slight"] + m["sdark"])
    longp = clamp01(m["light"] + m["dark"])
    allm = clamp01(np.maximum.reduce([m["rim"], m["inner"], m["glint"], short, longp, m["dots"]]))
    # bevel height: points ridge (light half higher), rim rounded
    hgt = (0.6 * m["rim"] + 0.4 * m["inner"] + 0.5 * m["glint"] + 0.5 * m["dots"]
           + short * 0.55 + m["slight"] * 0.15 + longp * 0.8 + m["light"] * 0.2)
    m.update(short=short, longp=longp, all=allm, height=blur(hgt.astype(F32), 1.2, wrap=False))
    return m


def _kite(light_cv, dark_cv, P, a, L, hw):
    ca, sa = math.cos(a), math.sin(a)
    nx, ny = -sa, ca
    tip = P(L * ca, L * sa)
    c = P(0, 0)
    left = P(hw * nx + 0.0, hw * ny)
    right = P(-hw * nx, -hw * ny)
    light_cv.poly([c, left, tip])
    dark_cv.poly([c, tip, right])


# ------------------------------------------------------------------ insert page
def insert_layout():
    rx, ry = 92, 112
    xs4 = [158, 394, 630, 866]
    xs3 = [276, 512, 748]
    centres = [(x, 300) for x in xs4] + [(x, 565) for x in xs4] + [(x, 830) for x in xs3]
    ret = (172, 1022, 852, 1268)
    return centres, (rx, ry), ret


def _uv(x, y):
    return [round(x / PAGE_W, 5), round(1.0 - y / PAGE_H, 5)]


def page_security_print(h, w, rng, clear_masks=(), rosette_at=None):
    """Background security print shared by the passport pages. Returns float RGB to composite on paper
    as (colour, alpha) layers."""
    l1, l2 = weave_lines(h, w)
    fr, fw = frame_lines(h, w)
    layers = [(WARM_GREY, l1 * 0.16), (SEA, l2 * 0.13), (WARM_GREY, fr * 0.55), (SEA, fw * 0.45)]
    if rosette_at is not None:
        cx, cy, r0, r1 = rosette_at
        ra, rb = rosette_lines(h, w, cx, cy, r0, r1, petals=14, spacing=6.5, amp=9.0)
        layers += [(SEA, ra * 0.20), (WARM_GREY, rb * 0.18)]
    clear = np.zeros((h, w), F32)
    for cm in clear_masks:
        clear = np.maximum(clear, cm)
    return layers, clear


def composite(base, layers, clear=None, clear_amt=0.8):
    out = base.copy()
    for col, a in layers:
        if clear is not None:
            a = a * (1 - clear_amt * clear)
        out = over(out, np.broadcast_to(col, out.shape), a)
    return out


@graphic("passport_insert_page",
         {"passport_insert_page.png": "Passport insert page: 11 empty oval seal spaces + return field (RGBA, opaque).",
          "passport_insert_layout.json": "Normalised (Unity UV, origin bottom-left) oval centres/radii and return-field rect."},
         "Tiled paper_ivory + interlaced iso-line guilloche weave, braided ribbon, frame, faint emblem watermark; "
         "anti-aliased SDF ellipse borders in dark-red ink; dashed return field drawn with supersampled strokes.")
def passport_insert_page(rng):
    h, w = PAGE_H, PAGE_W
    paper, _ = page_paper(h, w, rng, ox=211, oy=97)
    centres, (rx, ry), ret = insert_layout()
    clears = [oval_fill(h, w, cx, cy, rx - 4, ry - 4, soft=10.0) for cx, cy in centres]
    x, y = px_grid(h, w)
    rx0, ry0, rx1, ry1 = ret
    clears.append(aa(sd_box(x, y, (rx0 + rx1) / 2, (ry0 + ry1) / 2, (rx1 - rx0) / 2 - 6, (ry1 - ry0) / 2 - 6), 8.0))
    layers, clear = page_security_print(h, w, rng, clears)
    ra, rb = ribbon(h, w, 104, 150, 90, w - 90, strands=12)
    rc, rd = ribbon(h, w, 968, 990, 120, w - 120, strands=8, lam=48, env=260)
    layers += [(SEA, ra * 0.55), (WARM_GREY, rb * 0.50), (SEA, rc * 0.45), (WARM_GREY, rd * 0.40)]
    em = emblem_masks(w, h, 512, 600, 250, ss=2)
    layers += [(WARM_GREY, em["all"] * 0.07)]
    page = composite(paper, layers, clear)
    # seal spaces: thin dark-red ink border + inner hairline
    ink = np.zeros((h, w), F32)
    for cx, cy in centres:
        ink = np.maximum(ink, oval_ring(h, w, cx, cy, rx, ry, 2.0))
        ink = np.maximum(ink, oval_ring(h, w, cx, cy, rx, ry, 0.8, inset=6.0) * 0.8)
    # return field: dashed border + corner brackets
    cv = SS(w, h, ss=3)
    dash, gap_ = 14.0, 8.0
    for (ax, ay, bx, by) in ((rx0, ry0, rx1, ry0), (rx1, ry0, rx1, ry1), (rx1, ry1, rx0, ry1), (rx0, ry1, rx0, ry0)):
        L = math.hypot(bx - ax, by - ay)
        ux, uy = (bx - ax) / L, (by - ay) / L
        s = 4.0
        while s < L - 4:
            e = min(s + dash, L - 4)
            cv.line([(ax + ux * s, ay + uy * s), (ax + ux * e, ay + uy * e)], 2.0, round_caps=False)
            s = e + gap_
    for (cx_, cy_, sx, sy) in ((rx0 - 10, ry0 - 10, 1, 1), (rx1 + 10, ry0 - 10, -1, 1), (rx0 - 10, ry1 + 10, 1, -1),
                               (rx1 + 10, ry1 + 10, -1, -1)):
        cv.line([(cx_ + sx * 26, cy_), (cx_, cy_), (cx_, cy_ + sy * 26)], 1.4, round_caps=False)
    ink = np.maximum(ink, cv.cov())
    tone = 1 + 0.06 * spectral((h, w), rng, beta=2.0, fmin=10, fmax=200)
    page = over(page, np.broadcast_to(INK_RED, page.shape), clamp01(ink * 0.92 * tone))
    out = rgba(albedo_guard(page, lo=0.10, hi=0.90), np.ones((h, w), F32))
    seals = []
    for name, (cx, cy) in zip(SEAL_ORDER, centres):
        seals.append(dict(id=name, center=_uv(cx, cy), radius=[round(rx / PAGE_W, 5), round(ry / PAGE_H, 5)],
                          center_px=[cx, cy], radius_px=[rx, ry]))
    layout = dict(
        texture="passport_insert_page.png", size_px=[PAGE_W, PAGE_H],
        uv_convention="Unity UV: origin bottom-left, u right, v up (v = 1 - y_px / height). *_px values use "
                      "image pixels with origin top-left.",
        seal_order=SEAL_ORDER,
        seals=seals,
        return_field=dict(id="Return",
                          center=_uv((rx0 + rx1) / 2, (ry0 + ry1) / 2),
                          rect=dict(xMin=round(rx0 / PAGE_W, 5), yMin=round(1 - ry1 / PAGE_H, 5),
                                    width=round((rx1 - rx0) / PAGE_W, 5), height=round((ry1 - ry0) / PAGE_H, 5)),
                          rect_px=dict(x0=rx0, y0=ry0, x1=rx1, y1=ry1)),
    )
    return {"passport_insert_page.png": out, "passport_insert_layout.json": layout}


# ------------------------------------------------------------------ ID page
@graphic("passport_id_page",
         {"passport_id_page.png": "Passport identity page: plain light-grey photo window + blank field lines.",
          "passport_id_layout.json": "Photo window and field-line positions (Unity UV) for in-engine compositing."},
         "Same paper/security print as the insert page; guilloche rosette with faint emblem watermark; "
         "photo window kept flat light grey for the in-engine portrait.")
def passport_id_page(rng):
    h, w = PAGE_H, PAGE_W
    paper, _ = page_paper(h, w, rng, ox=517, oy=331)
    win = (96, 236, 416, 636)
    x, y = px_grid(h, w)
    wx0, wy0, wx1, wy1 = win
    win_sd = sd_box(x, y, (wx0 + wx1) / 2, (wy0 + wy1) / 2, (wx1 - wx0) / 2, (wy1 - wy0) / 2)
    clear = [aa(win_sd - 10, 10.0)]
    layers, clr = page_security_print(h, w, rng, clear, rosette_at=(512, 1030, 110, 250))
    ra, rb = ribbon(h, w, 742, 778, 90, w - 90, strands=10, lam=56, env=300)
    rc, rd = ribbon(h, w, 1292, 1318, 90, w - 90, strands=8, lam=40, env=220)
    re_, rf = ribbon(h, w, 104, 150, 90, w - 90, strands=12)
    layers += [(SEA, re_ * 0.5), (WARM_GREY, rf * 0.45)]
    em = emblem_masks(w, h, 512, 1030, 92, ss=2)
    layers += [(SEA, ra * 0.5), (WARM_GREY, rb * 0.45), (SEA, rc * 0.4), (WARM_GREY, rd * 0.35),
               (WARM_GREY, em["all"] * 0.12)]
    page = composite(paper, layers, clr, clear_amt=1.0)
    # photo window: plain light grey, thin frame
    page = over(page, np.broadcast_to(C((206, 206, 203)), page.shape), aa(win_sd, 1.0))
    frame = aa(np.abs(win_sd + 0.5) - 0.7)
    frame2 = aa(np.abs(win_sd - 5) - 0.45)
    page = over(page, np.broadcast_to(WARM_GREY * 0.8, page.shape), frame * 0.9)
    page = over(page, np.broadcast_to(SEA, page.shape), frame2 * 0.6)
    # field lines
    cv, stub = SS(w, h, ss=3), SS(w, h, ss=3)
    fields = []
    for fy in (300, 370, 440, 510, 580):
        cv.line([(468, fy), (932, fy)], 1.0, round_caps=False)
        stub.line([(468, fy - 24), (540, fy - 24)], 0.8, round_caps=False)
        fields.append((468, fy, 932, fy))
    for (fx0, fx1) in ((468, 690), (710, 932)):
        cv.line([(fx0, 650), (fx1, 650)], 1.0, round_caps=False)
        stub.line([(fx0, 626), (fx0 + 60, 626)], 0.8, round_caps=False)
        fields.append((fx0, 650, fx1, 650))
    cv.line([(96, 700), (416, 700)], 1.0, round_caps=False)
    fields.append((96, 700, 416, 700))
    for fy in (1180, 1236):
        cv.line([(96, fy), (932, fy)], 1.0, round_caps=False)
        stub.line([(96, fy - 24), (176, fy - 24)], 0.8, round_caps=False)
        fields.append((96, fy, 932, fy))
    page = over(page, np.broadcast_to(WARM_GREY * 0.85, page.shape), cv.cov() * 0.55)
    page = over(page, np.broadcast_to(WARM_GREY, page.shape), stub.cov() * 0.30)
    page = albedo_guard(page, lo=0.10, hi=0.90)
    layout = dict(texture="passport_id_page.png", size_px=[PAGE_W, PAGE_H],
                  uv_convention="Unity UV: origin bottom-left (v = 1 - y_px / height).",
                  photo_window=dict(xMin=round(wx0 / w, 5), yMin=round(1 - wy1 / h, 5), width=round((wx1 - wx0) / w, 5),
                                    height=round((wy1 - wy0) / h, 5), rect_px=dict(x0=wx0, y0=wy0, x1=wx1, y1=wy1),
                                    aspect="4:5 (matches saeed_portrait.png 512x640)"),
                  field_lines=[dict(start=_uv(a, b), end=_uv(c, d)) for a, b, c, d in fields])
    return {"passport_id_page.png": page, "passport_id_layout.json": layout}


# ------------------------------------------------------------------ cover
@graphic("passport_cover",
         {"passport_cover.png": "Passport cover albedo: deep-navy pebbled leather with aged-gold compass-lens emblem.",
          "passport_cover_n.png": "Optional extra: cover tangent normal (leather grain + emblem deboss), +Y up.",
          "passport_cover_ms.png": "Optional extra: URP metallic (R) / smoothness (A) map, gold foil metallic."},
         "leather_navy tiled to 1024x1400; original emblem rasterised from supersampled shapes; debossed with a "
         "bevel height field, gold foil with wear noise and bevel shading; blind-debossed border line.")
def passport_cover(rng):
    h, w = PAGE_H, PAGE_W
    lalb, lht = leather_maps()
    alb = tile_to(lalb, h, w, 133, 71).copy()
    lh = tile_to(lht, h, w, 133, 71)
    x, y = px_grid(h, w)
    cx, cy, R = 512, 600, 168
    em = emblem_masks(w, h, cx, cy, R, ss=3)
    allm = em["all"]
    region = blur(allm, 3.0, wrap=False)
    border_sd = sd_box(x, y, w / 2, h / 2, w / 2 - 48, h / 2 - 48, 26)
    border = aa(np.abs(border_sd) - 1.6)
    wear = smoothstep(1.4, 2.4, spectral((h, w), rng, beta=1.3, fmin=20, fmax=300))
    wear2 = smoothstep(1.8, 2.6, spectral((h, w), rng, beta=2.0, fmin=4, fmax=40))
    foil = clamp01(allm * (1 - 0.8 * wear * (0.35 + 0.4 * em["light"] + 0.5 * em["rim"])) * (1 - 0.5 * wear2))
    lens_in = aa(np.sqrt((x - cx) ** 2 + (y - cy) ** 2) - 0.79 * R, 1.5) * (1 - allm)
    # emboss height (debossed into the leather)
    eh = em["height"]
    height = lh * (1 - 0.7 * region) * (1 - 0.8 * lens_in) - 0.35 * region - 0.2 * lens_in + 0.25 * eh - 0.25 * border
    # shading for foil: light from upper-left over the emblem bevel
    gy_, gx_ = np.gradient(blur(eh, 1.0, wrap=False))
    shade = clamp01(0.55 - 2.2 * (gx_ * 0.6 + gy_ * 0.8) + 0.30 * em["light"] - 0.22 * em["dark"]
                    + 0.18 * em["slight"] - 0.20 * em["sdark"] + 0.10 * em["rim"])
    gold = mix(np.broadcast_to(C((124, 94, 52)), (h, w, 3)), C((214, 180, 112)), shade)
    gold = gold * (1 + 0.06 * spectral((h, w), rng, beta=1.0, fmin=30, fmax=400))[..., None]
    # pressed (debossed) leather is a little darker and smoother
    alb = mix(alb, alb * 0.82, clamp01(region * 1.3))
    smooth_navy = np.broadcast_to(C((30, 40, 70)), (h, w, 3)) * (1 + 0.05 * spectral((h, w), rng, beta=2.5, fmin=2, fmax=20))[..., None]
    alb = mix(alb, smooth_navy, lens_in * 0.75)
    alb = mix(alb, C((52, 66, 104)), lens_in * np.exp(-(((x - cx) * 0.7 + (y - cy) * 0.7 + 0.25 * R) / (0.2 * R)) ** 2) * 0.35)
    alb = mix(alb, alb * 0.78, border)
    alb = mix(alb, gold, foil)
    edge = np.minimum(np.minimum(x, w - 1 - x), np.minimum(y, h - 1 - y))
    rub = smoothstep(26, 0, edge) * (0.5 + 0.5 * smoothstep(-0.5, 1.5, spectral((h, w), rng, beta=1.5, fmin=8, fmax=120)))
    alb = mix(alb, alb * 1.25 + 0.02, rub * 0.5)
    alb = albedo_guard(alb, lo=0.12, hi=0.86)
    nrm = height_to_normal(height, 3.0, wrap=False)
    met = foil * 0.85
    smooth = 0.32 + 0.38 * foil + 0.08 * region + 0.15 * lens_in
    ms = np.stack([met, np.zeros_like(met), np.zeros_like(met), smooth], -1)
    return {"passport_cover.png": alb, "passport_cover_n.png": normal_rgb(nrm), "passport_cover_ms.png": ms}


# ------------------------------------------------------------------ calibration acetate
@graphic("calibration_insert",
         "Transparent acetate overlay: registration marks + 11-point constellation aligned to the seal "
         "centres and a cancelled 12th point in the return field.",
         "Reads the same layout as passport_insert_layout.json; supersampled strokes in sea-green and dark-red, "
         "very faint acetate tint and edge; alpha-only background.")
def calibration_insert(rng):
    h, w = PAGE_H, PAGE_W
    centres, (rx, ry), ret = insert_layout()
    rx0, ry0, rx1, ry1 = ret
    x, y = px_grid(h, w)
    sea_l, sea_d, red_l = SS(w, h, ss=3), SS(w, h, ss=3), SS(w, h, ss=3)
    # registration crosshairs in the corners
    for (cx, cy) in ((64, 64), (w - 64, 64), (64, h - 64), (w - 64, h - 64)):
        red_l.ellipse(cx, cy, 16, 16, fill=None, outline=255, width=1.4)
        red_l.ellipse(cx, cy, 6, 6, fill=None, outline=255, width=1.0)
        red_l.line([(cx - 30, cy), (cx + 30, cy)], 1.2, round_caps=False)
        red_l.line([(cx, cy - 30), (cx, cy + 30)], 1.2, round_caps=False)
    # concentric arcs: around the cancelled 12th point (gap towards point 11) and quarter arcs at the corners
    tx0, ty0 = (rx0 + rx1) / 2, (ry0 + ry1) / 2
    gx0, gy0 = centres[-1]
    gap_dir = math.atan2(gy0 - ty0, gx0 - tx0)
    for r in (46, 74, 102, 130):
        a0, a1 = gap_dir + 0.35, gap_dir + 2 * math.pi - 0.35
        n_seg = max(8, int((a1 - a0) * r / 18))
        for k in range(n_seg):
            if k % 2:
                continue
            aa0 = a0 + (a1 - a0) * k / n_seg
            aa1 = a0 + (a1 - a0) * (k + 1) / n_seg
            sea_d.line([(tx0 + r * math.cos(t), ty0 + r * math.sin(t)) for t in np.linspace(aa0, aa1, 6)], 0.9,
                       round_caps=False)
    for (cx, cy, a0) in ((64, 64, 0.0), (w - 64, 64, math.pi / 2), (w - 64, h - 64, math.pi), (64, h - 64, 1.5 * math.pi)):
        for r in (40, 54, 68):
            sea_d.line([(cx + r * math.cos(t), cy + r * math.sin(t)) for t in np.linspace(a0, a0 + math.pi / 2, 24)], 0.9,
                       round_caps=False)
    # constellation through the 11 seal centres in seal order
    sea_l.line([(float(cx), float(cy)) for cx, cy in centres], 1.3, round_caps=False)
    for cx, cy in centres:
        sea_l.ellipse(cx, cy, 11, 11, fill=None, outline=255, width=1.4)
        sea_l.ellipse(cx, cy, 3.2, 3.2, fill=255)
        sea_d.line([(cx - 22, cy), (cx - 14, cy)], 1.0, round_caps=False)
        sea_d.line([(cx + 14, cy), (cx + 22, cy)], 1.0, round_caps=False)
        sea_d.line([(cx, cy - 22), (cx, cy - 14)], 1.0, round_caps=False)
        sea_d.line([(cx, cy + 14), (cx, cy + 22)], 1.0, round_caps=False)
    # 12th point: return field centre, distinct diamond marker with a cancel cross, dashed link from point 11
    tx, ty = (rx0 + rx1) / 2, (ry0 + ry1) / 2
    gx, gy = centres[-1]
    L = math.hypot(tx - gx, ty - gy)
    for s in np.arange(16, L - 26, 14):
        e = min(s + 7, L - 26)
        sea_d.line([(gx + (tx - gx) * s / L, gy + (ty - gy) * s / L), (gx + (tx - gx) * e / L, gy + (ty - gy) * e / L)],
                   1.2, round_caps=False)
    d = 20
    red_l.line([(tx, ty - d), (tx + d, ty), (tx, ty + d), (tx - d, ty), (tx, ty - d)], 2.2, round_caps=False)
    red_l.line([(tx - 30, ty - 30), (tx + 30, ty + 30)], 3.0)
    red_l.line([(tx - 30, ty + 30), (tx + 30, ty - 30)], 3.0)
    # ruler ticks on the left and bottom edges
    for yy_ in range(120, h - 100, 20):
        L_ = 16 if yy_ % 100 == 0 else 8
        red_l.line([(14, yy_), (14 + L_, yy_)], 1.0, round_caps=False)
    for xx_ in range(120, w - 100, 20):
        L_ = 16 if xx_ % 100 == 0 else 8
        red_l.line([(xx_, h - 14), (xx_, h - 14 - L_)], 1.0, round_caps=False)
    a_sl, a_sd, a_r = sea_l.cov(), sea_d.cov(), red_l.cov()
    # acetate: faint tint, faint diagonal sheen, slightly visible cut edge
    edge_sd = sd_box(x, y, w / 2, h / 2, w / 2 - 2.5, h / 2 - 2.5, 10)
    edge = aa(np.abs(edge_sd) - 0.9)
    sheet_in = aa(edge_sd, 1.0)
    sheen = smoothstep(0.0, 1.0, np.exp(-(((x * 0.6 + y * 0.8) - 900) / 160) ** 2))
    a_bg = sheet_in * (0.035 + 0.035 * sheen)
    rgb = np.broadcast_to(C((206, 226, 220)), (h, w, 3)).copy()
    A = a_bg.copy()

    def add(col, a):
        nonlocal rgb, A
        a = clamp01(a)
        out_a = a + A * (1 - a)
        rgb = (col * a[..., None] + rgb * (A * (1 - a))[..., None]) / np.maximum(out_a, 1e-6)[..., None]
        A = out_a

    add(C((150, 176, 170)), edge * 0.30)
    add(SEA * 0.9, a_sd * 0.55)
    add(SEA * 0.85, a_sl * 0.85)
    add(INK_RED * 1.1, a_r * 0.85)
    return {"calibration_insert.png": rgba(rgb, A)}


# ------------------------------------------------------------------ optician notes
def _scribble_word(rng, x0, base, length, xh):
    """Illegible cursive-like stroke: a prolate cycloid with per-'letter' heights, ascender/descender loops
    and a forward slant. Never forms real letters."""
    n_l = max(2, int(length / (xh * 0.95)))
    lw = length / n_l
    hs = []
    for i in range(n_l):
        r = rng.random()
        hs.append(xh * (2.1 if r < 0.16 else (-1.6 if r < 0.26 else rng.uniform(0.75, 1.05))))
    pts = []
    k = lw * rng.uniform(0.22, 0.30)
    for t in np.linspace(0, n_l, n_l * 14):
        i = min(int(t), n_l - 1)
        f = t - i
        yv = base - hs[i] * (1 - math.cos(2 * math.pi * f)) / 2     # negative height = descender loop
        xv = x0 + lw * t - k * math.sin(2 * math.pi * t)
        xv += 0.28 * (base - yv)      # slant
        pts.append((xv + rng.normal(0, 0.25), yv + rng.normal(0, 0.25)))
    return pts


def _draw_pressure(cv, pts, w0, w1, rng):
    n = len(pts)
    step = 4
    for i in range(0, n - 1, step):
        seg = pts[i:i + step + 1]
        t = i / max(1, n - 1)
        wd = w0 + (w1 - w0) * (0.5 + 0.5 * math.sin(t * 6 + rng.uniform(0, 1))) * rng.uniform(0.85, 1.1)
        cv.line(seg, wd)


@graphic("optician_notes",
         "Optician's notes prop: aged ivory notepaper with illegible handwritten-looking scribbles, a glasses "
         "sketch and arrows in dark-blue ink (no real words).",
         "Tiled paper + ageing/foxing; ruled lines; cursive-like scribbles from prolate cycloids with random "
         "letter heights and pen-pressure widths; hand-wobbled glasses sketch with arrows and dimension lines.")
def optician_notes(rng):
    h, w = PAGE_H, PAGE_W
    paper, _ = page_paper(h, w, rng, age=0.06, ox=389, oy=703)
    paper = paper * C((246, 236, 214)) / 0.93
    fox = np.zeros((h, w), F32)
    for _ in range(14):
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(2, 9)
        x, y = px_grid(h, w)
        fox = np.maximum(fox, np.exp(-((x - cx) ** 2 + (y - cy) ** 2) / (2 * r * r)))
    stain = smoothstep(1.0, 2.4, spectral((h, w), rng, beta=2.6, fmin=1, fmax=10))
    paper = mix(paper, C((196, 168, 128)), fox * 0.35 + stain * 0.10)
    rule, marg = SS(w, h, ss=2), SS(w, h, ss=2)
    lines_y = list(range(170, h - 90, 44))
    for ly in lines_y:
        rule.line([(40, ly), (w - 40, ly)], 0.9, round_caps=False)
    marg.line([(112, 60), (112, h - 60)], 1.0, round_caps=False)
    page = over(paper, np.broadcast_to(C((120, 140, 170)), paper.shape), rule.cov() * 0.32)
    page = over(page, np.broadcast_to(C((170, 90, 90)), page.shape), marg.cov() * 0.28)
    ink = SS(w, h, ss=3)
    sketch_box = (480, 760, 960, 1150)
    for li, ly in enumerate(lines_y):
        if ly > 1290:
            break
        in_sketch_rows = sketch_box[1] - 30 < ly < sketch_box[3] + 20
        x = 136 + (rng.uniform(0, 60) if li % 5 == 0 else rng.uniform(0, 12))
        x_end = (sketch_box[0] - 30) if in_sketch_rows else w - rng.uniform(60, 220)
        if rng.random() < 0.12 and not in_sketch_rows:
            continue
        while x < x_end - 40:
            L = rng.uniform(36, 130)
            if x + L > x_end:
                break
            pts = _scribble_word(rng, x, ly - 6, L, rng.uniform(9, 12))
            _draw_pressure(ink, pts, 1.3, 2.3, rng)
            if rng.random() < 0.3:      # 'dots' and 'crossbars' make it read as handwriting
                px_ = x + rng.uniform(0.2, 0.8) * L
                ink.ellipse(px_ + 4, ly - 30, 1.4, 1.4, fill=255)
            if rng.random() < 0.18:
                px_ = x + rng.uniform(0.1, 0.7) * L
                ink.line([(px_, ly - 18), (px_ + rng.uniform(10, 18), ly - 19)], 1.4)
            if rng.random() < 0.05:     # crossed-out word
                ink.line([(x - 2, ly - 12), (x + L + 2, ly - 10)], 1.6)
            x += L + rng.uniform(14, 26)
    # glasses sketch (front view) with wobble, drawn twice like a quick pen sketch
    gcx, gcy = 720, 930

    def wob(pts, amp=1.2):
        return [(px_ + rng.normal(0, amp * 0.4), py_ + rng.normal(0, amp * 0.4)) for px_, py_ in pts]

    def lens(cx, cy, a, b):
        pts = []
        for t in np.linspace(0, 2 * math.pi, 90):
            ct, st = math.cos(t), math.sin(t)
            r = 1.0 / ((abs(ct) ** 4 + abs(st) ** 4) ** 0.25)
            pts.append((cx + a * r * ct, cy + b * r * st))
        return pts

    for rep in range(2):
        jit = rep * 1.6
        for sgn in (-1, 1):
            ink.line(wob(lens(gcx + sgn * 98 + jit, gcy + jit * 0.5, 74, 56)), 1.8 - 0.5 * rep)
        ink.line(wob([(gcx - 26, gcy - 46), (gcx, gcy - 54), (gcx + 26, gcy - 46)]), 1.7 - 0.5 * rep)
        ink.line(wob([(gcx - 24, gcy - 22), (gcx, gcy - 34), (gcx + 24, gcy - 22)]), 1.5 - 0.4 * rep)
        ink.line(wob([(gcx - 172, gcy - 44), (gcx - 214, gcy - 52)]), 1.6 - 0.4 * rep)
        ink.line(wob([(gcx + 172, gcy - 44), (gcx + 214, gcy - 52)]), 1.6 - 0.4 * rep)
    # right-hinge notch circled + arrows
    ink.line(wob(lens(gcx + 186, gcy - 44, 26, 22)), 1.4)
    for (ax0, ay0, ax1, ay1, bend) in ((gcx + 230, gcy - 170, gcx + 196, gcy - 70, 40),
                                       (gcx - 220, gcy + 150, gcx - 120, gcy + 50, -50),
                                       (gcx + 40, gcy + 170, gcx + 90, gcy + 66, 30)):
        mx, my = (ax0 + ax1) / 2 + bend, (ay0 + ay1) / 2
        pts = [((1 - t) ** 2 * ax0 + 2 * (1 - t) * t * mx + t * t * ax1, (1 - t) ** 2 * ay0 + 2 * (1 - t) * t * my + t * t * ay1)
               for t in np.linspace(0, 1, 30)]
        ink.line(wob(pts, 0.8), 1.6)
        dx, dy = ax1 - pts[-4][0], ay1 - pts[-4][1]
        L = math.hypot(dx, dy) + 1e-6
        ux, uy = dx / L, dy / L
        for s in (1, -1):
            hx = ax1 - 16 * ux + s * 8 * -uy
            hy = ay1 - 16 * uy + s * 8 * ux
            ink.line([(hx, hy), (ax1, ay1)], 1.6)
    # dimension line under the glasses (no numbers)
    ink.line(wob([(gcx - 172, gcy + 96), (gcx + 172, gcy + 96)], 0.6), 1.2)
    for xx_ in (gcx - 172, gcx + 172, gcx):
        ink.line([(xx_, gcy + 86), (xx_, gcy + 106)], 1.2)
    a_ink = ink.cov()
    dens = clamp01(0.82 + 0.12 * spectral((h, w), rng, beta=1.8, fmin=4, fmax=60))
    page = over(page, np.broadcast_to(BLUE_INK, page.shape), a_ink * dens)
    return {"optician_notes.png": albedo_guard(page, lo=0.10, hi=0.90)}


# ------------------------------------------------------------------ lens contamination overlays
def _lens_layer(level, rng, n=1024):
    x, y = px_grid(n, n)
    c = n / 2
    r = np.sqrt((x + 0.5 - c) ** 2 + (y + 0.5 - c) ** 2) / c          # 1.0 at edge midpoints
    rc = 0.84                                                          # fully clear inside (55.4% of pixels)
    keep = smoothstep(rc, rc + 0.10, r)
    rgb = np.broadcast_to(CHARCOAL, (n, n, 3)).copy()
    A = np.zeros((n, n), F32)

    def add(col, a):
        nonlocal rgb, A
        a = clamp01(a) * keep
        out_a = a + A * (1 - a)
        col = np.broadcast_to(col, rgb.shape) if np.ndim(col) == 1 else col
        rgb = (col * a[..., None] + rgb * (A * (1 - a))[..., None]) / np.maximum(out_a, 1e-6)[..., None]
        A = out_a

    hz = spectral((n, n), rng, beta=2.5, fmin=1, fmax=12)
    haze = smoothstep(rc, 1.45, r) * (0.55 + 0.25 * hz)
    add(C((52, 55, 58)), haze * [0.40, 0.40, 0.44, 0.52][level - 1])
    if level >= 3:
        wx = spectral((n, n), rng, beta=2.8, fmin=1, fmax=10) * 40
        wy = spectral((n, n), rng, beta=2.8, fmin=1, fmax=10) * 40
        from tx_core import warp
        sm = warp(spectral((n, n), rng, beta=2.4, fmin=2, fmax=18), wx * 1.6, wy * 1.6, order=1)
        wisp = warp(spectral((n, n), rng, beta=1.8, fmin=4, fmax=30, elong=(1, 3)), wx * 2.2, wy * 2.2, order=1)
        reach = [0, 0, 1.10, 0.98][level - 1]
        creep = smoothstep(-0.6, 1.4, (r - reach) * 4.0 + sm * 0.75)
        tendr = smoothstep(0.2, 1.8, sm + 0.4 * wisp) * smoothstep(rc + 0.02, rc + 0.30, r)
        smoke = blur(clamp01(creep * (0.6 + 0.25 * sm) + tendr * 0.30), 2.5)
        add(C((46, 48, 50)), smoke * [0, 0, 0.50, 0.70][level - 1])
        add(C((70, 92, 88)), smoke * smoothstep(0.6, 1.8, sm) * 0.10)
    if level >= 2:
        count = [0, 9, 16, 30][level - 1]
        steps = [0, 18, 26, 34][level - 1]
        width = [0, 1.0, 1.4, 2.0][level - 1]
        starts = []
        for k in range(count):
            a = rng.uniform(0, 2 * math.pi)
            rr = rng.uniform(1.05, 1.40)
            sx, sy = c + rr * c * math.cos(a), c + rr * c * math.sin(a)
            sx, sy = min(max(sx, -4), n + 4), min(max(sy, -4), n + 4)
            starts.append((sx, sy, a + math.pi + rng.normal(0, 0.45)))

        def stop(px_, py_):
            return math.hypot(px_ - c, py_ - c) / c < rc + 0.05

        core, glint = SS(n, n, ss=2), SS(n, n, ss=2)
        for pts, wd in crack_paths(rng, starts, steps=steps, step_len=10.0, wander=0.11,
                                   branch_p=0.06 if level < 4 else 0.10, max_depth=2, stop=stop, w0=width):
            core.line(pts, max(0.6, wd), round_caps=False)
            glint.line([(px_ + 1.3, py_ - 1.3) for px_, py_ in pts], max(0.5, wd * 0.6), round_caps=False)
        if level == 4:          # spalled chips at the rim corners
            for k in range(8):
                a = rng.uniform(0, 2 * math.pi)
                rr = rng.uniform(1.08, 1.38)
                px0, py0 = c + rr * c * math.cos(a), c + rr * c * math.sin(a)
                sz = rng.uniform(6, 16)
                poly = [(px0 + sz * math.cos(t) * rng.uniform(0.5, 1.0), py0 + sz * math.sin(t) * rng.uniform(0.5, 1.0))
                        for t in np.linspace(0, 2 * math.pi, 7)[:-1]]
                core.poly(poly)
        a_core, a_gl = core.cov(), glint.cov()
        add(C((150, 200, 186)), a_gl * [0, 0.30, 0.35, 0.42][level - 1])
        add(C((26, 28, 30)), a_core * [0, 0.70, 0.80, 0.90][level - 1])
    # guarantee an exactly transparent centre
    A = np.where(r < rc, 0.0, A)
    return rgba(rgb, A)


@graphic("lens_contam",
         {f"lens_contam_{i}.png": f"Lens-contamination overlay stage {i} of 4 (centre 55%+ fully transparent)."
          for i in range(1, 5)},
         "Radial masks with a hard-zero centre (r < 0.42 of width); fBm edge haze, domain-warped smoke creeping "
         "inward, branching random-walk rim fractures with offset sea-green glints, spalled chips at stage 4.")
def lens_contam(rng):
    return {f"lens_contam_{i}.png": _lens_layer(i, rng) for i in range(1, 5)}


# ------------------------------------------------------------------ UI
@graphic("ui_vignette", "Soft radial screen vignette (RGBA, near-black, alpha only at the edges).",
         "Smooth radial alpha falloff with blue-noise-like dithering to avoid 8-bit banding.")
def ui_vignette(rng):
    n = 512
    x, y = px_grid(n, n)
    u = (x + 0.5) / n * 2 - 1
    v = (y + 0.5) / n * 2 - 1
    r = np.sqrt(u * u + v * v)
    a = smoothstep(0.45, 1.42, r) ** 1.35 * 0.88
    a = a + (rng.random((n, n)) - 0.5) / 255.0
    rgb = np.broadcast_to(C((10, 11, 14)), (n, n, 3))
    return {"ui_vignette.png": rgba(rgb, clamp01(a))}


@graphic("ui_panel_paper", "Menu panel card: ivory paper with soft deckled edge (alpha).",
         "Tiled paper_ivory, gentle edge ageing; alpha from a rounded rectangle whose outline is perturbed by "
         "fibrous noise, with a translucent fibre fringe.")
def ui_panel_paper(rng):
    h, w = 640, 1024
    paper, _ = page_paper(h, w, rng, age=0.0, ox=57, oy=401)
    x, y = px_grid(h, w)
    sd = sd_box(x, y, w / 2, h / 2, w / 2 - 22, h / 2 - 22, 18)
    n1 = spectral((h, w), rng, beta=1.0, fmin=30, fmax=400)
    n2 = spectral((h, w), rng, beta=2.2, fmin=4, fmax=40)
    d = sd + 3.2 * n1 + 4.0 * n2
    core = aa(d + 2.5, 1.2)
    fringe = aa(d - 2.0, 2.5) * (0.55 + 0.25 * n1)
    alpha = clamp01(np.maximum(core, fringe))
    edge = smoothstep(30, 0, -d)
    paper = mix(paper, paper * C((228, 212, 184)) / 0.88, edge * 0.35)
    paper = mix(paper, paper * 0.92, smoothstep(10, 0, -d) * 0.5)
    return {"ui_panel_paper.png": rgba(albedo_guard(paper, lo=0.1, hi=0.9), alpha)}


# ------------------------------------------------------------------ icon
@graphic("icon_app", "Game icon: deep-navy passport corner with one dark-red oval stamp around a smoke-grey lens.",
         "Leather-textured navy cover with rounded corner, ivory page inset, bold double-ring stamp with slight "
         "ink texture, glass lens with sea-green rim reflection and specular glint; designed to read at 32 px.")
def icon_app(rng):
    n = 1024
    x, y = px_grid(n, n)
    bg = mix(np.broadcast_to(C((44, 48, 54)), (n, n, 3)), C((24, 26, 30)),
             smoothstep(200, 900, np.sqrt((x - 300) ** 2 + (y - 300) ** 2)))
    lalb, lht = leather_maps()
    lea = blur(tile_to(lalb, n, n, 300, 200), 1.4, wrap=False)
    cover_sd = sd_box(x, y, 330, 330, 642, 642, 130)            # extends off the top-left, corner at (972, 972)
    cover = aa(cover_sd, 1.2)
    bevel = smoothstep(-26, 0, cover_sd)
    lea_sh = lea * (1.08 - 0.25 * bevel)[..., None]
    img = over(bg, lea_sh * 1.15, cover)
    shadow = blur(aa(sd_box(x, y, 284, 284, 560, 560, 76), 1.0), 12.0, wrap=False)
    img = mix(img, img * 0.72, shadow * cover * 0.6)
    page_sd = sd_box(x, y, 270, 270, 560, 560, 76)                # corner at (830, 830): ~140 px of navy shows
    paper, _ = page_paper(n, n, rng, age=0.0, ox=33, oy=77)
    l1, l2 = weave_lines(n, n, spacing=14, amp=16, lam=200, width=1.2)
    paper = over(paper, np.broadcast_to(SEA, paper.shape), l2 * 0.10)
    paper = over(paper, np.broadcast_to(WARM_GREY, paper.shape), l1 * 0.10)
    img = over(img, paper, aa(page_sd, 1.2))
    # stamp
    ang = math.radians(-12)
    cx, cy = 408, 408
    ca, sa = math.cos(ang), math.sin(ang)
    X = (x - cx) * ca + (y - cy) * sa
    Y = -(x - cx) * sa + (y - cy) * ca
    sd_o = sd_ellipse(X, Y, 0, 0, 252, 300)
    ring_o = aa(np.abs(sd_o + 20) - 16)
    ring_i = aa(np.abs(sd_o + 76) - 6)
    dots = np.zeros((n, n), F32)
    for k in range(30):
        t = 2 * math.pi * k / 30
        dx, dy = 197 * math.cos(t), 245 * math.sin(t)
        dots = np.maximum(dots, aa(np.sqrt((X - dx) ** 2 + (Y - dy) ** 2) - 7))
    stamp = clamp01(np.maximum.reduce([ring_o, ring_i, dots * 0.9]))
    tex = clamp01(0.86 + 0.14 * spectral((n, n), rng, beta=1.0, fmin=30, fmax=300))
    voids = smoothstep(1.5, 2.4, spectral((n, n), rng, beta=0.5, fmin=60, fmax=500))
    img = over(img, np.broadcast_to(C((140, 34, 34)), img.shape), stamp * tex * (1 - 0.6 * voids) * 0.95)
    # lens
    lr = 140
    dl = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
    lens = aa(dl - lr, 1.2)
    rim = aa(np.abs(dl - lr - 6) - 9)
    glass = mix(np.broadcast_to(C((122, 128, 132)), (n, n, 3)), C((62, 66, 70)), smoothstep(-lr, lr, (x - cx) * 0.6 + (y - cy) * 0.8))
    ang_p = np.arctan2(y - cy, x - cx)
    sea_rim = smoothstep(lr - 34, lr - 4, dl) * lens * smoothstep(0.2, 0.9, np.cos(ang_p + 2.3))
    glass = mix(glass, C((120, 186, 166)), sea_rim * 0.75)
    hx, hy = cx - 60, cy - 66
    spec = aa(sd_ellipse(x, y, hx, hy, 46, 22) , 6.0) * smoothstep(-1, 1, -(x - hx) * 0.3 - (y - hy) * 0.2 + 10)
    glass = mix(glass, C((236, 240, 238)), spec * 0.65)
    glass = mix(glass, C((220, 228, 226)), aa(np.sqrt((x - (cx + 74)) ** 2 + (y - (cy + 78)) ** 2) - 9, 2.0) * 0.5)
    img = over(img, glass, lens)
    img = over(img, np.broadcast_to(C((78, 80, 84)), img.shape), rim)
    img = over(img, np.broadcast_to(C((150, 152, 156)), img.shape),
               aa(np.abs(dl - lr - 1) - 1.5) * smoothstep(0.0, 1.0, -np.sin(ang_p + 0.8)))
    return {"icon_app.png": albedo_guard(img, lo=0.06, hi=0.95)}


# register the other graphic modules (they add to GRAPHICS on import)
import tx_stamps  # noqa: E402,F401
import tx_paint  # noqa: E402,F401
