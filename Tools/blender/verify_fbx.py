"""
Re-import each exported FBX into an empty scene and log what Blender sees.
usage: blender -b --factory-startup --python verify_fbx.py -- [Name.fbx ...]   (default: all)
Writes Art/Models/_logs/<Name>.verify.json
"""
import sys
import os
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
import ts_lib as L  # noqa: E402

try:
    import io_scene_fbx.parse_fbx as parse_fbx
except Exception:  # pragma: no cover
    parse_fbx = None


def fbx_globals(path):
    out = {}
    if parse_fbx is None:
        return out
    root, ver = parse_fbx.parse(path)
    out["fbx_version"] = ver
    for top in root.elems:
        if top.id == b"GlobalSettings":
            for p in top.elems:
                if p.id == b"Properties70":
                    for pp in p.elems:
                        key = pp.props[0].decode() if pp.props else ""
                        if key in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign",
                                   "CoordAxis", "CoordAxisSign", "UnitScaleFactor"):
                            out[key] = pp.props[-1]
    return out


def signed_volume(ob):
    me = ob.data
    me.calc_loop_triangles()
    vol = 0.0
    co = [v.co for v in me.vertices]
    for t in me.loop_triangles:
        a, b, c = (co[i] for i in t.vertices)
        vol += a.dot(b.cross(c)) / 6.0
    return vol


def mesh_checks(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    loose_v = sum(1 for v in bm.verts if not v.link_faces)
    loose_e = sum(1 for e in bm.edges if not e.link_faces)
    boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    nonman = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    zero = sum(1 for f in bm.faces if f.calc_area() < 1e-14)
    bm.free()
    return {"loose_verts": loose_v, "loose_edges": loose_e, "boundary_edges": boundary,
            "nonmanifold_edges": nonman, "degenerate_faces": zero}


def verify(fn):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(L.MODELS_DIR, fn)
    bpy.ops.import_scene.fbx(filepath=path)
    bpy.context.view_layer.update()
    objs = list(bpy.context.scene.objects)
    mn, mx = L.world_bbox(objs)
    rep = {"file": fn, "bytes": os.path.getsize(path), "objects": [], "globals": fbx_globals(path)}
    total = 0
    slots = set()
    for o in sorted(objs, key=lambda x: (x.parent is not None, x.name)):
        d = {"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None}
        mw = o.matrix_world
        d["world_loc"] = [round(c, 5) for c in mw.translation]
        if o.type == "MESH":
            t = L.tris(o)
            total += t
            d["tris"] = t
            d["slots"] = [s.material.name if s.material else None for s in o.material_slots]
            slots.update(n for n in d["slots"] if n)
            d["uv_layers"] = [u.name for u in o.data.uv_layers]
            omn, omx = L.world_bbox([o])
            d["world_dims"] = [round(omx[k] - omn[k], 5) for k in range(3)]
            d.update(mesh_checks(o))
            d["signed_volume"] = signed_volume(o)
            d["custom_normals"] = bool(getattr(o.data, "has_custom_normals", False))
        rep["objects"].append(d)
    rep["tris"] = total
    rep["dims_m"] = [round(mx[k] - mn[k], 4) for k in range(3)]
    rep["bbox_min"] = [round(c, 4) for c in mn]
    rep["bbox_max"] = [round(c, 4) for c in mx]
    rep["slots"] = sorted(slots)
    with open(os.path.join(L.LOG_DIR, os.path.splitext(fn)[0] + ".verify.json"), "w") as fh:
        json.dump(rep, fh, indent=2)
    print("VERIFIED", fn, rep["tris"], rep["dims_m"], rep["slots"])
    return rep


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    files = argv or sorted(f for f in os.listdir(L.MODELS_DIR) if f.lower().endswith(".fbx"))
    for f in files:
        verify(f)
