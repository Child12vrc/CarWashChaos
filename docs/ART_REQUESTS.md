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
