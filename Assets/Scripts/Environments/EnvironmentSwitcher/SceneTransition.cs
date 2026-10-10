// Displays a sliding transition when switching scenes.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransition : MonoBehaviour
{
    public static bool IsRunning { get; private set; }

    private Color color;
    private float offsetX;
    private float alpha;
    private bool visible;

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

        IsRunning = true;

        GameObject host = new GameObject("SceneTransition");
        DontDestroyOnLoad(host);

        SceneTransition runner = host.AddComponent<SceneTransition>();
        runner.color = color;

        runner.StartCoroutine(
            runner.Routine(
                sceneName,
                Mathf.Max(0.1f, slideSeconds),
                canProceed,
                capture,
                onCancelled
            )
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
            onCancelled?.Invoke();

            yield break;
        }

        while (!load.isDone)
            yield return null;

        yield return null;
        yield return null;

        RunSession.ClearPending();

        yield return Slide(0f, -width, 1f, 0f, seconds);

        Finish();
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
        visible = false;
        IsRunning = false;

        Destroy(gameObject);
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