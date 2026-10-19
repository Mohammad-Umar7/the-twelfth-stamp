"""Painted 2D pieces: Saeed's passport portrait (+ drained variant), the passenger ID-photo ovals and
four travel postcards. Original stylised illustrations built from simple shapes, gradients, soft
shading bands and procedural brush/grain texture. No text, no real people, no real logos.
"""
from __future__ import annotations

import math

import numpy as np
from PIL import Image
from scipy import ndimage

from tx_core import (F32, C, SS, aa, bezier, blur, clamp01, crack_paths, mix, over, px_grid, ramp, rgba, saturate,
                     sd_ellipse, smoothstep, spectral, warp)
from tx_graphics import CHARCOAL, graphic


# ------------------------------------------------------------------ painting helpers
def vgrad(h, w, stops):
    t = (np.arange(h, dtype=F32) + 0.5) / h
    col = ramp(t, stops)
    return np.broadcast_to(col[:, None, :], (h, w, 3)).copy()


def mask_poly(w, h, pts, ss=3):
    cv = SS(w, h, ss=ss)
    cv.poly(pts)
    return cv.cov()


def mask_polys(w, h, polys, ss=3):
    cv = SS(w, h, ss=ss)
    for p in polys:
        cv.poly(p)
    return cv.cov()


def mask_lines(w, h, lines, ss=3):
    cv = SS(w, h, ss=ss)
    for pts, wd in lines:
        cv.line(pts, wd)
    return cv.cov()


def glow(h, w, cx, cy, r):
    x, y = px_grid(h, w)
    return np.exp(-((x - cx) ** 2 + (y - cy) ** 2) / (r * r)).astype(F32)


def brush_tex(h, w, rng, angle=0.0, elong=7.0, fmin=12, fmax=220):
    """Anisotropic 'brush stroke' noise; strokes run along `angle` (0 = horizontal)."""
    return spectral((h, w), rng, beta=1.0, fmin=fmin, fmax=fmax, elong=(elong, 1), angle=angle)


def grain(h, w, rng, amt=0.018):
    return 1 + amt * spectral((h, w), rng, beta=0.0, fmax=0.45 * max(h, w))


def vignette(h, w, k=0.25):
    x, y = px_grid(h, w)
    u = (x + 0.5) / w * 2 - 1
    v = (y + 0.5) / h * 2 - 1
    return 1 - k * smoothstep(0.4, 1.5, np.sqrt(u * u + v * v))


def postcard_border(img, rng, b=16):
    h, w = img.shape[:2]
    x, y = px_grid(h, w)
    d = -np.minimum(np.minimum(x - b, w - 1 - b - x), np.minimum(y - b, h - 1 - b - y))
    inner = aa(d, 1.0)
    paper = np.broadcast_to(C((232, 224, 204)), img.shape) * grain(h, w, rng, 0.02)[..., None]
    out = mix(paper, img, inner)
    return mix(out, out * 0.8, aa(np.abs(d) - 0.6) * 0.5)


def catmull(pts, n=10, closed=True):
    from tx_stamps import catmull as _c
    return _c(pts, n, closed)


def leaf_poly(spine, width):
    """Polygon around a polyline spine with a sin-profile width (palm fronds, flames...)."""
    pts = np.array(spine, F32)
    n = len(pts)
    d = np.gradient(pts, axis=0)
    d /= (np.linalg.norm(d, axis=1, keepdims=True) + 1e-6)
    nrm = np.stack([-d[:, 1], d[:, 0]], 1)
    t = np.linspace(0, 1, n)
    wv = width * np.sin(np.pi * np.clip(t, 0, 1)) ** 0.7
    left = pts + nrm * wv[:, None]
    right = pts - nrm * wv[:, None]
    return [tuple(p) for p in left] + [tuple(p) for p in right[::-1]]


# ------------------------------------------------------------------ portrait geometry (512 x 640)
PW, PH = 512, 640
FCX, FCY, FRX, FRY = 256.0, 288.0, 110.0, 146.0


def face_outline(cx=FCX, cy=FCY, rx=FRX, ry=FRY, n=160, grow=0.0):
    pts = []
    for t in np.linspace(0, 2 * math.pi, n, endpoint=False):
        s = math.sin(t)
        k = 1 - 0.17 * max(0.0, s) ** 1.6          # narrower jaw / chin
        pts.append((cx + (rx + grow) * math.cos(t) * k, cy + (ry + grow) * s))
    return pts


def hairline_y(x):
    dx = x - FCX
    return 190 + 0.0036 * dx * dx + 8 * np.exp(-((np.abs(dx) - 66) / 18) ** 2)


def cheek_line_y(x):
    xs = [130, 150, 170, 192, 212, 228, 240, 256, 272, 284, 300, 320, 342, 362, 382]
    ys = [306, 318, 350, 374, 388, 389, 376, 373, 376, 389, 388, 374, 350, 318, 306]
    return np.interp(x, xs, ys)


