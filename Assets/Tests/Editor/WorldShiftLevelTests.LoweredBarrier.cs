using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Playtest: "Does the grey thing slow you down?" A lowered barrier leaves no grey slab on the road,
// only flat world-coloured dashes marking where it rises.
public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator LoweredBarriersLeaveOnlyFlatDashesOnTheRoad()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(1); yield return WaitForNavigation();
        var game = Started();
        float wall = CheckLoweredBarrierBases(game);
        // Picture a lowered Day wall from Night.
        Pose(game, wall - 22f, 0f);
        Call(game, "ApplyWorld", World("Echo"));
        StepBarriers(game);
        for (int i = 0; i < 3; i++) yield return null;
        Capture("lowered-barrier-night");
    }

    // Returns the start of the first full-width Day wall.
    private static float CheckLoweredBarrierBases(MonoBehaviour game)
    {
        var barriers = game.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c.GetType().Name == "WorldShiftRetractableBarrier").ToArray();
        Assert.That(barriers, Is.Not.Empty);
        foreach (var barrier in barriers)
        {
            var mesh = barrier.transform.Find("Flush base").GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices; var colours = mesh.colors;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].y <= .025f) continue;
                Color c = colours.Length > i ? colours[i] : Color.white;
                bool grey = Mathf.Abs(c.r - c.g) < .04f && Mathf.Abs(c.g - c.b) < .06f && c.r < .5f;
                Assert.That(grey, Is.False, $"{barrier.name}: no grey above the road when lowered.");
            }
            Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(.025f), $"{barrier.name}: the lowered footprint is flush with the road.");
        }
        var walls = ((System.Array)Get(game, "Hazards")).Cast<object>()
            .Where(h => Field(h, "Kind").ToString() == "Barrier" && (int)Field(h, "LaneMask") == 7 && Field(h, "World").ToString() == "Day"
                && !(bool)Field(h, "SharedAcrossWorlds"));
        return (float)Field(walls.First(), "Start");
    }

    private static void StepBarriers(MonoBehaviour game)
    {
        foreach (var barrier in game.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c.GetType().Name == "WorldShiftRetractableBarrier"))
            Call(barrier, "Step", 2f);
    }
}
