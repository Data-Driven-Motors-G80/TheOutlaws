using UnityEngine;

namespace Outlaws.WorldShift
{
    // Short, bounded effects share the stylized mesh/material language of the coast.
    // All clocks come from simulation, so Pause/retry cannot leave live missiles behind.
    public sealed partial class WorldShiftOceanHelicopter
    {
        private Transform muzzleFlash, hitBurst, hitSparks, policeMuzzleFlash;
        private sealed class StrikeView
        {
            public Transform Root, Marker, Wreck, Beam, Shock, Fragments, Dust, Scorch, EscapeLane;
            public readonly WorldShiftRetractableBarrier[] DayBarriers=new WorldShiftRetractableBarrier[2];
            public readonly Transform[] Missile=new Transform[MaximumMissiles], Flame=new Transform[MaximumMissiles], Trail=new Transform[MaximumMissiles], Blast=new Transform[MaximumMissiles], Reticle=new Transform[MaximumMissiles];
            public readonly Transform[] PointMarker=new Transform[MaximumMissiles], PointWreck=new Transform[MaximumMissiles];
            public Material Warning;
        }
        private readonly StrikeView[] strikeViews=new StrikeView[3];
        private Material fireMaterial, hitMaterial, trailMaterial;
        private AudioClip hitClip, lockClip;
        private Vector3 launcherRest;
        private Transform playerExhaust;
        private readonly Transform[] playerTrail=new Transform[14];

        private void BuildPlayerRocket()
        {
            var model=new WorldShiftArtMesh();
            Color shell=new Color(.91f,.94f,.93f),metal=new Color(.12f,.24f,.3f),accent=new Color(1,.61f,.16f);
            model.ChamferBox(Vector3.zero,new Vector3(.42f,.42f,1.6f),.12f,shell);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;
                model.Triangle(new Vector3(Mathf.Cos(a)*.21f,Mathf.Sin(a)*.21f,.78f),
                    new Vector3(Mathf.Cos(b)*.21f,Mathf.Sin(b)*.21f,.78f),Vector3.forward*1.3f,accent,true);
            }
            foreach(float sign in new[]{-1f,1f})
            {
                model.Triangle(new Vector3(sign*.18f,0,-.35f),new Vector3(sign*.66f,0,-.9f),new Vector3(sign*.18f,0,-.78f),metal,true);
                model.Triangle(new Vector3(0,sign*.18f,-.35f),new Vector3(0,sign*.66f,-.9f),new Vector3(0,sign*.18f,-.78f),metal,true);
            }
            model.Box(new Vector3(0,0,-.72f),new Vector3(.46f,.46f,.1f),metal);
            model.Box(new Vector3(0,0,.28f),new Vector3(.44f,.44f,.16f),accent);
            rocket=MeshObject("Auto-guided coastal rocket",model,transform,finish);
            rocket.gameObject.SetActive(false);
        }
        private Vector3 PlayerRocketPoint(float t)
        {
            float travel=t*.45f+t*t*.55f;
            return Vector3.Lerp(playerRocketOrigin,craft.position,travel)
                +Vector3.up*(Mathf.Sin(t*Mathf.PI)*7f);
        }

