using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal class WinTreasury : AbstractWindow<WinTreasury>
    {
        public const string Id = "civ_treasury";
        private static readonly string[] TabKeys = { "tr_tab_flow", "tr_tab_ledger", "tr_tab_armies" };
        private static readonly string[] TabIcons = { "ui/Icons/civ/gov/tab_flow", "ui/Icons/civ/gov/tab_ledger", "ui/Icons/civ/gov/tab_armies" };
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
            UiKit.Text(head, G.Name(k), new Vector2(10f, 6f), new Vector2(170f, 12f), 10, TextAnchor.MiddleLeft, UiKit.Gold);
            string sub = Txt.F("tr_reserve", (int)Treasury.Reserve(k)) + (Treasury.Unpaid(k) > 0 ? " · " + Txt.T("tr_unpaid") : "");
            UiKit.Text(head, sub, new Vector2(10f, -6f), new Vector2(170f, 10f), 7, TextAnchor.MiddleLeft, Treasury.Unpaid(k) > 0 ? UiKit.Bad : UiKit.Soft);
            if (!Gov.On("civ_gov_treasury"))
            {
                UiKit.Label(c, Txt.T("tr_off"), 20f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
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
                    Flow(c, k);
                    break;
                case 1:
                    LedgerTab(c, k);
                    break;
                case 2:
                    Armies(c, k);
                    break;
            }
        }

        private void Flow(Transform c, Kingdom k)
        {
            Dictionary<string, float> f = Treasury.Flow(k);
            float inc = Treasury.Sources.Sum(s => Math.Max(0f, Store.Get(f, s)));
            float use = Treasury.Uses.Sum(s => Math.Abs(Math.Min(0f, Store.Get(f, s))));
            float max = Math.Max(1f, Math.Max(inc, use));
            UiKit.Header(c, Txt.F("tr_income", (int)inc), UiKit.Good);
            foreach (string s in Treasury.Sources)
            {
                float v = Store.Get(f, s);
                UiKit.Bar(UiKit.Box(c, "Src", 13f), v / max, new Color(0.5f, 0.85f, 0.45f), new Vector2(UiKit.W - 20f, 11f), Vector2.zero, Txt.T("tr_src_" + s) + " " + (int)v);
            }
            UiKit.Header(c, Txt.F("tr_spent", (int)use), UiKit.Bad);
            foreach (string s in Treasury.Uses)
            {
                float v = -Store.Get(f, s);
                UiKit.Bar(UiKit.Box(c, "Use", 13f), v / max, new Color(0.95f, 0.5f, 0.4f), new Vector2(UiKit.W - 20f, 11f), Vector2.zero, Txt.T("tr_use_" + s) + " " + (int)v);
            }
            UiKit.Header(c, Txt.T("tr_laws"), UiKit.Gold);
            GovUi.Row(c, Laws.GroupName("taxation"), Laws.Name("taxation", Laws.Get(k, "taxation")) + " · " + G.Pct(Treasury.TaxRate(k)), UiKit.Gold);
            GovUi.Row(c, Laws.GroupName("fiscal"), Laws.Name("fiscal", Laws.Get(k, "fiscal")), UiKit.Gold);
            GovUi.Row(c, Txt.T("tr_pay"), Treasury.Pay(k).ToString("0.##") + " × " + k.countTotalWarriors(), UiKit.Soft);
            WorksCount w = Treasury.Works(k);
            UiKit.Header(c, Txt.T("tr_works"), UiKit.Gold);
            GovUi.Row(c, Txt.T("tr_w_market"), w.Market.ToString(), UiKit.Soft);
            GovUi.Row(c, Txt.T("tr_w_temple"), w.Temple.ToString(), UiKit.Soft);
            GovUi.Row(c, Txt.T("tr_w_barracks"), w.Barracks.ToString(), UiKit.Soft);
            GovUi.Row(c, Txt.T("tr_w_tower"), w.Tower.ToString(), UiKit.Soft);
            GovUi.Row(c, Txt.T("tr_w_statue"), w.Statue.ToString(), UiKit.Soft);
            GovUi.Row(c, Txt.T("tr_w_windmill"), w.Windmill.ToString(), UiKit.Soft);
        }

        private void LedgerTab(Transform c, Kingdom k)
        {
            UiKit.Header(c, Txt.T("tr_ledger"), UiKit.Gold);
            List<string[]> l = Treasury.Ledger(k);
            if (l.Count == 0) UiKit.Label(c, Txt.T("tr_no_ledger"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            foreach (string[] e in l)
            {
                bool inc = e[1] == "income";
                GovUi.Row(c, e[0] + " · " + Txt.T("tr_l_" + e[1]), (inc ? "+" : "-") + e[2], inc ? UiKit.Good : UiKit.Bad);
            }
            float[] old = Ledger.Last(k);
            if (old == null) return;
            UiKit.Header(c, Txt.T("tr_flows"), UiKit.Gold);
            for (int i = 0; i < Ledger.Flows.Length; i++)
            {
                float a = old[i * 2], b = old[i * 2 + 1];
                if (a < 1f && b < 1f) continue;
                GovUi.Row(c, Txt.T("civ_flow_" + Ledger.Flows[i]), "+" + (int)a + " / -" + (int)b, UiKit.Soft);
            }
        }

        private void Armies(Transform c, Kingdom k)
        {
            bool act = GovUi.CanAct(k);
            UiKit.Header(c, Txt.F("tr_tier", Txt.T("civ_armtier_" + ArmyCommand.Tier(k)), ArmyCommand.Tier(k), ArmyCommand.MaxTier(k)), UiKit.Gold);
            if (ArmyCommand.Tier(k) < ArmyCommand.MaxTier(k)) GovUi.Row(c, Txt.T("tr_next_tier"), ((int)ArmyCommand.NextCost(k)).ToString(), UiKit.Soft);
            UiKit.Header(c, Txt.T("tr_missions"), UiKit.Gold);
            foreach (City ct in G.Cities(k).OrderByDescending(x => ArmyCommand.Guard(x) ? 1 : 0).ThenByDescending(x => x.getPopulationPeople()).Take(16))
            {
                City cc = ct;
                string m = ArmyCommand.Mission(ct);
                Action click = null;
                if (act) click = () =>
                {
                    int i = Array.IndexOf(ArmyCommand.Missions, ArmyCommand.Mission(cc));
                    ArmyCommand.SetMission(cc, ArmyCommand.Missions[(i + 1) % ArmyCommand.Missions.Length], true);
                    Render();
                };
                int army = ct.army != null ? ct.army.countUnits() : 0;
                string left = G.Name(ct) + (ArmyCommand.Guard(ct) ? " · " + Txt.T("tr_guard") : "");
                GovUi.Row(c, left, Txt.T("civ_mission_" + m) + " · " + army, UiKit.Gold, null, click, Txt.T("civ_mission_" + m), Txt.T("civ_mission_" + m + "_d"));
            }
            UiKit.Paragraph(c, Txt.T("tr_orders_help"), 7, UiKit.Dim);
        }
    }
}
