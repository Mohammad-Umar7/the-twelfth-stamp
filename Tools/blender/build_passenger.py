"""
Passenger_Parts.fbx (reference C) - the enemy, as separate rigid parts (no armature).

Blender axes: front -Y, up +Z, metres. Each part's origin is its joint:
  limbs hang along -Z from their joint, Hips/Chest/Head extend +Z, Foot extends forward (-Y).
Parts are laid out in the file as one assembled 2.15 m figure (left arm + left leg only;
the game spawns/mirrors its own copies). Every part is mirror-symmetric about its local YZ plane,
so one mesh serves both sides.

Materials: TS_PassengerGlass (smoked glass plates/body), TS_BronzeSeam (plate rims, seams,
fracture strips, rings), TS_FacePlate (empty face visor, chest slot cavity).
"""
import sys
import os
import math
import random

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402

GLASS, SEAM, FACE = 0, 1, 2
MATS = ["TS_PassengerGlass", "TS_BronzeSeam", "TS_FacePlate"]
D = math.radians


# ---------------------------------------------------------------------------- curves
def cr(ts, vs):
    """Catmull-Rom interpolation through (ts, vs); clamped outside."""
    n = len(ts)

    def f(t):
        if t <= ts[0]:
            return vs[0]
        if t >= ts[-1]:
            return vs[-1]
        i = max(k for k in range(n - 1) if ts[k] <= t)
        t0, t1 = ts[i], ts[i + 1]
        u = (t - t0) / (t1 - t0)
        p0 = vs[i - 1] if i > 0 else vs[i] - (vs[i + 1] - vs[i])
        p1, p2 = vs[i], vs[i + 1]
        p3 = vs[i + 2] if i + 2 < n else vs[i + 1] + (vs[i + 1] - vs[i])
        return 0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u
                      + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3)
    return f


def const(v):
    return lambda t: v


class GenCyl:
    """Generalised cylinder: superellipse cross-section along an axis.
    axis 'Z': section in XY (theta 0 = +X, 90 = +Y/back, 270 = -Y/front), position z(t).
    axis 'Y': section in XZ (theta 0 = +X, 90 = +Z/top), position y(t)."""

    def __init__(self, ts, pos, rx, ry, c1=None, c2=None, e=None, axis="Z"):
        self.fp = cr(ts, pos)
        self.frx = cr(ts, rx)
        self.fry = cr(ts, ry)
        self.f1 = cr(ts, c1) if c1 else const(0.0)
        self.f2 = cr(ts, c2) if c2 else const(0.0)
        self.fe = cr(ts, e) if isinstance(e, list) else const(e or 2.0)
        self.axis = axis

    def center(self, t):
        if self.axis == "Z":
            return Vector((self.f1(t), self.f2(t), self.fp(t)))
        return Vector((self.f1(t), self.fp(t), self.f2(t)))

    def pt(self, t, th, off=0.0):
        rx = max(self.frx(t) + off, 1e-5)
        ry = max(self.fry(t) + off, 1e-5)
        e = self.fe(t)
        c, s = math.cos(th), math.sin(th)
        a = rx * math.copysign(abs(c) ** (2 / e), c)
        b = ry * math.copysign(abs(s) ** (2 / e), s)
        if self.axis == "Z":
            return Vector((self.f1(t) + a, self.f2(t) + b, self.fp(t)))
        return Vector((self.f1(t) + a, self.fp(t), self.f2(t) + b))

    def normal(self, t, th, off=0.0):
        h = 1e-3
        dt = self.pt(min(t + h, 1.0), th, off) - self.pt(max(t - h, 0.0), th, off)
        dth = self.pt(t, th + h, off) - self.pt(t, th - h, off)
        n = dth.cross(dt)
        if n.length < 1e-12:
            n = self.pt(t, th, off) - self.center(t)
        rad = self.pt(t, th, off) - self.center(t)
        if n.dot(rad) < 0:
            n = -n
        return n.normalized()


# ---------------------------------------------------------------------------- builders
def core(mb, g, nt, nth, t0=0.0, t1=1.0, mat=GLASS, phase=0.0, off=0.0):
    rings = []
    for i in range(nt + 1):
        t = t0 + (t1 - t0) * i / nt
        rings.append([mb.v(g.pt(t, phase + 2 * math.pi * k / nth, off)) for k in range(nth)])
    mb.bridge(rings, closed_ring=True, mat=mat)
    mb.cap(rings[0], mat, fan_center=g.center(t0))
    mb.cap(rings[-1], mat, fan_center=g.center(t1))
    return rings


def spaced(n, e):
    """[0, e, ...n interior steps..., 1-e, 1]"""
    inner = [e + (1 - 2 * e) * k / n for k in range(n + 1)]
    return [0.0] + inner + [1.0]


