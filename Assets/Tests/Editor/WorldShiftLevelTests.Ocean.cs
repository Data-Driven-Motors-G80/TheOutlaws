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
    public void OceanItemsAreRequiredSweptAndResetOnlyInTheRetriedSection()
    {
        var course = Static(TypeFor("WorldShiftLevelCourse"), "For", OceanLevel);
        var items = (Vector2[])Field(course, "RequiredItems");
        var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
        var checkpoints = ((Array)Field(course, "Checkpoints")).Cast<object>().ToArray();
        Assert.That(items.Length, Is.EqualTo(11));
        for (int section = 0; section < 5; section++)
        {
            float start = (float)Field(checkpoints[section], "Distance");
            float ramp = (float)Field(ramps[section], "Ramp");
            var targets = items.Where(i => i.x >= start && i.x < ramp).ToArray();
            Assert.That(targets.Length, Is.EqualTo(new[] { 1, 2, 2, 3, 3 }[section]));
            Assert.That(targets.All(i => i.x >= start + 60f && i.x <= ramp - 60f), Is.True);
        }
        for (int level = 0; level < 6; level++)
            if (level != OceanLevel) Assert.That(((Vector2[])Field(Static(TypeFor("WorldShiftLevelCourse"), "For", level), "RequiredItems")), Is.Empty);

        var collection = Activator.CreateInstance(TypeFor("WorldShiftLevelCollection"), new object[] { items });
        Assert.That(Call(collection, "Step", 90f, 110f, -2f, 2f), Is.True, "Swept contact at the target counts.");
        Assert.That(Get(collection, "Count"), Is.EqualTo(1));
        Call(collection, "Step", 90f, 110f, 0f, 0f);
        Assert.That(Get(collection, "Count"), Is.EqualTo(1), "An item counts once.");
        float checkpoint = (float)Field(checkpoints[1], "Distance");
        Call(collection, "Restore", checkpoint);
        var second = items[1];
        Call(collection, "Step", second.x - 2f, second.x + 2f, second.y, second.y);
        Assert.That(Get(collection, "Count"), Is.EqualTo(2));
        Call(collection, "Restore", checkpoint);
        Assert.That(Get(collection, "Count"), Is.EqualTo(1));
        Assert.That(Call(collection, "IsTaken", 1), Is.False);
        Assert.That(Call(collection, "Step", second.x - 2f, second.x + 6f, -second.y, -second.y), Is.False,
            "Passing an uncollected item fails the section.");
        Call(collection, "Restore", 0f);
        Assert.That(Get(collection, "Count"), Is.EqualTo(0), "A full restart clears all collection.");
    }

    [UnityTest]
    public IEnumerator OceanCheckpointGantriesMatchSavesAndConfirmOnlyOnCrossing()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(OceanLevel); yield return WaitForNavigation();
        var game=Started();var coast=Find("WorldShiftCheckpointGantries");
        var checkpoints=(Array)Get(game,"Checkpoints");
        var gates=((IEnumerable)Field(coast,"checkpointGates")).Cast<object>().OrderBy(g=>(int)Field(g,"Index")).ToArray();
        Assert.That(gates.Length,Is.EqualTo(checkpoints.Length-1),"Every saved landing gets one gantry; the start and finish do not.");
        for(int i=0;i<gates.Length;i++)
        {
            var gate=gates[i];float at=(float)Field(gate,"At");
            Assert.That(at,Is.EqualTo((float)Field(checkpoints.GetValue(i+1),"Distance")).Within(.001f));
            var root=(Transform)Field(gate,"Root");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<TMPro.TMP_Text>().Single().text,Is.EqualTo("CHECKPOINT"));
            var structure=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Checkpoint gantry structure");
            foreach(Vector3 vertex in structure.sharedMesh.vertices)
                if(vertex.y<5f) Assert.That(Mathf.Abs(vertex.x),Is.GreaterThan(5.5f),"All low supports must be outside the road.");
        }
        foreach(string mode in new[] {"Day","Echo"})
        {
            object matching=((IEnumerable)checkpoints).Cast<object>().Skip(1).First(c=>Field(c,"World").Equals(World(mode)));
            Pose(game,(float)Field(matching,"Distance")-28f,0f);Call(game,"ApplyWorld",World(mode));
            var nearby=gates.Single(g=>Mathf.Abs((float)Field(g,"At")-(float)Field(matching,"Distance"))<.01f);
            Assert.That(((Transform)Field(nearby,"Root")).gameObject.activeInHierarchy,Is.True);
            Assert.That(((Transform)Field(nearby,"Sweep")).gameObject.activeSelf,Is.False);
            Capture("ocean-checkpoint-gantry-"+(mode=="Day" ? "day" : "night"));
        }
        float first=(float)Field(gates[0],"At");
        Call(Get(game,"Collection"),"Restore",first-1f);
        Pose(game,first-1f,0f);Call(game,"ApplyWorld",Field(checkpoints.GetValue(1),"World"));
        Step(game,.1f);
        Assert.That(Get(game,"CheckpointIndex"),Is.EqualTo(1));
        float pulse=(float)Field(gates[0],"PulseAt");Assert.That(pulse,Is.GreaterThanOrEqualTo(0f));
        Assert.That(((Transform)Field(gates[0],"Saved")).gameObject.activeSelf,Is.True);
        coast.GetType().GetField("animationTime",Flags).SetValue(coast,pulse+.2f);
        Call(game,"PresentWorldForCamera",Get(game,"World"));
        Assert.That((float)Field(gates[0],"PulseAt"),Is.EqualTo(pulse),"Rendering or switching worlds must not replay confirmation.");
        Capture("ocean-checkpoint-gantry-saved");
        Call(game,"RetryCheckpoint");
        Assert.That(((Transform)Field(gates[0],"Sweep")).gameObject.activeSelf,Is.False,"Retrying a saved checkpoint does not replay the light sweep.");
        Assert.That(((Transform)Field(gates[0],"Saved")).gameObject.activeSelf,Is.True);
    }

    [UnityTest]
    public IEnumerator OceanCollectionWorksInBothWorldsAndMissRetriesTheCheckpoint()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(OceanLevel); yield return WaitForNavigation();
        var game = Started();
        var collection = Get(game, "Collection");
        Pose(game, 70f, 0f);
        Capture("ocean-required-diamond-day");
        Step(game, 1.3f);
        Assert.That(Get(collection, "Count"), Is.EqualTo(1));
        Call(game, "RetryCheckpoint");
        Assert.That(Get(collection, "Count"), Is.EqualTo(0));
        Call(game, "TrySwitch");
        Pose(game, 70f, 0f);
        Capture("ocean-required-diamond-night");
        Step(game, 1.3f);
        Assert.That(Get(collection, "Count"), Is.EqualTo(1), "Night collects the same diamond.");
        float checkpoint = (float)Field(((Array)Get(game, "Checkpoints")).GetValue(1), "Distance");
        Pose(game, checkpoint - 1f, 0f);
        Step(game, .1f);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(1));
        Assert.That((float)Get(game, "CheckpointNoticeRemaining"), Is.GreaterThan(0f));
        Capture("ocean-checkpoint-counter-1080");
        var hud = Find("WorldShiftLevelHud");
        var counter = (RectTransform)hud.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Required diamonds");
        Assert.That(counter, Is.Not.Null);
        Assert.That(hud.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Checkpoint notice"), Is.False,
            "The checkpoint gantry confirms saves; the HUD has no checkpoint pop-up.");
        Call(game, "RetryCheckpoint");
        var items = (Vector2[])Field(collection, "Items");
        Pose(game, items[1].x - 3f, 3.3f);
        Step(game, .45f);
        Assert.That(Get(game, "MissedRequiredItem"), Is.True);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(1));
        Capture("ocean-missed-diamond");
        for (int i = 0; i < 40 && Get(game, "State").ToString() == "Retrying"; i++)
            Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That((float)Get(game, "Distance"), Is.EqualTo((float)Field(((Array)Get(game, "Checkpoints")).GetValue(1), "Distance")).Within(.01f));
        Assert.That(Get(collection, "Count"), Is.EqualTo(1));
        Assert.That(Call(collection, "IsTaken", 1), Is.False);
    }
}
