# CarWashChaos (working title) — agent rules
3D first-person co-op (1-4P) physics game. Run a self-service car wash; vehicles hide absurd surprises. Price $6-8, session 15-25 min.

## Stack (fixed — do not change without asking)
- Unity 6000.3.28f1, URP 17.3, C#, new Input System, PhysX (built-in). Windows dev machine.
- Project root = this folder (C:\GameDev\Projects\MyGame3D). Our code: Assets/_Project/Scripts/{Runtime,Editor}. Existing TPS prototype in Scripts/Runtime/TPS = reference only, don't delete.
- Unity exe (Hub default): "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" (if missing, ask user for path)
- Net: Netcode for GameObjects (NGO 2.x). Dev transport = Unity Transport (UTP). Release = Facepunch.Steamworks + FacepunchTransport (community transport) + Steam lobbies
- Multi-client testing: Multiplayer Play Mode (MPPM) package
- Tests: Unity Test Framework. CLI (editor must be closed):
  `Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Temp/editmode.xml -logFile -`
- Batch editor scripts: `Unity -batchmode -nographics -projectPath . -executeMethod <Class.Method> -quit -logFile -`

## Docs (read only the one you need)
- docs/GDD.md    — design, data tables (vehicles, critters, tools, upgrades, economy)
- docs/TECH.md   — architecture, systems S01-S16, netcode, dirt-painting algorithm, Unity workflow
- docs/ASSETS.md — 3D model list, poly budgets, naming/socket conventions
- docs/TASKS.md  — milestones M0-M7, task IDs, acceptance criteria. Work = pick next unchecked task.

## Code-first Unity rules (agent cannot click in the Editor)
- Prefer code over hand-edited YAML. Prefabs/scenes/ScriptableObject assets are created by Editor builder scripts under `Assets/_Project/Scripts/Editor/Builders/` (menu `CarWash/Build/...` + static method callable via `-executeMethod`). Re-running a builder must be idempotent (overwrite).
- Never hand-edit `.unity`/`.prefab` YAML except trivial value tweaks. Never edit `Library/`.
- New `.cs` file → also write its `.meta` with a fresh random 32-hex `guid` if assets must reference it before Unity imports.
- Content data source of truth: `Assets/_Project/Data/Source/*.json` → `DataBuilder` generates ScriptableObjects in `Data/Generated/`. Edit JSON, re-run builder. Never hardcode balance numbers; read `BalanceData`.
- If a step truly needs human Editor work (e.g. assigning imported art), write it under `## Manual steps` in TASKS.md with exact clicks, and continue.

## Coding rules
- Namespace `CarWash.<Area>`. One class per file, file = class name. PascalCase public, _camelCase private fields, `[SerializeField] private` over public fields.
- Assembly definitions: CarWash.Runtime, CarWash.Editor, CarWash.Tests.EditMode, CarWash.Tests.PlayMode.
- Pure logic (economy, scoring, data validation, state machines) in plain C# classes with no MonoBehaviour → EditMode tested.
- Cross-system communication via `GameEvents` static events; no `FindObjectOfType` in Update; cache refs.
- Host-authoritative: gameplay state changes only when `IsServer`. Clients send `[Rpc(SendTo.Server)]` intents.
- MULTIPLAYER-FIRST: every feature is built networked from day one (NetworkBehaviour, server authority) and verified with 2 MPPM players before its task is ticked. Singleplayer = host with no clients; no special-casing.
- Max ~300 lines/file. No new features beyond docs. Design gap → simplest option + `// DESIGN-GAP:` comment + report.
- No per-frame allocations in hot paths (spray, painter, critter AI): pool, use NonAlloc physics queries.

## Workflow
1. Read task in docs/TASKS.md → relevant section of TECH/GDD only.
2. Implement + EditMode tests for pure logic.
3. Run builders + tests via CLI; fix until green. Check compile errors in log first.
4. Tick task `[x]` in TASKS.md, append 1-line note if behavior differs from spec.
5. Summarize changed files in ≤5 lines.
