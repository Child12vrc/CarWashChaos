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
| SW_PayTerminal | 결제기 + 모드 버튼 | 0.5 × 1.5 × 0.35 | box | front = -Z; screen, coin/card slot, 4 mode buttons (물·거품·왁스·헹굼) |
| SW_BoomArm | 회전 붐 암 (천장) | 0.3 × 0.9 × 2.8 | none | hangs from the canopy; arm along +Z; the arm slowly sways (BoomSway) |
| SW_FoamBrushHolder | 폼브러시 거치대 | 0.4 × 1.3 × 0.25 | none | wall-mounted; front = -Z |
| SW_HoseReelDrum | 호스 릴 | 0.6 × 0.6 × 0.3 | none | wall-mounted drum; axis along Z |
| SW_DrainGrate | 배수구 (바닥 중앙) | 1.6 × 0.03 × 0.6 | none | flush with the floor |
| SW_VacuumPillar | 진공 청소기 기둥 | 0.5 × 1.9 × 0.5 | box | hose + nozzle on the side; coin box |
| SW_MatRack | 매트 거치대 | 1.6 × 1.4 × 0.5 | box | 3 car mats hanging on a bar |
| SW_MatWasher | 매트 세척기 | 0.9 × 1.1 × 0.7 | box | box with a mat slot + rollers; front = -Z |
| SW_AirGun | 에어건 | 0.4 × 1.5 × 0.4 | none | post + coiled hose + gun |
| SW_RecycleBins | 분리수거함 (3칸) | 1.5 × 1 × 0.5 | box | 캔 / 플라스틱 / 일반 |
| SW_PriceBoard | 가격표 간판 (입구) | 2.4 × 3.2 × 0.2 | box | two posts + board; text faces -Z |
| SW_InfoBoard | 이용 안내판 | 1.6 × 2.2 × 0.15 | box | text faces -Z |
| SW_NoEntrySign | 진입금지 표지 | 0.7 × 2.2 × 0.1 | none | red disc + white bar; faces -Z |
| SW_RoadArrow | 노면 화살표 | 0.9 × 0.01 × 3 | none | painted, points +Z |
| SW_CoinChanger | 동전교환기 | 0.6 × 1.6 × 0.5 | box | front = -Z |
| SW_VendingMachine | 자판기 | 1 × 1.9 × 0.8 | box | glass front with drinks; front = -Z |
| SW_Bench | 벤치 | 1.6 × 0.85 × 0.5 | box | front = -Z |
| SW_Parasol | 파라솔 + 테이블 | 2.2 × 2.4 × 2.2 | none | round table + umbrella |
| SW_Cctv | CCTV 카메라 | 0.3 × 0.25 × 0.45 | none | wall / pillar mount; looks along -Z |
| SW_Extinguisher | 소화기 | 0.2 × 0.62 × 0.2 | none |  |
| SW_WaterTank | 물탱크 | 1.8 × 2.4 × 1.8 | box | big plastic tank on a stand + ladder |
| SW_ChemDrum | 약품 드럼통 | 0.6 × 0.9 × 0.6 | box | blue drum with a label |
| SW_BucketStack | 버킷 (쌓아 둔 것) | 0.4 × 0.6 × 0.4 | none |  |
| SW_TowelRack | 수건 걸이 | 1.2 × 1.3 × 0.4 | none | frame + 4 towels |
| SW_SlipperySign | 미끄럼 주의 표지 | 0.4 × 0.7 × 0.5 | none | yellow A-frame |
| SW_Pipes | 배관 (벽 따라) | 6 × 0.3 × 0.25 | none | two pipes along local X with brackets |
| SW_ElectricBox | 전기 박스 | 0.7 × 1 × 0.3 | none | wall box; front = -Z |
| SW_AcUnit | 에어컨 실외기 | 0.9 × 0.7 × 0.35 | box | fan grille on the front (-Z) |
| SW_Planter | 화분 (플랜터) | 0.8 × 0.9 × 0.8 | box |  |
| SW_Bicycle | 자전거 | 1.7 × 1 × 0.5 | none | along local X |

Also wanted: the canopy (one roof over 3 bays, 24.8 × 11.2 m, height 4.3 m, blue fascia with the name), the drying / vacuum area canopy (11.3 × 5.2 m, height 3.35 m), and the angry-face icon and ring / arrow sprites for the patience timers (G6, currently drawn in code by `UiSprites`).
<!-- /G9 self-wash props -->
