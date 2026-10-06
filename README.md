# The Outlaws

A fast-paced Unity police-chase game where players weave through traffic,
manage limited fuel and ammunition, fight back against pursuing police, and
survive an endless chase for as long as possible.

The project uses Team 19's **Money Heist** project as its foundation and adapts
selected gameplay ideas from Team 21's **Redline Run** into one cohesive game.

## Gameplay

- Drive through Team 19's infinite-road environment.
- Avoid or shoot obstacles in front of the car.
- Fire backward to push the police away temporarily.
- Collect limited ammunition and fuel pickups.
- Survive increasing police pressure: a forgiving 20-second opening ramps to capped difficulty at 90 seconds.
- Obstacle rows gradually become denser, with an open lane, adjacent-lane escape routes, and speed-aware spacing. Six-second quieter stretches recur every 24 seconds after the opening.
- Police closing speed ramps from 0.1 to 0.65 m/s relative to the car, easing during quieter stretches. Backward shots still repel and slow the police; pickup rates stay unchanged.
- Tune the opening, ramp duration, maximum obstacle density, and maximum police closing speed under the game manager's **Difficulty** Inspector section.
- Drive through the purple portal at 675 m to enter the desert: the road turns sandy gold for the rest of the run. Restarting restores the original road.
- Drive as far as possible: there is no finish gate or automatic win at 1,350 m.
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

The integration systems and HUD layout are saved in `GetawayChase`. The HUD
keeps Team 19's bars and adds clear `FUEL`, `AMMO`, and `POLICE DISTANCE`
labels. Team 21's unwanted visual-effect system is not included.

## Source projects

- [Team 19 — Money Heist](https://github.com/CSCI-526/Team19-Kangod-Tao-Gong)
- [Team 21 — Redline Run](https://github.com/CSCI-526/Team21-He_Ali)

See `Docs/INTEGRATION.md` for the feature and code mapping.
