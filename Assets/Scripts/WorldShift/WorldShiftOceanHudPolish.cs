using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private bool oceanHud;
        private RectTransform oceanCounter;
        private int lastCollection;
        private float collectionPulse, oceanProgress;
        private bool oceanProgressReady;

        private void PolishOceanHud()
        {
            oceanHud=true;
            Color panel=new Color(.025f,.065f,.085f,.76f);
            var badge=(RectTransform)worldIcon.transform.parent;
            badge.anchorMin=badge.anchorMax=new Vector2(.045f,.945f);
            badge.pivot=new Vector2(.5f,.5f);badge.anchoredPosition=Vector2.zero;badge.sizeDelta=new Vector2(58,58);
            Round(badge,panel);worldIcon.rectTransform.sizeDelta=new Vector2(38,38);
            var bar=(RectTransform)progressFill.transform.parent;
            bar.anchorMin=new Vector2(.30f,.950f);bar.anchorMax=new Vector2(.70f,.960f);
            Round(bar,new Color(.035f,.09f,.12f,.68f));
            progressFill.color=new Color(.67f,.91f,.86f);
            foreach(var pip in pips) pip.rectTransform.sizeDelta=new Vector2(10,10);
            var flag=(RectTransform)bar.Find("Finish");flag.sizeDelta=new Vector2(21,21);
            carMarker.sizeDelta=new Vector2(5,16);
            var pauseButton=(RectTransform)driving.transform.Find("Pause button");
            pauseButton.anchorMin=pauseButton.anchorMax=new Vector2(.955f,.945f);
            pauseButton.anchoredPosition=Vector2.zero;pauseButton.sizeDelta=new Vector2(52,52);
            Round(pauseButton,panel);
            foreach(Transform child in pauseButton) ((RectTransform)child).sizeDelta=new Vector2(27,27);
            if(collectionStatus!=null)
            {
                oceanCounter=(RectTransform)collectionStatus.transform.parent;
                oceanCounter.anchorMin=oceanCounter.anchorMax=new Vector2(.5f,.895f);
                oceanCounter.anchoredPosition=Vector2.zero;oceanCounter.sizeDelta=new Vector2(166,38);
                Round(oceanCounter,panel);
                collectionStatus.fontSize=20;collectionStatus.enableAutoSizing=true;
                collectionStatus.fontSizeMin=14;collectionStatus.fontSizeMax=20;
                collectionStatus.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
                collectionStatus.rectTransform.anchorMin=new Vector2(.27f,0);
                collectionStatus.rectTransform.anchorMax=new Vector2(.94f,1);
                var diamond=Icon(oceanCounter,ShiftIcon.Diamond,23);
                diamond.color=WorldShiftLevelProps.CollectionColour;
                diamond.rectTransform.anchoredPosition=new Vector2(-52,0);
                lastCollection=game.Collection.Count;
            }
            var notice=(RectTransform)checkpointNotice.transform;
            notice.sizeDelta=new Vector2(240,40);notice.anchoredPosition=new Vector2(0,-32);
            Round(notice,panel);
            foreach(var text in notice.GetComponentsInChildren<TMPro.TMP_Text>()) text.fontSize=18;
            foreach(var image in notice.GetComponentsInChildren<Image>())
                if(image.transform!=notice)
                {
                    image.rectTransform.sizeDelta=new Vector2(25,25);
                    image.rectTransform.anchoredPosition=new Vector2(-95,0);
                }
        }

        private static void Round(RectTransform panel,Color colour)
        {
            var image=panel.GetComponent<Image>();image.sprite=FreezeRoundedSprite(false);
            image.type=Image.Type.Sliced;image.color=colour;
        }

        private void RefreshOceanHud()
        {
            float delta=Time.unscaledDeltaTime;
            if(!oceanProgressReady || game.Progress<oceanProgress)
            {oceanProgress=game.Progress;oceanProgressReady=true;}
            else oceanProgress=Mathf.Lerp(oceanProgress,game.Progress,1-Mathf.Exp(-10*delta));
            progressFill.rectTransform.anchorMax=new Vector2(oceanProgress,1);
            carMarker.anchorMin=carMarker.anchorMax=new Vector2(oceanProgress,.5f);
            if(oceanCounter!=null)
            {
                int collected=game.Collection.Count;
                if(collected>lastCollection) collectionPulse=.32f;
                if(collected<lastCollection) collectionPulse=0;
                lastCollection=collected;
                collectionPulse=Mathf.Max(0,collectionPulse-delta);
                oceanCounter.localScale=Vector3.one*(1+Mathf.Sin(collectionPulse/.32f*Mathf.PI)*.07f);
                collectionStatus.color=game.MissedRequiredItem ? new Color(1,.62f,.43f) : WorldShiftLevelProps.CollectionColour;
            }
            float remaining=game.CheckpointNoticeRemaining;
            float arrival=Mathf.Clamp01((2.2f-remaining)/.16f);
            checkpointNotice.alpha=game.State==ShiftRunState.Running && remaining>0
                ? Mathf.SmoothStep(0,1,arrival)*Mathf.Clamp01(remaining/.40f) : 0;
            checkpointNotice.transform.localScale=Vector3.one*Mathf.Lerp(.97f,1,Mathf.SmoothStep(0,1,arrival));
        }
    }
}
