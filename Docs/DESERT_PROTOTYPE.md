# Desert prototype — first playable slice

Branch: `desert-environment`. Based on main `a9dd593`.

Open `Assets/Scenes/Env_Desert.unity`, press Play, turn the tutorial off with T on the title screen, then press Enter. Normal portals now choose among the other three environments. F9 in the Editor/development build cycles City → Snow → Ocean Coast → Desert → City during a run.

The desert uses a sunset palette, a sand-coloured surround, flattened sphere dunes, and the existing curved road and primitive vehicles. Concrete wall renderers are hidden only on desert road instances; the existing boundary colliders remain. Rock fields now bring a temporary sandstorm on top of the distant haze. No external art is required.

## Crossing rocks

`DesertCrossingController` on The Outlaws Game Systems alternates clear-weather groups of **1–3 rocks** with sandstorm regions of **10–12 rocks**, choosing a fresh random count for each encounter. The first small group is requested after 4 active seconds. A storm region is requested 6 seconds after the small group resolves; the next small group is requested 3.5 seconds after that region and its fog fade-out resolve. Normal obstacle rows continue between encounters. Pickup/portal reservations and existing obstacles may delay a request. Pauses and the tutorial do not advance the encounter. These are spaced crossings, not simultaneous walls of 10–12 rocks.

A dashed yellow line and roadside ROCK arrows announce each crossing. Orange spheres wait off-road, then roll across in alternating directions. Crossings are staggered along the road, at least 28 m apart, with spacing increasing with speed (including nitro). Several rocks can be rolling simultaneously without forming a solid wall. Rocks follow the sampled road's local axes, including bends. Default radius is 1.05 m, warning countdown is 1.5 seconds, and full crossing duration is 4 seconds. Markers are visible before the countdown begins. Movement is fixed once each countdown starts. Road streaming creates future rocks ahead; a late placement inside the reaction window is skipped.

Dodge through the open part of the road, or spend one stored gun shot to destroy the rock. Impact uses the existing orange penalty (15 m of police separation), does not remove fuel, and respects the shield. A hit rock is retired so it cannot charge repeated impacts. Normal obstacle rows continue between crossings with slightly increased spacing.

The controller reserves the shared obstacle-free gap before spawning, waits for existing obstacles to clear it, and releases it after the whole field and fog fade-out, or on scene teardown. Mystery rows and portals cannot claim that gap simultaneously. If a reservation cannot produce a usable road section within 10 active seconds, it is released and retried later.

## Sandstorm

`DesertSandstorm` only accompanies the long regions, not the ordinary 1–3 rock groups. It blends the saved fog toward a sandy colour over 2 seconds as the player approaches the field. Near road and player remain clear; the far road disappears in fog. At full strength, fog starts about 5 m ahead of the player (previously 10 m). Its end distance uses the greater of 50 m or 2 seconds of travel (previously 65 m / 2.5 seconds), plus the chase camera's offset. Gentle gusts modulate this without the large clear patches of the previous version. It fades out over 1.5 seconds after the field. No additional speed, fuel, steering or collision penalty is applied by weather.

Pause freezes weather. Tutorial, game over and leaving the desert clear it; scene teardown never overwrites the next environment's fog. An active Sunny Day carried from snow temporarily clears the sandstorm too. Adjust rock count/spacing/interval and fog fade/visibility independently on The Outlaws Game Systems.

## Integration and checks

The desert is registered in Build Settings and all four scenes' portal lists. Scene switching uses the existing run snapshot. `desert_s` records desert time separately from city time; collisions/shots use the existing gameplay events.

Run **The Outlaws > Validate Environment Setup** for saved scene wiring and Edit Mode **DesertIntegrationTests** for running-game checks (1/2/3-rock clear-weather groups, 10–12-rock storm regions, movement, pause, reservation release, shooting, collision cost, no fuel damage, and backpack/pursuit round trips).

`DesertSceneSetup` is an explicit initial builder. It refuses to replace an already configured desert. Tune the saved scene's controller settings for normal iteration.

Special collectibles, their challenge route, and additional desert-specific rewards are subsequent work.
