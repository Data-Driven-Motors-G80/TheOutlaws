using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class WorldShiftLevelTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string BeatenKey = "WorldShift.Levels.HighestBeaten", UnlockAllKey = "WorldShift.Levels.UnlockAll";
    private int savedBeaten, savedUnlockAll;
    private readonly List<(string sheet, string name, Dictionary<string, object> data)> analytics = new List<(string, string, Dictionary<string, object>)>();

    private static Type TypeFor(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift." + name)).First(t => t != null);
    private static Type Levels => TypeFor("WorldShiftLevels");
    private static object Get(object o, string n) => o.GetType().GetProperty(n, Flags).GetValue(o);
    private static object Field(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    private static object Static(Type t, string n, params object[] args) => t.GetMethod(n, Flags).Invoke(null, args);
    private static object StaticGet(Type t, string n) => t.GetProperty(n, Flags).GetValue(null);
    private static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == name);
    private static object World(string name) => Enum.Parse(TypeFor("ShiftWorld"), name);
    private static void Capture(string name)
    {
        // The simulation is stepped manually; bring the HUD up to date before the picture.
        var hud = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(c => c.GetType().Name == "WorldShiftLevelHud");
        if (hud != null) Call(hud, "Refresh");
        WorldShiftTestCapture.Capture("level-" + name);
    }
    private static void Pose(MonoBehaviour game, float distance, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, distance);
        game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
        Call(Field(game, "driver"), "RestoreExternalPose", distance, x);
        Call(game, "UpdatePolice", 0f);
    }
    private static void Step(MonoBehaviour game, float seconds, float steer = 0f)
    {
        for (float t = 0f; t < seconds; t += .05f) Call(game, "StepSimulation", .05f, steer, false);
    }

    [SetUp]
    public void Save()
    {
        savedBeaten = PlayerPrefs.GetInt(BeatenKey, -1);
        savedUnlockAll = PlayerPrefs.GetInt(UnlockAllKey, 0);
        analytics.Clear();
        Action<string, string, Dictionary<string, object>> sink = (sheet, name, data) => analytics.Add((sheet, name, data));
        TypeFor("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, sink);
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        TypeFor("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, null);
        Static(Levels, "Leave");
        PlayerPrefs.SetInt(BeatenKey, savedBeaten);
        PlayerPrefs.SetInt(UnlockAllKey, savedUnlockAll);
        PlayerPrefs.Save();
    }

    // Call after EnterPlayMode (nested enumerators cannot enter play mode), then wait two frames.
    private void StartLevel(int level)
    {
        // Play mode resets session statics, so attach the analytics sink again.
        Action<string, string, Dictionary<string, object>> sink = (sheet, name, data) => analytics.Add((sheet, name, data));
        TypeFor("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, sink);
        PlayerPrefs.SetInt(UnlockAllKey, 1);
        Assert.That(Static(Levels, "Play", level, "menu"), Is.True);
    }

    private static MonoBehaviour Started()
    {
        var game = Find("WorldShiftCity");
        game.enabled = false;
        // Edit-mode tests may find the object after Awake but before its first frame.
        // Initialize the attempt as well as driving before manually stepping it.
        if (Get(game, "State").ToString() == "Ready") Call(game, "Start");
        return game;
    }

    [UnityTest]
    public IEnumerator MenuLoadsTrainingAndAllFiveLevelsFromTheSharedScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        PlayerPrefs.SetInt(UnlockAllKey, 1);
        Static(Levels, "Menu"); yield return null; yield return null;
        string[] environments = { "City", "City", "Desert", "Ocean", "Snow", "Mountain" };
        for (int level = 0; level < environments.Length; level++)
        {
            var menu = Find("WorldShiftMenu");
            menu.GetComponentsInChildren<Button>().Single(b => b.name == "Level " + level).onClick.Invoke();
            yield return null; yield return null;
            var game = Started();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("City_WorldShift"));
            Assert.That(Get(game, "LevelIndex"), Is.EqualTo(level));
            Assert.That(Get(game, "Environment").ToString(), Is.EqualTo(environments[level]));
            Assert.That((bool)Field(Get(game, "Course"), "Training"), Is.EqualTo(level == 0));
            Assert.That(((Array)Get(game, "Checkpoints")).Length, Is.GreaterThan(0));
            Assert.That(((Array)Get(game, "Hazards")).Length, Is.GreaterThan(0));
            if (level == 3) Assert.That(((Vector2[])Field(Get(game, "Course"), "RequiredItems")).Length, Is.EqualTo(11));
            Step(game, .1f);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            Assert.That((float)Get(game, "Distance"), Is.GreaterThan(0f));
            Static(Levels, "Menu"); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WorldShift_Menu"));
        }
    }

    [Test]
    public void EveryCourseEndsSectionsOnRampsAndLeavesTimeToSwitch()
    {
        var builder = TypeFor("WorldShiftLevelCourse");
        for (int level = 0; level < 6; level++)
        {
            object course = Static(builder, "For", level);
            var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
            var hazards = ((Array)Field(course, "Hazards")).Cast<object>().ToArray();
            var checkpoints = ((Array)Field(course, "Checkpoints")).Cast<object>().ToArray();
            float length = (float)Field(course, "Length");
            Assert.That(((Array)Field(course, "Signs")).Length, Is.EqualTo(level == 0 ? 6 : 0),
                "Only Training has instructional highway signs.");
            Assert.That(ramps.Length, Is.EqualTo(level == 0 ? 1 : level == 2 ? 6 : 5), "level " + level);
            Assert.That(checkpoints.Length, Is.EqualTo(level == 2 ? 7 : level == 0 ? 6 : 5), "Respawn positions, including the initial spawn, level " + level);
            if (level == 0)
            {
                Assert.That(hazards.Any(h => (float)Field(h, "Start") == 90f && (bool)Field(h, "SharedAcrossWorlds")), Is.True,
                    "The white opening block teaches that permanent hazards survive a switch.");
                Assert.That(hazards.Any(h => Field(h, "Kind").ToString() == "SwitchWall"), Is.True,
                    "The tutorial has a real orange wall that clears after switching.");
                Assert.That(hazards.Any(h => Field(h, "Kind").ToString() == "Oncoming" && (bool)Field(h, "SharedAcrossWorlds")), Is.True,
                    "Oncoming traffic has a separate playable lesson.");
            }
            for (int i = 1; i < checkpoints.Length; i++)
                Assert.That((float)Field(checkpoints[i], "Distance"), Is.GreaterThan((float)Field(checkpoints[i - 1], "Distance")));
            foreach (var ramp in ramps)
            {
                float start = (float)Field(ramp, "Ramp") - 30f, end = (float)Field(ramp, "End") + 10f;
                foreach (var h in hazards.Where(h => (float)Field(h, "MotionSpeed") == 0f))
                    Assert.That((float)Field(h, "End") < start || (float)Field(h, "Start") > end, Is.True,
                        $"Level {level}: ramp approach and landing at {start + 30f} stay clear of obstacles.");
            }
            var walls = hazards.Where(h => (int)Field(h, "LaneMask") == 7 && (float)Field(h, "MotionSpeed") == 0f).ToArray();
            foreach (var a in walls)
                foreach (var b in walls)
                    if (!Field(a, "World").Equals(Field(b, "World")))
                        Assert.That(Mathf.Abs((float)Field(a, "Start") - (float)Field(b, "Start")), Is.GreaterThanOrEqualTo(35f),
                            $"Level {level}: opposite full walls at {Field(a, "Start")} and {Field(b, "Start")} need a switch window.");
            foreach (var checkpoint in checkpoints)
            {
                float at = (float)Field(checkpoint, "Distance");
                foreach (var wall in walls.Where(w => Field(w, "World").Equals(Field(checkpoint, "World"))))
                    Assert.That((float)Field(wall, "Start") - at, Is.Not.InRange(0f, 45f), $"Level {level}: respawn at {at} is not right in front of a wall.");
            }
            foreach (var h in hazards) Assert.That((float)Field(h, "End"), Is.LessThan(length));
        }
    }

    [Test]
    public void OceanItemsAreRequiredSweptAndResetOnlyInTheRetriedSection()
    {
        var course = Static(TypeFor("WorldShiftLevelCourse"), "For", 3);
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
            if (level != 3) Assert.That(((Vector2[])Field(Static(TypeFor("WorldShiftLevelCourse"), "For", level), "RequiredItems")), Is.Empty);

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
    public IEnumerator OceanArtKeepsWorldSwitchJumpGapsOpen()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(3); yield return null; yield return null;
        var game = Started(); var coast = Find("WorldShiftOceanPresentation");
        Assert.That(coast.GetComponentsInChildren<Collider>(true), Is.Empty,
            "Coastal scenery must never add physical obstacles to the authored course.");
        var landmarks=coast.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Ocean landmark - ")).ToArray();
        Assert.That(landmarks.Length,Is.EqualTo(5),"Each Ocean section should have its own authored landmark.");
        Assert.That(coast.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Soft contact shadow"),Is.True);
        var route = Get(game, "Route");
        var jumps = (Array)Get(game, "RelayLayout");
        foreach (object jump in jumps)
        {
            float takeoff = (float)Field(jump, "Takeoff");
            float ramp = (float)Field(jump, "Ramp"), recovery = (float)Field(jump, "End");
            Pose(game, takeoff - 20f, 0f);
            foreach (string mode in new[] { "Day", "Echo" })
            {
                Call(game, "PresentWorldForCamera", World(mode));
                bool checkedGap = false;
                foreach (object chunk in (IEnumerable)Field(coast, "chunks"))
                {
                    if (!(bool)Field(chunk, "Deck")) continue;
                    float start = (float)Field(chunk, "At"), end = (float)Field(chunk, "End");
                    if (start < ramp || end > recovery) continue;
                    bool surface = (bool)Call(route, "HasSurface", (start + end) * .5f, World(mode));
                    Assert.That(((GameObject)Field(chunk, "Root")).activeSelf, Is.EqualTo(surface),
                        "Bridge railings and supports must disappear with the jump deck.");
                    checkedGap = true;
                }
                Assert.That(checkedGap, Is.True);
                foreach(object chunk in (IEnumerable)Field(coast,"chunks"))
                {
                    object onlyWorld=Field(chunk,"OnlyWorld");
                    if(onlyWorld==null || Mathf.Abs((float)Field(chunk,"At")-ramp)>.01f) continue;
                    Assert.That(((GameObject)Field(chunk,"Root")).activeSelf,Is.EqualTo(onlyWorld.Equals(World(mode))),
                        "Landing frames belong to the landing world and must not imply a solid source-world landing.");
                }
            }
        }
        for(int section=0;section<jumps.Length;section++)
        {
            float from=section==0 ? 0 : (float)Field(jumps.GetValue(section-1),"End");
            Pose(game,from+65f,0f);Call(game,"PresentWorldForCamera",World("Day"));
            Capture("ocean-region-"+(section+1));
        }
        // Representative sea-stack and final-jump views, beyond the opening cove.
        var checkpoints = (Array)Get(game, "Checkpoints");
        Pose(game, (float)Field(checkpoints.GetValue(2), "Distance") + 45f, 0f);
        Call(game, "PresentWorldForCamera", World("Day")); Capture("ocean-sea-stacks-day");
        var final = jumps.GetValue(jumps.Length - 1);
        Pose(game, (float)Field(final, "Ramp") - 35f, 0f);
        Call(game, "PresentWorldForCamera", World("Day")); Capture("ocean-final-jump-day");
        Call(game, "PresentWorldForCamera", World("Echo")); Capture("ocean-final-jump-night");
    }

    [UnityTest]
    public IEnumerator OceanObstacleArtFitsItsCollisionFootprintsAndSwitchesWithItsWorld()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(3); yield return null; yield return null;
        var game = Started(); var presentation = Field(game, "presentation");
        bool capturedContainer = false, capturedBarrier = false, capturedCargo = false, capturedTrolley = false, capturedVan = false;
        foreach (object hazardView in (IEnumerable)Field(presentation, "hazards"))
        {
            var data = Field(hazardView, "Data");
            var solid = (GameObject)Field(hazardView, "Solid");
            var ghost = (GameObject)Field(hazardView, "Ghost");
            Assert.That(solid.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Obstacle X"), Is.False,
                "Ocean hazards should use coastal models instead of prototype X marks.");
            bool moving = (bool)Field(data, "LateralMotion");
            float width = moving ? 1.6f : (int)Field(data, "LaneMask") == 7 ? 10f : 3.05f;
            float depth = (float)Field(data, "End") - (float)Field(data, "Start");
            var extent = new Vector3(width, 1.8f, depth) * .5f + Vector3.one * .08f;
            foreach (Transform model in solid.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "World-coloured coastal hazard").Select(t => t.parent))
            {
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                    foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 p = model.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                        Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(extent.x));
                        Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(extent.y));
                        Assert.That(Mathf.Abs(p.z), Is.LessThanOrEqualTo(extent.z));
                    }
            }
            float distance = (float)Field(data, "Start");
            Pose(game, distance - 25f, 0f);
            foreach (string mode in new[] {"Day", "Echo"})
            {
                object world = World(mode);
                Call(game, "PresentWorldForCamera", world);
                bool visible = (bool)Call(data, "VisibleIn", world);
                Assert.That(solid.activeSelf, Is.EqualTo(visible));
                Assert.That(ghost.activeSelf, Is.EqualTo(!moving && !visible));
            }
            Call(game, "PresentWorldForCamera", Field(data, "World"));
            string label = null;
            if (!capturedContainer && solid.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Shipping container"))
            { label = "container"; capturedContainer = true; }
            if (!capturedBarrier && solid.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Harbour wave barricade"))
            { label = "barricade"; capturedBarrier = true; }
            if (!capturedCargo && solid.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Fishing cargo stack"))
            { label = "fishing-cargo"; capturedCargo = true; }
            if (!capturedTrolley && moving) { label = "cargo-trolley"; capturedTrolley = true; }
            if (!capturedVan && solid.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Parked coastal service van"))
            { label = "service-van"; capturedVan = true; }
            if (label != null)
            {
                Pose(game, distance - 12f, 0f);
                Call(game, "PresentWorldForCamera", Field(data, "World"));
                Capture("ocean-obstacle-" + label);
            }
        }
        Assert.That(capturedContainer && capturedBarrier && capturedCargo && capturedTrolley, Is.True,
            "The Ocean course should present all four coastal obstacle silhouettes.");
    }

    [UnityTest]
    public IEnumerator OncomingTrafficHasApproachingHeadlightsAndWheelsThatFollowTravel()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(1); yield return null; yield return null;
        var game=Started(); var presentation=Field(game,"presentation");
        int count=0;
        foreach(object hazardView in (IEnumerable)Field(presentation,"hazards"))
        {
            var data=Field(hazardView,"Data");
            if(Field(data,"Kind").ToString()!="Oncoming" || (bool)Field(data,"SharedAcrossWorlds")) continue;
            var root=(Transform)Field(hazardView,"Root");
            var solid=(GameObject)Field(hazardView,"Solid");
            var ghost=(GameObject)Field(hazardView,"Ghost");
            var car=solid.GetComponentsInChildren<MonoBehaviour>(true).Single(c=>c.GetType().Name=="WorldShiftRoadVehicleArt");
            Assert.That(Vector3.Dot(car.transform.forward,root.forward),Is.LessThan(-.99f),
                "Oncoming vehicles must face the player, rather than showing their tail lights.");
            var wheels=car.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Traffic wheel and six-spoke rim").ToArray();
            Assert.That(wheels.Length,Is.EqualTo(4));
            Assert.That(car.GetComponentsInChildren<Collider>(true),Is.Empty,
                "Art must use the existing swept collision footprint, without new physics colliders.");
            // Every visible vehicle part must stay inside the established road-space envelope.
            foreach(var mesh in car.GetComponentsInChildren<MeshFilter>(true))
                foreach(Vector3 vertex in mesh.sharedMesh.vertices)
                {
                    Vector3 p=car.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex));
                    Assert.That(Mathf.Abs(p.x),Is.LessThanOrEqualTo(1.605f));
                    Assert.That(Mathf.Abs(p.y),Is.LessThanOrEqualTo(.98f));
                    Assert.That(Mathf.Abs(p.z),Is.LessThanOrEqualTo(2.83f));
                }
            float start=(float)Field(data,"Start");
            Pose(game,start-85f,0f); Call(game,"PresentWorldForCamera",Field(data,"World"));
            Vector3 before=root.position; Quaternion wheelBefore=wheels[0].localRotation;
            Pose(game,start-65f,0f); Call(game,"PresentWorldForCamera",Field(data,"World"));
            Assert.That(Vector3.Distance(before,root.position),Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(wheelBefore,wheels[0].localRotation),Is.GreaterThan(1f));
            Assert.That(solid.activeSelf,Is.True); Assert.That(ghost.activeSelf,Is.False);
            if(count==0)
            {
                Capture("oncoming-vehicle-day");
                Pose(game,start-55f,0f); Call(game,"PresentWorldForCamera",Field(data,"World"));
                Capture("oncoming-vehicle-close");
            }
            object other=World(Field(data,"World").ToString()=="Day" ? "Echo" : "Day");
            Call(game,"PresentWorldForCamera",other);
            Assert.That(solid.activeSelf,Is.False); Assert.That(ghost.activeSelf,Is.True);
            count++;
        }
        Assert.That(count,Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator OceanPoliceCruiserKeepsItsIdentityInBothWorlds()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(3); yield return null; yield return null;
        var game=Started();var police=(Component)Get(game,"Police");
        var cruisers=police.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c.GetType().Name=="WorldShiftPoliceCarArt").ToArray();
        Assert.That(cruisers.Length,Is.EqualTo(1),"Ocean must keep the shared marked cruiser, rather than replacing it with a coupe.");
        var cruiser=cruisers[0];
        Assert.That(cruiser.GetComponentsInChildren<Collider>(true),Is.Empty,"Police art must not alter catch-up collision behaviour.");
        var body=cruiser.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Black and white police body");
        Assert.That(body.sharedMesh.colors.Any(c=>c.r>.85f&&c.g>.85f&&c.b>.85f),Is.True);
        Assert.That(body.sharedMesh.colors.Any(c=>c.r<.1f&&c.g<.1f&&c.b<.1f),Is.True);
        Assert.That(cruiser.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="POLICE livery and roof unit number").sharedMesh.vertexCount,Is.GreaterThan(0));
        Pose(game,120f,0f);
        for(int mode=0;mode<2;mode++)
        {
            if(mode==1) Assert.That(Call(game,"TrySwitch"),Is.True);
            Assert.That(cruiser.gameObject.activeInHierarchy,Is.True);
            var red=cruiser.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Red roof light lens");
            var blue=cruiser.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Blue roof light lens");
            Assert.That(red.sharedMesh.colors.All(c=>c.r>.8f&&c.b<.2f),Is.True);
            Assert.That(blue.sharedMesh.colors.All(c=>c.b>.8f&&c.r<.2f),Is.True);
            // Pull alongside for the review image, like the existing crash/capture animation.
            var route=Get(game,"Route");var point=Call(route,"Sample",121f);
            police.transform.position=(Vector3)Get(point,"Position")+(Vector3)Get(point,"Right")*2.6f;
            police.transform.rotation=Quaternion.LookRotation((Vector3)Get(point,"Forward"),(Vector3)Get(point,"Up"));
            Capture("ocean-police-cruiser-"+(mode==0 ? "day" : "night"));
        }
    }

    [UnityTest]
    public IEnumerator LevelSwitchesRespondImmediatelyInFlightAndAfterEveryLanding()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        for(int level=0;level<6;level++)
        {
            StartLevel(level);yield return null;yield return null;
            var game=Started();var jump=((Array)Get(game,"RelayLayout")).GetValue(0);
            float takeoff=(float)Field(jump,"Takeoff"),landing=(float)Field(jump,"Landing");
            float lane=level==2 ? 3.3f : 0f; // The existing safe bypass of Desert's first aerial pair.
            object source=Field(jump,"Source"),destination=Get(jump,"Destination");
            var checkpoints=((IEnumerable)Get(game,"Checkpoints")).Cast<object>().ToArray();
            int checkpoint=Array.FindLastIndex(checkpoints,c=>(float)Field(c,"Distance")<takeoff);
            game.GetType().GetField("checkpointIndex",Flags).SetValue(game,checkpoint);
            Call(game,"RetryCheckpoint");
            game.GetType().GetField("trainingLessonRead",Flags).SetValue(game,true);
            Call(Get(game,"Collection"),"Restore",takeoff-.5f);
            Pose(game,takeoff-.5f,lane);Call(game,"ApplyWorld",source);
            var route=Get(game,"Route");
            Assert.That(Call(route,"HasSurface",landing+2f,source),Is.False,"The landing must stay missing in the launch world before a successful flight.");
            Step(game,.03f);
            Assert.That(Get(game,"IsRelayAirborne"),Is.True,"Level "+level+" should have taken off.");
            float airborneAt=(float)Get(game,"Distance");
            foreach(object expected in new[] {destination,source,destination})
            {
                Call(game,"StepSimulation",0f,0f,true);
                Assert.That(Get(game,"World"),Is.EqualTo(expected),"Every separate Space press should commit in the current frame, even with no simulation time.");
                Assert.That((float)Get(game,"Distance"),Is.EqualTo(airborneAt));
            }
            for(int frame=0;(float)Get(game,"Distance")<landing+.1f && frame<250;frame++)
            {
                Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"),"Level "+level+" at "+Get(game,"Distance")+": "+Field(game,"lastFailure"));Step(game,.05f);
            }
            Assert.That(Get(game,"IsRelayAirborne"),Is.False);
            float landedAt=(float)Get(game,"Distance");
            Call(game,"StepSimulation",0f,0f,true);
            Assert.That(Get(game,"World"),Is.EqualTo(source),"Level "+level+" silently drops Space during the old 56-metre landing lock.");
            Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"),"An immediate return switch after a valid landing must have a real recovery deck.");
            Assert.That(Call(route,"HasSurface",landedAt,source),Is.True);
            object roadPiece=((IEnumerable)Field(route,"pieces")).Cast<object>().First(p=>(float)Field(p,"Start")<=landedAt && (float)Field(p,"End")>landedAt);
            Assert.That(((Renderer)Field(roadPiece,"Surface")).enabled,Is.True,"The shared collision deck must be visibly solid immediately.");
            if(level==3) Capture("ocean-landed-return-switch");
            Call(game,"StepSimulation",0f,0f,true);
            Assert.That(Get(game,"World"),Is.EqualTo(destination));
            Assert.That((float)Get(game,"Distance"),Is.EqualTo(landedAt));
            // A retry before the ramp must restore the puzzle, not leave the recovered deck unlocked.
            Call(game,"RetryCheckpoint");
            Assert.That(Call(route,"HasSurface",landing+2f,source),Is.False);
            game.GetType().GetField("trainingLessonRead",Flags).SetValue(game,true);
            Call(Get(game,"Collection"),"Restore",takeoff-.5f);
            Pose(game,takeoff-.5f,lane);Call(game,"ApplyWorld",source);
            for(int frame=0;Get(game,"State").ToString()=="Running" && (float)Get(game,"Distance")<landing+2f && frame<250;frame++)
                Step(game,.05f);
            Assert.That(Get(game,"State").ToString(),Is.EqualTo("Retrying"),"Not switching in the air must still miss the landing.");
            Assert.That((string)Field(game,"lastFailure"),Is.EqualTo("MISSED RELAY LANDING"),"Level "+level+" must still require the destination world at touchdown.");
        }
    }

    [UnityTest]
    public IEnumerator OceanCheckpointGantriesMatchSavesAndConfirmOnlyOnCrossing()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(3); yield return null; yield return null;
        var game=Started();var coast=Find("WorldShiftOceanPresentation");
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
            var structure=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Coastal checkpoint structure");
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
        StartLevel(3); yield return null; yield return null;
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
        var notice = (RectTransform)hud.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Checkpoint notice");
        var counterCorners = new Vector3[4]; var noticeCorners = new Vector3[4];
        counter.GetWorldCorners(counterCorners); notice.GetWorldCorners(noticeCorners);
        Assert.That(noticeCorners.Max(c => c.y), Is.LessThan(counterCorners.Min(c => c.y)),
            "The checkpoint notice stays below the diamond counter.");
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
        Capture("menu-locked");
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
    public IEnumerator OneHitCrashLetsPoliceCatchUpThenRestartsAtTheCheckpoint()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(1); yield return null; yield return null;
        var game = Started();
        Assert.That(Get(game, "LevelMode"), Is.True);
        Assert.That(Get(game, "Combat"), Is.Null, "No shooting in level play.");
        Assert.That(Get(game, "Getaway"), Is.Null, "No cash, fuel pickups, power-ups or forks.");
        Assert.That(Get(game, "Environment").ToString(), Is.EqualTo("City"));
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        var firstBlock = ((IEnumerable)Get(game, "Hazards")).Cast<object>()
            .Single(h => (float)Field(h, "Start") == 50f && (int)Field(h, "LaneMask") == 2);
        Assert.That(Field(firstBlock, "SharedAcrossWorlds"), Is.True,
            "The first center block remains solid when the player switches worlds.");
        Capture("city-start");
        for (int i = 0; i < 200 && Get(game, "State").ToString() == "Running"; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "The first block in the middle lane ends the attempt.");
        Assert.That(Get(game, "Capturing"), Is.True);
        Assert.That(Get(game, "Deaths"), Is.EqualTo(1));
        var police = (Component)Get(game, "Police");
        var player = (Transform)Get(game, "Player");
        float before = Vector3.Distance(police.transform.position, player.position);
        Step(game, .8f);
        Assert.That(Vector3.Distance(police.transform.position, player.position), Is.LessThan(before - 2f), "The police close in after a crash.");
        Capture("police-catch");
        for (int i = 0; i < 40 && Get(game, "State").ToString() != "Running"; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(0f).Within(.01f));
        var death = analytics.Single(a => a.name == "wl_death");
        Assert.That(death.data["cause"], Is.EqualTo("crash"));
        Assert.That(death.data["hazard_world"], Is.EqualTo("both"), "The opening permanent block exists in both worlds.");
    }

    [UnityTest]
    public IEnumerator OtherWorldObstaclesShowAsDashesAndSwitchingIntoOneCrashes()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(1); yield return null; yield return null;
        var game = Started();
        // City section 1: a Night block covers the left and middle lanes at 150 m.
        Pose(game, 132f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        var ghost = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Echo Barrier 150").Find("Outline - belongs to other world");
        Assert.That(ghost.gameObject.activeInHierarchy, Is.True, "Night's obstacle is visible as dashes from Day.");
        Capture("day-sees-night-dashes");
        Pose(game, 151f, 0f);
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "Switching inside the dashes fills them in.");
        Assert.That(analytics.Single(a => a.name == "wl_death").data["cause"], Is.EqualTo("switched_into"));
        Capture("switched-into-dashes");
    }

    [UnityTest]
    public IEnumerator DesertSlidersMoveInDayAndFreezeInNight()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(2); yield return null; yield return null;
        var game = Started();
        Assert.That(Get(game, "Environment").ToString(), Is.EqualTo("Desert"));
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Highway sign")), Is.False);
        var slider = ((Array)Get(game, "Hazards")).Cast<object>().First(h => (bool)Field(h, "LateralMotion"));
        Step(game, .6f);
        float moved = (float)Get(slider, "MotionTravel");
        Assert.That(moved, Is.GreaterThan(0f));
        Capture("desert-day");
        Call(game, "TrySwitch");
        Step(game, .6f);
        Assert.That((float)Get(slider, "MotionTravel"), Is.EqualTo(moved).Within(.0001f), "Sliders freeze in Night.");
        Capture("desert-night");
    }

    [UnityTest]
    public IEnumerator TrainingFreezeDemonstrationWaitsSafelyAndAcceptsEarlyInput()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(0); yield return null; yield return null;
        var game = Started();
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 4);
        Call(game, "RetryCheckpoint");
        game.GetType().GetField("trainingLessonRead", Flags).SetValue(game, true);
        Pose(game, 768f, 4f);
        Step(game, .2f);
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true), $"distance={Get(game, "Distance")} stop={Get(game, "TrainingFreezeStop")} state={Get(game, "State")} sign={Get(game, "TrainingLessonPaused")}");
        float distance = (float)Get(game, "Distance"), duration = (float)Get(game, "Duration");
        var sliders = ((Array)Field(game, "levelHazards")).Cast<object>()
            .Where(h => (bool)Field(h, "LateralMotion") && (float)Field(h, "Start") >= 700f && (float)Field(h, "End") < 970f).ToArray();
        Assert.That(sliders.Length, Is.EqualTo(3));
        Capture("training-freeze-demonstration-1080");
        float motion = (float)Get(sliders[0], "MotionTravel");
        Step(game, 1f, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(distance).Within(.001f));
        Assert.That((float)Get(game, "Duration"), Is.EqualTo(duration).Within(.001f));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.Not.EqualTo(motion));
        Assert.That(Get(game, "Deaths"), Is.EqualTo(0));
        Call(game, "TogglePause");
        motion = (float)Get(sliders[0], "MotionTravel");
        Step(game, 1f);
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.EqualTo(motion));
        Call(game, "TogglePause");
        Call(game, "TrySwitch");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(1));
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true), "The first press demonstrates freezing without releasing the car.");
        Step(game, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(distance).Within(.001f));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.EqualTo(motion));
        Capture("training-freeze-night-stopped-1080");
        Call(game, "TrySwitch");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(2));
        Step(game, 1f);
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true));
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(distance).Within(.001f));
        Assert.That((float)Get(game, "Duration"), Is.EqualTo(duration).Within(.001f));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.Not.EqualTo(motion), "Day visibly restores obstacle motion while the car waits.");
        Capture("training-freeze-day-moving-1080");
        motion = (float)Get(sliders[0], "MotionTravel");
        Call(game, "TrySwitch");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(3));
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(false));
        Assert.That(analytics.Count(a => a.name == "wl_freeze_demo"), Is.EqualTo(1), "The demonstration time is recorded once.");
        Step(game, .25f);
        Assert.That((float)Get(game, "Distance"), Is.GreaterThan(distance));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.EqualTo(motion));
        // Once the action is demonstrated, the checkpoint never tests compliance again.
        Call(game, "TrySwitch");
        Pose(game, 969f, 4f);
        Step(game, .1f);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(5));

        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 4);
        Call(game, "RetryCheckpoint");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(0));
        game.GetType().GetField("trainingLessonRead", Flags).SetValue(game, true);
        Pose(game, 740f, 4f);
        Call(game, "TrySwitch");
        Step(game, 2f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(770f).Within(.01f));
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(1), "Early freezing counts, but both states must be demonstrated.");
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true));
        Call(game, "TrySwitch");
        Step(game, .25f);
        Call(game, "TrySwitch");
        Step(game, 11f);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(5), "All three frozen sliders leave a navigable opening.");
        Assert.That(Get(game, "Deaths"), Is.EqualTo(0));
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 4);
        Call(game, "RetryCheckpoint");
        game.GetType().GetField("trainingLessonRead", Flags).SetValue(game, true);
        Pose(game, 768f, 0f);
        Step(game, .2f);
        Call(game, "TrySwitch");
        Call(game, "TrySwitch");
        Call(game, "TrySwitch");
        Pose(game, 809f, 0f);
        Step(game, .1f);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "Frozen sliders remain solid.");
        Step(game, 3f);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(4));
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator TrainingUsesSignsAndFinishingUnlocksLevelOne()
    {
        Static(Levels, "ResetProgress");
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(0); yield return null; yield return null;
        PlayerPrefs.SetInt(UnlockAllKey, 0);
        var game = Started();
        Assert.That(Get(game, "TrainingLessonPaused"), Is.False);
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "Training stops with the road sign in view.");
        float firstSignStop = (float)Get(game, "Distance");
        Assert.That(firstSignStop, Is.GreaterThan(0f).And.LessThan(25f));
        Step(game, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(firstSignStop).Within(.001f));
        Call(game, "ContinueTrainingLesson");
        Step(game, .1f);
        Assert.That((float)Get(game, "Distance"), Is.GreaterThan(0f));
        Assert.That(game.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Highway sign")), Is.EqualTo(6));
        foreach (string lesson in new[] { "Steer", "Switch", "Dashes", "Oncoming", "Freeze", "Ramp" })
        {
            var sign = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Highway sign " + lesson);
            var artwork = sign.Find("Sign face/Sign artwork");
            Assert.That(artwork, Is.Not.Null, lesson + " uses the supplied road-sign artwork.");
            Assert.That(artwork.GetComponent<Renderer>().sharedMaterial.mainTexture, Is.Not.Null);
        }
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Obstacle lesson"), Is.False,
            "The road signs teach with pictograms instead of sentences.");
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Oncoming direction" && t.localPosition.y > 1f), Is.True,
            "The oncoming arrow is raised above the block and faces the driver.");
        var gates = game.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Checkpoint gate").ToArray();
        Assert.That(gates.Length, Is.GreaterThan(0));
        foreach (var gate in gates)
        {
            Assert.That(gate.Find("Gate lintel"), Is.Null, "Training checkpoints leave the view over the road open.");
            Assert.That(gate.Find("Gate post"), Is.Null, "Checkpoints use one horizontal road stripe, without upright lines.");
            Assert.That(gate.Find("CHECKPOINT cue"), Is.Null);
            Assert.That(gate.GetComponentsInChildren<Transform>().Count(t => t.name == "Cube"), Is.EqualTo(1));
        }
        Pose(game, 177f, 0f);
        Step(game, .2f);
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "The next instruction waits by its own road sign.");
        float stoppedAt = (float)Get(game, "Distance");
        Step(game, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(stoppedAt).Within(.001f));
        Call(game, "ContinueTrainingLesson");
        Pose(game, 20f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        Capture("training-steer-sign");
        object course = Get(game, "Course");
        var ramp = ((Array)Field(course, "Ramps")).GetValue(0);
        // This test isolates the ramp; freeze gating is covered separately.
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 5);
        Pose(game, (float)Field(ramp, "Ramp") - 50f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        Capture("training-ramp");
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Ramp air cue"), Is.False);
        bool attemptedLanding = false;
        for (int i = 0; i < 400 && Get(game, "State").ToString() == "Running"; i++)
        {
            if ((bool)Get(game, "TrainingLessonPaused")) Call(game, "ContinueTrainingLesson");
            Call(game, "StepSimulation", .05f, 0f, false);
            if ((bool)Get(game, "IsRelayAirborne") && Get(game, "World").ToString() == "Day")
            {
                attemptedLanding = true;
                Call(game, "TrySwitch");
            }
        }
        Assert.That(attemptedLanding, Is.True);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"), "Launch in Day, switch midair, land in Night, reach the finish.");
        Assert.That(StaticGet(Levels, "HighestBeaten"), Is.EqualTo(0));
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.True);
        Assert.That(Static(Levels, "IsUnlocked", 2), Is.False);
        yield return null;
        Capture("training-complete");
        var row = analytics.Single(a => a.sheet == "ws_level_attempts").data;
        Assert.That(row["outcome"], Is.EqualTo("complete"));
        Assert.That(row["level"], Is.EqualTo(0));
        // The test skips ahead by reflection, so check the section the player actually finished in.
        Assert.That(row["first_try_" + row["section_at_end"]], Is.Not.EqualTo(-1), "Finishing the level resolves the final section's first try.");
    }

    [UnityTest]
    public IEnumerator TrainingRecordsSignReadingTriesAndWhereLessonsFail()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(0); yield return null; yield return null;
        var game = Started();
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True);
        Assert.That(analytics.Count(a => a.name == "wl_sign_shown"), Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.5f);
        Call(game, "ContinueTrainingLesson");
        var read = analytics.Single(a => a.name == "wl_sign_read").data;
        Assert.That(read["lesson"], Is.EqualTo(0));
        Assert.That((float)read["read_s"], Is.GreaterThanOrEqualTo(.45f), "Reading time is the wall-clock time the card stayed up.");
        Assert.That(read["how"], Is.EqualTo("continue"));

        // Drive straight into the steering lesson's middle block.
        Pose(game, 84f, 0f);
        Step(game, 1f);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Assert.That(analytics.Last(a => a.name == "wl_death").data["lane"], Is.EqualTo("M"));
        for (int i = 0; i < 400 && !(bool)Get(game, "TrainingLessonPaused"); i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "The sign returns after a crash.");
        Assert.That(analytics.Last(a => a.name == "wl_sign_shown").data["view"], Is.EqualTo(2));
        // A manual restart while the sign is up closes that view and counts as another try.
        Call(game, "RetryCheckpoint");
        Assert.That(analytics.Last(a => a.name == "wl_sign_read").data["how"], Is.EqualTo("restart"));

        Static(TypeFor("WorldShiftLevelAnalytics"), "EndAttempt", "menu");
        var row = analytics.Single(a => a.sheet == "ws_level_attempts").data;
        Assert.That(row["reads_0"], Is.EqualTo(2));
        Assert.That((float)row["read_0"], Is.GreaterThanOrEqualTo(.45f), "First reading of the sign.");
        Assert.That((float)row["read_total_0"], Is.GreaterThanOrEqualTo((float)row["read_0"]));
        Assert.That((float)row["read_s"], Is.GreaterThanOrEqualTo(.45f));
        Assert.That(row["retries_0"], Is.EqualTo(1));
        Assert.That(row["tries_0"], Is.EqualTo(3), "First go + one crash + one manual restart.");
        Assert.That(row["first_try_0"], Is.EqualTo(0));
        Assert.That(row["first_try_1"], Is.EqualTo(-1));
        StringAssert.IsMatch(@"^\d+:M:crash$", (string)row["fails_0"]);
        Assert.That(row["quit_on_sign"], Is.EqualTo(-1));
        Assert.That((float)row["freeze_demo_s"], Is.EqualTo(-1f));
        yield return null;
    }
}