        private void BuildCombatEffects()
        {
            var airframe=Mat("Helicopter damage flash",Color.white,.15f);
            foreach(var renderer in craft.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterial==finish) renderer.sharedMaterial=airframe;
            fireMaterial=Mat("Rocket exhaust and blast",new Color(1f,.46f,.07f),1);
            hitMaterial=Mat("Aircraft impact sparks",new Color(1f,.88f,.4f),1);
            trailMaterial=Mat("Missile smoke trail",new Color(.72f,.76f,.78f),.25f);
            playerExhaust=EffectRock("Player missile hot exhaust",rocket,fireMaterial);
            playerExhaust.localPosition=Vector3.back*1.05f;
            for(int i=0;i<playerTrail.Length;i++)
                playerTrail[i]=EffectRock("Guided missile smoke "+i,transform,trailMaterial);
            muzzleFlash=EffectRock("Player launcher muzzle flash",turret,hitMaterial);
            policeMuzzleFlash=EffectRock("Police missile launch flash",rightWeapon,fireMaterial);
            policeMuzzleFlash.localPosition=Vector3.forward*1.1f;
            hitBurst=EffectRock("Helicopter rocket impact flash",transform,hitMaterial);
            hitSparks=BuildFragments("Aircraft hit sparks",hitMaterial,12,.12f);
            for(int i=0;i<strikeViews.Length;i++) strikeViews[i]=BuildStrikeView(i);
            launcherRest=launcherBarrel.localPosition;
            // Original synthesized cues; no downloaded audio or large imported assets.
            shotClip=CreateCombatCue("Ocean rocket launch",.3f,0);
            impactClip=CreateCombatCue("Ocean road explosion",.65f,1);
            hitClip=CreateCombatCue("Ocean helicopter metal hit",.3f,2);
            lockClip=CreateCombatCue("Ocean strike lock",.22f,3);
            PresentCombatEffects(false);
        }

        private Transform EffectRock(string name,Transform parent,Material material)
        {
            var mesh=new WorldShiftArtMesh();mesh.Rock(Vector3.zero,Vector3.one,Color.white,13);
            return MeshObject(name,mesh,parent,material);
        }
        private Transform BuildFragments(string name,Material material,int count,float size)
        {
            var root=new GameObject(name).transform;root.SetParent(transform,false);
            for(int i=0;i<count;i++)
            {
                var part=EffectRock(name+" "+i,root,material);
                float angle=i*2.39996f;
                part.localPosition=new Vector3(Mathf.Cos(angle)*(1+i%3*.3f),.5f+(i%4)*.5f,Mathf.Sin(angle)*(1+i%3*.3f));
                part.localScale=Vector3.one*size;
            }
            return root;
        }

