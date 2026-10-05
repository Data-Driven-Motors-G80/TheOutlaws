using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class HandleCrash : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObstacleSpawner obstacles;
    [SerializeField] private ChaseMeter meter;
    [SerializeField] private CarPickupEffects pickupEffects;

    [Header("Obstacle Collision")]
    [SerializeField, Range(0f, 1f)]
    private float crashDrainAmount = RiskRunState.DefaultCrashProximityDrain;

    private bool restarting;
    private FuelMeter fuelMeter;
    private readonly Dictionary<Transform, Vector3> contactedObstacles = new Dictionary<Transform, Vector3>();

    private void Awake()
    {
        fuelMeter = GetComponent<FuelMeter>();

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        if (pickupEffects == null)
        {
            pickupEffects = GetComponent<CarPickupEffects>();
        }
    }

    private void OnDisable()
    {
        contactedObstacles.Clear();
    }

    private void Reset()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        Renderer visual =
            GetComponentInChildren<Renderer>();

        if (visual != null)
        {
            box.center =
                transform.InverseTransformPoint(
                    visual.bounds.center
                );

            box.size = visual.bounds.size;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (restarting)
        {
            return;
        }

        FuelTank tank = other.GetComponent<FuelTank>();
        if (tank != null)
        {
            if (fuelMeter != null)
            {
                fuelMeter.ChangeFuel(tank.FuelAmount);
            }
            if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
            {
                pickupEffects.RefillFuel(tank.FuelAmount);
            }
            Destroy(other.gameObject);
            return;
        }

        ChaseCar chaseCar =
            other.GetComponentInParent<ChaseCar>();

        if (chaseCar != null)
        {
            if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
            {
                if (pickupEffects.TryConsumeShield())
                {
                    return;
                }

                pickupEffects.TryCrash(crashDrainAmount);
                return;
            }

            Crash();
            return;
        }

        if (obstacles == null ||
            !other.transform.IsChildOf(
                obstacles.transform
            ))
        {
            return;
        }

        if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
        {
            Transform obstacle = other.transform;
            while (obstacle.parent != null && obstacle.parent != obstacles.transform)
            {
                obstacle = obstacle.parent;
            }

            if (contactedObstacles.TryGetValue(obstacle, out Vector3 previousPlacement)
                && (previousPlacement - obstacle.position).sqrMagnitude < 0.01f)
            {
                return;
            }

            contactedObstacles[obstacle] = obstacle.position;
            if (pickupEffects.TryConsumeShield())
            {
                return;
            }

            pickupEffects.TryCrash(crashDrainAmount);
            return;
        }

        if (meter != null)
        {
            meter.ApplyDrain(crashDrainAmount);
        }
    }

    public void Crash()
    {
        if (restarting)
        {
            return;
        }

        if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
        {
            if (pickupEffects.TryConsumeShield())
            {
                return;
            }

            // A collision drains proximity. The run stays in the same scene and
            // is restarted explicitly with R after capture.
            pickupEffects.TryCrash(crashDrainAmount);
            return;
        }

        restarting = true;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void OutOfFuel()
    {
        if (restarting)
        {
            return;
        }

        if (pickupEffects != null && pickupEffects.isActiveAndEnabled)
        {
            pickupEffects.EndRun();
            return;
        }

        if (meter != null)
        {
            meter.ApplyDrain(1f);
        }
    }
}
