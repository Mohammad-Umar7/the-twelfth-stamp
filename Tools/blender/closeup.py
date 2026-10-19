"""
QA helper: render a close-up of a saved asset .blend.
usage: blender -b --factory-startup <file.blend> --python closeup.py -- <out.png> <tx> <ty> <tz> <dx> <dy> <dz> <dist> [lens]
(target point and view direction in Blender world space, metres)
"""
import sys
import os
import math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import ts_lib as L  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
out = argv[0]
t = Vector(tuple(float(a) for a in argv[1:4]))
d = Vector(tuple(float(a) for a in argv[4:7])).normalized()
dist = float(argv[7])
lens = float(argv[8]) if len(argv) > 8 else 50.0
sc = bpy.context.scene
sc.render.engine = L._engine()
sc.render.resolution_x, sc.render.resolution_y = 800, 600
sc.render.film_transparent = False
w = bpy.data.worlds.new("W")
w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.42, 0.42, 0.42, 1)
bg.inputs["Strength"].default_value = 0.8
sc.world = w
cd = bpy.data.cameras.new("C")
cd.lens = lens
cd.clip_start = dist * 0.01
cd.clip_end = dist * 100
cam = bpy.data.objects.new("C", cd)
sc.collection.objects.link(cam)
cam.location = t + d * dist
cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.camera = cam
for name, dirv, p in (("K", (-1, -0.8, 1.2), 1.0), ("F", (1, -0.6, 0.3), 0.35), ("R", (0.3, 1.2, 0.8), 0.7)):
    ld = bpy.data.lights.new(name, "AREA")
    ld.energy = 60 * p * (dist ** 2) * 6
    ld.size = dist * 1.5
    lo = bpy.data.objects.new(name, ld)
    lo.location = t + Vector(dirv).normalized() * dist * 2.5
    lo.rotation_euler = (t - lo.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(lo)
for o in sc.objects:
    if o.name.startswith("Preview"):
        o.hide_render = True
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
