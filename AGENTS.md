# TheOutlaws project guidance

Applies throughout this repository. Read before making changes. The user's latest request takes priority.

## Product decisions — level play (current)
- The home screen is a level select: 0 Training, 1 City, 2 Desert, 3 Ocean, 4 Snow, 5 Mountain. Each level unlocks after beating the previous one (lock icon until then). Progress is saved in PlayerPrefs.
- Team testing: Shift+U on the home screen, or **The Outlaws > Levels > Unlock All Levels (testing)** in the Editor, opens every level without changing saved progress. **Reset Level Progress** clears it.
- Every level runs in `City_WorldShift`; `WorldShiftLevelCourse` decides the environment and layout. New levels need course data, not a new scene.
- Core mechanic: still obstacles belong to Day or Night. The other world's still obstacles always show as dashed outlines (Day = yellow, Night = black). Dashes are passable; switching while inside one is a crash. Oncoming traffic belongs to one world and also shows as dashes.
- Anything that moves sideways (sliders, desert rocks) or rolls toward you (rolling rocks) exists in both worlds, keeps one orange colour, moves in Day and freezes in Night. No vertical movers yet.
- After a successful level landing, the short recovery deck joins both worlds and Space switches immediately. The landing stays world-specific until touchdown succeeds; a retry before that ramp resets its recovery deck.
- One hit = crash. The police car (otherwise a fixed distance behind) pulls up to you, then you restart at the last checkpoint. No lives, pursuit gap or police meter.
- Levels have 5 sections, except Desert (Level 2), which has 6 including a dedicated sandstorm rock field. Every section ends with a big ramp: launch in one world, switch midair, land in the other. Landings are normally checkpoints; Desert instead has 7 respawn positions including the start, concentrated before difficult ramps and the storm. Training uses plain gates for its first four steps.
- Desert launches in Day, Night, Day, Night, Day, Day. Night flights have an orange full-width sliding block in the later half, labeled DAY TO MOVE. Blocks shuttle out and back repeatedly in Day and freeze in Night; six ground blocks use the same rule, including two in the storm, and only start moving within 40 m. The final Day jump has 10 fast spheres sweeping all three lanes in two waves with widely staggered, shuffled phases and a steering window; depth spacing prevents overlap. The storm has 19 ground rocks. Desert air steering uses .65 control. All movers stay orange and solid in both worlds. Dust fades before ramps and retains at least 70 m of road visibility.
- Desert scenery reuses the old low ellipsoid dunes with no colliders, clear of all road bends; retain the road-following sand/seam fix. Every Desert moving block, including the small sliders, says DAY TO MOVE. STORM is painted on the road before the dust field, which also has three alternating Night-only half-road barriers.
- Desert ordinary driving uses alternating 80 m canyon bends (about 28–32 degrees), with a gentler S-bend in the storm. Jump approaches remain straight from ramp minus 80 m through recovery end plus 45 m; road-piece boundaries align there. Both worlds share the road geometry.
- Removed from play: cash, garage/upgrades, shooting/ammo, road splitting (forks), cash-out exits, the optional bonus ramp. Disabled (code kept for later levels): fuel, power-ups, the rift.
- Use as little text as possible. Training teaches with green highway signs (key caps + arrows/pictograms). Instructional green highway signs are exclusive to Training; do not place them in regular levels. Level checkpoints have green road stripes, a labeled gate and a brief CHECKPOINT confirmation above the car's sight line. The rest of the HUD uses sun/moon, progress and pause icons.
- Art and assets are now allowed. Ocean has a stylized coastal art pass in `WorldShiftOceanPresentation`, finish/landmark/shadow details in `WorldShiftOceanPolish`, and its compact HUD in `WorldShiftOceanHudPolish`. Keep prominent world-coloured hazard panels, world-switch silhouettes and road visibility clear.
- All current levels, including Training, use the same maroon player coupe (`WorldShiftPlayerCarArt`) and marked police cruiser (`WorldShiftPoliceCarArt`). Ocean adds lighting/contact shadows without replacing either model.
- Pursuit cars use `WorldShiftPoliceCarArt`: black-and-white panels, POLICE livery and a steady red/blue roof light bar. Ocean must retain this shared cruiser rather than replace it with its player coupe.
- Ocean checkpoints use `WorldShiftOceanCheckpoints`: four coastal gantries at actual saves, green stripes and reflective accents, with one-time light/chime confirmation. Keep supports outside the road and the chequered finish separate.
- Ocean (Level 3) has 11 required diamonds in both worlds; a miss retries the section. Completed sections retain their items. Snow and Mountain remain first-pass courses.
- Analytics: `WorldShiftLevelAnalytics` writes one `ws_level_attempts` row per attempt and `wl_*` events (see Docs/ANALYTICS.md, "Levels").

