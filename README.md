# Wee Woo Run (The Outlaws)

A fast-paced Unity police-chase game where players weave through traffic,
manage limited fuel and ammunition, shoot obstacles, and
survive an endless chase for as long as possible.

The project uses Team 19's **Money Heist** project as its foundation and adapts
selected gameplay ideas from Team 21's **Redline Run** into one cohesive game.

## Gameplay

- **Title screen:** *Wee Woo Run*, your best distance, a tutorial ON/OFF switch (ON for a browser's first game, OFF after)
  and your missions (completed / 10, the current mission and the next two).
- **Tutorial (show, don't tell):** one small box with one short instruction (LEFT AND RIGHT, AVOID OBSTACLES, GRAB FUEL, GRAB AMMO, SHOOT)
  that stays until you do it; A / Left Arrow and D / Right Arrow are labelled LEFT and RIGHT. A miss flashes "TRY AGAIN!" for a second. Order: move both ways → dodge a block → fuel plunges
  (never empties) and the F waits in the open lane past a block → collect A boxes → shoot a "2" wall (driving into it resets:
  collect ammo again, then a new wall). The car drives at 14 m/s, the police can close in but never catch you, and nothing
  counts. The real run then starts seamlessly with a "GO!" chip and distance counting from 0.
- The fuel bar turns red under 30%.
- Blocks show the shots needed on their front face (green 1, yellow 2). Pickups are labelled on their front face:
  **A** ammo, **F** fuel, **?** mystery. Ammo is shown as one blue square per round (max 5, start with 2). Shots fly down
  the whole visible road (stopping at walls) and there is no fire delay: every Space press fires. Blocks are bigger and gaps are sometimes tight.
- Hitting blocks costs police distance (green 9 m, yellow 15.75 m plus 10% fuel).
- Roadblocks start at 200 m and come every 150–210 m (90–130 m after 2,000 m). The mix of blocks never changes
  (55% green 1s, 45% yellow 2s); only how many there are increases. Every row and roadblock still **leaves one
  lane open**, so ammo is the practical shortcut, never a requirement. A boxes come every 90–220 m to match.
- Difficulty: speed rises 20 → 30 m/s over 90 s. Obstacles start sparse during a 30 s warm-up, build up over two minutes,
  then **keep getting denser for the whole run** (more rows, more double-lane rows, tighter gaps, roadblocks from 210 m
  apart down to ~55 m apart). Blocks that would sit side by side are merged into one bigger block.
- **The police can only catch you through mistakes.** You are always slightly faster: after a hit they close in, then
  you pull away again (back to the starting 45 m gap, never further). You pull away at 1.5 m/s at first and 0.6 m/s after
  2.5 minutes, so it's several hits close together, not clean driving, that gets you caught.
- Pickups never spawn on top of each other or inside/just behind a block.
- **Portals:** a giant glowing gate fills the whole road (no way around it). The first, to the mountains (snow), stands at
  500 m; each environment lasts another 500 m before the next gate. Arriving shows "PORTAL! YOU ENTERED THE MOUNTAINS"
  (or "BACK TO THE CITY").
- The top-right rear camera shows the roof of your car and the police behind it; its border turns red as they close in.
- Mystery boxes: none for the first 500 m, then roughly every 320 m. Reverse steering 10%, nitro, shield and police
  jammer 30% each. Only one power-up at a time: no new ? box appears while one is active or another is still on the road. Grabbing one shows a single centred message
  ("NITRO ACTIVATED!", "SHIELD ACTIVATED!", "POLICE JAMMED!"; reverse counts down "CONTROLS SWAP IN 3, 2, 1"). Only one
  message shows at a time and the newest replaces the last. The active power-up is a single tile under the rear camera.
- Blocks and pickups appear about 95 m ahead, so nothing is a surprise.
- Pickups (F, A, ?) never spawn inside or just behind a block; a row of blocks moves any pickup it would cover into its
  open lane. Collecting every F always keeps you fuelled (tanks every 125 m give more than you burn).
- Fuel drains 4/s from 100; F tanks every 125 m restore 35. Running out of fuel or being caught ends the run.
- **Missions:** 10 missions, 3 held at a time (listed in order). After an easy starter ("Reach 1,000 m"), every mission is a
  challenge you can retry each run ("Reach 750 m without getting hit", "Destroy 5 blocks in one run", "Reach the
  mountains without missing an F"...). Only the 3 you start a run with can be completed in that run; finished ones are
  replaced from the next run. The game-over card lists the run's missions with ticks; live progress is in the pause menu.
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
