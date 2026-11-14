using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal class WinParliament : AbstractWindow<WinParliament>
    {
        public const string Id = "civ_parliament";
        private static readonly string[] TabKeys = { "parl_tab_chamber", "parl_tab_bill", "parl_tab_laws", "parl_tab_plots" };
        private static readonly string[] TabIcons = { "ui/Icons/civ/gov/tab_chamber", "ui/Icons/civ/gov/tab_bill", "ui/Icons/civ/gov/tab_laws", "ui/Icons/civ/gov/tab_plots" };
        private static Kingdom _k;
        private static int _tab;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open(Kingdom k)
        {
            if (k != null) _k = k;
            GovUi.Ensure(Id, Create);
            UiKit.Open(Id);
        }

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
        }

        public override void OnNormalEnable()
        {
            Render();
        }

        private void Render()
        {
            Transform c = ContentTransform;
            UiKit.Clear(c);
            if (!G.Loaded) return;
            if (!G.Ok(_k)) _k = SelectedMetas.selected_kingdom;
            if (!G.Ok(_k) || !_k.isCiv())
            {
                UiKit.Label(c, Txt.T("state_none"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            Kingdom k = _k;
            RectTransform head = UiKit.Box(c, "Head", 30f);
            UiKit.Panel(head, 0.85f);
            UiKit.Banner(head, k, new Vector2(-UiKit.W / 2f + 16f, 0f), 24f);
            UiKit.Text(head, G.Name(k) + " · " + Gov.Council(k), new Vector2(10f, 6f), new Vector2(170f, 12f), 10, TextAnchor.MiddleLeft, UiKit.Gold);
            UiKit.Text(head, Laws.Regime(k) + " · " + Gov.RulerName(k), new Vector2(10f, -6f), new Vector2(170f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
            if (!Gov.On("civ_gov_parliament"))
            {
                UiKit.Label(c, Txt.T("parl_off"), 20f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            UiKit.Tabs(c, TabKeys, TabIcons, _tab, i =>
            {
                _tab = i;
                Render();
            });
            switch (_tab)
            {
                case 0:
                    Chamber(c, k);
                    break;
                case 1:
                    BillTab(c, k);
                    break;
                case 2:
                    LawsTab(c, k);
                    break;
                case 3:
                    PlotsTab(c, k);
                    break;
            }
        }

        private static List<long> Ordered(Kingdom k)
        {
            return Parliament.Seats(k).Keys.OrderBy(id => id == 0L ? 0f : Parliament.Stance(id)[0]).ToList();
        }

        private void Chamber(Transform c, Kingdom k)
        {
            var seats = Parliament.Seats(k);
            int total = seats.Values.Sum();
            if (Profile.Get(k).Era < 3) UiKit.Label(c, Txt.T("parl_early"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            if (total == 0)
            {
                UiKit.Label(c, Txt.T("parl_empty"), 20f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            Hemicycle(c, k, seats, total);
            UiKit.Header(c, Txt.T("parl_clans"), UiKit.Gold);
            long royal = Parliament.RoyalId(k);
            foreach (long id in seats.Keys.OrderByDescending(x => seats[x]).Take(14))
            {
                string name = Gov.ClanName(id) + (id == royal && id != 0L ? " ♛" : "");
                long bloc = Parliament.BlocOf(id);
                string right = seats[id] + " · " + (id == 0L ? "-" : Parliament.FactionOf(k, id));
                string tip = id == 0L ? Txt.T("civ_independents_d") : TieText(k, id, royal, bloc);
                GovUi.Row(c, name, right, null, Gov.ClanColor(id), () => { }, name, tip);
            }
            UiKit.Header(c, Txt.T("parl_stances"), UiKit.Gold);
            Chart(c, k, seats);
            Mandate(c, k);
        }

        private static string TieText(Kingdom k, long id, long royal, long bloc)
        {
            var sb = new System.Text.StringBuilder();
            float[] s = Parliament.Stance(id);
            sb.Append(Txt.F("parl_stance_d", G.Signed(s[0] * 100f), G.Signed(s[1] * 100f))).Append('\n');
            if (bloc != 0L) sb.Append(Gov.BlocTerm(k)).Append(": ").Append(Gov.ClanName(bloc)).Append('\n');
            if (royal != 0L && royal != id)
            {
                ClanTie t = Parliament.Tie(id, royal);
                sb.Append(Txt.F("parl_tie_d", (int)t.Total, (int)t.Politics, (int)t.Marriage, (int)t.Neighbors, (int)t.Memory));
            }
            List<City> dom;
            if (Parliament.Domains(k).TryGetValue(id, out dom)) sb.Append('\n').Append(string.Join(", ", dom.Take(6).Select(x => G.Name(x)).ToArray()));
            return sb.ToString();
        }

        private static void Hemicycle(Transform c, Kingdom k, Dictionary<long, int> seats, int total)
        {
            RectTransform box = UiKit.Box(c, "Hemi", 104f);
            UiKit.Panel(box, 0.6f);
            int per = Math.Max(1, (int)Math.Ceiling(total / 300f));
            int n = (int)Math.Ceiling(total / (float)per);
            int rows = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n / 3f)), 1, 9);
            float r0 = 28f, r1 = 92f;
            var radii = new List<float>();
            for (int i = 0; i < rows; i++) radii.Add(rows == 1 ? r1 : r0 + (r1 - r0) * i / (rows - 1));
            float sum = radii.Sum();
            var pos = new List<KeyValuePair<float, Vector2>>();
            int placed = 0;
            for (int i = 0; i < rows; i++)
            {
                int cnt = i == rows - 1 ? n - placed : Math.Max(1, Mathf.RoundToInt(n * radii[i] / sum));
                cnt = Math.Min(cnt, n - placed);
                for (int j = 0; j < cnt; j++)
                {
                    float a = cnt == 1 ? Mathf.PI / 2f : Mathf.PI - Mathf.PI * j / (cnt - 1);
                    pos.Add(new KeyValuePair<float, Vector2>(a, new Vector2(Mathf.Cos(a) * radii[i], Mathf.Sin(a) * radii[i] * 0.95f - 44f)));
                }
                placed += cnt;
            }
            pos = pos.OrderByDescending(p => p.Key).ToList();
            float size = Mathf.Clamp(150f / Mathf.Sqrt(n), 2.5f, 9f);
            int idx = 0;
            foreach (long id in Ordered(k))
            {
                Color col = Gov.ClanColor(id);
                int m = (int)Math.Ceiling(seats[id] / (float)per);
                for (int s = 0; s < m && idx < pos.Count; s++, idx++) GovUi.Square(box, pos[idx].Value, size, col);
            }
            UiKit.Text(box, total + " " + Txt.T("parl_seats") + (per > 1 ? " (×" + per + ")" : ""), new Vector2(0f, -44f), new Vector2(120f, 12f), 8, TextAnchor.MiddleCenter, UiKit.Soft);
        }

        private static void Chart(Transform c, Kingdom k, Dictionary<long, int> seats)
        {
            RectTransform box = UiKit.Box(c, "Chart", 110f);
            UiKit.Panel(box, 0.6f);
            int band = Gov.Band(k);
            float w = 170f, h = 90f;
            GovUi.Rect(box, Vector2.zero, new Vector2(w, 1f), new Color(1f, 1f, 1f, 0.3f));
            GovUi.Rect(box, Vector2.zero, new Vector2(1f, h), new Color(1f, 1f, 1f, 0.3f));
            UiKit.Text(box, Gov.Faction(band, 0), new Vector2(-w / 4f, -h / 2f + 6f), new Vector2(w / 2f, 10f), 6, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Text(box, Gov.Faction(band, 1), new Vector2(-w / 4f, h / 2f - 6f), new Vector2(w / 2f, 10f), 6, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Text(box, Gov.Faction(band, 2), new Vector2(w / 4f, -h / 2f + 6f), new Vector2(w / 2f, 10f), 6, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Text(box, Gov.Faction(band, 3), new Vector2(w / 4f, h / 2f - 6f), new Vector2(w / 2f, 10f), 6, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Text(box, Txt.T("parl_axis_x"), new Vector2(0f, -h / 2f - 6f), new Vector2(w, 9f), 6, TextAnchor.MiddleCenter, UiKit.Soft);
            UiKit.Text(box, Txt.T("parl_axis_y"), new Vector2(-w / 2f - 12f, 0f), new Vector2(30f, 40f), 6, TextAnchor.MiddleCenter, UiKit.Soft);
            int max = Math.Max(1, seats.Values.Max());
            foreach (var kv in seats)
            {
                if (kv.Key == 0L) continue;
                float[] s = Parliament.Stance(kv.Key);
                float size = 3f + 6f * kv.Value / max;
                GovUi.Square(box, new Vector2(s[0] * w / 2f, s[1] * h / 2f), size, Gov.ClanColor(kv.Key));
            }
            float[] g = Laws.Pos("gov", Policies.Id(k, "gov"));
            GovUi.Square(box, new Vector2(g[0] * w / 2f, g[1] * h / 2f), 4f, new Color(1f, 1f, 1f, 0.9f));
        }

        private static void Mandate(Transform c, Kingdom k)
        {
            if (!Gov.On("civ_gov_cycle")) return;
            int ph = Statecraft.Phase(k);
            UiKit.Header(c, Txt.T("parl_mandate") + (ph >= 0 ? " · " + Txt.T("civ_phase_" + Statecraft.Phases[ph]) : ""), UiKit.Gold);
            float m = Civilizations.Mandate.Get(k);
            UiKit.Bar(UiKit.Box(c, "Mand", 14f), m / 100f, m >= 50f ? new Color(0.5f, 0.85f, 0.45f) : m >= 25f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.4f, 0.35f), new Vector2(UiKit.W - 20f, 12f), Vector2.zero, ((int)m) + " / 100 · " + Txt.F("parl_target", (int)Civilizations.Mandate.Target(k)));
            foreach (var kv in Civilizations.Mandate.Components(k))
            {
                if (Math.Abs(kv.Value) < 0.5f) continue;
                GovUi.Row(c, Txt.T("civ_mc_" + kv.Key), G.Signed(kv.Value), kv.Value >= 0 ? UiKit.Good : UiKit.Bad);
            }
            GovUi.Row(c, Txt.T("parl_dynasty"), Civilizations.Mandate.Dynasty(k) + " · " + Civilizations.Mandate.Years(k) + " " + Txt.T("parl_years"));
        }

        private void BillTab(Transform c, Kingdom k)
        {
            List<BillItem> bill = Parliament.Bill(k);
            long by = Parliament.Proposer(k);
            bool act = GovUi.CanAct(k);
            if (bill.Count == 0)
            {
                int ch = Parliament.Charter(k);
                string txt = ch > G.Year ? Txt.F("parl_charter_until", ch) : Txt.F("parl_next_bill", Math.Max(G.Year, Parliament.NextBill(k)));
                UiKit.Label(c, txt, 18f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
                if (act && Profile.Get(k).Era >= 3)
                {
                    var b0 = new List<KeyValuePair<string, Action>>
                    {
                        new KeyValuePair<string, Action>("parl_btn_propose", () => Parliament.ProposeNow(k)),
                        new KeyValuePair<string, Action>("parl_btn_charter", () => Parliament.LockCharter(k, 10))
                    };
                    GovUi.Buttons(c, b0, Render);
                }
                return;
            }
            UiKit.Header(c, Txt.F("parl_bill_by", Gov.ClanName(by), Parliament.BillYear(k)), UiKit.Gold);
            var seats = Parliament.Seats(k);
            var order = Ordered(k);
            foreach (BillItem it in bill)
            {
                int[] t = Parliament.Tally(k, it, by);
                string line = Laws.GroupName(it.Group) + ": " + Laws.Name(it.Group, it.Old) + " → " + Laws.Name(it.Group, it.Id);
                UiKit.Paragraph(c, line, 8, UiKit.Soft);
                RectTransform box = UiKit.Box(c, "Votes", 16f);
                int all = Math.Max(1, t[0] + t[1]);
                UiKit.Bar(box, t[0] / (float)all, t[0] > t[1] ? new Color(0.5f, 0.85f, 0.45f) : new Color(0.95f, 0.45f, 0.4f), new Vector2(UiKit.W - 20f, 12f), Vector2.zero, Txt.F("parl_tally", t[0], t[1]));
                RectTransform row = UiKit.Box(c, "Clans", 10f);
                float x = -UiKit.W / 2f + 8f;
                foreach (long id in order)
                {
                    if (x > UiKit.W / 2f - 8f) break;
                    bool yes = Parliament.VoteOf(k, id, it, by);
                    GovUi.Square(row, new Vector2(x, 2f), 6f, Gov.ClanColor(id));
                    GovUi.Square(row, new Vector2(x, -3f), 3f, yes ? UiKit.Good : UiKit.Bad);
                    x += 9f;
                }
            }
            if (act)
            {
                var b = new List<KeyValuePair<string, Action>>
                {
                    new KeyValuePair<string, Action>("parl_btn_vote", () => Parliament.VoteNow(k)),
                    new KeyValuePair<string, Action>("parl_btn_decree", () => Parliament.Decree(k)),
                    new KeyValuePair<string, Action>("parl_btn_withdraw", () => Parliament.Withdraw(k)),
                    new KeyValuePair<string, Action>("parl_btn_charter", () => Parliament.LockCharter(k, 10))
                };
                GovUi.Buttons(c, b, Render);
            }
        }

        private void LawsTab(Transform c, Kingdom k)
        {
            bool act = GovUi.CanAct(k);
            UiKit.Header(c, Txt.T("parl_laws"), UiKit.Gold);
            foreach (string g in Laws.Groups)
            {
                string cur = Laws.Get(k, g);
                string group = g;
                List<LawDef> opts = Laws.Options(k, g);
                Action click = null;
                if (act && opts.Count > 1)
                {
                    click = () =>
                    {
                        int i = opts.FindIndex(o => o.Id == Laws.Get(k, group));
                        LawDef next = opts[(i + 1) % opts.Count];
                        Laws.Set(k, group, next.Id, "player");
                        Render();
                    };
                }
                LawDef d = Laws.Law(cur);
                string tip = Txt.T("civ_law_" + cur + "_d") + (d != null && d.Eff.Count > 0 ? "\n\n" + TechInfo.Effects(d.Eff) : "") + (Laws.Locked(k, g) ? "\n\n" + Txt.T("parl_locked") : "");
                GovUi.Row(c, Laws.GroupName(g), Laws.Name(g, cur) + (Laws.Locked(k, g) ? " •" : ""), Laws.Locked(k, g) ? UiKit.Cyan : UiKit.Gold, null, click, Laws.Name(g, cur), tip);
            }
            UiKit.Header(c, Txt.T("parl_policies"), UiKit.Gold);
            foreach (string g in Policies.Groups)
            {
                string cur = Policies.Id(k, g);
                GovUi.Row(c, Txt.T("civ_pg_" + g), Txt.T("civ_p_" + cur), Policies.Locked(k, g) ? UiKit.Cyan : UiKit.Soft);
            }
            if (act)
            {
                var b = new List<KeyValuePair<string, Action>>
                {
                    new KeyValuePair<string, Action>("parl_btn_unlock", () => { foreach (string g in Laws.Groups) Laws.Unlock(k, g); })
                };
                GovUi.Buttons(c, b, Render);
            }
        }

        private void PlotsTab(Transform c, Kingdom k)
        {
            UiKit.Header(c, Txt.T("parl_blocs"), UiKit.Gold);
            var leaders = Parliament.Leaders(k);
            if (leaders.Count == 0) UiKit.Label(c, Txt.T("parl_no_blocs"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            var seats = Parliament.Seats(k);
            foreach (long l in leaders)
            {
                var ms = Parliament.Members(k, l);
                int n = ms.Sum(x => seats.ContainsKey(x) ? seats[x] : 0);
                GovUi.Row(c, Gov.BlocTerm(k) + " " + Gov.ClanName(l), ms.Count + " · " + n + " " + Txt.T("parl_seats"), UiKit.Soft, Gov.ClanColor(l), null);
            }
            UiKit.Header(c, Txt.T("parl_plots"), UiKit.Gold);
            List<GovPlot> plots = Plots.Of(k);
            if (plots.Count == 0) UiKit.Label(c, Txt.T("parl_no_plots"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            foreach (GovPlot p in plots)
            {
                string left = Txt.T("civ_plot_" + p.Type) + " · " + Plots.ActorName(p.Type, p.Actor);
                string tip = Txt.F("parl_plot_d", Plots.TargetName(p), Txt.T("civ_motive_" + p.Motive), p.Start, p.Start + p.Need);
                GovUi.Row(c, left, G.Pct(p.Progress), UiKit.Gold, null, () => { }, left, tip);
            }
        }
    }
}
