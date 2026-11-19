using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanHelicopter
    {
        // Shares the real aircraft and coast; no separate scene, camera, textures or colliders.
        public void AdvanceIntro(float dt)
        {
            if(game.StoryActive&&!WorldShiftSceneLoading.Active) IntroClock=Mathf.Min(IntroDuration,IntroClock+Mathf.Max(0,dt));
        }
        private void PresentIntroAircraft()
        {
            var frame=game.Route.Sample(42f);
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(IntroClock/3.8f));
            craft.position=frame.Position+frame.Right*Mathf.Lerp(-32f,Mathf.Sin(-1.2f)*15f,t)
                +frame.Forward*Mathf.Lerp(30f,0,t)+frame.Up*Mathf.Lerp(19f,12f,t);
            craft.rotation=Quaternion.LookRotation(-frame.Forward,frame.Up)*Quaternion.Euler(0,Mathf.Lerp(-28f,0,t),Mathf.Lerp(13f,-3.3f,t));
        }
        public bool PresentIntroCamera(Camera camera)
        {
            if(!game.StoryActive||camera==null) return false;
            var frame=game.Route.Sample(42f);
            Vector3 close=frame.Position-frame.Forward*18f-frame.Right*12f+Vector3.up*10f;
            var start=game.Route.Sample(0f);var ahead=game.Route.Sample(22f);
            Vector3 follow=start.Position-start.Forward*12f+Vector3.up*6.4f;
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.3f,IntroDuration,IntroClock));
            camera.transform.position=Vector3.Lerp(close,follow,blend);
            // Derive the fly-in pose here too so script execution order cannot cause a frame of lag.
            PresentIntroAircraft();
            Vector3 focus=Vector3.Lerp(craft.position,ahead.Position+Vector3.up*.6f,blend);
            camera.transform.rotation=Quaternion.LookRotation(focus-camera.transform.position,Vector3.up);
            camera.fieldOfView=Mathf.Lerp(57f,70f,blend);
            return true;
        }
    }
}
