using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>Configured via Resources/GoogleFormsAnalytics.json; Editor uploads are independently controlled.</summary>
public sealed class GoogleFormsAnalytics : MonoBehaviour
{
    [Serializable] public sealed class Field { public string metric; public string entry; }
    [Serializable] public sealed class Settings
    {
        public bool enabled;
        public bool sendFromEditor;
        public string formResponseUrl;
        public Field[] fields;
    }
    [Serializable] public sealed class Value { public string metric; public string value; }
    [Serializable] public sealed class Report { public Value[] values; }
    private static GoogleFormsAnalytics instance;
    private Settings settings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; RunAnalyticsState.ResetSession(); }

    public static void Complete(OutlawGameManager game)
    {
        if (!RunAnalyticsState.End()) return;
        if (instance == null)
        {
            var host = new GameObject("Google Forms Analytics");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GoogleFormsAnalytics>();
            TextAsset config = Resources.Load<TextAsset>("GoogleFormsAnalytics");
            if (config != null) instance.settings = JsonUtility.FromJson<Settings>(config.text);
        }
        var values = new Dictionary<string, string>();
        values["session_id"] = RunAnalyticsState.SessionId;
        values["run_id"] = RunAnalyticsState.RunId;
        values["survival_seconds"] = Number(game.ElapsedSeconds);
        values["distance_metres"] = Number(game.DistanceTravelled);
        values["city_seconds"] = Number(RunAnalyticsState.CitySeconds);
        values["snow_seconds"] = Number(RunAnalyticsState.SnowSeconds);
        values["ammo_remaining"] = game.Shooting.CurrentAmmo.ToString(CultureInfo.InvariantCulture);
        values["environment"] = SceneManager.GetActiveScene().name;
        values["game_version"] = Application.version;
        bool empty = game.Fuel.IsEmpty;
        // Fuel depletion also calls EndRun, so IsGameOver alone cannot distinguish police capture.
        values["death_reason"] = empty ? "fuel" : "police";
        foreach (string key in new[] { "shots_fired", "shots_hit", "obstacles_destroyed", "obstacle_collisions",
            "fuel_pickups", "ammo_pickups", "shield_pickups", "nitro_pickups", "recovery_pickups", "reverse_pickups" })
            values[key] = RunAnalyticsState.Get(key).ToString(CultureInfo.InvariantCulture);

        var rows = new List<Value>();
        foreach (var pair in values) rows.Add(new Value { metric = pair.Key, value = pair.Value });
        string report = JsonUtility.ToJson(new Report { values = rows.ToArray() });
        // Retain the latest completed run locally, even if the form is unavailable.
        PlayerPrefs.SetString("Outlaws.LastAnalyticsReport", report);
        PlayerPrefs.Save();
        if (Application.isEditor) Debug.Log("[Analytics] Completed run: " + report);
        if (instance.settings == null || !instance.settings.enabled ||
            (Application.isEditor && !instance.settings.sendFromEditor)) return;
        instance.StartCoroutine(instance.Send(values));
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private IEnumerator Send(Dictionary<string, string> values)
    {
        if (!Uri.TryCreate(settings.formResponseUrl, UriKind.Absolute, out Uri url) ||
            url.Scheme != "https" || url.Host != "docs.google.com" || !url.AbsolutePath.EndsWith("/formResponse"))
        { Debug.LogWarning("[Analytics] Configure a Google Forms formResponse URL."); yield break; }
        var form = new WWWForm();
        var mapped = new HashSet<string>();
        var entries = new HashSet<string>();
        foreach (Field field in settings.fields ?? Array.Empty<Field>())
        {
            if (field == null || string.IsNullOrEmpty(field.metric) || !values.ContainsKey(field.metric) ||
                string.IsNullOrEmpty(field.entry) || !System.Text.RegularExpressions.Regex.IsMatch(field.entry, @"^entry\.\d+$") ||
                !mapped.Add(field.metric) || !entries.Add(field.entry))
            { Debug.LogWarning("[Analytics] Invalid or duplicate form field mapping; submission skipped."); yield break; }
            form.AddField(field.entry, values[field.metric]);
        }
        if (mapped.Count != values.Count)
        { Debug.LogWarning("[Analytics] Map all analytics fields before enabling submissions."); yield break; }
        using (UnityWebRequest request = UnityWebRequest.Post(settings.formResponseUrl, form))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("[Analytics] Form request completed. Verify the row in Google Sheets.");
            else
                Debug.LogWarning("[Analytics] Submission unconfirmed. Check Sheets before retrying; browser CORS can hide a successful response.");
        }
        // No automatic retries: Forms cannot deduplicate and a CORS error may follow an accepted write.
    }
}
