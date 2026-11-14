using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.General;
using UnityEngine;

namespace Civilizations
{
    internal class BiomeDef
    {
        public string Id;
        public string Base;
        public string Low;
        public string High;
        public int Pool;
        public bool Cold;
        public bool Seed;
    }

    internal static class Biomes
    {
        public static readonly List<BiomeDef> Defs = new List<BiomeDef>
        {
            new BiomeDef { Id = "steppe", Base = "biome_savanna", Low = "#C9B867", High = "#AE9C50", Pool = 3, Seed = true },
            new BiomeDef { Id = "acacia", Base = "biome_savanna", Low = "#D8A545", High = "#BC8A33", Pool = 2, Seed = true },
            new BiomeDef { Id = "tundra", Base = "biome_permafrost", Low = "#95A88E", High = "#7B8F76", Pool = 3, Cold = true, Seed = true },
            new BiomeDef { Id = "bamboo", Base = "biome_jungle", Low = "#86C24A", High = "#679F35", Pool = 2, Seed = true },
            new BiomeDef { Id = "mangrove", Base = "biome_swamp", Low = "#56804C", High = "#43683C", Pool = 2, Seed = true },
            new BiomeDef { Id = "salt", Base = "biome_desert", Low = "#EAE6DA", High = "#D2CDBF", Pool = 1, Seed = true },
            new BiomeDef { Id = "farmland", Base = "biome_grass", Low = "#B3CF55", High = "#97B540" },
            new BiomeDef { Id = "industrial", Base = "biome_wasteland", Low = "#807D74", High = "#67645C" },
            new BiomeDef { Id = "radiation", Base = "biome_wasteland", Low = "#86D24E", High = "#64B23A" },
            new BiomeDef { Id = "ruins", Base = "biome_rocklands", Low = "#918979", High = "#797263" },
            new BiomeDef { Id = "ash", Base = "biome_wasteland", Low = "#5C5752", High = "#48443F", Seed = true },
            new BiomeDef { Id = "crystal", Base = "biome_crystal", Low = "#BC90EA", High = "#9D72D2", Seed = true },
            new BiomeDef { Id = "glow", Base = "biome_jungle", Low = "#2F716C", High = "#225954", Seed = true },
            new BiomeDef { Id = "terraformed", Base = "biome_grass", Low = "#6ED38C", High = "#53B873", Seed = true }
        };

        private static readonly Dictionary<string, TopTileType> _low = new Dictionary<string, TopTileType>();
        private static readonly Dictionary<string, TopTileType> _high = new Dictionary<string, TopTileType>();
        private static readonly List<PowerButton> _buttons = new List<PowerButton>();
        private static bool _assets, _ui;

        public static string BiomeId(string id)
        {
            return "biome_civ_" + id;
        }

        public static bool Has(string id)
        {
            return _low.ContainsKey(id) && _high.ContainsKey(id);
        }

        public static void InitAssets()
        {
            if (_assets) return;
            _assets = true;
            if (CivMod.C != null && !CivMod.C.NewBiomes) return;
            foreach (BiomeDef d in Defs)
            {
                try
                {
                    Make(d);
                }
                catch (Exception e)
                {
                    CivMod.Report("biome-" + d.Id, e);
                }
            }
            Hub.On(Hub.NukeDetonated, e =>
            {
                if (e.C != null) Paint("radiation", G.Tile(e.C), 9, 0.85f);
            });
            Hub.On(Hub.CityCaptured, e =>
            {
                if (e.C != null && G.Chance(0.15f)) Paint("ruins", G.Tile(e.C), 3, 0.4f);
            });
        }

        private static void Make(BiomeDef d)
        {
            BiomeAsset src = AssetManager.biome_library.get(d.Base);
            if (src == null) return;
            TopTileType baseLow = AssetManager.top_tiles.get(src.tile_low);
            TopTileType baseHigh = AssetManager.top_tiles.get(src.tile_high);
            if (baseLow == null || baseHigh == null) return;
            if (TileTypeBase.last_index_id + 3 >= TileLibrary.array_tiles.Length) return;
            string bid = BiomeId(d.Id);
            if (AssetManager.biome_library.get(bid) != null) return;
            TopTileType low = Tile("civ_" + d.Id + "_low", baseLow, d.Low, bid);
            TopTileType high = Tile("civ_" + d.Id + "_high", baseHigh, d.High, bid);
            BiomeAsset b = AssetManager.biome_library.clone(bid, d.Base);
            b.tile_low = low.id;
            b.tile_high = high.id;
            b.localized_key = bid;
            b.cold_biome = d.Cold || src.cold_biome;
            b.special_biome = d.Pool <= 0;
            b.generator_pot_amount = d.Pool;
            b._cached_tile_low = low;
            b._cached_tile_high = high;
            low.biome_asset = b;
            high.biome_asset = b;
            if (d.Pool > 0) AssetManager.biome_library.addBiomeToPool(b);
            _low[d.Id] = low;
            _high[d.Id] = high;
            if (d.Seed) Seed(d, b, src);
        }

        private static TopTileType Tile(string id, TopTileType from, string hex, string biome)
        {
            TopTileType t = AssetManager.top_tiles.clone(id, from.id);
            t.color_hex = hex;
            t.color = Toolbox.makeColor(hex);
            t.edge_color_hex = from.edge_color_hex;
            t.edge_color = from.edge_color;
            t.sprites = from.sprites;
            t.music_assets = from.music_assets;
            t.setBiome(biome);
            t.has_biome_tags = t.biome_tags != null && t.biome_tags.Count > 0;
            return t;
        }

