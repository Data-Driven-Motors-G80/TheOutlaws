using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private sealed class CityDog
        {
            public Citizen Owner;
            public Transform Root, Tail;
            public Transform[] Legs;
        }
        private sealed class CityCrow
        {
            public Transform Flock, Root, LeftWing, RightWing;
            public float Phase, Radius, Height;
        }
        private readonly List<CityDog> cityDogs = new List<CityDog>();
        private readonly List<CityCrow> cityCrows = new List<CityCrow>();
        private readonly List<Mesh[]> dogStyles = new List<Mesh[]>();
        private Mesh crowBody, crowWing;

        private void BuildCityAnimalMeshes()
        {
            Color[] coats = { new Color(.48f,.30f,.16f),new Color(.22f,.23f,.24f),new Color(.77f,.66f,.49f) };
            var dark = new Color(.08f,.075f,.07f);
            for (int i=0; i<coats.Length; i++)
            {
                var body=new WorldShiftArtMesh(); var leg=new WorldShiftArtMesh(); var tail=new WorldShiftArtMesh();
                Color coat=coats[i];
                body.ChamferBox(new Vector3(0,.48f,0),new Vector3(.32f,.35f,.72f),.08f,coat);
                body.ChamferBox(new Vector3(0,.69f,.36f),new Vector3(.30f,.33f,.32f),.055f,coat);
                body.Box(new Vector3(0,.62f,.59f),new Vector3(.21f,.15f,.22f),coat*.8f);
                body.Box(new Vector3(0,.65f,.71f),new Vector3(.13f,.085f,.045f),dark);
                foreach(float side in new[]{-1f,1f})
                {
                    body.Triangle(new Vector3(side*.04f,.83f,.31f),new Vector3(side*.14f,1.02f,.28f),
                        new Vector3(side*.17f,.79f,.41f),coat*.75f,true);
                    body.Box(new Vector3(side*.153f,.74f,.43f),new Vector3(.025f,.045f,.05f),dark);
                }
                body.Box(new Vector3(0,.51f,.34f),new Vector3(.34f,.07f,.10f),new Color(.36f,.44f,.50f));
                leg.Box(new Vector3(0,-.16f,0),new Vector3(.105f,.32f,.12f),coat);
                leg.Box(new Vector3(0,-.315f,.045f),new Vector3(.13f,.09f,.21f),coat*.65f);
                tail.Beam(Vector3.zero,new Vector3(0,.24f,-.24f),.09f,.09f,coat);
                tail.Beam(new Vector3(0,.24f,-.24f),new Vector3(0,.36f,-.28f),.075f,.075f,coat);
                var style=new[]{body.Mesh("City dog coat and ears"),leg.Mesh("City dog paw"),tail.Mesh("City dog tail")};
                dogStyles.Add(style); meshes.AddRange(style);
            }
            var crow=new WorldShiftArtMesh(); var wing=new WorldShiftArtMesh();
            Color feathers=new Color(.055f,.065f,.09f), tips=new Color(.10f,.12f,.15f);
            crow.ChamferBox(Vector3.zero,new Vector3(.19f,.20f,.47f),.07f,feathers);
            crow.ChamferBox(new Vector3(0,.075f,.25f),new Vector3(.18f,.19f,.20f),.055f,feathers);
            crow.Triangle(new Vector3(-.065f,.07f,.33f),new Vector3(0,.04f,.52f),new Vector3(.065f,.07f,.33f),dark,true);
            crow.Quad(new Vector3(-.08f,0,-.15f),new Vector3(-.15f,-.025f,-.48f),
                new Vector3(.15f,-.025f,-.48f),new Vector3(.08f,0,-.15f),feathers);
            // Swept wings with separated primary feathers give a crow silhouette in flight.
            wing.Triangle(Vector3.zero,new Vector3(.34f,.025f,.11f),new Vector3(.78f,0,-.18f),feathers,true);
            wing.Triangle(Vector3.zero,new Vector3(.78f,0,-.18f),new Vector3(.20f,0,-.25f),feathers,true);
            for(int j=0;j<5;j++)
            {
                float x=.38f+j*.09f;
                wing.Triangle(new Vector3(x,0,-.12f),new Vector3(x+.15f,0,-.42f+j*.035f),
                    new Vector3(x+.095f,0,-.10f),tips,true);
            }
            crowBody=crow.Mesh("City crow body and tail"); crowWing=wing.Mesh("City crow feathered wing");
            meshes.Add(crowBody); meshes.Add(crowWing);
        }

        private void AddCityDog(Citizen owner, int index)
        {
            var root=new GameObject("City dog").transform; root.SetParent(owner.Root,false);
            var style=dogStyles[(index/5)%dogStyles.Count];
            StreetPart(root,"Coat and ears",style[0],Vector3.zero);
            var legs=new Transform[4];
            for(int j=0;j<4;j++)
                legs[j]=StreetPart(root,"Animated paw",style[1],new Vector3(j%2==0 ? -.13f : .13f,.35f,j<2 ? .25f : -.25f));
            var tail=StreetPart(root,"Wagging tail",style[2],new Vector3(0,.54f,-.35f));
            cityDogs.Add(new CityDog { Owner=owner,Root=root,Legs=legs,Tail=tail });
        }

        private void BuildCityCrows()
        {
            int flock=0;
            for(float at=60f;at<route.Course.Length;at+=100f)
            {
                float side=flock++%2==0 ? -1f : 1f;
                var root=Root("City crow flock",at,side*7f);
                // Above the chase and jumps, below the skyline; no physics or gameplay hitboxes.
                root.position+=Vector3.up*(17f+flock%3*2f);
                for(int j=0;j<6;j++)
                {
                    var bird=new GameObject("Flying crow").transform; bird.SetParent(root,false);
                    StreetPart(bird,"Body and beak",crowBody,Vector3.zero);
                    var right=StreetPart(bird,"Right feathered wing",crowWing,new Vector3(.07f,0,0));
                    var left=StreetPart(bird,"Left feathered wing",crowWing,new Vector3(-.07f,0,0));
                    left.localScale=new Vector3(-1,1,1);
                    cityCrows.Add(new CityCrow { Flock=root,Root=bird,LeftWing=left,RightWing=right,
                        Phase=j*Mathf.PI/3f+flock*.83f,Radius=2.4f+j*.24f,Height=(j%3)*.7f });
                }
            }
        }

        private void StepCityAnimals()
        {
            foreach(var dog in cityDogs)
            {
                if(!dog.Root.gameObject.activeInHierarchy) continue;
                var owner=dog.Owner;
                // Follow the owner's earlier position, including a delayed turn at either end.
                float followClock=streetClock*.95f+owner.Phase-1.05f;
                float travel=Mathf.PingPong(followClock,5f);
                float direction=Mathf.Repeat(followClock,10f)<5f ? 1f : -1f;
                var p=route.Sample(owner.At+travel-2.5f);
                dog.Root.SetPositionAndRotation(p.Position+p.Right*(owner.Lateral+owner.Side*.30f)+p.Up*.045f,
                    Quaternion.LookRotation(p.Forward*direction,p.Up));
                float gait=streetClock*7.6f+owner.Phase;
                for(int j=0;j<dog.Legs.Length;j++)
                    dog.Legs[j].localRotation=Quaternion.Euler(Mathf.Sin(gait+(j==0||j==3 ? 0 : Mathf.PI))*24f,0,0);
                dog.Tail.localRotation=Quaternion.Euler(0,Mathf.Sin(streetClock*8f+owner.Phase)*26f,0);
            }
            foreach(var crow in cityCrows)
            {
                if(!crow.Flock.gameObject.activeSelf) continue;
                float angle=streetClock*.42f+crow.Phase;
                crow.Root.localPosition=new Vector3(Mathf.Sin(angle)*crow.Radius,crow.Height+Mathf.Sin(angle*2f)*.28f,Mathf.Cos(angle)*crow.Radius*2f);
                var forward=new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle)*2f);
                crow.Root.localRotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(0,0,-10f);
                // Alternate flapping and short glides, with a different phase for each bird.
                float flap=Mathf.Repeat(streetClock+crow.Phase,3.8f)<2.6f ? Mathf.Sin(streetClock*9f+crow.Phase)*32f : 7f;
                crow.RightWing.localRotation=Quaternion.Euler(0,0,flap);
                crow.LeftWing.localRotation=Quaternion.Euler(0,0,-flap);
            }
        }
    }
}
