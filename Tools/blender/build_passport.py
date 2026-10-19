"""
Passport_Open.fbx / Passport_Closed.fbx  (reference B) - fictional commemorative booklet.

Built in millimetres, converted to metres. Pages / front cover face -Y (toward the viewer), +Z up.
Page 105 x 148 mm, cover 108.5 x 152 mm.

Open:   root "Passport_Open", origin = gutter centre (where the two top pages meet, mid-height).
        Children: PageLeft, PageRight (0..1 UVs across each page, u left->right, v bottom->top as
        seen from the front; origins on the gutter line), StackLeft, StackRight (TS_Paper faces,
        TS_PaperEdge edges), Covers (TS_LeatherNavy, both covers + spine), Thread (saddle stitches).
Closed: root "Passport_Closed", origin = middle of the spine's outer edge.
        Children: Cover (front cover, 0..1 UVs on its front face, origin on the spine hinge line)
                  + raised compass-rose-and-lens emblem merged in (slot TS_Gold),
                  Back (back cover + spine, TS_LeatherNavy), Pages (block, TS_Paper/TS_PaperEdge).
"""
import sys
import os
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402

PW, PH = 105.0, 148.0          # page
CW, CH = 108.5, 152.0          # cover (from the spine)
CT = 1.3                       # cover board thickness
ST = 3.0                       # page stack thickness per side (open)
V = math.radians(6.0)          # half-angle of the open V
VC = 4.0                       # softening of the V kink at the spine
XC, YG = 15.0, 2.5             # gutter curl width / depth
CORNER = 4.0                   # cover corner radius


# ------------------------------------------------------------------ open booklet
def curl_y(x):
    """Page top surface depth (+Y = back) as a function of distance from the gutter."""
    y = 0.0
    if x < XC:
        y = YG * (1 - x / XC) ** 2.6
    if x > 78.0:  # outer edge lifts a little toward the viewer
        y -= 0.55 * ((x - 78.0) / 27.0) ** 2
    return y


def page_columns(n=26):
    xs = [PW * 1.02 * i / 2000 for i in range(2001)]
    pts = [(x, curl_y(x)) for x in xs]
    acc = [0.0]
    for i in range(1, len(pts)):
        acc.append(acc[-1] + math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]))
    cols = []
    for k in range(n + 1):
        s = PW * (k / n) ** 1.55
        j = next(i for i in range(len(acc)) if acc[i] >= s - 1e-9)
        if j == 0:
            cols.append((s, 0.0, curl_y(0.0)))
            continue
        f = (s - acc[j - 1]) / max(acc[j] - acc[j - 1], 1e-12)
        x = pts[j - 1][0] + (pts[j][0] - pts[j - 1][0]) * f
        cols.append((s, x, curl_y(x)))
    return cols


def shear(x):
    return -math.tan(V) * (math.sqrt(x * x + VC * VC) - VC)


def place(v):
    """local open-booklet coords (y' toward the back, gutter point at y'=YG) -> final."""
    return Vector((v[0], v[1] - YG + shear(v[0]), v[2]))


def wave(x, z):
    return -0.12 * (0.5 + 0.5 * math.sin(2 * math.pi * (z / PH) * 1.5 + x / 30.0)) * min(1.0, x / 20.0)


