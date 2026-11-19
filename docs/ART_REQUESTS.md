# ART_REQUESTS — 2D resources generated via ChatGPT (image gen)
Pipeline: request in ChatGPT → download PNG → QA (checklist) → post-process (trim, resize, alpha, pow2) → commit to `Assets/_Project/Art/UI|VFX/...` → VS Code agent wires into game. Status: TODO / REQ / OK / REDO(n).

## Global style (prepend to every prompt)
"Game asset for a stylized low-poly 3D co-op comedy game about a chaotic self-service car wash. Clean flat vector look, bold rounded shapes, thick dark navy outline (#1B2340), saturated palette: soap blue #3DB8F5, foam white #F4FBFF, mud brown #7A4E2D, warning yellow #FFC93C, cash green #4CD964, alert red #FF5A4E. No text, no letters, no watermark, no logo. Transparent background (PNG with alpha). Centered, single object, consistent lighting from top-left."

## QA checklist (reject → REDO with specific fix)
1. Real alpha transparency (no checkerboard baked, no white box). 2. No text/letters/watermark. 3. Readable at 64px (silhouette test). 4. Matches palette/outline style of already-accepted assets. 5. Single centered subject, ≤8% padding after trim. 6. Grid sheets: exact requested grid, equal cells, no overlap. 7. No copyrighted characters/brands.

## Batch A — HUD icons — DONE via ChatGPT (sheets A1,A2) (512px source → 128px in-game) [MVP]
| id | subject | out path |
|---|---|---|
| ico_tool_washer | pressure washer gun | UI/Icons/Tools |
| ico_tool_foam | foam cannon | UI/Icons/Tools |
| ico_tool_towel | folded towel | UI/Icons/Tools |
| ico_tool_vac | shop vacuum | UI/Icons/Tools |
| ico_tool_net | net pole | UI/Icons/Tools |
| ico_money | stack of cash + coin | UI/Icons/HUD |
| ico_rep_star / ico_rep_star_empty | star filled / outline-only | UI/Icons/HUD |
| ico_timer | stopwatch | UI/Icons/HUD |
| ico_clean | sparkle drop | UI/Icons/HUD |
| ico_patience | angry face clock | UI/Icons/HUD |
| ico_ping | location pin with exclamation shape (no text) | UI/Icons/HUD |

## Batch B — critter & vehicle icons [MVP] — DONE via ChatGPT (B1 critters+boss+pigeon, B2 vehicles)
raccoon, possum, mud slime, bee swarm, monster truck boss; hatchback, sedan, pickup, minivan → UI/Icons/Critters, UI/Icons/Vehicles

## Batch C — VFX textures — DONE procedurally (see ART_IMPORT.md); GPT only if style upgrade needed (grayscale/white on transparent, tintable in shader) [MVP]
| id | spec | size |
|---|---|---|
| fx_water_droplet | single droplet, soft edge | 128 |
| fx_mist_puff | soft cloud puff | 256 |
| fx_splash_sheet | 4x4 flipbook splash burst | 1024 (256 cells) |
| fx_foam_blob | bubbly foam clump | 256 |
| fx_foam_decal | top-down foam patch, tileable edges soft | 512 |
| fx_mud_splat | top-down mud splat decal | 512 |
| fx_sparkle | 4-point glint star | 128 |
| fx_coin | coin front | 128 |
| fx_stun_star | cartoon dizzy star | 128 |
| fx_bubble | soap bubble with highlight | 128 |

## Batch D — UI frames (9-slice) [MVP] — DONE procedurally (see ART_IMPORT.md)
panel_main (rounded rect, outline, soft inner shadow), btn_primary / btn_hover / btn_pressed, bay_card bg, progress_bar bg + fill, toast bg, crosshair (+ hit variant). Requirement: uniform border so 9-slice works; corner radius consistent.

## Batch E — later
DONE: upgrade icons U01-U10 + tool unlock icons (E1,E2). TODO: title logo art (text added in-engine), main menu background illustration, Steam capsule art (needs real text → handled separately).

