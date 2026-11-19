# G9 batch 3: the remaining self-wash props (same frames as the SelfWashBuilder graybox; front = −Z; Unity coords via U()).
import math, bmesh
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import U, PG
from props_selfwash import B, Cz, Cy, Cx, torus_z, tube_u, arc, EXP


def torus_y(g, x, y, z, R, r, uv, n=20, m=6):
    """Ring lying flat (axis = Unity Y)."""
    g.add(L.t_torus(R, r, n, m), uv, Matrix.Translation(U(x, y, z)))


def torus_x(g, x, y, z, R, r, uv, n=24, m=6):
    """Ring standing in the Unity YZ plane (axis = Unity X) — bicycle wheels along X."""
    g.add(L.t_torus(R, r, n, m), uv, Matrix.Translation(U(x, y, z)) @ L.RY90)


def mat_rack():
    g = PG("Frame")
    for x in (-0.78, 0.78):
        B(g, x, 0.7, 0, 0.06, 1.4, 0.06, ("metal",), bev=0.01)
        B(g, x, 0.025, 0, 0.1, 0.05, 0.5, ("sw", "dark"), bev=0.012)
    Cx(g, 0, 1.36, 0, 0.028, 1.6, ("metal",), seg=10)
    m = PG("Mats")
    for i, col in enumerate(("black", "dark", "black")):
        x, z = -0.48 + i * 0.48, 0.03 + 0.02 * i
        B(m, x, 0.98, z, 0.42, 0.68, 0.02, ("sw", col), bev=0.008)
        for k in range(5):
            B(m, x, 0.74 + k * 0.1, z - 0.012, 0.34, 0.02, 0.006, ("sw", "black"), bev=0.0)   # ribs
        for cx in (-0.12, 0.12):
            B(m, x + cx, 1.33, 0.0, 0.03, 0.06, 0.06, ("metal",), bev=0.006)                    # clips
    return [("Frame", g), ("Mats", m)]


def mat_washer():
    g = PG("Body")
    B(g, 0, 0.55, 0, 0.9, 1.06, 0.7, ("sw", "white"), bev=0.05, seg=2)
    B(g, 0, 1.08, 0.05, 0.92, 0.06, 0.6, ("sw", "blue"), bev=0.02)                              # top
    B(g, 0, 1.1, -0.2, 0.72, 0.05, 0.3, ("sw", "black"), bev=0.01)                              # mat slot
    for z in (-0.28, -0.12):
        Cx(g, 0, 1.08, z, 0.045, 0.68, ("sw", "deepblue"), seg=12)                              # brush rollers
    B(g, 0.27, 0.72, -0.352, 0.24, 0.3, 0.012, ("sw", "navy"), bev=0.006)                       # control panel
    B(g, 0.27, 0.8, -0.36, 0.18, 0.08, 0.006, ("tile", "screen", "+Y", "dark"), bev=0.0)
    B(g, 0.27, 0.65, -0.36, 0.16, 0.1, 0.006, ("tile", "coinpanel", "+Y", "grey"), bev=0.0)
    Cz(g, -0.2, 0.75, -0.36, 0.05, 0.02, ("sw", "red"), seg=14)                                # stop
    B(g, -0.1, 0.25, -0.355, 0.5, 0.12, 0.01, ("tile", "grate", "+Y", "grey"), bev=0.0)         # drain
    B(g, 0.452, 0.5, 0, 0.008, 0.3, 0.4, ("tile", "vent", "-X", "grey"), bev=0.0)
    B(g, 0, 0.03, 0, 0.92, 0.06, 0.72, ("sw", "dark"), bev=0.01)                                # plinth
    return [("Body", g)]


