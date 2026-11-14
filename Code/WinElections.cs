using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class WinElections : GovWindow<WinElections>
    {
        public const string Id = "civ_elections";
        private static City _city;
        private static Actor _pick;

        protected override float Wd
        {
            get { return 600f; }
        }

        protected override float Ht
        {
            get { return 360f; }
        }

        protected override string Tab
        {
            get { return "elections"; }
        }

        public static void Open(Kingdom k)
        {
            if (GovKit.Pick(k) != K) _city = null;
            Show(Id, k, () => CreateAndInit(Id));
        }

        protected override string Sig(Kingdom k)
        {
            return base.Sig(k) + "|" + (G.Ok(k.king) ? G.Id(k.king) : "") + "|" + Rulers.NextVote(k) + "|" + (G.Ok(_city) ? _city.data.id : 0L) + "|" + (G.Ok(_pick) ? G.Id(_pick) : "");
        }

        protected override void Draw(Sheet s, Kingdom k)
        {
            Title(s, k, Gov.FormName(k) + " · " + Gov.Title(k) + ": " + Gov.RulerName(k), null);
            float L = Mathf.Floor(s.W * 0.5f);
            Sheet left = s.Sub(0f, 28f, L, s.H - 28f);
            if (Rulers.Elects(k)) Election(left, k);
            else Succession(left, k);
            Domains(s.Sub(L, 28f, s.W - L, s.H - 28f), k);
        }

        private static void Election(Sheet s, Kingdom k)
        {
            bool mass = GovEra.Has(k, GovEra.MassVote);
            s.Header(4f, 0f, s.W - 8f, Txt.T("civ_elect_title"), "ballot");
            List<Candidate> l = Rulers.Candidates(k);
            int total = Math.Max(1, l.Sum(c => c.Votes));
            Candidate win = Rulers.Winner(l);
            var seats = Fam.Seats(k);
            float cw = (s.W - 8f) / Math.Max(1, l.Count);
            for (int i = 0; i < l.Count; i++)
            {
                Candidate c = l[i];
                float x = 4f + i * cw;
                bool sel = c.A == _pick;
                s.Panel(x, 18f, cw - 3f, 76f, sel ? 1f : 0.6f);
                if (c == win) s.Icon(x + cw - 19f, 20f, 13f, "crown", UiKit.Gold);
                UiKit.Avatar(s.R, c.A, s.At(x + 4f, 22f, 30f, 30f), 0.6f);
                s.Text(x + 36f, 20f, cw - 42f, 12f, c.A.getName(), 7, TextAnchor.MiddleLeft, UiKit.Gold);
                s.Rect(x + 36f, 35f, 7f, 7f, Fam.Color(c.Fam));
                s.Text(x + 46f, 32f, cw - 52f, 12f, Fam.Name(c.Fam), 6, TextAnchor.MiddleLeft, UiKit.Soft);
                int fs = seats.ContainsKey(c.Fam) ? seats[c.Fam] : 0;
                s.Text(x + 36f, 44f, cw - 42f, 10f, fs + " " + Txt.T("civ_parl_seats"), 6, TextAnchor.MiddleLeft, UiKit.Dim);
                s.Text(x + 4f, 56f, cw - 10f, 10f, (c.A == k.king ? Txt.T("civ_elect_incumbent") + " · " : "") + Txt.F("civ_elect_age", c.A.getAge()), 6, TextAnchor.MiddleLeft, UiKit.Dim);
                float ch = c.Votes / (float)total;
                s.Bar(x + 4f, 70f, cw - 11f, 9f, ch, c == win ? UiKit.Good : UiKit.Cyan, (int)(ch * 100f) + "%");
                Actor a = c.A;
                s.Hit(x, 18f, cw - 3f, 50f, () =>
                {
                    _pick = a;
                    Redraw();
                }, a.getName(), Txt.T("civ_elect_pick_d"));
            }
            if (l.Count == 0) s.Text(4f, 18f, s.W - 8f, 76f, Txt.T("civ_elect_none"), 8, TextAnchor.MiddleCenter, UiKit.Dim);
            var blocks = l.Select(c => new KeyValuePair<Color, int>(Fam.Color(c.Fam), c.Votes)).ToList();
            float hh = 92f;
            s.Panel(4f, 98f, s.W - 8f, hh, 0.5f);
            if (l.Count > 0) GovKit.Hemicycle(s, 8f, 100f, s.W - 16f, hh - 6f, blocks, total);
            float y = 98f + hh + 2f;
            if (win != null) s.Text(4f, y, s.W - 8f, 12f, Txt.F("civ_elect_expected", win.A.getName(), (int)(win.Votes * 100f / total)), 8, TextAnchor.MiddleCenter, UiKit.Gold);
            y += 12f;
            int left = Math.Max(0, Rulers.NextVote(k) - G.Year);
            s.Text(4f, y, s.W - 8f, 10f, Txt.F("civ_elect_next", left) + " · " + Txt.T(mass ? "civ_elect_mass" : "civ_elect_seats"), 7, TextAnchor.MiddleCenter, UiKit.Soft);
            y += 11f;
            string[] last = Rulers.LastVote(k).Split('|');
            if (last.Length >= 4) s.Text(4f, y, s.W - 8f, 10f, Txt.F("civ_elect_last", last[0], last[1], last[2], last[3]), 6, TextAnchor.MiddleCenter, UiKit.Dim);
            y += 12f;
            if (!mass)
            {
                var ch2 = Rulers.Choices(k, l);
                float x = 6f;
                foreach (long f in Fam.Members(k).Take(8))
                {
                    if (!ch2.ContainsKey(f)) continue;
                    if (x > s.W - 70f) break;
                    s.Rect(x, y + 2f, 6f, 6f, Fam.Color(f));
                    s.Text(x + 8f, y, 54f, 10f, Fam.Name(f) + " → " + ch2[f].A.getName(), 5, TextAnchor.MiddleLeft, UiKit.Dim);
                    x += 66f;
                }
            }
            if (!GovKit.CanAct(k)) return;
            float bw = (s.W - 12f) / 2f;
            s.Btn(4f, s.H - 19f, bw, 17f, Txt.T("civ_elect_hold"), () =>
            {
                Rulers.Hold(k);
                _pick = null;
                Done(k);
            }, Txt.T("civ_elect_hold"), Txt.T("civ_elect_hold_d"), true);
            Actor ap = G.Ok(_pick) ? _pick : null;
            var b = s.Btn(8f + bw, s.H - 19f, bw, 17f, Txt.T("civ_elect_appoint"), () =>
            {
                if (!G.Ok(_pick)) return;
                Rulers.Appoint(k, _pick);
                Mandate.Add(k, -5f);
                _pick = null;
                Done(k);
            }, Txt.T("civ_elect_appoint"), ap != null ? Txt.F("civ_elect_appoint_d", ap.getName()) : Txt.T("civ_elect_pick_d"));
            if (ap == null && b.Text != null) b.Text.color = UiKit.Dim;
        }

        private static void Succession(Sheet s, Kingdom k)
        {
            bool rep = Gov.Republic(k);
            s.Header(4f, 0f, s.W - 8f, Txt.T(rep ? "civ_elect_council_rule" : "civ_elect_succession"), rep ? "ballot" : "crown");
            float y = 18f;
            s.Panel(4f, y, s.W - 8f, 40f, 0.7f);
            if (G.Ok(k.king))
            {
                UiKit.Avatar(s.R, k.king, s.At(8f, y + 4f, 32f, 32f), 0.6f);
                s.Text(44f, y + 2f, s.W - 52f, 12f, Gov.Title(k) + " " + k.king.getName(), 8, TextAnchor.MiddleLeft, UiKit.Gold);
                s.Text(44f, y + 14f, s.W - 52f, 11f, Fam.Name(Gov.ClanId(k.king)) + " · " + Txt.F("civ_elect_age", k.king.getAge()), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                s.Text(44f, y + 25f, s.W - 52f, 11f, Txt.F("civ_elect_dynasty", Mandate.Dynasty(k) + 1), 6, TextAnchor.MiddleLeft, UiKit.Dim);
            }
            else s.Text(8f, y, s.W - 16f, 40f, Gov.RulerName(k), 9, TextAnchor.MiddleCenter, UiKit.Bad);
            y += 44f;
            if (rep && !GovEra.Has(k, GovEra.Republic) || rep && !Gov.On("civ_gov_elections"))
            {
                s.Locked(4f, y, s.W - 8f, 44f, GovEra.Republic, Txt.T("civ_elect_locked"));
                y += 48f;
            }
            else if (!rep)
            {
                s.Text(6f, y, s.W - 12f, 11f, Txt.T("civ_elect_heirs"), 8, TextAnchor.MiddleLeft, UiKit.Gold);
                y += 12f;
                List<Actor> heirs = Rulers.Succession(k);
                if (heirs.Count == 0) s.Text(6f, y, s.W - 12f, 12f, Txt.T("civ_elect_no_heirs"), 7, TextAnchor.MiddleLeft, UiKit.Bad);
                float hw = (s.W - 8f) / 3f;
                for (int i = 0; i < heirs.Count && i < 6; i++)
                {
                    Actor a = heirs[i];
                    float x = 4f + (i % 3) * hw, yy = y + (i / 3) * 30f;
                    s.Panel(x, yy, hw - 3f, 28f, i == 0 ? 0.9f : 0.5f);
                    UiKit.Avatar(s.R, a, s.At(x + 2f, yy + 2f, 24f, 24f), 0.45f);
                    s.Text(x + 28f, yy + 1f, hw - 32f, 12f, (i + 1) + ". " + a.getName(), 6, TextAnchor.MiddleLeft, i == 0 ? UiKit.Gold : UiKit.Soft);
                    s.Text(x + 28f, yy + 13f, hw - 32f, 12f, Txt.F("civ_elect_age", a.getAge()) + (a.kingdom != k ? " · " + G.Name(a.kingdom) : ""), 6, TextAnchor.MiddleLeft, UiKit.Dim);
                }
                y += heirs.Count > 3 ? 62f : 32f;
            }
            Offices(s, k, y + 2f);
        }

        private static void Offices(Sheet s, Kingdom k, float y)
        {
            if (y > s.H - 30f) return;
            string head = Txt.T("civ_elect_offices");
            if (Parl.Active(k) && GovEra.Has(k, GovEra.Nobles)) head += " · " + Parl.OptName("offices", Parl.Get(k, "offices"));
            s.Header(4f, y, s.W - 8f, head, "office");
            y += 18f;
            var cities = G.Cities(k).OrderByDescending(c => c == k.capital).ThenByDescending(c => c.getPopulationPeople()).ToList();
            int rows = Math.Max(0, (int)((s.H - y) / 12f));
            foreach (City c in cities.Take(rows))
            {
                Actor gv = c == k.capital ? k.king : c.leader;
                long f = c == k.capital ? Fam.Royal(k) : Gov.ClanId(c.leader);
                s.Text(6f, y, s.W * 0.32f, 11f, G.Name(c) + (c == k.capital ? " *" : ""), 7, TextAnchor.MiddleLeft, c == k.capital ? UiKit.Gold : UiKit.Soft);
                s.Text(6f + s.W * 0.32f, y, s.W * 0.33f, 11f, G.Ok(gv) ? gv.getName() : Txt.T("civ_gov_vacant"), 7, TextAnchor.MiddleLeft, G.Ok(gv) ? UiKit.Soft : UiKit.Bad);
                s.Rect(6f + s.W * 0.66f, y + 2f, 7f, 7f, Fam.Color(f));
                s.Text(16f + s.W * 0.66f, y, s.W * 0.34f - 22f, 11f, Fam.Name(f), 6, TextAnchor.MiddleLeft, UiKit.Dim);
                y += 12f;
            }
        }

        private static void Domains(Sheet s, Kingdom k)
        {
            bool fams = GovEra.Has(k, GovEra.Fiefs);
            s.Header(4f, 0f, s.W - 8f, Txt.T(fams ? "civ_dom_title" : "civ_dom_territory"), "domains");
            if (!GovEra.Has(k, GovEra.Nobles))
            {
                s.Locked(4f, 18f, s.W - 8f, s.H - 20f, GovEra.Nobles, Txt.T("civ_dom_title"));
                return;
            }
            if (_city != null && (!G.Ok(_city) || _city.kingdom != k)) _city = null;
            float mh = Mathf.Floor((s.H - 18f) * 0.62f);
            s.Panel(4f, 18f, s.W - 8f, mh, 0.9f);
            GovKit.Domains(s, 6f, 20f, s.W - 12f, mh - 4f, k, fams, _city, c =>
            {
                _city = c;
                Redraw();
            });
            float y = 20f + mh;
            float ch = s.H - y;
            s.Panel(4f, y, s.W - 8f, ch, 0.7f);
            if (!G.Ok(_city))
            {
                s.Text(8f, y + 2f, s.W - 16f, 12f, Txt.T("civ_dom_click"), 7, TextAnchor.MiddleCenter, UiKit.Dim);
                if (fams) Legend(s, k, y + 16f, ch - 18f);
                return;
            }
            City c0 = _city;
            bool cap = c0 == k.capital;
            s.Text(8f, y + 2f, s.W * 0.6f, 12f, G.Name(c0) + (cap ? " · " + Txt.T("civ_dom_capital") : ""), 8, TextAnchor.MiddleLeft, cap ? UiKit.Gold : UiKit.Soft);
            s.Text(s.W * 0.6f, y + 2f, s.W * 0.4f - 8f, 12f, Txt.F("civ_dom_pop", c0.getPopulationPeople()), 7, TextAnchor.MiddleRight, UiKit.Soft);
            Actor gv = cap ? k.king : c0.leader;
            if (G.Ok(gv))
            {
                UiKit.Avatar(s.R, gv, s.At(8f, y + 16f, 28f, 28f), 0.5f);
                s.Text(40f, y + 16f, s.W * 0.5f, 11f, gv.getName(), 7, TextAnchor.MiddleLeft, UiKit.Gold);
                s.Text(40f, y + 27f, s.W * 0.5f, 11f, Txt.T(cap ? "civ_dom_ruler" : "civ_dom_governor") + " · " + Txt.F("civ_elect_age", gv.getAge()), 6, TextAnchor.MiddleLeft, UiKit.Dim);
            }
            else s.Text(8f, y + 16f, s.W * 0.5f, 22f, Txt.T("civ_gov_vacant"), 7, TextAnchor.MiddleLeft, UiKit.Bad);
            if (!fams) return;
            long f = Fam.Holder(c0, k);
            SeatTable t = Fam.Table(k);
            int n = t.Lands.ContainsKey(f) ? t.Lands[f].Count : 0;
            int seats = t.Seats.ContainsKey(f) ? t.Seats[f] : 0;
            float fx = s.W * 0.55f;
            s.Rect(fx, y + 18f, 8f, 8f, Fam.Color(f));
            s.Text(fx + 11f, y + 16f, s.W - fx - 18f, 11f, Fam.Name(f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
            s.Text(fx, y + 28f, s.W - fx - 8f, 11f, Txt.F("civ_dom_fam_stats", n, seats), 6, TextAnchor.MiddleLeft, UiKit.Dim);
        }

        private static void Legend(Sheet s, Kingdom k, float y, float h)
        {
            SeatTable t = Fam.Table(k);
            float x = 8f, cw = (s.W - 16f) / 2f;
            int i = 0, rows = Math.Max(1, (int)(h / 11f));
            foreach (long f in Fam.Members(k))
            {
                if (i >= rows * 2) break;
                float xx = x + (i / rows) * cw, yy = y + (i % rows) * 11f;
                s.Rect(xx, yy + 2f, 7f, 7f, Fam.Color(f));
                s.Text(xx + 10f, yy, cw - 12f, 11f, Fam.Name(f) + " (" + (t.Lands.ContainsKey(f) ? t.Lands[f].Count : 0) + ")", 6, TextAnchor.MiddleLeft, UiKit.Soft);
                i++;
            }
        }
    }
}
