using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Civilizations
{
    internal static class Ships
    {
        public static string For(City c, bool trading)
        {
            if (c == null || c.kingdom == null || !c.kingdom.isCiv()) return null;
            List<UnitDef> options = Vehicles.Options(c.kingdom, d => d.Cls == "ship" && (trading ? d.Role == "trade" : d.Role == "war"));
            string route = Research.Route(c.kingdom);
            var counts = new Dictionary<string, int>();
            var newest = new Dictionary<string, int>();
            foreach (UnitDef d in options)
            {
                string duty = VehicleRoster.Duty(d);
                int n;
                if (!newest.TryGetValue(duty, out n) || d.Era > n) newest[duty] = d.Era;
            }
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "ship") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                string duty = VehicleRoster.Duty(d);
                foreach (Actor ship in asset.units)
                {
                    if (!G.Ok(ship) || ship.kingdom != c.kingdom) continue;
                    int n;
                    counts.TryGetValue(duty, out n);
                    counts[duty] = n + 1;
                    counts.TryGetValue(d.Id, out n);
                    counts[d.Id] = n + 1;
                }
            }
            UnitDef best = null;
            float score = -1f;
            foreach (UnitDef d in options)
            {
                string duty = VehicleRoster.Duty(d);
                int roles, models;
                counts.TryGetValue(duty, out roles);
                counts.TryGetValue(d.Id, out models);
                float s = VehicleRoster.ShipShare(duty) * VehicleRoster.Weight(d, newest[duty]) / ((roles + 1f) * (models + 1f));
                if (!string.IsNullOrEmpty(d.Route) && d.Route != "real" && d.Route == route) s *= 2f;
                if (s <= score) continue;
                score = s;
                best = d;
            }
            return best != null ? best.Id : null;
        }

        public static string ForFish(City c)
        {
            if (c == null || c.kingdom == null || !c.kingdom.isCiv()) return null;
            List<UnitDef> options = Vehicles.Options(c.kingdom, d => d.Cls == "ship" && d.Role == "fish");
            string route = Research.Route(c.kingdom);
            UnitDef best = null;
            int score = -1;
            foreach (UnitDef d in options)
            {
                int s = d.Era * 4 + (!string.IsNullOrEmpty(d.Route) && d.Route != "real" && d.Route == route ? 2 : 0) + (d.Route == "real" ? 1 : 0);
                if (s <= score) continue;
                score = s;
                best = d;
            }
            return best != null ? best.Id : null;
        }

        private static string DutyOf(Actor a)
        {
            UnitDef d;
            if (Db.UnitById.TryGetValue(a.asset.id, out d)) return VehicleRoster.Duty(d);
            if (a.asset.boat_type == "boat_type_trading") return "ship_trade";
            if (a.asset.boat_type == "boat_type_fishing") return "ship_fish";
            if (a.asset.boat_type == "boat_type_transport") return "ship_surface";
            return null;
        }

        public static void Retire(Kingdom k, int yrs)
        {
            if (!G.Ok(k) || !k.isCiv() || k.units == null) return;
            var current = new Dictionary<string, HashSet<string>>();
            foreach (UnitDef d in Vehicles.Options(k, x => x.Cls == "ship"))
            {
                string duty = VehicleRoster.Duty(d);
                HashSet<string> set;
                if (!current.TryGetValue(duty, out set)) current[duty] = set = new HashSet<string>();
                set.Add(d.Id);
            }
            int budget = 4;
            int era = Research.Era(k);
            float chance = System.Math.Min(0.9f, 0.3f * yrs);
            foreach (Actor a in k.units.ToArray())
            {
                if (budget <= 0) break;
                if (!G.Ok(a) || a.asset == null || !a.asset.is_boat || a.isFavorite()) continue;
                string duty = DutyOf(a);
                HashSet<string> set = null;
                bool modern = duty != null && current.TryGetValue(duty, out set) && set.Count > 0;
                if (modern && set.Contains(a.asset.id)) continue;
                if (!modern)
                {
                    UnitDef od;
                    int shipEra = Db.UnitById.TryGetValue(a.asset.id, out od) ? od.Era : 0;
                    if (current.Count == 0 || era - shipEra < 4) continue;
                }
                Boat boat = a.getSimpleComponent<Boat>();
                if (boat != null && boat.hasPassengers()) continue;
                if (!G.Chance(chance)) continue;
                ActionLibrary.removeUnit(a);
                budget--;
            }
        }

        [HarmonyPatch(typeof(BuildingAsset), "getBoatAssetIDFromType")]
        private static class EraShips
        {
            private static bool Prefix(string pSpeciesBoat, City pCity, ref string __result)
            {
                if (pCity == null) return true;
                ActorAsset a = pCity.getActorAsset();
                if (a != null && a.architecture_asset != null) return true;
                ActorAsset f = pCity.getFounderSpecies();
                ArchitectureAsset arch = f != null ? f.architecture_asset : null;
                if (arch == null)
                {
                    __result = "boat_fishing";
                    return false;
                }
                __result = pSpeciesBoat == "boat_type_trading" ? arch.actor_asset_id_trading : pSpeciesBoat == "boat_type_transport" ? arch.actor_asset_id_transport : arch.actor_asset_id_boat_fishing;
                return false;
            }

            private static void Postfix(string pSpeciesBoat, City pCity, ref string __result)
            {
                if (pCity == null) return;
                string id = null;
                if (pSpeciesBoat == "boat_type_trading") id = For(pCity, true);
                else if (pSpeciesBoat == "boat_type_transport") id = For(pCity, false);
                else if (pSpeciesBoat == "boat_type_fishing") id = ForFish(pCity);
                if (pSpeciesBoat == "boat_type_fishing" && pCity.kingdom != null && pCity.kingdom.isCiv() && !Research.Has(pCity.kingdom, "dugout_canoe")) __result = null;
                if (!string.IsNullOrEmpty(id)) __result = id;
            }
        }
    }
}
