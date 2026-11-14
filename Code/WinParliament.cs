using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class WinParliament : GovWindow<WinParliament>
    {
        public const string Id = "civ_parliament";
        private static int _tab;

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
            get { return "parliament"; }
        }

        public static void Open(Kingdom k)
        {
            Show(Id, k, () => CreateAndInit(Id));
        }

        protected override string Sig(Kingdom k)
        {
            Bill b = Parl.Current(k);
            string pol = string.Join(",", Parl.Defs.Select(d => Parl.Get(k, d.Id).ToString()).ToArray());
            return base.Sig(k) + "|" + (b != null ? b.Pol + b.Opt + b.Due : "") + "|" + pol + "|" + Fam.Seats(k).Count + "|" + _tab;
        }

        private static Color Shade(Kingdom k, long f)
        {
            if (GovEra.Has(k, GovEra.Blocs)) return Fam.Color(Fam.Bloc(k, f));
            if (GovEra.Has(k, GovEra.Bills)) return f == 0L ? UiKit.Dim : Fam.FactionColor(Fam.Faction(f));
            return Fam.Color(f);
        }

        protected override void Draw(Sheet s, Kingdom k)
        {
            Title(s, k, Gov.FormName(k) + " · " + Parl.Regime(k), Txt.T("civ_parl_regime_d"));
            float L = Mathf.Floor(s.W * 0.5f);
            Left(s.Sub(0f, 28f, L, s.H - 28f), k);
            Right(s.Sub(L, 28f, s.W - L, s.H - 28f), k);
        }

        private static void Left(Sheet s, Kingdom k)
        {
            int tier = GovEra.Tier(k);
            s.Header(4f, 0f, s.W - 8f, Gov.Council(k), "tab_parliament");
            SeatTable t = Fam.Table(k);
            List<long> fams = Fam.Members(k);
            var blocks = new List<KeyValuePair<Color, int>>();
            int total;
            if (tier == 0)
            {
                foreach (long f in fams) blocks.Add(new KeyValuePair<Color, int>(Fam.Color(f), 1));
                total = fams.Count;
            }
            else
            {
                foreach (var g in fams.GroupBy(f => Shade(k, f)).OrderByDescending(g => g.Sum(f => t.Seats[f]))) blocks.Add(new KeyValuePair<Color, int>(g.Key, g.Sum(f => t.Seats[f])));
                total = t.Total;
            }
            s.Panel(4f, 18f, s.W - 8f, 112f, 0.5f);
            GovKit.Hemicycle(s, 8f, 22f, s.W - 16f, 104f, blocks, total);
            s.Text(4f, 112f, s.W - 8f, 14f, total + " " + Txt.T(tier == 0 ? "civ_parl_elders_count" : "civ_parl_seats"), 9, TextAnchor.MiddleCenter, UiKit.Gold);
            s.Text(4f, 132f, s.W - 8f, 12f, Largest(k, t, fams, tier), 8, TextAnchor.MiddleCenter, UiKit.Soft);
            bool axes = GovEra.Has(k, GovEra.Blocs);
            float lw = s.W - 8f - 96f;
            float y = 148f;
            s.Text(6f, y, lw, 11f, Txt.T("civ_parl_families"), 8, TextAnchor.MiddleLeft, UiKit.Gold);
            y += 12f;
            int rows = Math.Max(1, (int)((s.H - y - 2f) / 13f));
            foreach (long f in fams.Take(rows))
            {
                s.Rect(6f, y + 2f, 8f, 8f, Fam.Color(f));
                s.Text(17f, y, lw * 0.45f, 12f, Fam.Name(f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                string grp = tier >= 2 && f != 0L ? (axes && Fam.Bloc(k, f) != f ? Fam.Name(Fam.Bloc(k, f)) : Fam.FactionName(k, Fam.Faction(f))) : "";
                s.Text(17f + lw * 0.45f, y, lw * 0.35f, 12f, grp, 6, TextAnchor.MiddleLeft, tier >= 2 ? Shade(k, f) : UiKit.Dim);
                s.Text(lw - 30f, y, 34f, 12f, tier == 0 ? "" : t.Seats[f].ToString(), 7, TextAnchor.MiddleRight, UiKit.Gold);
                y += 13f;
            }
            float mx = s.W - 98f, my = 148f, ms = Math.Min(92f, s.H - my - 2f);
            if (axes) Axes(s, k, fams, mx, my, ms);
            else s.Locked(mx, my, 92f, ms, GovEra.Blocs, Txt.T("civ_parl_axes"));
        }

        private static string Largest(Kingdom k, SeatTable t, List<long> fams, int tier)
        {
            if (fams.Count == 0) return Txt.T("civ_parl_empty");
            if (tier < 2) return Txt.F("civ_parl_largest", Fam.Name(fams[0]), t.Seats[fams[0]]);
            var by = fams.Where(f => f != 0L).GroupBy(f => GovEra.Has(k, GovEra.Blocs) ? Fam.Name(Fam.Bloc(k, f)) : Fam.FactionName(k, Fam.Faction(f))).Select(g => new KeyValuePair<string, int>(g.Key, g.Sum(f => t.Seats[f]))).OrderByDescending(x => x.Value).FirstOrDefault();
            if (by.Key == null) return Txt.F("civ_parl_largest", Fam.Name(fams[0]), t.Seats[fams[0]]);
            return Txt.F("civ_parl_largest", by.Key, by.Value);
        }

        private static void Axes(Sheet s, Kingdom k, List<long> fams, float x, float y, float sz)
        {
            s.Panel(x, y, 92f, sz, 0.6f);
            float cx = x + 46f, cy = y + sz / 2f;
            s.Rect(x + 4f, cy, 84f, 1f, UiKit.Dim);
            s.Rect(cx, y + 4f, 1f, sz - 8f, UiKit.Dim);
            s.Text(x + 2f, y + 1f, 88f, 9f, Txt.T("civ_parl_ax_radical"), 6, TextAnchor.UpperCenter, UiKit.Dim);
            s.Text(x + 2f, y + sz - 10f, 88f, 9f, Txt.T("civ_parl_ax_moderate"), 6, TextAnchor.LowerCenter, UiKit.Dim);
            s.Text(x + 2f, cy - 9f, 40f, 9f, Txt.T("civ_parl_ax_local"), 6, TextAnchor.MiddleLeft, UiKit.Dim);
            s.Text(x + 50f, cy - 9f, 40f, 9f, Txt.T("civ_parl_ax_central"), 6, TextAnchor.MiddleRight, UiKit.Dim);
            var seats = Fam.Seats(k);
            int max = Math.Max(1, seats.Values.DefaultIfEmpty(1).Max());
            foreach (long f in fams)
            {
                if (f == 0L) continue;
                Vector2 st = Fam.Stance(f);
                float d = 3f + 5f * seats[f] / max;
                float px = cx + st.x * 40f - d / 2f, py = cy - st.y * (sz / 2f - 6f) - d / 2f;
                s.Rect(px - 0.5f, py - 0.5f, d + 1f, d + 1f, GovKit.Dark(Fam.Color(Fam.Bloc(k, f)), 0.3f));
                s.Rect(px, py, d, d, Fam.Color(f));
                s.Hit(px - 1f, py - 1f, d + 2f, d + 2f, null, Fam.Name(f), Fam.FactionName(k, Fam.Faction(f)) + "\n" + Txt.T("civ_parl_bloc") + ": " + Fam.Name(Fam.Bloc(k, f)));
            }
        }

        private static void Right(Sheet s, Kingdom k)
        {
            s.Header(4f, 0f, s.W - 8f, Txt.T("civ_parl_policies"), "policies");
            float tw = (s.W - 8f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int ti = i;
                bool open = GovEra.Has(k, Parl.TabEra[i]);
                var b = s.Btn(4f + i * tw, 18f, tw - 2f, 16f, Txt.T("civ_parl_tab_" + Parl.Tabs[i]), () =>
                {
                    _tab = ti;
                    Redraw();
                }, Txt.T("civ_parl_tab_" + Parl.Tabs[i]), open ? null : GovEra.Note(Parl.TabEra[i]), _tab == i);
                if (!open && b.Text != null) b.Text.color = UiKit.Dim;
            }
            float y = 38f;
            if (!GovEra.Has(k, Parl.TabEra[_tab]))
            {
                s.Locked(4f, y, s.W - 8f, 104f, Parl.TabEra[_tab], Txt.T("civ_parl_tab_" + Parl.Tabs[_tab]));
                y += 108f;
            }
            else
            {
                bool act = GovKit.CanAct(k);
                foreach (PolDef d in Parl.Defs.Where(x => x.Tab == _tab))
                {
                    Policy(s, k, d, y, act);
                    y += 18f;
                }
                y = Math.Max(y, 38f + 18f * 4f) + 4f;
            }
            BillPanel(s.Sub(0f, y, s.W, s.H - y), k);
        }

        private static void Policy(Sheet s, Kingdom k, PolDef d, float y, bool act)
        {
            bool av = Parl.Available(k, d);
            s.Panel(4f, y, s.W - 8f, 17f, av ? 0.7f : 0.35f);
            s.Icon(6f, y + 1.5f, 14f, "p_" + d.Id, av ? (Color?)null : UiKit.Dim);
            s.Text(23f, y, s.W * 0.4f - 20f, 17f, Parl.PolName(d.Id), 8, TextAnchor.MiddleLeft, av ? UiKit.Soft : UiKit.Dim);
            s.Hit(23f, y, s.W * 0.4f - 20f, 17f, null, Parl.PolName(d.Id), Txt.T("civ_parl_pol_" + d.Id + "_d"));
            float sx = s.W * 0.4f + 4f, sw = s.W - sx - 6f;
            if (!av)
            {
                string why = !GovEra.Has(k, d.Era) ? GovEra.Note(d.Era) : Txt.T(d.Need == "lord" ? "civ_parl_need_lord" : "civ_parl_need_vassal");
                s.Text(sx, y, sw, 17f, why, 6, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            int cur = Parl.Get(k, d.Id);
            Bill b = Parl.Current(k);
            bool pending = b != null && b.Pol == d.Id;
            int shown = pending ? b.Opt : cur;
            s.Text(sx + 16f, y, sw - 32f, 17f, Parl.OptName(d.Id, shown) + (pending ? " *" : ""), 8, TextAnchor.MiddleCenter, pending ? UiKit.Cyan : UiKit.Gold);
            s.Hit(sx + 16f, y, sw - 32f, 17f, null, Parl.OptName(d.Id, shown), Txt.T("civ_parl_opt_" + d.Id + "_" + d.Opts[shown] + "_d") + (pending ? "\n" + Txt.T("civ_parl_pending") : ""));
            if (!act) return;
            s.Btn(sx, y + 1f, 15f, 15f, "<", () => Change(k, d, shown, -1));
            s.Btn(sx + sw - 15f, y + 1f, 15f, 15f, ">", () => Change(k, d, shown, 1));
        }

        private static void Change(Kingdom k, PolDef d, int from, int dir)
        {
            int to = from + dir;
            while (to >= 0 && to < d.Opts.Length && !Parl.OptOk(k, d, to)) to += dir;
            if (to < 0 || to >= d.Opts.Length) return;
            if (Parl.Voting(k)) Parl.Propose(k, d.Id, to, Fam.Royal(k), 2);
            else Parl.Set(k, d.Id, to, "ruler");
            Done(k);
        }

        private static void BillPanel(Sheet s, Kingdom k)
        {
            if (s.H < 30f) return;
            s.Header(4f, 0f, s.W - 8f, Txt.T(Parl.Voting(k) ? "civ_parl_bill" : "civ_parl_mood"), Parl.Voting(k) ? "bill" : "mood");
            var seats = Fam.Seats(k);
            if (!Parl.Voting(k))
            {
                float mood = 0f;
                int n = 0;
                foreach (var kv in seats)
                {
                    mood += Parl.Mood(k, kv.Key) * Math.Max(1, kv.Value);
                    n += Math.Max(1, kv.Value);
                }
                mood = n > 0 ? mood / n : 0.6f;
                s.Text(6f, 18f, s.W - 12f, 12f, Txt.T(GovEra.Has(k, GovEra.Nobles) ? "civ_parl_king_decides" : "civ_parl_elders_decide"), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                s.Bar(6f, 32f, s.W - 12f, 10f, mood, mood >= 0.5f ? UiKit.Good : UiKit.Bad, Txt.T("civ_parl_mood") + " " + (int)(mood * 100f) + "%");
                float y = 46f;
                foreach (long f in Fam.Members(k).Take(Math.Max(0, (int)((s.H - 46f - 44f) / 12f))))
                {
                    float m = Parl.Mood(k, f);
                    s.Rect(6f, y + 2f, 7f, 7f, Fam.Color(f));
                    s.Text(16f, y, s.W * 0.5f, 11f, Fam.Name(f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                    s.Bar(s.W * 0.55f, y + 2f, s.W * 0.45f - 8f, 7f, m, m >= 0.5f ? UiKit.Good : UiKit.Bad);
                    y += 12f;
                }
                s.Locked(4f, s.H - 40f, s.W - 8f, 38f, GovEra.Bills, Txt.T("civ_parl_bills_locked"));
                return;
            }
            Bill b = Parl.Current(k);
            if (b == null)
            {
                s.Text(6f, 22f, s.W - 12f, 30f, Txt.T("civ_parl_no_bill"), 8, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            VoteResult r = Parl.Forecast(k, b.Pol, b.Opt, b.By);
            s.Text(6f, 17f, s.W - 12f, 12f, Parl.BillText(k, b), 8, TextAnchor.MiddleCenter, UiKit.Gold);
            int left = Math.Max(0, b.Due - G.Year);
            s.Text(6f, 29f, s.W - 12f, 10f, Txt.F("civ_parl_by", Fam.Name(b.By)) + " · " + Txt.F("civ_parl_due", left), 7, TextAnchor.MiddleCenter, UiKit.Soft);
            float bw = s.W - 12f;
            float yes = r.Total > 0 ? bw * r.Yes / r.Total : 0f;
            s.Rect(6f, 41f, bw, 9f, GovKit.Dark(UiKit.Bad, 0.7f));
            if (yes > 0f) s.Rect(6f, 41f, yes, 9f, GovKit.Dark(UiKit.Good, 0.8f));
            float need = b.Pol == "regime" ? 0.75f : 0.5f;
            s.Rect(6f + bw * need, 39f, 1f, 13f, UiKit.Gold);
            s.Text(8f, 40f, bw / 2f, 11f, Txt.T("civ_parl_yes") + " " + r.Yes, 7, TextAnchor.MiddleLeft, Color.white);
            s.Text(6f + bw / 2f, 40f, bw / 2f - 2f, 11f, r.No + " " + Txt.T("civ_parl_no"), 7, TextAnchor.MiddleRight, Color.white);
            float y2 = 54f;
            int rows = Math.Max(0, (int)((s.H - y2 - 22f) / 11f));
            var order = r.Votes.Keys.OrderByDescending(f => seats.ContainsKey(f) ? seats[f] : 0).ToList();
            int cols = order.Count > rows ? 2 : 1;
            float cw = (s.W - 12f) / cols;
            for (int i = 0; i < order.Count && i < rows * cols; i++)
            {
                long f = order[i];
                float x = 6f + (i / Math.Max(1, rows)) * cw, yy = y2 + (i % Math.Max(1, rows)) * 11f;
                s.Rect(x, yy + 2f, 7f, 7f, Fam.Color(f));
                s.Text(x + 10f, yy, cw * 0.5f, 11f, Fam.Name(f), 6, TextAnchor.MiddleLeft, UiKit.Soft);
                s.Icon(x + cw * 0.5f + 10f, yy + 1f, 9f, r.Votes[f] ? "vote_yes" : "vote_no", r.Votes[f] ? UiKit.Good : UiKit.Bad);
                float m = r.Mood.ContainsKey(f) ? r.Mood[f] : 0.5f;
                s.Bar(x + cw * 0.5f + 22f, yy + 3f, cw * 0.5f - 28f, 6f, m, m >= 0.5f ? UiKit.Good : UiKit.Bad);
            }
            if (!GovKit.CanAct(k)) return;
            float w3 = (s.W - 12f) / 3f;
            s.Btn(6f, s.H - 19f, w3 - 2f, 17f, Txt.T("civ_parl_vote_now"), () =>
            {
                Parl.VoteNow(k);
                Done(k);
            }, Txt.T("civ_parl_vote_now"), Txt.T("civ_parl_vote_now_d"), true);
            if (!Gov.Republic(k)) s.Btn(6f + w3, s.H - 19f, w3 - 2f, 17f, Txt.T("civ_parl_decree"), () =>
            {
                Parl.Decree(k);
                Done(k);
            }, Txt.T("civ_parl_decree"), Txt.T("civ_parl_decree_d"));
            s.Btn(6f + w3 * 2f, s.H - 19f, w3 - 2f, 17f, Txt.T("civ_parl_withdraw"), () =>
            {
                Parl.Withdraw(k, true);
                Done(k);
            }, Txt.T("civ_parl_withdraw"), Txt.T("civ_parl_withdraw_d"));
        }
    }
}