        private static void Seed(BiomeDef d, BiomeAsset b, BiomeAsset src)
        {
            string baseSeed = "seeds_" + src.id.Replace("biome_", "");
            DropAsset drop = AssetManager.drops.get(baseSeed) ?? AssetManager.drops.get("seeds_grass");
            GodPower power = AssetManager.powers.get(baseSeed) ?? AssetManager.powers.get("seeds_grass");
            if (drop == null || power == null) return;
            string id = "seeds_civ_" + d.Id;
            if (AssetManager.drops.get(id) != null) return;
            DropAsset nd = AssetManager.drops.clone(id, drop.id);
            nd.drop_type_low = b.tile_low;
            nd.drop_type_high = b.tile_high;
            GodPower np = AssetManager.powers.clone(id, power.id);
            np.drop_id = id;
            np.name = id;
            np.path_icon = "ui/Icons/civ/biome/" + d.Id;
        }

        public static void Ui()
        {
            if (_ui) return;
            _ui = true;
            try
            {
                PowersTab tab = PowerButtonCreator.GetTab(PowerTabNames.Nature);
                if (tab == null) return;
                int at = -1;
                for (int i = 0; i < tab.transform.childCount; i++)
                {
                    if (tab.transform.GetChild(i).name.StartsWith("seeds_")) at = i;
                }
                foreach (BiomeDef d in Defs)
                {
                    string id = "seeds_civ_" + d.Id;
                    if (!d.Seed || AssetManager.powers.get(id) == null) continue;
                    Tab.Label(id, Txt.F("civ_biome_seeds", Txt.T(BiomeId(d.Id))), Txt.T(BiomeId(d.Id) + "_d"));
                    PowerButton b = PowerButtonCreator.CreateGodPowerButton(id, UiKit.Icon("ui/Icons/civ/biome/" + d.Id));
                    PowerButtonCreator.AddButtonToTab(b, tab);
                    if (at >= 0)
                    {
                        at++;
                        b.transform.SetSiblingIndex(Math.Min(at, tab.transform.childCount - 1));
                    }
                    _buttons.Add(b);
                }
                tab.sortButtons();
                tab.setNewWidth();
                tab.findNeighbours(true);
            }
            catch (Exception e)
            {
                CivMod.Report("biome-ui", e);
            }
        }

        public static int Paint(string id, WorldTile center, int r, float chance)
        {
            TopTileType low, high;
            if (center == null || !_low.TryGetValue(id, out low) || !_high.TryGetValue(id, out high)) return 0;
            int n = 0;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    if (!G.Chance(chance * (1f - 0.5f * (float)Math.Sqrt(dx * dx + dy * dy) / Math.Max(1, r)))) continue;
                    WorldTile t = World.world.GetTile(center.x + dx, center.y + dy);
                    if (!Paintable(t)) continue;
                    t.setTopTileType((t.main_type.id ?? "").Contains("high") ? high : low);
                    n++;
                }
            }
            if (n > 0) World.world.resetRedrawTimer();
            return n;
        }

        private static bool Paintable(WorldTile t)
        {
            if (t == null || t.main_type == null || !t.main_type.ground || t.main_type.liquid || t.main_type.mountains) return false;
            if (t.building != null || t.Type == null || t.Type.road) return false;
            return t.top_type == null || t.top_type.is_biome || t.top_type.can_be_biome;
        }

        public static string PlanetBiome(string sub)
        {
            switch (sub)
            {
                case "crystal": return BiomeId("crystal");
                case "jungle":
                case "ocean": return BiomeId("glow");
                case "volcanic": return BiomeId("ash");
                case "ice": return BiomeId("tundra");
                case "terraformed": return BiomeId("terraformed");
            }
            return null;
        }

        public static void Year()
        {
            if (_low.Count == 0) return;
            foreach (City c in G.Cities())
            {
                if (!G.Ok(c.kingdom)) continue;
                WorldTile t = G.Tile(c);
                if (t == null) continue;
                if (Pollution.Level(c) > 60f && G.Chance(0.25f)) Paint("industrial", t, 6, 0.25f);
                else if (Pollution.Level(c) < 10f && G.Chance(0.1f)) Restore(t, 6);
                int era = Research.Era(c.kingdom);
                if (era >= 2 && era <= 10 && c.getPopulationPeople() >= 40 && G.Chance(0.2f)) Paint("farmland", t, 7, 0.2f);
            }
        }

        private static void Restore(WorldTile center, int r)
        {
            TopTileType ind;
            if (!_low.TryGetValue("industrial", out ind)) return;
            BiomeAsset grass = AssetManager.biome_library.get("biome_grass");
            if (grass == null) return;
            TopTileType gl = AssetManager.top_tiles.get(grass.tile_low), gh = AssetManager.top_tiles.get(grass.tile_high);
            if (gl == null || gh == null) return;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    WorldTile t = World.world.GetTile(center.x + dx, center.y + dy);
                    if (t == null || t.top_type == null || t.top_type.biome_id != BiomeId("industrial") || !G.Chance(0.3f)) continue;
                    t.setTopTileType((t.main_type.id ?? "").Contains("high") ? gh : gl);
                }
            }
        }
    }
}
