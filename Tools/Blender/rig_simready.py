"""Rig a SimReady (USD + UsdPhysics) robot in Blender and export FBX + joint JSON for Unity.

Usage:
  blender -b --factory-startup --python rig_simready.py -- <robot.usd> <out_dir> <RobotName>

How it works:
  1. Reads every UsdPhysics revolute/prismatic joint (pivot, axis, limits, parent/child link).
  2. Imports the USD render geometry with Blender's importer.
  3. Builds an armature: one bone per joint, head at the joint pivot, bone direction = joint axis.
     Rotating a bone around its own Y axis is therefore exactly the physical joint motion.
  4. Rigid-binds each link's meshes to its joint bone (bone parenting, no weight painting needed
     for rigid industrial links).
  5. Exports <RobotName>.fbx (Unity axes) and <RobotName>.joints.json (limits, order, types).
"""
import sys, os, json, math
import bpy
from mathutils import Matrix, Vector
from pxr import Usd, UsdGeom, UsdPhysics, Gf

argv = sys.argv[sys.argv.index("--") + 1:]
usd_path, out_dir, robot_name = os.path.abspath(argv[0]), os.path.abspath(argv[1]), argv[2]
os.makedirs(out_dir, exist_ok=True)

# ---------------------------------------------------------------- 1. read joints
stage = Usd.Stage.Open(usd_path)
mpu = UsdGeom.GetStageMetersPerUnit(stage) or 1.0
cache = UsdGeom.XformCache()
AXIS = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}


def gf_to_matrix(m):
    return Matrix([[m[c][r] for c in range(4)] for r in range(4)])


joints = []
for prim in stage.Traverse():
    is_rev = prim.IsA(UsdPhysics.RevoluteJoint)
    if not (is_rev or prim.IsA(UsdPhysics.PrismaticJoint)):
        continue
    j = UsdPhysics.Joint(prim)
    b0 = j.GetBody0Rel().GetTargets()
    b1 = j.GetBody1Rel().GetTargets()
    if not b1:
        continue
    body0 = stage.GetPrimAtPath(b0[0]) if b0 else None
    w0 = gf_to_matrix(cache.GetLocalToWorldTransform(body0)) if body0 else Matrix.Identity(4)
    lp = Vector(j.GetLocalPos0Attr().Get() or (0, 0, 0))
    lr = j.GetLocalRot0Attr().Get() or Gf.Quatf(1, 0, 0, 0)
    q = Matrix(gf_to_matrix(Gf.Matrix4d().SetRotate(Gf.Rotation(Gf.Quatd(lr)))).to_3x3())
    frame = w0 @ (Matrix.Translation(lp) @ q.to_4x4())
    axis_name = prim.GetAttribute("physics:axis").Get() or "Z"
    axis_world = (frame.to_3x3() @ AXIS[axis_name]).normalized()
    pivot = frame.to_translation() * mpu
    lo = prim.GetAttribute("physics:lowerLimit").Get()
    hi = prim.GetAttribute("physics:upperLimit").Get()
    joints.append({
        "name": prim.GetName(),
        "type": "revolute" if is_rev else "prismatic",
        "parentLink": str(b0[0]).split("/")[-1] if b0 else None,
        "childLink": str(b1[0]).split("/")[-1],
        "pivot": pivot, "axis": axis_world,
        "lower": float(lo) if lo is not None else -180.0,
        "upper": float(hi) if hi is not None else 180.0,
    })
print(f"[rig] {len(joints)} joints")

# ---------------------------------------------------------------- 2. import geometry
bpy.ops.wm.read_factory_settings(use_empty=True)
kw = dict(filepath=usd_path, import_cameras=False, import_lights=False,
          import_guide=False, import_proxy=False, import_render=True, scale=mpu)
props = bpy.ops.wm.usd_import.get_rna_type().properties.keys()
for k in ("support_scene_instancing", "use_instancing"):
    if k in props:
        kw[k] = False
