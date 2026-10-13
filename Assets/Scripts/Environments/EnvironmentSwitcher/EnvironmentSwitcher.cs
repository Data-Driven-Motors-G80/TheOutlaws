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

    private const float FirstSwitchMinimumMetres = 1200f;
    private const float FirstSwitchMaximumMetres = 1800f;

    private const float PortalLeadMetres = 95f;
    private const float MountainMetres = 1000f;

    private OutlawGameManager game;
    private bool switching;
    private PortalGate portal;
    private string portalTarget;
    private InfiniteRoad road;
    private Transform player;
    private ObstacleSpawner obstacles;
    private readonly System.Random firstSwitchRandom = new System.Random(System.Guid.NewGuid().GetHashCode());

    private void Awake()
    {
        game = GetComponent<OutlawGameManager>();
        road = FindFirstObjectByType<InfiniteRoad>();
        obstacles = FindFirstObjectByType<ObstacleSpawner>();
        GameObject car = GameObject.Find("PlayerCar");
        if (car != null) player = car.transform;
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
                RunSession.NextPortalDistance = game.DistanceTravelled + (target == "Env_Snow" ? MountainMetres : 1500f);
                StartSwitch(target);
            }

            return;
        }
#endif

        UpdatePortal();
    }

    /// <summary>
    /// Portals are physical gates across the road. The first one (to the mountains) stands at a random
    /// 1,200-1,800 m; the mountains last 1,000 m before a gate back; the next trip comes 1,200-2,000 m later.
    /// </summary>
    private void UpdatePortal()
    {
        float distance = game.DistanceTravelled;
        if (RunSession.NextPortalDistance < 0f)
            RunSession.NextPortalDistance = Range(FirstSwitchMinimumMetres, FirstSwitchMaximumMetres);

        if (portal == null)
        {
            float ahead = RunSession.NextPortalDistance - distance;
            if (ahead > PortalLeadMetres) return;
            if (road == null || player == null ||
                !road.TryGetPointAhead(player.position, Mathf.Max(ahead, 30f), out RoadPathPoint point))
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
        bool enteringMountains = portalTarget == "Env_Snow";
        RunSession.NextPortalDistance = distance + (enteringMountains ? MountainMetres : Range(1200f, 2000f));
        Destroy(portal.gameObject);
        portal = null;
        StartSwitch(portalTarget);
    }

    private float Range(float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)firstSwitchRandom.NextDouble();
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
            capture: () => RunSession.SetPending(game.CaptureRun()),
            onCancelled: () => switching = false
        );
    }
}