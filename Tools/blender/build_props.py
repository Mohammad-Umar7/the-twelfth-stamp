"""
Props: VintageCamera, Noisemaker, HandLantern, GardenLamp, Reflector, VerificationDesk, VoidStamp,
FestivalToken, OpticalPanel.

Blender: metres, front faces -Y, +Z up. Origins noted per asset.
usage: blender -b --factory-startup --python build_props.py -- [VintageCamera ...]   (default: all)
"""
import sys
import os
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402
import build_apartment as A  # noqa: E402
import build_passport as P  # noqa: E402


# ------------------------------------------------------------------ helpers
def lathe_r(mb, prof, segs, mat, center=(0, 0, 0), axis="Z", rmod=None, smooth=True, closed=False):
    """Lathe with an optional radius modifier rmod(r, h, angle, row). Profiles touching the axis get poles;
    closed=True joins the last profile point back to the first (annulus profiles)."""
    c = Vector(center)
    rings, poles = [], []
    for idx, (r, h) in enumerate(prof):
        if r < 1e-9:
            rings.append([mb.v(c + L._axis_vec(0, 0, h, axis, None))])
            poles.append(True)
            continue
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            rr = rmod(r, h, a, idx) if rmod else r
            ring.append(mb.v(c + L._axis_vec(rr * math.cos(a), rr * math.sin(a), h, axis, None)))
        rings.append(ring)
        poles.append(False)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        if poles[k] and poles[k + 1]:
            continue
        for i in range(segs):
            i1 = (i + 1) % segs
            if poles[k]:
                mb.f((r0[0], r1[i1], r1[i]), mat, None, smooth)
            elif poles[k + 1]:
                mb.f((r0[i], r0[i1], r1[0]), mat, None, smooth)
            else:
                mb.f((r0[i], r0[i1], r1[i1], r1[i]), mat, None, smooth)
    if closed:
        mb.bridge([rings[-1], rings[0]], closed_ring=True, mat=mat, smooth=smooth)
        return rings
    if not poles[0]:
        mb.cap(rings[0], mat, flip=True)
    if not poles[-1]:
        mb.cap(rings[-1], mat)
    return rings


def knurl(rows, teeth, depth):
    rows = set(rows)

    def f(r, h, a, idx):
        if idx in rows:
            return r - depth * (0.5 + 0.5 * math.cos(teeth * a))
        return r
    return f


def scale_all(root, s):
    for o in L.descendants(root):
        if o.type == "MESH":
            o.data.transform(Matrix.Scale(s, 4))
        if o is not root:
            o.location = o.location * s


def finish(ob, root, smooth=40.0, uv=True):
    L.cleanup(ob, 1e-7)
    L.shade(ob, smooth, True)
    L.add_triangulate(ob, 5)
    L.add_weighted_normal(ob)
    L.apply_modifiers(ob)
    if uv:
        L.uv_smart(ob, angle=60.0)
    ob.parent = root
    return ob


def planar_uv(ob, axis_u, axis_v, u0, u1, v0, v1):
    """Planar 0..1 UVs: u = dot(co, axis_u) mapped u0..u1 -> 0..1 (same for v)."""
    me = ob.data
    uvl = me.uv_layers.active or me.uv_layers.new(name="UVMap")
    au, av = Vector(axis_u), Vector(axis_v)
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = ((co.dot(au) - u0) / (u1 - u0), (co.dot(av) - v0) / (v1 - v0))


