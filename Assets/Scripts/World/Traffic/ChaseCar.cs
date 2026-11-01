// Positions pursuing police behind the player and handles shot reactions.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChaseCar : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private Transform player;
    [SerializeField] private ChaseMeter meter;
    [SerializeField] private BoxCollider chaseCollider;
    [SerializeField] private BoxCollider playerCollider;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float turnSharpness = 8f;
    [SerializeField, Min(0f)] private float edgeMargin = 1f;

    [Header("Visible Pursuit Range")]
    [SerializeField, Min(1f)] private float maximumVisualGap = 4.5f;
    [SerializeField, Min(0f)] private float minimumVisualGap = 0.35f;
    [SerializeField, Min(0f)] private float visualGapChangeSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float visibleBelowMeterValue = 0.5f;
    private Renderer[] policeRenderers;

    private float startingBumperGap;
    private float displayedBumperGap;
    private float relativeLateralOffset;
    private float previousLateralOffset;
    private AutoDriveCar playerDriver;
    private OutlawGameManager game;
    private float heightOffset;
    private CarPickupEffects effects;

    public void HitByShot()
    {
        if (player != null)
            player.GetComponent<RiskRunController>()?.ApplyShotSlowdown();
    }

    public float DistanceToPlayer { get; private set; }

    private void Awake()
    {
        game = FindFirstObjectByType<OutlawGameManager>();
        policeRenderers = GetComponentsInChildren<Renderer>(true);
        if (player != null)
        {
            effects = player.GetComponent<CarPickupEffects>();
            playerDriver = player.GetComponent<AutoDriveCar>();
        }
        if (chaseCollider == null)
        {
            chaseCollider =
                GetComponentInChildren<BoxCollider>();
        }

        if (player != null && playerCollider == null)
        {
            playerCollider =
                player.GetComponentInChildren<BoxCollider>();
        }
    }

    private void Start()
    {
        if (!HasValidSetup())
        {
            enabled = false;
            return;
        }

        if (!road.TryGetPathPoint(
                transform.position,
                out RoadPathPoint chasePoint))
        {
            LogError("could not find the chase car road position.");
            enabled = false;
            return;
        }

        if (!road.TryGetPathPoint(
                player.position,
                out RoadPathPoint playerPoint))
        {
            LogError("could not find the player road position.");
            enabled = false;
            return;
        }

        startingBumperGap = Mathf.Max(minimumVisualGap, maximumVisualGap);
        displayedBumperGap = GetTargetBumperGap();
        DistanceToPlayer = displayedBumperGap;

        Vector3 chaseOffset =
            transform.position - chasePoint.Position;

        relativeLateralOffset = 0f;
        previousLateralOffset = Vector3.Dot(chaseOffset, chasePoint.Right);

        heightOffset = Vector3.Dot(
            chaseOffset,
            chasePoint.Up
        );

        IgnoreOtherChaseCars();
        UpdateVisibility();
    }

    private void LateUpdate()
    {
        UpdateVisibility();
        if (!road.TryGetPathPoint(
                player.position,
                out RoadPathPoint playerPoint))
        {
            return;
        }

        float chaseFrontLength = GetColliderExtent(
            chaseCollider,
            playerPoint.Forward
        );

        float playerBackLength = GetColliderExtent(
            playerCollider,
            playerPoint.Forward
        );

        displayedBumperGap = Mathf.MoveTowards(
            displayedBumperGap,
            GetTargetBumperGap(),
            (game != null && game.IsTutorialHandoff
                ? Mathf.Min(visualGapChangeSpeed, 0.8f) : visualGapChangeSpeed) * Time.deltaTime);

        float centerFollowDistance =
            displayedBumperGap +
            chaseFrontLength +
            playerBackLength;

        if (!TryGetPointBehind(
                playerPoint,
                centerFollowDistance,
                out RoadPathPoint target))
        {
            return;
        }

        Vector3 playerOffset =
            player.position - playerPoint.Position;

        float playerLateralOffset = Vector3.Dot(
            playerOffset,
            playerPoint.Right
        );

        float desiredLateralOffset =
            playerLateralOffset +
            relativeLateralOffset;

        float limit = Mathf.Max(
            0f,
            target.Width * 0.5f - edgeMargin
        );

        desiredLateralOffset = Mathf.Clamp(
            desiredLateralOffset,
            -limit,
            limit
        );

        Vector3 targetPosition =
            target.Position +
            target.Right * desiredLateralOffset +
            target.Up * heightOffset;

        transform.position = targetPosition;

        float lateralVelocity = Time.deltaTime > 0f
            ? (desiredLateralOffset - previousLateralOffset) / Time.deltaTime : 0f;
        previousLateralOffset = desiredLateralOffset;
        float forwardSpeed = playerDriver != null ? playerDriver.BaseForwardSpeed : 15f;
        float steerAngle = Mathf.Clamp(
            Mathf.Atan2(lateralVelocity, Mathf.Max(1f, forwardSpeed)) * Mathf.Rad2Deg,
            -20f, 20f);
        Quaternion targetRotation = Quaternion.LookRotation(target.Forward, target.Up)
            * Quaternion.Euler(0f, steerAngle, 0f);

        float blend =
            1f - Mathf.Exp(
                -turnSharpness * Time.deltaTime
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            blend
        );

        DistanceToPlayer = displayedBumperGap;
    }

    private void UpdateVisibility()
    {

        bool alwaysVisible = RearViewActive;
        float normalizedGap = effects != null && effects.isActiveAndEnabled
            ? effects.RunState.PursuitGap / RiskRunState.MaximumPursuitGap
            : meter != null ? meter.Value : 1f;
        foreach (Renderer policeRenderer in policeRenderers)
            if (policeRenderer != null)
                policeRenderer.forceRenderingOff = !alwaysVisible && normalizedGap > visibleBelowMeterValue;
    }

    public static bool RearViewActive { get; set; }

    private void OnDisable()
    {
        if (policeRenderers == null) return;
        foreach (Renderer policeRenderer in policeRenderers)
            if (policeRenderer != null) policeRenderer.forceRenderingOff = false;
    }

    // Convert pursuit state into a visible gap behind the player.
    private float GetTargetBumperGap()
    {

        float pursuitRatio = effects != null
            ? effects.RunState.PursuitGap / RiskRunState.InitialPursuitGap
            : meter.Value;

        if (game != null && game.IsTutorial)
        {
            float twoHitRatio = 1f - 2f * TrafficObstacleRules.GreenProximityDrain;
            pursuitRatio += twoHitRatio
                - RiskRunState.TutorialInitialPursuitGap / RiskRunState.InitialPursuitGap;
        }
        float normalGap = Mathf.Max(minimumVisualGap, Mathf.LerpUnclamped(
            minimumVisualGap,
            startingBumperGap,
            pursuitRatio));

        if (game == null || game.IsTutorial || game.StartedAfterTutorial) return normalGap;
        float openingBlend = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(3f, 10f, game.ElapsedSeconds));
        return Mathf.Lerp(Mathf.Min(normalGap, 1f), normalGap, openingBlend);
    }

    private float GetColliderExtent(
        BoxCollider box,
        Vector3 direction)
    {
        Transform boxTransform = box.transform;

        Vector3 scale = boxTransform.lossyScale;

        Vector3 halfSize = new Vector3(
            box.size.x * Mathf.Abs(scale.x) * 0.5f,
            box.size.y * Mathf.Abs(scale.y) * 0.5f,
            box.size.z * Mathf.Abs(scale.z) * 0.5f
        );

        direction.Normalize();

        return
            Mathf.Abs(Vector3.Dot(
                direction,
                boxTransform.right
            )) * halfSize.x +
            Mathf.Abs(Vector3.Dot(
                direction,
                boxTransform.up
            )) * halfSize.y +
            Mathf.Abs(Vector3.Dot(
                direction,
                boxTransform.forward
            )) * halfSize.z;
    }

    // Prevent police colliders from pushing one another out of formation.
    private void IgnoreOtherChaseCars()
    {
        Collider[] ownColliders =
            GetComponentsInChildren<Collider>();

        ChaseCar[] chaseCars =
            FindObjectsByType<ChaseCar>(
                FindObjectsSortMode.None
            );

        foreach (ChaseCar chaseCar in chaseCars)
        {
            if (chaseCar == this)
            {
                continue;
            }

            Collider[] otherColliders =
                chaseCar.GetComponentsInChildren<Collider>();

            foreach (Collider ownCollider in ownColliders)
            {
                foreach (Collider otherCollider in otherColliders)
                {
                    Physics.IgnoreCollision(
                        ownCollider,
                        otherCollider
                    );
                }
            }
        }
    }

    private bool TryGetPointBehind(
        RoadPathPoint from,
        float distance,
        out RoadPathPoint result)
    {
        return road.TryGetPointAhead(from.Position, -distance, out result);
    }

    private bool HasValidSetup()
    {
        if (road == null)
        {
            LogError("road reference is missing.");
            return false;
        }

        if (player == null)
        {
            LogError("player reference is missing.");
            return false;
        }

        if (meter == null)
        {
            LogError("chase meter reference is missing.");
            return false;
        }

        if (chaseCollider == null)
        {
            LogError("chase car Box Collider is missing.");
            return false;
        }

        if (playerCollider == null)
        {
            LogError("player Box Collider is missing.");
            return false;
        }

        return true;
    }

    private void LogError(string message)
    {
        Debug.LogError(
            $"{nameof(ChaseCar)}: {message}",
            this
        );
    }
}
