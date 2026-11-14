using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class FlagPart
    {
        public string Id = "";
        public int Index;
        public readonly HashSet<string> Tags = new HashSet<string>();
        public readonly List<int> Groups = new List<int>();
        public bool Horizontal;
        public int Ax, Ay;
    }

    internal class FlagPalette
    {
        public string Id = "";
        public Color32 C1, C2, Metal;
        public readonly HashSet<string> Tags = new HashSet<string>();
    }

    internal class FlagSheet
    {
        public Color32[] Px;
        public int W, H, Cell, Cols;

        public Color32 Get(int index, int x, int y)
        {
            int cx = (index % Cols) * Cell + x;
            int cy = (index / Cols) * Cell + y;
            if (Px == null || cx < 0 || cy < 0 || cx >= W || cy >= H) return new Color32(0, 0, 0, 0);
            return Px[cy * W + cx];
        }
    }

    internal static class FlagArt
    {
        public const int N = 64;
        public static readonly List<FlagPart> Emblems = new List<FlagPart>();
        public static readonly List<FlagPart> Charges = new List<FlagPart>();
        public static readonly List<FlagPart> Shapes = new List<FlagPart>();
        public static readonly List<FlagPalette> Palettes = new List<FlagPalette>();
        public static readonly Dictionary<string, FlagPart> EmblemById = new Dictionary<string, FlagPart>();
        public static readonly Dictionary<string, FlagPart> ChargeById = new Dictionary<string, FlagPart>();
        public static readonly Dictionary<string, FlagPart> ShapeById = new Dictionary<string, FlagPart>();
        public static readonly Dictionary<string, FlagPalette> PaletteById = new Dictionary<string, FlagPalette>();

        public static readonly string[] Divisions =
        {
            "plain", "fess2", "pale2", "tri_h", "tri_v", "stripes5", "stripes7", "vstripes5", "canton", "stripes_canton",
            "cross", "nordic", "saltire", "triangle_hoist", "tri_hoist_bicolor", "quarterly", "border", "bend", "per_bend", "per_bend_sinister",
            "disc", "pale", "fess", "per_saltire", "checky", "chief", "base", "pile", "pall", "lozenge",
            "wavy", "gyronny", "cross_border", "fess_border", "hoist_band", "hoist_tri", "rising_sun", "diag_stripes", "chevron", "bars3"
        };

        private static FlagSheet _emb, _chg, _shp;
        private static bool _loaded;
        private static readonly Dictionary<int, int[]> _bbox = new Dictionary<int, int[]>();
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static readonly LinkedList<string> _order = new LinkedList<string>();
        private const int Cap = 120;
        private static Sprite _blank;

        public static bool Ready
        {
            get
            {
                Load();
                return _emb != null && _shp != null && Emblems.Count > 0 && Shapes.Count > 0;
            }
        }

        public static Sprite Blank
        {
            get
            {
                if (_blank != null) return _blank;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                tex.SetPixels32(px);
                tex.Apply(false);
                _blank = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
                return _blank;
            }
        }

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                string dir = Path.Combine(Path.Combine(CivMod.Folder, "GameResources"), "flags");
                JObject o = JObject.Parse(File.ReadAllText(Path.Combine(Path.Combine(CivMod.Folder, "Data"), "flags.json")));
                JObject cells = (JObject)o["cells"];
                ReadParts((JArray)o["emblems"], Emblems, EmblemById);
                ReadParts((JArray)o["charges"], Charges, ChargeById);
                ReadParts((JArray)o["shapes"], Shapes, ShapeById);
                foreach (JToken t in (JArray)o["palettes"])
                {
                    var p = new FlagPalette { Id = (string)t["id"], C1 = Hex((string)t["c1"]), C2 = Hex((string)t["c2"]), Metal = Hex((string)t["metal"]) };
                    foreach (JToken g in (JArray)t["tags"]) p.Tags.Add((string)g);
                    Palettes.Add(p);
                    PaletteById[p.Id] = p;
                }
                _emb = Sheet(Path.Combine(dir, "emblems.png"), (int)cells["emblem"], (int)cells["emblem_cols"]);
                _chg = Sheet(Path.Combine(dir, "charges.png"), (int)cells["charge"], (int)cells["charge_cols"]);
                _shp = Sheet(Path.Combine(dir, "shapes.png"), (int)cells["shape"], (int)cells["shape_cols"]);
            }
            catch (Exception e)
            {
                CivMod.Report("flags-load", e);
            }
        }

        private static void ReadParts(JArray arr, List<FlagPart> into, Dictionary<string, FlagPart> by)
        {
            if (arr == null) return;
            foreach (JToken t in arr)
            {
                var p = new FlagPart { Id = (string)t["id"], Index = into.Count };
                JArray tags = t["tags"] as JArray;
                if (tags != null) foreach (JToken g in tags) p.Tags.Add((string)g);
                JArray groups = t["groups"] as JArray;
                if (groups != null) foreach (JToken g in groups) p.Groups.Add((int)g);
                if (t["h"] != null) p.Horizontal = (bool)t["h"];
                if (t["ax"] != null) p.Ax = (int)t["ax"];
                if (t["ay"] != null) p.Ay = (int)t["ay"];
                into.Add(p);
                by[p.Id] = p;
            }
        }

        private static FlagSheet Sheet(string path, int cell, int cols)
        {
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            Color32[] raw = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) Array.Copy(raw, (h - 1 - y) * w, px, y * w, w);
            UnityEngine.Object.Destroy(tex);
            return new FlagSheet { Px = px, W = w, H = h, Cell = cell, Cols = cols };
        }

        public static Color32 Hex(string h)
        {
            Color c;
            if (!string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h.StartsWith("#") ? h : "#" + h, out c)) return c;
            return new Color32(200, 200, 200, 255);
        }

        public static string Hex(Color32 c)
        {
            return c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
        }

        public static float Lum(Color32 c)
        {
            return (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
        }

        private static Color32 Mul(Color32 c, float f)
        {
            return new Color32((byte)Mathf.Clamp(c.r * f, 0f, 255f), (byte)Mathf.Clamp(c.g * f, 0f, 255f), (byte)Mathf.Clamp(c.b * f, 0f, 255f), 255);
        }

        private static int[] Box(int shape)
        {
            int[] b;
            if (_bbox.TryGetValue(shape, out b)) return b;
            b = new[] { N, N, -1, -1 };
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    if (!IsCloth(_shp.Get(shape, x, y))) continue;
                    if (x < b[0]) b[0] = x;
                    if (y < b[1]) b[1] = y;
                    if (x > b[2]) b[2] = x;
                    if (y > b[3]) b[3] = y;
                }
            }
            if (b[2] < b[0]) b = new[] { 16, 16, 47, 47 };
            _bbox[shape] = b;
            return b;
        }

        private static bool IsCloth(Color32 c)
        {
            return c.a == 255 && c.r == 255 && c.g == 255 && c.b == 255;
        }

        public static int Region(string div, float u, float v, float aspect)
        {
            float du = u - 0.5f, dv = v - 0.5f;
            float tu = 0.09f / Math.Max(0.5f, aspect), tv = 0.11f;
            switch (div)
            {
                case "fess2": return v < 0.5f ? 0 : 1;
                case "pale2": return u < 0.5f ? 0 : 1;
                case "tri_h": return v < 1f / 3f ? 1 : v < 2f / 3f ? 0 : 2;
                case "tri_v": return u < 1f / 3f ? 1 : u < 2f / 3f ? 0 : 2;
                case "stripes5": return (int)(v * 5f) % 2 == 0 ? 0 : 1;
                case "stripes7": return (int)(v * 7f) % 2 == 0 ? 0 : 1;
                case "vstripes5": return (int)(u * 5f) % 2 == 0 ? 0 : 1;
                case "canton": return u < 0.45f && v < 0.5f ? 1 : 0;
                case "stripes_canton": return u < 0.45f && v < 0.54f ? 1 : (int)(v * 7f) % 2 == 0 ? 0 : 2;
                case "cross": return Math.Abs(du) < tu * 1.2f || Math.Abs(dv) < tv * 1.2f ? 1 : 0;
                case "nordic": return Math.Abs(u - 0.36f) < tu || Math.Abs(dv) < tv ? 1 : 0;
                case "saltire": return Math.Abs(u - v) < 0.12f || Math.Abs(1f - u - v) < 0.12f ? 1 : 0;
                case "triangle_hoist": return u < 0.5f * (1f - Math.Abs(2f * v - 1f)) ? 1 : 0;
                case "tri_hoist_bicolor": return u < 0.45f * (1f - Math.Abs(2f * v - 1f)) ? 1 : v < 0.5f ? 0 : 2;
                case "quarterly": return (u < 0.5f) ^ (v < 0.5f) ? 1 : 0;
                case "border": return u < 0.1f || u > 0.9f || v < 0.09f || v > 0.91f ? 1 : 0;
                case "bend": return Math.Abs(u - v) < 0.15f ? 1 : 0;
                case "per_bend": return u > v ? 0 : 1;
                case "per_bend_sinister": return u + v < 1f ? 0 : 1;
                case "disc": return du * du * aspect * aspect + dv * dv < 0.09f ? 1 : 0;
                case "pale": return Math.Abs(du) < 0.18f ? 1 : 0;
                case "fess": return Math.Abs(dv) < 0.18f ? 1 : 0;
                case "per_saltire": return (u > v) ^ (u + v > 1f) ? 1 : 0;
                case "checky": return ((int)(u * 4f) + (int)(v * 5f)) % 2;
                case "chief": return v < 0.3f ? 1 : 0;
                case "base": return v > 0.7f ? 1 : 0;
                case "pile": return Math.Abs(du) < 0.4f * (1f - v) ? 1 : 0;
                case "pall": return (v < 0.5f && (Math.Abs(u - v) < 0.12f || Math.Abs(1f - u - v) < 0.12f)) || (v >= 0.45f && Math.Abs(du) < 0.1f) ? 1 : 0;
                case "lozenge": return Math.Abs(du) + Math.Abs(dv) < 0.42f ? 1 : 0;
                case "wavy": return (int)((v + 0.05f * Mathf.Sin(u * 14f)) * 5f) % 2 == 0 ? 0 : 1;
                case "gyronny":
                {
                    float a = Mathf.Atan2(dv, du * aspect);
                    int s = (int)Math.Floor((a + Math.PI) / (Math.PI / 4.0));
                    return s % 2;
                }
                case "cross_border":
                    if (u < 0.08f || u > 0.92f || v < 0.08f || v > 0.92f) return 2;
                    return Math.Abs(du) < tu || Math.Abs(dv) < tv ? 1 : 0;
                case "fess_border":
                    if (u < 0.08f || u > 0.92f || v < 0.08f || v > 0.92f) return 2;
                    return Math.Abs(dv) < 0.16f ? 1 : 0;
                case "hoist_band": return u < 0.3f ? 1 : 0;
                case "hoist_tri": return u < 0.3f ? 1 : v < 0.5f ? 0 : 2;
                case "rising_sun": return du * du * aspect * aspect + (v - 1f) * (v - 1f) < 0.2f ? 1 : 0;
                case "diag_stripes": return (int)((u + v) * 3.5f) % 2;
                case "chevron": return Math.Abs(v - (0.75f - Math.Abs(du) * 0.9f)) < 0.13f ? 1 : 0;
                case "bars3": return v > 0.15f && v < 0.85f && (int)((v - 0.15f) * 7.15f) % 2 == 0 ? 1 : 0;
                default: return 0;
            }
        }

        public static void Clear()
        {
            foreach (var kv in _cache)
            {
                if (kv.Value == null) continue;
                UnityEngine.Object.Destroy(kv.Value.texture);
                UnityEngine.Object.Destroy(kv.Value);
            }
            _cache.Clear();
            _order.Clear();
        }

        public static Sprite Get(FlagSpec f, Color32 main)
        {
            if (f == null || !Ready) return null;
            string key = f.ToString() + "#" + Hex(main);
            Sprite s;
            if (_cache.TryGetValue(key, out s) && s != null) return s;
            Color32[] px = Compose(f, main);
            if (px == null) return null;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var flip = new Color32[N * N];
            for (int y = 0; y < N; y++) Array.Copy(px, y * N, flip, (N - 1 - y) * N, N);
            tex.SetPixels32(flip);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 1f);
            s.name = "civ_flag";
            _cache[key] = s;
            _order.AddLast(key);
            while (_order.Count > Cap)
            {
                string old = _order.First.Value;
                _order.RemoveFirst();
                Sprite os;
                if (_cache.TryGetValue(old, out os) && os != null)
                {
                    UnityEngine.Object.Destroy(os.texture);
                    UnityEngine.Object.Destroy(os);
                }
                _cache.Remove(old);
            }
            return s;
        }

        public static Color32 EmblemColor(Color32 bg, FlagPalette pal, Color32 main)
        {
            float lb = Lum(bg);
            Color32 best = pal.Metal;
            if (Math.Abs(Lum(best) - lb) > 0.28f) return best;
            Color32[] opts = { pal.C1, pal.C2, main, new Color32(245, 242, 232, 255), new Color32(40, 38, 48, 255) };
            float bd = -1f;
            foreach (Color32 c in opts)
            {
                float d = Math.Abs(Lum(c) - lb);
                if (d > bd)
                {
                    bd = d;
                    best = c;
                }
            }
            return best;
        }

        public static Color32[] Compose(FlagSpec f, Color32 main)
        {
            FlagPart shape;
            if (!ShapeById.TryGetValue(f.Shape, out shape)) shape = Shapes[0];
            FlagPalette pal;
            if (!PaletteById.TryGetValue(f.Pal, out pal)) pal = Palettes[0];
            main.a = 255;
            var px = new Color32[N * N];
            int[] b = Box(shape.Index);
            float bw = b[2] - b[0] + 1, bh = b[3] - b[1] + 1;
            float aspect = bw / Math.Max(1f, bh);
            var reg = new Color32[3];
            reg[0] = main;
            reg[1] = pal.C1;
            reg[2] = pal.C2;
            if (Math.Abs(Lum(reg[1]) - Lum(reg[0])) < 0.08f && Math.Abs(reg[1].r - reg[0].r) + Math.Abs(reg[1].g - reg[0].g) + Math.Abs(reg[1].b - reg[0].b) < 60) reg[1] = pal.C2;
            Color32 unCol = main;
            FlagPart unEmb = null;
            if (f.Un.Length > 0) ParseRef(f.Un, out unEmb, out unCol);
            Color32 ovCol = main;
            FlagPart ovEmb = null;
            if (f.Ov.Length > 0) ParseRef(f.Ov, out ovEmb, out ovCol);
            var cloth = new bool[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    Color32 c = _shp.Get(shape.Index, x, y);
                    if (c.a == 0) continue;
                    if (!IsCloth(c))
                    {
                        px[y * N + x] = c;
                        continue;
                    }
                    cloth[y * N + x] = true;
                    float u = (x - b[0] + 0.5f) / bw, v = (y - b[1] + 0.5f) / bh;
                    int r = Region(f.Div, u, v, aspect);
                    Color32 col = reg[Math.Min(2, r)];
                    if (unEmb != null && u >= 0.5f && r == 0) col = unCol;
                    if (ovEmb != null && u < 0.42f && v < 0.46f) col = ovCol;
                    if (u > 0.82f) col = Mul(col, 0.9f);
                    else if (u < 0.1f) col = Mul(col, 1.06f);
                    px[y * N + x] = col;
                }
            }
            int cx = (b[0] + b[2]) / 2 + shape.Ax, cy = (b[1] + b[3]) / 2 + shape.Ay;
            bool canton = f.Div == "canton" || f.Div == "stripes_canton";
            FlagPart emb;
            EmblemById.TryGetValue(f.Emblem, out emb);
            if (ovEmb != null)
            {
                int ox = b[0] + (int)(bw * 0.21f), oy = b[1] + (int)(bh * 0.23f);
                Stamp(px, cloth, _emb, ovEmb.Index, 20, ox, oy, EmblemColor(ovCol, pal, main), true);
                for (int x = b[0]; x < b[0] + (int)(bw * 0.42f); x++)
                {
                    int y = b[1] + (int)(bh * 0.46f);
                    if (y < N && cloth[y * N + x]) px[y * N + x] = Mul(px[y * N + x], 0.6f);
                }
            }
            if (emb != null)
            {
                if (unEmb != null)
                {
                    int lx = b[0] + (int)(bw * 0.25f), rx = b[0] + (int)(bw * 0.75f);
                    bool half = bw < 36f;
                    Stamp(px, cloth, _emb, emb.Index, 20, lx, cy, EmblemColor(Sample(px, lx, cy), pal, main), half);
                    Stamp(px, cloth, _emb, unEmb.Index, 20, rx, cy, EmblemColor(Sample(px, rx, cy), pal, main), half);
                }
                else if (canton && ovEmb == null)
                {
                    int ex = b[0] + (int)(bw * 0.225f), ey = b[1] + (int)(bh * (f.Div == "canton" ? 0.25f : 0.27f));
                    Stamp(px, cloth, _emb, emb.Index, 20, ex, ey, EmblemColor(Sample(px, ex, ey), pal, main), bw * 0.45f < 22f);
                }
                else
                {
                    int ex = cx, ey = cy;
                    if (f.Div == "triangle_hoist" || f.Div == "tri_hoist_bicolor") ex = b[0] + (int)(bw * 0.17f);
                    else if (f.Div == "hoist_band" || f.Div == "hoist_tri") ex = b[0] + (int)(bw * 0.15f);
                    else if (f.Div == "chief") ey = b[1] + (int)(bh * 0.62f);
                    else if (f.Div == "base") ey = b[1] + (int)(bh * 0.38f);
                    else if (f.Div == "rising_sun") ey = b[1] + (int)(bh * 0.4f);
                    bool half = (f.Div == "triangle_hoist" || f.Div == "tri_hoist_bicolor" || f.Div == "hoist_band" || f.Div == "hoist_tri") && bw < 44f;
                    Stamp(px, cloth, _emb, emb.Index, 20, ex, ey, EmblemColor(Sample(px, ex, ey), pal, main), half);
                }
            }
            FlagPart ch;
            if (f.Charge.Length > 0 && ChargeById.TryGetValue(f.Charge, out ch)) DrawCharges(px, cloth, ch, f.Layout, b, cx, cy, canton, ovEmb != null, pal, main);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    if (!cloth[i]) continue;
                    bool edge = x == 0 || y == 0 || x == N - 1 || y == N - 1 || !cloth[i - 1] || !cloth[i + 1] || !cloth[i - N] || !cloth[i + N];
                    if (edge) px[i] = Mul(px[i], 0.62f);
                }
            }
            return px;
        }

        public static Color32[] EditorBackground(int shapeIndex, string div)
        {
            if (!Ready || shapeIndex < 0 || shapeIndex >= Shapes.Count) return null;
            var px = new Color32[N * N];
            int[] b = Box(shapeIndex);
            float bw = b[2] - b[0] + 1, bh = b[3] - b[1] + 1;
            float aspect = bw / Math.Max(1f, bh);
            byte[] tone = { 255, 178, 118 };
            var cloth = new bool[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    Color32 c = _shp.Get(shapeIndex, x, y);
                    if (c.a == 0) continue;
                    if (!IsCloth(c))
                    {
                        byte g = (byte)(Lum(c) * 255f);
                        px[y * N + x] = new Color32(g, g, g, c.a);
                        continue;
                    }
                    cloth[y * N + x] = true;
                    float u = (x - b[0] + 0.5f) / bw, v = (y - b[1] + 0.5f) / bh;
                    byte t = tone[Math.Min(2, Region(div, u, v, aspect))];
                    px[y * N + x] = new Color32(t, t, t, 255);
                }
            }
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    if (!cloth[i]) continue;
                    bool edge = x == 0 || y == 0 || x == N - 1 || y == N - 1 || !cloth[i - 1] || !cloth[i + 1] || !cloth[i - N] || !cloth[i + N];
                    if (edge) px[i] = Mul(px[i], 0.62f);
                }
            }
            return px;
        }

        public static Color32[] EditorIcon(int emblemIndex)
        {
            if (!Ready || _emb == null || emblemIndex < 0 || emblemIndex >= Emblems.Count) return null;
            var px = new Color32[N * N];
            int size = 20, scale = 2, x0 = (N - size * scale) / 2, y0 = (N - size * scale) / 2;
            for (int y = 0; y < size * scale; y++)
            {
                for (int x = 0; x < size * scale; x++)
                {
                    Color32 g = _emb.Get(emblemIndex, x / scale, y / scale);
                    if (g.a == 0) continue;
                    byte v = (byte)Math.Max(60, (int)g.r);
                    px[(y0 + y) * N + x0 + x] = new Color32(v, v, v, 255);
                }
            }
            return px;
        }

        public static Sprite ToSprite(Color32[] px, string name)
        {
            if (px == null) return null;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var flip = new Color32[N * N];
            for (int y = 0; y < N; y++) Array.Copy(px, y * N, flip, (N - 1 - y) * N, N);
            tex.SetPixels32(flip);
            tex.Apply(false);
            Sprite s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 1f);
            s.name = name;
            return s;
        }

        private static Color32 Sample(Color32[] px, int x, int y)
        {
            x = Mathf.Clamp(x, 0, N - 1);
            y = Mathf.Clamp(y, 0, N - 1);
            return px[y * N + x];
        }

        private static void ParseRef(string s, out FlagPart emb, out Color32 col)
        {
            emb = null;
            col = new Color32(200, 200, 200, 255);
            string[] p = s.Split(':');
            if (p.Length > 0) EmblemById.TryGetValue(p[0], out emb);
            if (p.Length > 1) col = Hex("#" + p[1]);
        }

        private static void Stamp(Color32[] px, bool[] cloth, FlagSheet sheet, int index, int cell, int cx, int cy, Color32 tint, bool half)
        {
            if (sheet == null) return;
            int size = half ? cell / 2 : cell;
            int x0 = cx - size / 2, y0 = cy - size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 g;
                    if (half)
                    {
                        Color32 a = sheet.Get(index, x * 2, y * 2), bb = sheet.Get(index, x * 2 + 1, y * 2), c = sheet.Get(index, x * 2, y * 2 + 1), d = sheet.Get(index, x * 2 + 1, y * 2 + 1);
                        int n = (a.a > 0 ? 1 : 0) + (bb.a > 0 ? 1 : 0) + (c.a > 0 ? 1 : 0) + (d.a > 0 ? 1 : 0);
                        if (n < 2) continue;
                        int sum = (a.a > 0 ? a.r : 0) + (bb.a > 0 ? bb.r : 0) + (c.a > 0 ? c.r : 0) + (d.a > 0 ? d.r : 0);
                        byte v = (byte)(sum / n);
                        g = new Color32(v, v, v, 255);
                    }
                    else
                    {
                        g = sheet.Get(index, x, y);
                        if (g.a == 0) continue;
                    }
                    int tx = x0 + x, ty = y0 + y;
                    if (tx < 0 || ty < 0 || tx >= N || ty >= N || !cloth[ty * N + tx]) continue;
                    float k = g.r / 255f;
                    px[ty * N + tx] = new Color32((byte)(tint.r * k), (byte)(tint.g * k), (byte)(tint.b * k), 255);
                }
            }
        }

        private static void DrawCharges(Color32[] px, bool[] cloth, FlagPart ch, string layout, int[] b, int cx, int cy, bool canton, bool ov, FlagPalette pal, Color32 main)
        {
            var pts = new List<Vector2Int>();
            float bw = b[2] - b[0] + 1, bh = b[3] - b[1] + 1;
            switch (layout)
            {
                case "canton":
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                            pts.Add(new Vector2Int(b[0] + (int)(bw * (0.07f + c * 0.13f)) + (r % 2) * 2, b[1] + (int)(bh * (0.1f + r * 0.14f))));
                    break;
                case "row3":
                    for (int i = 0; i < 3; i++) pts.Add(new Vector2Int(cx + (i - 1) * 9, b[1] + 6));
                    break;
                case "ring":
                    for (int i = 0; i < 8; i++)
                    {
                        float a = Mathf.PI * 2f * i / 8f;
                        pts.Add(new Vector2Int(cx + (int)(Mathf.Cos(a) * 14f), cy + (int)(Mathf.Sin(a) * 13f)));
                    }
                    break;
                case "corners":
                    pts.Add(new Vector2Int(b[0] + 5, b[1] + 5));
                    pts.Add(new Vector2Int(b[2] - 5, b[1] + 5));
                    pts.Add(new Vector2Int(b[0] + 5, b[3] - 5));
                    pts.Add(new Vector2Int(b[2] - 5, b[3] - 5));
                    break;
                case "arc":
                    for (int i = 0; i < 5; i++)
                    {
                        float a = Mathf.PI * (1.15f + i * 0.175f);
                        pts.Add(new Vector2Int(cx + (int)(Mathf.Cos(a) * 15f), cy + 2 + (int)(Mathf.Sin(a) * 15f)));
                    }
                    break;
                case "hoist":
                    for (int i = 0; i < 3; i++) pts.Add(new Vector2Int(b[0] + 6, b[1] + (int)(bh * (0.2f + i * 0.3f))));
                    break;
                case "below":
                    for (int i = 0; i < 3; i++) pts.Add(new Vector2Int(cx + (i - 1) * 9, b[3] - 6));
                    break;
                default:
                    pts.Add(new Vector2Int(canton || ov ? b[2] - 7 : b[0] + 6, b[1] + 6));
                    break;
            }
            foreach (Vector2Int p in pts)
            {
                if (ov && p.x < b[0] + bw * 0.44f && p.y < b[1] + bh * 0.48f && layout != "canton") continue;
                Color32 bg = Sample(px, p.x, p.y);
                Stamp(px, cloth, _chg, ch.Index, 8, p.x, p.y, EmblemColor(bg, pal, main), false);
            }
        }
    }
}
