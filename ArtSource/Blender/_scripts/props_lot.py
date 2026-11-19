# Batch F #3 / #4 / #6: office set, lockers, day board, shop terminal, interior trash, dumpster, vent, animal cage.
# Unity local coords via U(); pivot bottom centre and front = −Z unless noted (LotBuilder rotates them to face the lot).
import math, bmesh
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import U, PG
from props_selfwash import B, Cz, Cy, Cx, torus_z, tube_u
from props_selfwash3 import torus_y, torus_x

LOT = "Assets/_Project/Art/Models/Generated/Lot/{}.glb"
PROPS_DIR = "Assets/_Project/Art/Models/Generated/Props/{}.glb"


# ------------------------------------------------------------------ office (8 × 6 m footprint, 3 m walls, front = −Z)
def office():
    w, d, h = 8.0, 6.0, 3.0
    s = PG("Shell")
    B(s, 0, 0.15, 0, w + 0.1, 0.3, d + 0.1, ("sw", "concrete"), bev=0.02)                        # plinth
    B(s, 0, h / 2, 0, w, h, d, ("sw", "cream"), bev=0.04, seg=2)                                 # walls
    B(s, 0, h + 0.15, 0, w + 0.12, 0.3, d + 0.12, ("sw", "white"), bev=0.03)                     # parapet
    B(s, 0, h + 0.02, 0, w - 0.2, 0.04, d - 0.2, ("sw", "grey"), bev=0.0)                        # roof membrane
    B(s, 0, 0.9, -d / 2 - 0.005, w - 0.4, 0.04, 0.02, ("sw", "deepblue"), bev=0.0)               # wainscot line
    for x in (-w / 2 + 0.15, w / 2 - 0.15):                                                      # corner trims
        for z in (-d / 2 + 0.15, d / 2 - 0.15):
            B(s, x, h / 2, z, 0.32, h, 0.32, ("sw", "white"), bev=0.02)
    f = PG("Front")
    # door (left) + big window (right) on the front, awning over both
    B(f, -2.4, 1.1, -3.02, 1.2, 2.2, 0.08, ("sw", "dark"), bev=0.02)                             # door frame
    gl = PG("Glass")
    B(gl, -2.4, 1.1, -3.04, 1.0, 2.0, 0.02, ("glass",), bev=0.0)
    B(f, -2.05, 1.05, -3.08, 0.05, 0.3, 0.05, ("metal",), bev=0.01)                              # door bar
    B(f, 1.0, 1.55, -3.02, 3.6, 1.5, 0.08, ("sw", "dark"), bev=0.02)                             # window frame
    B(gl, 1.0, 1.55, -3.04, 3.44, 1.36, 0.02, ("glass",), bev=0.0)
    for x in (-0.2, 1.0, 2.2):
        B(f, x, 1.55, -3.05, 0.05, 1.36, 0.04, ("sw", "dark"), bev=0.0)                          # mullions
    B(f, 1.0, 0.76, -3.12, 3.7, 0.06, 0.2, ("sw", "white"), bev=0.01)                            # sill
    # interior hint behind the window: blinds half down
    for k in range(6):
        B(f, 1.0, 2.15 - k * 0.07, -2.97, 3.4, 0.05, 0.01, ("sw", "white"), bev=0.0)
    aw = PG("Awning")
    for k in range(10):                                                                          # striped awning
        x = -3.6 + k * 0.72 + 0.36
        rot = Matrix.Rotation(math.radians(-20), 4, 'X')
        B(aw, x, 2.55, -3.4, 0.72, 0.03, 0.85, ("sw", "blue" if k % 2 else "white"), bev=0.0, rot=rot)
    B(aw, 0, 2.38, -3.8, 7.2, 0.18, 0.03, ("sw", "blue"), bev=0.005)                             # valance
    for x in (-3.5, 3.5):
        tube_u(aw, [(x, 2.75, -3.0), (x, 2.45, -3.78)], 0.015, ("metal",), 6)                    # arms
    sg = PG("Sign")                                                                              # blank name board (TextDecal)
    B(sg, 0, 2.82, -3.06, 3.0, 0.32, 0.06, ("sw", "navy"), bev=0.02)
    for x in (-2.8, 2.8):                                                                        # wall lamps
        Cz(sg, x, 2.2, -3.08, 0.08, 0.08, ("sw", "dark"), seg=10)
        Cz(sg, x, 2.2, -3.14, 0.06, 0.04, ("sw", "cream"), seg=10)
    r = PG("Roof")
    B(r, 2.2, 3.4, 1.2, 1.0, 0.6, 0.45, ("sw", "white"), bev=0.03)                               # rooftop AC
    B(r, 2.0, 3.4, 0.97, 0.45, 0.45, 0.006, ("tile", "fan", "+Y", "white"), bev=0.0)
    tube_u(r, [(-3.0, 3.05, 2.9), (-3.0, 3.6, 2.9)], 0.06, ("metal",), 8)                        # vent stack
    side = PG("Side")                                                                            # side window + downpipe + meter
    B(side, w / 2 + 0.02, 1.6, 0.8, 0.08, 1.0, 1.4, ("sw", "dark"), bev=0.02)
    B(gl, w / 2 + 0.04, 1.6, 0.8, 0.02, 0.86, 1.26, ("glass",), bev=0.0)
    tube_u(side, [(w / 2 + 0.06, 3.2, -2.85), (w / 2 + 0.06, 0.3, -2.85)], 0.05, ("sw", "grey"), 8)
    B(side, -w / 2 - 0.08, 1.4, -1.5, 0.12, 0.5, 0.35, ("sw", "grey"), bev=0.02)                 # meter box
    return [("Shell", s), ("Front", f), ("Glass", gl), ("Awning", aw), ("Sign", sg), ("Roof", r), ("Side", side)]


