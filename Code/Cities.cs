using System.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Civilizations
{
    internal static class Cities
    {
        private static Cfg C { get { return CivMod.C; } }

        public static bool Alone(MethodBase m)
        {
            if (m == null) return true;
            Patches info = Harmony.GetPatchInfo(m);
            if (info == null) return true;
            foreach (Patch p in info.Transpilers) if (p.owner != "agriche.civilizations") return false;
            return true;
        }

        public static bool On { get { return C == null || C.TinyOn; } }
        public static int ZoneRange { get { return C == null ? 3 : Math.Max(1, C.ZoneRange); } }
        public static int Spacing { get { return C == null ? 1 : Math.Max(0, Math.Min(3, C.Spacing)); } }
        public static int MinExpandWave { get { return On ? Spacing - 1 : 2; } }
        public static int SpacingOrDefault { get { return On ? Spacing : 3; } }
        public static int MinAdults { get { return C == null ? 10 : Math.Max(1, C.MinAdults); } }
        public static int MinCities { get { return C == null ? 2 : Math.Max(1, C.MinCities); } }
        public static int MaxCities { get { return C == null ? 5 : Math.Max(MinCities, C.MaxCities); } }
        public static int MinGround { get { return C == null ? 20 : Math.Max(1, C.MinGround); } }
        public static int MinIsland { get { return C == null ? 100 : Math.Max(1, C.MinIsland); } }
        public static int EmpireCities { get { return Math.Max(3, MaxCities); } }

        [HarmonyPatch(typeof(City), nameof(City.getZoneRange))]
        private static class ZoneRangePatch
        {
            private static bool Prefix(bool pAllowCheat, ref int __result)
            {
                if (!On) return true;
                if (pAllowCheat && DebugConfig.isOn(DebugOption.CityUnlimitedZoneRange)) return true;
                __result = ZoneRange;
                return false;
            }
        }

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getMaxCities))]
        private static class MaxCitiesPatch
        {
            private static void Postfix(Kingdom __instance, ref int __result)
            {
                if (On) __result = Math.Min(MaxCities, Math.Max(MinCities, __result));
                if (__instance == null || __instance.data == null || !__instance.isCiv()) return;
                string s = Politics.Stage(__instance);
                if (s == "tribe" || s == "village" || s == "city_state") __result = Math.Min(__result, 1);
                else if (s == "town") __result = Math.Min(__result, 2);
                else if (s == "empire") __result += 3;
                else if (s == "nation") __result += 1;
            }
        }

        [HarmonyPatch(typeof(CityPlaceFinder), "startWave")]
        private static class PlaceFinderSpacingPatch
        {
            private static bool Prepare(MethodBase original)
            {
                return Cities.Alone(original);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool done = false;
                foreach (CodeInstruction ins in instructions)
                {
                    if (!done && ins.opcode == OpCodes.Ldc_I4_3)
                    {
                        done = true;
                        ins.opcode = OpCodes.Call;
                        ins.operand = AccessTools.PropertyGetter(typeof(Cities), nameof(SpacingOrDefault));
                    }
                    yield return ins;
                }
            }
        }

        [HarmonyPatch(typeof(BehKingCheckNewCityFoundation), "startWave")]
        private static class ExpansionWavePatch
        {
            private static bool Prepare(MethodBase original)
            {
                return Cities.Alone(original);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool done = false;
                CodeInstruction prev = null;
                foreach (CodeInstruction ins in instructions)
                {
                    if (!done && prev != null && prev.opcode == OpCodes.Ldarg_1 && ins.opcode == OpCodes.Ldc_I4_2)
                    {
                        done = true;
                        ins.opcode = OpCodes.Call;
                        ins.operand = AccessTools.PropertyGetter(typeof(Cities), nameof(MinExpandWave));
                    }
                    prev = ins;
                    yield return ins;
                }
            }
        }

        [HarmonyPatch(typeof(BehKingCheckNewCityFoundation), "hasCitiesWithoutPopulation")]
        private static class SmallCityBlockPatch
        {
            private static bool Prefix(Kingdom pKingdom, ref bool __result)
            {
                if (!On) return true;
                __result = false;
                if (pKingdom.capital == null) return false;
                WorldTile capitalTile = pKingdom.capital.getTile();
                if (capitalTile == null) return false;
                int limit = MinAdults;
                foreach (City city in pKingdom.getCities())
                {
                    if (city.countUnits() > limit) continue;
                    WorldTile tile = city.getTile();
                    if (tile != null && tile.reachableFrom(capitalTile))
                    {
                        __result = true;
                        return false;
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(BehKingCheckNewCityFoundation), "getCityListForExpansion")]
        private static class ExpansionCandidatesPatch
        {
            private static bool Prefix(Kingdom pKingdom, ref ListPool<City> __result)
            {
                if (!On) return true;
                var list = new ListPool<City>(pKingdom.countCities());
                int limit = MinAdults;
                foreach (City city in pKingdom.getCities())
                {
                    if (city.getTile() != null && city.status.population_adults >= limit && !city.needSettlers()) list.Add(city);
                }
                list.Shuffle();
                __result = list;
                return false;
            }
        }

        [HarmonyPatch(typeof(TileZone), nameof(TileZone.canStartCityHere))]
        private static class CityGroundPatch
        {
            private static bool Prefix(TileZone __instance, ref bool __result)
            {
                if (!On) return true;
                __result = !(__instance.hasCity() || __instance.tiles_with_ground < MinGround || __instance.hasTilesOfType(TileLibrary.hills));
                return false;
            }
        }

        [HarmonyPatch(typeof(CityPlaceFinder), "prepareBasicZones")]
        private static class IslandSizePatch
        {
            private static bool Prefix()
            {
                if (!On) return true;
                List<TileZone> zones = World.world.zone_calculator.zones;
                int minIsland = MinIsland;
                for (int i = 0; i < zones.Count; i++)
                {
                    TileZone zone = zones[i];
                    if (!zone.canStartCityHere())
                    {
                        zone.setGoodForNewCity(false);
                        continue;
                    }
                    TileIsland island = zone.centerTile != null && zone.centerTile.region != null ? zone.centerTile.region.island : null;
                    if (island == null || island.getTileCount() < minIsland) zone.setGoodForNewCity(false);
                }
                return false;
            }
        }
    }
}
