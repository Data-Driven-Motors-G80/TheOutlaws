using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChaseMeter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    private AutoDriveCar playerCar;
    private HandleCrash crashHandler;
    private CarPickupEffects pickupEffects;

    [Header("Steering Condition")]
    [SerializeField, Range(0f, 1f)] private float steerThreshold = 0.15f;
    [SerializeField, Min(0f)] private float steerDrainRate = 0.07f;

    [Header("Recovery")]
    [SerializeField, Min(0f)] private float regenRate = 0.05f;

    public float Value { get; private set; } = 1f;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError($"{nameof(ChaseMeter)}: player reference is missing.", this);
            enabled = false;
            return;
        }

        playerCar = player.GetComponent<AutoDriveCar>();
        crashHandler = player.GetComponent<HandleCrash>();
        pickupEffects = player.GetComponent<CarPickupEffects>();

        if (playerCar == null || crashHandler == null)
        {
            Debug.LogError($"{nameof(ChaseMeter)}: player needs both Auto Drive Car and Handle Crash.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
        {
            // The recovery model owns the pursuit clock. Keep the existing
            // 0..1 meter as a presentation layer for ChaseCar and the HUD.
            Value = pickupEffects.RunState.IsGameOver
                ? 0f
                : Mathf.Clamp01(
                    pickupEffects.RunState.PursuitGap /
                    RiskRunState.MaximumPursuitGap);
            return;
        }

        float drain = GetSteeringDrain();

        if (drain <= 0f)
        {
            Value = Mathf.Clamp01(Value + regenRate * Time.deltaTime);
        }
        else
        {
            Value = Mathf.Clamp01(Value - drain * Time.deltaTime);
        }

        if (Value <= 0f)
        {
            crashHandler.Crash();
        }
    }

    private float GetSteeringDrain()
    {
        return Mathf.Abs(playerCar.SteerInput) >= steerThreshold ? steerDrainRate : 0f;
    }

    public void ApplyDrain(float amount)
    {
        if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
        {
            pickupEffects.TryCrash(amount);
            return;
        }

        Value = Mathf.Clamp01(Value - amount);

        if (Value <= 0f)
        {
            crashHandler.Crash();
        }
    }
}
