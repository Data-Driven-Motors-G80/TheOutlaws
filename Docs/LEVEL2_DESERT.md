# Level 2 desert

Level 2 uses Drew's shared level controller, six ramp sections and
one-hit retries. Day-only obstacles stay teal, Night-only obstacles pale violet with the other
world shown as passable dashes. All moving rocks are orange and solid in both
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
flights (2 and 4) have a full-width orange rectangular block 56 m after takeoff,
in the later half of the flight. Its face says **DAY TO MOVE**. In Day it repeatedly
slides to the roadside and back; Night stops it at its current position.
Motion starts at takeoff, so earlier Day driving cannot
accidentally open it. The first block opens left; the second opens right, with
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

Six ground sliding blocks at 780, 1,315, 1,715, 2,385, 2,530 and 3,130 m use the
same orange, shared-world rule. They start closed, activate within 40 m in Day
and repeatedly shuttle to the roadside and back. Remaining in Night leaves them closed;
return to Day to create an exit, then freeze the opening if useful. They do not
vanish on switching. The same swept box footprint drives contact and the visual.
Every sliding block, including the two smaller sliders, carries the same
**DAY TO MOVE** cue. Small blocks use a raised label so it remains readable.

Section 5 has 19 ground rocks (up from 13) in three waves, with mixed initial
positions and opposing movement. Two sliding blocks and Night roadblocks between waves
make players wake the next group rather than stay frozen for the entire field.
Three Night-only half-road barriers at 2,232, 2,300 and 2,446 m alternate right,
left, right. They leave the opposite half open in Night and become passable
dashes in Day. The entrance has **STORM** painted directly on the road at 2,200 m.
Dust fades in from 2,200 m and clears by 2,595 m, before the 2,660 m ramp.
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

Geometry reuses `DesertRollingRock.CreateVisual`; motion reuses `DesertTrialRock`.
`WorldShiftLevelRock` connects these to the shared hazard data and the car's
authored flight arc. Contact uses a sphere against the swept car box, including
height. Night and pause stop both translation and visible rotation. Checkpoint
retries reset rock clocks, as they do for the existing sliders.
