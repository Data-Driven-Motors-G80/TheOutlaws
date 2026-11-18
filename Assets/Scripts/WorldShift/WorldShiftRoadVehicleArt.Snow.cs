using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftRoadVehicleArt
    {
        private Material snowBeacon;
        private float snowBeaconTime;

        private void CreateSnowplough(Vector3 size,ShiftWorld world)
        {
            gameObject.name="Oncoming snowplough";
            transform.localScale=new Vector3(size.x/3.05f,size.y/1.8f,size.z/5.5f);
            var body=new WorldShiftArtMesh();var trim=new WorldShiftArtMesh();var blade=new WorldShiftArtMesh();
            Color paint=new Color(.20f,.35f,.44f),rubber=new Color(.045f,.065f,.08f),steel=new Color(.35f,.43f,.47f);
            Color snow=new Color(.89f,.95f,.98f),glass=new Color(.06f,.17f,.23f);
            body.ChamferBox(new Vector3(0,-.26f,-.15f),new Vector3(2.52f,.65f,4.4f),.12f,paint);
            body.ChamferBox(new Vector3(0,.16f,.45f),new Vector3(2.28f,.98f,1.95f),.12f,paint);
            body.ChamferBox(new Vector3(0,-.02f,-1.40f),new Vector3(2.30f,.64f,1.75f),.1f,steel);
            body.ChamferBox(new Vector3(0,.67f,.45f),new Vector3(2.26f,.14f,1.96f),.05f,snow);
            trim.Box(new Vector3(0,.28f,1.435f),new Vector3(1.98f,.49f,.045f),glass);
            trim.Beam(new Vector3(-.79f,.06f,1.465f),new Vector3(-.27f,.24f,1.465f),.035f,.025f,rubber);
            trim.Beam(new Vector3(.19f,.06f,1.465f),new Vector3(.71f,.24f,1.465f),.035f,.025f,rubber);
            foreach(float side in new[]{-1f,1f})
            {
                trim.Box(new Vector3(side*1.15f,.27f,.40f),new Vector3(.035f,.48f,1.45f),glass);
                trim.Box(new Vector3(side*1.32f,.20f,1.08f),new Vector3(.18f,.20f,.25f),rubber);
                foreach(float z in new[]{-1.55f,1.48f})
                {
                    BuildWheel(new Vector3(side*1.25f,-.50f,z),side,rubber,steel,snow*.75f);
                    // Raised tread bars make the standard wheel read as a winter tyre.
                    var tread=new WorldShiftArtMesh();
                    for(int i=0;i<12;i++)
                    {
                        float a=i*Mathf.PI/6f;
                        tread.Box(new Vector3(0,Mathf.Cos(a)*.375f,Mathf.Sin(a)*.375f),new Vector3(.24f,.045f,.045f),rubber*.7f);
                    }
                    var treadObject=Draw("Snow tyre tread",tread,OwnMaterial("Snow tyre rubber",false)).transform;
                    treadObject.SetParent(wheels[wheels.Count-1],false);
                }
                var lamp=new WorldShiftArtMesh();
                lamp.Box(new Vector3(side*.91f,.04f,1.49f),new Vector3(.35f,.21f,.05f),Color.white);
                var finish=OwnMaterial(side<0?"Left plough headlight":"Right plough headlight",true);
                Draw("Snowplough raised headlight",lamp,finish);
                if(side<0) leftHeadlight=finish;else rightHeadlight=finish;
            }
            // The blade, marker poles, cab and tyres all stay within +/-1.525 x, +/-.9 y, +/-2.75 z.
            blade.ChamferBox(new Vector3(0,-.46f,2.36f),new Vector3(2.92f,.74f,.55f),.09f,steel);
            blade.Box(new Vector3(0,-.82f,2.39f),new Vector3(2.94f,.09f,.57f),rubber);
            WorldShiftSnowObstacleArt.WorldPanel(blade,new Vector3(0,-.40f,2.675f),2.72f,.51f,world);
            foreach(float side in new[]{-1f,1f})
            {
                trim.Beam(new Vector3(side*.64f,-.44f,1.70f),new Vector3(side*1.15f,-.44f,2.14f),.13f,.13f,steel);
                trim.Box(new Vector3(side*1.40f,-.14f,2.37f),new Vector3(.065f,1.05f,.065f),new Color(.97f,.53f,.13f));
            }
            body.ChamferBox(new Vector3(0,.32f,-1.42f),new Vector3(2.10f,.12f,1.6f),.04f,snow);
            Draw("Snowplough cab and snow cover",body,OwnMaterial("Winter maintenance blue",false));
            Draw("Snowplough glass and equipment",trim,OwnMaterial("Snowplough equipment finish",false));
            var bladeFinish=OwnMaterial("World-coloured plough blade",false);bladeFinish.SetFloat("_Emission",.35f);
            Draw("Snowplough blade and world panel",blade,bladeFinish);
            var beacon=new WorldShiftArtMesh();beacon.Cylinder(new Vector3(0,.74f,.45f),.17f,.13f,.14f,Color.white,12);
            snowBeacon=OwnMaterial("Snowplough amber beacon",true);snowBeacon.SetColor(TintId,new Color(1.7f,.67f,.08f));
            Draw("Snowplough amber beacon",beacon,snowBeacon);
            BuildDirectionWarnings();
        }

        private void UpdateSnowBeacon()
        {
            if(snowBeacon==null || WorldShiftSceneLoading.Active) return;
            if(warningGame!=null && (warningGame.State!=ShiftRunState.Running || warningGame.TrainingLessonPaused)) return;
            snowBeaconTime+=Time.deltaTime;
            float pulse=.55f+.45f*Mathf.Sin(snowBeaconTime*Mathf.PI*3f);
            snowBeacon.SetColor(TintId,new Color(1.7f,.67f,.08f)*pulse);
        }
    }
}
