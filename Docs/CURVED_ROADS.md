# Road geometry and environment tuning

This guide covers the current WorldShift finite routes and identifies the older streamed-road implementation separately. Start the current game from `WorldShift_Menu`, not `Env_City`.

## Shared geometry

`RoadSegment.ConfigureCurve` and `RoadCurveMesh` build primitive road surfaces from a route distance, heading change and start/end slope. `RoadMarking` follows the generated path. `AutoDriveCar` samples the route while A/D moves the car laterally. Obstacles, pickups and the chasing car use the same road coordinate.

Keep unit root scale and the template children named `Road`, `Left Roadside Wall` and `Right Roadside Wall`. Changes to these shared foundations can affect both current and legacy scenes.

## Current finite routes

`WorldShiftRoute.Build` creates the full authored route at runtime. Geometry is shared between Day and Nightshift; relay gaps determine where each mode has a deck. `WorldShiftForkRoad` supplies separated branches and constrains the car to its committed carriageway.

Tune `Assets/Scripts/WorldShift/WorldShiftEscapeProfile.cs` for City, Ocean, Snow and Mountain. Desert also uses `DesertShiftLevel` and its route profile in `WorldShiftRoute`.

| Environment | Road character |
| --- | --- |
| City | Flat outside its ramp, with short street chicanes |
| Desert | Stronger canyon bends and long rolling swells |
| Ocean | Tighter sweeping coastal bends, small rises and bridge-edge cues |
| Snow | Firm, longer bends, gradual descents, 0.85 steering grip and recoverable release slide in both modes; extra slide clears near forks and jumps |
| Mountain | Climbs, sharp alternating switchbacks and winding descents with approaching rock blocks |

Keep forks and relay approaches clear and readable. Flatten terrain around those sections. Fully separated branches retain the main road width; Ocean's bridge cues do not narrow the actual steering space. Place Day supplies after mode encounters, not inside Day-only barriers or required gaps. Approaching Mountain rocks must not move back into an unfinished fork merge.

Main relays require switching in the air. Landing protection ignores premature return switches until both roads rejoin. The optional bonus ramp keeps a ground bypass. On short transitions it starts before the environment boundary, with a flat, straight approach and at least 24 m of recovery after landing before the scene changes. Exact mode and resource rules belong in the [campaign guide](WORLD_SHIFT_CAMPAIGN.md).

## Legacy streamed roads

`InfiniteRoad` in the older `Env_*` scenes streams pooled pieces. Its Inspector options include Enable Curves, Curve Length, Bend Angle, Maximum Heading, Straight Intro Distance and Ahead/Behind Buffer. These settings do not author the current finite courses.

The original flat-road implementation and its historical validation results are preserved in [Archive/CURVED_ROADS_ORIGINAL_VALIDATION.md](Archive/CURVED_ROADS_ORIGINAL_VALIDATION.md). The isolated dual-view compositor is a separate [validation experiment](Archive/CITY_DUAL_VIEW_VALIDATION.md); it does not establish support for split views on curved or sloped roads.

## Verification

Use the road geometry tests for shared mesh/sampling changes, and `WorldShiftEnvironmentFlowTests` for complete finite courses, mode barriers and environment slopes. `WorldShiftCombatRoutesTests` checks collision, freezing, shooting and branches; `WorldShiftRelayTests` checks the older standalone relay layout. Inspect a representative driving view when changing layout or visibility. Run broader suites only when shared simulation changes justify them.
