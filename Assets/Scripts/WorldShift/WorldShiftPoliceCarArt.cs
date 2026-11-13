using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Shared pursuit-car identity: black and white panels, readable livery and a red/blue roof bar.</summary>
    public sealed class WorldShiftPoliceCarArt : MonoBehaviour
    {
        private readonly List<Mesh> meshes=new List<Mesh>();
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Transform> wheels=new List<Transform>();
        private Vector3 previousPosition;
        private bool poseReady;
        private float wheelAngle;

        public static GameObject Build(Transform parent)
        {
            var root=new GameObject("Police patrol cruiser");root.transform.SetParent(parent,false);
            root.AddComponent<WorldShiftPoliceCarArt>().BuildModel();return root;
        }

        private void BuildModel()
        {
            Color black=new Color(.035f,.050f,.065f),white=new Color(.94f,.95f,.91f);
            Color rubber=new Color(.025f,.032f,.038f),steel=new Color(.52f,.60f,.64f);
            Color glass=new Color(.055f,.17f,.23f);
            var body=new WorldShiftArtMesh();var trim=new WorldShiftArtMesh();var markings=new WorldShiftArtMesh();
            var lamps=new WorldShiftArtMesh();var red=new WorldShiftArtMesh();var blue=new WorldShiftArtMesh();
            Material finish=Finish("Police paint, glass and trim",false);
            finish.SetFloat("_Smoothness",.58f);finish.SetFloat("_Specular",.18f);
            Material lens=Finish("Steady police light lenses",true);
            body.ChamferBox(new Vector3(0,.49f,0),new Vector3(1.78f,.53f,3.60f),.095f,black);
            body.ChamferBox(new Vector3(0,.77f,1.12f),new Vector3(1.66f,.13f,1.24f),.035f,black);
            body.ChamferBox(new Vector3(0,.75f,-1.37f),new Vector3(1.66f,.12f,.71f),.035f,black);
            Vector3 a=new Vector3(-.77f,.75f,-1.02f),b=new Vector3(.77f,.75f,-1.02f);
            Vector3 c=new Vector3(.77f,.75f,.53f),d=new Vector3(-.77f,.75f,.53f);
            Vector3 e=new Vector3(-.62f,1.20f,-.68f),f=new Vector3(.62f,1.20f,-.68f);
            Vector3 g=new Vector3(.62f,1.20f,.12f),h=new Vector3(-.62f,1.20f,.12f);
            trim.Quad(a,e,f,b,glass);trim.Quad(d,c,g,h,glass);
            trim.Quad(b,f,g,c,glass);trim.Quad(d,h,e,a,glass);
            body.Quad(e,h,g,f,black);
            foreach(float side in new[] {-1f,1f})
            {
                body.ChamferBox(new Vector3(side*.895f,.57f,-.08f),new Vector3(.035f,.37f,1.60f),.012f,white);
                trim.Beam(new Vector3(side*.77f,.75f,-.22f),new Vector3(side*.62f,1.20f,-.22f),.055f,.055f,black);
                trim.Box(new Vector3(side*.919f,.49f,-.23f),new Vector3(.012f,.22f,.018f),black);
                trim.Box(new Vector3(side*.922f,.66f,-.49f),new Vector3(.012f,.040f,.18f),steel);
                body.ChamferBox(new Vector3(side*.88f,.85f,.36f),new Vector3(.16f,.13f,.25f),.025f,black);
                trim.Box(new Vector3(side*.88f,.85f,.224f),new Vector3(.11f,.075f,.02f),steel);
                foreach(float z in new[] {-1.12f,1.12f})
                {
                    var wheel=new WorldShiftArtMesh();wheel.Wheel(Vector3.zero,.34f,.17f,rubber);
                    Vector3 face=Vector3.right*side*.095f;
                    wheel.Wheel(face,.21f,.025f,steel);wheel.Wheel(face+Vector3.right*side*.02f,.10f,.025f,black);
                    for(int i=0;i<6;i++)
                    {
                        float angle=i*Mathf.PI/3;
                        wheel.Beam(face+Vector3.right*side*.025f,
                            face+new Vector3(side*.025f,Mathf.Cos(angle)*.185f,Mathf.Sin(angle)*.185f),.035f,.035f,white);
                    }
                    Transform root=Draw("Police wheel and steel rim",wheel,finish).transform;
                    root.localPosition=new Vector3(side*.855f,.34f,z);wheels.Add(root);
                }
                lamps.ChamferBox(new Vector3(side*.56f,.63f,1.815f),new Vector3(.39f,.16f,.035f),.025f,new Color(1f,.94f,.78f));
                lamps.Box(new Vector3(side*.70f,.60f,-1.818f),new Vector3(.22f,.13f,.035f),new Color(1f,.12f,.10f));
                trim.Beam(new Vector3(side*.42f,.35f,1.88f),new Vector3(side*.42f,.68f,1.88f),.08f,.08f,black);
                Lettering(markings,"POLICE",new Vector3(side*.920f,.52f,-.08f),Vector3.forward*side,Vector3.up,1.22f,.22f,black);
            }
            trim.ChamferBox(new Vector3(0,.35f,1.82f),new Vector3(1.66f,.16f,.12f),.045f,white);
            trim.ChamferBox(new Vector3(0,.35f,-1.82f),new Vector3(1.66f,.16f,.12f),.045f,white);
            trim.Beam(new Vector3(-.53f,.61f,1.88f),new Vector3(.53f,.61f,1.88f),.08f,.08f,black);
            trim.Box(new Vector3(0,.63f,1.807f),new Vector3(.63f,.19f,.025f),black);
            for(int i=0;i<3;i++) trim.Box(new Vector3(0,.58f+i*.055f,1.827f),new Vector3(.58f,.015f,.015f),steel);
            // Livery is geometry, so its readability doesn't depend on a downloaded font or texture.
            Lettering(markings,"POLICE",new Vector3(0,.846f,1.12f),Vector3.right,Vector3.forward,1.30f,.31f,white);
            Lettering(markings,"POLICE",new Vector3(0,.61f,-1.827f),Vector3.right,Vector3.up,1.10f,.18f,white);
            Lettering(markings,"07",new Vector3(0,1.211f,-.48f),Vector3.right,Vector3.forward,.52f,.27f,white);
            body.ChamferBox(new Vector3(0,1.245f,-.06f),new Vector3(1.37f,.10f,.30f),.035f,black);
            red.ChamferBox(new Vector3(-.43f,1.335f,-.06f),new Vector3(.45f,.13f,.27f),.04f,new Color(1f,.055f,.075f));
            blue.ChamferBox(new Vector3(.43f,1.335f,-.06f),new Vector3(.45f,.13f,.27f),.04f,new Color(.055f,.36f,1f));
            lamps.ChamferBox(new Vector3(0,1.325f,-.06f),new Vector3(.22f,.11f,.25f),.03f,white);
            trim.Beam(new Vector3(.45f,.80f,-1.43f),new Vector3(.45f,1.15f,-1.43f),.025f,.025f,black);
            Draw("Black and white police body",body,finish);
            Draw("Police windows, grille and push bar",trim,finish);
            Draw("POLICE livery and roof unit number",markings,finish);
            Draw("Police headlamps and tail lamps",lamps,lens);
            Draw("Red roof light lens",red,lens);Draw("Blue roof light lens",blue,lens);
        }

        private static void Lettering(WorldShiftArtMesh mesh,string text,Vector3 centre,Vector3 right,Vector3 up,float width,float height,Color colour)
        {
            float pixelX=width/(text.Length*6-1),pixelY=height/7;
            Vector3 origin=centre-right*width*.5f-up*height*.5f;
            for(int letter=0;letter<text.Length;letter++)
            {
                string[] rows=Glyph(text[letter]);
                for(int y=0;y<7;y++)
                    for(int x=0;x<5;x++)
                    {
                        if(rows[y][x]!='1') continue;
                        Vector3 p=origin+right*((letter*6+x)*pixelX)+up*((6-y)*pixelY);
                        mesh.Quad(p,p+up*pixelY,p+up*pixelY+right*pixelX,p+right*pixelX,colour);
                    }
            }
        }

        private static string[] Glyph(char c)
        {
            switch(c)
            {
                case 'P':return new[]{"11110","10001","10001","11110","10000","10000","10000"};
                case 'O':case '0':return new[]{"01110","10001","10001","10001","10001","10001","01110"};
                case 'L':return new[]{"10000","10000","10000","10000","10000","10000","11111"};
                case 'I':return new[]{"11111","00100","00100","00100","00100","00100","11111"};
                case 'C':return new[]{"01111","10000","10000","10000","10000","10000","01111"};
                case 'E':return new[]{"11111","10000","10000","11110","10000","10000","11111"};
                default:return new[]{"11111","00001","00010","00100","01000","01000","01000"};
            }
        }
        private Material Finish(string label,bool glow)
        {
            var m=new Material(Resources.Load<Shader>("WorldShiftOceanSurface")){name=label};
            m.SetFloat("_Emission",glow ? 1f : 0f);materials.Add(m);return m;
        }
        private GameObject Draw(string label,WorldShiftArtMesh art,Material material)
        {
            var root=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(transform,false);
            var mesh=art.Mesh(label);meshes.Add(mesh);root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return root;
        }
        private void LateUpdate()
        {
            Vector3 now=transform.position;
            if(poseReady && Time.timeScale>0 && (now-previousPosition).sqrMagnitude<100f)
            {
                wheelAngle+=Vector3.Dot(now-previousPosition,transform.forward)/.34f*Mathf.Rad2Deg;
                foreach(var wheel in wheels) wheel.localRotation=Quaternion.AngleAxis(wheelAngle,Vector3.right);
            }
            previousPosition=now;poseReady=true;
        }
        private void OnDestroy()
        {
            foreach(var mesh in meshes) Release(mesh);
            foreach(var material in materials) Release(material);
        }
        private static void Release(Object obj)
        { if(Application.isPlaying) Destroy(obj);else DestroyImmediate(obj); }
    }
}
