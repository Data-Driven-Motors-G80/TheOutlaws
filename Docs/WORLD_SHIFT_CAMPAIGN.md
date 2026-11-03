# WorldShift campaign

This is the gameplay reference for the current low-fidelity playtest. Open the menu as described in the [README](../README.md). Code ownership is in [Architecture](ARCHITECTURE.md); geometry and tuning are in [Curved roads](CURVED_ROADS.md).

## Run flow

**City → Desert → Ocean → Snow → Mountain → City**, automatically. The next lap is harder. Environment transitions keep carried cash, ammunition and unused shields; each environment starts with full fuel and normal police separation.

A marked cash-out side exit appears every two environments. Stay on the main road to continue, including from the centre lane. Taking the left side exit banks the carried haul and ends the run. Cashing out offers **Upgrade car** and **Start next run**. There is no Garage stop between environments.

Ordinary environment transitions have a 60 m approach (about 2–3 seconds), with the next-environment message starting at that approach. Cash-out legs retain a 440 m finishing section so players can choose the side exit. On ordinary transitions the optional bonus ramp starts before the boundary, leaving at least 24 m of road after landing before the next environment loads. Its approach remains flat and straight.

Getting caught, missing a required landing or running out of fuel loses the run's carried cash. Previously saved cash and upgrades remain. Leaving a live run also loses its carried cash. Escape has no checkpoint retry.

## Day and Nightshift

| Rule | Day | Nightshift |
| --- | --- | --- |
| Cash, fuel, ammo pickups and power-ups | Available | Hidden and unavailable |
| Marked full-width Day barrier | Solid; standard X marks | Disappears |
| Shared moving obstacles | Move | Freeze at their current position and remain solid |
| Shared static X blocks | Solid | Solid |
| Shooting with carried ammo | Available | Available |
| Fuel consumption and pursuit | Continue | Continue |

Normal speed, fuel consumption and road geometry are the same in both modes. Nightshift has no extra timer or fuel penalty. Supplies after encounters give a reason to return to Day. Stay in Nightshift until a Day barrier is behind the car.

Visible X-marked road obstacles are shootable, including traffic and Day-only walls. Ammo offers an alternative to shifting or steering; other shared obstacles always retain a steering bypass. Each environment has three Day-only full-width walls, away from forks and relay approaches. The additional wall is followed by two Day-only cash pickups roughly 50–80 metres later, rewarding a return to Day after clearing the wall. No new wall is placed across an entire fork branch without a way through.

## Roads and encounters

- **City:** mostly flat streets and short chicanes. An opening sliding block leads into a switch wall and Day rewards; another mover follows the fork rejoin. Day cash breaks up the next barrier sequence, and alternating two-lane blocks require steering in the final slalom. The 1,440 m main route plus exit extension targets roughly 55–65 seconds at normal speed.
- **Desert:** stronger canyon bends and dune-like swells, crossing rocks, freezeable traffic and switch walls. Its already dense rock sections keep their obstacle count; Day cash creates return windows between the fields and before the ramp.
- **Ocean:** tighter sweeping coastal bends, small rises, visible bridge edges, paired crossing blocks with opposite motion, then switch walls and Day supplies. An earlier moving block starts the first sequence; Day cash between crossing blocks rewards returning from Nightshift. The final alternating two-lane blocks require a lane change. The actual road retains safe steering width.
- **Snow:** firmer long bends and gradual descents. Steering grip is 0.85 in both modes, with gentle coasting on steering release and stronger countersteering to catch the slide. Extra coasting is disabled near forks, ramps and landing recovery. Slow moving blocks, a Day return window after the fork and a widely spaced final slalom reward planning; freezing hazards does not restore grip.
- **Mountain:** climbs, sharp alternating switchbacks and winding descents, half-width barriers and rock blocks approaching downhill. An extra approaching centre rock closes the gap after the fork, with Day reward windows before and after the next rock group. Early encounters introduce each separately; the descent combines approaching rocks with offset half-walls. Forks and ramp approaches remain clear.

Forks have full-width branches once separated. The left branch spaces out ammo, boost and cash in that order, with at least 20 m between same-lane supply pickups and other rewards; the right offers more Day cash beside blockers. Both rejoin the main road. The branch is preserved when switching modes. Plain overhead banners label each branch: AMMO + BOOST or CASH. They replace the fork instruction in the HUD and remain visible in Nightshift.

