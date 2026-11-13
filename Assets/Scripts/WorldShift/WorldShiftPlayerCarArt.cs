using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>The same maroon getaway coupe and finishes in every current level.</summary>
    public sealed class WorldShiftPlayerCarArt : MonoBehaviour
    {
        private readonly List<Mesh> meshes=new List<Mesh>();
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Transform> wheels=new List<Transform>();
        private static readonly Color Cream=new Color(.89f,.87f,.73f);
        private Vector3 previousPosition;
        private bool poseReady;
        private float wheelAngle;

        public static GameObject Build(Transform parent)
        {
            var root=new GameObject("Player getaway coupe");root.transform.SetParent(parent,false);
            root.AddComponent<WorldShiftPlayerCarArt>().BuildModel();return root;
        }

        private void BuildModel()
        {
            var vehicleFinish=Finish("Shared coupe paint and trim",0,.60f,.23f,.18f);
            var glassFinish=Finish("Shared coupe glazing",5,.85f,.35f,.05f);
            var luminous=Finish("Shared coupe lamp lenses",0,0,0,0);luminous.SetFloat("_Emission",1f);
            var g = new WorldShiftArtMesh(); var lamps = new WorldShiftArtMesh(); var windows = new WorldShiftArtMesh();
            Color paint = new Color(.64f, .08f, .22f);
            Color glass = new Color(.08f, .23f, .29f);
            Color rubber = new Color(.035f, .055f, .065f);
            g.ChamferBox(new Vector3(0, .46f, 0), new Vector3(1.58f, .48f, 3.05f), .075f, paint);
            g.ChamferBox(new Vector3(0, .70f, .94f), new Vector3(1.48f, .12f, 1.05f), .035f, paint * 1.12f);
            g.Box(new Vector3(0, .69f, -1.12f), new Vector3(1.48f, .12f, .72f), paint);
            // A tapered cabin with sloped glass gives a recognisable sports-car silhouette.
            Vector3 a = new Vector3(-.70f,.70f,-.77f), b = new Vector3(.70f,.70f,-.77f);
            Vector3 c = new Vector3(.70f,.70f,.48f), d = new Vector3(-.70f,.70f,.48f);
            Vector3 e = new Vector3(-.57f,1.13f,-.48f), f = new Vector3(.57f,1.13f,-.48f);
            Vector3 h = new Vector3(-.57f,1.13f,.12f), j = new Vector3(.57f,1.13f,.12f);
            windows.Quad(a,e,f,b,glass); windows.Quad(d,c,j,h,glass);
            windows.Quad(b,f,j,c,glass); windows.Quad(d,h,e,a,glass);
            g.Quad(e,h,j,f,paint);
            foreach (float side in new[] {-1f,1f})
            {
                float x = side*.81f;
                foreach (float z in new[] {-.95f,.96f})
                {
                    var wheelMesh=new WorldShiftArtMesh();
                    wheelMesh.Wheel(Vector3.zero,.34f,.18f,rubber);
                    wheelMesh.Wheel(Vector3.right*side*.105f,.18f,.025f,Cream);
                    var wheel=Draw("Player wheel and rim",wheelMesh,vehicleFinish).transform;
                    wheel.localPosition=new Vector3(x,.33f,z);wheels.Add(wheel);
                }
                g.Box(new Vector3(side*.83f,.81f,.2f), new Vector3(.20f,.12f,.24f), paint);
                lamps.Box(new Vector3(side*.52f,.62f,1.535f), new Vector3(.38f,.13f,.035f), new Color(1f,.93f,.73f));
                lamps.Box(new Vector3(side*.55f,.59f,-1.535f), new Vector3(.32f,.11f,.035f), new Color(1f,.16f,.14f));
                g.Box(new Vector3(side*.23f,.77f,.95f), new Vector3(.12f,.02f,.98f), Cream);
                g.Box(new Vector3(side*.23f,1.142f,-.18f), new Vector3(.12f,.02f,.58f), Cream);
            }
            g.Box(new Vector3(0,.35f,-1.55f), new Vector3(1.35f,.14f,.10f), rubber);
            g.Box(new Vector3(0,.34f,1.55f), new Vector3(1.35f,.12f,.10f), rubber);
            g.Box(new Vector3(0,.49f,-1.61f), new Vector3(.30f,.13f,.025f), Cream);
            Draw("Player coupe body", g, vehicleFinish);
            Draw("Player coupe glazing",windows,glassFinish);
            Draw("Player headlamps and tail lamps", lamps, luminous);
        }

        private Material Finish(string label,float kind,float smooth,float specular,float wear)
        {
            var m=new Material(Resources.Load<Shader>("WorldShiftOceanSurface")){name=label};
            m.SetFloat("_Finish",kind);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Specular",specular);m.SetFloat("_Wear",wear);
            materials.Add(m);return m;
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
        {if(Application.isPlaying) Destroy(obj);else DestroyImmediate(obj);}
    }
}
