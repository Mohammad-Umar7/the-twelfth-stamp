"""
Saeed.fbx - the player, Saeed (about 25, Emirati), for the bathroom mirror and the ending.

Body: the MPFB 2 (MakeHuman) base human, CC0, built headless: male, 25, slim; MPFB's game_engine rig and
its weights, bones renamed for Unity. Modelled on top of it: the white kandura (the body shell from MPFB's
tights helper above the chest, a lofted straight robe below it with soft vertical folds, a round
collarless neckline, front placket, furakha tassel, plain cuffs), short curly hair with volume on top, a
short full beard and moustache, brows, eyes, a brown leather crossbody satchel (right shoulder to left hip,
two slider buckles) and brown suede Chelsea boots. Body parts the kandura and the boots hide are removed.
Textures are baked from procedural shaders (albedo and normal per material slot).

Blender axes: front -Y, up +Z, metres, feet at the origin. Exported (armature + one skinned mesh) so the
front faces Unity +Z like every other model in Resources/Models.

usage: blender -b --python-exit-code 1 -P build_saeed.py [-- <work_dir> <first_stage> <last_stage> [<out_root>]]
(work_dir defaults to %LOCALAPPDATA%\TheTwelfthStamp\Blender\saeed_work, outside the repo; stages 1 to 4)
stages: 1 body and rig, 2 kandura, boots, satchel, 3 head (hair, beard, brows, eyes), 4 finish (weights,
trim, join, UVs, bake, rename, export, previews). Each stage saves <work_dir>/saeed_s<N>.blend. Outputs go to
the repo (Resources/Models/Saeed.fbx, Resources/Art/Saeed/*.png, Art/Models/Saeed*.png) unless <out_root> is
given (then <out_root>/Models, <out_root>/Art/Saeed, <out_root>/Previews).
"""
import sys
import os
import math
import random

import bpy
import bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, r"C:\Dev\12th-Stamp\Tools\blender"):
    if os.path.isfile(os.path.join(p, "ts_lib.py")) and p not in sys.path:
        sys.path.insert(0, p)
import ts_lib as L  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
WORK = ARGS[0] if len(ARGS) > 0 else os.path.join(os.environ.get("LOCALAPPDATA", os.path.expanduser("~")), "TheTwelfthStamp", "Blender", "saeed_work")
FIRST = int(ARGS[1]) if len(ARGS) > 1 else 1
LAST = int(ARGS[2]) if len(ARGS) > 2 else 4
OUT = ARGS[3] if len(ARGS) > 3 else None
os.makedirs(WORK, exist_ok=True)
if OUT:
    L.MODELS_DIR = os.path.join(OUT, "Models")
    L.PREVIEW_DIR = os.path.join(OUT, "Previews")
    L.LOG_DIR = os.path.join(OUT, "Previews", "_logs")
    ART_DIR = os.path.join(OUT, "Art", "Saeed")
else:
    ART_DIR = os.path.join(L.REPO, "Game", "Assets", "Resources", "Art", "Saeed")
for d_ in (L.MODELS_DIR, L.PREVIEW_DIR, L.LOG_DIR, ART_DIR):
    os.makedirs(d_, exist_ok=True)

HEIGHT = 1.78
SLOTS = ["TS_Kandura", "TS_Skin", "TS_Hair", "TS_Leather", "TS_Suede", "TS_Eyes"]
KAND, SKIN, HAIR, LEATHER, SUEDE, EYES = range(6)
ARM_BONES = ("clavicle", "upperarm", "lowerarm", "hand", "thumb", "index", "middle", "ring", "pinky")
LEG_BONES = ("thigh", "calf", "foot", "ball")
random.seed(12)


def log(*a):
    print("SAEED", *a)
    sys.stdout.flush()


# ----------------------------------------------------------------------------------------------- basics
def setup():
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.context.preferences.use_preferences_save = False   # this session's add-on state is never saved
    bpy.ops.preferences.addon_enable(module="bl_ext.user_default.mpfb")
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0


def save(stage):
    path = os.path.join(WORK, "saeed_s%d.blend" % stage)
    bpy.ops.wm.save_as_mainfile(filepath=path)
    log("saved", path)


def load(stage):
    path = os.path.join(WORK, "saeed_s%d.blend" % stage)
    bpy.ops.wm.open_mainfile(filepath=path)
    bpy.context.preferences.use_preferences_save = False
    log("loaded", path)


def ob(name):
    return bpy.data.objects[name]


def duplicate(src, name):
    new = src.copy()
    new.data = src.data.copy()
    new.name = name
    new.data.name = name
    bpy.context.scene.collection.objects.link(new)
    return new


def mats_for(obj, slots):
    obj.data.materials.clear()
    for s in slots:
        obj.data.materials.append(material(s))


def material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
    return m


def bone_head(rig, name):
    return rig.matrix_world @ rig.data.bones[name].head_local


def bone_tail(rig, name):
    return rig.matrix_world @ rig.data.bones[name].tail_local


def deform_names(o):
    return {g.index: g.name for g in o.vertex_groups}


BONES = set()   # the rig's bone names (MPFB meshes also carry region groups such as "fingernails" at weight 1)


def dominant(weights, names):
    """The bone with the largest weight among a vertex's groups (names: group index -> name)."""
    best, bw = None, 0.0
    for gi, w in weights.items():
        n = names.get(gi)
        if n is None or n not in BONES:
            continue
        if w > bw:
            best, bw = n, w
    return best or ""


def taubin(bm, verts, iterations=8, lam=0.5, mu=-0.53, keep=None):
    """Smoothing that keeps the volume (alternating shrink and inflate steps)."""
    for _ in range(iterations):
        laplacian(bm, verts, 1, lam, keep)
        laplacian(bm, verts, 1, mu, keep)


def is_arm(bone):
    return bone.startswith(ARM_BONES)


def is_leg(bone):
    return bone.startswith(LEG_BONES)


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))) if b != a else (1.0 if x >= b else 0.0)
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def boundary_loops(bm, verts_filter=None):
    """Closed boundary loops as lists of BMVerts (optionally only edges whose verts pass the filter)."""
    edges = [e for e in bm.edges if e.is_boundary and (verts_filter is None or all(verts_filter(v) for v in e.verts))]
    adj = {}
    for e in edges:
        a, b = e.verts
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    seen, loops = set(), []
    for start in adj:
        if start in seen:
            continue
        loop, prev, cur = [start], None, start
        seen.add(start)
        while True:
            nxt = [n for n in adj[cur] if n is not prev and n not in seen]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            seen.add(cur)
            loop.append(cur)
        if len(loop) > 2:
            loops.append(loop)
    return loops


def hull2d(pts):
    pts = sorted(set((round(p[0], 5), round(p[1], 5)) for p in pts))
    if len(pts) < 3:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def ray_hull(hull, c, theta):
    """Distance from c along angle theta to the convex hull's boundary."""
    d = (math.cos(theta), math.sin(theta))
    best = 0.0
    n = len(hull)
    for i in range(n):
        a, b = hull[i], hull[(i + 1) % n]
        ex, ey = b[0] - a[0], b[1] - a[1]
        den = d[0] * ey - d[1] * ex
        if abs(den) < 1e-12:
            continue
        wx, wy = a[0] - c[0], a[1] - c[1]
        t = (wx * ey - wy * ex) / den
        s = (wx * d[1] - wy * d[0]) / den
        if t > 0 and -1e-6 <= s <= 1 + 1e-6:
            best = max(best, t)
    return best


def bvh_of(o):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.transform(o.matrix_world)
    t = BVHTree.FromBMesh(bm)
    bm.free()
    return t


def laplacian(bm, verts, iterations=4, factor=0.5, keep=None):
    """Plain Laplacian smoothing of the given verts (keep: verts that stay put)."""
    vs = [v for v in verts if keep is None or v not in keep]
    for _ in range(iterations):
        new = {}
        for v in vs:
            nb = [e.other_vert(v) for e in v.link_edges]
            if not nb:
                continue
            avg = sum((n.co for n in nb), Vector()) / len(nb)
            new[v] = v.co.lerp(avg, factor)
        for v, co in new.items():
            v.co = co


# ----------------------------------------------------------------------------------------------- stage 1
def stage_body():
    from bl_ext.user_default.mpfb.services.humanservice import HumanService
    from bl_ext.user_default.mpfb.services.targetservice import TargetService

    macros = TargetService.get_default_macro_info_dict()
    macros.update({"gender": 1.0, "age": 0.5, "muscle": 0.55, "weight": 0.4, "height": 0.5, "proportions": 0.65})
    macros["race"] = {"african": 0.08, "asian": 0.10, "caucasian": 0.82}
    base = HumanService.create_human(mask_helpers=False, detailed_helpers=True, extra_vertex_groups=True,
                                     feet_on_ground=True, scale=0.1, macro_detail_dict=macros)
    # A calm, slightly amused half-smile; a gentle aquiline nose; defined, slightly lowered brows; a slim oval
    # face with defined cheekbones and jaw (the user's turnaround).
    TargetService.bulk_load_targets(base, [
        {"target": "mouth-angles-up", "value": 0.35},
        {"target": "mouth-lowerlip-volume-incr", "value": 0.05},
        {"target": "nose-hump-incr", "value": 0.3},
        {"target": "nose-volume-incr", "value": 0.08},
        {"target": "nose-scale-vert-incr", "value": 0.1},
        {"target": "nose-scale-horiz-decr", "value": 0.15},
        {"target": "nose-nostrils-width-decr", "value": 0.12},
        {"target": "eyebrows-trans-down", "value": 0.15},
        {"target": "head-oval", "value": 0.5},
        {"target": "head-scale-horiz-decr", "value": 0.12},
        {"target": "l-cheek-bones-incr", "value": 0.35},
        {"target": "r-cheek-bones-incr", "value": 0.35},
        {"target": "l-cheek-volume-decr", "value": 0.3},
        {"target": "r-cheek-volume-decr", "value": 0.3},
        {"target": "chin-prominent-incr", "value": 0.2},
        {"target": "chin-width-decr", "value": 0.15},
        {"target": "neck-scale-horiz-decr", "value": 0.08},
    ])
    rig = HumanService.add_builtin_rig(base, "game_engine")
    TargetService.bake_targets(base)
    rig.name = "SaeedRig"
    base.name = "BaseHuman"
    BONES.update(rig.data.bones.keys())

    # Scale to Saeed's height (the top of the skull).
    me = base.data
    gi = {g.name: g.index for g in base.vertex_groups}
    body_g = gi["body"]
    zmax = max(v.co.z for v in me.vertices if any(g.group == body_g and g.weight > 0.5 for g in v.groups))
    s = HEIGHT / zmax
    me.transform(Matrix.Scale(s, 4))
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in rig.data.edit_bones:
        eb.head = eb.head * s
        eb.tail = eb.tail * s
    bpy.ops.object.mode_set(mode="OBJECT")
    log("scaled by", round(s, 4), "skull top", round(zmax * s, 4))

    def centre(group, thr=0.5):
        g = gi[group]
        pts = [v.co.copy() for v in me.vertices if any(x.group == g and x.weight > thr for x in v.groups)]
        return sum(pts, Vector()) / len(pts), pts

    eye_l, pts = centre("helper-l-eye")
    eye_r, _ = centre("helper-r-eye")
    eye_rad = sum((p - eye_l).length for p in pts) / len(pts)
    teeth, _ = centre("helper-upper-teeth")
    tongue, _ = centre("helper-tongue")

    tights = duplicate(base, "Tights")
    body = duplicate(base, "Body")
    for o, group in ((tights, "helper-tights"), (body, "body")):
        bm = bmesh.new()
        bm.from_mesh(o.data)
        dl = bm.verts.layers.deform.active
        g = o.vertex_groups[group].index
        kill = [v for v in bm.verts if v[dl].get(g, 0.0) < 0.5]
        bmesh.ops.delete(bm, geom=kill, context="VERTS")
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()
    bpy.data.objects.remove(base, do_unlink=True)

    body["eye_l"] = list(eye_l)
    body["eye_r"] = list(eye_r)
    body["eye_rad"] = eye_rad
    body["teeth"] = list(teeth)
    body["tongue"] = list(tongue)
    log("eyes", [round(c, 4) for c in eye_l], round(eye_rad, 4), "teeth", [round(c, 4) for c in teeth])
    log("body verts", len(body.data.vertices), "tights verts", len(tights.data.vertices))


