"""Procedural VFX textures + 9-slice UI frames for CarWashChaos.
Usage: python3 gen.py OUTDIR
VFX: white/grayscale RGB with alpha (tint in material) unless noted.
UI: palette colors, navy outline, supersampled 4x for AA.
"""
import sys, os, math, json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
SS = 4
NAVY = (27, 35, 64)
SOAP = (61, 184, 245)
FOAM = (244, 251, 255)
YEL = (255, 201, 60)
GRN = (76, 217, 100)
RED = (255, 90, 78)
rng = np.random.default_rng(7)
manifest = {}

def save(img, rel, **meta):
    p = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p, optimize=True)
    manifest[rel] = dict(size=list(img.size), **meta)

def grid(n):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    return (x + 0.5) / n * 2 - 1, (y + 0.5) / n * 2 - 1

def value_noise(n, cells, seed):
    r = np.random.default_rng(seed)
    g = r.random((cells + 1, cells + 1)).astype(np.float32)
    g[-1, :] = g[0, :]; g[:, -1] = g[:, 0]  # tileable
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
    return np.asarray(img, np.float32) / 255

def fbm(n, seed, octaves=4, base=4):
    acc = np.zeros((n, n), np.float32); amp = 1; tot = 0
    for o in range(octaves):
        acc += value_noise(n, base * 2 ** o, seed + o) * amp; tot += amp; amp *= 0.5
    return acc / tot

def white_alpha(a, rgb=None):
    a = np.clip(a, 0, 1)
    n = a.shape[0]
    if rgb is None:
        rgb = np.ones((n, a.shape[1], 3), np.float32)
    arr = np.dstack([rgb, a]) * 255
    return Image.fromarray(arr.astype(np.uint8), "RGBA")

def ss_canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))

def down(img, w, h):
    return img.resize((w, h), Image.LANCZOS)

# ---------------- VFX ----------------
def droplet(n=128):
    x, y = grid(n)
    # teardrop: circle at bottom + tapering top
    yy = y * 1.15
    r = 0.55
    body = np.sqrt(x ** 2 + (yy - 0.25) ** 2)
    taper = np.abs(x) - (0.55 * np.clip((yy + 0.85) / 1.1, 0, 1) ** 1.6)
    d = np.where(yy < 0.25, np.maximum(taper, -1), body - r)
    a = np.clip(-d * n / 6, 0, 1)
    # inner shading for volume: brighter rim, highlight
    hl = np.exp(-(((x + 0.18) / 0.12) ** 2 + ((yy - 0.1) / 0.2) ** 2))
    shade = 0.75 + 0.25 * np.clip(-d * 4, 0, 1) * 0 + 0.25 * hl
    rgb = np.dstack([shade] * 3)
    return white_alpha(a * (0.85 + 0.15 * hl), np.clip(rgb, 0, 1))

def soft_puff(n=256, seed=1, lobes=7, softness=0.35):
    x, y = grid(n)
    r = np.random.default_rng(seed)
    a = np.zeros_like(x)
    for i in range(lobes):
        ang = i / lobes * 2 * math.pi + r.random() * 0.5
        cx, cy = math.cos(ang) * 0.32, math.sin(ang) * 0.28
        rad = 0.32 + r.random() * 0.12
        a = np.maximum(a, 1 - np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / rad)
    a = np.maximum(a, 1 - np.sqrt(x ** 2 + y ** 2) / 0.5)
    a = np.clip(a / softness, 0, 1) ** 1.5
    a *= 0.7 + 0.3 * fbm(n, seed + 10)
    a *= np.clip((1 - np.sqrt(x ** 2 + y ** 2)) * 3, 0, 1)
    return a

def mist(n=256):
    a = soft_puff(n, 3, 8, 0.9)
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(n / 14)), np.float32) / 255
    a *= 0.75 + 0.25 * fbm(n, 4, 4, 3)
    x, y = grid(n); a *= np.clip((1 - np.sqrt(x ** 2 + y ** 2)) * 2.5, 0, 1)
    return white_alpha(np.clip(a / max(a.max(), 1e-3), 0, 1) * 0.9)

