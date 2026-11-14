using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class WinVassals : GovWindow<WinVassals>
    {
        public const string Id = "civ_vassals";
        private static int _page;
        public static readonly string[] Origins = { "demand", "protection", "granted", "grant", "appanage", "release", "war", "surrender", "cession", "peace", "puppet", "sandbox" };

        protected override float Wd
        {
            get { return 430f; }
        }

        protected override float Ht
        {
            get { return 350f; }
        }

        protected override string Tab
        {
            get { return "vassals"; }
        }

        public static void Open(Kingdom k)
        {
            Show(Id, k, () => CreateAndInit(Id));
        }

        protected override string Sig(Kingdom k)
        {
            List<Kingdom> vs = Diplomacy.DependentsOf(k);
            return base.Sig(k) + "|" + vs.Count + "|" + string.Join(",", vs.Select(v => ((int)Vassals.Integration(v)).ToString()).ToArray()) + "|" + _page + "|" + (Diplomacy.Overlord(k) != null);
        }

        private static Color StateColor(int st)
        {
            switch (st)
            {
                case 0: return UiKit.Bad;
                case 1: return UiKit.Gold;
                case 2: return UiKit.Soft;
                case 3: return UiKit.Good;
            }
            return UiKit.Dim;
        }

        protected override void Draw(Sheet s, Kingdom k)
        {
            Title(s, k, Txt.T("civ_gov_tab_vassals"), null);
            float y = 28f;
            if (!GovEra.Has(k, GovEra.Tributaries))
            {
                s.Locked(4f, y, s.W - 8f, 60f, GovEra.Tributaries, Txt.T("civ_gov_tab_vassals"));
                return;
            }
            bool act = GovKit.CanAct(k);
            Kingdom lord = Diplomacy.Overlord(k);
            if (G.Ok(lord))
            {
                s.Header(4f, y, s.W - 8f, Txt.T("civ_vas_our_lord"), "lord");
                y += 18f;
                s.Panel(4f, y, s.W - 8f, 44f, 0.8f);
                UiKit.Banner(s.R, lord, s.At(8f, y + 4f, 26f, 26f), 26f);
                int st = Vassals.State(k);
                s.Text(40f, y + 2f, s.W * 0.5f, 12f, G.Name(lord), 9, TextAnchor.MiddleLeft, UiKit.Gold);
                s.Text(40f, y + 15f, s.W * 0.5f, 11f, Txt.T("civ_vas_status_" + Vassals.Status(k)) + " · " + Vassals.StateName(st), 7, TextAnchor.MiddleLeft, StateColor(st));
                s.Text(40f, y + 27f, s.W * 0.5f, 11f, Txt.F("civ_vas_paid", Vassals.LastTribute(k)), 6, TextAnchor.MiddleLeft, UiKit.Dim);
                s.Bar(s.W * 0.55f, y + 4f, s.W * 0.45f - 12f, 10f, Vassals.Integration(k) / 100f, StateColor(st), Txt.T("civ_vas_integration") + " " + (int)Vassals.Integration(k));
                if (act) s.Btn(s.W * 0.55f, y + 22f, s.W * 0.45f - 12f, 17f, Txt.T("civ_vas_independence"), () =>
                {
                    Vassals.Independence(k, true);
                    Done(k);
                }, Txt.T("civ_vas_independence"), Txt.T("civ_vas_independence_d"));
                y += 50f;
            }
            List<Kingdom> vs = Diplomacy.DependentsOf(k).Where(G.Ok).OrderByDescending(v => Vassals.Integration(v)).ToList();
            s.Header(4f, y, s.W - 8f, Txt.F("civ_vas_dependents", vs.Count), "vassal");
            y += 18f;
            float foot = 22f;
            int per = Math.Max(1, (int)((s.H - y - foot - 14f) / 32f));
            int pages = Math.Max(1, (vs.Count + per - 1) / per);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            if (vs.Count == 0) s.Text(6f, y, s.W - 12f, 24f, Txt.T("civ_vas_none"), 8, TextAnchor.MiddleCenter, UiKit.Dim);
            foreach (Kingdom v in vs.Skip(_page * per).Take(per))
            {
                Row(s, k, v, y, act);
                y += 32f;
            }
            if (pages > 1)
            {
                s.Btn(s.W / 2f - 50f, s.H - foot - 14f, 18f, 13f, "<", () =>
                {
                    _page--;
                    Redraw();
                });
                s.Text(s.W / 2f - 30f, s.H - foot - 14f, 60f, 13f, (_page + 1) + " / " + pages, 7, TextAnchor.MiddleCenter, UiKit.Soft);
                s.Btn(s.W / 2f + 32f, s.H - foot - 14f, 18f, 13f, ">", () =>
                {
                    _page++;
                    Redraw();
                });
            }
            if (!act || Diplomacy.Overlord(k) != null) return;
            float bw = (s.W - 12f) / 2f;
            Kingdom t = Vassals.Victim(k, Profile.Get(k));
            var b = s.Btn(4f, s.H - 19f, bw, 17f, Txt.T("civ_vas_demand"), () =>
            {
                Kingdom tt = Vassals.Victim(k, Profile.Get(k));
                if (tt != null) Vassals.Submission(k, tt);
                Done(k);
            }, Txt.T("civ_vas_demand"), t != null ? Txt.F("civ_vas_demand_d", G.Name(t)) : Txt.T("civ_vas_demand_none"));
            if (t == null && b.Text != null) b.Text.color = UiKit.Dim;
            if (GovEra.Has(k, GovEra.Fiefs) && !Gov.Republic(k))
            {
                s.Btn(8f + bw, s.H - 19f, bw, 17f, Txt.T("civ_vas_fief"), () =>
                {
                    Vassals.Appanage(k);
                    Done(k);
                }, Txt.T("civ_vas_fief"), Txt.T("civ_vas_fief_d"));
            }
            else s.Text(8f + bw, s.H - 19f, bw, 17f, GovEra.Note(GovEra.Fiefs), 6, TextAnchor.MiddleCenter, UiKit.Dim);
        }

        private static void Row(Sheet s, Kingdom k, Kingdom v, float y, bool act)
        {
            int st = Vassals.State(v);
            s.Panel(4f, y, s.W - 8f, 30f, 0.7f);
            UiKit.Banner(s.R, v, s.At(7f, y + 3f, 24f, 24f), 24f);
            Kingdom vv = v;
            s.Hit(7f, y + 3f, 24f, 24f, () => GovKit.Open("vassals", vv), G.Name(v), Gov.FormName(v));
            s.Text(34f, y + 1f, s.W * 0.36f, 12f, G.Name(v), 8, TextAnchor.MiddleLeft, UiKit.Gold);
            s.Text(34f, y + 13f, s.W * 0.36f, 10f, Txt.T("civ_vas_status_" + Vassals.Status(v)) + " · " + Vassals.StateName(st), 6, TextAnchor.MiddleLeft, StateColor(st));
            s.Text(34f, y + 21f, s.W * 0.36f, 9f, Txt.F("civ_vas_row_info", G.Cities(v).Count, Vassals.LastTribute(v)), 5, TextAnchor.MiddleLeft, UiKit.Dim);
            float bx = 34f + s.W * 0.36f;
            s.Bar(bx, y + 4f, s.W * 0.22f, 9f, Vassals.Integration(v) / 100f, StateColor(st), ((int)Vassals.Integration(v)).ToString());
            string org = Vassals.Origin(v);
            if (Array.IndexOf(Origins, org) >= 0) s.Text(bx, y + 16f, s.W * 0.22f, 10f, Txt.T("civ_vas_origin_" + org), 5, TextAnchor.MiddleLeft, UiKit.Dim);
            if (!act) return;
            float x = bx + s.W * 0.22f + 4f, w = (s.W - 8f - x) / 2f;
            bool prov = GovEra.Has(k, GovEra.Provinces) && st == 3;
            var dr = s.Btn(x, y + 6f, w - 2f, 17f, Txt.T("civ_vas_direct"), () =>
            {
                if (GovEra.Has(k, GovEra.Provinces) && Vassals.State(vv) == 3) Vassals.DirectRule(k, vv);
                Done(k);
            }, Txt.T("civ_vas_direct"), prov ? Txt.T("civ_vas_direct_d") : GovEra.Has(k, GovEra.Provinces) ? Txt.T("civ_vas_direct_no") : GovEra.Note(GovEra.Provinces));
            if (!prov && dr.Text != null) dr.Text.color = UiKit.Dim;
            s.Btn(x + w, y + 6f, w - 2f, 17f, Txt.T("civ_vas_release"), () =>
            {
                Diplomacy.Release(vv, "release");
                Done(k);
            }, Txt.T("civ_vas_release"), Txt.T("civ_vas_release_d"));
        }
    }
}
