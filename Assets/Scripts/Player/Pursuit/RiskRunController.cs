// Updates the pursuit simulation using the current driving pace.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
[RequireComponent(typeof(AutoDriveCar), typeof(CarPickupEffects))]
public sealed class RiskRunController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float policeClosingSpeed = 0.5f;

    private AutoDriveCar car;
    private CarPickupEffects effects;
    private bool restarting;
    private float slowedUntil;
    private bool hasShooting;
    private bool restartOwnedByGame;

    public void SetClosingSpeed(float metresPerSecond)
    {
        policeClosingSpeed = Mathf.Clamp(metresPerSecond, -3f, 1f);
    }

    public void ApplyShotSlowdown()
    {
        if (effects == null || effects.RunState.IsGameOver) return;
        effects.RepelPolice(8f);
        slowedUntil = Mathf.Max(slowedUntil, Time.time + 4f);
        ConfigureSpeeds();
    }

    private void Awake()
    {
        car = GetComponent<AutoDriveCar>();
        hasShooting = GetComponent<OutlawShooting>() != null;
        effects = GetComponent<CarPickupEffects>();

        restartOwnedByGame = Object.FindFirstObjectByType<OutlawGameManager>() != null;
        ConfigureSpeeds();
    }

    private void Update()
    {
        if (effects == null || !effects.isActiveAndEnabled) return;
        ConfigureSpeeds();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (effects.RunState.IsGameOver)
        {
            if (!restartOwnedByGame && !restarting && keyboard.rKey.wasPressedThisFrame)
            {
                restarting = true;
                Scene activeScene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    activeScene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(activeScene.path);
#endif
            }
            return;
        }

        if (!hasShooting && keyboard.spaceKey.wasPressedThisFrame)
            effects.Clear();
    }

    // Translate the configured closing rate into player and pursuer speeds.
    private void ConfigureSpeeds()
    {
        if (effects == null) return;
        effects.SetSimulationActive(car != null && car.isActiveAndEnabled && car.IsDriveReady);
        if (car != null)
        {
            float policeSpeed = car.BaseForwardSpeed + policeClosingSpeed;
            float effectivePoliceSpeed = Time.time < slowedUntil
                ? Mathf.Max(0f, Mathf.Min(policeSpeed, car.BaseForwardSpeed - 6f))
                : policeSpeed;
            effects.ConfigurePursuit(car.BaseForwardSpeed * car.EnvironmentSpeedMultiplier, effectivePoliceSpeed);
        }
    }
}
