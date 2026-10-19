"""
Controller_Left.fbx / Controller_Right.fbx - generic ring-less touch controllers (no logos).

Blender: pointing direction -Y, face plate up +Z, metres. Root origin = grip centre.
Children: Body (TS_ControllerBody + TS_ControllerDark details), thumbstick, primaryButton,
secondaryButton, trigger, grip (TS_ControllerButton), plus menu (left) / system (right).
Buttons: origin at their own centre. thumbstick: origin at its pivot (stick axis on the face plate).
Left: thumbstick on the outer side (user's left = Blender +X = Unity -X), X/Y buttons inner.
Right: mirrored.
"""
import sys
import os
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ts_lib as L  # noqa: E402

BODY, DARK, BTN = 0, 1, 2
MATS = ["TS_ControllerBody", "TS_ControllerDark", "TS_ControllerButton"]
TILT = math.radians(22.0)

# head: generalised cylinder along Y (back +, front -), section in XZ (theta 90 = top)
H_YS = [0.012, 0.004, -0.014, -0.040, -0.066, -0.084, -0.095, -0.101]
H_TS = [(H_YS[0] - y) / (H_YS[0] - H_YS[-1]) for y in H_YS]
H_W = [0.007, 0.021, 0.0285, 0.0300, 0.0270, 0.0205, 0.0125, 0.0040]
H_H = [0.007, 0.0165, 0.0200, 0.0205, 0.0190, 0.0158, 0.0112, 0.0040]
H_CZ = [0.052, 0.053, 0.054, 0.055, 0.054, 0.052, 0.050, 0.049]
H_E = [2.4, 2.8, 3.3, 3.6, 3.3, 2.8, 2.4, 2.2]
HEAD = L.GenCyl(H_TS, H_YS, H_W, H_H, c2=H_CZ, e=H_E, axis="Y")

# handle: along local Z, then tilted forward by TILT about X
G_TS = [0.0, 0.08, 0.3, 0.55, 0.8, 1.0]
G_Z0, G_Z1 = -0.064, 0.055
HANDLE = L.GenCyl(G_TS, [G_Z0 + (G_Z1 - G_Z0) * t for t in G_TS], [0.010, 0.0170, 0.0188, 0.0192, 0.0190, 0.0182],
                  [0.011, 0.0185, 0.0215, 0.0222, 0.0215, 0.0205], c2=[0.002, 0.0, -0.001, -0.002, -0.002, -0.002],
                  e=2.3)
TILT_M = Matrix.Rotation(TILT, 4, "X")


def t_of_y(y):
    for i in range(len(H_YS) - 1):
        if H_YS[i] >= y >= H_YS[i + 1]:
            return H_TS[i] + (H_TS[i + 1] - H_TS[i]) * (H_YS[i] - y) / (H_YS[i] - H_YS[i + 1])
    return H_TS[-1] if y < H_YS[-1] else H_TS[0]


def top_z(x, y, off=0.0):
    """z of the head's upper surface at (x, y)."""
    t = t_of_y(y)
    rx = HEAD.frx(t) + off
    rz = HEAD.fry(t) + off
    e = HEAD.fe(t)
    c = math.copysign(min(1.0, abs((x - HEAD.f1(t)) / rx)) ** (e / 2), x)
    th = math.acos(max(-1.0, min(1.0, c)))
    s = math.sin(th)
    return HEAD.f2(t) + rz * abs(s) ** (2 / e)


def top_normal(x, y):
    h = 1e-4
    dzx = (top_z(x + h, y) - top_z(x - h, y)) / (2 * h)
    dzy = (top_z(x, y + h) - top_z(x, y - h)) / (2 * h)
    return Vector((-dzx, -dzy, 1.0)).normalized()


def core_mesh(mb, g, nt, nth, mat):
    rings = []
    for i in range(nt + 1):
        t = i / nt
        rings.append([mb.v(g.pt(t, 2 * math.pi * k / nth)) for k in range(nth)])
    mb.bridge(rings, closed_ring=True, mat=mat)
    mb.cap(rings[0], mat, fan_center=g.center(0.0))
    mb.cap(rings[-1], mat, fan_center=g.center(1.0))


def top_patch(mb, outline, lift_top, lift_bot, mat, rings=4):
    """Thin slab following the head's top surface over a closed 2D outline (x, y)."""
    cx = sum(p[0] for p in outline) / len(outline)
    cy = sum(p[1] for p in outline) / len(outline)
    tops, bots = [], []
    for i in range(1, rings + 1):
        s = i / rings
        rt, rb = [], []
        for (x, y) in outline:
            px, py = cx + (x - cx) * s, cy + (y - cy) * s
            rt.append(mb.v((px, py, top_z(px, py) + lift_top)))
            rb.append(mb.v((px, py, top_z(px, py) + lift_bot)))
        tops.append(rt)
        bots.append(rb)
    ct = mb.v((cx, cy, top_z(cx, cy) + lift_top))
    cb = mb.v((cx, cy, top_z(cx, cy) + lift_bot))
    n = len(outline)
    for k in range(n):
        mb.f((ct, tops[0][k], tops[0][(k + 1) % n]), mat)
        mb.f((cb, bots[0][(k + 1) % n], bots[0][k]), mat)
    mb.bridge(tops, closed_ring=True, mat=mat)
    mb.bridge(bots, closed_ring=True, mat=mat, flip=True)
    for k in range(n):
        k1 = (k + 1) % n
        mb.f((tops[-1][k], bots[-1][k], bots[-1][k1], tops[-1][k1]), mat)


