using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator EveryLevelHasThemedColliderFreeJumpDropsInBothWorlds()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        yield return CheckThemedJumpDrops();
    }

    private IEnumerator CheckThemedJumpDrops()
    {
        string[] themes={"Depot safety pit","City storm drain","Open sea and shark fins","Blue ice crevasse","Mountain misty gorge","Sandstone ravine"};
        for(int level=0;level<6;level++)
        {
            StartLevel(level);yield return WaitForNavigation();
            var game=Started();var drops=Find("WorldShiftLevelDropScenery");
            var visuals=Find("WorldShiftRelayVisuals");var route=Get(game,"Route");
            var ramps=(Array)Get(game,"RelayLayout");
            var dropRoots=visuals.GetComponentsInChildren<Transform>(true).Where(t=>t.name==themes[level]).ToArray();
            Assert.That(dropRoots.Length,Is.EqualTo(ramps.Length));
            Assert.That(dropRoots.SelectMany(t=>t.GetComponentsInChildren<Collider>(true)),Is.Empty,"Dangers are scenery, never new collision surfaces.");
            Assert.That(visuals.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("lava")||t.name.Contains("molten")),Is.False);
            foreach(object jump in ramps)
            {
                float takeoff=(float)Field(jump,"Takeoff"),landing=(float)Field(jump,"Landing");
                Pose(game,takeoff-4f,0f);Call(Find("WorldShiftPresentation"),"UpdateVisibility",takeoff-4f);
                var theme=visuals.GetComponentsInChildren<Transform>(true).Single(t=>t.name==themes[level] && t.parent.name=="Relay "+Field(jump,"Number")+" - launch and landing");
                foreach(string mode in new[]{"Day","Echo"})
                {
                    Call(game,"ApplyWorld",World(mode));
                    Assert.That(theme.gameObject.activeInHierarchy,Is.True,"The danger stays visible when worlds switch.");
                    Assert.That(Call(route,"HasSurface",(takeoff+landing)*.5f,World(mode)),Is.False,"The jump must stay open.");
                }
            }
            object first=ramps.GetValue(0);
            float start=(float)Field(first,"Takeoff"),end=(float)Field(first,"Landing");
            Pose(game,start-4f,0f);Call(Find("WorldShiftPresentation"),"UpdateVisibility",start-4f);
            Call(game,"ApplyWorld",World("Day"));
            Capture("drop-"+level+"-driving-day");CaptureDropScenery(route,start,end,"drop-"+level+"-day");
            Call(game,"ApplyWorld",World("Echo"));CaptureDropScenery(route,start,end,"drop-"+level+"-night");
            if(level==OceanLevel)
            {
                var fins=visuals.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Circling shark fin").ToArray();
                Assert.That(fins.Length,Is.EqualTo(ramps.Length*2));
                var fin=fins.First(t=>t.gameObject.activeInHierarchy);Vector3 before=fin.position;
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(Vector3.Distance(before,fin.position),Is.GreaterThan(.01f));
                Call(game,"TogglePause");before=fin.position;yield return new WaitForSecondsRealtime(.1f);
                Assert.That(fin.position,Is.EqualTo(before),"Sharks freeze with Pause.");Call(game,"TogglePause");
            }
            Call(game,"Fail","NO ROAD");
            float retry=(float)Field(game,"retryRemaining");
            yield return new WaitForSecondsRealtime(.82f);
            Assert.That((bool)Field(drops,"burstStarted"),Is.True,"Each environment gets a brief themed fall effect.");
            Assert.That((float)Field(game,"retryRemaining"),Is.EqualTo(retry),"Scenery never changes retry timing.");
            Step(game,1.1f);
            Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
        }
    }

    private static void CaptureDropScenery(object route,float takeoff,float landing,string name)
    {
        var p=Call(route,"Sample",Mathf.Lerp(takeoff,landing,.30f));
        Vector3 at=(Vector3)Get(p,"Position"),forward=(Vector3)Get(p,"Forward"),right=(Vector3)Get(p,"Right");
        var camera=Camera.main;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.transform.SetPositionAndRotation(at-forward*16f+right*8f+Vector3.up*19f,
                Quaternion.LookRotation(forward*20f-right*8f+Vector3.down*24f));
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            string folder="/tmp/outlaws-themed-drops";Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previous;RenderTexture.active=active;camera.transform.SetPositionAndRotation(position,rotation);
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
