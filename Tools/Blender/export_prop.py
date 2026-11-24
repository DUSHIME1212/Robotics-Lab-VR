"""Re-export a small prop FBX with Unity axes and metric scale.
blender -b --factory-startup --python export_prop.py -- <in.fbx> <out.fbx>"""
import bpy, sys, os
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.abspath(a[0]))
for o in bpy.data.objects:
    if o.type != "MESH": continue
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.abspath(a[1]), use_selection=True, object_types={"MESH", "EMPTY"},
                         apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", path_mode="STRIP")
print("[prop] exported", a[1])
