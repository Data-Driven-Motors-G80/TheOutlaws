# City World Shift prototype

The final 20 seconds now use the validated full-frame Day/Night compositor.
The separate [20-second validation scene](CITY_DUAL_VIEW_VALIDATION.md) remains available.

A finite two-minute escape route: one car, one shared road, two obstacle layouts.
The route is 3,360 metres, normally at 28 m/s (about 101 km/h). Three relay
boosts reach 42 m/s (151 km/h); a clean run takes about 114 seconds. Retries add time. Driving uses the original `AutoDriveCar` steering,
skid, suspension and body response through an externally ticked finite route.

## Play

Open `Assets/Scenes/City_WorldShift.unity` and press Play. The editor menu
**The Outlaws > World Shift > Open City Prototype** opens the same scene.
Press **Enter** or click **Start Run**.

- **A / D or left / right arrows:** steer. The car moves forward automatically.
- **Space:** switch Day / Echo without changing position, steering momentum or speed.
  After crossing the final world rift, Space rests; steer across the centre seam instead.
- **Esc:** pause/resume. **R:** retry the current checkpoint.
- On completion, **Enter / Next Level** opens Desert. **Replay** or R restarts; **Level Select** opens the campaign menu. See `WORLD_SHIFT_CAMPAIGN.md`.

Warm, multicolour Day and cool blue Echo share exactly the same road geometry.
Solid objects belong to the current world. Other-world outlines and dashed relay
landing previews are removed throughout the run. Switching into a solid object
immediately counts as an impact; switching onto missing road causes a fall.
The joined finale shows the actual cropped worlds. Gaps remove the road surface in one world and expose an 18-metre-deep shaft,
while the other world provides a bridge. Striped lips and reflectors mark the edge.
Pit walls follow the actual curved road edges. Brighter yellow/black broken lips,
three approach X marks, faint amber side guides and descending wall ticks distinguish
the opening from flat dark asphalt. The far inner face has a subdued glow for depth.
Night pits have continuous amber side rims, tall corner markers and stronger depth
ticks. Night pedestrian heads, chests and lower legs have reflective markers;
all Night obstacle bodies have a readable baseline emission outside the headlight.
The decorative headlight haze fades before an active gap so it cannot resemble a
phantom road surface across the void; physical headlight illumination remains active.
Driving into a gap shows a gravity-driven fall and a closer view before retrying.
On the ground, switching into an occupied lane or a missing bridge is rejected.
An active relay flight allows switching above the void, then checks the destination
deck at touchdown. Ordinary pits never grant this exception. There is no switch cooldown or resource.

## Route

Six sections combine ordinary switching, three relay jumps, missing bridges,
steering around shared blocks, and the final joining of both worlds. Each section begins at a
clear checkpoint. Ordinary impacts bring the chasing police closer; capture or empty
fuel retries after 0.7 seconds, and bridge falls take 1.05 seconds.
The final extraction screen shows active driving time, retries and world switches.

The layout is authored and deterministic, with safe routes through every section.
Hazards include fire, police blockades, barriers, spike strips, missing bridges,
Day centre dividers, Night pedestrian crossings, single-lane oncoming police and
wall-attached half-width roadblocks. The opening is sparse; later sections combine
familiar decisions. The remaining Day divider is 140 m long and
follows road bends and physically keeps the car on its chosen side; switching to
Night removes them. Night crossings use zebra stripes and grey geometric
people. Thirty-six single-lane oncoming cars approach at 12-14 m/s, eighteen in Night
and eighteen in Day. They arrive in authored groups, rather than random simultaneous
closures; their render/collision bounds share
the same deterministic road-progress clock and pause with the run.

After the first landing, a Night crossing interrupts the easy Night bypass. Later,
Day and Night full-width closures alternate, requiring a world choice as well as
steering around traffic. Cars approach on different lanes, with launch and landing
recovery zones kept clear. A complete steering/switching route is checked using
the real driver and collision system, with no forced hits. The original opening,
base speed, fuel drain, pursuit penalty and three relay jumps are unchanged.

The shared road reuses the existing mesh code, with authored bends and gentle
slopes between the relays. Launch/landing stretches are straight; each ramp rises
about 13 metres over 60 metres and ends at a 28% grade. Continuous roadside strips reuse the actual road
mesh edge vertices, so sidewalks and background meet the road on slopes and bends.
The strips never span the carriageway or fill missing bridges.
There are no hairpins in this short route.
Echo has readable blue ambient light and shorter visibility (fog from 48 to 150 m).
Ambient fill, moonlight and asphalt brightness are raised so players can read
obstacles ahead without relying entirely on the headlight. Approaching police have
weakly emissive white headlight bars so their direction is readable earlier.
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
offers 13 world-specific cans at 12 positions. Overlap pickups sit entirely in
their matching half (Day left, Night right), including Day 2,950 m,
Day 3,228 m and Night 3,320 m. The second relay rewards a Day landing with fuel at 1,968 m. Below 25%, the
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

### Three relay jumps

| Relay | Ramp / takeoff / landing (metres) | Required worlds | Air time |
| --- | --- | --- | --- |
| 1 | 600 / 660 / 744 | Day ramp -> Night landing | 2.0 s |
| 2 | 1740 / 1800 / 1896 | Night ramp -> Day landing | 2.3 s |
| 3 | 2300 / 2360 / 2468 | Day ramp -> Night landing | 2.6 s |

