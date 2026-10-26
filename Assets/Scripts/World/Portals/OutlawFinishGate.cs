using UnityEngine;

public static class OutlawFinishGate
{
    public static GameObject Create(RoadPathPoint point)
    {
        GameObject root = new GameObject("Extraction Finish Line");
        root.transform.position = point.Position;
        root.transform.rotation = Quaternion.LookRotation(point.Forward, point.Up);

        float halfWidth = Mathf.Max(3f, point.Width * 0.5f - 0.5f);
        CreateBar(root.transform, new Vector3(-halfWidth, 2.2f, 0f), new Vector3(0.35f, 4.4f, 0.35f), Color.white);
        CreateBar(root.transform, new Vector3(halfWidth, 2.2f, 0f), new Vector3(0.35f, 4.4f, 0.35f), Color.white);
        CreateBar(root.transform, new Vector3(0f, 4.2f, 0f), new Vector3(halfWidth * 2f, 0.45f, 0.45f), new Color(0.9f, 0.1f, 0.1f));
        return root;
    }

    private static void CreateBar(Transform parent, Vector3 localPosition, Vector3 scale, Color color)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "Finish Gate Bar";
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = scale;
        Object.Destroy(bar.GetComponent<Collider>());
        bar.GetComponent<Renderer>().material.color = color;
    }
}
