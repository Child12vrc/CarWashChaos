# Shared prop toolkit: one 1024 atlas (icons / screens / labels + 16 flat colour swatches) used by every prop through
# MAT_PropAtlas, a steel material MAT_PropMetal and an optional MAT_PropGlass → ≤ 3 materials per prop, one texture.
# UVs are written per part when it is added: ("sw", k) = flat swatch k, ("tile", name, axis, fallback_k) = planar icon
# on the faces looking along `axis` (+Y = the prop's front in Blender = Unity −Z).
# Unity frame helper U(x, y, z): Unity local → Blender (glTF export mirrors X and swaps Y/Z).
import bpy, bmesh, os, math
import numpy as np
from mathutils import Vector, Matrix
import parts_lib as L

ATLAS_PNG = os.path.join(L.BLEND, r"Textures\T_PropAtlas.png")
N = 1024


def U(x, y, z):
    return Vector((-x, -z, y))


# ---------------------------------------------------------------- palette (sRGB)
SW = {
    "blue": (0.24, 0.72, 0.96), "navy": (0.11, 0.14, 0.25), "white": (0.94, 0.96, 0.98), "yellow": (1.0, 0.79, 0.24),
    "green": (0.30, 0.80, 0.40), "red": (0.93, 0.27, 0.24), "grey": (0.66, 0.68, 0.71), "dark": (0.16, 0.17, 0.19),
    "black": (0.05, 0.05, 0.06), "concrete": (0.52, 0.52, 0.5), "orange": (1.0, 0.55, 0.15), "wood": (0.58, 0.38, 0.22),
    "sky": (0.62, 0.85, 1.0), "pink": (1.0, 0.62, 0.80), "deepblue": (0.10, 0.33, 0.70), "cream": (0.95, 0.90, 0.78),
}
SW_KEYS = list(SW)
BIG = ["screen", "btn_water", "btn_foam", "btn_wax", "btn_rinse", "hazard", "vending", "coinpanel"]
SMALL = ["rc_can", "rc_plastic", "rc_general", "vent", "fan", "keypad", "lens", "drumlabel",
         "warning", "coin", "vac", "towel", "electric", "plate", "grate", "brand"]


def rect_of(name):
    """(u0, v0, u1, v1) of a tile or swatch."""
    if name in BIG:
        i = BIG.index(name)
        c, r = i % 4, i // 4
        return (c * 0.25, 1 - (r + 1) * 0.25, (c + 1) * 0.25, 1 - r * 0.25)
    if name in SMALL:
        i = SMALL.index(name)
        c, r = i % 8, i // 8
        return (c * 0.125, 0.5 - (r + 1) * 0.125, (c + 1) * 0.125, 0.5 - r * 0.125)
    i = SW_KEYS.index(name)
    c, r = i % 8, i // 8
    return (c * 0.125, 0.25 - (r + 1) * 0.125, (c + 1) * 0.125, 0.25 - r * 0.125)


# ---------------------------------------------------------------- atlas painting (numpy, v up)
def _grid(n):
    y, x = np.mgrid[0:n, 0:n]
    return (x + 0.5) / n, (y + 0.5) / n  # 0..1, y up


def _paint(img, mask, col, a=1.0):
    c = np.array(col, dtype=np.float32)
    m = (mask.astype(np.float32) * a)[..., None]
    img[..., :3] = img[..., :3] * (1 - m) + c * m


def _rrect(x, y, x0, y0, x1, y1, r):
    cx, cy = np.clip(x, x0 + r, x1 - r), np.clip(y, y0 + r, y1 - r)
    return (x >= x0) & (x <= x1) & (y >= y0) & (y <= y1) & ((x - cx) ** 2 + (y - cy) ** 2 <= r * r)


def _circle(x, y, cx, cy, r):
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r


def _ring(x, y, cx, cy, r0, r1):
    d = (x - cx) ** 2 + (y - cy) ** 2
    return (d <= r1 * r1) & (d >= r0 * r0)


def _poly(x, y, pts):
    inside = np.zeros(x.shape, bool)
    n = len(pts)
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        cond = ((y0 > y) != (y1 > y)) & (x < (x1 - x0) * (y - y0) / (y1 - y0 + 1e-9) + x0)
        inside ^= cond
    return inside


