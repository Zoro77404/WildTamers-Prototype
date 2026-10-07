# PROGRESS
plan: PLAN.md
phase: 4 — Main menu, settings, pause, Arabic localization (user request in chat 2026-10-07)
status: done
tested: Phase 4
updated: 2026-10-07 20:05

## About
- Wild Tamers prototype: Pokémon GO–style, low-poly animals, PC/Editor now, phone portrait (1080x1920) target
- Unity 6000.3.10f1, URP, Input System only, glTFast for .glb
- Style: flat pastel low-poly, bright and friendly (Quaternius CC0 animals, Fredoka font)
- Languages: English + Arabic (Unity Localization 1.5.13, RTLTMPro, Tajawal); chosen in Settings, first launch follows the device language
- github: https://github.com/Zoro77404/WildTamers-Prototype

## Steps
(none — Phase 4 done)

## Done
- Phase 1 — fake map, player + fake GPS, 6 animals, spawner, starter/encounter/team UI, battle hand-off; reviewed + play-tested (2026-10-01)
- Phase 2 — turn-based battle (arena, UI, AI, juice), XP/level-ups, JSON save, active pick, reset; 13 automated battles + restart verified (2026-10-01)
- Phase 3 — Arabian roster (8 animals with description + history), starters Camel/Horse/Falcon, save v2 with migration, team select before each fight, 3v1 boss battle, "New animal!" card + tap-to-view; 22 EditMode tests, play-tested win / lose / run / restart (2026-10-06)
- Phase 4 — MainMenuScene (low-poly desert, idling animals, Play/Team/Settings/Quit), pause menu on map + battle, Settings (language, music/sfx sliders, Reset save with confirm), Unity Localization (UI + Animals tables, en + ar), RTLTMPro + Tajawal Arabic, mirrored layouts, CC0 music + sfx; 36 EditMode tests, every screen play-tested in English and Arabic (2026-10-07)