def plate(mb, g, pm, off_in, off_out, nu, nv, frac=0, rng=None, seam_w=0.0035, frac_w=0.0009, all_seam=False):
    """Layered plate on surface g. pm(u, v) -> (t, theta). Bronze rolled rim, smoked glass face."""
    # physical size estimate -> rim width as a parameter fraction
    def P(u, v, off=off_out):
        t, th = pm(u, v)
        return g.pt(t, th, off)
    wu = sum((P((k + 1) / 8, 0.5) - P(k / 8, 0.5)).length for k in range(8))
    wv = sum((P(0.5, (k + 1) / 8) - P(0.5, k / 8)).length for k in range(8))
    eu = min(0.2, seam_w / max(wu, 1e-4))
    ev = min(0.2, seam_w / max(wv, 1e-4))
    us = spaced(nu, eu)
    vs = spaced(nv, ev)
    nU, nV = len(us), len(vs)
    lift = off_out - off_in

    def off_at(i, j):
        if i in (0, nU - 1) or j in (0, nV - 1):
            return off_in + lift * 0.3
        if i in (1, nU - 2) or j in (1, nV - 2):
            return off_in + lift * 0.9
        return off_out
    G = [[mb.v(P(us[i], vs[j], off_at(i, j))) for i in range(nU)] for j in range(nV)]
    for j in range(nV - 1):
        for i in range(nU - 1):
            rim = i in (0, nU - 2) or j in (0, nV - 2)
            mb.f((G[j][i], G[j][i + 1], G[j + 1][i + 1], G[j + 1][i]), SEAM if (rim or all_seam) else GLASS)
    # boundary loop (outer) and the matching inner loop sunk below the surface
    loop = [(i, 0) for i in range(nU)] + [(nU - 1, j) for j in range(1, nV)] + \
           [(i, nV - 1) for i in range(nU - 2, -1, -1)] + [(0, j) for j in range(nV - 2, 0, -1)]
    outer = [G[j][i] for (i, j) in loop]
    inner = [mb.v(P(us[i], vs[j], off_in - 0.003)) for (i, j) in loop]
    for k in range(len(loop)):
        k1 = (k + 1) % len(loop)
        mb.f((outer[k], outer[k1], inner[k1], inner[k]), SEAM)
    # inner face: fan from the sunk centre
    t, th = pm(0.5, 0.5)
    mb.cap(inner, GLASS, flip=True, fan_center=g.pt(t, th, off_in - 0.006))
    # fracture lines
    if frac and rng is not None:
        for _ in range(frac):
            fracture(mb, g, pm, off_out + 0.00025, rng, frac_w, (eu, ev))


def fracture(mb, g, pm, off, rng, w, margins):
    eu, ev = margins
    u = rng.uniform(0.15, 0.85)
    v = rng.uniform(0.15, 0.85)
    ang = rng.uniform(0, 2 * math.pi)
    n = rng.randint(6, 11)
    step = rng.uniform(0.035, 0.06)
    pts, nrm = [], []
    for k in range(n):
        uu = min(1 - 2.5 * eu, max(2.5 * eu, u))
        vv = min(1 - 2.5 * ev, max(2.5 * ev, v))
        t, th = pm(uu, vv)
        pts.append(g.pt(t, th, off))
        nrm.append(g.normal(t, th, off))
        ang += rng.uniform(-0.55, 0.55)
        u += math.cos(ang) * step
        v += math.sin(ang) * step
        if not (0.0 < u < 1.0 and 0.0 < v < 1.0):
            break
    if len(pts) >= 3:
        strip(mb, pts, nrm, w, 0.00035, SEAM, taper=True)


def strip(mb, pts, nrm, w, h, mat, closed=False, taper=False):
    """Flat raised strip that follows a surface (pts with surface normals)."""
    n = len(pts)
    prof = [(-w / 2, -h * 0.6), (w / 2, -h * 0.6), (w * 0.28, h), (-w * 0.28, h)]
    rings = []
    for i in range(n):
        if closed:
            T = pts[(i + 1) % n] - pts[i - 1]
        else:
            T = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        T.normalize()
        N = nrm[i]
        B = N.cross(T).normalized()
        N2 = T.cross(B).normalized()
        k = 1.0
        if taper and not closed:
            k = 0.35 + 0.65 * math.sin(math.pi * i / (n - 1)) if n > 1 else 1.0
        rings.append([mb.v(pts[i] + B * (a * k) + N2 * b) for (a, b) in prof])
    mb.bridge(rings, closed_ring=True, closed_path=closed, mat=mat)
    if not closed:
        mb.cap(rings[0], mat, flip=True)
        mb.cap(rings[-1], mat)


def surface_curve(g, params, off):
    pts = [g.pt(t, th, off) for (t, th) in params]
    nrm = [g.normal(t, th, off) for (t, th) in params]
    return pts, nrm


def ball(mb, center, r, mat=GLASS, segs=16, rings=10, sz=1.0):
    prof = []
    for k in range(rings + 1):
        a = -math.pi / 2 + math.pi * k / rings
        prof.append((r * math.cos(a) if 0 < k < rings else 0.0, r * math.sin(a) * sz))
    L.lathe(mb, prof, segs=segs, mat=mat, center=center)


def torus(mb, center, R, r, axis="Z", segs=28, tsegs=6, mat=SEAM, sx=1.0, sy=1.0, flat=1.0):
    c = Vector(center)
    path = []
    for k in range(segs):
        a = 2 * math.pi * k / segs
        x, y = R * sx * math.cos(a), R * sy * math.sin(a)
        if axis == "Z":
            path.append(c + Vector((x, y, 0)))
        elif axis == "X":
            path.append(c + Vector((0, x, y)))
        else:
            path.append(c + Vector((x, 0, y)))
    fb = {"Z": (0, 0, 1), "X": (1, 0, 0), "Y": (0, 1, 0)}[axis]
    prof = [(r * math.cos(2 * math.pi * k / tsegs), r * flat * math.sin(2 * math.pi * k / tsegs)) for k in range(tsegs)]
    L.sweep(mb, path, prof, mat=mat, closed_path=True, caps=False, fixed_binormal=fb)


def section_ring(mb, g, t, off, w, h, nth=40, mat=SEAM):
    """Band that follows the cross-section of g at t (belts, ankle/wrist rings)."""
    params = [(t, 2 * math.pi * k / nth) for k in range(nth)]
    pts, nrm = surface_curve(g, params, off)
    strip(mb, pts, nrm, w, h, mat, closed=True)


def tpm(t0, t1, a0, a1, tb=None, tt=None):
    """Plate parameter map: u along theta a0..a1 (degrees), v along t.
    tb/tt: optional functions of u giving the bottom/top t (for V or slanted edges)."""
    def pm(u, v):
        lo = tb(u) if tb else t0
        hi = tt(u) if tt else t1
        return (lo + (hi - lo) * v, D(a0 + (a1 - a0) * u))
    return pm