def portrait_masks(rng):
    h, w = PH, PW
    x, y = px_grid(h, w)
    m = {}
    neckline = [(256 + 70 * math.cos(t), 474 + 24 * math.sin(t)) for t in np.linspace(math.pi, 0, 48)]
    garment = [(0, 640), (0, 584)] + bezier((0, 584), (40, 526), (130, 494), (186, 474)) + neckline + \
        bezier((326, 474), (382, 494), (472, 526), (512, 584)) + [(512, 640)]
    m["garment"] = mask_poly(w, h, garment)
    m["neck"] = mask_poly(w, h, [(206, 376), (306, 376), (318, 486), (194, 486)])
    ex = FRX + 7
    m["ears"] = mask_polys(w, h, [face_outline(FCX - ex, 302, 17, 31, 48), face_outline(FCX + ex, 302, 17, 31, 48)])
    m["ear_in"] = mask_polys(w, h, [face_outline(FCX - ex + 3, 304, 8, 18, 32), face_outline(FCX + ex - 3, 304, 8, 18, 32)])
    m["face"] = mask_poly(w, h, face_outline())
    nz = spectral((h, w), rng, beta=1.2, fmin=20, fmax=200)
    cap = aa(sd_ellipse(x, y, FCX, FCY - 16, FRX + 11, FRY + 24) + 0.9 * nz, 1.3)
    above = smoothstep(2.0, -2.0, y - hairline_y(x) + 1.2 * nz)
    side = (1 - m["face"]) * smoothstep(286, 262, y)
    hair = cap * clamp01(np.maximum(above, side))
    fsd = sd_ellipse(x, y, FCX, FCY, FRX, FRY)                       # <0 inside the face
    width = np.interp(y, [226, 260, 300, 330], [15, 12, 9, 6])
    sb = smoothstep(-width - 1.5, -width + 1.5, fsd) * smoothstep(1.0, -1.0, fsd)
    sb = sb * smoothstep(222, 232, y) * smoothstep(334, 322, y) * (np.abs(x - FCX) > 70)
    m["hair"] = clamp01(np.maximum(hair, blur(sb.astype(F32), 0.8, wrap=False) * m["face"]))
    jaw = mask_poly(w, h, face_outline(grow=3.0))
    below = smoothstep(-2.0, 2.0, y - cheek_line_y(x) + 1.0 * nz)
    mouth = aa(sd_ellipse(x, y, 256, 396, 22, 8.0), 1.5)
    m["beard"] = clamp01(jaw * below * (1 - mouth))
    m["mouth"] = mouth
    col = SS(w, h, ss=3)
    arc = [(256 + 66 * math.cos(t), 472 + 22 * math.sin(t)) for t in np.linspace(0.03 * math.pi, 0.97 * math.pi, 60)]
    col.line(arc, 9)
    m["collar"] = col.cov()
    return m


def _lambert_head(h, w):
    x, y = px_grid(h, w)
    nx = (x - FCX) / (FRX * 1.05)
    ny = (y - FCY) / (FRY * 1.1)
    nz = np.sqrt(clamp01(1 - nx * nx - ny * ny))
    L = np.array([-0.42, -0.30, 0.86])
    L /= np.linalg.norm(L)
    return clamp01(nx * L[0] + ny * L[1] + nz * L[2])


def soft_bands(s, levels=4, soft=0.35):
    q = s * levels
    f = q - np.floor(q)
    return (np.floor(q) + smoothstep(0.5 - soft / 2, 0.5 + soft / 2, f)) / levels


