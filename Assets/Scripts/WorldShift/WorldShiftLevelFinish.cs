using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private GameObject finishResult;
        private Button finishContinue, finishSkip;
        private TMP_Text escapeCaption;
        private void BuildEscapeFinish(Transform safe)
        {
            complete=WorldShiftMenu.Panel(safe,"Level complete",Vector2.zero,Vector2.one,Color.clear).gameObject;
            WorldShiftMenu.Panel(complete.transform,"Cinematic top bar",new Vector2(0,.90f),Vector2.one,new Color(.004f,.012f,.02f,.94f));
            WorldShiftMenu.Panel(complete.transform,"Cinematic bottom bar",Vector2.zero,new Vector2(1,.09f),new Color(.004f,.012f,.02f,.94f));
            PauseLabel(complete.transform,game.LevelIndex.ToString("00")+" / "+WorldShiftLevels.Name(game.LevelIndex).ToUpperInvariant(),.04f,.92f,.70f,.98f,15,PauseAccent,FontStyles.Bold).characterSpacing=2;
            finishSkip=PauseTextButton(complete.transform,"Skip / Esc",new Vector2(.84f,.92f),new Vector2(.96f,.975f),()=>{game.Escape.Skip();Refresh();});
            escapeCaption=PauseLabel(complete.transform,"",.05f,.10f,.95f,.17f,23,Cream);
            escapeCaption.alignment=TextAlignmentOptions.Center;
            var result=WorldShiftMenu.Panel(complete.transform,"Escape result",new Vector2(.43f,.12f),new Vector2(.96f,.46f),PauseFace);
            Round(result,PauseFace);finishResult=result.gameObject;
            PauseLabel(result,game.Course.Training?"PRACTICE COMPLETE":"PURSUIT ENDED",.045f,.77f,.95f,.95f,12,PauseAccent).characterSpacing=2;
            PauseLabel(result,game.Course.Training?"Ready for the getaway":"You lost them",.045f,.44f,.96f,.79f,42,Cream,FontStyles.Bold);
            string next=WorldShiftLevels.HasNext?"Next escape: "+WorldShiftLevels.Name(game.LevelIndex+1):"Every route escaped.";
            PauseLabel(result,next,.045f,.29f,.96f,.46f,18,PauseMuted);
            finishContinue=PauseAction(result,WorldShiftLevels.HasNext?"Continue":"Back to routes",new Vector2(.045f,.055f),new Vector2(.49f,.25f),ShiftIcon.Play,ContinueAfterEscape,true);
            PauseTextButton(result,"Level select",new Vector2(.52f,.055f),new Vector2(.75f,.25f),WorldShiftLevels.Menu);
            PauseTextButton(result,"Replay",new Vector2(.78f,.055f),new Vector2(.96f,.25f),WorldShiftLevels.Retry);
        }
        private void ContinueAfterEscape()
        {
            if(game.Escape.Active&&!game.Escape.Ready) { game.Escape.Skip();return; }
            if(WorldShiftLevels.HasNext) WorldShiftLevels.Next();else WorldShiftLevels.Menu();
        }
        private void RefreshEscapeFinish()
        {
            bool ready=game.Escape.Ready;
            finishResult.SetActive(ready);
            finishSkip.gameObject.SetActive(game.Escape.Active&&!ready);
            finishSkip.interactable=game.Escape.CanSkip;
            escapeCaption.gameObject.SetActive(!ready);
            bool departed=game.Escape.Progress>.45f;
            escapeCaption.text=game.Environment==ShiftLevel.Ocean
                ?departed?"Too late, officers.":"Catch the ferry."
                :departed?"A closed door. A clean escape.":"One last turn. Keep going.";
        }
    }
}
