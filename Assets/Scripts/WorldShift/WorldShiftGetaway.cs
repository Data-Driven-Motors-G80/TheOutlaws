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
        public const float ExitTail = 440f;
        public const float ContinueBoundary = 1.8f;
        private WorldShiftDrivingMessages messages;
        public void OnWorldChanged() => messages?.OnWorldChanged();
        internal int CurrentLeg => currentLeg;
        internal string JumpHint => IsCity
            ? city.IsRelayAirborne || WorldShiftRelay.At(Distance) != null ? city.SectionHint : ""
            : desert.IsAirborne || desert.Relay?.SectionAt(Distance) != null ? desert.Hint : "";
        private sealed class Loot
        {
            public float At, X;
            public bool Taken, Bonus;
            public Transform Visual;
        }
        private WorldShiftCity city;
        private DesertShiftTrial desert;
        private readonly List<Loot> loot = new List<Loot>();
        private readonly List<Material> materials = new List<Material>();
        private TMP_Text haul, guidance, resultTitle, resultValue, resultNote;
        private TMP_Text pauseNote, choiceLeft, choiceRight;
        private GameObject promptPanel;
        private WorldShiftStatusStrip statusStrip;
        private Image choiceLeftPanel, choiceRightPanel;
        private GameObject result, pause, choices, mirrorPanel, drivingHud;
        private bool continuePending;
        private float environmentEnd;
        private float lootFeedbackUntil = float.NegativeInfinity;
        private int currentLeg, bestBefore;
        public WorldShiftForkRoad Routing { get; private set; }
        public WorldShiftCombat Combat => IsCity ? city.Combat : desert.Combat;
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
        internal ShiftRunState State => IsCity ? city.State : desert.State;
        private WorldShiftRoute Route => IsCity ? city.Route : desert.Route;
        private RearViewCamera RearView => IsCity ? city.RearView : desert.RearView;

        public void Bind(WorldShiftCity controller) { city = controller; Initialize(); }
        public void Bind(DesertShiftTrial controller) { desert = controller; Initialize(); }

        private void Initialize()
        {
            messages = new WorldShiftDrivingMessages(this);
            currentLeg = WorldShiftFlow.Escape.Leg;
            bestBefore = WorldShiftFlow.BestHaul;
            environmentEnd = IsCity ? WorldShiftLevel.Length : DesertShiftLevel.Length;
            Material gold = MakeMaterial(Gold), handle = MakeMaterial(new Color(.12f, .1f, .04f));
            // Functional primitive markers only: no glow, decorative art or audio.
            foreach (var material in new[] { gold, handle })
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            // The opening trail teaches collection without a separate instructions screen.
            AddLoot(35f, 0f, gold, handle);
            AddLoot(55f, 0f, gold, handle);
            AddLoot(75f, 0f, gold, handle);
            if (IsCity)
            {
                foreach (var pickup in city.Pickups)
                {
                    // Offer a separate lane choice; never hide a fuel pickup inside a cash box.
                    if (pickup.Distance > environmentEnd - 100f) continue;
                    AddLoot(pickup.Distance, pickup.Lane == 0 ? -3.2f : 0f, gold, handle);
                }
            }
            else
            {
                // Cash alongside fuel on safe central stretches.
                for (int i = 0; i < 10; i++)
                    AddLoot(desert.FuelDistance(i), -3.2f, gold, handle);
            }
            Routing = gameObject.AddComponent<WorldShiftForkRoad>();
            Routing.Bind(this, Route, environmentEnd, !IsCity, MakeMaterial);
            foreach (float offset in new[] { 105f, 205f }) AddLoot(Routing.ForkStart + offset, -2.8f, gold, handle);
            foreach (float offset in new[] { 112f, 128f, 146f, 202f, 216f, 230f })
                AddLoot(Routing.ForkStart + offset, Routing.RightLootLane, gold, handle);
            Extras = gameObject.AddComponent<WorldShiftGetawayExtras>();
            Extras.Bind(this, Route, IsCity ? city.Player : desert.Player, environmentEnd, MakeMaterial);
            foreach (float at in new[] { Extras.Jump.Start + 8f, Extras.Jump.Start + 22f, Extras.Jump.Takeoff + 8f,
                Extras.Jump.Takeoff + 24f, Extras.Jump.Takeoff + 42f, Extras.Jump.Landing - 8f })
                AddLoot(at, WorldShiftBonusJump.Lane, gold, handle, Extras.Jump.HeightAt(at), true);
            AddLoot(Extras.Jump.Start + 16f, -3.2f, gold, handle);
            AddLoot(Extras.Jump.Start + 60f, -3.2f, gold, handle);
            // A spaced loot trail gives the magnet a useful window after its pickup.
            foreach (float at in new[] { 175f, 200f, 225f, 250f })
                AddLoot(at, -3.2f, gold, handle);
            BuildExit();
            OnWorldChanged();
            BuildHud();
            Present(World);
            WorldShiftAnalytics.BeginLeg(IsCity ? "City" : "Desert", currentLeg, Length, () => Distance, () => Duration, () => World);
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

        private void AddLoot(float at, float x, Material gold, Material handle, float rise = 0f, bool bonus = false)
        {
            var root = CreateLootBox(transform, "Loot Day " + at, Route.Sample(at), x, gold, handle, rise);
            loot.Add(new Loot { At = at, X = x, Visual = root, Bonus = bonus });
        }

        public static Transform CreateLootBox(Transform parent, string name, RoadPathPoint point, float x, Material gold, Material handle, float rise = 0f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(point.Position + point.Right * x + Vector3.up * (.15f + rise), Quaternion.LookRotation(point.Forward));
            // A low, chunky cash box differs from the tall road barriers. The symbol belongs on its face.
            Box(root, new Vector3(0f, .6f, 0f), new Vector3(1.7f, 1f, 1.05f), gold);
            Box(root, new Vector3(0f, .62f, -.54f), new Vector3(1.12f, .78f, .04f), handle);
            WorldLabel(root, "$", new Vector3(0f, .62f, -.566f), Gold, 10f);
            var symbol = root.GetComponentInChildren<TextMeshPro>();
            symbol.name = "Loot dollar symbol";
            symbol.rectTransform.sizeDelta = new Vector2(1.05f, .76f);
            symbol.fontStyle = FontStyles.Bold;
            return root;
        }

        public void Collect(float from, float to, float fromX, float toX)
        {
            if (State != ShiftRunState.Running) return;
            Extras.Collect(from, to, fromX, toX);
            Routing.CollectContacts(from, to, fromX, toX);
            foreach (var item in loot)
            {
                if (item.Bonus && !Extras.Jump.Traversing) continue;
                if (item.Taken || to < item.At - 1.3f || from > item.At + 1.3f) continue;
                if (World != ShiftWorld.Day) continue;
                if (Routing.RewardSeparated(item.At) && Mathf.Sign(item.X) != Routing.Branch(item.At, toX)) continue;
                float entry = Mathf.InverseLerp(from, to, item.At - 1.3f);
                float exit = Mathf.InverseLerp(from, to, item.At + 1.3f);
                float a = Mathf.Lerp(fromX, toX, entry), b = Mathf.Lerp(fromX, toX, exit);
                float reach = Extras.MagnetActive ? 7f : 1.35f;
                if (Mathf.Max(a, b) < item.X - reach || Mathf.Min(a, b) > item.X + reach) continue;
                item.Taken = true;
                item.Visual.gameObject.SetActive(false);
                int value = WorldShiftFlow.Escape.Collect(100);
                if (value <= 0) continue;
                lootFeedbackUntil = Duration + .35f;
                WorldShiftAnalytics.Loot(value);
            }
        }

        // Called only when simulation crosses the finish gate, never by UI refresh/rendering.
        public void Finish(float lateral)
        {
            if (WorldShiftFlow.Escape.Status != GetawayRunState.RunStatus.Running || continuePending) return;
            if (Routing.ContinueSelected(lateral)) continuePending = true;
            else WorldShiftFlow.FinishGetawayLeg(false);
        }

        public void Lose(string reason)
        {
            int haul = WorldShiftFlow.Escape.Carried;
            WorldShiftFlow.Escape.Lose(reason);
            WorldShiftAnalytics.Lost(reason, haul);
        }

        public void Present(ShiftWorld displayedWorld)
        {
            Extras?.Present(displayedWorld);
            Routing?.Present(displayedWorld);
            Combat?.Present();
            foreach (var item in loot)
            {
                bool visible = !item.Taken && displayedWorld == ShiftWorld.Day && item.At > Distance - 10f && item.At < Distance + 180f;
                item.Visual.gameObject.SetActive(visible);
                if (visible)
                {
                    var point = Route.Sample(item.At);
                    bool sameBranch = !Routing.RewardSeparated(item.At) || Mathf.Sign(item.X) == Routing.Branch(item.At, Lateral);
                    float pull = sameBranch && Extras.MagnetActive && (!item.Bonus || Extras.Jump.Traversing)
                        ? Mathf.Clamp01(1f - (item.At - Distance) / 18f) : 0f;
                    float x = Mathf.Lerp(item.X, Lateral, pull);
                    item.Visual.position = Routing.Position(item.At, x) + Vector3.up * (.15f + (item.Bonus ? Extras.Jump.HeightAt(item.At) : 0f));
                }
            }
        }

        public void BeginStep(float from, float to, AutoDriveCar driver)
        {
            Routing.BeginStep(from, to, driver.LateralOffset);
            Routing.Constrain(from, driver);
            Extras.BeginStep(from, to, driver.LateralOffset);
        }
        public void FinishStep(float distance, AutoDriveCar driver)
        { Routing.Constrain(distance, driver); Extras.FinishStep(distance, driver); }
        public void HitForkObstacle(float at) { if (IsCity) city.HitForkObstacle(at); else desert.HitForkObstacle(at); }

        private void BuildExit()
        {
            Material safe = MakeMaterial(Green), risk = MakeMaterial(Gold);
            foreach (float at in new[] { environmentEnd + 240f, Length })
                foreach (int side in new[] { -1, 1 })
                {
                    RoadPathPoint point = Route.Sample(at);
                    var root = new GameObject(side < 0 ? "Exit branch gate" : "Continue branch gate").transform;
                    root.SetParent(transform, false);
                    root.SetPositionAndRotation(Routing.Position(at, side * 2.5f), Quaternion.LookRotation(point.Forward));
                    Material colour = side < 0 ? safe : risk;
                    foreach (float x in new[] { -2.55f, 2.55f })
                    {
                        // The approach is still a shared road: keep its centre clear
                        // until the two exit branches have actually separated.
                        if (at < Routing.ExitChoiceAt && (side < 0 ? x > 0f : x < 0f)) continue;
                        Box(root, new Vector3(x, 2.5f, 0f), new Vector3(.12f, 5f, .12f), colour);
                    }
                    WorldLabel(root, side < 0 ? "Exit" : "Next", new Vector3(0f, 4.5f, 0f), side < 0 ? Green : Gold, 4f);
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
            statusStrip = WorldShiftStatusStrip.Create(hudRoot, true);
            haul = statusStrip.Loot;
            mirrorPanel = statusStrip.MirrorPanel;
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
            Label(pause.transform, "A / D to steer. Space to switch. F / click to shoot.", .14f, .10f, .80f, .16f, 18f).alignment = TextAlignmentOptions.Center;
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
            statusStrip.SetAmmo(Combat != null ? Combat.Ammo : 0, Combat != null);
            statusStrip.Refresh(World, Fuel, RearView != null ? RearView.Texture : null, Gap);
            haul.text = "$" + run.Carried.ToString("N0");
            float pickupPulse = Mathf.Clamp01((lootFeedbackUntil - Duration) / .35f);
            haul.color = Color.Lerp(Gold, Color.white, pickupPulse);
            haul.rectTransform.localScale = Vector3.one * (1f + .12f * pickupPulse);
            choices.SetActive(running && InExit);
            choiceLeft.text = "Left: Cash out\n$" + run.Carried.ToString("N0");
            choiceRight.text = "Right: Keep going\n" + (IsCity ? "Desert" : "City") + " / " + (1f + Mathf.Min(currentLeg + 1, 40) * .5f).ToString("0.#") + "x loot";
            choiceLeftPanel.color = !Routing.ContinueSelected(Lateral) ? new Color(.08f, .3f, .2f, .98f) : Ink;
            choiceRightPanel.color = Routing.ContinueSelected(Lateral) ? new Color(.35f, .23f, .06f, .98f) : Ink;
            string nextPrompt = running && !InExit ? messages.DrivingPrompt() : "";
            if (guidance.text != nextPrompt) guidance.text = nextPrompt;
            promptPanel.SetActive(nextPrompt.Length > 0);
            // A fixed responsive width avoids an autosizing/preferred-width feedback loop.
            float maxPromptWidth = Mathf.Max(300f, ((RectTransform)drivingHud.transform).rect.width * .61f);
            ((RectTransform)promptPanel.transform).sizeDelta = new Vector2(maxPromptWidth, 60f);
            drivingHud.SetActive(!settled && State != ShiftRunState.Paused);
            pause.SetActive(State == ShiftRunState.Paused);
            pauseNote.text = run.Carried > 0 ? "Leaving now means losing your $" + run.Carried.ToString("N0") + "." : "Take your time. We'll wait.";
            result.SetActive(settled);
            bool banked = run.Status == GetawayRunState.RunStatus.Banked;
            resultTitle.text = banked ? run.SettledHaul > bestBefore ? "New best getaway!" : "Nice getaway!" : run.Failure == "CAUGHT" ? "They got you." : run.Failure == "OUT OF FUEL" ? "Out of fuel." : run.Failure == "MISSED LANDING" ? "Missed the landing." : "Off the road.";
            resultValue.text = (banked ? "+$" : "-$") + run.SettledHaul.ToString("N0");
            resultValue.color = banked ? Green : new Color(1f, .4f, .3f);
            resultNote.text = banked ? "Best getaway: $" + WorldShiftFlow.BestHaul.ToString("N0") + "\nTotal saved: $" + WorldShiftFlow.Banked.ToString("N0")
                : "This run's loot is gone. You still have $" + WorldShiftFlow.Banked.ToString("N0") + " saved.";
        }

        private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
