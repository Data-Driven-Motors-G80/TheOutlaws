using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftPresentation
    {
        private void BuildPitLighting(Transform parent, ShiftHazard gap)
        {
            Material housing = RuntimeMaterials.Coloured(new Color(.13f, .14f, .15f));
            Material lens = Reflective(new Color(1f, .65f, .22f), .65f);
            foreach (bool entrance in new[] { true, false })
            {
                float edge = entrance ? gap.Start : gap.End;
                RoadPathPoint point = route.Sample(edge + (entrance ? 1.5f : -1.5f));
                foreach (float side in new[] { -1f, 1f })
                {
                    var fixture = new GameObject("Pit edge floodlight");
                    fixture.transform.SetParent(parent, false);
                    fixture.transform.SetPositionAndRotation(point.Position + point.Right * side * 5.5f + point.Up * 1.8f,
                        Quaternion.LookRotation(point.Forward, point.Up));
                    Box(fixture.transform, "Pit lamp support", Vector3.down * .9f, new Vector3(.12f, 1.8f, .12f), housing);
                    Box(fixture.transform, "Pit lamp overhang", new Vector3(-side * .55f, 0f, 0f), new Vector3(1.1f, .09f, .09f), housing);
                    Vector3 overOpening = new Vector3(-side * 1.1f, 0f, 0f);
                    Box(fixture.transform, "Pit warning lamp lens", overOpening, new Vector3(.36f, .22f, .32f), lens);
                    var source = new GameObject("Pit lip and inner wall light");
                    source.transform.SetParent(fixture.transform, false);
                    // Overhang the opening so the light reaches the visible inner
                    // faces, rather than only the outside of the exposed deck.
                    source.transform.localPosition = overOpening;
                    Light light = source.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, .72f, .38f);
                    light.intensity = 18f;
                    light.range = 22f;
                    light.shadows = LightShadows.None;
                }
            }
        }

        private void BuildHeadlight(Transform player)
        {
            var lamp = new GameObject("Player forward cone headlight");
            // Attach to the stable car root, not the swaying visual body. The beam
            // follows road pitch and steering without flickering with suspension.
            lamp.transform.SetParent(player.parent, false);
            lamp.transform.localPosition = new Vector3(0f, 1.15f, 1.6f);
            lamp.transform.localRotation = Quaternion.Euler(1f, 0f, 0f);
            playerHeadlight = lamp.AddComponent<Light>();
            playerHeadlight.type = LightType.Spot;
            playerHeadlight.color = new Color(.88f, .94f, 1f);
            playerHeadlight.range = game.GetawayMode ? WorldShiftFlow.Garage.HeadlightRange : 74f;
            playerHeadlight.spotAngle = 36f;
            playerHeadlight.innerSpotAngle = 22f;
            playerHeadlight.intensity = 900f * Mathf.Pow(playerHeadlight.range / 74f, 2f);
            playerHeadlight.renderMode = LightRenderMode.ForcePixel;
            playerHeadlight.shadows = LightShadows.Soft;
            playerHeadlight.shadowStrength = .85f;
            playerHeadlight.shadowBias = .035f;
            playerHeadlight.shadowNormalBias = .1f;
            playerHeadlight.shadowNearPlane = .15f;
            BuildHeadlightHaze(lamp.transform);
        }

        private void BuildHeadlightHaze(Transform lamp)
        {
            Shader shader = Resources.Load<Shader>("WorldShiftHeadlight");
            if (shader == null) return; // The physical spotlight is still usable without haze.
            var material = new Material(shader);
            headlightHazeMaterial = material;
            ownedMaterials.Add(material);
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            const int columns = 8, rows = 24;
            // A faint horizontal slice of haze makes the expanding spotlight
            // legible from the chase camera without a bright tunnel in the view.
            // Both side edges and the far end fade completely to transparent.
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                float z = t * 54f * (playerHeadlight.range / 74f);
                float radius = .12f + z * Mathf.Tan(16f * Mathf.Deg2Rad);
                for (int column = 0; column <= columns; column++)
                {
                    float across = column / (float)columns * 2f - 1f;
                    vertices.Add(new Vector3(across * radius, 0f, z));
                    float fade = Mathf.Sin(t * Mathf.PI) * (1f - across * across);
                    colours.Add(new Color(.58f, .73f, 1f, .065f * fade));
                    if (row == 0 || column == 0) continue;
                    int b = row * (columns + 1) + column, a = b - columns - 1;
                    triangles.Add(a - 1); triangles.Add(b - 1); triangles.Add(b);
                    triangles.Add(a - 1); triangles.Add(b); triangles.Add(a);
                }
            }
            var mesh = new Mesh { name = "Soft prototype headlight cone" };
            mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);
            headlightHaze = new GameObject("Headlight atmosphere", typeof(MeshFilter), typeof(MeshRenderer));
            headlightHaze.transform.SetParent(lamp, false);
            headlightHaze.GetComponent<MeshFilter>().sharedMesh = mesh;
            Renderer renderer = headlightHaze.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Material Reflective(Color colour, float glow)
        {
            var key = (colour, glow);
            if (glowMaterials.TryGetValue(key, out Material shared)) return shared;
            var material = new Material(RuntimeMaterials.Coloured(colour));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * glow);
            ownedMaterials.Add(material);
            glowMaterials.Add(key, material);
            return material;
        }
    }
}
