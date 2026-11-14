using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal class WinElections : AbstractWindow<WinElections>
    {
        public const string Id = "civ_elections";
        private static readonly string[] TabKeys = { "elect_tab_power", "elect_tab_offices", "elect_tab_domains", "elect_tab_vassals" };
        private static readonly string[] TabIcons = { "ui/Icons/civ/gov/tab_power", "ui/Icons/civ/gov/tab_offices", "ui/Icons/civ/gov/tab_domains", "ui/Icons/civ/gov/tab_vassals" };
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
            UiKit.Text(head, Gov.RulerName(k), new Vector2(10f, -6f), new Vector2(170f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
            UiKit.Tabs(c, TabKeys, TabIcons, _tab, i =>
            {
                _tab = i;
                Render();
            });
            switch (_tab)
            {
                case 0:
                    Power(c, k);
                    break;
                case 1:
                    Offices(c, k);
                    break;
                case 2:
                    Domains(c, k);
                    break;
                case 3:
                    VassalTab(c, k);
                    break;
            }
        }

        private static string Who(Actor a)
        {
            if (!G.Ok(a)) return "-";
            return a.getName() + (a.clan != null && a.clan.data != null ? " (" + a.clan.data.name + ")" : "");
        }

        private static void Person(Transform c, Actor a, string right, Color? col = null)
        {
            if (!G.Ok(a)) return;
            Actor t = a;
            GovUi.Row(c, Who(a), right, col, a.clan != null ? Gov.ClanColor(Gov.Id(a.clan)) : (Color?)null, () => { if (G.Ok(t)) ActionLibrary.openUnitWindow(t); });
        }

        private void Power(Transform c, Kingdom k)
        {
            PolicyDef gov = Policies.Get(k, "gov");
            bool elective = gov != null && gov.Elections;
            if (elective)
            {
                int next = Store.Int(k.data, Politics.KElect, 0);
                UiKit.Header(c, Txt.F("elect_next", next > 0 ? next.ToString() : "-"), UiKit.Gold);
                List<Actor> cands = Politics.Candidates(k);
                if (cands.Count == 0) UiKit.Label(c, Txt.T("elect_no_cands"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
                foreach (Actor a in cands)
                {
                    Opinion o = Politics.Get(a);
                    string right = o != null ? Txt.T("civ_p_" + o.Ideo) : "-";
                    if (a.isKing()) right = Txt.T("elect_incumbent") + " · " + right;
                    Person(c, a, right, a.isKing() ? UiKit.Gold : UiKit.Soft);
                }
            }
            else
            {
                UiKit.Header(c, Txt.T("elect_succession"), UiKit.Gold);
                List<Actor> line = Succession(k);
                if (line.Count == 0) UiKit.Label(c, Txt.T("elect_no_heirs"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
                int i = 1;
                foreach (Actor a in line) Person(c, a, "#" + (i++) + " · " + a.getAge(), UiKit.Soft);
            }
            int ph = Statecraft.Phase(k);
            if (Gov.On("civ_gov_cycle") && ph >= 0)
            {
                UiKit.Header(c, Txt.T("parl_mandate") + " · " + Txt.T("civ_phase_" + Statecraft.Phases[ph]), UiKit.Gold);
                float m = Mandate.Get(k);
                UiKit.Bar(UiKit.Box(c, "Mand", 14f), m / 100f, m >= 50f ? new Color(0.5f, 0.85f, 0.45f) : new Color(0.95f, 0.6f, 0.35f), new Vector2(UiKit.W - 20f, 12f), Vector2.zero, ((int)m).ToString());
                if (Mandate.Rebelling(k)) UiKit.Label(c, Txt.T("elect_rebellion"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Bad);
            }
            Legit(c, k);
        }

        private static void Legit(Transform c, Kingdom k)
        {
            GovUi.Row(c, Txt.T("elect_legit"), G.Pct(Legitimacy.Get(k)) + " · " + Txt.T("civ_legit_" + Legitimacy.Source(k)), UiKit.Gold);
            GovUi.Row(c, Txt.T("civ_row_stability"), G.Pct(Politics.Stability(k)), UiKit.Soft);
        }

        public static List<Actor> Succession(Kingdom k)
        {
            var l = new List<Actor>();
            Clan royal = k.getKingClan();
            if (royal == null) return l;
            foreach (Actor a in royal.units)
            {
                if (!G.Ok(a) || a.isKing() || a.kingdom != k) continue;
                l.Add(a);
            }
            return l.OrderByDescending(a => a.isAdult() ? 1 : 0).ThenByDescending(a => a.getAge()).ThenByDescending(a => a.renown).Take(8).ToList();
        }

        private void Offices(Transform c, Kingdom k)
        {
            UiKit.Header(c, Txt.T("elect_ruler"), UiKit.Gold);
            if (k.hasKing()) Person(c, k.king, Gov.Title(k), UiKit.Gold);
            UiKit.Header(c, Txt.T("elect_governors"), UiKit.Gold);
            foreach (City ct in G.Cities(k).OrderByDescending(x => x.getPopulationPeople()).Take(16))
            {
                if (G.Ok(ct.leader)) Person(c, ct.leader, G.Name(ct), UiKit.Soft);
                else GovUi.Row(c, G.Name(ct), Txt.T("elect_vacant"), UiKit.Dim);
            }
            Actor gen = Mandate.BestGeneral(k);
            if (gen != null)
            {
                UiKit.Header(c, Txt.T("elect_general"), UiKit.Gold);
                Person(c, gen, Txt.T("civ_deed_captain"), UiKit.Soft);
            }
        }

        private void Domains(Transform c, Kingdom k)
        {
            var dom = Parliament.Domains(k);
            var seats = Parliament.Seats(k);
            foreach (var kv in dom.OrderByDescending(x => x.Value.Count))
            {
                UiKit.Header(c, Gov.ClanName(kv.Key) + " · " + (seats.ContainsKey(kv.Key) ? seats[kv.Key] : 0) + " " + Txt.T("parl_seats"), UiKit.Gold);
                foreach (City ct in kv.Value.Take(8)) GovUi.Row(c, G.Name(ct), ct.getPopulationPeople() + " · " + ArmyMission(ct), UiKit.Soft, Gov.ClanColor(kv.Key));
            }
        }

        private static string ArmyMission(City c)
        {
            return Txt.T("civ_mission_" + ArmyCommand.Mission(c));
        }

        private void VassalTab(Transform c, Kingdom k)
        {
            Kingdom lord = Diplomacy.Overlord(k);
            if (G.Ok(lord))
            {
                UiKit.Header(c, Txt.T("elect_overlord"), UiKit.Gold);
                GovUi.Row(c, G.Name(lord), Vassals.BondName(k), UiKit.Soft);
                GovUi.Row(c, Txt.T("elect_integration"), (int)Vassals.Integration(k) + " · " + Vassals.StateName(k), UiKit.Cyan);
                GovUi.Row(c, Txt.T("elect_status"), Txt.T("civ_vstatus_" + Vassals.Status(k)), UiKit.Soft);
            }
            List<Kingdom> deps = Diplomacy.DependentsOf(k);
            UiKit.Header(c, Txt.T("elect_vassals"), UiKit.Gold);
            if (deps.Count == 0) UiKit.Label(c, Txt.T("elect_no_vassals"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            bool act = GovUi.CanAct(k);
            foreach (Kingdom v in deps)
            {
                Kingdom vv = v;
                Action click = null;
                if (act && Vassals.Integration(v) >= 70f) click = () =>
                {
                    Vassals.DirectRule(k, vv);
                    Render();
                };
                GovUi.Row(c, G.Name(v), Vassals.BondName(v) + " · " + (int)Vassals.Integration(v) + " · " + Vassals.StateName(v), UiKit.Soft, G.Ok(v) ? UiKit.Hex(G.Hex(v)) : (Color?)null, click, G.Name(v), Txt.T("civ_vstatus_" + Vassals.Status(v)) + (click != null ? "\n\n" + Txt.T("elect_direct_rule_d") : ""));
            }
            UiKit.Header(c, Txt.T("elect_vassal_laws"), UiKit.Gold);
            foreach (string g in new[] { "vassal_policy", "vassal_tribute", "fealty" }) GovUi.Row(c, Laws.GroupName(g), Laws.Name(g, Laws.Get(k, g)), UiKit.Gold);
        }
    }
}