# ----------------------------------------------------------------------------------------------- stage 2
def kandura_levels(rig):
    neck = bone_head(rig, "neck_01")
    return {
        "z_c": bone_head(rig, "spine_03").z,          # where the lofted robe takes over from the body shell
        "z_hips": bone_head(rig, "pelvis").z - 0.04,  # folds start below here
        "z_hem": 0.07,                                # ankle length: the boots show below
        "neck": neck,
    }


def neckline_side(p, neck):
    """> 0 above the round neckline (front lower than the back)."""
    f = Vector((0.0, neck.y - 0.06, neck.z - 0.024))
    b = Vector((0.0, neck.y + 0.06, neck.z + 0.012))
    n = (b - f).cross(Vector((1.0, 0.0, 0.0))).normalized()
    if n.z < 0:
        n = -n
    return (p - f).dot(n)


def wrist_frame(rig, side):
    elbow = bone_head(rig, "lowerarm_" + side)
    wrist = bone_head(rig, "hand_" + side)
    d = (wrist - elbow).normalized()
    return elbow, wrist, d


def stage_kandura():
    rig = ob("SaeedRig")
    body = ob("Body")
    tights = ob("Tights")
    lv = kandura_levels(rig)
    z_c, z_hem, z_hips, neck = lv["z_c"], lv["z_hem"], lv["z_hips"], lv["neck"]
    wf = {s: wrist_frame(rig, s) for s in ("l", "r")}
    cuff_back = 0.012          # the sleeve ends this far short of the wrist joint
    side_of = lambda co: "l" if co.x > 0 else "r"

    top = duplicate(tights, "Kandura")
    top.modifiers.clear()
    top.parent = None
    names = deform_names(top)
    bm = bmesh.new()
    bm.from_mesh(top.data)
    dl = bm.verts.layers.deform.active
    dom = {v: dominant(v[dl], names) for v in bm.verts}

    # Cut the torso at z_c (torso faces only: the forearms hang below it), the sleeves at the cuff and the
    # neck at the neckline, then drop the legs, the hands and everything past the cuts.
    torso_faces = [f for f in bm.faces if not any(is_arm(dom[v]) or is_leg(dom[v]) for v in f.verts)]
    geom = list({e for f in torso_faces for e in f.edges}) + torso_faces + list({v for f in torso_faces for v in f.verts})
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z_c), plane_no=(0, 0, 1))
    for s in ("l", "r"):
        elbow, wrist, d = wf[s]
        cut = wrist - d * cuff_back
        arm_faces = [f for f in bm.faces if any(is_arm(dom.get(v, "")) for v in f.verts) and all((v.co.x > 0) == (s == "l") for v in f.verts)]
        g = list({e for f in arm_faces for e in f.edges}) + arm_faces + list({v for f in arm_faces for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=g, plane_co=cut, plane_no=d)
    g = list(bm.verts) + list(bm.edges) + list(bm.faces)
    nf = Vector((0.0, neck.y - 0.06, neck.z - 0.024))
    nb_ = Vector((0.0, neck.y + 0.06, neck.z + 0.012))
    nn = (nb_ - nf).cross(Vector((1.0, 0.0, 0.0))).normalized()
    if nn.z < 0:
        nn = -nn
    bmesh.ops.bisect_plane(bm, geom=g, plane_co=nf, plane_no=nn)

    # Re-derive dominance for the new verts (nearest old neighbour) and delete.
    def dom_of(v):
        if v in dom:
            return dom[v]
        for e in v.link_edges:
            o = e.other_vert(v)
            if o in dom:
                return dom[o]
        return ""

    kill = []
    for v in bm.verts:
        b = dom_of(v)
        co = v.co
        if is_leg(b):
            kill.append(v)
            continue
        if neckline_side(co, neck) > 1e-5:
            kill.append(v)
            continue
        if is_arm(b) and not b.startswith("clavicle"):
            elbow, wrist, d = wf[side_of(co)]
            if (co - (wrist - d * cuff_back)).dot(d) > 1e-5:
                kill.append(v)
            continue
        if co.z < z_c - 1e-5:
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    # Keep the biggest connected piece (bisect slivers, stray islands).
    bm.verts.ensure_lookup_table()
    seen, best = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        comp, stack = [], [v]
        seen.add(v)
        while stack:
            x = stack.pop()
            comp.append(x)
            for e in x.link_edges:
                o = e.other_vert(x)
                if o not in seen:
                    seen.add(o)
                    stack.append(o)
        if len(comp) > len(best):
            best = comp
    keep = set(best)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep], context="VERTS")

    # Inflate: a loose fit over the shoulders and chest, roomy sleeves narrowing to the cuff.
    bm.normal_update()
    moves = {}
    for v in bm.verts:
        b = dom_of(v)
        co = v.co
        if is_arm(b) and not b.startswith("clavicle"):
            elbow, wrist, d = wf[side_of(co)]
            shoulder = bone_head(rig, "upperarm_" + side_of(co))
            t_all = (co - shoulder).length / max(1e-6, (wrist - shoulder).length)
            off = lerp(0.026, 0.011, smoothstep(0.35, 1.0, t_all))
        else:
            off = 0.013 if co.z >= neck.z - 0.15 else lerp(0.022, 0.013, smoothstep(z_c, neck.z - 0.15, co.z))
            off = lerp(0.0035, off, smoothstep(0.0, 0.07, -neckline_side(co, neck)))   # snug at the collar
        moves[v] = co + v.normal * off
    for v, co in moves.items():
        v.co = co
    loops = boundary_loops(bm)
    boundary = {v for lp in loops for v in lp}
    laplacian(bm, list(bm.verts), iterations=6, factor=0.45, keep=boundary)

    # The front hangs straight from the chest: nothing below the chest's forward-most line goes back in.
    pec_z = neck.z - 0.17
    cols = {}
    for v in bm.verts:
        if abs(v.co.x) < 0.2 and pec_z - 0.02 < v.co.z < pec_z + 0.04 and v.co.y < neck.y:
            k = round(v.co.x / 0.012)
            cols[k] = min(cols.get(k, 9.0), v.co.y)
    for v in bm.verts:
        if v.co.z < pec_z and v.co.y < neck.y and not is_arm(dom_of(v)):
            k = round(v.co.x / 0.012)
            if k in cols:
                drop = (pec_z - v.co.z)
                v.co.y = min(v.co.y, cols[k] + 0.03 * drop)
    for v in boundary:
        if abs(v.co.z - z_c) < 0.03 and not is_arm(dom_of(v)):
            v.co.z = z_c

    # ---- the lofted robe below z_c, joined to the shell's bottom loop
    def waist_loop():
        lps = boundary_loops(bm)
        return min(lps, key=lambda lp: abs(sum(v.co.z for v in lp) / len(lp) - z_c) + abs(sum(v.co.x for v in lp) / len(lp)))

    low = waist_loop()
    # Densify the loop to about 1.2 cm spacing so the folds have vertices to live on.
    edges = []
    for i in range(len(low)):
        a, b = low[i], low[(i + 1) % len(low)]
        e = bm.edges.get((a, b))
        if e:
            n = max(0, int(math.ceil((a.co - b.co).length / 0.012)) - 1)
            if n:
                edges.append((e, n))
    for e, n in edges:
        bmesh.ops.subdivide_edges(bm, edges=[e], cuts=n)
    low = waist_loop()
    cx = sum(v.co.x for v in low) / len(low)
    cy = sum(v.co.y for v in low) / len(low)
    thetas = [math.atan2(v.co.y - cy, v.co.x - cx) for v in low]
    if sum((thetas[(i + 1) % len(low)] - thetas[i] + math.pi) % (2 * math.pi) - math.pi for i in range(len(low))) < 0:
        low.reverse()
        thetas.reverse()
    r_top = [math.hypot(v.co.x - cx, v.co.y - cy) for v in low]

    # Body slices (no arms) for the hull the robe must clear.
    bnames = deform_names(body)
    bbm = bmesh.new()
    bbm.from_mesh(body.data)
    bdl = bbm.verts.layers.deform.active
    slice_pts = [(v.co.x, v.co.y, v.co.z) for v in bbm.verts if not is_arm(dominant(v[bdl], bnames))]
    bbm.free()
    rings = 30
    zs = [z_c - (z_c - z_hem) * (k / rings) for k in range(rings + 1)]
    prev = r_top[:]
    ring_verts = [low]
    fold_ph = (random.uniform(0, 6.28), random.uniform(0, 6.28), random.uniform(0, 6.28))
    for k in range(1, rings + 1):
        z = zs[k]
        pts = [(x, y) for (x, y, zz) in slice_pts if abs(zz - z) < 0.016]
        hull = hull2d(pts) if len(pts) >= 3 else []
        clear = lerp(0.03, 0.05, smoothstep(z_hips, 0.45, z)) + 0.016 * smoothstep(0.2, z_hem, z)
        fold_a = 0.017 * smoothstep(z_hips, z_hem + 0.15, z)
        ring = []
        cur = []
        for i, th in enumerate(thetas):
            rh = ray_hull(hull, (cx, cy), th) if hull else 0.0
            r = max(rh + clear, prev[i] - 0.0045)
            back = 0.75 + 0.5 * max(0.0, math.sin(th))
            fold = 0.5 + 0.5 * (0.6 * math.sin(11 * th + fold_ph[0]) + 0.28 * math.sin(17 * th + fold_ph[1]) + 0.12 * math.sin(5 * th + fold_ph[2]))
            cur.append(r)
            rr = r + fold_a * back * fold
            ring.append(bm.verts.new((cx + rr * math.cos(th), cy + rr * math.sin(th), z)))
        prev = cur
        ring_verts.append(ring)
    n = len(low)
    for k in range(rings):
        a, b = ring_verts[k], ring_verts[k + 1]
        for i in range(n):
            j = (i + 1) % n
            try:
                bm.faces.new((a[i], a[j], b[j], b[i]))
            except ValueError:
                pass
    # Ease the seam between the shell and the loft.
    band = [v for v in bm.verts if abs(v.co.z - z_c) < 0.07 and math.hypot(v.co.x - cx, v.co.y - cy) < 0.4]
    laplacian(bm, band, iterations=3, factor=0.35, keep=set(ring_verts[-1]))

    # ---- openings: an inner facing at the hem, the cuffs and the neckline, so the cloth has thickness
    bm.normal_update()
    loops = boundary_loops(bm)
    for lp in loops:
        zc = sum(v.co.z for v in lp) / len(lp)
        c = sum((v.co for v in lp), Vector()) / len(lp)
        if zc < z_hem + 0.02:                        # hem: in and up
            mid = [bm.verts.new(Vector((v.co.x + (c.x - v.co.x) * 0.02, v.co.y + (c.y - v.co.y) * 0.02, v.co.z))) for v in lp]
            inner = [bm.verts.new(Vector((m.co.x + (c.x - m.co.x) * 0.01, m.co.y + (c.y - m.co.y) * 0.01, m.co.z + 0.04))) for m in mid]
            chain = [lp, mid, inner]
        elif zc > neck.z - 0.06:                     # neckline: a rolled edge, then a facing down inside
            mid, inner = [], []
            for v in lp:
                inward = Vector((c.x - v.co.x, c.y - v.co.y, 0.0)).normalized()
                v.co = v.co - inward * 0.0008
                m = bm.verts.new(v.co + inward * 0.0024 + Vector((0, 0, -0.0012)))
                mid.append(m)
                inner.append(bm.verts.new(m.co + Vector((0, 0, -0.011))))
            chain = [lp, mid, inner]
        else:                                        # a cuff: in toward the arm, back up the sleeve
            s = side_of(c)
            elbow, wrist, d = wf[s]
            mid = [bm.verts.new(v.co + (c - v.co).normalized() * 0.004) for v in lp]
            inner = [bm.verts.new(m.co - d * 0.025) for m in mid]
            chain = [lp, mid, inner]
        for a, b in zip(chain[:-1], chain[1:]):
            m = len(a)
            for i in range(m):
                j = (i + 1) % m
                try:
                    bm.faces.new((a[i], a[j], b[j], b[i]))
                except ValueError:
                    pass
    # Cuff bands: the last 3.5 cm of each sleeve stands a little proud.
    for v in bm.verts:
        s = side_of(v.co)
        elbow, wrist, d = wf[s]
        t = (v.co - (wrist - d * cuff_back)).dot(d)
        if -0.036 < t <= 1e-4:
            axis_pt = elbow + d * (v.co - elbow).dot(d)
            radial = v.co - axis_pt
            if radial.length > 0.02:
                v.co += radial.normalized() * 0.0016
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(top.data)
    bm.free()
    top.data.update()
    for g in list(top.vertex_groups):
        top.vertex_groups.remove(g)
    mats_for(top, ["TS_Kandura"])
    log("kandura verts", len(top.data.vertices), "faces", len(top.data.polygons))

    placket_and_furakha(top, neck)
    boots(body, rig)
    satchel(top, rig)


