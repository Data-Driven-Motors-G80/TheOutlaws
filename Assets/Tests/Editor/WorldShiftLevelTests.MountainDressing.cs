using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Mountain finishing touches: valley mist, waterfalls, a distinct Night sky, wind/eagle/goat sounds,
// picture-only falling-rock signs, chalets with lit windows, stone markers and a stone-and-timber checkpoint gate.
public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator MountainHasMistWaterfallsNightSkySoundsAndRoadsideDressing()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        CheckMountainDressing(game);
        // Goats bleat once as a crossing comes up in Day.
        float goat = FirstGoatCrossing(game);
        Pose(game, goat - 30f, 3.4f); Call(game, "ApplyWorld", World("Day"));
        for (int i = 0; i < 4; i++) yield return null;
        Assert.That(Field(Find("WorldShiftLevelAudio"), "lastBleat"), Is.Not.Null, "A goat crossing just ahead bleats.");

        // Pictures: the drop side with mist, a waterfall, the Night sky and a checkpoint gate.
        float gap = ((Vector3[])Field(MountainCourse(), "EdgeGaps"))[0].x;
        Pose(game, gap - 25f, 3.4f); Call(game, "ApplyWorld", World("Day"));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-valley-mist");
        Call(game, "ApplyWorld", World("Echo"));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-guardrail-gap-night");
        Call(game, "ApplyWorld", World("Day"));
        Pose(game, WaterfallDistance(game) - 45f, -3.4f);
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-waterfall");
        Call(game, "ApplyWorld", World("Echo"));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-night-sky");
        float checkpoint = (float)Field(((System.Array)Get(game, "Checkpoints")).GetValue(1), "Distance");
        Pose(game, checkpoint - 30f, 0f); Call(game, "ApplyWorld", World("Day"));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-checkpoint-gate");
    }

    private static void CheckMountainDressing(MonoBehaviour game)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null).ToArray();
        Assert.That(all.Count(t => t.name == "Soft valley mist"), Is.GreaterThanOrEqualTo(20), "Mist banks fill the valley on the drop side.");
        Assert.That(all.Count(t => t.name == "Waterfall"), Is.GreaterThanOrEqualTo(2));
        Assert.That(all.Count(t => t.name == "Falling rocks warning sign"), Is.GreaterThanOrEqualTo(5));
        Assert.That(all.Count(t => t.name == "Timber chalet with lit windows"), Is.GreaterThanOrEqualTo(2));
        Assert.That(all.Count(t => t.name == "Painted stone road marker"), Is.GreaterThanOrEqualTo(20));
        // Guardrail gaps read from far away: advance warning, self-lit chevrons, flashing beacons, kerb paint.
        int gaps = ((Vector3[])Field(MountainCourse(), "EdgeGaps")).Length;
        Assert.That(all.Count(t => t.name == "Guardrail gap advance warning"), Is.EqualTo(gaps));
        Assert.That(all.Count(t => t.name == "Flashing gap beacon"), Is.EqualTo(gaps * 3));
        Assert.That(all.Count(t => t.name == "Gap edge kerb paint"), Is.GreaterThanOrEqualTo(gaps * 15));
        Assert.That(all.Count(t => t.name == "Self-lit chevron board"), Is.EqualTo(gaps * 3));
        foreach (var t in all.Where(t => t.name == "Soft valley mist" || t.name == "Waterfall" || t.name.EndsWith("warning sign")
            || t.name.StartsWith("Timber chalet") || t.name == "Painted stone road marker"))
            Assert.That(t.GetComponentsInChildren<Collider>(true), Is.Empty, $"{t.name} is scenery only.");
        // Mist always sits below the road beside it.
        var route = Get(game, "Route");
        foreach (var mist in all.Where(t => t.name == "Valley mist bank"))
        {
            float nearest = float.MaxValue, roadY = 0f;
            float length = (float)Field(Get(route, "Course"), "Length");
            for (float at = 0f; at < length; at += 6f)
            {
                var p = (Vector3)Get(Call(route, "Sample", at), "Position");
                float flat = new Vector2(p.x - mist.position.x, p.z - mist.position.z).magnitude;
                if (flat < nearest) { nearest = flat; roadY = p.y; }
            }
            Assert.That(mist.GetChild(0).position.y, Is.LessThan(roadY - 10f), "Mist stays well below the road.");
        }
        var sky = RenderSettings.skybox;
        Assert.That(sky.GetFloat("_Mountain"), Is.EqualTo(1f), "Mountain uses its own Night sky (big moon, Milky Way, stars).");
        Assert.That(all.Any(t => t.name == "Mountain wind"), Is.True);
        // Mountain plays its own sounds, never the City crowd or footpath voices (both live in WorldShiftLevelAudio).
        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var wind = sources.Single(s => s.name == "Mountain wind");
        Assert.That(wind.clip != null && wind.clip.name == "WS_MountainWind" && wind.loop && wind.isPlaying, Is.True, "Mountain wind loops.");
        Assert.That(sources.Any(s => s.name == "Mountain wildlife"), Is.True, "Eagle cries and goat bleats have a source.");
        Assert.That(sources.Single(s => s.name == "Environment song").clip.name, Is.EqualTo("Mountain"), "Mountain song.");
        Assert.That(sources.Any(s => s.name.StartsWith("City crowd") || s.name.EndsWith("footpath conversation")), Is.False, "No City sounds on Mountain.");
        Assert.That(Resources.Load<AudioClip>("WS_EagleCry"), Is.Not.Null);
        Assert.That(Resources.Load<AudioClip>("WS_GoatBleat"), Is.Not.Null);
    }

    private static float FirstGoatCrossing(MonoBehaviour game)
        => ((System.Array)Get(game, "Hazards")).Cast<object>().Where(h => Field(h, "Rock") != null).Select(h => (float)Field(h, "Start")).Min();

    private static float WaterfallDistance(MonoBehaviour game)
    {
        var fall = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t != null && t.name == "Waterfall");
        var route = Get(game, "Route");
        float best = 0f, bestDistance = float.MaxValue;
        float length = (float)Field(Get(route, "Course"), "Length");
        for (float at = 0f; at < length; at += 3f)
        {
            var p = (Vector3)Get(Call(route, "Sample", at), "Position");
            float flat = new Vector2(p.x - fall.position.x, p.z - fall.position.z).magnitude;
            if (flat < bestDistance) { bestDistance = flat; best = at; }
        }
        return best;
    }
}