def paint_portrait(rng):
    h, w = PH, PW
    x, y = px_grid(h, w)
    m = portrait_masks(rng)
    flat = lambda c: np.broadcast_to(C(c), (h, w, 3))
    img = vgrad(h, w, [(0, (228, 231, 231)), (1, (213, 217, 219))])
    img = mix(img, img * 1.025, glow(h, w, 256, 240, 270))
    head_all = clamp01(np.maximum.reduce([m["face"], m["hair"], m["ears"], m["neck"]]))
    sh = np.roll(np.roll(blur(head_all, 16, wrap=False), 12, axis=1), 6, axis=0)
    img = mix(img, img * 0.93, sh * 0.6)
    # garment
    gsh = 1 - 0.12 * ((x - 256) / 256) ** 2 - 0.05 * smoothstep(490, 640, y)
    fold = mask_lines(w, h, [(bezier((140, 530), (196, 586), (220, 640)), 14), (bezier((372, 530), (318, 586), (292, 640)), 14)])
    gar = flat((236, 232, 222)) * gsh[..., None]
    gar = mix(gar, gar * 0.91, blur(fold, 7, wrap=False))
    img = over(img, gar, m["garment"])
    skin_shadow, skin_mid = C((146, 100, 78)), C((186, 136, 104))
    lam = soft_bands(_lambert_head(h, w), 4, 0.55)
    skin = ramp(lam, [(0, (142, 98, 76)), (0.45, (174, 126, 96)), (0.7, (190, 140, 108)), (1.0, (206, 158, 124))])
    neck = mix(np.broadcast_to(skin_mid * 0.95, (h, w, 3)), skin_shadow * 0.88, smoothstep(456, 386, y) * 0.85)
    img = over(img, neck, m["neck"])
    img = mix(img, img * 0.9, np.roll(blur(m["collar"], 3, wrap=False), 3, axis=0) * 0.35 * m["garment"])
    col_c = mix(flat((242, 239, 231)), C((226, 222, 212)), smoothstep(474, 496, y))
    img = over(img, col_c, m["collar"])
    seam = mask_lines(w, h, [([(256 + 66 * math.cos(t), 472 + 22 * math.sin(t) + 3.5) for t in np.linspace(0.08 * math.pi, 0.92 * math.pi, 50)], 0.9)])
    img = mix(img, img * 0.9, seam * m["collar"] * 0.6)
    cord = mask_lines(w, h, [(bezier((256, 498), (252, 540), (259, 578)), 3.0)])
    tassel = mask_poly(w, h, catmull([(259, 574), (264, 586), (259, 602), (254, 586)], 8))
    img = over(img, flat((204, 200, 190)), clamp01(cord + tassel))
    img = over(img, flat((198, 194, 184)), mask_lines(w, h, [([(256, 498), (256, 640)], 1.3)]) * 0.55)
    # ears + face
    img = over(img, mix(np.broadcast_to(skin_mid * 0.92, (h, w, 3)), skin_shadow, m["ear_in"] * 0.7), m["ears"])
    img = over(img, skin, m["face"])
    fm = m["face"]
    img = mix(img, img * C((255, 222, 212)) / 0.97, (glow(h, w, 204, 330, 26) + glow(h, w, 308, 330, 26)) * fm * 0.22)
    img = mix(img, img * 0.88, glow(h, w, 256, 360, 15) * fm * 0.5)
    img = mix(img, img * 0.87, (glow(h, w, 212, 272, 22) + glow(h, w, 300, 272, 22)) * fm * 0.35)
    img = mix(img, img * 0.85, smoothstep(14, 0, y - hairline_y(x)) * smoothstep(-4, 0, y - hairline_y(x)) * fm * 0.35)
    # nose
    nose_sh = mask_lines(w, h, [(bezier((268, 296), (276, 326), (272, 346)), 7)])
    img = mix(img, img * 0.85, blur(nose_sh, 4, wrap=False) * 0.7)
    img = mix(img, img * 1.08, blur(mask_lines(w, h, [([(250, 292), (251, 336)], 5)]), 4, wrap=False) * 0.6)
    img = mix(img, img * 1.06, glow(h, w, 254, 342, 9) * 0.6)
    nostrils = clamp01(aa(sd_ellipse(x, y, 245, 351, 5.5, 3.0), 1.2) + aa(sd_ellipse(x, y, 267, 351, 5.5, 3.0), 1.2))
    img = mix(img, C((96, 62, 50)), nostrils * 0.75)
    ala = mask_lines(w, h, [(bezier((238, 340), (233, 351), (244, 357)), 2.0), (bezier((274, 340), (279, 351), (268, 357)), 2.0)])
    img = mix(img, img * 0.75, blur(ala, 1.2, wrap=False) * 0.7)
    # eyes and brows
    for ex, sgn in ((FCX - 44, -1), (FCX + 44, 1)):
        ey = 282
        inner_x, outer_x = ex - 18 * sgn, ex + 19 * sgn
        upper = bezier((inner_x, ey + 2), (ex - 2 * sgn, ey - 11), (outer_x, ey))
        lower = bezier((outer_x, ey), (ex, ey + 10), (inner_x, ey + 2))
        eye = mask_poly(w, h, upper + lower)
        img = over(img, flat((232, 226, 216)), eye)
        r_ = np.sqrt((x - ex - 0.5) ** 2 + (y - ey - 0.5) ** 2)
        img = over(img, flat((70, 46, 32)), aa(r_ - 7.6, 1.0) * eye)
        img = over(img, flat((40, 26, 20)), aa(np.abs(r_ - 7) - 0.8) * eye)
        img = over(img, flat((22, 16, 14)), aa(r_ - 3.2, 1.0) * eye)
        img = over(img, flat((246, 244, 238)), aa(np.sqrt((x - ex + 2.5) ** 2 + (y - ey + 2.5) ** 2) - 1.6, 1.0))
        img = over(img, flat((44, 32, 28)), mask_lines(w, h, [(upper, 2.6)]))
        img = over(img, flat((120, 84, 66)), mask_lines(w, h, [(lower, 1.2)]) * 0.45)
        crease = bezier((inner_x + 2 * sgn, ey - 6), (ex, ey - 18), (outer_x - 1 * sgn, ey - 7))
        img = over(img, flat((132, 92, 72)), mask_lines(w, h, [(crease, 1.4)]) * 0.45)
        brow = catmull([(ex - 22 * sgn, ey - 18), (ex - 4 * sgn, ey - 28), (ex + 23 * sgn, ey - 24), (ex + 21 * sgn, ey - 20),
                        (ex - 2 * sgn, ey - 21), (ex - 20 * sgn, ey - 11)], 8)
        img = over(img, flat((40, 33, 30)), blur(mask_poly(w, h, brow), 0.7, wrap=False))
    # mouth
    upper_lip = bezier((233, 393), (245, 386), (256, 390)) + bezier((256, 390), (267, 386), (279, 393)) + bezier((279, 393), (256, 396), (233, 393))
    lower_lip = bezier((235, 394), (256, 407), (277, 394)) + bezier((277, 394), (256, 397), (235, 394))
    img = over(img, flat((124, 80, 70)), mask_poly(w, h, upper_lip))
    img = over(img, flat((150, 98, 86)), mask_poly(w, h, lower_lip))
    img = over(img, flat((84, 52, 46)), mask_lines(w, h, [(bezier((234, 393), (256, 395), (278, 393)), 1.5)]))
    # neat short beard: trimmed edge, stubble texture, skin slightly visible
    stub = clamp01(0.5 + 0.5 * spectral((h, w), rng, beta=0.4, fmax=300))
    bmask = blur(m["beard"], 1.0, wrap=False)
    shadow5 = smoothstep(-12, 0, y - cheek_line_y(x)) * (1 - m["beard"]) * fm
    img = mix(img, img * 0.86, shadow5 * 0.35)
    beard_col = mix(flat((40, 34, 32)), C((62, 52, 46)), stub * 0.55)
    img = over(img, beard_col, bmask * (0.80 + 0.12 * stub))
    img = mix(img, img * 0.80, glow(h, w, 256, 432, 30) * m["beard"] * 0.3)
    # hair
    hair_tex = brush_tex(h, w, rng, angle=math.pi / 2, elong=2.5, fmin=40, fmax=300)
    hair_col = mix(flat((34, 30, 30)), C((72, 68, 70)), clamp01(0.2 + 0.3 * hair_tex) * smoothstep(250, 150, y) * smoothstep(330, 170, x))
    img = over(img, hair_col, blur(m["hair"], 0.8, wrap=False))
    # painterly finish (subtle, isotropic on the background)
    tex = brush_tex(h, w, rng, angle=0.7, elong=1.6, fmin=20, fmax=160)
    body = clamp01(np.maximum(head_all, m["garment"]))
    img = img * (1 + 0.010 * tex * body)[..., None] * grain(h, w, rng, 0.008)[..., None]
    return clamp01(img), m


@graphic("saeed_portrait",
         {"saeed_portrait.png": "Passport photo of Saeed (original stylised illustration, plain light background).",
          "saeed_portrait_drained.png": "Vitality indicator: same portrait desaturated, cooler, with charcoal cracks "
                                        "creeping in from the edges."},
         "Layered vector shapes (supersampled), banded Lambert shading on an ellipsoid head, soft glows for "
         "form, procedural stubble/hair texture and subtle brush grain. Drained variant: desaturate + cool grade "
         "+ branching random-walk cracks from the border with charcoal edge haze.")