## Legacy endless escape (not reachable from the menu)
The rules below describe the older endless five-environment escape. Shared runtime code is retained, but scene-dependent tests and editor tools are archived under `Docs/Archive/RetiredScenes`; do not extend it.

- Escape advances automatically in this order: City → Desert → Ocean → Snow → Mountain → City. Ocean, Snow and Mountain use low-fidelity authored profiles on the shared finite driving controller.
- On ordinary transitions, the optional bonus ramp starts 60 m before the environment boundary and lands at least 24 m before the finish. Keep its approach flat/straight and free of Snow coasting. Never transition mid-ramp or in flight.
- Ordinary environment transitions use a 60 m tail (about 2–3 seconds); cash-out legs keep a 440 m decision approach.
- Cash-out side exits appear every two environments. Staying on the main road continues automatically; only an explicit left side exit ends and banks a run. No cash-out exit at ordinary environment boundaries.
- Cash, ammo and unused shields carry across environments. Later laps increase obstacle motion timing by 15% per lap, capped at 45%; pursuit pressure and loot multipliers also rise. Keep safe steering bypasses.
- Between-run actions use “Upgrade car” and “Start next run.” The Garage is never an interruption between environments.
- Home menu: one vertical stack (Start run, Tutorial, Upgrade car) with saved cash under Upgrade car. Keep controls in Pause/tutorial; use A/D key labels instead of unsupported arrow glyphs.
- This is a low-fidelity mechanics playtest. Add visuals only when they explain an action, danger, state or consequence.
- The broader cleanup of existing decorative visuals is still a proposal. Do not carry it out without a request.
- Day provides cash, fuel pickups and power-ups. Full-width Day-only switch walls (the same X marks as other obstacles, no instruction text) disappear in Nightshift and remain shootable in Day. All other shared moving obstacles freeze in their current positions and remain solid. No cash, fuel, ammo pickups or active power-ups in Nightshift; carried ammo still works. Fuel consumption and the police chase continue.
- Roads have environment-specific geometry: City chicanes, rolling Desert canyon bends, tighter coastal bridge bends, firmer Snow descent bends and sharp Mountain switchbacks/descents with approaching rock blocks. Forks and relay approaches keep clear sight lines. Snow steering grip is 0.85 in both modes, with 0.6 release grip and 1.25 countersteer grip on ordinary stretches. Disable extra coasting near forks, ramps and landing recovery; Night does not restore grip.
- City is the approved pacing reference. Later courses connect moving hazards to authored Day cash windows; Desert keeps its dense hazard count, Snow retains wider slalom gaps, and all jump/fork recovery and pickup spacing rules still apply. PacingRewards in WorldShiftEscapeProfile owns the extra cash windows.
- Each environment has three Day-only switch walls, clear of forks and relay recovery. The extra wall has a Day-only cash window 50–80 m later to encourage returning to Day.
- Keep normal driving speed and fuel consumption equal between modes. Do not introduce a Nightshift speed boost, extra fuel penalty or time limit without a request.
- Escape City targets 55–65 seconds including the exit: 1,440 m main route, an early fork at 400 m, sliding barriers followed by Day supplies, short street chicanes and one main relay. City pacing links an opening mover at 125 m, a rejoin mover at 710 m, Day cash at 760/930/1300 m, and alternating two-lane blocks at 1260/1340 m; keep landing recovery clear. Practice keeps the longer original layout.
- After a successful relay landing, ignore return switches until the road rejoins; explain the temporary restriction and confirm when switching is safe.
- Jumps have visible gaps: launch in the correct world and switch midair to reach the landing in the other world.
- Forks need room to dodge obstacles without ammo or a shield. Fully separated branches are as wide as the main road. Space out power-up pickups around useful stretches.
- Nightshift obstacle visuals: pale icy-blue blocks with dark navy X marks; Day uses muted red/brown with white X marks. Keep identical opaque shapes, with immediate palette changes and no flashing or frost particles.
- Solid obstacles use simple blocks/walls with clear X marks; every X-marked road obstacle is shootable in both modes when ahead, in range and the player has ammo. Keep pickups visually distinct from hazards.
- Approved visual simplifications: block player/police cars without police lights; plain street posts; cash blocks with `$`; wide supply blocks labeled FUEL, SHIELD, MAGNET, BOOST or AMMO and minimal active-effect indicators; a plain menu with text, flat panels and buttons.
- Tutorial matches gameplay: no cash/fuel/power-up pickups or supply outlines in Nightshift. Frame prompts around the escape and Day/Nightshift choices; frozen traffic is one consequence. Use the shared Day-only walls and relay landing protection. Early correct actions count.
- Tutorial shooting cue shows F / CLICK: both keyboard F and left-click/trackpad click fire.
- Tutorial: stable lesson cards, one message at a time; related sentences may share a card. Keep retry instructions and related jump/gap messages stable; do not flash generic acknowledgements between them. Stop only for an unfinished action, never for a mandatory reading timer. Waiting must be safe from traffic and pursuit.
- Keep same-lane supply pickups at least 20 m from other pickups. Fork supply order is ammo (+70 m), boost (+130 m), then cash (+190/+235 m); Mountain central ammo precedes fuel by 30 m.
- Reward forks use plain overhead route banners (AMMO + BOOST / CASH, no Day-only subtitle), visible in both modes. Keep the HUD quiet for this choice; sign supports stay outside the carriageways and have no collision.
- Gameplay messages: jump/world-switch instructions, cash-out choices, one early shooting hint and an occasional Nightshift reminder. Avoid pickup popups, repeated control tips, message stacking and flicker.
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
- Unity version comes from `ProjectSettings/ProjectVersion.txt` (currently `6000.3.22f1`). Use targeted `-testFilter` values and save results/logs under `/tmp`.
- `--burst-disable-compilation` has avoided a native Burst startup crash in the temporary test runner. This is a test-run flag, not a project-setting change.
- Report exactly what was checked and what remains unverified. Keep progress updates and final responses brief.
- Preserve unrelated working-tree changes, local Unity account links, editor-tool settings and material serialization noise. Do not stage them incidentally.
- PR descriptions should be casual bullet lists about mechanics and player-visible behavior, with brief validation; avoid implementation detail dumps.

