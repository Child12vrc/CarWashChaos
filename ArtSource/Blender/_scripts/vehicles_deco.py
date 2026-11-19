# Character parts for the Batch F #8 vehicles (added to Exterior_Parts after vehicles_v2.build_v2).
# Materials: vehicle ones registered by build_v2 in L.MAT ("Paint", "Rubber", "Rim", "HeadLens", "TailLens", "Metal")
# + the prop atlas (PG) for swatches / icons. Blender frame: −Y front, +X driver side.
import bpy, bmesh, math
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from parts_lib import G, box
from props_lib import PG


def _ext():
    return bpy.data.collections["Exterior_Parts"]


def _place_g(g, name, origin, parent=None):
    g.transform(Matrix.Translation(-origin))
    o = g.obj(Matrix.Translation(origin), _ext())
    o.name, o.data.name = name, "SM_" + name
    if parent:
        mw = o.matrix_world.copy()
        o.parent = parent
        o.matrix_world = mw
    return o


def _place_pg(pg, name, origin=Vector()):
    o = pg.obj(_ext(), name, Matrix.Translation(origin))
    o.data.name = "SM_" + name
    return o


def _sph(pg, c, r, uv, u=12, v=8, squash=1.0):
    pg.add(L.t_sphere(r, u, v), uv, Matrix.Translation(c) @ Matrix.Diagonal((1, 1, squash, 1)))


def _tube(pg, pts, r, uv, seg=8):
    pg.add(PL.tube([Vector(p) for p in pts], r, seg), uv)


# ------------------------------------------------------------------ V05 ice-cream truck
def v05(car, info):
    hw = car.b1.x
    c = PG("IceCream")  # giant cone on the box roof
    top = car.b1.z
    cone = L.t_cyl(0.32, 0.9, 16, r2=0.02)
    cone.transform(Matrix.Rotation(math.pi, 4, 'X'))
    c.add(cone, ("sw", "wood"), Matrix.Translation(Vector((0, 1.3, top + 0.55))))
    for i, (dz, col, r) in enumerate(((1.08, "pink", 0.34), (1.42, "white", 0.3), (1.7, "wood", 0.24))):
        _sph(c, Vector((0, 1.3, top + dz)), r, ("sw", col), 14, 9, 0.85)
    _sph(c, Vector((0.05, 1.27, top + 1.95)), 0.07, ("sw", "red"), 10, 6)                              # cherry
    for k in range(6):
        a = k * 1.1
        c.box(Vector((math.cos(a) * 0.22, 1.3 + math.sin(a) * 0.22, top + 1.25 + 0.05 * (k % 3))), (0.08, 0.025, 0.025), ("sw", ("yellow", "blue", "green")[k % 3]), 0.008, 1)
    c.box(Vector((0, 1.3, top + 0.06)), (0.5, 0.5, 0.12), ("sw", "dark"), 0.03, 2)                     # mount
    _place_pg(c, "IceCream")
    w = PG("ServingWindow")  # right side (Unity right = −X)
    x = -hw - 0.012
    w.box(Vector((x, 0.9, 1.75)), (0.03, 1.5, 0.85), ("sw", "dark"), 0.02, 2)
    w.box(Vector((x - 0.01, 0.9, 1.75)), (0.012, 1.36, 0.72), ("glass",), 0.0, 1)
    w.box(Vector((x - 0.15, 0.9, 1.3)), (0.3, 1.4, 0.05), ("metal",), 0.01, 1)                       # counter
    for k in range(8):                                                                                # awning
        w.box(Vector((x - 0.2, 0.25 + k * 0.19, 2.32)), (0.42, 0.19, 0.03), ("sw", "pink" if k % 2 else "white"), 0.0, 1,
              Matrix.Rotation(math.radians(-18), 4, 'Y'))
    w.box(Vector((x - 0.005, -0.0, 1.85)), (0.012, 0.4, 0.5), ("tile", "vending", "-X", "dark"), 0.0, 1)  # menu
    for z in (0.95, 2.45):                                                                            # pink stripes
        for s in (1, -1):
            w.box(Vector((s * (hw + 0.006), 1.15, z)), (0.012, 2.4, 0.12), ("sw", "pink"), 0.0, 1)
    _place_pg(w, "ServingWindow")
    sp = PG("Speaker")
    sp.add(L.t_cyl(0.12, 0.25, 12, r2=0.05), ("sw", "white"), Matrix.Translation(Vector((0, -1.0, 2.25))) @ Matrix.Rotation(math.radians(80), 4, 'X'))
    sp.box(Vector((0, -0.9, 2.12)), (0.06, 0.06, 0.14), ("metal",), 0.01, 1)
    _place_pg(sp, "Speaker")


