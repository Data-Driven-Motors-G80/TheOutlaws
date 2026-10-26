using UnityEngine;

/// <summary>Original antialiased icon textures, prepared once during HUD setup.</summary>
public static class BackpackIcon
{
    private static Texture2D[] icons;
    public static void Preload()
    {
        if (icons != null && icons[0] != null) return;
        icons = new[]
        {
            Resources.Load<Texture2D>("BackpackIcons/empty"),
            Resources.Load<Texture2D>("BackpackIcons/reverse"),
            Resources.Load<Texture2D>("BackpackIcons/jammer"),
            Resources.Load<Texture2D>("BackpackIcons/shield"),
            Resources.Load<Texture2D>("BackpackIcons/nitro"),
            Resources.Load<Texture2D>("BackpackIcons/sun"),
            Resources.Load<Texture2D>("BackpackIcons/gun")
        };
    }
    public static void Draw(Rect area, RandomPickupOutcome item)
    {
        Preload();
        int index = (int)item;
        var icon = icons[index >= 0 && index < icons.Length ? index : 0];
        if (icon == null) return;
        Color previous = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(area, icon, ScaleMode.ScaleToFit, true);
        GUI.color = previous;
    }
}
