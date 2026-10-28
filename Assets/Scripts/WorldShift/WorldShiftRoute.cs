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

        public void Build(RoadSegment template)
        {
            dayRoad = RuntimeMaterials.Coloured(new Color(0.22f, 0.23f, 0.24f));
            echoRoad = RuntimeMaterials.Coloured(new Color(0.035f, 0.045f, 0.055f));
            Material lines = RuntimeMaterials.Coloured(new Color(0.85f, 0.88f, 0.87f));
            var cuts = new SortedSet<float> { -80f, 0f, WorldShiftLevel.Length + 80f };
            for (float d = -40f; d < WorldShiftLevel.Length + 80f; d += 40f) cuts.Add(d);
            foreach (var hazard in WorldShiftLevel.Hazards)
                if (hazard.Kind == ShiftHazardKind.Gap) { cuts.Add(hazard.Start); cuts.Add(hazard.End); }
            Pose entry = new Pose(Vector3.back * 80f, Quaternion.identity);
            float start = -80f;
            foreach (float end in cuts)
            {
                if (end <= start) continue;
                RoadSegment segment = Instantiate(template, transform);
                segment.name = "Shared road " + start.ToString("0");
                Transform legacyCar = segment.transform.Find("Car");
                if (legacyCar != null) legacyCar.gameObject.SetActive(false);
                float angle = Heading(end) - Heading(start);
                segment.ConfigureCurve(end - start, angle, false, Slope(start), Slope(end));
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

        // Original-style 20-25 degree bends, authored so road puzzles stay readable.
        private static readonly Vector2[] HeadingKeys =
        {
            new Vector2(-80f, 0f), new Vector2(560f, 0f), new Vector2(640f, 22f), new Vector2(780f, 22f),
            new Vector2(880f, -3f), new Vector2(1040f, -3f), new Vector2(1120f, -22f), new Vector2(1320f, -22f),
            new Vector2(1420f, 0f), new Vector2(1680f, 0f), new Vector2(1780f, 24f), new Vector2(1880f, 24f),
            new Vector2(2000f, 0f), new Vector2(2240f, 0f), new Vector2(2340f, -24f), new Vector2(2480f, -24f),
            new Vector2(2580f, 0f), new Vector2(2800f, 0f), new Vector2(2900f, 22f), new Vector2(3060f, 22f),
            new Vector2(3180f, 0f), new Vector2(WorldShiftLevel.Length + 80f, 0f)
        };

        private static float Heading(float d)
        {
            for (int i = 1; i < HeadingKeys.Length; i++)
                if (d <= HeadingKeys[i].x)
                    return Mathf.Lerp(HeadingKeys[i - 1].y, HeadingKeys[i].y,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HeadingKeys[i - 1].x, HeadingKeys[i].x, d)));
            return HeadingKeys[HeadingKeys.Length - 1].y;
        }
        private static float Slope(float d) => d < 720f ? 0f : 0.025f * Mathf.Sin((d - 720f) / 190f);

        public RoadPathPoint Sample(float distance)
        {
            distance = Mathf.Clamp(distance, -80f, WorldShiftLevel.Length + 80f);
            foreach (var piece in pieces)
                if (distance <= piece.End + 0.001f) return piece.Segment.Sample(distance - piece.Start);
            return pieces[pieces.Count - 1].Segment.Sample(pieces[pieces.Count - 1].Segment.Length);
        }

        public bool HasSurface(float distance, ShiftWorld world)
        {
            foreach (var h in WorldShiftLevel.Hazards)
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
                bool exists = HasSurface((piece.Start + piece.End) * 0.5f, world);
                piece.Surface.enabled = exists;
                piece.Surface.sharedMaterial = world == ShiftWorld.Day ? dayRoad : echoRoad;
                piece.Markings.enabled = exists;
            }
        }

    }
}
