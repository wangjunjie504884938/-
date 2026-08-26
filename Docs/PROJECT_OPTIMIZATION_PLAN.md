# Project Optimization Plan

Date: 2026-06-02

## Verification Summary

- `Tuanjie.exe -batchmode -projectPath D:\unity\demo -quit` can open the project.
- `dotnet build Assembly-CSharp.csproj` succeeds with `0` errors.
- Build warnings are assembly-version conflicts from the Tuanjie/Codely bridge references, not game-code compile errors.
- A batchmode Play smoke test entry was added at `Assets/Editor/CodexSmokeTest.cs`, but Tuanjie batchmode did not complete the Play loop reliably. Use it from a normal editor session later if needed.

## P0 - Fix Runtime Entry And Black-Screen Risks

These should be done first because they affect whether the project starts consistently.

1. Align the startup scene.
   - Current Build Settings include only `Assets/Scenes/SampleScene.scene`.
   - Last editor scene setup points to `Assets/Scenes/GameScene.scene`.
   - Risk: editor play and packaged play may start from different content.
   - Decision needed: choose one official entry scene. Recommended: keep one clean `Boot.scene` or make `SampleScene.scene` the only official boot scene.

2. Clean stale scene objects.
   - `SampleScene.scene` contains an `EnemySpawner` object with a script GUID that is not present in current `Assets/Scripts`.
   - `GameScene.scene` contains an older `CharacterSlash` player workflow while the current game flow creates `PlayerController` at runtime.
   - Recommended: remove stale `EnemySpawner`; archive or delete `GameScene` old demo objects after confirming no art/reference value remains.

3. Stop rebuilding HUD every time.
   - `GameManager.BuildHUDElements()` creates many UI objects at runtime.
   - This conflicts with the current direction: authored UI/prefabs instead of code-generated UI/art.
   - Recommended: convert HUD, title, hub, stage select, shop, skill tree, equip UI into prefabs one by one. Boss HP already follows this direction.

4. Replace `DestroyImmediate` in play flow.
   - `GameManager.StartDungeon()` uses `DestroyImmediate` for `DungeonVisuals`.
   - In play mode, use `Destroy` and a controlled rebuild lifecycle.

## P1 - Code Architecture Cleanup

These are the main code-health changes.

1. Split `GameManager.cs` (currently about 1229 lines).
   - Keep only high-level flow entry.
   - Move UI construction/binding to `UIRoot` or prefab controllers.
   - Move wave, spawn, enemy kill, dungeon complete logic to `CombatDirector`.
   - Move dungeon visual lifecycle to `DungeonRuntime`.

2. Split `PlayerController.cs`.
   - `PlayerInputController`: keyboard, joystick, buttons.
   - `PlayerMotor`: movement, dash, facing.
   - `PlayerCombatController`: attacks, skills, cooldowns, damage, healing.
   - `PlayerView`: sprite animation and local VFX triggers.

3. Split `EnemyController.cs`.
   - `EnemyState`: HP, attack, defense, type, reward.
   - `EnemyBrain`: melee/ranged/elite/boss AI.
   - `EnemyCombat`: damage, death, attack execution.
   - `EnemyView`: sprite, HP bar, hit flash, death VFX.

4. Split data from runtime state.
   - `Stats.cs` should become `PlayerRuntimeStats` plus `PlayerProgressData`.
   - `Equipment.cs` should become item definitions, inventory state, and inventory service.
   - This will also make multiplayer conversion much easier.

## P2 - Performance Optimization

These improve frame stability and mobile performance.

1. Pool VFX objects.
   - `VFXHelper.cs` creates many `GameObject`s per hit, skill, death, heal, and trail.
   - Replace repeated `new GameObject`/`Destroy` with small object pools for particles, damage numbers, slash trails, and pulses.

2. Cache generated sprites/textures.
   - `DungeonVisuals`, `TitleScreen`, `EnemyHealthBar`, `PlayerController`, and `EnemyController` create `Texture2D` and `Sprite` repeatedly.
   - Cache one-pixel sprites and repeated decorative sprites by key.

3. Reduce per-frame search calls.
   - `CameraFollow` searches player by tag when target is null.
   - `EnemyController` searches player by tag when target is null.
   - `GameManager` uses `FindObjectOfType` / `FindObjectsOfType` in flow operations.
   - Use explicit registration events: player spawned, enemy spawned, enemy killed, dungeon visuals created.

4. Replace broad physics overlap scans where possible.
   - `PlayerController` and projectiles use `Physics2D.OverlapCircleAll` frequently.
   - Use non-alloc APIs or a reusable collider buffer for melee/AOE checks.

5. Convert runtime visual generation to assets.
   - Current project still creates many sprites/textures in code.
   - New direction: authored sprites, prefabs, particle systems, materials.
   - Convert in this order: HUD -> player/enemy prefabs -> projectiles/VFX -> dungeon tiles/decorations.

## P3 - Remove Or Archive Unused Code

Handle carefully. Do not delete until each item is confirmed in editor.

1. `CharacterSlash.cs`
   - Appears to belong to the old `GameScene` prototype.
   - Current flow uses `PlayerController` and `CharacterSprite`.
   - Recommended: move to `Assets/Archive` or delete after confirming `GameScene` is not needed.

2. `GameScene.scene`
   - Contains old hand-built character workflow.
   - Recommended: archive as prototype or remove from active workflow.

3. `EnemySpawner` object in `SampleScene.scene`
   - Has missing/unknown script GUID.
   - Recommended: remove object from scene.

4. Excess packages.
   - `visualscripting`, `timeline`, `collab-proxy`, multiple IDE packages, and adaptive performance may be unused.
   - Recommended: remove only after confirming no editor tooling depends on them. Start with unused runtime packages, not IDE packages.

## Multiplayer Readiness

Do not start network code before P0/P1. First make the single-player architecture network-friendly.

1. Authority split.
   - CombatDirector owns wave, spawn, damage, loot, boss, clear result.
   - Clients only display state and send input.

2. State objects.
   - Player state, enemy state, projectile state, loot state, dungeon session state.
   - These should be serializable snapshots.

3. Deterministic inputs.
   - Replace direct `Random.Range` inside gameplay results with a seeded random service for map, loot, enemy composition, and upgrade choices.

4. UI becomes local view only.
   - UI should never decide rules, rewards, enemy death, upgrades, or loot.

## Suggested Work Order

1. Create one official boot scene and remove stale `EnemySpawner`.
2. Convert the main HUD from generated code to prefab.
3. Split `GameManager` into flow and combat director.
4. Pool VFX and projectile objects.
5. Split player input/motor/combat/view.
6. Split enemy state/brain/combat/view.
7. Convert core data to serializable state for multiplayer.
8. Add proper PlayMode tests for boot, class select, stage start, wave clear, boss clear.
