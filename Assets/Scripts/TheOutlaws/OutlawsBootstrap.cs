using UnityEngine;
using UnityEngine.SceneManagement;

public static class OutlawsBootstrap
{
    private const int RunSeed = 19021;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetRunState()
    {
        // Unity editor settings can preserve global state between Play runs.
        // Reset it explicitly so every run begins from the same conditions.
        Time.timeScale = 1f;
        Random.InitState(RunSeed);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "GetawayChase" || Object.FindFirstObjectByType<OutlawGameManager>() != null)
        {
            return;
        }

        GameObject root = new GameObject("The Outlaws Game Systems");
        root.AddComponent<OutlawGameManager>();
    }
}
