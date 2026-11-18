using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator SnowfallBuildsOverIceFadesBeforeJumpsAndPauses()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(SnowLevel);yield return WaitForNavigation();
        var game=Started();var scenery=Find("WorldShiftEnvironmentScenery");
        var particles=(ParticleSystem)Field(scenery,"snowParticles");
        Assert.That(UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Shader>("WorldShiftSnowfall")),Is.False);
        Assert.That(particles.GetComponent<ParticleSystemRenderer>().renderMode,Is.EqualTo(ParticleSystemRenderMode.Billboard));
        var zones=(Vector2[])Field(Get(game,"Course"),"IceZones");
        Pose(game,20f,0);Call(scenery,"UpdateVisibility",20f);Call(scenery,"RefreshSnowfall");
        Assert.That(Get(scenery,"SnowfallStrength"),Is.EqualTo(0f));
        float light=particles.emission.rateOverTime.constant;
        Assert.That(light,Is.EqualTo(22f));
        foreach(var zone in zones)
        {
            float middle=(zone.x+zone.y)*.5f;
            Pose(game,middle,0);Call(scenery,"UpdateVisibility",middle);Call(scenery,"RefreshSnowfall");
            Assert.That(Get(scenery,"SnowfallStrength"),Is.EqualTo(1f));
            Assert.That(particles.emission.rateOverTime.constant,Is.GreaterThan(light*6));
            Assert.That(particles.main.startSize.constantMax,Is.LessThanOrEqualTo(.18f));
            float exit=zone.y-10f;
            Pose(game,exit,0);Call(scenery,"UpdateVisibility",exit);Call(scenery,"RefreshSnowfall");
            Assert.That((float)Get(scenery,"SnowfallStrength"),Is.InRange(0.01f,.20f));
            Pose(game,zone.y+10f,0);Call(scenery,"UpdateVisibility",zone.y+10f);Call(scenery,"RefreshSnowfall");
            Assert.That(particles.emission.rateOverTime.constant,Is.EqualTo(light));
        }
        float at=zones[0].x+35f;
        Pose(game,at,0);Call(scenery,"UpdateVisibility",at);Call(scenery,"RefreshSnowfall");
        foreach(string mode in new[]{"Day","Echo"})
        {
            Call(game,"ApplyWorld",World(mode));Call(scenery,"LateUpdate");
            float fogEnd=RenderSettings.fogEndDistance;
            particles.Simulate(4f,true,true);particles.Pause();
            Assert.That(particles.particleCount,Is.GreaterThan(400));
            Capture("snow-heavy-ice-"+mode.ToLowerInvariant());
            Assert.That(RenderSettings.fogEndDistance,Is.EqualTo(fogEnd),"Heavier flakes must not shorten road visibility.");
        }
        Call(game,"TogglePause");Call(scenery,"RefreshSnowfall");float paused=particles.time;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(particles.time,Is.EqualTo(paused));Assert.That(particles.isPaused,Is.True);
        Call(game,"TogglePause");Call(scenery,"RefreshSnowfall");Assert.That(particles.isPlaying,Is.True);
        Pose(game,0f,0);Call(scenery,"UpdateVisibility",0f);Call(scenery,"RefreshSnowfall");
        Assert.That(particles.particleCount,Is.Zero,"A checkpoint teleport clears the old heavy cloud.");
        Assert.That(particles.emission.rateOverTime.constant,Is.EqualTo(light));
    }
}