def air_gun():
    g = PG("Post")
    B(g, 0, 0.02, 0, 0.3, 0.04, 0.3, ("sw", "dark"), bev=0.01)
    B(g, 0, 0.75, 0, 0.1, 1.46, 0.1, ("metal",), bev=0.015)
    B(g, 0, 1.3, -0.1, 0.28, 0.3, 0.12, ("sw", "blue"), bev=0.03, seg=2)                       # compressor box
    Cz(g, -0.06, 1.33, -0.165, 0.055, 0.015, ("sw", "white"), seg=16)                           # gauge
    torus_z(g, -0.06, 1.33, -0.17, 0.055, 0.008, ("metal",), 16, 4)
    B(g, -0.05, 1.33, -0.175, 0.005, 0.04, 0.002, ("sw", "red"), bev=0.0)
    Cz(g, 0.08, 1.36, -0.165, 0.02, 0.015, ("sw", "green"), seg=10)
    B(g, 0.08, 1.24, -0.162, 0.08, 0.06, 0.006, ("tile", "coin", "+Y", "yellow"), bev=0.0)
    h = PG("Hose")
    h.add(PL.helix(U(0, 0.6, -0.2), 0.1, 0.012, 4, 0.36, "Z", 6, 12), ("sw", "yellow"))         # coiled hose
    tube_u(h, [(0, 0.96, -0.2), (0.03, 1.12, -0.16), (0.05, 1.16, -0.11)], 0.012, ("sw", "yellow"), 6)
    B(h, 0.0, 1.1, -0.2, 0.02, 0.12, 0.02, ("metal",), bev=0.004)                               # hook
    B(h, 0.15, 1.08, -0.2, 0.04, 0.12, 0.1, ("sw", "dark"), bev=0.012)                          # gun
    Cz(h, 0.15, 1.12, -0.29, 0.008, 0.12, ("metal",), seg=6)
    return [("Post", g), ("Hose", h)]


def recycle_bins():
    g = PG("Bins")
    for i, (col, tile) in enumerate((("blue", "rc_can"), ("yellow", "rc_plastic"), ("green", "rc_general"))):
        x = -0.5 + i * 0.5
        B(g, x, 0.44, 0, 0.46, 0.86, 0.46, ("sw", col), bev=0.03, seg=2)
        B(g, x, 0.9, 0, 0.48, 0.07, 0.48, ("sw", "dark"), bev=0.02)
        Cy(g, x, 0.935, -0.05, 0.08, 0.01, ("sw", "black"), seg=16)                             # throw-in hole
        B(g, x, 0.55, -0.232, 0.24, 0.24, 0.006, ("tile", tile, "+Y", "white"), bev=0.0)
        B(g, x, 0.06, 0, 0.47, 0.06, 0.47, ("sw", "dark"), bev=0.01)
    return [("Bins", g)]


def price_board():
    g = PG("Board")
    for x in (-1.05, 1.05):
        B(g, x, 1.2, 0.05, 0.12, 2.4, 0.12, ("metal",), bev=0.02)
        B(g, x, 0.1, 0.05, 0.3, 0.2, 0.3, ("sw", "concrete"), bev=0.03)
    B(g, 0, 2.25, 0, 2.48, 1.98, 0.08, ("sw", "white"), bev=0.03, seg=2)                       # frame
    B(g, 0, 2.25, -0.005, 2.4, 1.9, 0.1, ("sw", "deepblue"), bev=0.02)                          # board (text faces −Z)
    B(g, 0, 3.05, -0.06, 2.3, 0.3, 0.02, ("sw", "yellow"), bev=0.01)                            # header plate
    for x in (-0.8, 0.0, 0.8):                                                                  # top lamps
        tube_u(g, [(x, 3.2, 0.0), (x, 3.32, 0.0), (x, 3.34, -0.15)], 0.014, ("metal",), 6)
        Cy(g, x, 3.3, -0.2, 0.05, 0.05, ("sw", "dark"), seg=10, r2=0.025)
    return [("Board", g)]


def info_board():
    g = PG("Board")
    B(g, 0, 0.7, 0.04, 0.1, 1.4, 0.1, ("metal",), bev=0.015)
    B(g, 0, 0.06, 0.04, 0.3, 0.12, 0.3, ("sw", "concrete"), bev=0.02)
    B(g, 0, 1.65, 0.0, 1.66, 1.16, 0.06, ("sw", "deepblue"), bev=0.02)                          # frame
    B(g, 0, 1.65, -0.005, 1.6, 1.1, 0.08, ("sw", "white"), bev=0.01)                            # board (text faces −Z)
    B(g, 0, 2.22, 0.0, 1.7, 0.06, 0.12, ("sw", "deepblue"), bev=0.02)                           # top cap
    return [("Board", g)]


