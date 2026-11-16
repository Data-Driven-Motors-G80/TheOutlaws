using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelEscape
    {
        private Material previousSky, finaleSky;
        private CameraClearFlags previousClearFlags;
        private float previousFarClip, previousFogStart, previousFogEnd;
        private Color previousFogColor;
        private bool finaleAtmosphereSet;

        private void BuildFinalOverlook()
        {
            // Separate from the course: the saved win cannot be affected by the final camera shot.
            var art = new WorldShiftArtMesh();
            var slate = new Color(.22f,.32f,.35f);
            art.Rock(new Vector3(0,-90,87),new Vector3(31,99,46),slate,41);
            art.Box(new Vector3(0,-1.8f,85),new Vector3(26,3.4f,48),slate);
            art.Box(new Vector3(0,-92,500),new Vector3(2400,4,2400),new Color(.19f,.34f,.37f));
            art.Box(new Vector3(0,-.15f,66),new Vector3(11,.3f,60),new Color(.20f,.26f,.28f));
            art.Box(new Vector3(0,-.12f,94),new Vector3(24,.3f,15),new Color(.29f,.34f,.32f));
            for(int z=40;z<88;z+=6)
                art.Box(new Vector3(0,.03f,z),new Vector3(.13f,.02f,2.4f),new Color(.91f,.78f,.50f));
            foreach(float side in new[]{-1f,1f})
            {
                for(int z=44;z<96;z+=5)
                    art.Box(new Vector3(side*6,.55f,z),new Vector3(.18f,1.1f,.18f),new Color(.43f,.47f,.44f));
                art.Box(new Vector3(side*6,.95f,69),new Vector3(.18f,.18f,54),new Color(.66f,.64f,.52f));
            }
            // Layered peaks give the pullback a large, readable silhouette without gameplay geometry.
            for(int row=0;row<3;row++) for(int i=-4;i<=4;i++)
            {
                float z=285+row*140+i%2*22;
                var tint=Color.Lerp(new Color(.16f,.30f,.35f),new Color(.48f,.58f,.56f),row*.30f);
                float x=i*(105+row*15),floor=-90-row*15;
                var peak=new Vector3(x+14,18+(i*i%4)*13-row*9,z);
                var a=new Vector3(x-90,floor,z-78);var b=new Vector3(x+90,floor,z-78);
                var c=new Vector3(x+85,floor,z+80);var d=new Vector3(x-85,floor,z+80);
                art.Triangle(a,peak,b,tint,true);art.Triangle(b,peak,c,tint*.84f,true);
                art.Triangle(c,peak,d,tint*.74f,true);art.Triangle(d,peak,a,tint*.90f,true);
            }
            Draw("Final mountain overlook",art,Finish("Dawn mountain stone",Color.white,.10f));
            var sun=new WorldShiftArtMesh();sun.Cylinder(Vector3.zero,24,24,1,new Color(1f,.78f,.43f),48);
            var sunrise=Draw("Final sunrise",sun,Finish("Final sunrise glow",Color.white,1));
            sunrise.localPosition=new Vector3(-55,38,410);sunrise.localRotation=Quaternion.Euler(-90,0,0);
            previousSky=RenderSettings.skybox;
            finaleAtmosphereSet=true;
            previousClearFlags=view.clearFlags;previousFarClip=view.farClipPlane;
            previousFogColor=RenderSettings.fogColor;previousFogStart=RenderSettings.fogStartDistance;previousFogEnd=RenderSettings.fogEndDistance;
            view.clearFlags=CameraClearFlags.Skybox;view.farClipPlane=Mathf.Max(1000,previousFarClip);
            RenderSettings.fogStartDistance=120;RenderSettings.fogEndDistance=750;
            var shader=Resources.Load<Shader>("WorldShiftEnvironmentSky");
            if(shader!=null)
            {
                finaleSky=new Material(shader){name="Final dawn sky"};materials.Add(finaleSky);
                RenderSettings.skybox=finaleSky;
            }
        }

        private void ApplyFinalOverlook()
        {
            float dawn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.2f,11f,elapsed));
            RenderSettings.fogColor=Color.Lerp(previousFogColor,new Color(.84f,.66f,.43f),dawn);
            if(finaleSky!=null)
            {
                finaleSky.SetFloat("_Night",1-dawn);
                finaleSky.SetColor("_Horizon",Color.Lerp(new Color(.07f,.14f,.22f),new Color(1f,.69f,.36f),dawn));
                finaleSky.SetColor("_Zenith",Color.Lerp(new Color(.02f,.05f,.12f),new Color(.20f,.42f,.51f),dawn));
            }
            if(elapsed<4.5f) return;
            float drive=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.5f,8.5f,elapsed));
            coupe.gameObject.SetActive(true);
            coupe.localPosition=new Vector3(0,.02f,Mathf.Lerp(39,92,drive));
            float pullback=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.5f,SequenceDuration,elapsed));
            Vector3 camera=Vector3.Lerp(new Vector3(24,22,56),new Vector3(31,18,66),pullback);
            Vector3 target=new Vector3(-4,2,Mathf.Lerp(48,112,drive));
            view.transform.SetPositionAndRotation(stage.TransformPoint(camera),Quaternion.LookRotation(target-camera,Vector3.up));
            view.fieldOfView=Mathf.Lerp(55,63,pullback);
            float fade=1-Mathf.InverseLerp(4.5f,6.5f,elapsed);
            Lens(redLens,0);Lens(blueLens,0);
            Shader.SetGlobalVector("_PoliceBeaconRed",Vector4.zero);
            Shader.SetGlobalVector("_PoliceBeaconBlue",Vector4.zero);
            engine.volume=WorldShiftAudioSettings.Effects*.06f*fade;
        }

        private void RestoreFinaleSky()
        {
            if(!finaleAtmosphereSet) return;
            if(finaleSky!=null && RenderSettings.skybox==finaleSky) RenderSettings.skybox=previousSky;
            RenderSettings.fogColor=previousFogColor;RenderSettings.fogStartDistance=previousFogStart;RenderSettings.fogEndDistance=previousFogEnd;
            if(view!=null) {view.clearFlags=previousClearFlags;view.farClipPlane=previousFarClip;}
            finaleAtmosphereSet=false;
        }
    }
}
