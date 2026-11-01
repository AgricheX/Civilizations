using HarmonyLib;

namespace Civilizations
{
    internal static class VehicleTraits
    {
        private static bool Locked(Actor a)
        {
            if (a == null || a.asset == null) return false;
            UnitDef d;
            return Db.UnitById.TryGetValue(a.asset.id, out d) && d.Cls != "soldier";
        }

        private static bool Allowed(Actor a, ActorTrait t)
        {
            if (t == null) return false;
            if (t.id.StartsWith("civ_")) return true;
            return a.asset.traits != null && a.asset.traits.Contains(t.id);
        }

        [HarmonyPatch(typeof(Actor), nameof(Actor.addTrait), new[] { typeof(ActorTrait), typeof(bool) })]
        private static class Block
        {
            private static bool Prefix(Actor __instance, ActorTrait pTrait, ref bool __result)
            {
                if (!Locked(__instance) || Allowed(__instance, pTrait)) return true;
                __result = false;
                return false;
            }
        }
    }
}
