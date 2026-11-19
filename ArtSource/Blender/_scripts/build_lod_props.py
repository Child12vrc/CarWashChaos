# Batch H4: scenery + big props → <name>_lod.glb (sources <folder>/<name>_lod.blend, built from <name>.blend).
#  trees / bush     <Node>_LOD1 = 50 % collapse, <Node>_LOD2 = convex blob (foliage) + a few-sided trunk (60–100 tris total)
#  ParkedCar_*      Body_LOD1 = Body + Details (+ TaxiSign) joined and collapsed, Details_LOD1 / TaxiSign_LOD1 empty,
#                   Wheels_LOD1 = 12-sided tyres + flat hub discs, Glass_LOD1 collapsed  (~35–40 % in all)
#  StreetLamp / UtilityPole   <Node>_LOD1 = 40 % collapse
#  SW_Canopy / SW_VacCanopy   no LOD; every mesh except RoofSign / Lights joined into `Structure` (1 submesh per material)
# Collapse is atlas-safe: each face remembers its atlas cell (swatch / tile) and its UVs are clamped back into it.
#   blender -b --factory-startup --python ArtSource/Blender/_scripts/build_lod_props.py -- [names]
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parts_lib as L
import props_lib as PL
from props_lib import PG
import lod_lib as LL

PROJ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
BL = os.path.join(PROJ, "ArtSource", "Blender")
ITEMS = {
    "Tree_A": ("Scenery", "tree"), "Tree_B": ("Scenery", "tree"), "Tree_C": ("Scenery", "tree"), "Bush_A": ("Scenery", "tree"),
    "ParkedCar_Compact": ("Scenery", "parked"), "ParkedCar_Sedan": ("Scenery", "parked"),
    "ParkedCar_SUV": ("Scenery", "parked"), "ParkedCar_Taxi": ("Scenery", "parked"),
    "StreetLamp": ("Scenery", "lod1"), "UtilityPole": ("Scenery", "lod1"),
    "SW_Canopy": ("Buildings", "merge"), "SW_VacCanopy": ("Buildings", "merge"),
}
RECTS = [PL.rect_of(n) for n in PL.BIG + PL.SMALL + PL.SW_KEYS]


def _cell(uvs):
    c = sum(uvs, Vector((0, 0))) / len(uvs)
    for i, (u0, v0, u1, v1) in enumerate(RECTS):
        if u0 <= c.x <= u1 and v0 <= c.y <= v1:
            return i
    return -1


def safe_collapse(o, target, min_tris=8):
    """Collapse-decimate to ~target tris; every face keeps its material and its atlas cell."""
    me = o.data
    n = LL.tris(o)
    if n <= max(target, min_tris):
        return n
    uv = me.uv_layers[0].data if me.uv_layers else None
    keys, fk = [], []
    for p in me.polygons:
        m = me.materials[p.material_index]
        textured = uv is not None and m and m.node_tree and any(nd.type == 'TEX_IMAGE' for nd in m.node_tree.nodes)
        k = (p.material_index, _cell([Vector(uv[i].uv) for i in p.loop_indices]) if textured else -1)
        if k not in keys:
            keys.append(k)
        fk.append(keys.index(k))
    orig = list(me.materials)
    tmp = [bpy.data.materials.new(f"_tmp{i}") for i in range(len(keys))]
    me.materials.clear()
    for t in tmp:
        me.materials.append(t)
    me.polygons.foreach_set("material_index", fk)
    res = LL.decimate_to(o, max(target, min_tris), tol=0.1)
    me = o.data
    idx = [keys[tmp.index(me.materials[p.material_index])] for p in me.polygons]
    uv = me.uv_layers[0].data if me.uv_layers else None
    for p, (mi, cell) in zip(me.polygons, idx):
        if cell >= 0 and uv is not None:
            u0, v0, u1, v1 = RECTS[cell]
            e = 1.5 / 1024
            for i in p.loop_indices:
                x, y = uv[i].uv
                uv[i].uv = (min(max(x, u0 + e), u1 - e), min(max(y, v0 + e), v1 - e))
    used = sorted({mi for mi, _ in idx})
    me.materials.clear()
    for mi in used:
        me.materials.append(orig[mi])
    me.polygons.foreach_set("material_index", [used.index(mi) for mi, _ in idx])
    for t in tmp:
        bpy.data.materials.remove(t)
    return res