# ------------------------------------------------------------------ VintageCamera (mm)
def build_camera():
    L.reset()
    root = L.empty("VintageCamera", size=20)
    LEA, CHR, GLS, DRK, FRO = 0, 1, 2, 3, 4
    MATS = ["TS_Leather", "TS_Chrome", "TS_Glass", "TS_MetalDark", "TS_PlasticWhite"]
    W, D = 138.0, 33.0
    outline = L.resample(L.rrect(W, D, 15.5, seg=10), 64)
    objs = []
    # body bands: bottom plate / leatherette / top plate
    for (z0, z1, mat, grow, bev) in ((0.0, 6.5, CHR, 0.4, 1.4), (6.5, 55.5, LEA, 0.0, 0.5), (55.5, 80.0, CHR, 0.4, 2.2)):
        mb = L.MB()
        pts = L.offset2d(outline, grow) if grow else outline
        L.prism(mb, pts, z0, z1, mat=mat)
        ob = mb.build("band", MATS)
        L.add_bevel(ob, bev, 3, 30.0)
        L.apply_modifiers(ob)
        objs.append(ob)
    mb = L.MB()
    yf = -D / 2 - 0.4
    xz = (Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0)))  # prisms standing out of the front face
    # front windows: viewfinder (+X), frosted illumination window, rangefinder (-X)
    for (cx, w, h, mat) in ((44.0, 18.0, 12.0, GLS), (22.5, 13.0, 10.0, FRO), (-44.0, 11.0, 10.0, GLS)):
        pts = L.rrect(w, h, 1.6, seg=3)
        L.prism(mb, pts, -0.5, 0.35, mat=DRK, basis=xz, origin=(cx, yf, 68.0))
        L.prism(mb, L.offset2d(pts, -1.0), 0.3, 0.6, mat=mat, basis=xz, origin=(cx, yf, 68.0))
        frame = [Vector((cx + x, yf - 0.45, 68.0 + z)) for (x, z) in L.resample(L.offset2d(pts, 0.6), 32)]
        L.sweep(mb, frame, L.rrect(1.1, 0.9, 0.3, seg=1), mat=CHR, closed_path=True, caps=False,
                fixed_binormal=(0, -1, 0))
    # lens mount + lens (along -Y)
    lx, lz = 6.0, 31.0
    lc = (lx, yf, lz)
    lathe_r(mb, [(0, 0.0), (24.5, 0.0), (24.5, 2.6), (22.0, 3.0), (0, 3.0)], 48, CHR, center=lc, axis="-Y")
    lathe_r(mb, [(0, 2.9), (21.0, 2.9), (21.4, 3.3), (21.4, 8.4), (21.0, 8.8), (0, 8.8)], 96, CHR, center=lc, axis="-Y",
            rmod=knurl([2, 3], 48, 0.55))
    lathe_r(mb, [(0, 8.7), (22.0, 8.7), (22.6, 9.3), (22.6, 16.4), (22.0, 17.0), (0, 17.0)], 96, CHR, center=lc,
            axis="-Y", rmod=knurl([2, 3], 48, 0.7))
    lathe_r(mb, [(0, 16.9), (19.6, 16.9), (19.6, 30.5), (20.6, 31.0), (20.6, 35.2), (20.0, 36.0), (16.4, 36.0),
                 (16.0, 35.4), (0, 35.4)], 64, CHR, center=lc, axis="-Y")
    lathe_r(mb, [(0, 33.0), (15.9, 33.0), (15.9, 35.5), (0, 35.5)], 48, DRK, center=lc, axis="-Y")
    lathe_r(mb, [(0, 35.6), (8.0, 35.4), (13.0, 34.9), (15.2, 34.3), (15.2, 33.8), (0, 33.8)], 48, GLS, center=lc,
            axis="-Y")
    # focus tab
    A.superellipsoid(mb, (3.0, 2.6, 2.8), (lx, yf - 12.8, lz - 24.2), 0.6, 0.6, 14, 8, CHR)
    # top plate controls
    zt = 80.0
    lathe_r(mb, [(0, 0.0), (10.0, 0.0), (10.0, 4.6), (9.4, 5.2), (0, 5.4)], 72, CHR, center=(-40.0, 2.0, zt),
            rmod=knurl([1, 2], 36, 0.5))
    lathe_r(mb, [(0, 5.3), (6.0, 5.3), (6.0, 5.8), (0, 5.8)], 32, DRK, center=(-40.0, 2.0, zt))
    lathe_r(mb, [(0, 0.0), (4.6, 0.0), (4.6, 2.6), (3.0, 2.8), (3.0, 4.6), (2.6, 5.2), (0, 5.3)], 24, CHR,
            center=(-57.0, 0.0, zt))
    # film advance lever around the release collar
    lev = [Vector((-57.0, 0.0, zt + 1.4)), Vector((-50.0, 6.5, zt + 1.4)), Vector((-36.0, 12.0, zt + 1.4)),
           Vector((-27.0, 12.5, zt + 1.4))]
    lev = L.polyline_resample3d([tuple(p) for p in A.bezier_chain(lev)], 14)
    L.sweep(mb, lev, L.rrect(1.5, 4.6, 0.5, seg=1), mat=CHR, caps=True, up=(0, 0, 1))
    lathe_r(mb, [(0, 0.0), (3.2, 0.0), (3.2, 2.6), (0, 3.4)], 16, DRK, center=(-27.0, 12.5, zt + 0.6))
    # rewind knob
    lathe_r(mb, [(0, 0.0), (5.0, 0.0), (5.0, 2.0), (9.5, 2.4), (9.5, 7.2), (8.8, 8.0), (0, 8.2)], 72, CHR,
            center=(48.0, 1.0, zt), rmod=knurl([3, 4], 36, 0.55))
    # accessory shoe
    L.box(mb, (20.0, 18.0, 0.8), center=(4.0, 1.0, zt + 0.4), mat=CHR)
    for sx in (-1, 1):
        L.box(mb, (20.0, 2.2, 2.6), center=(4.0, 1.0 + sx * 7.9, zt + 1.6), mat=CHR)
        L.box(mb, (20.0, 3.2, 0.6), center=(4.0, 1.0 + sx * 6.4, zt + 2.6), mat=CHR)
    # strap lugs
    for sx in (-1, 1):
        A.superellipsoid(mb, (1.6, 3.2, 3.0), (sx * (W / 2 + 0.6), 0.0, 63.0), 0.6, 0.6, 12, 8, CHR)
        L.sweep(mb, [Vector((sx * (W / 2 + 3.5 + 2.6 * math.cos(a)), 0.0, 63.0 + 2.6 * math.sin(a)))
                     for a in [2 * math.pi * k / 16 for k in range(16)]], L.circle2d(0.8, 6), mat=CHR,
                closed_path=True, caps=False, fixed_binormal=(0, 1, 0))
    detail = mb.build("detail", MATS)
    body = finish(L.join(objs + [detail], "Camera"), root, 35.0)
    scale_all(root, L.MM)
    return A.export_asset(root, "VintageCamera", [("VintageCamera.png", dict(view=(-0.6, -1.0, 0.5), lens=60)),
                                                  ("VintageCamera_back.png", dict(view=(0.5, 1.0, 0.7), lens=60, floor=False))])