def no_entry_sign():
    g = PG("Sign")
    Cy(g, 0, 0.9, 0.03, 0.035, 1.8, ("metal",), seg=10)
    Cz(g, 0, 1.85, 0.0, 0.33, 0.02, ("sw", "red"), seg=28)
    torus_z(g, 0, 1.85, 0.0, 0.33, 0.012, ("sw", "white"), 28, 4)
    B(g, 0, 1.85, -0.014, 0.46, 0.11, 0.01, ("sw", "white"), bev=0.0)
    Cz(g, 0, 1.85, 0.016, 0.31, 0.01, ("sw", "grey"), seg=20)                                    # back
    for y in (1.72, 1.98):
        B(g, 0, y, 0.035, 0.1, 0.03, 0.03, ("metal",), bev=0.005)                               # clamps
    return [("Sign", g)]


def bench():
    g = PG("Bench")
    for i in range(3):
        B(g, 0, 0.45, -0.15 + i * 0.13, 1.6, 0.04, 0.11, ("sw", "wood"), bev=0.012)
    for k in range(2):
        rot = Matrix.Rotation(math.radians(-12), 4, 'X')
        B(g, 0, 0.6 + k * 0.12, 0.22 + k * 0.025, 1.6, 0.09, 0.035, ("sw", "wood"), bev=0.012, rot=rot)
    for x in (-0.7, 0.7):
        B(g, x, 0.22, 0.0, 0.06, 0.44, 0.06, ("sw", "dark"), bev=0.01)                          # front-back legs
        B(g, x, 0.22, -0.18, 0.06, 0.44, 0.06, ("sw", "dark"), bev=0.01)
        B(g, x, 0.22, 0.2, 0.06, 0.44, 0.06, ("sw", "dark"), bev=0.01)
        B(g, x, 0.42, 0.01, 0.07, 0.04, 0.46, ("sw", "dark"), bev=0.01)                          # seat bearer
        B(g, x, 0.64, 0.24, 0.05, 0.42, 0.05, ("sw", "dark"), bev=0.01, rot=Matrix.Rotation(math.radians(-12), 4, 'X'))
        B(g, x * 1.04, 0.6, -0.04, 0.06, 0.04, 0.38, ("sw", "dark"), bev=0.015)                 # armrest
        B(g, x * 1.04, 0.52, -0.2, 0.05, 0.16, 0.05, ("sw", "dark"), bev=0.01)
    return [("Bench", g)]


