"""Delete small faces inside an axis-aligned box (e.g. third-party logos) from a .blend and re-export FBX.
blender -b file.blend --python remove_region.py -- out.fbx xmin xmax ymin ymax zmin zmax max_face_area"""
import sys, bpy, bmesh
from mathutils import Vector
a=sys.argv[sys.argv.index("--")+1:]; out=a[0]; x0,x1,y0,y1,z0,z1,amax=map(float,a[1:8])
removed=0
for o in [o for o in bpy.data.objects if o.type=="MESH"]:
    bm=bmesh.new(); bm.from_mesh(o.data)
    kill=[f for f in bm.faces if f.calc_area()<amax and x0<=(c:=o.matrix_world@f.calc_center_median()).x<=x1 and y0<=c.y<=y1 and z0<=c.z<=z1]
    removed+=len(kill); bmesh.ops.delete(bm, geom=kill, context="FACES"); bm.to_mesh(o.data); bm.free()
print("[region] removed faces:", removed)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="STRIP")
bpy.ops.wm.save_mainfile()
