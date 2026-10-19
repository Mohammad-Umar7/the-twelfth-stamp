"""
Glasses_Open.fbx / Glasses_Folded.fbx  (reference A)

Built in millimetres, converted to metres before export.
Blender axes: +X = wearer's LEFT (front view: screen right), -Y = front (lens outer face), +Z up.
Origin = centre of the arched nose bridge on the frame mid-plane.

Objects (children of the root empty):
  Frame   - rims, top bar, arched bridge, nose pad arms + pads, end pieces, hinge blocks
            slots TS_Gunmetal, TS_NosePad. RIGHT end piece (wearer's right, -X) carries the notch.
  TempleL / TempleR - origin on the vertical hinge axis; slots TS_Gunmetal, TS_TempleTip
  LensL / LensR     - origin at the lens centre; slot TS_Lens; planar 0..1 UVs (front view)
"""
import sys
import os
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402

# ---------------------------------------------------------------- dimensions (mm)
LENS_W, LENS_H = 52.0, 42.0
DBL = 19.0                      # distance between lenses
LCX = DBL / 2 + LENS_W / 2      # lens centre x (35.5)
LCZ = -9.0                      # lens centre z (bridge centre is z=0)
LENS_T = 1.8                    # lens thickness
LENS_BULGE = 1.5                # meniscus bulge toward -Y at the centre
RIM_IN, RIM_OUT, RIM_D = 0.35, 1.75, 2.6   # rim profile: overlap into lens, outward width, depth
N_EDGE = 60                     # samples along the lens outline
HINGE = Vector((66.6, 3.3, 5.6))  # +X hinge axis point (vertical axis)


def lens_outline():
    """Outline of the +X lens in (x, z), CCW seen from the front.
    Softly squared: a squarer super-ellipse on top, rounder below, slight taper toward
    the bottom-outer corner (reference A)."""
    a, b = LENS_W / 2, LENS_H / 2
    out = []
    M = 720
    for i in range(M):
        t = 2 * math.pi * i / M
        c, s = math.cos(t), math.sin(t)
        n = 4.6 if s >= 0 else 2.75
        x = a * math.copysign(abs(c) ** (2 / n), c)
        z = b * math.copysign(abs(s) ** (2 / n), s)
        if z < 0:
            k = -z / b
            x *= (1 - 0.045 * k) if x > 0 else (1 - 0.012 * k)
            z -= 1.0 * max(0.0, 1 - (x / a) ** 2) * k   # gentle sag on the bottom edge
        out.append((LCX + x, LCZ + z))
    out = L.resample(L.ensure_ccw(out), N_EDGE, closed=True)
    # start the loop at the bottom-centre so seams hide underneath
    k = min(range(len(out)), key=lambda i: (out[i][1], abs(out[i][0] - LCX)))
    return out[k:] + out[:k]


def to3(p2, y=0.0):
    return Vector((p2[0], y, p2[1]))


