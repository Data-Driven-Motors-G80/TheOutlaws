# For later

Ideas agreed for Wee Woo Run that are parked until we're allowed to add art/animation.
The spirals and bolts below were removed from the alpha build (art isn't allowed yet); `git log -S SpiralTexture` finds the code.

## Power-up animations

- **Dizzy spirals** (reversed controls): bring back the two big purple spirals at the screen edges.
  Make them spin (opposite directions on each side), maybe pulse slightly.
  Code: `DrawEffectOverlays` in `Assets/Scripts/TheOutlaws/OutlawHUD.cs`.
- **Nitro lightning bolts**: bring them back. Add a flicker (alpha) and a small sideways jitter.
  Same method as above.
