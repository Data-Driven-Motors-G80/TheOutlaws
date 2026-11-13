# WorldShift — The Outlaws

A low-fidelity Unity driving playtest built around switching between Day and Night.

## Run locally

1. Install Unity **6000.3.23f1** through Unity Hub and add this repository as a project.
2. Open `Assets/Scenes/WorldShift_Menu.unity`, or choose **The Outlaws > Levels > Play From Home Screen**.
3. Enter Play mode and pick a level.

## How it plays

- The home screen lists the levels: **0 Training, 1 City, 2 Desert, 3 Ocean, 4 Snow, 5 Mountain**. Beat a level to unlock the next.
- Level 0 introduces each obstacle through a short road encounter: warm white permanent blocks, teal Day-only blocks, pale violet Night-only blocks, red oncoming blocks and orange sideways movers. Teal blocks disappear in Nightshift regardless of their width; permanent blocks stay solid in both worlds. The other world's obstacles show as dashed outlines: you can drive through dashes, but switching while inside one is a crash.
- Shared movers exist in both worlds: sideways movers are orange with black markings, and oncoming blocks are red with white X markings. It moves in Day and freezes in Night.
- One hit and the police catch you; you restart at the last checkpoint.
- Every section ends with a big ramp: launch in one world, switch in the air, land in the other. After a successful landing, the recovery deck exists in both worlds and Space responds immediately. Checkpoint positions are unchanged.

**Team testing:** press **Shift+U** on the home screen (or use **The Outlaws > Levels > Unlock All Levels (testing)** in the Editor) to open every level. An open-lock icon in the corner shows it is on. **Reset Level Progress** clears saved progress.

## Controls

| Action | Input |
| --- | --- |
| Steer | A/D or left/right arrow keys |
| Switch Day/Night | Space |
| Pause/resume | Esc or the on-screen pause button |

## Adding or tuning a level

All layouts live in `Assets/Scripts/WorldShift/WorldShiftLevelCourse.cs`. Each level is a chain like
`.Day(50f, Middle).Night(100f, All).Slider(150f, 1f).Ramp(250f, D)`: offsets are metres from the section start, and `Ramp` ends the section. Every level shares the `City_WorldShift` scene, so you do not need a new scene. `WorldShiftLevelTests` checks that ramps stay clear and that walls leave room to switch.

The older endless escape (cash, garage, fuel, shooting, forks) is still in the code but is no longer reachable from the menu.

## Development documentation

- [Documentation index](Docs/README.md)
- [Gameplay and progression](Docs/WORLD_SHIFT_CAMPAIGN.md)
- [Architecture and code map](Docs/ARCHITECTURE.md)
- [Road geometry and tuning](Docs/CURVED_ROADS.md)
- [Current game scenes](Docs/SCENES.md)
- [Working conventions](AGENTS.md)

For missing scenes, use **The Outlaws > Levels > Create Missing Game Scenes**. It preserves existing scenes and registers only the menu and shared gameplay scene.

Obsolete campaign, environment and validation scenes have been removed. Their scene-dependent tests and editor tools are preserved outside Unity in [the retired-scene archive](Docs/Archive/RetiredScenes/README.md). Shared driving code, prefabs, materials and mechanic implementations remain available.

The project builds on Team 19's Money Heist and Team 21's Redline Run; [integration history](Docs/INTEGRATION.md) and [contribution records](Docs/TeamContribution.md) preserve that provenance. See also [legacy references](Docs/Legacy/README.md) and [historical notes](Docs/Archive/README.md).
