# WorldShift architecture

The playable game uses authored finite routes and one simulation clock per active scene. The older World game remains in the repository but is a separate entry point. See [gameplay rules](WORLD_SHIFT_CAMPAIGN.md) and [scene scopes](SCENES.md).

## Ownership

| File or system | Responsibility |
| --- | --- |
| `WorldShiftFlow` | Five-environment order, scene handoff, cash-out cadence, saved bank and Garage access |
| `GetawayRunState` | Carried cash, ammo, shield and one-time settlement across environments |
| `VehicleUpgrades` | Permanent local upgrades and prices |
| `WorldShiftCity` | Finite simulation for City, Ocean, Snow and Mountain; its historical class name is retained |
| `DesertShiftTrial` | Desert simulation and its separate short development trial |
| `WorldShiftEscapeProfile` | Environment course data, curves, slopes, fuel, relays, grip and difficulty scaling |
| `WorldShiftRoute`, `WorldShiftForkRoad` | Shared road sampling, physical branches, steering commitment and cash-out side exits |
| `WorldShiftRelay`, `WorldShiftBonusJump` | Required cross-mode landings and the optional bonus ramp |
| `WorldShiftCombat` | Ammo, targeting and authoritative destroyed-obstacle state |
| `WorldShiftGetaway` | Cash collection, run HUD, pause/results and continuation handoff |
| `WorldShiftDrivingMessages` | One prompt priority order, pacing and reminders |
| `WorldShiftGetawayExtras` | Independent shield, magnet and boost pickups and active indicators |
| `WorldShiftPresentation`, `DesertShiftTrialView` | Environment rendering, hazard visibility and camera presentation |
| `WorldShiftObstacleVisual`, `WorldShiftPickupVisual`, `WorldShiftBlockVisuals` | Shared primitive visuals and meaningful labels |
| `WorldShiftMenu`, `WorldShiftHud`, `WorldShiftTutorial` | Menu/Garage, tutorial presentation and authored lessons |

Most files above live in `Assets/Scripts/WorldShift`. Shared run/resource rules live in `Assets/Scripts/Core/Rules`; `AutoDriveCar` remains in `Player/Driving`.

## Simulation boundaries

The scene controller advances driving, obstacle motion, collision, resources and rewards in small substeps. Day advances moving hazards; Nightshift retains their current positions. Both modes use the same road coordinate. Collision, targeting, steering restrictions and rendering consult the shared obstacle active-state check so a destroyed block cannot retain invisible collisions.

Presentation can temporarily render another mode without changing the player's world, clocks or resources. Prompts read simulation state; rendering must not collect cash, finish a leg or reset a Nightshift visit.

A successful main relay landing retains a brief one-world deck. Returning too early is ignored rather than killing the run. At an ordinary environment boundary, continuation is automatic. At designated cash-out legs, only the explicit side exit settles the haul.

## Shared foundations and legacy scope

`AutoDriveCar`, `RoadSegment`, `RoadCurveMesh`, `RoadMarking`, `FuelState`, `RiskRunState` and runtime material helpers are still shared foundations. Do not remove them because they originated in the older game.

The legacy `Env_*` scenes use `OutlawGameManager`, `EnvironmentRoot`, `RunSession`, mystery rewards and their own portal/progression systems. Ocean surf and Snow slide controllers remain there; they are not attached to the current finite profiles. Their architecture is preserved in [Legacy/World architecture](Legacy/WORLD_ARCHITECTURE.md).

The existing analytics documentation covers the legacy reporter. The WorldShift controllers currently do not invoke that reporter; its presence is not evidence that current Escape emits the old events.

## Changes and verification

- Course/road tuning: edit `WorldShiftEscapeProfile` or the Desert authored data, then use the closest course checks.
- Prompt/copy changes: use `WorldShiftDrivingMessages` or tutorial copy, then inspect the diff; a full gameplay suite is unnecessary by default.
- Shared collision/mode changes: check combat/routes, environment flow and relay coverage.
- Scene setup: `Assets/Editor/WorldShiftCampaignSetup.cs` creates missing scenes without replacing existing assets. Current tools are grouped separately from Legacy and Validation menus.

Relevant tests: `WorldShiftEnvironmentFlowTests`, `WorldShiftCombatRoutesTests`, `WorldShiftGetawayTests`, `WorldShiftCampaignTests`, `WorldShiftRelayTests` and `WorldShiftSceneSetupTests`. Retain focused checks; archived documents are historical records, not current passing-test guarantees.
