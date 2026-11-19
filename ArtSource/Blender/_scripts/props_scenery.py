# Batch F #9 scenery: trees, bush, street lamp, utility pole. Origin = base centre, < 2k tris, front −Z where it matters.
import math, random
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import U, PG
from props_selfwash import B, Cz, Cy, Cx, tube_u

SCN = "Assets/_Project/Art/Models/Generated/Scenery/{}.glb"


def _blob(g, c, r, uv, seed, u=9, v=6, jitter=0.12, squash=1.0):
    """Faceted foliage clump: a low-poly sphere with radial noise."""
    rnd = random.Random(seed)
    bm = L.t_sphere(1.0, u, v)
    for vt in bm.verts:
        k = 1 + rnd.uniform(-jitter, jitter)
        vt.co = Vector((vt.co.x * r * k, vt.co.y * r * k, vt.co.z * r * k * squash))
    g.add(bm, uv, Matrix.Translation(c), mat="PropFoliage" if uv == ("sw", "green") else None)


def _trunk(g, pts, r0, r1, uv=("sw", "wood"), seg=7):
    """Tapered trunk / branch along Unity points."""
    n = len(pts)
    for i in range(n - 1):
        a, b = U(*pts[i]), U(*pts[i + 1])
        ra = r0 + (r1 - r0) * i / (n - 1)
        rb = r0 + (r1 - r0) * (i + 1) / (n - 1)
        d = b - a
        g.add(L.t_cyl(ra, d.length, seg, r2=rb), uv, Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4())


def tree_a():  # round broadleaf (zelkova-like), ~5.5 m
    t = PG("Trunk")
    _trunk(t, [(0, 0, 0), (0.05, 1.4, 0), (0.0, 2.4, 0.05)], 0.16, 0.1)
    _trunk(t, [(0.0, 2.0, 0.0), (0.7, 2.9, 0.2)], 0.07, 0.04)
    _trunk(t, [(0.0, 2.2, 0.0), (-0.6, 3.1, -0.3)], 0.07, 0.04)
    Cy(t, 0, 0.03, 0, 0.3, 0.06, ("sw", "wood"), seg=7, r2=0.18)                                     # root flare
    f = PG("Foliage")
    for i, (x, y, z, r) in enumerate([(0, 3.6, 0, 1.5), (0.9, 3.2, 0.3, 1.0), (-0.9, 3.3, -0.3, 1.05), (0.2, 4.4, -0.2, 1.0),
                                      (-0.3, 3.0, 0.8, 0.9), (0.4, 3.0, -0.9, 0.85)]):
        _blob(f, U(x, y, z), r, ("sw", "green"), i * 7 + 1, 10, 7)
    return [("Trunk", t), ("Foliage", f)]


def tree_b():  # conifer, ~6 m
    t = PG("Trunk")
    _trunk(t, [(0, 0, 0), (0, 1.2, 0)], 0.14, 0.11)
    f = PG("Foliage")
    for i, (y, r, h) in enumerate([(1.0, 1.5, 1.9), (2.3, 1.2, 1.7), (3.4, 0.9, 1.5), (4.4, 0.55, 1.3)]):
        bm = L.t_cyl(r, h, 9, r2=0.05)
        rnd = random.Random(i + 40)
        for vt in bm.verts:
            if vt.co.z < 0:
                vt.co.x *= 1 + rnd.uniform(-0.08, 0.08)
                vt.co.y *= 1 + rnd.uniform(-0.08, 0.08)
        f.add(bm, ("sw", "green"), Matrix.Translation(U(0, y + h / 2, 0)), mat="PropFoliage")
    return [("Trunk", t), ("Foliage", f)]


def tree_c():  # street tree in a square tree grate (ginkgo-like column), ~4.5 m
    t = PG("Trunk")
    _trunk(t, [(0, 0, 0), (0, 1.8, 0), (0.05, 2.6, 0)], 0.11, 0.07)
    B(t, 0, 0.02, 0, 1.2, 0.04, 1.2, ("tile", "grate", "+Z", "grey"), bev=0.005)                     # tree grate
    for x in (-0.35, 0.35):                                                                          # stakes
        tube_u(t, [(x, 0, 0.35), (x * 0.4, 1.3, 0.12)], 0.025, ("sw", "wood"), 6)
    f = PG("Foliage")
    for i, (y, r) in enumerate([(2.4, 0.85), (3.1, 0.95), (3.8, 0.75), (4.3, 0.45)]):
        _blob(f, U(0.05 * (i % 2), y, 0), r, ("sw", "green"), 90 + i, 8, 6, 0.1, 0.9)
    return [("Trunk", t), ("Foliage", f)]


def bush():
    f = PG("Bush")
    for i, (x, y, z, r) in enumerate([(0, 0.35, 0, 0.45), (0.35, 0.3, 0.1, 0.32), (-0.35, 0.28, -0.05, 0.34), (0.05, 0.55, 0.1, 0.3)]):
        _blob(f, U(x, y, z), r, ("sw", "green"), 200 + i, 8, 6, 0.12, 0.85)
    for (x, y, z) in ((0.2, 0.62, -0.2), (-0.25, 0.5, -0.25), (0.4, 0.42, -0.15)):
        f.add(L.t_sphere(0.04, 6, 4), ("sw", "white"), Matrix.Translation(U(x, y, z)))               # flowers
    return [("Bush", f)]


