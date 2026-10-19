"""Twelve original ink stamps (512x512 RGBA, transparent background, no lettering).

Each design is drawn as vector shapes in 'stamp units' (centre 0,0; outer ring radius 1; y down) into a
4x supersampled mask. The mask is then 'inked': rough edges (blurred mask + noise threshold), uneven
pressure gradient, blotchy density, dry patches, pin-hole voids and a slight ink rim, then box-downsampled.
Symbols are original simplified drawings; no national emblems, crests or flags.
"""
from __future__ import annotations

import math

import numpy as np
from scipy import ndimage

from tx_core import F32, C, SS, bezier, clamp01, downsample, rgba, smoothstep, spectral
from tx_graphics import INK_RED, graphic

SIZE, SSF, SCALE = 512, 4, 236


class Stamp:
    """Vector drawing in stamp units; optional transform xf(x, y) -> (x, y) applied to every point."""

    def __init__(self, xf=None):
        self.cv = SS(SIZE, SIZE, ss=SSF)
        self.xf = xf

    def P(self, x, y):
        if self.xf is not None:
            x, y = self.xf(x, y)
        return (SIZE / 2 + x * SCALE, SIZE / 2 + y * SCALE)

    def poly(self, pts, ink=True):
        self.cv.poly([self.P(*p) for p in pts], fill=255 if ink else 0)

    def line(self, pts, w, ink=True, caps=True):
        self.cv.line([self.P(*p) for p in pts], w * SCALE, fill=255 if ink else 0, round_caps=caps)

    def ellipse(self, x, y, rx, ry, ink=True, n=72):
        self.poly([(x + rx * math.cos(t), y + ry * math.sin(t)) for t in np.linspace(0, 2 * math.pi, n, endpoint=False)], ink)

    def circle(self, x, y, r, ink=True):
        self.ellipse(x, y, r, r, ink, n=max(16, int(r * 300)))

    def ring(self, x, y, rx, ry, w, ink=True):
        pts = [(x + rx * math.cos(t), y + ry * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 181)]
        self.line(pts, w, ink, caps=False)

    def rect(self, x0, y0, x1, y1, ink=True):
        self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], ink)

    def hires(self):
        return self.cv.hires()


# ------------------------------------------------------------------ helpers
def catmull(pts, n=10, closed=True):
    P = list(pts)
    if closed:
        P = [P[-1]] + P + [P[0], P[1]]
    else:
        P = [P[0]] + P + [P[-1]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for t in np.linspace(0, 1, n, endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[k]) + (-p0[k] + p2[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t2
                                    + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t3) for k in range(2)))
    if not closed:
        out.append(pts[-1])
    return out


def border(st, rx=1.0, ry=1.0, w_out=0.052, w_in=0.022, k_in=0.835):
    st.ring(0, 0, rx - w_out / 2, ry - w_out / 2, w_out)
    st.ring(0, 0, rx * k_in, ry * k_in, w_in)


def band_xy(rx, ry, t, k=0.912):
    return rx * k * math.cos(t), ry * k * math.sin(t)


def band_dots(st, rx, ry, n, r, phase=0.0, alt=None, jitter=None):
    for i in range(n):
        t = phase + 2 * math.pi * i / n + (jitter[i] if jitter is not None else 0.0)
        x, y = band_xy(rx, ry, t)
        rr = r if alt is None or i % 2 == 0 else alt
        st.circle(x, y, rr)


def band_ticks(st, rx, ry, n, w, k0=0.865, k1=0.955, phase=0.0):
    for i in range(n):
        t = phase + 2 * math.pi * i / n
        st.line([(rx * k0 * math.cos(t), ry * k0 * math.sin(t)), (rx * k1 * math.cos(t), ry * k1 * math.sin(t))], w,
                caps=False)


def band_diamonds(st, rx, ry, n, s, phase=0.0):
    for i in range(n):
        t = phase + 2 * math.pi * i / n
        x, y = band_xy(rx, ry, t)
        ct, sn = math.cos(t), math.sin(t)
        st.poly([(x + s * ct * 1.6, y + s * sn * 1.6), (x - s * sn, y + s * ct), (x - s * ct * 1.6, y - s * sn * 1.6),
                 (x + s * sn, y - s * ct)])


def band_rings(st, rx, ry, n, r, w):
    for i in range(n):
        t = 2 * math.pi * i / n
        x, y = band_xy(rx, ry, t)
        st.ring(x, y, r, r, w)


