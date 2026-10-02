using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public sealed class OutlawGameManager : MonoBehaviour
{
    private const float FinishDistance = 1350f;
    private const float BasePoliceSpeed = 14f;
    private const float MaximumPoliceSpeed = 18f;
    private const float PoliceIncreaseInterval = 22f;
    private const float PoliceIncreaseAmount = 0.65f;
    private const float VehicleScale = 0.85f;
    private static readonly Vector3 PolicePrefabScale = new Vector3(2f, 1f, 3.5f);

    private AutoDriveCar driver;
    private CarPickupEffects pickupEffects;
    private FuelMeter fuel;
    private RiskRunController pursuit;
    private InfiniteRoad road;
    private ObstacleSpawner obstacles;
    private OutlawShooting shooting;
    private OutlawHUD hud;
    private ChaseCar[] policeCars;
    private float elapsed;
    private float nextPoliceIncrease = PoliceIncreaseInterval;
    private float previousPursuitGap;
    private float policeAlertRemaining;
    private GameObject finishGate;

    public OutlawGameState State { get; private set; } = OutlawGameState.Ready;
    public float DistanceTravelled => driver != null ? driver.RoadDistanceTravelled : 0f;
    public float DistanceRemaining => Mathf.Max(0f, FinishDistance - DistanceTravelled);
    public float Progress => Mathf.Clamp01(DistanceTravelled / FinishDistance);
    public float ElapsedSeconds => elapsed;
    public OutlawShooting Shooting => shooting;
    public FuelMeter Fuel => fuel;
    public CarPickupEffects PickupEffects => pickupEffects;
    public bool PoliceAlertActive => policeAlertRemaining > 0f;
    public int PoliceCarCount => policeCars != null ? policeCars.Length : 0;

    private void Awake()
    {
        GameObject player = GameObject.Find("PlayerCar");
        if (player == null)
        {
            Debug.LogError("The Outlaws could not find Team 19's PlayerCar.", this);
            enabled = false;
            return;
        }

        driver = player.GetComponent<AutoDriveCar>();
        pickupEffects = player.GetComponent<CarPickupEffects>();
        fuel = player.GetComponent<FuelMeter>();
        pursuit = player.GetComponent<RiskRunController>();
        road = Object.FindFirstObjectByType<InfiniteRoad>();
        obstacles = Object.FindFirstObjectByType<ObstacleSpawner>();

        policeCars = Object.FindObjectsByType<ChaseCar>(FindObjectsSortMode.None);
        System.Array.Sort(policeCars, (a, b) => string.CompareOrdinal(a.name, b.name));
        for (int i = 1; i < policeCars.Length; i++)
            policeCars[i].gameObject.SetActive(false);
        if (policeCars.Length > 0)
            policeCars = new[] { policeCars[0] };
        ConfigureVehiclesAndCamera(player.transform);

        if (!ValidateRequiredSystems())
        {
            enabled = false;
            return;
        }

        shooting = player.GetComponent<OutlawShooting>();
        if (shooting == null)
        {
            shooting = player.AddComponent<OutlawShooting>();
        }
        shooting.Configure(pickupEffects, obstacles);

        OutlawPickupSpawner spawner = gameObject.AddComponent<OutlawPickupSpawner>();
        spawner.Configure(road, player.transform, shooting, pickupEffects);

        hud = gameObject.AddComponent<OutlawHUD>();
        hud.Configure(this);

        if (pursuit != null)
        {
            pursuit.SetPoliceSpeed(BasePoliceSpeed);
        }

        previousPursuitGap = pickupEffects.RunState.PursuitGap;

        Time.timeScale = 0f;
    }

    private void ConfigureVehiclesAndCamera(Transform player)
    {
        player.localScale = Vector3.one * VehicleScale;

        foreach (ChaseCar policeCar in policeCars)
        {
            // ChaseCar lives directly on Team 19's stretched car prefab. Keep
            // those original proportions and reduce each axis by only 15%.
            policeCar.transform.localScale = PolicePrefabScale * VehicleScale;
            policeCar.gameObject.SetActive(true);
            policeCar.enabled = true;

            Renderer[] renderers = policeCar.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer policeRenderer in renderers)
            {
                policeRenderer.enabled = true;
                policeRenderer.material.color = new Color(0.08f, 0.2f, 0.55f);
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.transform.IsChildOf(player))
        {
            // Frame the road between the player and police. With the smaller
            // vehicles this places advancing police clearly inside the view.
            mainCamera.transform.localPosition = new Vector3(0f, 6f, -10f);
            mainCamera.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            mainCamera.transform.localScale = Vector3.one;
        }
    }

    private bool ValidateRequiredSystems()
    {
        bool valid = driver != null && pickupEffects != null && fuel != null &&
                     pursuit != null && road != null && obstacles != null &&
                     policeCars != null && policeCars.Length > 0;
        if (!valid)
        {
            Debug.LogError(
                "The Outlaws setup is incomplete. The player, road, obstacle spawner, " +
                "fuel system, pursuit controller, and at least one police car are required.",
                this);
        }
        return valid;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (State == OutlawGameState.Ready)
        {
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                BeginGame();
            }
            return;
        }

        if (State == OutlawGameState.Won || State == OutlawGameState.Lost)
        {
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Restart();
            }
            return;
        }

        elapsed += Time.deltaTime;
        UpdatePoliceFeedback();
        IncreasePolicePressure();
        UpdateFinishGate();

        if (DistanceTravelled >= FinishDistance)
        {
            EndGame(OutlawGameState.Won);
        }
        else if ((pickupEffects != null && pickupEffects.RunState.IsGameOver) ||
                 (fuel != null && fuel.IsEmpty))
        {
            EndGame(OutlawGameState.Lost);
        }
    }

    private void UpdatePoliceFeedback()
    {
        float currentGap = pickupEffects.RunState.PursuitGap;
        if (currentGap < previousPursuitGap - 1f)
        {
            policeAlertRemaining = 2.25f;
        }
        else
        {
            policeAlertRemaining = Mathf.Max(0f, policeAlertRemaining - Time.deltaTime);
        }
        previousPursuitGap = currentGap;
    }

    public void BeginGame()
    {
        if (State != OutlawGameState.Ready)
        {
            return;
        }

        State = OutlawGameState.Running;
        Time.timeScale = 1f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.path);
