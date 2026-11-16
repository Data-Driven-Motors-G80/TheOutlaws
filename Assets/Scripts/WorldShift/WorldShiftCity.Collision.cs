using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftCity
    {
        private float ResolveDividers(float from, float to, float previousLateral, float lateral, float steer)
        {
            float clearance = WorldShiftLevel.RoadWidth / 6f + .8f + .03f;
            foreach (var hazard in Hazards)
            {
                if (!WorldShiftCombat.IsActive(Combat, hazard, hazard.VisibleIn(World)) || hazard.Kind != ShiftHazardKind.Divider
                    || to + 1.5f < hazard.Start || from - 1.5f > hazard.End || Mathf.Abs(lateral) >= clearance) continue;
                float side = Mathf.Abs(previousLateral) > .1f ? Mathf.Sign(previousLateral)
                    : Mathf.Abs(steer) > .1f ? Mathf.Sign(steer) : 1f;
                lateral = side * clearance;
                driver.ConstrainExternalLateral(lateral);
            }
            return lateral;
        }

        private void CollectFuel(float from, float to, float fromLateral, float toLateral)
        {
            if (GetawayMode)
                for (int i = 0; i < fuelPassed.Length; i++)
                    if (!fuelTaken[i] && !fuelPassed[i] && from > Pickups[i].Distance + 1.3f) { fuelPassed[i] = true; WorldShiftAnalytics.Missed("fuel"); }
            if (GetawayMode && World != ShiftWorld.Day) return;
            for (int i = 0; i < fuelTaken.Length; i++)
            {
                ShiftFuelPickup pickup = Pickups[i];
                if (fuelTaken[i] || (!GetawayMode && !IsWorldVisible(pickup.World, pickup.Distance, pickup.Lane * WorldShiftLevel.RoadWidth / 3f, World)) || to + 1.3f < pickup.Distance || from - 1.3f > pickup.Distance) continue;
                float t = Mathf.InverseLerp(from, to, pickup.Distance);
                if (Mathf.Abs(Mathf.Lerp(fromLateral, toLateral, t) - pickup.Lane * WorldShiftLevel.RoadWidth / 3f) > 1.35f) continue;
                fuelTaken[i] = true;
                FuelCollected++;
                WorldShiftAnalytics.Fuel();
                fuel.Refill(WorldShiftLevel.FuelRefill);
                presentation.RefreshFuel();
                Notify("FUEL +26", 1.2f);
            }
        }

        // Swept checks in the common road coordinate prevent tunnelling or switching inside a wall.
        private ShiftHazard FindCollision(ShiftWorld world, float from, float to, float fromLateral, float toLateral, bool skipHandled = false,
            float fromHeight = float.NaN, float toHeight = float.NaN)
        {
            const float halfWidth = 0.8f;
            foreach (var hazard in Hazards)
            {
                bool merged = IsJoinedDistance(hazard.Start);
                if (!WorldShiftCombat.IsActive(Combat, hazard)) continue;
                if ((!merged && !hazard.VisibleIn(world)) || (skipHandled && hitHazards.Contains(hazard))) continue;
                if (hazard.Gate != null)
                {
                    if (hazard.Gate.Airborne != IsRelayAirborne) continue;
                    float aHeight = float.IsNaN(fromHeight) ? Player.position.y : fromHeight;
                    float bHeight = float.IsNaN(toHeight) ? Player.position.y : toHeight;
                    if (hazard.Gate.Touches(from, to, fromLateral, toLateral, aHeight, bHeight,
                        Mathf.Abs(to - from) < .00001f ? hazard.Gate.Lateral : hazard.PreviousLateralOffset,
                        LevelGatePosition(hazard.Gate).y)) return hazard;
                    continue;
                }
                if (hazard.Rock != null)
                {
                    if (hazard.Rock.Airborne != IsRelayAirborne) continue;
                    float aHeight = float.IsNaN(fromHeight) ? Player.position.y : fromHeight;
                    float bHeight = float.IsNaN(toHeight) ? Player.position.y : toHeight;
                    if (hazard.Rock.Touches(from, to, fromLateral, toLateral, aHeight, bHeight,
                        Mathf.Abs(to - from) < .00001f ? hazard.Rock.Lateral : hazard.PreviousLateralOffset,
                        LevelRockPosition(hazard.Rock).y)) return hazard;
                    continue;
                }
                if (IsRelayAirborne) continue;
                // A missing deck drops the car when its centre crosses the lip.
                float halfLength = hazard.Kind == ShiftHazardKind.Gap ? 0f : 1.5f;
                hazard.BoundsAt(from, out float start, out float end);
                hazard.BoundsAt(to, out float nextStart, out float nextEnd);
                if (hazard.Kind == ShiftHazardKind.Pedestrians)
                {
                    // The authored six-metre crossing is road paint, not a solid
                    // wall. Contact belongs to the figures standing near its centre.
                    float centre = (start + end) * .5f;
                    float nextCentre = (nextStart + nextEnd) * .5f;
                    start = centre - .65f;
                    end = centre + .65f;
                    nextStart = nextCentre - .65f;
                }
                float relativeFrom = from - start, relativeTo = to - nextStart;
                float depth = end - start;
                if (relativeTo + halfLength < 0f || relativeFrom - halfLength > depth) continue;
                float entry = relativeTo > relativeFrom ? Mathf.Clamp01((-halfLength - relativeFrom) / (relativeTo - relativeFrom)) : 0f;
                float exit = relativeTo > relativeFrom ? Mathf.Clamp01((depth + halfLength - relativeFrom) / (relativeTo - relativeFrom)) : 1f;
                float a = Mathf.Lerp(fromLateral, toLateral, entry), b = Mathf.Lerp(fromLateral, toLateral, exit);
                if (hazard.LateralMotion)
                {
                    float obstacleHalfWidth = LevelMode ? WorldShiftSpikePlatform.Width * .5f : .8f;
                    float left = Mathf.Min(hazard.PreviousLateralOffset, hazard.LateralOffset) - obstacleHalfWidth - halfWidth;
                    float right = Mathf.Max(hazard.PreviousLateralOffset, hazard.LateralOffset) + obstacleHalfWidth + halfWidth;
                    if (Mathf.Max(a, b) >= left && Mathf.Min(a, b) <= right) return hazard;
                    continue;
                }
                if (hazard.Kind == ShiftHazardKind.HalfBarrier)
                {
                    float min = hazard.HalfSide < 0 ? -WorldShiftLevel.RoadWidth * .5f : 0f;
                    float max = hazard.HalfSide < 0 ? 0f : WorldShiftLevel.RoadWidth * .5f;
                    if (merged && hazard.World != WorldShiftLevel.WorldAt(hazard.HalfSide)) continue;
                    if (Mathf.Max(a, b) + halfWidth > min && Mathf.Min(a, b) - halfWidth < max) return hazard;
                    continue;
                }
                for (int lane = 0; lane < 3; lane++)
                {
                    if ((hazard.LaneMask & (1 << lane)) == 0) continue;
                    float centre = (lane - 1) * WorldShiftLevel.RoadWidth / 3f;
                    float extent = WorldShiftLevel.RoadWidth / 6f + halfWidth;
                    if (Mathf.Max(a, b) >= centre - extent && Mathf.Min(a, b) <= centre + extent) return hazard;
                }
            }
            return null;
        }
    }
}
