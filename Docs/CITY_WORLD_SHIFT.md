# City World Shift prototype

A finite two-minute escape route: one car, one shared road, two obstacle layouts.
The route is 3,360 metres at 28 m/s (about 101 km/h). Retries add time;
a clean run takes 120 seconds. Driving uses the original `AutoDriveCar` steering,
skid, suspension and body response through an externally ticked finite route.

## Play

Open `Assets/Scenes/City_WorldShift.unity` and press Play. The editor menu
**The Outlaws > World Shift > Open City Prototype** opens the same scene.
Press **Enter** or click **Start Run**.

- **A / D or left / right arrows:** steer. The car moves forward automatically.
- **Space:** switch Day / Echo without changing position, steering momentum or speed.
- **Esc:** pause/resume. **R:** retry the current checkpoint.
- On completion, **Enter** or **Run Again** restarts the route.

Warm, multicolour Day and cool blue Echo share exactly the same road geometry.
Solid objects belong to the current world. Other-world outlines are only shown in
the first 560-metre tutorial sector; later sectors have no ghost obstacles or bridge
outlines. Gaps remove the road surface in one world and expose an 18-metre-deep shaft,
while the other world provides a bridge. Striped lips and reflectors mark the edge.
Pit walls follow the actual curved road edges. Brighter yellow/black broken lips,
three approach X marks, faint amber side guides and descending wall ticks distinguish
the opening from flat dark asphalt. The far inner face has a subdued glow for depth.
The decorative headlight haze fades before an active gap so it cannot resemble a
phantom road surface across the void; physical headlight illumination remains active.
Driving into a gap shows a gravity-driven fall and a closer view before retrying.
Switching into an occupied lane or a missing bridge is rejected
with a message, without consuming anything. There is no switch cooldown or resource.

## Route

Six sections introduce switching, staying in a world, steering around shared blocks,
crossing missing bridges, and combining those decisions. Each section begins at a
clear checkpoint. Ordinary impacts bring the chasing police closer; capture or empty
fuel retries after 0.7 seconds, and bridge falls take 1.05 seconds.
The final extraction screen shows active driving time, retries and world switches.

The layout is authored and deterministic, with safe routes through every section.
There are 56 authored decision positions containing 69 world-specific hazards:
fire, police blockades, barriers, spike strips, missing bridges, long Day centre
dividers, Night pedestrian crossings and single-lane oncoming police. The opening
is sparse; later sections combine more hazards. Day dividers are 70–200 m long,
follow road bends and physically keep the car on its chosen side; switching to
Night removes them. One crossing overlaps a Day divider, requiring a side lane
and a world switch together. Night crossings use zebra stripes and grey geometric
people. Four oncoming cars approach at 12 m/s; their render/collision bounds share
the same deterministic road-progress clock and pause with the run.

The shared road reuses the existing mesh code, now with authored 20–25 degree bends
over 80–120 m and gentle slopes. Continuous roadside strips reuse the actual road
mesh edge vertices, so sidewalks and background meet the road on slopes and bends.
The strips never span the carriageway or fill missing bridges.
There are no hairpins in this short route.
Echo has dim blue ambient light and shorter visibility (fog from 24 to 78 m).
Ambient fill and moonlight are slightly raised so unlit areas retain faint silhouettes.
A matching ambient probe prevents daylight fill from leaking into the night world.
A single player-mounted spot headlight spreads forward with a 74 m range and soft
cone edges, automatically switching on in Echo. Roadside lights provide small pools
of light instead of illuminating the entire road. Hazard labels also fade at range.
Building facade strips emit a muted blue light in Echo; one weak alternating fill
per street block gently lights the nearby pavement. Each missing bridge has four
fixed amber lamps around its entrance/exit, overhanging the lip to light the exposed inner walls
so the opening remains recognizable in the dark. They belong to the gap's world.
Echo uses dark neutral asphalt and lighter sidewalks, separated by continuous cream
edge lines that follow the road and disappear over missing bridges. Night obstacles
carry reflective tape: bumper strips, spike warnings, cliff markers and pedestrian
chest bands. Its simulated reflected brightness responds to the actual headlight cone
and range. Outside the beam, obstacle bodies retain very weak self illumination
and the tape retains a slightly stronger baseline, so silhouettes remain readable.
Each Night crossing has three red traffic signals suspended over the road; their
supports and signals are decorative, with no collision. A car impact launches only
the grey figures across its actual width, using forward/upward impulse, gravity,
tumbling and a landing bounce. The entire crossing costs one normal pursuit hit,
regardless of how many figures are struck. Pause freezes the figures and retry
restores their standing poses.
Missing bridges are announced by red/yellow triangular pictogram signs 38 m before
the edge, with a broken road and car silhouette instead of a floating text label.
Echo reuses `CityStreetlight.prefab`;
police and barrier visuals reuse the existing prefabs. Buildings and spikes remain
simple geometry/materials; no art assets are added.
The car retains its gold colour in both worlds.

