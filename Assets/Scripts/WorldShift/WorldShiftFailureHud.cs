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

        private void BuildFailurePanel(Transform parent)
        {
            var panel=WorldShiftMenu.Panel(parent,"Checkpoint retry notice",new Vector2(.24f,.64f),new Vector2(.76f,.84f),Ink);
            Round(panel, new Color(.025f,.045f,.07f,.96f));
            failurePanel=panel.gameObject;
            var group=panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=false; group.interactable=false;
            WorldShiftMenu.Panel(panel,"Failure accent",new Vector2(.025f,.16f),new Vector2(.035f,.84f),new Color(1,.35f,.25f));
            failureTitle=WorldShiftMenu.Label(panel,"BUSTED",.08f,.43f,.94f,.91f,38);
            failureTitle.color=new Color(1,.52f,.39f); failureTitle.fontStyle=FontStyles.Bold;
            failureTitle.alignment=TextAlignmentOptions.Center;
            failureTitle.enableAutoSizing=true; failureTitle.fontSizeMin=20; failureTitle.fontSizeMax=38;
            failureDetail=WorldShiftMenu.Label(panel,"Returning to checkpoint",.08f,.16f,.94f,.43f,20);
            failureDetail.color=Cream; failureDetail.alignment=TextAlignmentOptions.Center;
            failureDetail.enableAutoSizing=true; failureDetail.fontSizeMin=12; failureDetail.fontSizeMax=20;
            var track=WorldShiftMenu.Panel(panel,"Retry track",new Vector2(.08f,.08f),new Vector2(.94f,.105f),new Color(.2f,.27f,.32f));
            retryFill=WorldShiftMenu.Panel(track,"Retry progress",Vector2.zero,Vector2.one,Gold).GetComponent<Image>();
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
            failureTitle.color=game.MissedRequiredItem?WorldShiftLevelProps.CollectionColour:new Color(1,.52f,.39f);
            retryFill.rectTransform.anchorMax=new Vector2(game.CheckpointRetryProgress,1);
        }
    }
}
