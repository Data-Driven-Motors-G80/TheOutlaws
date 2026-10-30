# Transform / Drive — team prototype

This branch adds an isolated navigation game. The original game and world-shift scenes remain available. Fuel is the **only pickup** in the new mode. There is no police chase, shooting, mystery reward, extra-life collectible, or world-switch input.

## Play

Use Unity **6000.3.23f1**, open `Assets/Scenes/Stunt_Menu.unity`, and press Play. The editor shortcut is **The Outlaws > Transform Stunt > Open Prototype**. Choose City, Desert, or Coast. Tutorial-first defaults on until training is completed; tutorial can always be replayed or skipped.

| Input | Action |
|---|---|
| A/D or Left/Right | Steer across the track |
| Q | Shrink one step: Large → Medium → Small |
| E | Grow one step: Small → Medium → Large |
| Escape | Pause/resume |
| R | Retry the current checkpoint or tutorial lesson |
| Enter | Start from the ready screen |

Forward travel is automatic. Small is 55% scale and travels at 10 m/s, Medium travels at 18 m/s, and Large is 140% scale and travels at 13 m/s. Small and Medium consume 2 fuel per second; Large consumes 3.5. Green fuel restores 30. Ground speed changes gradually after resizing; an airborne resize preserves launch momentum.

Fans affect Small strongly and Large weakly. Gold walls break only under a fully grown Large car; solid orange barriers remain solid. Heavy gold plates require Large and open a purple door. Small fits narrow vents. A transformation that would intersect an obstacle is refused. In City, breaking the support reveals a Small vent route on the left and a Medium speed strip on the right. Both reach the next checkpoint.

## Tutorial and courses

Eight tutorial lessons teach steering, shrinking, breaking a weak wall with Large, fuel, heavy plates, airflow, automatic ramp jumps, and a combined finish. Lessons advance only when their required action succeeds. Failure restores the current lesson. Fuel cannot run out during training.

City teaches passages and gates. Desert introduces crosswinds and jumps. Coast combines those skills with rotating machinery. Checkpoints sit shortly before each major obstacle. Crashes, falls, and empty fuel retry the current checkpoint after a brief pause, with at least 60 fuel. Course time includes retries but excludes pauses. Full restart clears the timer. Best times are stored locally per browser/device; tutorial completion is stored separately.

## Implementation

`StuntCourse` owns authored geometry, fuel positions, wind volumes, jumps, plates and checkpoints. `StuntRun` owns the deterministic simulation, collisions, size changes, breaking, lesson progression and checkpoint snapshots. `StuntGame` presents the simulation using the existing car, road, fuel and streetlight assets and the shared driver's finite-route pose interface. Prototype scenes instantiate no original gameplay managers or pickup spawners.

The simulation uses small time steps, including during transformation, to prevent passing through thin barriers. Visual scale matches the collision footprint. Procedural scenery uses the project's existing road meshes and environment palette/materials, with simple obstacle and machinery geometry.

## Build and verification

**The Outlaws > Transform Stunt > Build WebGL** produces `Builds/TransformStunt-WebGL`. Serve the directory over HTTP, rather than opening `index.html` directly. From the repository, run `node Tools/serve-transform-stunt.cjs`, then visit `http://127.0.0.1:8080`. Stop the server with Ctrl+C. The build uses uncompressed files for straightforward static hosting. Share the entire build folder with teammates, or upload its contents to a static host when ready.

Headless acceptance checks: launch Unity with `-batchmode -nographics -quit -projectPath <project> -executeMethod TransformStuntValidation.Run -logFile <log>`. These exercise real steering, transformations, fuel, collisions, and tutorial completion on every course, plus scene reference checks. `TransformStuntPreview.Capture` is an opt-in graphical smoke check; run without `-nographics` or `-quit` and it exits after saving four camera previews.

## Team playtest

Start with a fresh tutorial run without outside instructions. Record:

- Which controls or lessons were unclear?
- Did Q and E make changing size predictable?
- Did both sizes feel useful? Did you stay small for most of a course?
- Were gates, fans, jump openings, and failures readable early enough?
- Did checkpoint retries help you improve, and did you want another attempt?

This is a tuning prototype with geometric art. Human playtests should decide final speeds, wind strength, and difficulty before adding more content.
