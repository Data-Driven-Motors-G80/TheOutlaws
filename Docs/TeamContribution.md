# Team contribution record

Updated: 2026-09-27

## Version boundary

- Team baseline: latest local `main` snapshot, commit `8aa3b0464bb82659aa10bad97bafba6eb2982e05`.
- Remote check: personal Chrome verified `main` at `b34c307168a7df20fc22ae3521272bcd6e1e97c7` (`fixed HUD placement`), a scene-only commit. Its HUD placement intent is applied locally with FuelBar in the upper-left and proximity in the upper-right to keep both visible. This was a browser-backed verification rather than a completed command-line pull.
- Earlier integration snapshot: `e97b934`.
- Current local branch: `codex/final-main-merge-check`.
- The team baseline remains recoverable as the parent commit; no remote branch was changed.

## Team baseline work

The team version supplies the endless road, automatic forward driving, steering input, obstacle generation, chase cars, proximity meter, distance score, scene structure, and the original immediate restart behavior after a fatal crash.

## Our work

### New gameplay systems

- Added `MysteryPickup` and `PickupSpawner`.
- Added a hidden 50/25/25 pickup outcome: reversed steering for five seconds, half-gap recovery, or a one-hit shield.
- Added a one-hit shield that blocks the next collision that would start crash recovery, then clears itself.
- Added the local `FuelState` model and fuel drain display.
- Added `RiskRunState` and `RiskRunController` so a crash drains proximity without slowing the car, proximity recovers after clean driving, capture stops the run, and **R** restarts it.
- Kept the team `ScoreMeter` unchanged; pickups do not add score in this slice.

### Main-scene integration

- Added `CarPickupEffects` and `RiskRunController` to the player car.
- Updated `AutoDriveCar`, `HandleCrash`, and `ChaseMeter` to use the shared recovery clock and to avoid immediate scene reload on ordinary collisions.
- Added a `PickupSystem` root to `GetawayChase.unity` with the pickup spawner and HUD.

### Player feedback update

- Removed the persistent pickup panel so it no longer covers the fuel or proximity bars.
- Removed the bottom controls/probability prompt and the old `GAP` wording.
- Kept a two-second center popup for actionable pickup results:
  - `PROXIMITY RESTORED` / `Police distance recovered`
  - `CONTROLS REVERSED!` / `A / Left: move right     D / Right: move left`
  - `SHIELD READY!` / `Next hit is blocked`
  - `HIT BLOCKED!` / `Shield used`
- Added `PickupCount` so repeated pickups with the same result can still trigger a new popup.

## Current player-facing prompts

- Fuel and proximity remain in their existing scene bars without an overlapping pickup panel.
- Center pickup popup: `PROXIMITY RESTORED`, `CONTROLS REVERSED!`, `SHIELD READY!`, or `HIT BLOCKED!`.
- Capture popup: `CAUGHT!` / `Press R to restart`.
- The saved `PickupPlayground.unity` test scene was missing the newer HUD copy; it now has both bars in separate top corners plus the fuel/proximity runtime references.

## Verification boundary

- A prior Unity `6000.3.23f1` session recompiled the earlier slice; the current feedback-fix batch attempt stopped during license initialization before a new compile.
- The feedback fix is source-checked for an unobstructed HUD, no bottom panel/GAP wording, no crash speed multiplier, and proximity drain on collision.
- A direct source-level check confirmed all three random outcomes, one-use shield behavior, preservation during existing collision protection, and ordinary-crash fallback (`reverse=527`, `proximity=226`, `shield=247` from 1000 seeded draws).
- Full EditMode/live collision verification and WebGL build remain open if the current Unity license check exits with code 198; player balance and readability still need team playtest feedback.
- User runtime playtest of the current scene confirmed the core feedback loop: collision speed stays normal, proximity decreases, and zero proximity permits **R** restart. Automated Unity reruns and WebGL publication are still separate verification items.

## Files owned by our slice

- `Assets/Scripts/Pickups/`
- `Assets/Scripts/PickupIntegration/`
- `Assets/Prefabs/Pickups/MysteryPickup.prefab`
- `Assets/Editor/Pickups/`
- `Assets/Tests/Editor/`
- `Assets/Scenes/PickupPlayground.unity`
- `Assets/Scenes/GetawayChase.unity` integration objects
- `Assets/Scripts/Car/AutoDriveCar.cs`
- `Assets/Scripts/Car/HandleCrash.cs`
- `Assets/Scripts/HUD/Proximity/ChaseMeter.cs`