def placket_and_furakha(kand, neck):
    """The front placket (a raised strip down from the neckline) and the furakha cord and tassel."""
    tree = bvh_of(kand)

    def front_hit(x, z):
        hit = tree.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)), 2.0)
        return hit if hit[0] is not None else None

    # The neckline's front point at the centre.
    z_top = None
    for k in range(80):
        z = neck.z - 0.005 - k * 0.001
        h = front_hit(0.0, z)
        if h is not None:
            z_top = z
            break
    mb = L.MB()
    rows = []
    n = 26
    for i in range(n + 1):
        z = z_top - 0.004 - i * (0.24 / n)
        row = []
        for x in (-0.0088, -0.0072, 0.0072, 0.0088):
            h = front_hit(x, z)
            if h is None:
                continue
            raised = abs(x) < 0.008
            row.append(mb.v(h[0] + h[1] * (0.0019 if raised else 0.0005)))
        if len(row) == 4:
            rows.append(row)
    for a, b in zip(rows[:-1], rows[1:]):
        for k in range(3):
            mb.f((a[k], b[k], b[k + 1], a[k + 1]), 0)
    if rows:
        mb.f(tuple(rows[-1][::-1]), 0)
    for dz in (0.045, 0.095, 0.145):
        h = front_hit(0.0, z_top - dz)
        if h is None:
            continue
        L.lathe(mb, [(0.0, 0.0017), (0.0028, 0.0015), (0.0044, 0.0008), (0.0047, 0.0), (0.0, 0.0)], segs=10, mat=0,
                center=h[0] + h[1] * 0.0017, rot=h[1].to_track_quat("Z", "Y").to_matrix())
    placket = mb.build("Placket", [material("TS_Kandura")])
    L.shade(placket, 30.0)

    # Furakha: a thin cord from the top of the placket, down its centre to the upper belly; then the tassel.
    pts = []
    for i in range(34):
        z = z_top - 0.007 - i * 0.0088
        h = front_hit(0.0, z)
        if h is None:
            break
        pts.append(h[0] + h[1] * (0.0042 + 0.0006 * i / 33))
    mb = L.MB()
    prof = L.circle2d(0.0016, 8)
    L.sweep(mb, pts, prof, mat=0, caps=True)
    top_k = pts[0]
    L.lathe(mb, [(0.0001, 0.003), (0.0028, 0.0022), (0.0035, 0.0), (0.0028, -0.0022), (0.0001, -0.003)], segs=12,
            mat=0, center=(top_k.x, top_k.y - 0.001, top_k.z))
    end = pts[-1]
    # Tassel: a knot, then strands flaring out, hanging straight down a little off the chest.
    tprof = [(0.0001, 0.0), (0.0036, -0.002), (0.0044, -0.006), (0.003, -0.011), (0.0032, -0.016),
             (0.0052, -0.03), (0.0072, -0.05), (0.0078, -0.066), (0.0001, -0.0665)]
    L.lathe(mb, [(r, z) for r, z in tprof], segs=16, mat=0, center=(end.x, end.y - 0.003, end.z - 0.002))
    fur = mb.build("Furakha", [material("TS_Kandura")])
    # Strand ridges on the tassel's skirt.
    bmf = bmesh.new()
    bmf.from_mesh(fur.data)
    for v in bmf.verts:
        dz = (end.z - 0.002) - v.co.z
        if 0.017 < dz < 0.0665:
            rad = Vector((v.co.x - end.x, v.co.y - (end.y - 0.003), 0.0))
            if rad.length > 1e-5:
                a = math.atan2(rad.y, rad.x)
                v.co += rad.normalized() * (0.0007 * math.sin(16 * a))
    bmf.to_mesh(fur.data)
    bmf.free()
    L.shade(fur, 40.0)
    log("placket", len(placket.data.polygons), "furakha", len(fur.data.polygons))


