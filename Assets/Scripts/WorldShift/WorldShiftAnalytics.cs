using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Playtest analytics for the WorldShift escape and tutorial (Docs/ANALYTICS.md, "WorldShift").
    /// Gameplay code reports moments here; this class keeps the per-leg and per-run tallies and
    /// writes ws_runs, ws_legs and ws_tutorials rows plus ws_* events through WeeWooAnalytics.
    /// </summary>
    public static class WorldShiftAnalytics
    {
        // Impacts and deaths this soon after a switch are treated as caused by the switch.
        public const float SwitchBlameSeconds = 1.5f;

        // Every pickup on the player's route is either taken or passed; a pass in Night could not have been taken.
        public static readonly string[] PickupKinds = { "cash", "cash_ramp", "fuel", "ammo", "shield", "magnet", "boost" };
        private const int TakenSlot = 0, MissedSlot = 1, NightSlot = 2;

        private sealed class Tally
        {
            public int Switches, AirSwitches, Impacts, ImpactsAfterSwitch, ShieldSaves, Shots, ShotHits, LootBoxes, LootValue, Fuel, Ammo;
            public int Pauses, AmmoFullTouches;
            public float DaySeconds, NightSeconds, DriveSeconds;
            public readonly int[,] Pickups = new int[PickupKinds.Length, 3];
            public Tally Copy() { var copy = new Tally(); copy.Add(this); return copy; }
            public void Add(Tally other)
            {
                Switches += other.Switches; AirSwitches += other.AirSwitches; Impacts += other.Impacts; ImpactsAfterSwitch += other.ImpactsAfterSwitch;
                ShieldSaves += other.ShieldSaves; Shots += other.Shots; ShotHits += other.ShotHits;
                LootBoxes += other.LootBoxes; LootValue += other.LootValue; Fuel += other.Fuel; Ammo += other.Ammo;
                Pauses += other.Pauses; AmmoFullTouches += other.AmmoFullTouches;
                DaySeconds += other.DaySeconds; NightSeconds += other.NightSeconds; DriveSeconds += other.DriveSeconds;
                for (int k = 0; k < PickupKinds.Length; k++)
                    for (int j = 0; j < 3; j++) Pickups[k, j] += other.Pickups[k, j];
            }
            public void Write(Dictionary<string, object> row)
            {
                row["switches"] = Switches;
                row["switches_per_min"] = DriveSeconds > 0f ? Round(Switches / (DriveSeconds / 60f)) : 0f;
                row["day_s"] = Round(DaySeconds);
                row["night_s"] = Round(NightSeconds);
                row["night_pct"] = DaySeconds + NightSeconds > 0f ? Mathf.Round(100f * NightSeconds / (DaySeconds + NightSeconds)) : -1f;
                row["impacts"] = Impacts;
                row["impacts_after_switch"] = ImpactsAfterSwitch;
                row["shield_saves"] = ShieldSaves;
                row["shots"] = Shots;
                row["shot_hits"] = ShotHits;
                row["loot_boxes"] = LootBoxes;
                row["loot_value"] = LootValue;
                row["fuel_pickups"] = Fuel;
                row["ammo_pickups"] = Ammo;
                row["air_switches"] = AirSwitches;
                row["pauses"] = Pauses;
                row["ammo_full_touches"] = AmmoFullTouches;
                int taken = 0, offered = 0;
                for (int k = 0; k < PickupKinds.Length; k++)
                {
                    row[PickupKinds[k] + "_taken"] = Pickups[k, TakenSlot];
                    row[PickupKinds[k] + "_missed"] = Pickups[k, MissedSlot];
                    row[PickupKinds[k] + "_missed_night"] = Pickups[k, NightSlot];
                    taken += Pickups[k, TakenSlot]; offered += Pickups[k, TakenSlot] + Pickups[k, MissedSlot];
                }
                row["pickups_taken_pct"] = offered > 0 ? Mathf.Round(100f * taken / offered) : -1f;
            }
        }

        private sealed class Death
        {
            public string Reason = "", World = "", LastImpact = "";
            public float Distance, SinceSwitch = -1f, SinceImpact = -1f, FuelPct = -1f, Gap = -1f;
        }

        // Where the player is right now; supplied by whichever scene is driving.
        private static Func<float> distance, duration, fuel, gap;
        private static Func<ShiftWorld> world;
        private static string mode = "", environment = "";

        private static string runId;
        private static int runNumber, leg, legsStarted, legsCleared, forksLeft, forksRight;
        private static bool tutorialFirst;
        private static int upgradeFuel, upgradeArmor, upgradeLights;
        private static float runStartRealtime;
        private static readonly List<string> powerUps = new List<string>();
        private static Tally run = new Tally(), current = new Tally();
        private static bool legOpen, legCashOut;
        private static float legLength, worldSince, lastSwitch, lastImpact, endDistance = -1f;
        private static int fork, legTier;
        private static string legPowerUp = "", lastImpactKind = "";
        private static Death death;

        private static bool tutorialOpen;
        private static int tutorialStep, tutorialAttempt;
        private static float stepStarted, tutorialStartRealtime;
        private static float[] stepSeconds;
        private static int[] stepRetries;

        /// <summary>Test seam: receives every event and row instead of the uploader when set.</summary>
        internal static Action<string, string, Dictionary<string, object>> Sink;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            distance = duration = fuel = gap = null; world = null; mode = environment = "";
            runId = null; runNumber = tutorialAttempt = 0; legOpen = tutorialOpen = false; Sink = null;
        }

        public static void MenuShown() => Event("ws_menu", "banked_total", WorldShiftFlow.Banked, "best_haul", WorldShiftFlow.BestHaul);
        public static void GarageShown() => Event("ws_garage", "banked_total", WorldShiftFlow.Banked, "upgrade_total", UpgradeTotal);

        public static void UpgradeBought(VehicleUpgrades.Part part, int price)
            => Event("ws_upgrade", "part", part.ToString(), "level", WorldShiftFlow.Garage.Level(part), "price", price,
                "banked_total", WorldShiftFlow.Banked, "upgrade_total", UpgradeTotal);

        private static int UpgradeTotal => WorldShiftFlow.Garage.Level(VehicleUpgrades.Part.FuelTank)
            + WorldShiftFlow.Garage.Level(VehicleUpgrades.Part.Armor) + WorldShiftFlow.Garage.Level(VehicleUpgrades.Part.Headlights);

        // ----- Escape runs -----

        public static void BeginRun(bool withTutorial)
        {
            runId = Guid.NewGuid().ToString("N").Substring(0, 12);
            runNumber++;
            tutorialFirst = withTutorial;
            runStartRealtime = Time.realtimeSinceStartup;
            legsStarted = legsCleared = forksLeft = forksRight = 0;
            powerUps.Clear();
            run = new Tally();
            death = null;
            legOpen = false;
            endDistance = -1f;
            // Upgrades are bought between runs, so the levels at the start describe the whole run.
            upgradeFuel = WorldShiftFlow.Garage.Level(VehicleUpgrades.Part.FuelTank);
            upgradeArmor = WorldShiftFlow.Garage.Level(VehicleUpgrades.Part.Armor);
            upgradeLights = WorldShiftFlow.Garage.Level(VehicleUpgrades.Part.Headlights);
            Event("ws_run_start", "tutorial_first", withTutorial, "banked_total", WorldShiftFlow.Banked, "upgrade_total", UpgradeTotal);
        }

        public static void BeginLeg(string env, int legIndex, float length, Func<float> getDistance, Func<float> getDuration, Func<ShiftWorld> getWorld,
            Func<float> getFuel = null, Func<float> getGap = null)
        {
            if (runId == null) return;
            Attach("escape", env, getDistance, getDuration, getWorld);
            fuel = getFuel; gap = getGap;
            leg = legIndex; legLength = length; legsStarted++;
            // Cash-out is offered only on some legs, and later laps of the five environments get harder.
            legCashOut = WorldShiftFlow.CashOutAvailable; legTier = WorldShiftFlow.DifficultyTier;
            current = new Tally();
            legOpen = true; fork = 0; legPowerUp = ""; lastImpactKind = "";
            worldSince = 0f; lastSwitch = lastImpact = float.NegativeInfinity;
            Event("ws_leg_start", "carried", WorldShiftFlow.Escape.Carried, "loot_multiplier", WorldShiftFlow.Escape.LootMultiplier,
                "cashout_offered", legCashOut, "tier", legTier);
        }

        public static void Gate(bool continueRun)
        {
            if (!legOpen) return;
            // "through": the leg ended without a cash-out choice, so it is not a gate decision.
            string choice = !legCashOut ? "through" : continueRun ? "next" : "exit";
            Event("ws_gate", "choice", choice, "carried", WorldShiftFlow.Escape.Carried);
            legsCleared++;
            if (continueRun) EndLeg(choice);
        }

        public static void Banked(int haul) { if (runId != null) EndRun("banked", "", haul); }
        public static void Lost(string failure, int haul) { if (runId != null) EndRun("lost", failure, haul); }

        /// <summary>Closes whatever is open without a result: restart, back to menu.</summary>
        public static void Abandon(string why)
        {
            if (tutorialOpen) EndTutorial(why);
            if (runId != null) EndRun(why, "", WorldShiftFlow.Escape.Carried);
        }

        // ----- Moments reported by gameplay -----

        public static void Switched(ShiftWorld to, bool airborne = false)
        {
            if (!Tracking) return;
            if (airborne) current.AirSwitches++;
            float now = Now;
            float stayed = now - worldSince;
            if (to == ShiftWorld.Day) current.NightSeconds += stayed; else current.DaySeconds += stayed;
            worldSince = lastSwitch = now;
            current.Switches++;
            Event("ws_switch", "to", Name(to), "stayed_s", Round(stayed), "airborne", airborne, "carried", WorldShiftFlow.Escape.Carried);
        }

        public static void Impact(string kind)
        {
            if (!Tracking) return;
            float since = Now - lastSwitch;
            current.Impacts++;
            if (since <= SwitchBlameSeconds) current.ImpactsAfterSwitch++;
            lastImpact = Now; lastImpactKind = kind;
            Event("ws_impact", "kind", kind, "s_since_switch", Since(lastSwitch));
        }

        public static void ShieldSaved() { if (Tracking) { current.ShieldSaves++; Event("ws_shield_save"); } }
        public static void Shot(bool hit)
        {
            if (!Tracking) return;
            current.Shots++; if (hit) current.ShotHits++;
            Event("ws_shot", "hit", hit, "ammo_left", WorldShiftFlow.Escape.Ammo);
        }
        public static void Loot(int value, bool ramp = false)
        {
            if (!Tracking) return;
            current.LootBoxes++; current.LootValue += value;
            Pickup(ramp ? "cash_ramp" : "cash", true);
        }
        public static void Fuel() { if (Tracking) { current.Fuel++; Pickup("fuel", true); } }
        public static void Ammo() { if (Tracking) { current.Ammo++; Pickup("ammo", true); } }
        public static void AmmoFull() { if (Tracking) current.AmmoFullTouches++; }
        public static void Paused() { if (Tracking) { current.Pauses++; Event("ws_pause"); } }

        public static void PowerUp(string kind)
        {
            if (!Tracking) return;
            legPowerUp = kind; powerUps.Add(kind);
            Pickup(kind, true);
        }

        /// <summary>A pickup on the player's route was passed without being taken.</summary>
        public static void Missed(string kind) => Pickup(kind, false);

        private static void Pickup(string kind, bool taken)
        {
            if (!Tracking || mode != "escape") return;
            int k = Array.IndexOf(PickupKinds, kind);
            if (k < 0) return;
            bool night = world() != ShiftWorld.Day;
            current.Pickups[k, taken ? TakenSlot : MissedSlot]++;
            if (!taken && night) current.Pickups[k, NightSlot]++;
            Event("ws_pickup", "kind", kind, "taken", taken);
        }

        public static void Fork(int branch)
        {
            if (!Tracking || mode != "escape") return;
            fork = branch;
            if (branch > 0) forksRight++; else forksLeft++;
            Event("ws_fork", "branch", branch > 0 ? "right" : "left");
        }

        public static void Died(string reason)
        {
            if (!Tracking) return;
            death = new Death
            {
                Reason = reason, World = Name(world()), Distance = Round(distance()),
                SinceSwitch = Since(lastSwitch), SinceImpact = Since(lastImpact), LastImpact = lastImpactKind,
                FuelPct = fuel != null ? Mathf.Round(100f * fuel()) : -1f, Gap = gap != null ? Round(gap()) : -1f
            };
            if (tutorialOpen && tutorialStep < stepRetries.Length) stepRetries[tutorialStep]++;
            Event("ws_death", "reason", reason, "s_since_switch", death.SinceSwitch, "s_since_impact", death.SinceImpact,
                "after_switch", death.SinceSwitch >= 0f && death.SinceSwitch <= SwitchBlameSeconds);
        }

        // ----- Tutorial -----

        public static void BeginTutorial(int steps, Func<float> getDistance, Func<float> getDuration, Func<ShiftWorld> getWorld)
        {
            if (tutorialOpen) EndTutorial("restarted");
            Attach("tutorial", "Tutorial", getDistance, getDuration, getWorld);
            tutorialOpen = true; tutorialAttempt++; tutorialStep = 0; stepStarted = 0f;
            tutorialStartRealtime = Time.realtimeSinceStartup;
            stepSeconds = new float[steps]; stepRetries = new int[steps];
            current = new Tally(); worldSince = 0f; lastSwitch = lastImpact = float.NegativeInfinity; lastImpactKind = "";
            Event("ws_tutorial_start", "attempt", tutorialAttempt, "then_escape", runId != null);
        }

        public static void TutorialStep(int step, string title)
        {
            if (!tutorialOpen || step <= tutorialStep || step >= stepSeconds.Length) return;
            stepSeconds[tutorialStep] += Now - stepStarted;
            Event("ws_tutorial_step", "step", step, "title", title, "prev_step_s", Round(stepSeconds[tutorialStep]), "prev_step_retries", stepRetries[tutorialStep]);
            tutorialStep = step; stepStarted = Now;
        }

        public static void EndTutorial(string status)
        {
            if (!tutorialOpen) return;
            Row("ws_tutorials", TutorialRow(status));
            Event("ws_tutorial_end", "status", status, "step", tutorialStep);
            tutorialOpen = false;
            if (mode == "tutorial") Detach();
            WeeWooAnalytics.Flush();
        }

        // ----- Rows -----

        private static void EndLeg(string outcome, string failure = "")
        {
            if (!legOpen) return;
            current = LegSoFar();
            endDistance = Round(distance());
            Row("ws_legs", LegRow(current, outcome, failure));
            run.Add(current);
            legOpen = false;
            // The next leg is a new scene; never call into this one again.
            Detach();
        }

        private static void EndRun(string outcome, string failure, int haul)
        {
            bool dead = outcome == "lost";
            if (legOpen) EndLeg(dead ? "lost" : outcome == "banked" ? "exit" : outcome, failure);
            Row("ws_runs", RunRow(run, outcome, failure, haul, dead));
            Event("ws_run_end", "outcome", outcome, "failure", failure, "haul", haul);
            runId = null;
            WeeWooAnalytics.Flush();
        }

        private static Dictionary<string, object> LegRow(Tally tally, string outcome, string failure)
        {
            var row = new Dictionary<string, object>
            {
                ["run_id"] = runId, ["leg"] = leg, ["env"] = environment, ["outcome"] = outcome, ["failure"] = failure,
                ["dist_m"] = Round(distance()), ["length_m"] = Round(legLength),
                ["progress_pct"] = legLength > 0f ? Mathf.Round(100f * Mathf.Clamp01(distance() / legLength)) : -1f,
                ["drive_s"] = Round(tally.DriveSeconds), ["fork"] = fork > 0 ? "right" : fork < 0 ? "left" : "none",
                ["powerup"] = legPowerUp, ["cashout_offered"] = legCashOut, ["tier"] = legTier, ["carried"] = WorldShiftFlow.Escape.Carried, ["loot_multiplier"] = WorldShiftFlow.Escape.LootMultiplier
            };
            tally.Write(row);
            WriteDeath(row, outcome == "lost");
            return row;
        }

        private static Dictionary<string, object> RunRow(Tally tally, string outcome, string failure, int haul, bool dead)
        {
            var row = new Dictionary<string, object>
            {
                ["run_id"] = runId, ["run_number_session"] = runNumber, ["tutorial_first"] = tutorialFirst,
                ["outcome"] = outcome, ["failure"] = failure, ["haul"] = haul,
                ["banked"] = outcome == "banked" ? haul : 0, ["lost"] = outcome == "banked" ? 0 : haul,
                ["legs_started"] = legsStarted, ["legs_cleared"] = legsCleared,
                ["end_env"] = environment, ["end_leg"] = leg, ["end_dist_m"] = distance != null ? Round(distance()) : endDistance,
                ["drive_s"] = Round(tally.DriveSeconds), ["wallclock_s"] = Round(Time.realtimeSinceStartup - runStartRealtime),
                ["forks_left"] = forksLeft, ["forks_right"] = forksRight, ["powerups"] = string.Join("|", powerUps),
                ["upgrade_fuel"] = upgradeFuel, ["upgrade_armor"] = upgradeArmor, ["upgrade_lights"] = upgradeLights,
                ["upgrade_total"] = upgradeFuel + upgradeArmor + upgradeLights,
                ["banked_total"] = WorldShiftFlow.Banked, ["best_haul"] = WorldShiftFlow.BestHaul
            };
            tally.Write(row);
            WriteDeath(row, dead);
            return row;
        }

        private static Dictionary<string, object> TutorialRow(string status)
        {
            var row = new Dictionary<string, object>
            {
                ["attempt"] = tutorialAttempt, ["status"] = status, ["step_reached"] = tutorialStep,
                ["step_title"] = tutorialStep < WorldShiftTutorial.Checkpoints.Length ? WorldShiftTutorial.Checkpoints[tutorialStep].Title : "",
                ["drive_s"] = Round(Now), ["wallclock_s"] = Round(Time.realtimeSinceStartup - tutorialStartRealtime),
                ["then_escape"] = runId != null, ["switches"] = current.Switches, ["impacts"] = current.Impacts, ["shots"] = current.Shots
            };
            float[] seconds = (float[])stepSeconds.Clone();
            if (tutorialStep < seconds.Length) seconds[tutorialStep] += Now - stepStarted;
            for (int i = 0; i < seconds.Length; i++)
            {
                string title = WorldShiftTutorial.Checkpoints[i].Title.ToLowerInvariant().Replace(' ', '_');
                row["s_" + i + "_" + title] = Round(seconds[i]);
                row["retries_" + i + "_" + title] = stepRetries[i];
            }
            return row;
        }

        private static void WriteDeath(Dictionary<string, object> row, bool dead)
        {
            Death d = dead ? death : null;
            row["death_reason"] = d?.Reason ?? "";
            row["death_world"] = d?.World ?? "";
            row["death_dist_m"] = d?.Distance ?? -1f;
            row["death_s_since_switch"] = d?.SinceSwitch ?? -1f;
            row["death_after_switch"] = d != null && d.SinceSwitch >= 0f && d.SinceSwitch <= SwitchBlameSeconds;
            row["death_s_since_impact"] = d?.SinceImpact ?? -1f;
            row["death_last_impact"] = d?.LastImpact ?? "";
            row["death_fuel_pct"] = d?.FuelPct ?? -1f;
            row["death_police_gap_m"] = d?.Gap ?? -1f;
        }

        // ----- Plumbing -----

        private static bool Tracking => (legOpen || tutorialOpen) && distance != null;
        private static float Now => duration != null ? duration() : 0f;
        private static float Since(float at) => float.IsNegativeInfinity(at) ? -1f : Round(Now - at);
        private static float Round(float value) => Mathf.Round(value * 10f) / 10f;
        private static string Name(ShiftWorld w) => w == ShiftWorld.Day ? "day" : "night";

        // The open leg's tally with time in the current world counted up to now.
        private static Tally LegSoFar()
        {
            Tally tally = current.Copy();
            float stayed = Now - worldSince;
            if (world() == ShiftWorld.Day) tally.DaySeconds += stayed; else tally.NightSeconds += stayed;
            tally.DriveSeconds = Now;
            return tally;
        }

        private static void Attach(string newMode, string env, Func<float> getDistance, Func<float> getDuration, Func<ShiftWorld> getWorld)
        {
            mode = newMode; environment = env; distance = getDistance; duration = getDuration; world = getWorld; fuel = gap = null;
            WeeWooAnalytics.EnsureStarted();
            WeeWooAnalytics.ContextProvider = Context;
            WeeWooAnalytics.ExitRowsProvider = ExitRows;
        }

        private static void Detach()
        {
            mode = ""; distance = duration = fuel = gap = null; world = null;
            if (WeeWooAnalytics.ContextProvider == (Func<Dictionary<string, object>>)Context) WeeWooAnalytics.ContextProvider = null;
        }

        private static Dictionary<string, object> Context()
        {
            var data = new Dictionary<string, object> { ["scene"] = SceneManager.GetActiveScene().name, ["ws_mode"] = mode };
            if (distance == null) return data;
            data["run_id"] = runId ?? "";
            data["env"] = environment;
            data["leg"] = leg;
            data["dist"] = Round(distance());
            data["world"] = Name(world());
            data["drive_s"] = Round(Now);
            if (fuel != null) data["fuel_pct"] = Mathf.Round(100f * fuel());
            if (gap != null) data["police_gap_m"] = Round(gap());
            return data;
        }

        // If the tab closes now, these rows arrive by beacon as "tab_closed".
        private static List<KeyValuePair<string, Dictionary<string, object>>> ExitRows()
        {
            var rows = new List<KeyValuePair<string, Dictionary<string, object>>>();
            if (tutorialOpen) rows.Add(new KeyValuePair<string, Dictionary<string, object>>("ws_tutorials", TutorialRow("tab_closed")));
            if (runId == null) return rows;
            Tally total = run.Copy();
            if (legOpen)
            {
                Tally legSoFar = LegSoFar();
                total.Add(legSoFar);
                rows.Add(new KeyValuePair<string, Dictionary<string, object>>("ws_legs", LegRow(legSoFar, "tab_closed", "")));
            }
            rows.Add(new KeyValuePair<string, Dictionary<string, object>>("ws_runs", RunRow(total, "tab_closed", "", WorldShiftFlow.Escape.Carried, false)));
            return rows;
        }

        private static void Event(string name, params object[] keyValues)
        {
            var data = new Dictionary<string, object>();
            for (int i = 0; i + 1 < keyValues.Length; i += 2) data[(string)keyValues[i]] = keyValues[i + 1];
            if (Sink != null) { Sink("events", name, data); return; }
            WeeWooAnalytics.Track(name, data);
        }

        private static void Row(string sheet, Dictionary<string, object> data)
        {
            if (Sink != null) { Sink(sheet, null, data); return; }
            WeeWooAnalytics.AddRow(sheet, data);
        }
    }
}