def splash_frame(n, t, seed):
    """t in [0,1]: burst of droplets radiating + ring, fading."""
    x, y = grid(n)
    r = np.random.default_rng(seed)
    a = np.zeros_like(x)
    count = 14
    for i in range(count):
        ang = (i / count) * 2 * math.pi + r.random() * 0.3
        speed = 0.7 + r.random() * 0.25
        dist = speed * (0.15 + 0.85 * t)
        cx, cy = math.cos(ang) * dist, math.sin(ang) * dist * 0.8 + 0.2 * t * t
        rad = (0.09 + r.random() * 0.06) * (1 - 0.6 * t)
        # stretch along motion
        dx, dy = x - cx, y - cy
        ca, sa = math.cos(ang), math.sin(ang)
        u = dx * ca + dy * sa; v = -dx * sa + dy * ca
        d = np.sqrt((u / (1.0 + 1.5 * (1 - t))) ** 2 + v ** 2) - rad
        a = np.maximum(a, np.clip(-d * n / 3, 0, 1))
    ring_r = 0.15 + 0.7 * t
    ring = np.exp(-((np.sqrt(x ** 2 + (y / 0.8) ** 2) - ring_r) / (0.04 + 0.03 * t)) ** 2) * (1 - t)
    core = np.clip(1 - np.sqrt(x ** 2 + y ** 2) / (0.25 * (1 - t) + 1e-3), 0, 1) * (1 - t) ** 2
    a = np.maximum(a * (1 - t ** 3), np.maximum(ring * 0.8, core))
    return a

def splash_sheet(cell=256, cols=4, rows=4):
    sheet = np.zeros((cell * rows, cell * cols), np.float32)
    frames = cols * rows
    for f in range(frames):
        t = f / (frames - 1) * 0.92
        fr = splash_frame(cell, t, 11)
        r_, c_ = divmod(f, cols)
        sheet[r_ * cell:(r_ + 1) * cell, c_ * cell:(c_ + 1) * cell] = fr
    return white_alpha(sheet)

def bubbles_mask(n, seed, count, rmin, rmax, region=0.75, outline=True):
    x, y = grid(n)
    r = np.random.default_rng(seed)
    fill = np.zeros_like(x); edge = np.zeros_like(x)
    for _ in range(count):
        while True:
            cx, cy = r.uniform(-region, region, 2)
            if cx * cx + cy * cy < region * region: break
        rad = r.uniform(rmin, rmax)
        d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) - rad
        fill = np.maximum(fill, np.clip(-d * n / 2, 0, 1))
        if outline:
            edge = np.maximum(edge, np.clip(1 - np.abs(d) * n / 2.2, 0, 1))
    return fill, edge

def foam_blob(n=256):
    fill, edge = bubbles_mask(n, 21, 38, 0.08, 0.2, 0.62)
    shade = 0.82 + 0.18 * fbm(n, 5)
    rgb = np.dstack([shade - edge * 0.25] * 3)  # bubble edges slightly gray
    return white_alpha(fill, np.clip(rgb, 0, 1))

def foam_decal(n=512):
    x, y = grid(n)
    base = fbm(n, 31, 5, 3)
    radial = 1 - np.sqrt(x ** 2 + y ** 2)
    shape = np.clip((base * 0.9 + radial * 1.1 - 0.75) * 6, 0, 1)
    fill, edge = bubbles_mask(n, 33, 260, 0.015, 0.06, 0.95)
    a = shape * (0.75 + 0.25 * fill)
    rgb = np.dstack([0.9 + 0.1 * fill - 0.2 * edge * shape] * 3)
    return white_alpha(a, np.clip(rgb, 0, 1))

def mud_splat(n=512):
    x, y = grid(n)
    th = np.arctan2(y, x); rr = np.sqrt(x ** 2 + y ** 2)
    r = np.random.default_rng(41)
    spikes = np.zeros_like(th)
    for k in range(9):
        spikes += np.cos(th * (k + 3) + r.random() * 6.28) * r.uniform(0.02, 0.06)
    a = np.clip((0.48 + spikes - rr) * n / 8, 0, 1)
    # satellite drops
    for _ in range(22):
        ang = r.random() * 6.28; dist = r.uniform(0.55, 0.9); rad = r.uniform(0.015, 0.05)
        d = np.sqrt((x - math.cos(ang) * dist) ** 2 + (y - math.sin(ang) * dist) ** 2) - rad
        a = np.maximum(a, np.clip(-d * n / 4, 0, 1))
    a *= np.clip((1 - rr) * 6, 0, 1)
    tex = 0.7 + 0.3 * fbm(n, 43, 5, 6)
    rgb = np.dstack([tex] * 3)  # grayscale; tint brown in material (mud #7A4E2D)
    return white_alpha(a * (0.85 + 0.15 * tex), rgb)