def parasol():
    g = PG("Parasol")
    Cy(g, 0, 1.2, 0, 0.025, 2.4, ("metal",), seg=10)
    # 8-panel canopy, alternating red / white wedges
    apex = U(0, 2.42, 0)
    for k in range(8):
        a0, a1 = 2 * math.pi * k / 8, 2 * math.pi * (k + 1) / 8
        bm = bmesh.new()
        p0 = bm.verts.new(apex)
        p1 = bm.verts.new(U(math.cos(a0) * 1.1, 2.12, math.sin(a0) * 1.1))
        p2 = bm.verts.new(U(math.cos(a1) * 1.1, 2.12, math.sin(a1) * 1.1))
        q1 = bm.verts.new(U(math.cos(a0) * 1.1, 2.04, math.sin(a0) * 1.1))
        q2 = bm.verts.new(U(math.cos(a1) * 1.1, 2.04, math.sin(a1) * 1.1))
        u0 = bm.verts.new(apex - Vector((0, 0, 0.025)))
        u1 = bm.verts.new(U(math.cos(a0) * 1.08, 2.1, math.sin(a0) * 1.08))
        u2 = bm.verts.new(U(math.cos(a1) * 1.08, 2.1, math.sin(a1) * 1.08))
        bm.faces.new((p0, p1, p2))
        bm.faces.new((u0, u2, u1))                                                              # underside
        bm.faces.new((p1, q1, q2, p2))                                                          # valance
        bm.faces.new((u1, u2, q2, q1))
        g.add(bm, ("sw", "red" if k % 2 else "white"))
    Cy(g, 0, 2.45, 0, 0.05, 0.08, ("sw", "white"), seg=10)                                      # finial
    Cy(g, 0, 0.73, 0, 0.42, 0.03, ("sw", "white"), seg=24)                                      # table top
    Cy(g, 0, 0.36, 0, 0.04, 0.7, ("metal",), seg=10)
    Cy(g, 0, 0.02, 0, 0.3, 0.04, ("sw", "dark"), seg=20)                                        # base
    for a in (0.6, 3.7):                                                                        # two chairs
        cx, cz = math.cos(a) * 0.75, math.sin(a) * 0.75
        rot = Matrix.Rotation(-a + math.pi / 2, 4, 'Z')
        c = PG("tmp")
        B(c, 0, 0.44, 0, 0.42, 0.04, 0.4, ("sw", "white"), bev=0.015)
        B(c, 0, 0.66, 0.19, 0.42, 0.4, 0.04, ("sw", "white"), bev=0.015)
        for lx in (-0.18, 0.18):
            for lz in (-0.17, 0.17):
                B(c, lx, 0.21, lz, 0.03, 0.42, 0.03, ("sw", "white"), bev=0.006)
        c.bm.transform(Matrix.Translation(U(cx, 0, cz)) @ rot)
        me = bpy_mesh(c)
        g.bm.from_mesh(me)
        _rm(me)
    return [("Parasol", g)]


def bpy_mesh(pg):
    import bpy
    me = bpy.data.meshes.new("tmp")
    pg.bm.to_mesh(me)
    return me


def _rm(me):
    import bpy
    bpy.data.meshes.remove(me)


def cctv():
    g = PG("Camera")
    B(g, 0, 0, 0.17, 0.12, 0.14, 0.03, ("sw", "white"), bev=0.01)                              # wall plate
    B(g, 0, -0.02, 0.07, 0.04, 0.04, 0.2, ("sw", "white"), bev=0.01)                            # arm
    rot = Matrix.Rotation(math.radians(12), 4, 'X')
    B(g, 0, -0.07, -0.1, 0.13, 0.12, 0.3, ("sw", "white"), bev=0.03, seg=2, rot=rot)           # housing
    B(g, 0, -0.005, -0.12, 0.16, 0.012, 0.36, ("sw", "white"), bev=0.004, rot=rot)              # sun hood
    B(g, 0, -0.1, -0.262, 0.09, 0.09, 0.008, ("tile", "lens", "+Y", "dark"), bev=0.0, rot=rot)
    Cz(g, 0.045, -0.04, -0.255, 0.006, 0.006, ("sw", "red"), seg=6)
    return [("Camera", g)]


def extinguisher():
    g = PG("Extinguisher")
    Cy(g, 0, 0.25, 0, 0.08, 0.46, ("sw", "red"), seg=18)
    g.add(L.t_sphere(0.08, 18, 8), ("sw", "red"), Matrix.Translation(U(0, 0.48, 0)) @ Matrix.Diagonal((1, 1, 0.6, 1)))
    Cy(g, 0, 0.03, 0, 0.082, 0.06, ("sw", "black"), seg=18)                                     # foot ring
    B(g, 0, 0.3, -0.081, 0.09, 0.14, 0.004, ("sw", "white"), bev=0.0)                            # label
    Cy(g, 0, 0.55, 0, 0.022, 0.06, ("metal",), seg=10)                                          # valve
    B(g, 0, 0.6, -0.02, 0.03, 0.02, 0.12, ("sw", "black"), bev=0.006)                            # handle
    B(g, 0, 0.58, 0.03, 0.025, 0.015, 0.08, ("metal",), bev=0.005)
    Cz(g, 0.03, 0.56, -0.02, 0.015, 0.01, ("sw", "white"), seg=10)                              # gauge
    tube_u(g, [(0.02, 0.55, 0.02), (0.07, 0.5, 0.04), (0.09, 0.3, 0.03), (0.085, 0.15, 0.0)], 0.009, ("sw", "black"), 6)
    return [("Extinguisher", g)]