bpy.ops.wm.usd_import(**{k: v for k, v in kw.items() if k in props or k == "filepath"})
bpy.ops.object.select_all(action="DESELECT")

link_names = {j["childLink"] for j in joints} | {j["parentLink"] for j in joints if j["parentLink"]}
root_links = {j["parentLink"] for j in joints} - {j["childLink"] for j in joints}


def base(name):
    return name.split(".")[0]


def owning_link(obj):
    p = obj
    while p:
        if base(p.name) in link_names:
            return base(p.name)
        p = p.parent
    return None


# Realize any instanced collections so every link has real meshes.
for o in list(bpy.data.objects):
    if o.instance_type == "COLLECTION" and o.instance_collection:
        o.select_set(True)
if bpy.context.selected_objects:
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.duplicates_make_real(use_base_parent=True, use_hierarchy=True)
    bpy.ops.object.select_all(action="DESELECT")

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
# Drop collision helpers that slipped through.
for o in list(meshes):
    n = o.name.lower()
    if "collision" in n or "collider" in n or o.display_type in {"BOUNDS", "WIRE"}:
        bpy.data.objects.remove(o)
        meshes.remove(o)
link_of = {o.name: owning_link(o) for o in meshes}

# ---------------------------------------------------------------- 3. armature
arm_data = bpy.data.armatures.new(robot_name + "_Rig")
arm = bpy.data.objects.new(robot_name + "_Rig", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones
root = eb.new("root")
root.head, root.tail = Vector((0, 0, 0)), Vector((0, 0, 0.1))
bone_for_link = {l: "root" for l in root_links}
size = max(0.03, 0.08 * max((j["pivot"].length for j in joints), default=1))
for j in joints:  # joints are in parent-before-child order in all SimReady files we use
    b = eb.new(j["name"])
    b.head = j["pivot"]
    b.tail = j["pivot"] + j["axis"] * size
    b.parent = eb[bone_for_link.get(j["parentLink"], "root")]
    b.use_connect = False
    bone_for_link[j["childLink"]] = j["name"]
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------- 4. rigid bind
for o in meshes:
    bone = bone_for_link.get(link_of[o.name], "root")
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
    # Apply transforms so mesh data is in world space, then bind with a single vertex group.
    o.data = o.data.copy()
    o.data.transform(mw)
    o.matrix_world = Matrix.Identity(4)
    o.vertex_groups.clear()
    vg = o.vertex_groups.new(name=bone)
    vg.add(range(len(o.data.vertices)), 1.0, "REPLACE")
    o.parent = arm
    mod = o.modifiers.new("Armature", "ARMATURE")
    mod.object = arm

# Merge meshes per bone to keep draw calls low for Quest.
by_bone = {}
for o in meshes:
    by_bone.setdefault(o.vertex_groups[0].name, []).append(o)
for bone, objs in by_bone.items():
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    bpy.context.view_layer.objects.active.name = f"{bone}_mesh"

for o in list(bpy.data.objects):
    if o.type == "EMPTY":
        bpy.data.objects.remove(o)

# ---------------------------------------------------------------- 5. export
fbx = os.path.join(out_dir, robot_name + ".fbx")
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"ARMATURE", "MESH"},
                         add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
                         apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                         bake_anim=False, mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True)
blend_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "SourceAssets", "Blend")
os.makedirs(blend_dir, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, robot_name + ".blend"))

meta = {"robot": robot_name, "source": os.path.basename(usd_path), "joints": [
    {"name": j["name"], "type": j["type"], "lower": j["lower"], "upper": j["upper"],
     "parent": bone_for_link.get(j["parentLink"], "root"), "childLink": j["childLink"]} for j in joints]}
with open(os.path.join(out_dir, robot_name + ".joints.json"), "w") as f:
    json.dump(meta, f, indent=2)
print(f"[rig] exported {fbx}: {len(by_bone)} mesh groups, {len(joints)} bones")
