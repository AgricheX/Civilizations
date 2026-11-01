using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Civilizations
{
    internal static class Storage
    {
        public const int Cap = 99999;
        private static readonly float[] EraFactor = { 1f, 1.3f, 1.8f, 2.5f, 3.3f, 4.5f, 6f, 8f, 11f, 15f, 22f, 32f, 45f, 62f, 85f };
        private static readonly Dictionary<string, KeyValuePair<int, int>> _vanilla = new Dictionary<string, KeyValuePair<int, int>>();
        private static readonly Dictionary<long, KeyValuePair<int, float>> _cache = new Dictionary<long, KeyValuePair<int, float>>();

        public static void InitAssets()
        {
            foreach (ResourceAsset r in AssetManager.resources.list)
            {
                if (r == null || _vanilla.ContainsKey(r.id)) continue;
                _vanilla[r.id] = new KeyValuePair<int, int>(Math.Max(1, r.maximum), Math.Max(1, r.storage_max));
                r.maximum = Cap;
            }
        }

        private static KeyValuePair<int, int> Vanilla(ResourceAsset r)
        {
            KeyValuePair<int, int> v;
            if (_vanilla.TryGetValue(r.id, out v)) return v;
            v = new KeyValuePair<int, int>(999, Math.Max(1, r.storage_max));
            _vanilla[r.id] = v;
            if (r.maximum < Cap) r.maximum = Cap;
            return v;
        }

        public static float Multiplier(City c)
        {
            if (c == null || c.data == null) return 1f;
            KeyValuePair<int, float> hit;
            int year = G.Year;
            if (_cache.TryGetValue(c.data.id, out hit) && hit.Key == year) return hit.Value;
            int era = Math.Max(0, Math.Min(EraFactor.Length - 1, Research.Era(c)));
            float bonus = 0f;
            foreach (Building b in c.buildings)
            {
                if (b == null || b.asset == null || !b.isUsable()) continue;
                BuildingAsset a = b.asset;
                if (a.type == "type_hall") bonus += 0.6f;
                else if (a.type == "type_stockpile") bonus += 0.5f;
                else if (a.type == "type_docks" || a.type == "type_market") bonus += 0.25f;
                else if (a.type == "type_windmill") bonus += 0.15f;
                else if (a.type == "type_civ_bank" || a.type == "type_civ_station" || a.type == "type_civ_airport") bonus += 0.35f;
                else if (a.type == "type_civ_factory") bonus += 0.2f;
                else if (a.storage) bonus += 0.3f;
            }
            float m = EraFactor[era] * (1f + Math.Min(6f, bonus));
            _cache[c.data.id] = new KeyValuePair<int, float>(year, m);
            return m;
        }

        public static int CityHard(City c, ResourceAsset r)
        {
            return (int)Math.Min(Cap, Vanilla(r).Key * Multiplier(c));
        }

        public static int CitySoft(City c, ResourceAsset r)
        {
            return (int)Math.Min(CityHard(c, r), Vanilla(r).Value * Multiplier(c));
        }

        private static int Storages(City c)
        {
            return c == null ? 1 : Math.Max(1, c.storages.Count);
        }

        public static int BuildingHard(Building b, ResourceAsset r)
        {
            if (b == null || b.city == null) return Vanilla(r).Key;
            return Math.Max(Vanilla(r).Key, CityHard(b.city, r) / Storages(b.city));
        }

        public static int BuildingSoft(Building b, ResourceAsset r)
        {
            if (b == null || b.city == null) return Vanilla(r).Value;
            return Math.Max(Vanilla(r).Value, CitySoft(b.city, r) / Storages(b.city));
        }

        [HarmonyPatch(typeof(Building), nameof(Building.addResources))]
        private static class Add
        {
            private static void Postfix(Building __instance, string pResourceID, ref int __result)
            {
                if (__result <= 0 || __instance == null || __instance.resources == null) return;
                ResourceAsset r = AssetManager.resources.get(pResourceID);
                if (r == null) return;
                int cap = BuildingHard(__instance, r);
                if (__result > cap)
                {
                    __instance.resources.set(pResourceID, cap);
                    __result = cap;
                }
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.hasSpaceForResource))]
        private static class Space
        {
            private static bool Prefix(Building __instance, ResourceAsset pResourceAsset, ref bool __result)
            {
                if (__instance == null || __instance.resources == null || pResourceAsset == null) return true;
                __result = __instance.resources.get(pResourceAsset.id) < BuildingSoft(__instance, pResourceAsset);
                return false;
            }
        }
    }
}
