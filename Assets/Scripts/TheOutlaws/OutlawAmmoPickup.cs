using UnityEngine;

public sealed class OutlawAmmoPickup : MonoBehaviour
{
    private OutlawShooting shooting;
    private float baseY;

    public static GameObject Create(Vector3 position, Vector3 up, OutlawShooting playerShooting)
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pickup.name = "Ammo Pickup";
        pickup.transform.position = position;
        pickup.transform.up = up;
        pickup.transform.localScale = new Vector3(0.55f, 0.24f, 0.55f);

        Renderer renderer = pickup.GetComponent<Renderer>();
        renderer.material.color = new Color(0.12f, 0.55f, 1f);

        Collider trigger = pickup.GetComponent<Collider>();
        trigger.isTrigger = true;
        if (trigger is CapsuleCollider capsule)
        {
            // A generous vertical trigger makes collection reliable on curved
            // or banked road segments without making the visual larger.
            capsule.height = 3f;
            capsule.radius = 0.75f;
        }
        Rigidbody body = pickup.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        OutlawAmmoPickup behaviour = pickup.AddComponent<OutlawAmmoPickup>();
        behaviour.shooting = playerShooting;
        behaviour.baseY = position.y;
        return pickup;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.Self);
        Vector3 position = transform.position;
        position.y = baseY + Mathf.Sin(Time.time * 3f) * 0.15f;
        transform.position = position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<OutlawShooting>() == shooting && shooting.AddAmmo(3))
        {
            Destroy(gameObject);
        }
    }
}
