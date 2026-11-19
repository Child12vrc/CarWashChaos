# Tools v2 (held items). Same node names / sockets / origin (= grip) as the v1 GLBs; forward = Unity +Z = Blender −Y.
# Materials: the legacy tool materials (Soap Blue, Dark Navy Rubber, Warm Yellow Lights, Rear Red Lights, Foam White Rims)
# so ToolBuilder's material copies keep their names, + MAT_PropMetal / MAT_PropAtlas for steel and printed details.
import math
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import PG
import vehicles_gen as VG

TOOLS = "Assets/_Project/Art/Models/Generated/Tools/{}_v2.glb"
UVN = ("sw", "grey")


def _legacy():
    for k, v in VG.LEGACY.items():
        PL.PMAT[k] = VG._mat(k, *v)


def T(g, tbm, mat, M=None):
    g.add(tbm, UVN if mat in VG.LEGACY else (("metal",) if mat == "PropMetal" else UVN), M, mat=mat)


def bx(g, c, s, mat, bev=0.01, seg=2, rot=None):
    T(g, L.t_box(s, bev, seg), mat, Matrix.Translation(Vector(c)) @ (rot or Matrix()))


def cy(g, c, r, d, mat, axis="Y", seg=14, r2=None):
    R = {"Y": Matrix.Rotation(math.radians(90), 4, 'X'), "X": L.RY90, "Z": Matrix()}[axis]
    T(g, L.t_cyl(r, d, seg, r2), mat, Matrix.Translation(Vector(c)) @ R)


RX = lambda a: Matrix.Rotation(math.radians(a), 4, 'X')


# ------------------------------------------------------------------ pressure washer gun
def washer_gun():
    _legacy()
    grip = PG("Grip")
    bx(grip, (0, 0.0, -0.025), (0.075, 0.085, 0.21), "Dark Navy Rubber", 0.025, 3, RX(-12))
    for k in range(3):                                                                     # finger grooves
        bx(grip, (0, -0.045, -0.07 + k * 0.045), (0.07, 0.012, 0.02), "Dark Navy Rubber", 0.006, 1, RX(-12))
    bx(grip, (0, 0.012, -0.135), (0.08, 0.1, 0.02), "Warm Yellow Lights", 0.008, 1)         # butt cap
    body = PG("GunBody")
    bx(body, (0, -0.1, 0.075), (0.11, 0.3, 0.12), "Warm Yellow Lights", 0.035, 3)
    bx(body, (0, -0.1, 0.14), (0.05, 0.24, 0.02), "Dark Navy Rubber", 0.008, 1)            # top rib
    for s in (1, -1):
        bx(body, (s * 0.056, -0.08, 0.075), (0.004, 0.12, 0.05), "Soap Blue", 0.0, 1)      # side badge
    bx(body, (0, 0.03, 0.08), (0.06, 0.04, 0.05), "Dark Navy Rubber", 0.01, 1)              # safety lever
    trig = PG("Trigger")
    bx(trig, (0, -0.075, -0.02), (0.022, 0.03, 0.12), "Rear Red Lights", 0.01, 2, RX(15))
    guard = PG("TriggerGuard")
    T(guard, L.t_torus(0.07, 0.008, 16, 4), "Warm Yellow Lights",
      Matrix.Translation(Vector((0, -0.075, -0.035))) @ L.RY90 @ Matrix.Diagonal((1, 1.0, 0.85, 1)))
    hose = PG("HoseConnector")
    cy(hose, (0, 0.07, 0.075), 0.026, 0.05, "PropMetal")                                    # swivel
    cy(hose, (0, 0.1, 0.075), 0.022, 0.03, "Dark Navy Rubber")                              # boot
    wand = PG("Wand")
    cy(wand, (0, -0.44, 0.075), 0.012, 0.42, "PropMetal", seg=10)                           # steel lance
    cy(wand, (0, -0.32, 0.075), 0.02, 0.14, "Soap Blue", seg=12)                            # 2nd-hand sleeve
    for k in range(5):
        T(wand, L.t_torus(0.021, 0.003, 12, 3), "Dark Navy Rubber", Matrix.Translation(Vector((0, -0.27 - k * 0.025, 0.075))) @ RX(90))
    noz = PG("Nozzle")
    cy(noz, (0, -0.66, 0.075), 0.022, 0.04, "PropMetal")                                    # quick coupler
    cy(noz, (0, -0.695, 0.075), 0.03, 0.035, "Soap Blue", seg=6)                            # hex tip
    cy(noz, (0, -0.716, 0.075), 0.012, 0.01, "Rear Red Lights", seg=10)                     # colour-coded orifice
    return [("Grip", grip), ("GunBody", body), ("Trigger", trig), ("TriggerGuard", guard), ("HoseConnector", hose),
            ("Wand", wand), ("Nozzle", noz), ("Socket_HoseIn", None, Vector((0, 0.11, 0.075))),
            ("Socket_Nozzle", None, Vector((0, -0.72, 0.075)))]


