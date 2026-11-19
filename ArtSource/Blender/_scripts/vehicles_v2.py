# Vehicle exterior v2 (called from build_interiors.run, inside the vehicle .blend):
#  - Body: the v1 body minus a cabin cavity (removes the deck under the glass) and the two side-door openings
#    → the interior shows through the windows and an open door. Same outer silhouette / bounds as v1.
#  - Door_L / Door_R: the body chunk that filled the opening (flush with the skin, real thickness, inner face),
#    6 mm seams, hinge origin = the v1 door origin; child DoorDetail_* (inner card + outer handle) and Mirror_* (on the door).
#  - Modular parts in the v1 bounds: Headlight_*, Taillight_*, Grille, Bumper_*, LicensePlate_*, Wipers, Wheel_* (+ RoofRack,
#    Antenna, Exhaust, FuelCap per car). Glass / Trunk / Socket_* / extra nodes are copied unchanged (Glass loses its
#    horizontal faces so no glass sheet lies across the cabin) → the game's cabin box and the interior GLBs still fit.
#  - UV0 + UV2 (TEXCOORD_1) regenerated on Body without overlaps (dirt painting).
import bpy, bmesh, math, os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
import parts_lib as L
import props_lib as PL
from parts_lib import G, box

GAP = 0.006
VEH_DIR = "Assets/_Project/Art/Models/Generated/Vehicles"


def _link(o, col):
    for c in list(o.users_collection):
        c.objects.unlink(o)
    col.objects.link(o)


def _world_copy(o, name, merge=True):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.transform(o.matrix_world)
    if merge:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    me = bpy.data.meshes.new("SM_" + name)
    bm.to_mesh(me)
    bm.free()
    for m in o.data.materials:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _box_obj(name, mn, mx, mat):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    c, s = (mn + mx) / 2, mx - mn
    for v in bm.verts:
        v.co = Vector((c.x + v.co.x * s.x, c.y + v.co.y * s.y, c.z + v.co.z * s.z))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _boolean(target, cutters, op):
    col = bpy.data.collections.new("TMP_Cutters")
    bpy.context.scene.collection.children.link(col)
    for c in cutters:
        _link(c, col)
        c.hide_render = True
    mod = target.modifiers.new("Bool", 'BOOLEAN')
    mod.operation = op
    mod.solver = 'EXACT'
    if len(cutters) == 1:
        mod.operand_type = 'OBJECT'
        mod.object = cutters[0]
    else:
        mod.operand_type = 'COLLECTION'
        mod.collection = col
    try:
        mod.use_hole_tolerant = True
    except Exception:
        pass
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(target.evaluated_get(dg))
    target.modifiers.remove(mod)
    old = target.data
    target.data = me
    me.name = old.name
    bpy.data.meshes.remove(old)
    for c in cutters:
        bpy.data.objects.remove(c)
    bpy.data.collections.remove(col)
    bm = bmesh.new()
    bm.from_mesh(target.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-5)
    bm.to_mesh(target.data)
    bm.free()
    return target


def _volume(o):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    v = abs(bm.calc_volume(signed=False))
    bm.free()
    return v


def _set_origin(o, M):
    """Keep the mesh where it is in the world, move the object frame to M."""
    o.data.transform(M.inverted() @ o.matrix_world)
    o.matrix_world = M


def _uv_body(o):
    me = o.data
    while len(me.uv_layers) < 2:
        me.uv_layers.new(name="UV2" if len(me.uv_layers) == 1 else "UVMap")
    me.uv_layers[0].name, me.uv_layers[1].name = "UVMap", "UV2"
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    for i in (1, 0):
        me.uv_layers.active_index = i
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.02, area_weight=0.0, correct_aspect=True, scale_to_bounds=True)
        bpy.ops.object.mode_set(mode='OBJECT')
    me.uv_layers.active_index = 0


