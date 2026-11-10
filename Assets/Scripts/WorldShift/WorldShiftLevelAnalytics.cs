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
        // Per-section tries and failure log ("metres:lane:cause"), so every level shows where and how players fail.
        private const int MaxFailsLogged = 15;
        private static int sectionRetries;
        private static List<int> sectionRetryCounts = new List<int>();
        private static List<string> sectionFails = new List<string>();
        private static List<string> sectionFailLogs = new List<string>();
        // Training sign reading: seconds each road-sign card stays up before the player continues (wall clock, pauses excluded).
        private static float[] readFirst = new float[0], readTotal = new float[0];
        private static int[] readCount = new int[0];
        private static bool signOpen;
        private static int signLesson = -1;
        private static float signStart = float.NaN, signElapsed;
        // Training freeze demonstration: seconds the car waits at the stop until Night/Day/Night has been shown.
        private static float freezeStart = float.NaN, freezeDemoSeconds = -1f;

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
            sectionRetries = 0; sectionRetryCounts = new List<int>(); sectionFails = new List<string>(); sectionFailLogs = new List<string>();
            readFirst = new float[sections]; readTotal = new float[sections]; readCount = new int[sections];
            for (int i = 0; i < sections; i++) readFirst[i] = -1f;
            signOpen = false; signLesson = -1; signStart = float.NaN; signElapsed = 0f;
            freezeStart = float.NaN; freezeDemoSeconds = -1f;
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
        public static void Died(string cause, ShiftHazard hazard, bool airborne, float lateral = float.NaN)
        {
            if (!open) return;
            RampEnded(false, cause);
            deaths++; sectionDeaths++;
            float since = Now - lastSwitch;
            if (cause == "switched_into") switchDeaths++;
            string kind = hazard == null ? cause : hazard.Kind.ToString();
            string key = cause + ":" + kind;
            deathCauses[key] = deathCauses.TryGetValue(key, out int count) ? count + 1 : 1;
            string lane = Lane(lateral);
            int metres = distance != null ? Mathf.RoundToInt(distance()) : 0;
            if (sectionFails.Count < MaxFailsLogged) sectionFails.Add(metres + ":" + lane + ":" + cause);
            Event("wl_death", "cause", cause, "hazard_kind", kind,
                "hazard_world", hazard == null ? "" : hazard.SharedAcrossWorlds ? "both" : Name(hazard.World),
                "moving", hazard != null && (hazard.LateralMotion || hazard.MotionSpeed != 0f),
                "s_since_switch", float.IsInfinity(since) ? -1f : Round(since), "after_switch", since <= SwitchBlameSeconds,
                "airborne", airborne, "deaths_this_section", sectionDeaths,
                "lane", lane, "x", float.IsNaN(lateral) ? 0f : Round(lateral));
        }

        /// <summary>Lane at the moment of failure: L, M or R (? when unknown).</summary>
        public static string Lane(float lateral)
        {
            if (float.IsNaN(lateral)) return "?";
            int lane = Mathf.Clamp(Mathf.RoundToInt(lateral / (WorldShiftLevel.RoadWidth / 3f)) + 1, 0, 2);
            return lane == 0 ? "L" : lane == 1 ? "M" : "R";
        }

        /// <summary>The player pressed R (or Retry in pause) to restart the current checkpoint.</summary>
        public static void CheckpointRetried()
        {
            if (!open) return;
            sectionRetries++;
            Event("wl_checkpoint_retry", "retries_this_section", sectionRetries);
        }

        /// <summary>A Training road-sign card appeared and stopped the car.</summary>
        public static void SignShown(int lesson)
        {
            if (!open || signOpen) return;
            signOpen = true;
            signLesson = lesson;
            signElapsed = 0f;
            signStart = Time.realtimeSinceStartup;
            Event("wl_sign_shown", "lesson", lesson, "view", lesson >= 0 && lesson < readCount.Length ? readCount[lesson] + 1 : 1);
        }

        /// <summary>The sign card closed: "continue" (Space) or "restart" (checkpoint restart while it was up).</summary>
        public static void SignRead(string how = "continue")
        {
            if (!open || !signOpen) return;
            float seconds = SignSeconds();
            signOpen = false;
            int i = signLesson;
            signLesson = -1;
            if (i < 0 || i >= readCount.Length) return;
            readCount[i]++;
            readTotal[i] += seconds;
            if (readFirst[i] < 0f) readFirst[i] = seconds;
            Event("wl_sign_read", "lesson", i, "view", readCount[i], "read_s", Round(seconds), "how", how);
        }

        private static float SignSeconds() => signElapsed + (float.IsNaN(signStart) ? 0f : Time.realtimeSinceStartup - signStart);

        /// <summary>The car is waiting at the freeze demonstration stop. Safe to call every step.</summary>
        public static void FreezeDemoWaiting()
        {
            if (open && float.IsNaN(freezeStart) && freezeDemoSeconds < 0f) freezeStart = Time.realtimeSinceStartup;
        }

        /// <summary>Night, Day and Night have all been shown, so the freeze lesson lets the car drive on.</summary>
        public static void FreezeDemonstrated()
        {
            if (!open || freezeDemoSeconds >= 0f) return;
            freezeDemoSeconds = float.IsNaN(freezeStart) ? 0f : Time.realtimeSinceStartup - freezeStart;
            Event("wl_freeze_demo", "demo_s", Round(freezeDemoSeconds));
        }

        public static void Respawned(ShiftWorld into)
        {
            if (!open) return;
            float now = Now, stayed = now - worldSince;
            if (world() == ShiftWorld.Day) daySeconds += stayed; else nightSeconds += stayed;
            worldSince = now;
            lastSwitch = float.NegativeInfinity;
        }

        public static void Paused()
        {
            if (!open) return;
            pauses++;
            // Reading time stops while the pause menu covers the sign.
            if (signOpen && !float.IsNaN(signStart)) { signElapsed += Time.realtimeSinceStartup - signStart; signStart = float.NaN; }
            Event("wl_pause");
        }

        public static void Resumed()
        {
            if (open && signOpen && float.IsNaN(signStart)) signStart = Time.realtimeSinceStartup;
        }

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
            sectionRetryCounts.Add(sectionRetries);
            sectionFailLogs.Add(string.Join(";", sectionFails));
            sectionStart = Now; sectionDeaths = sectionSwitches = sectionRetries = 0;
            sectionFails = new List<string>();
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
                int sectionDeathTotal = done ? sectionDeathCounts[i] : current ? sectionDeaths : 0;
                int retries = done ? sectionRetryCounts[i] : current ? sectionRetries : 0;
                row["retries_" + i] = retries;
                // Tries: the first go plus every death or manual restart in this section.
                row["tries_" + i] = done || current ? 1 + sectionDeathTotal + retries : 0;
                // 1 = cleared without a death or restart, 0 = needed another try, -1 = not reached or still on the first try.
                row["first_try_" + i] = done ? (sectionDeathTotal + retries == 0 ? 1 : 0)
                    : current ? (sectionDeathTotal + retries > 0 ? 0 : -1) : -1;
                row["fails_" + i] = done ? sectionFailLogs[i] : current ? string.Join(";", sectionFails) : "";
            }
            if (level == 0)
            {
                float readSum = 0f;
                for (int i = 0; i < sections; i++)
                {
                    float total = readTotal[i];
                    // A sign still on screen when the attempt ends counts toward reading time.
                    if (signOpen && signLesson == i) total += SignSeconds();
                    row["read_" + i] = Round(readFirst[i]);
                    row["read_total_" + i] = Round(total);
                    row["reads_" + i] = readCount[i];
                    readSum += total;
                }
                row["read_s"] = Round(readSum);
                row["quit_on_sign"] = signOpen ? signLesson : -1;
                row["freeze_demo_s"] = Round(freezeDemoSeconds);
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
