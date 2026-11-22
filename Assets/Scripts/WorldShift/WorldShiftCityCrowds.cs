using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private readonly List<Vector2> cityCrowdPockets = new List<Vector2>();

        private void BuildScatteredCityCrowds()
        {
            int index = 0;
            var random=new System.Random(1526);
            var placed=new List<Vector3>();
            var rampSpectators=new List<Vector3>();
            var rampRandom=new System.Random(2526);
            foreach(var ramp in route.Course.Ramps)
                foreach(float side in new[]{-1f,1f})
                    for(float at=ramp.Ramp+2f+(float)rampRandom.NextDouble()*3f;at<ramp.Takeoff-2f;
                        at+=4f+(float)rampRandom.NextDouble()*5f)
                        rampSpectators.Add(new Vector3(at,side,1.3f+(float)rampRandom.NextDouble()*1.2f));
            // Independent gaps and lateral positions on each pavement replace the old
            // three-row grid. Keep the layout stable across retries and world switches.
            for (float block=20f; block<route.Course.Length-35f; block+=18f)
            {
                var root=Root("Scattered City footpath crowd",block+9f,0f);
                var parts=new List<CombineInstance>();
                void Person(float at,float side,float offset)
                {
                    if(!FootpathClear(at,side,offset,.5f)) return;
                    float lateral=side*(WorldShiftLevel.RoadWidth*.5f+offset);
                    foreach(var citizen in citizens)
                        if(Mathf.Abs(citizen.Lateral-lateral)<.85f
                            && Mathf.Abs(citizen.At-at)<(citizen.Activity==StreetActivity.Walk ? 3.5f : 1.05f)) return;
                    foreach(var other in placed)
                        if(other.y==side && Mathf.Abs(other.x-at)<1.6f && Mathf.Abs(other.z-offset)<.85f) return;
                    placed.Add(new Vector3(at,side,offset));
                    int n=index++;
                    var p=route.Sample(at);
                    Vector3 position=p.Position+p.Right*lateral+p.Up*.035f;
                    float yaw=(float)random.NextDouble()*360f;
                    Quaternion rotation=Quaternion.LookRotation(p.Forward,p.Up)*Quaternion.Euler(0,yaw,0);
                    var figure=root.worldToLocalMatrix*Matrix4x4.TRS(position,rotation,Vector3.one*(.93f+n%5*.025f));
                    var style=citizenStyles[random.Next(citizenStyles.Count)];
                    void Part(Mesh mesh,Vector3 local,Quaternion pose)
                        => parts.Add(new CombineInstance { mesh=mesh,transform=figure*Matrix4x4.TRS(local,pose,Vector3.one) });
                    Part(style[0],Vector3.zero,Quaternion.identity);
                    Part(style[1],new Vector3(-.32f,1.37f,0),Quaternion.Euler(-12f-n%3*17f,0,-9f));
                    Part(style[1],new Vector3(.32f,1.37f,0),Quaternion.Euler(n%2==0 ? -48f : 8f,0,9f));
                    Part(style[2],new Vector3(-.13f,.76f,0),Quaternion.Euler(-6f,0,0));
                    Part(style[2],new Vector3(.13f,.76f,0),Quaternion.Euler(6f,0,0));
                    cityCrowdPockets.Add(new Vector2(at,side));
                }
                foreach(float side in new[]{-1f,1f})
                {
                    for(float at=block+(float)random.NextDouble()*4f;at<block+18f;
                        at+=3.5f+(float)random.NextDouble()*5.5f)
                        Person(at,side,1.1f+(float)random.NextDouble()*1.6f);
                    // Place spectators explicitly along every ramp's sloped footpaths.
                    // Sample the road height rather than leaving them on the flat approach.
                    foreach(var spectator in rampSpectators)
                        if(spectator.y==side && spectator.x>=block && spectator.x<block+18f)
                            Person(spectator.x,side,spectator.z);
                }
                if(parts.Count==0) { chunks.RemoveAt(chunks.Count-1); Destroy(root.gameObject); continue; }
                var mesh=new Mesh { name="Scattered pavement crowd",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(parts.ToArray(),true,true); meshes.Add(mesh);
                StreetPart(root,"Batched crowd",mesh,Vector3.zero);
            }
        }

        public float CityCrowdPresence(float at, float side)
        {
            float nearest=45f;
            foreach(var pocket in cityCrowdPockets)
                if(pocket.y==side) nearest=Mathf.Min(nearest,Mathf.Abs(pocket.x-at));
            return Mathf.SmoothStep(0f,1f,1f-nearest/45f);
        }
    }
}
