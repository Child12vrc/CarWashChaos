# Batch F #5: bee. Contract of C02_bee.glb (part nodes, root "Critter", Body origin at the body centre, Wing_L on +X Blender
# = Unity −X left, wings pivot at the root, Socket_Carry / Socket_Head) + a single-mesh "_hy"-style variant.
# Faces Unity +Z (Blender −Y). 0.13 m long, bottom at y 0.
import math
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import PG

CRIT = "Assets/_Project/Art/Models/Generated/Critters/{}.glb"
BODY_C = Vector((0, 0.009, 0.038))


def _ell(g, c, r, uv, u=14, v=8):
    g.add(L.t_sphere(1.0, u, v), uv, Matrix.Translation(c) @ Matrix.Diagonal((r[0], r[1], r[2], 1)))


def _bee_parts():
    body = PG("Body")
    # abdomen: yellow ellipsoid with 3 black bands + fuzzy thorax
    _ell(body, Vector((0, 0.022, 0.036)), (0.03, 0.036, 0.03), ("sw", "yellow"))
    for i, y in enumerate((0.008, 0.022, 0.036)):
        k = 1 - ((y - 0.022) / 0.036) ** 2
        rr = 0.03 * math.sqrt(max(0.05, k)) + 0.0015
        body.add(L.t_torus(rr, 0.0045, 16, 4), ("sw", "black"), Matrix.Translation(Vector((0, y, 0.036))) @ L.RX90)
    _ell(body, Vector((0, -0.008, 0.042)), (0.022, 0.02, 0.021), ("sw", "orange"))       # thorax
    for side in (1, -1):                                                                  # 3 leg pairs
        for k, y in enumerate((-0.014, -0.004, 0.006)):
            pts = [Vector((side * 0.012, y, 0.03)), Vector((side * 0.026, y + 0.004, 0.018)), Vector((side * 0.024, y + 0.008 * (k - 1), 0.002))]
            body.add(PL.tube(pts, 0.0022, 5), ("sw", "black"))
    head = PG("Head")
    _ell(head, Vector((0, -0.038, 0.046)), (0.02, 0.018, 0.019), ("sw", "black"))
    for side in (1, -1):
        _ell(head, Vector((side * 0.011, -0.05, 0.05)), (0.009, 0.006, 0.01), ("sw", "white"), 10, 6)   # eyes
        _ell(head, Vector((side * 0.012, -0.0545, 0.051)), (0.0045, 0.003, 0.005), ("sw", "black"), 8, 4)
        head.add(PL.tube([Vector((side * 0.006, -0.045, 0.062)), Vector((side * 0.012, -0.052, 0.074)), Vector((side * 0.016, -0.058, 0.077))], 0.0015, 5), ("sw", "black"))
        _ell(head, Vector((side * 0.016, -0.059, 0.078)), (0.003, 0.003, 0.003), ("sw", "black"), 6, 4)
    _ell(head, Vector((0, -0.054, 0.038)), (0.006, 0.004, 0.004), ("sw", "pink"), 8, 4)                # smile cheek
    sting = PG("Stinger")
    sting.add(L.t_cyl(0.004, 0.016, 8, r2=0.0005), ("sw", "dark"), Matrix.Translation(Vector((0, 0.064, 0.036))) @ Matrix.Rotation(math.radians(-90), 4, 'X'))
    wings = {}
    for name, side in (("Wing_L", 1), ("Wing_R", -1)):
        w = PG(name)
        c = Vector((side * 0.04, 0.008, 0.062))
        rot = Matrix.Rotation(math.radians(side * 15), 4, 'Y') @ Matrix.Rotation(math.radians(-side * 20), 4, 'Z')
        w.add(L.t_sphere(1.0, 14, 6), ("glass",), Matrix.Translation(c) @ rot @ Matrix.Diagonal((0.03, 0.017, 0.0025, 1)))
        wings[name] = (w, Vector((side * 0.012, 0.005, 0.055)))
    return body, head, sting, wings


def bee_v2():
    body, head, sting, wings = _bee_parts()
    out = [("Body", body, BODY_C), ("Head", head, Vector((0, -0.019, 0.041)), "Body"), ("Stinger", sting, Vector((0, 0.044, 0.038)), "Body")]
    for n, (w, piv) in wings.items():
        out.append((n, w, piv, "Body"))
    out += [("Socket_Carry", None, Vector((0, 0.009, 0.055)), "Body"), ("Socket_Head", None, Vector((0, -0.038, 0.076)), "Head")]
    return out


def bee_hy():
    import bpy
    body, head, sting, wings = _bee_parts()
    g = PG("C02_bee_hy")
    for p in [body, head, sting] + [w for w, _ in wings.values()]:
        bm = p.bm.copy()
        remap = []
        for m in p.mats:
            if m not in g.mats:
                g.mats.append(m)
            remap.append(g.mats.index(m))
        for f in bm.faces:
            f.material_index = remap[f.material_index]
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me)
        bm.free()
        g.bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    return [("Bee", g)]
def _tmp(p):
    import bpy
    me = bpy.data.meshes.new("tmp")
    p.bm.to_mesh(me)
    return me


PROPS = {
    "C02_bee_v2": dict(build=bee_v2, size="0.14 m", folder="Critters", export=CRIT.format("C02_bee_v2"), root="Critter", view=(-0.7, -1.0, 0.6)),
    "C02_bee_hy": dict(build=bee_hy, size="0.14 m (single mesh)", folder="Critters", export=CRIT.format("C02_bee_hy"), view=(-0.7, -1.0, 0.6)),
}
