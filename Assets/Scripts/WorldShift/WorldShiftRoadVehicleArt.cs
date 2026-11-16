using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Actual oncoming traffic, facing against travel. Rendering never changes its collision box.</summary>
    public sealed class WorldShiftRoadVehicleArt : MonoBehaviour
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
            WorldShiftObstacleVisual.Palette palette, ShiftLevel environment)
        {
            var root = new GameObject("Oncoming traffic vehicle");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre + Vector3.up * size.y * .175f;
            root.transform.localRotation = Quaternion.Euler(0,180,0);
            var art = root.AddComponent<WorldShiftRoadVehicleArt>();
            art.Create(size,palette,environment);
            art.SetHeadlightPulse(Time.time);
            return art;
        }

        private void Create(Vector3 size, WorldShiftObstacleVisual.Palette palette, ShiftLevel environment)
        {
            // Taller warning silhouette; horizontal gameplay footprint remains unchanged.
            transform.localScale = new Vector3(size.x/3.05f,size.y/1.8f*1.35f,size.z/5.5f);
            var paint=new WorldShiftArtMesh();
            var trim=new WorldShiftArtMesh();
            var markings=new WorldShiftArtMesh();
            Color red=WorldShiftLevelProps.MoverColour, rubber=new Color(.035f,.045f,.052f);
            Color glass=new Color(.055f,.10f,.14f), steel=new Color(.27f,.30f,.33f);
            paint.ChamferBox(new Vector3(0,-.22f,0),new Vector3(2.72f,.64f,5.2f),.1f,red);
            paint.ChamferBox(new Vector3(0,.42f,-.65f),new Vector3(2.5f,.94f,3.85f),.12f,red);
            // Broad hood, plain red roof, armored cabin and dark front windscreen.
            paint.ChamferBox(new Vector3(0,.05f,1.94f),new Vector3(2.62f,.35f,1.26f),.08f,red);
            trim.Box(new Vector3(0,.49f,1.289f),new Vector3(2.13f,.58f,.035f),glass);
            trim.Box(new Vector3(0,.49f,1.315f),new Vector3(.07f,.58f,.025f),rubber);
            foreach(float side in new[] {-1f,1f})
            {
                trim.Box(new Vector3(side*1.31f,.4f,.85f),new Vector3(.08f,.45f,.65f),glass);
                trim.ChamferBox(new Vector3(side*1.43f,.32f,1.13f),new Vector3(.17f,.29f,.35f),.035f,rubber);
                foreach(float z in new[] {-1.7f,-.6f})
                {
                    paint.ChamferBox(new Vector3(side*1.26f,.38f,z),new Vector3(.12f,.65f,.9f),.04f,red*.85f);
                    trim.Box(new Vector3(side*1.335f,.42f,z),new Vector3(.035f,.08f,.36f),steel);
                }
                foreach(float z in new[] {-1.7f,1.7f})
                {
                    trim.Box(new Vector3(side*1.33f,-.23f,z),new Vector3(.2f,.16f,1.04f),rubber);
                    BuildWheel(new Vector3(side*1.37f,-.51f,z),side,rubber,steel,new Color(.6f,.63f,.65f));
                }
                trim.Box(new Vector3(side*.94f,-.06f,2.61f),new Vector3(.88f,.50f,.1f),rubber);
                // Large white front lenses remain readable at the game's low rendering resolution.
                var lens = new WorldShiftArtMesh();
                lens.ChamferBox(new Vector3(side*.94f,-.04f,2.68f),
                    new Vector3(.78f,.42f,.035f),.025f,Color.white);
                var lamp = OwnMaterial(side < 0 ? "Left flashing headlight" : "Right flashing headlight",true);
                Draw(side < 0 ? "Left warning headlight" : "Right warning headlight",lens,lamp);
                if(side < 0) leftHeadlight=lamp; else rightHeadlight=lamp;
                trim.Box(new Vector3(side*.59f,-.27f,2.68f),new Vector3(.14f,.62f,.12f),steel);
            }
            trim.Box(new Vector3(0,-.41f,2.66f),new Vector3(2.72f,.23f,.15f),rubber);
            trim.Box(new Vector3(0,-.1f,2.62f),new Vector3(.95f,.4f,.07f),rubber);
            for(int i=0;i<3;i++)
                trim.Box(new Vector3(0,-.22f+i*.12f,2.67f),new Vector3(.85f,.035f,.045f),steel);
            // Only the hood carries an X. The roof stays plain behind the direction cue.
            markings.Beam(new Vector3(-.83f,.235f,1.48f),new Vector3(.83f,.235f,2.4f),.19f,.018f,Color.white);
            markings.Beam(new Vector3(.83f,.235f,1.48f),new Vector3(-.83f,.235f,2.4f),.19f,.018f,Color.white);
            var bodyFinish=OwnMaterial("Red armored van paint",false);
            bodyFinish.SetFloat("_Emission",.35f);
            Draw("Red armored body",paint,bodyFinish);
            Draw("Armored glass, bumper and trim",trim,OwnMaterial("Armored trim",false));
            Draw("White hood X",markings,OwnMaterial("Reflective van markings",true));
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
