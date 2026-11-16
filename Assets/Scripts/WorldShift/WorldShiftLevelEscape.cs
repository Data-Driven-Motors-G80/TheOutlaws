using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Post-win presentation only. Progress is already saved by the simulation.</summary>
    public sealed partial class WorldShiftLevelEscape : MonoBehaviour
    {
        public const float Duration = 6.5f;
        private WorldShiftCity game;
        private Camera view;
        private Transform stage, coupe, cruiser, ferry, ramp, shutter;
        private Renderer redLens, blueLens;
        private AudioSource engine, siren, policeBrake;
        private Material harbourWater;
        private MaterialPropertyBlock lensProperties;
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private float elapsed;
        public bool Active { get; private set; }
        public bool IsFinale => game != null && game.LevelIndex == WorldShiftLevels.Count - 1;
        public float SequenceDuration => IsFinale ? 14f : Duration;
        public bool FinaleRevealed { get; private set; }
        public void RevealFinale() { if (IsFinale && Active && Ready) FinaleRevealed = true; }
        public bool Ready => !Active || elapsed >= SequenceDuration;
        public bool CanSkip => Active && elapsed >= .65f && !Ready;
        public float Progress => Mathf.Clamp01(elapsed / SequenceDuration);
        public void Bind(WorldShiftCity controller, Camera camera) { game = controller; view = camera; }
        public void EnsureStarted()
        {
            if (!isActiveAndEnabled || Active || game.State != ShiftRunState.Won || game.Course.Training) return;
            Active = true;
            foreach (var renderer in game.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled) { hidden.Add(renderer); renderer.enabled = false; }
            foreach(var canvas in game.GetComponentsInChildren<Canvas>(true))
                if(canvas.renderMode==RenderMode.WorldSpace && canvas.enabled) {hiddenCanvases.Add(canvas);canvas.enabled=false;}
            stage = new GameObject("Getaway celebration stage").transform;
            stage.position = Vector3.down * 2000f;
            coupe = WorldShiftPlayerCarArt.Build(stage).transform;
            cruiser = WorldShiftPoliceCarArt.Build(stage).transform;
            redLens = cruiser.Find("Red roof light lens").GetComponent<Renderer>();
            blueLens = cruiser.Find("Blue roof light lens").GetComponent<Renderer>();
            lensProperties = new MaterialPropertyBlock();
            if (game.Environment == ShiftLevel.Ocean) BuildHarbour(); else BuildPassage();
            if (IsFinale) BuildFinalOverlook();
            engine = Source("Final getaway engine", "WS_Engine", true);
            siren = Source("Receding police siren", "WorldShiftPoliceSiren", true);
            siren.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1600f; // matches the driving siren
            policeBrake=Source("Police braking", "WS_Tyres", false);policeBrake.pitch=.85f;
            engine.Play(); siren.Play();
            ApplyPose();
        }
        private AudioSource Source(string name, string resource, bool loop)
        {
            var root = new GameObject(name); root.transform.SetParent(stage, false);
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.loop = loop; source.spatialBlend = 0; source.volume = 0; source.clip = Resources.Load<AudioClip>(resource);
            return source;
        }
        private void LateUpdate()
        {
            if (game == null) return;
            EnsureStarted();
            if (Active) Advance(Time.unscaledDeltaTime);
        }
        public void Advance(float delta)
        {
            if (!Active) return;
            elapsed = Mathf.Min(SequenceDuration, elapsed + Mathf.Max(0, delta));
            ApplyPose();
        }
        public void Skip()
        {
            if (!CanSkip) return;
            elapsed = SequenceDuration; ApplyPose();
        }
        private void ApplyPose()
        {
            bool ocean = game.Environment == ShiftLevel.Ocean;
            float depart = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.2f, Duration, elapsed));
            if (ocean)
            {
                ferry.localPosition = new Vector3(0, Mathf.Sin(elapsed * 1.5f) * .035f, 24f + depart * 21f);
                float drive = Mathf.Clamp01(elapsed / 2.2f);
                float carZ = Mathf.Lerp(-18f, 20f, drive);
                float deckHeight = Mathf.Lerp(.02f, .28f, Mathf.InverseLerp(7.5f, 14f, carZ));
                coupe.localPosition = new Vector3(0, deckHeight + ferry.localPosition.y, carZ + depart * 21f);
                ramp.localRotation = Quaternion.Euler(Mathf.SmoothStep(-3f, 70f, Mathf.InverseLerp(2.2f, 3.2f, elapsed)), 0, 0);
            }
            else
            {
                coupe.localPosition = new Vector3(0, .02f, Mathf.Lerp(-18f, 32f, Mathf.Clamp01(elapsed / 2.8f)));
                coupe.gameObject.SetActive(elapsed < 3f);
                shutter.localPosition = new Vector3(0, Mathf.Lerp(9f, 3f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.7f, 4.2f, elapsed))), 17f);
            }
            float police = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 2.8f), 2);
            cruiser.localPosition = new Vector3(ocean ? 1.5f : -1.6f, .02f, Mathf.Lerp(-29f, ocean ? 5.5f : 10f, police));
            float flash = .5f + .5f * Mathf.Sin(elapsed * 8f);
            Lens(redLens, flash); Lens(blueLens, 1f - flash);
            var red = redLens.transform.position; var blue = blueLens.transform.position;
            Shader.SetGlobalVector("_PoliceBeaconRed", new Vector4(red.x, red.y, red.z, flash));
            Shader.SetGlobalVector("_PoliceBeaconBlue", new Vector4(blue.x, blue.y, blue.z, 1f - flash));
            float effects = WorldShiftAudioSettings.Effects;
            policeBrake.volume=effects*.07f;
            if(elapsed>1.9f && elapsed<2.8f) {if(!policeBrake.isPlaying) policeBrake.Play();}
            else policeBrake.Stop();
            if(harbourWater!=null) harbourWater.SetFloat("_OceanTime",elapsed);
            engine.pitch = ocean ? Mathf.Lerp(1.25f, .55f, depart) : 1.25f;
            engine.volume = effects * (ocean ? Mathf.Lerp(.12f, .035f, depart) : .12f * (1f - Mathf.InverseLerp(1.8f, 3f, elapsed)));
            siren.volume = effects * .025f * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.5f, 5.5f, elapsed)));
            if (Ready) { engine.Stop(); siren.Stop(); }
            // A camera sweep keeps the dock/police and departing ferry in the same story shot.
            Vector3 camera = ocean ? Vector3.Lerp(new Vector3(15, 9, -31), new Vector3(25, 13, -1), Mathf.SmoothStep(0, 1, Progress))
                : Vector3.Lerp(new Vector3(4.5f, 7, -28), new Vector3(4.8f, 7, -3), Mathf.SmoothStep(0, 1, Progress));
            Vector3 target = ocean ? new Vector3(0, 1, Mathf.Lerp(-3, 23, Progress)) : new Vector3(0, 1.8f, Mathf.Lerp(-3, 18, Progress));
            view.transform.SetPositionAndRotation(stage.TransformPoint(camera), Quaternion.LookRotation(target - camera, Vector3.up));
            view.fieldOfView = 55f;
            if (IsFinale) ApplyFinalOverlook();
        }
        private void Lens(Renderer renderer, float strength)
        {
            lensProperties.Clear(); lensProperties.SetColor("_BaseColor", Color.white * Mathf.Lerp(.15f, 1.4f, strength));
            renderer.SetPropertyBlock(lensProperties);
        }
        private Material Finish(string name, Color tint, float glow = 0)
        {
            var material = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = name };
            material.SetColor("_BaseColor", tint); material.SetFloat("_Emission", glow);
            materials.Add(material); return material;
        }
        private Transform Draw(string name, WorldShiftArtMesh art, Material material, Transform parent = null)
        {
            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
            root.SetParent(parent != null ? parent : stage, false);
            var mesh = art.Mesh(name); meshes.Add(mesh); root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = material; return root;
        }
        private void BuildHarbour()
        {
            var paint = Finish("Harbour finishes", Color.white);
            var sea = new WorldShiftArtMesh(); sea.Box(new Vector3(0, -1.2f, 20), new Vector3(2000, .2f, 2000), new Color(.035f, .32f, .40f));
            harbourWater=new Material(Resources.Load<Shader>("WorldShiftOceanWater")){name="Ferry harbour water"};materials.Add(harbourWater);
            harbourWater.SetFloat("_ChannelMask",0);harbourWater.SetFloat("_FoamAmount",.025f);
            harbourWater.SetFloat("_RippleScale",.65f);
            if(game.World==ShiftWorld.Echo)
            {
                harbourWater.SetColor("_DeepColor",new Color(.008f,.035f,.10f));
                harbourWater.SetColor("_ShallowColor",new Color(.025f,.23f,.30f));
                harbourWater.SetColor("_SkyColor",new Color(.06f,.15f,.24f));
            }
            Draw("Harbour water", sea, harbourWater);
            var islands=new WorldShiftArtMesh();
            islands.Rock(new Vector3(-42,1,70),new Vector3(22,8,25),new Color(.32f,.43f,.42f),2);
            islands.Rock(new Vector3(43,1,85),new Vector3(30,12,35),new Color(.35f,.44f,.40f),5);
            islands.Cylinder(new Vector3(-42,4,70),2.1f,1.6f,10,new Color(.87f,.86f,.73f),12);
            islands.Cylinder(new Vector3(-42,14,70),2.4f,2.4f,1.3f,new Color(.09f,.28f,.32f),12);
            islands.Cylinder(new Vector3(-42,15.3f,70),2.7f,0,1.5f,new Color(.65f,.27f,.16f),12);
            Draw("Harbour islands and lighthouse",islands,paint);
            var dock = new WorldShiftArtMesh();
            dock.Box(new Vector3(0, -.55f, -12), new Vector3(16, 1.1f, 40), new Color(.34f, .38f, .38f));
            dock.Box(new Vector3(0, -.7f, -37), new Vector3(95, .9f, 16), new Color(.25f, .31f, .31f));
            foreach (float side in new[] {-1f, 1f})
            {
                for (int z = -26; z <= 6; z += 8)
                {
                    dock.Cylinder(new Vector3(side * 7.2f, -.05f, z), .30f, .28f, .65f, new Color(.20f, .26f, .27f), 10);
                    dock.Box(new Vector3(side * 7.2f, .60f, z), new Vector3(.8f, .10f, .5f), new Color(.85f, .70f, .30f));
                }
                dock.Box(new Vector3(side * 22f, 2.8f, -32), new Vector3(16, 6.5f, 14), new Color(.37f, .52f, .53f));
                for (int z = -27; z < 7; z += 4) dock.Box(new Vector3(side * 6.4f, .012f, z), new Vector3(.13f, .025f, 2), new Color(.88f, .77f, .43f));
            }
            Draw("Harbour dock and bollards", dock, paint);
            ferry = new GameObject("Departing car ferry").transform; ferry.SetParent(stage, false);
            var boat = new WorldShiftArtMesh();
            boat.ChamferBox(new Vector3(0, -.6f, 0), new Vector3(10, 1.5f, 21), .4f, new Color(.08f, .20f, .28f));
            boat.Box(new Vector3(0, .12f, 0), new Vector3(9.6f, .25f, 20.6f), new Color(.49f, .55f, .53f));
            boat.Box(new Vector3(0, 1.65f, 6.8f), new Vector3(7.8f, 3, 5.5f), new Color(.86f, .86f, .75f));
            boat.Box(new Vector3(0, 2.0f, 3.99f), new Vector3(6.7f, 1.05f, .06f), new Color(.08f, .25f, .31f));
            boat.Box(new Vector3(0, 3.23f, 6.8f), new Vector3(8.3f, .20f, 5.9f), new Color(.13f, .30f, .35f));
            foreach (float side in new[] {-1f, 1f})
            {
                boat.Box(new Vector3(side * 4.7f, 1.15f, 0), new Vector3(.10f, .10f, 20), new Color(.88f, .88f, .79f));
                for (int z = -9; z <= 9; z += 3) boat.Box(new Vector3(side * 4.7f, .70f, z), new Vector3(.09f, 1f, .09f), new Color(.86f, .86f, .76f));
                boat.Box(new Vector3(side * 2.1f, .255f, -2), new Vector3(.09f, .02f, 12), new Color(.88f, .77f, .40f));
            }
            foreach(float side in new[]{-1f,1f}) for(int z=-7;z<=2;z+=3)
                boat.Cylinder(new Vector3(side*4.85f,-.3f,z),.33f,.33f,.6f,new Color(.035f,.055f,.065f),10);
            boat.Box(new Vector3(0,3.8f,8.5f),new Vector3(.10f,1.2f,.10f),new Color(.72f,.77f,.74f));
            boat.Box(new Vector3(0,4.35f,8.5f),new Vector3(1.8f,.10f,.22f),new Color(.87f,.87f,.75f));
            Draw("Ferry hull, deck, wheelhouse and rails", boat, paint, ferry);
            ramp = new GameObject("Ferry loading ramp").transform; ramp.SetParent(ferry, false); ramp.localPosition = new Vector3(0, .24f, -10.5f);
            var board = new WorldShiftArtMesh(); board.Box(new Vector3(0, 0, -3), new Vector3(4.4f, .18f, 6), new Color(.46f, .50f, .47f));
            foreach (float x in new[] {-2.1f, 2.1f}) board.Box(new Vector3(x, .10f, -3), new Vector3(.12f, .05f, 6), new Color(.95f, .75f, .26f));
            Draw("Rising ferry ramp", board, paint, ramp);
            var wake = new WorldShiftArtMesh();
            foreach (float side in new[] {-1f, 1f}) wake.Quad(new Vector3(side * 4.8f, -1.08f, -7), new Vector3(side * 5.4f, -1.08f, -9), new Vector3(side * 8.5f, -1.08f, -26), new Vector3(side * 7.9f, -1.08f, -25), new Color(.55f, .81f, .81f));
            Draw("Ferry wake", wake, Finish("Wake foam", Color.white, .3f), ferry);
        }
        private void BuildPassage()
        {
            bool desert = game.Environment == ShiftLevel.Desert, snow = game.Environment == ShiftLevel.Snow;
            Color ground = desert ? new Color(.59f,.40f,.25f) : snow ? new Color(.69f,.79f,.83f) : new Color(.22f,.29f,.33f);
            var material = Finish("Escape passage finishes", Color.white);
            var set = new WorldShiftArtMesh();
            set.Box(new Vector3(0,-.3f,8),new Vector3(110,.5f,140),ground);
            set.Box(new Vector3(0,-.02f,0),new Vector3(11,.08f,85),new Color(.13f,.18f,.21f));
            for(int z=-38;z<34;z+=6) set.Box(new Vector3(0,.03f,z),new Vector3(.13f,.02f,2.4f),new Color(.84f,.83f,.69f));
            foreach(float side in new[]{-1f,1f})
            {
                set.Box(new Vector3(side*6,3.1f,26),new Vector3(1,6.2f,20),ground*.8f);
                if(game.Environment==ShiftLevel.City)
                {
                    set.Box(new Vector3(side*16,7,20),new Vector3(15,14,36),new Color(.25f,.35f,.40f));
                    for(int z=5;z<35;z+=5) for(int y=3;y<14;y+=3) set.Box(new Vector3(side*8.45f,y,z),new Vector3(.05f,1.2f,2.4f),new Color(.77f,.72f,.48f));
                }
                else for(int i=0;i<6;i++)
                {
                    var foot=new Vector3(side*(12+i%2*5),0,-20+i*13);
                    if(snow)
                    {
                        set.Cylinder(foot,.25f,.18f,4,new Color(.28f,.26f,.22f),8);
                        set.Cylinder(foot+Vector3.up*1.3f,2.5f,0,6,new Color(.16f,.30f,.29f),7);
                        set.Cylinder(foot+Vector3.up*4.1f,1.5f,0,3.3f,new Color(.79f,.86f,.87f),7);
                    }
                    else set.Rock(foot+Vector3.up*4,new Vector3(7,9+i%3*2,9),ground*(.8f+i*.04f),i);
                }
            }
            set.Box(new Vector3(0,6.2f,26),new Vector3(13,.7f,20),ground*.75f);
            set.Box(new Vector3(0,3.1f,36),new Vector3(12,6.3f,.4f),new Color(.018f,.028f,.035f));
            Draw("Themed escape passage",set,material);
            var gate=new WorldShiftArtMesh();gate.Box(Vector3.zero,new Vector3(10.8f,6,.25f),new Color(.20f,.29f,.31f));
            for(int i=-5;i<=5;i++) gate.Box(new Vector3(0,i*.5f,-.15f),new Vector3(10.6f,.055f,.05f),new Color(.42f,.49f,.48f));
            gate.Box(new Vector3(0,-2.6f,-.18f),new Vector3(10.6f,.24f,.06f),new Color(.92f,.70f,.25f));
            shutter=Draw("Closing service passage shutter",gate,material);
        }
        private void OnDisable()
        {
            Active = false;
            foreach(var renderer in hidden) if(renderer!=null) renderer.enabled=true;
            hidden.Clear();
            foreach(var canvas in hiddenCanvases) if(canvas!=null) canvas.enabled=true;
            hiddenCanvases.Clear();
            if(stage!=null) Destroy(stage.gameObject);
            RestoreFinaleSky();
            Shader.SetGlobalVector("_PoliceBeaconRed",Vector4.zero);Shader.SetGlobalVector("_PoliceBeaconBlue",Vector4.zero);
        }
        private void OnDestroy()
        {
            foreach(var mesh in meshes) if(mesh!=null) Destroy(mesh);
            foreach(var material in materials) if(material!=null) Destroy(material);
        }
    }
}
