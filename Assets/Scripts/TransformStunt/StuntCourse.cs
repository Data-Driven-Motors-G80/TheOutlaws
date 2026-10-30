using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.TransformStunt
{
    public enum StuntEnvironment { Tutorial, City, Desert, Coast }
    public enum StuntMotion { Static, Slide, Rotate }

    [Serializable]
    public sealed class StuntObstacle
    {
        public Vector3 Center, Size;
        public float Yaw, Amplitude, Period = 6f, Phase;
        public StuntMotion Motion;
        public int GateId = -1;
        public bool Weak;
        public int Plate = -1;
        public Vector3 Position(float time) => Center + (Motion == StuntMotion.Slide
            ? Vector3.right * (Mathf.Sin(time * Mathf.PI * 2f / Period + Phase) * Amplitude) : Vector3.zero);
        public float Angle(float time) => Yaw + (Motion == StuntMotion.Rotate ? time * 360f / Period : 0f);
    }

    public sealed class StuntWind
    {
        public float Start, End, MinX = -5, MaxX = 5;
        public Vector3 Acceleration;
        public bool Contains(Vector3 p) => p.z >= Start && p.z <= End && p.x >= MinX && p.x <= MaxX;
    }

    public sealed class StuntJump
    {
        public float Takeoff, Landing, End;
        public float LaunchUp = 8f;
        public float Height(float z) => z < Takeoff - 14 || z >= Landing ? 0 :
            z <= Takeoff ? Mathf.InverseLerp(Takeoff - 14, Takeoff, z) * 1.4f : 0;
    }

    public sealed class StuntCourse
    {
        public const float Width = 10f;
        public readonly StuntEnvironment Environment;
        public readonly List<StuntObstacle> Obstacles = new List<StuntObstacle>();
        public readonly List<StuntWind> Winds = new List<StuntWind>();
        public readonly List<StuntJump> Jumps = new List<StuntJump>();
        public readonly List<Vector3> Fuel = new List<Vector3>();
        public readonly List<float> Checkpoints = new List<float>();
        public readonly List<float> Plates = new List<float>();
        public readonly List<KeyValuePair<float, string>> Signs = new List<KeyValuePair<float, string>>();
        public float Length;
        public bool Tutorial => Environment == StuntEnvironment.Tutorial;

        public StuntCourse(StuntEnvironment environment)
        {
            Environment = environment;
            Length = Tutorial ? 1120 : 1200;
            if (Tutorial) BuildTutorial(); else BuildCourse();
        }

        public void Block(float x, float z, float width, float depth = 1.2f)
            => Obstacles.Add(new StuntObstacle { Center = new Vector3(x, 1.4f, z), Size = new Vector3(width, 2.8f, depth) });
        public void WeakWall(float z) => Obstacles.Add(new StuntObstacle { Center = new Vector3(0, 1.4f, z), Size = new Vector3(10, 2.8f, .7f), Weak = true });
        public void PlateDoor(float plateZ, float doorZ)
        {
            int id = Plates.Count; Plates.Add(plateZ);
            Obstacles.Add(new StuntObstacle { Center = new Vector3(0, 1.4f, doorZ), Size = new Vector3(10, 2.8f, .7f), Plate = id });
        }

        public void Gate(float z, float gap, float yaw = 0, bool moving = false, float x = 0)
        {
            Quaternion turn = Quaternion.Euler(0, yaw, 0);
            int gateId = Obstacles.Count;
            foreach (float side in new[] { -1f, 1f })
                Obstacles.Add(new StuntObstacle {
                    Center = new Vector3(x, 1.4f, z) + turn * new Vector3(side * (gap * .5f + 5), 0, 0),
                    Size = new Vector3(10, 2.8f, .35f), Yaw = yaw,
                    GateId = gateId,
                    Motion = moving ? StuntMotion.Slide : StuntMotion.Static, Amplitude = moving ? .9f : 0,
                    Period = 9f });
        }

        private void Sign(float z, string label) => Signs.Add(new KeyValuePair<float, string>(z, label));
        private void Wind(float start, float end, float force, float lift = 0)
            => Winds.Add(new StuntWind { Start = start, End = end, Acceleration = new Vector3(force, lift, 0) });
        private void Jump(float start, float gap, bool opening)
        {
            Jumps.Add(new StuntJump { Takeoff = start, Landing = start + gap, End = start + gap + 45 });
            Sign(start - 45, "RAMP / NORMAL SPEED\nSPACE IN FLIGHT");
            if (opening)
                Obstacles.Add(new StuntObstacle { Center = new Vector3(0, 6.05f, start + 15), Size = new Vector3(10, 1.5f, .4f) });
        }

        private void BuildTutorial()
        {
            for (int i = 0; i < 8; i++) Checkpoints.Add(i * 140);
            Block(-3.5f, 90, 2); Block(3.5f, 110, 2);
            Gate(205, 1.6f); Sign(175, "SPACE / SHRINK");
            WeakWall(350); Sign(310, "E / LARGE BREAKS GOLD WALLS");
            Fuel.Add(new Vector3(0, .65f, 475));
            PlateDoor(570, 630); Sign(540, "LARGE / PRESS GOLD PLATE");
            Wind(735, 800, 1.3f); Sign(710, "FAN / SMALL DRIFTS MORE");
            Jump(900, 26, true);
            Gate(1035, 1.6f); Gate(1080, 4.5f, moving: true);
        }

        private void BuildCourse()
        {
            Checkpoints.AddRange(Environment == StuntEnvironment.City
                ? new[] { 0f, 145f, 280f, 370f, 520f, 620f, 760f, 850f, 970f, 1050f }
                : new[] { 0f, 155f, 270f, 380f, 490f, 610f, 760f, 940f, 1080f });
            for (float z = 110; z < Length - 20; z += 150) Fuel.Add(new Vector3(0, .65f, z));
            if (Environment == StuntEnvironment.City)
            {
                Gate(80, 3.4f, x: -1.2f); Sign(40, "STEER THROUGH THE OPENING");
                Gate(195, 1.6f); Gate(320, 4.5f, moving: true);
                WeakWall(405); Sign(365, "LARGE / BREAK THE SUPPORT");
                // Two full-road openings: a small left vent and a Medium-width right speed route.
                Block(-4.55f, 500, .9f, .7f); Block(-.525f, 500, 4.15f, .7f); Block(4.425f, 500, 1.15f, .7f);
                Wind(470, 510, -.5f); Sign(445, "SMALL LEFT / FAST MEDIUM RIGHT");
                PlateDoor(570, 630); Sign(540, "GROW LARGE / PRESS THE PLATE");
                Gate(660, 1.6f); Gate(810, 4.5f, moving: true); Gate(900, 1.6f);
                WeakWall(1015); Gate(1090, 4.5f, moving: true);
            }
            else
            {
                Wind(65, 130, 2); Sign(30, "CROSSWIND / NORMAL RESISTS");
                Gate(195, 1.6f); Jump(315, 28, true);
                Gate(420, 4.5f, moving: true);
                Wind(535, 600, -2); Sign(500, "WIND FROM THE RIGHT");
                Jump(650, 27, true);
                if (Environment == StuntEnvironment.Coast)
                {
                    Obstacles.Add(new StuntObstacle { Center = new Vector3(-2, .8f, 815), Size = new Vector3(5, 1.6f, .5f), Motion = StuntMotion.Rotate, Period = 10 });
                    Sign(775, "ROTATING ARM / PASS RIGHT");
                }
                else Gate(815, 1.6f);
                // An optional small-car updraft extends a short ramp jump; normal cars can clear it unaided.
                Wind(980, 1025, .8f, .8f); Jump(990, 28, false);
                Sign(950, "UPDRAFT / SMALL GETS LIFT");
                Gate(1120, 4.5f, moving: true);
                for (int i = 0; i < Fuel.Count; i++)
                    foreach (var jump in Jumps)
                        if (Fuel[i].z > jump.Takeoff - 15 && Fuel[i].z < jump.Landing + 20)
                            Fuel[i] = new Vector3(0, .65f, jump.Landing + 28);
            }
        }

        public bool HasRoad(float z)
        {
            foreach (var jump in Jumps) if (z > jump.Takeoff && z < jump.Landing) return false;
            return true;
        }
        public float Surface(float z)
        {
            foreach (var jump in Jumps) if (z >= jump.Takeoff - 14 && z <= jump.Takeoff) return jump.Height(z);
            return 0;
        }
    }
}