# ------------------------------------------------------------------ V06 hearse
def v06(car, info):
    gx = car.g1.x
    p = PG("RearQuarters")  # padded rear side panels + chrome landau bars
    for s in (1, -1):
        p.box(Vector((s * (gx + 0.012), 1.85, 1.32)), (0.03, 1.4, 0.56), ("sw", "black"), 0.02, 2)
        pts = [Vector((s * (gx + 0.03), 1.3 + k * 0.06, 1.32 + 0.18 * math.sin(k * 0.35))) for k in range(12)]
        p.add(PL.tube(pts, 0.018, 6), ("metal",))
    for s in (1, -1):                                                                                 # chrome belt line
        p.box(Vector((s * (car.b1.x + 0.005), 0.0, car.beltY - 0.05)), (0.01, 5.0, 0.03), ("metal",), 0.0, 1)
    _place_pg(p, "RearQuarters")
    if info:  # coffin on the cabin floor behind the front seats
        cf = PG("Coffin")
        y0 = -info["fsz"] + 0.5
        y1 = -car.rearZ - 0.25
        L_ = y1 - y0
        cy = (y0 + y1) / 2
        z0 = car.floorY
        cf.box(Vector((0, cy, z0 + 0.2)), (0.62, L_, 0.36), ("sw", "wood"), 0.04, 2)
        cf.box(Vector((0, cy, z0 + 0.4)), (0.66, L_ + 0.04, 0.06), ("sw", "wood"), 0.03, 2)
        for s in (1, -1):
            for t in (-0.3, 0.0, 0.3):
                cf.box(Vector((s * 0.33, cy + t * L_, z0 + 0.22)), (0.03, 0.14, 0.03), ("sw", "yellow"), 0.008, 1)
        cf.add(L.t_sphere(0.09, 10, 6), ("sw", "white"), Matrix.Translation(Vector((0, cy - L_ * 0.25, z0 + 0.47))))  # flowers
        cf.add(L.t_sphere(0.07, 10, 6), ("sw", "pink"), Matrix.Translation(Vector((0.08, cy - L_ * 0.2, z0 + 0.46))))
        _place_pg(cf, "Coffin")


# ------------------------------------------------------------------ V07 garbage truck / B02 circus trailer (shared box truck)
def _truck_common(car, beacon_col):
    hw = car.b1.x
    b = PG("TruckDetails")
    b.add(L.t_cyl(0.1, 0.12, 12), ("sw", beacon_col), Matrix.Translation(Vector((0.5, -2.05, 2.55))))     # beacons
    b.add(L.t_cyl(0.1, 0.12, 12), ("sw", beacon_col), Matrix.Translation(Vector((-0.5, -2.05, 2.55))))
    for s in (1, -1):                                                                                   # cab steps
        b.box(Vector((s * (hw + 0.06), -1.42, 0.62)), (0.12, 0.26, 0.04), ("metal",), 0.01, 1)
        b.box(Vector((s * (hw + 0.06), 2.95, 1.6)), (0.04, 0.04, 1.1), ("metal",), 0.008, 1)          # rear grab bars
    b.box(Vector((0, 3.1, 0.62)), (car.b1.x * 2 - 0.2, 0.3, 0.05), ("metal",), 0.01, 1)                  # rear step
    return b


def v07(car, info):
    b = _truck_common(car, "orange")
    hw = car.b1.x
    for s in (1, -1):
        for y in (-0.6, 0.3, 1.2, 2.1):
            b.box(Vector((s * (hw + 0.01), y, 1.95)), (0.03, 0.08, 2.1), ("sw", "green"), 0.01, 1)    # box ribs
        b.add(PL.tube([Vector((s * (hw - 0.15), 2.4, 2.9)), Vector((s * (hw - 0.15), 2.95, 1.4))], 0.06, 8), ("metal",))  # rams
    b.box(Vector((0, 3.01, 1.05)), (2.0, 0.02, 0.35), ("tile", "hazard", "+Y", "yellow"), 0.0, 1)
    b.box(Vector((0, 3.05, 1.55)), (1.8, 0.1, 0.5), ("sw", "black"), 0.02, 1)                           # hopper mouth
    for k in range(3):                                                                                  # bags poking out
        b.add(L.t_sphere(0.16, 10, 6), ("sw", ("black", "dark", "blue")[k]), Matrix.Translation(Vector((-0.5 + k * 0.5, 3.12, 1.62))))
    _place_pg(b, "TruckDetails")


