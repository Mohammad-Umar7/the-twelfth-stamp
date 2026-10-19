"""
Landmark silhouettes for distant backdrops (stylised but recognisable).
Blender: metres, front -Y, +Z up, origin at ground centre.
usage: blender -b --factory-startup --python build_landmarks.py -- [Landmark_Eiffel ...]   (default: all)
"""
import sys
import os
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402
import build_apartment as A  # noqa: E402
from build_props import lathe_r, knurl, finish, planar_uv  # noqa: E402


# ------------------------------------------------------------------ helpers
def beam(mb, p0, p1, w, mat=0, d=None):
    """Closed square-section beam from p0 to p1 (w x d)."""
    p0, p1 = Vector(p0), Vector(p1)
    t = p1 - p0
    if t.length < 1e-6:
        return
    t.normalize()
    ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
    n = t.cross(ref).normalized()
    b = n.cross(t).normalized()
    d = d or w
    cs = [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)]
    a = [mb.v(p0 + n * x + b * y) for (x, y) in cs]
    c = [mb.v(p1 + n * x + b * y) for (x, y) in cs]
    for i in range(4):
        i1 = (i + 1) % 4
        mb.f((a[i], a[i1], c[i1], c[i]), mat, None, False)
    mb.f((a[3], a[2], a[1], a[0]), mat, None, False)
    mb.f((c[0], c[1], c[2], c[3]), mat, None, False)


def loft(mb, rings_pts, mat=0, cap0=True, cap1=True, closed=False):
    rings = [[mb.v(p) for p in ring] for ring in rings_pts]
    mb.bridge(rings, closed_ring=True, mat=mat, closed_path=closed)
    if not closed:
        if cap0:
            mb.cap(rings[0], mat, flip=True, fan_center=sum(rings_pts[0], Vector()) / len(rings_pts[0]))
        if cap1:
            mb.cap(rings[-1], mat, fan_center=sum(rings_pts[-1], Vector()) / len(rings_pts[-1]))
    return rings


def bx(mb, size, center, mat=0, rot=None):
    L.box(mb, size, center=center, mat=mat, rot=rot)


def pointed_arch(w, z0, zs, steps=10, k=0.3):
    """CCW outline (x, z): rectangle from z0 to the springing zs, then a two-centred pointed arch."""
    c = k * w / 2
    R = w / 2 + c
    pts = [(-w / 2, z0), (w / 2, z0), (w / 2, zs)]
    a_top = math.acos(c / R)
    for i in range(1, steps + 1):
        a = a_top * i / steps
        pts.append((-c + R * math.cos(a), zs + R * math.sin(a)))
    a_l = math.acos(-c / R)
    for i in range(1, steps + 1):
        a = a_l + (math.pi - a_l) * i / steps
        pts.append((c + R * math.cos(a), zs + R * math.sin(a)))
    return L.dedupe2d(pts)


def arch_cutter(w, z0, zs, depth, face_dist, angle_deg, mat=0):
    """Closed prism of a pointed-arch outline, cutting `depth` into a face at distance face_dist
    whose outward normal is -Y rotated by angle_deg about Z."""
    mb = L.MB()
    basis = (Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0)))
    L.prism(mb, pointed_arch(w, z0, zs), -face_dist - 2.0, -face_dist + depth, mat=mat, basis=basis)
    ob = mb.build("cut", ["TS_Stone"])
    ob.data.transform(Matrix.Rotation(math.radians(angle_deg), 4, "Z"))
    return ob


def ring_pts(r, z, n, a0=0.0, sx=1.0, sy=1.0, c=(0, 0)):
    return [Vector((c[0] + r * sx * math.cos(a0 + 2 * math.pi * i / n), c[1] + r * sy * math.sin(a0 + 2 * math.pi * i / n), z))
            for i in range(n)]


def square_pts(a, z, n_side=4, r=0.0):
    pts = L.resample(L.rrect(2 * a, 2 * a, max(r, 1e-4), seg=3), n_side * 4)
    return [Vector((x, y, z)) for (x, y) in pts]


def done(root, name, views):
    return A.export_asset(root, name, views)