def ring_on_top(mb, x, y, r_in, r_out, h, mat, segs=24):
    """Bezel ring sitting on the face plate around (x, y)."""
    z0 = top_z(x, y) + 0.0006
    prof = [(r_in, -0.0008), (r_out, -0.0008), (r_out - 0.0004, h), (r_in + 0.0003, h)]
    st = len(mb.verts)
    rings = []
    for (r, z) in prof:
        rings.append([mb.v((x + r * math.cos(2 * math.pi * k / segs), y + r * math.sin(2 * math.pi * k / segs), z0 + z))
                      for k in range(segs)])
    mb.bridge(rings + [rings[0]], closed_ring=True, mat=mat)


def button_mesh(mb, r, h, mat, dome=0.0006, segs=24, skirt=0.0015):
    """Round button centred on its own origin (bbox centre)."""
    prof = [(0, h / 2 + dome), (r * 0.55, h / 2 + dome * 0.75), (r * 0.9, h / 2 + dome * 0.15), (r, h / 2 - 0.0004),
            (r, -h / 2 + skirt * 0.2), (r + 0.0006, -h / 2), (0, -h / 2)]
    L.lathe(mb, prof, segs=segs, mat=mat)


def build(side):
    s = 1.0 if side == "Left" else -1.0
    name = "Controller_" + side
    L.reset()
    root = L.empty(name, size=0.05)
    # ---------------- body: head U handle
    hm = L.MB()
    core_mesh(hm, HEAD, 28, 36, BODY)
    head = hm.build("Head", MATS)
    gm = L.MB()
    core_mesh(gm, HANDLE, 22, 32, BODY)
    for i in range(len(gm.verts)):
        gm.verts[i] = TILT_M @ gm.verts[i]
    handle = gm.build("Handle", MATS)
    L.boolean(head, handle, "UNION")
    body = head
    body.name = "Body"
    # ---------------- details on the body (dark)
    dm = L.MB()
    fp = [(s * 0.0015 + 0.0235 * math.copysign(abs(math.cos(a)) ** (2 / 2.7), math.cos(a)),
           -0.0395 + 0.0275 * math.copysign(abs(math.sin(a)) ** (2 / 2.7), math.sin(a)))
          for a in [2 * math.pi * k / 48 for k in range(48)]]
    top_patch(dm, fp, 0.0007, -0.0025, DARK, rings=4)
    # nose band (tracking window)
    nose = L.MB()
    rings = []
    for i in range(7):
        t = 0.80 + 0.2 * i / 6
        rings.append([nose.v(HEAD.pt(t, 2 * math.pi * k / 36, 0.0004)) for k in range(36)])
    nose.bridge(rings, closed_ring=True, mat=DARK)
    nose.cap(rings[-1], DARK, fan_center=HEAD.center(1.0) + Vector((0, -0.0004, 0)))
    inner = [nose.v(HEAD.pt(0.80, 2 * math.pi * k / 36, -0.003)) for k in range(36)]
    nose.bridge([inner, rings[0]], closed_ring=True, mat=DARK)
    nose.cap(inner, DARK, flip=True, fan_center=HEAD.center(0.86))
    # thumbstick boot and button bezels
    TS = (s * 0.0105, -0.043)
    PB = (-s * 0.0115, -0.032)
    SB = (-s * 0.0085, -0.0505)
    MB_ = (s * 0.0062, -0.0205)
    st = len(dm.verts)
    L.lathe(dm, [(0, 0.0017), (0.0108, 0.0016), (0.0128, 0.0004), (0.0128, -0.002), (0, -0.002)], segs=32, mat=DARK)
    L.transform_mb_range(dm, st, Matrix.Translation(Vector((TS[0], TS[1], top_z(*TS) + 0.0006))))
    for (x, y, r) in (PB + (0.0062,), SB + (0.0062,), MB_ + (0.0039,)):
        ring_on_top(dm, x, y, r + 0.0003, r + 0.0013, 0.0012, DARK)
    # lanyard post at the bottom of the handle
    st = len(dm.verts)
    L.lathe(dm, [(0, -0.004), (0.0042, -0.0035), (0.0045, 0.0), (0.0042, 0.0035), (0, 0.004)], segs=12, mat=DARK,
            axis="X")
    L.transform_mb_range(dm, st, TILT_M @ Matrix.Translation(Vector((0, 0.004, G_Z0 + 0.001))))
    details = dm.build("Details", MATS)
    nose_ob = nose.build("Nose", MATS)
    body = L.join([body, details, nose_ob], "Body")
    body.parent = root
    # ---------------- thumbstick (origin at pivot)
    tm = L.MB()
    L.lathe(tm, [(0, 0.0), (0.0042, 0.0), (0.0042, 0.0052), (0.0060, 0.0062), (0.0094, 0.0068), (0.0099, 0.0078),
                 (0.0097, 0.0098), (0.0090, 0.0104), (0.0060, 0.0098), (0.0, 0.0095)], segs=32, mat=BTN)
    # grip ridges on the cap rim
    for k in range(24):
        a = 2 * math.pi * k / 24
        L.box(tm, (0.0009, 0.0012, 0.0017), center=(0.0099 * math.cos(a), 0.0099 * math.sin(a), 0.0088),
              mat=BTN, rot=L.rotz(math.degrees(a)))
    stick = tm.build("thumbstick", MATS)
    stick.location = (TS[0], TS[1], top_z(*TS) + 0.0012)
    # ---------------- buttons (origin at own centre)
    objs = [stick]
    for (nm, (x, y), r, h) in (("primaryButton", PB, 0.0060, 0.0034), ("secondaryButton", SB, 0.0060, 0.0034),
                               ("menu" if s > 0 else "system", MB_, 0.0037, 0.0026)):
        bm = L.MB()
        button_mesh(bm, r, h, BTN)
        ob = bm.build(nm, MATS)
        ob.location = (x, y, top_z(x, y) + 0.0006 + h / 2 - 0.0008)
        objs.append(ob)
    # ---------------- trigger (curved paddle under the head front)
    trm = L.MB()
    path = []
    for i in range(13):
        t = i / 12
        path.append(Vector((0.0, -0.059 + 0.006 * t + 0.0045 * math.sin(math.pi * t), 0.038 - 0.034 * t)))
    prof = L.rrect(0.0078, 0.0185, 0.0028, seg=2)
    L.sweep(trm, path, prof, mat=BTN, caps=True, up=(0, -1, 0),
            scale=lambda t: (1.0 - 0.25 * t, 1.0 - 0.12 * t))
    trig = trm.build("trigger", MATS)
    # ---------------- grip button (pad on the inner side of the handle)
    grm = L.MB()
    G = HANDLE
    uu, vv = 8, 10
    th0, th1 = (math.radians(170), math.radians(240)) if s > 0 else (math.radians(-60), math.radians(10))
    rows_o, rows_i = [], []
    for j in range(vv + 1):
        t = 0.42 + 0.42 * j / vv
        ro, ri = [], []
        for i in range(uu + 1):
            th = th0 + (th1 - th0) * i / uu
            edge = min(i, uu - i, j, vv - j)
            lift = 0.0022 if edge >= 2 else (0.0016 if edge == 1 else 0.0008)
            ro.append(grm.v(TILT_M @ G.pt(t, th, lift)))
            ri.append(grm.v(TILT_M @ G.pt(t, th, -0.001)))
        rows_o.append(ro)
        rows_i.append(ri)
    grm.bridge([list(r) for r in rows_o], closed_ring=False, mat=BTN)
    grm.bridge([list(r) for r in rows_i], closed_ring=False, mat=BTN, flip=True)
    loop_o = rows_o[0] + [rows_o[j][-1] for j in range(1, vv + 1)] + rows_o[-1][-2::-1] + [rows_o[j][0] for j in range(vv - 1, 0, -1)]
    loop_i = rows_i[0] + [rows_i[j][-1] for j in range(1, vv + 1)] + rows_i[-1][-2::-1] + [rows_i[j][0] for j in range(vv - 1, 0, -1)]
    for k in range(len(loop_o)):
        k1 = (k + 1) % len(loop_o)
        grm.f((loop_o[k], loop_o[k1], loop_i[k1], loop_i[k]), BTN)
    grip = grm.build("grip", MATS)
    objs += [trig, grip]
    # ---------------- finish
    for ob in [body] + objs:
        L.cleanup(ob, 1e-7)
        L.shade(ob, 40.0, True)
        L.add_triangulate(ob, 5)
        L.add_weighted_normal(ob)
        L.apply_modifiers(ob)
        L.uv_smart(ob, angle=60.0)
        ob.parent = root
    # buttons/trigger/grip: origin at own bbox centre
    for ob in objs:
        if ob is stick:
            continue
        mn, mx = L.world_bbox([ob])
        L.set_origin(ob, (mn + mx) / 2)
    bpy.context.view_layer.update()
    info = L.export(root, name + ".fbx")
    L.preview(root, name + ".png", view=(-0.7, -1.0, 0.65), lens=55)
    L.preview(root, name + "_top.png", view=(0.0, -0.25, 1.0), lens=70, floor=False, margin=0.75)
    L.preview(root, name + "_side.png", view=(-s * 1.0, 0.0, 0.1), lens=60, floor=False)
    L.save_blend(name + ".blend")
    return info


if __name__ == "__main__":
    build("Left")
    build("Right")
