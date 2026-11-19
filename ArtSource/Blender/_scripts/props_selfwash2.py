# G9 batch 2: canopies (Buildings/), vacuum pillar, vending machine, coin changer. Unity local frame via U(); front = −Z.
import math
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import U, PG
from props_selfwash import B, Cz, Cy, Cx, torus_z, tube_u, arc, EXP


# ------------------------------------------------------------------ canopies (visual only: Unity keeps its colliders)
def _canopy(name, w, d, y, posts, roof_sw, fascia_sw, fascia_h, post_w, sign=None):
    """Origin = ground centre of the roof footprint. Roof top at y + 0.15, ceiling at y − 0.04."""
    r = PG("Roof")
    B(r, 0, y + 0.07, 0, w, 0.16, d, ("sw", roof_sw), bev=0.05, seg=2)                         # roof slab
    B(r, 0, y - 0.02, 0, w - 0.24, 0.03, d - 0.24, ("sw", "white"), bev=0.0)                   # ceiling panels
    n = int((w - 0.3) / 0.6)
    for i in range(1, n):                                                                       # panel joints
        x = -w / 2 + 0.15 + i * (w - 0.3) / n
        B(r, x, y - 0.04, 0, 0.025, 0.012, d - 0.3, ("sw", "grey"), bev=0.0)
    f = PG("Fascia")
    for z, s in ((d / 2, 1), (-d / 2, -1)):
        B(f, 0, y - 0.05, z, w + 0.1, fascia_h, 0.12, ("sw", fascia_sw), bev=0.03, seg=2)
        B(f, 0, y - 0.05 - fascia_h * 0.36, z + s * 0.07, w + 0.14, fascia_h * 0.11, 0.035, ("sw", "yellow"), bev=0.008)
        B(f, 0, y - 0.05 + fascia_h / 2 + 0.01, z, w + 0.14, 0.03, 0.16, ("metal",), bev=0.006)   # top flashing
    for x, s in ((w / 2, 1), (-w / 2, -1)):
        B(f, x + s * 0.0, y - 0.05, 0, 0.12, fascia_h, d + 0.1, ("sw", fascia_sw), bev=0.03, seg=2)
    p = PG("Pillars")
    for (x, z) in posts:
        B(p, x, y / 2, z, post_w, y, post_w, ("metal",), bev=0.03, seg=2)
        B(p, x, 0.12, z, post_w + 0.16, 0.24, post_w + 0.16, ("sw", "concrete"), bev=0.03, seg=2)   # footing
        B(p, x, 0.7, z, post_w + 0.04, 0.8, post_w + 0.04, ("tile", "hazard", "+Y", "yellow"), bev=0.012)  # bump guard
        B(p, x, y - 0.15, z, post_w + 0.12, 0.12, post_w + 0.12, ("metal",), bev=0.02)           # capital
    lights = PG("Lights")
    for (lx, lz) in _light_grid(w, d):
        B(lights, lx, y - 0.05, lz, 0.9, 0.04, 0.28, ("sw", "dark"), bev=0.01)
        B(lights, lx, y - 0.075, lz, 0.82, 0.015, 0.2, ("sw", "cream"), bev=0.004)
    out = [("Roof", r), ("Fascia", f), ("Pillars", p), ("Lights", lights)]
    if sign:
        sw_, sh, sz = sign
        s = PG("RoofSign")
        B(s, 0, y + 0.75, sz, sw_, sh, 0.16, ("sw", fascia_sw), bev=0.04, seg=2)
        B(s, 0, y + 0.75, sz, sw_ + 0.1, sh + 0.1, 0.1, ("sw", "white"), bev=0.03, seg=2)          # white frame behind
        for x in (-sw_ * 0.42, sw_ * 0.42):
            B(s, x, y + 0.27, sz, 0.1, 0.4, 0.1, ("metal",), bev=0.01)
        for x in (-sw_ * 0.3, 0.0, sw_ * 0.3):                                                   # gooseneck lamps
            tube_u(s, [(x, y + 0.75 + sh / 2, sz), (x, y + 0.75 + sh / 2 + 0.18, sz), (x, y + 0.75 + sh / 2 + 0.2, sz + 0.2)], 0.015, ("metal",), 6)
            Cy(s, x, y + 0.75 + sh / 2 + 0.18, sz + 0.24, 0.06, 0.05, ("sw", "dark"), seg=10, r2=0.03)
        out.append(("RoofSign", s))
    # gutters + downpipes on the two end pillars of the south side
    g = PG("Gutters")
    for z, s in ((d / 2 + 0.11, 1), (-d / 2 - 0.11, -1)):
        B(g, 0, y - 0.32, z, w, 0.08, 0.08, ("metal",), bev=0.01)
    xs = sorted({x for x, _ in posts})
    for x in (xs[0], xs[-1]):
        z = min(zz for _, zz in posts)
        tube_u(g, [(x + 0.25, y - 0.32, -d / 2 - 0.11), (x + 0.25, y - 0.6, z - 0.25), (x + 0.25, 0.15, z - 0.25)], 0.045, ("metal",), 8)
    out.append(("Gutters", g))
    return out


