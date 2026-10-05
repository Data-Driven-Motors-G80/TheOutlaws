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

`GetawayChase` stores the combined systems on `The Outlaws Game Systems`,
with shooting on `PlayerCar` and the labeled bars saved in the canvas.
`OutlawGameManager` connects these existing components when Play starts.
`OutlawsBootstrap` only resets time scale and the random seed for each run.

Camera transforms and field of view, vehicle scale, police materials and
visibility, and HUD layout now come from the saved scene. Startup does not
overwrite these values or recreate missing systems. HUD references can be
assigned in the Inspector; existing named labels remain a binding fallback.
The explicit **The Outlaws > Apply Gameplay Layout to Current Scene** editor
command rebuilds the default layout only when invoked; it can overwrite custom
layout choices and should not be used during ordinary editing.

Make persistent hierarchy edits outside Play mode and save the scene. Unity
discards ordinary Play-mode edits when Play stops. During gameplay, movement
scripts still control vehicle positions, bars reflect live resource values,
and spawners still create road segments, obstacles, and pickups as before.

The central integration scripts are in `Assets/Scripts/TheOutlaws`.
