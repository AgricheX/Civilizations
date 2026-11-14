using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Mandate
    {
        private const string KValue = Gov.P + "mandate";
        private const string KDecay = Gov.P + "decay";
        private const string KWounds = Gov.P + "wounds";
        private const string KCap = Gov.P + "cap";
        private const string KResto = Gov.P + "resto";
        private const string KDyn = "civ_dyn";
        private const string KDynY = "civ_dyn_y";
        private const string KDynN = "civ_dyn_n";
        public const string KRebelUntil = Gov.P + "rebel_until";
        public const string KRebelOf = Gov.P + "rebel_of";
        public static readonly string[] Phases = { "renewal", "prime", "decline", "collapse" };
        public static readonly string[] Parts = { "loyal", "ruler", "vassals", "livelihood", "overextended", "treasury", "war", "decay", "wounds" };

        public static void Register()
        {
            Hub.On(Hub.PeaceTerms, e =>
            {
                if (!G.Ok(e.K) || !G.Ok(e.O) || e.Id == null || e.Id == "white_peace") return;
                bool small = e.Id == "reparations" || e.Id == "truce";
                Add(e.K, small ? 2f : 5f);
                Add(e.O, small ? -4f : -10f);
            });
            Hub.On(Hub.CityCaptured, e =>
            {
                if (e.Id != "captured" || !G.Ok(e.K) || !G.Ok(e.O)) return;
                Add(e.K, 1f);
                Add(e.O, -2f);
                if (e.C != null && Store.Str(e.O.data, KCap) == G.Id(e.C))
                {
                    Add(e.O, -15f);
                    if (Phase(e.O) == 2 && Active(e.O)) Store.Put(e.O.data, Gov.P + "cap_lost", 1);
                }
            });
        }

        public static bool Applies(Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv() || !Gov.On("civ_gov_cycle") || !GovEra.Has(k, GovEra.Mandate)) return false;
            if (Diplomacy.Overlord(k) != null) return false;
            return G.Cities(k).Count >= 3 || Diplomacy.DependentsOf(k).Count > 0;
        }

        public static bool Active(Kingdom k)
        {
            return Applies(k) && Phase(k) >= 0;
        }

        public static float Get(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KValue, 60f) : 0f;
        }

        public static void Set(Kingdom k, float v)
        {
            if (G.Ok(k)) Store.Put(k.data, KValue, G.Clamp(v, 0f, 100f));
        }

        public static void Add(Kingdom k, float v)
        {
            if (Active(k)) Set(k, Get(k) + v);
        }

        public static int Phase(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KDyn, -1) : -1;
        }

        public static int Years(Kingdom k)
        {
            return G.Ok(k) ? G.Year - Store.Int(k.data, KDynY, G.Year) : 0;
        }

        public static int Dynasty(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KDynN, 1) : 1;
        }

        public static float Decay(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KDecay, 0f) : 0f;
        }

        public static bool Rebelling(Kingdom k)
        {
            return G.Ok(k) && Store.Int(k.data, KRebelUntil, 0) > G.Year;
        }

        public static bool Restoring(Kingdom k)
        {
            return G.Ok(k) && Store.Int(k.data, KResto, 0) > 0;
        }

        public static string PhaseName(int ph)
        {
            return ph < 0 ? Txt.T("civ_mand_none") : Txt.T("civ_mand_phase_" + Phases[ph]);
        }

        public static Dictionary<string, float> Components(Kingdom k)
        {
            var c = new Dictionary<string, float>();
            if (!G.Ok(k)) return c;
            List<City> cities = G.Cities(k);
            int loyal = cities.Count(x => x.getLoyalty(false) >= 0);
            c["loyal"] = cities.Count > 0 ? ((float)loyal / cities.Count - 0.7f) * 30f : 0f;
            Actor r = k.king;
            c["ruler"] = G.Ok(r) ? G.Clamp(((r.stewardship + r.intelligence) / 2f - 5f) * 2f, -10f, 10f) : -15f;
            List<Kingdom> vs = Diplomacy.DependentsOf(k);
            c["vassals"] = vs.Count > 0 ? (vs.Average(v => Vassals.Integration(v)) - 50f) / 5f : 0f;
            c["livelihood"] = (Politics.Stability(k) - 0.5f) * 20f;
            c["overextended"] = vs.Count > 0 && cities.Count < 6 ? -(6 - cities.Count) * 3f : 0f;
            c["treasury"] = Gov.On("civ_gov_treasury") && Treasury.Owed(k) > 0f ? -8f : 0f;
            float war = 0f;
            foreach (global::War w in World.world.wars.getWars(k))
            {
                if (w == null || w.hasEnded()) continue;
                war += Statecraft.Score(k, w) / 10f;
            }
            c["war"] = G.Clamp(war, -10f, 6f);
            float d = Decay(k);
            c["decay"] = -(d / 40f) * (d / 40f) * 20f;
            c["wounds"] = -Store.Flt(k.data, KWounds, 0f);
            return c;
        }

        public static float Deserved(Kingdom k)
        {
            return G.Clamp(50f + Components(k).Values.Sum(), 0f, 100f);
        }

        public static void SetPhase(Kingdom k, int ph, bool log)
        {
            Store.Put(k.data, KDyn, ph);
            Store.Put(k.data, KDynY, G.Year);
            Bonus.Dirty(k);
            if (log && ph >= 0) Gov.Log("phase", k, G.Name(k), PhaseName(ph), ((int)Get(k)).ToString());
        }

        public static void NewDynasty(Kingdom k, bool reset)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KDynN, Dynasty(k) + 1);
            if (reset || Phase(k) < 0)
            {
                Store.Put(k.data, KDecay, 0f);
                Store.Put(k.data, KWounds, 0f);
                Set(k, 60f);
                if (Applies(k)) SetPhase(k, 0, true);
            }
        }

        public static void Year(Kingdom k, KProfile p, int yrs)
        {
            if (!Applies(k))
            {
                if (Phase(k) >= 0) SetPhase(k, -1, false);
                return;
            }
            if (k.capital != null) Store.Put(k.data, KCap, G.Id(k.capital));
            if (Phase(k) < 0)
            {
                Set(k, 60f);
                SetPhase(k, 0, true);
                return;
            }
            var comp = Components(k);
            float gov = comp["loyal"] + comp["ruler"] + comp["vassals"] + comp["livelihood"];
            Store.Put(k.data, KDecay, Decay(k) + yrs * (gov > 0f ? 0.8f : gov < -5f ? 1.3f : 1f));
            float m = Get(k);
            float t = G.Clamp(50f + comp.Values.Sum(), 0f, 100f);
            m += (t - m) * Math.Min(1f, 0.15f * yrs);
            Set(k, m);
            m = Get(k);
            int ph = Phase(k);
            bool capLost = Store.Int(k.data, Gov.P + "cap_lost", 0) > 0;
            Store.Put(k.data, Gov.P + "cap_lost", 0);
            switch (ph)
            {
                case 0:
                    if (m >= 70f) SetPhase(k, 1, true);
                    else if (m < 40f) SetPhase(k, 2, true);
                    break;
                case 1:
                    if (m < 50f) SetPhase(k, 2, true);
                    break;
                case 2:
                    if (m >= 70f) SetPhase(k, 1, true);
                    else if (m < 20f || capLost)
                    {
                        if (!GreatRebellion(k)) SetPhase(k, 3, true);
                    }
                    else if (m < 30f && G.Chance(0.2f * yrs)) Revolt(k);
                    break;
                case 3:
                    if (!Rebelling(k) || !World.world.wars.getWars(k).Any(w => w != null && !w.hasEnded()))
                    {
                        if (G.Cities(k).Count >= 3)
                        {
                            Store.Put(k.data, KWounds, Store.Flt(k.data, KWounds, 0f) + 8f);
                            Set(k, 45f);
                            SetPhase(k, 2, false);
                            Gov.Log("pacified", k, G.Name(k));
                        }
                        else SetPhase(k, -1, false);
                    }
                    break;
            }
            Restoration(k, yrs);
        }

        private static void Revolt(Kingdom k)
        {
            City c = G.Cities(k).Where(x => x != k.capital && x.getLoyalty(false) < 0).OrderBy(x => x.getLoyalty(false)).FirstOrDefault();
            Actor lead = Gov.Somebody(c);
            if (c == null || lead == null) return;
            Kingdom nk = c.makeOwnKingdom(lead, true, false);
            if (G.Ok(nk)) Store.Put(nk.data, KRebelOf, k.data.id.ToString());
        }

        private static void Restoration(Kingdom k, int yrs)
        {
            int due = Store.Int(k.data, KResto, 0);
            Actor r = k.king;
            if (due > 0)
            {
                if (G.Year < due) return;
                Store.Put(k.data, KResto, 0);
                float ch = 0.3f + (G.Ok(r) ? (r.stewardship + r.intelligence - 10) / 40f : -0.2f);
                if (G.Chance(G.Clamp(ch, 0.05f, 0.85f)))
                {
                    Store.Put(k.data, KDecay, Decay(k) * 0.4f);
                    Set(k, Math.Max(70f, Get(k)));
                    SetPhase(k, 1, false);
                    Gov.Log("restoration", k, G.Name(k), G.Ok(r) ? r.getName() : "-");
                }
                else
                {
                    Set(k, Get(k) - 10f);
                    Gov.Log("restoration_fail", k, G.Name(k), G.Ok(r) ? r.getName() : "-");
                }
                return;
            }
            if (Phase(k) != 2 || !G.Ok(r) || r.level < 5 || Player.Is(k) && !Player.Auto(k, "politics")) return;
            float want = r.stewardship / 10f * (70f - Get(k)) / 50f * 0.12f * yrs;
            if (G.Chance(want)) StartRestoration(k);
        }

        public static bool StartRestoration(Kingdom k)
        {
            if (!Active(k) || Restoring(k)) return false;
            Store.Put(k.data, KResto, G.Year + G.Range(1, 3));
            return true;
        }

        public static bool GreatRebellion(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            List<City> all = G.Cities(k).Where(c => G.Ok(c) && c != k.capital).ToList();
            if (all.Count < 2) return false;
            int want = Math.Min(6, Math.Max(1, all.Count / 4 + 1));
            var seeds = new List<City>();
            foreach (City c in all.OrderBy(c => c.getLoyalty(false)))
            {
                if (seeds.Count >= want) break;
                if (seeds.Any(x => x.neighbours_cities != null && x.neighbours_cities.Contains(c))) continue;
                seeds.Add(c);
            }
            var rebels = new List<Kingdom>();
            Vassals.Busy = true;
            try
            {
                foreach (City c in seeds)
                {
                    Actor lead = Gov.Somebody(c);
                    if (lead == null || c.kingdom != k) continue;
                    Kingdom nk = c.makeOwnKingdom(lead, true, false);
                    if (!G.Ok(nk)) continue;
                    Store.Put(nk.data, KRebelOf, k.data.id.ToString());
                    rebels.Add(nk);
                }
                if (rebels.Count == 0) return false;
                int target = (int)(all.Count * 0.6f) - rebels.Count;
                foreach (City c in all.Where(x => G.Ok(x) && x.kingdom == k).OrderBy(x => x.getLoyalty(false)).ToList())
                {
                    if (target-- <= 0) break;
                    Kingdom near = rebels.Where(G.Ok).OrderBy(r => r.capital != null ? G.Dist(r.capital, c) : 9999f).FirstOrDefault();
                    if (near != null) c.joinAnotherKingdom(near, false, true);
                }
            }
            finally
            {
                Vassals.Busy = false;
            }
            foreach (Kingdom nk in rebels)
            {
                if (!G.Ok(nk) || World.world.wars.isInWarWith(nk, k)) continue;
                Statecraft.Escalating = true;
                try
                {
                    World.world.diplomacy.startWar(nk, k, WarTypeLibrary.rebellion);
                }
                finally
                {
                    Statecraft.Escalating = false;
                }
            }
            foreach (Kingdom v in Diplomacy.DependentsOf(k).ToList())
            {
                if (G.Chance(1f - Vassals.Integration(v) / 110f)) Vassals.Independence(v, false);
            }
            Store.Put(k.data, KRebelUntil, G.Year + 12);
            SetPhase(k, 3, false);
            Set(k, Math.Min(Get(k), 15f));
            Legitimacy.Shock(k, -0.1f);
            Gov.Log("rebellion", k, G.Name(k), rebels.Count.ToString(), null, rebels[0], seeds[0]);
            Hub.Raise(Hub.CivilWar, k, rebels[0], seeds[0], null, "great_rebellion");
            Fam.Invalidate(k);
            return true;
        }

        public static void Push(Kingdom k)
        {
            if (!G.Ok(k)) return;
            int ph = Phase(k);
            if (ph < 0)
            {
                Set(k, 60f);
                SetPhase(k, 0, true);
                return;
            }
            if (ph == 2)
            {
                Set(k, 15f);
                if (!GreatRebellion(k)) SetPhase(k, 3, true);
                return;
            }
            if (ph == 3)
            {
                Set(k, 60f);
                NewDynasty(k, true);
                return;
            }
            Set(k, ph == 0 ? 75f : 45f);
            SetPhase(k, ph + 1, true);
        }

        public static float AttackPct(Kingdom k)
        {
            if (!Active(k)) return 0f;
            float m = Get(k);
            return m >= 50f ? (m - 50f) / 50f * 10f : (m - 50f) / 50f * 25f;
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!Active(k)) return fx;
            float a = (float)Math.Round(AttackPct(k));
            if (a != 0f) fx["attack"] = a;
            return fx;
        }

        public static int Loyalty(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !Active(c.kingdom)) return 0;
            switch (Phase(c.kingdom))
            {
                case 1: return 6;
                case 2: return -(int)G.Clamp((70f - Get(c.kingdom)) / 6f, 0f, 10f);
                case 3: return -12;
            }
            return 0;
        }

        public static int VassalOpinion(Kingdom lord)
        {
            if (!Active(lord)) return 0;
            switch (Phase(lord))
            {
                case 0: return 5;
                case 1: return 15;
                default: return -(int)G.Clamp((70f - Get(lord)) / 2f, 0f, 30f);
            }
        }
    }
}
