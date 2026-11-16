using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftMenu
    {
        private static readonly Color RouteTeal = new Color(.42f,.86f,.78f);
        private static readonly Color Muted = new Color(.55f,.67f,.73f);
        private static Sprite roundedCard;
        private static Texture2D cardShade;
        private RectTransform menuSafe;
        private GridLayoutGroup routeGrid;
        private TMP_Text completedLabel;
        private Vector2 lastGridSize = new Vector2(-1,-1);
        private Rect lastSafeArea;
        private static readonly string[] Descriptions={"Learn the Day / Night switch","","","Collect every coastal diamond","",""};

        private void BuildMenuPresentation(RectTransform root)
        {
            Panel(root,"Midnight background",Vector2.zero,Vector2.one,new Color(.018f,.035f,.055f));
            var glow=Panel(root,"Header atmosphere",new Vector2(.55f,.76f),Vector2.one,Color.clear);
            var wash=TextureLayer(glow,"Atmosphere wash");
            wash.texture=Shade();wash.color=new Color(.22f,.55f,.58f,.10f);wash.raycastTarget=false;
            menuSafe=Panel(root,"Safe area",Vector2.zero,Vector2.one,Color.clear);
            Rect area=Screen.safeArea;
            menuSafe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            menuSafe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            var stripe=Panel(menuSafe,"Brand accent",new Vector2(.05f,.955f),new Vector2(.10f,.961f),RouteTeal);RoundCard(stripe);
            var wordmark=MenuText(menuSafe,"WORLDSHIFT",.05f,.86f,.70f,.94f,50,Cream,FontStyles.Bold);wordmark.characterSpacing=3;
            MenuText(menuSafe,"Choose your escape",.05f,.795f,.70f,.85f,22,Muted);
            completedLabel=MenuText(menuSafe,"00 / 06",.75f,.82f,.95f,.90f,30,RouteTeal,FontStyles.Bold);
            completedLabel.alignment=TextAlignmentOptions.Right;
            levels=Panel(menuSafe,"Levels",new Vector2(.05f,.13f),new Vector2(.95f,.755f),Color.clear);
            routeGrid=levels.gameObject.AddComponent<GridLayoutGroup>();
            routeGrid.spacing=new Vector2(18,18);routeGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;
            testBadge=Panel(menuSafe,"All levels open (Shift+U)",new Vector2(.75f,.035f),new Vector2(.95f,.09f),Color.clear).gameObject;
            Badge((RectTransform)testBadge.transform,ShiftIcon.Unlocked,Gold,18,new Vector2(.08f,.5f));
            MenuText(testBadge.transform,"TESTING",.20f,0,1,1,12,Gold).alignment=TextAlignmentOptions.Right;
        }

        private RectTransform DestinationCard(int level,bool open)
        {
            bool beaten=WorldShiftLevels.IsBeaten(level);
            bool next=open&&!beaten&&level==Mathf.Clamp(WorldShiftLevels.HighestBeaten+1,0,WorldShiftLevels.Count-1);
            var tile=Panel(levels,"Level "+level,Vector2.zero,Vector2.one,Color.white);RoundCard(tile);
            var border=tile.GetComponent<Image>();border.raycastTarget=true;
            var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=border;
            var colours=button.colors;
            colours.normalColor=next?RouteTeal:new Color(.12f,.21f,.26f);
            colours.highlightedColor=colours.selectedColor=RouteTeal;colours.pressedColor=Gold;colours.fadeDuration=.12f;
            button.colors=colours;
            button.onClick.AddListener(()=>
            {
                if (WorldShiftSceneLoading.Active) return;
                WorldShiftLevelAnalytics.LevelPicked(level,WorldShiftLevels.IsUnlocked(level));
                WorldShiftLevels.Play(level);
            });
            var face=Panel(tile,"Destination artwork",Vector2.zero,Vector2.one,new Color(.04f,.08f,.11f));
            face.offsetMin=Vector2.one*2;face.offsetMax=Vector2.one*-2;RoundCard(face);face.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            var artObject=new GameObject("Environment illustration",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
            var artRect=(RectTransform)artObject.transform;artRect.SetParent(face,false);artRect.anchorMin=Vector2.zero;artRect.anchorMax=Vector2.one;artRect.offsetMin=artRect.offsetMax=Vector2.zero;
            var art=artObject.GetComponent<RawImage>();art.texture=Resources.Load<Texture2D>("WorldShiftLevelCards/"+WorldShiftLevels.Name(level));
            art.raycastTarget=false;art.color=open?Color.white:new Color(.40f,.47f,.54f,1f);
            var fitter=artObject.GetComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio=art.texture!=null?(float)art.texture.width/art.texture.height:1.5f;
            var shade=Panel(face,"Artwork shade",Vector2.zero,Vector2.one,Color.clear);
            var gradient=TextureLayer(shade,"Contrast gradient");gradient.texture=Shade();gradient.raycastTarget=false;
            var index=Panel(face,"Route number",new Vector2(.045f,.76f),new Vector2(.17f,.94f),new Color(.02f,.05f,.075f,.90f));RoundCard(index);
            MenuText(index,level.ToString("00"),0,0,1,1,15,open?Cream:Muted,FontStyles.Bold).alignment=TextAlignmentOptions.Center;
            if(beaten) Badge(face,ShiftIcon.Check,RouteTeal,24,new Vector2(.91f,.85f));
            MenuText(face,WorldShiftLevels.Name(level),.055f,.18f,.85f,.37f,32,open?Cream:new Color(.76f,.80f,.81f),FontStyles.Bold);
            string description=open?Descriptions[level]:"Complete "+WorldShiftLevels.Name(level-1)+" to unlock";
            if(!string.IsNullOrEmpty(description)) MenuText(face,description,.055f,.055f,.87f,.18f,12,open?new Color(.71f,.79f,.81f):Muted);
            Badge(face,open?ShiftIcon.Next:ShiftIcon.Lock,open?RouteTeal:Muted,19,new Vector2(.93f,.17f));
            return tile;
        }
        private void UpdateMenuLayout()
        {
            if(routeGrid==null||menuSafe==null) return;
            Rect area=Screen.safeArea;
            if(area!=lastSafeArea)
            {
                lastSafeArea=area;
                menuSafe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
                menuSafe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
                menuSafe.ForceUpdateRectTransforms();
            }
            int columns=menuSafe.rect.width/menuSafe.rect.height<1.2f?2:3;
            var size=levels.rect.size;
            if(size==lastGridSize && routeGrid.constraintCount==columns) return;
            lastGridSize=size;
            routeGrid.constraintCount=columns;
            int rows=WorldShiftLevels.Count/columns;
            routeGrid.cellSize=new Vector2(Mathf.Max(1,(size.x-18*(columns-1))/columns),Mathf.Max(1,(size.y-18*(rows-1))/rows));
        }
        internal static RawImage TextureLayer(Transform parent,string name)
        {
            var root=new GameObject(name,typeof(RectTransform),typeof(RawImage));
            var rect=(RectTransform)root.transform;rect.SetParent(parent,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=root.GetComponent<RawImage>();image.raycastTarget=false;return image;
        }
        private static TMP_Text MenuText(Transform parent,string text,float x0,float y0,float x1,float y1,float size,Color colour,FontStyles style=FontStyles.Normal)
        {
            var label=Label(parent,text,x0,y0,x1,y1,size);label.color=colour;WorldShiftTypography.Apply(label,style,size);label.verticalAlignment=VerticalAlignmentOptions.Middle;
            return label;
        }
        private static void RoundCard(RectTransform rect)
        {
            if(roundedCard==null)
            {
                const int size=64;const float radius=10;
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Menu rounded panel",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                {
                    float dx=Mathf.Max(Mathf.Abs(x+.5f-size/2f)-(size/2f-radius),0);
                    float dy=Mathf.Max(Mathf.Abs(y+.5f-size/2f)-(size/2f-radius),0);
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy)));
                }
                texture.SetPixels(pixels);texture.Apply(false,true);
                roundedCard=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
            }
            var image=rect.GetComponent<Image>();image.sprite=roundedCard;image.type=Image.Type.Sliced;
        }
        internal static Texture2D Shade()
        {
            if(cardShade!=null) return cardShade;
            cardShade=new Texture2D(2,64,TextureFormat.RGBA32,false){name="Destination card shade",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++)
            {
                float t=y/63f;
                float alpha=Mathf.Lerp(.97f,.03f,Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.80f)));
                for(int x=0;x<2;x++) cardShade.SetPixel(x,y,new Color(.012f,.025f,.045f,alpha));
            }
            cardShade.Apply(false,true);return cardShade;
        }
    }
}
