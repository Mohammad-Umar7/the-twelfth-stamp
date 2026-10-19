"""
QA helper: put a labelled UV test image on given materials of a saved asset and render.
usage: blender -b --factory-startup <asset.blend> --python uvcheck.py -- <test.png> <out.png> <vx> <vy> <vz> <mat1,mat2,...>
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import ts_lib as L
argv = sys.argv[sys.argv.index("--") + 1:]
img = bpy.data.images.load(argv[0])
out = argv[1]
view = tuple(float(a) for a in argv[2:5])
mats = argv[5].split(",")
for name in mats:
    m = bpy.data.materials.get(name)
    if not m:
        continue
    nt = m.node_tree
    p = L._principled(m)
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = img
    nt.links.new(t.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 0.0
    try:
        p.inputs["Alpha"].default_value = 1.0
    except Exception:
        pass
root = next(o for o in bpy.context.scene.objects if o.parent is None and o.type == "EMPTY")
L.preview(root, os.path.basename(out), view=view, lens=60, floor=False)
import shutil
shutil.move(os.path.join(L.PREVIEW_DIR, os.path.basename(out)), out)
