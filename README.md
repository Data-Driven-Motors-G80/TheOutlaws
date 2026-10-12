# The Outlaws

A fast-paced Unity police-chase game where players weave through traffic,
manage limited fuel and ammunition, shoot obstacles, and
survive an endless chase for as long as possible.

The project uses Team 19's **Money Heist** project as its foundation and adapts
selected gameplay ideas from Team 21's **Redline Run** into one cohesive game.

## Gameplay

- Drive through Team 19's infinite-road environment.
- Avoid or shoot traffic-light obstacles in front of the car. Red obstacles require three hits, yellow require two, and green require one. Collisions cost 18 m, 11.25 m, or 5.625 m of police distance respectively; hitting a red obstacle also removes 20% of maximum fuel. Fully destroying an obstacle that started red drops one ammo pickup just beyond it. Shots travel up to 30 metres and hit the nearest obstacle in their path.
- Carry up to five rounds, starting with two. The opening tutorial places three extra ammo pickups before a full-width green and then yellow teaching barrier. Later ammo pickups grant one round and are visible 55 m ahead. Random spacing is 180–260 m before 300 m, 300–450 m from 300–700 m, and 400–600 m after 700 m. Spacing tiers use total run distance across environment changes.
- From 330 m onward, roadblocks appear every 240–330 m and favor two-lane green/yellow walls, with occasional full-width and red/yellow/green choice walls; no all-red wall is generated. After 2,000 m, roadblocks appear every 160–240 m and 70% are full-width green or yellow walls.
- Start at 15 m/s with a 15-second opening. At 25 seconds and every 25 seconds afterward (25, 50, 75 seconds), speed increases by 1 m/s and obstacle density by 5 percentage points. Speed caps at 30 m/s; density caps at 1.6, with speed-aware escape spacing.
- Switch to a different environment at randomized intervals of 50–120 seconds. Speed, obstacle density, and police pressure continue using total run time, without a transition reset or relief period. Fuel, ammo, distance, and score carry over; opening teaching barriers do not repeat.
- City roads are dark gray and snow roads are ice blue. Press F9 during gameplay in the Editor or a development build to test the transition.
- Random special pickups give reverse steering a 10% chance; nitro, shield, and police distance recovery each have a 30% chance.
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
