using System;
using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal class CosView
    {
        public Sprite Sprite;
        public readonly List<KeyValuePair<CNode, Vector2>> Spots = new List<KeyValuePair<CNode, Vector2>>();
    }

    internal static class CosmosArt
    {
        public const int W = 200;
        public const int H = 140;
        private static Color32[] _px;
        public static float Phase;

        private static readonly Dictionary<string, string> StarCol = new Dictionary<string, string>
        {
            { "O", "#9BB0FF" }, { "B", "#AABFFF" }, { "A", "#CAD7FF" }, { "F", "#F8F7E8" }, { "G", "#FFE9A0" }, { "K", "#FFC890" }, { "M", "#FF8A5A" },
            { "red_giant", "#FF5A3A" }, { "white_dwarf", "#E0E8FF" }, { "neutron", "#9AF0FF" }, { "black_hole", "#1A1020" }, { "binary", "#FFE0B0" }
        };

        private static readonly Dictionary<string, string> BodyCol = new Dictionary<string, string>
        {
            { "terran", "#4FA83A" }, { "ocean", "#3E9AE8" }, { "jungle", "#2E8A30" }, { "swamp", "#6A7A3A" }, { "arid", "#D8A85A" }, { "barren", "#9A928A" },
            { "volcanic", "#C8443A" }, { "crystal", "#B07AF0" }, { "ice", "#CFEFFF" }, { "gas_giant", "#E0B07A" }, { "reef", "#3AD0C0" }, { "abyss", "#2A3A7A" },
            { "fungal", "#C86AB0" }, { "machine", "#8AA0B0" }, { "asteroid", "#7A7068" }
        };

        public static Color32 Hex(string h)
        {
            Color c;
            if (string.IsNullOrEmpty(h) || !ColorUtility.TryParseHtmlString(h.StartsWith("#") ? h : "#" + h, out c)) c = Color.white;
            return c;
        }

        private static void Begin(Color32 bg, int seed)
        {
            if (_px == null) _px = new Color32[W * H];
            for (int i = 0; i < _px.Length; i++) _px[i] = bg;
            var r = new System.Random(seed);
            for (int i = 0; i < 90; i++)
            {
                int x = r.Next(0, W), y = r.Next(0, H);
                byte v = (byte)r.Next(70, 200);
                P(x, y, new Color32(v, v, (byte)Math.Min(255, v + 30), 255));
            }
        }

        private static Sprite End()
        {
            var flipped = new Color32[W * H];
            for (int y = 0; y < H; y++) Array.Copy(_px, y * W, flipped, (H - 1 - y) * W, W);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(flipped);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
        }

        private static void P(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            _px[y * W + x] = c;
        }

        private static void Blend(int x, int y, Color32 c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Color32 o = _px[y * W + x];
            _px[y * W + x] = Color32.Lerp(o, c, a);
        }

        private static void Disc(float cx, float cy, float r, Color32 c)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r + 0.3f) P(x, y, c);
        }

        private static void Ring(float cx, float cy, float rx, float ry, Color32 c, float a = 1f)
        {
            int steps = Math.Max(24, (int)(rx * 6));
            for (int i = 0; i < steps; i++)
            {
                double t = i * Math.PI * 2 / steps;
                int x = (int)Math.Round(cx + Math.Cos(t) * rx), y = (int)Math.Round(cy + Math.Sin(t) * ry);
                if (a >= 1f) P(x, y, c);
                else Blend(x, y, c, a);
            }
        }

        private static void Line(float x0, float y0, float x1, float y1, Color32 c, float a = 1f)
        {
            int n = (int)Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= n; i++)
            {
                float t = n == 0 ? 0 : (float)i / n;
                int x = (int)Math.Round(x0 + (x1 - x0) * t), y = (int)Math.Round(y0 + (y1 - y0) * t);
                if (a >= 1f) P(x, y, c);
                else Blend(x, y, c, a);
            }
        }

        private static void Glow(float cx, float cy, float r, Color32 c, float a)
        {
            for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                {
                    float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d <= r) Blend(x, y, c, a * (1f - d / r));
                }
        }

        private static void Box(int x, int y, int s, Color32 c, Color32 edge, bool owned)
        {
            for (int yy = y; yy < y + s; yy++)
                for (int xx = x; xx < x + s; xx++)
                {
                    bool border = xx == x || yy == y || xx == x + s - 1 || yy == y + s - 1;
                    P(xx, yy, border ? (owned ? edge : Color32.Lerp(c, new Color32(0, 0, 0, 255), 0.4f)) : c);
                }
        }

        private static Polity OwnerOf(CNode n)
        {
            Polity p = Cosmos.OwnerExact(n.Path);
            if (p != null) return p;
            if (Cosmos.St == null) return null;
            Polity best = null;
            int count = 0;
            var tally = new Dictionary<long, int>();
            foreach (Polity q in Cosmos.St.Polities)
            {
                foreach (string node in q.Nodes)
                {
                    if (!CosmosGen.Contains(n.Path, node)) continue;
                    int c;
                    tally.TryGetValue(q.Id, out c);
                    tally[q.Id] = ++c;
                    if (c > count)
                    {
                        count = c;
                        best = q;
                    }
                }
            }
            return best;
        }

        public static CosView Render(CNode n)
        {
            var v = new CosView();
            if (n == null) return v;
            switch (n.Level)
            {
                case 1: SystemView(n, v); break;
                case 0: BodyView(n, v); break;
                case 5: GalaxyView(n, v); break;
                case 2:
                case 3:
                case 4: FieldView(n, v); break;
                default: DeepView(n, v); break;
            }
            Voyages(n, v);
            Marks(n, v);
            v.Sprite = End();
            return v;
        }

        private static void SystemView(CNode n, CosView v)
        {
            Begin(new Color32(8, 10, 22, 255), n.Seed);
            float sx = 16, sy = H / 2f;
            string col;
            Color32 sc = Hex(StarCol.TryGetValue(n.Sub, out col) ? col : "#FFE9A0");
            if (n.Sub == "black_hole")
            {
                Glow(sx, sy, 16, Hex("#FF9A2A"), 0.6f);
                Ring(sx, sy, 9, 4, Hex("#FFB050"));
                Disc(sx, sy, 6, new Color32(0, 0, 0, 255));
            }
            else
            {
                Glow(sx, sy, 22, sc, 0.35f);
                Disc(sx, sy, n.Sub == "red_giant" ? 12 : n.Sub == "white_dwarf" || n.Sub == "neutron" ? 3 : 8, sc);
                if (n.Sub == "binary") Disc(sx + 6, sy - 9, 4, Hex("#FF9A6A"));
            }
            List<CNode> kids = CosmosGen.Children(n);
            float step = kids.Count > 0 ? (W - 34f) / kids.Count : 20f;
            for (int i = 0; i < kids.Count; i++)
            {
                CNode b = kids[i];
                float rx = 18 + step * (i + 0.8f), ry = rx * 0.42f;
                Ring(sx, sy, rx, ry, new Color32(70, 80, 120, 255), 0.45f);
                double ang = b.Y * Math.PI * 2 + Phase * (0.6 / (1 + i));
                float bx = sx + (float)Math.Cos(ang) * rx, by = sy + (float)Math.Sin(ang) * ry;
                bx = Mathf.Clamp(bx, 4, W - 5);
                by = Mathf.Clamp(by, 4, H - 5);
                float r = b.Sub == "gas_giant" ? 4.5f : b.Sub == "asteroid" ? 1.5f : 2.5f;
                Color32 bc = Hex(BodyCol.TryGetValue(b.Sub, out col) ? col : "#888888");
                Polity owner = Cosmos.OwnerExact(b.Path);
                if (owner != null) Ring(bx, by, r + 2, r + 2, Hex(owner.Color));
                if (b.Sub == "asteroid")
                {
                    var rr = new System.Random(b.Seed);
                    for (int k = 0; k < 8; k++) P((int)(bx + rr.Next(-4, 5)), (int)(by + rr.Next(-3, 4)), bc);
                }
                else Disc(bx, by, r, bc);
                if (b.Sub == "gas_giant" && b.Index % 2 == 1) Ring(bx, by, r + 3, 1.2f, new Color32(210, 190, 150, 255));
                if (b.Home) P((int)bx, (int)(by - r - 2), new Color32(255, 230, 90, 255));
                v.Spots.Add(new KeyValuePair<CNode, Vector2>(b, new Vector2(bx, by)));
            }
        }

        private static void BodyView(CNode n, CosView v)
        {
            Begin(new Color32(8, 10, 22, 255), n.Seed);
            string col;
            Color32 c = Hex(BodyCol.TryGetValue(n.Sub, out col) ? col : "#888888");
            float cx = W / 2f, cy = H / 2f, r = n.Sub == "gas_giant" ? 52 : n.Sub == "asteroid" ? 22 : 42;
            var rnd = new System.Random(n.Seed);
            for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > r * r) continue;
                    float shade = 0.75f + 0.25f * (-(dx + dy) / (r * 1.4f));
                    float noise = (float)(Math.Sin(x * 0.31 + n.Seed) * Math.Cos(y * 0.27 + n.Seed * 0.5)) * 0.12f;
                    if (n.Sub == "gas_giant") noise = (float)Math.Sin(y * 0.45 + n.Seed) * 0.15f;
                    Color32 k = Color32.Lerp(new Color32(0, 0, 0, 255), c, Mathf.Clamp01(shade + noise));
                    if ((n.Sub == "terran" || n.Sub == "ocean" || n.Sub == "jungle") && Math.Sin(x * 0.18 + n.Seed) + Math.Cos(y * 0.21 - n.Seed) > (n.Sub == "ocean" ? 1.1 : 0.4)) k = Color32.Lerp(k, Hex("#3E7AD8"), 0.8f);
                    P(x, y, k);
                }
            Polity owner = Cosmos.OwnerExact(n.Path);
            if (owner != null)
            {
                Ring(cx, cy, r + 3, r + 3, Hex(owner.Color));
                for (int i = 0; i < (int)Math.Min(40, 4 + owner.Pop * 4); i++)
                {
                    double a = rnd.NextDouble() * Math.PI * 2, d = rnd.NextDouble() * r * 0.8;
                    P((int)(cx + Math.Cos(a) * d), (int)(cy + Math.Sin(a) * d), new Color32(255, 230, 140, 255));
                }
            }
        }

        private static void FieldView(CNode n, CosView v)
        {
            Begin(new Color32(6, 8, 18, 255), n.Seed);
            var r = new System.Random(n.Seed);
            if (n.Nebula || n.Level == 3)
            {
                Color32 neb = Hex(new[] { "#7A3AA0", "#3A6AB0", "#B04A6A", "#3AA08A" }[r.Next(0, 4)]);
                for (int i = 0; i < 6; i++) Glow(r.Next(20, W - 20), r.Next(20, H - 20), r.Next(18, 42), neb, 0.25f);
            }
            foreach (CNode c in CosmosGen.Children(n))
            {
                float x = c.X * (W - 12) + 6, y = c.Y * (H - 12) + 6;
                Polity owner = OwnerOf(c);
                string col;
                Color32 fill = n.Level == 2 ? Hex(StarCol.TryGetValue(c.Sub, out col) ? col : "#FFE9A0") : n.Level == 3 ? Hex("#A0B8FF") : Hex("#C8B8FF");
                int s = n.Level == 2 ? 6 : n.Level == 3 ? 8 : 10;
                if (n.Level == 4)
                {
                    var cr = new System.Random(c.Seed);
                    for (int k = 0; k < 14; k++) P((int)(x + cr.Next(-6, 7)), (int)(y + cr.Next(-6, 7)), Color32.Lerp(fill, new Color32(255, 255, 255, 255), (float)cr.NextDouble() * 0.5f));
                }
                Box((int)x - s / 2, (int)y - s / 2, s, owner != null ? Color32.Lerp(fill, Hex(owner.Color), 0.55f) : Color32.Lerp(fill, new Color32(0, 0, 0, 255), 0.35f), owner != null ? Hex(owner.Color) : fill, owner != null);
                if (CosmosGen.Contains(c.Path, Cosmos.St != null ? Cosmos.St.Home : "")) P((int)x, (int)y - s / 2 - 2, new Color32(255, 230, 90, 255));
                v.Spots.Add(new KeyValuePair<CNode, Vector2>(c, new Vector2(x, y)));
            }
        }

        private static void GalaxyView(CNode n, CosView v)
        {
            Begin(new Color32(4, 5, 14, 255), n.Seed);
            float cx = W / 2f, cy = H / 2f;
            var r = new System.Random(n.Seed);
            bool spiral = n.Sub == "spiral" || n.Sub == "barred";
            Glow(cx, cy, 26, Hex("#FFE0A0"), 0.7f);
            List<CNode> kids = CosmosGen.Children(n);
            for (int i = 0; i < 2600; i++)
            {
                double t = r.NextDouble();
                double arm = r.Next(0, 2) * Math.PI;
                double ang = spiral ? arm + t * 4.2 + r.NextDouble() * 0.35 : r.NextDouble() * Math.PI * 2;
                double rad = spiral ? 6 + t * 62 : r.NextDouble() * 50 * (n.Sub == "dwarf" ? 0.6 : 1);
                int x = (int)(cx + Math.Cos(ang) * rad * 1.35), y = (int)(cy + Math.Sin(ang) * rad * 0.8);
                Blend(x, y, Hex(t < 0.2 ? "#FFE8B0" : "#B8C8FF"), 0.55f);
            }
            for (int i = 0; i < kids.Count; i++)
            {
                CNode a = kids[i];
                double t = (i + 0.5) / kids.Count;
                double ang = spiral ? (i % 2) * Math.PI + t * 4.2 : t * Math.PI * 2;
                double rad = spiral ? 8 + t * 58 : 20 + (i % 3) * 12;
                if (a.Name.Contains("Bulge") || a.Name.Contains("Core")) rad = 0;
                float x = (float)(cx + Math.Cos(ang) * rad * 1.35), y = (float)(cy + Math.Sin(ang) * rad * 0.8);
                Polity owner = OwnerOf(a);
                if (owner != null) Glow(x, y, 12, Hex(owner.Color), 0.55f);
                Disc(x, y, 1.5f, owner != null ? Hex(owner.Color) : new Color32(230, 230, 255, 255));
                if (CosmosGen.Contains(a.Path, Cosmos.St != null ? Cosmos.St.Home : "")) P((int)x, (int)y - 4, new Color32(255, 230, 90, 255));
                v.Spots.Add(new KeyValuePair<CNode, Vector2>(a, new Vector2(x, y)));
            }
        }

        private static void DeepView(CNode n, CosView v)
        {
            Begin(new Color32(3, 3, 10, 255), n.Seed);
            List<CNode> kids = CosmosGen.Children(n);
            var pos = new List<Vector2>();
            foreach (CNode c in kids) pos.Add(new Vector2(c.X * (W - 16) + 8, c.Y * (H - 16) + 8));
            if (n.Level >= 8)
            {
                for (int i = 0; i < pos.Count; i++)
                {
                    int best = -1;
                    float bd = float.MaxValue;
                    for (int j = 0; j < pos.Count; j++)
                    {
                        if (j == i) continue;
                        float d = (pos[i] - pos[j]).sqrMagnitude;
                        if (d < bd)
                        {
                            bd = d;
                            best = j;
                        }
                    }
                    if (best >= 0) Line(pos[i].x, pos[i].y, pos[best].x, pos[best].y, Hex("#5A4A8A"), 0.6f);
                    if (i + 1 < pos.Count && i % 3 == 0) Line(pos[i].x, pos[i].y, pos[i + 1].x, pos[i + 1].y, Hex("#3A3A6A"), 0.5f);
                }
            }
            for (int i = 0; i < kids.Count; i++)
            {
                CNode c = kids[i];
                Vector2 p = pos[i];
                Polity owner = OwnerOf(c);
                Color32 col = owner != null ? Hex(owner.Color) : Hex(n.Level == 6 ? "#D8D0FF" : "#B0A8E8");
                if (n.Level == 6)
                {
                    if (c.Sub == "elliptical" || c.Sub == "dwarf") Glow(p.x, p.y, c.Sub == "dwarf" ? 4 : 7, col, 0.9f);
                    else
                    {
                        Glow(p.x, p.y, 6, col, 0.7f);
                        Ring(p.x, p.y, 7, 3, col, 0.7f);
                    }
                }
                else
                {
                    var cr = new System.Random(c.Seed);
                    int count = n.Level == 9 ? 10 : 7;
                    for (int k = 0; k < count; k++) Glow(p.x + cr.Next(-6, 7), p.y + cr.Next(-5, 6), 2.5f, col, 0.8f);
                }
                if (CosmosGen.Contains(c.Path, Cosmos.St != null ? Cosmos.St.Home : "")) P((int)p.x, (int)p.y - 8, new Color32(255, 230, 90, 255));
                v.Spots.Add(new KeyValuePair<CNode, Vector2>(c, p));
            }
        }

        private static void Marks(CNode n, CosView v)
        {
            if (Cosmos.St == null) return;
            foreach (Mega m in Cosmos.St.Megas)
            {
                if (!CosmosGen.Contains(n.Path, m.Node) && m.Node != n.Path) continue;
                Kingdom k = Cosmos.KingOf(m.King);
                Color32 c = m.Ruin ? new Color32(130, 120, 110, 255) : G.Ok(k) ? Hex(G.Hex(k)) : new Color32(255, 220, 120, 255);
                float a = m.Done ? 0.9f : m.Ruin ? 0.45f : 0.35f + 0.5f * m.Progress;
                if (n.Level == 1 && (m.Id.Contains("dyson") || m.Id == "matrioshka_brain" || m.Id == "stellar_engine" || m.Id == "ringworld"))
                {
                    float r = m.Id == "dyson_sphere" || m.Id == "matrioshka_brain" ? 13f : m.Id == "ringworld" ? 17f : 11f;
                    Ring(16, H / 2f, r, m.Id == "ringworld" ? r * 0.35f : r, c, a);
                    if (m.Id == "dyson_swarm") for (int i = 0; i < 18; i++) Blend((int)(16 + Math.Cos(i * 0.35) * r), (int)(H / 2f + Math.Sin(i * 0.35) * r), c, a);
                    continue;
                }
                if (n.Level == 0 && m.Id == "orbital_ring" && m.Node == n.Path)
                {
                    Ring(W / 2f, H / 2f, 50, 8, c, a);
                    continue;
                }
                Vector2? p = SpotFor(v, m.Node);
                if (!p.HasValue) continue;
                Ring(p.Value.x, p.Value.y, 5, 5, c, a);
                Ring(p.Value.x, p.Value.y, 3, 3, new Color32(255, 255, 255, 255), a * 0.6f);
            }
            foreach (Battle b in Cosmos.St.Battles)
            {
                if (Cosmos.Now - b.Year > 3 || !CosmosGen.Contains(n.Path, b.Node)) continue;
                Vector2? p = SpotFor(v, b.Node);
                if (!p.HasValue) continue;
                var r = new System.Random(b.Year ^ b.Node.GetHashCode());
                for (int i = 0; i < 6; i++)
                {
                    float x = p.Value.x + r.Next(-6, 7), y = p.Value.y + r.Next(-6, 7);
                    Glow(x, y, 3, new Color32(255, (byte)r.Next(80, 200), 40, 255), 0.8f);
                    Line(x, y, x + r.Next(-8, 9), y + r.Next(-8, 9), new Color32(255, 80, 80, 255), 0.7f);
                }
            }
            foreach (string dead in Cosmos.St.Dead)
            {
                if (!CosmosGen.Contains(n.Path, dead)) continue;
                Vector2? p = SpotFor(v, dead);
                if (p.HasValue) Line(p.Value.x - 3, p.Value.y - 3, p.Value.x + 3, p.Value.y + 3, new Color32(120, 120, 120, 255));
            }
        }

        private static Vector2? SpotFor(CosView v, string path)
        {
            foreach (var kv in v.Spots) if (CosmosGen.Contains(kv.Key.Path, path)) return kv.Value;
            return null;
        }

        private static void Voyages(CNode n, CosView v)
        {
            if (Cosmos.St == null || n.Level == 0) return;
            foreach (Voyage x in Cosmos.St.Voyages)
            {
                if (!CosmosGen.Contains(n.Path, x.From) && !CosmosGen.Contains(n.Path, x.To)) continue;
                Vector2? a = SpotFor(v, x.From), b = SpotFor(v, x.To);
                if (!a.HasValue || !b.HasValue) continue;
                float t = x.Arrive <= x.Depart ? 1f : Mathf.Clamp01((Cosmos.Now - x.Depart) / (float)(x.Arrive - x.Depart));
                Vector2 p = Vector2.Lerp(a.Value, b.Value, t);
                Line(a.Value.x, a.Value.y, b.Value.x, b.Value.y, new Color32(255, 255, 255, 255), 0.12f);
                Polity owner = x.Polity != 0 ? Cosmos.Get(x.Polity) : null;
                Kingdom k = Cosmos.KingOf(x.King);
                Color32 c = x.Kind == "invasion" ? Hex("#FF4A3A") : owner != null ? Hex(owner.Color) : G.Ok(k) ? Hex(G.Hex(k)) : new Color32(255, 255, 255, 255);
                P((int)p.x, (int)p.y, c);
                P((int)p.x - 1, (int)p.y, c);
                P((int)p.x, (int)p.y - 1, new Color32(255, 255, 255, 255));
            }
        }
    }
}
