# Integration notes

## Team 19 foundation

The combined project preserves Team 19's Unity project settings, infinite-road
system, automatic driving and steering, chase cars, obstacles, fuel bar and
fuel-tank pickups, crash handling, score, proximity system, and existing pickup
framework.

## Team 21 features adapted

The following Team 21 concepts were adapted to Team 19's architecture:

- start menu and complete start/win/lose/restart flow;
- limited forward and backward shooting;
- forward shots destroy Team 19 obstacles;
- backward shots restore distance in Team 19's pursuit model;
- ammunition inventory and road pickups;
- progressively increasing police pressure;
- a finite extraction objective and finish gate;
- fuel as a required survival resource.

Team 21's separate player controller, police controller, duplicate HUD, and
particle-based visual effects were intentionally not copied because they would
conflict with Team 19's road and chase systems.

## Runtime integration

`OutlawsBootstrap` activates only in the `GetawayChase` scene. It creates the
combined systems at runtime and connects them to the existing Team 19 scene
objects, which keeps the original scene references stable and makes the merge
easy to maintain.

The central integration scripts are in `Assets/Scripts/TheOutlaws`.
