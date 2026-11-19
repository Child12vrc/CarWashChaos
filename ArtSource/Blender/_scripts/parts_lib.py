# Shared cartoon low-poly part library (Blender 5.0). Every part is built in its OWN local frame:
#   Blender -Y = forward (Unity +Z after glTF export), +X = left / driver side (Unity -X), +Z = up.
# Assemblers (build_interiors.py, later exterior / props) place parts with a Matrix and can re-check them.
# build_parts_library.py puts every part in Parts_Library.blend and exports Art/Models/Parts/<Category>/<Name>.glb.
import bpy, bmesh, os, math
from mathutils import Vector, Matrix

PROJ = r"C:\GameDev\Projects\MyGame3D"
PARTS = os.path.join(PROJ, r"Assets\_Project\Art\Models\Parts")


def P(x, f, u):  # x = + left / driver side, f = forward, u = up
    return Vector((x, -f, u))


def clamp(v, a, b):
    return max(a, min(b, v))


# ---------------------------------------------------------------- shared materials (flat PBR, cartoon)
MAT_SPEC = {  # name: (base colour, roughness, metallic)
    "Fabric_Seat": ((0.90, 0.90, 0.88), 0.95, 0.0),   # white: tinted per car in game
    "Fabric_Carpet": ((0.20, 0.21, 0.24), 1.0, 0.0),
    "Plastic_Dark": ((0.10, 0.105, 0.12), 0.5, 0.0),
    "Rubber_Black": ((0.03, 0.03, 0.035), 0.85, 0.0),
}
MAT = {}


