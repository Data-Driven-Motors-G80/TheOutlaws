using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;

public sealed class WorldShiftPolicePursuitTests
{
    private static Type Controller => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a=>a.GetType("Outlaws.WorldShift.WorldShiftPolicePursuit")).First(t=>t!=null);
    private object ai;
    private IList obstacles;
    private float player;
    private void Setup()
    {
        ai=Activator.CreateInstance(Controller);
        obstacles=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(Controller.GetNestedType("Obstacle")));
        player=20f; Controller.GetMethod("Reset").Invoke(ai,new object[]{player,0f});
    }
    private float Value(string key)=>Convert.ToSingle(Controller.GetProperty(key).GetValue(ai));
    private void Step(float dt,float x=0f,bool flying=false)
    {
        player+=20f*dt;
        Controller.GetMethod("Step").Invoke(ai,new object[]{dt,player,x,20f,4f,obstacles,new Func<float,bool>(_=>flying)});
    }
    private void Add(object key,float start,float end,float left,float right)
        =>obstacles.Add(Activator.CreateInstance(Controller.GetNestedType("Obstacle"),key,start,end,left,right));
    [Test] public void SteeringWaitsBeforeFollowingAndStopsWhilePaused()
    {
        Setup();for(int i=0;i<14;i++)Step(.02f,3f);
        Assert.That(Value("Lateral")==0f);
        for(int i=0;i<15;i++)Step(.02f,3f);
        Assert.That(Value("Lateral")>0f && Value("Lateral")<3f);
        float before=Value("Distance");Step(0f,-3f);Assert.That(Value("Distance")==before);
    }
    [Test] public void AvoidsKnownBlockWithoutHittingIt()
    {
        Setup();Add(new object(),30f,34f,-.8f,.8f);
        for(int i=0;i<120;i++)Step(1f/60f);
        Assert.That(Value("Hits")==0f);Assert.That(Value("Distance")>35f);
    }
    [Test] public void SuddenObstacleTriggersOneHitThenRecovery()
    {
        Setup();float at=Value("Distance");Add(new object(),at+1f,at+4f,-1.6f,1.6f);
        Step(.02f);Assert.That(Value("Hits")==1f);
        for(int i=0;i<12;i++)Step(.02f);
        Assert.That(Value("Hits")==1f);
        obstacles.Clear();float gap=player-Value("Distance");
        for(int i=0;i<500;i++)Step(.02f);
        Assert.That(player-Value("Distance")<gap);
        Assert.That(Math.Abs(player-Value("Distance")-9.3f)<.3f);
    }
    [Test] public void FullWidthBlockStopsPoliceUntilRemoved()
    {
        Setup();Add(new object(),35f,38f,-5f,5f);
        for(int i=0;i<180;i++)Step(1f/60f);
        Assert.That(Value("Distance")<=33.5f);
        obstacles.Clear();for(int i=0;i<120;i++)Step(1f/60f);
        Assert.That(Value("Distance")>38f);
    }
    [Test] public void FlightIgnoresGroundBlocksAndResetClearsState()
    {
        Setup();Add(new object(),15f,40f,-5f,5f);
        for(int i=0;i<120;i++)Step(1f/60f,0f,true);
        Assert.That(Value("Hits")==0f && Value("Distance")>40f);
        Controller.GetMethod("Reset").Invoke(ai,new object[]{100f,-2f});
        Assert.That(Math.Abs(Value("Distance")-90.7f)<.001f && Value("Lateral")==-2f && Value("Hits")==0f);
    }
    [Test] public void SteeringAndRecoveryAreStableAcrossFrameRates()
    {
        float[] results=new float[3];int j=0;
        foreach(int fps in new[]{30,60,120})
        {
            Setup();for(int i=0;i<fps*2;i++)Step(1f/fps,3f);
            results[j++]=Value("Lateral");
            Assert.That(Math.Abs(player-Value("Distance")-9.3f)<.1f);
        }
        Assert.That(results.Max()-results.Min()<.05f);
    }
}
