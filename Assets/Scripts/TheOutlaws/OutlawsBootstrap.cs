using UnityEngine;
using UnityEngine.SceneManagement;

public static class OutlawsBootstrap
{
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
