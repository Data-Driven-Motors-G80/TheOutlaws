using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class WorldShiftLevelTests
{
    [TestCase(0, 0, 844, 390)]
    [TestCase(44, 21, 756, 348)]
    [TestCase(0, 0, 390, 844)]
    [TestCase(0, 24, 768, 1000)]
    public void MobileScreenFitKeepsWholeLandscapeInsideSafeArea(float x, float y, float width, float height)
    {
        var safe = new Rect(x, y, width, height);
        var fitted = (Rect)Static(TypeFor("WorldShiftMobileScreen"), "FitArea", safe);
        Assert.That(fitted.width / fitted.height, Is.EqualTo(1280f / 720f).Within(.0001f));
        Assert.That(fitted.xMin, Is.GreaterThanOrEqualTo(safe.xMin - .001f));
        Assert.That(fitted.yMin, Is.GreaterThanOrEqualTo(safe.yMin - .001f));
        Assert.That(fitted.xMax, Is.LessThanOrEqualTo(safe.xMax + .001f));
        Assert.That(fitted.yMax, Is.LessThanOrEqualTo(safe.yMax + .001f));
        Assert.That(fitted.center.x, Is.EqualTo(safe.center.x).Within(.001f));
        Assert.That(fitted.center.y, Is.EqualTo(safe.center.y).Within(.001f));
    }

    [UnityTest]
    public IEnumerator MobileControlsAndTrainingTapPreserveSignArtworkAndFitPanels()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        var inputSystem = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.InputSystem.InputSystem")).First(t => t != null);
        var device = inputSystem.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
            .Invoke(null, new object[] { "Touchscreen", "Mobile test", null });
        try
        {
            StartLevel(0); yield return WaitForNavigation();
            var game = Started();
            var hud = Find("WorldShiftLevelHud");
            Call(hud, "Refresh"); Canvas.ForceUpdateCanvases();
            yield return null;
            Call(hud, "Refresh"); Canvas.ForceUpdateCanvases();
            var safe = (RectTransform)Field(hud, "hudSafeArea");
            var uiCanvas = safe.GetComponentInParent<Canvas>();
            Assert.That(safe.rect.width, Is.EqualTo(1280).Within(2), $"FitV2 screen={Screen.width}x{Screen.height}, canvas={uiCanvas.pixelRect}, scale={uiCanvas.scaleFactor}, area={StaticGet(TypeFor("WorldShiftMobileScreen"), "ContentArea")}, anchors={safe.anchorMin}/{safe.anchorMax}.");
            Assert.That(safe.rect.height, Is.EqualTo(720).Within(2));
            game.GetType().GetProperty("TrainingLessonPaused", Flags).SetValue(game, true);
            game.GetType().GetField("trainingSignShownAt", Flags).SetValue(game, Time.realtimeSinceStartup);
            Call(game, "ContinueTrainingLessonFromTouch");
            Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "Keep the existing input delay.");
            game.GetType().GetField("trainingSignShownAt", Flags).SetValue(game, Time.realtimeSinceStartup - 1f);
            hud.GetType().GetField("lessonFade", Flags).SetValue(hud, 1f);
            Call(hud, "Refresh"); Canvas.ForceUpdateCanvases();
            Assert.That(((RawImage)Field(hud, "signImage")).gameObject.activeSelf, Is.True);
            Assert.That(((Component)Field(hud, "signFallback")).gameObject.activeSelf, Is.False);
            Assert.That(((GameObject)Field(hud, "trainingLesson")).GetComponentsInChildren<TMP_Text>().Any(t => t.text == "TAP"), Is.True);
            var world = Get(game, "World");
            var touchStateType = device.GetType().Assembly.GetType("UnityEngine.InputSystem.LowLevel.TouchState");
            var touchState = Activator.CreateInstance(touchStateType);
            touchStateType.GetField("touchId").SetValue(touchState, 11);
            touchStateType.GetField("position").SetValue(touchState, new Vector2(2, 2));
            var phase = touchStateType.GetProperty("phase");
            phase.SetValue(touchState, Enum.Parse(phase.PropertyType, "Began"));
            var queueTouch = inputSystem.GetMethods().Single(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition).MakeGenericMethod(touchStateType);
            queueTouch.Invoke(null, new[] { device, touchState, (object)(-1d) });
            inputSystem.GetMethod("Update", Type.EmptyTypes).Invoke(null, null);
            Call(game, "Update");
            Assert.That(Get(game, "TrainingLessonPaused"), Is.False);
            Assert.That(Get(game, "World"), Is.EqualTo(world));
            ((GameObject)Field(hud, "touchControls")).transform.Find("Switch Day Night").GetComponent<Button>().onClick.Invoke();
            Assert.That(Call(hud, "ConsumeTouchSwitch"), Is.False);
            Call(hud, "Refresh");
            phase.SetValue(touchState, Enum.Parse(phase.PropertyType, "Ended"));
            queueTouch.Invoke(null, new[] { device, touchState, (object)(-1d) });
            inputSystem.GetMethod("Update", Type.EmptyTypes).Invoke(null, null);
            yield return null;
            var left = (Component)Field(hud, "touchLeft");
            var tap = new PointerEventData(EventSystem.current) { pointerId = 22, position = ((RectTransform)left.transform).position };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(tap, hits);
            Assert.That(hits.Any(h => h.gameObject == left.gameObject), Is.True, $"Steering must receive real UI hits at {tap.position}; screen={Screen.width}x{Screen.height}.");
            Call(left, "OnPointerDown", tap);
            Assert.That(Get(hud, "TouchSteering"), Is.EqualTo(-1f));
            var controls = (GameObject)Field(hud, "touchControls");
            controls.transform.Find("Switch Day Night").GetComponent<Button>().onClick.Invoke();
            Assert.That(Call(hud, "ConsumeTouchSwitch"), Is.True);
            Assert.That(Call(hud, "ConsumeTouchSwitch"), Is.False);
            Call(game, "TogglePause"); Call(hud, "Refresh");
            Call(game, "TogglePause"); Call(hud, "Refresh");
            Assert.That(Get(hud, "TouchSteering"), Is.EqualTo(0f));
            Call(game, "TogglePause"); Call(hud, "Refresh");
            var pauseCard = (RectTransform)Field(hud, "pauseCard");
            Assert.That(pauseCard.rect.width, Is.LessThanOrEqualTo(safe.rect.width));
            Assert.That(pauseCard.rect.height, Is.LessThanOrEqualTo(safe.rect.height));
            WorldShiftTestCapture.Capture("mobile-fit-pause");
        }
        finally
        {
            inputSystem.GetMethods().Single(m => m.Name == "RemoveDevice" && m.GetParameters().Length == 1)
                .Invoke(null, new[] { device });
        }
    }
}