def _light_grid(w, d):
    if w > 20:
        return [(x, z) for x in (-8, 0, 8) for z in (-2.4, 0.8, 4.0)]
    return [(x, 0.0) for x in (-w * 0.3, 0.0, w * 0.3)]


def canopy():
    # SelfWashBuilder.Canopy: x −12.4…12.4, z −1.6…9.6 → centre (0, 4); pillars x ±12.2 / ±4, z (−1.3, 9.3) − 4
    posts = [(x, z) for x in (-12.2, -4.0, 4.0, 12.2) for z in (-5.3, 5.3)]
    return _canopy("Canopy", 24.8, 11.2, 4.3, posts, "grey", "deepblue", 0.75, 0.35, sign=(7.2, 0.9, 5.2))


def vac_canopy():
    # VacuumArea: zone 10.5 × 4.4 (+0.8 roof overhang), roof at 3.35, posts at the zone corners − 0.2
    posts = [(x, z) for x in (-5.05, 5.05) for z in (-2.0, 2.0)]
    return _canopy("VacCanopy", 11.3, 5.2, 3.3, posts, "green", "green", 0.36, 0.22)


# ------------------------------------------------------------------ 진공 청소기 기둥
def vacuum_pillar():
    g = PG("Body")
    B(g, 0, 0.05, 0, 0.5, 0.1, 0.5, ("sw", "dark"), bev=0.02)                                  # base
    Cy(g, 0, 0.85, 0, 0.2, 1.5, ("sw", "yellow"), seg=20)                                       # column
    Cy(g, 0, 0.25, 0, 0.205, 0.22, ("tile", "hazard", "+Y", "yellow"), seg=20)
    Cy(g, 0, 1.62, 0, 0.22, 0.06, ("sw", "navy"), seg=20)
    B(g, 0, 1.79, 0, 0.5, 0.24, 0.5, ("sw", "blue"), bev=0.06, seg=3)                          # head
    B(g, 0, 1.79, -0.252, 0.2, 0.17, 0.008, ("tile", "vac", "+Y", "yellow"), bev=0.0)           # vacuum icon
    B(g, 0, 1.27, -0.2, 0.24, 0.32, 0.08, ("sw", "navy"), bev=0.02)                            # coin box
    B(g, 0, 1.3, -0.242, 0.18, 0.14, 0.006, ("tile", "coinpanel", "+Y", "grey"), bev=0.0)
    Cz(g, 0, 1.17, -0.245, 0.028, 0.02, ("sw", "green"), seg=12)                               # start button
    B(g, 0, 1.47, -0.24, 0.16, 0.06, 0.012, ("tile", "screen", "+Y", "dark"), bev=0.0)          # timer display
    # hose: out of the head on +X, a big loop down and up to the holster
    h = PG("Hose")
    tube_u(h, [(0.25, 1.72, 0.0), (0.36, 1.66, 0.0), (0.42, 1.3, 0.02), (0.4, 0.75, 0.04), (0.33, 0.55, 0.05),
               (0.27, 0.7, 0.06), (0.26, 0.95, 0.06), (0.27, 1.05, 0.05)], 0.035, ("sw", "black"), 10)
    B(h, 0.23, 1.05, 0.05, 0.06, 0.16, 0.12, ("sw", "navy"), bev=0.015)                         # holster
    B(h, 0.29, 1.15, 0.05, 0.05, 0.22, 0.08, ("sw", "dark"), bev=0.012)                         # nozzle
    for y in (0.75, 1.3):
        torus_z(h, 0.41, y, 0.03, 0.04, 0.01, ("metal",), 10, 4)                                # hose bands
    return [("Body", g), ("Hose", h)]


