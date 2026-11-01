using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ai.behaviours;

namespace Civilizations
{
    internal static class Catchup
    {
        private static readonly Dictionary<long, float> _boost = new Dictionary<long, float>();
        private static bool _chaining;

        private static readonly Dictionary<string, string> Subs = new Dictionary<string, string>
        {
            { "civ_electronics", "civ_machinery" }, { "civ_machinery", "civ_steel" }, { "civ_steel", "common_metals" },
            { "civ_concrete", "civ_bricks" }, { "civ_bricks", "stone" }, { "civ_glass", "stone" },
            { "civ_plastics", "civ_chemicals" }, { "civ_chemicals", "civ_coal" }, { "civ_tools", "common_metals" },
            { "civ_textiles", "leather" }, { "civ_paper", "wood" }
        };

        public static void Clear()
        {
            _boost.Clear();
        }

        public static float Boost(City c)
        {
            float b;
            return c != null && c.data != null && _boost.TryGetValue(c.data.id, out b) ? b : 1f;
        }

        private static IEnumerable<BuildDef> Missing(City c)
        {
            Kingdom k = c.kingdom;
            int pop = c.getPopulationPeople();
            int blds = c.buildings.Count;
            foreach (BuildDef d in Buildings.Defs.Values)
            {
                if (d.Limit <= 0 || d.Type == "type_house" || d.Type == "type_hall") continue;
                if (string.IsNullOrEmpty(d.Tech) || !Research.Has(k, d.Tech)) continue;
                if (pop < d.Pop || blds < d.Blds) continue;
                BuildingAsset a = AssetManager.buildings.get(d.Id);
                if (a == null || !Buildings.Allowed(c, a)) continue;
                if (c.countBuildingsType(d.Type) > 0) continue;
                yield return d;
            }
        }

        private static int Upgradable(City c)
        {
            int n = 0;
            foreach (Building b in c.buildings)
            {
                if (b != null && b.asset != null && b.isUsable() && b.canBeUpgraded()) n++;
            }
            return n;
        }

        public static void CityYear(City c, int yrs)
        {
            if (c == null || c.data == null || c.kingdom == null || !c.kingdom.isCiv()) return;
            int missing = Missing(c).Count();
            int up = Upgradable(c);
            float lag = missing * 0.5f + up * 0.15f;
            _boost[c.data.id] = 1f + Math.Min(4f, lag);
        }

        public static int Scaled(City c, int amount)
        {
            float b = Boost(c);
            if (b <= 1.01f) return amount;
            return Math.Max(1, (int)Math.Ceiling(amount * Math.Max(0.3f, 1f / b)));
        }

        public static bool Has(City c, string res, int amount)
        {
            int n = Scaled(c, amount);
            if (Economy.Has(c, res, n)) return true;
            string s;
            return Subs.TryGetValue(res, out s) && Economy.Has(c, s, n * 2);
        }

        public static void Take(City c, string res, int amount)
        {
            int n = Scaled(c, amount);
            if (Economy.Take(c, res, n)) return;
            string s;
            if (Subs.TryGetValue(res, out s) && Economy.Take(c, s, n * 2)) return;
            Economy.TakeUpTo(c, res, n);
        }

        public static void Chain(Building b, City c)
        {
            if (_chaining || b == null || c == null) return;
            _chaining = true;
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    if (!b.canBeUpgraded()) break;
                    if (!CityBehBuild.upgradeBuilding(b, c)) break;
                }
            }
            finally
            {
                _chaining = false;
            }
        }

        private static Dictionary<string, int> EraKit(int era)
        {
            var kit = new Dictionary<string, int>();
            foreach (BuildDef d in Buildings.Defs.Values)
            {
                if (string.IsNullOrEmpty(d.Tech)) continue;
                TechDef t = Db.Tech(d.Tech);
                if (t == null || t.Era != era) continue;
                for (int i = 0; i < d.Mats.Length && i < d.MatAmounts.Length; i++)
                {
                    int v;
                    kit.TryGetValue(d.Mats[i], out v);
                    kit[d.Mats[i]] = Math.Min(80, v + d.MatAmounts[i] * 2);
                }
            }
            return kit;
        }

        public static void Kit(Kingdom k, int era, int mult = 1)
        {
            if (k == null || !k.isCiv()) return;
            Dictionary<string, int> kit = EraKit(era);
            if (kit.Count == 0) return;
            foreach (City c in G.Cities(k))
            {
                foreach (var kv in kit) Economy.Give(c, kv.Key, kv.Value * mult);
            }
        }

        public static int Develop(Kingdom k)
        {
            if (k == null || !k.isCiv()) return 0;
            int era = Research.Era(k);
            int done = 0;
            for (int e = Math.Max(0, era - 2); e <= era; e++) Kit(k, e, 2);
            foreach (City c in G.Cities(k).ToList())
            {
                foreach (string r in new[] { "wood", "stone", "common_metals", "gold" }) Economy.Give(c, r, 150);
                for (int pass = 0; pass < 8; pass++)
                {
                    bool any = false;
                    foreach (Building b in c.buildings.ToList())
                    {
                        if (b == null || !b.isUsable() || b.isUnderConstruction() || !b.canBeUpgraded()) continue;
                        if (b.upgradeBuilding())
                        {
                            Buildings.Restyle(b, c);
                            any = true;
                            done++;
                        }
                    }
                    if (!any) break;
                }
                int built = 0;
                foreach (BuildDef d in Missing(c).ToList())
                {
                    if (built >= 8) break;
                    BuildingAsset a = AssetManager.buildings.get(d.Id);
                    if (a == null) continue;
                    if (c.under_construction_building != null && c.under_construction_building.isUnderConstruction()) c.under_construction_building.updateBuild(100000);
                    c.under_construction_building = null;
                    Building nb = CityBehBuild.tryToBuild(c, a);
                    if (nb == null) continue;
                    nb.updateBuild(100000);
                    c.under_construction_building = null;
                    built++;
                    done++;
                }
                CityYear(c, 1);
            }
            return done;
        }

        [HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.execute))]
        private static class FasterBuildTick
        {
            private static void Postfix(City pCity)
            {
                if (pCity == null || pCity.timer_build < 4.9f) return;
                float b = Boost(pCity);
                if (b > 1.01f) pCity.timer_build = 5f / b;
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.updateBuild))]
        private static class FasterConstruction
        {
            private static void Prefix(Building __instance, ref int pProgress)
            {
                if (pProgress <= 0 || pProgress >= 1000 || __instance == null) return;
                float b = Boost(__instance.city);
                if (b > 1.01f) pProgress = (int)Math.Ceiling(pProgress * b);
            }
        }
    }
}
