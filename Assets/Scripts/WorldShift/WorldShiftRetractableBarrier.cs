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

        public static WorldShiftRetractableBarrier Build(Transform parent, Vector3 centre, Vector3 size, ShiftWorld world,
            ShiftLevel environment = ShiftLevel.City, int variant = 0)
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
            string winterShape=environment!=ShiftLevel.Snow?null:w<=3.1f?"World-coloured block"
                :w<6f || (variant/5)%2==0?"Upright pines":"World-coloured roadblock";
            if(winterShape!=null) root.name="Retractable "+world+" "+winterShape;
            // Desert follows Snow's format: half and full-width closures alternate sandstone hoodoos and saguaro cacti.
            string desertShape=environment!=ShiftLevel.Desert || w<=3.1f?null:(variant/5)%2==0?"Sandstone hoodoos":"Saguaro cacti";
            if(desertShape!=null) root.name="Retractable "+world+" "+desertShape;
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
            if(winterShape=="Upright pines") WorldShiftSnowObstacleArt.UprightPines(body,w,h,d,world);
            else if(desertShape=="Sandstone hoodoos") WorldShiftDesertObstacleArt.Hoodoos(body,w,h,d,world);
            else if(desertShape!=null) WorldShiftDesertObstacleArt.Saguaros(body,w,h,d,world);
            else for (int i=0; i<count; i++)
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
                Theme(body,environment,world,x,panelWidth-.12f,h,d-.08f);
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

        // Environment dressing around the world-coloured face. The face colour, sun/moon icon,
        // footprint and sinking motion stay identical, so the mechanic reads the same everywhere.
        // All detail stays inside the panel's box (collision is independent of this art).
        private static void Theme(WorldShiftArtMesh art, ShiftLevel environment, ShiftWorld world,
            float x, float w, float h, float d)
        {
            bool day = world == ShiftWorld.Day;
            Color ink = day ? new Color(.06f,.07f,.09f) : new Color(.78f,.8f,.82f);
            Color wood = new Color(.46f,.32f,.19f), woodDark = new Color(.3f,.2f,.12f);
            float front = d*.5f;
            // Keep every face detail clear of the sun/moon icon so the world stays readable.
            float iconR = Mathf.Min((w+.12f)*.36f,h*.38f)*1.12f, iconY = h*.53f;
            bool OverIcon(float px, float y, float height) => Mathf.Abs(px) < iconR && y+height*.5f > iconY-iconR && y-height*.5f < iconY+iconR;
            void FaceBox(float x0, float x1, float y, float height, float depth, Color c)
            {
                if (x1-x0 < .01f) return;
                foreach (float side in new[] {-1f,1f})
                    art.Box(new Vector3(x+(x0+x1)*.5f,y,side*(front-depth*.5f+.012f)),new Vector3(x1-x0,height,depth),c);
            }
            void OnBothFaces(float y, float height, float x0, float x1, float depth, Color c)
            {
                if (y+height*.5f > iconY-iconR && y-height*.5f < iconY+iconR)
                {
                    FaceBox(x0,Mathf.Min(x1,-iconR),y,height,depth,c);
                    FaceBox(Mathf.Max(x0,iconR),x1,y,height,depth,c);
                }
                else FaceBox(x0,x1,y,height,depth,c);
            }
            switch (environment)
            {
                case ShiftLevel.City:
                {
                    // Roadworks: a diagonal hazard band along the foot and amber lamps at the top corners.
                    float band = h*.17f;
                    OnBothFaces(band*.5f,band,-w*.5f,w*.5f,.02f,ink);
                    Color stripe = day ? WorldShiftLevelProps.SwitchColour : new Color(.95f,.72f,.2f);
                    for (float sx=-w*.5f+.12f; sx<w*.5f-.12f; sx+=.42f)
                        if (!OverIcon(sx+band*.5f,band*.5f,band)) foreach (float side in new[] {-1f,1f})
                        {
                            float z=side*(front+.006f);
                            art.Quad(new Vector3(x+sx,0,z),new Vector3(x+sx+.14f,0,z),
                                new Vector3(x+sx+.14f+band,band,z),new Vector3(x+sx+band,band,z),stripe);
                            art.Quad(new Vector3(x+sx+band,band,z),new Vector3(x+sx+.14f+band,band,z),
                                new Vector3(x+sx+.14f,0,z),new Vector3(x+sx,0,z),stripe);
                        }
                    foreach (float cx in new[] {-w*.38f,w*.38f})
                        art.Box(new Vector3(x+cx,h-.09f,0),new Vector3(.18f,.14f,d-.02f),new Color(1f,.62f,.12f));
                    break;
                }
                case ShiftLevel.Desert:
                {
                    // Weathered stockade: plank seams, timber end posts and sand drifted against the foot.
                    for (int i=1;i<5;i++) if (i!=2) OnBothFaces(h*i/5f,.035f,-w*.5f,w*.5f,.02f,woodDark);
                    foreach (float px in new[] {-w*.5f+.09f,w*.5f-.09f})
                        art.Box(new Vector3(x+px,h*.5f,0),new Vector3(.18f,h,d),wood);
                    art.Box(new Vector3(x,h*.07f,0),new Vector3(w,h*.14f,d),new Color(.86f,.7f,.47f));
                    break;
                }
                case ShiftLevel.Ocean:
                {
                    // Harbour floodgate: inset framed face, safety tape and bolted steel ribs.
                    OnBothFaces(h*.12f,h*.09f,-w*.5f,w*.5f,.035f,new Color(.87f,.9f,.88f));
                    foreach(float edge in new[]{-1f,1f})
                    {
                        float rib=edge*(w*.5f-.09f);
                        art.ChamferBox(new Vector3(x+rib,h*.48f,0),new Vector3(.16f,h*.88f,d+.025f),.025f,new Color(.23f,.29f,.32f));
                        OnBothFaces(h*.9f,.07f,rib-.07f,rib+.07f,.045f,new Color(1f,.8f,.35f));
                    }
                    // Rubber crown and rivets stay clear of the shared sun/moon symbol.
                    art.Box(new Vector3(x,h-.07f,0),new Vector3(w,.14f,d),new Color(.05f,.05f,.06f));
                    for (float rx=-w*.5f+.18f; rx<w*.5f-.1f; rx+=.34f)
                        foreach (float ry in new[] {h*.2f,h*.78f})
                            if (!OverIcon(rx,ry,.06f)) FaceBox(rx-.03f,rx+.03f,ry,.06f,.03f,ink);
                    break;
                }
                case ShiftLevel.Mountain:
                {
                    // Rockfall wall: staggered stone courses under a timber top rail.
                    Color mortar = day ? new Color(.18f,.15f,.1f) : new Color(.5f,.52f,.55f);
                    foreach (float y in new[] {h*.22f,h*.84f}) OnBothFaces(y,.035f,-w*.5f,w*.5f,.02f,mortar);
                    for (float jx=-w*.5f+.35f; jx<w*.5f; jx+=.7f)
                        if (!OverIcon(jx,h*.11f,h*.22f)) FaceBox(jx-.017f,jx+.017f,h*.11f,h*.22f,.02f,mortar);
                    art.Box(new Vector3(x,h-.06f,0),new Vector3(w,.12f,d),wood);
                    break;
                }
            }
        }

        internal static void Icon(WorldShiftArtMesh art,Vector3 centre,float r,ShiftWorld world,Color ink)
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
