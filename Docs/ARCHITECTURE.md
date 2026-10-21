# Gameplay structure

This refactor keeps the current endless-driving mechanics, rewards, tuning, tutorials, portals, missions, and analytics. It does not implement the proposed environment collectibles or collection rounds.

## Where things live

| Folder under `Assets/Scripts` | Responsibility |
| --- | --- |
| `Core/Run` | Run lifecycle, scene-to-scene snapshot, startup |
| `Core/SceneSetup` | Explicit environment references |
| `Core/Rules` | Shared state, reward selection, difficulty, obstacle and mission rules |
| `Player` | Driving, fuel, combat, pursuit, pickup effects |
| `Rewards` | Pickup objects, spawn schedules, placement and tracking |
| `World` | Road, obstacles, environment behaviours, traffic, portals |
| `UI` | HUD, meters, pickup feedback, rear-view camera |
| `Tutorial` | Existing tutorial sequence |
| `Missions` | Player profile and mission persistence |
| `Analytics` | Run tracking, report construction, upload |
| `Presentation` | Runtime materials and world labels |

Reward prefabs live in `Assets/Prefabs/Rewards`. Environment reward assets live in `Assets/Settings/Rewards`.

## Configuring an environment

Both saved scenes have an `EnvironmentRoot` on **The Outlaws Game Systems**. Assign the player, road, obstacle spawner, fuel and mystery spawners, police, score, and reward table here. The game manager and portal switcher use these references rather than searching the scene. The game manager preserves the existing rule of using only active police cars.

The reward table configures mystery-box outcomes, not the independent fuel and ammo schedules. City retains 10% reverse steering and 30% each nitro, recovery, and shield. Snow retains its initial 25% Sunny Day roll, followed by the same four-way roll if Sunny Day is not selected. Sunny Day still lasts 15 seconds. This two-stage selection preserves the original random-number sequence as well as the probabilities.

Create a table with **Assets > Create > The Outlaws > Reward Table**. Change the weighted entries to enable or disable existing rewards. Runtime timers and effect state belong to the player, never to the shared asset. New effect behaviour belongs in the effect receiver; reward timing and placement belong in the spawner.

## Optional obstacle behaviour

`ObstacleSpawner` owns row placement, pooling, spacing, difficulty, and tutorial placement. Assign an optional `ObstacleMotion` component to change movement. Snow uses `ApproachingObstacleMotion` with its original 20–30 m/s speed and 1.7 spacing multipliers; City has no motion component. Fog remains a separate optional component and now depends on the shared spawner.

The small `SnowObstacleSpawner` wrapper intentionally remains to preserve the scene's existing script GUID and execution order. Its former movement implementation now lives entirely in the reusable motion component. Tutorial placements still bypass automatic movement.

## Run lifecycle and reporting

`OutlawGameManager` controls run flow and difficulty. `RunAnalyticsReporter` builds the existing context, run, session, and tutorial-exit payloads and manages analytics callbacks. Reporting fields and event names are unchanged. `RunSession` still carries the same snapshot between environment scenes.

Fuel, ammo, and mystery boxes keep separate schedules. Fuel and ammo share `PickupTracking` for missed-pickup accounting; all three continue using the existing placement utilities.

## Unity references and checks

Moved scripts and prefabs retain their `.meta` GUIDs. Existing `MoneyHeist.Pickups` and road assembly names are retained. Small `.asmref` files keep the player effects, mystery collectible, and pickup HUD in their original assembly despite their new folders. This avoids changing serialized type identities or test dependencies.

Run **The Outlaws > Validate Environment Setup** to check both scenes for missing scripts, required references, reward settings, and snow movement configuration. Run Edit Mode tests for shared gameplay rules and seeded reward-selection parity. Playtest tutorial handoff, pickup effects, pause/restart, and City–Snow–City portal transitions after changing scene wiring.
