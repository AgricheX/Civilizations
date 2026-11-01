using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal static class NetPhoto
    {
        private const int W = 64;
        private const int H = 36;
        private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

        public static bool Has(NetPost p)
        {
            return p != null && !string.IsNullOrEmpty(p.Snap);
        }

        public static Sprite Get(NetPost p)
        {
            if (!Has(p)) return null;
            Sprite s;
            if (_cache.TryGetValue(p.Id, out s) && s != null) return s;
            int i = p.Snap.IndexOf(',');
            int cx, cy;
            if (i <= 0 || !int.TryParse(p.Snap.Substring(0, i), out cx) || !int.TryParse(p.Snap.Substring(i + 1), out cy)) return null;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[W * H];
            int x0 = cx - W / 2, y0 = cy - H / 2;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    WorldTile t = Globe.Tile(x0 + x, y0 + y);
                    Color32 c = new Color32(20, 24, 30, 255);
                    if (t != null && t.Type != null)
                    {
                        c = t.getColor();
                        c.a = 255;
                        if (t.building != null && t.building.isAlive())
                        {
                            Kingdom k = t.building.kingdom;
                            Color32 kc = k != null && k.getColor() != null ? k.getColor().getColorMain32() : new Color32(180, 160, 140, 255);
                            c = Color32.Lerp(new Color32(70, 60, 55, 255), kc, (x + y) % 3 == 0 ? 0.8f : 0.45f);
                            c.a = 255;
                        }
                        else if (t.hasUnits())
                        {
                            c = new Color32(245, 235, 220, 255);
                        }
                    }
                    px[y * W + x] = c;
                }
            }
            for (int x = 0; x < W; x++)
            {
                px[x] = Darken(px[x]);
                px[(H - 1) * W + x] = Darken(px[(H - 1) * W + x]);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
            if (_cache.Count > 80)
            {
                foreach (var kv in _cache)
                {
                    if (kv.Value != null)
                    {
                        Object.Destroy(kv.Value.texture);
                        Object.Destroy(kv.Value);
                    }
                }
                _cache.Clear();
            }
            _cache[p.Id] = s;
            return s;
        }

        private static Color32 Darken(Color32 c)
        {
            return new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f), 255);
        }

        public static void Clear()
        {
            foreach (var kv in _cache)
            {
                if (kv.Value != null)
                {
                    Object.Destroy(kv.Value.texture);
                    Object.Destroy(kv.Value);
                }
            }
            _cache.Clear();
        }
    }
}
