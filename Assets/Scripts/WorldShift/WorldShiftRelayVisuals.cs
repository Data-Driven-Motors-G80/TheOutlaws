using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Prototype launch arrows and landing targets, deliberately different from pit warnings.</summary>
    public sealed class WorldShiftRelayVisuals : MonoBehaviour
    {
        private sealed class View
        {
            public ShiftRelay Data;
            public GameObject Root, Ramp, SourceVoid, DestinationVoid, Landing, Preview;
        }
        private readonly List<View> views = new List<View>();
        private readonly List<Material> owned = new List<Material>();
        private WorldShiftRoute route;
        private Material boost, white, amber, cyan, ghost;

        public void Build(WorldShiftCity controller, WorldShiftRoute sharedRoute)
            => Build(sharedRoute, WorldShiftRelay.Sections);

        public void Build(WorldShiftRoute sharedRoute, ShiftRelay[] sections)
        {
            route = sharedRoute;
            boost = Unlit(new Color(.25f, 1f, .54f));
            white = Unlit(new Color(.9f, 1f, .96f));
            amber = Unlit(new Color(1f, .7f, .25f));
            cyan = Unlit(new Color(.25f, .8f, 1f));
            ghost = Unlit(new Color(.48f, .6f, .66f));
            foreach (var data in sections)
            {
                var view = new View { Data = data, Root = Child(transform, "Relay " + data.Number + " - launch and landing") };
                views.Add(view);
                view.Ramp = Child(view.Root.transform, "Green boost arrows on the actual uphill deck");
                for (float d = data.Ramp + 3f; d < data.Takeoff - 2f; d += 6f)
                {
                    RoadPathPoint p = route.Sample(d);
                    if (d < data.Ramp + 25f)
                        Box(view.Ramp.transform, "Full-road acceleration strip", p.Position + p.Up * .055f,
                            Quaternion.LookRotation(p.Forward, p.Up), new Vector3(9.3f, .06f, 1.15f), boost);
                    foreach (float x in new[] { -3.2f, 0f, 3.2f })
                        Line(view.Ramp.transform, "Forward boost chevron", white, .11f,
                            p.Position + p.Right * (x - .65f) + p.Up * .10f - p.Forward * .8f,
                            p.Position + p.Right * x + p.Up * .10f + p.Forward * .4f,
                            p.Position + p.Right * (x + .65f) + p.Up * .10f - p.Forward * .8f);
                }
                var signPoint = route.Sample(data.Ramp - 22f);
                string source = data.Source == ShiftWorld.Day ? "DAY" : "NIGHT";
                string target = data.Destination == ShiftWorld.Day ? "DAY" : "NIGHT";
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 at = signPoint.Position + signPoint.Right * side * 5.75f;
                    Box(view.Root.transform, "Ramp sign support", at + signPoint.Up * 1.2f,
                        Quaternion.LookRotation(signPoint.Forward, signPoint.Up), new Vector3(.12f, 2.4f, .12f), white);
                    // Side-view ramp and flight arc pictogram, not the triangle/X used for ordinary pits.
                    Line(view.Root.transform, "Ramp and takeoff pictogram", boost, .14f,
                        at + signPoint.Up * 2.6f - signPoint.Right * .7f,
                        at + signPoint.Up * 3.15f + signPoint.Right * .45f,
                        at + signPoint.Up * 2.6f + signPoint.Right * .45f);
                    Line(view.Root.transform, "Flight arc pictogram", white, .10f,
                        at + signPoint.Up * 3.25f + signPoint.Right * .5f,
                        at + signPoint.Up * 3.65f + signPoint.Right * 1f,
                        at + signPoint.Up * 3.5f + signPoint.Right * 1.5f);
                    Label(view.Root.transform, source + " BOOST", at + signPoint.Up * 4.1f,
                        signPoint, new Color(.4f, 1f, .65f), 3f);
                }
                view.SourceVoid = Child(view.Root.transform, "Launch world missing landing");
                view.DestinationVoid = Child(view.Root.transform, "Landing world missing ramp");
                // Reuse the existing real shaft geometry but not its danger lips/triangle/X markings.
                WorldShiftPitVisuals.Build(view.SourceVoid.transform, route,
                    new ShiftHazard(data.Source, ShiftHazardKind.Gap, data.Takeoff, data.End, 7), false);
                WorldShiftPitVisuals.Build(view.DestinationVoid.transform, route,
                    new ShiftHazard(data.Destination, ShiftHazardKind.Gap, data.Ramp, data.Landing, 7), false);
                view.Landing = Child(view.Root.transform, "Solid landing markers");
                view.Preview = Child(view.Root.transform, "Other-world landing outline - no road here yet");
                Material targetMaterial = data.Destination == ShiftWorld.Day ? amber : cyan;
                LandingFrame(view.Landing.transform, data, targetMaterial, false);
                RoadPathPoint landingPoint = route.Sample(data.Landing + 12f);
                Label(view.Root.transform, target + " LANDING", landingPoint.Position + landingPoint.Up * 3.3f,
                    landingPoint, data.Destination == ShiftWorld.Day ? new Color(1f, .75f, .35f) : new Color(.4f, .85f, 1f), 5f);
            }
            SetWorld(ShiftWorld.Day);
            Refresh(0f);
        }

        public void SetWorld(ShiftWorld world)
        {
            foreach (var view in views)
            {
                bool launch = world == view.Data.Source;
                view.Ramp.SetActive(launch);
                view.SourceVoid.SetActive(launch);
                view.DestinationVoid.SetActive(!launch);
                view.Landing.SetActive(!launch);
                view.Preview.SetActive(false);
            }
        }

        public void Refresh(float distance)
        {
            foreach (var view in views)
            {
                bool visible = distance >= view.Data.Ramp - 180f && distance < view.Data.End + 80f;
                if (view.Root.activeSelf != visible) view.Root.SetActive(visible);
            }
        }

        private void LandingFrame(Transform parent, ShiftRelay jump, Material material, bool dashed)
        {
            for (float d = jump.Landing; d < jump.Landing + 24f; d += 4f)
                foreach (float side in new[] { -1f, 1f })
                {
                    var a = route.Sample(d); var b = route.Sample(d + (dashed ? 2f : 4f));
                    Line(parent, "Landing edge", material, .15f,
                        a.Position + a.Right * side * 4.65f + a.Up * .12f,
                        b.Position + b.Right * side * 4.65f + b.Up * .12f);
                }
            var lip = route.Sample(jump.Landing);
            Line(parent, "Landing threshold", material, .17f,
                lip.Position - lip.Right * 4.65f + lip.Up * .12f,
                lip.Position + lip.Right * 4.65f + lip.Up * .12f);
            foreach (float side in new[] { -1f, 1f })
                Line(parent, "Landing beacon - decorative", material, .18f,
                    lip.Position + lip.Right * side * 5.2f,
                    lip.Position + lip.Right * side * 5.2f + lip.Up * 3.5f);
            var target = route.Sample(jump.Landing + 10f);
            Vector3 tip = target.Position + target.Up * 1.8f;
            Line(parent, "Downward landing target", material, .14f, tip + target.Up * 2.6f, tip);
            Line(parent, "Landing arrowhead", material, .14f,
                tip - target.Right + target.Up * 1.2f, tip, tip + target.Right + target.Up * 1.2f);
        }

        private static GameObject Child(Transform parent, string name)
        { var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj; }
        private Material Unlit(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            owned.Add(material);
            return material;
        }
        private static void Line(Transform parent, string name, Material material, float width, params Vector3[] points)
        {
            var line = Child(parent, name).AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
        }
        private static void Box(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.localScale = size;
            obj.GetComponent<Collider>().enabled = false;
            obj.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static void Label(Transform parent, string text, Vector3 position, RoadPathPoint point, Color color, float width)
        {
            var label = Child(parent, text).AddComponent<TextMeshPro>();
            label.transform.SetPositionAndRotation(position, Quaternion.LookRotation(point.Forward, point.Up));
            label.text = text;
            label.fontSize = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(width, 1.2f);
        }
        private void OnDestroy() { foreach (var material in owned) if (material != null) Destroy(material); }
    }
}