def finish_part(ob, angle=38.0):
    L.cleanup(ob, 1e-7)
    L.shade(ob, angle, True)
    L.add_triangulate(ob, 5)
    L.add_weighted_normal(ob)
    L.apply_modifiers(ob)
    L.uv_smart(ob, angle=55.0)
    return ob


def sym_plates(mb, g, specs, rng, nu=7, nv=10, frac=2):
    for s in specs:
        plate(mb, g, s[0], s[1], s[2], s[3] if len(s) > 3 else nu, s[4] if len(s) > 4 else nv,
              frac=frac, rng=rng)


# ---------------------------------------------------------------------------- limbs
def limb_plates(g, rng, t0, t1, front=(234, 306), back=(56, 124), side=36, mb=None, nv=10, frac=2,
                lift=(0.0015, 0.0075)):
    a, b = lift
    sym_plates(mb, g, [
        (tpm(t0, t1, front[0], front[1]), a, b, 7, nv),
        (tpm(t0 + 0.02, t1 - 0.03, back[0], back[1]), a, b, 7, nv),
        (tpm(t0 + 0.04, t1 - 0.02, -side, side), a, b, 6, nv),
        (tpm(t0 + 0.04, t1 - 0.02, 180 - side, 180 + side), a, b, 6, nv),
    ], rng, frac=frac)


def build_thigh(rng):
    mb = L.MB()
    Lz = 0.50
    ts = [0.0, 0.08, 0.3, 0.55, 0.8, 0.95, 1.0]
    g = GenCyl(ts, [-Lz * t for t in ts], [0.030, 0.058, 0.060, 0.054, 0.045, 0.040, 0.026],
               [0.032, 0.063, 0.068, 0.060, 0.048, 0.042, 0.028],
               c2=[0.0, -0.003, -0.008, -0.006, -0.002, 0.0, 0.0], e=2.15)
    core(mb, g, 18, 22, 0.0, 1.0)
    ball(mb, (0, 0, 0), 0.05)
    torus(mb, (0, 0, -0.035), 0.066, 0.0035, sy=1.06)
    limb_plates(g, rng, 0.09, 0.93, mb=mb, nv=11)
    # layered overlays: V-cut upper thigh guard and knee guard
    plate(mb, g, tpm(0.07, 0.4, 244, 296, tt=lambda u: 0.40 - 0.07 * (2 * u - 1) ** 2), 0.0075, 0.013, 6, 6,
          frac=2, rng=rng)
    plate(mb, g, tpm(0.84, 0.97, 248, 292), 0.0075, 0.012, 5, 3, frac=1, rng=rng)
    return mb.build("Part_Thigh", MATS)


def build_shin(rng):
    mb = L.MB()
    Lz = 0.52
    ts = [0.0, 0.07, 0.25, 0.5, 0.75, 0.93, 1.0]
    g = GenCyl(ts, [-Lz * t for t in ts], [0.026, 0.045, 0.048, 0.042, 0.033, 0.028, 0.020],
               [0.028, 0.048, 0.058, 0.050, 0.037, 0.030, 0.022],
               c2=[0.0, 0.003, 0.012, 0.010, 0.004, 0.0, 0.0], e=2.1)
    core(mb, g, 18, 20, 0.0, 1.0)
    ball(mb, (0, 0, 0), 0.042)
    # knee cap: shallow dome facing front with a bronze rim
    st = len(mb.verts)
    L.lathe(mb, [(0, 0.017), (0.016, 0.014), (0.028, 0.008), (0.036, 0.0), (0.036, -0.004), (0.0, -0.006)],
            segs=20, mat=GLASS, axis="-Y")
    L.transform_mb_range(mb, st, Matrix.Translation(Vector((0, -0.033, -0.012))))
    torus(mb, (0, -0.034, -0.012), 0.036, 0.0028, axis="Y", segs=24)
    limb_plates(g, rng, 0.12, 0.93, front=(238, 302), mb=mb, nv=11)
    plate(mb, g, tpm(0.16, 0.55, 250, 290, tt=lambda u: 0.55 - 0.1 * abs(2 * u - 1)), 0.0075, 0.012, 5, 6,
          frac=1, rng=rng)
    section_ring(mb, g, 0.945, 0.0035, 0.006, 0.003)
    section_ring(mb, g, 0.975, 0.0025, 0.004, 0.0025)
    return mb.build("Part_Shin", MATS)


def build_upper_arm(rng):
    mb = L.MB()
    Lz = 0.36
    ts = [0.0, 0.1, 0.35, 0.6, 0.85, 1.0]
    g = GenCyl(ts, [-Lz * t for t in ts], [0.024, 0.042, 0.043, 0.039, 0.034, 0.022],
               [0.026, 0.044, 0.048, 0.042, 0.036, 0.024],
               c2=[0.0, -0.002, -0.006, -0.004, 0.0, 0.0], e=2.1)
    core(mb, g, 14, 20, 0.0, 1.0)
    ball(mb, (0, 0, 0), 0.044)
    # shoulder cap: domed shell over the joint
    cap = GenCyl([0.0, 0.3, 0.6, 0.85, 1.0], [0.054, 0.047, 0.02, -0.015, -0.058],
                 [0.0, 0.034, 0.05, 0.052, 0.049], [0.0, 0.032, 0.048, 0.05, 0.047], e=2.0)
    plate(mb, cap, lambda u, v: (0.02 + 0.98 * v, D(92.0 + 356.0 * u)), 0.0, 0.006, 16, 6, frac=2, rng=rng)
    torus(mb, (0, 0, -0.056), 0.049, 0.0028, segs=26)
    limb_plates(g, rng, 0.24, 0.92, front=(236, 304), mb=mb, nv=8)
    return mb.build("Part_UpperArm", MATS)


