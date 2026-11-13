using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class CosmosSim
    {
        public const string KFx = "civ_cos_fx";
        private static readonly double[] DriveLy = { 0.0, 0.01, 0.05, 0.6, 25, 400, 6000, 1.2e5, 1.5e6, 2e7, 1e10 };
        private static readonly double[] CommLy = { 1.0, 1.0, 120, 1e12 };
        private static readonly Dictionary<long, KeyValuePair<int, int>> _scale = new Dictionary<long, KeyValuePair<int, int>>();

        public static void Clear()
        {
            _scale.Clear();
        }

        public static int Drive(Kingdom k)
        {
            return Math.Max(0, Research.Level(k, "cos:drive:"));
        }

        public static int Reach(Kingdom k)
        {
            return Math.Max(0, Research.Level(k, "cos:colony:"));
        }

        public static int Comm(Kingdom k)
        {
            return Math.Max(0, Research.Level(k, "cos:comm:"));
        }

        public static int CachedScale(Kingdom k)
        {
            if (!G.Ok(k) || !Cosmos.Ready) return 0;
            KeyValuePair<int, int> c;
            if (_scale.TryGetValue(k.data.id, out c) && c.Key == G.Year) return c.Value;
            int s = Cosmos.Scale(k);
            _scale[k.data.id] = new KeyValuePair<int, int>(G.Year, s);
            Store.Put(k.data, Cosmos.KScale, s);
            return s;
        }

        public static int AlienDrive(int era)
        {
            if (era < Research.PlanetEras) return 0;
            return Math.Min(10, 1 + (era - Research.PlanetEras) * 10 / 27);
        }

        public static double Speed(int drive)
        {
            return DriveLy[Math.Max(0, Math.Min(DriveLy.Length - 1, drive))];
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            return G.Ok(k) ? Store.Map(k.data, KFx) : new Dictionary<string, float>();
        }

        public static void Year(int yrs)
        {
            CosState st = Cosmos.St;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                int era = Research.Era(k);
                if (era < 13 && !Cosmos.Of(k).Any()) continue;
                CivMod.Safe("cos-expand", () => Expand(k, yrs));
            }
            CivMod.Safe("cos-voyages", Voyages);
            CivMod.Safe("cos-polities", () => Polities(yrs));
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                CivMod.Safe("cos-fx", () => Fx(k));
            }
            CivMod.Safe("cos-aliens", () => CosmosLife.Year(yrs));
            CivMod.Safe("cos-war", () => CosmosWar.Year(yrs));
            CivMod.Safe("cos-invasions", () => Invasions.Year(yrs));
            CivMod.Safe("cos-megas", () => Megastructures.Year(yrs));
            CivMod.Safe("cos-ending", Ending.Check);
        }

        private static int Pending(Kingdom k)
        {
            long id = Cosmos.Key(k);
            return Cosmos.St.Voyages.Count(v => v.King == id && v.Kind == "colony");
        }

        private static void Expand(Kingdom k, int yrs)
        {
            int reach = Reach(k);
            int drive = Drive(k);
            if (reach <= 0 || drive <= 0) return;
            int colonies = Cosmos.Of(k).Count();
            if (Pending(k) >= 1 + reach / 2) return;
            KProfile p = Profile.Get(k);
            float want = 0.12f + 0.06f * reach;
            if (p.Fam.Contains("imperial") || p.Ideo == "nationalism") want += 0.08f;
            if (p.Ideo == "environmentalism") want -= 0.04f;
            if (colonies > 6 * reach) want *= 0.3f;
            want *= Spacecraft.ColonyMult(k);
            if (!G.Chance(Math.Min(0.9f, want * yrs))) return;
            int scope = Math.Min(reach, ScopeFor(drive));
            string target = Target(k, scope);
            if (target == null) return;
            if (!PayLaunch(k, scope)) return;
            Launch(k, null, Seat(k), target, "colony", 1f);
        }

        public static int ScopeFor(int drive)
        {
            if (drive <= 1) return 1;
            if (drive <= 3) return 2;
            if (drive == 4) return 3;
            if (drive == 5) return 4;
            if (drive == 6) return 5;
            if (drive == 7) return 6;
            if (drive == 8) return 7;
            if (drive == 9) return 8;
            return 9;
        }

        public static string Seat(Kingdom k)
        {
            return Cosmos.St.Home;
        }

        private static bool PayLaunch(Kingdom k, int scope)
        {
            City cap = k.capital;
            if (!G.Ok(cap)) return false;
            int gold = 30 + scope * 40;
            if (Economy.Amount(cap, "gold") < gold) return G.Chance(0.3f);
            Economy.Take(cap, "gold", gold);
            Economy.TakeUpTo(cap, "civ_fuel", 10);
            return true;
        }

        public static string Target(Kingdom k, int scope)
        {
            string home = Cosmos.St.Home;
            string region = CosmosGen.Ancestor(home, scope);
            if (region == null) return null;
            var r = new Random(unchecked(G.Year * 31 + (int)k.data.id));
            string best = null;
            float score = 0f;
            for (int tries = 0; tries < 8; tries++)
            {
                string node = Descend(region, scope, r);
                if (node == null || node == home) continue;
                if (Cosmos.Owner(node) != null && Cosmos.OwnerExact(node) != null) continue;
                Polity owner = Cosmos.Owner(node);
                if (owner != null && owner.King != Cosmos.Key(k) && owner.Kind != "colony") continue;
                CNode b = CosmosGen.Get(node);
                float s = 0.1f + b.Hab * 2f + b.Res.Length * 0.4f + (float)r.NextDouble() * 0.3f;
                int spread = Cosmos.Spread(home, node);
                s *= spread == scope ? 1.3f : 1f;
                if (s > score)
                {
                    score = s;
                    best = node;
                }
            }
            return best;
        }

        private static string Descend(string region, int scope, Random r)
        {
            CNode n = CosmosGen.Get(region);
            int guard = 0;
            while (n != null && n.Level > 0 && guard++ < 12)
            {
                if (n.Count <= 0) return null;
                string next = CosmosGen.Child(n.Path, r.Next(0, n.Count));
                if (CosmosGen.Contains(next, Cosmos.St.Home) && r.NextDouble() < 0.5 && n.Level > 1)
                {
                    next = CosmosGen.Child(n.Path, r.Next(0, n.Count));
                }
                n = CosmosGen.Get(next);
            }
            return n != null && n.Level == 0 ? n.Path : null;
        }

        public static Voyage Launch(Kingdom k, Polity from, string origin, string target, string kind, float power)
        {
            int drive = G.Ok(k) ? Drive(k) : from != null ? AlienDrive(from.Era) : 1;
            double dist = CosmosGen.DistanceLy(origin, target);
            double speed = Math.Max(1e-6, Speed(drive) * (G.Ok(k) ? Megastructures.SpeedMult(k) : 1f));
            int scale = Cosmos.Spread(Cosmos.St.Home, target);
            double years = dist / speed / Cosmos.TimeFactor(scale);
            int arrive = Cosmos.Now + Math.Max(1, (int)Math.Ceiling(Math.Min(5000, years)));
            var v = new Voyage { King = G.Ok(k) ? Cosmos.Key(k) : 0, Polity = from != null ? from.Id : 0, From = origin, To = target, Depart = Cosmos.Now, Arrive = arrive, Kind = kind, Power = power };
            Cosmos.St.Voyages.Add(v);
            CosmosHub.Voyage(k, v);
            return v;
        }

        private static void Voyages()
        {
            List<Voyage> list = Cosmos.St.Voyages;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Voyage v = list[i];
                if (v.Arrive > Cosmos.Now) continue;
                if (Cosmos.Remote(v.King) && Universe.Frozen(Cosmos.TagOf(v.King))) continue;
                list.RemoveAt(i);
                if (Cosmos.Remote(v.King))
                {
                    if (v.Kind == "colony") SettleRemote(v);
                    continue;
                }
                Kingdom k = Cosmos.KingOf(v.King);
                if (v.Kind == "colony") Settle(k, v);
                else if (v.Kind == "fleet") CosmosWar.Arrive(k, v);
                else if (v.Kind == "probe") CosmosLife.Probe(k, v);
                else if (v.Kind == "invasion") Invasions.Arrive(v);
            }
        }

        private static void SettleRemote(Voyage v)
        {
            if (Cosmos.OwnerExact(v.To) != null) return;
            Polity from = Cosmos.St.Polities.FirstOrDefault(p => p.King == v.King && p.Kind != "world") ?? Cosmos.St.Polities.FirstOrDefault(p => Cosmos.TagOf(p.King) == Cosmos.TagOf(v.King) && p.Kind == "world");
            if (from == null) return;
            CNode b = CosmosGen.Get(v.To);
            Polity p2 = Cosmos.NewPolity(null, b.Hab >= 0.35f ? "colony" : "outpost", v.To, ColonyName(null, b));
            p2.King = v.King;
            p2.Species = from.Species;
            p2.Era = from.Era;
            p2.Gov = from.Gov;
            p2.Ideo = from.Ideo;
            p2.Econ = from.Econ;
            p2.Color = from.Color;
            p2.Pop = 0.001f + b.Hab * 0.01f;
        }

        private static void Settle(Kingdom k, Voyage v)
        {
            Polity owner = Cosmos.OwnerExact(v.To);
            if (owner != null)
            {
                if (owner.King == v.King && v.King != 0) owner.Pop += 0.002f;
                return;
            }
            Polity alien = Cosmos.Owner(v.To);
            if (alien != null && alien.Kind == "alien" && !string.IsNullOrEmpty(alien.Seat) && CosmosGen.Contains(CosmosGen.ParentPath(alien.Seat), v.To))
            {
                CosmosLife.Contact(k, alien, v.To);
                return;
            }
            CNode b = CosmosGen.Get(v.To);
            Polity p;
            if (G.Ok(k))
            {
                p = Cosmos.NewPolity(k, b.Hab >= 0.35f ? "colony" : "outpost", v.To, ColonyName(k, b));
                p.Pop = 0.001f + b.Hab * 0.01f;
                CosmosHub.Colony(k, p, false);
                if (CosmosLife.Ruin(v.To)) CosmosLife.Excavate(k, v.To);
                CosmosLife.SeedAround(v.To);
            }
            else if (v.Polity != 0)
            {
                Polity from = Cosmos.Get(v.Polity);
                if (from == null) return;
                p = Cosmos.NewPolity(null, from.Kind == "alien" ? "alien" : "colony", v.To, ColonyName(null, b));
                p.Parent = from.Id;
                p.Species = from.Species;
                p.Era = from.Era;
                p.Color = from.Color;
                p.Gov = from.Gov;
                p.Ideo = from.Ideo;
                p.Econ = from.Econ;
                p.Pop = 0.002f;
                CosmosHub.Colony(null, p, false);
            }
        }

        public static string ColonyName(Kingdom k, CNode b)
        {
            var r = new Random(b.Seed);
            if (G.Ok(k) && r.NextDouble() < 0.4) return Txt.F("cos_name_new", G.Name(k.capital ?? G.Cities(k).FirstOrDefault()), b.Name);
            if (r.NextDouble() < 0.5) return b.Name;
            return CosmosGen.Word(r) + " " + Txt.T("cos_name_colony");
        }

        private static void Polities(int yrs)
        {
            foreach (Polity p in Cosmos.St.Polities.ToList())
            {
                if (p.Kind == "alien" || p.Kind == "invader" || p.Kind == "precursor" || p.Kind == "world") continue;
                bool remote = Cosmos.Remote(p.King);
                if (remote && Universe.Frozen(Cosmos.TagOf(p.King))) continue;
                Kingdom k = p.King != 0 && !remote ? Cosmos.KingOf(p.King) : null;
                if (p.King != 0 && !remote && !G.Ok(k))
                {
                    Orphan(p);
                    continue;
                }
                CNode seat = CosmosGen.Get(p.Seat);
                float hab = seat != null ? Math.Max(0.05f, seat.Hab) : 0.2f;
                if (G.Ok(k) && Research.HasFeature(k, "cos_terraform")) hab = Math.Min(1f, hab + 0.35f);
                float cap = 0.05f + hab * 20f + p.Nodes.Count * 2f;
                float tf = Cosmos.TimeFactor(Cosmos.Spread(Cosmos.St.Home, p.Seat));
                float growth = 0.04f * hab * yrs * Math.Min(20f, tf);
                p.Pop = Math.Min(cap, p.Pop + p.Pop * growth * (1f - p.Pop / Math.Max(0.01f, cap)) + 0.0005f * yrs);
                if (p.Grace > 0) p.Grace = Math.Max(0, p.Grace - yrs);
                if (G.Ok(k))
                {
                    p.Era = Math.Max(p.Era, Research.Era(k) - Lag(k, p));
                    p.Gov = Profile.Get(k).Gov;
                    p.Color = G.Hex(k);
                    Loyalty(k, p, yrs);
                }
                else Drift(p, yrs);
            }
        }

        private static int Lag(Kingdom k, Polity p)
        {
            double delay = CosmosGen.DistanceLy(Cosmos.St.Home, p.Seat) / CommLy[Math.Min(3, Comm(k))];
            if (delay < 5) return 0;
            if (delay < 200) return 1;
            return 2;
        }

        private static void Loyalty(Kingdom k, Polity p, int yrs)
        {
            double delay = CosmosGen.DistanceLy(Cosmos.St.Home, p.Seat) / CommLy[Math.Min(3, Comm(k))];
            float legit = Legitimacy.Get(k);
            float pull = 0.004f * (float)Math.Log(1.0 + delay) + 0.002f * (float)Math.Log(1.0 + p.Pop * 100f);
            float hold = 0.006f * legit + (Profile.Get(k).Fam.Contains("authoritarian") ? 0.003f : 0f);
            p.Loyalty = G.Clamp(p.Loyalty + (hold - pull) * yrs, 0f, 1f);
            if (p.Loyalty < 0.22f && p.Pop > 0.05f && G.Chance(0.15f * yrs))
            {
                p.King = 0;
                p.Kind = "independent";
                p.Name = Txt.F("cos_name_free", p.Name);
                p.Loyalty = 1f;
                Cosmos.Dirty();
                CosmosHub.Independence(k, p);
            }
        }

        private static void Orphan(Polity p)
        {
            p.King = 0;
            p.Kind = "independent";
            p.Loyalty = 1f;
            Cosmos.Dirty();
        }

        private static void Drift(Polity p, int yrs)
        {
            if (p.Pop < 0.02f && p.Era > 0 && G.Chance(0.01f * yrs))
            {
                p.Era--;
                if (p.Era <= 2) CosmosHub.Primitive(p);
            }
            else if (p.Pop > 0.5f && G.Chance(0.004f * yrs)) p.Era = Math.Min(Db.LastEra, p.Era + 1);
        }

        private static readonly Dictionary<string, string[]> ResFx = new Dictionary<string, string[]>
        {
            { "helium3", new[] { "prod", "energy_eff" } }, { "comet_ice", new[] { "growth", "health" } }, { "asteroid_metals", new[] { "build", "prod" } },
            { "antimatter", new[] { "attack", "prod" } }, { "exotic_matter", new[] { "speed", "research" } }, { "neutronium", new[] { "defense", "build" } },
            { "dark_matter", new[] { "research", "prod" } }, { "vacuum_energy", new[] { "prod", "energy_eff" } }, { "stellar_energy", new[] { "prod", "research" } }
        };

        public static Dictionary<string, float> Stock(Kingdom k)
        {
            var m = new Dictionary<string, float>();
            foreach (Polity p in Cosmos.Of(k))
            {
                foreach (string n in p.Nodes)
                {
                    CNode b = CosmosGen.Get(n);
                    if (b == null || b.Level != 0) continue;
                    foreach (string r in b.Res) if (Research.HasUnlock(k, "cos:res:" + r)) Store.Add(m, r, 1f + (float)Math.Log(1.0 + p.Pop * 10.0));
                }
            }
            if (Megastructures.Of(k).Any(x => x.Done && (x.Id == "dyson_swarm" || x.Id == "dyson_sphere"))) Store.Add(m, "stellar_energy", 6f);
            foreach (string r in new[] { "dark_matter", "vacuum_energy", "exotic_matter", "neutronium", "antimatter" })
            {
                if (Research.HasUnlock(k, "cos:res:" + r) && Store.Get(m, r) <= 0f) Store.Add(m, r, 2f);
            }
            return m;
        }

        private static Dictionary<string, float> Resources(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            foreach (var kv in Stock(k))
            {
                string[] stats;
                if (!ResFx.TryGetValue(kv.Key, out stats)) continue;
                float v = Math.Min(40f, kv.Value * 3f);
                foreach (string s in stats) Store.Add(fx, s, v);
            }
            return fx;
        }

        private static void Fx(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            float pop = 0f;
            int n = 0;
            foreach (Polity p in Cosmos.Of(k))
            {
                pop += p.Pop;
                n++;
            }
            if (n > 0)
            {
                float s = (float)Math.Log(1.0 + pop * 50.0);
                fx["research"] = s * 4f;
                fx["prod"] = s * 3f;
                fx["gold"] = s * 3f;
                fx["prestige"] = s * 5f + n;
            }
            foreach (var kv in Megastructures.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            foreach (var kv in CosmosLife.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            foreach (var kv in Resources(k)) Store.Add(fx, kv.Key, kv.Value);
            Dictionary<string, float> old = Store.Map(k.data, KFx);
            Store.Map(k.data, KFx, fx);
            if (old.Count != fx.Count || fx.Any(kv => Math.Abs(Store.Get(old, kv.Key) - kv.Value) > 0.5f)) Bonus.Dirty(k);
            int before = Store.Int(k.data, "civ_cos_scale_seen", 0);
            int now = CachedScale(k);
            if (now > before)
            {
                Store.Put(k.data, "civ_cos_scale_seen", now);
                CosmosHub.ScaleUp(k, now);
            }
        }
    }
}