def sib(src, name, mesh=None):
    o = bpy.data.objects.new(name, mesh)
    if mesh is None:
        o.empty_display_size = 0.05
    for c in src.users_collection:
        c.objects.link(o)
    o.parent = src.parent
    o.matrix_world = src.matrix_world.copy()
    return o


def dup(src, name):
    me = src.data.copy()
    me.name = "SM_" + name
    return sib(src, name, me)


def join_into(dst, srcs):
    """Append the meshes of srcs (same frame as dst: all props are built at their root's origin) into dst."""
    bm = bmesh.new()
    bm.from_mesh(dst.data)
    mats = list(dst.data.materials)
    for s in srcs:
        me = s.data.copy()
        me.transform(dst.matrix_world.inverted() @ s.matrix_world)
        rem = []
        for m in me.materials:
            if m not in mats:
                mats.append(m)
            rem.append(mats.index(m))
        for p in me.polygons:
            p.material_index = rem[p.material_index]
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = dst.data
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    bm.to_mesh(me)
    bm.free()


def blob(o, subdiv, mat):
    """Convex blob: an icosphere whose vertices sit on the support function of the LOD0 points (20 or 80 tris)."""
    pts = [v.co.copy() for v in o.data.vertices]
    c = sum(pts, Vector()) / len(pts)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    for v in bm.verts:
        d = v.co.normalized()
        v.co = c + d * max((p - c).dot(d) for p in pts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = o.data
    me.materials.clear()
    me.materials.append(mat)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.material_index = 0
        p.use_smooth = True


def tree(objs, root):
    total = sum(LL.tris(o) for o in objs)
    fol = bpy.data.materials.get("MAT_PropFoliage")
    for o in objs:
        safe_collapse(dup(o, o.name + "_LOD1"), LL.tris(o) // 2)
        is_fol = any(m == fol for m in o.data.materials)
        o2 = dup(o, o.name + "_LOD2")
        if is_fol:
            blob(o2, 2 if LL.tris(o) > 300 else 1, fol)   # bmesh: 1 = icosahedron (20), 2 = 80 tris
        else:
            safe_collapse(o2, min(14, LL.tris(o) // 3), 6)


def parked(objs, root):
    by = {o.name: o for o in objs}
    body, glass, wheels = by["Body"], by["Glass"], by["Wheels"]
    extra = [by[n] for n in ("Details", "TaxiSign") if n in by]
    b1 = dup(body, "Body_LOD1")
    join_into(b1, extra)
    total0 = sum(LL.tris(o) for o in objs)
    safe_collapse(b1, int((LL.tris(body) + sum(LL.tris(e) for e in extra)) * 0.3))
    for e in extra:
        sib(e, e.name + "_LOD1")
    safe_collapse(dup(glass, "Glass_LOD1"), int(LL.tris(glass) * 0.45))
    # wheels: tyre components (big) → 12-sided cylinders; hub caps → one flat 8-gon each
    bm = bmesh.new()
    bm.from_mesh(wheels.data)
    comps, seen = [], set()
    for v in bm.verts:
        if v in seen:
            continue
        stack, comp = [v], []
        seen.add(v)
        while stack:
            x = stack.pop()
            comp.append(x.co.copy())
            for e in x.link_edges:
                y = e.other_vert(x)
                if y not in seen:
                    seen.add(y)
                    stack.append(y)
        lo = Vector((min(p.x for p in comp), min(p.y for p in comp), min(p.z for p in comp)))
        hi = Vector((max(p.x for p in comp), max(p.y for p in comp), max(p.z for p in comp)))
        comps.append((lo, hi))
    bm.free()
    Rmax = max((hi.z - lo.z) for lo, hi in comps) / 2
    PL.PMAT["PropAtlas"] = bpy.data.materials["MAT_PropAtlas"]
    g = PG("Wheels_LOD1")
    for lo, hi in comps:
        c, R, w = (lo + hi) / 2, (hi.z - lo.z) / 2, hi.x - lo.x
        if R > Rmax * 0.8:
            g.cyl(c, R, w, ("sw", "black"), 12, "X")
        else:
            d = bmesh.new()
            ring = [d.verts.new((0, math.cos(2 * math.pi * k / 8) * R, math.sin(2 * math.pi * k / 8) * R)) for k in range(8)]
            out = 1 if c.x > 0 else -1
            d.faces.new(ring if out < 0 else ring[::-1])
            bmesh.ops.triangulate(d, faces=d.faces[:])
            g.add(d, ("sw", "white"), Matrix.Translation(Vector((hi.x if out > 0 else lo.x, c.y, c.z))))
    w1 = g.obj(wheels.users_collection[0], "Wheels_LOD1")
    w1.parent = wheels.parent
    w1.matrix_world = wheels.matrix_world.copy()


def lod1(objs, root):
    for o in objs:
        safe_collapse(dup(o, o.name + "_LOD1"), int(LL.tris(o) * 0.4))


def merge(objs, root):
    keep = ("RoofSign", "Lights")
    parts = [o for o in objs if o.name not in keep]
    st = sib(root, "Structure", bpy.data.meshes.new("SM_Structure"))
    st.parent = root
    st.matrix_world = root.matrix_world.copy()
    join_into(st, parts)
    for p in parts:
        bpy.data.objects.remove(p)


def level_tris(objs_all, level):
    names = {o.name: o for o in objs_all}
    t = 0
    for o in objs_all:
        lv = 2 if o.name.endswith("_LOD2") else 1 if o.name.endswith("_LOD1") else 0
        base = o.name[:-5] if lv else o.name
        if (lv == level or (lv == 0 and f"{base}_LOD{level}" not in names)) and o.type == 'MESH':
            t += LL.tris(o)
    return t


def run(name):
    folder, kind = ITEMS[name]
    path = os.path.join(BL, folder, name + ".blend")
    bpy.ops.wm.open_mainfile(filepath=path)
    exp = next(c for c in bpy.data.collections if c.get("export_path"))
    before = dict(tris=sum(LL.tris(o) for o in exp.all_objects), meshes=sum(1 for o in exp.all_objects if o.type == 'MESH'),
                  drawcalls=sum(len({p.material_index for p in o.data.polygons}) for o in exp.all_objects if o.type == 'MESH'))
    root = next(o for o in exp.all_objects if o.parent is None)
    objs = [o for o in exp.all_objects if o.type == 'MESH']
    {"tree": tree, "parked": parked, "lod1": lod1, "merge": merge}[kind](objs, root)
    exp["export_path"] = exp["export_path"][:-4] + "_lod.glb"
    exp.name = exp.name + "_lod"
    allo = list(exp.all_objects)
    lods = [level_tris(allo, k) for k in range(3)]
    after = dict(meshes=sum(1 for o in allo if o.type == 'MESH' and not o.name.endswith(("_LOD1", "_LOD2"))),
                 drawcalls=sum(len({p.material_index for p in o.data.polygons}) for o in allo if o.type == 'MESH' and not o.name.endswith(("_LOD1", "_LOD2"))))
    print("RESULT", name, before, "lod tris", lods, after)
    # preview (not for the canopies: no LOD there)
    if kind != "merge":
        groups = [[o for o in allo if o.type == 'MESH' and (o.name.endswith(f"_LOD{k}") if k else not o.name.endswith(("_LOD1", "_LOD2")))] for k in range(3)]
        for k in (1, 2):   # nodes without a LOD at that level show LOD0 (copy for the picture only)
            have = {o.name[:-5] for o in groups[k]}
            for o in groups[0]:
                if o.name not in have:
                    groups[k].append(None if any(x.name == o.name + f"_LOD{k}" for x in allo) else o)
            groups[k] = [o for o in groups[k] if o is not None]
        dims = max(o.dimensions.x for o in groups[0])
        L.preview_setup(Vector((0, 0, 1)), Vector((0, -6, 3)))
        _preview(groups, os.path.join(PROJ, "docs", "art_preview", f"{name}_lod.png"), dims * 1.4)
    L.save_blend(os.path.join(BL, folder, name + "_lod.blend"))
    sys.path.insert(0, BL)
    import export_all
    export_all.export_collection(exp)


def _preview(groups, path, spacing):
    """Copies of each level side by side (the copies are deleted again)."""
    tmp = []
    shown = []
    for i, g in enumerate(groups):
        for o in g:
            c = o.copy()
            c.parent = None
            c.matrix_world = Matrix.Translation(Vector((i * spacing, 0, 0))) @ o.matrix_world
            bpy.context.scene.collection.objects.link(c)
            tmp.append(c)
        shown.append([c for c in tmp[-len(g):]] if g else [])
    hidden = [o for o in bpy.context.scene.objects if o not in tmp and o.type == 'MESH' and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    LL.preview_lods(shown, path, 0, eye_dir=Vector((0.35, -1.0, 0.45)))
    for o in hidden:
        o.hide_render = False
    for c in tmp:
        bpy.data.objects.remove(c)


names = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for n in (names or list(ITEMS)):
    run(n)
