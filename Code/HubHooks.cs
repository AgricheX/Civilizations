using HarmonyLib;

namespace Civilizations
{
    internal static class HubHooks
    {
        [HarmonyPatch(typeof(WarManager), nameof(WarManager.newWar))]
        private static class NewWar
        {
            private static void Postfix(global::War __result, Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pType)
            {
                if (__result == null || !G.Ok(pAttacker) || !G.Ok(pDefender)) return;
                CivMod.Safe("hub-war", () =>
                {
                    string goal = WarGoals.Choose(pAttacker, pDefender, pType != null ? pType.id : "");
                    WarGoals.Set(__result, goal);
                    Hub.Raise(Hub.WarStarted, pAttacker, pDefender, G.Tile(pDefender.capital) != null ? pDefender.capital : null, null, pType != null ? pType.id : "", goal);
                });
            }
        }

        [HarmonyPatch(typeof(WarManager), nameof(WarManager.endWar))]
        private static class EndWar
        {
            private static void Prefix(global::War pWar, WarWinner pWinner)
            {
                if (pWar == null || !pWar.isAlive() || pWar.hasEnded()) return;
                CivMod.Safe("hub-peace", () => Peace.Resolve(pWar, pWinner));
            }
        }

        [HarmonyPatch(typeof(City), nameof(City.joinAnotherKingdom))]
        private static class Captured
        {
            private static void Prefix(City __instance, out Kingdom __state)
            {
                __state = __instance != null ? __instance.kingdom : null;
            }

            private static void Postfix(City __instance, Kingdom pNewSetKingdom, bool pCaptured, bool pRebellion, Kingdom __state)
            {
                if (__instance == null || __state == pNewSetKingdom) return;
                CivMod.Safe("hub-city", () => Hub.Raise(Hub.CityCaptured, pNewSetKingdom, __state, __instance, null, pRebellion ? "rebellion" : pCaptured ? "captured" : "joined"));
            }
        }

        [HarmonyPatch(typeof(CityManager), nameof(CityManager.buildNewCity))]
        private static class Founded
        {
            private static void Postfix(City __result)
            {
                if (__result == null) return;
                CivMod.Safe("hub-newcity", () => Hub.Raise(Hub.CityFounded, __result.kingdom, null, __result));
            }
        }

        [HarmonyPatch(typeof(KingdomManager), nameof(KingdomManager.makeNewCivKingdom))]
        private static class NewKingdom
        {
            private static void Postfix(Kingdom __result, Actor pActor)
            {
                if (__result == null) return;
                CivMod.Safe("hub-newkingdom", () => Hub.Raise(Hub.KingdomFounded, __result, pActor != null ? pActor.kingdom : null, pActor != null ? pActor.city : null, pActor));
            }
        }

        [HarmonyPatch(typeof(KingdomManager), nameof(KingdomManager.removeObject))]
        private static class Gone
        {
            private static void Prefix(Kingdom pKingdom)
            {
                if (pKingdom == null || pKingdom.data == null) return;
                CivMod.Safe("hub-gone", () => Hub.Raise(Hub.KingdomGone, pKingdom));
            }
        }
    }
}
