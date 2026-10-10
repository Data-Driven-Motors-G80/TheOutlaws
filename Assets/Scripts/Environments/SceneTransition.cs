using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Slide + fade cover, scene load, reveal. Lives on a DontDestroyOnLoad object so it survives the
/// scene change. Drawn with IMGUI so it covers the Outlaws HUD as well as the normal canvases.
/// </summary>
public sealed class SceneTransition : MonoBehaviour
{
    public static bool IsRunning { get; private set; }

    private Color color;
    private float offsetX;
    private float alpha;
    private bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsRunning = false;

    /// <param name="canProceed">Checked once the screen is covered; return false to cancel (e.g. the run just ended).</param>
    /// <param name="capture">Called while covered, right before loading, to store the run state.</param>
    /// <param name="onCancelled">Called if the switch was cancelled and the current scene keeps running.</param>
    public static void Run(string sceneName, float slideSeconds, Color color,
        Func<bool> canProceed, Action capture, Action onCancelled)
    {
        if (IsRunning) return;
        IsRunning = true;

        GameObject host = new GameObject("SceneTransition");
        DontDestroyOnLoad(host);
        SceneTransition runner = host.AddComponent<SceneTransition>();
        runner.color = color;
        runner.StartCoroutine(runner.Routine(sceneName, Mathf.Max(0.1f, slideSeconds),
            canProceed, capture, onCancelled));
    }

    private IEnumerator Routine(string sceneName, float seconds,
        Func<bool> canProceed, Action capture, Action onCancelled)
    {
        float width = Screen.width;

        // 1. Cover: slide in from the right while fading in.
        yield return Slide(width, 0f, 0f, 1f, seconds);

        // 2. Load, unless the run ended while we were sliding in.
        bool proceed = canProceed == null || canProceed();
        AsyncOperation load = null;
        if (proceed)
        {
            capture?.Invoke();
            load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                RunSession.ClearPending();
                Debug.LogError($"SceneTransition: could not load '{sceneName}'. Is it in Build Settings and spelled exactly?");
                proceed = false;
            }
        }

        if (!proceed)
        {
            yield return Slide(0f, -width, 1f, 0f, seconds);
            Finish();
            onCancelled?.Invoke();
            yield break;
        }

        while (!load.isDone) yield return null;

        // Give the new scene's Start() methods a couple of frames to read the carried state.
        yield return null;
        yield return null;
        RunSession.ClearPending();

        // 3. Reveal: slide out to the left.
        yield return Slide(0f, -width, 1f, 0f, seconds);
        Finish();
    }

    private IEnumerator Slide(float fromX, float toX, float fromAlpha, float toAlpha, float seconds)
    {
        visible = true;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            offsetX = Mathf.Lerp(fromX, toX, k);
            alpha = Mathf.Lerp(fromAlpha, toAlpha, k);
            yield return null;
        }
        offsetX = toX;
        alpha = toAlpha;
    }

    private void Finish()
    {
        visible = false;
        IsRunning = false;
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (!visible || Event.current.type != EventType.Repaint) return;

        GUI.depth = -32000; // lowest depth = drawn last = on top of every other IMGUI and canvas
        Color previous = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, alpha);
        GUI.DrawTexture(new Rect(offsetX, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
