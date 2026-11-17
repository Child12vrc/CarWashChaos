# GDD — CarWashChaos
Notation: `→` leads to, `x` multiply, `%` percent, `[MVP]` = needed for first playable/demo.

## 1. Pillars
P1 Washer feels great: strong spray VFX/SFX, dirt visibly peels, water pushes everything.
P2 Mundane → chaos reveal: every vehicle may hide a surprise. Players expect calm; get mayhem.
P3 Friends make it funny: friendly-fire spraying, physics slapstick, proximity voice.
P4 Short & readable: 1 day ≈ 4-5 min, 1 week (5 days) ≈ 20-25 min.
P5 Solo-viable: scales customer count & critter HP by player count.

## 2. Core loop
```
Day start → customers queue → player assigns car to bay (or car auto-parks free bay)
 → wash stages (Foam→Wash→Rinse→Dry, + optional Vacuum/Wax)
 → surprise triggers (on door/trunk open, or dirt% threshold, or timer)
 → handle surprise (spray/capture/extinguish)
 → customer inspects → pays (score) → leaves
Day end (timer) → summary → pay rent → shop upgrades → next day
Week end (Day 5) → Boss vehicle → week result → unlock next week
```
Lose: money < rent at day end → "Bankrupt" → retry current week from Day 1 (unlocks persist).
Win: survive 4 weeks (full game). Demo = Week 1 only. [MVP = Week 1]

## 3. Player verbs
| Verb | Input (KB/Pad) | Notes |
|---|---|---|
| Move/Sprint/Jump/Crouch | WASD/Shift/Space/Ctrl | Foam floor = low friction (slide) |
| Use tool (primary) | LMB / RT | hold to spray |
| Alt use | RMB / LT | nozzle mode toggle (wide/narrow) |
| Interact | E / X | open door/trunk, buttons, terminal, assign bay |
| Grab/Throw | F / Y (hold-release) | physics carry; throw force charges |
| Tool slots | 1-4 / D-pad | 4 slots, tools are world objects (drop/swap/steal) |
| Ping | MMB / RB | world marker, "Look here!" |
| Emote/Voice | V push-to-talk (opt), proximity default | |

Players spraying players: knockback + "Soaked" (10% slower 3s). No damage. Heavy knockback can knock down (1.5s getup).

## 4. Tools (source: Data/Source/tools.json)
| id | name | function | removes | MVP |
|---|---|---|---|---|
| T01 | Pressure Washer | cone raycast spray, knockback | MUD(R); GREASE(G) only if foamed | ✔ |
| T02 | Foam Cannon | sprays foam layer, makes floor slippery | marks FOAM flag (needed for GREASE) | ✔ |
| T03 | Towel/Dryer | close-range wipe | SPOTS(B) (appear after rinse) | ✔ |
| T04 | Shop Vac | suck small props/trash/critters ≤ small | interior trash items | ✔ |
| T05 | Wax Buffer | optional polish → tip bonus x1.2 | adds SHINE | |
| T06 | Net Pole | capture stunned critter (carry to cage) | — | ✔ |
| T07 | Fire Extinguisher | puts out fire, freezes slime 3s | — | |
| T08 | Leaf Blower | strong push, no cleaning, dries 2x slower than towel but ranged | SPOTS(B) slow | |
Tools have weight; dropping on ground is a physics prop. Washer is hose-tethered to bay (hose length 8m, upgradeable) → can't run off with it; tether visual = rope/curve.

## 5. Dirt model
3 channels per vehicle surface: R=MUD, G=GREASE, B=SPOTS. Plus interior trash props count.
- MUD: washer removes.
- GREASE: washer removes only where FOAM applied (foam lasts 20s).
- SPOTS: spawn where rinsed (generated when MUD/GREASE removed, 40% of area), towel/dryer removes.
- Cleanliness% = 1 − weighted avg dirt (R 0.5, G 0.3, B 0.2) over vehicle coverage; trash: −2% each left.
- Re-dirt sources: pigeon droppings, mud slime, players splashing mud from ground puddles.

