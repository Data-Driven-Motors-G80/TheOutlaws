# Curved roads

City and Snow now generate gentle left/right bends and S-curves during Play mode.
The first 160 metres stay straight for the traffic-light tutorial. Curves are
80 metres long, separated by 20-metre straights. Controls are unchanged: the car
follows the road, while A/D or the arrow keys move across it.

## Tuning

Select the object with `InfiniteRoad` in either environment scene:

- **Enable Curves** switches procedural curves on/off.
- **Curve Length** controls how gradually each curve unfolds (80 m by default).
- **Bend Angle** sets the heading change or S-curve peak (25 degrees).
- **Maximum Heading** bounds the route heading to prevent it doubling back.
- **Straight Intro Distance** controls the initial tutorial stretch (160 m).
- **Ahead Buffer / Behind Buffer** retain road ahead of the player and behind the
  camera/police (180 m / 40 m).

Changes are intended to be made before entering Play mode. The existing straight
road template supplies the road and wall materials. `RoadCurveMesh` builds each
pooled piece's surface and wall meshes/colliders; `RoadMarking` follows the same
path. No spline package or separately modelled curved assets are required.
Scene view remains straight until Play mode generates the pieces.

## Movement and placement

`AutoDriveCar` advances by distance along the route, preserving speed and scoring
through bends independently of lateral steering. Police, obstacles, ammo,
power-ups, and fuel use the same path sampling. Road recycling uses route
distance rather than whether the car has crossed a world-space end plane.
The existing child camera follows the player's smoothed road-facing rotation.

This first version supports flat, forward-running roads with a constant width.
It does not add junctions, manual corner steering, hills, or banked turns. The
procedural template expects unit root scale and children named `Road`,
`Left Roadside Wall`, and `Right Roadside Wall`, as in the current prefab.

## Checks

Run the EditMode tests in `TheOutlaws.Road.EditorTests`. They cover left/right
bends, S-curves, speed-independent distance sampling, edge alignment at joins,
surface/collider correspondence, reuse as a straight piece, and 5 km of streaming
with retained road both ahead and behind.

For a gameplay check, open `Env_City` or `Env_Snow`, enter Play mode, and start the
run. After 160 m, check lane changes, camera tracking, police pursuit, and fuel
placement through the bends. F9 switches environments in the editor.

### Validation (Unity 6000.3.23f1)

- All 7 road EditMode tests pass.
- An isolated Play mode check drove 1 km in each scene with alternating A/D
  input, checking car/police alignment and obstacle, fuel, and power-up placement.
  Survival penalties were disabled in that check to exercise the full route.
  City and Snow camera renders were also inspected.
- The full EditMode suite passed 93/95 tests. The two failures were reproduced
  against the original `c8c0076` code: fuel refill expectation (90 vs 100), and
  reverse steering after component disable/re-enable (-1 vs 1). They are separate
  from these road changes.
