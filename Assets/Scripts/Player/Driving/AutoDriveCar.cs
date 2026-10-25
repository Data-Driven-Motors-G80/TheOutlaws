using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class AutoDriveCar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private Transform visual;

    [Header("Speed")]
    [SerializeField, Min(0.01f)] private float forwardSpeed = RunDifficultyState.StartSpeed;
    [SerializeField, Min(0.01f)] private float maximumForwardSpeed = RunDifficultyState.MaximumSpeed;
    [SerializeField, Min(0f)] private float speedIncreaseAmount = 0.75f;
    [SerializeField, Min(0.01f)] private float speedIncreaseInterval = 30f;

    [Header("Steering")]
    [SerializeField, Min(0.01f)] private float minimumLateralSpeed = 8f;
    [SerializeField, Min(0.01f)] private float maximumLateralSpeed = 12f;
    [SerializeField, Min(0.01f)] private float minimumLateralAcceleration = 30f;
    [SerializeField, Min(0.01f)] private float maximumLateralAcceleration = 45f;
    [SerializeField, Min(0f)] private float edgeMargin = 1f;
    [Tooltip("Largest angle the nose turns away from the road direction, in degrees.")]
    [SerializeField, Range(0f, 45f)] private float maxSteerAngle = 20f;
    [Tooltip("How fast steering winds toward full lock, per second. Higher = twitchier.")]
    [SerializeField, Min(1f)] private float steeringRiseRate = 7f;
    [Tooltip("How fast steering returns to centre or reverses, per second.")]
    [SerializeField, Min(1f)] private float steeringReturnRate = 10f;

    [Header("Ice")]
    [SerializeField] private bool icyRoad;
    [SerializeField, Range(0.5f, 1f)] private float iceSteeringGrip = 0.9f;
    [SerializeField, Range(0.1f, 1f)] private float iceReleaseGrip = 0.3f;

    [Header("Cornering")]
    [Tooltip("Outward push in bends, in m/s for every 1 g of sideways force. 0 = off.")]
    [SerializeField, Min(0f)] private float centrifugalDrift = 3f;
    [Tooltip("Body lean in degrees for every 1 g of sideways force.")]
    [SerializeField, Min(0f)] private float rollPerG = 6f;
    [SerializeField, Range(0f, 25f)] private float maxBodyRoll = 9f;

    [Header("Skid")]
    [Tooltip("Seconds of full steering before the tires start to skid.")]
    [SerializeField, Min(0f)] private float skidOnsetTime = 0.25f;
    [Tooltip("Seconds of further steering to reach a full skid.")]
    [SerializeField, Min(0.05f)] private float skidRampTime = 0.7f;
    [Tooltip("Seconds for the skid to fade after releasing.")]
    [SerializeField, Min(0.05f)] private float skidRecoveryTime = 0.5f;
    [Tooltip("Extra sideways slide at full skid, in m/s.")]
    [SerializeField, Min(0f)] private float skidSlide = 5f;
    [Tooltip("Steering strength lost at full skid (0 = none, 1 = all).")]
    [SerializeField, Range(0f, 1f)] private float skidSteeringLoss = 0.5f;

    [Header("Drift Boost")]
    [Tooltip("How far the nose swings into the turn at full drift, in degrees.")]
    [SerializeField, Range(0f, 45f)] private float driftBodyAngle = 24f;
    [Tooltip("Body lean into the turn at full drift, in degrees.")]
    [SerializeField, Range(0f, 15f)] private float driftLean = 5f;
    [Tooltip("Upward speed of the hop when a drift starts, in m/s. 0 = no hop.")]
    [SerializeField, Min(0f)] private float driftHopSpeed = 3.2f;
    [Tooltip("Seconds of full drift needed before releasing gives a boost.")]
    [SerializeField, Min(0.1f)] private float turboChargeTime = 0.6f;
    [Tooltip("Seconds the boost lasts.")]
    [SerializeField, Min(0.05f)] private float turboDuration = 0.7f;
    [Tooltip("Nose-up kick when the boost fires.")]
    [SerializeField, Min(0f)] private float turboKick = 40f;

    [Header("Air")]
    [SerializeField, Min(0.1f)] private float gravity = 9.81f;
    [Tooltip("Extra gravity while falling, so jumps feel weighty rather than floaty.")]
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.4f;
    [Tooltip("Upward speed cap at take-off, in m/s. 0 = no cap.")]
    [SerializeField, Min(0f)] private float maxLaunchSpeed = 4f;
    [Tooltip("Steering strength while off the ground (0 = none, 1 = full).")]
    [SerializeField, Range(0f, 1f)] private float airSteeringControl = 0.15f;

    [Header("Body")]
    [SerializeField, Range(0f, 10f)] private float maxBodyPitch = 4f;
    [Tooltip("Fine shake from the road surface at top speed, in metres. 0 = smooth.")]
    [SerializeField, Min(0f)] private float roadVibration = 0.012f;
    [SerializeField, Min(1f)] private float suspensionStiffness = 120f;
    [SerializeField, Min(0.1f)] private float suspensionDamping = 10f;
    [SerializeField, Min(0f)] private float maxSuspensionTravel = 0.18f;

    private const float DriftResponse = 12f;
    private const float RoadFollowSharpness = 22f;
    private const float AirPitchSharpness = 3f;
    private const float YawStiffness = 70f;
    private const float YawDamping = 11f;
    private const float SteerSlipAngle = 4f;
    private const float PitchPerAcceleration = 0.35f;
    private const float LandingCompression = 0.02f;

    private InputAction steerAction;
    private CarPickupEffects pickupEffects;
    private float currentForwardSpeed;
    private bool managedDifficulty;

    public void SetDifficultySpeed(float speed)
    {
        managedDifficulty = true;
        currentForwardSpeed = Mathf.Clamp(speed, forwardSpeed, maximumForwardSpeed);
    }

    public void SetTutorialSpeed(float speed)
    {
        managedDifficulty = true;
        currentForwardSpeed = Mathf.Clamp(speed, 1f, maximumForwardSpeed);
    }
    private float speedIncreaseTimer;
    private float lateralOffset;
    private float lateralSpeed;
    private float heightOffset;
    private bool hasStarted;

    private float driftSpeed;
    private float airHeight;
    private float verticalVelocity;
    private float previousGroundY;
    private float previousGroundVelocity;
    private bool hasGroundSample;
    private float roll, rollVelocity;
    private float bounce, bounceVelocity;
    private float previousLateralSpeed;
    private float steerHoldTime;
    private float holdSide;
    private float steerWheel;
    private float hop, hopVelocity;
    private float driftCharge;
    private bool wasDrifting;
    private float yawAngle, yawVelocity;
    private float pitch, pitchVelocity;
    private float previousHorizontalSpeed, filteredAcceleration;
    private bool hasSpeedSample;
    private float steerLateralAccel;
    private Vector3 visualBasePosition;
    private bool visualBaseStored;

    public Transform FiringFrame => visual != null ? visual : transform;
    public bool IsOnIce => icyRoad;
    public float SteerInput { get; private set; }
    public bool IsAirborne { get; private set; }
    public float AirHeight => airHeight;
    public float LateralG { get; private set; }
    public float Skid { get; private set; }
    public event System.Action<float> Landed;
    public event System.Action<float> DriftBoosted;
    public float DriftBoost { get; private set; }
    public bool DriftCharged => driftCharge >= turboChargeTime;
    public bool IsDrifting => wasDrifting;
    public float ForwardSpeed => currentForwardSpeed;
    public float BaseForwardSpeed => currentForwardSpeed > 0f
        ? currentForwardSpeed
        : Mathf.Min(forwardSpeed, maximumForwardSpeed);
    public float EnvironmentSpeedMultiplier { get; set; } = 1f;
    public float EnvironmentSteeringGrip { get; set; } = 1f;

    public void ApplyLateralImpulse(float metresPerSecond)
    {
        if (IsDriveReady && isActiveAndEnabled)
            lateralSpeed = Mathf.Clamp(lateralSpeed + metresPerSecond, -maximumLateralSpeed, maximumLateralSpeed);
    }
    public float RoadDistanceTravelled { get; private set; }
    public float RoadPositionDistance { get; private set; }
    public bool IsDriveReady { get; private set; }
    public float SpeedIncreaseTimer => speedIncreaseTimer;
    public float StartingSpeed => forwardSpeed;
    public float MaximumSpeed => maximumForwardSpeed;
    public float SpeedIncreaseAmount => speedIncreaseAmount;
    public float SpeedIncreaseInterval => speedIncreaseInterval;

    public void RestoreMotion(float currentSpeed, float increaseTimer)
    {
        currentForwardSpeed = Mathf.Clamp(
            currentSpeed, Mathf.Min(forwardSpeed, maximumForwardSpeed), maximumForwardSpeed);
        speedIncreaseTimer = Mathf.Max(0f, increaseTimer);
    }

    private void Awake()
    {
        if (RunSession.TryGetPending(out RunSnapshot carried) && carried.MaximumSpeed > 0f)
        {
            forwardSpeed = carried.StartingSpeed;
            maximumForwardSpeed = carried.MaximumSpeed;
            speedIncreaseAmount = carried.SpeedIncreaseAmount;
            speedIncreaseInterval = carried.SpeedIncreaseInterval;
            RestoreMotion(carried.ForwardSpeed, carried.SpeedTimer);
        }
        steerAction = CreateSteerAction();
        pickupEffects = GetComponent<CarPickupEffects>();
        if (visual != null)
        {
            visualBasePosition = visual.localPosition;
            visualBaseStored = true;
        }
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
        if (RunSession.TryGetPending(out RunSnapshot carried))
            RestoreMotion(carried.ForwardSpeed, carried.SpeedTimer);
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

        road.TryGetDistance(transform.position, out float initialDistance);
        RoadPositionDistance = initialDistance;

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
        driftSpeed = 0f;
        steerHoldTime = 0f;
        Skid = 0f;
        airHeight = 0f;
        verticalVelocity = 0f;
        IsAirborne = false;
        hasGroundSample = false;
        steerWheel = 0f;
        hop = hopVelocity = 0f;
        driftCharge = 0f;
        wasDrifting = false;
        DriftBoost = 0f;
        yawAngle = yawVelocity = 0f;
        pitch = pitchVelocity = 0f;
        filteredAcceleration = 0f;
        hasSpeedSample = false;
        IsDriveReady = true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        float deltaTime = Time.deltaTime;

        if (!managedDifficulty) UpdateForwardSpeed(deltaTime);

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

        float rawSteer = Mathf.Clamp(
            steerAction.ReadValue<float>(),
            -1f,
            1f
        );
        float steerInput = SmoothSteering(rawSteer, deltaTime);

        float forwardDistance = currentForwardSpeed * EnvironmentSpeedMultiplier * deltaTime;
        float accelerationMultiplier = 1f;
        bool captured = false;

        if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
        {
            if (!pickupEffects.SimulationActive)
            {
                return;
            }

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
        UpdateSkid(steerInput, currentMaxLateralSpeed, deltaTime);

        float surfaceGrip = EnvironmentSteeringGrip;
        if (icyRoad)
        {
            bool releasedSteering = Mathf.Abs(steerInput) < 0.01f;
            bool countersteering = steerInput * lateralSpeed < 0f;
            surfaceGrip *= releasedSteering ? iceReleaseGrip
                : countersteering ? 1f : iceSteeringGrip;
        }

        if (IsAirborne) surfaceGrip *= airSteeringControl;
        surfaceGrip *= 1f - Skid * skidSteeringLoss;

        lateralSpeed = Mathf.MoveTowards(
            lateralSpeed,
            steerInput * currentMaxLateralSpeed,
            currentLateralAcceleration * accelerationMultiplier * surfaceGrip * deltaTime
        );

        if (captured)
        {
            lateralSpeed = 0f;
        }

        float nextDistance = RoadPositionDistance + forwardDistance;
        if (!road.TrySample(nextDistance, out RoadPathPoint next)) return;

        float limit = Mathf.Max(
            0f,
            next.Width * 0.5f - edgeMargin
        );

        float centrifugalAccel = 0f;
        if (deltaTime > 0.0001f)
        {
            float speed = forwardDistance / deltaTime;
            centrifugalAccel = -SampleYawRate(nextDistance) * speed * speed;
            float gripScale = Mathf.Min(2f, 1f / Mathf.Max(0.3f, surfaceGrip));
            float targetDrift = IsAirborne ? driftSpeed
                : Mathf.Clamp(centrifugalAccel / gravity, -1.5f, 1.5f) * centrifugalDrift * gripScale;
            driftSpeed = Mathf.MoveTowards(driftSpeed, targetDrift, DriftResponse * deltaTime);
        }
        else driftSpeed = 0f;

        float desiredOffset =
            lateralOffset +
            (lateralSpeed + driftSpeed + holdSide * Skid * skidSlide) * deltaTime;

        lateralOffset = Mathf.Clamp(
            desiredOffset,
            -limit,
            limit
        );

        if (!Mathf.Approximately(
                desiredOffset,
                lateralOffset))
        {
            float wallSide = Mathf.Sign(desiredOffset);
            if (lateralSpeed * wallSide > 0f) lateralSpeed = 0f;
        }

        Vector3 groundPosition =
            next.Position +
            next.Right * lateralOffset +
            next.Up * heightOffset;

        Vector3 airDirection = UpdateVertical(next, groundPosition.y, forwardDistance, deltaTime);
        UpdateBody(centrifugalAccel, forwardDistance, deltaTime);

        transform.position = groundPosition + Vector3.up * airHeight;

        RoadPositionDistance = nextDistance;
        RoadDistanceTravelled += forwardDistance;

        ApplyRotation(
            next,
            deltaTime,
            airDirection
        );
    }

    private void UpdateSkid(float steerInput, float maxLateralSpeed, float deltaTime)
    {
        if (IsAirborne || Mathf.Abs(steerInput) < 0.01f) steerHoldTime = 0f;
        else
        {
            if (Mathf.Sign(steerInput) != holdSide) steerHoldTime = 0f;
            holdSide = Mathf.Sign(steerInput);
            steerHoldTime += deltaTime * Mathf.Abs(lateralSpeed) / maxLateralSpeed;
        }
        Skid = Mathf.MoveTowards(
            Skid,
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(skidOnsetTime, skidOnsetTime + skidRampTime, steerHoldTime)),
            deltaTime / skidRecoveryTime);
        UpdateDrift(steerHoldTime > 0f, deltaTime);
    }

    private void UpdateDrift(bool holding, float deltaTime)
    {
        bool drifting = Skid > 0.15f;
        if (drifting && !wasDrifting && !IsAirborne) hopVelocity = driftHopSpeed;
        if (drifting && holding && Skid > 0.9f) driftCharge += deltaTime;
        else if (!holding || !drifting)
        {
            if (driftCharge >= turboChargeTime) FireTurbo();
            driftCharge = 0f;
        }
        wasDrifting = drifting;
        DriftBoost = Mathf.MoveTowards(DriftBoost, 0f, deltaTime / turboDuration);
    }

    private void FireTurbo()
    {
        DriftBoost = 1f;
        pitchVelocity -= turboKick;
        hopVelocity = Mathf.Max(hopVelocity, driftHopSpeed * 0.6f);
        DriftBoosted?.Invoke(turboDuration);
    }

    private float SampleYawRate(float distance)
    {
        const float half = 2f;
        if (!road.TrySample(distance - half, out RoadPathPoint from) ||
            !road.TrySample(distance + half, out RoadPathPoint to)) return 0f;
        Vector3 a = Vector3.ProjectOnPlane(from.Forward, Vector3.up);
        Vector3 b = Vector3.ProjectOnPlane(to.Forward, Vector3.up);
        if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return 0f;
        return Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad / (2f * half);
    }

    private Vector3 UpdateVertical(RoadPathPoint point, float groundY, float forwardDistance, float deltaTime)
    {
        Vector3 flat = new Vector3(point.Forward.x, 0f, point.Forward.z);
        float flatLength = flat.magnitude;
        float slope = flatLength > 1e-3f ? point.Forward.y / flatLength : 0f;
        float horizontalSpeed = deltaTime > 0.0001f ? forwardDistance / deltaTime : 0f;
        float groundVelocity = slope * horizontalSpeed;
        Vector3 flatDirection = flatLength > 1e-3f ? flat / flatLength : Vector3.forward;

        if (deltaTime <= 0.0001f)
        {
            if (IsAirborne) IsAirborne = false;
            airHeight = 0f;
            verticalVelocity = groundVelocity;
            previousGroundY = groundY;
            previousGroundVelocity = groundVelocity;
            hasGroundSample = true;
            return point.Forward;
        }

        float gravityNow = gravity;
        if (!IsAirborne)
        {
            float roadAcceleration = hasGroundSample ? (groundVelocity - previousGroundVelocity) / deltaTime : 0f;
            if (hasGroundSample && horizontalSpeed > 1f && roadAcceleration < -gravityNow)
            {
                IsAirborne = true;
                verticalVelocity = maxLaunchSpeed > 0f ? Mathf.Min(groundVelocity, maxLaunchSpeed) : groundVelocity;
                airHeight = 0f;
                bounceVelocity += 0.6f;
            }
            else
            {
                verticalVelocity = groundVelocity;
                airHeight = 0f;
            }
        }

        if (IsAirborne)
        {
            float step = Mathf.Min(deltaTime, 0.05f);
            verticalVelocity -= gravityNow * (verticalVelocity < 0f ? fallGravityMultiplier : 1f) * step;
            float carY = previousGroundY + airHeight + verticalVelocity * step;
            airHeight = carY - groundY;
            if (airHeight <= 0f)
            {
                float impact = Mathf.Max(0f, groundVelocity - verticalVelocity);
                airHeight = 0f;
                IsAirborne = false;
                verticalVelocity = groundVelocity;
                float kick = Mathf.Sqrt(suspensionStiffness) / 0.6f;
                bounceVelocity -= Mathf.Min(impact * LandingCompression, maxSuspensionTravel) * kick;
                if (impact > 1f) Landed?.Invoke(impact);
            }
        }

        previousGroundY = groundY;
        previousGroundVelocity = groundVelocity;
        hasGroundSample = true;
        return flatDirection * horizontalSpeed + Vector3.up * verticalVelocity;
    }

    private void UpdateBody(float centrifugalAccel, float forwardDistance, float deltaTime)
    {
        if (deltaTime <= 0.0001f) return;
        float step = Mathf.Min(deltaTime, 0.05f);

        float measured = (lateralSpeed - previousLateralSpeed) / deltaTime;
        previousLateralSpeed = lateralSpeed;
        steerLateralAccel = Mathf.Lerp(steerLateralAccel, measured, 1f - Mathf.Exp(-10f * step));

        float felt = IsAirborne ? 0f : centrifugalAccel - steerLateralAccel * 0.5f;
        LateralG = Mathf.Lerp(LateralG, felt / gravity, 1f - Mathf.Exp(-8f * step));
        float rollTarget = Mathf.Clamp(-felt / gravity * rollPerG, -maxBodyRoll, maxBodyRoll)
            - holdSide * Skid * driftLean;

        rollVelocity += (suspensionStiffness * (rollTarget - roll) - suspensionDamping * rollVelocity) * step;
        roll += rollVelocity * step;

        bounceVelocity += (-suspensionStiffness * bounce - suspensionDamping * bounceVelocity) * step;
        bounce = Mathf.Clamp(bounce + bounceVelocity * step, -maxSuspensionTravel, maxSuspensionTravel * 0.5f);

        float horizontalSpeed = forwardDistance / deltaTime;

        float acceleration = hasSpeedSample
            ? Mathf.Clamp((horizontalSpeed - previousHorizontalSpeed) / deltaTime, -25f, 25f) : 0f;
        previousHorizontalSpeed = horizontalSpeed;
        hasSpeedSample = true;
        filteredAcceleration = Mathf.Lerp(filteredAcceleration, acceleration, 1f - Mathf.Exp(-6f * step));
        float pitchTarget = IsAirborne ? 0f
            : Mathf.Clamp(-filteredAcceleration * PitchPerAcceleration, -maxBodyPitch, maxBodyPitch);
        pitchVelocity += (suspensionStiffness * (pitchTarget - pitch) - suspensionDamping * pitchVelocity) * step;
        pitch += pitchVelocity * step;

        float travelSpeed = Mathf.Max(horizontalSpeed, 5f);
        float travelAngle = Mathf.Atan2(lateralSpeed, travelSpeed) * Mathf.Rad2Deg;
        float slipLead = SteerInput * SteerSlipAngle * (1f - Skid * skidSteeringLoss);
        float yawTarget = Mathf.Clamp(travelAngle + slipLead, -maxSteerAngle, maxSteerAngle)
            + holdSide * Skid * driftBodyAngle;

        hopVelocity -= 22f * step;
        hop += hopVelocity * step;
        if (hop <= 0f)
        {
            if (hopVelocity < -1.5f) bounceVelocity -= Mathf.Min(-hopVelocity * 0.03f, maxSuspensionTravel);
            hop = 0f;
            hopVelocity = 0f;
        }

        yawVelocity += (YawStiffness * (yawTarget - yawAngle) - YawDamping * yawVelocity) * step;
        yawAngle += yawVelocity * step;
    }

    private float SmoothSteering(float raw, float deltaTime)
    {
        bool windingOn = Mathf.Abs(raw) > Mathf.Abs(steerWheel) && raw * steerWheel >= 0f;
        float rate = windingOn ? steeringRiseRate : steeringReturnRate;
        steerWheel = Mathf.MoveTowards(steerWheel, raw, rate * deltaTime);
        return steerWheel;
    }

    private float RoadVibration()
    {
        if (roadVibration <= 0f || IsAirborne) return 0f;
        float speedFactor = Mathf.Clamp01(currentForwardSpeed / Mathf.Max(0.01f, maximumForwardSpeed));
        float surface = icyRoad ? 0.3f : 1f;
        float t = Time.time;
        float noise = (Mathf.PerlinNoise(t * 31f, 0.37f) - 0.5f) * 2f
            + (Mathf.PerlinNoise(t * 73f, 5.11f) - 0.5f) * 0.6f;
        return noise * roadVibration * speedFactor * speedFactor * surface;
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
        Vector3 airDirection)
    {
        float roadBlend = 1f - Mathf.Exp(-RoadFollowSharpness * deltaTime);

        Quaternion roadRotation =
            Quaternion.LookRotation(
                point.Forward,
                point.Up
            );

        Quaternion bodyRotation = roadRotation;
        float bodyBlend = roadBlend;
        if (IsAirborne && airDirection.sqrMagnitude > 0.01f)
        {
            bodyRotation = Quaternion.LookRotation(airDirection, Vector3.up);
            bodyBlend = 1f - Mathf.Exp(-AirPitchSharpness * deltaTime);
        }

        if (visual != null)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                bodyRotation,
                bodyBlend
            );

            visual.localRotation = Quaternion.Euler(pitch, yawAngle, roll);

            if (visualBaseStored)
                visual.localPosition = visualBasePosition + Vector3.up * (bounce + RoadVibration() + hop);

            return;
        }

        Quaternion target =
            bodyRotation *
            Quaternion.Euler(
                pitch,
                yawAngle,
                roll
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            target,
            bodyBlend
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