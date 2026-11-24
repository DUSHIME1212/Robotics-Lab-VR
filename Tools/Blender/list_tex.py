import sys,bpy,os
src=sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=src)
for i in bpy.data.images: print("[img]", i.name, i.filepath, os.path.exists(bpy.path.abspath(i.filepath)), i.packed_file is not None, i.size[:])
print("[names]", sorted({o.name.split('.')[0][:20] for o in bpy.data.objects})[:80])
