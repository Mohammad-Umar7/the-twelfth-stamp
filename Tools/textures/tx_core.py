"""Core helpers for The Twelfth Stamp procedural textures.

Everything is deterministic (fixed seeds derived from asset names with CRC32)
and, where a function says "periodic", the result tiles seamlessly in both axes:

* spectral() synthesises noise in the Fourier domain, so it is periodic by construction.
* voronoi() uses a periodic KD-tree (cKDTree boxsize=1), so cells wrap around the tile.
* blur()/warp()/height_to_normal() use wrap-around boundary modes.
* draw helpers can repeat shapes at +-W/+-H offsets so strokes cross the seam correctly.

Only numpy, scipy and Pillow are used. No external images.
"""
from __future__ import annotations

import math
import zlib

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import cKDTree

F32 = np.float32


# --------------------------------------------------------------------------- seeds
def seed_of(*parts) -> int:
    s = "|".join(str(p) for p in parts)
    return zlib.crc32(s.encode("utf-8")) & 0xFFFFFFFF


def rng_for(*parts) -> np.random.Generator:
    return np.random.default_rng(seed_of(*parts))


# --------------------------------------------------------------------------- math
def clamp01(x):
    return np.clip(x, 0.0, 1.0)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def norm01(a):
    lo, hi = float(a.min()), float(a.max())
    return ((a - lo) / (hi - lo + 1e-12)).astype(F32)


def nstd(a):
    a = a - a.mean()
    return (a / (a.std() + 1e-12)).astype(F32)


def uv_grid(h, w):
    """Pixel-centre coordinates in [0,1). Returns (u, v) with v increasing downwards."""
    v, u = np.mgrid[0:h, 0:w].astype(F32)
    return (u + 0.5) / w, (v + 0.5) / h


def px_grid(h, w):
    v, u = np.mgrid[0:h, 0:w].astype(F32)
    return u, v


# --------------------------------------------------------------------------- periodic noise
def _fgrid(h, w):
    s = max(h, w)
    fy = (np.fft.fftfreq(h) * s).astype(F32)
    fx = (np.fft.fftfreq(w) * s).astype(F32)
    return np.meshgrid(fx, fy)


def spectral(shape, rng, beta=2.0, fmin=0.0, fmax=None, elong=(1.0, 1.0), angle=0.0, band=None):
    """Periodic noise synthesised in the frequency domain (zero mean, unit std).

    beta  : power-spectrum exponent (amplitude ~ f^-beta/2). 0 = white, 2 = brownian/fBm-like.
    fmin  : soft high-pass (cycles per tile).   fmax: soft low-pass (cycles per tile).
    elong : frequency-axis weights (ex, ey). ex > 1 suppresses variation along x, i.e. streaks along x.
    angle : rotation of the anisotropy (radians).
    band  : (f0, bw) gaussian band-pass instead of a power law.
    """
    h, w = shape
    FX, FY = _fgrid(h, w)
    if angle:
        c, s = math.cos(angle), math.sin(angle)
        FX, FY = FX * c + FY * s, -FX * s + FY * c
    f = np.sqrt((FX * elong[0]) ** 2 + (FY * elong[1]) ** 2)
    if band is not None:
        f0, bw = band
        amp = np.exp(-((f - f0) / bw) ** 2)
    else:
        with np.errstate(divide="ignore"):
            amp = np.where(f > 0, f ** (-beta / 2.0), 0.0)
    amp = amp * (f > 0)
    if fmin and fmin > 0:
        amp = amp * (1.0 - np.exp(-((f / fmin) ** 4)))
    if fmax:
        amp = amp * np.exp(-((f / fmax) ** 2))
    ph = rng.standard_normal((h, w)) + 1j * rng.standard_normal((h, w))
    out = np.fft.ifft2(amp * ph).real
    return nstd(out)


