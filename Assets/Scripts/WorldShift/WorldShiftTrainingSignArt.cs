using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>The road sign and enlarged lesson preview share the same authored artwork.</summary>
    internal static class WorldShiftTrainingSignArt
    {
        public static string Description(LevelSignKind kind)
        {
            switch (kind)
            {
                case LevelSignKind.Steer:
                    return "Hold A / D or ARROW KEYS to steer.\nYou drive forward automatically.";
                case LevelSignKind.Switch:
                    return "Press SPACE to switch Day / Night.\nSwitch worlds to lower the barrier.";
                case LevelSignKind.Oncoming:
                    return "Steer into a clear lane with A / D or ARROW KEYS.\nOne hit means a retry.";
                case LevelSignKind.Freeze:
                    return "SPACE switches worlds: Day moves, Night freezes.\nOrange obstacles stay solid.";
                case LevelSignKind.AirFreeze:
                    return "Press SPACE to switch to Night.\nMoving obstacles freeze and stay solid.";
                case LevelSignKind.AirRelease:
                    return "Press SPACE to switch to Day.\nFrozen obstacles move again.";
                default:
                    return "Take off in Day. Press SPACE in the air.\nLand in Night.";
            }
        }

        public static Texture2D Load(string resource)
        {
            if (resource == null) return null;
            const string prefix = "CurrentTraining";
            return Resources.Load<Texture2D>(resource.StartsWith(prefix)
                ? "WorldShiftTrainingSigns/" + resource.Substring(prefix.Length) : resource);
        }
    }
}
