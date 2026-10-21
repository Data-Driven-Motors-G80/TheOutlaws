using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class OutlawShooting : MonoBehaviour
{
    private const int MaximumAmmoValue = BackpackState.GunShots;
    // No cooldown: every Space press fires (as long as there is ammo).
    private const float FireCooldown = 0f;

    private AutoDriveCar driver;
    private CarPickupEffects pickupEffects;
    private ObstacleSpawner obstacles;
    private float nextFireTime;

    public int CurrentAmmo => pickupEffects != null ? pickupEffects.Backpack.Shots : 0;
    public int MaximumAmmo => MaximumAmmoValue;
    public float NormalizedAmmo => (float)CurrentAmmo / MaximumAmmoValue;

    public void Configure(CarPickupEffects effects, ObstacleSpawner obstacleSpawner)
    {
        driver = GetComponent<AutoDriveCar>();
        pickupEffects = effects;
        obstacles = obstacleSpawner;
        pickupEffects.Backpack.Clear();
        nextFireTime = 0f;
    }

    // Read the final visible pose after AutoDriveCar.Update applies drift and road rotation.
    private void LateUpdate()
    {
        if (Time.timeScale <= 0f || Keyboard.current == null || Time.time < nextFireTime || pickupEffects == null || !pickupEffects.SimulationActive || pickupEffects.RunState.IsGameOver)
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
        if (pickupEffects == null) return;
        if (amount <= 0) pickupEffects.Backpack.Clear();
        else pickupEffects.Backpack.Restore(RandomPickupOutcome.Gun, amount);
    }

    public bool AddAmmo(int amount)
    {
        if (amount <= 0 || pickupEffects == null) return false;
        return pickupEffects.Backpack.Receive(RandomPickupOutcome.Gun);
    }

    private void TryShoot()
    {
        var item = pickupEffects.Backpack.Item;
        if (!pickupEffects.UseBackpack()) return;
        if (item != RandomPickupOutcome.Gun) return;

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

        GameEvents.RaiseShotFired();
        nextFireTime = Time.time + FireCooldown;
    }
}
