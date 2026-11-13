using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Shared vehicle art entry point for every finite-route level.</summary>
    public static class WorldShiftBlockVisuals
    {
        public static GameObject Vehicle(Transform parent, bool police)
            => police ? WorldShiftPoliceCarArt.Build(parent) : WorldShiftPlayerCarArt.Build(parent);
    }
}