def band_squares(st, rx, ry, n, s):
    for i in range(n):
        t = 2 * math.pi * i / n
        x, y = band_xy(rx, ry, t)
        ct, sn = math.cos(t), math.sin(t)
        st.poly([(x + s * (ct - sn), y + s * (sn + ct)), (x + s * (-ct - sn), y + s * (-sn + ct)),
                 (x + s * (-ct + sn), y + s * (-sn - ct)), (x + s * (ct + sn), y + s * (sn - ct))])


def band_zigzag(st, rx, ry, n, w):
    pts = []
    for i in range(n * 2 + 1):
        t = 2 * math.pi * i / (n * 2)
        k = 0.875 if i % 2 == 0 else 0.948
        pts.append((rx * k * math.cos(t), ry * k * math.sin(t)))
    st.line(pts, w)


def wavy(x0, x1, y, amp, lam, n=60):
    return [(x, y + amp * math.sin(2 * math.pi * x / lam)) for x in np.linspace(x0, x1, n)]


def knock_outline(st, pts, w):
    st.line(list(pts) + [pts[0]], w, ink=False)


# ------------------------------------------------------------------ ink rendering
def ink(hi, rng, density=1.0, rough=1.0, voids=1.0, press=None):
    H = hi.shape[0]
    m = ndimage.gaussian_filter(hi, 1.5)
    n1 = spectral((H, H), rng, beta=1.2, fmin=40, fmax=700)
    edge = clamp01((m - 0.5 + 0.11 * rough * n1) * 4.0 + 0.5)
    yy, xx = np.mgrid[0:H, 0:H].astype(F32) / H * 2 - 1
    a = rng.uniform(0, 2 * math.pi) if press is None else press
    grad = 0.78 + 0.22 * (0.5 + 0.5 * (xx * math.cos(a) + yy * math.sin(a)))
    blot = 1 + 0.12 * spectral((H, H), rng, beta=2.2, fmin=2, fmax=18)
    void = smoothstep(1.4, 2.4, spectral((H, H), rng, beta=0.3, fmin=120, fmax=900)) * voids
    dry = smoothstep(1.1, 2.2, spectral((H, H), rng, beta=1.6, fmin=5, fmax=50)) * 0.32 * voids
    dens = clamp01(density * grad * blot * (1 - 0.9 * void) * (1 - dry))
    inner = ndimage.gaussian_filter(edge, 5.0)
    rim = clamp01(edge - inner) * 0.45
    alpha = clamp01(edge * (dens + rim)) * 0.94
    return downsample(alpha.astype(F32), SSF)


def finish(alpha, col=INK_RED):
    rgb = np.broadcast_to(col, alpha.shape + (3,))
    return rgba(rgb, alpha)


# ------------------------------------------------------------------ designs (stamp units, y down)
OVAL = (0.84, 1.0)


def d_uae(st, rng):
    rx, ry = OVAL
    border(st, rx, ry)
    band_dots(st, rx, ry, 30, 0.02)
    A, B, Cc = (-0.48, -0.02), (0.34, -0.70), (0.34, 0.05)
    luff = bezier(A, (-0.28, -0.52), B)
    leech = bezier(B, (0.27, -0.30), Cc)
    foot = bezier(Cc, (-0.05, 0.03), A)
    sail = luff + leech + foot
    st.poly(sail)
    for t in (0.33, 0.58, 0.82):           # sail seams (negative lines)
        i = int(t * (len(luff) - 1))
        p = luff[i]
        q = (p[0] + 0.30 * (Cc[0] - B[0]) + 0.05, p[1] + 0.62 * (Cc[1] - B[1]) * (1 - t * 0.4))
        st.line([p, q], 0.016, ink=False)
    yard = [(x - 0.025, y - 0.02) for x, y in bezier((-0.55, 0.03), (-0.30, -0.55), (0.38, -0.75))]
    st.line(yard, 0.03)
    st.line([(0.05, 0.14), (0.10, -0.66)], 0.03)
    hull = bezier((-0.54, 0.02), (0.0, 0.15), (0.44, 0.08)) + [(0.41, 0.17)] + bezier((0.40, 0.17), (-0.06, 0.30), (-0.54, 0.02))
    knock_outline(st, hull, 0.05)
    st.poly(hull)
    st.line(wavy(-0.50, 0.50, 0.29, 0.014, 0.17), 0.026)
    st.line(wavy(-0.40, 0.40, 0.36, 0.012, 0.15), 0.022)
    base = 0.58
    blds = [(-0.47, -0.40, 0.05), (-0.40, -0.33, 0.09), (-0.33, -0.27, 0.06), (-0.27, -0.22, 0.15), (-0.22, -0.13, 0.07),
            (-0.13, -0.02, 0.10), (-0.02, 0.06, 0.06), (0.06, 0.12, 0.12), (0.12, 0.22, 0.08), (0.22, 0.27, 0.05),
            (0.27, 0.35, 0.10), (0.35, 0.41, 0.07), (0.41, 0.47, 0.04)]
    poly = [(-0.48, base)]
    for x0, x1, hgt in blds:
        poly += [(x0, base - hgt), (x1, base - hgt)]
    poly += [(0.48, base)]
    st.poly(poly)
    st.ellipse(-0.075, base - 0.10, 0.055, 0.05)          # small dome
    st.line([(-0.245, base - 0.15), (-0.245, base - 0.22)], 0.012)   # mast on the tallest block
    for x0, x1, hgt in blds:                                # tiny window knockouts
        if hgt > 0.075:
            st.line([((x0 + x1) / 2, base - hgt + 0.03), ((x0 + x1) / 2, base - 0.02)], 0.01, ink=False)
    st.line([(-0.50, base + 0.035), (0.50, base + 0.035)], 0.02)


