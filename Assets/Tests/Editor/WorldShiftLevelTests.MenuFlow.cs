using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator MenuLoadsTrainingAndAllFiveLevelsFromTheSharedScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        PlayerPrefs.SetInt(UnlockAllKey, 1);
        Static(Levels, "Menu"); yield return WaitForNavigation();
        string[] environments = { "City", "City", "Ocean", "Mountain", "Snow", "Desert" };
        Vector3[] playerShape=null,policeShape=null;
        Color[] playerPaint=null,policePaint=null;
        for (int level = 0; level < environments.Length; level++)
        {
            var menu = Find("WorldShiftMenu");
            ClickButton(menu.GetComponentsInChildren<Button>().Single(b => b.name == "Level " + level));
            yield return WaitForNavigation();
            var game = Started();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("City_WorldShift"));
            Assert.That(Get(game, "LevelIndex"), Is.EqualTo(level));
            Assert.That(Get(game, "Environment").ToString(), Is.EqualTo(environments[level]));
            Assert.That((bool)Field(Get(game, "Course"), "Training"), Is.EqualTo(level == 0));
            Assert.That(((Array)Get(game, "Checkpoints")).Length, Is.GreaterThan(0));
            Assert.That(((Array)Get(game, "Hazards")).Length, Is.GreaterThan(0));
            if (level == OceanLevel) Assert.That(((Vector2[])Field(Get(game, "Course"), "RequiredItems")).Length, Is.EqualTo(12));
            var playerArt=((Transform)Get(game,"Player")).GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c.GetType().Name=="WorldShiftPlayerCarArt").ToArray();
            var policeArt=((Component)Get(game,"Police")).GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c.GetType().Name=="WorldShiftPoliceCarArt").ToArray();
            Assert.That(playerArt.Length,Is.EqualTo(1),"Every level must use exactly one shared player coupe.");
            Assert.That(policeArt.Length,Is.EqualTo(1),"Every level must use exactly one shared police cruiser.");
            Assert.That(policeArt[0].GetComponentsInChildren<MonoBehaviour>(true).Count(c=>c.GetType().Name=="WorldShiftPoliceEmergencyEffects"),Is.EqualTo(1),
                "Every current level must bind the same police siren and lights.");
            var levelAudio=Find("WorldShiftLevelAudio");
            Assert.That(levelAudio,Is.Not.Null);
            var song=(AudioSource)Field(levelAudio,"music");
            if(level==0) Assert.That(song,Is.Null);
            else { Assert.That(song.clip,Is.Not.Null);Assert.That(song.clip.length,Is.GreaterThan(80)); }
            var pMeshes=playerArt[0].GetComponentsInChildren<MeshFilter>(true).OrderBy(m=>m.name).ToArray();
            var cMeshes=policeArt[0].GetComponentsInChildren<MeshFilter>(true).OrderBy(m=>m.name).ToArray();
            Vector3[] pShape=pMeshes.SelectMany(m=>m.sharedMesh.vertices).ToArray(),cShape=cMeshes.SelectMany(m=>m.sharedMesh.vertices).ToArray();
            Color[] pPaint=pMeshes.SelectMany(m=>m.sharedMesh.colors).ToArray(),cPaint=cMeshes.SelectMany(m=>m.sharedMesh.colors).ToArray();
            if(level==0) {playerShape=pShape;policeShape=cShape;playerPaint=pPaint;policePaint=cPaint;}
            else
            {
                Assert.That(pShape,Is.EqualTo(playerShape),"Player geometry must match Training in level "+level);
                Assert.That(cShape,Is.EqualTo(policeShape),"Police geometry must match Training in level "+level);
                Assert.That(pPaint,Is.EqualTo(playerPaint),"Player livery must stay consistent in level "+level);
                Assert.That(cPaint,Is.EqualTo(policePaint),"Police livery must stay consistent in level "+level);
            }
            Assert.That(playerArt[0].GetComponentsInChildren<Collider>(true),Is.Empty,"Vehicle art must not change driving collision rules.");
            Step(game, .1f);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            Assert.That((float)Get(game, "Distance"), Is.GreaterThan(0f));
            if(level==1 || level==OceanLevel)
            {
                Pose(game,70f,0f);
                var point=Call(Get(game,"Route"),"Sample",71f);
                var police=(Component)Get(game,"Police");
                police.transform.position=(Vector3)Get(point,"Position")+(Vector3)Get(point,"Right")*2.6f;
                police.transform.rotation=Quaternion.LookRotation((Vector3)Get(point,"Forward"),(Vector3)Get(point,"Up"));
                foreach(string mode in new[] {"Day","Echo"})
                {
                    Call(game,"ApplyWorld",World(mode));
                    Capture("shared-vehicles-"+environments[level].ToLowerInvariant()+"-"+(mode=="Day" ? "day" : "night"));
                }
            }
            Static(Levels, "Menu"); yield return WaitForNavigation();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WorldShift_Menu"));
        }
    }

    [UnityTest]
    public IEnumerator GetawayCelebrationSavesBeforePlayingAndSupportsNaturalFinishAndSkip()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        foreach(int level in new[]{3,1,2,4,5})
        {
            StartLevel(level);yield return WaitForNavigation();
            var game=Started();float end=(float)Get(game,"RunLength");
            Call(Get(game,"Collection"),"Restore",end+10f);
            var ramps=(Array)Field(Get(game,"Course"),"Ramps");var last=ramps.GetValue(ramps.Length-1);
            Pose(game,end-.5f,0f);Call(game,"ApplyWorld",Get(last,"Destination"));Step(game,.1f);
            Assert.That(Get(game,"State").ToString(),Is.EqualTo("Won"),"A successful finish starts the celebration.");
            Assert.That(Static(Levels,"IsBeaten",level),Is.True,"Progress must be saved before the celebration starts.");
            var escape=(MonoBehaviour)Get(game,"Escape");Call(escape,"EnsureStarted");
            Assert.That(Get(escape,"Active"),Is.True);Assert.That(Get(escape,"Ready"),Is.False);
            Assert.That(((Transform)Field(escape,"stage")).GetComponentsInChildren<Collider>().Length,Is.Zero);
            Call(escape,"Skip");Assert.That(Get(escape,"Ready"),Is.False,"A held finish input must not instantly skip the first moment.");
            if(level==OceanLevel)
            {
                var audio=Find("WorldShiftLevelAudio");Call(audio,"Refresh",0f);
                Assert.That((bool)Field(audio,"winCuePlayed"),Is.False,"Save the victory sting for the reveal.");
                Call(escape,"Advance",2.5f);Capture("ocean-ferry-escape-boarding");
                var ferry=(Transform)Field(escape,"ferry");var police=(Transform)Field(escape,"cruiser");
                Assert.That(ferry.localPosition.z,Is.GreaterThan(24));
                Call(escape,"Advance",4f);Call(audio,"Refresh",0f);
                Assert.That((bool)Field(audio,"winCuePlayed"),Is.True);
                Assert.That(ferry.localPosition.z-police.localPosition.z,Is.GreaterThan(30));
                Assert.That(((Transform)Field(escape,"ramp")).localEulerAngles.x,Is.GreaterThan(60));
                Capture("ocean-ferry-you-lost-them");
            }
            else
            {
                Call(escape,"Advance",.8f);Call(escape,"Skip");
                Assert.That(((Transform)Field(escape,"shutter")).localPosition.y,Is.EqualTo(3f).Within(.01f));
                Assert.That(((Transform)Field(escape,"coupe")).gameObject.activeSelf,Is.EqualTo(level==5));
                Capture("escape-passage-level-"+level);
            }
            Assert.That(Get(escape,"Ready"),Is.True);
            float distance=(float)Get(game,"Distance");Step(game,.5f);
            Assert.That((float)Get(game,"Distance"),Is.EqualTo(distance),"The cinematic must never resume simulation after success.");
            var source=((Transform)Field(escape,"stage")).GetComponentsInChildren<AudioSource>().Single(v=>v.name=="Receding police siren");
            Assert.That(source.volume,Is.Zero,"The police are now too far away to hear.");
        }
        Static(Levels,"Menu");yield return WaitForNavigation();
        Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(v=>v.GetType().Name=="WorldShiftLevelEscape"),Is.False);
        Assert.That(GameObject.Find("Getaway celebration stage"),Is.Null);
    }

    [Test]
    public void LevelsUnlockInOrderAndTheTestSwitchOpensEverything()
    {
        Static(Levels, "ResetProgress");
        PlayerPrefs.SetInt(UnlockAllKey, 0);
        Assert.That(Static(Levels, "IsUnlocked", 0), Is.True);
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.False);
        Static(Levels, "Complete", 0);
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.True);
        Assert.That(Static(Levels, "IsUnlocked", 2), Is.False);
        Static(Levels, "Complete", 0);
        Assert.That(StaticGet(Levels, "HighestBeaten"), Is.EqualTo(0), "Replaying never lowers progress.");
        Levels.GetProperty("UnlockAll").SetValue(null, true);
        Assert.That(Static(Levels, "IsUnlocked", 5), Is.True);
        Assert.That(StaticGet(Levels, "HighestBeaten"), Is.EqualTo(0), "Unlock All does not change saved progress.");
    }

    [UnityTest]
    public IEnumerator HomeShowsSixLevelsWithLocksUntilBeaten()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        Static(Levels, "ResetProgress");
        PlayerPrefs.SetInt(UnlockAllKey, 0);
        yield return new EnterPlayMode();
        var menu = Find("WorldShiftMenu");
        var tiles = menu.GetComponentsInChildren<Button>().Select(b => b.name).ToArray();
        Assert.That(tiles, Is.EqualTo(new[] { "Level 0", "Level 1", "Level 2", "Level 3", "Level 4", "Level 5" }));
        Assert.That(menu.GetComponentsInChildren<Transform>().Count(t => t.name == "Lock badge"), Is.EqualTo(5));
        Assert.That(menu.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text), Does.Not.Contain("Upgrade car"));
        Assert.That(menu.GetComponentsInChildren<RawImage>().Where(v=>v.name=="Environment illustration").Count(v=>v.texture!=null),Is.EqualTo(6));
        Capture("menu-locked");
        WorldShiftTestCapture.Capture("menu-locked-4x3",960,720);
        WorldShiftTestCapture.Capture("menu-locked-portrait",720,1080);
        menu.GetComponentsInChildren<Button>().Single(b => b.name == "Level 3").onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WorldShift_Menu"), "Locked levels do not start.");
        Levels.GetProperty("UnlockAll").SetValue(null, true);
        Call(menu, "BuildLevels");
        yield return null;
        Assert.That(menu.GetComponentsInChildren<Transform>().Count(t => t.name == "Lock badge"), Is.EqualTo(0));
        Capture("menu-unlock-all");
    }

    [UnityTest]
    public IEnumerator ContinueButtonsAdvanceExactlyOneLevelThroughTheWholeCampaign()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        PlayerPrefs.SetInt(BeatenKey,-1);PlayerPrefs.SetInt(UnlockAllKey,0);
        Static(Levels,"Menu");yield return WaitForNavigation();
        ClickButton(VisibleButton("Level 3"));yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("WorldShift_Menu"),"Locked cards cannot start a level.");
        ClickButton(VisibleButton("Level 0"));yield return WaitForNavigation();
        for(int level=0;level<6;level++)
        {
            Assert.That(Get(Find("WorldShiftCity"),"StoryActive"),Is.EqualTo(level==1||level==2),"City and Ocean have skippable introductions.");
            var game=Started();Assert.That(Get(game,"LevelIndex"),Is.EqualTo(level));
            string[] expected = { "City", "City", "Ocean", "Mountain", "Snow", "Desert" };
            Assert.That(Get(game,"Environment").ToString(),Is.EqualTo(expected[level]));
            Assert.That(Get(Get(game,"Escape"),"IsFinale"),Is.EqualTo(level==DesertLevel),"Only Desert ends the campaign.");
            Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
            FinishForButtons(game);
            var escape=Get(game,"Escape");if((bool)Get(escape,"Active")) Call(escape,"Advance",Get(escape,"SequenceDuration"));
            var hud=Find("WorldShiftLevelHud");Call(hud,"Refresh");yield return null;
            Assert.That(StaticGet(Levels,"HighestBeaten"),Is.EqualTo(level),"Continue must use real saved unlocks.");
            if(level==5) {Call(hud,"CompleteFinale");yield return null;Call(hud,"Refresh");}
            var next=(Button)Field(hud,"finishContinue");int attempt=(int)StaticGet(Levels,"Attempt");
            ClickButton(next);
            // A rapid second click/submit from the outgoing screen must not skip a destination.
            ExecuteEvents.Execute(next.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
            yield return WaitForNavigation();
            if(level<5)
            {
                Assert.That(StaticGet(Levels,"Current"),Is.EqualTo(level+1));
                Assert.That(StaticGet(Levels,"Attempt"),Is.EqualTo(attempt+1));
            }
            else Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("WorldShift_Menu"));
        }
    }

    [UnityTest]
    public IEnumerator PauseCreditsRestartMenuSkipAndReplayButtonsReceiveClicks()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(2);yield return WaitForNavigation();
        var game=Started();var hud=Find("WorldShiftLevelHud");Call(hud,"Refresh");
        var pauseButton=VisibleButton("Pause button");ClickButton(pauseButton);
        ExecuteEvents.Execute(pauseButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
        Call(hud,"Refresh");yield return null;
        Assert.That(Get(game,"State").ToString(),Is.EqualTo("Paused"));
        ClickButton(VisibleButton("Music credits"));yield return null;
        Assert.That(((GameObject)Field(hud,"musicCreditsPanel")).activeSelf,Is.True);
        ButtonPointer(VisibleButton("Song source"));ButtonPointer(VisibleButton("CC BY 4.0"));
        Assert.That(Uri.IsWellFormedUriString((string)Static(TypeFor("WorldShiftMusicLibrary"),"SourceUrl",Get(game,"Environment")),UriKind.Absolute),Is.True);
        ClickButton(VisibleButton("Back"));yield return null;
        Assert.That(((GameObject)Field(hud,"musicCreditsPanel")).activeSelf,Is.False);
        var resume=VisibleButton("Resume");ClickButton(resume);
        ExecuteEvents.Execute(resume.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
        Call(hud,"Refresh");yield return null;
        Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
        ClickButton(VisibleButton("Pause button"));Call(hud,"Refresh");yield return null;
        int attempt=(int)StaticGet(Levels,"Attempt");ClickButton(VisibleButton("Restart level"));
        yield return WaitForNavigation();game=Started();hud=Find("WorldShiftLevelHud");
        Assert.That(Get(game,"LevelIndex"),Is.EqualTo(2));Assert.That(StaticGet(Levels,"Attempt"),Is.EqualTo(attempt+1));
        Assert.That((float)Get(game,"Distance"),Is.LessThan(10));
        Call(hud,"Refresh");yield return null;ClickButton(VisibleButton("Pause button"));Call(hud,"Refresh");yield return null;ClickButton(VisibleButton("Level select"));
        yield return WaitForNavigation();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("WorldShift_Menu"));
        ClickButton(VisibleButton("Level 3"));yield return WaitForNavigation();
        game=Started();hud=Find("WorldShiftLevelHud");FinishForButtons(game);
        var escape=Get(game,"Escape");Call(escape,"Advance",.8f);Call(hud,"Refresh");yield return null;
        ClickButton(VisibleButton("Skip / Esc"));Assert.That(Get(escape,"Ready"),Is.True);
        Assert.That(StaticGet(Levels,"Current"),Is.EqualTo(3),"Skip only ends the cinematic.");yield return null;
        ClickButton(VisibleButton("Replay"));yield return WaitForNavigation();
        game=Started();Assert.That(Get(game,"LevelIndex"),Is.EqualTo(3));
        FinishForButtons(game);Call(Get(game,"Escape"),"Advance",6.5f);hud=Find("WorldShiftLevelHud");Call(hud,"Refresh");yield return null;
        ClickButton(VisibleButton("Level select"));yield return WaitForNavigation();
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("WorldShift_Menu"));
    }

    [UnityTest]
    public IEnumerator CityOpeningComicHoldsDrivingSupportsButtonsAndSkipsRetries()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        analytics.Clear();StartLevel(1);yield return WaitForNavigation();
        var game=Find("WorldShiftCity");game.enabled=false;var hud=Find("WorldShiftLevelHud");
        Assert.That(Get(game,"StoryActive"),Is.True);Assert.That(Get(game,"State").ToString(),Is.EqualTo("Ready"));
        Assert.That(analytics.Count(a=>a.name=="wl_level_start"),Is.Zero,"Reading the story is outside the driving attempt.");
        Step(game,2f);Call(game,"BeginRun");
        Assert.That((float)Get(game,"Distance"),Is.Zero);Assert.That((float)Get(game,"Duration"),Is.Zero);
        Assert.That(Get(game,"State").ToString(),Is.EqualTo("Ready"));
        var audio=Find("WorldShiftLevelAudio");Call(audio,"Refresh",1f);Assert.That(Get(audio,"Suspended"),Is.True);
        var siren=Find("WorldShiftPoliceEmergencyEffects");Call(siren,"Refresh",1f);Assert.That(Get(siren,"IsPursuing"),Is.False);
        var art=(RawImage)Field(hud,"storyArt");Assert.That(art.texture,Is.Not.Null);Assert.That(art.uvRect.x,Is.Zero);
        Capture("city-opening-the-device");WorldShiftTestCapture.Capture("city-opening-4x3",960,720);
        var next=(Button)Field(hud,"storyNext");ClickButton(next);
        ExecuteEvents.Execute(next.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
        Assert.That(Field(hud,"storyPage"),Is.EqualTo(1),"One input advances one comic panel.");yield return null;
        Capture("city-opening-the-alarm");
        ClickButton((Button)Field(hud,"storyBack"));Assert.That(Field(hud,"storyPage"),Is.EqualTo(0));yield return null;
        ClickButton(next);yield return null;ClickButton(next);yield return null;
        Assert.That(Field(hud,"storyPage"),Is.EqualTo(2));Capture("city-opening-the-getaway");
        Assert.That(next.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("Start getaway"));
        ClickButton(next);
        Assert.That(Get(game,"StoryActive"),Is.False);Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
        Assert.That(((GameObject)Field(hud,"storyRoot")).activeSelf,Is.False);
        Assert.That(analytics.Count(a=>a.name=="wl_level_start"),Is.EqualTo(1));
        Call(audio,"Refresh",1f);Assert.That(Get(audio,"Suspended"),Is.False);
        Step(game,.2f);Assert.That((float)Get(game,"Distance"),Is.GreaterThan(0));
        Static(Levels,"Retry");yield return WaitForNavigation();
        game=Find("WorldShiftCity");game.enabled=false;
        Assert.That(Get(game,"StoryActive"),Is.False,"Restart must go straight back to the road.");
        Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
        Assert.That(GameObject.Find("City opening story"),Is.Null);
        StartLevel(1);yield return WaitForNavigation();
        game=Find("WorldShiftCity");game.enabled=false;Assert.That(Get(game,"StoryActive"),Is.True);
        ClickButton(VisibleButton("Skip"));
        Assert.That(Get(game,"StoryActive"),Is.False);Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
        Assert.That(Get(game,"World").ToString(),Is.EqualTo("Day"));
        StartLevel(1);yield return WaitForNavigation();
        Assert.That(Get(Find("WorldShiftCity"),"StoryActive"),Is.True);
        Static(Levels,"Menu");yield return WaitForNavigation();
        Assert.That(GameObject.Find("City opening story"),Is.Null,"Leaving the scene cleans up the whole comic.");
        StartLevel(2);yield return WaitForNavigation();
        Assert.That(Get(Find("WorldShiftCity"),"StoryActive"),Is.True);
        Assert.That(GameObject.Find("City opening story"),Is.Null,"Ocean uses its own fly-in, not the City comic.");
    }
    [UnityTest]
    public IEnumerator MenuIsLaidOutBeforeFirstDrawAndLoadingRespondsImmediately()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        // Allocate captured state after the Play-mode domain reload.
        yield return CheckMenuStartup();
    }

    private static IEnumerator CheckMenuStartup()
    {
        Vector2 firstSize=Vector2.zero,firstPosition=Vector2.zero;float firstScale=0;
        UnityEngine.Events.UnityAction<Scene,LoadSceneMode> loaded=(scene,mode)=>
        {
            if(scene.name!="WorldShift_Menu") return;
            var menu=Find("WorldShiftMenu");
            var tile=(RectTransform)menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Level 0").transform;
            firstSize=tile.rect.size;firstPosition=tile.anchoredPosition;
            firstScale=tile.GetComponentInParent<Canvas>().scaleFactor;
        };
        SceneManager.sceneLoaded+=loaded;
        try {Static(Levels,"Menu");yield return WaitForNavigation();}
        finally {SceneManager.sceneLoaded-=loaded;}
        var current=Find("WorldShiftMenu");
        var card=(RectTransform)current.GetComponentsInChildren<Button>().Single(b=>b.name=="Level 0").transform;
        Assert.That(firstScale,Is.GreaterThan(0));
        Assert.That(Vector2.Distance(firstSize,card.rect.size),Is.LessThan(.1f),"Cards must have their final dimensions before the first draw.");
        Assert.That(Vector2.Distance(firstPosition,card.anchoredPosition),Is.LessThan(.1f));
        Assert.That(firstScale,Is.EqualTo(card.GetComponentInParent<Canvas>().scaleFactor).Within(.001f));
        Capture("menu-settled-first-frame");
        var clock=System.Diagnostics.Stopwatch.StartNew();
        ClickButton(VisibleButton("Level 0"));clock.Stop();
        Debug.Log("Worldshift menu selection returned in "+clock.Elapsed.TotalMilliseconds.ToString("F1")+" ms.");
        Assert.That((bool)StaticGet(TypeFor("WorldShiftSceneLoading"),"Active"),Is.True);
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("WorldShift_Menu"),"The input callback must return before scene construction.");
        Assert.That(GameObject.Find("Loading backdrop"),Is.Not.Null,"The same click creates visible loading feedback.");
        Capture("menu-loading-immediate");
        Assert.That(Static(Levels,"Play",0,"menu"),Is.False,"Loading rejects further selections, even for an unlocked level.");
        yield return WaitForNavigation();
        Assert.That(StaticGet(Levels,"Current"),Is.EqualTo(0));
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("City_WorldShift"));
        Assert.That(GameObject.Find("Worldshift scene loading"),Is.Null,"Loading presentation cleans up after the new scene settles.");
    }

}
