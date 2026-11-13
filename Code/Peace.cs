using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Peace
    {
        public const string KDemil = "civ_demil_until";

        private static readonly HashSet<long> _busy = new HashSet<long>();

        public static void Resolve(global::War w, WarWinner winner)
        {
            if (w == null || w.data == null || !_busy.Add(w.data.id)) return;
            try
            {
                Run(w, winner);
            }
            finally
            {
                _busy.Remove(w.data.id);
            }
        }

        private static void Run(global::War w, WarWinner winner)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d)) return;
            string goal = WarGoals.Get(w);
            Kingdom win = null, lose = null;
            if (winner == WarWinner.Attackers)
            {
                win = a;
                lose = d;
            }
            else if (winner == WarWinner.Defenders)
            {
                win = d;
                lose = a;
            }
            else
            {
                float sa = (w.getDeadDefenders() + 1f) / (w.getDeadAttackers() + 1f) * (float)Math.Sqrt(Profile.Get(a).Strength / Math.Max(1f, Profile.Get(d).Strength));
                if (sa > 1.6f)
                {
                    win = a;
                    lose = d;
                }
                else if (sa < 0.6f)
                {
                    win = d;
                    lose = a;
                }
            }
            if (win == null)
            {
                Diplomacy.Sign(a, d, Profile.Get(a).Era >= 12 ? "armistice" : "truce", 12);
                Hub.Raise(Hub.WarEnded, a, d, null, null, "draw", goal);
                Hub.Raise(Hub.PeaceTerms, a, d, d.capital, null, "white_peace", goal);
                return;
            }
            Hub.Raise(Hub.WarEnded, win, lose, null, null, winner.ToString(), goal);
            Apply(w, win, lose, goal);
        }

        public static void Apply(global::War w, Kingdom win, Kingdom lose, string goal)
        {
            KProfile pw = Profile.Get(win), pl = Profile.Get(lose);
            int era = pw.Era;
            float dom = (lose == w.getMainDefender() ? (w.getDeadDefenders() + 1f) / (w.getDeadAttackers() + 1f) : (w.getDeadAttackers() + 1f) / (w.getDeadDefenders() + 1f));
            dom *= (float)Math.Sqrt(pw.Strength / Math.Max(1f, pl.Strength));
            var terms = new List<string>();
            if (goal == "independence" && Diplomacy.Overlord(lose) == win) terms.Add("dependency_kept");
            else if (goal == "independence" && Diplomacy.Overlord(win) == lose)
            {
                Diplomacy.Release(win, "war");
                terms.Add("independence");
            }
            else if (goal == "liberation" && Diplomacy.Overlord(lose) != null)
            {
                Diplomacy.Release(lose, "liberation");
                terms.Add("liberation");
            }
            else
            {
                bool imperial = pw.Fam.Contains("imperial") || pw.Fam.Contains("authoritarian") || pw.Fam.Contains("monarchy") && era <= 9;
                if (dom > 5f && pl.Cities <= 3 && (era <= 9 || imperial) && goal != "raid")
                {
                    foreach (City c in G.Cities(lose).ToList()) c.joinAnotherKingdom(win, true);
                    terms.Add("annexation");
                }
                else if (dom > 2.5f && (goal == "subjugation" || goal == "colonial" || goal == "succession" || goal == "holy" && era <= 8 || goal == "ideology" && pw.Fam.Contains("authoritarian")) && Diplomacy.DepOf(win) == null)
                {
                    Diplomacy.MakeDependent(lose, win, "peace");
                    terms.Add(Diplomacy.KindFor(win, lose));
                }
                else if (era <= 5 || goal == "tribute" || goal == "raid")
                {
                    Diplomacy.Sign(lose, win, "tribute", goal == "raid" ? 10 : 25);
                    terms.Add("tribute");
                    if (era >= 5) terms.Add("hostages");
                    if (era >= 5) Diplomacy.Sign(lose, win, "hostages", 20);
                }
                else
                {
                    if (era >= 10 && lose.capital != null && win.capital != null)
                    {
                        int gold = (int)(Economy.Amount(lose.capital, "gold") * Math.Min(0.5f, 0.15f * dom));
                        if (gold > 0 && Economy.Take(lose.capital, "gold", gold))
                        {
                            Economy.Give(win.capital, "gold", gold / 2);
                            terms.Add("reparations");
                        }
                    }
                    if (era >= 10 && dom > 2f)
                    {
                        Store.Put(lose.data, KDemil, G.Year + 20);
                        terms.Add("demilitarization");
                    }
                    if (era >= 12 && dom > 3f && pw.Fam.Contains("authoritarian") && Diplomacy.DepOf(win) == null)
                    {
                        Diplomacy.MakeDependent(lose, win, "puppet");
                        terms.Add("puppet");
                    }
                }
            }
            Diplomacy.Sign(win, lose, era >= 12 ? "armistice" : "truce", era <= 3 ? 8 : 15);
            if (terms.Count == 0) terms.Add("truce");
            Hub.Raise(Hub.PeaceTerms, win, lose, lose.capital, null, string.Join(",", terms), goal, dom);
        }

        public static bool Demilitarized(Kingdom k)
        {
            return G.Ok(k) && Store.Int(k.data, KDemil, 0) > G.Year;
        }
    }
}