# ------------------------------------------------------------------ Eiffel (~100 m)
def build_eiffel():
    L.reset()
    root = L.empty("Landmark_Eiffel", size=5)
    IRON, DECK, LIGHT = 0, 1, 2
    MATS = ["TS_MetalDark", "TS_Metal", "TS_EmissiveWarm"]

    def w(z):
        return 0.6 + 18.4 * math.exp(-z / 29.0)
    Z1, Z2, Z3 = 17.0, 35.0, 84.0

    def s(z):
        return 7.5 + (w(Z2) - 0.7 - 7.5) * (z / Z2)
    mb = L.MB()
    # four legs: box-section lattice with X-bracing on every face
    levels = [Z2 * k / 10 for k in range(11)]
    for (sx, sy) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        C = []
        for z in levels:
            W, S = w(z), s(z)
            C.append([Vector((sx * W, sy * W, z)), Vector((sx * (W - S), sy * W, z)),
                      Vector((sx * (W - S), sy * (W - S), z)), Vector((sx * W, sy * (W - S), z))])
        for k in range(len(levels) - 1):
            cw = 0.95 - 0.45 * levels[k] / Z2
            for i in range(4):
                i1 = (i + 1) % 4
                beam(mb, C[k][i], C[k + 1][i], cw, IRON)
                if (C[k][i] - C[k][i1]).length > 0.5:
                    beam(mb, C[k][i], C[k + 1][i1], 0.32, IRON)
                    beam(mb, C[k][i1], C[k + 1][i], 0.32, IRON)
        for k in range(len(levels)):
            for i in range(4):
                if (C[k][i] - C[k][(i + 1) % 4]).length > 0.5:
                    beam(mb, C[k][i], C[k][(i + 1) % 4], 0.4, IRON)
    # upper shaft
    up = [Z2 + (Z3 - Z2) * (k / 16) ** 0.9 for k in range(17)]
    C = [[Vector((sx * w(z), sy * w(z), z)) for (sx, sy) in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for z in up]
    for k in range(len(up) - 1):
        cw = 0.6 - 0.3 * (up[k] - Z2) / (Z3 - Z2)
        for i in range(4):
            i1 = (i + 1) % 4
            beam(mb, C[k][i], C[k + 1][i], cw, IRON)
            beam(mb, C[k][i], C[k + 1][i1], 0.26, IRON)
            beam(mb, C[k][i1], C[k + 1][i], 0.26, IRON)
            # mid-face posts where the faces are still wide
            if w(up[k]) > 3.0:
                m0 = (C[k][i] + C[k][i1]) / 2
                m1 = (C[k + 1][i] + C[k + 1][i1]) / 2
                beam(mb, m0, m1, 0.28, IRON)
    for k in range(len(up)):
        for i in range(4):
            beam(mb, C[k][i], C[k][(i + 1) % 4], 0.3, IRON)
    # great arches between the legs on all four faces (double arch with radial bracing)
    for rot in range(4):
        st = len(mb.verts)
        for (R, Rz) in ((10.6, 11.6), (9.2, 10.1)):
            pts = []
            for j in range(25):
                t = math.pi * j / 24
                x, z = R * math.cos(t), 4.0 + Rz * math.sin(t)
                pts.append(Vector((x, -(w(z) - 0.9), z)))
            L.sweep(mb, pts, L.rrect(0.7, 0.9, 0.1, seg=1), mat=IRON, caps=True, fixed_binormal=(0, 1, 0))
        for j in range(1, 12):
            t = math.pi * j / 12
            zo, zi = 4.0 + 11.6 * math.sin(t), 4.0 + 10.1 * math.sin(t)
            po = Vector((10.6 * math.cos(t), -(w(zo) - 0.9), zo))
            pi = Vector((9.2 * math.cos(t), -(w(zi) - 0.9), zi))
            beam(mb, po, pi, 0.3, IRON)
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    # first platform: square ring deck + frieze band + railing
    W1 = w(Z1) + 1.3
    inner = w(Z1) - s(Z1)
    for rot in range(4):
        st = len(mb.verts)
        bx(mb, (2 * W1 + 0.3, W1 - inner, 1.4), (0, -(W1 + inner) / 2, Z1 + 0.7), DECK)
        bx(mb, (2 * W1 - 0.1, 0.4, 2.2), (0, -W1 + 0.2, Z1 - 0.9), IRON)
        bx(mb, (2 * W1, 0.15, 1.1), (0, -W1 + 0.1, Z1 + 1.95), IRON)
        for j in range(17):
            x = -W1 + 2 * W1 * j / 16
            bx(mb, (0.12, 0.12, 1.1), (x, -W1 + 0.1, Z1 + 1.95), IRON)
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    # second platform
    W2 = w(Z2) + 1.1
    bx(mb, (2 * W2, 2 * W2, 1.2), (0, 0, Z2 + 0.6), DECK)
    for rot in range(4):
        st = len(mb.verts)
        bx(mb, (2 * W2, 0.15, 1.1), (0, -W2 + 0.1, Z2 + 1.75), IRON)
        bx(mb, (2 * W2, 0.35, 1.6), (0, -W2 + 0.2, Z2 - 0.6), IRON)
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    # top: deck, cabin, lantern, mast, beacon
    W3 = w(Z3) + 0.9
    bx(mb, (2 * W3, 2 * W3, 0.8), (0, 0, Z3 + 0.4), DECK)
    bx(mb, (2 * W3 - 0.8, 2 * W3 - 0.8, 3.0), (0, 0, Z3 + 2.3), DECK)
    lathe_r(mb, [(0, Z3 + 3.8), (1.6, Z3 + 3.8), (1.4, Z3 + 5.5), (0.9, Z3 + 6.5), (0.5, Z3 + 7.0), (0, Z3 + 7.1)], 16, IRON)
    lathe_r(mb, [(0, Z3 + 7.0), (0.32, Z3 + 7.0), (0.22, 99.0), (0, 99.2)], 12, IRON)
    mb_l = L.MB()
    A.superellipsoid(mb_l, (0.45, 0.45, 0.45), (0, 0, Z3 + 7.4), 1.0, 1.0, 12, 8, 0)
    for i in range(len(mb.verts)):
        if mb.verts[i].z < 0.0:
            mb.verts[i].z = 0.0
    tower = finish(mb.build("Tower", MATS), root, 40.0)
    beacon = finish(mb_l.build("Beacon", ["TS_EmissiveWarm"]), root, 60.0)
    L.join([tower, beacon], "Tower")
    return done(root, "Landmark_Eiffel", [("Landmark_Eiffel.png", dict(view=(-0.55, -1.0, 0.25), lens=50))])


# ------------------------------------------------------------------ Liberty (~93 m)
def build_liberty():
    L.reset()
    root = L.empty("Landmark_Liberty", size=5)
    STONE, MET, FL, DK = 0, 1, 2, 3
    MATS = ["TS_Stone", "TS_Metal", "TS_EmissiveWarm", "TS_MetalDark"]
    mb = L.MB()
    # 11-point star fort base
    star = []
    for k in range(22):
        a = math.pi / 2 + 2 * math.pi * k / 22
        r = 31.0 if k % 2 == 0 else 23.5
        star.append((r * math.cos(a), r * math.sin(a)))
    L.prism(mb, star, 0.0, 6.5, mat=STONE)
    L.prism(mb, L.offset2d(L.ensure_ccw(star), 0.6), 6.5, 7.1, mat=STONE)
    # pedestal: plinth, tapered shaft with pilasters and panels, cornice, attic, parapet
    bx(mb, (22, 22, 4.0), (0, 0, 9.0), STONE)
    rings = [square_pts(9.4, 11.0, 4, 0.4), square_pts(8.1, 37.5, 4, 0.4)]
    loft(mb, rings, STONE)
    for rot in range(4):
        st = len(mb.verts)
        for x in (-7.2, 7.2):
            loft(mb, [[Vector((x + dx, -9.4 + dy, 11.0)) for (dx, dy) in ((-1.1, -0.5), (1.1, -0.5), (1.1, 0.6), (-1.1, 0.6))],
                      [Vector((x * 0.87 + dx, -8.1 + dy, 37.5)) for (dx, dy) in ((-1.0, -0.5), (1.0, -0.5), (1.0, 0.6), (-1.0, 0.6))]],
                 STONE)
        for z in (20.0, 30.5):
            f = (z - 11.0) / 26.5
            hw = 9.4 + (8.1 - 9.4) * f
            bx(mb, (2 * hw + 0.9, 0.9, 0.7), (0, -hw - 0.15, z), STONE)
        # loggia panel (dark recess look) with a raised frame
        bx(mb, (6.0, 0.3, 7.5), (0, -8.9, 25.5), DK)
        bx(mb, (7.4, 0.5, 0.6), (0, -8.75, 29.5), STONE)
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    bx(mb, (18.6, 18.6, 1.4), (0, 0, 38.2), STONE)
    bx(mb, (17.6, 17.6, 0.8), (0, 0, 39.3), STONE)
    rings = [square_pts(7.6, 39.7, 4, 0.3), square_pts(7.3, 45.0, 4, 0.3)]
    loft(mb, rings, STONE)
    bx(mb, (16.4, 16.4, 1.0), (0, 0, 45.5), STONE)
    bx(mb, (14.6, 14.6, 1.0), (0, 0, 46.5), STONE)
    # statue: robe with folds (generalised cylinder along Z, faces -Y)
    zs = [47.0, 48.0, 50.5, 54.0, 58.0, 62.0, 65.5, 68.5, 71.5, 74.0, 76.0, 77.2]
    rx = [6.0, 6.1, 5.6, 5.0, 4.6, 4.3, 4.2, 4.5, 4.9, 5.2, 4.8, 2.6]
    ry = [5.0, 5.1, 4.6, 4.0, 3.6, 3.3, 3.3, 3.6, 3.7, 3.5, 3.0, 2.0]
    cy = [0.3, 0.3, 0.2, 0.0, -0.1, -0.1, 0.0, 0.1, 0.2, 0.3, 0.3, 0.3]
    ts = [(z - zs[0]) / (zs[-1] - zs[0]) for z in zs]
    frx, fry, fcy = L.cr(ts, rx), L.cr(ts, ry), L.cr(ts, cy)
    segs = 56
    rings = []
    for j in range(34):
        t = j / 33
        z = zs[0] + (zs[-1] - zs[0]) * t
        fold = max(0.0, 1.0 - t / 0.75)
        ring = []
        for i in range(segs):
            th = 2 * math.pi * i / segs
            m = 1.0 + 0.05 * fold * math.sin(13 * th + 0.25 * z) + 0.025 * fold * math.sin(7 * th - 0.4 * z)
            # forward knee under the robe (her left = +X, front = -Y)
            kn = math.exp(-((t - 0.28) / 0.12) ** 2) * max(0.0, -math.sin(th)) * max(0.0, math.cos(th) + 0.3) * 0.6
            ring.append(Vector((frx(t) * m * math.cos(th), fcy(t) + fry(t) * m * math.sin(th) - kn, z)))
        rings.append(ring)
    loft(mb, rings, MET)
    # neck + head + crown
    lathe_r(mb, [(0, 76.5), (1.5, 76.6), (1.35, 78.5), (0, 78.8)], 20, MET, center=(0, 0.2, 0))
    A.superellipsoid(mb, (2.0, 2.3, 2.6), (0, 0.0, 80.8), 1.0, 1.0, 24, 14, MET)
    torus_pts = [Vector((2.25 * math.cos(a), 0.15 + 2.45 * math.sin(a), 82.0)) for a in [2 * math.pi * k / 32 for k in range(32)]]
    L.sweep(mb, torus_pts, L.circle2d(0.35, 8), mat=MET, closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
    for k in range(7):
        ang = math.radians(180 + 15 + 25 * k)
        d = Vector((math.cos(ang), math.sin(ang), 0.75)).normalized()
        base = Vector((2.1 * math.cos(ang), 0.15 + 2.3 * math.sin(ang), 82.2))
        tip = base + d * 3.4
        st = len(mb.verts)
        L.lathe(mb, [(0.0, 0.0), (0.36, 0.0), (0.0, 3.4)], segs=6, mat=MET)
        q = Vector((0, 0, 1)).rotation_difference(d)
        L.transform_mb_range(mb, st, Matrix.Translation(base) @ q.to_matrix().to_4x4())
    # right arm raised with the torch (her right = -X)
    arm = [Vector((-3.9, 0.3, 75.0)), Vector((-4.9, 0.4, 78.5)), Vector((-5.5, 0.5, 82.0)), Vector((-5.8, 0.6, 85.0)),
           Vector((-5.9, 0.6, 87.2))]
    arm = L.polyline_resample3d([tuple(p) for p in A.bezier_chain(arm)], 18)
    L.sweep(mb, arm, L.circle2d(1.0, 14), mat=MET, caps=True, up=(1, 0, 0),
            scale=lambda t: 1.55 - 0.85 * t if t < 0.5 else (1.12 - 0.42 * (t - 0.5) / 0.5) * (1.0 if t < 0.9 else 0.85))
    A.superellipsoid(mb, (0.75, 0.75, 0.9), (-5.9, 0.6, 87.4), 0.8, 0.8, 12, 8, MET)
    lathe_r(mb, [(0, 87.0), (0.45, 87.0), (0.45, 89.6), (0, 89.6)], 12, MET, center=(-5.95, 0.6, 0))
    lathe_r(mb, [(0, 89.4), (0.6, 89.4), (1.25, 90.2), (1.35, 90.9), (1.1, 91.1), (0, 91.0)], 20, MET, center=(-5.95, 0.6, 0))
    fm = L.MB()
    lathe_r(fm, [(0, 90.9), (0.95, 91.1), (1.0, 91.7), (0.7, 92.6), (0.3, 93.3), (0, 93.6)], 16, 0, center=(-5.95, 0.6, 0))
    # left arm holding the tablet (her left = +X)
    larm = [Vector((4.2, 0.2, 75.0)), Vector((5.1, 0.0, 72.0)), Vector((5.5, -0.6, 69.5)), Vector((5.0, -2.0, 70.0)),
            Vector((4.4, -2.9, 71.2))]
    larm = L.polyline_resample3d([tuple(p) for p in A.bezier_chain(larm)], 16)
    L.sweep(mb, larm, L.circle2d(1.0, 12), mat=MET, caps=True, up=(1, 0, 0), scale=lambda t: 1.3 - 0.6 * t)
    st = len(mb.verts)
    bx(mb, (3.2, 0.7, 6.4), (0, 0, 0), MET)
    L.transform_mb_range(mb, st, Matrix.Translation(Vector((4.7, -2.7, 73.8))) @ Matrix.Rotation(math.radians(-18), 4, "X")
                         @ Matrix.Rotation(math.radians(-8), 4, "Y"))
    statue = finish(mb.build("Statue", MATS), root, 45.0)
    flame = finish(fm.build("TorchFlame", ["TS_EmissiveWarm"]), root, 60.0)
    L.join([statue, flame], "Statue")
    return done(root, "Landmark_Liberty", [("Landmark_Liberty.png", dict(view=(-0.5, -1.0, 0.2), lens=50)),
                                           ("Landmark_Liberty_top.png", dict(view=(-0.6, -1.0, 0.2), lens=50, margin=0.3,
                                                                             target=(0, 0, 80.0), floor=False))])


# ------------------------------------------------------------------ Pagoda (5 tiers, ~40 m)
def curved_roof(mb, zc, core, eave, drop, upturn, thick, mat_top, mat_under, n=96, nt=9):
    """Square concave hipped roof ring from the core (half-size) to the eave, corners upturned."""
    base = L.resample(L.rrect(2.0, 2.0, 0.06, seg=4), n)

    def ring(t, dz=0.0, grow=0.0):
        a = core + (eave - core) * t + grow
        out = []
        for (x, y) in base:
            px, py = x * a, y * a
            cf = (min(abs(x), 1.0) * min(abs(y), 1.0)) ** 6
            z = zc - drop * (t ** 1.5) + upturn * (t ** 2.6) * cf + dz
            out.append(Vector((px, py, z)))
        return out
    tops = [ring(j / nt) for j in range(nt + 1)]
    unders = [ring(j / nt, dz=-thick * (0.6 + 0.4 * (1 - j / nt)), grow=-0.05) for j in range(nt, -1, -1)]
    rr = [[mb.v(p) for p in r] for r in tops + unders]
    for j in range(len(rr) - 1):
        m = mat_top if j < nt else mat_under
        mb.bridge([rr[j], rr[j + 1]], closed_ring=True, mat=m)
    mb.bridge([rr[-1], rr[0]], closed_ring=True, mat=mat_under)
    # hip ridges along the four corners
    for k in range(4):
        idx = min(range(n), key=lambda i: -(base[i][0] * [1, -1, -1, 1][k] + base[i][1] * [1, 1, -1, -1][k]))
        path = [tops[j][idx] + Vector((0, 0, 0.12)) for j in range(nt + 1)]
        L.sweep(mb, path, L.circle2d(0.16, 6), mat=mat_top, caps=True, up=(0, 0, 1))


def build_pagoda():
    L.reset()
    root = L.empty("Landmark_Pagoda", size=3)
    WOOD, DARK, ROOF, STONE, BR, WHITE = 0, 1, 2, 3, 4, 5
    MATS = ["TS_Wood", "TS_WoodDark", "TS_MetalDark", "TS_Stone", "TS_Brass", "TS_PlasticWhite"]
    mb = L.MB()
    # stone platform with steps on the front
    bx(mb, (15.0, 15.0, 1.2), (0, 0, 0.6), STONE)
    for k in range(3):
        bx(mb, (4.0, 0.45, 0.4 * (k + 1)), (0, -7.5 - 0.45 * (3 - k) + 0.225, 0.2 * (k + 1)), STONE)
    z = 1.2
    body = 4.3
    for k in range(5):
        hb = [4.2, 3.5, 3.3, 3.1, 2.9][k]
        # balcony + railing (upper storeys)
        if k > 0:
            bx(mb, (2 * (body + 0.7), 2 * (body + 0.7), 0.3), (0, 0, z + 0.15), DARK)
            for rot in range(4):
                st = len(mb.verts)
                bx(mb, (2 * (body + 0.6), 0.12, 0.12), (0, -(body + 0.6), z + 0.95), DARK)
                for j in range(9):
                    x = -(body + 0.6) + 2 * (body + 0.6) * j / 8
                    bx(mb, (0.12, 0.12, 0.7), (x, -(body + 0.6), z + 0.65), DARK)
                L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
        # walls: plaster core with corner + middle posts, doors on the front centre
        bx(mb, (2 * body, 2 * body, hb), (0, 0, z + hb / 2), WHITE)
        for rot in range(4):
            st = len(mb.verts)
            for x in (-body, -body / 3, body / 3, body):
                bx(mb, (0.42, 0.42, hb), (x, -body, z + hb / 2), WOOD)
            bx(mb, (2 * body, 0.3, 0.35), (0, -body - 0.05, z + hb - 0.2), WOOD)
            bx(mb, (2 * body / 3 - 0.5, 0.18, hb * 0.72), (0, -body - 0.02, z + hb * 0.36 + 0.05), WOOD)
            L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
        z += hb
        # bracket band under the eaves
        bx(mb, (2 * body + 0.9, 2 * body + 0.9, 0.55), (0, 0, z + 0.27), DARK)
        bx(mb, (2 * body + 1.8, 2 * body + 1.8, 0.4), (0, 0, z + 0.75), DARK)
        eave = body + [3.1, 2.8, 2.6, 2.4, 2.3][k]
        curved_roof(mb, z + 2.0, body * 0.7, eave, 1.7, 1.1, 0.45, ROOF, DARK)
        z += 2.0
        body *= 0.86
    # top roof cap + sorin finial (bronze)
    bx(mb, (2.6, 2.6, 0.8), (0, 0, z + 0.4), ROOF)
    zf = z + 0.8
    lathe_r(mb, [(0, zf), (1.0, zf), (1.0, zf + 0.6), (0.75, zf + 0.7), (0, zf + 0.7)], 16, BR)
    lathe_r(mb, [(0, zf + 0.65), (0.6, zf + 0.7), (0.95, zf + 1.1), (1.0, zf + 1.5), (0.4, zf + 1.6), (0, zf + 1.6)], 20, BR)
    lathe_r(mb, [(0, zf + 1.55), (0.25, zf + 1.6), (0.9, zf + 2.0), (0.9, zf + 2.2), (0, zf + 2.2)], 20, BR)
    lathe_r(mb, [(0, zf + 2.1), (0.16, zf + 2.1), (0.12, zf + 9.0), (0, zf + 9.1)], 12, BR)
    for k in range(9):
        zr = zf + 2.6 + 0.62 * k
        r = 0.95 - 0.035 * k
        pts = [Vector((r * math.cos(a), r * math.sin(a), zr)) for a in [2 * math.pi * i / 24 for i in range(24)]]
        L.sweep(mb, pts, L.circle2d(0.1, 6), mat=BR, closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
    # water-flame ornament (flat, facing front) + jewel
    st = len(mb.verts)
    flame_pts = []
    for i in range(16):
        a = 2 * math.pi * i / 16
        r = 0.75 + 0.18 * math.cos(4 * a)
        flame_pts.append((r * math.cos(a), r * 1.25 * math.sin(a)))
    L.prism(mb, flame_pts, -0.06, 0.06, mat=BR, basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))),
            origin=(0, 0, zf + 8.7))
    A.superellipsoid(mb, (0.32, 0.32, 0.42), (0, 0, zf + 9.75), 1.0, 1.0, 14, 8, BR)
    pag = finish(mb.build("Pagoda", MATS), root, 40.0)
    return done(root, "Landmark_Pagoda", [("Landmark_Pagoda.png", dict(view=(-0.65, -1.0, 0.3), lens=50))])


