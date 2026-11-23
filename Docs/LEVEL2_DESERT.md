# Level 5 Desert

Level 5 uses Drew's shared level controller, six ramp sections and
one-hit retries. Still obstacles belong to Day or Night; the other world's appear as passable dashed outlines (Day yellow, Night black). All moving rocks are orange and solid in both
worlds: Day moves them and Night freezes their exact current position.

The course is 3,516 metres, approximately 121 seconds of clean driving with the
existing car speed/boost curves. No fuel, money, upgrade or shooting changes.

Ordinary road sections use left/right canyon bends based on the old Env_Desert's
80 m, 25-degree curves. Headings now reach roughly 28–32 degrees, alternating
direction between sections; the storm has a gentler +20 / -14 degree S-bend.
Every jump is straight from 80 m before its ramp through 45 m after its recovery
end. Road-piece cuts align with those boundaries so a curved piece cannot extend
into the jump. Day and Night retain exactly the same road shape.

Ground sections build from crossing rocks and alternating closures to a central
Night opening, opposing movers and a final short rock field. Section 2 starts in
Night: leaving its centre rock frozen at the starting position blocks the Night
opening. After the first Day wall, briefly return to Day to move it aside, then
freeze again before the second Day wall and the Night opening.

Launch order is Day, Night, Day, Night, Day, Day. Flights contain 2, 0, 3, 2, 4
and 10 spheres, plus one sliding block on each Night launch. Day-launch fields
use different lanes, phases and motion directions. Every airborne sphere has
at least 6 m of longitudinal spacing from the others in that flight, so their
visible bodies cannot merge even when their sideways paths cross.

Desert air steering uses the previous desert trial's .65 control. Night-launch
flights (2 and 4) have a full-width vulture flock 56 m after takeoff, in the later
half of the flight. In Day it repeatedly swings 4.5 m to one side, then the same 4.5 m to the other; Night stops it at its current position. Like every Desert
sideways mover it moves whenever it is Day, so it is already moving as you approach. The first block opens left; the second opens right, with
two earlier spheres away from its exit side. Land in the other world.

The final jump always launches in Day. Its 196 m flight fits ten fast spheres
in two waves of five. Centres are at least 10 m apart longitudinally, with a
66 m steering window between waves. Every sphere sweeps from -3.69 to +3.69 m,
covering all three lanes, at 10.35 m/s. Wider phase offsets are deliberately shuffled
within each wave, with a different order on the second wave. Neighbours start
about 1.2–2.7 m apart laterally instead of following a shallow curve. The two
waves move in opposite directions. Night freezes the exact positions;
each frozen wave leaves an outside bypass. The last rock centre
is at least 20 m before landing; landings remain clear.
Instructional green highway signs appear only in Training.

Six ground sliding blocks (camel caravans) at 780, 1,315, 1,715, 2,385, 2,530 and 3,130 m use the
same shared-world rule. They start closed on each attempt/retry, move whenever it is
Day, wherever the player is, and repeatedly walk 4.5 m to one side and then the same 4.5 m to the other (staying close to the road). Remaining in Night leaves them closed;
return to Day to create an exit, then freeze the opening if useful. They do not
vanish on switching. The same swept box footprint drives contact and the visual.
Every sideways mover carries the same white arrow (`WorldShiftMoverArrow`), pointing the way it is
going and flipping when it turns around, and no text.

Section 5 has 19 ground rocks (up from 13) in three waves, with mixed initial
positions and opposing movement. Two sliding blocks and Night roadblocks between waves
make players wake the next group rather than stay frozen for the entire field.
Three Night-only half-road barriers at 2,232, 2,300 and 2,446 m alternate right,
left, right. They leave the opposite half open in Night and become passable
dashes in Day. Dust fades in from 2,200 m and clears by 2,595 m, before the 2,660 m ramp.
It reuses `DesertSandstorm.ApplyVisibility`, with at least 70 m of visible road,
the same dust intensity in both modes and pause/retry-safe distance-based fades.

The background reuses `DesertPresentation.CreatePrototypeDune`: low ellipsoid
graybox dunes in four sizes, staggered on both sides, with sandy-yellow Day
and muted sand-brown Night colours. They have no collision. Placement excludes road bends,
including other nearby portions of the route. The continuous slope-following
sand stays in place to preserve road seams and visible gaps.

| Respawn point | Distance | Section |
| --- | --- | --- |
| Start | 0 m | Crossing and freezing |
| Checkpoint 1 | 810 m | First Night ramp approach |
| Checkpoint 2 | 1,350 m | Third ramp approach |
| Checkpoint 3 | 1,890 m | Second Night ramp approach |
| Checkpoint 4 | 2,160 m | Sandstorm rock field |
| Checkpoint 5 | 2,590 m | Fifth ramp approach |
| Checkpoint 6 | 3,150 m | Final ramp approach |

The finish is 3,516 m. There are six earned checkpoints, or seven respawn positions
including the initial spawn. The first gentle two-ball jump shares the opening
spawn; later ramp checkpoints are 70 m before takeoff approaches. The storm has
its own entrance checkpoint. Landings no longer add extra Desert checkpoints.
Retry patterns are deterministic, not rerolled.

Every earned checkpoint has a green labeled gate and two bright road stripes.
Passing it shows a small check icon and CHECKPOINT banner for 2.2 seconds, away
from the driving line. The confirmation pauses with gameplay and clears on retry;
the checkpoint count and positions have not changed.

Ground spheres are dense tumbleweeds (`WorldShiftDesertRockArt`); airborne spheres are
large vultures (same 1.1 m hitbox) that face their direction of travel and flap on the Day
clock, so Night and pause freeze them mid-flap (`WorldShiftDesertCreatures`); motion reuses `DesertTrialRock`.
Sliders are camels crossing the road, ground sliding blocks are camel caravans, airborne
sliding blocks are vulture flocks, and wide
Day/Night closures alternate sandstone hoodoos and tall saguaro cactus stands, both with Lydia's sun/moon panels
(`WorldShiftDesertObstacleArt`). See `OBSTACLE_VISUAL_RULES.md`.
`WorldShiftLevelRock` connects these to the shared hazard data and the car's
authored flight arc. Contact uses a sphere against the swept car box, including
height. Night and pause stop both translation and visible rotation. Checkpoint
retries reset rock clocks, as they do for the existing sliders.

## Environment presentation

The current Desert uses low wind-shaped dune ridges and warm sandy shoulders. Small oases
and curved coconut palms sit outside the driving corridor, away from jump approaches.
Oases have gently rippling water, reeds and stones along their irregular shores.
Day has a warm horizon; Night has a subdued blue sky and crescent moon.
Scenery is decorative and has no collision. Both worlds retain identical road
geometry; sand edges use the road mesh vertices, including curves and slopes.
Night shifts scenery to a subdued blue palette while keeping road markings clear.
The shared failure HUD reports BUSTED, MISSED LANDING or OFF THE ROAD and shows
automatic checkpoint retry progress.