## Batch F — 3D model upgrades (M4.6 D4, 2026-11-18) — sourced by the user (GPT / free CC0 assets), wired by the agent
Ranked by how crude they look *in normal play* (what players stare at most first). Drop the file at the path with the node rules. **Already auto-wired** (picked up on the next CI run, graybox / procedural stays the fallback): #1 interiors, #7/#8 vehicles. **Wired by the agent when the file arrives** (the path / rules below are what it will expect): #2-#6, #9, #10 — tell the agent "모델 도착".

| # | what | why it stands out | drop-in path & rules |
|---|---|---|---|
| 1 | **Car interiors** (per type) — **DONE 2026-11-19**: Interior_V01…B01.glb + InteriorDoors_<id>.glb (see ART_IMPORT.md "Interiors"; Unity wiring follow-ups listed there) | every door opening shows the procedural box cabin (M4.6 D3) | `Art/Models/Interiors/Interior_<V01…B01>.glb` or one `Interior_Default.glb`; origin = cabin floor centre, +Z forward, fits a cabin ~1.4 m wide (scaled to the car); seats / dash / wheel / door cards; no colliders needed |
| 2 | **Bay building** (walls, roof, shutters, hose reel, button panel) | players spend the whole day inside a bay; today: flat boxes + a texture | `Art/Models/Generated/Lot/Bay.glb`: 6 m × 10 m footprint, open N/S ends, nodes `ShutterN`/`ShutterS` (roll up along +Y), `Socket_HoseReel` (west wall, middle), `Socket_ButtonPanel` (east wall); keep the 3.6 m roof height |
| 3 | **Office + lockers + day board + shop terminal** | spawn point and start of every day (lockers are plain boxes) | `Lot/Office.glb` (8 × 6 m), `Lot/Locker.glb` (0.8 × 2 × 0.5 m), `Lot/DayBoard.glb` (2.4 × 1.2 m board, front −Z), `Lot/Terminal.glb` |
| 4 | **Interior trash** (cup, bottle, wrapper, fries box, diaper) | primitive cylinders / cubes, seen up close when picked up | `Art/Models/Generated/Props/T_<cup|bottle|wrapper|fries|diaper>.glb`, origin = bottom centre, ≤ 0.2 m |
| 5 | **Mud slime + bee** (part-node GLBs) | look much cruder next to the Hunyuan raccoon / possum | `Critters/C03_mud_slime_hy.glb`, `C02_bee_hy.glb`: same format as the raccoon _hy (one mesh, PBR, faces +Z); slime: squash pivot = bottom centre |
| 6 | **Dumpster, vent, animal-control cage, vacuum pedestal** | flat boxes in the alley / by the office | `Lot/Dumpster.glb` (3 × 1.5 × 1.8 m), `Lot/Vent.glb`, `Lot/Cage.glb` (2.2 m cube, door on −Z, `Socket_Inside`) |
| 7 | **Vehicles V01-V04, B01** | clean but low-poly (2-5k tris) next to 50k-tri critters | same file names / node contract as now (Body with UV2 not required any more — the dirt UV2 is regenerated; Door_L/Door_R/Trunk hinge pivots, Glass, Wheel_*, Socket_*) |
| 8 | **Graybox vehicles V05-V08, B02-B04** | only from week 2 on, so later | `Vehicles/<id>_<name>.glb`, same contract as V01 |
| 9 | **Scenery** (trees, street lamps, utility poles, parked-car variety) | mid-distance, already readable | `Art/Models/Generated/Scenery/Tree_A.glb` … (origin at the base), LOD-friendly (< 2k tris each) |
| 10 | **First-person hands** | M4.6 D8 attaches tools to the character's hands; a nicer FP arm mesh would help | Humanoid arms or reuse the Quaternius character (no extra file needed) |

