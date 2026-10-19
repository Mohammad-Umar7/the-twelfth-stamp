"""
Apartment set (contemporary Abu Dhabi apartment): Sofa, DiningTable, DiningChair, KitchenRun, Fridge,
BathroomSink, Door, Suitcase, Phone.

Blender: metres, front faces -Y, +Z up. Furniture origins at the floor centre of the footprint
unless noted (Door: hinge bottom; Phone: centre; Suitcase lid: hinge axis).
usage: blender -b --factory-startup --python build_apartment.py -- [Sofa Door ...]   (default: all)
"""
import sys
import os
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402


# ------------------------------------------------------------------ helpers
def bbox_obj(name, size, center, mat, bevel=0.0, seg=2, angle=40.0, rot=None, profile=0.5):
    mb = L.MB()
    L.box(mb, size, center=(0, 0, 0), mat=0)
    ob = mb.build(name, [mat])
    if bevel > 0:
        L.add_bevel(ob, bevel, seg, angle, profile=profile)
        L.apply_modifiers(ob)
    M = Matrix.Translation(Vector(center))
    if rot is not None:
        M = M @ rot.to_4x4()
    ob.data.transform(M)
    return ob


def mb_obj(mb, name, mats, bevel=0.0, seg=2, angle=40.0):
    ob = mb.build(name, mats)
    if bevel > 0:
        L.add_bevel(ob, bevel, seg, angle)
        L.apply_modifiers(ob)
    return ob


def finish_join(objs, name, root, smooth=40.0, uv="smart"):
    ob = L.join(objs, name)
    L.cleanup(ob, 1e-6)
    L.shade(ob, smooth, True)
    L.add_triangulate(ob, 5)
    L.add_weighted_normal(ob)
    L.apply_modifiers(ob)
    if uv == "smart":
        L.uv_smart(ob, angle=60.0)
    ob.parent = root
    return ob


def superellipsoid(mb, half, center, e1=0.35, e2=0.35, nu=24, nv=14, mat=0, bulge=0.0, rot=None, sag=0.0):
    a, b, c = half
    cen = Vector(center)
    rings = []
    st = len(mb.verts)
    for j in range(1, nv):
        eta = -math.pi / 2 + math.pi * j / nv
        ce, se = math.cos(eta), math.sin(eta)
        ring = []
        for i in range(nu):
            w = -math.pi + 2 * math.pi * i / nu
            cw, sw = math.cos(w), math.sin(w)
            fe = math.copysign(abs(ce) ** e1, ce)
            x = a * fe * math.copysign(abs(cw) ** e2, cw)
            y = b * fe * math.copysign(abs(sw) ** e2, sw)
            z = c * math.copysign(abs(se) ** e1, se)
            k = max(0.0, 1 - (x / a) ** 2) * max(0.0, 1 - (y / b) ** 2)
            if z > 0:
                z += bulge * k
            else:
                z -= bulge * 0.35 * k
            z -= sag * k * (1 if z > 0 else 0)
            ring.append(mb.v(Vector((x, y, z))))
        rings.append(ring)
    bot = mb.v(Vector((0, 0, -c - bulge * 0.35)))
    top = mb.v(Vector((0, 0, c + bulge - sag)))
    mb.bridge(rings, closed_ring=True, mat=mat)
    n = nu
    for i in range(n):
        mb.f((bot, rings[0][(i + 1) % n], rings[0][i]), mat)
        mb.f((top, rings[-1][i], rings[-1][(i + 1) % n]), mat)
    M = Matrix.Translation(cen)
    if rot is not None:
        M = M @ rot.to_4x4()
    L.transform_mb_range(mb, st, M)


def tapered_leg(mb, p_top, p_bot, r_top, r_bot, mat=0, segs=16):
    p_top, p_bot = Vector(p_top), Vector(p_bot)
    d = (p_top - p_bot)
    ln = d.length
    st = len(mb.verts)
    L.lathe(mb, [(0, 0), (r_bot * 0.92, 0), (r_bot, 0.003), (r_top, ln)], segs=segs, mat=mat)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    L.transform_mb_range(mb, st, Matrix.Translation(p_bot) @ q.to_matrix().to_4x4())
    for i in range(st, len(mb.verts)):
        if mb.verts[i].z < p_bot.z:
            mb.verts[i].z = p_bot.z


def bar_handle(mb, p0, p1, r, out, stand, mat=0, segs=12):
    """Round bar between p0 and p1, held off the surface by two standoffs along `out`."""
    p0, p1, out = Vector(p0), Vector(p1), Vector(out).normalized()
    a, b = p0 + out * stand, p1 + out * stand
    L.sweep(mb, [a + (b - a) * (k / 6) for k in range(7)], L.circle2d(r, segs), mat=mat, caps=True,
            up=tuple(out))
    for p in (p0 + (p1 - p0) * 0.08, p1 + (p0 - p1) * 0.08):
        L.sweep(mb, [p - out * 0.002, p + out * (stand + 0.0005)], L.circle2d(r * 0.8, 10), mat=mat, caps=True,
                up=(0, 0, 1) if abs(out.z) < 0.9 else (1, 0, 0))


