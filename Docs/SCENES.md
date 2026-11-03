# Scene scopes and editor tools

## Current playable game

Open **The Outlaws > World Shift > Open Game Menu** or `Assets/Scenes/WorldShift_Menu.unity` and enter Play mode.

The build order is Menu, City, Desert, Ocean, Snow, Mountain, Tutorial. Files use `WorldShift_Menu.unity`, `<Environment>_WorldShift.unity` and `WorldShift_Tutorial.unity`. Gameplay order and cash-out timing are owned by `WorldShiftFlow`, not build indices.

Use **The Outlaws > World Shift > Setup > Create Missing Campaign Scenes** to create missing variants and register current build entries. The tool preserves existing scenes. It disables known legacy/validation entries while preserving unrelated build entries.

## Validation and practice

These remain development-only entry points and are excluded from the normal build:

- `City_DualWorldValidation.unity`: fixed-centre split-screen rendering experiment.
- `Desert_WorldShiftTrial.unity`: short rock-freezing trial.
- Opening `City_WorldShift` directly without an Escape run uses its standalone checkpoint practice layout; launch from the menu to test current Escape.

Find these under **The Outlaws > World Shift > Validation**. Keep their scene paths and `.meta` GUIDs stable because editor tools and regression tests reference them.

## Legacy World game

`Env_City`, `Env_Desert`, `Env_OceanCoast` and `Env_Snow` are deprecated as current playtest entry points. Their separate gameplay systems remain for reference and possible mechanic ports. Tools are grouped under **The Outlaws > Legacy World**. These scenes stay in place and remain disabled in the normal build.

This cleanup does not delete old artwork, prefabs, materials, scenes or shared driving code. Asset removal requires a separate dependency review.
