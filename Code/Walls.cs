using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class Walls
    {
        public const string KLastWar = "civ_last_war";
        public const string KBorder = "civ_border_walls";
        private const string KTower = "civ_wall_tower";
        private static readonly string[] TierLook = { "wall_wild", "wall_order", "wall_order", "wall_ancient", "wall_iron" };
        private static readonly int[] TierStrength = { 1, 2, 3, 3, 4 };
        private static TopTileType[] _tiers;
        private static TopTileType _border;
        private static TopTileType _damaged;
        private static TopTileType _trench;
        private static readonly HashSet<TopTileType> _ours = new HashSet<TopTileType>();
        private static readonly Dictionary<City, List<WorldTile>> _cache = new Dictionary<City, List<WorldTile>>();
        private static readonly Dictionary<WorldTile, int> _hp = new Dictionary<WorldTile, int>();
        private static float _tick;

        public static void InitAssets()
        {
            if (AssetManager.top_tiles.get("wall_order") == null) return;
            _tiers = new TopTileType[5];
            for (int i = 0; i < 5; i++) _tiers[i] = Tile("civ_wallt_" + i, TierLook[i], null, TierStrength[i], i == 0);
            _border = Tile("civ_wallt_border", "wall_iron", null, 4, false);
            _damaged = Tile("civ_wallt_damaged", "wall_order", "#7A6F64", 1, false);
            TopTileType road = TopTileLibrary.road ?? AssetManager.top_tiles.get("road");
            if (road != null)
            {
                _trench = AssetManager.top_tiles.get("civ_trench");
                if (_trench == null)
                {
                    _trench = AssetManager.top_tiles.clone("civ_trench", road.id);
                    _trench.color_hex = "#4E3B2A";
                    _trench.color = Toolbox.makeColor(_trench.color_hex);
                    _trench.walk_multiplier = 0.55f;
                    _trench.road = false;
                    _trench.can_be_set_on_fire = false;
                }
                if (_trench.sprites == null) _trench.sprites = road.sprites ?? WallArt.Flat("civ_trench", "#4E3B2A", "#3A2C1F");
                _ours.Add(_trench);
            }
        }

        private static readonly Dictionary<TopTileType, string> _look = new Dictionary<TopTileType, string>();

        private static TopTileType Tile(string id, string look, string color, int strength, bool wood)
        {
            TopTileType src = AssetManager.top_tiles.get(look) ?? AssetManager.top_tiles.get("wall_order");
            TopTileType t = AssetManager.top_tiles.get(id);
            if (t == null)
            {
                t = AssetManager.top_tiles.clone(id, src.id);
                if (color != null)
                {
                    t.color_hex = color;
                    t.color = Toolbox.makeColor(color);
                }
                t.strength = strength;
                t.burnable = wood;
                t.can_be_set_on_fire = wood;
                t.can_be_removed_with_pickaxe = true;
                t.can_be_removed_with_axe = wood;
            }
            if (t.sprites == null) t.sprites = src.sprites ?? WallArt.Flat(id, src.color_hex ?? "#A1B1A2", src.edge_color_hex ?? "#787F87");
            _look[t] = src.id;
            _ours.Add(t);
            return t;
        }

        public static TopTileType[] Drawn()
        {
            var list = new List<TopTileType>();
            if (_tiers != null) list.AddRange(_tiers);
            if (_border != null) list.Add(_border);
            if (_damaged != null) list.Add(_damaged);
            return list.ToArray();
        }

        public static string Look(TopTileType t)
        {
            string l;
            return t != null && _look.TryGetValue(t, out l) ? l : "wall_order";
        }

        public static bool IsWall(WorldTile t)
        {
            return t != null && t.top_type != null && _ours.Contains(t.top_type) && t.top_type != _trench;
        }

        private static bool IsOurs(WorldTile t)
        {
            return t != null && t.top_type != null && _ours.Contains(t.top_type);
        }

        public static int Level(Kingdom k)
        {
            return Research.Level(k, "walls:");
        }

        public static bool ModernPeace(Kingdom k)
        {
            if (Research.Era(k) < 10) return false;
            int last = Store.Int(k.data, KLastWar, -9999);
            if (World.world.wars.hasWars(k)) return false;
            string ideo = Policies.Id(k, "ideo");
            if (ideo == "militarism") return G.Year - last > 80;
            return G.Year - last > 30;
        }

        public static bool WantsFortress(City c, BuildingAsset b)
        {
            if (!G.Ok(c) || c.kingdom == null) return false;
            Kingdom k = c.kingdom;
            if (b.id == "civ_fortress") return !ModernPeace(k) && Research.Era(k) < 10 && (c.isCapitalCity() || Threat(c) > 0.3f);
            return true;
        }

        public static float Threat(City c)
        {
            Kingdom k = c.kingdom;
            float t = 0f;
            if (World.world.wars.hasWars(k)) t += 0.6f;
            int last = Store.Int(k.data, KLastWar, -9999);
            if (G.Year - last < 20) t += 0.3f;
            foreach (City n in c.neighbours_cities)
            {
                if (n == null || n.kingdom == null || n.kingdom == k) continue;
                if (n.kingdom.getSpecies() != k.getSpecies()) t += 0.1f;
                if (World.world.wars.isInWarWith(k, n.kingdom)) t += 0.3f;
            }
            if (Policies.Id(k, "ideo") == "militarism") t += 0.2f;
            if (Politics.StageIndex(k) <= 1) t *= 0.6f;
            return Math.Min(1f, t);
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (World.world.wars.hasWars(k)) Store.Put(k.data, KLastWar, G.Year);
            BorderWalls(k, yrs);
        }

        private static int MaxHp(WorldTile t)
        {
            if (t.top_type == _border) return 60;
            if (t.top_type == _damaged) return 8;
            for (int i = 0; i < 5; i++) if (_tiers != null && t.top_type == _tiers[i]) return 14 + i * 8;
            return 10;
        }

        private static string[][] Costs =
        {
            new[] { "wood:1" }, new[] { "stone:1", "wood:2" }, new[] { "stone:2" }, new[] { "civ_bricks:1", "stone:3" }, new[] { "civ_concrete:1", "civ_steel:1", "stone:4" }
        };

        private static bool Pay(City c, int tier)
        {
            foreach (string opt in Costs[Math.Max(0, Math.Min(4, tier))])
            {
                int i = opt.IndexOf(':');
                string res = opt.Substring(0, i);
                int n = int.Parse(opt.Substring(i + 1));
                if (Economy.Has(c, res, n + (res == "stone" || res == "wood" ? 20 : 0))) return Economy.Take(c, res, n);
            }
            return false;
        }

        private static readonly Dictionary<City, HashSet<WorldTile>> _gates = new Dictionary<City, HashSet<WorldTile>>();

        private static bool Road(WorldTile t)
        {
            return t != null && (t.Type != null && t.Type.road || t.top_type == Infrastructure.RailBed);
        }

        private static bool Gate(City c, WorldTile t)
        {
            if (Road(t)) return true;
            HashSet<WorldTile> g;
            return _gates.TryGetValue(c, out g) && g.Contains(t);
        }

        private static HashSet<WorldTile> Gates(City c, List<WorldTile> perim)
        {
            var set = new HashSet<WorldTile>();
            WorldTile center = c.getTile();
            if (center == null || perim.Count == 0) return set;
            var perimSet = new HashSet<WorldTile>(perim);
            int cx = center.x, cy = center.y;
            int[][] dirs = { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } };
            foreach (int[] d in dirs)
            {
                WorldTile best = null;
                int bestOff = int.MaxValue;
                bool hasRoad = false;
                foreach (WorldTile t in perim)
                {
                    int dx = t.x - cx, dy = t.y - cy;
                    int along = d[0] != 0 ? dx * d[0] : dy * d[1];
                    int side = d[0] != 0 ? Math.Abs(dy) : Math.Abs(dx);
                    if (along <= 0 || side > along) continue;
                    if (Road(t)) { hasRoad = true; break; }
                    if (side < bestOff)
                    {
                        bestOff = side;
                        best = t;
                    }
                }
                if (hasRoad || best == null) continue;
                set.Add(best);
                foreach (WorldTile n in best.neighbours)
                {
                    if (n == null || !perimSet.Contains(n)) continue;
                    bool across = d[0] != 0 ? n.x == best.x : n.y == best.y;
                    if (across)
                    {
                        set.Add(n);
                        break;
                    }
                }
            }
            return set;
        }

        private static List<WorldTile> Perimeter(City c)
        {
            var list = new List<WorldTile>();
            var zones = new HashSet<TileZone>(c.zones);
            foreach (TileZone z in c.zones.ToList())
            {
                bool edge = false;
                foreach (TileZone n in z.neighbours) if (n != null && !zones.Contains(n)) edge = true;
                if (!edge) continue;
                foreach (WorldTile t in z.tiles.ToList())
                {
                    if (t == null || t.Type == null || t.Type.liquid) continue;
                    if (t.Type.block && !IsOurs(t)) continue;
                    if (t.main_type != null && (t.main_type.mountains || t.main_type.rocks)) continue;
                    bool border = false;
                    foreach (WorldTile n in t.neighbours)
                    {
                        if (n != null && n.zone != null && !zones.Contains(n.zone))
                        {
                            border = true;
                            break;
                        }
                    }
                    if (border) list.Add(t);
                }
            }
            return list;
        }

        private static bool NearBuilding(WorldTile t)
        {
            foreach (WorldTile n in t.neighbours)
            {
                if (n == null) continue;
                if (n.hasBuilding()) return true;
                foreach (WorldTile m in n.neighbours) if (m != null && m.hasBuilding()) return true;
            }
            return false;
        }

        private static void Place(WorldTile t, TopTileType type)
        {
            MapAction.terraformTop(t, type, AssetManager.terraform.get("road"));
            _hp.Remove(t);
        }

        private static void Remove(WorldTile t)
        {
            _hp.Remove(t);
            MapAction.decreaseTile(t, false, "flash");
        }

        private static void Migrate(City c)
        {
            foreach (Building b in c.buildings.ToList())
            {
                if (b == null || b.asset == null || b.asset.type != "type_civ_wall") continue;
                b.startDestroyBuilding();
            }
        }

        public static void CityYear(City c, int yrs)
        {
            Kingdom k = c.kingdom;
            if (k == null || !k.isCiv() || _tiers == null) return;
            Migrate(c);
            List<WorldTile> perim = Perimeter(c);
            var perimSet = new HashSet<WorldTile>(perim);
            _gates[c] = Gates(c, perim);
            Cleanup(c, perimSet);
            if (ModernPeace(k))
            {
                Demolish(c, yrs, false);
                _cache[c] = perim.Where(IsWall).ToList();
                return;
            }
            int level = Level(k);
            if (level < 0)
            {
                _cache[c] = perim.Where(IsWall).ToList();
                return;
            }
            int tier = Math.Min(level, 4);
            bool modern = Research.Era(k) >= 11;
            bool war = World.world.wars.hasWars(k);
            if (Threat(c) < 0.45f || c.zones.Count < 9)
            {
                Repair(c, perim, yrs);
                _cache[c] = perim.Where(IsWall).ToList();
                return;
            }
            int budget = (int)((3 + Roles.Count(c, Roles.Engineer) * 3 + Roles.Count(c, Roles.Sapper) * 2) * yrs * (0.5f + Threat(c)));
            TopTileType want = modern ? _trench : _tiers[tier];
            if (modern && !war) want = _tiers[4];
            int built = 0;
            foreach (WorldTile t in perim)
            {
                if (budget <= 0) break;
                if (t.hasBuilding() || t.top_type == want || t.top_type == _border || want != _trench && t.countUnits() > 0) continue;
                if (!IsOurs(t) && NearBuilding(t)) continue;
                if (Gate(c, t))
                {
                    if (IsWall(t)) Remove(t);
                    continue;
                }
                if (t.top_type == _damaged) continue;
                if (IsOurs(t) && want != _trench)
                {
                    int cur = Array.IndexOf(_tiers, t.top_type);
                    if (cur >= tier) continue;
                }
                if (!Pay(c, modern ? 4 : tier)) break;
                Place(t, want);
                budget--;
                built++;
            }
            Repair(c, perim, yrs);
            Towers(c, perim, modern);
            if (built > 8) Internet.OnWalls(c, level);
            _cache[c] = perim.Where(IsWall).ToList();
            World.world.resetRedrawTimer();
        }

        private static void Repair(City c, List<WorldTile> perim, int yrs)
        {
            int budget = (1 + Roles.Count(c, Roles.Engineer) * 2) * yrs;
            int tier = Math.Max(0, Math.Min(4, Level(c.kingdom)));
            foreach (WorldTile t in perim)
            {
                if (budget <= 0) return;
                if (t.top_type != _damaged) continue;
                if (!Pay(c, tier)) return;
                Place(t, _tiers[tier]);
                budget--;
            }
        }

        private static void Cleanup(City c, HashSet<WorldTile> perim)
        {
            int n = 0;
            foreach (TileZone z in c.zones.ToList())
            {
                foreach (WorldTile t in z.tiles.ToList())
                {
                    if (!IsOurs(t) || t.top_type == _border || perim.Contains(t)) continue;
                    Remove(t);
                    if (++n >= 60) return;
                }
            }
        }

        private static BuildingAsset TowerAsset(City c, bool modern)
        {
            if (modern) return AssetManager.buildings.get("civ_antiair");
            BuildingAsset a = Buildings.Arch(c, "order_watch_tower");
            return a ?? AssetManager.buildings.get("watch_tower_human");
        }

        private static void Towers(City c, List<WorldTile> perim, bool modern)
        {
            int have = 0;
            foreach (Building b in c.buildings)
            {
                if (b != null && b.data != null && Store.Bool(b.data, KTower)) have++;
            }
            int max = Math.Min(10, 2 + Math.Max(0, Level(c.kingdom)) + perim.Count / 60);
            if (have >= max) return;
            BuildingAsset asset = TowerAsset(c, modern);
            if (asset == null) return;
            var spots = new List<WorldTile>();
            foreach (WorldTile t in perim)
            {
                if (!Gate(c, t)) continue;
                foreach (WorldTile n in t.neighbours)
                {
                    if (n != null && !n.hasBuilding() && n.Type != null && !n.Type.liquid && !n.Type.block && !n.Type.road && n.zone != null && n.zone.city == c) spots.Add(n);
                }
            }
            for (int i = 0; i < perim.Count; i += 20)
            {
                WorldTile t = perim[i];
                foreach (WorldTile n in t.neighbours)
                {
                    if (n != null && !n.hasBuilding() && n.Type != null && !n.Type.liquid && !n.Type.block && n.zone != null && n.zone.city == c)
                    {
                        spots.Add(n);
                        break;
                    }
                }
            }
            foreach (WorldTile s in spots)
            {
                if (have >= max) return;
                if (!Pay(c, 2)) return;
                Building b = World.world.buildings.addBuilding(asset, s, true);
                if (b == null) continue;
                if (b.data != null) Store.Put(b.data, KTower, true);
                have++;
            }
        }

        public static void Demolish(City c, int yrs, bool borders)
        {
            int n = 0;
            foreach (TileZone z in c.zones.ToList())
            {
                foreach (WorldTile t in z.tiles.ToList())
                {
                    if (!IsOurs(t)) continue;
                    if (t.top_type == _border && !borders) continue;
                    if (!G.Chance(0.25f * yrs)) continue;
                    Remove(t);
                    if (++n > 40) break;
                }
                if (n > 40) break;
            }
            if (n > 10) Internet.OnWallsDown(c);
        }

        public static void Tick(float dt)
        {
            if (_tiers == null) return;
            _tick += dt;
            if (_tick < 1f) return;
            float span = _tick;
            _tick = 0f;
            foreach (var kv in _cache.ToList())
            {
                City c = kv.Key;
                if (!G.Ok(c) || c.kingdom == null)
                {
                    _cache.Remove(c);
                    continue;
                }
                List<WorldTile> walls = kv.Value;
                if (walls.Count == 0 || !World.world.wars.hasWars(c.kingdom)) continue;
                int samples = Math.Min(walls.Count, 14);
                for (int s = 0; s < samples; s++)
                {
                    WorldTile t = walls[UnityEngine.Random.Range(0, walls.Count)];
                    if (!IsWall(t)) continue;
                    int dmg = 0;
                    foreach (Actor a in Finder.getUnitsFromChunk(t, 1, 2.5f))
                    {
                        if (!G.Ok(a) || a.kingdom == null || !World.world.wars.isInWarWith(a.kingdom, c.kingdom)) continue;
                        UnitDef d = Vehicles.Def(a);
                        if (d != null) dmg += d.Cls == "siege" ? 6 : d.Cls == "tank" ? 4 : 2;
                        else if (a.isWarrior()) dmg += 1;
                    }
                    if (dmg <= 0) continue;
                    int hp;
                    if (!_hp.TryGetValue(t, out hp)) hp = MaxHp(t);
                    hp -= (int)Math.Ceiling(dmg * span);
                    if (hp <= 0)
                    {
                        Remove(t);
                        continue;
                    }
                    if (hp <= MaxHp(t) / 2 && t.top_type != _damaged && t.top_type != _border)
                    {
                        MapAction.terraformTop(t, _damaged, AssetManager.terraform.get("road"));
                        hp = Math.Min(hp, MaxHp(t));
                    }
                    _hp[t] = hp;
                }
            }
        }

        public static void Clear()
        {
            _cache.Clear();
            _hp.Clear();
            _gates.Clear();
        }

        private static void BorderWalls(Kingdom k, int yrs)
        {
            if (_border == null) return;
            if (!Research.Has(k, "concrete_walls") && Research.Era(k) < 11) return;
            string x = Policies.Id(k, "xeno");
            string ideo = Policies.Id(k, "ideo");
            foreach (City c in G.Cities(k))
            {
                foreach (City n in c.neighbours_cities)
                {
                    if (n == null || n.kingdom == null || n.kingdom == k || !n.kingdom.isCiv()) continue;
                    Kingdom o = n.kingdom;
                    float want = 0f;
                    if (x == "xenophobia" && o.getSpecies() != k.getSpecies()) want += 0.5f;
                    if (ideo == "nationalism") want += 0.2f;
                    if (World.world.wars.isInWarWith(k, o)) want += 0.3f;
                    if (Politics.Satisfaction(o) < 0.35f && Bonus.Sum(k, "gold") > Bonus.Sum(o, "gold") + 20f) want += 0.3f;
                    if (Research.Has(k, "concrete_walls")) want += 0.2f;
                    if (x == "xenophilia") want -= 0.4f;
                    if (Policies.Id(k, "transport") == "rail_first" && Economy.TradeOpinion(k, o) > 10) want -= 0.2f;
                    if (want < 0.5f)
                    {
                        if (want < 0.2f) Demolish(c, yrs, true);
                        continue;
                    }
                    BuildBorder(c, n, yrs);
                }
            }
        }

        private static void BuildBorder(City mine, City theirs, int yrs)
        {
            int budget = 6 * yrs;
            var theirZones = new HashSet<TileZone>(theirs.zones);
            int built = 0;
            foreach (TileZone z in mine.zones.ToList())
            {
                bool adj = false;
                foreach (TileZone n in z.neighbours) if (n != null && theirZones.Contains(n)) adj = true;
                if (!adj) continue;
                foreach (WorldTile t in z.tiles.ToList())
                {
                    if (budget <= 0) break;
                    if (t == null || t.hasBuilding() || t.Type == null || t.Type.liquid || t.top_type == _border || t.countUnits() > 0) continue;
                    if (t.Type.block && !IsOurs(t)) continue;
                    bool edge = false;
                    foreach (WorldTile n in t.neighbours) if (n != null && n.zone != null && theirZones.Contains(n.zone)) edge = true;
                    if (!edge || (t.Type.road && (t.x + t.y) % 3 != 0)) continue;
                    if (!Economy.Has(mine, "civ_concrete", 1) && !Economy.Has(mine, "stone", 30)) return;
                    if (!Economy.Take(mine, "civ_concrete", 1)) Economy.Take(mine, "stone", 2);
                    Place(t, _border);
                    built++;
                    budget--;
                }
            }
            if (built > 0)
            {
                World.world.resetRedrawTimer();
                if (G.Chance(0.2f)) Internet.OnBorderWall(mine.kingdom, theirs.kingdom);
            }
        }
    }
}
