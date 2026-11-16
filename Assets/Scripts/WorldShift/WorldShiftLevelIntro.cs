using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private static readonly string[] StoryTitles = { "One last\njob.", "One loud\nalarm.", "One way\nout." };
        private static readonly string[] StoryLines =
        {
            "You stole a device that turns Day into Night.",
            "The police were waiting outside.",
            "Get in. Shift worlds. Lose the police."
        };
        private GameObject storyRoot;
        private RectTransform storyCard;
        private RawImage storyArt;
        private TMP_Text storyTitle, storyLine;
        private Button storyNext, storyBack;
        private readonly Image[] storyDots = new Image[3];
        private int storyPage, storyActionFrame = -1;
        private float storyKeyboardReady;

        private void BuildCityStory(Transform safe)
        {
            if (!game.StoryActive) return;
            storyRoot = WorldShiftMenu.Panel(safe,"City opening story",Vector2.zero,Vector2.one,new Color(.008f,.018f,.03f,.98f)).gameObject;
            storyRoot.GetComponent<Image>().raycastTarget = true;
            storyCard = WorldShiftMenu.Panel(storyRoot.transform,"Story card",new Vector2(.5f,.5f),new Vector2(.5f,.5f),PauseFace);
            storyCard.sizeDelta = new Vector2(1060,620); Round(storyCard,PauseFace);
            CardChrome(storyCard, shadow: false);
            var illustration = WorldShiftMenu.Panel(storyCard,"Comic panel",new Vector2(.014f,.024f),new Vector2(.57f,.976f),Color.white);
            Round(illustration,Color.white); illustration.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            storyArt = WorldShiftMenu.TextureLayer(illustration,"City comic artwork");
            storyArt.texture = Resources.Load<Texture2D>("WorldShiftStory/CityIntro");
            var fit = storyArt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = storyArt.texture != null ? storyArt.texture.width/(3f*storyArt.texture.height) : 1f;
            for (int i=0;i<storyDots.Length;i++)
            {
                var dot = WorldShiftMenu.Panel(storyCard,"Story page "+(i+1),new Vector2(.62f+i*.025f,.885f),new Vector2(.62f+i*.025f,.885f),PauseMuted);
                dot.sizeDelta = Vector2.one*7;
                storyDots[i] = dot.GetComponent<Image>(); storyDots[i].sprite = WorldShiftIcons.Get(ShiftIcon.Circle);
            }
            PauseTextButton(storyCard,"Skip",new Vector2(.84f,.85f),new Vector2(.95f,.92f),EndCityStory);
            storyTitle = PauseLabel(storyCard,"",.62f,.53f,.95f,.82f,48,Cream,FontStyles.Bold);
            storyLine = PauseLabel(storyCard,"",.62f,.34f,.95f,.52f,22,new Color(.75f,.83f,.83f));
            storyNext = PauseAction(storyCard,"Continue",new Vector2(.62f,.19f),new Vector2(.95f,.29f),ShiftIcon.Play,AdvanceCityStory,true);
            PauseLabel(storyNext.transform,"ENTER",.79f,.12f,.96f,.88f,10,PauseFace).alignment = TextAlignmentOptions.Right;
            storyBack = PauseTextButton(storyCard,"Back",new Vector2(.62f,.08f),new Vector2(.76f,.15f),PreviousCityStory);
            ShowCityStoryPage();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(storyNext.gameObject);
        }

        private void ShowCityStoryPage()
        {
            storyTitle.text = StoryTitles[storyPage]; storyLine.text = StoryLines[storyPage];
            storyArt.uvRect = new Rect(storyPage/3f,0,1f/3f,1);
            storyNext.GetComponentInChildren<TMP_Text>().text = storyPage==2 ? "Start getaway" : "Continue";
            storyBack.gameObject.SetActive(storyPage>0);
            for (int i=0;i<storyDots.Length;i++) storyDots[i].color = i==storyPage ? PauseAccent : PauseMuted*.55f;
            // Enter can also submit the selected button; the frame guard prevents advancing twice.
            storyKeyboardReady = Time.unscaledTime+.2f;
        }

        private void AdvanceCityStory()
        {
            if (!game.StoryActive || storyActionFrame==Time.frameCount) return;
            storyActionFrame = Time.frameCount;
            if (storyPage==2) EndCityStory();
            else { storyPage++; ShowCityStoryPage(); }
        }

        private void PreviousCityStory()
        {
            if (!game.StoryActive || storyPage==0 || storyActionFrame==Time.frameCount) return;
            storyActionFrame = Time.frameCount; storyPage--; ShowCityStoryPage();
        }

        private void EndCityStory()
        {
            if (!game.StoryActive) return;
            game.FinishStory(); storyRoot.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Refresh();
        }

        private void UpdateCityStory()
        {
            if (Keyboard.current==null || Time.unscaledTime<storyKeyboardReady) return;
            var keys = Keyboard.current;
            if (keys.escapeKey.wasPressedThisFrame) EndCityStory();
            else if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) AdvanceCityStory();
            else if (keys.leftArrowKey.wasPressedThisFrame) PreviousCityStory();
        }

        private void RefreshCityStory()
        {
            if (storyRoot==null) return;
            storyRoot.SetActive(game.StoryActive);
            if (!game.StoryActive) return;
            var area = (RectTransform)storyRoot.transform;
            float scale = Mathf.Min(1f,(area.rect.width-32f)/1060f,(area.rect.height-32f)/620f);
            storyCard.localScale = Vector3.one*Mathf.Max(.1f,scale);
        }
    }
}