# ------------------------------------------------------------------ locker / wardrobe (0.8 × 2 × 0.5, front = −Z)
def locker():
    g = PG("Cabinet")
    B(g, 0, 1.0, 0, 0.8, 1.96, 0.5, ("sw", "grey"), bev=0.02)
    B(g, 0, 0.04, 0, 0.76, 0.08, 0.46, ("sw", "dark"), bev=0.01)                                 # kick plinth
    B(g, 0, 1.99, 0, 0.82, 0.04, 0.52, ("sw", "dark"), bev=0.01)                                 # top cap
    d = PG("Door")  # origin at the hinge (left edge, Unity −X), opens about Y
    B(d, 0, 1.04, -0.255, 0.72, 1.82, 0.025, ("sw", "deepblue"), bev=0.012)
    for k in range(4):
        B(d, 0, 1.75 - k * 0.05, -0.27, 0.36, 0.025, 0.006, ("sw", "black"), bev=0.0)           # vents
        B(d, 0, 0.35 - k * 0.05, -0.27, 0.36, 0.025, 0.006, ("sw", "black"), bev=0.0)
    B(d, 0.26, 1.04, -0.278, 0.04, 0.2, 0.03, ("metal",), bev=0.01)                              # handle
    B(d, 0.0, 1.45, -0.27, 0.16, 0.06, 0.006, ("sw", "white"), bev=0.0)                          # name tag
    B(d, 0.0, 1.55, -0.27, 0.1, 0.06, 0.006, ("sw", "yellow"), bev=0.0)                          # number plate
    for y in (0.4, 1.0, 1.6):
        B(d, -0.36, y, -0.255, 0.025, 0.08, 0.03, ("metal",), bev=0.004)                         # hinges
    hinge = U(-0.36, 0, -0.255)
    return [("Cabinet", g), ("Door", d, hinge)]


# ------------------------------------------------------------------ day board (2.4 × 1.2, wall-mounted, front = −Z, origin = board centre)
def day_board():
    g = PG("Board")
    B(g, 0, 0, 0.0, 2.4, 1.2, 0.05, ("metal",), bev=0.02)                                         # aluminium frame
    B(g, 0, 0.02, -0.015, 2.3, 1.08, 0.04, ("sw", "white"), bev=0.004)                            # writing surface (UI on top)
    B(g, 0, -0.58, -0.06, 2.0, 0.04, 0.08, ("metal",), bev=0.01)                                  # marker tray
    for i, col in enumerate(("red", "blue", "green", "black")):
        Cx(g, -0.6 + i * 0.16, -0.55, -0.07, 0.012, 0.12, ("sw", col), seg=8)                   # markers
    for i, (x, y, col) in enumerate(((-1.0, 0.45, "red"), (1.02, 0.44, "yellow"), (0.95, -0.4, "blue"))):
        Cz(g, x, y, -0.04, 0.025, 0.02, ("sw", col), seg=10)                                     # magnets
    B(g, -1.0, 0.33, -0.037, 0.14, 0.14, 0.004, ("sw", "yellow"), bev=0.0)                       # sticky note
    B(g, 0, 0.62, -0.03, 2.3, 0.08, 0.04, ("sw", "deepblue"), bev=0.01)                          # header strip
    return [("Board", g)]


