# Level 2 Ocean — optional helicopter pursuit

Five sections, one-hit collisions and four saved landing checkpoints remain. An
additional save at 460 m, before the first ramp, lets players retry the first jump
without replaying the opening. Ocean still ends with the ferry escape; Mountain
is the next destination in the current level order. Existing scenery, traffic and
world-switch mechanics are preserved; dry approaches are longer to reserve safe
helicopter strike pockets.

## Ammunition and counterattack

There are 12 rocket crates (2 / 2 / 2 / 3 / 3 per section).
They are collectible in Day; Night shows their ground markers. Missing one never
retries the level. Each pickup automatically launches a homing rocket from the
roof launcher, with no new input, inventory or manual aiming.

The helicopter has 6 health. At 3 hits it smokes and stops combining strikes with
Day cargo; at 5 hits it loses its twin attack and repeats last-stand barrages in subsequent safe attack pockets.
All stages keep the ordinary attack cadence. At 6 hits it explodes, immediately
cancels its attacks and falls into the sea. Remaining ammo is hidden.
A segmented health bar follows the aircraft, alongside the HUD counter. Zero
pickups still allow a win. Police stripes, POLICE lettering and red/blue beacons
match the shared cruiser. The final rocket triggers a large fireball, shock ring,
flying police-aircraft fragments and a short breakup kick. A fixed world-space dive
ends in a foam ring, water plumes, a separate crash boom/wash and a second .7-second
camera kick. Both pulses decay quickly. Effects play once and clear on retry.
The larger finned player rocket takes 1.35 seconds, arcs upward with bright exhaust
and a smoke trail, and tracks the moving aircraft. Health visuals update on arrival.
The roof launcher recoils; rocket arrival triggers
sparks, a brief flash, a small aircraft kick and a synthesized metallic hit cue.

## Attacks and switching

The five sections have 13 heavy attack starts (3 / 4 / 2 / 2 / 2). Each section now
reserves one **108 m spread field**, replacing two compact encounters instead
of stacking a long attack on top of the old Night walls. Early compact attacks normally
start 72 m apart. Sections 3-5 replace their later compact pockets with Night
driving sequences; recurring Day missiles continue where placement is safe. The
second section retains a twin salvo and a pincer so heavy attack variety remains. All damage stages use the available pockets; damage removes
cargo combinations and eventually twins instead of skipping attacks.

- **Missile / twin:** the existing single-lane and adjacent-lane strikes.
- **Spread point saturation:** seven individual targets 18 m apart, covering
  left, middle and right over 108 m. Later impacts are timed to their road
  distance, creating repeated steering decisions. Each square has its own
  warning, missile, impact and solid debris. There is no single green lane
  through the entire field; players can steer between points or freeze the attack.
- **Repeated last stand:** at 1 HP, every subsequent ordinary safe attack start
  becomes an 18-missile barrage until the boss is killed. Compact pockets retain
  an outside exit. Spread pockets have three six-missile waves 54 m apart,
  alternating the free outside lane except beside a Night obstacle, where the
  current slalom exit takes priority. Impacts beside a forced Day-return gate are
  omitted before warnings appear, including the last wave of a dense field. Saving a checkpoint does not suppress later
  barrages. The five curtain encounters retain their slower timing at 1 HP.
- **Slow missile curtain:** one authored opportunity per section, five at most
  if the aircraft survives. Fifteen missiles cover almost the full road in three
  small rows. Launches are staggered, impact takes 3.05–3.35 seconds of Day time,
  and warnings lock 90 m ahead. It asks the player to freeze the incoming field
  in Night before it reaches the road, rather than squeeze through a fake gap.
  No Night closure or cargo occupies the curtain pocket.

Warnings never retarget. Current-lane/short movement prediction selects the
attack side once, with a per-run seed retained on retries. Night freezes launch
and impact clocks and missiles. Already landed debris remains solid in both
worlds. Warning squares alone cause no damage. Full-health single/twin encounters
can still combine with Day cargo; Night lowers that cargo. Point fields and
curtains never add cargo.

Course data explicitly reserves each field's start and end. Pickups, ramps and
checkpoint recovery stay clear. Section 1 retains the gentle single Night car.
Sections 2-5 now have a Night slalom inside each spread field: opposite two-lane
closures at +16 / +58 m, side-lane oncoming cars at +22 / +76 m, then a full Night
gate at +100 m. The two half closures are 42 m apart. Their safe side alternates;
missile points beside each closure protect that side rather than one permanent
outside lane. The full gate forces a return to Day. All planned impacts within
22 m of this gate are removed before the warning is shown, leaving a clear
return window even if the player let the other missiles land. Twelve full
Night closures across the course prevent permanent Night camping. The slow
missile curtains remain separate, with an empty Night route. Ammo is Day-only.