# ------------------------------------------------------------------ Arch tower (~150 m)
def build_arch_tower():
    L.reset()
    root = L.empty("Landmark_ArchTower", size=5)
    GLASS, METAL, LIGHT, CURTAIN = 0, 1, 2, 3
    MATS = ["TS_Glass", "TS_Metal", "TS_EmissiveWarm", "TS_MetalDark"]
    HTOP = 142.0

    def x_out(z):
        v = 28.0 - 11.0 * (z / 150.0)
        if z > 128.0:
            u = min(1.0, (z - 128.0) / (HTOP - 128.0 + 0.6))
            v *= math.sqrt(max(0.0, 1.0 - u * u)) * 0.55 + 0.45 * (1.0 - u)
        return v

    def x_in(z):
        return 18.0 * math.sqrt(max(0.0, 1.0 - z / 125.0)) if z < 125.0 else 0.0

    def depth(z):
        return 11.0 - 3.0 * (z / 150.0)
    mb = L.MB()
    FL = 4.0
    nfl = int(HTOP // FL)

    def section(z, inset):
        xo, xi, d = x_out(z) - inset, x_in(z), depth(z) - inset
        r = min(1.5, max(0.2, (xo - xi) * 0.2))
        if xi > 0.05:
            secs = []
            for sx in (-1, 1):
                w = xo - (xi + inset)
                if w < 0.3:
                    continue
                cx = sx * (xi + inset + w / 2)
                secs.append(L.resample([(cx + px, py) for (px, py) in L.rrect(w, 2 * d, min(r, w / 2 - 0.01), seg=3)], 28))
            return secs
        return [L.resample(L.rrect(2 * xo, 2 * d, r, seg=3), 40)]
    for f in range(nfl):
        z0 = f * FL
        for (za, zb, inset, mat) in ((z0, z0 + 0.7, 0.0, METAL), (z0 + 0.7, z0 + FL, 0.3, CURTAIN)):
            sa = section(za, inset)
            sb = section(zb, inset)
            if len(sa) != len(sb):
                sb = section(za, inset)
            for (pa, pb) in zip(sa, sb):
                loft(mb, [[Vector((x, y, za)) for (x, y) in pa], [Vector((x, y, zb)) for (x, y) in pb]], mat)
    # roof crown + spire + beacon
    ztop = nfl * FL
    loft(mb, [[Vector((x, y, ztop)) for (x, y) in section(ztop - 0.01, 0.0)[0]],
              [Vector((x * 0.6, y * 0.6, ztop + 2.0)) for (x, y) in section(ztop - 0.01, 0.0)[0]]], METAL)
    lathe_r(mb, [(0, ztop + 1.9), (1.1, ztop + 1.9), (0.7, ztop + 4.5), (0.25, 149.3), (0, 149.4)], 12, METAL)
    # vertical fins on the front and back faces of each leg (continuous, follow the taper)
    for sx in (-1, 1):
        for fx in (0.25, 0.5, 0.75):
            for sy in (-1, 1):
                pts = []
                for k in range(0, 31):
                    z = 123.0 * k / 30
                    xo, xi = x_out(z), x_in(z)
                    pts.append(Vector((sx * (xi + (xo - xi) * fx), sy * (depth(z) + 0.05), z)))
                L.sweep(mb, pts, L.rrect(0.35, 0.5, 0.05, seg=1), mat=METAL, caps=True, fixed_binormal=(0, 1, 0))
    # sky bridge across the opening near the top
    zb0 = 100.0
    xi = x_in(zb0 + 3.0)
    bx(mb, (2 * xi + 2.0, 9.0, 0.8), (0, 0, zb0 + 0.4), METAL)
    bx(mb, (2 * xi + 2.0, 9.0, 0.8), (0, 0, zb0 + 6.0), METAL)
    bx(mb, (2 * xi + 1.4, 8.4, 4.8), (0, 0, zb0 + 3.2), GLASS)
    for j in range(9):
        x = -xi + 2 * xi * j / 8
        for sy in (-1, 1):
            bx(mb, (0.35, 0.35, 4.8), (x, sy * 4.3, zb0 + 3.2), METAL)
    for i in range(len(mb.verts)):
        if mb.verts[i].z < 0.0:
            mb.verts[i].z = 0.0
    lm = L.MB()
    A.superellipsoid(lm, (0.4, 0.4, 0.4), (0, 0, 149.6), 1.0, 1.0, 12, 8, 0)
    tower = finish(mb.build("Tower", MATS), root, 30.0)
    beacon = finish(lm.build("Beacon", ["TS_EmissiveWarm"]), root, 60.0)
    L.join([tower, beacon], "Tower")
    return done(root, "Landmark_ArchTower", [("Landmark_ArchTower.png", dict(view=(-0.5, -1.0, 0.15), lens=50))])


# ------------------------------------------------------------------ Domed pavilion (~65 m)
def onion(mb, z0, r0, h, mat, segs=48, neck=0.22):
    prof = [(r0, z0), (r0 * 1.08, z0 + 0.12 * h), (r0 * 1.12, z0 + 0.25 * h), (r0 * 1.05, z0 + 0.42 * h),
            (r0 * 0.82, z0 + 0.6 * h), (r0 * 0.5, z0 + 0.78 * h), (r0 * neck, z0 + 0.92 * h), (r0 * 0.09, z0 + 0.98 * h),
            (0, z0 + h)]
    lathe_r(mb, [(0, z0)] + prof, segs, mat)


def chhatri(mb, c, r, z0, hcol, mat, gold, n=8):
    cx, cy = c
    lathe_r(mb, [(0, z0), (r * 1.1, z0), (r * 1.1, z0 + 0.6), (0, z0 + 0.6)], 16, mat, center=(cx, cy, 0))
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        bx(mb, (0.45 * r / 3, 0.45 * r / 3, hcol), (cx + 0.85 * r * math.cos(a), cy + 0.85 * r * math.sin(a), z0 + 0.6 + hcol / 2), mat)
    zt = z0 + 0.6 + hcol
    lathe_r(mb, [(0, zt), (r * 1.15, zt), (r * 1.15, zt + 0.5), (0, zt + 0.5)], 16, mat, center=(cx, cy, 0))
    st = len(mb.verts)
    onion(mb, zt + 0.5, r * 0.95, r * 1.6, mat, segs=20)
    L.transform_mb_range(mb, st, Matrix.Translation(Vector((cx, cy, 0))))
    lathe_r(mb, [(0, zt + 0.5 + r * 1.55), (0.12 * r, zt + 0.5 + r * 1.55), (0.06 * r, zt + 0.5 + r * 2.2), (0, zt + 0.5 + r * 2.25)],
            8, gold, center=(cx, cy, 0))


def build_domed_pavilion():
    L.reset()
    root = L.empty("Landmark_DomedPavilion", size=5)
    MAR, STONE, GOLD, DARK = 0, 1, 2, 3
    MATS = ["TS_Marble", "TS_Stone", "TS_Gold", "TS_MetalDark"]
    # main octagonal block with pishtaq frames, iwans cut by booleans
    bm = L.MB()
    H0, H1 = 5.0, 30.0
    S, CH = 28.0, 12.0
    octa = [(S - CH, -S), (S, -S + CH), (S, S - CH), (S - CH, S), (-S + CH, S), (-S, S - CH), (-S, -S + CH), (-S + CH, -S)]
    L.prism(bm, octa, H0, H1, mat=MAR)
    block = bm.build("Block", MATS)
    for rot in range(4):
        fm = L.MB()
        bx(fm, (20.0, 1.2, H1 - H0 + 2.5), (0, -S - 0.5, (H0 + H1 + 2.5) / 2), MAR)
        fr = fm.build("frame", MATS)
        fr.data.transform(Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
        L.boolean(block, fr, "UNION")
    for rot in range(4):
        L.boolean(block, arch_cutter(13.0, H0 - 0.1, 20.0, 5.0, S + 1.1, 90 * rot))
    d45 = (2 * S - CH) / math.sqrt(2)
    for rot in range(4):
        for (z0, zs) in ((H0 + 1.0, 11.0), (17.0, 23.0)):
            L.boolean(block, arch_cutter(6.0, z0, zs, 2.2, d45, 45 + 90 * rot))
    mb = L.MB()
    # platform with a plinth band
    bx(mb, (92.0, 92.0, H0), (0, 0, H0 / 2), STONE)
    bx(mb, (93.0, 93.0, 0.6), (0, 0, H0 - 0.3), STONE)
    # parapet band with small merlons
    for rot in range(4):
        st = len(mb.verts)
        bx(mb, (2 * (S - CH) + 0.6, 1.0, 1.2), (0, -S - 0.1, H1 + 0.6), MAR)
        bx(mb, (21.0, 1.4, 1.6), (0, -S - 0.6, H1 + 3.3), MAR)
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    # drum + onion dome + finial
    lathe_r(mb, [(0, H1), (12.0, H1), (12.0, 36.0), (12.6, 36.4), (12.6, 37.0), (0, 37.0)], 48, MAR)
    onion(mb, 37.0, 12.0, 22.5, MAR, segs=56)
    lathe_r(mb, [(0, 58.6), (0.9, 58.8), (1.0, 59.6), (0.5, 60.2), (0.75, 60.8), (0.75, 61.4), (0.3, 62.0), (0.2, 64.5),
                 (0, 65.0)], 16, GOLD)
    # four chhatris on the roof corners
    for (sx, sy) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        chhatri(mb, (sx * 17.5, sy * 17.5), 3.2, H1, 4.2, MAR, GOLD)
    # four minarets at the platform corners
    for (sx, sy) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        c = (sx * 41.0, sy * 41.0)
        lathe_r(mb, [(0, H0), (3.6, H0), (3.6, H0 + 1.4), (2.7, H0 + 1.8), (2.3, 44.0), (0, 44.0)], 24, MAR, center=(c[0], c[1], 0))
        for zb in (17.0, 29.0, 40.0):
            lathe_r(mb, [(0, zb - 0.9), (2.4, zb - 0.9), (3.4, zb), (3.5, zb + 0.6), (0, zb + 0.6)], 24, MAR, center=(c[0], c[1], 0))
            ring = [Vector((c[0] + 3.35 * math.cos(a), c[1] + 3.35 * math.sin(a), zb + 1.3)) for a in [2 * math.pi * i / 24 for i in range(24)]]
            L.sweep(mb, ring, L.circle2d(0.12, 4), mat=MAR, closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
        chhatri(mb, c, 2.3, 44.0, 3.2, MAR, GOLD)
    rest = mb.build("Rest", MATS)
    pav = L.join([block, rest], "Pavilion")
    finish(pav, root, 40.0)
    return done(root, "Landmark_DomedPavilion", [("Landmark_DomedPavilion.png", dict(view=(-0.6, -1.0, 0.3), lens=50))])


# ------------------------------------------------------------------ Clock tower (~100 m)
def build_clock_tower():
    L.reset()
    root = L.empty("Landmark_ClockTower", size=5)
    STONE, DARK, GOLD = 0, 1, 2
    MATS = ["TS_Stone", "TS_MetalDark", "TS_Gold"]
    mb = L.MB()
    HW = 6.0
    bx(mb, (2 * HW + 1.2, 2 * HW + 1.2, 2.0), (0, 0, 1.0), STONE)
    bx(mb, (2 * HW, 2 * HW, 54.0), (0, 0, 2.0 + 27.0), STONE)
    for rot in range(4):
        st = len(mb.verts)
        # corner buttresses and pilasters
        bx(mb, (1.5, 1.5, 54.0), (-HW, -HW, 29.0), STONE)
        for x in (-2.0, 2.0):
            bx(mb, (0.7, 0.5, 52.0), (x, -HW - 0.2, 29.0), STONE)
        # string courses
        for z in range(10, 56, 8):
            bx(mb, (2 * HW + 1.0, 0.6, 0.5), (0, -HW - 0.1, float(z)), STONE)
        # tall window panels between the pilasters
        for z0 in range(4, 52, 8):
            for x in (-4.0, 0.0, 4.0):
                bx(mb, (1.3 if x else 1.8, 0.12, 5.2), (x, -HW - 0.05, z0 + 3.0), DARK)
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    # clock stage (slightly wider) with gilded rings around the faces
    CW = 6.8
    bx(mb, (2 * CW, 2 * CW, 17.0), (0, 0, 56.0 + 8.5), STONE)
    FZ, FR = 64.5, 3.5
    for rot in range(4):
        st = len(mb.verts)
        bx(mb, (9.2, 0.6, 9.2), (0, -CW - 0.3, FZ), STONE)
        ring = [Vector((FR * 1.08 * math.cos(a), -CW - 0.75, FZ + FR * 1.08 * math.sin(a))) for a in [2 * math.pi * i / 48 for i in range(48)]]
        L.sweep(mb, ring, L.rrect(0.5, 0.35, 0.1, seg=1), mat=GOLD, closed_path=True, caps=False, fixed_binormal=(0, -1, 0))
        for (cx, cz) in ((-4.1, FZ + 4.1), (4.1, FZ + 4.1), (-4.1, FZ - 4.1), (4.1, FZ - 4.1)):
            A.superellipsoid(mb, (0.5, 0.25, 0.5), (cx, -CW - 0.6, cz), 0.6, 0.6, 10, 6, GOLD)
        # gable above the clock
        L.prism(mb, [(-4.8, 0.0), (4.8, 0.0), (0.0, 3.6)], -0.6, 0.0, mat=STONE,
                basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))), origin=(0, -CW, 73.0))
        for x in (-6.4, 6.4):
            lathe_r(mb, [(0, 73.0), (0.55, 73.0), (0.55, 74.5), (0, 77.0)], 8, STONE, center=(x, -CW, 0))
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    # belfry with arcade openings
    bx(mb, (2 * CW - 0.4, 2 * CW - 0.4, 7.0), (0, 0, 73.0 + 3.5), STONE)
    for rot in range(4):
        st = len(mb.verts)
        for x in (-3.8, 0.0, 3.8):
            L.prism(mb, pointed_arch(2.6, 74.0, 77.6, steps=6), -0.08, 0.1, mat=DARK,
                    basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))), origin=(x, -(CW - 0.2), 0))
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    # spire: lower pyramid, lantern stage, upper spire, pinnacles, finial
    loft(mb, [square_pts(CW + 0.2, 80.0, 1, 0.05), square_pts(3.2, 88.0, 1, 0.05)], DARK)
    bx(mb, (6.4, 6.4, 3.0), (0, 0, 89.5), STONE)
    for rot in range(4):
        st = len(mb.verts)
        for x in (-1.6, 1.6):
            L.prism(mb, pointed_arch(1.4, 88.4, 90.2, steps=4), -0.05, 0.1, mat=DARK,
                    basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))), origin=(x, -3.2, 0))
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(90 * rot), 4, "Z"))
    loft(mb, [square_pts(3.4, 91.0, 1, 0.05), square_pts(0.25, 99.0, 1, 0.02)], DARK)
    lathe_r(mb, [(0, 98.8), (0.3, 98.8), (0.4, 99.2), (0.15, 99.4), (0.08, 100.0), (0, 100.1)], 10, GOLD)
    for (sx, sy) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        bx(mb, (1.2, 1.2, 2.5), (sx * (CW - 0.2), sy * (CW - 0.2), 81.25), STONE)
        loft(mb, [square_pts(0.6, 82.5, 1, 0.02), square_pts(0.05, 86.5, 1, 0.01)], DARK, cap1=True)
        L.transform_mb_range(mb, len(mb.verts) - 10, Matrix.Translation(Vector((sx * (CW - 0.2), sy * (CW - 0.2), 0))))
        lathe_r(mb, [(0, 86.4), (0.12, 86.4), (0, 87.0)], 6, GOLD, center=(sx * (CW - 0.2), sy * (CW - 0.2), 0))
    tower = finish(mb.build("Tower", MATS), root, 35.0)
    # Faces: four clock discs (one mesh), 0..1 UVs per face (u to the viewer's right, v up)
    fm = L.MB()
    dirs = [(Vector((0, -1, 0)), Vector((1, 0, 0))), (Vector((1, 0, 0)), Vector((0, 1, 0))),
            (Vector((0, 1, 0)), Vector((-1, 0, 0))), (Vector((-1, 0, 0)), Vector((0, -1, 0)))]
    for (nrm, uax) in dirs:
        cen = nrm * (CW + 0.62) + Vector((0, 0, FZ))
        rim = []
        uvs = []
        for i in range(48):
            a = 2 * math.pi * i / 48
            p = cen + uax * (FR * math.cos(a)) + Vector((0, 0, FR * math.sin(a)))
            rim.append(fm.v(p))
            uvs.append((0.5 + 0.5 * math.cos(a), 0.5 + 0.5 * math.sin(a)))
        back = [fm.v(fm.verts[i] - nrm * 0.12) for i in rim]
        c0 = fm.v(cen)
        for i in range(48):
            i1 = (i + 1) % 48
            fm.f((c0, rim[i], rim[i1]), 0, [(0.5, 0.5), uvs[i], uvs[i1]])
            fm.f((rim[i1], rim[i], back[i], back[i1]), 0, [uvs[i1], uvs[i], uvs[i], uvs[i1]])
        c1 = fm.v(cen - nrm * 0.12)
        for i in range(48):
            i1 = (i + 1) % 48
            fm.f((c1, back[i1], back[i]), 0, [(0.5, 0.5), uvs[i1], uvs[i]])
    faces = fm.build("Faces", ["TS_EmissiveWarm"])
    finish(faces, root, 30.0, uv=False)
    return done(root, "Landmark_ClockTower", [("Landmark_ClockTower.png", dict(view=(-0.55, -1.0, 0.25), lens=50)),
                                              ("Landmark_ClockTower_top.png", dict(view=(-0.55, -1.0, 0.2), lens=50, margin=0.25,
                                                                                   target=(0, 0, 78.0), floor=False))])


