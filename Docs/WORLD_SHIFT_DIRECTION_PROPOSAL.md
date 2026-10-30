# WorldShift endless getaway

WorldShift is an endless escape through Day and Night. Collect loot, reach an exit, and choose whether to bank your haul or risk the next environment. Capture, a wreck, or empty fuel loses the entire carried haul. Money already banked stays safe.

## Play the escape

Open `Assets/Scenes/WorldShift_Menu.unity` and choose **Play escape** under **Endless escape**. The first escape includes a short lesson in steering and switching between worlds. **Practice** opens a separate route picker for City or Desert, with nearby retries and no bank rewards. **New here? Learn to play** opens the lesson on its own.

- **A / D or arrows:** steer.
- **Space:** switch Day and Night throughout the route, including the final stretch and exits.
- **Escape:** pause. Choosing **Leave this run** forfeits the carried haul; the pause screen states the amount before you choose.

Only one world appears on screen at a time. Steering never changes worlds; Space always does. The merged-world finale and its portal have been removed from escape, practice, and the tutorial.

The first version alternates City and Desert indefinitely. Snow and Ocean Coast remain existing environment scenes; they are not yet connected to this escape loop. More environments can join the same segment boundary once they have validated WorldShift routes.

## The risk decision

Each environment ends with a clear 400-metre extension beyond its authored hazards. The first part offers an optional loot ramp; the final 220 metres offer extraction. Green **Exit** markings cover the left and centre of the road. Amber **Next** markings occupy the right lane. The HUD highlights the lane currently selected and shows the amount that can be banked and the next environment's loot multiplier.

The choice commits at the final gate. Steer right of the marked lane boundary to continue; otherwise extract. No extra button or timed popup is needed. Fuel consumption and pursuit closing pause through this clear bonus and exit stretch so the player can read the choice. Steering and forward motion continue.

Extraction deposits the carried amount once, updates the best banked haul, and shows the result. Continuing preserves the entire carried haul and loads the next environment automatically. Each new environment starts with a full tank and normal police separation, providing a brief recovery between legs.

## Rewards and difficulty

Gold briefcases marked with a dollar sign are loot. Collect them by driving through them in their world. The opening trail provides a safe introduction; later rewards follow existing fuel locations to offer world and lane choices. Each pickup can be collected once per environment visit.

Near the start of each escape leg, choose one automatic power-up: a cyan shield on the left in Day, a purple magnet on the right in Night, or a gold boost in the middle in Night. Taking one removes the other choices for that leg. You can pass them all without taking one. A carried shield leaves the Night alternatives available on the next leg.

The magnet lasts six driving seconds and draws in loot across the road in the current world. It cannot collect elevated ramp bags from underneath. A Night loot trail makes the reward immediately visible. The boost lasts four driving seconds, adds 20% forward speed, and reduces steering input by 20%. Police retain their normal speed, so boosting creates separation. Both effects pause with the game and reset between environments. A shrinking purple ring or gold exhaust trails show the active effect without another HUD panel.

The shield lasts until used. Driving through it automatically equips one shield, shown by cyan rings around the car. It absorbs one solid impact, with a brief grace period for overlapping contacts. Shields do not stack, carry across continued environments, and disappear when used or the run ends. They do not prevent falls, capture from a depleted pursuit gap, or running out of fuel.

Before each extraction, an optional right-lane ramp leads through six elevated bags. A wide ground route on the left offers two bags. The ramp takes off automatically, guides the short approach with low rails, and restores full steering in the air. The landing is supported by the ordinary road in either world; this bonus jump never requires a world switch. Bags follow the actual flight arc, and their rewards require taking the ramp, so they cannot be collected by driving underneath. Existing world-switch relay jumps remain unchanged.

The first environment pays $100 per briefcase. Each continued leg adds 0.5 to the loot multiplier: x1, x1.5, x2, and so on, capped at x21. Later legs also add pursuit closing speed, capped after six continues. Existing hazards, relays, and the desert's freeze mechanic remain. Repeated visits reuse the authored layouts; procedural generation is a later extension.

The bank and best haul save locally across sessions. A live haul is not saved and cannot be recovered after quitting. Banked money currently measures success; a shop and permanent upgrades are deferred until the risk loop has been playtested.

## Driving pace

City cruises at 30.8 m/s and Desert at 26.4 m/s, 10% faster than their original pace. That pace stays consistent across continued environments; there is no gradual speed escalation. The optional boost still adds 20% for four driving seconds, taking normal cruising to 36.96 m/s in City or 31.68 m/s in Desert. Police match normal cruising, with the existing small closing pressure; only the pickup boost creates extra separation.

The tutorial retains 18 m/s. Authored relay approaches, flight, and landing recovery keep their original speed curves and steering, including their 42 m/s peak. The optional bonus ramp and its recovery also keep the original 28 m/s City or 24 m/s Desert pace. Pickup boosts do not override those jump sequences. The standalone short Desert exercise and rendering validation scene retain their original speeds.

## Clear screens and short guidance

The home screen has two mode cards: **Endless escape** and **Practice**. Each gets a short, plain explanation and one action. Rounded cards show small illustrated Day/Night roads; a gold Escape button and a quieter blue Practice button establish the action hierarchy. The background fades from dusk into night. The Practice route picker uses the same card style, and keyboard focus starts on the primary action. Route selection appears only after choosing Practice. The home screen has no bank totals, keyboard legend, or tutorial toggle competing with the mode choices.

The lesson shows one friendly prompt at a time, such as **Tap Space to try night.** It has no driving statistics, sector headings, or extra control panels. Completion says **You've got it!** and names the next action: starting an escape or trying a practice route.

As the first chase starts, a short cue appears over the moving road: **You're the getaway driver. Grab loot. Reach an exit to keep it.** It fades after seven seconds, pauses with the run, and does not repeat between environments. It adds no loading screen, button, or interruption.

The escape HUD shows **Your loot**, the current world, the environment, a route progress bar, fuel, and the pursuit mirror. Leg numbers and persistent reward multipliers are removed; the next reward rate appears only at an exit, where it helps the decision. Controls fade after the opening. The exit offers **Left: Cash out** and **Right: Keep going**, with no extra instruction banner.

Pause and results hide the driving HUD completely. **Leave this run** is paired with a short sentence saying how much loot will be lost. Success says **Nice getaway!** and shows the saved amount. Failure explains what happened in everyday language and offers **Play again**. Practice screens follow the same tone, without scorecards or retry counts.

## Implementation

- `GetawayRunState` owns carried loot, leg progression, reward scaling, and one-time settlement.
- `WorldShiftFlow` owns the escape session, scene transitions, and local bank persistence.
- `WorldShiftGetaway` shares pickups, exit geometry, and escape UI between City and Desert.
- `WorldShiftBonusJump` shares the optional ramp and flight curve across simulation and geometry. `WorldShiftGetawayExtras` adds the ramp, landing markings, power-up choices, timed magnet and boost effects, protection grace period, and visible power effects.
- Existing City and Desert simulations collect loot through swept road-coordinate checks and end the escape on failure. Checkpoint retry commands are disabled in escape mode.
- Pickups and hazards appear and interact only in the selected world. The old split-view compositor remains only in the standalone rendering validation scene.

The current goal is to test whether players want to risk another environment. Evaluate clarity of the exit lanes, intentional extraction decisions, understandable losses, and willingness to replay before adding a shop, additional currencies, or more environments.
