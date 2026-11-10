# WorldShift architecture

The current game has a level-select menu and one shared gameplay scene. Training
and all five levels load `City_WorldShift`; the selected course supplies the
environment, obstacles, ramps and checkpoints. See [scene scopes](SCENES.md).

## Current ownership

| File or system | Responsibility |
| --- | --- |
| `WorldShiftMenu` | Level select, unlock display and testing controls |
| `WorldShiftLevels` | Selected level, saved progress, retry, next level and menu flow |
| `WorldShiftLevelCourse` | Training and City/Desert/Ocean/Snow/Mountain layouts, bends, slopes, ramps and checkpoints |
| `WorldShiftCity` | Shared level simulation, controls, collisions, world switching and checkpoint retries |
| `WorldShiftLevelCollection` | Required Ocean diamonds, misses and collection reset at checkpoints |
| `WorldShiftLevelHud`, `WorldShiftLevelProps`, `WorldShiftIcons` | Level HUD, Training prompts, road signs and simple visual cues |
| `WorldShiftLevelRock`, `WorldShiftSlidingGate`, `WorldShiftLevelSandstorm` | Desert motion, release gates and dust visibility |
| `WorldShiftRoute`, `WorldShiftRelay` | Road sampling, cross-world jumps and landing protection |
| `WorldShiftPresentation`, `WorldShiftRoadside` | Environment and hazard rendering |
| `WorldShiftLevelAnalytics` | Current level attempts and events; see [analytics](ANALYTICS.md) |

Most files above live in `Assets/Scripts/WorldShift`. `AutoDriveCar` remains in
`Player/Driving` and supplies the shared driving foundation.

## Simulation boundaries

The controller advances driving, obstacle motion and collision in small substeps.
Day advances shared movers; Night freezes their current positions. Still hazards
belong to one world and show as passable dashed outlines in the other. Switching
inside an outline causes a crash. One hit retries the last checkpoint.

Ramps launch in one world and land in the other. A successful landing retains
brief protection against switching back before the road rejoins. Ocean collection
runs in the same simulation steps, before checkpoint advancement or completion,
so a missed diamond cannot be banked by crossing a checkpoint.

Presentation may preview another world without changing simulation state.
Rendering must not collect items or advance motion, checkpoints or progress.

## Retained foundations and retired scenes

`AutoDriveCar`, `RoadSegment`, `RoadCurveMesh`, `RoadMarking`, fuel/pursuit state
and runtime material helpers remain shared foundations. They must not be deleted
just because they originated in the older game.

The original World game and endless escape retain runtime classes for reference
and reuse. They are not current menu modes. Their extra scenes have been removed;
scene-dependent test suites and editor builders are preserved outside Assets in
[the retired-scene archive](Archive/RetiredScenes/README.md). Legacy WorldShift
flow entry points now select current levels instead of loading deleted variants.

## Changes and verification

- Course tuning: edit `WorldShiftLevelCourse`, then use `WorldShiftLevelTests` and `DesertLevelCourseTests` where relevant.
- Prompt changes: edit the level HUD/props, then inspect a representative render.
- Shared driving and collision changes: use level, retained City-controller and relay checks.
- Scene setup: `WorldShiftCampaignSetup` creates only missing menu/shared scenes, preserves existing scene contents and registers those two build entries.

`WorldShiftLevelTests.MenuLoadsTrainingAndAllFiveLevelsFromTheSharedScene`
checks every menu selection and return home. `WorldShiftSceneSetupTests` checks
the build list and ensures obsolete scenes are not recreated.
