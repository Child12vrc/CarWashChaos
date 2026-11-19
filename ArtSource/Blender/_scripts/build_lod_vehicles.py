# Batch H1: vehicles v2 → <file>_v3.glb (merged static parts, one shared atlas material, LOD1 / LOD2 siblings).
# Source: the Vehicle_v2 export collection of ArtSource/Blender/Vehicles/<id>_<Name>.blend (left untouched);
# result:  ArtSource/Blender/Vehicles/<id>_<Name>_v3.blend (collection Vehicle_v3) + Generated/Vehicles/<file>_v3.glb.
#
#  LOD0  Body / Glass / Door_* (+ DoorDetail_*, Mirror_*) / Trunk / Wheel_* / Propeller / Barrel / Socket_* unchanged
#        (names, origins, UV2); every other mesh is joined into one node `Static` (origin = car origin);
#        Headlight_±1 / Taillight_±1 stay as empties at their old place.
#        Materials: the paint (Body's material) and the glass stay; every other flat colour + MAT_PropAtlas moves to ONE
#        material MAT_VehicleAtlas (T_VehicleAtlas: prop atlas at half size in the lower-left quarter + colour cells).
#  LOD1  ~40 %: wheels 18 sides without lug nuts, Static / DoorDetail / Mirror / Propeller / Barrel collapse-decimated,
#        Body / Door / Trunk: limited dissolve that never crosses a UV / UV2 seam (same UV2 layout).
#  LOD2  ~12 %: wheels 10 sides, small details dropped, Body / Door / Trunk coarser dissolve, Glass = one hull,
#        DoorDetail / Mirror → empty `_LOD2` nodes.
#   blender -b --factory-startup --python ArtSource/Blender/_scripts/build_lod_vehicles.py -- [ids]
import bpy, bmesh, math, os, sys, glob, json
import numpy as np
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parts_lib as L
import lod_lib as LL
import vehicles_v2 as V2

PROJ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
VDIR = os.path.join(PROJ, "ArtSource", "Blender", "Vehicles")
ATLAS_SRC = os.path.join(PROJ, "ArtSource", "Blender", "Textures", "T_PropAtlas.png")
ATLAS_OUT = os.path.join(PROJ, "ArtSource", "Blender", "Textures", "T_VehicleAtlas.png")
PREV = os.path.join(PROJ, "docs", "art_preview")

KEEP = ("Body", "Glass", "Door_", "DoorDetail_", "Mirror_", "Trunk", "Wheel_", "Propeller", "Barrel", "Socket_")
LIGHTS = ("Headlight_", "Taillight_")
DROP2 = ("LicensePlate", "Wipers", "FuelCap", "Antenna", "Exhaust", "Speaker", "Mirror_", "DoorDetail_")
PAINTED = ("Body", "Door_", "Trunk")       # carry UV2 (dirt mask) → seam-safe dissolve only
GRID = 16                                   # colour cells: 64 px in a 1024 atlas


def is_keep(n):
    return n in ("Body", "Glass", "Trunk", "Propeller", "Barrel") or n.startswith(KEEP[2:])


# ---------------------------------------------------------------- materials / atlas
def bsdf(m):
    return next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if m and m.node_tree else None


def mat_kind(m, paint):
    if m is None or m == paint:
        return "keep"
    b = bsdf(m)
    if b is None:
        return "keep"
    if b.inputs["Alpha"].default_value < 0.99 or (m.name and "Glass" in m.name):
        return "keep"
    link = b.inputs["Base Color"].links
    if link:
        img = getattr(link[0].from_node, "image", None)
        return "atlas" if img and "PropAtlas" in img.name else "keep"
    return "flat"


def mat_key(m):
    c = bsdf(m).inputs["Base Color"].default_value
    return tuple(round(float(x), 3) for x in c[:3])