# ------------------------------------------------------------------ Noisemaker (portable radio, mm)
def build_radio():
    L.reset()
    root = L.empty("Noisemaker", size=30)
    LEA, CHR, CLO, GLS, DRK, PLA, RUB = range(7)
    MATS = ["TS_Leather", "TS_Chrome", "TS_FabricDark", "TS_Glass", "TS_MetalDark", "TS_Plastic", "TS_Rubber"]
    W, D, H = 250.0, 90.0, 160.0
    xz = (Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0)))
    mb = L.MB()
    L.prism(mb, L.rrect(W, H, 20.0, seg=8), -D / 2, D / 2, mat=LEA, basis=xz, origin=(0, 0, H / 2))
    case = mb.build("case", MATS)
    L.add_bevel(case, 5.0, 4, 30.0)
    L.apply_modifiers(case)
    mb = L.MB()
    yf = -D / 2
    # chrome face plate
    face = L.rrect(232.0, 140.0, 13.0, seg=6)
    L.prism(mb, face, -0.2, 1.6, mat=CHR, basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))),
            origin=(0, yf, H / 2))
    yp = yf - 1.6
    # speaker: cloth panel + chrome louvres + surround
    gx, gz, gw, gh = -42.0, H / 2, 128.0, 112.0
    L.prism(mb, L.rrect(gw, gh, 8.0, seg=4), -0.1, 0.8, mat=CLO,
            basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))), origin=(gx, yp, gz))
    for k in range(11):
        z = gz - gh / 2 + 9.0 + k * (gh - 18.0) / 10
        path = [Vector((gx - gw / 2 + 4.0 + (gw - 8.0) * t / 8, yp - 2.2, z)) for t in range(9)]
        L.sweep(mb, path, L.rrect(2.6, 4.2, 1.0, seg=2), mat=CHR, caps=True, fixed_binormal=(0, 0, 1))
    sur = [Vector((gx + x, yp - 1.2, gz + z)) for (x, z) in L.resample(L.rrect(gw + 4.0, gh + 4.0, 10.0, seg=6), 64)]
    L.sweep(mb, sur, L.rrect(3.4, 3.0, 1.0, seg=1), mat=CHR, closed_path=True, caps=False, fixed_binormal=(0, -1, 0))
    # dial: bezel ring, glass, needle, centre knob (the dial face itself is the child "Dial")
    dx, dz, dr = 70.0, H / 2 + 8.0, 36.0
    ring = [Vector((dx + (dr + 2.0) * math.cos(a), yp - 1.6, dz + (dr + 2.0) * math.sin(a))) for a in
            [2 * math.pi * k / 64 for k in range(64)]]
    L.sweep(mb, ring, L.rrect(4.6, 4.0, 1.4, seg=2), mat=CHR, closed_path=True, caps=False, fixed_binormal=(0, -1, 0))
    lathe_r(mb, [(0, 2.9), (dr * 0.6, 2.75), (dr + 0.4, 2.3), (dr + 0.4, 1.6), (0, 1.6)], 48, GLS,
            center=(dx, yp, dz), axis="-Y")
    L.box(mb, (1.1, 0.5, dr - 6.0), center=(0, 0, 0), mat=DRK)
    st = len(mb.verts) - 8
    L.transform_mb_range(mb, st, Matrix.Translation(Vector((dx, yp - 1.1, dz))) @ Matrix.Rotation(math.radians(-38), 4, "Y")
                         @ Matrix.Translation(Vector((0, 0, (dr - 6.0) / 2))))
    lathe_r(mb, [(0, 3.2), (11.0, 3.2), (12.0, 4.0), (12.0, 13.0), (11.0, 14.0), (0, 14.4)], 64, CHR,
            center=(dx, yp, dz), axis="-Y", rmod=knurl([2, 3], 32, 0.6))
    lathe_r(mb, [(0, 14.3), (7.0, 14.3), (7.0, 14.8), (0, 14.8)], 24, DRK, center=(dx, yp, dz), axis="-Y")
    # volume knob on top (right)
    lathe_r(mb, [(0, 0.0), (10.0, 0.0), (10.5, 1.0), (10.5, 9.0), (9.5, 10.0), (0, 10.4)], 48, PLA,
            center=(96.0, 6.0, H - 1.0), rmod=knurl([2, 3], 24, 0.6))
    # strap handle with chrome brackets
    hp = [Vector((-82.0 + 164.0 * t, 0.0, H + 2.0 + 44.0 * math.sin(math.pi * t) ** 0.8)) for t in [k / 20 for k in range(21)]]
    L.sweep(mb, hp, L.rrect(4.0, 24.0, 1.6, seg=2), mat=LEA, caps=True, fixed_binormal=(0, 1, 0))
    for sx in (-1, 1):
        L.box(mb, (16.0, 30.0, 6.0), center=(sx * 82.0, 0.0, H + 1.0), mat=CHR)
        lathe_r(mb, [(0, -16.5), (3.0, -16.5), (3.0, 16.5), (0, 16.5)], 12, CHR, center=(sx * 82.0, 0.0, H + 5.0), axis="Y")
    # telescopic antenna (top-left back corner), leaning out and back
    base = Vector((-108.0, 28.0, H))
    lathe_r(mb, [(0, 0.0), (7.0, 0.0), (7.0, 4.0), (0, 4.0)], 20, CHR, center=base)
    A.superellipsoid(mb, (5.0, 5.0, 5.0), base + Vector((0, 0, 7.0)), 1.0, 1.0, 16, 10, CHR)
    d = Vector((-math.tan(math.radians(32)), math.tan(math.radians(10)), 1.0)).normalized()
    seg_r = [3.6, 3.0, 2.4]
    p0 = base + Vector((0, 0, 7.0))
    for k, r in enumerate(seg_r):
        a = p0 + d * (k * 88.0)
        b = p0 + d * (k * 88.0 + 96.0)
        L.sweep(mb, [a, b], L.circle2d(r, 12), mat=CHR, caps=True)
        L.sweep(mb, [b - d * 3.0, b], L.circle2d(r + 0.6, 12), mat=CHR, caps=True)
    A.superellipsoid(mb, (3.4, 3.4, 3.4), p0 + d * (2 * 88.0 + 98.0), 1.0, 1.0, 14, 8, CHR)
    # rubber feet
    for sx in (-1, 1):
        for sy in (-1, 1):
            lathe_r(mb, [(0, -3.5), (5.0, -3.5), (6.5, -2.0), (6.5, 0.5), (0, 0.5)], 16, RUB,
                    center=(sx * 95.0, sy * 28.0, 0.0))
    detail = mb.build("detail", MATS)
    radio = finish(L.join([case, detail], "Radio"), root, 35.0)
    # Dial face (child, emissive)
    dm = L.MB()
    lathe_r(dm, [(0, 0.4), (dr, 0.4), (dr, -0.6), (0, -0.6)], 64, 0, center=(dx, yp, dz), axis="-Y")
    dial = dm.build("Dial", ["TS_EmissiveWarm"])
    finish(dial, root, 30.0, uv=False)
    planar_uv(dial, (1, 0, 0), (0, 0, 1), dx - dr, dx + dr, dz - dr, dz + dr)
    # lift so the feet rest on the floor
    lift = -min(min(v.co.z for v in o.data.vertices) for o in (radio, dial))
    for o in (radio, dial):
        o.data.transform(Matrix.Translation(Vector((0, 0, lift))))
    scale_all(root, L.MM)
    return A.export_asset(root, "Noisemaker", [("Noisemaker.png", dict(view=(-0.55, -1.0, 0.45), lens=55))])


