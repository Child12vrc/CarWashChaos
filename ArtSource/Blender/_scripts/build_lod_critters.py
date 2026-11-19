# Batch H2: Hunyuan critters (50k) → <id>_hy_lod.glb with LOD0 ~6k / <node>_LOD1 ~1.5k / <node>_LOD2 ~400.
# Same single-node contract as the _hy file (node name, X+90 rotation, origin, scale, material name).
# Plain decimation smears small texture features (eyes, mouth) because the old UVs no longer follow the surface, so
# every LOD gets its own fresh UVs and the 50k model (base colour, metal/rough, geometry + normal map) is baked onto
# them. All three LODs share ONE material and one 1024 texture set:
#   U 0–0.75 = LOD0, U 0.75–1 / V 0.5–1 = LOD1, U 0.75–1 / V 0–0.5 = LOD2.
#   blender -b --factory-startup --python ArtSource/Blender/_scripts/build_lod_critters.py -- [ids]
import bpy, os, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parts_lib as L
import lod_lib as LL

PROJ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
SRC = "Assets/_Project/Art/Models/Generated/Critters/{}.glb"
CRITTERS = ["C01_raccoon_hy", "C05_possum_hy", "C03_mud_slime_hy"]
BUDGET = (6000, 1500, 400)
REGION = ((0.0, 0.0, 0.75, 1.0), (0.75, 0.5, 0.25, 0.5), (0.75, 0.0, 0.25, 0.5))   # u0, v0, du, dv per LOD
RES = 1024


def unwrap(o, region):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004, area_weight=0.0, scale_to_bounds=True)
    bpy.ops.uv.pack_islands(margin=0.006, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    u0, v0, du, dv = region
    for d in o.data.uv_layers.active.data:
        d.uv = (u0 + d.uv.x * du, v0 + d.uv.y * dv)


def _img(name, non_color, res=RES):
    img = bpy.data.images.new(name, res, res, alpha=False)
    img.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    return img


def bake(hi, lods, cid):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 4
    sc.render.bake.margin = 6
    mat = lods[0].data.materials[0]
    nt = mat.node_tree
    tex = {n.image.name: n for n in nt.nodes if n.type == 'TEX_IMAGE'}
    base_n = next(n for n in tex.values() if not ("normal" in n.image.name or "metallic" in n.image.name))
    mr_n = next(n for n in tex.values() if "metallic" in n.image.name)
    nrm_n = next(n for n in tex.values() if "normal" in n.image.name)
    # the 50k mesh reads a copy of the original material; the LODs keep the original (bake targets live there)
    src = mat.copy()
    src.name = mat.name + "_bakesrc"
    hi.data.materials[0] = src
    snt = src.node_tree
    out = next(n for n in snt.nodes if n.type == 'OUTPUT_MATERIAL')
    bsdf_link = out.inputs['Surface'].links[0].from_socket
    emis = snt.nodes.new('ShaderNodeEmission')
    stex = {n.image.name: n for n in snt.nodes if n.type == 'TEX_IMAGE'}

    targets = {"base": _img(f"T_{cid}_lod_base", False), "mr": _img(f"T_{cid}_lod_mr", True, 512),
               "normal": _img(f"T_{cid}_lod_normal", True)}
    tgt = nt.nodes.new('ShaderNodeTexImage')
    nt.nodes.active = tgt
    bpy.ops.object.select_all(action='DESELECT')
    hi.hide_set(False)
    hi.hide_render = False
    hi.select_set(True)
    for o in lods:
        o.select_set(True)
    size = max(lods[0].dimensions)
    for key, img in targets.items():
        tgt.image = img
        if key == "normal":
            snt.links.new(bsdf_link, out.inputs['Surface'])
            btype, kw = 'NORMAL', dict(normal_space='TANGENT')
        else:
            srcimg = stex[(base_n if key == "base" else mr_n).image.name]
            snt.links.new(srcimg.outputs['Color'], emis.inputs['Color'])
            snt.links.new(emis.outputs['Emission'], out.inputs['Surface'])
            btype, kw = 'EMIT', {}
        # one LOD active at a time; the image is shared so each bake only fills that LOD's region
        for o in lods:
            bpy.context.view_layer.objects.active = o
            for x in lods:
                x.select_set(x is o)
            bpy.ops.object.bake(type=btype, use_selected_to_active=True, cage_extrusion=size * 0.03,
                                max_ray_distance=size * 0.08, use_clear=(o is lods[0]), **kw)
        jpg = key == "base"
        img.filepath_raw = os.path.join(bpy.app.tempdir, img.name + (".jpg" if jpg else ".png"))
        img.file_format = 'JPEG' if jpg else 'PNG'
        img.save()
        img.pack()
    # material: swap the three textures for the baked ones
    base_n.image, mr_n.image, nrm_n.image = targets["base"], targets["mr"], targets["normal"]
    nt.nodes.remove(tgt)
    hi.data.materials[0] = mat
    bpy.data.materials.remove(src)
    hi.hide_render = True


def run(cid):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(PROJ, SRC.format(cid)))
    hi = bpy.data.objects[cid]
    hi.name = cid + "_HI"
    ref = L.collection("Reference_hy")
    for c in hi.users_collection:
        c.objects.unlink(hi)
    ref.objects.link(hi)
    exp = L.collection("Export")
    col = L.collection(cid + "_lod", exp, export_path=SRC.format(cid + "_lod"), zero_root=0)
    lods = []
    for i, budget in enumerate(BUDGET):
        name = cid if i == 0 else f"{cid}_LOD{i}"
        o = LL.copy_obj(hi, name, col)
        LL.weld(o)
        n = LL.decimate_to(o, budget)
        o.data.name = "SM_" + name
        for p in o.data.polygons:
            p.use_smooth = True
        unwrap(o, REGION[i])
        lods.append(o)
        print(f"[{cid}] {name}: {n} tris")
    bake(hi, lods, cid)
    ref.hide_render = True
    hi.hide_set(True)
    dims = lods[0].dimensions
    L.preview_setup(Vector((0, 0, dims.z / 2)), Vector((0, -3, 1)))
    LL.preview_lods([[o] for o in lods], os.path.join(PROJ, "docs", "art_preview", f"{cid}_lod.png"),
                    spacing=max(dims.x, dims.y) * 1.25, eye_dir=Vector((0.25, -1.0, 0.3)))
    L.save_blend(os.path.join(PROJ, "ArtSource", "Blender", "Critters", f"{cid}_lod.blend"))
    sys.path.insert(0, os.path.join(PROJ, "ArtSource", "Blender"))
    import export_all
    print("exported", export_all.export_collection(col))


ids = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for cid in (ids or CRITTERS):
    run(cid)
