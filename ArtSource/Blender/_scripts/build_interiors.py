# Batch F #1 — car interiors, assembled per vehicle from Parts_Library.blend (fixed parts are LINKED meshes,
# size-dependent parts — seats, dash shell, floor, console, mirror stem, door cards — are generated to fit).
#   blender -b --factory-startup --python ArtSource/Blender/_scripts/build_interiors.py -- [V01 V02 ...] [norender]
# Writes ArtSource/Blender/Vehicles/<id>_<Name>.blend, exports its Export collection
# (Interior_<id>.glb, InteriorDoors_<id>.glb) and renders previews to docs/art_preview/.
# Cabin = the same box VehicleBuilder (M4.6 D3) derives from the vehicle GLB, so the game's width scale stays 1.0.
# Frame: Blender -Y = front (Unity +Z), +X = driver side / left (Unity -X), +Z up.
import bpy, bmesh, os, sys, math
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import parts_lib as L
from parts_lib import P, G, box, clamp
import export_all
import vehicles_v2
import vehicles_gen as VG
import vehicles_deco as VD

PROJ = L.PROJ
VEH = os.path.join(PROJ, r"Assets\_Project\Art\Models\Generated\Vehicles")
PREV = os.path.join(PROJ, r"docs\art_preview")
CARS = [("V01", "V01_hatchback", "V01_Hatchback"), ("V02", "V02_sedan", "V02_Sedan"), ("V03", "V03_pickup", "V03_Pickup"),
        ("V04", "V04_minivan", "V04_Minivan"), ("B01", "B01_monster_truck", "B01_MonsterTruck")]
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ONLY = [a for a in ARGS if a[:1] in "VB"]
RENDER = "norender" not in ARGS
MARGIN = 0.015
LINKED = ["SteeringWheel", "SteeringColumn", "InstrumentCluster", "Vent_Round", "GearLever", "Pedals", "FloorMat", "AirFreshener", "TrashBin_Small"]


class Car:
    def __init__(s, file, gen=None):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if gen:
            gen()  # Batch F #8: procedural v1-style node set
        else:
            bpy.ops.import_scene.gltf(filepath=os.path.join(VEH, file + ".glb"))
        s.objs = {o.name: o for o in bpy.data.objects}
        s.nocabin = gen is not None and gen.__name__.upper() in VG.NOCABIN
        body = L.collection("Body")
        socks = L.collection("Sockets")
        for o in list(bpy.data.objects):
            for c in list(o.users_collection):
                c.objects.unlink(o)
            (socks if o.name.startswith("Socket_") else body).objects.link(o)
            if o.type == 'MESH':
                o.data.name = "SM_" + o.name
        dg = bpy.context.evaluated_depsgraph_get()
        s.trees = {}
        for o in bpy.data.objects:
            if o.type != 'MESH' or o.name.startswith("Wheel"):
                continue
            bm = bmesh.new()
            bm.from_object(o, dg)
            bm.transform(o.matrix_world)
            s.trees[o.name] = BVHTree.FromBMesh(bm)
            bm.free()
        b0, b1 = s.bb(s.objs["Body"])
        if "Glass" in s.objs and not s.nocabin:
            g0, g1 = s.bb(s.objs["Glass"])
        else:  # no cabin: a nominal box in the upper body (only used for preview framing)
            g0 = Vector((b0.x + 0.2, b0.y + 0.6, b1.z - 0.5))
            g1 = Vector((b1.x - 0.2, b1.y - 0.6, b1.z))
        s.g0, s.g1, s.b0, s.b1 = g0, g1, b0, b1
        s.halfW = min(g1.x - g0.x, b1.x - b0.x) * 0.5 - 0.1
        s.beltY, s.roofY = g0.z, g1.z
        s.floorY = max(b0.z + 0.25, g0.z - 0.75)
        s.rearZ, s.frontZ = -g1.y + 0.15, -g0.y - 0.25
        s.yc, s.zc = (g0.y + g1.y) / 2, (g0.z + g1.z) / 2
        h = s.ray(Vector((0.3, s.yc, s.zc)), Vector((0, 0, -1)))
        s.deck = s.zc - h[1] if h else s.floorY

    @staticmethod
    def bb(o):
        c = [o.matrix_world @ Vector(k) for k in o.bound_box]
        return Vector([min(v[i] for v in c) for i in range(3)]), Vector([max(v[i] for v in c) for i in range(3)])

    def ray(s, o, d, maxd=50.0, only=None):
        best = None
        for k, t in s.trees.items():
            if only and k not in only:
                continue
            h = t.ray_cast(o, d, maxd)
            if h[0] is not None and (best is None or h[3] < best[1]):
                best = (k, h[3], h[0])
        return best

    def roof(s, x, f):
        h = s.ray(P(x, f, s.zc), Vector((0, 0, 1)))
        return s.zc + h[1] if h else s.roofY

    def front(s, x, u):
        h = s.ray(Vector((x, s.yc, u)), Vector((0, -1, 0)))
        return -(s.yc - h[1]) if h else 99

    def inside(s, p):
        if abs(p.x) > 1e-3 and s.ray(Vector((0, p.y, p.z)), Vector((math.copysign(1, p.x), 0, 0)), abs(p.x) + MARGIN):
            return False
        dy = p.y - s.yc
        if abs(dy) > 1e-3 and s.ray(Vector((p.x, s.yc, p.z)), Vector((0, math.copysign(1, dy), 0)), abs(dy) + MARGIN):
            return False
        if p.z > s.zc and s.ray(Vector((p.x, p.y, s.zc)), Vector((0, 0, 1)), p.z - s.zc + MARGIN):
            return False
        return True

    def bad(s, g, M=None):
        vs = g.verts()
        if M is not None:
            vs = [M @ v for v in vs]
        return sum(0 if s.inside(v) else 1 for v in vs)