# ------------------------------------------------------------------ HandLantern (m)
def build_lantern():
    L.reset()
    root = L.empty("HandLantern", size=0.05)
    PAPER, WOOD = 0, 1
    MATS = ["TS_Paper", "TS_WoodDark"]
    z0, z1 = 0.045, 0.355
    NR = 15

    def base_r(t):
        return 0.064 + 0.056 * math.sin(math.pi * t) ** 0.75

    rows = []
    for k in range(NR * 4 + 1):
        t = k / (NR * 4)
        ph = (k % 4)
        rib = {0: 1.0, 1: 0.35, 2: 0.0, 3: 0.35}[ph]
        rows.append((base_r(t) * (1.0 + 0.012 * rib), z0 + (z1 - z0) * t))
    inner = [(base_r(t) - 0.0012, z0 + (z1 - z0) * t) for t in [k / 16 for k in range(16, -1, -1)]]
    mb = L.MB()
    segs = 40
    outer_rings = []
    for (r, z) in rows:
        outer_rings.append([mb.v((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z))
                            for i in range(segs)])
    inner_rings = []
    for (r, z) in inner:
        inner_rings.append([mb.v((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z))
                            for i in range(segs)])
    mb.bridge(outer_rings + inner_rings + [outer_rings[0]], closed_ring=True, mat=PAPER)
    # bamboo ribs as fine raised lines on the outside (every rib row)
    for k in range(1, NR):
        t = k / NR
        r = base_r(t) * 1.012 + 0.0004
        z = z0 + (z1 - z0) * t
        path = [Vector((r * math.cos(a), r * math.sin(a), z)) for a in [2 * math.pi * i / 48 for i in range(48)]]
        L.sweep(mb, path, L.circle2d(0.0011, 4), mat=PAPER, closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
    # wooden caps
    lathe_r(mb, [(0, 0.012), (0.074, 0.012), (0.077, 0.018), (0.077, 0.034), (0.071, 0.05), (0.06, 0.05),
                 (0.06, 0.03), (0, 0.03)], 40, WOOD)
    lathe_r(mb, [(0.06, 0.35), (0.071, 0.35), (0.077, 0.366), (0.077, 0.382), (0.074, 0.388), (0.056, 0.388),
                 (0.056, 0.36), (0.06, 0.36)], 40, WOOD, closed=True)
    # candle holder + candle
    lathe_r(mb, [(0, 0.03), (0.024, 0.03), (0.026, 0.036), (0.016, 0.04), (0.0, 0.04)], 20, WOOD)
    # bail handle + pivot studs + grip
    arc = [Vector((0.079 * math.cos(a), 0.0, 0.372 + 0.15 * math.sin(a))) for a in [math.pi * k / 24 for k in range(25)]]
    L.sweep(mb, arc, L.circle2d(0.0042, 10), mat=WOOD, caps=True, up=(0, 1, 0))
    for sx in (-1, 1):
        lathe_r(mb, [(0, 0.0), (0.008, 0.0), (0.008, 0.006), (0.0, 0.007)], 14, WOOD, center=(sx * 0.077, 0, 0.372), axis="X")
    lathe_r(mb, [(0, -0.052), (0.008, -0.052), (0.0125, -0.04), (0.014, 0.0), (0.0125, 0.04), (0.008, 0.052),
                 (0, 0.052)], 20, WOOD, center=(0, 0, 0.522), axis="X")
    lantern = mb.build("Lantern", MATS + ["TS_PlasticWhite"])
    cm = L.MB()
    lathe_r(cm, [(0, 0.04), (0.012, 0.04), (0.012, 0.085), (0.009, 0.088), (0.0, 0.088)], 16, 0)
    L.sweep(cm, [Vector((0, 0, 0.087)), Vector((0, 0, 0.096))], L.circle2d(0.0012, 6), mat=1, caps=True)
    candle = cm.build("Candle", ["TS_PlasticWhite", "TS_FabricDark"])
    lantern = finish(L.join([lantern, candle], "Lantern"), root, 40.0)
    gm = L.MB()
    lathe_r(gm, [(0, 0.098), (0.02, 0.104), (0.036, 0.13), (0.044, 0.17), (0.04, 0.21), (0.026, 0.24), (0.0, 0.252)], 24, 0)
    glow = finish(gm.build("Glow", ["TS_EmissiveWarm"]), root, 60.0)
    # origin: grip centre
    grip = Vector((0, 0, 0.522))
    for o in (lantern, glow):
        o.data.transform(Matrix.Translation(-grip))
    glow_c = Vector((0, 0, 0.175)) - grip
    L.set_origin(glow, glow_c)
    return A.export_asset(root, "HandLantern", [("HandLantern.png", dict(view=(-0.6, -1.0, 0.35), lens=55))])


# ------------------------------------------------------------------ GardenLamp (m; built ~1.4x then scaled 0.7)
def build_garden_lamp():
    L.reset()
    root = L.empty("GardenLamp", size=0.05)
    BR, GL, WK = 0, 1, 2
    MATS = ["TS_Brass", "TS_Glass", "TS_Fabric"]
    mb = L.MB()
    prof = [(0, 0.0), (0.056, 0.0), (0.059, 0.004), (0.057, 0.01), (0.046, 0.015), (0.03, 0.022), (0.022, 0.032),
            (0.02, 0.045), (0.028, 0.051), (0.028, 0.056), (0.02, 0.061), (0.03, 0.068), (0.052, 0.08), (0.066, 0.097),
            (0.069, 0.108), (0.064, 0.124), (0.048, 0.137), (0.032, 0.143), (0.03, 0.15), (0.034, 0.152),
            (0.034, 0.158), (0.03, 0.161), (0.0, 0.161)]
    lathe_r(mb, prof, 48, BR)
    # decorative bead rings on the font
    for z, r in ((0.108, 0.0695), (0.08, 0.0525)):
        L.sweep(mb, [Vector((r * math.cos(a), r * math.sin(a), z)) for a in [2 * math.pi * k / 48 for k in range(48)]],
                L.circle2d(0.0018, 6), mat=BR, closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
    # gallery (chimney holder) with prongs
    lathe_r(mb, [(0.031, 0.158), (0.038, 0.158), (0.038, 0.176), (0.036, 0.18), (0.034, 0.18), (0.034, 0.162),
                 (0.031, 0.162)], 40, BR, closed=True)
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        L.box(mb, (0.006, 0.0015, 0.016), center=(0.0375 * math.cos(a), 0.0375 * math.sin(a), 0.184), mat=BR,
              rot=L.rotz(math.degrees(a) + 90))
    # wick tube + wick
    L.box(mb, (0.016, 0.006, 0.014), center=(0, 0, 0.168), mat=BR)
    L.box(mb, (0.013, 0.0035, 0.008), center=(0, 0, 0.178), mat=WK)
    # wick knob (side, +X)
    L.sweep(mb, [Vector((0.03, 0, 0.155)), Vector((0.05, 0, 0.155))], L.circle2d(0.0022, 8), mat=BR, caps=True)
    lathe_r(mb, [(0, 0.0), (0.012, 0.0), (0.012, 0.006), (0.0, 0.006)], 32, BR, center=(0.05, 0, 0.155), axis="X",
            rmod=knurl([1, 2], 16, 0.0012))
    # side frame rods + top cap + carry ring
    for sx in (-1, 1):
        rod = [Vector((sx * 0.037, 0, 0.17)), Vector((sx * 0.055, 0, 0.2)), Vector((sx * 0.062, 0, 0.26)),
               Vector((sx * 0.056, 0, 0.33)), Vector((sx * 0.04, 0, 0.385))]
        L.sweep(mb, L.polyline_resample3d([tuple(p) for p in A.bezier_chain(rod)], 18), L.circle2d(0.0028, 8), mat=BR,
                caps=True, up=(0, 1, 0))
    lathe_r(mb, [(0.0, 0.392), (0.045, 0.392), (0.047, 0.386), (0.043, 0.38), (0.03, 0.38), (0.03, 0.372),
                 (0.027, 0.372), (0.027, 0.38), (0.0, 0.38)], 40, BR)
    lathe_r(mb, [(0, 0.391), (0.024, 0.391), (0.018, 0.404), (0.008, 0.41), (0.0, 0.411)], 32, BR)
    ring = [Vector((0.028 * math.cos(a), 0.0, 0.437 + 0.028 * math.sin(a))) for a in [2 * math.pi * k / 32 for k in range(32)]]
    L.sweep(mb, ring, L.circle2d(0.0045, 10), mat=BR, closed_path=True, caps=False, fixed_binormal=(0, 1, 0))
    # glass chimney (thin shell)
    outer = [(0.0315, 0.162), (0.0325, 0.19), (0.044, 0.215), (0.051, 0.245), (0.046, 0.285), (0.029, 0.322),
             (0.0262, 0.35), (0.0262, 0.371)]
    inner = [(r - 0.0022, z) for (r, z) in outer][::-1]
    segs = 40
    rings = []
    for (r, z) in outer + inner:
        rings.append([mb.v((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z))
                      for i in range(segs)])
    for j in range(len(rings) - 1):
        mb.bridge([rings[j], rings[j + 1]], closed_ring=True, mat=GL)
    mb.bridge([rings[-1], rings[0]], closed_ring=True, mat=GL)
    lamp = finish(mb.build("Lamp", MATS), root, 40.0)
    # Flame: teardrop, origin at its base (wick tip)
    fm = L.MB()
    lathe_r(fm, [(0, 0.0), (0.004, 0.002), (0.0075, 0.008), (0.008, 0.013), (0.0065, 0.02), (0.0035, 0.029),
                 (0.0, 0.038)], 20, 0)
    flame = fm.build("Flame", ["TS_EmissiveWarm"])
    flame.location = (0, 0, 0.182)
    finish(flame, root, 60.0)
    scale_all(root, 0.7)
    return A.export_asset(root, "GardenLamp", [("GardenLamp.png", dict(view=(-0.6, -1.0, 0.45), lens=60))])


# ------------------------------------------------------------------ Reflector (m)
def build_reflector():
    L.reset()
    root = L.empty("Reflector", size=0.2)
    DK, BR, CH, RB = 0, 1, 2, 3
    MATS = ["TS_MetalDark", "TS_Brass", "TS_Chrome", "TS_Rubber"]
    BEAR = 0.62
    MC = Vector((0, 0, 1.25))
    R = 0.45
    # Base
    bm = L.MB()
    lathe_r(bm, [(0, 0.012), (0.3, 0.012), (0.3, 0.035), (0.285, 0.05), (0.15, 0.075), (0.07, 0.09), (0.05, 0.11),
                 (0.042, 0.13), (0.04, 0.58), (0, 0.58)], 48, DK)
    lathe_r(bm, [(0, 0.57), (0.055, 0.57), (0.058, 0.585), (0.058, 0.612), (0.054, 0.62), (0, 0.62)], 40, BR)
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 2
        lathe_r(bm, [(0, 0.0), (0.03, 0.0), (0.032, 0.006), (0.03, 0.013), (0, 0.013)], 16, RB,
                center=(0.24 * math.cos(a), 0.24 * math.sin(a), 0.0))
    base = finish(bm.build("Base", MATS), root, 35.0)
    # Pivot: turntable, yoke, mirror
    pm = L.MB()
    lathe_r(pm, [(0, 0.62), (0.075, 0.62), (0.078, 0.63), (0.075, 0.65), (0, 0.65)], 40, BR)
    L.box(pm, (0.16, 0.08, 0.035), center=(0, 0, 0.665), mat=DK)
    yoke = [Vector((-0.5, 0, 1.25)), Vector((-0.5, 0, 0.8))]
    for k in range(1, 8):
        a = math.pi / 2 * k / 8
        yoke.append(Vector((-0.5 + 0.12 * (1 - math.cos(a)), 0, 0.8 - 0.12 * math.sin(a))))
    yoke += [Vector((-0.38, 0, 0.68)), Vector((0.38, 0, 0.68))]
    for k in range(1, 8):
        a = math.pi / 2 * (8 - k) / 8
        yoke.append(Vector((0.5 - 0.12 * (1 - math.cos(a)), 0, 0.8 - 0.12 * math.sin(a))))
    yoke += [Vector((0.5, 0, 0.8)), Vector((0.5, 0, 1.25))]
    L.sweep(pm, yoke, L.rrect(0.03, 0.06, 0.008, seg=2), mat=DK, caps=True, fixed_binormal=(0, 1, 0))
    for sx in (1, -1):
        st = len(pm.verts)
        lathe_r(pm, [(0, 0.0), (0.04, 0.0), (0.042, 0.006), (0.042, 0.03), (0.036, 0.04), (0, 0.04)], 32, BR,
                center=(0.48, 0, 1.25), axis="X")
        lathe_r(pm, [(0, 0.0), (0.03, 0.0), (0.033, 0.006), (0.033, 0.03), (0.025, 0.04), (0, 0.042)], 48, BR,
                center=(0.52, 0, 1.25), axis="X", rmod=knurl([2, 3], 24, 0.003))
        L.sweep(pm, [Vector((0.44, 0, 1.25)), Vector((0.485, 0, 1.25))], L.circle2d(0.018, 16), mat=BR, caps=True)
        if sx < 0:
            L.transform_mb_range(pm, st, Matrix.Scale(-1, 4, Vector((1, 0, 0))))
    # mirror: back shell + rim (brass), polished face (chrome)
    lathe_r(pm, [(0, 0.088), (0.06, 0.086), (0.07, 0.07), (0.12, 0.062), (0.2, 0.05), (0.205, 0.056), (0.215, 0.048),
                 (0.33, 0.032), (0.335, 0.038), (0.345, 0.03), (0.43, 0.018), (0.452, 0.006), (0.456, -0.01),
                 (0.448, -0.022), (0.43, -0.024), (0.422, -0.016), (0.422, -0.008), (0, -0.008)], 96, BR,
            center=MC, axis="Y")
    lathe_r(pm, [(0, -0.0125), (0.21, -0.0122), (0.4205, -0.0112), (0.4205, -0.007), (0, -0.007)], 96, CH,
            center=MC, axis="Y")
    pivot = finish(pm.build("Pivot", MATS), root, 35.0)
    L.set_origin(pivot, Vector((0, 0, BEAR)))
    return A.export_asset(root, "Reflector", [("Reflector.png", dict(view=(-0.6, -1.0, 0.3), lens=50)),
                                              ("Reflector_back.png", dict(view=(0.6, 1.0, 0.3), lens=50))])


# ------------------------------------------------------------------ OpticalPanel (m)
def build_optical_panel():
    L.reset()
    root = L.empty("OpticalPanel", size=0.2)
    DK, MT, GL, RB = 0, 1, 2, 3
    MATS = ["TS_MetalDark", "TS_Metal", "TS_Glass", "TS_Rubber"]
    BEAR = 0.83
    RC = Vector((0, 0, 1.255))
    bm = L.MB()
    lathe_r(bm, [(0, 0.01), (0.22, 0.01), (0.22, 0.03), (0.2, 0.042), (0.09, 0.06), (0.04, 0.075), (0.03, 0.1),
                 (0.03, 0.8), (0, 0.8)], 48, DK)
    lathe_r(bm, [(0, 0.79), (0.045, 0.79), (0.047, 0.8), (0.047, 0.825), (0.043, 0.83), (0, 0.83)], 32, MT)
    lathe_r(bm, [(0, 0.0), (0.21, 0.0), (0.21, 0.012), (0, 0.012)], 48, RB)
    base = finish(bm.build("Base", MATS), root, 35.0)
    pm = L.MB()
    lathe_r(pm, [(0, 0.83), (0.06, 0.83), (0.062, 0.838), (0.06, 0.85), (0, 0.85)], 32, MT)
    lathe_r(pm, [(0, 0.85), (0.022, 0.85), (0.022, 0.9), (0, 0.9)], 24, DK)
    L.box(pm, (0.09, 0.07, 0.035), center=(0, 0, 0.905), mat=DK)
    ring = [RC + Vector((0.338 * math.cos(a), 0, 0.338 * math.sin(a))) for a in [2 * math.pi * k / 96 for k in range(96)]]
    prof = [(-0.016, -0.024), (0.016, -0.024), (0.02, -0.018), (0.02, 0.018), (0.016, 0.024), (-0.016, 0.024),
            (-0.016, 0.006), (-0.022, 0.006), (-0.022, -0.006), (-0.016, -0.006)]
    L.sweep(pm, ring, prof, mat=MT, closed_path=True, caps=False, fixed_binormal=(0, -1, 0))
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        for sy in (-1, 1):
            lathe_r(pm, [(0, 0.0), (0.006, 0.0), (0.006, 0.002), (0.004, 0.0035), (0, 0.004)], 12, DK,
                    center=RC + Vector((0.338 * math.cos(a), sy * 0.024, 0.338 * math.sin(a))),
                    axis="-Y" if sy < 0 else "Y")
    rest = pm.build("PivotRest", MATS)
    finish(rest, root, 35.0)
    dm = L.MB()
    lathe_r(dm, [(0, -0.017), (0.16, -0.0145), (0.26, -0.0108), (0.3255, -0.0065), (0.3255, 0.0065), (0.26, 0.0108),
                 (0.16, 0.0145), (0, 0.017)], 96, 0, center=RC, axis="Y")
    disc = dm.build("Disc", ["TS_Glass"])
    finish(disc, root, 60.0, uv=False)
    planar_uv(disc, (1, 0, 0), (0, 0, 1), -0.3255, 0.3255, RC.z - 0.3255, RC.z + 0.3255)
    pivot = L.join([rest, disc], "Pivot")
    pivot.parent = root
    L.set_origin(pivot, Vector((0, 0, BEAR)))
    return A.export_asset(root, "OpticalPanel", [("OpticalPanel.png", dict(view=(-0.6, -1.0, 0.3), lens=50))])


# ------------------------------------------------------------------ VoidStamp (m)
def build_void_stamp():
    L.reset()
    root = L.empty("VoidStamp", size=0.05)
    WD, WL, MT, RB, FB = 0, 1, 2, 3, 4
    MATS = ["TS_WoodDark", "TS_Wood", "TS_Metal", "TS_Rubber", "TS_FabricDark"]
    DW, DD = 0.09, 0.06
    mb = L.MB()
    A.rrect_slab(mb, DW - 0.002, DD - 0.002, 0.004, 0.004, (0, 0, 0.007), mat=FB, plane="XY", seg=3)
    blk = L.MB()
    A.rrect_slab(blk, DW + 0.004, DD + 0.004, 0.03, 0.006, (0, 0, 0.024), mat=WD, plane="XY", seg=4)
    block = blk.build("block", MATS)
    L.add_bevel(block, 0.0025, 3, 40.0)
    L.apply_modifiers(block)
    A.rrect_slab(mb, 0.07, 0.042, 0.0012, 0.003, (0, 0, 0.0395), mat=MT, plane="XY", seg=3)
    lathe_r(mb, [(0, 0.039), (0.021, 0.039), (0.021, 0.042), (0.017, 0.044), (0.017, 0.056), (0.0185, 0.058),
                 (0.0185, 0.062), (0, 0.062)], 40, MT)
    lathe_r(mb, [(0, 0.061), (0.0145, 0.061), (0.012, 0.072), (0.011, 0.085), (0.015, 0.1), (0.024, 0.118),
                 (0.028, 0.13), (0.027, 0.14), (0.021, 0.148), (0.01, 0.152), (0, 0.153)], 48, WL)
    body = finish(L.join([block, mb.build("rest", MATS)], "Body"), root, 40.0)
    fm = L.MB()
    A.rrect_slab(fm, DW - 0.004, DD - 0.004, 0.005, 0.0035, (0, 0, 0.0025), mat=0, plane="XY", seg=3)
    face = fm.build("Face", ["TS_Rubber"])
    finish(face, root, 30.0, uv=False)
    planar_uv(face, (1, 0, 0), (0, -1, 0), -(DW - 0.004) / 2, (DW - 0.004) / 2, -(DD - 0.004) / 2, (DD - 0.004) / 2)
    return A.export_asset(root, "VoidStamp", [("VoidStamp.png", dict(view=(-0.6, -1.0, 0.55), lens=60))])


# ------------------------------------------------------------------ FestivalToken (mm)
def build_token():
    L.reset()
    root = L.empty("FestivalToken", size=10)
    mb = L.MB()
    # body: fields at |y| = 0.9, rim to |y| = 1.3, reeded edge (60 reeds)
    prof = [(0, -0.9), (13.8, -0.9), (14.4, -1.0), (14.6, -1.3), (15.6, -1.3), (16.0, -0.95), (16.0, 0.95),
            (15.6, 1.3), (14.6, 1.3), (14.4, 1.0), (13.8, 0.9), (0, 0.9)]
    lathe_r(mb, prof, 120, 0, axis="Y", rmod=knurl([5, 6], 60, 0.14))
    # obverse (front, -Y): compass rose + ring + lens boss
    tips = [10.5, 4.6, 11.8, 4.6, 10.5, 4.6, 11.8, 4.6]
    P.raised_poly(mb, P.star_poly(tips, 2.3), 1.22, 1.02, 0.85)
    ring = [Vector((12.6 * math.cos(a), -0.9, 12.6 * math.sin(a))) for a in [2 * math.pi * k / 64 for k in range(64)]]
    L.sweep(mb, ring, [(-0.3, 0.05), (0.3, 0.05), (0.22, -0.25), (-0.22, -0.25)], mat=0, closed_path=True, caps=False,
            fixed_binormal=(0, 1, 0))
    lathe_r(mb, [(0, -1.27), (1.2, -1.25), (2.0, -1.2), (2.3, -1.12), (2.3, -1.0), (0, -1.0)], 24, 0, axis="Y")
    # reverse (+Y): central seal (ring + boss) and twelve small seals around it
    lathe_r(mb, [(0, 1.28), (2.6, 1.25), (3.0, 1.15), (3.0, 1.0), (0, 1.0)], 32, 0, axis="Y")
    ring2 = [Vector((5.0 * math.cos(a), 0.9, 5.0 * math.sin(a))) for a in [2 * math.pi * k / 48 for k in range(48)]]
    L.sweep(mb, ring2, [(-0.32, -0.05), (0.32, -0.05), (0.24, 0.3), (-0.24, 0.3)], mat=0, closed_path=True, caps=False,
            fixed_binormal=(0, 1, 0))
    for k in range(12):
        a = 2 * math.pi * k / 12 + math.pi / 2
        lathe_r(mb, [(0, 1.24), (0.9, 1.21), (1.3, 1.12), (1.35, 1.0), (1.35, 0.85), (0, 0.85)], 16, 0, axis="Y",
                center=(9.6 * math.cos(a), 0.0, 9.6 * math.sin(a)))
    coin = finish(mb.build("Token", ["TS_Brass"]), root, 30.0)
    scale_all(root, L.MM)
    return A.export_asset(root, "FestivalToken", [("FestivalToken.png", dict(view=(-0.45, -1.0, 0.35), lens=60, floor=False)),
                                                  ("FestivalToken_back.png", dict(view=(0.45, 1.0, 0.35), lens=60, floor=False))])


# ------------------------------------------------------------------ VerificationDesk (m)
def build_ledger():
    mb = L.MB()
    PWd, PHt, ST = 0.19, 0.27, 0.012

    def curl(x):
        ax = abs(x)
        y = 0.0
        if ax < 0.035:
            y = 0.0105 * (1 - ax / 0.035) ** 2.2
        return y
    for s in (-1, 1):
        xs = [s * (0.003 + (PWd - 0.003) * (k / 16) ** 1.4) for k in range(17)]
        if s < 0:
            xs = xs[::-1]
        L.ruled_slab(mb, xs, lambda x: -PHt / 2, lambda x: PHt / 2, lambda x: curl(x), lambda x: ST,
                     mats=(0, 0, 1, 1, 1, 1), rows=1)
    xs = L.corner_xs(-PWd - 0.012, PWd + 0.012, 0.004, 0.004, n_corner=3, step=0.03)
    L.ruled_slab(mb, xs, lambda x: -(PHt / 2 + 0.008), lambda x: PHt / 2 + 0.008, lambda x: ST + 0.0003,
                 lambda x: ST + 0.0035, mats=(2, 2, 2, 2, 2, 2), rows=1)
    # ribbon bookmark hanging out of the bottom
    rib = [Vector((0.006, 0.004, -PHt / 2 + 0.03)), Vector((0.008, 0.002, -PHt / 2 - 0.004)),
           Vector((0.01, 0.011, -PHt / 2 - 0.018)), Vector((0.014, 0.0152, -PHt / 2 - 0.05))]
    L.sweep(mb, L.polyline_resample3d([tuple(p) for p in A.bezier_chain(rib)], 10), L.rrect(0.006, 0.0006, 0.0002, seg=1),
            mat=3, caps=True, up=(0, -1, 0))
    st = 0
    # lie flat: pages up (+Z), head away from the visitor (+Y)
    L.transform_mb_range(mb, st, Matrix.Translation(Vector((0, 0, ST + 0.0035))) @ Matrix.Rotation(math.radians(-90), 4, "X"))
    ob = mb.build("Ledger", ["TS_Paper", "TS_PaperEdge", "TS_Leather", "TS_Fabric"])
    return ob


def build_desk():
    L.reset()
    root = L.empty("VerificationDesk", size=0.3)
    W, Dp, H = 1.6, 0.75, 0.76
    parts = []
    parts.append(A.bbox_obj("top", (W, Dp, 0.04), (0, 0, H - 0.02), "TS_Wood", bevel=0.007, seg=3))
    fy = -Dp / 2 + 0.075
    parts.append(A.bbox_obj("front", (W - 0.08, 0.03, 0.66), (0, fy, 0.06 + 0.33), "TS_WoodDark", bevel=0.003))
    for k in range(3):
        cx = (k - 1) * 0.49
        parts.append(A.bbox_obj("inset", (0.43, 0.012, 0.5), (cx, fy - 0.02, 0.4), "TS_Wood", bevel=0.006, seg=3))
    for sx in (-1, 1):
        parts.append(A.bbox_obj("side", (0.03, Dp - 0.14, 0.66), (sx * (W / 2 - 0.055), 0.0, 0.39), "TS_WoodDark", bevel=0.003))
    parts.append(A.bbox_obj("plinth", (W - 0.14, Dp - 0.2, 0.06), (0, 0.01, 0.03), "TS_WoodDark", bevel=0.002))
    # brass trim strips on the front
    tm = L.MB()
    for z in (0.705, 0.075):
        L.box(tm, (W - 0.08, 0.006, 0.012), center=(0, fy - 0.018, z), mat=0)
    # drawer pedestal on the official's side (+Y), with brass knobs
    ped = A.bbox_obj("pedestal", (0.42, 0.5, 0.66), (0.52, 0.06, 0.06 + 0.33), "TS_WoodDark", bevel=0.003)
    parts.append(ped)
    dm = L.MB()
    for k, (z0, z1) in enumerate(((0.07, 0.3), (0.305, 0.5), (0.505, 0.695))):
        L.box(dm, (0.4, 0.018, z1 - z0 - 0.004), center=(0.52, 0.31 + 0.009, (z0 + z1) / 2), mat=0)
        lathe_r(tm, [(0, 0.0), (0.008, 0.0), (0.006, 0.01), (0.012, 0.018), (0.011, 0.024), (0, 0.025)], 16, 0,
                center=(0.52, 0.328, (z0 + z1) / 2), axis="Y")
    dob = dm.build("drawers", ["TS_Wood"])
    L.add_bevel(dob, 0.003, 2, 40.0)
    L.apply_modifiers(dob)
    parts.append(dob)
    parts.append(tm.build("trim", ["TS_Brass"]))
    # festival cloth runner draped over the front edge, with a gold hem
    cm = L.MB()
    yt, ye = -0.25, -Dp / 2
    path = [(Vector((0, yt + (ye - yt) * t, H + 0.0024))) for t in [k / 8 for k in range(9)]]
    for k in range(1, 7):
        a = math.pi / 2 * k / 6
        path.append(Vector((0, ye - 0.0072 * math.sin(a), H + 0.0024 - 0.0072 * (1 - math.cos(a)))))
    drop0 = path[-1]
    for k in range(1, 13):
        path.append(drop0 + Vector((0, -0.0015 * k / 12, -0.36 * k / 12)))
    acc = [0.0]
    for i in range(1, len(path)):
        acc.append(acc[-1] + (path[i] - path[i - 1]).length)
    nu = 16
    grid = []
    for j, p in enumerate(path):
        row = []
        for i in range(nu + 1):
            u = i / nu
            x = -0.31 + 0.62 * u
            pp = p.copy()
            pp.x = x
            if j >= len(path) - 12:
                hem = 0.085 * (1 - abs(2 * u - 1))
                frac = (j - (len(path) - 13)) / 12
                pp.z -= hem * frac
                pp.y -= 0.004 * math.sin(math.pi * u * 3) * frac
            row.append(cm.v(pp))
        grid.append(row)
    for j in range(len(grid) - 1):
        for i in range(nu):
            cm.f((grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]), 0)
    cloth = cm.build("cloth", ["TS_Fabric", "TS_Gold"], recalc=False)
    sol = cloth.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = 0.002
    sol.offset = -1.0
    L.apply_modifiers(cloth)
    L.recalc_normals(cloth)
    hm = L.MB()
    last = path[-1]
    hem_path = []
    for i in range(nu * 2 + 1):
        u = i / (nu * 2)
        hem_path.append(Vector((-0.31 + 0.62 * u, last.y - 0.002 - 0.004 * math.sin(math.pi * u * 3),
                                last.z - 0.085 * (1 - abs(2 * u - 1)) + 0.002)))
    L.sweep(hm, hem_path, L.rrect(0.006, 0.01, 0.002, seg=1), mat=1, caps=True, fixed_binormal=(0, 1, 0))
    for i in range(0, nu * 2 + 1, 2):
        u = i / (nu * 2)
        p = hem_path[i]
        L.sweep(hm, [p, p + Vector((0, 0, -0.03))], L.circle2d(0.0016, 5), mat=1, caps=True)
    parts += [cloth, hm.build("hem", ["TS_Fabric", "TS_Gold"])]
    desk = A.finish_join(parts, "Desk", root, smooth=40.0)
    # ---- props on the desk (separate children, origin at their own base centre)
    led = build_ledger()
    led.location = (-0.27, 0.01, H)
    led.data.transform(Matrix.Rotation(math.radians(6), 4, "Z"))
    finish(led, root, 35.0)
    # ink pad: tin + felt + open lid
    im = L.MB()
    A.rrect_slab(im, 0.11, 0.075, 0.014, 0.008, (0, 0, 0.007), mat=0, plane="XY", seg=4)
    A.rrect_slab(im, 0.1, 0.065, 0.002, 0.006, (0, 0, 0.0145), mat=1, plane="XY", seg=4)
    st = len(im.verts)
    A.rrect_slab(im, 0.112, 0.077, 0.004, 0.008, (0, 0.0, 0.016), mat=0, plane="XY", seg=4)
    A.rrect_slab(im, 0.08, 0.045, 0.001, 0.004, (0, 0.0, 0.0185), mat=0, plane="XY", seg=3)
    hinge = Vector((0, 0.0385, 0.014))
    L.transform_mb_range(im, st, Matrix.Translation(hinge) @ Matrix.Rotation(math.radians(-100), 4, "X")
                         @ Matrix.Translation(-hinge))
    pad = im.build("InkPad", ["TS_Metal", "TS_FabricDark"])
    pad.location = (0.2, 0.0, H)
    finish(pad, root, 35.0)
    # brass hand stamp
    sm = L.MB()
    lathe_r(sm, [(0, 0.0), (0.021, 0.0), (0.021, 0.004), (0, 0.004)], 32, 2)
    lathe_r(sm, [(0, 0.004), (0.024, 0.004), (0.024, 0.012), (0.02, 0.017), (0.011, 0.02), (0.009, 0.03), (0.011, 0.034),
                 (0, 0.034)], 32, 0)
    lathe_r(sm, [(0, 0.033), (0.008, 0.033), (0.007, 0.045), (0.013, 0.06), (0.019, 0.075), (0.02, 0.085),
                 (0.017, 0.094), (0.009, 0.099), (0, 0.1)], 32, 1)
    stamp = sm.build("HandStamp", ["TS_Brass", "TS_WoodDark", "TS_Rubber"])
    stamp.location = (0.34, 0.09, H)
    finish(stamp, root, 40.0)
    # small brass desk lamp with a warm bulb
    lm = L.MB()
    lathe_r(lm, [(0, 0.0), (0.07, 0.0), (0.072, 0.006), (0.06, 0.02), (0.02, 0.026), (0.0, 0.026)], 40, 0)
    L.sweep(lm, [Vector((0, 0, 0.02)), Vector((0, 0, 0.33))], L.circle2d(0.008, 14), mat=0, caps=True)
    arm = [Vector((0, 0, 0.33)), Vector((0, 0, 0.345)), Vector((-0.03, 0, 0.36)), Vector((-0.13, 0, 0.36))]
    L.sweep(lm, L.polyline_resample3d([tuple(p) for p in A.bezier_chain(arm)], 12), L.circle2d(0.0065, 12), mat=0,
            caps=True, up=(0, 1, 0))
    st = len(lm.verts)
    shade_out = [(0.012, 0.0), (0.02, -0.01), (0.045, -0.045), (0.072, -0.085), (0.074, -0.09)]
    shade_in = [(r - 0.0015, z) for (r, z) in shade_out][::-1]
    segs = 40
    rings = []
    for (r, z) in shade_out + shade_in:
        rings.append([lm.v((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z))
                      for i in range(segs)])
    for j in range(len(rings) - 1):
        lm.bridge([rings[j], rings[j + 1]], closed_ring=True, mat=0 if j < len(shade_out) - 1 else 1)
    lm.bridge([rings[-1], rings[0]], closed_ring=True, mat=0)
    L.transform_mb_range(lm, st, Matrix.Translation(Vector((-0.13, 0, 0.36))) @ Matrix.Rotation(math.radians(12), 4, "Y"))
    A.superellipsoid(lm, (0.022, 0.022, 0.028), (-0.135, 0.0, 0.31), 1.0, 1.0, 20, 12, 2)
    lamp = lm.build("Lamp", ["TS_Brass", "TS_Metal", "TS_EmissiveWarm"])
    lamp.location = (0.64, 0.2, H)
    finish(lamp, root, 40.0)
    return A.export_asset(root, "VerificationDesk", [("VerificationDesk.png", dict(view=(-0.55, -1.0, 0.6), lens=50)),
                                                     ("VerificationDesk_top.png", dict(view=(0.15, 0.6, 1.0), lens=50, margin=0.55,
                                                                                       target=(0.15, 0.0, 0.8), floor=False))])


BUILDERS = {
    "VintageCamera": build_camera, "Noisemaker": build_radio, "HandLantern": build_lantern,
    "GardenLamp": build_garden_lamp, "Reflector": build_reflector, "VerificationDesk": build_desk,
    "VoidStamp": build_void_stamp, "FestivalToken": build_token, "OpticalPanel": build_optical_panel,
}

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name in (argv or list(BUILDERS)):
        BUILDERS[name]()
