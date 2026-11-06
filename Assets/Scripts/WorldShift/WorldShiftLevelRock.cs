using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Reuse the desert trial's reversible crossing clock on the new level course.</summary>
    public sealed class WorldShiftLevelRock
    {
        public readonly DesertTrialRock Motion;
        public readonly ShiftRelay Flight;
        public readonly float LaneCentre, TravelScale;
        public bool Airborne => Flight != null;
        public float Distance => Motion.Distance;
        public float Radius => Motion.Radius;
        public float Lateral => LaneCentre + Motion.Lateral * TravelScale;

        public WorldShiftLevelRock(float at, float phase, float direction, float speed, float radius = 1f, ShiftRelay flight = null,
            float laneCentre = 0f, float travelScale = 1f)
        {
            Motion = new DesertTrialRock(at, phase, direction, speed, airborne: flight != null, radius: radius);
            Flight = flight;
            LaneCentre = laneCentre;
            TravelScale = travelScale;
        }

        public WorldShiftLevelRock Copy() => new WorldShiftLevelRock(Distance, Motion.Phase, Motion.Direction,
            Motion.MotionSpeed, Radius, Flight, LaneCentre, TravelScale);

        // Sphere against the swept car box. Both use the same visible road-space position.
        // Convex distance minimisation handles lateral motion and the flight arc without
        // charging a hit for the empty corners of a sphere's bounding box.
        public bool Touches(float from, float to, float fromX, float toX, float fromY, float toY,
            float previousX, float centreY)
        {
            if (Mathf.Max(from, to) < Distance - Radius - 1.5f || Mathf.Min(from, to) > Distance + Radius + 1.5f) return false;
            Vector3 a = new Vector3(fromX - previousX, fromY + .55f - centreY, from - Distance);
            Vector3 b = new Vector3(toX - Lateral, toY + .55f - centreY, to - Distance);
            float Squared(float t)
            {
                Vector3 delta = Vector3.Lerp(a, b, t);
                delta = new Vector3(Mathf.Max(0f, Mathf.Abs(delta.x) - .8f),
                    Mathf.Max(0f, Mathf.Abs(delta.y) - .5f), Mathf.Max(0f, Mathf.Abs(delta.z) - 1.5f));
                return delta.sqrMagnitude;
            }
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 16; i++)
            {
                float l = Mathf.Lerp(lo, hi, 1f / 3f), r = Mathf.Lerp(lo, hi, 2f / 3f);
                if (Squared(l) < Squared(r)) hi = r; else lo = l;
            }
            return Mathf.Min(Squared(0f), Squared(1f), Squared((lo + hi) * .5f)) < Radius * Radius;
        }
    }
}