def _shade(img, x, y, k=0.10):
    """Soft top-left light + edge darkening → reads as a lit, slightly bevelled face."""
    f = 1 + k * (0.5 - y) * -1 + k * 0.5 * (0.5 - x)
    edge = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    f *= 1 - 0.18 * np.clip(1 - edge / 0.06, 0, 1)
    img[..., :3] *= f[..., None]


def _button(img, x, y, col, glyph):
    _paint(img, np.ones_like(x, bool), SW["dark"])
    _paint(img, _circle(x, y, .5, .5, .47), (0.85, 0.87, 0.9))
    _paint(img, _circle(x, y, .5, .5, .41), col)
    _paint(img, _circle(x, y, .42, .6, .28) & ~_circle(x, y, .5, .5, .3), tuple(min(1, c * 1.15) for c in col), 0.5)
    glyph(img, x, y)


def draw_tile(name, n):
    img = np.ones((n, n, 4), np.float32)
    x, y = _grid(n)
    W = SW["white"]
    if name == "screen":
        _paint(img, np.ones_like(x, bool), SW["navy"])
        _paint(img, _rrect(x, y, .06, .08, .94, .92, .06), (0.10, 0.28, 0.52))
        g = np.clip(y, 0, 1)
        img[..., 2] = np.where(_rrect(x, y, .06, .08, .94, .92, .06), img[..., 2] * (0.75 + 0.35 * g), img[..., 2])
        _paint(img, _circle(x, y, .26, .6, .15), SW["yellow"])
        _paint(img, _ring(x, y, .26, .6, .09, .11), (0.85, 0.6, 0.1))
        for i, w in enumerate((.5, .38, .44)):
            _paint(img, _rrect(x, y, .46, .68 - i * .13, .46 + w * .9, .74 - i * .13, .02), W)
        _paint(img, _rrect(x, y, .1, .14, .9, .26, .04), SW["green"])
        _paint(img, _circle(x, y, .82, .82, .04), SW["green"])
    elif name.startswith("btn_"):
        col = {"btn_water": SW["blue"], "btn_foam": SW["pink"], "btn_wax": SW["yellow"], "btn_rinse": SW["green"]}[name]

        def glyph(im, x, y):
            if name == "btn_water":
                _paint(im, _circle(x, y, .5, .42, .14) | _poly(x, y, [(.5, .74), (.37, .47), (.63, .47)]), W)
            elif name == "btn_foam":
                for cx, cy, r in ((.42, .44, .12), (.6, .5, .1), (.5, .63, .08)):
                    _paint(im, _circle(x, y, cx, cy, r), W)
                    _paint(im, _circle(x, y, cx - r * .3, cy + r * .3, r * .25), col, 0.6)
            elif name == "btn_wax":
                _paint(im, _poly(x, y, [(.5, .76), (.56, .56), (.76, .5), (.56, .44), (.5, .24), (.44, .44), (.24, .5), (.44, .56)]), W)
            else:
                for k in range(3):
                    yy = .38 + k * .12
                    _paint(im, np.abs(y - (yy + .04 * np.sin((x - .3) * 22))) < .025, W, 1.0 * (np.abs(x - .5) < .2))
        _button(img, x, y, col, glyph)
    elif name == "hazard":
        _paint(img, np.ones_like(x, bool), SW["yellow"])
        _paint(img, ((x + y) * 4 % 1) < 0.5, SW["black"])
    elif name == "vending":
        _paint(img, np.ones_like(x, bool), (0.07, 0.09, 0.14))
        cols = [SW["red"], SW["blue"], SW["green"], SW["orange"], SW["white"], SW["pink"], SW["yellow"]]
        for r in range(4):
            y0 = .08 + r * .23
            _paint(img, (y > y0 - .02) & (y < y0), (0.75, 0.78, 0.82))
            for c in range(6):
                x0 = .07 + c * .145
                col = cols[(r * 2 + c) % len(cols)]
                _paint(img, _rrect(x, y, x0, y0, x0 + .1, y0 + .17, .03), col)
                _paint(img, _rrect(x, y, x0 + .015, y0 + .05, x0 + .085, y0 + .09, .01), W, 0.85)
                _paint(img, (x > x0 + .02) & (x < x0 + .035) & (y > y0 + .02) & (y < y0 + .16), W, 0.35)
        _paint(img, (x < .5) & (y > x * 0.5 + .5), W, 0.07)  # glass glint
    elif name == "coinpanel":
        _paint(img, np.ones_like(x, bool), SW["grey"])
        _shade(img, x, y)
        _paint(img, _rrect(x, y, .12, .58, .55, .88, .05), SW["dark"])
        _paint(img, _rrect(x, y, .31, .66, .36, .8, .02), SW["black"])  # coin slot
        _paint(img, _circle(x, y, .76, .73, .13), SW["yellow"])
        _paint(img, _ring(x, y, .76, .73, .07, .09), (0.85, 0.6, 0.1))
        _paint(img, _rrect(x, y, .12, .2, .88, .45, .05), SW["dark"])
        _paint(img, _rrect(x, y, .2, .3, .8, .35, .02), SW["black"])   # bill / card slot
        _paint(img, _circle(x, y, .85, .52, .03), SW["green"])
    elif name in ("rc_can", "rc_plastic", "rc_general"):
        bg = {"rc_can": SW["blue"], "rc_plastic": SW["green"], "rc_general": SW["grey"]}[name]
        _paint(img, np.ones_like(x, bool), bg)
        _paint(img, _circle(x, y, .5, .5, .38), W)
        if name == "rc_can":
            _paint(img, _rrect(x, y, .38, .28, .62, .72, .05), bg)
            _paint(img, (np.abs(y - .62) < .02) & (np.abs(x - .5) < .12), W)
        elif name == "rc_plastic":
            _paint(img, _rrect(x, y, .4, .25, .6, .6, .06) | _rrect(x, y, .45, .58, .55, .72, .02), bg)
        else:
            _paint(img, _poly(x, y, [(.32, .28), (.68, .28), (.64, .62), (.36, .62)]) | (_rrect(x, y, .3, .62, .7, .68, .02)), bg)
    elif name == "vent":
        _paint(img, np.ones_like(x, bool), SW["grey"])
        _paint(img, ((y * 9) % 1 < .45) & (x > .08) & (x < .92) & (y > .08) & (y < .92), SW["dark"])
    elif name == "fan":
        _paint(img, np.ones_like(x, bool), SW["white"])
        _paint(img, _circle(x, y, .5, .5, .44), SW["dark"])
        a = np.arctan2(y - .5, x - .5)
        r = np.hypot(x - .5, y - .5)
        _paint(img, (np.sin(a * 4 + r * 9) > .3) & (r < .4) & (r > .07), SW["grey"])
        _paint(img, _ring(x, y, .5, .5, .3, .32) | _ring(x, y, .5, .5, .42, .44) | ((np.abs(x - .5) < .012) | (np.abs(y - .5) < .012)) & (r < .44), (0.85, 0.86, 0.88))
    elif name == "keypad":
        _paint(img, np.ones_like(x, bool), SW["dark"])
        for r in range(4):
            for c in range(3):
                _paint(img, _rrect(x, y, .15 + c * .25, .1 + r * .21, .32 + c * .25, .26 + r * .21, .03), SW["grey"])
    elif name == "lens":
        _paint(img, np.ones_like(x, bool), SW["dark"])
        _paint(img, _circle(x, y, .5, .5, .4), SW["black"])
        _paint(img, _circle(x, y, .5, .5, .22), (0.10, 0.16, 0.35))
        _paint(img, _circle(x, y, .4, .62, .08), W, 0.8)
    elif name == "drumlabel":
        _paint(img, np.ones_like(x, bool), W)
        _paint(img, (np.abs(y - .82) < .06) | (np.abs(y - .18) < .06), SW["deepblue"])
        _paint(img, _circle(x, y, .5, .45, .14) | _poly(x, y, [(.5, .72), (.37, .5), (.63, .5)]), SW["blue"])
    elif name == "warning":
        _paint(img, np.ones_like(x, bool), SW["yellow"])
        _paint(img, _poly(x, y, [(.5, .88), (.1, .14), (.9, .14)]), SW["black"])
        _paint(img, _poly(x, y, [(.5, .76), (.2, .2), (.8, .2)]), SW["yellow"])
        _paint(img, _rrect(x, y, .46, .38, .54, .64, .03) | _circle(x, y, .5, .29, .04), SW["black"])
    elif name == "coin":
        _paint(img, np.ones_like(x, bool), SW["dark"])
        _paint(img, _circle(x, y, .5, .5, .42), SW["yellow"])
        _paint(img, _ring(x, y, .5, .5, .3, .34), (0.85, 0.6, 0.1))
    elif name == "vac":
        _paint(img, np.ones_like(x, bool), SW["yellow"])
        a = np.arctan2(y - .5, x - .5)
        r = np.hypot(x - .5, y - .5)
        _paint(img, (np.abs(((a / (2 * math.pi) + r * 2.5) % 1) - .5) < .12) & (r < .4), SW["navy"])
    elif name == "towel":
        _paint(img, np.ones_like(x, bool), SW["white"])
        _paint(img, (np.abs(y - .12) < .04) | (np.abs(y - .88) < .04), SW["blue"])
        _paint(img, ((x * 40) % 1 < .5), (0.85, 0.88, 0.92), 0.4)
    elif name == "electric":
        _paint(img, np.ones_like(x, bool), SW["yellow"])
        _paint(img, _poly(x, y, [(.56, .88), (.3, .46), (.48, .46), (.42, .12), (.7, .56), (.52, .56)]), SW["black"])
    elif name == "plate":  # Korean-style white plate "12가 3456" (drawn squashed: the tile is stretched ~4.7:1 on the plate)
        _paint(img, np.ones_like(x, bool), SW["dark"])
        _paint(img, _rrect(x, y, .015, .06, .985, .94, .03), (0.97, 0.97, 0.97))
        segs = {"1": "bc", "2": "abged", "3": "abgcd", "4": "fgbc", "5": "afgcd", "6": "afgedc"}
        def digit(ch, x0, w=.085, y0=.2, h=.6, t=.022, tv=.09):
            ym = y0 + h / 2
            R = {"a": (x0, y0 + h - tv, x0 + w, y0 + h), "g": (x0, ym - tv / 2, x0 + w, ym + tv / 2), "d": (x0, y0, x0 + w, y0 + tv),
                 "f": (x0, ym, x0 + t, y0 + h), "b": (x0 + w - t, ym, x0 + w, y0 + h), "e": (x0, y0, x0 + t, ym), "c": (x0 + w - t, y0, x0 + w, ym)}
            for s in segs[ch]:
                a = R[s]
                _paint(img, (x >= a[0]) & (x <= a[2]) & (y >= a[1]) & (y <= a[3]), (0.08, 0.08, 0.1))
        for ch, x0 in zip("12", (.06, .17)):
            digit(ch, x0)
        # 가: ㄱ + ㅏ
        ink = (0.08, 0.08, 0.1)
        _paint(img, ((x >= .30) & (x <= .37) & (y >= .71) & (y <= .8)) | ((x >= .35) & (x <= .37) & (y >= .3) & (y <= .8)), ink)
        _paint(img, ((x >= .41) & (x <= .43) & (y >= .2) & (y <= .8)) | ((x >= .43) & (x <= .46) & (y >= .46) & (y <= .54)), ink)
        for ch, x0 in zip("3456", (.52, .63, .74, .85)):
            digit(ch, x0)
    elif name == "matgrid":
        _paint(img, np.ones_like(x, bool), SW["dark"])
        _paint(img, ((x * 10) % 1 < .5) ^ ((y * 10) % 1 < .5), SW["black"], 0.6)
    elif name == "grate":
        _paint(img, np.ones_like(x, bool), SW["grey"])
        _paint(img, ((x * 12) % 1 < .55) & (y > .1) & (y < .9), SW["black"])
    elif name == "brand":  # SUDSY drop logo (no text)
        _paint(img, np.ones_like(x, bool), SW["navy"])
        _paint(img, _circle(x, y, .5, .42, .26) | _poly(x, y, [(.5, .9), (.27, .52), (.73, .52)]), SW["blue"])
        _paint(img, _circle(x, y, .42, .48, .07), W)
        _paint(img, _circle(x, y, .6, .36, .05), W)
    else:  # swatch: flat colour with a very light grain
        _paint(img, np.ones_like(x, bool), SW[name])
        img[..., :3] *= (1 + 0.025 * np.sin(x * 61 + y * 37) * np.cos(y * 53))[..., None]
    if name in SMALL + BIG and name not in ("vending", "screen"):
        _shade(img, x, y, 0.06)
    return np.clip(img, 0, 1)


