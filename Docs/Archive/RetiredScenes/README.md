# Retired scene tools and tests

The current game uses only `WorldShift_Menu` and `City_WorldShift`. Training
and all five environments are authored in `WorldShiftLevelCourse`.

The following obsolete scenes and their `.meta` files were removed:

- `Desert_WorldShift`, `Ocean_WorldShift`, `Snow_WorldShift`, `Mountain_WorldShift`, `WorldShift_Tutorial`: previous campaign/escape variants.
- `Env_City`, `Env_Desert`, `Env_OceanCoast`, `Env_Snow`: the original World game.
- `City_DualWorldValidation`, `Desert_WorldShiftTrial`: standalone experiments.
- `Assets/_Recovery/0`: recovery copy of the original World game, with no current level controller.

`Editor/` preserves the retired scene builders and validation commands, including
the old pickup playground builders that referenced an already-removed GetawayChase scene.
`Tests/` preserves the integration suites for the original World game, endless
escape, old tutorial and experimental scenes. The old City-driver smoke test is
preserved as a text snippet; the remaining shared City/relay tests stay active.
These files are outside `Assets`, so Unity does not compile or discover them.
They are historical references, not checks of current level play.

The active tests are `WorldShiftLevelTests`, `DesertLevelCourseTests` and
`WorldShiftSceneSetupTests`, alongside retained shared-unit and controller tests.
The common rendering helper was extracted into `WorldShiftTestCapture`.

Runtime classes and supporting art/prefabs/materials were not deleted. Current
levels reuse parts of those systems. Legacy WorldShift flow entry points route
to the current level controller; they no longer load removed scene variants.
Historical documents elsewhere in Docs/Archive and Docs/Legacy may still name
removed scenes. Restore any historical scene/tool/test set together from Git
history if needed; do not run the archived setup tools against the current game.