Each ramp has green acceleration strips and repeated white forward chevrons.
Rectangular ramp/flight pictograms and a world label announce takeoff. The car
launches automatically from the ramp lip; **press Space while airborne** and remain
in the landing world until touchdown. The target has solid edge markers, a down arrow and
side beacons. The destination's real road appears after switching. No new art is used.

The first 24 ramp metres smoothly accelerate both player and police from 28 to
42 m/s. Flight stays at 42 m/s; the 56 metres after landing smoothly return both
to 28. Pursuit uses the same speed boost on both sides, so this adds urgency without
granting free escape distance. Steering remains available at reduced strength in air.
The chase car follows the same arc at its trailing route position, including in the mirror.

The launch world has no landing deck; the destination world has no ramp. Grounded
switches onto that absent ramp are rejected. Airborne switching preserves position
and speed. Missing the destination causes a fall and checkpoint retry, with no
automatic world switch. Pause freezes flight; retry clears flight and boost state.
The fixed arc belongs only to these marked relays. The shared driver opts into
external vertical control only there; other scenes retain their original physics.

Ordinary pits remain hazards: yellow/black lips, X markings and triangular warning
signs, with no boost strip and no automatic jump. They still require switching to
an existing road **before the edge**. Relay stretches replace selected older obstacle
rows; normal pit sections and the final rift remain separate, with safe approach buffers.

### Final world rift

The only joined section begins at **2,800 m / about 94 seconds** and lasts through
extraction at 3,360 m / about 114 seconds. The middle sectors use ordinary Day/Night switching and relay flight, with no
early world joining.

A jagged amber/cyan geometric gate marks the entrance in both worlds. It has no
collider, fill panel or new art. Approach arrows and a short HUD cue announce the
change. Passing its road-distance threshold triggers a sound and opens the image
seam over 16 metres (about 0.57 seconds). The first block is 100 metres beyond the
gate, leaving a clear transition and steering buffer. The gate is also a checkpoint.

The compositor renders the existing **complete Day and Night views** from identical
cameras and crops Day's left half with Night's right half at unchanged screen UVs.
Sky, fog, lighting, buildings and road appearance therefore remain their original
world versions. No replacement half-road/half-city geometry is built. Gameplay,
fuel and pursuit still update once. The normal camera renders directly before the
final sector; the extra two world renders are active only during the finale.

The final road is straight, with slope easing to zero by 2,720 m. The camera eases
out lateral follow before the gate and remains on the road centre through the finale.
This keeps the screen seam aligned with the collision boundary at every depth.
Earlier bends and slopes remain. Arbitrary curved seams are not implemented.

Two ordinary-world half-width barriers at 480 m and 520 m introduce the shape.
Seven alternating half-road blocks at 2,900/2,970/3,040/3,110/3,180/3,250/3,320 m
require crossing between the visible worlds. The first 100 m after the rift remain
clear. Six single-lane cars follow the first six walls on the same blocked half,
meeting the player about 20 m after the wall. Read the following car before
crossing back; the opposite half remains available. Each wall reaches from a road edge to the centre; straddling
the seam still contacts the visible wall. Space is inactive during this section,
and the split stays visible on the finish screen. Impact penalties are unchanged.

The three city relay jumps precede this final crossing. Next Level now leads to
the full desert, whose final intersection reuses this compositor. Its left-side
obstacles keep moving while the right-side Night snapshot remains frozen.

### Files

- `WorldShiftLevel.cs`: speed, length, fuel, checkpoints and both obstacle layouts. Lane
  bits are 1 = left, 2 = middle, 4 = right; combine them for wider closures.
- `WorldShiftRoute.cs`: shared geometry and world-specific missing bridge surfaces.
- `WorldShiftRoadside.cs`: continuous sidewalks and background using actual road-edge vertices.
- `WorldShiftConvergence.cs`: reused half-width wall mesh builder; its older half-city builder is no longer called.
- `WorldShiftSplitView.cs` / `WorldShiftSplitView.shader`: complete world renders and full-frame crop, enabled at the final rift.
- `WorldShiftRift.cs`: non-solid geometric entrance warning and deterministic pulse.
- `WorldShiftRelay.cs`: relay layout, boost, shared arc and landing state.
- `WorldShiftRelayVisuals.cs`: uphill boost chevrons, ramp pictograms, real voids and landing markers.
- The older `WorldShiftConvergenceSurface` / `WorldShiftConvergenceSky` shaders are not used by either current city scene.
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
mirror, pause, collection, divider boundaries, half-wall collisions, final-rift
camera/seam alignment, disabled Space, capture/retry and falls), and that
the original City scene still drives through the regular input lifecycle.
Set `OUTLAWS_WORLD_SHIFT_PREVIEW` to an output directory during a graphical test run
to capture representative Day/Echo and bridge previews.
Set `OUTLAWS_FINAL_RIFT_PREVIEW` instead to capture only the approach, opening
transition and joined finale. The separate `WorldShiftSplitViewTests` checks the
source-image crop and the short validation scene.

`WorldShiftRelayTests` covers all three launches/landings, police boost, pause,
unsafe early switching, missed landing and retry. Set `OUTLAWS_RELAY_PREVIEW` for
three launch/airborne/landing screenshots during the focused test.
