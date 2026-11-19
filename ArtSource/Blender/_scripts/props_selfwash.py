# G9 self-service car wash props. Every builder works in UNITY local coordinates through U(x, y, z) and sizes (W, H, D),
# matching the graybox in SelfWashBuilder (same pivot / front / node names) so the prefab can swap 1:1.
# Front = Unity −Z (Blender +Y → tile axis "+Y").
import math
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import U, PG

EXP = "Assets/_Project/Art/Models/Generated/SelfWash/{}.glb"


def B(g, x, y, z, w, h, d, uv=("sw", "grey"), bev=0.015, seg=2, rot=None):
    return g.box(U(x, y, z), (w, d, h), uv, bev, seg, rot)


def Cz(g, x, y, z, r, length, uv=("sw", "grey"), seg=16, r2=None):
    """Cylinder along Unity Z (= Blender Y)."""
    return g.cyl(U(x, y, z), r, length, uv, seg, "Y", r2)


def Cy(g, x, y, z, r, length, uv=("sw", "grey"), seg=16, r2=None):
    """Cylinder along Unity Y (up)."""
    return g.cyl(U(x, y, z), r, length, uv, seg, "Z", r2)


def Cx(g, x, y, z, r, length, uv=("sw", "grey"), seg=16):
    return g.cyl(U(x, y, z), r, length, uv, seg, "X")


def torus_z(g, x, y, z, R, r, uv=("metal",), n=20, m=6):
    """Ring facing Unity −Z (in the Unity XY plane)."""
    g.add(L.t_torus(R, r, n, m), uv, Matrix.Translation(U(x, y, z)) @ L.RX90)


def tube_u(g, pts_u, r, uv=("sw", "black"), seg=8):
    g.add(PL.tube([U(*p) for p in pts_u], r, seg), uv)


def arc(p0, p1, sag, n=10):
    """Points from p0 to p1 (Unity coords) sagging by `sag` metres in −Y."""
    out = []
    for i in range(n + 1):
        t = i / n
        p = [p0[k] + (p1[k] - p0[k]) * t for k in range(3)]
        p[1] -= sag * math.sin(math.pi * t)
        out.append(tuple(p))
    return out


# ------------------------------------------------------------------ 결제기 + 모드 버튼
def pay_terminal():
    g = PG("Body")
    B(g, 0, 0.04, 0, 0.5, 0.08, 0.35, ("tile", "hazard", "+Y", "dark"), bev=0.01)          # plinth (hazard front)
    B(g, 0, 0.75, 0.01, 0.46, 1.34, 0.31, ("sw", "blue"), bev=0.05, seg=3)                 # cabinet
    B(g, 0, 0.42, 0.01, 0.468, 0.06, 0.318, ("sw", "yellow"), bev=0.012)                   # yellow band
    B(g, 0, 1.46, -0.02, 0.52, 0.1, 0.37, ("sw", "navy"), bev=0.04, seg=3)                 # hood
    B(g, 0, 1.46, -0.206, 0.09, 0.075, 0.01, ("tile", "brand", "+Y", "navy"), bev=0.0)     # drop logo plate
    B(g, 0, 1.06, -0.16, 0.4, 0.62, 0.02, ("sw", "navy"), bev=0.012)                       # front panel
    tilt = Matrix.Rotation(math.radians(12), 4, 'X')
    B(g, 0, 1.24, -0.175, 0.34, 0.24, 0.025, ("sw", "dark"), bev=0.012, rot=tilt)          # screen bezel
    B(g, 0, 1.24, -0.19, 0.3, 0.2, 0.012, ("tile", "screen", "+Y", "dark"), bev=0.0, rot=tilt)
    for (x, y), t in zip([(-0.075, 1.0), (0.075, 1.0), (-0.075, 0.87), (0.075, 0.87)], ["btn_water", "btn_foam", "btn_wax", "btn_rinse"]):
        Cz(g, x, y, -0.185, 0.052, 0.035, ("tile", t, "+Y", "grey"), seg=20)
        torus_z(g, x, y, -0.172, 0.058, 0.007, ("metal",), 20, 4)
    B(g, 0, 0.7, -0.175, 0.32, 0.17, 0.022, ("tile", "coinpanel", "+Y", "grey"), bev=0.008)  # coin / card panel
    Cz(g, 0.15, 0.53, -0.172, 0.045, 0.02, ("sw", "yellow"), seg=16)                       # emergency stop
    Cz(g, 0.15, 0.53, -0.19, 0.035, 0.03, ("sw", "red"), seg=16)
    B(g, -0.236, 0.32, 0.01, 0.012, 0.22, 0.24, ("tile", "vent", "-X", "grey"), bev=0.004)  # side vent (Unity −X side)
    # wand holster on the +X side
    B(g, 0.26, 0.98, -0.02, 0.06, 0.16, 0.12, ("sw", "dark"), bev=0.02)
    Cx(g, 0.24, 1.08, -0.02, 0.018, 0.08, ("metal",), seg=10)
    for x in (-0.18, 0.18):                                                                 # anchor bolts
        for z in (-0.12, 0.12):
            Cy(g, x, 0.085, z, 0.012, 0.012, ("metal",), seg=8)
    return [("Body", g)]


