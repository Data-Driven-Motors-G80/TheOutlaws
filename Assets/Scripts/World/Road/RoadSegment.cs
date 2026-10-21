using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoadSegment : MonoBehaviour
{
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    [SerializeField, Min(0.1f)] private float width = 10f;
    private Vector3[] curvePoints;
    private Vector3[] curveDirections;
    private RoadCurveMesh curveMesh;

    public float Width => width;
    public int PathPointCount => curvePoints != null ? curvePoints.Length : waypoints.Length + 2;
    public Pose ExitPose => new Pose(endPoint.position, endPoint.rotation);

    public float Length
    {
        get
        {
            if (curvePoints != null) return curveLength;
            float length = 0f;
            for (int i = 1; i < PathPointCount; i++)
                length += Vector3.Distance(GetPathPoint(i - 1), GetPathPoint(i));
            return length;
        }
    }
    private float curveLength;

    // A smooth change of heading produces a bend; a heading that returns to
    // zero produces an S-curve. Both meet neighbouring pieces tangentially.
    public void ConfigureCurve(float length, float angle, bool sCurve)
    {
        curveLength = Mathf.Max(1f, length);
        int steps = Mathf.CeilToInt(curveLength);
        curvePoints = new Vector3[steps + 1];
        curveDirections = new Vector3[steps + 1];
        float step = curveLength / steps;
        for (int i = 0; i <= steps; i++)
        {
            curveDirections[i] = Direction((float)i / steps, angle, sCurve);
            if (i > 0)
                curvePoints[i] = curvePoints[i - 1] + Direction((i - 0.5f) / steps, angle, sCurve) * step;
        }
        startPoint.SetPositionAndRotation(transform.position, transform.rotation);
        endPoint.SetPositionAndRotation(transform.TransformPoint(curvePoints[steps]),
            transform.rotation * Quaternion.LookRotation(curveDirections[steps]));
        if (curveMesh == null) curveMesh = gameObject.AddComponent<RoadCurveMesh>();
        curveMesh.Rebuild(this);
        if (TryGetComponent(out RoadMarking markings)) markings.Rebuild();
    }

    private static Vector3 Direction(float t, float angle, bool sCurve)
    {
        float blend = sCurve ? Mathf.Pow(Mathf.Sin(Mathf.PI * t), 2f) : t * t * (3f - 2f * t);
        return Quaternion.Euler(0f, angle * blend, 0f) * Vector3.forward;
    }

    public RoadPathPoint Sample(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, Length);
        if (curvePoints != null)
        {
            float index = distance / curveLength * (curvePoints.Length - 1);
            int from = Mathf.Min(Mathf.FloorToInt(index), curvePoints.Length - 2);
            float t = index - from;
            return new RoadPathPoint(transform.TransformPoint(Vector3.Lerp(curvePoints[from], curvePoints[from + 1], t)),
                transform.TransformDirection(Vector3.Slerp(curveDirections[from], curveDirections[from + 1], t)).normalized, width);
        }
        for (int i = 1; i < PathPointCount; i++)
        {
            Vector3 from = GetPathPoint(i - 1);
            Vector3 to = GetPathPoint(i);
            float length = Vector3.Distance(from, to);
            if (length <= Mathf.Epsilon) continue;
            if (distance <= length || i == PathPointCount - 1)
                return new RoadPathPoint(Vector3.Lerp(from, to, distance / length), (to - from) / length, width);
            distance -= length;
        }
        return new RoadPathPoint(startPoint.position, startPoint.forward, width);
    }

    public float ProjectDistance(Vector3 position, out float sqrDistance)
    {
        // Generated segments use unit scale. Project locally to avoid hundreds
        // of TransformPoint calls for every obstacle and pickup each frame.
        if (curvePoints != null) position = transform.InverseTransformPoint(position);
        float closest = 0f;
        float travelled = 0f;
        sqrDistance = float.MaxValue;
        for (int i = 1; i < PathPointCount; i++)
        {
            Vector3 from = curvePoints != null ? curvePoints[i - 1] : GetPathPoint(i - 1);
            Vector3 delta = (curvePoints != null ? curvePoints[i] : GetPathPoint(i)) - from;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon) continue;
            float t = Mathf.Clamp01(Vector3.Dot(position - from, delta) / delta.sqrMagnitude);
            float candidate = (position - from - delta * t).sqrMagnitude;
            if (candidate < sqrDistance)
            {
                sqrDistance = candidate;
                closest = travelled + length * t;
            }
            travelled += length;
        }
        return closest;
    }

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
        if (curvePoints != null) return transform.TransformPoint(curvePoints[index]);
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
