using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Owns gameplay prompt priority and timing, independently of HUD rendering.</summary>
    public sealed class WorldShiftDrivingMessages
    {
        private readonly WorldShiftGetaway game;
        public WorldShiftDrivingMessages(WorldShiftGetaway owner) { game = owner; }
        private float Distance => game.Distance;
        private float Duration => game.Duration;
        private ShiftWorld World => game.World;
        private ShiftRunState State => game.State;
        private WorldShiftForkRoad Routing => game.Routing;
        private WorldShiftGetawayExtras Extras => game.Extras;
        private WorldShiftCombat Combat => game.Combat;
        public const float NightReminderDelay = 12f, NightReminderInterval = 45f;
        public const string OpeningPrompt = "Collect cash. Bank it at a marked cash-out exit.";
        private const string NightReminderText = "Fuel still drains. Return to Day for supplies.";
        private const string ShootPrompt = "F / click to shoot the block.";
        private float lastActionPromptAt = float.NegativeInfinity;
        private float shootCandidateSince = float.PositiveInfinity;
        private float shootLastEligibleAt = float.NegativeInfinity;
        private float shootShownAt = float.PositiveInfinity;
        private bool shootHintFinished, openingGoalDismissed;
        private float nightEnteredAt = float.PositiveInfinity;
        public void OnWorldChanged()
            => nightEnteredAt = World == ShiftWorld.Echo ? Duration : float.PositiveInfinity;
        private string NightReminder
        {
            get
            {
                if (World != ShiftWorld.Echo || State != ShiftRunState.Running || game.InRecovery) return "";
                float elapsed = Duration - nightEnteredAt - NightReminderDelay;
                if (elapsed < 0f) return "";
                float phase = Mathf.Repeat(elapsed, NightReminderInterval);
                return phase < 6f ? NightReminderText : "";
            }
        }
        public string DrivingPrompt()
        {
            // One owner and one priority order. Context-critical instructions never queue behind old tips.
            if (game.InExit && !game.CashOutAvailable) return ActionPrompt(game.NextEnvironmentName + " ahead.");
            string jump = game.JumpHint;
            if (jump.Length > 0) return ActionPrompt(jump);
            string switchWall = game.SwitchWallHint;
            if (switchWall.Length > 0) return ActionPrompt(switchWall);
            if (game.Environment == ShiftLevel.Snow && Duration < 6f && Distance < 120f)
                return ActionPrompt("Snow: steer early. Countersteer to catch a slide.");
            if (Distance >= Routing.ForkStart - 100f && Distance < Routing.ChoiceAt)
                return ActionPrompt(""); // Overhead route banners own this choice; keep the HUD quiet.
            if (Distance >= Extras.Jump.Start - 100f && Distance < Extras.Jump.Start && !Extras.Jump.Committed)
                return ActionPrompt("Right ramp: bonus cash in Day. Left: stay on the road.");
            string shoot = ShootingPrompt();
            if (shoot.Length > 0) return ActionPrompt(shoot);
            // Give the player a quiet beat between an action and optional information.
            if (Duration - lastActionPromptAt < 1f) return "";
            if (NightReminder.Length > 0) return NightReminder;
            if (!openingGoalDismissed && game.CurrentLeg == 0 && World == ShiftWorld.Day && Duration < 5f && Distance < 150f)
                return OpeningPrompt;
            return "";
        }

        private string ActionPrompt(string text)
        {
            openingGoalDismissed = true;
            lastActionPromptAt = Duration;
            return text;
        }

        private string ShootingPrompt()
        {
            if (shootHintFinished || Combat == null) return "";
            if (Combat.ShotsFired > 0) { shootHintFinished = true; return ""; }
            if (Combat.Ammo <= 0) return "";
            if (Combat.Hint == "Click to shoot.")
            {
                shootLastEligibleAt = Duration;
                if (float.IsPositiveInfinity(shootCandidateSince)) shootCandidateSince = Duration;
                if (Duration - shootCandidateSince < .4f) return "";
                if (float.IsPositiveInfinity(shootShownAt)) shootShownAt = Duration;
                if (Duration - shootShownAt < 4f) return ShootPrompt;
                shootHintFinished = true;
            }
            else
            {
                // Brief aim changes must not make the caption alternate every frame.
                if (!float.IsPositiveInfinity(shootShownAt) && Duration - shootLastEligibleAt < .35f) return ShootPrompt;
                if (!float.IsPositiveInfinity(shootShownAt)) shootHintFinished = true;
                shootCandidateSince = float.PositiveInfinity;
            }
            return "";
        }

    }
}