def build_forearm(rng):
    mb = L.MB()
    Lz = 0.36
    ts = [0.0, 0.08, 0.3, 0.6, 0.85, 1.0]
    g = GenCyl(ts, [-Lz * t for t in ts], [0.020, 0.036, 0.037, 0.031, 0.025, 0.018],
               [0.020, 0.035, 0.035, 0.028, 0.021, 0.016], e=2.1)
    core(mb, g, 14, 18, 0.0, 1.0)
    ball(mb, (0, 0, 0), 0.035)
    torus(mb, (0, 0, -0.03), 0.04, 0.003, segs=24)
    limb_plates(g, rng, 0.12, 0.9, front=(238, 302), side=38, mb=mb, nv=9)
    section_ring(mb, g, 0.925, 0.003, 0.005, 0.0028)
    section_ring(mb, g, 0.965, 0.0025, 0.004, 0.0025)
    return mb.build("Part_Forearm", MATS)


KNUCKLES = [-0.0285, -0.0095, 0.0095, 0.0285]
PALM_L = 0.09


def build_hand(rng):
    mb = L.MB()
    ts = [0.0, 0.2, 0.5, 0.85, 1.0]
    g = GenCyl(ts, [-0.012 - (PALM_L - 0.012) * t for t in ts], [0.022, 0.030, 0.034, 0.035, 0.033],
               [0.012, 0.014, 0.015, 0.014, 0.012], e=3.2)
    core(mb, g, 8, 20, 0.0, 1.0)
    ball(mb, (0, 0, 0), 0.02)
    torus(mb, (0, 0, -0.012), 0.026, 0.0025, sy=0.62, segs=24)
    # back-of-hand plate (front, -Y) and palm plate (+Y)
    plate(mb, g, tpm(0.12, 0.9, 205, 335), 0.0012, 0.0055, 8, 6, frac=2, rng=rng)
    plate(mb, g, tpm(0.15, 0.88, 30, 150), 0.0012, 0.004, 7, 5, frac=1, rng=rng)
    # knuckle bar (fingers hinge here) with 4 knuckle bosses
    L.lathe(mb, [(0, -0.036), (0.0045, -0.0355), (0.0055, -0.034), (0.0055, 0.034), (0.0045, 0.0355), (0, 0.036)],
            segs=10, mat=SEAM, axis="X", center=(0, 0, -PALM_L))
    for x in KNUCKLES:
        L.lathe(mb, [(0, -0.0068), (0.0068, -0.006), (0.0075, 0.0), (0.0068, 0.006), (0, 0.0068)], segs=10,
                mat=GLASS, axis="X", center=(x, 0, -PALM_L))
    return mb.build("Part_Hand", MATS)


def build_finger_tag():
    mb = L.MB()
    W, LEN, TH = 0.014, 0.15, 0.0042
    r = W / 2
    # tag outline in (x, z): chamfered top, rounded tip
    pts = [(-r, -0.0055), (-r + 0.0025, -0.003), (r - 0.0025, -0.003), (r, -0.0055)]
    for k in range(9):
        a = math.radians(0 - 180 * k / 8)
        pts.append((r * math.cos(a), -LEN + r + r * math.sin(a)))
    pts = L.dedupe2d(pts)
    pts = L.ensure_ccw([(x, z) for (x, z) in pts])
    basis = (Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0)))
    L.prism(mb, pts, -TH / 2, TH / 2, mat=GLASS, basis=basis)
    tag = mb.build("TagBody", MATS)
    L.add_bevel(tag, 0.0007, 1, 30.0)
    L.apply_modifiers(tag)
    # punched hole near the tip
    cm = L.MB()
    L.lathe(cm, [(0.0021, -0.01), (0.0021, 0.01)], segs=12, mat=0, axis="Y", center=(0, 0, -LEN + 0.0135))
    cut = cm.build("HoleCut", MATS)
    L.boolean(tag, cut, "DIFFERENCE")
    # bronze edge frame, grommet, joint bands, knuckle barrel
    fm = L.MB()
    rim = L.resample(L.offset2d(pts, 0.0002), 30, closed=True)
    path = [Vector((x, 0, z)) for (x, z) in rim]
    L.sweep(fm, path, [(-0.0008, -TH / 2 - 0.0005), (0.0008, -TH / 2 - 0.0005), (0.0008, TH / 2 + 0.0005), (-0.0008, TH / 2 + 0.0005)], mat=SEAM, closed_path=True, caps=False,
            fixed_binormal=(0, 1, 0))
    for yy in (-TH / 2 - 0.0002, TH / 2 + 0.0002):
        torus(fm, (0, yy, -LEN + 0.0135), 0.0029, 0.0008, axis="Y", segs=12, tsegs=4)
    for zc in (-0.052, -0.1):
        for yy in (-TH / 2 - 0.0001, TH / 2 + 0.0001):
            sgn = -1 if yy < 0 else 1
            L.lathe(fm, [(0, 0.0007 * sgn), (0.0012, 0.0005 * sgn), (0.0014, 0.0), (0.0014, -0.0004 * sgn), (0, -0.0004 * sgn)],
                    segs=6, mat=SEAM, axis="Y", center=(0, yy, zc))
        L.box(fm, (W + 0.001, TH + 0.0006, 0.0011), center=(0, 0, zc + 0.0035), mat=SEAM)
    L.lathe(fm, [(0, -0.0078), (0.0034, -0.0075), (0.0038, -0.006), (0.0038, 0.006), (0.0034, 0.0075),
                 (0, 0.0078)], segs=8, mat=SEAM, axis="X", center=(0, 0, 0))
    frame = fm.build("TagFrame", MATS)
    ob = L.join([tag, frame], "Part_FingerTag")
    return ob


