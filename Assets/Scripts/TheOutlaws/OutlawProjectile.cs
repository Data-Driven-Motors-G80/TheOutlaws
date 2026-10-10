using UnityEngine;

public sealed class OutlawProjectile : MonoBehaviour
{
    private const float Speed = 34f;
    private const float MaximumRange = 30f;
    private bool targetsPolice;
    private CarPickupEffects pickupEffects;
    private ObstacleSpawner obstacles;
    private Vector3 direction;
    private bool consumed;
    private float distanceTravelled;
    private Vector3 launchPosition;

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
        behaviour.launchPosition = position;
        behaviour.targetsPolice = targetsPolice;
        behaviour.pickupEffects = effects;
        behaviour.obstacles = obstacleSpawner;
        Object.Destroy(projectile, 3f);
    }

    private void Update()
    {
        if (consumed) return;
        float distance = Mathf.Min(Speed * Time.deltaTime, MaximumRange - distanceTravelled);
        // Clamp the sweep to the remaining range and resolve the nearest hit first.
        // SphereCastAll does not guarantee hit order.
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, 0.14f,
            direction, distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            OnTriggerEnter(hit.collider);
            if (consumed) return;
        }
        transform.position += direction * distance;
        distanceTravelled += distance;
        if (distanceTravelled >= MaximumRange)
        {
            consumed = true;
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (consumed) return;
        // Include trigger callbacks in the range limit, including the bullet's radius.
        if ((other.ClosestPoint(launchPosition) - launchPosition).sqrMagnitude > MaximumRange * MaximumRange)
            return;
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

        if (obstacles.TryApplyShot(other))
        {
            consumed = true;
            Destroy(gameObject);
        }
    }
}
