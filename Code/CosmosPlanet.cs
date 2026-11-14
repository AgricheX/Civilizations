using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class CosmosPlanet
    {
        private const string Folder = "civ_cosmos_home";
        private const string KPolity = "civ_polity";
        public static string Active;
        public static CosState Kept;
        private static bool _returning;
        private static bool _shaped;
        private static float _wait;
        private static readonly Dictionary<long, int> _start = new Dictionary<long, int>();

        private static readonly Dictionary<string, string[]> Biomes = new Dictionary<string, string[]>
        {
            { "terran", new[] { "biome_grass", "biome_maple", "biome_flower", "biome_birch" } }, { "jungle", new[] { "biome_jungle", "biome_savanna" } },
            { "swamp", new[] { "biome_swamp", "biome_mushroom" } }, { "arid", new[] { "biome_desert", "biome_sand", "biome_savanna" } },
            { "barren", new[] { "biome_wasteland", "biome_rocklands" } }, { "volcanic", new[] { "biome_infernal", "biome_wasteland" } },
            { "crystal", new[] { "biome_crystal", "biome_rocklands" } }, { "ice", new[] { "biome_permafrost" } }, { "fungal", new[] { "biome_mushroom", "biome_swamp" } },
            { "machine", new[] { "biome_cybertile", "biome_wasteland" } }, { "ocean", new[] { "biome_lemon", "biome_jungle" } }, { "reef", new[] { "biome_lemon", "biome_flower" } },
            { "abyss", new[] { "biome_corrupted", "biome_swamp" } }
        };

        private static readonly Dictionary<string, string[]> SpeciesFor = new Dictionary<string, string[]>
        {
            { "insectoid", new[] { "ant_black", "beetle", "human" } }, { "reptilian", new[] { "lizard", "crocodile", "orc" } }, { "avian", new[] { "bird", "penguin", "elf" } },
            { "amphibian", new[] { "frog", "human" } }, { "silicate", new[] { "rock_golem", "dwarf" } }, { "aquatic", new[] { "crab", "fish", "human" } },
            { "floater", new[] { "elf" } }, { "fungal", new[] { "mushroom", "druid", "elf" } }, { "hive", new[] { "bee", "ant_red", "orc" } },
            { "machine", new[] { "robot", "dwarf" } }, { "energy", new[] { "fairy", "elf" } }
        };

        public static bool CanOpen(CNode n)
        {
            if (!Cosmos.Ready || n == null || n.Level != 0 || n.Home || Active != null) return false;
            if (n.Sub == "gas_giant" || n.Sub == "asteroid" || CosmosGen.Homes.Contains(n.Path)) return false;
            if (Cosmos.St.Dead.Contains(n.Path)) return false;
            return true;
        }

        private static string HomeFolder()
        {
            return SaveManager.generateMainPath(Folder);
        }

        public static void Open(string path)
        {
            if (!G.Loaded || !Cosmos.Ready || CosmosScale.Busy || _returning) return;
            bool hop = Active != null;
            if (hop && (!CosmosScale.Scale(Active) || Active == path)) return;
            CivMod.Safe("planet-open", () =>
            {
                if (hop) Sync();
                else
                {
                    Persist.All();
                    SaveManager.saveWorldToDirectory(HomeFolder());
                }
                CosmosScale.Clear();
                if (!CosmosScale.Scale(path)) CosmosScale.Fx(false);
                Kept = Cosmos.St;
                Active = path;
                _shaped = false;
                _wait = 3f;
                _start.Clear();
                CNode target = CosmosGen.Get(path);
                Chronicle.Log("cos_planet", null, target.Name.Length > 0 ? target.Name : CosmosGen.Describe(target), "", "");
                World.world.clickGenerateNewMap();
            });
        }

        public static void Return()
        {
            if (Active == null || CosmosScale.Busy || _returning) return;
            CivMod.Safe("planet-return", () =>
            {
                Sync();
                CosmosScale.Fx(false);
                CosmosScale.Clear();
                Kept = Cosmos.St;
                _returning = true;
                SavedMap map = SaveManager.getMapFromPath(HomeFolder());
                if (map == null)
                {
                    _returning = false;
                    Active = null;
                    Notify.Show(Txt.T("cos_planet_lost"));
                    return;
                }
                World.world.save_manager.loadData(map, HomeFolder());
            });
        }

        public static bool TakeKept(out CosState st)
        {
            st = null;
            if (Kept == null) return false;
            st = Kept;
            Kept = null;
            if (_returning)
            {
                _returning = false;
                Active = null;
            }
            return true;
        }

        public static void Abandon()
        {
            if (Active == null) return;
            CosmosScale.Fx(false);
            CosmosScale.Clear();
            Active = null;
            _returning = false;
            _shaped = false;
            _start.Clear();
            Notify.Show(Txt.T("cos_planet_left"));
        }

        public static void Tick(float dt)
        {
            CivMod.Safe("scale-fx", () => CosmosScale.Tick(dt));
            if (Active == null || _returning || !G.Loaded) return;
            if (_shaped)
            {
                _wait -= dt;
                if (_wait <= 0f)
                {
                    _wait = 5f;
                    CivMod.Safe("planet-link", Link);
                }
                return;
            }
            _wait -= dt;
            if (_wait > 0f) return;
            _shaped = true;
            _wait = 5f;
            CivMod.Safe("planet-shape", Shape);
            CivMod.Safe("planet-populate", Populate);
        }

        private static void Shape()
        {
            CNode n = CosmosGen.Get(Active);
            if (n.Level >= 1)
            {
                CosmosScale.Shape(n);
                return;
            }
            string[] biomes;
            if (!Biomes.TryGetValue(n.Sub, out biomes)) biomes = Biomes["terran"];
            string extra = global::Civilizations.Biomes.PlanetBiome(n.Sub);
            if (extra == null && Cosmos.St != null && Cosmos.OwnerExact(Active) != null) extra = global::Civilizations.Biomes.PlanetBiome("terraformed");
            if (extra != null && AssetManager.biome_library.get(extra) != null) biomes = biomes.Concat(new[] { extra, extra }).ToArray();
            var r = new Random(n.Seed);
            bool water = n.Sub == "ocean" || n.Sub == "reef" || n.Sub == "abyss";
            bool dry = n.Sub == "arid" || n.Sub == "barren" || n.Sub == "volcanic" || n.Sub == "machine" || n.Sub == "crystal";
            TileType ocean = AssetManager.tiles.get("deep_ocean");
            TileType soil = AssetManager.tiles.get("soil_low");
            foreach (WorldTile t in World.world.tiles_list)
            {
                if (t == null || t.main_type == null) continue;
                double noise = Math.Sin(t.x * 0.045 + n.Seed) * Math.Cos(t.y * 0.05 - n.Seed * 0.3);
                if (water && t.main_type.ground && noise > -0.35 && ocean != null)
                {
                    t.setTileType(ocean);
                    continue;
                }
                if (dry && t.main_type.liquid && noise > -0.2 && soil != null)
                {
                    t.setTileType(soil);
                }
                if (!t.main_type.ground && !(dry && t.main_type.liquid)) continue;
                BiomeAsset b = AssetManager.biome_library.get(biomes[(int)Math.Abs(noise * 10) % biomes.Length]);
                if (b == null) continue;
                string topId = (t.main_type.id ?? "").Contains("high") ? b.tile_high : b.tile_low;
                TopTileType top = string.IsNullOrEmpty(topId) ? null : AssetManager.top_tiles.get(topId);
                if (top != null) t.setTopTileType(top);
            }
            World.world.resetRedrawTimer();
        }

        private static string Species(Polity p)
        {
            ActorAsset same = AssetManager.actor_library.get(p.Species ?? "");
            if (same != null && same.civ) return p.Species;
            string[] opts;
            if (p.Species != null && SpeciesFor.TryGetValue(p.Species, out opts))
            {
                foreach (string s in opts)
                {
                    ActorAsset a = AssetManager.actor_library.get(s);
                    if (a != null && a.civ) return s;
                }
            }
            ActorAsset own = AssetManager.actor_library.get(p.Species ?? "");
            return own != null && own.civ ? p.Species : "human";
        }

        private static void Populate()
        {
            if (CosmosScale.Scale(Active))
            {
                var rs = new Random(CosmosGen.Get(Active).Seed);
                foreach (var kv in CosmosScale.Seats(Active).ToList())
                {
                    string sp = Species(kv.Key);
                    for (int i = 0; i < 14; i++)
                    {
                        WorldTile t = kv.Value;
                        foreach (WorldTile x in kv.Value.getTilesAround(3)) if (x != null && x.main_type != null && x.main_type.ground && rs.NextDouble() < 0.1) t = x;
                        Actor a = World.world.units.createNewUnit(sp, t);
                        if (a != null) Store.Put(a.data, KPolity, kv.Key.Id.ToString());
                    }
                }
                return;
            }
            List<Polity> here = Cosmos.St.Polities.Where(p => p.Nodes.Contains(Active)).ToList();
            var land = World.world.tiles_list.Where(t => t != null && t.main_type != null && t.main_type.ground && !t.Type.liquid).ToList();
            if (land.Count == 0) return;
            var r = new Random(CosmosGen.Get(Active).Seed);
            foreach (Polity p in here)
            {
                int groups = Math.Max(1, Math.Min(5, 1 + (int)Math.Log(1.0 + p.Pop)));
                for (int g = 0; g < groups; g++)
                {
                    WorldTile c = land[r.Next(0, land.Count)];
                    string sp = Species(p);
                    for (int i = 0; i < 14; i++)
                    {
                        WorldTile t = c;
                        foreach (WorldTile x in c.getTilesAround(4)) if (x != null && x.main_type != null && x.main_type.ground && r.NextDouble() < 0.1) t = x;
                        Actor a = World.world.units.createNewUnit(sp, t);
                        if (a != null) Store.Put(a.data, KPolity, p.Id.ToString());
                    }
                }
            }
        }

        private static void Link()
        {
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv() || Store.Str(k.data, KPolity).Length > 0) continue;
                long pid = 0;
                foreach (City c in G.Cities(k))
                {
                    foreach (Actor a in c.units)
                    {
                        long v;
                        if (G.Ok(a) && long.TryParse(Store.Str(a.data, KPolity), out v))
                        {
                            pid = v;
                            break;
                        }
                    }
                    if (pid != 0) break;
                }
                Polity p = pid != 0 ? Cosmos.Get(pid) : null;
                if (p == null) continue;
                Store.Put(k.data, KPolity, pid.ToString());
                Research.SetupEra(k, Math.Min(Db.LastEra, p.Era));
                if (!string.IsNullOrEmpty(p.Gov) && Db.Policy(p.Gov) != null) Policies.Set(k, "gov", p.Gov, "planet");
                if (CosmosScale.Scale(Active)) CosmosScale.Arm(k, p);
                if (!_start.ContainsKey(pid)) _start[pid] = 0;
                _start[pid] += Math.Max(1, G.Pop(k));
            }
        }

        private static void Sync()
        {
            var pop = new Dictionary<long, int>();
            var wild = new List<Kingdom>();
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                long pid;
                if (long.TryParse(Store.Str(k.data, KPolity), out pid))
                {
                    int n;
                    pop.TryGetValue(pid, out n);
                    pop[pid] = n + G.Pop(k);
                }
                else if (G.Cities(k).Count > 0) wild.Add(k);
            }
            bool scale = CosmosScale.Scale(Active);
            foreach (Polity p in (scale ? CosmosScale.Present(Active) : Cosmos.St.Polities.Where(x => x.Nodes.Contains(Active)).ToList()))
            {
                int start, now;
                _start.TryGetValue(p.Id, out start);
                pop.TryGetValue(p.Id, out now);
                if (start <= 0) continue;
                if (scale)
                {
                    p.Pop = Math.Max(0.001f, p.Pop * G.Clamp((float)Math.Max(1, now) / start, 0.5f, 2f));
                    continue;
                }
                if (now <= 0)
                {
                    if (p.Seat == Active) Cosmos.St.Polities.Remove(p);
                    else p.Nodes.Remove(Active);
                    continue;
                }
                p.Pop = Math.Max(0.001f, p.Pop * G.Clamp((float)now / start, 0.3f, 3f));
            }
            if (scale) CosmosScale.Settle(Active);
            foreach (Kingdom k in wild.Take(scale ? 0 : 3))
            {
                if (Cosmos.OwnerExact(Active) != null) break;
                Polity p = Cosmos.NewPolity(null, "independent", Active, G.Name(k));
                p.Species = k.getSpecies();
                p.Era = Research.Era(k);
                p.Pop = Math.Max(0.001f, G.Pop(k) / 1000f);
                p.Color = G.Hex(k);
            }
            Cosmos.Dirty();
            CNode an = CosmosGen.Get(Active);
            Cosmos.Log(Txt.F("cos_log_planet", an.Name.Length > 0 ? an.Name : CosmosGen.Describe(an)));
        }
    }
}
