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

The player coupe is shared across Training and all five levels, with the same silhouette, wheels, windows, lights and maroon livery. Diamonds bob
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
buttons and the same larger progress bar, checkpoint dots and finish icon used
in the other levels. Progress movement, collection pulses and the
checkpoint notice animate smoothly. The checkpoint notice remains below the
counter, above the driving sight line; missed-item retries retain clear feedback.

The pursuing police use the shared black-and-white patrol cruiser, with POLICE
livery on the doors, bonnet and rear, a roof unit number and alternating red/blue
roof beacons. Ocean retains that marked model in both worlds, alongside its
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

Ocean uses the same player and police model factories as the other levels; it
adds its coastal lighting and contact shadows without creating vehicle overrides.

The shared cruiser has a looping, distance-attenuated police wail and alternating
red/blue lights with local spill on nearby surfaces in both worlds. Audio fades
in and grows louder as the cruiser approaches the player, reaching full volume
within 8 m and fading out by 70 m. Distance is measured from the player car,
with stereo positioning preserved independently of the chase camera. Pausing
or a stopped Training lesson suspends the sound and light cycle; resuming keeps
their phase. Completion, non-capture retries and scene cleanup stop the effects.
The siren is an original locally synthesized 3.2-second mono loop (570–1040 Hz),
not an external recording. Police AI, collision rules and retry timing are unchanged.

### Driving audio

All current levels have a speed-responsive engine, tyre squeal during sharp ground
steering, ramp takeoff, landing, collision and completion sounds. Airborne engine
volume drops and the engine free-revs slightly. Ocean adds coastal wind/waves,
sparse daytime gull calls and the full licensed instrumental song "Surf Shimmy" by Kevin MacLeod. All five
environments have a distinct licensed song; Training stays quiet. Night smoothly
softens the same recording over .75 s without changing its playback position.
Ocean ambience remains exclusive to Ocean.

Pause has separate Music and Effects sliders, saved in `ws_audio_music` and
`ws_audio_effects`. Effects includes the engine, ambience, cues, existing diamond
and checkpoint chimes, traffic warning horns and the police siren. Sound playback
pauses with gameplay and stopped Training lessons. Music ducks on retry and
makes room for a short completion sting on success. Existing collection chimes
are reused rather than doubled. Gameplay and level unlocks are unchanged.

The effects WAVs are original synthetic audio. Rebuild them with
`python3 Tools/Audio/generate_game_audio.py` from the repository root; this uses
only the Python standard library and preserves existing asset GUIDs. Licensed
songs are separate full recordings under `Assets/Resources/WorldShiftMusic`, with
source URLs, hashes and CC BY 4.0 attribution in `CREDITS.txt`. Pause has Music
credits with links to the composer and license. The police siren is capped at .04
instead of .15 and low-pass filtered at 1600 Hz to leave space for the soundtrack.

### Ferry getaway ending

Success saves progress and records completion before a 6.5-second, skippable
getaway sequence. The shared coupe drives onto a ferry, the cruiser brakes at
the dock, the loading ramp rises and the ferry departs. A camera sweep, moving
water, wake, lighthouse and receding siren carry the story into a "You lost them"
reveal with the next destination. The victory sting waits for that reveal.
Escape, Enter or the Skip button skips after the opening .65 seconds; Continue
then loads the next level. Replaying and returning to the menu stay available.

`WorldShiftLevelEscape` uses a separate temporary stage with the shared car art,
no colliders and no simulation/progress changes. It restores hidden driving
renderers/world-space signs and releases stage meshes, materials and audio on
scene exit. Training finishes with readiness copy; the other environments have
themed closing-passage escapes.
