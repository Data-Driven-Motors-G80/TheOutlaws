// Caches runtime colour materials to avoid repeated material creation.
using System.Collections.Generic;
using UnityEngine;

public static class RuntimeMaterials
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material template;
    private static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

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
    // Release cached material references between play sessions.
    private static void ResetStatics()
    {
        template = null;
        cache.Clear();
    }
}
