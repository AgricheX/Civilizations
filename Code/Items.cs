using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Civilizations
{
    internal static class Items
    {
        public const EquipmentType ClothesSlot = (EquipmentType)6;

        private static readonly Dictionary<string, string> MaterialTech = new Dictionary<string, string>
        {
            { "copper", "copper_smelting" }, { "bronze", "bronze_working" }, { "silver", "iron_working" }, { "iron", "iron_working" },
            { "steel", "crucible_steel" }, { "mythril", "crucible_steel" }, { "adamantine", "crucible_steel" }
        };
        private static readonly Dictionary<string, string> SubtypeTech = new Dictionary<string, string>
        {
            { "bow", "bow_and_arrow" }, { "shotgun", "flintlock" }, { "alien_blaster", "lasers" }, { "sword", "copper_tools" }, { "axe", "polished_stone" }, { "spear", "wooden_spear" }
        };
        private static readonly Dictionary<string, string> GunProjectile = new Dictionary<string, string>
        {
            { "musket", "shotgun_bullet" }, { "rifle", "shotgun_bullet" }, { "mg", "shotgun_bullet" }, { "arc", "freeze_orb" },
            { "ray", "green_orb" }, { "plasma", "plasma_ball" }, { "spore", "acid_ball" }, { "rocket", "firebomb" }
        };

        public static void InitStatuses()
        {
            StatusLibrary lib = AssetManager.status;
            Status(lib, "civ_cold", 60f, s =>
            {
                s.base_stats["speed"] = -15f;
                s.base_stats["armor"] = -2f;
            });
            Status(lib, "civ_radiation", 120f, s =>
            {
                s.base_stats["health"] = -30f;
                s.base_stats["speed"] = -10f;
                s.action_interval = 4f;
                s.action = delegate(BaseSimObject pTarget, WorldTile pTile)
                {
                    if (pTarget != null && pTarget.isAlive() && pTarget.isActor()) pTarget.a.getHit(4f, true, AttackType.Other, null, false, false, false);
                    return true;
                };
            });
            Status(lib, "civ_unsupplied", 40f, s =>
            {
                s.base_stats["speed"] = -40f;
                s.base_stats["attack_speed"] = -1f;
                s.base_stats["damage"] = -10f;
            });
            Status(lib, "civ_no_fuel", 40f, s => { s.base_stats["speed"] = -80f; });
            Status(lib, "civ_blackout", 30f, s => { s.base_stats["speed"] = -5f; });
        }

        private static void Status(StatusLibrary lib, string id, float duration, Action<StatusAsset> setup)
        {
            if (lib.get(id) != null) return;
            var s = new StatusAsset
            {
                id = id,
                locale_id = "status_title_" + id,
                locale_description = "status_description_" + id,
                duration = duration,
                path_icon = "ui/Icons/civ/status/" + id.Substring(4)
            };
            setup(s);
            lib.add(s);
        }

        private static void Group(string id, string color)
        {
            if (AssetManager.item_groups.get(id) == null) AssetManager.item_groups.add(new ItemGroupAsset { id = id, name = "civ_item_group_" + id.Substring(4), color = color });
        }

        public static void InitAssets()
        {
            Group("civ_firearm", "#FFD27A");
            Group("civ_uniform", "#9BE07A");
            Group("civ_clothes", "#9AD0FF");
            foreach (ItemDef d in Db.Items)
            {
                if (AssetManager.items.get(d.Id) != null) continue;
                EquipmentAsset t;
                if (d.Kind == "gun")
                {
                    t = AssetManager.items.clone(d.Id, "$range");
                    t.equipment_subtype = "civ_gun";
                    t.group_id = "civ_firearm";
                    string proj;
                    t.projectile = GunProjectile.TryGetValue(d.Proj, out proj) && AssetManager.projectiles.get(proj) != null ? proj : "shotgun_bullet";
                    if (AssetManager.projectiles.get(t.projectile) == null) t.projectile = "arrow";
                    t.path_gameplay_sprite = "items/weapons/w_" + d.Id;
                    t.gameplay_sprites = SpriteTextureLoader.getSpriteList(t.path_gameplay_sprite);
                    t.path_slash_animation = "effects/slashes/slash_punch";
                    t.name_templates = AssetLibrary<EquipmentAsset>.l<string>("shotgun_name");
                    t.base_stats["damage"] = Store.Get(d.Stats, "damage");
                    t.base_stats["range"] = Store.Get(d.Stats, "range");
                    t.base_stats["attack_speed"] = Store.Get(d.Stats, "attack_speed") * 0.02f;
                    t.base_stats["targets"] = 1f;
                    t.base_stats["accuracy"] = 0.2f;
                    t.metallic = true;
                    t.rigidity_rating = 5;
                    t.setCost(10, "civ_steel", 1);
                }
                else if (d.Kind == "uniform")
                {
                    t = AssetManager.items.clone(d.Id, "$armor");
                    t.equipment_subtype = "civ_uniform";
                    t.group_id = "civ_uniform";
                    t.base_stats["armor"] = Store.Get(d.Stats, "armor");
                    t.setCost(4, "civ_textiles", 2);
                }
                else
                {
                    t = AssetManager.items.clone(d.Id, "$armor");
                    t.equipment_type = ClothesSlot;
                    t.equipment_subtype = "civ_clothes";
                    t.group_id = "civ_clothes";
                    t.name_class = "civ_item_class_clothes";
                    t.base_stats["armor"] = Store.Get(d.Stats, "armor");
                    t.setCost(2, "civ_clothes", 1);
                }
                t.material = "basic";
                t.is_pool_weapon = false;
                t.pool_rate = 0;
                t.translation_key = "item_" + d.Id;
                t.path_icon = "ui/Icons/civ/item/" + d.Id;
                t.equipment_value = 100 + Db.EraOf(d.Tech) * 60 + (int)(Store.Get(d.Stats, "damage") / 10f + Store.Get(d.Stats, "armor") / 4f);
                t.needs_to_be_explored = false;
                List<EquipmentAsset> list;
                if (!AssetManager.items.equipment_by_subtypes.TryGetValue(t.equipment_subtype, out list))
                {
                    list = new List<EquipmentAsset>();
                    AssetManager.items.equipment_by_subtypes.Add(t.equipment_subtype, list);
                }
                if (!list.Contains(t)) list.Add(t);
            }
        }

        public static bool Allowed(Kingdom k, EquipmentAsset e)
        {
            if (e == null) return false;
            if (k == null || !k.isCiv()) return true;
            ItemDef d;
            if (Db.ItemById.TryGetValue(e.id, out d))
            {
                if (!Research.Has(k, d.Tech) || !JapanStyle.Allows(k, d.Id)) return false;
                if (!string.IsNullOrEmpty(d.Route) && d.Route != Research.Route(k) && Research.Route(k) != "") return false;
                return true;
            }
            string tech;
            if (!string.IsNullOrEmpty(e.equipment_subtype) && SubtypeTech.TryGetValue(e.equipment_subtype, out tech) && !Research.Has(k, tech)) return false;
            if (!string.IsNullOrEmpty(e.material) && MaterialTech.TryGetValue(e.material, out tech) && !Research.Has(k, tech)) return false;
            return true;
        }

        public static EquipmentAsset Best(Kingdom k, string kind, int minValue)
        {
            EquipmentAsset best = null;
            string route = Research.Route(k);
            foreach (ItemDef d in Db.Items)
            {
                if (d.Kind != kind || !Research.Has(k, d.Tech) || !JapanStyle.Allows(k, d.Id)) continue;
                if (!string.IsNullOrEmpty(d.Route) && d.Route != route) continue;
                EquipmentAsset e = AssetManager.items.get(d.Id);
                if (e == null || e.equipment_value <= minValue) continue;
                if (best == null || e.equipment_value > best.equipment_value) best = e;
            }
            return best;
        }

        public static float GunShare(Kingdom k)
        {
            int era = Research.Era(k);
            if (Research.Has(k, "assault_rifle")) return 0.95f;
            if (Research.Has(k, "machine_gun")) return 0.85f;
            if (Research.Has(k, "rifling")) return 0.75f;
            if (Research.Has(k, "flintlock")) return 0.5f;
            if (Research.Has(k, "musket")) return 0.3f;
            if (Research.Has(k, "arquebus")) return 0.15f;
            return era >= 13 ? 0.95f : 0f;
        }

        public static void ColdYear(City c)
        {
            WorldTile t = G.Tile(c);
            if (t == null || t.Type == null || t.Type.biome_asset == null || !t.Type.biome_asset.cold_biome) return;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a)) continue;
                float warmth = Clothes.Warmth(a);
                if (warmth >= 0.8f) continue;
                if (!G.Chance((1f - warmth) * 0.5f)) continue;
                if (warmth < 0.3f && !a.hasStatus("civ_cold")) a.addStatusEffect("civ_cold", 60f);
                else if (G.Chance(0.3f)) a.changeHappiness("civ_cold");
            }
        }

        private static bool Equip(Actor a, City c, EquipmentAsset best, string creator, int tries, string mat, int amount)
        {
            ActorEquipmentSlot slot = a.equipment.getSlot(best.equipment_type);
            Item cur = slot.getItem();
            if (cur != null && cur.isCursed()) return false;
            Item item = World.world.items.generateItem(best, a.kingdom, creator, tries, a);
            if (!slot.isEmpty())
            {
                Item old = slot.getItem();
                slot.takeAwayItem();
                c.tryToPutItem(old);
            }
            slot.setItem(item, a);
            if (mat != null) Economy.Take(c, mat, amount);
            return true;
        }

        [HarmonyPatch(typeof(ItemCrafting), "hasEnoughResourcesToCraft")]
        private static class CraftGate
        {
            private static void Postfix(Actor pActor, EquipmentAsset pAsset, City pCity, ref bool __result)
            {
                if (!__result || pCity == null) return;
                if (!Allowed(pCity.kingdom, pAsset)) __result = false;
            }
        }

        public static void FixSprites()
        {
            EquipmentAsset shotgun = AssetManager.items.get("shotgun") as EquipmentAsset;
            foreach (ItemDef d in Db.Items)
            {
                if (d.Kind != "gun") continue;
                EquipmentAsset e = AssetManager.items.get(d.Id) as EquipmentAsset;
                if (e == null) continue;
                bool bad = e.gameplay_sprites == null || e.gameplay_sprites.Length == 0 || shotgun != null && e.gameplay_sprites == shotgun.gameplay_sprites;
                if (!bad) continue;
                UnityEngine.Sprite[] s = SpriteTextureLoader.getSpriteList("items/weapons/w_" + d.Id);
                if (s != null && s.Length > 0) e.gameplay_sprites = s;
            }
            foreach (ResourceAsset r in AssetManager.resources.list)
            {
                if (r == null || r.gameplay_sprites != null && r.gameplay_sprites.Length > 0 || string.IsNullOrEmpty(r.full_sprite_path)) continue;
                UnityEngine.Sprite[] s = SpriteTextureLoader.getSpriteList(r.full_sprite_path);
                if (s != null && s.Length > 0) r.gameplay_sprites = s;
            }
        }

        [HarmonyPatch(typeof(ItemRendering), nameof(ItemRendering.getItemMainSpriteFrame))]
        private static class SafeSprite
        {
            private static bool Prefix(IHandRenderer pHandRendererAsset, ref UnityEngine.Sprite __result)
            {
                if (pHandRendererAsset == null) return true;
                UnityEngine.Sprite[] s = pHandRendererAsset.getSprites();
                if (s != null && s.Length > 0) return true;
                EquipmentAsset e = pHandRendererAsset as EquipmentAsset;
                if (e != null)
                {
                    EquipmentAsset fallback = AssetManager.items.get("shotgun") as EquipmentAsset;
                    if (fallback != null && fallback.gameplay_sprites != null && fallback.gameplay_sprites.Length > 0)
                    {
                        e.gameplay_sprites = fallback.gameplay_sprites;
                        return true;
                    }
                }
                ResourceAsset r = pHandRendererAsset as ResourceAsset;
                if (r != null)
                {
                    if ((r.gameplay_sprites == null || r.gameplay_sprites.Length == 0) && AssetManager.resources.get("wood") != null) r.gameplay_sprites = AssetManager.resources.get("wood").gameplay_sprites;
                    if (r.gameplay_sprites != null && r.gameplay_sprites.Length > 0) return true;
                }
                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(ItemCrafting), nameof(ItemCrafting.tryToCraftRandomWeapon))]
        private static class SafeWeapon
        {
            private static Exception Finalizer(Exception __exception, ref bool __result)
            {
                if (__exception != null)
                {
                    __result = false;
                    CivMod.Log("craft weapon: " + __exception.Message);
                }
                return null;
            }
        }

        [HarmonyPatch(typeof(ItemCrafting), nameof(ItemCrafting.tryToCraftRandomArmor))]
        private static class SafeArmor
        {
            private static Exception Finalizer(Exception __exception, ref bool __result)
            {
                if (__exception != null)
                {
                    __result = false;
                    CivMod.Log("craft armor: " + __exception.Message);
                }
                return null;
            }
        }

        [HarmonyPatch(typeof(ItemCrafting), nameof(ItemCrafting.craftItem))]
        private static class Modern
        {
            private static bool Prefix(Actor pActor, string pCreatorName, EquipmentType pType, int pTries, City pCity, ref bool __result)
            {
                if (pActor == null || pActor.equipment == null || pCity == null)
                {
                    __result = false;
                    return false;
                }
                Item held = pActor.equipment.getSlot(pType).getItem();
                if (held != null && held.asset == null)
                {
                    pActor.equipment.getSlot(pType).takeAwayItem();
                }
                if (pActor == null || pCity == null || pCity.kingdom == null || !pCity.kingdom.isCiv()) return true;
                Kingdom k = pCity.kingdom;
                try
                {
                    if (pType == EquipmentType.Weapon)
                    {
                        float share = GunShare(k);
                        if (share <= 0f || !G.Chance(share)) return true;
                        Item cur = pActor.equipment.getSlot(pType).getItem();
                        int curValue = cur != null ? cur.asset.equipment_value : 0;
                        EquipmentAsset best = Best(k, "gun", curValue);
                        if (best == null || !Economy.Has(pCity, "civ_steel", 1) && !Economy.Has(pCity, "common_metals", 2)) return true;
                        string mat = Economy.Has(pCity, "civ_steel", 1) ? "civ_steel" : "common_metals";
                        __result = Equip(pActor, pCity, best, pCreatorName, pTries, mat, mat == "civ_steel" ? 1 : 2);
                        return !__result;
                    }
                    if (pType == EquipmentType.Armor || pType == EquipmentType.Helmet)
                    {
                        if (!pActor.isWarrior()) return true;
                        Item cur = pActor.equipment.getSlot(EquipmentType.Armor).getItem();
                        int curValue = cur != null ? cur.asset.equipment_value : 0;
                        EquipmentAsset best = Best(k, "uniform", curValue);
                        if (best == null || !Economy.Has(pCity, "civ_textiles", 2)) return true;
                        __result = Equip(pActor, pCity, best, pCreatorName, pTries, "civ_textiles", 2);
                        return !__result;
                    }
                }
                catch (Exception e)
                {
                    CivMod.Log("craft: " + e.Message);
                }
                return true;
            }
        }
    }
}
