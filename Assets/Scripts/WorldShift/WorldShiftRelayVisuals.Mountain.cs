using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain jumps look like a ski jump: timber side walls with red-and-white kerbs along the ramp, a yellow-and-black
    /// hazard band on the lip, and tall pennant poles with amber lamps either side of the takeoff. Built on the launch
    /// world's ramp only (the other world shows the usual dashed "missing ramp" outline). The green boost strip and
    /// chevrons are unchanged. Visual only.
    /// </summary>
    public sealed partial class WorldShiftRelayVisuals
    {
        private Material skiJumpFinish, skiJumpLamps;

        private void BuildSkiJump(Transform parent, ShiftRelay data)
        {
            if (skiJumpFinish == null)
            {
                skiJumpFinish = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = "Ski jump timber and paint" };
                skiJumpFinish.SetFloat("_Emission", .22f); skiJumpFinish.SetFloat("_Specular", .04f);
                skiJumpLamps = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = "Ski jump lamps" };
                skiJumpLamps.SetFloat("_Emission", 1.3f);
                owned.Add(skiJumpFinish); owned.Add(skiJumpLamps);
            }
            var art = new WorldShiftArtMesh();
            var lamps = new WorldShiftArtMesh();
            Color timber = new Color(.52f, .34f, .19f), timberDark = new Color(.34f, .22f, .12f);
            Color red = new Color(.86f, .1f, .08f), white = new Color(.96f, .96f, .94f);
            Color yellow = new Color(1f, .78f, .1f), black = new Color(.06f, .06f, .07f), steel = new Color(.58f, .62f, .64f);
            // Side walls and kerbs follow the ramp's rise, from its foot to the lip.
            int stripe = 0;
            for (float d = data.Ramp; d < data.Takeoff - .01f; d += 2f, stripe++)
            {
                float end = Mathf.Min(d + 2f, data.Takeoff);
                var a = route.Sample(d); var b = route.Sample(end);
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side * (a.Width * .5f + .18f);
                    Vector3 A = a.Position + a.Right * x, B = b.Position + b.Right * x;
                    art.Beam(A + a.Up * .42f, B + b.Up * .42f, .3f, .84f, stripe % 2 == 0 ? timber : timber * .9f);
                    art.Beam(A + a.Up * .9f, B + b.Up * .9f, .38f, .12f, stripe % 2 == 0 ? red : white);
                    // Diagonal braces on the outside face.
                    art.Beam(A + a.Right * side * .2f + a.Up * .1f, B + b.Right * side * .2f + b.Up * .8f, .08f, .08f, timberDark);
                }
            }
            // Hazard band across the lip, on the edge face, plus a steel lip plate.
            var lip = route.Sample(data.Takeoff);
            float half = lip.Width * .5f;
            int bands = 12;
            for (int i = 0; i < bands; i++)
            {
                float x0 = -half + i * (2f * half / bands), x1 = x0 + 2f * half / bands;
                Vector3 c = lip.Position + lip.Right * ((x0 + x1) * .5f) - lip.Up * .25f + lip.Forward * .05f;
                art.Beam(c - lip.Right * (x1 - x0) * .5f, c + lip.Right * (x1 - x0) * .5f, .05f, .45f, i % 2 == 0 ? yellow : black);
            }
            art.Beam(lip.Position - lip.Right * half + lip.Up * .02f - lip.Forward * .4f, lip.Position + lip.Right * half + lip.Up * .02f - lip.Forward * .4f,
                .8f, .04f, steel);
            // Pennant poles and lamps either side of the takeoff.
            foreach (float side in new[] { -1f, 1f })
                foreach (float back in new[] { 0f, 14f })
                {
                    var p = route.Sample(data.Takeoff - back);
                    Vector3 foot = p.Position + p.Right * side * (p.Width * .5f + .75f);
                    float height = back == 0f ? 6.5f : 4.8f;
                    art.Beam(foot, foot + Vector3.up * height, .14f, .14f, steel);
                    for (int f = 0; f < 4; f++)
                    {
                        Vector3 top = foot + Vector3.up * (height - .3f - f * .55f);
                        Vector3 tip = top + p.Right * side * .95f - Vector3.up * .22f + p.Forward * (f % 2 == 0 ? .15f : -.15f);
                        Color flag = f % 2 == 0 ? red : white;
                        art.Triangle(top, top - Vector3.up * .42f, tip, flag, true);
                    }
                    lamps.Cylinder(foot + Vector3.up * height, .17f, .14f, .26f, new Color(1f, .64f, .1f), 10);
                }
            Draw(parent, "Ski jump timber walls, kerbs, lip band and pennant poles", art, skiJumpFinish);
            Draw(parent, "Ski jump lamps", lamps, skiJumpLamps);
        }

        private static void Draw(Transform parent, string label, WorldShiftArtMesh art, Material material)
        {
            var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            obj.GetComponent<MeshFilter>().sharedMesh = art.Mesh(label);
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
