// Displays a sliding transition when switching scenes.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransition : MonoBehaviour
{
    public static bool IsRunning { get; private set; }

    private Color color;
    private float offsetX;
    private float alpha;
    private bool visible;
    private bool completed;
    private bool cleanedUp;
    private string destination;
    private Action cancelled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsRunning = false;
    }

    public static void Run(
        string sceneName,
        float slideSeconds,
        Color color,
        Func<bool> canProceed,
        Action capture,
        Action onCancelled)
    {
        if (IsRunning)
            return;

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneTransition: destination '{sceneName}' is not available in Build Settings.");
            onCancelled?.Invoke();
            return;
        }
        IsRunning = true;

        GameObject host = new GameObject("SceneTransition");
        DontDestroyOnLoad(host);

        SceneTransition runner = host.AddComponent<SceneTransition>();
        runner.color = color;
        runner.destination = sceneName;
        runner.cancelled = onCancelled;

        runner.StartCoroutine(
            runner.Guarded(runner.Routine(
                sceneName,
                Mathf.Max(0.1f, slideSeconds),
                canProceed,
                capture,
                onCancelled
            ))
        );
    }

    private IEnumerator Routine(
        string sceneName,
        float seconds,
        Func<bool> canProceed,
        Action capture,
        Action onCancelled)
    {
        float width = Screen.width;

        yield return Slide(width, 0f, 0f, 1f, seconds);

        bool proceed = canProceed == null || canProceed();
        AsyncOperation load = null;

        if (proceed)
        {
            capture?.Invoke();

            load = SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single
            );

            if (load == null)
            {
                RunSession.ClearPending();
                Debug.LogError(
                    $"SceneTransition: could not load '{sceneName}'. " +
                    "Is it in Build Settings and spelled exactly?"
                );

                proceed = false;
            }
        }

        if (!proceed)
        {
            yield return Slide(0f, -width, 1f, 0f, seconds);

            Finish();

            yield break;
        }

        while (!load.isDone)
            yield return null;

        yield return null;
        yield return null;

        RunSession.ClearPending();

        yield return Slide(0f, -width, 1f, 0f, seconds);

        completed = true;
        Finish();
    }

    // Drive nested enumerators here so exceptions in either loading or sliding
    // cannot strand the persistent full-screen overlay.
    private IEnumerator Guarded(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            object current = null;
            bool more = false;
            Exception failure = null;
            try
            {
                more = stack.Peek().MoveNext();
                if (more) current = stack.Peek().Current;
            }
            catch (Exception exception) { failure = exception; }
            if (failure != null)
            {
                Debug.LogError($"SceneTransition: transition to '{destination}' failed: {failure}");
                Finish();
                yield break;
            }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested);
            else yield return current;
        }
    }

    private IEnumerator Slide(
        float fromX,
        float toX,
        float fromAlpha,
        float toAlpha,
        float seconds)
    {
        visible = true;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t);

            offsetX = Mathf.Lerp(fromX, toX, progress);
            alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);

            yield return null;
        }

        offsetX = toX;
        alpha = toAlpha;
    }

    private void Finish()
    {
        Cleanup();
        Destroy(gameObject);
    }

    private void OnDisable() { Cleanup(); }

    private void Cleanup()
    {
        if (cleanedUp) return;
        cleanedUp = true;
        visible = false;
        IsRunning = false;
        RunSession.ClearPending();
        if (!completed)
        {
            try { cancelled?.Invoke(); }
            catch (Exception exception) { Debug.LogError($"SceneTransition: cancellation of '{destination}' failed: {exception}"); }
        }
        cancelled = null;
    }

    private void OnGUI()
    {
        if (!visible || Event.current.type != EventType.Repaint)
            return;

        GUI.depth = -32000;

        Color previous = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, alpha);

        GUI.DrawTexture(
            new Rect(offsetX, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture
        );

        GUI.color = previous;
    }
}