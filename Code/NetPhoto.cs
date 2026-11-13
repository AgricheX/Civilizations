using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class NetPhoto
    {
        public const int W = 80;
        public const int H = 45;
        private const int Cap = 32;
        private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();
        private static readonly LinkedList<int> _order = new LinkedList<int>();
        private static readonly Dictionary<int, Color32[]> _src = new Dictionary<int, Color32[]>();
        private static readonly Dictionary<int, Vector2Int> _srcSize = new Dictionary<int, Vector2Int>();
        private static readonly HashSet<string> NoSnap = new HashSet<string> { "portrait", "meme", "flag", "doc_treaty", "doc_ballot", "doc_chart", "doc_decree", "doc_paper", "doc_flag" };

        public static bool Has(NetPost p)
        {
            if (p == null) return false;
            if (p.Img.Length > 0 && NoSnap.Contains(p.Img)) return true;
            return p.Snap.Length > 0;
        }

        public static Sprite Get(NetPost p)
        {
            if (!Has(p)) return null;
            Sprite s;
            if (_cache.TryGetValue(p.Id, out s) && s != null) return s;
            Color32[] buf = null;
            try
            {
                buf = Build(p);
            }
            catch (Exception e)
            {
                CivMod.Report("net-photo", e);
            }
            if (buf == null) return null;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var flip = new Color32[W * H];
            for (int y = 0; y < H; y++) Array.Copy(buf, y * W, flip, (H - 1 - y) * W, W);
            tex.SetPixels32(flip);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
            _cache[p.Id] = s;
            _order.AddLast(p.Id);
            while (_order.Count > Cap)
            {
                int old = _order.First.Value;
                _order.RemoveFirst();
                Drop(old);
            }
            if (_src.Count > 160)
            {
                _src.Clear();
                _srcSize.Clear();
            }
            return s;
        }

        private static void Drop(int id)
        {
            Sprite s;
            if (!_cache.TryGetValue(id, out s)) return;
            if (s != null)
            {
                UnityEngine.Object.Destroy(s.texture);
                UnityEngine.Object.Destroy(s);
            }
            _cache.Remove(id);
        }

        public static void Forget(IEnumerable<int> ids)
        {
            foreach (int id in ids)
            {
                if (!_cache.ContainsKey(id)) continue;
                Drop(id);
                _order.Remove(id);
            }
        }

        public static void Clear()
        {
            foreach (int id in _cache.Keys.ToList()) Drop(id);
            _cache.Clear();
            _order.Clear();
            _src.Clear();
            _srcSize.Clear();
        }

        private static bool ParseSnap(string snap, out int x, out int y)
        {
            x = y = 0;
            if (string.IsNullOrEmpty(snap)) return false;
            int i = snap.IndexOf(',');
            return i > 0 && int.TryParse(snap.Substring(0, i), out x) && int.TryParse(snap.Substring(i + 1), out y);
        }

        private static string Kind(NetPost p)
        {
            string t = p.Img.Length > 0 ? p.Img : "city";
            if (p.Era >= 13 && (t == "map" || (t == "battle" && p.Seed % 3 == 0))) return "sat";
            if (p.Era >= 14 && t == "city" && p.Who != "person" && p.Seed % 5 == 0) return "cctv";
            return t;
        }

        private static Color32[] Build(NetPost p)
        {
            var buf = new Color32[W * H];
            var rnd = new System.Random(p.Seed);
            string kind = Kind(p);
            int cx, cy;
            bool has = ParseSnap(p.Snap, out cx, out cy);
            Kingdom k = G.KingdomById(p.Kingdom);
            if (!has && k != null && k.capital != null && G.Tile(k.capital) != null)
            {
                cx = G.Tile(k.capital).x;
                cy = G.Tile(k.capital).y;
                has = true;
            }
            string filter = FilterFor(p, kind);
            switch (kind)
            {
                case "portrait":
                    if (!Portrait(buf, p, k, rnd))
                    {
                        if (!has) return null;
                        Scene(buf, cx, cy, rnd, false);
                    }
                    break;
                case "flag":
                    FlagCard(buf, p, k);
                    break;
                case "meme":
                    Meme(buf, p, k, rnd);
                    break;
                case "doc_treaty":
                case "doc_ballot":
                case "doc_chart":
                case "doc_decree":
                case "doc_paper":
                case "doc_flag":
                    Document(buf, kind, p, k, rnd);
                    break;
                case "map":
                    if (!has) return null;
                    Map(buf, cx, cy, false);
                    break;
                case "sat":
                    if (!has) return null;
                    Map(buf, cx, cy, true);
                    break;
                case "battle":
                    if (!has) return null;
                    Scene(buf, cx, cy, rnd, true);
                    Battle(buf, rnd);
                    break;
                case "festival":
                    if (!has) return null;
                    Scene(buf, cx, cy, rnd, false);
                    Festival(buf, rnd, (p.Seed & 1) == 1, p.Era);
                    break;
                default:
                    if (!has) return null;
                    Scene(buf, cx, cy, rnd, false);
                    break;
            }
            Filter(buf, filter, rnd);
            return buf;
        }

        private static string FilterFor(NetPost p, string kind)
        {
            if (kind == "sat") return "sat";
            if (kind == "cctv") return "cctv";
            if (p.Med.Length == 0) return "modern";
            int e = p.Era;
            if (e <= 4) return "carving";
            if (e <= 7) return "illum";
            if (e <= 9) return "engraving";
            if (e == 10) return "sepia";
            if (e == 11) return "bw";
            if (e <= 13) return "faded";
            if (kind == "portrait" && p.Who == "person" && p.Med == "social") return "selfie";
            return "modern";
        }

        private static int Hash(int a, int b, int c)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ c * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return h ^ (h >> 15);
            }
        }

        private static Color32 Shade(Color32 c, float f)
        {
            return new Color32((byte)Mathf.Clamp(c.r * f, 0f, 255f), (byte)Mathf.Clamp(c.g * f, 0f, 255f), (byte)Mathf.Clamp(c.b * f, 0f, 255f), 255);
        }

        private static Color32 Mix(Color32 a, Color32 b, float t)
        {
            return new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);
        }

        private static void Put(Color32[] buf, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            buf[y * W + x] = c;
        }

        private static void Blend(Color32[] buf, int x, int y, Color32 c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            buf[y * W + x] = Mix(buf[y * W + x], c, a);
        }

        private static void Fill(Color32[] buf, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Math.Max(0, y0); y <= Math.Min(H - 1, y1); y++)
                for (int x = Math.Max(0, x0); x <= Math.Min(W - 1, x1); x++)
                    buf[y * W + x] = c;
        }

        private static void Line(Color32[] buf, int x0, int y0, int x1, int y1, Color32 c)
        {
            int n = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= n; i++)
            {
                float t = n == 0 ? 0f : i / (float)n;
                Put(buf, Mathf.RoundToInt(x0 + (x1 - x0) * t), Mathf.RoundToInt(y0 + (y1 - y0) * t), c);
            }
        }

        private static Color32 TileColor(WorldTile t, int px, int py)
        {
            if (t == null || t.Type == null) return new Color32(20, 24, 30, 255);
            Color32 c = t.getColor();
            c.a = 255;
            int h = Hash(t.x, t.y, px * 7 + py);
            float f = 1f + ((h & 15) - 7) * 0.011f;
            if (t.Type.liquid && ((px + py * 2 + t.x) % 9 == 0)) f += 0.12f;
            return Shade(c, f);
        }

        private static void Terrain(Color32[] buf, float x0, float y0, int sc)
        {
            for (int py = 0; py < H; py++)
            {
                for (int px = 0; px < W; px++)
                {
                    float wx = x0 + (px + 0.5f) / sc;
                    float wy = y0 + (H - 1 - py + 0.5f) / sc;
                    WorldTile t = Globe.Tile(Mathf.FloorToInt(wx), Mathf.FloorToInt(wy));
                    buf[py * W + px] = TileColor(t, px, py);
                }
            }
        }

        private static Color32[] Pixels(Sprite s, out int w, out int h)
        {
            w = h = 0;
            if (s == null || s.texture == null) return null;
            int id = s.GetInstanceID();
            Color32[] px;
            Vector2Int size;
            if (_src.TryGetValue(id, out px) && _srcSize.TryGetValue(id, out size))
            {
                w = size.x;
                h = size.y;
                return px;
            }
            try
            {
                Rect r = s.textureRect;
                w = (int)r.width;
                h = (int)r.height;
                if (w <= 0 || h <= 0 || w > 128 || h > 128) return null;
                Color[] raw = s.texture.GetPixels((int)r.x, (int)r.y, w, h);
                px = new Color32[raw.Length];
                for (int i = 0; i < raw.Length; i++) px[i] = raw[i];
            }
            catch (Exception)
            {
                px = null;
            }
            _src[id] = px;
            _srcSize[id] = new Vector2Int(w, h);
            return px;
        }

        private static void Blit(Color32[] buf, Sprite s, float sx, float sy, int scale, bool flip = false)
        {
            int w, h;
            Color32[] px = Pixels(s, out w, out h);
            if (px == null) return;
            int ox = Mathf.RoundToInt(sx - s.pivot.x * scale);
            int oy = Mathf.RoundToInt(sy - s.pivot.y * scale);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[y * w + (flip ? w - 1 - x : x)];
                    if (c.a < 40) continue;
                    c.a = 255;
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                            Put(buf, ox + x * scale + dx, H - 1 - (oy + y * scale + dy), c);
                }
            }
        }

        private static Sprite BuildingSprite(Building b)
        {
            try
            {
                Sprite s = b.getLastColoredSprite();
                if (s != null) return s;
                return b.checkSpriteToRender();
            }
            catch (Exception)
            {
                try
                {
                    return b.calculateMainSprite();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private static Sprite ActorSprite(Actor a)
        {
            try
            {
                Sprite s = a.getLastColoredSprite();
                if (s != null) return s;
                return a.getSpriteToRender();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static List<Actor> _units = new List<Actor>();

        private static void Scene(Color32[] buf, int cx, int cy, System.Random rnd, bool battle)
        {
            const int sc = 4;
            float x0 = cx - W / (2f * sc), y0 = cy - H / (2f * sc);
            Terrain(buf, x0, y0, sc);
            var blds = new HashSet<Building>();
            _units.Clear();
            int tx0 = Mathf.FloorToInt(x0) - 2, tx1 = Mathf.CeilToInt(x0 + W / (float)sc) + 2;
            int ty0 = Mathf.FloorToInt(y0) - 1, ty1 = Mathf.CeilToInt(y0 + H / (float)sc) + 5;
            for (int ty = ty0; ty <= ty1; ty++)
            {
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    WorldTile t = Globe.Tile(tx, ty);
                    if (t == null) continue;
                    if (t.building != null && t.building.isAlive()) blds.Add(t.building);
                    if (_units.Count < (battle ? 60 : 30) && t.hasUnits()) t.doUnits(a => { if (G.Ok(a) && _units.Count < 60) _units.Add(a); });
                }
            }
            var draw = new List<KeyValuePair<float, object>>();
            foreach (Building b in blds) draw.Add(new KeyValuePair<float, object>(b.current_position.y, b));
            foreach (Actor a in _units) draw.Add(new KeyValuePair<float, object>(a.current_position.y - 0.1f, a));
            foreach (var kv in draw.OrderByDescending(x => x.Key))
            {
                Building b = kv.Value as Building;
                if (b != null)
                {
                    Sprite s = BuildingSprite(b);
                    if (s != null) Blit(buf, s, (b.current_position.x - x0) * sc, (b.current_position.y - y0) * sc, 1);
                    continue;
                }
                Actor a = kv.Value as Actor;
                if (a == null) continue;
                Sprite us = ActorSprite(a);
                if (us != null) Blit(buf, us, (a.current_position.x - x0) * sc, (a.current_position.y - y0) * sc, 1, rnd.Next(2) == 0);
            }
            _units.Clear();
        }

        private static void Battle(Color32[] buf, System.Random rnd)
        {
            for (int i = 0; i < 7; i++)
            {
                int x = rnd.Next(W), y = rnd.Next(H / 2, H);
                int r = 3 + rnd.Next(4);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx * dx + dy * dy <= r * r) Blend(buf, x + dx, y + dy - r, new Color32(150, 146, 140, 255), 0.45f);
            }
            for (int i = 0; i < 26; i++)
            {
                int x = rnd.Next(W), y = rnd.Next(H);
                Put(buf, x, y, rnd.Next(2) == 0 ? new Color32(255, 160, 40, 255) : new Color32(255, 220, 90, 255));
                Put(buf, x, y + 1, new Color32(200, 70, 30, 255));
            }
            for (int i = 0; i < W * H; i++) buf[i] = Mix(buf[i], new Color32(140, 80, 50, 255), 0.12f);
        }

        private static void Festival(Color32[] buf, System.Random rnd, bool fireworks, int era)
        {
            for (int i = 0; i < W * H; i++) buf[i] = Mix(Shade(buf[i], 0.5f), new Color32(20, 26, 70, 255), 0.3f);
            Color32[] warm = { new Color32(255, 210, 90, 255), new Color32(255, 140, 60, 255), new Color32(255, 90, 90, 255), new Color32(255, 240, 160, 255) };
            for (int s = 0; s < 3; s++)
            {
                int y = 8 + rnd.Next(18);
                int x0 = rnd.Next(10), x1 = W - rnd.Next(10);
                for (int x = x0; x < x1; x++)
                {
                    int yy = y + (int)(Mathf.Sin((x - x0) / (float)(x1 - x0) * Mathf.PI) * 4f);
                    Put(buf, x, yy, new Color32(60, 50, 40, 255));
                    if ((x - x0) % 5 == 0)
                    {
                        Color32 c = warm[rnd.Next(warm.Length)];
                        Put(buf, x, yy + 1, c);
                        Put(buf, x, yy + 2, Shade(c, 0.8f));
                    }
                }
            }
            if (era < 6)
            {
                int bx = W / 2 + rnd.Next(-15, 15), by = H - 8;
                for (int i = 0; i < 18; i++) Put(buf, bx + rnd.Next(-3, 4), by - rnd.Next(0, 8), warm[rnd.Next(warm.Length)]);
            }
            if (!fireworks) return;
            Color32[] fw = { new Color32(255, 80, 80, 255), new Color32(90, 200, 255, 255), new Color32(255, 230, 90, 255), new Color32(140, 255, 120, 255), new Color32(255, 120, 255, 255) };
            for (int n = 0; n < 4; n++)
            {
                int cx = 8 + rnd.Next(W - 16), cy = 5 + rnd.Next(14), r = 4 + rnd.Next(4);
                Color32 c = fw[rnd.Next(fw.Length)];
                for (int i = 0; i < 12; i++)
                {
                    float a = Mathf.PI * 2f * i / 12f;
                    for (int d = r / 2; d <= r; d++) Put(buf, cx + Mathf.RoundToInt(Mathf.Cos(a) * d), cy + Mathf.RoundToInt(Mathf.Sin(a) * d), d == r ? Color.white : (Color)c);
                }
                Put(buf, cx, cy, Color.white);
            }
        }

        private static Color32[] FlagPixels(string code, Kingdom k)
        {
            FlagSpec f = FlagSpec.Parse(code);
            if (f == null && k != null) f = Flags.Spec(k);
            if (f == null || !FlagArt.Ready) return null;
            return FlagArt.Compose(f, k != null ? Flags.Main(k) : new Color32(180, 160, 140, 255));
        }

        private static void StampFlag(Color32[] buf, Color32[] flag, int cx, int cy, int step)
        {
            if (flag == null) return;
            int n = FlagArt.N / step;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Color32 c = flag[(y * step) * FlagArt.N + x * step];
                    if (c.a < 40) continue;
                    c.a = 255;
                    Put(buf, cx - n / 2 + x, cy - n / 2 + y, c);
                }
            }
        }

        private static void Gradient(Color32[] buf, Color32 top, Color32 bottom)
        {
            for (int y = 0; y < H; y++)
            {
                Color32 c = Mix(top, bottom, y / (float)(H - 1));
                for (int x = 0; x < W; x++) buf[y * W + x] = c;
            }
        }

        private static bool Portrait(Color32[] buf, NetPost p, Kingdom k, System.Random rnd)
        {
            Actor a = G.ActorById(p.Who == "person" ? p.AuthorId : p.Photo);
            if (!G.Ok(a) && k != null && k.hasKing()) a = k.king;
            if (!G.Ok(a)) return false;
            Sprite s = ActorSprite(a);
            if (s == null) return false;
            Kingdom ak = a.kingdom ?? k;
            Color32 main = ak != null ? Flags.Main(ak) : new Color32(120, 130, 150, 255);
            Gradient(buf, Shade(main, 1.1f), Shade(main, 0.55f));
            Color32[] flag = ak != null ? FlagPixels(Flags.Code(ak), ak) : null;
            if (flag != null && p.Who != "person") StampFlag(buf, flag, W - 18, 18, 2);
            else if (flag != null) StampFlag(buf, flag, W - 10, 9, 4);
            for (int y = H - 6; y < H; y++)
                for (int x = 0; x < W; x++)
                    buf[y * W + x] = Shade(main, 0.4f);
            int w, h;
            if (Pixels(s, out w, out h) == null) return false;
            int scale = Math.Max(1, Math.Min(4, (H - 4) / Math.Max(1, h)));
            Blit(buf, s, W / 2f - 6f + s.pivot.x * scale - w * scale / 2f, 4f + s.pivot.y * scale, scale, rnd.Next(2) == 0);
            return true;
        }

        private static void FlagCard(Color32[] buf, NetPost p, Kingdom k)
        {
            Color32 main = k != null ? Flags.Main(k) : new Color32(120, 130, 150, 255);
            Gradient(buf, new Color32(150, 190, 230, 255), new Color32(210, 230, 245, 255));
            for (int y = H - 7; y < H; y++)
                for (int x = 0; x < W; x++)
                    buf[y * W + x] = Mix(new Color32(80, 140, 60, 255), main, 0.15f);
            string code = p.Flag.Split('#')[0];
            Color32[] flag = FlagPixels(code, k);
            if (flag == null) return;
            for (int y = 0; y < FlagArt.N; y++)
            {
                for (int x = 0; x < FlagArt.N; x++)
                {
                    Color32 c = flag[y * FlagArt.N + x];
                    if (c.a < 40) continue;
                    c.a = 255;
                    int tx = W / 2 - 22 + (x * 44) / FlagArt.N;
                    int ty = (y * 44) / FlagArt.N;
                    Put(buf, tx, ty, c);
                }
            }
        }

        private static readonly Color32 Paper = new Color32(236, 226, 196, 255);
        private static readonly Color32 Ink = new Color32(70, 52, 40, 255);

        private static void Scribble(Color32[] buf, int x0, int x1, int y, System.Random rnd, Color32 ink)
        {
            int x = x0;
            while (x < x1)
            {
                int len = 2 + rnd.Next(6);
                for (int i = 0; i < len && x + i < x1; i++) Put(buf, x + i, y, ink);
                x += len + 1 + rnd.Next(2);
            }
        }

        private static void Document(Color32[] buf, string kind, NetPost p, Kingdom k, System.Random rnd)
        {
            Color32 desk = new Color32(96, 66, 44, 255);
            Fill(buf, 0, 0, W - 1, H - 1, desk);
            for (int y = 0; y < H; y += 3) for (int x = 0; x < W; x++) if (Hash(x, y, 3) % 5 == 0) Put(buf, x, y, Shade(desk, 0.85f));
            int x0 = 10, x1 = W - 11, y0 = 2, y1 = H - 3;
            Color32 paper = p.Era >= 10 ? new Color32(238, 236, 228, 255) : Paper;
            Fill(buf, x0 + 1, y0 + 1, x1 + 1, y1 + 1, Shade(desk, 0.6f));
            Fill(buf, x0, y0, x1, y1, paper);
            for (int i = x0; i <= x1; i++)
            {
                Put(buf, i, y0, Shade(paper, 0.85f));
                Put(buf, i, y1, Shade(paper, 0.8f));
            }
            Kingdom o = G.KingdomById(p.About);
            switch (kind)
            {
                case "doc_treaty":
                {
                    if (p.Era < 8)
                    {
                        Fill(buf, x0 - 2, y0 - 1, x1 + 2, y0 + 1, new Color32(130, 90, 50, 255));
                        Fill(buf, x0 - 2, y1 - 1, x1 + 2, y1 + 1, new Color32(130, 90, 50, 255));
                    }
                    Color32[] fa = k != null ? FlagPixels(Flags.Code(k), k) : null;
                    Color32[] fb = o != null ? FlagPixels(Flags.Code(o), o) : null;
                    StampFlag(buf, fa, x0 + 10, y0 + 9, 4);
                    StampFlag(buf, fb, x1 - 10, y0 + 9, 4);
                    for (int y = y0 + 6; y < y1 - 10; y += 3) Scribble(buf, x0 + 20, x1 - 20, y, rnd, Ink);
                    for (int y = y0 + 20; y < y1 - 8; y += 3) Scribble(buf, x0 + 4, x1 - 4, y, rnd, Ink);
                    Seal(buf, x0 + 14, y1 - 5, k);
                    Seal(buf, x1 - 14, y1 - 5, o);
                    break;
                }
                case "doc_ballot":
                    for (int i = 0; i < 4; i++)
                    {
                        int y = y0 + 6 + i * 9;
                        Fill(buf, x0 + 5, y, x0 + 10, y + 5, Ink);
                        Fill(buf, x0 + 6, y + 1, x0 + 9, y + 4, paper);
                        if (i == p.Seed % 4)
                        {
                            Line(buf, x0 + 6, y + 2, x0 + 7, y + 4, new Color32(200, 40, 40, 255));
                            Line(buf, x0 + 7, y + 4, x0 + 11, y - 1, new Color32(200, 40, 40, 255));
                        }
                        Scribble(buf, x0 + 14, x1 - 6, y + 2, rnd, Ink);
                    }
                    break;
                case "doc_chart":
                {
                    for (int x = x0 + 4; x < x1 - 2; x += 6) for (int y = y0 + 3; y < y1 - 3; y++) Put(buf, x, y, Shade(paper, 0.9f));
                    for (int y = y0 + 4; y < y1 - 3; y += 6) for (int x = x0 + 3; x < x1 - 2; x++) Put(buf, x, y, Shade(paper, 0.9f));
                    bool up = !(p.Kind.Contains("down") || p.Kind == "bankrupt" || p.Kind == "inflation");
                    Color32 c = up ? new Color32(40, 150, 70, 255) : new Color32(200, 50, 40, 255);
                    int yy = up ? y1 - 6 : y0 + 6, prevx = x0 + 3, prevy = yy;
                    for (int x = x0 + 3; x < x1 - 2; x += 4)
                    {
                        yy += (up ? -1 : 1) * rnd.Next(0, 4) + rnd.Next(-2, 3);
                        yy = Mathf.Clamp(yy, y0 + 3, y1 - 4);
                        Line(buf, prevx, prevy, x, yy, c);
                        Line(buf, prevx, prevy + 1, x, yy + 1, c);
                        prevx = x;
                        prevy = yy;
                    }
                    break;
                }
                case "doc_flag":
                {
                    string[] parts = p.Flag.Split('#');
                    Color32[] nf = FlagPixels(parts[0], k);
                    Color32[] of = parts.Length > 1 ? FlagPixels(parts[1], k) : null;
                    Fill(buf, 0, 0, W - 1, H - 1, new Color32(210, 222, 236, 255));
                    StampFlag(buf, of, 20, 22, 2);
                    StampFlag(buf, nf, W - 21, 22, 2);
                    for (int i = 0; i < 9; i++) Put(buf, 36 + i, 22, Ink);
                    Line(buf, 42, 19, 45, 22, Ink);
                    Line(buf, 42, 25, 45, 22, Ink);
                    for (int x = 0; x < W; x++) Put(buf, x, H - 1, new Color32(150, 160, 180, 255));
                    break;
                }
                case "doc_paper":
                {
                    Fill(buf, x0 + 3, y0 + 2, x1 - 3, y0 + 6, Ink);
                    for (int y = y0 + 9; y < y0 + 12; y++) Scribble(buf, x0 + 3, x1 - 3, y, rnd, Ink);
                    int mid = (x0 + x1) / 2;
                    Fill(buf, x0 + 3, y0 + 14, mid - 2, y1 - 4, new Color32(150, 150, 150, 255));
                    for (int y = y0 + 14; y < y1 - 3; y += 2) Scribble(buf, mid + 1, x1 - 3, y, rnd, Ink);
                    break;
                }
                default:
                {
                    Color32[] fa = k != null ? FlagPixels(Flags.Code(k), k) : null;
                    StampFlag(buf, fa, (x0 + x1) / 2, y0 + 9, 4);
                    for (int y = y0 + 19; y < y1 - 8; y += 3) Scribble(buf, x0 + 4, x1 - 4, y, rnd, Ink);
                    Seal(buf, (x0 + x1) / 2, y1 - 5, k);
                    break;
                }
            }
        }

        private static void Seal(Color32[] buf, int cx, int cy, Kingdom k)
        {
            Color32 c = k != null ? Mix(new Color32(170, 30, 30, 255), Flags.Main(k), 0.3f) : new Color32(170, 30, 30, 255);
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                    if (dx * dx + dy * dy <= 10) Put(buf, cx + dx, cy + dy, dx * dx + dy * dy >= 7 ? Shade(c, 0.7f) : c);
            Put(buf, cx - 1, cy - 1, Shade(c, 1.3f));
        }

        private static void Map(Color32[] buf, int cx, int cy, bool sat)
        {
            int x0 = cx - W / 2, y0 = cy - H / 2;
            var owner = new Kingdom[W * H];
            for (int py = 0; py < H; py++)
            {
                for (int px = 0; px < W; px++)
                {
                    WorldTile t = Globe.Tile(x0 + px, y0 + (H - 1 - py));
                    Color32 c = TileColor(t, px, py);
                    Kingdom k = null;
                    try
                    {
                        if (t != null && t.zone != null && t.zone.city != null) k = t.zone.city.kingdom;
                    }
                    catch (Exception)
                    {
                        k = null;
                    }
                    owner[py * W + px] = k;
                    if (!sat)
                    {
                        c = Shade(Mix(c, new Color32(200, 190, 160, 255), 0.35f), 0.95f);
                        if (k != null) c = Mix(c, Flags.Main(k), 0.55f);
                    }
                    buf[py * W + px] = c;
                }
            }
            if (sat) return;
            for (int py = 1; py < H - 1; py++)
            {
                for (int px = 1; px < W - 1; px++)
                {
                    Kingdom k = owner[py * W + px];
                    if (k == null) continue;
                    if (owner[py * W + px + 1] != k || owner[(py + 1) * W + px] != k) buf[py * W + px] = Shade(buf[py * W + px], 0.55f);
                }
            }
        }

        private static void Meme(Color32[] buf, NetPost p, Kingdom k, System.Random rnd)
        {
            Actor a = G.ActorById(p.AuthorId);
            if (!G.Ok(a)) a = Internet.RandomUser(k);
            Sprite s = G.Ok(a) ? ActorSprite(a) : null;
            int t = Math.Abs(p.Seed) % 4;
            switch (t)
            {
                case 0:
                    Fill(buf, 0, 0, W / 2 - 1, H / 2 - 1, new Color32(230, 120, 110, 255));
                    Fill(buf, 0, H / 2, W / 2 - 1, H - 1, new Color32(130, 210, 120, 255));
                    Fill(buf, W / 2, 0, W - 1, H - 1, new Color32(245, 245, 240, 255));
                    if (s != null)
                    {
                        Blit(buf, s, W / 4f, H / 2f + 3f, 2, true);
                        Blit(buf, s, W / 4f, 3f, 2, false);
                    }
                    for (int y = 6; y < H / 2 - 4; y += 3) Scribble(buf, W / 2 + 4, W - 5, y, rnd, new Color32(60, 60, 70, 255));
                    for (int y = H / 2 + 5; y < H - 4; y += 3) Scribble(buf, W / 2 + 4, W - 5, y, rnd, new Color32(60, 60, 70, 255));
                    Line(buf, W / 2, 0, W / 2, H - 1, new Color32(40, 40, 40, 255));
                    Line(buf, 0, H / 2, W - 1, H / 2, new Color32(40, 40, 40, 255));
                    break;
                case 1:
                    Gradient(buf, new Color32(255, 170, 60, 255), new Color32(200, 70, 30, 255));
                    for (int i = 0; i < 60; i++)
                    {
                        int x = rnd.Next(W), y = rnd.Next(H / 2, H);
                        Put(buf, x, y, new Color32(255, 230, 120, 255));
                        Put(buf, x, y - 1, new Color32(255, 140, 40, 255));
                    }
                    Fill(buf, W / 2 - 14, H - 12, W / 2 + 14, H - 10, new Color32(110, 70, 40, 255));
                    if (s != null) Blit(buf, s, W / 2f, 12f, 2, false);
                    Fill(buf, W / 2 + 8, H - 20, W / 2 + 10, H - 17, Color.white);
                    break;
                case 2:
                    for (int i = 0; i < 3; i++)
                    {
                        int y0 = i * 15;
                        Color32 bg = Mix(new Color32(60, 60, 90, 255), new Color32(250, 240, 180, 255), i / 2f);
                        Fill(buf, 0, y0, W - 1, y0 + 14, bg);
                        if (s != null) Blit(buf, s, 14f, H - (y0 + 14) + 1f, 1, false);
                        int r = 2 + i * 2;
                        for (int dy = -r; dy <= r; dy++)
                            for (int dx = -r; dx <= r; dx++)
                                if (dx * dx + dy * dy <= r * r) Blend(buf, 14 + dx, y0 + 4 + dy, new Color32(255, 250, 200, 255), 0.25f + i * 0.2f);
                        for (int y = y0 + 4; y < y0 + 12; y += 3) Scribble(buf, 30, W - 4, y, rnd, new Color32(30, 30, 40, 255));
                        Line(buf, 0, y0 + 14, W - 1, y0 + 14, new Color32(20, 20, 20, 255));
                    }
                    break;
                default:
                {
                    Kingdom o = G.KingdomById(p.About) ?? G.Pick(G.Kingdoms().Where(x => x != k).ToList());
                    Color32 ca = k != null ? Flags.Main(k) : new Color32(90, 120, 200, 255);
                    Color32 cb = o != null ? Flags.Main(o) : new Color32(200, 90, 80, 255);
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                            buf[y * W + x] = x + y / 3 < W / 2 + 7 ? ca : cb;
                    StampFlag(buf, k != null ? FlagPixels(Flags.Code(k), k) : null, 12, 10, 4);
                    StampFlag(buf, o != null ? FlagPixels(Flags.Code(o), o) : null, W - 12, 10, 4);
                    if (s != null) Blit(buf, s, 20f, 4f, 2, false);
                    Actor b = o != null ? Internet.RandomUser(o) : null;
                    Sprite sb = G.Ok(b) ? ActorSprite(b) : null;
                    if (sb != null) Blit(buf, sb, W - 20f, 4f, 2, true);
                    Line(buf, W / 2 - 3, 14, W / 2 + 2, 22, Color.white);
                    Line(buf, W / 2 + 2, 22, W / 2 - 2, 24, Color.white);
                    Line(buf, W / 2 - 2, 24, W / 2 + 3, 32, Color.white);
                    break;
                }
            }
            for (int x = 0; x < W; x++)
            {
                Put(buf, x, 0, new Color32(20, 20, 20, 255));
                Put(buf, x, H - 1, new Color32(20, 20, 20, 255));
            }
        }

        private static float Lum(Color32 c)
        {
            return (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
        }

        private static void Filter(Color32[] buf, string f, System.Random rnd)
        {
            switch (f)
            {
                case "carving":
                {
                    var src = (Color32[])buf.Clone();
                    Color32 stone = new Color32(168, 150, 124, 255), cut = new Color32(84, 66, 52, 255);
                    for (int y = 0; y < H; y++)
                    {
                        for (int x = 0; x < W; x++)
                        {
                            float l = Lum(src[y * W + x]);
                            float r = x + 1 < W ? Lum(src[y * W + x + 1]) : l;
                            float d = y + 1 < H ? Lum(src[(y + 1) * W + x]) : l;
                            bool edge = Math.Abs(l - r) > 0.09f || Math.Abs(l - d) > 0.09f;
                            Color32 c = Shade(stone, 0.9f + ((Hash(x, y, 9) & 7) - 3) * 0.025f + l * 0.12f);
                            buf[y * W + x] = edge ? cut : c;
                        }
                    }
                    break;
                }
                case "illum":
                {
                    var src = (Color32[])buf.Clone();
                    Color32[] pal = { new Color32(60, 44, 34, 255), new Color32(150, 50, 40, 255), new Color32(50, 80, 140, 255), new Color32(80, 120, 60, 255), new Color32(200, 160, 70, 255), new Color32(226, 210, 170, 255), new Color32(140, 110, 80, 255) };
                    for (int i = 0; i < W * H; i++)
                    {
                        Color32 c = src[i], best = pal[0];
                        int bd = int.MaxValue;
                        foreach (Color32 q in pal)
                        {
                            int dd = (c.r - q.r) * (c.r - q.r) + (c.g - q.g) * (c.g - q.g) + (c.b - q.b) * (c.b - q.b);
                            if (dd < bd)
                            {
                                bd = dd;
                                best = q;
                            }
                        }
                        buf[i] = Mix(best, new Color32(232, 214, 172, 255), 0.18f);
                    }
                    for (int y = 0; y < H - 1; y++)
                        for (int x = 0; x < W - 1; x++)
                            if (!Same(src[y * W + x], src[y * W + x + 1]) || !Same(src[y * W + x], src[(y + 1) * W + x])) buf[y * W + x] = Mix(buf[y * W + x], pal[0], 0.55f);
                    break;
                }
                case "engraving":
                {
                    Color32 paper = new Color32(232, 222, 196, 255), ink = new Color32(52, 40, 34, 255);
                    for (int y = 0; y < H; y++)
                    {
                        for (int x = 0; x < W; x++)
                        {
                            float l = Lum(buf[y * W + x]);
                            bool dark = l < 0.25f || (l < 0.45f && (x + y) % 2 == 0) || (l < 0.65f && (x + y) % 3 == 0) || (l < 0.8f && (x - y) % 5 == 0 && x % 2 == 0);
                            buf[y * W + x] = dark ? ink : paper;
                        }
                    }
                    break;
                }
                case "sepia":
                    for (int i = 0; i < W * H; i++)
                    {
                        float l = Mathf.Clamp01(Lum(buf[i]) * 1.1f + ((Hash(i, 1, 2) & 7) - 3) * 0.015f);
                        buf[i] = new Color32((byte)(l * 235 + 20), (byte)(l * 200 + 18), (byte)(l * 150 + 14), 255);
                    }
                    Vignette(buf, 0.35f);
                    break;
                case "bw":
                    for (int i = 0; i < W * H; i++)
                    {
                        float l = Mathf.Clamp01((Lum(buf[i]) - 0.5f) * 1.3f + 0.5f + ((Hash(i, 5, 7) & 7) - 3) * 0.02f);
                        byte v = (byte)(l * 255);
                        buf[i] = new Color32(v, v, v, 255);
                    }
                    Vignette(buf, 0.3f);
                    break;
                case "faded":
                    for (int i = 0; i < W * H; i++)
                    {
                        Color32 c = buf[i];
                        float l = Lum(c) * 255f;
                        c = new Color32((byte)((c.r + l) / 2f), (byte)((c.g + l) / 2f), (byte)((c.b + l) / 2f), 255);
                        buf[i] = Mix(c, new Color32(240, 210, 190, 255), 0.18f);
                    }
                    for (int y = 0; y < H; y += 2) for (int x = 0; x < W; x++) if (Hash(x, y, 11) % 23 == 0) Put(buf, x, y, new Color32(250, 240, 230, 255));
                    break;
                case "selfie":
                    for (int i = 0; i < W * H; i++)
                    {
                        Color32 c = buf[i];
                        float l = Lum(c) * 255f;
                        c = new Color32((byte)Mathf.Clamp(l + (c.r - l) * 1.3f + 12, 0, 255), (byte)Mathf.Clamp(l + (c.g - l) * 1.3f + 4, 0, 255), (byte)Mathf.Clamp(l + (c.b - l) * 1.3f, 0, 255), 255);
                        buf[i] = c;
                    }
                    Vignette(buf, 0.25f);
                    for (int n = 0; n < 6; n++)
                    {
                        int x = rnd.Next(W), y = rnd.Next(H);
                        Put(buf, x, y, Color.white);
                        Put(buf, x - 1, y, new Color32(255, 240, 255, 255));
                        Put(buf, x + 1, y, new Color32(255, 240, 255, 255));
                        Put(buf, x, y - 1, new Color32(255, 240, 255, 255));
                        Put(buf, x, y + 1, new Color32(255, 240, 255, 255));
                    }
                    break;
                case "sat":
                    for (int i = 0; i < W * H; i++) buf[i] = Mix(buf[i], new Color32(60, 110, 70, 255), 0.25f);
                    for (int y = 0; y < H; y += 3) for (int x = 0; x < W; x++) buf[y * W + x] = Shade(buf[y * W + x], 0.88f);
                    for (int i = -5; i <= 5; i++)
                    {
                        if (Math.Abs(i) < 2) continue;
                        Put(buf, W / 2 + i, H / 2, new Color32(230, 60, 50, 255));
                        Put(buf, W / 2, H / 2 + i, new Color32(230, 60, 50, 255));
                    }
                    Corners(buf, new Color32(200, 255, 200, 255));
                    break;
                case "cctv":
                    for (int i = 0; i < W * H; i++)
                    {
                        float l = Lum(buf[i]);
                        buf[i] = new Color32((byte)(l * 150 + 20), (byte)(l * 210 + 30), (byte)(l * 150 + 20), 255);
                    }
                    for (int y = 1; y < H; y += 2) for (int x = 0; x < W; x++) buf[y * W + x] = Shade(buf[y * W + x], 0.82f);
                    Fill(buf, 3, 3, 5, 5, new Color32(240, 50, 40, 255));
                    Corners(buf, new Color32(220, 255, 220, 255));
                    break;
                default:
                    for (int i = 0; i < W * H; i++)
                    {
                        Color32 c = buf[i];
                        buf[i] = new Color32((byte)Mathf.Clamp((c.r - 128) * 1.06f + 130, 0, 255), (byte)Mathf.Clamp((c.g - 128) * 1.06f + 130, 0, 255), (byte)Mathf.Clamp((c.b - 128) * 1.06f + 128, 0, 255), 255);
                    }
                    break;
            }
        }

        private static bool Same(Color32 a, Color32 b)
        {
            return Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) < 60;
        }

        private static void Vignette(Color32[] buf, float amount)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - W / 2f) / (W / 2f), dy = (y - H / 2f) / (H / 2f);
                    float d = Mathf.Clamp01((dx * dx + dy * dy) - 0.45f);
                    buf[y * W + x] = Shade(buf[y * W + x], 1f - d * amount);
                }
            }
        }

        private static void Corners(Color32[] buf, Color32 c)
        {
            for (int i = 0; i < 5; i++)
            {
                Put(buf, 1 + i, 1, c);
                Put(buf, 1, 1 + i, c);
                Put(buf, W - 2 - i, 1, c);
                Put(buf, W - 2, 1 + i, c);
                Put(buf, 1 + i, H - 2, c);
                Put(buf, 1, H - 2 - i, c);
                Put(buf, W - 2 - i, H - 2, c);
                Put(buf, W - 2, H - 2 - i, c);
            }
        }
    }
}
