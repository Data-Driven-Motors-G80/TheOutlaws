using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Driving off a Mountain guardrail gap: the car carries its speed off the edge in a straight line (out of the
    /// bend), flies briefly with the nose dipping and the body leaning toward the valley, then lands on the slope and
    /// rolls down the hillside toward the mist. Deterministic and purely visual; the retry is unchanged.
    /// </summary>
    public sealed partial class WorldShiftCity
    {
        private Vector3 gapFallStart, gapFallForward, gapFallRight;
        private float gapFallTime, gapFallSide, gapFallSpeed, gapFallDistance, gapFallPitch;

        /// <summary>
        /// The camera follows the car over the edge from behind and above, never dropping below the road, so the
        /// car stays large in frame as it rolls down toward the mist.
        /// </summary>
        public Vector3 GapFallCameraPosition
        {
            get
            {
                Vector3 heading = (gapFallForward * .9f + gapFallRight * gapFallSide * .45f).normalized;
                Vector3 behind = Player.position - heading * 10f - gapFallRight * gapFallSide * 2f;
                behind.y = Mathf.Max(Player.position.y + 5f, gapFallStart.y + 3.5f);
                return behind;
            }
        }

        private void BeginGapFall()
        {
            var road = Route.Sample(Distance);
            gapFallStart = Player.position;
            gapFallForward = Vector3.ProjectOnPlane(road.Forward, Vector3.up).normalized;
            gapFallRight = Vector3.Cross(Vector3.up, gapFallForward).normalized;
            gapFallSide = Mathf.Sign(Lateral);
            gapFallSpeed = Mathf.Max(18f, Speed);
            gapFallDistance = Distance;
            gapFallTime = 0f; gapFallPitch = 0f;
        }

        private void StepGapFall(float delta)
        {
            gapFallTime += delta;
            float t = gapFallTime;
            // Straight on, angled out of the bend, slowing as it goes.
            Vector3 heading = (gapFallForward * .9f + gapFallRight * gapFallSide * .45f).normalized;
            const float drag = .9f;
            float travelled = gapFallSpeed * (1f - Mathf.Exp(-drag * t)) / drag;
            Vector3 flat = gapFallStart + heading * travelled;
            // Height of the valley slope under the car, measured from the road beside it.
            var road = Route.Sample(gapFallDistance + Vector3.Dot(flat - gapFallStart, gapFallForward));
            float offset = Vector3.Dot(flat - road.Position, Vector3.ProjectOnPlane(road.Right, Vector3.up).normalized) * gapFallSide;
            float ground = road.Position.y + .55f + WorldShiftEnvironmentScenery.MountainGroundHeight(offset - WorldShiftLevel.RoadWidth * .5f, 1f);
            float air = gapFallStart.y - .5f * 20f * t * t;
            bool airborne = air > ground + .05f;
            Player.position = new Vector3(flat.x, Mathf.Max(air, ground), flat.z);
            // On the shoulder it just skids; past the break of the slope it tips toward the valley, then rolls over
            // and over in step with how far it has tumbled downhill. The nose dips only while it is in the air.
            float slopeStart = WorldShiftLevel.RoadWidth * .5f + 2.6f;
            float lean = 38f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(slopeStart - .8f, slopeStart + 2.2f, offset));
            float roll = lean + Mathf.Max(0f, offset - slopeStart - 2.2f) * 30f;
            gapFallPitch = Mathf.MoveTowards(gapFallPitch, airborne ? 20f : 4f, 60f * delta);
            Player.rotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(gapFallPitch, 0f, -gapFallSide * roll);
        }
    }
}
