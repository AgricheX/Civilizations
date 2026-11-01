using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class EraBuildingVisuals
    {
        private const int EraCount = 15;
        private static readonly string[] Families = { "mine", "mill", "barracks", "bonfire", "stockpile", "market", "well", "watchtower" };
        private static readonly int[] FirstEras = { 3, 2, 4, 0, 0, 5, 5, 1 };
        private static readonly Sprite[][] Frames = new Sprite[Families.Length * EraCount][];
        private static readonly bool[] Loaded = new bool[Families.Length * EraCount];
        private static volatile Dictionary<Kingdom, int[]> _stages = new Dictionary<Kingdom, int[]>();
        private static bool _initialized;
        private static float _next;

        private static void Refresh()
        {
            if (!G.Loaded)
            {
                if (_stages.Count != 0) _stages = new Dictionary<Kingdom, int[]>();
                _next = 0f;
                return;
            }
            float now = Time.unscaledTime;
            if (now < _next || Db.Eras.Count == 0) return;
            _next = now + 1f;
            if (!_initialized)
            {
                for (int family = 0; family < Families.Length; family++)
                {
                    for (int era = FirstEras[family]; era < EraCount; era++) GetFrames(family, era);
                }
                _initialized = true;
            }
            var snapshot = new Dictionary<Kingdom, int[]>();
            foreach (Kingdom kingdom in G.Kingdoms())
            {
                if (!G.Ok(kingdom) || !kingdom.isCiv()) continue;
                var stages = new int[Families.Length];
                for (int family = 0; family < Families.Length; family++) stages[family] = Stage(family, kingdom);
                snapshot[kingdom] = stages;
            }
            _stages = snapshot;
        }

        private static int Family(BuildingAsset asset)
        {
            if (asset == null) return -1;
            switch (asset.type)
            {
                case "type_mine": return 0;
                case "type_windmill": return 1;
                case "type_barracks": return 2;
                case "type_bonfire": return 3;
                case "type_stockpile": return 4;
                case "type_market": return 5;
                case "type_well": return 6;
                case "type_watch_tower": return 7;
                default: return -1;
            }
        }

        private static int Stage(int family, Kingdom kingdom)
        {
            int era = Math.Max(0, Math.Min(EraCount - 1, Research.Era(kingdom)));
            if (era < FirstEras[family]) return -1;
            if (family == 0 && (era < 3 || !Research.Has(kingdom, "copper_smelting"))) return -1;
            if (family == 2 && (era < 4 || !Research.Has(kingdom, "standing_army"))) return -1;
            if (family == 5 && !Research.Has(kingdom, "marketplaces")) return -1;
            if (family == 6 && !Research.Has(kingdom, "public_wells")) return -1;
            if (family == 7 && !Research.Has(kingdom, "palisades")) return -1;
            if (family == 1)
            {
                if (era < 2 || !Research.Has(kingdom, "agriculture")) return -1;
                if (era >= 7 && era <= 9 && !Research.Has(kingdom, "windmill"))
                    era = Research.Has(kingdom, "watermill") ? 6 : 5;
                if (era == 6 && !Research.Has(kingdom, "watermill")) era = 5;
            }
            return era;
        }

        private static Sprite[] GetFrames(int family, int era)
        {
            int slot = family * EraCount + era;
            if (Loaded[slot]) return Frames[slot];
            Loaded[slot] = true;
            int count = family == 1 || family == 3 ? 2 : 1;
            var sprites = new Sprite[count];
            string path = "buildings/civ/era_visuals/" + Families[family] + "/" + era + "/main_";
            for (int i = 0; i < count; i++)
            {
                Sprite source = SpriteTextureLoader.getSprite(path + i);
                if (source == null)
                {
                    Debug.LogWarning("[Civilizations] Missing era sprite: " + path + i);
                    foreach (Sprite created in sprites) if (created != null) UnityEngine.Object.Destroy(created);
                    return null;
                }
                source.texture.filterMode = FilterMode.Point;
                Sprite sprite = Sprite.Create(source.texture, source.rect,
                    new Vector2(0.5f, 2f / source.rect.height), 1f);
                sprite.name = "civ_era_" + Families[family] + "_" + era + "_" + i;
                sprites[i] = sprite;
            }
            Frames[slot] = sprites;
            return sprites;
        }

        [HarmonyPatch(typeof(Sim), nameof(Sim.Update))]
        private static class PrepareVisuals
        {
            private static void Postfix()
            {
                CivMod.Safe("era building visuals", Refresh);
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.calculateMainSprite))]
        private static class FinishedBuildingSprite
        {
            private static void Postfix(Building __instance, ref Sprite __result)
            {
                Building b = __instance;
                if (b == null || __result == null) return;
                int family = Family(b.asset);
                if (family < 0 || b.data == null || b.isUnderConstruction() || b.isRuin() ||
                    b.check_spawn_animation || b.isAbandoned()) return;
                City city = b.city;
                if (!G.Ok(city) || !G.Ok(city.kingdom) || !city.kingdom.isCiv()) return;
                if (b.asset.has_special_animation_state && !b.hasResourcesToCollect()) return;
                if (b.asset.spawn_drops && b.data.hasFlag("stop_spawn_drops")) return;
                int[] stages;
                if (!_stages.TryGetValue(city.kingdom, out stages)) return;
                int era = stages[family];
                if (era < 0) return;
                Sprite[] sprites = Frames[family * EraCount + era];
                if (sprites == null) return;
                __result = sprites.Length == 1 ? sprites[0] :
                    AnimationHelper.getSpriteFromList(b.GetHashCode(), sprites, b.asset.animation_speed);
            }
        }
    }
}