def build_atlas():
    a = np.ones((N, N, 4), np.float32)
    for name in BIG + SMALL + SW_KEYS:
        u0, v0, u1, v1 = rect_of(name)
        x0, y0, x1, y1 = int(u0 * N), int(v0 * N), int(u1 * N), int(v1 * N)
        a[y0:y1, x0:x1] = draw_tile(name, x1 - x0)
    os.makedirs(os.path.dirname(ATLAS_PNG), exist_ok=True)
    img = bpy.data.images.new("T_PropAtlas", N, N, alpha=False)
    img.pixels.foreach_set(a.ravel())
    img.filepath_raw = ATLAS_PNG
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    return ATLAS_PNG


# ---------------------------------------------------------------- materials
PMAT = {}


def prop_mats():
    PMAT.clear()
    img = bpy.data.images.load(ATLAS_PNG, check_existing=True)
    img.name = "T_PropAtlas"
    for name, (col, rough, metal, alpha, tex) in {
        "PropAtlas": ((1, 1, 1), 0.55, 0.0, 1.0, True),
        "PropMetal": ((0.72, 0.74, 0.77), 0.32, 0.85, 1.0, False),
        "PropGlass": ((0.70, 0.86, 0.95), 0.05, 0.0, 0.35, False),
        "PropFoliage": ((0.17, 0.42, 0.16), 0.9, 0.0, 1.0, False),
    }.items():
        m = bpy.data.materials.new("MAT_" + name)
        try:
            m.use_nodes = True
        except Exception:
            pass
        nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = (*col, 1)
        b.inputs["Roughness"].default_value = rough
        b.inputs["Metallic"].default_value = metal
        if alpha < 1:
            b.inputs["Alpha"].default_value = alpha
            try:
                m.surface_render_method = 'BLENDED'
            except Exception:
                pass
        if tex:
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = img
            nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
        m.diffuse_color = (*col, alpha)
        PMAT[name] = m
    return PMAT