def boots(body, rig):
    """Suede Chelsea boots: the feet offset outward, a flat sole with a welt, open at the ankle under the hem."""
    names = deform_names(body)
    b = duplicate(body, "Boots")
    b.modifiers.clear()
    b.parent = None
    bm = bmesh.new()
    bm.from_mesh(b.data)
    dl = bm.verts.layers.deform.active
    keep = set()
    for v in bm.verts:
        d = dominant(v[dl], names)
        if (d.startswith(("foot", "ball")) or d.startswith("calf")) and v.co.z < 0.135:
            keep.add(v)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep], context="VERTS")
    bm.normal_update()
    moves = {v: v.co + v.normal * 0.006 for v in bm.verts}
    for v, co in moves.items():
        v.co = co
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)   # closed, for the remesh
    bm.to_mesh(b.data)
    bm.free()
    b.vertex_groups.clear()
    select_only([b])
    rm = b.modifiers.new("Remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = 0.0045                      # bridges the gaps between the toes
    bpy.ops.object.modifier_apply(modifier="Remesh")
    bm = bmesh.new()
    bm.from_mesh(b.data)
    taubin(bm, list(bm.verts), iterations=6)
    bmesh.ops.delete(bm, geom=[fc for fc in bm.faces if all(v.co.z > 0.128 for v in fc.verts)], context="FACES")  # open at the ankle
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    for side in (1, -1):
        vs = [v for v in bm.verts if (v.co.x > 0) == (side > 0)]
        cxs = sum(v.co.x for v in vs) / max(1, len(vs))
        for v in vs:
            if v.co.z < 0.0:
                v.co.z = 0.0
            if v.co.z < 0.019:
                v.co.x += (v.co.x - cxs) * 0.075      # the crepe sole stands out a little
                v.co.y *= 1.0 + 0.016 * (1 if v.co.y < 0 else 0.6)
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(b.data)
    bm.free()
    for g in list(b.vertex_groups):
        b.vertex_groups.remove(g)
    mats_for(b, ["TS_Suede"])
    log("boots", len(b.data.polygons))


def frame_box(mb, centre, t, bn, n, size, mat):
    """A box oriented by the frame (t along, bn across, n out); size = (along, across, out)."""
    corners = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                corners.append(centre + t * (sx * size[0] / 2) + bn * (sy * size[1] / 2) + n * (sz * size[2] / 2))
    v = [mb.v(c) for c in corners]
    # indices: (sx, sy, sz) -> 4*sx' + 2*sy' + sz'
    idx = lambda a, b, c: v[(a > 0) * 4 + (b > 0) * 2 + (c > 0)]
    for f in (((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1)), ((-1, -1, 1), (-1, 1, 1), (1, 1, 1), (1, -1, 1)),
              ((-1, -1, -1), (-1, -1, 1), (1, -1, 1), (1, -1, -1)), ((-1, 1, -1), (1, 1, -1), (1, 1, 1), (-1, 1, 1)),
              ((-1, -1, -1), (-1, 1, -1), (-1, 1, 1), (-1, -1, 1)), ((1, -1, -1), (1, -1, 1), (1, 1, 1), (1, 1, -1))):
        mb.f(tuple(idx(*c) for c in f), mat, smooth=False)


def satchel(kand, rig):
    """A brown leather crossbody strap from the right shoulder (-X) round the torso to a small bag on the
    left hip (+X), with two metal slider buckles on the front run."""
    tree = bvh_of(kand)
    clav_r = bone_tail(rig, "clavicle_r")
    pelvis = bone_head(rig, "pelvis")
    neck = bone_head(rig, "neck_01")
    # Bag frame on the left hip, a little forward of the side.
    zb = pelvis.z - 0.085
    ang = math.radians(16.0)                                 # at the side, a little behind the hip (+Y)
    out = Vector((math.cos(ang), math.sin(ang), 0.0))
    hit = tree.ray_cast(Vector((0.0, pelvis.y, zb)) + out * 0.6, -out, 1.0)
    surf = hit[0] if hit[0] is not None else Vector((0.2, pelvis.y, zb))
    bag_n = out
    bag_w = Vector((0.0, 0.0, 1.0)).cross(bag_n).normalized()  # across the bag (front-back)
    W, H, Dp = 0.15, 0.165, 0.05
    bag_c = surf + bag_n * (Dp / 2 + 0.006)
    attach_f = bag_c + bag_w * (W / 2 - 0.012) + Vector((0, 0, H / 2 - 0.004))
    attach_b = bag_c - bag_w * (W / 2 - 0.012) + Vector((0, 0, H / 2 - 0.004))
    if attach_f.y > attach_b.y:
        attach_f, attach_b = attach_b, attach_f
    a = Vector((clav_r.x * 0.82, neck.y + 0.01, clav_r.z + 0.06))  # over the right shoulder
    pn = (attach_f - a).cross(attach_b - a).normalized()
    o = Vector((0.0, pelvis.y, (a.z + attach_f.z) / 2))
    o = o - pn * (o - a).dot(pn)
    u = (a - o).normalized()
    v_ = pn.cross(u).normalized()
    samples = []
    for i in range(240):
        phi = 2 * math.pi * i / 240
        d = u * math.cos(phi) + v_ * math.sin(phi)
        h = tree.ray_cast(o + d * 0.03, d, 0.6)
        if h[0] is None:
            continue
        nrm = h[1] if h[1].dot(d) > 0 else -h[1]
        samples.append((h[0], nrm, (h[0] - o).length))
    # Rays near the armpit can run into a sleeve: drop samples much farther out than their neighbours.
    good = []
    for i, (p, n, dist) in enumerate(samples):
        win = sorted(samples[(i + k) % len(samples)][2] for k in range(-6, 7))
        if dist <= win[len(win) // 2] * 1.18:
            good.append((p, n))
    samples = good
    # Cut out the run that passes under the bag and order the strap from the front attachment, over the
    # shoulder, to the back attachment.
    keep = [(p, n) for p, n in samples if (p - bag_c).length > 0.115]
    # find the gap
    best_gap, gi = 0, 0
    for i in range(len(keep)):
        g = (keep[i][0] - keep[(i + 1) % len(keep)][0]).length
        if g > best_gap:
            best_gap, gi = g, i
    run = keep[gi + 1:] + keep[:gi + 1]
    if (run[0][0] - attach_f).length > (run[0][0] - attach_b).length:
        run.reverse()
    pts = [attach_f] + [p for p, n in run] + [attach_b]
    nrms = [bag_n] + [n for p, n in run] + [bag_n]
    pts = [p + n * 0.0012 for p, n in zip(pts, nrms)]
    arc = [0.0]
    for i in range(1, len(pts)):
        arc.append(arc[-1] + (pts[i] - pts[i - 1]).length)
    shoulder_i = max(range(len(pts)), key=lambda k: pts[k].z)
    mb = L.MB()
    th = 0.003
    left, right, ltop, rtop = [], [], [], []
    for i, (p, n) in enumerate(zip(pts, nrms)):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        bn = n.cross(t).normalized()
        pad = 1.0 - smoothstep(0.085, 0.115, abs(arc[i] - arc[shoulder_i]))   # the shoulder pad
        w = lerp(0.02, 0.031, pad)
        tt = lerp(th, 0.0068, pad)
        left.append(mb.v(p - bn * w))
        right.append(mb.v(p + bn * w))
        ltop.append(mb.v(p - bn * w * 0.96 + n * tt))
        rtop.append(mb.v(p + bn * w * 0.96 + n * tt))
    for i in range(len(pts) - 1):
        j = i + 1
        mb.f((ltop[i], rtop[i], rtop[j], ltop[j]), 0)
        mb.f((left[i], left[j], right[j], right[i]), 0)
        mb.f((left[i], ltop[i], ltop[j], left[j]), 0)
        mb.f((right[i], right[j], rtop[j], rtop[i]), 0)
    strap = mb.build("Strap", [material("TS_Leather")])
    L.shade(strap, 35.0)
    # Two slider buckles on the chest run (a quarter and half way from the shoulder towards the bag).
    mb = L.MB()
    buckles = []
    for k in (int(lerp(shoulder_i, 0, 0.25)), int(lerp(shoulder_i, 0, 0.5))):
        k = max(1, min(len(pts) - 2, k))
        p, n = pts[k], nrms[k]
        t = (pts[k + 1] - pts[k - 1]).normalized()
        bn = n.cross(t).normalized()
        c = p + n * (th + 0.0016)
        for (dt, db, sa, sb) in ((0.0095, 0.0, 0.003, 0.047), (-0.0095, 0.0, 0.003, 0.047), (0.0, 0.0225, 0.022, 0.003),
                                 (0.0, -0.0225, 0.022, 0.003), (0.0, 0.0, 0.003, 0.041)):
            frame_box(mb, c + t * dt + bn * db, t, bn, n, (sa, sb, 0.0026), 0)
        buckles.append(k)
    metal = mb.build("Buckles", [material("TS_Leather")])
    L.shade(metal, 30.0)
    metal["metal"] = 1

    # The bag: a rounded box with a flap and a small clasp.
    mb = L.MB()
    up = Vector((0, 0, 1))
    prof = L.superellipse2d(W / 2, Dp / 2, 4.0, 48)
    basis = Matrix((bag_w, bag_n, up)).transposed()
    rows = []
    zs = [-H / 2, -H / 2 + 0.006, H / 2 - 0.006, H / 2]
    scale = [0.92, 1.0, 1.0, 0.94]
    for z, sc in zip(zs, scale):
        rows.append([mb.v(bag_c + basis @ Vector((x * sc, y * sc, z))) for x, y in prof])
    mb.bridge(rows, closed_ring=True, mat=0)
    mb.cap(rows[0], mat=0, flip=True)
    mb.cap(rows[-1], mat=0)
    # Flap over the top and down 70% of the outer face.
    fl = []
    for k in range(12):
        tt = k / 11
        if tt < 0.35:
            y = -Dp / 2 + (Dp + 0.007) * (tt / 0.35)
            z = H / 2 + 0.0035
        else:
            y = Dp / 2 + 0.0035
            z = H / 2 - (tt - 0.35) / 0.65 * (H * 0.7)
        fl.append((y, z))
    flap_rows = []
    for xx in [-W / 2 - 0.002 + i * (W + 0.004) / 10 for i in range(11)]:
        row = []
        for y, z in fl:
            curve = 0.004 * (1 - (2 * xx / W) ** 2) if z < H / 2 - H * 0.55 else 0.0
            row.append(mb.v(bag_c + basis @ Vector((xx, y, z - curve))))
        flap_rows.append(row)
    for a2, b2 in zip(flap_rows[:-1], flap_rows[1:]):
        for k in range(len(fl) - 1):
            mb.f((a2[k], a2[k + 1], b2[k + 1], b2[k]), 0)
    bag = mb.build("Bag", [material("TS_Leather")])
    L.shade(bag, 35.0)
    mb = L.MB()
    clasp_c = bag_c + basis @ Vector((0.0, Dp / 2 + 0.006, H / 2 - H * 0.62))
    frame_box(mb, clasp_c, bag_w, up, bag_n, (0.028, 0.012, 0.004), 0)
    for sgn in (1, -1):
        ring_c = bag_c + basis @ Vector((sgn * (W / 2 - 0.012), 0.0, H / 2 + 0.006))
        frame_box(mb, ring_c, bag_w, bag_n, up, (0.004, 0.02, 0.012), 0)
    bag_metal = mb.build("BagMetal", [material("TS_Leather")])
    bag_metal["metal"] = 1
    for o in (strap, bag):
        o["bag_c"] = list(bag_c)
    log("strap", len(strap.data.polygons), "bag", len(bag.data.polygons), "buckles at", buckles)



# ----------------------------------------------------------------------------------------------- stage 3
def head_features(body):
    """Landmarks on the face from MPFB's region groups and the stage 1 measurements."""
    names = deform_names(body)
    gi = {v: k for k, v in names.items()}
    me = body.data

    def members(group, thr=0.5):
        g = gi[group]
        return [v.co.copy() for v in me.vertices if any(x.group == g and x.weight > thr for x in v.groups)]

    lips = members("lips")
    ears = members("ears")
    scalp = members("scalp")
    f = {}
    f["eye_l"] = Vector(body["eye_l"])
    f["eye_r"] = Vector(body["eye_r"])
    f["eye_rad"] = body["eye_rad"]
    f["eye_z"] = (f["eye_l"].z + f["eye_r"].z) / 2
    f["eye_y"] = (f["eye_l"].y + f["eye_r"].y) / 2
    f["lips_c"] = sum(lips, Vector()) / len(lips)
    f["mouth_w"] = max(abs(p.x) for p in lips)
    f["lip_top"] = max(p.z for p in lips)
    f["lip_bot"] = min(p.z for p in lips)
    for side, sg in (("l", 1), ("r", -1)):
        e = [p for p in ears if p.x * sg > 0]
        f["ear_front_" + side] = min(p.y for p in e)
        f["ear_top_" + side] = max(p.z for p in e)
        f["ear_bot_" + side] = min(p.z for p in e)
        f["ear_x_" + side] = sum(abs(p.x) for p in e) / len(e)
    f["skull_top"] = max(p.z for p in scalp)
    # The chin: the lowest point of the face's front below the lower lip (the throat sits further back).
    front = [v.co for v in me.vertices if abs(v.co.x) < 0.01 and f["lip_bot"] - 0.08 < v.co.z < f["lip_bot"] and v.co.y < f["lips_c"].y + 0.012]
    f["chin_z"] = min(p.z for p in front)
    nose = [v.co for v in me.vertices if abs(v.co.x) < 0.008 and f["lip_top"] < v.co.z < f["eye_z"]]
    tip = min(nose, key=lambda p: p.y)
    f["nose_tip"] = tip.copy()
    log("features", {k: (round(v, 4) if isinstance(v, float) else [round(c, 4) for c in v]) for k, v in f.items()})
    return f


def region_shell(body, weight_of, name, mat, subdiv=0, soft_edge=False, rim=False, extra=None):
    """Copies the body faces in a region (weight_of(v) -> thickness, or None outside) and pushes each vertex out
    along its normal by its thickness. soft_edge: faces with any vertex inside are kept and the outside
    vertices sink just under the skin, so the edge follows the region smoothly instead of stepping along the
    faces. rim: a wall from the shell's open edge back down to the skin (for thick edges). extra(v, normal):
    an additional offset vector per vertex (after subdivision)."""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.normal_update()
    thick = {}
    for v in bm.verts:
        t = weight_of(v)
        if t is not None:
            thick[v] = t
    if soft_edge:
        kill = [f for f in bm.faces if not any(v in thick for v in f.verts)]
        for v in bm.verts:
            if v not in thick:
                thick[v] = -0.0008
    else:
        kill = [f for f in bm.faces if not all(v in thick for v in f.verts)]
    bmesh.ops.delete(bm, geom=kill, context="FACES_ONLY")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    if subdiv:
        bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=subdiv, use_grid_fill=True)
        # new verts: interpolate thickness from neighbours
        for _ in range(3):
            for v in bm.verts:
                if v not in thick:
                    nb = [thick[e.other_vert(v)] for e in v.link_edges if e.other_vert(v) in thick]
                    if nb:
                        thick[v] = sum(nb) / len(nb)
    bm.normal_update()
    normals = {v: v.normal.copy() for v in bm.verts}
    base = {v: v.co.copy() for v in bm.verts}
    for v in bm.verts:
        v.co = v.co + normals[v] * thick.get(v, 0.0005)
        if extra is not None:
            v.co = v.co + extra(base[v], normals[v])
    if rim:
        loops = boundary_loops(bm)
        for lp in loops:
            low = [bm.verts.new(base[v] - normals[v] * 0.0015) for v in lp]
            for i in range(len(lp)):
                j = (i + 1) % len(lp)
                try:
                    bm.faces.new((lp[i], lp[j], low[j], low[i]))
                except ValueError:
                    pass
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(material(mat))
    return o


def voronoi_points(bounds_min, bounds_max, cell):
    pts = []
    nx = int((bounds_max.x - bounds_min.x) / cell) + 2
    ny = int((bounds_max.y - bounds_min.y) / cell) + 2
    nz = int((bounds_max.z - bounds_min.z) / cell) + 2
    for i in range(nx):
        for j in range(ny):
            for k in range(nz):
                pts.append(bounds_min + Vector(((i + random.random()) * cell, (j + random.random()) * cell, (k + random.random()) * cell)))
    kd = KDTree(len(pts))
    for i, p in enumerate(pts):
        kd.insert(p, i)
    kd.balance()
    return kd


def head_bm(body, keep, cuts):
    """The body's faces near the head (keep(co) for every vert), subdivided `cuts` times per edge with the
    deform weights interpolated, normals updated."""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    kill = [fc for fc in bm.faces if not all(keep(v.co) for v in fc.verts)]
    bmesh.ops.delete(bm, geom=kill, context="FACES_ONLY")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=cuts, use_grid_fill=True)
    bm.normal_update()
    return bm


