using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Instant lane-assisted shots share the same authoritative destruction state as collisions and rendering.</summary>
    public sealed class WorldShiftCombat : MonoBehaviour
    {
        private sealed class AmmoPickup { public float At, X; public bool Taken; public Transform Visual; }
        private struct Target { public object Key; public float At, X; public bool Breakable; }
        private sealed class ShotVisual { public Transform Body; public Vector3[] Path; public float Started; }
        private WorldShiftCity city;
        private DesertShiftTrial desert;
        private readonly HashSet<object> destroyed = new HashSet<object>();
        private readonly List<AmmoPickup> pickups = new List<AmmoPickup>();
        private int practiceAmmo = GetawayRunState.MaximumAmmo;
        private float nextShot;
        private readonly List<ShotVisual> shots = new List<ShotVisual>();
        public int ShotsFired { get; private set; }
        public float LastShotAt { get; private set; } = float.NegativeInfinity;
        public int Ammo => Escape ? WorldShiftFlow.Escape.Ammo : practiceAmmo;
        private bool Escape => city != null ? city.GetawayMode : desert.GetawayMode;
        private bool Running => (city != null ? city.State : desert.State) == ShiftRunState.Running;
        private float Distance => city != null ? city.Distance : desert.Distance;
        private float Lateral => city != null ? city.Lateral : desert.Lateral;
        private float Duration => city != null ? city.Duration : desert.Duration;
        private bool Airborne => city != null ? city.IsRelayAirborne : desert.IsAirborne;
        private ShiftWorld World => city != null ? city.World : desert.World;
        private WorldShiftRoute Route => city != null ? city.Route : desert.Route;
        private Transform Player => city != null ? city.Player : desert.Player;
        private WorldShiftGetaway Game => city != null ? city.Getaway : desert.Getaway;
        public bool IsDestroyed(object key) => key != null && destroyed.Contains(key);
        public string Hint
        {
            get
            {
                var target = FindTarget();
                if (target.Key == null || !target.Breakable) return "";
                if (Ammo > 0) return "F or click to break it.";
                return target.Key is WorldShiftForkRoad.Blocker ? "Steer around it." : "Steer around it or tap Space.";
            }
        }
        public void Bind(WorldShiftCity game) { city = game; Initialize(); }
        public void Bind(DesertShiftTrial game) { desert = game; Initialize(); }
        private void LateUpdate() => Present();
        private void Initialize()
        {
            if (city != null && city.TutorialLevel) return;
            AddAmmoPickup(780f, 0f);
            if (Escape) AddAmmoPickup((city != null ? WorldShiftLevel.Length : DesertShiftLevel.Length) + 140f, -2.8f);
        }
        private void AddAmmoPickup(float at, float x)
        {
            var p = Route.Sample(at); var root = new GameObject("Ammo refill - three rounds").transform;
            root.SetParent(transform, false); root.SetPositionAndRotation(p.Position + p.Right * x + p.Up * 1.4f, Quaternion.LookRotation(p.Forward, p.Up));
            for (int i = 0; i < 3; i++)
            {
                var round = GameObject.CreatePrimitive(PrimitiveType.Capsule); round.name = "Ammo round"; round.transform.SetParent(root, false);
                round.transform.localPosition = new Vector3((i - 1) * .38f, 0f, 0f); round.transform.localScale = new Vector3(.2f, .5f, .2f);
                round.GetComponent<Collider>().enabled = false; round.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(Color.white);
            }
            pickups.Add(new AmmoPickup { At = at, X = x, Visual = root });
        }
        public void ResetPractice()
        {
            if (Escape) return;
            practiceAmmo = GetawayRunState.MaximumAmmo; ShotsFired = 0; destroyed.Clear(); nextShot = 0f;
            ClearShots();
            LastShotAt = float.NegativeInfinity;
            foreach (var pickup in pickups) pickup.Taken = false;
        }
        public bool TryShoot()
        {
            if (city != null && city.TutorialLevel && (!city.TutorialShootingActive || World != ShiftWorld.Day
                || IsDestroyed(WorldShiftTutorial.ShootingObstacle))) return false;
            if (!Running || Ammo <= 0 || Duration < nextShot) return false;
            if (Escape) { if (!WorldShiftFlow.Escape.SpendShot()) return false; }
            else practiceAmmo--;
            nextShot = Duration + .18f; ShotsFired++;
            LastShotAt = Duration;
            var target = FindTarget();
            if (target.Key != null && target.Breakable) destroyed.Add(target.Key);
            float end = target.Key != null ? target.At : Distance + OutlawProjectile.MaximumRange;
            float x = target.Key != null ? target.X : Lateral;
            var driver = Player.GetComponent<AutoDriveCar>();
            Transform frame = driver != null ? driver.FiringFrame : Player;
            Vector3 muzzle = frame.position + frame.forward * 2.2f + frame.up * .55f;
            var points = new List<Vector3> { muzzle };
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f, d = Mathf.Lerp(Distance + 2f, end, t), lane = Mathf.Lerp(Lateral, x, t);
                if (Airborne && desert != null)
                {
                    var flight = desert.DriverPoint(d);
                    points.Add(flight.Position + flight.Right * lane + flight.Up * 1.1f);
                }
                else
                {
                    float height = Airborne ? Mathf.Max(1.1f, Player.position.y - Route.Sample(Distance).Position.y + .8f) : 1.1f;
                    points.Add(Position(d, lane) + Route.Sample(d).Up * height);
                }
            }
            var bullet = OutlawProjectile.CreateVisual(muzzle);
            bullet.transform.SetParent(transform, true);
            bullet.GetComponent<Collider>().enabled = false;
            shots.Add(new ShotVisual { Body = bullet.transform, Path = points.ToArray(), Started = Duration });
            Present();
            if (city != null) city.PresentWorldForCamera(World); else desert.PresentWorldForCamera(World);
            return true;
        }
        private Target FindTarget()
        {
            Target best = new Target { At = Distance + 70f };
            if (city != null)
            {
                foreach (var h in city.Hazards)
                {
                    if (city.TutorialLevel && (!city.TutorialShootingActive || h != WorldShiftTutorial.ShootingObstacle)) continue;
                    if (IsDestroyed(h) || h.World != World || Airborne || h.Kind == ShiftHazardKind.Gap || h.Kind == ShiftHazardKind.Fire || h.Kind == ShiftHazardKind.Spikes) continue;
                    h.BoundsAt(Distance, out float at, out float end);
                    float x = Lateral;
                    bool occupied = h.Kind == ShiftHazardKind.HalfBarrier ? h.HalfSide < 0 ? Lateral < .7f : Lateral > -.7f
                        : (h.LaneMask & (1 << Mathf.Clamp(Mathf.RoundToInt(Lateral / (WorldShiftLevel.RoadWidth / 3f)) + 1, 0, 2))) != 0;
                    if (!occupied) continue;
                    if (h.Kind == ShiftHazardKind.HalfBarrier) x = Mathf.Clamp(Lateral, h.HalfSide < 0 ? -4f : .5f, h.HalfSide < 0 ? -.5f : 4f);
                    Offer(ref best, h, at, x, h.Kind == ShiftHazardKind.Barrier || h.Kind == ShiftHazardKind.HalfBarrier);
                }
            }
            else
            {
                foreach (var rock in desert.Rocks)
                    if (!IsDestroyed(rock) && rock.VisibleIn(World) && rock.Airborne == Airborne)
                        Offer(ref best, rock, rock.Distance - rock.Radius, desert.RockLateral(rock, World), true, rock.Radius + .7f);
                if (!Airborne) foreach (var gate in desert.ActiveGates)
                {
                    if (IsDestroyed(gate) || gate.World != World) continue;
                    if (gate.HalfSide < 0 && Lateral > .7f || gate.HalfSide > 0 && Lateral < -.7f) continue;
                    Offer(ref best, gate, gate.Start, Lateral, true);
                }
                if (!Airborne) foreach (var car in desert.Traffic)
                    Offer(ref best, car, car.Position(World, false) - DesertShiftTraffic.HalfLength,
                        car.Lane * WorldShiftLevel.RoadWidth / 3f, false, 1.6f);
            }
            if (!Airborne && Game?.Routing != null) foreach (var block in Game.Routing.Blockers)
                if (!IsDestroyed(block) && Game.Routing.Branch(block.At, Lateral) > 0)
                    Offer(ref best, block, block.At - .7f, block.X, true, 1.5f);
            return best;
        }
        private void Offer(ref Target best, object key, float at, float x, bool breakable, float aim = 1.6f)
        {
            if (at < Distance + 2f || at >= best.At || Mathf.Abs(x - Lateral) > aim) return;
            // Never auto-aim across the gap into a different fork branch.
            if (Game?.Routing != null && WorldShiftForkRoad.Owns(Game.Routing.EnvironmentEnd, at)
                && Game.Routing.Branch(at, Lateral) != 0 && Mathf.Sign(x) != Game.Routing.Branch(at, Lateral)) return;
            best = new Target { Key = key, At = at, X = x, Breakable = breakable };
        }
        private Vector3 Position(float d, float x) => Game?.Routing != null ? Game.Routing.Position(d, x) : Route.Sample(d).Position + Route.Sample(d).Right * x;
        public void Step(float from, float to, float a, float b)
        {
            if (!Running) return;
            foreach (var pickup in pickups)
            {
                if (pickup.Taken || to + 1.3f < pickup.At || from - 1.3f > pickup.At) continue;
                if (Game?.Routing != null && Game.Routing.RewardSeparated(pickup.At) && Game.Routing.RewardBranch >= 0) continue;
                float x = Mathf.Lerp(a, b, Mathf.InverseLerp(from, to, pickup.At));
                if (Mathf.Abs(x - pickup.X) > 1.35f) continue;
                bool accepted = Escape ? WorldShiftFlow.Escape.CollectAmmo() : practiceAmmo < GetawayRunState.MaximumAmmo;
                if (!accepted) continue;
                if (!Escape) practiceAmmo = GetawayRunState.MaximumAmmo;
                pickup.Taken = true;
            }
            Present();
        }
        public void Present()
        {
            foreach (var pickup in pickups)
            {
                pickup.Visual.gameObject.SetActive(!pickup.Taken && pickup.At > Distance - 8f && pickup.At < Distance + 170f);
                pickup.Visual.position = Position(pickup.At, pickup.X) + Route.Sample(pickup.At).Up * 1.4f;
            }
            if (!Running && (city != null ? city.State : desert.State) != ShiftRunState.Paused) { ClearShots(); return; }
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                float remaining = Mathf.Max(0f, Duration - shot.Started) * OutlawProjectile.Speed;
                bool flying = false;
                for (int segment = 1; segment < shot.Path.Length; segment++)
                {
                    float length = Vector3.Distance(shot.Path[segment - 1], shot.Path[segment]);
                    if (remaining <= length)
                    {
                        shot.Body.position = Vector3.Lerp(shot.Path[segment - 1], shot.Path[segment], length > .001f ? remaining / length : 1f);
                        flying = true; break;
                    }
                    remaining -= length;
                }
                if (!flying) { Destroy(shot.Body.gameObject); shots.RemoveAt(i); }
            }
        }
        private void ClearShots()
        {
            foreach (var shot in shots) Destroy(shot.Body.gameObject);
            shots.Clear();
        }
        public static void MarkBreakable(Transform parent, Vector3 position)
        {
            var line = new GameObject("Breakable crack symbol", typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false); line.transform.localPosition = position; line.useWorldSpace = false;
            line.sharedMaterial = RuntimeMaterials.Coloured(Color.white); line.widthMultiplier = .12f;
            line.positionCount = 5; line.SetPositions(new[] { new Vector3(-.3f, .5f, 0f), new Vector3(.15f, .2f, 0f),
                new Vector3(-.15f, 0f, 0f), new Vector3(.25f, -.25f, 0f), new Vector3(-.1f, -.5f, 0f) });
        }
    }
}
