"""
GlassesCase.fbx (reference A): hard clamshell spectacle case, exported closed.

Built in millimetres (converted to metres). Front (clasp) faces -Y, hinge at the back (+Y).
Root "GlassesCase" origin = bottom centre (sits on a surface).
Children:
  CaseBottom - shallow tray; origin on the hinge line (back edge at seam height)
  CaseLid    - domed lid; origin exactly on the hinge axis. Opens with a NEGATIVE rotation
               about its local X in Unity (positive -> into the base). ~-105 deg = fully open.
Slots: TS_LeatherNavy (outside), TS_Lining (inside), TS_Brass (seam lips, clasp).
"""
import sys
import os
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402

LEN, WID, HEI = 165.0, 62.0, 45.0
CORNER_R = 27.0
SEAM = 16.0          # seam height (top of the tray)
T = 2.4              # shell + lining thickness
RB = 6.0             # tray bottom fillet radius
LID_WALL = 4.0       # vertical part of the lid wall above the seam
DOME_IN, DOME_H = 24.0, HEI - SEAM - LID_WALL   # dome inset / height
N = 72               # samples around the outline
HINGE = Vector((0.0, WID / 2, SEAM))   # hinge axis point (axis along X)

MAT_OUT, MAT_IN, MAT_BRASS = 0, 1, 2


def outline():
    pts = L.rrect(LEN, WID, CORNER_R, seg=16)
    pts = L.resample(L.ensure_ccw(pts), N, closed=True)
    # start the loop at the back-centre so the UV seam hides at the hinge
    k = max(range(len(pts)), key=lambda i: (pts[i][1], -abs(pts[i][0])))
    return pts[k:] + pts[:k]


OUT = outline()


def ring(mb, d, z):
    pts = L.offset2d(OUT, -d) if d > 1e-9 else OUT
    return [mb.v((x, y, z)) for (x, y) in pts]


def tray_profiles():
    out = []
    # bottom fillet: from the flat bottom (d=RB, z=0) to the wall (d=0, z=RB)
    for i in range(0, 5):
        phi = math.radians(90 * i / 4)
        out.append((RB - RB * math.sin(phi), RB - RB * math.cos(phi), MAT_OUT))
    out.append((0.0, SEAM - 0.9, MAT_OUT))
    out.append((0.35, SEAM - 0.55, MAT_OUT))      # chamfer into the seam groove
    out.append((0.35, SEAM - 0.55, MAT_BRASS))    # split for the brass band
    out.append((0.35, SEAM, MAT_BRASS))
    inn = [(T, SEAM, MAT_BRASS), (T, SEAM, MAT_IN)]  # lip top (brass), then lining
    ri = RB - T
    # inner wall down, inner fillet to the inner floor
    for i in range(3, -1, -1):
        phi = math.radians(90 * i / 3)
        inn.append((RB - ri * math.sin(phi), RB - ri * math.cos(phi), MAT_IN))
    return out, inn


def lid_profiles():
    out = []
    zw = SEAM + LID_WALL
    # dome from the top cap (d=DOME_IN, z=HEI) down to the wall (d=0, z=zw)
    steps = 7
    for i in range(steps, -1, -1):
        psi = math.radians(90 * i / steps)
        out.append((DOME_IN * (1 - math.cos(psi)), zw + DOME_H * math.sin(psi), MAT_OUT))
    out.append((0.0, SEAM + 0.9, MAT_OUT))
    out.append((0.35, SEAM + 0.55, MAT_OUT))
    out.append((0.35, SEAM + 0.55, MAT_BRASS))
    out.append((0.35, SEAM, MAT_BRASS))
    inn = [(T, SEAM, MAT_BRASS), (T, SEAM, MAT_IN)]
    a_in, b_in = DOME_IN - T, DOME_H - T
    for i in range(0, 6):
        psi = math.radians(90 * i / 5)
        inn.append((T + a_in * (1 - math.cos(psi)), zw + b_in * math.sin(psi), MAT_IN))
    return out, inn


def build_shell_obj(name, profiles, top):
    mb = L.MB()
    pout, pin = profiles
    rings = [ring(mb, d, z) for (d, z, m) in pout + pin]
    mats = [m for (d, z, m) in pout + pin]
    for j in range(len(rings) - 1):
        # band material: brass if both ends brass, lining if either is lining, else leather
        a, b = mats[j], mats[j + 1]
        if a == MAT_BRASS and b == MAT_BRASS:
            m = MAT_BRASS
        elif MAT_IN in (a, b) and MAT_BRASS not in (a, b):
            m = MAT_IN
        elif a == MAT_OUT and b == MAT_OUT:
            m = MAT_OUT
        else:
            m = None  # zero-height split band between materials: skip
        if m is None:
            continue
        mb.bridge([rings[j], rings[j + 1]], closed_ring=True, mat=m)
    mb.cap(rings[0], MAT_OUT)
    mb.cap(rings[-1], MAT_IN)
    ob = mb.build(name, ["TS_LeatherNavy", "TS_Lining", "TS_Brass"])
    return ob


