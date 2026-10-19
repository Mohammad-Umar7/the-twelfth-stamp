"""
ts_lib.py - shared helpers for The Twelfth Stamp asset scripts (Blender 5.2, headless).

Every asset script does:
    import sys, os; sys.path.insert(0, os.path.dirname(__file__))
    import ts_lib as L
    L.reset()
    ... build geometry with L.MB / sweeps / lathes ...
    L.finish(obj, ...)            # bevel/weighted normals/apply/UV
    L.export(root, "Name.fbx")    # FBX per the export contract
    L.preview(root, "Name.png")   # Eevee 800x600 three-quarter preview

Conventions (Blender space): metres, +Z up, the asset's FRONT faces -Y.
"""
import bpy
import bmesh
import math
import os
import json
from mathutils import Vector, Matrix, Euler, Quaternion

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS_DIR = os.path.join(REPO, "Game", "Assets", "Resources", "Models")
PREVIEW_DIR = os.path.join(REPO, "Art", "Models")
LOG_DIR = os.path.join(PREVIEW_DIR, "_logs")

MM = 0.001  # millimetre in metres

# ---------------------------------------------------------------------------
# Material slots (names are the contract; colours are preview-only)
# ---------------------------------------------------------------------------
# name: (base rgb, metallic, roughness, extra)
SLOT_LOOK = {
    "TS_Gunmetal":        ((0.21, 0.205, 0.195), 1.0, 0.4, {}),
    "TS_Lens":            ((0.028, 0.04, 0.036), 0.0, 0.03, {"alpha": 0.8, "spec": 0.7}),
    "TS_TempleTip":       ((0.012, 0.012, 0.013), 0.0, 0.18, {}),
    "TS_NosePad":         ((0.62, 0.64, 0.6), 0.0, 0.12, {"alpha": 0.45}),
    "TS_LeatherNavy":     ((0.011, 0.016, 0.042), 0.0, 0.6, {"bump": 0.35}),
    "TS_Lining":          ((0.70, 0.64, 0.52), 0.0, 0.92, {}),
    "TS_Brass":           ((0.52, 0.38, 0.15), 1.0, 0.34, {}),
    "TS_Paper":           ((0.80, 0.75, 0.63), 0.0, 0.85, {}),
    "TS_PaperEdge":       ((0.66, 0.60, 0.48), 0.0, 0.9, {}),
    "TS_Gold":            ((0.78, 0.56, 0.22), 1.0, 0.28, {}),
    "TS_PassengerGlass":  ((0.022, 0.028, 0.027), 0.0, 0.07, {"spec": 0.8}),
    "TS_BronzeSeam":      ((0.50, 0.36, 0.18), 1.0, 0.32, {}),
    "TS_FacePlate":       ((0.004, 0.005, 0.005), 0.0, 0.03, {}),
    "TS_ControllerBody":  ((0.82, 0.82, 0.80), 0.0, 0.42, {}),
    "TS_ControllerButton": ((0.10, 0.10, 0.11), 0.0, 0.38, {}),
    "TS_ControllerDark":  ((0.025, 0.025, 0.028), 0.0, 0.5, {}),
    "TS_Wood":            ((0.42, 0.27, 0.15), 0.0, 0.48, {}),
    "TS_WoodDark":        ((0.12, 0.07, 0.04), 0.0, 0.42, {}),
    "TS_Fabric":          ((0.52, 0.49, 0.45), 0.0, 0.95, {}),
    "TS_FabricDark":      ((0.10, 0.10, 0.12), 0.0, 0.95, {}),
    "TS_Metal":           ((0.62, 0.62, 0.62), 1.0, 0.32, {}),
    "TS_MetalDark":       ((0.10, 0.10, 0.11), 1.0, 0.42, {}),
    "TS_Chrome":          ((0.92, 0.92, 0.92), 1.0, 0.06, {}),
    "TS_Plastic":         ((0.16, 0.16, 0.18), 0.0, 0.4, {}),
    "TS_PlasticWhite":    ((0.86, 0.86, 0.84), 0.0, 0.32, {}),
    "TS_Glass":           ((0.75, 0.82, 0.82), 0.0, 0.02, {"alpha": 0.25, "spec": 0.9}),
    "TS_Ceramic":         ((0.90, 0.90, 0.88), 0.0, 0.1, {}),
    "TS_Stone":           ((0.52, 0.50, 0.47), 0.0, 0.42, {}),
    "TS_Marble":          ((0.86, 0.85, 0.83), 0.0, 0.18, {}),
    "TS_Rubber":          ((0.035, 0.035, 0.035), 0.0, 0.85, {}),
    "TS_EmissiveWarm":    ((1.0, 0.62, 0.28), 0.0, 0.5, {"emit": 6.0}),
    "TS_Leather":         ((0.06, 0.045, 0.035), 0.0, 0.55, {"bump": 0.25}),
}


def _principled(mat):
    for n in mat.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            return n
    return None


def material(name):
    """Get or create a TS_* material with a preview look."""
    if name not in SLOT_LOOK:
        raise ValueError("Unknown material slot " + name)
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    rgb, metal, rough, extra = SLOT_LOOK[name]
    p = _principled(m)
    p.inputs["Base Color"].default_value = (*rgb, 1.0)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if "spec" in extra:
        try:
            p.inputs["Specular IOR Level"].default_value = extra["spec"]
        except KeyError:
            pass
    if "alpha" in extra:
        p.inputs["Alpha"].default_value = extra["alpha"]
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            pass
    if "emit" in extra:
        p.inputs["Emission Color"].default_value = (*rgb, 1.0)
        p.inputs["Emission Strength"].default_value = extra["emit"]
    if "bump" in extra:
        nt = m.node_tree
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 900.0
        noise.inputs["Detail"].default_value = 6.0
        tc = nt.nodes.new("ShaderNodeTexCoord")
        nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = extra["bump"]
        bump.inputs["Distance"].default_value = 0.0004
        nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], p.inputs["Normal"])
    m.diffuse_color = (*rgb, 1.0)
    return m


