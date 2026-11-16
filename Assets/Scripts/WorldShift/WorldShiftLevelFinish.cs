using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private GameObject finishResult, finishCinematic, finishBackdrop;
        private RectTransform finishCard;
        private Button finishContinue, finishSkip;
        private TMP_Text escapeCaption;
        private void BuildEscapeFinish(Transform safe)
        {
            complete=WorldShiftMenu.Panel(safe,"Level complete",Vector2.zero,Vector2.one,Color.clear).gameObject;
            finishCinematic=WorldShiftMenu.Panel(complete.transform,"Cinematic overlay",Vector2.zero,Vector2.one,Color.clear).gameObject;
            WorldShiftMenu.Panel(finishCinematic.transform,"Cinematic top bar",new Vector2(0,.90f),Vector2.one,new Color(.004f,.012f,.02f,.94f));
            WorldShiftMenu.Panel(finishCinematic.transform,"Cinematic bottom bar",Vector2.zero,new Vector2(1,.09f),new Color(.004f,.012f,.02f,.94f));
            PauseLabel(finishCinematic.transform,game.LevelIndex.ToString("00")+" / "+WorldShiftLevels.Name(game.LevelIndex).ToUpperInvariant(),.04f,.92f,.70f,.98f,15,PauseAccent,FontStyles.Bold).characterSpacing=2;
            finishSkip=PauseTextButton(finishCinematic.transform,"Skip / Esc",new Vector2(.84f,.92f),new Vector2(.96f,.975f),()=>{game.Escape.Skip();Refresh();});
            escapeCaption=PauseLabel(finishCinematic.transform,"",.05f,.10f,.95f,.17f,23,Cream);
            escapeCaption.alignment=TextAlignmentOptions.Center;
            finishBackdrop=WorldShiftMenu.Panel(complete.transform,"Result backdrop",Vector2.zero,Vector2.one,new Color(.008f,.018f,.03f,.78f)).gameObject;
            finishBackdrop.GetComponent<Image>().raycastTarget=true;
            if (game.Escape.IsFinale) { BuildFinale(finishBackdrop.transform); return; }
            finishCard=WorldShiftMenu.Panel(finishBackdrop.transform,"Escape result",new Vector2(.5f,.5f),new Vector2(.5f,.5f),PauseFace);
            finishCard.sizeDelta=new Vector2(1020,540);Round(finishCard,PauseFace);finishResult=finishCard.gameObject;
            CardChrome(finishCard);
            bool hasNext=WorldShiftLevels.HasNext;
            int destination=hasNext?game.LevelIndex+1:game.LevelIndex;
            var hero=WorldShiftMenu.Panel(finishCard,"Destination artwork",new Vector2(.014f,.026f),new Vector2(.44f,.974f),Color.white);
            Round(hero,Color.white);hero.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            var art=WorldShiftMenu.TextureLayer(hero,"Destination illustration");
            art.texture=Resources.Load<Texture2D>("WorldShiftLevelCards/"+WorldShiftLevels.Name(destination));
            var fit=art.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio=art.texture!=null?(float)art.texture.width/art.texture.height:1.5f;
            WorldShiftMenu.TextureLayer(hero,"Destination contrast").texture=WorldShiftMenu.Shade();
            PauseLabel(hero,WorldShiftLevels.Name(destination),.08f,.12f,.94f,.29f,48,Cream,FontStyles.Bold);
            PauseLabel(finishCard,game.Course.Training?"Ready for\nthe getaway":"You lost them",.49f,.52f,.95f,.81f,44,Cream,FontStyles.Bold);
            if(!hasNext) PauseLabel(finishCard,"Every route escaped.",.49f,.435f,.95f,.515f,19,PauseMuted);
            finishContinue=PauseAction(finishCard,hasNext?"Continue to "+WorldShiftLevels.Name(destination):"Back to routes",new Vector2(.49f,.265f),new Vector2(.95f,.385f),ShiftIcon.Play,ContinueAfterEscape,true);
            PauseLabel(finishContinue.transform,"ENTER",.79f,.12f,.96f,.88f,11,PauseFace).alignment=TextAlignmentOptions.Right;
            if(hasNext) PauseAction(finishCard,"Level select",new Vector2(.49f,.13f),new Vector2(.715f,.23f),ShiftIcon.Home,WorldShiftLevels.Menu);
            PauseAction(finishCard,"Replay",new Vector2(.735f,.13f),new Vector2(.95f,.23f),ShiftIcon.Retry,WorldShiftLevels.Retry);
        }
        private void ContinueAfterEscape()
        {
            if (game.Escape.IsFinale) { AdvanceFinale(); return; }
            if(game.Escape.Active&&!game.Escape.Ready) { game.Escape.Skip();return; }
            if(WorldShiftLevels.HasNext) WorldShiftLevels.Next();else WorldShiftLevels.Menu();
        }
        private void RefreshEscapeFinish()
        {
            bool ready=game.Escape.Ready;
            if (game.Escape.IsFinale)
            {
                finishBackdrop.SetActive(ready);finishCinematic.SetActive(!ready);
                finishSkip.interactable=game.Escape.CanSkip;
                escapeCaption.text=game.Escape.Progress<.33f?"One last shift.":"Leave the sirens behind.";
                if (ready) RefreshFinale();
                return;
            }
            finishBackdrop.SetActive(ready);
            finishResult.SetActive(ready);
            finishCinematic.SetActive(!ready);
            if(ready)
            {
                var area=(RectTransform)complete.transform;
                float scale=Mathf.Min(1f,(area.rect.width-32f)/1020f,(area.rect.height-32f)/540f);
                finishCard.localScale=Vector3.one*Mathf.Max(.1f,scale);
            }
            finishSkip.interactable=game.Escape.CanSkip;
            bool departed=game.Escape.Progress>.45f;
            escapeCaption.text=game.Environment==ShiftLevel.Ocean
                ?departed?"Too late, officers.":"Catch the ferry."
                :departed?"A closed door. A clean escape.":"One last turn. Keep going.";
        }
    }
}