# ------------------------------------------------------------------ Column gate (~26 m)
def horse(mb, c, mat, scale=1.0, rear=0.0):
    cx, cy, cz = c
    s = scale
    A.superellipsoid(mb, (0.42 * s, 1.15 * s, 0.55 * s), (cx, cy, cz + 1.75 * s), 0.7, 0.7, 12, 8, mat)
    neck = [Vector((cx, cy - 0.8 * s, cz + 2.0 * s)), Vector((cx, cy - 1.15 * s, cz + 2.55 * s + rear)),
            Vector((cx, cy - 1.35 * s, cz + 3.0 * s + rear))]
    L.sweep(mb, neck, L.circle2d(0.27 * s, 8), mat=mat, caps=True, up=(1, 0, 0), scale=lambda t: 1.2 - 0.4 * t)
    head = Vector((cx, cy - 1.6 * s, cz + 2.9 * s + rear))
    st = len(mb.verts)
    A.superellipsoid(mb, (0.16 * s, 0.42 * s, 0.2 * s), (0, 0, 0), 0.8, 0.8, 10, 6, mat)
    L.transform_mb_range(mb, st, Matrix.Translation(head) @ Matrix.Rotation(math.radians(-35), 4, "X"))
    for (lx, ly) in ((-0.22, -0.75), (0.22, -0.75), (-0.22, 0.8), (0.22, 0.8)):
        lift = rear * 0.6 if ly < 0 else 0.0
        beam(mb, (cx + lx * s, cy + ly * s, cz + 1.5 * s), (cx + lx * s, cy + (ly - 0.15 * (lift > 0)) * s, cz + lift * s), 0.16 * s, mat)
    tail = [Vector((cx, cy + 1.1 * s, cz + 1.9 * s)), Vector((cx, cy + 1.45 * s, cz + 1.3 * s)), Vector((cx, cy + 1.5 * s, cz + 0.7 * s))]
    L.sweep(mb, tail, L.circle2d(0.1 * s, 6), mat=mat, caps=True, up=(1, 0, 0))


