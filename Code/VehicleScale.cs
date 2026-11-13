using HarmonyLib;

namespace Civilizations
{
    internal static class VehicleScale
    {
        internal static float Multiplier(UnitDef unit)
        {
            if (unit == null) return 1f;
            if (unit.Cls == "tank")
            {
                switch (unit.Art)
                {
                    case "land_battleship":
                    case "brass_colossus":
                    case "bio_titan": return 3.2f;
                    case "landship":
                    case "heavy_tank": return 2.8f;
                    case "mbt2":
                    case "railgun_tank":
                    case "jp_tank": return 2.0f;
                    default: return 2.5f;
                }
            }
            if (unit.Cls == "air") return 1.5f;
            if (unit.Cls == "space") return unit.Role == "capital" ? 3f : unit.Role == "line" ? 2.2f : unit.Role == "escort" ? 1.6f : 1.8f;
            if (unit.Cls == "aa") return 2.3f;
            if (unit.Cls == "siege") return unit.Art == "siege_tower" || unit.Art == "trebuchet" ? 2.3f : 2f;
            if (unit.Cls != "land") return 1f;
            if (unit.Art == "apc" || (unit.Art != null && unit.Art.Contains("truck"))) return 2.3f;
            if (unit.Art == "cart" || unit.Art == "chariot" || unit.Art == "carriage") return 1.7f;
            return 1.8f;
        }

        [HarmonyPatch(typeof(Actor), nameof(Actor.updateStats))]
        private static class GroundVehicleSize
        {
            private static void Postfix(Actor __instance)
            {
                if (__instance == null || __instance.asset == null || __instance.stats == null) return;
                UnitDef unit;
                if (!Db.UnitById.TryGetValue(__instance.asset.id, out unit)) return;
                float multiplier = Multiplier(unit);
                if (multiplier == 1f) return;
                float scale = __instance.stats["scale"];
                if (scale > 0f && !float.IsInfinity(scale)) __instance.target_scale = scale * multiplier;
            }
        }
    }
}
