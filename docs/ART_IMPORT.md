# ART_IMPORT — generated textures (procedural, by cloud Claude)
Root: `Assets/_Project/Art/`. Regenerable; do not hand-edit PNGs. Source of truth for import = this table.

## Import rules (implement as AssetPostprocessor `ArtImportPostprocessor` matching these folders)
- `Art/UI/**`: Texture Type Sprite (2D and UI), Single, Mesh Type Full Rect, Alpha Is Transparency, no mipmaps, Filter Bilinear, Compression High Quality, Pixels Per Unit 100. Apply `spriteBorder` from table (order L,B,R,T px) → Image Type Sliced.
- `Art/VFX/Textures/**`: Texture Type Default, Alpha Is Transparency, mipmaps ON, Wrap Clamp, sRGB ON. `fx_splash_sheet` = 4x4 flipbook (Particle System Texture Sheet Animation Tiles 4x4).
- White/gray textures are tint-ready: set color in material/particle Start Color (suggested tint column).

| file | size | 9-slice border L,B,R,T | tint | usage |
|---|---|---|---|---|
| Art/UI/Frames/ui_bar_bg.png | 128x32 | 16,14,16,14 |  |  |
| Art/UI/Frames/ui_bar_fill.png | 128x32 | 14,14,14,14 |  | WHITE: Image type Sliced or Filled; tint per state (green #4CD964 / yellow #FFC93C / red #FF5A4E) |
| Art/UI/Frames/ui_btn_disabled.png | 128x64 | 24,26,24,22 |  |  |
| Art/UI/Frames/ui_btn_hover.png | 128x64 | 24,26,24,22 |  |  |
| Art/UI/Frames/ui_btn_pressed.png | 128x64 | 24,22,24,22 |  |  |
| Art/UI/Frames/ui_btn_primary.png | 128x64 | 24,26,24,22 |  |  |
| Art/UI/Frames/ui_card.png | 128x96 | 22,26,22,22 |  | bay card / tooltip |
| Art/UI/Frames/ui_panel.png | 128x128 | 28,34,28,28 |  | 9-slice; border = L,B,R,T px |
| Art/UI/Frames/ui_toast.png | 256x64 | 34,34,34,30 |  |  |
| Art/UI/HUD/ui_crosshair.png | 64x64 |  |  |  |
| Art/UI/HUD/ui_crosshair_hit.png | 64x64 |  |  |  |
| Art/UI/HUD/ui_slot.png | 96x96 | 24,28,24,24 |  | tool slot bg |
| Art/UI/HUD/ui_slot_selected.png | 96x96 | 24,28,24,24 |  |  |
| Art/VFX/Textures/fx_bubble.png | 128x128 |  |  | particle |
| Art/VFX/Textures/fx_coin.png | 128x128 |  |  | particle / UI, colored |
| Art/VFX/Textures/fx_foam_blob.png | 256x256 |  |  | particle |
| Art/VFX/Textures/fx_foam_decal.png | 512x512 |  |  | URP DecalProjector base map |
| Art/VFX/Textures/fx_mist_puff.png | 256x256 |  | #E6F6FF | particle, alpha blended |
| Art/VFX/Textures/fx_mud_splat.png | 512x512 |  | #7A4E2D | URP DecalProjector, tint brown |
| Art/VFX/Textures/fx_sparkle.png | 128x128 |  |  | particle, additive |
| Art/VFX/Textures/fx_splash_sheet.png | 1024x1024 |  |  | Texture Sheet Animation 4x4, 16 frames |
| Art/VFX/Textures/fx_stun_star.png | 128x128 |  |  | particle orbit, colored |
| Art/VFX/Textures/fx_water_droplet.png | 128x128 |  | #9FDDFB | particle |

## Where to use (wire when the owning task is reached; don't jump ahead of TASKS order)
- T1.4/T1.5 spray & foam: fx_water_droplet, fx_mist_puff, fx_splash_sheet (hit), fx_foam_blob, fx_bubble, fx_foam_decal (DecalProjector), fx_mud_splat (slime/re-dirt decals, M3).
- Clean 100% juice: fx_sparkle. Pay: fx_coin burst. Critter stunned: fx_stun_star orbit.
- HUD T2.5 (and any UI already built): ui_panel, ui_card (bay card), ui_bar_bg+ui_bar_fill (clean%/patience; tint fill green #4CD964 / yellow #FFC93C / red #FF5A4E), ui_slot/ui_slot_selected (tool slots), ui_crosshair/_hit (hit when spray ray hits dirtable), ui_toast, ui_btn_* (Button transition = Sprite Swap: highlighted=hover, pressed, disabled).
- Icons (tools/critters/vehicles/HUD) will come later via ChatGPT into `Art/UI/Icons/` — use placeholders until then.

## Icons (ChatGPT-generated, 34) — `Art/UI/Icons/{Tools,HUD,Critters,Vehicles,Upgrades}/ico_*.png`
- 256x256 sprites, transparent, trimmed & centered. Import: same as `Art/UI/**` (Sprite, no 9-slice border). Full list: docs/ICONS.md.
- Wire when the owning task is reached: Tools → T1.6 inventory slots (now), HUD → T2.4/T2.5, Vehicles → T2.5 bay card, Critters → M3 alerts/summary, Upgrades + ico_unlock → T4.1 shop.
- Sources (raw GPT sheets) are not in the project; regenerate only via cloud Claude.

## Environment (ChatGPT-generated, for M4.5 P6 map polish) — `Art/Environment/...`
| file | size | import | usage |
|---|---|---|---|
| Art/Environment/Backdrop/env_skyline_far.png | 1536x604 | Default, alpha transparency, Wrap U = **Mirror**, V = Clamp, mipmaps on | Distant city ring around the lot (~80-120 m): unlit/alpha-clip quads or a cylinder strip; tile mirrored (A, flipped A, ...). Sky itself = URP procedural skybox. |
| Art/Environment/Textures/tex_asphalt.png | 512 | Default, Wrap Repeat, mipmaps on, aniso 4 | road + lot ground (tiling ~4 m) |
| Art/Environment/Textures/tex_concrete_wet.png | 512 | same | bay floors (tiling ~3 m) |
| Art/Environment/Textures/tex_brick.png | 512 | same | office / alley walls (tiling ~2 m) |
| Art/Environment/Textures/tex_metal_blue.png | 512 | same | bay walls / roof sides (tiling ~2 m) |
| Art/Environment/Signs/sign_sudsy.png | 512 max side | Sprite or Default+alpha | big roadside sign on a pole at the entry |
| Art/Environment/Signs/sign_bay1..3.png | 512 wide | same | plate above each bay entrance |
| Art/Environment/Signs/sign_caution_slippery.png | 512 | same | A-frame prop near bays/foam areas (physics prop, can be knocked over) |
| Art/Environment/Signs/sign_exit.png | 512 wide | same | exit lane |
Surface textures are seamless (checked by half-offset). Signs: alpha-cut quads (URP Lit alpha clip) so they get lighting.

## 3D Models (ChatGPT-generated via code, .glb) — `Art/Models/Generated/<Category>/`
- Requires package **com.unity.cloud.gltfast** (glTFast) to import .glb. Scale meters, +Y up, front +Z, origin at ground center.
- Node contract (vehicles): Body (dirtable, has TEXCOORD_1 = UV2 non-overlapping), Glass, Wheel_FL/FR/RL/RR, Door_L, Door_R, Trunk (pivots on hinge lines), Mirror_L/R, Bumper_Front/Rear, Headlight_*, Taillight_*, Grille, empties Socket_Door_L/Socket_Door_R/Socket_Trunk/Socket_Interior. Materials: Soap Blue (body, recolor per VehicleData), Light Blue Glass (BLEND), Dark Navy Rubber, Foam White Rims, Warm Yellow Lights, Rear Red Lights.
- Replace the graybox VehicleBuilder silhouettes when present; keep DirtNetSync on Body (UV2), doors/trunk hinge nodes drive VehicleDoor.
| file | tris | notes |
|---|---|---|
| Vehicles/V01_hatchback.glb | 1868 | 3.80 x 1.73 x 1.50 m |
| Vehicles/V02_sedan.glb | 2940 | 4.6 x 1.8 x 1.45 m |
| Vehicles/V03_pickup.glb | 3508 | 5.0 x 1.9 x 1.8 m, extra node HayBales; tailgate = Trunk (hinge at bottom) |
