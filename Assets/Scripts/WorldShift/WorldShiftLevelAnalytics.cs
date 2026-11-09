using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Playtest analytics for level play (Docs/ANALYTICS.md, "Levels"). One ws_level_attempts row
    /// per attempt (level load to finish, retry, menu or tab close) plus wl_* events.
    /// </summary>
    public static class WorldShiftLevelAnalytics
    {
        public const string Sheet = "ws_level_attempts";
        // A death this soon after a switch is blamed on the switch.
        public const float SwitchBlameSeconds = 1.5f;

        /// <summary>Test seam: receives every event and row instead of the uploader when set.</summary>
        internal static Action<string, string, Dictionary<string, object>> Sink;
        /// <summary>How the next attempt was started: menu, retry or next.</summary>
        public static string PendingStart = "menu";

        private static Func<float> distance, duration;
        private static Func<ShiftWorld> world;
        private static bool open;
        private static string attemptId, from;
        private static int level, sections, section, checkpointsReached, highestBefore;
        private static int deaths, switches, airSwitches, pauses, switchDeaths, sectionDeaths, sectionSwitches;
        private static int activeRamp;
        private static readonly Dictionary<int, int> rampStarts = new Dictionary<int, int>(), rampWins = new Dictionary<int, int>(), rampLosses = new Dictionary<int, int>(), rampAborts = new Dictionary<int, int>(), rampFirst = new Dictionary<int, int>();
        private static void Increment(Dictionary<int, int> counts, int ramp) { counts[ramp] = counts.TryGetValue(ramp, out int count) ? count + 1 : 1; }
        public static void RampStarted(int ramp)
        {
            if (!open) return;
            RampEnded(false, "interrupted");
            activeRamp = ramp;
            Increment(rampStarts, ramp);
            Event("wl_ramp_start", "ramp", ramp, "ramp_try", rampStarts[ramp]);
        }
        public static void RampEnded(bool landed, string reason)
        {
            if (!open || activeRamp == 0) return;
            int ramp = activeRamp;
            activeRamp = 0;
            bool aborted = reason == "retry" || reason == "interrupted" || reason == "menu" || reason == "left" || reason == "tab_closed";
            Increment(aborted ? rampAborts : landed ? rampWins : rampLosses, ramp);
            if (rampStarts[ramp] == 1) rampFirst[ramp] = aborted ? -1 : landed ? 1 : 0;
            Event("wl_ramp_result", "ramp", ramp, "ramp_try", rampStarts[ramp], "result", aborted ? "aborted" : landed ? "landed" : "failed", "cause", reason);
        }
        private static float daySeconds, nightSeconds, worldSince, lastSwitch, sectionStart, furthest, realStart;
        private static List<float> sectionSeconds = new List<float>();
        private static List<int> sectionDeathCounts = new List<int>(), sectionSwitchCounts = new List<int>();
        private static readonly Dictionary<string, int> deathCauses = new Dictionary<string, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { open = false; Sink = null; PendingStart = "menu"; distance = duration = null; world = null; }

        public static void MenuShown() => Event("wl_menu", "highest_beaten", WorldShiftLevels.HighestBeaten, "unlock_all", WorldShiftLevels.UnlockAll);
        public static void LevelPicked(int picked, bool unlocked) => Event("wl_level_pick", "picked", picked, "unlocked", unlocked);
        public static void UnlockAllToggled(bool on) => Event("wl_unlock_all", "on", on);

        public static void BeginAttempt(int levelIndex, int sectionCount, Func<float> getDistance, Func<float> getDuration, Func<ShiftWorld> getWorld)
        {
            EndAttempt("left");
            level = levelIndex; sections = sectionCount; from = PendingStart; PendingStart = "menu";
            distance = getDistance; duration = getDuration; world = getWorld;
            attemptId = Guid.NewGuid().ToString("N").Substring(0, 12);
            highestBefore = WorldShiftLevels.HighestBeaten;
            section = checkpointsReached = deaths = switches = airSwitches = pauses = switchDeaths = sectionDeaths = sectionSwitches = 0;
            daySeconds = nightSeconds = worldSince = sectionStart = furthest = 0f;
            lastSwitch = float.NegativeInfinity;
            realStart = Time.realtimeSinceStartup;
            sectionSeconds = new List<float>(); sectionDeathCounts = new List<int>(); sectionSwitchCounts = new List<int>();
            deathCauses.Clear();
            activeRamp = 0; rampStarts.Clear(); rampWins.Clear(); rampLosses.Clear(); rampAborts.Clear(); rampFirst.Clear();
            open = true;
            WeeWooAnalytics.EnsureStarted();
            WeeWooAnalytics.ContextProvider = Context;
            WeeWooAnalytics.ExitRowsProvider = ExitRows;
            Event("wl_level_start", "from", from, "attempt", WorldShiftLevels.Attempt, "sections", sections, "unlock_all", WorldShiftLevels.UnlockAll);
        }

        public static void Switched(ShiftWorld to, bool airborne)
        {
            if (!open) return;
            float now = Now, stayed = now - worldSince;
            if (to == ShiftWorld.Day) nightSeconds += stayed; else daySeconds += stayed;
            worldSince = lastSwitch = now;
            switches++; sectionSwitches++;
            if (airborne) airSwitches++;
            Event("wl_switch", "to", Name(to), "stayed_s", Round(stayed), "airborne", airborne);
        }

        public static void Checkpoint(int index)
        {
            if (!open) return;
            CloseSection();
            checkpointsReached = Mathf.Max(checkpointsReached, index);
            section = index;
            Event("wl_checkpoint", "checkpoint", index, "deaths_total", deaths);
        }

        /// <summary>cause: crash, switched_into, fell or missed_landing.</summary>
        public static void Died(string cause, ShiftHazard hazard, bool airborne)
        {
            if (!open) return;
            RampEnded(false, cause);
            deaths++; sectionDeaths++;
            float since = Now - lastSwitch;
            if (cause == "switched_into") switchDeaths++;
            string kind = hazard == null ? cause : hazard.Kind.ToString();
            string key = cause + ":" + kind;
            deathCauses[key] = deathCauses.TryGetValue(key, out int count) ? count + 1 : 1;
            Event("wl_death", "cause", cause, "hazard_kind", kind,
                "hazard_world", hazard == null ? "" : hazard.SharedAcrossWorlds ? "both" : Name(hazard.World),
                "moving", hazard != null && (hazard.LateralMotion || hazard.MotionSpeed != 0f),
                "s_since_switch", float.IsInfinity(since) ? -1f : Round(since), "after_switch", since <= SwitchBlameSeconds,
                "airborne", airborne, "deaths_this_section", sectionDeaths);
        }

        public static void Respawned(ShiftWorld into)
        {
            if (!open) return;
            float now = Now, stayed = now - worldSince;
            if (world() == ShiftWorld.Day) daySeconds += stayed; else nightSeconds += stayed;
            worldSince = now;
            lastSwitch = float.NegativeInfinity;
        }

        public static void Paused() { if (open) { pauses++; Event("wl_pause"); } }

        public static void Completed()
        {
            if (!open) return;
            Event("wl_level_complete", "deaths", deaths, "switches", switches);
            EndAttempt("complete");
        }

        /// <summary>Writes the attempt row once. Later calls do nothing until the next attempt.</summary>
        public static void EndAttempt(string outcome)
        {
            if (!open) return;
            RampEnded(false, outcome);
            var row = AttemptRow(outcome);
            open = false;
            Event("wl_level_end", "outcome", outcome, "deaths", deaths);
            Row(Sheet, row);
            distance = duration = null; world = null;
            if (WeeWooAnalytics.ContextProvider == (Func<Dictionary<string, object>>)Context) WeeWooAnalytics.ContextProvider = null;
            if (Sink == null) WeeWooAnalytics.Flush();
        }

        private static void CloseSection()
        {
            sectionSeconds.Add(Now - sectionStart);
            sectionDeathCounts.Add(sectionDeaths);
            sectionSwitchCounts.Add(sectionSwitches);
            sectionStart = Now; sectionDeaths = sectionSwitches = 0;
        }

        private static Dictionary<string, object> AttemptRow(string outcome)
        {
            float now = Now, stayed = now - worldSince;
            float day = daySeconds, night = nightSeconds;
            if (world != null) { if (world() == ShiftWorld.Day) day += stayed; else night += stayed; }
            var row = new Dictionary<string, object>
            {
                ["attempt_id"] = attemptId, ["level"] = level, ["level_name"] = WorldShiftLevels.Name(level),
                ["outcome"] = outcome, ["from"] = from, ["attempt"] = WorldShiftLevels.Attempt,
                ["unlock_all"] = WorldShiftLevels.UnlockAll, ["highest_beaten_before"] = highestBefore,
                ["drive_s"] = Round(now), ["real_s"] = Round(Time.realtimeSinceStartup - realStart),
                ["sections"] = sections, ["checkpoints_reached"] = checkpointsReached, ["section_at_end"] = section,
                ["furthest_m"] = Round(Mathf.Max(furthest, distance != null ? distance() : 0f)),
                ["deaths"] = deaths, ["switch_deaths"] = switchDeaths, ["switches"] = switches, ["air_switches"] = airSwitches,
                ["switches_per_min"] = now > 0f ? Round(switches / (now / 60f)) : 0f,
                ["day_s"] = Round(day), ["night_s"] = Round(night),
                ["night_pct"] = day + night > 0f ? Mathf.Round(100f * night / (day + night)) : -1f,
                ["pauses"] = pauses,
            };
            for (int i = 0; i < sections; i++)
            {
                bool done = i < sectionSeconds.Count, current = i == sectionSeconds.Count;
                row["s_" + i] = done ? Round(sectionSeconds[i]) : current ? Round(now - sectionStart) : -1f;
                row["deaths_" + i] = done ? sectionDeathCounts[i] : current ? sectionDeaths : 0;
                row["switches_" + i] = done ? sectionSwitchCounts[i] : current ? sectionSwitches : 0;
            }
            foreach (var pair in rampStarts)
            {
                int r = pair.Key;
                row["ramp_attempts_" + r] = pair.Value;
                row["ramp_success_" + r] = rampWins.TryGetValue(r, out int wins) ? wins : 0;
                row["ramp_failed_" + r] = rampLosses.TryGetValue(r, out int losses) ? losses : 0;
                row["ramp_aborted_" + r] = (rampAborts.TryGetValue(r, out int aborts) ? aborts : 0) + (activeRamp == r ? 1 : 0);
                row["ramp_first_" + r] = rampFirst.TryGetValue(r, out int first) ? first : -1;
            }
            var causes = new List<string>();
            foreach (var pair in deathCauses) causes.Add(pair.Key + "=" + pair.Value);
            row["death_causes"] = string.Join(";", causes);
            return row;
        }

        private static float Now
        {
            get
            {
                if (duration == null) return 0f;
                if (distance != null) furthest = Mathf.Max(furthest, distance());
                return duration();
            }
        }

        private static Dictionary<string, object> Context()
        {
            var data = new Dictionary<string, object> { ["scene"] = SceneManager.GetActiveScene().name, ["ws_mode"] = "level" };
            if (distance == null) return data;
            data["attempt_id"] = attemptId;
            data["level"] = level;
            data["env"] = WorldShiftLevels.Name(level);
            data["section"] = section;
            data["dist"] = Round(distance());
            data["world"] = Name(world());
            data["drive_s"] = Round(Now);
            return data;
        }

        private static List<KeyValuePair<string, Dictionary<string, object>>> ExitRows()
        {
            var rows = new List<KeyValuePair<string, Dictionary<string, object>>>();
            if (open) rows.Add(new KeyValuePair<string, Dictionary<string, object>>(Sheet, AttemptRow("tab_closed")));
            return rows;
        }

        private static string Name(ShiftWorld w) => w == ShiftWorld.Day ? "day" : "night";
        private static float Round(float value) => Mathf.Round(value * 100f) / 100f;

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