def d_egypt(st, rng):
    border(st)
    band_ticks(st, 1, 1, 40, 0.016)
    st.circle(0.36, -0.40, 0.12)
    st.ring(0.36, -0.40, 0.175, 0.175, 0.02)
    for k in range(12):
        a = 2 * math.pi * k / 12
        st.line([(0.36 + 0.205 * math.cos(a), -0.40 + 0.205 * math.sin(a)),
                 (0.36 + 0.25 * math.cos(a), -0.40 + 0.25 * math.sin(a))], 0.02)

    def pyramid(apex, bl, br, front_x, stripe=0.064):
        face_l = [apex, bl, (front_x, bl[1])]
        face_r = [apex, (front_x, bl[1]), br]
        knock_outline(st, [apex, bl, br], 0.045)
        st.poly(face_l)
        st.poly(face_r)
        y = apex[1] + 0.09
        while y < bl[1] - 0.03:
            tl = (y - apex[1]) / (bl[1] - apex[1])
            xl = apex[0] + (bl[0] - apex[0]) * tl
            xf = apex[0] + (front_x - apex[0]) * tl
            if xf - xl > 0.07:
                st.line([(xl + 0.035, y), (xf - 0.03, y)], 0.022, ink=False, caps=False)
            y += stripe
    pyramid((0.40, -0.04), (0.16, 0.40), (0.60, 0.40), 0.45)
    pyramid((-0.10, -0.42), (-0.62, 0.40), (0.42, 0.40), 0.02)
    st.line([(-0.64, 0.44), (0.64, 0.44)], 0.035)
    st.line([(-0.48, 0.52), (0.48, 0.52)], 0.02)


def d_france(st, rng):
    rx, ry = OVAL
    border(st, rx, ry)
    band_dots(st, rx, ry, 40, 0.012, alt=0.02)
    # lattice tower (behind)
    legL = bezier((0.07, 0.10), (0.21, -0.10), (0.245, -0.66))
    legR = bezier((0.45, 0.10), (0.31, -0.10), (0.275, -0.66))
    st.line(legL, 0.032)
    st.line(legR, 0.032)
    st.line([(0.26, -0.68), (0.26, -0.75)], 0.018)
    st.poly([(0.235, -0.66), (0.285, -0.66), (0.26, -0.70)])
    for yb, hw in ((-0.10, 0.14), (-0.40, 0.05)):
        st.line([(0.26 - hw - 0.02, yb), (0.26 + hw + 0.02, yb)], 0.03)
    st.line(bezier((0.10, 0.10), (0.26, -0.06), (0.42, 0.10)), 0.02)
    for (y0, y1) in ((-0.40, -0.25), (-0.25, -0.10)):
        def lx(y, leg):
            i = int(np.argmin([abs(p[1] - y) for p in leg]))
            return leg[i][0]
        st.line([(lx(y0, legL), y0), (lx(y1, legR), y1)], 0.012)
        st.line([(lx(y0, legR), y0), (lx(y1, legL), y1)], 0.012)
    # bridge
    bridge = [(-0.64, 0.06), (0.64, 0.06), (0.64, 0.36), (-0.64, 0.36)]
    st.poly([(-0.66, 0.02), (0.66, 0.02), (0.66, 0.38), (-0.66, 0.38)], ink=False)
    st.rect(-0.64, 0.10, 0.64, 0.17)
    st.line([(-0.64, 0.055), (0.64, 0.055)], 0.02)
    for xb in np.arange(-0.60, 0.61, 0.08):
        st.line([(xb, 0.06), (xb, 0.10)], 0.014, caps=False)
    st.rect(-0.62, 0.17, 0.62, 0.36)
    for ax in (-0.38, 0.0, 0.38):
        arch = [(ax - 0.15, 0.37)] + [(ax + 0.15 * math.cos(t), 0.36 - 0.15 * math.sin(t))
                                      for t in np.linspace(math.pi, 0, 30)] + [(ax + 0.15, 0.37)]
        st.poly(arch, ink=False)
        st.line([(ax + 0.17 * math.cos(t), 0.36 - 0.17 * math.sin(t)) for t in np.linspace(math.pi, 0, 30)], 0.012,
                ink=False)
    st.line(wavy(-0.55, 0.55, 0.44, 0.012, 0.16), 0.024)
    st.line(wavy(-0.45, 0.45, 0.51, 0.011, 0.14), 0.022)
    st.line(wavy(-0.32, 0.32, 0.58, 0.010, 0.12), 0.02)


