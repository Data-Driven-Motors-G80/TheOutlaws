using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class OutlawShooting : MonoBehaviour
{
    private const int MaximumAmmoValue = 5;
    // No cooldown: every Space press fires (as long as there is ammo).
    private const float FireCooldown = 0f;

    private AutoDriveCar driver;
    private CarPickupEffects pickupEffects;
    private ObstacleSpawner obstacles;
    private float nextFireTime;

    public int CurrentAmmo { get; private set; } = 2;
    public int MaximumAmmo => MaximumAmmoValue;
    public float NormalizedAmmo => (float)CurrentAmmo / MaximumAmmoValue;

    public void Configure(CarPickupEffects effects, ObstacleSpawner obstacleSpawner)
    {
        driver = GetComponent<AutoDriveCar>();
        pickupEffects = effects;
        obstacles = obstacleSpawner;
        CurrentAmmo = 2;
        nextFireTime = 0f;
    }

    // Read the final visible pose after AutoDriveCar.Update applies drift and road rotation.
    private void LateUpdate()
    {
        if (Time.timeScale <= 0f || Keyboard.current == null || Time.time < nextFireTime)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TryShoot();
        }
    }

    public void SetAmmo(int amount)
    {
        CurrentAmmo = Mathf.Clamp(amount, 0, MaximumAmmoValue);
    }

    public bool AddAmmo(int amount)
    {
        if (amount <= 0 || CurrentAmmo >= MaximumAmmoValue)
        {
            return false;
        }

        CurrentAmmo = Mathf.Min(MaximumAmmoValue, CurrentAmmo + amount);
        return true;
    }

    private void TryShoot()
    {
        if (CurrentAmmo <= 0)
        {
            GameEvents.RaiseDryFire();
            return;
        }

        Transform firingFrame = driver != null ? driver.FiringFrame : transform;
        Vector3 direction = firingFrame.forward;
        Vector3 position = firingFrame.position + direction * 2.2f + firingFrame.up * 0.55f;
        OutlawProjectile.Create(
            position,
            direction,
            false,
            pickupEffects,
            obstacles,
            GetComponentsInChildren<Collider>());

        CurrentAmmo--;
        GameEvents.RaiseShotFired();
        nextFireTime = Time.time + FireCooldown;
    }
}
