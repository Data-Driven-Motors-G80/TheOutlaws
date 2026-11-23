using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain getaway: the coupe races into a stone-arched tunnel in the mountainside and a heavy avalanche gate
    /// drops behind it, leaving the police braking outside. Uses the shared passage timing and camera, with the gate
    /// as the closing shutter.
    /// </summary>
    public sealed partial class WorldShiftLevelEscape
    {
        private void BuildMountainTunnel()
        {
            var material = Finish("Mountain tunnel finishes", Color.white);
            var lamps = Finish("Mountain tunnel lights", new Color(1f, .78f, .45f), 1.4f);
            var set = new WorldShiftArtMesh();
            Color meadow = new Color(.34f, .42f, .30f), granite = new Color(.47f, .48f, .48f), graniteDark = new Color(.36f, .37f, .38f);
            Color stone = new Color(.60f, .57f, .52f), snow = new Color(.92f, .95f, .98f), asphalt = new Color(.13f, .18f, .21f);
            set.Box(new Vector3(0, -.3f, 8), new Vector3(140, .5f, 150), meadow);
            set.Box(new Vector3(0, -.02f, 0), new Vector3(11, .08f, 85), asphalt);
            for (int z = -38; z < 17; z += 6) set.Box(new Vector3(0, .03f, z), new Vector3(.13f, .02f, 2.4f), new Color(.84f, .83f, .69f));
            // Guardrails along the approach.
            foreach (float side in new[] { -1f, 1f })
            {
                set.Box(new Vector3(side * 6.4f, .8f, -11f), new Vector3(.12f, .3f, 54f), new Color(.6f, .64f, .65f));
                for (int z = -38; z < 16; z += 4) set.Box(new Vector3(side * 6.4f, .4f, z), new Vector3(.14f, .8f, .14f), new Color(.6f, .64f, .65f));
            }
            // The mountainside: big rock masses either side of the portal and over it, snow on the shoulders.
            for (int i = 0; i < 7; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (14f + (i / 2) * 11f);
                float height = 22f + (i * 7) % 13;
                set.Rock(new Vector3(x, -1f, 30f + (i % 3) * 5f), new Vector3(9f + i % 3 * 2f, height, 14f), i % 2 == 0 ? granite : graniteDark, i);
                set.Rock(new Vector3(x, height * .78f, 30f + (i % 3) * 5f), new Vector3(4.5f, height * .3f, 7f), snow, i + 20);
            }
            set.Rock(new Vector3(0f, 7f, 30f), new Vector3(16f, 18f, 13f), granite, 41);
            set.Rock(new Vector3(-2f, 21f, 32f), new Vector3(8f, 9f, 7f), snow, 42);
            // Cut rock face around the portal, then a stone arch and keystone framing the opening.
            // The face is built around the opening, so the raised gate stays hidden inside it until it drops.
            foreach (float side in new[] { -1f, 1f })
                set.Box(new Vector3(side * 10.225f, 6f, 17f), new Vector3(9.55f, 14f, 1.2f), graniteDark);
            set.Box(new Vector3(0f, 9.65f, 17f), new Vector3(10.9f, 6.7f, 1.2f), graniteDark);
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 6; i++)
                    set.ChamferBox(new Vector3(side * (5.85f + (i % 2) * .1f), .5f + i * 1.0f, 16.1f), new Vector3(1.1f, .95f, .9f), .08f, stone * (.92f + .04f * (i % 3)));
            const int voussoirs = 11;
            for (int i = 0; i < voussoirs; i++)
            {
                float a = Mathf.PI * (i + .5f) / voussoirs;
                Vector3 c = new Vector3(Mathf.Cos(a) * 6.3f, 6.1f + Mathf.Sin(a) * 2.6f, 16.1f);
                Color block = i == voussoirs / 2 ? new Color(.70f, .66f, .58f) : stone * (.9f + .05f * (i % 3));
                set.ChamferBox(c, new Vector3(1.45f, i == voussoirs / 2 ? 1.25f : 1f, .95f), .08f, block);
            }
            set.Box(new Vector3(0f, 7.3f, 16.05f), new Vector3(11.4f, 2.4f, .9f), stone * .88f);
            // Tunnel interior: dark walls receding, with warm lights along the roof.
            set.Box(new Vector3(0f, 3.1f, 40f), new Vector3(12f, 6.3f, .4f), new Color(.018f, .028f, .035f));
            foreach (float side in new[] { -1f, 1f }) set.Box(new Vector3(side * 5.6f, 3.1f, 28.8f), new Vector3(.4f, 6.3f, 22.4f), new Color(.08f, .09f, .1f));
            set.Box(new Vector3(0f, 6.2f, 28f), new Vector3(11.6f, .3f, 23f), new Color(.07f, .08f, .09f));
            // Fir trees on the meadow either side of the approach.
            for (int i = 0; i < 10; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var foot = new Vector3(side * (11f + (i * 7) % 9), 0f, -34f + i * 5.5f);
                set.Cylinder(foot, .3f, .22f, 2.2f, new Color(.30f, .22f, .14f), 7);
                set.Cylinder(foot + Vector3.up * 1.5f, 2.2f, 0f, 4.2f, new Color(.15f, .30f, .24f), 7);
                set.Cylinder(foot + Vector3.up * 3.8f, 1.5f, 0f, 3.2f, new Color(.18f, .35f, .27f), 7);
            }
            Draw("Mountain tunnel escape", set, material);
            var lights = new WorldShiftArtMesh();
            for (int z = 19; z < 39; z += 4) lights.Box(new Vector3(0f, 5.95f, z), new Vector3(2.2f, .12f, .5f), Color.white);
            foreach (float side in new[] { -1f, 1f }) lights.Box(new Vector3(side * 4.6f, 5.4f, 15.9f), new Vector3(.3f, .4f, .3f), Color.white);
            Draw("Tunnel lights and portal lanterns", lights, lamps);
            // Heavy avalanche gate: dark steel panel, red-and-white hazard band, rivets. It is the closing shutter.
            var gate = new WorldShiftArtMesh();
            gate.Box(Vector3.zero, new Vector3(10.8f, 6, .3f), new Color(.21f, .24f, .27f));
            for (int i = -2; i <= 2; i++) gate.Box(new Vector3(0, i * 1.2f, -.17f), new Vector3(10.6f, .12f, .06f), new Color(.36f, .39f, .42f));
            for (int i = 0; i < 12; i++)
                gate.Box(new Vector3(-5.0f + i * .91f, -2.55f, -.19f), new Vector3(.91f, .55f, .05f), i % 2 == 0 ? new Color(.86f, .1f, .08f) : new Color(.96f, .96f, .94f));
            for (float x = -5f; x <= 5f; x += 1.25f)
                for (float y = -2f; y <= 2.5f; y += 1.5f) gate.Cylinder(new Vector3(x, y, -.2f), .07f, .07f, .05f, new Color(.55f, .58f, .6f), 6);
            shutter = Draw("Closing service passage shutter", gate, material);
        }
    }
}
