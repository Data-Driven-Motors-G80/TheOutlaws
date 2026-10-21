# Wee Woo Run analytics

Replaces the Google Forms setup (`GOOGLE_FORMS_ANALYTICS.md`, now superseded). The game sends rows to a
Google Apps Script web app, which writes them into four tabs of a Google Sheet and builds a dashboard tab.

## One-time setup (about 3 minutes)

1. Open the Google Sheet you want the data in (the old responses sheet is fine).
2. **Extensions → Apps Script**. Delete what's there, paste `Docs/analytics/WeeWooAnalytics.gs`, save.
3. **Deploy → New deployment → Web app**. Execute as: **Me**. Who has access: **Anyone**. Authorize when asked.
4. Copy the URL ending in `/exec` into `Assets/Resources/WeeWooAnalytics.json` → `"endpointUrl"`.
5. Rebuild the WebGL build. Opening the `/exec` URL in a browser should show `{"ok":true,...}`.
6. Run **refreshMetricTabs** once in Apps Script to organize existing data, or finish a game run.
   Seven metric tabs are created and refreshed automatically on subsequent run/tutorial summary uploads.
   The older `dashboard` remains a separate manual report (menu: **Wee Woo Analytics → Refresh dashboard**).

## Updating an existing installation for automatic metric tabs

1. In the existing Google Sheet, open **Extensions → Apps Script**.
2. Replace the receiver code with the updated `Docs/analytics/WeeWooAnalytics.gs` and save.
3. Run **refreshMetricTabs** once and authorize if prompted. This backfills existing records.
4. Choose **Deploy → Manage deployments → Edit → New version → Deploy** for the existing web app.
   Keep its existing `/exec` URL. No Unity rebuild or endpoint change is needed when that URL stays the same.
5. Finish a non-editor game run. Check the new raw row in `runs` and the refreshed timestamp in `Overview`.

The generated tabs are `Overview`, `Survival & Progression`, `Shooting & Ammo`, `Collisions & Police`,
`Fuel`, `Mystery Pickups`, and `Tutorial Summary`. Each has summaries; category tabs also include
relevant per-run/per-attempt columns. Editor/test/ignore and missing-build records remain raw but are
excluded from these reports. Run summary statistics use fuel/police deaths; detail tables include quits
and restarts. Builds are pooled and identified in detail rows. Pickup outcomes measure collection,
not necessarily successful use. Missing or negative sentinel numbers are excluded from averages.
`Shooting & Ammo` includes `ammo_wasted_full` as **Ammo pickups touched while full**, both in its
death-ended-run summary and per-run details. These are rejected collection attempts when ammo is full,
not inferred intentional avoidance. They can overlap `ammo_missed`; do not sum the two counts.
Destroyed obstacles are labelled **1-hit obstacles destroyed (Orange)** and **2-hit obstacles destroyed
(Red)** in this report, using their starting strength. The underlying `destroyed_green` and
`destroyed_yellow` keys remain unchanged for compatibility. Historical `destroyed_red` records are
shown separately as **Legacy 3-hit obstacles destroyed**; they are not the current red two-hit type.
Colour labels describe the current game; compare older versions by strength rather than colour.

Reports deduplicate runs by `run_id`, preferring a normal ending over a tab-close beacon. Tutorial
records are deduplicated by session, timestamp and status, preserving separate attempts. Raw data is
not removed. Existing nonempty tabs with these names are preserved: rename them before running setup
if they were created manually. Generated tabs are script-owned and rewritten on refresh.

Event-only uploads do not rebuild reports. A report-generation error is logged and returned in
`metricTabsError`, while successfully saved raw rows still receive `ok: true`. After fixing the error,
run **refreshMetricTabs** to backfill. This is intended for class playtests; report generation reads
all run/tutorial summaries, so larger datasets may eventually need scheduled rather than per-run refreshes.

