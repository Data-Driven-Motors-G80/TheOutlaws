using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AutoDriveCar))]
public sealed class SnowSlideEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private Transform visual;

    [Header("Slide")]
    [SerializeField, Min(0f)] private float driftSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float extraTurnSpeed = 3f;
    [SerializeField, Min(0.01f)] private float slideAcceleration = 6f;
    [SerializeField, Min(0f)] private float edgeMargin = 1f;

    [Header("Feel")]
    [SerializeField, Range(0f, 30f)] private float extraYawAngle = 8f;

    private AutoDriveCar car;
    private OutlawGameManager game;
    private float slideOffset;
    private float slideSpeed;
    private float driftDirection = 1f;
    private Vector3 appliedOffset;
    private Quaternion appliedYaw = Quaternion.identity;

    private void Awake()
    {
        car = GetComponent<AutoDriveCar>();
        game = FindFirstObjectByType<OutlawGameManager>();
        if (road == null) road = FindFirstObjectByType<InfiniteRoad>();
        enabled = road != null;
    }

    private void Update()
    {
        Release();
    }

    private void OnDisable()
    {
        Release();
    }

    private void LateUpdate()
    {
        if (!road.TryGetPathPoint(transform.position, out RoadPathPoint point)) return;

        bool running = car.IsDriveReady && game != null && game.State == OutlawGameState.Running;
        float steer = running ? car.SteerInput : 0f;
        bool steering = Mathf.Abs(steer) > 0.1f;

        if (steering) driftDirection = Mathf.Sign(steer);

        slideSpeed = running
            ? Mathf.MoveTowards(slideSpeed, steering ? steer * extraTurnSpeed : driftDirection * driftSpeed, slideAcceleration * Time.deltaTime)
            : 0f;

        float lateral = Vector3.Dot(transform.position - point.Position, point.Right);
        float limit = Mathf.Max(0f, point.Width * 0.5f - edgeMargin);
        float desired = slideOffset + slideSpeed * Time.deltaTime;

        slideOffset = Mathf.Clamp(desired, -limit - lateral, limit - lateral);
        if (!Mathf.Approximately(desired, slideOffset)) slideSpeed = 0f;

        appliedOffset = point.Right * slideOffset;
        transform.position += appliedOffset;

        if (visual == null) return;
        appliedYaw = Quaternion.Euler(0f, extraYawAngle * slideSpeed / extraTurnSpeed, 0f);
        visual.localRotation *= appliedYaw;
    }

    private void Release()
    {
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
        if (visual != null) visual.localRotation *= Quaternion.Inverse(appliedYaw);
        appliedYaw = Quaternion.identity;
    }
}