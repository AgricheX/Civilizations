using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class WinTreasury : GovWindow<WinTreasury>
    {
        public const string Id = "civ_treasury";

        protected override float Wd
        {
            get { return 330f; }
        }

        protected override float Ht
        {
            get { return 390f; }
        }

        protected override string Tab
        {
            get { return "treasury"; }
        }

        public static void Open(Kingdom k)
        {
            Show(Id, k, () => CreateAndInit(Id));
        }

        protected override string Sig(Kingdom k)
        {
            return base.Sig(k) + "|" + (int)Treasury.Gold(k) + "|" + Treasury.Ledger(k).Count;
        }

        private static string FlowName(Kingdom k, string key)
        {
            if (key.StartsWith("in_c"))
            {
                long id;
                if (long.TryParse(key.Substring(4), out id))
                {
                    City c = G.Cities(k).FirstOrDefault(x => x.data.id == id);
                    return c != null ? G.Name(c) : Txt.T("civ_treas_flow_lost_city");
                }
            }
            return Txt.T("civ_treas_flow_" + key);
        }

        protected override void Draw(Sheet s, Kingdom k)
        {
            Title(s, k, Txt.T("civ_gov_tab_treasury"), null);
            float y = 28f;
            if (!Treasury.Coin(k))
            {
                s.Locked(4f, y, s.W - 8f, 50f, GovEra.Coin, Txt.T("civ_treas_coin_locked"));
                y += 54f;
                if (k.capital != null) s.Text(6f, y, s.W - 12f, 12f, Txt.F("civ_treas_capital_gold", Economy.Amount(k.capital, "gold")), 8, TextAnchor.MiddleLeft, UiKit.Gold);
                y += 14f;
                Ledger(s, k, y);
                return;
            }
            s.Panel(4f, y, s.W - 8f, 34f, 0.85f);
            s.Icon(8f, y + 5f, 24f, "gold");
            s.Text(36f, y + 1f, s.W * 0.5f, 18f, ((int)Treasury.Gold(k)).ToString(), 13, TextAnchor.MiddleLeft, UiKit.Gold);
            float d = Treasury.Delta(k);
            s.Text(36f, y + 19f, s.W * 0.5f, 12f, (d >= 0 ? "+" : "") + (int)d + " " + Txt.T("civ_treas_last_year"), 7, TextAnchor.MiddleLeft, d >= 0 ? UiKit.Good : UiKit.Bad);
            float owed = Treasury.Owed(k);
            string side = owed > 0f ? Txt.F("civ_treas_owed", (int)owed, Treasury.UnpaidYears(k)) : Txt.F("civ_treas_tax", (int)(Treasury.TaxShare(k) * 100f));
            s.Text(s.W * 0.5f, y + 2f, s.W * 0.5f - 10f, 30f, side, 7, TextAnchor.MiddleRight, owed > 0f ? UiKit.Bad : UiKit.Soft);
            y += 38f;
            Dictionary<string, float> flows = Treasury.Flows(k);
            if (GovEra.Has(k, GovEra.Flow)) y = Flow(s, k, flows, y);
            else y = List(s, k, flows, y);
            if (GovEra.Has(k, GovEra.Guard))
            {
                s.Text(6f, y, s.W - 12f, 12f, Txt.F("civ_treas_guard", Treasury.GuardSize(k), (int)(Treasury.LevyShare(k) * 100f)), 7, TextAnchor.MiddleLeft, UiKit.Cyan);
                s.Hit(6f, y, s.W - 12f, 12f, null, Txt.T("civ_treas_guard_t"), Txt.T("civ_treas_guard_d"));
            }
            else s.Text(6f, y, s.W - 12f, 12f, GovEra.Note(GovEra.Guard) + " · " + Txt.T("civ_treas_guard_t"), 6, TextAnchor.MiddleLeft, UiKit.Dim);
            y += 14f;
            Ledger(s, k, y);
        }

        private static float List(Sheet s, Kingdom k, Dictionary<string, float> flows, float y)
        {
            s.Header(4f, y, s.W - 8f, Txt.T("civ_treas_flows"), "flow");
            y += 18f;
            float inc = flows.Where(x => x.Key.StartsWith("in_")).Sum(x => x.Value);
            s.Text(6f, y, s.W - 12f, 11f, Txt.T("civ_treas_income") + ": " + (int)inc, 7, TextAnchor.MiddleLeft, UiKit.Good);
            y += 12f;
            foreach (var kv in flows.Where(x => x.Key.StartsWith("out_") && x.Value >= 1f).OrderByDescending(x => x.Value).Take(5))
            {
                s.Text(12f, y, s.W * 0.6f, 11f, FlowName(k, kv.Key), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                s.Text(s.W * 0.6f, y, s.W * 0.4f - 10f, 11f, "-" + (int)kv.Value, 7, TextAnchor.MiddleRight, UiKit.Bad);
                y += 11f;
            }
            s.Text(6f, y, s.W - 12f, 10f, GovEra.Note(GovEra.Flow), 6, TextAnchor.MiddleLeft, UiKit.Dim);
            return y + 14f;
        }

        private static float Flow(Sheet s, Kingdom k, Dictionary<string, float> flows, float y)
        {
            s.Header(4f, y, s.W - 8f, Txt.T("civ_treas_flows"), "flow");
            y += 18f;
            const float H = 128f;
            s.Panel(4f, y, s.W - 8f, H + 8f, 0.5f);
            var ins = flows.Where(x => x.Key.StartsWith("in_") && x.Value >= 1f).OrderByDescending(x => x.Value).ToList();
            if (ins.Count > 6)
            {
                float rest = ins.Skip(5).Sum(x => x.Value);
                ins = ins.Take(5).ToList();
                ins.Add(new KeyValuePair<string, float>("in_other", rest));
            }
            var outs = flows.Where(x => x.Key.StartsWith("out_") && x.Value >= 1f).OrderByDescending(x => x.Value).ToList();
            float saved = flows.ContainsKey("saved") ? flows["saved"] : 0f;
            if (saved >= 1f) outs.Add(new KeyValuePair<string, float>("saved", saved));
            float tin = ins.Sum(x => x.Value), tout = outs.Sum(x => x.Value);
            float tot = Math.Max(tin, tout);
            if (tot < 1f)
            {
                s.Text(8f, y, s.W - 16f, H, Txt.T("civ_treas_no_flow"), 8, TextAnchor.MiddleCenter, UiKit.Dim);
                return y + H + 12f;
            }
            float gap = 3f;
            float sc = (H - gap * Math.Max(ins.Count, outs.Count)) / tot;
            float lx = 70f, rx = s.W - 74f, cx = s.W / 2f - 5f;
            float top = y + 4f;
            float cTop = top + (H - tin * sc) / 2f;
            float cTopOut = top + (H - tout * sc) / 2f;
            s.Rect(cx, Math.Min(cTop, cTopOut), 10f, Math.Max(tin, tout) * sc, UiKit.Gold);
            float ly = top, acc = cTop;
            foreach (var kv in ins)
            {
                float h = Math.Max(1f, kv.Value * sc);
                Color c = kv.Key == "in_tribute" ? UiKit.Cyan : kv.Key == "in_reparations" ? UiKit.Bad : UiKit.Good;
                s.Rect(lx - 6f, ly, 6f, h, c);
                GovKit.Sankey(s, lx, ly, cx, acc, h, h, new Color(c.r, c.g, c.b, 0.35f));
                s.Text(6f, ly + h / 2f - 5f, lx - 14f, 10f, FlowName(k, kv.Key), 6, TextAnchor.MiddleRight, UiKit.Soft);
                s.Hit(6f, ly, lx, Math.Max(8f, h), null, FlowName(k, kv.Key), ((int)kv.Value).ToString());
                acc += h;
                ly += h + gap;
            }
            float ry = top;
            acc = cTopOut;
            foreach (var kv in outs)
            {
                float h = Math.Max(1f, kv.Value * sc);
                Color c = kv.Key == "saved" ? UiKit.Gold : kv.Key == "out_guard" || kv.Key == "out_arms" ? UiKit.Bad : kv.Key == "out_works" ? UiKit.Good : UiKit.Soft;
                GovKit.Sankey(s, cx + 10f, acc, rx, ry, h, h, new Color(c.r, c.g, c.b, 0.35f));
                s.Rect(rx, ry, 6f, h, c);
                s.Text(rx + 9f, ry + h / 2f - 5f, s.W - rx - 14f, 10f, FlowName(k, kv.Key), 6, TextAnchor.MiddleLeft, UiKit.Soft);
                s.Hit(rx, ry, s.W - rx - 6f, Math.Max(8f, h), null, FlowName(k, kv.Key), ((int)kv.Value).ToString());
                acc += h;
                ry += h + gap;
            }
            return y + H + 12f;
        }

        private static void Ledger(Sheet s, Kingdom k, float y)
        {
            if (y > s.H - 30f) return;
            s.Header(4f, y, s.W - 8f, Txt.T("civ_treas_ledger"), "ledger");
            y += 18f;
            if (!GovEra.Has(k, GovEra.Ledger))
            {
                s.Locked(4f, y, s.W - 8f, Math.Min(40f, s.H - y), GovEra.Ledger);
                return;
            }
            var l = Treasury.Ledger(k);
            if (l.Count == 0) s.Text(6f, y, s.W - 12f, 12f, Txt.T("civ_treas_ledger_empty"), 7, TextAnchor.MiddleLeft, UiKit.Dim);
            foreach (var e in l)
            {
                if (y > s.H - 11f) break;
                s.Text(6f, y, 34f, 11f, e.Key.ToString(), 7, TextAnchor.MiddleLeft, UiKit.Gold);
                s.Text(40f, y, s.W - 46f, 11f, e.Value, 7, TextAnchor.MiddleLeft, UiKit.Soft);
                y += 11f;
            }
        }
    }
}
