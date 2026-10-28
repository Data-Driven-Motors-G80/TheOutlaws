using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// A lightweight whole-figure impact body. The finite run owns its physics
    /// clock, so pauses and checkpoint retries also control the flying figures.
    /// </summary>
    public sealed class WorldShiftPedestrian : MonoBehaviour
    {
        private WorldShiftCity game;
        private Vector3 standingPosition, centre, velocity, spin;
        private Quaternion standingRotation, rotation;
        private RoadPathPoint standingRoad;
        private float roadDistance, elapsed;
        private int bounces;

        public bool HasBeenHit { get; private set; }
        public bool IsAirborne { get; private set; }
        public float StandingLateral { get; private set; }
        public float RoadDistance => roadDistance;

        public void Initialize(WorldShiftCity controller, float distance)
        {
            game = controller;
            roadDistance = distance;
            standingPosition = transform.position;
            standingRotation = transform.rotation;
            standingRoad = game.Route.Sample(distance);
            StandingLateral = Vector3.Dot(standingPosition - standingRoad.Position, standingRoad.Right);
        }

        public void Launch(Vector3 carPosition, Vector3 carForward, float carSpeed)
        {
            if (HasBeenHit || game == null) return;
            HasBeenHit = IsAirborne = true;
            elapsed = 0f;
            bounces = 0;
            rotation = standingRotation;
            centre = standingPosition + standingRoad.Up * .9f;
            float side = Mathf.Sign(Vector3.Dot(standingPosition - carPosition, standingRoad.Right));
            if (side == 0f) side = StandingLateral >= 0f ? 1f : -1f;
            velocity = carForward.normalized * Mathf.Clamp(carSpeed * .43f, 8f, 15f)
                + standingRoad.Up * 8f + standingRoad.Right * side * 2.6f;
            spin = (standingRoad.Right * (230f + Mathf.Abs(StandingLateral) * 12f)
                + standingRoad.Forward * side * 150f + standingRoad.Up * side * 65f);
        }

        public void Step(float deltaTime)
        {
            if (!IsAirborne || game == null || (game.State != ShiftRunState.Running && game.State != ShiftRunState.Retrying)) return;
            float remaining = Mathf.Max(0f, deltaTime);
            while (remaining > .00001f && IsAirborne)
            {
                float dt = Mathf.Min(remaining, 1f / 60f);
                remaining -= dt;
                elapsed += dt;
                velocity += Vector3.down * (22f * dt);
                centre += velocity * dt;
                rotation = Quaternion.Euler(spin * dt) * rotation;
                spin *= Mathf.Exp(-.22f * dt);
                float at = roadDistance + Vector3.Dot(centre - standingPosition, standingRoad.Forward);
                RoadPathPoint ground = game.Route.Sample(at);
                float height = Vector3.Dot(centre - ground.Position, ground.Up);
                bool besideRoad = Mathf.Abs(Vector3.Dot(centre - ground.Position, ground.Right)) > ground.Width * .5f;
                bool hasGround = besideRoad || game.Route.HasSurface(at, ShiftWorld.Echo);
                if (hasGround && height < .36f && Vector3.Dot(velocity, ground.Up) < 0f)
                {
                    centre += ground.Up * (.36f - height);
                    if (bounces++ == 0 && elapsed < 2.5f)
                    {
                        float down = Vector3.Dot(velocity, ground.Up);
                        velocity = (velocity - ground.Up * down) * .58f - ground.Up * down * .22f;
                        spin *= .55f;
                    }
                    else
                    {
                        IsAirborne = false;
                        velocity = Vector3.zero;
                        rotation = Quaternion.LookRotation(ground.Forward, ground.Up) * Quaternion.Euler(90f, 0f, 0f);
                    }
                }
                transform.SetPositionAndRotation(centre - rotation * Vector3.up * .9f, rotation);
                if (elapsed > 3.5f) IsAirborne = false;
            }
        }

        public void RestoreStanding()
        {
            HasBeenHit = IsAirborne = false;
            velocity = spin = Vector3.zero;
            elapsed = 0f;
            bounces = 0;
            transform.SetPositionAndRotation(standingPosition, standingRotation);
        }
    }
}
