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
    public IEnumerator CityNightLightingConnectsFixturesRoadAndSharedSurfaces()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(1);yield return null;yield return null;
        var game=Started();var scenery=Find("WorldShiftEnvironmentScenery");var lighting=Find("WorldShiftLocalLighting");
        lighting.enabled=false;
        Pose(game,175f,0f);Call(game,"ApplyWorld",World("Echo"));Call(Find("WorldShiftPresentation"),"UpdateVisibility",175f);Call(lighting,"Refresh");
        var lamps=scenery.GetComponentsInChildren<Light>(true);
        Assert.That(lamps.Length,Is.GreaterThan(10));
        Assert.That(lamps.All(v=>v.type==LightType.Spot&&v.range>=15f&&v.innerSpotAngle<v.spotAngle),Is.True);
        Assert.That(lamps.Any(v=>v.isActiveAndEnabled),Is.True);
        Assert.That((int)Get(lighting,"ActiveLightCount"),Is.InRange(1,8));
        Assert.That(Shader.GetGlobalInt("_OutlawsLightCount"),Is.EqualTo(Get(lighting,"ActiveLightCount")));
        Assert.That(scenery.GetComponentsInChildren<Collider>(true).Any(v=>v.enabled),Is.False);
        var glows=scenery.GetComponentsInChildren<Renderer>(true).Where(v=>v.name=="Streetlamp optical glow").ToArray();
        Assert.That(glows.Length,Is.EqualTo(lamps.Length));Assert.That(glows.Any(v=>v.isVisible||v.enabled),Is.True);
        var nightRoad=(Material)Field(Get(game,"Route"),"echoRoad");
        Assert.That(nightRoad.GetFloat("_Specular"),Is.GreaterThan(.3f));Assert.That(nightRoad.GetFloat("_Grain"),Is.GreaterThan(.5f));
        foreach(string shader in new[]{"WorldShiftOceanSurface","WorldShiftLampGlow"})
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Shader>(shader)),Is.False,shader);
        Capture("city-streetlight-night-reflections");
        Call(game,"ApplyWorld",World("Day"));Call(Find("WorldShiftPresentation"),"UpdateVisibility",175f);Call(lighting,"Refresh");
        Assert.That(lamps.Any(v=>v.enabled),Is.False);Assert.That(glows.Any(v=>v.enabled),Is.False);
        Capture("city-streetlight-day-finish");
        Call(game,"ApplyWorld",World("Echo"));Call(Find("WorldShiftPresentation"),"UpdateVisibility",175f);Call(lighting,"Refresh");
        Assert.That(lamps.Any(v=>v.isActiveAndEnabled),Is.True,"Switching back must restore the lighting immediately.");
        lighting.enabled=true;lighting.enabled=false;
        Assert.That(Shader.GetGlobalInt("_OutlawsLightCount"),Is.Zero,"No stale lights may leak into another scene.");
    }

    [UnityTest]
    public IEnumerator CityAndDesertSceneryMeetRoadEdgesAndFailureNoticeClearsOnRetry()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        for (int level = 1; level <= 2; level++)
        {
            StartLevel(level); yield return null; yield return null;
            var game = Started();
            var scenery = Find("WorldShiftEnvironmentScenery");
            Assert.That(scenery.GetComponentsInChildren<Collider>(true).Any(c => c.enabled), Is.False);
            var route = Get(game, "Route");
            var side = Find("WorldShiftRoadside");
            var roads = ((IEnumerable)Field(route, "pieces")).Cast<object>().ToArray();
            var shoulders = ((IEnumerable)Field(side, "pieces")).Cast<object>().ToArray();
            Assert.That(shoulders.Length, Is.EqualTo(roads.Length));
            for (int i = 0; i < roads.Length; i++)
            {
                var roadRenderer = (Renderer)Field(roads[i], "Surface");
                var roadMesh = roadRenderer.GetComponent<MeshFilter>().sharedMesh;
                var sideMesh = (Mesh)Field(shoulders[i], "Mesh");
                var edges = roadMesh.vertices; var vertices = sideMesh.vertices;
                for (int j = 0; j < edges.Length / 2; j++)
                {
                    Assert.That(Vector3.Distance(roadRenderer.transform.TransformPoint(edges[j*2]),side.transform.TransformPoint(vertices[j*6+2])), Is.LessThan(.001f));
                    Assert.That(Vector3.Distance(roadRenderer.transform.TransformPoint(edges[j*2+1]),side.transform.TransformPoint(vertices[j*6+3])), Is.LessThan(.001f));
                }
            }
            if (level == 2)
            {
                int oases = 0, trees = 0;
                foreach (Transform child in scenery.transform)
                { if (child.name == "Desert oasis") oases++; if (child.name == "Roadside coconut palm") trees++; }
                Assert.That(oases, Is.GreaterThan(2)); Assert.That(trees, Is.GreaterThan(5));
            }
            if (level == 1)
                Assert.That(scenery.GetComponentsInChildren<Light>(true).Length, Is.GreaterThan(10), "Original streetlight prefabs should be present along the city.");
            Pose(game, level == 1 ? 125f : 85f, 0);
            Call(game, "ApplyWorld", World("Day"));
            Capture(level == 1 ? "restored-city-day" : "desert-oasis-day");
            Call(game, "ApplyWorld", World("Echo"));
            Capture(level == 1 ? "restored-city-night" : "desert-oasis-night");
            if (level == 2)
            {
                Pose(game, 1830f, 0); Call(game,"ApplyWorld",World("Day")); Capture("desert-late-bend");
            }
            var hud = Find("WorldShiftLevelHud");
            Call(game, "Fail", "CRASH"); Call(hud,"Refresh");
            Assert.That(((GameObject)Field(hud,"failurePanel")).activeSelf, Is.True);
            Assert.That(((TMPro.TMP_Text)Field(hud,"failureTitle")).text, Is.EqualTo("BUSTED"));
            Capture(level == 1 ? "city-busted" : "desert-busted");
            Call(game,"StepSimulation",1.7f,0f,false); Call(hud,"Refresh");
            Assert.That(Get(game,"State").ToString(), Is.EqualTo("Running"));
            Assert.That(((GameObject)Field(hud,"failurePanel")).activeSelf, Is.False);
            Call(game,"Fail","NO ROAD"); Call(hud,"Refresh");
            Assert.That(((TMPro.TMP_Text)Field(hud,"failureTitle")).text, Is.EqualTo("OFF THE ROAD"));
            Call(game,"RetryCheckpoint"); Call(hud,"Refresh");
            Assert.That(((GameObject)Field(hud,"failurePanel")).activeSelf, Is.False);
        }
        yield return new ExitPlayMode();
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
}
