using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class Shelter
    {
        private static Sprite _sprite;
        private static bool _tried;

        private static Sprite Get()
        {
            if (_tried) return _sprite;
            _tried = true;
            Sprite s = SpriteTextureLoader.getSprite("buildings/civ/civ_shelter/main_0");
            if (s == null) return null;
            s.texture.filterMode = FilterMode.Point;
            _sprite = Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 1f / s.rect.height), 1f);
            _sprite.name = "civ_shelter";
            return _sprite;
        }

        [HarmonyPatch(typeof(Building), nameof(Building.calculateMainSprite))]
        private static class Paint
        {
            private static void Postfix(Building __instance, ref Sprite __result)
            {
                Building b = __instance;
                if (b == null || __result == null || b.asset == null || b.asset.type != "type_house") return;
                if (b.isUnderConstruction() || b.isRuin() || b.check_spawn_animation) return;
                if (Buildings.HouseTier(b.asset) > 1 || b.asset.id.StartsWith("civ_")) return;
                City c = b.city;
                if (!G.Ok(c) || !G.Ok(c.kingdom) || !c.kingdom.isCiv() || Research.Era(c.kingdom) > 0) return;
                Sprite s = Get();
                if (s != null) __result = s;
            }
        }
    }
}
