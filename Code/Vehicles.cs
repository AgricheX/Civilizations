using System;
using System.Collections.Generic;
using System.Linq;
using ai;

namespace Civilizations
{
    internal static class Vehicles
    {
        public const string KRider = "civ_rider";
        private static float _tick;

        private static readonly Dictionary<string, string[]> Mats = new Dictionary<string, string[]>
        {
            { "fodder", new[] { "wood" } }, { "powder", new[] { "common_metals", "civ_powder" } }, { "coal", new[] { "civ_steel", "civ_machinery" } },
            { "fuel", new[] { "civ_steel", "civ_machinery" } }, { "electric", new[] { "civ_steel", "civ_electronics" } }, { "uranium", new[] { "civ_steel", "civ_electronics" } },
            { "bio", new[] { "civ_biomass" } }, { "", new[] { "wood" } }
        };

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.VehiclesOn; }
        }

        public const string KConscript = "civ_conscript";

        public static bool IsSoldier(UnitDef d)
        {
            return d != null && d.Cls == "soldier";
        }

        public static bool IsVehicle(Actor a)
        {
            return a != null && a.asset != null && Db.UnitById.ContainsKey(a.asset.id) && !a.asset.is_boat;
        }

        public static UnitDef Def(Actor a)
        {
            UnitDef d;
            return a != null && a.asset != null && Db.UnitById.TryGetValue(a.asset.id, out d) ? d : null;
        }

        public static bool Military(UnitDef d)
        {
            return d.Cls != "land" || d.Id == "civ_chariot" || Store.Get(d.Stats, "damage") >= 30f;
        }

        public static List<Actor> In(City c)
        {
            var list = new List<Actor>();
            if (c == null) return list;
            foreach (Actor a in c.units)
            {
                if (G.Ok(a) && IsVehicle(a) && !IsSoldier(Def(a)) && !Carriers.Embarked(a)) list.Add(a);
            }
            return list;
        }

        public static int FuelNeed(City c, string fuel)
        {
            if (c == null) return 0;
            int n = 0;
            foreach (Actor a in c.units)
            {
                UnitDef d = Def(a);
                if (d != null && d.Fuel == fuel) n++;
            }
            return n;
        }

        public static int WantedCrew(City c, bool air)
        {
            if (!On || c == null || c.kingdom == null) return 0;
            int pop = c.getPopulationPeople();
            if (air) return Research.Has(c.kingdom, "hot_air_balloon") && pop >= 60 ? 1 + c.countWarriors() / 15 : 0;
            if (!Research.Has(c.kingdom, "the_wheel")) return 0;
            return Math.Min(5, pop / 35);
        }

        public static bool Available(Kingdom k, UnitDef d)
        {
            if (k == null || d == null || !VehicleRoster.InService(d, Research.Era(k))) return false;
            if (!Research.Has(k, d.Tech) || !JapanStyle.Allows(k, d.Id)) return false;
            if (!string.IsNullOrEmpty(d.Route))
            {
                string r = Research.Route(k);
                if (d.Route == "real") return r == "" || r == "real" || r == "diesel" && d.Era < 11 || r == "atom" && d.Era < 12 || r == "bio" && d.Era < 13;
                return d.Route == r;
            }
            return true;
        }

        public static UnitDef Best(City c, Func<UnitDef, bool> filter)
        {
            UnitDef best = null;
            foreach (UnitDef d in Db.Units)
            {
                if (!filter(d) || !Available(c.kingdom, d)) continue;
                if (best == null || d.Era > best.Era || d.Era == best.Era && Store.Get(d.Stats, "health") > Store.Get(best.Stats, "health")) best = d;
            }
            return best;
        }

        public static List<UnitDef> Options(Kingdom k, Func<UnitDef, bool> filter)
        {
            var all = new List<UnitDef>();
            var newest = new Dictionary<string, int>();
            foreach (UnitDef d in Db.Units)
            {
                if (!filter(d) || !Available(k, d) || AssetManager.actor_library.get(d.Id) == null) continue;
                all.Add(d);
                string duty = VehicleRoster.Duty(d);
                int era;
                if (!newest.TryGetValue(duty, out era) || d.Era > era) newest[duty] = d.Era;
            }
            int current = Research.Era(k);
            return all.FindAll(d => VehicleRoster.Relevant(d, current, newest[VehicleRoster.Duty(d)]));
        }

        private static UnitDef Choose(City c, string duty, List<Actor> have)
        {
            List<UnitDef> options = Options(c.kingdom, d => VehicleRoster.Duty(d) == duty);
            if (options.Count == 0) return null;
            int newest = options.Max(d => d.Era);
            options.RemoveAll(d => !CanAfford(c, d));
            if (options.Count == 0) return null;
            var weights = new List<float>();
            foreach (UnitDef d in options)
            {
                int count = Count(have, x => x.Id == d.Id);
                float w = VehicleRoster.Weight(d, newest) / (1f + count);
                if (d.Base == "civ_light_drone" && options.Count > 1) w *= 0.5f;
                weights.Add(w);
            }
            return options[G.Weighted(weights)];
        }

        private static readonly string[] CheapMats = { "civ_electronics" };

        private static string[] MatsFor(UnitDef d)
        {
            if (d.Base == "civ_light_drone" && d.Fuel != "bio") return CheapMats;
            string[] m;
            if (!Mats.TryGetValue(d.Fuel ?? "", out m)) m = Mats[""];
            return m;
        }

        private const int GoldPerMissing = 12;

        private static int MissingGold(City c, UnitDef d, int scale)
        {
            int gold = 0;
            foreach (string r in MatsFor(d)) if (!Economy.Has(c, r, scale)) gold += GoldPerMissing * scale;
            return gold;
        }

        public static bool CanAfford(City c, UnitDef d)
        {
            int scale = Math.Max(1, (int)Math.Round(d.Crew * 0.1f));
            int gold = MissingGold(c, d, scale);
            return gold == 0 || Economy.Amount(c, "gold") >= gold;
        }

        public static void PayFor(City c, UnitDef d)
        {
            Pay(c, d);
        }

        private static void Pay(City c, UnitDef d)
        {
            int scale = Math.Max(1, (int)Math.Round(d.Crew * 0.1f));
            int gold = 0;
            foreach (string r in MatsFor(d))
            {
                if (Economy.Has(c, r, scale)) Economy.Take(c, r, scale);
                else gold += GoldPerMissing * scale;
            }
            if (gold > 0) Economy.Take(c, "gold", Math.Min(gold, Economy.Amount(c, "gold")));
        }

        public static void CityYear(City c, int yrs)
        {
            if (!On || c.kingdom == null || !c.kingdom.isCiv()) return;
            List<Actor> have = In(c);
            Upgrade(c, have);
            int warriors = c.countWarriors();
            bool war = World.world.wars.hasWars(c.kingdom);
            int drivers = WantedCrew(c, false);
            Produce(c, have, "cargo", drivers - drivers / 2, Roles.Driver, false);
            Produce(c, have, "passenger", drivers / 2, Roles.Driver, false);
            int era = Research.Era(c.kingdom);
            bool bioMounts = Research.Route(c.kingdom) == "bio" && Research.Has(c.kingdom, "bio_mounts");
            float cav = era >= 12 && !bioMounts ? 0f : era >= 11 ? 1f : 3f;
            bool air = Aircraft.EnemyAir(c.kingdom);
            Fill(c, have, new[] { "cavalry", "siege", "tank", "apc", "aa" }, new[] { cav, 2f, 3f, 1.5f, air ? 1.5f : 0f }, Cap(30, war), PerYear(war, yrs), x => x == "cavalry" ? "" : Roles.Artillery, war);
            Aircraft.CityYear(c, yrs, have);
            Navy(c, yrs, war);
            Soldiers.CityYear(c, yrs, war);
            if (Store.Flt(c.data, "civ_fuel_ok", 1f) < 0.3f)
            {
                foreach (Actor v in have)
                {
                    UnitDef d = Def(v);
                    if (d == null || d.Fuel == "" || d.Fuel == "fodder" || !G.Ok(v)) continue;
                    if (G.Chance(0.25f * yrs)) Dismount(v, false);
                    else if (!v.hasStatus("civ_no_fuel")) v.addStatusEffect("civ_no_fuel", 60f);
                }
            }
        }

        public static int Count(List<Actor> have, Func<UnitDef, bool> filter)
        {
            int n = 0;
            foreach (Actor v in have)
            {
                UnitDef d = Def(v);
                if (G.Ok(v) && d != null && filter(d)) n++;
            }
            return n;
        }

        public static int Cap(int full, bool war)
        {
            int n = war ? full : Math.Max(1, full / 3);
            if (CivMod.C != null && CivMod.C.PerfMode) n = Math.Max(1, n / 3);
            return n;
        }

        public static int PerYear(bool war, int yrs)
        {
            return Math.Min(12, (war ? 6 : 2) * Math.Max(1, yrs));
        }

        public static void Fill(City c, List<Actor> have, string[] duties, float[] weights, int cap, int perYear, Func<string, string> role, bool war)
        {
            int total = Count(have, d => Array.IndexOf(duties, VehicleRoster.Duty(d)) >= 0 && Military(d));
            var blocked = new HashSet<string>();
            int made = 0;
            while (total < cap && made < perYear)
            {
                string best = null;
                float score = float.MaxValue;
                for (int i = 0; i < duties.Length; i++)
                {
                    if (weights[i] <= 0f || blocked.Contains(duties[i])) continue;
                    string duty = duties[i];
                    int n = Count(have, d => VehicleRoster.Duty(d) == duty && Military(d));
                    float s = (n + 1f) / weights[i];
                    if (s < score)
                    {
                        score = s;
                        best = duty;
                    }
                }
                if (best == null) return;
                if (!Make(c, have, best, role(best), true, war))
                {
                    blocked.Add(best);
                    continue;
                }
                total++;
                made++;
            }
        }

        private static void Navy(City c, int yrs, bool war)
        {
            if (c.kingdom == null || !Research.Has(c.kingdom, "dugout_canoe")) return;
            Building dock = null;
            foreach (Building b in c.buildings)
            {
                if (b != null && b.isAlive() && b.component_docks != null && b.component_docks.tiles_ocean != null && b.component_docks.tiles_ocean.Count > 0)
                {
                    dock = b;
                    break;
                }
            }
            if (dock == null) return;
            int have = 0;
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "ship" || d.Role == "trade") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor s in asset.units) if (G.Ok(s) && s.city == c) have++;
            }
            int cap = Cap(10, war);
            int made = 0;
            int per = Math.Min(4, (war ? 3 : 1) * Math.Max(1, yrs));
            while (have < cap && made < per)
            {
                string id = Ships.For(c, false);
                UnitDef d;
                if (string.IsNullOrEmpty(id) || !Db.UnitById.TryGetValue(id, out d) || AssetManager.actor_library.get(id) == null) return;
                if (!CanAfford(c, d)) return;
                WorldTile t = dock.component_docks.tiles_ocean.GetRandom();
                if (t == null || t.region == null || t.region.island == null || !t.region.island.goodForDocks()) return;
                Actor ship = World.world.units.createNewUnit(id, t);
                if (ship == null) return;
                dock.component_docks.addBoatToDock(ship);
                if (ship.kingdom != c.kingdom) ship.setKingdom(c.kingdom);
                if (war) Store.Put(ship.data, KConscript, true);
                Pay(c, d);
                have++;
                made++;
            }
        }

        private static Actor Spawn(City c, string id)
        {
            WorldTile tile = c.getTile();
            if (tile == null) return null;
            Actor v = World.world.units.createNewUnit(id, tile, false, 0f, null, null, false, true);
            if (v == null) return null;
            v.setKingdom(c.kingdom);
            v.setCity(c);
            v.setProfession(UnitProfession.Warrior);
            Store.Put(v.data, KRider, "");
            Store.Put(v.data, KConscript, true);
            VehicleNames.Apply(v);
            return v;
        }

        public static bool Make(City c, List<Actor> have, string duty, string crewRole, bool military, bool crewless)
        {
            UnitDef d = Choose(c, duty, have);
            if (d == null) return false;
            Actor crew = null;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || IsVehicle(a) || !a.isAdult() || a.isKing() || a.isCityLeader() || !a.isSapient()) continue;
                if (military && !a.isWarrior()) continue;
                if (crewRole.Length > 0 && Roles.Of(a) == crewRole)
                {
                    crew = a;
                    break;
                }
                if (crew == null && (crewRole.Length == 0 || military && Roles.Of(a).Length == 0)) crew = a;
            }
            Actor v2 = crew != null ? Mount(crew, d.Id) : crewless ? Spawn(c, d.Id) : null;
            if (v2 == null) return false;
            Pay(c, d);
            have.Add(v2);
            return true;
        }

        public static bool Produce(City c, List<Actor> have, string duty, int want, string crewRole, bool military)
        {
            if (CivMod.C != null && CivMod.C.PerfMode) want = Math.Min(want, military ? 3 : 2);
            if (want <= 0) return false;
            int count = Count(have, x => VehicleRoster.Duty(x) == duty && Military(x) == military);
            if (count >= want) return false;
            UnitDef d = Choose(c, duty, have);
            if (d == null) return false;
            Actor crew = null;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || IsVehicle(a) || !a.isAdult() || a.isKing() || a.isCityLeader() || !a.isSapient()) continue;
                if (military && !a.isWarrior()) continue;
                if (crewRole.Length > 0 && Roles.Of(a) == crewRole)
                {
                    crew = a;
                    break;
                }
                if (crew == null && (crewRole.Length == 0 || military && Roles.Of(a).Length == 0)) crew = a;
            }
            if (crew == null) return false;
            Actor v2 = Mount(crew, d.Id);
            if (v2 == null) return false;
            Pay(c, d);
            have.Add(v2);
            return true;
        }

        private static void Upgrade(City c, List<Actor> have)
        {
            int era = Research.Era(c.kingdom);
            foreach (Actor v in have.ToList())
            {
                if (!G.Ok(v)) continue;
                UnitDef d = Def(v);
                if (d == null) continue;
                string duty = VehicleRoster.Duty(d);
                UnitDef latest = Best(c, x => VehicleRoster.Duty(x) == duty);
                if (VehicleRoster.Relevant(d, era, latest != null ? latest.Era : d.Era)) continue;
                UnitDef next = Choose(c, duty, have);
                if (next != null)
                {
                    Actor replacement = Swap(v, next.Id, Store.Str(v.data, KRider));
                    if (replacement == null) continue;
                    Pay(c, next);
                    have.Remove(v);
                    have.Add(replacement);
                }
                else if (Dismount(v, false) != null) have.Remove(v);
            }
        }

        public static Actor Mount(Actor rider, string vehicleId)
        {
            if (!G.Ok(rider) || rider.current_tile == null) return null;
            Actor v = Swap(rider, vehicleId, rider.asset.id);
            if (v != null) Internet.OnVehicle(v, vehicleId);
            return v;
        }

        private static Actor Swap(Actor from, string toAsset, string riderAsset)
        {
            WorldTile tile = from.current_tile;
            Kingdom k = from.kingdom;
            City c = from.city;
            UnitDef def;
            Db.UnitById.TryGetValue(toAsset, out def);
            bool warrior = from.isWarrior() || (def != null && Military(def));
            string role = Roles.Of(from);
            Actor v = World.world.units.createNewUnit(toAsset, tile, false, 0f, null, null, false, true);
            if (v == null) return null;
            ActorTool.copyUnitToOtherUnit(from, v);
            v.data.asset_id = toAsset;
            if (k != null) v.setKingdom(k);
            if (c != null) v.setCity(c);
            Store.Put(v.data, KRider, riderAsset ?? "");
            if (from.subspecies != null && from.subspecies.data != null) Store.Put(v.data, "civ_rider_sub", from.subspecies.data.id.ToString());
            else Store.Put(v.data, "civ_rider_sub", Store.Str(from.data, "civ_rider_sub"));
            Store.Put(v.data, Roles.KRole, role);
            if (warrior) v.setProfession(UnitProfession.Warrior);
            VehicleNames.Apply(v);
            ActionLibrary.removeUnit(from);
            return v;
        }

        public static Actor Dismount(Actor v, bool died)
        {
            if (v == null || v.data == null) return null;
            string rider = Store.Str(v.data, KRider);
            if (string.IsNullOrEmpty(rider) || AssetManager.actor_library.get(rider) == null || v.current_tile == null) return null;
            Subspecies sub = null;
            long sid;
            if (long.TryParse(Store.Str(v.data, "civ_rider_sub"), out sid)) sub = World.world.subspecies.get(sid);
            if (sub == null && v.city != null) sub = v.city.getMainSubspecies();
            Actor p = World.world.units.createNewUnit(rider, v.current_tile, false, 0f, sub, null, false, true);
            if (p == null) return null;
            ActorTool.copyUnitToOtherUnit(v, p);
            p.data.asset_id = rider;
            if (v.kingdom != null && v.kingdom.isCiv()) p.setKingdom(v.kingdom);
            if (v.city != null) p.setCity(v.city);
            foreach (string r in Roles.All)
            {
                ActorTrait t = AssetManager.traits.get("civ_role_" + r);
                if (t != null) p.removeTrait(t);
            }
            Store.Put(p.data, Roles.KRole, "");
            if (!died) ActionLibrary.removeUnit(v);
            return p;
        }

        public static bool OnDeath(BaseSimObject pTarget, WorldTile pTile)
        {
            try
            {
                if (pTarget == null || !pTarget.isActor()) return false;
                Actor v = pTarget.a;
                UnitDef d = Def(v);
                float survive = d != null && d.Cls == "air" ? 0.3f : 0.5f;
                if (G.Chance(survive)) Dismount(v, true);
                Internet.OnVehicleLost(v);
            }
            catch (Exception e)
            {
                CivMod.Log("vehicle death: " + e.Message);
            }
            return true;
        }

        public static void Tick(float dt)
        {
            if (!On) return;
            _tick += dt;
            if (_tick < 2f) return;
            _tick = 0f;
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls == "ship" || d.Cls == "soldier") continue;
                ActorAsset a = AssetManager.actor_library.get(d.Id);
                if (a == null || a.units == null || a.units.Count == 0) continue;
                bool mil = Military(d);
                foreach (Actor v in a.units.ToList())
                {
                    if (!G.Ok(v)) continue;
                    if (v.kingdom == null || !v.kingdom.isCiv())
                    {
                        if (G.Chance(0.2f)) Dismount(v, false);
                        continue;
                    }
                    if (mil && !v.isWarrior()) v.setProfession(UnitProfession.Warrior);
                    if (d.Fuel == "electric" && v.city != null && Energy.Blackout(v.city) && !v.hasStatus("civ_no_fuel")) v.addStatusEffect("civ_no_fuel", 20f);
                }
            }
        }
    }
}
