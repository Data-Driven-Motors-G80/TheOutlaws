using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private Material cityShopPosters, cityShopSigns;
        private static readonly string[] CityShopNames =
            { "SALON", "BARBER SHOP", "LIQUOR STORE", "STRIP CLUB", "PIZZA", "COFFEE", "MINI MART", "RECORDS" };
        private static readonly Color[] CityShopColours =
        {
            new Color(.82f,.35f,.53f),new Color(.32f,.51f,.65f),new Color(.28f,.58f,.49f),new Color(.62f,.25f,.67f),
            new Color(.76f,.32f,.25f),new Color(.62f,.44f,.29f),new Color(.43f,.61f,.30f),new Color(.30f,.43f,.73f)
        };

        private void CityShop(Transform root,float side,int index,float width,float at)
        {
            if(cityShopPosters==null)
            {
                cityShopPosters=new Material(Resources.Load<Shader>("WorldShiftCityShopPoster")) { name="Illustrated City shop posters" };
                cityShopPosters.SetTexture("_BaseMap",Resources.Load<Texture2D>("WorldShiftCityShops"));
                cityShopPosters.SetColor("_Tint",Color.white); materials.Add(cityShopPosters);
                cityShopSigns=MakeMaterial("City shop illuminated signs",.8f);
            }
            int kind=index%CityShopNames.Length;
            Color colour=CityShopColours[kind], dark=new Color(.095f,.12f,.15f), stone=new Color(.64f,.61f,.55f);
            float height=6.6f+(index%3)*2f;
            var body=new WorldShiftArtMesh(); var light=new WorldShiftArtMesh(); var glazing=new WorldShiftArtMesh();
            body.Box(new Vector3(0,height*.5f-.25f,0),new Vector3(width+1f,height+.5f,15.2f),Color.Lerp(colour,stone,.35f));
            body.Box(new Vector3(0,.23f,0),new Vector3(width+1.15f,.45f,15.35f),dark);
            body.Box(new Vector3(0,height+.1f,0),new Vector3(width+1.35f,.3f,15.5f),dark);
            body.Box(new Vector3(0,height+.4f,0),new Vector3(width+.8f,.3f,15f),colour);
            // Alternate a low parapet, stepped roof, and taller upper-floor silhouette.
            if(index%3==1)
                body.Box(new Vector3(0,height+.8f,-5.5f),new Vector3(width*.6f,1.1f,.35f),colour);
            else if(index%3==2)
                body.Box(new Vector3(side*2f,height+.85f,3.7f),new Vector3(2.8f,1.4f,2.2f),dark);
            for(float y=6.1f;y<height-.8f;y+=2.4f)
                foreach(float z in new[]{-4.8f,0f,4.8f})
                {
                    float x=-side*(width*.5f+.55f);
                    body.Box(new Vector3(x,y,z),new Vector3(.12f,1.75f,2.2f),dark);
                    glazing.Box(new Vector3(x-side*.08f,y,z),new Vector3(.035f,1.48f,1.94f),new Color(.25f,.44f,.52f));
                }
            // Street-facing shopfront: a recessed entrance between two display windows.
            float frontage=-side*(width*.5f+.57f);
            foreach(float z in new[]{-4.7f,4.7f})
            {
                body.Box(new Vector3(frontage,1.9f,z),new Vector3(.12f,2.9f,3.9f),dark);
                glazing.Box(new Vector3(frontage-side*.08f,1.9f,z),new Vector3(.035f,2.64f,3.6f),new Color(.10f,.22f,.28f));
            }
            body.Box(new Vector3(frontage,1.65f,0),new Vector3(.12f,3f,1.8f),dark);
            glazing.Box(new Vector3(frontage-side*.08f,1.65f,0),new Vector3(.035f,2.7f,1.55f),new Color(.16f,.30f,.35f));
            light.Box(new Vector3(frontage-side*.13f,1.45f,.57f),new Vector3(.05f,.35f,.055f),stone);
            // Distinct awnings and bands keep each business recognisable while passing.
            body.Box(new Vector3(frontage-side*.45f,3.58f,0),new Vector3(1.25f,.16f,14.5f),colour*.7f);
            if(kind==0 || kind==1 || kind==4 || kind==5)
                for(int stripe=0;stripe<14;stripe++)
                    body.Box(new Vector3(frontage-side*.45f,3.67f,-6.5f+stripe),new Vector3(1.25f,.035f,.43f),stone);
            light.Box(new Vector3(frontage-side*.13f,4.5f,0),new Vector3(.12f,1.4f,14.2f),colour*.65f);
            light.Box(new Vector3(0,4.5f,-7.72f),new Vector3(width+.5f,1.4f,.12f),colour*.65f);
            Render(root,"Shop masonry, entrance and awnings",body,surface);
            Render(root,"Shop display glazing",glazing,windows);
            Render(root,"Illuminated shop sign boards",light,cityShopSigns);
            Quaternion streetFace=Quaternion.Euler(0,side*90f,0);
            ShopLabel(root,new Vector3(frontage-side*.23f,4.5f,0),streetFace,13.6f,CityShopNames[kind]);
            ShopLabel(root,new Vector3(0,4.5f,-7.80f),Quaternion.identity,width,CityShopNames[kind]);
            // Real illustrated panels on the display window and the approach-facing wall.
            ShopPoster(root,new Vector3(frontage-side*.15f,1.95f,-4.7f),streetFace,1.4f,2.65f,kind);
            ShopPoster(root,new Vector3(-side*width*.29f,1.95f,-7.72f),Quaternion.identity,1.4f,2.65f,kind);
            // Large roof-mounted pictures face approaching traffic, instead of relying
            // on small windows that only become visible beside the car.
            ShopLandmarkSign(root,new Vector3(0,height+5.4f,-6.95f),Quaternion.identity,kind,5.2f,9.8f);
            var supports=new WorldShiftArtMesh();
            foreach(float x in new[]{-1.9f,1.9f})
                supports.Box(new Vector3(x,height+1.6f,-6.7f),new Vector3(.18f,3.2f,.22f),dark);
            Render(root,"Rooftop shop billboard supports",supports,surface);
            if(index%3==0) CityShopPoleSign(at-9f,side,kind);
            if(kind==1) // A modelled red/white/blue barber pole beside the door.
                for(int band=0;band<9;band++)
                {
                    var pole=new WorldShiftArtMesh();
                    pole.Cylinder(new Vector3(frontage-side*.30f,1f+band*.18f,1.35f),.16f,.16f,.18f,
                        band%3==0 ? new Color(.8f,.17f,.18f) : band%3==1 ? Color.white : new Color(.19f,.34f,.65f),8);
                    Render(root,"Barber pole stripe",pole,cityShopSigns);
                }
        }

        private void ShopLandmarkSign(Transform parent,Vector3 position,Quaternion rotation,int kind,float width,float pictureHeight)
        {
            var sign=new GameObject("Large illustrated sign - "+CityShopNames[kind]).transform;
            sign.SetParent(parent,false); sign.localPosition=position; sign.localRotation=rotation;
            var frame=new WorldShiftArtMesh();
            Color accent=CityShopColours[kind];
            frame.ChamferBox(new Vector3(0,.95f,0),new Vector3(width+.45f,pictureHeight+2.35f,.4f),.15f,new Color(.055f,.075f,.10f));
            frame.Box(new Vector3(0,.95f,-.22f),new Vector3(width+.15f,pictureHeight+2.05f,.025f),accent);
            frame.Box(new Vector3(0,pictureHeight*.5f+1.0f,-.25f),new Vector3(width,1.7f,.025f),accent*.45f);
            Render(sign,"Large illuminated shop sign frame",frame,cityShopSigns);
            ShopPoster(sign,new Vector3(0,0,-.27f),Quaternion.identity,width,pictureHeight,kind);
            ShopLabel(sign,new Vector3(0,pictureHeight*.5f+1f,-.29f),Quaternion.identity,width-.35f,CityShopNames[kind],1.5f);
        }

        private void CityShopPoleSign(float at,float side,int kind)
        {
            const float lateral=12.5f;
            // Include the full elevated sign width when checking every road arm.
            // The pole stays behind the footpath, with no gameplay colliders.
            if(NearJump(at,15f) || !Clear(at,side*lateral,3.3f)) return;
            var root=Root("Tall shop pole sign - "+CityShopNames[kind],at,side*lateral);
            var pole=new WorldShiftArtMesh();
            Color metal=new Color(.18f,.22f,.27f);
            pole.Box(new Vector3(0,.2f,0),new Vector3(.9f,.4f,.9f),metal);
            pole.Cylinder(Vector3.zero,.22f,.16f,8.5f,metal,8);
            pole.Box(new Vector3(0,8.25f,0),new Vector3(2.2f,.18f,.22f),metal);
            Render(root,"Tall shop sign pole",pole,surface);
            // A slight inward angle makes the image readable from the driving lane.
            ShopLandmarkSign(root,new Vector3(0,10.8f,0),Quaternion.Euler(0,side*20f,0),kind,4f,7.55f);
        }

        private void ShopLabel(Transform parent,Vector3 position,Quaternion rotation,float width,string message,float height=1.14f)
        {
            var label=new GameObject("Shop sign - "+message,typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(parent,false); label.transform.localPosition=position; label.transform.localRotation=rotation;
            WorldShiftTypography.Apply(label,FontStyles.Bold,36f);
            label.text=message; label.color=new Color(1f,.96f,.84f); label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=new Vector2(width,height);
            label.enableAutoSizing=true; label.fontSize=height*7.5f; label.fontSizeMax=height*7.5f; label.fontSizeMin=4.5f;
            label.textWrappingMode=TextWrappingModes.NoWrap;
            label.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        private void ShopPoster(Transform parent,Vector3 position,Quaternion rotation,float width,float height,int tile)
        {
            float x=width*.5f,y=height*.5f;
            float u=(tile%4)*.25f,v=tile<4 ? .5f : 0f;
            var mesh=new Mesh { name="Illustrated shop poster - "+CityShopNames[tile] };
            mesh.vertices=new[]{new Vector3(-x,-y,0),new Vector3(x,-y,0),new Vector3(x,y,0),new Vector3(-x,y,0)};
            mesh.uv=new[]{new Vector2(u+.004f,v+.004f),new Vector2(u+.246f,v+.004f),new Vector2(u+.246f,v+.496f),new Vector2(u+.004f,v+.496f)};
            mesh.triangles=new[]{0,2,1,0,3,2}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var poster=new GameObject("Shop illustration - "+CityShopNames[tile],typeof(MeshFilter),typeof(MeshRenderer)).transform;
            poster.SetParent(parent,false); poster.localPosition=position; poster.localRotation=rotation;
            poster.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=poster.GetComponent<MeshRenderer>(); renderer.sharedMaterial=cityShopPosters;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
}