# ---------------------------------------------------------------------------
# Scene
# ---------------------------------------------------------------------------
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.unit_settings.length_unit = "METERS"
    for d in (MODELS_DIR, PREVIEW_DIR, LOG_DIR):
        os.makedirs(d, exist_ok=True)


def link(obj, parent=None):
    bpy.context.scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = Matrix.Identity(4)
    return obj


def empty(name, location=(0, 0, 0), parent=None, size=0.05):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = size
    e.location = location
    link(e, parent)
    return e


# ---------------------------------------------------------------------------
# Mesh builder
# ---------------------------------------------------------------------------
class MB:
    """Accumulates vertices/faces (with material index + optional UVs)."""

    def __init__(self):
        self.verts = []
        self.faces = []
        self.fmat = []
        self.fuv = []
        self.fsmooth = []

    # -- primitives ---------------------------------------------------------
    def v(self, co):
        self.verts.append(Vector(co))
        return len(self.verts) - 1

    def vs(self, cos):
        return [self.v(c) for c in cos]

    def f(self, idx, mat=0, uv=None, smooth=True):
        self.faces.append(tuple(idx))
        self.fmat.append(mat)
        self.fuv.append(uv)
        self.fsmooth.append(smooth)

    def bridge(self, rings, closed_ring=True, closed_path=False, mat=0, smooth=True,
               u_vals=None, v_vals=None, flip=False):
        """Quads between consecutive rings (lists of vertex indices, equal length)."""
        n = len(rings[0])
        nr = len(rings)
        if u_vals is None:
            u_vals = [i / (n if closed_ring else max(n - 1, 1)) for i in range(n + 1)]
        if v_vals is None:
            v_vals = [j / (nr if closed_path else max(nr - 1, 1)) for j in range(nr + 1)]
        jr = range(nr if closed_path else nr - 1)
        ir = range(n if closed_ring else n - 1)
        for j in jr:
            r0 = rings[j]
            r1 = rings[(j + 1) % nr]
            for i in ir:
                i1 = (i + 1) % n
                a, b, c, d = r0[i], r0[i1], r1[i1], r1[i]
                uv = [(u_vals[i], v_vals[j]), (u_vals[i + 1], v_vals[j]),
                      (u_vals[i + 1], v_vals[j + 1]), (u_vals[i], v_vals[j + 1])]
                if flip:
                    self.f((d, c, b, a), mat, uv[::-1], smooth)
                else:
                    self.f((a, b, c, d), mat, uv, smooth)

    def cap(self, ring, mat=0, flip=False, smooth=False, fan_center=None):
        if fan_center is not None:
            c = self.v(fan_center)
            n = len(ring)
            for i in range(n):
                tri = (ring[i], ring[(i + 1) % n], c)
                self.f(tri[::-1] if flip else tri, mat, None, smooth)
        else:
            self.f(tuple(ring[::-1]) if flip else tuple(ring), mat, None, smooth)

    # -- output ------------------------------------------------------------
    def build(self, name, mats, parent=None, location=(0, 0, 0), merge_dist=1e-7, recalc=True):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        me.validate(clean_customdata=False)
        for m in mats:
            me.materials.append(material(m) if isinstance(m, str) else m)
        if len(me.polygons) == len(self.faces):
            for p, mi, sm in zip(me.polygons, self.fmat, self.fsmooth):
                p.material_index = mi
                p.use_smooth = sm
            if any(u is not None for u in self.fuv):
                uvl = me.uv_layers.new(name="UVMap")
                for p, uv in zip(me.polygons, self.fuv):
                    if uv is None:
                        continue
                    for k, li in enumerate(p.loop_indices):
                        uvl.data[li].uv = uv[k]
        ob = bpy.data.objects.new(name, me)
        ob.location = location
        link(ob, parent)
        if merge_dist:
            merge_by_distance(ob, merge_dist)
        if recalc:
            recalc_normals(ob)
        return ob


# ---------------------------------------------------------------------------
# 2D shape helpers (lists of (x, y), CCW)
# ---------------------------------------------------------------------------
def rrect(w, h, r, seg=6, cx=0.0, cy=0.0):
    r = min(r, w / 2 - 1e-6, h / 2 - 1e-6)
    pts = []
    corners = [(w / 2 - r, -h / 2 + r, -90), (w / 2 - r, h / 2 - r, 0),
               (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180)]
    for (x, y, a0) in corners:
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + x + r * math.cos(a), cy + y + r * math.sin(a)))
    return dedupe2d(pts)


def rquad(corners, radii, seg=8):
    """Polygon through 4 corners (CCW) with individual fillet radii -> smooth outline."""
    out = []
    n = len(corners)
    for i in range(n):
        p0 = Vector(corners[i - 1])
        p1 = Vector(corners[i])
        p2 = Vector(corners[(i + 1) % n])
        r = radii[i]
        d0 = (p0 - p1).normalized()
        d2 = (p2 - p1).normalized()
        ang = math.acos(max(-1, min(1, d0.dot(d2))))
        t = r / math.tan(ang / 2)
        a = p1 + d0 * t
        b = p1 + d2 * t
        bis = (d0 + d2).normalized()
        c = p1 + bis * (r / math.sin(ang / 2))
        a0 = math.atan2(a.y - c.y, a.x - c.x)
        a1 = math.atan2(b.y - c.y, b.x - c.x)
        da = a1 - a0
        while da > math.pi:
            da -= 2 * math.pi
        while da < -math.pi:
            da += 2 * math.pi
        for k in range(seg + 1):
            aa = a0 + da * k / seg
            out.append((c.x + r * math.cos(aa), c.y + r * math.sin(aa)))
    return dedupe2d(out)