# ---------------------------------------------------------------- pieces
def build_lens(name, mirror, parent):
    outline = lens_outline()
    mb = L.MB()
    scales = [1.0, 0.68, 0.34]
    rings_f, rings_b = [], []
    for s in scales:
        bulge = LENS_BULGE * (1 - s * s)
        rf, rb = [], []
        for (x, z) in outline:
            px = LCX + (x - LCX) * s
            pz = LCZ + (z - LCZ) * s
            rf.append(mb.v((px, -LENS_T / 2 - bulge, pz)))
            rb.append(mb.v((px, LENS_T / 2 - bulge, pz)))
        rings_f.append(rf)
        rings_b.append(rb)
    n = len(outline)

    def uv_of(i):
        x, z = outline[i % n]
        return ((x - (LCX - LENS_W / 2)) / LENS_W, (z - (LCZ - LENS_H / 2 - 1.2)) / (LENS_H + 1.6))

    # front surface (faces -Y): rings from outer to inner
    for j in range(len(scales) - 1):
        a, b = rings_f[j], rings_f[j + 1]
        s0, s1 = scales[j], scales[j + 1]
        for i in range(n):
            i1 = (i + 1) % n
            def uvs(idx, s):
                u, v = uv_of(idx)
                return (0.5 + (u - 0.5) * s, 0.5 + (v - 0.5) * s)
            mb.f((a[i], b[i], b[i1], a[i1]), 0, [uvs(i, s0), uvs(i, s1), uvs(i1, s1), uvs(i1, s0)])
    cf = mb.v((LCX, -LENS_T / 2 - LENS_BULGE, LCZ))
    last = rings_f[-1]
    for i in range(n):
        i1 = (i + 1) % n
        u0, v0 = uv_of(i)
        u1, v1 = uv_of(i1)
        s = scales[-1]
        mb.f((last[i], cf, last[i1]), 0,
             [(0.5 + (u0 - .5) * s, 0.5 + (v0 - .5) * s), (0.5, 0.5), (0.5 + (u1 - .5) * s, 0.5 + (v1 - .5) * s)])
    # back surface (faces +Y)
    for j in range(len(scales) - 1):
        a, b = rings_b[j], rings_b[j + 1]
        s0, s1 = scales[j], scales[j + 1]
        for i in range(n):
            i1 = (i + 1) % n
            def uvs(idx, s):
                u, v = uv_of(idx)
                return (0.5 + (u - 0.5) * s, 0.5 + (v - 0.5) * s)
            mb.f((a[i1], b[i1], b[i], a[i]), 0, [uvs(i1, s0), uvs(i1, s1), uvs(i, s1), uvs(i, s0)])
    cb = mb.v((LCX, LENS_T / 2 - LENS_BULGE, LCZ))
    last = rings_b[-1]
    for i in range(n):
        i1 = (i + 1) % n
        u0, v0 = uv_of(i)
        u1, v1 = uv_of(i1)
        s = scales[-1]
        mb.f((last[i1], cb, last[i]), 0,
             [(0.5 + (u1 - .5) * s, 0.5 + (v1 - .5) * s), (0.5, 0.5), (0.5 + (u0 - .5) * s, 0.5 + (v0 - .5) * s)])
    # edge band
    for i in range(n):
        i1 = (i + 1) % n
        u0, v0 = uv_of(i)
        u1, v1 = uv_of(i1)
        mb.f((rings_f[0][i], rings_f[0][i1], rings_b[0][i1], rings_b[0][i]), 0,
             [(u0, v0), (u1, v1), (u1, v1), (u0, v0)], smooth=False)
    ob = mb.build(name, ["TS_Lens"], parent=parent)
    L.merge_by_distance(ob, 1e-5)
    if mirror:
        mirror_x(ob)
    L.set_origin(ob, Vector((-LCX if mirror else LCX, -LENS_BULGE / 2, LCZ)))
    # smooth the faces, keep the edge band crisp
    L.shade(ob, 30.0, True)
    L.add_triangulate(ob, 5)
    L.add_weighted_normal(ob)
    L.apply_modifiers(ob)
    return ob


def mirror_x(ob):
    ob.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
    ob.data.flip_normals()
    ob.location.x = -ob.location.x


def rim_profile():
    a0, a1 = -RIM_IN, RIM_OUT
    b0, b1 = -RIM_D / 2, RIM_D / 2
    c = 0.32
    g0, g1 = a0 + 0.95, a0 + 1.55  # groove on the front face
    # (a = outward, b = toward +Y/back); CCW in (a, b)
    return [
        (a0, b0), (g0, b0), ((g0 + g1) / 2, b0 + 0.3), (g1, b0), (a1 - c, b0),
        (a1, b0 + c), (a1, b1 - c), (a1 - c, b1), (a0, b1),
    ]


def build_rim(mb, mirror):
    pts = [to3(p) for p in lens_outline()]
    if mirror:
        pts = [Vector((-p.x, p.y, p.z)) for p in pts][::-1]
    L.sweep(mb, pts, rim_profile(), mat=0, closed_path=True, caps=False, fixed_binormal=(0, 1, 0))


