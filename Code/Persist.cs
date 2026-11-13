using HarmonyLib;

namespace Civilizations
{
    internal static class Persist
    {
        public static void All()
        {
            if (!G.Loaded) return;
            CivMod.Safe("save-internet", Internet.Save);
            CivMod.Safe("save-companies", Companies.Save);
            CivMod.Safe("save-rails", Rails.Save);
            CivMod.Safe("save-diplomacy", Diplomacy.Save);
            CivMod.Safe("save-orgs", Orgs.Save);
            CivMod.Safe("save-orders", WorldOrders.Save);
            CivMod.Safe("save-fog", Fog.Save);
            CivMod.Safe("save-walls", Walls.Save);
            CivMod.Safe("save-cosmos", Cosmos.Save);
            CivMod.Safe("save-maps", Store.Flush);
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.currentWorldToSavedMap))]
        private static class BeforeSave
        {
            private static void Prefix()
            {
                All();
            }
        }
    }
}
