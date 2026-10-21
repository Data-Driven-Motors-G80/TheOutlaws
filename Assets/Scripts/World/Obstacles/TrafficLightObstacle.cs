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

    public TrafficObstacleState CurrentState { get; private set; }
    public TrafficObstacleState InitialState { get; private set; }
    public bool StartedRed { get; private set; }
    /// <summary>The car drove into this block (shielded or not).</summary>
    public bool WasHit { get; private set; }

    public void MarkHit() => WasHit = true;
    /// <summary>Shots still needed to clear this block.</summary>
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

    /// <summary>Returns true when this shot destroys the one-shot obstacle.</summary>
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
        // Cache only body renderers before adding decorations, so tinting never turns the bands orange.
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

    private void ApplyVisualState()
    {
        // The current project only spawns one- and two-shot obstacles during the
        // normal run. Show those as orange and red respectively; shooting a red
        // obstacle once visibly lowers it to the orange state.
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

        if (shotLabel != null) shotLabel.SetText(ShotsRemaining.ToString());
    }

    private void PlaceShotLabel()
    {
        if (!Application.isPlaying || renderers.Length == 0 || renderers[0] == null) return;
        // The shot count is printed on the face the player drives towards.
        if (shotLabel == null)
        {
            shotLabel = WorldLabel.AttachToFrontFace(renderers[0], transform, ShotsRemaining.ToString(), Color.white);
            shotLabel.SetBackground(WarningBlack);
        }
        shotLabel.SetText(ShotsRemaining.ToString());
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
