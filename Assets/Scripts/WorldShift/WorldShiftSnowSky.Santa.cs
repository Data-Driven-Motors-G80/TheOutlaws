using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftSnowSky
    {
        private sealed class DeerRig
        {
            public Transform Root;
            public readonly Transform[] Legs=new Transform[4],Knees=new Transform[4];
            public float X,Phase;
        }
        private readonly List<DeerRig> team=new List<DeerRig>();
        private readonly List<LineRenderer> reins=new List<LineRenderer>();
        private Transform sleigh,waveArm;

        private Transform DrawFigure(Transform parent,string label,WorldShiftArtMesh art)
        {
            Mesh mesh=art.Mesh(label);figureMeshes.Add(mesh);
            var root=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(parent,false);
            root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=figureFinish;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return root.transform;
        }

        private void BuildSanta()
        {
            santa=new GameObject("Santa sky Easter egg").transform;santa.SetParent(transform,false);santa.localScale=Vector3.one*2.4f;
            figureFinish=new Material(Resources.Load<Shader>("WorldShiftSkyArt")){name="Snow Santa flyover"};
            var art=new WorldShiftArtMesh();
            Color red=new Color(.68f,.06f,.08f),cream=new Color(.96f,.94f,.83f);
            Color gold=new Color(.88f,.62f,.18f),bark=new Color(.25f,.14f,.08f);
            art.ChamferBox(new Vector3(-5,-.15f,0),new Vector3(3.8f,.75f,1.5f),.15f,red);
            art.Box(new Vector3(-6.5f,.55f,0),new Vector3(.30f,1.3f,1.5f),red);
            foreach(float side in new[]{-1f,1f})
            {
                art.Beam(new Vector3(-7,-.8f,side*.70f),new Vector3(-2.6f,-.8f,side*.70f),.10f,.10f,gold);
                art.Beam(new Vector3(-2.6f,-.8f,side*.70f),new Vector3(-2.2f,-.35f,side*.70f),.10f,.10f,gold);
                art.Beam(new Vector3(-6,-.35f,side*.60f),new Vector3(-6,-.78f,side*.70f),.10f,.10f,gold);
            }
            art.ChamferBox(new Vector3(-4.4f,.75f,0),new Vector3(.9f,1.15f,.9f),.16f,red);
            art.Rock(new Vector3(-4.25f,1.25f,0),new Vector3(.36f,.6f,.38f),new Color(.89f,.66f,.49f),5);
            art.Rock(new Vector3(-3.92f,1.05f,0),new Vector3(.26f,.55f,.39f),cream,6);
            art.Cylinder(new Vector3(-4.25f,1.8f,0),.39f,.08f,.72f,red,10);
            art.Rock(new Vector3(-4.05f,2.38f,0),new Vector3(.17f,.23f,.17f),cream,8);
            art.Rock(new Vector3(-5.7f,.25f,0),new Vector3(.65f,1.35f,.55f),new Color(.35f,.27f,.12f),14);
            for(int i=0;i<12;i++)
                art.Rock(new Vector3(-8-i*.85f,Mathf.Sin(i*1.7f)*.4f,Mathf.Cos(i)*.35f),
                    Vector3.one*(.12f+(i%3)*.04f),new Color(.85f,.76f,.48f,1-i/15f),i);
            sleigh=DrawFigure(santa,"Banking sleigh and Santa",art);
            waveArm=new GameObject("Santa waving arm").transform;waveArm.SetParent(sleigh,false);
            waveArm.localPosition=new Vector3(-4.1f,1.1f,-.44f);
            var arm=new WorldShiftArtMesh();arm.Beam(Vector3.zero,new Vector3(.15f,.55f,0),.22f,.22f,red);
            arm.Box(new Vector3(.15f,.55f,0),new Vector3(.25f,.12f,.25f),cream);
            arm.Rock(new Vector3(.16f,.59f,0),new Vector3(.13f,.22f,.13f),cream,8);
            DrawFigure(waveArm,"Santa mitten",arm);
            for(int deer=0;deer<3;deer++)
            {
                var rig=new DeerRig {X=deer*3.8f,Phase=deer*.72f};
                rig.Root=new GameObject("Galloping reindeer "+(deer+1)).transform;rig.Root.SetParent(santa,false);
                team.Add(rig);var body=new WorldShiftArtMesh();
                body.ChamferBox(new Vector3(0,.45f,0),new Vector3(1.65f,.70f,.62f),.13f,bark);
                body.Beam(new Vector3(.55f,.60f,0),new Vector3(.9f,1.35f,0),.36f,.36f,bark);
                body.ChamferBox(new Vector3(1.1f,1.35f,0),new Vector3(.76f,.40f,.44f),.08f,bark);
                body.Box(new Vector3(0,.48f,0),new Vector3(.10f,.75f,.67f),red);
                foreach(float side in new[]{-1f,1f})
                {
                    body.Beam(new Vector3(.88f,1.50f,side*.18f),new Vector3(.55f,2.12f,side*.45f),.07f,.07f,gold);
                    body.Beam(new Vector3(.70f,1.85f,side*.32f),new Vector3(1.05f,2.08f,side*.55f),.06f,.06f,gold);
                }
                DrawFigure(rig.Root,"Reindeer body and antlers",body);
                for(int leg=0;leg<4;leg++)
                {
                    bool front=leg<2;float side=leg%2==0?-1:1;
                    var hip=new GameObject("Reindeer leg pivot").transform;hip.SetParent(rig.Root,false);
                    hip.localPosition=new Vector3(front?.55f:-.60f,.24f,side*.22f);rig.Legs[leg]=hip;
                    var upper=new WorldShiftArtMesh();upper.Beam(Vector3.zero,Vector3.down*.45f,.12f,.12f,bark);
                    DrawFigure(hip,"Reindeer upper leg",upper);
                    var knee=new GameObject("Reindeer knee").transform;knee.SetParent(hip,false);knee.localPosition=Vector3.down*.45f;rig.Knees[leg]=knee;
                    var lower=new WorldShiftArtMesh();lower.Beam(Vector3.zero,Vector3.down*.43f,.10f,.10f,bark);
                    lower.Box(Vector3.down*.43f,new Vector3(.20f,.12f,.14f),new Color(.10f,.08f,.06f));
                    DrawFigure(knee,"Reindeer lower leg and hoof",lower);
                }
            }
            foreach(float side in new[]{-1f,1f})
            {
                var root=new GameObject("Flexible sleigh rein");root.transform.SetParent(santa,false);
                var line=root.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=10;
                line.widthMultiplier=.035f;line.startColor=line.endColor=gold;line.sharedMaterial=figureFinish;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;reins.Add(line);
            }
            AnimateTeam(0);
        }

        private void AnimateTeam(float seconds)
        {
            float gait=seconds*6.8f;
            sleigh.localPosition=new Vector3(0,Mathf.Sin(gait-.8f)*.08f,Mathf.Cos(gait*.45f)*.04f);
            sleigh.localRotation=Quaternion.Euler(0,Mathf.Sin(gait*.35f)*1.5f,Mathf.Sin(gait-.8f)*2f);
            waveArm.localRotation=Quaternion.AngleAxis(18f+Mathf.Sin(seconds*4f)*24f,Vector3.forward);
            foreach(var deer in team)
            {
                float phase=gait-deer.Phase;
                deer.Root.localPosition=new Vector3(deer.X,Mathf.Sin(phase)*.16f,Mathf.Sin(phase*.5f)*.04f);
                deer.Root.localRotation=Quaternion.AngleAxis(Mathf.Sin(phase)*3f,Vector3.forward);
                for(int leg=0;leg<4;leg++)
                {
                    bool front=leg<2;float step=phase+(leg%2)*Mathf.PI+(front?0:Mathf.PI*.7f);
                    deer.Legs[leg].localRotation=Quaternion.AngleAxis((front?25f:-25f)+Mathf.Sin(step)*34f,Vector3.forward);
                    deer.Knees[leg].localRotation=Quaternion.AngleAxis(-15f-38f*(.5f+.5f*Mathf.Sin(step+.8f)),Vector3.forward);
                }
            }
            for(int side=0;side<reins.Count;side++)
            {
                Vector3 from=sleigh.localPosition+sleigh.localRotation*new Vector3(-3.6f,.70f,side==0?-.4f:.4f);
                reins[side].SetPosition(0,from);int vertex=1;
                foreach(var deer in team)
                {
                    Vector3 to=deer.Root.localPosition+deer.Root.localRotation*new Vector3(0,.70f,side==0?-.4f:.4f);
                    for(int i=1;i<=3;i++)
                    {float t=i/3f;reins[side].SetPosition(vertex++,Vector3.Lerp(from,to,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*.16f);}
                    from=to;
                }
            }
        }
    }
}
