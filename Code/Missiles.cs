using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class Missiles
    {
        private const string CruiseStock = "civ_cruise_stock";
        private const string HyperStock = "civ_hypersonic_stock";

        public static void InitAssets()
        {
            Projectile("civ_guided_missile", 65f, 0.08f);
            Projectile("civ_naval_missile", 45f, 0.1f);
            Projectile("civ_air_missile", 70f, 0.08f, "civ/transit/air_missile");
        }

        private static void Projectile(string id, float speed, float scale, string art = "civ/transit/missile")
        {
            if (AssetManager.projectiles.get(id) != null || AssetManager.projectiles.get("cannonball") == null) return;
            ProjectileAsset p = AssetManager.projectiles.clone(id, "cannonball");
            p.animated = false;
            p.speed = speed;
            p.speed_random = 0f;
            p.look_at_target = true;
            p.use_min_angle_height = false;
            p.scale_start = scale;
            p.scale_target = scale;
            p.terraform_option = "cannonball";
            p.terraform_range = 1;
            p.can_be_left_on_ground = false;
            Sprite sprite = SpriteTextureLoader.getSprite(art);
            if (sprite != null) p.frames = new[] { sprite };
        }

        public static void Year(Kingdom k)
        {
            if (!Vehicles.On || !G.Ok(k)) return;
            bool cruise = Research.HasFeature(k, "cruise_missile");
            bool hyper = Research.HasFeature(k, "hypersonic_missile");
            if (!cruise && !hyper) return;
            if (cruise) Build(k, false);
            if (hyper) Build(k, true);
            if (!World.world.wars.hasWars(k) || !G.Chance(0.2f)) return;
            if (hyper && Store.Int(k.data, HyperStock, 0) > 0 && (!cruise || G.Chance(0.35f)) && TryLaunch(k, true)) return;
            if (cruise && Store.Int(k.data, CruiseStock, 0) > 0 && TryLaunch(k, false)) return;
            if (hyper && Store.Int(k.data, HyperStock, 0) > 0) TryLaunch(k, true);
        }

        private static void Build(Kingdom k, bool hyper)
        {
            string key = hyper ? HyperStock : CruiseStock;
            int stock = Store.Int(k.data, key, 0);
            if (stock >= (hyper ? 4 : 6)) return;
            int scale = hyper ? 2 : 1;
            foreach (City c in G.Cities(k))
            {
                if (!c.hasBuildingType("type_civ_factory")) continue;
                if (!Economy.Has(c, "civ_steel", 2 * scale) || !Economy.Has(c, "civ_electronics", 2 * scale) || !Economy.Has(c, "civ_ammo", 4 * scale) || !Economy.Has(c, "civ_fuel", scale) || !Economy.Has(c, "gold", 20 * scale)) continue;
                Economy.Take(c, "civ_steel", 2 * scale);
                Economy.Take(c, "civ_electronics", 2 * scale);
                Economy.Take(c, "civ_ammo", 4 * scale);
                Economy.Take(c, "civ_fuel", scale);
                Economy.Take(c, "gold", 20 * scale);
                Store.Put(k.data, key, stock + 1);
                return;
            }
        }

        private static List<WorldTile> Launchers(Kingdom k)
        {
            var list = new List<WorldTile>();
            foreach (City c in G.Cities(k))
            {
                if (c.countBuildingsType("type_civ_fortress") == 0 && c.countBuildingsType("type_civ_silo") == 0) continue;
                WorldTile tile = G.Tile(c);
                if (tile != null) list.Add(tile);
            }
            foreach (string id in new[] { "civ_missile_destroyer", "civ_nuclear_sub" })
            {
                ActorAsset asset = AssetManager.actor_library.get(id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor a in asset.units)
                    if (G.Ok(a) && a.kingdom == k && a.current_tile != null) list.Add(a.current_tile);
            }
            return list;
        }

        private static int Priority(Building b)
        {
            if (b == null || b.asset == null || !b.isUsable() || b.isUnderConstruction()) return 0;
            switch (b.asset.type)
            {
                case "type_civ_silo": return 6;
                case "type_civ_antiair":
                case "type_civ_fortress":
                case "type_civ_airfield": return 5;
                case "type_civ_airport": return 4;
                case "type_civ_factory": return 3;
                default: return 0;
            }
        }

        private static bool TryLaunch(Kingdom k, bool hyper)
        {
            List<WorldTile> origins = Launchers(k);
            if (origins.Count == 0) return false;
            float range = hyper ? 280f : 180f;
            float best = float.MinValue;
            WorldTile from = null, target = null;
            foreach (Kingdom enemy in World.world.wars.getEnemiesOf(k))
            {
                foreach (City c in G.Cities(enemy))
                {
                    foreach (Building b in c.buildings)
                    {
                        int priority = Priority(b);
                        if (priority == 0 || b.current_tile == null) continue;
                        foreach (WorldTile origin in origins)
                        {
                            float distance = G.Dist(origin, b.current_tile);
                            if (distance > range) continue;
                            float score = priority * 50f - distance * 0.1f;
                            if (score <= best) continue;
                            best = score;
                            from = origin;
                            target = b.current_tile;
                        }
                    }
                }
            }
            if (target == null) return false;
            string key = hyper ? HyperStock : CruiseStock;
            Store.Put(k.data, key, Math.Max(0, Store.Int(k.data, key, 0) - 1));
            WorldTile impact = target;
            Transit.ConventionalMissile(from, impact, hyper, () => Impact(k, impact, hyper));
            return true;
        }

        private static void Impact(Kingdom k, WorldTile tile, bool hyper)
        {
            if (!G.Ok(k) || tile == null) return;
            City city = tile.zone != null ? tile.zone.city : null;
            if (city != null && city.kingdom != null && !World.world.wars.isInWarWith(k, city.kingdom)) return;
            if (city != null)
            {
                int sam = Vehicles.Count(Vehicles.In(city), d => d.Cls == "aa");
                float intercept = Math.Min(0.65f, city.countBuildingsType("type_civ_antiair") * 0.12f + sam * 0.08f);
                if (G.Chance(hyper ? intercept * 0.3f : intercept))
                {
                    Transit.Puff(new Vector2(tile.x + 0.5f, tile.y + 3f), 1.2f);
                    return;
                }
            }
            float radius = hyper ? 5f : 3.5f;
            float damage = hyper ? 340f : 220f;
            EffectsLibrary.spawnAtTile("fx_firebomb_explosion", tile, hyper ? 0.8f : 0.55f);
            foreach (Actor a in Finder.getUnitsFromChunk(tile, 2, radius).ToList())
            {
                if (!G.Ok(a) || a.kingdom == null || !World.world.wars.isInWarWith(k, a.kingdom) || a.current_tile == null) continue;
                float distance = G.Dist(tile, a.current_tile);
                if (distance > radius) continue;
                a.getHit(damage * (1f - distance / (radius * 1.5f)), true, AttackType.Other, null, false, false, false);
            }
            if (city == null || city.kingdom == null || !World.world.wars.isInWarWith(k, city.kingdom)) return;
            foreach (Building b in city.buildings.ToList())
            {
                if (b == null || b.current_tile == null || !b.isAlive()) continue;
                float distance = G.Dist(tile, b.current_tile);
                if (distance > radius) continue;
                b.getHit((hyper ? 800f : 500f) * (1f - distance / (radius * 1.5f)), true, AttackType.Other, null, false, false, false);
            }
        }
    }
}
