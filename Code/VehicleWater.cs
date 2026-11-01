using System.Collections.Generic;
using HarmonyLib;

namespace Civilizations
{
    internal static class VehicleWater
    {
        private static readonly Dictionary<ActorAsset, bool> Sinks = new Dictionary<ActorAsset, bool>();

        private static bool Sinkable(ActorAsset asset)
        {
            bool r;
            if (Sinks.TryGetValue(asset, out r)) return r;
            UnitDef d;
            r = Db.UnitById.TryGetValue(asset.id, out d) && d != null
                && (d.Cls == "land" || d.Cls == "tank" || d.Cls == "siege" || d.Cls == "aa")
                && d.Art != "bio_titan";
            Sinks[asset] = r;
            return r;
        }

        [HarmonyPatch(typeof(Actor), nameof(Actor.u5_curTileAction))]
        private static class Sink
        {
            private static void Postfix(Actor __instance)
            {
                WorldTile t = __instance.current_tile;
                if (t == null || t.Type == null || !t.Type.ocean) return;
                if (__instance.asset == null || !Sinkable(__instance.asset)) return;
                if (!__instance.isAlive() || __instance.position_height > 0f || __instance.is_inside_boat || __instance.isInsideSomething() || __instance.isFlying()) return;
                __instance.getHit(__instance.getMaxHealth() * 10f + 1000f, true, AttackType.Water, null, false, false, false);
            }
        }
    }
}