## Batch G — PBR maps (M4.6 D5) — wanted from the user, the agent generates stand-ins meanwhile
The editor tool `CarWash/Build/Generate Normal Maps` makes `<name>_n.png` normal maps from the albedo luminance (stand-ins, flagged `generated` in ART_IMPORT.md). Hand-made / AI maps are better for:
| texture | needs | notes |
|---|---|---|
| tex_asphalt.png | normal + roughness (smoothness in A) + AO | cracks, aggregate; 1024, tiling |
| tex_concrete_wet.png | normal + roughness (puddle areas glossy) | 1024, tiling |
| tex_brick.png | normal + height + AO | mortar depth matters most |
| tex_metal_blue.png | normal + metallic/roughness | corrugation ridges |
| Car body dirt | dirt albedo + normal (grime noise) | 512 tiling, used by the wash shader's dirty layer |
| Fabric / plastic (interiors) | small tiling normal | 256 |

<!-- G9 self-wash props: generated by SelfWashBuilder -->
## M4.9 G9 — self-service car wash props (procedural placeholders, replace 1:1)

Each one is a prefab under `Assets/_Project/Prefabs/Props/SelfWash/SW_<id>.prefab`. Pivot: bottom centre. Front: −Z. Sizes in metres (W × H × D). Low poly, flat colours, readable at 10 m. GLB is fine. Keep the footprint, because routes and colliders use it.

| id | 이름 | size W×H×D (m) | collider | notes |
|---|---|---|---|---|
| SW_PayTerminal | 결제기 + 모드 버튼 | 0.5 × 1.5 × 0.35 | box | **Blender model in use** — front = -Z; screen, coin/card slot, 4 mode buttons (물·거품·왁스·헹굼) |
| SW_BoomArm | 회전 붐 암 (천장) | 0.3 × 0.9 × 2.8 | none | **Blender model in use** — hangs from the canopy; arm along +Z; the arm slowly sways (BoomSway) |
| SW_FoamBrushHolder | 폼브러시 거치대 | 0.4 × 1.3 × 0.25 | none | **Blender model in use** — wall-mounted; front = -Z |
| SW_HoseReelDrum | 호스 릴 | 0.6 × 0.6 × 0.3 | none | **Blender model in use** — wall-mounted drum; axis along Z |
| SW_DrainGrate | 배수구 (바닥 중앙) | 1.6 × 0.03 × 0.6 | none | flush with the floor |
| SW_VacuumPillar | 진공 청소기 기둥 | 0.5 × 1.9 × 0.5 | box | **Blender model in use** — hose + nozzle on the side; coin box |
| SW_MatRack | 매트 거치대 | 1.6 × 1.4 × 0.5 | box | **Blender model in use** — 3 car mats hanging on a bar |
| SW_MatWasher | 매트 세척기 | 0.9 × 1.1 × 0.7 | box | **Blender model in use** — box with a mat slot + rollers; front = -Z |
| SW_AirGun | 에어건 | 0.4 × 1.5 × 0.4 | none | **Blender model in use** — post + coiled hose + gun |
| SW_RecycleBins | 분리수거함 (3칸) | 1.5 × 1 × 0.5 | box | **Blender model in use** — 캔 / 플라스틱 / 일반 |
| SW_PriceBoard | 가격표 간판 (입구) | 2.4 × 3.2 × 0.2 | box | **Blender model in use** — two posts + board; text faces -Z |
| SW_InfoBoard | 이용 안내판 | 1.6 × 2.2 × 0.15 | box | **Blender model in use** — text faces -Z |
| SW_NoEntrySign | 진입금지 표지 | 0.7 × 2.2 × 0.1 | none | **Blender model in use** — red disc + white bar; faces -Z |
| SW_RoadArrow | 노면 화살표 | 0.9 × 0.01 × 3 | none | painted, points +Z |
| SW_CoinChanger | 동전교환기 | 0.6 × 1.6 × 0.5 | box | **Blender model in use** — front = -Z |
| SW_VendingMachine | 자판기 | 1 × 1.9 × 0.8 | box | **Blender model in use** — glass front with drinks; front = -Z |
| SW_Bench | 벤치 | 1.6 × 0.85 × 0.5 | box | **Blender model in use** — front = -Z |
| SW_Parasol | 파라솔 + 테이블 | 2.2 × 2.4 × 2.2 | none | **Blender model in use** — round table + umbrella |
| SW_Cctv | CCTV 카메라 | 0.3 × 0.25 × 0.45 | none | **Blender model in use** — wall / pillar mount; looks along -Z |
| SW_Extinguisher | 소화기 | 0.2 × 0.62 × 0.2 | none | **Blender model in use** —  |
| SW_WaterTank | 물탱크 | 1.8 × 2.4 × 1.8 | box | **Blender model in use** — big plastic tank on a stand + ladder |
| SW_ChemDrum | 약품 드럼통 | 0.6 × 0.9 × 0.6 | box | **Blender model in use** — blue drum with a label |
| SW_BucketStack | 버킷 (쌓아 둔 것) | 0.4 × 0.6 × 0.4 | none | **Blender model in use** —  |
| SW_TowelRack | 수건 걸이 | 1.2 × 1.3 × 0.4 | none | **Blender model in use** — frame + 4 towels |
| SW_SlipperySign | 미끄럼 주의 표지 | 0.4 × 0.7 × 0.5 | none | **Blender model in use** — yellow A-frame |
| SW_Pipes | 배관 (벽 따라) | 6 × 0.3 × 0.25 | none | **Blender model in use** — two pipes along local X with brackets |
| SW_ElectricBox | 전기 박스 | 0.7 × 1 × 0.3 | none | **Blender model in use** — wall box; front = -Z |
| SW_AcUnit | 에어컨 실외기 | 0.9 × 0.7 × 0.35 | box | **Blender model in use** — fan grille on the front (-Z) |
| SW_Planter | 화분 (플랜터) | 0.8 × 0.9 × 0.8 | box | **Blender model in use** —  |
| SW_Bicycle | 자전거 | 1.7 × 1 × 0.5 | none | **Blender model in use** — along local X |

