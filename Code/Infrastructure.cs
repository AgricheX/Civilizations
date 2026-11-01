using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class Infrastructure
    {
        public const string KDemand = "civ_rdem";
        private static TopTileType[] _levels;
        public static TopTileType RailBed;

        public static void InitAssets()
        {
            TopTileType road = TopTileLibrary.road ?? AssetManager.top_tiles.get("road");
            if (road == null) return;
            _levels = new TopTileType[6];
            _levels[0] = road;
            _levels[1] = Make("civ_road_packed", "#A98A6B", 1.6f, road);
            _levels[2] = Make("civ_road_stone", "#9C9488", 1.75f, road);
            _levels[3] = Make("civ_road_macadam", "#7E7872", 1.9f, road);
            _levels[4] = Make("civ_road_asphalt", "#3E3E44", 2.2f, road);
            _levels[5] = Make("civ_road_highway", "#2E2F35", 2.5f, road);
            RailBed = Make("civ_rail_bed", "#5B4E45", 1.3f, road);
            Walls.InitAssets();
        }

        private static TopTileType Make(string id, string color, float walk, TopTileType road)
        {
            TopTileType t = AssetManager.top_tiles.get(id);
            if (t != null) return t;
            try
            {
                t = AssetManager.top_tiles.clone(id, road.id);
                t.color_hex = color;
                t.color = Toolbox.makeColor(color);
                t.walk_multiplier = walk;
                t.can_be_set_on_fire = false;
                t.setDrawLayer(TileZIndexes.road, "road");
                Sprite[] sprites = SpriteTextureLoader.getSpriteList("tiles/" + id);
                if (sprites != null && sprites.Length > 0)
                {
                    t.sprites = new TileSprites();
                    foreach (Sprite s in sprites) t.sprites.addVariation(s, id);
                }
                return t;
            }
            catch (Exception e)
            {
                CivMod.Log("road tile " + id + ": " + e.Message);
                return road;
            }
        }

        public static int RoadLevel(City c)
        {
            if (c == null || c.kingdom == null) return -1;
            return Research.Level(c.kingdom, "roads:");
        }

        public static int TileLevel(WorldTile t)
        {
            if (t == null || t.top_type == null || !t.Type.road) return -1;
            if (_levels == null) return 0;
            for (int i = 5; i >= 1; i--) if (t.top_type == _levels[i]) return i;
            if (t.top_type == RailBed) return 0;
            return 0;
        }

        public static bool NearWater(City c, int radius)
        {
            WorldTile t = G.Tile(c);
            if (t == null) return false;
            for (int dx = -radius; dx <= radius; dx += 2)
            {
                for (int dy = -radius; dy <= radius; dy += 2)
                {
                    WorldTile o = Globe.Tile(t.x + dx, t.y + dy);
                    if (o != null && o.Type != null && o.Type.liquid && !o.Type.lava && !o.Type.ocean) return true;
                    if (o != null && o.Type != null && o.Type.ocean && dx * dx + dy * dy < radius * radius / 2) return true;
                }
            }
            return false;
        }

        public static void AddDemand(City a, City b, int amount)
        {
            if (a == null || b == null || a.data == null || a == b) return;
            var m = Store.Map(a.data, KDemand);
            Store.Add(m, b.data.id.ToString(), amount);
            Store.Map(a.data, KDemand, m);
        }

        public static float Budget(City c, int yrs)
        {
            int engineers = Roles.Count(c, Roles.Engineer);
            int work = Store.Int(c.data, "civ_eng_work", 0);
            Store.Put(c.data, "civ_eng_work", 0);
            float budget = (3 + engineers * 6) * yrs + Math.Min(work, 30);
            return budget;
        }

        public static void Year(City c, int yrs)
        {
            int level = RoadLevel(c);
            if (level < 0) return;
            var dem = Store.Map(c.data, KDemand);
            foreach (string key in dem.Keys.ToList()) dem[key] *= (float)Math.Pow(0.8, yrs);
            Store.Map(c.data, KDemand, dem);
            float budget = Budget(c, yrs);
            string tr = Policies.Id(c.kingdom, "transport");
            float roadShare = tr == "road_first" ? 1f : tr == "rail_first" ? 0.45f : 0.75f;
            if (!Research.HasFeature(c.kingdom, "rail")) roadShare = 1f;
            float railBudget = budget * (1f - roadShare);
            budget *= roadShare;
            if (level >= 2 && Roles.Count(c, Roles.Engineer) == 0) budget /= 3f;
            foreach (City n in c.neighbours_cities_kingdom)
            {
                if (n != null && n.isAlive() && Store.Get(dem, n.data.id.ToString()) < 5f) Store.Add(dem, n.data.id.ToString(), 6f);
            }
            var targets = dem.Where(kv => kv.Value >= 5f).OrderByDescending(kv => kv.Value).Take(2).ToList();
            int b = (int)budget;
            foreach (var kv in targets)
            {
                City other = G.CityById(kv.Key);
                if (other == null) continue;
                b = Build(c, other, level, b);
                if (b <= 0) break;
            }
            Rails.Invest(c, railBudget);
        }

        public static float Cost(WorldTile t, bool bridges)
        {
            if (t == null || t.Type == null) return -1f;
            if (t.Type.lava || t.Type.block && !t.Type.road) return -1f;
            if (t.Type.liquid)
            {
                if (!bridges) return -1f;
                return t.main_type == TileLibrary.shallow_waters || t.main_type == TileLibrary.close_ocean ? 6f : -1f;
            }
            if (t.hasBuilding()) return 12f;
            if (t.Type.road) return 0.6f;
            if (t.Type.mountains) return 8f;
            if (t.Type.id.Contains("hills")) return 2.5f;
            return 1.5f;
        }

        private static int Build(City a, City b, int level, int budget)
        {
            WorldTile ta = G.Tile(a);
            WorldTile tb = G.Tile(b);
            if (ta == null || tb == null) return budget;
            if (G.Dist(a, b) > 160f) return budget;
            bool bridges = Research.HasFeature(a.kingdom, "bridges");
            List<WorldTile> path = Paths.Find(ta, tb, t => Cost(t, bridges), 9000);
            if (path.Count == 0) return budget;
            TopTileType type = _levels != null ? _levels[Math.Min(level, 5)] : TopTileLibrary.road;
            foreach (WorldTile t in path)
            {
                if (budget <= 0) break;
                if (t == null || t.hasBuilding()) continue;
                if (t.Type.liquid)
                {
                    if (budget < 3) break;
                    MapAction.terraformMain(t, TileLibrary.sand);
                    budget -= 2;
                }
                else if (t.Type.block || (!t.Type.ground && !t.Type.road)) continue;
                if (t.top_type == RailBed) continue;
                int cur = TileLevel(t);
                if (cur >= level) continue;
                MapAction.terraformTop(t, type, AssetManager.terraform.get("road"));
                budget--;
            }
            World.world.resetRedrawTimer();
            return budget;
        }
    }
}
