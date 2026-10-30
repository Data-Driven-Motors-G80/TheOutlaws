# World Shift playable flow

The menu now starts the **Endless Getaway** mode: collect loot, steer left to bank at an exit, or right to continue into the next environment. City and Desert loop with increasing rewards and pursuit pressure. Capture, wrecks, or empty fuel lose the carried haul. Practice retains the original checkpoint flow described below. See [the getaway design](WORLD_SHIFT_DIRECTION_PROPOSAL.md).

Open `Assets/Scenes/WorldShift_Menu.unity` and press Play, or use **The Outlaws > World Shift > Open Game Menu**. This scene is also first in Build Settings. Existing environment scenes remain available.

The home screen separates **Endless escape** from **Practice**. Practice opens City and Desert route buttons; **New here? Learn to play** opens the tutorial. The available routes are:

- **City:** the existing 3,360 m route, three airborne relays and a final stretch of world-specific obstacles; roughly 114 seconds without retries.
- **Desert:** 2,880 m, about 115 seconds without retries. Cruise at 24 m/s, boost at 42 m/s. 58 rocks (including six airborne rocks), 14 freezeable oncoming police cars, alternating world walls, six checkpoints, shared fuel and the existing police/mirror. The final 480 m retains manual Space switching and shows only the selected world.
- **Tutorial:** an independent 720 m city course at 18 m/s (about 40 seconds). Steering, switching, returning to Day, and choosing whether to steer around an obstacle or switch worlds. No fuel drain. Retries restart the current lesson.

The first escape includes the short tutorial. **New here? Learn to play** also opens it independently. Tutorial can also be selected independently; its Continue button leads to City. City practice completion offers **Try the desert** and **Back to menu**. Desert practice completion offers **Try again** and **Back to menu**. Pause screens offer a clear way to resume or leave.

## Shared rules

- A/D or arrows steer. Space changes worlds throughout every route. Steering never changes worlds. Escape pauses. R retries the current checkpoint.
- All other-world hazard outlines and dashed relay landing previews are removed. Road lane markings and physical hazard signs remain.
- Switching is allowed into occupied positions. It immediately applies the same collision penalty as driving into that obstacle, rather than refusing the switch. Switching onto missing road causes a fall; relay flight is still exempt until landing.
- Solid impacts cost 22.5 m of the initial 45 m police gap; collision protection prevents one contact being charged repeatedly. Fuel is not lost on impact. Fuel exhaustion, capture and missed road retry the current checkpoint.
- Throughout the Desert, Day advances rocks and approaching traffic; Night pauses both at their current positions. Frozen objects remain solid. Finale rocks and walls belong to their assigned world and appear only when that world is selected. Space remains available through the finish. Checkpoints restore clocks, traffic travel and resources.
- The merged-world finale, portal, and split screen have been removed from City, Desert, and the tutorial. The standalone rendering validation scene retains its compositor for development checks only.

## Difficulty pass

- City now has 84 authored hazard entries (36 oncoming cars, up from 16). New Day closures interrupt long safe bypasses; both worlds have additional traffic. Launches and landing recovery areas remain clear. The full city has a verified zero-hit route.
- Full-desert rocks move at 3 m/s laterally. The fields at 1,100-1,356 m and 1,580-1,836 m each contain three four-rock groups at 3.8 m/s. Night walls between groups require returning to Day and freezing a new opening. Walls have clear approach/exit space; these groups have no centre-only guard rails, allowing side routes when the centre is blocked.
- Desert oncoming cars reuse the City's police prefab and travel at 14 m/s when moving. They occupy single side lanes, leaving space to steer around a frozen vehicle. The original short desert practice retains its slower timing.
- The fourth sector now has a Day boost ramp at 1,944-2,004 m, a four-second flight, and a Night landing at 2,172 m. Night has no launch deck; Day has no landing deck until 2,228 m. Six airborne rocks move at 5 m/s in Day and freeze in place in Night. Steering stays available in flight. The player and pursuing police share the boost and flight path. Ramp, launch, flight and recovery are clear of unrelated ground hazards. Green boost arrows distinguish this sequence from ordinary pits. A missed landing or entering the missing launch road retries the 1,920 m checkpoint.

## Implementation / scope

`WorldShiftFlow` owns scene routing; `WorldShiftMenu` builds the start UI. `WorldShiftTutorial` and `DesertShiftLevel` hold the new authored layouts. `DesertShiftTrial` supports both the original short trial and the full level. Gameplay renders the selected world directly; `IWorldShiftView` and the compositor remain available for the standalone rendering validation scene. All visuals remain simple geometry/materials.

Focused verification covers menu/tutorial routing to either selected level, a collision caused by switching, missing-road failure, roll/freeze behavior, manual switching and consistent Night freezing through the finale, completion UI, and the existing complete city and short desert routes. This is a functional smoke check, not a full playtest of the new desert difficulty.
