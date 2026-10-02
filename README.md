# The Outlaws

A fast-paced Unity police-chase game where players weave through traffic,
manage limited fuel and ammunition, fight back against pursuing police, and
reach the extraction point before they are caught.

The project uses Team 19's **Money Heist** project as its foundation and adapts
selected gameplay ideas from Team 21's **Redline Run** into one cohesive game.

## Gameplay

- Drive through Team 19's infinite-road environment.
- Avoid or shoot obstacles in front of the car.
- Fire backward to push the police away temporarily.
- Collect limited ammunition and fuel pickups.
- Survive increasing police pressure.
- Reach the extraction gate to win.
- Running out of fuel or letting the police catch you ends the run.

## Controls

| Action | Controls |
| --- | --- |
| Steer | `A` / `D` or Left / Right Arrow |
| Shoot forward | `W` or Up Arrow |
| Shoot backward | `S` or Down Arrow |
| Start | Enter or the Start button |
| Restart | `R` or the Restart button |

## Unity setup

1. Install Unity `6000.3.23f1` through Unity Hub.
2. Add this repository as a Unity project.
3. Open `Assets/Scenes/GetawayChase.unity`.
4. Enter Play mode.

The integration systems install themselves when `GetawayChase` loads. The HUD
keeps Team 19's bars and adds clear `FUEL`, `AMMO`, and `POLICE DISTANCE`
labels. Team 21's unwanted visual-effect system is not included.

## Source projects

- [Team 19 — Money Heist](https://github.com/CSCI-526/Team19-Kangod-Tao-Gong)
- [Team 21 — Redline Run](https://github.com/CSCI-526/Team21-He_Ali)

See `Docs/INTEGRATION.md` for the feature and code mapping.