Editor play also uploads (`sendFromEditor: true`) with the build tag suffixed `-editor`, and prints each server reply as
`[Analytics] Uploaded: {"ok":true,"written":N}`. The dashboard ignores builds containing `editor`, `test` or `ignore`.
Change `buildTag` for each new playtest build so the versions can be separated.

## How data is delivered

- Events are batched and sent every 12 s, plus immediately at key moments (run end, tutorial step, pause,
  focus loss). On WebGL the uploads use `fetch(keepalive)`.
- **Tab closes are captured**: a `navigator.sendBeacon` fires on `pagehide` with the session summary and,
  if the player was mid-run or mid-tutorial, an "abandoned" row. Quitting is a result, not missing data.
- Identity is anonymous: a random `player_id` per browser (PlayerPrefs), a `session_id` per page load and a
  `run_id` per run. No names, emails or device IDs. Incognito windows count as new players.

## Tabs

| Tab | One row per | Key columns |
| --- | --- | --- |
| `events` | gameplay event | `ev`, `state`, `scene`, `run_t`, `dist`, plus event-specific fields |
| `runs` | real run (tutorial excluded) | ~70 columns: distance, end reason, shooting, pickups, hits by colour, fuel, steering, pauses, missions |
| `tutorials` | completed / abandoned / restarted tutorial | status, quit step, seconds and retries per step |
| `sessions` | page close | session length, runs this session, where they quit (title, tutorial, mid-run, game over) |
| `dashboard` | — | computed summary (menu: Wee Woo Analytics → Refresh dashboard) |

## Data visualization with Python and Matplotlib

Export the Google Sheet as Microsoft Excel (.xlsx). From the repository root, run:

```bash
python3 -m pip install -r Docs/analytics/requirements.txt
python3 Docs/analytics/create_metric_charts.py "path/to/analytics.xlsx" --output "path/to/charts"
```

On Windows, use `python` or `py` instead of `python3` if needed. The script reads the `runs` tab,
excludes editor/test/ignore and missing-build records, deduplicates run IDs, and charts fuel/police-ended
runs. It generates survival, death-cause, shooting-accuracy, and pickup-collection PNGs. Dates, totals,
and histogram limits come from the export. It does not access the live sheet or change the workbook.
Re-export and rerun to include newer data. Dependencies are Python 3 and Matplotlib.

These are actual-data charts for the implemented visualization pipeline. The GDD mockup sketches use
invented values and are separate artifacts; they should not be described as measured playtest results.

## Events

`session_start` (browser, screen size, returning player, prior runs) · `title_shown` · `tutorial_toggled` ·
`start_pressed` (tutorial on/default/toggled, seconds on title) · `tutorial_start` · `tutorial_step_start` /
`tutorial_step_complete` (seconds, retries, shots, dry fires) · `tutorial_retry` (missed_fuel, missed_ammo,
hit_wall, hit_block) · `tutorial_complete` · `run_start` · `checkpoint` every 250 m (gap, fuel, ammo, speed, hits) ·
`collision` (colour, shielded, reversed controls, ammo, **could_shoot**) · `obstacle_destroyed` · `pickup` /
`pickup_missed` (fuel, ammo, mystery + outcome) · `dry_fire_first` · `low_fuel` · `env_switch` ·
`mission_complete` · `pause` / `resume` (source: key or focus_lost) · `restart_pressed` · `run_end`.

## Live endpoint

`Assets/Resources/WeeWooAnalytics.json` points at the deployed web app (`.../AKfycbxIxSZS.../exec`). Verified on 2026-10-13:
GET returns `{"ok":true}`, a labelled POST wrote one row (build `SETUP_TEST_IGNORE`) and an editor run uploaded rows
with build `editor-test-ignore`. Filter out both builds. The real build tag is `weewoo-1.1`.

The automatic metric-tab update changes `doPost`, so saving alone is not enough: update the existing
web-app deployment to a new version using the steps above.

## Run columns added for death and bullet analysis

- `death_cause`: `police_right_after_hit`, `police_after_many_hits`, `police_slow_pressure`, `fuel_missed_tanks`,
  `fuel_red_block_hits`, `fuel_other` (plus `restart`, `restart_from_pause`, `quit_tab_closed`).
