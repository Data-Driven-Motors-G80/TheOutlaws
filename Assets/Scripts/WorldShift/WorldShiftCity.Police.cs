using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftCity
    {
        private bool policeReady, policeCapturing;
        private float policeCaptureTime, policeCaptureStart, policeCaptureX;
        private readonly WorldShiftPolicePursuit policePursuit = new WorldShiftPolicePursuit();
        private readonly System.Collections.Generic.List<WorldShiftPolicePursuit.Obstacle> policeObstacles = new System.Collections.Generic.List<WorldShiftPolicePursuit.Obstacle>();
        public float PoliceRoadDistance => LevelMode ? policePursuit.Distance : Distance - 9.3f;
        private bool PoliceAirborne(float at)
        {
            foreach(var section in RelayLayout)
                if(at>=section.Takeoff && at<section.Landing) return true;
            return false;
        }
        private void CollectPoliceObstacles()
        {
            policeObstacles.Clear();
            foreach(var h in Hazards)
            {
                if(!WorldShiftCombat.IsActive(Combat,h) || !h.VisibleIn(World) || h.Kind==ShiftHazardKind.Gap) continue;
                void Add(float start,float end,float left,float right)
                { policeObstacles.Add(new WorldShiftPolicePursuit.Obstacle(h,start,end,left,right)); }
                if(h.Gate!=null)
                {
                    if(!h.Gate.Airborne) Add(h.Gate.Distance-h.Gate.Size.z*.5f,h.Gate.Distance+h.Gate.Size.z*.5f,
                        h.Gate.Lateral-h.Gate.Size.x*.5f,h.Gate.Lateral+h.Gate.Size.x*.5f);
                    continue;
                }
                if(h.Rock!=null)
                {
                    if(!h.Rock.Airborne) Add(h.Rock.Distance-h.Rock.Radius,h.Rock.Distance+h.Rock.Radius,
                        h.Rock.Lateral-h.Rock.Radius,h.Rock.Lateral+h.Rock.Radius);
                    continue;
                }
                // Bounds are evaluated at PLAYER progress, exactly as the rendered oncoming traffic.
                h.BoundsAt(Distance,out float start,out float end);
                if(h.LateralMotion) Add(start,end,h.LateralOffset-WorldShiftSpikePlatform.Width*.5f,h.LateralOffset+WorldShiftSpikePlatform.Width*.5f);
                else if(h.Kind==ShiftHazardKind.HalfBarrier) Add(start,end,h.HalfSide<0?-5f:0f,h.HalfSide<0?0f:5f);
                else for(int lane=0;lane<3;lane++)
                {
                    if((h.LaneMask & (1<<lane))==0) continue;
                    float x=(lane-1)*WorldShiftLevel.RoadWidth/3f;
                    Add(start,end,x-WorldShiftLevel.RoadWidth/6f,x+WorldShiftLevel.RoadWidth/6f);
                }
            }
        }

        private void UpdatePolice(float deltaTime)
        {
            if (Police == null) return;
            if (LevelMode)
            {
                if(!policeReady) { policePursuit.Reset(Distance,Lateral); policeReady=true; }
                if(Capturing)
                {
                    // Capture remains scripted: a player crash always retries the checkpoint.
                    if(!policeCapturing)
                    {
                        policeCaptureTime=0f; policeCaptureStart=policePursuit.Distance; policeCaptureX=policePursuit.Lateral;
                    }
                    policeCapturing=true;
                    policeCaptureTime+=deltaTime;
                    float t=Mathf.Clamp01(policeCaptureTime/1.2f);
                    t=1f-Mathf.Pow(1f-t,3f);
                    Police.PresentFinitePose(deltaTime,Mathf.Lerp(policeCaptureStart,Distance-3.3f,t),
                        Mathf.Lerp(policeCaptureX,Lateral+(Lateral>0f?-2.6f:2.6f),t),Distance,0f);
                }
                else
                {
                    policeCapturing=false;
                    CollectPoliceObstacles();
                    float limit=Mathf.Max(0f,Route.Sample(policePursuit.Distance).Width*.5f-1f);
                    policePursuit.Step(deltaTime,Distance,Lateral,Speed,limit,policeObstacles,PoliceAirborne);
                    Police.PresentFinitePose(deltaTime,policePursuit.Distance,policePursuit.Lateral,Distance,policePursuit.Jolt);
                }
                return;
            }
            // Like the original chase, compress logical distance into a readable mirror gap.
            Police.StepFinite(deltaTime, Distance, Lateral, Mathf.Lerp(.45f, 6f, PursuitFraction));
        }
    }
}
