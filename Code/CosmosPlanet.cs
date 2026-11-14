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
            bool ready = CivMod.C == null || CivMod.C.NpcPlanets;
            if (ready && here.Count == 0)
            {
                Polity native = Natives(r);
                if (native != null) here.Add(native);
            }
            var used = new List<WorldTile>();
            foreach (Polity p in here)
            {
                string sp = Species(p);
                if (ready)
                {
                    bool ok = false;
                    try
                    {
                        ok = Realms(p, sp, land, used, r);
                    }
                    catch (Exception e)
                    {
                        CivMod.Report("planet-realms", e);
                    }
                    if (ok) continue;
                }
                int groups = Math.Max(1, Math.Min(5, 1 + (int)Math.Log(1.0 + p.Pop)));
                for (int g = 0; g < groups; g++)
                {
                    WorldTile c = land[r.Next(0, land.Count)];
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

        private static readonly Dictionary<string, string[]> NativeSpecies = new Dictionary<string, string[]>
        {
            { "terran", new[] { "human", "elf", "dwarf", "orc" } }, { "jungle", new[] { "elf", "human" } }, { "swamp", new[] { "orc", "human" } },
            { "arid", new[] { "human", "orc" } }, { "barren", new[] { "dwarf" } }, { "volcanic", new[] { "orc", "dwarf" } }, { "crystal", new[] { "dwarf", "elf" } },
            { "ice", new[] { "dwarf", "human" } }, { "fungal", new[] { "elf", "orc" } }, { "ocean", new[] { "human" } }, { "reef", new[] { "human", "elf" } }
        };

        private static Polity Natives(Random r)
        {
            CNode n = CosmosGen.Get(Active);
            if (n == null || n.Level != 0 || Cosmos.OwnerExact(Active) != null) return null;
            if (CivMod.C != null && !CivMod.C.NpcFolk) return null;
            string[] opts;
            if (!NativeSpecies.TryGetValue(n.Sub, out opts)) return null;
            int fermi = Cosmos.St != null ? Cosmos.St.Fermi : 1;
            double chance = n.Hab * (fermi == 0 ? 0.15 : fermi == 2 ? 0.75 : 0.45);
            if (r.NextDouble() >= chance)
            {
                if (r.NextDouble() < 0.3 && !Cosmos.St.Ruins.Contains(Active)) Cosmos.St.Ruins.Add(Active);
                return null;
            }
            string sp = opts[r.Next(0, opts.Length)];
            ActorAsset a = AssetManager.actor_library.get(sp);
            if (a == null || !a.civ) return null;
            AlienKind kind = CosmosLife.KindOf(sp);
            string name = kind != null ? Word(kind, r) : CosmosGen.Word(r);
            Polity p = Cosmos.NewPolity(null, "independent", Active, name);
            p.Species = sp;
            p.Era = r.NextDouble() < 0.7 ? r.Next(0, 3) : r.Next(3, 6);
            p.Gov = CosmosLife.EraGov(p.Era, r);
            p.Econ = CosmosLife.EraEcon(p.Era, r);
            p.Pop = 0.3f + (float)r.NextDouble() * 3f;
            p.Stance = r.NextDouble() < (kind != null ? kind.Hostility : 0.3f) ? "wary" : "open";
            p.Color = "#" + ((r.Next(0x40, 0xFF) << 16) | (r.Next(0x40, 0xFF) << 8) | r.Next(0x40, 0xFF)).ToString("X6");
            Cosmos.Dirty();
            Cosmos.Log(Txt.F("cos_log_natives", p.Name, n.Name.Length > 0 ? n.Name : CosmosGen.Describe(n)));
            return p;
        }

        private static string Word(AlienKind k, Random r)
        {
            string s = k.SylA[r.Next(0, k.SylA.Length)] + k.SylB[r.Next(0, k.SylB.Length)];
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static WorldTile Spot(List<WorldTile> land, List<WorldTile> used, Random r, WorldTile near, int min, int max)
        {
            WorldTile best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < 40; i++)
            {
                WorldTile t;
                if (near != null)
                {
                    double ang = r.NextDouble() * Math.PI * 2;
                    int d = r.Next(min, max + 1);
                    t = World.world.GetTile(near.x + (int)(Math.Cos(ang) * d), near.y + (int)(Math.Sin(ang) * d));
                }
                else t = land[r.Next(0, land.Count)];
                if (t == null || t.zone == null || t.zone.city != null || t.Type == null || t.Type.liquid || t.main_type == null || !t.main_type.ground) continue;
                float score = 0f;
                foreach (WorldTile u in used) score = Math.Min(score == 0f ? float.MaxValue : score, G.Dist(u, t));
                if (used.Count == 0) score = 1f;
                if (near != null && used.Count > 0) score = Math.Min(score, 60f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            if (best != null && near == null && used.Count > 0 && bestScore < 18f) return null;
            return best;
        }

        private static void Villagers(City c, string sp, WorldTile at, int n, long pid)
        {
            for (int i = 0; i < n; i++)
            {
                WorldTile t = at;
                foreach (WorldTile x in at.getTilesAround(4)) if (x != null && x.main_type != null && x.main_type.ground && !x.Type.liquid && G.Chance(0.1f)) t = x;
                Actor a = World.world.units.createNewUnit(sp, t);
                if (a == null) continue;
                Store.Put(a.data, KPolity, pid.ToString());
                a.joinCity(c);
                a.setMetasFromCity(c);
            }
        }

        private static bool Realms(Polity p, string sp, List<WorldTile> land, List<WorldTile> used, Random r)
        {
            int nations = p.Pop >= 40f ? 3 : p.Pop >= 8f ? 2 : 1;
            if (p.Era <= 2 && p.Pop >= 2f) nations = Math.Min(4, nations + 1);
            int perCities = Math.Max(1, Math.Min(4, 1 + (int)Math.Log(1.0 + p.Pop / nations)));
            int pop = Math.Max(8, Math.Min(30, 8 + p.Era));
            var made = new List<Kingdom>();
            for (int k = 0; k < nations; k++)
            {
                WorldTile seat = Spot(land, used, r, null, 0, 0);
                if (seat == null) break;
                Actor founder = World.world.units.createNewUnit(sp, seat);
                if (founder == null) break;
                Store.Put(founder.data, KPolity, p.Id.ToString());
                if (!World.world.cities.canStartNewCityCivilizationHere(founder) || !founder.buildCityAndStartCivilization())
                {
                    founder.die(true);
                    continue;
                }
                used.Add(seat);
                Kingdom kd = founder.kingdom;
                City first = founder.city;
                if (!G.Ok(kd) || first == null) continue;
                Villagers(first, sp, seat, pop, p.Id);
                for (int c = 1; c < perCities; c++)
                {
                    WorldTile t = Spot(land, used, r, seat, 18, 34);
                    if (t == null) continue;
                    Actor f = World.world.units.createNewUnit(sp, t);
                    if (f == null) continue;
                    Store.Put(f.data, KPolity, p.Id.ToString());
                    f.setKingdom(kd);
                    City nc = World.world.cities.buildNewCity(f, t.zone);
                    if (nc == null) continue;
                    used.Add(t);
                    Villagers(nc, sp, t, Math.Max(5, pop / 2), p.Id);
                }
                Setup(kd, p, r);
                made.Add(kd);
            }
            if (made.Count == 0) return false;
            for (int i = 0; i < made.Count; i++)
            {
                for (int j = i + 1; j < made.Count; j++)
                {
                    if (r.NextDouble() < (p.Stance == "wary" || p.Stance == "hunter" ? 0.4 : 0.15)) World.world.diplomacy.startWar(made[i], made[j], WarTypeLibrary.normal);
                }
            }
            return true;
        }

        private static void Setup(Kingdom k, Polity p, Random r)
        {
            Store.Put(k.data, KPolity, p.Id.ToString());
            int era = Math.Max(0, Math.Min(Db.LastEra, p.Era + r.Next(-1, 2)));
            Research.SetupEra(k, era);
            string gov = !string.IsNullOrEmpty(p.Gov) && Db.Policy(p.Gov) != null ? p.Gov : CosmosLife.EraGov(era, r);
            if (Db.Policy(gov) != null) Policies.Set(k, "gov", gov, "planet");
            if (!string.IsNullOrEmpty(p.Econ) && Db.Policy(p.Econ) != null) Policies.Set(k, "econ", p.Econ, "planet");
            if (!string.IsNullOrEmpty(p.Ideo) && Db.Policy(p.Ideo) != null) Policies.Set(k, "ideo", p.Ideo, "planet");
            if (Flags.On) Flags.Reform(k, "founded");
            if (!_start.ContainsKey(p.Id)) _start[p.Id] = 0;
            _start[p.Id] += Math.Max(1, G.Pop(k));
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
