# Coding Rules — Hard Constraints

Date: 2026-08-17

## 1. UI — No Code-Generated UI

- **New UI must use prefabs.** No `new GameObject(` for UI construction (panels, buttons, text, layouts).
- Dynamic list items (friend rows, inventory slots, mail entries) must use an **item pool**, not instantiate-and-destroy.
- VFX and dungeon decorations are exempt (already pooled via `VFXPool`).
- **Acceptance**: `new GameObject` count in `Assets/Scripts/UI/` must only decrease, never increase.

## 2. PlayerPrefs — Layered Access

- **Local settings** (volume, quality, language): `PlayerPrefs` is OK.
- **Player progress** (level, gold, equipment, skills): must go through `LocalSaveRepository` or `RuntimePlayerData`. UI must not read `PlayerPrefs` directly.
- **Server-authoritative data** (gold, equipment, gacha, mail, guild, friends, leaderboard): only via `DataSyncService` / `CloudSaveManager`.
- **Volatile state** (online status, in-combat flags): never persisted to `PlayerPrefs` or long-TTL cache.

## 3. No Global Find in Hot Paths

- `FindObjectOfType` / `FindObjectsOfType` / `GameObject.Find` must NOT appear in:
  - `Update()`, `FixedUpdate()`, `LateUpdate()`
  - `StartDungeon()`, `SpawnWave()`, `TakeDamage()`, `OnEnemyKilled()`
- Startup/initialization code is exempt (acceptable to use `FindObjectOfType` in `Awake`/`Start`).
- Use `SceneRegistry`, explicit references, or event registration instead.

## 4. Large File Editing

- **No regex batch replace** on files >500 lines (07-29 VFXHelper incident).
- Use `String.Replace` or line-by-line precise edits.
- After editing reward/progression chains, verify: gold, XP, equipment, achievements, stage-clear record are ALL applied (07-23 missing GainXp incident).

## 5. Tuanjie Engine Workarounds

- Use `RectMask2D` instead of `Mask` (Mask doesn't work).
- Use `EventTrigger` for button feedback instead of `ColorTint` (ColorTint doesn't work).
- Use `Destroy()` instead of `DestroyImmediate()` in play mode (GPU Resource ID leak).
- No emoji in UI text (renders as blank). Use Chinese text labels instead.

## 6. Compilation Gate

- Every code change must compile with **0 errors** before moving on.
- Check `unity_console` after every compile — ignore GPU Resource ID warnings (Tuanjie engine bug).
- Server changes must pass `dotnet build` with 0 errors.
