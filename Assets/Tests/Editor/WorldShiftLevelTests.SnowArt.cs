using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator SnowWinterArtKeepsFootprintsWorldsAndTrafficMotion()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        yield return CheckSnowWinterArt();
    }

    private IEnumerator CheckSnowWinterArt()
    {
        StartLevel(SnowLevel);yield return WaitForNavigation();var game=Started();
        var presentation=Find("WorldShiftPresentation");
        var all=game.GetComponentsInChildren<Transform>(true);
        Assert.That(all.Any(t=>t.name=="Packed snowdrift"),Is.True);
        foreach(string shape in new[]{"Upright pines","World-coloured block","World-coloured roadblock"})
            Assert.That(all.Any(t=>t.name.EndsWith(shape)),Is.True,shape);
        foreach(var bank in game.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c.GetType().Name=="WorldShiftSnowObstacleArt"))
        {
            Assert.That(bank.GetComponentsInChildren<Collider>(true),Is.Empty);
            foreach(var v in bank.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Assert.That(Mathf.Abs(v.x),Is.LessThanOrEqualTo(1.526f));
                Assert.That(v.y,Is.InRange(-.001f,1.801f));Assert.That(Mathf.Abs(v.z),Is.LessThanOrEqualTo(3.001f));
            }
        }
        int ploughs=0;bool capturedPine=false,capturedBlock=false,capturedGate=false,capturedDrift=false;
        foreach(object view in (IEnumerable)Field(presentation,"hazards"))
        {
            var data=Field(view,"Data");var solid=(GameObject)Field(view,"Solid");var ghost=(GameObject)Field(view,"Ghost");
            var root=(Transform)Field(view,"Root");float start=(float)Field(data,"Start");
            bool traffic=Field(data,"Kind").ToString()=="Oncoming";
            var barrier=solid.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c.GetType().Name=="WorldShiftRetractableBarrier");
            Pose(game,start-(traffic?90f:24f),0f);Call(game,"PresentWorldForCamera",Field(data,"World"));
            if((bool)Field(data,"SharedAcrossWorlds") && solid.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Packed snowdrift"))
            {
                foreach(string mode in new[]{"Day","Echo"})
                {
                    Call(game,"PresentWorldForCamera",World(mode));
                    Assert.That(solid.activeSelf,Is.True,"Packed snow is a physical obstruction in both worlds.");
                    if(!capturedDrift) Capture("snow-packed-drift-"+mode.ToLowerInvariant());
                }
                capturedDrift=true;
            }
            if(barrier!=null)
            {
                Call(barrier,"Step",1f);
                float width=(int)Field(data,"LaneMask")==7?10f:Field(data,"Kind").ToString()=="HalfBarrier"?5f:3.05f;
                foreach(var filter in barrier.GetComponentsInChildren<MeshFilter>(true))
                    foreach(Vector3 v in filter.sharedMesh.vertices)
                    {
                        Assert.That(Mathf.Abs(v.x),Is.LessThanOrEqualTo(width*.5f+.02f));
                        Assert.That(v.y,Is.InRange(-.02f,barrier.name.EndsWith("Upright pines")?5.80f:1.82f));
                        Assert.That(Mathf.Abs(v.z),Is.LessThanOrEqualTo(3.02f));
                    }
                if(barrier.name.EndsWith("Upright pines"))
                    Assert.That(((Mesh)Field(barrier,"bodyMesh")).bounds.max.y,Is.GreaterThanOrEqualTo(5.3f),"Trees must stand visibly upright above the car.");
                if(barrier.name.EndsWith("World-coloured block") || barrier.name.EndsWith("World-coloured roadblock"))
                    Assert.That(((Mesh)Field(barrier,"bodyMesh")).colors.Any(c=>c.r>.85f&&c.g>.85f&&c.b>.85f),Is.False,
                        "Day/Night boxes must keep their world colour without white snow caps.");
                string label=null;
                if(!capturedPine&&barrier.name.EndsWith("Upright pines")){capturedPine=true;label="pine";}
                if(!capturedBlock&&barrier.name.EndsWith("World-coloured block")){capturedBlock=true;label="plain-block";}
                if(!capturedGate&&barrier.name.EndsWith("World-coloured roadblock")){capturedGate=true;label="gate";}
                if(label!=null) Capture("snow-winter-"+label);
                Call(game,"PresentWorldForCamera",Field(data,"World").ToString()=="Day"?World("Echo"):World("Day"));
                Call(barrier,"Step",1f);
                Assert.That(((Transform)Field(barrier,"panels")).gameObject.activeSelf,Is.False);
                Assert.That(ghost.activeSelf,Is.True);
                Assert.That(barrier.transform.Find("Flush base").gameObject.activeInHierarchy,Is.True,
                    "The lowered obstacle retains its painted dashed footprint.");
            }
            if(!traffic) continue;
            var car=solid.GetComponentsInChildren<MonoBehaviour>(true).Single(c=>c.GetType().Name=="WorldShiftRoadVehicleArt");
            Assert.That(car.name,Is.EqualTo("Oncoming snowplough"));
            Assert.That(Vector3.Dot(car.transform.forward,root.forward),Is.LessThan(-.99f));
            Assert.That(car.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(car.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Traffic wheel and six-spoke rim"),Is.EqualTo(4));
            foreach(var filter in car.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name!="Oncoming road direction warnings"))
                foreach(Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p=car.transform.InverseTransformPoint(filter.transform.TransformPoint(v));
                    Assert.That(Mathf.Abs(p.x),Is.LessThanOrEqualTo(1.526f));
                    Assert.That(Mathf.Abs(p.y),Is.LessThanOrEqualTo(.901f));Assert.That(Mathf.Abs(p.z),Is.LessThanOrEqualTo(2.751f));
                }
            Vector3 before=root.position;Pose(game,start-70f,0f);Call(game,"PresentWorldForCamera",Field(data,"World"));
            Assert.That(Vector3.Distance(before,root.position),Is.GreaterThan(1f));
            if(ploughs++==0)
            {
                Pose(game,start-57f,0f);Call(game,"PresentWorldForCamera",Field(data,"World"));
                Capture("snow-winter-snowplough");
                var beacon=(Material)Field(car,"snowBeacon");
                Call(game,"TogglePause");Color paused=beacon.GetColor("_BaseColor");
                yield return new WaitForSecondsRealtime(.15f);Assert.That(beacon.GetColor("_BaseColor"),Is.EqualTo(paused));
                Call(game,"TogglePause");yield return new WaitForSecondsRealtime(.12f);
                Assert.That(beacon.GetColor("_BaseColor"),Is.Not.EqualTo(paused));
            }
            Call(game,"PresentWorldForCamera",Field(data,"World").ToString()=="Day"?World("Echo"):World("Day"));
            Assert.That(solid.activeSelf,Is.False);Assert.That(ghost.activeSelf,Is.True);
        }
        Assert.That(ploughs,Is.GreaterThan(0));
        Assert.That(capturedPine&&capturedBlock&&capturedGate&&capturedDrift,Is.True);
    }
}
