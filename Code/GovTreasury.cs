using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal class WorksCount
    {
        public int Year;
        public int Market, Temple, Barracks, Tower, Statue, Windmill;
    }

    internal static class Treasury
    {
        private const string KRes = "civ_tr_res";
        private const string KUnpaid = "civ_tr_unpaid";
        private const string KLog = "civ_tr_log";
        private const string KFlow = "civ_tr_flow";
        public static readonly string[] Sources = { "taxes", "companies", "currency", "tribute" };
        public static readonly string[] Uses = { "soldiers", "court", "works", "armament" };
        private static readonly Dictionary<long, WorksCount> _works = new Dictionary<long, WorksCount>();

        public static float Reserve(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KRes, 0f) : 0f;
        }

        public static void AddReserve(Kingdom k, float v)
        {
            if (G.Ok(k)) Store.Put(k.data, KRes, Math.Max(0f, Reserve(k) + v));
        }

        public static int Unpaid(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KUnpaid, 0) : 0;
        }

        public static float TaxRate(Kingdom k)
        {
            switch (Laws.Get(k, "taxation"))
            {
                case "light_tax": return 0.04f;
                case "flat_tax": return 0.08f;
                case "progressive_tax": return 0.1f;
                case "heavy_tax": return 0.16f;
            }
            return 0.06f;
        }

        public static float Pay(Kingdom k)
        {
            return 0.5f + Gov.Band(k) * 0.25f;
        }

        public static WorksCount Works(Kingdom k)
        {
            WorksCount w;
            if (_works.TryGetValue(k.data.id, out w) && w.Year == G.Year) return w;
            w = new WorksCount { Year = G.Year };
            foreach (City c in G.Cities(k))
            {
                WorksCount cw = Works(c);
                w.Market += cw.Market;
                w.Temple += cw.Temple;
                w.Barracks += cw.Barracks;
                w.Tower += cw.Tower;
                w.Statue += cw.Statue;
                w.Windmill += cw.Windmill;
            }
            _works[k.data.id] = w;
            return w;
        }

        public static WorksCount Works(City c)
        {
            var w = new WorksCount();
            if (!G.Ok(c) || c.buildings == null) return w;
            foreach (Building b in c.buildings)
            {
                if (b == null || b.asset == null || b.asset.id == null) continue;
                string id = b.asset.id;
                if (id.Contains("market")) w.Market++;
                else if (id.Contains("temple")) w.Temple++;
                else if (id.Contains("barracks")) w.Barracks++;
                else if (id.Contains("watch_tower") || id.Contains("watchtower")) w.Tower++;
                else if (id.Contains("statue")) w.Statue++;
                else if (id.Contains("windmill")) w.Windmill++;
            }
            return w;
        }

        public static int Statues(Kingdom k)
        {
            return G.Ok(k) ? Works(k).Statue : 0;
        }

        public static int TempleLoyalty(City c)
        {
            if (!G.Ok(c) || !Gov.On("civ_gov_treasury")) return 0;
            return Math.Min(9, Works(c).Temple * 3);
        }

        public static Dictionary<string, float> Flow(Kingdom k)
        {
            return G.Ok(k) ? Store.Map(k.data, KFlow) : new Dictionary<string, float>();
        }

        public static List<string[]> Ledger(Kingdom k)
        {
            var l = new List<string[]>();
            if (!G.Ok(k)) return l;
            foreach (string e in Store.Str(k.data, KLog).Split(';'))
            {
                string[] p = e.Split('|');
                if (p.Length >= 3) l.Add(p);
            }
            return l;
        }

        private static void Note(Kingdom k, string what, float amount)
        {
            if (Math.Abs(amount) < 1f) return;
            List<string> l = Store.Str(k.data, KLog).Split(';').Where(x => x.Length > 0).ToList();
            l.Insert(0, G.Year + "|" + what + "|" + ((int)amount));
            if (l.Count > 24) l.RemoveRange(24, l.Count - 24);
            Store.Put(k.data, KLog, string.Join(";", l.ToArray()));
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            if (p.Era < 1) return;
            var flow = new Dictionary<string, float>();
            float rate = TaxRate(k);
            float taxes = 0f;
            foreach (City c in G.Cities(k))
            {
                WorksCount cw = Works(c);
                float r = rate * (1f + Math.Min(0.5f, cw.Market * 0.1f));
                int amt = (int)(Economy.Amount(c, "gold") * r);
                if (amt > 0 && Economy.Take(c, "gold", amt)) taxes += amt;
            }
            flow["taxes"] = taxes;
            flow["companies"] = Companies.Of(k).Count * 3f * yrs;
            flow["currency"] = Currency.Has(k) ? 5f * yrs + Reserve(k) * 0.02f : 0f;
            float tribute = 0f;
            foreach (Kingdom v in Diplomacy.DependentsOf(k)) tribute += 2f * Vassals.TributeMult(k) * Profile.Get(v).Cities;
            flow["tribute"] = tribute * yrs;
            float income = flow["taxes"] + flow["companies"] + flow["currency"] + flow["tribute"];
            string fis = Laws.Get(k, "fiscal");
            float soldiers = k.countTotalWarriors() * Pay(k) * yrs;
            float court = p.Cities * (0.5f + Gov.Band(k) * 0.2f) * yrs * (fis == "court_first" ? 1.5f : fis == "reserve_first" ? 0.7f : 1f);
            float res = Reserve(k) + income - court;
            flow["court"] = -court;
            if (res >= soldiers)
            {
                res -= soldiers;
                flow["soldiers"] = -soldiers;
                if (Unpaid(k) > 0) Store.Put(k.data, KUnpaid, 0);
            }
            else
            {
                flow["soldiers"] = -Math.Max(0f, res);
                res = 0f;
                int u = Unpaid(k) + yrs;
                Store.Put(k.data, KUnpaid, u);
                Bonus.Dirty(k);
                Desert(k, u);
                Gov.Log("treasury_unpaid", k, G.Name(k), u.ToString());
            }
            float works = 0f;
            float keep = fis == "reserve_first" ? 400f : 150f;
            if ((fis == "works_first" && res > 60f) || res > keep * 2f) works = FundWorks(k, Math.Min(res * 0.3f, 200f));
            res -= works;
            flow["works"] = -works;
            float arm = ArmyCommand.Spend(k, res, keep);
            res -= arm;
            flow["armament"] = -arm;
            Store.Put(k.data, KRes, Math.Max(0f, res));
            Store.Map(k.data, KFlow, flow);
            Note(k, "income", income);
            Note(k, "spent", court + soldiers + works + arm);
            Bonus.Dirty(k);
        }

        private static void Desert(Kingdom k, int unpaid)
        {
            if (unpaid < 2) return;
            int n = 0, max = Math.Max(1, k.countTotalWarriors() / 10);
            foreach (Actor a in k.getUnits().ToList())
            {
                if (n >= max) break;
                if (!G.Ok(a) || !a.isWarrior() || a.is_army_captain) continue;
                a.setProfession(UnitProfession.Unit, true);
                n++;
            }
        }

        private static float FundWorks(Kingdom k, float budget)
        {
            if (budget < 20f) return 0f;
            City c = G.Cities(k).Where(x => G.Ok(x)).OrderBy(x => x.buildings != null ? x.buildings.Count : 0).FirstOrDefault();
            if (c == null) return 0f;
            int amt = (int)budget;
            Economy.Give(c, "gold", amt);
            if (G.Chance(0.3f)) Gov.Log("treasury_works", k, G.Name(k), G.Name(c), amt.ToString(), null, c);
            return amt;
        }

        public static float MandateShare(Kingdom k)
        {
            if (Unpaid(k) > 0) return -10f;
            float upkeep = k.countTotalWarriors() * Pay(k) + Profile.Get(k).Cities;
            return Reserve(k) > upkeep * 2f ? 5f : 0f;
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!G.Ok(k) || !k.isCiv()) return fx;
            if (Unpaid(k) > 0)
            {
                fx["attack"] = -0.12f;
                fx["defense"] = -0.06f;
            }
            WorksCount w = Works(k);
            if (w.Market > 0) fx["trade"] = Math.Min(0.08f, w.Market * 0.01f);
            if (w.Barracks > 0) Store.Add(fx, "attack", Math.Min(0.06f, w.Barracks * 0.01f));
            if (w.Tower > 0) Store.Add(fx, "defense", Math.Min(0.06f, w.Tower * 0.01f));
            if (w.Windmill > 0) Store.Add(fx, "prod", Math.Min(0.06f, w.Windmill * 0.01f));
            return fx;
        }

        public static void Bankrupt(Kingdom k)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KRes, 0f);
            Store.Put(k.data, KUnpaid, Math.Max(1, Unpaid(k)));
            Bonus.Dirty(k);
            Gov.Log("treasury_unpaid", k, G.Name(k), Unpaid(k).ToString());
        }

        public static void SetReserve(Kingdom k, float v)
        {
            if (G.Ok(k)) Store.Put(k.data, KRes, Math.Max(0f, v));
        }
    }
}
