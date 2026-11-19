using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanHelicopter
    {
        private Transform craft, rotor, tailRotor, turret, rocket;
        private readonly List<Transform> smoke = new List<Transform>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private Material finish, glow, smokeFinish;
        private AudioSource audioSource;
        private AudioClip shotClip, impactClip;
        private float rotorAngle;
        private Transform leftWeapon, rightWeapon;
        private Renderer aircraftRenderer;
        private Material Mat(string name, Color colour, float emission)
        {
            var m=new Material(Resources.Load<Shader>("WorldShiftOceanSurface")){name=name};
            m.SetColor("_BaseColor",colour);m.SetFloat("_Emission",emission);materials.Add(m);return m;
        }
        private Transform MeshObject(string name, WorldShiftArtMesh art, Transform parent, Material material)
        {
            var o=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(parent,false);
            var mesh=art.Mesh(name);meshes.Add(mesh);o.GetComponent<MeshFilter>().sharedMesh=mesh;
            o.GetComponent<MeshRenderer>().sharedMaterial=material;return o.transform;
        }
        private Transform Box(string name,Transform parent,Vector3 size,Color colour,Material material)
        {
            var a=new WorldShiftArtMesh();a.Box(Vector3.zero,size,colour);return MeshObject(name,a,parent,material);
        }
        private void BuildArt()
        {
            finish=Mat("Police helicopter finish",Color.white,.15f);
            glow=Mat("Air strike warning",new Color(1,.52f,.08f),.8f);
            smokeFinish=Mat("Helicopter smoke",new Color(.22f,.25f,.28f),.12f);
            craft=new GameObject("Police helicopter").transform;craft.SetParent(transform,false);craft.localScale=Vector3.one*1.45f;
            var body=new WorldShiftArtMesh();var dark=new Color(.08f,.12f,.17f);var white=new Color(.86f,.9f,.93f);
            body.ChamferBox(Vector3.zero,new Vector3(3.3f,2.4f,5.4f),.45f,white);
            body.ChamferBox(new Vector3(0,.3f,2.1f),new Vector3(2.9f,1.5f,1.6f),.3f,new Color(.07f,.25f,.35f));
            body.Beam(new Vector3(0,.3f,-2),new Vector3(0,1,-7),.48f,.6f,dark);
            body.Box(new Vector3(0,1.8f,-6.8f),new Vector3(.2f,2,1.4f),white);
            body.Box(new Vector3(0,1.5f,0),new Vector3(.3f,.9f,.3f),dark);
            foreach(float side in new[]{-1f,1f})
            {
                body.Beam(new Vector3(side*1.4f,-1,1.2f),new Vector3(side*2,-2,1.2f),.15f,.15f,dark);
                body.Beam(new Vector3(side*1.4f,-1,-1.2f),new Vector3(side*2,-2,-1.2f),.15f,.15f,dark);
                body.Box(new Vector3(side*2,-2,0),new Vector3(.18f,.18f,5),dark);
                body.Box(new Vector3(side*1.75f,-.2f,.3f),new Vector3(.35f,.45f,2),dark);
                body.Box(new Vector3(side*1.72f,.5f,-.8f),new Vector3(.1f,.25f,.7f),side<0?Color.red:Color.blue);
            }
            MeshObject("Police air unit body",body,craft,finish);
            BuildPoliceDetails();
            BuildPoliceLivery();
            var blades=new WorldShiftArtMesh();blades.Box(Vector3.zero,new Vector3(11,.08f,.38f),dark);blades.Box(Vector3.zero,new Vector3(.38f,.08f,11),dark);
            rotor=MeshObject("Helicopter main rotor",blades,craft,finish);rotor.localPosition=Vector3.up*2;
            tailRotor=Box("Helicopter tail rotor",craft,new Vector3(.12f,2.8f,.16f),dark,finish);tailRotor.localPosition=new Vector3(.35f,1,-6.7f);
            BuildRoofLauncher();
            BuildAircraftHealth();
            BuildPlayerRocket();
            for(int i=0;i<7;i++)
            {
                var puff=new WorldShiftArtMesh();puff.Rock(Vector3.zero,Vector3.one,Color.white,i*7);
                smoke.Add(MeshObject("Damaged engine smoke",puff,transform,smokeFinish));
            }
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
            // Combat cues are created once by BuildCombatEffects.
            Present();
        }
        private void Present()
        {
            if(game==null||craft==null) return;
            bool visible=game.State!=ShiftRunState.Won && game.State!=ShiftRunState.Lost;
            bool running=game.State==ShiftRunState.Running && !WorldShiftSceneLoading.Active;
            if(!running) audioSource.Pause(); else audioSource.UnPause();
            var p=game.Route.Sample(Mathf.Min(game.RunLength,game.Distance+42f));
            float side=Mathf.Sin(MotionTime*.42f-1.2f)*15f;
            craft.position=p.Position+p.Right*(side)+p.Up*(12);
            craft.rotation=Quaternion.LookRotation(-p.Forward,p.Up)*Quaternion.Euler(0,0,Mathf.Cos(MotionTime*.42f)*-9);
            if(game.StoryActive) PresentIntroAircraft();
            if(defeatExplosionPlayed) PresentFallingAircraft();
            craft.gameObject.SetActive(visible&&(!defeatExplosionPlayed||crashAge<SeaCrashSeconds));
            if((running&&game.World==ShiftWorld.Day&&!Defeated) || game.StoryActive&&!WorldShiftSceneLoading.Active) rotorAngle+=Time.deltaTime*1300;
            leftWeapon.gameObject.SetActive(Hits<5);
            rotor.localRotation=Quaternion.Euler(0,rotorAngle,0);tailRotor.localRotation=Quaternion.Euler(rotorAngle*1.8f,0,0);
            launcherMount.gameObject.SetActive(visible);
            turret.rotation=Quaternion.LookRotation(craft.position-turret.position,game.Player.up);
            PresentAircraftDetails(visible&&!game.StoryActive);
            PresentCombatEffects(visible);
            PresentDefeatEffects(visible);
            PresentDolphins(visible);
            for(int i=0;i<smoke.Count;i++)
            {
                smoke[i].gameObject.SetActive(craft.gameObject.activeSelf&&Hits>=3);
                float t=Mathf.Repeat(MotionTime*.6f+i*.16f,1);
                smoke[i].position=craft.position+p.Up*(1+t*5)-p.Forward*t*3;
                smoke[i].localScale=Vector3.one*(.5f+t*1.7f);
            }

        }

        private void BuildPoliceDetails()
        {
            aircraftRenderer=craft.GetComponentInChildren<Renderer>();
            var a=new WorldShiftArtMesh();Color trim=new Color(.09f,.15f,.20f),glass=new Color(.16f,.39f,.48f);
            a.ChamferBox(new Vector3(0,1.3f,-.7f),new Vector3(1.8f,.65f,2.7f),.18f,trim);
            a.Cylinder(new Vector3(0,1.62f,-.6f),.4f,.4f,.25f,new Color(.38f,.43f,.46f),12);
            a.Box(new Vector3(0,.35f,2.92f),new Vector3(.09f,1.25f,.05f),trim);
            a.Box(new Vector3(0,-.25f,2.92f),new Vector3(2.4f,.08f,.05f),trim);
            a.Box(new Vector3(0,1.05f,-5.5f),new Vector3(3.5f,.13f,.9f),new Color(.82f,.87f,.91f));
            foreach(float side in new[]{-1f,1f})
            {
                a.ChamferBox(new Vector3(side*1.67f,.25f,.45f),new Vector3(.06f,1.2f,1.3f),.02f,glass);
                a.Box(new Vector3(side*1.71f,-.4f,.45f),new Vector3(.05f,.12f,1.5f),trim);
                a.Box(new Vector3(side*1.72f,-.08f,-.35f),new Vector3(.07f,.1f,.3f),trim);
                a.Beam(new Vector3(side*.6f,.75f,2.97f),new Vector3(side*.3f,.22f,2.97f),.035f,.035f,trim);
                for(int v=0;v<5;v++) a.Box(new Vector3(side*.94f,1.3f,-1.5f+v*.3f),new Vector3(.04f,.3f,.12f),Color.black);
                var label=new GameObject("Police aircraft livery",typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.transform.SetParent(craft,false);label.transform.localPosition=new Vector3(side*1.685f,-.54f,-.45f);
                label.transform.localRotation=Quaternion.Euler(0,-side*90,0);
                label.rectTransform.sizeDelta=new Vector2(3f,.5f);label.text="POLICE";label.alignment=TextAlignmentOptions.Center;
                WorldShiftTypography.Apply(label,FontStyles.Bold,3f);label.fontSize=3f;label.enableAutoSizing=true;label.fontSizeMin=.4f;label.fontSizeMax=3f;
                label.textWrappingMode=TextWrappingModes.NoWrap;label.color=new Color(.94f,.97f,1f);
            }
            MeshObject("Cockpit frames, vents and tail stabilizer",a,craft,finish);
            leftWeapon=Weapon(-1);rightWeapon=Weapon(1);
        }
        private Transform Weapon(float side)
        {
            var root=new GameObject("Police launcher pod").transform;root.SetParent(craft,false);root.localPosition=new Vector3(side*2.1f,-.6f,.6f);
            var a=new WorldShiftArtMesh();a.ChamferBox(Vector3.zero,new Vector3(.6f,.55f,1.8f),.12f,new Color(.19f,.24f,.28f));
            foreach(float x in new[]{-.13f,.13f}) a.Box(new Vector3(x,0,.91f),new Vector3(.16f,.19f,.02f),Color.black);
            MeshObject("Weapon pod",a,root,finish);return root;
        }

        private void OnDestroy()
        {
            DestroyCombatEffects();
            if(launcherMount!=null) Destroy(launcherMount.gameObject);
            foreach(var m in meshes) if(m!=null) Destroy(m);
            foreach(var m in materials) if(m!=null) Destroy(m);
        }
    }
}