def fbm(shape, rng, fmin=2, fmax=None, beta=2.0):
    return spectral(shape, rng, beta=beta, fmin=fmin, fmax=fmax)


def blur(a, sigma, wrap=True):
    mode = "wrap" if wrap else "reflect"
    if np.ndim(sigma) == 0 and sigma <= 0:
        return a
    if a.ndim == 3:
        return np.stack([ndimage.gaussian_filter(a[..., c], sigma, mode=mode) for c in range(a.shape[2])], -1)
    return ndimage.gaussian_filter(a, sigma, mode=mode)


def warp(a, dx, dy, order=1, wrap=True):
    """Sample a at (x+dx, y+dy). dx, dy in pixels. Wrap-around if wrap."""
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(F32)
    coords = np.array([yy + dy, xx + dx])
    mode = "grid-wrap" if wrap else "nearest"

    def one(ch):
        return ndimage.map_coordinates(ch, coords, order=order, mode=mode).astype(F32)

    if a.ndim == 3:
        return np.stack([one(a[..., c]) for c in range(a.shape[2])], -1)
    return one(a)


def tile_to(a, h, w, ox=0, oy=0):
    """Repeat a periodic texture to size (h, w) with an offset (seamless if a tiles)."""
    H, W = a.shape[:2]
    yi = (np.arange(h) + oy) % H
    xi = (np.arange(w) + ox) % W
    return a[yi][:, xi]


# --------------------------------------------------------------------------- periodic voronoi
def _wrap01(p):
    p = np.mod(p, 1.0)
    p[p >= 1.0] = 0.0
    return p


def relaxed_points(count, rng, iters=2, res=256):
    """Random points on the unit torus, Lloyd-relaxed for an even spread."""
    pts = rng.random((count, 2))
    for _ in range(iters):
        u, v = uv_grid(res, res)
        q = np.stack([u.ravel(), v.ravel()], 1).astype(np.float64)
        tree = cKDTree(pts, boxsize=1.0)
        _, idx = tree.query(q, k=1)
        d = q - pts[idx]
        d -= np.round(d)
        c = np.bincount(idx, minlength=count).astype(np.float64)
        sx = np.bincount(idx, d[:, 0], count)
        sy = np.bincount(idx, d[:, 1], count)
        ok = c > 0
        pts[ok, 0] += sx[ok] / c[ok]
        pts[ok, 1] += sy[ok] / c[ok]
        pts = _wrap01(pts)
    return pts


def voronoi(n, pts, du=None, dv=None):
    """Periodic Voronoi on an n x n tile. pts in [0,1)^2 (x, y).

    du/dv: optional periodic warp offsets (in tile units) applied to the lookup coordinates.
    Returns dict with f1, f2 (tile units), id, id2, edge (distance to nearest bisector, tile units),
    dx, dy (wrapped offset from the owning site, tile units).
    """
    pts = _wrap01(np.asarray(pts, dtype=np.float64).copy())
    u, v = uv_grid(n, n)
    if du is not None:
        u = u + du
        v = v + dv
    q = _wrap01(np.stack([u.ravel(), v.ravel()], 1).astype(np.float64))
    tree = cKDTree(pts, boxsize=1.0)
    d, idx = tree.query(q, k=2)
    d1 = q - pts[idx[:, 0]]
    d1 -= np.round(d1)
    d2 = q - pts[idx[:, 1]]
    d2 -= np.round(d2)
    sep = np.linalg.norm(d1 - d2, axis=1)
    edge = (np.einsum("ij,ij->i", d2, d2) - np.einsum("ij,ij->i", d1, d1)) / (2.0 * sep + 1e-12)
    R = lambda a: a.reshape(n, n).astype(F32)
    return dict(f1=R(d[:, 0]), f2=R(d[:, 1]), id=idx[:, 0].reshape(n, n), id2=idx[:, 1].reshape(n, n),
                edge=R(edge), dx=R(d1[:, 0]), dy=R(d1[:, 1]))