def build_page(name, side, cols, parent):
    mb = L.MB()
    rows = 8
    grid = []
    for (s, x, y) in cols:
        col = []
        for r in range(rows + 1):
            z = -PH / 2 + PH * r / rows
            col.append(mb.v(place((side * x, y + wave(x, z), z))))
        grid.append(col)
    for i in range(len(cols) - 1):
        u0 = cols[i][0] / PW
        u1 = cols[i + 1][0] / PW
        if side < 0:
            u0, u1 = 1 - u0, 1 - u1
        for r in range(rows):
            v0, v1 = r / rows, (r + 1) / rows
            a, b, c, d = grid[i][r], grid[i + 1][r], grid[i + 1][r + 1], grid[i][r + 1]
            if side > 0:
                mb.f((a, b, c, d), 0, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
            else:
                mb.f((b, a, d, c), 0, [(u1, v0), (u0, v0), (u0, v1), (u1, v1)])
    ob = mb.build(name, ["TS_Paper"], parent=parent, recalc=False)
    return ob


def build_stack(name, side, cols, parent):
    mb = L.MB()
    xs = [c[1] for c in cols]
    ys = [c[2] + 0.06 for c in cols]

    def yf(x):
        for i in range(len(xs) - 1):
            if xs[i] <= x <= xs[i + 1] + 1e-9:
                f = (x - xs[i]) / max(xs[i + 1] - xs[i], 1e-12)
                return ys[i] + (ys[i + 1] - ys[i]) * f
        return ys[-1]
    L.ruled_slab(mb, xs, lambda x: -PH / 2, lambda x: PH / 2, yf, lambda x: ST,
                 mats=(0, 0, 1, 1, 1, 1), rows=1)
    for i in range(len(mb.verts)):
        v = mb.verts[i]
        mb.verts[i] = place((side * v.x, v.y, v.z))
    ob = mb.build(name, ["TS_Paper", "TS_PaperEdge"], parent=parent)
    return ob


def build_covers(parent):
    mb = L.MB()
    xs = L.corner_xs(-CW, CW, CORNER, CORNER, n_corner=6, step=5.0)
    # extra columns near the spine for a smooth V kink
    xs = sorted(set([round(x, 6) for x in xs] + [-2.0, -1.0, 0.0, 1.0, 2.0, -3.0, 3.0]))

    def zt(x):
        return L.rounded_extent(x, -CW, CW, CH / 2, CORNER, CORNER)
    L.ruled_slab(mb, xs, lambda x: -zt(x), zt, lambda x: ST + 0.02, lambda x: ST + CT,
                 mats=(0, 0, 0, 0, 0, 0), rows=1)
    for i in range(len(mb.verts)):
        mb.verts[i] = place(mb.verts[i])
    return mb.build("Covers", ["TS_LeatherNavy"], parent=parent)


def twisted_cord(mb, path, radius=0.42, period=2.0, mat=0):
    total = sum((path[i] - path[i - 1]).length for i in range(1, len(path)))
    prof = []
    for k in range(8):
        th = 2 * math.pi * k / 8
        r = radius * (0.8 + 0.2 * math.cos(2 * th))
        prof.append((r * math.cos(th), r * math.sin(th)))
    turns = total / period
    L.sweep(mb, path, prof, mat=mat, caps=True, up=(1, 0, 0),
            twist=lambda t: 2 * math.pi * turns * t,
            scale=lambda t: 0.55 + 0.45 * math.sin(math.pi * min(1.0, max(0.0, t))) ** 0.35)


def build_thread(parent):
    mb = L.MB()
    centres = [-60.0, -40.0, -20.0, 0.0, 20.0, 40.0, 60.0]
    seg = 12.0
    y0 = -0.42  # rests in front of the gutter crease
    for c in centres:
        pts = []
        n = 18
        for i in range(n + 1):
            t = i / n
            z = c - seg / 2 + seg * t
            dive = 0.55 * (max(0.0, abs(2 * t - 1) - 0.8) / 0.2) ** 2  # ends dive into the paper
            pts.append(Vector((0.0, y0 + dive, z)))
        twisted_cord(mb, pts)
    return mb.build("Thread", ["TS_PaperEdge"], parent=parent)


def build_open():
    L.reset()
    root = L.empty("Passport_Open", size=30)
    cols = page_columns()
    pl = build_page("PageLeft", -1, cols, root)
    pr = build_page("PageRight", +1, cols, root)
    sl = build_stack("StackLeft", -1, cols, root)
    sr = build_stack("StackRight", +1, cols, root)
    cv = build_covers(root)
    th = build_thread(root)
    for ob, ang in ((pl, 30.0), (pr, 30.0), (sl, 30.0), (sr, 30.0), (cv, 35.0), (th, 60.0)):
        L.cleanup(ob, 1e-6)
        L.shade(ob, ang, True)
        L.add_triangulate(ob, 5)
        L.add_weighted_normal(ob)
        L.apply_modifiers(ob)
        if ob in (sl, sr, cv, th):
            L.uv_smart(ob)
    finish_export(root, "Passport_Open", [
        ("Passport_Open.png", dict(view=(-0.35, -1.0, 0.55), lens=55)),
        ("Passport_Open_front.png", dict(view=(0.0, -1.0, 0.0), lens=70, floor=False)),
        ("Passport_Open_gutter.png", dict(view=(0.25, -1.0, 0.35), lens=50, floor=False, margin=0.22,
                                          target=(0.0, -0.002, 0.03))),
    ])


# ------------------------------------------------------------------ closed booklet
T2 = 4.25          # half thickness of the closed booklet
SPX = T2           # spine axis x (outer spine edge at x = 0)
EMB_C = Vector((SPX + (CW - SPX) / 2, 0.0, 8.0))   # emblem centre (x, -, z) on the front face


def star_poly(tips, valley_r, n=16, a0=0.0):
    pts = []
    for k in range(n):
        a = a0 + 2 * math.pi * k / n
        r = tips[k // 2] if k % 2 == 0 else valley_r
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def raised_poly(mb, pts2d, h_center, h_edge, base, mat=0, center=None):
    """Faceted raised polygon (fan to a raised centre) + walls down to `base` height.
    2D coords (a=x, b=z); heights toward -Y are applied by the caller's transform."""
    c = center if center is not None else (0.0, 0.0)
    top = [mb.v((a, -h_edge, b)) for (a, b) in pts2d]
    bot = [mb.v((a, -base, b)) for (a, b) in pts2d]
    ci = mb.v((c[0], -h_center, c[1]))
    n = len(pts2d)
    for k in range(n):
        k1 = (k + 1) % n
        mb.f((ci, top[k], top[k1]), mat, None, False)
        mb.f((top[k], bot[k], bot[k1], top[k1]), mat, None, False)
    mb.f(tuple(bot[::-1]), mat, None, False)


def build_emblem(parent_cover):
    mb = L.MB()
    # 8-point compass rose: long N/E/S/W, short diagonals
    tips = [30.5, 14.0, 33.5, 14.0, 30.5, 14.0, 33.5, 14.0]   # E, NE, N, NW, W, SW, S, SE
    raised_poly(mb, star_poly(tips, 6.2), 1.15, 0.28, -0.1)
    # main ring
    ring = [Vector((19.0 * math.cos(a), 0.0, 19.0 * math.sin(a))) for a in
            [2 * math.pi * k / 56 for k in range(56)]]
    prof = [(-0.75, -0.1), (0.75, -0.1), (0.6, 0.42), (-0.6, 0.42)]
    L.sweep(mb, ring, prof, mat=0, closed_path=True, caps=False, fixed_binormal=(0, -1, 0))
    # lens: ring + domed glass-like boss in the centre
    lens = [Vector((6.6 * math.cos(a), -1.05, 6.6 * math.sin(a))) for a in [2 * math.pi * k / 32 for k in range(32)]]
    L.sweep(mb, lens, L.circle2d(0.75, 6), mat=0, closed_path=True, caps=False, fixed_binormal=(0, -1, 0))
    st = len(mb.verts)
    L.lathe(mb, [(0, 1.75), (2.2, 1.6), (3.8, 1.3), (5.1, 0.95), (5.6, 0.6)], segs=24, mat=0,
            axis="-Y", cap_start=True, cap_end=True)
    # crescent: a small raised sliver inside the lens
    cres = []
    for k in range(13):
        a = math.radians(-40 + 160 * k / 12)
        cres.append((3.7 * math.cos(a) + 0.4, 3.7 * math.sin(a) - 0.4))
    for k in range(12, -1, -1):
        a = math.radians(-40 + 160 * k / 12)
        cres.append((2.6 * math.cos(a) + 1.3, 2.6 * math.sin(a) - 1.0))
    st2 = len(mb.verts)
    top = [mb.v((a, -2.05, b)) for (a, b) in cres]
    bot = [mb.v((a, -1.2, b)) for (a, b) in cres]
    n = len(cres)
    for k in range(n):
        k1 = (k + 1) % n
        mb.f((top[k], bot[k], bot[k1], top[k1]), 0, None, False)
    mb.f(tuple(top), 0, None, False)
    mb.f(tuple(bot[::-1]), 0, None, False)
    # dotted ring
    for k in range(20):
        a = 2 * math.pi * (k + 0.5) / 20
        L.lathe(mb, [(0, 0.42), (0.55, 0.1), (0.55, -0.1)], segs=6, mat=0, axis="-Y",
                center=(23.5 * math.cos(a), 0.0, 23.5 * math.sin(a)), cap_start=True, cap_end=True)
    # sparkles beyond the long points and on the diagonals
    for (r, a, s) in [(38.5, 90, 1.0), (38.5, 270, 1.0), (35.5, 0, 0.9), (35.5, 180, 0.9),
                      (26.5, 45, 0.6), (26.5, 135, 0.6), (26.5, 225, 0.6), (26.5, 315, 0.6)]:
        cx, cz = r * math.cos(math.radians(a)), r * math.sin(math.radians(a))
        sp = star_poly([2.8 * s] * 4, 0.75 * s, n=8, a0=0.0)
        raised_poly(mb, [(cx + p[0], cz + p[1]) for p in sp], 0.6, 0.15, -0.1, center=(cx, cz))
    ob = mb.build("Emblem", ["TS_Gold"])
    # place on the cover's front face
    ob.data.transform(Matrix.Translation(Vector((EMB_C.x, -T2, EMB_C.z))))
    return ob


def remove_internal_walls(ob, x_wall):
    """Delete faces lying in the plane x = x_wall (internal wall where the spine joins)."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    kill = [f for f in bm.faces if all(abs(v.co.x - x_wall) < 1e-4 and v.co.y > 0.0 for v in f.verts)]
    bmesh.ops.delete(bm, geom=kill, context="FACES_ONLY")
    bm.to_mesh(ob.data)
    bm.free()


def build_closed():
    L.reset()
    root = L.empty("Passport_Closed", size=30)
    # front cover
    mb = L.MB()
    xs = L.corner_xs(SPX, CW, 0.0, CORNER, n_corner=6, step=5.0)

    def zt(x):
        return L.rounded_extent(x, SPX, CW, CH / 2, 0.0, CORNER)
    L.ruled_slab(mb, xs, lambda x: -zt(x), zt, lambda x: -T2, lambda x: -T2 + CT,
                 mats=(0, 0, 0, 0, 0, 0), rows=1, uv_box=(SPX, CW, -CH / 2, CH / 2))
    cover = mb.build("Cover", ["TS_LeatherNavy"], parent=root)
    # back cover + spine
    mb = L.MB()
    L.ruled_slab(mb, xs, lambda x: -zt(x), zt, lambda x: T2 - CT, lambda x: T2,
                 mats=(0, 0, 0, 0, 0, 0), rows=1)
    # spine half tube (theta 90..270 around the axis at x = SPX)
    ns = 12
    outer, inner = [], []
    for k in range(ns + 1):
        th = math.radians(90 + 180 * k / ns)
        for (r, lst) in ((T2, outer), (T2 - CT, inner)):
            lst.append((SPX + r * math.cos(th), r * math.sin(th)))
    ob_rings = []
    for (x, y) in outer:
        ob_rings.append((mb.v((x, y, -CH / 2)), mb.v((x, y, CH / 2))))
    ib_rings = []
    for (x, y) in inner:
        ib_rings.append((mb.v((x, y, -CH / 2)), mb.v((x, y, CH / 2))))
    for k in range(ns):
        o0, o1 = ob_rings[k], ob_rings[k + 1]
        i0, i1 = ib_rings[k], ib_rings[k + 1]
        mb.f((o0[0], o1[0], o1[1], o0[1]), 0)
        mb.f((i0[1], i1[1], i1[0], i0[0]), 0)
        mb.f((o0[1], o1[1], i1[1], i0[1]), 0)
        mb.f((i0[0], i1[0], o1[0], o0[0]), 0)
    # close the front end of the spine (it abuts the front cover)
    o, i = ob_rings[-1], ib_rings[-1]
    mb.f((o[0], o[1], i[1], i[0]), 0)
    back = mb.build("Back", ["TS_LeatherNavy"], parent=root, merge_dist=1e-4, recalc=False)
    remove_internal_walls(back, SPX)
    L.recalc_normals(back)
    # page block
    mb = L.MB()
    px0, px1 = 2.2, CW - 2.4
    L.ruled_slab(mb, [px0, px1], lambda x: -PH / 2, lambda x: PH / 2, lambda x: -T2 + CT + 0.05,
                 lambda x: T2 - CT - 0.05, mats=(0, 0, 1, 1, 1, 1), rows=1)
    pages = mb.build("Pages", ["TS_Paper", "TS_PaperEdge"], parent=root)
    emblem = build_emblem(cover)
    for ob, ang in ((cover, 35.0), (back, 35.0), (pages, 30.0), (emblem, 25.0)):
        L.cleanup(ob, 1e-6)
        L.shade(ob, ang, True)
        L.add_triangulate(ob, 5)
        L.add_weighted_normal(ob)
        L.apply_modifiers(ob)
    for ob in (back, pages):
        L.uv_smart(ob)
    # emblem UVs: planar in the cover's 0..1 space (front face projection)
    me = emblem.data
    uvl = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = ((co.x - SPX) / (CW - SPX), (co.z + CH / 2) / CH)
    # the raised emblem is part of the Cover object (slot TS_Gold) so it moves with the cover
    cover = L.join([cover, emblem], "Cover")
    cover.parent = root
    # cover origin on the spine hinge line
    L.set_origin(cover, Vector((SPX, -T2 + CT / 2, 0.0)))
    finish_export(root, "Passport_Closed", [
        ("Passport_Closed.png", dict(view=(-0.45, -1.0, 0.5), lens=55)),
        ("Passport_Closed_front.png", dict(view=(0.0, -1.0, 0.0), lens=70, floor=False)),
        ("Passport_Closed_emblem.png", dict(view=(0.35, -1.0, 0.5), lens=50, floor=False, margin=0.38,
                                            target=(EMB_C.x * L.MM, -T2 * L.MM, EMB_C.z * L.MM))),
    ])


def finish_export(root, name, previews):
    for o in L.descendants(root):
        if o.type == "MESH":
            o.data.transform(Matrix.Scale(L.MM, 4))
        if o is not root:
            o.location = o.location * L.MM
    bpy.context.view_layer.update()
    L.export(root, name + ".fbx")
    for (png, kw) in previews:
        L.preview(root, png, **kw)
    L.save_blend(name + ".blend")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = argv[0] if argv else "both"
    if which in ("open", "both"):
        build_open()
    if which in ("closed", "both"):
        build_closed()
