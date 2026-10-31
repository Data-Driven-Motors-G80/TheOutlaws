# WorldShift playable flow

Open `Assets/Scenes/WorldShift_Menu.unity` and press Play, or use **The Outlaws > World Shift > Open Game Menu**. The menu offers **Endless escape**, which starts directly in City, and an optional **Tutorial**. Finishing or skipping the tutorial leads into Escape. There is no Practice submenu.

## Goal and Day/Night rules

Collect loot, then steer left at an exit to keep it or right to risk the haul in the next environment. City and Desert alternate indefinitely. Capture, an unsuccessful run, or empty fuel loses the carried haul. The bank and best banked getaway persist locally; live runs do not. There is no shop or permanent upgrade system.

**Day is for earning. Night is for surviving.** Both Escape environments use the same rules:

- **Day:** traffic and rolling rocks move. Cash boxes and all power-up choices are visible and collectible.
- **Night:** the same road hazards freeze at their current positions and remain solid. Cash boxes and power-up pickups disappear and cannot be collected, including by the magnet. Returning to Day resumes motion from the frozen position and restores uncollected rewards still ahead.
- **Both:** fuel and ammo remain available, shooting works, the car keeps moving, and police keep chasing. Night has no additional timer or resource cost. Active powers keep their existing driving-time durations.
- Roads, ramps and landing decks stay available when switching. Automatic jumps can launch and land in either world; switching is useful for freezing hazards or returning to Day for airborne loot, without a mandatory world change to land.

The header labels the selected state **Day / loot** or **Night / freeze**. Switching never removes an obstacle or changes the selected fork. Fuel drain and pursuit closing pause in the final extension; solid impacts still count there.

## Routes and decisions

City has a 3,360 m authored route with three automatic jumps. Its Escape layout uses spaced shared hazards, including moving oncoming traffic, shootable single-lane blocks, and fixed fire/police obstacles with steering bypasses. The former overlapping world-specific closures were replaced with spaced obstacles on one road. Gentle street bends lead into straight fork and ramp approaches. Moving traffic uses a Day-only simulation clock after entering its approach range; collision checks and rendering use the same positions.

Desert has a 2,880 m route with rolling rocks, freezeable oncoming traffic and an airborne rock section. Its Escape walls are shared half-width barriers, with room to steer around them. Finale rocks are present in both worlds. The former centre-only crossing guards are omitted in Escape so freezing a rock does not force a collision. Rocks continue rolling in Day and preserve their exact positions in Night. Broad desert curves follow the open landscape; the canyon choice has a straight approach and opens back out before later hazards.

Each environment has one fork during the main chase:

- **City, 1,120–1,400 m:** a broad main road on the left and a narrower side street on the right. Plain building boundaries mark the side street's usable space.
- **Desert, 720–1,000 m:** an open road on the left and a narrow canyon on the right. Plain canyon walls communicate its reduced visibility and steering room.
- The left branch offers ammo and two Day cash boxes. The right offers six Day cash boxes behind two breakable obstacles. The left has more steering room (up to 5.8 m of road width versus 3.8 m on the right) and stays clear without ammo.
- Both paths are visible before steering commits the choice; no new controls or decision screen are used. The roads rejoin before later hazards and ramps. Switching Day/Night preserves the chosen road and its geometry. All cash stays Day-only.
- Normal road hazards, traffic, buildings and central pickups are cleared or repositioned around the fork so nothing sits in the gap between roads. Ammo before the fork is at 780 m in City and 640 m in Desert; the left fork offers another refill. Fuel remains available in both worlds.

A shorter 440 m extension follows each environment. An optional right ramp offers six elevated Day cash boxes, while the ground bypass offers two Day boxes. Elevated rewards require the ramp; either world can launch and land safely. A separate final junction offers green **Exit** on the left and amber **Next** on the right. Steering commits the choice as the roads separate; the gate banks the haul once or carries it into the next environment. The previous end-of-environment reward fork has been moved into the chase rather than duplicated.

Continued legs increase loot by 0.5 per leg, capped at x21, and pursuit pressure, capped after six continues. Each environment restores fuel and normal police separation. Cruise speed stays stable between environments. Results show the best banked getaway, total savings and a new-best message when beaten. Snow and Ocean Coast remain separate development scenes outside this loop.

## Controls, shooting and power-ups

**A/D or arrows** steer. **Space** switches Day/Night. **Left-click or F** shoots. **Escape** pauses. Leaving a live run loses the carried haul. Escape has no checkpoint retries.

Start with three rounds. White three-round pickups refill to three before the main fork and again on its safe branch. Ammo carries between Escape environments. Shots clear cracked barriers and rocks in either world, with modest assistance in the current lane. Cars, fire and spikes cannot be destroyed. Shared fork obstacles cannot be removed by switching worlds.

