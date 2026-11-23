using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftCity
    {
        private void UpdateCamera(bool immediate)
        {
            if(Escape!=null && Escape.Active) return;
            if(StoryActive && OceanBoss!=null && OceanBoss.PresentIntroCamera(view)) return;
            RoadPathPoint point = Route.SampleDriver(Distance, Route.Sample(Distance), Lateral);
            const float follow = .28f;
            Vector3 desired = point.Position - point.Forward * 12f + Vector3.up * 6.4f + point.Right * Lateral * follow;
            bool falling = State == ShiftRunState.Retrying && (lastFailure == "NO ROAD" || lastFailure == "MISSED RELAY LANDING");
            if ((IsRelayAirborne || (Getaway?.Extras.Jump.Traversing ?? false)) && !falling) desired.y += Player.position.y - point.Position.y - .08f;
            if (falling) desired = point.Position - point.Forward * 2f + Vector3.up * 10f;
            // Off a guardrail gap: stand at the broken edge and look down as the car tumbles into the valley mist.
            // Off a guardrail gap: hold still just inside the edge and watch the car go over and roll down the slope.
            if (falling && GapFall) desired = GapFallCameraPosition;
            Vector3 target = Route.SampleDriver(Distance + 22f, Route.Sample(Distance + 22f), Lateral).Position + Vector3.up * 0.6f;
            float cameraFov = Mathf.Lerp(70f, 76f, Mathf.InverseLerp(WorldShiftLevel.Speed * WorldShiftLevel.CruiseSpeedMultiplier, WorldShiftRelay.BoostSpeed, Speed));
            // Show the actual practice block and its ice while driving is safely stopped.
            // Keep the view centred on the road so sideways motion remains easy to judge.
            if (TrainingFreezeWaiting && !TrainingLessonPaused && State == ShiftRunState.Running)
            {
                ShiftHazard practice = null;
                foreach (var hazard in Hazards)
                    if (hazard.LateralMotion && hazard.Start > Distance && (practice == null || hazard.Start < practice.Start))
                        practice = hazard;
                if (practice != null)
                {
                    var focus = Route.Sample((practice.Start + practice.End) * .5f);
                    desired = focus.Position - focus.Forward * 10f + focus.Up * 4f;
                    target = focus.Position + focus.Up * .7f;
                    cameraFov = 55f;
                }
            }
            float blend = immediate ? 1f : 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            view.transform.position = Vector3.Lerp(view.transform.position, desired, blend);
            if (IsMerged)
                view.transform.position -= point.Right * Vector3.Dot(view.transform.position - point.Position, point.Right);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, cameraFov, blend);
            if (falling) target = GapFall ? Player.position : Player.position + point.Forward * 4f;
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation, Quaternion.LookRotation(target - view.transform.position, Vector3.up), IsMerged ? 1f : blend);
            // Crash/landing kick, applied in camera space after the normal follow.
            if (Impact != null) view.transform.position += view.transform.rotation * Impact.ShakeOffset;
        }
    }
}
