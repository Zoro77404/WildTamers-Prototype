# PROGRESS
plan: PLAN.md
phase: 4 — Main menu, settings, pause, Arabic localization (no PLAN.md entry; user request in chat 2026-10-07)
status: in-progress
tested: Phase 3
updated: 2026-10-07 20:01

## About
- Wild Tamers prototype: Pokémon GO–style, low-poly animals, PC/Editor now, phone portrait (1080x1920) target
- Unity 6000.3.10f1, URP, Input System only, glTFast for .glb
- Style: flat pastel low-poly, bright and friendly (Quaternius CC0 animals, Fredoka font)
- github: https://github.com/Zoro77404/WildTamers-Prototype

## Steps
- [x] 4.1 Packages: Unity Localization, RTLTMPro, Tajawal font + Arabic TMP asset — Localization 1.5.13, RTLTMPro embedded, Tajawal static TMP fonts
- [x] 4.2 Core: Loc facade, GameSettings (saved), string tables en/ar (UI keys) — Loc facade, GameSettings, en/ar tables built (107 UI texts), plurals verified
- [x] 4.3 Arabic + English animal texts in tables (names, style, description, history, moves) — 8 animals x 9 texts written in Arabic + English, in the Animals table
- [x] 4.4 Localize every runtime script/builder text (map, battle, cards, popups, toasts) — all runtime texts via Loc, log built from functions, tests added
- [x] 4.5 RTL: text component, font swap, alignment + layout mirroring — RTLTMPro text, font swap, mirrored layouts, bars fill from right
- [x] 4.6 Audio: CC0 music + sfx, AudioManager, button/hit sounds — CC0 menu/battle music + 7 sfx, AudioManager with crossfade
- [x] 4.7 Main menu scene + animated background, Settings, Pause, Confirm popup, Reset save moved — MainMenuScene, Settings, Pause, Confirm built and play-tested en/ar
- [x] 4.8 Rebuild scenes, build settings, scene flow, first-launch language — scenes rebuilt, build list MainMenu/Map/Battle, play-from-menu, device language on first launch
- [~] 4.9 Tests + EN/AR screenshots of every screen, fix issues
- [ ] 4.10 Docs, PROGRESS, final push

## Done
- Phase 1 — fake map, player + fake GPS, 6 animals, spawner, starter/encounter/team UI, battle hand-off; reviewed + play-tested (2026-10-01)
- Phase 2 — turn-based battle (arena, UI, AI, juice), XP/level-ups, JSON save, active pick, reset; 13 automated battles + restart verified (2026-10-01)
- Phase 3 — Arabian roster (8 animals with description + history), starters Camel/Horse/Falcon, save v2 with migration, team select before each fight, 3v1 boss battle, "New animal!" card + tap-to-view; 22 EditMode tests, play-tested win / lose / run / restart (2026-10-06)

