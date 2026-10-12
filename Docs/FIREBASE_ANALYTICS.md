# Firebase analytics integration plan

Status: archived proposal, superseded by GOOGLE_FORMS_ANALYTICS.md. Firebase is not implemented; the game uses Google Forms for completed-run analytics.

## Collection

Use UnityWebRequest and the Firebase Realtime Database REST API. GitHub Pages hosts the WebGL build; Firebase stores events independently.

Start a new run ID only when the player starts a fresh attempt. Keep it across Env_City / Env_Snow transitions. Emit one run_started and one run_ended per completed attempt. Use the game manager's accumulated elapsed time and distance, which survive scene changes. Paused game time must not increase survival time.

Recommended event fields:

- event_id: unique ID, reused for retries
- event_name: run_started or run_ended
- run_id: unique attempt ID
- survival_seconds: accumulated active gameplay seconds (0 for start)
- distance_metres: accumulated driving distance (0 for start)
- end_reason: not_applicable, police, fuel, police_and_fuel, or won
- environment: active scene name
- game_version: Application.version; increment for each playtest build
- build_channel: test or production
- client_timestamp_utc: client clock timestamp, not authoritative server time

Recommended database path: /analytics/{build_channel}/{authenticated_uid}/{event_id}.json
Use PUT with a stable event ID so a retried upload does not create another event. Use bounded retries with unscaled delays so uploads continue on the paused game-over screen. Keep the uploader alive across scene loads. Failed uploads must not interrupt gameplay.

Do not report closing the browser as a death: browser shutdown cannot reliably finish an HTTP request. Unmatched run_started events are incomplete/unknown runs and must be excluded from completed-run survival statistics.

## Connection requirements

Provide the Realtime Database root HTTPS URL and confirm authentication configuration. Prefer Firebase Anonymous Authentication with user-scoped database rules and no public reads. Do not embed service-account keys or database secrets in the Unity/WebGL client. Rules must validate the event schema, restrict writes to the authenticated player's path, and prevent deletion or modification of other players' records. No rules have been deployed by this plan.

Agree on player-facing collection notice/choice before enabling collection. Keep Editor/development test events separate from class playtest data.

## Verification before deployment

1. Start and end a run: confirm exactly one matching start/end pair in Firebase.
2. Switch city to snow: same run ID, total duration/distance preserved, no extra start event.
3. Restart: new run ID, time/distance reset.
4. Lose through fuel and police separately: verify end reasons.
5. Simulate failed connection: gameplay remains usable; retries retain event ID.
6. Test the actual GitHub Pages WebGL build and verify receipt in Firebase.

## Analysis

Export JSON from Firebase and pair events by run_id. Deduplicate by event_id. Use run_ended for median survival time, survival-time distribution, death-reason counts, and comparison by game_version/environment. Keep incomplete attempts separate. An anonymous account identifies a browser installation/session, not necessarily one unique person.

References:
- https://firebase.google.com/docs/database/rest/save-data
- https://firebase.google.com/docs/database/rest/auth
