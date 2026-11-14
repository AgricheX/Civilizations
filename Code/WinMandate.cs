using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class WinMandate : GovWindow<WinMandate>
    {
        public const string Id = "civ_mandate";

        protected override float Wd
        {
            get { return 400f; }
        }

        protected override float Ht
        {
            get { return 340f; }
        }

        protected override string Tab
        {
            get { return "mandate"; }
        }

        public static void Open(Kingdom k)
        {
            Show(Id, k, () => CreateAndInit(Id));
        }

        protected override string Sig(Kingdom k)
        {
            return base.Sig(k) + "|" + (int)Mandate.Get(k) + "|" + Mandate.Phase(k) + "|" + Mandate.Restoring(k) + "|" + Mandate.Rebelling(k);
        }

        private static readonly Color[] PhaseColors = { UiKit.Good, UiKit.Gold, UiKit.Cyan, UiKit.Bad };

        protected override void Draw(Sheet s, Kingdom k)
        {
            Title(s, k, Txt.T("civ_gov_tab_mandate"), null);
            float y = 28f;
            if (!GovEra.Has(k, GovEra.Mandate))
            {
                s.Locked(4f, y, s.W - 8f, 60f, GovEra.Mandate, Txt.T("civ_gov_tab_mandate"));
                return;
            }
            if (!Mandate.Applies(k))
            {
                s.Panel(4f, y, s.W - 8f, 40f, 0.6f);
                s.Text(8f, y, s.W - 16f, 40f, Txt.T(Diplomacy.Overlord(k) != null ? "civ_mand_na_vassal" : "civ_mand_na_small"), 8, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            int ph = Mandate.Phase(k);
            float L = 160f;
            Cycle(s, k, 4f, y, L, ph);
            float v = Mandate.Get(k);
            float rx = L + 10f, rw = s.W - rx - 4f;
            s.Header(rx, y, rw, Txt.T("civ_mand_title"), "mandate");
            float yy = y + 20f;
            s.Text(rx, yy, rw, 16f, ((int)v).ToString() + " / 100", 12, TextAnchor.MiddleCenter, v >= 50f ? UiKit.Good : UiKit.Bad);
            yy += 18f;
            s.Bar(rx + 2f, yy, rw - 4f, 10f, v / 100f, v >= 70f ? UiKit.Good : v >= 40f ? UiKit.Gold : UiKit.Bad);
            foreach (int t in new[] { 20, 40, 50, 70 }) s.Rect(rx + 2f + (rw - 4f) * t / 100f, yy - 2f, 1f, 14f, Color.white);
            yy += 12f;
            s.Text(rx, yy, rw, 10f, "20 · 40 · 50 · 70", 6, TextAnchor.MiddleCenter, UiKit.Dim);
            s.Hit(rx, yy - 14f, rw, 24f, null, Txt.T("civ_mand_thresholds"), Txt.T("civ_mand_thresholds_d"));
            yy += 12f;
            float des = Mandate.Deserved(k);
            s.Text(rx, yy, rw, 11f, Txt.F("civ_mand_deserved", (int)des), 7, TextAnchor.MiddleLeft, des >= v ? UiKit.Good : UiKit.Bad);
            yy += 11f;
            float atk = Mandate.AttackPct(k);
            s.Text(rx, yy, rw, 11f, Txt.F("civ_mand_army", (atk >= 0f ? "+" : "") + (int)atk), 7, TextAnchor.MiddleLeft, atk >= 0f ? UiKit.Good : UiKit.Bad);
            yy += 14f;
            s.Text(rx, yy, rw, 11f, Txt.T("civ_mand_factors"), 8, TextAnchor.MiddleLeft, UiKit.Gold);
            yy += 12f;
            Dictionary<string, float> parts = Mandate.Components(k);
            foreach (string p in Mandate.Parts)
            {
                float f;
                if (!parts.TryGetValue(p, out f) || Math.Abs(f) < 0.05f) continue;
                if (yy > s.H - 30f) break;
                s.Text(rx + 4f, yy, rw * 0.7f, 11f, Txt.T("civ_mand_part_" + p), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                s.Text(rx + rw * 0.7f, yy, rw * 0.3f - 4f, 11f, (f >= 0f ? "+" : "") + f.ToString("0.0"), 7, TextAnchor.MiddleRight, f >= 0f ? UiKit.Good : UiKit.Bad);
                s.Hit(rx, yy, rw, 11f, null, Txt.T("civ_mand_part_" + p), Txt.T("civ_mand_part_" + p + "_d"));
                yy += 11f;
            }
            string st = Mandate.Rebelling(k) ? Txt.T("civ_mand_rebelling") : Mandate.Restoring(k) ? Txt.T("civ_mand_restoring") : "";
            if (st.Length > 0) s.Text(4f, s.H - 40f, L, 14f, st, 8, TextAnchor.MiddleCenter, Mandate.Rebelling(k) ? UiKit.Bad : UiKit.Cyan);
            if (!GovKit.CanAct(k)) return;
            bool can = ph == 2 && !Mandate.Restoring(k);
            var b = s.Btn(4f, s.H - 19f, L, 17f, Txt.T("civ_mand_restore"), () =>
            {
                if (Mandate.Phase(k) == 2) Mandate.StartRestoration(k);
                Done(k);
            }, Txt.T("civ_mand_restore"), can ? Txt.T("civ_mand_restore_d") : Txt.T("civ_mand_restore_no"), can);
            if (!can && b.Text != null) b.Text.color = UiKit.Dim;
        }

        private static void Cycle(Sheet s, Kingdom k, float x, float y, float w, int ph)
        {
            s.Header(x, y, w, Txt.T("civ_mand_cycle"), "cycle");
            float cy = y + 20f + w / 2f, cx = x + w / 2f, r = w / 2f - 26f;
            s.Panel(x, y + 18f, w, w + 4f, 0.5f);
            for (int i = 0; i < 48; i++)
            {
                float a = Mathf.PI / 2f - i / 48f * Mathf.PI * 2f;
                int seg = (i + 6) / 12 % 4;
                s.Rect(cx + Mathf.Cos(a) * r - 1.5f, cy - Mathf.Sin(a) * r - 1.5f, 3f, 3f, seg == ph ? PhaseColors[seg] : GovKit.Dark(PhaseColors[seg], 0.4f));
            }
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI / 2f - i * Mathf.PI / 2f;
                float px = cx + Mathf.Cos(a) * r, py = cy - Mathf.Sin(a) * r;
                bool on = i == ph;
                float sz = on ? 30f : 22f;
                s.Panel(px - sz / 2f - 2f, py - sz / 2f - 2f, sz + 4f, sz + 4f, on ? 1f : 0.6f);
                s.Icon(px - sz / 2f, py - sz / 2f, sz, "ph_" + Mandate.Phases[i], on ? (Color?)null : UiKit.Dim);
                float ty = i == 2 ? py + sz / 2f + 2f : py - sz / 2f - 12f;
                if (i == 1 || i == 3) ty = py + sz / 2f + 2f;
                s.Text(px - 40f, ty, 80f, 10f, Mandate.PhaseName(i), on ? 8 : 6, TextAnchor.MiddleCenter, on ? PhaseColors[i] : UiKit.Dim);
                s.Hit(px - sz / 2f, py - sz / 2f, sz, sz, null, Mandate.PhaseName(i), Txt.T("civ_mand_phase_" + Mandate.Phases[i] + "_d"));
            }
            s.Text(cx - 40f, cy - 14f, 80f, 12f, Txt.F("civ_mand_dynasty", Mandate.Dynasty(k) + 1), 7, TextAnchor.MiddleCenter, UiKit.Gold);
            s.Text(cx - 40f, cy, 80f, 12f, Txt.F("civ_mand_years", Mandate.Years(k)), 7, TextAnchor.MiddleCenter, UiKit.Soft);
        }
    }
}
