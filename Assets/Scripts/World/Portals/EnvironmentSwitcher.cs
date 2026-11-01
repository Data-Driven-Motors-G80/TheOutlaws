// Offers portals and transfers the current run into another environment.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EnvironmentSwitcher : MonoBehaviour
{
    [Header("Environments")]
    [SerializeField] private string[] environmentScenes = { "Env_City", "Env_Snow", "Env_OceanCoast", "Env_Desert" };

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

    // Wait for a safe road gap before displaying a due portal.
    private void UpdatePortal()
    {
        if (RunSession.NextPortalAt < 0f)
            RunSession.ScheduleNextPortal(0f);

        if (portal == null)
        {
            if (RunSession.SecondsUntilPortal(game.ElapsedSeconds) > 0f) return;
            float plannedAt = game.Player.GetComponent<AutoDriveCar>().RoadPositionDistance + PortalLeadMetres;
            if (!game.CanScheduleEncounter(this, RunEncounterKind.Portal, plannedAt - 10f, plannedAt + 15f)) return;
            if (obstacles != null && !obstacles.TryReserveSafeGap(this)) return;
            game.BeginEncounter(this, RunEncounterKind.Portal, plannedAt - 10f, plannedAt + 15f);
            if (road == null || player == null ||
                !road.TryGetPointAhead(player.position, PortalLeadMetres, out RoadPathPoint point))
                return;
            if (obstacles != null && !obstacles.IsSafeGap(point.Position, 14f)) return;
            portalTarget = PickOtherEnvironment();
            bool toMountains = portalTarget == "Env_Snow";
            bool toOcean = portalTarget == "Env_OceanCoast";
            bool toDesert = portalTarget == "Env_Desert";
            portal = PortalGate.Create(point, toDesert ? new Color(0.95f, 0.68f, 0.3f)
                : toOcean ? new Color(0.08f, 0.8f, 0.7f)
                : toMountains ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.6f, 0.15f),
                toDesert ? "DESERT" : toOcean ? "OCEAN COAST" : toMountains ? "MOUNTAINS" : "CITY");
            return;
        }

        if (!road.IsBehind(portal.transform.position, player.position, 0.5f)) return;
        Destroy(portal.gameObject);
        portal = null;
        StartSwitch(portalTarget);
    }

    private void OnDisable()
    {
        if (obstacles != null) obstacles.ReleaseSafeGap(this);
        if (game != null) game.EndEncounter(this);
    }

    private string PickOtherEnvironment()
    {
        if (environmentScenes == null) return null;
        string current = SceneManager.GetActiveScene().name;
        string[] alternatives = System.Array.FindAll(environmentScenes,
            name => !string.IsNullOrEmpty(name) && name != current);
        return alternatives.Length > 0 ? alternatives[Random.Range(0, alternatives.Length)] : null;
    }

    // Capture the run before loading the selected environment.
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

                RunSession.ScheduleNextPortal(game.ElapsedSeconds);
                RunSession.SetPending(game.CaptureRun());
            },
            onCancelled: () =>
            {
                switching = false;
                if (obstacles != null) obstacles.ReleaseSafeGap(this);
                if (game != null) game.EndEncounter(this);
            }
        );
    }
}
