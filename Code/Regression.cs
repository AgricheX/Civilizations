using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Regression
    {
        private const string KDormant = "civ_dormant";
        private const string KLost = "civ_lost_techs";
        private const string KShock = "civ_reg_shock";
        private const string KDark = "civ_reg_dark";
        private const string KSmall = "civ_reg_small";
        private const string KAlone = "civ_reg_alone";
        private const string KFuel = "civ_reg_fuel";
        private const string KInfra = "civ_reg_infra";
        private const string KEmp = "civ_emp_until";
        private const string WNukes = "civ_reg_nukes";
        private const int LostAfter = 60;

        private static readonly HashSet<string> Electronic = new HashSet<string> { "comp", "comm", "space" };

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.Regression; }
        }

        public static bool Apocalypse
        {
            get { return On && (CivMod.C == null || CivMod.C.Apocalypse); }
        }

        public static Dictionary<string, float> Dormant(Kingdom k)
        {
            return Store.Map(k.data, KDormant);
        }

        public static bool IsDormant(Kingdom k, TechDef t)
        {
            return G.Ok(k) && t != null && Dormant(k).ContainsKey(t.Id);
        }

        public static float CostMult(Kingdom k, TechDef t)
        {
            if (!G.Ok(k) || t == null) return 1f;
            if (Dormant(k).ContainsKey(t.Id)) return 0.35f / Protection(k, 0.5f);
            if (Store.Set(k.data, KLost).Contains(t.Id)) return 0.8f;
            return 1f;
        }

        private static float Protection(Kingdom k, float weight)
        {
            int archives = 0;
            foreach (City c in G.Cities(k)) archives += c.countBuildingsType("type_library") + c.countBuildingsType("type_civ_university") * 2 + c.countBuildingsType("type_civ_lab") * 2;
            return 1f + weight * Math.Min(8, archives) * 0.25f;
        }

        public static TechDef Leaf(Kingdom k, Func<TechDef, bool> filter)
        {
            KState s = Research.State(k);
            if (s == null) return null;
            var needed = new HashSet<string>();
            foreach (TechDef t in s.Known.All()) foreach (string r in t.Req) needed.Add(r);
            TechDef pick = null;
            foreach (TechDef t in s.Known.All())
            {
                if (needed.Contains(t.Id)) continue;
                if (filter != null && !filter(t)) continue;
                if (Research.IsFundamental(t) && t.Era < Research.Era(k)) continue;
                if (pick == null || t.Era > pick.Era || (t.Era == pick.Era && t.Index > pick.Index)) pick = t;
            }
            return pick;
        }

        public static int Sleep(Kingdom k, string cause, int n, Func<TechDef, bool> filter = null, bool protect = true)
        {
            if (!On || !G.Ok(k)) return 0;
            int done = 0;
            float guard = protect ? 1f / Protection(k, 1f) : 1f;
            for (int i = 0; i < n; i++)
            {
                if (protect && i > 0 && !G.Chance(guard)) continue;
                TechDef t = Leaf(k, filter);
                if (t == null) break;
                Dictionary<string, float> d = Dormant(k);
                d[t.Id] = G.Year;
                Store.Map(k.data, KDormant, d);
                Research.Lose(k, t, "dormant:" + cause);
                Hub.Raise(Hub.TechDormant, k, null, k.capital, null, t.Id, cause);
                done++;
            }
            return done;
        }

        public static int Wake(Kingdom k, int n, string reason)
        {
            Dictionary<string, float> d = Dormant(k);
            if (d.Count == 0) return 0;
            int done = 0;
            foreach (var kv in d.OrderBy(x => Db.Tech(x.Key) != null ? Db.Tech(x.Key).Era : 99).ToList())
            {
                if (done >= n) break;
                TechDef t = Db.Tech(kv.Key);
                if (t == null)
                {
                    d.Remove(kv.Key);
                    continue;
                }
                bool ok = true;
                foreach (string r in t.Req) if (!Research.Has(k, r)) ok = false;
                if (!ok) continue;
                Research.Learn(k, t, reason);
                done++;
            }
            return done;
        }

        public static void Register()
        {
            Hub.On(Hub.TechLearned, e =>
            {
                if (!G.Ok(e.K)) return;
                Dictionary<string, float> d = Dormant(e.K);
                if (d.Remove(e.Id))
                {
                    Store.Map(e.K.data, KDormant, d);
                    Hub.Raise(Hub.TechRecovered, e.K, null, e.K.capital, null, e.Id, e.Arg);
                }
                HashSet<string> lost = Store.Set(e.K.data, KLost);
                if (lost.Remove(e.Id)) Store.Set(e.K.data, KLost, lost);
            });
            Hub.On(Hub.CityCaptured, e =>
            {
                if (!G.Ok(e.O) || e.C == null) return;
                float add = e.O.capital == null || e.O.capital == e.C ? 2f : 0.6f;
                Store.Put(e.O.data, KShock, Store.Flt(e.O.data, KShock, 0f) + add);
            });
            Hub.On(Hub.CivilWar, e =>
            {
                if (G.Ok(e.K)) Store.Put(e.K.data, KShock, Store.Flt(e.K.data, KShock, 0f) + 1.5f);
            });
            Hub.On(Hub.NukeDetonated, e =>
            {
                SaveCustomData w = Store.World();
                if (w == null) return;
                Dictionary<string, float> m = Store.ParseMap(Store.Str(w, WNukes));
                Store.Add(m, G.Year.ToString(), 1f);
                foreach (string y in m.Keys.ToList())
                {
                    int yy;
                    if (!int.TryParse(y, out yy) || G.Year - yy > 12) m.Remove(y);
                }
                Store.Put(w, WNukes, Store.WriteMap(m));
            });
            Hub.On(Hub.PolicyChanged, e =>
            {
                if (!G.Ok(e.K) || !On) return;
                PolicyDef p = Db.Policy(e.Id);
                if (p == null || p.Group != "ideo") return;
                KProfile pr = Profile.Get(e.K);
                if ((e.Id == "traditionalism" || e.Id == "religious_zeal") && pr.Era >= 9 && (pr.Fam.Contains("theocratic") || pr.Fam.Contains("authoritarian")) && G.Chance(0.25f))
                {
                    Sleep(e.K, "purism", G.Range(1, 3), t => t.Era >= pr.Era - 1 && (t.Cat == "sci" || t.Cat == "comp" || t.Cat == "med" || t.Cat == "soc"), false);
                }
            });
            Hub.On(Hub.Event, e =>
            {
                if (!G.Ok(e.K)) return;
                switch (e.Id)
                {
                    case "plague":
                        Sleep(e.K, "plague", G.Chance(0.5f) ? 1 : 0);
                        break;
                    case "solar_storm":
                        if (Research.Era(e.K) >= 13)
                        {
                            int n = Sleep(e.K, "solar_storm", G.Range(1, 4), t => Electronic.Contains(t.Cat), false);
                            if (n > 0) Store.Put(e.K.data, KEmp, G.Year + G.Range(3, 7));
                        }
                        break;
                    case "brain_drain":
                        Sleep(e.K, "brain_drain", 1, t => t.Cat == "sci" || t.Cat == "comp");
                        break;
                    case "renaissance":
                        Wake(e.K, G.Range(2, 5), "renaissance");
                        break;
                    case "great_library":
                        Wake(e.K, 1, "rediscovery");
                        break;
                }
            });
        }

        public static void ArchivesLost(Kingdom k)
        {
            Sleep(k, "archives", 1);
        }

        public static void DarkAge(Kingdom k)
        {
            if (Sleep(k, "dark_age", 1) > 0) foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_dark_age", 0.3f);
        }

        private static int MinPop(int era)
        {
            if (era < 6) return 0;
            return 30 + (era - 5) * 18;
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!On || !G.Ok(k)) return;
            KProfile p = Profile.Get(k);
            int era = p.Era;
            Dictionary<string, float> d = Dormant(k);
            if (d.Count > 0)
            {
                HashSet<string> lost = null;
                foreach (var kv in d.ToList())
                {
                    if (G.Year - (int)kv.Value < LostAfter) continue;
                    d.Remove(kv.Key);
                    if (lost == null) lost = Store.Set(k.data, KLost);
                    lost.Add(kv.Key);
                    Hub.Raise(Hub.TechLost, k, null, k.capital, null, kv.Key, "forgotten");
                }
                Store.Map(k.data, KDormant, d);
                if (lost != null) Store.Set(k.data, KLost, lost);
            }
            int empUntil = Store.Int(k.data, KEmp, 0);
            if (empUntil > 0 && G.Year >= empUntil)
            {
                Store.Put(k.data, KEmp, 0);
                Wake(k, 4, "restored");
            }
            float shock = Store.Flt(k.data, KShock, 0f);
            if (shock >= 3f && era >= 4)
            {
                int n = Math.Min(4, (int)(shock / 1.5f));
                Store.Put(k.data, KShock, 0f);
                if (Sleep(k, "collapse", n) > 0) foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_dark_age", 0.4f);
            }
            else Store.Put(k.data, KShock, Math.Max(0f, shock - 0.4f * yrs));
            int dark = Store.Int(k.data, KDark, 0);
            if (p.Legit < 0.3f && p.Stability < 0.35f && era >= 3) dark += yrs;
            else dark = Math.Max(0, dark - yrs);
            if (dark >= 20)
            {
                dark = 5;
                DarkAge(k);
            }
            Store.Put(k.data, KDark, dark);
            int small = Store.Int(k.data, KSmall, 0);
            if (p.Pop < MinPop(era) || era >= 9 && p.Cities < 2) small += yrs;
            else small = Math.Max(0, small - yrs * 2);
            if (small >= 15)
            {
                small = 0;
                Sleep(k, "too_small", 1, t => t.Era >= era - 1);
            }
            Store.Put(k.data, KSmall, small);
            if (era >= 9)
            {
                int alone = Store.Int(k.data, KAlone, 0);
                bool isolated = !Fog.KnownKingdoms(k).Any();
                alone = isolated ? alone + yrs : 0;
                if (alone >= 30)
                {
                    alone = 10;
                    Sleep(k, "isolation", 1);
                }
                Store.Put(k.data, KAlone, alone);
            }
            Infrastructure(k, p, yrs);
            Fuel(k, p, yrs);
        }

        private static void Infrastructure(Kingdom k, KProfile p, int yrs)
        {
            if (p.Era < 10) return;
            int factories = 0, power = 0, labs = 0;
            foreach (City c in G.Cities(k))
            {
                factories += c.countBuildingsType("type_civ_factory");
                power += c.countBuildingsType("type_civ_power");
                labs += c.countBuildingsType("type_civ_lab") + c.countBuildingsType("type_civ_university");
            }
            Dictionary<string, float> m = Store.Map(k.data, KInfra);
            Step(m, "ind", factories == 0, yrs);
            Step(m, "energy", p.Era >= 11 && power == 0, yrs);
            Step(m, "sci", p.Era >= 12 && labs == 0, yrs);
            foreach (string cat in new[] { "ind", "energy", "sci" })
            {
                if (Store.Get(m, cat) < 25f) continue;
                m[cat] = 10f;
                string cc = cat;
                Sleep(k, "infrastructure", 1, t => t.Cat == cc && t.Era >= 10);
            }
            Store.Map(k.data, KInfra, m);
        }

        private static void Step(Dictionary<string, float> m, string key, bool missing, int yrs)
        {
            float v = Store.Get(m, key);
            m[key] = missing ? v + yrs : Math.Max(0f, v - yrs * 2);
        }

        private static void Fuel(Kingdom k, KProfile p, int yrs)
        {
            if (p.Era < 11) return;
            float oil = 0f, uranium = 0f;
            foreach (City c in G.Cities(k))
            {
                oil += Economy.Amount(c, "civ_oil") + Economy.Amount(c, "civ_fuel");
                uranium += Economy.Amount(c, "civ_uranium");
            }
            Dictionary<string, float> dep = Economy.Depot(k);
            oil += Store.Get(dep, "civ_oil") + Store.Get(dep, "civ_fuel");
            uranium += Store.Get(dep, "civ_uranium");
            Dictionary<string, float> m = Store.Map(k.data, KFuel);
            Step(m, "diesel", p.Route == "diesel" && oil <= 0f, yrs);
            Step(m, "atom", p.Route == "atom" && uranium <= 0f, yrs);
            foreach (string r in new[] { "diesel", "atom" })
            {
                if (Store.Get(m, r) < 30f) continue;
                m[r] = 12f;
                string rr = r;
                Sleep(k, "depletion", 1, t => t.Route == rr);
            }
            Store.Map(k.data, KFuel, m);
        }

        public static void Year()
        {
            if (!Apocalypse) return;
            SaveCustomData w = Store.World();
            if (w == null) return;
            Dictionary<string, float> m = Store.ParseMap(Store.Str(w, WNukes));
            float n = 0f;
            foreach (float v in m.Values) n += v;
            if (n < 8f) return;
            Store.Put(w, WNukes, "");
            Hub.Raise(Hub.Event, null, null, null, null, "nuclear_winter", "", n);
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!G.Chance(0.6f)) continue;
                Sleep(k, "nuclear_winter", G.Range(1, 4));
                foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_disaster", 0.5f);
            }
        }
    }
}