def d_usa(st, rng):
    border(st)
    band_ticks(st, 1, 1, 56, 0.012, k0=0.875, k1=0.945)
    st.poly([(-0.045, 0.66), (0.045, 0.66), (0.085, 0.08), (-0.085, 0.08)])
    for yy in (0.30, 0.42, 0.54):
        st.line([(-0.12, yy), (0.12, yy)], 0.018, ink=False, caps=False)
    st.rect(-0.11, 0.02, 0.11, 0.08)
    st.poly([(-0.21, -0.10), (0.21, -0.10), (0.12, 0.02), (-0.12, 0.02)])
    st.rect(-0.235, -0.15, 0.235, -0.10)
    st.line([(-0.16, -0.045), (0.16, -0.045)], 0.016, ink=False, caps=False)
    outer = [(-0.19, -0.16), (-0.27, -0.31), (-0.17, -0.51), (-0.12, -0.39), (-0.07, -0.54), (0.0, -0.76),
             (0.07, -0.57), (0.11, -0.43), (0.17, -0.55), (0.26, -0.33), (0.19, -0.16)]
    st.poly(catmull(outer, 10))
    inner = [(-0.07, -0.19), (-0.10, -0.31), (-0.03, -0.42), (0.0, -0.50), (0.04, -0.41), (0.10, -0.30), (0.07, -0.19)]
    st.poly(catmull(inner, 10), ink=False)
    st.poly(catmull([(-0.025, -0.22), (-0.035, -0.29), (0.0, -0.36), (0.035, -0.29), (0.025, -0.22)], 8))


def d_japan(st, rng):
    rx, ry = OVAL
    border(st, rx, ry)
    band_dots(st, rx, ry, 26, 0.018, phase=math.pi / 26)
    for t in (-math.pi / 2, math.pi / 2):
        x, y = band_xy(rx, ry, t)
        st.poly([(x, y - 0.045), (x + 0.035, y), (x, y + 0.045), (x - 0.035, y)])
    top = bezier((-0.52, -0.50), (-0.38, -0.56), (-0.24, -0.64)) + [(0.24, -0.64)] + bezier((0.24, -0.64), (0.38, -0.56), (0.52, -0.50))
    low = bezier((0.52, -0.50), (0.30, -0.40), (0.0, -0.43)) + bezier((0.0, -0.43), (-0.30, -0.40), (-0.52, -0.50))
    st.poly(top + low)
    st.line(bezier((-0.40, -0.515), (0.0, -0.55), (0.40, -0.515)), 0.014, ink=False)
    st.line([(-0.26, -0.67), (0.26, -0.67)], 0.03)
    st.line([(-0.40, -0.345), (0.40, -0.345)], 0.03)
    st.line([(0, -0.345), (0, -0.25)], 0.018)
    st.rect(-0.12, -0.255, 0.12, -0.19)
    st.ellipse(0, 0.07, 0.30, 0.27)
    st.rect(-0.12, 0.325, 0.12, 0.39)
    for yk in (-0.11, -0.03, 0.05, 0.13, 0.21):
        w = 0.30 * math.sqrt(max(0.0, 1 - ((yk - 0.07) / 0.27) ** 2)) - 0.035
        pts = [(x, yk + 0.025 * (1 - (x / max(w, 1e-3)) ** 2)) for x in np.linspace(-w, w, 30)]
        st.line(pts, 0.017, ink=False, caps=False)
    st.line([(0, 0.39), (0, 0.50)], 0.02)
    st.poly([(-0.035, 0.49), (0.035, 0.49), (0.06, 0.62), (-0.06, 0.62)])
    for xk in (-0.03, 0.0, 0.03):
        st.line([(xk, 0.53), (xk * 1.6, 0.62)], 0.008, ink=False)