## Map
- `Assets/_Project/Scripts/` — Core (GameSession, SaveSystem, SaveMigration, PartySelection, GameConfig), Map, Player, Animals, UI, Battle, Editor (builders + Tests)
- `Assets/_Project/Scripts/Editor/*Builder.cs` — menu Wild Tamers/Build/* regenerates assets/scenes (Animal Assets, Prefabs, Map Scene, Battle Scene)
- `AnimalAssetBuilder.cs` roster = source of truth for species stats, recolors, hump/horns (texts moved to `Editor/Lang/AnimalTexts.cs`); Debug menu: Render Animal Contact Sheet, Run Balance Simulation
- `Assets/_Project/Resources/` — AnimalDatabase, GameConfig (all tuning incl. boss multipliers)
- `Assets/_Project/Scenes/MapScene.unity` — HUD, TeamPanel, EncounterPopup, TeamSelectScreen, AnimalCard, Toast
- `Assets/_Project/Scenes/BattleScene.unity` — 3 party spots + WildSpot, BattleController, BattleHUD (boss card + 3 team cards), AnimalCard
- Save file: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/WildTamers-Prototype/wildtamers_save.json` (version 2)

- `Assets/_Project/Scripts/Lang/` — `Loc` (all text + language switch), `LocalizedText` (font swap, alignment flip, ForceFix), `RTLMirror`/`RTLMirrorIgnore` (layout flip), `LocalizationFonts`
- `Assets/_Project/Scripts/Editor/Lang/` — `UiStrings.cs` (107 UI texts en+ar), `AnimalTexts.cs` (8 animals × 9 texts en+ar), `LocalizationBuilder` (menu Wild Tamers/Build/Localization rebuilds the tables), `ArabicFontBuilder` (Tajawal TMP assets)
- `Assets/_Project/Localization/` — locales, Localization Settings, string tables UI + Animals; `Assets/AddressableAssetsData/` is created by the Localization package
- `Scripts/Menu/` MainMenuController + scenery scripts; `Scripts/Audio/` AudioManager + AudioLibrary (Resources/AudioLibrary); `Scripts/UI/` SettingsPanel, PauseMenu, PauseButton, ConfirmPopup
- Scenes: MainMenuScene (build index 0), MapScene (1), BattleScene (2); builders MainMenuSceneBuilder, MenuUiBuilder (shared toast/confirm/settings/pause), ProjectSetupBuilder (audio library, build list, "Play From Main Menu" toggle)
- Settings file: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/WildTamers-Prototype/wildtamers_settings.json` (language, music, sfx); separate from the save so Reset save never touches it

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

- Phase 4 text: Unity Localization 1.5.13, string tables "UI" and "Animals" (keys `<animalId>.name/the/wild/style/description/history/attack/skill/skilldesc`), Smart Strings with real CLDR plurals (Arabic 1/2/3–10/11+). AnimalData no longer holds any text (`Localized…` properties read the table)
- Arabic is written from scratch for Saudi players, with no diacritics (RTLTMPro/TMP place marks badly), no "+" in front of numbers (bidi puts it on the wrong side of Arabic-Indic digits) and "٣٧ من ٤١" instead of "37/41" (a slash swaps the two numbers in RTL). Digits are Arabic-Indic in Arabic (RTLTMPro converts every digit through `ForceFix`)
- Text pipeline: every text is an `RTLTextMeshPro` + `LocalizedText` (UIBuild.Text adds both). English passes through untouched; Arabic gets joined letters, right-to-left, Tajawal font and mirrored alignment. Panel roots carry `RTLMirror` (flips anchors/offsets/pivots from the authored English layout); bars fill from the right via `BarAnchors`. Battle log is kept as functions (`Func<string>`) so it re-renders on a language switch; Arabic log lines show at once (a typewriter would run backwards)
- Tajawal lacks the isolated Arabic presentation forms; `ArabicFontBuilder` aliases them to the base glyphs. Fredoka fonts have Tajawal as fallback so "العربية" shows in English mode
- Language buttons never move or mirror (English left, العربية right). First launch: Arabic only if `Application.systemLanguage == Arabic`
- Pause = `Time.timeScale 0` (UI uses unscaled time). Main Menu from the map leaves at once (autosaved); from a fight it asks "Leave the battle?" and gives the fight up (the wild animal stays). Reset save: on the main menu it stays there with a toast; from pause it restarts at the map (new "New animal!" cards)
- Music: menu track also plays on the map, battle track in fights; tracks cross-fade and loop through a cross-fade near their end. Sliders map squared to loudness. Sources/credits in RequiredAssets.md
- Git: work and push on `main` (user said so 2026-10-07); one early plan commit also sits on `Sultan`

## Notes
- Asset download staging: `_downloads/` (project root, not imported, git-ignored)
- Original v1 save backup: `_backup/2026-10-06/save-before-phase3.json` (restored after the play-tests, so the next launch shows the migration)
- MCP screenshots: pass output_folder `Temp/Shots` (default lands in Assets/Screenshots); Game view size "Portrait1080x1920" was added for portrait shots
- Play-test via MCP: UI clicks = Button.onClick.Invoke via reflection; battle bot = EditorApplication.update closure pressing the action buttons

- Play-test helpers: `WildTamers.EditorTools.PlaytestHelper` (Click(buttonName), Language(bool), StartBattle(id, level), BotOn/Off, LatinTexts() = visible texts that still have English letters) via execute_code. Wait ~0.5 s after opening a panel before scanning (fade-in). The tests/play-tests rewrite the real save: back it up first (`_backup/2026-10-07/save-before-phase4-tests.json` is the pre-Phase-4 save and was restored)

## Later
- Towers can partly hide animals on the map — lower tallest buildings or add x-ray silhouettes
- Show lead animal HP on the encounter popup
- Real device pass for Arabic (Android): confirm the Addressables content builds with the player and the font atlas size is fine on low-end phones
- Optional: mirror the map HUD hint for touch controls when the GPS build lands

## Architecture (from old ProjectMemory.md)
- `ILocationProvider` (FakeLocationProvider now, GPS later), `MapView` + `MapTileProvider`, `AnimalData` SOs in `AnimalDatabase`, `GameSession` (DontDestroyOnLoad: team, wild spawns, battle hand-off, SceneFader, PreviewStudio)
- `BattleRules` + `BattleAI` are pure math (EditMode tests in `Scripts/Editor/Tests`); all tuning in `Resources/GameConfig`
- Scenes MapScene (0) and BattleScene (1) are generated by `Wild Tamers/Build/*` menu items — change the builder, then rebuild

## Next
None — Phase 4 is done. Open ideas live under "Later".
