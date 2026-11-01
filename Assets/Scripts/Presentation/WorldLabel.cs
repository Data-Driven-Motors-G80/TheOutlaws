// Creates readable labels attached to world-space objects.
using TMPro;
using UnityEngine;

public sealed class WorldLabel : MonoBehaviour
{
    private const float FaceFill = 0.75f;
    private const float SurfaceOffset = 0.03f;

    private Renderer target;
    private TextMeshPro text;
    private Renderer background;

    private Transform fixedFace;
    private bool hidden;

    public float MaximumSize { get; set; } = 1.6f;

    public static WorldLabel Attach(Renderer box, string value, Color color)
    {
        WorldLabel label = Create(value, color);
        label.target = box;
        label.LateUpdate();
        return label;
    }

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

    // Refresh label placement after its target has moved.
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

        float letters = Mathf.Max(1, text.text.Length);
        float size = Mathf.Min(faceHeight * FaceFill, faceWidth * FaceFill / (0.7f * letters));
        size = Mathf.Clamp(size, 0.2f, MaximumSize);
        text.fontSize = size * 10f;
        if (background != null)
            background.transform.localScale = new Vector3(size * (0.7f * letters + 0.3f), size * 1.15f, 1f);

        transform.position = centre + outward * (depth + SurfaceOffset);

        transform.rotation = Quaternion.LookRotation(-outward, up);
    }
}
