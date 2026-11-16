using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private void AudioControls(Transform parent)
        {
            VolumeSlider(parent,"Music",.62f,WorldShiftAudioSettings.Music,value=>WorldShiftAudioSettings.Music=value);
            VolumeSlider(parent,"Effects",.23f,WorldShiftAudioSettings.Effects,value=>WorldShiftAudioSettings.Effects=value);
        }

        private void MusicCredits(Transform parent)
        {
            var credits=WorldShiftMenu.Panel(pauseCard,"Music credits panel",Vector2.zero,Vector2.one,PauseFace);
            Round(credits,PauseFace);credits.GetComponent<Image>().raycastTarget=true;musicCreditsPanel=credits.gameObject;
            PauseLabel(credits,"THE SOUND OF THE GETAWAY",.06f,.85f,.94f,.92f,12,PauseAccent).characterSpacing=2;
            PauseLabel(credits,"Soundtrack",.06f,.74f,.94f,.85f,36,Cream,FontStyles.Bold);
            string[] levels={"City","Desert","Ocean","Snow","Mountain"};
            for(int i=0;i<levels.Length;i++)
            {
                float top=.69f-i*.068f;
                PauseLabel(credits,levels[i].ToUpperInvariant(),.06f,top-.055f,.28f,top,13,PauseMuted);
                var environment=(ShiftLevel)System.Enum.Parse(typeof(ShiftLevel),levels[i]);
                PauseLabel(credits,WorldShiftMusicLibrary.Title(environment),.30f,top-.055f,.94f,top,20,Cream);
            }
            PauseLabel(credits,"All songs by Kevin MacLeod (incompetech.com)\nCreative Commons: By Attribution 4.0\nRecordings loop with fades and Night filtering.",.06f,.18f,.94f,.36f,15,PauseMuted);
            PauseTextButton(credits,"Song source",new Vector2(.06f,.07f),new Vector2(.32f,.15f),()=>Application.OpenURL(WorldShiftMusicLibrary.SourceUrl(game.Environment)));
            PauseTextButton(credits,"CC BY 4.0",new Vector2(.35f,.07f),new Vector2(.61f,.15f),()=>Application.OpenURL(WorldShiftMusicLibrary.LicenseUrl));
            Button back=null;
            back=PauseTextButton(credits,"Back",new Vector2(.64f,.07f),new Vector2(.94f,.15f),()=>
            {
                credits.gameObject.SetActive(false);pauseBody.interactable=pauseBody.blocksRaycasts=true;pauseBody.alpha=1f;
                if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(creditsLink.gameObject);
            });
            credits.gameObject.SetActive(false);
            creditsLink=PauseTextButton(parent,"Music credits",new Vector2(.735f,.014f),new Vector2(.95f,.06f),()=>
            {
                pauseBody.interactable=pauseBody.blocksRaycasts=false;pauseBody.alpha=0f;credits.gameObject.SetActive(true);credits.SetAsLastSibling();
                if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(back.gameObject);
            });
        }
        private static Button PauseTextButton(Transform parent,string title,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var rect=WorldShiftMenu.Panel(parent,title,min,max,new Color(.08f,.15f,.19f));Round(rect,new Color(.08f,.15f,.19f));
            var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>{if(!WorldShiftSceneLoading.Active) action();});
            var colours=button.colors;colours.normalColor=new Color(.70f,.80f,.85f);colours.highlightedColor=colours.selectedColor=Color.white;colours.fadeDuration=.12f;button.colors=colours;
            PauseLabel(rect,title,.03f,.05f,.97f,.95f,15,PauseAccent).alignment=TextAlignmentOptions.Center;
            return button;
        }
        private void VolumeSlider(Transform parent,string label,float y,float value,UnityEngine.Events.UnityAction<float> changed)
        {
            var row=WorldShiftMenu.Panel(parent,label+" volume",new Vector2(0,y),new Vector2(1,y),Color.clear);
            row.sizeDelta=new Vector2(0,52);
            PauseLabel(row,label,0,.53f,.70f,1,18,Cream);
            var percent=PauseLabel(row,Mathf.RoundToInt(value*100)+"%",.72f,.53f,1,1,15,PauseMuted);
            percent.alignment=TextAlignmentOptions.Right;
            var hit=WorldShiftMenu.Panel(row,"Slider",Vector2.zero,new Vector2(1,.50f),Color.clear);
            hit.GetComponent<Image>().raycastTarget=true;
            var slider=hit.gameObject.AddComponent<Slider>();
            var track=WorldShiftMenu.Panel(hit,"Track",new Vector2(0,.37f),new Vector2(1,.63f),new Color(.13f,.21f,.25f));Round(track,new Color(.13f,.21f,.25f));
            var fillArea=WorldShiftMenu.Panel(hit,"Fill area",new Vector2(0,.37f),new Vector2(1,.63f),Color.clear);
            fillArea.offsetMin=new Vector2(8,0);fillArea.offsetMax=new Vector2(-8,0);
            var fill=WorldShiftMenu.Panel(fillArea,"Fill",Vector2.zero,Vector2.one,PauseAccent);Round(fill,PauseAccent);
            var handles=WorldShiftMenu.Panel(hit,"Handle area",new Vector2(0,.5f),new Vector2(1,.5f),Color.clear);
            handles.offsetMin=new Vector2(8,0);handles.offsetMax=new Vector2(-8,0);
            var handle=WorldShiftMenu.Panel(handles,"Handle",new Vector2(0,.5f),new Vector2(0,.5f),Cream);
            handle.sizeDelta=new Vector2(17,17);handle.GetComponent<Image>().sprite=WorldShiftIcons.Get(ShiftIcon.Circle);
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();
            slider.minValue=0;slider.maxValue=1;slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v=>{changed(v);percent.text=Mathf.RoundToInt(v*100)+"%";});
        }
    }
}
