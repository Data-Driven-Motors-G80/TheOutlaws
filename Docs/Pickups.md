# Risk and recovery prototype

## Current reporting slice (2026-09-27)

The current local slice leaves pickup score out of the gameplay path. Mystery pickups have four equally likely results (25% each). Distance recovery is presented as `POLICE JAMMER DEPLOYED`; nitro displays `NITRO ACTIVATED!`.

| Result | Behavior |
| --- | --- |
| Police jammer | Adds half of the maximum police-gap capacity, capped at the maximum. It does not add score or start a temporary effect. |
| Nitro | Boosts forward speed by 35% for 5 seconds by default. Replaces the current temporary driving effect; collecting nitro again refreshes its duration without stacking speed. |
| Reversed controls | Shows 3 → 2 → 1, activating reversed steering as 1 appears. Lasts 8 seconds by default, then displays CONTROLS ARE NORMAL. Duration is editable on CarPickupEffects. Fuel is unchanged and no score is added. |
| One-hit shield | Blocks the next collision that would start crash recovery. It is consumed once and cannot be stacked. |

The current main-scene slice uses the local `FuelState` owned by `CarPickupEffects`; it does not add a second score authority or a pickup score bonus. The persistent pickup panel was removed so it no longer covers the fuel or proximity bars; only the short center result/capture popup remains. The proximity recovery popup uses `PROXIMITY RESTORED` and no longer shows the old `GAP` wording or the bottom control/probability panel. The saved `PickupPlayground.unity` test scene was synchronized with the main scene after a playtest showed that its older copy had only the score header; it now contains the ProximityHUD, FuelBar, FuelMeter, ChaseMeter, and FuelTankSpawner wiring too.

The `+10` pickup reward, `+75` completion reward, and Space forfeiture rules described in the historical section below are retained only for comparison with the earlier prototype. They are not part of this reporting slice.

Local development branch: `codex/final-main-merge-check`.
Base: latest local `main` snapshot at `8aa3b0464bb82659aa10bad97bafba6eb2982e05`. The authenticated personal Chrome session verified remote `main` at `b34c307168a7df20fc22ae3521272bcd6e1e97c7` (`fixed HUD placement`); the layout intent was integrated locally, with the final FuelBar placed in the upper-left and the proximity bar in the upper-right so the two bars do not overlap. Command-line `git pull` remains unverified because Git Credential Manager could not persist the browser login.
Editor: Unity **6000.3.23f1**.

## Historical behavior from the earlier reward prototype

Optional plain cube pickups give 10 points immediately and apply one random temporary effect:

| Effect | Behavior | Duration |
| --- | --- | --- |
| Reversed controls | Left/A moves right; Right/D moves left | 5 seconds |
| Speed boost | Forward speed is multiplied by 1.35 | 5 seconds |
| Slippery steering | Lateral acceleration and deceleration are multiplied by 0.3 | 5 seconds |

Completing the 5-second effect earns another 75 points. Press **Space** to cancel early and forfeit that pending 75; already earned points remain. One effect is active at a time. A new pickup discards the old pending bonus, grants its own 10 points, and starts a new effect and 75-point challenge. This applies to repeated pickups of the same type too. Timers use scaled game time.

Colliding with an obstacle no longer slows the car. A new collision deducts `0.2` normalized proximity, which is exactly 9 m from the current police distance. Recovery distance is preserved, so a hit at the 75 m maximum leaves 66 m. A 1-second duplicate-contact protection window follows. The recovery wait still holds positive proximity recovery for 2.6 seconds, after which clean driving can recover proximity. Police travel at 14 m/s and the base car at 15 m/s; proximity starts at 45 m and is capped at 75 m. These are implementation values, not playtest findings.

At 0 m the run ends, pending rewards are lost, and movement and score stop. Press **R** to restart after capture. There is no fixed lives counter. The score combines forward road distance and already earned pickup points; sideways weaving does not farm distance points in this prototype. Pursuit is represented by functional text, not a separate police AI.

## Mechanics-only scope

Use only basic geometric placeholders and functional text. Do not add artwork, custom decorative materials, textures, icons, particle effects, decorative animation, music, or sound effects. The pickup builder uses an unanimated default cube and creates no material assets. This restriction also applies to self-made and AI-generated content.