Shots use the original game's small yellow sphere, with the same 0.28 m size, muzzle offset and 70 m/s travel speed. The visible shot follows the road around bends. Hit resolution remains immediate, removing both the obstacle and its collision. Visible bullets pause with driving time and clear on retry or run end. There are no shot sounds.

Near the start of each environment, Day offers one automatic power-up choice; collecting one removes the other choices for that visit. All can be skipped. Night hides all three pickups.

| Lane | Power-up | Effect |
| --- | --- | --- |
| Left | Shield | Absorbs one solid impact, with brief protection against overlapping contacts. An unused shield carries between environments. |
| Middle | Boost | Adds 20% forward speed and reduces steering input by 20% for four driving seconds. Police retain their normal pace. |
| Right | Magnet | Pulls Day loot across the selected road branch for six driving seconds. It cannot collect in Night or pull elevated bags from the ground. |

Powers use dark diamond markers: cyan shield, orange bolt, purple magnet. Fuel uses a green F can. Ammo uses three white capsules. Cash uses a low primitive gold box with a dollar symbol. Fuel and cash occupy separate lanes. Rings and trails only communicate active powers. Power durations pause with the game; boost and magnet reset between environments.

## Tutorial and guidance

The separate 576 m tutorial runs at 18 m/s (about 32 seconds of driving, plus time spent on the shooting lesson), with no fuel drain. It teaches steering, freezing shared traffic with Space, returning to Day for a small loot trail, shooting a marked block, and steering through an opening. The first moving traffic encounter leaves room to finish the steering lesson. The final steering section follows shooting closely, with obstacles at 510 m and 558 m and the finish just after the last one. Its cash boxes use the same primitive appearance and Day-only collection rule as Escape, without adding a currency HUD.

At the shooting lesson, forward movement waits safely before the block until the player clicks or presses F. **Click to shoot the block.** becomes **Nice shot!** for one driving second. Earlier lessons keep shooting disabled. Steering and switching instructions clear after their actions and stay dismissed for that lesson; retrying restores them. Tutorial completion offers **Start your escape**.

Driving uses one contextual prompt above the road, below the status strip and left of the mirror. At the 1280×720 reference size, prompts use 26 px type with a 24 px minimum. The car and lower road stay free of text. Urgent road instructions, pickup acknowledgements and route guidance share that slot. Ramp approaches say **Drive up the ramp to jump.**; takeoff is automatic. Escape no longer asks players to switch worlds to make a landing deck appear.

The first Escape crate says **Drive through to collect**. Collection removes it and briefly pulses the existing loot total for 0.35 driving seconds, with no additional popup. **Grab loot. Exit to keep it.** is the short opening goal. Exit choices replace the contextual prompt. Pause and results hide driving stats.

Fuel is always a rounded-up percentage, red below 25%. Three ammo pips replace the numeric speed readout. The pursuit mirror remains at the top right, preserving image proportions and turning its border red for danger. There are no duplicate fuel bars, progress bars, driving clocks or control footers. Roadside instruction text, other-world outlines and the merged-world finale are removed from player-facing routes.

## Pace and beta scope

Normal Escape cruise is 30.8 m/s in City and 26.4 m/s in Desert. Boost reaches 36.96 or 31.68 m/s. Authored jumps retain their original speed curves, including the 42 m/s peak; optional ramps retain 28/24 m/s. Night does not change player speed. Solid impacts cost 22.5 m of the initial 45 m police gap, with protection against repeated overlapping contacts.

WorldShift is silent: no music, switch sounds, shot sounds or pickup sounds. New presentation uses functional primitive shapes and symbols, without decorative assets, particles, bobbing or glowing loot.

The shared rules apply to the menu's Escape run and revised tutorial. Standalone checkpoint Practice scenes and rendering validation scenes retain their older authored layouts for development; they are not menu choices. Their existing tests remain useful for the legacy mechanics.

`GetawayRunState` owns loot, ammo, powers and settlement. `WorldShiftGetaway` shares reward collection and UI; `WorldShiftCombat` shares targeting, ammo and destruction. `WorldShiftForkRoad` owns split-road geometry and branch commitment. `WorldShiftRelay` and `WorldShiftRoute` share launch, landing and road availability. `WorldShiftFlow` owns scene routing and bank persistence.

Verification covers Day-only rewards and powers, exact Night freezing and resuming, solid frozen obstacles, shared fuel/ammo, continued pursuit, Night launches and either-world landings, complete City traversal, tutorial completion and prompt dismissal, shooting, forks, settlement and legacy route checks. Pacing, reward visibility and how often players choose each world still need player testing.