def dedupe2d(pts, eps=1e-7):
    out = []
    for p in pts:
        if not out or (abs(p[0] - out[-1][0]) > eps or abs(p[1] - out[-1][1]) > eps):
            out.append(p)
    if len(out) > 1 and abs(out[0][0] - out[-1][0]) < eps and abs(out[0][1] - out[-1][1]) < eps:
        out.pop()
    return out


def resample(pts, count, closed=True):
    """Evenly resample a polyline by arc length."""
    P = [Vector((p[0], p[1], p[2] if len(p) > 2 else 0.0)) for p in pts]
    if closed:
        P = P + [P[0]]
    seg = [(P[i + 1] - P[i]).length for i in range(len(P) - 1)]
    total = sum(seg)
    out = []
    n = count if closed else count - 1
    for k in range(count):
        t = total * k / n
        acc = 0.0
        for i, s in enumerate(seg):
            if acc + s >= t - 1e-12 or i == len(seg) - 1:
                f = 0.0 if s < 1e-12 else min(1.0, max(0.0, (t - acc) / s))
                q = P[i].lerp(P[i + 1], f)
                out.append(q)
                break
            acc += s
    if len(pts[0]) == 2:
        return [(q.x, q.y) for q in out]
    return out


def polygon_area(pts):
    a = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % len(pts)]
        a += x0 * y1 - x1 * y0
    return a / 2


def ensure_ccw(pts):
    return pts if polygon_area(pts) > 0 else pts[::-1]


def offset2d(pts, d):
    """Offset closed CCW polygon outward by d (negative = inward). Miter-limited."""
    n = len(pts)
    out = []
    for i in range(n):
        p0 = Vector(pts[i - 1])
        p1 = Vector(pts[i])
        p2 = Vector(pts[(i + 1) % n])
        e0 = (p1 - p0).normalized()
        e1 = (p2 - p1).normalized()
        n0 = Vector((e0.y, -e0.x))
        n1 = Vector((e1.y, -e1.x))
        m = (n0 + n1)
        if m.length < 1e-9:
            m = n1
        m.normalize()
        cosh = max(0.3, m.dot(n1))
        q = p1 + m * (d / cosh)
        out.append((q.x, q.y))
    return out


def circle2d(r, n, cx=0.0, cy=0.0, a0=0.0):
    return [(cx + r * math.cos(a0 + 2 * math.pi * i / n), cy + r * math.sin(a0 + 2 * math.pi * i / n)) for i in range(n)]


def ellipse2d(rx, ry, n, cx=0.0, cy=0.0):
    return [(cx + rx * math.cos(2 * math.pi * i / n), cy + ry * math.sin(2 * math.pi * i / n)) for i in range(n)]


def superellipse2d(a, b, e, n):
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        c, s = math.cos(t), math.sin(t)
        x = a * math.copysign(abs(c) ** (2 / e), c)
        y = b * math.copysign(abs(s) ** (2 / e), s)
        pts.append((x, y))
    return pts


# ---------------------------------------------------------------------------
# 3D generators (all write into an MB)
# ---------------------------------------------------------------------------
def _frames(path, closed, up_hint=Vector((0, 0, 1)), fixed_binormal=None):
    n = len(path)
    T = []
    for i in range(n):
        if closed:
            a, b = path[(i - 1) % n], path[(i + 1) % n]
        else:
            a, b = path[max(i - 1, 0)], path[min(i + 1, n - 1)]
        t = (b - a)
        T.append(t.normalized() if t.length > 1e-12 else Vector((0, 1, 0)))
    if fixed_binormal is not None:
        B0 = Vector(fixed_binormal).normalized()
        N = []
        B = []
        for t in T:
            nn = B0.cross(t)
            if nn.length < 1e-9:
                nn = Vector((1, 0, 0))
            nn.normalize()
            N.append(nn)
            B.append(t.cross(nn).normalized())
        return T, N, B
    up = Vector(up_hint)
    N0 = up - up.dot(T[0]) * T[0]
    if N0.length < 1e-6:
        alt = Vector((1, 0, 0)) if abs(T[0].x) < 0.9 else Vector((0, 1, 0))
        N0 = alt - alt.dot(T[0]) * T[0]
    N0.normalize()
    N = [N0]
    for i in range(n - 1):
        v1 = path[i + 1] - path[i]
        c1 = v1.dot(v1)
        if c1 < 1e-18:
            N.append(N[-1].copy())
            continue
        rL = N[i] - (2 / c1) * v1.dot(N[i]) * v1
        tL = T[i] - (2 / c1) * v1.dot(T[i]) * v1
        v2 = T[i + 1] - tL
        c2 = v2.dot(v2)
        Ni = rL if c2 < 1e-18 else rL - (2 / c2) * v2.dot(rL) * v2
        Ni = Ni - Ni.dot(T[i + 1]) * T[i + 1]
        N.append(Ni.normalized())
    B = [T[i].cross(N[i]).normalized() for i in range(n)]
    return T, N, B