def water_tank():
    g = PG("Stand")
    B(g, 0, 0.27, 0, 1.8, 0.06, 1.8, ("metal",), bev=0.01)                                     # platform
    for x in (-0.82, 0.82):
        for z in (-0.82, 0.82):
            B(g, x, 0.14, z, 0.08, 0.26, 0.08, ("metal",), bev=0.01)
    t = PG("Tank")
    Cy(t, 0, 1.3, 0, 0.85, 1.9, ("sw", "sky"), seg=28)
    g_ = L.t_sphere(0.85, 28, 10)
    t.add(g_, ("sw", "sky"), Matrix.Translation(U(0, 2.25, 0)) @ Matrix.Diagonal((1, 1, 0.18, 1)))  # dome
    for y in (0.6, 1.0, 1.4, 1.8):
        torus_y(t, 0, y, 0, 0.855, 0.02, ("sw", "sky"), 28, 4)                                  # ribs
    Cy(t, 0.3, 2.4, 0.1, 0.16, 0.06, ("sw", "deepblue"), seg=16)                                # hatch
    Cx(t, 0.9, 0.45, 0, 0.05, 0.25, ("metal",), seg=10)                                         # outlet + valve
    torus_x(t, 1.0, 0.45, 0, 0.06, 0.01, ("sw", "red"), 12, 4)
    lad = PG("Ladder")
    for x in (-0.2, 0.2):
        B(lad, x, 1.2, -0.92, 0.04, 2.3, 0.04, ("metal",), bev=0.006)
    for i in range(6):
        B(lad, 0, 0.5 + i * 0.35, -0.92, 0.4, 0.03, 0.03, ("metal",), bev=0.004)
    return [("Stand", g), ("Tank", t), ("Ladder", lad)]


def chem_drum():
    g = PG("Drum")
    Cy(g, 0, 0.45, 0, 0.29, 0.88, ("sw", "blue"), seg=24)
    for y in (0.3, 0.6):
        torus_y(g, 0, y, 0, 0.292, 0.012, ("sw", "blue"), 24, 4)
    torus_y(g, 0, 0.89, 0, 0.28, 0.015, ("sw", "deepblue"), 24, 4)
    Cz(g, 0, 0.47, -0.27, 0.0001, 0.0001, ("sw", "white"))  # (keeps index stable)
    B(g, 0, 0.47, -0.285, 0.24, 0.2, 0.012, ("tile", "drumlabel", "+Y", "white"), bev=0.0)       # label
    Cy(g, 0.15, 0.9, 0, 0.04, 0.03, ("sw", "dark"), seg=10)
    Cy(g, -0.15, 0.895, 0.05, 0.025, 0.02, ("sw", "dark"), seg=8)
    return [("Drum", g)]


def bucket_stack():
    g = PG("Buckets")
    for i, col in enumerate(("blue", "blue", "yellow")):
        y0 = i * 0.1
        Cy(g, 0, y0 + 0.16, 0, 0.18, 0.32, ("sw", col), seg=20, r2=0.15)
        torus_y(g, 0, y0 + 0.32, 0, 0.18, 0.012, ("sw", col), 20, 4)
    tube_u(g, [(-0.18, 0.5, 0), (-0.12, 0.62, 0), (0, 0.66, 0), (0.12, 0.62, 0), (0.18, 0.5, 0)], 0.006, ("metal",), 6)
    return [("Buckets", g)]


