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
| Vehicles/V04_minivan.glb | 2920 | 4.8 x 1.9 x 1.8 m |
| Vehicles/B01_monster_truck.glb | 4832 | 5.2 x 3.0 x 3.2 m, wheel dia ~1.6 m, roof light bar |
| Props/P_traffic_cone.glb | 186 | 0.5 m; origin bottom center (all props) |
| Props/P_bucket.glb | 340 | 0.5 m tall with handle |
| Props/P_tire.glb | 464 | lying flat, 0.65 m |
| Props/P_trash_bag.glb | 166 | 0.63 m |
| Props/P_caution_sign.glb | 94 | 0.9 m A-frame; node SignFace has planar UV 0-1 → put Art/Environment/Signs/sign_caution_slippery.png on it |
| Props/P_crate.glb | 228 | 0.6 m cube |
| Props/P_hay_bale.glb | 176 | 1.0 x 0.5 x 0.45 m |
| Tools/T_pressure_washer_gun.glb | 260 | origin = grip, +Z forward; Socket_Nozzle (tip), Socket_HoseIn (back) |
| Tools/T_foam_cannon.glb | 248 | origin = grip; Socket_Nozzle |
| Tools/T_towel.glb | 216 | folded towel; Socket_Nozzle (wipe point) |
| Tools/T_shop_vac.glb | 662 | canister on casters + hose; Socket_Nozzle at nozzle tip |
| Tools/T_net_pole.glb | 568 | 1.87 m; Socket_Capture at hoop center |
| Critters/C01_raccoon.glb | 926 | 0.75 m incl. tail; nodes Body, Head (pivot neck), Tail (pivot base), Leg_FL/FR/BL/BR (pivot hip/shoulder), Ear_L/R; Socket_Carry, Socket_Head; root node "Critter". No rig — animate by rotating nodes in code |
| Critters/C05_possum.glb | 932 | 0.7 m incl. tail; same node set as raccoon (play-dead = roll Body 90° + legs up) |
| Critters/C03_mud_slime.glb | 580 | 0.6 m blob; Body pivot bottom center (squash/stretch scale), Eye_L/R, Drip_1..3, Socket_Carry/Head |
| Critters/C02_bee.glb | 506 | 0.14 m single bee; Body, Head, Wing_L/R (pivot at wing root → flap), Stinger; swarm = instance several |
| Critters/C01_raccoon_hy.glb | ~50k | Hunyuan3D, single mesh (1 node, no rig), PBR embedded (base 1024 jpg, normal 1024, MR 512), node X+90° (feet at y≈0 after it), 0.92 m standing pose, faces +Z. In game: scaled to 0.66 m, code animation on one pivot (hop / lean / waddle / squash, on its back when stunned); preferred over C01_raccoon.glb (part-node version kept as fallback); gameplay capsule unchanged |
| Critters/C05_possum_hy.glb | ~50k | Hunyuan3D, same format as above, 0.70 m standing pose (long tail −Z), faces +Z. In game: scaled to 0.52 m, playing dead = lies on its side; preferred over C05_possum.glb (fallback) |
| Critters/C03_mud_slime_hy.glb | ~50k | Hunyuan3D (2026-11-19), same format (node X+90 → upright), 0.85 × 0.78 × 0.87 m blob, eye toward +Z. In game: scaled to the part-node slime's width (0.81 m → ~0.73 m tall) under a "Body" pivot at the bottom centre (CritterAnimator.Slime squash / stretch; no eye / drip nodes); preferred over C03_mud_slime.glb (fallback). Gameplay sphere collider unchanged. Bees: generation failed → C02_bee.glb stays |

