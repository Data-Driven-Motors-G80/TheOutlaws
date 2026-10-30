using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>Shared escape pickups, extraction gate and concise UI for both environments.</summary>
    public sealed class WorldShiftGetaway : MonoBehaviour
    {
        public const float ExitTail = 400f;
        public const float ContinueBoundary = 1.8f;
        private sealed class Loot
        {
            public float At, X;
            public ShiftWorld World;
            public bool Taken, Bonus;
            public Transform Visual;
        }
        private WorldShiftCity city;
        private DesertShiftTrial desert;
        private readonly List<Loot> loot = new List<Loot>();
        private readonly List<Material> materials = new List<Material>();
        private TMP_Text haul, world, stage, guidance, fuelText, resultTitle, resultValue, resultNote;
        private TMP_Text pauseNote, choiceLeft, choiceRight;
        private GameObject promptPanel;
        private Image mirrorBorder;
        private RectTransform fuelFill, progressFill;
        private Image choiceLeftPanel, choiceRightPanel;
        private GameObject result, pause, choices, mirrorPanel, drivingHud;
        private RawImage mirror;
        private bool continuePending, openingGoalDismissed;
        private float environmentEnd;
        private float lootFeedbackUntil = float.NegativeInfinity;
        private int currentLeg;
        private static readonly Color Gold = new Color(1f, .77f, .25f);
        private static readonly Color Cyan = new Color(.3f, .85f, 1f);
        private static readonly Color Green = new Color(.4f, 1f, .68f);
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .96f);
        public bool InRecovery => Distance >= environmentEnd + 20f;
        public bool InExit => Distance >= environmentEnd + 180f;
        public WorldShiftGetawayExtras Extras { get; private set; }
        private bool IsCity => city != null;
        internal float Distance => IsCity ? city.Distance : desert.Distance;
        private float Lateral => IsCity ? city.Lateral : desert.Lateral;
        internal float Duration => IsCity ? city.Duration : desert.Duration;
        private float Length => environmentEnd + ExitTail;
        private float Fuel => IsCity ? city.FuelFraction : desert.FuelFraction;
        private float Gap => IsCity ? city.PursuitGap : desert.PursuitGap;
        internal ShiftWorld World => IsCity ? city.World : desert.World;
        private ShiftRunState State => IsCity ? city.State : desert.State;
        private WorldShiftRoute Route => IsCity ? city.Route : desert.Route;
        private RearViewCamera RearView => IsCity ? city.RearView : desert.RearView;

        public void Bind(WorldShiftCity controller) { city = controller; Initialize(); }
        public void Bind(DesertShiftTrial controller) { desert = controller; Initialize(); }

        private void Initialize()
        {
            currentLeg = WorldShiftFlow.Escape.Leg;
            environmentEnd = IsCity ? WorldShiftLevel.Length : DesertShiftLevel.Length;
            Material gold = MakeMaterial(Gold), handle = MakeMaterial(new Color(.12f, .1f, .04f));
            // Functional primitive markers only: no glow, decorative art or audio.
            foreach (var material in new[] { gold, handle })
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            // The opening trail teaches collection without a separate instructions screen.
            AddLoot(35f, 0f, ShiftWorld.Day, gold, handle);
            AddLoot(55f, 0f, ShiftWorld.Day, gold, handle);
            AddLoot(75f, 0f, ShiftWorld.Day, gold, handle);
            if (IsCity)
            {
                foreach (var pickup in WorldShiftLevel.FuelPickups)
                {
                    // Offer a separate lane choice; never hide a fuel pickup inside a cash box.
                    if (pickup.Distance > environmentEnd - 100f) continue;
                    AddLoot(pickup.Distance, pickup.Lane == 0 ? -3.2f : 0f, pickup.World, gold, handle);
                }
            }
            else
            {
                // Alternate worlds on safe central stretches.
                for (int i = 0; i < 10; i++)
                    AddLoot(desert.FuelDistance(i), -3.2f, i % 2 == 0 ? ShiftWorld.Day : ShiftWorld.Echo, gold, handle);
            }
            Extras = gameObject.AddComponent<WorldShiftGetawayExtras>();
            Extras.Bind(this, Route, IsCity ? city.Player : desert.Player, environmentEnd, MakeMaterial);
            foreach (float at in new[] { Extras.Jump.Start + 8f, Extras.Jump.Start + 22f, Extras.Jump.Takeoff + 8f,
                Extras.Jump.Takeoff + 24f, Extras.Jump.Takeoff + 42f, Extras.Jump.Landing - 8f })
                AddLoot(at, WorldShiftBonusJump.Lane, ShiftWorld.Echo, gold, handle, Extras.Jump.HeightAt(at), true);
            AddLoot(Extras.Jump.Start + 16f, -3.2f, ShiftWorld.Day, gold, handle);
            AddLoot(Extras.Jump.Start + 60f, -3.2f, ShiftWorld.Day, gold, handle);
            // A visible Night trail makes the magnet choice useful immediately.
            foreach (float at in new[] { 65f, 90f, 115f, 140f })
                AddLoot(at, -3.2f, ShiftWorld.Echo, gold, handle);
            BuildExit();
            BuildHud();
            Present(World);
        }

        private Material MakeMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * .6f);
            materials.Add(material);
            return material;
        }

        private void AddLoot(float at, float x, ShiftWorld owner, Material gold, Material handle, float rise = 0f, bool bonus = false)
        {
            RoadPathPoint point = Route.Sample(at);
            var root = new GameObject("Loot " + owner + " " + at).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(point.Position + point.Right * x + Vector3.up * (.15f + rise), Quaternion.LookRotation(point.Forward));
            // A low, chunky cash box differs from the tall road barriers. The symbol belongs on its face.
            Box(root, new Vector3(0f, .6f, 0f), new Vector3(1.7f, 1f, 1.05f), gold);
            Box(root, new Vector3(0f, 1.15f, 0f), new Vector3(1.8f, .16f, 1.12f), gold);
            Box(root, new Vector3(-.28f, 1.36f, 0f), new Vector3(.13f, .3f, .16f), handle);
            Box(root, new Vector3(.28f, 1.36f, 0f), new Vector3(.13f, .3f, .16f), handle);
            Box(root, new Vector3(0f, 1.51f, 0f), new Vector3(.69f, .13f, .16f), handle);
            Box(root, new Vector3(0f, .62f, -.54f), new Vector3(1.12f, .78f, .04f), handle);
            WorldLabel(root, "$", new Vector3(0f, .62f, -.566f), Gold, 10f);
            var symbol = root.GetComponentInChildren<TextMeshPro>();
            symbol.name = "Loot dollar symbol";
            symbol.rectTransform.sizeDelta = new Vector2(1.05f, .76f);
            symbol.fontStyle = FontStyles.Bold;
            loot.Add(new Loot { At = at, X = x, World = owner, Visual = root, Bonus = bonus });
        }

        public void Collect(float from, float to, float fromX, float toX)
        {
            if (State != ShiftRunState.Running) return;
            Extras.Collect(from, to, fromX, toX);
            foreach (var item in loot)
            {
                if (item.Bonus && !Extras.Jump.Traversing) continue;
                if (item.Taken || to < item.At - 1.3f || from > item.At + 1.3f) continue;
                if (item.World != World) continue;
                float entry = Mathf.InverseLerp(from, to, item.At - 1.3f);
                float exit = Mathf.InverseLerp(from, to, item.At + 1.3f);
                float a = Mathf.Lerp(fromX, toX, entry), b = Mathf.Lerp(fromX, toX, exit);
                float reach = Extras.MagnetActive ? 7f : 1.35f;
                if (Mathf.Max(a, b) < item.X - reach || Mathf.Min(a, b) > item.X + reach) continue;
                item.Taken = true;
                item.Visual.gameObject.SetActive(false);
                if (WorldShiftFlow.Escape.Collect(100) > 0) lootFeedbackUntil = Duration + .35f;
            }
        }

        // Called only when simulation crosses the finish gate, never by UI refresh/rendering.
        public void Finish(float lateral)
        {
            if (WorldShiftFlow.Escape.Status != GetawayRunState.RunStatus.Running || continuePending) return;
            if (lateral >= ContinueBoundary) continuePending = true;
            else WorldShiftFlow.FinishGetawayLeg(false);
        }

        public void Lose(string reason) => WorldShiftFlow.Escape.Lose(reason);

        public void Present(ShiftWorld displayedWorld)
        {
            Extras?.Present(displayedWorld);
            foreach (var item in loot)
            {
                bool visible = !item.Taken && item.World == displayedWorld && item.At > Distance - 10f && item.At < Distance + 180f;
                item.Visual.gameObject.SetActive(visible);
                if (visible)
                {
                    var point = Route.Sample(item.At);
                    float pull = Extras.MagnetActive && (!item.Bonus || Extras.Jump.Traversing)
                        ? Mathf.Clamp01(1f - (item.At - Distance) / 18f) : 0f;
                    float x = Mathf.Lerp(item.X, Lateral, pull);
                    item.Visual.position = point.Position + point.Right * x + Vector3.up * (.15f + (item.Bonus ? Extras.Jump.HeightAt(item.At) : 0f));
                }
            }
        }

        private void BuildExit()
        {
            Material safe = MakeMaterial(Green), risk = MakeMaterial(Gold);
            for (float at = environmentEnd + 200f; at < Length; at += 18f)
            {
                RoadPathPoint point = Route.Sample(at);
                var root = new GameObject("Extraction lane marker").transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward));
                Box(root, new Vector3(-2.4f, .045f, 0f), new Vector3(.3f, .06f, 4f), safe);
                Box(root, new Vector3(3.3f, .045f, 0f), new Vector3(.3f, .06f, 4f), risk);
                Box(root, new Vector3(ContinueBoundary, .04f, 0f), new Vector3(.08f, .05f, 6f), risk);
            }
            foreach (float at in new[] { environmentEnd + 240f, Length })
            {
                RoadPathPoint point = Route.Sample(at);
                var root = new GameObject("Bank or continue gate").transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward));
                Box(root, new Vector3(-5.3f, 3f, 0f), new Vector3(.15f, 6f, .15f), safe);
                Box(root, new Vector3(5.3f, 3f, 0f), new Vector3(.15f, 6f, .15f), risk);
                WorldLabel(root, "Exit", new Vector3(-2.4f, 5f, 0f), Green, 4f);
                WorldLabel(root, "Next", new Vector3(3.3f, 5f, 0f), Gold, 4f);
            }
        }

        private static void Box(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            box.GetComponent<Collider>().enabled = false;
        }

        private static void WorldLabel(Transform parent, string value, Vector3 position, Color color, float size)
        {
            var root = new GameObject(value, typeof(TextMeshPro));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var label = root.GetComponent<TextMeshPro>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = value; label.fontSize = size; label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(5f, 2f);
        }

        private void BuildHud()
        {
            var canvas = WorldShiftMenu.CreateCanvas(transform, "Getaway HUD");
            // Safe area also applies to overlays and buttons on narrow/notched displays.
            var safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvas, false);
            var screen = Screen.safeArea;
            safe.anchorMin = new Vector2(screen.xMin / Screen.width, screen.yMin / Screen.height);
            safe.anchorMax = new Vector2(screen.xMax / Screen.width, screen.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            var hudRoot = WorldShiftMenu.Panel(safe, "Driving", Vector2.zero, Vector2.one, Color.clear);
            drivingHud = hudRoot.gameObject;
            var header = Panel(hudRoot, "Run", .02f, .89f, .98f, .98f);
            world = Label(header, "Day", .02f, .1f, .2f, .9f, 22f);
            haul = Label(header, "$0", .28f, .1f, .65f, .9f, 30f);
            haul.alignment = TextAlignmentOptions.Center; haul.color = Gold;
            stage = Label(header, "", .69f, .1f, .98f, .9f, 18f);
            stage.alignment = TextAlignmentOptions.Right;
            // One shared prompt above the horizon, away from the car, mirror and road hazards.
            var promptRoot = Panel(hudRoot, "One prompt", .14f, .77f, .75f, .85f);
            promptRoot.GetComponent<Image>().color = new Color(.025f, .045f, .07f, .82f);
            promptRoot.anchorMin = promptRoot.anchorMax = new Vector2(.445f, .81f);
            promptRoot.sizeDelta = new Vector2(480f, 52f);
            promptPanel = promptRoot.gameObject;
            guidance = Label(promptRoot, "", .03f, .08f, .97f, .92f, 26f);
            guidance.name = "Gameplay prompt";
            guidance.alignment = TextAlignmentOptions.Center;
            guidance.fontSizeMin = 24f;
            guidance.fontSizeMax = 26f;
            guidance.textWrappingMode = TextWrappingModes.NoWrap;
            var track = Panel(hudRoot, "Exit distance", .02f, .88f, .98f, .885f);
            progressFill = WorldShiftMenu.Panel(track, "Progress", Vector2.zero, Vector2.one, Gold);
            var fuelPanel = Panel(hudRoot, "Fuel", .02f, .04f, .20f, .13f);
            fuelText = Label(fuelPanel, "FUEL", .08f, .4f, .92f, .95f, 17f);
            var fuelTrack = Panel(fuelPanel, "Fuel track", .08f, .18f, .92f, .30f);
            fuelFill = WorldShiftMenu.Panel(fuelTrack, "Fuel remaining", Vector2.zero, Vector2.one, Gold);
            mirrorPanel = Panel(hudRoot, "Pursuit", .78f, .68f, .98f, .86f).gameObject;
            mirrorBorder = mirrorPanel.GetComponent<Image>();
            var image = new GameObject("Mirror", typeof(RectTransform), typeof(RawImage));
            image.transform.SetParent(mirrorPanel.transform, false);
            mirror = image.GetComponent<RawImage>(); mirror.raycastTarget = false;
            mirror.rectTransform.anchorMin = new Vector2(.025f, .04f);
            mirror.rectTransform.anchorMax = new Vector2(.98f, .98f);
            mirror.rectTransform.offsetMin = mirror.rectTransform.offsetMax = Vector2.zero;
            WorldShiftMenu.Button(hudRoot, "Pause", new Vector2(.85f, .04f), new Vector2(.98f, .11f), TogglePause);

            var choice = Panel(hudRoot, "Extraction choice", .16f, .65f, .76f, .84f);
            choices = choice.gameObject;
            choiceLeftPanel = Panel(choice, "Bank lane", .01f, .05f, .49f, .95f).GetComponent<Image>();
            choiceRightPanel = Panel(choice, "Risk lane", .51f, .05f, .99f, .95f).GetComponent<Image>();
            choiceLeft = Label(choiceLeftPanel.transform, "", .05f, .05f, .95f, .95f, 23f);
            choiceRight = Label(choiceRightPanel.transform, "", .05f, .05f, .95f, .95f, 23f);
            choiceLeft.alignment = choiceRight.alignment = TextAlignmentOptions.Center;
            choiceLeft.color = Green; choiceRight.color = Gold;

            pause = Panel(safe, "Pause backdrop", 0f, 0f, 1f, 1f).gameObject;
            Label(pause.transform, "Paused", .18f, .66f, .82f, .80f, 44f).alignment = TextAlignmentOptions.Center;
            pauseNote = Label(pause.transform, "", .18f, .51f, .82f, .63f, 22f);
            pauseNote.alignment = TextAlignmentOptions.Center;
            Label(pause.transform, "A / D to steer. Space to switch.", .20f, .10f, .80f, .16f, 18f).alignment = TextAlignmentOptions.Center;
            WorldShiftMenu.Button(pause.transform, "Keep driving", new Vector2(.24f, .35f), new Vector2(.76f, .46f), TogglePause);
            WorldShiftMenu.Button(pause.transform, "Leave this run", new Vector2(.24f, .20f), new Vector2(.76f, .31f), WorldShiftFlow.Menu);

            result = Panel(safe, "Result backdrop", 0f, 0f, 1f, 1f).gameObject;
            resultTitle = Label(result.transform, "", .15f, .69f, .85f, .83f, 42f);
            resultValue = Label(result.transform, "", .15f, .49f, .85f, .68f, 64f);
            resultNote = Label(result.transform, "", .15f, .36f, .85f, .48f, 21f);
            resultTitle.alignment = resultValue.alignment = resultNote.alignment = TextAlignmentOptions.Center;
            WorldShiftMenu.Button(result.transform, "Play again", new Vector2(.20f, .18f), new Vector2(.49f, .29f), () => WorldShiftFlow.StartGetaway(false));
            WorldShiftMenu.Button(result.transform, "Back to menu", new Vector2(.51f, .18f), new Vector2(.80f, .29f), WorldShiftFlow.Menu);
            Refresh();
        }

        private static RectTransform Panel(Transform parent, string name, float x0, float y0, float x1, float y1)
            => WorldShiftMenu.Panel(parent, name, new Vector2(x0, y0), new Vector2(x1, y1), Ink);
        private static TMP_Text Label(Transform parent, string value, float x0, float y0, float x1, float y1, float size)
            => WorldShiftMenu.Label(parent, value, x0, y0, x1, y1, size);

        private void TogglePause() { if (IsCity) city.TogglePause(); else desert.TogglePause(); }

        private void Update()
        {
            if (continuePending)
            {
                continuePending = false;
                WorldShiftFlow.FinishGetawayLeg(true);
                return;
            }
            bool settled = WorldShiftFlow.Escape.Status == GetawayRunState.RunStatus.Banked || WorldShiftFlow.Escape.Status == GetawayRunState.RunStatus.Lost;
            if (settled && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                WorldShiftFlow.StartGetaway(false);
            Refresh();
        }

        private void Refresh()
        {
            var run = WorldShiftFlow.Escape;
            Extras.Refresh();
            bool running = State == ShiftRunState.Running;
            bool settled = run.Status == GetawayRunState.RunStatus.Banked || run.Status == GetawayRunState.RunStatus.Lost;
            world.text = World == ShiftWorld.Day ? "Day" : "Night";
            world.color = World == ShiftWorld.Day ? Gold : Cyan;
            haul.text = "$" + run.Carried.ToString("N0");
            float pickupPulse = Mathf.Clamp01((lootFeedbackUntil - Duration) / .35f);
            haul.color = Color.Lerp(Gold, Color.white, pickupPulse);
            haul.rectTransform.localScale = Vector3.one * (1f + .12f * pickupPulse);
            stage.text = IsCity ? "City" : "Desert";
            fuelFill.anchorMax = new Vector2(Mathf.Clamp01(Fuel), 1f);
            fuelText.text = "Fuel";
            fuelText.color = Fuel < .25f ? new Color(1f, .3f, .2f) : Gold;
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(Distance / Length), 1f);
            bool danger = Gap < 23f;
            // The mirror border carries pursuit danger without another competing sentence.
            mirrorBorder.color = danger ? new Color(.95f, .25f, .18f, .95f) : Ink;
            mirrorPanel.SetActive(RearView != null);
            if (RearView != null) mirror.texture = RearView.Texture;
            choices.SetActive(running && InExit);
            choiceLeft.text = "Left: Cash out\n$" + run.Carried.ToString("N0");
            choiceRight.text = "Right: Keep going\n" + (IsCity ? "Desert" : "City") + " / " + (1f + Mathf.Min(currentLeg + 1, 40) * .5f).ToString("0.#") + "x loot";
            choiceLeftPanel.color = Lateral < ContinueBoundary ? new Color(.08f, .3f, .2f, .98f) : Ink;
            choiceRightPanel.color = Lateral >= ContinueBoundary ? new Color(.35f, .23f, .06f, .98f) : Ink;
            guidance.text = running && !InExit ? DrivingPrompt() : "";
            promptPanel.SetActive(guidance.text.Length > 0);
            float maxPromptWidth = Mathf.Max(300f, ((RectTransform)drivingHud.transform).rect.width * .61f);
            ((RectTransform)promptPanel.transform).sizeDelta = new Vector2(Mathf.Min(maxPromptWidth, guidance.preferredWidth + 48f), 52f);
            drivingHud.SetActive(!settled && State != ShiftRunState.Paused);
            pause.SetActive(State == ShiftRunState.Paused);
            pauseNote.text = run.Carried > 0 ? "Leaving now means losing your $" + run.Carried.ToString("N0") + "." : "Take your time. We'll wait.";
            result.SetActive(settled);
            bool banked = run.Status == GetawayRunState.RunStatus.Banked;
            resultTitle.text = banked ? "Nice getaway!" : run.Failure == "CAUGHT" ? "They got you." : run.Failure == "OUT OF FUEL" ? "Out of fuel." : run.Failure == "MISSED LANDING" ? "Missed the landing." : "Off the road.";
            resultValue.text = (banked ? "+$" : "-$") + run.SettledHaul.ToString("N0");
            resultValue.color = banked ? Green : new Color(1f, .4f, .3f);
            resultNote.text = banked ? "You've saved $" + WorldShiftFlow.Banked.ToString("N0") + " so far."
                : "This run's loot is gone. You still have $" + WorldShiftFlow.Banked.ToString("N0") + " saved.";
        }

        private string DrivingPrompt()
        {
            // Road instructions take precedence over the goal and pickup acknowledgements.
            string road = IsCity ? CityHint() : DesertHint();
            if (road.Length > 0) { openingGoalDismissed = true; return road; }
            if (Extras.Feedback.Length > 0) { openingGoalDismissed = true; return Extras.Feedback; }
            if (currentLeg == 0 && WorldShiftFlow.Escape.Carried == 0 && World == ShiftWorld.Day && Distance < 36.3f)
                return "Drive through to collect";
            // Driving time pauses with the game; distance prevents replaying this after a teleport/restore.
            if (!openingGoalDismissed && currentLeg == 0 && Duration < 2.2f && Distance < 80f) return "Grab loot. Exit to keep it.";
            return Extras.Hint;
        }

        private string CityHint()
        {
            if (city.IsRelayAirborne || WorldShiftRelay.At(Distance) != null) return city.SectionHint;
            foreach (var hazard in city.Hazards)
            {
                if (hazard.World != World || hazard.LaneMask != 7) continue;
                hazard.BoundsAt(Distance, out float start, out float end);
                if (start > Distance + 42f || end < Distance - 2f) continue;
                return hazard.Kind == ShiftHazardKind.Gap ? "Space for solid road." : "Space to switch.";
            }
            return "";
        }

        private string DesertHint()
        {
            if (desert.IsAirborne) return World == ShiftWorld.Day ? "Space to land in night." : "Stay in night to land.";
            if (desert.Relay?.SectionAt(Distance) != null) return desert.Hint;
            if (desert.Switches == 0 && Distance >= 100f && Distance < 180f) return "Space freezes the rocks.";
            return DesertShiftLevel.InRockField(Distance) && World == ShiftWorld.Day ? "Space to freeze a gap." : "";
        }

        private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
