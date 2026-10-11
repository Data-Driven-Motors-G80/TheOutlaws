using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoadSegment : MonoBehaviour
{
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    [SerializeField, Min(0.1f)] private float width = 10f;

    public float Width => width;
    public int PathPointCount => waypoints.Length + 2;
    public Pose ExitPose => new Pose(endPoint.position, endPoint.rotation);

    public bool IsConfigured
    {
        get
        {
            if (startPoint == null || endPoint == null)
            {
                return false;
            }

            foreach (Transform waypoint in waypoints)
            {
                if (waypoint == null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public Vector3 GetPathPoint(int index)
    {
        if (index == 0)
        {
            return startPoint.position;
        }

        if (index == waypoints.Length + 1)
        {
            return endPoint.position;
        }

        return waypoints[index - 1].position;
    }

    public float DistancePastEnd(Vector3 worldPosition)
    {
        return Vector3.Dot(worldPosition - endPoint.position, endPoint.forward);
    }

    public void AlignStartTo(Pose entry)
    {
        Quaternion correction = entry.rotation * Quaternion.Inverse(startPoint.rotation);
        transform.rotation = correction * transform.rotation;
        transform.position += entry.position - startPoint.position;
    }

    private void Reset()
    {
        startPoint = FindChild("StartPoint");
        endPoint = FindChild("EndPoint");

        Transform road = FindChild("Road");

        if (road != null && road.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
        {
            width = meshFilter.sharedMesh.bounds.size.x * road.lossyScale.x;
        }
    }

    private Transform FindChild(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }
}