Fuel reuses `FuelState` with 100 capacity, 2 units consumed per active driving second
and +26 per pickup. World-specific fuel cans reuse `FuelTank.prefab`. The route
now offers 13 world-specific cans at 12 positions, including added clear pickups
at Day 500 m, Echo 1,968 m and Day 3,228 m. Below 25%, the
fuel card and thin screen edges pulse red. Fuel freezes when paused or retrying.
Retries restore the checkpoint's fuel snapshot (at least 35 units) and its future
pickups, so an empty tank cannot trap a player in a restart loop. Obstacles do not
apply an additional fuel penalty.

The pursuer reuses `ChaseCar`, `CityPoliceVehicle.prefab`, `RiskRunState` and
`RearViewCamera`; the mirror shows the actual follower and changes border colour
as pursuit closes. The logical starting gap is 45 m. Police close slowly during
clean driving; every ordinary impact costs 22.5 m. Two effective impacts exhaust
the starting gap and cause capture, using actual distance rather than a hit counter.
One obstacle cannot
repeatedly drain distance while the car passes through it. Original collision
protection is retained. No impact applies an extra fuel penalty. A divider also
resolves side contact, so taking a hit never lets the car drive inside it.
Retries restore the checkpoint's gap snapshot with at least 32 m separation.
As in the original game, visual separation is compressed to keep police legible
in the small mirror; the caption displays logical distance.

The scene focuses on driving, switching, pursuit and fuel. Backpack, shooting,
analytics and portals are not started. The new scene is
appended to Build Settings; select it as the first scene when building this prototype
on its own.

## Adjust or extend

- `WorldShiftLevel.cs`: speed, length, fuel, checkpoints and both obstacle layouts. Lane
  bits are 1 = left, 2 = middle, 4 = right; combine them for wider closures.
- `WorldShiftRoute.cs`: shared geometry and world-specific missing bridge surfaces.
- `WorldShiftRoadside.cs`: continuous sidewalks and background using actual road-edge vertices.
- `WorldShiftRoadSigns.cs`: decorative overhead signals and triangular broken-road pictograms.
- `WorldShiftPitVisuals.cs`: road-fitted open shafts, warning lips and visible depth cues.
- `WorldShiftPedestrian.cs`: manually stepped whole-figure impact physics, pause and reset.
- `WorldShiftCity.cs`: input, original-driver/pursuit integration, moving sweeps, divider contact, fuel, switching and retries.
- `WorldShiftPresentation.cs`: palettes, primitive city and obstacle previews.
- `WorldShiftHud.cs`: start/pause/results screens and compact driving HUD.

Geometry and UI are generated when Play begins. Scene references reuse the existing
straight road and car prefabs. Keep checkpoint areas clear in both worlds and leave
time to change lanes; changing an obstacle's layout should always be playtested.

Three focused editor tests (`WorldShiftCityTests`) validate level/fuel data, a complete
simulated route without a single forced hit (including moving collision, pursuit,
mirror, pause, collection, divider boundaries, capture/retry and falls), and that
the original City scene still drives through the regular input lifecycle.
Set `OUTLAWS_WORLD_SHIFT_PREVIEW` to an output directory during a graphical test run
to capture representative Day/Echo and bridge previews.
