using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class EraGoods
    {
        public static readonly string[] FuelKinds = { "fuel", "coal", "uranium", "bio", "fusion", "antimatter", "dark", "vacuum" };

        public static readonly string[] Raw = { "civ_helium3", "civ_asteroid_metals", "civ_comet_ice", "civ_antimatter", "civ_exotic_matter", "civ_neutronium", "civ_dark_matter" };

        private static readonly Dictionary<string, string> CosmicRes = new Dictionary<string, string>
        {
            { "helium3", "civ_helium3" }, { "asteroid_metals", "civ_asteroid_metals" }, { "comet_ice", "civ_comet_ice" }, { "antimatter", "civ_antimatter" },
            { "exotic_matter", "civ_exotic_matter" }, { "neutronium", "civ_neutronium" }, { "dark_matter", "civ_dark_matter" }
        };

        public static string FuelRes(string fuel)
        {
            switch (fuel)
            {
                case "coal": return "civ_coal";
                case "fuel": return "civ_fuel";
                case "uranium": return "civ_uranium";
                case "bio": return "civ_biomass";
                case "fusion": return "civ_fusion_cells";
                case "antimatter": return "civ_antimatter_cells";
                case "dark": return "civ_dark_cells";
                case "vacuum": return "civ_vacuum_cells";
            }
            return null;
        }

        public static string[] Mats(int era)
        {
            if (era >= 35) return new[] { "civ_smart_matter", "civ_neutronium_plate" };
            if (era >= 28) return new[] { "civ_smart_matter", "civ_metamaterials" };
            if (era >= 25) return new[] { "civ_neutronium_plate", "civ_nanomaterials" };
            if (era >= 22) return new[] { "civ_nanomaterials", "civ_superconductors" };
            if (era >= 18) return new[] { "civ_alloys", "civ_superconductors" };
            if (era >= 15) return new[] { "civ_alloys" };
            if (era >= 12) return new[] { "civ_steel", "civ_electronics" };
            return new[] { "civ_steel" };
        }

        public static string KingdomFuel(Kingdom k)
        {
            if (!G.Ok(k)) return "civ_fuel";
            if (Research.Has(k, "vacuum_fabrication")) return "civ_vacuum_cells";
            if (Research.Has(k, "dark_matter_reactors")) return "civ_dark_cells";
            if (Research.Has(k, "antimatter_depots")) return "civ_antimatter_cells";
            if (Research.Has(k, "helium3_refineries")) return "civ_fusion_cells";
            return "civ_fuel";
        }

        public static int TakeFuel(City c, Kingdom k, int amount)
        {
            if (c == null || amount <= 0) return 0;
            int got = Economy.TakeUpTo(c, KingdomFuel(k), amount);
            if (got < amount) got += Economy.TakeUpTo(c, "civ_fuel", amount - got);
            return got;
        }

        public static bool HasFuel(City c, Kingdom k, int amount)
        {
            if (c == null) return false;
            return Economy.Amount(c, KingdomFuel(k)) + Economy.Amount(c, "civ_fuel") >= amount;
        }

        public static bool HasMats(City c, int era, int amount)
        {
            if (c == null) return false;
            foreach (string m in Mats(era)) if (!Economy.Has(c, m, amount)) return false;
            return true;
        }

        public static void TakeMats(City c, int era, int amount)
        {
            if (c == null) return;
            foreach (string m in Mats(era)) Economy.Take(c, m, amount);
        }

        public static bool IsSpaceport(Building b)
        {
            return b != null && b.asset != null && b.asset.type == "type_civ_launchpad";
        }

        public static int Ports(City c)
        {
            int n = 0;
            if (c == null) return 0;
            foreach (Building b in c.buildings) if (b != null && b.isUsable() && IsSpaceport(b)) n++;
            return n;
        }

        public static void Imports(Kingdom k, int yrs)
        {
            if (!G.Ok(k) || !k.isCiv() || Research.Era(k) < 15) return;
            Dictionary<string, float> stock;
            try
            {
                stock = CosmosSim.Stock(k);
            }
            catch (Exception)
            {
                return;
            }
            if (stock == null || stock.Count == 0) return;
            var docks = new List<City>();
            City cap = k.capital;
            foreach (City c in G.Cities(k))
            {
                if (c == cap || Ports(c) > 0) docks.Add(c);
            }
            if (docks.Count == 0 && G.Ok(cap)) docks.Add(cap);
            if (docks.Count == 0) return;
            float trade = Bonus.Relative(k, "trade");
            foreach (var kv in stock)
            {
                string res;
                if (!CosmicRes.TryGetValue(kv.Key, out res)) continue;
                int total = (int)Math.Min(120f, kv.Value * 2f * trade * Math.Max(1, yrs));
                if (total <= 0) continue;
                int each = Math.Max(1, total / docks.Count);
                foreach (City c in docks) Economy.Give(c, res, each);
            }
        }

        public static void Extract(City c, Kingdom k, float tools, int yrs, Dictionary<string, float> rep, Action<City, string, float, int, Dictionary<string, float>> produce)
        {
            if (Research.Era(k) < 15) return;
            int mines = c.countBuildingsType("type_mine");
            int ports = Ports(c);
            if (Research.Has(k, "helium3_mining")) produce(c, "civ_helium3", (mines * 0.6f + ports * 3f) * tools, yrs, rep);
            if (Research.Has(k, "asteroid_mining")) produce(c, "civ_asteroid_metals", (ports * 4f + (Research.Has(k, "automated_mining_swarms") ? mines * 0.8f : 0f)) * tools, yrs, rep);
            if (Research.HasUnlock(k, "cos:res:comet_ice") || Research.Has(k, "deuterium_helium_farms")) produce(c, "civ_comet_ice", (ports * 3f + mines * 0.3f) * tools, yrs, rep);
            if (Research.Has(k, "neutron_star_mining")) produce(c, "civ_neutronium", ports * 1.2f * tools, yrs, rep);
            if (Research.Has(k, "dark_matter_harvest")) produce(c, "civ_dark_matter", ports * 1.5f * tools, yrs, rep);
        }

        public static readonly HashSet<string> Foods = new HashSet<string> { "civ_canned", "civ_frozen", "civ_vat_meals", "civ_star_rations" };

        public static IEnumerable<string> FuelResources()
        {
            return FuelKinds.Select(FuelRes).Where(x => x != null);
        }
    }
}
