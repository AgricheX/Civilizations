using System.Collections.Generic;
using HarmonyLib;

namespace Civilizations
{
    internal static class ClanChaosNaturalBlock
    {
        private static bool IsBlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            switch (id)
            {
                case "void_ban":
                case "cursed_blood":
                case "nitroglycerin_blood":
                case "antimatter_blood":
                    return true;
            }
            ClanTrait trait = AssetManager.clan_traits != null ? AssetManager.clan_traits.get(id) : null;
            return trait != null && trait.group_id == "chaos";
        }

        private static bool IsBlockedTrait(ClanTrait trait)
        {
            return trait != null && (trait.group_id == "chaos" || IsBlocked(trait.id));
        }

        private static void DisableRandom(ClanTraitLibrary library)
        {
            if (library == null) return;
            foreach (ClanTrait trait in library.list)
            {
                if (IsBlockedTrait(trait)) trait.spawn_random_trait_allowed = false;
            }
            if (library._pot_allowed_to_be_given_randomly != null)
                library._pot_allowed_to_be_given_randomly.RemoveAll(IsBlockedTrait);
        }

        private static List<string> AllowedOnly(List<string> source)
        {
            if (source == null) return null;
            List<string> filtered = null;
            for (int i = 0; i < source.Count; i++)
            {
                string id = source[i];
                if (IsBlocked(id))
                {
                    if (filtered != null) continue;
                    filtered = new List<string>(source.Count);
                    for (int j = 0; j < i; j++) filtered.Add(source[j]);
                }
                else if (filtered != null) filtered.Add(id);
            }
            return filtered ?? source;
        }

        [HarmonyPatch(typeof(Clan), nameof(Clan.default_traits), MethodType.Getter)]
        private static class ClanDefaults
        {
            [HarmonyPrepare]
            private static bool Prepare()
            {
                DisableRandom(AssetManager.clan_traits);
                return true;
            }

            private static void Postfix(ref List<string> __result)
            {
                __result = AllowedOnly(__result);
            }
        }

        [HarmonyPatch(typeof(ClanTraitLibrary), nameof(ClanTraitLibrary.getDefaultTraitsForMeta))]
        private static class LibraryDefaults
        {
            private static void Postfix(ref List<string> __result)
            {
                __result = AllowedOnly(__result);
            }
        }

        [HarmonyPatch(typeof(ClanTraitLibrary), nameof(ClanTraitLibrary.init))]
        private static class LibraryInit
        {
            private static void Postfix(ClanTraitLibrary __instance)
            {
                DisableRandom(__instance);
            }
        }

        [HarmonyPatch(typeof(ClanManager), nameof(ClanManager.addRandomTraitFromBiomeToClan))]
        private static class BiomeTraits
        {
            private static bool Prefix(Clan pClan, WorldTile pTile)
            {
                if (pClan == null || pTile == null || pTile.Type == null) return true;
                BiomeAsset biome = pTile.Type.biome_asset;
                if (biome == null) return true;
                List<string> source = biome.spawn_trait_clan;
                List<string> filtered = AllowedOnly(source);
                if (ReferenceEquals(source, filtered)) return true;
                pClan.addRandomTraitFromBiome<ClanTrait>(pTile, filtered, AssetManager.clan_traits);
                return false;
            }
        }
    }
}
