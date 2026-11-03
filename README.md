# WorldShift — The Outlaws

A low-fidelity Unity driving playtest built around switching between Day and Nightshift.

## Run locally

1. Install Unity **6000.3.23f1** through Unity Hub and add this repository as a project.
2. Open `Assets/Scenes/WorldShift_Menu.unity`, or choose **The Outlaws > World Shift > Open Game Menu**.
3. Enter Play mode. Choose **Start run**, **Tutorial**, or **Upgrade car**.

The playable loop is **City → Desert → Ocean → Snow → Mountain → City**. Environment changes happen automatically. Marked cash-out side exits appear every two environments; staying on the main road continues the run. Banking ends the run and makes its cash available for permanent upgrades.

## Controls

| Action | Input |
| --- | --- |
| Steer | A/D or left/right arrow keys |
| Switch Day/Nightshift | Space |
| Shoot | F or left-click |
| Pause/resume | Esc or the on-screen Pause button |

Controls are also available in Pause. The optional tutorial teaches actions by driving.

Day has cash, fuel and power-ups. Nightshift clears marked Day-only barriers and freezes shared moving obstacles in place; frozen blocks remain solid. Ammo remains available in either mode. Main jumps require switching in the air to reach the other mode's landing.

## Development documentation

- [Documentation index](Docs/README.md)
- [Gameplay and progression](Docs/WORLD_SHIFT_CAMPAIGN.md)
- [Architecture and code map](Docs/ARCHITECTURE.md)
- [Road geometry and tuning](Docs/CURVED_ROADS.md)
- [Playable, validation and legacy scenes](Docs/SCENES.md)
- [Working conventions](AGENTS.md)

For missing scenes, use **The Outlaws > World Shift > Setup > Create Missing Campaign Scenes**. It preserves existing scenes and registers the five environments, menu and tutorial in the build list.

The older `Env_*` World game and prototype scenes are retained for reference and validation; they are not the current playtest entry points. See [legacy references](Docs/Legacy/README.md) and [historical notes](Docs/Archive/README.md). The project builds on Team 19's Money Heist and Team 21's Redline Run; [integration history](Docs/INTEGRATION.md) and [contribution records](Docs/TeamContribution.md) preserve that provenance.
