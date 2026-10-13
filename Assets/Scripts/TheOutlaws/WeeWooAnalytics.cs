using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Sends gameplay analytics to a Google Apps Script web app that writes rows into a Sheet
/// (see Docs/ANALYTICS.md). Events are batched; on WebGL a beacon also delivers the
/// final state when the tab closes, so quitting mid-run or mid-tutorial is still recorded.
/// Configured via Resources/WeeWooAnalytics.json. No names, emails or device ids are sent.
/// </summary>
public sealed class WeeWooAnalytics : MonoBehaviour
{
    [Serializable]
    private sealed class Settings
    {
        public bool enabled;
        public bool sendFromEditor;
        public string endpointUrl;
        public string buildTag;
        public float flushSeconds = 12f;
    }

    private struct Row
    {
        public string Sheet;
        public Dictionary<string, object> Data;
    }

    private const int MaximumBatch = 40;

    private static WeeWooAnalytics instance;
    private static readonly List<Row> pending = new List<Row>();
    private static int sequence;
    private static float sessionStartRealtime;

    public static string SessionId { get; private set; }
    /// <summary>Supplies run time, distance and screen for every event row.</summary>
    public static Func<Dictionary<string, object>> ContextProvider;
    /// <summary>Rows to deliver if the tab closes right now (session end, abandoned run, ...).</summary>
    public static Func<List<KeyValuePair<string, Dictionary<string, object>>>> ExitRowsProvider;

    private Settings settings;
    private float nextFlush;
    private float nextExitRefresh;
    private bool uploadsEnabled;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void WWR_Init(string url);
    [DllImport("__Internal")] private static extern void WWR_Post(string body);
    [DllImport("__Internal")] private static extern void WWR_SetExit(string body);
    [DllImport("__Internal")] private static extern string WWR_UserAgent();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        pending.Clear();
        sequence = 0;
        SessionId = null;
        ContextProvider = null;
        ExitRowsProvider = null;
    }

    /// <summary>Creates the uploader and records the session start (once per page load).</summary>
    public static void EnsureStarted()
    {
        if (instance != null) return;
        var host = new GameObject("Wee Woo Analytics");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<WeeWooAnalytics>();
        instance.Configure();
    }

    /// <summary>Build tag for every row; editor play is suffixed "-editor" so it can be filtered out.</summary>
    public static string BuildTag => (instance != null && instance.settings != null && !string.IsNullOrEmpty(instance.settings.buildTag)
        ? instance.settings.buildTag
        : Application.version) + (Application.isEditor ? "-editor" : "");

    private void Configure()
    {
        SessionId = Guid.NewGuid().ToString("N").Substring(0, 16);
        sessionStartRealtime = Time.realtimeSinceStartup;
        TextAsset config = Resources.Load<TextAsset>("WeeWooAnalytics");
        settings = config != null ? JsonUtility.FromJson<Settings>(config.text) : new Settings();
        bool validUrl = Uri.TryCreate(settings.endpointUrl, UriKind.Absolute, out Uri url) && url.Scheme == "https";
        uploadsEnabled = settings.enabled && validUrl && (!Application.isEditor || settings.sendFromEditor);
        if (settings.enabled && !validUrl)
            Debug.LogWarning("[Analytics] Set endpointUrl in Resources/WeeWooAnalytics.json to the Apps Script web app URL.");
#if UNITY_WEBGL && !UNITY_EDITOR
        if (uploadsEnabled) WWR_Init(settings.endpointUrl);
#endif
        nextFlush = Time.realtimeSinceStartup + settings.flushSeconds;

        int sessionNumber = OutlawProfile.RegisterSession();
        var session = new Dictionary<string, object>
        {
            ["session_number"] = sessionNumber,
            ["returning_player"] = sessionNumber > 1,
            ["prior_runs"] = OutlawProfile.TotalRuns,
            ["missions_done"] = OutlawProfile.MissionsDoneCount,
            ["tutorial_ever_completed"] = OutlawProfile.TutorialEverCompleted,
            ["platform"] = Application.platform.ToString(),
            ["os"] = SystemInfo.operatingSystem,
            ["browser"] = UserAgent(),
            ["screen_w"] = Screen.width,
            ["screen_h"] = Screen.height,
            ["language"] = Application.systemLanguage.ToString(),
            ["mobile"] = Application.isMobilePlatform
        };
        TrackInternal("session_start", session);
        Flush();
    }

    private static string UserAgent()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return WWR_UserAgent(); } catch { return "unknown"; }
#else
        return Application.isEditor ? "editor" : "standalone";