def b02(car, info):
    b = _truck_common(car, "yellow")
    hw = car.b1.x
    top = 3.05
    for s in (1, -1):  # scalloped valance + bulbs along the top edges
        for k in range(14):
            y = -1.0 + k * 0.28
            t = bmesh.new()
            a = t.verts.new((s * (hw + 0.012), y, top - 0.05))
            bb = t.verts.new((s * (hw + 0.012), y + 0.28, top - 0.05))
            c = t.verts.new((s * (hw + 0.012), y + 0.14, top - 0.3))
            t.faces.new((a, bb, c) if s > 0 else (a, c, bb))
            b.add(t, ("sw", "white" if k % 2 else "yellow"))
            _sph(b, Vector((s * (hw + 0.03), y + 0.14, top + 0.02)), 0.045, ("sw", "yellow"), 8, 5)
        for k, (y, z) in enumerate(((-0.3, 2.2), (0.9, 1.7), (2.0, 2.3))):  # stars
            b.box(Vector((s * (hw + 0.008), y, z)), (0.01, 0.55, 0.55), ("tile", "btn_wax", "+X" if s > 0 else "-X", "yellow"), 0.0, 1)
        b.box(Vector((s * (hw + 0.006), 1.0, 1.15)), (0.012, 4.0, 0.16), ("sw", "yellow"), 0.0, 1)       # stripe
    _sph(b, Vector((0, 1.0, top + 0.45)), 0.4, ("sw", "yellow"), 16, 8, 0.6)                             # tent dome
    b.add(L.t_cyl(0.02, 0.5, 6), ("metal",), Matrix.Translation(Vector((0, 1.0, top + 0.8))))
    flag = bmesh.new()
    v = [flag.verts.new(p) for p in ((0, 1.0, top + 1.0), (0, 1.35, top + 0.92), (0, 1.0, top + 0.82))]
    flag.faces.new(v)
    b.add(flag, ("sw", "red"))
    _place_pg(b, "CircusDetails")


