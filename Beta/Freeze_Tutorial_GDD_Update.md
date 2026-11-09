# Tutorial 5: Understanding Moving Hazards

Horizontal moving obstacles are teal, Day-only obstacles are orange, and Night-only obstacles remain pale violet.

The player encounters three shared sliders that move in Day and freeze in Night. After the lesson sign, the car waits safely while the player presses Space to try Night, Day, and Night again. The car remains stopped during the first two switches so the player can observe the hazards stop in Night and move again in Day. The third switch freezes the route and resumes driving. The banner names the current state and the next action, while retaining the steering controls. Early correct switching counts toward the sequence. Frozen hazards remain solid; ordinary collisions return the player to the lesson checkpoint and restart the demonstration. The ending checkpoint does not reset players for their route or world choice.

The required demonstration is changing moving hazards into stationary hazards, restoring their motion, and freezing them again before driving through the practice section. All time spent stationary in the demonstration is excluded from median active driving time.

Verification: the earlier single-switch demonstration, course layout, and Training/ramp completion passed Unity tests. The revised Night → Day → Night demonstration has compiled, but its final gameplay test remains pending after interrupted test runs. Rebuild and deploy the updated Beta version before describing this behavior as part of the submitted playable build.
