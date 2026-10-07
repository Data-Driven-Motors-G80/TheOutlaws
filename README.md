# The Outlaws

A fast-paced Unity police-chase game where players weave through traffic,
manage limited fuel and ammunition, shoot obstacles, and
survive an endless chase for as long as possible.

The project uses Team 19's **Money Heist** project as its foundation and adapts
selected gameplay ideas from Team 21's **Redline Run** into one cohesive game.

## Gameplay

- Drive through Team 19's infinite-road environment.
- Avoid or shoot obstacles in front of the car. Shots travel up to 20 metres from the firing point and hit the nearest obstacle in their path. Obstacles vary in width, height, and depth; widths range from 0.8 to 4.8 metres and can cross lane markings. Each row reserves a 2.8-metre escape gap, and colliders scale with the visible blocks.
- Carry up to five rounds, starting with two. Each ammo pickup grants one round, placed at independently randomized intervals of 500–800 metres, including the first pickup, and visible 55 m ahead. Ammo spacing is randomized again each run. Pickups alternate between the road edges, with no free opening pickup or immediate replacements for missed ammo. Fuel pickups are unchanged.
- Survive increasing police pressure: a five-second opening ramps to capped difficulty at 75 seconds. The first obstacle blocks the centre lane about 50 metres ahead, prompting an early dodge.
- Obstacle rows start 20% denser, with a 25% chance of two obstacles, and gradually become denser, with an open lane, adjacent-lane escape routes, and speed-aware spacing. Six-second quieter stretches recur every 24 seconds after the opening.
- Police closing speed ramps from 0.25 to 0.65 m/s relative to the car, easing during quieter stretches.
- Tune the opening, ramp duration, maximum obstacle density, and maximum police closing speed under the game manager's **Difficulty** Inspector section.
- Drive through the icy-blue portal at 675 m to enter the snowy mountain theme: the road turns ice blue for the rest of the run. This first pass changes the road palette; mountain scenery is not yet added. Restarting restores the original road.
- Drive as far as possible: there is no finish gate or automatic win at 1,350 m.
- Running out of fuel or letting the police catch you ends the run.

## Controls

| Action | Controls |
| --- | --- |
| Steer | `A` / `D` or Left / Right Arrow |
| Shoot forward | Space |
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
