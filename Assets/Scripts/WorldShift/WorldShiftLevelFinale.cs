using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private static readonly string[] FinaleTitles={"The last\nshift.","The last\nspark.","A new\ndawn."};
        private static readonly string[] FinaleLines={"The passage seals the road behind you.","The device burns out. It got you this far.","No sirens. No one in the mirror."};
        private RectTransform finaleCard;
        private GameObject finaleStory, finaleEnd;
        private RawImage finaleArt;
        private TMP_Text finaleTitle, finaleLine;
        private Button finaleNext, finaleBack, finaleRoutes;
        private readonly Image[] finaleDots=new Image[3];
        private int finalePage, finaleActionFrame=-1;
        private bool finaleReady;
        private float finaleRevealTime;
        private CanvasGroup finaleEndFade;

        private void BuildFinale(Transform parent)
        {
            finaleStory=WorldShiftMenu.Panel(parent,"Final chapter",Vector2.zero,Vector2.one,PauseFace).gameObject;
            finaleCard=WorldShiftMenu.Panel(finaleStory.transform,"Epilogue comic",Vector2.one*.5f,Vector2.one*.5f,PauseFace);
            finaleCard.sizeDelta=new Vector2(1160,660);Round(finaleCard,PauseFace);CardChrome(finaleCard,false);
            var panel=WorldShiftMenu.Panel(finaleCard,"Final comic panel",new Vector2(.014f,.024f),new Vector2(.58f,.976f),Color.white);
            Round(panel,Color.white);panel.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            finaleArt=WorldShiftMenu.TextureLayer(panel,"Finale comic artwork");
            finaleArt.texture=Resources.Load<Texture2D>("WorldShiftStory/FinaleComic");
            var fit=finaleArt.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio=finaleArt.texture!=null?finaleArt.texture.width/(3f*finaleArt.texture.height):1f;
            for(int i=0;i<3;i++)
            {
                var dot=WorldShiftMenu.Panel(finaleCard,"Final chapter page "+i,new Vector2(.63f+i*.025f,.88f),new Vector2(.63f+i*.025f,.88f),PauseMuted);
                dot.sizeDelta=Vector2.one*7;finaleDots[i]=dot.GetComponent<Image>();finaleDots[i].sprite=WorldShiftIcons.Get(ShiftIcon.Circle);
            }
            PauseTextButton(finaleCard,"Skip",new Vector2(.85f,.845f),new Vector2(.96f,.92f),CompleteFinale);
            finaleTitle=PauseLabel(finaleCard,"",.63f,.53f,.96f,.81f,54,Cream,FontStyles.Bold);
            finaleLine=PauseLabel(finaleCard,"",.63f,.34f,.95f,.52f,22,WorldShiftUiStyle.Muted);
            finaleNext=PauseAction(finaleCard,"Continue",new Vector2(.63f,.19f),new Vector2(.95f,.29f),ShiftIcon.Play,AdvanceFinale,true);
            finaleBack=PauseTextButton(finaleCard,"Back",new Vector2(.63f,.08f),new Vector2(.77f,.15f),PreviousFinale);

            finaleEnd=WorldShiftMenu.Panel(parent,"The End",Vector2.zero,Vector2.one,PauseFace).gameObject;
            var hero=WorldShiftMenu.TextureLayer(finaleEnd.transform,"Final dawn illustration");
            hero.texture=Resources.Load<Texture2D>("WorldShiftStory/FinaleDawn");
            var heroFit=hero.gameObject.AddComponent<AspectRatioFitter>();heroFit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            heroFit.aspectRatio=hero.texture!=null?(float)hero.texture.width/hero.texture.height:16f/9f;
            WorldShiftMenu.TextureLayer(finaleEnd.transform,"Final dawn contrast").texture=WorldShiftMenu.Shade();
            PauseLabel(finaleEnd.transform,"W E E   W O O   R U N",.065f,.79f,.57f,.86f,19,PauseAccent,FontStyles.Bold);
            PauseLabel(finaleEnd.transform,"THE END",.06f,.46f,.64f,.76f,112,Cream,FontStyles.Bold);
            PauseLabel(finaleEnd.transform,"You lost them. For good.",.065f,.38f,.57f,.46f,26,Cream);
            finaleRoutes=PauseAction(finaleEnd.transform,"Level select",new Vector2(.065f,.16f),new Vector2(.34f,.26f),ShiftIcon.Home,ContinueAfterEscape,true);
            PauseAction(finaleEnd.transform,"Replay " + WorldShiftLevels.Name(game.LevelIndex),new Vector2(.065f,.055f),new Vector2(.34f,.135f),ShiftIcon.Retry,WorldShiftLevels.Retry);
            finaleEndFade=finaleEnd.AddComponent<CanvasGroup>();
            finaleEnd.SetActive(false);ShowFinalePage();
        }

        private void ShowFinalePage()
        {
            finaleTitle.text=FinaleTitles[finalePage];finaleLine.text=FinaleLines[finalePage];
            finaleArt.uvRect=new Rect(finalePage/3f,0,1f/3f,1);
            finaleNext.GetComponentInChildren<TMP_Text>().text=finalePage==2?"Finish":"Continue";
            finaleBack.gameObject.SetActive(finalePage>0);
            for(int i=0;i<3;i++) finaleDots[i].color=i==finalePage?PauseAccent:PauseMuted*.55f;
            finishContinue=finaleNext;
        }

        private void AdvanceFinale()
        {
            if(finaleActionFrame==Time.frameCount) return;
            finaleActionFrame=Time.frameCount;
            if(!game.Escape.Ready) {game.Escape.Skip();return;}
            if(!finaleReady) return;
            if(finalePage>=3) {WorldShiftLevels.Menu();return;}
            if(finalePage==2) RevealFinaleEnd();
            else {finalePage++;ShowFinalePage();}
        }

        private void PreviousFinale()
        {
            if(!finaleReady || finalePage==0 || finalePage>=3 || finaleActionFrame==Time.frameCount) return;
            finaleActionFrame=Time.frameCount;finalePage--;ShowFinalePage();
        }

        private void CompleteFinale()
        {
            if(!finaleReady || finalePage>=3 || finaleActionFrame==Time.frameCount) return;
            finaleActionFrame=Time.frameCount;RevealFinaleEnd();
        }

        private void RevealFinaleEnd()
        {
            finalePage=3;finaleStory.SetActive(false);finaleEnd.SetActive(true);
            finaleRevealTime=Time.unscaledTime;finaleEndFade.alpha=0;
            finishContinue=finaleRoutes;game.Escape.RevealFinale();
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(finaleRoutes.gameObject);
        }

        private void RefreshFinale()
        {
            if(!finaleReady) {finaleReady=true;finaleActionFrame=Time.frameCount;}
            var area=(RectTransform)complete.transform;
            float scale=Mathf.Min(1f,(area.rect.width-32f)/1160f,(area.rect.height-32f)/660f);
            finaleCard.localScale=Vector3.one*Mathf.Max(.1f,scale);
            if(finalePage>=3) finaleEndFade.alpha=Mathf.SmoothStep(0,1,(Time.unscaledTime-finaleRevealTime)/.7f);
        }
    }
}
