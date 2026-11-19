# Decorative parked cars for the streets (no doors / interior / UV2), low poly, one shared look with the v2 cars.
# Origin = ground centre, front = Unity +Z (Blender −Y) like the customer cars.
import bmesh, math
from mathutils import Vector, Matrix
import parts_lib as L
import props_lib as PL
from props_lib import PG
import vehicles_gen as VG

SCN = "Assets/_Project/Art/Models/Generated/Scenery/{}.glb"


def _car(L_, W, prof, glass, paint, R=0.32, wy=None, extra=None):
    hl = L_ / 2
    b = PG("Body")
    bm = bmesh.new()
    VG._prism(bm, prof, -W / 2, W / 2, 0.06)
    b.add(bm, ("sw", paint))
    g = PG("Glass")
    gbm = bmesh.new()
    VG._prism(gbm, glass, -W / 2 + 0.06, W / 2 - 0.06, 0.02)
    g.add(gbm, ("sw", "navy"))  # opaque dark glass: no interior behind it
    roof_y0, roof_y1 = glass[1][0], glass[2][0]
    roof_z = glass[1][1]
    b.box(Vector((0, (roof_y0 + roof_y1) / 2, roof_z + 0.04)), (W - 0.08, roof_y1 - roof_y0 + 0.08, 0.08), ("sw", paint), 0.03, 2)
    d = PG("Details")
    zf = prof[0][1] + 0.35
    for s in (1, -1):
        d.box(Vector((s * (W / 2 - 0.28), -hl - 0.01, zf)), (0.3, 0.04, 0.14), ("sw", "yellow"), 0.02, 1)       # headlights
        d.box(Vector((s * (W / 2 - 0.2), hl + 0.01, zf)), (0.22, 0.04, 0.14), ("sw", "red"), 0.02, 1)         # taillights
        d.box(Vector((s * (W / 2 + 0.05), roof_y0 + 0.05, glass[0][1] + 0.08)), (0.08, 0.16, 0.1), ("sw", paint), 0.02, 1)  # mirrors
    for y, s in ((-hl - 0.05, -1), (hl + 0.05, 1)):
        d.box(Vector((0, y, prof[0][1] + 0.12)), (W - 0.06, 0.12, 0.18), ("sw", "dark"), 0.03, 2)               # bumpers
        d.box(Vector((0, y + s * 0.065, prof[0][1] + 0.14)), (0.42, 0.01, 0.09), ("tile", "plate", "+Y" if s > 0 else "-Y", "white"), 0.0, 1)
    d.box(Vector((0, -hl - 0.005, zf - 0.05)), (0.6, 0.03, 0.12), ("sw", "dark"), 0.01, 1)                        # grille
    w = PG("Wheels")
    for x in (W / 2 - 0.1, -(W / 2 - 0.1)):
        for y in (wy or (-hl + 0.75, hl - 0.75)):
            w.cyl(Vector((x, y, R)), R, 0.22, ("sw", "black"), 14, "X")
            w.cyl(Vector((x + math.copysign(0.115, x), y, R)), R * 0.55, 0.02, ("sw", "white"), 10, "X")
    out = [("Body", b), ("Glass", g), ("Details", d), ("Wheels", w)]
    if extra:
        out += extra(W, hl, roof_z)
    return out


def compact():
    return _car(3.6, 1.7, [(-1.8, 0.3), (-1.8, 0.85), (-1.0, 0.95), (1.8, 0.95), (1.8, 0.3)],
                [(-1.0, 0.95), (-0.55, 1.45), (1.3, 1.45), (1.75, 0.95)], "red", 0.3)


def sedan():
    return _car(4.5, 1.8, [(-2.25, 0.3), (-2.25, 0.82), (-1.1, 0.92), (1.4, 0.92), (2.25, 0.85), (2.25, 0.3)],
                [(-1.1, 0.92), (-0.55, 1.42), (0.75, 1.42), (1.4, 0.92)], "grey", 0.33)


def suv():
    return _car(4.6, 1.9, [(-2.3, 0.38), (-2.3, 1.0), (-1.3, 1.1), (2.3, 1.1), (2.3, 0.38)],
                [(-1.3, 1.1), (-0.85, 1.75), (2.15, 1.75), (2.25, 1.1)], "deepblue", 0.38)


def taxi():
    def sign(W, hl, rz):
        s = PG("TaxiSign")
        s.box(Vector((0, 0.1, rz + 0.17)), (0.5, 0.2, 0.18), ("sw", "white"), 0.04, 2)
        for x in (-0.15, 0.15):
            s.box(Vector((x, 0.1, rz + 0.06)), (0.06, 0.12, 0.06), ("sw", "dark"), 0.01, 1)
        for side in (1, -1):  # checker band on the doors
            for k in range(10):
                s.box(Vector((side * (W / 2 + 0.006), -1.0 + k * 0.22, 0.72 + 0.05 * (k % 2))), (0.012, 0.11, 0.05), ("sw", "black"), 0.0, 1)
        return [("TaxiSign", s)]
    return _car(4.5, 1.8, [(-2.25, 0.3), (-2.25, 0.82), (-1.1, 0.92), (1.4, 0.92), (2.25, 0.85), (2.25, 0.3)],
                [(-1.1, 0.92), (-0.55, 1.42), (0.75, 1.42), (1.4, 0.92)], "orange", 0.33, extra=sign)


def _p(fn, name):
    return dict(build=fn, size="parked car", folder="Scenery", export=SCN.format(name), view=(-0.9, -1.0, 0.6))


PROPS = {
    "ParkedCar_Compact": _p(compact, "ParkedCar_Compact"),
    "ParkedCar_Sedan": _p(sedan, "ParkedCar_Sedan"),
    "ParkedCar_SUV": _p(suv, "ParkedCar_SUV"),
    "ParkedCar_Taxi": _p(taxi, "ParkedCar_Taxi"),
}
