# World Shift playable flow

Open `Assets/Scenes/WorldShift_Menu.unity` and press Play, or use **The Outlaws > World Shift > Open Game Menu**. This scene is also first in Build Settings. Existing environment scenes remain available.

The menu has three entries:

- **City:** the existing 3,360 m route, three airborne relays and final Day/Night intersection; roughly 114 seconds without retries.
- **Desert:** 2,880 m, about 115 seconds without retries. Cruise at 24 m/s, boost at 42 m/s. 58 rocks (including six airborne rocks), 14 freezeable oncoming police cars, alternating world walls, six checkpoints, shared fuel and the existing police/mirror. The final 480 m uses the same full-frame Day/Night compositor as City. No replacement half-road is generated.
- **Tutorial:** an independent 720 m city course at 18 m/s (about 40 seconds). Steering, switching, returning to Day, half-road obstacles, then steering across a short intersection. No fuel drain. Retries restart the current lesson.

The **Play tutorial before this level** switch sends the player through the tutorial and then to the selected City or Desert level. Tutorial can also be selected independently; its Continue button leads to City. City completion offers Next Level (Desert), Level Select and Replay. Desert completion offers Replay and Level Select. Pause menus also return to Level Select.

## Shared rules

- A/D or arrows steer. Space changes worlds outside intersections. Escape pauses. R retries the current checkpoint.
- All other-world hazard outlines and dashed relay landing previews are removed. Road lane markings and physical hazard signs remain.
- Switching is allowed into occupied positions. It immediately applies the same collision penalty as driving into that obstacle, rather than refusing the switch. Switching onto missing road causes a fall; relay flight is still exempt until landing.
- Solid impacts cost 22.5 m of the initial 45 m police gap; collision protection prevents one contact being charged repeatedly. Fuel is not lost on impact. Fuel exhaustion, capture and missed road retry the current checkpoint.
- Before the Desert rift, Day advances rocks and approaching traffic; Night pauses both at their current positions. Frozen objects remain solid. Each finale rock belongs to exactly one world and has its own row: Day rocks move entirely within the left half; Night rocks freeze entirely within the right half. Their full bodies clear the seam by at least 0.35 m, preventing duplicate snapshots or dangerous clipped fragments. Traffic stays in its assigned side lane. Space is inactive there. Checkpoints restore both clocks, traffic travel and resources.

## Difficulty pass

- City now has 84 authored hazard entries (36 oncoming cars, up from 16). New Day closures interrupt long safe bypasses; both worlds have additional traffic. Launches and landing recovery areas remain clear. The full city has a verified zero-hit route.
- Full-desert rocks move at 3 m/s laterally. The fields at 1,100-1,356 m and 1,580-1,836 m each contain three four-rock groups at 3.8 m/s. Night walls between groups require returning to Day and freezing a new opening. Walls have clear approach/exit space; these groups have no centre-only guard rails, allowing side routes when the centre is blocked.
- Desert oncoming cars reuse the City's police prefab and travel at 14 m/s when moving. They occupy single side lanes, leaving space to steer around a frozen vehicle. The original short desert practice retains its slower timing.
- The fourth sector now has a Day boost ramp at 1,944-2,004 m, a four-second flight, and a Night landing at 2,172 m. Night has no launch deck; Day has no landing deck until 2,228 m. Six airborne rocks move at 5 m/s in Day and freeze in place in Night. Steering stays available in flight. The player and pursuing police share the boost and flight path. Ramp, launch, flight and recovery are clear of unrelated ground hazards. Green boost arrows distinguish this sequence from ordinary pits. A missed landing or entering the missing launch road retries the 1,920 m checkpoint.

## Implementation / scope

`WorldShiftFlow` owns scene routing; `WorldShiftMenu` builds the start UI. `WorldShiftTutorial` and `DesertShiftLevel` hold the new authored layouts. `DesertShiftTrial` supports both the original short trial and the full level. `IWorldShiftView` lets City and Desert reuse the compositor and rift presentation. All visuals remain simple geometry/materials.

Focused verification covers menu/tutorial routing to either selected level, a collision caused by switching, missing-road failure, roll/freeze behavior, two rendered views at the desert rift, completion UI, and the existing complete city and short desert routes. This is a functional smoke check, not a full playtest of the new desert difficulty.
