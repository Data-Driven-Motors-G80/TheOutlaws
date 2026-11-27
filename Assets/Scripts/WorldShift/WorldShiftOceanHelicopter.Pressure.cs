using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanHelicopter
    {
        // Small, individually warned shots keep Day under pressure between the authored heavy attacks.
        // A separate bounded pool reuses the existing missile/marker/impact art without changing their rules.
        private readonly List<AirStrike> pressureStrikes=new List<AirStrike>(3);
        private readonly StrikeView[] pressureViews=new StrikeView[3];
        private float pressureCooldown,pressureResumeAt=15f;
        public int PressureShotsFired { get; private set; }
        public int PressureStrikeCount => pressureStrikes.Count;

        private bool StepPressure(float dt,float from,float to,float fromX,float toX)
        {
            pressureStrikes.RemoveAll(s=>from>s.EndDistance+PointHalfSize+CarHalfLength+1f);
            bool day=game.World==ShiftWorld.Day;
            if(day) pressureCooldown=Mathf.Max(0f,pressureCooldown-dt);
            if(day && from>=pressureResumeAt && !game.IsRelayAirborne && pressureCooldown<=0f && pressureStrikes.Count<pressureViews.Length)
            {
                float target=to+60f;
                int aim=Mathf.RoundToInt(PredictLane(PressureShotsFired+101,dt,fromX,toX));
                if(PressureLane(target,aim,out float x))
                {
                    var point=new PointImpact {Distance=target,Lateral=x,LaunchSeconds=.3f,ImpactSeconds=1.8f};
                    pressureStrikes.Add(new AirStrike {Distance=target,EndDistance=target,Lateral=x,
                        Kind=OceanAirAttackKind.PointSalvo,Pressure=true,Points=new[]{point}});
                    PressureShotsFired++;
                    audioSource.PlayOneShot(lockClip,.10f*WorldShiftAudioSettings.Effects);
                    pressureCooldown=from<game.Course.Ramps[0].End ? 1.3f : .95f;
                }
                else pressureCooldown=.15f; // Wait for a fair spot; don't accumulate a burst to fire later.
            }
            bool collision=false;
            foreach(var strike in pressureStrikes)
            {
                float previous=strike.Time;
                if(day) strike.Time+=dt;
                collision|=StepPoints(strike,dt,previous,from,to,fromX,toX);
            }
            return collision;
        }

        private bool PressureLane(float at,int aim,out float x)
        {
            x=0f;
            if(at>game.RunLength-55f) return false;
            foreach(var jump in game.Course.Ramps)
                if(at>=jump.Ramp-55f && at<=jump.End+45f) return false;
            foreach(var save in game.Checkpoints) if(Mathf.Abs(at-save.Distance)<45f) return false;
            foreach(var item in game.Collection.Items) if(Mathf.Abs(at-item.x)<10f) return false;
            for(int i=0;i<slots.Count;i++)
                if(game.Course.AirStrikeEnds[i]<=slots[i]+1f && Mathf.Abs(at-slots[i])<18f) return false;

            int occupied=0;
            foreach(var h in game.Hazards)
            {
                if(h.LateralMotion)
                {
                    // Never predict the moving spike's future phase or fill its only frozen exit.
                    if(at>=h.Start-18f && at<=h.End+18f) return false;
                    continue;
                }
                float k=h.MotionSpeed/WorldShiftLevel.Speed;
                float first=(h.Start+(h.Start-150f)*k)/(1f+k),last=(h.End+(h.Start-150f)*k)/(1f+k);
                float margin=h.LaneMask==7 ? 24f : 16f;
                if(at<first-margin || at>last+margin) continue;
                // Full Night gates require a completely empty Day return window, not a late dodge.
                if(h.LaneMask==7) return false;
                occupied|=h.LaneMask;
            }
            foreach(var strike in strikes) MarkNearbyPoints(strike,at,ref occupied);
            foreach(var strike in pressureStrikes) MarkNearbyPoints(strike,at,ref occupied);
            if(occupied==7) return false;
            // Do not let a later authored warning fill a previously chosen pressure-shot exit.
            // Protecting a shared outside lane is stricter than merely testing the current world.
            for(int tryLane=0;tryLane<3;tryLane++)
            {
                int lane=tryLane==0 ? aim+1 : (aim+1+tryLane)%3;
                if((occupied | (1<<lane))==7) continue;
                x=(lane-1)*WorldShiftLevel.RoadWidth/3f;
                return true;
            }
            return false;
        }

        private static void MarkNearbyPoints(AirStrike strike,float at,ref int occupied)
        {
            if(strike.Points==null) return;
            foreach(var point in strike.Points)
            {
                if(Mathf.Abs(point.Distance-at)>18f) continue;
                for(int lane=0;lane<3;lane++)
                    if(Mathf.Abs((lane-1)*WorldShiftLevel.RoadWidth/3f-point.Lateral)<PointHalfSize+CarHalfWidth+.25f)
                        occupied|=1<<lane;
            }
        }
    }
}
