using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Desert-local visibility effect; the rock-field controller owns its duration.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnvironmentRoot))]
public sealed class DesertSandstorm : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float fadeInSeconds = 2f;
    [SerializeField, Min(0.5f)] private float fadeOutSeconds = 1.5f;
    [SerializeField, Min(40f)] private float minimumVisibility = 50f;
    [SerializeField, Min(1.5f)] private float visibleTravelSeconds = 2f;
    [SerializeField] private Color dustColor = new Color(0.72f, 0.51f, 0.29f);
    private EnvironmentRoot environment;
    private OutlawGameManager game;
    private bool requested, saved, originalFog;
    private FogMode originalMode;
    private Color originalColor;
    private float originalStart, originalEnd, originalDensity, gustTime;
    private Camera skyCamera;
    private Color originalSky;
    public float Intensity { get; private set; }
    public bool IsClear => Intensity <= 0.001f;
    public void SetStorm(bool active) => requested = active;

    private void Awake()
    {
        environment = GetComponent<EnvironmentRoot>();
        game = GetComponent<OutlawGameManager>();
        enabled = environment.IsDesert && environment.Player != null && game != null;
    }

    private void OnEnable()
    {
        originalFog = RenderSettings.fog;
        originalMode = RenderSettings.fogMode;
        originalColor = RenderSettings.fogColor;
        originalStart = RenderSettings.fogStartDistance;
        originalEnd = RenderSettings.fogEndDistance;
        originalDensity = RenderSettings.fogDensity;
        skyCamera = Camera.main;
        if (skyCamera != null) originalSky = skyCamera.backgroundColor;
        saved = true;
    }

    private void LateUpdate()
    {
        if (game.IsTutorial || game.State == OutlawGameState.Lost || game.State == OutlawGameState.Won)
        { Restore(); return; }
        if (game.State != OutlawGameState.Running || Time.timeScale <= 0f) return;
        bool sunny = game.PickupEffects != null && game.PickupEffects.SunnyDayActive;
        float target = requested && !sunny ? 1f : 0f;
        Intensity = Mathf.MoveTowards(Intensity, target, Time.deltaTime / (target > Intensity ? fadeInSeconds : fadeOutSeconds));
        if (IsClear) { RestoreFog(); return; }
        gustTime += Time.deltaTime;
        float strength = Intensity * (0.98f + 0.02f * Mathf.Sin(gustTime * 1.7f));
        var car = environment.Player;
        float speed = car.ForwardSpeed * car.EnvironmentSpeedMultiplier;
        if (game.PickupEffects != null) speed *= game.PickupEffects.RunState.ForwardSpeedMultiplier;
        // Fog is measured from the camera, not the bumper. Keep the car and near road readable.
        float cameraOffset = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, car.transform.position) : 30f;
        float visibility = Mathf.Max(minimumVisibility, speed * visibleTravelSeconds);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(originalColor, dustColor, strength);
        if (skyCamera == null)
        {
            skyCamera = Camera.main;
            if (skyCamera != null) originalSky = skyCamera.backgroundColor;
        }
        if (skyCamera != null) skyCamera.backgroundColor = Color.Lerp(originalSky, dustColor, strength);
        RenderSettings.fogStartDistance = Mathf.Lerp(originalStart, cameraOffset + 5f, strength);
        RenderSettings.fogEndDistance = Mathf.Lerp(originalEnd, cameraOffset + visibility, strength);
    }

    private void Restore()
    {
        requested = false; Intensity = 0f; gustTime = 0f;
        RestoreFog();
    }

    private void RestoreFog()
    {
        // Do not overwrite a destination scene's RenderSettings during portal teardown.
        if (!saved || SceneManager.GetActiveScene() != gameObject.scene) return;
        RenderSettings.fog = originalFog;
        RenderSettings.fogMode = originalMode;
        RenderSettings.fogColor = originalColor;
        RenderSettings.fogStartDistance = originalStart;
        RenderSettings.fogEndDistance = originalEnd;
        RenderSettings.fogDensity = originalDensity;
        if (skyCamera != null) skyCamera.backgroundColor = originalSky;
    }

    private void OnDisable() => Restore();
}
