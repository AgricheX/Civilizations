using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class CosmosScale
    {
        public const string PowerId = "civ_cosmos_enter";
        public const string Void = "civ_space";
        public const string Star = "civ_space_star";
        public const string Nebula = "civ_space_nebula";
        public const string Dust = "civ_space_dust";

        private class Isle
        {
            public CNode Node;
            public int X, Y, R;
        }

        private class Glow
        {
            public SpriteRenderer Sr;
            public Sprite[] Frames;
            public float Phase;
        }

        private static readonly List<Isle> _isles = new List<Isle>();
        private static readonly HashSet<long> _armed = new HashSet<long>();
        private static readonly List<Glow> _glows = new List<Glow>();
        private static readonly Dictionary<string, Sprite[]> _frames = new Dictionary<string, Sprite[]>();
        private static GameObject _root;
        private static SpriteRenderer[] _border;
        private static Sprite[] _borderFrames;
        private static Color _camBg;
        private static bool _camSaved;
        private static float _clock;

        private static readonly string[] RegionBiomes = { "biome_crystal", "biome_flower", "biome_maple", "biome_lemon", "biome_mushroom", "biome_permafrost", "biome_infernal", "biome_cybertile", "biome_birch", "biome_savanna" };

        public static void InitAssets()
        {
            Tile(Void, "#050610");
            Tile(Star, "#EBEEFF");
            Tile(Nebula, "#34184A");
            Tile(Dust, "#262A42");
            if (AssetManager.powers.get(PowerId) == null)
            {
                AssetManager.powers.add(new GodPower
                {
                    id = PowerId,
                    name = PowerId,
                    path_icon = "ui/Icons/civ/btn/enter",
                    force_brush = "circ_0",
                    unselect_when_window = true,
                    show_tool_sizes = false,
                    hold_action = false,
                    click_action = Click
                });
            }
        }

        private static void Tile(string id, string hex)
        {
            if (AssetManager.tiles.get(id) != null) return;
            TileType t = AssetManager.tiles.clone(id, "deep_ocean");
            t.color_hex = hex;
            t.color = Toolbox.makeColor(hex);
            t.edge_color_hex = hex;
            t.edge_color = t.color;
            t.ocean = false;
            t.can_be_frozen = false;
            t.can_be_unfrozen = false;
            t.freeze_to_id = null;
            t.can_be_filled_with_ocean = false;
            t.explodable_by_ocean = false;
            t.used_in_generator = false;
            t.can_be_autotested = false;
            t.allowed_to_be_finger_copied = false;
            t.ignore_ocean_edge_rendering = true;
            t.water_fill_sound = null;
            t.sprites = null;
            AssetManager.tiles.loadSpritesForTile(t);
        }

        public static bool Scale(string path)
        {
            CNode n = path == null ? null : CosmosGen.Get(path);
            return n != null && n.Level >= 1;
        }

        public static bool CanOpen(CNode n)
        {
            if (!Cosmos.Ready || n == null || n.Level < 1 || n.Count <= 0 || Busy) return false;
            return CosmosPlanet.Active == null || CosmosPlanet.Active != n.Path && Scale(CosmosPlanet.Active);
        }

        public static bool Busy
        {
            get { return SmoothLoader._has_actions; }
        }

        public static string Up(string path)
        {
            CNode n = CosmosGen.Get(path);
            if (n == null || n.Level >= CosmosGen.Universe) return null;
            return CosmosGen.ParentPath(path);
        }

        public static void Clear()
        {
            _isles.Clear();
            _armed.Clear();
        }

        public static void Fx(bool on)
        {
            if (!on)
            {
                if (_root != null) UnityEngine.Object.Destroy(_root);
                _root = null;
                _border = null;
                _glows.Clear();
                Camera cam = Camera.main;
                if (_camSaved && cam != null) cam.backgroundColor = _camBg;
                _camSaved = false;
                return;
            }
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _glows.Clear();
            _root = new GameObject("CivScaleFx");
            Camera c = Camera.main;
            if (c != null)
            {
                if (!_camSaved) _camBg = c.backgroundColor;
                _camSaved = true;
                c.backgroundColor = new Color32(5, 6, 16, 255);
            }
            Border();
        }

        private static void Layer(SpriteRenderer sr, int offset)
        {
            SpriteRenderer night = null;
            if (WorldAgeEffects.instance != null) WorldAgeEffects.instance.dict_effects.TryGetValue("night", out night);
            if (night != null)
            {
                sr.sortingLayerID = night.sortingLayerID;
                sr.sortingOrder = night.sortingOrder + offset;
            }
            else sr.sortingOrder = 5000 + offset;
        }

        private static void Border()
        {
            if (_borderFrames == null)
            {
                var l = new List<Sprite>();
                for (int i = 0; i < 4; i++)
                {
                    Sprite s = SpriteTextureLoader.getSprite("civ/space/frame_" + i);
                    if (s == null) continue;
                    Texture2D tex = s.texture;
                    tex.wrapMode = TextureWrapMode.Repeat;
                    tex.filterMode = FilterMode.Point;
                    l.Add(Sprite.Create(tex, s.rect, new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect));
                }
                _borderFrames = l.ToArray();
            }
            if (_borderFrames.Length == 0) return;
            float w = MapBox.width, h = MapBox.height, e = Math.Max(2048f, Math.Max(w, h) * 3f);
            var rects = new[]
            {
                new Rect(-e, -e, e, h + 2 * e), new Rect(w, -e, e, h + 2 * e), new Rect(0, -e, w, e), new Rect(0, h, w, e)
            };
            _border = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("CivSpaceBorder" + i);
                go.transform.SetParent(_root.transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _borderFrames[0];
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = rects[i].size;
                Layer(sr, 4);
                go.transform.position = new Vector3(rects[i].center.x, rects[i].center.y, -7f);
                _border[i] = sr;
            }
        }

        private static Sprite[] Frames(string key)
        {
            Sprite[] f;
            if (_frames.TryGetValue(key, out f)) return f;
            var l = new List<Sprite>();
            for (int i = 0; i < 4; i++)
            {
                Sprite s = SpriteTextureLoader.getSprite("civ/space/" + key + "_" + i);
                if (s == null) break;
                s.texture.filterMode = FilterMode.Point;
                l.Add(Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), 1f));
            }
            _frames[key] = f = l.ToArray();
            return f;
        }

        private static void Place(string key, float x, float y, float size, int order)
        {
            Sprite[] f = Frames(key);
            if (f.Length == 0 || _root == null) return;
            var go = new GameObject("CivScale_" + key);
            go.transform.SetParent(_root.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = f[0];
            Layer(sr, order);
            go.transform.position = new Vector3(x, y, -6f);
            float k = size / Math.Max(1f, f[0].rect.width);
            go.transform.localScale = new Vector3(k, k, 1f);
            _glows.Add(new Glow { Sr = sr, Frames = f, Phase = UnityEngine.Random.value * 4f });
        }

        public static void Tick(float dt)
        {
            if (_root == null) return;
            _clock += dt;
            foreach (Glow g in _glows)
            {
                if (g.Sr == null || g.Frames.Length < 2) continue;
                g.Sr.sprite = g.Frames[(int)(_clock * 3f + g.Phase) % g.Frames.Length];
            }
            if (_border != null && _borderFrames != null && _borderFrames.Length > 1)
            {
                Sprite s = _borderFrames[(int)(_clock * 1.2f) % _borderFrames.Length];
                foreach (SpriteRenderer sr in _border) if (sr != null && sr.sprite != s) sr.sprite = s;
            }
        }

        private static float Noise(int x, int y, int seed)
        {
            double v = 0;
            double[][] o = { new[] { 0.021, 0.027, 1.0 }, new[] { 0.053, 0.047, 0.5 }, new[] { 0.11, 0.13, 0.25 }, new[] { 0.23, 0.19, 0.12 } };
            for (int i = 0; i < o.Length; i++)
            {
                double fx = o[i][0], fy = o[i][1], a = o[i][2];
                v += a * Math.Sin(x * fx + seed * (i + 1) + Math.Sin(y * fy * 0.7 + i)) * Math.Cos(y * fy - seed * 0.3 + Math.Cos(x * fx * 0.6));
            }
            return (float)(v / 1.87 + 0.5);
        }

        private static void Wipe()
        {
            foreach (Actor a in World.world.units.ToList()) if (a != null && a.isAlive()) a.dieSimpleNone();
            foreach (Building b in World.world.buildings.ToList()) if (b != null && !b.isRemoved()) b.startRemove();
        }

        public static void Shape(CNode n)
        {
            _isles.Clear();
            Wipe();
            Fx(true);
            int w = MapBox.width, h = MapBox.height;
            var grid = new WorldTile[w, h];
            foreach (WorldTile t in World.world.tiles_list) if (t != null && t.x < w && t.y < h) grid[t.x, t.y] = t;
            TileType tv = AssetManager.tiles.get(Void), ts = AssetManager.tiles.get(Star), tn = AssetManager.tiles.get(Nebula), td = AssetManager.tiles.get(Dust);
            if (tv == null) return;
            var r = new System.Random(n.Seed);
            float neb = n.Level == 1 ? 0.8f : n.Nebula ? 0.66f : 0.76f;
            foreach (WorldTile t in World.world.tiles_list)
            {
                if (t == null) continue;
                float v = Noise(t.x, t.y, n.Seed % 97);
                TileType pick = v > neb || v > neb - 0.08f && r.NextDouble() < (v - neb + 0.08f) / 0.08f ? tn : r.NextDouble() < 0.009 ? ts : tv;
                t.setTileTypes(pick ?? tv, null, true);
            }
            List<CNode> kids = CosmosGen.Children(n);
            int cx = w / 2, cy = h / 2, dim = Math.Min(w, h);
            if (n.Level == 1) System(n, kids, grid, w, h, cx, cy, dim, r, td);
            else Regions(n, kids, grid, w, h, dim, r, td, tn);
            World.world.resetRedrawTimer();
        }

        private static void System(CNode n, List<CNode> kids, WorldTile[,] grid, int w, int h, int cx, int cy, int dim, System.Random r, TileType td)
        {
            int sr = Math.Max(5, dim / 18);
            float flat = h / (float)Math.Max(1, w) * 1.05f;
            for (int i = 0; i < kids.Count; i++)
            {
                CNode k = kids[i];
                float orbit = sr + 10 + (dim / 2f - sr - 16) * (i + 1f) / (kids.Count + 0.5f);
                Ring(grid, w, h, cx, cy, orbit, orbit * flat, td, 2);
                double a = k.Y * Math.PI * 2;
                int x = cx + (int)(Math.Cos(a) * orbit), y = cy + (int)(Math.Sin(a) * orbit * flat);
                if (k.Sub == "asteroid")
                {
                    TileType hills = AssetManager.tiles.get("hills");
                    for (int j = 0; j < 160; j++)
                    {
                        double b = r.NextDouble() * Math.PI * 2;
                        float o = orbit + (float)(r.NextDouble() * 6 - 3);
                        Disc(grid, w, h, cx + (int)(Math.Cos(b) * o), cy + (int)(Math.Sin(b) * o * flat), r.NextDouble() < 0.3 ? 1 : 0, hills, "biome_rocklands");
                    }
                    Disc(grid, w, h, x, y, 4, AssetManager.tiles.get("soil_low"), "biome_rocklands");
                    _isles.Add(new Isle { Node = k, X = x, Y = y, R = 4 });
                    continue;
                }
                bool giant = k.Sub == "gas_giant";
                int rad = giant ? Math.Max(8, dim / 14) : Math.Max(6, dim / 22);
                if (giant) Ring(grid, w, h, x, y, rad + 3, (rad + 3) * 0.35f, td, 1);
                Planet(grid, w, h, x, y, rad, k);
                _isles.Add(new Isle { Node = k, X = x, Y = y, R = rad });
            }
            Place("star_" + (Frames("star_" + n.Sub).Length > 0 ? n.Sub : "G"), cx, cy, sr * 4.2f, 3);
        }

        private static void Planet(WorldTile[,] grid, int w, int h, int x, int y, int rad, CNode k)
        {
            string b = Biome(k);
            bool water = k.Sub == "ocean" || k.Sub == "reef" || k.Sub == "abyss";
            TileType low = AssetManager.tiles.get("soil_low"), high = AssetManager.tiles.get("soil_high"), shallow = AssetManager.tiles.get("shallow_waters");
            Disc(grid, w, h, x, y, rad, k.Sub == "gas_giant" ? high : low, b);
            if (water && shallow != null)
            {
                var rr = new System.Random(k.Seed);
                for (int i = 0; i < 4; i++) Disc(grid, w, h, x + rr.Next(-rad / 2, rad / 2 + 1), y + rr.Next(-rad / 2, rad / 2 + 1), Math.Max(1, rad / 3), shallow, null);
            }
            else if (k.Sub != "gas_giant") Disc(grid, w, h, x - rad / 4, y + rad / 4, Math.Max(1, rad / 3), high, b);
        }

        private static void Regions(CNode n, List<CNode> kids, WorldTile[,] grid, int w, int h, int dim, System.Random r, TileType td, TileType tn)
        {
            int rad = Math.Max(5, Math.Min(dim / 9, (int)(dim / (2.1 * Math.Sqrt(Math.Max(1, kids.Count))))));
            foreach (CNode k in kids)
            {
                int x = (int)(k.X * (w - 2 * rad)) + rad, y = (int)(k.Y * (h - 2 * rad)) + rad;
                for (int tries = 0; tries < 16 && _isles.Any(o => (o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y) < (o.R + rad + 5) * (o.R + rad + 5)); tries++)
                {
                    x = r.Next(rad + 2, Math.Max(rad + 3, w - rad - 2));
                    y = r.Next(rad + 2, Math.Max(rad + 3, h - rad - 2));
                }
                _isles.Add(new Isle { Node = k, X = x, Y = y, R = rad });
            }
            if (n.Level >= 5)
            {
                foreach (Isle a in _isles)
                {
                    foreach (Isle b in _isles.Where(o => o != a).OrderBy(o => (o.X - a.X) * (o.X - a.X) + (o.Y - a.Y) * (o.Y - a.Y)).Take(2))
                        Line(grid, w, h, a.X, a.Y, b.X, b.Y, n.Level >= 6 ? tn : td);
                }
            }
            TileType low = AssetManager.tiles.get("soil_low"), high = AssetManager.tiles.get("soil_high");
            foreach (Isle s in _isles)
            {
                string biome = RegionBiomes[Math.Abs(s.Node.Seed) % RegionBiomes.Length];
                if (s.Node.Level >= 5 || n.Level >= 5) Spiral(grid, w, h, s, low, high, biome, s.Node.Sub);
                else if (s.Node.Level == 1)
                {
                    Disc(grid, w, h, s.X, s.Y, s.R, low, biome);
                    Place("star_" + (Frames("star_" + s.Node.Sub).Length > 0 ? s.Node.Sub : "G"), s.X, s.Y, Math.Max(6f, s.R * 0.9f), 3);
                }
                else Cluster(grid, w, h, s, low, high, biome);
            }
        }

        private static void Cluster(WorldTile[,] grid, int w, int h, Isle s, TileType low, TileType high, string biome)
        {
            var r = new System.Random(s.Node.Seed);
            Disc(grid, w, h, s.X, s.Y, (int)(s.R * 0.7f), low, biome);
            Disc(grid, w, h, s.X, s.Y, Math.Max(1, s.R / 4), high, biome);
            for (int i = 0; i < 5; i++)
            {
                double a = r.NextDouble() * Math.PI * 2;
                float d = s.R * (0.6f + (float)r.NextDouble() * 0.4f);
                Disc(grid, w, h, s.X + (int)(Math.Cos(a) * d), s.Y + (int)(Math.Sin(a) * d), Math.Max(1, s.R / 5), low, biome);
            }
        }

        private static void Spiral(WorldTile[,] grid, int w, int h, Isle s, TileType low, TileType high, string biome, string kind)
        {
            Disc(grid, w, h, s.X, s.Y, Math.Max(2, s.R / 3), high, biome);
            if (kind == "elliptical" || kind == "dwarf")
            {
                Disc(grid, w, h, s.X, s.Y, (int)(s.R * (kind == "dwarf" ? 0.55f : 0.85f)), low, biome);
                Disc(grid, w, h, s.X, s.Y, Math.Max(2, s.R / 3), high, biome);
                return;
            }
            int arms = kind == "irregular" ? 3 : 2;
            var r = new System.Random(s.Node.Seed);
            for (int a = 0; a < arms; a++)
            {
                double off = a * Math.PI * 2 / arms + r.NextDouble() * 0.3;
                for (double t = 0; t < 3.4; t += 0.08)
                {
                    double rr = s.R * 0.25 + t / 3.4 * s.R * 0.8;
                    int x = s.X + (int)(Math.Cos(t * 1.6 + off) * rr), y = s.Y + (int)(Math.Sin(t * 1.6 + off) * rr * 0.8);
                    Disc(grid, w, h, x, y, Math.Max(1, (int)(s.R * 0.16 * (1.2 - t / 3.4))), low, biome);
                }
            }
        }

        private static void Ring(WorldTile[,] grid, int w, int h, int cx, int cy, float rx, float ry, TileType type, int dash)
        {
            if (type == null) return;
            int steps = (int)(Math.Max(rx, ry) * 7);
            for (int i = 0; i < steps; i++)
            {
                if (dash > 1 && i / 3 % dash == 1) continue;
                double a = i * Math.PI * 2 / steps;
                int x = cx + (int)Math.Round(Math.Cos(a) * rx), y = cy + (int)Math.Round(Math.Sin(a) * ry);
                if (x < 0 || y < 0 || x >= w || y >= h || grid[x, y] == null || grid[x, y].Type.ground) continue;
                grid[x, y].setTileTypes(type, null, true);
            }
        }

        private static string Biome(CNode k)
        {
            switch (k.Sub)
            {
                case "terran": return "biome_grass";
                case "jungle": return "biome_jungle";
                case "swamp": return "biome_swamp";
                case "arid": return "biome_desert";
                case "barren": return "biome_wasteland";
                case "volcanic": return "biome_infernal";
                case "crystal": return "biome_crystal";
                case "ice": return "biome_permafrost";
                case "gas_giant": return "biome_sand";
                case "fungal": return "biome_mushroom";
                case "machine": return "biome_cybertile";
                case "reef": return "biome_lemon";
                case "ocean": return "biome_lemon";
                case "abyss": return "biome_corrupted";
                default: return "biome_savanna";
            }
        }

        private static void Disc(WorldTile[,] grid, int w, int h, int cx, int cy, int rad, TileType type, string biome)
        {
            if (type == null) return;
            BiomeAsset b = biome == null ? null : AssetManager.biome_library.get(biome);
            for (int y = cy - rad; y <= cy + rad; y++)
            {
                for (int x = cx - rad; x <= cx + rad; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h || (x - cx) * (x - cx) + (y - cy) * (y - cy) > rad * rad + rad) continue;
                    WorldTile t = grid[x, y];
                    if (t == null) continue;
                    TopTileType top = null;
                    if (b != null && type.ground)
                    {
                        string topId = type.id.Contains("high") ? b.tile_high : b.tile_low;
                        top = string.IsNullOrEmpty(topId) ? null : AssetManager.top_tiles.get(topId);
                    }
                    t.setTileTypes(type, top, true);
                }
            }
        }

        private static void Line(WorldTile[,] grid, int w, int h, int x0, int y0, int x1, int y1, TileType type)
        {
            if (type == null) return;
            int n = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= n; i++)
            {
                int x = x0 + (x1 - x0) * i / Math.Max(1, n), y = y0 + (y1 - y0) * i / Math.Max(1, n);
                for (int d = -1; d <= 1; d++)
                {
                    int yy = y + d;
                    if (x < 0 || yy < 0 || x >= w || yy >= h || grid[x, yy] == null || grid[x, yy].Type.ground) continue;
                    grid[x, yy].setTileTypes(type, null, true);
                }
            }
        }

        public static Polity Holder(CNode k)
        {
            Polity p = Cosmos.OwnerExact(k.Path);
            if (p != null || Cosmos.St == null) return p;
            string pre = k.Path.Length == 0 ? "" : k.Path + ".";
            var count = new Dictionary<Polity, int>();
            foreach (Polity q in Cosmos.St.Polities)
            {
                int c = q.Nodes.Count(x => x.StartsWith(pre));
                if (c > 0) count[q] = c;
            }
            return count.Count == 0 ? Cosmos.Owner(k.Path) : count.OrderByDescending(kv => kv.Value).First().Key;
        }

        public static List<Polity> Present(string active)
        {
            var l = new List<Polity>();
            foreach (CNode k in CosmosGen.Children(CosmosGen.Get(active)))
            {
                Polity p = Holder(k);
                if (p != null && !l.Contains(p)) l.Add(p);
            }
            return l;
        }

        public static IEnumerable<KeyValuePair<Polity, WorldTile>> Seats(string active)
        {
            foreach (Isle s in _isles)
            {
                Polity p = Holder(s.Node);
                if (p == null) continue;
                WorldTile t = World.world.GetTile(s.X, s.Y);
                if (t != null) yield return new KeyValuePair<Polity, WorldTile>(p, t);
            }
        }

        public static void Arm(Kingdom k, Polity p)
        {
            if (!G.Ok(k) || p == null || !_armed.Add(p.Id)) return;
            City c = G.Cities(k).FirstOrDefault();
            if (c == null) return;
            var ships = Db.Units.Where(u => u.Cls == "space" && u.Era <= p.Era && AssetManager.actor_library.get(u.Id) != null).OrderByDescending(u => u.Era).Take(4).ToList();
            if (ships.Count == 0) return;
            int n = Mathf.Clamp(2 + (int)(p.Fleet / 40f), 2, 10);
            for (int i = 0; i < n; i++) Vehicles.SpawnFor(c, ships[i % ships.Count].Id);
        }

        private static Isle At(WorldTile t)
        {
            Isle best = null;
            float bd = float.MaxValue;
            foreach (Isle s in _isles)
            {
                float d = (s.X - t.x) * (s.X - t.x) + (s.Y - t.y) * (s.Y - t.y);
                if (d <= (s.R + 4) * (s.R + 4) && d < bd)
                {
                    bd = d;
                    best = s;
                }
            }
            return best;
        }

        private static bool Click(WorldTile tile, string power)
        {
            if (tile == null) return false;
            if (CosmosPlanet.Active == null || !Scale(CosmosPlanet.Active))
            {
                Notify.Show(Txt.T("cos_enter_outside"));
                return false;
            }
            if (Busy) return false;
            Isle s = At(tile);
            if (s == null)
            {
                Notify.Show(Txt.T("cos_enter_none"));
                return false;
            }
            CNode k = s.Node;
            if (k.Level == 0 && k.Home)
            {
                CosmosPlanet.Return();
                return true;
            }
            if (k.Level == 0 && (k.Sub == "gas_giant" || k.Sub == "asteroid"))
            {
                Notify.Show(Txt.F("cos_enter_gas", CosmosGen.Describe(k)));
                return false;
            }
            if (k.Level == 0 && (Cosmos.St.Dead.Contains(k.Path) || CosmosGen.Homes.Contains(k.Path)))
            {
                Notify.Show(Txt.T("cos_enter_closed"));
                return false;
            }
            CosmosPlanet.Open(k.Path);
            return true;
        }

        public static void Settle(string active)
        {
            if (CosmosGen.Get(active) == null || Cosmos.St == null) return;
            foreach (Isle s in _isles)
            {
                WorldTile t = World.world.GetTile(s.X, s.Y);
                if (t == null) continue;
                var votes = new Dictionary<long, int>();
                foreach (WorldTile x in t.getTilesAround(s.R))
                {
                    City c = x != null && x.zone != null ? x.zone.city : null;
                    if (!G.Ok(c) || !G.Ok(c.kingdom)) continue;
                    long pid;
                    if (!long.TryParse(Store.Str(c.kingdom.data, "civ_polity"), out pid)) continue;
                    int v;
                    votes.TryGetValue(pid, out v);
                    votes[pid] = v + 1;
                }
                if (votes.Count == 0) continue;
                Polity win = Cosmos.Get(votes.OrderByDescending(kv => kv.Value).First().Key);
                Polity old = Holder(s.Node);
                if (win == null || win == old) continue;
                string pre = s.Node.Path + ".";
                if (old != null)
                {
                    var moved = old.Nodes.Where(x => x == s.Node.Path || x.StartsWith(pre)).ToList();
                    foreach (string m in moved)
                    {
                        old.Nodes.Remove(m);
                        if (!win.Nodes.Contains(m)) win.Nodes.Add(m);
                    }
                    if (moved.Count == 0 && s.Node.Level == 0 && !win.Nodes.Contains(s.Node.Path)) win.Nodes.Add(s.Node.Path);
                }
                else if (!win.Nodes.Contains(s.Node.Path)) win.Nodes.Add(s.Node.Path);
                Cosmos.Log(Txt.F("cos_log_scale_taken", win.Name, s.Node.Name.Length > 0 ? s.Node.Name : CosmosGen.Describe(s.Node)));
            }
            Cosmos.Dirty();
        }
    }
}
