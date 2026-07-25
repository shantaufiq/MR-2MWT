# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Mixed Reality application for Meta Quest 3 that administers the **2-Minute Walk Test (2MWT)** and **6-Minute Walk Test (6MWT)** — clinical mobility/endurance assessments used in rehabilitation (e.g. post-surgery patients). The player physically walks laps around a virtual track overlaid on their real room via passthrough MR; the app tracks distance walked, step count, correct/wrong path adherence, and lap count, then reports results.

This is a **Unity project (2022.3.6f1)** targeting **Android/Meta Quest 3**, built on the Meta XR SDK (`com.meta.xr.sdk.all` 72.0.0) with OVRPlugin head tracking and the Meta Movement SDK (Mixamo-rigged avatar body tracking).

## Build / Run

There is no CLI build pipeline, linter, or test suite in this repo — this is a standard Unity Editor project:

- Open in **Unity 2022.3.6f1** (must match exactly, see `ProjectSettings/ProjectVersion.txt`).
- Build target: **Android** (ARM64), deployed to Meta Quest 3 via Build Settings → Build And Run (device connected over USB/Link).
- `Assets/Scenes/Main-Scene.unity` is the **only scene in Build Settings** — it's a single-scene app; all app states (registration, instructions, trial, main test, results) are managed in-scene rather than via scene loading.
- No `Assets/Editor` custom tooling, no `Tests/` folder, no CI config exists.

## Architecture

### Single-scene state machine

The whole app lives in `Main-Scene.unity` and is driven by `ApplicationManager.cs` (`Assets/Scripts/Main-Script/`), which owns an `AppState` enum (Startup → Registration → Instruction → Settings → Trial → MainTest → Result) and holds the session's `UserData` and `TestResultData` (one each for 2MWT and 6MWT). `CanvasManager.cs` handles panel navigation/UI transitions within that state machine.

### Test flow

`WalkTestManager.cs` orchestrates the actual test: countdown → start tracking → timer loop → capture results. Key points:
- `StartTrialTest()` / `StartMainTest()` run a 5-4-3-2-1 countdown, then start the distance tracker and the test timer in the same callback (synchronous, no timing skew between them).
- The 6-minute main test and the 2-minute test share one continuous run: `Capture2MWTAfter(120f)` snapshots results at the 2-minute mark without stopping tracking, then the full timer continues to 360s for the 6MWT snapshot. So a single physical walk produces both results.
- `_MRDistanceTracker.TotalDistance` is polled every frame only for UI (smartwatch display); there is **no sanity-check/cross-validation** of the final distance value anywhere in this file.

### Distance tracking (the core, most failure-prone subsystem)

Active tracker: `Assets/Scripts/MRWalkingTracker_Quest3_MixamoFootSteps.cs`, wired into `WalkTestManager` as `_MRDistanceTracker`. Distance is derived from **OVRPlugin head-node pose** (`OVRPlugin.GetNodePoseStateImmediate(Node.Head)`), not controllers — XZ-plane delta position per frame, gated through several filters (noise/outlier/velocity/hip/direction thresholds), batched into 0.2s "windows," and committed to `totalDistance` only if a window clears `windowThreshold`. A single `distanceScale` Inspector field is the only calibration knob (linear multiplier applied at window-commit time).

Known behavior/gotcha (fixed in this codebase, see git history): frames rejected by the velocity/hip/alignment filters used to reset the position baseline anyway, permanently discarding legitimately-walked distance during slow/hesitant movement (common at the start of a test). The fix keeps the baseline stationary on those rejections so the distance carries forward instead of being lost. Only genuine noise (`frameThreshold`) and outlier/teleport (`maxStep`) rejections reset the baseline.

Ground-truth distance for calibration in this project is computed manually as `(laps completed × track perimeter) + leftover distance to the stopping point` — not measured with an independent device — so calibration work should compare against that formula, not assume an external gold-standard.

There is a **second, unused legacy distance tracker**, `Assets/Scripts/Main-Script/DistanceTracker.cs` (CharacterController-velocity based) — not attached anywhere in `Main-Scene.unity`, kept for reference/Sandboxing scenes only. Don't confuse the two.

### Track generation & lap counting

`Assets/Scripts/Main-Script/TrackWaypointGenerator.cs` procedurally builds an oval track (two straights + two arcs, configurable `straightLength`/`radius`) anchored to a real floor plane found via `FloorAnchorSpawner.cs` (Meta MRUK floor-anchor spawning, `Assets/Scripts/`). It also owns lap counting: trigger colliders are spawned along the track (`AddComponent<TrackCheckpoint>()` at runtime — not visible as static scene references, but very much live), and `TryCountLap()` cross-checks lap validity against the distance tracker's `TotalDistance` (`getDistanceCallback`, wired from `WalkTestManager`) using a tolerance (`_lapDistanceTolerance`, default 0.7) so a lap can't be counted by clipping the checkpoint path short.

