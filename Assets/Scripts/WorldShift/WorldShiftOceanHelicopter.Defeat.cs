using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanHelicopter
    {
        private Transform defeatRoot, defeatFire, defeatCore, defeatShock, defeatSmoke;
        private readonly Transform[] defeatPieces=new Transform[18];
        private bool defeatExplosionPlayed;
        private AudioClip defeatClip, seaCrashClip;
        private const float SeaCrashSeconds=1.65f;
        private bool seaImpactPlayed;
        private Transform seaImpactRoot, seaFoam, seaFire;
        private readonly Transform[] seaSpray=new Transform[16];
        private Vector3 crashOrigin,seaImpactPoint;
        private Quaternion crashRotation;
        public bool SeaImpactPlayed => seaImpactPlayed;
        public bool DefeatExplosionPlayed => defeatExplosionPlayed;
        public float DefeatAge => crashAge;

        private void BuildDefeatEffects()
        {
            defeatRoot=new GameObject("Helicopter destruction burst").transform;defeatRoot.SetParent(transform,false);
            defeatFire=ExplosionSphere("Large police helicopter fireball",fireMaterial);
            defeatCore=ExplosionSphere("Hot explosion core",hitMaterial);
            var ring=new WorldShiftArtMesh();ring.Ring(1,.12f,Color.white);
            defeatShock=MeshObject("Air burst pressure ring",ring,defeatRoot,fireMaterial);
            defeatSmoke=BuildFragments("Destruction smoke",smokeFinish,9,1.1f);defeatSmoke.SetParent(defeatRoot,false);
            for(int i=0;i<defeatPieces.Length;i++)
            {
                var art=new WorldShiftArtMesh();
                art.ChamferBox(Vector3.zero,new Vector3(.25f+i%3*.18f,.12f,.5f+i%4*.3f),.04f,i%3==0?new Color(.9f,.93f,.95f):new Color(.08f,.14f,.2f));
                defeatPieces[i]=MeshObject("Police aircraft fragment "+i,art,defeatRoot,finish);
            }
            defeatClip=CreateCombatCue("Police helicopter destruction",1.1f,1);
            seaImpactRoot=new GameObject("Helicopter sea impact").transform;seaImpactRoot.SetParent(transform,false);
            var foam=Mat("Sea crash foam",new Color(.7f,.94f,1f),.7f);
            var foamMesh=new WorldShiftArtMesh();foamMesh.Ring(1,.13f,Color.white);
            seaFoam=MeshObject("Expanding sea crash ring",foamMesh,seaImpactRoot,foam);
            seaFire=ExplosionSphere("Sea impact fireball",fireMaterial);seaFire.SetParent(seaImpactRoot,false);
            for(int i=0;i<seaSpray.Length;i++) seaSpray[i]=EffectRock("Sea splash plume "+i,seaImpactRoot,foam);
            seaCrashClip=CreateCombatCue("Helicopter sea crash boom and wash",1.4f,4);
            seaImpactRoot.gameObject.SetActive(false);
            defeatRoot.gameObject.SetActive(false);
        }
        private Transform ExplosionSphere(string name,Material material)
        {
            var art=new WorldShiftArtMesh();
            Vector3 P(int row,int column)
            {
                float latitude=row*Mathf.PI/10f,longitude=column*Mathf.PI/8f;
                return new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
            }
            for(int row=0;row<10;row++) for(int column=0;column<16;column++)
                art.Quad(P(row,column),P(row,column+1),P(row+1,column+1),P(row+1,column),Color.white*(.86f+(row+column)%3*.07f));
            return MeshObject(name,art,defeatRoot,material);
        }
        private void BeginDefeatExplosion()
        {
            defeatExplosionPlayed=true;crashAge=0;
            crashOrigin=craft.position;crashRotation=craft.rotation;
            var shore=game.Route.Sample(Mathf.Min(game.RunLength,game.Distance+105f));
            seaImpactPoint=shore.Position+shore.Right*18f;seaImpactPoint.y=-9.85f;
            defeatRoot.position=crashOrigin;
            audioSource.PlayOneShot(defeatClip,.6f*WorldShiftAudioSettings.Effects);
            // Two short decaying pulses: aircraft breakup, then the heavier impact in the sea.
            game.Impact?.Kick(.26f,.42f);
        }
        private void PresentFallingAircraft()
        {
            float t=Mathf.Clamp01(crashAge/SeaCrashSeconds);
            craft.position=Vector3.Lerp(crashOrigin,seaImpactPoint,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*3f);
            craft.rotation=crashRotation*Quaternion.Euler(t*95f,t*25f,t*155f);
        }
        private void StepSeaImpact()
        {
            if(seaImpactPlayed||crashAge<SeaCrashSeconds) return;
            seaImpactPlayed=true;
            seaImpactRoot.position=seaImpactPoint;
            audioSource.PlayOneShot(seaCrashClip,.72f*WorldShiftAudioSettings.Effects);
            game.Impact?.Kick(.42f,.7f);
        }
        private void PresentDefeatEffects(bool visible)
        {
            if(defeatRoot==null) return;
            float splashAge=crashAge-SeaCrashSeconds;
            bool splash=visible&&seaImpactPlayed&&splashAge>=0&&splashAge<1.65f;
            seaImpactRoot.gameObject.SetActive(splash);
            if(splash)
            {
                seaFoam.localScale=Vector3.one*(2+splashAge*12);
                seaFire.gameObject.SetActive(splashAge<.45f);
                seaFire.localPosition=Vector3.up*2;
                seaFire.localScale=Vector3.one*(4*Mathf.Sin(Mathf.Clamp01(splashAge/.45f)*Mathf.PI));
                for(int i=0;i<seaSpray.Length;i++)
                {
                    float a=i*2.39996f;
                    Vector3 v=new Vector3(Mathf.Cos(a)*(5+i%3),13+i%4*2,Mathf.Sin(a)*(5+i%3));
                    seaSpray[i].localPosition=v*splashAge+Vector3.down*(splashAge*splashAge*9);
                    seaSpray[i].localScale=new Vector3(.65f,2.4f,.65f)*Mathf.Clamp01((1.65f-splashAge)*2);
                }
            }
            bool show=visible&&defeatExplosionPlayed&&crashAge<2.5f;
            defeatRoot.gameObject.SetActive(show);if(!show) return;
            float age=crashAge;
            defeatFire.gameObject.SetActive(age<.85f);defeatCore.gameObject.SetActive(age<.45f);
            float fire=Mathf.Sin(Mathf.Clamp01(age/.85f)*Mathf.PI);
            defeatFire.localScale=new Vector3(8,7,8)*Mathf.Max(.05f,fire);
            defeatCore.localScale=Vector3.one*(4*Mathf.Sin(Mathf.Clamp01(age/.45f)*Mathf.PI));
            var camera=Camera.main;
            if(camera!=null) defeatCore.position=defeatRoot.position+(camera.transform.position-defeatRoot.position).normalized*5f;
            defeatShock.gameObject.SetActive(age<.9f);
            defeatShock.localRotation=Quaternion.Euler(70,0,0);defeatShock.localScale=Vector3.one*(1+age*13);
            defeatSmoke.localPosition=Vector3.up*(age*2);
            defeatSmoke.localScale=Vector3.one*(1+age*1.8f)*Mathf.Clamp01((2.5f-age)*2);
            for(int i=0;i<defeatPieces.Length;i++)
            {
                float a=i*2.39996f, speed=4+i%5;
                Vector3 velocity=new Vector3(Mathf.Cos(a)*speed,3+i%4,Mathf.Sin(a)*speed);
                defeatPieces[i].localPosition=velocity*age+Vector3.down*(age*age*4);
                defeatPieces[i].localRotation=Quaternion.Euler(age*(100+i*13),age*i*37,age*(75+i*9));
                defeatPieces[i].localScale=Vector3.one*Mathf.Clamp01(2.5f-age);
            }
        }
        private void ResetDefeatEffects()
        {
            defeatExplosionPlayed=Defeated;seaImpactPlayed=Defeated;
            if(seaImpactRoot!=null) seaImpactRoot.gameObject.SetActive(false);
            if(defeatRoot!=null) defeatRoot.gameObject.SetActive(false);
        }
    }
}
