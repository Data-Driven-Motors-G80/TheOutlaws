using UnityEngine;

/// <summary>Police follower on a manually ticked finite WorldShift road. The finite controller
/// supplies simulation time, road samples and the pursuit gap explicitly.</summary>
[DisallowMultipleComponent]
public sealed class ChaseCar : MonoBehaviour
{
    private const float TurnSharpness = 8f;
    private const float EdgeMargin = 1f;
    private const float VisualGapChangeSpeed = 5f;

    private Transform player;
    private AutoDriveCar playerDriver;
    private Renderer[] policeRenderers;
    private float displayedBumperGap;
    private float previousLateralOffset;
    private System.Func<float, RoadPathPoint> finiteRoute;
    private bool finitePoseReady;

    public bool IsFiniteRoute => finiteRoute != null;
    public float DistanceToPlayer { get; private set; }

    /// <summary>Set by the rear-view camera while it exists.</summary>
    public static bool RearViewActive { get; set; }

    public static ChaseCar CreateForFiniteRoute(GameObject prefab, Transform parent, Transform targetPlayer,
        System.Func<float, RoadPathPoint> sample)
    {
        if (sample == null) throw new System.ArgumentNullException(nameof(sample));
        GameObject instance = new GameObject("Chasing police block");
        instance.transform.SetParent(parent, false);
        Outlaws.WorldShift.WorldShiftBlockVisuals.Vehicle(instance.transform, true);
        ChaseCar chase = instance.GetComponent<ChaseCar>();
        if (chase == null) chase = instance.AddComponent<ChaseCar>();
        chase.finiteRoute = sample;
        chase.player = targetPlayer;
        chase.playerDriver = targetPlayer != null ? targetPlayer.GetComponent<AutoDriveCar>() : null;
        // Never self-updates: the finite controller ticks it.
        chase.enabled = false;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        chase.policeRenderers = instance.GetComponentsInChildren<Renderer>(true);
        return chase;
    }

    public void StepFinite(float delta, float playerDistance, float lateral, float bumperGap)
    {
        if (finiteRoute == null || player == null) return;
        delta = Mathf.Max(0f, delta);
        bumperGap = Mathf.Max(0f, bumperGap);
        // A zero-time step is used when restoring a checkpoint, so stale visual
        // separation and steering do not leak into the restarted section.
        bool snap = !finitePoseReady || delta <= 0f;
        displayedBumperGap = snap ? bumperGap
            : Mathf.MoveTowards(displayedBumperGap, bumperGap, VisualGapChangeSpeed * delta);
        RoadPathPoint target = finiteRoute(playerDistance - (displayedBumperGap + 3.3f));
        float limit = Mathf.Max(0f, target.Width * .5f - EdgeMargin);
        float desired = Mathf.Clamp(lateral, -limit, limit);
        float offset = snap ? desired : Mathf.Lerp(previousLateralOffset, desired, 1f - Mathf.Exp(-TurnSharpness * delta));
        float lateralVelocity = delta > 0f && !snap ? (offset - previousLateralOffset) / delta : 0f;
        transform.position = target.Position + target.Right * offset + target.Up * .08f;
        TurnAlong(target, lateralVelocity, delta, snap);
        previousLateralOffset = offset;
        DistanceToPlayer = displayedBumperGap;
        finitePoseReady = true;
    }

    /// <summary>Render the level AI pose without adding a second steering filter.</summary>
    public void PresentFinitePose(float delta, float distance, float lateral, float playerDistance, float jolt)
    {
        if (finiteRoute == null) return;
        var point = finiteRoute(distance);
        bool snap = !finitePoseReady || delta <= 0f;
        float velocity = snap ? 0f : (lateral - previousLateralOffset) / delta;
        transform.position = point.Position + point.Right * lateral + point.Up * .08f;
        TurnAlong(point, velocity, delta, snap);
        if (jolt > 0f) transform.rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(jolt * 55f) * 4f);
        previousLateralOffset = lateral;
        displayedBumperGap = Mathf.Max(0f, playerDistance - distance - 3.3f);
        DistanceToPlayer = displayedBumperGap;
        finitePoseReady = true;
    }

    private void Awake() => policeRenderers = GetComponentsInChildren<Renderer>(true);

    private void TurnAlong(RoadPathPoint target, float lateralVelocity, float deltaTime, bool snap = false)
    {
        float forwardSpeed = playerDriver != null ? playerDriver.BaseForwardSpeed : 15f;
        float steerAngle = Mathf.Clamp(
            Mathf.Atan2(lateralVelocity, Mathf.Max(1f, forwardSpeed)) * Mathf.Rad2Deg, -20f, 20f);
        Quaternion targetRotation = Quaternion.LookRotation(target.Forward, target.Up)
            * Quaternion.Euler(0f, steerAngle, 0f);
        float blend = 1f - Mathf.Exp(-TurnSharpness * deltaTime);
        transform.rotation = snap ? targetRotation : Quaternion.Slerp(transform.rotation, targetRotation, blend);
    }

    private void OnDisable()
    {
        if (policeRenderers == null) return;
        foreach (Renderer policeRenderer in policeRenderers)
            if (policeRenderer != null) policeRenderer.forceRenderingOff = false;
    }
}
