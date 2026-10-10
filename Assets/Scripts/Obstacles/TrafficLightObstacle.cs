using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrafficLightObstacle : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly Color GreenColor = new Color(0.08f, 0.8f, 0.16f);
    private static readonly Color YellowColor = new Color(1f, 0.72f, 0.04f);
    private static readonly Color RedColor = new Color(0.9f, 0.03f, 0.03f);

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;

    public TrafficObstacleState CurrentState { get; private set; }
    public bool StartedRed { get; private set; }
    public float ProximityDrain => TrafficObstacleRules.GetProximityDrain(CurrentState);
    public float FuelPenaltyFraction => TrafficObstacleRules.GetFuelPenaltyFraction(CurrentState);

    public void Initialize(TrafficObstacleState initialState)
    {
        CacheRenderers();
        CurrentState = initialState;
        StartedRed = initialState == TrafficObstacleState.Red;
        ApplyVisualState();
    }

    /// <summary>Returns true when this shot destroys the green obstacle.</summary>
    public bool ApplyShot()
    {
        CurrentState = TrafficObstacleRules.ApplyShot(CurrentState, out bool destroyed);
        if (!destroyed) ApplyVisualState();
        return destroyed;
    }

    public Vector3 GetRewardPosition(float forwardOffset = 2.5f)
    {
        CacheRenderers();
        if (renderers.Length == 0)
            return transform.position + transform.forward * forwardOffset + transform.up * 0.32f;

        Bounds bounds = renderers[0].bounds;
        Vector3 up = transform.up.normalized;
        Vector3 extents = bounds.extents;
        float projectedExtent = Mathf.Abs(up.x) * extents.x
            + Mathf.Abs(up.y) * extents.y
            + Mathf.Abs(up.z) * extents.z;
        Vector3 ground = bounds.center - up * projectedExtent;
        return ground + transform.forward * forwardOffset + up * 0.32f;
    }

    private void CacheRenderers()
    {
        if (renderers != null) return;
        renderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
    }

    private void ApplyVisualState()
    {
        Color color = CurrentState == TrafficObstacleState.Red
            ? RedColor
            : CurrentState == TrafficObstacleState.Yellow ? YellowColor : GreenColor;

        foreach (Renderer obstacleRenderer in renderers)
        {
            if (obstacleRenderer == null) continue;
            obstacleRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            obstacleRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
