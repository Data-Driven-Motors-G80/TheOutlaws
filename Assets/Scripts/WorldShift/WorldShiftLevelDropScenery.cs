using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Collider-free jump dangers shared by Day and Night. The route still owns every fall.</summary>
    public sealed partial class WorldShiftLevelDropScenery : MonoBehaviour
    {
        private sealed class Fin { public Transform Root; public Vector3 Centre; public Quaternion Heading; public float Phase; }
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Fin> fins = new List<Fin>();
        private WorldShiftCity game;
        private WorldShiftRoute route;
        private Material surface, water;
        private Transform burst;
        private float clock, fallAge, burstAge;
        private bool wasFalling, burstStarted;
        private ShiftLevel environment;
        public const float OceanSeaHeight = -10f;

        public void Bind(WorldShiftCity controller, WorldShiftRoute sharedRoute)
        {
            game = controller; route = sharedRoute;
            environment = route.Course.Training ? ShiftLevel.Tutorial : route.Course.Environment;
            surface = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = "Themed jump drop surfaces" };
            surface.SetFloat("_Emission", .12f);
            water = new Material(Resources.Load<Shader>("WorldShiftOceanWater")) { name = "Jump drain and gorge water" };
            water.SetFloat("_ChannelMask", 0f); water.SetFloat("_RippleScale", .45f);
            if (environment == ShiftLevel.City)
            {
                water.SetColor("_DeepColor",new Color(.025f,.10f,.09f));
                water.SetColor("_ShallowColor",new Color(.13f,.28f,.23f));
                water.SetColor("_FoamColor",new Color(.47f,.58f,.48f));
            }
            BuildFallBurst();
        }

        public static string Theme(ShiftLevel level) => level switch
        {
            ShiftLevel.Tutorial => "Depot safety pit",
            ShiftLevel.City => "City storm drain",
            ShiftLevel.Desert => "Sandstone ravine",
            ShiftLevel.Ocean => "Open sea and shark fins",
            ShiftLevel.Snow => "Blue ice crevasse",
            _ => "Mountain misty gorge"
        };

        public void BuildJump(Transform parent, ShiftRelay jump)
        {
            var root = new GameObject(Theme(environment)).transform; root.SetParent(parent, false);
            float low = float.PositiveInfinity;
            for (float d = jump.Ramp; d <= jump.End + .01f; d += 2f) low = Mathf.Min(low, route.Sample(d).Position.y);
            low = Mathf.Min(low, route.Sample(jump.End).Position.y);
            float depth = environment switch { ShiftLevel.Tutorial => 4f, ShiftLevel.City => 7f,
                ShiftLevel.Desert => 11f, ShiftLevel.Snow => 12f, _ => 23f };
            float floor = low - depth;
            var bed = new WorldShiftArtMesh(); var detail = new WorldShiftArtMesh(); var flow = new WorldShiftArtMesh();
            if (environment == ShiftLevel.Mountain) { BuildMountainGorge(root, jump, low); return; }
            if (environment == ShiftLevel.Ocean)
            {
                // Reuse the continuous sea, rather than placing a second water plane over it.
                for (int i = 0; i < 2; i++) BuildShark(root, jump, i);
                for (int i = 0; i < 4; i++)
                {
                    var p = route.Sample(Mathf.Lerp(jump.Takeoff, jump.Landing, (i + .5f) / 4f));
                    Vector3 centre = p.Position + p.Right * (i % 2 == 0 ? -7f : 7f); centre.y = OceanSeaHeight - .6f;
                    bed.Rock(centre, new Vector3(1.3f, 1.2f, 1.8f), new Color(.25f,.36f,.39f), i + jump.Number);
                    Foam(detail, centre + Vector3.up * .65f, 2f, new Color(.65f,.87f,.87f));
                }
            }
            else
            {
                Color wall = environment switch { ShiftLevel.Tutorial => new Color(.27f,.31f,.32f),
                    ShiftLevel.City => new Color(.28f,.34f,.33f), ShiftLevel.Desert => new Color(.65f,.30f,.13f),
                    ShiftLevel.Snow => new Color(.22f,.57f,.73f), _ => new Color(.28f,.31f,.34f) };
                for (float d = jump.Ramp; d < jump.End; d += 4f)
                {
                    float end = Mathf.Min(d + 4f, jump.End);
                    var a = route.Sample(d); var b = route.Sample(end);
                    float half = a.Width * .5f + .65f;
                    Vector3 AL(float x, float y) => new Vector3(a.Position.x, y, a.Position.z) + a.Right * x;
                    Vector3 BL(float x, float y) => new Vector3(b.Position.x, y, b.Position.z) + b.Right * x;
                    // Every vertex stays below the deck; nothing bridges a missing road surface.
                    bed.Quad(AL(-half,floor), BL(-half,floor), BL(half,floor), AL(half,floor), wall * .55f);
                    foreach (float side in new[] {-1f,1f})
                    {
                        float x = side * half;
                        float topA = a.Position.y - 1.4f, topB = b.Position.y - 1.4f;
                        bool rocky = environment == ShiftLevel.Desert || environment == ShiftLevel.Mountain;
                        if (rocky)
                        {
                            float jagA=.4f+Mathf.Abs(Mathf.Sin(d*.73f+side))*1.1f;
                            float jagB=.4f+Mathf.Abs(Mathf.Sin(end*.73f+side))*1.1f;
                            Vector3 midA=AL(x-side*jagA,Mathf.Lerp(floor,topA,.5f));
                            Vector3 midB=BL(x-side*jagB,Mathf.Lerp(floor,topB,.5f));
                            if(side<0)
                            {
                                bed.Quad(AL(x,topA),BL(x,topB),midB,midA,wall);
                                bed.Quad(midA,midB,BL(x,floor),AL(x,floor),wall*.72f);
                            }
                            else
                            {
                                bed.Quad(BL(x,topB),AL(x,topA),midA,midB,wall*.83f);
                                bed.Quad(midB,midA,AL(x,floor),BL(x,floor),wall*.64f);
                            }
                        }
                        else if (side < 0) bed.Quad(AL(x,topA),BL(x,topB),BL(x,floor),AL(x,floor),wall);
                        else bed.Quad(BL(x,topB),AL(x,topA),AL(x,floor),BL(x,floor),wall * .83f);
                        if (environment == ShiftLevel.Snow)
                        {
                            detail.Beam(AL(x,topA-.5f),BL(x,topB-.8f),.35f,.25f,new Color(.78f,.92f,.97f));
                            detail.Rock(AL(x-side*.7f,floor+1f),new Vector3(.65f,4f,1.2f),new Color(.39f,.74f,.87f),Mathf.RoundToInt(d));
                        }
                        if (environment == ShiftLevel.Desert || environment == ShiftLevel.Mountain)
                        {
                            float variation=Mathf.Abs(Mathf.Sin(d*1.73f+side*2f));
                            float height = environment == ShiftLevel.Desert ? 3.2f+variation*5.2f : 5f+variation*10f;
                            detail.Rock(AL(x-side*(.6f+variation*1.5f),floor),new Vector3(.75f+variation*.7f,height,1f+variation),wall * 1.15f,Mathf.RoundToInt(d));
                            detail.Beam(AL(x,topA-2f),BL(x,topB-2.3f),.20f,.28f,wall * 1.35f);
                        }
                        if (environment == ShiftLevel.City)
                        {
                            detail.Beam(AL(x-side*.25f,topA-1.6f),BL(x-side*.25f,topB-1.6f),.38f,.38f,new Color(.37f,.26f,.15f));
                            detail.Beam(AL(x-side*.15f,topA-1.9f),AL(x-side*.15f,topA-1.3f),.53f,.53f,new Color(.20f,.23f,.23f));
                        }
                    }
                    if (environment == ShiftLevel.City || environment == ShiftLevel.Mountain)
                    {
                        float channel = environment == ShiftLevel.City ? half-.2f : 2.2f;
                        flow.Quad(AL(-channel,floor+.18f),BL(-channel,floor+.18f),BL(channel,floor+.18f),AL(channel,floor+.18f),Color.white);
                        detail.Beam(AL(-channel*.4f,floor+.24f),BL(-channel*.35f,floor+.24f),.12f,.035f,new Color(.56f,.77f,.75f));
                    }
                    if (environment == ShiftLevel.Tutorial)
                    {
                        for (float x = -3.6f; x <= 3.6f; x += 2.4f)
                        {
                            Vector3 centre = (AL(x,floor+.45f)+BL(x,floor+.45f))*.5f;
                            detail.ChamferBox(centre,new Vector3(2.25f,.8f,end-d-.15f),.14f,new Color(.91f,.38f,.12f));
                            detail.Beam(centre+Vector3.left*.95f+Vector3.up*.41f,centre+Vector3.right*.95f+Vector3.up*.41f,.035f,.035f,new Color(.98f,.68f,.29f));
                        }
                    }
                }
                // Fine, low cloud ribbons reveal the depth without hiding the touchdown edge.
                if (environment == ShiftLevel.Snow || environment == ShiftLevel.Mountain)
                    for (int i=0;i<9;i++)
                    {
                        var p=route.Sample(Mathf.Lerp(jump.Ramp,jump.End,(i+.5f)/9f));
                        Vector3 at=p.Position;at.y=floor+(environment==ShiftLevel.Snow?2f:9f);
                        detail.Rock(at+p.Right*Mathf.Sin(i*2.3f)*2f,new Vector3(2.4f,.16f,3.2f),
                            environment==ShiftLevel.Snow?new Color(.66f,.80f,.85f):new Color(.48f,.58f,.60f),i);
                    }
            }
            Render(root,"Drop walls and bed",bed,surface);
            Render(root,"Themed drop details",detail,surface);
            if (environment == ShiftLevel.City || environment == ShiftLevel.Mountain) Render(root,"Flowing water below jump",flow,water);
        }

        private void BuildShark(Transform parent, ShiftRelay jump, int index)
        {
            var p=route.Sample(Mathf.Lerp(jump.Takeoff,jump.Landing,index==0?.30f:.72f));
            Vector3 centre=p.Position; centre.y=OceanSeaHeight+.06f;
            var geometry=new WorldShiftArtMesh();
            Color fin=new Color(.15f,.23f,.28f);
            geometry.Triangle(new Vector3(0,0,-1.0f),new Vector3(0,1.15f,-.15f),new Vector3(0,0,.8f),fin,true);
            geometry.Triangle(new Vector3(-.13f,0,-1f),new Vector3(0,1.15f,-.15f),new Vector3(.13f,0,-1f),fin*.8f,true);
            geometry.Rock(new Vector3(0,-.24f,0),new Vector3(.42f,.30f,1.7f),fin*.7f,index);
            Foam(geometry,new Vector3(0,.015f,-1.1f),.55f,new Color(.53f,.77f,.80f));
            Transform root=Render(parent,"Circling shark fin",geometry,surface);
            Quaternion heading=Quaternion.LookRotation(Vector3.ProjectOnPlane(p.Forward,Vector3.up));
            root.SetPositionAndRotation(centre,heading);
            fins.Add(new Fin {Root=root,Centre=centre,Heading=heading,Phase=index*Mathf.PI+jump.Number});
        }

        private static void Foam(WorldShiftArtMesh mesh,Vector3 centre,float radius,Color colour)
        {
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6f,b=(i+.65f)*Mathf.PI/6f;
                mesh.Beam(centre+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),
                    centre+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),.10f,.025f,colour);
            }
        }

        private Transform Render(Transform parent,string label,WorldShiftArtMesh geometry,Material material)
        {
            var mesh=geometry.Mesh(label); meshes.Add(mesh);
            var root=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(parent,false);
            root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return root.transform;
        }

        private void BuildFallBurst()
        {
            Color colour=environment switch { ShiftLevel.Tutorial=>new Color(.93f,.58f,.20f),ShiftLevel.City=>new Color(.51f,.72f,.70f),
                ShiftLevel.Desert=>new Color(.85f,.60f,.34f),ShiftLevel.Ocean=>new Color(.75f,.94f,.98f),
                ShiftLevel.Snow=>new Color(.88f,.96f,1f),_=>new Color(.55f,.55f,.51f) };
            var g=new WorldShiftArtMesh();
            for(int i=0;i<22;i++)
            {
                float angle=i*2.39996f,radius=.5f+(i%5)*.38f;
                Vector3 at=new Vector3(Mathf.Cos(angle)*radius,(i%4)*.35f,Mathf.Sin(angle)*radius);
                g.Rock(at,new Vector3(.15f,.3f+(i%3)*.15f,.15f),colour,i);
            }
            burst=Render(transform,"Themed fall burst",g,surface);burst.gameObject.SetActive(false);
        }

        private void Update()
        {
            if(game==null || WorldShiftSceneLoading.Active || game.State==ShiftRunState.Paused || game.TrainingLessonPaused) return;
            float dt=Time.deltaTime;clock+=dt;
            water.SetFloat("_OceanTime",clock);
            AnimateGorgeMist();
            foreach(var fin in fins)
            {
                if(!fin.Root.gameObject.activeInHierarchy) continue;
                float a=clock*.35f+fin.Phase;
                fin.Root.SetPositionAndRotation(fin.Centre+fin.Heading*new Vector3(Mathf.Cos(a)*2.8f,0,Mathf.Sin(a)*4.2f),
                    fin.Heading*Quaternion.LookRotation(new Vector3(-Mathf.Sin(a)*2.8f,0,Mathf.Cos(a)*4.2f)));
            }
            // Guardrail-gap falls have their own roll down the slope; the jump splash/burst is for jump drops only.
            bool falling=game.State==ShiftRunState.Retrying && !game.GapFall && (game.FailureReason=="NO ROAD" || game.FailureReason=="MISSED RELAY LANDING");
            if(!falling) {fallAge=0;wasFalling=false;burstStarted=false;burst.gameObject.SetActive(false);return;}
            if(!wasFalling) {fallAge=0;burstAge=0;wasFalling=true;}
            fallAge+=dt;
            float delay=environment==ShiftLevel.Ocean?.76f:environment==ShiftLevel.Snow?.70f:environment==ShiftLevel.Desert?.65f:environment==ShiftLevel.City?.58f:.40f;
            if(!burstStarted && fallAge>=delay)
            {
                burstStarted=true;burstAge=0;burst.position=game.Player.position;
                if(environment==ShiftLevel.Ocean) burst.position=new Vector3(burst.position.x,OceanSeaHeight+.1f,burst.position.z);
                burst.gameObject.SetActive(true);
            }
            if(burstStarted)
            {
                burstAge+=dt;float t=Mathf.Clamp01(burstAge/.38f);
                burst.localScale=new Vector3(1+t*1.7f,1-t*.65f,1+t*1.7f);
                if(t>=1) burst.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            foreach(var mesh in meshes) if(mesh!=null) Destroy(mesh);
            if(surface!=null) Destroy(surface);if(water!=null) Destroy(water);if(gorgeMist!=null) Destroy(gorgeMist);
        }
    }
}
