using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Actual oncoming traffic, facing against travel. Rendering never changes its collision box.</summary>
    public sealed partial class WorldShiftRoadVehicleArt : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Transform> wheels = new List<Transform>();
        private const float TyreRadius = .39f;
        private Material leftHeadlight, rightHeadlight;
        private WorldShiftCity warningGame;
        private ShiftHazard warningHazard;
        private AudioSource hornSource;
        private static AudioClip hornClip;
        private bool hornPlayed;

        public void ConfigureWarning(WorldShiftCity game, ShiftHazard hazard)
        {
            warningGame=game; warningHazard=hazard;
            hornSource=gameObject.AddComponent<AudioSource>();
            hornSource.playOnAwake=false; hornSource.loop=false;
            hornSource.spatialBlend=0f; hornSource.volume=.65f;
            WorldShiftAudioGain.Bind(hornSource,game,.65f);
            if(hornClip==null)
            {
                const int rate=22050;
                var samples=new float[(int)(rate*.65f)];
                for(int i=0;i<samples.Length;i++)
                {
                    float t=(float)i/rate;
                    float local=t<.28f?t:t-.35f;
                    if(local<0f || local>.25f) continue;
                    float envelope=Mathf.Min(1f,local/.015f)*Mathf.Min(1f,(.25f-local)/.035f);
                    samples[i]=envelope*(Mathf.Sin(2*Mathf.PI*420f*local)*.32f
                        +Mathf.Sin(2*Mathf.PI*510f*local)*.23f+Mathf.Sin(2*Mathf.PI*840f*local)*.08f);
                }
                hornClip=AudioClip.Create("Oncoming van beep beep",samples.Length,1,rate,false);
                hornClip.SetData(samples,0);
            }
            hornSource.clip=hornClip;
        }

        public void ResetWarning()
        {
            hornPlayed=false;
            if(hornSource!=null) hornSource.Stop();
        }

        private void UpdateHorn()
        {
            if(warningGame==null || warningHazard==null || hornSource==null) return;
            bool active=warningGame.State==ShiftRunState.Running && !warningGame.TrainingLessonPaused
                && warningHazard.VisibleIn(warningGame.World)
                && !(warningHazard.SharedAcrossWorlds && warningGame.World==ShiftWorld.Echo);
            warningHazard.BoundsAt(warningGame.Distance,out float start,out float end);
            float ahead=start-warningGame.Distance;
            if(!active || ahead< -5f)
            {
                if(hornSource.isPlaying) hornSource.Stop();
                return;
            }
            if(!hornPlayed && ahead>0f && ahead<=40f)
            {
                hornPlayed=true;
                hornSource.Play();
            }
        }
        private static readonly int TintId = Shader.PropertyToID("_BaseColor");

        public static WorldShiftRoadVehicleArt Build(Transform parent, Vector3 centre, Vector3 size,
            WorldShiftObstacleVisual.Palette palette, ShiftLevel environment, int variant = 0, bool shared = false,
            ShiftWorld world = ShiftWorld.Day)
        {
            var root = new GameObject("Oncoming traffic vehicle");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre + Vector3.up * size.y * (environment==ShiftLevel.Snow?0f:.175f);
            root.transform.localRotation = Quaternion.Euler(0,180,0);
            var art = root.AddComponent<WorldShiftRoadVehicleArt>();
            art.Create(size,palette,environment,variant,shared,world);
            art.SetHeadlightPulse(Time.time);
            return art;
        }

        // Civilian traffic colours: never the player's red, the mover colour, the Day yellow or police black/white.
        private static readonly Color[] CivilianPaint =
        {
            new Color(.16f,.36f,.62f), new Color(.13f,.45f,.33f), new Color(.72f,.74f,.76f), new Color(.92f,.92f,.88f),
            new Color(.40f,.27f,.55f), new Color(.45f,.66f,.82f), new Color(.30f,.33f,.37f), new Color(.62f,.52f,.38f)
        };

        private void Create(Vector3 size, WorldShiftObstacleVisual.Palette palette, ShiftLevel environment, int variant, bool shared, ShiftWorld world)
        {
            if(environment==ShiftLevel.Snow) { CreateSnowplough(size,world);return; }
            // Taller warning silhouette; horizontal gameplay footprint remains unchanged.
            transform.localScale = new Vector3(size.x/3.05f,size.y/1.8f*1.35f,size.z/5.5f);
            var paint=new WorldShiftArtMesh();
            var trim=new WorldShiftArtMesh();
            Color rubber=new Color(.035f,.045f,.052f);
            Color glass=new Color(.055f,.10f,.14f), steel=new Color(.27f,.30f,.33f);
            int style = shared ? 3 : ((variant % 5) + 5) % 5;
            Color body = shared ? WorldShiftLevelProps.SideMoverColour : CivilianPaint[((variant / 5) % CivilianPaint.Length + CivilianPaint.Length) % CivilianPaint.Length];
            Color dark = body*.82f; dark.a=1f;
            // Lower body and bumpers are shared by every style; the cabin shape gives each its silhouette.
            paint.ChamferBox(new Vector3(0,-.22f,0),new Vector3(2.62f,.58f,5.2f),.1f,body);
            switch (style)
            {
                case 0: // Sedan: long hood and boot, low cabin in the middle.
                    paint.ChamferBox(new Vector3(0,.3f,-.2f),new Vector3(2.3f,.6f,2.4f),.14f,body);
                    trim.Box(new Vector3(0,.32f,1.01f),new Vector3(2.0f,.44f,.035f),glass);
                    break;
                case 1: // Hatchback: short nose, tall rounded cabin to the back.
                    paint.ChamferBox(new Vector3(0,.38f,-.75f),new Vector3(2.35f,.76f,3.4f),.2f,body);
                    trim.Box(new Vector3(0,.42f,.96f),new Vector3(2.05f,.52f,.035f),glass);
                    break;
                case 2: // Pickup: cab up front, open bed with low walls behind.
                    paint.ChamferBox(new Vector3(0,.36f,.75f),new Vector3(2.4f,.72f,1.7f),.14f,body);
                    trim.Box(new Vector3(0,.4f,1.61f),new Vector3(2.05f,.5f,.035f),glass);
                    foreach(float side in new[] {-1f,1f})
                        paint.Box(new Vector3(side*1.22f,.18f,-1.25f),new Vector3(.12f,.32f,2.5f),dark);
                    paint.Box(new Vector3(0,.18f,-2.48f),new Vector3(2.5f,.32f,.12f),dark);
                    trim.Box(new Vector3(0,.04f,-1.25f),new Vector3(2.3f,.04f,2.4f),rubber);
                    break;
                case 3: // Box van: tall load box behind a short cab.
                    paint.ChamferBox(new Vector3(0,.42f,-.65f),new Vector3(2.5f,.94f,3.85f),.12f,body);
                    paint.ChamferBox(new Vector3(0,.05f,1.94f),new Vector3(2.52f,.35f,1.26f),.08f,body);
                    trim.Box(new Vector3(0,.49f,1.289f),new Vector3(2.13f,.58f,.035f),glass);
                    break;
                default: // SUV: tall, squared cabin with roof rails.
                    paint.ChamferBox(new Vector3(0,.4f,-.35f),new Vector3(2.45f,.84f,3.3f),.12f,body);
                    trim.Box(new Vector3(0,.44f,1.31f),new Vector3(2.1f,.56f,.035f),glass);
                    foreach(float side in new[] {-1f,1f})
                        trim.Box(new Vector3(side*.95f,.86f,-.35f),new Vector3(.06f,.06f,2.8f),steel);
                    break;
            }
            foreach(float side in new[] {-1f,1f})
            {
                // Side windows along whichever cabin the style uses.
                trim.Box(new Vector3(side*1.2f,.38f,style==2?.75f:style==0?-.2f:-.5f),new Vector3(.05f,.32f,style==2?1.2f:2.0f),glass);
                trim.ChamferBox(new Vector3(side*1.36f,.24f,style==2?1.4f:.9f),new Vector3(.15f,.2f,.26f),.03f,rubber);
                foreach(float z in new[] {-1.7f,1.7f})
                {
                    trim.Box(new Vector3(side*1.3f,-.23f,z),new Vector3(.18f,.16f,1.04f),rubber);
                    BuildWheel(new Vector3(side*1.37f,-.51f,z),side,rubber,steel,new Color(.6f,.63f,.65f));
                }
                trim.Box(new Vector3(side*.94f,-.06f,2.61f),new Vector3(.88f,.40f,.1f),rubber);
                // Large white front lenses remain readable at the game's low rendering resolution.
                var lens = new WorldShiftArtMesh();
                lens.ChamferBox(new Vector3(side*.94f,-.06f,2.68f),new Vector3(.7f,.32f,.035f),.025f,Color.white);
                var lamp = OwnMaterial(side < 0 ? "Left flashing headlight" : "Right flashing headlight",true);
                Draw(side < 0 ? "Left warning headlight" : "Right warning headlight",lens,lamp);
                if(side < 0) leftHeadlight=lamp; else rightHeadlight=lamp;
            }
            trim.Box(new Vector3(0,-.41f,2.66f),new Vector3(2.62f,.23f,.15f),rubber);
            trim.Box(new Vector3(0,-.1f,2.62f),new Vector3(.95f,.3f,.07f),rubber);
            for(int i=0;i<3;i++)
                trim.Box(new Vector3(0,-.2f+i*.1f,2.67f),new Vector3(.85f,.03f,.045f),steel);
            var bodyFinish=OwnMaterial(shared ? "Shared mover van paint" : "Traffic car paint",false);
            bodyFinish.SetFloat("_Emission",shared ? .35f : .12f);
            Draw(shared ? "Shared mover van body" : "Traffic car body",paint,bodyFinish);
            Draw("Traffic glass, bumper and trim",trim,OwnMaterial("Traffic trim",false));
            if (!shared) BuildWorldBadge(world);
            BuildDirectionWarnings();
        }

        private void BuildWorldBadge(ShiftWorld world)
        {
            var badge = new WorldShiftArtMesh();
            Color colour = world == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour;
            Color ink = new Color(.035f,.045f,.06f);
            // A broad front-facing roof plate, readable over the hood without hiding the headlights.
            badge.ChamferBox(new Vector3(0,.89f,1.05f),new Vector3(1.42f,1.05f,.13f),.06f,ink);
            badge.ChamferBox(new Vector3(0,.89f,1.13f),new Vector3(1.3f,.93f,.05f),.03f,colour);
            WorldShiftRetractableBarrier.Icon(badge,new Vector3(0,.89f,1.161f),.36f,world,ink);
            Draw(world + " traffic sun-moon badge",badge,OwnMaterial("Traffic world symbol",true));
        }

        private void BuildDirectionWarnings()
        {
            // Large road chevrons point along local +Z (toward the approaching player).
            // No hovering marker: keep the skyline and tutorial signs unobstructed.
            var warning = new WorldShiftArtMesh();
            foreach(float tip in new[] {7f, 12f, 17f})
            {
                Chevron(warning,-.875f,1.46f,tip,3.3f,1.85f,new Color(.025f,.03f,.04f));
                Chevron(warning,-.865f,1.29f,tip-.18f,2.95f,1.35f,Color.white);
            }
            Draw("Oncoming road direction warnings",warning,OwnMaterial("Road warning arrows",true));
        }

        private void Update()
        {
            SetHeadlightPulse(Time.time);
            UpdateSnowBeacon();
            UpdateHorn();
        }
        private void OnDisable()
        {
            if(hornSource!=null) hornSource.Stop();
        }

        private void SetHeadlightPulse(float seconds)
        {
            // One gentle alternating cycle per second; both lamps remain visible between peaks.
            float pulse=.5f+.5f*Mathf.Cos(seconds*Mathf.PI*2f);
            if(leftHeadlight != null) leftHeadlight.SetColor(TintId,Color.Lerp(new Color(.4f,.4f,.36f),new Color(2.5f,2.5f,2.3f),pulse));
            if(rightHeadlight != null) rightHeadlight.SetColor(TintId,Color.Lerp(new Color(.4f,.4f,.36f),new Color(2.5f,2.5f,2.3f),1f-pulse));
        }

        private static void Chevron(WorldShiftArtMesh mesh,float y,float halfWidth,float tip,
            float depth,float thickness,Color colour)
        {
            Vector3 P(float x,float z)=>new Vector3(x,y,z);
            Vector3 left=P(-halfWidth,tip-depth), point=P(0,tip), right=P(halfWidth,tip-depth);
            Vector3 innerLeft=P(-halfWidth,tip-depth-thickness), innerPoint=P(0,tip-thickness),
                innerRight=P(halfWidth,tip-depth-thickness);
            mesh.Triangle(left,point,innerPoint,colour,true);
            mesh.Triangle(left,innerPoint,innerLeft,colour,true);
            mesh.Triangle(point,right,innerRight,colour,true);
            mesh.Triangle(point,innerRight,innerPoint,colour,true);
        }

        private void BuildWheel(Vector3 centre,float side,Color rubber,Color steel,Color silver)
        {
            var mesh = new WorldShiftArtMesh();
            mesh.Wheel(Vector3.zero,TyreRadius,.22f,rubber);
            Vector3 face=Vector3.right*(side*.12f);
            mesh.Wheel(face,.25f,.025f,steel);
            mesh.Wheel(face+Vector3.right*(side*.016f),.10f,.025f,silver);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3f;
                Vector3 outer=face+new Vector3(side*.025f,Mathf.Cos(a)*.22f,Mathf.Sin(a)*.22f);
                mesh.Beam(face+Vector3.right*side*.025f,outer,.045f,.045f,silver);
            }
            var wheel=Draw("Traffic wheel and six-spoke rim",mesh,OwnMaterial("Traffic wheel finish",false)).transform;
            wheel.localPosition=centre; wheels.Add(wheel);
        }

        private Material OwnMaterial(string label,bool glow)
        {
            var m=new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) {name=label};
            m.SetFloat("_Emission",glow ? 1f : 0f); materials.Add(m);return m;
        }
        private GameObject Draw(string label,WorldShiftArtMesh geometry,Material material)
        {
            var obj=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));
            obj.transform.SetParent(transform,false);
            Mesh mesh=geometry.Mesh(label);meshes.Add(mesh);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            return obj;
        }
        public void SetTravel(float metres)
        {
            foreach(var wheel in wheels) wheel.localRotation=Quaternion.AngleAxis(metres/TyreRadius*Mathf.Rad2Deg,Vector3.right);
        }
        private void OnDestroy()
        {
            foreach(var mesh in meshes) Release(mesh);
            foreach(var material in materials) Release(material);
        }
        private static void Release(Object resource)
        {
            if(Application.isPlaying) Destroy(resource);else DestroyImmediate(resource);
        }
    }
}