def build_top_bar(mb):
    xe = LCX - LENS_W / 2 + 17.0
    ztop = LCZ + LENS_H / 2 + 0.35 + 1.35
    path = []
    for i in range(9):
        t = i / 8
        x = -xe + 2 * xe * t
        # ends dip into the rims, middle rides 1.1 mm above the rim line
        dip = 0.9 * (abs(2 * t - 1) ** 6)
        path.append(Vector((x, -0.25, ztop - dip)))
    prof = L.rrect(1.9, 1.7, 0.6, seg=2)  # (a = up/down, b = depth)
    L.sweep(mb, path, prof, mat=0, caps=True, fixed_binormal=(0, 1, 0))


RIM_INNER_X = LCX - LENS_W / 2 - 0.7   # centre of the rim wire on the nose side (+X lens)


def build_bridge(mb):
    xin = RIM_INNER_X
    half = L.bezier_points((xin, 0, -4.6), (xin - 2.0, 0, 0.6), (4.2, 0, 0.0), (0, 0, 0), 9)
    other = [Vector((-p.x, p.y, p.z)) for p in half[:-1]]
    path = other + half[::-1]
    prof = L.rrect(1.8, 1.6, 0.55, seg=2)
    L.sweep(mb, path, prof, mat=0, caps=True, fixed_binormal=(0, 1, 0))


def build_pad(mb, side):
    """side=+1 for the +X (wearer's left) pad."""
    s = side
    xin = RIM_INNER_X
    p0 = Vector((s * xin, 0.9, -7.0))
    arm = L.bezier_points(p0, (s * (xin + 0.5), 5.0, -7.6), (s * (xin - 0.4), 7.3, -10.6), (s * (xin - 1.5), 7.1, -12.6), 10)
    L.sweep(mb, arm, L.circle2d(0.42, 6), mat=0, caps=True, up=(1, 0, 0))
    # pad mount (small box clip on the back of the pad)
    clip_c = Vector((s * (xin - 1.9), 7.3, -14.0))
    rot = L.rotz(-s * 18)
    L.box(mb, (1.2, 1.3, 3.2), center=clip_c, mat=0, rot=rot)
    # pad: flattened ellipsoid, face pointing inward/back/down, long axis tilted
    st = len(mb.verts)
    L.lathe(mb, [(0, -0.75), (2.0, -0.62), (3.0, -0.3), (3.3, 0.0), (3.0, 0.32), (2.0, 0.66), (0, 0.8)],
            segs=12, mat=1, axis="Y", sx=1.0, sy=1.75)
    M = (Matrix.Translation(Vector((s * (xin - 2.9), 7.9, -15.0)))
         @ Matrix.Rotation(math.radians(-s * 28), 4, "Z")
         @ Matrix.Rotation(math.radians(s * 12), 4, "Y")
         @ Matrix.Rotation(math.radians(90), 4, "Z"))
    L.transform_mb_range(mb, st, M)


def end_piece_outline():
    # (x, z) chamfered block, +X side
    return [(60.2, 1.2), (67.4, 1.2), (68.7, 2.4), (68.7, 8.6), (67.4, 9.9), (60.2, 9.9)]


def build_end_piece(side, notch=False):
    mb = L.MB()
    pts = end_piece_outline()
    if side < 0:
        pts = [(-x, z) for (x, z) in pts]
    L.prism(mb, pts, -1.35, 1.9, mat=0,
            basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))))
    # raised facet on the front face
    inner = [(61.8, 2.6), (66.6, 2.6), (67.2, 3.2), (67.2, 7.9), (66.6, 8.5), (61.8, 8.5)]
    if side < 0:
        inner = [(-x, z) for (x, z) in inner]
    L.prism(mb, inner, -1.7, -1.0, mat=0,
            basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))))
    # hinge blocks (frame side): top and bottom knuckle carriers behind the end piece
    hx = side * HINGE.x
    for (z0, z1) in ((7.0, 9.6), (1.6, 4.2)):
        L.box(mb, (2.6, 2.6, z1 - z0), center=(hx - side * 0.9, HINGE.y - 0.6, (z0 + z1) / 2), mat=0)
        L.lathe(mb, [(1.25, z0), (1.25, z1)], segs=10, mat=0, center=(hx, HINGE.y, 0))
    # screw head on top of the hinge
    L.lathe(mb, [(0, 10.05), (0.75, 10.0), (0.95, 9.75), (0.95, 9.55)], segs=10, mat=0, center=(hx, HINGE.y, 0))
    ob = mb.build("EndPiece%s" % ("L" if side > 0 else "R"), ["TS_Gunmetal"])
    L.merge_by_distance(ob, 1e-5)
    L.add_bevel(ob, 0.3, 1, 40.0)
    L.apply_modifiers(ob)
    if notch:
        # V-notch across the top face of the RIGHT end piece (wearer's right = -X)
        cm = L.MB()
        nx = side * 64.2
        L.box(cm, (1.5, 3.6, 1.5), center=(nx, -0.6, 9.9), mat=0, rot=L.roty(45))
        cut = cm.build("NotchCutter", ["TS_Gunmetal"])
        L.boolean(ob, cut, "DIFFERENCE")
    return ob