def fit_seat(car, name, x, fc, w, seatH, D=0.5, lean=13.0, heads=1, maxT=0.8, posts=True):
    pv_f = fc - D / 2 + 0.07
    T = min(maxT, min(car.roof(x + dx, pv_f - 0.1) for dx in (-w / 2, 0, w / 2)) - 0.06 - (seatH - 0.05))
    M = Matrix.Translation(P(x, fc, car.floorY))
    while True:
        g = L.seat(name, w, D, seatH, T, lean, heads, posts, seg=3 if heads == 1 else 2)
        bad = car.bad(g, M)
        if bad == 0:
            return g, M, T
        g.bm.free()
        if T > 0.33:
            T -= 0.03
        elif lean > 4:
            lean -= 3
        else:
            return None, M, -bad


def assemble(car, cid, meshes):
    """Returns [(name, G or linked mesh, matrix, parent name)], report, info."""
    hw, fl, belt, fz, rz = car.halfW, car.floorY, car.beltY, car.frontZ, car.rearZ
    seatH = clamp((belt - fl) * 0.45, 0.25, 0.42)
    f_back = fz - 0.36          # dash rear face (VehicleBuilder dashZ - 0.18)
    fsz = fz - 0.18 - 0.75      # front seat centre (VehicleBuilder frontSeatZ → its trash spots)
    drvX = hw * 0.48
    sw = min(0.52, hw * 0.8)
    parts, rep = [], []

    def add(name, src, M, parent=None):
        parts.append((name, src, M, parent))

    def check(name, g, M=None):
        b = car.bad(g, M)
        if b:
            rep.append(f"!! {name} clips {b} verts")

    # floor + tunnel
    g = G("Floor")
    box(g, "Fabric_Carpet", P(0, (rz + fz) / 2 - 0.05, fl - 0.02), (hw * 2, fz - rz + 0.1, 0.04), bev=0.008, seg=1)
    box(g, "Fabric_Carpet", P(0, fsz + 0.35, fl + 0.05), (0.2, 0.9, 0.1), bev=0.04, seg=2)
    check("Floor", g)
    add("Floor", g, Matrix())

    # front seats
    for nm, sx in (("Seat_Driver", drvX), ("Seat_Passenger", -drvX)):
        g, M, T = fit_seat(car, nm, sx, fsz, sw, seatH)
        rep.append(f"{nm} back+headrest {T:.2f} m")
        add(nm, g, M)
    # rear rows
    rows = []
    if fsz - rz > 0.9:  # VehicleBuilder rule → its rear trash spots lie on this bench
        rows.append((max(rz + 0.4, fsz - 0.95), 0.48))
        if rows[0][0] - 0.95 - (rz - 0.1) > 0.35:
            rows.append((rows[0][0] - 0.95, 0.48))
    elif cid in VG.COMPACT_BENCH:  # compact bench (hatchback / sedan)
        rows.append((fsz - 0.25 - 0.17 - 0.17, 0.34))
    rows = rows[:VG.MAX_ROWS.get(cid, 9)]
    for i, (rf, D) in enumerate(rows):
        nm = "RearBench" if i == 0 else f"RearBench_{i + 1}"
        done = False
        for shift in (0.0, 0.05, 0.1, 0.15):
            for wcut in (0.12, 0.2, 0.3):
                g, M, T = fit_seat(car, nm, 0.0, rf + shift, hw * 2 - wcut, seatH, D=D, lean=10, heads=2, maxT=0.72, posts=False)
                if g is not None and T >= 0.33:
                    rep.append(f"{nm} f={rf + shift:.2f} w={hw * 2 - wcut:.2f} back+headrest {T:.2f} m")
                    add(nm, g, M)
                    rows[i] = (rf + shift, D)
                    done = True
                    break
                if g is not None:
                    g.bm.free()
            if done:
                break
        if not done:
            rep.append(f"{nm}: no room (skipped)")
            rows[i] = None
    rows = [r for r in rows if r]

    # rear panel / cab back wall
    rp_f = rz - 0.02 if not rows else min(rz - 0.02, min(r[0] for r in rows) - 0.3)
    for _ in range(25):
        g = G("RearPanel")
        box(g, "Fabric_Carpet", P(0, rp_f, (fl + belt - 0.03) / 2), (hw * 2 - 0.02, 0.04, belt - 0.03 - fl), bev=0.01, seg=1)
        if car.bad(g) == 0:
            break
        rp_f += 0.03
    add("RearPanel", g, Matrix())

    # dashboard shell (+ linked cluster / vents, generated centre stack)
    u_top = belt + 0.03
    f_front = min(min(car.front(x, u_top) for x in (-hw + 0.02, 0, hw - 0.02)) - 0.03, fz + 0.25)
    for _ in range(20):
        g = G("Dashboard")
        box(g, "Plastic_Dark", P(0, (f_back + f_front) / 2, u_top - 0.12), (hw * 2, f_front - f_back, 0.24), bev=0.06, seg=3)
        box(g, "Plastic_Dark", P(0, (f_back + 0.1 + f_front) / 2, (fl + u_top - 0.22) / 2), (hw * 2 - 0.02, f_front - f_back - 0.1, u_top - 0.22 - fl), bev=0.02, seg=1)
        box(g, "Rubber_Black", P(-drvX, f_back - 0.003, u_top - 0.17), (0.34, 0.008, 0.008), bev=0.0)  # glovebox seam
        Mc = Matrix.Translation(P(drvX, f_back - 0.02, u_top - 0.015))
        if car.bad(g) == 0 and car.bad(L.instrument_cluster(), Mc) == 0:
            break
        u_top -= 0.01
        f_front -= 0.02
    add("Dashboard", g, Matrix())
    add("InstrumentCluster", meshes["InstrumentCluster"], Mc, "Dashboard")
    for nm, sx in (("Vent_L", 1), ("Vent_R", -1)):
        add(nm, meshes["Vent_Round"], Matrix.Translation(P(sx * (hw - 0.08), f_back - 0.005, u_top - 0.08)), "Dashboard")
    sh = u_top - 0.11 - (fl + 0.12)
    add("CenterStack", L.center_stack(sh), Matrix.Translation(P(0, f_back - 0.07, fl + 0.12)), "Dashboard")
    rep.append(f"dash top {u_top:.2f} (deck {car.deck:.2f}) f {f_back:.2f}..{f_front:.2f}")

    # steering wheel + column (linked), hands
    hub = P(drvX, fz - 0.54, belt - 0.02)
    Mw = Matrix.Translation(hub) @ L.WHEEL_TILT
    check("SteeringWheel", L.steering_wheel(), Mw)
    add("SteeringColumn", meshes["SteeringColumn"], Mw)
    add("SteeringWheel", meshes["SteeringWheel"], Mw)

    # console + gear lever (linked)
    c_rear, c_front = fsz - 0.2, f_back + 0.02
    ch = seatH + 0.02
    cl = c_front - c_rear
    add("Console", L.console(cl, ch), Matrix.Translation(P(0, c_rear, fl)))
    add("GearLever", meshes["GearLever"], Matrix.Translation(P(0, c_rear + min(0.62, cl - 0.12), fl + ch)), "Console")

    # pedals, mats, trash bin (linked)
    add("Pedals", meshes["Pedals"], Matrix.Translation(P(drvX, f_back + 0.02, fl)))
    mf = (fsz + 0.27 + f_back + 0.08) / 2
    add("FloorMat_FL", meshes["FloorMat"], Matrix.Translation(P(drvX, mf, fl)))
    add("FloorMat_FR", meshes["FloorMat"], Matrix.Translation(P(-drvX, mf, fl)))
    for i, (rf, D) in enumerate(rows):
        for nm, sx in ((f"FloorMat_R{i + 1}L", 1), (f"FloorMat_R{i + 1}R", -1)):
            add(nm, meshes["FloorMat"], Matrix.Translation(P(sx * hw * 0.45, rf + D / 2 + 0.22, fl)))
    Mb = Matrix.Translation(P(-(hw - 0.13), fsz + 0.55, fl + 0.012))
    if car.bad(L.trash_bin_small(), Mb) == 0:
        add("TrashBin", meshes["TrashBin_Small"], Mb)

    # rear-view mirror (generated stem) + air freshener (linked)
    u_m = car.roofY - 0.1
    f_ws = car.front(0, u_m)
    for _ in range(15):
        mc = P(0, f_ws - 0.08, u_m - 0.02)
        stem = max(0.02, car.roof(0, f_ws - 0.06) - 0.02 - (mc.z + 0.03))
        g = L.rear_view_mirror(stem)
        Mm = Matrix.Translation(mc)
        if car.bad(g, Mm) == 0:
            break
        u_m -= 0.01
        f_ws -= 0.015
    add("RearViewMirror", g, Mm)
    Ma = Matrix.Translation(mc + Vector((0.05, 0, -0.03)))
    check("AirFreshener", L.air_freshener(), Ma)
    add("AirFreshener", meshes["AirFreshener"], Ma, "RearViewMirror")

    for name, src, M, _ in parts:
        if isinstance(src, G):
            check(name, src, M)
    info = dict(seatH=seatH, fsz=fsz, drvX=drvX, rows=[r[0] for r in rows], hub=hub, f_back=f_back)
    return parts, rep, info