def bfs_dist(region):
    """Distance in from the region's edge, over edges."""
    edge_vs = [v for v in region if any(e.other_vert(v) not in region for e in v.link_edges)]
    dist = {v: 0.0 for v in edge_vs}
    frontier = list(edge_vs)
    while frontier:
        nxt = []
        for v in frontier:
            for e in v.link_edges:
                o = e.other_vert(v)
                if o in region:
                    d = dist[v] + e.calc_length()
                    if d < dist.get(o, 9.0):
                        dist[o] = d
                        nxt.append(o)
        frontier = nxt
    return dist


def shell_from_bm(bm, thick, name, mat, soft_edge=False, rim=False, extra=None):
    """A shell over the faces of bm in the region (thick: BMVert -> offset). soft_edge: faces with any vertex in
    the region are kept and the outside vertices sink just under the skin, so the edge is smooth; rim: a wall
    from the open edge down to the skin; extra(base, normal): an added offset. Frees bm."""
    if soft_edge:
        kill = [fc for fc in bm.faces if not any(v in thick for v in fc.verts)]
    else:
        kill = [fc for fc in bm.faces if not all(v in thick for v in fc.verts)]
    bmesh.ops.delete(bm, geom=kill, context="FACES_ONLY")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    normals = {v: v.normal.copy() for v in bm.verts}
    base = {v: v.co.copy() for v in bm.verts}
    for v in bm.verts:
        v.co = v.co + normals[v] * thick.get(v, -0.0008)
        if extra is not None:
            v.co = v.co + extra(base[v], normals[v])
    if rim:
        for lp in boundary_loops(bm):
            low = [bm.verts.new(base[v] - normals[v] * 0.0015) for v in lp]
            for i in range(len(lp)):
                j = (i + 1) % len(lp)
                try:
                    bm.faces.new((lp[i], lp[j], low[j], low[i]))
                except ValueError:
                    pass
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    dl = bm.verts.layers.deform.active
    if dl is not None:
        for v in bm.verts:
            v[dl].clear()
    for fc in bm.faces:
        fc.smooth = True
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(material(mat))
    return o


def stage_head():
    body = ob("Body")
    rig = ob("SaeedRig")
    f = head_features(body)
    names = deform_names(body)
    gi = {v: k for k, v in names.items()}
    g_scalp, g_lips, g_ears = gi["scalp"], gi["lips"], gi["ears"]
    head_c = bone_head(rig, "head")
    ez, ey = f["eye_z"], f["eye_y"]
    top = f["skull_top"]
    lc, mw = f["lips_c"], f["mouth_w"]
    lip_top, lip_bot, chin = f["lip_top"], f["lip_bot"], f["chin_z"]

    # ---- hair: MPFB's scalp region with the front hairline brought down to a normal forehead; short on the
    # sides, volume and curls on top, curls tumbling a little over the forehead, a closed front edge.
    def in_hair(v, dl):
        p = v.co
        if v[dl].get(g_ears, 0.0) > 0.2:
            return False
        if v[dl].get(g_scalp, 0.0) > 0.35:
            return True
        if p.y < head_c.y - 0.015 and abs(p.x) < 0.062:
            return p.z > ez + 0.064 + 0.012 * smoothstep(0.02, 0.055, abs(p.x))
        return False

    def hair_thickness(p, d):
        up = smoothstep(ez + 0.035, top - 0.03, p.z)                 # 0 at the temples, 1 on top
        back = smoothstep(head_c.y - 0.01, head_c.y + 0.08, p.y)    # 1 at the back of the head
        side = 0.008 + 0.006 * back
        t = lerp(side, 0.039, up)
        fade = smoothstep(0.0, 0.006 + 0.016 * up, d)
        return 0.0008 + t * fade

    hb = head_bm(body, lambda co: co.z > ez - 0.03 and abs(co.x) < 0.11, cuts=2)
    dl = hb.verts.layers.deform.active
    region = {v for v in hb.verts if in_hair(v, dl)}
    dist = bfs_dist(region)
    thick = {v: hair_thickness(v.co, dist.get(v, 0.03)) for v in region}
    front_y = ey + 0.025

    def overhang(p, n):
        fr = smoothstep(front_y + 0.03, front_y - 0.005, p.y) * smoothstep(ez + 0.03, ez + 0.06, p.z)
        return Vector((0.0, -0.013, -0.005)) * fr

    hair = shell_from_bm(hb, thick, "Hair", "TS_Hair", rim=True, extra=overhang)
    # Curls: rounded clumps of about 1.6 cm with creases between them, deeper where the hair is deep.
    cb = bmesh.new()
    cb.from_mesh(hair.data)
    cb.normal_update()
    mn = Vector((min(v.co.x for v in cb.verts), min(v.co.y for v in cb.verts), min(v.co.z for v in cb.verts))) - Vector((0.03, 0.03, 0.03))
    mx = Vector((max(v.co.x for v in cb.verts), max(v.co.y for v in cb.verts), max(v.co.z for v in cb.verts))) + Vector((0.03, 0.03, 0.03))
    kd = voronoi_points(mn, mx, 0.016)
    for v in cb.verts:
        depth = smoothstep(ez + 0.04, top - 0.015, v.co.z)
        near = kd.find_n(v.co, 2)
        f1, f2 = near[0][2], near[1][2]
        bump = 1.0 - smoothstep(0.0, 0.0125, f1)
        crease = 1.0 - smoothstep(0.0, 0.0022, f2 - f1)
        v.co = v.co + v.normal * ((0.009 * bump - 0.0026 * crease) * depth + 0.0015 * bump)
    cb.to_mesh(hair.data)
    cb.free()

    # ---- beard: a short full beard and moustache with a clean cheek line; the lips and the front of the
    # throat stay bare. Evaluated on a finely subdivided face so its edges are smooth.
    def beard(v, dl):
        """(membership 0..1, thickness or None)."""
        p = v.co
        n = v.normal
        ax = abs(p.x)
        sd = "l" if p.x > 0 else "r"
        if v[dl].get(g_lips, 0.0) > 0.15 or v[dl].get(g_ears, 0.0) > 0.2 or v[dl].get(g_scalp, 0.0) > 0.35:
            return 0.0, None
        s_ear = smoothstep(0.0, 0.008, (f["ear_front_" + sd] + 0.002) - p.y)
        cheek = lerp(lip_top + 0.016, ez - 0.017, smoothstep(mw + 0.002, mw + 0.045, ax))
        s_cheek = smoothstep(0.0, 0.007, cheek - p.z)
        neck_line = chin - 0.028 + 0.012 * smoothstep(0.02, 0.07, ax)
        if n.y < -0.3 and p.z < chin - 0.004:                      # the front of the throat stays bare
            neck_line = chin - 0.004
        s_neck = smoothstep(0.0, 0.01, p.z - neck_line)
        s_back = 1.0 - smoothstep(lc.y + 0.07, lc.y + 0.085, p.y) * (1.0 - smoothstep(chin - 0.01, chin + 0.02, p.z))
        e = (ax / (mw + 0.0035)) ** 2 + ((p.z - (lip_top + lip_bot) / 2) / ((lip_top - lip_bot) / 2 + 0.004)) ** 2
        s_lips = smoothstep(1.0, 1.4, e) if p.y < lc.y + 0.02 else 1.0
        mous = ax < mw + 0.006 and p.z > lip_top - 0.001 and p.y < lc.y + 0.02
        s_m = smoothstep(0.0, 0.003, (lip_top + 0.0125) - p.z) if mous else 1.0
        m = s_ear * s_cheek * s_neck * s_back * s_lips * s_m
        if m < 0.03:
            return m, None
        base = 0.0026 if mous else (0.0047 if p.z < lip_bot else 0.0033)
        if ax > mw + 0.03 and not mous:
            base = lerp(base, 0.0027, smoothstep(mw + 0.03, mw + 0.06, ax))
        return m, 0.0004 + base * m

    near_face = lambda co: chin - 0.07 < co.z < ez + 0.01 and co.y < head_c.y + 0.06
    bb_ = head_bm(body, near_face, cuts=2)
    dl = bb_.verts.layers.deform.active
    thick = {}
    for v in bb_.verts:
        m, t = beard(v, dl)
        if t is not None:
            thick[v] = t
    beard_o = shell_from_bm(bb_, thick, "Beard", "TS_Hair", soft_edge=True)

    # ---- brows: thick, arched, heavier at the inner end; strips laid on the brow ridge by raycasts
    tree = bvh_of(body)
    mb = L.MB()
    for sign in (1, -1):
        rows = []
        N = 16
        for i in range(N + 1):
            u = i / N
            x = sign * (0.0105 + 0.046 * u)
            h = lerp(0.0108, 0.0034, u ** 0.85)
            zc_ = ez + 0.0125 + 0.0058 * math.sin(math.pi * min(1.0, u * 1.05)) - 0.0035 * u + h / 2
            row = []
            for dz, off in ((-h / 2, 0.0003), (0.0, 0.0018), (h / 2, 0.0003)):
                hit = tree.ray_cast(Vector((x, -1.0, zc_ + dz)), Vector((0, 1, 0)), 2.0)
                if hit[0] is None:
                    row = None
                    break
                row.append(hit[0] + hit[1] * off)
            if row:
                rows.append([mb.v(c) for c in row])
        for a_, b_ in zip(rows[:-1], rows[1:]):
            for k in range(2):
                mb.f((a_[k], b_[k], b_[k + 1], a_[k + 1]), 0)
    brows = mb.build("Brows", [material("TS_Hair")])
    bb = bmesh.new()
    bb.from_mesh(brows.data)
    bb.normal_update()
    for fc in bb.faces:
        if fc.normal.y > 0:                                   # outward (toward the viewer), not into the head
            fc.normal_flip()
    bb.to_mesh(brows.data)
    bb.free()
    L.shade(brows, 60.0)

    # ---- eyes: spheres with the pole forward, UVs projected from the front (iris at the centre)
    mb = L.MB()
    r = f["eye_rad"] * 0.98
    for c in (f["eye_l"], f["eye_r"]):
        rings = []
        nlat, nlon = 12, 20
        pole = mb.v(c + Vector((0, -r - 0.0006, 0)))
        uv_of = lambda p_, c=c: (0.5 + 0.5 * (p_.x - c.x) / r, 0.5 + 0.5 * (p_.z - c.z) / r)
        for i in range(1, nlat):
            a = math.pi * i / nlat
            ring = []
            for j in range(nlon):
                b = 2 * math.pi * j / nlon
                d = Vector((math.sin(a) * math.cos(b), -math.cos(a), math.sin(a) * math.sin(b)))
                bulge = 0.0006 * (1 - smoothstep(0.0, 0.62, a))          # the cornea
                ring.append(mb.v(c + d * (r + bulge)))
            rings.append(ring)
        back = mb.v(c + Vector((0, r, 0)))
        for k in range(len(rings) - 1):
            a_, b_ = rings[k], rings[k + 1]
            for j in range(nlon):
                j1 = (j + 1) % nlon
                q = (a_[j], a_[j1], b_[j1], b_[j])
                mb.f(q, 0, [uv_of(mb.verts[x]) if mb.verts[x].y < c.y + 0.3 * r else (0.03, 0.5) for x in q])
        for j in range(nlon):
            j1 = (j + 1) % nlon
            mb.f((pole, rings[0][j1], rings[0][j]), 0, [uv_of(mb.verts[x]) for x in (pole, rings[0][j1], rings[0][j])])
            mb.f((back, rings[-1][j], rings[-1][j1]), 0, [(0.03, 0.5)] * 3)
    eyes = mb.build("Eyes", [material("TS_Eyes")])
    L.shade(eyes, 80.0)

    # ---- masks for the skin bake, on the body's own vertices: stubble (the beard membership, so it fades
    # past the beard's edge) and the scalp's shade under the hairline.
    mats_for(body, ["TS_Skin"])
    b0 = bmesh.new()
    b0.from_mesh(body.data)
    b0.normal_update()
    dl = b0.verts.layers.deform.active
    hair_r = {v for v in b0.verts if in_hair(v, dl)}
    hd = bfs_dist(hair_r)
    bmask = body.data.color_attributes.get("masks") or body.data.color_attributes.new("masks", "FLOAT_COLOR", "POINT")
    for v in b0.verts:
        m = beard(v, dl)[0] if near_face(v.co) else 0.0
        sc_ = smoothstep(0.0, 0.01, hd.get(v, 0.03)) if v in hair_r else 0.0
        bmask.data[v.index].color = (m, sc_, 0.0, 1.0)
    b0.free()
    for o in (hair, beard_o, brows, eyes):
        log(o.name, len(o.data.polygons), "faces")


