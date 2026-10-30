using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Outlaws.TransformStunt
{
    public sealed class StuntGame : MonoBehaviour
    {
        public int environment = -1;
        public RoadSegment roadTemplate;
        public GameObject carPrefab, fuelPrefab, streetlightPrefab;
        public Material desertMaterial, coastMaterial;
        public Camera view;
        public StuntRun Run { get; private set; }
        public static readonly string[] SceneNames = { "Stunt_Tutorial", "Stunt_City", "Stunt_Desert", "Stunt_Coast" };
        private static int destination = 1;
        private Transform car;
        private AutoDriveCar poseDriver;
        private Transform orientation;
        private readonly List<Transform> obstacleViews = new List<Transform>();
        private readonly List<GameObject> fuelViews = new List<GameObject>();
        private readonly List<Transform> fanViews = new List<Transform>();
        private readonly List<KeyValuePair<Transform, float>> courseSigns = new List<KeyValuePair<Transform, float>>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private GUIStyle title, subtitle, body, button, small, toggle;
        private bool tutorialFirst, savedResult;
        private Color accent = new Color(.25f, .95f, .75f);

        private void Awake()
        {
            Time.timeScale = 1;
            tutorialFirst = PlayerPrefs.GetInt("Stunt.TutorialCompleted", 0) == 0;
            if (view == null) view = Camera.main;
            if (environment < 0) return;
            Run = new StuntRun(new StuntCourse((StuntEnvironment)environment));
            Build();
            UpdateVisuals(true);
        }

        private void Update()
        {
            if (Run == null) return;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.enterKey.wasPressedThisFrame && !Run.Started) Run.Started = true;
                if (k.escapeKey.wasPressedThisFrame && Run.Started && !Run.Finished) Run.Paused = !Run.Paused;
                if (k.rKey.wasPressedThisFrame) Run.Retry();
                Run.Transform(k.qKey.wasPressedThisFrame, k.eKey.wasPressedThisFrame);
            }
            float steer = k == null ? 0 : ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0);
            Run.Advance(Time.deltaTime, steer);
            UpdateVisuals(false);
            if (Run.Finished && !savedResult)
            {
                savedResult = true;
                if (environment == 0) PlayerPrefs.SetInt("Stunt.TutorialCompleted", 1);
                else
                {
                    string key = "Stunt.Best." + environment;
                    if (Run.Duration < PlayerPrefs.GetFloat(key, float.MaxValue)) PlayerPrefs.SetFloat(key, Run.Duration);
                }
                PlayerPrefs.Save();
            }
        }

        private void OnApplicationFocus(bool focused) { if (!focused && Run != null && Run.Started && !Run.Finished) Run.Paused = true; }

        private Material Mat(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color; material.SetFloat("_Smoothness", .3f);
            ownedMaterials.Add(material); return material;
        }
        private Transform Box(string name, Vector3 pos, Vector3 size, Material material, float angle = 0)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name; box.transform.SetParent(transform, false);
            box.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, angle, 0));
            box.transform.localScale = size;
            box.GetComponent<Collider>().enabled = false;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box.transform;
        }
        private static void VisualOnly(GameObject obj)
        {
            foreach (var script in obj.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var rb in obj.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }
        }
        private Transform Label(string text, Vector3 position, float scale = .4f)
        {
            var obj = new GameObject("Course sign: " + text); obj.transform.SetParent(transform, false);
            obj.transform.position = position;
            var label = obj.AddComponent<TextMesh>(); label.text = text; label.fontSize = 50; label.characterSize = scale;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = Color.white;
            obj.transform.rotation = Quaternion.identity;
            return obj.transform;
        }

        private void Build()
        {
            var course = Run.Course;
            Color sky = environment == 2 ? new Color(.78f, .63f, .43f) : environment == 3 ? new Color(.35f, .67f, .77f) : new Color(.38f, .53f, .67f);
            view.backgroundColor = sky; view.clearFlags = CameraClearFlags.SolidColor;
            view.fieldOfView = 61; view.farClipPlane = 270;
            RenderSettings.skybox = null; RenderSettings.ambientLight = new Color(.7f, .75f, .8f);
            RenderSettings.fog = true; RenderSettings.fogColor = sky;
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 130; RenderSettings.fogEndDistance = 260;
            var roadMat = Mat(new Color(.12f, .17f, .21f));
            var edgeMat = Mat(accent);
            var hazardMat = Mat(new Color(1f, .38f, .15f));
            var machineMat = Mat(new Color(.68f, .35f, .95f));
            var weakMat = Mat(new Color(1f, .78f, .15f));
            var groundMat = Mat(environment == 2 ? new Color(.7f, .49f, .27f) : environment == 3 ? new Color(.06f, .4f, .5f) : new Color(.25f, .31f, .35f));
            var sceneMat = Mat(environment == 2 ? new Color(.57f, .35f, .20f) : new Color(.32f, .43f, .5f));
            var fuelMat = Mat(new Color(.25f, 1, .42f));
            Box("Environment ground", new Vector3(0, -8, course.Length / 2), new Vector3(220, 1, course.Length + 400), groundMat);

            var cuts = new SortedSet<float> { -30, course.Length + 45 };
            for (float z = 0; z < course.Length; z += 30) cuts.Add(z);
            foreach (var jump in course.Jumps) { cuts.Add(jump.Takeoff - 14); cuts.Add(jump.Takeoff); cuts.Add(jump.Landing); }
            float previous = -30;
            foreach (float end in cuts)
            {
                if (end <= previous) continue;
                float mid = (previous + end) / 2;
                if (course.HasRoad(mid))
                {
                    var segment = Instantiate(roadTemplate, transform); segment.name = "Reused road " + previous;
                    segment.transform.SetPositionAndRotation(new Vector3(0, course.Surface(previous), previous), Quaternion.identity);
                    float slope = (course.Surface(end) - course.Surface(previous)) / (end - previous);
                    // Landing boundaries must not slope down from the absent launch deck.
                    if (course.Surface(previous) == 0 && course.Surface(end) == 0) slope = 0;
                    segment.ConfigureCurve(end - previous, 0, false, slope, slope);
                    foreach (var c in segment.GetComponentsInChildren<Collider>()) c.enabled = false;
                    foreach (string child in new[] { "Car", "Left Roadside Wall", "Right Roadside Wall" })
                    { var t = segment.transform.Find(child); if (t != null) t.gameObject.SetActive(false); }
                    var surface = segment.transform.Find("Road");
                    if (surface != null) surface.GetComponent<Renderer>().sharedMaterial = roadMat;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var rail = Box("Track edge", new Vector3(side * 5, course.Surface(mid) + .025f, mid), new Vector3(.10f, .05f, end - previous), edgeMat);
                        rail.rotation = Quaternion.Euler(-Mathf.Atan(slope) * Mathf.Rad2Deg, 0, 0);
                    }
                }
                previous = end;
            }
            for (float z = 15; z < course.Length; z += 45)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    if (environment == 2)
                    {
                        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        rock.name = "Desert rock"; rock.transform.SetParent(transform); rock.transform.position = new Vector3(side * 16, -2, z);
                        rock.transform.localScale = new Vector3(9, 12, 11); rock.GetComponent<Collider>().enabled = false;
                        rock.GetComponent<Renderer>().sharedMaterial = desertMaterial != null ? desertMaterial : sceneMat;
                    }
                    else if (environment == 3)
                    {
                        Box("Coast pier", new Vector3(side * 12, -3, z), new Vector3(5, 9, 1), sceneMat);
                        Box("Coast platform", new Vector3(side * 14, 1.6f, z), new Vector3(9, .3f, 6), sceneMat);
                    }
                    else
                    {
                        float h = 6 + Mathf.Abs(Mathf.Sin(z)) * 17;
                        Box("City tower", new Vector3(side * 18, h / 2 - 4, z), new Vector3(10, h, 18), sceneMat);
                        if (streetlightPrefab != null)
                        {
                            var lamp = Instantiate(streetlightPrefab, new Vector3(side * 7, 0, z), Quaternion.identity, transform);
                            VisualOnly(lamp);
                        }
                    }
                }
            }
            foreach (var obstacle in course.Obstacles)
            {
                var obj = Box(obstacle.Weak ? "Breakable gold support" : obstacle.Plate >= 0 ? "Plate door" : obstacle.Motion == StuntMotion.Static ? "Solid obstacle" : "Moving machinery", obstacle.Center, obstacle.Size,
                    obstacle.Weak ? weakMat : obstacle.Plate >= 0 ? machineMat : obstacle.Motion == StuntMotion.Static ? hazardMat : machineMat, obstacle.Yaw);
                obstacleViews.Add(obj);
                var stripe = Box("Warning band", Vector3.zero, Vector3.one, edgeMat);
                stripe.SetParent(obj, false); stripe.localPosition = new Vector3(0, .30f, -.505f); stripe.localScale = new Vector3(1, .08f, .015f);
            }
            foreach (float plate in course.Plates)
            {
                Box("Heavy plate", new Vector3(0, .08f, plate), new Vector3(6, .16f, 3), weakMat);
                courseSigns.Add(new KeyValuePair<Transform, float>(Label("LARGE PLATE", new Vector3(0, 3, plate - 8), .12f), plate + 3));
            }
            if (environment == 1)
                for (float z = 445; z < 500; z += 5)
                    Box("Medium speed ramp arrow", new Vector3(2.7f, .04f, z), new Vector3(1.5f, .05f, .6f), edgeMat);
            foreach (var point in course.Fuel)
            {
                var pickup = Instantiate(fuelPrefab, point, Quaternion.identity, transform); VisualOnly(pickup);
                pickup.name = "Fuel only";
                foreach (var renderer in pickup.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = fuelMat;
                fuelViews.Add(pickup); Label("F", point + Vector3.up * 1.6f, .15f).SetParent(pickup.transform, true);
            }
            foreach (var wind in course.Winds)
            {
                float side = wind.Acceleration.x >= 0 ? -1 : 1;
                float z = (wind.Start + wind.End) / 2;
                Box("Fan housing", new Vector3(side * 6.5f, 1.3f, z), new Vector3(.5f, 3, 3), machineMat);
                var rotor = new GameObject("Fan rotor").transform; rotor.SetParent(transform); rotor.position = new Vector3(side * 6.15f, 1.3f, z);
                foreach (float angle in new[] { 0f, 90f })
                {
                    var blade = Box("Fan blade", Vector3.zero, Vector3.one, edgeMat);
                    blade.SetParent(rotor, false); blade.localScale = new Vector3(.15f, 2.5f, .25f); blade.localRotation = Quaternion.Euler(angle, 0, 0);
                }
                fanViews.Add(rotor);
                for (float at = wind.Start; at < wind.End; at += 7)
                    Label(wind.Acceleration.y > 0 ? "UPDRAFT  >>>" : side < 0 ? ">>>" : "<<<", new Vector3(0, .2f, at), .08f);
                Box("Wind zone start", new Vector3(0, .025f, wind.Start), new Vector3(10, .03f, .3f), machineMat);
                Box("Wind zone end", new Vector3(0, .025f, wind.End), new Vector3(10, .03f, .3f), machineMat);
            }
            foreach (var jump in course.Jumps)
            {
                courseSigns.Add(new KeyValuePair<Transform, float>(Label("RAMP", new Vector3(0, 2.5f, jump.Takeoff - 18), .14f), jump.Takeoff - 15));
                courseSigns.Add(new KeyValuePair<Transform, float>(Label("LAND HERE", new Vector3(0, 1, jump.Landing + 9), .13f), jump.Landing + 12));
                for (float z = jump.Takeoff - 12; z < jump.Takeoff; z += 3)
                    Box("Ramp boost marking", new Vector3(0, course.Surface(z) + .03f, z), new Vector3(8, .05f, .3f), edgeMat);
            }
            foreach (var sign in course.Signs)
                courseSigns.Add(new KeyValuePair<Transform, float>(Label(sign.Value, new Vector3(0, 4.8f, sign.Key), .09f), sign.Key + 3));
            foreach (float cp in course.Checkpoints)
            {
                Box("Checkpoint line", new Vector3(0, .03f, cp), new Vector3(10, .06f, .4f), edgeMat);
                if (cp > 0) courseSigns.Add(new KeyValuePair<Transform, float>(Label("CHECKPOINT", new Vector3(0, 5, cp), .11f), cp + 3));
            }
            foreach (float side in new[] { -1f, 1f }) Box("Finish post", new Vector3(side * 5, 3, course.Length), new Vector3(.4f, 6, .4f), edgeMat);
            Box("Finish arch", new Vector3(0, 6, course.Length), new Vector3(10, .4f, .4f), edgeMat);
            Label("FINISH", new Vector3(0, 5, course.Length), .2f);

            car = new GameObject("Transforming player").transform; car.SetParent(transform);
            orientation = new GameObject("Size and orientation").transform; orientation.SetParent(car, false);
            var model = Instantiate(carPrefab, orientation); VisualOnly(model);
            model.transform.localPosition = new Vector3(0, .3f, 0); model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(1.8f, .45f, 3);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = Mat(new Color(1, .78f, .18f));
            var glass = Mat(new Color(.045f, .13f, .19f));
            var cabin = Box("Player cabin", Vector3.zero, Vector3.one, glass);
            cabin.SetParent(orientation, false); cabin.localPosition = new Vector3(0, .7f, -.1f); cabin.localScale = new Vector3(1.35f, .4f, 1.4f);
            var tireMat = Mat(new Color(.025f, .035f, .045f));
            foreach (float x in new[] { -.72f, .72f }) foreach (float z in new[] { -.95f, .95f })
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name = "Player wheel";
                wheel.transform.SetParent(orientation, false); wheel.transform.localPosition = new Vector3(x, .27f, z);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90); wheel.transform.localScale = new Vector3(.54f, .14f, .54f);
                wheel.GetComponent<Collider>().enabled = false; wheel.GetComponent<Renderer>().sharedMaterial = tireMat;
            }
            // Existing finite-route driver supplies the shared route pose. The stunt simulation owns its footprint and flight.
            poseDriver = car.gameObject.AddComponent<AutoDriveCar>();
            poseDriver.ConfigureExternalRoute(z => new RoadPathPoint(new Vector3(0, Run.Position.y - .08f, z), Vector3.forward, 10), null, 20);
        }

        private void UpdateVisuals(bool immediate)
        {
            if (car == null) return;
            poseDriver.RestoreExternalPose(Run.Position.z, Run.Position.x);
            orientation.localScale = Vector3.one * Run.Scale;
            orientation.localRotation = Quaternion.identity;
            for (int i = 0; i < obstacleViews.Count; i++)
            {
                var obstacle = Run.Course.Obstacles[i];
                obstacleViews[i].gameObject.SetActive(!Run.Broken[i] && (obstacle.Plate < 0 || !Run.Activated[obstacle.Plate]));
                obstacleViews[i].SetPositionAndRotation(obstacle.Position(Run.Clock), Quaternion.Euler(0, obstacle.Angle(Run.Clock), 0));
            }
            for (int i = 0; i < fuelViews.Count; i++) fuelViews[i].SetActive(!Run.FuelTaken[i]);
            foreach (var sign in courseSigns) sign.Key.gameObject.SetActive(Run.Position.z < sign.Value);
            foreach (var fan in fanViews) fan.localRotation = Quaternion.Euler(Run.Clock * 300, 0, 0);
            Vector3 desired = new Vector3(Run.Position.x * .35f, Mathf.Max(0, Run.Position.y) + 8, Run.Position.z - 12);
            view.transform.position = immediate ? desired : Vector3.Lerp(view.transform.position, desired, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10));
            view.transform.LookAt(new Vector3(Run.Position.x * .4f, Mathf.Max(0, Run.Position.y) + 1, Run.Position.z + 17));
        }

        public static void Load(int index) { Time.timeScale = 1; SceneManager.LoadScene(SceneNames[index]); }
        private void Select(int index)
        {
            destination = index;
            Load(tutorialFirst ? 0 : index);
        }

        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            small = new GUIStyle(body) { fontSize = 15 };
            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 19 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, fixedHeight = 50 };
        }
        private static void Panel(Rect rect, Color color)
        { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }

        private void OnGUI()
        {
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            if (Run == null) { Menu(); return; }
            Panel(new Rect(20, 20, 1240, 86), new Color(.025f, .045f, .065f, .94f));
            GUI.Label(new Rect(40, 28, 270, 34), environment == 0 ? "TRAINING / " + (Run.Lesson + 1) + " OF 8" : ((StuntEnvironment)environment).ToString().ToUpper() + " / STUNT RUN", subtitle);
            GUI.Label(new Rect(40, 65, 310, 28), Run.Duration.ToString("0.0") + "s   /   " + Run.Crashes + " retries   /   " + (Run.Position.z / Run.Course.Length * 100).ToString("0") + "%", small);
            GUI.Label(new Rect(380, 28, 380, 30), StuntRun.SizeNames[Run.SizeIndex] + "    " + Run.Speed.ToString("0.0") + " m/s", subtitle);
            GUI.Label(new Rect(380, 65, 400, 26), "Q SHRINK   /   E GROW" + (Run.Airborne ? "   /   AIRBORNE" : ""), small);
            GUI.Label(new Rect(960, 28, 260, 30), "FUEL  " + Run.Fuel.CurrentFuel.ToString("0") + " / 100", subtitle);
            Panel(new Rect(960, 67, 260, 14), new Color(.15f, .2f, .25f));
            Panel(new Rect(960, 67, 260 * Run.Fuel.CurrentFuel / 100, 14), Run.Fuel.CurrentFuel < 25 ? new Color(1, .3f, .2f) : accent);
            Panel(new Rect(20, 610, 1240, 90), new Color(.025f, .045f, .065f, .94f));
            GUI.Label(new Rect(40, 625, 1200, 64), Run.Instruction, body);
            if (Run.FeedbackTime > 0)
            {
                Panel(new Rect(250, 135, 780, 80), new Color(.04f, .08f, .11f, .95f));
                GUI.Label(new Rect(270, 150, 740, 58), Run.Feedback, body);
            }
            if (!Run.Started || Run.Paused || Run.Finished)
            {
                Panel(new Rect(330, 220, 620, 365), new Color(.025f, .045f, .065f, .98f));
                GUILayout.BeginArea(new Rect(365, 240, 550, 330));
                GUILayout.Label(Run.Finished ? (environment == 0 ? "TRAINING COMPLETE" : "COURSE COMPLETE") : Run.Paused ? "PAUSED" : "TRANSFORM / DRIVE", subtitle);
                GUILayout.Space(12);
                GUILayout.Label(Run.Finished ? $"Time {Run.Duration:0.0}s   ·   Retries {Run.Crashes}" : environment == 0 ? "Learn one skill at a time. Miss a step? Retry only that lesson." : "Reach the finish. Collect green fuel. Use normal size for speed and stability; shrink for gaps.", body);
                GUILayout.Space(12);
                if (Run.Finished)
                {
                    if (GUILayout.Button(environment == 0 ? "CONTINUE TO COURSE" : environment < 3 ? "NEXT COURSE" : "LEVEL SELECT", button))
                    { if (environment == 0) Load(destination); else if (environment < 3) Load(environment + 1); else SceneManager.LoadScene("Stunt_Menu"); }
                    if (GUILayout.Button("REPLAY", button)) Load(environment);
                }
                else
                {
                    if (GUILayout.Button(Run.Paused ? "RESUME" : "START / ENTER", button)) { Run.Started = true; Run.Paused = false; }
                    if (Run.Paused && GUILayout.Button("RESTART COURSE", button)) Load(environment);
                    if (environment == 0 && GUILayout.Button("SKIP TUTORIAL", button)) Load(destination);
                }
                if (GUILayout.Button("LEVEL SELECT", button)) SceneManager.LoadScene("Stunt_Menu");
                GUILayout.EndArea();
            }
        }

        private void Menu()
        {
            Panel(new Rect(0, 0, 1280, 720), new Color(.025f, .045f, .065f));
            Panel(new Rect(60, 72, 6, 105), accent);
            GUI.Label(new Rect(90, 65, 1000, 60), "TRANSFORM / DRIVE", title);
            GUI.Label(new Rect(92, 130, 1040, 65), "One car. Different shapes. Find your way through.", subtitle);
            string[] names = { "01 / CITY", "02 / DESERT", "03 / COAST" };
            string[] descriptions = { "Tight passages & angled gates", "Crosswinds & transformation jumps", "Moving machinery & combined skills" };
            for (int i = 0; i < 3; i++)
            {
                float x = 90 + i * 370;
                Panel(new Rect(x, 245, 345, 225), new Color(.08f, .13f, .18f));
                GUI.Label(new Rect(x + 20, 267, 310, 40), names[i], subtitle);
                GUI.Label(new Rect(x + 20, 315, 300, 65), descriptions[i], body);
                float best = PlayerPrefs.GetFloat("Stunt.Best." + (i + 1), 0);
                GUI.Label(new Rect(x + 20, 375, 300, 26), best > 0 ? "BEST " + best.ToString("0.0") + "s" : "NO TIME SET", small);
                if (GUI.Button(new Rect(x + 20, 410, 305, 45), "DRIVE", button)) Select(i + 1);
            }
            tutorialFirst = GUI.Toggle(new Rect(95, 510, 650, 40), tutorialFirst, "  PLAY TUTORIAL BEFORE SELECTED COURSE", toggle);
            if (GUI.Button(new Rect(90, 565, 345, 50), "TUTORIAL / PRACTICE", button)) { destination = 1; Load(0); }
            GUI.Label(new Rect(480, 572, 700, 70), "A/D steer  ·  Q shrink  ·  E grow\nFuel only. Unlimited checkpoint retries.", body);
        }

        private void OnDestroy()
        {
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