#endif
    }

    private void EndGame(OutlawGameState result)
    {
        if (State != OutlawGameState.Running)
        {
            return;
        }

        State = result;
        Time.timeScale = 0f;
    }

    private void IncreasePolicePressure()
    {
        if (pursuit == null || elapsed < nextPoliceIncrease)
        {
            return;
        }

        nextPoliceIncrease += PoliceIncreaseInterval;
        pursuit.SetPoliceSpeed(Mathf.Min(
            MaximumPoliceSpeed,
            pursuit.PoliceSpeed + PoliceIncreaseAmount));
    }

    private void UpdateFinishGate()
    {
        if (finishGate != null || road == null || DistanceRemaining > 90f)
        {
            return;
        }

        if (TryGetPointAhead(DistanceRemaining, out RoadPathPoint point))
        {
            finishGate = OutlawFinishGate.Create(point);
        }
    }

    private bool TryGetPointAhead(float distance, out RoadPathPoint point)
    {
        if (!road.TryGetPathPoint(driver.transform.position, out point))
        {
            return false;
        }

        float travelled = 0f;
        while (travelled < distance)
        {
            float step = Mathf.Min(4f, distance - travelled);
            Vector3 sample = point.Position + point.Forward * step;
            if (!road.TryGetPathPoint(sample, out point))
            {
                return false;
            }
            travelled += step;
        }

        return true;
    }
}