# ---------------------------------------------------------------------------- foot
def build_foot(rng):
    mb = L.MB()
    # loft along Y from heel (+) to toe (-); section in XZ
    ys = [0.062, 0.055, 0.035, 0.0, -0.045, -0.09, -0.13, -0.158, -0.172]
    ts = [(ys[0] - y) / (ys[0] - ys[-1]) for y in ys]
    tops = [-0.058, -0.04, -0.012, -0.004, -0.03, -0.052, -0.07, -0.082, -0.09]
    bots = [-0.094, -0.098, -0.1, -0.1, -0.1, -0.1, -0.1, -0.099, -0.096]
    half_w = [0.012, 0.024, 0.032, 0.036, 0.039, 0.040, 0.034, 0.022, 0.006]
    cz = [(a + b) / 2 for a, b in zip(tops, bots)]
    hz = [(a - b) / 2 for a, b in zip(tops, bots)]
    g = GenCyl(ts, ys, half_w, hz, c2=cz, e=[2.6, 3.0, 3.2, 3.4, 3.4, 3.4, 3.2, 2.8, 2.4], axis="Y")
    core(mb, g, 16, 20, 0.0, 1.0)
    ball(mb, (0, 0, 0), 0.03)
    torus(mb, (0, 0, -0.012), 0.034, 0.003, segs=24)
    # instep (top), heel, sides; theta 90 = top
    plate(mb, g, tpm(0.22, 0.93, 35, 145), 0.0012, 0.006, 7, 9, frac=2, rng=rng)
    plate(mb, g, tpm(0.0, 0.2, 20, 160, tt=lambda u: 0.2 + 0.05 * (1 - abs(2 * u - 1))), 0.0012, 0.006, 6, 3,
          frac=1, rng=rng)
    plate(mb, g, tpm(0.05, 0.9, -28, 25), 0.0012, 0.005, 4, 9, frac=1, rng=rng)
    plate(mb, g, tpm(0.05, 0.9, 155, 208), 0.0012, 0.005, 4, 9, frac=1, rng=rng)
    return mb.build("Part_Foot", MATS)


# ---------------------------------------------------------------------------- hips
def build_hips(rng):
    mb = L.MB()
    zs = [-0.064, -0.045, -0.02, 0.02, 0.08, 0.15, 0.23, 0.28]
    ts = [(z - zs[0]) / (zs[-1] - zs[0]) for z in zs]
    g = GenCyl(ts, zs, [0.03, 0.07, 0.106, 0.128, 0.135, 0.128, 0.114, 0.109],
               [0.036, 0.058, 0.076, 0.088, 0.092, 0.088, 0.079, 0.075],
               c2=[0.0, 0.0, 0.004, 0.006, 0.006, 0.004, 0.002, 0.002], e=2.4)
    core(mb, g, 16, 28, 0.0, 1.0)
    # front V (brief) plate, back plates (glutes), sides
    plate(mb, g, tpm(0.0, 0.0, 236, 304, tb=lambda u: 0.05 + 0.3 * abs(2 * u - 1) ** 1.2,
                     tt=lambda u: 0.62 - 0.06 * abs(2 * u - 1)), 0.0015, 0.0085, 7, 8, frac=2, rng=rng)
    plate(mb, g, tpm(0.6, 0.86, 228, 312), 0.0015, 0.0075, 8, 4, frac=1, rng=rng)
    plate(mb, g, tpm(0.3, 0.84, 200, 232, tb=lambda u: 0.42 - 0.12 * u), 0.004, 0.0105, 4, 7, frac=1, rng=rng)
    plate(mb, g, tpm(0.3, 0.84, 308, 340, tb=lambda u: 0.30 + 0.12 * u), 0.004, 0.0105, 4, 7, frac=1, rng=rng)
    plate(mb, g, tpm(0.08, 0.86, 30, 88, tb=lambda u: 0.08 + 0.25 * (1 - u)), 0.0015, 0.008, 7, 9, frac=2, rng=rng)
    plate(mb, g, tpm(0.08, 0.86, 92, 150, tb=lambda u: 0.08 + 0.25 * u), 0.0015, 0.008, 7, 9, frac=2, rng=rng)
    plate(mb, g, tpm(0.42, 0.86, -24, 24), 0.0015, 0.007, 5, 6, frac=1, rng=rng)
    plate(mb, g, tpm(0.42, 0.86, 156, 204), 0.0015, 0.007, 5, 6, frac=1, rng=rng)
    # waist belt and hip sockets
    section_ring(mb, g, 0.93, 0.004, 0.009, 0.0035, nth=48)
    for sx in (-1, 1):
        torus(mb, (sx * 0.095, 0, -0.005), 0.05, 0.0035, axis="Z", segs=24)
    return mb.build("Part_Hips", MATS)


# ---------------------------------------------------------------------------- chest
SLOT = dict(w=0.13, h=0.02, z=0.335, depth=0.05)


def chest_body():
    zs = [0.0, 0.06, 0.15, 0.24, 0.30, 0.35, 0.385, 0.408, 0.425]
    ts = [(z - zs[0]) / (zs[-1] - zs[0]) for z in zs]
    g = GenCyl(ts, zs, [0.110, 0.114, 0.132, 0.158, 0.176, 0.184, 0.166, 0.105, 0.045],
               [0.066, 0.069, 0.076, 0.083, 0.084, 0.078, 0.067, 0.054, 0.04],
               c2=[0.002, 0.002, -0.001, -0.005, -0.006, -0.004, 0.0, 0.004, 0.008],
               e=[2.3, 2.3, 2.5, 2.8, 3.0, 2.9, 2.5, 2.2, 2.0])
    return g, ts, zs


def t_of_z(ts, zs, z):
    for i in range(len(zs) - 1):
        if zs[i] <= z <= zs[i + 1]:
            return ts[i] + (ts[i + 1] - ts[i]) * (z - zs[i]) / (zs[i + 1] - zs[i])
    return ts[-1]


def theta_front_of_x(g, t, x, off):
    """theta on the front half where the surface reaches x."""
    rx = g.frx(t) + off
    e = g.fe(t)
    c = math.copysign(min(1.0, abs(x / rx)) ** (e / 2), x)
    return 2 * math.pi - math.acos(c)