def saeed_portrait(rng):
    img, m = paint_portrait(rng)
    h, w = PH, PW
    x, y = px_grid(h, w)
    d = saturate(img, 0.22)
    d = d * C((226, 236, 250))[None, None, :] / 0.95
    d = 0.08 + 0.86 * d
    edge = np.minimum(np.minimum(x, w - 1 - x), np.minimum(y, h - 1 - y))
    haze = smoothstep(90, 0, edge) * (0.6 + 0.4 * spectral((h, w), rng, beta=2.0, fmin=3, fmax=40))
    d = mix(d, CHARCOAL, clamp01(haze) * 0.45)
    starts = []
    for k in range(16):
        side = k % 4
        t = rng.uniform(0.08, 0.92)
        if side == 0:
            starts.append((t * w, -2, math.pi / 2 + rng.normal(0, 0.4)))
        elif side == 1:
            starts.append((w + 2, t * h, math.pi + rng.normal(0, 0.4)))
        elif side == 2:
            starts.append((t * w, h + 2, -math.pi / 2 + rng.normal(0, 0.4)))
        else:
            starts.append((-2, t * h, rng.normal(0, 0.4)))
    cv, cv2 = SS(w, h, ss=3), SS(w, h, ss=3)
    def stop(px_, py_):        # cracks creep in from the border but never reach the face
        return ((px_ - 256) / 170.0) ** 2 + ((py_ - 300) / 215.0) ** 2 < 1.0

    for pts, wd in crack_paths(rng, starts, steps=26, step_len=6.5, wander=0.18, branch_p=0.10, max_depth=3, w0=1.6,
                               stop=stop):
        cv.line(pts, max(0.6, wd))
        cv2.line([(px_ + 1.0, py_ + 1.0) for px_, py_ in pts], max(0.5, wd * 0.7))
    cr = cv.cov()
    d = mix(d, C((214, 220, 226)), cv2.cov() * 0.22 * (1 - cr))
    d = mix(d, C((34, 36, 40)), cr * 0.75)
    return {"saeed_portrait.png": img, "saeed_portrait_drained.png": clamp01(d)}


# ------------------------------------------------------------------ passenger photo ovals
def _photo_oval(rng):
    n = 512
    x, y = px_grid(n, n)
    cx, cy, rx, ry = 256, 256, 200, 244
    sd = sd_ellipse(x, y, cx, cy, rx, ry)
    alpha = aa(sd, 1.2)
    base = np.broadcast_to(C((226, 222, 212)), (n, n, 3)).copy()
    base *= grain(n, n, rng, 0.012)[..., None]
    sheen = np.exp(-(((x - cx) * 0.7 + (y - cy) * 0.7 + 60) / 120) ** 2)
    base = mix(base, C((240, 238, 232)), sheen * 0.35)
    base = mix(base, base * 0.86, smoothstep(-26, 0, sd) * 0.6)
    return base, alpha, sd


def _silhouette_in_oval(rng):
    """Head-and-shoulders silhouette masks resampled from the portrait geometry into the oval canvas."""
    m = portrait_masks(rng)
    n = 512
    out = {}
    for k in ("face", "hair", "beard", "garment", "neck", "ears"):
        im = Image.fromarray((m[k] * 255).astype(np.uint8)).resize((int(PW * 0.80), int(PH * 0.80)), Image.LANCZOS)
        canvas = Image.new("L", (n, n), 0)
        canvas.paste(im, (int(256 - PW * 0.40), 30))
        out[k] = np.asarray(canvas, F32) / 255
    out["head"] = clamp01(np.maximum.reduce([out["face"], out["hair"], out["ears"]]))
    out["body"] = clamp01(np.maximum(out["garment"], out["neck"]))
    out["all"] = clamp01(np.maximum(out["head"], out["body"]))
    return out


@graphic("passenger_face",
         {"passenger_face.png": "Empty ID-photo oval: off-white matte, photo-paper sheen, faint head-and-shoulders "
                                "impression (no features).",
          "passenger_face_mimic.png": "Same oval with a faint, smeared, ghostly young-man head-and-shoulders "
                                      "silhouette (no facial features)."},
         "SDF oval alpha; sheen band + inner edge shade; silhouette from the portrait geometry, embossed "
         "(gradient light/shadow) for the empty version; smeared with directional blur, domain warp and drag "
         "streaks for the mimic.")
def passenger_face(rng):
    base, alpha, sd = _photo_oval(rng)
    n = 512
    sil = _silhouette_in_oval(rng)
    soft = blur(sil["all"], 9, wrap=False)
    gy, gx = np.gradient(blur(sil["all"], 4, wrap=False))
    emb = clamp01(-(gx * 0.6 + gy * 0.8) * 9)
    shd = clamp01((gx * 0.6 + gy * 0.8) * 9)
    empty = mix(base, base * 0.95, soft * 0.6)
    empty = mix(empty, C((246, 244, 238)), emb * 0.35)
    empty = mix(empty, empty * 0.88, shd * 0.45)
    dark = clamp01(sil["head"] * 0.55 + sil["body"] * 0.30 + sil["hair"] * 0.30 + sil["beard"] * 0.25)
    wx = spectral((n, n), rng, beta=2.5, fmin=2, fmax=20) * 7
    wy = spectral((n, n), rng, beta=2.5, fmin=2, fmax=20) * 5
    g = warp(dark, wx, wy, order=1, wrap=False)
    k = np.zeros((41, 9), F32)
    k[:, 4] = np.linspace(1.0, 0.2, 41)
    k /= k.sum()
    drag = ndimage.convolve(g, k, mode="nearest")
    g = blur(np.maximum(g * 0.75, drag), 3.0, wrap=False)
    streaks = clamp01(0.5 + 0.5 * spectral((n, n), rng, beta=1.0, fmin=6, fmax=120, elong=(8, 1)))
    ghost = clamp01(g * (0.75 + 0.35 * streaks))
    mimic = mix(base, C((96, 98, 102)), ghost * 0.40)
    mimic = mix(mimic, C((70, 72, 78)), blur(sil["hair"] * 0.5 + sil["beard"] * 0.4, 6, wrap=False) * 0.18)
    return {"passenger_face.png": rgba(empty, alpha), "passenger_face_mimic.png": rgba(mimic, alpha)}


# ------------------------------------------------------------------ postcards (1024 x 640)
CW, CH = 1024, 640


def _finish_card(img, rng, sky_mask=None):
    h, w = img.shape[:2]
    t1 = brush_tex(h, w, rng, angle=0.0, elong=8.0)
    t2 = brush_tex(h, w, rng, angle=0.5, elong=4.0)
    if sky_mask is None:
        sky_mask = np.zeros((h, w), F32)
    tex = 0.02 * t1 * sky_mask + 0.03 * t2 * (1 - sky_mask)
    img = img * (1 + tex)[..., None] * grain(h, w, rng, 0.018)[..., None] * vignette(h, w, 0.22)[..., None]
    return clamp01(postcard_border(clamp01(img), rng))