# ------------------------------------------------------------------ 회전 붐 암 (천장)
def boom_arm():
    p = PG("Pivot")
    B(p, 0, -0.02, 0, 0.3, 0.04, 0.3, ("metal",), bev=0.008)                              # ceiling plate
    for x in (-0.11, 0.11):
        for z in (-0.11, 0.11):
            Cy(p, x, -0.045, z, 0.014, 0.012, ("sw", "dark"), seg=8)
    Cy(p, 0, -0.33, 0, 0.065, 0.58, ("metal",), seg=16)                                    # column
    Cy(p, 0, -0.64, 0, 0.11, 0.1, ("sw", "blue"), seg=20)                                  # bearing housing
    Cy(p, 0, -0.69, 0, 0.085, 0.03, ("sw", "dark"), seg=20)
    a = PG("Arm")  # origin at the swivel (Unity y −0.72); BoomSway rotates this node
    ay = -0.72
    Cy(a, 0, ay, 0, 0.075, 0.08, ("metal",), seg=16)                                       # swivel collar
    B(a, 0, ay - 0.01, 1.22, 0.09, 0.1, 2.72, ("metal",), bev=0.02)                        # main beam
    B(a, 0, ay - 0.11, 1.0, 0.04, 0.04, 1.9, ("metal",), bev=0.01)                         # lower chord
    for i in range(6):                                                                     # truss struts
        z0 = 0.15 + i * 0.32
        rot = Matrix.Rotation(math.radians(35 if i % 2 else -35), 4, 'X')
        B(a, 0, ay - 0.06, z0 + 0.08, 0.03, 0.13, 0.03, ("metal",), bev=0.005, rot=rot)
    B(a, 0, ay - 0.01, -0.22, 0.16, 0.14, 0.18, ("sw", "dark"), bev=0.03)                  # counterweight
    B(a, 0, ay + 0.0, 2.6, 0.13, 0.14, 0.1, ("sw", "blue"), bev=0.03)                      # end cap
    tube_u(a, [(0.0, ay + 0.06, -0.05), (0.0, ay + 0.065, 1.2), (0.0, ay + 0.06, 2.45), (0.0, ay + 0.02, 2.55)], 0.022)
    for z in (0.4, 1.0, 1.6, 2.2):                                                         # hose clips
        B(a, 0, ay + 0.06, z, 0.07, 0.03, 0.03, ("sw", "dark"), bev=0.006)
    Cy(a, 0, ay - 0.08, 2.55, 0.035, 0.07, ("metal",), seg=12)                             # rotary joint
    drop = [(0.0, ay - 0.12, 2.55), (0.0, ay - 0.4, 2.56), (0.02, ay - 0.7, 2.55), (0.0, ay - 0.86, 2.54)]
    tube_u(a, drop, 0.024)
    Cy(a, 0, ay - 0.9, 2.54, 0.032, 0.06, ("sw", "yellow"), seg=12)                        # quick coupler
    return [("Pivot", p), ("Arm", a, U(0, ay, 0))]


