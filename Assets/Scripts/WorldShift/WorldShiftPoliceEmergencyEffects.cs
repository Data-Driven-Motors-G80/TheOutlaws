using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Shared level pursuit presentation; never changes police AI or player failure.</summary>
    public sealed class WorldShiftPoliceEmergencyEffects : MonoBehaviour
    {
        private static readonly int BaseColour=Shader.PropertyToID("_BaseColor");
        private static readonly int RedBeacon=Shader.PropertyToID("_PoliceBeaconRed");
        private static readonly int BlueBeacon=Shader.PropertyToID("_PoliceBeaconBlue");
        private static readonly Color Red=new Color(1f,.055f,.075f),Blue=new Color(.055f,.25f,1f);
        private const float Cycle=.90f,Range=9f;
        private WorldShiftCity game;
        private Renderer redLens,blueLens;
        private Light redLight,blueLight;
        private AudioSource siren;
        private MaterialPropertyBlock lensProperties;
        private float clock, sirenGain;
        public bool IsPursuing {get;private set;}
        public bool SirenPaused {get;private set;}

        public void Bind(WorldShiftCity controller)
        {
            game=controller;
            lensProperties=new MaterialPropertyBlock();
            redLens=transform.Find("Red roof light lens").GetComponent<Renderer>();
            blueLens=transform.Find("Blue roof light lens").GetComponent<Renderer>();
            redLight=Beacon("Red police beacon",new Vector3(-.43f,1.43f,-.06f),Red);
            blueLight=Beacon("Blue police beacon",new Vector3(.43f,1.43f,-.06f),Blue);
            siren=gameObject.AddComponent<AudioSource>();
            siren.clip=Resources.Load<AudioClip>("WorldShiftPoliceSiren");
            siren.playOnAwake=false;siren.loop=true;siren.volume=0f;
            siren.spatialBlend=1f;siren.dopplerLevel=0f;
            // Keep stereo positioning, but measure loudness from the car rather than the chase camera.
            siren.rolloffMode=AudioRolloffMode.Custom;
            siren.SetCustomCurve(AudioSourceCurveType.CustomRolloff,AnimationCurve.Linear(0f,1f,1f,1f));
            siren.priority=160;
            var filter=gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency=1600f;filter.lowpassResonanceQ=1f;
            Refresh(0f);
        }

        private Light Beacon(string label,Vector3 position,Color colour)
        {
            var root=new GameObject(label);root.transform.SetParent(transform,false);root.transform.localPosition=position;
            var light=root.AddComponent<Light>();light.type=LightType.Point;light.color=colour;
            light.range=Range;light.intensity=0f;light.shadows=LightShadows.None;
            light.renderMode=LightRenderMode.ForcePixel;light.enabled=false;return light;
        }

        private void Update() => Refresh(Time.deltaTime);

        public void Refresh(float delta)
        {
            if(game==null || siren==null) return;
            bool suspended=WorldShiftAudioSettings.Suspended(game);
            bool active=!suspended && (game.State==ShiftRunState.Running || game.Capturing);
            bool wasPursuing=IsPursuing;IsPursuing=active;
            if(!active)
            {
                if(suspended)
                {
                    if(wasPursuing) {siren.Pause();SirenPaused=true;}
                }
                else {siren.Stop();sirenGain=0f;siren.volume=0f;SirenPaused=false;}
                SetLights(0f,0f,false);
                return;
            }
            if(SirenPaused) siren.UnPause();
            else if(!siren.isPlaying && siren.clip!=null) siren.Play();
            SirenPaused=false;
            delta=Mathf.Max(0f,delta);
            clock=Mathf.Repeat(clock+delta,Cycle);
            float distance=Vector3.Distance(transform.position,game.Player.position);
            float proximity=1f-Mathf.InverseLerp(8f,70f,distance);
            sirenGain=Mathf.MoveTowards(sirenGain,.04f*proximity,delta*.10f);
            siren.volume=sirenGain*WorldShiftAudioSettings.Effects;
            float red=Mathf.SmoothStep(0f,1f,.5f+.5f*Mathf.Cos(clock/Cycle*Mathf.PI*2));
            SetLights(red,1f-red,true);
        }

        private void SetLights(float red,float blue,bool active)
        {
            Tint(redLens,active ? Mathf.Lerp(.10f,1.25f,red) : .35f);
            Tint(blueLens,active ? Mathf.Lerp(.10f,1.25f,blue) : .35f);
            if(redLight==null || blueLight==null) return;
            redLight.enabled=blueLight.enabled=active;
            redLight.intensity=6f*red;blueLight.intensity=6f*blue;
            // The custom low-poly surface uses the same local lights without adding shader variants.
            // Standard URP surfaces receive the actual point lights above.
            SetShaderBeacon(RedBeacon,redLight.transform.position,active ? red*1.2f : 0f);
            SetShaderBeacon(BlueBeacon,blueLight.transform.position,active ? blue*1.2f : 0f);
        }

        private void Tint(Renderer lens,float brightness)
        {
            if(lens==null) return;
            lens.GetPropertyBlock(lensProperties);
            lensProperties.SetColor(BaseColour,new Color(brightness,brightness,brightness,1f));
            lens.SetPropertyBlock(lensProperties);
        }

        private static void SetShaderBeacon(int property,Vector3 position,float intensity)
            => Shader.SetGlobalVector(property,new Vector4(position.x,position.y,position.z,intensity));

        private void OnDisable()
        {
            if(siren!=null) siren.Stop();
            IsPursuing=SirenPaused=false;
            SetLights(0f,0f,false);
            Shader.SetGlobalVector(RedBeacon,Vector4.zero);Shader.SetGlobalVector(BlueBeacon,Vector4.zero);
        }
    }
}