# ------------------------------------------------------------------ shop terminal (0.8 × 1.2 × 0.5, front = −Z)
def terminal():
    g = PG("Kiosk")
    B(g, 0, 0.04, 0, 0.6, 0.08, 0.45, ("sw", "dark"), bev=0.02)
    B(g, 0, 0.5, 0.05, 0.36, 0.84, 0.3, ("sw", "navy"), bev=0.04, seg=2)                         # pedestal
    rot = Matrix.Rotation(math.radians(25), 4, 'X')  # screen tilted up towards the player
    hc = U(0, 1.05, -0.02)
    g.box(hc, (0.8, 0.44, 0.32), ("sw", "blue"), 0.05, 2, rot)                                   # head
    off = rot.to_3x3() @ Vector((0, 0.226, 0))
    g.box(hc + off, (0.62, 0.012, 0.24), ("sw", "dark"), 0.008, 1, rot)                           # bezel
    g.box(hc + off * 1.03, (0.56, 0.008, 0.2), ("tile", "screen", "+Y", "dark"), 0.0, 1, rot)    # screen
    B(g, 0.0, 0.9, -0.24, 0.4, 0.04, 0.12, ("tile", "keypad", "+Z", "dark"), bev=0.01)           # keyboard ledge
    B(g, 0.3, 0.92, -0.2, 0.1, 0.06, 0.14, ("sw", "dark"), bev=0.01)                              # card reader
    B(g, 0, 0.7, -0.106, 0.2, 0.2, 0.006, ("tile", "coin", "+Y", "yellow"), bev=0.0)              # shop badge
    return [("Kiosk", g)]


# ------------------------------------------------------------------ interior trash (origin bottom centre, ≤ 0.2 m)
def t_cup():
    g = PG("Cup")
    Cy(g, 0, 0.065, 0, 0.04, 0.13, ("sw", "white"), seg=14, r2=0.05)
    Cy(g, 0, 0.135, 0, 0.052, 0.012, ("sw", "dark"), seg=14)                                     # lid
    Cy(g, 0.015, 0.18, 0, 0.004, 0.09, ("sw", "red"), seg=6)                                     # straw
    B(g, 0, 0.07, -0.046, 0.05, 0.04, 0.004, ("sw", "green"), bev=0.0)                            # logo band
    return [("Cup", g)]


def t_bottle():
    g = PG("Bottle")
    Cy(g, 0, 0.065, 0, 0.032, 0.13, ("glass",), seg=12)
    Cy(g, 0, 0.15, 0, 0.032, 0.04, ("glass",), seg=12, r2=0.014)
    Cy(g, 0, 0.18, 0, 0.015, 0.025, ("sw", "blue"), seg=10)                                      # cap
    Cy(g, 0, 0.07, 0, 0.0335, 0.05, ("sw", "sky"), seg=12)                                       # label
    return [("Bottle", g)]


def t_wrapper():
    g = PG("Wrapper")
    bm = L.t_box((0.14, 0.06, 0.02), 0.008, 1)
    for v in bm.verts:  # crumple
        v.co.z += 0.006 * math.sin(v.co.x * 90 + v.co.y * 40)
        v.co.y *= 1 + 0.4 * abs(v.co.x) * 6
    g.add(bm, ("sw", "orange"), Matrix.Translation(U(0, 0.01, 0)))
    for x in (-0.075, 0.075):
        B(g, x, 0.01, 0, 0.02, 0.012, 0.05, ("sw", "orange"), bev=0.003)                          # twisted ends
    return [("Wrapper", g)]