def d_saudi(st, rng):
    rx, ry = OVAL
    border(st, rx, ry)
    band_diamonds(st, rx, ry, 18, 0.02)
    st.line([(-0.44, 0.62), (0.44, 0.62)], 0.03)
    left = [(-0.18, 0.62), (-0.125, -0.25)] + bezier((-0.125, -0.25), (-0.115, -0.60), (-0.10, -0.77))[1:]
    right = [(x * -1, y) for x, y in reversed(left)]
    st.poly(left + right)
    yv, yt, w = -0.30, -0.80, 0.078
    cut = [(-w * math.sqrt(max(0.0, (yv - y) / (yv - yt))), y) for y in np.linspace(yt, yv, 30)]
    cut += [(-x, y) for x, y in reversed(cut)]
    st.poly(cut, ink=False)
    st.line([(-0.10, -0.585), (0.10, -0.585)], 0.022, caps=False)
    for xk in (-0.075, -0.025, 0.025, 0.075):
        st.line([(xk, 0.58), (xk * 0.8, -0.24)], 0.011, ink=False, caps=False)
    st.rect(-0.40, 0.46, -0.24, 0.61)
    st.rect(0.24, 0.50, 0.40, 0.61)
    st.rect(-0.30, 0.40, -0.25, 0.46)
    for (x0, y0) in ((-0.37, 0.50), (-0.31, 0.50), (-0.37, 0.555), (-0.31, 0.555), (0.27, 0.535), (0.33, 0.535)):
        st.rect(x0, y0, x0 + 0.025, y0 + 0.025, ink=False)


def _pavilion(st):
    st.rect(-0.52, 0.03, 0.52, 0.10)
    st.rect(-0.32, -0.20, 0.32, 0.03)
    arch = [(-0.09, 0.035), (-0.09, -0.08)] + bezier((-0.09, -0.08), (-0.09, -0.15), (0.0, -0.17)) + \
        bezier((0.0, -0.17), (0.09, -0.15), (0.09, -0.08)) + [(0.09, 0.035)]
    st.poly(arch, ink=False)
    for sx in (-0.21, 0.21):
        st.poly([(sx - 0.035, 0.035), (sx - 0.035, -0.05), (sx, -0.10), (sx + 0.035, -0.05), (sx + 0.035, 0.035)], ink=False)
    st.rect(-0.15, -0.26, 0.15, -0.20)
    dome = bezier((-0.15, -0.26), (-0.31, -0.36), (-0.10, -0.47), (0.0, -0.58)) + \
        bezier((0.0, -0.58), (0.10, -0.47), (0.31, -0.36), (0.15, -0.26))
    st.poly(dome)
    st.line([(0, -0.58), (0, -0.67)], 0.02)
    st.circle(0, -0.635, 0.02)
    for sx in (-0.265, 0.265):
        st.rect(sx - 0.06, -0.235, sx + 0.06, -0.20)
        st.poly(bezier((sx - 0.06, -0.235), (sx - 0.08, -0.29), (sx, -0.34)) + bezier((sx, -0.34), (sx + 0.08, -0.29), (sx + 0.06, -0.235)))
        st.line([(sx, -0.34), (sx, -0.38)], 0.012)
    for sx in (-0.47, 0.47):
        st.rect(sx - 0.026, -0.36, sx + 0.026, 0.03)
        for yb in (-0.12, -0.26):
            st.rect(sx - 0.045, yb - 0.015, sx + 0.045, yb + 0.015)
        st.poly(bezier((sx - 0.035, -0.36), (sx - 0.04, -0.40), (sx, -0.43)) + bezier((sx, -0.43), (sx + 0.04, -0.40), (sx + 0.035, -0.36)))


def d_india(st, rng):
    border(st)
    band_rings(st, 1, 1, 18, 0.024, 0.01)
    _pavilion(st)
    st.line([(-0.68, 0.11), (0.68, 0.11)], 0.032)
    wl = 0.135
    st.xf = lambda x, y: (x, wl + (0.10 - y) * 0.82)
    _pavilion(st)
    st.xf = None
    y = 0.165
    while y < 0.72:
        half = math.sqrt(max(0.0, 0.80 ** 2 - y * y))
        st.line([(-half, y), (half, y)], 0.022, ink=False, caps=False)
        y += 0.05