def make_mats():
    MAT.clear()
    for n, (c, r, mt) in MAT_SPEC.items():
        m = bpy.data.materials.get("MAT_" + n) or bpy.data.materials.new("MAT_" + n)
        try:
            m.use_nodes = True
        except Exception:
            pass
        b = next(nd for nd in m.node_tree.nodes if nd.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = (*c, 1)
        b.inputs["Roughness"].default_value = r
        b.inputs["Metallic"].default_value = mt
        m.diffuse_color = (*c, 1)
        MAT[n] = m


# ---------------------------------------------------------------- mesh group (one node, several materials)
class G:
    def __init__(s, name):
        s.name, s.bm, s.mats = name, bmesh.new(), []

    def add(s, tbm, mat, M=None):
        if M is not None:
            tbm.transform(M)
        if mat not in s.mats:
            s.mats.append(mat)
        idx = s.mats.index(mat)
        for f in tbm.faces:
            f.material_index = idx
            f.smooth = False
        me = bpy.data.meshes.new("tmp")
        tbm.to_mesh(me)
        tbm.free()
        s.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def merge(s, other, M=None):
        """Append another group (e.g. a library part) with a transform."""
        me = bpy.data.meshes.new("tmp")
        bm = other.bm.copy()
        if M is not None:
            bm.transform(M)
        remap = []
        for m in other.mats:
            if m not in s.mats:
                s.mats.append(m)
            remap.append(s.mats.index(m))
        for f in bm.faces:
            f.material_index = remap[f.material_index]
        bm.to_mesh(me)
        bm.free()
        s.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def transform(s, M):
        s.bm.transform(M)
        return s

    def verts(s):
        return [v.co.copy() for v in s.bm.verts]

    def obj(s, matrix=None, col=None):
        """Mesh object. Geometry is in the frame given by `matrix` (object placed at matrix, mesh stays local)."""
        bm = s.bm.copy()
        uv = bm.loops.layers.uv.new("UVMap")
        M = matrix if matrix is not None else Matrix()
        bm.transform(M)  # box projection in world space, 1 UV unit = 1 m (tiling fabric / carpet normals)
        bm.normal_update()
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for l in f.loops:
                c = l.vert.co
                l[uv].uv = (c.y, c.z) if ax == 0 else (c.x, c.z) if ax == 1 else (c.x, c.y)
        bm.transform(M.inverted())
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        me = bpy.data.meshes.new("SM_" + s.name)
        bm.to_mesh(me)
        bm.free()
        for m in s.mats:
            me.materials.append(MAT[m])
        o = bpy.data.objects.new(s.name, me)
        o.matrix_world = M
        (col or bpy.context.scene.collection).objects.link(o)
        return o


# ---------------------------------------------------------------- primitives
def t_box(size, bev, seg=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    bev = min(bev, min(size) * 0.45)
    if bev > 0.002:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bev, offset_type='OFFSET', segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return bm


def t_cyl(r, depth, seg=12, r2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=depth)
    return bm


def t_sphere(r, u=10, v=6):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    return bm


def t_torus(R, r, n=24, m=8):
    bm = bmesh.new()
    rings = []
    for i in range(n):
        a = 2 * math.pi * i / n
        rings.append([bm.verts.new(((R + r * math.cos(2 * math.pi * j / m)) * math.cos(a), (R + r * math.cos(2 * math.pi * j / m)) * math.sin(a),
                                    r * math.sin(2 * math.pi * j / m))) for j in range(m)])
    for i in range(n):
        a, b = rings[i], rings[(i + 1) % n]
        for j in range(m):
            bm.faces.new((a[j], b[j], b[(j + 1) % m], a[(j + 1) % m]))
    return bm


def t_prism(outline, depth):
    """Flat extruded outline (list of (x, z)) with thickness `depth` along Y, centred."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, -depth / 2, z)) for x, z in outline]
    f = bm.faces.new(vs)
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.y += depth
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


RX90 = Matrix.Rotation(math.radians(90), 4, 'X')  # cylinder axis Z → along Y (faces forward / back)
RY90 = Matrix.Rotation(math.radians(90), 4, 'Y')  # cylinder axis Z → along X


def box(g, mat, center, size, bev=0.02, seg=2, rotx=0.0, pivot=None):
    """size = (x, f, u) metres; rotx degrees about the lateral axis, positive = top leans BACK (+Y)."""
    if rotx and pivot is not None:
        M = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(-rotx), 4, 'X') @ Matrix.Translation(center - pivot)
    else:
        M = Matrix.Translation(center)
    g.add(t_box(size, bev, seg), mat, M)


# ================================================================ INTERIOR PARTS (local frames)
def seat(name="Seat_Bucket", w=0.52, D=0.5, seatH=0.3, T=0.75, lean=13.0, heads=1, posts=True, seg=3):
    """Bucket seat / bench. Origin = floor under the cushion centre; cushion top at z = seatH.
    T = back + headrest height above the cushion pivot; heads = 0 / 1 / 2 headrests."""
    g = G(name)
    top = seatH
    box(g, "Fabric_Seat", P(0, 0.01, top - 0.065), (w, D, 0.13), bev=0.045, seg=seg)
    for sx in (-1, 1):  # cushion bolsters
        box(g, "Fabric_Seat", P(sx * (w / 2 - 0.045), 0, top - 0.04), (0.09, D * 0.92, 0.12), bev=0.04, seg=2)
    ped_h = max(0.03, seatH - 0.13)
    box(g, "Plastic_Dark", P(0, 0, ped_h / 2), (w * 0.72, D * 0.8, ped_h), bev=0.012, seg=1)
    for sx in (-1, 1):  # seat rails
        box(g, "Rubber_Black", P(sx * w * 0.3, 0, 0.012), (0.03, D * 0.95, 0.024), bev=0.006, seg=1)
    pv = P(0, -D / 2 + 0.07, top - 0.05)
    hr = clamp(T * 0.2, 0.09, 0.15)
    gap = 0.035 if heads else 0.0
    Hb = max(0.22, T - (hr + gap if heads else 0))
    box(g, "Fabric_Seat", pv + Vector((0, 0, Hb / 2)), (w, 0.12, Hb), bev=0.05, seg=seg, rotx=lean, pivot=pv)
    for sx in (-1, 1):  # back bolsters
        box(g, "Fabric_Seat", pv + Vector((sx * (w / 2 - 0.04), -0.03, Hb * 0.42)), (0.08, 0.1, Hb * 0.8), bev=0.035, seg=2, rotx=lean, pivot=pv)
    if heads:
        hw = min(0.26, w * 0.52) if heads == 1 else min(0.24, w * 0.22)
        for hx in ([0.0] if heads == 1 else [-w * 0.3, w * 0.3]):
            box(g, "Fabric_Seat", pv + Vector((hx, 0.01, Hb + gap + hr / 2)), (hw, 0.1, hr), bev=0.04, seg=seg, rotx=lean, pivot=pv)
            if posts:
                R = Matrix.Rotation(math.radians(-lean), 4, 'X')
                for px in (-hw * 0.28, hw * 0.28):
                    c = pv + Vector((hx + px, 0.0, Hb + gap / 2))
                    g.add(t_cyl(0.008, gap + 0.04, 6), "Plastic_Dark", Matrix.Translation(pv) @ R @ Matrix.Translation(c - pv))
    return g


WHEEL_R = 0.17
WHEEL_TILT = Matrix.Rotation(math.radians(112), 4, 'X')  # local +Z = column (forward-down), +Y = 12 o'clock


def steering_wheel(R=WHEEL_R):
    """Origin = hub centre. Local: +Z along the column away from the driver, +Y = 12 o'clock, +X = driver's left.
    Hands: 10 o'clock (+X) / 2 o'clock (-X) on the rim → hand_points()."""
    g = G("SteeringWheel")
    g.add(t_torus(R, 0.022, 28, 8), "Rubber_Black")
    g.add(t_cyl(0.055, 0.05, 16), "Plastic_Dark", Matrix.Translation((0, 0, 0.01)))
    g.add(t_cyl(0.03, 0.012, 12), "Rubber_Black", Matrix.Translation((0, 0, -0.02)))  # horn badge
    for a in (0, 180, 270):  # spokes: left (+X), right (-X), bottom (-Y = 6 o'clock)
        ang = math.radians(a)
        mid = Vector((math.cos(ang) * R / 2, math.sin(ang) * R / 2, 0.005))
        g.add(t_box((R - 0.03, 0.04, 0.018), 0.008, 1), "Plastic_Dark", Matrix.Translation(mid) @ Matrix.Rotation(ang, 4, 'Z'))
    return g


def hand_points(R=WHEEL_R):
    a = math.radians(60)
    return {"Hand_L": Vector((R * math.sin(a), R * math.cos(a), 0)), "Hand_R": Vector((-R * math.sin(a), R * math.cos(a), 0))}


def steering_column(L=0.36):
    """Origin = hub; runs along local +Z (same frame as the wheel)."""
    g = G("SteeringColumn")
    g.add(t_cyl(0.038, L, 12, r2=0.05), "Plastic_Dark", Matrix.Translation((0, 0, L / 2 + 0.02)))
    g.add(t_box((0.03, 0.012, 0.09), 0.005, 1), "Plastic_Dark", Matrix.Translation((0.07, 0, 0.06)))   # stalks
    g.add(t_box((0.03, 0.012, 0.09), 0.005, 1), "Plastic_Dark", Matrix.Translation((-0.07, 0, 0.06)))
    return g


def instrument_cluster():
    """Hood + 2 round gauges. Origin = bottom-rear-centre of the hood (sits on the dash top, gauges face -f / the driver)."""
    g = G("InstrumentCluster")
    box(g, "Plastic_Dark", P(0, 0.08, 0.05), (0.38, 0.22, 0.1), bev=0.04, seg=3)
    for gx in (-0.085, 0.085):
        g.add(t_cyl(0.055, 0.02, 16), "Rubber_Black", Matrix.Translation(P(gx, -0.025, -0.065)) @ RX90)
        g.add(t_torus(0.055, 0.008, 16, 4), "Fabric_Seat", Matrix.Translation(P(gx, -0.035, -0.065)) @ RX90)
        box(g, "Fabric_Seat", P(gx + 0.015, -0.037, -0.055), (0.035, 0.004, 0.008), bev=0.0)  # needle
    return g


def vent_round(r=0.035):
    g = G("Vent_Round")
    g.add(t_cyl(r, 0.03, 12), "Rubber_Black", RX90)
    g.add(t_torus(r, 0.006, 12, 4), "Plastic_Dark", Matrix.Translation(P(0, -0.016, 0)) @ RX90)
    return g


def center_stack(h=0.4):
    """Origin = bottom-centre of the rear (driver-facing) face."""
    g = G("CenterStack")
    box(g, "Plastic_Dark", P(0, 0.05, h / 2), (0.26, 0.1, h), bev=0.025, seg=2)
    box(g, "Rubber_Black", P(0, -0.003, h - 0.09), (0.18, 0.02, 0.09), bev=0.008, seg=1)  # radio
    for i, kx in enumerate((-0.06, 0.06)):
        g.add(t_cyl(0.016, 0.02, 10), "Plastic_Dark", Matrix.Translation(P(kx, -0.012, h * 0.45)) @ RX90)  # knobs
    for vx in (-0.075, 0.075):
        box(g, "Rubber_Black", P(vx, -0.005, h + 0.02), (0.11, 0.03, 0.05), bev=0.01, seg=1)  # vents
    return g


def console(L=0.7, h=0.3):
    """Centre console between the seats. Origin = floor, rear end centre; extends forward by L."""
    g = G("Console")
    box(g, "Plastic_Dark", P(0, L / 2, h / 2), (0.22, L, h), bev=0.03, seg=2)
    box(g, "Fabric_Seat", P(0, 0.17, h + 0.04), (0.2, 0.32, 0.08), bev=0.03, seg=3)  # armrest lid
    for cx in (-0.045, 0.045):
        g.add(t_cyl(0.035, 0.012, 10), "Rubber_Black", Matrix.Translation(P(cx, 0.42, h + 0.0)))  # cup holders
    return g


def gear_lever():
    """Origin = boot base on the console top."""
    g = G("GearLever")
    g.add(t_cyl(0.045, 0.04, 10, r2=0.02), "Rubber_Black", Matrix.Translation((0, 0, 0.02)))
    g.add(t_cyl(0.011, 0.13, 8), "Plastic_Dark", Matrix.Translation((0, 0, 0.08)))
    g.add(t_sphere(0.032, 10, 6), "Rubber_Black", Matrix.Translation((0, 0, 0.15)))
    box(g, "Plastic_Dark", P(0, 0.0, 0.004), (0.11, 0.16, 0.008), bev=0.0)  # surround plate
    return g


def pedals():
    """Clutch-less pair (brake + accelerator), cartoon. Origin = floor centre between them; +f = towards the firewall."""
    g = G("Pedals")
    for x, w, h, nm in ((0.07, 0.1, 0.09, "brake"), (-0.09, 0.06, 0.14, "gas")):
        R = Matrix.Rotation(math.radians(-25), 4, 'X')
        g.add(t_box((w, 0.02, h), 0.008, 1), "Rubber_Black", Matrix.Translation(P(x, 0.0, 0.1 + h / 2)) @ R)
        g.add(t_box((0.02, 0.02, 0.18), 0.004, 1), "Plastic_Dark", Matrix.Translation(P(x, 0.08, 0.2 + h * 0.4)) @ Matrix.Rotation(math.radians(30), 4, 'X'))
    return g


def floor_mat(w=0.4, l=0.4):
    g = G("FloorMat")
    box(g, "Rubber_Black", P(0, 0, 0.006), (w, l, 0.012), bev=0.004, seg=1)
    for i in range(3):  # ribs
        box(g, "Rubber_Black", P(0, -l * 0.3 + i * l * 0.3, 0.014), (w * 0.8, 0.015, 0.006), bev=0.0)
    return g


def rear_view_mirror(stem=0.05):
    """Origin = mirror body centre; stem goes up by `stem` to the roof."""
    g = G("RearViewMirror")
    c = Vector()
    box(g, "Plastic_Dark", c, (0.2, 0.03, 0.06), bev=0.012, seg=2, rotx=-8, pivot=c)
    box(g, "Rubber_Black", c + Vector((0, 0.016, 0)), (0.18, 0.004, 0.045), bev=0.0)
    box(g, "Plastic_Dark", c + Vector((0, -0.01, 0.03 + stem / 2)), (0.02, 0.02, stem), bev=0.004, seg=1)
    return g


def air_freshener():
    """Hanging card on a string (generic drop shape — not any brand's silhouette). Origin = hook point (top)."""
    g = G("AirFreshener")
    g.add(t_cyl(0.0015, 0.09, 4), "Rubber_Black", Matrix.Translation((0, 0, -0.045)))
    pts = [(math.sin(t) * 0.032 * (1 - 0.35 * math.cos(t)), -0.09 - 0.045 * (1 - math.cos(t))) for t in [i * 2 * math.pi / 12 for i in range(12)]]
    g.add(t_prism(pts, 0.004), "Fabric_Seat")
    return g


def trash_bin_small():
    """Little car trash can (hangs on the console / sits in a footwell). Origin = bottom centre."""
    g = G("TrashBin_Small")
    g.add(t_cyl(0.07, 0.17, 12, r2=0.08), "Plastic_Dark", Matrix.Translation((0, 0, 0.085)))
    g.add(t_cyl(0.085, 0.02, 12), "Rubber_Black", Matrix.Translation((0, 0, 0.18)))
    box(g, "Rubber_Black", P(0, 0, 0.192), (0.06, 0.03, 0.01), bev=0.0)  # lid flap
    return g


def tissue_box():
    """Origin = bottom centre (sits on the rear shelf / dash)."""
    g = G("TissueBox")
    box(g, "Fabric_Seat", P(0, 0, 0.045), (0.22, 0.12, 0.09), bev=0.01, seg=1)
    box(g, "Fabric_Seat", P(0, 0, 0.1), (0.05, 0.03, 0.03), bev=0.012, seg=2)  # tissue puff
    return g


def door_card(L=0.9, H=0.4, side=1, depth=0.095):
    """Inner door card. Origin = centre of the door's inner surface; panel grows inward (towards -side·X) by at most `depth`."""
    g = G("DoorCard")
    k = clamp(depth / 0.095, 0.25, 1.0)  # squeeze everything when the seats sit close to the door
    t = 0.025 * k
    X = lambda dx: -side * dx
    box(g, "Plastic_Dark", Vector((X(t / 2), 0, 0)), (t, L, H), bev=0.008 * k, seg=1)
    box(g, "Fabric_Carpet", Vector((X(t + 0.004 * k), 0.02, H * 0.08)), (0.008 * k, L * 0.7, H * 0.36), bev=0.003 * k, seg=1)
    box(g, "Plastic_Dark", Vector((X(t + 0.035 * k), L * 0.05, -H * 0.14)), (0.07 * k, L * 0.42, 0.045), bev=0.018 * k, seg=2)  # armrest
    box(g, "Rubber_Black", Vector((X(t + 0.012 * k), -L / 2 + L * 0.2, H * 0.22)), (0.025 * k, 0.12, 0.03), bev=0.008 * k, seg=1)  # handle
    g.add(t_cyl(min(0.07, H * 0.2), 0.012 * k, 16), "Rubber_Black", Matrix.Translation(Vector((X(t + 0.002), L / 2 - L * 0.22, -H * 0.33))) @ RY90)  # speaker
    box(g, "Plastic_Dark", Vector((X(t + 0.02 * k), 0, -H * 0.4)), (0.04 * k, L * 0.5, 0.06), bev=0.015 * k, seg=1)  # map pocket
    return g


INTERIOR_LIBRARY = {
    "Seat_Bucket": lambda: seat(),
    "Seat_Bench": lambda: seat("Seat_Bench", w=1.2, D=0.48, lean=10, heads=2, T=0.7, posts=False),
    "SteeringWheel": steering_wheel,
    "SteeringColumn": steering_column,
    "InstrumentCluster": instrument_cluster,
    "Vent_Round": vent_round,
    "CenterStack": center_stack,
    "Console": console,
    "GearLever": gear_lever,
    "Pedals": pedals,
    "FloorMat": floor_mat,
    "RearViewMirror": rear_view_mirror,
    "AirFreshener": air_freshener,
    "TrashBin_Small": trash_bin_small,
    "TissueBox": tissue_box,
    "DoorCard": door_card,
}


def export_glb(objs, root, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_yup=True, export_apply=True,
                              export_texcoords=True, export_normals=True, export_materials='EXPORT', export_cameras=False, export_lights=False)


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


# ---------------------------------------------------------------- Parts_Library.blend (link / append)
BLEND = os.path.join(PROJ, r"ArtSource\Blender")
LIBRARY_BLEND = os.path.join(BLEND, r"Parts\Parts_Library.blend")


def link_library(meshes, link=True):
    """Link MAT_* materials + the given SM_<part> meshes from Parts_Library.blend; MAT now points at the linked materials."""
    want_m = ["MAT_" + k for k in MAT_SPEC]
    with bpy.data.libraries.load(LIBRARY_BLEND, link=link, relative=False) as (src, dst):
        dst.materials = [m for m in src.materials if m in want_m]
        dst.meshes = [m for m in src.meshes if m in ["SM_" + n for n in meshes]]
    MAT.clear()
    for k in MAT_SPEC:
        MAT[k] = bpy.data.materials["MAT_" + k]
    return {m.name[3:]: m for m in dst.meshes}


def collection(name, parent=None, **props):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    par = parent or bpy.context.scene.collection
    if c.name not in par.children:
        par.children.link(c)
    for k, v in props.items():
        c[k] = v
    return c


def preview_setup(target, eye, lens=35, sun_rot=(40, 15, 30)):
    """Camera + sun + light-grey world so a .blend opens ready to look at (CAM_Preview / LGT_Sun)."""
    sc = bpy.context.scene
    col = collection("Preview")
    cam = bpy.data.objects.get("CAM_Preview")
    if cam is None:
        cam = bpy.data.objects.new("CAM_Preview", bpy.data.cameras.new("CAM_Preview"))
        col.objects.link(cam)
    cam.data.lens = lens
    cam.location = eye
    cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    if bpy.data.objects.get("LGT_Sun") is None:
        sun = bpy.data.objects.new("LGT_Sun", bpy.data.lights.new("LGT_Sun", 'SUN'))
        sun.data.energy = 3.0
        sun.rotation_euler = [math.radians(a) for a in sun_rot]
        col.objects.link(sun)
    w = bpy.data.worlds.get("WLD_Preview") or bpy.data.worlds.new("WLD_Preview")
    try:
        w.use_nodes = True
    except Exception:
        pass
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.78, 0.82, 0.88, 1)
    bg.inputs[1].default_value = 0.9
    sc.world = w
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1200, 800
    sc.view_settings.view_transform = 'Standard'
    return cam


def save_blend(path):
    bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups from the scripts
    for _ in range(3):
        bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path, relative_remap=True, compress=True)
