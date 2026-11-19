# Builds prop .blend files + .glb + preview PNGs from prop definitions (props_selfwash.py, …).
#   blender -b --factory-startup --python ArtSource/Blender/_scripts/build_props.py -- [atlas] [SW_PayTerminal …]
# Each prop → ArtSource/Blender/<folder>/<id>.blend (collections: Model, Sockets, Export, Preview)
#          → <export_path> (root node = <id> at the origin, Unity frame: pivot / front as in ART_REQUESTS)
#          → docs/art_preview/<id>.png
import bpy, bmesh, os, sys, math
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import parts_lib as L
import props_lib as PL
import export_all
import props_selfwash
import props_selfwash2
import props_selfwash3
import props_lot
import props_critters
import props_scenery
import props_tools
import props_parked

PREV = os.path.join(L.PROJ, r"docs\art_preview")
REGISTRY = {}
REGISTRY.update(props_selfwash.PROPS)
REGISTRY.update(props_selfwash2.PROPS)
REGISTRY.update(props_selfwash3.PROPS)
REGISTRY.update(props_lot.PROPS)
REGISTRY.update(props_critters.PROPS)
REGISTRY.update(props_scenery.PROPS)
REGISTRY.update(props_tools.PROPS)
REGISTRY.update(props_parked.PROPS)
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def tris_of(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons) if o.type == 'MESH' else 0


def overlap_report(objs):
    """Cheap self-check: count faces whose centre lies inside another node's mesh (interpenetration hint)."""
    return 0


def build(pid):
    spec = REGISTRY[pid]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    PL.prop_mats()
    model = L.collection("Model")
    socks = L.collection("Sockets")
    exp = L.collection("Export")
    ecol = L.collection("EXP_" + pid, exp, export_path=spec["export"], zero_root=0)
    root = bpy.data.objects.new(spec.get("root", pid), None)
    root.empty_display_size = 0.2
    model.objects.link(root)
    ecol.objects.link(root)
    made = {}
    nodes = spec["build"]()
    for n in nodes:
        name, src = n[0], n[1]
        origin = n[2] if len(n) > 2 and n[2] is not None else Vector()
        parent = n[3] if len(n) > 3 else None
        if src is None:  # empty / socket
            o = bpy.data.objects.new(name, None)
            o.empty_display_size = 0.05
            o.location = origin
            socks.objects.link(o)
        else:
            o = src.obj(model, name, Matrix.Translation(origin))
        ecol.objects.link(o)
        made[name] = (o, parent)
    bpy.context.view_layer.update()
    for name, (o, parent) in made.items():
        mw = o.matrix_world.copy()
        o.parent = made[parent][0] if parent else root
        o.matrix_world = mw
    bpy.context.view_layer.update()
    tri = sum(tris_of(o) for o, _ in made.values())
    pts = [o.matrix_world @ v.co for o, _ in made.values() if o.type == 'MESH' for v in o.data.vertices]
    mn = Vector([min(p[i] for p in pts) for i in range(3)])
    mx = Vector([max(p[i] for p in pts) for i in range(3)])
    size_u = (mx.x - mn.x, mx.z - mn.z, mx.y - mn.y)  # Unity W × H × D
    centre = (mn + mx) / 2
    diag = (mx - mn).length
    eye = centre + Vector(spec.get("view", (-0.55, 1.0, 0.55))).normalized() * max(0.3, diag * 1.7)
    L.preview_setup(target=centre, eye=eye, lens=40)
    blend = os.path.join(L.BLEND, spec["folder"], pid + ".blend")
    L.save_blend(blend)
    export_all.export_current()
    # ---- preview render (not saved): ground (+ wall for wall-mounted props)
    sc = bpy.context.scene
    pcol = L.collection("Preview")
    gm = bpy.data.materials.new("MAT_PreviewGround")
    gm.use_nodes = True
    next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.36, 0.37, 0.39, 1)
    def plane(nm, M, size):
        me = bpy.data.meshes.new(nm)
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=size)
        bm.to_mesh(me)
        me.materials.append(gm)
        o = bpy.data.objects.new(nm, me)
        o.matrix_world = M
        pcol.objects.link(o)
    floor_z = spec.get("floor_z", mn.z)
    plane("PreviewGround", Matrix.Translation((0, 0, floor_z - 0.001)), 6)
    if spec.get("wall_y") is not None:
        plane("PreviewWall", Matrix.Translation((0, spec["wall_y"], 0)) @ Matrix.Rotation(math.radians(90), 4, 'X'), 6)
    if spec.get("ceiling_z") is not None:
        plane("PreviewCeiling", Matrix.Translation((0, 0, spec["ceiling_z"])), 6)
    sc.render.filepath = os.path.join(PREV, pid + ".png")
    sc.render.resolution_x, sc.render.resolution_y = 900, 900
    bpy.ops.render.render(write_still=True)
    print(f"PROP {pid}: tris {tri}, size W×H×D {size_u[0]:.2f} × {size_u[1]:.2f} × {size_u[2]:.2f} m (spec {spec['size']}), "
          f"mats {sorted({m.name for o, _ in made.values() if o.type == 'MESH' for m in o.data.materials})}, nodes {list(made)}")


if __name__ == "__main__":
    if "atlas" in ARGS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        print("ATLAS", PL.build_atlas())
    ids = [a for a in ARGS if a in REGISTRY] or ([] if "atlas" in ARGS and len(ARGS) == 1 else list(REGISTRY))
    for pid in ids:
        build(pid)