def t_fries():
    g = PG("Fries")
    B(g, 0, 0.05, 0, 0.08, 0.1, 0.045, ("sw", "blue"), bev=0.006)
    for i in range(7):
        x = -0.03 + (i % 4) * 0.02
        z = -0.01 + (i // 4) * 0.02
        B(g, x, 0.11 + 0.01 * (i % 3), z, 0.01, 0.07, 0.01, ("sw", "yellow"), bev=0.002,
          rot=Matrix.Rotation(math.radians((i * 7) % 15 - 7), 4, 'Y'))
    B(g, 0, 0.06, -0.024, 0.082, 0.02, 0.003, ("sw", "white"), bev=0.0)                           # stripe
    return [("Fries", g)]


def t_diaper():
    g = PG("Diaper")
    bm = L.t_box((0.16, 0.12, 0.05), 0.02, 2)
    for v in bm.verts:
        v.co.y *= 1 - 0.25 * math.cos(v.co.x * 18)                                              # hourglass
    g.add(bm, ("sw", "white"), Matrix.Translation(U(0, 0.025, 0)))
    for x in (-0.06, 0.06):
        B(g, x, 0.051, 0, 0.03, 0.004, 0.03, ("sw", "sky"), bev=0.0)                              # tapes
    B(g, 0, 0.051, 0.0, 0.05, 0.004, 0.04, ("sw", "yellow"), bev=0.0)                            # print
    return [("Diaper", g)]


# ------------------------------------------------------------------ dumpster (3 × 1.5 × 1.8), vent (1.2 × 1.2 × 1), cage (2.2 m, door −Z)
def dumpster():
    g = PG("Body")
    bm = L.t_box((3.0, 1.6, 1.3), 0.04, 2)
    for v in bm.verts:  # tapered sides (wider at the top)
        k = (v.co.z + 0.65) / 1.3
        v.co.y *= 0.85 + 0.15 * k
    g.add(bm, ("sw", "green"), Matrix.Translation(U(0, 0.85, 0)))
    for x in (-1.0, 0.0, 1.0):
        B(g, x, 0.85, -0.82, 0.08, 1.2, 0.06, ("sw", "green"), bev=0.015)                         # ribs
    B(g, 0, 1.45, 0, 3.08, 0.06, 1.84, ("sw", "dark"), bev=0.015)                                # rim
    for x in (-1.6, 1.6):
        B(g, x, 1.0, 0, 0.12, 0.08, 0.6, ("metal",), bev=0.015)                                  # fork pockets
    for x in (-1.3, 1.3):
        for z in (-0.6, 0.6):
            Cz(g, x, 0.1, z, 0.1, 0.06, ("sw", "black"), seg=12)                                 # wheels
    B(g, 0.9, 0.95, -0.83, 0.5, 0.25, 0.004, ("tile", "warning", "+Y", "yellow"), bev=0.0)
    lid = PG("Lid")  # hinge at the back top edge (+Z)
    for i, x in enumerate((-0.76, 0.76)):
        rot = Matrix.Rotation(math.radians(3 + i * 9), 4, 'X')
        B(lid, x, 1.52, 0, 1.48, 0.06, 1.84, ("sw", "black"), bev=0.02, rot=rot)
    return [("Body", g), ("Lid", lid)]


def vent():
    g = PG("Vent")
    B(g, 0, 0.55, 0, 1.2, 1.1, 1.0, ("sw", "grey"), bev=0.03, seg=2)
    B(g, 0, 0.55, -0.505, 1.0, 0.9, 0.01, ("tile", "vent", "+Y", "grey"), bev=0.0)
    B(g, 0, 1.15, 0, 1.3, 0.1, 1.1, ("sw", "dark"), bev=0.02)                                    # cap
    Cy(g, 0.3, 1.45, 0.2, 0.18, 0.5, ("metal",), seg=14)                                         # exhaust stack
    Cy(g, 0.3, 1.72, 0.2, 0.26, 0.06, ("metal",), seg=14)                                        # rain hat
    B(g, 0, 0.02, 0, 1.3, 0.04, 1.1, ("sw", "concrete"), bev=0.01)
    tube_u(g, [(-0.6, 0.7, 0.5), (-0.6, 0.7, 0.7), (-0.6, 0.0, 0.7)], 0.04, ("sw", "dark"), 6)   # conduit to the wall
    return [("Vent", g)]


def cage():
    g = PG("Frame")
    B(g, 0, 0.03, 0, 2.2, 0.06, 2.2, ("tile", "grate", "+Z", "grey"), bev=0.01)                  # floor
    for x in (-1.05, 1.05):
        for z in (-1.05, 1.05):
            B(g, x, 1.0, z, 0.08, 2.0, 0.08, ("metal",), bev=0.01)
    B(g, 0, 2.02, 0, 2.2, 0.05, 2.2, ("metal",), bev=0.01)
    for i in range(-3, 4):  # bars on W / E / back (+Z); the door side (−Z) is open
        B(g, -1.05, 1.0, i * 0.3, 0.03, 2.0, 0.03, ("metal",), bev=0.0)
        B(g, 1.05, 1.0, i * 0.3, 0.03, 2.0, 0.03, ("metal",), bev=0.0)
        B(g, i * 0.3, 1.0, 1.05, 0.03, 2.0, 0.03, ("metal",), bev=0.0)
    for y in (0.6, 1.4):  # cross rails
        B(g, -1.05, y, 0, 0.04, 0.04, 2.1, ("metal",), bev=0.0)
        B(g, 1.05, y, 0, 0.04, 0.04, 2.1, ("metal",), bev=0.0)
        B(g, 0, y, 1.05, 2.1, 0.04, 0.04, ("metal",), bev=0.0)
    d = PG("Door")  # open gate leaf on the −Z side, hinge at (−1.05, z −1.05)
    for i in range(4):
        B(d, -0.85 + i * 0.25, 1.0, -1.08, 0.025, 1.9, 0.025, ("metal",), bev=0.0)
    for y in (0.1, 1.0, 1.9):
        B(d, -0.55, y, -1.08, 0.9, 0.04, 0.04, ("metal",), bev=0.0)
    hinge = U(-1.05, 0, -1.08)
    s = PG("Sign")
    B(s, 0, 2.35, -1.0, 1.6, 0.4, 0.05, ("sw", "yellow"), bev=0.015)                              # blank (text decal)
    B(s, -0.68, 2.35, -1.03, 0.2, 0.2, 0.004, ("tile", "warning", "+Y", "yellow"), bev=0.0)
    return [("Frame", g), ("Door", d, hinge), ("Sign", s), ("Socket_Inside", None, U(0, 0.06, 0))]


def _l(fn, size, folder="Props/Lot", **kw):
    return dict(build=fn, size=size, folder=folder, **kw)


PROPS = {
    "Office": _l(office, "8 × 3.3 × 6", "Buildings", export=LOT.format("Office")),
    "Locker": _l(locker, "0.8 × 2 × 0.5", "Props/Office", export=LOT.format("Locker")),
    "DayBoard": _l(day_board, "2.4 × 1.2 × 0.1", "Props/Office", export=LOT.format("DayBoard"), wall_y=-0.04, floor_z=-1.6),
    "Terminal": _l(terminal, "0.8 × 1.2 × 0.5", "Props/Office", export=LOT.format("Terminal")),
    "T_cup": _l(t_cup, "≤ 0.2", "Props/Street", export=PROPS_DIR.format("T_cup")),
    "T_bottle": _l(t_bottle, "≤ 0.2", "Props/Street", export=PROPS_DIR.format("T_bottle")),
    "T_wrapper": _l(t_wrapper, "≤ 0.2", "Props/Street", export=PROPS_DIR.format("T_wrapper")),
    "T_fries": _l(t_fries, "≤ 0.2", "Props/Street", export=PROPS_DIR.format("T_fries")),
    "T_diaper": _l(t_diaper, "≤ 0.2", "Props/Street", export=PROPS_DIR.format("T_diaper")),
    "Dumpster": _l(dumpster, "3 × 1.5 × 1.8", "Props/Street", export=LOT.format("Dumpster")),
    "Vent": _l(vent, "1.2 × 1.2 × 1", "Props/Street", export=LOT.format("Vent")),
    "Cage": _l(cage, "2.2 m cube, door −Z", "Props/Street", export=LOT.format("Cage")),
}
