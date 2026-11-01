// Presents a fuel tank and tracks whether it was collected.
using UnityEngine;

public class FuelTank : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fuelAmount = 40f;
    public float FuelAmount => fuelAmount;
    public bool Collected { get; private set; }

    public void MarkCollected() => Collected = true;

    private float bobHeight = 0.12f;
    private float bobSpeed = 5f;

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

    private void Start()
    {
        PickupPlacement.EnsureClear(transform);
        Renderer box = visual != null ? visual.GetComponent<Renderer>() : null;
        if (box == null) box = GetComponentInChildren<Renderer>();
        if (box != null) WorldLabel.Attach(box, "F", Color.white);
    }

    // Keep the fuel pickup presentation updated while it remains in the world.
    private void Update()
    {
        if (visual == null)
        {
            return;
        }

        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;

        visual.localPosition = Vector3.up * bobOffset;
    }

}