## Interiors (Batch F #1, 2026-11-19) — `Art/Models/Interiors/` (source: `ArtSource/Blender/Vehicles/<id>_<Name>.blend`, see ArtSource/Blender/README.md)
Assembled from the part library `Art/Models/Parts/Interior/*.glb` (16 parts, source `ArtSource/Blender/Parts/Parts_Library.blend`). Cartoon low poly, bevelled, no colliders, UV0 = box projection with 1 UV unit = 1 m (tiling fabric / carpet normals). There are 4 materials: `MAT_Fabric_Seat` is white and gets the per-car seat tint; the others are `MAT_Fabric_Carpet`, `MAT_Plastic_Dark` and `MAT_Rubber_Black`.
- **Interior_<id>.glb**: the root node `Interior_<id>` is at the cabin floor centre, and +Z is forward. The cabin box is the one VehicleBuilder already computes from the GLB Glass bounds (halfW = min(glass, body width) / 2 − 0.1, floorY = max(body min + 0.25, glass min − 0.75), rearZ = glass min z + 0.15, frontZ = glass max z − 0.25). The model's X width is **exactly 2 × halfW**, so VehicleInteriorBuilder's width scale comes out at 1.0 and the numbers below are real metres. The driver sits on the left (−X). Seats, dashboard and wheel follow the procedural layout (frontSeatZ = frontZ − 0.93, driverX = ∓0.48 halfW, seatH = clamp((belt − floor) × 0.45, 0.25, 0.42), rear bench rule), so the existing TrashSpots land on the cushions.
  - Nodes: `Floor`, `Seat_Driver`, `Seat_Passenger` (origin = the floor under the cushion centre), `RearBench` / `RearBench_2`, `RearPanel`.
  - `Dashboard` with children `InstrumentCluster`, `Vent_L`, `Vent_R`, `CenterStack`.
  - `SteeringColumn` and **`SteeringWheel`** (origin = hub; local +Z runs down the column away from the driver, local +Y = 12 o'clock). The wheel has the empties **`Hand_L` / `Hand_R`** on the rim at 10 / 2 o'clock (R 0.17 m).
  - `Console` (child `GearLever`), `Pedals`, `FloorMat_FL/FR/R1L/R1R/R2L/R2R`, `TrashBin`, `RearViewMirror` (child `AirFreshener`), and the empty **`Socket_DriverHips`** (hip joint, 0.09 m above the cushion, 0.06 m behind the seat centre).
- **InteriorDoors_<id>.glb**: inner door cards `DoorPanel_L` / `DoorPanel_R` / `DoorPanel_Rear_L` / `DoorPanel_Rear_R` (sedan), each in the vehicle frame with its origin = that door node's pivot and no rotation → parent each under `Door_*` with local pose 0 / scale 1 so it swings with the door. Replaces VehicleInteriorBuilder.DoorTrim when present.
- **Unity follow-ups (not done by the art pass):**
  1. CustomerDriver / DevChecksInterior look for `Interior/DriverSeat` (+ `Roof`) and `Interior/SteeringWheel`. With a GLB these nodes sit under `Interior/Model/Interior_<id>/…`, so map them: DriverSeat ≈ `Socket_DriverHips` − 0.09 m Y; Roof = roofY above it. The `Hand_L` / `Hand_R` empties can replace the ±0.15 m grip offsets.
  2. Load `InteriorDoors_<id>.glb`.
  3. The vehicle bodies are closed solids below the belt line: their top "deck" sits 0.06–0.24 m under the glass, and the side walls are closed except the V01 door openings. Through the glass only the parts above the deck show (seat backs, headrests, dash top, wheel, mirror). An open door shows the body's side wall, not the seats. This is a vehicle-model limit; the exterior v2 pass should cut door openings and drop the deck.

| file | tris (+door cards) | cabin W × L × floor→roof (m) | origin in vehicle (Unity) | contents |
|---|---|---|---|---|
| Interior_V01.glb | 5972 (+688) | 1.29 × 1.67 × 0.83 | (0, 0.56, −0.145) | 2 front seats (no room for a rear bench: rear glass) |
| Interior_V02.glb | 7128 (+1376) | 1.32 × 1.61 × 0.77 | (0, 0.55, −0.075) | 2 front seats + compact rear bench at z −0.64, 4 door cards |
| Interior_V03.glb | 5972 (+688) | 1.41 × 1.75 × 1.12 | (0, 0.55, 0.475) | single cab, 2 seats, cab back wall |
| Interior_V04.glb | 8284 (+688) | 1.41 × 3.10 × 1.12 | (0, 0.55, −0.177) | 2 seats + benches at z −0.51 and −1.46 |
| Interior_B01.glb | 5972 (+688) | 1.76 × 1.75 × 1.52 | (0, 1.43, 0.325) | single cab, 2 seats |

Clipping: every vertex is ray-checked against Body / Doors / Glass / Trunk (1.5 cm margin): 0 clipping vertices, except where parts pass through the body deck below the belt line (unavoidable, see 3). Previews: `docs/art_preview/Interior_<id>_{open,open_R,top}.png`.

Parts library (`Art/Models/Parts/Interior/`, origin of each part in the comments in `ArtSource/Blender/_scripts/parts_lib.py`): Seat_Bucket, Seat_Bench, SteeringWheel (+Hand_L/R), SteeringColumn, InstrumentCluster, Vent_Round, CenterStack, Console, GearLever, Pedals, FloorMat, RearViewMirror, AirFreshener, TrashBin_Small, TissueBox, DoorCard (56–1316 tris each).

## Vehicles v2 (Batch F #7, 2026-11-19) — `Art/Models/Generated/Vehicles/<file>_v2.glb` (source: the `Vehicle_v2` export collection in `ArtSource/Blender/Vehicles/<id>_<Name>.blend`, generator `_scripts/vehicles_v2.py`)
New files; the v1 GLBs are untouched, and v1 is kept inside each .blend as the hidden `Reference_v1` collection.

**Node contract (same as v1, top-level nodes, no extra root):**
- `Body`: one material = the v1 paint material, TEXCOORD_0 + **TEXCOORD_1 (UV2) regenerated with no overlap** (rasterised check: 0 overlapping texels).
- `Glass`: the same bounds as v1, so the cabin box and the Interior GLBs still fit. Its horizontal faces are removed, so no glass sheet lies across the cabin.
- `Door_L` / `Door_R` / `Trunk`: the origin = the hinge, the same position as v1.
- `Wheel_FL/FR/RL/RR`: same centres and sizes.
- `Headlight_±1`, `Taillight_±1`, `Grille`, `Bumper_Front/Rear` and `Socket_*`: same names and bounds.
- Extra v1 nodes are kept: CargoBed, HayBales, LiftedFrame, Suspension, RoofLightBar.
- `Body` bounds are identical to v1, so VehicleHeadlights still places the lights right. Exception: V04 is ±0.86 m wide instead of ±0.90, because its side trim strip now lives on the doors.

**What changed:**
1. **Body**: the v1 solid minus a cabin cavity. The cavity is x ±(halfW + 0.01), from floorY − 0.045 up to the belt line, across the glass length. This removes the deck under the glass, so the interior is fully visible through the windows. The two side-door openings are cut down to the sill.
2. **Doors**: each door is the piece of v1 body that filled its opening. It is flush with the skin, has real thickness (its inner face sits at the cavity wall) and a 6 mm seam.
   - V02's `Door_Rear_L/R` are rebuilt the same way: flush and static.
   - V01: the v1 body has no side wall at the doors, so its door is the v1 door cut at the belt line.
   - Children of each door: `DoorDetail_*` (inner door card sized to the gap left by the seats + outer handle; replaces InteriorDoors_<id>.glb and DoorTrim for v2) and **`Mirror_L/R`** (painted housing, chrome glass, arm). Mirrors still get the paint because they keep the body material and the `Mirror` name.
3. **Modular parts**, built in the v1 bounds:
   - Lamps: rubber housing, bevelled lens and round LED dots on the headlights; tail lights with a reverse strip.
   - Grille: chrome slats.
   - Bumpers: bevelled, with a trim line.
   - `LicensePlate_Front/Rear`: a Korean-style 52 × 11 cm plate from the prop atlas.
   - `Wipers` on the windscreen base.
   - Wheels: lathe tyres with grooves and shoulders, 5-spoke rims, a hub cap in the v1 hub colour and lug nuts. B01 gets chunky tread blocks.
   - `Exhaust` and `FuelCap` on every car, `RoofRack` on V01 and V04, `Antenna` on V02.
4. **Materials**: the v1 materials keep their names (Soap Blue, Dark Navy Rubber, Foam White Rims, Warm Yellow Lights, Rear Red Lights, Light Blue Glass, …). New on v2:
   - `MAT_PropMetal`: chrome
   - `MAT_PropAtlas`: plates
   - interior `MAT_*`: door cards

**Checks:**
- The Interior GLB (unchanged) against every v2 Body / Door / DoorDetail mesh: 0 BVH overlaps on all 5 cars.
- Previews: `docs/art_preview/Vehicle_<id>_v2{,_rear,_open,_open_R}.png`.

**Unity:**
- v2 is opt-in. `GlbFor` sorts `id_*.glb` and picks v1 first, so it must prefer `*_v2.glb`.
- The door box colliders get thicker, because they come from the door mesh bounds.
- The physics hull is unchanged.

| file | tris (whole car) | Body tris | notes |
|---|---|---|---|
| V01_hatchback_v2.glb | 10684 | 430 | roof rack, belt-cut v1 doors |
| V02_sedan_v2.glb | 11832 | 1074 | 4 flush doors (rear static), antenna |
| V03_pickup_v2.glb | 11584 | 1362 | cargo bed + hay bales kept |
| V04_minivan_v2.glb | 11228 | 946 | long side doors, roof rack |
| B01_monster_truck_v2.glb | 18278 | 974 | chunky tread tyres (most of the tris), light bar kept |

## Self-wash props (G9 replacements, 2026-11-19) — `Art/Models/Generated/SelfWash/SW_<id>.glb` (source: `ArtSource/Blender/Props/CarWash/SW_<id>.blend`, generator `_scripts/props_selfwash.py`)
Self-made (no matching CC0 asset exists for Korean self-wash equipment). Style: cartoon, bevelled, flat colours plus icon details. Every node keeps the graybox prefab's local frame (same pivot, front = −Z, same node names where the code uses them), so it swaps in 1:1.

Materials, at most 2–3 per prop and shared by every prop:
- `MAT_PropAtlas`: base colour = the 1024 atlas `T_PropAtlas.png` (embedded in each GLB; the source is `ArtSource/Blender/Textures/T_PropAtlas.png`). The atlas holds icons, screens and labels plus 16 flat swatches. GlbKit.Lit keeps `baseColorTexture` and reuses one material asset by name, so all props share a single material and texture at runtime.
- `MAT_PropMetal`: steel, metallic 0.85, roughness 0.32.
- `MAT_PropGlass`: transparent, where needed.

| file | tris | size W×H×D (m) | nodes / notes |
|---|---|---|---|
| SW_PayTerminal.glb | 2368 | 0.55 × 1.51 × 0.39 (incl. holster) | `Body`. Screen, 4 icon buttons (water / foam / wax / rinse, left→right, top→bottom), coin + card panel, emergency stop, hazard plinth, side vent, wand holster on +X |
| SW_BoomArm.glb | 2212 | 0.30 × 1.65 × 2.96 | origin = ceiling mount. `Pivot` = plate + column + bearing. `Arm` = node at y −0.72: truss beam along +Z, counterweight, hose with clips, rotary joint and a hose drop to y −1.62 |
| SW_FoamBrushHolder.glb | 1604 | 0.40 × 1.06 × 0.22 | wall at +Z. `Holder` = plate + foam sign + cradle + drip shelf; `Brush` = handle + head + pink bristles + foam blobs + hose |
| SW_HoseReelDrum.glb | 2880 | 0.61 × 0.96 × 0.35 | origin = drum centre. `Bracket` (wall at +Z), `Drum` (spins about Z: solid back flange, spoked front flange, wound hose, crank), `HoseOut` (drops to y −0.66) |


## PBR texture sets (third-party, CC0 — Poly Haven, no attribution required) — `Art/ThirdParty/PolyHaven/<set>/`
1k JPG: `<set>_diff_1k.jpg` albedo (sRGB), `<set>_nor_gl_1k.jpg` OpenGL normal (used as-is, import type set to Normal map), `<set>_arm_1k.jpg` R AO / G roughness / B metallic (linear). Licence: `Art/ThirdParty/PolyHaven/LICENSE_CC0_PolyHaven.txt`. The JPGs are only read: GrayboxMaterials.PbrMask packs ARM into `Art/Generated/PBR/<set>_mask.png` (R metallic, G AO, A smoothness = 1 − roughness) for URP Lit's _MetallicGlossMap + _OcclusionMap. A delivered set wins over the old single texture + generated normal (M4.6 D5).
| Set | Used for |
|---|---|
| asphalt_02 | lot ground, roads, lanes (was tex_asphalt) |
| concrete_floor_02 | bay floors, outer ground (was tex_concrete_wet) |
| red_brick_03 | office, fences, city blocks (was tex_brick) |
| box_profile_metal_sheet | bay walls (tinted blue) + city metal buildings (was tex_metal_blue) |
| painted_metal_shutter | bay shutters (were flat grey) |
| dirty_carpet | car interior floor (procedural interiors) |
| fabric_pattern_05 | seats: normal + AO/roughness only, colour = the per-car seat tint |
| brown_mud_02 | VehicleDirt mud layer: object-space triplanar albedo + normal + AO/smoothness where mud remains (_MudTexOn) |

## Player characters (third-party, CC0) — `Art/ThirdParty/Quaternius/Characters/`
Source: Quaternius "Ultimate Modular Men Pack" (Feb 2022) + "Ultimate Modular Women Pack" (Apr 2022), **Humanoid Rig** versions, CC0 1.0 (LICENSE_CC0_Quaternius.txt). Low-poly stylized, vertex/flat colors, ~same scale as YBot (≈1.8 m; verify), Unity Humanoid avatar compatible.
| file | use |
|---|---|
| Men/Male_Worker.fbx, Women/Female_Worker.fbx | default car-wash staff look (hi-vis/work clothes) |
| Men/Male_Casual.fbx, Male_Casual2.fbx, Women/Female_Casual.fbx | default player looks |
| Men/Male_Punk, Male_Beach, Male_Farmer, Male_Suit; Women/Female_Punk, Female_Formal, Female_Suit | extra looks (lobby outfit picker / future cosmetics) |
Integration: Rig = Humanoid (Create From This Model), Avatar per model; reuse existing Mixamo locomotion + animator controller via humanoid retargeting; rebuild ragdoll from Humanoid bones (RagdollBuilder already uses humanoid bone mapping); player color = tint on the shirt material/vertex-color region or keep colored outline/name tag if the mesh has no separable shirt. Lobby: each player picks one of the looks (default: P1 Male_Worker, P2 Female_Worker, P3 Male_Casual, P4 Female_Casual). Remove YBot from the shipped build once replaced (keep in project for reference).
