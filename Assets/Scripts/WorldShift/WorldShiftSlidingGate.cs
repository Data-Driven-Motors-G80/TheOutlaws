using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>A shared solid block: Day shuttles it to the roadside and back, Night holds its position.</summary>
    public sealed class WorldShiftSlidingGate
    {
        public readonly float Distance, Direction, Speed;
        public readonly ShiftRelay Flight;
        public bool Airborne => Flight != null;
        public Vector3 Size => new Vector3(10f, 2.2f, 1.8f);
        public float DaySeconds { get; private set; }
        public float Lateral => Direction * Mathf.PingPong(DaySeconds * Speed, Size.x + 1f);

        public WorldShiftSlidingGate(float distance, float direction, ShiftRelay flight = null)
        { Distance = distance; Direction = Mathf.Sign(direction); Flight = flight; Speed = flight == null ? 3.8f : 6f; }

        public WorldShiftSlidingGate Copy() => new WorldShiftSlidingGate(Distance, Direction, Flight);
        public void Reset() => DaySeconds = 0f;
        public void Advance(float delta, float distance, ShiftWorld world)
        {
            float begin = Airborne ? Flight.Takeoff : Distance - 40f;
            float end = Distance + 100f; // Keep the return leg moving while the block is still visible behind the car.
            if (world == ShiftWorld.Day && distance >= begin && distance <= end) DaySeconds += delta;
        }

        // Swept box versus box in the same road/flight coordinates used for rendering.
        public bool Touches(float from, float to, float fromX, float toX, float fromY, float toY,
            float previousX, float centreY)
        {
            Vector3 a = new Vector3(fromX - previousX, fromY + .55f - centreY, from - Distance);
            Vector3 b = new Vector3(toX - Lateral, toY + .55f - centreY, to - Distance);
            Vector3 extent = Size * .5f + new Vector3(.8f, .5f, 1.5f);
            float enter = 0f, exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float change = b[axis] - a[axis];
                if (Mathf.Abs(change) < .00001f)
                { if (Mathf.Abs(a[axis]) > extent[axis]) return false; continue; }
                float first = (-extent[axis] - a[axis]) / change;
                float last = (extent[axis] - a[axis]) / change;
                enter = Mathf.Max(enter, Mathf.Min(first, last));
                exit = Mathf.Min(exit, Mathf.Max(first, last));
                if (enter > exit) return false;
            }
            return true;
        }
    }
}