def sweep(mb, path, profile, mat=0, closed_path=False, caps=True, up=(0, 0, 1),
          fixed_binormal=None, scale=None, smooth=True, cap_smooth=False, twist=None):
    """Sweep a closed 2D profile (x along N, y along B) along a 3D path.
    scale: optional list/func of per-point scale (s or (sx, sy))."""
    path = [Vector(p) for p in path]
    T, N, B = _frames(path, closed_path, Vector(up), fixed_binormal)
    rings = []
    for i, p in enumerate(path):
        s = 1.0
        if scale is not None:
            s = scale(i / (len(path) - 1)) if callable(scale) else scale[i]
        sx, sy = (s, s) if not isinstance(s, (tuple, list)) else s
        ang = 0.0
        if twist is not None:
            ang = twist(i / (len(path) - 1)) if callable(twist) else twist[i]
        ca, sa = math.cos(ang), math.sin(ang)
        ring = []
        for (x, y) in profile:
            x2, y2 = x * ca - y * sa, x * sa + y * ca
            ring.append(mb.v(p + N[i] * (x2 * sx) + B[i] * (y2 * sy)))
        rings.append(ring)
    # u along profile, v along path (arc length normalised)
    L = [0.0]
    for i in range(1, len(path)):
        L.append(L[-1] + (path[i] - path[i - 1]).length)
    if closed_path:
        L.append(L[-1] + (path[0] - path[-1]).length)
    tot = L[-1] if L[-1] > 0 else 1.0
    v_vals = [l / tot for l in L] + ([1.0] if not closed_path else [])
    mb.bridge(rings, closed_ring=True, closed_path=closed_path, mat=mat, smooth=smooth, v_vals=v_vals)
    if caps and not closed_path:
        mb.cap(rings[0], mat, flip=True, smooth=cap_smooth)
        mb.cap(rings[-1], mat, flip=False, smooth=cap_smooth)
    return rings


def lathe(mb, prof, segs=32, mat=0, center=(0, 0, 0), smooth=True, cap_start=True, cap_end=True,
          a0=0.0, a1=2 * math.pi, axis="Z", rot=None, sx=1.0, sy=1.0):
    """Revolve profile [(r, h), ...] around the axis. Poles where r == 0."""
    full = abs((a1 - a0) - 2 * math.pi) < 1e-6
    n = segs if full else segs + 1
    c = Vector(center)
    rings = []
    pole_flags = []
    for (r, h) in prof:
        if r < 1e-9:
            rings.append([mb.v(c + _axis_vec(0, 0, h, axis, rot))])
            pole_flags.append(True)
            continue
        ring = []
        for i in range(n):
            a = a0 + (a1 - a0) * i / segs
            ring.append(mb.v(c + _axis_vec(r * math.cos(a) * sx, r * math.sin(a) * sy, h, axis, rot)))
        rings.append(ring)
        pole_flags.append(False)
    # bridge
    L = [0.0]
    for k in range(1, len(prof)):
        L.append(L[-1] + math.hypot(prof[k][0] - prof[k - 1][0], prof[k][1] - prof[k - 1][1]))
    tot = L[-1] or 1.0
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        v0, v1 = L[k] / tot, L[k + 1] / tot
        if pole_flags[k] and pole_flags[k + 1]:
            continue
        if pole_flags[k]:
            for i in range(n if full else n - 1):
                i1 = (i + 1) % n
                mb.f((r0[0], r1[i1], r1[i]), mat, [(i / segs, v0), ((i + 1) / segs, v1), (i / segs, v1)], smooth)
        elif pole_flags[k + 1]:
            for i in range(n if full else n - 1):
                i1 = (i + 1) % n
                mb.f((r0[i], r0[i1], r1[0]), mat, [(i / segs, v0), ((i + 1) / segs, v0), (i / segs, v1)], smooth)
        else:
            for i in range(n if full else n - 1):
                i1 = (i + 1) % n
                mb.f((r0[i], r0[i1], r1[i1], r1[i]), mat,
                     [(i / segs, v0), ((i + 1) / segs, v0), ((i + 1) / segs, v1), (i / segs, v1)], smooth)
    if full:
        if cap_start and not pole_flags[0]:
            mb.cap(rings[0], mat, flip=True, smooth=False)
        if cap_end and not pole_flags[-1]:
            mb.cap(rings[-1], mat, flip=False, smooth=False)
    return rings


def _axis_vec(x, y, h, axis, rot):
    if axis == "Z":
        v = Vector((x, y, h))
    elif axis == "Y":
        v = Vector((x, h, y))
    elif axis == "-Y":
        v = Vector((x, -h, -y))
    else:  # X
        v = Vector((h, x, y))
    if rot is not None:
        v = rot @ v
    return v


