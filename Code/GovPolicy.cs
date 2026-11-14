using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class PolDef
    {
        public string Id = "";
        public int Tab;
        public string[] Opts;
        public int[] OptEra;
        public int Era;
        public float Ax, Ay;
        public string Need = "";
        public int Def = 1;
    }

    internal class Bill
    {
        public string Pol = "";
        public int Opt;
        public long By;
        public int Due;
    }

    internal class VoteResult
    {
        public int Yes, No;
        public bool Pass;
        public readonly Dictionary<long, bool> Votes = new Dictionary<long, bool>();
        public readonly Dictionary<long, float> Mood = new Dictionary<long, float>();

        public int Total
        {
            get { return Yes + No; }
        }
    }

    internal static class Parl
    {
        private const string KPol = Gov.P + "pol_";
        private const string KBill = Gov.P + "bill";
        private const string KNext = Gov.P + "bill_next";
        public const string KDecree = Gov.P + "decree";
        public static readonly string[] Tabs = { "domestic", "military", "diplomacy" };
        public static readonly int[] TabEra = { GovEra.Council, GovEra.MilitaryTab, GovEra.DiplomacyTab };

        public static readonly List<PolDef> Defs = new List<PolDef>
        {
            new PolDef { Id = "regime", Tab = 0, Opts = new[] { "monarchy", "republic" }, OptEra = new[] { 0, GovEra.Republic }, Era = GovEra.Republic, Ax = 0f, Ay = 1f, Def = 0 },
            new PolDef { Id = "offices", Tab = 0, Opts = new[] { "hereditary", "kin", "merit" }, OptEra = new[] { 0, 0, GovEra.Bills }, Era = 0, Ax = 0.3f, Ay = 0.8f },
            new PolDef { Id = "lands", Tab = 0, Opts = new[] { "feudal", "limited", "direct" }, OptEra = new[] { 0, 0, 0 }, Era = GovEra.Fiefs, Ax = 1f, Ay = 0f },
            new PolDef { Id = "tax", Tab = 0, Opts = new[] { "light", "shared", "central" }, OptEra = new[] { 0, 0, 0 }, Era = GovEra.Coin, Ax = 1f, Ay = 0f },
            new PolDef { Id = "spending", Tab = 0, Opts = new[] { "works", "balanced", "arms" }, OptEra = new[] { 0, GovEra.Guard, GovEra.Guard }, Era = GovEra.Works, Ax = 0.5f, Ay = 0.3f },
            new PolDef { Id = "culture", Tab = 0, Opts = new[] { "tolerance", "custom", "unity" }, OptEra = new[] { 0, 0, GovEra.Nobles }, Era = 0, Ax = 0.8f, Ay = -0.3f },
            new PolDef { Id = "levy", Tab = 1, Opts = new[] { "retinues", "limited", "balanced", "legions" }, OptEra = new[] { 0, 0, 0, 0 }, Era = GovEra.Guard, Ax = 1f, Ay = 0f },
            new PolDef { Id = "doctrine", Tab = 1, Opts = new[] { "land", "mixed", "sea" }, OptEra = new[] { 0, 0, 0 }, Era = GovEra.MilitaryTab, Ax = 0f, Ay = 0f },
            new PolDef { Id = "foreign", Tab = 2, Opts = new[] { "peace", "realpolitik", "expansion" }, OptEra = new[] { 0, 0, 0 }, Era = GovEra.DiplomacyTab, Ax = 0.3f, Ay = 0.6f },
            new PolDef { Id = "vassal_rule", Tab = 2, Opts = new[] { "autonomy", "contract", "assimilation" }, OptEra = new[] { 0, 0, 0 }, Era = GovEra.DiplomacyTab, Ax = 1f, Ay = 0f, Need = "lord" },
            new PolDef { Id = "tribute", Tab = 2, Opts = new[] { "none", "customary", "heavy", "harsh" }, OptEra = new[] { 0, 0, 0, 0 }, Era = GovEra.DiplomacyTab, Ax = 1f, Ay = 0f, Need = "lord" },
            new PolDef { Id = "fealty", Tab = 2, Opts = new[] { "autonomy", "honor", "loyal" }, OptEra = new[] { 0, 0, 0 }, Era = GovEra.DiplomacyTab, Ax = 1f, Ay = -0.3f, Need = "vassal" }
        };

        public static PolDef Def(string id)
        {
            return Defs.FirstOrDefault(d => d.Id == id);
        }

        public static bool Active(Kingdom k)
        {
            return G.Ok(k) && k.isCiv() && Gov.On("civ_gov_parliament");
        }

        public static bool Voting(Kingdom k)
        {
            return Active(k) && GovEra.Has(k, GovEra.Bills);
        }

        public static bool Available(Kingdom k, PolDef d)
        {
            if (!G.Ok(k) || d == null || !GovEra.Has(k, d.Era) || !GovEra.Has(k, TabEra[d.Tab])) return false;
            if (d.Need == "lord" && Diplomacy.DependentsOf(k).Count == 0) return false;
            if (d.Need == "vassal" && Diplomacy.Overlord(k) == null) return false;
            return true;
        }

        public static bool OptOk(Kingdom k, PolDef d, int i)
        {
            return i >= 0 && i < d.Opts.Length && GovEra.Has(k, d.OptEra[i]);
        }

        private static int Nearest(Kingdom k, PolDef d, int i)
        {
            i = Mathf.Clamp(i, 0, d.Opts.Length - 1);
            if (OptOk(k, d, i)) return i;
            for (int r = 1; r < d.Opts.Length; r++)
            {
                if (OptOk(k, d, i - r)) return i - r;
                if (OptOk(k, d, i + r)) return i + r;
            }
            return 0;
        }

        public static int Get(Kingdom k, string id)
        {
            PolDef d = Def(id);
            if (d == null || !G.Ok(k)) return 1;
            if (id == "regime") return Gov.Republic(k) ? 1 : 0;
            return Nearest(k, d, Store.Int(k.data, KPol + id, d.Def));
        }

        public static string Opt(Kingdom k, string id)
        {
            PolDef d = Def(id);
            return d == null ? "" : d.Opts[Get(k, id)];
        }

        public static bool Is(Kingdom k, string id, string opt)
        {
            if (!Active(k)) return false;
            PolDef d = Def(id);
            return d != null && Available(k, d) && Opt(k, id) == opt;
        }

        public static string OptName(string id, int i)
        {
            PolDef d = Def(id);
            return d == null ? "" : Txt.T("civ_parl_opt_" + id + "_" + d.Opts[Mathf.Clamp(i, 0, d.Opts.Length - 1)]);
        }

        public static string PolName(string id)
        {
            return Txt.T("civ_parl_pol_" + id);
        }

        public static void Set(Kingdom k, string id, int i, string reason)
        {
            PolDef d = Def(id);
            if (d == null || !G.Ok(k)) return;
            i = Nearest(k, d, i);
            int old = Get(k, id);
            if (id == "regime") Rulers.SetRegime(k, i == 1, reason);
            else Store.Put(k.data, KPol + id, i);
            if (old != i && id == "tax") Treasury.Note(k, Txt.F("civ_treas_note_tax", OptName(id, i)));
            Bonus.Dirty(k);
            Fam.Invalidate(k);
        }

        public static float Tension(Kingdom k, long fam, PolDef d)
        {
            int n = d.Opts.Length - 1;
            if (n <= 0) return 0f;
            return Math.Abs(Want(k, fam, d) - Get(k, d.Id)) / (float)n;
        }

        public static int Want(Kingdom k, long fam, PolDef d)
        {
            Vector2 s = Fam.Stance(fam);
            float t = d.Ax * s.x + d.Ay * s.y;
            long royal = Fam.Royal(k);
            bool isRoyal = fam != 0L && fam == royal;
            Clan c = Fam.ClanOf(fam);
            Actor chief = c != null ? c.getChief() : null;
            switch (d.Id)
            {
                case "regime":
                    if (fam < 0L || isRoyal && !Gov.Republic(k)) t = -1f;
                    break;
                case "tax":
                case "levy":
                case "vassal_rule":
                case "tribute":
                case "culture":
                case "lands":
                    if (isRoyal) t += 0.6f;
                    if (fam < 0L) t -= d.Id == "lands" ? 1.2f : 0.5f;
                    break;
                case "doctrine":
                    t = (Profile.Get(k).Coastal ? 0.3f : -0.6f) + s.x * 0.2f;
                    break;
                case "foreign":
                    if (G.Ok(chief) && (chief.hasTrait("bloodlust") || chief.hasTrait("ambitious"))) t += 0.4f;
                    if (G.Ok(chief) && (chief.hasTrait("pacifist") || chief.hasTrait("peaceful"))) t -= 0.4f;
                    break;
                case "spending":
                    if (Profile.Get(k).AtWar) t += 0.4f;
                    break;
            }
            if (d.Id == "levy" && Profile.Get(k).AtWar) t += 0.3f;
            t = Mathf.Clamp(t, -1f, 1f);
            int i = Mathf.RoundToInt((t + 1f) / 2f * (d.Opts.Length - 1));
            return Nearest(k, d, i);
        }

        public static float Mood(Kingdom k, long fam)
        {
            float sum = 0f;
            int n = 0;
            foreach (PolDef d in Defs)
            {
                if (d.Id == "regime" || !Available(k, d)) continue;
                sum += Tension(k, fam, d);
                n++;
            }
            if (n == 0) return 0.6f;
            return Mathf.Clamp01(1f - sum / n * 1.4f);
        }

        public static Bill Current(Kingdom k)
        {
            if (!G.Ok(k)) return null;
            string[] p = Store.Str(k.data, KBill).Split('|');
            if (p.Length < 4) return null;
            var b = new Bill { Pol = p[0] };
            if (Def(b.Pol) == null || !int.TryParse(p[1], out b.Opt) || !long.TryParse(p[2], out b.By) || !int.TryParse(p[3], out b.Due)) return null;
            return b;
        }

        private static void Save(Kingdom k, Bill b)
        {
            Store.Put(k.data, KBill, b == null ? "" : b.Pol + "|" + b.Opt + "|" + b.By + "|" + b.Due);
        }

        public static string BillText(Kingdom k, Bill b)
        {
            if (b == null) return "";
            return PolName(b.Pol) + ": " + OptName(b.Pol, Get(k, b.Pol)) + " → " + OptName(b.Pol, b.Opt);
        }

        private static bool Moves(Kingdom k, long fam, PolDef d, int opt)
        {
            int w = Want(k, fam, d), cur = Get(k, d.Id);
            return Math.Abs(w - opt) < Math.Abs(w - cur);
        }

        public static VoteResult Forecast(Kingdom k, string pol, int opt, long by)
        {
            var r = new VoteResult();
            PolDef d = Def(pol);
            if (d == null) return r;
            var seats = Fam.Seats(k);
            var own = new Dictionary<long, bool>();
            foreach (long f in seats.Keys)
            {
                int w = Want(k, f, d), cur = Get(k, pol);
                int gain = Math.Abs(w - cur) - Math.Abs(w - opt);
                bool yes = gain > 0;
                if (!yes && f != 0L && by != 0L && f != by)
                {
                    float rel = Fam.Rel(k, f, by);
                    if (gain == 0 && rel > 15f || gain == -1 && rel > 45f) yes = true;
                }
                if (f == by) yes = true;
                own[f] = yes;
                r.Mood[f] = Mood(k, f);
            }
            foreach (long f in seats.Keys)
            {
                long lead = Fam.Bloc(k, f);
                bool v = lead != f && own.ContainsKey(lead) ? own[lead] : own[f];
                r.Votes[f] = v;
                if (v) r.Yes += seats[f];
                else r.No += seats[f];
            }
            float need = pol == "regime" ? 0.75f : 0.5f;
            r.Pass = r.Total > 0 && r.Yes > r.Total * need;
            return r;
        }

        public static void Propose(Kingdom k, string pol, int opt, long by, int delay)
        {
            PolDef d = Def(pol);
            if (d == null || !Available(k, d) || !OptOk(k, d, opt)) return;
            if (opt == Get(k, pol))
            {
                Bill cur = Current(k);
                if (cur != null && cur.Pol == pol) Withdraw(k, false);
                return;
            }
            Bill b = Current(k);
            int due = b != null && b.Due > G.Year ? b.Due : G.Year + delay;
            Save(k, new Bill { Pol = pol, Opt = opt, By = by, Due = due });
            Gov.Log("bill", k, G.Name(k), Fam.Name(by), BillText(k, Current(k)));
        }

        public static void Withdraw(Kingdom k, bool log)
        {
            Bill b = Current(k);
            Save(k, null);
            Store.Put(k.data, KNext, G.Year + 1);
            if (log && b != null) Gov.Log("failed", k, G.Name(k), BillText(k, b), Txt.T("civ_parl_withdrawn"));
        }

        public static VoteResult VoteNow(Kingdom k)
        {
            Bill b = Current(k);
            if (b == null) return null;
            PolDef d = Def(b.Pol);
            VoteResult r = Forecast(k, b.Pol, b.Opt, b.By);
            string text = BillText(k, b);
            foreach (long f in r.Votes.Keys)
            {
                if (f == 0L || f == b.By) continue;
                bool natural = Moves(k, f, d, b.Opt);
                if (r.Votes[f] && !natural) Fam.Remember(b.By, f, 5f);
                if (!r.Votes[f] && !r.Pass) Fam.Remember(b.By, f, -6f);
                if (r.Pass) Fam.Remember(f, b.By, natural ? 6f : -8f);
            }
            if (r.Pass) Set(k, b.Pol, b.Opt, "parliament");
            Save(k, null);
            Store.Put(k.data, KNext, G.Year + G.Range(4, 7));
            Gov.Log(r.Pass ? "passed" : "failed", k, G.Name(k), text, r.Yes + " / " + r.Total);
            return r;
        }

        public static void Decree(Kingdom k)
        {
            Bill b = Current(k);
            if (b == null) return;
            PolDef d = Def(b.Pol);
            VoteResult r = Forecast(k, b.Pol, b.Opt, b.By);
            string text = BillText(k, b);
            foreach (var kv in Fam.Table(k).Lands)
            {
                if (r.Votes.ContainsKey(kv.Key) && r.Votes[kv.Key]) continue;
                foreach (City c in kv.Value) if (c != k.capital) Store.Put(c.data, KDecree, G.Year + 8);
                Fam.Remember(kv.Key, Fam.Royal(k), -10f);
            }
            Set(k, b.Pol, b.Opt, "decree");
            Save(k, null);
            Store.Put(k.data, KNext, G.Year + G.Range(4, 7));
            Mandate.Add(k, -3f);
            Gov.Log("decree", k, G.Name(k), text);
        }

        private static void Draft(Kingdom k)
        {
            var seats = Fam.Seats(k);
            if (seats.Count == 0) return;
            var ids = seats.Keys.ToList();
            var w = ids.Select(f => seats[f] * (1.25f - Mood(k, f))).ToList();
            long by = ids[G.Weighted(w)];
            PolDef best = null;
            float bt = 0.01f;
            foreach (PolDef d in Defs)
            {
                if (!Available(k, d)) continue;
                float t = Tension(k, by, d);
                if (d.Id == "regime") t *= 0.5f;
                if (t > bt)
                {
                    bt = t;
                    best = d;
                }
            }
            if (best == null)
            {
                Store.Put(k.data, KNext, G.Year + 2);
                return;
            }
            int want = Want(k, by, best), cur = Get(k, best.Id);
            int step = best.Id == "regime" ? want : cur + Math.Sign(want - cur);
            if (by == Fam.Royal(k) && Mandate.Phase(k) >= 2 && step > cur && best.Ax > 0.5f)
            {
                Store.Put(k.data, KNext, G.Year + 2);
                return;
            }
            Save(k, new Bill { Pol = best.Id, Opt = step, By = by, Due = G.Year + G.Range(2, 4) });
            Gov.Log("bill", k, G.Name(k), Fam.Name(by), BillText(k, Current(k)));
        }

        private static void RulerDecides(Kingdom k)
        {
            long royal = Fam.Royal(k);
            if (royal == 0L) return;
            PolDef best = null;
            float bt = 0.01f;
            foreach (PolDef d in Defs)
            {
                if (d.Id == "regime" || !Available(k, d)) continue;
                float t = Tension(k, royal, d);
                if (t > bt)
                {
                    bt = t;
                    best = d;
                }
            }
            if (best == null) return;
            int cur = Get(k, best.Id);
            int step = cur + Math.Sign(Want(k, royal, best) - cur);
            Set(k, best.Id, step, "ruler");
            if (GovEra.Has(k, GovEra.Nobles)) Gov.Log("ruler_decides", k, G.Name(k), PolName(best.Id), OptName(best.Id, step));
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            if (!Active(k)) return;
            if (Get(k, "culture") == 2) Assimilate(k, yrs);
            bool auto = !Player.Is(k) || Player.Auto(k, "politics");
            if (!Voting(k))
            {
                if (Current(k) != null) Save(k, null);
                if (auto && G.Chance(0.12f * yrs)) RulerDecides(k);
                return;
            }
            Bill b = Current(k);
            if (b != null && !Available(k, Def(b.Pol)))
            {
                Save(k, null);
                b = null;
            }
            if (b != null)
            {
                if (G.Year >= b.Due) VoteNow(k);
                return;
            }
            if (auto && G.Year >= Store.Int(k.data, KNext, 0)) Draft(k);
        }

        private static void Assimilate(Kingdom k, int yrs)
        {
            if (k.capital == null) return;
            Culture cul = k.capital.getCulture();
            if (cul == null) return;
            foreach (City c in G.Cities(k))
            {
                if (c.getCulture() == cul || !G.Chance(0.05f * yrs)) continue;
                c.setCulture(cul);
            }
        }

        public static string ChamberId(Kingdom k)
        {
            string f = Gov.Form(k);
            string id;
            switch (f)
            {
                case "tribe": id = "elders"; break;
                case "chiefdom": id = "nobles"; break;
                case "military_republic": id = "war_council"; break;
                case "merchant_republic": id = "great_council"; break;
                case "republic": id = GovEra.Has(k, GovEra.MassVote) ? "congress" : "senate"; break;
                case "empire": id = "imperial_diet"; break;
                case "duchy":
                case "dominion":
                case "province":
                case "hereditary_province": id = "provincial_assembly"; break;
                default:
                    if (Get(k, "lands") == 0 && GovEra.Has(k, GovEra.Fiefs)) id = "estates";
                    else if (Get(k, "lands") == 2 && Get(k, "tax") == 2) id = "privy_council";
                    else id = GovEra.Has(k, GovEra.MassVote) ? "parliament" : "royal_diet";
                    break;
            }
            if (GovEra.Tier(k) >= 2 && GovEra.Has(k, GovEra.Future)) id += "_fut";
            return id;
        }

        public static string RegimeId(Kingdom k)
        {
            int era = GovEra.Of(k);
            string f = Gov.Form(k);
            bool rep = Gov.Republic(k);
            string id;
            if (era < GovEra.Nobles) id = "band";
            else if (era < GovEra.Kingdoms) id = "chieftaincy";
            else if (Diplomacy.Overlord(k) != null && Get(k, "fealty") == 0 && GovEra.Has(k, GovEra.DiplomacyTab)) id = rep ? "autonomy_state" : "dominion_vassal";
            else if (f == "military_republic") id = "junta_state";
            else if (Get(k, "lands") == 0 && Get(k, "offices") == 0 && Get(k, "tax") == 0) id = "feudal_patchwork";
            else if (f == "empire" && Get(k, "culture") == 2 && Get(k, "lands") == 2 && Get(k, "tax") >= 1) id = "heavenly_throne";
            else if (Get(k, "lands") == 2 && Get(k, "tax") == 2 && Get(k, "culture") == 2) id = rep ? "unitary_state" : "absolute_crown";
            else if (Get(k, "levy") == 3 && Get(k, "foreign") == 2 && GovEra.Has(k, GovEra.Guard)) id = "war_state";
            else if (f == "merchant_republic" || Get(k, "tax") == 0 && Get(k, "foreign") == 0 && GovEra.Has(k, GovEra.Coin)) id = "trade_oligarchy";
            else if (rep && Get(k, "lands") == 0) id = "confederacy";
            else if (Get(k, "offices") == 2 && Get(k, "tax") == 2) id = "bureaucracy";
            else if (Get(k, "culture") == 0 && Get(k, "lands") == 1) id = "commonwealth";
            else id = rep ? "republic" : "crown";
            if (GovEra.Has(k, GovEra.Future)) id += "_fut";
            return id;
        }

        public static string Regime(Kingdom k)
        {
            return Txt.T("civ_regime_" + RegimeId(k));
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!Active(k)) return fx;
            if (Is(k, "offices", "hereditary")) fx["stability"] = 2f;
            if (Is(k, "offices", "merit")) fx["research"] = 3f;
            if (Is(k, "doctrine", "land"))
            {
                Store.Add(fx, "attack", 5f);
                Store.Add(fx, "naval", -10f);
            }
            if (Is(k, "doctrine", "sea"))
            {
                Store.Add(fx, "attack", -3f);
                Store.Add(fx, "naval", 15f);
            }
            if (Is(k, "foreign", "expansion"))
            {
                Store.Add(fx, "attack", 3f);
                Store.Add(fx, "stability", -2f);
            }
            if (Is(k, "foreign", "peace"))
            {
                Store.Add(fx, "attack", -2f);
                Store.Add(fx, "stability", 2f);
            }
            if (Is(k, "culture", "tolerance")) Store.Add(fx, "trade", 2f);
            if (Is(k, "culture", "unity")) Store.Add(fx, "culture", 3f);
            return fx;
        }

        public static int CityLoyalty(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !Active(c.kingdom) || !GovEra.Has(c.kingdom, GovEra.Nobles)) return 0;
            Kingdom k = c.kingdom;
            int v = 0;
            if (Store.Int(c.data, KDecree, 0) > G.Year) v -= 12;
            if (k.capital != null && c != k.capital && c.getCulture() != k.capital.getCulture())
            {
                if (Is(k, "culture", "tolerance")) v += 3;
                if (Is(k, "culture", "unity")) v -= 4;
            }
            if (c == k.capital) return v;
            long fam = Fam.Holder(c, k);
            if (fam == 0L || fam == Fam.Royal(k)) return v;
            v += Mathf.Clamp(Mathf.RoundToInt((Mood(k, fam) - 0.6f) * 20f), -10, 6);
            if (Fam.Stance(fam).x < 0f && Get(k, "tax") == 2 && Get(k, "levy") == 3 && GovEra.Has(k, GovEra.Guard)) v -= 5;
            return v;
        }

        public static float Support(Kingdom k, string group, string id)
        {
            if (!G.Ok(k)) return 1f;
            if (group == "gov" && Voting(k))
            {
                bool rep = Coherence.Is(id, "republic") || Coherence.Is(id, "democratic") && !Coherence.Is(id, "monarchy");
                if (rep == Gov.Republic(k)) return 0.7f;
                VoteResult r = Forecast(k, "regime", rep ? 1 : 0, Fam.Royal(k));
                return r.Total > 0 ? (float)r.Yes / r.Total * 0.67f : 0.5f;
            }
            return G.Clamp(0.5f + (Politics.Stability(k) - 0.5f) * 0.4f, 0f, 1f);
        }
    }
}
