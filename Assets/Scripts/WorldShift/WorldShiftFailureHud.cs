using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private GameObject failurePanel;
        private TMP_Text failureTitle, failureDetail;
        private Image retryFill;

        private static readonly Color FailureAccent = new Color(1f,.52f,.39f);
        private TMP_Text failureKicker;

        // Matches the pause and finish cards: dark face, soft shadow, fine rim, spaced kicker over a bold cream title.
        private void BuildFailurePanel(Transform parent)
        {
            var panel=WorldShiftMenu.Panel(parent,"Checkpoint retry notice",new Vector2(.31f,.62f),new Vector2(.69f,.84f),Color.clear);
            failurePanel=panel.gameObject;
            var group=panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=false; group.interactable=false;
            CardChrome(panel);
            failureKicker=PauseLabel(panel,"CAUGHT",.06f,.73f,.94f,.88f,12,FailureAccent,FontStyles.Bold);
            failureKicker.characterSpacing=2.5f; failureKicker.alignment=TextAlignmentOptions.Center;
            failureTitle=PauseLabel(panel,"BUSTED",.06f,.40f,.94f,.74f,44,Cream,FontStyles.Bold);
            failureTitle.alignment=TextAlignmentOptions.Center;
            failureTitle.enableAutoSizing=true; failureTitle.fontSizeMin=22; failureTitle.fontSizeMax=44;
            failureDetail=PauseLabel(panel,"Returning to checkpoint",.06f,.21f,.94f,.38f,17,PauseMuted);
            failureDetail.alignment=TextAlignmentOptions.Center;
            var track=WorldShiftMenu.Panel(panel,"Retry track",new Vector2(.12f,.11f),new Vector2(.88f,.135f),new Color(.18f,.26f,.30f,.7f));
            Round(track,new Color(.18f,.26f,.30f,.7f));
            retryFill=WorldShiftMenu.Panel(track,"Retry progress",Vector2.zero,Vector2.one,FailureAccent).GetComponent<Image>();
            failurePanel.SetActive(false);
        }

        private void RefreshFailurePanel()
        {
            bool failed=game.State==ShiftRunState.Retrying;
            failurePanel.SetActive(failed);
            if(!failed) return;
            failureTitle.text=game.MissedRequiredItem?"MISSED DIAMOND":game.Capturing?"BUSTED"
                :game.FailureReason=="MISSED RELAY LANDING"?"MISSED LANDING":"OFF THE ROAD";
            failureDetail.text=game.CheckpointIndex==0?"Returning to the start":"Returning to checkpoint";
            Color accent=game.MissedRequiredItem?WorldShiftLevelProps.CollectionColour:FailureAccent;
            failureKicker.text=game.MissedRequiredItem?"SECTION RETRY":game.Capturing?"CAUGHT":"CRASHED";
            failureKicker.color=accent; retryFill.color=accent;
            retryFill.rectTransform.anchorMax=new Vector2(game.CheckpointRetryProgress,1);
        }
    }
}