def _palm(cv, rng, bx, by, tx, ty, s):
    trunk = bezier((bx, by), (bx + 26 * s, (by + ty) / 2), (tx, ty), n=30)
    pts = np.array(trunk)
    wv = np.linspace(8.0 * s, 4.5 * s, len(pts))
    d = np.gradient(pts, axis=0)
    d /= np.linalg.norm(d, axis=1, keepdims=True) + 1e-6
    nrm = np.stack([-d[:, 1], d[:, 0]], 1)
    cv.poly([tuple(p) for p in pts + nrm * wv[:, None] / 2] + [tuple(p) for p in (pts - nrm * wv[:, None] / 2)[::-1]])
    for k in range(9):
        a = -math.pi * 0.98 + k * math.pi * 0.245 + rng.normal(0, 0.07)
        L = 78 * s * rng.uniform(0.8, 1.1)
        droop = 30 * s * (0.4 + abs(math.cos(a)))
        end = (tx + L * math.cos(a), ty + L * 0.55 * math.sin(a) + droop)
        mid = (tx + L * 0.55 * math.cos(a), ty + L * 0.55 * math.sin(a) * 0.9 - 8 * s)
        spine = bezier((tx, ty), mid, end, n=24)
        cv.poly(leaf_poly(spine, 9 * s))
    cv.ellipse(tx, ty + 4 * s, 7 * s, 6 * s, fill=255)


def card_pyramids(rng):
    h, w = CH, CW
    x, y = px_grid(h, w)
    hz = 468
    img = vgrad(h, w, [(0, (78, 106, 142)), (0.30, (160, 142, 142)), (0.56, (226, 170, 112)), (hz / h, (246, 204, 140)),
                       (1, (246, 204, 140))])
    img = mix(img, C((255, 214, 150)), glow(h, w, 236, 430, 240) * 0.6)
    img = mix(img, C((255, 242, 214)), aa(np.sqrt((x - 236) ** 2 + (y - 430) ** 2) - 30, 2.0))
    cl = spectral((h, w), rng, beta=2.0, fmin=2, fmax=40, elong=(7, 1))
    clouds = smoothstep(0.7, 1.8, cl) * smoothstep(60, 140, y) * smoothstep(360, 240, y)
    img = mix(img, C((236, 170, 140)), clouds * 0.45)
    lit_edge = clamp01(clouds - np.roll(clouds, -4, axis=0))
    img = mix(img, C((252, 214, 176)), lit_edge * 0.5)
    far = [(0, hz)] + [(xx, hz - 12 - 10 * math.sin(xx / 90.0) - 6 * math.sin(xx / 37.0)) for xx in range(0, w + 1, 16)] + [(w, hz + 20), (0, hz + 20)]
    img = over(img, np.broadcast_to(C((214, 160, 112)), (h, w, 3)), mask_poly(w, h, far))
    courses = 0.5 + 0.5 * np.sin(y * 2 * math.pi / 13.0)
    litc = vgrad(h, w, [(0, (240, 178, 106)), (1, (206, 136, 82))]) * (1 - 0.05 * smoothstep(0.6, 1.0, courses))[..., None]
    for (apex, bl, br, front, ds) in (((856, 270), (716, 482), (1010, 482), 872, 1.05), ((614, 166), (366, 486), (884, 486), 652, 1.0),
                                      ((328, 336), (246, 488), (418, 488), 342, 0.95)):
        ml = mask_poly(w, h, [apex, bl, (front, bl[1])])
        ms = mask_poly(w, h, [apex, (front, bl[1]), br])
        shc = vgrad(h, w, [(0, (150, 104, 98)), (1, (116, 80, 76))]) * ds
        img = over(img, litc, ml)
        img = over(img, shc, ms)
        img = mix(img, C((252, 214, 150)), mask_lines(w, h, [([apex, bl], 1.6)]) * 0.5)
    haze = smoothstep(380, 486, y) * smoothstep(520, 470, y)
    img = mix(img, C((244, 196, 140)), haze * 0.35)
    d1 = [(0, 506)] + bezier((0, 506), (260, 470), (520, 520), (1024, 500)) + [(1024, 640), (0, 640)]
    d2 = [(0, 590)] + bezier((0, 590), (300, 540), (640, 560), (1024, 610)) + [(1024, 640), (0, 640)]
    m1, m2 = mask_poly(w, h, d1), mask_poly(w, h, d2)
    img = over(img, vgrad(h, w, [(0, (234, 176, 108)), (0.8, (204, 140, 88)), (1, (180, 116, 76))]), m1)
    rip = 0.5 + 0.5 * np.sin((y + 18 * np.sin(x / 70.0)) * 2 * math.pi / 11.0)
    img = mix(img, img * 0.93, smoothstep(0.8, 1.0, rip) * m1 * (1 - m2) * 0.8)
    img = over(img, vgrad(h, w, [(0, (206, 142, 90)), (1, (160, 100, 70))]), m2)
    img = mix(img, C((250, 200, 140)), mask_lines(w, h, [(bezier((0, 590), (300, 540), (640, 560), (1024, 610)), 3)]) * 0.5)
    palm = SS(w, h, ss=3)
    _palm(palm, rng, 96, 566, 124, 404, 1.0)
    _palm(palm, rng, 178, 576, 166, 450, 0.78)
    pm = palm.cov()
    img = over(img, np.broadcast_to(C((92, 62, 50)), (h, w, 3)), pm)
    img = mix(img, C((206, 140, 90)), clamp01(pm - np.roll(pm, 2, axis=1)) * 0.5)
    return _finish_card(img, rng, smoothstep(hz + 4, hz - 4, y))