**Routine single missiles:** while the helicopter lives, Day also has recurring
small-square strikes, about every .95 seconds where placement is safe (1.3 seconds
in the opening). Each locks 60 m ahead, launches after .3 seconds and lands after
1.8 seconds of Day time. Three lightweight pooled views reuse the existing art.
Night freezes them and stops new launches; passing a shot or defeating the boss
clears it. The scheduler does not bank skipped shots for a later burst. It avoids
ramps, respawn approaches, pickups, moving spikes, heavy compact salvos and full
world-switch gates. Ordinary obstacles and existing missile warnings are checked
together to leave at least one common outside/centre passage. Authored spread
fields can overlap this small-shot pressure without filling the protected exit.

Passed attacks expire
only after their final target, including a long field's distant waves. Pausing
freezes simulation; retry clears live effects and restores banked damage. A kill
immediately cancels every attack, with no unavoidable after-death barrage. The
Night freeze rule itself has not been weakened or given an energy/cooldown limit.

## Restored ordinary road hazards

The five sections include 15 ordinary Day barrier rows, alongside thirty-six
oncoming vehicles (four Day, thirty-two Night) and helicopter attacks. The first section
omits the opening Day car and narrows its second barrier to one lane, avoiding
an immediate traffic/barrier combination. They reuse the existing retractable
sun-marked barriers, including their original colours and sinking animation.
Two original teal police spike sliders per section (ten total) use the unchanged
WorldShiftSpikePlatform factory: metal cones, teal base and white direction
arrows. They move continuously throughout Day, including before the car approaches,
and freeze while remaining solid in Night. Their model, colours and speed are unchanged.

Spikes sit at section offsets 124 m and 347 m. The second section's later compact missile pockets move
8 m forward to clear the second spike. The long bombing fields, all 13 heavy helicopter
opportunities, 12 ammo pickups and five slow curtains remain. Selected late
closures now belong to Day, leaving readable separation from Night rows; the
last 34 m before each ramp stays clear.

## Night difficulty by checkpoint

Landing sections 1-5 have 4 / 9 / 14 / 15 / 18 world-specific Night hazards
(rows and individual cars), including 2 / 4 / 8 / 8 / 10 oncoming vehicles.
Their maximum traffic speed settings increase from 16 to 26 / 32 / 38 / 44;
Night traffic keeps moving and retains the existing moon roof badge. The short
pre-first-ramp retry checkpoint does not introduce another difficulty jump.

Sections 3-5 add a late sequence at offsets 372 / 402 / 432 / 462 m: a centre-only
opening, paired oncoming traffic, another centre-only opening and another paired
traffic row. Sections 4-5 add another centre-only gate at 492 m. The final section
also adds paired traffic at 522 m and a full Night closure at 534 m, returning to
a protected Day window. No new models or obstacle rules are introduced. The
second section's twin/pincer salvos and continuous safe Day missile scheduler
retain pressure in Day while these later sequences make Night a driving challenge.

The same five ramps and checkpoint coordinates are retained (0, 460, 736, 1512,
2298 and 3114 m). Initial difficulty and first-jump recovery are unchanged. The
late gauntlets use normal steering, with about 30 m between single-lane centre/side decisions;
ordinary opposite-world obstacles stay at least 28 m apart at their encounter
positions. Routine missiles may not close the only passage or the full Night gate's
Day-return window. The original spike meshes and all coastal scenery are retained.

## Giant airborne dolphins

The first 132 m flight has just one swimmer at takeoff +62 m, allowing time to
read the opening after launch. Its pre-ramp checkpoint at 460 m restores actual
collected ammunition and boss damage. Later flights retain two large, bright
swimmers, at takeoff +38 m and +98 m (nine dolphins total).
They face sideways and cross in opposite directions at 9 m/s, reaching beyond
both road edges. Centred bodies cover all three lanes: use Day to move them aside,
then either steer through or freeze the opening in Night. The second swimmer asks
for another sideways adjustment. Bodies stay solid in both worlds and after the
boss is defeated. Tapered, swept collision follows their broad body rather than an
oversized spherical hitbox. Every upcoming dolphin moves from the start of Day
play, even far from the player; there is no approach trigger. Night freezes all
of them. Retries reset their clocks, then Day movement resumes.
Ocean air steering remains .6 and the destination-world landing rule is unchanged.