def d_brazil(st, rng):
    border(st)
    band_dots(st, 1, 1, 32, 0.022, alt=0.012)
    rx_, ryt = 0.40, 0.12
    yt, yb = -0.10, 0.40

    def front(y0, x):
        return y0 + ryt * math.sqrt(max(0.0, 1 - (x / rx_) ** 2))
    xs = np.linspace(-rx_, rx_, 60)
    st.poly([(x, front(yt, x)) for x in xs] + [(x, front(yb, x)) for x in xs[::-1]])
    st.poly([(x, front(yt, x) + 0.075) for x in xs] + [(x, front(yb, x) - 0.075) for x in xs[::-1]], ink=False)
    xs2 = np.linspace(-0.36, 0.36, 9)
    for i in range(len(xs2) - 1):
        xa, xb = xs2[i], xs2[i + 1]
        st.line([(xa, front(yt, xa) + 0.075), ((xa + xb) / 2, front(yb, (xa + xb) / 2) - 0.075), (xb, front(yt, xb) + 0.075)],
                0.016)
    st.ring(0, yt, rx_ - 0.02, ryt - 0.01, 0.04)
    st.line([(-0.42, -0.60), (0.06, -0.19)], 0.032)
    st.line([(0.42, -0.60), (-0.06, -0.19)], 0.032)
    st.circle(0.07, -0.185, 0.035)
    st.circle(-0.07, -0.185, 0.035)
    placed = 0
    tries = 0
    while placed < 26 and tries < 2000:
        tries += 1
        x, y = rng.uniform(-0.72, 0.72), rng.uniform(-0.72, 0.72)
        if x * x + y * y > 0.70 ** 2:
            continue
        if -0.48 < x < 0.48 and -0.28 < y < 0.58:
            continue
        if abs(abs(x) - (0.42 - (y + 0.60) * (0.36 / 0.41))) < 0.08 and -0.66 < y < -0.15:
            continue
        a = rng.uniform(0, math.pi)
        if rng.random() < 0.6:
            L, W = rng.uniform(0.03, 0.05), 0.016
            ca, sa = math.cos(a), math.sin(a)
            st.poly([(x - L * ca - W * sa, y - L * sa + W * ca), (x + L * ca - W * sa, y + L * sa + W * ca),
                     (x + L * ca + W * sa, y + L * sa - W * ca), (x - L * ca + W * sa, y - L * sa - W * ca)])
        else:
            st.circle(x, y, rng.uniform(0.014, 0.022))
        placed += 1


def d_uk(st, rng):
    border(st)
    band_ticks(st, 1, 1, 60, 0.011, k0=0.875, k1=0.95)
    st.ring(0, 0.02, 0.56, 0.56, 0.045)
    st.ring(0, 0.02, 0.50, 0.50, 0.014)
    for k in range(60):
        a = 2 * math.pi * k / 60
        if k % 5 == 0:
            continue
        st.line([(0.445 * math.sin(a), 0.02 - 0.445 * math.cos(a)), (0.48 * math.sin(a), 0.02 - 0.48 * math.cos(a))], 0.011,
                caps=False)
    for k in range(12):
        a = 2 * math.pi * k / 12
        offs = (-0.028, 0.028) if k == 0 else (0.0,)
        for o in offs:
            px, py = o * math.cos(a), o * math.sin(a)
            st.line([(px + 0.36 * math.sin(a), py + 0.02 - 0.36 * math.cos(a)),
                     (px + 0.475 * math.sin(a), py + 0.02 - 0.475 * math.cos(a))], 0.042, caps=False)

    def hand(angle_deg, L, hw, tail):
        a = math.radians(angle_deg)
        ux, uy = math.sin(a), -math.cos(a)
        nx, ny = -uy, ux
        cx, cy = 0, 0.02
        return [(cx - ux * tail + nx * hw * 0.6, cy - uy * tail + ny * hw * 0.6), (cx + ux * L * 0.7 + nx * hw, cy + uy * L * 0.7 + ny * hw),
                (cx + ux * L, cy + uy * L), (cx + ux * L * 0.7 - nx * hw, cy + uy * L * 0.7 - ny * hw),
                (cx - ux * tail - nx * hw * 0.6, cy - uy * tail - ny * hw * 0.6)]
    st.poly(hand(-6.0, 0.43, 0.026, 0.08))      # minute hand at 59 min
    st.poly(hand(-0.5, 0.28, 0.045, 0.06))      # hour hand just before 12
    st.circle(0, 0.02, 0.05)
    st.circle(0, 0.02, 0.016, ink=False)
    st.poly([(-0.11, -0.585), (0.11, -0.585), (0.0, -0.755)])
    st.circle(0, -0.772, 0.02)
    st.line([(-0.24, 0.625), (0.24, 0.625)], 0.03)
    st.line([(-0.16, 0.675), (0.16, 0.675)], 0.022)


