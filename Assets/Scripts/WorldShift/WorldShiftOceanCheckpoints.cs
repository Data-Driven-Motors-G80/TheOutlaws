using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanPresentation
    {
        private sealed class CoastalCheckpoint
        {
            public Transform Root, Sweep, Saved;
            public float At, PulseAt=-10f;
            public int Index;
        }
        private readonly List<CoastalCheckpoint> checkpointGates=new List<CoastalCheckpoint>();
        private AudioClip checkpointChime;
        private int lastCheckpointSeen;

        public void BuildCheckpointGantry(float distance)
        {
            var point=route.Sample(distance);
            var root=new GameObject("Coastal checkpoint gantry").transform;root.SetParent(transform,false);
            root.SetPositionAndRotation(point.Position,Quaternion.LookRotation(point.Forward,point.Up));
            var structure=new WorldShiftArtMesh();var reflectors=new WorldShiftArtMesh();
            var panel=new WorldShiftArtMesh();var stripe=new WorldShiftArtMesh();
            Color steel=new Color(.70f,.76f,.76f),green=WorldShiftLevelProps.CheckpointColour;
            foreach(float side in new[] {-1f,1f})
            {
                float x=side*6.3f;
                structure.ChamferBox(new Vector3(x,.25f,0),new Vector3(.86f,.5f,.9f),.08f,Stone);
                structure.Cylinder(new Vector3(x,.5f,0),.13f,.11f,6.1f,steel,12);
                structure.Beam(new Vector3(x,5.65f,0),new Vector3(x-side*.8f,6.50f,0),.095f,.095f,steel);
                structure.Box(new Vector3(x,6.48f,0),new Vector3(.34f,.40f,.40f),steel);
                foreach(float y in new[] {1.3f,1.7f,2.1f})
                    reflectors.Box(new Vector3(x,y,-.14f),new Vector3(.12f,.18f,.025f),green);
                foreach(float bolt in new[] {-.23f,.23f})
                    structure.Cylinder(new Vector3(x+bolt,.51f,bolt),.045f,.045f,.045f,steel,8);
            }
            structure.ChamferBox(new Vector3(0,6.50f,0),new Vector3(12.8f,.25f,.36f),.055f,steel);
            panel.ChamferBox(new Vector3(0,6.50f,-.20f),new Vector3(4.9f,.90f,.14f),.05f,new Color(.035f,.09f,.105f));
            foreach(float y in new[] {6.10f,6.90f})
                reflectors.Box(new Vector3(0,y,-.285f),new Vector3(4.78f,.045f,.025f),green);
            foreach(float x in new[] {-2.38f,2.38f})
                reflectors.Box(new Vector3(x,6.5f,-.285f),new Vector3(.045f,.82f,.025f),green);
            reflectors.Box(new Vector3(0,5.99f,-.10f),new Vector3(11.6f,.055f,.055f),green*.60f);
            stripe.Box(new Vector3(0,.045f,0),new Vector3(WorldShiftLevel.RoadWidth,.05f,.42f),green);
            Render("Coastal checkpoint structure",structure,metalFinish,root);
            Render("Checkpoint sign backing",panel,scenery,root);
            Render("Reflective green checkpoint accents",reflectors,luminous,root);
            Render("Actual checkpoint road stripe",stripe,luminous,root);
            var label=new GameObject("CHECKPOINT sign",typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(root,false);label.transform.localPosition=new Vector3(.20f,6.5f,-.30f);
            label.rectTransform.sizeDelta=new Vector2(4.1f,.72f);
            label.font=TMP_Settings.defaultFontAsset;label.text="CHECKPOINT";label.fontSize=5f;
            label.fontStyle=FontStyles.Bold;label.alignment=TextAlignmentOptions.Center;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.color=new Color(.93f,1f,.96f);
            label.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            var sweep=new WorldShiftArtMesh();
            sweep.ChamferBox(Vector3.zero,new Vector3(.95f,.085f,.055f),.018f,new Color(.8f,1f,.85f));
            var sweepRoot=Render("Checkpoint confirmation light sweep",sweep,luminous,root).transform;
            sweepRoot.localPosition=new Vector3(-5.3f,5.99f,-.15f);sweepRoot.gameObject.SetActive(false);
            var saved=new WorldShiftArtMesh();
            saved.Beam(new Vector3(-2.17f,6.49f,-.31f),new Vector3(-2.04f,6.35f,-.31f),.07f,.045f,green);
            saved.Beam(new Vector3(-2.04f,6.35f,-.31f),new Vector3(-1.78f,6.67f,-.31f),.07f,.045f,green);
            var savedRoot=Render("Saved checkpoint tick",saved,luminous,root).transform;
            savedRoot.gameObject.SetActive(false);
            int index=0;
            for(int i=1;i<game.Checkpoints.Length;i++)
                if(Mathf.Abs(game.Checkpoints[i].Distance-distance)<.1f) index=i;
            checkpointGates.Add(new CoastalCheckpoint {Root=root,At=distance,Index=index,Sweep=sweepRoot,Saved=savedRoot});
            chunks.Add(new CoastChunk {Root=root.gameObject,At=distance,End=distance});
            if(checkpointChime==null) BuildCheckpointChime();
        }

        private void RefreshCheckpointGantries(float distance)
        {
            int reached=game.CheckpointIndex;
            if(reached<lastCheckpointSeen)
                foreach(var gate in checkpointGates) gate.PulseAt=-10f;
            if(reached>lastCheckpointSeen && game.State==ShiftRunState.Running && game.CheckpointNoticeRemaining>0)
                foreach(var gate in checkpointGates)
                    if(gate.Index==reached && distance>=gate.At)
                    {
                        gate.PulseAt=animationTime;
                        if(checkpointChime!=null) audioSource.PlayOneShot(checkpointChime,.70f);
                    }
            lastCheckpointSeen=reached;
            foreach(var gate in checkpointGates)
            {
                gate.Saved.gameObject.SetActive(gate.Index>0 && reached>=gate.Index);
                if(game.CheckpointNoticeRemaining<=0) gate.PulseAt=-10f;
                float t=(animationTime-gate.PulseAt)/.75f;
                gate.Sweep.gameObject.SetActive(t>=0 && t<1f);
                gate.Sweep.localPosition=new Vector3(Mathf.Lerp(-5.3f,5.3f,Mathf.SmoothStep(0,1,t)),5.99f,-.15f);
            }
        }

        private void BuildCheckpointChime()
        {
            const int rate=22050;
            var samples=new float[(int)(rate*.45f)];
            float[] notes={523.25f,659.25f,783.99f};
            for(int i=0;i<samples.Length;i++)
                for(int note=0;note<notes.Length;note++)
                {
                    float t=(float)i/rate-note*.055f;
                    if(t<0) continue;
                    samples[i]+=Mathf.Sin(t*2*Mathf.PI*notes[note])*Mathf.Min(1,t*100f)*Mathf.Exp(-t*13f)*.18f;
                }
            checkpointChime=AudioClip.Create("Coastal checkpoint confirmation",samples.Length,1,rate,false);
            checkpointChime.SetData(samples,0);
        }
    }
}
