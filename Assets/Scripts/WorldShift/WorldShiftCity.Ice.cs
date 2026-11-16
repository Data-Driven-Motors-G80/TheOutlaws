using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Black ice on authored course stretches (<see cref="LevelCourse.IceZones"/>). The car keeps its forward
    /// speed, but sideways control changes: steering builds a slide slowly, letting go keeps the slide going,
    /// and only countersteering brakes it. Bends also push a sliding car wide (AutoDriveCar's drift scales
    /// with low grip). Everything stays deterministic so a retry plays exactly the same.
    /// </summary>
    public sealed partial class WorldShiftCity
    {
        // Fractions of normal snow grip (AutoDriveCar lateral acceleration).
        public const float IceSteeringGrip = .2f;
        public const float IceReleaseGrip = .05f;
        public const float IceCountersteerGrip = .38f;

        /// <summary>True while the car is on the ground on a stretch of ice.</summary>
        public bool OnIce { get; private set; }

        private void ApplySurfaceGrip(float distance)
        {
            float baseGrip = GetawayMode || LevelMode ? WorldShiftEscapeProfile.SteeringGrip(environment) : 1f;
            OnIce = LevelMode && course != null && !IsRelayAirborne && course.IceAt(distance);
            if (OnIce)
            {
                // AutoDriveCar multiplies steering grip by the release/countersteer factor, so express those
                // as ratios of the ice steering grip.
                driver.EnvironmentSteeringGrip = baseGrip * IceSteeringGrip;
                driver.EnvironmentReleaseGrip = IceReleaseGrip / IceSteeringGrip;
                driver.EnvironmentCountersteerGrip = IceCountersteerGrip / IceSteeringGrip;
                return;
            }
            bool snowSlide = GetawayMode && WorldShiftEscapeProfile.SnowSlide(environment, distance, RelayLayout);
            driver.EnvironmentSteeringGrip = baseGrip;
            driver.EnvironmentReleaseGrip = snowSlide ? .6f : 1f;
            driver.EnvironmentCountersteerGrip = snowSlide ? 1.25f : 1f;
        }
    }
}