def prism(mb, pts2d, h0, h1, mat=0, basis=None, origin=(0, 0, 0), smooth=False, caps=True,
          side_mat=None, cap_mat=None):
    """Extrude a CCW 2D outline between heights h0..h1 along basis normal.
    basis = (u_axis, v_axis, n_axis) as Vectors; default XY plane extruded along +Z."""
    if basis is None:
        basis = (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    U, V, Nn = (Vector(b) for b in basis)
    o = Vector(origin)
    pts2d = ensure_ccw(list(pts2d)) if (U.cross(V)).dot(Nn) > 0 else ensure_ccw(list(pts2d))[::-1]
    bot = [mb.v(o + U * x + V * y + Nn * h0) for (x, y) in pts2d]
    top = [mb.v(o + U * x + V * y + Nn * h1) for (x, y) in pts2d]
    sm = side_mat if side_mat is not None else mat
    cm = cap_mat if cap_mat is not None else mat
    n = len(pts2d)
    per = [0.0]
    for i in range(1, n + 1):
        a = pts2d[i - 1]
        b = pts2d[i % n]
        per.append(per[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    tot = per[-1] or 1
    for i in range(n):
        i1 = (i + 1) % n
        uv = [(per[i] / tot, 0), (per[i + 1] / tot, 0), (per[i + 1] / tot, 1), (per[i] / tot, 1)]
        mb.f((bot[i], bot[i1], top[i1], top[i]), sm, uv, smooth)
    if caps:
        mb.f(tuple(bot[::-1]), cm, None, False)
        mb.f(tuple(top), cm, None, False)
    return bot, top


def box(mb, size, center=(0, 0, 0), mat=0, rot=None):
    sx, sy, sz = (s / 2 for s in size)
    c = Vector(center)
    corners = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
               (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
    idx = []
    for co in corners:
        v = Vector(co)
        if rot is not None:
            v = rot @ v
        idx.append(mb.v(c + v))
    F = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    for f in F:
        mb.f(tuple(idx[i] for i in f), mat, None, False)
    return idx


def corner_xs(x0, x1, r0, r1, n_corner=6, step=6.0):
    """Column x positions across [x0, x1], dense inside rounded corners of radius r0 (at x0)
    and r1 (at x1)."""
    xs = []
    if r0 > 0:
        for k in range(n_corner):
            th = math.radians(90 * k / n_corner)
            xs.append(x0 + r0 - r0 * math.cos(th))
    a = x0 + r0
    b = x1 - r1
    n = max(1, int(math.ceil((b - a) / step)))
    for k in range(n + 1):
        xs.append(a + (b - a) * k / n)
    if r1 > 0:
        for k in range(n_corner - 1, -1, -1):
            th = math.radians(90 * k / n_corner)
            xs.append(x1 - r1 + r1 * math.cos(th))
    out = []
    for x in sorted(xs):
        if not out or x - out[-1] > 1e-6:
            out.append(x)
    return out


def rounded_extent(x, x0, x1, h, r0, r1):
    """Half-height of a rounded rectangle (corner radius r0 at the x0 end, r1 at x1)."""
    if r0 > 0 and x < x0 + r0:
        d = (x0 + r0) - x
        return h - r0 + math.sqrt(max(0.0, r0 * r0 - d * d))
    if r1 > 0 and x > x1 - r1:
        d = x - (x1 - r1)
        return h - r1 + math.sqrt(max(0.0, r1 * r1 - d * d))
    return h


def ruled_slab(mb, xs, zb, zt, yf, yb, mats=(0, 0, 0, 0, 0, 0), rows=1, uv_box=None):
    """Closed slab spanned over columns xs (ascending). zb/zt(x): bottom/top, yf/yb(x): front/back
    (front faces -Y). mats = (front, back, top, bottom, end_x0, end_x1).
    Front/back UVs are planar 0..1 over uv_box=(x0, x1, z0, z1) (defaults to the slab bounds);
    front u runs left->right as seen from -Y, back is mirrored."""
    n = len(xs)
    if uv_box is None:
        uv_box = (xs[0], xs[-1], min(zb(x) for x in xs), max(zt(x) for x in xs))
    ux0, ux1, vz0, vz1 = uv_box

    def uv(x, z, back=False):
        u = (x - ux0) / (ux1 - ux0)
        v = (z - vz0) / (vz1 - vz0)
        return (1 - u, v) if back else (u, v)
    F = []
    B = []
    for x in xs:
        colf, colb = [], []
        for r in range(rows + 1):
            z = zb(x) + (zt(x) - zb(x)) * r / rows
            colf.append((mb.v((x, yf(x), z)), x, z))
            colb.append((mb.v((x, yb(x), z)), x, z))
        F.append(colf)
        B.append(colb)
    mf, mbk, mt, mbo, m0, m1 = mats
    for i in range(n - 1):
        for r in range(rows):
            a, b, c, d = F[i][r], F[i + 1][r], F[i + 1][r + 1], F[i][r + 1]
            mb.f((a[0], b[0], c[0], d[0]), mf, [uv(a[1], a[2]), uv(b[1], b[2]), uv(c[1], c[2]), uv(d[1], d[2])])
            a, b, c, d = B[i][r], B[i][r + 1], B[i + 1][r + 1], B[i + 1][r]
            mb.f((a[0], b[0], c[0], d[0]), mbk, [uv(a[1], a[2], True), uv(b[1], b[2], True), uv(c[1], c[2], True), uv(d[1], d[2], True)])
        # top wall / bottom wall
        u0 = (xs[i] - ux0) / (ux1 - ux0)
        u1 = (xs[i + 1] - ux0) / (ux1 - ux0)
        mb.f((F[i][-1][0], F[i + 1][-1][0], B[i + 1][-1][0], B[i][-1][0]), mt, [(u0, 1.0), (u1, 1.0), (u1, 0.98), (u0, 0.98)])
        mb.f((F[i][0][0], B[i][0][0], B[i + 1][0][0], F[i + 1][0][0]), mbo, [(u0, 0.0), (u0, 0.02), (u1, 0.02), (u1, 0.0)])
    # end walls
    for (i, m, flip) in ((0, m0, True), (n - 1, m1, False)):
        for r in range(rows):
            q = (F[i][r][0], F[i][r + 1][0], B[i][r + 1][0], B[i][r][0])
            v0 = (F[i][r][2] - vz0) / (vz1 - vz0)
            v1 = (F[i][r + 1][2] - vz0) / (vz1 - vz0)
            uu = 0.0 if i == 0 else 1.0
            mb.f(q[::-1] if flip else q, m, [(uu, v0), (uu, v1), (uu, v1), (uu, v0)][::-1] if flip else [(uu, v0), (uu, v1), (uu, v1), (uu, v0)])
    return F, B


def grid_patch(mb, fn, nu, nv, mat=0, smooth=True, uv=True):
    """Parametric patch fn(u, v)->Vector for u,v in [0,1]; returns 2D index grid."""
    G = [[mb.v(fn(i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            a, b, c, d = G[j][i], G[j][i + 1], G[j + 1][i + 1], G[j + 1][i]
            uvs = [(i / nu, j / nv), ((i + 1) / nu, j / nv), ((i + 1) / nu, (j + 1) / nv), (i / nu, (j + 1) / nv)] if uv else None
            mb.f((a, b, c, d), mat, uvs, smooth)
    return G


def arc_points(center, radius, a0, a1, n, plane="XZ"):
    pts = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        c, s = math.cos(a) * radius, math.sin(a) * radius
        if plane == "XZ":
            pts.append(Vector(center) + Vector((c, 0, s)))
        elif plane == "XY":
            pts.append(Vector(center) + Vector((c, s, 0)))
        else:  # YZ
            pts.append(Vector(center) + Vector((0, c, s)))
    return pts


def bezier_points(p0, p1, p2, p3, n):
    p0, p1, p2, p3 = (Vector(p) for p in (p0, p1, p2, p3))
    out = []
    for i in range(n + 1):
        t = i / n
        mt = 1 - t
        out.append(p0 * mt ** 3 + p1 * 3 * mt * mt * t + p2 * 3 * mt * t * t + p3 * t ** 3)
    return out


def polyline_resample3d(pts, count):
    return resample([tuple(p) for p in pts], count, closed=False)


# ---------------------------------------------------------------------------
# Object-level operations
# ---------------------------------------------------------------------------
def _ctx(obj):
    return bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj],
                                     selected_editable_objects=[obj])


def set_active(obj):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def merge_by_distance(obj, dist=1e-6):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    bm.to_mesh(obj.data)
    bm.free()


def cleanup(obj, dist=1e-7):
    """Merge coincident verts and dissolve zero-area faces / zero-length edges.
    Use before shading/custom normals."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    bmesh.ops.dissolve_degenerate(bm, dist=dist, edges=bm.edges)
    bm.to_mesh(obj.data)
    bm.free()


def recalc_normals(obj, inside=False):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if inside:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()


def shade(obj, angle_deg=35.0, smooth=True):
    me = obj.data
    for p in me.polygons:
        p.use_smooth = smooth
    if smooth:
        try:
            me.set_sharp_from_angle(angle=math.radians(angle_deg))
        except Exception:
            pass


def add_bevel(obj, width, segments=2, angle_deg=40.0, limit="ANGLE", profile=0.5, harden=False,
              clamp=True, vgroup=None):
    m = obj.modifiers.new("Bevel", "BEVEL")
    m.width = width
    m.segments = segments
    m.limit_method = limit
    if limit == "ANGLE":
        m.angle_limit = math.radians(angle_deg)
    if limit == "VGROUP" and vgroup:
        m.vertex_group = vgroup
    m.profile = profile
    m.use_clamp_overlap = clamp
    m.harden_normals = harden
    m.miter_outer = "MITER_ARC"
    return m


def add_weighted_normal(obj, weight=50):
    m = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    m.keep_sharp = True
    m.weight = weight
    m.mode = "FACE_AREA"
    return m


def add_triangulate(obj, min_vertices=5):
    m = obj.modifiers.new("Triangulate", "TRIANGULATE")
    m.quad_method = "BEAUTY"
    m.ngon_method = "BEAUTY"
    m.min_vertices = min_vertices
    try:
        m.keep_custom_normals = True
    except Exception:
        pass
    return m


def add_subsurf(obj, levels=1):
    m = obj.modifiers.new("Subsurf", "SUBSURF")
    m.levels = levels
    m.render_levels = levels
    return m


def apply_modifiers(obj):
    for m in list(obj.modifiers):
        with _ctx(obj):
            try:
                bpy.ops.object.modifier_apply(modifier=m.name)
            except RuntimeError as e:
                print("modifier_apply failed", obj.name, m.name, e)
                obj.modifiers.remove(m)


def boolean(obj, cutter, op="DIFFERENCE", solver=None, apply=True, keep_cutter=False):
    m = obj.modifiers.new("Bool", "BOOLEAN")
    m.operation = op
    m.object = cutter
    solvers = [i.identifier for i in bpy.types.BooleanModifier.bl_rna.properties["solver"].enum_items]
    if solver is None:
        solver = "EXACT" if "EXACT" in solvers else solvers[0]
    m.solver = solver
    cutter.hide_render = True
    cutter.hide_viewport = True
    if apply:
        with _ctx(obj):
            bpy.ops.object.modifier_apply(modifier=m.name)
        cleanup(obj)
        if not keep_cutter:
            me = cutter.data
            bpy.data.objects.remove(cutter, do_unlink=True)
            if me and me.users == 0:
                bpy.data.meshes.remove(me)
    return m


def join(objs, name=None):
    objs = [o for o in objs if o is not None]
    base = objs[0]
    if len(objs) > 1:
        with bpy.context.temp_override(object=base, active_object=base, selected_objects=objs,
                                       selected_editable_objects=objs):
            bpy.ops.object.join()
    if name:
        base.name = name
        base.data.name = name
    return base


def uv_smart(obj, angle=66.0, margin=0.01):
    set_active(obj)
    with _ctx(obj):
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin,
                                 area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")


def uv_cube(obj, size=1.0):
    """Box projection in object space (keeps real-world texel density)."""
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uvl = me.uv_layers.active
    for p in me.polygons:
        n = p.normal
        ax = max(range(3), key=lambda k: abs(n[k]))
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if ax == 0:
                u, v = co.y * math.copysign(1, n.x), co.z
            elif ax == 1:
                u, v = -co.x * math.copysign(1, n.y), co.z
            else:
                u, v = co.x, co.y * math.copysign(1, n.z)
            uvl.data[li].uv = (u / size, v / size)


def has_uv(obj):
    return len(obj.data.uv_layers) > 0


def finish(obj, bevel=None, seg=2, angle=40.0, smooth_angle=35.0, weighted=True, uv="smart",
           harden=False, keep_uv=False, subsurf=0, profile=0.5):
    """Bevel (optional) -> smooth by angle -> triangulate ngons -> weighted normals -> apply.
    Then UV unwrap (unless keep_uv)."""
    cleanup(obj)
    if subsurf:
        add_subsurf(obj, subsurf)
    if bevel:
        add_bevel(obj, bevel, seg, angle, harden=harden, profile=profile)
    shade(obj, smooth_angle, True)
    add_triangulate(obj, 5)
    if weighted:
        add_weighted_normal(obj)
    apply_modifiers(obj)
    if keep_uv and has_uv(obj):
        pass
    elif uv == "smart":
        uv_smart(obj)
    elif uv == "cube":
        uv_cube(obj)
    clean_mesh(obj)
    return obj


def clean_mesh(obj):
    """Remove loose verts/edges (no faces)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    loose_e = [e for e in bm.edges if not e.link_faces]
    if loose_e:
        bmesh.ops.delete(bm, geom=loose_e, context="EDGES")
    loose_v = [v for v in bm.verts if not v.link_faces]
    if loose_v:
        bmesh.ops.delete(bm, geom=loose_v, context="VERTS")
    # keep custom normals: only write back if we removed something
    if loose_e or loose_v:
        bm.to_mesh(obj.data)
    bm.free()


def set_origin(obj, world_point):
    """Move the object's origin to world_point without moving geometry."""
    wp = Vector(world_point)
    mw = obj.matrix_world.copy()
    local = mw.inverted() @ wp
    obj.data.transform(Matrix.Translation(-local))
    obj.matrix_world = mw @ Matrix.Translation(local)


def tris(obj):
    if obj.type != "MESH":
        return 0
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def descendants(root):
    out = [root]
    for c in root.children:
        out += descendants(c)
    return out


def world_bbox(objs):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        if o.type != "MESH":
            continue
        mw = o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            for k in range(3):
                mn[k] = min(mn[k], w[k])
                mx[k] = max(mx[k], w[k])
    return mn, mx


def check_transforms(root):
    issues = []
    for o in descendants(root):
        if any(abs(a) > 1e-6 for a in o.rotation_euler) or any(abs(s - 1) > 1e-6 for s in o.scale):
            issues.append(o.name)
    return issues


def apply_rot_scale(obj):
    """Bake rotation+scale into mesh data (keeps location)."""
    if obj.type == "MESH":
        M = Matrix.LocRotScale(None, obj.rotation_euler, obj.scale)
        obj.data.transform(M)
    obj.rotation_euler = (0, 0, 0)
    obj.scale = (1, 1, 1)


# ---------------------------------------------------------------------------
# Export + stats
# ---------------------------------------------------------------------------
def export(root, filename, extra_info=None):
    objs = descendants(root)
    # bake_space_transform mis-writes grandchildren (spurious 90 deg rotation in the FBX),
    # so every exported object must be a direct child of the root.
    for o in objs:
        if o is not root and o.parent is not root:
            raise RuntimeError("Nested hierarchy not allowed for export: %s -> %s" % (o.name, o.parent.name))
        if o is not root and (any(abs(a) > 1e-6 for a in o.rotation_euler) or any(abs(s - 1) > 1e-6 for s in o.scale)):
            raise RuntimeError("Unapplied rotation/scale on %s" % o.name)
    for o in objs:
        if o.type == "MESH":
            strip_unused_slots(o)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    path = os.path.join(MODELS_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False, bake_anim=False)
    info = stats(root)
    info["file"] = filename
    info["bytes"] = os.path.getsize(path)
    if extra_info:
        info.update(extra_info)
    with open(os.path.join(LOG_DIR, os.path.splitext(filename)[0] + ".build.json"), "w") as fh:
        json.dump(info, fh, indent=2)
    print("EXPORTED", path, info["tris"], "tris", info["bytes"], "bytes")
    return info


def strip_unused_slots(ob):
    """Drop material slots no polygon uses (avoids empty submeshes in Unity)."""
    me = ob.data
    if not me.materials:
        return
    used = sorted({p.material_index for p in me.polygons})
    if len(used) == len(me.materials) and used == list(range(len(me.materials))):
        return
    mats = [me.materials[i] for i in used]
    remap = {old: new for new, old in enumerate(used)}
    idx = [remap[p.material_index] for p in me.polygons]
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    for p, i in zip(me.polygons, idx):
        p.material_index = i


def stats(root):
    objs = descendants(root)
    mn, mx = world_bbox(objs)
    info = {
        "root": root.name,
        "tris": sum(tris(o) for o in objs),
        "dims_m": [round(mx[k] - mn[k], 4) for k in range(3)],
        "bbox_min": [round(mn[k], 4) for k in range(3)],
        "bbox_max": [round(mx[k], 4) for k in range(3)],
        "objects": [],
        "slots": sorted({s.material.name for o in objs if o.type == "MESH" for s in o.material_slots if s.material}),
        "transform_issues": check_transforms(root),
    }
    for o in objs:
        d = {"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
             "loc": [round(c, 5) for c in o.location]}
        if o.type == "MESH":
            d["tris"] = tris(o)
            d["slots"] = [s.material.name for s in o.material_slots if s.material]
            d["uv"] = has_uv(o)
        info["objects"].append(d)
    return info


# ---------------------------------------------------------------------------
# Preview render
# ---------------------------------------------------------------------------
def _engine():
    items = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for cand in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        if cand in items:
            return cand
    return items[0]


def preview(root, png_name, view=(-0.62, -1.0, 0.55), lens=50, floor=True, margin=0.92,
            extra_objs=None, target=None, res=(800, 600), light_scale=1.0, cam_roll=0.0,
            ortho=False, bg=0.42):
    """Render an Eevee preview (800x600, neutral grey) from a three-quarter view direction
    (vector from the target to the camera, in Blender space; front is -Y)."""
    sc = bpy.context.scene
    sc.render.engine = _engine()
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = 48
    except Exception:
        pass
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Medium High Contrast"
    except Exception:
        try:
            sc.view_settings.look = "None"
        except Exception:
            pass
    objs = descendants(root) + (extra_objs or [])
    mn, mx = world_bbox(objs)
    ctr = (mn + mx) / 2 if target is None else Vector(target)
    radius = max((mx - mn).length / 2, 1e-3)
    tmp = []
    # world
    w = bpy.data.worlds.new("PreviewWorld")
    w.use_nodes = True
    bgn = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bgn.inputs["Color"].default_value = (bg, bg, bg, 1)
    bgn.inputs["Strength"].default_value = 0.55
    sc.world = w
    # floor
    if floor:
        fm = bpy.data.materials.new("PreviewFloor")
        fm.use_nodes = True
        p = _principled(fm)
        p.inputs["Base Color"].default_value = (bg * 0.62, bg * 0.62, bg * 0.62, 1)
        p.inputs["Roughness"].default_value = 0.85
        me = bpy.data.meshes.new("PreviewFloor")
        s = radius * 40
        z = mn.z - 0.0005 * radius
        me.from_pydata([(-s, -s, z), (s, -s, z), (s, s, z), (-s, s, z)], [], [(0, 1, 2, 3)])
        me.materials.append(fm)
        fo = bpy.data.objects.new("PreviewFloor", me)
        sc.collection.objects.link(fo)
        tmp.append(fo)
    # camera
    cam_d = bpy.data.cameras.new("PreviewCam")
    cam_d.lens = lens
    cam_d.clip_start = radius * 0.01
    cam_d.clip_end = radius * 400
    cam = bpy.data.objects.new("PreviewCam", cam_d)
    sc.collection.objects.link(cam)
    tmp.append(cam)
    d = Vector(view).normalized()
    sensor = 36.0
    fov_h = 2 * math.atan(sensor / (2 * lens))
    aspect = res[0] / res[1]
    fov_v = 2 * math.atan(math.tan(fov_h / 2) / aspect)
    fov = min(fov_h, fov_v)
    dist = radius * margin / math.sin(fov / 2)
    cam.location = ctr + d * dist
    look = (ctr - cam.location).normalized()
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    cam.rotation_euler.rotate_axis("Z", math.radians(cam_roll))
    if ortho:
        cam_d.type = "ORTHO"
        cam_d.ortho_scale = radius * 2.3
    sc.camera = cam
    # lights: key, fill, rim (area lights sized to the subject)
    def area(name, loc_dir, power, size, color=(1, 1, 1)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy = power * (radius ** 2) * 7 * light_scale
        ld.size = size * radius
        ld.color = color
        lo = bpy.data.objects.new(name, ld)
        lo.location = ctr + Vector(loc_dir).normalized() * radius * 4
        lo.rotation_euler = (ctr - lo.location).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)
        tmp.append(lo)
    area("Key", (-1.0, -0.9, 1.2), 42, 2.5, (1.0, 0.97, 0.92))
    area("Fill", (1.2, -0.7, 0.4), 18, 4.0, (0.92, 0.96, 1.0))
    area("Rim", (0.4, 1.3, 0.9), 45, 2.0, (1.0, 1.0, 1.0))
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    sc.render.filepath = os.path.join(PREVIEW_DIR, png_name)
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    # cleanup preview objects
    for o in tmp:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        try:
            if data is not None and data.users == 0:
                if isinstance(data, bpy.types.Mesh):
                    bpy.data.meshes.remove(data)
                elif isinstance(data, bpy.types.Camera):
                    bpy.data.cameras.remove(data)
                elif isinstance(data, bpy.types.Light):
                    bpy.data.lights.remove(data)
        except Exception:
            pass
    print("PREVIEW", sc.render.filepath)
    return sc.render.filepath


def save_blend(name):
    path = os.path.join(REPO, "Art", "Models", "_blend", name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    return path


def rotz(deg):
    return Matrix.Rotation(math.radians(deg), 3, "Z")


def rotx(deg):
    return Matrix.Rotation(math.radians(deg), 3, "X")


def roty(deg):
    return Matrix.Rotation(math.radians(deg), 3, "Y")


def transform_mb_range(mb, start, M):
    """Apply a 4x4 matrix to verts added since index start."""
    for i in range(start, len(mb.verts)):
        mb.verts[i] = M @ mb.verts[i]


# ---------------------------------------------------------------------------
# Generalised cylinder (shared with build_passenger.py)
# ---------------------------------------------------------------------------
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


