using UnityEngine;

/// <summary>Only the visual bobs; the generous collection trigger stays steady.</summary>
public sealed class MysteryPickupVisual : MonoBehaviour
{
    private Vector3 restingPosition;
    private float phase;
    private Renderer box;
    private TMPro.TMP_Text label;
    private MaterialPropertyBlock properties;
    private Color baseColor;
    private bool fading;
    private float fadeTime;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public void BeginFade()
    {
        if (fading) return;
        fading = true;
        fadeTime = 0f;
        properties = new MaterialPropertyBlock();
    }
    public void Configure(Renderer visual, float width)
    {
        var mesh = visual.GetComponent<MeshFilter>();
        float localWidth = mesh != null ? mesh.sharedMesh.bounds.size.x : 1f;
        Vector3 scale = transform.localScale;
        scale.x = width / Mathf.Max(.01f, localWidth * transform.parent.lossyScale.x);
        transform.localScale = scale;
        transform.localPosition += Vector3.up * .7f;
        restingPosition = transform.localPosition;
        phase = transform.position.x * .2f;
        visual.sharedMaterial = Resources.Load<Material>("MysteryPickupTransparent");
        visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        box = visual;
        baseColor = visual.sharedMaterial.color;
        label = WorldLabel.Attach(visual, "?", new Color(1f, .9f, .3f)).GetComponent<TMPro.TMP_Text>();
    }
    private void Update()
    {
        transform.localPosition = restingPosition + Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * .12f);
        if (!fading) return;
        fadeTime += Time.deltaTime;
        // Let the car pass first, then gently dissolve the box and its question mark.
        float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fadeTime - 0.25f) / 0.75f));
        Color color = baseColor;
        color.a *= opacity;
        box.GetPropertyBlock(properties);
        properties.SetColor(BaseColorId, color);
        box.SetPropertyBlock(properties);
        if (label != null) label.alpha = opacity;
        if (fadeTime >= 1f) Destroy(transform.parent.gameObject);
    }
}
