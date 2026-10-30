# WorldShift endless getaway

WorldShift is an endless escape through Day and Night. Collect loot, reach an exit, and choose whether to bank your haul or risk the next environment. Capture, a wreck, or empty fuel loses the entire carried haul. Money already banked stays safe.

## Play the escape

Open `Assets/Scenes/WorldShift_Menu.unity` and choose **Start Escape**. The optional tutorial teaches steering, switching, and crossing the joined worlds before the escape starts. Practice buttons open City, Desert, or the basics with checkpoint retries and no bank rewards.

- **A / D or arrows:** steer.
- **Space:** switch Day and Night. In joined sections, steer between the two halves instead.
- **Escape:** pause. Leaving through **Abandon Loot** forfeits the carried haul.

The first version alternates City and Desert indefinitely. Snow and Ocean Coast remain existing environment scenes; they are not yet connected to this escape loop. More environments can join the same segment boundary once they have validated WorldShift routes.

## The risk decision

Each environment ends with a clear 220-metre approach, beyond its authored hazards. Green **Bank** markings cover the left and centre of the road. Amber **Risk** markings occupy the right lane. The HUD highlights the lane currently selected and shows the amount that can be banked and the next environment's loot multiplier.

The choice commits at the final gate. Steer right of the marked lane boundary to continue; otherwise extract. No extra button or timed popup is needed. Fuel consumption and pursuit closing pause inside the marked exit approach so the player can read the choice. Steering and forward motion continue.

Extraction deposits the carried amount once, updates the best banked haul, and shows the result. Continuing preserves the entire carried haul and loads the next environment automatically. Each new environment starts with a full tank and normal police separation, providing a brief recovery between legs.

## Rewards and difficulty

Gold briefcases marked with a dollar sign are loot. Collect them by driving through them in their world. The opening trail provides a safe introduction; later rewards follow existing fuel locations to offer world and lane choices. Each pickup can be collected once per environment visit.

The first environment pays $100 per briefcase. Each continued leg adds 0.5 to the loot multiplier: x1, x1.5, x2, and so on, capped at x21. Later legs also add pursuit closing speed, capped after six continues. Existing hazards, relays, and the desert's freeze mechanic remain. Repeated visits reuse the authored layouts; procedural generation is a later extension.

The bank and best haul save locally across sessions. A live haul is not saved and cannot be recovered after quitting. Banked money currently measures success; a shop and permanent upgrades are deferred until the risk loop has been playtested.

## Clear screens and short guidance

The main menu gives priority to **Start Escape**, followed by a tutorial toggle and optional practice. It states the stakes in one sentence.

The escape HUD shows carried loot with **At Risk**, the current world, environment leg, a route progress bar, fuel, and the pursuit mirror. It removes sector descriptions, switch counts, retry counts, and the speed readout. Controls fade after the opening and remain available in pause.

Guidance appears near the moment it matters: **Switch worlds**, **Jump, then switch**, **Land in Night**, or **Cross worlds**. The tutorial presents one action per lesson, without paragraphs. The exit uses two short choices: **Left / Bank** and **Right / Risk**. Results show money banked or lost and offer **New Escape** and **Menu**.

Practice retains its checkpoint safety net and uses shorter ready, pause, and completion screens. This keeps learning separate from the stakes of a scored escape.

## Implementation

- `GetawayRunState` owns carried loot, leg progression, reward scaling, and one-time settlement.
- `WorldShiftFlow` owns the escape session, scene transitions, and local bank persistence.
- `WorldShiftGetaway` shares pickups, exit geometry, and escape UI between City and Desert.
- Existing City and Desert simulations collect loot through swept road-coordinate checks and end the escape on failure. Checkpoint retry commands are disabled in escape mode.
- The existing Day/Night compositor presents each loot pickup in its owning world without advancing the simulation twice.

The current goal is to test whether players want to risk another environment. Evaluate clarity of the exit lanes, intentional extraction decisions, understandable losses, and willingness to replay before adding a shop, additional currencies, or more environments.