def pill(mb, w, h, depth, center, mat, dome=0.6, segs=10, rot=None):
    """Stadium-shaped brass piece standing on the XZ plane, protruding toward -Y."""
    st = len(mb.verts)
    pts = L.rrect(w, h, h / 2 - 0.01, seg=segs // 2)
    pts = L.resample(pts, 22, closed=True)
    base = L.offset2d(pts, 0.0)
    rings = []
    # side wall + domed face: offsets inward as it rises toward -Y
    layers = [(0.0, 0.0), (0.0, depth - dome), (0.45, depth - dome * 0.35), (1.2, depth)]
    for (ins, dy) in layers:
        p2 = L.offset2d(base, -ins) if ins > 0 else base
        rings.append([mb.v((x, -dy, z)) for (x, z) in p2])
    for j in range(len(rings) - 1):
        mb.bridge([rings[j], rings[j + 1]], closed_ring=True, mat=mat)
    mb.cap(rings[0], mat)
    mb.cap(rings[-1], mat)
    M = Matrix.Translation(Vector(center))
    if rot is not None:
        M = M @ rot.to_4x4()
    L.transform_mb_range(mb, st, M)


def build():
    L.reset()
    root = L.empty("GlassesCase", size=20)
    # ---- tray
    bottom = build_shell_obj("CaseBottom", tray_profiles(), False)
    cm = L.MB()
    # clasp lower button (on the tray front wall, y = -WID/2)
    pill(cm, 15.0, 8.4, 2.4, (0.0, -WID / 2 + 0.2, SEAM - 5.6), 0)
    # mounting plate behind the button
    L.prism(cm, L.rrect(17.0, 9.0, 2.6, seg=4), -WID / 2 - 0.7, -WID / 2 + 0.4, mat=0,
            basis=(Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))), origin=(0, 0, SEAM - 5.4))
    # hinge roll along the back seam (leather covered)
    L.lathe(cm, [(0, -55.5), (1.1, -55.4), (1.6, -54.6), (1.6, 54.6), (1.1, 55.4), (0, 55.5)], segs=12,
            mat=1, axis="X", center=(0, WID / 2, SEAM))
    clasp_b = cm.build("ClaspB", ["TS_Brass", "TS_LeatherNavy"])
    L.add_bevel(clasp_b, 0.25, 1, 35.0)
    L.apply_modifiers(clasp_b)
    bottom = L.join([bottom, clasp_b], "CaseBottom")
    # ---- lid
    lid = build_shell_obj("CaseLid", lid_profiles(), True)
    lm = L.MB()
    pill(lm, 13.0, 5.2, 1.9, (0.0, -WID / 2 + 0.2, SEAM + 3.4), 0)
    # small lip that hooks over the button
    L.box(lm, (9.0, 2.0, 1.6), center=(0.0, -WID / 2 - 0.9, SEAM + 0.75), mat=0)
    clasp_l = lm.build("ClaspL", ["TS_Brass"])
    L.add_bevel(clasp_l, 0.25, 1, 35.0)
    L.apply_modifiers(clasp_l)
    lid = L.join([lid, clasp_l], "CaseLid")
    for ob in (bottom, lid):
        L.cleanup(ob, 1e-5)
        L.shade(ob, 42.0, True)
        L.add_triangulate(ob, 5)
        L.add_weighted_normal(ob)
        L.apply_modifiers(ob)
        L.uv_smart(ob, angle=60.0)
        ob.parent = root
        # origin on the hinge line / hinge axis
        L.set_origin(ob, HINGE)
    # mm -> m
    for o in L.descendants(root):
        if o.type == "MESH":
            o.data.transform(Matrix.Scale(L.MM, 4))
        if o is not root:
            o.location = o.location * L.MM
    bpy.context.view_layer.update()
    info = L.export(root, "GlassesCase.fbx")
    L.preview(root, "GlassesCase.png", view=(-0.55, -1.0, 0.62), lens=60)
    # QA: open the lid in the preview only (not exported)
    lid.rotation_euler.x = math.radians(-105)
    bpy.context.view_layer.update()
    L.preview(root, "GlassesCase_open.png", view=(-0.45, -1.0, 0.95), lens=60)
    lid.rotation_euler.x = 0.0
    L.save_blend("GlassesCase.blend")
    return info


if __name__ == "__main__":
    build()
