using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public enum OceanAirAttackKind { Missile, TwinSalvo, PointSalvo, ClusterSalvo, MissileCurtain }

    /// <summary>Optional coastal pursuit: read the marked lane(s), steer or freeze the incoming attack.</summary>
    public sealed partial class WorldShiftOceanHelicopter : MonoBehaviour
    {
        public const int MaximumHealth = 6;
        public const float WarningSeconds = 2.2f;
        public const float MissileLaunchSeconds = .65f;
        public const float RocketFlightSeconds = 1.35f;
        public const float WarningDistance = 90f;
        public const float StrikeHalfDepth = 5f;
        public const float PointHalfSize = .9f;
        private const float CarHalfWidth = .8f, CarHalfLength = 1.5f;
        private const int MaximumMissiles = 18;
        public int Hits => Mathf.Min(MaximumHealth, game.Collection.Count);
        public int Health => MaximumHealth - Hits;
        public int DisplayHealth => Mathf.Min(MaximumHealth,Health+(shotAge<RocketFlightSeconds?1:0));
        public string CollisionReason { get; private set; } = "";
        public bool Defeated => Health == 0;
        public int Stage => Hits >= 5 ? 2 : Hits >= 3 ? 1 : 0;
        public float AttackTime => active?.Time ?? 0f;
        public float MotionTime { get; private set; }
        public bool AttackActive => strikes.Count > 0;
        public int LiveStrikeCount => strikes.Count;
        public float TargetDistance => active?.Distance ?? -1f;
        public float TargetEndDistance => active?.EndDistance ?? -1f;
        public float TargetLateral => active?.Lateral ?? 0f;
        public float TargetHalfWidth => active?.HalfWidth ?? 0f;
        public bool ComboActive => active!=null && active.Combo;
        public float DayBlockLateral => active?.BlockLateral ?? 0f;
        public OceanAirAttackKind AttackKind => active?.Kind ?? OceanAirAttackKind.Missile;
        public bool ImpactActive => active != null && active.Landed;
        public bool FinalSalvoSent { get; private set; }
        private bool savedFinalSalvo;
        public int AttackSeed { get; private set; }
        public float IntroClock { get; private set; }
        public const float IntroDuration = 5.5f;
        public string Status => DisplayHealth==0 ? "HELICOPTER DOWN"
            : active!=null && active.Kind==OceanAirAttackKind.MissileCurtain ? "NIGHT: FREEZE"
            : active!=null && active.Kind==OceanAirAttackKind.ClusterSalvo ? "LAST STAND" : "HELI " + DisplayHealth + " / " + MaximumHealth;
        private sealed class PointImpact
        {
            public float Distance, Lateral, LaunchSeconds, ImpactSeconds, ImpactAge=10f;
            public bool Launched, Landed;
            public Vector3 Origin;
        }
        private sealed class AirStrike
        {
            public float Distance, EndDistance, Lateral, Time, ImpactAge=10f;
            public Vector3 Origin;
            public OceanAirAttackKind Kind;
            public bool Launched, Landed, Combo, Pincer, Pressure;
            public PointImpact[] Points;
            public float EscapeLateral;
            public bool IsPointSalvo => Points!=null;
            public bool Spread => EndDistance>Distance+1f;
            public float BlockLateral => Lateral<=0 ? WorldShiftLevel.RoadWidth/3f : -WorldShiftLevel.RoadWidth/3f;
            public float HalfWidth => Kind==OceanAirAttackKind.Missile ? 2.1f : 3.5f;
            public float ImpactSeconds => IsPointSalvo ? Points[Points.Length-1].ImpactSeconds : WarningSeconds;
            public int MissileCount => IsPointSalvo ? Points.Length : Kind==OceanAirAttackKind.TwinSalvo ? 2 : 1;
            public float LaunchAt(int i) => IsPointSalvo ? Points[i].LaunchSeconds : MissileLaunchSeconds;
        }
        private WorldShiftCity game;
        private readonly List<float> slots = new List<float>();
        private readonly List<AirStrike> strikes = new List<AirStrike>(3);
        private AirStrike active;
        private int nextSlot, shownHits;
        private float shotAge = 10, crashAge;
        private bool hitSound = true;
        private Vector3 playerRocketOrigin;
        public IReadOnlyList<float> AttackSlots => slots;

        public void Bind(WorldShiftCity controller)
        {
            game = controller;
            // One seed per level entry; retries keep the same pattern while player motion changes aim.
            AttackSeed=Random.Range(1,1000000);
            slots.AddRange(game.Course.AirStrikeTargets);
            BuildArt();
            BuildCombatEffects();
            BuildDefeatEffects();
            BuildDolphins();
        }

        // Damage removes cargo/twin combinations, not the ordinary attack cadence.
        public bool AttackAllowed(int candidate) => true;

        public void SaveCheckpoint() => savedFinalSalvo=FinalSalvoSent;

        private float PredictLane(int candidate,float dt,float fromX,float toX)
        {
            // Alternate between the current lane and a short, bounded steering prediction.
            // Once a marker is shown, this is never called again for that strike.
            uint hash=unchecked((uint)(AttackSeed+candidate*7919)*1664525u+1013904223u);
            float lead=hash%3==0 ? 0f : .35f+(hash%101)/100f*.2f;
            float velocity=dt>.00001f ? Mathf.Clamp((toX-fromX)/dt,-8f,8f) : 0f;
            float predicted=Mathf.Clamp(toX+velocity*lead,-WorldShiftLevel.RoadWidth/3f,WorldShiftLevel.RoadWidth/3f);
            return Mathf.Clamp(Mathf.Round(predicted/(WorldShiftLevel.RoadWidth/3f)),-1,1);
        }

        // Simulation owns all projectile clocks. Rendering cannot advance or repeat damage.
        public bool Step(float dt, float from, float to, float fromX, float toX)
        {
            if(game.State!=ShiftRunState.Running) return false;
            CollisionReason = "";
            bool airborneCollision=StepDolphins(dt,from,to,fromX,toX);
            if(airborneCollision) CollisionReason="DOLPHIN COLLISION";
            shotAge+=dt;
            if(!hitSound && shotAge>=RocketFlightSeconds)
            {
                hitSound=true;
                audioSource.PlayOneShot(hitClip,.32f*WorldShiftAudioSettings.Effects);
            }
            if(Hits>shownHits)
            {
                shownHits=Hits;shotAge=0;hitSound=false;
                playerRocketOrigin=launcherMuzzle.position;
                audioSource.PlayOneShot(shotClip,.23f*WorldShiftAudioSettings.Effects);
            }
            if(Defeated)
            {
                strikes.Clear();pressureStrikes.Clear();active=null;
                if(shotAge>=RocketFlightSeconds)
                {
                    if(!defeatExplosionPlayed) BeginDefeatExplosion();
                    crashAge+=Mathf.Min(dt,Mathf.Max(0f,shotAge-RocketFlightSeconds));
                    StepSeaImpact();
                }
                return airborneCollision;
            }
            bool day=game.World==ShiftWorld.Day;
            if(day) MotionTime+=dt;
            strikes.RemoveAll(s=>from>s.EndDistance+StrikeHalfDepth+2f);
            active=strikes.Count>0 ? strikes[strikes.Count-1] : null;
            while(nextSlot<slots.Count && to>=slots[nextSlot]-WarningDistance)
            {
                int candidate=nextSlot++;
                // Last stand repeats at every safe opportunity until the aircraft is destroyed.
                bool final=Health==1 && shotAge>=RocketFlightSeconds;
                bool curtain=game.Course.AirStrikeCurtains[candidate];
                bool spread=game.Course.AirStrikeEnds[candidate]>slots[candidate]+1f;
                if(from>slots[candidate]-60f || (!final&&!AttackAllowed(candidate)) || !SafePocket(slots[candidate])) continue;
                // Teach one missile, then frequently mix seven-point fields with legacy attacks.
                // Keep a twin and pincer in the second section before later compact pockets become Night routes.
                bool points=curtain || spread || final || candidate>=1 && candidate!=5 && candidate!=6 && candidate%5!=2 && candidate%5!=3;
                bool twin=!points && Stage<2 && (candidate==5 || candidate%5==3);
                var kind=curtain ? OceanAirAttackKind.MissileCurtain : final ? OceanAirAttackKind.ClusterSalvo : points ? OceanAirAttackKind.PointSalvo
                    : twin ? OceanAirAttackKind.TwinSalvo : OceanAirAttackKind.Missile;
                bool pincer=!points && !twin && candidate>=2 && Stage==0;
                float lane=PredictLane(candidate,dt,fromX,toX);
                bool combo=pincer || twin&&Stage==0;
                float side=lane==0 ? ((AttackSeed+candidate)%2==0?-1:1) : lane;
                active=new AirStrike { Distance=slots[candidate],EndDistance=game.Course.AirStrikeEnds[candidate],Kind=kind,Combo=combo,Pincer=pincer,
                    Lateral=pincer ? 0f : kind!=OceanAirAttackKind.Missile ? side*WorldShiftLevel.RoadWidth/6f : lane*WorldShiftLevel.RoadWidth/3f };
                if(points) BuildPointPattern(active,side,candidate);
                if(kind==OceanAirAttackKind.ClusterSalvo) FinalSalvoSent=true;
                strikes.Add(active);
                audioSource.PlayOneShot(lockClip,.19f*WorldShiftAudioSettings.Effects);
            }
            bool collision=airborneCollision;
            foreach(var strike in strikes)
            {
                strike.ImpactAge+=dt;
                float previousTime=strike.Time;
                if(day) strike.Time+=dt;
                if(strike.IsPointSalvo)
                {
                    if(StepPoints(strike,dt,previousTime,from,to,fromX,toX))
                    { collision=true;CollisionReason="AIR STRIKE"; }
                    continue;
                }
                if(!strike.Launched && strike.Time>=MissileLaunchSeconds)
                {
                    strike.Launched=true;strike.Origin=rightWeapon.position;
                    audioSource.PlayOneShot(shotClip,.24f*WorldShiftAudioSettings.Effects);
                }
                if(!strike.Landed && strike.Time>=strike.ImpactSeconds)
                {
                    strike.Landed=true;strike.ImpactAge=0;
                    audioSource.PlayOneShot(impactClip,.32f*WorldShiftAudioSettings.Effects);
                }
                if(game.IsRelayAirborne || to<strike.Distance-StrikeHalfDepth || from>strike.Distance+StrikeHalfDepth) continue;
                float a=Mathf.Clamp01((strike.Distance-StrikeHalfDepth-from)/Mathf.Max(.0001f,to-from));
                float b=Mathf.Clamp01((strike.Distance+StrikeHalfDepth-from)/Mathf.Max(.0001f,to-from));
                float x0=Mathf.Lerp(fromX,toX,a),x1=Mathf.Lerp(fromX,toX,b);
                bool missileHit=strike.Landed && Mathf.Min(x0,x1)<=strike.Lateral+strike.HalfWidth && Mathf.Max(x0,x1)>=strike.Lateral-strike.HalfWidth;
                if(missileHit) {collision=true;CollisionReason="AIR STRIKE";}
                if(day&&strike.Combo)
                    foreach(float side in new[]{-1f,1f})
                    {
                        float block=side*WorldShiftLevel.RoadWidth/3f;
                        if(strike.Pincer||Mathf.Abs(block-strike.BlockLateral)<.1f)
                        {
                            bool cargoHit=Mathf.Min(x0,x1)<=block+1.55f && Mathf.Max(x0,x1)>=block-1.55f;
                            if(cargoHit) {collision=true;if(CollisionReason.Length==0) CollisionReason="OBSTACLE";}
                        }
                    }
            }
            if(StepPressure(dt,from,to,fromX,toX)) {collision=true;CollisionReason="AIR STRIKE";}
            StepCargoAnimations(dt);
            return collision;
        }

        private void BuildPointPattern(AirStrike strike,float side,int candidate)
        {
            bool final=strike.Kind==OceanAirAttackKind.ClusterSalvo;
            bool curtain=strike.Kind==OceanAirAttackKind.MissileCurtain;
            strike.EscapeLateral=-side*WorldShiftLevel.RoadWidth/3f;
            strike.Points=new PointImpact[curtain?15:final?MaximumMissiles:7];
            float[] compactX={-3.25f,.85f,-1.7f,.7f,-3.4f,-.55f,-2.6f};
            float[] compactZ={-4f,-3.8f,-1.4f,.3f,1.5f,4f,3.9f};
            float[] spreadX={-3.25f,2.9f,0f,3.25f,-2.9f,0f,3.1f};
            for(int i=0;i<strike.Points.Length;i++)
            {
                int wave=i/6,within=i%6;
                float x,z,launch,impact;
                if(curtain)
                {
                    // Physically near full-width: Night is the route. Long warning, slow flight,
                    // no Night barrier in this pocket, no invisible damage outside the squares.
                    x=(i%5-2)*2f;z=(i/5-1)*3.5f;
                    launch=.35f+i*.055f;impact=3.05f+i/5*.1f+i%5*.025f;
                }
                else if(strike.Spread)
                {
                    // Real longitudinal separation: seven impacts 18 m apart across all lanes.
                    // Last stand instead spans three dense waves 54 m apart, with alternating exits.
                    z=final ? wave*54f+(within/2-1)*3.8f : i*18f;
                    float flip=final && wave%2==1 ? -1f : 1f;
                    x=final ? ((within%2==0?-3.25f:-.2f)+(within/2-1)*.12f)*flip : spreadX[i];
                    impact=(final?1.95f:1.65f)+Mathf.Max(0f,z)/25.2f+(final?within*.04f:0f);
                    launch=impact-(final?1.55f:1.3f);
                }
                else
                {
                    x=final ? (within%2==0?-3.25f:-.2f)+(wave-1)*.28f : compactX[i];
                    z=final ? (within/2-1)*3.8f+(wave-1)*.12f : compactZ[i];
                    if((AttackSeed+candidate)%2==0) z=-z;
                    launch=.35f+i*(final?.055f:.10f);
                    impact=final ? 1.95f+wave*.32f+within*.065f : 1.65f+i*.13f;
                }
                strike.Points[i]=new PointImpact {
                    Distance=strike.Distance+z,Lateral=curtain?x:-side*x,
                    LaunchSeconds=launch,ImpactSeconds=impact
                };
            }
            if(strike.Spread) ProtectNightPassages(strike);
        }

        // Only authored Night rows inside a spread field qualify for coordinated missile placement.
        // Oncoming motion is resolved to encounter coordinates, not the distant spawn position.
        private static bool NightPassage(ShiftHazard h,float at,float end,out float first,out float last,out float safeX)
        {
            first=last=safeX=0f;
            if(end<=at+1f || h.World!=ShiftWorld.Echo || h.SharedAcrossWorlds || h.LateralMotion) return false;
            bool car=h.Kind==ShiftHazardKind.Oncoming && (h.LaneMask==1 || h.LaneMask==4);
            bool wall=h.Kind==ShiftHazardKind.Barrier && (h.LaneMask==3 || h.LaneMask==6 || h.LaneMask==7);
            if(!car && !wall) return false;
            float k=h.MotionSpeed/WorldShiftLevel.Speed;
            first=(h.Start+(h.Start-150f)*k)/(1f+k);
            last=(h.End+(h.Start-150f)*k)/(1f+k);
            safeX=h.LaneMask==7 ? 0f : (h.LaneMask==1 || h.LaneMask==3 ? 1f : -1f)*WorldShiftLevel.RoadWidth/3f;
            return first>=at+12f && last<=end;
        }

        private void ProtectNightPassages(AirStrike strike)
        {
            var keep=new List<PointImpact>(strike.Points);
            foreach(var h in game.Hazards)
            {
                if(!NightPassage(h,strike.Distance,strike.EndDistance,out float first,out float last,out float safeX)) continue;
                if(h.LaneMask==7)
                {
                    // A mandatory return to Day gets a genuinely clear window, even after impact.
                    // Decide before showing warnings; never delete a missile that the player already saw.
                    keep.RemoveAll(p=>p.Distance>=first-22f && p.Distance<=last+22f);
                    continue;
                }
                foreach(var point in keep)
                {
                    if(point.Distance<first-16f || point.Distance>last+16f) continue;
                    point.Lateral=safeX>0f ? Mathf.Min(point.Lateral,.75f) : Mathf.Max(point.Lateral,-.75f);
                }
            }
            strike.Points=keep.ToArray();
        }

        private bool StepPoints(AirStrike strike,float dt,float previousTime,float from,float to,float fromX,float toX)
        {
            bool collision=false,launchSound=false,impactSound=false;
            foreach(var point in strike.Points)
            {
                point.ImpactAge+=dt;
                if(!point.Launched && strike.Time>=point.LaunchSeconds)
                {
                    point.Launched=true;point.Origin=rightWeapon.position;
                    launchSound=true;strike.Launched=true;
                }
                if(!point.Landed && strike.Time>=point.ImpactSeconds)
                {
                    point.Landed=true;point.ImpactAge=0;
                    impactSound=true;strike.Landed=true;
                }
                if(!point.Landed || game.IsRelayAirborne) continue;
                // A hit only exists after this individual missile lands, never on its warning.
                float earliest=dt>0f ? Mathf.Clamp01((point.ImpactSeconds-previousTime)/dt) : 0f;
                float collisionFrom=Mathf.Lerp(from,to,earliest),collisionX=Mathf.Lerp(fromX,toX,earliest);
                float front=point.Distance-PointHalfSize-CarHalfLength,back=point.Distance+PointHalfSize+CarHalfLength;
                if(to<front || collisionFrom>back) continue;
                float span=Mathf.Max(.0001f,to-collisionFrom);
                float a=Mathf.Clamp01((front-collisionFrom)/span),b=Mathf.Clamp01((back-collisionFrom)/span);
                float x0=Mathf.Lerp(collisionX,toX,a),x1=Mathf.Lerp(collisionX,toX,b);
                float half=PointHalfSize+CarHalfWidth;
                if(Mathf.Min(x0,x1)<=point.Lateral+half && Mathf.Max(x0,x1)>=point.Lateral-half) collision=true;
            }
            // Bound overlapping sound voices while retaining the staggered rhythm.
            if(launchSound) audioSource.PlayOneShot(shotClip,.14f*WorldShiftAudioSettings.Effects);
            if(impactSound) audioSource.PlayOneShot(impactClip,.20f*WorldShiftAudioSettings.Effects);
            return collision;
        }

        public bool SafePocket(float at)
        {
            int index=slots.IndexOf(at);
            float end=index>=0 ? game.Course.AirStrikeEnds[index] : at;
            foreach(var jump in game.Course.Ramps)
                if(end+StrikeHalfDepth>=jump.Ramp-55f && at-StrikeHalfDepth<=jump.End+35f) return false;
            foreach(var checkpoint in game.Checkpoints)
                if(checkpoint.Distance>at-75f && checkpoint.Distance<end+75f) return false;
            foreach(var item in game.Collection.Items) if(item.x>at-18f && item.x<end+18f) return false;
            foreach(var h in game.Hazards)
            {
                h.BoundsAt(at,out float h0,out float h1);
                h.BoundsAt(end,out float h2,out float h3);
                if(Mathf.Max(h1,h3)<at-18f || Mathf.Min(h0,h2)>end+18f) continue;
                if(index<0 || game.Course.AirStrikeCurtains[index]
                    || !NightPassage(h,at,end,out _,out _,out _)) return false;
            }
            return true;
        }

        public void Restore(float distance)
        {
            strikes.Clear();pressureStrikes.Clear();pressureCooldown=0f;pressureResumeAt=distance+15f;PressureShotsFired=0;
            active=null;nextSlot=0;MotionTime=0;
            if(distance<=0f) savedFinalSalvo=false;
            FinalSalvoSent=savedFinalSalvo || Defeated;
            while(nextSlot<slots.Count && slots[nextSlot]<distance+60f) nextSlot++;
            shownHits=Hits;shotAge=10;hitSound=true;crashAge=Defeated?5:0;
            ResetDefeatEffects();ResetDolphins(distance);
            audioSource.Stop();Present();
        }
        private void LateUpdate() => Present();
    }
}