## Try the separate scene

Use **Money Heist > Pickups > Create or Refresh Test Scene**, then play `Assets/Scenes/PickupPlayground.unity`. The builder creates a pickup prefab and a separate copy of the existing game scene. It replaces the inherited skybox with a plain background and disables post-processing and volumes. Effects are random; the first pickup is off-centre so the player can choose to approach it. Obstacle rows are 24–36 m apart, with a 15% chance of a second obstacle in the row. The scene is added to Build Settings for restart.

`GetawayChase.unity` is not edited by this builder. Refreshing the test scene replaces edits made to the generated test scene and prefab.

## Main-scene integration status

The integration is now present in `Assets/Scenes/GetawayChase.unity` on the local branch:

1. `CarPickupEffects` and `RiskRunController` are on the player car beside `AutoDriveCar`.
2. `HandleCrash` and `ChaseMeter` route impacts through the recovery model, so a collision drains proximity without changing the car speed; capture requires an explicit **R** restart.
3. A separate `PickupSystem` root owns `PickupSpawner` and `PickupHUD`. It references the existing road, player, obstacle spawner, and `MysteryPickup` prefab; pickups remain outside the obstacle-spawner hierarchy.
4. `PickupSpawner` configures the special pickup as a 50% reversed-controls, 25% proximity-recovery, or 25% one-hit-shield result. The team `ScoreMeter` remains unchanged, so this slice adds no pickup score.
5. Spacing and effect duration still need player feedback before the team decides whether to merge the tuning.

`AutoDriveCar` remains the only script that moves the vehicle. `RiskRunState` supplies one integrated forward distance per frame so pursuit and actual movement agree across effect expiry and capture. Without an enabled `CarPickupEffects` component, the original scene keeps its original driving, collision restart and distance-score behavior. Road and obstacle-generation scripts are not changed.

## Validation commands

The source-level check for the current slice passed: `FuelState` consumed and clamped fuel correctly, a seeded 1,000-draw sample produced all three random outcomes, proximity recovery adds half the maximum capacity while clamping at the maximum, and a collision drains proximity without changing the speed multiplier. Unit coverage also checks that a shield blocks one new collision, is preserved during the built-in collision protection window, and resets with the run. This does not verify Unity scene wiring or actual input/collider behavior.

The earlier risk/recovery version passed **62 state/lifecycle tests and 38 real input/physics checks** in a separate 6000.3.22f1 copy; those results covered the old collision-slowdown design and remain historical. A prior 6000.3.23f1 editor session had recompiled the earlier slice, but the current feedback-fix batch attempt stopped during license initialization before a new compile or test run. The focused pure C# and static checks were rerun after the UI and collision changes. A user runtime playtest then confirmed normal collision speed, proximity drain, capture at zero, and **R** restart in the current scene. Automated Play Mode and WebGL reruns remain an evidence gap. These checks establish behavior, not player enjoyment or balanced tuning.

A Development WebGL build of this risk/recovery version also succeeded. The local output is `Builds/LocalPreview-RiskRun-6000.3.22f1` (ignored by Git), served at `http://127.0.0.1:8766/`. Browser checks confirmed scene/text, continuing after a collision, capture, and R restart through the focused canvas. No console errors/warnings were reported during that check. The full mechanic assertions above were run in the Editor. This is a local preview, not a GitHub Pages deployment; the earlier pickup-only preview is preserved separately.

- EditMode: run assembly `MoneyHeist.Pickups.EditorTests` using Unity Test Runner.
- Scene generation: `-batchmode -executeMethod PickupDemoBuilder.CreateDemo -quit`.
- Input/physics smoke check: `-batchmode -executeMethod PickupValidation.RunSmoke -pickupSmokeReport <absolute-json-path>`; do not add `-quit`, because the runner exits when its asynchronous checks finish.
- WebGL: `-batchmode -executeMethod PickupValidation.BuildWebGL -quit`.

The smoke runner is editor-only and is attached temporarily by the validation command. It is not saved in the game scene or included in player builds. See the local course progress record for actual results; command availability alone does not indicate a pass.