def cell_rect(i):
    """i-th free cell (skipping the lower-left 8x8 block that holds the half-size prop atlas)."""
    free = [(x, y) for y in range(GRID) for x in range(GRID) if not (x < GRID // 2 and y < GRID // 2)]
    x, y = free[i]
    return x / GRID, y / GRID, (x + 1) / GRID, (y + 1) / GRID


def lin2srgb(c):
    c = np.clip(np.asarray(c, np.float64), 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def build_atlas(colors):
    src = bpy.data.images.load(ATLAS_SRC, check_existing=True)
    n = src.size[0]
    a = np.array(src.pixels[:], np.float32).reshape(n, n, 4)
    half = a.reshape(n // 2, 2, n // 2, 2, 4).mean(axis=(1, 3))
    N = 1024
    out = np.ones((N, N, 4), np.float32)
    out[:N // 2, :N // 2] = half
    cs = N // GRID
    for i, col in enumerate(colors):
        u0, v0, _, _ = cell_rect(i)
        x, y = int(round(u0 * N)), int(round(v0 * N))
        out[y:y + cs, x:x + cs, :3] = lin2srgb(col)
    img = bpy.data.images.get("T_VehicleAtlas") or bpy.data.images.new("T_VehicleAtlas", N, N, alpha=False)
    img.pixels[:] = out.ravel()
    img.filepath_raw = ATLAS_OUT
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(src)
    return img


def atlas_material():
    m = bpy.data.materials.get("MAT_VehicleAtlas") or bpy.data.materials.new("MAT_VehicleAtlas")
    m.use_nodes = True
    nt = m.node_tree
    b = bsdf(m)
    b.inputs["Roughness"].default_value = 0.5
    b.inputs["Metallic"].default_value = 0.0
    img = bpy.data.images.load(ATLAS_OUT, check_existing=False)
    img.name = "T_VehicleAtlas"
    img.pack()
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = img
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    m.diffuse_color = (0.6, 0.6, 0.6, 1)
    return m


def remap(o, paint, amat, cells):
    """Flat colours → a cell of MAT_VehicleAtlas, MAT_PropAtlas UVs → the half-size quarter; paint / glass stay."""
    me = o.data
    kinds = [mat_kind(m, paint) for m in me.materials]
    if all(k == "keep" for k in kinds):
        return
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv[0] if bm.loops.layers.uv else bm.loops.layers.uv.new("UVMap")
    new = []

    def idx(m):
        if m not in new:
            new.append(m)
        return new.index(m)
    for f in bm.faces:
        m = me.materials[f.material_index] if f.material_index < len(me.materials) else None
        k = kinds[f.material_index] if f.material_index < len(kinds) else "keep"
        if k == "keep":
            f.material_index = idx(m)
            continue
        if k == "atlas":
            for l in f.loops:
                l[uv].uv = l[uv].uv * 0.5
        else:
            u0, v0, u1, v1 = cell_rect(cells[mat_key(m)])
            cu, cv = (u0 + u1) / 2, (v0 + v1) / 2
            for l in f.loops:
                c = l.vert.co
                l[uv].uv = (cu + ((c.x * 7.3 + c.z * 3.1) % 1) * 0.02 - 0.01, cv + ((c.y * 5.7 + c.z * 2.3) % 1) * 0.02 - 0.01)
        f.material_index = idx(amat)
    me.materials.clear()          # (clearing slots resets the face indices → write the faces afterwards)
    for m in new:
        me.materials.append(m)
    bm.to_mesh(me)
    bm.free()


# ---------------------------------------------------------------- geometry helpers
def join(parts, name, col):
    bm = bmesh.new()
    mats = []
    for p in parts:
        me = p.data.copy()
        me.transform(p.matrix_world)
        if p.matrix_world.determinant() < 0:
            me.flip_normals()
        remap_i = []
        for m in me.materials:
            if m not in mats:
                mats.append(m)
            remap_i.append(mats.index(m))
        for poly in me.polygons:
            poly.material_index = remap_i[poly.material_index] if remap_i else 0
        while len(me.uv_layers) > 1:
            me.uv_layers.remove(me.uv_layers[-1])
        if me.uv_layers:
            me.uv_layers[0].name = "UVMap"
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new("SM_" + name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    return o


def sibling(src, name, mesh=None):
    """New object next to `src` (same parent, same world matrix)."""
    o = bpy.data.objects.new(name, mesh)
    if mesh is None:
        o.empty_display_size = 0.05
    src.users_collection[0].objects.link(o)
    o.parent = src.parent
    o.matrix_world = src.matrix_world.copy()
    return o


def dup(src, name):
    me = src.data.copy()
    me.name = "SM_" + name
    return sibling(src, name, me)


def collapse(o, ratio, min_tris=24):
    n = LL.tris(o)
    if n <= min_tris:
        return n
    return LL.decimate_to(o, max(min_tris, int(n * ratio)), tol=0.1)


def seam_dissolve(o, angle_deg):
    """Limited dissolve that keeps every UV / UV2 seam and material border → UV layouts stay exactly as on LOD0."""
    me = o.data
    bm = bmesh.new()
    bm.from_mesh(me)
    layers = list(bm.loops.layers.uv.values())
    for e in bm.edges:
        e.seam = False
        if len(e.link_loops) != 2:
            e.seam = True
            continue
        l1, l2 = e.link_loops
        for uv in layers:
            a1, b1 = l1[uv].uv, l1.link_loop_next[uv].uv
            if l1.vert == l2.vert:
                a2, b2 = l2[uv].uv, l2.link_loop_next[uv].uv
            else:
                a2, b2 = l2.link_loop_next[uv].uv, l2[uv].uv
            if (a1 - a2).length > 1e-5 or (b1 - b2).length > 1e-5:
                e.seam = True
                break
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(angle_deg), use_dissolve_boundaries=False,
                             verts=bm.verts[:], edges=bm.edges[:], delimit={'SEAM', 'MATERIAL'})
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
    for e in bm.edges:
        e.seam = False
    bm.to_mesh(me)
    bm.free()
    return LL.tris(o)


def glass_hull(o):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    res = bmesh.ops.convex_hull(bm, input=bm.verts[:])
    bmesh.ops.delete(bm, geom=[g for g in res["geom_interior"] + res["geom_unused"] if isinstance(g, bmesh.types.BMVert)], context='VERTS')
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(4), verts=bm.verts[:], edges=bm.edges[:])
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        f.material_index = 0
    bm.to_mesh(o.data)
    bm.free()


def wheel_lod(src, lod, chunky):
    """Simplified wheel in the LOD0 wheel's frame and materials (tyre, rim, hub)."""
    mats = list(src.data.materials)
    tyre, rim = mats[0], mats[1] if len(mats) > 1 else mats[0]
    hub = mats[2] if len(mats) > 2 else rim
    L.MAT["_Tyre"], L.MAT["_Rim"], L.MAT["_Hub"] = tyre, rim, hub
    d = src.dimensions
    R, W = d.z / 2 - (0.055 if chunky else 0.0), d.x
    s = 1 if src.matrix_world.translation.x > 0 else -1
    w2 = W / 2
    rr = R * (0.55 if not chunky else 0.5)
    sh = min(0.035, W * 0.18)
    g = L.G(src.name + f"_LOD{lod}")
    if lod == 1:
        prof = [(rr * 0.98, -w2 * 0.92 * s), (R - sh, -w2 * s), (R, -(w2 - sh) * s), (R, (w2 - sh) * s), (R - sh, w2 * s), (rr * 0.98, w2 * 0.92 * s)]
        seg, dseg = (20 if chunky else 18), 14
    else:
        prof = [(rr, -w2 * s), (R, -w2 * 0.85 * s), (R, w2 * 0.85 * s), (rr, w2 * s)]
        seg, dseg = 10, 8
    if s < 0:
        prof = prof[::-1]
    if chunky and lod == 1:
        prof = [(r + (0.03 if abs(r - R) < 1e-6 else 0), a) for r, a in prof]
    g.add(V2._lathe(prof, seg), "_Tyre")
    if chunky and lod == 1:
        for k in range(10):
            a = 2 * math.pi * (k + 0.5) / 10
            c = Vector((0, math.cos(a) * (R + 0.03), math.sin(a) * (R + 0.03)))
            g.add(L.t_box((W * 0.8, 0.07, 0.12), 0.0, 1), "_Tyre", Matrix.Translation(c) @ Matrix.Rotation(a, 4, 'X'))
    dish = [(0.0, w2 * 0.58 * s), (rr * 0.9, w2 * 0.66 * s), (rr, w2 * 0.8 * s), (rr, -w2 * 0.7 * s), (0.0, -w2 * 0.7 * s)]
    if lod == 2:
        dish = [(0.0, w2 * 0.66 * s), (rr, w2 * 0.8 * s), (rr, -w2 * 0.7 * s), (0.0, -w2 * 0.7 * s)]
    if s < 0:
        dish = dish[::-1]
    g.add(V2._lathe(dish, dseg), "_Rim")
    if lod == 1:
        for k in range(5):
            a = 2 * math.pi * k / 5 + math.pi / 10
            c = Vector((w2 * 0.72 * s, math.cos(a) * rr * 0.55, math.sin(a) * rr * 0.55))
            g.add(L.t_box((0.03, rr * 0.75, rr * 0.2), 0.0, 1), "_Rim", Matrix.Translation(c) @ Matrix.Rotation(a, 4, 'X'))
        g.add(L.t_cyl(rr * 0.3, 0.04, 10), "_Hub", Matrix.Translation((w2 * 0.76 * s, 0, 0)) @ L.RY90)
    o = g.obj(src.matrix_world.copy(), src.users_collection[0])
    o.name = src.name + f"_LOD{lod}"
    o.data.name = "SM_" + o.name
    o.parent = src.parent
    o.matrix_world = src.matrix_world.copy()
    return o


# ---------------------------------------------------------------- per car
def visible_at(col, level):
    """What Unity shows at LOD `level`: <Node>_LOD<level> if it exists (an empty one hides the node), else LOD0."""
    objs = {o.name: o for o in col.all_objects}
    vis = set()
    for o in objs.values():
        lv = 2 if o.name.endswith("_LOD2") else 1 if o.name.endswith("_LOD1") else 0
        base = o.name[:-5] if lv else o.name
        if lv == level or (lv == 0 and f"{base}_LOD{level}" not in objs):
            vis.add(o)
    return vis


def stats(col, level):
    t = r = dc = 0
    mats = set()
    for o in visible_at(col, level):
        if o.type == 'MESH' and len(o.data.polygons):
            t += LL.tris(o)
            r += 1
            used = {p.material_index for p in o.data.polygons}
            dc += len(used)
            mats |= {o.data.materials[i].name for i in used if i < len(o.data.materials)}
    return dict(tris=t, renderers=r, drawcalls=dc, materials=len(mats))


def preview(col, cid):
    sc = bpy.context.scene
    pts = [o.matrix_world @ Vector(c) for o in col.all_objects if o.type == 'MESH' and not o.name.endswith(("_LOD1", "_LOD2")) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi) / 2
    size = (hi - lo).length
    L.preview_setup(c, c + Vector((1, -1, 0.5)))
    cam = sc.camera
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = size * 1.05
    eye = Vector((0.75, -1.0, 0.45)).normalized() * size * 2
    cam.location = c + eye
    cam.rotation_euler = (-eye).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = 640, 480
    meshes = [o for o in col.all_objects if o.type == 'MESH']
    tiles = []
    for lv in range(3):
        vis = visible_at(col, lv)
        for o in meshes:
            o.hide_render = o not in vis
        p = os.path.join(bpy.app.tempdir, f"lod{lv}.png")
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(p)
        tiles.append(np.array(im.pixels[:], np.float32).reshape(480, 640, 4))
        bpy.data.images.remove(im)
    for o in meshes:
        o.hide_render = False
    out = np.concatenate(tiles, axis=1)
    img = bpy.data.images.new("prev", 1920, 480, alpha=False)
    img.pixels[:] = out.ravel()
    img.filepath_raw = os.path.join(PREV, f"Vehicle_{cid}_v3_lod.png")
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def uv2_ok(col):
    bad = []
    for o in col.all_objects:
        if o.type == 'MESH' and o.name.startswith(PAINTED) and len(o.data.uv_layers) > 1:
            ov, _ = V2._uv2_overlap_check(o)
            if ov:
                bad.append((o.name, ov))
    return bad


def run(blend, cells, atlas_img):
    bpy.ops.wm.open_mainfile(filepath=blend)
    cid = os.path.basename(blend).split("_")[0]
    v2 = bpy.data.collections["Vehicle_v2"]
    stem = os.path.basename(v2["export_path"])[:-len("_v2.glb")]
    keep_cols = {v2} | set(v2.children_recursive)
    keep = set(v2.all_objects)
    for o in list(bpy.data.objects):
        if o not in keep and o.name not in ("CAM_Preview", "LGT_Sun"):
            bpy.data.objects.remove(o)
    for c in list(bpy.data.collections):
        if c not in keep_cols and c.name not in ("Export", "Preview"):
            bpy.data.collections.remove(c)
    v2.name = "Vehicle_v3"
    v3 = v2
    v3["export_path"] = f"Assets/_Project/Art/Models/Generated/Vehicles/{stem}_v3.glb"
    v3["zero_root"] = 0
    before = stats(v3, 0)
    objs = {o.name: o for o in v3.all_objects}
    body = objs["Body"]
    paint = body.data.materials[0]
    for o in v3.all_objects:
        if o.type == 'MESH' and o.data.users > 1:
            o.data = o.data.copy()
    ext = next((c for c in v3.children_recursive if c.name.startswith("Exterior_Parts")), v3)

    # ---- static parts → Static / Static_LOD1 / Static_LOD2 (decimate the copies while colours are still per material)
    statics = [o for o in v3.all_objects if o.type == 'MESH' and not is_keep(o.name)]
    lod_parts = {1: [], 2: []}
    for p in statics:
        for lv in (1, 2):
            if lv == 2 and (p.name.startswith(DROP2) or LL.tris(p) < 100):
                continue
            c = dup(p, f"{p.name}__lod{lv}")
            collapse(c, 0.4 if lv == 1 else 0.14, 12)
            lod_parts[lv].append(c)
    static = join(statics, "Static", ext)
    s1 = join(lod_parts[1], "Static_LOD1", ext)
    s2 = join(lod_parts[2], "Static_LOD2", ext)
    for c in lod_parts[1] + lod_parts[2]:
        bpy.data.objects.remove(c)
    for p in statics:
        for ch in list(p.children):          # a kept node under a merged one → keep its world place
            mw = ch.matrix_world.copy()
            ch.parent = p.parent
            ch.matrix_world = mw
        name, mw, par = p.name, p.matrix_world.copy(), p.parent
        bpy.data.objects.remove(p)
        if name.startswith(LIGHTS):
            e = bpy.data.objects.new(name, None)
            e.empty_display_size = 0.05
            ext.objects.link(e)
            e.parent = par
            e.matrix_world = mw

    # ---- kept nodes
    objs = {o.name: o for o in v3.all_objects}
    for n, o in sorted(objs.items()):
        if o.type != 'MESH' or n in ("Static", "Static_LOD1", "Static_LOD2"):
            continue
        if n.startswith("Wheel_"):
            wheel_lod(o, 1, cid == "B01")
            wheel_lod(o, 2, cid == "B01")
        elif n.startswith(PAINTED):
            for lv, ang in ((1, 6), (2, 22)):
                c = dup(o, f"{n}_LOD{lv}")
                seam_dissolve(c, ang)
                if LL.tris(c) >= LL.tris(o) * 0.95:      # nothing to gain → Unity keeps LOD0
                    bpy.data.objects.remove(c)
        elif n.startswith(("DoorDetail_", "Mirror_")):
            collapse(dup(o, f"{n}_LOD1"), 0.45)
            sibling(o, f"{n}_LOD2")
        elif n in ("Propeller", "Barrel"):
            collapse(dup(o, f"{n}_LOD1"), 0.5)
            collapse(dup(o, f"{n}_LOD2"), 0.2, 12)
        elif n == "Glass" and LL.tris(o) > 60:
            glass_hull(dup(o, "Glass_LOD2"))

    # ---- one atlas material for everything that is not paint / glass
    amat = bpy.data.materials.get("MAT_VehicleAtlas") or atlas_material()
    for o in v3.all_objects:
        if o.type == 'MESH':
            remap(o, paint, amat, cells)
    bad = uv2_ok(v3)
    res = dict(id=cid, file=f"{stem}_v3.glb", before=before, lod0=stats(v3, 0), lod1=stats(v3, 1), lod2=stats(v3, 2), uv2_overlap=bad)
    print("RESULT", json.dumps(res))
    preview(v3, cid)
    out = blend[:-6] + "_v3.blend"
    L.save_blend(out)
    sys.path.insert(0, os.path.join(PROJ, "ArtSource", "Blender"))
    import export_all
    export_all.export_collection(v3)
    return res


def scan_colors(blends):
    keys = set()
    for b in blends:
        bpy.ops.wm.open_mainfile(filepath=b)
        v2 = bpy.data.collections["Vehicle_v2"]
        paint = bpy.data.objects["Body"].data.materials[0]
        for o in v2.all_objects:
            if o.type == 'MESH':
                for m in o.data.materials:
                    if mat_kind(m, paint) == "flat":
                        keys.add(mat_key(m))
    return sorted(keys)


ids = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
blends = sorted(b for b in glob.glob(os.path.join(VDIR, "*.blend")) if not b.endswith("_v3.blend"))
colors = scan_colors(blends)
print("palette", len(colors))
bpy.ops.wm.read_factory_settings(use_empty=True)
build_atlas(colors)
cells = {k: i for i, k in enumerate(colors)}
results = []
for b in blends:
    if ids and os.path.basename(b).split("_")[0] not in ids:
        continue
    results.append(run(b, cells, None))
with open(os.path.join(bpy.app.tempdir, "lod_vehicles.json"), "w") as f:
    json.dump(results, f)