## 6. Vehicles (source: Data/Source/vehicles.json)
| id | name | size | base pay | time limit s | dirt profile | surprise pool | tier | MVP |
|---|---|---|---|---|---|---|---|---|
| V01 | Hatchback | S | 40 | 90 | R high | C01,C05 | 1 | ✔ |
| V02 | Sedan | M | 55 | 110 | R,B | C01,C02,C05 | 1 | ✔ |
| V03 | Pickup (hay bed) | M | 70 | 120 | R very high | C03,C02 | 1 | ✔ |
| V04 | Minivan (family) | M | 80 | 130 | G, trash x8 | C01,C04,C06 | 2 | ✔ |
| V05 | Ice-cream Truck | L | 100 | 150 | G high | C07 | 2 | |
| V06 | Hearse | L | 110 | 150 | B high | C08 | 2 | |
| V07 | Garbage Truck | XL | 160 | 200 | R,G max | C04,C02 | 3 | |
| V08 | Army Tank | XL | 220 | 220 | R max | C09 | 3 | |
| B01 | BOSS W1: Monster Truck | XL | 400 | 300 | all max | C03 x2 + C04 | boss | ✔ |
| B02 | BOSS W2: Circus Trailer | XL | 450 | 300 | all | C10 + C01 x3 | boss | |
| B03 | BOSS W3: Swamp Airboat | XL | 500 | 300 | G max | C11 | boss | |
| B04 | BOSS W4: Crashed UFO | XL | 700 | 360 | special "goo" (G) | C12 | boss | |
Fields: size, mesh scene, initial dirt seed/params, door/trunk sockets, surprise_pool(weights), surprise_chance(0.35 W1 → 0.8 W4), customer_patience.

## 7. Surprises / critters (source: Data/Source/critters.json)
All critters: `wetness` HP (washer adds wetness/s). At 100 → Stunned 6s → capturable (Net T06 or grab). Captured → carry to Animal Control cage → +$15 bonus. Escaped critters keep causing trouble until day end.
| id | name | trigger | behavior | counter | MVP |
|---|---|---|---|---|---|
| C01 | Raccoon | open door/trunk | steals nearest dropped tool, flees, hides in vents/dumpster | spray → stun → capture | ✔ |
| C02 | Bee Swarm | dirt R < 50% on pickup bed / random | chases nearest player, sting = screen wobble + slow | foam cannon kills swarm instantly; washer disperses | ✔ |
| C03 | Mud Slime | dirt washed past 30% | hops, re-dirts vehicles (R paint), splits in 2 when hit (max 3 gens) | washer shrinks; extinguisher freezes | ✔ |
| C04 | Pigeon Flock | timer random | land on clean cars, drop B/R splats | leaf blower/washer scares (no capture) | |
| C05 | Possum | open door | plays dead; if left, wakes and bites (knockdown) | grab → cage (no stun needed) | ✔ |
| C06 | Toddler-sized Gremlin "Kid" (unnamed creature) | minivan open | throws trash out, turns on bay buttons randomly | capture | |
| C07 | Melting Ice-cream Blob | ice-cream truck heat | spreads sticky floor (slow) | extinguisher | |
| C08 | Ghost | hearse | invisible except in foam/water; turns off lights, pushes players | spray while visible | |
| C09 | Turret (tank) | tank hatch opens | sprays paint pellets (re-dirt) at players | wash nozzle to jam (100 wetness) | |
| C10 | Clown Car Horde | boss | 8 tiny clowns run out, honk, steal | capture all | |
| C11 | Swamp Gator | boss | charges, knocks down | stun → 3 players drag together | |
| C12 | Alien Goo Core | boss | spawns goo slimes until core cleaned | clean core 100% | |
Critter count/HP scale: hp_mult = 1 + 0.25*(players−1).

