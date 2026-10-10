# The Outlaws

A fast-paced Unity police-chase game where players weave through traffic,
manage limited fuel and ammunition, shoot obstacles, and
survive an endless chase for as long as possible.

The project uses Team 19's **Money Heist** project as its foundation and adapts
selected gameplay ideas from Team 21's **Redline Run** into one cohesive game.

## Gameplay

- Drive through Team 19's infinite-road environment.
- Avoid or shoot traffic-light obstacles in front of the car. Red obstacles require three hits, yellow require two, and green require one. Collisions cost 18 m, 11.25 m, or 5.625 m of police distance respectively; hitting a red obstacle also removes 20% of maximum fuel. Fully destroying an obstacle that started red drops one ammo pickup just beyond it. Shots travel up to 30 metres and hit the nearest obstacle in their path.
- Carry up to five rounds, starting with two. The opening tutorial places three extra ammo pickups before a full-width green and then yellow teaching barrier. Later ammo pickups grant one round and are visible 55 m ahead. Random spacing is 180–260 m before 300 m, 120–180 m from 300–700 m, and 90–150 m after 700 m.
- From 330 m onward, roadblocks appear every 240–330 m and favor two-lane green/yellow walls, with occasional full-width and red/yellow/green choice walls; no all-red wall is generated. After 2,000 m, roadblocks appear every 160–240 m and 70% are full-width green or yellow walls.
- Survive increasing police pressure: a five-second opening ramps to capped difficulty at 75 seconds. The first two obstacle rows are tutorial roadblocks; normal fair-row generation begins after them.
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