def temple_path():
    """Centre line of the +X temple in local hinge space (origin on the hinge axis)."""
    pts = []
    start = Vector((0.55, 1.6, 0.0))
    straight = 98.0
    splay, drop = 1.6, 1.8
    for i in range(7):
        t = i / 6
        pts.append(start + Vector((splay * t, straight * t, -drop * t)))
    end = pts[-1]
    d = (Vector((splay, straight, -drop))).normalized()
    R, turn = 30.0, math.radians(38)
    # bend down in the plane spanned by d and -Z
    up = Vector((0, 0, 1))
    side_n = d.cross(up).normalized()
    down = side_n.cross(d).normalized() * -1  # points downward, perpendicular to d
    for i in range(1, 11):
        a = turn * i / 10
        pts.append(end + d * (R * math.sin(a)) + down * (R * (1 - math.cos(a))))
    d2 = (d * math.cos(turn) + down * math.sin(turn)).normalized()
    tail_end = pts[-1]
    remaining = 140.0 - straight - R * turn - 1.6
    for i in range(1, 5):
        pts.append(tail_end + d2 * (remaining * i / 4))
    return pts


def arclen(pts):
    acc = [0.0]
    for i in range(1, len(pts)):
        acc.append(acc[-1] + (pts[i] - pts[i - 1]).length)
    return acc


def build_temple(name, side, parent, fold_deg):
    mb = L.MB()
    path = temple_path()
    s_acc = arclen(path)
    total = s_acc[-1]
    # metal bar up to just inside the acetate sleeve
    sleeve_start = 98.5
    bar = [p for p, s in zip(path, s_acc) if s <= sleeve_start + 6.0]
    bar_s = [s for s in s_acc if s <= sleeve_start + 6.0]

    def bar_scale(i):
        s = bar_s[i]
        if s < 9.0:
            k = 1.0 + 0.25 * (1 - s / 9.0)
            return (k, 1.0 + 0.2 * (1 - s / 9.0))
        t = (s - 9.0) / (sleeve_start - 9.0)
        return (1.0 - 0.22 * min(t, 1.0), 1.0 - 0.12 * min(t, 1.0))
    prof = L.rrect(3.1, 1.45, 0.42, seg=1)   # a = height (Z), b = thickness (X)
    L.sweep(mb, bar, prof, mat=0, caps=True, fixed_binormal=(1, 0, 0),
            scale=[bar_scale(i) for i in range(len(bar))])
    # hinge knuckle (temple side, middle) + connector block
    L.lathe(mb, [(1.25, -1.25), (1.25, 1.25)], segs=10, mat=0, center=(0, 0, 0))
    L.box(mb, (1.9, 3.4, 2.5), center=(0.35, 1.6, 0.0), mat=0)
    # acetate sleeve (temple tip)
    tip_pts = [p for p, s in zip(path, s_acc) if s >= sleeve_start - 0.01]
    tip_s = [s for s in s_acc if s >= sleeve_start - 0.01]
    # densify tip path
    tip_dense = L.polyline_resample3d(tip_pts, 20)
    tip_acc = arclen(tip_dense)
    tl = tip_acc[-1]

    def tip_scale(i):
        s = tip_acc[i]
        k = 1.0
        if s < 3.0:
            k = 0.82 + 0.18 * (s / 3.0)
        rem = tl - s
        if rem < 3.0:
            k *= math.sqrt(max(0.0, 1 - ((3.0 - rem) / 3.0) ** 2)) * 0.92 + 0.08
        return (k * (1.0 + 0.12 * (s / tl)), k)
    tprof = L.rrect(3.5, 2.3, 1.0, seg=2)
    L.sweep(mb, tip_dense, tprof, mat=1, caps=True, fixed_binormal=(1, 0, 0),
            scale=[tip_scale(i) for i in range(len(tip_dense))])
    ob = mb.build(name, ["TS_Gunmetal", "TS_TempleTip"])
    L.merge_by_distance(ob, 1e-5)
    L.cleanup(ob, 1e-5)
    if side < 0:
        ob.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
        ob.data.flip_normals()
    # fold about the hinge's vertical axis (rotation baked into the mesh)
    if fold_deg:
        ob.data.transform(Matrix.Rotation(math.radians(side * fold_deg), 4, "Z"))
    ob.location = Vector((side * HINGE.x, HINGE.y, HINGE.z))
    ob.parent = parent
    L.shade(ob, 50.0, True)
    L.add_triangulate(ob, 5)
    L.add_weighted_normal(ob)
    L.apply_modifiers(ob)
    L.uv_smart(ob)
    return ob