def door_cards(car):
    res = []
    for dn in ("Door_L", "Door_R", "Door_Rear_L", "Door_Rear_R"):
        d = car.objs.get(dn)
        if not d:
            continue
        b0, b1 = car.bb(d)
        side = 1 if (b0.x + b1.x) > 0 else -1
        z0, z1 = b0.z + 0.07, min(b1.z, car.g0.z) - 0.035
        xi = []
        for y in [b0.y + (b1.y - b0.y) * t for t in (0.25, 0.5, 0.75)]:
            for z in (z0 + 0.05, (z0 + z1) / 2, z1 - 0.03):
                h = car.ray(Vector((0, y, z)), Vector((side, 0, 0)), 3, only=[dn])
                if h:
                    xi.append(abs(h[2].x))
        if not xi:
            continue
        inner = min(xi) - 0.004
        y0, y1 = b0.y + 0.07, b1.y - 0.07
        g = L.door_card(y1 - y0, z1 - z0, side)
        res.append(("DoorPanel_" + dn[5:], dn, g, Matrix.Translation(Vector((side * inner, (y0 + y1) / 2, (z0 + z1) / 2)))))
    return res


def run_nocabin(car, cid, file, blend_name):
    L.link_library(LINKED)
    L.collection("Export")
    _, rep, _ = vehicles_v2.build_v2(car, cid, file, 0.0, cabin=False)
    VD.DECO[cid](car, None)
    c = (car.b0 + car.b1) / 2
    tgt = Vector((0, c.y, (car.b0.z + car.b1.z) / 2))
    L.preview_setup(target=tgt, eye=tgt + Vector((car.b1.x + 3.2, -3.6, 1.8)), lens=30)
    blend = os.path.join(L.BLEND, "Vehicles", blend_name + ".blend")
    L.save_blend(blend)
    out = export_all.export_current()
    print(f"### {cid} (no cabin) → {os.path.relpath(blend, PROJ)}  exports {out}")
    for r in rep:
        print("    " + r)
    if RENDER:
        vehicles_v2.render_v2(car, cid, {}, tgt, PREV)