## Where to look
- `README.md` and `Docs/README.md` are the current entry points. `Docs/Archive` is historical, and `Docs/Legacy` describes the older World game. Do not treat historical mechanics as current requirements.
- `Assets/Editor/WorldShiftCampaignSetup.cs` creates only the menu and shared gameplay scene without overwriting existing scenes. Keep both scene paths and `.meta` GUIDs stable. Retired scene tools and tests live outside Assets in `Docs/Archive/RetiredScenes`.
- `Assets/Scripts/WorldShift/WorldShiftCity.cs`, `DesertShiftTrial.cs`: simulation, controls, collisions, fuel and world switching.
- `WorldShiftFlow.cs`: five-environment cycle and cash-out cadence. `WorldShiftEscapeProfile.cs`: Ocean/Snow/Mountain courses, fuel, relays and colours, plus City difficulty scaling. `WorldShiftGetaway.cs` in that directory: escape loop, cash and HUD rendering. `WorldShiftDrivingMessages.cs`: gameplay prompt priority, shooting hint pacing and Nightshift reminders.
- `WorldShiftTutorial.cs`, `WorldShiftHud.cs`: tutorial lessons and presentation. `WorldShiftModeGuide.cs`: shared mode names/copy.
- `WorldShiftRoute.cs`, `WorldShiftForkRoad.cs`, `WorldShiftRelay.cs`, `WorldShiftRelayVisuals.cs`: roads, choices, gaps and landings.
- `WorldShiftCombat.cs`: shared obstacle active-state check for targeting, collision, steering and rendering. `WorldShiftGetawayExtras.cs`: shield, magnet, boost and optional bonus ramp. `WorldShiftObstacleVisual.cs`: consistent X blocks. `WorldShiftBlockVisuals.cs`: player and police blocks. `WorldShiftPickupVisual.cs`: shared labeled supply blocks.
- `WorldShiftPresentation.cs`, `DesertShiftTrialView.cs`: environment and hazard visuals.
- `Assets/Tests/Editor/WorldShiftSceneSetupTests.cs`: the two current scenes and build setup. `WorldShiftLevelTests.cs`: menu flow for every current level, Training, Ocean collection and shared level rules. `DesertLevelCourseTests.cs`: current Level 2. `WorldShiftRelayTests.cs`: shared jumps.
- Level play: `WorldShiftLevels.cs` (unlocks, flow), `WorldShiftLevelCourse.cs` (all level layouts), `WorldShiftLevelHud.cs`, `WorldShiftLevelProps.cs` (dashes, highway signs), `WorldShiftIcons.cs`, `WorldShiftLevelAnalytics.cs`. Level-mode branches in `WorldShiftCity` are guarded by `LevelMode`. Tests: `Assets/Tests/Editor/WorldShiftLevelTests.cs`.
- `ProjectSettings/EditorBuildSettings.asset`: only `WorldShift_Menu`, followed by `City_WorldShift`. Training and all five levels share the latter scene.
