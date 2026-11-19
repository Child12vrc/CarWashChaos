# Batch F #8: procedural "v1" bodies for the vehicles that only had graybox silhouettes (V05–V08, B02–B04).
# GEN[cid]() builds the same node set a v1 GLB import gives (Body solid, Glass, Door_L/R + Trunk with hinge origins,
# Wheel_*, Headlight_±1, Taillight_±1, Grille, Bumper_*, Mirror_*, Socket_*) so build_interiors / vehicles_v2 can treat
# them exactly like V01–B01. DECO[cid](car, info) adds the character parts afterwards (Exterior_Parts).
# Blender frame: −Y = front, +X = driver side (Unity −X), +Z up.
import bpy, bmesh, math
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from parts_lib import G, box

NOCABIN = {"V08", "B03", "B04"}  # no glass cabin → no Interior_<id>.glb, no door openings
COMPACT_BENCH = {"V01", "V02"}
MAX_ROWS = {"V06": 0, "V05": 0, "V07": 0, "B02": 0}
PAINT = {"V05": (0.96, 0.95, 0.92), "V06": (0.06, 0.06, 0.07), "V07": (0.25, 0.6, 0.3), "V08": (0.36, 0.42, 0.24),
         "B02": (0.86, 0.16, 0.18), "B03": (0.55, 0.5, 0.36), "B04": (0.72, 0.75, 0.8)}
LEGACY = {"Dark Navy Rubber": ((0.11, 0.14, 0.25), 0.8, 0.0, 1.0), "Foam White Rims": ((0.94, 0.97, 1.0), 0.4, 0.0, 1.0),
          "Soap Blue": ((0.24, 0.72, 0.96), 0.4, 0.0, 1.0), "Warm Yellow Lights": ((1.0, 0.79, 0.24), 0.3, 0.0, 1.0),
          "Rear Red Lights": ((1.0, 0.35, 0.31), 0.3, 0.0, 1.0), "Light Blue Glass": ((0.7, 0.86, 0.95), 0.05, 0.0, 0.35)}


