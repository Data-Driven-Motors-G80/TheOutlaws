using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-browser progress kept in PlayerPrefs (IndexedDB on WebGL, so incognito starts fresh).
/// Nothing here identifies a person: the player id is a random anonymous token.
/// </summary>
public static class OutlawProfile
{
    private const string PlayerIdKey = "WeeWoo.PlayerId";
    private const string StartedKey = "WeeWoo.HasStartedGame";
    private const string MissionsKey = "WeeWoo.MissionsDone";
    private const string RunsKey = "WeeWoo.TotalRuns";
    private const string SessionsKey = "WeeWoo.Sessions";
    private const string TutorialDoneKey = "WeeWoo.TutorialCompleted";

    private static HashSet<int> missionsDone;

    public static string PlayerId
    {
        get
        {
            string id = PlayerPrefs.GetString(PlayerIdKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 12);
                PlayerPrefs.SetString(PlayerIdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    public static bool HasStartedGame => PlayerPrefs.GetInt(StartedKey, 0) == 1;
    /// <summary>The tutorial is on for a browser's very first game only.</summary>
    public static bool TutorialDefault => !HasStartedGame;
    public static int TotalRuns => PlayerPrefs.GetInt(RunsKey, 0);
    public static int SessionCount => PlayerPrefs.GetInt(SessionsKey, 0);
    public static bool TutorialEverCompleted => PlayerPrefs.GetInt(TutorialDoneKey, 0) == 1;

    public static void MarkGameStarted()
    {
        PlayerPrefs.SetInt(StartedKey, 1);
        PlayerPrefs.Save();
    }

    public static int RegisterRun()
    {
        int runs = TotalRuns + 1;
        PlayerPrefs.SetInt(RunsKey, runs);
        PlayerPrefs.Save();
        return runs;
    }

    public static int RegisterSession()
    {
        int sessions = SessionCount + 1;
        PlayerPrefs.SetInt(SessionsKey, sessions);
        PlayerPrefs.Save();
        return sessions;
    }

    public static void MarkTutorialCompleted()
    {
        PlayerPrefs.SetInt(TutorialDoneKey, 1);
        PlayerPrefs.Save();
    }

    public static bool IsMissionDone(int id)
    {
        LoadMissions();
        return missionsDone.Contains(id);
    }

    public static int MissionsDoneCount
    {
        get
        {
            LoadMissions();
            return missionsDone.Count;
        }
    }

    public static void CompleteMission(int id)
    {
        LoadMissions();
        if (!missionsDone.Add(id)) return;
        PlayerPrefs.SetString(MissionsKey, string.Join(",", missionsDone));
        PlayerPrefs.Save();
    }

    public static List<MissionDefinition> VisibleMissions() => MissionCatalog.Visible(IsMissionDone);

    private const string TotalDistanceKey = "WeeWoo.TotalDistance";
    private const string TotalDestroyedKey = "WeeWoo.TotalDestroyed";
    private const string TotalAmmoKey = "WeeWoo.TotalAmmoPickups";

    public static float TotalDistance => PlayerPrefs.GetFloat(TotalDistanceKey, 0f);
    public static int TotalDestroyed => PlayerPrefs.GetInt(TotalDestroyedKey, 0);
    public static int TotalAmmoPickups => PlayerPrefs.GetInt(TotalAmmoKey, 0);

    /// <summary>Adds a finished run to the lifetime totals used by "(total)" missions.</summary>
    public static void AddRunTotals(float distance, int destroyed, int ammoPickups)
    {
        PlayerPrefs.SetFloat(TotalDistanceKey, TotalDistance + Mathf.Max(0f, distance));
        PlayerPrefs.SetInt(TotalDestroyedKey, TotalDestroyed + Mathf.Max(0, destroyed));
        PlayerPrefs.SetInt(TotalAmmoKey, TotalAmmoPickups + Mathf.Max(0, ammoPickups));
        PlayerPrefs.Save();
    }

    /// <summary>Lifetime totals with no current run (for progress on the title screen).</summary>
    public static MissionRunStats SavedTotals() => new MissionRunStats
    {
        TotalDistance = TotalDistance,
        TotalDestroyed = TotalDestroyed,
        TotalAmmoPickups = TotalAmmoPickups
    };

    private const string BestKey = "WeeWoo.BestDistance";

    /// <summary>Best real-run distance in metres on this browser.</summary>
    public static int BestDistance => PlayerPrefs.GetInt(BestKey, 0);

    /// <summary>Saves the distance if it beats the best; returns true for a new best.</summary>
    public static bool RecordDistance(float metres)
    {
        int rounded = Mathf.FloorToInt(metres);
        if (rounded <= BestDistance) return false;
        PlayerPrefs.SetInt(BestKey, rounded);
        PlayerPrefs.Save();
        return true;
    }

    private static void LoadMissions()
    {
        if (missionsDone != null) return;
        missionsDone = new HashSet<int>();
        foreach (string part in PlayerPrefs.GetString(MissionsKey, "").Split(','))
            if (int.TryParse(part, out int id)) missionsDone.Add(id);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        missionsDone = null;
    }
}