# ----------------------------------------------------------------------------------------------- stage 4
RENAME = {"pelvis": "Hips", "spine_01": "Spine", "spine_02": "Chest", "spine_03": "UpperChest", "neck_01": "Neck",
          "head": "Head"}
for _s, _S in (("l", "L"), ("r", "R")):
    RENAME.update({"clavicle_" + _s: "Shoulder_" + _S, "upperarm_" + _s: "UpperArm_" + _S, "lowerarm_" + _s: "Forearm_" + _S,
                   "hand_" + _s: "Hand_" + _S, "thigh_" + _s: "UpperLeg_" + _S, "calf_" + _s: "LowerLeg_" + _S,
                   "foot_" + _s: "Foot_" + _S, "ball_" + _s: "Toes_" + _S})
    for _a, _b in (("thumb", "Thumb"), ("index", "Index"), ("middle", "Middle"), ("ring", "Ring"), ("pinky", "Little")):
        for _k in (1, 2, 3):
            RENAME["%s_0%d_%s" % (_a, _k, _s)] = "%s%d_%s" % (_b, _k, _S)

TEX_SIZE = {"TS_Skin": 2048, "TS_Kandura": 2048, "TS_Hair": 1024, "TS_Leather": 1024, "TS_Suede": 512}
ROUGH = {"TS_Skin": 0.55, "TS_Kandura": 0.85, "TS_Hair": 0.72, "TS_Leather": 0.6, "TS_Suede": 0.92, "TS_Eyes": 0.08}


def lin(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


def gauss(p, c, sigma):
    d = (p - c).length
    return math.exp(-d * d / (2 * sigma * sigma))


def select_only(objs, active=None):
    bpy.context.view_layer.update()
    for o in list(bpy.context.scene.objects):
        if o is not None:
            o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def transfer_weights(dst, src):
    select_only([dst])
    m = dst.modifiers.new("DT", "DATA_TRANSFER")
    m.object = src
    m.use_vert_data = True
    m.data_types_verts = {"VGROUP_WEIGHTS"}
    m.vert_mapping = "POLYINTERP_NEAREST"
    m.layers_vgroup_select_src = "ALL"
    m.layers_vgroup_select_dst = "NAME"
    bpy.ops.object.datalayout_transfer(modifier="DT")
    bpy.ops.object.modifier_apply(modifier="DT")


def set_rigid(o, weights):
    for g in list(o.vertex_groups):
        o.vertex_groups.remove(g)
    idx = list(range(len(o.data.vertices)))
    for bone, w in weights.items():
        o.vertex_groups.new(name=bone).add(idx, w, "REPLACE")


def keep_bone_groups(o):
    for g in list(o.vertex_groups):
        if g.name not in BONES:
            o.vertex_groups.remove(g)


def tidy_weights(o):
    select_only([o])
    bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.01)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)


def paint(o, fn):
    """Per-vertex base colour (linear RGB) into the 'albedo' attribute the bake shaders read."""
    me = o.data
    a = me.color_attributes.get("albedo") or me.color_attributes.new("albedo", "FLOAT_COLOR", "POINT")
    for v in me.vertices:
        c = fn(v)
        a.data[v.index].color = (c[0], c[1], c[2], 1.0)


def join(objs, active):
    select_only(objs, active)
    bpy.ops.object.join()
    return active


def smart_uv(o):
    me = o.data
    uv = me.uv_layers.get("UVMap")
    for u in list(me.uv_layers):
        if u.name != "UVMap":
            me.uv_layers.remove(u)
    if uv is None:
        uv = me.uv_layers.new(name="UVMap")
    me.uv_layers.active = me.uv_layers["UVMap"]
    select_only([o])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(62.0), island_margin=0.006, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def decimate(o, target_tris):
    t = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if t <= target_tris:
        return
    select_only([o])
    m = o.modifiers.new("Dec", "DECIMATE")
    m.ratio = target_tris / t
    m.use_collapse_triangulate = False
    bpy.ops.object.modifier_apply(modifier="Dec")


