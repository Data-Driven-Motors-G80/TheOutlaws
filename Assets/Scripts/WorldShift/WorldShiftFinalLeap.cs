using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Two readable aerial gates for the final Night-right to Day-left transfer.</summary>
    public sealed class WorldShiftFinalLeap : MonoBehaviour
    {
        private sealed class Gate
        {
            public ShiftHazard Hit;
            public float Centre, Width;
            public Transform Root;
            public float X(float distance) => Centre + Mathf.Sin((distance - WorldShiftRelay.FinalLeap.Takeoff) * .055f) * .2f;
        }
        private readonly List<Gate> gates = new List<Gate>();
        private readonly List<Material> materials = new List<Material>();
        private WorldShiftRoute route;
        private WorldShiftRelay sampler;

        public void Build(WorldShiftRoute sharedRoute)
        {
            route = sharedRoute;
            sampler = new WorldShiftRelay(route);
            AddGate(ShiftWorld.Echo, 3240f, 3.1f, 1.8f);
            AddGate(ShiftWorld.Day, 3272f, -1.05f, 1.2f);
        }

        private void AddGate(ShiftWorld world, float at, float x, float width)
        {
            var root = new GameObject(world + " final aerial barrier").transform;
            root.SetParent(transform, false);
            var data = new Gate { Hit = new ShiftHazard(world, ShiftHazardKind.Barrier, at - 1f, at + 1f, 0),
                Centre = x, Width = width, Root = root };
            gates.Add(data);
            Material body = new Material(RuntimeMaterials.Coloured(new Color(.26f, .29f, .32f)));
            Material edge = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            edge.SetColor("_BaseColor", world == ShiftWorld.Day ? new Color(1f, .65f, .2f) : new Color(.3f, .85f, 1f));
            materials.Add(body); materials.Add(edge);
            Box(root, "Solid airborne block", Vector3.zero, new Vector3(width, 1.8f, 2f), body);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, "Visible horizontal edge", new Vector3(0f, side * .86f, -1.04f), new Vector3(width, .09f, .08f), edge);
                Box(root, "Visible vertical edge", new Vector3(side * width * .48f, 0f, -1.04f), new Vector3(.09f, 1.8f, .08f), edge);
            }
            // An arrow on the obstacle's face reinforces the required leftward route.
            Box(root, "Left arrow shaft", new Vector3(.03f, 0f, -1.1f), new Vector3(width * .55f, .10f, .06f), edge);
            foreach (float sign in new[] { -1f, 1f })
            {
                Transform tip = Box(root, "Left arrowhead", new Vector3(-width * .21f, sign * .10f, -1.1f), new Vector3(.28f, .09f, .06f), edge);
                tip.localRotation = Quaternion.Euler(0f, 0f, sign * 45f);
            }
        }

        public void Present(ShiftWorld world, float distance)
        {
            foreach (var gate in gates)
            {
                gate.Root.gameObject.SetActive(world == gate.Hit.World && distance > 3060f && distance < gate.Hit.End + 25f);
                var point = sampler.SampleFlight(WorldShiftRelay.FinalLeap, gate.Hit.Start + 1f);
                gate.Root.SetPositionAndRotation(point.Position + point.Right * gate.X(distance) + Vector3.up * .9f,
                    Quaternion.LookRotation(point.Forward, point.Up));
            }
        }

        public ShiftHazard FindCollision(float from, float to, float xFrom, float xTo, ISet<ShiftHazard> handled)
        {
            foreach (var gate in gates)
            {
                if (handled.Contains(gate.Hit) || to + 1.5f < gate.Hit.Start || from - 1.5f > gate.Hit.End) continue;
                float a = xFrom - gate.X(from), b = xTo - gate.X(to), extent = gate.Width * .5f + .8f;
                if (Mathf.Min(a, b) < extent && Mathf.Max(a, b) > -extent) return gate.Hit;
            }
            return null;
        }

        private static Transform Box(Transform root, string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(root, false); obj.transform.localPosition = position; obj.transform.localScale = size;
            obj.GetComponent<Collider>().enabled = false; obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj.transform;
        }
        private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
