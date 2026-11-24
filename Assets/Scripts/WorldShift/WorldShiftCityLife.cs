using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    // Street life is scenery, with no hazard registration, physics or road crossings.
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private enum StreetActivity { Walk, Chat, Scuffle }
        private sealed class Citizen
        {
            public Transform Root, Body, LeftArm, RightArm, LeftLeg, RightLeg;
            public float At, Side, Phase, Facing, Lateral;
            public StreetActivity Activity;
        }
        private sealed class Conversation
        {
            public Transform Root;
            public TMP_Text Text;
            public float At, Lateral;
            public int Script;
        }
        private readonly List<Citizen> citizens = new List<Citizen>();
        private readonly List<Conversation> conversations = new List<Conversation>();
        private readonly List<Mesh[]> citizenStyles = new List<Mesh[]>();
        private readonly List<Chunk> cityFurniture = new List<Chunk>();
        private Mesh speechPlate;
        private float streetClock;
        private static readonly string[][] StreetBanter =
        {
            new[] { "That's my taxi!", "Then wave faster!" },
            new[] { "Coffee?", "Make it two." },
            new[] { "You hear that siren?", "There goes the peace." },
            new[] { "Watch the jacket!", "You started it!" },
            new[] { "Bus late again?", "Every single day." }
        };

        private void BuildCityLife()
        {
            foreach (var chunk in chunks)
                if (chunk.Root.name == "City pavement furniture" || chunk.Root.name == "City roadside streetlight") cityFurniture.Add(chunk);
            BuildCitizenStyles();
            BuildCityAnimalMeshes();
            var plate = new WorldShiftArtMesh();
            plate.ChamferBox(Vector3.zero, new Vector3(3.9f, .83f, .045f), .12f, new Color(.96f, .93f, .82f));
            plate.Triangle(new Vector3(-.25f,-.4f,-.03f),new Vector3(0,-.68f,-.03f),
                new Vector3(.18f,-.4f,-.03f),new Color(.96f,.93f,.82f),true);
            speechPlate = plate.Mesh("City speech bubble"); meshes.Add(speechPlate);
            int index = 0;
            var random = new System.Random(526);
            foreach (float side in new[] { -1f, 1f })
                for (float along = 24f+(float)random.NextDouble()*12f; along < route.Course.Length-30f;
                    along += 18f+(float)random.NextDouble()*20f)
                {
                    float offset = 1.2f+(float)random.NextDouble()*1.4f;
                    if (!FootpathClear(along, side, offset)) continue;
                    int n = index++;
                    StreetActivity activity = n % 11 == 5 ? StreetActivity.Scuffle
                        : n % 3 == 0 ? StreetActivity.Chat : StreetActivity.Walk;
                    var citizen = AddCitizen(along, side, n, activity, 1f, offset);
                    if (activity == StreetActivity.Walk && n % 5 == 0) AddCityDog(citizen, n);
                    if (activity == StreetActivity.Walk) continue;
                    AddCitizen(along + 1.15f, side, n + 3, activity, -1f, offset);
                    int script = n % 4;
                    AddConversation(along + .575f, side, activity == StreetActivity.Scuffle ? 3 : script == 3 ? 4 : script, offset);
                }
            BuildScatteredCityCrowds();
            BuildCityCrows();
            ResetCityLife();
        }

        // Validate the complete walking envelope against every road arm and nearby furniture.
        // Both footpath lanes leave the kerb clear, including the dogs' following distance.
        private bool FootpathClear(float at, float side, float offset, float travel = 4.5f)
        {
            // The ramp footpaths rise with the route. Populate those slopes and the
            // landing footpaths, stopping before the unsupported airborne span.
            foreach (var jump in route.Course.Ramps)
                if (at+travel >= jump.Takeoff-1f && at-travel <= jump.Landing+1f) return false;
            float lateral = side * (WorldShiftLevel.RoadWidth * .5f + offset);
            float clearance = WorldShiftLevel.RoadWidth * .5f + .8f;
            for (float along = -travel; along <= travel; along += .5f)
            {
                var p = route.Sample(at + along);
                Vector3 centre = p.Position + p.Right * lateral;
                foreach (var road in roadPoints)
                    if (new Vector2(centre.x-road.x,centre.z-road.z).sqrMagnitude < clearance*clearance) return false;
                foreach (var chunk in cityFurniture)
                {
                    if (travel < 1f && chunk.Root.name == "City pavement furniture")
                    {
                        Vector3 local = chunk.Root.transform.InverseTransformPoint(centre);
                        if (Mathf.Abs(local.x)<1.35f && Mathf.Abs(local.z)<2.4f
                            || Mathf.Abs(local.x+side*2.2f)<1.65f && Mathf.Abs(local.z-4.5f)<.95f) return false;
                        continue;
                    }
                    float radius = chunk.Root.name == "City pavement furniture" ? 6.5f : 1.25f;
                    if ((chunk.Root.transform.position-centre).sqrMagnitude < radius*radius) return false;
                }
            }
            return true;
        }

        private void BuildCitizenStyles()
        {
            Color[] coats = { new Color(.24f,.40f,.48f),new Color(.54f,.26f,.31f),
                new Color(.34f,.43f,.31f),new Color(.62f,.53f,.39f),new Color(.40f,.34f,.52f),new Color(.30f,.33f,.38f) };
            Color[] skins = { new Color(.72f,.47f,.32f),new Color(.40f,.25f,.18f),new Color(.91f,.69f,.51f) };
            for (int n = 0; n < coats.Length; n++)
            {
                var body = new WorldShiftArtMesh(); var arm = new WorldShiftArtMesh(); var leg = new WorldShiftArtMesh();
                Color skin = skins[n%skins.Length], shoes = new Color(.12f,.13f,.15f);
                body.ChamferBox(new Vector3(0,1.12f,0),new Vector3(.48f,.62f,.30f),.06f,coats[n]);
                body.Cylinder(new Vector3(0,1.43f,0),.085f,.085f,.12f,skin,6);
                body.ChamferBox(new Vector3(0,1.72f,0),new Vector3(.33f,.38f,.32f),.07f,skin);
                body.Box(new Vector3(0,1.91f,-.025f),new Vector3(.35f,.10f,.32f),shoes);
                body.Box(new Vector3(0,1.75f,-.145f),new Vector3(.34f,.28f,.06f),shoes);
                foreach (float x in new[] { -.075f, .075f })
                    body.Box(new Vector3(x,1.75f,.166f),new Vector3(.035f,.035f,.018f),shoes);
                if (n%2 == 0) // A shoulder bag, kept outside the arm swing.
                {
                    body.Beam(new Vector3(-.22f,1.43f,-.17f),new Vector3(.22f,.9f,-.17f),.045f,.035f,shoes);
                    body.Box(new Vector3(.16f,.89f,-.23f),new Vector3(.32f,.29f,.15f),new Color(.33f,.24f,.18f));
                }
                arm.Box(new Vector3(0,-.19f,0),new Vector3(.16f,.39f,.18f),coats[n]);
                arm.ChamferBox(new Vector3(0,-.46f,0),new Vector3(.15f,.17f,.16f),.035f,skin);
                leg.Box(new Vector3(0,-.31f,0),new Vector3(.18f,.63f,.21f),new Color(.20f,.24f,.29f));
                leg.Box(new Vector3(0,-.65f,.055f),new Vector3(.21f,.13f,.34f),shoes);
                var style = new[] { body.Mesh("Citizen coat and head"),arm.Mesh("Citizen sleeve and hand"),leg.Mesh("Citizen trousers and shoe") };
                citizenStyles.Add(style); meshes.AddRange(style);
            }
        }

        private Transform StreetPart(Transform parent, string label, Mesh mesh, Vector3 position)
        {
            var part = new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer)).transform;
            part.SetParent(parent,false); part.localPosition = position;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = surface;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return part;
        }

        private Citizen AddCitizen(float at, float side, int index, StreetActivity activity, float facing, float offset)
        {
            float lateral = side * (WorldShiftLevel.RoadWidth*.5f+offset);
            var root = Root("City citizen - " + activity,at,lateral);
            var style = citizenStyles[index%citizenStyles.Count];
            var body = new GameObject("Animated figure").transform; body.SetParent(root,false);
            float size = .94f + index%4*.035f; body.localScale = Vector3.one*size;
            StreetPart(body,"Coat and head",style[0],Vector3.zero);
            var citizen = new Citizen
            {
                Root=root,Body=body,At=at,Side=side,Phase=index*1.79f,Facing=facing,
                Activity=activity,Lateral=lateral,
                LeftArm=StreetPart(body,"Left arm",style[1],new Vector3(-.32f,1.37f,0)),
                RightArm=StreetPart(body,"Right arm",style[1],new Vector3(.32f,1.37f,0)),
                LeftLeg=StreetPart(body,"Left leg",style[2],new Vector3(-.13f,.76f,0)),
                RightLeg=StreetPart(body,"Right leg",style[2],new Vector3(.13f,.76f,0))
            };
            citizens.Add(citizen);
            return citizen;
        }

        private void AddConversation(float at, float side, int script, float offset)
        {
            // Visibility is owned by the conversation budget, not the scenery chunk pass.
            var root = new GameObject("City banter").transform;
            root.SetParent(transform,false);
            var bubble = StreetPart(root,"Speech bubble",speechPlate,Vector3.zero);
            bubble.GetComponent<MeshRenderer>().sharedMaterial = windows;
            var textRoot = new GameObject("Street conversation",typeof(TextMeshPro));
            textRoot.transform.SetParent(root,false); textRoot.transform.localPosition = new Vector3(0,0,-.05f);
            var text = textRoot.GetComponent<TextMeshPro>();
            text.font = WorldShiftTypography.Body; text.fontSize = 3.1f;
            text.color = new Color(.12f,.16f,.20f); text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(3.65f,.72f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            conversations.Add(new Conversation { Root=root,Text=text,At=at,Lateral=side*(WorldShiftLevel.RoadWidth*.5f+offset),Script=script });
            root.gameObject.SetActive(false);
        }

        public void ResetCityLife()
        {
            streetClock=0f;
            foreach (var conversation in conversations) conversation.Root.gameObject.SetActive(false);
            StepCityLife(0f,null);
        }

        public void StepCityLife(float delta, Camera camera)
        {
            streetClock += Mathf.Max(0f,delta);
            RefreshCityTrafficLights();
            StepTrainingLife();
            foreach (var citizen in citizens)
            {
                if (!citizen.Root.gameObject.activeSelf) continue;
                bool walking = citizen.Activity == StreetActivity.Walk;
                bool fighting = citizen.Activity == StreetActivity.Scuffle;
                float phase = streetClock*(fighting ? 4.5f : 3.8f)+citizen.Phase;
                // Ping-pong on a short, pre-cleared strip; no crossing or drifting into the road.
                float travel = Mathf.PingPong(streetClock*.95f+citizen.Phase,5f);
                float direction = Mathf.Repeat(streetClock*.95f+citizen.Phase,10f)<5f ? 1f : -1f;
                float at = citizen.At+(walking ? travel-2.5f : 0f);
                var p = route.Sample(at);
                Vector3 forward = p.Forward*(walking ? direction : citizen.Facing);
                citizen.Root.SetPositionAndRotation(p.Position+p.Right*citizen.Lateral+p.Up*.035f,
                    Quaternion.LookRotation(forward,p.Up));
                float stride = walking ? Mathf.Sin(phase)*27f : 0f;
                citizen.LeftLeg.localRotation=Quaternion.Euler(stride,0,0);
                citizen.RightLeg.localRotation=Quaternion.Euler(-stride,0,0);
                citizen.LeftArm.localRotation=Quaternion.Euler(walking ? -stride : fighting ? -65f-25f*Mathf.Sin(phase) : -25f-20f*Mathf.Sin(phase),0,-8f);
                citizen.RightArm.localRotation=Quaternion.Euler(walking ? stride : fighting ? -65f+25f*Mathf.Sin(phase) : -12f+18f*Mathf.Sin(phase+.8f),0,8f);
                citizen.Body.localPosition=Vector3.up*(walking ? .025f*Mathf.Abs(Mathf.Sin(phase)) : 0f);
                citizen.Body.localRotation=Quaternion.Euler(fighting ? 8f : 0f,fighting ? Mathf.Sin(phase)*12f : Mathf.Sin(phase*.5f)*4f,0);
            }
            StepCityAnimals();
            // At most one nearby exchange is visible, above the pavement, never the driving HUD.
            Conversation nearest=null;
            foreach (var conversation in conversations)
                if (conversation.At>distance-4f && conversation.At<distance+40f
                    && (nearest==null || Mathf.Abs(conversation.At-distance)<Mathf.Abs(nearest.At-distance))) nearest=conversation;
            foreach (var conversation in conversations)
            {
                bool visible = camera!=null && conversation==nearest && Mathf.Repeat(streetClock,5.6f)<4.8f;
                conversation.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                var p=route.Sample(conversation.At);
                conversation.Root.position=p.Position+p.Right*conversation.Lateral+p.Up*3.05f;
                conversation.Root.rotation=camera.transform.rotation;
                int line=(int)(Mathf.Repeat(streetClock,5.6f)/2.4f)%2;
                string phrase=StreetBanter[conversation.Script][line];
                if (conversation.Text.text!=phrase) conversation.Text.text=phrase;
            }
        }
    }
}
