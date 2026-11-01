using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Civilizations
{
    internal static class Clothes
    {
        private class Holder
        {
            public ActorEquipmentSlot Slot = new ActorEquipmentSlot(Items.ClothesSlot);
        }

        private class CityHolder
        {
            public List<long> List = new List<long>();
        }

        private static readonly ConditionalWeakTable<ActorEquipment, Holder> _slots = new ConditionalWeakTable<ActorEquipment, Holder>();
        private static readonly ConditionalWeakTable<CityEquipment, CityHolder> _city = new ConditionalWeakTable<CityEquipment, CityHolder>();

        public const string KWear = "civ_wear";

        public static float Warmth(Actor a)
        {
            if (!G.Ok(a)) return 0f;
            float w = 0f;
            ItemDef d;
            string id = Wearing(a);
            if (id.Length > 0 && Db.ItemById.TryGetValue(id, out d)) w += d.Warmth;
            try
            {
                if (a.equipment != null && a.equipment.getSlot(EquipmentType.Armor).getItem() != null) w += 0.3f;
            }
            catch (Exception)
            {
            }
            return w;
        }

        public static string Wearing(Actor a)
        {
            if (!G.Ok(a) || a.data == null) return "";
            string id = Store.Str(a.data, KWear);
            if (id.Length > 0) return id;
            if (a.equipment == null) return "";
            try
            {
                Item c = a.equipment.getSlot(Items.ClothesSlot).getItem();
                return c != null && c.asset != null ? c.asset.id : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static void Virtualize(Actor a)
        {
            if (a.equipment == null) return;
            try
            {
                ActorEquipmentSlot slot = a.equipment.getSlot(Items.ClothesSlot);
                Item old = slot.getItem();
                if (old == null) return;
                if (old.asset != null && Store.Str(a.data, KWear).Length == 0) Store.Put(a.data, KWear, old.asset.id);
                slot.takeAwayItem();
                old.clearUnit();
                old.setShouldBeRemoved();
            }
            catch (Exception)
            {
            }
        }

        public static void Year(City c, int yrs)
        {
            Kingdom k = c.kingdom;
            if (k == null || !k.isCiv()) return;
            EquipmentAsset best = Items.Best(k, "cloth", 0);
            if (best == null) return;
            ItemDef bd = Db.ItemById[best.id];
            int budget = Math.Max(1, c.getPopulationPeople() / 6) * yrs;
            bool cold = false;
            WorldTile t = G.Tile(c);
            if (t != null && t.Type != null && t.Type.biome_asset != null && t.Type.biome_asset.cold_biome) cold = true;
            int given = 0;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !a.isSapient() || Vehicles.IsVehicle(a)) continue;
                Virtualize(a);
                if (budget <= 0) continue;
                string curId = Wearing(a);
                EquipmentAsset cur = curId.Length > 0 ? AssetManager.items.get(curId) as EquipmentAsset : null;
                int curValue = cur != null ? cur.equipment_value : 0;
                if (curValue >= best.equipment_value) continue;
                EquipmentAsset pick = best;
                if (cold && bd.Warmth < 0.8f)
                {
                    EquipmentAsset warm = WarmestAvailable(k);
                    if (warm != null && (cur == null || warm.equipment_value > curValue)) pick = warm;
                }
                bool slave = Xeno.Status(a) == Xeno.Slave;
                if (slave && cur != null) continue;
                string mat = Economy.Has(c, "civ_clothes", 1) ? "civ_clothes" : Economy.Has(c, "leather", 2) ? "leather" : null;
                if (mat == null)
                {
                    budget = 0;
                    continue;
                }
                Economy.Take(c, mat, mat == "leather" ? 2 : 1);
                Store.Put(a.data, KWear, pick.id);
                budget--;
                given++;
                if (G.Chance(0.3f)) a.changeHappiness("civ_new_clothes");
            }
            if (given > 0) Store.Put(c.data, "civ_cloth_year", G.Year);
        }

        private static EquipmentAsset WarmestAvailable(Kingdom k)
        {
            EquipmentAsset best = null;
            float bw = 0f;
            foreach (ItemDef d in Db.Items)
            {
                if (d.Kind != "cloth" || !Research.Has(k, d.Tech)) continue;
                if (!string.IsNullOrEmpty(d.Route) && d.Route != Research.Route(k)) continue;
                if (d.Warmth > bw)
                {
                    bw = d.Warmth;
                    best = AssetManager.items.get(d.Id);
                }
            }
            return best;
        }

        [HarmonyPatch(typeof(ActorEquipment), "initDictionary")]
        private static class SlotPatch
        {
            private static void Postfix(ActorEquipment __instance)
            {
                Holder h = _slots.GetValue(__instance, x => new Holder());
                if (!__instance._dictionary.ContainsKey(Items.ClothesSlot)) __instance._dictionary.Add(Items.ClothesSlot, h.Slot);
            }
        }

        [HarmonyPatch(typeof(CityEquipment), nameof(CityEquipment.init))]
        private static class CitySlotPatch
        {
            private static void Postfix(CityEquipment __instance)
            {
                CityHolder h = _city.GetValue(__instance, x => new CityHolder());
                if (!__instance.items_dicts.ContainsKey(Items.ClothesSlot)) __instance.items_dicts.Add(Items.ClothesSlot, h.List);
            }
        }

        [HarmonyPatch(typeof(ItemLibrary), nameof(ItemLibrary.getEquipmentType))]
        private static class TypeName
        {
            private static void Postfix(EquipmentType pType, ref string __result)
            {
                if (__result == null && pType == Items.ClothesSlot) __result = "civ_clothes";
            }
        }
    }
}