def build_chest(rng):
    g, ts, zs = chest_body()
    # core with a slot pocket
    cm = L.MB()
    core(cm, g, 22, 32, 0.0, 1.0)
    body = cm.build("ChestCore", MATS)
    sm = L.MB()
    L.box(sm, (SLOT["w"], 0.3, SLOT["h"]), center=(0, -0.15 - 0.098 + SLOT["depth"], SLOT["z"]), mat=FACE)
    L.boolean(body, sm.build("SlotCut", MATS), "DIFFERENCE")
    # breastplate (one V-bottomed shield) with the slot cut through it
    pm_ = L.MB()
    plate(pm_, g, tpm(0.0, 0.0, 206, 334, tb=lambda u: 0.36 + 0.30 * abs(2 * u - 1) ** 1.1,
                      tt=lambda u: 0.9 - 0.04 * abs(2 * u - 1)), 0.0015, 0.011, 14, 12, frac=3, rng=rng)
    bp = pm_.build("Breastplate", MATS)
    sm = L.MB()
    L.box(sm, (SLOT["w"], 0.3, SLOT["h"]), center=(0, -0.15 - 0.098 + SLOT["depth"], SLOT["z"]), mat=FACE)
    L.boolean(bp, sm.build("SlotCut2", MATS), "DIFFERENCE")
    # everything else
    mb = L.MB()
    # bronze frame around the slot, following the breastplate surface
    off = 0.0105
    tz0 = t_of_z(ts, zs, SLOT["z"] - SLOT["h"] / 2 - 0.007)
    tz1 = t_of_z(ts, zs, SLOT["z"] + SLOT["h"] / 2 + 0.007)
    ti0 = t_of_z(ts, zs, SLOT["z"] - SLOT["h"] / 2)
    ti1 = t_of_z(ts, zs, SLOT["z"] + SLOT["h"] / 2)
    xo, xi = SLOT["w"] / 2 + 0.008, SLOT["w"] / 2

    def fr(xa, xb, ta, tb_):
        def pm(u, v):
            t = ta + (tb_ - ta) * v
            x = xa + (xb - xa) * u
            return (t, theta_front_of_x(g, t, x, off))
        return pm
    # side pieces run the full height; top/bottom pieces overlap them (no shared edges)
    for (xa, xb, ta, tb_) in ((-xi - 0.003, xi + 0.003, tz0, ti0), (-xi - 0.003, xi + 0.003, ti1, tz1),
                              (-xo, -xi, tz0, tz1), (xi, xo, tz0, tz1)):
        plate(mb, g, fr(xa, xb, ta, tb_), off - 0.002, off + 0.0035, 10 if xb - xa > 0.05 else 2, 2,
              seam_w=0.002, all_seam=True)
    # segmented abdomen bands (shingled upward)
    for k, (ta, tb_, oo) in enumerate(((0.035, 0.17, 0.0), (0.14, 0.28, 0.002), (0.25, 0.4, 0.004))):
        plate(mb, g, tpm(ta, tb_, 212, 328), 0.0015 + oo, 0.0075 + oo, 10, 4, frac=1, rng=rng)
    # flank (rib) plates
    plate(mb, g, tpm(0.06, 0.78, -30, 30, tt=lambda u: 0.78 - 0.08 * u), 0.0015, 0.0075, 6, 10, frac=2, rng=rng)
    plate(mb, g, tpm(0.06, 0.78, 150, 210, tt=lambda u: 0.70 + 0.08 * u), 0.0015, 0.0075, 6, 10, frac=2, rng=rng)
    # back: two long plates either side of the spine, shoulder blades layered on top
    plate(mb, g, tpm(0.04, 0.9, 34, 87), 0.0015, 0.0075, 7, 12, frac=3, rng=rng)
    plate(mb, g, tpm(0.04, 0.9, 93, 146), 0.0015, 0.0075, 7, 12, frac=3, rng=rng)
    plate(mb, g, tpm(0.58, 0.88, 46, 84, tb=lambda u: 0.62 - 0.06 * u), 0.0075, 0.0135, 5, 5, frac=1, rng=rng)
    plate(mb, g, tpm(0.58, 0.88, 96, 134, tb=lambda u: 0.56 + 0.06 * u), 0.0075, 0.0135, 5, 5, frac=1, rng=rng)
    # shoulder tops
    plate(mb, g, tpm(0.86, 0.985, -55, 55), 0.0015, 0.007, 9, 3, frac=1, rng=rng)
    plate(mb, g, tpm(0.86, 0.985, 125, 235), 0.0015, 0.007, 9, 3, frac=1, rng=rng)
    # spine seam with a diamond between the shoulder blades
    params = [(0.03 + 0.92 * k / 40, D(90)) for k in range(41)]
    pts, nrm = surface_curve(g, params, 0.0085)
    strip(mb, pts, nrm, 0.005, 0.0022, SEAM)
    dc = g.pt(0.8, D(90), 0.009)
    dn = g.normal(0.8, D(90), 0.009)
    st = len(mb.verts)
    L.lathe(mb, [(0, 0.004), (0.016, 0.0), (0.0, -0.004)], segs=4, mat=SEAM, axis="Y")
    L.transform_mb_range(mb, st, Matrix.Translation(dc + dn * 0.001) @ Matrix.Diagonal((0.7, 1.0, 1.3, 1.0)))
    # neck: column + vertical plates + collar
    ng = GenCyl([0.0, 0.5, 1.0], [0.40, 0.43, 0.462], [0.048, 0.036, 0.033], [0.045, 0.036, 0.034],
                c2=[0.008, 0.004, 0.002])
    core(mb, ng, 6, 16, 0.0, 1.0)
    for (a0, a1) in ((210, 260), (280, 330), (30, 80), (100, 150)):
        plate(mb, ng, tpm(0.25, 0.95, a0, a1), 0.0008, 0.0045, 4, 4)
    section_ring(mb, g, 0.975, 0.004, 0.008, 0.0035, nth=36)
    # shoulder sockets
    for sx in (-1, 1):
        torus(mb, (sx * 0.192, 0.0, 0.385), 0.044, 0.004, axis="X", segs=24)
    rest = mb.build("ChestRest", MATS)
    ob = L.join([body, bp, rest], "Part_Chest")
    return ob


