using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChaseCar : MonoBehaviour
{
    private const float SampleStep = 2f;

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
    [SerializeField, Min(1f)] private float maximumVisualGap = 8f;
    [SerializeField, Min(0f)] private float minimumVisualGap = 0.35f;

    private float startingBumperGap;
    private float relativeLateralOffset;
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
        if (player != null) effects = player.GetComponent<CarPickupEffects>();
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

        float centerDistance = Vector3.Distance(
            transform.position,
            player.position
        );

        float chaseFrontLength = GetColliderExtent(
            chaseCollider,
            chasePoint.Forward
        );

        float playerBackLength = GetColliderExtent(
            playerCollider,
            playerPoint.Forward
        );

        float sceneBumperGap = Mathf.Max(
            0f,
            centerDistance - chaseFrontLength - playerBackLength);

        // The gameplay model begins with a 45 m pursuit gap, while the scene
        // cars were originally placed only a few metres apart. Use a readable
        // visual range so a collision produces an obvious police advance.
        startingBumperGap = Mathf.Max(sceneBumperGap, maximumVisualGap);

        Vector3 chaseOffset =
            transform.position - chasePoint.Position;

        Vector3 playerOffset =
            player.position - playerPoint.Position;

        float chaseLateralOffset = Vector3.Dot(
            chaseOffset,
            chasePoint.Right
        );

        float playerLateralOffset = Vector3.Dot(
            playerOffset,
            playerPoint.Right
        );

        relativeLateralOffset = 0f;

        heightOffset = Vector3.Dot(
            chaseOffset,
            chasePoint.Up
        );

        IgnoreOtherChaseCars();
    }

    private void LateUpdate()
    {
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

        // Include the buffer above the initial gap: rear shots must visibly
        // increase separation even while the HUD's normalized bar is full.
        float pursuitRatio = effects != null
            ? effects.RunState.PursuitGap / RiskRunState.InitialPursuitGap
            : meter.Value;
        float bumperGap = Mathf.LerpUnclamped(
            minimumVisualGap,
            startingBumperGap,
            pursuitRatio);

        float centerFollowDistance =
            bumperGap +
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

        Vector3 movement =
            targetPosition - transform.position;

        transform.position = targetPosition;

        Vector3 movementDirection =
            Vector3.ProjectOnPlane(
                movement,
                target.Up
            );

        if (movementDirection.sqrMagnitude < 0.0001f)
        {
            movementDirection = target.Forward;
        }
        else
        {
            movementDirection.Normalize();
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                movementDirection,
                target.Up
            );

        float blend =
            1f - Mathf.Exp(
                -turnSharpness * Time.deltaTime
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            blend
        );

        DistanceToPlayer = bumperGap;
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
        result = from;
        float travelled = 0f;

        while (travelled < distance)
        {
            float step = Mathf.Min(
                SampleStep,
                distance - travelled
            );

            Vector3 samplePosition =
                result.Position -
                result.Forward * step;

            if (!road.TryGetPathPoint(
                    samplePosition,
                    out result))
            {
                return false;
            }

            travelled += step;
        }

        return true;
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
