using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Treasury
    {
        private const string KGold = Gov.P + "gold";
        private const string KDelta = Gov.P + "gold_delta";
        private const string KOwed = Gov.P + "owed";
        private const string KUnpaidY = Gov.P + "unpaid_y";
        private const string KFlow = Gov.P + "flow";
        private const string KFlowCur = Gov.P + "flow_cur";
        private const string KLedger = Gov.P + "ledger";
        private const string KTier = Gov.P + "arms";
        private const string KLast = Gov.P + "tr_last";
        private const string KCityUnpaid = Gov.P + "tr_unpaid";
        private const string KWorks = Gov.P + "works";
        public static readonly string[] WorkTypes = { "market", "temple", "barracks", "statue" };
        public static readonly string[] Outs = { "court", "guard", "local", "arms", "works", "rep_out", "tribute_out" };

        public static void Register()
        {
        }

        public static bool Coin(Kingdom k)
        {
            return G.Ok(k) && k.isCiv() && Gov.On("civ_gov_treasury") && GovEra.Has(k, GovEra.Coin);
        }

        public static float Gold(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KGold, 0f) : 0f;
        }

        public static void SetGold(Kingdom k, float v)
        {
            if (G.Ok(k)) Store.Put(k.data, KGold, Math.Max(0f, v));
        }

        public static float Delta(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KDelta, 0f) : 0f;
        }

        public static float Owed(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KOwed, 0f) : 0f;
        }

        public static int UnpaidYears(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KUnpaidY, 0) : 0;
        }

        public static int Tier(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KTier, 0) : 0;
        }

        public static Dictionary<string, float> Flows(Kingdom k)
        {
            return G.Ok(k) ? Store.Map(k.data, KFlow) : new Dictionary<string, float>();
        }

        private static void Flow(Kingdom k, string key, float v)
        {
            Dictionary<string, float> m = Store.Map(k.data, KFlowCur);
            Store.Add(m, key, v);
            Store.Map(k.data, KFlowCur, m);
        }

        public static void Receive(Kingdom k, float v, string source)
        {
            if (!G.Ok(k) || v <= 0f) return;
            if (!Coin(k))
            {
                if (k.capital != null) Economy.Give(k.capital, "gold", (int)v);
                return;
            }
            SetGold(k, Gold(k) + v);
            Flow(k, "in_" + source, v);
        }

        public static void PaidOut(Kingdom k, float v, string what)
        {
            if (G.Ok(k) && v > 0f && Coin(k)) Flow(k, "out_" + what, v);
        }

        public static float Seize(Kingdom k, float share)
        {
            if (!G.Ok(k)) return 0f;
            float got = 0f;
            if (Coin(k))
            {
                float t = Gold(k) * share;
                SetGold(k, Gold(k) - t);
                Flow(k, "out_rep_out", t);
                got += t;
            }
            foreach (City c in G.Cities(k)) got += Economy.TakeUpTo(c, "gold", (int)(Economy.Amount(c, "gold") * share));
            return got;
        }

        public static void Note(Kingdom k, string text)
        {
            if (!G.Ok(k) || !GovEra.Has(k, GovEra.Ledger) || !Gov.On("civ_gov_treasury")) return;
            List<string> l = Store.Str(k.data, KLedger).Split('\n').Where(x => x.Length > 0).ToList();
            l.Insert(0, G.Year + "\t" + text.Replace("\n", " ").Replace("\t", " "));
            if (l.Count > 30) l.RemoveRange(30, l.Count - 30);
            Store.Put(k.data, KLedger, string.Join("\n", l.ToArray()));
        }

        public static List<KeyValuePair<int, string>> Ledger(Kingdom k)
        {
            var l = new List<KeyValuePair<int, string>>();
            if (!G.Ok(k)) return l;
            foreach (string e in Store.Str(k.data, KLedger).Split('\n'))
            {
                int t = e.IndexOf('\t');
                int y;
                if (t <= 0 || !int.TryParse(e.Substring(0, t), out y)) continue;
                l.Add(new KeyValuePair<int, string>(y, e.Substring(t + 1)));
            }
            return l;
        }

        public static float TaxShare(Kingdom k)
        {
            if (!Parl.Active(k)) return 0.4f;
            switch (Parl.Get(k, "tax"))
            {
                case 0: return 0.25f;
                case 2: return 0.75f;
            }
            return 0.5f;
        }

        public static float LevyShare(Kingdom k)
        {
            if (!GovEra.Has(k, GovEra.Guard)) return 0f;
            if (!Parl.Active(k)) return 0.25f;
            return Parl.Get(k, "levy") * 0.25f;
        }

        public static int GuardSize(Kingdom k)
        {
            if (!G.Ok(k) || !GovEra.Has(k, GovEra.Guard)) return 0;
            float n = 0f, share = LevyShare(k);
            foreach (City c in G.Cities(k)) n += c == k.capital ? c.countWarriors() : c.countWarriors() * share;
            return (int)n;
        }

        public static Dictionary<string, float> Works(City c)
        {
            return G.Ok(c) ? Store.Map(c.data, KWorks) : new Dictionary<string, float>();
        }

        public static int WorkCount(City c, string type)
        {
            return (int)Store.Get(Works(c), type);
        }

        private static int Built(City c, string part)
        {
            int n = 0;
            if (c.buildings == null) return 0;
            foreach (Building b in c.buildings)
            {
                if (b != null && b.asset != null && b.asset.id != null && b.asset.id.Contains(part)) n++;
            }
            return n;
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            if (!GovEra.Has(k, GovEra.Works)) return;
            if (!Coin(k))
            {
                Monument(k, yrs);
                return;
            }
            float before = Gold(k);
            float share = TaxShare(k), levy = LevyShare(k);
            float income = 0f, local = 0f;
            var taxes = new Dictionary<string, float>();
            foreach (City c in G.Cities(k))
            {
                int g = Economy.Amount(c, "gold");
                int last = Store.Int(c.data, KLast, g);
                float gain = Math.Max(0, g - last);
                int markets = Built(c, "market") + WorkCount(c, "market");
                float mult = 1f + Math.Min(0.6f, markets * 0.15f);
                int amt = (int)((gain * share + c.getPopulationPeople() * 0.02f * share * yrs) * mult);
                amt = Economy.TakeUpTo(c, "gold", amt);
                income += amt;
                if (amt > 0) taxes["c" + c.data.id] = amt;
                float troops = c == k.capital ? 0f : c.countWarriors() * (1f - levy);
                int pay = (int)Math.Ceiling(troops * 0.5f * yrs);
                int paid = Economy.TakeUpTo(c, "gold", pay);
                local += paid;
                Store.Put(c.data, KCityUnpaid, paid < pay ? Store.Int(c.data, KCityUnpaid, 0) + yrs : 0);
                if (paid < pay && GovEra.Has(k, GovEra.Disband)) Disband(k, c, Math.Max(1, (int)(troops * 0.25f)));
                Store.Put(c.data, KLast, Economy.Amount(c, "gold"));
            }
            float gold = Gold(k) + income;
            Dictionary<string, float> flow = Store.Map(k.data, KFlowCur);
            foreach (var kv in taxes) flow["in_" + kv.Key] = kv.Value;
            float court = income * 0.1f;
            gold -= court;
            flow["out_court"] = court;
            flow["out_local"] = local;
            if (GovEra.Has(k, GovEra.Guard))
            {
                int size = GuardSize(k);
                float pay = size * 0.5f * yrs;
                if (gold >= pay + Owed(k))
                {
                    gold -= pay + Owed(k);
                    flow["out_guard"] = pay + Owed(k);
                    if (Owed(k) > 0f) Note(k, Txt.T("civ_treas_note_paid"));
                    Store.Put(k.data, KOwed, 0f);
                    Store.Put(k.data, KUnpaidY, 0);
                }
                else
                {
                    float paid = Math.Max(0f, gold);
                    flow["out_guard"] = paid;
                    gold -= paid;
                    float owed = Owed(k) + pay - paid;
                    Store.Put(k.data, KOwed, owed);
                    Store.Put(k.data, KUnpaidY, UnpaidYears(k) + yrs);
                    if (UnpaidYears(k) <= yrs) Note(k, Txt.F("civ_treas_note_unpaid", (int)owed));
                    Gov.Log("unpaid", k, G.Name(k), ((int)owed).ToString());
                    if (GovEra.Has(k, GovEra.Disband) && k.capital != null) Disband(k, null, Math.Max(1, size / 4));
                }
                float reserve = pay * (p.AtWar ? 4f : 2f);
                float surplus = gold - reserve;
                float worksShare = Parl.Active(k) ? (Parl.Get(k, "spending") == 0 ? 1f : Parl.Get(k, "spending") == 1 ? 0.5f : 0f) : 0.5f;
                if (surplus > 0f)
                {
                    float spent = FundWorks(k, surplus * worksShare, flow);
                    gold -= spent;
                    surplus -= spent;
                    gold -= Arm(k, surplus * (1f - worksShare), flow);
                }
            }
            else gold -= FundWorks(k, gold * 0.5f, flow);
            SetGold(k, gold);
            Store.Put(k.data, KDelta, gold - before);
            flow["saved"] = gold - before;
            Store.Map(k.data, KFlow, flow);
            Store.Put(k.data, KFlowCur, "");
            Bonus.Dirty(k);
        }

        private static void Monument(Kingdom k, int yrs)
        {
            City cap = k.capital;
            if (cap == null || WorkCount(cap, "statue") >= 3 || Economy.Amount(cap, "gold") < 150 || !G.Chance(0.15f * yrs)) return;
            if (!Economy.Take(cap, "gold", 60)) return;
            AddWork(k, cap, "statue");
        }

        private static void AddWork(Kingdom k, City c, string type)
        {
            Dictionary<string, float> w = Works(c);
            Store.Add(w, type, 1f);
            Store.Map(c.data, KWorks, w);
            Note(k, Txt.F("civ_treas_note_works", Txt.T("civ_treas_work_" + type), G.Name(c)));
            long fam = Fam.Holder(c, k);
            if (fam != 0L && c != k.capital) Fam.Remember(fam, Fam.Royal(k), 10f);
            if (G.Chance(0.3f)) Gov.Log("works", k, G.Name(k), Txt.T("civ_treas_work_" + type), G.Name(c), null, c);
        }

        private static float FundWorks(Kingdom k, float budget, Dictionary<string, float> flow)
        {
            float cost = 40f + GovEra.Of(k) * 5f;
            if (budget < cost) return 0f;
            List<City> cities = G.Cities(k);
            if (cities.Count == 0) return 0f;
            City pick = null;
            string type = null;
            bool threat = Profile.Get(k).AtWar;
            if (threat)
            {
                pick = cities.Where(c => WorkCount(c, "barracks") < 2 && c.neighbours_kingdoms != null && c.neighbours_kingdoms.Any(o => World.world.wars.isInWarWith(k, o))).FirstOrDefault();
                type = "barracks";
            }
            if (pick == null)
            {
                pick = cities.Where(c => WorkCount(c, "temple") < 2 && c.getLoyalty(false) < 10).OrderBy(c => c.getLoyalty(false)).FirstOrDefault();
                type = "temple";
            }
            if (pick == null)
            {
                pick = cities.Where(c => WorkCount(c, "market") < 2).OrderByDescending(c => c.getPopulationPeople()).FirstOrDefault();
                type = "market";
            }
            if (pick == null && k.capital != null && WorkCount(k.capital, "statue") < 3)
            {
                pick = k.capital;
                type = "statue";
            }
            if (pick == null) return 0f;
            AddWork(k, pick, type);
            Store.Add(flow, "out_works", cost);
            return cost;
        }

        public static int MaxTier(Kingdom k)
        {
            return GovEra.Has(k, GovEra.Guard) ? Math.Min(6, 1 + (GovEra.Of(k) - GovEra.Guard) / 3) : 0;
        }

        private static float Arm(Kingdom k, float budget, Dictionary<string, float> flow)
        {
            int t = Tier(k);
            if (t >= MaxTier(k)) return 0f;
            float cost = 100f * (t + 1);
            if (budget < cost) return 0f;
            Store.Put(k.data, KTier, t + 1);
            Store.Add(flow, "out_arms", cost);
            Note(k, Txt.F("civ_treas_note_arms", t + 1));
            return cost;
        }

        private static void Disband(Kingdom k, City c, int n)
        {
            foreach (Actor a in (c != null ? c.units : k.getUnits()).ToList())
            {
                if (n <= 0) break;
                if (!G.Ok(a) || !a.isWarrior() || a.is_army_captain) continue;
                a.setProfession(UnitProfession.Unit, true);
                n--;
            }
        }

        public static bool LocalUnpaid(City c)
        {
            return G.Ok(c) && Store.Int(c.data, KCityUnpaid, 0) > 0;
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!Coin(k)) return fx;
            int u = UnpaidYears(k);
            if (u > 0) fx["attack"] = -Math.Min(25f, u * 8f);
            int t = Tier(k);
            if (t > 0)
            {
                Store.Add(fx, "attack", t * 2f);
                Store.Add(fx, "defense", t * 1.5f);
            }
            int barracks = 0, statues = 0;
            foreach (City c in G.Cities(k))
            {
                barracks += WorkCount(c, "barracks");
                statues += WorkCount(c, "statue");
            }
            if (barracks > 0) Store.Add(fx, "attack", Math.Min(6f, barracks));
            if (statues > 0) Store.Add(fx, "stability", Math.Min(4f, statues));
            return fx;
        }

        public static int CityLoyalty(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !Gov.On("civ_gov_treasury")) return 0;
            int v = Math.Min(8, WorkCount(c, "temple") * 4);
            if (LocalUnpaid(c)) v -= 6;
            return v;
        }

        public static void Bankrupt(Kingdom k)
        {
            if (!G.Ok(k)) return;
            SetGold(k, 0f);
            Store.Put(k.data, KOwed, Math.Max(Owed(k), GuardSize(k) * 0.5f + 1f));
            Store.Put(k.data, KUnpaidY, Math.Max(1, UnpaidYears(k)));
            Note(k, Txt.F("civ_treas_note_unpaid", (int)Owed(k)));
            Bonus.Dirty(k);
        }
    }
}