def street_lamp():  # Korean road lamp: 7 m pole, curved arm towards −Z, LED head
    g = PG("Pole")
    Cy(g, 0, 0.25, 0, 0.16, 0.5, ("sw", "grey"), seg=10)                                             # base cover
    Cy(g, 0, 3.6, 0, 0.09, 6.8, ("sw", "grey"), seg=10, r2=0.06)
    tube_u(g, [(0, 6.6, 0), (0, 7.1, -0.2), (0, 7.25, -0.8), (0, 7.2, -1.5)], 0.05, ("sw", "grey"), 8)
    B(g, 0, 0.9, -0.165, 0.14, 0.3, 0.02, ("sw", "dark"), bev=0.005)                                 # service hatch
    B(g, 0, 2.6, -0.075, 0.08, 0.12, 0.012, ("sw", "yellow"), bev=0.0)                               # ID plate
    h = PG("Head")
    B(h, 0, 7.15, -1.65, 0.32, 0.1, 0.7, ("sw", "grey"), bev=0.04, seg=2)
    B(h, 0, 7.095, -1.67, 0.26, 0.02, 0.56, ("sw", "cream"), bev=0.005)                              # LED panel (emissive in Unity)
    return [("Pole", g), ("Head", h)]


def utility_pole():  # concrete pole, 10 m, cross-arms, insulators, transformer, wire stubs along X
    g = PG("Pole")
    Cy(g, 0, 5.0, 0, 0.17, 10.0, ("sw", "concrete"), seg=10, r2=0.11)
    for i in range(10):                                                                              # step bolts
        y = 2.4 + i * 0.4
        Cx(g, 0.14 if i % 2 else -0.14, y, 0, 0.012, 0.18, ("metal",), seg=6)
    B(g, 0, 1.6, -0.15, 0.12, 0.18, 0.01, ("sw", "yellow"), bev=0.0)                                 # number plate
    arms = PG("Arms")
    for y, w in ((9.4, 2.4), (8.6, 1.8)):
        B(arms, 0, y, 0, w, 0.09, 0.09, ("sw", "dark"), bev=0.01)
        for x in (-w / 2 + 0.15, 0.0 if w > 2 else None, w / 2 - 0.15):
            if x is None:
                continue
            Cy(arms, x, y + 0.13, 0, 0.045, 0.16, ("sw", "white"), seg=8)                            # insulators
            Cy(arms, x, y + 0.2, 0, 0.06, 0.03, ("sw", "white"), seg=8)
            tube_u(arms, [(x, y + 0.22, 0), (x + (1.0 if x >= 0 else -1.0) * 0.0 + 1.2, y + 0.1, 0)], 0.008, ("sw", "black"), 4)
            tube_u(arms, [(x, y + 0.22, 0), (x - 1.2, y + 0.1, 0)], 0.008, ("sw", "black"), 4)
        tube_u(arms, [(-w / 2 + 0.1, y - 0.04, 0), (0, y - 0.5, 0)], 0.015, ("metal",), 4)          # braces
        tube_u(arms, [(w / 2 - 0.1, y - 0.04, 0), (0, y - 0.5, 0)], 0.015, ("metal",), 4)
    tr = PG("Transformer")
    Cy(tr, 0, 7.2, -0.42, 0.25, 0.75, ("sw", "grey"), seg=12)
    Cy(tr, 0, 7.6, -0.42, 0.27, 0.06, ("sw", "grey"), seg=12)
    for x in (-0.1, 0.1):
        Cy(tr, x, 7.72, -0.42, 0.03, 0.14, ("sw", "white"), seg=6)
    B(tr, 0, 7.2, -0.2, 0.12, 0.5, 0.12, ("metal",), bev=0.01)                                      # bracket
    tube_u(tr, [(0.1, 7.78, -0.42), (0.5, 8.4, -0.1), (0.75, 8.85, 0)], 0.01, ("sw", "black"), 4)
    for y in (6.0, 6.4):                                                                             # telecom cables, sagging stubs
        tube_u(tr, [(-1.5, y - 0.12, 0.15), (-0.75, y - 0.04, 0.15), (0, y, 0.15), (0.75, y - 0.04, 0.15), (1.5, y - 0.12, 0.15)], 0.018, ("sw", "black"), 5)
    B(tr, 0, 5.4, 0.16, 0.25, 0.35, 0.12, ("sw", "dark"), bev=0.02)                                  # cable box
    return [("Pole", g), ("Arms", arms), ("Transformer", tr)]


def _s(fn, size, **kw):
    return dict(build=fn, size=size, folder="Scenery", export=SCN.format(fn.__name__.title().replace("_", "")), **kw)


PROPS = {
    "Tree_A": dict(build=tree_a, size="≈ 3.6 × 5.4 × 3.6", folder="Scenery", export=SCN.format("Tree_A")),
    "Tree_B": dict(build=tree_b, size="≈ 3 × 6 × 3", folder="Scenery", export=SCN.format("Tree_B")),
    "Tree_C": dict(build=tree_c, size="≈ 1.9 × 4.7 × 1.9", folder="Scenery", export=SCN.format("Tree_C")),
    "Bush_A": dict(build=bush, size="≈ 1.4 × 0.9 × 1", folder="Scenery", export=SCN.format("Bush_A")),
    "StreetLamp": dict(build=street_lamp, size="≈ 0.4 × 7.3 × 2", folder="Scenery", export=SCN.format("StreetLamp")),
    "UtilityPole": dict(build=utility_pole, size="≈ 3.6 × 10 × 0.8", folder="Scenery", export=SCN.format("UtilityPole")),
}
