# Obstacle visual rules (all levels)

These rules apply to every level, so players can read any obstacle the same way everywhere. Drew wrote them
while reworking Desert, which follows them now. The other levels have open issues listed below, for each level's
owner to fix in their own level. They were left untouched to avoid merge conflicts.

## The rules

1. **Anything that swaps with Space shows the sun/moon icon.** If an obstacle belongs to one world only (a
   Day/Night barrier, a half barrier, one-world traffic, any themed blocker), it has a world-coloured panel with
   a dark **sun** (Day, orange panel) or **moon** (Night, lavender panel). Copy Lydia's Snow version:
   `WorldShiftSnowObstacleArt.WorldPanel(art, centre, width, height, world)`. Examples: the plough blade in
   `WorldShiftRoadVehicleArt.Snow.cs`, the upright pines in `WorldShiftSnowObstacleArt.UprightPines`, and the
   Desert sandstone hoodoos and saguaro cacti in `WorldShiftDesertObstacleArt.Hoodoos` / `.Saguaros`.
2. **Anything that moves sideways shows the same arrow, pointing the way it is going.** That covers sliders,
   sliding blocks, crossing rocks, drums, trolleys, tumbleweeds, vultures and camels. It is a filled white
   single-headed arrow with a dark outline: `WorldShiftMoverArrow.Attach(...)` or `WorldShiftMoverArrow.OnBothFaces(...)`.
   Put it on the approach faces, or above a sphere outside the rolling hierarchy so it never tilts.
   `WorldShiftPresentation` aims every arrow each frame (`SidewaysHeading`): it flips the moment the mover turns
   around, and a mover frozen in Night keeps showing the way it will go when Day resumes. (Playtesters had to guess
   the direction from the old double-headed arrow.) Never draw your own arrow; the shared one is the only one aimed.
3. **No text on obstacles or painted on the road.** No "DAY TO MOVE", no "STORM". The icons and the
   frozen-ice cue explain everything. (Training's instruction signs are the agreed exception.)
4. **Sideways movers always move in Day, wherever the player is.** No "wakes up when you get close" behaviour.
   Hazards render from about 270 m ahead, so anything that only starts moving nearer than that looks like a
   surprise. Set `ShiftHazard.MovesWheneverDay` (see `LevelCourse.CreateHazards`, Desert does this) for your level.
5. **Theme obstacles to the level, but keep the readable parts.** Follow Lydia's Snow format: the same
   footprint, a themed body (snowbank / pines / snowplough, sandstone hoodoos and saguaros / camels / vultures),
   plus the world panel or sideways arrow above. **Sideways movers must be things that really move sideways** (animals, vehicles, carts), not
   objects that can't move on their own (Desert: camels and a camel caravan, not cacti or spike sleds).
   **Floating things must be living, flying animals** (Desert: vultures and a vulture flock).
   Mountain: Day/Night closures are rockslide piles (`WorldShiftMountainObstacleArt`), sideways movers are goats crossing the road.
6. **Keep movers on screen.** A sliding block swings evenly side to side, at most 4.5 m each way from the middle (`WorldShiftSlidingGate.Travel`), never far out on one side only.
   No circles or patches under obstacles, and no floating ice ring under flying animals.

## Open issues in other levels (for each level's owner)

Found during the Desert pass. Desert is fixed; each item below names where to fix it.

### Motion that starts late (City, Ocean, Training)
- `ShiftHazard.Advance` in `WorldShiftLevel.cs` only moves sliders and crossing rocks once the car is within
  **150 m** (`Start - 150f`). Hazards are visible from **270 m** (`WorldShiftPresentation.UpdateVisibility`,
  `distance + 270f`), so City sliders, Ocean cargo trolleys and oil drums, and Training sliders sit still for
  about 120 m and then start moving.
  **Fix:** in `LevelCourse.CreateHazards`, extend `MovesWheneverDay` to your environment (one condition).
  Re-check Training's freeze lesson and Ocean's diamond lanes afterwards.
- Same class of surprise, for forward movers: one-world traffic and rolling rocks (Mountain, Training) wait
  parked until 150 m (`ShiftHazard.BoundsAt`), then drive at you. Consider starting them at the visibility
  distance instead.

### Sun/moon icon missing on one-world traffic (City, Ocean)
- City and Ocean `DayCar`/`NightCar` are plain civilian cars (`WorldShiftRoadVehicleArt.Create`) with no
  sun/moon, so you can't tell which world they belong to. Snow's ploughs already have it on the blade.
  **Fix:** add `WorldShiftSnowObstacleArt.WorldPanel` to the car's nose, like the plough. The car body is stretched
  1.35× vertically, so put the panel on a child with the inverse scale to keep the icon round.

### Two slightly different sun/moon drawings (all levels)
- `WorldShiftRetractableBarrier.Icon` (the plain Day/Night barriers in City, Ocean, Mountain, and the Snow and Desert
  single-lane blocks) draws the sun and moon with different proportions from Lydia's
  `WorldShiftSnowObstacleArt.WorldPanel`. The sun disc and rays differ, and the moon's inner curve is .55 vs .45.
  **Fix:** have `RetractableBarrier.Icon` reuse the `WorldPanel` icon geometry, so there is exactly one drawing.

### Sideways arrows (resolved for all levels)
- Spike platforms, boulders/drums (`WorldShiftBoulderArt`) and Ocean cargo trolleys now use `WorldShiftMoverArrow`,
  so every level shows one arrow design, aimed along the direction of travel. `EverySidewaysMoverArrowPointsTheWayItIsGoing`
  checks every level.
- Theming: other levels' sideways movers are still police spike platforms. Desert uses camels (sliders) and camel caravans (sliding blocks).
  Each level could give them a themed body (keep the teal deck and the arrow).

### Text still in levels
- Ocean diamonds float a **"COLLECT"** label (`WorldShiftLevelProps.RequiredItem`). This breaks rule 3, so it is the
  Ocean owner's call to remove it.
- Checkpoint gantries say **"CHECKPOINT"** (`WorldShiftCheckpointGantries`). This is a product decision in
  AGENTS.md (a labeled gate), so it has been kept for now. Raise it with the team if it should go.
- Dead code after the Desert pass: `WorldShiftLevelProps.SlidingGateHint` ("DAY TO MOVE") and
  `WorldShiftLevelProps.StormRoadMark` ("STORM") are no longer called. `WorldShiftBoulderArt.Tumbleweed` is no
  longer used either (Desert spheres now live in `WorldShiftDesertRockArt`). They were left in place to avoid
  touching shared files and can be deleted in any cleanup pass.
