using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private Image oceanIntroProgress;
        private void BuildOceanIntro(Transform safe)
        {
            storyRoot=WorldShiftMenu.Panel(safe,"Ocean helicopter fly-in",Vector2.zero,Vector2.one,Color.clear).gameObject;
            storyRoot.GetComponent<Image>().raycastTarget=true;
            var top=WorldShiftMenu.Panel(storyRoot.transform,"Intro top letterbox",new Vector2(0,.84f),Vector2.one,new Color(.012f,.025f,.04f,.94f));
            var bottom=WorldShiftMenu.Panel(storyRoot.transform,"Intro lower letterbox",Vector2.zero,new Vector2(1,.18f),new Color(.012f,.025f,.04f,.94f));
            storyTitle=PauseLabel(top,"AIR SUPPORT INBOUND",.045f,.18f,.77f,.82f,36,Cream,FontStyles.Bold);
            storyTitle.enableAutoSizing=true;storyTitle.fontSizeMin=22;storyTitle.fontSizeMax=36;
            storyLine=PauseLabel(bottom,"The police called for backup.",.045f,.3f,.78f,.82f,26,Cream,FontStyles.Bold);
            storyLine.enableAutoSizing=true;storyLine.fontSizeMin=18;storyLine.fontSizeMax=26;
            PauseLabel(bottom,"OCEAN  /  POLICE AIR UNIT",.045f,.09f,.78f,.31f,13,PauseMuted);
            var skip=PauseAction(top,"Skip",new Vector2(.81f,.23f),new Vector2(.96f,.77f),ShiftIcon.Play,EndCityStory,true);
            PauseLabel(bottom,"ENTER / ESC TO SKIP",.78f,.14f,.96f,.5f,13,PauseMuted).alignment=TextAlignmentOptions.Right;
            var track=WorldShiftMenu.Panel(bottom,"Intro duration",new Vector2(.045f,.05f),new Vector2(.955f,.07f),PauseMuted*.4f);
            oceanIntroProgress=WorldShiftMenu.Panel(track,"Elapsed",Vector2.zero,Vector2.one,PauseAccent).GetComponent<Image>();
            storyKeyboardReady=Time.unscaledTime+.25f;
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(skip.gameObject);
        }
        private void UpdateOceanIntro()
        {
            if(WorldShiftSceneLoading.Active || game.OceanBoss==null) return;
            game.OceanBoss.AdvanceIntro(Time.unscaledDeltaTime);
            var key=Keyboard.current;
            if(game.OceanBoss.IntroClock>=WorldShiftOceanHelicopter.IntroDuration
                || Time.unscaledTime>=storyKeyboardReady && key!=null && (key.escapeKey.wasPressedThisFrame||key.enterKey.wasPressedThisFrame||key.numpadEnterKey.wasPressedThisFrame))
                EndCityStory();
        }
        private void RefreshOceanIntro()
        {
            if(storyRoot==null) return;
            storyRoot.SetActive(game.StoryActive);
            if(!game.StoryActive||game.OceanBoss==null) return;
            float t=game.OceanBoss.IntroClock;
            storyLine.text=t<2.4f ? "The police called for backup." : "Collect rockets. Switch to Night to freeze missiles.";
            oceanIntroProgress.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(t/WorldShiftOceanHelicopter.IntroDuration),1);
        }
    }
}
