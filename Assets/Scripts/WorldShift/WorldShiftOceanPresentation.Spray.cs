using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Sea spray over the bridge edges, wet patches near the kerbs and tyre mist through them.
    /// Presentation only: spray comes from the sides and stays over the outer edge of the road, never
    /// over the centre lane, and pauses near ramps so jumps keep a clear view.</summary>
    public sealed partial class WorldShiftOceanPresentation
    {
        private sealed class Droplet { public Transform Body; public Vector3 Velocity; public float Age, Life, Size; }
        private readonly List<Droplet> droplets = new List<Droplet>();
        private readonly List<Vector3> wetPatches = new List<Vector3>(); // x = distance, y = lateral, z = half length
        private Mesh dropletMesh;
        private Material sprayMaterial, mistMaterial;
        private float nextSpray = 2f, nextMist;
        private int sprayCount;

        private void BuildSpray()
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dropletMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(primitive); else DestroyImmediate(primitive);
            sprayMaterial = RuntimeMaterials.Coloured(new Color(.92f, .98f, 1f));
            mistMaterial = RuntimeMaterials.Coloured(new Color(.8f, .9f, .95f));
            // Wet patches along the kerbs, away from ramps and gaps.
            var wet = new WorldShiftArtMesh();
            int i = 0;
            for (float d = 40f; d < game.RunLength - 20f; d += 34f, i++)
            {
                if (NearRamp(d, 40f)) continue;
                float side = i % 2 == 0 ? 1f : -1f;
                var p = route.Sample(d);
                float lateral = side * (p.Width * .5f - 1.1f), half = 2.2f + (i * 7 % 4) * .6f;
                wetPatches.Add(new Vector3(d, lateral, half));
                for (float s = -half; s < half - .01f; s += 1f)
                {
                    var a = route.Sample(d + s); var b = route.Sample(d + s + 1f);
                    float wA = .7f + .25f * Mathf.Sin((d + s) * 1.3f), wB = .7f + .25f * Mathf.Sin((d + s + 1f) * 1.3f);
                    Vector3 A(RoadPathPoint q, float w, float off) => q.Position + q.Right * (lateral + off * w) + q.Up * .035f;
                    Q2(wet, A(a, wA, -1f), A(b, wB, -1f), A(b, wB, 1f), A(a, wA, 1f), new Color(.12f, .17f, .2f));
                }
            }
            // Darker, glossier asphalt: clearly still road, never mistaken for a gap.
            var wetFinish = Material("WorldShiftOceanSurface", "Wet asphalt patches");
            wetFinish.SetFloat("_Specular", .55f);
            if (wetFinish.HasProperty("_Smoothness")) wetFinish.SetFloat("_Smoothness", .92f);
            Render("Wet road patches", wet, wetFinish);
        }

        private bool NearRamp(float distance, float margin)
        {
            foreach (var relay in game.RelayLayout)
                if (distance > relay.Ramp - margin - 40f && distance < relay.End + margin) return true;
            return false;
        }

        private void SpawnDroplet(Vector3 at, Vector3 velocity, float size, float life, Material material)
        {
            var obj = new GameObject("Sea spray", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(transform, true);
            obj.transform.position = at; obj.transform.localScale = Vector3.one * size;
            obj.GetComponent<MeshFilter>().sharedMesh = dropletMesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            droplets.Add(new Droplet { Body = obj.transform, Velocity = velocity, Life = life, Size = size });
        }

        private void AnimateSpray(float delta)
        {
            float distance = game.Distance;
            bool night = world == ShiftWorld.Echo;
            // A wave breaks against the bridge side ahead of the car every few seconds.
            nextSpray -= delta;
            if (nextSpray <= 0f && game.State == ShiftRunState.Running)
            {
                sprayCount++;
                nextSpray = (night ? 4.5f : 2.6f) + (sprayCount * 37 % 10) * .2f;
                float at = distance + 28f + (sprayCount * 13 % 30);
                if (!NearRamp(at, 25f) && at < game.RunLength - 30f && !game.IsRelayAirborne)
                {
                    var p = route.Sample(at);
                    float side = sprayCount % 2 == 0 ? 1f : -1f, edge = p.Width * .5f;
                    for (int i = 0; i < (night ? 10 : 18); i++)
                    {
                        float along = (i * 7 % 9 - 4) * .7f;
                        Vector3 origin = p.Position + p.Forward * along + p.Right * side * (edge + .6f) + p.Up * .4f;
                        // Arcs up over the rail and falls no further in than the outer lane's edge.
                        Vector3 v = Vector3.up * (5f + (i * 11 % 7) * .7f) - p.Right * side * (1.2f + (i * 5 % 4) * .45f) + p.Forward * ((i * 3 % 5) - 2) * .3f;
                        SpawnDroplet(origin, v, .12f + (i % 4) * .05f, .9f + (i % 3) * .15f, sprayMaterial);
                    }
                }
            }
            // Tyre mist while driving through a wet patch.
            nextMist -= delta;
            if (nextMist <= 0f && game.State == ShiftRunState.Running && !game.IsRelayAirborne)
                foreach (var patch in wetPatches)
                    if (Mathf.Abs(distance - patch.x) < patch.z && Mathf.Abs(game.Lateral - patch.y) < 1.4f)
                    {
                        nextMist = .07f;
                        var p = route.Sample(distance - 1.3f);
                        foreach (float wheel in new[] { -.8f, .8f })
                            SpawnDroplet(p.Position + p.Right * (game.Lateral + wheel) + p.Up * .25f,
                                -p.Forward * 3f + Vector3.up * 1.6f + p.Right * wheel * 1.5f, .1f, .35f, mistMaterial);
                        break;
                    }
            for (int i = droplets.Count - 1; i >= 0; i--)
            {
                var d = droplets[i];
                d.Age += delta;
                if (d.Age >= d.Life || d.Body == null)
                {
                    if (d.Body != null) Destroy(d.Body.gameObject);
                    droplets.RemoveAt(i); continue;
                }
                d.Velocity += Vector3.down * 14f * delta;
                d.Body.position += d.Velocity * delta;
                float k = d.Age / d.Life;
                d.Body.localScale = Vector3.one * d.Size * (1f - k * k);
            }
        }

        private void ClearSpray()
        {
            foreach (var d in droplets) if (d.Body != null) Destroy(d.Body.gameObject);
            droplets.Clear();
        }
    }
}