# ------------------------------------------------------------------ foam cannon
def foam_cannon():
    _legacy()
    grip = PG("Grip")
    cy(grip, (0, 0, 0), 0.034, 0.12, "Dark Navy Rubber", seg=12)
    for k in range(4):
        T(grip, L.t_torus(0.035, 0.004, 12, 3), "Dark Navy Rubber", Matrix.Translation(Vector((0, -0.04 + k * 0.027, 0))) @ RX(90))
    cy(grip, (0, 0.065, 0), 0.026, 0.02, "PropMetal")                                       # inlet
    head = PG("MixerHead")
    bx(head, (0, -0.13, 0.075), (0.1, 0.2, 0.1), "Warm Yellow Lights", 0.03, 3)
    cy(head, (0, -0.13, 0.14), 0.035, 0.03, "Rear Red Lights", "Z", 14)                     # ratio dial
    bx(head, (0, -0.13, 0.158), (0.008, 0.05, 0.008), "Foam White Rims", 0.0, 1)
    cy(head, (0.058, -0.1, 0.075), 0.015, 0.02, "PropMetal", "X")                           # fan knob
    cy(head, (0, -0.15, 0.02), 0.04, 0.03, "Warm Yellow Lights", "Z")                       # bottle collar
    bot = PG("Bottle")
    cy(bot, (0, -0.15, -0.14), 0.095, 0.27, "Soap Blue", "Z", 18)
    cy(bot, (0, -0.15, -0.08), 0.096, 0.12, "Foam White Rims", "Z", 18)                     # foam level band
    for k in range(4):
        T(bot, L.t_torus(0.096, 0.004, 18, 3), "Soap Blue", Matrix.Translation(Vector((0, -0.15, -0.25 + k * 0.06))))
    bx(bot, (0, -0.245, -0.12), (0.06, 0.006, 0.08), "Foam White Rims", 0.0, 1)             # label
    noz = PG("Nozzle")
    cy(noz, (0, -0.26, 0.075), 0.03, 0.06, "PropMetal", seg=12)
    cy(noz, (0, -0.31, 0.075), 0.035, 0.05, "Soap Blue", seg=12, r2=0.045)                  # fan horn
    bx(noz, (0, -0.345, 0.075), (0.07, 0.01, 0.012), "Dark Navy Rubber", 0.0, 1)           # slot
    return [("Grip", grip), ("MixerHead", head), ("Bottle", bot), ("Nozzle", noz), ("Socket_Nozzle", None, Vector((0, -0.35, 0.075)))]


# ------------------------------------------------------------------ towel
def towel():
    _legacy()
    t = PG("FoldedTowel")
    t.box(Vector((0, -0.12, 0.024)), (0.34, 0.24, 0.05), ("tile", "towel", "+Z", "white"), 0.02, 2)
    up = PG("UpperFold")
    up.box(Vector((0, -0.14, 0.064)), (0.32, 0.2, 0.035), ("tile", "towel", "+Z", "white"), 0.017, 2)
    seam = PG("FoldSeam")
    cy(seam, (0, -0.035, 0.052), 0.018, 0.3, "Foam White Rims", "X", 10)                    # rolled fold
    hem = PG("HemStitches")
    for k in range(9):
        bx(hem, (-0.13 + k * 0.0325, -0.236, 0.078), (0.016, 0.006, 0.004), "Soap Blue", 0.0, 1)
    return [("FoldedTowel", t), ("UpperFold", up), ("FoldSeam", seam), ("HemStitches", hem),
            ("Socket_Nozzle", None, Vector((0, -0.24, 0.025)))]