# ---------------------------------------------------------------------------- head
FACE_C = (0.0, 0.155)       # (x, z) centre of the face oval in head space
FACE_R = (0.08, 0.11)       # half width / half height (0.16 x 0.22)
FACE_RECESS = 0.012


def head_body():
    zs = [0.03, 0.06, 0.12, 0.17, 0.22, 0.26, 0.283, 0.296]
    ts = [(z - zs[0]) / (zs[-1] - zs[0]) for z in zs]
    g = GenCyl(ts, zs, [0.05, 0.076, 0.093, 0.100, 0.096, 0.076, 0.046, 0.002],
               [0.064, 0.090, 0.108, 0.116, 0.112, 0.088, 0.052, 0.002],
               c2=[-0.006, -0.004, 0.0, 0.006, 0.009, 0.007, 0.004, 0.003], e=2.0)
    return g, ts, zs


def head_front_y(g, ts, zs, x, z, off):
    t = t_of_z(ts, zs, z)
    th = theta_front_of_x(g, t, x, off)
    return g.pt(t, th, off), t, th


def face_grid(g, ts, zs, off, rings=6, segs=48, shrink=1.0):
    """Polar grid over the face oval, projected onto the head surface offset by `off`."""
    cx, cz = FACE_C
    rx, rz = FACE_R[0] * shrink, FACE_R[1] * shrink
    grid = []
    for i in range(1, rings + 1):
        s = i / rings
        row = []
        for k in range(segs):
            a = 2 * math.pi * k / segs
            x = cx + rx * s * math.cos(a)
            z = cz + rz * s * math.sin(a)
            p, t, th = head_front_y(g, ts, zs, x, z, off)
            row.append((p, t, th))
        grid.append(row)
    p0, t0, th0 = head_front_y(g, ts, zs, cx, cz, off)
    return (p0, t0, th0), grid


def build_head(rng):
    g, ts, zs = head_body()
    hm = L.MB()
    core(hm, g, 22, 36, 0.0, 1.0)
    # neck connector
    L.lathe(hm, [(0.0, -0.006), (0.03, -0.004), (0.031, 0.05), (0.0, 0.056)], segs=16, mat=GLASS)
    shell = hm.build("HeadShell", MATS)
    # recess cutter: oval patch on the head surface offset inward, extruded far to the front
    cm = L.MB()
    (c0, _, _), grid = face_grid(g, ts, zs, -FACE_RECESS, rings=5, segs=48)
    ci = cm.v(c0)
    rows = [[cm.v(p) for (p, t, th) in row] for row in grid]
    n = len(rows[0])
    for k in range(n):
        cm.f((ci, rows[0][(k + 1) % n], rows[0][k]), 0)
    cm.bridge(rows, closed_ring=True, mat=0, flip=True)
    front = [cm.v(Vector((p.x, -0.4, p.z))) for (p, t, th) in grid[-1]]
    for k in range(n):
        k1 = (k + 1) % n
        cm.f((rows[-1][k], rows[-1][k1], front[k1], front[k]), 0)
    cm.f(tuple(front[::-1]), 0)
    cutter = cm.build("FaceCut", MATS)
    L.boolean(shell, cutter, "DIFFERENCE")
    mb = L.MB()
    # face rim (bronze) on the surface around the oval
    (cc, _, _), rimg = face_grid(g, ts, zs, 0.0005, rings=1, segs=72, shrink=1.0)
    pts = [p for (p, t, th) in rimg[0]]
    nrm = [g.normal(t, th, 0.0005) for (p, t, th) in rimg[0]]
    strip(mb, pts, nrm, 0.0085, 0.0045, SEAM, closed=True)
    # cranium plates: left / right halves, back seam gap, crown seam over the top
    plate(mb, g, tpm(0.1, 0.93, 96, 206, tb=lambda u: 0.1 + 0.25 * max(0.0, u - 0.6) / 0.4), 0.0012, 0.0062,
          9, 10, frac=3, rng=rng)
    plate(mb, g, tpm(0.1, 0.93, -26, 84, tb=lambda u: 0.1 + 0.25 * max(0.0, 0.4 - u) / 0.4), 0.0012, 0.0062,
          9, 10, frac=3, rng=rng)
    # crown seam: from the top of the face rim over the crown and down the back
    params = []
    for k in range(18):
        params.append((0.86 + (0.995 - 0.86) * k / 17, D(270)))
    for k in range(30):
        params.append((0.995 - (0.995 - 0.12) * k / 29, D(90)))
    pts, nrm = surface_curve(g, params, 0.0068)
    strip(mb, pts, nrm, 0.0055, 0.0026, SEAM)
    # temple discs (ears)
    for sx in (-1, 1):
        th = 0.0 if sx > 0 else math.pi
        tt = t_of_z(ts, zs, 0.15)
        c = g.pt(tt, th, 0.006)
        st = len(mb.verts)
        L.lathe(mb, [(0, 0.004), (0.011, 0.003), (0.016, 0.0), (0.016, -0.004), (0.0, -0.005)], segs=20,
                mat=SEAM, axis="X")
        M = Matrix.Translation(c)
        if sx < 0:
            M = M @ Matrix.Scale(-1, 4, Vector((1, 0, 0)))
        L.transform_mb_range(mb, st, M)
    # jaw / chin band
    section_ring(mb, g, 0.06, 0.003, 0.006, 0.003, nth=40)
    rest = mb.build("HeadRest", MATS)
    head = L.join([shell, rest], "Part_Head")
    return head, g, ts, zs