## 8. Economy & scoring (source: Data/Source/balance.json)
```
pay = base_pay
    x clean_mult   (clean% <60:0 (refuse) | 60-79:0.6 | 80-94:1.0 | 95+:1.3)
    x speed_mult   (finish ≤50% time:1.2 | ≤100%:1.0 | over: −1%/s, min 0.5)
    + tip          (wax done: +20% base; surprise handled w/o vehicle re-dirt: +10%)
    − damage       (vehicle hit by thrown props: −5 each, cap 30%)
reputation (0-5★, start 3): +0.1 per pay≥base, −0.3 refuse/leave angry.
arrival_interval_s = lerp(30, 12, rep/5) / player_scale
rent_per_day = [60, 80, 100, 120, 150] (week1 days 1-5), +40% per week
```
Customer patience: waits in queue max 45s → leaves angry (−rep).
Start money $50. Day length 240s + overtime until bays empty (max 60s).

## 9. Upgrades (shop terminal in office; source: Data/Source/upgrades.json)
| id | name | cost | effect | max lv |
|---|---|---|---|---|
| U01 | Pressure + | 80/160/320 | clean rate +25%/lv | 3 |
| U02 | Nozzle Range | 60/120 | range 6m → 8 → 10m | 2 |
| U03 | Long Hose | 50/100 | tether 8 → 12 → 16m | 2 |
| U04 | Bay 2 / Bay 3 | 200 / 400 | open bay | 2 |
| U05 | Foam Cannon unlock | 100 | buy T02. W1 Day1 starts with washer+towel+vac+net only; Day1 vehicles have no GREASE | 1 |
| U06 | Auto Dryer Arch | 250 | bay dries SPOTS automatically 30s | 1 |
| U07 | Vending Machine | 150 | passive +$2 per customer | 1 |
| U08 | Animal Control Pro | 120 | capture bonus $15 → $30 | 1 |
| U09 | Non-slip Boots | 90 | foam slip −60% (team) | 1 |
| U10 | Tools: Extinguisher/Blower/Wax | 80 each | unlock T07/T08/T05 | 1 |
Cosmetics (hats, uniforms) unlocked by achievements; no money cost.

## 10. Week structure & difficulty
| week | vehicles tiers | surprise chance | new mechanic | boss |
|---|---|---|---|---|
| W1 | 1 (+V04 day4) | 0.35 | basics | B01 |
| W2 | 1-2 | 0.5 | pigeons, night day (day5) | B02 |
| W3 | 2-3 | 0.65 | power outage event | B03 |
| W4 | 2-3 | 0.8 | random daily modifiers | B04 |
Daily events (W2+): Rush Hour (x2 arrivals 60s), Inspection (bonus if all bays ≥90%), Heatwave (water evaporates: spots spawn faster), Outage (lights/dryer off 45s).

## 11. Map — "Sudsy Lot" (single map MVP)
Layout (top view, ~40x30m):
```
[Road/entry]→[Queue lane x4 slots]→[Bay1][Bay2][Bay3]→[Exit]
[Office: shop terminal, day board, cage(Animal Control)]  [Vacuum station]
[Back alley: dumpster(critter hide), vents, puddles(mud)]  [Spawn: player lockers]
```
Bay = 6x10m roof, drain grate, wall hose reel (washer tether anchor), button panel (lights, auto dryer, gate).
Closed bays have shutter down.

## 12. Session & social
- Host creates Steam lobby (public/friends/private), max 4, join mid-day allowed (spawn at lockers).
- Proximity voice (Steam voice) range 20m, radio upgrade? (no, out of scope).
- Pause only in solo. Host leaving → session ends with summary.
- Save: per host, week progress + unlocks. Clients keep cosmetics only.

## 13. Feel / audio-visual
Style: low-poly stylized, flat colors, bold outlines (optional post), saturated. Dirt reads as brown/black grime with normal-ish noise; clean surfaces get specular sparkle VFX at 95%+.
Juice: "SPARKLING!" text pop on 100% clean, coin burst to office on pay, screen shake on knockdown, slide whistle on slip, critter reveal sting music cue.
Camera: FP, FOV 90 (60-110 setting), viewmodel tools, headbob toggle.

## 14. Out of scope (do NOT build)
Story/cutscenes, character creator, multiple maps (post-launch), mobile, crossplay, ranked, mod support.

## 15. Steam features (M7)
Achievements (20), Rich Presence ("Week 2 Day 3 — 3 bays"), lobby invites, Steam Deck verified target (60fps low preset), demo build flag (Week 1 only).
