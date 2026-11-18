using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Snow-only sky art and a sleigh-bell flyby. No colliders, lights or changes to driving.</summary>
    public sealed partial class WorldShiftSnowSky : MonoBehaviour
    {
        public const float FlyoverSeconds = 14f;
        private WorldShiftCity game;
        private Camera view;
        private Material sky, figureFinish;
        private readonly List<Mesh> figureMeshes=new List<Mesh>();
        private AudioSource sleighBells;
        private bool bellsPaused;
        private Transform santa;
        private float skyTime, flyoverTime;
        private bool flyoverStarted;
        public bool FlyoverActive => flyoverStarted && flyoverTime < FlyoverSeconds;

        public void Bind(WorldShiftCity controller,Camera camera,Material skyMaterial)
        {
            game=controller;view=camera;sky=skyMaterial;
            sky.SetFloat("_Snow",1f);
            // Every Snow run gets one flyover; checkpoint retries never repeat it.
            BuildSanta();
            sleighBells=santa.gameObject.AddComponent<AudioSource>();
            sleighBells.playOnAwake=false;sleighBells.loop=true;sleighBells.spatialBlend=0;
            sleighBells.dopplerLevel=0;sleighBells.priority=180;sleighBells.volume=0;
            sleighBells.clip=Resources.Load<AudioClip>("WS_SantaBells");
            santa.gameObject.SetActive(false);
        }

        private bool SafeToStart()
        {
            if(game.OnIce || game.IsRelayAirborne) return false;
            float d=game.Distance;
            foreach(var jump in game.RelayLayout)
                if(d>=jump.Ramp-95f && d<jump.End+5f) return false;
            foreach(var hazard in game.Hazards)
            {
                hazard.BoundsAt(d,out float start,out float end);
                if(start<d+35f && end>d-5f) return false;
            }
            // The opening is clear dry road. Don't postpone the flyover for its gentle first bend.
            if(d<20f) return true;
            var a=game.Route.Sample(d);var b=game.Route.Sample(d+12f);
            return Vector3.Angle(a.Forward,b.Forward)<5f;
        }

        public void Advance(float delta)
        {
            if(game==null || WorldShiftSceneLoading.Active || game.State!=ShiftRunState.Running || game.TrainingLessonPaused)
            {
                UpdateFlyoverAudio();
                if(game!=null && (game.State==ShiftRunState.Won || game.State==ShiftRunState.Lost)) santa.gameObject.SetActive(false);
                return;
            }
            float dt=Mathf.Max(0,delta);skyTime+=dt;sky.SetFloat("_SkyTime",skyTime);
            figureFinish.SetFloat("_Night",sky.GetFloat("_Night"));
            if(!flyoverStarted && game.Distance>=4f && SafeToStart())
            {flyoverStarted=true;flyoverTime=0;santa.gameObject.SetActive(true);}
            if(!FlyoverActive) {UpdateFlyoverAudio();santa.gameObject.SetActive(false);return;}
            flyoverTime=Mathf.Min(FlyoverSeconds,flyoverTime+dt);
            PoseSanta();UpdateFlyoverAudio();
            if(!FlyoverActive) santa.gameObject.SetActive(false);
        }

        private static Vector3 FlightPoint(float t)
        {
            float u=1-t;
            return u*u*u*new Vector3(-150,32,145)+3*u*u*t*new Vector3(-65,50,95)
                +3*u*t*t*new Vector3(75,16,105)+t*t*t*new Vector3(160,42,155);
        }

        private void PoseSanta()
        {
            float t=flyoverTime/FlyoverSeconds;
            Vector3 forward=Vector3.ProjectOnPlane(game.Route.Sample(game.Distance).Forward,Vector3.up).normalized;
            Quaternion frame=Quaternion.LookRotation(forward,Vector3.up);
            Vector3 point=FlightPoint(t);
            Vector3 tangent=(FlightPoint(Mathf.Min(1,t+.002f))-FlightPoint(Mathf.Max(0,t-.002f))).normalized;
            Vector3 normal=Vector3.Cross(tangent,Vector3.up).normalized;
            Quaternion heading=Quaternion.LookRotation(normal,Vector3.Cross(normal,tangent));
            float bank=Mathf.Sin(t*Mathf.PI*2)*8f;
            santa.SetPositionAndRotation(view.transform.position+frame*point,
                frame*heading*Quaternion.AngleAxis(bank,Vector3.right));
            AnimateTeam(flyoverTime);
            figureFinish.SetFloat("_Opacity",Mathf.SmoothStep(0,1,t/.10f)*(1-Mathf.SmoothStep(0,1,(t-.90f)/.10f)));
        }

        private void UpdateFlyoverAudio()
        {
            if(sleighBells==null) return;
            if(!FlyoverActive || game==null || game.State==ShiftRunState.Won || game.State==ShiftRunState.Lost)
            {sleighBells.Stop();bellsPaused=false;return;}
            float t=flyoverTime/FlyoverSeconds;
            float envelope=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.7f);
            sleighBells.volume=WorldShiftAudioSettings.Effects*.18f*envelope;
            sleighBells.panStereo=Mathf.Clamp(FlightPoint(t).x/145f,-1,1)*.75f;
            sleighBells.pitch=Mathf.Lerp(1.03f,.96f,t);
            bool pause=WorldShiftAudioSettings.Suspended(game) || game.State!=ShiftRunState.Running;
            if(pause) {if(!bellsPaused) sleighBells.Pause();bellsPaused=true;return;}
            if(bellsPaused) {sleighBells.UnPause();bellsPaused=false;}
            if(!sleighBells.isPlaying) sleighBells.Play();
        }

        private void Update() => Advance(Time.deltaTime);
        private void LateUpdate() { if(FlyoverActive && game!=null && game.State==ShiftRunState.Running) PoseSanta(); }

        private void OnDestroy()
        {
            if(sky!=null) {sky.SetFloat("_Snow",0);sky.SetFloat("_SkyTime",0);}
            if(sleighBells!=null) sleighBells.Stop();
            foreach(var mesh in figureMeshes) if(mesh!=null) Destroy(mesh);
            if(figureFinish!=null) Destroy(figureFinish);
        }
    }
}
