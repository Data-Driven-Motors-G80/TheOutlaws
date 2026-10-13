# Wee Woo Run analytics

Replaces the Google Forms setup (`GOOGLE_FORMS_ANALYTICS.md`, now superseded). The game sends rows to a
Google Apps Script web app, which writes them into four tabs of a Google Sheet and builds a dashboard tab.

## One-time setup (about 3 minutes)

1. Open the Google Sheet you want the data in (the old responses sheet is fine).
2. **Extensions → Apps Script**. Delete what's there, paste `Docs/analytics/WeeWooAnalytics.gs`, save.
3. **Deploy → New deployment → Web app**. Execute as: **Me**. Who has access: **Anyone**. Authorize when asked.
4. Copy the URL ending in `/exec` into `Assets/Resources/WeeWooAnalytics.json` → `"endpointUrl"`.
5. Rebuild the WebGL build. Opening the `/exec` URL in a browser should show `{"ok":true,...}`.
6. In the Sheet, reload and use **Wee Woo Analytics → Refresh dashboard** whenever you want updated numbers.

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

After pasting a new version of `WeeWooAnalytics.gs`, just save: `doPost` is unchanged, so no redeploy is needed for the
dashboard. (If `doPost` ever changes: Deploy → Manage deployments → Edit → New version.)

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
- Apps Script can't deduplicate. In rare cases a beacon and a normal upload can overlap; deduplicate `runs` by `run_id`
  (keep the row whose `end_reason` is not `quit_tab_closed`).
