using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class Walls
    {
        public const string KLastWar = "civ_last_war";
        public const string KBorder = "civ_border_walls";
        public const int ShieldEra = 15;
        private const string KTower = "civ_wall_tower";
        private const string KSig = "civ_wsig";
        private const string KPlanYear = "civ_wplan_y";
        private const string KPlan = "civ_wplan";
        private const string KFoes = "civ_wfoes";
        private const string KReg = "civ_wall_reg";
        private const int ReplanYears = 5;
        private static readonly string[] TierLook = { "wall_wild", "wall_order", "wall_ancient", "wall_iron" };
        private static readonly int[] TierStrength = { 1, 2, 3, 4 };
        private static TopTileType[] _tiers;
        private static TopTileType _border;
        private static TopTileType _shield;
        private static TopTileType _damaged;
        private static TopTileType _trench;
        private static readonly HashSet<TopTileType> _ours = new HashSet<TopTileType>();
        private static readonly Dictionary<TopTileType, string> _look = new Dictionary<TopTileType, string>();
        private static readonly Dictionary<int, long> _reg = new Dictionary<int, long>();
        private static readonly Dictionary<long, List<int>> _byOwner = new Dictionary<long, List<int>>();
        private static readonly Dictionary<long, HashSet<int>> _plans = new Dictionary<long, HashSet<int>>();
        private static readonly Dictionary<int, int> _hp = new Dictionary<int, int>();
        private static bool _loaded;
        private static bool _ownersDirty = true;

        public static void InitAssets()
        {
            if (AssetManager.top_tiles.get("wall_order") == null) return;
            _tiers = new TopTileType[4];
            for (int i = 0; i < 4; i++) _tiers[i] = Tile("civ_wallt_" + i, TierLook[i], null, TierStrength[i], i == 0);
            _border = Tile("civ_wallt_border", "wall_iron", null, 4, false);
            _shield = Tile("civ_wallt_shield", "wall_iron", "#5AD6FF", 5, false);
            _look[_shield] = "civ_shield";
            _damaged = Tile("civ_wallt_damaged", "wall_order", "#7A6F64", 1, false);
            Tile("civ_wallt_4", "wall_iron", null, 4, false);
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
            if (_shield != null) list.Add(_shield);
            if (_damaged != null) list.Add(_damaged);
            TopTileType old = AssetManager.top_tiles.get("civ_wallt_4");
            if (old != null) list.Add(old);
            return list.ToArray();
        }

        public static string Look(TopTileType t)
        {
            string l;
            return t != null && _look.TryGetValue(t, out l) ? l : "wall_order";
        }

        public static bool IsShield(TopTileType t)
        {
            return t != null && t == _shield;
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

        public static void Clear()
        {
            _reg.Clear();
            _byOwner.Clear();
            _plans.Clear();
            _hp.Clear();
            _loaded = false;
            _ownersDirty = true;
        }

        private static void Ensure()
        {
            if (_loaded || !G.Loaded) return;
            _loaded = true;
            _reg.Clear();
            SaveCustomData d = Store.World();
            string s = Store.Str(d, KReg);
            if (s.Length > 0)
            {
                foreach (string p in s.Split(';'))
                {
                    int i = p.IndexOf(':');
                    if (i <= 0) continue;
                    int id;
                    long owner;
                    if (int.TryParse(p.Substring(0, i), out id) && long.TryParse(p.Substring(i + 1), out owner)) _reg[id] = owner;
                }
            }
            foreach (TopTileType t in _ours)
            {
                if (t == null) continue;
                foreach (WorldTile w in t.getCurrentTiles().ToList())
                {
                    if (w == null || _reg.ContainsKey(w.tile_id)) continue;
                    Kingdom k = Owner(w);
                    _reg[w.tile_id] = k != null ? k.data.id : 0L;
                }
            }
            _ownersDirty = true;
        }

        public static void Save()
        {
            if (!_loaded) return;
            var sb = new StringBuilder();
            foreach (var kv in _reg)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            Store.Put(Store.World(), KReg, sb.ToString());
        }

        private static WorldTile At(int id)
        {
            WorldTile[] all = World.world.tiles_list;
            return all != null && id >= 0 && id < all.Length ? all[id] : null;
        }

        private static Kingdom Owner(WorldTile t)
        {
            City c = t != null && t.zone != null ? t.zone.city : null;
            return c != null && G.Ok(c) && c.kingdom != null && c.kingdom.isCiv() ? c.kingdom : null;
        }

        private static void Owners()
        {
            if (!_ownersDirty) return;
            _ownersDirty = false;
            _byOwner.Clear();
            foreach (var kv in _reg)
            {
                List<int> l;
                if (!_byOwner.TryGetValue(kv.Value, out l)) _byOwner[kv.Value] = l = new List<int>();
                l.Add(kv.Key);
            }
        }

        private static void Register(WorldTile t, Kingdom k)
        {
            _reg[t.tile_id] = k.data.id;
            _ownersDirty = true;
        }

        private static void Unregister(int id)
        {
            if (_reg.Remove(id)) _ownersDirty = true;
            _hp.Remove(id);
        }

        public static void Register()
        {
            Hub.On(Hub.WarStarted, e =>
            {
                if (!G.Ok(e.K) || !G.Ok(e.O)) return;
                Foe(e.K, e.O);
                Foe(e.O, e.K);
            });
            Hub.On(Hub.CityCaptured, e =>
            {
                if (e.Arg != "captured" || !G.Ok(e.K) || !G.Ok(e.O)) return;
                Foe(e.O, e.K);
            });
        }

        private static void Foe(Kingdom k, Kingdom o)
        {
            Dictionary<string, float> m = Store.Map(k.data, KFoes);
            m[o.data.id.ToString()] = G.Year;
            if (m.Count > 24)
            {
                foreach (string key in m.OrderBy(x => x.Value).Take(m.Count - 24).Select(x => x.Key).ToList()) m.Remove(key);
            }
            Store.Map(k.data, KFoes, m);
        }

        private static bool RecentFoe(Kingdom k, Kingdom o, int years)
        {
            float y = Store.Get(Store.Map(k.data, KFoes), o.data.id.ToString());
            return y != 0f && G.Year - (int)y <= years;
        }

        private static bool Partner(Kingdom k, Kingdom o)
        {
            if (Diplomacy.Overlord(k) == o || Diplomacy.Overlord(o) == k) return true;
            foreach (Treaty t in Diplomacy.Between(k, o))
            {
                if (t.Type == "non_aggression" || t.Type == "trade_treaty" || t.Type == "embassy" || t.Type == "defense_pact" || t.Type == "entente" || t.Type == "nuclear_umbrella" || t.Type == "royal_marriage") return true;
            }
            if (Orgs.Shares(k, o, x => true)) return true;
            try
            {
                if (k.hasAlliance() && k.getAlliance() == o.getAlliance()) return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        private static int Opinion(Kingdom a, Kingdom b)
        {
            try
            {
                KingdomOpinion op = World.world.diplomacy.getOpinion(a, b);
                return op != null ? op.total : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool Hostile(Kingdom k, Kingdom o, KProfile p)
        {
            if (World.world.wars.isInWarWith(k, o)) return true;
            if (RecentFoe(k, o, 40)) return true;
            return Opinion(k, o) < -25;
        }

        private static bool WallAgainst(Kingdom k, Kingdom o, KProfile p)
        {
            bool xeno = p.Xeno == "xenophobia";
            if (Partner(k, o) && !xeno) return false;
            float want = 0f;
            if (World.world.wars.isInWarWith(k, o)) want += 1f;
            if (RecentFoe(k, o, 40)) want += 0.6f;
            int op = Opinion(k, o);
            if (op < -20) want += 0.35f;
            if (op < -60) want += 0.25f;
            if (xeno)
            {
                want += 0.55f;
                if (o.getSpecies() != k.getSpecies()) want += 0.2f;
            }
            if (p.Ideo == "nationalism") want += 0.25f;
            if (p.Ideo == "militarism") want += 0.15f;
            if (p.Xeno == "xenophilia") want -= 0.4f;
            if (ModernPeace(k)) want -= 0.3f;
            if (Profile.Get(o).Strength > p.Strength * 1.5f) want += 0.15f;
            return want >= 0.5f;
        }

        private static Vector2 Center(Kingdom k)
        {
            WorldTile t = k.capital != null ? G.Tile(k.capital) : null;
            if (t != null) return new Vector2(t.x, t.y);
            float x = 0f, y = 0f;
            int n = 0;
            foreach (City c in G.Cities(k))
            {
                WorldTile ct = G.Tile(c);
                if (ct == null) continue;
                x += ct.x;
                y += ct.y;
                n++;
            }
            return n > 0 ? new Vector2(x / n, y / n) : Vector2.zero;
        }

        private static bool Mine(TileZone z, Kingdom k)
        {
            return z != null && z.city != null && z.city.kingdom == k;
        }

        private static List<TileZone> BorderZones(Kingdom k)
        {
            var list = new List<TileZone>();
            foreach (City c in G.Cities(k))
            {
                foreach (TileZone z in c.zones)
                {
                    if (z == null || z.neighbours == null) continue;
                    foreach (TileZone n in z.neighbours)
                    {
                        if (n != null && !Mine(n, k))
                        {
                            list.Add(z);
                            break;
                        }
                    }
                }
            }
            return list;
        }

        private static int Signature(Kingdom k, List<TileZone> zones)
        {
            unchecked
            {
                int h = 17;
                foreach (TileZone z in zones)
                {
                    h ^= z.id * 486187739;
                    h += 31;
                    foreach (TileZone n in z.neighbours)
                    {
                        if (n == null || Mine(n, k)) continue;
                        long o = n.city != null && n.city.kingdom != null ? n.city.kingdom.data.id : 0L;
                        h ^= (int)(o * 16777619L) + n.id;
                    }
                }
                foreach (global::War w in World.world.wars.getWars(k)) h ^= (int)(w.getID() * 2654435761L);
                h ^= Policies.Id(k, "xeno").GetHashCode();
                return h;
            }
        }

        private static bool Buildable(WorldTile t)
        {
            if (t == null || t.Type == null || t.hasBuilding()) return false;
            TileTypeBase ty = t.Type;
            if (ty.liquid || ty.lava || ty.ocean) return false;
            if (ty.block && !IsOurs(t)) return false;
            TileType m = t.main_type;
            if (m == null || m.liquid || m.lava || m.ocean || m.mountains || m.summit || m.rocks) return false;
            foreach (WorldTile n in t.neighboursAll)
            {
                if (n == null || n.Type == null) return false;
                if (n.Type.liquid || n.Type.ocean || n.main_type != null && (n.main_type.liquid || n.main_type.ocean)) return false;
            }
            if (m.sand)
            {
                foreach (WorldTile n in t.getTilesAround(3))
                {
                    if (n != null && n.main_type != null && (n.main_type.liquid || n.main_type.ocean)) return false;
                }
            }
            return true;
        }

        private static bool Gate(WorldTile t)
        {
            return t != null && (t.Type != null && t.Type.road || t.top_type == Infrastructure.RailBed);
        }

        private static HashSet<int> Plan(Kingdom k, List<TileZone> zones)
        {
            KProfile p = Profile.Get(k);
            Vector2 me = Center(k);
            var against = new Dictionary<long, bool>();
            var threats = new List<Vector2>();
            foreach (Kingdom o in Fog.KnownKingdoms(k))
            {
                if (!G.Ok(o) || o == k || !o.isCiv() || o.capital == null) continue;
                if (!Hostile(k, o, p)) continue;
                Vector2 oc = Center(o);
                if (oc != Vector2.zero) threats.Add(oc);
            }
            bool xenoAll = p.Xeno == "xenophobia";
            var plan = new HashSet<int>();
            foreach (TileZone z in zones)
            {
                var outside = new Dictionary<TileZone, bool>();
                foreach (TileZone n in z.neighbours)
                {
                    if (n == null || Mine(n, k)) continue;
                    Kingdom o = n.city != null && G.Ok(n.city) ? n.city.kingdom : null;
                    bool want;
                    if (o != null && o.isCiv())
                    {
                        if (!against.TryGetValue(o.data.id, out want)) against[o.data.id] = want = WallAgainst(k, o, p);
                    }
                    else if (n.tiles_with_ground <= 0)
                    {
                        want = false;
                    }
                    else
                    {
                        want = xenoAll || Facing(me, z, threats);
                    }
                    outside[n] = want;
                }
                if (!outside.Values.Any(x => x)) continue;
                foreach (WorldTile t in z.tiles)
                {
                    if (t == null) continue;
                    bool edge = false;
                    foreach (WorldTile n in t.neighbours)
                    {
                        if (n == null || n.zone == null || n.zone == z) continue;
                        bool w;
                        if (outside.TryGetValue(n.zone, out w) && w)
                        {
                            edge = true;
                            break;
                        }
                    }
                    if (edge && Buildable(t)) plan.Add(t.tile_id);
                }
            }
            return plan;
        }

        private static bool Facing(Vector2 me, TileZone z, List<Vector2> threats)
        {
            if (threats.Count == 0 || z.centerTile == null) return false;
            Vector2 dir = new Vector2(z.centerTile.x, z.centerTile.y) - me;
            if (dir.sqrMagnitude < 1f) return false;
            foreach (Vector2 t in threats)
            {
                Vector2 td = t - me;
                if (td.sqrMagnitude < 1f) continue;
                if (Vector2.Angle(dir, td) <= 55f) return true;
            }
            return false;
        }

        private static HashSet<int> LoadPlan(Kingdom k)
        {
            HashSet<int> plan;
            if (_plans.TryGetValue(k.data.id, out plan)) return plan;
            plan = new HashSet<int>();
            string s = Store.Str(k.data, KPlan);
            if (s.Length > 0)
            {
                foreach (string x in s.Split(','))
                {
                    int id;
                    if (int.TryParse(x, out id)) plan.Add(id);
                }
            }
            _plans[k.data.id] = plan;
            return plan;
        }

        private static void SavePlan(Kingdom k, HashSet<int> plan)
        {
            _plans[k.data.id] = plan;
            Store.Put(k.data, KPlan, string.Join(",", plan.Select(x => x.ToString()).ToArray()));
        }

        private static TopTileType Want(Kingdom k)
        {
            int era = Research.Era(k);
            if (era >= ShieldEra && _shield != null) return _shield;
            if (era >= 11 || Research.Has(k, "concrete_walls")) return _border;
            int level = Level(k);
            if (level < 0) return null;
            return _tiers[Math.Min(level, 3)];
        }

        private static int Rank(TopTileType t)
        {
            if (t == null) return -1;
            if (t == _shield) return 6;
            if (t == _border) return 5;
            for (int i = 0; i < _tiers.Length; i++) if (_tiers[i] == t) return i + 1;
            return 0;
        }

        private static readonly Dictionary<TopTileType, string[]> CostCache = new Dictionary<TopTileType, string[]>();

        private static string[] Costs(TopTileType t)
        {
            if (t == _shield) return new[] { "civ_electronics:1", "civ_steel:1", "civ_concrete:2" };
            if (t == _border) return new[] { "civ_concrete:1", "civ_steel:1", "stone:3" };
            int i = Array.IndexOf(_tiers, t);
            switch (i)
            {
                case 0: return new[] { "wood:1" };
                case 1: return new[] { "stone:1", "wood:2" };
                case 2: return new[] { "civ_bricks:1", "stone:2" };
                default: return new[] { "stone:3", "civ_steel:1" };
            }
        }

        private static bool Pay(City c, TopTileType t)
        {
            foreach (string opt in Costs(t))
            {
                int i = opt.IndexOf(':');
                string res = opt.Substring(0, i);
                int n = int.Parse(opt.Substring(i + 1));
                if (Economy.Has(c, res, n + (res == "stone" || res == "wood" ? 20 : 0))) return Economy.Take(c, res, n);
            }
            return false;
        }

        private static void Refund(City c, TopTileType t)
        {
            if (c == null || t == null) return;
            string opt = Costs(t)[0];
            int i = opt.IndexOf(':');
            int n = int.Parse(opt.Substring(i + 1));
            if (G.Chance(0.6f)) Economy.Give(c, opt.Substring(0, i), Math.Max(1, n / 2 + (G.Chance(0.5f) ? 1 : 0)));
        }

        public static int Tiers { get { return _tiers != null ? _tiers.Length : 0; } }

        public static bool SandboxPlace(WorldTile t, int tier, Kingdom k)
        {
            if (_tiers == null || t == null || !G.Ok(k) || t.Type == null || t.Type.liquid || !t.Type.ground) return false;
            TopTileType type = tier < 0 ? _border : tier >= _tiers.Length ? _shield : _tiers[tier];
            if (type == null) return false;
            Place(t, type, k);
            return true;
        }

        public static bool SandboxErase(WorldTile t)
        {
            if (t == null || !IsWall(t) && t.top_type != _trench) return false;
            Remove(t);
            Unregister(t.tile_id);
            return true;
        }

        private static void Place(WorldTile t, TopTileType type, Kingdom k)
        {
            MapAction.terraformTop(t, type, AssetManager.terraform.get("road"));
            _hp.Remove(t.tile_id);
            Register(t, k);
        }

        private static void Remove(WorldTile t)
        {
            _hp.Remove(t.tile_id);
            MapAction.decreaseTile(t, false, "flash");
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (_tiers == null || !G.Ok(k) || !k.isCiv()) return;
            Ensure();
            if (World.world.wars.hasWars(k)) Store.Put(k.data, KLastWar, G.Year);
            Capture(k);
            TopTileType want = Want(k);
            HashSet<int> plan;
            if (want == null)
            {
                plan = new HashSet<int>();
                SavePlan(k, plan);
            }
            else
            {
                List<TileZone> zones = BorderZones(k);
                int sig = Signature(k, zones);
                int last = Store.Int(k.data, KPlanYear, int.MinValue);
                if (sig != Store.Int(k.data, KSig, 0) || last == int.MinValue || G.Year - last >= ReplanYears)
                {
                    plan = Plan(k, zones);
                    SavePlan(k, plan);
                    Store.Put(k.data, KSig, sig);
                    Store.Put(k.data, KPlanYear, G.Year);
                }
                else plan = LoadPlan(k);
            }
            Dismantle(k, plan, yrs);
            if (want != null && plan.Count > 0)
            {
                int built = Build(k, plan, want, yrs);
                Towers(k, plan);
                if (built > 0)
                {
                    World.world.resetRedrawTimer();
                    if (built > 8) CivMod.Safe("walls-news", () => WallNews(k, built));
                }
            }
        }

        private static void WallNews(Kingdom k, int built)
        {
            Kingdom foe = null;
            foreach (Kingdom o in Fog.KnownKingdoms(k))
            {
                if (World.world.wars.isInWarWith(k, o))
                {
                    foe = o;
                    break;
                }
            }
            if (foe != null && G.Chance(0.25f)) Internet.OnBorderWall(k, foe);
            else if (k.capital != null && G.Chance(0.2f)) Internet.OnWalls(k.capital, Math.Max(0, Level(k)));
            if (G.Chance(0.05f)) Chronicle.Log("border_wall", k, G.Name(k), null, null, foe, null, k.capital, null, false);
        }

        private static void Capture(Kingdom k)
        {
            Owners();
            var moved = new List<int>();
            foreach (var kv in _byOwner)
            {
                if (kv.Key == k.data.id) continue;
                foreach (int id in kv.Value)
                {
                    WorldTile t = At(id);
                    if (t == null || !IsOurs(t))
                    {
                        moved.Add(-id - 1);
                        continue;
                    }
                    if (Owner(t) == k) moved.Add(id);
                }
            }
            foreach (int id in moved)
            {
                if (id < 0) Unregister(-id - 1);
                else
                {
                    _reg[id] = k.data.id;
                    _ownersDirty = true;
                }
            }
        }

        private static int Build(Kingdom k, HashSet<int> plan, TopTileType want, int yrs)
        {
            int engineers = 0;
            foreach (City c in G.Cities(k)) engineers += Roles.Count(c, Roles.Engineer) + Roles.Count(c, Roles.Sapper);
            bool war = World.world.wars.hasWars(k);
            int budget = (int)((6 + engineers * 3) * yrs * (war ? 1.6f : 1f));
            budget = Math.Min(budget, 160);
            int rank = Rank(want);
            int built = 0;
            foreach (int id in plan)
            {
                if (budget <= 0) break;
                WorldTile t = At(id);
                if (t == null || Owner(t) != k) continue;
                if (Gate(t))
                {
                    if (IsWall(t))
                    {
                        Remove(t);
                        Unregister(id);
                    }
                    continue;
                }
                if (t.top_type == want) continue;
                if (t.hasBuilding() || t.countUnits() > 0) continue;
                if (IsOurs(t) && t.top_type != _damaged && Rank(t.top_type) >= rank) continue;
                City c = t.zone.city;
                if (!Pay(c, want)) continue;
                Place(t, want, k);
                budget--;
                built++;
            }
            return built;
        }

        private static void Dismantle(Kingdom k, HashSet<int> plan, int yrs)
        {
            Owners();
            List<int> mine;
            if (!_byOwner.TryGetValue(k.data.id, out mine)) return;
            int limit = Math.Min(60, 12 * yrs);
            int n = 0;
            foreach (int id in mine.ToList())
            {
                if (n >= limit) break;
                if (plan.Contains(id)) continue;
                WorldTile t = At(id);
                if (t == null || !IsOurs(t))
                {
                    Unregister(id);
                    continue;
                }
                if (!G.Chance(0.35f)) continue;
                City c = t.zone != null ? t.zone.city : null;
                Refund(c != null && c.kingdom == k ? c : k.capital, t.top_type);
                Remove(t);
                Unregister(id);
                n++;
            }
            if (n > 10 && k.capital != null) Internet.OnWallsDown(k.capital);
            if (n > 0) World.world.resetRedrawTimer();
        }

        private static BuildingAsset TowerAsset(City c, int era)
        {
            if (era >= 11) return AssetManager.buildings.get("civ_antiair");
            BuildingAsset a = Buildings.Arch(c, "order_watch_tower");
            return a ?? AssetManager.buildings.get("watch_tower_human");
        }

        private static void Towers(Kingdom k, HashSet<int> plan)
        {
            int era = Research.Era(k);
            int have = 0;
            var towers = new List<WorldTile>();
            foreach (City c in G.Cities(k))
            {
                foreach (Building b in c.buildings)
                {
                    if (b == null || b.data == null || !Store.Bool(b.data, KTower)) continue;
                    have++;
                    WorldTile bt = b.current_tile;
                    if (bt != null) towers.Add(bt);
                }
            }
            int max = Math.Min(30, plan.Count / 24 + 1);
            if (have >= max) return;
            int made = 0;
            int i = 0;
            foreach (int id in plan)
            {
                if (have >= max || made >= 3) return;
                if (i++ % 24 != 0) continue;
                WorldTile t = At(id);
                if (t == null || !IsWall(t)) continue;
                bool near = false;
                foreach (WorldTile o in towers)
                {
                    if (Math.Abs(o.x - t.x) + Math.Abs(o.y - t.y) < 14)
                    {
                        near = true;
                        break;
                    }
                }
                if (near) continue;
                City c = t.zone.city;
                if (c == null || c.kingdom != k) continue;
                BuildingAsset asset = TowerAsset(c, era);
                if (asset == null) continue;
                WorldTile spot = null;
                foreach (WorldTile n in t.neighbours)
                {
                    if (n != null && n.zone == t.zone && !n.hasBuilding() && n.Type != null && !n.Type.liquid && !n.Type.block && !n.Type.road && !plan.Contains(n.tile_id))
                    {
                        spot = n;
                        break;
                    }
                }
                if (spot == null || !Pay(c, _tiers[Math.Min(2, _tiers.Length - 1)])) continue;
                Building b = World.world.buildings.addBuilding(asset, spot, true);
                if (b == null) continue;
                if (b.data != null) Store.Put(b.data, KTower, true);
                towers.Add(spot);
                have++;
                made++;
            }
        }

        public static void CityYear(City c, int yrs)
        {
            if (c == null || c.kingdom == null || !c.kingdom.isCiv()) return;
            foreach (Building b in c.buildings.ToList())
            {
                if (b == null || b.asset == null || b.asset.type != "type_civ_wall") continue;
                b.startDestroyBuilding();
            }
        }

        public static void Demolish(City c, int yrs, bool borders)
        {
        }

        private static int MaxHp(TopTileType t)
        {
            if (t == _shield) return 90;
            if (t == _border) return 60;
            if (t == _damaged) return 8;
            int i = _tiers != null ? Array.IndexOf(_tiers, t) : -1;
            return i >= 0 ? 14 + i * 10 : 10;
        }

        private static void Hit(WorldTile t, int dmg)
        {
            int id = t.tile_id;
            int hp;
            if (!_hp.TryGetValue(id, out hp)) hp = MaxHp(t.top_type);
            hp -= dmg;
            if (hp <= 0)
            {
                Remove(t);
                Unregister(id);
                return;
            }
            if (hp <= MaxHp(t.top_type) / 2 && t.top_type != _damaged && t.top_type != _border && t.top_type != _shield)
            {
                MapAction.terraformTop(t, _damaged, AssetManager.terraform.get("road"));
                hp = Math.Min(hp, MaxHp(_damaged));
            }
            _hp[id] = hp;
        }

        private static bool Enemy(WorldTile t, Kingdom by)
        {
            long owner;
            if (!_reg.TryGetValue(t.tile_id, out owner) || !IsWall(t)) return false;
            if (by == null) return true;
            Kingdom k = World.world.kingdoms.get(owner);
            return G.Ok(k) && k != by && World.world.wars.isInWarWith(by, k);
        }

        private static int Power(Actor a)
        {
            UnitDef d = Vehicles.Def(a);
            if (d != null) return d.Cls == "siege" ? 6 : d.Cls == "tank" ? 4 : 2;
            return 1;
        }

        public static void OnAttack(Actor a)
        {
            if (_tiers == null || !_loaded || _reg.Count == 0 || !G.Ok(a) || a.kingdom == null || a.current_tile == null) return;
            if (!World.world.wars.hasWars(a.kingdom)) return;
            WorldTile best = null;
            float bestD = 99f;
            foreach (WorldTile t in a.current_tile.getTilesAround(2))
            {
                if (t == null || !Enemy(t, a.kingdom)) continue;
                float d = Toolbox.DistTile(a.current_tile, t);
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }
            if (best != null) Hit(best, Power(a));
        }

        public static void OnBlast(WorldTile center, int rad, BaseSimObject by)
        {
            if (_tiers == null || !_loaded || _reg.Count == 0 || center == null) return;
            Kingdom k = by != null && by.isActor() ? by.a.kingdom : by != null && by.isBuilding() ? by.b.kingdom : null;
            int dmg = Math.Max(4, rad * 4);
            foreach (WorldTile t in center.getTilesAround(Math.Max(1, Math.Min(rad, 12))))
            {
                if (t != null && Enemy(t, k)) Hit(t, dmg);
            }
        }

        [HarmonyPatch(typeof(Actor), nameof(Actor.tryToAttack))]
        private static class Attacked
        {
            private static void Postfix(Actor __instance, bool __result)
            {
                if (!__result || _reg.Count == 0) return;
                try
                {
                    OnAttack(__instance);
                }
                catch (Exception e)
                {
                    CivMod.Report("walls-hit", e);
                }
            }
        }

        [HarmonyPatch(typeof(MapAction), nameof(MapAction.damageWorld))]
        private static class Blast
        {
            private static void Postfix(WorldTile pTile, int pRad, BaseSimObject pByWho)
            {
                if (_reg.Count == 0) return;
                try
                {
                    OnBlast(pTile, pRad, pByWho);
                }
                catch (Exception e)
                {
                    CivMod.Report("walls-blast", e);
                }
            }
        }
    }
}
