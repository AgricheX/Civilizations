using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class StillBuildings
    {
        private static readonly HashSet<string> Ids = new HashSet<string> { "civ_plant_wind", "civ_plant_tesla", "civ_plant_bio", "civ_plant_biomass" };

        private class Frozen
        {
            public BuildingAsset Asset;
            public Sprite Sprite;
        }

        private static readonly ConditionalWeakTable<Building, Frozen> _still = new ConditionalWeakTable<Building, Frozen>();

        [HarmonyPatch(typeof(Building), nameof(Building.calculateMainSprite))]
        private static class Hold
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Building __instance, ref Sprite __result)
            {
                Building b = __instance;
                if (b == null || __result == null || b.asset == null || !Ids.Contains(b.asset.id)) return;
                if (b.isUnderConstruction() || b.isRuin() || b.check_spawn_animation) return;
                Frozen f;
                if (_still.TryGetValue(b, out f) && f.Asset == b.asset && f.Sprite != null)
                {
                    __result = f.Sprite;
                    return;
                }
                _still.Remove(b);
                _still.Add(b, new Frozen { Asset = b.asset, Sprite = __result });
            }
        }
    }
}
