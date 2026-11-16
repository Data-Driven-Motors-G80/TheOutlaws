using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private static readonly Color PauseFace = WorldShiftUiStyle.Face;
        private static readonly Color PauseMuted = WorldShiftUiStyle.Muted;
        private static readonly Color PauseAccent = WorldShiftUiStyle.Accent;
        private RectTransform pauseCard;
        private CanvasGroup pauseBody;
        private GameObject musicCreditsPanel;
        private Button pauseResume, creditsLink;
        private bool pauseWasVisible;
        private TMP_Text pauseProgress, pauseWorld;
        private RectTransform pauseProgressFill;
        private Image pauseWorldIcon;

        private static readonly Color CardRim = WorldShiftUiStyle.Rim;

        /// <summary>Shared card chrome for pause, finish, failure, story and Training prompts: soft shadow, fine rim, dark face.</summary>
        private static void CardChrome(RectTransform card, bool shadow = true, float shadowSpread = 5f, float shadowDrop = 12f)
        {
            if (shadow)
            {
                var drop=WorldShiftMenu.Panel(card,"Card shadow",Vector2.zero,Vector2.one,new Color(0,0,0,.3f));
                drop.offsetMin=new Vector2(-shadowSpread,-shadowDrop);drop.offsetMax=new Vector2(shadowSpread,-shadowDrop/3f);
                Round(drop,new Color(0,0,0,.3f));drop.SetAsFirstSibling();
            }
            var rim=WorldShiftMenu.Panel(card,"Fine border",Vector2.zero,Vector2.one,CardRim);Round(rim,CardRim);
            var face=WorldShiftMenu.Panel(rim,"Panel face",Vector2.zero,Vector2.one,PauseFace);
            face.offsetMin=Vector2.one;face.offsetMax=-Vector2.one;Round(face,PauseFace);
        }

        private void BuildPauseScreen(Transform safe)
        {
            pause = WorldShiftMenu.Panel(safe,"Paused",Vector2.zero,Vector2.one,new Color(.008f,.018f,.03f,.78f)).gameObject;
            pause.GetComponent<Image>().raycastTarget=true;
            pauseCard=WorldShiftMenu.Panel(pause.transform,"Pause card",new Vector2(.5f,.5f),new Vector2(.5f,.5f),PauseFace);
            pauseCard.sizeDelta=new Vector2(1060,620);Round(pauseCard,PauseFace);
            CardChrome(pauseCard);
            var body=WorldShiftMenu.Panel(pauseCard,"Pause content",Vector2.zero,Vector2.one,Color.clear);
            pauseBody=body.gameObject.AddComponent<CanvasGroup>();
            BuildPauseDestination(body);
            PauseLabel(body,"Paused",.49f,.765f,.95f,.875f,48,Cream,FontStyles.Bold);
            pauseResume=PauseAction(body,"Resume",new Vector2(.49f,.645f),new Vector2(.95f,.745f),ShiftIcon.Play,()=>{if(game.State==ShiftRunState.Paused) game.TogglePause();},true);
            PauseLabel(pauseResume.transform,"ESC",.82f,.12f,.95f,.88f,12,PauseFace).alignment=TextAlignmentOptions.Right;
            PauseAction(body,"Restart level",new Vector2(.49f,.537f),new Vector2(.715f,.62f),ShiftIcon.Retry,WorldShiftLevels.Retry);
            PauseAction(body,"Level select",new Vector2(.735f,.537f),new Vector2(.95f,.62f),ShiftIcon.Home,WorldShiftLevels.Menu);
            var audio=WorldShiftMenu.Panel(body,"Audio settings",new Vector2(.49f,.20f),new Vector2(.95f,.495f),Color.clear);
            AudioControls(audio);
            WorldShiftMenu.Panel(body,"Controls divider",new Vector2(.49f,.179f),new Vector2(.95f,.181f),new Color(.18f,.26f,.30f,.55f));
            PauseControls(body);
            if(!game.Course.Training) MusicCredits(body);
        }

        private void BuildPauseDestination(Transform body)
        {
            var hero=WorldShiftMenu.Panel(body,"Current destination artwork",new Vector2(.014f,.024f),new Vector2(.442f,.976f),Color.white);
            Round(hero,Color.white);hero.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            var art=WorldShiftMenu.TextureLayer(hero,"Destination illustration");
            art.texture=Resources.Load<Texture2D>("WorldShiftLevelCards/"+WorldShiftLevels.Name(game.LevelIndex));
            var fit=art.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio=art.texture!=null?(float)art.texture.width/art.texture.height:1.5f;
            var shade=WorldShiftMenu.TextureLayer(hero,"Destination contrast");shade.texture=WorldShiftMenu.Shade();
            var world=WorldShiftMenu.Panel(hero,"Paused world",new Vector2(.68f,.875f),new Vector2(.935f,.95f),new Color(.025f,.055f,.075f,.90f));
            Round(world,new Color(.025f,.055f,.075f,.90f));
            pauseWorldIcon=Icon(world,ShiftIcon.Sun,20);
            pauseWorldIcon.rectTransform.anchorMin=pauseWorldIcon.rectTransform.anchorMax=new Vector2(.23f,.5f);
            pauseWorldIcon.rectTransform.anchoredPosition=Vector2.zero;
            pauseWorld=PauseLabel(world,"Day",.43f,0,.96f,1,14,Cream);
            PauseLabel(hero,WorldShiftLevels.Name(game.LevelIndex),.075f,.24f,.94f,.37f,48,Cream,FontStyles.Bold);
            pauseProgress=PauseLabel(hero,"0%",.73f,.103f,.92f,.155f,15,PauseAccent,FontStyles.Bold);
            pauseProgress.alignment=TextAlignmentOptions.Right;
            var track=WorldShiftMenu.Panel(hero,"Route progress track",new Vector2(.08f,.07f),new Vector2(.92f,.079f),new Color(.24f,.35f,.38f));
            Round(track,new Color(.24f,.35f,.38f));
            pauseProgressFill=WorldShiftMenu.Panel(track,"Route progress fill",Vector2.zero,Vector2.up,PauseAccent);
            Round(pauseProgressFill,PauseAccent);
        }

        private static TMP_Text PauseLabel(Transform parent,string text,float x0,float y0,float x1,float y1,float size,Color colour,FontStyles style=FontStyles.Normal)
        {
            var label=WorldShiftMenu.Label(parent,text,x0,y0,x1,y1,size);
            label.color=colour;WorldShiftTypography.Apply(label,style,size);label.verticalAlignment=VerticalAlignmentOptions.Middle;
            return label;
        }
        private static Button PauseAction(Transform parent,string title,Vector2 min,Vector2 max,ShiftIcon icon,Action action,bool primary=false)
        {
            Color face=primary?Gold:new Color(.075f,.125f,.16f);
            var rect=WorldShiftMenu.Panel(parent,title,min,max,face);Round(rect,face);
            var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>{if(!WorldShiftSceneLoading.Active) action();});
            var colours=button.colors;
            colours.normalColor=primary?Color.white:new Color(.65f,.75f,.80f);colours.highlightedColor=primary?new Color(.92f,1f,.88f):Color.white;
            colours.selectedColor=colours.highlightedColor;colours.pressedColor=new Color(.75f,.85f,.85f);colours.fadeDuration=.12f;
            button.colors=colours;
            var glyph=Icon(rect,icon,23);glyph.color=primary?PauseFace:PauseMuted;
            glyph.rectTransform.anchorMin=glyph.rectTransform.anchorMax=new Vector2(.10f,.5f);glyph.rectTransform.anchoredPosition=Vector2.zero;
            PauseLabel(rect,title,.21f,.08f,.96f,.92f,primary?23:17,primary?PauseFace:Cream,primary?FontStyles.Bold:FontStyles.Normal);
            return button;
        }
        private static void PauseControls(Transform parent)
        {
            var row=WorldShiftMenu.Panel(parent,"Controls",new Vector2(.49f,.065f),new Vector2(.95f,.15f),Color.clear);
            SmallKey(row,"A",0,.065f);SmallKey(row,"D",.08f,.145f);
            PauseLabel(row,"Steer",.18f,.40f,.43f,1,15,PauseMuted);
            PauseLabel(row,"or arrow keys",.18f,0,.43f,.43f,11,PauseMuted);
            SmallKey(row,"SPACE",.48f,.65f);PauseLabel(row,"Switch world",.69f,0,1,1,15,PauseMuted);
        }

        private static void SmallKey(Transform parent,string text,float x0,float x1)
        {
            var key=WorldShiftMenu.Panel(parent,"Key "+text,new Vector2(x0,.18f),new Vector2(x1,.82f),new Color(.10f,.16f,.20f));
            Round(key,new Color(.10f,.16f,.20f));
            PauseLabel(key,text,0,0,1,1,13,Cream,FontStyles.Bold).alignment=TextAlignmentOptions.Center;
        }
        private void RefreshPauseScreen()
        {
            bool showing=game.State==ShiftRunState.Paused;
            if(showing)
            {
                var area=(RectTransform)pause.transform;
                float scale=Mathf.Min(1f,(area.rect.width-32f)/1060f,(area.rect.height-32f)/620f);
                pauseCard.localScale=Vector3.one*Mathf.Max(.1f,scale);
                float progress=Mathf.Clamp01(game.Distance/Mathf.Max(1f,game.RunLength));
                pauseProgress.text=Mathf.FloorToInt(progress*100)+"%";
                pauseProgressFill.anchorMax=new Vector2(progress,1);
                bool day=game.World==ShiftWorld.Day;
                pauseWorld.text=day?"Day":"Night";
                pauseWorldIcon.sprite=WorldShiftIcons.Get(day?ShiftIcon.Sun:ShiftIcon.Moon);
                pauseWorldIcon.color=day?Gold:Cream;
                if(!pauseWasVisible)
                {
                    if(musicCreditsPanel!=null) musicCreditsPanel.SetActive(false);
                    pauseBody.interactable=pauseBody.blocksRaycasts=true;pauseBody.alpha=1f;
                    if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(pauseResume.gameObject);
                }
            }
            pauseWasVisible=showing;
        }
    }
}
