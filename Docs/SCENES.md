# Current game scenes

Only two scene files belong to the current game and build:

| Scene | Purpose |
| --- | --- |
| `Assets/Scenes/WorldShift_Menu.unity` | Level select: Training and levels 1–5, saved unlocks and testing controls. First scene in the build. |
| `Assets/Scenes/City_WorldShift.unity` | Shared gameplay scene for every level, including Desert and Ocean. The name is retained to preserve its references and GUID. |

Start with the menu, either by opening it and entering Play mode or choosing
**The Outlaws > Levels > Play From Home Screen**. Selecting a level calls
`WorldShiftLevels.Play`, which loads the shared gameplay scene and selects the
course in `WorldShiftLevelCourse.For`. Training uses slot 0, City 1, Desert 2,
Ocean 3, Snow 4 and Mountain 5. Separate environment scenes are not needed.
Opening the shared scene directly still exposes the retained standalone practice
controller; use the menu to test the current levels.

**The Outlaws > Levels > Create Missing Game Scenes** creates only missing menu
and shared gameplay scenes. It never overwrites existing scenes and sets the
build list to those two entries. It does not recreate old environment variants.

The old campaign variants, `Env_*` scenes, split-screen validation, Desert trial
and recovery scene have been removed. Their scene-dependent tests and setup
tools are [archived outside Assets](Archive/RetiredScenes/README.md), so Unity
neither compiles nor exposes them as menu commands. Shared runtime code and
supporting assets have been retained because current levels reuse them.