def build_column_gate():
    L.reset()
    root = L.empty("Landmark_ColumnGate", size=5)
    STONE, MET = 0, 1
    MATS = ["TS_Stone", "TS_Metal"]
    mb = L.MB()
    W2, D2 = 19.5, 6.5
    bx(mb, (2 * W2 + 2.0, 2 * D2 + 2.0, 0.5), (0, 0, 0.25), STONE)
    bx(mb, (2 * W2 + 1.0, 2 * D2 + 1.0, 0.5), (0, 0, 0.75), STONE)
    Z0, ZC = 1.0, 13.0
    xs = [-17.2, -10.6, -3.6, 3.6, 10.6, 17.2]
    for x in xs:
        for y in (-5.0, 5.0):
            prof = [(0, Z0), (1.0, Z0), (1.0, Z0 + 0.25), (0.86, Z0 + 0.35)]
            for k in range(1, 7):
                z = Z0 + 0.35 + (ZC - Z0 - 0.35) * k / 6
                r = 0.86 - 0.12 * (k / 6) + 0.03 * math.sin(math.pi * k / 6)
                prof.append((r, z))
            prof += [(0.78, ZC + 0.12), (1.05, ZC + 0.5), (1.08, ZC + 0.62), (0, ZC + 0.62)]
            lathe_r(mb, prof, 40, STONE, center=(x, y, 0), rmod=knurl(list(range(3, 10)), 20, 0.06))
            bx(mb, (2.3, 2.3, 0.4), (x, y, ZC + 0.82), STONE)
        # passage walls between the front and back columns
        bx(mb, (1.4, 8.6, ZC - Z0 + 1.0), (x, 0, (Z0 + ZC + 1.0) / 2), STONE)
    ZE = ZC + 1.02
    bx(mb, (2 * W2 - 1.0, 2 * D2 - 0.6, 1.5), (0, 0, ZE + 0.75), STONE)
    bx(mb, (2 * W2 - 1.0, 2 * D2 - 0.6, 1.4), (0, 0, ZE + 2.2), STONE)
    for sy in (-1, 1):
        for j in range(23):
            x = -W2 + 0.9 + (2 * W2 - 1.8) * j / 22
            bx(mb, (0.55, 0.16, 1.2), (x, sy * (D2 - 0.25), ZE + 2.2), STONE)
    bx(mb, (2 * W2 + 0.4, 2 * D2 + 0.6, 0.35), (0, 0, ZE + 3.07), STONE)
    bx(mb, (2 * W2 + 0.8, 2 * D2 + 1.0, 0.35), (0, 0, ZE + 3.42), STONE)
    ZA = ZE + 3.6
    bx(mb, (2 * W2 - 3.0, 2 * D2 - 1.6, 2.4), (0, 0, ZA + 1.2), STONE)
    bx(mb, (2 * W2 - 2.6, 2 * D2 - 1.2, 0.3), (0, 0, ZA + 2.55), STONE)
    bx(mb, (16.0, 2 * D2 - 2.4, 0.9), (0, 0, ZA + 3.15), STONE)
    for rot in (0, 180):
        st = len(mb.verts)
        L.prism(mb, [(-8.0, 0.0), (8.0, 0.0), (6.6, 0.9), (-6.6, 0.9)], -0.3, 0.3, mat=STONE,
                basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))), origin=(0, -(D2 - 1.5), ZA + 3.6))
        L.transform_mb_range(mb, st, Matrix.Rotation(math.radians(rot), 4, "Z"))
    ZQ = ZA + 3.6
    bx(mb, (7.4, 3.6, 0.6), (0, 0.3, ZQ + 0.3), STONE)
    gate = mb.build("Gate", MATS)
    # quadriga: four horses, chariot, Victoria with a standard
    qm = L.MB()
    for k, hx in enumerate((-2.25, -0.75, 0.75, 2.25)):
        horse(qm, (hx, -0.4, ZQ + 0.6), 0, scale=0.85, rear=0.3 if k in (1, 2) else 0.12)
    lathe_r(qm, [(0, ZQ + 0.6), (1.4, ZQ + 0.6), (1.5, ZQ + 1.6), (1.2, ZQ + 2.1), (0, ZQ + 2.1)], 16, 0, center=(0, 1.3, 0))
    for sx in (-1, 1):
        lathe_r(qm, [(0, -0.1), (0.9, -0.1), (0.9, 0.1), (0, 0.1)], 16, 0, center=(sx * 1.5, 1.5, ZQ + 1.5), axis="X")
    lathe_r(qm, [(0, ZQ + 2.0), (0.55, ZQ + 2.0), (0.4, ZQ + 3.4), (0.3, ZQ + 4.2), (0, ZQ + 4.3)], 12, 0, center=(0, 1.3, 0))
    A.superellipsoid(qm, (0.25, 0.25, 0.3), (0, 1.25, ZQ + 4.55), 1.0, 1.0, 10, 6, 0)
    for sx in (-1, 1):
        L.prism(qm, [(sx * 0.08, 0.0), (sx * 1.6, 0.9), (sx * 1.2, 1.6), (sx * 0.25, 0.6)], -0.06, 0.06, mat=0,
                basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))), origin=(0, 1.55, ZQ + 3.4))
    beam(qm, (0.5, 1.0, ZQ + 2.6), (0.6, 0.9, ZQ + 4.8), 0.1, 0)
    ring = [Vector((0.6 + 0.35 * math.cos(a), 0.9, ZQ + 5.12 + 0.35 * math.sin(a))) for a in [2 * math.pi * i / 16 for i in range(16)]]
    L.sweep(qm, ring, L.circle2d(0.08, 5), mat=0, closed_path=True, caps=False, fixed_binormal=(0, 1, 0))
    quad = qm.build("Quadriga", ["TS_Metal"])
    land = L.join([gate, quad], "Gate")
    finish(land, root, 40.0)
    return done(root, "Landmark_ColumnGate", [("Landmark_ColumnGate.png", dict(view=(-0.5, -1.0, 0.25), lens=50)),
                                              ("Landmark_ColumnGate_top.png", dict(view=(-0.5, -1.0, 0.25), lens=50, margin=0.35,
                                                                                   target=(0, 0, 24.0), floor=False))])


