// Switches environments when the car drives through a portal gate.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EnvironmentSwitcher : MonoBehaviour
{
    [Header("Environments")]
    [SerializeField] private string[] environmentScenes = { "Env_City", "Env_Snow" };

    [Header("Transition")]
    [SerializeField, Min(0.1f)] private float slideSeconds = 0.6f;
    [SerializeField] private Color transitionColor = new Color(0.88f, 0.94f, 1f, 1f);

    private const float PortalLeadMetres = 95f;

    private OutlawGameManager game;
    private bool switching;
    private PortalGate portal;
    private string portalTarget;
    private InfiniteRoad road;
    private Transform player;
    private ObstacleSpawner obstacles;

    private void Awake()
    {
        game = GetComponent<OutlawGameManager>();
        EnvironmentRoot environment = GetComponent<EnvironmentRoot>();
        if (environment == null) return;
        road = environment.Road;
        obstacles = environment.Obstacles;
        if (environment.Player != null) player = environment.Player.transform;
    }

    private void Update()
    {
        if (game == null || switching || game.State != OutlawGameState.Running || game.IsTutorial)
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

                string target = environmentScenes[(index + 1) % environmentScenes.Length];
                RunSession.ScheduleNextPortal(game.ElapsedSeconds);
                StartSwitch(target);
            }

            return;
        }
#endif

        UpdatePortal();
    }

    /// <summary>
    /// A gate appears ahead after 60 seconds of active gameplay in each environment.
    /// Pauses and the tutorial do not count; driving through the gate switches scenes.
    /// </summary>
    private void UpdatePortal()
    {
        if (RunSession.NextPortalAt < 0f)
            RunSession.ScheduleNextPortal(0f);

        if (portal == null)
        {
            if (RunSession.SecondsUntilPortal(game.ElapsedSeconds) > 0f) return;
            if (road == null || player == null ||
                !road.TryGetPointAhead(player.position, PortalLeadMetres, out RoadPathPoint point))
                return;
            portalTarget = PickOtherEnvironment();
            bool toMountains = portalTarget == "Env_Snow";
            portal = PortalGate.Create(point, toMountains ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.6f, 0.15f),
                toMountains ? "MOUNTAINS" : "CITY");
            if (obstacles != null)
            {
                // Nothing new in front of the gate, and its doorway is clear.
                obstacles.SuppressAutoSpawn = true;
                obstacles.ClearNear(portal.transform.position, 14f);
            }
            return;
        }

        if (!road.IsBehind(portal.transform.position, player.position, 0.5f)) return;
        Destroy(portal.gameObject);
        portal = null;
        StartSwitch(portalTarget);
    }

    private string PickOtherEnvironment()
    {
        if (environmentScenes == null) return null;
        string current = SceneManager.GetActiveScene().name;
        string[] alternatives = System.Array.FindAll(environmentScenes,
            name => !string.IsNullOrEmpty(name) && name != current);
        return alternatives.Length > 0 ? alternatives[Random.Range(0, alternatives.Length)] : null;
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
            capture: () =>
            {
                // Start the next environment's minute from the carried run time.
                RunSession.ScheduleNextPortal(game.ElapsedSeconds);
                RunSession.SetPending(game.CaptureRun());
            },
            onCancelled: () => switching = false
        );
    }
}
