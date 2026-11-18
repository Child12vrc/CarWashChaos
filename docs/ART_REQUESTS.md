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
| 1 | **Car interiors** (per type) | every door opening shows the procedural box cabin (M4.6 D3) | `Art/Models/Interiors/Interior_<V01…B01>.glb` or one `Interior_Default.glb`; origin = cabin floor centre, +Z forward, fits a cabin ~1.4 m wide (scaled to the car); seats / dash / wheel / door cards; no colliders needed |
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
