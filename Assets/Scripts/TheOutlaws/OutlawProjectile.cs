using UnityEngine;

public sealed class OutlawProjectile : MonoBehaviour
{
    private const float Speed = 34f;
    private bool targetsPolice;
    private CarPickupEffects pickupEffects;
    private ObstacleSpawner obstacles;
    private Vector3 direction;
    private bool consumed;

    public static void Create(
        Vector3 position,
        Vector3 direction,
        bool targetsPolice,
        CarPickupEffects effects,
        ObstacleSpawner obstacleSpawner,
        Collider[] playerColliders)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = targetsPolice ? "Rear Shot" : "Forward Shot";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * 0.28f;

        Renderer renderer = projectile.GetComponent<Renderer>();
        renderer.material.color = targetsPolice
            ? new Color(0.2f, 0.75f, 1f)
            : new Color(1f, 0.75f, 0.05f);

        SphereCollider projectileCollider = projectile.GetComponent<SphereCollider>();
        projectileCollider.isTrigger = true;
        Rigidbody body = projectile.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        foreach (Collider playerCollider in playerColliders)
        {
            Physics.IgnoreCollision(projectileCollider, playerCollider);
        }

        OutlawProjectile behaviour = projectile.AddComponent<OutlawProjectile>();
        behaviour.direction = direction.normalized;
        behaviour.targetsPolice = targetsPolice;
        behaviour.pickupEffects = effects;
        behaviour.obstacles = obstacleSpawner;
        Object.Destroy(projectile, 3f);
    }

    private void Update()
    {
        transform.position += direction * Speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (consumed) return;
        if (targetsPolice)
        {
            ChaseCar police = other.GetComponentInParent<ChaseCar>();
            if (police == null)
            {
                return;
            }

            consumed = true;
            police.HitByShot();
            Destroy(gameObject);
            return;
        }

        if (obstacles == null || !other.transform.IsChildOf(obstacles.transform))
        {
            return;
        }

        Transform obstacle = other.transform;
        while (obstacle.parent != null && obstacle.parent != obstacles.transform)
        {
            obstacle = obstacle.parent;
        }

        Destroy(obstacle.gameObject);
        Destroy(gameObject);
    }
}
