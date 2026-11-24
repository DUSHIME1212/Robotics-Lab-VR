import sys, bpy
from mathutils import Vector
a=sys.argv[sys.argv.index("--")+1:]; src,out=a
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
ms=[o for o in bpy.data.objects if o.type=="MESH"]
pts=[o.matrix_world@Vector(c) for o in ms for c in o.bound_box]
lo=Vector([min(p[i] for p in pts) for i in range(3)]); hi=Vector([max(p[i] for p in pts) for i in range(3)])
print("[fbx] objects",len(bpy.data.objects),"meshes",len(ms),"arm",[o.name for o in bpy.data.objects if o.type=="ARMATURE"],"size",hi-lo,"tris",sum(len(o.data.polygons) for o in ms),"mats",len(bpy.data.materials),"imgs",len(bpy.data.images),"anims",[x.name for x in bpy.data.actions])
sc=bpy.context.scene
sc.render.engine="BLENDER_WORKBENCH"; sc.display.shading.color_type="TEXTURE"; sc.render.resolution_x=sc.render.resolution_y=512
w=bpy.data.worlds.new("w"); sc.world=w
c=(lo+hi)/2; r=(hi-lo).length
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera=cam
cam.location=c+Vector((1,-1.4,0.8))*r*0.8; cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler()
sc.render.filepath=out; bpy.ops.render.render(write_still=True)
