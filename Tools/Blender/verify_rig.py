import sys, bpy, math
from mathutils import Vector
out = sys.argv[sys.argv.index("--")+1]; ang = float(sys.argv[-1])
arm = next(o for o in bpy.data.objects if o.type=="ARMATURE")
for pb in arm.pose.bones:
    if pb.name!="root": pb.rotation_mode="XYZ"; pb.rotation_euler=(0, math.radians(ang), 0)
sc=bpy.context.scene
try: sc.render.engine="BLENDER_WORKBENCH"
except TypeError as e: print(e)
sc.display.shading.color_type="RANDOM"
sc.render.resolution_x=sc.render.resolution_y=512
bpy.context.view_layer.update()
ms=[o for o in bpy.data.objects if o.type=="MESH"]
pts=[o.matrix_world@Vector(c) for o in ms for c in o.bound_box]
lo=Vector([min(p[i] for p in pts) for i in range(3)]); hi=Vector([max(p[i] for p in pts) for i in range(3)])
c=(lo+hi)/2; r=(hi-lo).length
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.location=c+Vector((1.2,-1.6,0.8))*r*0.9
cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler()
sc.render.filepath=out; bpy.ops.render.render(write_still=True)
