using UnityEngine;

namespace Outlaws.WorldShift
{
    // Approach traffic is shared between worlds, just like the rolling rocks.
    // Night freezes its current position; entering the rift preserves a Night
    // snapshot while the complete Day view continues to advance independently.
    public sealed class DesertShiftTraffic
    {
        public readonly float Start;
        public readonly int Lane;
        public const float Speed = 14f, HalfLength = 2.75f, HalfWidth = 1.15f;
        public float Travel { get; private set; }
        public float NightTravel { get; private set; }
        public DesertShiftTraffic(float start, int lane) { Start = start; Lane = lane; }
        public float Position(ShiftWorld world, bool joined) => Start - (joined && world == ShiftWorld.Echo ? NightTravel : Travel);
        public void Advance(float delta, ShiftWorld world, float playerDistance)
        {
            if (world == ShiftWorld.Day && playerDistance >= Start - 160f) Travel += Speed * delta;
        }
        public void CaptureNight() => NightTravel = Travel;
        public void Restore(float travel, float nightTravel) { Travel = travel; NightTravel = nightTravel; }
    }

    public static class WorldShiftTrafficVisual
    {
        // Same police prefab and facing used by City; no pursuit behaviour or
        // physical collider can run on these manually simulated obstacle cars.
        public static GameObject Create(GameObject prefab, Transform parent, float lateral)
        {
            var car = Object.Instantiate(prefab, parent);
            car.name = "Oncoming police - faces player";
            car.transform.localPosition = new Vector3(lateral, .03f, 0f);
            car.transform.localScale = Vector3.one * 1.15f;
            car.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (Collider collider in car.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (MonoBehaviour script in car.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
            foreach (Light lamp in car.GetComponentsInChildren<Light>()) lamp.enabled = false;
            return car;
        }
    }
}