def run(cid, file, blend_name, gen=None):
    car = Car(file, gen)
    if car.nocabin:
        return run_nocabin(car, cid, file, blend_name)
    meshes = L.link_library(LINKED)
    icol = L.collection("Interior", export_path=f"Assets/_Project/Art/Models/Interiors/Interior_{cid}.glb", zero_root=1)
    isock = L.collection("Interior_Sockets", icol)
    dcol = L.collection("Interior_Doors", export_path=f"Assets/_Project/Art/Models/Interiors/InteriorDoors_{cid}.glb", zero_root=0)
    L.collection("Exterior_Parts")
    exp = L.collection("Export")
    for c in (icol, dcol):
        if c.name not in exp.children:
            exp.children.link(c)

    parts, rep, info = assemble(car, cid, meshes)
    fmid = (car.rearZ + car.frontZ) / 2
    root = bpy.data.objects.new(f"Interior_{cid}", None)
    root.empty_display_size = 0.3
    root.location = P(0, fmid, car.floorY)
    icol.objects.link(root)
    made = {}
    for name, src, M, parent in parts:
        if isinstance(src, G):
            if M == Matrix():  # built in vehicle space → origin = cabin floor centre (the interior root)
                src.transform(Matrix.Translation(-root.location))
                M = Matrix.Translation(root.location)
            o = src.obj(M, icol)
            o.name = name
            o.data.name = "SM_" + name
        else:
            o = bpy.data.objects.new(name, src)
            o.matrix_world = M
            icol.objects.link(o)
        made[name] = (o, parent)
    for hn, p in L.hand_points().items():
        e = bpy.data.objects.new(hn, None)
        e.empty_display_size = 0.03
        isock.objects.link(e)
        made[hn] = (e, None)
        e.matrix_world = made["SteeringWheel"][0].matrix_world @ Matrix.Translation(p)
        made[hn] = (e, "SteeringWheel")
    hips = bpy.data.objects.new("Socket_DriverHips", None)
    hips.empty_display_size = 0.1
    hips.location = P(info["drvX"], info["fsz"] - 0.06, car.floorY + info["seatH"] + 0.09)
    isock.objects.link(hips)
    made["Socket_DriverHips"] = (hips, None)
    bpy.context.view_layer.update()
    for name, (o, parent) in made.items():
        par = made[parent][0] if parent else root
        mw = o.matrix_world.copy()
        o.parent = par
        o.matrix_world = mw
    # door cards in the door's own frame (parent under Door_* in Unity with local pose 0)
    droot = bpy.data.objects.new(f"InteriorDoors_{cid}", None)
    dcol.objects.link(droot)
    dtri = 0
    door_objs = []
    for name, dn, g, M in door_cards(car):
        d = car.objs[dn]
        g.transform(d.matrix_world.inverted() @ M)
        o = g.obj(d.matrix_world.copy(), dcol)
        o.name = name
        o.data.name = "SM_" + name
        o.parent = droot
        o.matrix_world = d.matrix_world.copy()
        dtri += L.tris(o)
        door_objs.append((o, d))
    bpy.context.view_layer.update()

    _, rep2, doors_v2 = vehicles_v2.build_v2(car, cid, file, car.frontZ)  # exterior v2 in the same .blend
    rep += rep2
    if cid in VD.DECO:
        VD.DECO[cid](car, info)
    tri = sum(L.tris(o) for o, _ in made.values() if o.type == 'MESH')
    xs = [(o.matrix_world @ v.co).x for o, _ in made.values() if o.type == 'MESH' for v in o.data.vertices]
    tgt = P(0.0, fmid + 0.1, (car.floorY + car.beltY) / 2 + 0.1)
    L.preview_setup(target=tgt, eye=tgt + Vector((car.b1.x + 2.6, -1.9, 1.25)), lens=30)
    blend = os.path.join(L.BLEND, "Vehicles", blend_name + ".blend")
    L.save_blend(blend)
    out = export_all.export_current()

    print(f"### {cid} → {os.path.relpath(blend, PROJ)}  exports {out}")
    print(f"    tris {tri} (+door cards {dtri}); width {max(xs) - min(xs):.3f} vs cabin {2 * car.halfW:.3f}")
    print(f"    cabin halfW {car.halfW:.3f} floorY {car.floorY:.3f} beltY {car.beltY:.3f} roofY {car.roofY:.3f} deck {car.deck:.3f} "
          f"rearZ {car.rearZ:.3f} frontZ {car.frontZ:.3f} | seatH {info['seatH']:.3f} frontSeatZ {info['fsz']:.3f} rows {[round(r, 2) for r in info['rows']]}")
    hp = hips.matrix_world.translation
    print(f"    origin (vehicle, Unity) = (0, {car.floorY:.3f}, {fmid:.3f}); hips (Unity) = ({-hp.x:.3f}, {hp.z:.3f}, {-hp.y:.3f})")
    for r in rep:
        print("    " + r)

    if not RENDER:
        return
    vehicles_v2.render_v2(car, cid, doors_v2, tgt, PREV)
    bpy.data.collections["Reference_v1"].hide_render = False  # the interior previews below use the v1 car
    for c in ("Body", "Exterior_Parts", "Sockets"):
        bpy.data.collections[c].hide_render = True
    sc = bpy.context.scene
    for o, d in door_objs:  # preview only (not saved): cards ride on the doors, doors open
        mw = o.matrix_world.copy()
        o.parent = d
        o.matrix_world = mw
    for dn in ("Door_L", "Door_R", "Door_Rear_L", "Door_Rear_R"):
        d = car.objs.get(dn)
        if d:
            b0, b1 = car.bb(d)  # swing about the front (hinge) edge, rear end outward
            side = 1 if (b0.x + b1.x) > 0 else -1
            pv = Vector(((b0.x + b1.x) / 2, b0.y, 0))
            d.matrix_world = Matrix.Translation(pv) @ Matrix.Rotation(math.radians(-65 * side), 4, 'Z') @ Matrix.Translation(-pv) @ d.matrix_world
    me = bpy.data.meshes.new("SM_Ground")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=12)
    bm.to_mesh(me)
    gm = bpy.data.materials.new("MAT_Ground")
    gm.diffuse_color = (0.42, 0.43, 0.45, 1)
    gm.use_nodes = True
    next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.42, 0.43, 0.45, 1)
    me.materials.append(gm)
    sc.collection.objects.link(bpy.data.objects.new("Ground", me))
    cam = sc.camera
    for suffix, eye in (("open", Vector((car.b1.x + 2.6, -1.9, 1.25))), ("open_R", Vector((-(car.b1.x + 2.4), -1.5, 1.0)))):
        cam.location = tgt + eye
        cam.rotation_euler = (-eye).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(PREV, f"Interior_{cid}_{suffix}.png")
        bpy.ops.render.render(write_still=True)
    # top view with the roof skin cut off (copies; originals hidden)
    zcut = car.roofY - 0.04
    for n, o in list(car.objs.items()):
        if o.type != 'MESH':
            continue
        if n in ("Glass", "RoofLightBar") or n.startswith("Mirror"):
            o.hide_render = True
        elif n == "Body" or n.startswith("Door") or n == "Trunk":
            bm = bmesh.new()
            bm.from_mesh(o.data)
            bm.transform(o.matrix_world)
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, zcut), plane_no=(0, 0, 1), clear_outer=True)
            bm.transform(o.matrix_world.inverted())
            m2 = o.data.copy()
            bm.to_mesh(m2)
            c = bpy.data.objects.new(n + "_cut", m2)
            c.matrix_world = o.matrix_world.copy()
            sc.collection.objects.link(c)
            o.hide_render = True
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = (car.b1.y - car.b0.y) * 1.25
    cam.location = Vector((0, (car.b0.y + car.b1.y) / 2, car.b1.z + 6))
    cam.rotation_euler = (0, 0, math.radians(90))
    sc.render.filepath = os.path.join(PREV, f"Interior_{cid}_top.png")
    bpy.ops.render.render(write_still=True)


for cid, f, bn in CARS:
    if ONLY and cid not in ONLY:
        continue
    run(cid, f, bn)
for cid, gen in VG.GEN.items():
    if (ONLY and cid not in ONLY) or (not ONLY and "new" not in ARGS):
        continue
    f, bn = VG.FILES[cid]
    run(cid, f, bn, gen)
