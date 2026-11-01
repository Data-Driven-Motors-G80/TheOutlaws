// Tracks obstacle damage, warning colours and shot feedback.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrafficLightObstacle : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly Color BarrierOrange = new Color(1f, 0.42f, 0f);
    private static readonly Color BarrierRed = new Color(0.9f, 0.04f, 0.02f);
    private static readonly Color WarningBlack = new Color(0.125f, 0.14f, 0.17f);

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;

    private WorldLabel shotLabel;
    private string environmentLabel;
    private string ShotText => string.IsNullOrEmpty(environmentLabel)
        ? ShotsRemaining.ToString() : environmentLabel + " " + ShotsRemaining;

    public void SetEnvironmentLabel(string value)
    {
        environmentLabel = value;
        if (shotLabel != null) shotLabel.SetText(ShotText);
    }

    public TrafficObstacleState CurrentState { get; private set; }
    public TrafficObstacleState InitialState { get; private set; }
    public bool StartedRed { get; private set; }

    public bool WasHit { get; private set; }

    public void MarkHit() => WasHit = true;

    public int ShotsRemaining => (int)CurrentState + 1;
    public float ProximityDrain => TrafficObstacleRules.GetProximityDrain(CurrentState);
    public float FuelPenaltyFraction => TrafficObstacleRules.GetFuelPenaltyFraction(CurrentState);

    public void Initialize(TrafficObstacleState initialState)
    {
        CacheRenderers();
        CurrentState = initialState;
        InitialState = initialState;
        WasHit = false;
        StartedRed = initialState == TrafficObstacleState.Red;
        ApplyVisualState();
        PlaceShotLabel();
    }

    public bool ApplyShot()
    {
        TrafficObstacleState before = CurrentState;
        CurrentState = TrafficObstacleRules.ApplyShot(CurrentState, out bool destroyed);
        if (!destroyed) ApplyVisualState();
        GameEvents.RaiseObstacleShot(before, destroyed);
        if (destroyed)
        {
            GameEvents.RaiseObstacleDestroyed(InitialState);
            if (shotLabel != null) shotLabel.SetVisible(false);
        }
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
        if (Application.isPlaying) CreateWarningBands();
    }

    private void CreateWarningBands()
    {

        foreach (Renderer body in renderers)
        {
            if (body == null) continue;
            Bounds bounds = body.localBounds;
            foreach (float offset in new[] { -0.3f, 0.3f })
            {
                GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = "Black Warning Band";
                band.layer = body.gameObject.layer;
                band.transform.SetParent(body.transform, false);
                band.transform.localPosition = bounds.center + Vector3.up * (bounds.size.y * offset);
                band.transform.localScale = new Vector3(bounds.size.x * 1.01f, bounds.size.y * 0.16f, bounds.size.z * 1.01f);
                Collider collider = band.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                Renderer bandRenderer = band.GetComponent<Renderer>();
                bandRenderer.sharedMaterial = RuntimeMaterials.Coloured(WarningBlack);
                bandRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }

    // Keep obstacle appearance and shot labels aligned with its damage state.
    private void ApplyVisualState()
    {

        Color barrierColour = CurrentState == TrafficObstacleState.Green
            ? BarrierOrange
            : BarrierRed;
        foreach (Renderer obstacleRenderer in renderers)
        {
            if (obstacleRenderer == null) continue;
            obstacleRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, barrierColour);
            propertyBlock.SetColor(ColorId, barrierColour);
            obstacleRenderer.SetPropertyBlock(propertyBlock);
        }

        if (shotLabel != null) shotLabel.SetText(ShotText);
    }

    private void PlaceShotLabel()
    {
        if (!Application.isPlaying || renderers.Length == 0 || renderers[0] == null) return;

        if (shotLabel == null)
        {
            shotLabel = WorldLabel.AttachToFrontFace(renderers[0], transform, ShotText, Color.white);
            shotLabel.SetBackground(WarningBlack);
        }
        shotLabel.SetText(ShotText);
        shotLabel.SetVisible(true);
    }

    private void OnEnable()
    {
        if (shotLabel != null) shotLabel.SetVisible(true);
    }

    private void OnDisable()
    {
        if (shotLabel != null) shotLabel.SetVisible(false);
    }

    private void OnDestroy()
    {
        if (shotLabel != null) Destroy(shotLabel.gameObject);
    }
}