def _mat(name, col, rough=0.5, metal=0.0, alpha=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*col, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    m.diffuse_color = (*col, alpha)
    return m


def _obj(name, bm, mats, origin=Vector()):
    bm.transform(Matrix.Translation(-origin))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("SM_" + name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    o.location = origin
    bpy.context.scene.collection.objects.link(o)
    return o


def _prism(bm, pts, x0, x1, bev=0.04):
    """Side profile [(y, z)] extruded from x0 to x1, bevelled → appended to bm."""
    t = bmesh.new()
    vs = [t.verts.new((x0, y, z)) for y, z in pts]
    f = t.faces.new(vs)
    r = bmesh.ops.extrude_face_region(t, geom=[f])
    for v in [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.x = x1
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    if bev > 0:
        bmesh.ops.bevel(t, geom=t.edges[:], offset=bev, offset_type='OFFSET', segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
    _merge(bm, t)


def _merge(bm, t):
    me = bpy.data.meshes.new("tmp")
    t.to_mesh(me)
    t.free()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def _cube(bm, c, s, bev=0.02):
    _merge(bm, _tb(c, s, bev))


def _tb(c, s, bev=0.02):
    t = L.t_box(s, bev, 2)
    t.transform(Matrix.Translation(c))
    return t


class Kit:
    """Builds the v1-style node set from a compact description."""

    def __init__(s, cid):
        s.cid = cid
        s.paint = _mat("Paint_" + cid, PAINT[cid], 0.45)
        s.m = {k: _mat(k, *v) for k, v in LEGACY.items()}

    def body(s, shells):
        bm = bmesh.new()
        for kind, *a in shells:
            if kind == "prism":
                _prism(bm, *a)
            elif kind == "box":
                _cube(bm, *a)
        return _obj("Body", bm, [s.paint])

    def glass(s, pts, hw):
        bm = bmesh.new()
        _prism(bm, pts, -hw, hw, 0.0)
        return _obj("Glass", bm, [s.m["Light Blue Glass"]])

    def door(s, name, side, y0, y1, z0, z1, x):
        bm = bmesh.new()
        _cube(bm, Vector((side * (x + 0.01), (y0 + y1) / 2, (z0 + z1) / 2)), (0.06, y1 - y0, z1 - z0), 0.015)
        return _obj(name, bm, [s.paint], Vector((side * x, y0, z0)))

    def trunk(s, c, size, hinge):
        bm = bmesh.new()
        _cube(bm, c, size, 0.02)
        return _obj("Trunk", bm, [s.paint], hinge)

    def wheel(s, name, x, y, R, W):
        bm = L.t_cyl(R, W, 20)
        bm.transform(L.RY90)
        return _obj(name, bm, [s.m["Dark Navy Rubber"]], Vector((x, y, R))) if False else _obj(name, _moved(bm, Vector((x, y, R))), [s.m["Dark Navy Rubber"]], Vector((x, y, R)))

    def part(s, name, c, size, mat, bev=0.015):
        bm = bmesh.new()
        _cube(bm, c, size, bev)
        return _obj(name, bm, [s.m[mat] if isinstance(mat, str) else mat])

    def lights(s, y_front, y_rear, hw, z_head, z_tail, head=(0.32, 0.18), tail=(0.2, 0.22)):
        for i in (-1, 1):
            s.part(f"Headlight_{i}", Vector((i * (hw - 0.3), y_front - 0.02, z_head)), (head[0], 0.05, head[1]), "Warm Yellow Lights")
            s.part(f"Taillight_{i}", Vector((i * (hw - 0.22), y_rear + 0.02, z_tail)), (tail[0], 0.05, tail[1]), "Rear Red Lights")

    def socket(s, name, p):
        e = bpy.data.objects.new(name, None)
        e.location = p
        bpy.context.scene.collection.objects.link(e)
        return e


def _moved(bm, v):
    bm.transform(Matrix.Translation(v))
    return bm


def _common(k, W, y_front, y_rear, z_bump, z_grille, z_head, z_tail, wheels, R, ww, doors, belt, trunk, interior):
    hw = W / 2
    k.lights(y_front, y_rear, hw, z_head, z_tail)
    k.part("Grille", Vector((0, y_front - 0.015, z_grille)), (min(0.8, W * 0.35), 0.03, 0.14), "Dark Navy Rubber")
    k.part("Bumper_Front", Vector((0, y_front - 0.06, z_bump)), (W - 0.06, 0.14, 0.2), "Dark Navy Rubber")
    k.part("Bumper_Rear", Vector((0, y_rear + 0.06, z_bump)), (W - 0.06, 0.14, 0.2), "Dark Navy Rubber")
    for n, x, y in wheels:
        k.wheel(n, x, y, R, ww)
    for side, nm in ((1, "L"), (-1, "R")):
        if doors:
            y0, y1, z0 = doors
            k.door("Door_" + nm, side, y0, y1, z0, belt, hw)
            k.part("Mirror_" + nm, Vector((side * (hw + 0.06), y0 + 0.1, belt + 0.07)), (0.09, 0.2, 0.13), k.paint)
            k.socket("Socket_Door_" + nm, Vector((side * hw, y0, z0)))
    if trunk:
        c, size, hinge = trunk
        k.trunk(c, size, hinge)
        k.socket("Socket_Trunk", hinge)
    k.socket("Socket_Interior", interior)


# ------------------------------------------------------------------ V05 ice-cream truck (L): cab + tall serving box
def v05():
    k = Kit("V05")
    W, R = 2.0, 0.38
    zb = 0.56  # body bottom: floor clears the wheel tops
    belt, roof = 1.18, 2.0
    k.body([("prism", [(-2.45, zb), (-2.45, 1.05), (-1.95, belt), (-0.08, belt), (-0.08, 2.55), (0.05, 2.68), (2.35, 2.68),
                       (2.45, 2.55), (2.45, zb)], -W / 2, W / 2, 0.05),
            ("box", Vector((0, -0.86, roof + 0.05)), (W - 0.04, 1.55, 0.1))])
    k.glass([(-1.95, belt), (-1.55, roof), (-0.1, roof), (-0.1, belt)], W / 2 - 0.05)
    _common(k, W, -2.45, 2.45, 0.66, 0.85, 0.98, 1.2, [("Wheel_FL", 0.85, -1.6), ("Wheel_FR", -0.85, -1.6), ("Wheel_RL", 0.85, 1.55),
            ("Wheel_RR", -0.85, 1.55)], R, 0.26, (-1.55, -0.2, 0.62), belt,
            (Vector((0, 2.47, 1.6)), (W - 0.2, 0.05, 1.7), Vector((0, 2.47, 2.45))), Vector((0, -1.0, belt - 0.2)))
    return k


# ------------------------------------------------------------------ V06 hearse (L): long low wagon
def v06():
    k = Kit("V06")
    W, R = 1.9, 0.34
    zb, belt, roof = 0.5, 1.02, 1.62
    k.body([("prism", [(-2.65, zb), (-2.65, 0.92), (-1.6, belt), (2.55, belt), (2.65, 0.95), (2.65, zb)], -W / 2, W / 2, 0.05),
            ("box", Vector((0, 0.72, roof + 0.045)), (W - 0.06, 3.7, 0.09))])
    k.glass([(-1.6, belt), (-1.08, roof), (2.55, roof), (2.6, belt)], W / 2 - 0.06)
    _common(k, W, -2.65, 2.65, 0.55, 0.78, 0.82, 0.88, [("Wheel_FL", 0.8, -1.75), ("Wheel_FR", -0.8, -1.75), ("Wheel_RL", 0.8, 1.6),
            ("Wheel_RR", -0.8, 1.6)], R, 0.24, (-1.05, -0.05, 0.55), belt,
            (Vector((0, 2.68, 1.3)), (W - 0.2, 0.05, 0.62), Vector((0, 2.68, 1.6))), Vector((0, 0.5, belt - 0.2)))
    return k


# ------------------------------------------------------------------ V07 garbage truck (XL): cab-over + compactor body
def _truck(cid, W, box_top, rear):
    k = Kit(cid)
    R = 0.5
    zb, belt, roof = 0.82, 1.5, 2.4
    k.body([("prism", [(-3.0, zb), (-3.0, belt), (-1.1, belt), (-1.1, box_top), (rear[0], box_top), (3.0, rear[1]),
                       (3.0, zb)], -W / 2, W / 2, 0.06),
            ("box", Vector((0, -1.95, roof + 0.05)), (W - 0.06, 1.72, 0.1))])
    k.glass([(-2.98, belt), (-2.8, roof), (-1.12, roof), (-1.12, belt)], W / 2 - 0.06)
    _common(k, W, -3.0, 3.0, 0.75, 1.05, 1.15, 1.2, [("Wheel_FL", 1.0, -2.1), ("Wheel_FR", -1.0, -2.1), ("Wheel_RL", 1.0, 1.25),
            ("Wheel_RR", -1.0, 1.25), ("Wheel_RL2", 1.0, 2.3), ("Wheel_RR2", -1.0, 2.3)], R, 0.38, (-2.6, -1.25, 0.9), belt,
            (Vector((0, 3.03, (box_top + zb) / 2 + 0.1)), (W - 0.24, 0.06, box_top - zb - 0.4), Vector((0, 3.03, box_top - 0.15))),
            Vector((0, -2.0, belt - 0.2)))
    return k


def v07():
    return _truck("V07", 2.4, 3.0, (2.7, 2.5))


def b02():
    return _truck("B02", 2.4, 3.05, (2.85, 2.95))


# ------------------------------------------------------------------ V08 army tank (no cabin)
def v08():
    k = Kit("V08")
    W = 2.0
    k.body([("prism", [(-2.5, 0.5), (-2.75, 0.85), (-2.05, 1.25), (2.4, 1.25), (2.6, 0.9), (2.45, 0.5)], -W / 2, W / 2, 0.05)])
    t = bmesh.new()  # turret (part of the dirtable body)
    tb = L.t_cyl(1.0, 0.6, 16, r2=0.85)
    tb.transform(Matrix.Translation((0, 0.25, 1.55)) @ Matrix.Diagonal((1.0, 1.15, 1.0, 1)))
    _merge(t, tb)
    body = bpy.data.objects["Body"]
    _merge_obj(body, t)
    k.trunk(Vector((0.35, 0.55, 1.88)), (0.5, 0.5, 0.06), Vector((0.35, 0.8, 1.86)))   # commander hatch (C09 turret pops out)
    k.socket("Socket_Trunk", Vector((0.35, 0.8, 1.86)))
    k.socket("Socket_Interior", Vector((0, 0.3, 1.0)))
    for i in (-1, 1):
        k.part(f"Headlight_{i}", Vector((i * 0.75, -2.45, 1.22)), (0.18, 0.12, 0.12), "Warm Yellow Lights")
        k.part(f"Taillight_{i}", Vector((i * 0.8, 2.47, 1.1)), (0.14, 0.04, 0.1), "Rear Red Lights")
    return k


def _merge_obj(o, bm):
    t = bmesh.new()
    t.from_mesh(o.data)
    bm.transform(o.matrix_world.inverted())
    _merge(t, bm)
    t.to_mesh(o.data)
    t.free()


# ------------------------------------------------------------------ B03 swamp airboat on a trailer (no cabin)
def b03():
    k = Kit("B03")
    W = 2.3
    k.body([("prism", [(-2.6, 0.95), (-2.95, 1.35), (-2.7, 1.5), (2.55, 1.5), (2.6, 0.95)], -W / 2, W / 2, 0.06)])
    k.socket("Socket_Interior", Vector((0, 0.0, 1.2)))
    for n, x, y in (("Wheel_FL", 1.0, -0.9), ("Wheel_FR", -1.0, -0.9), ("Wheel_RL", 1.0, 0.2), ("Wheel_RR", -1.0, 0.2)):
        k.wheel(n, x, y, 0.36, 0.24)
    for i in (-1, 1):
        k.part(f"Taillight_{i}", Vector((i * 0.95, 2.7, 0.62)), (0.16, 0.05, 0.1), "Rear Red Lights")
        k.part(f"Headlight_{i}", Vector((i * 0.7, -2.85, 1.38)), (0.16, 0.05, 0.1), "Warm Yellow Lights")
    return k


# ------------------------------------------------------------------ B04 crashed UFO (no cabin): tilted saucer + glass dome
def b04():
    k = Kit("B04")
    prof = [(0.05, 0.0), (1.2, 0.05), (2.25, 0.35), (2.3, 0.45), (1.6, 0.75), (0.9, 0.85), (0.05, 0.86)]
    bm = bmesh.new()
    rings = []
    seg = 32
    for r, z in prof:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    for a, b in zip(rings, rings[1:]):
        for i in range(seg):
            bm.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    tilt = Matrix.Translation((0, 0, 0.75)) @ Matrix.Rotation(math.radians(12), 4, 'X') @ Matrix.Rotation(math.radians(-6), 4, 'Y')
    bm.transform(tilt)
    _obj("Body", bm, [k.paint])
    dome = L.t_sphere(1.0, 24, 10)
    for v in dome.verts:
        if v.co.z < 0:
            v.co.z = 0
    dome.transform(tilt @ Matrix.Translation((0, 0, 0.84)) @ Matrix.Diagonal((0.95, 0.95, 0.75, 1)))
    _obj("Glass", dome, [k.m["Light Blue Glass"]])
    hatch_c = tilt @ Vector((0, 1.65, 0.62))
    k.trunk(hatch_c, (0.9, 0.08, 0.5), tilt @ Vector((0, 1.65, 0.86)))
    k.socket("Socket_Trunk", tilt @ Vector((0, 1.65, 0.86)))
    k.socket("Socket_Interior", tilt @ Vector((0, 0, 1.2)))
    return k


GEN = {"V05": v05, "V06": v06, "V07": v07, "V08": v08, "B02": b02, "B03": b03, "B04": b04}
FILES = {"V05": ("V05_ice_cream_truck", "V05_IceCreamTruck"), "V06": ("V06_hearse", "V06_Hearse"), "V07": ("V07_garbage_truck", "V07_GarbageTruck"),
         "V08": ("V08_tank", "V08_Tank"), "B02": ("B02_circus_trailer", "B02_CircusTrailer"), "B03": ("B03_swamp_airboat", "B03_SwampAirboat"),
         "B04": ("B04_crashed_ufo", "B04_CrashedUFO")}
