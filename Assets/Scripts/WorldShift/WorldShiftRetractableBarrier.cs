using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Presentation only: world collision changes immediately, independently of the panels.</summary>
    internal sealed class WorldShiftRetractableBarrier : MonoBehaviour
    {
        private Mesh bodyMesh, baseMesh;
        private Material finish;
        private Transform panels;
        private bool initialized, raised;
        private float amount;
        public const float TransitionSeconds = .18f;

        public static bool Supports(ShiftHazard data) => !data.SharedAcrossWorlds && !data.LateralMotion
            && data.MotionSpeed == 0f && (data.Kind == ShiftHazardKind.Barrier
            || data.Kind == ShiftHazardKind.HalfBarrier || data.Kind == ShiftHazardKind.SwitchWall
            || data.Kind == ShiftHazardKind.Divider);

        public static WorldShiftRetractableBarrier Build(Transform parent, Vector3 centre, Vector3 size, ShiftWorld world)
        {
            var root = new GameObject("Retractable " + world + " barrier");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre - Vector3.up * size.y * .5f;
            var result = root.AddComponent<WorldShiftRetractableBarrier>();
            Color colour = world == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour;
            Color dark = new Color(.055f, .065f, .085f);
            var body = new WorldShiftArtMesh();
            var footing = new WorldShiftArtMesh();
            float w = size.x, h = size.y, d = size.z;
            footing.Box(new Vector3(0,.025f,0), new Vector3(w,.05f,d), new Color(.29f,.31f,.33f));
            // Solid road cover with thick dashed paint, never a raised wireframe or dark opening.
            const float stripe = .22f, dash = .95f, gap = .32f;
            for(float z=-d*.5f; z<d*.5f; z+=dash+gap)
            {
                float length=Mathf.Min(dash,d*.5f-z);
                foreach(float x in new[] {-w*.5f+stripe*.5f,w*.5f-stripe*.5f})
                    footing.Box(new Vector3(x,.065f,z+length*.5f),new Vector3(stripe,.025f,length),colour);
            }
            for(float x=-w*.5f; x<w*.5f; x+=dash+gap)
            {
                float length=Mathf.Min(dash,w*.5f-x);
                foreach(float z in new[] {-d*.5f+stripe*.5f,d*.5f-stripe*.5f})
                    footing.Box(new Vector3(x+length*.5f,.065f,z),new Vector3(length,.025f,stripe),colour);
            }
            // One broad panel per lane, rather than many narrow posts.
            int count = Mathf.Clamp(Mathf.RoundToInt(w / (10f / 3f)), 1, 3);
            // Lowered seams line up with the same panel divisions.
            for (int seam = 1; seam < count; seam++)
                footing.Box(new Vector3(-w*.5f+w*seam/count,.054f,0),
                    new Vector3(.035f,.012f,d-.15f), new Color(.22f,.24f,.26f));
            float panelWidth = w / count;
            for (int i=0; i<count; i++)
            {
                float x = -w*.5f + (i+.5f)*panelWidth;
                body.ChamferBox(new Vector3(x,h*.5f,0),new Vector3(panelWidth-.12f,h,d-.08f),.08f,colour);
                foreach (float side in new[] {-1f,1f})
                {
                    float z=side*(d*.5f-.033f);
                    foreach (float corner in new[] {-.36f,.36f})
                        body.Box(new Vector3(x+corner*panelWidth,h*.89f,z),new Vector3(.065f,.065f,.025f),dark);
                    Icon(body,new Vector3(x,h*.53f,z+side*.018f),Mathf.Min(panelWidth*.36f,h*.38f),world,dark);
                }
            }
            result.finish = new Material(Resources.Load<Shader>("WorldShiftOceanSurface"));
            result.finish.name = world + " retractable barrier";
            result.finish.SetFloat("_Emission",.45f);
            result.finish.SetFloat("_Specular",.02f);
            result.bodyMesh=body.Mesh("Retractable panels");
            result.baseMesh=footing.Mesh("Flush barrier footprint");
            result.panels=result.Draw(root.transform,"Panels",result.bodyMesh);
            result.Draw(root.transform,"Flush base",result.baseMesh);
            return result;
        }

        private Transform Draw(Transform parent,string label,Mesh mesh)
        {
            var obj=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));
            obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial=finish;
            return obj.transform;
        }

        private static void Icon(WorldShiftArtMesh art,Vector3 centre,float r,ShiftWorld world,Color ink)
        {
            if(world == ShiftWorld.Day)
            {
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI*2/24, b=(i+1)*Mathf.PI*2/24;
                    art.Triangle(centre,centre+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*r*.58f,
                        centre+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*r*.58f,ink,true);
                }
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4;
                    Vector3 axis=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0), cross=new Vector3(-axis.y,axis.x,0)*r*.13f;
                    Vector3 p=centre+axis*r*.75f,q=centre+axis*r;
                    art.Triangle(p-cross,q-cross,q+cross,ink,true);
                    art.Triangle(p-cross,q+cross,p+cross,ink,true);
                }
            }
            else
            {
                // A broad crescent, with the opening toward the right.
                for(int i=0;i<24;i++)
                {
                    float a=Mathf.Lerp(Mathf.PI*.5f,Mathf.PI*1.5f,i/24f);
                    float b=Mathf.Lerp(Mathf.PI*.5f,Mathf.PI*1.5f,(i+1)/24f);
                    Vector3 Outer(float t)=>centre+new Vector3(Mathf.Cos(t)*r,Mathf.Sin(t)*r,0);
                    Vector3 Inner(float t)=>centre+new Vector3(Mathf.Cos(t)*r*.55f,Mathf.Sin(t)*r,0);
                    art.Triangle(Outer(a),Outer(b),Inner(b),ink,true);
                    art.Triangle(Outer(a),Inner(b),Inner(a),ink,true);
                }
            }
        }

        public void SetRaised(bool value)
        {
            raised=value;
            if (!initialized) { initialized=true; amount=value?1f:0f; Apply(); }
        }
        public void Step(float seconds)
        {
            amount=Mathf.MoveTowards(amount,raised?1f:0f,Mathf.Max(0f,seconds)/TransitionSeconds);
            Apply();
        }
        private void Apply()
        {
            float height=amount*amount*(3f-2f*amount);
            panels.localScale=new Vector3(1f,Mathf.Max(.001f,height),1f);
            panels.gameObject.SetActive(amount>0f);
        }
        private void OnDestroy()
        {
            if(Application.isPlaying) { Destroy(bodyMesh); Destroy(baseMesh); Destroy(finish); }
            else { DestroyImmediate(bodyMesh); DestroyImmediate(baseMesh); DestroyImmediate(finish); }
        }
    }
}
