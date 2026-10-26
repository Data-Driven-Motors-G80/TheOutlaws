# Gameplay structure

The endless chase now uses a one-slot mystery-reward backpack. Space fires the equipped three-shot gun or activates the stored positive item. No starting ammo or independent ammo pickups are supplied. Environment collectibles and world switching are not implemented.

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

The reward table configures mystery outcomes. City weights are reverse 1, gun 3, nitro 2, jammer 2, shield 2. Snow first rolls 25% Sunny Day, then uses the same weights. Positive rewards replace the stored item without activating it; unused bullets are discarded. Reverse controls keep their warning and apply without replacing the backpack. Activated effects survive later pickups.

BackpackState owns the item and shots; CarPickupEffects receives and activates rewards. OutlawShooting is the sole Space input reader. A newly collected reward cannot be activated in the same frame. Empty-slot input is ignored. Positive effects are not stacked beyond their existing rules.

PickupSpawner creates forced three-lane rows, targeting the first encounter after five active seconds and subsequent encounters twenty seconds after resolution. Each row grants exactly one reward and is counted as one encounter. Boxes are placed ahead using current speed and safe approaches; portal transitions may defer rows. Active effects no longer suppress rewards. The old ammo spawner component remains serialized for compatibility but is disabled by the game manager.

Create a table with **Assets > Create > The Outlaws > Reward Table**. Change the weighted entries to enable or disable existing rewards. Runtime timers and effect state belong to the player, never to the shared asset. New effect behaviour belongs in the effect receiver; reward timing and placement belong in the spawner.

## Optional obstacle behaviour

`ObstacleSpawner` owns row placement, pooling, spacing, difficulty, and tutorial placement. Assign an optional `ObstacleMotion` component to change movement. Snow uses `ApproachingObstacleMotion` with its original 20–30 m/s speed and 1.7 spacing multipliers; City has no motion component. Fog remains a separate optional component and now depends on the shared spawner.

The small `SnowObstacleSpawner` wrapper intentionally remains to preserve the scene's existing script GUID and execution order. Its former movement implementation now lives entirely in the reusable motion component. Tutorial placements still bypass automatic movement.

## Run lifecycle and reporting

`OutlawGameManager` controls run flow and difficulty. `RunAnalyticsReporter` builds the existing context, run, session, and tutorial-exit payloads and manages analytics callbacks. Reporting fields and event names are unchanged. `RunSession` still carries the same snapshot between environment scenes.

Fuel keeps its existing schedule. Mystery rows carry their schedule through scenes, along with the backpack, remaining shots, and transient effects. Full-road rows bypass lateral pickup relocation; regular fuel placement is unchanged.

## Unity references and checks

Moved scripts and prefabs retain their `.meta` GUIDs. Existing `MoneyHeist.Pickups` and road assembly names are retained. Small `.asmref` files keep the player effects, mystery collectible, and pickup HUD in their original assembly despite their new folders. This avoids changing serialized type identities or test dependencies.

Run **The Outlaws > Validate Environment Setup** to check both scenes for missing scripts, required references, reward settings, and snow movement configuration. Run Edit Mode tests for shared gameplay rules and seeded reward-selection parity. Playtest tutorial handoff, pickup effects, pause/restart, and City–Snow–City portal transitions after changing scene wiring.

## Ocean Coast

`Env_OceanCoast` is the third build scene and portal destination. Its `EnvironmentRoot` enables `ocean` and has an `OceanCoastController` for surf lanes and coast guard wakes. `OceanObstacleSpawner` retains shared fair rows, pooling, collision and shooting rules, with REEF/BUOY labels. The driving environment multiplier feeds pursuit before the effects tick, so integrated travel and police separation agree. It does not change difficulty speed or consume collectible nitro. See [OCEAN_COAST.md](OCEAN_COAST.md) for controls, tuning and prototype limits.
