using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator SnowSkyArtAlwaysFliesOverOncePausesAndCleansUp()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        yield return CheckSnowSkyArt();
    }

    private IEnumerator CheckSnowSkyArt()
    {
        StartLevel(SnowLevel);yield return WaitForNavigation();var game=Started();var skyArt=Find("WorldShiftSnowSky");
        skyArt.enabled=false;
        foreach(string shader in new[]{"WorldShiftEnvironmentSky","WorldShiftSkyArt"})
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Shader>(shader)),Is.False,shader);
        Assert.That(RenderSettings.skybox.GetFloat("_Snow"),Is.EqualTo(1f));
        Assert.That(skyArt.GetComponentsInChildren<Collider>(true),Is.Empty);
        Assert.That(skyArt.GetComponentsInChildren<AudioSource>(true),Is.Empty);
        Call(skyArt,"Advance",1f);Assert.That(Get(skyArt,"FlyoverActive"),Is.False);
        var ice=(Vector2[])Field(Get(game,"Course"),"IceZones");
        Pose(game,ice[0].x+5f,0);Call(game,"ApplySurfaceGrip",ice[0].x+5f);
        Call(skyArt,"Advance",1f);Assert.That(Get(skyArt,"FlyoverActive"),Is.False,"Do not start on black ice.");
        var ramp=((System.Array)Get(game,"RelayLayout")).GetValue(0);float takeoff=(float)Field(ramp,"Takeoff");
        Pose(game,takeoff-8f,0);Call(game,"ApplySurfaceGrip",takeoff-8f);
        Call(skyArt,"Advance",1f);Assert.That(Get(skyArt,"FlyoverActive"),Is.False,"Do not start at a jump.");
        float safe=-1;
        for(float d=0;d<=20f;d+=1f)
        {
            Pose(game,d,0);Call(game,"ApplySurfaceGrip",d);Call(skyArt,"Advance",.04f);
            if((bool)Get(skyArt,"FlyoverActive")){safe=d;break;}
        }
        Assert.That(safe,Is.InRange(4f,20f),"Santa must appear on the opening stretch without waiting for a later checkpoint.");
        Debug.Log("Snow sky guaranteed opening flyover trigger: "+safe+" m");
        Call(game,"UpdateCamera",true);
        float duration=(float)skyArt.GetType().GetField("FlyoverSeconds",Flags).GetValue(null);
        Call(skyArt,"Advance",duration*.5f);
        var santa=(Transform)Field(skyArt,"santa");
        Assert.That(santa.gameObject.activeSelf,Is.True);
        Assert.That(santa.position.y-Camera.main.transform.position.y,Is.GreaterThan(25f));
        Vector3 screen=Camera.main.WorldToViewportPoint(santa.position);
        Assert.That(screen.x,Is.InRange(.25f,.75f));Assert.That(screen.y,Is.InRange(.60f,.93f),"Fly below the HUD and above the road.");
        Bounds bounds=santa.GetComponent<Renderer>().bounds;
        Vector2 vMin=Vector2.one*float.PositiveInfinity,vMax=Vector2.one*float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
        {
            Vector3 p=Camera.main.WorldToViewportPoint(new Vector3(x==0?bounds.min.x:bounds.max.x,
                y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z));
            vMin=Vector2.Min(vMin,new Vector2(p.x,p.y));vMax=Vector2.Max(vMax,new Vector2(p.x,p.y));
        }
        Assert.That(vMax.x-vMin.x,Is.GreaterThan(.18f),"The sleigh and team must be noticeable at gameplay resolution.");
        Assert.That(vMax.y-vMin.y,Is.GreaterThan(.025f));
        float before=(float)Field(skyArt,"skyTime");Vector3 position=santa.position;
        Call(game,"TogglePause");Call(skyArt,"Advance",10f);
        Assert.That((float)Field(skyArt,"skyTime"),Is.EqualTo(before));Assert.That(santa.position,Is.EqualTo(position));
        Call(game,"TogglePause");
        Call(game,"ApplyWorld",World("Day"));Call(skyArt,"Advance",0f);Capture("snow-sky-day-santa");
        Call(game,"ApplyWorld",World("Echo"));Call(skyArt,"Advance",0f);Capture("snow-sky-night-aurora-santa");
        // Also inspect the normal approach view, with no Easter egg in frame.
        Call(skyArt,"Advance",duration);Assert.That(Get(skyArt,"FlyoverActive"),Is.False);
        Capture("snow-sky-night-aurora");
        Call(game,"Fail","NO ROAD");Step(game,1.1f);
        Pose(game,safe,0);Call(game,"ApplySurfaceGrip",safe);Call(skyArt,"Advance",1f);
        Assert.That(Get(skyArt,"FlyoverActive"),Is.False,"Checkpoint retries cannot repeat Santa.");
        Static(Levels,"Menu");yield return WaitForNavigation();
        Assert.That(GameObject.Find("Santa sky Easter egg"),Is.Null);
        Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(c=>c.GetType().Name=="WorldShiftSnowSky"),Is.False);
        StartLevel(1);yield return WaitForNavigation();
        Assert.That(RenderSettings.skybox.GetFloat("_Snow"),Is.Zero,"Snow sky effects cannot leak into City.");
    }
}
