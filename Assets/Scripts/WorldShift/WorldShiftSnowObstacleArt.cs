using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Snow-only obstacle art. Geometry stays in the existing obstacle box and adds no colliders.</summary>
    internal sealed class WorldShiftSnowObstacleArt : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private static readonly Color Snow = new Color(.88f,.94f,.98f);
        private static readonly Color Ice = new Color(.34f,.65f,.77f);

        public static void Snowbank(Transform parent, Vector3 centre, Vector3 size)
        {
            var root=new GameObject("Packed snowbank");root.transform.SetParent(parent,false);
            root.transform.localPosition=centre-Vector3.up*size.y*.5f;
            var owner=root.AddComponent<WorldShiftSnowObstacleArt>();
            var art=new WorldShiftArtMesh();float w=size.x,h=size.y,d=size.z;
            art.ChamferBox(new Vector3(0,h*.40f,0),new Vector3(w,h*.8f,d),.24f,Ice);
            art.ChamferBox(new Vector3(0,h*.68f,0),new Vector3(w*.93f,h*.48f,d*.96f),.28f,Snow);
            // Hard, dark edging and reflector stakes separate a solid bank from harmless roadside snow.
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*(w*.5f-.13f);
                art.Box(new Vector3(x,h*.5f,0),new Vector3(.13f,h,d*.90f),new Color(.13f,.24f,.30f));
                foreach(float z in new[]{-d*.5f+.12f,d*.5f-.12f})
                {
                    art.Box(new Vector3(x,h*.5f,z),new Vector3(.20f,h,.20f),new Color(.17f,.23f,.26f));
                    art.Box(new Vector3(x,h*.76f,z),new Vector3(.21f,h*.20f,.21f),new Color(.96f,.42f,.15f));
                }
            }
            owner.mesh=art.Mesh("Packed snow and reflector stakes");
            owner.material=new Material(Resources.Load<Shader>("WorldShiftOceanSurface")){name="Solid snowbank finish"};
            owner.material.SetFloat("_Emission",.15f);
            root.AddComponent<MeshFilter>().sharedMesh=owner.mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=owner.material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        public static void Glacier(WorldShiftArtMesh art,float w,float h,float d,ShiftWorld world)
        {
            art.Rock(Vector3.zero,new Vector3(w*.5f,h,d*.5f),Ice,17);
            art.Rock(new Vector3(-w*.12f,h*.07f,0),new Vector3(w*.34f,h*.90f,d*.35f),Snow,41);
            foreach(float side in new[]{-1f,1f})
            {
                float z=side*(d*.5f-.05f);
                WorldPanel(art,new Vector3(0,h*.45f,z),w*.86f,h*.60f,world);
            }
        }

        public static void FallenPine(WorldShiftArtMesh art,float w,float h,float d,ShiftWorld world)
        {
            Color bark=new Color(.31f,.19f,.12f), needles=new Color(.07f,.25f,.21f);
            float trunkY=h*.52f;
            art.ChamferBox(new Vector3(0,h*.08f,0),new Vector3(w,h*.16f,d),.10f,Snow);
            art.Beam(new Vector3(-w*.48f,trunkY,0),new Vector3(w*.48f,trunkY,0),h*.32f,h*.32f,bark);
            for(int tier=0;tier<3;tier++)
            {
                float start=-w*.38f+tier*w*.19f,tip=Mathf.Min(w*.47f,start+w*.48f);
                float ry=h*(.44f-tier*.045f),rz=Mathf.Min(d*.43f,h*(.70f-tier*.07f));
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6f,b=(i+1)*Mathf.PI/6f;
                    Vector3 A=new Vector3(start,trunkY+Mathf.Sin(a)*ry,Mathf.Cos(a)*rz);
                    Vector3 B=new Vector3(start,trunkY+Mathf.Sin(b)*ry,Mathf.Cos(b)*rz);
                    Vector3 T=new Vector3(tip,trunkY,0);
                    Color tint=Mathf.Sin((a+b)*.5f)>.3f?Snow:needles*(i%2==0?1f:.80f);tint.a=1;
                    art.Triangle(A,B,T,tint,true);
                }
            }
            // World panels stay unobscured by branches and fit within the original front/back edges.
            foreach(float side in new[]{-1f,1f})
                WorldPanel(art,new Vector3(0,h*.42f,side*(d*.5f-.05f)),Mathf.Min(w*.65f,3f),h*.42f,world);
        }

        internal static void WorldPanel(WorldShiftArtMesh art,Vector3 centre,float width,float height,ShiftWorld world)
        {
            Color colour=world==ShiftWorld.Day?WorldShiftLevelProps.SwitchColour:WorldShiftLevelProps.NightColour;
            Color ink=new Color(.055f,.065f,.085f);
            art.ChamferBox(centre,new Vector3(width,height,.07f),.035f,colour);
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 face=centre+Vector3.forward*side*.039f;
                float radius=Mathf.Min(height*.35f,width*.18f);
                if(world==ShiftWorld.Day)
                {
                    for(int i=0;i<16;i++)
                    {
                        float a=i*Mathf.PI/8f,b=(i+1)*Mathf.PI/8f;
                        art.Triangle(face,face+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius*.56f,
                            face+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius*.56f,ink,true);
                    }
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4f;Vector3 axis=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                        art.Beam(face+axis*radius*.74f,face+axis*radius,radius*.15f,.012f,ink);
                    }
                }
                else for(int i=0;i<16;i++)
                {
                    float a=Mathf.PI*.5f+i*Mathf.PI/16f,b=a+Mathf.PI/16f;
                    Vector3 A(float t,float r)=>face+new Vector3(Mathf.Cos(t)*radius*r,Mathf.Sin(t)*radius,0);
                    art.Quad(A(a,1),A(b,1),A(b,.45f),A(a,.45f),ink);
                    art.Quad(A(a,.45f),A(b,.45f),A(b,1),A(a,1),ink);
                }
            }
        }

        private void OnDestroy()
        {
            if(Application.isPlaying){Destroy(mesh);Destroy(material);}
            else{DestroyImmediate(mesh);DestroyImmediate(material);}
        }
    }
}