def towel_rack():
    g = PG("Rack")
    for x in (-0.58, 0.58):
        B(g, x, 0.65, 0, 0.05, 1.3, 0.05, ("metal",), bev=0.008)
        B(g, x, 0.02, 0, 0.08, 0.04, 0.4, ("sw", "dark"), bev=0.01)
    Cx(g, 0, 1.25, 0, 0.02, 1.2, ("metal",), seg=10)
    t = PG("Towels")
    for i, uv in enumerate((("tile", "towel", "+Y", "white"), ("sw", "blue"), ("sw", "yellow"), ("tile", "towel", "+Y", "white"))):
        x = -0.42 + i * 0.28
        B(t, x, 1.0, -0.035, 0.24, 0.5, 0.015, uv, bev=0.006)                                   # front drop
        B(t, x, 1.07, 0.035, 0.24, 0.36, 0.015, ("sw", uv[1] if uv[0] == "sw" else "white"), bev=0.006)  # back drop
        Cx(t, x, 1.255, 0, 0.032, 0.24, ("sw", uv[1] if uv[0] == "sw" else "white"), seg=10)     # fold over the bar
    return [("Rack", g), ("Towels", t)]


def slippery_sign():
    g = PG("Sign")
    for z, s in ((-0.1, -1), (0.1, 1)):
        rot = Matrix.Rotation(math.radians(16 * s), 4, 'X')
        B(g, 0, 0.33, z, 0.38, 0.66, 0.02, ("sw", "yellow"), bev=0.012, rot=rot)
        if s < 0:  # warning icon low on the front leaf (the "미끄럼 주의" decal sits above it)
            B(g, 0, 0.14, z - 0.06, 0.14, 0.12, 0.004, ("tile", "warning", "+Y", "yellow"), bev=0.0, rot=Matrix.Rotation(math.radians(-16), 4, 'X'))
    B(g, 0, 0.66, 0, 0.36, 0.04, 0.05, ("sw", "dark"), bev=0.01)                                 # hinge / handle
    return [("Sign", g)]


def pipes():
    g = PG("Pipes")
    Cx(g, 0, 1.3, 0, 0.06, 6.0, ("metal",), seg=14)
    Cx(g, 0, 1.55, 0, 0.04, 6.0, ("sw", "orange"), seg=12)
    for i in range(4):
        x = -2.7 + i * 1.8
        B(g, x, 1.42, 0.08, 0.05, 0.42, 0.06, ("sw", "dark"), bev=0.008)
        torus_x(g, x + 0.2, 1.3, 0, 0.066, 0.012, ("sw", "dark"), 14, 4)                        # couplings
        torus_x(g, x + 0.2, 1.55, 0, 0.046, 0.01, ("sw", "dark"), 12, 4)
    Cz(g, 1.0, 1.3, -0.1, 0.02, 0.1, ("metal",), seg=8)                                          # valve stem
    torus_z(g, 1.0, 1.3, -0.16, 0.07, 0.01, ("sw", "red"), 16, 4)                                # valve wheel
    return [("Pipes", g)]


def electric_box():
    g = PG("Box")
    B(g, 0, 0.5, 0, 0.7, 1.0, 0.3, ("sw", "grey"), bev=0.02)
    B(g, 0, 0.5, -0.155, 0.62, 0.92, 0.02, ("sw", "grey"), bev=0.012)                            # door
    B(g, 0, 0.78, -0.168, 0.16, 0.14, 0.004, ("tile", "electric", "+Y", "yellow"), bev=0.0)
    B(g, 0.25, 0.5, -0.17, 0.03, 0.12, 0.02, ("sw", "dark"), bev=0.006)                          # handle
    for y in (0.2, 0.8):
        B(g, -0.32, y, -0.16, 0.025, 0.08, 0.025, ("metal",), bev=0.004)                         # hinges
    B(g, 0, 0.2, -0.168, 0.4, 0.12, 0.004, ("tile", "vent", "+Y", "grey"), bev=0.0)
    B(g, 0, 1.02, -0.02, 0.74, 0.04, 0.36, ("sw", "grey"), bev=0.01)                             # rain cap
    Cy(g, 0.2, -0.15, 0.05, 0.03, 0.3, ("sw", "dark"), seg=8)                                     # conduit
    return [("Box", g)]


