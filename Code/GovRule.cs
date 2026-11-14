using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal class Candidate
    {
        public Actor A;
        public long Fam;
        public int Votes;
    }

    internal static class Rulers
    {
        private const string KNextVote = Gov.P + "vote_y";
        private const string KElected = Gov.P + "elected";
        private const string KLastVote = Gov.P + "vote_last";

        public static bool Elects(Kingdom k)
        {
            return G.Ok(k) && Gov.On("civ_gov_elections") && Gov.Republic(k) && GovEra.Has(k, GovEra.Republic) && Diplomacy.Overlord(k) == null;
        }

        public static int Term(Kingdom k)
        {
            return GovEra.Has(k, GovEra.MassVote) ? 8 : 12;
        }

        public static int NextVote(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KNextVote, G.Year + Term(k)) : 0;
        }

        public static string LastVote(Kingdom k)
        {
            return G.Ok(k) ? Store.Str(k.data, KLastVote) : "";
        }

        public static void Crown(Kingdom k, Actor a)
        {
            if (!G.Ok(k) || !G.Ok(a) || k.king == a) return;
            if (G.Ok(k.king)) k.kingLeftEvent();
            else k.removeKing();
            if (a.city != null && a.isCityLeader()) a.city.removeLeader();
            if (a.kingdom != k) a.setKingdom(k);
            k.setKing(a);
            Fam.Invalidate(k);
            Profile.Invalidate(k);
        }

        public static float Merit(Actor a)
        {
            if (!G.Ok(a)) return 0f;
            return a.renown * 0.05f + a.level * 3f + a.stewardship * 2f + a.intelligence + a.diplomacy;
        }

        public static List<Candidate> Candidates(Kingdom k)
        {
            var l = new List<Candidate>();
            if (!G.Ok(k)) return l;
            if (G.Ok(k.king)) l.Add(new Candidate { A = k.king, Fam = Gov.ClanId(k.king) });
            var used = new HashSet<long>(l.Select(c => c.Fam));
            foreach (City c in G.Cities(k).Where(x => G.Ok(x.leader)).OrderByDescending(x => Merit(x.leader)))
            {
                if (l.Count >= 3) break;
                long f = Gov.ClanId(c.leader);
                if (f != 0L && used.Contains(f)) continue;
                used.Add(f);
                l.Add(new Candidate { A = c.leader, Fam = f });
            }
            Count(k, l);
            return l;
        }

        private static float Closeness(Vector2 s, Candidate c)
        {
            return -Vector2.Distance(s, Fam.Stance(c.Fam));
        }

        public static Dictionary<long, Candidate> Choices(Kingdom k, List<Candidate> l)
        {
            var d = new Dictionary<long, Candidate>();
            if (l.Count == 0) return d;
            foreach (long f in Fam.Seats(k).Keys)
            {
                Candidate own = l.FirstOrDefault(c => c.Fam == f && f != 0L);
                Vector2 s = Fam.Stance(f);
                d[f] = own ?? l.OrderByDescending(c => Closeness(s, c)).First();
            }
            return d;
        }

        private static void Count(Kingdom k, List<Candidate> l)
        {
            if (l.Count == 0) return;
            if (GovEra.Has(k, GovEra.MassVote))
            {
                foreach (City c in G.Cities(k))
                {
                    long f = Fam.Holder(c, k);
                    Vector2 s = Fam.Stance(f);
                    Candidate pick = l.OrderByDescending(x => Closeness(s, x) + (x.A == k.king && c.getLoyalty(false) < 0 ? -0.6f : 0f)).First();
                    pick.Votes += Math.Max(1, c.getPopulationPeople());
                }
                return;
            }
            var seats = Fam.Seats(k);
            foreach (var kv in Choices(k, l)) kv.Value.Votes += seats[kv.Key];
        }

        public static Candidate Winner(List<Candidate> l)
        {
            return l.OrderByDescending(c => c.Votes).ThenByDescending(c => Merit(c.A)).FirstOrDefault();
        }

        public static bool Hold(Kingdom k)
        {
            List<Candidate> l = Candidates(k);
            Candidate w = Winner(l);
            Store.Put(k.data, KNextVote, G.Year + Term(k));
            if (w == null) return false;
            Crown(k, w.A);
            Store.Put(k.data, KElected, G.Id(w.A));
            int total = l.Sum(c => c.Votes);
            Store.Put(k.data, KLastVote, G.Year + "|" + w.A.getName() + "|" + w.Votes + "|" + total);
            Gov.Log("election", k, G.Name(k), w.A.getName(), Fam.Name(w.Fam));
            return true;
        }

        public static void Appoint(Kingdom k, Actor a)
        {
            if (!G.Ok(a)) return;
            Crown(k, a);
            Store.Put(k.data, KElected, G.Id(a));
            Store.Put(k.data, KNextVote, G.Year + Term(k));
        }

        public static List<Actor> Succession(Kingdom k)
        {
            var l = new List<Actor>();
            if (!G.Ok(k)) return l;
            long royal = Fam.Royal(k);
            Clan c = Gov.ClanById(royal);
            if (c == null) return l;
            var kids = G.Ok(k.king) ? new HashSet<Actor>(k.king.getChildren(true)) : new HashSet<Actor>();
            foreach (Actor a in c.units.Where(x => G.Ok(x) && x != k.king && x.kingdom == k).OrderByDescending(x => kids.Contains(x)).ThenByDescending(x => x.isAdult()).ThenByDescending(x => x.getAge()))
            {
                l.Add(a);
                if (l.Count >= 6) break;
            }
            foreach (Kingdom v in Diplomacy.DependentsOf(k))
            {
                if (l.Count >= 8) break;
                if (G.Ok(v.king) && Gov.ClanId(v.king) == royal) l.Add(v.king);
            }
            return l;
        }

        public static Actor BestGeneral(Kingdom k)
        {
            Actor best = null;
            float bs = -1f;
            foreach (Actor a in k.getUnits())
            {
                if (!G.Ok(a) || !a.isAdult() || a.isKing()) continue;
                if (!a.is_army_captain && !a.isWarrior()) continue;
                float s = a.data.kills * 3f + a.renown * 0.1f + (a.is_army_captain ? 30f : 0f) + a.warfare;
                if (s > bs)
                {
                    bs = s;
                    best = a;
                }
            }
            return best;
        }

        public static void SetRegime(Kingdom k, bool republic, string reason)
        {
            if (!G.Ok(k) || Gov.Republic(k) == republic) return;
            Store.Put(k.data, Gov.KForm, republic ? "r" : "m");
            if (!republic) Store.Put(k.data, Gov.KMil, 0);
            string fam = republic ? "republic" : "monarchy";
            PolicyDef best = Policies.Options(k, "gov").Where(p => Coherence.Is(p.Id, fam) && Policies.Unlocked(k, p)).OrderByDescending(p => Coherence.EraFit(p, GovEra.Of(k))).FirstOrDefault();
            if (best != null && best.Id != Policies.Id(k, "gov")) Policies.Set(k, "gov", best.Id, "parliament");
            if (republic)
            {
                Store.Put(k.data, KNextVote, G.Year);
                Gov.Log("republic", k, G.Name(k), Gov.FormName(k));
            }
            else
            {
                Gov.Log("monarchy", k, G.Name(k), Gov.RulerName(k));
                Mandate.NewDynasty(k, false);
            }
            Fam.Invalidate(k);
            Bonus.Dirty(k);
        }

        public static bool ProclaimEmpire(Kingdom k)
        {
            if (!G.Ok(k) || !G.Ok(k.king)) return false;
            SetRegime(k, false, "empire");
            Store.Put(k.data, Gov.KEmpire, 1);
            Mandate.NewDynasty(k, false);
            Gov.Log("empire", k, G.Name(k), k.king.getName(), Gov.FormName(k));
            return true;
        }

        public static bool Coup(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            Actor gen = BestGeneral(k);
            if (gen == null) return false;
            Crown(k, gen);
            if (gen.hasTrait("ambitious") || !GovEra.Has(k, GovEra.Junta))
            {
                Store.Put(k.data, Gov.KForm, "m");
                Store.Put(k.data, Gov.KMil, 0);
                Mandate.NewDynasty(k, true);
                Gov.Log("coup", k, G.Name(k), gen.getName());
            }
            else
            {
                Store.Put(k.data, Gov.KForm, "r");
                Store.Put(k.data, Gov.KMil, 1);
                Store.Put(k.data, KNextVote, G.Year + Term(k) * 2);
                PolicyDef junta = Db.Policy("military_junta");
                if (junta != null && Policies.Unlocked(k, junta)) Policies.Set(k, "gov", "military_junta", "coup");
                Mandate.NewDynasty(k, true);
                Gov.Log("junta", k, G.Name(k), gen.getName());
            }
            Hub.Raise(Hub.Coup, k, null, k.capital, gen, "general");
            Bonus.Dirty(k);
            return true;
        }

        public static void Appointments(Kingdom k)
        {
            if (!Parl.Active(k) || !GovEra.Has(k, GovEra.Nobles)) return;
            int opt = Parl.Get(k, "offices");
            long royal = Fam.Royal(k);
            foreach (City c in G.Cities(k))
            {
                if (c == k.capital || c.hasLeader()) continue;
                Actor pick = null;
                IEnumerable<Actor> pool = c.units.Where(a => G.Ok(a) && a.isAdult() && !a.isKing() && !a.isCityLeader());
                if (opt == 0)
                {
                    long prev;
                    long.TryParse(Store.Str(c.data, Gov.P + "prev_clan"), out prev);
                    pick = pool.Where(a => prev != 0L && Gov.ClanId(a) == prev).OrderByDescending(Merit).FirstOrDefault();
                }
                else if (opt == 1) pick = pool.Where(a => royal != 0L && Gov.ClanId(a) == royal).OrderByDescending(Merit).FirstOrDefault();
                else pick = pool.OrderByDescending(Merit).FirstOrDefault();
                if (pick != null) c.setLeader(pick, true);
            }
            foreach (City c in G.Cities(k)) if (G.Ok(c.leader)) Store.Put(c.data, Gov.P + "prev_clan", Gov.ClanId(c.leader).ToString());
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            Appointments(k);
            if (Elects(k))
            {
                bool vacant = !G.Ok(k.king) || Store.Str(k.data, KElected) != G.Id(k.king);
                if (G.Year >= NextVote(k) || vacant && Store.Int(k.data, Gov.KMil, 0) == 0) Hold(k);
            }
            if (Player.Is(k) && !Player.Auto(k, "politics")) return;
            int era = GovEra.Of(k);
            int ph = Mandate.Phase(k);
            if (Gov.Republic(k) && era >= GovEra.Kingdoms && Diplomacy.Overlord(k) == null && ph == 1 && Gov.AllCities(k) >= 6 && G.Ok(k.king))
            {
                Actor r = k.king;
                if (!r.hasTrait("content") && !r.hasTrait("honest") && !r.hasTrait("pacifist") && !r.hasTrait("peaceful"))
                {
                    float ch = 0.015f * yrs * (r.hasTrait("ambitious") || r.hasTrait("bloodlust") || r.hasTrait("evil") ? 3f : 1f);
                    if (G.Chance(ch)) ProclaimEmpire(k);
                }
            }
            if (ph == 3 && era >= GovEra.Nobles && G.Chance(0.1f * yrs * (k.getCulture() != null && k.getCulture().hasTrait("warriors_ascension") ? 2f : 1f))) Coup(k);
        }

        public static bool Extinct(Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv() || G.Cities(k).Count < 2 || !Gov.On("civ_gov_elections")) return false;
            if (!GovEra.Has(k, GovEra.Republic)) return false;
            bool rep = Gov.Republic(k);
            if (!rep)
            {
                float ch = 0.4f;
                Culture cu = k.getCulture();
                if (cu != null && (cu.hasTrait("diplomatic_ascension") || cu.hasTrait("golden_rule"))) ch = 0.8f;
                if (!G.Chance(ch)) return false;
                Store.Put(k.data, Gov.KForm, "r");
                PolicyDef best = Policies.Options(k, "gov").Where(p => Coherence.Is(p.Id, "republic") && Policies.Unlocked(k, p)).OrderByDescending(p => Coherence.EraFit(p, GovEra.Of(k))).FirstOrDefault();
                if (best != null) Policies.Set(k, "gov", best.Id, "revolution");
                Gov.Log("republic", k, G.Name(k), Gov.FormName(k));
            }
            Store.Put(k.data, KNextVote, G.Year);
            return true;
        }

        [HarmonyPatch(typeof(ai.behaviours.KingdomBehCheckKing), "checkKingdomChaos")]
        private static class NoSplit
        {
            private static bool Prefix(Kingdom pMainKingdom)
            {
                bool keep = false;
                CivMod.Safe("gov-extinct", () => keep = Extinct(pMainKingdom));
                return !keep;
            }
        }
    }
}