# ------------------------------------------------------------------ 자판기
def vending_machine():
    g = PG("Body")
    B(g, 0, 0.98, 0.0, 1.0, 1.84, 0.8, ("sw", "red"), bev=0.04, seg=2)                         # cabinet
    for x in (-0.42, 0.42):
        for z in (-0.32, 0.32):
            Cy(g, x, 0.02, z, 0.035, 0.04, ("sw", "black"), seg=8)                              # feet
    B(g, 0, 1.84, -0.405, 0.94, 0.12, 0.02, ("sw", "white"), bev=0.01)                          # light header
    B(g, -0.13, 1.15, -0.4, 0.68, 1.22, 0.02, ("sw", "dark"), bev=0.012)                        # window frame
    B(g, -0.13, 1.15, -0.412, 0.62, 1.16, 0.006, ("tile", "vending", "+Y", "dark"), bev=0.0)    # drinks
    gl = PG("Glass")
    B(gl, -0.13, 1.15, -0.43, 0.62, 1.16, 0.008, ("glass",), bev=0.0)
    B(g, 0.35, 1.32, -0.405, 0.2, 0.28, 0.012, ("tile", "keypad", "+Y", "dark"), bev=0.0)       # selection keys
    B(g, 0.35, 1.0, -0.405, 0.2, 0.26, 0.012, ("tile", "coinpanel", "+Y", "grey"), bev=0.0)     # coins / notes
    B(g, 0.35, 0.72, -0.41, 0.16, 0.08, 0.03, ("sw", "dark"), bev=0.01)                         # change cup
    B(g, -0.13, 0.32, -0.41, 0.56, 0.2, 0.04, ("sw", "black"), bev=0.015)                       # pickup slot
    B(g, -0.13, 0.36, -0.43, 0.5, 0.1, 0.01, ("sw", "dark"), bev=0.0)                           # flap
    B(g, 0, 0.12, -0.405, 0.96, 0.08, 0.012, ("sw", "dark"), bev=0.0)                           # kick plate
    B(g, 0.505, 1.1, 0.0, 0.012, 0.9, 0.6, ("sw", "white"), bev=0.0)                            # side graphic band (Unity −X side)
    return [("Body", g), ("Glass", gl)]


# ------------------------------------------------------------------ 동전교환기
def coin_changer():
    g = PG("Body")
    B(g, 0, 0.8, 0.0, 0.6, 1.6, 0.5, ("sw", "yellow"), bev=0.04, seg=2)
    B(g, 0, 1.42, -0.255, 0.5, 0.2, 0.012, ("sw", "cream"), bev=0.004)                          # name plate (text decal goes here)
    B(g, 0, 1.6, -0.02, 0.64, 0.06, 0.56, ("sw", "navy"), bev=0.02)                             # lid
    B(g, 0, 1.12, -0.255, 0.44, 0.36, 0.012, ("tile", "coinpanel", "+Y", "grey"), bev=0.004)    # note acceptor
    B(g, -0.15, 1.42, -0.262, 0.08, 0.08, 0.004, ("tile", "coin", "+Y", "yellow"), bev=0.0)     # coin badge
    B(g, 0, 0.47, -0.27, 0.34, 0.14, 0.08, ("metal",), bev=0.015)                               # payout tray
    B(g, 0, 0.5, -0.31, 0.28, 0.08, 0.01, ("sw", "black"), bev=0.0)
    for i, x in enumerate((-0.18, -0.06, 0.06, 0.18)):
        Cz(g, x, 0.85, -0.255, 0.022, 0.012, ("sw", ("green", "blue", "orange", "red")[i]), seg=10)  # denomination lamps
    B(g, 0, 0.1, -0.255, 0.56, 0.14, 0.012, ("tile", "hazard", "+Y", "yellow"), bev=0.0)
    B(g, 0.25, 0.78, -0.26, 0.04, 0.06, 0.02, ("metal",), bev=0.006)                            # key lock
    return [("Body", g)]


PROPS = {
    "SW_Canopy": dict(build=canopy, size="24.8 × 4.45 × 11.2 (+ roof sign to 5.25)", folder="Buildings", export=EXP.format("SW_Canopy")),
    "SW_VacCanopy": dict(build=vac_canopy, size="11.3 × 3.45 × 5.2", folder="Buildings", export=EXP.format("SW_VacCanopy")),
    "SW_VacuumPillar": dict(build=vacuum_pillar, size="0.5 × 1.9 × 0.5", folder="Props/CarWash", export=EXP.format("SW_VacuumPillar")),
    "SW_VendingMachine": dict(build=vending_machine, size="1 × 1.9 × 0.8", folder="Props/CarWash", export=EXP.format("SW_VendingMachine")),
    "SW_CoinChanger": dict(build=coin_changer, size="0.6 × 1.6 × 0.5", folder="Props/CarWash", export=EXP.format("SW_CoinChanger")),
}
