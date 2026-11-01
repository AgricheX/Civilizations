using System;

namespace Civilizations
{
    internal static class Docks
    {
        public const int Tiers = 5;

        public static string Id(int tier)
        {
            return "civ_docks_" + tier;
        }

        public static void InitAssets()
        {
            if (AssetManager.buildings.get("docks_human") == null) return;
            for (int t = 1; t <= Tiers; t++)
            {
                string id = Id(t);
                if (AssetManager.buildings.get(id) != null) continue;
                BuildingAsset b = AssetManager.buildings.clone(id, "docks_human");
                b.group = "civ";
                b.main_path = "buildings/civ/";
                b.sprite_path = "buildings/civ/" + id;
                b.sprites_are_initiated = false;
                b.building_sprites = null;
                b.has_sprites_main_disabled = false;
                b.has_sprite_construction = false;
                b.has_sprites_ruin = true;
                b.has_sprites_main = true;
                b.can_be_upgraded = false;
                b.upgrade_to = string.Empty;
                b.upgraded_from = string.Empty;
                b.upgrade_level = 1 + t;
                b.burnable = t <= 1;
                if (b.base_stats == null) b.base_stats = new BaseStats();
                b.base_stats["health"] = 1200f + t * 900f;
                b.draw_light_area = true;
                b.draw_light_size = 0.4f + t * 0.1f;
                b.atlas_asset = AssetManager.dynamic_sprites_library.get(b.atlas_id) ?? AssetManager.dynamic_sprites_library.get("buildings");
            }
        }

        public static int TierFor(int era)
        {
            if (era >= 14) return 5;
            if (era >= 12) return 4;
            if (era >= 10) return 3;
            if (era >= 8) return 2;
            if (era >= 5) return 1;
            return 0;
        }

        public static int TierOf(BuildingAsset a)
        {
            if (a == null || !a.id.StartsWith("civ_docks_")) return 0;
            int n;
            return int.TryParse(a.id.Substring(10), out n) ? n : 0;
        }

        public static BuildingAsset Wanted(Building b, City c)
        {
            if (b == null || b.asset == null || c == null || c.kingdom == null || !c.kingdom.isCiv()) return null;
            try
            {
                int tier = TierFor(Research.Era(c.kingdom));
                int cur = TierOf(b.asset);
                if (tier == cur) return null;
                if (tier == 0)
                {
                    return Buildings.Arch(c, "order_docks_1") ?? AssetManager.buildings.get("docks_human");
                }
                if (cur == 0 && !Research.Has(c.kingdom, "the_sail") && b.asset.boat_types != null && b.asset.boat_types.Length < 2) return null;
                return AssetManager.buildings.get(Id(tier));
            }
            catch (Exception e)
            {
                CivMod.Log("docks: " + e.Message);
                return null;
            }
        }
    }
}
