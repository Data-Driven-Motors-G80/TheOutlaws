using UnityEngine;
using TMPro;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanHelicopter
    {
        private Transform launcherMount, launcherBarrel, launcherMuzzle, aircraftHealth;
        private readonly Transform[] aircraftHealthSegments = new Transform[MaximumHealth];
        private Material healthFinish, redBeaconFinish, blueBeaconFinish;
        private Camera healthCamera;

        private void BuildRoofLauncher()
        {
            Color steel = new Color(.30f,.37f,.43f), dark = new Color(.075f,.10f,.13f);
            Color white = new Color(.84f,.88f,.91f), brass = new Color(.92f,.53f,.13f);
            launcherMount = new GameObject("Roof launcher mounting plate").transform;
            launcherMount.SetParent(game.Player,false);
            launcherMount.localPosition = new Vector3(0,1.38f,0);
            launcherMount.localScale = Vector3.one*.8f;
            var mount = new WorldShiftArtMesh();
            mount.ChamferBox(Vector3.zero,new Vector3(1.15f,.14f,1.08f),.05f,dark);
            mount.Cylinder(new Vector3(0,.06f,0),.46f,.46f,.12f,steel,16);
            mount.Cylinder(new Vector3(0,.18f,0),.35f,.35f,.10f,dark,16);
            foreach(float x in new[]{-.44f,.44f}) foreach(float z in new[]{-.4f,.4f})
                mount.Cylinder(new Vector3(x,.075f,z),.05f,.05f,.035f,white,6);
            MeshObject("Mounting bolts and rotating bearing",mount,launcherMount,finish);
            turret = new GameObject("Ocean automatic rocket launcher").transform;
            turret.SetParent(launcherMount,false);turret.localPosition=Vector3.up*.29f;
            var cradle = new WorldShiftArtMesh();
            cradle.ChamferBox(new Vector3(0,.12f,-.26f),new Vector3(.78f,.50f,.80f),.10f,steel);
            foreach(float x in new[]{-.36f,.36f})
            {
                cradle.Wheel(new Vector3(x,.2f,-.17f),.18f,.1f,dark);
                cradle.Wheel(new Vector3(x*1.15f,.2f,-.17f),.085f,.035f,brass);
            }
            cradle.ChamferBox(new Vector3(.27f,.46f,-.3f),new Vector3(.20f,.16f,.3f),.035f,dark);
            cradle.Box(new Vector3(.27f,.46f,-.14f),new Vector3(.13f,.08f,.025f),new Color(.25f,.80f,.86f));
            MeshObject("Armoured breech and elevation pivots",cradle,turret,finish);
            launcherBarrel=new GameObject("Recoil barrel assembly").transform;
            launcherBarrel.SetParent(turret,false);launcherBarrel.localPosition=new Vector3(0,.23f,.04f);
            var barrel = new WorldShiftArtMesh();
            barrel.Cylinder(Vector3.zero,.225f,.205f,1.55f,white,12);
            foreach(float y in new[]{.12f,.50f,1.32f}) barrel.Cylinder(Vector3.up*y,.247f,.247f,.09f,steel,12);
            barrel.Cylinder(Vector3.up*1.45f,.265f,.265f,.17f,dark,12);
            barrel.Cylinder(Vector3.up*1.61f,.235f,.235f,.022f,brass,12);
            barrel.Cylinder(Vector3.up*1.635f,.185f,.185f,.006f,Color.black,12);
            for(int i=0;i<4;i++)
                barrel.Box(new Vector3(.222f,.68f+i*.13f,0),new Vector3(.025f,.065f,.17f),dark);
            var tube=MeshObject("Faceted tube, heat bands and recessed muzzle",barrel,launcherBarrel,finish);
            tube.localRotation=Quaternion.Euler(90,0,0);
            launcherMuzzle=new GameObject("Rocket muzzle").transform;
            launcherMuzzle.SetParent(launcherBarrel,false);launcherMuzzle.localPosition=Vector3.forward*1.65f;
        }

        private void BuildPoliceLivery()
        {
            var paint=new WorldShiftArtMesh();Color navy=new Color(.035f,.075f,.12f), white=new Color(.95f,.97f,1f);
            foreach(float side in new[]{-1f,1f})
            {
                // Broad contrasting department bands read from the chase camera, not only in close-ups.
                paint.Box(new Vector3(side*1.668f,-.56f,-.42f),new Vector3(.025f,.62f,3.4f),navy);
                paint.Box(new Vector3(side*1.69f,-.88f,-.42f),new Vector3(.025f,.065f,3.35f),white);
                paint.Box(new Vector3(side*.14f,1.8f,-6.8f),new Vector3(.04f,.44f,1.18f),navy);
                paint.Beam(new Vector3(side*.3f,.64f,-3.8f),new Vector3(side*.27f,.71f,-4.4f),.07f,.18f,white);
            }
            paint.ChamferBox(new Vector3(0,-.66f,2.72f),new Vector3(2.25f,.48f,.12f),.035f,navy);
            MeshObject("Black and white police department stripes",paint,craft,finish);
            var nose=new GameObject("Nose POLICE lettering",typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            nose.transform.SetParent(craft,false);nose.transform.localPosition=new Vector3(0,-.66f,2.79f);
            nose.transform.localRotation=Quaternion.Euler(0,180,0);
            nose.rectTransform.sizeDelta=new Vector2(2.05f,.37f);nose.text="POLICE";
            nose.alignment=TextAlignmentOptions.Center;WorldShiftTypography.Apply(nose,FontStyles.Bold,3f);
            nose.fontSize=3;nose.enableAutoSizing=true;nose.fontSizeMin=.4f;nose.fontSizeMax=3;
            nose.textWrappingMode=TextWrappingModes.NoWrap;nose.color=white;
            redBeaconFinish=Mat("Police aircraft red beacon",new Color(1,.035f,.025f),1f);
            blueBeaconFinish=Mat("Police aircraft blue beacon",new Color(.04f,.24f,1f),1f);
            Box("Red navigation strobe",craft,new Vector3(.22f,.24f,.42f),Color.white,redBeaconFinish).localPosition=new Vector3(-1.58f,.97f,-1.1f);
            Box("Blue navigation strobe",craft,new Vector3(.22f,.24f,.42f),Color.white,blueBeaconFinish).localPosition=new Vector3(1.58f,.97f,-1.1f);
        }

        private void BuildAircraftHealth()
        {
            aircraftHealth=new GameObject("Following helicopter health bar").transform;
            aircraftHealth.SetParent(transform,false);
            var backing=Mat("Air unit health backing",new Color(.025f,.045f,.065f),.85f);
            healthFinish=Mat("Air unit health segments",new Color(.12f,.88f,.73f),1f);
            Box("Health bar border",aircraftHealth,new Vector3(5.7f,.58f,.09f),Color.white,backing);
            for(int i=0;i<MaximumHealth;i++)
            {
                var empty=Box("Armour slot "+i,aircraftHealth,new Vector3(.61f,.36f,.04f),new Color(.25f,.29f,.32f),backing);
                empty.localPosition=new Vector3((i-(MaximumHealth-1)*.5f)*.685f,0,-.065f);
                aircraftHealthSegments[i]=Box("Armour health "+i,aircraftHealth,new Vector3(.61f,.36f,.035f),Color.white,healthFinish);
                aircraftHealthSegments[i].localPosition=empty.localPosition-Vector3.forward*.04f;
            }
        }

        private void PresentAircraftDetails(bool visible)
        {
            bool show=visible && DisplayHealth>0 && craft.gameObject.activeSelf;
            aircraftHealth.gameObject.SetActive(show);
            if(show)
            {
                if(healthCamera==null) healthCamera=Camera.main;
                aircraftHealth.position=craft.position+Vector3.up*5.2f;
                if(healthCamera!=null) aircraftHealth.rotation=healthCamera.transform.rotation;
                for(int i=0;i<aircraftHealthSegments.Length;i++) aircraftHealthSegments[i].gameObject.SetActive(i<DisplayHealth);
                healthFinish.SetColor("_BaseColor",DisplayHealth>4 ? new Color(.12f,.88f,.73f) : DisplayHealth>2 ? new Color(1,.67f,.13f) : new Color(1,.22f,.10f));
            }
            bool red=Mathf.Repeat(MotionTime*2.8f,1)<.5f;
            redBeaconFinish.SetFloat("_Emission",red?1.9f:.15f);
            blueBeaconFinish.SetFloat("_Emission",red?.15f:1.9f);
        }
    }
}
