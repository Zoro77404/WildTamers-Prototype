# PLAN — Wild Tamers (Prototype)

## What this is
A small prototype to test the idea: a Pokémon GO–style game with cute low-poly animals instead of Pokémon. Instead of throwing balls, you fight wild animals in turn-based battles (Attack, Defend, Skill, Run). Win the fight → the animal joins your team.

This is only a test build:
- Runs on PC in the Unity Editor (mouse + keyboard). No phone build yet.
- No real GPS or Google Maps yet — a fake map stands in for it.
- Keep it small, but it must feel good to play (clean UI, simple juice).

## Build it ready for later (important)
The real game will be a phone game with a real map (GPS + Google Maps API). So:
- Player position comes through an `ILocationProvider` interface. Now: `FakeLocationProvider` (WASD / click to move). Later: a GPS provider — swap it in without touching other code.
- Map visuals sit behind a `MapView` script so the fake ground can be replaced by real map tiles later.
- All UI is built for a phone portrait screen (1080×1920, Canvas Scaler "Scale With Screen Size") with big buttons. Set the Game view to that size. Clicks = taps later.
- Animal stats live in ScriptableObjects (`AnimalData`), not hard-coded.

## Art
All animals must be low-poly (never realistic). Use free CC0 low-poly animal packs (Quaternius "Ultimate Animated Animals", Kenney) with their animations if available. The whole game matches that style: low-poly, flat colors, bright and friendly.

## Phase 1 — Map, walking, and wild animals
Goal: walk around a fake map, see wild animals, tap one to start a fight.

1. Project setup: folders, `MapScene` and `BattleScene`, both in Build Settings.
2. Fake map: big ground with a map-like look (grass, roads, parks, flat building blocks). Camera tilted top-down and following the player, like Pokémon GO. Mouse wheel zoom.
3. Player: simple avatar moved by `FakeLocationProvider` (WASD + click-to-move). A range circle around the player shows how close you must be to fight.
4. Animals: `AnimalData` ScriptableObject — name, model/prefab, max HP, attack, defense, speed, a normal attack name, and one skill (name, power, cooldown). Make 6 animals (e.g. Wolf, Fox, Bear, Eagle, Snake, Boar) with different stat styles (fast/weak, slow/tanky, etc.).
5. Starter: on first launch, pick 1 of 3 starter animals (simple screen).
6. Spawner: wild animals appear at random spots around the player (max ~8), with a level near the player's team level. Far ones despawn, new ones spawn as you walk. Small idle bounce so they feel alive.
7. Tap a wild animal: if in range → popup with name, level, small preview, and Fight / Leave buttons. If too far → short "Get closer" message.
8. Fight → load `BattleScene`, passing the wild animal + your active animal through a `GameSession` object that survives scene loads (placeholder battle screen for now with a "Back" button).
9. HUD: "Team" button opens a list of your animals (name, level, HP).

Done when: you can pick a starter, walk around, animals spawn/despawn, and tapping one in range opens the battle scene and you can come back.

## Phase 2 — Turn-based battle
Goal: the full loop works — find → fight → win → animal joins your team.

1. Battle scene: small arena, your animal on one side, the wild one on the other, good camera angle.
2. Battle UI (portrait): both names, levels, HP bars (smooth drain), a battle log line ("Wolf used Bite! 12 damage"), and 4 big buttons:
   - Attack — normal damage.
   - Defend — take half damage until your next turn.
   - Skill — strong move, then goes on cooldown (show turns left on the button).
   - Run — chance to escape based on speed; fail = lose the turn.
3. Turns: the faster animal goes first each round. Simple damage formula using attack, defense, level, and a small random spread; small chance of a critical hit.
4. Enemy AI: mostly attacks, uses its skill when ready, sometimes defends when low on HP.
5. Feel: attack lunge, hit shake/flash, floating damage numbers, faint animation, short pause between turns so it's easy to follow.
6. Win: the wild animal joins your team (full HP) and your animal gets XP; level up raises stats (show "Level up!"). Lose: your animal faints, back to the map, team heals.
7. Back on the map: the fought animal is removed. The team screen lets you pick your active animal.
8. Save: team (animals, levels, XP, active animal) saved to a JSON file and loaded on start. A small "Reset save" button for testing.

Done when: you can play the full loop many times, the team grows, levels go up, and it all still works after restarting the game.

## Later — do NOT build now
Real GPS, Google Maps API map, phone build, type advantages, items, gyms, multiplayer, online accounts.
