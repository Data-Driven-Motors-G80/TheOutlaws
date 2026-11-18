using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Snow-only obstacle art. Ground footprints stay unchanged; upright tree canopies rise above the box. No colliders.</summary>
    internal sealed class WorldShiftSnowObstacleArt : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private static readonly Color Snow = new Color(.88f,.94f,.98f);
        private static readonly Color Ice = new Color(.34f,.65f,.77f);

        public static void Snowbank(Transform parent, Vector3 centre, Vector3 size)
        {
            var root=new GameObject("Packed snowdrift");root.transform.SetParent(parent,false);
            root.transform.localPosition=centre-Vector3.up*size.y*.5f;
            var owner=root.AddComponent<WorldShiftSnowObstacleArt>();
            var art=new WorldShiftArtMesh();float w=size.x,h=size.y,d=size.z;
            // Sloping, asymmetric rings form a ploughed pile rather than a rectangular crate.
            Vector2[] rim={new Vector2(.5f,.32f),new Vector2(.30f,.5f),new Vector2(-.30f,.5f),new Vector2(-.5f,.32f),
                new Vector2(-.5f,-.32f),new Vector2(-.30f,-.5f),new Vector2(.30f,-.5f),new Vector2(.5f,-.32f)};
            var foot=new Vector3[rim.Length];var shoulder=new Vector3[rim.Length];var crown=new Vector3[rim.Length];
            float seed=Mathf.Round(parent.position.z+centre.x*7f);
            float lean=Mathf.Sin(seed*1.3f)*w*.08f;
            for(int i=0;i<rim.Length;i++)
            {
                Vector2 v=rim[i];float variation=Mathf.Sin(seed+i*2.1f);
                foot[i]=new Vector3(v.x*w,.015f,v.y*d);
                shoulder[i]=new Vector3(v.x*w*.88f,h*(.32f+variation*.055f),v.y*d*.88f);
                crown[i]=new Vector3(v.x*w*.46f+lean,h*(.64f+variation*.075f),v.y*d*.46f);
            }
            Vector3 peak=new Vector3(lean,h*.98f,Mathf.Sin(seed)*d*.09f);
            for(int i=0;i<rim.Length;i++)
            {
                int j=(i+1)%rim.Length;
                Color packed=Color.Lerp(Ice,Snow,.45f+(i%3)*.12f);
                art.Quad(foot[i],shoulder[i],shoulder[j],foot[j],packed);
                art.Quad(shoulder[i],crown[i],crown[j],shoulder[j],Snow*(i%2==0?1f:.93f));
                art.Triangle(crown[i],peak,crown[j],Snow);
            }
            // Small plough markers identify the blocked edges without framing the pile like a box.
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 stake=new Vector3(side*w*.41f,h*.34f,-d*.39f);
                art.Box(stake,new Vector3(.085f,h*.68f,.085f),new Color(.28f,.32f,.34f));
                art.Box(stake+Vector3.up*h*.23f,new Vector3(.13f,h*.18f,.13f),new Color(.96f,.30f,.12f));
            }
            owner.mesh=art.Mesh("Sloped packed snow mound and plough markers");
            owner.material=new Material(Resources.Load<Shader>("WorldShiftOceanSurface")){name="Solid snowbank finish"};
            owner.material.SetFloat("_Emission",.15f);
            root.AddComponent<MeshFilter>().sharedMesh=owner.mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=owner.material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        public static void UprightPines(WorldShiftArtMesh art,float w,float h,float d,ShiftWorld world)
        {
            Color bark=new Color(.27f,.15f,.09f),needles=new Color(.055f,.22f,.17f);
            // A bank fills the existing blocked area, so gaps between trunks never suggest a safe lane.
            art.ChamferBox(new Vector3(0,h*.15f,0),new Vector3(w,h*.30f,d),.16f,Ice);
            art.ChamferBox(new Vector3(0,h*.28f,0),new Vector3(w*.97f,h*.20f,d*.98f),.14f,Snow);
            int count=Mathf.Max(1,Mathf.RoundToInt(w/3f));
            float spacing=w/count;
            for(int tree=0;tree<count;tree++)
            {
                float x=-w*.5f+spacing*(tree+.5f);
                float height=h*(3f+(tree%2)*.2f),radius=Mathf.Min(spacing*.45f,1.55f);
                art.Cylinder(new Vector3(x,h*.12f,0),.22f,.14f,height*.72f,bark,10);
                for(int tier=0;tier<3;tier++)
                {
                    float bottom=height*(.20f+tier*.22f),top=height*(.72f+tier*.14f);
                    float r=radius*(1f-tier*.22f),coneHeight=top-bottom;
                    art.Cylinder(new Vector3(x,bottom,0),r,0,coneHeight,needles,12);
                    // Snow caps leave a broad dark-green skirt on every upright branch tier.
                    art.Cylinder(new Vector3(x,bottom+coneHeight*.32f,0),r*.72f,0,coneHeight*.68f,Snow,12);
                }
                foreach(float side in new[]{-1f,1f})
                    WorldPanel(art,new Vector3(x,h*.62f,side*(d*.5f-.05f)),
                        Mathf.Min(spacing*.68f,2.1f),h*.56f,world);
            }
            // Bright, continuous lower edges make the full blocked width readable on approach.
            Color marker=world==ShiftWorld.Day?WorldShiftLevelProps.SwitchColour:WorldShiftLevelProps.NightColour;
            foreach(float side in new[]{-1f,1f})
                art.Box(new Vector3(0,h*.19f,side*(d*.5f-.04f)),new Vector3(w,h*.16f,.06f),marker);
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
