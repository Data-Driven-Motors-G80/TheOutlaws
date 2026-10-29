using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Non-solid, geometric warning for the distance-triggered final crossing.</summary>
    public sealed class WorldShiftRift : MonoBehaviour
    {
        private IWorldShiftView game;
        private Transform entrance;
        private readonly List<LineRenderer> filaments = new List<LineRenderer>();
        private Material day, night;

        public void Build(IWorldShiftView controller, WorldShiftRoute route)
        {
            game = controller;
            var point = route.Sample(game.MergeStart);
            entrance = new GameObject("World rift - pass through, never a collision").transform;
            entrance.SetParent(transform, false);
            entrance.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
            day = MakeMaterial("Rift daylight filament", new Color(1f, .68f, .18f));
            night = MakeMaterial("Rift night filament", new Color(.18f, .85f, 1f));
            foreach (int side in new[] { -1, 1 })
            {
                Material material = side < 0 ? day : night;
                Line("Jagged portal outline", material, .11f,
                    new Vector3(side * 5.5f, .08f, 0f), new Vector3(side * 5.8f, 1.7f, .1f),
                    new Vector3(side * 5.35f, 3f, -.12f), new Vector3(side * 5.7f, 4.8f, .1f),
                    new Vector3(side * 4.2f, 6.6f, 0f), new Vector3(side * 2.7f, 6.35f, .15f),
                    new Vector3(side * 1.4f, 7.1f, -.1f), new Vector3(0f, 6.8f, 0f));
                // Broken upper seam suggests a tear, leaving the driver's view open.
                Line("Upper time fracture", material, .07f,
                    new Vector3(side * .07f, 7.1f, 0f), new Vector3(side * .28f, 6.1f, 0f),
                    new Vector3(side * .08f, 5.45f, 0f), new Vector3(side * .22f, 4.75f, 0f));
                // Outward arrows show that both halves are traversable. No wall or pane.
                float x = side * 2.5f;
                Line("Steer across symbol", material, .11f,
                    new Vector3(x - side * .7f, 5.1f, -.15f), new Vector3(x + side * .7f, 5.1f, -.15f));
                Line("Steer across arrowhead", material, .11f,
                    new Vector3(x, 5.6f, -.15f), new Vector3(x + side * .7f, 5.1f, -.15f),
                    new Vector3(x, 4.6f, -.15f));
                // Short paired approach markers lead into the gate without covering the road.
                for (int i = 0; i < 3; i++)
                {
                    float z = -24f + i * 8f;
                    Line("Approach marker", material, .09f,
                        new Vector3(side * 4.85f, .06f, z - 1.2f),
                        new Vector3(side * 4.35f, .06f, z),
                        new Vector3(side * 4.85f, .06f, z + 1.2f));
                }
            }
            Refresh(0f);
        }

        public void Refresh(float distance)
        {
            if (entrance == null) return;
            bool visible = distance >= game.MergeStart - 180f && distance <= game.MergeStart + 65f;
            if (entrance.gameObject.activeSelf != visible) entrance.gameObject.SetActive(visible);
            if (!visible) return;
            // Simulation time keeps this cue stable across both renders and when paused.
            float pulse = .88f + .12f * Mathf.Sin(game.Duration * 5f);
            foreach (LineRenderer line in filaments) line.widthMultiplier = pulse;
        }

        private static Material MakeMaterial(string label, Color colour)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = label };
            material.SetColor("_BaseColor", colour);
            return material;
        }

        private void Line(string label, Material material, float width, params Vector3[] points)
        {
            var root = new GameObject(label);
            root.transform.SetParent(entrance, false);
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthCurve = AnimationCurve.Constant(0f, 1f, width);
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            filaments.Add(line);
        }

        private void OnDestroy()
        {
            if (day != null) Destroy(day);
            if (night != null) Destroy(night);
            if (entrance != null) Destroy(entrance.gameObject);
        }
    }
}