**Checkpoint gate mechanism.** The active `Waypoint Generator.prefab` (in `Main-Scene.unity`) is configured with `checkpointCount: 80`, which against the actual generated point density (~185 points for the default 15m straight / 1m radius track) yields ~93 real checkpoints (`cpTotal`, spaced every 2nd generated point). `OnCheckpointPassed(index)` requires the player to cross four gate checkpoints in order — start (`cpA`≈0), middle (`midCp`), end (`cpEndA`/`cpEndB`), back to start — before `TryCountLap()` fires; this plus the distance-tolerance check are the two independent anti-cheat layers preventing a lap from being counted by shortcutting. Only one GameObject is tagged `"Player"` (a capsule collider on `PlayerColliderFollower.cs`, which snaps to the head anchor's XZ position at a fixed Y ≈ chest height) — so checkpoint crossings are single-source, not fan-out from multiple avatar body colliders.

Known gotcha (fixed in this codebase, see git history): `ResetLapState()` used to reset `lapsCompleted`/`lapStarted`/the gate-progress flags but **not** `lastCheckpointPassed` or `isWrongWay`. Since `lapCountingEnabled` is true from app start (never gated to the active test window) and checkpoint indices restart at 0 every time the track regenerates (orientation change, floor re-anchor, or `HideTrack()` after a completed main test), a stale `lastCheckpointPassed` from a previous session/track generation would get compared against the new session's first crossed index and could spuriously fire `onWrongWay` right at the start of a fresh test. The fix resets both fields inside `ResetLapState()`.

`onReachingLap` (invoked from `TryCountLap()`) also drives item respawn: `WalkTestManager.StartMainTest()` re-enables every `ItemObject` in `_itemObjectList` on each lap completion (see below), and fills the progress `_boxFill[n-1]` UI element. This replaced an earlier, buggier respawn condition based on "≥9 items collected," which would stall forever if the player intentionally skipped exactly one item.

There is a second, larger, **unused** track generator, `Assets/Scripts/TrackWaypointGenerator_MetaMR.cs` (854 lines, MRUK-based) — confirmed not referenced in `Main-Scene.unity`. Treat it as an earlier iteration, not the active implementation.

### Gamification items

`GamificationAssetContainer.cs` (`Assets/Scripts/`) holds two pre-placed sets of `ItemObject` (`Assets/Scripts/Main-Script/ItemObject.cs`) as scene children — one laid out for a clockwise track, one for counter-clockwise — not spawned procedurally. `TrackWaypointGenerator.Generate()` calls `GamificationAssetContainer.SpawnAsset(clockwise)` at the end of track generation, which just toggles the matching parent active and hands its item list (plus score/star/box-fill UI refs) into `WalkTestManager.InitGamifiAsset(...)`. Items are enabled all at once when a test's countdown finishes, and individually disabled on pickup (`OnTriggerEnter` from the `"Player"`-tagged collider → `WalkTestManager.AddScore()`); they get re-enabled in bulk on every `onReachingLap` during the main test (see "Track generation & lap counting" above).

### Script organization (evolution, not by design)

Scripts aren't organized by a clean layering convention — they reflect iterative development:
- `Assets/Scripts/Sandboxing/` — early prototypes (`MRWalkingDistanceTracker_OVRPlugin.cs`, `PlayerDistanceTracker.cs`, `SingleFootHorizontalDistanceAndStepTracker.cs`, etc.), only referenced from `Assets/Scenes/Sandboxing/*.unity` (not in the build).
- `Assets/Scripts/` (top-level) — the active distance tracker, floor/MRUK integration, gamification container. Mixed with some now-unused iterations (`TrackWaypointGenerator_MetaMR.cs`).
- `Assets/Scripts/Main-Script/` — the current production test-flow scripts (`ApplicationManager`, `WalkTestManager`, `CanvasManager`, `TrackWaypointGenerator`, `TrackCheckpoint`, `Smartwatch`, `FormRegistration`, `ItemObject`, `UIToggleButtonsGroup`).

When investigating a feature, check whether a script attached to `Main-Scene.unity` actually matches the GUID in the `.meta` file before trusting it's the live implementation — several near-duplicate scripts exist across these three locations. (`grep` the `guid:` from the `.cs.meta` against `Assets/Scenes/Main-Scene.unity` to confirm.)

All custom project scripts compile into the default `Assembly-CSharp` — no custom `.asmdef`. Third-party assets (AutoHand, NaughtyAttributes) do have their own asmdefs.

### Notable third-party dependencies

- `com.meta.xr.sdk.all` / `com.meta.xr.sdk.interaction.ovr` — Meta XR Core/MRUK/Interaction SDK.
- `com.meta.xr.sdk.movement` (git: oculus-samples/Unity-Movement) — body tracking driving the Mixamo-rigged avatar used for foot-step/hip validation in the distance tracker.
- `Assets/Plugins/QFSW/Audio Manager Pro/` — `SFXManager`/`SFXObject`/`MusicManager` singletons used throughout for audio (`SFXManager.Main.PlayFromSFXObjectLibrary(...)`), not a project-authored system.
- Demigiant DOTween — used for UI/item tweening (e.g. `ItemObject.cs`).
