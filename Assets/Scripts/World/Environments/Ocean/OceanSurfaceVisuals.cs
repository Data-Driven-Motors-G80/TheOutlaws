using System.Collections.Generic;
using UnityEngine;

/// <summary>Ocean presentation only: shader clock, surrounding water and gentle model buoyancy.</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(EnvironmentRoot))]
public sealed class OceanSurfaceVisuals : MonoBehaviour
{
    [SerializeField] private Material waterMaterial;
    [SerializeField, Min(200f)] private float seaSize = 1600f;
    [SerializeField, Range(0f, 0.25f)] private float bobHeight = 0.08f;
    [SerializeField, Range(0f, 5f)] private float rollDegrees = 1.5f;

    private static readonly int OceanTimeId = Shader.PropertyToID("_OceanTime");
    private static readonly int ChannelMaskId = Shader.PropertyToID("_ChannelMask");
    private readonly List<RoadSegment> segments = new List<RoadSegment>();
    private EnvironmentRoot environment;
    private OutlawGameManager game;
    private OceanCoastController coast;
    private Transform model;
    private Transform sea;
    private MaterialPropertyBlock surfaceProperties;
    private float waveTime;
    private Vector3 appliedBob;
    private Quaternion appliedTilt = Quaternion.identity;
    private Renderer seaRenderer;

    private void Start()
    {
        environment = GetComponent<EnvironmentRoot>();
        game = GetComponent<OutlawGameManager>();
        coast = GetComponent<OceanCoastController>();
        if (!environment.IsOcean || environment.Player == null || waterMaterial == null || game == null)
        {
            enabled = false;
            return;
        }
        // Never bob the player root: it also owns the collider and chase camera.
        Transform visible = environment.Player.FiringFrame;
        if (visible != environment.Player.transform) model = visible;
        surfaceProperties = new MaterialPropertyBlock();
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Open ocean backdrop";
        water.transform.SetParent(transform, false);
        water.transform.localScale = new Vector3(seaSize / 10f, 1f, seaSize / 10f);
        Collider collider = water.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        sea = water.transform;
        seaRenderer = water.GetComponent<Renderer>();
        seaRenderer.sharedMaterial = waterMaterial;
        seaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        FollowPlayer();
    }

    private void Update()
    {
        ReleaseModelOffset();
        if (game.State == OutlawGameState.Running) waveTime += Time.deltaTime;
    }

    private void LateUpdate()
    {
        FollowPlayer();
        // Property blocks avoid changing the saved/shared material or leaking animation into another scene.
        surfaceProperties.SetFloat(OceanTimeId, waveTime);
        surfaceProperties.SetFloat(ChannelMaskId, 0f);
        seaRenderer.SetPropertyBlock(surfaceProperties);
        surfaceProperties.SetFloat(ChannelMaskId, 1f);
        environment.Road.GetComponentsInChildren(false, segments);
        foreach (RoadSegment segment in segments)
        {
            Transform surface = segment.transform.Find("Road");
            if (surface != null && surface.TryGetComponent(out Renderer renderer))
                renderer.SetPropertyBlock(surfaceProperties);
        }
        if (model == null) return;
        float strength = coast != null && coast.Surf.IsSurfing ? 1.5f : 1f;
        appliedBob = Vector3.up * (Mathf.Sin(waveTime * 2f) * bobHeight * strength);
        appliedTilt = Quaternion.Euler(Mathf.Sin(waveTime * 1.6f) * rollDegrees * 0.5f,
            0f, Mathf.Sin(waveTime * 1.3f) * rollDegrees * strength);
        model.localPosition += appliedBob;
        model.localRotation *= appliedTilt;
    }

    private void FollowPlayer()
    {
        Vector3 player = environment.Player.transform.position;
        float waterHeight = environment.Road.TryGetPathPoint(player, out RoadPathPoint point)
            ? point.Position.y - 0.04f : player.y - 0.5f;
        sea.position = new Vector3(player.x, waterHeight, player.z);
    }

    private void ReleaseModelOffset()
    {
        if (model != null)
        {
            model.localPosition -= appliedBob;
            model.localRotation *= Quaternion.Inverse(appliedTilt);
        }
        appliedBob = Vector3.zero;
        appliedTilt = Quaternion.identity;
    }

    private void OnDisable() => ReleaseModelOffset();

    private void OnDestroy()
    {
        if (sea != null) Destroy(sea.gameObject);
    }
}
