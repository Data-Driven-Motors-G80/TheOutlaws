using UnityEngine;

public class FuelTank : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fuelAmount = 50f;
    public float FuelAmount => fuelAmount;

    private float bobHeight = 0.12f;
    private float bobSpeed = 5f;
    private float flipSpeed = 90f;

    private Vector3 initialPosition;

    private void Awake()
    {
        // Set the initial position of the fuel tank
        initialPosition = transform.position;
    }
    private void Update()
    {
        // Bobbing motion
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = initialPosition + Vector3.up * bobOffset;

        // Rotating motion
        transform.Rotate(Vector3.up, flipSpeed * Time.deltaTime);
    }

}