def sparkle(n=128):
    x, y = grid(n)
    ax, ay = np.abs(x), np.abs(y)
    star = np.maximum(np.clip(1 - (ax * 9 + ay * 1.05), 0, 1), np.clip(1 - (ay * 9 + ax * 1.05), 0, 1))
    diag = np.maximum(np.clip(1 - (np.abs(x - y) * 9 + np.abs(x + y) * 1.6) / 1.4, 0, 1),
                      np.clip(1 - (np.abs(x + y) * 9 + np.abs(x - y) * 1.6) / 1.4, 0, 1)) * 0.5
    glow = np.exp(-(x ** 2 + y ** 2) / 0.03)
    return white_alpha(np.clip(star + diag + glow, 0, 1))

def bubble(n=128):
    x, y = grid(n)
    rr = np.sqrt(x ** 2 + y ** 2)
    rim = np.clip((rr - 0.6) / 0.28, 0, 1) ** 2 * np.clip((0.9 - rr) * n / 3, 0, 1)
    body = np.clip((0.9 - rr) * n / 3, 0, 1) * 0.12
    hl = np.exp(-(((x + 0.35) / 0.16) ** 2 + ((y + 0.35) / 0.1) ** 2)) * 0.95
    hl2 = np.exp(-(((x - 0.38) / 0.07) ** 2 + ((y - 0.4) / 0.07) ** 2)) * 0.6
    a = np.clip(rim * 0.85 + body + hl + hl2, 0, 1)
    # iridescent tint in RGB (subtle)
    ang = np.arctan2(y, x)
    rgb = np.dstack([0.85 + 0.15 * np.cos(ang), 0.88 + 0.12 * np.cos(ang + 2.1), 0.92 + 0.08 * np.cos(ang + 4.2)])
    rgb = np.where((hl + hl2)[..., None] > 0.3, 1.0, rgb)
    return white_alpha(a, np.clip(rgb, 0, 1))

def poly_star(cx, cy, r_out, r_in, points=5, rot=-math.pi / 2):
    pts = []
    for i in range(points * 2):
        r = r_out if i % 2 == 0 else r_in
        a = rot + i * math.pi / points
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts

def stun_star(n=128):
    c = ss_canvas(n, n); d = ImageDraw.Draw(c); N = n * SS
    pts = poly_star(N / 2, N / 2 + N * 0.03, N * 0.44, N * 0.2)
    d.polygon(pts, fill=YEL + (255,), outline=NAVY + (255,), width=int(N * 0.045))
    # highlight
    d.ellipse([N * 0.36, N * 0.3, N * 0.46, N * 0.42], fill=(255, 240, 180, 255))
    return down(c, n, n)

def coin(n=128):
    c = ss_canvas(n, n); d = ImageDraw.Draw(c); N = n * SS; w = int(N * 0.045)
    d.ellipse([N * .06, N * .06, N * .94, N * .94], fill=(214, 150, 30, 255), outline=NAVY + (255,), width=w)
    d.ellipse([N * .16, N * .14, N * .84, N * .82], fill=YEL + (255,))
    d.ellipse([N * .26, N * .24, N * .74, N * .72], outline=(214, 150, 30, 255), width=int(N * .04))
    d.polygon(poly_star(N * .5, N * .49, N * .14, N * .065), fill=(214, 150, 30, 255))
    d.ellipse([N * .24, N * .2, N * .36, N * .3], fill=(255, 245, 200, 255))
    return down(c, n, n)

# ---------------- UI (9-slice) ----------------
def rrect(w, h, radius, fill, outline=NAVY, ow=4, shadow=0, inner_hl=True, grad=None):
    W, H, R, OW, SH = w * SS, h * SS, radius * SS, ow * SS, shadow * SS
    c = ss_canvas(w, h); d = ImageDraw.Draw(c)
    if SH:
        d.rounded_rectangle([0, SH, W - 1, H - 1], R, fill=NAVY + (255,))
    body_h = H - 1 - SH
    d.rounded_rectangle([0, 0, W - 1, body_h], R, fill=outline + (255,))
    inner = [OW, OW, W - 1 - OW, body_h - OW]
    d.rounded_rectangle(inner, max(R - OW, 1), fill=fill + (255,))
    if grad:  # lighter top half
        top = [inner[0], inner[1], inner[2], inner[1] + (inner[3] - inner[1]) * 0.45]
        d.rounded_rectangle(top, max(R - OW, 1), fill=grad + (255,))
        d.rectangle([top[0], top[1] + R, top[2], top[3]], fill=grad + (255,))
    if inner_hl:
        d.line([inner[0] + R, inner[1] + OW * 0.9, inner[2] - R, inner[1] + OW * 0.9], fill=(255, 255, 255, 110), width=max(OW // 2, 2))
    return down(c, w, h)

def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))

