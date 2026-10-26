using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CurvedRoadTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void Cleanup()
    {
        foreach (GameObject item in objects)
            if (item != null) Object.DestroyImmediate(item);
        objects.Clear();
    }

    [TestCase(25f, false)]
    [TestCase(-25f, false)]
    [TestCase(25f, true)]
    [TestCase(-25f, true)]
    public void CurvesHaveConsistentDistanceSmoothHeadingAndExpectedExit(float angle, bool sCurve)
    {
        RoadSegment segment = MakeSegment();
        segment.ConfigureCurve(80f, angle, sCurve);
        Assert.That(segment.Length, Is.EqualTo(80f).Within(0.001f));
        Assert.That(Vector3.Angle(segment.Sample(0f).Forward, Vector3.forward), Is.LessThan(0.01f));
        Vector3 exit = Quaternion.Euler(0f, sCurve ? 0f : angle, 0f) * Vector3.forward;
        Assert.That(Vector3.Angle(segment.Sample(80f).Forward, exit), Is.LessThan(0.05f));
        for (int i = 1; i <= 80; i++)
        {
            RoadPathPoint before = segment.Sample(i - 1);
            RoadPathPoint after = segment.Sample(i);
            Assert.That(Vector3.Distance(before.Position, after.Position), Is.EqualTo(1f).Within(0.002f));
            Assert.That(Vector3.Angle(before.Forward, after.Forward), Is.LessThan(1.1f));
            Assert.That(after.Forward.z, Is.GreaterThan(0.9f));
        }
        Assert.That(Mathf.Sign(segment.Sample(80f).Position.x), Is.EqualTo(Mathf.Sign(angle)));
    }

    [Test]
    public void CurvedPiecesJoinAtBothRoadEdgesAndCanBeReusedAsStraight()
    {
        RoadSegment first = MakeSegment();
        RoadSegment second = MakeSegment();
        first.ConfigureCurve(80f, 25f, false);
        second.ConfigureCurve(80f, -25f, true);
        first.AlignStartTo(new Pose(new Vector3(13f, 0f, 21f), Quaternion.Euler(0f, -10f, 0f)));
        second.AlignStartTo(first.ExitPose);
        AssertJoin(first, second);
        second.ConfigureCurve(20f, 0f, false);
        second.AlignStartTo(first.ExitPose);
        AssertJoin(first, second);
        Assert.That(second.Length, Is.EqualTo(20f));
    }

    [Test]
    public void SurfaceWallsAndCollidersFollowCurveWithoutMutatingSharedMeshes()
    {
        RoadSegment segment = MakeSegment();
        var sourceMesh = segment.transform.Find("Road").GetComponent<MeshFilter>().sharedMesh;
        segment.ConfigureCurve(80f, 25f, false);
        Transform surface = segment.transform.Find("Road");
        Mesh mesh = surface.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh, Is.Not.SameAs(sourceMesh));
        Assert.That(surface.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(mesh));
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i <= 80; i++)
        {
            RoadPathPoint point = segment.Sample(i);
            Vector3 left = surface.TransformPoint(vertices[i * 2]);
            Vector3 right = surface.TransformPoint(vertices[i * 2 + 1]);
            Assert.That(Vector3.Distance((left + right) * 0.5f, point.Position), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(left, right), Is.EqualTo(10f).Within(0.001f));
            Assert.That(mesh.normals[i * 2].y, Is.GreaterThan(0.99f));
        }
        Transform wall = segment.transform.Find("Left Roadside Wall");
        Assert.That(wall.GetComponent<BoxCollider>().enabled, Is.False);
        Assert.That(wall.GetComponent<MeshCollider>().sharedMesh.vertexCount, Is.EqualTo(81 * 4));
        segment.ConfigureCurve(20f, 0f, false);
        Assert.That(surface.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
        Assert.That(mesh.vertexCount, Is.EqualTo(42));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StreamingKeepsAheadAndBehindCoverageOverLongRun(bool hairpins)
    {
        RoadSegment template = MakeSegment();
        GameObject car = MakeObject("Test Car");
        GameObject holder = MakeObject("Test Road");
        holder.SetActive(false);
        InfiniteRoad road = holder.AddComponent<InfiniteRoad>();
        Set(road, "car", car.transform);
        Set(road, "segmentPrefabs", new[] { template });
        Set(road, "curveFrequency", 1f);
        Set(road, "hairpinFrequency", hairpins ? 0.15f : 0f);
        Set(road, "behindBuffer", 40f);
        Set(road, "straightIntroDistance", 160f);
        Assert.That(road.TryGetDistance(car.transform.position, out float start), Is.True);
        Assert.That(start, Is.EqualTo(40f).Within(0.001f));
        float previousZ = car.transform.position.z;
        for (float distance = start; distance < 5000f; distance += 5f)
        {
            Assert.That(road.TrySample(distance, out RoadPathPoint point), Is.True);
            car.transform.position = point.Position + point.Right * 3f;
            typeof(InfiniteRoad).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(road, null);
            Assert.That(road.TryGetPointAhead(point.Position, 55f, out RoadPathPoint ahead), Is.True);
            Assert.That(road.TryGetPointAhead(point.Position, -15f, out RoadPathPoint behind), Is.True);
            Assert.That(road.IsBehind(behind.Position, point.Position, 10f), Is.True);
            Assert.That(road.IsBehind(ahead.Position, point.Position, 10f), Is.False);
            Assert.That(road.LastDistance - distance, Is.GreaterThanOrEqualTo(179.9f));
            Assert.That(point.Forward.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
            if (!hairpins)
            {
                Assert.That(point.Forward.z, Is.GreaterThan(0.8f));
                Assert.That(point.Position.z, Is.GreaterThanOrEqualTo(previousZ - 0.001f));
            }
            previousZ = point.Position.z;
        }
        Assert.That(road.TrySample(road.LastDistance + 1f, out _), Is.False);
        Assert.That(road.TrySample(road.FirstDistance - 1f, out _), Is.False);
        Assert.That(holder.GetComponentsInChildren<RoadSegment>(true).Length, Is.LessThan(20));
    }

    private static void AssertJoin(RoadSegment first, RoadSegment second)
    {
        RoadPathPoint a = first.Sample(first.Length);
        RoadPathPoint b = second.Sample(0f);
        Assert.That(Vector3.Distance(a.Position, b.Position), Is.LessThan(0.001f));
        Assert.That(Vector3.Angle(a.Forward, b.Forward), Is.LessThan(0.05f));
        foreach (float side in new[] { -5f, 5f })
            Assert.That(Vector3.Distance(a.Position + a.Right * side, b.Position + b.Right * side), Is.LessThan(0.001f));
    }

    private RoadSegment MakeSegment()
    {
        GameObject root = MakeObject("Road Template");
        Transform start = new GameObject("StartPoint").transform;
        start.SetParent(root.transform, false);
        Transform end = new GameObject("EndPoint").transform;
        end.SetParent(root.transform, false);
        end.localPosition = Vector3.forward * 20f;
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.name = "Road";
        surface.transform.SetParent(root.transform, false);
        surface.transform.localPosition = Vector3.forward * 10f;
        surface.transform.localScale = new Vector3(1f, 1f, 2f);
        foreach (string name in new[] { "Left Roadside Wall", "Right Roadside Wall" })
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(root.transform, false);
        }
        RoadSegment segment = root.AddComponent<RoadSegment>();
        Set(segment, "startPoint", start);
        Set(segment, "endPoint", end);
        return segment;
    }

    private GameObject MakeObject(string name)
    {
        var result = new GameObject(name);
        objects.Add(result);
        return result;
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