def card_bridge(rng):
    h, w = CH, CW
    x, y = px_grid(h, w)
    hz = 372
    sky = vgrad(h, w, [(0, (34, 42, 84)), (0.28, (84, 76, 126)), (0.46, (184, 116, 112)), (hz / h, (236, 158, 106)),
                       (1, (236, 158, 106))])
    img = sky.copy()
    img = mix(img, C((250, 190, 130)), glow(h, w, 380, hz, 200) * 0.45)
    stars = (rng.random((h, w)) < 0.0006) * smoothstep(200, 40, y)
    img = mix(img, C((240, 236, 220)), blur(stars.astype(F32), 0.6, wrap=False) * 2.0)
    sk = []
    xx = 0
    while xx < w:
        bw = rng.uniform(40, 90)
        top = hz - rng.uniform(34, 70)
        sk += [(xx, hz), (xx, top + 10), (xx + 8, top), (xx + bw - 8, top), (xx + bw, top + 10)]
        xx += bw
    sk += [(w, hz + 2), (0, hz + 2)]
    skm = mask_poly(w, h, sk)
    chim = SS(w, h, ss=2)
    for k in range(40):
        cx_ = rng.uniform(0, w)
        chim.rect(cx_, hz - rng.uniform(66, 84), cx_ + 6, hz - 26)
    city = clamp01(skm + chim.cov())
    img = over(img, mix(np.broadcast_to(C((62, 60, 96)), (h, w, 3)), sky, 0.25), city)
    win = (rng.random((h, w)) < 0.004) * city * smoothstep(hz - 70, hz - 8, y)
    win = ndimage.grey_dilation(win.astype(F32), size=(5, 3))
    img = mix(img, C((255, 200, 128)), win * 0.75)
    tw = SS(w, h, ss=3)
    left = bezier((628, hz + 4), (668, 260), (686, 92))
    right = bezier((752, hz + 4), (712, 260), (694, 92))
    tw.poly(left + right[::-1])
    tw.line([(690, 96), (690, 52)], 3)
    archm = SS(w, h, ss=3)
    archm.poly([(646, hz + 4)] + [(690 + 40 * math.cos(t), hz + 4 - 44 * math.sin(t)) for t in np.linspace(math.pi, 0, 30)] + [(734, hz + 4)])
    tm = clamp01(tw.cov() - archm.cov())
    plat = mask_polys(w, h, [[(648, 296), (732, 296), (728, 306), (652, 306)], [(670, 214), (710, 214), (708, 222), (672, 222)]])
    tm = clamp01(tm + plat)
    img = over(img, np.broadcast_to(C((50, 50, 84)), (h, w, 3)), tm)
    lat = mask_lines(w, h, [([(650, 360), (726, 300)], 1.0), ([(730, 360), (654, 300)], 1.0), ([(662, 296), (716, 222)], 0.8),
                            ([(718, 296), (664, 222)], 0.8), ([(676, 214), (702, 150)], 0.7), ([(704, 214), (678, 150)], 0.7)])
    img = mix(img, C((120, 104, 140)), lat * tm * 0.5)
    img = mix(img, C((255, 220, 160)), glow(h, w, 690, 54, 7) * 0.9)
    yy = np.clip(2 * hz - y, 0, h - 1).astype(int)
    refl = img[yy, np.clip(x.astype(int), 0, w - 1)]
    rip = spectral((h, w), rng, beta=1.5, fmin=4, fmax=120, elong=(14, 1))
    refl = warp(refl, rip * 6, rip * 0, order=1, wrap=False)
    water = refl * 0.62 + C((20, 24, 46)) * 0.25
    water = mix(water, water * 1.25, smoothstep(0.8, 1.8, rip) * 0.6)
    img = np.where((y > hz)[..., None], water, img)
    deck_top, wl = 388, 474
    bm = mask_poly(w, h, [(0, deck_top), (w, deck_top), (w, wl), (0, wl)])
    arches = []
    for ax in (110, 370, 630, 890):
        arches.append([(ax - 96, wl + 2)] + [(ax + 96 * math.cos(t), wl - 50 * math.sin(t)) for t in np.linspace(math.pi, 0, 40)] + [(ax + 96, wl + 2)])
    am = mask_polys(w, h, arches)
    under = vgrad(h, w, [(0, (22, 22, 40)), (0.70, (24, 24, 44)), (0.74, (40, 40, 66)), (1, (40, 40, 66))])
    img = mix(img, under, am * (y < wl) * 0.82)
    bm = clamp01(bm - am)
    img = over(img, vgrad(h, w, [(0, (64, 52, 76)), (0.62, (44, 40, 66)), (1, (30, 30, 50))]), bm)
    img = mix(img, C((236, 160, 120)), mask_lines(w, h, [([(0, deck_top + 1), (w, deck_top + 1)], 2.0)]) * 0.6)
    yy2 = np.clip(2 * wl - y, 0, h - 1).astype(int)
    br_refl = bm[yy2, np.clip(x.astype(int), 0, w - 1)] * (y > wl)
    br_refl = warp(br_refl, rip * 5, rip * 0, order=1, wrap=False) * smoothstep(wl + 110, wl, y)
    img = mix(img, C((28, 28, 48)), br_refl * 0.6)
    lamps = SS(w, h, ss=3)
    for lx_ in range(60, w, 120):
        lamps.line([(lx_, deck_top), (lx_, deck_top - 30)], 2.0)
        lamps.ellipse(lx_, deck_top - 34, 4.5, 6, fill=255)
        img = mix(img, C((255, 200, 130)), glow(h, w, lx_, deck_top - 34, 22) * 0.45)
        streak = np.exp(-((x - lx_) / 4.0) ** 2) * smoothstep(wl, wl + 6, y) * smoothstep(640, wl + 20, y)
        streak = warp(streak.astype(F32), rip * 4, rip * 0, order=1, wrap=False)
        img = mix(img, C((250, 180, 110)), streak * 0.35)
    lc = lamps.cov()
    img = over(img, np.broadcast_to(C((40, 36, 56)), (h, w, 3)), lc)
    img = mix(img, C((255, 226, 170)), lc * smoothstep(deck_top - 42, deck_top - 30, y) * smoothstep(deck_top - 26, deck_top - 32, y) * 0.9)
    return _finish_card(img, rng, smoothstep(hz + 4, hz - 4, y))