def ac_unit():
    g = PG("Unit")
    B(g, 0, 0.38, 0, 0.9, 0.62, 0.35, ("sw", "white"), bev=0.03, seg=2)
    B(g, -0.15, 0.38, -0.177, 0.5, 0.5, 0.006, ("tile", "fan", "+Y", "white"), bev=0.0)
    B(g, 0.3, 0.38, -0.177, 0.2, 0.44, 0.006, ("tile", "vent", "+Y", "grey"), bev=0.0)
    B(g, 0, 0.035, 0, 0.95, 0.07, 0.4, ("metal",), bev=0.01)                                     # feet
    tube_u(g, [(0.4, 0.3, 0.1), (0.5, 0.3, 0.15), (0.5, 0.3, 0.3)], 0.015, ("sw", "orange"), 6)  # pipes to the wall
    tube_u(g, [(0.4, 0.22, 0.1), (0.48, 0.22, 0.15), (0.48, 0.22, 0.3)], 0.012, ("sw", "white"), 6)
    return [("Unit", g)]


def planter():
    g = PG("Planter")
    B(g, 0, 0.24, 0, 0.8, 0.48, 0.8, ("sw", "concrete"), bev=0.04, seg=2)
    B(g, 0, 0.49, 0, 0.84, 0.05, 0.84, ("sw", "grey"), bev=0.015)                                # rim
    B(g, 0, 0.5, 0, 0.7, 0.02, 0.7, ("sw", "wood"), bev=0.0)                                      # soil
    for (x, y, z, r) in ((0, 0.72, 0, 0.3), (-0.18, 0.64, 0.12, 0.2), (0.2, 0.64, -0.1, 0.22), (0.12, 0.66, 0.2, 0.18), (-0.15, 0.66, -0.18, 0.18)):
        g.add(L.t_sphere(r, 10, 6), ("sw", "green"), Matrix.Translation(U(x, y, z)))
    for (x, y, z) in ((0.1, 0.95, -0.12), (-0.2, 0.82, -0.05), (0.25, 0.8, 0.12), (-0.05, 0.88, 0.22)):
        g.add(L.t_sphere(0.035, 8, 4), ("sw", "pink"), Matrix.Translation(U(x, y, z)))           # flowers
    return [("Planter", g)]


def bicycle():
    g = PG("Bicycle")
    for x in (0.6, -0.6):
        torus_z(g, x, 0.34, 0, 0.32, 0.022, ("sw", "black"), 24, 6)                              # tyres
        torus_z(g, x, 0.34, 0, 0.29, 0.008, ("metal",), 20, 4)                                    # rims
        Cz(g, x, 0.34, 0, 0.03, 0.06, ("metal",), seg=10)                                        # hubs
        for k in range(6):                                                                      # spokes
            a = math.pi * k / 6
            B(g, x, 0.34, 0, 0.004, 0.56, 0.004, ("metal",), bev=0.0, rot=Matrix.Rotation(a, 4, 'Y'))
    red = ("sw", "red")
    tube_u(g, [(-0.15, 0.35, 0), (0.42, 0.78, 0)], 0.022, red, 8)        # down tube
    tube_u(g, [(-0.2, 0.82, 0), (0.42, 0.8, 0)], 0.02, red, 8)           # top tube
    tube_u(g, [(-0.15, 0.35, 0), (-0.2, 0.84, 0)], 0.02, red, 8)         # seat tube
    tube_u(g, [(-0.6, 0.34, 0.05), (-0.15, 0.35, 0.05)], 0.012, red, 6)  # chain stays
    tube_u(g, [(-0.6, 0.34, 0.05), (-0.2, 0.8, 0.05)], 0.012, red, 6)    # seat stays
    tube_u(g, [(0.6, 0.34, 0), (0.46, 0.9, 0)], 0.018, ("metal",), 6)    # fork
    tube_u(g, [(0.46, 0.9, 0), (0.44, 1.0, 0)], 0.018, ("metal",), 6)
    Cz(g, 0.44, 1.0, 0, 0.012, 0.5, ("metal",), seg=8)                   # handlebar
    for z in (-0.25, 0.25):
        Cz(g, 0.44, 1.0, z, 0.018, 0.1, ("sw", "black"), seg=8)          # grips
    B(g, -0.22, 0.88, 0, 0.24, 0.05, 0.11, ("sw", "black"), bev=0.02)    # saddle
    Cz(g, -0.15, 0.35, 0, 0.07, 0.03, ("metal",), seg=14)               # chainring
    for z, s in ((-0.09, -1), (0.09, 1)):
        B(g, -0.15, 0.35 - 0.06 * s, z, 0.03, 0.15, 0.015, ("metal",), bev=0.004)  # cranks
        B(g, -0.15, 0.35 - 0.13 * s, z + 0.03 * s, 0.09, 0.02, 0.05, ("sw", "black"), bev=0.006)  # pedals
    tube_u(g, [(-0.3, 0.3, 0.06), (-0.4, 0.02, 0.18)], 0.008, ("metal",), 6)  # kickstand
    return [("Bicycle", g)]