def _uv2_overlap_check(o):
    """Rasterise UV2 triangles at 256² and count texels covered twice (0 = no overlap)."""
    import numpy as np
    me = o.data
    uv = me.uv_layers[1].data
    me.calc_loop_triangles()
    N = 256
    cov = np.zeros((N, N), np.int16)
    for t in me.loop_triangles:
        p = [Vector(uv[i].uv) * N for i in t.loops]
        x0, x1 = int(max(0, min(q.x for q in p))), int(min(N - 1, max(q.x for q in p)))
        y0, y1 = int(max(0, min(q.y for q in p))), int(min(N - 1, max(q.y for q in p)))
        if x1 < x0 or y1 < y0:
            continue
        ys, xs = np.mgrid[y0:y1 + 1, x0:x1 + 1] + 0.5
        (ax, ay), (bx, by), (cx, cy) = [(q.x, q.y) for q in p]
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-12:
            continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
        inside = (l1 > 0.01) & (l2 > 0.01) & (1 - l1 - l2 > 0.01)
        cov[y0:y1 + 1, x0:x1 + 1] += inside
    return int((cov > 1).sum()), int((cov > 0).sum())


# ---------------------------------------------------------------- part builders (G, vehicle materials registered in L.MAT)
def _lathe(profile, seg=24):
    """Revolve [(r, a)] around the X axis (a = along X). Returns bmesh (closed if the profile starts / ends at r=0)."""
    bm = bmesh.new()
    rings = []
    for r, a in profile:
        rings.append([bm.verts.new((a, r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg))) for k in range(seg)])
    for i in range(len(rings) - 1):
        for k in range(seg):
            q = (rings[i][k], rings[i][(k + 1) % seg], rings[i + 1][(k + 1) % seg], rings[i + 1][k])
            try:
                bm.faces.new(q)
            except ValueError:
                pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def wheel(R, W, side, chunky=False, hub_mat="Hub"):
    """Wheel in its local frame (origin = centre, axis X, outer face towards +side·X)."""
    g = G("Wheel")
    s = side
    w2 = W / 2
    rr = R * (0.55 if not chunky else 0.5)  # rim radius
    sh = min(0.035, W * 0.18)                 # shoulder rounding
    prof = [(rr * 0.98, -w2 * 0.92 * s), (R - sh, -w2 * s), (R - sh * 0.25, -(w2 - sh * 0.4) * s), (R, -(w2 - sh) * s),
            (R, -w2 * 0.35 * s), (R - 0.012, -w2 * 0.3 * s), (R - 0.012, -w2 * 0.2 * s), (R, -w2 * 0.15 * s),
            (R, w2 * 0.15 * s), (R - 0.012, w2 * 0.2 * s), (R - 0.012, w2 * 0.3 * s), (R, w2 * 0.35 * s),
            (R, (w2 - sh) * s), (R - sh * 0.25, (w2 - sh * 0.4) * s), (R - sh, w2 * s), (rr * 0.98, w2 * 0.92 * s)]
    if s < 0:
        prof = prof[::-1]
    g.add(_lathe(prof, 28 if not chunky else 32), "Tyre")
    if chunky:  # monster-truck tread blocks
        n = 18
        for k in range(n):
            a = 2 * math.pi * (k + 0.5) / n
            for off in (-1, 1):
                c = Vector((off * W * 0.22, math.cos(a + off * 0.08) * (R + 0.02), math.sin(a + off * 0.08) * (R + 0.02)))
                g.add(L.t_box((W * 0.36, 0.07, 0.14), 0.015, 1), "Tyre", Matrix.Translation(c) @ Matrix.Rotation(a, 4, 'X'))
    # rim: dish + spokes + hub
    dish = [(0.0, w2 * 0.55 * s), (rr * 0.35, w2 * 0.58 * s), (rr * 0.88, w2 * 0.66 * s), (rr, w2 * 0.8 * s),
            (rr, -w2 * 0.7 * s), (0.0, -w2 * 0.7 * s)]
    if s < 0:
        dish = dish[::-1]
    g.add(_lathe(dish, 24), "Rim")
    for k in range(5):
        a = 2 * math.pi * k / 5 + math.pi / 10
        c = Vector((w2 * 0.72 * s, math.cos(a) * rr * 0.55, math.sin(a) * rr * 0.55))
        g.add(L.t_box((0.03, rr * 0.75, rr * 0.2), 0.008, 1), "Rim", Matrix.Translation(c) @ Matrix.Rotation(a, 4, 'X'))
    g.add(L.t_cyl(rr * 0.3, 0.04, 16), hub_mat, Matrix.Translation((w2 * 0.76 * s, 0, 0)) @ L.RY90)
    for k in range(5):
        a = 2 * math.pi * k / 5
        g.add(L.t_cyl(rr * 0.05, 0.03, 6), "Metal", Matrix.Translation((w2 * 0.84 * s, math.cos(a) * rr * 0.2, math.sin(a) * rr * 0.2)) @ L.RY90)
    return g


