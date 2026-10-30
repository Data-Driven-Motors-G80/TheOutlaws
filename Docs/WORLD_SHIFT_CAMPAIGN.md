# World Shift playable flow

The menu now starts the **Endless Getaway** mode: collect loot, steer left to bank at an exit, or right to continue into the next environment. City and Desert loop with increasing rewards and pursuit pressure. Capture, wrecks, or empty fuel lose the carried haul. Practice retains the original checkpoint flow described below.

Open `Assets/Scenes/WorldShift_Menu.unity` and press Play, or use **The Outlaws > World Shift > Open Game Menu**. This scene is also first in Build Settings. Existing environment scenes remain available.

The home screen separates **Endless escape** from **Practice**. Practice opens City and Desert route buttons; **New here? Learn to play** opens the tutorial. The available routes are:

- **City:** the existing 3,360 m route, three airborne relays and a final stretch of world-specific obstacles. Normal cruise is 30.8 m/s; jump sequences retain their original speed curves.
- **Desert:** 2,880 m. Normal cruise is 26.4 m/s; the authored relay retains its original 24–42 m/s curve. 58 rocks (including six airborne rocks), 14 freezeable oncoming police cars, alternating world walls, six checkpoints, shared fuel and the existing police/mirror. The final 480 m retains manual Space switching and shows only the selected world.
- **Tutorial:** an independent 720 m city course at 18 m/s (about 40 seconds). Steering, switching, returning to Day, and choosing whether to steer around an obstacle or switch worlds. No fuel drain. Retries restart the current lesson.

The first escape includes the short tutorial. **New here? Learn to play** also opens it independently. Tutorial can also be selected independently; its Continue button leads to City. City practice completion offers **Try the desert** and **Back to menu**. Desert practice completion offers **Try again** and **Back to menu**. Pause screens offer a clear way to resume or leave.

## Escape rules

- **A/D or arrows** steer; **Space** switches Day/Night throughout the route; **Escape** pauses. Escape mode disables checkpoint retries. Leaving a live run loses its carried loot.
- Each environment adds a 400 m clear extension. An optional automatic ramp on the right offers six elevated Night bags; the left ground route offers two Day bags. Elevated bags require taking the ramp. The jump lands safely in either world without a required switch.
- The last 220 m offer green **Exit** markings on the left/centre and amber **Next** markings on the right. The final gate commits the choice: cash out once, or carry the haul into the next environment. Fuel drain and pursuit closing pause through this bonus/exit stretch.
- Each new environment starts with full fuel and normal police separation. Bags start at $100; continued legs add 0.5 to the loot multiplier, capped at x21. Pursuit pressure also increases, capped after six continues. Driving speed does not increase between environments.
- The bank and best haul persist locally. Live hauls are not saved. Snow and Ocean Coast are separate environment scenes and are not part of this loop. Banked money currently measures success; there is no shop or permanent upgrade system.

## Loot readability and beta presentation

Loot uses low-fidelity gold cash boxes made from primitive cubes: a short body, lid, handle, and a large dollar symbol on the face. Their colliders are disabled. There are no floating word labels, decorative textures, glowing materials, bobbing, particles, or pickup sounds. The first Day crate in the first environment uses **Drive through to collect** in the existing single prompt slot; road guidance still takes priority. The instruction stops after collection or passing that crate and does not repeat in later environments.

Collection removes the box immediately and briefly highlights/enlarges the existing loot total for 0.35 driving seconds. This is functional confirmation without another text popup; pausing freezes it. WorldShift has no music or sound effects, including the removed Day/Night switch sound. Beta presentation changes are limited to communicating objects, state and actions; purely aesthetic art and effects are out of scope.

## Escape power-ups

Power-ups use dark diamond markers with a cyan shield, purple magnet, or orange lightning bolt. They have no dollar symbols or cash-box geometry. Fuel uses a green can with an F on its face. Cash boxes occupy a different lane from fuel, so their visuals do not overlap. These are functional primitive markers, without decorative assets or audio.

Near the start of each environment, choose one automatic pickup. Taking one removes the other choices for that visit; all can be skipped.

| Power-up | Location | Effect |
| --- | --- | --- |
| Shield | Day, left | Absorbs one solid impact, with brief protection against overlapping contacts. Does not stack; unused shields carry into the next environment. It cannot prevent falls, depleted pursuit, or empty fuel. |
| Magnet | Night, right | Pulls loot across the road in the selected world for six driving seconds. Cannot collect elevated bags from underneath. |
| Boost | Night, centre | Adds 20% forward speed with 20% less steering input for four driving seconds. Police keep their normal pace, creating separation. |

Magnet and boost timers pause with the game and reset between environments. Cyan shield rings, a shrinking purple magnet ring, and orange boost trails show active effects. A carried shield still allows choosing a Night power on the next visit.

## Pace and guidance

Normal cruising is 30.8 m/s in City and 26.4 m/s in Desert, 10% faster than the original pace. The pickup boost reaches 36.96 or 31.68 m/s respectively. Police match normal cruising with the existing small closing pressure. Tutorial speed stays at 18 m/s. Authored relay approaches, flight, landing recovery and steering retain their original curves, including the 42 m/s peak. The bonus ramp and recovery retain 28 m/s in City or 24 m/s in Desert; pickup boosts do not override jump sequences. Standalone development exercises retain their original pace.

