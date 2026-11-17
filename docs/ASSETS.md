# ASSETS — 3D & audio list
Style: low-poly stylized, flat color via 1 shared palette atlas texture (256x256, UV0 snaps to swatches), no PBR maps except vehicles' dirt (shader). Prototype with CC0 kits (Kenney Car Kit/City Kit, Quaternius animals/characters) then replace.
Format: .fbx from Blender (Apply Scalings: FBX All, Forward -Z, Up Y, Apply Transform), 1u = 1m. Import: Generate Lightmap UVs ON for dirtable vehicles (prototype) or author UV2 manually (final), Read/Write OFF, Mesh Compression Low.
Collision naming (handled by `ModelPostprocessor` AssetPostprocessor): child mesh `*_col` → MeshCollider (static, renderer removed); `*_ccol` → convex MeshCollider (≤255 tris); `*_box` → BoxCollider fit to bounds. Sockets = empties named `Socket_<Name>` (kept as Transforms). Rig: Humanoid for player, Generic for critters.

## Vehicles (priority: MVP ✔)
Rules: separate meshes `Body` (dirtable, unique UV2), `Glass`, `Wheels`(4, no dirt), `Door_L`, `Door_R`, `Trunk` (pivot at hinge). Sockets: `Socket_Door_L`, `Socket_Door_R`, `Socket_Trunk`, `Socket_Interior`, `Socket_Surprise`, `Socket_Driver`. Collision: `Body_ccol` (≤3 convex hulls).
| id | asset | tris budget | UV2 tex | MVP |
|---|---|---|---|---|
| V01 | hatchback | 3k | 256 | ✔ |
| V02 | sedan | 3.5k | 512 | ✔ |
| V03 | pickup w/ hay bed | 4k | 512 | ✔ |
| V04 | minivan | 4k | 512 | ✔ |
| V05 | ice-cream truck | 5k | 1024 | |
| V06 | hearse | 5k | 1024 | |
| V07 | garbage truck | 7k | 1024 | |
| V08 | tank (turret Socket_Turret) | 7k | 1024 | |
| B01 | monster truck (huge wheels dirtable too) | 8k | 1024 | ✔ |
| B02-B04 | trailer / airboat / UFO | 8k | 1024 | |

## Characters
| asset | tris | rig | anims | MVP |
|---|---|---|---|---|
| Player body (third-person, seen by others) | 4k | humanoid 20-30 bones | idle, walk, run, jump, fall, knocked_down, getup, carry, spray_hold, slip | ✔ |
| Player FP arms | 1.5k | arms rig | idle, hold_spray, wipe, throw_charge, throw | ✔ |
| Color variants via shader param (4 uniform colors) + hats (6, attach `Socket_Head`) | 300 each | — | — | 4 colors ✔ |

## Critters
| id | tris | anims |
|---|---|---|
| C01 raccoon ✔ | 1.5k | idle, run, steal_grab, hide, stunned, carried, reveal |
| C02 bee swarm ✔ | 50 per bee (particles) | wing flap shader |
| C03 mud slime ✔ | 800 (blob, shape keys: squash/stretch) | hop, split, idle, frozen |
| C05 possum ✔ | 1.2k | dead, wake, bite, run, carried |
| C04 pigeon | 300 | fly, land, peck, poop |
| C06 gremlin | 2k | run, throw, laugh, carried |
| C07 ice blob | 800 | ooze |
| C08 ghost | 1k (fresnel shader) | float, push |
| C09 turret | part of tank | rotate, fire, jammed |
| C10 clown | 1k | run, honk, carried |
| C11 gator | 2.5k | crawl, charge, stunned, dragged |
| C12 alien core | 2k | pulse, spawn |

## Tools (viewmodel + world version same mesh)
| id | tris | sockets |
|---|---|---|
| T01 pressure washer gun + hose reel(world) ✔ | 1.5k | `Socket_Nozzle`, `Socket_HoseIn` |
| T02 foam cannon ✔ | 1.2k | `Socket_Nozzle` |
| T03 towel (cloth shader wave) ✔ | 200 | — |
| T04 shop vac ✔ | 1.5k | `Socket_Nozzle` |
| T06 net pole ✔ | 600 | `Socket_Capture` |
| T05 wax buffer, T07 extinguisher, T08 leaf blower | 1k each | `Socket_Nozzle` |

## Environment — Sudsy Lot
| asset | notes | MVP |
|---|---|---|
| Bay module (roof, walls, drain grate, light, button panel, hose reel, shutter) | modular, 3 instances | ✔ |
| Ground tiles: asphalt, concrete wet, curb, painted lines | tile 4m, `_col` | ✔ |
| Office building (interior: terminal, board, lockers, cage) | 8x6m | ✔ |
| Vacuum station, vending machine, dumpster, vents, fence, sign "SUDSY" | | ✔ |
| Road segment + skybox backdrop (city silhouettes, billboards) | | ✔ |
| Props: cones, bucket, sponge, trash bag, tire, crate, hay bale, chair | each ≤ 500 tris, `_ccol` | ✔ |
| Trash items (interior): cup, wrapper, bottle, diaper, fries | ≤150 tris | ✔ |
| Auto dryer arch (U06), Bay 2/3 shutters | | |

## VFX
water stream, mist, splash, foam blobs + foam decal, mud splat decal, sparkle (clean 100%), coin burst, stun stars, bee swarm, slime goo, pigeon splat decal, stinger hit screen fx (UI).

## UI art
Tool icons (8), critter icons (12), upgrade icons (10), rep stars, bay status card, font: 1 bold rounded display + 1 readable sans (OFL licensed).

## Audio list (SFX ids used in code → `Assets/_Project/Audio/SFX/<id>.ogg`, registered in `AudioLibrary` ScriptableObject by DataBuilder)
washer_loop, washer_start, washer_stop, washer_hit_metal, washer_hit_soft, foam_loop, vac_loop, vac_suck, towel_wipe, slip_whistle, knockdown, grab, throw, door_open, trunk_open, critter_reveal_sting, raccoon_chitter, bee_buzz_loop, slime_squelch, slime_split, possum_hiss, pigeon_coo, coin_pay, cash_big, angry_honk, car_arrive, car_leave, bay_button, shutter_open, ui_click, ui_buy, day_start_bell, day_end_bell.
Music: menu, day_calm(loop, layered: +drums when ≥2 critters active), boss, summary, gameover.