- Situation at the end: `last_hit_colour`, `hits_last_10s`, `fuel_pct_at_end`, `ammo_at_end`, `reversed_at_end`,
  `nitro_at_end`, `shield_at_end`.
- How blocks were passed, per colour: `dodged_*`, `hits_*`, `destroyed_*`; shots aimed per colour `shots_on_*`.
- Bullet holding: `avg_ammo_held`, `avg_hold_s` (seconds a bullet is carried before it is fired), `s_ammo_full`,
  `s_ammo_empty`, and `ammo_timeline_10s` / `shots_timeline_10s` (ammo held and shots fired every 10 s).
- `checkpoint` events every 250 m now include `shots_since`, `ammo_picked_since`, `destroyed_since`, `dodged_since`,
  `hits_since`, so bullet use can be plotted against distance.
- Power-ups: `mystery_*` outcome counts, `hits_shielded`, `hits_with_nitro`, `hits_while_reversed`, `s_reversed`.

## Run length and missions

- `survival_s` / `run_minutes` (active driving time) and `wallclock_s` (including pauses) for every run.
- Missions: `missions_active`, `missions_completed`, `missions_completed_count`, `mission_progress_end`
  (e.g. `2:5 / 10|3:done|4:0 / 2`), `missions_done_total`, lifetime totals; `mission_complete` events include the
  player's run number.
- Dashboard: "How long players last" (median/mean/longest, % of runs past 30/60/90/120/180 s) and "Top 10 runs" with the
  furthest players' death cause, hits, shots, accuracy, destroyed/dodged blocks, average ammo and missions.

## Questions the data answers (predicted weak spots)

- **Is the tutorial too long or confusing?** Funnel % per step, seconds and retries per step, which step people quit on.
- **Do players understand shooting?** % runs with zero shots, accuracy, shots that hit nothing (fired out of range),
  dry fires (pressing Space with no ammo → ammo squares unclear), and `could_shoot` on collisions (had the ammo but didn't use it).
- **Do players understand pickups?** Collect vs miss rate for F, A and ? boxes; A boxes grabbed while already full.
- **Is fuel or the police the real killer?** End reasons, fuel deaths that missed a tank, `caught_within_5s_of_hit`.
- **Is reversed steering too punishing?** Hits while controls were swapped, per second swapped.
- **Is difficulty well tuned?** % runs reaching 500/1000/1500/2000/2500 m and snow, first vs later runs (learning curve),
  tutorial-on vs tutorial-off performance.
- **Are missions motivating / too hard?** Players completing each of the 10 missions.
- **Engagement:** runs per player, title-screen bounces, pauses, tab-aways, restarts from pause, where sessions end.

## Notes

- `-1` means "not applicable" (e.g. `accuracy_pct` with no shots, `first_hit_m` with no hits).
- Raw uploads can overlap. Automatic metric reports deduplicate `runs` by `run_id`, preferring the row
  whose `end_reason` is not `quit_tab_closed`; raw rows remain available for inspection.


## Backpack prototype (weewoo-1.3-backpack)

Analyze this build separately from earlier standalone-ammo builds. A mystery row is one encounter; sibling boxes removed after collection are not missed pickups. A gun is reported as mystery outcome `Gun`, not a standalone ammo pickup. Legacy `ammo_spawned`, `ammo_collected`, `ammo_missed`, and `ammo_wasted_full` therefore remain zero in new gameplay.

New run fields: `gun_rewards`, `backpack_replacements`, `discarded_bullets`, `backpack_uses`, `backpack_at_end`. `backpack_received` events record item, replaced item, discarded bullets and previous holding time; `backpack_used` events record item and holding time (each gun shot is a use). Collection does not imply activation. Existing shots/hits and destruction counts remain valid. Gun bullet holding times reset on replacement; a full gun now means three shots. The no-ammo-pickup mission is now a no-shooting mission.
