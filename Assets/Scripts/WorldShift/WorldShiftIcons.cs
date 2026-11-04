using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public enum ShiftIcon { Sun, Moon, Lock, Unlocked, Check, Play, Pause, Home, Retry, Next, Circle, Flag }

    /// <summary>Small procedural UI icons so menus and HUD need no words.</summary>
    public static class WorldShiftIcons
    {
        private const int Size = 128;
        private static readonly Dictionary<ShiftIcon, Sprite> cache = new Dictionary<ShiftIcon, Sprite>();

        public static Sprite Get(ShiftIcon icon)
        {
            if (cache.TryGetValue(icon, out Sprite sprite) && sprite != null) return sprite;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = icon + " icon", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // Coordinates run -1..1 with y up; shapes return signed distance (negative inside).
                    var p = new Vector2((x + .5f) / Size * 2f - 1f, (y + .5f) / Size * 2f - 1f);
                    float d = Shape(icon, p);
                    float alpha = Mathf.Clamp01(.5f - d * Size * .5f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size);
            cache[icon] = sprite;
            return sprite;
        }

        private static float Shape(ShiftIcon icon, Vector2 p)
        {
            switch (icon)
            {
                case ShiftIcon.Sun:
                {
                    float d = Circle(p, Vector2.zero, .42f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        d = Mathf.Min(d, Segment(p, dir * .6f, dir * .85f) - .07f);
                    }
                    return d;
                }
                case ShiftIcon.Moon: return Mathf.Max(Circle(p, Vector2.zero, .78f), -Circle(p, new Vector2(.38f, .25f), .62f));
                case ShiftIcon.Lock:
                case ShiftIcon.Unlocked:
                {
                    float body = Box(p, new Vector2(0f, -.28f), new Vector2(.58f, .44f));
                    Vector2 shackleCentre = icon == ShiftIcon.Lock ? new Vector2(0f, .2f) : new Vector2(.42f, .32f);
                    float ring = Mathf.Abs(Circle(p, shackleCentre, .34f)) - .09f;
                    ring = Mathf.Max(ring, shackleCentre.y - p.y);
                    float legs = Mathf.Min(Box(p, shackleCentre + new Vector2(-.34f, -.2f), new Vector2(.09f, .22f)),
                        Box(p, shackleCentre + new Vector2(.34f, -.2f), new Vector2(.09f, .22f)));
                    return Mathf.Min(body, Mathf.Min(ring, legs));
                }
                case ShiftIcon.Check: return Mathf.Min(Segment(p, new Vector2(-.62f, .02f), new Vector2(-.2f, -.45f)),
                    Segment(p, new Vector2(-.2f, -.45f), new Vector2(.66f, .52f))) - .13f;
                case ShiftIcon.Play: return Triangle(p, new Vector2(-.45f, .62f), new Vector2(-.45f, -.62f), new Vector2(.65f, 0f));
                case ShiftIcon.Pause: return Mathf.Min(Box(p, new Vector2(-.28f, 0f), new Vector2(.16f, .6f)), Box(p, new Vector2(.28f, 0f), new Vector2(.16f, .6f)));
                case ShiftIcon.Home: return Mathf.Min(Triangle(p, new Vector2(-.78f, .05f), new Vector2(.78f, .05f), new Vector2(0f, .75f)),
                    Mathf.Max(Box(p, new Vector2(0f, -.35f), new Vector2(.52f, .42f)), -Box(p, new Vector2(0f, -.5f), new Vector2(.14f, .28f))));
                case ShiftIcon.Retry:
                {
                    float ring = Mathf.Abs(Circle(p, Vector2.zero, .55f)) - .11f;
                    ring = Mathf.Max(ring, -Box(p, new Vector2(.35f, .45f), new Vector2(.3f, .3f)));
                    return Mathf.Min(ring, Triangle(p, new Vector2(.1f, .82f), new Vector2(.1f, .28f), new Vector2(.5f, .55f)));
                }
                case ShiftIcon.Next: return Mathf.Min(Triangle(p, new Vector2(-.75f, .55f), new Vector2(-.75f, -.55f), new Vector2(.05f, 0f)),
                    Triangle(p, new Vector2(0f, .55f), new Vector2(0f, -.55f), new Vector2(.8f, 0f)));
                case ShiftIcon.Flag: return Mathf.Min(Box(p, new Vector2(-.55f, 0f), new Vector2(.07f, .8f)),
                    Box(p, new Vector2(.05f, .38f), new Vector2(.55f, .38f)));
                default: return Circle(p, Vector2.zero, .9f);
            }
        }

        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        private static float Box(Vector2 p, Vector2 c, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }
        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }
        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Edge planes oriented so the centroid is inside (negative) for either winding.
            Vector2 centre = (a + b + c) / 3f;
            float Edge(Vector2 u, Vector2 v)
            {
                Vector2 n = new Vector2(v.y - u.y, u.x - v.x).normalized;
                if (Vector2.Dot(centre - u, n) > 0f) n = -n;
                return Vector2.Dot(p - u, n);
            }
            return Mathf.Max(Edge(a, b), Mathf.Max(Edge(b, c), Edge(c, a)));
        }
    }
}
