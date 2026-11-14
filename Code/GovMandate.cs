using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Mandate
    {
        private const string KMandate = "civ_mandate";
        private const string KDyn = "civ_dyn";
        private const string KDynY = "civ_dyn_y";
        private const string KDynN = "civ_dyn_n";
        private const string KRoyal = "civ_royal";
        private const string KOldRoyal = "civ_old_royal";
        private const string KOldRoyalY = "civ_old_royal_y";
        public const string KRebelUntil = "civ_gr_until";
        public const string KRebelOf = "civ_gr_of";

        public static void Register()
        {
            Hub.On(Hub.WarEnded, e =>
            {
                if (e.Id == "draw" || e.Id == "Peace") return;
                if (G.Ok(e.K)) Add(e.K, 4f);
                if (G.Ok(e.O)) Add(e.O, -6f);
            });
        }

        public static float Get(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KMandate, 55f) : 0f;
        }

        public static void Set(Kingdom k, float v)
        {
            if (G.Ok(k)) Store.Put(k.data, KMandate, G.Clamp(v, 0f, 100f));
        }

        public static void Add(Kingdom k, float v)
        {
            Set(k, Get(k) + v);
        }

        public static int Phase(Kingdom k)
        {
            return Statecraft.Phase(k);
        }

        public static int Years(Kingdom k)
        {
            return G.Ok(k) ? G.Year - Store.Int(k.data, KDynY, G.Year) : 0;
        }

        public static int Dynasty(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KDynN, 1) : 1;
        }

        public static bool Rebelling(Kingdom k)
        {
            return G.Ok(k) && Store.Int(k.data, KRebelUntil, 0) > G.Year;
        }

        public static Dictionary<string, float> Components(Kingdom k)
        {
            var c = new Dictionary<string, float>();
            if (!G.Ok(k)) return c;
            KProfile p = Profile.Get(k);
            c["base"] = 35f;
            c["legitimacy"] = (Legitimacy.Get(k) - 0.5f) * 40f;
            c["stability"] = (p.Stability - 0.5f) * 30f;
            float loy = 0f;
            int n = 0;
            foreach (City ct in G.Cities(k))
            {
                loy += ct.getLoyalty(false);
                n++;
            }
            c["loyalty"] = n > 0 ? G.Clamp(loy / n / 3f, -10f, 10f) : 0f;
            float war = 0f;
            foreach (global::War w in World.world.wars.getWars(k))
            {
                if (w == null || w.hasEnded()) continue;
                war += Statecraft.Score(k, w) / 10f;
            }
            c["war"] = G.Clamp(war - Statecraft.Exhaustion(k) / 10f, -15f, 10f);
            c["treasury"] = Gov.On("civ_gov_treasury") ? Treasury.MandateShare(k) : 0f;
            c["parliament"] = Parliament.Active(k) && p.Era >= 3 ? (Parliament.RoyalShare(k) - 0.35f) * 20f : 0f;
            int ph = Phase(k);
            c["age"] = -Math.Min(25f, Years(k) * (ph == 1 ? 0.3f : 0.2f));
            c["renewal"] = ph == 0 ? 12f : 0f;
            c["rebellion"] = Rebelling(k) ? -15f : 0f;
            c["works"] = Gov.On("civ_gov_treasury") ? Math.Min(6f, Treasury.Statues(k) * 1.5f) : 0f;
            return c;
        }

        public static float Target(Kingdom k)
        {
            return G.Clamp(Components(k).Values.Sum(), 0f, 100f);
        }

        public static void Year(Kingdom k, KProfile p, int yrs)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            CheckRoyal(k);
            if (Phase(k) < 0) SetPhase(k, 0, false);
            float m = Get(k), t = Target(k);
            float f = Math.Min(1f, 0.2f * yrs);
            m = m + (t - m) * f;
            Set(k, m);
            int ph = Phase(k), age = Years(k);
            switch (ph)
            {
                case 0:
                    if (age >= 10) SetPhase(k, m >= 55f ? 1 : 2, true);
                    break;
                case 1:
                    if (m < 48f) SetPhase(k, 2, true);
                    break;
                case 2:
                    if (m < 25f) SetPhase(k, 3, true);
                    else if (m >= 66f && age >= 5) SetPhase(k, 1, true);
                    break;
                case 3:
                    Collapse(k, p, m, age, yrs);
                    break;
            }
            Spread(k);
            MaybeRestoration(k, m);
        }

        private static void CheckRoyal(Kingdom k)
        {
            long r = Parliament.RoyalId(k);
            string cur = r == 0L ? "" : r.ToString();
            string old = Store.Str(k.data, KRoyal);
            if (cur == old) return;
            Store.Put(k.data, KRoyal, cur);
            if (old.Length == 0 || cur.Length == 0) return;
            Store.Put(k.data, KOldRoyal, old);
            Store.Put(k.data, KOldRoyalY, G.Year);
            NewDynasty(k);
        }

        public static void NewDynasty(Kingdom k)
        {
            Store.Put(k.data, KDynN, Dynasty(k) + 1);
            Set(k, Math.Max(45f, Get(k)));
            SetPhase(k, 0, false);
            if (Statecraft.Dynastic(k)) Gov.Log("new_dynasty", k, G.Name(k), Gov.ClanName(Parliament.RoyalId(k)), Dynasty(k).ToString());
            Hub.Raise(Hub.Legitimacy, k, null, k.capital, null, "new_dynasty");
        }

        public static void SetPhase(Kingdom k, int ph, bool log)
        {
            Store.Put(k.data, KDyn, ph);
            Store.Put(k.data, KDynY, G.Year);
            Bonus.Dirty(k);
            if (log) Gov.Log("mandate_phase", k, G.Name(k), Txt.T("civ_phase_" + Statecraft.Phases[ph]), ((int)Get(k)).ToString());
        }

        private static void Collapse(Kingdom k, KProfile p, float m, int age, int yrs)
        {
            if (m >= 40f)
            {
                SetPhase(k, 0, true);
                return;
            }
            if (!Rebelling(k) && p.Cities >= 4 && G.Chance(0.25f * yrs))
            {
                GreatRebellion(k);
                return;
            }
            if (G.Chance(0.12f * yrs) && GeneralCoup(k)) return;
            if (age >= 10)
            {
                Store.Put(k.data, KDynN, Dynasty(k) + 1);
                Set(k, 45f);
                SetPhase(k, 0, true);
            }
        }

        public static bool GreatRebellion(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            var pool = G.Cities(k).Where(c => G.Ok(c) && c != k.capital).OrderBy(c => c.getLoyalty(false)).ToList();
            int want = Math.Min(4, Math.Max(1, pool.Count / 4));
            var picked = new List<City>();
            foreach (City c in pool)
            {
                if (picked.Count >= want) break;
                if (picked.Any(x => x.neighbours_cities != null && x.neighbours_cities.Contains(c))) continue;
                picked.Add(c);
            }
            if (picked.Count == 0) return false;
            City head = picked[0];
            Actor lead = Gov.Somebody(head);
            if (lead == null) return false;
            Kingdom nk = head.makeOwnKingdom(lead, true, false);
            if (!G.Ok(nk)) return false;
            Store.Put(nk.data, KRebelOf, k.data.id.ToString());
            for (int i = 1; i < picked.Count; i++) if (G.Ok(picked[i]) && picked[i].kingdom == k) picked[i].joinAnotherKingdom(nk, false, true);
            Store.Put(k.data, KRebelUntil, G.Year + 12);
            if (!World.world.wars.isInWarWith(nk, k))
            {
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
            Legitimacy.Shock(k, -0.1f);
            Gov.Log("great_rebellion", k, G.Name(k), G.Name(nk), picked.Count.ToString(), nk, head);
            Hub.Raise(Hub.CivilWar, k, nk, head, lead, "great_rebellion");
            return true;
        }

        private static void Spread(Kingdom k)
        {
            if (!Rebelling(k)) return;
            string id = k.data.id.ToString();
            foreach (Kingdom nk in G.Kingdoms().ToList())
            {
                if (!G.Ok(nk) || nk == k || Store.Str(nk.data, KRebelOf) != id) continue;
                if (!World.world.wars.isInWarWith(nk, k)) continue;
                foreach (City rc in G.Cities(nk).ToList())
                {
                    if (rc.neighbours_cities == null) continue;
                    City next = rc.neighbours_cities.Where(c => G.Ok(c) && c.kingdom == k && c != k.capital && c.getLoyalty(false) < 10).OrderBy(c => c.getLoyalty(false)).FirstOrDefault();
                    if (next == null || !G.Chance(0.3f)) continue;
                    next.joinAnotherKingdom(nk, false, true);
                    Gov.Log("rebellion_spread", nk, G.Name(next), G.Name(nk), G.Name(k), k, next);
                    return;
                }
            }
        }

        public static Actor BestGeneral(Kingdom k)
        {
            Actor best = null;
            float bs = -1f;
            foreach (Actor a in k.getUnits())
            {
                if (!G.Ok(a) || !a.isAdult() || a.isKing()) continue;
                if (!a.is_army_captain && !a.isWarrior()) continue;
                float s = a.renown + (a.is_army_captain ? 50f : 0f) + a.data.kills * 2f;
                if (s > bs)
                {
                    bs = s;
                    best = a;
                }
            }
            return best;
        }

        public static bool GeneralCoup(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            Actor gen = BestGeneral(k);
            if (gen == null) return false;
            Crown(k, gen);
            PolicyDef junta = Db.Policy("military_junta");
            if (junta != null && Policies.Unlocked(k, junta)) Policies.Set(k, "gov", "military_junta", "coup");
            Set(k, 40f);
            SetPhase(k, 0, false);
            Gov.Log("general_coup", k, G.Name(k), gen.getName(), null, null, gen.city);
            Hub.Raise(Hub.Coup, k, null, k.capital, gen, "general");
            return true;
        }

        public static void Crown(Kingdom k, Actor a)
        {
            Actor old = k.king;
            if (G.Ok(old)) k.kingLeftEvent();
            else k.removeKing();
            if (a.city != null && a.isCityLeader()) a.city.removeLeader();
            k.setKing(a);
            Parliament.Invalidate(k);
        }

        private static void MaybeRestoration(Kingdom k, float m)
        {
            string old = Store.Str(k.data, KOldRoyal);
            if (old.Length == 0) return;
            long id;
            if (!long.TryParse(old, out id)) return;
            Clan c = Gov.ClanById(id);
            if (c == null || G.Year - Store.Int(k.data, KOldRoyalY, G.Year) > 40)
            {
                Store.Put(k.data, KOldRoyal, "");
                return;
            }
            if (m >= 40f || Plots.Has(k, "restoration", id)) return;
            Actor chief = c.getChief();
            if (!G.Ok(chief) || chief.kingdom != k || !chief.isAdult()) return;
            if (G.Chance(0.2f)) Plots.Start(k, "restoration", id, k.data.id, G.Range(2, 4), "lost_crown");
        }

        public static void Restore(Kingdom k, long clan)
        {
            Clan c = Gov.ClanById(clan);
            if (c == null || !G.Ok(k)) return;
            Actor chief = c.getChief();
            if (!G.Ok(chief) || chief.kingdom != k) return;
            if (!G.Chance(G.Clamp(0.75f - Get(k) / 100f, 0.15f, 0.7f))) return;
            Crown(k, chief);
            Store.Put(k.data, KOldRoyal, "");
            Set(k, 55f);
            SetPhase(k, 0, false);
            Gov.Log("restoration", k, G.Name(k), c.data.name, chief.getName());
        }

        public static void Push(Kingdom k)
        {
            if (!G.Ok(k)) return;
            int ph = Phase(k);
            if (ph < 0) ph = 0;
            int next = (ph + 1) % 4;
            Set(k, next == 0 ? 50f : next == 1 ? 70f : next == 2 ? 40f : 15f);
            SetPhase(k, next, true);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!G.Ok(k)) return fx;
            switch (Phase(k))
            {
                case 0:
                    fx["prod"] = 0.03f;
                    fx["loyalty"] = 0.02f;
                    fx["attack"] = 0.03f;
                    break;
                case 1:
                    fx["gold"] = 0.08f;
                    fx["research"] = 0.05f;
                    fx["prod"] = 0.05f;
                    fx["attack"] = 0.05f;
                    break;
                case 2:
                    fx["gold"] = -0.05f;
                    fx["prod"] = -0.03f;
                    fx["attack"] = -0.05f;
                    break;
                case 3:
                    fx["gold"] = -0.12f;
                    fx["defense"] = -0.08f;
                    fx["attack"] = -0.15f;
                    break;
            }
            return fx;
        }

        public static int Loyalty(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !c.kingdom.isCiv() || !Gov.On("civ_gov_cycle")) return 0;
            switch (Phase(c.kingdom))
            {
                case 0: return 5;
                case 1: return 8;
                case 2: return -6;
                case 3: return -18;
            }
            return 0;
        }
    }
}