# --------------------------------------------------------------------------- normals
def height_to_normal(h, strength, wrap=True, pre_blur=0.0):
    """Tangent-space normal (Unity / OpenGL convention: +X right, +Y up = green) from a height field.

    Sobel derivatives (wrap-around when wrap=True). Image rows go downwards, so the Y component
    uses +dh/drow, which equals -dh/dv in texture space. strength scales the height (1.0 height
    unit = `strength` pixels).
    """
    h = h.astype(F32)
    if pre_blur > 0:
        h = blur(h, pre_blur, wrap)
    mode = "wrap" if wrap else "nearest"
    gx = ndimage.sobel(h, axis=1, mode=mode) / 8.0
    gy = ndimage.sobel(h, axis=0, mode=mode) / 8.0
    nx = -gx * strength
    ny = gy * strength
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + 1.0)
    return np.stack([nx * inv, ny * inv, inv], -1).astype(F32)


def normal_rgb(n):
    return to_u8(n * 0.5 + 0.5)


def normal_tilt_deg(n):
    return float(np.degrees(np.arccos(np.clip(n[..., 2], -1, 1))).mean())


# --------------------------------------------------------------------------- colour
def C(c):
    """Colour from '#rrggbb' or (r,g,b) 0-255 -> float32 array in [0,1] (sRGB encoded)."""
    if isinstance(c, str):
        s = c.lstrip("#")
        return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], F32) / 255.0
    return np.asarray(c, F32) / 255.0


def ramp(t, stops):
    """Gradient map. stops: list of (pos, colour)."""
    t = np.clip(t, 0.0, 1.0)
    pos = np.array([s[0] for s in stops], F32)
    cols = np.stack([C(s[1]) for s in stops])
    out = np.empty(t.shape + (3,), F32)
    for c in range(3):
        out[..., c] = np.interp(t, pos, cols[:, c])
    return out


def solid(h, w, c):
    return np.broadcast_to(C(c), (h, w, 3)).astype(F32).copy()


def mix(a, b, t):
    t = np.asarray(t, F32)
    if t.ndim == a.ndim - 1:
        t = t[..., None]
    return a + (b - a) * t


def lum(rgb):
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def saturate(rgb, s):
    y = lum(rgb)[..., None]
    return y + (rgb - y) * s