def tris_of(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


# ---- bake shaders ----------------------------------------------------------------------------------------
class NT:
    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.n = self.nt.nodes
        self.l = self.nt.links

    def node(self, kind, **inputs):
        nd = self.n.new(kind)
        for k, v in inputs.items():
            if isinstance(v, bpy.types.NodeSocket):
                self.l.new(v, nd.inputs[k])
            else:
                nd.inputs[k].default_value = v
        return nd

    def link(self, a, b):
        self.l.new(a, b)

    def vscale(self, vec, f):
        nd = self.n.new("ShaderNodeVectorMath")
        nd.operation = "SCALE"
        self.l.new(vec, nd.inputs[0])
        if isinstance(f, bpy.types.NodeSocket):
            self.l.new(f, nd.inputs["Scale"])
        else:
            nd.inputs["Scale"].default_value = f
        return nd.outputs["Vector"]

    def maprange(self, val, lo, hi, a=0.0, b=1.0):
        nd = self.n.new("ShaderNodeMapRange")
        self.l.new(val, nd.inputs["Value"])
        nd.inputs["From Min"].default_value = a
        nd.inputs["From Max"].default_value = b
        nd.inputs["To Min"].default_value = lo
        nd.inputs["To Max"].default_value = hi
        return nd.outputs["Result"]

    def coords(self, scale=(1, 1, 1)):
        tc = self.n.new("ShaderNodeTexCoord")
        if scale == (1, 1, 1):
            return tc.outputs["Object"]
        mp = self.n.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = scale
        self.l.new(tc.outputs["Object"], mp.inputs["Vector"])
        return mp.outputs["Vector"]

    def math(self, op, a, b=None):
        nd = self.n.new("ShaderNodeMath")
        nd.operation = op
        for i, x in enumerate((a, b)):
            if x is None:
                continue
            if isinstance(x, bpy.types.NodeSocket):
                self.l.new(x, nd.inputs[i])
            else:
                nd.inputs[i].default_value = x
        return nd.outputs[0]


def height_layer(t, spec):
    """A height field (float socket) from a spec: (kind, scale or (sx, sy, sz), extra)."""
    kind, scale, extra = spec
    sc = scale if isinstance(scale, tuple) else (scale, scale, scale)
    co = t.coords(sc)
    if kind == "noise":
        nd = t.node("ShaderNodeTexNoise", Vector=co, Scale=1.0, Detail=extra.get("detail", 3.0))
        return nd.outputs["Fac"]
    if kind == "voronoi":
        nd = t.n.new("ShaderNodeTexVoronoi")
        t.link(co, nd.inputs["Vector"])
        nd.inputs["Scale"].default_value = 1.0
        return nd.outputs["Distance"]
    if kind == "wave":
        nd = t.n.new("ShaderNodeTexWave")
        nd.wave_type = "BANDS"
        nd.bands_direction = extra.get("dir", "X")
        t.link(co, nd.inputs["Vector"])
        nd.inputs["Scale"].default_value = 1.0
        nd.inputs["Distortion"].default_value = extra.get("distortion", 0.0)
        nd.inputs["Detail"].default_value = extra.get("detail", 2.0)
        return nd.outputs["Fac"]
    if kind == "line":                                    # a groove at a height (extra: axis, at, width, mask)
        sep = t.n.new("ShaderNodeSeparateXYZ")
        t.link(t.coords(), sep.inputs["Vector"])
        d = t.math("ABSOLUTE", t.math("SUBTRACT", sep.outputs[extra.get("axis", "Z")], extra["at"]))
        g = t.maprange(d, 1.0, 0.0, 0.0, extra.get("width", 0.0012))
        if "above_y" in extra:                            # only behind (the back yoke)
            g = t.math("MULTIPLY", g, t.math("GREATER_THAN", sep.outputs["Y"], extra["above_y"]))
        return g
    raise ValueError(kind)


def bake_shader(mat, size, noise_scale, noise_amt, ao, bumps):
    t = NT(mat)
    out = t.node("ShaderNodeOutputMaterial")
    bsdf = t.node("ShaderNodeBsdfPrincipled")
    t.link(bsdf.outputs[0], out.inputs["Surface"])
    attr = t.n.new("ShaderNodeAttribute")
    attr.attribute_name = "albedo"
    nz = t.node("ShaderNodeTexNoise", Vector=t.coords(), Scale=noise_scale, Detail=4.0)
    col = t.vscale(attr.outputs["Color"], t.maprange(nz.outputs["Fac"], 1 - noise_amt, 1 + noise_amt, 0.3, 0.7))
    if mat.name == "TS_Hair":                              # strands: lighter streaks, curling
        wv = t.n.new("ShaderNodeTexWave")
        wv.wave_type = "BANDS"
        wv.bands_direction = "Z"
        t.link(t.coords((380.0, 380.0, 380.0)), wv.inputs["Vector"])
        wv.inputs["Scale"].default_value = 1.0
        wv.inputs["Distortion"].default_value = 20.0
        wv.inputs["Detail"].default_value = 3.0
        col = t.vscale(col, t.maprange(wv.outputs["Fac"], 0.85, 1.55, 0.0, 1.0))
    aon = t.n.new("ShaderNodeAmbientOcclusion")
    aon.samples = 16
    aon.inputs["Distance"].default_value = 0.03
    col = t.vscale(col, t.maprange(aon.outputs["AO"], 1 - ao, 1.0))
    t.link(col, bsdf.inputs["Base Color"])
    normal = None
    for spec, strength, dist in bumps:
        b = t.n.new("ShaderNodeBump")
        b.inputs["Strength"].default_value = strength
        b.inputs["Distance"].default_value = dist
        t.link(height_layer(t, spec), b.inputs["Height"])
        if normal is not None:
            t.link(normal, b.inputs["Normal"])
        normal = b.outputs["Normal"]
    if normal is not None:
        t.link(normal, bsdf.inputs["Normal"])
    imgs = {}
    for kind in ("albedo", "normal"):
        name = "saeed_%s%s" % (mat.name[3:].lower(), "" if kind == "albedo" else "_n")
        img = bpy.data.images.get(name) or bpy.data.images.new(name, size, size, alpha=False)
        img.colorspace_settings.name = "sRGB" if kind == "albedo" else "Non-Color"
        if kind == "normal":
            img.generated_color = (0.5, 0.5, 1.0, 1.0)
        nd = t.n.new("ShaderNodeTexImage")
        nd.image = img
        nd.name = "Bake_" + kind
        imgs[kind] = (img, nd)
    return imgs


def eye_texture(size=512):
    import numpy as np
    yy, xx = np.mgrid[0:size, 0:size]
    u = (xx + 0.5) / size - 0.5
    v = (yy + 0.5) / size - 0.5
    r = np.sqrt(u * u + v * v)
    a = np.arctan2(v, u)
    rng = np.random.default_rng(7)
    fib = 0.5 + 0.5 * np.sin(a * 37 + 2.0 * np.sin(a * 5)) * np.cos(a * 23 + r * 40)
    sclera = np.stack([np.full_like(r, c) for c in lin((0.92, 0.89, 0.85))], -1)
    edge = np.clip((r - 0.3) / 0.2, 0, 1)[..., None]
    sclera = sclera * (1 - edge) + np.array(lin((0.86, 0.74, 0.70))) * edge
    iris_c = np.array(lin((0.24, 0.13, 0.06)))
    iris_d = np.array(lin((0.12, 0.06, 0.03)))
    t = np.clip((r - 0.06) / 0.12, 0, 1)[..., None]
    iris = iris_d * (1 - t) * 0.6 + iris_c * (0.7 + 0.5 * fib[..., None]) * (0.4 + 0.6 * t)
    ring = np.clip(1 - np.abs(r - 0.175) / 0.012, 0, 1)[..., None]
    iris = iris * (1 - 0.7 * ring)
    img = sclera.copy()
    m_iris = (r < 0.182)[..., None]
    img = np.where(m_iris, iris, img)
    soft = np.clip((r - 0.172) / 0.012, 0, 1)[..., None]
    img = np.where((r < 0.19)[..., None], img * (1 - soft * (r >= 0.182)[..., None]) + sclera * soft * (r >= 0.182)[..., None], img)
    pupil = (r < 0.062)[..., None]
    img = np.where(pupil, np.array(lin((0.015, 0.012, 0.012))), img)
    rgba = np.concatenate([img, np.ones_like(r)[..., None]], -1).astype(np.float32)
    im = bpy.data.images.get("saeed_eyes") or bpy.data.images.new("saeed_eyes", size, size, alpha=False)
    im.colorspace_settings.name = "sRGB"
    im.pixels.foreach_set(rgba.ravel())
    im.update()
    return im


def final_material(mat, albedo, normal):
    t = NT(mat)
    out = t.node("ShaderNodeOutputMaterial")
    bsdf = t.node("ShaderNodeBsdfPrincipled")
    t.link(bsdf.outputs[0], out.inputs["Surface"])
    tex = t.n.new("ShaderNodeTexImage")
    tex.image = albedo
    t.link(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = ROUGH.get(mat.name, 0.5)
    if mat.name == "TS_Hair":
        try:
            bsdf.inputs["Specular IOR Level"].default_value = 0.25
        except KeyError:
            pass
    if normal is not None:
        nt_ = t.n.new("ShaderNodeTexImage")
        nt_.image = normal
        nm = t.n.new("ShaderNodeNormalMap")
        t.link(nt_.outputs["Color"], nm.inputs["Color"])
        t.link(nm.outputs["Normal"], bsdf.inputs["Normal"])


def setup_cycles():
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 16
    sc.render.bake.margin = 6
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = kind
                prefs.get_devices()
                gpus = [d for d in prefs.devices if d.type == kind]
                if gpus:
                    for d in prefs.devices:
                        d.use = d.type == kind
                    sc.cycles.device = "GPU"
                    log("cycles on", kind, [d.name for d in gpus])
                    return
            except Exception:
                continue
    except Exception as e:
        log("gpu setup failed", e)
    sc.cycles.device = "CPU"
    log("cycles on CPU")


def stage_finish():
    rig = ob("SaeedRig")
    body = ob("Body")
    f = head_features(body)
    lv = kandura_levels(rig)
    neck, z_hem = lv["neck"], lv["z_hem"]
    pelvis = bone_head(rig, "pelvis")
    wf = {s_: wrist_frame(rig, s_) for s_ in ("l", "r")}
    if "Tights" in bpy.data.objects:
        bpy.data.objects.remove(ob("Tights"), do_unlink=True)
    src = duplicate(body, "_BodySrc")                     # the full body: the weight source
    src.modifiers.clear()
    src.parent = None

    # ---- paint base colours (linear) ------------------------------------------------------------------
    gi = {g.index: g.name for g in body.vertex_groups}
    masks = body.data.color_attributes.get("masks")
    # The first-person hands' skin (HandMesh.SkinColor, sRGB 0.70 0.52 0.40), so the mirror and the hands agree.
    skin_base, skin_red, skin_lip = Vector(lin((0.70, 0.52, 0.40))), Vector(lin((0.72, 0.47, 0.38))), Vector(lin((0.60, 0.40, 0.35)))
    stub, scalp_c, nail_c = Vector((0.70, 0.69, 0.75)), Vector(lin((0.30, 0.21, 0.16))), Vector(lin((0.80, 0.63, 0.57)))
    cheeks = [Vector((sg * 0.045, f["eye_y"] + 0.006, f["eye_z"] - 0.033)) for sg in (1, -1)]
    eyes_c = [f["eye_l"], f["eye_r"]]

    def skin(v):
        w = {gi[g.group]: g.weight for g in v.groups}
        p = v.co
        c = skin_base.copy()
        red = max(gauss(p, ch, 0.022) for ch in cheeks) * 0.45 + gauss(p, f["nose_tip"], 0.014) * 0.35 + w.get("ears", 0.0) * 0.45
        c = c.lerp(skin_red, min(1.0, red))
        c = c.lerp(skin_lip, min(1.0, w.get("lips", 0.0) * 1.1))
        mk = masks.data[v.index].color if masks else (0, 0, 0, 1)
        c = Vector((c.x * lerp(1, stub.x, mk[0]), c.y * lerp(1, stub.y, mk[0]), c.z * lerp(1, stub.z, mk[0])))
        c = c.lerp(scalp_c, mk[1] * 0.75)
        c = c.lerp(nail_c, w.get("fingernails", 0.0) * 0.85)
        d = min((p - e).length for e in eyes_c)
        return c * (1 - 0.14 * (1 - smoothstep(0.019, 0.033, d)))

    paint(body, skin)
    kc = Vector(lin((0.929, 0.922, 0.894)))
    paint(ob("Kandura"), lambda v: kc * (0.965 if v.co.z < z_hem + 0.05 else 1.0))
    paint(ob("Placket"), lambda v: kc)
    paint(ob("Furakha"), lambda v: Vector(lin((0.95, 0.945, 0.925))))
    strap_c, bag_c_, metal_c = Vector(lin((0.205, 0.16, 0.105))), Vector(lin((0.18, 0.135, 0.09))), Vector(lin((0.27, 0.27, 0.28)))
    paint(ob("Strap"), lambda v: strap_c)
    paint(ob("Bag"), lambda v: bag_c_)
    paint(ob("Buckles"), lambda v: metal_c)
    paint(ob("BagMetal"), lambda v: metal_c)
    suede_c, sole_c = Vector(lin((0.33, 0.26, 0.19))), Vector(lin((0.60, 0.53, 0.43)))
    paint(ob("Boots"), lambda v: sole_c if v.co.z < 0.0185 else suede_c)
    hc = Vector(lin((0.045, 0.034, 0.027)))
    for nm in ("Hair", "Beard", "Brows"):
        paint(ob(nm), lambda v: hc)

    # ---- weights ---------------------------------------------------------------------------------------
    for nm in ("Kandura", "Placket", "Furakha", "Boots", "Strap", "Buckles", "Beard"):
        transfer_weights(ob(nm), src)
        keep_bone_groups(ob(nm))
    for nm in ("Hair", "Brows", "Eyes"):
        set_rigid(ob(nm), {"head": 1.0})
    for nm in ("Bag", "BagMetal"):
        set_rigid(ob(nm), {"pelvis": 0.75, "thigh_l": 0.25})
    # The skirt follows the legs only halfway; the rest stays with the hips (no tearing between the knees).
    k = ob("Kandura")
    vg = {g.name: g for g in k.vertex_groups}
    pg = vg.get("pelvis") or k.vertex_groups.new(name="pelvis")
    for v in k.data.vertices:
        f_ = 0.5 * smoothstep(pelvis.z - 0.05, pelvis.z - 0.25, v.co.z)
        if f_ <= 0:
            continue
        moved = 0.0
        for g in v.groups:
            name = k.vertex_groups[g.group].name
            if is_leg(name):
                d = g.weight * f_
                g.weight -= d
                moved += d
        if moved > 0:
            pg.add([v.index], moved, "ADD")
    for nm in ("Kandura", "Placket", "Furakha", "Boots", "Strap", "Buckles", "Beard"):
        tidy_weights(ob(nm))
    bpy.data.objects.remove(src, do_unlink=True)

    # ---- drop the body the kandura and the boots hide ----------------------------------------------------
    names = deform_names(body)
    bm = bmesh.new()
    bm.from_mesh(body.data)
    dl = bm.verts.layers.deform.active
    kill = []
    for v in bm.verts:
        b = dominant(v[dl], names)
        co = v.co
        if b in ("head", "neck_01") or (b in ("spine_03",) or b.startswith("clavicle")):
            if neckline_side(co, neck) > -0.025:
                continue
            kill.append(v)
            continue
        if is_arm(b):
            elbow, wrist, d = wf["l" if co.x > 0 else "r"]
            if (co - (wrist - d * 0.012)).dot(d) > -0.03:
                continue
        kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(body.data)
    bm.free()
    keep_bone_groups(body)
    tidy_weights(body)
    decimate(ob("Boots"), 1300)
    decimate(ob("Beard"), 4800)
    decimate(ob("Hair"), 4600)
    decimate(ob("Kandura"), 7000)
    # The body: the hands are dense (fingers); the face keeps most of its detail.
    select_only([body])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    me_b = body.data
    for v in me_b.vertices:
        v.select = v.co.z < 1.35                  # the hands (the neck ends far above)
    for e in me_b.edges:
        e.select = all(me_b.vertices[i].select for i in e.vertices)
    for pl in me_b.polygons:
        pl.select = all(me_b.vertices[i].select for i in pl.vertices)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    hands = [o for o in bpy.context.selected_objects if o is not body][0]
    hands.name = "Hands"
    decimate(hands, int(tris_of(hands) * 0.5))
    decimate(body, int(tris_of(body) * 0.84))
    join([body, hands], body)
    tidy_weights(body)

    # ---- one object per material (they share a texture), UVs ---------------------------------------------
    kand = join([ob("Kandura"), ob("Placket"), ob("Furakha")], ob("Kandura"))
    hair = join([ob("Hair"), ob("Beard"), ob("Brows")], ob("Hair"))
    leather = join([ob("Strap"), ob("Buckles"), ob("Bag"), ob("BagMetal")], ob("Strap"))
    leather.name = "Leather"
    boots_o = ob("Boots")
    eyes = ob("Eyes")
    for o in (kand, hair, leather, boots_o):
        smart_uv(o)
    counts = {o.name: tris_of(o) for o in (body, kand, hair, leather, boots_o, eyes)}
    log("tris", counts, "total", sum(counts.values()))

    # ---- bake ------------------------------------------------------------------------------------------
    setup_cycles()
    specs = {
        "TS_Skin": (90.0, 0.05, 0.15, [(("noise", 900.0, {"detail": 2.0}), 0.10, 0.0004), (("noise", 160.0, {}), 0.05, 0.0006)]),
        "TS_Kandura": (30.0, 0.025, 0.3, [(("noise", (55.0, 55.0, 7.0), {"detail": 3.0}), 0.22, 0.002),
                                           (("wave", 330.0, {"dir": "X"}), 0.035, 0.0003),
                                           (("wave", 330.0, {"dir": "Z"}), 0.035, 0.0003),
                                           (("line", 1.0, {"axis": "Z", "at": z_hem + 0.024, "width": 0.0011}), -0.5, 0.0008),
                                           (("line", 1.0, {"axis": "Z", "at": neck.z - 0.115, "width": 0.0012, "above_y": neck.y + 0.02}), -0.6, 0.0008)]),
        "TS_Hair": (260.0, 0.22, 0.55, [(("wave", 420.0, {"dir": "Z", "distortion": 18.0, "detail": 3.0}), 0.22, 0.0004),
                                         (("wave", 300.0, {"dir": "X", "distortion": 22.0, "detail": 3.0}), 0.16, 0.0004),
                                         (("noise", 260.0, {"detail": 2.0}), 0.08, 0.0004)]),
        "TS_Leather": (25.0, 0.08, 0.35, [(("noise", 700.0, {"detail": 3.0}), 0.25, 0.0004), (("noise", 45.0, {}), 0.12, 0.001)]),
        "TS_Suede": (60.0, 0.07, 0.3, [(("noise", 1100.0, {"detail": 2.0}), 0.18, 0.0003)]),
    }
    imgs = {}
    for name, (nsc, namt, ao, bumps) in specs.items():
        imgs[name] = bake_shader(material(name), TEX_SIZE[name], nsc, namt, ao, bumps)
    baked = [body, kand, hair, leather, boots_o]
    for kind, btype in (("albedo", "DIFFUSE"), ("normal", "NORMAL")):
        for name, im in imgs.items():
            material(name).node_tree.nodes.active = im[kind][1]
        select_only(baked, body)
        if btype == "DIFFUSE":
            bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=True, margin=6)
        else:
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", use_clear=True, margin=6)
        log("baked", kind)
    eye_img = eye_texture(512)
    for name, im in imgs.items():
        for kind in ("albedo", "normal"):
            img = im[kind][0]
            img.filepath_raw = os.path.join(ART_DIR, img.name + ".png")
            img.file_format = "PNG"
            img.save()
    eye_img.filepath_raw = os.path.join(ART_DIR, "saeed_eyes.png")
    eye_img.file_format = "PNG"
    eye_img.save()
    for name, im in imgs.items():
        final_material(material(name), im["albedo"][0], im["normal"][0])
    final_material(material("TS_Eyes"), eye_img, None)
    log("textures in", ART_DIR)

    # ---- one skinned mesh, Unity bone names ------------------------------------------------------------
    mesh = join([body, kand, hair, leather, boots_o, eyes], body)
    mesh.name = "SaeedMesh"
    mesh.data.name = "SaeedMesh"
    for a in list(mesh.data.color_attributes):
        mesh.data.color_attributes.remove(a)
    for b in rig.data.bones:
        if b.name in RENAME:
            b.name = RENAME[b.name]
    missing = [g.name for g in mesh.vertex_groups if g.name not in rig.data.bones]
    log("groups without a bone:", missing)
    rig.name = "Armature"
    rig.data.name = "Armature"
    slots = [m.name for m in mesh.data.materials]
    log("slots", slots, "tris", tris_of(mesh), "verts", len(mesh.data.vertices), "bones", len(rig.data.bones))
    save(4)

    # ---- export ---------------------------------------------------------------------------------------
    # Checked in Unity 6000.6: the prefab root is identity; the "Armature" node carries Blender's axis conversion
    # (270 degrees about X) with or without the experimental baked space transform, and the skinned result stands up,
    # faces +Z, feet at 0, 1.83 m to the top of the hair. So the plain export.
    path = os.path.join(L.MODELS_DIR, "Saeed.fbx")
    select_only([rig, mesh], rig)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
                             use_tspace=True, add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
                             use_armature_deform_only=False, armature_nodetype="NULL", bake_anim=False,
                             path_mode="STRIP", embed_textures=False)
    mn, mx = L.world_bbox([mesh])
    dims = [round(mx[k] - mn[k], 4) for k in range(3)]
    info = {"file": "Saeed.fbx", "bytes": os.path.getsize(path), "tris": tris_of(mesh), "verts": len(mesh.data.vertices),
            "dims_m": dims, "slots": sorted(slots), "bones": [b.name for b in rig.data.bones], "parts_tris": counts,
            "objects": [{"name": "Armature", "type": "ARMATURE", "parent": None},
                        {"name": "SaeedMesh", "type": "MESH", "parent": "Armature", "tris": tris_of(mesh), "slots": slots,
                         "world_dims": dims, "uv": True}],
            "textures": sorted(fn for fn in os.listdir(ART_DIR) if fn.endswith(".png"))}
    import json
    with open(os.path.join(L.LOG_DIR, "Saeed.build.json"), "w") as fh:
        json.dump(info, fh, indent=2)
    log("exported", path, info["bytes"], "bytes")
    renders(rig, mesh, f)