City and Desert use the same compact top status strip in Escape and Practice. Fuel is always a percentage (for example **Fuel 100%**), rounding up while any fuel remains and turning red below 25%. Speed is always the current forward speed in **km/h**, rounded to a whole number; boosts and authored relay speeds update that readout. The strip keeps the world on the left, loot in Escape, fuel and speed beside it, and the pursuit mirror at the top right. The mirror preserves its image proportions and uses a red border for danger. There are no duplicate fuel bars, route-progress bars, driving clocks, or City/Desert labels during play. The short standalone Desert exercise shows speed but hides fuel because that exercise has no fuel mechanic. The tutorial keeps its single lesson prompt without driving stats.

The driving HUD uses one contextual prompt in a compact, content-sized dark panel below the header, to the left of the mirror and above the road horizon. At the 1280×720 reference size it uses 26 px type, with a 24 px minimum, one line, and clear padding. The tutorial uses the same upper placement. The car and lower road remain free of guidance text.

Ramp approaches use **Drive up the ramp to jump.** in the existing prompt slot. Driving onto a ramp triggers the jump automatically; Space remains world switching. Only after takeoff do required relay jumps show **Space to land in night/day.** Optional loot ramps require no world switch.

The first crate uses **Drive through to collect**; **Grab loot. Exit to keep it.** is the brief opening fallback once that cue is no longer needed. Urgent road or landing guidance replaces it; pickup acknowledgements and route hints use that same slot. Loot collection updates the total without a separate text popup. The mirror border turns red for pursuit danger. Driving controls appear in Pause and the tutorial rather than a second on-road footer. Exit choices replace the prompt entirely. The header shows the amount without an extra “Your loot” caption. Pause and results hide the driving HUD. The home screen uses rounded, illustrated Day/Night cards for Escape and Practice, with a separate optional lesson link.

## Shared rules

- A/D or arrows steer. Space changes worlds throughout every route. Steering never changes worlds. Escape pauses. In Practice, R retries the current checkpoint.
- Other-world hazard outlines, dashed relay landing previews, and roadside hazard/ramp/landing instructions are removed. Checkpoint and finish gates retain their geometry without word labels. Road markings, ramp pictograms and physical warnings remain; the single HUD prompt carries instructions. Loot and fuel keep their identifying $ and F symbols, and the cash-out/continue gate keeps Exit and Next labels.
- Switching is allowed into occupied positions. It immediately applies the same collision penalty as driving into that obstacle, rather than refusing the switch. Switching onto missing road causes a fall; relay flight is still exempt until landing.
- Solid impacts cost 22.5 m of the initial 45 m police gap; collision protection prevents one contact being charged repeatedly. Fuel is not lost on impact. In Practice, fuel exhaustion, capture and missed road retry the current checkpoint; in Escape they end the run and lose the carried haul.
- Throughout the Desert, Day advances rocks and approaching traffic; Night pauses both at their current positions. Frozen objects remain solid. Finale rocks and walls belong to their assigned world and appear only when that world is selected. Space remains available through the finish. Checkpoints restore clocks, traffic travel and resources.
- The merged-world finale, portal, and split screen have been removed from City, Desert, and the tutorial. The standalone rendering validation scene retains its compositor for development checks only.

## Difficulty pass

- City now has 84 authored hazard entries (36 oncoming cars, up from 16). New Day closures interrupt long safe bypasses; both worlds have additional traffic. Launches and landing recovery areas remain clear. The full city has a verified zero-hit route.
- Full-desert rocks move at 3 m/s laterally. The fields at 1,100-1,356 m and 1,580-1,836 m each contain three four-rock groups at 3.8 m/s. Night walls between groups require returning to Day and freezing a new opening. Walls have clear approach/exit space; these groups have no centre-only guard rails, allowing side routes when the centre is blocked.
- Desert oncoming cars reuse the City's police prefab and travel at 14 m/s when moving. They occupy single side lanes, leaving space to steer around a frozen vehicle. The original short desert practice retains its slower timing.
- The fourth sector now has a Day boost ramp at 1,944-2,004 m, a four-second flight, and a Night landing at 2,172 m. Night has no launch deck; Day has no landing deck until 2,228 m. Six airborne rocks move at 5 m/s in Day and freeze in place in Night. Steering stays available in flight. The player and pursuing police share the boost and flight path. Ramp, launch, flight and recovery are clear of unrelated ground hazards. Green boost arrows distinguish this sequence from ordinary pits. A missed landing or entering the missing launch road retries the 1,920 m checkpoint.

## Implementation / scope

`GetawayRunState` owns loot, progression, shields and settlement. `WorldShiftGetaway` shares escape pickups, exits and UI; `WorldShiftGetawayExtras` owns power-up effects and optional ramp presentation, with `WorldShiftBonusJump` sharing the flight curve between driving and geometry. `WorldShiftFlow` owns scene routing and bank persistence; `WorldShiftMenu` builds the start UI. `WorldShiftTutorial` and `DesertShiftLevel` hold the new authored layouts. `DesertShiftTrial` supports both the original short trial and the full level. Gameplay renders the selected world directly; `IWorldShiftView` and the compositor remain available for the standalone rendering validation scene. All visuals remain simple geometry/materials.

Focused verification covers menu/tutorial routing to either selected level, a collision caused by switching, missing-road failure, roll/freeze behavior, manual switching and consistent Night freezing through the finale, completion UI, and the existing complete city and short desert routes. This is a functional smoke check, not a full playtest of the new desert difficulty.
