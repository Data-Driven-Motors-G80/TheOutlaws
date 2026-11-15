using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    // Level-only visual pursuit. Never changes player failure or checkpoint state.
    public sealed class WorldShiftPolicePursuit
    {
        public struct Obstacle
        {
            public object Key;
            public float Start, End, Left, Right;
            public Obstacle(object key, float start, float end, float left, float right)
            { Key=key; Start=start; End=end; Left=left; Right=right; }
        }
        private struct Sample { public float Time, X; }
        private readonly Queue<Sample> history = new Queue<Sample>();
        private readonly HashSet<object> struck = new HashSet<object>();
        private float clock, delayedX, slowRemaining, steeringTarget;
        private object avoiding;
        public float Distance { get; private set; }
        public float Lateral { get; private set; }
        public float Jolt { get; private set; }
        public int Hits { get; private set; }
        public const float NormalSeparation = 9.3f; // six metres bumper gap + car length
        public void Reset(float playerDistance, float lateral)
        {
            Distance=playerDistance-NormalSeparation; Lateral=delayedX=steeringTarget=lateral;
            clock=slowRemaining=Jolt=0f; Hits=0; avoiding=null; history.Clear(); struck.Clear();
        }
        public void Step(float delta, float playerDistance, float playerX, float speed, float limit,
            List<Obstacle> obstacles, Func<float, bool> airborne)
        {
            if(delta<=0f) return;
            clock+=delta; history.Enqueue(new Sample {Time=clock,X=playerX});
            while(history.Count>0 && history.Peek().Time<=clock-.3f) delayedX=history.Dequeue().X;
            float remaining=delta;
            while(remaining>0f)
            {
                float dt=Mathf.Min(remaining,1f/60f); remaining-=dt;
                Jolt=Mathf.Max(0f,Jolt-dt);
                bool flying=airborne(Distance);
                float look=Mathf.Max(8f,speed);
                bool Clear(float x)
                {
                    if(flying) return true;
                    foreach(var o in obstacles)
                        if(o.End+1.5f>=Distance && o.Start-1.5f<=Distance+look
                            && x>=o.Left-.95f && x<=o.Right+.95f) return false;
                    return true;
                }
                float desired=Mathf.Clamp(delayedX,-limit,limit);
                bool keep=false;
                if(avoiding!=null)
                    foreach(var o in obstacles)
                        if(o.Key==avoiding && o.End+3f>Distance) {keep=Clear(steeringTarget);break;}
                if(!keep)
                {
                    avoiding=null; steeringTarget=desired;
                    if(!Clear(desired))
                    {
                        float best=float.PositiveInfinity;
                        for(float x=-limit;x<=limit+.001f;x+=.25f)
                            if(Clear(x))
                            {
                                float cost=Mathf.Abs(x-Lateral)+.25f*Mathf.Abs(x-desired);
                                if(cost<best){best=cost;steeringTarget=x;}
                            }
                        foreach(var o in obstacles)
                            if(o.End+1.5f>=Distance && o.Start<=Distance+look
                                && desired>=o.Left-.95f && desired<=o.Right+.95f){avoiding=o.Key;break;}
                    }
                }
                float xNext=Mathf.MoveTowards(Lateral,steeringTarget,4f*dt);
                float target=playerDistance-NormalSeparation;
                float velocity=Mathf.Max(0f,speed)*(slowRemaining>0f?.5f:Distance<target-.2f?1.15f:1f);
                slowRemaining=Mathf.Max(0f,slowRemaining-dt);
                // Brake for an unavoidable wall; do not pretend it is passable.
                if(!flying && !Clear(steeringTarget))
                    foreach(var o in obstacles)
                        if(o.End+1.5f>Distance && Lateral>=o.Left-.8f && Lateral<=o.Right+.8f)
                            velocity=Mathf.Min(velocity,Mathf.Sqrt(40f*Mathf.Max(0f,o.Start-1.6f-Distance)));
                float next=Mathf.Min(Distance+velocity*dt,target);
                next=Mathf.Max(Distance,next);
                if(!flying)
                    foreach(var o in obstacles)
                    {
                        if(next+1.5f<o.Start || Distance-1.5f>o.End
                            || Mathf.Max(Lateral,xNext)<o.Left-.8f || Mathf.Min(Lateral,xNext)>o.Right+.8f) continue;
                        // Contact or a world switch enclosing the cruiser: put it immediately before the surface.
                        if(struck.Add(o.Key)){slowRemaining=1f;Jolt=.22f;Hits++;}
                        next=Mathf.Min(next,o.Start-1.55f);xNext=Lateral;
                    }
                Distance=next;Lateral=Mathf.Clamp(xNext,-limit,limit);
            }
        }
    }
}
