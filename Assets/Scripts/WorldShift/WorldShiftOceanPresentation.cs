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

        private void BuildCoast()
        {
            int index = 0;
            for (float at = 35f; at < game.RunLength + 120f; at += 78f)
            {
                var point = route.Sample(at);
                float side = index % 2 == 0 ? -1f : 1f;
                if (LandmarkReserve(at,side)) {index++;continue;}
                float offset = 44f + (index % 3) * 13f;
                Vector3 centre = point.Position + point.Right * side * offset;
                if (!ClearOfRoad(centre, 28f)) { index++; continue; }
                var g = new WorldShiftArtMesh();
                var foam = new WorldShiftArtMesh();
                float height = Mathf.Max(5f, point.Position.y - SeaHeight - 2f);
                Vector3 basePoint = new Vector3(centre.x, SeaHeight - 1f, centre.z);
                g.Rock(basePoint, new Vector3(19f, height, 27f), Stone, index);
                for (int edge = 0; edge < 9; edge++)
                {
                    float a = edge * Mathf.PI * 2f / 9f, b = (edge + 1) * Mathf.PI * 2f / 9f;
                    float r = .86f + .14f * Mathf.Sin(edge * 7.3f + index * 2.1f);
                    float r2 = .86f + .14f * Mathf.Sin(((edge + 1) % 9) * 7.3f + index * 2.1f);
                    Vector3 x = new Vector3(Mathf.Cos(a) * 19f * r, 0, Mathf.Sin(a) * 27f * r);
                    Vector3 y = new Vector3(Mathf.Cos(b) * 19f * r2, 0, Mathf.Sin(b) * 27f * r2);
                    Vector3 waterline = new Vector3(centre.x, SeaHeight + .035f, centre.z);
                    foam.Quad(waterline + x * .98f, waterline + y * .98f,
                        waterline + y * 1.06f, waterline + x * 1.06f, new Color(.49f, .83f, .79f));
                    // Sandy shelf sloping into a bright turquoise lagoon gives each island a shoreline.
                    Vector3 shelf = waterline + Vector3.up * .5f;
                    foam.Quad(shelf + x * .9f, shelf + y * .9f, waterline + y * 1.02f, waterline + x * 1.02f, Sand);
                    Vector3 lagoon = waterline - Vector3.up * .02f;
                    foam.Quad(lagoon + x * 1.06f, lagoon + y * 1.06f, lagoon + y * 1.4f, lagoon + x * 1.4f, new Color(.16f, .78f, .74f));
                    foam.Quad(lagoon + x * 1.4f, lagoon + y * 1.4f, lagoon + y * 1.65f, lagoon + x * 1.65f, new Color(.07f, .52f, .58f));
                }
                // Small boulders scattered on the beach line.
                for (int r = 0; r < 3; r++)
                {
                    float a = (index * 1.7f + r * 2.2f);
                    g.Rock(new Vector3(centre.x + Mathf.Cos(a) * 21f, SeaHeight - .4f, centre.z + Mathf.Sin(a) * 28f),
                        new Vector3(1.6f + r * .5f, 2.2f + r, 1.8f), Stone * (.9f + r * .05f), index + r * 5);
                }
                Vector3 top = basePoint + Vector3.up * height * .83f;
                g.Rock(top - Vector3.up * 1.8f, new Vector3(15f, 2.5f, 21f), Sand, index + 3);
                int section = 0;
                foreach (var relay in game.RelayLayout) if (at > relay.End) section++;
                if ((section == 3 && index % 3 == 0) || (section == 4 && index % 5 == 0)) Lighthouse(g, top, index);
                else if (section == 0)
                {
                    House(g, top + new Vector3(-4f, 0f, 0f), index);
                    House(g, top + new Vector3(3f, .5f, 4f), index + 1);
                    Palm(g, top + new Vector3(7f, 0, -4f), index);
                }
                else
                {
                    Palm(g, top + new Vector3(-5f, 0, -3f), index);
                    Palm(g, top + new Vector3(4f, 0, 3f), index + 2);
                    if (section == 1) g.Rock(basePoint + new Vector3(10f, 0, 9f), new Vector3(6f, height + 14f, 7f), Stone, index + 9);
                }
                var root = Render("Coastal island " + index, g, scenery);
                Render("Island surf", foam, scenery, root.transform);
                chunks.Add(new CoastChunk { At = at - 25f, End = at + 25f, Root = root });
                // White sail silhouettes and hulls give the open side of the coast a second scale.
                if (index % 3 == 0)
                {
                    var boat = new WorldShiftArtMesh();
                    Vector3 anchor = point.Position - point.Right * side * 80f;
                    anchor.y = SeaHeight + .35f;
                    boat.Rock(anchor, new Vector3(2f, 1f, 6f), Coral, index);
                    boat.Beam(anchor, anchor + Vector3.up * 12f, .12f, .12f, Cream);
                    boat.Triangle(anchor + new Vector3(.1f, 2f, 0), anchor + new Vector3(.1f, 11.5f, 0), anchor + new Vector3(5f, 2f, 0), Cream, true);
                    boat.Triangle(anchor + new Vector3(-.1f, 3f, 0), anchor + new Vector3(-.1f, 10f, 0), anchor + new Vector3(-3f, 3f, 0), Teal, true);
                    chunks.Add(new CoastChunk { At = at, End = at + 30, Root = Render("Sailing boat " + index, boat, scenery) });
                }
                else
                {
                    // Weathered sea stacks fill the open-water side between boats.
                    Vector3 stack = point.Position - point.Right * side * (58f + (index % 4) * 9f);
                    if (ClearOfRoad(stack, 8f))
                    {
                        var rocks = new WorldShiftArtMesh();
                        stack.y = SeaHeight - 1f;
                        rocks.Rock(stack, new Vector3(4f, 9f + index % 5 * 2f, 5f), Stone * .92f, index + 11);
                        rocks.Rock(stack + new Vector3(5f, 0, 3f), new Vector3(2.4f, 4.5f, 2.6f), Stone, index + 13);
                        rocks.Rock(stack + Vector3.up * (8f + index % 5 * 2f) * .82f, new Vector3(2.4f, .9f, 3f), Leaf, index);
                        chunks.Add(new CoastChunk { At = at, End = at + 30, Root = Render("Sea stack " + index, rocks, scenery) });
                    }
                }
                // Red/green channel buoys bob below the viaduct and frame the route.
                {
                    var buoy = new WorldShiftArtMesh(); var lamp = new WorldShiftArtMesh();
                    Vector3 b = point.Position + point.Right * -side * 15f; b.y = SeaHeight;
                    Color paint = side > 0 ? new Color(.82f, .17f, .14f) : new Color(.12f, .55f, .30f);
                    buoy.Cylinder(b - Vector3.up * .3f, 1f, .8f, 1.1f, paint, 8);
                    buoy.Cylinder(b + Vector3.up * .8f, .3f, .2f, 1.8f, Cream, 6);
                    lamp.Cylinder(b + Vector3.up * 2.6f, .32f, .32f, .4f, side > 0 ? new Color(1f, .35f, .25f) : new Color(.35f, 1f, .55f), 6);
                    var buoyRoot = Render("Channel buoy " + index, buoy, scenery);
                    Render("Buoy lamp", lamp, luminous, buoyRoot.transform);
                    if (ClearOfRoad(b, -10f)) chunks.Add(new CoastChunk { At = at - 10f, End = at + 10f, Root = buoyRoot });
                    else buoyRoot.SetActive(false);
                }
                index++;
            }
            // Taller beacons frame each jump from well outside the driving lanes.
            for (int i = 0; i < game.RelayLayout.Length; i++)
            {
                var jump = game.RelayLayout[i]; var p = route.Sample(jump.Ramp - 26f);
                var g = new WorldShiftArtMesh(); var glow = new WorldShiftArtMesh();
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 foot = p.Position + p.Right * side * 8.4f;
                    g.Beam(foot - Vector3.up * 2f, foot + Vector3.up * 7f, .65f, .65f, Cream);
                    glow.Beam(foot + Vector3.up * 3f, foot + Vector3.up * 6.5f, .72f, .72f, i % 2 == 0 ? new Color(1f, .75f, .27f) : new Color(.64f, .68f, 1f));
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 arrow = foot + Vector3.up * (1f + j * .7f);
                        g.Beam(arrow - p.Right * .45f - Vector3.up * .3f, arrow, .14f, .14f, Teal);
                        g.Beam(arrow, arrow + p.Right * .45f - Vector3.up * .3f, .14f, .14f, Teal);
                    }
                }
                var root = Render("Jump beacon pair " + (i + 1), g, scenery);
                Render("Jump beacon lenses", glow, luminous, root.transform);
                chunks.Add(new CoastChunk { At = jump.Ramp - 26f, End = jump.End, Root = root });
            }
        }

        private void BuildGulls()
        {
            // Simple flapping V silhouettes circling ahead of the player; purely decorative.
            for (int i = 0; i < 6; i++)
            {
                var root = new GameObject("Gull " + (i + 1)).transform;
                root.SetParent(transform, false);
                foreach (float side in new[] { -1f, 1f })
                {
                    var wing = new WorldShiftArtMesh();
                    wing.Triangle(Vector3.zero, new Vector3(side * 1.1f, .25f, -.25f), new Vector3(side * 1.1f, .25f, .3f), Cream, true);
                    wing.Triangle(new Vector3(side * 1.1f, .25f, -.25f), new Vector3(side * 1.9f, -.15f, -.1f), new Vector3(side * 1.1f, .25f, .3f), Cream * .9f, true);
                    Render(side < 0 ? "Left wing" : "Right wing", wing, scenery, root);
                }
                gulls.Add(root);
            }
        }

        private void AnimateGulls()
        {
            Vector3 anchor = game.Player.position + game.Player.forward * 70f;
            for (int i = 0; i < gulls.Count; i++)
            {
                float t = animationTime * (.35f + i * .04f) + i * 1.9f;
                float radius = 18f + i * 6f, side = i % 2 == 0 ? 1f : -1f;
                Vector3 p = anchor + new Vector3(Mathf.Cos(t) * radius * side, 16f + i * 2.5f + Mathf.Sin(t * 2f) * 1.5f, Mathf.Sin(t) * radius);
                Vector3 velocity = new Vector3(-Mathf.Sin(t) * side, 0, Mathf.Cos(t));
                gulls[i].SetPositionAndRotation(p, Quaternion.LookRotation(velocity) * Quaternion.Euler(0, 0, side * 18f));
                float flap = Mathf.Sin(animationTime * 7f + i) * 22f;
                gulls[i].GetChild(0).localRotation = Quaternion.Euler(0, 0, -flap);
                gulls[i].GetChild(1).localRotation = Quaternion.Euler(0, 0, flap);
            }
        }

        private static void House(WorldShiftArtMesh g, Vector3 p, int variant)
        {
            g.Box(p + Vector3.up * 2f, new Vector3(5f, 4f, 6f), Cream);
            g.Box(p + Vector3.up * 4.2f, new Vector3(5.8f, .5f, 6.8f), variant % 2 == 0 ? Coral : Teal);
            for (int i = -1; i <= 1; i += 2)
            {
                g.Box(p + new Vector3(i * 1.3f, 2.3f, -3.03f), new Vector3(.9f, 1.3f, .08f), Teal);
                g.Box(p + new Vector3(2.53f, 2.3f, i * 1.6f), new Vector3(.08f, 1.3f, 1f), Teal);
            }
            g.Box(p + new Vector3(0, 1.1f, -3.05f), new Vector3(1f, 2.2f, .08f), Stone);
        }

        private static void Lighthouse(WorldShiftArtMesh g, Vector3 p, int seed)
        {
            for (int j = 0; j < 5; j++)
                g.Cylinder(p + Vector3.up * j * 3.2f, 2.4f - j * .22f, 2.18f - j * .22f, 3.2f, j % 2 == 0 ? Cream : Coral, 10);
            g.Cylinder(p + Vector3.up * 16f, 2.8f, 2.8f, .45f, Stone, 10);
            g.Cylinder(p + Vector3.up * 16.45f, 1.5f, 1.5f, 2f, new Color(.65f, .94f, .88f), 10);
            g.Cylinder(p + Vector3.up * 18.45f, 2.4f, 0f, 2f, Coral, 10);
        }

        private static void Palm(WorldShiftArtMesh g, Vector3 p, int seed)
        {
            float height = 7f + seed % 4;
            Vector3 crown = p + new Vector3(1.3f, height, .6f);
            g.Beam(p, crown, .45f, .45f, new Color(.42f, .30f, .19f));
            for (int i = 0; i < 7; i++)
            {
                float angle = (i * 360f / 7f + seed * 27f) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 across = Vector3.Cross(Vector3.up, d) * .8f;
                Vector3 middle = crown + d * 2.5f + Vector3.up * .65f;
                Vector3 tip = crown + d * 5f - Vector3.up * 1.5f;
                Color leaf = Color.Lerp(Leaf, new Color(.40f, .66f, .27f), (i % 3) * .22f);
                g.Triangle(crown, middle + across, middle, leaf, true);
                g.Triangle(crown, middle, middle - across, leaf * .85f, true);
                g.Triangle(middle + across, tip, middle, leaf, true);
                g.Triangle(middle, tip, middle - across, leaf * .85f, true);
            }
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

        /// <summary>Coastal hazard skins fitted to the existing road-aligned collision box.</summary>
        public Transform BuildObstacle(Transform parent, Vector3 centre, Vector3 size,
            WorldShiftObstacleVisual.Palette palette, ShiftHazard hazard, int variation)
        {
            if (!hazard.LateralMotion && size.x < 6f && variation % 2 == 0)
            {
                var van=WorldShiftRoadVehicleArt.Build(parent,centre,size,palette,ShiftLevel.Ocean);
                van.name="Parked coastal service van";
                Contact(van.transform,new Vector3(0,-.9f+.025f,0),new Vector2(3.05f,5.5f),contact);
                return van.transform;
            }
            var root = new GameObject(hazard.LateralMotion ? "Harbour cargo trolley"
                : size.x > 6f ? (variation % 2 == 0 ? "Shipping container" : "Harbour wave barricade")
                : "Fishing cargo stack").transform;
            root.SetParent(parent, false); root.localPosition = centre;
            var body = new WorldShiftArtMesh(); var markings = new WorldShiftArtMesh(); var fittings = new WorldShiftArtMesh();
            Vector3 h = size * .5f;
            float floor = -h.y;
            Color ink = new Color(.065f, .12f, .17f);
            Color metal = new Color(.40f, .50f, .52f);
            // The solid base marks every edge of the collision footprint. All fittings stay inside it.
            if (!hazard.LateralMotion)
                fittings.ChamferBox(new Vector3(0, floor + .10f, 0), new Vector3(size.x, .20f, size.z), .04f, ink);
            if (hazard.LateralMotion)
            {
                float wheelY = floor + .26f;
                foreach (float side in new[] {-1f, 1f})
                    foreach (float end in new[] {-1f, 1f})
                    {
                        var wheel = new WorldShiftArtMesh();
                        wheel.Wheel(Vector3.zero,.25f,.20f,ink);
                        wheel.Wheel(Vector3.right*end*.11f,.15f,.025f,metal);
                        for(int spoke=0;spoke<5;spoke++)
                        {
                            float a=spoke*Mathf.PI*.4f;
                            wheel.Beam(Vector3.right*end*.125f,new Vector3(end*.125f,Mathf.Cos(a)*.14f,Mathf.Sin(a)*.14f),.035f,.035f,Cream);
                        }
                        var roller=Render("Trolley drive roller",wheel,scenery,root).transform;
                        roller.localPosition=new Vector3(side*(h.x-.31f),wheelY,end*(h.z-.12f));
                        roller.localRotation=Quaternion.Euler(0,90,0);
                        rollers.Add(new DriveRoller {Root=roller,Data=hazard});
                    }
                fittings.ChamferBox(new Vector3(0,floor+.40f,0),new Vector3(size.x,.20f,size.z),.045f,metal);
                body.ChamferBox(new Vector3(0, floor+.57f, 0), new Vector3(size.x,.14f,size.z),.04f,Color.white);
                for (int box=0; box<2; box++)
                {
                    float z = -h.z+.90f+box*1.65f;
                    body.ChamferBox(new Vector3(0,floor+1.15f,z), new Vector3(size.x*.85f,1.02f,1.40f),.065f,Color.white);
                    fittings.Box(new Vector3(0,floor+1.67f,z), new Vector3(.10f,.035f,1.40f),ink);
                    foreach(float side in new[] {-1f,1f})
                    {
                        fittings.Box(new Vector3(side*h.x*.86f,floor+1.15f,z),new Vector3(.025f,1.02f,.10f),ink);
                        fittings.ChamferBox(new Vector3(side*.37f,floor+1.25f,z-.713f),new Vector3(.26f,.22f,.025f),.02f,metal);
                    }
                }
                // Visible electric drive, cooling grille and amber warning lenses explain the shuttle motion.
                float motorZ=h.z-.72f;
                body.ChamferBox(new Vector3(0,floor+1.0f,motorZ),new Vector3(size.x*.85f,.72f,1.22f),.10f,Color.white);
                fittings.Box(new Vector3(0,floor+1.0f,motorZ+.62f),new Vector3(size.x*.65f,.43f,.025f),ink);
                for(int i=0;i<5;i++)
                    fittings.Box(new Vector3(0,floor+.84f+i*.08f,motorZ+.64f),new Vector3(size.x*.58f,.025f,.02f),metal);
                foreach(float side in new[] {-1f,1f})
                {
                    fittings.Cylinder(new Vector3(side*.43f,floor+1.36f,motorZ),.10f,.08f,.14f,new Color(1f,.53f,.13f),12);
                    fittings.Box(new Vector3(side*(h.x-.03f),floor+.7f,0),new Vector3(.035f,.055f,size.z*.87f),Cream);
                }
                // The two sideways arrowheads remain readable when the trolley freezes in Night.
                foreach (float end in new[] {-1f,1f})
                {
                    float z=end*(h.z-.03f);
                    body.ChamferBox(new Vector3(0,floor+1.10f,z),new Vector3(size.x*.92f,.46f,.06f),.02f,Color.white);
                    foreach (float side in new[] {-1f,1f})
                    {
                        Vector3 tip=new Vector3(side*size.x*.32f,floor+1.10f,z);
                        markings.Beam(tip+new Vector3(-side*.23f,.19f,0),tip,.09f,.05f,Color.white);
                        markings.Beam(tip,tip+new Vector3(-side*.23f,-.19f,0),.09f,.05f,Color.white);
                    }
                }
            }
            else if (size.x > 6f && variation % 2 == 0)
            {
                body.ChamferBox(Vector3.zero,new Vector3(size.x-.10f,size.y-.08f,size.z-.10f),.08f,Color.white);
                // Corrugation wraps the roof and exposed faces, with locking bars on the rear doors.
                for (float x=-h.x+.25f; x<h.x-.1f; x+=.46f)
                {
                    body.Box(new Vector3(x,h.y-.025f,0),new Vector3(.10f,.05f,size.z-.12f),Color.white);
                    fittings.Box(new Vector3(x+.12f,h.y+.008f,0),new Vector3(.035f,.016f,size.z-.12f),new Color(.17f,.24f,.25f));
                    foreach (float face in new[] {-1f,1f})
                    {
                        body.Box(new Vector3(x,0,face*(h.z-.025f)),new Vector3(.10f,size.y-.12f,.05f),Color.white);
                        fittings.Box(new Vector3(x+.12f,0,face*(h.z+.005f)),new Vector3(.045f,size.y-.14f,.025f),new Color(.17f,.24f,.25f));
                    }
                }
                foreach (float face in new[] {-1f,1f})
                {
                    float z=face*(h.z-.015f);
                    fittings.Box(new Vector3(0,0,z),new Vector3(.055f,size.y-.12f,.025f),new Color(.17f,.24f,.25f));
                    foreach(float side in new[] {-1f,1f})
                    {
                        fittings.Box(new Vector3(side*h.x*.57f,0,z),new Vector3(.055f,size.y-.22f,.025f),metal);
                        fittings.Box(new Vector3(side*h.x*.57f,-.12f,z),new Vector3(.28f,.07f,.03f),metal);
                        fittings.Box(new Vector3(side*(h.x-.18f),floor+.35f,z),new Vector3(.20f,.40f,.03f),new Color(.17f,.24f,.25f));
                    }
                }
            }
            else if (size.x > 6f)
            {
                // Solid marine concrete profile: wide foot, tapered shoulders, narrow top.
                Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
                foreach(float end in new[] {-1f,1f})
                {
                    float z=end*h.z;
                    // Each end is a filled polygon, rather than a see-through fence over a solid hitbox.
                    Vector3[] outline={P(-h.x,floor,z),P(-h.x,-.30f,z),P(-h.x+.38f,h.y,z),
                        P(h.x-.38f,h.y,z),P(h.x,-.30f,z),P(h.x,floor,z)};
                    for(int j=1;j<outline.Length-1;j++)
                        if (end < 0f) body.Triangle(outline[0],outline[j],outline[j+1],Color.white);
                        else body.Triangle(outline[0],outline[j+1],outline[j],Color.white);
                }
                body.Quad(P(-h.x+.38f,h.y,-h.z),P(-h.x+.38f,h.y,h.z),P(h.x-.38f,h.y,h.z),P(h.x-.38f,h.y,-h.z),Color.white);
                body.Quad(P(-h.x,floor,h.z),P(-h.x,-.30f,h.z),P(-h.x,-.30f,-h.z),P(-h.x,floor,-h.z),Color.white);
                body.Quad(P(h.x,floor,-h.z),P(h.x,-.30f,-h.z),P(h.x,-.30f,h.z),P(h.x,floor,h.z),Color.white);
                body.Quad(P(-h.x,-.30f,h.z),P(-h.x+.38f,h.y,h.z),P(-h.x+.38f,h.y,-h.z),P(-h.x,-.30f,-h.z),Color.white);
                body.Quad(P(h.x,-.30f,-h.z),P(h.x-.38f,h.y,-h.z),P(h.x-.38f,h.y,h.z),P(h.x,-.30f,h.z),Color.white);
                foreach(float end in new[] {-1f,1f})
                {
                    float z=end*(h.z+.001f);
                    for(float x=-h.x+.45f; x<h.x-.8f; x+=1.12f)
                        if (end < 0f)
                            markings.Quad(P(x,-.20f,z),P(x+.46f,.68f,z),P(x+.91f,.68f,z),P(x+.45f,-.20f,z),Color.white);
                        else
                            markings.Quad(P(x+.45f,-.20f,z),P(x+.91f,.68f,z),P(x+.46f,.68f,z),P(x,-.20f,z),Color.white);
                }
                // Recessed mooring-ring shapes give the road closures a harbour identity.
                foreach(float x in new[] {-size.x*.32f,0f,size.x*.32f})
                {
                    fittings.Cylinder(new Vector3(x,h.y-.12f,0),.20f,.20f,.12f,metal,10);
                    fittings.RopeRing(new Vector3(x,h.y-.015f,0),.29f,.045f,metal);
                }
            }
            else
            {
                // Stacked fish crates with slatted faces, strapped down on a broad timber pallet.
                fittings.Box(new Vector3(0,floor+.23f,0),new Vector3(size.x,.16f,size.z),new Color(.47f,.32f,.19f));
                for(int row=0;row<3;row++)
                {
                    float z=Mathf.Lerp(-h.z+.9f,h.z-.9f,row*.5f);
                    body.ChamferBox(new Vector3(0,floor+.85f,z),new Vector3(size.x-.08f,1.15f,1.7f),.06f,Color.white);
                    for(int slat=0;slat<3;slat++)
                        foreach(float end in new[] {-1f,1f})
                            fittings.Box(new Vector3(0,floor+.58f+slat*.26f,z+end*.855f),new Vector3(size.x*.82f,.055f,.025f),ink);
                    foreach(float side in new[] {-1f,1f})
                        fittings.Box(new Vector3(side*(h.x-.12f),floor+.87f,z),new Vector3(.07f,1.16f,1.72f),ink);
                    fittings.RopeRing(new Vector3(0,floor+1.45f,z),Mathf.Min(.48f,h.x*.62f),.08f,new Color(.78f,.68f,.43f));
                }
                // A fishing float tops the load without exceeding its collision height.
                body.Cylinder(new Vector3(0,h.y-.30f,0),.22f,.08f,.30f,Color.white,10);
            }
            var painted = new GameObject("World-coloured coastal hazard").transform;
            painted.SetParent(root,false);
            AddClosureHardware(fittings,markings,size,hazard.LateralMotion);
            Material bodyFinish=hazard.LateralMotion ? paintedMetal : size.x > 6f
                ? variation % 2 == 0 ? paintedMetal : concrete : timber;
            Render("Weathered coastal hazard body",body,bodyFinish,root);
            Render("Obstacle block",markings,luminous,painted);
            palette.Add(painted);
            Render("Marine fittings",fittings,metalFinish,root);
            Contact(root,new Vector3(0,-size.y*.5f+.025f,0),new Vector2(size.x*.99f,size.z*.99f),contact);

            return root;
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