def card_lanterns(rng):
    h, w = CH, CW
    x, y = px_grid(h, w)
    vx, vy = 512, 352
    img = vgrad(h, w, [(0, (24, 30, 62)), (0.32, (52, 54, 102)), (0.52, (150, 100, 118)), (0.62, (176, 120, 120)), (1, (176, 120, 120))])
    pg = SS(w, h, ss=3)
    for i in range(5):
        wy = 318 - 46 * i
        ww = 118 - 17 * i
        body_w = ww * 0.55
        pg.rect(vx - body_w, wy, vx + body_w, wy + 34)
        roof = bezier((vx - ww - 18, wy - 6), (vx - ww * 0.6, wy + 4), (vx, wy + 2)) + bezier((vx, wy + 2), (vx + ww * 0.6, wy + 4), (vx + ww + 18, wy - 6))
        roof += [(vx + ww * 0.55, wy - 16), (vx - ww * 0.55, wy - 16)]
        pg.poly(roof)
    pg.line([(vx, 104), (vx, 40)], 4)
    for k in range(6):
        pg.ellipse(vx, 92 - k * 8, 7 - k * 0.6, 2.2, fill=255)
    pm = pg.cov()
    img = over(img, np.broadcast_to(C((28, 26, 48)), (h, w, 3)), pm)
    img = mix(img, C((120, 90, 130)), clamp01(pm - np.roll(pm, 2, axis=1)) * 0.6)
    floor = [(0, 640), (1024, 640), (vx + 46, vy + 10), (vx - 46, vy + 10)]
    fm = mask_poly(w, h, floor)
    img = over(img, vgrad(h, w, [(0, (40, 36, 44)), (0.55, (52, 46, 56)), (1, (66, 56, 62))]), fm)
    for sgn in (-1, 1):
        def X(xx):
            return vx + sgn * (vx - xx)
        fac = [(X(0), 110), (X(vx - 46), vy - 40), (X(vx - 46), vy + 10), (X(0), 610)]
        img = over(img, vgrad(h, w, [(0, (44, 30, 30)), (1, (58, 38, 34))]), mask_poly(w, h, fac))
        eave = SS(w, h, ss=3)
        eave.poly([(X(0), 238), (X(vx - 46), vy - 18), (X(vx - 46), vy - 12), (X(0), 262)])
        eave.poly([(X(0), 100), (X(vx - 46), vy - 44), (X(vx - 46), vy - 38), (X(0), 122)])
        img = over(img, np.broadcast_to(C((26, 20, 22)), (h, w, 3)), eave.cov())
        for k in range(6):
            t0, t1 = k / 6.0, (k + 0.62) / 6.0

            def P(t, yy0, yy1):
                xx = (vx - 46) * t ** 0.55
                return X(xx), yy0 + (yy1 - yy0) * t ** 0.55
            xa, ya_top = P(t0, 300, vy - 6)
            xb, yb_top = P(t1, 300, vy - 6)
            _, ya_bot = P(t0, 520, vy + 6)
            _, yb_bot = P(t1, 520, vy + 6)
            wm = mask_poly(w, h, [(xa, ya_top), (xb, yb_top), (xb, yb_bot), (xa, ya_bot)])
            img = over(img, np.broadcast_to(C((224, 150, 84)), (h, w, 3)), wm * 0.85)
            slats = SS(w, h, ss=2)
            for s in np.linspace(0, 1, 9)[1:-1]:
                xs_ = xa + (xb - xa) * s
                slats.line([(xs_, ya_top + (yb_top - ya_top) * s), (xs_, ya_bot + (yb_bot - ya_bot) * s)], max(0.8, 3 * (1 - t0)))
            img = over(img, np.broadcast_to(C((60, 34, 26)), (h, w, 3)), slats.cov() * wm)
    for sgn in (-1, 1):
        for k in range(9):
            t = (k + 0.3) / 9.0
            tt = t ** 0.6
            lx_ = vx + sgn * (vx - 46) * (1 - tt) * 0.86 + sgn * 18 * (1 - tt)
            ly_ = 270 + (vy - 30 - 270) * tt
            s = 30 * (1 - tt) + 4
            img = img + C((255, 140, 70)) * (glow(h, w, lx_, ly_, s * 2.6) * 0.30)[..., None]
            d = np.sqrt(((x - lx_) / (s * 0.86)) ** 2 + ((y - ly_) / s) ** 2)
            body = aa((d - 1) * s, 1.0)
            lcol = ramp(clamp01(d), [(0, (255, 214, 140)), (0.6, (236, 128, 66)), (1, (176, 56, 40))])
            img = over(img, lcol, body)
            cap = aa(np.maximum(np.abs(x - lx_) - s * 0.42, np.abs(y - (ly_ - s * 0.98)) - s * 0.12), 1.0)
            cap += aa(np.maximum(np.abs(x - lx_) - s * 0.42, np.abs(y - (ly_ + s * 0.98)) - s * 0.12), 1.0)
            img = over(img, np.broadcast_to(C((30, 22, 22)), (h, w, 3)), clamp01(cap))
            ribs = 0.5 + 0.5 * np.cos((y - ly_) / s * math.pi * 4)
            img = mix(img, img * 0.85, smoothstep(0.85, 1.0, ribs) * body * 0.6)
            ry0 = vy + 10 + (640 - vy - 10) * (1 - tt) * 0.75 + 20
            streak = np.exp(-((x - lx_) / max(2.0, s * 0.35)) ** 2) * smoothstep(ry0 - 30, ry0, y) * smoothstep(min(640, ry0 + 160 * (1 - tt) + 30), ry0, y)
            img = mix(img, C((236, 130, 70)), streak * fm * 0.35)
    img = mix(img, C((90, 60, 80)), glow(h, w, vx, vy, 120) * 0.25)
    return _finish_card(clamp01(img), rng, smoothstep(vy + 4, vy - 4, y) * (1 - fm))


