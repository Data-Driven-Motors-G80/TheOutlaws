using TMPro;
using UnityEngine;

/// <summary>
/// A bold letter or number printed flat on the front face of a box (pickups and obstacles).
/// It lives outside the box's hierarchy so non-uniform scale never stretches the text,
/// and follows the box's renderer so it bobs with it.
/// </summary>
public sealed class WorldLabel : MonoBehaviour
{
    private const float FaceFill = 0.75f;
    private const float SurfaceOffset = 0.03f;

    private Renderer target;
    private TextMeshPro text;
    private Renderer background;
    /// <summary>When set, the face this label sits on never changes (obstacles).</summary>
    private Transform fixedFace;
    private bool hidden;

    /// <summary>Largest letter height in metres (big for the portal gate).</summary>
    public float MaximumSize { get; set; } = 1.6f;

    /// <summary>Prints <paramref name="value"/> on the side of <paramref name="box"/> that faces the camera.</summary>
    public static WorldLabel Attach(Renderer box, string value, Color color)
    {
        WorldLabel label = Create(value, color);
        label.target = box;
        label.LateUpdate();
        return label;
    }

    /// <summary>Prints on the face of <paramref name="box"/> pointing against <paramref name="face"/>'s forward.</summary>
    public static WorldLabel AttachToFrontFace(Renderer box, Transform face, string value, Color color)
    {
        WorldLabel label = Create(value, color);
        label.target = box;
        label.fixedFace = face;
        label.LateUpdate();
        return label;
    }

    private static WorldLabel Create(string value, Color color)
    {
        var labelObject = new GameObject("World Label " + value);
        WorldLabel label = labelObject.AddComponent<WorldLabel>();
        label.text = labelObject.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) label.text.font = TMP_Settings.defaultFontAsset;
        label.text.text = value;
        label.text.alignment = TextAlignmentOptions.Center;
        label.text.fontStyle = FontStyles.Bold;
        label.text.textWrappingMode = TextWrappingModes.NoWrap;
        label.text.rectTransform.sizeDelta = new Vector2(4f, 2f);
        label.text.color = color;
        label.text.outlineColor = new Color32(0, 0, 0, 255);
        label.text.outlineWidth = 0.15f;
        return label;
    }

    public void SetText(string value)
    {
        if (text != null && text.text != value) text.text = value;
    }

    public void SetBackground(Color colour)
    {
        if (background == null)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Label Background";
            panel.layer = target.gameObject.layer;
            panel.transform.SetParent(transform, false);
            // The label faces into the box, so positive local Z sits just behind the text.
            panel.transform.localPosition = Vector3.forward * 0.01f;
            Collider collider = panel.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            background = panel.GetComponent<Renderer>();
            background.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        background.sharedMaterial = RuntimeMaterials.Coloured(colour);
        LateUpdate();
    }

    public void SetVisible(bool visible)
    {
        hidden = !visible;
        if (text != null) text.enabled = visible && target != null && target.gameObject.activeInHierarchy;
        if (background != null) background.enabled = text != null && text.enabled;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        bool visible = !hidden && target.enabled && target.gameObject.activeInHierarchy;
        if (text.enabled != visible) text.enabled = visible;
        if (background != null) background.enabled = visible;
        if (!visible) return;

        // Work in the box's own axes so the label sits exactly on a real face.
        Transform box = target.transform;
        Bounds local = target.localBounds;
        Vector3 scale = box.lossyScale;
        Vector3 centre = box.TransformPoint(local.center);
        Vector3 half = Vector3.Scale(local.extents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        Vector3 up = box.up;

        Vector3 wanted;
        if (fixedFace != null)
        {
            wanted = -fixedFace.forward;
        }
        else
        {
            Camera view = Camera.main;
            wanted = view != null ? view.transform.position - centre : -box.forward;
        }

        // Pick the side face (local +-X or +-Z) that points most towards "wanted".
        Vector3 outward = box.right;
        float depth = half.x, faceWidth = half.z * 2f;
        float best = Vector3.Dot(box.right, wanted);
        if (-best > best) { outward = -box.right; best = -best; }
        float forwardDot = Vector3.Dot(box.forward, wanted);
        if (Mathf.Abs(forwardDot) > best)
        {
            outward = forwardDot > 0f ? box.forward : -box.forward;
            depth = half.z;
            faceWidth = half.x * 2f;
        }
        float faceHeight = half.y * 2f;

        // TextMeshPro cap height is roughly a tenth of its font size, in metres.
        float letters = Mathf.Max(1, text.text.Length);
        float size = Mathf.Min(faceHeight * FaceFill, faceWidth * FaceFill / (0.7f * letters));
        size = Mathf.Clamp(size, 0.2f, MaximumSize);
        text.fontSize = size * 10f;
        if (background != null)
            background.transform.localScale = new Vector3(size * (0.7f * letters + 0.3f), size * 1.15f, 1f);

        transform.position = centre + outward * (depth + SurfaceOffset);
        // TextMeshPro reads correctly when its forward points into the face.
        transform.rotation = Quaternion.LookRotation(-outward, up);
    }
}
