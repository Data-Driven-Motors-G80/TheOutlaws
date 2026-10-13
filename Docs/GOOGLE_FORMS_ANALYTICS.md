# Google Forms gameplay analytics

> **Superseded** by `ANALYTICS.md` (Apps Script + Sheet). The game no longer submits to the form.

Status: all 20 short-answer questions are created and published, field entry IDs are configured, and uploads are enabled for builds. An anonymous HTTP test submission was verified in Google Forms on 2026-10-11 with every field populated correctly. Unity gameplay-to-server and deployed WebGL receipt verification remain pending. Editor uploads remain disabled.

Responses spreadsheet: https://docs.google.com/spreadsheets/d/1f3t5wuM9ULv2e6tuIEXRyRTaQqa7RzbFAPt0YLI8cF4/edit

The initial test row has run_id SETUP_TEST_20261011_01 and game_version SETUP_TEST_IGNORE. Exclude it from gameplay analysis.

Configuration: Assets/Resources/GoogleFormsAnalytics.json. Supply the formResponse URL and map every metric to its actual entry.NUMBER identifier. Never use question IDs or example IDs. Set enabled only after mapping and verifying access. Editor uploads default off; enable sendFromEditor only for a controlled test.

One row is constructed at game over, before the game pauses. A persistent uploader survives scene reloads. Session IDs are random per app/browser session; run IDs are fresh per attempt. No names, emails, or device identifiers are collected. Session ID does not identify a person across sessions.

Counters persist through scene switches. City/snow times use the same scaled frame time as the survival timer, excluding pauses. Shots fired exclude empty-ammo keypresses; hits count each accepted obstacle hit, even without destruction. Collisions count one contact per obstacle placement, including shield-protected impacts. Ammo counts successfully collected pickups, not rounds. Fuel counts collected tanks, including tanks collected while full. Special effects count accepted pickups, including stacked effects, not each second of effect duration. Reverse is counted when the pickup is accepted, before its countdown completes.

Fuel depletion also forces the pursuit state to game over; therefore an empty fuel meter takes precedence as the death reason. Browser closure produces no completed row. The latest completed report is retained in PlayerPrefs under Outlaws.LastAnalyticsReport and printed in the Editor console. This is not a durable queue of all runs.

No automatic retries are used: a browser CORS error can occur after Google has accepted a response. Check Sheets before retrying and deduplicate by run_id. A successful HTTP response alone is not proof of a stored row. Verify the actual GitHub Pages build before using these metrics for analysis.

## Form fields (Short answer)

| Form question | Configuration metric |
| --- | --- |
| Session ID | session_id |
| Run ID | run_id |
| Survival Seconds | survival_seconds |
| Shots Fired | shots_fired |
| Shots Hit | shots_hit |
| Obstacles Destroyed | obstacles_destroyed |
| Obstacle Collisions | obstacle_collisions |
| Fuel Pickups Collected | fuel_pickups |
| Ammo Pickups Collected | ammo_pickups |
| Shield Pickups | shield_pickups |
| Nitro Pickups | nitro_pickups |
| Police Recovery Pickups | recovery_pickups |
| Reverse Steering Pickups | reverse_pickups |
| Death Reason | death_reason |
| Game Version | game_version |
| Distance Travelled | distance_metres |
| Environment at Death | environment |
| City Time Seconds | city_seconds |
| Snow Time Seconds | snow_seconds |
| Ammo Remaining at Death | ammo_remaining |

Form responder access must permit anonymous responses; disable verified email collection, sign-in restrictions, and the one-response limit. Editor access can remain private. Link the Responses tab to a Google Sheet.

Before release: verify one row for a completed run, no row on environment switch, a new run ID after restart, shots/hits matching manual counts, both death reasons, and city_seconds + snow_seconds matching survival_seconds within rounding. Keep test rows visibly labeled in game_version and exclude them from analysis.
