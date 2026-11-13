using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Civilizations
{
    internal static class Units
    {
        private static readonly string[] MilitaryDecisions =
        {
            "check_swearing", "warrior_try_join_army_group", "city_walking_to_danger_zone", "warrior_army_captain_idle_walking_city",
            "warrior_army_captain_waiting", "warrior_army_leader_move_random", "warrior_army_leader_move_to_attack_target",
            "warrior_army_follow_leader", "warrior_random_move", "check_warrior_transport", "swim_to_island"
        };

        private static readonly List<ActorAsset> _created = new List<ActorAsset>();

        public static bool IsVehicle(ActorAsset a)
        {
            return a != null && _created.Contains(a);
        }

        private static HashSet<DecisionAsset> _blocked;

        private static bool IsVehicle(Actor a)
        {
            return a != null && IsVehicle(a.asset);
        }

        [HarmonyPatch(typeof(City), nameof(City.setLeader))]
        private static class NoVehicleLeader
        {
            private static bool Prefix(Actor pActor)
            {
                return !IsVehicle(pActor);
            }
        }

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.setKing))]
        private static class NoVehicleKing
        {
            private static bool Prefix(Actor pActor)
            {
                return !IsVehicle(pActor);
            }
        }

        [HarmonyPatch(typeof(ai.behaviours.CityBehCheckLeader), nameof(ai.behaviours.CityBehCheckLeader.execute))]
        private static class DropVehicleLeader
        {
            private static void Prefix(City pCity)
            {
                if (pCity != null && IsVehicle(pCity.leader)) pCity.removeLeader();
            }
        }

        [HarmonyPatch(typeof(ai.behaviours.KingdomBehCheckKing), nameof(ai.behaviours.KingdomBehCheckKing.execute))]
        private static class DropVehicleKing
        {
            private static void Prefix(Kingdom pKingdom)
            {
                if (pKingdom != null && IsVehicle(pKingdom.king)) pKingdom.kingLeftEvent();
            }
        }

        [HarmonyPatch(typeof(ActorAvatarData), nameof(ActorAvatarData.setData), new[] { typeof(Actor) })]
        private static class SafeAvatar
        {
            private static bool Prefix(ActorAvatarData __instance, Actor pActor)
            {
                if (pActor == null || pActor.equipment != null && pActor.kingdom != null) return true;
                ActorAsset asset = pActor.getActorAsset();
                ActorData data = pActor.data;
                __instance.setData(asset, pActor.subspecies?.mutation_skin_asset, data.sex, data.id, data.head, pActor.cached_sprite_head, data.phenotype_index, data.phenotype_shade, pActor.kingdom != null ? pActor.kingdom.getColor() : null, pActor.isEgg(), pActor.isKing(), false, pActor.hasTrait("wise"), pActor.subspecies?.egg_asset, pActor.isAdult(), !asset.prevent_unconscious_rotation && pActor.isLying(), pActor.isTouchingLiquid(), pActor.is_inside_boat, pActor.isHovering(), pActor.isImmovable(), !asset.prevent_unconscious_rotation && pActor.is_unconscious, pActor.hasStopIdleAnimation(), pActor.getHandRendererAsset(), pActor.GetHashCode(), pActor.getStatusesIds(), pActor.getStatusesDict());
                return false;
            }
        }

        [HarmonyPatch(typeof(Actor), "registerDecisions")]
        private static class VehicleDecisions
        {
            private static void Postfix(Actor __instance)
            {
                ActorAsset a = __instance.asset;
                if (!IsVehicle(a)) return;
                if (_blocked == null)
                {
                    _blocked = new HashSet<DecisionAsset>();
                    foreach (MetaTypeAsset m in new[] { MetaTypeLibrary.city, MetaTypeLibrary.family, MetaTypeLibrary.plot, MetaTypeLibrary.clan })
                    {
                        if (m == null || m.decisions_assets == null) continue;
                        foreach (DecisionAsset d in m.decisions_assets) if (d != null) _blocked.Add(d);
                    }
                }
                List<string> own = a.decision_ids;
                int n = 0;
                for (int i = 0; i < __instance.decisions_counter; i++)
                {
                    DecisionAsset d = __instance.decisions[i];
                    if (d == null) continue;
                    if (_blocked.Contains(d) && (own == null || !own.Contains(d.id))) continue;
                    __instance.decisions[n++] = d;
                }
                for (int i = n; i < __instance.decisions_counter; i++) __instance.decisions[i] = null;
                __instance.decisions_counter = n;
            }
        }

        public static void InitAssets()
        {
            Missiles.InitAssets();
            Weapon("civ_w_mg", "shotgun_bullet", 1f, 12f, 2f, 2, 0.5f);
            Weapon("civ_w_cannon", "cannonball", 1f, 16f, 0.3f, 1, 2f);
            Weapon("civ_w_rock", "rock", 1f, 14f, 0.25f, 1, 2f);
            Weapon("civ_w_bolt", "arrow", 1f, 12f, 0.5f, 1, 0.5f);
            Weapon("civ_w_missile", "civ_guided_missile", 1f, 20f, 0.4f, 1, 2f);
            Weapon("civ_w_naval_missile", "civ_naval_missile", 1f, 26f, 0.3f, 1, 2f);
            Weapon("civ_w_bomb", "firebomb", 1f, 4f, 0.3f, 2, 3f);
            Weapon("civ_w_arc", "freeze_orb", 1f, 14f, 0.8f, 1, 1.5f);
            Weapon("civ_w_ray", "green_orb", 1f, 14f, 0.8f, 1, 1.5f);
            Weapon("civ_w_acid", "acid_ball", 1f, 8f, 0.8f, 2, 1.5f);
            Weapon("civ_w_ram", "base_attack", 1f, 1f, 0.5f, 1, 1f);
            Weapon("civ_w_flak", "shotgun_bullet", 1f, 22f, 0.6f, 3, 1f);
            Weapon("civ_w_rocket", "cannonball", 1f, 14f, 0.4f, 1, 1.5f);
            Weapon("civ_w_air_missile", "civ_air_missile", 1f, 22f, 0.5f, 1, 1.5f);
            Weapon("civ_w_flame", "fireball", 1f, 10f, 0.6f, 2, 1.5f);
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls == "ship") Ship(d);
                else Land(d);
            }
        }

        private static void Weapon(string id, string projectile, float dmg, float range, float speed, int count, float aoe)
        {
            if (AssetManager.items.get(id) != null) return;
            EquipmentAsset t = AssetManager.items.clone(id, projectile == "base_attack" ? "$weapon" : "$range");
            t.equipment_subtype = "civ_vehicle_weapon";
            t.is_pool_weapon = false;
            t.pool_rate = 0;
            if (projectile != "base_attack") t.projectile = AssetManager.projectiles.get(projectile) != null ? projectile : "arrow";
            t.path_slash_animation = "effects/slashes/slash_punch";
            t.path_icon = "ui/Icons/civ/item/" + id;
            t.translation_key = "item_" + id;
            t.needs_to_be_explored = false;
            if (t.base_stats == null) t.base_stats = new BaseStats();
            t.base_stats["damage"] = dmg;
            t.base_stats["range"] = range;
            t.base_stats["attack_speed"] = speed;
            t.base_stats["projectiles"] = count;
            t.base_stats["targets"] = 1f;
            t.base_stats["area_of_effect"] = aoe;
        }

        public static string WeaponFor(UnitDef d)
        {
            string a = d.Art ?? "";
            if (d.Cls == "soldier")
            {
                switch (a)
                {
                    case "archer":
                    case "crossbow": return "civ_w_bolt";
                    case "musketeer":
                    case "rifleman":
                    case "gunner":
                    case "trooper":
                    case "shock":
                    case "brass":
                    case "diesel": return "civ_w_mg";
                    case "rocket": return "civ_w_rocket";
                    case "arc": return "civ_w_arc";
                    case "power": return "civ_w_ray";
                }
                return "civ_w_ram";
            }
            if (a == "death_ray") return "civ_w_ray";
            if (a == "davinci_flyer") return "civ_w_bomb";
            if (a == "avrocar") return "civ_w_mg";
            if (a == "davinci_tank") return "civ_w_cannon";
            if (a == "war_elephant") return "civ_w_ram";
            if (d.Cls == "space")
            {
                switch (d.Fam)
                {
                    case "energy": return "civ_w_ray";
                    case "missile": return "civ_w_air_missile";
                    case "kinetic": return "civ_w_cannon";
                    case "fighter": return "civ_w_mg";
                }
                return "civ_w_ray";
            }
            if (d.Cls == "air")
            {
                if (d.Id == "civ_multirole_fighter") return "civ_w_air_missile";
                if (d.Role == "bomber") return "civ_w_bomb";
                if (d.Route == "tesla") return "civ_w_arc";
                if (d.Route == "atom") return "civ_w_ray";
                if (d.Route == "bio") return "civ_w_acid";
                return "civ_w_mg";
            }
            if (a == "ram" || a == "siege_tower" || a == "bio_crawler" || a == "bio_titan") return "civ_w_ram";
            if (a == "catapult" || a == "trebuchet") return "civ_w_rock";
            if (a == "ballista") return "civ_w_bolt";
            if (d.Cls == "aa") return "civ_w_flak";
            if (d.Route == "tesla") return "civ_w_arc";
            if (d.Route == "atom" && d.Cls == "tank") return "civ_w_ray";
            if (d.Cls == "tank" || d.Cls == "siege") return "civ_w_cannon";
            if (d.Cls == "land" && Store.Get(d.Stats, "damage") >= 30f) return "civ_w_mg";
            return "civ_w_ram";
        }

        private static void Land(UnitDef d)
        {
            if (AssetManager.actor_library.get(d.Id) != null) return;
            bool air = d.Cls == "air" || d.Cls == "space";
            bool military = Vehicles.Military(d);
            ActorAsset a = AssetManager.actor_library.clone(d.Id, "$basic_unit$");
            a.is_humanoid = false;
            a.civ = false;
            a.can_have_subspecies = false;
            a.has_baby_form = false;
            a.can_turn_into_zombie = false;
            a.zombie_auto_asset = false;
            a.can_turn_into_mush = false;
            a.can_turn_into_tumor = false;
            a.unit_other = true;
            a.texture_id = d.Id;
            a.has_advanced_textures = false;
            a.use_phenotypes = false;
            a.has_skin = false;
            a.need_colored_sprite = true;
            a.animation_walk = ActorAnimationSequences.walk_0_3;
            a.animation_idle = ActorAnimationSequences.walk_0;
            a.animation_swim = ActorAnimationSequences.walk_0_3;
            a.disable_jump_animation = true;
            a.prevent_unconscious_rotation = true;
            a.shadow = true;
            a.shadow_texture = air ? "unitShadow_6" : "unitShadow_5";
            a.shadow_texture_egg = "unitShadow_2";
            a.shadow_texture_baby = "unitShadow_4";
            a.actor_size = ActorSize.S13_Human;
            a.icon = "civ/unit/" + d.Id.Substring(4);
            a.name_locale = d.Id;
            a.kingdom_id_wild = "neutral_animals";
            a.immune_to_injuries = true;
            a.inspect_children = false;
            a.inspect_sex = false;
            a.inspect_show_species = false;
            a.inspect_generation = false;
            a.inspect_avatar_scale = 2f;
            a.show_in_knowledge_window = false;
            a.show_in_taxonomy_tooltip = false;
            a.show_on_meta_layer = false;
            a.needs_to_be_explored = false;
            a.use_items = false;
            a.take_items = false;
            a.can_edit_traits = true;
            a.can_receive_traits = true;
            a.can_be_cloned = false;
            a.allow_possession = true;
            a.can_be_inspected = true;
            a.color_hex = "#000000";
            a.flying = air;
            a.very_high_flyer = air;
            a.die_on_blocks = !air;
            a.ignore_blocks = air;
            a.force_land_creature = !air;
            a.can_be_killed_by_stuff = true;
            a.can_attack_buildings = military;
            a.can_be_moved_by_powers = true;
            a.can_be_hurt_by_powers = true;
            a.effect_damage = true;
            a.update_z = true;
            a.can_flip = true;
            a.check_flip = (BaseSimObject s, WorldTile t) => true;
            if (a.base_stats == null) a.base_stats = new BaseStats();
            a.base_stats["health"] = Store.Get(d.Stats, "health");
            a.base_stats["speed"] = Store.Get(d.Stats, "speed") * 2.5f;
            a.base_stats["armor"] = Store.Get(d.Stats, "armor");
            a.base_stats["damage"] = Math.Max(1f, Store.Get(d.Stats, "damage"));
            a.base_stats["range"] = Math.Max(1f, Store.Get(d.Stats, "range"));
            a.base_stats["attack_speed"] = 1f;
            a.base_stats["targets"] = 1f;
            a.base_stats["mass"] = 1000f;
            a.base_stats["mass_2"] = 600f;
            a.base_stats["stamina"] = 500f;
            a.base_stats["lifespan"] = 80f;
            float sc = Store.Get(d.Stats, "scale");
            a.base_stats["scale"] = sc > 0f ? sc : 0.1f;
            a.base_stats["size"] = 1f;
            a.default_attack = WeaponFor(d);
            a.sound_hit = "event:/SFX/HIT/HitMetal";
            a.job = AssetLibrary<ActorAsset>.a<string>("decision");
            a.job_citizen = AssetLibrary<ActorAsset>.a<string>(military ? "attacker" : "civ_cargo");
            a.job_kingdom = AssetLibrary<ActorAsset>.a<string>(military ? "attacker" : "civ_cargo");
            a.job_attacker = AssetLibrary<ActorAsset>.a<string>("attacker");
            if (!military) a.job = AssetLibrary<ActorAsset>.a<string>(air ? "decision" : "civ_cargo");
            if (a.decision_ids != null) a.decision_ids.Clear();
            foreach (string dec in military ? MilitaryDecisions : new[] { "warrior_random_move", "check_swearing" })
            {
                if (AssetManager.decisions_library.get(dec) != null) a.addDecision(dec);
            }
            a.action_death = d.Cls == "soldier" ? (WorldAction)Soldiers.OnDeath : Vehicles.OnDeath;
            if (d.Cls == "soldier")
            {
                a.base_stats["mass"] = 60f;
                a.base_stats["mass_2"] = 40f;
                a.shadow_texture = "unitShadow_2";
                a.sound_hit = "event:/SFX/HIT/HitMetal";
            }
            a.color = Toolbox.makeColor(a.color_hex);
            if (a.units == null) a.units = new HashSet<Actor>();
            Textures(a);
            _created.Add(a);
        }

        private static void Textures(ActorAsset a)
        {
            try
            {
                a.texture_asset = null;
                AssetManager.actor_library.loadTexturesAndSprites(a);
                if (a.texture_asset != null)
                {
                    a.texture_asset.shadow = a.shadow;
                    a.texture_asset.shadow_texture = a.shadow_texture;
                    a.texture_asset.shadow_texture_egg = a.shadow_texture_egg;
                    a.texture_asset.shadow_texture_baby = a.shadow_texture_baby;
                    a.texture_asset.loadShadow();
                }
            }
            catch (Exception e)
            {
                CivMod.Log("unit textures " + a.id + ": " + e.Message);
            }
            a.sound_idle = null;
            a.sound_idle_loop = null;
            a.sound_spawn = null;
            a.sound_death = null;
            a.sound_attack = null;
        }

        private static void Ship(UnitDef d)
        {
            if (AssetManager.actor_library.get(d.Id) != null) return;
            string from = d.Role == "trade" ? "boat_trading_human" : d.Role == "fish" ? "boat_fishing" : "boat_transport_human";
            ActorAsset src = AssetManager.actor_library.get(from);
            if (src == null) return;
            ActorAsset a = AssetManager.actor_library.clone(d.Id, from);
            a.name_locale = d.Id;
            a.base_stats["health"] = Store.Get(d.Stats, "health");
            a.base_stats["speed"] = Store.Get(d.Stats, "speed");
            a.base_stats["armor"] = Store.Get(d.Stats, "armor");
            float shipScale = Store.Get(d.Stats, "scale");
            if (shipScale > 0f) a.base_stats["scale"] = shipScale;
            if (d.Role != "fish")
            {
                bool war = d.Role == "war" || d.Role == "unique";
                a.base_stats["damage"] = Math.Max(5f, Store.Get(d.Stats, "damage"));
                a.base_stats["attack_speed"] = war ? 0.6f : 0.2f;
                a.base_stats["range"] = war ? 12f : 6f;
                a.default_attack = d.Route == "bio" ? "civ_w_acid" : d.Art == "dromon" ? "civ_w_flame" : "boat_cannonball";
            }
            a.icon = "civ/unit/" + d.Id.Substring(4);
            a.has_override_sprite = true;
            a.shadow = false;
            a.can_be_inspected = true;
            if (a.units == null) a.units = new HashSet<Actor>();
            if (AssetManager.actor_library.list_only_boat_assets != null && !AssetManager.actor_library.list_only_boat_assets.Contains(a)) AssetManager.actor_library.list_only_boat_assets.Add(a);
            try
            {
                a.sound_idle = null;
                a.sound_spawn = null;
                a.sound_death = null;
                a.sound_attack = null;
            }
            catch (Exception)
            {
            }
            _created.Add(a);
        }

        [HarmonyPatch(typeof(ActorAssetLibrary), nameof(ActorAssetLibrary.post_init))]
        private static class PostInit
        {
            private static void Postfix()
            {
                foreach (ActorAsset a in _created)
                {
                    try
                    {
                        if (a.shadow && a.texture_asset != null) a.texture_asset.loadShadow();
                        if (a.is_boat) a.has_override_sprite = true;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ActorAsset), nameof(ActorAsset.getDescriptionID))]
        private static class Description
        {
            private static void Postfix(ActorAsset __instance, ref string __result)
            {
                if (__result == null && __instance != null && __instance.id != null && Db.UnitById.ContainsKey(__instance.id)) __result = __instance.id + "_d";
            }
        }
    }
}