def build(folded):
    L.reset()
    name = "Glasses_Folded" if folded else "Glasses_Open"
    root = L.empty(name, size=10)
    # Frame
    mb = L.MB()
    build_rim(mb, False)
    build_rim(mb, True)
    build_top_bar(mb)
    build_bridge(mb)
    build_pad(mb, +1)
    build_pad(mb, -1)
    frame = mb.build("Frame", ["TS_Gunmetal", "TS_NosePad"])
    L.merge_by_distance(frame, 1e-5)
    L.cleanup(frame, 1e-5)
    L.shade(frame, 50.0, True)
    L.add_triangulate(frame, 5)
    L.add_weighted_normal(frame)
    L.apply_modifiers(frame)
    epL = build_end_piece(+1)
    epR = build_end_piece(-1, notch=True)
    for e in (epL, epR):
        L.cleanup(e, 1e-5)
        L.shade(e, 40.0, True)
        L.add_weighted_normal(e)
        L.add_triangulate(e, 5)
        L.apply_modifiers(e)
    frame = L.join([frame, epL, epR], "Frame")
    frame.parent = root
    L.uv_smart(frame)
    # Lenses
    lensL = build_lens("LensL", False, root)
    lensR = build_lens("LensR", True, root)
    # Temples (wearer's left folds first and lies flat; the right one rests on it)
    tL = build_temple("TempleL", +1, root, 90.0 if folded else 0.0)
    tR = build_temple("TempleR", -1, root, 84.0 if folded else 0.0)
    # mm -> m
    for o in L.descendants(root):
        if o.type == "MESH":
            o.data.transform(Matrix.Scale(L.MM, 4))
        if o is not root:
            o.location = o.location * L.MM
    for o in L.descendants(root):
        L.clean_mesh(o) if o.type == "MESH" else None
    bpy.context.view_layer.update()
    info = L.export(root, name + ".fbx", {
        "notes": "Origin: centre of arched nose bridge. Blender +X = wearer's left. Notch on wearer's right (-X) end piece."})
    L.preview(root, name + ".png", view=(-0.75, -1.0, 0.5), lens=60)
    if not folded:
        L.preview(root, name + "_front.png", view=(0.0, -1.0, 0.08), lens=80, floor=False)
        L.preview(root, name + "_side.png", view=(-1.0, 0.0, 0.05), lens=80, floor=False)
    else:
        L.preview(root, name + "_back.png", view=(0.0, 1.0, 0.25), lens=80, floor=False)
    L.save_blend(name + ".blend")
    return info


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = argv[0] if argv else "both"
    if which in ("open", "both"):
        build(False)
    if which in ("folded", "both"):
        build(True)
