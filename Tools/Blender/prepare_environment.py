"""Clean up a downloaded environment FBX for Quest VR and export it for Unity.

Usage:
  blender -b --factory-startup --python prepare_environment.py -- <in.fbx> <out.fbx> <max_tris> [remove_name_prefixes,...]

- Removes image texture nodes whose files are missing (they render magenta in Unity) and gives
  each material a sensible solid PBR color based on its name.
- Deletes objects whose names start with any of the given prefixes (e.g. placeholder people).
- Joins meshes per material (fewer draw calls) and decimates to the triangle budget.
- Applies transforms and puts the model's floor at Z=0, centred on the origin.
"""
import sys, os, bpy, bmesh
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, dst, max_tris = os.path.abspath(a[0]), os.path.abspath(a[1]), int(a[2])
remove = [p.lower() for p in (a[3].split(",") if len(a) > 3 and a[3] else [])]
strip_all = len(a) > 4 and a[4] == "notex"  # drop every texture (e.g. third-party logos baked into images)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

for o in list(bpy.data.objects):
    if any(o.name.lower().startswith(p) for p in remove):
        bpy.data.objects.remove(o, do_unlink=True)

PALETTE = [  # name keyword -> base color, metallic, roughness
    (("glass", "vidrio", "translucent", "window"), (0.75, 0.85, 0.9), 0.0, 0.05),
    (("wood", "madera", "oak", "bamboo", "table"), (0.55, 0.38, 0.24), 0.0, 0.6),
    (("metal", "steel", "alum", "chrome", "corrog", "panel", "acero"), (0.72, 0.73, 0.75), 0.9, 0.35),
    (("floor", "piso", "ground", "tiling"), (0.42, 0.44, 0.47), 0.0, 0.7),
    (("plant", "leaf", "orchid", "fig"), (0.2, 0.45, 0.18), 0.0, 0.7),
    (("sofa", "couch", "chair", "fabric"), (0.78, 0.78, 0.76), 0.0, 0.9),
    (("black", "negro", "screen", "tv"), (0.04, 0.04, 0.05), 0.2, 0.3),
]

for m in bpy.data.materials:
    if not m.use_nodes:
        continue
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    missing = False
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE" and (strip_all or n.image is None or (not n.image.packed_file and not os.path.exists(bpy.path.abspath(n.image.filepath)))):
            nt.nodes.remove(n)
            missing = True
    if not bsdf:
        continue
    col = bsdf.inputs["Base Color"]
    c = tuple(col.default_value)
    is_magenta = c[0] > 0.7 and c[2] > 0.7 and c[1] < 0.3
    if missing or is_magenta:
        name = m.name.lower()
        for keys, rgb, met, rough in PALETTE:
            if any(k in name for k in keys):
                col.default_value = (*rgb, 1)
                bsdf.inputs["Metallic"].default_value = met
                bsdf.inputs["Roughness"].default_value = rough
                break
        else:
            if is_magenta or sum(c[:3]) < 0.05:
                col.default_value = (0.82, 0.83, 0.85, 1)  # neutral lab white
                bsdf.inputs["Roughness"].default_value = 0.6

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
# Realize transforms, unparent.
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
for o in [o for o in bpy.data.objects if o.type != "MESH"]:
    bpy.data.objects.remove(o, do_unlink=True)
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Join meshes by their first material.
groups = {}
for o in meshes:
    key = o.data.materials[0].name if o.data.materials and o.data.materials[0] else "_none"
    groups.setdefault(key, []).append(o)
joined = []
for key, objs in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = "env_" + key[:40]
    joined.append(ob)


def tri_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


total = sum(tri_count(o) for o in joined)
print(f"[env] {len(joined)} meshes, {total} tris before decimation")
if total > max_tris:
    ratio = max_tris / total
    for o in joined:
        if tri_count(o) < 2000:
            continue  # keep simple architectural pieces exact
        mod = o.modifiers.new("dec", "DECIMATE")
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    total = sum(tri_count(o) for o in joined)
print(f"[env] {total} tris after")

# Floor at Z=0, centred.
pts = [o.matrix_world @ Vector(c) for o in joined for c in o.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)])
hi = Vector([max(p[i] for p in pts) for i in range(3)])
off = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
for o in joined:
    o.data.transform(__import__("mathutils").Matrix.Translation(-off))
print(f"[env] size {hi - lo}")

os.makedirs(os.path.dirname(dst), exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="STRIP")
blend_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "SourceAssets", "Blend")
os.makedirs(blend_dir, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, os.path.splitext(os.path.basename(dst))[0] + ".blend"))
print(f"[env] exported {dst}")
