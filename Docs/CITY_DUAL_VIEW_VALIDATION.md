# Short city dual-world validation

Open `Assets/Scenes/City_DualWorldValidation.unity`, or use
**The Outlaws > World Shift > Open Short Dual-World Validation**. Press Play, then Enter.
The existing `City_WorldShift` scene now uses this compositor in its final sector,
after the world rift at 2,800 m. This short scene remains available for isolated comparison.

- 0–4 seconds: normal Day/Night switching with Space.
- 4–16 seconds: Day occupies the left half of the screen, Night the right.
  A/D or arrow keys cross between them; Space is temporarily inactive.
- Three alternating half-road barriers require right, left, then right steering.
- 16–20 seconds: the world occupied at the exit fills the screen; Space works again.
- Esc pauses; R retries this short route. Enter restarts after finishing.

Both worlds are rendered as **complete images with identical camera position and
projection**, using the existing city geometry, materials, lighting, sky and fog.
The compositor takes the left half of the Day image and the right half of the Night
image at unchanged UV coordinates. It does not stretch two whole roads into half-width
views, build a third environment, or place a wall between the worlds. The car can
straddle the seam and receive each world's appearance on the appropriate half.

Gameplay advances once. Separate render requests visit both presentation states
and restore the player's actual world afterward. Invisible obstacle halves cannot
collide. Original driving, pursuit, mirror and fuel systems remain in use.
Rendering is still Unity 3D / URP; the pipeline and project type are unchanged.

This proof deliberately uses a flat, straight road and a camera fixed laterally on
its centre. Screen x = 0.5 then matches road x = 0 at every depth. Curves, slopes,
camera lateral follow and desert are **not validated** by this scene.
The full city integration deliberately straightens and centres its final sector. Supporting them needs a separate seam/mask design, not simply enabling
the same fixed screen crop. Joining currently happens immediately at the section
boundaries, without an entrance animation.

During the joining section, two full world views are rendered per frame (plus the
existing mirror). The main camera skips scene geometry and displays the composite
behind the existing HUD. Each source image is capped at 1920 pixels wide while
preserving the aspect ratio. This is a correctness prototype, not a performance
guarantee for WebGL; no shader or art asset downloads are required.

Focused smoke coverage: matching source-image pixels on each composite half,
unchanged player state during rendering, crossing both directions, ignored hidden
barriers, Space lock/unlock, 20-second completion and restart.

The full-frame result was accepted and integrated into the city finale.
A complementary next mechanic remains a **cross-world relay jump** (launch in one world,
switch while airborne, land on the other world's surviving road). It is not part
of this implementation.
