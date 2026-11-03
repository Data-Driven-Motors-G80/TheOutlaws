using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Optional right-lane ramp. The ordinary road remains underneath the entire jump.</summary>
    public sealed class WorldShiftBonusJump
    {
        public const float Lane = 3.2f, ShortStartOffset = -60f;
        public readonly float Start, Takeoff, Landing, End;
        public bool Committed { get; private set; }
        public bool Completed { get; private set; }
        public float Distance { get; private set; }
        public bool OnRamp => Committed && Distance >= Start && Distance < Takeoff;
        public bool Airborne => Committed && Distance >= Takeoff && Distance < Landing;
        public bool Traversing => Committed && Distance >= Start - 1f && Distance < Landing;

        public WorldShiftBonusJump(float environmentEnd) : this(environmentEnd, false) { }
        public WorldShiftBonusJump(float environmentEnd, bool shortTransition)
        { Start = environmentEnd + (shortTransition ? ShortStartOffset : 40f); Takeoff = Start + 32f; Landing = Takeoff + 64f; End = Landing + 28f; }

        public void BeginStep(float from, float to, float lateral)
        {
            Distance = from;
            if (!Committed && !Completed && from < Start && to >= Start && lateral >= 2.4f)
                Committed = true;
        }

        public void FinishStep(float distance)
        {
            Distance = distance;
            if (Committed && distance >= Landing) { Completed = true; Committed = false; }
        }

        // The same height curve builds the deck, places aerial loot, and drives the car.
        public float HeightAt(float distance)
        {
            if (distance <= Start || distance >= Landing) return 0f;
            if (distance < Takeoff)
            {
                float t = Mathf.InverseLerp(Start, Takeoff, distance);
                return 8f * t * t - 4f * t * t * t;
            }
            float u = Mathf.InverseLerp(Takeoff, Landing, distance);
            return 4f + 8f * u - 28f * u * u + 16f * u * u * u;
        }

        public RoadPathPoint Sample(float distance, RoadPathPoint ground)
        {
            if (!Committed || distance < Start || distance > Landing) return ground;
            float slope = (HeightAt(distance + .05f) - HeightAt(distance - .05f)) / .1f;
            return new RoadPathPoint(ground.Position + Vector3.up * HeightAt(distance),
                (ground.Forward + Vector3.up * slope).normalized, ground.Width);
        }
    }
}
