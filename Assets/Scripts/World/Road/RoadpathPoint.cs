using UnityEngine;

public readonly struct RoadPathPoint
{
    public RoadPathPoint(Vector3 position, Vector3 forward, float width)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Position = position;
        Forward = forward;
        Right = right;
        Up = Vector3.Cross(forward, right);
        Width = width;
    }

    public Vector3 Position { get; }
    public Vector3 Forward { get; }
    public Vector3 Right { get; }
    public Vector3 Up { get; }
    public float Width { get; }
}