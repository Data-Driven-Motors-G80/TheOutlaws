using UnityEngine;

// A swept crossing test also catches fast cars that pass the portal in one frame.
public sealed class OutlawAreaPortal : MonoBehaviour
{
    private Transform player;
    private InfiniteRoad road;
    private OutlawGameManager game;
    private Vector3 previousPosition;
    private float halfWidth;
    private Material portalMaterial;

    public static OutlawAreaPortal Create(RoadPathPoint point, Transform player,
        InfiniteRoad road, OutlawGameManager game)
    {
        var root = new GameObject("Desert Portal");
        root.transform.SetPositionAndRotation(point.Position,
            Quaternion.LookRotation(point.Forward, point.Up));
        var portal = root.AddComponent<OutlawAreaPortal>();
        portal.player = player;
        portal.road = road;
        portal.game = game;
        portal.halfWidth = point.Width * 0.5f;
        portal.previousPosition = root.transform.InverseTransformPoint(player.position);

        // Use the project's existing surface shader so the portal works in builds too.
        Renderer surface = road.GetComponentInChildren<RoadSegment>()
            .transform.Find("Road").GetComponent<Renderer>();
        portal.portalMaterial = new Material(surface.sharedMaterial);
        portal.portalMaterial.color = new Color(0.65f, 0.15f, 1f);
        if (portal.portalMaterial.HasProperty("_EmissionColor"))
        {
            portal.portalMaterial.EnableKeyword("_EMISSION");
            portal.portalMaterial.SetColor("_EmissionColor", new Color(0.65f, 0.15f, 1f) * 2f);
        }
        portal.AddBar(new Vector3(-portal.halfWidth - 0.25f, 2.8f, 0f), new Vector3(0.5f, 5.6f, 0.5f));
        portal.AddBar(new Vector3(portal.halfWidth + 0.25f, 2.8f, 0f), new Vector3(0.5f, 5.6f, 0.5f));
        portal.AddBar(new Vector3(0f, 5.6f, 0f), new Vector3(point.Width + 1f, 0.5f, 0.5f));
        portal.AddBar(new Vector3(0f, 0.04f, 0f), new Vector3(point.Width, 0.06f, 0.5f));
        return portal;
    }

    private void AddBar(Vector3 position, Vector3 scale)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "Portal Frame";
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = position;
        bar.transform.localScale = scale;
        Collider collider = bar.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        bar.GetComponent<Renderer>().sharedMaterial = portalMaterial;
    }

    private void LateUpdate()
    {
        if (player == null || game == null || game.State != OutlawGameState.Running) return;
        Vector3 current = transform.InverseTransformPoint(player.position);
        if (previousPosition.z < 0f && current.z >= 0f)
        {
            float t = -previousPosition.z / (current.z - previousPosition.z);
            Vector3 crossing = Vector3.Lerp(previousPosition, current, t);
            if (Mathf.Abs(crossing.x) <= halfWidth && crossing.y >= -1f && crossing.y <= 5.6f)
            {
                road.SetSurfaceColor(new Color(0.82f, 0.61f, 0.32f));
                enabled = false;
                Destroy(gameObject, 3f);
            }
        }
        previousPosition = current;
    }

    private void OnDestroy()
    {
        if (portalMaterial != null) Destroy(portalMaterial);
    }
}
