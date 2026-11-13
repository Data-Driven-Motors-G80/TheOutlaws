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

        public static WorldShiftRoadVehicleArt Build(Transform parent, Vector3 centre, Vector3 size,
            WorldShiftObstacleVisual.Palette palette, ShiftLevel environment)
        {
            var root = new GameObject("Oncoming traffic vehicle");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre;
            root.transform.localRotation = Quaternion.Euler(0,180,0);
            var art = root.AddComponent<WorldShiftRoadVehicleArt>();
            art.Create(size,palette,environment);
            return art;
        }

        private void Create(Vector3 size, WorldShiftObstacleVisual.Palette palette, ShiftLevel environment)
        {
            // Normalised dimensions describe a 3.05 x 1.8 x 5.5 m collision envelope.
            transform.localScale = new Vector3(size.x/3.05f,size.y/1.8f,size.z/5.5f);
            bool van = environment == ShiftLevel.Ocean;
            bool utility = van || environment == ShiftLevel.Desert || environment == ShiftLevel.Mountain;
            var paint = new WorldShiftArtMesh(); var details = new WorldShiftArtMesh(); var lights = new WorldShiftArtMesh();
            Color rubber=new Color(.035f,.045f,.052f), glass=new Color(.055f,.14f,.20f);
            Color steel=new Color(.49f,.57f,.61f), silver=new Color(.76f,.81f,.80f);
            float bottom=-.9f;
            paint.ChamferBox(new Vector3(0,-.20f,0),new Vector3(2.76f,.70f,5.40f),.12f,Color.white);
            paint.ChamferBox(new Vector3(0,.19f,1.77f),new Vector3(2.65f,.20f,1.65f),.08f,Color.white);
            // Sloped windscreen, tapered roof and rear glass, with a genuinely different van silhouette.
            float roof=van ? .88f : .72f;
            float frontRoof=van ? 1.05f : .57f, rearRoof=utility ? -1.85f : -.91f;
            Vector3 a=new Vector3(-1.27f,.13f,-1.96f),b=new Vector3(1.27f,.13f,-1.96f);
            Vector3 c=new Vector3(1.27f,.13f,1.53f),d=new Vector3(-1.27f,.13f,1.53f);
            Vector3 e=new Vector3(-1.10f,roof,rearRoof),f=new Vector3(1.10f,roof,rearRoof);
            Vector3 g=new Vector3(1.10f,roof,frontRoof),h=new Vector3(-1.10f,roof,frontRoof);
            paint.Quad(e,h,g,f,Color.white);
            if (van) paint.Quad(a,e,f,b,Color.white); else details.Quad(a,e,f,b,glass);
            details.Quad(d,c,g,h,glass);
            details.Quad(b,f,g,c,glass); details.Quad(d,h,e,a,glass);
            // Visible door pillars, window seals, handles, lower sills and wheel-arch trim.
            foreach(float side in new[] {-1f,1f})
            {
                details.Beam(new Vector3(side*1.27f,.13f,1.53f),new Vector3(side*1.10f,roof,frontRoof),.065f,.065f,rubber);
                details.Beam(new Vector3(side*1.27f,.13f,-1.96f),new Vector3(side*1.10f,roof,rearRoof),.065f,.065f,rubber);
                paint.Beam(new Vector3(side*1.26f,.13f,-.25f),new Vector3(side*1.10f,roof,-.25f),.10f,.10f,Color.white);
                details.Box(new Vector3(side*1.385f,-.29f,-.14f),new Vector3(.018f,.48f,.022f),rubber);
                details.ChamferBox(new Vector3(side*1.397f,.02f,-.48f),new Vector3(.04f,.065f,.28f),.018f,steel);
                details.Box(new Vector3(side*1.38f,-.48f,0),new Vector3(.06f,.08f,4.05f),rubber);
                details.Beam(new Vector3(side*1.25f,.12f,1.26f),new Vector3(side*1.44f,.24f,1.26f),.06f,.06f,rubber);
                paint.ChamferBox(new Vector3(side*1.43f,.26f,1.26f),new Vector3(.18f,.20f,.38f),.045f,Color.white);
                details.Box(new Vector3(side*1.435f,.26f,1.06f),new Vector3(.13f,.12f,.025f),silver);
                foreach(float z in new[] {-1.67f,1.65f})
                {
                    // A dark wheel well behind each tyre prevents floating wheels and bright seams.
                    details.Wheel(new Vector3(side*1.30f,-.48f,z),.46f,.12f,rubber);
                    BuildWheel(new Vector3(side*1.37f,bottom+TyreRadius,z),side,rubber,steel,silver);
                }
                if (van)
                {
                    paint.ChamferBox(new Vector3(side*1.235f,.40f,-1.16f),new Vector3(.12f,.84f,1.35f),.035f,Color.white);
                    details.Box(new Vector3(side*1.308f,.18f,-1.18f),new Vector3(.018f,.05f,1.25f),steel);
                    details.Box(new Vector3(side*1.313f,.40f,-.64f),new Vector3(.018f,.10f,.22f),rubber);
                }
                // Front lamps are on the end that actually approaches the player.
                details.ChamferBox(new Vector3(side*.91f,-.04f,2.695f),new Vector3(.68f,.30f,.055f),.035f,rubber);
                lights.ChamferBox(new Vector3(side*.86f,-.015f,2.731f),new Vector3(.45f,.16f,.025f),.025f,new Color(1f,.92f,.72f));
                lights.Box(new Vector3(side*1.18f,-.02f,2.728f),new Vector3(.14f,.14f,.03f),new Color(1f,.48f,.08f));
                lights.Box(new Vector3(side*1.11f,-.04f,-2.721f),new Vector3(.35f,.23f,.025f),new Color(.95f,.06f,.07f));
            }
            details.ChamferBox(new Vector3(0,-.40f,2.71f),new Vector3(2.62f,.25f,.08f),.055f,rubber);
            details.ChamferBox(new Vector3(0,-.40f,-2.71f),new Vector3(2.62f,.25f,.08f),.055f,rubber);
            details.Box(new Vector3(0,-.05f,2.714f),new Vector3(.85f,.29f,.025f),rubber);
            for(int i=0;i<4;i++) details.Box(new Vector3(0,-.14f+i*.064f,2.735f),new Vector3(.75f,.022f,.015f),steel);
            details.ChamferBox(new Vector3(0,-.40f,2.76f),new Vector3(.43f,.15f,.025f),.018f,silver);
            details.ChamferBox(new Vector3(0,-.39f,-2.76f),new Vector3(.43f,.15f,.025f),.018f,silver);
            // Wipers, bonnet shut line, roof seams and a modest service rack.
            details.Beam(new Vector3(-.95f,.17f,1.53f),new Vector3(-.40f,.30f,1.39f),.035f,.035f,rubber);
            details.Beam(new Vector3(.20f,.17f,1.53f),new Vector3(.75f,.30f,1.39f),.035f,.035f,rubber);
            details.Box(new Vector3(0,.295f,1.03f),new Vector3(2.30f,.018f,.025f),rubber);
            if (utility)
                foreach(float z in new[] {-.7f,-1.55f})
                    details.Box(new Vector3(0,roof+.025f,z),new Vector3(2.16f,.05f,.08f),steel);
            if(van)
            {
                var neutral=OwnMaterial("Salt-weathered service van paint",false);
                neutral.SetColor("_BaseColor",new Color(.70f,.76f,.72f));neutral.SetFloat("_Wear",.45f);
                neutral.SetFloat("_Smoothness",.45f);
                Draw("Neutral coastal van body",paint,neutral);
                var livery=new WorldShiftArtMesh();
                foreach(float side in new[] {-1f,1f})
                    livery.Box(new Vector3(side*1.40f,-.16f,-.10f),new Vector3(.025f,.22f,3.7f),Color.white);
                livery.Box(new Vector3(0,-.28f,2.755f),new Vector3(2.30f,.15f,.025f),Color.white);
                var band=Draw("Obstacle block",livery,RuntimeMaterials.Coloured(Color.white));palette.Add(band.transform);
            }
            else
            {
                var body=Draw("Obstacle block",paint,RuntimeMaterials.Coloured(Color.white));palette.Add(body.transform);
            }
            var surface = OwnMaterial("Traffic glass, rubber and metal",false);
            surface.SetFloat("_Smoothness",.65f); surface.SetFloat("_Specular",.28f);
            Draw("Vehicle windows, fittings and trim",details,surface);
            Draw("Vehicle headlamps and indicators",lights,OwnMaterial("Traffic lamp lenses",true));
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
