using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coloured materials for objects created in code (A boxes, bullets, portal gates).
/// Primitives' default material uses a shader that is stripped from WebGL builds (shows magenta),
/// so they copy a real URP Lit material from Resources, which is always included in the build.
/// </summary>
public static class RuntimeMaterials
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material template;
    private static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

    /// <summary>A shared material per colour (bullets are fired constantly, so they reuse it).</summary>
    public static Material Coloured(Color colour)
    {
        if (cache.TryGetValue(colour, out Material cached) && cached != null) return cached;
        if (template == null) template = Resources.Load<Material>("WeeWooRuntimeLit");
        Material material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor(BaseColorId, colour);
        material.SetColor(ColorId, colour);
        cache[colour] = material;
        return material;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        template = null;
        cache.Clear();
    }
}