Also wanted: the canopy (one roof over 3 bays, 24.8 × 11.2 m, height 4.3 m, blue fascia with the name), the drying / vacuum area canopy (11.3 × 5.2 m, height 3.35 m), and the angry-face icon and ring / arrow sprites for the patience timers (G6, currently drawn in code by `UiSprites`).
<!-- /G9 self-wash props -->

## Art delivery status (3D model pass, kept outside the generated G9 block so SelfWashBuilder can regenerate it)
Status: DELIVERED means the file is in place and the Unity session can wire it. Details for each file (nodes, pivots, materials) are in ART_IMPORT.md "Self-wash props". Each item also has a source .blend under `ArtSource/Blender/…` and a preview at `docs/art_preview/<id>.png`.
| id | status | file | notes for wiring |
|---|---|---|---|
| Batch F #1 interiors | DELIVERED 2026-11-19 | Art/Models/Interiors/Interior_<id>.glb, InteriorDoors_<id>.glb | see ART_IMPORT "Interiors" (follow-ups 1–3) |
| SW_PayTerminal | DELIVERED 2026-11-19 | Art/Models/Generated/SelfWash/SW_PayTerminal.glb | same frame as the graybox (pivot bottom centre, front −Z); one node `Body`; keep the box collider |
| SW_BoomArm | DELIVERED 2026-11-19 | …/SelfWash/SW_BoomArm.glb | origin = ceiling mount (like the graybox); `Pivot` (static) + `Arm` (pivot at y −0.72 → put BoomSway on it); hose drop ends at y −1.62 |
| SW_FoamBrushHolder | DELIVERED 2026-11-19 | …/SelfWash/SW_FoamBrushHolder.glb | wall at local +Z (plate back at z 0.12); `Holder` + `Brush` (separate node, could be grabbed later) |
| SW_HoseReelDrum | DELIVERED 2026-11-19 | …/SelfWash/SW_HoseReelDrum.glb | origin = drum centre (like the graybox); `Bracket` (wall plate back at z 0.155), `Drum` (spins about local Z), `HoseOut` (static drop to y −0.66) |
| SW_DrainGrate | skipped | — | already a decal since M4.10 (a mesh would z-fight the floor again) |
| Batch F #7 vehicles v2 (V01–V04, B01) | DELIVERED 2026-11-19 | Art/Models/Generated/Vehicles/<file>_v2.glb (e.g. V01_hatchback_v2.glb); v1 files untouched | **opt-in:** VehicleGlbBuilder.GlbFor picks the first `id_*.glb` alphabetically, which is still v1 → prefer `*_v2.glb`. Same node contract. With v2, skip InteriorDoors_<id>.glb and VehicleInteriorBuilder.DoorTrim (the cards are now `DoorDetail_*` under each door). See ART_IMPORT "Vehicles v2" |
| SW_Canopy | DELIVERED 2026-11-19 | …/SelfWash/SW_Canopy.glb (source ArtSource/Blender/Buildings/SW_Canopy.blend) | visual only: keep SelfWashBuilder.Canopy's colliders. Origin = ground under the roof centre = world (0, 0, 4.0). Nodes: `Roof` (+ ceiling panels), `Fascia` (N/S/E/W, yellow stripe), `Pillars` (x ±12.2 / ±4, z ±5.3, footings, hazard guards), `Lights` (9 fittings), `RoofSign` (blank board at z +5.2, y 4.75: keep the TextDecal), `Gutters` |
| SW_VacCanopy | DELIVERED 2026-11-19 | …/SelfWash/SW_VacCanopy.glb (Buildings/SW_VacCanopy.blend) | visual only. Origin = VacuumZone centre (25.25, 0, −9.4); posts at x ±5.05, z ±2.0; roof at 3.3–3.45 (graybox 3.35) |
| SW_VacuumPillar | DELIVERED 2026-11-19 | …/SelfWash/SW_VacuumPillar.glb | pivot bottom centre, front −Z; `Body` + `Hose` (loop on +X up to the holster with the nozzle) |
| SW_VendingMachine | DELIVERED 2026-11-19 | …/SelfWash/SW_VendingMachine.glb | `Body` + `Glass` (MAT_PropGlass in front of the drink shelves) |
| SW_CoinChanger | DELIVERED 2026-11-19 | …/SelfWash/SW_CoinChanger.glb | `Body`; the cream name plate at y 1.42 z −0.255 is left blank for the existing "동전교환" TextDecal |
| SW_MatRack, SW_MatWasher, SW_AirGun, SW_RecycleBins, SW_PriceBoard, SW_InfoBoard, SW_NoEntrySign, SW_Bench, SW_Parasol, SW_Cctv, SW_Extinguisher, SW_WaterTank, SW_ChemDrum, SW_BucketStack, SW_TowelRack, SW_SlipperySign, SW_Pipes, SW_ElectricBox, SW_AcUnit, SW_Planter, SW_Bicycle | DELIVERED 2026-11-19 | …/SelfWash/SW_<id>.glb (sources Props/CarWash/SW_<id>.blend) | every one keeps its graybox frame (pivot, front −Z, Cctv origin = wall mount, Pipes at y 1.3 / 1.55 along X, Bicycle along X); boards (PriceBoard / InfoBoard / CoinChanger / SlipperySign) keep blank faces where the existing TextDecals go; 300–3100 tris each |
| SW_RoadArrow | skipped | — | decal since M4.10 |
| Batch F #3 office set: Lot/Office, Lot/Locker, Lot/DayBoard, Lot/Terminal | DELIVERED 2026-11-19 | Art/Models/Generated/Lot/*.glb (sources Buildings/Office.blend, Props/Office/*.blend) | all **front = −Z**, pivot bottom centre, except DayBoard: origin = board centre, wall side at +Z 0.025. LotBuilder's office / lockers / board / terminal face +Z (towards the lot) → rotate 180° about Y. Office = 8 × 6 m shell (+ 0.3 m parapet, awning to z −3.8): `Shell` `Front` `Glass` `Awning` `Sign` (blank navy board for a "사무실" decal) `Roof` `Side`; keep its box collider. Locker: `Cabinet` + `Door` (origin = hinge on the −X edge, swings about Y) |
| Batch F #4 trash: Props/T_cup, T_bottle, T_wrapper, T_fries, T_diaper | DELIVERED 2026-11-19 | Art/Models/Generated/Props/T_*.glb | origin bottom centre, 0.05–0.23 m, 136–260 tris |
| Batch F #5 bee: Critters/C02_bee_v2.glb (+ C02_bee_hy.glb) | DELIVERED 2026-11-19 | Art/Models/Generated/Critters/ | v2 = the same contract as C02_bee.glb (root `Critter`, `Body` at (0, 0.038, 0.009 → Unity z −0.009), children `Head` (+`Socket_Head`), `Stinger`, `Wing_L/R` pivot at the wing root, `Socket_Carry`), faces +Z, 1904 tris, wings MAT_PropGlass → a drop-in for BeeFlock. `_hy` = the same bee as one mesh (no wing flap), upright, no X+90 node rotation |
| Batch F #6: Lot/Dumpster, Lot/Vent, Lot/Cage | DELIVERED 2026-11-19 | Art/Models/Generated/Lot/*.glb | pivot bottom centre, front −Z. Dumpster `Body` + `Lid` (two lids, one half-open). Cage: `Frame`, `Door` (open gate leaf, origin = hinge at (−1.05, 0, −1.08)), `Sign` (blank, for a decal), **`Socket_Inside`**; door on −Z per the request (LotBuilder's cage opens to +Z → rotate 180°). Vent: rain-hat stack up to 1.75 m |
| Batch F #9 scenery: Scenery/Tree_A (round broadleaf 5.5 m), Tree_B (conifer 5.7 m), Tree_C (street tree in a grate 4.7 m), Bush_A, StreetLamp (7.3 m, arm + LED head towards −Z), UtilityPole (10 m, cross-arms, transformer, cable stubs along X) | DELIVERED 2026-11-19 | Art/Models/Generated/Scenery/*.glb (sources Scenery/*.blend) | origin = base centre; 150–1400 tris; foliage = MAT_PropFoliage, so trees can share one material. Parked-car variety: reuse the v2 vehicles |
| Batch F #2 bay building | superseded | — | the bays are now the G9 canopy + dividers → SW_Canopy covers it |
| Batch F #8 graybox vehicles V05–V08, B02–B04 | open | — | next: same v2 pipeline from scratch (no v1 GLB to start from) |
| Batch F #8 vehicles: V05 ice-cream truck, V06 hearse, V07 garbage truck, V08 tank, B02 circus trailer, B03 swamp airboat, B04 crashed UFO | DELIVERED 2026-11-19 | Art/Models/Generated/Vehicles/<id>_<name>_v2.glb + Interiors/Interior_<id>.glb (V05/V06/V07/B02) | same v2 node contract; no v1 existed → generated v1-style bodies (sources Vehicles/<id>_<Name>.blend). **V08 / B03 / B04 have no cabin**: no doors, no Interior GLB (skip VehicleInteriorBuilder or keep its hidden procedural cabin); trunk = turret hatch (V08, C09 pops out) / side hatch (B04); B03 has no Trunk; the trash spots must sit on its `Deck` seats. See ART_IMPORT "Vehicles v2 — Batch F #8" |
| Tools v2: T_pressure_washer_gun_v2, T_foam_cannon_v2, T_towel_v2, T_shop_vac_v2, T_net_pole_v2 | DELIVERED 2026-11-19 | Art/Models/Generated/Tools/<name>_v2.glb (sources Props/Tools/*.blend) | same node names, origin (= grip) and sockets as v1 (Socket_Nozzle / Socket_HoseIn / Socket_Capture at the v1 positions), forward +Z; legacy material names kept + MAT_PropMetal (steel lance, couplers) / MAT_PropAtlas (towel print); 360–2340 tris. Shop vac hose now reaches 0.66 m wide (v1 0.45) |
| Street parked cars: Scenery/ParkedCar_Compact, ParkedCar_Sedan, ParkedCar_SUV, ParkedCar_Taxi | DELIVERED 2026-11-19 | Art/Models/Generated/Scenery/ParkedCar_*.glb | decoration only: sealed body, opaque dark glass, no doors / interior / UV2. Origin ground centre, front +Z like the customer cars; 1250–1720 tris; nodes `Body` `Glass` `Details` `Wheels` (+ `TaxiSign`); atlas swatches → recolour by swapping the swatch or tinting `Body` |

## Batch H — M4.11 performance: LODs + part merge (requested 2026-11-19, Unity session) — status: DELIVERED 2026-11-19 (H1–H4, see "Batch H delivery status" below)
Why: v0.9 runs 17–21 % slower than v0.7 (host 95 → 79 fps by day). Measured in Unity: cars 10.7–18.3k tris in 26–30 parts with 11–13 materials each; the Hunyuan critters are **50k tris each** (several on screen at once). Goal: back to ~95 fps by day, ≥ 60 fps with 4 players.
Output: new files next to the current ones, **current files untouched**: `<file>_v3.glb` for vehicles (the loader already prefers the highest `_vN`), `<file>_lod.glb` for the rest. Unity builds the LODGroup itself from the node names.

**Naming rule (all items):** each LOD mesh is a **sibling of its LOD0 node, under the same parent**, named `<Node>_LOD1` / `<Node>_LOD2` (e.g. `Door_L/DoorDetail_L_LOD1`, `Wheel_FL_LOD2`). Same origin, same transform and same material names as LOD0. If a node has no LOD for a level, Unity keeps LOD0 for that level. If a node is dropped at a level, add an empty `<Node>_LOD2` (no mesh), e.g. the LOD2 of `Wipers`, `LicensePlate_*`, `FuelCap`, lug nuts, `Antenna`.

### H1 — vehicles v2 → `_v3` (all 12: V01–V08, B01–B04)
1. **LOD0 = merge static parts.** Join every node that never moves and shares a material into one mesh per material, under one node `Static` (e.g. lamps, grille, bumpers, plates, wipers, exhaust, roof rack, cargo parts). Keep separate, with names, origins and positions unchanged: `Body`, `Glass`, `Door_*` (+ their `DoorDetail_*` and `Mirror_*` children), `Trunk`, `Wheel_*`, `Propeller` (B03), `Socket_*`, `Headlight_±1` / `Taillight_±1` (Unity looks for them by name, so an empty node at the old position is fine if their mesh goes into `Static`). Target ≤ 12 renderers per car.
2. **LOD1 (from ~15 m, ~40 % of the tris)**:
   - Wheels: 16–20 sides, no lug nuts.
   - Simpler grille slats and lamp dots.
   - **`Body_LOD1` keeps TEXCOORD_1 (UV2) with the same layout as LOD0.** The dirt / wash mask is a texture on UV2 that is shared by every LOD. Only collapse edges inside UV2 islands, never across UV2 seams, and keep 0 overlap. The same applies to the doors and the trunk, which are painted and get dirt too.
3. **LOD2 (from ~40 m, ~12 %)**:
   - Wheels: 10 sides.
   - Detail parts dropped (empty `_LOD2` nodes).
   - `Body_LOD2`: still UV2 with the LOD0 layout, coarser shape.
   - Glass: one slab.
   - Doors: shell only.

   The cars only reach LOD2 on the road (40 m+), so the doors are closed there.
4. No change to bounds, hinge origins, sockets, UV2 of LOD0, or the Body silhouette at LOD0. Re-run the 0-overlap UV2 check on every `Body*` and `Door*` mesh at every level.

### H2 — Hunyuan critters → `C01_raccoon_hy_lod.glb`, `C05_possum_hy_lod.glb`, `C03_mud_slime_hy_lod.glb`
- Same single-mesh contract as the `_hy` files (same node, same X+90 rotation, same origin / scale, same PBR material).
- Decimate: `LOD0` ≈ 6k tris, `<node>_LOD1` ≈ 1.5k tris, `<node>_LOD2` ≈ 400 tris.
- Bake the 50k detail into the LOD0 normal map if the material has one.

### H3 — interiors (`Interior_<id>.glb`, 6–8k tris)
- No LOD meshes: Unity turns the cabin off when the doors are closed and the car is far away.
- Only ask: put the seats, dashboard, steering wheel and floor under one top-level node each. Merge static parts by material the same way as H1.1, but keep `SteeringWheel`, `Hand_L/R`, `Socket_*` and the trash-spot nodes separate with unchanged names.

### H4 — scenery and big props (`_lod.glb`)
- `Tree_A/B/C`, `Bush_A`: `_LOD1` at 50 %; `_LOD2` = 2–3 crossed quads (alpha-cut foliage on MAT_PropFoliage) or a 60–100 tri blob.
- `ParkedCar_*`: `_LOD1` at ~35 % (wheels 12 sides, details merged into Body). No UV2 needed, because parked cars don't get dirty.
- `StreetLamp`, `UtilityPole`: `_LOD1` at ~40 %.
- `SW_Canopy` (7.8k), `SW_VacCanopy` (3.9k): no LOD, but merge each into ≤ 1 mesh per material, with `RoofSign` and `Lights` kept as separate nodes.

### Not needed
- Small props under 3k tris (SW_*, T_*, tools): Unity handles them with distance culling, shadow-off and static batching.
- Previews are optional: `docs/art_preview/<id>_lod.png` showing LOD0 / 1 / 2 side by side.

Priority: **H2 → H1 → H4 → H3**.

### Batch H delivery status
| Item | Status | Path | Before → after |
|---|---|---|---|
| H2 critters: C01_raccoon_hy_lod, C05_possum_hy_lod, C03_mud_slime_hy_lod | DELIVERED 2026-11-19 | Art/Models/Generated/Critters/<id>_hy_lod.glb | 50k tris / 1 mat → LOD0 6000 / LOD1 1500 / LOD2 ~400 tris, still 1 mat (re-baked shared 1024 texture set, see ART_IMPORT "Batch H") |
| H1 vehicles ×12 → `<file>_v3.glb` | DELIVERED 2026-11-19 | Art/Models/Generated/Vehicles/<file>_v3.glb | per car: draw calls 63–70 → 16–22 (LOD2 10–14), materials 11–13 → 3, renderers 26–30 → 14–18; LOD1 26–42 % / LOD2 8–16 % of the tris (B04 52 / 23 %); UV2 overlap 0 at every level. Table in ART_IMPORT "H1". Unity: trunk paint check should become `StartsWith("Trunk")` |
| H4 scenery + big props: Tree_A/B/C, Bush_A, ParkedCar_×4, StreetLamp, UtilityPole, SW_Canopy, SW_VacCanopy → `<name>_lod.glb` | DELIVERED 2026-11-19 | Art/Models/Generated/{Scenery,SelfWash}/<name>_lod.glb | trees: LOD1 50 %, LOD2 28–91 tris (blob + trunk); parked cars: LOD1 37–39 %; lamp / pole: LOD1 40 %; canopy: 6 meshes / 9 draw calls → 3 / 5, vac canopy: 5 / 7 → 2 / 3 (RoofSign / Lights kept) |
| H3 interiors ×9 → `Interior_<id>_lod.glb` | DELIVERED 2026-11-19 | Art/Models/Interiors/Interior_<id>_lod.glb | meshes 19–25 → 5, draw calls 37–47 → 14, tris unchanged; nodes Seats / Dashboard / Floor / SteeringWheel (+Hand_L/R) / TrashBin / Socket_DriverHips. Unity: the loader looks for `Interior_{id}.glb` by name → prefer `_lod` |
