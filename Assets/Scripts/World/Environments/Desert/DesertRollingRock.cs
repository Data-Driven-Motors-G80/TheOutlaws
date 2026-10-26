using UnityEngine;

/// <summary>A one-shot crossing hazard. The controller owns its reserved road gap.</summary>
public sealed class DesertRollingRock : MonoBehaviour
{
    private TrafficLightObstacle obstacle;
    private SphereCollider hitbox;
    private Transform visual;
    private RoadPathPoint point;
    private float lateral, radius;
    public bool Resolved { get; private set; }
    public float Lateral => lateral;

    public static DesertRollingRock Create(Transform parent, RoadPathPoint point, float lateral, float radius)
    {
        var root = new GameObject("Crossing Rock (1 shot)");
        root.transform.SetParent(parent, false);
        root.transform.rotation = Quaternion.LookRotation(point.Forward, point.Up);
        var rock = root.AddComponent<DesertRollingRock>();
        rock.point = point;
        rock.lateral = lateral;
        rock.radius = radius;
        rock.hitbox = root.AddComponent<SphereCollider>();
        rock.hitbox.radius = radius;
        rock.hitbox.isTrigger = true;
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Rolling prototype visual";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * radius * 2f;
        var redundant = sphere.GetComponent<Collider>();
        redundant.enabled = false;
        Destroy(redundant);
        sphere.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(new Color(1f, 0.42f, 0f));
        rock.visual = sphere.transform;
        rock.obstacle = root.AddComponent<TrafficLightObstacle>();
        rock.obstacle.Initialize(TrafficObstacleState.Green); // Existing orange cost; no fuel damage.
        rock.Place();
        return rock;
    }

    public void Advance(float lateralDelta)
    {
        if (obstacle.WasHit) { hitbox.enabled = false; Resolved = true; }
        lateral += lateralDelta;
        visual.Rotate(Vector3.forward, -lateralDelta / radius * Mathf.Rad2Deg, Space.Self);
        Place();
    }

    private void Place() => transform.position = point.Position + point.Right * lateral + point.Up * radius;

    public bool TakeShot()
    {
        if (Resolved || !gameObject.activeInHierarchy) return false;
        if (obstacle.ApplyShot())
        {
            Resolved = true;
            hitbox.enabled = false;
            gameObject.SetActive(false);
        }
        return true;
    }

    public void ReportPassed()
    {
        if (!Resolved && !obstacle.WasHit) GameEvents.RaiseObstacleDodged(obstacle.CurrentState);
        Resolved = true;
    }
}
