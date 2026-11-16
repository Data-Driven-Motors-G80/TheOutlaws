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
    [Test]
    public void PoliceSirenClipHasHeadroomAndAContinuousLoop()
    {
        var clip=Resources.Load<AudioClip>("WorldShiftPoliceSiren");
        Assert.That(clip,Is.Not.Null);Assert.That(clip.channels,Is.EqualTo(1));
        Assert.That(clip.length,Is.EqualTo(3.2f).Within(.01f));
        var samples=new float[clip.samples];Assert.That(clip.GetData(samples,0),Is.True);
        Assert.That(samples.Any(v=>float.IsNaN(v)||float.IsInfinity(v)),Is.False);
        double rms=Math.Sqrt(samples.Average(v=>(double)v*v));
        Assert.That(rms,Is.InRange(.20,.60));Assert.That(samples.Max(v=>Math.Abs(v)),Is.LessThan(.9f));
        float seam=samples[0]-samples[samples.Length-1],next=samples[1]-samples[0];
        Assert.That(Mathf.Abs(seam-next),Is.LessThan(.06f),"The loop boundary must follow the waveform instead of clicking or leaving a gap.");
    }

    [Test]
    public void DrivingAudioAssetsAreFiniteAndEveryEnvironmentHasAFullSong()
    {
        string[] names={"WS_Engine","WS_Tyres","WS_Takeoff","WS_Landing","WS_Crash","WS_Gull","WS_Ocean","WS_Victory"};
        foreach(string name in names)
        {
            var clip=Resources.Load<AudioClip>(name);
            Assert.That(clip,Is.Not.Null,name);
            var samples=new float[clip.samples*clip.channels];Assert.That(clip.GetData(samples,0),Is.True,name);
            Assert.That(samples.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<.95f),Is.True,name);
            Assert.That(samples.Any(v=>Math.Abs(v)>.05f),Is.True,name+" must be audible.");
        }
        foreach(string environment in new[]{"City","Desert","Ocean","Snow","Mountain"})
        {
            var song=Resources.Load<AudioClip>("WorldShiftMusic/"+environment);
            Assert.That(song,Is.Not.Null,environment);Assert.That(song.length,Is.GreaterThan(80f));
            Assert.That(song.channels,Is.EqualTo(2));
            var importer=(UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Resources/WorldShiftMusic/"+environment+".mp3");
            Assert.That(importer.defaultSampleSettings.loadType,Is.EqualTo(AudioClipLoadType.CompressedInMemory));
        }
        Assert.That(Resources.Load<TextAsset>("WorldShiftMusic/CREDITS").text,Does.Contain("https://creativecommons.org/licenses/by/4.0/"));
    }

    [UnityTest]
    public IEnumerator DrivingAudioBlendsWorldsPausesAndRespectsIndependentVolumeControls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        string musicKey="ws_audio_music",effectsKey="ws_audio_effects";
        bool hadMusic=PlayerPrefs.HasKey(musicKey),hadEffects=PlayerPrefs.HasKey(effectsKey);
        float oldMusic=PlayerPrefs.GetFloat(musicKey),oldEffects=PlayerPrefs.GetFloat(effectsKey);
        try
        {
            var settings=TypeFor("WorldShiftAudioSettings");
            settings.GetProperty("Music").SetValue(null,.6f);settings.GetProperty("Effects").SetValue(null,.8f);
            StartLevel(2);yield return null;yield return null;
            {
                var desert=Started();var desertAudio=Find("WorldShiftLevelAudio");desertAudio.enabled=false;
                var song=(AudioSource)Field(desertAudio,"music");song.time=10f;Call(desertAudio,"Refresh",1f);
                Assert.That(song.volume,Is.GreaterThan(.3f),"Desert must have an audible soundtrack too.");
                Assert.That(song.clip,Is.EqualTo(Resources.Load<AudioClip>("WorldShiftMusic/Desert")));
                Call(desert,"TogglePause");Call(desertAudio,"Refresh",1f);Capture("desert-recorded-music-controls");
                WorldShiftTestCapture.Capture("pause-menu-4x3",960,720);
                WorldShiftTestCapture.Capture("pause-menu-wide-1080");
                UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(v=>v.name=="Music credits").onClick.Invoke();
                Capture("desert-music-credits");
                var creditText=string.Join("\n",UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Select(v=>v.text));
                Assert.That(creditText,Does.Contain("Whiskey on the Mississippi"));Assert.That(creditText,Does.Contain("Attribution 4.0"));
            }
            StartLevel(3);yield return null;yield return null;
            var game=Started();var audio=Find("WorldShiftLevelAudio");audio.enabled=false;
            var engine=(AudioSource)Field(audio,"engine");var tyres=(AudioSource)Field(audio,"tyres");
            var music=(AudioSource)Field(audio,"music");music.time=10f;
            var filter=music.GetComponent<AudioLowPassFilter>();
            Call(audio,"Refresh",1f);
            Assert.That(engine.volume,Is.GreaterThan(0));Assert.That(music.volume,Is.GreaterThan(.2f));
            Assert.That(music.loop&&engine.loop,Is.True);
            float songPosition=music.time;
            Assert.That(Call(game,"TrySwitch"),Is.True);Call(audio,"Refresh",.375f);
            Assert.That((float)Get(audio,"DayBlend"),Is.EqualTo(.5f).Within(.01f));
            Assert.That(filter.cutoffFrequency,Is.InRange(5000f,17000f));
            Assert.That(music.time,Is.EqualTo(songPosition).Within(.1f),"Switching worlds must not restart the song.");
            Call(audio,"Refresh",.375f);Assert.That(filter.cutoffFrequency,Is.EqualTo(4200f).Within(1f));Assert.That(music.volume,Is.GreaterThan(0));
            Call(game,"TogglePause");Call(audio,"Refresh",1f);Assert.That(Get(audio,"Suspended"),Is.True);
            Capture("ocean-audio-pause-controls");
            var sliders=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsSortMode.None);
            Assert.That(sliders.Length,Is.EqualTo(2));
            var musicSlider=sliders.Single(v=>v.transform.parent.name=="Music volume");
            var effectsSlider=sliders.Single(v=>v.transform.parent.name=="Effects volume");
            effectsSlider.value=0f;
            Call(game,"TogglePause");Call(audio,"Refresh",1f);
            Assert.That(Get(audio,"Suspended"),Is.False);Assert.That(engine.volume,Is.Zero);Assert.That(tyres.volume,Is.Zero);
            Assert.That(music.volume,Is.GreaterThan(0),"Effects mute must preserve the music.");
            var police=Find("WorldShiftPoliceEmergencyEffects");Call(police,"Refresh",1f);
            Assert.That(police.GetComponent<AudioSource>().volume,Is.Zero,"Effects mute includes the existing siren.");
            effectsSlider.value=.8f;musicSlider.value=0f;Call(audio,"Refresh",1f);
            Assert.That(music.volume,Is.Zero);Assert.That(engine.volume,Is.GreaterThan(0));
            Assert.That(PlayerPrefs.GetFloat(musicKey),Is.Zero);Assert.That(PlayerPrefs.GetFloat(effectsKey),Is.EqualTo(.8f));
            StartLevel(0);yield return null;yield return null;
            game=Started();audio=Find("WorldShiftLevelAudio");audio.enabled=false;
            Assert.That(Field(audio,"music"),Is.Null,"Training stays quiet for lessons.");
            Assert.That((float)TypeFor("WorldShiftAudioSettings").GetProperty("Music").GetValue(null),Is.Zero);
            game.GetType().GetProperty("TrainingLessonPaused",Flags).SetValue(game,true);
            Call(audio,"Refresh",1f);Assert.That(Get(audio,"Suspended"),Is.True);
            game.GetType().GetProperty("TrainingLessonPaused",Flags).SetValue(game,false);
            Call(audio,"Refresh",1f);Assert.That(Get(audio,"Suspended"),Is.False);
        }
        finally
        {
            if(hadMusic) PlayerPrefs.SetFloat(musicKey,oldMusic);else PlayerPrefs.DeleteKey(musicKey);
            if(hadEffects) PlayerPrefs.SetFloat(effectsKey,oldEffects);else PlayerPrefs.DeleteKey(effectsKey);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator PoliceSirenAndBeaconsFollowPursuitPauseAndTrainingLessons()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(3);yield return null;yield return null;
        var game=Started();var effects=Find("WorldShiftPoliceEmergencyEffects");
        var source=effects.GetComponent<AudioSource>();
        var red=(Light)Field(effects,"redLight");var blue=(Light)Field(effects,"blueLight");
        Assert.That(source.clip,Is.Not.Null);Assert.That(source.loop,Is.True);
        Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.dopplerLevel,Is.Zero);
        Assert.That(source.rolloffMode,Is.EqualTo(AudioRolloffMode.Custom));
        var player=(Transform)Get(game,"Player");var savedPosition=effects.transform.position;
        effects.transform.position=player.position+Vector3.forward*60f;Call(effects,"Refresh",2f);
        float distantVolume=source.volume;
        effects.transform.position=player.position+Vector3.forward*8f;Call(effects,"Refresh",2f);
        Assert.That(source.volume,Is.GreaterThan(distantVolume*4f),"Approaching the player must increase loudness regardless of the chase camera position.");
        Assert.That(source.volume,Is.LessThanOrEqualTo(.041f),"The close siren must stay behind the music.");
        Assert.That(effects.GetComponent<AudioLowPassFilter>().cutoffFrequency,Is.LessThanOrEqualTo(1600f));
        effects.transform.position=player.position+Vector3.forward*75f;Call(effects,"Refresh",2f);
        Assert.That(source.volume,Is.Zero,"A distant cruiser fades out.");
        effects.transform.position=savedPosition;
        Pose(game,120f,0f);
        var police=(Component)Get(game,"Police");var point=Call(Get(game,"Route"),"Sample",121f);
        police.transform.position=(Vector3)Get(point,"Position")+(Vector3)Get(point,"Right")*2.6f;
        police.transform.rotation=Quaternion.LookRotation((Vector3)Get(point,"Forward"),(Vector3)Get(point,"Up"));
        effects.GetType().GetField("clock",Flags).SetValue(effects,0f);Call(effects,"Refresh",0f);
        Assert.That(Get(effects,"IsPursuing"),Is.True);Assert.That(red.enabled&&blue.enabled,Is.True);
        Assert.That(red.intensity,Is.GreaterThan(5));Assert.That(blue.intensity,Is.LessThan(.1f));
        Capture("police-emergency-day-red");
        Call(effects,"Refresh",.45f);
        Assert.That(red.intensity,Is.LessThan(.1f));Assert.That(blue.intensity,Is.GreaterThan(5));
        Assert.That(Shader.GetGlobalVector("_PoliceBeaconBlue").w,Is.GreaterThan(0));
        Assert.That(Call(game,"TrySwitch"),Is.True);Call(effects,"Refresh",0f);
        Assert.That(Get(effects,"IsPursuing"),Is.True,"Night must not stop pursuit audio or lights.");
        Capture("police-emergency-night-blue");
        float clock=(float)Field(effects,"clock");
        Call(game,"TogglePause");Call(effects,"Refresh",1f);
        Assert.That(Get(effects,"IsPursuing"),Is.False);Assert.That(Get(effects,"SirenPaused"),Is.True);
        Assert.That((float)Field(effects,"clock"),Is.EqualTo(clock));
        Assert.That(red.enabled||blue.enabled,Is.False);Assert.That(Shader.GetGlobalVector("_PoliceBeaconBlue").w,Is.Zero);
        Call(game,"TogglePause");Call(effects,"Refresh",0f);
        Assert.That(Get(effects,"IsPursuing"),Is.True);Assert.That(Get(effects,"SirenPaused"),Is.False);
        Assert.That((float)Field(effects,"clock"),Is.EqualTo(clock),"Resuming preserves the alternating light phase.");
        Call(game,"Fail","crash");Call(effects,"Refresh",.1f);
        Assert.That(Get(effects,"IsPursuing"),Is.True,"The siren continues while the police pull alongside after a collision.");
        Call(game,"Fail","MISSED ITEM");Call(effects,"Refresh",0f);
        Assert.That(Get(effects,"IsPursuing"),Is.False);Assert.That(Get(effects,"SirenPaused"),Is.False);
        Assert.That(source.volume,Is.Zero);Assert.That(red.enabled||blue.enabled,Is.False);
        Call(game,"RetryCheckpoint");Call(effects,"Refresh",.1f);Assert.That(Get(effects,"IsPursuing"),Is.True);
        game.GetType().GetProperty("State",Flags).SetValue(game,Enum.Parse(Get(game,"State").GetType(),"Won"));
        Call(effects,"Refresh",0f);Assert.That(Get(effects,"IsPursuing"),Is.False);Assert.That(source.volume,Is.Zero);
        StartLevel(0);yield return null;yield return null;
        game=Started();effects=Find("WorldShiftPoliceEmergencyEffects");
        Call(effects,"Refresh",.1f);
        game.GetType().GetProperty("TrainingLessonPaused",Flags).SetValue(game,true);
        Call(effects,"Refresh",1f);
        Assert.That(Get(effects,"IsPursuing"),Is.False,"Stopped Training lessons must remain quiet.");
        Assert.That(Get(effects,"SirenPaused"),Is.True);
        game.GetType().GetProperty("TrainingLessonPaused",Flags).SetValue(game,false);
        Call(effects,"Refresh",0f);Assert.That(Get(effects,"IsPursuing"),Is.True);
        effects.enabled=false;
        Assert.That(Shader.GetGlobalVector("_PoliceBeaconRed"),Is.EqualTo(Vector4.zero));
        Assert.That(Shader.GetGlobalVector("_PoliceBeaconBlue"),Is.EqualTo(Vector4.zero));
    }
}
