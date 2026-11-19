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
        private Image[] helicopterHealth;

        private void PolishOceanHud()
        {
            oceanHud=true;
            Color panel=new Color(.025f,.065f,.085f,.76f);
            var badge=(RectTransform)worldIcon.transform.parent;
            badge.anchorMin=badge.anchorMax=new Vector2(.045f,.945f);
            badge.pivot=new Vector2(.5f,.5f);badge.anchoredPosition=Vector2.zero;badge.sizeDelta=new Vector2(58,58);
            Round(badge,panel);worldIcon.rectTransform.sizeDelta=new Vector2(38,38);
            // Keep the shared progress bar and marker sizes identical in every level.
            var pauseButton=(RectTransform)driving.transform.Find("Pause button");
            pauseButton.anchorMin=pauseButton.anchorMax=new Vector2(.955f,.945f);
            pauseButton.anchoredPosition=Vector2.zero;pauseButton.sizeDelta=new Vector2(52,52);
            Round(pauseButton,panel);
            foreach(Transform child in pauseButton)
                if(child.name!="Button face") ((RectTransform)child).sizeDelta=new Vector2(27,27);
            if(collectionStatus!=null)
            {
                oceanCounter=(RectTransform)collectionStatus.transform.parent;
                oceanCounter.anchorMin=oceanCounter.anchorMax=new Vector2(.5f,.88f);
                oceanCounter.anchoredPosition=Vector2.zero;oceanCounter.sizeDelta=new Vector2(245,38);
                Round(oceanCounter,panel);
                collectionStatus.fontSize=20;collectionStatus.enableAutoSizing=true;
                collectionStatus.fontSizeMin=14;collectionStatus.fontSizeMax=20;
                collectionStatus.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
                collectionStatus.rectTransform.anchorMin=new Vector2(.27f,0);
                collectionStatus.rectTransform.anchorMax=new Vector2(.94f,1);
                var diamond=Icon(oceanCounter,ShiftIcon.Siren,23);
                diamond.color=WorldShiftLevelProps.CollectionColour;
                diamond.rectTransform.anchoredPosition=new Vector2(-95,0);
                lastCollection=game.Collection.Count;
                helicopterHealth=new Image[WorldShiftOceanHelicopter.MaximumHealth];
                for(int i=0;i<helicopterHealth.Length;i++) helicopterHealth[i]=WorldShiftMenu.Panel(oceanCounter,"Helicopter armour "+i,new Vector2(.09f+i*(.82f/helicopterHealth.Length),.04f),new Vector2(.09f+(i+.86f)*(.82f/helicopterHealth.Length),.13f),WorldShiftLevelProps.CollectionColour).GetComponent<Image>();
                var help=WorldShiftMenu.Panel(oceanCounter,"Air pursuit hint",new Vector2(-.22f,-.72f),new Vector2(1.22f,-.12f),panel);Round(help,panel);
                var hint=WorldShiftMenu.Label(help,"DAY: AMMO > AUTO FIRE   /   NIGHT: FREEZE",.02f,0,.98f,1,13);
                hint.alignment=TMPro.TextAlignmentOptions.Center; hint.color=new Color(.85f,.93f,1f);
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
                if(helicopterHealth!=null && game.OceanBoss!=null)
                    for(int i=0;i<helicopterHealth.Length;i++) helicopterHealth[i].color=i<game.OceanBoss.DisplayHealth ? WorldShiftLevelProps.CollectionColour : new Color(.16f,.24f,.26f);
                if(collected>lastCollection) collectionPulse=.32f;
                if(collected<lastCollection) collectionPulse=0;
                lastCollection=collected;
                collectionPulse=Mathf.Max(0,collectionPulse-delta);
                oceanCounter.localScale=Vector3.one*(1+Mathf.Sin(collectionPulse/.32f*Mathf.PI)*.07f);
                collectionStatus.color=game.MissedRequiredItem ? new Color(1,.62f,.43f) : WorldShiftLevelProps.CollectionColour;
            }
        }
    }
}
