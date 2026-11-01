using System;
using HarmonyLib;

namespace Civilizations
{
    internal static class Rules
    {
        private const string Key = "civ_rules_applied";
        private static readonly string[] On = { "world_law_gaias_covenant" };
        private const string Key2 = "civ_rules_applied_2";
        private static readonly string[] Off = { "world_law_rites", "world_law_terramorphing", "world_law_disasters_other", "world_law_evolution_events", "world_law_biome_overgrowth" };
        private static readonly string[] Off2 = { "world_law_disasters_other", "world_law_evolution_events", "world_law_biome_overgrowth" };
        private static readonly string[] Needed = { "world_law_diplomacy", "world_law_rebellions" };

        public static void Apply()
        {
            SaveCustomData d = Store.World();
            if (d == null || World.world.world_laws == null || World.world.world_laws.dict == null) return;
            if (!Store.Bool(d, Key2))
            {
                Store.Put(d, Key2, true);
                if (CivMod.C == null || CivMod.C.ApplyRules)
                {
                    foreach (string id in Off2)
                    {
                        if (World.world.world_laws.dict.ContainsKey(id)) World.world.world_laws.dict[id].boolVal = false;
                    }
                    World.world.world_laws.updateCaches();
                }
            }
            if (Store.Bool(d, Key)) return;
            Store.Put(d, Key, true);
            if (CivMod.C == null || !CivMod.C.ApplyRules) return;
            var laws = World.world.world_laws.dict;
            int changed = 0;
            foreach (string id in On)
            {
                if (laws.ContainsKey(id) && !laws[id].boolVal)
                {
                    laws[id].boolVal = true;
                    changed++;
                }
            }
            foreach (string id in Off)
            {
                if (laws.ContainsKey(id) && laws[id].boolVal)
                {
                    laws[id].boolVal = false;
                    changed++;
                }
            }
            World.world.world_laws.updateCaches();
            if (changed > 0) Notify.Show(Txt.T("rules_applied"));
            bool chatRealms = AccessTools.TypeByName("ChatRealms.ChatRealmsMod") != null;
            foreach (string id in Needed)
            {
                if (laws.ContainsKey(id) && !laws[id].boolVal)
                {
                    Notify.Show(Txt.T(chatRealms ? "rules_chatrealms" : "rules_needed"));
                    break;
                }
            }
        }
    }
}
