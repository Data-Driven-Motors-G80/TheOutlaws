using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private GameObject touchControls;
        private WorldShiftTouchButton touchLeft, touchRight;
        private Image touchSwitchIcon;
        private int touchSwitchFrame = -10;
        private bool waitingForTrainingTapRelease;
        private bool TouchDriving => game != null && game.State == ShiftRunState.Running
            && !game.StoryActive && !game.TrainingLessonPaused && !WorldShiftSceneLoading.Active && !waitingForTrainingTapRelease;
        public float TouchSteering => TouchDriving && touchControls != null && touchControls.activeInHierarchy
            ? (touchRight.Held ? 1f : 0f) - (touchLeft.Held ? 1f : 0f) : 0f;

        public bool ConsumeTouchSwitch()
        {
            bool pressed = TouchDriving && touchSwitchFrame >= 0 && Time.frameCount - touchSwitchFrame <= 1;
            touchSwitchFrame = -10;
            return pressed;
        }
        private void BuildTouchControls(Transform parent)
        {
            touchControls = WorldShiftMenu.Panel(parent, "Touch controls", Vector2.zero, Vector2.one, Color.clear).gameObject;
            var left = IconButton(touchControls.transform, ShiftIcon.Next, new Vector2(.065f, .15f), 104f, () => { });
            left.name = "Hold left";
            left.GetComponent<Button>().enabled = false;
            left.GetChild(left.childCount - 1).localRotation = Quaternion.Euler(0, 0, 180);
            touchLeft = left.gameObject.AddComponent<WorldShiftTouchButton>();
            var right = IconButton(touchControls.transform, ShiftIcon.Next, new Vector2(.17f, .15f), 104f, () => { });
            right.name = "Hold right";
            right.GetComponent<Button>().enabled = false;
            touchRight = right.gameObject.AddComponent<WorldShiftTouchButton>();
            var change = IconButton(touchControls.transform, ShiftIcon.Moon, new Vector2(.91f, .15f), 112f,
                () => { if (TouchDriving) touchSwitchFrame = Time.frameCount; });
            change.name = "Switch Day Night";
            touchSwitchIcon = change.GetChild(change.childCount - 1).GetComponent<Image>();
            touchControls.SetActive(false);
        }
        private void RefreshTouchControls()
        {
            if (waitingForTrainingTapRelease)
            {
                bool held = false;
                if (Touchscreen.current != null)
                    foreach (var touch in Touchscreen.current.touches) held |= touch.press.isPressed;
                waitingForTrainingTapRelease = held;
            }
            touchControls.SetActive(WorldShiftMobileScreen.Active && TouchDriving);
            if (!touchControls.activeInHierarchy) touchSwitchFrame = -10;
            touchSwitchIcon.sprite = WorldShiftIcons.Get(game.World == ShiftWorld.Day ? ShiftIcon.Moon : ShiftIcon.Sun);
        }
        public void WaitForTrainingTapRelease() => waitingForTrainingTapRelease = true;
        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            touchLeft?.Clear(); touchRight?.Clear(); touchSwitchFrame = -10;
        }
    }
}
