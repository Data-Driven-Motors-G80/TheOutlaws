using UnityEngine;

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
}