def lamp(b0, b1, lens, accent, front=True):
    """Head / tail light filling the v1 bounds (b0, b1 world). Front lamps face -Y, rear +Y."""
    g = G("Lamp")
    c, s = (b0 + b1) / 2, b1 - b0
    d = -1 if front else 1
    box(g, "Rubber", c, (s.x, s.y, s.z), bev=min(0.03, s.z * 0.3), seg=2)
    box(g, lens, c + Vector((0, d * 0.006, 0)), (s.x - 0.02, s.y, s.z - 0.02), bev=min(0.035, s.z * 0.35), seg=3)
    if front:
        r = min(s.z, s.x) * 0.28
        for k, fx in enumerate((-0.25, 0.22)):
            g.add(L.t_cyl(r * (1.0 if k == 0 else 0.75), 0.012, 16), accent,
                  Matrix.Translation(c + Vector((fx * s.x, d * (s.y / 2 + 0.008), 0))) @ Matrix.Rotation(math.radians(90), 4, 'X'))
    else:
        box(g, accent, c + Vector((0, d * (s.y / 2 + 0.006), -s.z * 0.28)), (s.x * 0.8, 0.008, s.z * 0.18), bev=0.0)
    return g


def grille(b0, b1):
    g = G("Grille")
    c, s = (b0 + b1) / 2, b1 - b0
    box(g, "Rubber", c, (s.x, s.y, s.z), bev=min(0.02, s.z * 0.2), seg=2)
    n = 3
    for i in range(n):
        z = c.z - s.z * 0.3 + i * s.z * 0.3
        box(g, "Metal", Vector((c.x, b0.y - 0.004, z)), (s.x * 0.86, 0.012, s.z * 0.11), bev=0.004, seg=1)
    return g


def bumper(b0, b1, front=True):
    g = G("Bumper")
    c, s = (b0 + b1) / 2, b1 - b0
    box(g, "Rubber", c, (s.x, s.y, s.z), bev=min(0.045, s.z * 0.3, s.y * 0.45), seg=3)
    d = -1 if front else 1
    box(g, "Metal", Vector((c.x, c.y + d * (s.y / 2 + 0.002), b0.z + s.z * 0.18)), (s.x * 0.9, 0.006, 0.012), bev=0.0)  # trim line
    return g


def mirror(b0, b1, side):
    """Wing mirror inside the v1 bounds: arm from the door, painted housing, chrome glass facing back."""
    g = G("Mirror")
    c, s = (b0 + b1) / 2, b1 - b0
    hx = c.x + side * s.x * 0.15
    box(g, "Paint", Vector((hx, c.y, c.z)), (s.x * 0.7, s.y, s.z), bev=min(0.03, s.z * 0.35), seg=3)
    box(g, "Metal", Vector((hx, c.y + s.y / 2 + 0.002, c.z)), (s.x * 0.55, 0.006, s.z * 0.75), bev=0.0)
    box(g, "Rubber", Vector((c.x - side * s.x * 0.3, c.y, c.z - s.z * 0.2)), (s.x * 0.45, s.y * 0.35, s.z * 0.35), bev=0.01, seg=1)
    return g