Dolphin deaths show DAY: MOVE / NIGHT: FREEZE.
Missile deaths show NIGHT: FREEZE MISSILES / ROCKETS: HIT HELI. Only these two causes receive 3 seconds to read the hint;
ordinary cargo collisions retain the original retry timing and no mechanic hint.

Implementation: WorldShiftOceanHelicopter.cs / .Art.cs / .ArtDetails.cs / .Effects.cs / .Defeat.cs / .Dolphins.cs, optional mode in
WorldShiftLevelCollection, and the existing Ocean presentation/HUD. Short coverage
lives in WorldShiftLevelTests.Ocean.cs. Existing death analytics records AIR STRIKE or DOLPHIN COLLISION.

## Skippable helicopter introduction

Fresh Ocean entries (menu or Next Level) show a 5.5-second fly-in using the real
helicopter and coastal scenery. After a brief fly-in, the camera focuses on the
actual first rocket pickup for about three seconds, then returns to the car.
The short instruction reads DAY ONLY: COLLECT ROCKETS / AUTO-FIRE AT THE HELICOPTER /
DESTROY IT TO STOP THE BOMBING. This is a demonstration view only: it consumes no
pickup and does not damage the boss or advance the run. Skip,
Enter or Escape starts play immediately; otherwise the intro ends automatically.
Driving, attack/dolphin clocks, chase audio and attempt analytics wait until it
finishes. Checkpoint retries and full-level retries skip it. No new raster art,
video files or scene are required.

## Preserved coastal art

Ocean has an authored low-poly coastal presentation: a viaduct over animated
water, rocky palm islands, coastal houses, sailboats and striped lighthouses.
Five sections move through palm coves, villages and sea stacks, with tall paired
beacons marking the jump approaches. Day uses a turquoise coast; Night uses a
starry blue sky and turquoise bridge lighting. Jump gaps expose the sea and the
bridge edges disappear with their world's deck.

The player coupe is shared across Training and all five levels, with the same silhouette, wheels, windows, lights and maroon livery. Rocket crates bob
and rotate, then play a short glass chime and expanding ripple when collected.
Scenery has no colliders and stays outside the driving corridor. This art pass
keeps the existing layout, steering, collision rules and 12 optional ammunition pickups.

Ocean road closures reuse world-coloured retractable harbour panels with the same
sun/moon symbols, rivets, steel ribs and reflective trim. Footprints and .18-second
sinking stay consistent with the rest of the game. Older cargo/trolley/parked-van
art factories remain available, but are not part of this revised authored layout.

Oncoming traffic in the shared level renderer uses vehicles facing the player,
with approaching headlights, mirrors, windows, bumpers and rotating wheels. Ocean
includes twenty-two oncoming service vehicles (four Day, eighteen Night), with broad sun/moon
plates matching the shared barrier symbols. Day and Night encounter rows are separated.
Retractable harbour panels gain framed steel ribs and reflective trim without changing
collision or the sinking animation. Background art is unchanged.

The visual polish is Ocean-only. The five sections have authored fishing-harbour,
rocky-headland, cable-bridge, lighthouse and final-crossing landmarks. Soft contact
shadows ground cars, hazards and bridge piers; the player's contact fades during
flight and follows the road again on landing. Concrete, timber, painted steel,
glass and rubber use separate finishes with restrained highlights and wear.
World colours occupy broad hazard panels while cargo and vehicle bodies keep
natural material colours. Road scuffs and expansion joints follow each deck and
hide with it. Jump edges show end caps and girders; illuminated landing frames
appear only in the destination world. These details have no colliders.

Ocean's compact rounded HUD shows a police icon and helicopter health, smaller world/pause
buttons and the same larger progress bar, checkpoint dots and finish icon used
in the other levels. Progress movement, collection pulses and the
checkpoint notice animate smoothly. The checkpoint notice remains below the
counter, above the driving sight line; collision retries retain clear feedback.

The pursuing police use the shared black-and-white patrol cruiser, with POLICE
livery on the doors, bonnet and rear, a roof unit number and alternating red/blue
roof beacons. Ocean retains that marked model in both worlds, alongside its
existing contact shadow. Catch-up motion is unchanged; only dolphin/missile retry hints extend the pause to 3 seconds.

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
retain their shared recovery decks. Checkpoints follow the current authored landings; driving speeds are unchanged.

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
`ws_audio_effects`. Effects includes the engine, ambience, cues, existing pickup
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
