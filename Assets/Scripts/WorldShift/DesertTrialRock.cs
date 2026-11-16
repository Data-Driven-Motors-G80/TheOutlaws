using UnityEngine;

namespace Outlaws.WorldShift
{
    // Normal/airborne rocks share one state across worlds. Finale rocks belong
    // to one half only, with their entire body kept away from the seam.
    public sealed class DesertTrialRock
    {
        public readonly float Distance, Phase, Direction;
        public readonly float MotionSpeed;
        public readonly bool Guarded;
        public readonly int MergeSide;
        public readonly bool Airborne;
        public readonly float Radius;
        public bool VisibleIn(ShiftWorld world) => MergeSide == 0 || (MergeSide < 0 ? world == ShiftWorld.Day : world == ShiftWorld.Echo);
        public float DaySeconds { get; private set; }
        public float NightSeconds { get; private set; }
        public float Lateral => AtTime(DaySeconds);
        private float AtTime(float seconds)
        {
            float x = Direction * (4.1f - Mathf.Abs(Mathf.Repeat(seconds * MotionSpeed + Phase + 4.1f, 16.4f) - 8.2f));
            return MergeSide == 0 ? x : MergeSide * (1.45f + Mathf.PingPong(seconds * MotionSpeed + Phase, 2.1f));
        }
        public float LateralIn(ShiftWorld world, bool joined) => AtTime(joined && world == ShiftWorld.Echo ? NightSeconds : DaySeconds);
        public DesertTrialRock(float distance, float phase, float direction, float motionSpeed = 1.6f, bool guarded = false, int mergeSide = 0, bool airborne = false, float radius = 1.3f)
        { Distance = distance; Phase = phase; Direction = direction; MotionSpeed = motionSpeed; Guarded = guarded; MergeSide = mergeSide; Airborne = airborne; Radius = mergeSide != 0 || airborne ? 1.1f : radius; }
        public void Advance(float delta, ShiftWorld world)
        { if (world == ShiftWorld.Day) DaySeconds += delta; }
        public void Restore(float daySeconds) => DaySeconds = daySeconds;
        public void CaptureNight() => NightSeconds = DaySeconds;
        public void RestoreNight(float nightSeconds) => NightSeconds = nightSeconds;
    }

    public readonly struct DesertTrialGate
    {
        public readonly float Start;
        public readonly ShiftWorld World;
        public readonly int HalfSide;
        public readonly bool SwitchOnly;
        public bool VisibleIn(ShiftWorld world, bool shared) => !SwitchOnly && shared || world == World;
        public float End => Start + 8f;
        public DesertTrialGate(float start, ShiftWorld world, int halfSide = 0, bool switchOnly = false) { Start = start; World = world; HalfSide = halfSide; SwitchOnly = switchOnly; }
    }
}