#endif
    }

    public static float SessionSeconds => Time.realtimeSinceStartup - sessionStartRealtime;

    /// <summary>Logs one event. Arguments alternate key, value: Track("pickup", "kind", "Fuel").</summary>
    public static void Track(string eventName, params object[] keyValues)
    {
        var data = new Dictionary<string, object>();
        for (int i = 0; i + 1 < keyValues.Length; i += 2)
            data[Convert.ToString(keyValues[i], CultureInfo.InvariantCulture)] = keyValues[i + 1];
        TrackInternal(eventName, data);
    }

    private static void TrackInternal(string eventName, Dictionary<string, object> fields)
    {
        EnsureStarted();
        var data = new Dictionary<string, object>
        {
            ["ev"] = eventName,
            ["seq"] = ++sequence,
            ["session_s"] = Mathf.Round(SessionSeconds * 10f) / 10f
        };
        AddIdentity(data);
        Dictionary<string, object> context = ContextProvider?.Invoke();
        if (context != null)
            foreach (KeyValuePair<string, object> pair in context)
                data[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, object> pair in fields)
            data[pair.Key] = pair.Value;
        Enqueue("events", data);
        if (Application.isEditor && !instance.uploadsEnabled)
            Debug.Log("[Analytics] " + Json(data));
    }

    /// <summary>Adds a summary row (e.g. one per run) to the named sheet.</summary>
    public static void AddRow(string sheet, Dictionary<string, object> data)
    {
        EnsureStarted();
        AddIdentity(data);
        Enqueue(sheet, data);
        if (Application.isEditor && !instance.uploadsEnabled)
            Debug.Log($"[Analytics:{sheet}] " + Json(data));
    }

    private static void AddIdentity(Dictionary<string, object> data)
    {
        data["ts"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        data["player_id"] = OutlawProfile.PlayerId;
        data["session_id"] = SessionId;
        data["build"] = BuildTag;
    }

    private static void Enqueue(string sheet, Dictionary<string, object> data)
    {
        pending.Add(new Row { Sheet = sheet, Data = data });
        if (pending.Count >= MaximumBatch) Flush();
    }

    /// <summary>Sends everything queued now (run end, tutorial step, focus loss).</summary>
    public static void Flush()
    {
        if (instance == null) return;
        if (pending.Count > 0)
        {
            if (instance.uploadsEnabled)
            {
                string body = Payload(pending);
#if UNITY_WEBGL && !UNITY_EDITOR
                WWR_Post(body);
#else
                instance.StartCoroutine(instance.Post(body));
#endif
            }
            pending.Clear();
        }
        instance.nextFlush = Time.realtimeSinceStartup + instance.settings.flushSeconds;
        instance.RefreshExitPayload();
    }

    private IEnumerator Post(string body)
    {
        using (var request = new UnityWebRequest(settings.endpointUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            // text/plain keeps this a "simple" request, which Apps Script accepts without CORS preflight.
            request.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
            request.timeout = 20;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("[Analytics] Upload failed: " + request.error);
            else if (Application.isEditor)
                Debug.Log("[Analytics] Uploaded: " + request.downloadHandler.text);
        }
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (now >= nextFlush) Flush();
        if (now >= nextExitRefresh) RefreshExitPayload();
    }

    private void OnApplicationFocus(bool focused)
    {
        // Tab switches and closes start with a focus loss: get queued data out early.
        if (!focused) Flush();
    }

    private void RefreshExitPayload()
    {
        nextExitRefresh = Time.realtimeSinceStartup + 2f;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!uploadsEnabled) return;
        var rows = new List<Row>(pending);
        List<KeyValuePair<string, Dictionary<string, object>>> exit = ExitRowsProvider?.Invoke();
        if (exit != null)
            foreach (KeyValuePair<string, Dictionary<string, object>> pair in exit)
            {
                AddIdentity(pair.Value);
                rows.Add(new Row { Sheet = pair.Key, Data = pair.Value });
            }
        WWR_SetExit(rows.Count > 0 ? Payload(rows) : "");
#endif
    }

    private static string Payload(List<Row> rows)
    {
        var builder = new StringBuilder("{\"rows\":[");
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append("{\"sheet\":");
            AppendString(builder, rows[i].Sheet);
            builder.Append(",\"data\":");
            builder.Append(Json(rows[i].Data));
            builder.Append('}');
        }
        builder.Append("]}");
        return builder.ToString();
    }

    public static string Json(Dictionary<string, object> data)
    {
        var builder = new StringBuilder("{");
        bool first = true;
        foreach (KeyValuePair<string, object> pair in data)
        {
            if (!first) builder.Append(',');
            first = false;
            AppendString(builder, pair.Key);
            builder.Append(':');
            AppendValue(builder, pair.Value);
        }
        return builder.Append('}').ToString();
    }

    private static void AppendValue(StringBuilder builder, object value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case bool b:
                builder.Append(b ? "true" : "false");
                break;
            case float f:
                builder.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case double d:
                builder.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case int _:
            case long _:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            default:
                AppendString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char c in value ?? "")
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4"));
                    else builder.Append(c);
                    break;
            }
        }
        builder.Append('"');
    }

    public static Dictionary<string, object> SceneContext()
    {
        return new Dictionary<string, object> { ["scene"] = SceneManager.GetActiveScene().name };
    }
}
