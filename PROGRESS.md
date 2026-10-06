# PROGRESS
plan: PLAN.md
phase: 3 — Arabian roster + 3v1 battles (no PLAN.md entry; user request in chat 2026-10-06)
status: in-progress
tested: Phase 2
updated: 2026-10-06 18:00

## About
- Wild Tamers prototype: Pokémon GO–style, low-poly animals, PC/Editor now, phone portrait (1080x1920) target
- Unity 6000.3.10f1, URP, Input System only, glTFast for .glb
- Style: flat pastel low-poly, bright and friendly (Quaternius CC0 animals, Fredoka font)
- github: https://github.com/Zoro77404/WildTamers-Prototype

## Steps
- [x] 3.1 Models imported (Eagle Vol.2; Alpaca, White Horse, Husky, Deer UAA), licenses logged
- [x] 3.2 8 Arabian species built: data, description, history, prefabs (recolor, hump, horns); old six deleted
- [x] 3.3 Save v2 + migration (old animals → new species, missing starters added), last team, seen cards; 22 EditMode tests green
- [x] 3.4 3v1 rules: boss ×3 HP/×1.5 ATK/×1.1 DEF, speed order for 4, weakest-target AI, team run, XP for all 3; sim ~80% win
- [~] 3.5 Battle scene built (3 spots, HUD, results for 3) — needs visual check in Play Mode
- [~] 3.6 Team select screen built — needs Play Mode check
- [~] 3.7 Animal card built (map + battle + Team screen) — needs Play Mode check
- [ ] 3.8 Play-test win / lose / run / restart; tests green; docs

## Done
- Phase 1 — fake map, player + fake GPS, 6 animals, spawner, starter/encounter/team UI, battle hand-off; reviewed + play-tested (2026-10-01)
- Phase 2 — turn-based battle (arena, UI, AI, juice), XP/level-ups, JSON save, active pick, reset; 13 automated battles + restart verified (2026-10-01)

## Map
- `Assets/_Project/Scripts/` — Core (GameSession, SaveSystem, GameConfig, location), Map, Player, Animals, UI, Battle, Editor (builders + Tests)
- `Assets/_Project/Scripts/Editor/*Builder.cs` — menu Wild Tamers/Build/* regenerates assets/scenes
- `Assets/_Project/Resources/` — AnimalDatabase, GameConfig (all tuning)
- `Assets/_Project/Prefabs/Animals/` — 6 animal prefabs (AnimalVisual + Animator), WildAnimal wrapper
- `Assets/_Project/Scenes/MapScene.unity` — --Environment--/MapView, --Player--, --Systems--/MapSceneController, --UI--/MapCanvas
- `Assets/_Project/Scenes/BattleScene.unity` — --Animals--/PlayerSpot+WildSpot, --Systems--/BattleController, --UI--/BattleCanvas
- Save file: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/WildTamers-Prototype/wildtamers_save.json`

## Decisions
- Input: Input System only (activeInputHandler=1) → Keyboard/Mouse.current APIs
- Roster: Wolf, Fox, Bull, Stag (Quaternius UAA) + Snake, Frog (Quaternius Easy Enemy). No CC0 bear/eagle/boar in matching animated style → Bull=tank, Stag=glass cannon, Frog=sturdy
- Starters: Fox (fast), Frog (sturdy), Wolf (well-rounded — relabeled after rebalance)
- Quaternius Drive quota exceeded → same CC0 models via Poly Pizza GLB (Quaternius account)
- Map scale: 1u=1m, fight range 13m, camera dist 26–90 (pitch 36–56)
- Execution order: FakeLocationProvider -200 → MapView -100 → PlayerAvatar -90 (snaps in Awake)
- UI animations use UIEase.DeltaTime (unscaled, capped 1/30 s) so load hitches don't skip them
- Battle placeholder clamps tall animals (player 3.4 m, wild 3.8 m) to stay in frame
- Battle balance (Python sim): dmg = 0.85·power·ATK²/(ATK+DEF)·level(±1%/lv)·spread 0.85–1.15·crit 12% ×1.5 (normal attacks only)·guard ×0.5; ~4.5 rounds, each species 61–67% vs field at equal level; growth 0.06/lv
- Wild level offsets weighted -2..+2 = 25/30/25/15/5 (most fights winnable); TeamLevel = avg of top-3 levels
- XP: win = (10 + 8·wildLv)·gap factor; next level = 20 + 10·L
- Defend/Run resolve first in a round (priority); skill cooldown = own turns that must pass
- The fought wild animal is removed from the map after any outcome (win/lose/run)
- Team HP persists between fights; slow regen on the map (1.5%/s) so the loop stays playable
- 2.8 play-tests changed the save (incl. a reset); the pre-test save was restored afterwards (copy in `_backup/2026-10-01/save-before-2.8`)

## Notes
- Asset download staging: `_downloads/` (project root, not imported)
- Scripts backup before review fixes: `_backup/2026-10-01/Scripts`
- MCP screenshots: pass output_folder `Temp/Shots` (default lands in Assets/Screenshots)
- Play-test via MCP: UI clicks = EventSystem.RaycastAll + ExecuteEvents; map taps = MapPointerInput.HandleTap (reflection)
- Long play-tests: install an EditorApplication.update closure via execute_code after entering Play (domain reload clears it on exit); keep its log in AppDomain data and poll it

## Later
- Towers can partly hide animals on the map — lower tallest buildings or add x-ray silhouettes
- Show lead animal HP on the encounter popup

## Architecture (from old ProjectMemory.md)
- `ILocationProvider` (FakeLocationProvider now, GPS later), `MapView` + `MapTileProvider`, `AnimalData` SOs in `AnimalDatabase`, `GameSession` (DontDestroyOnLoad: team, wild spawns, battle hand-off, SceneFader, PreviewStudio)
- `BattleRules` + `BattleAI` are pure math (EditMode tests in `Scripts/Editor/Tests`); all tuning in `Resources/GameConfig`
- Scenes MapScene (0) and BattleScene (1) are generated by `Wild Tamers/Build/*` menu items — change the builder, then rebuild

## Next
None — every phase in PLAN.md is done ("Later" items are out of scope until a new plan).
