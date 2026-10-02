using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
[RequireComponent(typeof(AutoDriveCar), typeof(CarPickupEffects))]
public sealed class RiskRunController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float policeSpeed = 14f;

    public float PoliceSpeed => policeSpeed;

    public void SetPoliceSpeed(float value)
    {
        policeSpeed = Mathf.Max(0f, value);
        ConfigureSpeeds();
    }

    private AutoDriveCar car;
    private CarPickupEffects effects;
    private bool restarting;

    private void Awake()
    {
        car = GetComponent<AutoDriveCar>();
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

        if (keyboard.spaceKey.wasPressedThisFrame)
            effects.Clear();
    }

    private void ConfigureSpeeds()
    {
        if (effects == null) return;
        effects.SetSimulationActive(car != null && car.isActiveAndEnabled && car.IsDriveReady);
        if (car != null) effects.ConfigurePursuit(car.BaseForwardSpeed, policeSpeed);
    }
}
