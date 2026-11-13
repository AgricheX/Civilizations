using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class RailLink
    {
        public int Id;
        public string A = "";
        public string B = "";
        public string K = "";
        public string Path = "";
        public int Built;
        public int Length;
        public string Kind = "steam";
        public bool Done;
        public int Year;
        public int FleetVersion;
        public bool St;
        [JsonIgnore]
        public List<WorldTile> Tiles;
    }

    internal static class Rails
    {
        private const string KData = "civ_rails";
        private static List<RailLink> _links = new List<RailLink>();
        private static int _next = 1;
        public static int Version;

        public static List<RailLink> Links { get { return _links; } }

        public static void Load()
        {
            _links = new List<RailLink>();
            _next = 1;
            SaveCustomData d = Store.World();
            string s = d != null ? Store.Str(d, KData) : "";
            if (s.Length > 0)
            {
                try
                {
                    _links = JsonConvert.DeserializeObject<List<RailLink>>(s) ?? new List<RailLink>();
                }
                catch (Exception)
                {
                    _links = new List<RailLink>();
                }
            }
            foreach (RailLink l in _links) _next = Math.Max(_next, l.Id + 1);
            Version++;
        }

        public static void Save()
        {
            SaveCustomData d = Store.World();
            if (d == null) return;
            Store.Put(d, KData, JsonConvert.SerializeObject(_links));
        }

        public static List<WorldTile> TilesOf(RailLink l)
        {
            if (l.Tiles != null) return l.Tiles;
            var list = new List<WorldTile>();
            foreach (string p in l.Path.Split(';'))
            {
                int i = p.IndexOf(',');
                if (i <= 0) continue;
                int x, y;
                if (!int.TryParse(p.Substring(0, i), out x) || !int.TryParse(p.Substring(i + 1), out y)) continue;
                WorldTile t = Globe.Tile(x, y);
                if (t != null) list.Add(t);
            }
            l.Tiles = list;
            return list;
        }

        private static string Pack(List<WorldTile> tiles)
        {
            var sb = new StringBuilder();
            foreach (WorldTile t in tiles)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(t.x).Append(',').Append(t.y);
            }
            return sb.ToString();
        }

        public static bool HasStation(City c)
        {
            return G.Ok(c) && c.countBuildingsType("type_civ_station") > 0;
        }

        public static WorldTile StationTile(City c)
        {
            if (!G.Ok(c)) return null;
            foreach (Building b in c.buildings)
            {
                if (b == null || b.asset == null || b.asset.type != "type_civ_station" || !b.isUsable() || b.current_tile == null) continue;
                WorldTile t = b.current_tile;
                foreach (WorldTile n in t.neighbours)
                {
                    if (n != null && !n.hasBuilding() && n.Type != null && !n.Type.liquid && !n.Type.block) return n;
                }
                WorldTile below = Globe.Tile(t.x, t.y - 1);
                if (below != null && !below.hasBuilding()) return below;
                return t;
            }
            return G.Tile(c);
        }

        public static bool WantsStation(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !Research.HasFeature(c.kingdom, "rail")) return false;
            if (HasStation(c) || c.getPopulationPeople() < 25) return false;
            foreach (City n in c.neighbours_cities) if (n != null && n.isAlive()) return true;
            return false;
        }

        public static bool Connected(City a, City b)
        {
            if (a == null || b == null) return false;
            string ia = G.Id(a), ib = G.Id(b);
            foreach (RailLink l in _links)
            {
                if (!l.Done) continue;
                if ((l.A == ia && l.B == ib) || (l.A == ib && l.B == ia)) return true;
            }
            return false;
        }

        public static List<RailLink> LinksOf(City c)
        {
            var list = new List<RailLink>();
            string id = G.Id(c);
            foreach (RailLink l in _links) if (l.Done && (l.A == id || l.B == id)) list.Add(l);
            return list;
        }

        private static RailLink Find(City a, City b)
        {
            string ia = G.Id(a), ib = G.Id(b);
            foreach (RailLink l in _links)
            {
                if ((l.A == ia && l.B == ib) || (l.A == ib && l.B == ia)) return l;
            }
            return null;
        }

        public static string KindFor(Kingdom k)
        {
            return KindFor(k, 0, 1);
        }

        private static bool TrainKnown(Kingdom k, TrainDef t)
        {
            return t != null && Research.Has(k, t.Tech) && (string.IsNullOrEmpty(t.Route) || t.Route == Research.Route(k));
        }

        private static bool TrainKnown(Kingdom k, string id)
        {
            return TrainKnown(k, Db.Trains.Find(t => t.Id == id));
        }

        private static string KindFor(Kingdom k, int length, int id)
        {
            if (length >= 60 && id % 4 == 0)
            {
                foreach (string special in new[] { "maglev", "monorail", "gyro" })
                    if (TrainKnown(k, special)) return special;
            }
            if (length >= 45 && id % 3 == 0 && TrainKnown(k, "bullet")) return "bullet";
            return TrainKnown(k, "diesel") ? "diesel" : "steam";
        }

        public static TrainDef ForDeparture(RailLink link, City from)
        {
            var options = new List<TrainDef>();
            var weights = new List<float>();
            bool freight = G.Chance(0.55f);
            string track = VehicleRoster.Track(link.Kind);
            foreach (TrainDef t in Db.Trains)
            {
                if (t.Id == "tram" || VehicleRoster.Track(t.Id) != track || !TrainKnown(from.kingdom, t)) continue;
                if (t.Id == "armored" && !World.world.wars.hasWars(from.kingdom)) continue;
                if (t.Id == "steam" && Research.Route(from.kingdom) != "steam" && Research.Era(from.kingdom) >= 12 && TrainKnown(from.kingdom, "diesel")) continue;
                if (t.Fuel == "electric" && Energy.Blackout(from)) continue;
                string fuel = TrainFuel(t);
                if (fuel.Length > 0 && !Economy.Has(from, fuel, 1)) continue;
                float weight = 1f;
                if (t.Id == "diesel") weight = freight ? 2f : 0.7f;
                if (t.Id == "bullet") weight = freight ? 0.3f : 2f;
                if (t.Id == "steam") weight = Research.Route(from.kingdom) == "steam" ? 1.5f : 0.5f;
                if (t.Speed >= 4f) weight = 1f + t.Speed * 0.4f;
                options.Add(t);
                weights.Add(weight);
            }
            return options.Count > 0 ? options[G.Weighted(weights)] : null;
        }

        public static string TrainFuel(TrainDef t)
        {
            return t.Fuel == "coal" ? "civ_coal" : t.Fuel == "fuel" ? "civ_fuel" : t.Fuel == "uranium" ? "civ_uranium" : "";
        }

        public static void Invest(City c, float budget)
        {
            Kingdom k = c.kingdom;
            if (k == null || !Research.HasFeature(k, "rail") || !HasStation(c) || budget <= 0f) return;
            foreach (Company co in Companies.Of(k)) if (co.Sector == "rail") budget += Companies.Tier(co) * 3f;
            RailLink open = null;
            foreach (RailLink l in _links)
            {
                if (!l.Done && (l.A == G.Id(c) || l.B == G.Id(c)))
                {
                    open = l;
                    break;
                }
            }
            if (open == null) open = Plan(c);
            if (open == null)
            {
                Upgrade(c, k);
                return;
            }
            Build(open, (int)budget);
        }

        private static RailLink Plan(City c)
        {
            Kingdom k = c.kingdom;
            City best = null;
            float bd = float.MaxValue;
            bool intl = Research.HasFeature(k, "telegraph");
            foreach (City n in c.neighbours_cities)
            {
                if (n == null || !n.isAlive() || n.kingdom == null) continue;
                if (!HasStation(n)) continue;
                if (Find(c, n) != null) continue;
                if (n.kingdom != k)
                {
                    if (!intl || World.world.wars.isInWarWith(k, n.kingdom)) continue;
                    if (Policies.Id(k, "xeno") == "xenophobia" && n.kingdom.getSpecies() != k.getSpecies()) continue;
                }
                float d = G.Dist(c, n);
                if (n.kingdom != k) d *= 1.5f;
                if (d < bd)
                {
                    bd = d;
                    best = n;
                }
            }
            if (best == null || bd > 140f) return null;
            bool bridges = Research.HasFeature(k, "bridges");
            List<WorldTile> path = Paths.Find(StationTile(c), StationTile(best), t => RailCost(t, bridges), 12000);
            if (path.Count < 2) return null;
            int id = _next++;
            var l = new RailLink { Id = id, A = G.Id(c), B = G.Id(best), K = G.Id(k), Path = Pack(path), Length = path.Count, Kind = KindFor(k, path.Count, id), Year = G.Year, FleetVersion = 1, St = true };
            l.Tiles = path;
            _links.Add(l);
            Version++;
            return l;
        }

        private static float RailCost(WorldTile t, bool bridges)
        {
            float c = Infrastructure.Cost(t, bridges);
            if (c < 0f) return c;
            if (t.Type.mountains) return 14f;
            if (t.top_type == Infrastructure.RailBed) return 0.3f;
            return c;
        }

        private static void Build(RailLink l, int budget)
        {
            List<WorldTile> tiles = TilesOf(l);
            while (budget > 0 && l.Built < tiles.Count)
            {
                WorldTile t = tiles[l.Built];
                if (t != null && !t.hasBuilding())
                {
                    if (t.Type.liquid)
                    {
                        if (budget < 4) break;
                        MapAction.terraformMain(t, TileLibrary.sand);
                        budget -= 3;
                    }
                    if (!t.Type.block && Infrastructure.RailBed != null && !t.Type.road) MapAction.terraformTop(t, Infrastructure.RailBed, AssetManager.terraform.get("road"));
                }
                l.Built++;
                budget--;
            }
            if (l.Built >= tiles.Count && !l.Done)
            {
                l.Done = true;
                Version++;
                City a = G.CityById(l.A), b = G.CityById(l.B);
                Internet.OnRailOpened(a, b, l.Kind);
                if (a != null && b != null && a.kingdom != null && a.kingdom.capital == a) Chronicle.Log("rail_opened", a.kingdom, G.Name(a), G.Name(b), null, b.kingdom, null, a, null, false);
            }
            World.world.resetRedrawTimer();
        }

        private static void Upgrade(City c, Kingdom k)
        {
            foreach (RailLink l in LinksOf(c))
            {
                if (l.K != G.Id(k)) continue;
                string kind = KindFor(k, l.Length, l.Id);
                bool legacy = l.FleetVersion == 0;
                l.FleetVersion = 1;
                if (l.Kind == kind) continue;
                if (!legacy && VehicleRoster.Track(l.Kind) != VehicleRoster.Track(kind)) continue;
                l.Kind = kind;
                Version++;
                return;
            }
        }

        public static void Year()
        {
            bool changed = false;
            foreach (RailLink l in _links.ToArray())
            {
                City a = G.CityById(l.A), b = G.CityById(l.B);
                if (a == null || b == null)
                {
                    _links.Remove(l);
                    changed = true;
                    continue;
                }
                if (!l.St && HasStation(a) && HasStation(b) && a.kingdom != null)
                {
                    bool bridges = Research.HasFeature(a.kingdom, "bridges");
                    List<WorldTile> path = Paths.Find(StationTile(a), StationTile(b), t => RailCost(t, bridges), 12000);
                    l.St = true;
                    if (path.Count >= 2)
                    {
                        l.Path = Pack(path);
                        l.Tiles = path;
                        l.Length = path.Count;
                        l.Built = 0;
                        l.Done = false;
                        changed = true;
                    }
                    continue;
                }
                if (l.Done && a.kingdom != null && b.kingdom != null && World.world.wars.isInWarWith(a.kingdom, b.kingdom) && G.Chance(0.2f))
                {
                    l.Done = false;
                    l.Built = Math.Max(0, l.Built - G.Range(3, 12));
                    changed = true;
                    Internet.OnRailSabotage(a, b);
                }
            }
            if (changed) Version++;
        }

        public static int CountKm(Kingdom k)
        {
            string id = G.Id(k);
            int n = 0;
            foreach (RailLink l in _links) if (l.K == id) n += l.Built;
            return n;
        }
    }
}
