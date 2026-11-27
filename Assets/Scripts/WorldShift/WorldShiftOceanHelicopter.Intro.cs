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
            var item=game.Collection.Items[0];var pickup=game.Route.Sample(item.x);
            Vector3 rocketFocus=pickup.Position+pickup.Right*item.y+pickup.Up*2f;
            Vector3 rocketView=rocketFocus-pickup.Forward*7f-pickup.Right*2f+pickup.Up*3.5f;
            float lesson=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,1.55f,IntroClock));
            float returnToCar=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.6f,IntroDuration,IntroClock));
            camera.transform.position=Vector3.Lerp(Vector3.Lerp(close,rocketView,lesson),follow,returnToCar);
            // Sample the real pickup in the live scene; the lesson never consumes ammo or damages the boss.
            PresentIntroAircraft();
            Vector3 focus=Vector3.Lerp(Vector3.Lerp(craft.position,rocketFocus,lesson),ahead.Position+Vector3.up*.6f,returnToCar);
            camera.transform.rotation=Quaternion.LookRotation(focus-camera.transform.position,Vector3.up);
            camera.fieldOfView=Mathf.Lerp(Mathf.Lerp(57f,43f,lesson),70f,returnToCar);
            return true;
        }
    }
}
