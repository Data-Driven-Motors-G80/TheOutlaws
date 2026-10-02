using UnityEngine;

public class FuelTank : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fuelAmount = 50f;
    public float FuelAmount => fuelAmount;

    private float bobHeight = 0.12f;
    private float bobSpeed = 5f;
    private float flipSpeed = 90f;

    private Transform visual;

    private void Awake()
    {
        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();

        if (sourceFilter == null || sourceRenderer == null)
        {
            visual = transform;
            return;
        }

        GameObject visualObject = new GameObject("Fuel Visual");
        visualObject.transform.SetParent(transform, false);
        MeshFilter visualFilter = visualObject.AddComponent<MeshFilter>();
        MeshRenderer visualRenderer = visualObject.AddComponent<MeshRenderer>();
        visualFilter.sharedMesh = sourceFilter.sharedMesh;
        visualRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        sourceRenderer.enabled = false;
        visual = visualObject.transform;
    }

    private void Update()
    {
        if (visual == null)
        {
            return;
        }

        // Animate only the model. The root trigger remains low and stationary.
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        visual.localPosition = Vector3.up * bobOffset;
        visual.Rotate(Vector3.up, flipSpeed * Time.deltaTime, Space.Self);
    }

}