def d_morocco(st, rng):
    rx, ry = OVAL
    border(st, rx, ry)
    band_zigzag(st, rx, ry, 36, 0.014)
    cx, cy, R = 0.0, -0.06, 0.56

    def star(R_, w):
        s = R_ / math.sqrt(2)
        sq1 = [(cx - s, cy - s), (cx + s, cy - s), (cx + s, cy + s), (cx - s, cy + s)]
        sq2 = [(cx, cy - R_), (cx + R_, cy), (cx, cy + R_), (cx - R_, cy)]
        st.line(sq1 + [sq1[0]], w, caps=False)
        st.line(sq2 + [sq2[0]], w, caps=False)
    star(R, 0.034)
    star(R * 0.84, 0.013)
    body = catmull([(0.0, -0.275), (0.13, -0.255), (0.17, -0.12), (0.165, 0.02), (0.12, 0.12), (0.0, 0.145),
                    (-0.12, 0.12), (-0.165, 0.02), (-0.17, -0.12), (-0.13, -0.255)], 10)
    cap = bezier((-0.12, -0.27), (-0.09, -0.38), (0.0, -0.395)) + bezier((0.0, -0.395), (0.09, -0.38), (0.12, -0.27))
    spike = [(-0.018, -0.39), (0.018, -0.39), (0.0, -0.455)]
    cone = [(-0.10, 0.135), (0.10, 0.135), (0.0, 0.27)]
    for shape in (body, cap, cone, spike):
        knock_outline(st, shape, 0.06)
        st.poly(shape, ink=False)
    st.line([(0, -0.62), (0, -0.47)], 0.04, ink=False)
    for shape in (body, cap, cone, spike):
        st.poly(shape)
    st.ring(0, -0.475, 0.02, 0.02, 0.013)
    for yk in np.arange(-0.60, -0.50, 0.033):
        st.ring(0, yk, 0.011, 0.015, 0.008)
    st.circle(0, 0.30, 0.024)
    st.line([(-0.15, -0.215), (0.15, -0.215)], 0.014, ink=False, caps=False)
    st.line([(-0.13, 0.085), (0.13, 0.085)], 0.014, ink=False, caps=False)
    for xk in (-0.085, 0.0, 0.085):
        st.line([(xk, -0.20), (xk * 0.9, 0.07)], 0.011, ink=False, caps=False)
    for row, yk in enumerate(np.arange(-0.17, 0.06, 0.045)):
        for xk in (-0.125, -0.042, 0.042, 0.125):
            if abs(xk) > 0.1 and (yk < -0.15 or yk > 0.03):
                continue
            s = 0.013
            st.poly([(xk, yk - s * 1.3), (xk + s, yk), (xk, yk + s * 1.3), (xk - s, yk)], ink=False)


def d_germany(st, rng):
    border(st)
    band_squares(st, 1, 1, 24, 0.016)
    st.rect(-0.56, 0.46, 0.56, 0.52)
    st.rect(-0.52, 0.40, 0.52, 0.46)
    for xc in (-0.425, -0.255, -0.085, 0.085, 0.255, 0.425):
        st.rect(xc - 0.036, -0.08, xc + 0.036, 0.40)
        st.rect(xc - 0.052, -0.12, xc + 0.052, -0.08)
        st.rect(xc - 0.05, 0.36, xc + 0.05, 0.40)
        st.line([(xc, -0.06), (xc, 0.34)], 0.012, ink=False, caps=False)
    st.rect(-0.56, -0.26, 0.56, -0.12)
    st.line([(-0.52, -0.19), (0.52, -0.19)], 0.014, ink=False, caps=False)
    for xc in np.arange(-0.48, 0.49, 0.08):
        st.line([(xc, -0.175), (xc, -0.135)], 0.012, ink=False, caps=False)
    st.rect(-0.34, -0.38, 0.34, -0.26)
    st.line([(-0.30, -0.32), (0.30, -0.32)], 0.012, ink=False, caps=False)
    st.rect(-0.14, -0.46, 0.14, -0.38)