        private StrikeView BuildStrikeView(int index)
        {
            var view=new StrikeView();
            view.Root=new GameObject("Air strike effects "+index).transform;view.Root.SetParent(transform,false);
            view.Warning=Mat("Air strike danger "+index,new Color(1,.52f,.08f),.9f);
            var warning=new WorldShiftArtMesh();
            foreach(float x in new[]{-1.5f,1.5f}) warning.Box(new Vector3(x,0,0),new Vector3(.1f,.04f,8),Color.white);
            foreach(float z in new[]{-4f,4f}) warning.Box(new Vector3(0,0,z),new Vector3(3,.04f,.1f),Color.white);
            for(int i=-3;i<=3;i++) warning.Beam(new Vector3(-1.2f,0,i-.3f),new Vector3(1.2f,0,i+.3f),.07f,.035f,Color.white);
            view.Marker=MeshObject("Marked blast boundary",warning,view.Root,view.Warning);
            var escape=new WorldShiftArtMesh();
            foreach(float z in new[]{-3f,0f,3f})
            {
                escape.Beam(new Vector3(-.65f,0,z-.7f),new Vector3(0,0,z),.16f,.04f,Color.white);
                escape.Beam(new Vector3(0,0,z),new Vector3(.65f,0,z-.7f),.16f,.04f,Color.white);
            }
            view.EscapeLane=MeshObject("Final salvo open lane",escape,view.Root,Mat("Safe lane arrows "+index,new Color(.15f,1f,.65f),.9f));
            view.Beam=Box("Police targeting beam",view.Root,Vector3.one,Color.white,view.Warning);
            var rubble=new WorldShiftArtMesh();
            for(int i=0;i<7;i++) rubble.ChamferBox(new Vector3((i%2-.5f)*1.2f,.4f,i-3),new Vector3(1.5f,.8f,1.15f),.2f,new Color(.09f,.12f,.15f));
            view.Wreck=MeshObject("Solid blast debris",rubble,view.Root,finish);
            var scar=new WorldShiftArtMesh();scar.Box(Vector3.zero,new Vector3(3,.025f,8),new Color(.06f,.07f,.08f));
            view.Scorch=MeshObject("Scorched footprint",scar,view.Root,finish);
            var ring=new WorldShiftArtMesh();ring.Ring(1,.12f,Color.white);
            view.Shock=MeshObject("Explosion shock ring",ring,view.Root,fireMaterial);
            view.Fragments=BuildFragments("Flying strike fragments",finish,10,.3f);view.Fragments.SetParent(view.Root,false);
            view.Dust=BuildFragments("Blast dust crown",smokeFinish,7,.8f);view.Dust.SetParent(view.Root,false);
            var square=new WorldShiftArtMesh();
            foreach(float edge in new[]{-PointHalfSize,PointHalfSize})
            {
                square.Box(new Vector3(edge,0,0),new Vector3(.08f,.035f,PointHalfSize*2),Color.white);
                square.Box(new Vector3(0,0,edge),new Vector3(PointHalfSize*2,.035f,.08f),Color.white);
            }
            square.Beam(new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,.5f),.07f,.035f,Color.white);
            square.Beam(new Vector3(-.5f,0,.5f),new Vector3(.5f,0,-.5f),.07f,.035f,Color.white);
            var pointRubble=new WorldShiftArtMesh();
            pointRubble.Box(Vector3.zero,new Vector3(1.8f,.035f,1.8f),new Color(.07f,.09f,.11f));
            pointRubble.ChamferBox(new Vector3(-.32f,.24f,.15f),new Vector3(.8f,.48f,1.1f),.13f,new Color(.16f,.19f,.22f));
            pointRubble.ChamferBox(new Vector3(.38f,.16f,-.3f),new Vector3(.65f,.32f,.65f),.12f,new Color(.25f,.15f,.1f));
            for(int i=0;i<view.Missile.Length;i++)
            {
                view.PointMarker[i]=MeshObject("Point strike square "+i,square,view.Root,view.Warning);
                view.PointWreck[i]=MeshObject("Point strike debris "+i,pointRubble,view.Root,finish);
                var missile=new WorldShiftArtMesh();
                missile.ChamferBox(Vector3.zero,new Vector3(.42f,.42f,1.9f),.13f,new Color(.8f,.84f,.87f));
                missile.ChamferBox(new Vector3(0,0,.95f),new Vector3(.3f,.3f,.5f),.12f,new Color(.95f,.3f,.08f));
                missile.Box(new Vector3(0,0,-.65f),new Vector3(1.1f,.08f,.55f),new Color(.12f,.17f,.21f));
                missile.Box(new Vector3(0,0,-.65f),new Vector3(.08f,1.1f,.55f),new Color(.12f,.17f,.21f));
                view.Missile[i]=MeshObject("Police missile "+i,missile,view.Root,finish);
                view.Flame[i]=EffectRock("Rocket exhaust",view.Missile[i],fireMaterial);view.Flame[i].localPosition=Vector3.back*1.2f;
                view.Trail[i]=Box("Missile smoke ribbon",view.Root,Vector3.one,Color.white,trailMaterial);
                view.Blast[i]=EffectRock("Missile impact fireball",view.Root,fireMaterial);
                var reticle=new WorldShiftArtMesh();reticle.Ring(1,.08f,Color.white);
                view.Reticle[i]=MeshObject("Locking reticle "+i,reticle,view.Root,view.Warning);
            }
            for(int i=0;i<2;i++)
                view.DayBarriers[i]=WorldShiftRetractableBarrier.Build(view.Root,Vector3.up*.9f,
                    new Vector3(3.05f,1.8f,StrikeHalfDepth*2f),ShiftWorld.Day,ShiftLevel.Ocean);
            view.Root.gameObject.SetActive(false);return view;
        }
        private static Vector3 MissilePoint(Vector3 origin,float t,Vector3 destination)
            => Vector3.Lerp(origin,destination,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*4);

        private void PresentCombatEffects(bool visible)
        {
            if(strikeViews[0]==null) return;
            float hitAge=shotAge-RocketFlightSeconds;
            float recoil=Mathf.Clamp01(1-shotAge/.22f);
            launcherBarrel.localPosition=launcherRest-Vector3.forward*(recoil*.22f);
            muzzleFlash.gameObject.SetActive(visible && shotAge<.12f);
            muzzleFlash.position=launcherMuzzle.position;
            muzzleFlash.localScale=new Vector3(.4f,.4f,.65f)*(1+recoil);
            bool hit=visible && hitAge>=0 && hitAge<.5f;
            hitBurst.gameObject.SetActive(hit);hitSparks.gameObject.SetActive(hit);
            hitBurst.position=craft.position+craft.forward*1.6f;
            hitBurst.localScale=Vector3.one*(Mathf.Sin(Mathf.Clamp01(hitAge/.5f)*Mathf.PI)*1.8f+.1f);
            hitSparks.position=hitBurst.position;
            hitSparks.localScale=Vector3.one*(.2f+Mathf.Max(0,hitAge)*5);
            if(hit) craft.rotation*=Quaternion.Euler(Mathf.Sin(hitAge*45)*4,0,Mathf.Sin(hitAge*35)*7*(1-hitAge*2));
            aircraftRenderer.sharedMaterial.SetColor("_BaseColor",hitAge>=0 && hitAge<.12f ? new Color(1.7f,1.35f,.9f):Color.white);
            bool fired=visible && shotAge<RocketFlightSeconds;
            rocket.gameObject.SetActive(fired);
            if(fired)
            {
                float t=shotAge/RocketFlightSeconds;
                Vector3 pos=PlayerRocketPoint(t);
                Vector3 tangent=PlayerRocketPoint(Mathf.Min(1f,t+.012f))-PlayerRocketPoint(Mathf.Max(0,t-.012f));
                rocket.SetPositionAndRotation(pos,Quaternion.LookRotation(tangent.sqrMagnitude>.00001f?tangent:craft.forward));
                playerExhaust.localScale=new Vector3(.23f,.23f,.85f)*(1+.12f*Mathf.Sin(shotAge*45));
            }
            for(int i=0;i<playerTrail.Length;i++)
            {
                float t=shotAge/RocketFlightSeconds-(i+1)*.022f;
                bool smoke=visible&&shotAge<RocketFlightSeconds&&t>=0;
                playerTrail[i].gameObject.SetActive(smoke);
                if(!smoke) continue;
                playerTrail[i].position=PlayerRocketPoint(t);
                playerTrail[i].localScale=Vector3.one*(.09f+i*.018f);
            }
            bool muzzle=false;
            if(visible && active!=null)
                for(int i=0;i<active.MissileCount;i++)
                    muzzle|=active.Time>=active.LaunchAt(i) && active.Time<active.LaunchAt(i)+.11f;
            policeMuzzleFlash.gameObject.SetActive(muzzle);policeMuzzleFlash.localScale=new Vector3(.6f,.6f,.9f);
            for(int i=0;i<strikeViews.Length;i++)
            {
                var view=strikeViews[i];bool show=visible && i<strikes.Count;
                view.Root.gameObject.SetActive(show);
                if(show) PresentStrike(view,strikes[i]);
            }
        }
        private void PresentStrike(StrikeView view,AirStrike strike)
        {
            var road=game.Route.Sample(strike.Distance);
            Vector3 at=road.Position+road.Right*strike.Lateral+road.Up*.1f;
            Quaternion facing=Quaternion.LookRotation(road.Forward,road.Up);
            for(int i=0;i<2;i++)
            {
                float x=(i==0?-1:1)*WorldShiftLevel.RoadWidth/3f;
                var barrier=view.DayBarriers[i];
                bool used=strike.Combo&&(strike.Pincer||Mathf.Abs(x-strike.BlockLateral)<.1f);
                barrier.gameObject.SetActive(used);
                barrier.SetRaised(game.World==ShiftWorld.Day);
                var edge=game.Route.Sample(strike.Distance);
                barrier.transform.SetPositionAndRotation(edge.Position+edge.Right*x,facing);
            }
            bool points=strike.IsPointSalvo;
            view.Marker.gameObject.SetActive(!points);
            for(int i=0;i<view.PointMarker.Length;i++)
            {
                view.PointMarker[i].gameObject.SetActive(points && i<strike.MissileCount);
                view.PointWreck[i].gameObject.SetActive(false);
            }
            if(points) { PresentPoints(view,strike);return; }
            float width=strike.HalfWidth*2f, depth=StrikeHalfDepth*2f;
            Vector3 footprint=new Vector3(width/3f,1,depth/8f);
            bool impact=strike.Landed,flying=strike.Launched&&!impact;
            view.Warning.SetColor("_BaseColor",impact?new Color(1,.18f,.04f):game.World==ShiftWorld.Echo?new Color(.2f,.8f,1):Color.Lerp(new Color(1,.7f,.1f),new Color(1,.15f,.03f),Mathf.Clamp01(strike.Time/strike.ImpactSeconds)));
            view.Marker.SetPositionAndRotation(at,facing);view.Marker.localScale=footprint;
            view.Wreck.gameObject.SetActive(impact);view.Wreck.SetPositionAndRotation(at,facing);view.Wreck.localScale=footprint;
            view.Scorch.gameObject.SetActive(impact);view.Scorch.SetPositionAndRotation(at-road.Up*.04f,facing);view.Scorch.localScale=footprint;
            view.Beam.gameObject.SetActive(!impact);
            Vector3 delta=at-craft.position;
            view.Beam.SetPositionAndRotation((at+craft.position)*.5f,Quaternion.LookRotation(delta));view.Beam.localScale=new Vector3(.055f,.055f,delta.magnitude);
            if(strike==active&&!impact) rightWeapon.rotation=Quaternion.LookRotation(at-rightWeapon.position,road.Up);
            bool cluster=strike.Kind==OceanAirAttackKind.ClusterSalvo;
            view.EscapeLane.gameObject.SetActive(cluster);
            view.EscapeLane.SetPositionAndRotation(road.Position+road.Right*(-Mathf.Sign(strike.Lateral)*WorldShiftLevel.RoadWidth/3f)+road.Up*.13f,facing);
            for(int i=0;i<view.Missile.Length;i++)
            {
                bool used=i<strike.MissileCount;
                Vector3 dest=at+(strike.Kind!=OceanAirAttackKind.Missile?road.Right*(i%2==0?-1:1)*WorldShiftLevel.RoadWidth/6f:Vector3.zero)
                    +(cluster?road.Forward*((i/2-1)*3f):Vector3.zero);
                bool inFlight=used&&flying&&strike.Time>=strike.LaunchAt(i);
                view.Missile[i].gameObject.SetActive(inFlight);view.Trail[i].gameObject.SetActive(inFlight);
                view.Reticle[i].gameObject.SetActive(used&&!impact);
                view.Reticle[i].SetPositionAndRotation(dest+road.Up*.02f,facing);
                view.Reticle[i].localScale=Vector3.one*(1.35f+.12f*Mathf.Sin(strike.Time*16));
                if(inFlight)
                {
                    float t=Mathf.InverseLerp(strike.LaunchAt(i),strike.ImpactSeconds,strike.Time);
                    Vector3 origin=strike.Origin+road.Right*(i%2==0?-.5f:.5f)+Vector3.up*(cluster?i/2*.3f:0);
                    Vector3 pos=MissilePoint(origin,t,dest),previous=MissilePoint(origin,Mathf.Max(0,t-.12f),dest);
                    view.Missile[i].SetPositionAndRotation(pos,Quaternion.LookRotation(MissilePoint(origin,Mathf.Min(1,t+.01f),dest)-pos));
                    view.Flame[i].localScale=new Vector3(.28f,.28f,.9f)*(1+.15f*Mathf.Sin(strike.Time*40));
                    Vector3 trail=pos-previous;
                    view.Trail[i].SetPositionAndRotation((pos+previous)*.5f,Quaternion.LookRotation(trail.sqrMagnitude<.00001f?Vector3.forward:trail));
                    view.Trail[i].localScale=new Vector3(.17f,.17f,trail.magnitude);
                }
                view.Blast[i].gameObject.SetActive(used&&impact&&strike.ImpactAge<.7f);
                float f=Mathf.Clamp01(strike.ImpactAge/.7f);
                view.Blast[i].position=dest;
                view.Blast[i].localScale=new Vector3(2,3,3)*(Mathf.Sin(f*Mathf.PI)*1.2f+.05f);
            }
            float age=strike.ImpactAge;
            view.Shock.gameObject.SetActive(impact&&age<.7f);view.Fragments.gameObject.SetActive(impact&&age<.9f);view.Dust.gameObject.SetActive(impact&&age<1.3f);
            view.Shock.SetPositionAndRotation(at+road.Up*.03f,facing);view.Shock.localScale=new Vector3(width*.3f+age*2,1,1+age*4);
            view.Fragments.SetPositionAndRotation(at+road.Up*(age*2-age*age*3),facing);
            view.Fragments.localScale=new Vector3(width/3f,1,1)*(.3f+age*2.2f);
            view.Dust.SetPositionAndRotation(at+road.Up*age*.8f,facing);view.Dust.localScale=new Vector3(width/3f+age,1,1+age*2);
        }

        private void PresentPoints(StrikeView view,AirStrike strike)
        {
            // Each visible square owns its collision, including the slow curtain.
            foreach(var effect in new[]{view.Wreck,view.Scorch,view.Shock,view.Fragments,view.Dust,view.Beam})
                effect.gameObject.SetActive(false);
            bool night=game.World==ShiftWorld.Echo;
            view.Warning.SetColor("_BaseColor",night ? new Color(.2f,.85f,1f) : new Color(1f,.34f,.055f));
            var road=game.Route.Sample(strike.Distance);
            Quaternion facing=Quaternion.LookRotation(road.Forward,road.Up);
            view.EscapeLane.gameObject.SetActive(!strike.Spread && strike.Kind!=OceanAirAttackKind.MissileCurtain);
            view.EscapeLane.SetPositionAndRotation(road.Position+road.Right*strike.EscapeLateral+road.Up*.13f,facing);
            for(int i=0;i<view.Missile.Length;i++)
            {
                bool used=i<strike.Points.Length;
                var point=used?strike.Points[i]:null;
                bool flying=used&&point.Launched&&!point.Landed;
                view.Missile[i].gameObject.SetActive(flying);view.Trail[i].gameObject.SetActive(flying);
                view.Reticle[i].gameObject.SetActive(used&&!point.Landed);
                view.Blast[i].gameObject.SetActive(used&&point.Landed&&point.ImpactAge<.55f);
                if(!used) continue;
                var sample=game.Route.Sample(point.Distance);
                Quaternion rotation=Quaternion.LookRotation(sample.Forward,sample.Up);
                Vector3 dest=sample.Position+sample.Right*point.Lateral+sample.Up*.1f;
                view.PointMarker[i].SetPositionAndRotation(dest,rotation);
                view.PointWreck[i].gameObject.SetActive(point.Landed);
                view.PointWreck[i].SetPositionAndRotation(dest-sample.Up*.03f,rotation);
                view.Reticle[i].SetPositionAndRotation(dest+sample.Up*.02f,rotation);
                // A small shrinking ring shows this square's own countdown.
                float progress=Mathf.Clamp01(strike.Time/point.ImpactSeconds);
                view.Reticle[i].localScale=new Vector3(1,1,1)*Mathf.Lerp(.8f,.22f,progress);
                if(flying)
                {
                    float t=Mathf.InverseLerp(point.LaunchSeconds,point.ImpactSeconds,strike.Time);
                    Vector3 origin=point.Origin+road.Right*((i%2==0?-.5f:.5f))+Vector3.up*(i%3*.25f);
                    Vector3 pos=MissilePoint(origin,t,dest),prev=MissilePoint(origin,Mathf.Max(0,t-.12f),dest);
                    Vector3 tangent=MissilePoint(origin,Mathf.Min(1,t+.01f),dest)-prev;
                    view.Missile[i].SetPositionAndRotation(pos,Quaternion.LookRotation(tangent));
                    view.Flame[i].localScale=new Vector3(.28f,.28f,.9f)*(1+.15f*Mathf.Sin(strike.Time*40+i));
                    Vector3 trail=pos-prev;
                    view.Trail[i].SetPositionAndRotation((pos+prev)*.5f,Quaternion.LookRotation(trail.sqrMagnitude>.00001f?trail:sample.Forward));
                    view.Trail[i].localScale=new Vector3(.14f,.14f,trail.magnitude);
                }
                float age=Mathf.Clamp01(point.ImpactAge/.55f);
                view.Blast[i].position=dest;
                view.Blast[i].localScale=new Vector3(1.65f,2.5f,1.65f)*(Mathf.Sin(age*Mathf.PI)+.03f);
            }
            if(strike==active && !strike.Landed)
            {
                var point=strike.Points[0];var sample=game.Route.Sample(point.Distance);
                rightWeapon.rotation=Quaternion.LookRotation(sample.Position+sample.Right*point.Lateral-rightWeapon.position,road.Up);
            }
        }

        private void StepCargoAnimations(float dt)
        {
            for(int i=0;i<strikeViews.Length;i++)
            {
                var view=strikeViews[i];if(view==null) continue;
                foreach(var barrier in view.DayBarriers)
                {
                    barrier.SetRaised(game.World==ShiftWorld.Day);
                    barrier.Step(dt);
                }
            }
        }

        private static AudioClip CreateCombatCue(string name,float duration,int kind)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(duration*rate)];uint random=19721;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,u=t/duration;
                random=1664525*random+1013904223;float noise=(random>>8)/8388608f-1;
                float value;
                if(kind==0) value=noise*.5f+Mathf.Sin(2*Mathf.PI*(260*t-170*t*t))*.25f;
                else if(kind==1) value=noise*.45f+Mathf.Sin(2*Mathf.PI*65*t)*.4f;
                else if(kind==4) value=noise*(.25f+.35f*Mathf.Sin(t*7)*Mathf.Sin(t*7))+Mathf.Sin(2*Mathf.PI*(48*t-12*t*t))*.35f;
                else if(kind==2) value=noise*.25f+Mathf.Sin(2*Mathf.PI*920*t)*.3f+Mathf.Sin(2*Mathf.PI*1370*t)*.2f;
                else value=Mathf.Sin(2*Mathf.PI*720*t)*.35f*(Mathf.Sin(t*80)>0?1:0);
                samples[i]=value*Mathf.Min(1,t*200)*Mathf.Pow(1-u,kind==1?2:3);
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private void DestroyCombatEffects()
        {
            foreach(var clip in new[]{shotClip,impactClip,hitClip,lockClip,defeatClip,seaCrashClip}) if(clip!=null) Destroy(clip);
        }
    }
}
