using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    // Training "driving academy" dressing, visual only: no colliders, no people (people are City-only), nothing in
    // the lesson lanes or near the final jump. Each section gets a control tower with a spinning radar and beacon,
    // a coned slalom loop with a practice car and a working traffic light, a skid pad with a drifting car and a camera
    // drone, red/white tyre walls, windsocks, wordless practice road signs and (alternate sections) a checkered water
    // tower. Static parts are batched per setpiece; only the few moving parts are separate transforms.
    // Animated on the shared street clock, so it pauses with the game and lesson stops.
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private sealed class TrainingCar { public Transform Car; public float Phase, Speed; public bool Slalom; public float Radius, Direction; }
        private sealed class TrainingSpinner { public Transform Item; public Vector3 Base; public float Phase, Rate; public int Mode; }
        private sealed class TrainingSignal { public GameObject[] Lit; public float Phase; }
        private readonly List<TrainingCar> trainingCars = new List<TrainingCar>();
        private readonly List<TrainingSpinner> trainingSpinners = new List<TrainingSpinner>();
        private readonly List<TrainingSignal> trainingSignals = new List<TrainingSignal>();
        private readonly List<GameObject> trainingBeacons = new List<GameObject>();
        private const int SpinRadar = 0, SpinDrone = 1, SpinSock = 2, SpinFlag = 3;
        private const float SlalomLength = 56f, SlalomHalfWidth = 4f;
        private static readonly Color AcademyTeal = new Color(.16f, .50f, .56f), AcademyOrange = new Color(.95f, .48f, .16f),
            AcademyWhite = new Color(.88f, .90f, .86f), AcademySteel = new Color(.16f, .22f, .27f), AcademyRed = new Color(.86f, .20f, .18f);

        private void BuildTrainingLife()
        {
            var checkpoints = route.Course.Checkpoints;
            for (int section = 0; section < checkpoints.Length; section++)
            {
                float start = checkpoints[section].Distance;
                float side = section % 2 == 0 ? 1f : -1f;
                // Depots sit at +45/+65 m; the academy fills the second half of each section.
                float tower = start + 100f, slalom = start + 128f, pad = start + 150f, tyres = start + 88f;
                if (!NearJump(tower, 40f) && Clear(tower, side * 20f, 5f)) TrainingTower(tower, side, section);
                if (!NearJump(slalom, 40f + SlalomLength * .5f) && SlalomClear(slalom, -side * 17f)) TrainingSlalom(slalom, -side, section);
                if (!NearJump(pad, 40f) && Clear(pad, side * 27f, 10.5f)) TrainingSkidPad(pad, side, section);
                foreach (float s in new[] { -1f, 1f })
                    if (!NearJump(tyres, 30f) && Clear(tyres, s * 10f, 1.5f)) TrainingTyreWall(tyres, s, section);
                if (section % 2 == 1 && !NearJump(start + 120f, 40f) && Clear(start + 120f, side * 44f, 6f))
                    TrainingWaterTower(start + 120f, side * 44f);
            }
            StepTrainingLife();
        }

        private bool SlalomClear(float at, float lateral)
        {
            for (float d = -SlalomLength * .5f; d <= SlalomLength * .5f; d += 7f)
                if (!Clear(at + d, lateral, SlalomHalfWidth)) return false;
            return true;
        }

        private Transform TrainingPart(Transform parent, string label, Vector3 local, Quaternion rotation)
        {
            var part = new GameObject(label).transform; part.SetParent(parent, false);
            part.localPosition = local; part.localRotation = rotation; return part;
        }

        // A sign face in the plane facing the road (normal along local x), as a two-sided fan.
        private static void SignFace(WorldShiftArtMesh mesh, Vector3 centre, float radius, int sides, float turn, Color colour)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = turn + i * Mathf.PI * 2f / sides, b = turn + (i + 1) * Mathf.PI * 2f / sides;
                mesh.Triangle(centre, centre + new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)) * radius,
                    centre + new Vector3(0, Mathf.Sin(b), Mathf.Cos(b)) * radius, colour, true);
            }
        }

        private void TrainingTower(float at, float side, int section)
        {
            var root = Root("Training control tower", at, side * 20f);
            var body = new WorldShiftArtMesh(); var glass = new WorldShiftArtMesh();
            // Banded shaft, a wide glazed cab and a flat roof with railings.
            body.Box(new Vector3(0, .3f, 0), new Vector3(5f, .6f, 5f), AcademySteel);
            for (int band = 0; band < 5; band++)
                body.Box(new Vector3(0, .6f + band * 2.2f + 1.1f, 0), new Vector3(3f, 2.2f, 3f), band % 2 == 0 ? AcademyWhite : AcademyTeal);
            body.Box(new Vector3(0, 11.9f, 0), new Vector3(6.2f, .5f, 6.2f), AcademySteel);
            foreach (float x in new[] { -2.85f, 2.85f })
                foreach (float z in new[] { -2.85f, 2.85f }) body.Box(new Vector3(x, 13.35f, z), new Vector3(.22f, 2.4f, .22f), AcademySteel);
            glass.Box(new Vector3(0, 13.35f, 0), new Vector3(5.5f, 2.2f, 5.5f), new Color(.55f, .78f, .80f));
            body.Box(new Vector3(0, 14.7f, 0), new Vector3(6.8f, .4f, 6.8f), AcademyTeal);
            for (int i = -3; i <= 3; i++)
            {
                body.Box(new Vector3(i, 15.35f, -3.3f), new Vector3(.08f, .9f, .08f), AcademyWhite);
                body.Box(new Vector3(i, 15.35f, 3.3f), new Vector3(.08f, .9f, .08f), AcademyWhite);
            }
            body.Box(new Vector3(0, 15.8f, -3.3f), new Vector3(6.6f, .08f, .08f), AcademyWhite);
            body.Box(new Vector3(0, 15.8f, 3.3f), new Vector3(6.6f, .08f, .08f), AcademyWhite);
            body.Cylinder(new Vector3(-side * 1.8f, 14.9f, 1.8f), .07f, .05f, 4.2f, AcademySteel, 6); // antenna mast
            body.Cylinder(new Vector3(side * 1.2f, 14.9f, -1.2f), .25f, .2f, 1.2f, AcademySteel, 8);   // radar plinth
            Render(root, "Tower shaft, cab and roof", body, surface);
            Render(root, "Tower cab glazing", glass, windows);
            // Spinning radar bar.
            var radar = TrainingPart(root, "Spinning radar", new Vector3(side * 1.2f, 16.15f, -1.2f), Quaternion.identity);
            var dish = new WorldShiftArtMesh();
            dish.Box(new Vector3(0, .15f, 0), new Vector3(2.6f, .45f, .18f), AcademyWhite);
            dish.Box(new Vector3(0, .15f, -.12f), new Vector3(2.4f, .3f, .06f), AcademyOrange);
            Render(radar, "Radar bar", dish, surface);
            trainingSpinners.Add(new TrainingSpinner { Item = radar, Rate = 120f, Phase = section * 40f, Mode = SpinRadar });
            // Blinking red beacon on the mast.
            var beacon = new WorldShiftArtMesh(); beacon.Box(new Vector3(-side * 1.8f, 19.2f, 1.8f), new Vector3(.35f, .35f, .35f), AcademyRed);
            var beaconRoot = TrainingPart(root, "Tower beacon", Vector3.zero, Quaternion.identity);
            Render(beaconRoot, "Beacon lamp", beacon, windows);
            trainingBeacons.Add(beaconRoot.gameObject);
            // Windsock and three academy flags beside the tower.
            var poles = new WorldShiftArtMesh();
            poles.Cylinder(new Vector3(side * 4.5f, 0, 5f), .08f, .06f, 6.5f, AcademySteel, 6);
            for (int f = 0; f < 3; f++) poles.Cylinder(new Vector3(-side * 3.8f, 0, -3f + f * 2.2f), .06f, .05f, 7f, AcademySteel, 6);
            Render(root, "Windsock and flag poles", poles, surface);
            var sockPivot = TrainingPart(root, "Windsock", new Vector3(side * 4.5f, 6.3f, 5f), Quaternion.identity);
            var sockTilt = TrainingPart(sockPivot, "Windsock droop", Vector3.zero, Quaternion.Euler(78f, 0, 0));
            var sock = new WorldShiftArtMesh();
            float[] radius = { .42f, .37f, .32f, .27f, .22f };
            for (int i = 0; i < 4; i++) sock.Cylinder(new Vector3(0, i * .55f, 0), radius[i], radius[i + 1], .55f, i % 2 == 0 ? AcademyOrange : AcademyWhite, 10);
            Render(sockTilt, "Striped windsock", sock, surface);
            trainingSpinners.Add(new TrainingSpinner { Item = sockPivot, Phase = section * 1.7f, Rate = 1.6f, Mode = SpinSock });
            Color[] flags = { AcademyTeal, AcademyOrange, AcademyWhite };
            for (int f = 0; f < 3; f++)
            {
                var pivot = TrainingPart(root, "Academy flag", new Vector3(-side * 3.8f, 6.4f, -3f + f * 2.2f), Quaternion.identity);
                var flag = new WorldShiftArtMesh();
                flag.Quad(new Vector3(0, .5f, 0), new Vector3(0, .5f, 1.3f), new Vector3(0, -.3f, 1.3f), new Vector3(0, -.3f, 0), flags[f]);
                flag.Quad(new Vector3(0, -.3f, 0), new Vector3(0, -.3f, 1.3f), new Vector3(0, .5f, 1.3f), new Vector3(0, .5f, 0), flags[f]);
                Render(pivot, "Flag", flag, surface);
                trainingSpinners.Add(new TrainingSpinner { Item = pivot, Phase = f * 1.3f + section, Rate = 2.4f, Mode = SpinFlag });
            }
        }

        private void TrainingSlalom(float at, float side, int section)
        {
            var root = Root("Training slalom loop", at, side * 17f);
            var lane = new WorldShiftArtMesh();
            float half = SlalomLength * .5f;
            lane.Box(new Vector3(0, .03f, 0), new Vector3(SlalomHalfWidth * 2f, .06f, SlalomLength + 6f), new Color(.24f, .26f, .27f));
            foreach (float x in new[] { -SlalomHalfWidth + .2f, SlalomHalfWidth - .2f })
                lane.Box(new Vector3(x, .065f, 0), new Vector3(.14f, .02f, SlalomLength + 5f), AcademyWhite);
            for (float z = -half + 3f; z < half - 3f; z += 3f) lane.Box(new Vector3(0, .065f, z), new Vector3(.12f, .02f, 1.4f), AcademyWhite * .9f);
            // Start/finish chequer across both lanes.
            for (int i = 0; i < 8; i++)
                for (int r = 0; r < 2; r++)
                    lane.Box(new Vector3(-SlalomHalfWidth + .5f + i, .068f, -half - 1.5f + r * .8f), new Vector3(1f, .02f, .8f), (i + r) % 2 == 0 ? AcademyWhite : new Color(.07f, .08f, .09f));
            // Cones down the outbound lane; the car weaves through them.
            for (int k = 0; k < 9; k++)
            {
                var p = new Vector3(-2f, .06f, -half + 4f + k * 6f);
                lane.Box(p, new Vector3(.55f, .05f, .55f), AcademySteel);
                lane.Cylinder(p, .24f, .04f, .65f, AcademyOrange, 8);
                lane.Cylinder(p + Vector3.up * .26f, .15f, .12f, .13f, AcademyWhite, 8);
            }
            // Wordless practice signs facing the road: stop octagon, curve diamond, keep-right disc.
            float face = -side * (SlalomHalfWidth + .9f);
            void Pole(float z) => lane.Cylinder(new Vector3(face, 0, z), .06f, .05f, 2.4f, AcademySteel, 6);
            Pole(-14f); SignFace(lane, new Vector3(face - side * .08f, 2.6f, -14f), .55f, 8, Mathf.PI / 8f, AcademyRed);
            SignFace(lane, new Vector3(face - side * .1f, 2.6f, -14f), .45f, 8, Mathf.PI / 8f, AcademyWhite);
            SignFace(lane, new Vector3(face - side * .12f, 2.6f, -14f), .40f, 8, Mathf.PI / 8f, AcademyRed);
            Pole(2f); SignFace(lane, new Vector3(face - side * .08f, 2.6f, 2f), .6f, 4, 0f, new Color(.98f, .80f, .18f));
            lane.Beam(new Vector3(face - side * .12f, 2.35f, 2.2f), new Vector3(face - side * .12f, 2.8f, 1.9f), .1f, .02f, new Color(.07f, .08f, .09f));
            Pole(16f); SignFace(lane, new Vector3(face - side * .08f, 2.6f, 16f), .5f, 16, 0f, new Color(.18f, .42f, .78f));
            lane.Beam(new Vector3(face - side * .12f, 2.6f, 16.25f), new Vector3(face - side * .12f, 2.6f, 15.7f), .1f, .02f, AcademyWhite);
            // Red/white jersey barriers at both ends of the loop.
            foreach (float z in new[] { -half - 4.2f, half + 4.2f })
                for (int i = -1; i <= 1; i++)
                    lane.ChamferBox(new Vector3(i * 2.4f, .45f, z), new Vector3(2.2f, .9f, .5f), .1f, i == 0 ? AcademyRed : AcademyWhite);
            Render(root, "Slalom loop, cones, signs and barriers", lane, surface);
            // Traffic light at the start line: red, amber, green on a loop.
            var light = new WorldShiftArtMesh();
            Vector3 post = new Vector3(side * (SlalomHalfWidth + 1f), 0, -half - 2.5f);
            light.Cylinder(post, .1f, .08f, 3.4f, AcademySteel, 6);
            light.ChamferBox(post + new Vector3(0, 3.9f, 0), new Vector3(.55f, 1.5f, .45f), .08f, new Color(.07f, .08f, .09f));
            Color[] lamps = { AcademyRed, new Color(1f, .72f, .16f), new Color(.30f, .90f, .45f) };
            for (int i = 0; i < 3; i++) light.Box(post + new Vector3(0, 4.35f - i * .45f, -.24f), new Vector3(.3f, .3f, .04f), lamps[i] * .25f);
            Render(root, "Practice traffic light", light, surface);
            var lit = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var lens = new WorldShiftArtMesh(); lens.Box(post + new Vector3(0, 4.35f - i * .45f, -.265f), new Vector3(.3f, .3f, .03f), lamps[i]);
                var holder = TrainingPart(root, "Traffic light lens " + i, Vector3.zero, Quaternion.identity);
                Render(holder, "Lit lens", lens, windows); lit[i] = holder.gameObject;
            }
            trainingSignals.Add(new TrainingSignal { Lit = lit, Phase = section * 1.9f });
            var car = PracticeCar(root, section % 2 == 0 ? new Color(.20f, .62f, .70f) : new Color(.95f, .76f, .18f));
            trainingCars.Add(new TrainingCar { Car = car, Phase = section * 17f, Speed = 8.5f, Slalom = true });
        }

        private void TrainingSkidPad(float at, float side, int section)
        {
            var root = Root("Training skid pad", at, side * 27f);
            var pad = new WorldShiftArtMesh();
            pad.Cylinder(Vector3.zero, 10.5f, 10.5f, .05f, new Color(.30f, .32f, .33f), 32);
            pad.Cylinder(Vector3.zero, 7.9f, 7.9f, .07f, new Color(.11f, .12f, .13f), 32); // worn tyre ring
            pad.Cylinder(Vector3.zero, 6.1f, 6.1f, .08f, new Color(.30f, .32f, .33f), 32);
            pad.Cylinder(Vector3.zero, .8f, .8f, .1f, AcademyOrange, 12);
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 9.6f;
                pad.Box(p + Vector3.up * .05f, new Vector3(.6f, .06f, .6f), AcademySteel);
                pad.Cylinder(p + Vector3.up * .08f, .24f, .04f, .6f, AcademyOrange, 8);
                pad.Cylinder(p + Vector3.up * .33f, .15f, .12f, .13f, AcademyWhite, 8);
            }
            Render(root, "Skid pad, tyre marks and cones", pad, surface);
            var car = PracticeCar(root, section % 2 == 0 ? new Color(.95f, .76f, .18f) : new Color(.20f, .62f, .70f));
            trainingCars.Add(new TrainingCar { Car = car, Radius = 7f, Phase = section * 1.3f, Speed = .8f, Direction = section % 2 == 0 ? 1f : -1f });
            // A camera drone circles the pad, filming the drift.
            var drone = TrainingPart(root, "Camera drone", new Vector3(0, 7f, 0), Quaternion.identity);
            var frame = new WorldShiftArtMesh();
            frame.ChamferBox(Vector3.zero, new Vector3(.6f, .22f, .6f), .06f, new Color(.10f, .11f, .12f));
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    frame.Beam(Vector3.zero, new Vector3(x * .7f, .05f, z * .7f), .07f, .07f, new Color(.10f, .11f, .12f));
                    frame.Cylinder(new Vector3(x * .7f, .1f, z * .7f), .32f, .32f, .02f, new Color(.6f, .64f, .66f), 10);
                }
            frame.Box(new Vector3(0, -.18f, .2f), new Vector3(.2f, .2f, .2f), new Color(.07f, .08f, .09f));
            Render(drone, "Drone body and rotors", frame, surface);
            var led = new WorldShiftArtMesh(); led.Box(new Vector3(0, .14f, 0), new Vector3(.12f, .06f, .12f), AcademyRed);
            var ledRoot = TrainingPart(drone, "Drone light", Vector3.zero, Quaternion.identity);
            Render(ledRoot, "Drone LED", led, windows); trainingBeacons.Add(ledRoot.gameObject);
            trainingSpinners.Add(new TrainingSpinner { Item = drone, Base = drone.localPosition, Phase = section * 2f, Rate = .45f, Mode = SpinDrone });
        }

        private void TrainingTyreWall(float at, float side, int section)
        {
            var root = Root("Training tyre wall", at, side * 10f);
            var tyres = new WorldShiftArtMesh();
            for (int stack = 0; stack < 6; stack++)
                for (int level = 0; level < 3; level++)
                {
                    var p = new Vector3(0, level * .3f, -3.1f + stack * 1.25f);
                    tyres.Cylinder(p, .58f, .58f, .3f, new Color(.06f, .065f, .07f), 12);
                    tyres.Cylinder(p + Vector3.up * .05f, .6f, .6f, .14f, (stack + level + section) % 2 == 0 ? AcademyRed : AcademyWhite, 12);
                    tyres.Cylinder(p + Vector3.up * .301f, .3f, .3f, .01f, new Color(.03f, .035f, .04f), 12);
                }
            Render(root, "Red and white tyre wall", tyres, surface);
        }

        private void TrainingWaterTower(float at, float lateral)
        {
            var root = Root("Training water tower", at, lateral);
            var tower = new WorldShiftArtMesh();
            foreach (float x in new[] { -2.2f, 2.2f })
                foreach (float z in new[] { -2.2f, 2.2f })
                    tower.Beam(new Vector3(x, 0, z), new Vector3(x * .55f, 14f, z * .55f), .25f, .25f, AcademySteel);
            for (float y = 3.5f; y < 14f; y += 3.5f) tower.Box(new Vector3(0, y, 0), new Vector3(4.6f - y * .14f, .14f, 4.6f - y * .14f), AcademySteel);
            // Chequered tank, the classic test-track landmark.
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f, b = (i + 1) * Mathf.PI * 2f / 12f, y0 = 14f + ring * 1.2f, y1 = y0 + 1.2f;
                    Vector3 A = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.2f, B = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * 3.2f;
                    tower.Quad(B + Vector3.up * y0, B + Vector3.up * y1, A + Vector3.up * y1, A + Vector3.up * y0, (i + ring) % 2 == 0 ? AcademyWhite : AcademyRed);
                }
            tower.Cylinder(new Vector3(0, 18.8f, 0), 3.4f, .4f, 1.6f, AcademySteel, 12);
            Render(root, "Chequered water tower", tower, surface);
        }

        private Transform PracticeCar(Transform parent, Color paint)
        {
            var car = new GameObject("Practice car").transform; car.SetParent(parent, false);
            var body = new WorldShiftArtMesh(); var glass = new WorldShiftArtMesh();
            Color dark = new Color(.06f, .07f, .08f);
            body.ChamferBox(new Vector3(0, .62f, 0), new Vector3(1.8f, .62f, 3.9f), .12f, paint);
            body.ChamferBox(new Vector3(0, 1.14f, -.2f), new Vector3(1.5f, .5f, 1.9f), .1f, paint * .9f);
            body.Box(new Vector3(0, .95f, 0), new Vector3(1.82f, .12f, 3.92f), AcademyWhite); // academy stripe
            body.Box(new Vector3(0, 1.47f, -.2f), new Vector3(.55f, .16f, .3f), AcademyOrange); // instructor roof light
            foreach (float x in new[] { -.86f, .86f })
                foreach (float z in new[] { -1.25f, 1.25f }) body.Box(new Vector3(x, .34f, z), new Vector3(.3f, .62f, .62f), dark);
            glass.Box(new Vector3(0, 1.14f, -.2f), new Vector3(1.53f, .34f, 1.6f), new Color(.20f, .30f, .36f));
            glass.Box(new Vector3(0, 1.12f, .78f), new Vector3(1.3f, .3f, .06f), new Color(.20f, .30f, .36f));
            Render(car, "Practice car body", body, surface);
            Render(car, "Practice car windows", glass, windows);
            return car;
        }

        // Closed slalom loop: weave the cones on the outbound lane, U-turn, straight return lane.
        private static Vector3 SlalomPoint(float s)
        {
            float half = SlalomLength * .5f, turn = Mathf.PI * 2f, loop = SlalomLength * 2f + turn * 2f;
            s = Mathf.Repeat(s, loop);
            if (s < SlalomLength)
            {
                float z = -half + s;
                return new Vector3(-2f + Mathf.Cos((z - (-half + 4f)) * Mathf.PI / 6f) * 1.4f, 0, z);
            }
            s -= SlalomLength;
            if (s < turn) { float a = s / turn * Mathf.PI; return new Vector3(-Mathf.Cos(a) * 2f, 0, half + Mathf.Sin(a) * 2f); }
            s -= turn;
            if (s < SlalomLength) return new Vector3(2f, 0, half - s);
            s -= SlalomLength;
            float b = s / turn * Mathf.PI; return new Vector3(Mathf.Cos(b) * 2f, 0, -half - Mathf.Sin(b) * 2f);
        }

        private void StepTrainingLife()
        {
            float t = streetClock;
            foreach (var car in trainingCars)
            {
                if (!car.Car.gameObject.activeInHierarchy) continue;
                if (car.Slalom)
                {
                    float s = car.Phase + t * car.Speed;
                    Vector3 here = SlalomPoint(s), ahead = SlalomPoint(s + .6f);
                    car.Car.localPosition = here;
                    if ((ahead - here).sqrMagnitude > .0001f) car.Car.localRotation = Quaternion.LookRotation(ahead - here);
                    continue;
                }
                float angle = car.Phase + t * car.Speed * car.Direction;
                var tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * car.Direction;
                car.Car.localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * car.Radius;
                // Nose tucked toward the centre, like a car holding a drift.
                car.Car.localRotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0, -car.Direction * 28f, 0);
            }
            foreach (var spinner in trainingSpinners)
            {
                if (!spinner.Item.gameObject.activeInHierarchy) continue;
                switch (spinner.Mode)
                {
                    case SpinRadar: spinner.Item.localRotation = Quaternion.Euler(0, spinner.Phase + t * spinner.Rate, 0); break;
                    case SpinDrone:
                        float a = spinner.Phase + t * spinner.Rate;
                        spinner.Item.localPosition = spinner.Base + new Vector3(Mathf.Cos(a) * 6f, Mathf.Sin(t * 1.7f + spinner.Phase) * .35f, Mathf.Sin(a) * 6f);
                        spinner.Item.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a))) * Quaternion.Euler(8f, 0, -10f);
                        break;
                    case SpinSock: spinner.Item.localRotation = Quaternion.Euler(0, 40f + Mathf.Sin(t * spinner.Rate + spinner.Phase) * 25f, 0); break;
                    default: spinner.Item.localRotation = Quaternion.Euler(0, Mathf.Sin(t * spinner.Rate + spinner.Phase) * 22f, 0); break;
                }
            }
            foreach (var signal in trainingSignals)
            {
                // Red 3 s, green 3 s, amber 1 s.
                float c = Mathf.Repeat(t + signal.Phase, 7f);
                int on = c < 3f ? 0 : c < 6f ? 2 : 1;
                for (int i = 0; i < 3; i++) if (signal.Lit[i].activeSelf != (i == on)) signal.Lit[i].SetActive(i == on);
            }
            bool blink = Mathf.Repeat(t, 1.2f) < .5f;
            foreach (var beacon in trainingBeacons) if (beacon.activeSelf != blink) beacon.SetActive(blink);
        }
    }
}