# ------------------------------------------------------------------ Paris street lamp (4 m)
def build_street_lamp():
    L.reset()
    root = L.empty("StreetLamp_Paris", size=0.3)
    IRON = 0
    mb = L.MB()
    prof = [(0, 0.0), (0.26, 0.0), (0.26, 0.05), (0.235, 0.08), (0.235, 0.1), (0.21, 0.16), (0.19, 0.26), (0.165, 0.38),
            (0.14, 0.5), (0.12, 0.58), (0.15, 0.6), (0.15, 0.66), (0.11, 0.7), (0.1, 0.76), (0.085, 0.82)]
    for k in range(9):
        prof.append((0.072 - 0.014 * k / 8, 0.9 + 2.1 * k / 8))
    prof += [(0.07, 3.02), (0.09, 3.06), (0.09, 3.1), (0.07, 3.13), (0.06, 3.16), (0.0, 3.16)]
    lathe_r(mb, prof, 32, IRON, rmod=knurl(list(range(15, 24)) + [5, 6, 7], 16, 0.006))
    # ladder rest crossbar with ball ends
    beam(mb, (-0.32, 0, 2.9), (0.32, 0, 2.9), 0.035, IRON)
    for sx in (-1, 1):
        A.superellipsoid(mb, (0.035, 0.035, 0.035), (sx * 0.335, 0, 2.9), 1.0, 1.0, 10, 6, IRON)
    # decorative scrolls under the lantern
    for sx in (-1, 1):
        pts = [Vector((sx * (0.07 + 0.08 * math.sin(a)), 0.0, 2.95 + 0.17 * (1 - math.cos(a)) * 0.7 + 0.05 * a))
               for a in [math.pi * 1.4 * k / 14 for k in range(15)]]
        L.sweep(mb, pts, L.circle2d(0.012, 6), mat=IRON, caps=True, up=(0, 1, 0))
    # lantern frame: cup, hexagonal tapered frame, crown roof and finial
    lathe_r(mb, [(0, 3.12), (0.06, 3.12), (0.1, 3.18), (0.13, 3.24), (0.13, 3.27), (0, 3.27)], 12, IRON)
    zb, zt = 3.27, 3.72
    rb, rt = 0.125, 0.215
    bot = [Vector((rb * math.cos(a), rb * math.sin(a), zb)) for a in [math.pi / 6 + math.pi / 3 * i for i in range(6)]]
    top = [Vector((rt * math.cos(a), rt * math.sin(a), zt)) for a in [math.pi / 6 + math.pi / 3 * i for i in range(6)]]
    for i in range(6):
        beam(mb, bot[i], top[i], 0.022, IRON)
        beam(mb, bot[i], bot[(i + 1) % 6], 0.022, IRON)
        beam(mb, top[i], top[(i + 1) % 6], 0.026, IRON)
    lathe_r(mb, [(0, 3.71), (0.26, 3.71), (0.265, 3.74), (0.2, 3.8), (0.1, 3.88), (0.05, 3.91), (0.05, 3.94),
                 (0.03, 3.96), (0, 4.0)], 6, IRON)
    lathe_r(mb, [(0.0, 3.93), (0.022, 3.94), (0.0, 4.0)], 8, IRON)
    post = finish(mb.build("Post", ["TS_MetalDark"]), root, 35.0)
    gm = L.MB()
    gb = [Vector((0.118 * math.cos(a), 0.118 * math.sin(a), zb + 0.01)) for a in [math.pi / 6 + math.pi / 3 * i for i in range(6)]]
    gt = [Vector((0.205 * math.cos(a), 0.205 * math.sin(a), zt - 0.01)) for a in [math.pi / 6 + math.pi / 3 * i for i in range(6)]]
    loft(gm, [gb, gt], 0)
    lantern = finish(gm.build("Lantern", ["TS_EmissiveWarm"]), root, 20.0)
    L.set_origin(lantern, Vector((0, 0, (zb + zt) / 2)))
    return done(root, "StreetLamp_Paris", [("StreetLamp_Paris.png", dict(view=(-0.55, -1.0, 0.3), lens=50, margin=1.2))])


BUILDERS = {
    "Landmark_Eiffel": build_eiffel, "Landmark_Liberty": build_liberty, "Landmark_Pagoda": build_pagoda,
    "Landmark_ArchTower": build_arch_tower, "Landmark_DomedPavilion": build_domed_pavilion,
    "Landmark_ClockTower": build_clock_tower, "Landmark_ColumnGate": build_column_gate,
    "StreetLamp_Paris": build_street_lamp,
}

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name in (argv or list(BUILDERS)):
        BUILDERS[name]()
