# Desert world-switch timing trial

Open `Assets/Scenes/Desert_WorldShiftTrial.unity`, or use
**The Outlaws > World Shift > Open Desert Rock Freeze Trial**.
This is an independent 960 m / approximately 40-second prototype. It does not
replace the city run or the original desert environment and is not added to the
build scene order automatically. It reuses the original desert road/sand materials,
`RoadSegment_Straight`, `AutoDriveCar`, `Obstacle.prefab`, `DesertRoadsideSand`,
the rolling-rock visual factory and crossing warning builder. All visuals remain
prototype geometry; no new art is added.

## Rule and controls

- **Enter** starts or replays; **A/D / arrows** use the existing car steering.
- **Space** changes Day/Night without changing the car's position or speed.
- In **Day**, all rocks roll sideways from the beginning of the run, including
  distant ones; none waits for the car to approach. In **Night**, they freeze at exactly
  their current positions. They remain solid in both worlds.
- **Esc** pauses both movement and rock clocks. **R** retries the current exercise.
- Contact restarts the short exercise after 0.7 seconds. No fuel, shooting or
  pursuit is needed for this isolated test of the new rule.

Warm walls exist in Day; blue walls exist in Night. Inactive walls are invisible,
consistent with the city. Switching inside a solid wall counts as contact. Pairs of original desert obstacles leave a central
opening: let the rock clear it, then freeze. A frozen rock remains a hazard if
the player freezes it in the opening. Head-height pause symbols and a blue rock
material communicate freezing without relying only on text.

## Three exercises

| Road distance | Purpose |
| --- | --- |
| 0-300 m | Let one rock leave the centre, freeze it, pass, then return to Day at the blue wall. |
| 300-600 m | Two counter-moving rocks share a clear centre window. One well-timed freeze connects both crossings. |
| 600-960 m | Freeze the first crossing, return to Day to release the next rock, then freeze again. |

Checkpoints at 0/300/600 m save the shared Day clock and restore it on retry,
returning the car to Day. Rock positions resume from that saved layout; they do
not reset on ordinary world switches. Ready and pause stop all clocks.
The original desert's 25-degree bend scale and gentle slopes appear between the
freeze puzzles. Crossing areas stay straight. The reused sand-bank mesh follows
every road edge; the wide sand backdrop stays below the lowest road point, using
the earlier desert clipping fix. Low sphere dunes replace the temporary cube backdrop.
Original single/two-lane obstacle rows fill the clear links between exercises.
The road and `AutoDriveCar` are reused; collision and rock timing are owned by
this trial, independently of the original shooting/desert analytics systems.

## Playtest questions and next steps

Can a new player explain that Night freezes rocks but does not remove them?
Can they recognize a clear opening before pressing Space? Does the final
release-and-freeze sequence add a decision beyond repeatedly pressing a button?

If this works, develop the desert around timing and preserve the city's route
selection identity. Possible later variations, not implemented here:

- A moving bridge in Day freezes its alignment in Night, turning the same
  timing rule into a surface the player needs rather than a hazard to avoid.
- A checkpoint-free short side route requires freezing a narrow opening;
  the main route provides an easier timing window. Both obey the same rule.
- During a final joined section, the player's half of the road selects the
  moving/frozen state, reusing steering to apply the rule learned with Space.

Do not add sandstorms until the timing and silhouettes are readable. Existing
rolling-rock prototype geometry can be reused when building the full desert.

`DesertShiftTrialTests` checks a complete route, continuous movement of distant
rocks, persistent freeze/resume, the saved checkpoint clock, curved/sloped road,
pause and collision with a frozen rock followed by retry. Campaign integration,
including the expanded two-minute level and its final intersection, is documented
in `WORLD_SHIFT_CAMPAIGN.md`; the short trial remains a separate development scene.
