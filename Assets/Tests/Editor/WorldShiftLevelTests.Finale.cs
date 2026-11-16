using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator FinalMountainEscapeClosesStoryAndRevealsTheEnd()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(5);yield return WaitForNavigation();
        var game=Started();FinishForButtons(game);
        var escape=Get(game,"Escape");var hud=Find("WorldShiftLevelHud");var audio=Find("WorldShiftLevelAudio");
        Assert.That(Static(Levels,"IsBeaten",5),Is.True,"The ending never delays the saved win.");
        Assert.That(Get(escape,"IsFinale"),Is.True);
        Assert.That((float)Get(escape,"SequenceDuration"),Is.GreaterThan(6.5f));
        Call(escape,"Advance",5.5f);Call(hud,"Refresh");Capture("finale-mountain-exit");
        Call(escape,"Advance",3.5f);Call(hud,"Refresh");
        Assert.That(Get(escape,"Ready"),Is.False);
        Assert.That(((Transform)Field(escape,"coupe")).gameObject.activeSelf,Is.True);
        Capture("finale-mountain-dawn");
        Call(escape,"Advance",20f);Call(hud,"Refresh");Call(audio,"Refresh",0f);yield return null;
        Assert.That(Get(escape,"Ready"),Is.True);
        Assert.That((bool)Field(audio,"winCuePlayed"),Is.False,"Save the finale chord for The End.");
        Assert.That(((AudioClip)Field(audio,"victory")).name,Is.EqualTo("WS_Finale"));
        Assert.That(((GameObject)Field(hud,"finaleStory")).activeInHierarchy,Is.True);
        Capture("finale-comic-last-shift");
        var next=(Button)Field(hud,"finaleNext");
        ClickButton(next);
        // Enter can also submit the selected button in the same frame.
        Call(hud,"AdvanceFinale");
        Assert.That((int)Field(hud,"finalePage"),Is.EqualTo(1));yield return null;
        Capture("finale-comic-last-spark");
        ClickButton((Button)Field(hud,"finaleBack"));yield return null;
        Assert.That((int)Field(hud,"finalePage"),Is.Zero);
        ClickButton(next);yield return null;ClickButton(next);yield return null;
        Assert.That((int)Field(hud,"finalePage"),Is.EqualTo(2));
        Capture("finale-comic-new-dawn");
        ClickButton(next);Call(audio,"Refresh",0f);yield return null;
        Assert.That(Get(escape,"FinaleRevealed"),Is.True);
        Assert.That((bool)Field(audio,"winCuePlayed"),Is.True);
        Assert.That(((GameObject)Field(hud,"finaleEnd")).activeInHierarchy,Is.True);
        Assert.That(((GameObject)Field(hud,"finaleStory")).activeSelf,Is.False);
        yield return new WaitForSecondsRealtime(.8f);
        Capture("finale-the-end-1080");WorldShiftTestCapture.Capture("finale-the-end-4x3",1024,768);
        Assert.That(StaticGet(Levels,"HasNext"),Is.False);
        ClickButton((Button)Field(hud,"finishContinue"));yield return WaitForNavigation();
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("WorldShift_Menu"));
        Assert.That(GameObject.Find("Getaway celebration stage"),Is.Null);
        Assert.That(RenderSettings.skybox==null || RenderSettings.skybox.name!="Final dawn sky",Is.True);
    }

    [UnityTest]
    public IEnumerator FinaleCanSkipToTheEndAndReplayTheFinalLevel()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(5);yield return WaitForNavigation();
        var game=Started();FinishForButtons(game);var escape=Get(game,"Escape");var hud=Find("WorldShiftLevelHud");
        Call(escape,"Advance",.8f);Call(hud,"Refresh");yield return null;
        ClickButton((Button)Field(hud,"finishSkip"));Call(hud,"Refresh");yield return null;
        Assert.That((int)Field(hud,"finalePage"),Is.Zero,"Skipping the drive still offers the story conclusion.");
        ClickButton(VisibleButton("Skip"));yield return null;
        Assert.That((int)Field(hud,"finalePage"),Is.EqualTo(3));
        ClickButton(VisibleButton("Replay Mountain"));yield return WaitForNavigation();
        Assert.That(Get(Find("WorldShiftCity"),"LevelIndex"),Is.EqualTo(5));
        Assert.That(Get(Find("WorldShiftCity"),"StoryActive"),Is.False);
        Assert.That(Static(Levels,"IsBeaten",5),Is.True);
        Assert.That(GameObject.Find("Getaway celebration stage"),Is.Null);
    }
}
