using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>One finite path shared by both worlds, using the existing road geometry code.</summary>
    public sealed class WorldShiftRoute : MonoBehaviour
    {
        private sealed class Piece
        {
            public float Start, End;
            public RoadSegment Segment;
            public Renderer Surface, Markings;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private Material dayRoad, echoRoad;
        private bool validation;
        private float length;
        private ShiftRelay[] relays;

        public void Build(RoadSegment template, bool straightValidation = false, float validationLength = WorldShiftSplitView.Length, bool desertProfile = false, ShiftRelay[] relayLayout = null)
        {
            validation = straightValidation;
            relays = relayLayout ?? (validation ? System.Array.Empty<ShiftRelay>() : WorldShiftRelay.Sections);
            length = validation ? validationLength : WorldShiftLevel.Length;
            dayRoad = RuntimeMaterials.Coloured(new Color(0.22f, 0.23f, 0.24f));
            echoRoad = RuntimeMaterials.Coloured(new Color(0.085f, 0.105f, 0.14f));
            Material lines = RuntimeMaterials.Coloured(new Color(0.85f, 0.88f, 0.87f));
            var cuts = new SortedSet<float> { -80f, 0f, length + 80f };
            for (float d = -40f; d < length + 80f; d += 40f) cuts.Add(d);
            foreach (var hazard in validation ? WorldShiftSplitView.Layout : WorldShiftLevel.Hazards)
                if (hazard.Kind == ShiftHazardKind.Gap) { cuts.Add(hazard.Start); cuts.Add(hazard.End); }
            if (!validation) foreach (float boundary in WorldShiftLevel.MergeBounds) cuts.Add(boundary);
            foreach (var jump in relays)
                foreach (float cut in new[] { jump.Ramp - 20f, jump.Ramp, jump.Ramp + 24f, jump.Takeoff, jump.Landing, jump.End }) cuts.Add(cut);
            Pose entry = new Pose(Vector3.back * 80f, Quaternion.identity);
            float start = -80f;
            foreach (float end in cuts)
            {
                if (end <= start) continue;
                RoadSegment segment = Instantiate(template, transform);
                segment.name = "Shared road " + start.ToString("0");
                Transform legacyCar = segment.transform.Find("Car");
                if (legacyCar != null) legacyCar.gameObject.SetActive(false);
                float angle = desertProfile ? DesertHeading(end) - DesertHeading(start) : validation ? 0f : Heading(end) - Heading(start);
                segment.ConfigureCurve(end - start, angle, false,
                    desertProfile ? DesertRelaySlope(start, true) : validation ? 0f : Slope(start, true),
                    desertProfile ? DesertRelaySlope(end, false) : validation ? 0f : Slope(end, false));
                segment.AlignStartTo(entry);
                entry = segment.ExitPose;
                foreach (Collider collider in segment.GetComponentsInChildren<Collider>()) collider.enabled = false;
                foreach (string name in new[] { "Left Roadside Wall", "Right Roadside Wall" })
                {
                    Transform wall = segment.transform.Find(name);
                    if (wall != null) wall.gameObject.SetActive(false);
                }
                var markings = segment.GetComponent<RoadMarking>();
                if (markings == null) markings = segment.gameObject.AddComponent<RoadMarking>();
                markings.GetComponent<Renderer>().sharedMaterial = lines;
                var piece = new Piece
                {
                    Start = start, End = end, Segment = segment,
                    Surface = segment.transform.Find("Road").GetComponent<Renderer>(),
                    Markings = markings.GetComponent<Renderer>()
                };
                pieces.Add(piece);
                start = end;
            }
            SetWorld(ShiftWorld.Day);
        }

        // Reuse the old desert's 25-degree bend scale, with clear sight lines at
        // each timing puzzle. This finite path remains identical in both worlds.
        private static float DesertHeading(float d)
            => DesertBend(d, 200f, 360f, 520f, 640f)
                - DesertBend(d, 1120f, 1280f, 1440f, 1560f);
        // The aerial puzzle and final rift use a straight approach so their openings are readable.
        private float DesertRelaySlope(float d, bool afterEdge)
        {
            foreach (var jump in relays)
                if (d >= jump.Ramp - 20f && d <= jump.End)
                    return d < jump.Takeoff || (d == jump.Takeoff && !afterEdge)
                        ? .28f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(jump.Ramp, jump.Ramp + 24f, d)) : 0f;
            return DesertSlope(d);
        }
        private static float DesertBend(float d, float a, float b, float c, float e)
            => 25f * (Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, d))
                - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c, e, d)));
        private static float DesertSlope(float d)
            => d >= 200f && d <= 360f ? .055f * Mathf.Sin((d - 200f) / 160f * Mathf.PI * 2f)
                : d >= 520f && d <= 640f ? -.045f * Mathf.Sin((d - 520f) / 120f * Mathf.PI * 2f) : 0f;

        public void SetSurfaceMaterials(Material day, Material night)
        { if (day != null) dayRoad = day; if (night != null) echoRoad = night; }

        // Original-style 20-25 degree bends, authored so road puzzles stay readable.
        private static readonly Vector2[] HeadingKeys =
        {
            new Vector2(-80f, 0f), new Vector2(560f, 0f), new Vector2(820f, 0f),
            new Vector2(880f, -3f), new Vector2(1040f, -3f), new Vector2(1120f, -22f), new Vector2(1320f, -22f),
            new Vector2(1420f, 0f), new Vector2(1680f, 0f), new Vector2(1780f, 0f), new Vector2(1880f, 0f),
            new Vector2(2000f, 0f), new Vector2(2240f, 0f), new Vector2(2340f, 0f), new Vector2(2480f, 0f),
            new Vector2(2580f, 0f), new Vector2(2800f, 0f), new Vector2(WorldShiftLevel.Length + 80f, 0f)
        };

        private static float Heading(float d)
        {
            for (int i = 1; i < HeadingKeys.Length; i++)
                if (d <= HeadingKeys[i].x)
                    return Mathf.Lerp(HeadingKeys[i - 1].y, HeadingKeys[i].y,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HeadingKeys[i - 1].x, HeadingKeys[i].x, d)));
            return HeadingKeys[HeadingKeys.Length - 1].y;
        }
        private static float Slope(float d, bool afterEdge)
        {
            foreach (var jump in WorldShiftRelay.Sections)
                if (d >= jump.Ramp - 20f && d <= jump.End)
                    return d < jump.Takeoff || (d == jump.Takeoff && !afterEdge)
                        ? .28f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(jump.Ramp, jump.Ramp + 24f, d)) : 0f;
            return d < 720f ? 0f : .025f * Mathf.Sin((d - 720f) / 190f)
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2640f, 2720f, d)));
        }

        public RoadPathPoint Sample(float distance)
        {
            distance = Mathf.Clamp(distance, -80f, length + 80f);
            foreach (var piece in pieces)
                if (distance <= piece.End + 0.001f) return piece.Segment.Sample(distance - piece.Start);
            return pieces[pieces.Count - 1].Segment.Sample(pieces[pieces.Count - 1].Segment.Length);
        }

        public bool HasSurface(float distance, ShiftWorld world)
        {
            foreach (var jump in relays)
                if (!jump.HasDeck(distance, world)) return false;
            foreach (var h in validation ? WorldShiftSplitView.Layout : WorldShiftLevel.Hazards)
                if (h.World == world && h.Kind == ShiftHazardKind.Gap && distance >= h.Start && distance < h.End)
                    return false;
            return true;
        }

        // Roadside geometry uses the same piece boundaries and rendered edge vertices.
        public void VisitRoadPieces(System.Action<RoadSegment, float, float> visit)
        {
            foreach (var piece in pieces) visit(piece.Segment, piece.Start, piece.End);
        }

        public void SetWorld(ShiftWorld world)
        {
            foreach (var piece in pieces)
            {
                float middle = (piece.Start + piece.End) * .5f;
                bool exists = HasSurface(middle, world);
                piece.Surface.enabled = exists;
                piece.Surface.sharedMaterial = world == ShiftWorld.Day ? dayRoad : echoRoad;
                piece.Markings.enabled = exists;
            }
        }

    }
}
