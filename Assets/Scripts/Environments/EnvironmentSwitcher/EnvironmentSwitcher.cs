// Randomly switches environments during gameplay.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EnvironmentSwitcher : MonoBehaviour
{
    [Header("Environments")]
    [SerializeField] private string[] environmentScenes = { "Env_City", "Env_Snow" };

    [Header("Timing (minutes of play)")]
    [SerializeField, Min(0.1f)] private float minMinutes = 3f;
    [SerializeField, Min(0.1f)] private float maxMinutes = 5f;

    [Header("Transition")]
    [SerializeField, Min(0.1f)] private float slideSeconds = 0.6f;
    [SerializeField] private Color transitionColor = new Color(0.88f, 0.94f, 1f, 1f);

    private OutlawGameManager game;
    private bool switching;

    private void Awake()
    {
        game = GetComponent<OutlawGameManager>();
    }

    private void Update()
    {
        if (game == null || switching || game.State != OutlawGameState.Running)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
        {
            if (environmentScenes != null && environmentScenes.Length > 0)
            {
                int index = System.Array.IndexOf(
                    environmentScenes,
                    SceneManager.GetActiveScene().name
                );

                StartSwitch(environmentScenes[(index + 1) % environmentScenes.Length]);
            }

            return;
        }
#endif

        float elapsed = game.ElapsedSeconds;

        if (RunSession.NextSwitchAt < 0f)
            RunSession.NextSwitchAt = elapsed + NextInterval();

        if (elapsed < RunSession.NextSwitchAt)
            return;

        RunSession.NextSwitchAt = elapsed + NextInterval();

        if (environmentScenes == null || environmentScenes.Length == 0)
            return;

        string picked = environmentScenes[Random.Range(0, environmentScenes.Length)];

        if (picked == SceneManager.GetActiveScene().name)
            return;

        StartSwitch(picked);
    }

    private float NextInterval()
    {
        float low = Mathf.Min(minMinutes, maxMinutes);
        float high = Mathf.Max(minMinutes, maxMinutes);

        return Random.Range(low, high) * 60f;
    }

    private void StartSwitch(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || switching || SceneTransition.IsRunning)
            return;

        switching = true;

        SceneTransition.Run(
            sceneName,
            slideSeconds,
            transitionColor,
            canProceed: () => game != null && game.State == OutlawGameState.Running,
            capture: () => RunSession.SetPending(game.CaptureRun()),
            onCancelled: () => switching = false
        );
    }
}