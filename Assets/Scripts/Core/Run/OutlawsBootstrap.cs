// Resets session state when Unity starts a fresh play session.
using UnityEngine;

public static class OutlawsBootstrap
{
    private const int RunSeed = 19021;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetRunState()
    {

        Time.timeScale = 1f;
        Random.InitState(RunSeed);
    }
}