# ---------------------------------------------------------------- main
def build_v2(car, cid, file_stem, cabin_front_z):
    """car = build_interiors.Car (v1 imported). Returns (collection, report lines, door objects)."""
    rep = []
    objs = dict(car.objs)
    v1 = L.collection("Reference_v1")
    for n, o in objs.items():
        o.name = n + "_v1"
        _link(o, v1)
    for c in ("Body", "Sockets"):
        cc = bpy.data.collections.get(c)
        if cc and not cc.objects:
            bpy.data.collections.remove(cc)
    body_c = L.collection("Body")
    ext_c = L.collection("Exterior_Parts")
    sock_c = L.collection("Sockets")
    v2 = L.collection("Vehicle_v2", L.collection("Export"), export_path=f"{VEH_DIR}/{file_stem}_v2.glb", zero_root=0)
    for c in (body_c, ext_c, sock_c):
        if c.name not in v2.children:
            v2.children.link(c)

    body1 = objs["Body"]
    bmat = body1.data.materials[0]
    mats = {m.name: m for m in bpy.data.materials}
    L.MAT["Paint"] = bmat
    L.MAT["Rubber"] = mats.get("Dark Navy Rubber", L.MAT["Rubber_Black"])
    L.MAT["Rim"] = mats.get("Foam White Rims", L.MAT["Plastic_Dark"])
    L.MAT["Hub"] = mats.get("Soap Blue", bmat)
    L.MAT["HeadLens"] = mats.get("Warm Yellow Lights", bmat)
    L.MAT["TailLens"] = mats.get("Rear Red Lights", bmat)
    L.MAT["Tyre"] = L.MAT["Rubber"]
    PL.prop_mats()
    L.MAT["Metal"] = PL.PMAT["PropMetal"]

    hw, fl, deck = car.halfW, car.floorY, car.deck
    g0, g1, b0, b1 = car.g0, car.g1, car.b0, car.b1
    outer = b1.x + 0.3

    # ---- Body: cavity + door openings
    body = _world_copy(body1, "Body")
    _link(body, body_c)
    cav_lo = fl - 0.045
    cutters = [_box_obj("CUT_Cabin", Vector((-(hw + 0.01), g0.y + 0.03, cav_lo)), Vector((hw + 0.01, g1.y - 0.08, deck + 0.03)), bmat)]
    openings = {}
    for dn in ("Door_L", "Door_R", "Door_Rear_L", "Door_Rear_R"):
        d = objs.get(dn)
        if not d:
            continue
        d0, d1 = car.bb(d)
        side = 1 if (d0.x + d1.x) > 0 else -1
        zlo = max(d0.z, cav_lo + 0.01)
        mn = Vector((hw + 0.005 if side > 0 else -outer, d0.y, zlo))
        mx = Vector((outer if side > 0 else -(hw + 0.005), d1.y, g0.z + 0.004))
        openings[dn] = (mn, mx, side, d)
        cutters.append(_box_obj("CUT_" + dn, mn, mx, bmat))
    _boolean(body, cutters, 'DIFFERENCE')

    # ---- Doors = body chunk in the opening (flush skin, thickness, inner face) + v1 window frame on the hatchback
    doors = {}
    for dn, (mn, mx, side, d1o) in openings.items():
        chunk = _world_copy(body1, dn)
        _link(chunk, body_c)
        cut = _box_obj("CUT_in_" + dn, Vector((mn.x, mn.y + GAP, mn.z + GAP)), Vector((mx.x, mx.y - GAP, mx.z)), bmat)
        _boolean(chunk, [cut], 'INTERSECT')
        vol = _volume(chunk)
        if cid == "V01":  # its v1 door also carries the side-window frame → keep it (union)
            old = _world_copy(d1o, "OLD_" + dn)
            _boolean(chunk, [old], 'UNION')
        _set_origin(chunk, d1o.matrix_world.copy())
        doors[dn] = chunk
        rep.append(f"{dn}: chunk volume {vol * 1000:.1f} L, verts {len(chunk.data.vertices)}")
        # inner card (behind the dash) + outer handle → child DoorDetail_*
        dd = G("DoorDetail_" + dn[5:])
        y_a = max(mn.y + 0.04, -(cabin_front_z - 0.36) + 0.03)
        y_b = mx.y - 0.05
        z_a, z_b = mn.z + 0.05, g0.z - 0.035
        if y_b - y_a > 0.2:
            card = L.door_card(y_b - y_a, z_b - z_a, side)
            dd.merge(card, Matrix.Translation(Vector((side * (hw + 0.005), (y_a + y_b) / 2, (z_a + z_b) / 2))))
        hy = mx.y - 0.18
        hz = mn.z + (g0.z - mn.z) * 0.78
        hx = car.ray(Vector((side * outer, hy, hz)), Vector((-side, 0, 0)), only=["Body"])
        sx = abs(hx[2].x) if hx else b1.x
        box(dd, "Rubber", Vector((side * (sx + 0.012), hy, hz)), (0.025, 0.16, 0.035), bev=0.01, seg=2)
        dd.transform(chunk.matrix_world.inverted())
        ob = dd.obj(chunk.matrix_world.copy(), body_c)
        ob.name = "DoorDetail_" + dn[5:]
        ob.data.name = "SM_" + ob.name
        mw = ob.matrix_world.copy()
        ob.parent = chunk
        ob.matrix_world = mw

    # ---- unchanged nodes (Glass without its horizontal faces)
    for n, o in objs.items():
        if o.type == 'EMPTY':
            e = bpy.data.objects.new(n, None)
            e.matrix_world = o.matrix_world.copy()
            e.empty_display_size = 0.1
            sock_c.objects.link(e)
        elif n in ("Glass", "Trunk", "CargoBed", "HayBales", "LiftedFrame", "Suspension", "RoofLightBar"):
            c = o.copy()
            c.data = o.data.copy()
            c.name = n
            c.data.name = "SM_" + n
            (body_c if n in ("Glass", "Trunk") or n.startswith("Door") else ext_c).objects.link(c)
            if n == "Glass":
                bm = bmesh.new()
                bm.from_mesh(c.data)
                bmesh.ops.delete(bm, geom=[f for f in bm.faces if abs(f.normal.z) > 0.9], context='FACES')
                bm.to_mesh(c.data)
                bm.free()

    # ---- modular exterior parts in the v1 bounds
    def place(g, name, col, origin=None, parent=None):
        M = Matrix.Translation(origin) if origin is not None else Matrix()
        if origin is not None:
            g.transform(Matrix.Translation(-origin))
        o = g.obj(M, col)
        o.name = name
        o.data.name = "SM_" + name
        if parent:
            mw = o.matrix_world.copy()
            o.parent = parent
            o.matrix_world = mw
        return o

    for n, o in objs.items():
        if o.type != 'MESH':
            continue
        lo, hi = car.bb(o)
        cen = (lo + hi) / 2
        if n.startswith("Headlight"):
            place(lamp(lo, hi, "HeadLens", "Rim", True), n, ext_c, cen)
        elif n.startswith("Taillight"):
            place(lamp(lo, hi, "TailLens", "Rim", False), n, ext_c, cen)
        elif n == "Grille":
            place(grille(lo, hi), n, ext_c, cen)
        elif n.startswith("Bumper"):
            front = n.endswith("Front")
            place(bumper(lo, hi, front), n, ext_c, cen)
            pw = min(0.52, (hi.x - lo.x) * 0.5)
            pl = PL.PG("Plate")
            py = lo.y - 0.005 if front else hi.y + 0.005
            pl.box(Vector((0, py, cen.z)), (pw, 0.01, 0.11 * pw / 0.52), ("tile", "plate", "-Y" if front else "+Y", "white"), bev=0.0)
            pl.obj(ext_c, "LicensePlate_" + ("Front" if front else "Rear"), Matrix.Translation(Vector((0, py, cen.z))))
        elif n.startswith("Mirror"):
            side = 1 if cen.x > 0 else -1
            par = doors.get("Door_L" if side > 0 else "Door_R")
            place(mirror(lo, hi, side), n, ext_c, cen, par)
        elif n.startswith("Wheel"):
            side = 1 if cen.x > 0 else -1
            R = (hi.z - lo.z) / 2
            W = hi.x - lo.x
            g = wheel(R, W, side, chunky=(cid == "B01"))
            g.transform(Matrix.Translation(o.matrix_world.translation))
            place(g, n, ext_c, o.matrix_world.translation.copy())

    # wipers on the windscreen base
    wz = g0.z + 0.035
    hits = [car.ray(Vector((x, car.yc, wz)), Vector((0, -1, 0)), only=["Glass"]) for x in (0.0,)]
    hits2 = car.ray(Vector((0, car.yc, wz + 0.12)), Vector((0, -1, 0)), only=["Glass"])
    if hits[0] and hits2:
        y0, y1 = hits[0][2].y, hits2[2].y
        ang = math.atan2(0.12, y1 - y0)  # windscreen slope
        R = Matrix.Rotation(-(math.pi / 2 - ang), 4, 'X')
        wg = G("Wipers")
        for cx in (0.3 * hw, -0.45 * hw):
            L_ = hw * 0.8
            c = Vector((cx, y0 - 0.012, wz))
            wg.add(L.t_box((L_, 0.012, 0.014), 0.004, 1), "Rubber", Matrix.Translation(c) @ R)
            wg.add(L.t_box((L_ * 0.92, 0.02, 0.008), 0.003, 1), "Rubber", Matrix.Translation(c + Vector((0, -0.006, 0.012))) @ R)
            wg.add(L.t_cyl(0.018, 0.03, 10), "Rubber", Matrix.Translation(Vector((cx + L_ / 2, y0 - 0.01, wz - 0.005))))
        place(wg, "Wipers", ext_c, Vector((0, y0, wz)))

    # roof rack (minivan / hatchback), antenna (sedan), exhaust + fuel cap (all)
    roof_z = b1.z
    if cid in ("V01", "V04"):
        top = [car.ray(Vector((0, y, b1.z + 1)), Vector((0, 0, -1)), only=["Body"]) for y in [b0.y + (b1.y - b0.y) * t / 40 for t in range(41)]]
        ys = [b0.y + (b1.y - b0.y) * t / 40 for t, h in enumerate(top) if h and h[2].z > b1.z - 0.03]
        if ys:
            ry0, ry1 = min(ys) + 0.1, max(ys) - 0.1
            rk = G("RoofRack")
            rx = car.ray(Vector((outer, (ry0 + ry1) / 2, b1.z - 0.02)), Vector((-1, 0, 0)), only=["Body"])
            rhw = (abs(rx[2].x) if rx else hw) - 0.1
            for sx in (-1, 1):
                box(rk, "Rubber", Vector((sx * rhw, (ry0 + ry1) / 2, roof_z + 0.035)), (0.04, ry1 - ry0, 0.03), bev=0.01, seg=2)
                for yy in (ry0 + 0.05, ry1 - 0.05):
                    box(rk, "Rubber", Vector((sx * rhw, yy, roof_z + 0.012)), (0.05, 0.08, 0.03), bev=0.008, seg=1)
            for yy in (ry0 + (ry1 - ry0) * 0.3, ry0 + (ry1 - ry0) * 0.7):
                box(rk, "Metal", Vector((0, yy, roof_z + 0.06)), (rhw * 2 + 0.06, 0.03, 0.02), bev=0.006, seg=1)
            place(rk, "RoofRack", ext_c, Vector((0, (ry0 + ry1) / 2, roof_z)))
    if cid == "V02":
        an = G("Antenna")
        an.add(L.t_cyl(0.012, 0.16, 8, r2=0.004), "Rubber", Matrix.Translation(Vector((0, g1.y - 0.15, b1.z + 0.07))) @ Matrix.Rotation(math.radians(-25), 4, 'X'))
        an.add(L.t_cyl(0.022, 0.015, 10), "Rubber", Matrix.Translation(Vector((0, g1.y - 0.15, b1.z + 0.004))))
        place(an, "Antenna", ext_c, Vector((0, g1.y - 0.15, b1.z)))
    br = objs.get("Bumper_Rear")
    if br:
        lo, hi = car.bb(br)
        ex = G("Exhaust")
        ex.add(L.t_cyl(0.035, 0.18, 12), "Metal", Matrix.Translation(Vector((-hw * 0.6, hi.y - 0.04, lo.z - 0.03))) @ Matrix.Rotation(math.radians(90), 4, 'X'))
        ex.add(L.t_cyl(0.026, 0.19, 12), "Rubber", Matrix.Translation(Vector((-hw * 0.6, hi.y - 0.035, lo.z - 0.03))) @ Matrix.Rotation(math.radians(90), 4, 'X'))
        place(ex, "Exhaust", ext_c, Vector((-hw * 0.6, hi.y, lo.z - 0.03)))
    # fuel cap on the passenger (right = −X) rear quarter
    wr = objs.get("Wheel_RR")
    if wr:
        lo, hi = car.bb(wr)
        fy, fz = (lo.y + hi.y) / 2, hi.z + 0.15
        h = car.ray(Vector((-outer, fy, fz)), Vector((1, 0, 0)), only=["Body"])
        if h and fz < g0.z:
            fc = G("FuelCap")
            fc.add(L.t_cyl(0.06, 0.01, 16), "Rubber", Matrix.Translation(h[2] + Vector((-0.006, 0, 0))) @ L.RY90)
            place(fc, "FuelCap", ext_c, h[2].copy())

    _uv_body(body)
    over, used = _uv2_overlap_check(body)
    rep.append(f"Body: tris {sum(len(p.vertices) - 2 for p in body.data.polygons)}, UV2 overlap texels {over} / {used}")
    v1.hide_render = True
    for o in v1.objects:
        o.hide_set(True)
    return v2, rep, doors


