using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private Material cityAdvertisementSurface, cityGlassSurface;
        private static readonly string[] CityAdvertisementCopy =
        {
            "JOIN CSCI526",
            "CSCI526 ROCKS!",
            "DOGS > CATS",
            "BUY HAPPINESS",
            "PREMIUM AIR",
            "YOUR AD HERE"
        };
        private static readonly Color[] CityAdvertisementAccents =
        {
            new Color(.20f,.74f,.80f), new Color(.81f,.30f,.62f),
            new Color(.55f,.84f,.28f), new Color(.96f,.43f,.40f),
            new Color(.65f,.48f,.95f), new Color(.30f,.62f,.96f)
        };

        private void Architecture(Transform root, float side, int n, float height, float width, Color colour)
        {
            var body = new WorldShiftArtMesh(); var glass = new WorldShiftArtMesh(); var officeLights = new WorldShiftArtMesh();
            if(cityGlassSurface==null)
            {
                cityGlassSurface=MakeMaterial("City reflective curtain-wall glass",.025f);
                cityGlassSurface.SetFloat("_Finish",5f);
                cityGlassSurface.SetFloat("_Smoothness",.96f);
                cityGlassSurface.SetFloat("_Specular",.72f);
                cityGlassSurface.SetFloat("_Wear",0f);
            }
            var trim = new Color(.24f,.30f,.35f);
            var stone = new Color(.69f,.67f,.61f);
            var darkGlass = new Color(.14f,.24f,.31f);
            // A grounded podium, upper tower and roof cap give each block a silhouette.
            body.Box(new Vector3(0,-.5f,0),new Vector3(width+1f,9f,15.2f),stone);
            float topWidth = n%3==0 ? width*.77f : width;
            Color glazing=Color.Lerp(colour,new Color(.16f,.38f,.49f),.82f);
            glass.Box(new Vector3(0,(height+4)*.5f,0),new Vector3(topWidth,height-4,14),glazing);
            body.Box(new Vector3(0,4.15f,0),new Vector3(width+1.4f,.3f,15.6f),trim);
            body.Box(new Vector3(0,height+.2f,0),new Vector3(topWidth+.6f,.4f,14.6f),stone);
            // Slender aluminium mullions, uninterrupted glazing and a luminous crown.
            foreach(float face in new[]{-1f,1f})
            {
                for(int column=0;column<=5;column++)
                    body.Box(new Vector3(face*(topWidth*.5f+.07f),(height+4f)*.5f,-7f+column*2.8f),
                        new Vector3(.11f,height-4f,.065f),trim);
                for(int column=0;column<=4;column++)
                    body.Box(new Vector3(-topWidth*.5f+column*topWidth/4f,(height+4f)*.5f,face*7.07f),
                        new Vector3(.065f,height-4f,.11f),trim);
            }
            officeLights.Box(new Vector3(0,height-.65f,-7.12f),new Vector3(topWidth,.16f,.06f),new Color(.38f,.72f,.78f));
            body.Box(new Vector3(.8f,height+.95f,2.5f),new Vector3(3.2f,1.5f,3.8f),trim);
            if(n%3==0)
            {
                body.Box(new Vector3(0,height+2.7f,1),new Vector3(2f,2.2f,2.8f),colour);
                body.Cylinder(new Vector3(0,height+3.8f,1),.10f,.025f,3f,trim,6);
            }
            // Nearly edge-to-edge blue panes replace the small inset masonry windows.
            for(float y=5.8f;y<height-1f;y+=3.1f)
            {
                body.Box(new Vector3(0,y-1.5f,0),new Vector3(topWidth+.13f,.065f,14.13f),trim);
                foreach(float face in new[]{-1f,1f})
                {
                    for(int column=0;column<5;column++)
                    {
                        float z=-5.6f+column*2.8f;
                        Color pane=Color.Lerp(glazing,new Color(.50f,.72f,.78f),.20f+.15f*Mathf.Sin(y*.10f+column*.7f+n));
                        glass.Box(new Vector3(face*(topWidth*.5f+.035f),y,z),new Vector3(.04f,3f,2.72f),pane);
                        if((column+(int)y+n)%13==0)
                            officeLights.Box(new Vector3(face*(topWidth*.5f+.062f),y,z),new Vector3(.015f,2.5f,2.3f),new Color(.76f,.72f,.53f));
                    }
                    float paneWidth=topWidth/4f;
                    for(int column=0;column<4;column++)
                    {
                        float x=(column-1.5f)*paneWidth;
                        Color pane=Color.Lerp(glazing,new Color(.55f,.74f,.79f),.22f+.14f*Mathf.Sin(y*.12f+column+n));
                        glass.Box(new Vector3(x,y,face*7.035f),new Vector3(paneWidth-.075f,3f,.04f),pane);
                        if((column+(int)y+n)%11==0)
                            officeLights.Box(new Vector3(x,y,face*7.062f),new Vector3(paneWidth-.3f,2.5f,.015f),new Color(.76f,.72f,.53f));
                    }
                }
            }
            // Shop fronts face the pavement. Awnings use muted colours, never hazard colours.
            float frontage=-side*(width*.5f+.56f);
            for(int z=-5;z<=5;z+=4)
            {
                body.Box(new Vector3(frontage,1.7f,z),new Vector3(.10f,2.7f,3.1f),trim);
                glass.Box(new Vector3(frontage-side*.06f,1.7f,z),new Vector3(.04f,2.35f,2.75f),darkGlass);
                body.Box(new Vector3(frontage-side*.65f,3.25f,z),new Vector3(1.5f,.2f,3.4f),colour*.65f);
            }
            Render(root,"City aluminium framing and shop fronts",body,surface);
            Render(root,"Reflective glass curtain walls",glass,cityGlassSurface);
            Render(root,"Office lights and tower crown",officeLights,windows);
            float screenHeight=16f+n%3*2f;
            CityAdvertisement(root,new Vector3(0,6f+screenHeight*.5f,-7.45f),Quaternion.identity,
                topWidth+.35f,screenHeight,n);
            CityAdvertisement(root,new Vector3(-side*(topWidth*.5f+.3f),height*.46f,0),
                Quaternion.Euler(0,side*90f,0),14.2f,Mathf.Min(22f,height*.52f),n+CityAdvertisementCopy.Length/2);
        }

        private void CityAdvertisement(Transform building,Vector3 position,Quaternion rotation,float width,float height,int variant)
        {
            if(cityAdvertisementSurface==null)
            {
                cityAdvertisementSurface=MakeMaterial("Illuminated City advertisements",.85f);
                cityAdvertisementSurface.SetColor("_BaseColor",Color.white);
            }
            int design=variant%CityAdvertisementCopy.Length;
            string message=CityAdvertisementCopy[design];
            var board=new GameObject("City advertisement - "+message).transform;
            board.SetParent(building,false); board.localPosition=position; board.localRotation=rotation;
            var frame=new WorldShiftArtMesh();
            Color accent=CityAdvertisementAccents[design];
            frame.ChamferBox(Vector3.zero,new Vector3(width+.35f,height+.35f,.30f),.12f,new Color(.12f,.15f,.20f));
            frame.Box(new Vector3(0,0,-.17f),new Vector3(width,height,.04f),accent);
            frame.Box(new Vector3(0,0,-.20f),new Vector3(width-.22f,height-.22f,.03f),
                Color.Lerp(new Color(.018f,.025f,.045f),accent,.11f));
            foreach(float edge in new[]{-1f,1f})
                frame.Box(new Vector3(0,edge*(height*.5f-.65f),-.225f),new Vector3(width-.6f,.32f,.015f),accent);
            Render(board,"Illuminated billboard frame",frame,cityAdvertisementSurface);
            var label=new GameObject("Advertisement lettering",typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(board,false); label.transform.localPosition=new Vector3(0,0,-.24f);
            WorldShiftTypography.Apply(label,FontStyles.Bold,36f);
            label.text=message; label.color=new Color(.98f,.96f,.89f);
            label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=new Vector2(width-1f,height-2.2f);
            label.enableAutoSizing=true; label.fontSizeMin=8f; label.fontSizeMax=22f; label.fontSize=22f;
            label.textWrappingMode=TextWrappingModes.Normal;
            label.overflowMode=TextOverflowModes.Overflow;
            label.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        private void CityFurniture(float at,float side,int index)
        {
            if(NearJump(at,20) || !Clear(at,side*10.8f,2)) return;
            var root=Root("City pavement furniture",at,side*10.8f);
            var mesh=new WorldShiftArtMesh();
            var stone=new Color(.48f,.49f,.46f); var wood=new Color(.39f,.28f,.20f);
            mesh.Box(new Vector3(0,.22f,0),new Vector3(1.5f,.7f,3.8f),stone);
            mesh.Box(new Vector3(0,.59f,0),new Vector3(1.26f,.06f,3.5f),new Color(.19f,.22f,.15f));
            for(int j=-1;j<=1;j++)
                mesh.Rock(new Vector3(0,.6f,j*1.1f),new Vector3(.68f,.72f,.72f),new Color(.27f,.38f,.26f),index+j);
            for(int i=0;i<3;i++) mesh.Box(new Vector3(-side*2.2f,.6f,4.5f+(i-1)*.20f),new Vector3(2.2f,.12f,.16f),wood);
            foreach(float x in new[]{-1f,1f}) mesh.Box(new Vector3(-side*2.2f+x*.8f,.25f,4.5f),new Vector3(.12f,.6f,.6f),stone);
            Render(root,"Planter and bench",mesh,surface);
        }

        private void StreetEdges(RoadSegment segment,float start,float end)
        {
            var filter=segment.transform.Find("Road").GetComponent<MeshFilter>();
            var edges=filter.sharedMesh.vertices;
            var mesh=new WorldShiftArtMesh();
            Color curb=new Color(.61f,.63f,.62f),joint=new Color(.31f,.36f,.38f);
            for(int i=0;i<edges.Length/2-1;i++)
            {
                var a=filter.transform.TransformPoint(edges[i*2]);var b=filter.transform.TransformPoint(edges[(i+1)*2]);
                var ar=filter.transform.TransformPoint(edges[i*2+1]);var br=filter.transform.TransformPoint(edges[(i+1)*2+1]);
                var rightA=(ar-a).normalized; var rightB=(br-b).normalized;
                var upA=segment.Sample(segment.Length*i/(edges.Length/2-1)).Up;
                var upB=segment.Sample(segment.Length*(i+1)/(edges.Length/2-1)).Up;
                foreach(float side in new[]{-1f,1f})
                {
                    Vector3 near=side<0?a:ar, far=side<0?b:br;
                    var x=near+rightA*side*.08f;var y=far+rightB*side*.08f;
                    var xx=near+rightA*side*.30f;var yy=far+rightB*side*.30f;
                    if (side < 0)
                    {
                        mesh.Quad(x+upA*.14f,y+upB*.14f,y,x,curb);
                        mesh.Quad(xx+upA*.14f,yy+upB*.14f,y+upB*.14f,x+upA*.14f,curb);
                    }
                    else
                    {
                        mesh.Quad(x,y,y+upB*.14f,x+upA*.14f,curb);
                        mesh.Quad(x+upA*.14f,y+upB*.14f,yy+upB*.14f,xx+upA*.14f,curb);
                    }
                    if(i%6==0) mesh.Beam(near+rightA*side*.35f+upA*.016f,near+rightA*side*3.45f+upA*.016f,.025f,.025f,joint);
                }
            }
            var root=new GameObject("City curb and pavement joints").transform;
            root.SetParent(transform,false);
            Render(root,"Road-following kerb",mesh,surface);
            chunks.Add(new Chunk{At=(start+end)*.5f,Root=root.gameObject,Deck=true});
        }

        private static void PalmDetailed(WorldShiftArtMesh mesh,Vector3 foot,float height,int seed)
        {
            var bark=new Color(.40f,.27f,.16f);
            var lean=new Vector3(.6f*Mathf.Sin(seed*2f),0,.7f*Mathf.Cos(seed*2f));
            for(int i=0;i<9;i++)
            {
                float t=i/9f,next=(i+1)/9f;
                Vector3 a=foot+Vector3.up*height*t+lean*t*t,b=foot+Vector3.up*height*next+lean*next*next;
                mesh.Beam(a,b,.39f-t*.2f,.39f-t*.2f,bark*(1f+(i%2)*.1f));
            }
            Vector3 crown=foot+Vector3.up*height+lean;
            for(int j=0;j<11;j++)
            {
                float angle=(j*137.5f+seed*17)*Mathf.Deg2Rad;
                var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)); var cross=Vector3.Cross(Vector3.up,dir);
                float length= j<7?3.9f:2.6f;
                Color green=j%3==0?new Color(.39f,.55f,.22f):new Color(.19f,.39f,.20f);
                Vector3 Leaf(float t)=>crown+dir*(length*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.9f-t*t*(j<7?1.3f:.1f));
                for(int k=0;k<7;k++)
                {
                    float t=k/7f,u=(k+1)/7f;
                    float w=Mathf.Sin(t*Mathf.PI)*.57f,ww=Mathf.Sin(u*Mathf.PI)*.57f;
                    var a=Leaf(t);var b=Leaf(u);
                    mesh.Triangle(a+Vector3.up*.055f,a-cross*w,b-cross*ww,green,true);
                    mesh.Triangle(a+Vector3.up*.055f,b-cross*ww,b+Vector3.up*.055f,green,true);
                    mesh.Triangle(a+Vector3.up*.055f,b+Vector3.up*.055f,b+cross*ww,green*.84f,true);
                    mesh.Triangle(a+Vector3.up*.055f,b+cross*ww,a+cross*w,green*.84f,true);
                    if(k>0 && k<6)
                        foreach(float side in new[]{-1f,1f})
                            mesh.Triangle(a,a+cross*side*(w+.25f)-dir*.2f-Vector3.up*.12f,b,green,true);
                }
            }
            for(int i=0;i<4;i++) mesh.Rock(crown+new Vector3(Mathf.Sin(i*1.57f)*.27f,-.40f,Mathf.Cos(i*1.57f)*.27f),new Vector3(.23f,.32f,.23f),bark,i);
        }

        internal static Mesh DuneMesh(int form)
        {
            const int columns=24,rows=24;
            var points=new Vector3[(columns+1)*(rows+1)];var indices=new int[columns*rows*6];
            for(int z=0;z<=rows;z++)
            {
                float along=z/(float)rows*2-1;
                float envelope=Mathf.Sqrt(Mathf.Max(0,1-along*along));
                float crest=.12f+.18f*Mathf.Sin(along*2.4f+form);
                for(int x=0;x<=columns;x++)
                {
                    float across=x/(float)columns*2-1;
                    float h=across<crest?Mathf.Pow((across+1)/(crest+1),.85f):Mathf.Pow((1-across)/(1-crest),1.45f);
                    points[z*(columns+1)+x]=new Vector3(across*envelope*.5f,(h*.60f-.13f)*Mathf.Pow(envelope,.7f)-.04f,along*.5f);
                }
            }
            int n=0;
            for(int z=0;z<rows;z++) for(int x=0;x<columns;x++)
            {
                int a=z*(columns+1)+x,b=a+columns+1;
                indices[n++]=a;indices[n++]=b;indices[n++]=a+1;
                indices[n++]=a+1;indices[n++]=b;indices[n++]=b+1;
            }
            var mesh=new Mesh{name="Wind-shaped desert ridge "+form};
            mesh.vertices=points;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
