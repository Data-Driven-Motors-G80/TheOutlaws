# Wee Woo Run (The Outlaws)

A fast-paced Unity police-chase game where players weave through traffic,
manage limited fuel and ammunition, shoot obstacles, and
survive an endless chase for as long as possible.

The project uses Team 19's **Money Heist** project as its foundation and adapts
selected gameplay ideas from Team 21's **Redline Run** into one cohesive game.

## Gameplay

- **Title screen:** *Wee Woo Run*, your best distance, a tutorial ON/OFF switch (ON for a browser's first game, OFF after)
  and your missions (completed / 10, the current mission and the next two).
- **Tutorial (show, don't tell):** one instruction at a time in a box at the top centre of the screen (where the
  distance shows during a run): ESCAPE THE COPS! (2.5 s) → STEER (← LEFT / RIGHT →; nothing spawns until both are
  done) → AVOID OBSTACLES (two rows 45 m apart: go left, then right) → FUEL LOW, flashing until you grab fuel (fuel
  drains the whole time; right after the second row it drops into the red as FUEL LOW appears; two tanks one behind the other in the
  right lane, each filling half the bar (one is enough to continue), a block behind them guiding you there) → GRAB 3 AMMO (you start the tutorial with 2; collecting all three boxes is what counts; one behind the other in the middle lane, following the road's curve,
  then a small "2" block in the right lane) → SHOOT A BULLET (the shooting lessons drive a steady 20% slower with no catch-up boost; SPACE key shown; the first wall appears after it) → DESTROY THE OBSTACLE (a 1 wall)
  → DESTROY THE OBSTACLE (a 2 wall; shoot freely: at zero rounds an ammo box appears right in front of the car) → MYSTERY BOX: SHIELD! PROTECTS FROM A HIT (then a full-width wall to ram with it: HIT BLOCKED!, shown for about 2 s) → MYSTERY BOX: DIZZY! (right lane, level with the second obstacle: a narrow block on the far left edge, then a row leaving only the right lane open; CONTROLS REVERSED
  immediately, and the ← LEFT / RIGHT → keys light up again until you have moved the car left and right with the reversed controls) → steer through the wide gap between two narrow edge blocks to the last box: NITRO! SPEED BOOST (the car speeds up to 2x tutorial speed, far faster than the real run), then TUTORIAL COMPLETE on its own (failing the
  reversed stretch starts again from the dizzy box) The
  real run keeps that nitro speed for 3 s, then the nitro wears off and the car slows to normal run speed over 2.5 s. Each lesson is laid out at least 95 m ahead (nothing appears near the car), and
  the car speeds up to 1.5x while the next lesson is still more than 50 m away (never during the fuel warning). Any
  failure (a hit or a missed pickup) never stops the car: "TRY AGAIN!" pulses for 1.2 s, the police speed up right
  behind you, and the lesson is laid out again 70 m ahead. The car drives at 16 m/s and nothing counts.
- **Power-up visuals:** a blue circle in front of the car while a shield is ready (it bursts when a hit uses it).
  The reversed-controls spirals and nitro lightning bolts were removed for the alpha (no art yet; see `FORLATER.md`).
- The fuel bar turns red under 30%.
- Obstacles look like orange road barriers with black warning bands made from simple geometry. Both types show a large
  white shot count (1 or 2) on a dark front panel; shooting a 2 changes its label to 1 while keeping the warning appearance.
  Pickups are labelled on their front face:
  **A** ammo, **F** fuel, **?** mystery. Ammo is shown as one blue square per round (max 5, start with 2). Shots fly down
  the whole visible road (stopping at walls) and there is no fire delay: every Space press fires. Blocks are bigger and gaps are sometimes tight.
- Hitting blocks costs police distance only: one-shot orange blocks cost 15 m and two-shot red blocks cost 22.5 m. Collisions never subtract fuel.
- Roadblocks start at 200 m and come every 150–210 m (90–130 m after 2,000 m). The mix of blocks never changes
  (55% one-shot, 45% two-shot); only how many there are increases. Every row and roadblock still **leaves one
  lane open**, so ammo is the practical shortcut, never a requirement. A boxes come every 90–220 m to match.
- Difficulty: base speed starts at 20 m/s and rises by 1.5 m/s at independently randomized 12–18-second intervals (about 32 m/s at two minutes, 44 m/s at four). Each increase blends in over 2.5 seconds; base speed caps at 60 m/s. The schedule uses active real-run time, pauses during tutorials/pauses, and carries across environment changes. Nitro multiplies speed by 1.2 (24 m/s at the opening, 72 m/s at the cap). Obstacles start sparse during a 30 s warm-up, build up over two minutes,
  then **keep getting denser for the whole run** (more rows, more double-lane rows, tighter gaps, roadblocks from 210 m
  apart down to ~55 m apart). Blocks that would sit side by side are merged into one bigger block.
- **The police can only catch you through mistakes.** You are always slightly faster: after a hit they close in, then
  you pull away again (back to the starting 45 m gap, never further). You pull away at 1.5 m/s at first and 0.6 m/s after
  2.5 minutes, so it's several hits close together, not clean driving, that gets you caught. A hit that leaves the police 12 m or closer is fatal: the police car
  drives into yours and the run ends on contact.
- Pickups never spawn on top of each other or inside/just behind a block.
- **Portals:** a giant glowing gate fills the whole road (no way around it). A gate appears 95 m ahead after 60 seconds of
  active gameplay in each environment; pauses and the tutorial do not count. Drive through it to switch environments.
  Arriving shows "PORTAL! YOU ENTERED THE MOUNTAINS" (or "BACK TO THE CITY"). A live "PORTAL IN ... s" countdown sits
  beneath the distance and messages during the real run, changing to "PORTAL AHEAD" when the timer reaches zero.
- The tutorial starts with an 18 m gameplay gap and a nonfatal 6 m floor; the real run starts at 45 m. Police use a closer 4.5 m visual gap at the normal starting distance and turn into sideways steering while staying forward-facing when nitro makes them fall back.
- The top-right rear camera shows the roof of your car and the police behind it; its border turns red as they close in.
- Mystery boxes: none for the first 500 m, then roughly every 320 m. Reverse steering 10%, nitro, shield and police
  jammer 30% each. Only one power-up at a time: no new ? box appears while one is active or another is still on the road. Grabbing one shows a single centred message
  ("NITRO ACTIVATED!", "SHIELD ACTIVATED!", "POLICE JAMMED!"; reverse steering swaps the controls immediately). Only one
  message shows at a time and the newest replaces the last. The active power-up is a single tile under the rear camera.
- Blocks and pickups appear about 95 m ahead, so nothing is a surprise.
- Pickups (F, A, ?) never spawn inside or just behind a block; a row of blocks moves any pickup it would cover into its
  open lane. Collecting every F always keeps you fuelled (tanks every 125 m give more than you burn).
- Fuel drains 4/s from 100; F tanks every 125 m restore 35. Running out of fuel or being caught ends the run.
- **Missions:** 10 missions, 3 held at a time (listed in order). After an easy starter ("Reach 1,000 m"), every mission is a
  challenge you can retry each run ("Reach 750 m without getting hit", "Destroy 5 blocks in one run", "Reach the
  mountains without missing an F"...). Three missions are active at a time; finished ones unlock replacements during the current run. Every fresh run starts at 0/10; environment changes retain the current progress. The game-over card shows the completed count and remaining missions; live progress is in the pause menu.
- **High score:** best distance per browser, shown on the title and game-over screens ("NEW BEST!").
- **Analytics:** see `Docs/ANALYTICS.md`.

## Controls

| Action | Controls |
| --- | --- |
| Move left | `A` or Left Arrow |
| Move right | `D` or Right Arrow |
| Shoot forward | Space |
| Start | Enter or the Start button |
| Tutorial on/off (title) | `T` or the switch |
| Pause / resume | `Esc` or `P` (also pauses automatically when the tab loses focus) |
| Restart | `R` / Enter or the Restart button |

## Unity setup

1. Install Unity `6000.3.23f1` through Unity Hub.
2. Add this repository as a Unity project.
3. Open `Assets/Scenes/Env_City.unity`.
4. Enter Play mode.

The game starts in `Assets/Scenes/Env_City.unity` (build index 0) and switches to `Env_Snow` during a run.

## Source projects

- [Team 19 — Money Heist](https://github.com/CSCI-526/Team19-Kangod-Tao-Gong)
- [Team 21 — Redline Run](https://github.com/CSCI-526/Team21-He_Ali)

See `Docs/INTEGRATION.md` for the feature and code mapping.