# ---------------------------------------------------------------- UV-aware mesh group
AX = {"+Y": Vector((0, 1, 0)), "-Y": Vector((0, -1, 0)), "+X": Vector((1, 0, 0)), "-X": Vector((-1, 0, 0)), "+Z": Vector((0, 0, 1)), "-Z": Vector((0, 0, -1))}


def _uv_planar(axis, c):
    # viewer looking against `axis` (standing on the +axis side), image right / up
    return {"+Y": (-c.x, c.z), "-Y": (c.x, c.z), "+X": (c.y, c.z), "-X": (-c.y, c.z), "+Z": (c.x, c.y), "-Z": (c.x, -c.y)}[axis]


def _inset(r, px=3):
    d = px / N
    return (r[0] + d, r[1] + d, r[2] - d, r[3] - d)


class PG:
    def __init__(s, name):
        s.name, s.bm, s.mats = name, bmesh.new(), []
        s.uv = s.bm.loops.layers.uv.new("UVMap")

    def add(s, tbm, uv=("sw", "grey"), M=None, mat=None):
        """uv: ("sw", key) | ("tile", tile, axis, fallback_key) | ("metal",) | ("glass",)"""
        mat = mat or {"metal": "PropMetal", "glass": "PropGlass"}.get(uv[0], "PropAtlas")
        lay = tbm.loops.layers.uv.new("UVMap")
        tbm.normal_update()
        if uv[0] == "tile":
            _, tile, axis, fb = uv
            av = AX[axis]
            sel = [f for f in tbm.faces if f.normal.dot(av) > 0.7]
            pts = [_uv_planar(axis, l.vert.co) for f in sel for l in f.loops]
            if pts:
                u0, v0 = min(p[0] for p in pts), min(p[1] for p in pts)
                u1, v1 = max(p[0] for p in pts), max(p[1] for p in pts)
            r = _inset(rect_of(tile))
            fr = rect_of(fb)
            fc = ((fr[0] + fr[2]) / 2, (fr[1] + fr[3]) / 2)
            for f in tbm.faces:
                if f in sel:
                    for l in f.loops:
                        p = _uv_planar(axis, l.vert.co)
                        l[lay].uv = (r[0] + (p[0] - u0) / max(1e-6, u1 - u0) * (r[2] - r[0]), r[1] + (p[1] - v0) / max(1e-6, v1 - v0) * (r[3] - r[1]))
                else:
                    for l in f.loops:
                        l[lay].uv = fc
        else:
            key = uv[1] if uv[0] == "sw" else "grey"
            fr = rect_of(key)
            fc = ((fr[0] + fr[2]) / 2, (fr[1] + fr[3]) / 2)
            for f in tbm.faces:
                for l in f.loops:  # tiny box projection inside the swatch (keeps UVs non-degenerate)
                    c = l.vert.co
                    l[lay].uv = (fc[0] + ((c.x + c.y) % 1.0) * 0.02 - 0.01, fc[1] + ((c.z + c.y) % 1.0) * 0.02 - 0.01)  # stays inside the cell
        if M is not None:
            tbm.transform(M)
        if mat not in s.mats:
            s.mats.append(mat)
        idx = s.mats.index(mat)
        for f in tbm.faces:
            f.material_index = idx
            f.smooth = False
        me = bpy.data.meshes.new("tmp")
        tbm.to_mesh(me)
        tbm.free()
        s.bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        return s

    def box(s, center, size, uv=("sw", "grey"), bev=0.015, seg=2, rot=None):
        M = Matrix.Translation(center) @ (rot or Matrix())
        return s.add(L.t_box(size, bev, seg), uv, M)

    def cyl(s, center, r, depth, uv=("sw", "grey"), seg=16, axis="Z", r2=None, rot=None):
        R = {"Z": Matrix(), "Y": Matrix.Rotation(math.radians(-90), 4, 'X'), "X": L.RY90}[axis]
        tbm = L.t_cyl(r, depth, seg, r2)
        tbm.transform(R)  # axis first, so tile UVs see the real cap direction
        return s.add(tbm, uv, Matrix.Translation(center) @ (rot or Matrix()))

    def obj(s, col, name=None, matrix=None):
        name = name or s.name
        bmesh.ops.recalc_face_normals(s.bm, faces=s.bm.faces[:])
        me = bpy.data.meshes.new("SM_" + name)
        if matrix is not None:
            s.bm.transform(matrix.inverted())
        s.bm.to_mesh(me)
        for m in s.mats:
            me.materials.append(PMAT[m])
        o = bpy.data.objects.new(name, me)
        if matrix is not None:
            o.matrix_world = matrix
        col.objects.link(o)
        return o


def tube(path, r, seg=8):
    """Swept circle along a polyline (hoses, pipes). Returns a bmesh."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(path):
        t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        a = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        n1 = t.cross(a).normalized()
        n2 = t.cross(n1).normalized()
        rings.append([bm.verts.new(p + (n1 * math.cos(2 * math.pi * k / seg) + n2 * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
    for i in range(len(rings) - 1):
        for k in range(seg):
            a, b = rings[i], rings[i + 1]
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    return bm


def helix(center, r_coil, r_tube, turns, height, axis="Z", seg=6, steps_per_turn=14):
    pts = []
    n = int(turns * steps_per_turn)
    for i in range(n + 1):
        a = 2 * math.pi * i / steps_per_turn
        h = height * i / n
        pts.append(Vector((math.cos(a) * r_coil, math.sin(a) * r_coil, h)))
    bm = tube(pts, r_tube, seg)
    R = {"Z": Matrix(), "Y": L.RX90, "X": L.RY90}[axis]
    bm.transform(Matrix.Translation(center) @ R)
    return bm