def d_home(st, rng):
    jitter = rng.normal(0, 0.035, 24)
    st.ring(0.0, 0.0, 0.974, 0.962, 0.052)
    st.ring(0.0, 0.0, 0.835, 0.822, 0.022)
    band_dots(st, 1, 0.99, 24, 0.02, jitter=jitter)
    walls = [(-0.34, -0.06), (0.34, -0.06), (0.34, 0.50), (-0.34, 0.50)]
    st.line(walls + [walls[0]], 0.05)
    st.line([(-0.47, -0.02), (0.03, -0.51), (0.47, -0.02)], 0.05)
    st.poly([(0.19, -0.43), (0.29, -0.43), (0.29, -0.215), (0.19, -0.315)])
    st.circle(0, 0.215, 0.085)
    st.poly([(-0.045, 0.25), (0.045, 0.25), (0.10, 0.50), (-0.10, 0.50)])
    for sx in (-0.20, 0.20):
        sq = [(sx - 0.06, 0.04), (sx + 0.06, 0.04), (sx + 0.06, 0.16), (sx - 0.06, 0.16)]
        st.line(sq + [sq[0]], 0.026)
        st.line([(sx, 0.04), (sx, 0.16)], 0.014)
        st.line([(sx - 0.06, 0.10), (sx + 0.06, 0.10)], 0.014)
    st.line([(-0.56, 0.50), (0.56, 0.50)], 0.03)


def _xf(rot_deg=0.0, dx=0.0, dy=0.0, shear=0.0):
    a = math.radians(rot_deg)
    ca, sa = math.cos(a), math.sin(a)

    def f(x, y):
        x = x + shear * y
        return (x * ca - y * sa + dx, x * sa + y * ca + dy)
    return f


def render(design, rng, xf=None, **ink_kw):
    st = Stamp(xf)
    design(st, rng)
    return ink(st.hires(), rng, **ink_kw)


STAMPS = [
    ("stamp_uae", d_uae, "UAE: stylised dhow with lateen sail above water lines and a low skyline (vertical oval)."),
    ("stamp_egypt", d_egypt, "Egypt: pyramids with striped stone courses and a rayed sun disc (circle)."),
    ("stamp_france", d_france, "France: arched stone bridge over river lines with a lattice-tower hint (vertical oval)."),
    ("stamp_usa", d_usa, "USA: raised torch with a three-tongued flame (circle)."),
    ("stamp_japan", d_japan, "Japan: ribbed paper lantern hanging under a curved roofline (vertical oval)."),
    ("stamp_saudi", d_saudi, "Saudi Arabia: abstract tall tower with a parabolic arch opening and sky bridge (vertical oval)."),
    ("stamp_india", d_india, "India: domed pavilion with minarets reflected in water as broken lines (circle)."),
    ("stamp_brazil", d_brazil, "Brazil: carnival drum with crossed sticks and confetti specks (circle)."),
    ("stamp_uk", d_uk, "United Kingdom: clock face with hands at one minute to twelve (circle)."),
    ("stamp_morocco", d_morocco, "Morocco: interlaced 8-point star with a pierced hanging lantern (vertical oval)."),
    ("stamp_germany", d_germany, "Germany: gate of six columns with lintel and stepped attic (circle)."),
]

for _name, _fn, _purpose in STAMPS:
    def _make(fn=_fn, name=_name):
        def gen(rng):
            return {f"{name}.png": finish(render(fn, rng))}
        return gen
    graphic(_name, _purpose + " Dark-red ink, transparent background, no lettering.",
            "Vector shapes in stamp units -> 4x supersampled mask -> ink model (noisy-threshold rough edges, "
            "pressure gradient, blotches, dry patches, pin-hole voids, ink rim) -> box downsample.")(_make())


@graphic("stamp_home_forged",
         "The forged twelfth stamp: house outline with keyhole door, subtly wrong (crooked, double-printed with "
         "misregistration, slightly too-bright red).",
         "Same ink model as the other stamps; the design is drawn rotated/sheared, then printed a second time with "
         "an offset and rotation at lower density; colour #a8282a-ish instead of #7a1f1f.")
def stamp_home_forged(rng):
    a1 = render(d_home, rng, xf=_xf(3.5, 0.0, 0.0, 0.035), density=1.0, press=0.6)
    a2 = render(d_home, rng, xf=_xf(5.2, 0.030, 0.021, 0.035), density=0.58, press=3.6, voids=1.4)
    a = clamp01(1 - (1 - a1) * (1 - a2))
    return {"stamp_home_forged.png": finish(a, C((168, 40, 40)))}
