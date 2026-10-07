using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class OutlawShooting : MonoBehaviour
{
    private const int MaximumAmmoValue = 5;
    private const float FireCooldown = 0.3f;

    private CarPickupEffects pickupEffects;
    private ObstacleSpawner obstacles;
    private float nextFireTime;

    public int CurrentAmmo { get; private set; } = 2;
    public int MaximumAmmo => MaximumAmmoValue;
    public float NormalizedAmmo => (float)CurrentAmmo / MaximumAmmoValue;

    public void Configure(CarPickupEffects effects, ObstacleSpawner obstacleSpawner)
    {
        pickupEffects = effects;
        obstacles = obstacleSpawner;
        CurrentAmmo = 2;
        nextFireTime = 0f;
    }

    private void Update()
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
            return;
        }

        Vector3 direction = transform.forward;
        Vector3 position = transform.position + direction * 2.2f + transform.up * 0.55f;
        OutlawProjectile.Create(
            position,
            direction,
            false,
            pickupEffects,
            obstacles,
            GetComponentsInChildren<Collider>());

        CurrentAmmo--;
        nextFireTime = Time.time + FireCooldown;
    }
}
