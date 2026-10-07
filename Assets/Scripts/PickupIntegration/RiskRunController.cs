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

    // The manager owns the gameplay clock; preserve shot slowdown on top of difficulty.
    public void SetClosingSpeed(float metresPerSecond)
    {
        policeClosingSpeed = Mathf.Clamp(metresPerSecond, -0.5f, 1f);
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
            if (!restarting && keyboard.rKey.wasPressedThisFrame)
            {
                restarting = true;
                Scene activeScene = SceneManager.GetActiveScene();
                // The editor prototype can be played before it is added to a build profile.
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    activeScene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(activeScene.path);
#endif
            }
            return;
        }

        // In The Outlaws, Space belongs exclusively to shooting. Preserve the
        // original bail-out control only for the separate pickup playground.
        if (!hasShooting && keyboard.spaceKey.wasPressedThisFrame)
            effects.Clear();
    }

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
            effects.ConfigurePursuit(car.BaseForwardSpeed, effectivePoliceSpeed);
        }
    }
}
