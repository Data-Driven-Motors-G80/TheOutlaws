using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed class ShiftRelay
    {
        public readonly int Number;
        public readonly float Ramp, Takeoff, Landing, End;
        public readonly ShiftWorld Source;
        public ShiftWorld Destination => Source == ShiftWorld.Day ? ShiftWorld.Echo : ShiftWorld.Day;
        public ShiftRelay(int number, float ramp, float takeoff, float landing, ShiftWorld source)
        { Number = number; Ramp = ramp; Takeoff = takeoff; Landing = landing; End = landing + 56f; Source = source; }
        public bool Contains(float distance) => distance >= Ramp - 32f && distance < End;
        public bool HasDeck(float distance, ShiftWorld world) => world == Source
            ? distance < Takeoff || distance >= End : distance < Ramp || distance >= Landing;
        public float SpeedAt(float distance, float cruisingSpeed = WorldShiftLevel.Speed)
        {
            float boost = distance < Landing
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Ramp, Ramp + 24f, distance))
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Landing, End, distance));
            return Mathf.Lerp(cruisingSpeed, WorldShiftRelay.BoostSpeed, boost);
        }
    }

    /// <summary>Authored relay flight uses the shared route coordinate and one simulation clock.</summary>
    public sealed class WorldShiftRelay
    {
        public const float BoostSpeed = 42f;
        public static readonly ShiftRelay[] Sections =
        {
            new ShiftRelay(1, 600f, 660f, 744f, ShiftWorld.Day),
            new ShiftRelay(2, 1740f, 1800f, 1896f, ShiftWorld.Echo),
            new ShiftRelay(3, 2300f, 2360f, 2468f, ShiftWorld.Day)
        };
        private readonly WorldShiftRoute route;
        private readonly ShiftRelay[] sections;
        private readonly float cruisingSpeed;
        private ShiftRelay flying;
        public int Completed { get; private set; }
        public WorldShiftRelay(WorldShiftRoute sharedRoute, ShiftRelay[] layout = null, float speed = WorldShiftLevel.Speed)
        { route = sharedRoute; sections = layout ?? Sections; cruisingSpeed = speed; }
        public bool IsAirborne => flying != null;
        public ShiftRelay Flight => flying;
        public static ShiftRelay At(float distance)
        {
            foreach (var section in Sections) if (section.Contains(distance)) return section;
            return null;
        }
        public ShiftRelay SectionAt(float distance)
        {
            foreach (var section in sections) if (section.Contains(distance)) return section;
            return null;
        }
        public float SpeedAt(float distance) => SectionAt(distance)?.SpeedAt(distance, cruisingSpeed) ?? cruisingSpeed;
        public bool BeginStep(float from, float to, ShiftWorld world)
        {
            foreach (var section in sections)
                if (from < section.Takeoff && to >= section.Takeoff && world == section.Source)
                { flying = section; return true; }
            return false;
        }
        public bool FinishStep(float distance, ShiftWorld world, out bool landed)
        {
            landed = false;
            if (flying == null || distance < flying.Landing) return true;
            if (world != flying.Destination) return false;
            flying = null;
            Completed++;
            landed = true;
            return true;
        }
        public void Reset(float checkpoint)
        {
            flying = null;
            Completed = 0;
            foreach (var section in sections) if (section.Landing < checkpoint) Completed++;
        }
        public RoadPathPoint SampleDriver(float distance)
            => flying != null && distance >= flying.Takeoff && distance <= flying.Landing
                ? SampleFlight(flying, distance) : route.Sample(distance);
        public RoadPathPoint SamplePolice(float distance)
        {
            foreach (var section in sections)
                if (distance >= section.Takeoff && distance < section.Landing) return SampleFlight(section, distance);
            return route.Sample(distance);
        }
        public RoadPathPoint SampleFlight(ShiftRelay section, float distance)
        {
            var start = route.Sample(section.Takeoff);
            var end = route.Sample(section.Landing);
            float length = section.Landing - section.Takeoff;
            float t = Mathf.InverseLerp(section.Takeoff, section.Landing, distance);
            float peak = length * .07f;
            Vector3 position = Vector3.Lerp(start.Position, end.Position, t) + Vector3.up * (4f * peak * t * (1f - t));
            Vector3 tangent = end.Position - start.Position + Vector3.up * (4f * peak * (1f - 2f * t));
            return new RoadPathPoint(position, tangent.normalized, start.Width);
        }
        public string Hint(float distance, ShiftWorld world)
        {
            var section = flying ?? SectionAt(distance);
            if (section == null) return "";
            string source = section.Source == ShiftWorld.Day ? "DAY" : "NIGHT";
            string target = section.Destination == ShiftWorld.Day ? "DAY" : "NIGHT";
            if (flying != null) return world == section.Destination
                ? target + " LANDING READY - STAY IN THIS WORLD"
                : "AIRBORNE - PRESS SPACE FOR " + target + " LANDING";
            if (distance >= section.Landing) return "LANDED - BOTH CARS RETURN TO NORMAL SPEED";
            return world == section.Source ? "BOOST RAMP - DRIVE UP, THEN PRESS SPACE IN THE AIR"
                : "SWITCH TO " + source + " TO REACH THE BOOST RAMP";
        }
    }
}
