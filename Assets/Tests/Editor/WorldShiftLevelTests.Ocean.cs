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
    public IEnumerator OceanFlyInHoldsPlayCanSkipAndDoesNotRepeatOnRetry()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        analytics.Clear();StartLevel(OceanLevel);yield return WaitForNavigation();
        var game=Find("WorldShiftCity");game.enabled=false;var hud=Find("WorldShiftLevelHud");hud.enabled=false;
        var boss=Get(game,"OceanBoss");
        Assert.That(Get(game,"StoryActive"),Is.True);Assert.That(Get(game,"State").ToString(),Is.EqualTo("Ready"));
        Step(game,2f);Assert.That((float)Get(game,"Distance"),Is.Zero);Assert.That((float)Get(game,"Duration"),Is.Zero);
        Assert.That(analytics.Count(a=>a.name=="wl_level_start"),Is.Zero);
        var audio=Find("WorldShiftLevelAudio");Call(audio,"Refresh",1f);Assert.That(Get(audio,"Suspended"),Is.True);
        Call(boss,"AdvanceIntro",1.8f);Call(boss,"Present");Call(game,"UpdateCamera",true);
        Capture("ocean-intro-air-support");WorldShiftTestCapture.Capture("ocean-intro-4x3",960,720);
        Call(boss,"AdvanceIntro",10f);Call(hud,"UpdateOceanIntro");
        Assert.That(Get(game,"StoryActive"),Is.False);Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
        Assert.That((float)Get(game,"Distance"),Is.Zero);Assert.That((float)Get(game,"Duration"),Is.Zero);
        Assert.That(analytics.Count(a=>a.name=="wl_level_start"),Is.EqualTo(1));
        Assert.That(((GameObject)Field(hud,"storyRoot")).activeSelf,Is.False);
        Call(game,"RetryCheckpoint");Assert.That(Get(game,"StoryActive"),Is.False);
        Static(Levels,"Retry");yield return WaitForNavigation();
        game=Find("WorldShiftCity");game.enabled=false;Assert.That(Get(game,"StoryActive"),Is.False);
        StartLevel(OceanLevel);yield return WaitForNavigation();
        game=Find("WorldShiftCity");game.enabled=false;hud=Find("WorldShiftLevelHud");
        Assert.That(Get(game,"StoryActive"),Is.True);ClickButton(VisibleButton("Skip"));
        Assert.That(Get(game,"StoryActive"),Is.False);Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"));
        Assert.That(Get(game,"World").ToString(),Is.EqualTo("Day"));
    }

    [UnityTest]
    public IEnumerator OceanTacticalAimClusterExitAndDistantDolphinsStayFair()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(OceanLevel);yield return WaitForNavigation();var game=Started();var boss=Get(game,"OceanBoss");
        var dolphin=((IEnumerable)Field(boss,"dolphins")).Cast<object>().Last();
        Call(boss,"Restore",0f);Vector3 original=(Vector3)Field(dolphin,"Position");
        Call(boss,"Step",.3f,0f,1f,0f,0f);
        Assert.That(Vector3.Distance(original,(Vector3)Field(dolphin,"Position")),Is.GreaterThan(1f),"Even the final jump's dolphins move before approach.");
        Vector3 frozen=(Vector3)Field(dolphin,"Position");Call(game,"ApplyWorld",World("Echo"));Call(boss,"Step",1f,1f,2f,0f,0f);
        Assert.That(Vector3.Distance(frozen,(Vector3)Field(dolphin,"Position")),Is.LessThan(.001f));
        var slots=((IEnumerable)Get(boss,"AttackSlots")).Cast<float>().ToArray();
        var aim=new HashSet<float>();
        for(int seed=1;seed<=18;seed++)
        {
            boss.GetType().GetProperty("AttackSeed",Flags).SetValue(boss,seed);
            Call(boss,"Restore",0f);Call(game,"ApplyWorld",World("Day"));
            Call(boss,"Step",.05f,slots[1]-91,slots[1]-89,0f,.3f);
            aim.Add((float)Get(boss,"TargetLateral"));
        }
        Assert.That(aim.Count,Is.GreaterThan(1),"Variation includes current-lane and short movement prediction.");
        float locked=(float)Get(boss,"TargetLateral");
        Call(boss,"Step",.2f,slots[1]-89,slots[1]-80,-3f,-3f);
        Assert.That((float)Get(boss,"TargetLateral"),Is.EqualTo(locked),"The visible marker must never chase later steering.");
        var c=Get(game,"Collection");var items=(Vector2[])Field(c,"Items");
        for(int i=0;i<5;i++) Call(c,"Step",items[i].x-1,items[i].x+1,items[i].y,items[i].y,true);
        // At 1 HP the last stand repeats. Banking the first volley does not suppress later ones.
        foreach(int candidate in new[]{5,6})
        {
            Call(boss,"Restore",0f);float target=slots[candidate];
            Pose(game,target-91,-3f);Call(boss,"Present");
            Call(boss,"Step",.1f,target-91,target-89,-3f,-3f);
            Assert.That(Get(boss,"AttackKind").ToString(),Is.EqualTo("ClusterSalvo"));
            Assert.That(Get(boss,"ComboActive"),Is.False);
            float safe=-Mathf.Sign((float)Get(boss,"TargetLateral"))*3.333f;
            Call(boss,"Step",1.3f,target-89,target-60,safe,safe);Pose(game,target-60,safe);Call(boss,"Present");
            var view=((Array)Field(boss,"strikeViews")).GetValue(0);
            Assert.That(((Transform[])Field(view,"Missile")).Count(t=>t.gameObject.activeInHierarchy),Is.EqualTo(18));
            if(candidate==5) Capture("ocean-repeating-last-stand");
            float clock=(float)Get(boss,"AttackTime");Call(game,"ApplyWorld",World("Echo"));
            Call(boss,"Step",1f,target-60,target-50,safe,safe);
            Assert.That(Get(boss,"AttackTime"),Is.EqualTo(clock));
            Call(game,"ApplyWorld",World("Day"));Call(boss,"Step",1.7f,target-50,target-25,safe,safe);
            Assert.That(Call(boss,"Step",.01f,target-8,target+8,safe,safe),Is.False);
            Call(boss,"SaveCheckpoint");Call(boss,"Restore",target+10);
            Assert.That(Get(boss,"FinalSalvoSent"),Is.True);
        }
        // Test both attacks in one continuous boss simulation, rather than resetting between them.
        Call(boss,"Restore",0f);float first=slots[5],second=slots[6];
        Call(boss,"Step",.1f,first-91,first-89,-3f,-3f);
        var old=Field(boss,"active");
        Call(boss,"Step",.1f,second-91,second-89,3f,3f);
        Assert.That(Field(boss,"active"),Is.Not.SameAs(old));
        Assert.That(Get(boss,"AttackKind").ToString(),Is.EqualTo("ClusterSalvo"));
        Call(c,"Step",items[5].x-1,items[5].x+1,items[5].y,items[5].y,true);
        Assert.That(Call(boss,"Step",.01f,second-1,second+1,0f,0f),Is.False,"A kill cancels every pending barrage.");
        Assert.That(Get(boss,"AttackActive"),Is.False);
    }

    [UnityTest]
    public IEnumerator OceanSpreadBombingLeavesRoomForSteeringAndLivesThroughItsWholeCorridor()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(OceanLevel);yield return WaitForNavigation();var game=Started();var boss=Get(game,"OceanBoss");
        var course=Get(game,"Course");var slots=(float[])Field(course,"AirStrikeTargets");
        var ends=(float[])Field(course,"AirStrikeEnds");
        // Avoid capturing method-scope locals across EnterPlayMode's domain reload.
        int longCount=0;for(int i=0;i<slots.Length;i++) if(ends[i]>slots[i]+1f) longCount++;
        Assert.That(longCount,Is.EqualTo(5));
        foreach(float playerLane in new[]{-3f,3f})
        {
            Call(game,"RetryCheckpoint");float target=slots[1];
            Call(boss,"Restore",target-60f);Call(game,"ApplyWorld",World("Day"));
            Pose(game,target-60,playerLane);Call(boss,"Present");
            Call(boss,"Step",.05f,target-91,target-89,playerLane,playerLane);
            var strike=Field(boss,"active");var points=((IEnumerable)Field(strike,"Points")).Cast<object>().ToArray();
            Assert.That(points.Length,Is.EqualTo(7));Assert.That(Get(strike,"Spread"),Is.True);
            Assert.That(points.Select(p=>(float)Field(p,"Lateral")).Min(),Is.LessThan(-2.8f));
            Assert.That(points.Select(p=>(float)Field(p,"Lateral")).Max(),Is.GreaterThan(2.8f));
            for(int i=1;i<points.Length;i++)
                Assert.That((float)Field(points[i],"Distance")-(float)Field(points[i-1],"Distance"),Is.EqualTo(18f).Within(.001f));
            float clock=(float)Get(boss,"AttackTime");Call(game,"ApplyWorld",World("Echo"));
            Call(boss,"Step",1f,target-89,target-70,0f,0f);
            Assert.That(Get(boss,"AttackTime"),Is.EqualTo(clock));
            Call(game,"ApplyWorld",World("Day"));
            // Keyboard-only steering through the live field in Day: no teleporting between points.
            var driver=Field(game,"driver");
            for(int frame=0;frame<180 && (float)Get(game,"Distance")<target+115;frame++)
            {
                float d=(float)Get(game,"Distance"),x=(float)Get(game,"Lateral");
                var next=points.FirstOrDefault(p=>(float)Field(p,"Distance")+3f>=d);
                float goal=0f;
                if(next!=null && Mathf.Abs((float)Field(next,"Lateral"))<1.8f)
                    goal=Array.IndexOf(points,next)%2==0 ? -3f : 3f;
                float command=goal-x-(float)Field(driver,"lateralSpeed")*.18f;
                Step(game,.05f,Mathf.Abs(command)<.18f?0f:Mathf.Sign(command));
                Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"),"Steerable Day route at "+Get(game,"Distance"));
                if(frame==65) {Call(boss,"Present");Capture("ocean-spread-point-bombing");}
            }
            Assert.That((float)Get(game,"Distance"),Is.GreaterThan(target+108));
            Assert.That(points.All(p=>(bool)Field(p,"Landed")),Is.True,"The field must persist until its farthest point has passed.");
        }
        // The dense long last-stand variant has three alternating safe-side waves 54 m apart.
        var c=Get(game,"Collection");var items=(Vector2[])Field(c,"Items");
        for(int i=0;i<5;i++) Call(c,"Step",items[i].x-1,items[i].x+1,items[i].y,items[i].y,true);
        Call(boss,"Restore",0f);float t=slots[1];Call(boss,"Step",.05f,t-91,t-89,-3f,-3f);
        var dense=((IEnumerable)Field(Field(boss,"active"),"Points")).Cast<object>().ToArray();
        Assert.That(dense.Length,Is.EqualTo(18));
        for(int wave=0;wave<3;wave++)
        {
            float safe=(wave%2==0?1f:-1f)*-Mathf.Sign((float)Get(boss,"TargetLateral"))*3.333f;
            foreach(var point in dense.Skip(wave*6).Take(6))
                Assert.That(Mathf.Abs((float)Field(point,"Lateral")-safe),Is.GreaterThan(1.7f));
        }
    }

    [UnityTest]
    public IEnumerator OceanFiveSlowCurtainsGiveTimeToFreezeAndNeverShareANightClosure()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(OceanLevel);yield return WaitForNavigation();var game=Started();var boss=Get(game,"OceanBoss");
        var course=Get(game,"Course");var slots=(float[])Field(course,"AirStrikeTargets");
        var flags=(bool[])Field(course,"AirStrikeCurtains");Assert.That(flags.Count(x=>x),Is.EqualTo(5));
        for(int index=0;index<slots.Length;index++)
        {
            if(!flags[index]) continue;
            float target=slots[index];Call(boss,"Restore",0f);Call(game,"ApplyWorld",World("Day"));
            Call(boss,"Step",.05f,target-91,target-89,0f,0f);
            Assert.That(Get(boss,"AttackKind").ToString(),Is.EqualTo("MissileCurtain"));
            var strike=Field(boss,"active");var points=((IEnumerable)Field(strike,"Points")).Cast<object>().ToArray();
            Assert.That(points.Length,Is.EqualTo(15));
            Assert.That(points.Min(p=>(float)Field(p,"ImpactSeconds")),Is.GreaterThanOrEqualTo(3f));
            Call(boss,"Step",1.3f,target-89,target-56,0f,0f);Pose(game,target-56,0f);Call(boss,"Present");
            if(index==2) Capture("ocean-slow-missile-curtain");
            float clock=(float)Field(strike,"Time");Call(game,"ApplyWorld",World("Echo"));
            Assert.That(Call(boss,"Step",3f,target-56,target+8,0f,0f),Is.False,"Freezing permits passage through the curtain.");
            Assert.That(Field(strike,"Time"),Is.EqualTo(clock),"A newly warned field may become active while the earlier curtain stays frozen.");
            Call(game,"ApplyWorld",World("Day"));Call(boss,"Step",3f,target-89,target-70,0f,0f);
            foreach(float x in new[]{-3.333f,0f,3.333f})
                Assert.That(Call(boss,"StepPoints",strike,0f,10f,target-8,target+8,x,x),Is.True,"Late landed curtain covers every lane.");
        }
    }

    [Test]
    public void OceanAmmoIsOptionalDayOnlyAndRestoresActualCheckpointPickups()
    {
        var course=Static(TypeFor("WorldShiftLevelCourse"),"For",OceanLevel);
        var items=(Vector2[])Field(course,"RequiredItems");Assert.That(items.Length,Is.EqualTo(12));
        var hazards=((IEnumerable)Field(course,"Hazards")).Cast<object>().ToArray();
        Assert.That(hazards.Count(h=>Field(h,"World").Equals(World("Echo")) && (int)Field(h,"LaneMask")==7),Is.GreaterThanOrEqualTo(10),"Every section must break permanent Night camping.");
        Assert.That(hazards.Count(h=>Field(h,"World").Equals(World("Echo")) && Field(h,"Kind").ToString()=="Oncoming"),Is.EqualTo(5));
        var c=Activator.CreateInstance(TypeFor("WorldShiftLevelCollection"),new object[]{items,true});
        Assert.That(Call(c,"Step",90f,110f,0f,0f,false),Is.True);
        Assert.That(Get(c,"Count"),Is.EqualTo(0),"Night cannot collect ammo.");
        Assert.That(Call(c,"Step",90f,110f,items[0].y,items[0].y,true),Is.True);
        Assert.That(Get(c,"Count"),Is.EqualTo(1));Call(c,"SaveCheckpoint");
        var checkpoints=(Array)Field(course,"Checkpoints");float at=(float)Field(checkpoints.GetValue(1),"Distance");
        var second=items[1];Call(c,"Step",second.x-1,second.x+1,second.y,second.y,true);
        Assert.That(Get(c,"Count"),Is.EqualTo(2));Call(c,"Restore",at);
        Assert.That(Get(c,"Count"),Is.EqualTo(1));Assert.That(Call(c,"IsTaken",1),Is.False);
        Assert.That(Call(c,"Step",second.x-1,second.x+10,-second.y,-second.y,true),Is.True,"Missing ammo never fails.");
        Call(c,"Restore",0f);Assert.That(Get(c,"Count"),Is.EqualTo(0));
        Call(c,"SaveCheckpoint");Call(c,"Restore",at);
        Assert.That(Get(c,"Count"),Is.EqualTo(0),"Passing a checkpoint must never grant missed ammo.");
    }

    [UnityTest]
    public IEnumerator OceanRestoresOriginalDayBarriersAndMovingPoliceSpikes()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(OceanLevel);yield return WaitForNavigation();var game=Started();var boss=Get(game,"OceanBoss");
        var hazards=((IEnumerable)Get(game,"Hazards")).Cast<object>().ToArray();
        Assert.That(hazards.Count(h=>Field(h,"World").Equals(World("Day")) && !(bool)Field(h,"SharedAcrossWorlds")
            && Field(h,"Kind").ToString()=="Barrier"),Is.EqualTo(15),"Restore ordinary Day barriers independently of helicopter cargo.");
        var sliders=hazards.Where(h=>(bool)Field(h,"LateralMotion") && Field(h,"Rock")==null && Field(h,"Gate")==null).ToArray();
        Assert.That(sliders.Length,Is.EqualTo(10));
        var models=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.GetType().Name=="WorldShiftSpikePlatform").ToArray();
        Assert.That(models.Length,Is.EqualTo(10),"Use the original shared spike model for every restored slider.");
        foreach(var model in models)
        {
            Assert.That(model.name,Is.EqualTo("Sliding police spikes"));
            Assert.That(model.GetComponent<MeshFilter>().sharedMesh.name,Is.EqualTo("Police spike cones"));
        }
        foreach(var slider in sliders)
        {
            float distance=(float)Field(slider,"Start")-30f;
            Call(slider,"Advance",.6f,distance,World("Day"));
            float x=(float)Get(slider,"LateralOffset");Assert.That(Mathf.Abs(x),Is.GreaterThan(.5f));
            Call(slider,"Advance",1f,distance,World("Echo"));
            Assert.That(Get(slider,"LateralOffset"),Is.EqualTo(x),"Original spikes freeze but remain solid in Night.");
            Assert.That(Field(slider,"SharedAcrossWorlds"),Is.True);
        }
        foreach(float target in ((IEnumerable)Get(boss,"AttackSlots")).Cast<float>())
            Assert.That(Call(boss,"SafePocket",target),Is.True,"Restoring hazards must not suppress the authored helicopter attack at "+target);
        Pose(game,102f,3.333f);Call(game,"PresentWorldForCamera",World("Day"));
        Capture("ocean-restored-original-spikes-day");
        Call(game,"ApplyWorld",World("Echo"));Call(game,"PresentWorldForCamera",World("Echo"));
        Capture("ocean-restored-original-spikes-night");
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

    [Test]
    public void OceanOppositeWorldRowsLeaveSwitchingSpace()
    {
        var course=Static(TypeFor("WorldShiftLevelCourse"),"For",OceanLevel);
        var hazards=((IEnumerable)Field(course,"Hazards")).Cast<object>().Where(h=>!(bool)Field(h,"SharedAcrossWorlds")).ToArray();
        Vector2 Encounter(object h)
        {
            float start=(float)Field(h,"Start"),end=(float)Field(h,"End"),k=(float)Field(h,"MotionSpeed")/28f;
            return new Vector2((start+(start-150)*k)/(1+k),(end+(start-150)*k)/(1+k));
        }
        foreach(var day in hazards.Where(h=>Field(h,"World").Equals(World("Day"))))
            foreach(var night in hazards.Where(h=>Field(h,"World").Equals(World("Echo"))))
            {
                Vector2 a=Encounter(day),b=Encounter(night);
                Assert.That(Mathf.Max(b.x-a.y,a.x-b.y),Is.GreaterThanOrEqualTo(28f),"Day/Night encounter rows need a readable switch interval.");
            }
        var targets=(float[])Field(course,"AirStrikeTargets");var ends=(float[])Field(course,"AirStrikeEnds");
        for(int i=0;i<targets.Length;i++)
            foreach(var night in hazards.Where(h=>Field(h,"World").Equals(World("Echo"))))
            {
                Vector2 b=Encounter(night);
                Assert.That(Mathf.Max(b.x-(ends[i]+5),targets[i]-5-b.y),Is.GreaterThanOrEqualTo(28f),"Missile Day cargo must not border a Night wall.");
            }
    }

    [UnityTest]
    public IEnumerator OceanDolphinsFreezeAndLeaveAnAirborneSteeringRoute()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(OceanLevel);yield return WaitForNavigation();var game=Started();var boss=Get(game,"OceanBoss");
        Assert.That(Get(boss,"DolphinCount"),Is.EqualTo(10));
        var dolphins=((IEnumerable)Field(boss,"dolphins")).Cast<object>().ToArray();
        var flights=((IEnumerable)Field(Get(game,"Course"),"Ramps")).Cast<object>().ToArray();
        for(int index=0;index<flights.Length;index++)
        {
            Call(game,"RetryCheckpoint");Call(boss,"Restore",0f);
            var flight=flights[index];float takeoff=(float)Field(flight,"Takeoff");
            Call(Field(game,"relay"),"Reset",0f);
            Pose(game,takeoff-.5f,-3.333f);Call(game,"ApplyWorld",Field(flight,"Source"));Step(game,.03f);
            Assert.That(Get(game,"IsRelayAirborne"),Is.True);
            // Release a blocked Night launch; move the first swimmer toward the right bank.
            Call(game,"ApplyWorld",World("Day"));Step(game,.65f);
            Call(game,"ApplyWorld",World("Echo"));
            var first=dolphins[index*2];Vector3 frozen=(Vector3)Field(first,"Position");
            if(index==0) {Call(boss,"Present");Capture("ocean-two-giant-dolphins-frozen");}
            Step(game,.4f);
            Assert.That(Vector3.Distance(frozen,(Vector3)Field(first,"Position")),Is.LessThan(.001f));
            // After clearing the first fish, steer across its frozen opening to the second.
            Step(game,.9f,1f);Step(game,.6f);
            Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"),"Two frozen swimmers have a steerable route, jump "+index);
            Call(game,"ApplyWorld",Get(flight,"Destination"));Step(game,.7f);
            Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running").Or.EqualTo("Won"));
            Assert.That(Get(game,"IsRelayAirborne"),Is.False);
            // Do not finish the level: leave final victory navigation to its own tests.
        }
        // Cold/frozen at centre really closes the full road; releasing it opens the side.
        Call(game,"RetryCheckpoint");Call(boss,"Restore",0f);
        var d=dolphins[0];var firstFlight=flights[0];float launch=(float)Field(firstFlight,"Takeoff"),at=(float)Field(d,"At");
        Pose(game,launch-.5f,0f);Call(game,"ApplyWorld",World("Day"));Call(Field(game,"relay"),"Reset",0f);Step(game,.03f);
        d.GetType().GetField("Clock",Flags).SetValue(d,0f);Call(game,"ApplyWorld",World("Echo"));
        foreach(float x in new[]{-3.333f,0f,3.333f})
        {
            Pose(game,at,x);
            Assert.That(Call(boss,"Step",0f,at-.05f,at+.05f,x,x),Is.True,"Centred giant fish spans all lanes.");
        }
        Call(game,"Fail","DOLPHIN COLLISION");Capture("ocean-dolphin-retry-hint");
        var hud=Find("WorldShiftLevelHud");
        Assert.That(((TMPro.TMP_Text)Field(hud,"failureHint")).text,Does.Contain("DAY: MOVE"));
    }

    [UnityTest]
    public IEnumerator OceanHelicopterFreezesWeakensRestoresAndAllowsPacifistFinish()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(OceanLevel);yield return WaitForNavigation();var game=Started();
        var boss=Get(game,"OceanBoss");var c=Get(game,"Collection");
        Assert.That(boss,Is.Not.Null);Assert.That(Get(boss,"Health"),Is.EqualTo(6));
        var slots=((IEnumerable)Get(boss,"AttackSlots")).Cast<float>().ToArray();
        Assert.That(slots.Length,Is.EqualTo(20));
        foreach(float at in slots) Assert.That(Call(boss,"SafePocket",at),Is.True,"Strike pocket "+at);
        Pose(game,70f,0);Call(boss,"Present");Capture("ocean-helicopter-day");
        var traffic=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.GetType().Name=="WorldShiftRoadVehicleArt").ToArray();
        Assert.That(traffic.Length,Is.EqualTo(10));
        foreach(var car in traffic) Assert.That(car.GetComponentsInChildren<Transform>(true).Any(t=>t.name.EndsWith("traffic sun-moon badge")),Is.True);
        Call(game,"ApplyWorld",World("Echo"));Pose(game,76f,3.333f);Capture("ocean-night-traffic-badge");
        Call(game,"ApplyWorld",World("Echo"));Pose(game,82f,3.333f);Step(game,.65f);
        Assert.That(Get(c,"Count"),Is.EqualTo(0));
        Call(game,"RetryCheckpoint");Pose(game,82f,3.333f);Step(game,.65f);
        Assert.That(Get(boss,"Health"),Is.EqualTo(5));
        Call(boss,"Step",.3f,0f,0f,0f,0f);Call(boss,"Present");Capture("ocean-player-homing-rocket");
        Assert.That(((Transform)Field(boss,"rocket")).gameObject.activeInHierarchy,Is.True);
        Call(game,"RetryCheckpoint");Assert.That(Get(boss,"Health"),Is.EqualTo(6));
        float target=slots[0];
        Call(game,"ApplyWorld",World("Echo"));
        Call(boss,"Step",.2f,target-91,target-89,0f,0f);
        Assert.That(Get(boss,"AttackActive"),Is.True,"Night must arm a frozen warning instead of skipping it.");
        Assert.That(Get(boss,"AttackTime"),Is.EqualTo(0f));
        Call(game,"ApplyWorld",World("Day"));Call(boss,"Step",1.3f,target-89,target-60,0f,0f);
        Pose(game,target-60,0);Call(boss,"Present");Capture("ocean-helicopter-missile");
        var views=(Array)Field(boss,"strikeViews");
        var missiles=(Transform[])Field(views.GetValue(0),"Missile");
        Assert.That(missiles[0].gameObject.activeInHierarchy,Is.True);
        Assert.That(missiles[1].gameObject.activeInHierarchy,Is.False);
        Vector3 missilePosition=missiles[0].position;float warning=(float)Get(boss,"AttackTime");
        Call(game,"ApplyWorld",World("Echo"));Call(boss,"Step",.4f,target-60,target-50,0f,0f);Call(boss,"Present");
        Assert.That(Get(boss,"AttackTime"),Is.EqualTo(warning));
        Assert.That(Vector3.Distance(missiles[0].position,missilePosition),Is.LessThan(.001f));
        Call(game,"ApplyWorld",World("Day"));Call(boss,"Step",1.2f,target-50,target-30,0f,0f);
        Pose(game,target-30,3.333f);Call(boss,"Present");Capture("ocean-helicopter-impact");
        float lane=(float)Get(boss,"TargetLateral");
        Assert.That(Call(boss,"Step",.05f,target-8,target+8,lane,lane),Is.True,"Large blast has swept collision.");
        Call(game,"ApplyWorld",World("Echo"));
        Assert.That(Call(boss,"Step",.05f,target-8,target+8,lane,lane),Is.True,"Landed debris stays dangerous in Night.");
        Assert.That(Call(boss,"Step",.05f,target-8,target+8,-3.333f,-3.333f),Is.False);
        Call(game,"Fail","AIR STRIKE");Capture("ocean-missile-retry-hint");
        Assert.That(((TMPro.TMP_Text)Field(Find("WorldShiftLevelHud"),"failureHint")).text,Does.Contain("ROCKETS: HIT HELI"));
        Call(game,"RetryCheckpoint");
        // The fourth encounter combines a two-lane salvo and Day cargo; Night opens the outside exit.
        Call(boss,"Restore",0f);target=slots[13];
        Call(game,"ApplyWorld",World("Day"));Call(boss,"Step",.1f,target-91,target-89,-3f,-3f);
        Assert.That(Get(boss,"AttackKind").ToString(),Is.EqualTo("TwinSalvo"));
        Assert.That(Get(boss,"ComboActive"),Is.True);
        Assert.That((float)Get(boss,"TargetHalfWidth")*2,Is.GreaterThan(6.6f));
        Call(boss,"Step",1.2f,target-89,target-62,-3f,-3f);
        Pose(game,target-62,3.333f);Call(boss,"Present");Capture("ocean-helicopter-twin-salvo");
        Assert.That(missiles.Take(2).All(t=>t.gameObject.activeInHierarchy),Is.True);
        Assert.That(missiles.Skip(2).All(t=>!t.gameObject.activeInHierarchy),Is.True);
        Call(boss,"Step",1f,target-62,target-40,-3f,-3f);
        Assert.That(Call(boss,"Step",.05f,target-8,target+8,0f,0f),Is.True);
        Assert.That(Call(boss,"Step",.05f,target-8,target+8,3.333f,3.333f),Is.True,"Day cargo closes the missile bypass.");
        Call(game,"ApplyWorld",World("Echo"));
        Assert.That(Call(boss,"Step",.05f,target-8,target+8,3.333f,3.333f),Is.False,"Night removes cargo and keeps a real outside exit.");
        Call(boss,"Present");Capture("ocean-helicopter-combo-night");
        Call(game,"ApplyWorld",World("Day"));
        Assert.That((int)Get(boss,"LiveStrikeCount"),Is.LessThanOrEqualTo(3));
        // Repeated pincer rows keep the middle open until the centre missile lands.
        Call(boss,"Restore",0f);target=slots[17];Call(game,"ApplyWorld",World("Day"));
        Call(boss,"Step",.1f,target-91,target-89,3.333f,3.333f);
        Assert.That(Get(boss,"TargetLateral"),Is.EqualTo(0f));
        Pose(game,target-28,3.333f);Call(boss,"Present");Capture("ocean-day-pincer-warnings");
        Call(boss,"Step",2.2f,target-89,target-40,3.333f,3.333f);
        Assert.That(Call(boss,"Step",.01f,target-8,target+8,-3.333f,-3.333f),Is.True);
        Assert.That(Call(boss,"Step",.01f,target-8,target+8,3.333f,3.333f),Is.True);
        Call(game,"ApplyWorld",World("Echo"));
        Assert.That(Call(boss,"Step",.01f,target-8,target+8,3.333f,3.333f),Is.False);
        Call(boss,"Present");
        var barriers=(Array)Field(views.GetValue(0),"DayBarriers");
        var panel=(Transform)Field(barriers.GetValue(0),"panels");
        Assert.That(panel.gameObject.activeSelf,Is.True,"Cargo visibly sinks instead of disappearing on the switch.");
        Call(boss,"StepCargoAnimations",.08f);
        Assert.That(panel.localScale.y,Is.InRange(.01f,.99f));
        Call(boss,"StepCargoAnimations",.3f);Assert.That(panel.gameObject.activeSelf,Is.False);
        Capture("ocean-day-pincer-lowered");Call(game,"ApplyWorld",World("Day"));
        var items=(Vector2[])Field(c,"Items");
        for(int i=0;i<6;i++)
        {
            Call(c,"Step",items[i].x-1,items[i].x+1,items[i].y,items[i].y,true);
            if(i==2)
            {
                Assert.That(Get(boss,"Stage"),Is.EqualTo(1));Call(boss,"Step",.01f,0f,0f,0f,0f);
                Call(boss,"Step",1.40f,0f,0f,0f,0f);Call(boss,"Present");
                Assert.That(((Transform)Field(boss,"hitBurst")).gameObject.activeSelf,Is.True);
                Capture("ocean-helicopter-damaged");
            }
            if(i==2 || i==4)
            {
                int attacks=0;
                for(int candidate=0;candidate<slots.Length;candidate++)
                    if((bool)Call(boss,"AttackAllowed",candidate)) attacks++;
                Assert.That(attacks,Is.EqualTo(20),"Both damaged stages retain the faster ordinary attack cadence.");
            }
        }
        Assert.That(Get(boss,"Defeated"),Is.True);
        Assert.That(Call(boss,"Step",.05f,target-6,target+6,lane,lane),Is.False);
        Call(boss,"Step",1.4f,0f,0f,0f,0f);Call(boss,"Present");
        Assert.That(Get(boss,"DefeatExplosionPlayed"),Is.True);
        Assert.That(((Vector3)Get(Get(game,"Impact"),"ShakeOffset")).sqrMagnitude,Is.GreaterThan(0f));
        Assert.That(((Transform)Field(boss,"defeatRoot")).gameObject.activeSelf,Is.True);
        Call(boss,"Step",.2f,0f,0f,0f,0f);Call(boss,"Present");Capture("ocean-helicopter-destruction");
        Call(boss,"Step",1.65f,0f,0f,0f,0f);Call(boss,"Present");
        Assert.That(Get(boss,"SeaImpactPlayed"),Is.True);
        Assert.That(((Transform)Field(boss,"seaImpactRoot")).gameObject.activeSelf,Is.True);
        Capture("ocean-helicopter-sea-crash");
        Call(game,"RetryCheckpoint");Assert.That(Get(boss,"Defeated"),Is.False);
        Assert.That(((Transform)Field(boss,"defeatRoot")).gameObject.activeSelf,Is.False);
        // A frozen target expires behind the player; it must not teleport forward on unfreeze.
        Call(game,"ApplyWorld",World("Echo"));Call(boss,"Step",.1f,target-91,target-89,0f,0f);
        Call(boss,"Step",.1f,target+12,target+13,0f,0f);
        Assert.That((float)Get(boss,"TargetDistance"),Is.Not.EqualTo(target));
        FinishForButtons(game);Assert.That(Get(game,"State").ToString(),Is.EqualTo("Won"));
        Assert.That(Get(c,"Count"),Is.EqualTo(0),"No ammo is required to complete Ocean.");
    }
}