# ------------------------------------------------------------------ V08 tank
def v08(car, info):
    t = PG("Tracks")
    for s in (1, -1):
        x = s * 1.12
        for k in range(6):
            t.cyl(Vector((x, -1.9 + k * 0.78, 0.38)), 0.3, 0.32, ("sw", "dark"), 14, "X")             # road wheels
            t.cyl(Vector((x + s * 0.17, -1.9 + k * 0.78, 0.38)), 0.12, 0.03, ("metal",), 10, "X")
        t.cyl(Vector((x, -2.55, 0.75)), 0.25, 0.3, ("sw", "dark"), 12, "X")                          # sprocket / idler
        t.cyl(Vector((x, 2.5, 0.72)), 0.22, 0.3, ("sw", "dark"), 12, "X")
        path = []  # track loop (stadium) as link boxes
        for k in range(46):
            u = k / 46
            if u < 0.4:
                p = Vector((x, -2.3 + u / 0.4 * 4.6, 0.06))
            elif u < 0.5:
                a = (u - 0.4) / 0.1 * math.pi
                p = Vector((x, 2.3 + math.sin(a) * 0.36, 0.42 - math.cos(a) * 0.36))
            elif u < 0.9:
                p = Vector((x, 2.3 - (u - 0.5) / 0.4 * 4.6, 0.95))
            else:
                a = (u - 0.9) / 0.1 * math.pi
                p = Vector((x, -2.3 - math.sin(a) * 0.4, 0.95 - (1 - math.cos(a)) * 0.445))
            path.append(p)
        for k, p in enumerate(path):
            q = path[(k + 1) % len(path)]
            d = q - p
            rot = d.to_track_quat('Y', 'Z').to_matrix().to_4x4()
            t.add(L.t_box((0.42, max(0.04, d.length * 0.9), 0.06), 0.01, 1), ("sw", "black"), Matrix.Translation((p + q) / 2) @ rot)
        t.box(Vector((x, 0.0, 1.27)), (0.46, 5.1, 0.05), ("sw", "dark"), 0.01, 1)                       # fender
    _place_pg(t, "Tracks")
    br = G("Barrel")
    pivot = Vector((0, -0.75, 1.6))
    box(br, "Paint", pivot + Vector((0, -0.1, 0)), (0.5, 0.3, 0.32), bev=0.06, seg=2)                 # mantlet
    br.add(L.t_cyl(0.075, 2.3, 14), "Metal", Matrix.Translation(pivot + Vector((0, -1.3, 0))) @ Matrix.Rotation(math.radians(90), 4, 'X'))
    br.add(L.t_cyl(0.12, 0.3, 14), "Rubber", Matrix.Translation(pivot + Vector((0, -2.4, 0))) @ Matrix.Rotation(math.radians(90), 4, 'X'))
    _place_g(br, "Barrel", pivot)
    d = PG("TankDetails")
    for s in (1, -1):
        d.box(Vector((s * 0.96, 0.25, 1.55)), (0.01, 0.4, 0.4), ("tile", "btn_wax", "+X" if s > 0 else "-X", "white"), 0.0, 1)  # star
    d.add(L.t_cyl(0.008, 1.6, 6), ("sw", "black"), Matrix.Translation(Vector((-0.55, 0.9, 2.65))))       # antenna
    for k in range(2):
        d.box(Vector((-0.45 + k * 0.32, 2.55, 1.0)), (0.26, 0.15, 0.4), ("sw", "green"), 0.03, 2)        # jerry cans
    d.box(Vector((-0.4, 0.4, 1.86)), (0.3, 0.3, 0.06), ("sw", "dark"), 0.02, 1)                          # loader hatch
    _place_pg(d, "TankDetails")


# ------------------------------------------------------------------ B03 swamp airboat
def b03(car, info):
    tr = PG("Trailer")
    for s in (1, -1):
        tr.box(Vector((s * 0.55, -0.3, 0.72)), (0.1, 5.4, 0.12), ("metal",), 0.01, 1)                    # rails
        tr.box(Vector((s * 1.0, -0.35, 0.8)), (0.3, 1.9, 0.04), ("sw", "dark"), 0.01, 1)                 # fenders
    tr.add(PL.tube([Vector((0.55, -2.9, 0.72)), Vector((0, -3.5, 0.66)), Vector((-0.55, -2.9, 0.72))], 0.05, 8), ("metal",))  # tongue
    tr.cyl(Vector((0, -3.55, 0.6)), 0.06, 0.12, ("sw", "dark"), 10, "Z")
    for y in (-1.5, 0.6):
        tr.box(Vector((0, y, 0.85)), (1.1, 0.12, 0.12), ("sw", "dark"), 0.01, 1)                        # bunks
    _place_pg(tr, "Trailer")
    f = PG("Deck")
    for s in (1, -1):  # driver tower + passenger bench + bow rail
        f.box(Vector((s * 0.3, 0.95, 2.0)), (0.05, 0.05, 1.0), ("metal",), 0.01, 1)
    f.box(Vector((0, 0.95, 2.55)), (0.75, 0.55, 0.12), ("sw", "white"), 0.04, 2)                       # driver seat
    f.box(Vector((0, 1.2, 2.85)), (0.7, 0.1, 0.5), ("sw", "white"), 0.04, 2)
    f.box(Vector((0, -0.4, 1.75)), (1.5, 0.5, 0.18), ("sw", "white"), 0.04, 2)                        # bench
    f.box(Vector((0, -0.2, 2.0)), (1.5, 0.1, 0.35), ("sw", "white"), 0.04, 2)
    f.add(PL.tube([Vector((0.9, -2.0, 1.55)), Vector((0.6, -2.7, 1.75)), Vector((-0.6, -2.7, 1.75)), Vector((-0.9, -2.0, 1.55))], 0.025, 8), ("metal",))
    f.box(Vector((0, 1.85, 1.85)), (0.7, 0.6, 0.6), ("sw", "dark"), 0.06, 2)                           # engine
    _place_pg(f, "Deck")
    cage = PG("FanCage")
    c = Vector((0, 2.35, 2.35))
    for r in (0.95, 0.7):
        cage.add(L.t_torus(r, 0.022, 28, 6), ("metal",), Matrix.Translation(c + Vector((0, 0.15, 0))) @ L.RX90)
        cage.add(L.t_torus(r, 0.022, 28, 6), ("metal",), Matrix.Translation(c - Vector((0, 0.15, 0))) @ L.RX90)
    for k in range(12):
        a = 2 * math.pi * k / 12
        cage.add(L.t_box((0.02, 0.32, 0.02), 0.0, 1), ("metal",), Matrix.Translation(c + Vector((math.cos(a) * 0.95, 0, math.sin(a) * 0.95))))
    for s in (1, -1):
        cage.box(Vector((s * 0.55, 2.3, 1.75)), (0.06, 0.06, 1.0), ("metal",), 0.01, 1)                   # struts
        cage.box(Vector((s * 0.3, 2.62, 1.95)), (0.04, 0.35, 0.8), ("sw", "red"), 0.01, 1)               # rudders
    _place_pg(cage, "FanCage")
    pr = PG("Propeller")  # spins about Unity Z (Blender Y)
    for a in (0, math.pi):
        pr.box(c + Vector((math.cos(a) * 0.4, -0.02, math.sin(a) * 0.4)), (0.8, 0.03, 0.14), ("sw", "wood"), 0.02, 2,
               Matrix.Rotation(-a, 4, 'Y'))
    pr.cyl(c, 0.1, 0.12, ("sw", "dark"), 12, "Y")
    _place_pg(pr, "Propeller", c)