def rrect_slab(mb, w, h, t, r, center, mat=0, plane="XZ", seg=6):
    """Rounded-rectangle slab; plane XZ = standing (thickness along Y)."""
    pts = L.rrect(w, h, r, seg=seg)
    if plane == "XZ":
        basis = (Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0)))
    elif plane == "XY":
        basis = (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    else:
        basis = (Vector((0, 1, 0)), Vector((0, 0, 1)), Vector((1, 0, 0)))
    L.prism(mb, pts, -t / 2, t / 2, mat=mat, basis=basis, origin=center)


def export_asset(root, name, views):
    bpy.context.view_layer.update()
    info = L.export(root, name + ".fbx")
    for (png, kw) in views:
        L.preview(root, png, **kw)
    L.save_blend(name + ".blend")
    return info


# ------------------------------------------------------------------ Sofa
def build_sofa():
    L.reset()
    root = L.empty("Sofa", size=0.3)
    W, D = 2.30, 0.90
    parts = []
    # plinth + arms + back frame (upholstered, soft bevels)
    parts.append(bbox_obj("plinth", (W - 0.02, D - 0.06, 0.20), (0, 0.01, 0.215), "TS_Fabric", bevel=0.035, seg=4))
    for sx in (-1, 1):
        parts.append(bbox_obj("arm", (0.17, D, 0.50), (sx * (W / 2 - 0.085), 0.0, 0.365), "TS_Fabric",
                              bevel=0.06, seg=5))
    parts.append(bbox_obj("back", (W - 0.32, 0.20, 0.54), (0, D / 2 - 0.11, 0.54), "TS_Fabric", bevel=0.07, seg=5))
    # cushions
    cm = L.MB()
    inner = W - 0.34
    cw = inner / 3
    for k in range(3):
        x = -inner / 2 + cw * (k + 0.5)
        superellipsoid(cm, (cw / 2 - 0.005, 0.315, 0.075), (x, -0.075, 0.39), 0.32, 0.28, 28, 14, 0,
                       bulge=0.012)
        superellipsoid(cm, (cw / 2 - 0.006, 0.095, 0.215), (x, 0.235, 0.67), 0.36, 0.3, 26, 16, 0,
                       bulge=0.0, rot=L.rotx(-12) @ L.rotx(0))
    # throw pillows
    for sx, ang in ((-1, 18), (1, -18)):
        superellipsoid(cm, (0.22, 0.075, 0.22), (sx * 0.78, 0.13, 0.64), 0.55, 0.45, 20, 12, 1,
                       rot=L.rotz(ang) @ L.rotx(-14))
    cush = cm.build("cushions", ["TS_Fabric", "TS_FabricDark"])
    parts.append(cush)
    # piping along seat cushion edges
    pm = L.MB()
    for k in range(3):
        x = -inner / 2 + cw * (k + 0.5)
        for zz in (0.39 + 0.071, 0.39 - 0.071):
            loop = L.rrect(cw - 0.03, 0.62, 0.035, seg=4)
            path = [Vector((x + px, -0.075 + py, zz)) for (px, py) in L.resample(loop, 64, closed=True)]
            L.sweep(pm, path, L.circle2d(0.0045, 6), mat=0, closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
    parts.append(pm.build("piping", ["TS_FabricDark"]))
    # legs (dark wood, slight splay)
    lm = L.MB()
    for x in (-1.06, 0.0, 1.06):
        for y in (-0.36, 0.37):
            sx = 0 if x == 0 else math.copysign(0.012, x)
            sy = math.copysign(0.012, y)
            tapered_leg(lm, (x, y, 0.118), (x + sx, y + sy, 0.0), 0.022, 0.013, 0, 16)
    parts.append(lm.build("legs", ["TS_WoodDark"]))
    finish_join(parts, "SofaMesh", root, smooth=45.0)
    return export_asset(root, "Sofa", [("Sofa.png", dict(view=(-0.6, -1.0, 0.5), lens=50))])


# ------------------------------------------------------------------ Dining table + chair
def build_table():
    L.reset()
    root = L.empty("DiningTable", size=0.3)
    W, D, H = 1.60, 0.90, 0.75
    parts = [bbox_obj("top", (W, D, 0.036), (0, 0, H - 0.018), "TS_Wood", bevel=0.009, seg=3)]
    for (sx, sy, lx, ly) in ((0, 1, W - 0.17, 0.024), (0, -1, W - 0.17, 0.024), (1, 0, 0.024, D - 0.17), (-1, 0, 0.024, D - 0.17)):
        cx = sx * (W / 2 - 0.085)
        cy = sy * (D / 2 - 0.085)
        parts.append(bbox_obj("apron", (lx, ly, 0.085), (cx, cy, H - 0.036 - 0.0425), "TS_Wood", bevel=0.003))
    lm = L.MB()
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (W / 2 - 0.085), sy * (D / 2 - 0.085)
            # square tapered leg
            pts = L.rrect(0.056, 0.056, 0.006, seg=2)
            st = len(lm.verts)
            rings = []
            for (z, s) in ((0.0, 0.66), (0.012, 0.7), (H - 0.036, 1.0)):
                rings.append([lm.v((x + px * s, y + py * s, z)) for (px, py) in pts])
            lm.bridge(rings, closed_ring=True, mat=0)
            lm.cap(rings[0], 0, flip=True)
            lm.cap(rings[-1], 0)
    parts.append(lm.build("legs", ["TS_Wood"]))
    finish_join(parts, "DiningTableMesh", root, smooth=40.0)
    return export_asset(root, "DiningTable", [("DiningTable.png", dict(view=(-0.7, -1.0, 0.65), lens=50))])


def build_chair():
    L.reset()
    root = L.empty("DiningChair", size=0.2)
    parts = []
    SH = 0.46
    # seat frame + upholstered pad
    parts.append(bbox_obj("seatframe", (0.44, 0.44, 0.03), (0, -0.01, SH - 0.035), "TS_Wood", bevel=0.006, seg=2))
    cm = L.MB()
    superellipsoid(cm, (0.215, 0.215, 0.028), (0, -0.012, SH - 0.002), 0.3, 0.3, 24, 12, 0, bulge=0.008)
    parts.append(cm.build("pad", ["TS_Fabric"]))
    lm = L.MB()
    # front legs
    for sx in (-1, 1):
        tapered_leg(lm, (sx * 0.19, -0.19, SH - 0.04), (sx * 0.2, -0.205, 0.0), 0.017, 0.012, 0, 14)
    # back legs rising into the uprights (reclined)
    for sx in (-1, 1):
        path = [Vector((sx * 0.19, 0.225, 0.0)), Vector((sx * 0.19, 0.205, 0.2)), Vector((sx * 0.19, 0.19, SH - 0.04)),
                Vector((sx * 0.188, 0.205, 0.62)), Vector((sx * 0.185, 0.235, 0.86))]
        path = L.polyline_resample3d([tuple(p) for p in bezier_chain(path)], 16)
        L.sweep(lm, path, L.rrect(0.026, 0.034, 0.008, seg=2), mat=0, caps=True, up=(1, 0, 0),
                scale=lambda t: (1.0 - 0.25 * abs(t - 0.4), 1.0 - 0.2 * abs(t - 0.4)))
    # stretchers
    for sx in (-1, 1):
        L.sweep(lm, [Vector((sx * 0.195, -0.19, 0.16)), Vector((sx * 0.19, 0.2, 0.16))], L.circle2d(0.008, 10), mat=0,
                caps=True)
    L.sweep(lm, [Vector((-0.19, -0.19, 0.14)), Vector((0.19, -0.19, 0.14))], L.circle2d(0.008, 10), mat=0, caps=True)
    # curved backrest
    st = len(lm.verts)
    rest_path = [Vector((x, 0.035 * (1 - (x / 0.205) ** 2), 0.0)) for x in [-0.205 + 0.41 * k / 16 for k in range(17)]]
    L.sweep(lm, rest_path, L.rrect(0.17, 0.018, 0.006, seg=2), mat=0, caps=True, up=(0, 0, 1))
    L.transform_mb_range(lm, st, Matrix.Translation(Vector((0, 0.214, 0.755))) @ Matrix.Rotation(math.radians(-10), 4, "X"))
    for i in range(len(lm.verts)):
        if lm.verts[i].z < 0.0:
            lm.verts[i].z = 0.0
    parts.append(lm.build("frame", ["TS_Wood"]))
    finish_join(parts, "DiningChairMesh", root, smooth=45.0)
    return export_asset(root, "DiningChair", [("DiningChair.png", dict(view=(-0.75, -1.0, 0.55), lens=50))])


def bezier_chain(ctrl):
    """Smooth curve through control points (Catmull-Rom)."""
    out = []
    n = len(ctrl)
    for i in range(n - 1):
        p0 = ctrl[i - 1] if i > 0 else ctrl[i]
        p1, p2 = ctrl[i], ctrl[i + 1]
        p3 = ctrl[i + 2] if i + 2 < n else ctrl[i + 1]
        for k in range(8):
            t = k / 8
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(ctrl[-1])
    return out


# ------------------------------------------------------------------ Kitchen run
def build_kitchen():
    L.reset()
    root = L.empty("KitchenRun", size=0.4)
    RUN, DEP = 4.0, 0.60
    TOP_Z0, TOP_T = 0.87, 0.035
    FRONT_Y = -DEP / 2 + 0.02     # face of the carcass
    parts = []
    # carcass + plinth
    parts.append(bbox_obj("carcass", (RUN, DEP - 0.02, TOP_Z0 - 0.1), (0, 0.01, 0.1 + (TOP_Z0 - 0.1) / 2), "TS_Wood"))
    parts.append(bbox_obj("plinth", (RUN - 0.004, DEP - 0.08, 0.1), (0, 0.04, 0.05), "TS_MetalDark", bevel=0.002))
    # units: (width, kind)
    units = [(0.6, "drawers3"), (0.8, "sink"), (0.6, "door"), (0.6, "oven"), (0.6, "door"), (0.4, "drawers4"), (0.4, "door")]
    x = -RUN / 2
    fronts = L.MB()
    handles = L.MB()
    gap = 0.003
    FT = 0.019
    yfr = FRONT_Y - FT / 2
    z0, z1 = 0.1 + gap, TOP_Z0 - gap
    sink_x = hob_x = None
    for (w, kind) in units:
        xa, xb = x + gap, x + w - gap
        cx = (xa + xb) / 2
        if kind == "door":
            L.box(fronts, (xb - xa, FT, z1 - z0), center=(cx, yfr, (z0 + z1) / 2), mat=0)
            bar_handle(handles, (cx - (xb - xa) / 2 + 0.07, FRONT_Y - FT, z1 - 0.06), (cx + (xb - xa) / 2 - 0.07, FRONT_Y - FT, z1 - 0.06),
                       0.0065, (0, -1, 0), 0.03)
        elif kind == "sink":
            for (a, b) in ((xa, cx - gap / 2), (cx + gap / 2, xb)):
                L.box(fronts, (b - a, FT, z1 - z0), center=((a + b) / 2, yfr, (z0 + z1) / 2), mat=0)
                bar_handle(handles, ((a + b) / 2 - (b - a) / 2 + 0.06, FRONT_Y - FT, z1 - 0.06),
                           ((a + b) / 2 + (b - a) / 2 - 0.06, FRONT_Y - FT, z1 - 0.06), 0.0065, (0, -1, 0), 0.03)
            sink_x = cx
        elif kind.startswith("drawers"):
            n = int(kind[-1])
            hs = [0.2, 0.25, 0.32] if n == 3 else [0.17, 0.17, 0.2, 0.21]
            tot = sum(hs)
            zz = z0
            for h in hs:
                hh = (z1 - z0) * h / tot
                L.box(fronts, (xb - xa, FT, hh - gap), center=(cx, yfr, zz + hh / 2), mat=0)
                bar_handle(handles, (cx - (xb - xa) / 2 + 0.07, FRONT_Y - FT, zz + hh - 0.05),
                           (cx + (xb - xa) / 2 - 0.07, FRONT_Y - FT, zz + hh - 0.05), 0.0065, (0, -1, 0), 0.03)
                zz += hh
        elif kind == "oven":
            hob_x = cx
            # drawer below, oven door above it, control strip at the top
            dh = 0.15
            L.box(fronts, (xb - xa, FT, dh - gap), center=(cx, yfr, z0 + dh / 2), mat=0)
            bar_handle(handles, (cx - 0.2, FRONT_Y - FT, z0 + dh - 0.045), (cx + 0.2, FRONT_Y - FT, z0 + dh - 0.045),
                       0.0065, (0, -1, 0), 0.03)
            oz0, oz1 = z0 + dh, z1 - 0.1
            parts.append(bbox_obj("ovendoor", (xb - xa, 0.03, oz1 - oz0 - gap), (cx, FRONT_Y - 0.015, (oz0 + oz1) / 2),
                                  "TS_MetalDark", bevel=0.004, seg=2))
            parts.append(bbox_obj("ovenglass", (xb - xa - 0.1, 0.004, oz1 - oz0 - 0.16), (cx, FRONT_Y - 0.031, (oz0 + oz1) / 2 - 0.02),
                                  "TS_Glass", bevel=0.002, seg=1))
            bar_handle(handles, (cx - 0.24, FRONT_Y - 0.03, oz1 - 0.05), (cx + 0.24, FRONT_Y - 0.03, oz1 - 0.05), 0.008,
                       (0, -1, 0), 0.04)
            parts.append(bbox_obj("ovenpanel", (xb - xa, 0.03, 0.1 - gap), (cx, FRONT_Y - 0.015, z1 - 0.05), "TS_MetalDark",
                                  bevel=0.003, seg=2))
            km = L.MB()
            for k in range(4):
                kx = cx - 0.2 + k * 0.1 + (0.05 if k > 1 else 0)
                L.lathe(km, [(0, 0.0), (0.017, 0.0), (0.018, 0.012), (0.015, 0.022), (0.0, 0.023)], segs=20, mat=0,
                        axis="-Y", center=(kx, FRONT_Y - 0.03, z1 - 0.05))
            parts.append(km.build("knobs", ["TS_Metal"]))
        x += w
    front_ob = fronts.build("fronts", ["TS_Wood"])
    L.add_bevel(front_ob, 0.0018, 2, 40.0)
    L.apply_modifiers(front_ob)
    parts.append(front_ob)
    parts.append(handles.build("handles", ["TS_Metal"]))
    # countertop with the sink cut-out
    top = bbox_obj("top", (RUN + 0.02, DEP + 0.02, TOP_T), (0, -0.0, TOP_Z0 + TOP_T / 2), "TS_Stone", bevel=0.004, seg=2)
    cut = L.MB()
    rrect_slab(cut, 0.56, 0.40, 0.2, 0.05, (sink_x, -0.02, TOP_Z0 + 0.02), mat=0, plane="XY", seg=6)
    L.boolean(top, cut.build("sinkcut", ["TS_Stone"]), "DIFFERENCE")
    parts.append(top)
    # sink basin (thin stainless shell)
    sm = L.MB()
    outer = L.resample(L.rrect(0.56, 0.40, 0.05, seg=6), 48)
    inner_ = L.resample(L.rrect(0.54, 0.38, 0.045, seg=6), 48)
    zt = TOP_Z0 + 0.002
    lvl = []
    for (pts, z) in ((outer, zt), (inner_, zt), (L.offset2d(inner_, -0.01), zt - 0.19), (L.offset2d(inner_, -0.012), zt - 0.2)):
        lvl.append([sm.v((sink_x + px, -0.02 + py, z)) for (px, py) in pts])
    outer_low = [sm.v((sink_x + px, -0.02 + py, zt - 0.205)) for (px, py) in L.offset2d(outer, -0.006)]
    sm.bridge([lvl[0], lvl[1], lvl[2], lvl[3]], closed_ring=True, mat=0)
    sm.bridge([outer_low, lvl[0]], closed_ring=True, mat=0)
    c_in = sm.v((sink_x, -0.02, zt - 0.2))
    c_out = sm.v((sink_x, -0.02, zt - 0.205))
    n = len(outer)
    for k in range(n):
        sm.f((c_in, lvl[3][k], lvl[3][(k + 1) % n]), 0)
        sm.f((c_out, outer_low[(k + 1) % n], outer_low[k]), 0)
    L.lathe(sm, [(0, zt - 0.197), (0.035, zt - 0.197), (0.035, zt - 0.199), (0.0, zt - 0.199)], segs=20, mat=1,
            center=(sink_x, -0.02, 0))
    parts.append(sm.build("sink", ["TS_Metal", "TS_MetalDark"]))
    # gooseneck mixer tap (behind the basin)
    tm = L.MB()
    ty = 0.215
    L.lathe(tm, [(0, 0.0), (0.03, 0.0), (0.028, 0.012), (0.019, 0.02), (0.017, 0.12), (0.0, 0.122)], segs=24, mat=0,
            center=(sink_x, ty, TOP_Z0 + TOP_T))
    neck = [Vector((sink_x, ty, TOP_Z0 + TOP_T + 0.1 + 0.18 * t)) for t in (0.0, 0.33, 0.66, 1.0)]
    for k in range(1, 13):
        a = math.pi * k / 12
        neck.append(Vector((sink_x, ty - 0.11 * (1 - math.cos(a)), TOP_Z0 + TOP_T + 0.28 + 0.11 * math.sin(a))))
    neck.append(neck[-1] + Vector((0, 0, -0.06)))
    L.sweep(tm, neck, L.circle2d(0.0125, 16), mat=0, caps=True, up=(1, 0, 0))
    L.sweep(tm, [Vector((sink_x + 0.03, ty, TOP_Z0 + TOP_T + 0.085)), Vector((sink_x + 0.075, ty - 0.01, TOP_Z0 + TOP_T + 0.1))],
            L.circle2d(0.006, 10), mat=0, caps=True)
    parts.append(tm.build("tap", ["TS_Chrome"]))
    # gas hob set into the top
    hm = L.MB()
    hz = TOP_Z0 + TOP_T
    rrect_slab(hm, 0.58, 0.50, 0.006, 0.01, (hob_x, 0.0, hz + 0.003), mat=0, plane="XY", seg=4)
    for (bx, by, r) in ((-0.15, -0.11, 0.045), (0.15, -0.11, 0.038), (-0.15, 0.12, 0.038), (0.15, 0.12, 0.05)):
        L.lathe(hm, [(0, 0.0), (r, 0.0), (r, 0.012), (r * 0.82, 0.018), (r * 0.82, 0.022), (0.0, 0.022)], segs=24, mat=1,
                center=(hob_x + bx, by, hz + 0.006))
        L.lathe(hm, [(0, 0.0), (r * 0.62, 0.0), (r * 0.62, 0.008), (0.0, 0.011)], segs=20, mat=0,
                center=(hob_x + bx, by, hz + 0.028))
    # cast-iron grates
    for gy in (-0.11, 0.12):
        for gx in (-0.15, 0.15):
            for ang in (0, 90):
                st = len(hm.verts)
                L.box(hm, (0.24, 0.012, 0.012), center=(0, 0, 0.0), mat=0)
                L.transform_mb_range(hm, st, Matrix.Translation(Vector((hob_x + gx, gy, hz + 0.034))) @ Matrix.Rotation(math.radians(ang), 4, "Z"))
            for (dx, dy) in ((-0.11, 0), (0.11, 0), (0, -0.11), (0, 0.11)):
                L.box(hm, (0.012, 0.012, 0.03), center=(hob_x + gx + dx, gy + dy, hz + 0.022), mat=0)
    parts.append(hm.build("hob", ["TS_MetalDark", "TS_Metal"]))
    finish_join(parts, "KitchenRunMesh", root, smooth=35.0)
    return export_asset(root, "KitchenRun", [("KitchenRun.png", dict(view=(-0.55, -1.0, 0.6), lens=45)),
                                             ("KitchenRun_detail.png", dict(view=(-0.35, -1.0, 0.75), lens=50, margin=0.32,
                                                                           target=(-0.6, 0.0, 0.85), floor=False))])


# ------------------------------------------------------------------ Fridge
def build_fridge():
    L.reset()
    root = L.empty("Fridge", size=0.3)
    W, D, H = 0.85, 0.72, 1.90
    parts = []
    parts.append(bbox_obj("cabinet", (W, D - 0.07, H - 0.015), (0, 0.035, 0.015 + (H - 0.015) / 2), "TS_PlasticWhite",
                          bevel=0.018, seg=3))
    parts.append(bbox_obj("kick", (W - 0.06, 0.04, 0.065), (0, -D / 2 + 0.1, 0.015 + 0.0325), "TS_MetalDark", bevel=0.003))
    fm = L.MB()
    for sx in (-1, 1):
        for sy in (-1, 1):
            L.lathe(fm, [(0, 0.0), (0.016, 0.0), (0.018, 0.004), (0.012, 0.008), (0.012, 0.02), (0.0, 0.02)], segs=16,
                    mat=0, center=(sx * (W / 2 - 0.05), 0.035 + sy * ((D - 0.07) / 2 - 0.05), 0.0))
    parts.append(fm.build("feet", ["TS_MetalDark"]))
    gap = 0.004
    DT = 0.062
    yd = -D / 2 + 0.07 - DT / 2
    # French doors (top) + freezer drawer (bottom)
    dz0, dz1 = 0.08, 0.68
    uz0, uz1 = 0.68 + gap, H - 0.008
    doors = L.MB()
    for sx in (-1, 1):
        L.box(doors, (W / 2 - gap * 1.5, DT, uz1 - uz0), center=(sx * (W / 4 + gap / 4), yd, (uz0 + uz1) / 2), mat=0)
    L.box(doors, (W - gap * 2, DT, dz1 - dz0 - gap), center=(0, yd, (dz0 + dz1) / 2), mat=0)
    dob = doors.build("doors", ["TS_PlasticWhite"])
    L.add_bevel(dob, 0.012, 3, 40.0)
    L.apply_modifiers(dob)
    parts.append(dob)
    hm = L.MB()
    yf = yd - DT / 2
    for sx in (-1, 1):
        bar_handle(hm, (sx * 0.045, yf, uz0 + 0.18), (sx * 0.045, yf, uz0 + 0.88), 0.011, (0, -1, 0), 0.05)
    bar_handle(hm, (-0.3, yf, dz1 - 0.07), (0.3, yf, dz1 - 0.07), 0.011, (0, -1, 0), 0.05)
    parts.append(hm.build("handles", ["TS_Metal"]))
    # small control display on the left door
    parts.append(bbox_obj("display", (0.11, 0.004, 0.05), (-0.25, yf - 0.002, 1.48), "TS_Plastic", bevel=0.0015, seg=1))
    finish_join(parts, "FridgeMesh", root, smooth=40.0)
    return export_asset(root, "Fridge", [("Fridge.png", dict(view=(-0.6, -1.0, 0.45), lens=50))])


# ------------------------------------------------------------------ Bathroom sink
def build_bathroom():
    L.reset()
    root = L.empty("BathroomSink", size=0.3)
    WALL = 0.24
    parts = []
    # wall-hung vanity with two drawers
    VW, VD, vz0, vz1 = 0.80, 0.46, 0.40, 0.84
    parts.append(bbox_obj("vanity", (VW, VD - 0.02, vz1 - vz0), (0, WALL - VD / 2 + 0.01, (vz0 + vz1) / 2), "TS_Wood",
                          bevel=0.003))
    fm = L.MB()
    hm = L.MB()
    gap = 0.003
    fy = WALL - VD - 0.009
    hs = [(vz0 + gap, vz0 + 0.2), (vz0 + 0.2 + gap, vz1 - gap)]
    for (a, b) in hs:
        L.box(fm, (VW - 2 * gap, 0.018, b - a), center=(0, fy, (a + b) / 2), mat=0)
        # recessed-style top edge pull
        bar_handle(hm, (-0.18, fy - 0.009, b - 0.03), (0.18, fy - 0.009, b - 0.03), 0.005, (0, -1, 0), 0.022)
    fob = fm.build("fronts", ["TS_Wood"])
    L.add_bevel(fob, 0.0018, 2, 40.0)
    L.apply_modifiers(fob)
    parts += [fob, hm.build("pulls", ["TS_Metal"])]
    # marble top
    parts.append(bbox_obj("top", (VW + 0.02, VD + 0.01, 0.025), (0, WALL - (VD + 0.01) / 2, vz1 + 0.0125), "TS_Marble",
                          bevel=0.004, seg=2))
    tz = vz1 + 0.025
    # vessel basin (oval bowl with a thin wall)
    bm = L.MB()
    prof_out = [(0.0, 0.0), (0.12, 0.0), (0.2, 0.025), (0.245, 0.075), (0.255, 0.13), (0.252, 0.14)]
    prof_in = [(0.24, 0.14), (0.238, 0.13), (0.228, 0.08), (0.18, 0.035), (0.06, 0.018), (0.0, 0.017)]
    L.lathe(bm, prof_out + prof_in, segs=48, mat=0, center=(0, WALL - VD / 2 - 0.01, tz), sx=1.0, sy=0.72)
    L.lathe(bm, [(0, 0.0185), (0.022, 0.0185), (0.022, 0.0175), (0.0, 0.0172)], segs=20, mat=1,
            center=(0, WALL - VD / 2 - 0.01, tz))
    parts.append(bm.build("basin", ["TS_Ceramic", "TS_Chrome"]))
    # tall vessel mixer
    tm = L.MB()
    ty = WALL - 0.06
    L.lathe(tm, [(0, 0), (0.026, 0), (0.024, 0.01), (0.016, 0.018), (0.016, 0.27), (0.0, 0.272)], segs=24, mat=0,
            center=(0, ty, tz))
    spout = [Vector((0, ty - 0.005, tz + 0.255)), Vector((0, ty - 0.09, tz + 0.255)), Vector((0, ty - 0.13, tz + 0.248))]
    L.sweep(tm, spout, L.rrect(0.022, 0.016, 0.006, seg=2), mat=0, caps=True, up=(0, 0, 1))
    L.sweep(tm, [Vector((0.012, ty, tz + 0.2)), Vector((0.07, ty - 0.004, tz + 0.215))], L.circle2d(0.0055, 10), mat=0,
            caps=True)
    parts.append(tm.build("tap", ["TS_Chrome"]))
    # mirror frame on the wall
    mm = L.MB()
    MW, MH, mz = 0.70, 0.90, 1.62
    outer = L.resample(L.rrect(MW, MH, 0.08, seg=8), 80)
    inner_ = L.offset2d(outer, -0.022)
    yb, yfm = WALL - 0.005, WALL - 0.035
    rings = []
    for (pts, y) in ((outer, yb), (outer, yfm), (inner_, yfm), (inner_, yb)):
        rings.append([mm.v((px, y, mz + pz)) for (px, pz) in pts])
    mm.bridge(rings + [rings[0]], closed_ring=True, mat=0)
    parts.append(mm.build("mirrorframe", ["TS_MetalDark"]))
    vanity = finish_join(parts, "Vanity", root, smooth=40.0)
    # mirror glass (separate object so the game can give it a reflective material)
    gm = L.MB()
    gpts = L.offset2d(outer, -0.012)
    rrect_glass = gm.vs([(px, WALL - 0.02, mz + pz) for (px, pz) in gpts])
    back = gm.vs([(px, WALL - 0.012, mz + pz) for (px, pz) in gpts])
    gm.f(tuple(rrect_glass), 0)
    gm.f(tuple(back[::-1]), 0)
    nn = len(gpts)
    for k in range(nn):
        k1 = (k + 1) % nn
        gm.f((rrect_glass[k], back[k], back[k1], rrect_glass[k1]), 0)
    glass = gm.build("MirrorGlass", ["TS_Glass"])
    L.cleanup(glass)
    L.shade(glass, 30.0, True)
    L.add_triangulate(glass, 5)
    L.apply_modifiers(glass)
    # planar UVs on the mirror (front view 0..1)
    me = glass.data
    uvl = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = ((co.x + MW / 2) / MW, (co.z - (mz - MH / 2)) / MH)
    glass.parent = root
    for ob in (vanity, glass):
        ob.data.transform(Matrix.Translation(Vector((0, -WALL, 0))))
    return export_asset(root, "BathroomSink", [("BathroomSink.png", dict(view=(-0.6, -1.0, 0.35), lens=50))])


# ------------------------------------------------------------------ Door
def build_door():
    L.reset()
    root = L.empty("Door", size=0.2)
    W, H, T = 0.90, 2.10, 0.04
    X0 = 0.004
    parts = [bbox_obj("leaf", (W - X0, T, H - 0.008), (X0 + (W - X0) / 2, 0.0, 0.006 + (H - 0.008) / 2), "TS_Wood",
                      bevel=0.0025, seg=2)]
    # thin dark inlay lines (contemporary flush door)
    im = L.MB()
    for z in (0.32, 1.78):
        for sy in (-1, 1):
            L.box(im, (W - X0 - 0.12, 0.0012, 0.006), center=(X0 + (W - X0) / 2, sy * (T / 2 + 0.0004), z), mat=0)
    parts.append(im.build("inlays", ["TS_MetalDark"]))
    # lever handles both sides + escutcheons
    hm = L.MB()
    hx, hz = W - 0.065, 1.0
    for sy in (-1, 1):
        ys = sy * T / 2
        L.lathe(hm, [(0, 0), (0.026, 0), (0.026, 0.006), (0.022, 0.009), (0.0, 0.0095)], segs=28, mat=0,
                axis="-Y" if sy < 0 else "Y", center=(hx, ys, hz))
        L.lathe(hm, [(0, 0.0), (0.009, 0.0), (0.009, 0.048), (0.0, 0.05)], segs=16, mat=0,
                axis="-Y" if sy < 0 else "Y", center=(hx, ys, hz))
        lever = [Vector((hx, ys + sy * 0.045, hz)), Vector((hx - 0.03, ys + sy * 0.055, hz)),
                 Vector((hx - 0.13, ys + sy * 0.06, hz - 0.004))]
        L.sweep(hm, L.polyline_resample3d([tuple(p) for p in bezier_chain(lever)], 12), L.rrect(0.019, 0.011, 0.0045, seg=2),
                mat=0, caps=True, up=(0, 0, 1))
        L.lathe(hm, [(0, 0), (0.017, 0), (0.017, 0.005), (0.0, 0.006)], segs=20, mat=0,
                axis="-Y" if sy < 0 else "Y", center=(hx, ys, hz - 0.08))
        L.box(hm, (0.005, 0.004, 0.014), center=(hx, ys + sy * 0.006, hz - 0.08), mat=1)
    parts.append(hm.build("handles", ["TS_Metal", "TS_MetalDark"]))
    # hinges on the hinge edge (axis = origin, Z)
    km = L.MB()
    for z in (0.22, 1.05, 1.88):
        L.lathe(km, [(0, -0.05), (0.0075, -0.05), (0.008, -0.045), (0.008, 0.045), (0.0075, 0.05), (0, 0.052)], segs=16,
                mat=0, center=(0, 0, z))
        L.box(km, (0.024, 0.003, 0.09), center=(0.012, -T / 2 + 0.0, z), mat=0)
    parts.append(km.build("hinges", ["TS_Metal"]))
    finish_join(parts, "DoorLeaf", root, smooth=40.0)
    return export_asset(root, "Door", [("Door.png", dict(view=(-0.55, -1.0, 0.25), lens=50))])


# ------------------------------------------------------------------ Suitcase
def shell_half(mb, L_, W_, Hh, r_plan, r_edge, mat_shell, mat_trim, mat_line, ribs=True):
    """Open hard-shell half: shell from the rim (z=0) to the face (z=Hh); interior lined."""
    N = 72
    outline = L.resample(L.rrect(L_, W_, r_plan, seg=10), N)
    T = 0.004
    prof = []
    for k in range(7):
        a = math.radians(90 * k / 6)
        prof.append((r_edge * (1 - math.cos(a)), Hh - r_edge + r_edge * math.sin(a)))
    prof = [(0.0, 0.0), (0.0, Hh - r_edge)] + prof[1:]
    rings = []
    for (d, z) in prof:
        pts = L.offset2d(outline, -d) if d > 0 else outline
        rings.append([mb.v((x, y, z)) for (x, y) in pts])
    mb.bridge(rings, closed_ring=True, mat=mat_shell)
    mb.cap(rings[-1], mat_shell)
    # rim trim + interior lining
    inner_pts = L.offset2d(outline, -T)
    rim_in = [mb.v((x, y, 0.0)) for (x, y) in inner_pts]
    mb.bridge([rings[0], rim_in], closed_ring=True, mat=mat_trim)
    lin = []
    for (d, z) in prof[1:]:
        pts = L.offset2d(outline, -(d + T)) if True else outline
        lin.append([mb.v((x, y, min(z, Hh - T))) for (x, y) in pts])
    mb.bridge([rim_in] + lin, closed_ring=True, mat=mat_line, flip=True)
    mb.cap(lin[-1], mat_line, flip=True)
    if ribs:
        for k in range(4):
            yy = -W_ / 2 + W_ * (k + 1) / 5
            path = [Vector((-L_ / 2 + 0.05 + (L_ - 0.1) * t / 10, yy, Hh + 0.0005)) for t in range(11)]
            L.sweep(mb, path, [(-0.006, -0.001), (0.006, -0.001), (0.0035, 0.004), (-0.0035, 0.004)], mat=mat_shell,
                    caps=True, up=(0, 0, 1))


def build_suitcase():
    L.reset()
    root = L.empty("Suitcase", size=0.3)
    L_, W_, D_ = 0.75, 0.50, 0.25
    Hh = D_ / 2
    HINGE_Y = W_ / 2
    # base (open side up)
    bm = L.MB()
    st = len(bm.verts)
    shell_half(bm, L_, W_, Hh, 0.06, 0.035, 0, 1, 2)
    # flip so the shell face is on the floor and the opening faces up
    L.transform_mb_range(bm, st, Matrix.Translation(Vector((0, 0, Hh))) @ Matrix.Scale(-1, 4, Vector((0, 0, 1))))
    # spinner wheels at the -X end (lying on its back they point sideways)
    for sy in (-1, 1):
        for sz in (0.035, Hh - 0.035):
            cx = -L_ / 2 - 0.018
            L.box(bm, (0.036, 0.05, 0.04), center=(cx + 0.012, sy * 0.19, sz), mat=3)
            L.lathe(bm, [(0, -0.009), (0.022, -0.009), (0.024, -0.006), (0.024, 0.006), (0.022, 0.009), (0, 0.009)], segs=18,
                    mat=4, axis="Y", center=(cx - 0.012, sy * 0.19, sz))
    # carry handle on the +X end
    hp = [Vector((L_ / 2 + 0.002, -0.08, Hh / 2)), Vector((L_ / 2 + 0.03, -0.06, Hh / 2)),
          Vector((L_ / 2 + 0.035, 0.0, Hh / 2)), Vector((L_ / 2 + 0.03, 0.06, Hh / 2)), Vector((L_ / 2 + 0.002, 0.08, Hh / 2))]
    L.sweep(bm, L.polyline_resample3d([tuple(p) for p in bezier_chain(hp)], 16), L.rrect(0.012, 0.024, 0.005, seg=2),
            mat=3, caps=True, up=(0, 0, 1))
    # packed clothes
    for k, (h, sx, mat) in enumerate(((0.035, 0.0, 5), (0.03, 0.01, 6), (0.03, -0.015, 5))):
        superellipsoid(bm, (0.32 - k * 0.02, 0.2 - k * 0.012, h / 2), (sx - 0.02, 0.01, 0.012 + 0.03 * k + h / 2), 0.25, 0.3,
                       20, 8, mat, bulge=0.004, rot=L.rotz(k * 2.0 - 2.0))
    # zip band around the rim
    rim = L.resample(L.rrect(L_ + 0.002, W_ + 0.002, 0.06, seg=10), 72)
    L.sweep(bm, [Vector((x, y, Hh - 0.004)) for (x, y) in rim], L.rrect(0.004, 0.008, 0.0015, seg=1), mat=2,
            closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
    mats = ["TS_Plastic", "TS_Metal", "TS_Fabric", "TS_MetalDark", "TS_Rubber", "TS_Fabric", "TS_FabricDark"]
    base = bm.build("Base", mats)
    # lid: built closed-on-top then rotated open about the hinge (back long edge at the rim)
    lm = L.MB()
    st = len(lm.verts)
    shell_half(lm, L_, W_, Hh, 0.06, 0.035, 0, 1, 2)
    # interior: compression strap pair + divider pocket
    for sx in (-0.18, 0.18):
        L.box(lm, (0.035, W_ - 0.05, 0.004), center=(sx, 0.0, Hh - 0.006), mat=3)
        L.box(lm, (0.05, 0.03, 0.008), center=(sx, 0.0, Hh - 0.009), mat=1)
    L.box(lm, (L_ - 0.1, W_ * 0.42, 0.006), center=(0, -0.06, Hh - 0.012), mat=6)
    L.transform_mb_range(lm, st, Matrix.Translation(Vector((0, 0, Hh))))
    rim2 = L.resample(L.rrect(L_ + 0.002, W_ + 0.002, 0.06, seg=10), 72)
    L.sweep(lm, [Vector((x, y, Hh + 0.004)) for (x, y) in rim2], L.rrect(0.004, 0.008, 0.0015, seg=1), mat=2,
            closed_path=True, caps=False, fixed_binormal=(0, 0, 1))
    lid = lm.build("Lid", ["TS_Plastic", "TS_Metal", "TS_Fabric", "TS_FabricDark", "TS_Rubber", "TS_Fabric", "TS_FabricDark"])
    # open: rotate about the hinge axis (X axis through y=HINGE_Y, z=Hh) by 105 degrees
    hinge = Vector((0, HINGE_Y, Hh))
    R = Matrix.Translation(hinge) @ Matrix.Rotation(math.radians(-105), 4, "X") @ Matrix.Translation(-hinge)
    lid.data.transform(R)
    lift = -min(v.co.z for v in base.data.vertices)
    for ob in (base, lid):
        ob.data.transform(Matrix.Translation(Vector((0, 0, lift))))
    hinge = hinge + Vector((0, 0, lift))
    for ob in (base, lid):
        L.cleanup(ob, 1e-6)
        L.shade(ob, 40.0, True)
        L.add_triangulate(ob, 5)
        L.add_weighted_normal(ob)
        L.apply_modifiers(ob)
        L.uv_smart(ob, angle=60.0)
        ob.parent = root
    L.set_origin(lid, hinge)
    return export_asset(root, "Suitcase", [("Suitcase.png", dict(view=(-0.6, -1.0, 0.75), lens=50))])


# ------------------------------------------------------------------ Phone
def build_phone():
    L.reset()
    root = L.empty("Phone", size=0.03)
    PW, PH, PT = 0.0715, 0.147, 0.0078
    R = 0.0095
    parts = []
    fm = L.MB()
    rrect_slab(fm, PW, PH, PT, R, (0, 0, 0), mat=0, plane="XZ", seg=8)
    frame = fm.build("frame", ["TS_MetalDark"])
    L.add_bevel(frame, 0.0022, 4, 30.0)
    L.apply_modifiers(frame)
    parts.append(frame)
    # front glass (black bezel) and back glass
    gm = L.MB()
    rrect_slab(gm, PW - 0.0016, PH - 0.0016, 0.0006, R - 0.0008, (0, -PT / 2 - 0.0001, 0), mat=0, plane="XZ", seg=8)
    rrect_slab(gm, PW - 0.003, PH - 0.003, 0.0006, R - 0.0015, (0, PT / 2 + 0.0001, 0), mat=1, plane="XZ", seg=8)
    parts.append(gm.build("glass", ["TS_Plastic", "TS_Glass"]))
    # camera plateau + lenses on the back (top-left as seen from the back)
    cm = L.MB()
    cx, cz = 0.017, 0.052
    rrect_slab(cm, 0.03, 0.03, 0.0016, 0.007, (cx, PT / 2 + 0.0009, cz), mat=0, plane="XZ", seg=6)
    for (lx, lz) in ((cx - 0.0068, cz + 0.0068), (cx + 0.0068, cz + 0.0068), (cx - 0.0068, cz - 0.0068)):
        L.lathe(cm, [(0, 0.0), (0.0058, 0.0), (0.0058, 0.0012), (0.0048, 0.0016), (0.0, 0.0016)], segs=24, mat=1,
                axis="Y", center=(lx, PT / 2 + 0.0017, lz))
        L.lathe(cm, [(0, 0.0016), (0.0042, 0.0016), (0.0036, 0.0019), (0.0, 0.0021)], segs=20, mat=2,
                axis="Y", center=(lx, PT / 2 + 0.0017, lz))
    L.lathe(cm, [(0, 0.0), (0.0016, 0.0), (0.0016, 0.0004), (0.0, 0.0005)], segs=12, mat=1, axis="Y",
            center=(cx + 0.0068, PT / 2 + 0.0017, cz - 0.0068))
    parts.append(cm.build("camera", ["TS_MetalDark", "TS_Chrome", "TS_Glass"]))
    # side buttons
    bm = L.MB()
    L.box(bm, (0.0012, 0.0028, 0.016), center=(PW / 2 + 0.0003, 0, 0.03), mat=0)
    for z in (0.038, 0.022):
        L.box(bm, (0.0012, 0.0028, 0.011), center=(-PW / 2 - 0.0003, 0, z), mat=0)
    bob = bm.build("buttons", ["TS_MetalDark"])
    L.add_bevel(bob, 0.0004, 2, 30.0)
    L.apply_modifiers(bob)
    parts.append(bob)
    finish_join(parts, "PhoneBody", root, smooth=35.0)
    # Screen: separate object, 0..1 UVs across its front (u left->right, v bottom->top seen from the front)
    SW, SH = PW - 0.004, PH - 0.0045
    sm = L.MB()
    pts = L.resample(L.rrect(SW, SH, R - 0.0018, seg=10), 96)
    y = -PT / 2 - 0.0006
    f = sm.vs([(px, y, pz) for (px, pz) in pts])
    b = sm.vs([(px, y + 0.0003, pz) for (px, pz) in pts])
    uvf = [((px + SW / 2) / SW, (pz + SH / 2) / SH) for (px, pz) in pts]
    # front face must face -Y: ensure CCW seen from -Y (x right, z up)
    if L.polygon_area(pts) < 0:
        f, b, uvf = f[::-1], b[::-1], uvf[::-1]
    sm.f(tuple(f), 0, uvf)
    sm.f(tuple(b[::-1]), 0, [(1 - u, v) for (u, v) in uvf[::-1]])
    n = len(pts)
    for k in range(n):
        k1 = (k + 1) % n
        sm.f((f[k], b[k], b[k1], f[k1]), 0, [uvf[k], uvf[k], uvf[k1], uvf[k1]])
    screen = sm.build("Screen", ["TS_Glass"], recalc=False)
    L.cleanup(screen)
    L.add_triangulate(screen, 5)
    L.apply_modifiers(screen)
    screen.parent = root
    return export_asset(root, "Phone", [("Phone.png", dict(view=(-0.5, -1.0, 0.35), lens=60)),
                                        ("Phone_back.png", dict(view=(0.5, 1.0, 0.3), lens=60, floor=False))])


BUILDERS = {
    "Sofa": build_sofa, "DiningTable": build_table, "DiningChair": build_chair, "KitchenRun": build_kitchen,
    "Fridge": build_fridge, "BathroomSink": build_bathroom, "Door": build_door, "Suitcase": build_suitcase,
    "Phone": build_phone,
}

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name in (argv or list(BUILDERS)):
        BUILDERS[name]()
