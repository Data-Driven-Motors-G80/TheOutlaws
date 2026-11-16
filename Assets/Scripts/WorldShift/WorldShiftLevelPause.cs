using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private static readonly Color PauseFace = new Color(.035f,.065f,.09f,.98f);
        private static readonly Color PauseMuted = new Color(.55f,.66f,.71f);
        private static readonly Color PauseAccent = new Color(.42f,.86f,.78f);
        private RectTransform pauseCard;
        private CanvasGroup pauseBody;
        private GameObject musicCreditsPanel;
        private Button pauseResume, creditsLink;
        private bool pauseWasVisible;

        private void BuildPauseScreen(Transform safe)
        {
            pause = WorldShiftMenu.Panel(safe,"Paused",Vector2.zero,Vector2.one,new Color(.008f,.018f,.03f,.78f)).gameObject;
            pause.GetComponent<Image>().raycastTarget=true;
            pauseCard=WorldShiftMenu.Panel(pause.transform,"Pause card",new Vector2(.5f,.5f),new Vector2(.5f,.5f),PauseFace);
            pauseCard.sizeDelta=new Vector2(860,600);Round(pauseCard,PauseFace);
            var shadow=WorldShiftMenu.Panel(pauseCard,"Card shadow",Vector2.zero,Vector2.one,new Color(0,0,0,.25f));
            shadow.offsetMin=new Vector2(-5,-12);shadow.offsetMax=new Vector2(5,-4);Round(shadow,new Color(0,0,0,.25f));shadow.SetAsFirstSibling();
            var body=WorldShiftMenu.Panel(pauseCard,"Pause content",Vector2.zero,Vector2.one,Color.clear);
            pauseBody=body.gameObject.AddComponent<CanvasGroup>();
            var accent=WorldShiftMenu.Panel(body,"Accent",new Vector2(.05f,.945f),new Vector2(.12f,.952f),PauseAccent);
            Round(accent,PauseAccent);
            PauseLabel(body,"GETAWAY ON HOLD",.05f,.865f,.60f,.913f,13,PauseMuted).characterSpacing=3;
            PauseLabel(body,"Paused",.05f,.775f,.60f,.87f,44,Cream,FontStyles.Bold);
            var level=WorldShiftMenu.Panel(body,"Current level",new Vector2(.70f,.845f),new Vector2(.95f,.915f),new Color(.07f,.12f,.15f));
            Round(level,new Color(.07f,.12f,.15f));
            PauseLabel(level,game.LevelIndex.ToString("00")+" / "+WorldShiftLevels.Name(game.LevelIndex).ToUpperInvariant(),.06f,0,.94f,1,15,PauseAccent).alignment=TextAlignmentOptions.Center;
            WorldShiftMenu.Panel(body,"Column divider",new Vector2(.485f,.35f),new Vector2(.4865f,.75f),new Color(.18f,.26f,.30f,.55f));
            pauseResume=PauseAction(body,"Resume getaway",new Vector2(.05f,.635f),new Vector2(.44f,.745f),ShiftIcon.Play,game.TogglePause,true);
            PauseAction(body,"Restart level",new Vector2(.05f,.51f),new Vector2(.44f,.605f),ShiftIcon.Retry,WorldShiftLevels.Retry);
            PauseAction(body,"Level select",new Vector2(.05f,.39f),new Vector2(.44f,.485f),ShiftIcon.Home,WorldShiftLevels.Menu);
            var audio=WorldShiftMenu.Panel(body,"Audio settings",new Vector2(.535f,.34f),new Vector2(.95f,.76f),Color.clear);
            AudioControls(audio);
            WorldShiftMenu.Panel(body,"Footer divider",new Vector2(.05f,.285f),new Vector2(.95f,.287f),new Color(.18f,.26f,.30f,.55f));
            PauseControls(body);
            PauseLabel(body,"THE OUTLAWS",.05f,.055f,.40f,.10f,12,PauseMuted).characterSpacing=3;
            if(!game.Course.Training) MusicCredits(body);
        }
        private static TMP_Text PauseLabel(Transform parent,string text,float x0,float y0,float x1,float y1,float size,Color colour,FontStyles style=FontStyles.Normal)
        {
            var label=WorldShiftMenu.Label(parent,text,x0,y0,x1,y1,size);
            label.color=colour;label.fontStyle=style;label.verticalAlignment=VerticalAlignmentOptions.Middle;
            return label;
        }
        private static Button PauseAction(Transform parent,string title,Vector2 min,Vector2 max,ShiftIcon icon,Action action,bool primary=false)
        {
            Color face=primary?Gold:new Color(.075f,.125f,.16f);
            var rect=WorldShiftMenu.Panel(parent,title,min,max,face);Round(rect,face);
            var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
            var colours=button.colors;
            colours.normalColor=primary?Color.white:new Color(.65f,.75f,.80f);colours.highlightedColor=primary?new Color(.92f,1f,.88f):Color.white;
            colours.selectedColor=colours.highlightedColor;colours.pressedColor=new Color(.75f,.85f,.85f);colours.fadeDuration=.12f;
            button.colors=colours;
            var glyph=Icon(rect,icon,23);glyph.color=primary?PauseFace:PauseMuted;
            glyph.rectTransform.anchorMin=glyph.rectTransform.anchorMax=new Vector2(.105f,.5f);glyph.rectTransform.anchoredPosition=Vector2.zero;
            PauseLabel(rect,title,.21f,.08f,.96f,.92f,21,primary?PauseFace:Cream,primary?FontStyles.Bold:FontStyles.Normal);
            return button;
        }
        private static void PauseControls(Transform parent)
        {
            var row=WorldShiftMenu.Panel(parent,"Controls",new Vector2(.05f,.145f),new Vector2(.95f,.24f),Color.clear);
            SmallKey(row,"A",.0f,.048f);SmallKey(row,"D",.06f,.108f);
            PauseLabel(row,"Steer",.13f,.43f,.33f,1,16,PauseMuted);
            PauseLabel(row,"or arrow keys",.13f,0,.33f,.43f,10,PauseMuted);
            SmallKey(row,"SPACE",.36f,.49f);PauseLabel(row,"Switch world",.515f,0,.75f,1,16,PauseMuted);
            SmallKey(row,"ESC",.79f,.87f);PauseLabel(row,"Resume",.89f,0,1,1,16,PauseMuted);
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
                float scale=Mathf.Min(1f,(area.rect.width-32f)/860f,(area.rect.height-32f)/600f);
                pauseCard.localScale=Vector3.one*Mathf.Max(.1f,scale);
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
