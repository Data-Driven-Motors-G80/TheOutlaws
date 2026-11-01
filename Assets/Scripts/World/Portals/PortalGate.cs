// Builds a labelled portal aligned with the road.

using UnityEngine;

public sealed class PortalGate : MonoBehaviour
{
    public static PortalGate Create(RoadPathPoint point, Color colour, string label)
    {
        GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = "Portal Gate";
        Destroy(gate.GetComponent<Collider>());

        const float height = 18f;
        gate.transform.SetPositionAndRotation(point.Position + point.Up * (height * 0.5f - 0.5f),
            Quaternion.LookRotation(point.Forward, point.Up));
        gate.transform.localScale = new Vector3(point.Width + 12f, height, 0.6f);

        Renderer renderer = gate.GetComponent<Renderer>();
        renderer.sharedMaterial = RuntimeMaterials.Coloured(colour);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        WorldLabel text = WorldLabel.AttachToFrontFace(renderer, gate.transform, label, Color.white);
        text.MaximumSize = 4f;
        return gate.AddComponent<PortalGate>();
    }
}
