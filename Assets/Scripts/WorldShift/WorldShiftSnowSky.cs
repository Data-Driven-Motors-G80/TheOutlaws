using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Snow-only background art. No colliders, lights, audio or changes to driving.</summary>
    public sealed class WorldShiftSnowSky : MonoBehaviour
    {
        public const float FlyoverSeconds = 14f;
        private WorldShiftCity game;
        private Camera view;
        private Material sky, figureFinish;
        private Mesh figureMesh;
        private Transform santa;
        private float skyTime, flyoverTime;
        private bool flyoverStarted;
        public bool FlyoverActive => flyoverStarted && flyoverTime < FlyoverSeconds;

        public void Bind(WorldShiftCity controller,Camera camera,Material skyMaterial)
        {
            game=controller;view=camera;sky=skyMaterial;
            sky.SetFloat("_Snow",1f);
            // Every Snow run gets one flyover; checkpoint retries never repeat it.
            BuildSanta();santa.gameObject.SetActive(false);
        }

        private void BuildSanta()
        {
            var art=new WorldShiftArtMesh();
            Color red=new Color(.68f,.06f,.08f),cream=new Color(.96f,.94f,.83f);
            Color gold=new Color(.88f,.62f,.18f),bark=new Color(.25f,.14f,.08f);
            art.ChamferBox(new Vector3(-5,-.15f,0),new Vector3(3.8f,.75f,1.5f),.15f,red);
            art.Box(new Vector3(-6.5f,.55f,0),new Vector3(.30f,1.3f,1.5f),red);
            foreach(float side in new[]{-1f,1f})
            {
                art.Beam(new Vector3(-7,-.8f,side*.70f),new Vector3(-2.6f,-.8f,side*.70f),.10f,.10f,gold);
                art.Beam(new Vector3(-2.6f,-.8f,side*.70f),new Vector3(-2.2f,-.35f,side*.70f),.10f,.10f,gold);
                art.Beam(new Vector3(-6,-.35f,side*.60f),new Vector3(-6,-.78f,side*.70f),.10f,.10f,gold);
            }
            // Red coat, white beard and bent hat read from the side at Easter-egg scale.
            art.ChamferBox(new Vector3(-4.4f,.75f,0),new Vector3(.9f,1.15f,.9f),.16f,red);
            art.Rock(new Vector3(-4.25f,1.25f,0),new Vector3(.36f,.6f,.38f),new Color(.89f,.66f,.49f),5);
            art.Rock(new Vector3(-3.92f,1.05f,0),new Vector3(.26f,.55f,.39f),cream,6);
            art.Cylinder(new Vector3(-4.25f,1.8f,0),.39f,.08f,.72f,red,10);
            art.Rock(new Vector3(-4.05f,2.38f,0),new Vector3(.17f,.23f,.17f),cream,8);
            art.Rock(new Vector3(-5.7f,.25f,0),new Vector3(.65f,1.35f,.55f),new Color(.35f,.27f,.12f),14);
            for(int deer=0;deer<3;deer++)
            {
                float x=deer*3.8f;
                art.ChamferBox(new Vector3(x,.45f,0),new Vector3(1.65f,.70f,.62f),.13f,bark);
                art.Beam(new Vector3(x+.55f,.60f,0),new Vector3(x+.9f,1.35f,0),.36f,.36f,bark);
                art.ChamferBox(new Vector3(x+1.1f,1.35f,0),new Vector3(.76f,.40f,.44f),.08f,bark);
                foreach(float side in new[]{-1f,1f})
                {
                    art.Beam(new Vector3(x-.60f,.24f,side*.22f),new Vector3(x-1.2f,-.6f,side*.30f),.12f,.12f,bark);
                    art.Beam(new Vector3(x+.55f,.24f,side*.22f),new Vector3(x+.15f,-.45f,side*.30f),.12f,.12f,bark);
                    art.Beam(new Vector3(x+.88f,1.50f,side*.18f),new Vector3(x+.55f,2.12f,side*.45f),.07f,.07f,gold);
                    art.Beam(new Vector3(x+.70f,1.85f,side*.32f),new Vector3(x+1.05f,2.08f,side*.55f),.06f,.06f,gold);
                    art.Beam(new Vector3(-3.6f,.70f,side*.4f),new Vector3(x+.4f,.70f,side*.4f),.025f,.025f,gold);
                }
                art.Box(new Vector3(x,.48f,0),new Vector3(.10f,.75f,.67f),red);
            }
            for(int i=0;i<12;i++)
            {
                float x=-8-i*.85f;
                art.Rock(new Vector3(x,Mathf.Sin(i*1.7f)*.4f,Mathf.Cos(i)*.35f),
                    Vector3.one*(.12f+(i%3)*.04f),new Color(.85f,.76f,.48f,1-i/15f),i);
            }
            figureMesh=art.Mesh("Santa sleigh, three reindeer and faint star trail");
            figureFinish=new Material(Resources.Load<Shader>("WorldShiftSkyArt")){name="Snow Santa flyover"};
            var root=new GameObject("Santa sky Easter egg",typeof(MeshFilter),typeof(MeshRenderer));
            root.transform.SetParent(transform,false);santa=root.transform;
            santa.localScale=Vector3.one*2.4f;
            root.GetComponent<MeshFilter>().sharedMesh=figureMesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=figureFinish;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private bool SafeToStart()
        {
            if(game.OnIce || game.IsRelayAirborne) return false;
            float d=game.Distance;
            foreach(var jump in game.RelayLayout)
                if(d>=jump.Ramp-95f && d<jump.End+5f) return false;
            foreach(var hazard in game.Hazards)
            {
                hazard.BoundsAt(d,out float start,out float end);
                if(start<d+35f && end>d-5f) return false;
            }
            // The opening is clear dry road. Don't postpone the flyover for its gentle first bend.
            if(d<20f) return true;
            var a=game.Route.Sample(d);var b=game.Route.Sample(d+12f);
            return Vector3.Angle(a.Forward,b.Forward)<5f;
        }

        public void Advance(float delta)
        {
            if(game==null || WorldShiftSceneLoading.Active || game.State!=ShiftRunState.Running || game.TrainingLessonPaused)
            {
                if(game!=null && (game.State==ShiftRunState.Won || game.State==ShiftRunState.Lost)) santa.gameObject.SetActive(false);
                return;
            }
            float dt=Mathf.Max(0,delta);skyTime+=dt;sky.SetFloat("_SkyTime",skyTime);
            figureFinish.SetFloat("_Night",sky.GetFloat("_Night"));
            if(!flyoverStarted && game.Distance>=4f && SafeToStart())
            {flyoverStarted=true;flyoverTime=0;santa.gameObject.SetActive(true);}
            if(!FlyoverActive) {santa.gameObject.SetActive(false);return;}
            flyoverTime=Mathf.Min(FlyoverSeconds,flyoverTime+dt);
            PoseSanta();
            if(!FlyoverActive) santa.gameObject.SetActive(false);
        }

        private void PoseSanta()
        {
            float t=flyoverTime/FlyoverSeconds;
            Vector3 forward=Vector3.ProjectOnPlane(game.Route.Sample(game.Distance).Forward,Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            santa.SetPositionAndRotation(view.transform.position+forward*115f+right*Mathf.Lerp(-145f,145f,t)
                +Vector3.up*(32f+Mathf.Sin(t*Mathf.PI)*3f),Quaternion.LookRotation(forward,Vector3.up));
            figureFinish.SetFloat("_Opacity",Mathf.SmoothStep(0,1,t/.10f)*(1-Mathf.SmoothStep(0,1,(t-.90f)/.10f)));
        }

        private void Update() => Advance(Time.deltaTime);
        private void LateUpdate() { if(FlyoverActive && game!=null && game.State==ShiftRunState.Running) PoseSanta(); }

        private void OnDestroy()
        {
            if(sky!=null) {sky.SetFloat("_Snow",0);sky.SetFloat("_SkyTime",0);}
            if(figureMesh!=null) Destroy(figureMesh);
            if(figureFinish!=null) Destroy(figureFinish);
        }
    }
}