# ------------------------------------------------------------------ shop vac
def shop_vac():
    _legacy()
    can = PG("Canister")
    c = Vector((-0.092, 0.405, 0.3))
    cy(can, c, 0.21, 0.38, "Rear Red Lights", "Z", 22)
    for z in (0.18, 0.42):
        T(can, L.t_torus(0.212, 0.008, 22, 3), "Rear Red Lights", Matrix.Translation(Vector((c.x, c.y, z))))
    bx(can, (c.x, c.y - 0.21, 0.3), (0.12, 0.01, 0.1), "Foam White Rims", 0.0, 1)            # label
    cy(can, (c.x + 0.2, c.y - 0.05, 0.2), 0.04, 0.05, "Dark Navy Rubber", "X", 12)          # hose port
    lid = PG("Lid")
    cy(lid, (c.x, c.y, 0.535), 0.225, 0.08, "Dark Navy Rubber", "Z", 22, 0.2)
    for s in (1, -1):
        bx(lid, (c.x + s * 0.225, c.y, 0.5), (0.02, 0.06, 0.07), "PropMetal", 0.006, 1)       # latches
    cy(lid, (c.x - 0.12, c.y + 0.1, 0.6), 0.04, 0.03, "Warm Yellow Lights", "Z", 12)       # power switch
    hd = PG("TopHandle")
    T(hd, L.t_torus(0.1, 0.018, 16, 6), "Dark Navy Rubber",
      Matrix.Translation(Vector((c.x, c.y, 0.58))) @ L.RY90 @ Matrix.Diagonal((1, 1, 1, 1)))
    cas = PG("Casters")
    for a in range(4):
        ang = math.pi / 4 + a * math.pi / 2
        p = c + Vector((math.cos(ang) * 0.17, math.sin(ang) * 0.17, 0))
        cy(cas, (p.x, p.y, 0.04), 0.04, 0.03, "Dark Navy Rubber", "X", 12)
        bx(cas, (p.x, p.y, 0.1), (0.03, 0.03, 0.06), "PropMetal", 0.005, 1)
    hose = PG("Hose")  # corrugated: tube + rings
    pts = [Vector((0.15, 0.36, 0.2)), Vector((0.3, 0.2, 0.28)), Vector((0.25, -0.05, 0.38)), Vector((0.05, -0.3, 0.3)), Vector((-0.08, -0.46, 0.13))]
    hb = PL.tube(pts, 0.035, 10)
    T(hose, hb, "Dark Navy Rubber")
    for i in range(len(pts) - 1):
        for k in range(4):
            p = pts[i].lerp(pts[i + 1], (k + 0.5) / 4)
            d = (pts[i + 1] - pts[i]).normalized()
            T(hose, L.t_torus(0.037, 0.006, 10, 3), "Dark Navy Rubber", Matrix.Translation(p) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4())
    noz = PG("Nozzle")
    bx(noz, (-0.092, -0.55, 0.12), (0.06, 0.17, 0.05), "Soap Blue", 0.012, 2, RX(-15))      # crevice tool
    bx(noz, (-0.092, -0.632, 0.12), (0.07, 0.01, 0.018), "Dark Navy Rubber", 0.0, 1)
    return [("Canister", can), ("Lid", lid), ("TopHandle", hd), ("Casters", cas), ("Hose", hose), ("Nozzle", noz),
            ("Socket_Nozzle", None, Vector((-0.092, -0.635, 0.12)))]


# ------------------------------------------------------------------ net pole
def net_pole():
    _legacy()
    grip = PG("Grip")
    cy(grip, (0, 0.0, 0), 0.024, 0.28, "Dark Navy Rubber", seg=12)
    cy(grip, (0, 0.15, 0), 0.028, 0.02, "Warm Yellow Lights", seg=12)                       # end cap
    pole = PG("Pole")
    cy(pole, (0, -0.3, 0), 0.016, 0.3, "PropMetal", seg=10)
    cy(pole, (0, -0.9, 0), 0.013, 1.1, "Warm Yellow Lights", seg=10)                        # telescopic section
    cy(pole, (0, -0.45, 0), 0.022, 0.05, "Dark Navy Rubber", seg=12)                        # lock collar
    cy(pole, (0, -1.44, 0.0), 0.016, 0.03, "PropMetal", seg=10)
    hoop = PG("Hoop")
    T(hoop, L.t_torus(0.23, 0.012, 28, 6), "Soap Blue", Matrix.Translation(Vector((0, -1.45, 0.22))) @ RX(90))
    bx(hoop, (0, -1.45, 0.0), (0.03, 0.03, 0.04), "Soap Blue", 0.006, 1)                     # yoke
    net = PG("Netting")
    bag = L.t_cyl(0.22, 0.27, 16, r2=0.06)
    T(net, bag, "Foam White Rims", Matrix.Translation(Vector((0, -1.585, 0.22))) @ RX(90))
    for k in range(4):                                                                     # mesh rings
        r = 0.22 - k * 0.04
        T(net, L.t_torus(r, 0.004, 16, 3), "Dark Navy Rubber", Matrix.Translation(Vector((0, -1.48 - k * 0.06, 0.22))) @ RX(90))
    return [("Grip", grip), ("Pole", pole), ("Hoop", hoop), ("Netting", net), ("Socket_Capture", None, Vector((0, -1.45, 0.22)))]


def _t(fn, name, size):
    return dict(build=fn, size=size, folder="Props/Tools", export=TOOLS.format(name), view=(-0.8, -1.0, 0.6))


PROPS = {
    "T_pressure_washer_gun_v2": _t(washer_gun, "T_pressure_washer_gun", "0.12 × 0.29 × 0.83"),
    "T_foam_cannon_v2": _t(foam_cannon, "T_foam_cannon", "0.21 × 0.45 × 0.42"),
    "T_towel_v2": _t(towel, "T_towel", "0.34 × 0.09 × 0.24"),
    "T_shop_vac_v2": _t(shop_vac, "T_shop_vac", "0.45 × 0.68 × 1.27"),
    "T_net_pole_v2": _t(net_pole, "T_net_pole", "0.47 × 0.47 × 1.87"),
}