## Map
- `Assets/_Project/Scripts/` — Core (GameSession, SaveSystem, SaveMigration, PartySelection, GameConfig), Map, Player, Animals, UI, Battle, Editor (builders + Tests)
- `Assets/_Project/Scripts/Editor/*Builder.cs` — menu Wild Tamers/Build/* regenerates assets/scenes (Animal Assets, Prefabs, Map Scene, Battle Scene)
- `AnimalAssetBuilder.cs` roster = source of truth for species stats, texts, recolors, hump/horns; Debug menu: Render Animal Contact Sheet, Run Balance Simulation
- `Assets/_Project/Resources/` — AnimalDatabase, GameConfig (all tuning incl. boss multipliers)
- `Assets/_Project/Scenes/MapScene.unity` — HUD, TeamPanel, EncounterPopup, TeamSelectScreen, AnimalCard, Toast
- `Assets/_Project/Scenes/BattleScene.unity` — 3 party spots + WildSpot, BattleController, BattleHUD (boss card + 3 team cards), AnimalCard
- Save file: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/WildTamers-Prototype/wildtamers_save.json` (version 2)

## Decisions
- Input: Input System only (activeInputHandler=1) → Keyboard/Mouse.current APIs
- Roster (user, 2026-10-06): Camel, Arabian Horse, Falcon, Saluki, Arabian Oryx, Arabian Gazelle, Arabian Wolf, Arabian Fox. Closest free CC0 models, recolored: Alpaca+hump, White Horse, Eagle (shorter wings), Husky, Stag (no antlers)+straight horns, Deer+curved horns, Wolf, Fox (big ears)
- There was no Bear/Boar in the project (old roster: Wolf, Fox, Bull, Stag, Snake, Frog). Old saves: fox→Arabian Fox, wolf→Arabian Wolf, bull→Camel, stag→Gazelle, frog→Oryx, snake→Saluki (bear→Camel, boar→Oryx); level/XP/HP kept; missing starters added at level 5
- Starters: Camel (tank), Arabian Horse (fast), Falcon (glass cannon); no starter-pick screen any more — three "New animal!" cards instead
- Battle 3v1: every fighter acts once per round by speed (ties random); boss = 3× HP, +50% ATK, +10% DEF (sim: ~81% win for a sensible starter team at equal level, 65% vs +1, 45% vs +2); boss hits the lowest-HP animal 70% of the time; Run = whole team (avg speed vs boss); guard lasts until the owner's next turn
- XP after a win: every animal of the party gets the full reward (fainted ones too); wild animal joins at full HP as a normal animal
- "Healthy" for team select = not fainted and ≥25% HP; if nobody qualifies, anyone not fainted can fight; fewer than 3 able → fight with what you have
- Fainted/hurt animals stay hurt after a win and regen slowly on the map (1.5%/s); a loss heals the whole team
- Last fight team is remembered (uids in save); the map HUD chip shows its first animal
- Cards: "New animal!" shows once per species (seenSpecies in the save), on the map at start and in the battle after Continue; Team screen rows open the same card as "Animal info"
- Map scale: 1u=1m, fight range 13m, camera dist 26–90 (pitch 36–56)
- Execution order: FakeLocationProvider -200 → MapView -100 → PlayerAvatar -90 (snaps in Awake)
- UI animations use UIEase.DeltaTime (unscaled, capped 1/30 s) so load hitches don't skip them
- Battle math: dmg = 0.85·power·ATK²/(ATK+DEF)·level(±1%/lv)·spread 0.85–1.15·crit 12% ×1.5 (normal attacks only)·guard ×0.5; growth 0.06/lv
- Wild level offsets weighted -2..+2 = 25/30/25/15/5; TeamLevel = avg of top-3 levels; XP win = (10 + 8·wildLv)·gap factor; next level = 20 + 10·L

## Notes
- Asset download staging: `_downloads/` (project root, not imported, git-ignored)
- Original v1 save backup: `_backup/2026-10-06/save-before-phase3.json` (restored after the play-tests, so the next launch shows the migration)
- MCP screenshots: pass output_folder `Temp/Shots` (default lands in Assets/Screenshots); Game view size "Portrait1080x1920" was added for portrait shots
- Play-test via MCP: UI clicks = Button.onClick.Invoke via reflection; battle bot = EditorApplication.update closure pressing the action buttons

## Later
- Towers can partly hide animals on the map — lower tallest buildings or add x-ray silhouettes
- Show lead animal HP on the encounter popup

## Architecture (from old ProjectMemory.md)
- `ILocationProvider` (FakeLocationProvider now, GPS later), `MapView` + `MapTileProvider`, `AnimalData` SOs in `AnimalDatabase`, `GameSession` (DontDestroyOnLoad: team, wild spawns, battle hand-off, SceneFader, PreviewStudio)
- `BattleRules` + `BattleAI` are pure math (EditMode tests in `Scripts/Editor/Tests`); all tuning in `Resources/GameConfig`
- Scenes MapScene (0) and BattleScene (1) are generated by `Wild Tamers/Build/*` menu items — change the builder, then rebuild

## Next
None — Phase 3 is done. Open ideas live under "Later".
