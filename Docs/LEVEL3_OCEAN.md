# Level 3 Ocean

Ocean uses the shared level controller: five coastal driving sections, a world-switch
ramp at the end of each section, one-hit crashes and checkpoints after the first
four landings. The final landing finishes the level and unlocks Snow.

The new rule is **collect every diamond**. Faceted mint diamonds above glowing road circles
exist in both Day and Night. Drive through them; no extra button is needed.
They are required targets, separate from the X-marked obstacles.

Passing a diamond by more than 5 m without collecting it shows
MISSED DIAMOND - RETRY and returns to the current checkpoint. A crash, missed
jump or manual checkpoint retry also resets that section's diamonds. Diamonds
from completed sections stay collected. Restarting the whole level clears all
items. The HUD shows the total collected out of 11.

| Section | Required diamonds | Focus |
| --- | --- | --- |
| 1 | 1 | Collect a central diamond, then switch and freeze |
| 2 | 2 | Steer from the left target to the right target |
| 3 | 2 | Freeze moving blocks and line up for targets |
| 4 | 3 | Collect across lanes between world-specific walls |
| 5 | 3 | Combine collection, steering and freezing |

Items are at least 60 m after a checkpoint and 60 m before a ramp, with clear
space around each target. Instructional green signs appear only in Training.
Both worlds keep the same targets, speed and road geometry.
The course is 2,729 m. Fuel, cash, shooting and power-ups remain disabled in level play.

Ocean has an authored low-poly coastal presentation: a viaduct over animated
water, rocky palm islands, coastal houses, sailboats and striped lighthouses.
Five sections move through palm coves, villages and sea stacks, with tall paired
beacons marking the jump approaches. Day uses a turquoise coast; Night uses a
starry blue sky and turquoise bridge lighting. Jump gaps expose the sea and the
bridge edges disappear with their world's deck.

The Ocean car has a coupe silhouette, wheels, windows and lights. Diamonds bob
and rotate, then play a short glass chime and expanding ripple when collected.
Scenery has no colliders and stays outside the driving corridor. This art pass
keeps the existing layout, steering, collision rules and 11 required diamonds.

Ocean obstacles use coastal models: corrugated shipping containers and striped
harbour barricades close the road, while strapped fishing crates fill individual
lanes. Sideways movers are powered inspection trolleys: inset rails, drive rollers,
electric motor housings and directional markings explain their shuttle motion.
Some lane closures use parked coastal service vans; their wheels remain still.
Their world colours, dashed other-world footprints, movement and collision sizes
remain the same. Model details stay within the existing hazard footprints.

Oncoming traffic in the shared level renderer uses vehicles facing the player,
with approaching headlights, mirrors, windows, bumpers and rotating wheels. Ocean
currently has no oncoming traffic placements; its service vans are stationary
closures. Vehicle art changes do not add traffic or alter hazard timing.

The visual polish is Ocean-only. The five sections have authored fishing-harbour,
rocky-headland, cable-bridge, lighthouse and final-crossing landmarks. Soft contact
shadows ground cars, hazards and bridge piers; the player's contact fades during
flight and follows the road again on landing. Concrete, timber, painted steel,
glass and rubber use separate finishes with restrained highlights and wear.
World colours occupy broad hazard panels while cargo and vehicle bodies keep
natural material colours. Road scuffs and expansion joints follow each deck and
hide with it. Jump edges show end caps and girders; illuminated landing frames
appear only in the destination world. These details have no colliders.

Ocean's compact rounded HUD shows a diamond icon and count, smaller world/pause
buttons and a thin progress bar. Progress movement, collection pulses and the
checkpoint notice animate smoothly. The checkpoint notice remains below the
counter, above the driving sight line; missed-item retries retain clear feedback.

The pursuing police use the shared black-and-white patrol cruiser, with POLICE
livery on the doors, bonnet and rear, a roof unit number and steady red/blue
light-bar lenses. Ocean retains that marked model in both worlds, alongside its
existing contact shadow. Catch-up timing and checkpoint retries are unchanged.

The four saved checkpoints use coastal road gantries at the exact respawn
positions. Galvanised posts sit outside the carriageway, with a small dark
CHECKPOINT panel, reflective green accents and the green road stripe. A brief
light sweep and quiet chime confirm a newly reached checkpoint; its tick stays
lit after retrying. Switching worlds or respawning does not replay confirmation.
Gantries stay present in both worlds, separate from amber/blue landing frames.
The final crossing retains the chequered finish gate. Gantry art has no colliders.

Space switches immediately during flight and after touchdown. A correctly reached
landing opens its recovery deck in both worlds, including the matching road,
bridge edges and landing frame. Before a successful landing, the launch world's
landing stays missing and touching down in the wrong world still retries the
section. Retrying before a ramp restores that puzzle; saved, completed sections
retain their shared recovery decks. Checkpoint positions and driving speeds are
unchanged.