def to_u8(a):
    return (np.clip(a, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def srgb_to_lin(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def albedo_guard(rgb, lo=0.13, hi=0.86, ch_lo=0.03, ch_hi=0.94):
    """Soft-limit luminance to [lo, hi] (sRGB-encoded 0..1) and keep channels off pure black/white."""
    rgb = np.asarray(rgb, F32)
    y = lum(rgb)
    k0, k1 = lo + 0.05, hi - 0.05
    y2 = y.copy()
    m = y < k0
    y2[m] = lo + (k0 - lo) * np.exp((y[m] - k0) / (k0 - lo))
    m = y > k1
    y2[m] = hi - (hi - k1) * np.exp(-(y[m] - k1) / (hi - k1))
    scale = (y2 / np.maximum(y, 1e-4))[..., None]
    out = rgb * scale
    # if scaling pushed a channel over, pull it back keeping the others
    return np.clip(out, ch_lo, ch_hi).astype(F32)


# --------------------------------------------------------------------------- SDF helpers
def aa(sd, px=1.0):
    """Coverage from a signed distance in pixels (negative inside)."""
    return clamp01(0.5 - sd / px)


def sd_circle(x, y, cx, cy, r):
    return np.sqrt((x - cx) ** 2 + (y - cy) ** 2) - r


def sd_ellipse(x, y, cx, cy, rx, ry):
    """Approximate signed distance to an ellipse (first-order, good near the outline)."""
    X, Y = (x - cx), (y - cy)
    k = np.sqrt((X / rx) ** 2 + (Y / ry) ** 2) + 1e-9
    gx = X / (rx * rx * k)
    gy = Y / (ry * ry * k)
    return (k - 1.0) / (np.sqrt(gx * gx + gy * gy) + 1e-9)


def sd_box(x, y, cx, cy, hw, hh, r=0.0):
    qx = np.abs(x - cx) - (hw - r)
    qy = np.abs(y - cy) - (hh - r)
    ox = np.maximum(qx, 0)
    oy = np.maximum(qy, 0)
    return np.sqrt(ox * ox + oy * oy) + np.minimum(np.maximum(qx, qy), 0) - r


def iso_lines(f, spacing, width_px):
    """Anti-aliased lines along the iso-contours f = k*spacing (f in arbitrary units)."""
    gy, gx = np.gradient(f)
    g = np.sqrt(gx * gx + gy * gy) + 1e-6
    t = f / spacing
    d = np.abs(t - np.round(t)) * spacing / g
    return clamp01(width_px * 0.5 + 0.5 - d).astype(F32)


# --------------------------------------------------------------------------- supersampled drawing
class SS:
    """Supersampled 8-bit mask canvas drawn with Pillow; .cov() returns float coverage at target size."""

    def __init__(self, w, h, ss=4, wrap=False):
        self.w, self.h, self.ss, self.wrap = w, h, ss, wrap
        self.img = Image.new("L", (w * ss, h * ss), 0)
        self.d = ImageDraw.Draw(self.img)

    def _offsets(self, xs, ys):
        if not self.wrap:
            return [(0, 0)]
        x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
        offs = []
        for ox in (-self.w, 0, self.w):
            if x1 + ox < -4 or x0 + ox > self.w + 4:
                continue
            for oy in (-self.h, 0, self.h):
                if y1 + oy < -4 or y0 + oy > self.h + 4:
                    continue
                offs.append((ox, oy))
        return offs

    def _tp(self, pts, ox=0, oy=0):
        s = self.ss
        return [((x + ox) * s, (y + oy) * s) for x, y in pts]

    def poly(self, pts, fill=255):
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        for ox, oy in self._offsets(xs, ys):
            self.d.polygon(self._tp(pts, ox, oy), fill=fill)

    def line(self, pts, width, fill=255, round_caps=True):
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        wpx = max(1, int(round(width * self.ss)))
        for ox, oy in self._offsets(xs, ys):
            tp = self._tp(pts, ox, oy)
            self.d.line(tp, fill=fill, width=wpx, joint="curve")
            if round_caps and wpx > 2:
                r = wpx / 2.0
                for (x, y) in (tp[0], tp[-1]):
                    self.d.ellipse([x - r, y - r, x + r, y + r], fill=fill)

    def ellipse(self, cx, cy, rx, ry, fill=255, outline=None, width=0):
        s = self.ss
        for ox, oy in self._offsets([cx - rx, cx + rx], [cy - ry, cy + ry]):
            box = [(cx - rx + ox) * s, (cy - ry + oy) * s, (cx + rx + ox) * s, (cy + ry + oy) * s]
            if outline is not None:
                self.d.ellipse(box, fill=fill, outline=outline, width=max(1, int(round(width * s))))
            else:
                self.d.ellipse(box, fill=fill)

    def rect(self, x0, y0, x1, y1, fill=255, radius=0):
        s = self.ss
        for ox, oy in self._offsets([x0, x1], [y0, y1]):
            box = [(x0 + ox) * s, (y0 + oy) * s, (x1 + ox) * s - 1, (y1 + oy) * s - 1]
            if radius > 0:
                self.d.rounded_rectangle(box, radius=radius * s, fill=fill)
            else:
                self.d.rectangle(box, fill=fill)

    def hires(self):
        return np.asarray(self.img, F32) / 255.0

    def cov(self):
        a = np.asarray(self.img, F32) / 255.0
        return a.reshape(self.h, self.ss, self.w, self.ss).mean(axis=(1, 3)).astype(F32)


def downsample(a, k):
    """Box-filter downsample by integer factor k (2D or 3D arrays)."""
    h, w = a.shape[:2]
    if a.ndim == 2:
        return a.reshape(h // k, k, w // k, k).mean(axis=(1, 3)).astype(F32)
    return a.reshape(h // k, k, w // k, k, a.shape[2]).mean(axis=(1, 3)).astype(F32)


def rot(pts, ang, cx=0.0, cy=0.0):
    c, s = math.cos(ang), math.sin(ang)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def arc_pts(cx, cy, rx, ry, a0, a1, n=64):
    return [(cx + rx * math.cos(a0 + (a1 - a0) * i / (n - 1)), cy + ry * math.sin(a0 + (a1 - a0) * i / (n - 1)))
            for i in range(n)]


def bezier(p0, p1, p2, p3=None, n=48):
    pts = []
    for i in range(n):
        t = i / (n - 1)
        if p3 is None:
            x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
            y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
        else:
            x = (1 - t) ** 3 * p0[0] + 3 * (1 - t) ** 2 * t * p1[0] + 3 * (1 - t) * t * t * p2[0] + t ** 3 * p3[0]
            y = (1 - t) ** 3 * p0[1] + 3 * (1 - t) ** 2 * t * p1[1] + 3 * (1 - t) * t * t * p2[1] + t ** 3 * p3[1]
        pts.append((x, y))
    return pts


# --------------------------------------------------------------------------- crack / stroke generators
def crack_paths(rng, starts, steps=40, step_len=6.0, wander=0.35, branch_p=0.06, max_depth=3,
                stop=None, decay=0.92, w0=2.0):
    """Branching random-walk polylines. starts: list of (x, y, angle).
    Returns list of (points, width). stop(x, y) -> True halts a walk."""
    out = []
    stack = [(x, y, a, w0, 0, steps) for (x, y, a) in starts]
    while stack:
        x, y, a, w, depth, n = stack.pop()
        pts = [(x, y)]
        for _ in range(n):
            a += rng.normal(0, wander)
            x += math.cos(a) * step_len * rng.uniform(0.6, 1.4)
            y += math.sin(a) * step_len * rng.uniform(0.6, 1.4)
            pts.append((x, y))
            if stop is not None and stop(x, y):
                break
            if depth < max_depth and rng.random() < branch_p:
                stack.append((x, y, a + rng.choice([-1, 1]) * rng.uniform(0.4, 1.1), w * 0.65, depth + 1,
                              int(n * rng.uniform(0.3, 0.7))))
        out.append((pts, w))
    return out


# --------------------------------------------------------------------------- IO
def save_png(path, arr):
    """Save float [0,1] or uint8 arrays: (H,W) -> L, (H,W,3) -> RGB, (H,W,4) -> RGBA."""
    if isinstance(arr, Image.Image):
        img = arr
    else:
        a = arr if arr.dtype == np.uint8 else to_u8(arr)
        img = Image.fromarray(np.ascontiguousarray(a))
    img.save(path, format="PNG", compress_level=9)
    return img


def rgba(rgb, alpha):
    return np.concatenate([np.asarray(rgb, F32), np.asarray(alpha, F32)[..., None]], -1)


def over(dst, src, a):
    a = np.asarray(a, F32)
    if a.ndim == dst.ndim - 1:
        a = a[..., None]
    return dst * (1.0 - a) + src * a


def shade_lit(albedo, normal, light=(-0.45, 0.55, 0.70), ambient=0.30):
    """Quick lit preview for the contact sheet: Lambert with a light from the upper left."""
    L = np.array(light, F32)
    L /= np.linalg.norm(L)
    nd = np.clip((normal * L).sum(-1), 0, 1)
    flat = L[2]
    k = ambient + (1 - ambient) * nd / flat
    return np.clip(albedo * k[..., None], 0, 1)
