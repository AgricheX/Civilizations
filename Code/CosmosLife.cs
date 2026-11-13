using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal class AlienKind
    {
        public string Id;
        public string Life;
        public string[] Homes;
        public string[] Govs;
        public string[] Ideos;
        public string[] Econs;
        public float Pace;
        public float Hostility;
        public string[] SylA;
        public string[] SylB;
    }

    internal static class CosmosLife
    {
        public const int MaxPolities = 320;
        public static readonly List<AlienKind> Kinds = new List<AlienKind>
        {
            K("insectoid", "carbon", "terran,jungle,arid", "hive_mind,stellar_empire,military_junta", "militarism,cosmism", "planned,post_scarcity", 1.1f, 0.45f, "kri,tak,zix,chi,vek,tch", "x,k,ik,ax"),
            K("reptilian", "carbon", "terran,swamp,arid,volcanic", "stellar_empire,absolute_monarchy,cosmic_theocracy", "nationalism,militarism", "tributary,galactic_market", 1.0f, 0.5f, "ssa,thr,ka,zul,ssi,rah", "ss,th,ar,uk"),
            K("avian", "carbon", "terran,jungle,crystal", "interstellar_republic,planetary_federation,democracy", "liberalism,xenounity", "galactic_market,mixed_economy", 1.05f, 0.2f, "kee,ria,pio,tal,wee,sho", "ri,ka,el,ee"),
            K("amphibian", "carbon", "swamp,ocean,reef", "planetary_federation,tribal_council,distributed_consciousness", "environmentalism,humanism", "welfare,post_scarcity", 0.95f, 0.2f, "glo,bub,mur,ree,oon,plu", "lo,ub,ra,ee"),
            K("silicate", "silicate", "barren,volcanic,crystal", "distributed_consciousness,technocracy,ai_technocracy", "traditionalism,transhumanism", "planned,post_scarcity", 0.6f, 0.15f, "grak,tor,dum,kor,lith,zon", "k,th,on,um"),
            K("aquatic", "aquatic", "ocean,reef,abyss", "interstellar_republic,cosmic_theocracy,planetary_federation", "religious_zeal,xenounity", "galactic_market,welfare", 0.85f, 0.25f, "mua,lio,quo,waa,neri,sho", "a,o,ui,ea"),
            K("floater", "floater", "gas_giant", "distributed_consciousness,cosmic_theocracy", "humanism,cosmism", "post_scarcity", 0.7f, 0.1f, "aeo,vuu,lia,hoo,sae,mii", "u,a,ia,ao"),
            K("fungal", "fungal", "fungal,swamp,jungle", "hive_mind,distributed_consciousness", "environmentalism,xenounity", "post_scarcity,planned", 0.8f, 0.35f, "spo,myc,gol,rot,sha,umb", "ra,ium,ox,el"),
            K("hive", "hive", "arid,jungle,terran", "hive_mind", "militarism,cosmism", "planned", 1.25f, 0.6f, "zz,kir,vra,ch,xa,tik", "zz,ix,ra,ek"),
            K("machine", "machine", "machine,barren,asteroid", "ai_technocracy,technocracy", "transhumanism,authoritarianism", "planned,post_scarcity", 1.4f, 0.55f, "unit,zer,ax,kor,vex,ion", "-0,-1,-9,on"),
            K("energy", "energy", "gas_giant,volcanic,crystal", "distributed_consciousness,cosmic_theocracy", "cosmism,transhumanism", "post_scarcity", 0.5f, 0.3f, "ael,lum,syr,phae,iri,ora", "ae,is,yn,ar"),
        };

        private static AlienKind K(string id, string life, string homes, string govs, string ideos, string econs, float pace, float host, string a, string b)
        {
            return new AlienKind { Id = id, Life = life, Homes = homes.Split(','), Govs = govs.Split(','), Ideos = ideos.Split(','), Econs = econs.Split(','), Pace = pace, Hostility = host, SylA = a.Split(','), SylB = b.Split(',') };
        }

        public static AlienKind KindOf(string id)
        {
            foreach (AlienKind k in Kinds) if (k.Id == id) return k;
            return null;
        }

        private static string Name(AlienKind k, Random r)
        {
            int n = r.Next(2, 4);
            string s = "";
            for (int i = 0; i < n; i++) s += k.SylA[r.Next(0, k.SylA.Length)];
            s += k.SylB[r.Next(0, k.SylB.Length)];
            s = s.Replace("--", "-");
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static float Chance()
        {
            int f = Cosmos.St != null ? Cosmos.St.Fermi : 1;
            return f == 0 ? 0.03f : f == 2 ? 0.22f : 0.09f;
        }

        public static void Year(int yrs)
        {
            CosState st = Cosmos.St;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                foreach (Polity p in Cosmos.Of(k).ToList()) SeedAround(p.Seat);
            }
            foreach (Polity p in st.Polities.ToList())
            {
                if (p.Kind != "alien") continue;
                Grow(p, yrs);
            }
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv() || !Cosmos.Of(k).Any()) continue;
                Contacts(k);
                Opinions(k, yrs);
            }
            Council();
        }

        public static void SeedAround(string node)
        {
            if (string.IsNullOrEmpty(node)) return;
            string region = CosmosGen.Ancestor(node, 2);
            if (region == null || Cosmos.St.Seeded.Contains(region)) return;
            Cosmos.St.Seeded.Add(region);
            if (Cosmos.St.Seeded.Count > 4000) Cosmos.St.Seeded.RemoveAt(0);
            Seed(region);
        }

        private static void Seed(string region)
        {
            CNode n = CosmosGen.Get(region);
            var r = new Random(n.Seed ^ 0x5eed);
            float chance = Chance();
            foreach (CNode sys in CosmosGen.Children(n))
            {
                if (CosmosGen.Contains(sys.Path, Cosmos.St.Home) || CosmosGen.Homes.Any(h => CosmosGen.Contains(sys.Path, h))) continue;
                List<CNode> bodies = CosmosGen.Children(sys);
                if (bodies.Count == 0) continue;
                if (r.NextDouble() < chance * (Cosmos.St.Fermi == 0 ? 2.5 : 0.7))
                {
                    CNode b = bodies[r.Next(0, bodies.Count)];
                    if (!Cosmos.St.Ruins.Contains(b.Path)) Cosmos.St.Ruins.Add(b.Path);
                }
                if (Cosmos.St.Polities.Count >= MaxPolities || r.NextDouble() >= chance) continue;
                AlienKind kind = Kinds[r.Next(0, Kinds.Count)];
                CNode home = bodies.OrderByDescending(b => (Array.IndexOf(kind.Homes, b.Sub) >= 0 ? 2.0 : 0.0) + b.Hab + r.NextDouble() * 0.3).First();
                if (Cosmos.OwnerExact(home.Path) != null) continue;
                Spawn(kind, home, r);
            }
        }

        private static int RefEra()
        {
            int best = 0;
            foreach (Kingdom k in G.Kingdoms()) if (k.isCiv()) best = Math.Max(best, Research.Era(k));
            return best;
        }

        internal static Polity Spawn(AlienKind kind, CNode home, Random r)
        {
            int baseEra = RefEra();
            double roll = r.NextDouble();
            int era;
            string stance = "";
            if (roll < 0.35) era = r.Next(0, 10);
            else if (roll < 0.9) era = Math.Max(0, baseEra + r.Next(-8, 4));
            else
            {
                era = Math.Min(Db.LastEra, baseEra + r.Next(5, 12));
                stance = new[] { "guardian", "indifferent", "hunter" }[r.Next(0, 3)];
            }
            era = Math.Max(0, Math.Min(Db.LastEra, era));
            Polity p = Cosmos.NewPolity(null, "alien", home.Path, Name(kind, r));
            p.Species = kind.Id;
            p.Era = era;
            p.Gov = era < 4 ? "tribal_council" : kind.Govs[r.Next(0, kind.Govs.Length)];
            p.Ideo = kind.Ideos[r.Next(0, kind.Ideos.Length)];
            p.Econ = kind.Econs[r.Next(0, kind.Econs.Length)];
            p.Pace = kind.Pace;
            p.Pop = 0.5f + (float)r.NextDouble() * (era >= 15 ? 40f : 6f);
            p.Stance = stance.Length > 0 ? stance : r.NextDouble() < kind.Hostility ? "wary" : "open";
            p.Color = "#" + ((r.Next(0x40, 0xFF) << 16) | (r.Next(0x40, 0xFF) << 8) | r.Next(0x40, 0xFF)).ToString("X6");
            p.Grace = 0;
            int extra = era >= 15 ? Math.Min(6, 1 + (era - 15) / 2) : 0;
            List<CNode> sibs = CosmosGen.Children(CosmosGen.Get(CosmosGen.ParentPath(home.Path)));
            foreach (CNode b in sibs)
            {
                if (extra <= 0) break;
                if (b.Path == home.Path || Cosmos.OwnerExact(b.Path) != null) continue;
                p.Nodes.Add(b.Path);
                extra--;
            }
            Cosmos.Dirty();
            return p;
        }

        private static void Grow(Polity p, int yrs)
        {
            float tf = Math.Min(10f, Cosmos.TimeFactor(Research.ScaleOf(p.Era)));
            p.Pop = Math.Min(400f + p.Nodes.Count * 60f, p.Pop * (1f + 0.01f * yrs * tf) + 0.01f);
            if (p.Era < Db.LastEra && G.Chance(Math.Min(0.5f, 0.006f * p.Pace * yrs * tf))) p.Era++;
            if (p.Era >= Research.PlanetEras && Cosmos.St.Polities.Count < MaxPolities && G.Chance(0.02f * yrs))
            {
                string target = Near(p);
                if (target != null) CosmosSim.Launch(null, p, p.Seat, target, "colony", 1f).King = 0;
            }
        }

        private static string Near(Polity p)
        {
            int scope = Math.Max(1, Math.Min(5, Research.ScaleOf(p.Era)));
            string region = CosmosGen.Ancestor(p.Seat, scope);
            var r = new Random(unchecked(G.Year * 17 + (int)p.Id));
            for (int i = 0; i < 6; i++)
            {
                CNode n = CosmosGen.Get(region);
                int guard = 0;
                while (n != null && n.Level > 0 && n.Count > 0 && guard++ < 10) n = CosmosGen.Get(CosmosGen.Child(n.Path, r.Next(0, n.Count)));
                if (n == null || n.Level != 0 || Cosmos.Owner(n.Path) != null || CosmosGen.Contains(CosmosGen.Ancestor(Cosmos.St.Home, 1), n.Path)) continue;
                return n.Path;
            }
            return null;
        }

        private static int Gap(Kingdom k, Polity alien)
        {
            int best = CosmosGen.Universe;
            foreach (Polity p in Cosmos.Of(k)) foreach (string n in p.Nodes) best = Math.Min(best, Cosmos.Spread(n, alien.Seat));
            best = Math.Min(best, Cosmos.Spread(Cosmos.St.Home, alien.Seat));
            return best;
        }

        private static void Contacts(Kingdom k)
        {
            long id = Cosmos.Key(k);
            int reach = Math.Max(1, Math.Max(CosmosSim.Reach(k), CosmosSim.Drive(k) >= 2 ? 2 : 1));
            int detect = Research.Level(k, "cos:detect:");
            foreach (Polity a in Cosmos.St.Polities)
            {
                if (!Cosmos.Foreign(a) || a.Met.Contains(id)) continue;
                int gap = Gap(k, a);
                int alienReach = Research.ScaleOf(a.Era);
                if (gap > Math.Max(reach, Math.Max(alienReach, detect))) continue;
                Contact(k, a, a.Seat);
            }
        }

        public static void Contact(Kingdom k, Polity alien, string node)
        {
            if (alien == null || !G.Ok(k)) return;
            long id = Cosmos.Key(k);
            if (alien.Met.Contains(id)) return;
            alien.Met.Add(id);
            KProfile p = Profile.Get(k);
            string kind;
            if (alien.Era < 8) kind = "primitive";
            else if (alien.Stance == "hunter" || p.Xeno == "xenophobia" && alien.Stance != "guardian") kind = "hostile";
            else if (alien.Stance == "guardian") kind = "guardian";
            else if (alien.Stance == "wary" || Research.Era(k) < 17 && !Research.HasUnlock(k, "cos:contact")) kind = "wary";
            else if (Coherence.Is(alien.Econ, "mercantile") || Coherence.Is(p.Econ, "mercantile")) kind = "trade";
            else kind = "peaceful";
            float op = kind == "hostile" ? -50f : kind == "wary" ? -10f : kind == "trade" ? 20f : kind == "guardian" ? 30f : kind == "primitive" ? 0f : 25f;
            if (Research.HasUnlock(k, "cos:contact")) op += 10f;
            if (p.Xeno == "xenophilia" || p.Ideo == "xenounity") op += 15f;
            alien.Opinion[id] = op;
            CosmosHub.Contact(k, alien, kind);
            Fog.Version++;
        }

        private static void Opinions(Kingdom k, int yrs)
        {
            long id = Cosmos.Key(k);
            KProfile p = Profile.Get(k);
            foreach (Polity a in Cosmos.St.Polities)
            {
                if (!Cosmos.Foreign(a) || a.Kind == "invader" || !a.Met.Contains(id)) continue;
                float op;
                a.Opinion.TryGetValue(id, out op);
                float drift = 0f;
                if (a.Gov == p.Gov) drift += 1f;
                if (a.Ideo == p.Ideo) drift += 1f;
                if (p.Xeno == "xenophobia") drift -= 1.5f;
                if (p.Xeno == "xenophilia" || p.Ideo == "xenounity") drift += 1.5f;
                if (a.Stance == "hunter") drift -= 2f;
                if (Cosmos.St.Council.Contains(id) && Cosmos.St.Council.Contains(-a.Id)) drift += 1f;
                op = G.Clamp(op + drift * yrs * 0.5f - op * 0.01f * yrs, -100f, 100f);
                a.Opinion[id] = op;
            }
        }

        private static void Council()
        {
            CosState st = Cosmos.St;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv() || st.Council.Contains(Cosmos.Key(k)) || Research.Era(k) < 21) continue;
                long id = Cosmos.Key(k);
                List<Polity> friends = st.Polities.Where(a => Cosmos.Foreign(a) && a.Kind != "invader" && a.Met.Contains(id) && a.Opinion.ContainsKey(id) && a.Opinion[id] > 10f).ToList();
                if (friends.Count < 2) continue;
                bool first = st.Council.Count == 0;
                st.Council.Add(id);
                foreach (Polity a in friends) if (!st.Council.Contains(-a.Id)) st.Council.Add(-a.Id);
                if (first) Cosmos.Log(Txt.F("cos_log_council", G.Name(k)));
                Chronicle.Log("cos_first", k, G.Name(k), Txt.T(first ? "cos_council_founded" : "cos_council_joined"), "");
                Festivals.Mark(k, "council");
            }
        }

        public static void Probe(Kingdom k, Voyage v)
        {
            SeedAround(v.To);
        }

        public static bool Ruin(string node)
        {
            return Cosmos.St != null && Cosmos.St.Ruins.Contains(node);
        }

        public static void Excavate(Kingdom k, string node)
        {
            if (!G.Ok(k) || !Ruin(node)) return;
            Cosmos.St.Ruins.Remove(node);
            int era = Research.Era(k);
            var pool = Db.Techs.Where(t => t.Rare && t.Era >= Research.PlanetEras && t.Era <= era + 2 && !Research.Has(k, t.Id)).ToList();
            if (pool.Count == 0) return;
            TechDef pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (G.Chance(0.5f)) Research.Learn(k, pick, "artifact");
            else Research.Discover(k, pick);
            Chronicle.Log("cos_first", k, G.Name(k), Txt.T("cos_ruins_found"), Db.TechName(pick.Id));
            Internet.Official(k, "science", Txt.F("cos_news_ruins", G.Name(k), CosmosGen.Get(node).Name, Db.TechName(pick.Id)), true);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var m = new Dictionary<string, float>();
            if (Cosmos.St == null || !G.Ok(k)) return m;
            if (Cosmos.St.Council.Contains(Cosmos.Key(k)))
            {
                m["trade"] = 15f;
                m["research"] = 10f;
                m["stability"] = 4f;
            }
            return m;
        }
    }
}