Each environment has a main automatic jump: launch in the source mode and switch in the air to land in the other. After a successful landing, early return switches are safely ignored until both roads rejoin. The prompt explains the restriction, then confirms that switching is safe. This does not prevent a missed landing if the player never switches in the air.

An optional right-lane bonus ramp in the final extension offers elevated Day cash. It works in either mode; the ordinary ground route remains underneath and cannot collect the aerial cash trail. Fuel consumption and pursuit closing pause in this final recovery extension; solid impacts still count.

## Controls and resources

**A/D or arrow keys:** steer. **Space:** switch. **F or left-click:** shoot. **Esc:** pause/resume. Pause also displays controls.

Start with three shots; labeled AMMO pickups refill to three on the main road and the clear fork branch. Shots immediately clear the targeted block and its collision. The small visible projectile follows the road, pauses with driving time and clears on run end.

Day supplies are spaced around useful stretches:

| Pickup | Effect |
| --- | --- |
| FUEL | Restores 26 fuel units |
| SHIELD | Absorbs one Day impact; an unused shield carries between environments |
| MAGNET | Collects nearby Day cash on the selected road branch for six driving seconds |
| BOOST | Adds 20% speed for four driving seconds, with normal steering available |
| Cash block with $ | Adds to the carried haul at the current loot multiplier |

Active power-up benefits are disabled in Nightshift. Boost and magnet timers still advance with driving time; pausing stops them. An unused shield is stored, not consumed in Nightshift. Ammo pickups are Day-only too. Carried ammo can still be fired in either mode. Mountain ammo is placed before its fuel pickup so their blocks and collection windows remain separate.

Normal cruise is 25.2 m/s on the shared finite courses and 21.6 m/s in Desert. Main relay jumps retain their authored speed curves, up to 42 m/s. All visible solids use primitive blocks. Day obstacles are red/brown with white X marks; frozen Nightshift obstacles are opaque icy blue with navy X marks. Mode changes do not add flashing, frost particles or new artwork.

## Difficulty and upgrades

Carried cash earns an extra 0.5 multiplier per environment, capped at x21. Pursuit pressure increases with continued environments and is capped. Each completed five-environment lap increases moving-obstacle speed by 15%, capped at 45%. Difficulty preserves mode rules and safe steering bypasses.

The Garage is opened through **Upgrade car** on the main menu or results screen. Upgrades apply on the next run and persist locally after losses. Each has three levels costing $500, $1,000 and $2,000:

- Fuel tank: capacity 100 → 110 → 120 → 130.
- Armor: collision penalty reduction 0% → 5% → 10% → 15%.
- Headlights: Nightshift range 74 → 89 → 104 → 119 metres.

Saved cash and upgrades are local to the device/browser, not a cloud account.

## Tutorial and interface

The optional tutorial teaches escaping a Day-only roadblock through Nightshift, steering past stopped but solid traffic, returning to Day for cash and supplies, shooting, and switching midair from a Day ramp to a Nightshift landing. Cash is hidden and uncollectable at night, with no supply outlines. It uses the same shared hazards, real road gaps and post-landing switch protection as gameplay. Early actions count; players never have to toggle back just to replay a demonstration. A lesson waits safely only while its action remains unfinished. Retry cards keep the lesson instruction instead of flashing a generic message. Jump instructions stay stable across takeoff and landing, and the shooting-gap handoff has no blank flash. It has no fuel drain or mandatory reading timer. Key cues use A/D, SPACE and F / CLICK; completed actions clear their cards.

The menu has one vertical stack: **Start run**, **Tutorial**, **Upgrade car**, with saved cash under the upgrade action. Driving shows carried cash, fuel and ammo, plus one contextual prompt. Jump instructions and switch-wall instructions take priority over optional guidance. Pickups give silent visual feedback rather than extra message popups. Nightshift occasionally reminds the player to return to Day for supplies; the reminder is suppressed while recovery pauses fuel consumption.

The playtest is silent and uses primitive shapes, flat panels and functional labels. It does not use the older World's mystery backpack, missions, surf simulation, automatic portals or split-screen finale. Their development scenes remain available separately; see the [scene guide](SCENES.md).