def build_face_plate(g, ts, zs):
    """Empty oval visor: follows the head curvature, sits 2 mm proud of the recess floor."""
    mb = L.MB()
    off_f = -FACE_RECESS + 0.002
    (c0, _, _), gridf = face_grid(g, ts, zs, off_f, rings=6, segs=48, shrink=0.985)
    (c1, _, _), gridb = face_grid(g, ts, zs, off_f - 0.004, rings=6, segs=48, shrink=0.985)
    # slight extra dome on the front surface
    def dome(p, i, rings):
        s = i / rings
        return p + Vector((0, -0.004 * (1 - s * s), 0))
    rf = [[mb.v(dome(p, i + 1, 6)) for (p, t, th) in row] for i, row in enumerate(gridf)]
    rb = [[mb.v(p) for (p, t, th) in row] for row in gridb]
    cf = mb.v(dome(c0, 0, 6))
    cb = mb.v(c1)
    n = len(rf[0])
    for k in range(n):
        mb.f((cf, rf[0][k], rf[0][(k + 1) % n]), FACE)
        mb.f((cb, rb[0][(k + 1) % n], rb[0][k]), FACE)
    mb.bridge(rf, closed_ring=True, mat=FACE)
    mb.bridge(rb, closed_ring=True, mat=FACE, flip=True)
    for k in range(n):
        k1 = (k + 1) % n
        mb.f((rf[-1][k], rb[-1][k], rb[-1][k1], rf[-1][k1]), FACE)
    ob = mb.build("Part_FacePlate", MATS)
    return ob


# ---------------------------------------------------------------------------- assembly
LAYOUT = {
    "Part_Foot": (0.095, 0.0, 0.10),
    "Part_Shin": (0.095, 0.0, 0.62),
    "Part_Thigh": (0.095, 0.0, 1.12),
    "Part_Hips": (0.0, 0.0, 1.12),
    "Part_Chest": (0.0, 0.0, 1.40),
    "Part_Head": (0.0, 0.0, 1.85),
    "Part_UpperArm": (0.21, 0.0, 1.785),
    "Part_Forearm": (0.21, 0.0, 1.425),
    "Part_Hand": (0.21, 0.0, 1.065),
    "Part_FingerTag": (0.21 + KNUCKLES[1], 0.0, 1.065 - PALM_L),
}


def build():
    L.reset()
    rng = random.Random(1212)
    root = L.empty("Passenger_Parts", size=0.3)
    parts = {}
    parts["Part_Hips"] = build_hips(rng)
    parts["Part_Chest"] = build_chest(rng)
    head, g, ts, zs = build_head(rng)
    parts["Part_Head"] = head
    fp = build_face_plate(g, ts, zs)
    parts["Part_UpperArm"] = build_upper_arm(rng)
    parts["Part_Forearm"] = build_forearm(rng)
    parts["Part_Hand"] = build_hand(rng)
    parts["Part_FingerTag"] = build_finger_tag()
    parts["Part_Thigh"] = build_thigh(rng)
    parts["Part_Shin"] = build_shin(rng)
    parts["Part_Foot"] = build_foot(rng)
    for name, ob in parts.items():
        finish_part(ob)
        ob.parent = root
        ob.location = LAYOUT[name]
    # face plate: origin at its own centre, placed on the head
    finish_part(fp, 30.0)
    me = fp.data
    cen = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
    me.transform(Matrix.Translation(-cen))
    fp.parent = root
    fp.location = Vector(LAYOUT["Part_Head"]) + cen
    face_offset = [round(c, 4) for c in cen]
    bpy.context.view_layer.update()
    info = L.export(root, "Passenger_Parts.fbx", {"faceplate_offset_from_head_blender": face_offset,
                                                  "knuckles_x": KNUCKLES, "palm_length": PALM_L})
    # ---- preview: add mirrored right limbs + 8 finger tags (preview only, not exported)
    extra = []
    for name in ("Part_Foot", "Part_Shin", "Part_Thigh", "Part_UpperArm", "Part_Forearm", "Part_Hand"):
        src = parts[name]
        cp = src.copy()
        cp.data = src.data
        bpy.context.scene.collection.objects.link(cp)
        cp.parent = None
        loc = Vector(LAYOUT[name])
        cp.location = (-loc.x, loc.y, loc.z)
        extra.append(cp)
    for side in (1, -1):
        for kx in KNUCKLES:
            if side == 1 and kx == KNUCKLES[1]:
                continue
            cp = parts["Part_FingerTag"].copy()
            cp.data = parts["Part_FingerTag"].data
            bpy.context.scene.collection.objects.link(cp)
            cp.parent = None
            cp.location = (side * 0.21 + kx, 0.0, 1.065 - PALM_L)
            extra.append(cp)
    bpy.context.view_layer.update()
    L.preview(root, "Passenger_Parts.png", view=(-0.55, -1.0, 0.22), lens=50, extra_objs=extra, margin=1.04)
    L.preview(root, "Passenger_Parts_front.png", view=(0.0, -1.0, 0.0), lens=50, extra_objs=extra, margin=1.04)
    L.preview(root, "Passenger_Parts_side.png", view=(-1.0, 0.0, 0.0), lens=50, extra_objs=extra, margin=1.04)
    L.preview(root, "Passenger_Parts_back.png", view=(0.25, 1.0, 0.1), lens=50, extra_objs=extra, margin=1.04)
    L.preview(root, "Passenger_Parts_head.png", view=(-0.4, -1.0, 0.15), lens=50, extra_objs=extra, margin=0.13,
              target=(0.0, -0.05, 1.98), floor=False)
    L.preview(root, "Passenger_Parts_chest.png", view=(-0.3, -1.0, 0.1), lens=50, extra_objs=extra, margin=0.2,
              target=(0.0, -0.08, 1.65), floor=False)
    L.preview(root, "Passenger_Parts_hand.png", view=(-0.5, -1.0, 0.1), lens=50, extra_objs=extra, margin=0.12,
              target=(0.21, -0.02, 0.98), floor=False)
    for cp in extra:
        bpy.data.objects.remove(cp, do_unlink=True)
    L.save_blend("Passenger_Parts.blend")
    return info


if __name__ == "__main__":
    build()
