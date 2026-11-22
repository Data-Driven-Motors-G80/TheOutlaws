using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private sealed class CityTrafficSignal
        {
            public Transform Root;
            public Renderer[] Lamps;
            public float Phase;
            public int Aspect=-1;
        }
        private readonly List<CityTrafficSignal> cityTrafficSignals=new List<CityTrafficSignal>();
        private Material citySignalMaterial;
        private MaterialPropertyBlock citySignalProperties;

        private void BuildCityTrafficLights()
        {
            citySignalMaterial=MakeMaterial("City traffic signal lenses",1f);
            citySignalProperties=new MaterialPropertyBlock();
            // Paired signals face approaching drivers, behind the pavement edge.
            // They dress ordinary city stretches without interfering with jump sight lines.
            int index=0;
            for(float at=65f;at<route.Course.Length-60f;at+=95f)
            {
                if(NearJump(at,30f) || !Clear(at,-9.3f,1.4f) || !Clear(at,9.3f,1.4f)) continue;
                float phase=index++*2.7f;
                foreach(float side in new[]{-1f,1f})
                {
                    var root=Root("City roadside traffic light",at,side*9.3f);
                    var metal=new WorldShiftArtMesh(); var housing=new WorldShiftArtMesh();
                    Color steel=new Color(.26f,.29f,.29f), black=new Color(.065f,.075f,.08f);
                    metal.Cylinder(Vector3.zero,.19f,.13f,6.8f,steel,8);
                    metal.Box(new Vector3(0,.15f,0),new Vector3(.48f,.30f,.48f),steel);
                    // The arm stays over the footpath, not over a driving lane.
                    metal.Beam(new Vector3(0,6.65f,0),new Vector3(-side*1.1f,6.65f,0),.13f,.13f,steel);
                    float x=-side*.85f;
                    housing.ChamferBox(new Vector3(x,5.65f,0),new Vector3(.94f,2.55f,.28f),.10f,new Color(.32f,.30f,.18f));
                    housing.Box(new Vector3(x,5.65f,-.16f),new Vector3(.76f,2.36f,.08f),black);
                    var lamps=new Renderer[3];
                    for(int lamp=0;lamp<3;lamp++)
                    {
                        float y=6.35f-lamp*.70f;
                        housing.Box(new Vector3(x,y+.28f,-.26f),new Vector3(.67f,.065f,.43f),black);
                        var disc=new WorldShiftArtMesh();
                        Color colour=lamp==0 ? new Color(.98f,.055f,.035f)
                            : lamp==1 ? new Color(1f,.59f,.035f) : new Color(.07f,.95f,.30f);
                        Vector3 centre=new Vector3(x,y,-.215f);
                        for(int edge=0;edge<20;edge++)
                        {
                            float a=edge*Mathf.PI/10f,b=(edge+1)*Mathf.PI/10f;
                            disc.Triangle(centre,centre+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*.245f,
                                centre+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.245f,colour);
                        }
                        string label=lamp==0 ? "Red signal lens" : lamp==1 ? "Amber signal lens" : "Green signal lens";
                        Render(root,label,disc,citySignalMaterial);
                        lamps[lamp]=root.GetChild(root.childCount-1).GetComponent<Renderer>();
                    }
                    Render(root,"Traffic signal pole and arm",metal,surface);
                    Render(root,"Traffic signal housing and sun visors",housing,surface);
                    cityTrafficSignals.Add(new CityTrafficSignal { Root=root,Lamps=lamps,Phase=phase });
                }
            }
            RefreshCityTrafficLights();
        }

        private void RefreshCityTrafficLights()
        {
            foreach(var signal in cityTrafficSignals)
            {
                if(!signal.Root.gameObject.activeSelf) continue;
                // Green -> amber -> red, coordinated across the two sides of each pair.
                // The scenery clock follows the existing pause, story and retry gates.
                float cycle=Mathf.Repeat(streetClock+signal.Phase,17f);
                int aspect=cycle<8f ? 2 : cycle<10f ? 1 : 0;
                if(signal.Aspect==aspect) continue;
                signal.Aspect=aspect;
                for(int lamp=0;lamp<signal.Lamps.Length;lamp++)
                {
                    bool on=lamp==aspect;
                    citySignalProperties.Clear();
                    citySignalProperties.SetColor("_BaseColor",on ? Color.white : new Color(.075f,.075f,.075f));
                    citySignalProperties.SetFloat("_Emission",on ? 1f : .025f);
                    signal.Lamps[lamp].SetPropertyBlock(citySignalProperties);
                }
            }
        }
    }
}
