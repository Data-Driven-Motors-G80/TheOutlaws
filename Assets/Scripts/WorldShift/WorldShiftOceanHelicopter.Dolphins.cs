using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanHelicopter
    {
        private sealed class AirDolphin
        {
            public ShiftRelay Flight;
            public float At, Phase, Clock, Side;

            public Transform Body, Tail, Frost;
            public Vector3 Position, Previous;
        }
        private readonly List<AirDolphin> dolphins=new List<AirDolphin>();
        private WorldShiftRelay dolphinFlightSampler;
        private Material dolphinFinish, dolphinFrost;
        // Transverse, broad-body swimmers. At centre they span the road; at the bank they
        // leave a genuine lane-wide opening. Collision follows the visible tapered body.
        private static readonly Vector3 DolphinScale=new Vector3(2.6f,2.8f,4.5f);
        private static readonly Vector3 DolphinRadii=new Vector3(5.35f,1.15f,1.3f);
        private const float DolphinTravel=9.5f, DolphinSpeed=9f;
        public int DolphinCount => dolphins.Count;

        private void BuildDolphins()
        {
            dolphinFlightSampler=new WorldShiftRelay(game.Route,game.Course.Ramps);
            dolphinFinish=Mat("Coastal dolphin skin",Color.white,.4f);
            dolphinFrost=Mat("Frozen dolphin outline",new Color(.3f,.85f,1),.85f);
            for(int section=0;section<game.Course.Ramps.Length;section++)
            {
                var flight=game.Course.Ramps[section];int count=section==0 ? 1 : 2;
                for(int i=0;i<count;i++)
                {
                    var d=new AirDolphin {Flight=flight,At=flight.Takeoff+(section==0 ? 62f : i==0 ? 38f : 98f),
                        Phase=i*.7f,Side=i==0?1:-1};
                    d.Body=new GameObject("Giant crossing dolphin").transform;d.Body.SetParent(transform,false);d.Body.localScale=DolphinScale;
                    var body=new WorldShiftArtMesh();
                    Color skin=new Color(1f,.61f,.19f),belly=new Color(.98f,.93f,.77f),fin=new Color(.10f,.25f,.31f);
                    // Tapered flat-shaded rings form the arched back, snout and narrow tail root.
                    float[] z={-1.1f,-.65f,.05f,.65f,1.05f,1.35f};
                    float[] r={.12f,.38f,.51f,.38f,.17f,.05f};
                    for(int ring=0;ring<z.Length-1;ring++) for(int side=0;side<10;side++)
                    {
                        Vector3 P(int k,int j) {float a=j*Mathf.PI*.2f;return new Vector3(Mathf.Cos(a)*r[k],Mathf.Sin(a)*r[k]*.85f,z[k]);}
                        body.Quad(P(ring,side),P(ring,side+1),P(ring+1,side+1),P(ring+1,side),side>4?belly:skin);
                    }
                    body.Triangle(new Vector3(0,.3f,-.4f),new Vector3(0,1.05f,-.4f),new Vector3(0,.34f,.35f),fin,true);
                    foreach(float side in new[]{-1f,1f})
                    {
                        body.Triangle(new Vector3(side*.35f,-.08f,.3f),new Vector3(side*.95f,-.3f,-.25f),new Vector3(side*.3f,-.15f,-.3f),fin,true);
                        body.Box(new Vector3(side*.26f,.12f,.7f),new Vector3(.08f,.09f,.09f),new Color(.02f,.04f,.05f));
                    }
                    MeshObject("Faceted dolphin body and fins",body,d.Body,dolphinFinish);
                    var tail=new WorldShiftArtMesh();
                    foreach(float side in new[]{-1f,1f})
                        tail.Triangle(Vector3.zero,new Vector3(side*.75f,.05f,-.35f),new Vector3(side*.38f,-.03f,.14f),fin,true);
                    d.Tail=MeshObject("Dolphin tail flukes",tail,d.Body,dolphinFinish);d.Tail.localPosition=Vector3.back*1.1f;
                    var frost=new WorldShiftArtMesh();frost.RopeRing(Vector3.zero,1.15f,.045f,Color.white);
                    d.Frost=MeshObject("Frozen dolphin ring",frost,d.Body,dolphinFrost);
                    dolphins.Add(d);d.Position=DolphinPosition(d);d.Previous=d.Position;
                }
            }
        }
        private Vector3 DolphinPosition(AirDolphin d)
        {
            var flight=dolphinFlightSampler.SampleFlight(d.Flight,d.At);
            float lateral=d.Side*(Mathf.PingPong(d.Clock*DolphinSpeed+DolphinTravel,DolphinTravel*2)-DolphinTravel);
            return flight.Position+flight.Right*lateral+Vector3.up*.6f;
        }
        private bool StepDolphins(float dt,float from,float to,float fromX,float toX)
        {
            bool hit=false;
            foreach(var d in dolphins)
            {
                d.Previous=d.Position;
                if(from<d.Flight.Landing+5 && game.World==ShiftWorld.Day) d.Clock+=dt;
                d.Position=DolphinPosition(d);
                if(!game.IsRelayAirborne || to<d.At-4f || from>d.At+4f) continue;
                var road=dolphinFlightSampler.SampleFlight(d.Flight,d.At);
                float previousX=Vector3.Dot(d.Previous-road.Position,road.Right),currentX=Vector3.Dot(d.Position-road.Position,road.Right);
                float carFromY=dolphinFlightSampler.SampleFlight(d.Flight,from).Position.y+.08f;
                Vector3 a=new Vector3(fromX-previousX,carFromY+.55f-d.Previous.y,from-d.At);
                Vector3 b=new Vector3(toX-currentX,game.Player.position.y+.55f-d.Position.y,to-d.At);
                float Squared(float t)
                {
                    Vector3 delta=Vector3.Lerp(a,b,t);
                    delta=new Vector3(Mathf.Max(0,Mathf.Abs(delta.x)-.8f),Mathf.Max(0,Mathf.Abs(delta.y)-.5f),Mathf.Max(0,Mathf.Abs(delta.z)-1.5f));
                    // An ellipsoid follows the tapered swimmer instead of a giant invisible sphere.
                    return Vector3.Scale(delta,new Vector3(1/DolphinRadii.x,1/DolphinRadii.y,1/DolphinRadii.z)).sqrMagnitude;
                }
                float lo=0,hi=1;
                for(int i=0;i<12;i++)
                {
                    float l=Mathf.Lerp(lo,hi,1f/3f),r=Mathf.Lerp(lo,hi,2f/3f);
                    if(Squared(l)<Squared(r)) hi=r;else lo=l;
                }
                hit |= Mathf.Min(Squared(0),Squared(1),Squared((lo+hi)*.5f))<1f;
            }
            return hit;
        }
        private void PresentDolphins(bool visible)
        {
            foreach(var d in dolphins)
            {
                bool show=visible&&d.At>game.Distance-18f&&d.At<game.Distance+220f;
                d.Body.gameObject.SetActive(show);if(!show) continue;
                var road=dolphinFlightSampler.SampleFlight(d.Flight,d.At);
                // Keep the full silhouette broadside: readable fins, snout and an unambiguous gap.
                // Reverse at the far bank; the opposite side retains its readable opening.
                float direction=Mathf.Repeat(d.Clock*DolphinSpeed+DolphinTravel,DolphinTravel*4)<DolphinTravel*2?d.Side:-d.Side;
                d.Body.SetPositionAndRotation(d.Position,Quaternion.LookRotation(road.Right*direction,Vector3.up));
                d.Tail.localRotation=Quaternion.Euler(Mathf.Sin(d.Clock*6+d.Phase)*23,0,0);
                d.Frost.gameObject.SetActive(game.World==ShiftWorld.Echo);
            }
        }
        private void ResetDolphins(float distance)
        {
            foreach(var d in dolphins)
            {d.Clock=0;d.Position=DolphinPosition(d);d.Previous=d.Position;}
        }
    }
}