# ------------------------------------------------------------------ B04 crashed UFO
def b04(car, info):
    body = bpy.data.objects["Body"]
    tilt = Matrix.Translation((0, 0, 0.75)) @ Matrix.Rotation(math.radians(12), 4, 'X') @ Matrix.Rotation(math.radians(-6), 4, 'Y')
    u = PG("UfoDetails")
    for k in range(16):  # rim lights
        a = 2 * math.pi * k / 16
        u.add(L.t_sphere(0.07, 8, 5), ("sw", "yellow" if k % 2 else "sky"), tilt @ Matrix.Translation((math.cos(a) * 2.28, math.sin(a) * 2.28, 0.42)))
    for k, (a, broken) in enumerate(((0.5, False), (2.6, False), (4.6, True))):  # landing legs
        top = tilt @ Vector((math.cos(a) * 1.3, math.sin(a) * 1.3, 0.1))
        foot = Vector((math.cos(a) * 1.75, math.sin(a) * 1.75, 0.0 if not broken else 0.35))
        u.add(PL.tube([top, (top + foot) / 2 + Vector((0, 0, 0.05)), foot], 0.05, 8), ("metal",))
        if not broken:
            u.add(L.t_cyl(0.14, 0.05, 12), ("sw", "dark"), Matrix.Translation(foot + Vector((0, 0, 0.025))))
    u.add(L.t_cyl(0.015, 0.5, 6), ("metal",), tilt @ Matrix.Translation((0.3, -0.2, 1.85)))              # antenna
    u.add(L.t_sphere(0.05, 8, 5), ("sw", "red"), tilt @ Matrix.Translation((0.3, -0.2, 2.1)))
    seat = tilt @ Matrix.Translation((0, 0.15, 0.9))                                                      # pilot seat under the dome
    u.add(L.t_box((0.5, 0.45, 0.12), 0.04, 2), ("sw", "dark"), seat)
    u.add(L.t_box((0.5, 0.1, 0.5), 0.04, 2), ("sw", "dark"), seat @ Matrix.Translation((0, 0.2, 0.28)))
    u.add(L.t_box((0.6, 0.25, 0.2), 0.05, 2), ("sw", "grey"), tilt @ Matrix.Translation((0, -0.55, 0.95)))  # console
    for k in range(3):
        u.add(L.t_cyl(0.03, 0.02, 10), ("sw", ("green", "red", "sky")[k]), tilt @ Matrix.Translation((-0.15 + k * 0.15, -0.6, 1.06)))
    _place_pg(u, "UfoDetails")


DECO = {"V05": v05, "V06": v06, "V07": v07, "V08": v08, "B02": b02, "B03": b03, "B04": b04}
