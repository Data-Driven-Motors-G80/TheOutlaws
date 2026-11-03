# TheOutlaws project guidance

Applies throughout this repository. Read before making changes. The user's latest request takes priority.

## Product decisions
- This is a low-fidelity mechanics playtest. Add visuals only when they explain an action, danger, state or consequence.
- The broader cleanup of existing decorative visuals is still a proposal. Do not carry it out without a request.
- Day provides cash, fuel pickups and power-ups. Nightshift freezes moving obstacles in their current positions; they remain solid. No cash, fuel pickups or active power-ups in Nightshift. Fuel consumption and the police chase continue.
- Keep normal driving speed and fuel consumption equal between modes. Do not introduce a Nightshift speed boost, extra fuel penalty or time limit without a request.
- Jumps have visible gaps: launch in the correct world and switch midair to reach the landing in the other world.
- Forks need room to dodge obstacles without ammo or a shield. Fully separated branches are as wide as the main road. Space out power-up pickups around useful stretches.
- Solid obstacles use simple blocks/walls with clear X marks; every X-marked road obstacle is shootable in both modes when ahead, in range and the player has ammo. Keep pickups visually distinct from hazards.
- Approved visual simplifications: block player/police cars without police lights; plain street posts; cash blocks with `$`; wide supply blocks labeled FUEL, SHIELD, MAGNET, BOOST or AMMO and minimal active-effect indicators; a plain menu with text, flat panels and buttons.
- Tutorial: stable lesson cards, one message at a time; related sentences may share a card. Stop only for an unfinished action, never for a mandatory reading timer. Waiting must be safe from traffic and pursuit.
- Gameplay messages: jump/world-switch instructions, fork/exit choices, one early shooting hint and an occasional Nightshift reminder. Avoid pickup popups, repeated control tips, message stacking and flicker.
- Use a friendly, direct voice: familiar words, short sentences, clear actions and consequences. Keep instructions high contrast.

## Faster working process
- For requests for advice or lists, give recommendations without changing gameplay unless asked.
- Make the smallest coherent change. Use the map below instead of repeatedly exploring the whole project.
- Batch related edits before launching Unity. Run checks once on the finished change; rerun only when a failure or subsequent change justifies it.
- For documentation and wording-only changes, use a diff review. Do not launch Unity or run a full gameplay suite by default.
- For layout changes, inspect one representative rendered view when practical. For mechanics, run the closest relevant tests first.
- Broaden testing only for shared driving/collision/world-state changes, scene-flow changes or unresolved risks. Do not routinely rerun every suite for each small UI edit.
- Reuse the current Unity session when supported. Do not close the user's Editor or launch a second Unity instance against the same project.
- If an isolated test copy is needed, reuse an existing one and sync only changed files. Never assume its source or results are current. Avoid repeatedly copying Library or creating fresh projects for small changes.
- The temporary test copy used in this session is `/tmp/outlaws-balance-check`. Check it exists and matches the relevant source before using it.
- Unity version comes from `ProjectSettings/ProjectVersion.txt` (currently `6000.3.23f1`). Use targeted `-testFilter` values and save results/logs under `/tmp`.
- `--burst-disable-compilation` has avoided a native Burst startup crash in the temporary test runner. This is a test-run flag, not a project-setting change.
- Report exactly what was checked and what remains unverified. Keep progress updates and final responses brief.
- Preserve unrelated working-tree changes, local Unity account links, editor-tool settings and material serialization noise. Do not stage them incidentally.
- PR descriptions should be casual bullet lists about mechanics and player-visible behavior, with brief validation; avoid implementation detail dumps.

## Where to look
- `Assets/Scripts/WorldShift/WorldShiftCity.cs`, `DesertShiftTrial.cs`: simulation, controls, collisions, fuel and world switching.
- `WorldShiftGetaway.cs` in that directory: escape loop, cash and HUD rendering. `WorldShiftDrivingMessages.cs`: gameplay prompt priority, shooting hint pacing and Nightshift reminders.
- `WorldShiftTutorial.cs`, `WorldShiftHud.cs`: tutorial lessons and presentation. `WorldShiftModeGuide.cs`: shared mode names/copy.
- `WorldShiftRoute.cs`, `WorldShiftForkRoad.cs`, `WorldShiftRelay.cs`, `WorldShiftRelayVisuals.cs`: roads, choices, gaps and landings.
- `WorldShiftCombat.cs`: shared obstacle active-state check for targeting, collision, steering and rendering. `WorldShiftGetawayExtras.cs`: shield, magnet, boost and optional bonus ramp. `WorldShiftObstacleVisual.cs`: consistent X blocks. `WorldShiftBlockVisuals.cs`: player and police blocks. `WorldShiftPickupVisual.cs`: shared labeled supply blocks.
- `WorldShiftPresentation.cs`, `DesertShiftTrialView.cs`: environment and hazard visuals.
- `Assets/Tests/Editor/WorldShiftCampaignTests.cs`: tutorial and scene flow. `WorldShiftCombatRoutesTests.cs`: modes, routes, combat and resources. `WorldShiftGetawayTests.cs`: escape and HUD. `WorldShiftRelayTests.cs`: jumps.
- `ProjectSettings/EditorBuildSettings.asset`: scene list; `WorldShift_Menu` is first, followed by City, Desert and the tutorial.
