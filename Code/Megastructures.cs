using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Civilizations
{
    internal class MegaDef
    {
        public string Id;
        public int Level;
        public int Era;
        public int Years;
        public int Stages = 3;
        public int Max = 1;
        public string[] Mats = new string[0];
        public Dictionary<string, float> Fx = new Dictionary<string, float>();
        public float Shield;
        public float Speed;
        public bool Unique { get { return Level >= 5; } }
        public int Weight { get { return Level <= 1 ? 1 : Level <= 5 ? 2 : 3; } }
    }

    internal static class Megastructures
    {
        private static readonly string[] Raw =
        {
            "skyhook|0|15|25|2|5|trade:6 build:4|asteroid_metals",
            "orbital_ring|0|16|30|3|2|prod:10 trade:10 build:6|asteroid_metals",
            "rotovator|0|16|30|2|4|trade:8 prod:4|asteroid_metals",
            "arcology|0|16|40|3|5|growth:12 health:6 pollution:-10|",
            "transglobal_highway|0|16|45|3|1|trade:15 speed:6|",
            "space_elevator|0|17|60|3|5|trade:10 build:8 speed:5|asteroid_metals",
            "mass_driver|0|17|35|2|3|trade:6 attack:10|asteroid_metals",
            "solar_mirror|0|17|40|2|3|food:15 growth:6|",
            "solar_shade|0|17|40|2|3|health:10 pollution:-20|",
            "launch_loop|0|18|45|3|3|trade:10 speed:6|asteroid_metals",
            "startram|0|18|40|3|3|trade:8 build:6|asteroid_metals",
            "continental_dam|0|18|80|4|1|food:25 growth:8|",
            "linear_city|0|19|70|4|3|growth:20 prod:8|",
            "atmo_terraformer|0|19|90|4|2|growth:15 food:10 health:8|comet_ice",
            "space_fountain|0|20|55|3|3|trade:12 build:8|exotic_matter",
            "planetary_shield|0|20|50|3|2|defense:30|helium3|40",
            "cloud_city|0|21|60|3|3|growth:10 culture:10|",
            "planetary_cannon|0|21|45|2|3|defense:20 attack:10|antimatter|25",
            "shellworld|0|24|120|4|2|growth:25|neutronium",
            "planet_engine|0|25|140|4|1|defense:15 speed:10|antimatter|0|1.5",
            "hollow_world|0|27|160|5|1|growth:40 defense:15|neutronium",
            "orbital_habitat|1|15|25|3|6|growth:6 prod:3|",
            "helium3_refinery|1|16|40|2|3|prod:12|helium3",
            "dyson_swarm|1|17|80|4|1|prod:25 research:10 energy_eff:20|asteroid_metals",
            "stanford_torus|1|17|40|3|5|growth:8 research:4|asteroid_metals",
            "hollow_asteroid|1|17|35|2|5|growth:6 build:6|asteroid_metals",
            "oneill_cylinder|1|18|50|3|5|growth:10 prod:4|asteroid_metals",
            "bernal_sphere|1|18|45|3|5|growth:8 culture:4|asteroid_metals",
            "dyson_bubble|1|18|90|3|2|prod:20 research:8|asteroid_metals",
            "laser_propulsor|1|18|60|3|2|speed:10|helium3|0|1.5",
            "star_memorial|1|18|30|2|3|stability:10 loyalty:10|",
            "dyson_sphere|1|19|150|5|1|prod:50 research:20 energy_eff:30|asteroid_metals",
            "bishop_ring|1|19|70|3|3|growth:12 health:8|",
            "antimatter_collector|1|19|70|3|2|attack:15 prod:10|antimatter",
            "mega_art|1|19|50|3|3|culture:20 stability:6 prestige:15|",
            "stellar_engine|1|20|120|4|1|defense:20 trade:10|helium3|30|1.5",
            "science_nexus|1|20|80|4|1|research:40|exotic_matter",
            "grand_archive|1|20|70|3|1|research:15 culture:15 stability:10|",
            "shkadov_thruster|1|21|120|3|1|defense:15 speed:8|helium3|0|1.5",
            "mckendree_cylinder|1|21|90|3|3|growth:18 prod:6|asteroid_metals",
            "mega_shipyard|1|21|90|3|1|naval:30 build:10|asteroid_metals",
            "matrioshka_brain|1|22|160|5|1|research:60 culture:10|stellar_energy",
            "star_lifting|1|22|110|3|1|prod:25 health:5|stellar_energy",
            "sentry_array|1|22|70|3|1|defense:15|exotic_matter|15",
            "gravitational_balloon|1|23|110|4|2|growth:22|exotic_matter",
            "arc_furnace|1|23|80|3|2|prod:30|stellar_energy",
            "deep_space_citadel|1|24|100|3|2|defense:30|neutronium|50",
            "ringworld|1|26|200|5|1|growth:20 health:15 stability:10|neutronium",
            "quantum_catapult|1|26|110|3|1|speed:15|exotic_matter|0|3",
            "topopolis|1|28|220|5|1|growth:30 trade:20|neutronium",
            "nicoll_dyson_beam|1|29|200|4|1|attack:60|stellar_energy|60",
            "alderson_disk|1|30|260|5|1|growth:40 prod:30|neutronium",
            "planet_forge|1|31|240|5|1|growth:30 prod:20|neutronium",
            "ark_ship|2|18|120|4|1|growth:15 stability:8|helium3",
            "interstellar_beacon|2|19|60|2|2|trade:10 research:6|",
            "waystation|2|20|50|2|3|naval:15 speed:5|helium3",
            "warp_relay|2|21|90|3|2|trade:15 speed:8|exotic_matter|0|2",
            "star_gate|3|23|100|3|1|trade:30|exotic_matter|0|3",
            "mine_wall|3|23|90|3|1|defense:30|antimatter|40",
            "interstellar_assembly|3|24|100|3|1|stability:15 loyalty:15 trade:10|",
            "wormhole_gate|4|24|120|4|1|trade:35|exotic_matter|0|5",
            "nebula_harvester|4|25|100|3|2|prod:35|exotic_matter",
            "portal_network|4|26|140|4|1|trade:35|exotic_matter|0|6",
            "mind_loom|4|27|140|4|1|research:60|exotic_matter",
            "core_brain|5|28|250|5|1|research:100|neutronium",
            "galactic_archive|5|29|180|3|1|research:40 culture:40|neutronium",
            "galactic_dyson|5|34|400|5|1|prod:300 research:60|neutronium",
            "penrose_ring|5|30|220|4|1|prod:150|neutronium",
            "star_crucible|5|30|200|4|1|prod:80|neutronium",
            "galactic_barrier|5|31|240|4|1|defense:60|neutronium|120",
            "ancient_lock|5|31|200|3|1|stability:20 defense:30|exotic_matter|60",
            "satellite_net|6|31|220|3|1|trade:40 speed:10|dark_matter|0|4",
            "intergalactic_bridge|6|32|300|4|1|trade:50|dark_matter|0|5",
            "guided_merger|6|32|300|5|1|growth:60 prod:60|dark_matter",
            "black_hole_engine|7|33|300|4|1|prod:150 energy_eff:40|dark_matter",
            "quasar_beacon|7|34|300|4|1|prod:120 attack:60|dark_matter",
            "cluster_fortress|7|35|280|4|1|defense:80|dark_matter|200",
            "attractor_engine|8|37|400|5|1|prod:200 speed:20|dark_matter|0|5",
            "dark_matter_web|8|38|400|5|1|research:150 prod:150|dark_matter|0|3",
            "tamed_string|8|38|380|4|1|speed:30 trade:60|dark_matter|0|8",
            "vacuum_cradle|9|40|450|5|1|stability:40 health:40|vacuum_energy",
            "constant_rewrite|9|41|500|5|1|research:200 prod:200 stability:30 health:30|vacuum_energy",
            "universe_seed|9|41|500|5|1|research:150|vacuum_energy",
            "omega_engine|9|41|600|5|1|prod:300 research:300|vacuum_energy",
        };

        public static readonly List<MegaDef> Defs = Raw.Select(Parse).ToList();
        private static readonly Dictionary<string, MegaDef> _byId = Defs.ToDictionary(d => d.Id);
        private const int RuinCount = 14;

        private static MegaDef Parse(string line)
        {
            string[] f = line.Split('|');
            var d = new MegaDef { Id = f[0], Level = int.Parse(f[1]), Era = int.Parse(f[2]), Years = int.Parse(f[3]), Stages = int.Parse(f[4]), Max = int.Parse(f[5]) };
            foreach (string p in f[6].Split(' '))
            {
                int i = p.IndexOf(':');
                if (i > 0) d.Fx[p.Substring(0, i)] = float.Parse(p.Substring(i + 1), CultureInfo.InvariantCulture);
            }
            d.Mats = f[7].Length > 0 ? f[7].Split(',') : new string[0];
            d.Shield = f.Length > 8 ? float.Parse(f[8], CultureInfo.InvariantCulture) : 0f;
            d.Speed = f.Length > 9 ? float.Parse(f[9], CultureInfo.InvariantCulture) : 1f;
            return d;
        }

        public static MegaDef Def(string id)
        {
            MegaDef d;
            return id != null && _byId.TryGetValue(id, out d) ? d : null;
        }

        public static IEnumerable<Mega> Of(Kingdom k)
        {
            if (Cosmos.St == null || !G.Ok(k)) yield break;
            long key = Cosmos.Key(k);
            foreach (Mega m in Cosmos.St.Megas) if (!m.Ruin && m.King == key) yield return m;
        }

        public static bool Has(Kingdom k, string id)
        {
            return Of(k).Any(m => m.Done && m.Id == id);
        }

        public static int Stage(Mega m, MegaDef d)
        {
            if (m.Done) return d.Stages;
            return Math.Min(d.Stages - 1, (int)Math.Floor(m.Progress * d.Stages + 1e-4f));
        }

        public static int Admin(Kingdom k)
        {
            int cities = G.Cities(k).Count();
            int cols = Cosmos.Of(k).Count();
            return 2 + cities / 4 + cols / 3 + CosmosSim.CachedScale(k) + (int)(Politics.Stability(k) * 2f);
        }

        public static int Used(Kingdom k)
        {
            int n = 0;
            foreach (Mega m in Of(k))
            {
                MegaDef d = Def(m.Id);
                n += d != null ? d.Weight : 1;
            }
            return n;
        }

        public static int Logistics(Kingdom k)
        {
            int pads = 0;
            foreach (City c in G.Cities(k)) pads += c.countBuildingsType("type_civ_launchpad");
            return Math.Min(6, 1 + pads / 2 + Spacecraft.Mix(k).Builders / 2);
        }

        private static IEnumerable<string> Bases(Kingdom k)
        {
            string home = Cosmos.St.Home;
            if (!string.IsNullOrEmpty(home)) yield return home;
            foreach (Polity p in Cosmos.Of(k)) if (!string.IsNullOrEmpty(p.Seat)) yield return p.Seat;
        }

        private static bool Taken(Kingdom k, MegaDef d, string node)
        {
            long key = Cosmos.Key(k);
            foreach (Mega m in Cosmos.St.Megas)
            {
                if (m.Id != d.Id || m.Node != node) continue;
                if (m.Ruin || d.Unique || m.King == key) return true;
            }
            return false;
        }

        internal static string Site(Kingdom k, MegaDef d)
        {
            var options = new List<string>();
            foreach (string b in Bases(k))
            {
                string n = d.Level == 0 ? b : CosmosGen.Ancestor(b, d.Level);
                if (string.IsNullOrEmpty(n) || options.Contains(n) || Taken(k, d, n)) continue;
                options.Add(n);
            }
            if (options.Count == 0) return d.Level == 0 ? Cosmos.St.Home : CosmosGen.Ancestor(Cosmos.St.Home, d.Level);
            return options[G.Range(0, options.Count)];
        }

        private static string FreeSite(Kingdom k, MegaDef d)
        {
            foreach (string b in Bases(k).OrderBy(x => G.Range(0, 1000)))
            {
                string n = d.Level == 0 ? b : CosmosGen.Ancestor(b, d.Level);
                if (!string.IsNullOrEmpty(n) && !Taken(k, d, n)) return n;
            }
            return null;
        }

        private static bool Unlocked(Kingdom k, MegaDef d)
        {
            return Research.HasUnlock(k, "mega:" + d.Id);
        }

        public static void Year(int yrs)
        {
            CosState st = Cosmos.St;
            if (!st.Seeded.Contains("mega_ruins"))
            {
                st.Seeded.Add("mega_ruins");
                SeedRuins();
            }
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv() || Research.Era(k) < 15 || Cosmos.Remote(Cosmos.Key(k))) continue;
                Plan(k, yrs);
            }
            foreach (Mega m in st.Megas.ToList())
            {
                if (m.Done || m.Ruin || Cosmos.Remote(m.King)) continue;
                Kingdom k = Cosmos.KingOf(m.King);
                if (!G.Ok(k))
                {
                    Abandon(m);
                    continue;
                }
                MegaDef d = Def(m.Id);
                if (d == null) continue;
                Build(k, m, d, yrs);
            }
        }

        private static void Plan(Kingdom k, int yrs)
        {
            int admin = Admin(k), used = Used(k);
            if (used >= admin) return;
            int active = Of(k).Count(m => !m.Done);
            if (active >= Logistics(k)) return;
            if (Player.MegaPick(k) == null && !G.Chance(Math.Min(0.8f, 0.15f * yrs))) return;
            CosState st = Cosmos.St;
            long key = Cosmos.Key(k);
            foreach (Mega r in st.Megas.Where(x => x.Ruin).ToList())
            {
                MegaDef d = Def(r.Id);
                if (d == null || !Unlocked(k, d) || used + d.Weight > admin) continue;
                if (!Bases(k).Any(b => (d.Level == 0 ? b : CosmosGen.Ancestor(b, d.Level)) == r.Node)) continue;
                r.Ruin = false;
                r.King = key;
                r.Started = G.Year;
                Cosmos.Dirty();
                Note(k, "cos_mega_reformed", r);
                return;
            }
            MegaDef want = Player.MegaPick(k);
            if (want != null)
            {
                if (Unlocked(k, want) && used + want.Weight <= admin && Of(k).Count(m => m.Id == want.Id) < want.Max)
                {
                    string at = FreeSite(k, want);
                    if (at != null) st.Megas.Add(new Mega { Id = want.Id, King = key, Node = at, Started = G.Year });
                }
                Player.ClearMega(k);
                return;
            }
            if (Player.Locks(k, "space")) return;
            var pool = Defs.Where(d => Unlocked(k, d) && used + d.Weight <= admin && Of(k).Count(m => m.Id == d.Id) < d.Max).ToList();
            if (pool.Count == 0) return;
            var weights = pool.Select(d => 1f / (1f + Of(k).Count(m => m.Id == d.Id)) * (1f + d.Era * 0.05f)).ToList();
            MegaDef pick = pool[G.Weighted(weights)];
            string node = FreeSite(k, pick);
            if (node == null) return;
            st.Megas.Add(new Mega { Id = pick.Id, King = key, Node = node, Started = G.Year });
        }

        private static void Build(Kingdom k, Mega m, MegaDef d, int yrs)
        {
            City cap = k.capital;
            int upkeep = 10 + d.Level * 15;
            bool paid = cap != null && Economy.Amount(cap, "gold") >= upkeep;
            if (paid) Economy.Take(cap, "gold", upkeep);
            float rate = Bonus.Mult(k, "build") * Bonus.Mult(k, "prod") * 0.5f * (paid ? 1f : 0.3f) * Spacecraft.BuildMult(k);
            if (d.Mats.Length > 0)
            {
                Dictionary<string, float> stock = CosmosSim.Stock(k);
                foreach (string r in d.Mats) if (Store.Get(stock, r) <= 0f) rate *= 0.5f;
            }
            if (cap != null)
            {
                int need = 2 + d.Level;
                bool steel = Economy.Has(cap, "civ_steel", need), elec = Economy.Has(cap, "civ_electronics", need);
                if (steel) Economy.Take(cap, "civ_steel", need);
                if (elec) Economy.Take(cap, "civ_electronics", need);
                if (!steel || !elec) rate *= 0.7f;
            }
            if (Has(k, "mega_shipyard")) rate *= 1.1f;
            float tf = Math.Min(30f, Cosmos.TimeFactor(Math.Max(1, d.Level)));
            int before = Stage(m, d);
            m.Progress = Math.Min(1f, m.Progress + yrs * rate * tf / d.Years);
            if (World.world.wars.hasWars(k) && G.Chance(0.01f * yrs))
            {
                m.Progress = Math.Max(0f, m.Progress - 0.15f);
                Internet.Official(k, "war", Txt.F("cos_news_sabotage", Txt.T("cos_mega_" + m.Id)), false);
            }
            if (m.Progress < 1f)
            {
                int now = Stage(m, d);
                if (now > before)
                {
                    m.Stage = now;
                    Bonus.Dirty(k);
                    Internet.Official(k, "space", Txt.F("cos_news_mega_stage", G.Name(k), Txt.T("cos_mega_" + m.Id), now, d.Stages), false);
                }
                return;
            }
            m.Done = true;
            m.Stage = d.Stages;
            Bonus.Dirty(k);
            CosmosHub.Mega(k, m.Id, Place(m));
            if (m.Id == "dyson_swarm") Kardashev.Check(k);
        }

        private static string Place(Mega m)
        {
            CNode n = CosmosGen.Get(m.Node);
            return n != null ? n.Name : "";
        }

        private static void Abandon(Mega m)
        {
            MegaDef d = Def(m.Id);
            if (d == null || m.Progress < 0.2f)
            {
                Cosmos.St.Megas.Remove(m);
                return;
            }
            m.Ruin = true;
            m.King = 0;
            m.Done = false;
            m.Progress = Math.Min(m.Progress, 0.5f);
            m.Stage = Stage(m, d);
            Cosmos.Dirty();
        }

        private static void SeedRuins()
        {
            string home = Cosmos.St.Home;
            if (string.IsNullOrEmpty(home)) return;
            var r = new Random(Cosmos.St.Seed ^ 0x5eed);
            var pool = Defs.Where(d => d.Level >= 1 && d.Level <= 7).ToList();
            for (int i = 0; i < RuinCount; i++)
            {
                MegaDef d = pool[r.Next(0, pool.Count)];
                string region = CosmosGen.Ancestor(home, Math.Min(9, d.Level + 2));
                string body = CosmosGen.RandomBody(region, r);
                if (body == null) continue;
                string node = CosmosGen.Ancestor(body, d.Level);
                if (node == null || CosmosGen.Contains(node, home) || Cosmos.St.Megas.Any(x => x.Node == node && x.Id == d.Id)) continue;
                float p = 0.3f + (float)r.NextDouble() * 0.5f;
                var ruin = new Mega { Id = d.Id, Node = node, Ruin = true, Progress = p, Started = -r.Next(1000, 100000) };
                ruin.Stage = Stage(ruin, d);
                Cosmos.St.Megas.Add(ruin);
            }
        }

        public static void War(Kingdom win, Kingdom lose, string seat)
        {
            if (!G.Ok(win) || !G.Ok(lose) || Cosmos.St == null) return;
            Mega m = Of(lose).Where(x => x.Progress > 0.1f && (x.Node == seat || CosmosGen.Contains(x.Node, seat ?? ""))).OrderBy(x => G.Range(0, 1000)).FirstOrDefault();
            if (m == null || !G.Chance(0.4f)) return;
            MegaDef d = Def(m.Id);
            if (d == null) return;
            if (G.Chance(0.5f) && Used(win) + d.Weight <= Admin(win))
            {
                m.King = Cosmos.Key(win);
                Bonus.Dirty(win);
                Bonus.Dirty(lose);
                Note(win, "cos_mega_captured", m, lose);
            }
            else
            {
                Abandon(m);
                Bonus.Dirty(lose);
                Note(win, "cos_mega_ruined", m, lose);
            }
        }

        public static void Raid(Kingdom victim, string seat)
        {
            if (!G.Ok(victim) || Cosmos.St == null) return;
            Mega m = Of(victim).FirstOrDefault(x => x.Node == seat || CosmosGen.Contains(x.Node, seat ?? ""));
            if (m == null || !G.Chance(0.3f)) return;
            Abandon(m);
            Bonus.Dirty(victim);
            Note(victim, "cos_mega_ruined", m, null);
        }

        private static void Note(Kingdom k, string type, Mega m, Kingdom other = null)
        {
            string name = Txt.T("cos_mega_" + m.Id), place = Place(m);
            string who = G.Ok(k) ? G.Name(k) : "", them = G.Ok(other) ? G.Name(other) : "";
            Cosmos.Log(Txt.F("cos_log_" + type.Substring(4), who, name, place, them));
            if (!G.Ok(k)) return;
            Chronicle.Log(type, k, who, name, place);
            Internet.Official(k, "space", Txt.F("cos_news_" + type.Substring(4), who, name, place, them), false);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            foreach (Mega m in Of(k))
            {
                MegaDef d = Def(m.Id);
                if (d == null) continue;
                float share = m.Done ? 1f : d.Stages <= 1 ? 0f : (float)Stage(m, d) / d.Stages * 0.6f;
                if (share <= 0f) continue;
                foreach (var kv in d.Fx) Store.Add(fx, kv.Key, kv.Value * share);
                if (m.Done) Store.Add(fx, "prestige", 5f + d.Level * 3f);
            }
            return fx;
        }

        public static float Shield(Kingdom k)
        {
            float s = 0f;
            foreach (Mega m in Of(k))
            {
                MegaDef d = m.Done ? Def(m.Id) : null;
                if (d != null) s += d.Shield;
            }
            return s;
        }

        public static float SpeedMult(Kingdom k)
        {
            float s = 1f;
            foreach (Mega m in Of(k))
            {
                MegaDef d = m.Done ? Def(m.Id) : null;
                if (d != null) s = Math.Max(s, d.Speed);
            }
            return s;
        }
    }
}
