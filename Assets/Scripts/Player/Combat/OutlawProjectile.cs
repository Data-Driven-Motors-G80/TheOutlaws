using UnityEngine;

/// <summary>Shot constants and the projectile visual used by the WorldShift escape's shooting.</summary>
public static class OutlawProjectile
{
    // Effectively unlimited range: the shot flies until it hits a block or leaves the visible road.
    public const float Speed = 70f;
    public const float MaximumRange = 200f;

    public static GameObject CreateVisual(Vector3 position, bool targetsPolice = false)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = targetsPolice ? "Rear Shot" : "Forward Shot";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * 0.28f;
        projectile.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(targetsPolice
            ? new Color(0.2f, 0.75f, 1f)
            : new Color(1f, 0.75f, 0.05f));
        return projectile;
    }
}