def card_pavilion(rng):
    h, w = CH, CW
    x, y = px_grid(h, w)
    hz = 330
    img = vgrad(h, w, [(0, (110, 132, 180)), (0.22, (164, 156, 192)), (0.38, (226, 178, 176)), (hz / h, (248, 210, 180)),
                       (1, (248, 210, 180))])
    img = mix(img, C((255, 232, 196)), glow(h, w, 512, 300, 200) * 0.45)
    cl = smoothstep(0.8, 1.9, spectral((h, w), rng, beta=2.0, fmin=2, fmax=30, elong=(8, 1))) * smoothstep(40, 120, y) * smoothstep(280, 160, y)
    img = mix(img, C((240, 196, 196)), cl * 0.40)
    cx = 512
    pv = [[(300, 318), (724, 318), (724, 332), (300, 332)], [(378, 228), (646, 228), (646, 318), (378, 318)],
          [(450, 206), (574, 206), (574, 228), (450, 228)]]
    dome = bezier((452, 206), (400, 170), (470, 140), (512, 108)) + bezier((512, 108), (554, 140), (624, 170), (572, 206))
    pv.append(dome)
    for sx in (404, 620):
        pv.append([(sx - 24, 210), (sx + 24, 210), (sx + 24, 228), (sx - 24, 228)])
        pv.append(bezier((sx - 22, 210), (sx - 30, 192), (sx - 6, 182), (sx, 172)) + bezier((sx, 172), (sx + 6, 182), (sx + 30, 192), (sx + 22, 210)))
    for sx in (316, 708):
        pv.append([(sx - 8, 332), (sx - 7, 150), (sx + 7, 150), (sx + 8, 332)])
        pv.append([(sx - 13, 268), (sx + 13, 268), (sx + 13, 274), (sx - 13, 274)])
        pv.append([(sx - 12, 206), (sx + 12, 206), (sx + 12, 212), (sx - 12, 212)])
        pv.append(bezier((sx - 10, 152), (sx - 12, 140), (sx, 132), (sx, 128)) + bezier((sx, 128), (sx, 132), (sx + 12, 140), (sx + 10, 152)))
    pm = mask_polys(w, h, pv)
    fin = mask_lines(w, h, [([(512, 110), (512, 86)], 2.0), ([(404, 174), (404, 162)], 1.4), ([(620, 174), (620, 162)], 1.4)])
    pm = clamp01(pm + fin)
    lit = smoothstep(560, 430, x)
    marble = mix(np.broadcast_to(C((176, 174, 204)), (h, w, 3)), C((250, 238, 232)), lit)
    marble = mix(marble, C((255, 226, 214)), lit * smoothstep(330, 110, y) * 0.35)
    img = over(img, marble, pm)
    outline = clamp01(pm - ndimage.grey_erosion(pm, size=(3, 3)))
    img = mix(img, C((150, 140, 170)), outline * 0.55)
    iwan = bezier((478, 318), (478, 262), (500, 246), (512, 238)) + bezier((512, 238), (524, 246), (546, 262), (546, 318))
    img = over(img, vgrad(h, w, [(0, (130, 130, 166)), (1, (160, 156, 182))]), mask_poly(w, h, iwan))
    niches = []
    for sx in (412, 612):
        for (yt, yb) in ((240, 272), (282, 314)):
            niches.append([(sx - 14, yb), (sx - 14, yt + 10), (sx, yt), (sx + 14, yt + 10), (sx + 14, yb)])
    img = over(img, np.broadcast_to(C((160, 158, 188)), (h, w, 3)), mask_polys(w, h, niches))
    ground = vgrad(h, w, [(0, (104, 128, 100)), (1, (80, 108, 78))])
    stripes = 0.5 + 0.5 * np.sin((x - cx) / np.maximum(y - hz + 8, 1) * 6.0)
    ground = ground * (1 - 0.05 * smoothstep(0.5, 1.0, stripes))[..., None]
    img = np.where((y > hz)[..., None], ground, img)
    img = mix(img, C((236, 222, 210)), smoothstep(hz + 60, hz, y) * (y > hz) * 0.5)
    walk = mask_poly(w, h, [(cx - 330, 640), (cx + 330, 640), (cx + 48, hz + 2), (cx - 48, hz + 2)])
    img = over(img, vgrad(h, w, [(0, (214, 202, 190)), (1, (222, 210, 198))]), walk)
    pool = mask_poly(w, h, [(cx - 190, 640), (cx + 190, 640), (cx + 20, hz + 6), (cx - 20, hz + 6)])
    yy = np.clip(2 * hz - y, 0, h - 1).astype(int)
    refl = img[yy, np.clip(x.astype(int), 0, w - 1)]
    rip = spectral((h, w), rng, beta=1.6, fmin=4, fmax=100, elong=(12, 1))
    refl = warp(refl, rip * 3, rip * 0, order=1, wrap=False)
    water = mix(refl * 0.84, C((96, 118, 152)), 0.16)
    img = over(img, water, pool)
    edges = mask_lines(w, h, [([(cx - 190, 640), (cx - 20, hz + 6)], 2.0), ([(cx + 190, 640), (cx + 20, hz + 6)], 2.0)])
    img = mix(img, C((236, 228, 218)), edges * 0.8)
    for sgn in (-1, 1):
        for k in range(10):
            t = k / 10.0
            tt = t ** 0.7
            bx = cx + sgn * (380 * (1 - tt) + 70 * tt)
            by = 640 - (640 - hz - 4) * tt
            hh = 200 * (1 - tt) + 22
            ww_ = hh * 0.16
            tree = bezier((bx - ww_, by), (bx - ww_ * 1.3, by - hh * 0.6), (bx, by - hh)) + bezier((bx, by - hh), (bx + ww_ * 1.3, by - hh * 0.6), (bx + ww_, by))
            tc = mix(np.broadcast_to(C((40, 66, 54)), (h, w, 3)), C((80, 108, 82)), smoothstep(bx + ww_, bx - ww_, x) * 0.6)
            img = over(img, tc, mask_poly(w, h, tree))
    mist = smoothstep(hz + 80, hz - 10, y) * smoothstep(hz - 60, hz, y)
    img = mix(img, C((246, 228, 216)), mist * 0.30)
    return _finish_card(clamp01(img), rng, smoothstep(hz + 4, hz - 4, y))


@graphic("postcards",
         {"postcard_1.png": "Postcard: pyramids at golden hour (stylised painting, no text).",
          "postcard_2.png": "Postcard: river bridge with a distant lattice tower at dusk.",
          "postcard_3.png": "Postcard: lantern-lit street with a pagoda silhouette.",
          "postcard_4.png": "Postcard: domed white pavilion reflected in a long pool at dawn."},
         "Layered silhouettes (supersampled polygons/Beziers), gradient skies, glows, mirrored water with "
         "ripple warps, perspective rows; painterly anisotropic brush texture, grain, vignette, ivory border.")
def postcards(rng):
    return {"postcard_1.png": card_pyramids(rng), "postcard_2.png": card_bridge(rng),
            "postcard_3.png": card_lanterns(rng), "postcard_4.png": card_pavilion(rng)}