# ------------------------------------------------------------------ 폼브러시 거치대
def foam_brush_holder():
    g = PG("Holder")
    B(g, 0, 1.0, 0.1, 0.4, 0.52, 0.035, ("sw", "deepblue"), bev=0.015)                     # wall plate
    for x in (-0.16, 0.16):
        for y in (0.78, 1.22):
            Cz(g, x, y, 0.08, 0.012, 0.012, ("metal",), seg=8)
    B(g, 0, 1.17, 0.078, 0.13, 0.13, 0.012, ("tile", "btn_foam", "+Y", "white"), bev=0.004)  # foam sign
    B(g, 0, 1.0, 0.035, 0.2, 0.05, 0.1, ("metal",), bev=0.01)                              # cradle
    for x in (-0.07, 0.07):
        B(g, x, 1.03, -0.03, 0.025, 0.08, 0.04, ("metal",), bev=0.008)                     # cradle prongs
    B(g, 0, 0.3, 0.06, 0.26, 0.04, 0.12, ("metal",), bev=0.01)                             # drip shelf
    b = PG("Brush")
    Cy(b, 0, 0.78, -0.04, 0.022, 0.62, ("sw", "blue"), seg=12)                             # handle
    Cy(b, 0, 1.1, -0.04, 0.03, 0.06, ("sw", "dark"), seg=12)                               # grip end
    B(b, 0, 0.44, -0.04, 0.25, 0.07, 0.11, ("sw", "yellow"), bev=0.02)                     # head
    B(b, 0, 0.375, -0.04, 0.23, 0.07, 0.095, ("sw", "pink"), bev=0.03, seg=3)              # bristles
    for i, (x, z, r) in enumerate([(-0.07, -0.07, 0.03), (0.02, -0.08, 0.026), (0.08, -0.06, 0.022), (-0.02, -0.01, 0.024)]):
        b.add(L.t_sphere(r, 10, 6), ("sw", "white"), Matrix.Translation(U(x, 0.34, -0.04 + z * 0.4)))  # foam blobs
    tube_u(b, [(0, 1.13, -0.04), (0, 1.25, -0.05), (0.03, 1.33, 0.0), (0.05, 1.32, 0.07)], 0.016)  # hose to the wall
    return [("Holder", g), ("Brush", b)]


# ------------------------------------------------------------------ 호스 릴
def hose_reel_drum():
    s = PG("Bracket")
    B(s, 0, 0, 0.14, 0.44, 0.44, 0.025, ("sw", "dark"), bev=0.01)                          # wall plate
    Cz(s, 0, 0, 0.115, 0.05, 0.03, ("metal",), seg=16)
    B(s, 0, -0.17, 0.12, 0.06, 0.34, 0.03, ("metal",), bev=0.008)                          # strut
    d = PG("Drum")  # rotates about Unity Z through the origin
    Cz(d, 0, 0, 0.105, 0.3, 0.022, ("sw", "yellow"), seg=28)                               # back flange (solid)
    torus_z(d, 0, 0, -0.105, 0.285, 0.02, ("sw", "yellow"), 28, 6)                         # front flange: rim + spokes
    for k in range(5):
        a = math.radians(90 + k * 72)
        B(d, math.cos(a) * 0.2, math.sin(a) * 0.2, -0.105, 0.18, 0.035, 0.02, ("sw", "yellow"), bev=0.008,
          rot=Matrix.Rotation(a, 4, "Y"))
    Cz(d, 0, 0, 0, 0.13, 0.19, ("sw", "dark"), seg=20)                                     # core
    for i, z in enumerate((-0.07, -0.025, 0.02, 0.065)):                                   # wound hose
        torus_z(d, 0, 0, z, 0.2 - (i % 2) * 0.012, 0.024, ("sw", "black"), 24, 6)
    Cz(d, 0, 0, 0, 0.185, 0.17, ("sw", "black"), seg=24)
    Cz(d, 0, 0, -0.125, 0.06, 0.03, ("metal",), seg=16)                                    # hub cap
    B(d, 0.12, 0.0, -0.13, 0.24, 0.035, 0.02, ("metal",), bev=0.006)                       # crank arm
    Cz(d, 0.22, 0.0, -0.165, 0.018, 0.07, ("sw", "black"), seg=10)                         # crank grip
    h = PG("HoseOut")
    tube_u(h, [(0.0, -0.21, -0.02), (0.05, -0.3, -0.06), (0.06, -0.45, -0.1), (0.05, -0.6, -0.12)], 0.024)
    Cy(h, 0.05, -0.63, -0.12, 0.03, 0.05, ("metal",), seg=10)
    return [("Bracket", s), ("Drum", d), ("HoseOut", h)]


PROPS = {
    "SW_PayTerminal": dict(build=pay_terminal, size="0.5 × 1.5 × 0.35", folder="Props/CarWash", export=EXP.format("SW_PayTerminal")),
    "SW_BoomArm": dict(build=boom_arm, size="0.3 × 0.9 × 2.8 (graybox drop to −1.62)", folder="Props/CarWash", export=EXP.format("SW_BoomArm"), floor_z=-1.9),
    "SW_FoamBrushHolder": dict(build=foam_brush_holder, size="0.4 × 1.3 × 0.25", folder="Props/CarWash", export=EXP.format("SW_FoamBrushHolder"), wall_y=-0.12),
    "SW_HoseReelDrum": dict(build=hose_reel_drum, size="0.6 × 0.6 × 0.3", folder="Props/CarWash", export=EXP.format("SW_HoseReelDrum"), wall_y=-0.155, floor_z=-1.65),
}
