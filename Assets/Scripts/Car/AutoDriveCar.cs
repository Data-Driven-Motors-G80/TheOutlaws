using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class AutoDriveCar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private Transform visual;

    [Header("Forward Movement")]
    [SerializeField, Min(0.01f)] private float forwardSpeed = 15f;
    [SerializeField, Min(0.01f)] private float maximumForwardSpeed = 30f;
    [SerializeField, Min(0f)] private float speedIncreaseAmount = 0.75f;
    [SerializeField, Min(0.01f)] private float speedIncreaseInterval = 30f;

    [Header("Lateral Movement")]
    [SerializeField, Min(0.01f)] private float minimumLateralSpeed = 8f;
    [SerializeField, Min(0.01f)] private float maximumLateralSpeed = 12f;
    [SerializeField, Min(0.01f)] private float minimumLateralAcceleration = 30f;
    [SerializeField, Min(0.01f)] private float maximumLateralAcceleration = 45f;
    [SerializeField, Min(0f)] private float edgeMargin = 1f;

    [Header("Feel")]
    [SerializeField, Range(0f, 45f)] private float maxSteerAngle = 20f;
    [SerializeField, Min(0f)] private float turnSharpness = 12f;

    private InputAction steerAction;
    private CarPickupEffects pickupEffects;
    private float currentForwardSpeed;
    private float speedIncreaseTimer;
    private float lateralOffset;
    private float lateralSpeed;
    private float heightOffset;
    private bool hasStarted;

    public float SteerInput { get; private set; }
    public float ForwardSpeed => currentForwardSpeed;
    public float BaseForwardSpeed => currentForwardSpeed > 0f
        ? currentForwardSpeed
        : Mathf.Min(forwardSpeed, maximumForwardSpeed);
    public float RoadDistanceTravelled { get; private set; }
    public bool IsDriveReady { get; private set; }

    private void Awake()
    {
        steerAction = CreateSteerAction();
        pickupEffects = GetComponent<CarPickupEffects>();
    }

    private void OnEnable()
    {
        steerAction.Enable();
        if (hasStarted)
        {
            InitializeRoad();
        }
    }

    private void OnDisable()
    {
        IsDriveReady = false;
        steerAction.Disable();
    }

    private void OnDestroy()
    {
        steerAction.Dispose();
    }

    private void Start()
    {
        hasStarted = true;
        InitializeRoad();
    }

    private void InitializeRoad()
    {
        if (road == null ||
            !road.TryGetPathPoint(
                transform.position,
                out RoadPathPoint start))
        {
            Debug.LogError(
                $"{nameof(AutoDriveCar)}: needs a road reference, and the car must start on the road.",
                this
            );

            enabled = false;
            return;
        }

        Vector3 offset =
            transform.position - start.Position;

        lateralOffset = Vector3.Dot(
            offset,
            start.Right
        );

        heightOffset = Vector3.Dot(
            offset,
            start.Up
        );

        currentForwardSpeed = Mathf.Min(
            forwardSpeed,
            maximumForwardSpeed
        );
        IsDriveReady = true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        float deltaTime = Time.deltaTime;

        UpdateForwardSpeed(deltaTime);

        float speedProgress = Mathf.InverseLerp(
            forwardSpeed,
            maximumForwardSpeed,
            currentForwardSpeed
        );

        float currentMaxLateralSpeed = Mathf.Lerp(
            minimumLateralSpeed,
            maximumLateralSpeed,
            speedProgress
        );

        float currentLateralAcceleration = Mathf.Lerp(
            minimumLateralAcceleration,
            maximumLateralAcceleration,
            speedProgress
        );

        float steerInput = Mathf.Clamp(
            steerAction.ReadValue<float>(),
            -1f,
            1f
        );

        float forwardDistance = currentForwardSpeed * deltaTime;
        float accelerationMultiplier = 1f;
        bool captured = false;

        if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
        {
            if (!pickupEffects.SimulationActive)
            {
                return;
            }

                // RiskRunState is the single clock for pursuit, crash recovery and
            // effect expiry. Reuse its integrated distance so the car and the
            // chase model cross the same boundaries on the same frame.
            forwardDistance = pickupEffects.RunState.LastTickForwardDistance;
            captured = pickupEffects.RunState.IsGameOver;
            if (captured && forwardDistance <= 0f)
            {
                return;
            }

            steerInput *= pickupEffects.SteeringMultiplier;
            accelerationMultiplier = pickupEffects.LateralAccelerationMultiplier;
        }

        SteerInput = steerInput;

        lateralSpeed = Mathf.MoveTowards(
            lateralSpeed,
            steerInput * currentMaxLateralSpeed,
            currentLateralAcceleration * accelerationMultiplier * deltaTime
        );

        if (captured)
        {
            lateralSpeed = 0f;
        }

        if (!road.TryGetPathPoint(
                transform.position,
                out RoadPathPoint current))
        {
            return;
        }

        Vector3 lookAhead =
            current.Position +
            current.Forward *
            forwardDistance;

        if (!road.TryGetPathPoint(
                lookAhead,
                out RoadPathPoint next))
        {
            return;
        }

        float limit = Mathf.Max(
            0f,
            next.Width * 0.5f - edgeMargin
        );

        float desiredOffset =
            lateralOffset +
            lateralSpeed * deltaTime;

        lateralOffset = Mathf.Clamp(
            desiredOffset,
            -limit,
            limit
        );

        if (!Mathf.Approximately(
                desiredOffset,
                lateralOffset))
        {
            lateralSpeed = 0f;
        }

        transform.position =
            next.Position +
            next.Right * lateralOffset +
            next.Up * heightOffset;

        // Count only forward road travel. Lateral weaving must not farm score
        // or pickup spawn distance.
        RoadDistanceTravelled += Vector3.Distance(current.Position, next.Position);

        ApplyRotation(
            next,
            deltaTime,
            currentMaxLateralSpeed
        );
    }

    private void UpdateForwardSpeed(float deltaTime)
    {
        if (currentForwardSpeed >= maximumForwardSpeed)
        {
            return;
        }

        speedIncreaseTimer += deltaTime;

        while (speedIncreaseTimer >= speedIncreaseInterval)
        {
            speedIncreaseTimer -= speedIncreaseInterval;

            currentForwardSpeed = Mathf.Min(
                currentForwardSpeed + speedIncreaseAmount,
                maximumForwardSpeed
            );
        }
    }

    private void ApplyRotation(
        RoadPathPoint point,
        float deltaTime,
        float currentMaxLateralSpeed)
    {
        float blend =
            1f - Mathf.Exp(
                -turnSharpness * deltaTime
            );

        float steerAngle =
            maxSteerAngle *
            lateralSpeed /
            currentMaxLateralSpeed;

        Quaternion roadRotation =
            Quaternion.LookRotation(
                point.Forward,
                point.Up
            );

        if (visual != null)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                roadRotation,
                blend
            );

            visual.localRotation = Quaternion.Slerp(
                visual.localRotation,
                Quaternion.Euler(
                    0f,
                    steerAngle,
                    0f
                ),
                blend
            );

            return;
        }

        Quaternion target =
            roadRotation *
            Quaternion.Euler(
                0f,
                steerAngle,
                0f
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            target,
            blend
        );
    }

    private static InputAction CreateSteerAction()
    {
        InputAction action = new InputAction(
            "Steer",
            InputActionType.Value,
            expectedControlType: "Axis"
        );

        action.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/leftArrow")
            .With("Positive", "<Keyboard>/rightArrow");

        action.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        return action;
    }
}
