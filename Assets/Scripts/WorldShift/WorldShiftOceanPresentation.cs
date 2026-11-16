using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Ocean-only coastal art. All geometry is decorative; the shared route owns gameplay.</summary>
    public sealed partial class WorldShiftOceanPresentation : MonoBehaviour
    {
        private sealed class CoastChunk { public float At, End; public GameObject Root; public bool Deck; public ShiftWorld? OnlyWorld; public ShiftRelay RecoveryFor; }
        private sealed class Gem
        {
            public Transform Root, Stone, Ring, Burst;
            public bool Taken;
            public float CollectedAt = -10f;
        }
        private readonly List<CoastChunk> chunks = new List<CoastChunk>();
        private readonly List<Gem> gems = new List<Gem>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private sealed class DriveRoller { public Transform Root; public ShiftHazard Data; }
        private readonly List<DriveRoller> rollers = new List<DriveRoller>();
        private readonly List<Transform> gulls = new List<Transform>();
        private WorldShiftCity game;
        private WorldShiftRoute route;
        private Camera cameraView;
        private Material scenery, luminous, water, sky, asphaltDay, asphaltNight;
        private Transform sea;
        private AudioSource audioSource;
        private AudioClip chime;
        private ShiftWorld world;
        private float animationTime;
        private bool policeStyled;
        private const float SeaHeight = -10f;
        private static readonly Color Stone = new Color(.37f, .48f, .48f);
        private static readonly Color Sand = new Color(.9f, .79f, .53f);
        private static readonly Color Leaf = new Color(.12f, .48f, .30f);
        private static readonly Color Cream = new Color(.89f, .87f, .73f);
        private static readonly Color Coral = new Color(.94f, .38f, .25f);
        private static readonly Color Teal = new Color(.15f, .67f, .69f);

        public void Build(WorldShiftCity controller, WorldShiftRoute sharedRoute, Camera view)
        {
            game = controller; route = sharedRoute; cameraView = view;
            scenery = Material("WorldShiftOceanSurface", "Coastal stone, palms and architecture");
            luminous = Material("WorldShiftOceanSurface", "Coastal light strips and diamonds");
            luminous.SetFloat("_Emission", 1f);
            sky = Material("WorldShiftOceanSky", "Ocean sky");
            water = Material("WorldShiftOceanWater", "Animated coastal water");
            water.SetFloat("_ChannelMask", 0f);
            water.SetFloat("_RippleScale", .32f);
            asphaltDay = Material("WorldShiftOceanSurface", "Ocean asphalt - day");
            asphaltDay.SetColor("_BaseColor", new Color(.20f, .27f, .30f));
            asphaltDay.SetFloat("_Grain", 1f);
            asphaltNight = Material("WorldShiftOceanSurface", "Ocean asphalt - night");
            asphaltNight.SetColor("_BaseColor", new Color(.09f, .15f, .21f));
            asphaltNight.SetFloat("_Grain", 1f);
            route.SetSurfaceMaterials(asphaltDay, asphaltNight);
            BuildFinishes();
            var ocean = new WorldShiftArtMesh();
            ocean.Quad(new Vector3(-1600, 0, -1600), new Vector3(-1600, 0, 1600),
                new Vector3(1600, 0, 1600), new Vector3(1600, 0, -1600), Color.white);
            sea = Render("Open sea", ocean, water).transform;
            sea.position = new Vector3(0, SeaHeight, 0);
            route.VisitRoadPieces(BuildBridge);
            BuildCoast();
            BuildSectionLandmarks();
            BuildLandingFrames();
            BuildGems();
            BuildGulls();
            BuildCarContact(game.Player.Find("Original driving body"),false);
            BuildAudio();
        }

        private Material Material(string resource, string label)
        {
            var material = new Material(Resources.Load<Shader>(resource)) { name = label };
            materials.Add(material); return material;
        }

        private GameObject Render(string label, WorldShiftArtMesh geometry, Material material, Transform parent = null)
        {
            var root = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent != null ? parent : transform, false);
            Mesh mesh = geometry.Mesh(label); meshes.Add(mesh);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }

        private void BuildBridge(RoadSegment segment, float start, float end)
        {
            var deck = new WorldShiftArtMesh(); var lights = new WorldShiftArtMesh();
            // Follow the same samples and world-dependent holes as the driveable surface.
            for (float at = start; at < end - .01f; at += 4f)
            {
                float next = Mathf.Min(at + 4f, end);
                var a = route.Sample(at); var b = route.Sample(next);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 near = a.Position + a.Right * (side * 5.65f);
                    Vector3 far = b.Position + b.Right * (side * 5.65f);
                    deck.Beam(near + a.Up * .24f, far + b.Up * .24f, .42f, .48f, Cream);
                    deck.Beam(near + a.Up * .78f, far + b.Up * .78f, .10f, .10f, Stone);
                    deck.Beam(near, near + a.Up * .78f, .10f, .10f, Stone);
                    deck.Beam(a.Position + a.Right * side * 5.3f - a.Up * .72f,
                        b.Position + b.Right * side * 5.3f - b.Up * .72f, 1f, 1.2f, Stone);
                    lights.Beam(near - a.Right * side * .23f + a.Up * .45f,
                        far - b.Right * side * .23f + b.Up * .45f, .045f, .045f, Teal);
                }
            }
            if (end - start > 12f)
            {
                var p = route.Sample((start + end) * .5f);
                float bottom = SeaHeight - 2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 top = p.Position + p.Right * side * 4.6f - Vector3.up;
                    deck.Beam(new Vector3(top.x, bottom, top.z), top, 1.2f, 1.2f, Stone);
                }
                deck.Beam(p.Position - p.Right * 5.7f - Vector3.up * 1.5f,
                    p.Position + p.Right * 5.7f - Vector3.up * 1.5f, .9f, .9f, Cream);
            }
            AddJumpEndCaps(deck,start,end);
            var root = Render("Coastal viaduct " + start.ToString("0"), deck, concrete);
            BuildRoadPatina(root.transform,start,end);
            Render("Turquoise edge lighting", lights, luminous, root.transform);
            BuildPierContact(root.transform,(start+end)*.5f);
            chunks.Add(new CoastChunk { At = start, End = end, Root = root, Deck = true });
        }

        // Check the entire course so island silhouettes cannot intrude across an adjacent bend.
        private bool ClearOfRoad(Vector3 centre, float radius)
        {
            for (float d = 0; d <= game.RunLength; d += 4f)
            {
                Vector3 p = route.Sample(d).Position - centre;
                if (new Vector2(p.x, p.z).sqrMagnitude < (radius + 13f) * (radius + 13f)) return false;
            }
            return true;
        }

        private void BuildGems()
        {
            for (int i = 0; i < game.Collection.Items.Length; i++)
            {
                var item = game.Collection.Items[i]; var p = route.Sample(item.x);
                var root = new GameObject("Ocean diamond " + (i + 1)).transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(p.Position + p.Right * item.y, Quaternion.LookRotation(p.Forward, p.Up));
                var geometry = new WorldShiftArtMesh();
                for (int side = 0; side < 8; side++)
                {
                    float a = side * Mathf.PI / 4f, b = (side + 1) * Mathf.PI / 4f;
                    Vector3 left = new Vector3(Mathf.Cos(a) * .8f, .15f, Mathf.Sin(a) * .8f);
                    Vector3 right = new Vector3(Mathf.Cos(b) * .8f, .15f, Mathf.Sin(b) * .8f);
                    Color c = Color.Lerp(new Color(.12f, .79f, .73f), new Color(.85f, 1f, .92f), (side % 3) * .42f);
                    geometry.Triangle(left, Vector3.up * 1.1f, right, c);
                    geometry.Triangle(right, Vector3.down * .85f, left, c * .8f);
                }
                var stone = Render("Faceted diamond", geometry, luminous, root).transform;
                stone.localPosition = Vector3.up * 1.65f;
                var ringWorldShiftArtMesh = new WorldShiftArtMesh();
                ringWorldShiftArtMesh.Ring(1.1f, .065f, new Color(.35f, .96f, .83f));
                var ring = Render("Diamond landing circle", ringWorldShiftArtMesh, luminous, root).transform;
                ring.localPosition = Vector3.up * .08f;
                var burst = Render("Collection ripple", ringWorldShiftArtMesh, luminous, root).transform;
                burst.localPosition = Vector3.up * 1.2f; burst.gameObject.SetActive(false);
                gems.Add(new Gem { Root = root, Stone = stone, Ring = ring, Burst = burst });
            }
        }

        public void BuildTrolleyTrack(ShiftHazard data)
        {
            float distance=(data.Start+data.End)*.5f;
            var point=route.Sample(distance);
            var rail=new WorldShiftArtMesh();
            Color steel=new Color(.25f,.33f,.35f);
            foreach(float end in new[] {-1f,1f})
            {
                float z=end*((data.End-data.Start)*.5f-.12f);
                rail.Box(new Vector3(0,.018f,z),new Vector3(9.8f,.035f,.085f),steel);
                rail.Box(new Vector3(0,.022f,z+.10f),new Vector3(9.8f,.035f,.045f),Cream);
                for(float x=-4.7f;x<4.8f;x+=.7f)
                    rail.Box(new Vector3(x,.015f,z),new Vector3(.18f,.03f,.26f),steel);
            }
            var root=Render("Inset inspection-trolley rails",rail,scenery).transform;
            root.SetPositionAndRotation(point.Position,Quaternion.LookRotation(point.Forward,point.Up));
            chunks.Add(new CoastChunk {At=data.Start,End=data.End,Root=root.gameObject});
        }

        private void BuildAudio()
        {
            // A tiny synthesized two-note glass chime; no external assets or downloads.
            const int rate = 22050; var samples = new float[(int)(rate * .28f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Min(1f, t * 160f) * Mathf.Exp(-t * 19f);
                float second = Mathf.Max(0f, t - .075f);
                samples[i] = Mathf.Sin(t * 2f * Mathf.PI * 880f) * envelope * .35f
                    + Mathf.Sin(second * 2f * Mathf.PI * 1320f) * Mathf.Min(1f, second * 160f) * Mathf.Exp(-second * 22f) * .25f;
            }
            chime = AudioClip.Create("Ocean diamond chime", samples.Length, 1, rate, false);
            chime.SetData(samples, 0);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 0f; audioSource.volume = .3f;
            WorldShiftAudioGain.Bind(audioSource, game, .3f);
        }

        public void SetWorld(ShiftWorld next)
        {
            world = next; bool day = next == ShiftWorld.Day;
            sky.SetFloat("_Night", day ? 0f : 1f);
            scenery.SetColor("_BaseColor", day ? Color.white : new Color(.43f, .60f, .76f));
            water.SetColor("_DeepColor", day ? new Color(.015f, .22f, .34f) : new Color(.008f, .035f, .10f));
            water.SetColor("_ShallowColor", day ? new Color(.03f, .64f, .64f) : new Color(.025f, .23f, .30f));
            water.SetColor("_FoamColor", day ? new Color(.71f, .94f, .86f) : new Color(.18f, .40f, .49f));
            water.SetColor("_SkyColor", day ? new Color(.62f, .82f, .86f) : new Color(.06f, .15f, .24f));
            foreach (var gull in gulls) gull.gameObject.SetActive(day);
            RenderSettings.skybox = sky; cameraView.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.fogColor = day ? new Color(.65f, .83f, .82f) : new Color(.055f, .14f, .23f);
            RenderSettings.fogStartDistance = day ? 180f : 125f;
            RenderSettings.fogEndDistance = day ? 540f : 400f;
            SetFinishWorld(day);
        }

        public void Refresh(float distance)
        {
            if (!policeStyled && game.Police != null)
            {
                BuildCarContact(game.Police.transform,true);
                policeStyled = true;
            }
            RefreshCarContacts(distance);
            RefreshCheckpointGantries(distance);
            foreach(var roller in rollers)
                roller.Root.localRotation=Quaternion.Euler(0,90,0)*Quaternion.AngleAxis(roller.Data.LateralOffset/.25f*Mathf.Rad2Deg,Vector3.right);
            water.SetFloat("_OceanTime", animationTime);
            sea.position = new Vector3(game.Player.position.x, SeaHeight, game.Player.position.z);
            foreach (var chunk in chunks)
            {
                bool visible = chunk.End > distance - 100f && chunk.At < distance + 580f
                    && (!chunk.Deck || route.HasSurface((chunk.At + chunk.End) * .5f, world))
                    && (!chunk.OnlyWorld.HasValue || chunk.OnlyWorld.Value == world
                        || (chunk.RecoveryFor != null && route.IsLandingRecovered(chunk.RecoveryFor)));
                if (chunk.Root.activeSelf != visible) chunk.Root.SetActive(visible);
            }
            for (int i = 0; i < gems.Count; i++)
            {
                var gem = gems[i]; bool taken = game.Collection.IsTaken(i);
                if (taken && !gem.Taken && Mathf.Abs(game.Collection.Items[i].x - distance) < 12f)
                {
                    gem.CollectedAt = animationTime;
                    audioSource.PlayOneShot(chime);
                }
                if (!taken && gem.Taken) gem.CollectedAt = -10f;
                gem.Taken = taken;
                float age = animationTime - gem.CollectedAt;
                gem.Root.gameObject.SetActive(game.Collection.Items[i].x > distance - 30f
                    && game.Collection.Items[i].x < distance + 260f && (!taken || age < .55f));
                gem.Stone.gameObject.SetActive(!taken); gem.Ring.gameObject.SetActive(!taken);
                gem.Burst.gameObject.SetActive(taken && age < .55f);
                gem.Burst.localScale = Vector3.one * (1f + Mathf.Max(0f, age) * 6f);
            }
        }

        private void Update()
        {
            if (Time.timeScale == 0f || game.State != ShiftRunState.Running) return;
            animationTime += Time.deltaTime;
            AnimateGulls();
            for (int i = 0; i < gems.Count; i++)
            {
                var gem = gems[i];
                if (!gem.Root.gameObject.activeSelf) continue;
                gem.Stone.localPosition = Vector3.up * (1.65f + Mathf.Sin(animationTime * 2.8f + i) * .16f);
                gem.Stone.localRotation = Quaternion.Euler(0f, animationTime * 55f, 0f);
            }
        }

        private void OnDestroy()
        {
            if (RenderSettings.skybox == sky) RenderSettings.skybox = null;
            foreach (var mesh in meshes) Release(mesh);
            foreach (var material in materials) Release(material);
            Release(chime);
            Release(checkpointChime);
        }
        private static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }

    }
}