def render_v2(car, cid, doors, tgt, prev_dir):
    """Previews (not saved): front 3/4 closed, doors open, rear 3/4 → docs/art_preview/Vehicle_<id>_v2*.png."""
    sc = bpy.context.scene
    for c in ("Interior_Doors", "Reference_v1"):
        if bpy.data.collections.get(c):
            bpy.data.collections[c].hide_render = True
    gm = bpy.data.materials.new("MAT_PreviewGround")
    gm.use_nodes = True
    next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.42, 0.43, 0.45, 1)
    me = bpy.data.meshes.new("SM_PreviewGround")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=14)
    bm.to_mesh(me)
    me.materials.append(gm)
    ground = bpy.data.objects.new("PreviewGround", me)
    sc.collection.objects.link(ground)
    cam = sc.camera
    cam.data.type = 'PERSP'
    sc.render.resolution_x, sc.render.resolution_y = 1200, 800
    bx = car.b1.x

    def shot(suffix, eye, lens=30):
        cam.data.lens = lens
        cam.location = tgt + eye
        cam.rotation_euler = (-eye).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(prev_dir, f"Vehicle_{cid}_v2{suffix}.png")
        bpy.ops.render.render(write_still=True)

    shot("", Vector((bx + 2.9, -3.4, 1.5)), 32)
    shot("_rear", Vector((-(bx + 2.7), 3.4, 1.4)), 32)
    saved = {}
    for dn, d in [(k, v) for k, v in doors.items() if k in ("Door_L", "Door_R")]:
        side = 1 if d.matrix_world.translation.x > 0 else -1
        saved[d] = d.matrix_world.copy()
        o = d.matrix_world.translation.copy()
        d.matrix_world = Matrix.Translation(o) @ Matrix.Rotation(math.radians(-65 * side), 4, 'Z') @ Matrix.Translation(-o) @ d.matrix_world
    shot("_open", Vector((bx + 2.3, -1.0, 1.05)), 28)
    shot("_open_R", Vector((-(bx + 2.3), -1.0, 1.05)), 28)
    for d, m in saved.items():
        d.matrix_world = m
    bpy.data.objects.remove(ground)
    if bpy.data.collections.get("Interior_Doors"):
        bpy.data.collections["Interior_Doors"].hide_render = False
