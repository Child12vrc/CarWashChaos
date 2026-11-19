# Batch H3: interiors → Interior_<id>_lod.glb (no LOD meshes; static parts merged per group, one submesh per material).
# Top-level nodes:  Seats (Seat_*, RearBench) · Dashboard (dash, centre stack, cluster, vents, column, rear-view mirror,
# air freshener) · Floor (floor, mats, pedals, console, gear lever, rear panel) — each ONE mesh;
# unchanged: SteeringWheel (+ Hand_L / Hand_R), TrashBin, Socket_* (names, transforms).
# Source: the "Interior" export collection of ArtSource/Blender/Vehicles/<id>_<Name>.blend (left untouched);
# result: ArtSource/Blender/Vehicles/<id>_<Name>_Interior_lod.blend.
#   blender -b --factory-startup --python ArtSource/Blender/_scripts/build_interior_lod.py -- [ids]
import bpy, bmesh, os, sys, glob
from mathutils import Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parts_lib as L
import lod_lib as LL

PROJ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
VDIR = os.path.join(PROJ, "ArtSource", "Blender", "Vehicles")
KEEP = ("SteeringWheel", "Hand_", "TrashBin", "Socket_")
GROUPS = [("Seats", ("Seat_", "RearBench", "Bench")),
          ("Dashboard", ("Dashboard", "CenterStack", "InstrumentCluster", "Vent_", "SteeringColumn", "RearViewMirror", "AirFreshener")),
          ("Floor", ())]   # Floor = everything else


def group_of(n):
    for g, pre in GROUPS:
        if pre and n.startswith(pre):
            return g
    return "Floor"


def merged(name, parts, root, col):
    bm = bmesh.new()
    mats = []
    inv = root.matrix_world.inverted()
    for p in parts:
        me = p.data.copy()
        me.transform(inv @ p.matrix_world)
        rem = []
        for m in me.materials:
            if m not in mats:
                mats.append(m)
            rem.append(mats.index(m))
        for poly in me.polygons:
            poly.material_index = rem[poly.material_index]
        while len(me.uv_layers) > 1:
            me.uv_layers.remove(me.uv_layers[-1])
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new("SM_" + name)
    for m in mats:
        me.materials.append(m)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    o.parent = root
    o.matrix_parent_inverse = Matrix()
    o.matrix_basis = Matrix()
    return o


def run(blend):
    bpy.ops.wm.open_mainfile(filepath=blend)
    cid = os.path.basename(blend).split("_")[0]
    col = bpy.data.collections.get("Interior")
    if col is None or not col.get("export_path"):
        return None
    keep_cols = {col} | set(col.children_recursive)
    keep = set(col.all_objects)
    for o in list(bpy.data.objects):
        if o not in keep and o.name not in ("CAM_Preview", "LGT_Sun"):
            bpy.data.objects.remove(o)
    for c in list(bpy.data.collections):
        if c not in keep_cols and c.name not in ("Export", "Preview"):
            bpy.data.collections.remove(c)
    if col.name not in bpy.data.collections["Export"].children:
        bpy.data.collections["Export"].children.link(col)
    meshes = [o for o in col.all_objects if o.type == 'MESH']
    before = dict(tris=sum(LL.tris(o) for o in meshes), meshes=len(meshes),
                  drawcalls=sum(len({p.material_index for p in o.data.polygons}) for o in meshes))
    root = next(o for o in col.all_objects if o.parent is None)
    parts = [o for o in meshes if not o.name.startswith(KEEP)]
    groups = {}
    for p in parts:
        groups.setdefault(group_of(p.name), []).append(p)
    for g, ps in groups.items():
        merged(g + "__new", ps, root, col)
    for p in parts:
        for ch in list(p.children):
            if ch not in parts:
                mw = ch.matrix_world.copy()
                ch.parent = root
                ch.matrix_world = mw
    for p in parts:
        bpy.data.objects.remove(p)
    for g in groups:
        o = bpy.data.objects[g + "__new"]
        o.name = g
        o.data.name = "SM_" + g
    meshes = [o for o in col.all_objects if o.type == 'MESH']
    after = dict(tris=sum(LL.tris(o) for o in meshes), meshes=len(meshes),
                 drawcalls=sum(len({p.material_index for p in o.data.polygons}) for o in meshes))
    col["export_path"] = col["export_path"][:-4] + "_lod.glb"
    print("RESULT", cid, before, "->", after, sorted(o.name for o in col.all_objects))
    L.save_blend(blend[:-6] + "_Interior_lod.blend")
    sys.path.insert(0, os.path.join(PROJ, "ArtSource", "Blender"))
    import export_all
    export_all.export_collection(col)
    return cid, before, after


ids = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for b in sorted(glob.glob(os.path.join(VDIR, "*.blend"))):
    if b.endswith(("_v3.blend", "_lod.blend")):
        continue
    if ids and os.path.basename(b).split("_")[0] not in ids:
        continue
    run(b)