def crosshair(n=64, hit=False):
    c = ss_canvas(n, n); d = ImageDraw.Draw(c); N = n * SS
    col = RED if hit else (255, 255, 255)
    ow = int(N * 0.06); lw = int(N * 0.09)
    if hit:
        for sx, sy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
            p0 = (N / 2 + sx * N * .14, N / 2 + sy * N * .14); p1 = (N / 2 + sx * N * .36, N / 2 + sy * N * .36)
            d.line([p0, p1], fill=NAVY + (255,), width=lw + ow * 2)
            d.line([p0, p1], fill=col + (255,), width=lw)
    r = N * 0.1
    d.ellipse([N / 2 - r - ow, N / 2 - r - ow, N / 2 + r + ow, N / 2 + r + ow], fill=NAVY + (255,))
    d.ellipse([N / 2 - r, N / 2 - r, N / 2 + r, N / 2 + r], fill=col + (255,))
    return down(c, n, n)

def build():
    save(droplet(), "VFX/Textures/fx_water_droplet.png", tint="#9FDDFB", usage="particle")
    save(mist(), "VFX/Textures/fx_mist_puff.png", tint="#E6F6FF", usage="particle, alpha blended")
    save(splash_sheet(), "VFX/Textures/fx_splash_sheet.png", flipbook="4x4", usage="Texture Sheet Animation 4x4, 16 frames")
    save(foam_blob(), "VFX/Textures/fx_foam_blob.png", usage="particle")
    save(foam_decal(), "VFX/Textures/fx_foam_decal.png", usage="URP DecalProjector base map")
    save(mud_splat(), "VFX/Textures/fx_mud_splat.png", tint="#7A4E2D", usage="URP DecalProjector, tint brown")
    save(sparkle(), "VFX/Textures/fx_sparkle.png", usage="particle, additive")
    save(bubble(), "VFX/Textures/fx_bubble.png", usage="particle")
    save(coin(), "VFX/Textures/fx_coin.png", usage="particle / UI, colored")
    save(stun_star(), "VFX/Textures/fx_stun_star.png", usage="particle orbit, colored")

    B = dict(spriteBorder=None)
    save(rrect(128, 128, 22, (40, 52, 92), ow=5, shadow=6), "UI/Frames/ui_panel.png", border=[28, 34, 28, 28], usage="9-slice; border = L,B,R,T px")
    save(rrect(128, 64, 18, SOAP, ow=5, shadow=6), "UI/Frames/ui_btn_primary.png", border=[24, 26, 24, 22])
    save(rrect(128, 64, 18, mix(SOAP, (255, 255, 255), .2), ow=5, shadow=6), "UI/Frames/ui_btn_hover.png", border=[24, 26, 24, 22])
    save(rrect(128, 64, 18, mix(SOAP, NAVY, .25), ow=5, shadow=2, grad=None, inner_hl=False), "UI/Frames/ui_btn_pressed.png", border=[24, 22, 24, 22])
    save(rrect(128, 64, 18, (120, 128, 150), ow=5, shadow=6, inner_hl=False), "UI/Frames/ui_btn_disabled.png", border=[24, 26, 24, 22])
    save(rrect(128, 96, 16, FOAM, ow=5, shadow=5, inner_hl=False), "UI/Frames/ui_card.png", border=[22, 26, 22, 22], usage="bay card / tooltip")
    save(rrect(128, 32, 14, (24, 30, 54), ow=4, shadow=0, inner_hl=False), "UI/Frames/ui_bar_bg.png", border=[16, 14, 16, 14])
    save(rrect(128, 32, 14, (255, 255, 255), outline=(255, 255, 255), ow=0, shadow=0, inner_hl=False), "UI/Frames/ui_bar_fill.png", border=[14, 14, 14, 14], usage="Image type Sliced or Filled; tint per state (green/yellow/red)")
    save(rrect(256, 64, 30, (24, 30, 54), ow=4, shadow=4, inner_hl=False), "UI/Frames/ui_toast.png", border=[34, 34, 34, 30])
    save(crosshair(), "UI/HUD/ui_crosshair.png")
    save(crosshair(hit=True), "UI/HUD/ui_crosshair_hit.png")
    save(rrect(96, 96, 18, (40, 52, 92), ow=5, shadow=5), "UI/HUD/ui_slot.png", border=[24, 28, 24, 24], usage="tool slot bg")
    save(rrect(96, 96, 18, (40, 52, 92), outline=YEL, ow=6, shadow=5), "UI/HUD/ui_slot_selected.png", border=[24, 28, 24, 24])
    with open(os.path.join(OUT, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=1)

build()
print("\n".join(sorted(manifest)))