def renders(rig, mesh, f):
    """Previews: the standard three-quarter view, and front, side and face views."""
    try:
        L.preview(rig, "Saeed.png")
    except Exception as e:
        log("ts_lib preview failed:", e)
    sc = bpy.context.scene
    items = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in items else items[0]
    try:
        sc.view_settings.view_transform = "AgX"
    except Exception:
        pass
    world = sc.world or bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.42, 0.43, 0.45, 1)
    bg.inputs[1].default_value = 0.4
    for o in [o for o in sc.objects if o.type in ("LIGHT", "CAMERA")]:
        bpy.data.objects.remove(o, do_unlink=True)

    def light(name, loc, energy, size=2.0):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy = energy
        ld.size = size
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 1.0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()

    light("Key", (-1.8, -2.6, 2.6), 430)
    light("Fill", (2.4, -1.8, 1.5), 140)
    light("Rim", (0.5, 2.8, 2.8), 300)
    cam_d = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_d)
    sc.collection.objects.link(cam)
    sc.camera = cam
    out_dir = L.PREVIEW_DIR

    def shot(name, direction, target, ortho=None, lens=None, dist=None, res=(800, 1000)):
        sc.render.resolution_x, sc.render.resolution_y = res
        d = Vector(direction).normalized()
        if ortho:
            cam_d.type = "ORTHO"
            cam_d.ortho_scale = ortho
            cam.location = Vector(target) + d * 6.0
        else:
            cam_d.type = "PERSP"
            cam_d.lens = lens
            cam.location = Vector(target) + d * dist
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        log("rendered", sc.render.filepath)

    shot("Saeed_front.png", (0, -1, 0.03), (0, 0, 0.9), ortho=1.95)
    shot("Saeed_side.png", (1, 0, 0.03), (0, 0, 0.9), ortho=1.95)
    shot("Saeed_back.png", (0, 1, 0.03), (0, 0, 0.9), ortho=1.95)
    shot("Saeed_face.png", (0.32, -1, 0.04), (0, f["eye_y"], f["eye_z"] - 0.03), lens=85, dist=0.75, res=(800, 800))


# ----------------------------------------------------------------------------------------------- main
def main():
    setup()
    if FIRST > 1:
        load(FIRST - 1)
    if "SaeedRig" in bpy.data.objects:
        BONES.update(bpy.data.objects["SaeedRig"].data.bones.keys())
    stages = {1: stage_body, 2: stage_kandura, 3: stage_head, 4: stage_finish}
    for st in range(FIRST, LAST + 1):
        if st not in stages:
            log("stage", st, "not written yet")
            break
        log("stage", st)
        random.seed(12 + st)
        stages[st]()
        save(st)


main()
