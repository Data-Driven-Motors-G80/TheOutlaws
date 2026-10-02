using UnityEngine;

public sealed class OutlawAmmoPickup : MonoBehaviour
{
    private OutlawShooting shooting;
    private Transform visual;

    public static GameObject Create(Vector3 position, Vector3 up, OutlawShooting playerShooting)
    {
        GameObject pickup = new GameObject("Ammo Pickup");
        pickup.name = "Ammo Pickup";
        pickup.transform.position = position;
        pickup.transform.up = up;

        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualObject.name = "Ammo Visual";
        visualObject.transform.SetParent(pickup.transform, false);
        visualObject.transform.localScale = new Vector3(0.55f, 0.24f, 0.55f);
        Object.Destroy(visualObject.GetComponent<Collider>());

        Renderer renderer = visualObject.GetComponent<Renderer>();
        renderer.material.color = new Color(0.12f, 0.55f, 1f);

        CapsuleCollider trigger = pickup.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.height = 1.5f;
        trigger.radius = 0.75f;
        Rigidbody body = pickup.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        OutlawAmmoPickup behaviour = pickup.AddComponent<OutlawAmmoPickup>();
        behaviour.shooting = playerShooting;
        behaviour.visual = visualObject.transform;
        return pickup;
    }

    private void Update()
    {
        if (visual == null)
        {
            return;
        }

        visual.Rotate(Vector3.up, 90f * Time.deltaTime, Space.Self);
        visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.15f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<OutlawShooting>() == shooting && shooting.AddAmmo(3))
        {
            Destroy(gameObject);
        }
    }
}
