using System;
using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal static class Currency
    {
        public const string KName = "civ_cur_name";
        public const string KSym = "civ_cur_sym";
        public const string KRate = "civ_cur_rate";
        public const string KInfl = "civ_cur_infl";
        public const string KHist = "civ_cur_hist";
        public const string KUnion = "civ_cur_union";
        public const string KReforms = "civ_cur_reforms";

        public static bool Has(Kingdom k)
        {
            return G.Ok(k) && Store.Str(k.data, KName).Length > 0;
        }

        public static string Name(Kingdom k)
        {
            if (!G.Ok(k)) return "";
            string u = Store.Str(k.data, KUnion);
            Kingdom owner = G.KingdomById(u);
            if (owner != null && owner != k && Has(owner)) return Name(owner);
            return Store.Str(k.data, KName);
        }

        public static string Symbol(Kingdom k)
        {
            if (!G.Ok(k)) return "";
            Kingdom owner = Issuer(k);
            return Store.Str(owner.data, KSym);
        }

        public static Kingdom Issuer(Kingdom k)
        {
            Kingdom owner = G.KingdomById(Store.Str(k.data, KUnion));
            return owner != null && Has(owner) ? owner : k;
        }

        public static float Rate(Kingdom k)
        {
            if (!Has(k)) return 0f;
            Kingdom owner = Issuer(k);
            return Store.Flt(owner.data, KRate, 1f);
        }

        public static float Inflation(Kingdom k)
        {
            if (!Has(k)) return 0f;
            return Store.Flt(Issuer(k).data, KInfl, 0.02f);
        }

        public static List<float> History(Kingdom k)
        {
            var l = new List<float>();
            if (!Has(k)) return l;
            foreach (string p in Store.Str(Issuer(k).data, KHist).Split(','))
            {
                float v;
                if (float.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) l.Add(v);
            }
            return l;
        }

        public static float Exchange(Kingdom from, Kingdom to)
        {
            float a = Rate(from);
            float b = Rate(to);
            if (a <= 0f || b <= 0f) return 1f;
            return a / b;
        }

        public static void Year(Kingdom k, int yrs)
        {
            if (!Research.HasFeature(k, "currency"))
            {
                return;
            }
            if (!Has(k))
            {
                Create(k, false);
                return;
            }
            Kingdom issuer = Issuer(k);
            if (issuer != k) return;
            float rate = Store.Flt(k.data, KRate, 1f);
            float infl = Store.Flt(k.data, KInfl, 0.02f);
            float target = 0.02f;
            if (Research.HasFeature(k, "paper_money")) target += 0.02f;
            if (World.world.wars.hasWars(k)) target += 0.05f;
            int famine = 0, cities = 0;
            foreach (City c in G.Cities(k))
            {
                cities++;
                if (Economy.IsFamine(c)) famine++;
            }
            if (cities > 0) target += 0.08f * famine / cities;
            target += (0.5f - Politics.Stability(k)) * 0.1f;
            if (Research.HasFeature(k, "central_bank")) target = target * 0.55f + 0.01f;
            string econ = Policies.Id(k, "econ");
            if (econ == "planned") target += 0.01f;
            target += UnityEngine.Random.Range(-0.02f, 0.03f);
            infl += (target - infl) * 0.4f;
            infl = Mathf.Clamp(infl, -0.05f, 3f);
            float growth = (Bonus.Relative(k, "trade") - 1f) * 0.05f + (Bonus.Relative(k, "prod") - 1f) * 0.04f + 0.01f;
            rate *= Mathf.Pow(Math.Max(0.05f, 1f + growth - infl * 0.6f), yrs);
            rate = Mathf.Clamp(rate, 0.0001f, 1000f);
            Store.Put(k.data, KInfl, infl);
            Store.Put(k.data, KRate, rate);
            List<float> h = History(k);
            h.Add(rate);
            while (h.Count > 40) h.RemoveAt(0);
            var parts = new List<string>();
            foreach (float v in h) parts.Add(v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture));
            Store.Put(k.data, KHist, string.Join(",", parts));
            if (infl > 0.5f && G.Chance(0.3f)) Internet.OnHyperinflation(k, infl);
            if (infl > 0.8f && G.Chance(0.1f)) Chronicle.Log("hyperinflation", k, G.Name(k), Store.Str(k.data, KName));
            if (rate < 0.02f || infl > 1.2f)
            {
                Reform(k);
            }
            Unions(k);
        }

        public static void Create(Kingdom k, bool reform)
        {
            string name = Names.Currency(k, Research.Era(k));
            Store.Put(k.data, KName, name);
            Store.Put(k.data, KSym, Names.Symbol(name));
            Store.Put(k.data, KRate, 1f);
            Store.Put(k.data, KInfl, 0.02f);
            Store.Put(k.data, KHist, "1");
            if (!reform) Internet.OnCurrency(k, name);
        }

        private static void Reform(Kingdom k)
        {
            string old = Store.Str(k.data, KName);
            Store.Put(k.data, KReforms, Store.Int(k.data, KReforms, 0) + 1);
            Create(k, true);
            Internet.OnCurrencyReform(k, old, Store.Str(k.data, KName));
            Chronicle.Log("currency_reform", k, G.Name(k), Store.Str(k.data, KName), null, null, null, null, null, false);
        }

        private static void Unions(Kingdom k)
        {
            Alliance al = k.getAlliance();
            if (al == null || !G.Chance(0.01f)) return;
            if (!Research.HasFeature(k, "central_bank")) return;
            Kingdom strongest = k;
            foreach (Kingdom o in al.kingdoms_list)
            {
                if (o == null || !o.isAlive() || !Has(o)) continue;
                if (Rate(o) * G.Pop(o) > Rate(strongest) * G.Pop(strongest)) strongest = o;
            }
            if (strongest == k) return;
            Store.Put(k.data, KUnion, strongest.data.id.ToString());
            Internet.OnCurrencyUnion(k, strongest);
            Chronicle.Log("currency_union", k, G.Name(k), Store.Str(strongest.data, KName), null, strongest);
        }

        public static string Format(Kingdom k, float goldValue)
        {
            if (!Has(k)) return Mathf.RoundToInt(goldValue) + " " + Txt.T("civ_gold");
            float r = Rate(k);
            float amount = r > 0f ? goldValue / r : goldValue;
            return Short(amount) + " " + Symbol(k);
        }

        public static string Short(float v)
        {
            float a = Math.Abs(v);
            if (a >= 1e9f) return (v / 1e9f).ToString("0.#") + "B";
            if (a >= 1e6f) return (v / 1e6f).ToString("0.#") + "M";
            if (a >= 1e3f) return (v / 1e3f).ToString("0.#") + "k";
            return v.ToString(a < 10f ? "0.##" : "0");
        }
    }
}