def _p(fn, size, **kw):
    return dict(build=fn, size=size, folder="Props/CarWash", **kw)


PROPS = {
    "SW_MatRack": _p(mat_rack, "1.6 × 1.4 × 0.5", export=EXP.format("SW_MatRack")),
    "SW_MatWasher": _p(mat_washer, "0.9 × 1.1 × 0.7", export=EXP.format("SW_MatWasher")),
    "SW_AirGun": _p(air_gun, "0.4 × 1.5 × 0.4", export=EXP.format("SW_AirGun")),
    "SW_RecycleBins": _p(recycle_bins, "1.5 × 1 × 0.5", export=EXP.format("SW_RecycleBins")),
    "SW_PriceBoard": _p(price_board, "2.4 × 3.2 × 0.2", export=EXP.format("SW_PriceBoard")),
    "SW_InfoBoard": _p(info_board, "1.6 × 2.2 × 0.15", export=EXP.format("SW_InfoBoard")),
    "SW_NoEntrySign": _p(no_entry_sign, "0.7 × 2.2 × 0.1", export=EXP.format("SW_NoEntrySign")),
    "SW_Bench": _p(bench, "1.6 × 0.85 × 0.5", export=EXP.format("SW_Bench")),
    "SW_Parasol": _p(parasol, "2.2 × 2.4 × 2.2", export=EXP.format("SW_Parasol")),
    "SW_Cctv": _p(cctv, "0.3 × 0.25 × 0.45", export=EXP.format("SW_Cctv"), wall_y=-0.185, floor_z=-0.6),
    "SW_Extinguisher": _p(extinguisher, "0.2 × 0.62 × 0.2", export=EXP.format("SW_Extinguisher")),
    "SW_WaterTank": _p(water_tank, "1.8 × 2.4 × 1.8", export=EXP.format("SW_WaterTank")),
    "SW_ChemDrum": _p(chem_drum, "0.6 × 0.9 × 0.6", export=EXP.format("SW_ChemDrum")),
    "SW_BucketStack": _p(bucket_stack, "0.4 × 0.6 × 0.4", export=EXP.format("SW_BucketStack")),
    "SW_TowelRack": _p(towel_rack, "1.2 × 1.3 × 0.4", export=EXP.format("SW_TowelRack")),
    "SW_SlipperySign": _p(slippery_sign, "0.4 × 0.7 × 0.5", export=EXP.format("SW_SlipperySign")),
    "SW_Pipes": _p(pipes, "6 × 0.3 × 0.25 (at y 1.3–1.55)", export=EXP.format("SW_Pipes"), wall_y=-0.12),
    "SW_ElectricBox": _p(electric_box, "0.7 × 1 × 0.3", export=EXP.format("SW_ElectricBox"), wall_y=-0.15),
    "SW_AcUnit": _p(ac_unit, "0.9 × 0.7 × 0.35", export=EXP.format("SW_AcUnit")),
    "SW_Planter": _p(planter, "0.8 × 0.9 × 0.8", export=EXP.format("SW_Planter")),
    "SW_Bicycle": _p(bicycle, "1.7 × 1 × 0.5", export=EXP.format("SW_Bicycle")),
}
