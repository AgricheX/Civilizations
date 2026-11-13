using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal class WinState : AbstractWindow<WinState>
    {
        public const string Id = "civ_state";
        private static readonly string[] TabKeys = { "state_tab_overview", "state_tab_relations", "state_tab_orgs", "state_tab_culture" };
        private static readonly string[] TabIcons = { "ui/Icons/civ/state/overview", "ui/Icons/civ/state/relations", "ui/Icons/civ/state/orgs", "ui/Icons/civ/state/culture" };
        private static Kingdom _k;
        private static int _tab;
        private static int _page;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open(Kingdom k)
        {
            if (k != null) _k = k;
            _page = 0;
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
            RectTransform head = UiKit.Box(c, "Head", 30f);
            UiKit.Panel(head, 0.85f);
            UiKit.Banner(head, _k, new Vector2(-UiKit.W / 2f + 16f, 0f), 24f);
            UiKit.Text(head, G.Name(_k), new Vector2(0f, 6f), new Vector2(140f, 12f), 10, TextAnchor.MiddleLeft, UiKit.Gold);
            UiKit.Text(head, Txt.T("civ_p_" + Policies.Id(_k, "gov")) + " · " + Db.EraName(Research.Era(_k)), new Vector2(0f, -6f), new Vector2(140f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
            UiKit.Tabs(c, TabKeys, TabIcons, _tab, i =>
            {
                _tab = i;
                _page = 0;
                Render();
            });
            switch (_tab)
            {
                case 0:
                    Overview(c);
                    break;
                case 1:
                    Relations(c);
                    break;
                case 2:
                    OrgsTab(c);
                    break;
                case 3:
                    Culture(c);
                    break;
            }
        }

        private static string Pct(float v)
        {
            return Mathf.RoundToInt(v * 100f) + "%";
        }

        private static Color Tone(float v)
        {
            return v >= 0.6f ? UiKit.Good : v < 0.3f ? UiKit.Bad : UiKit.Soft;
        }

        private void Overview(Transform c)
        {
            KProfile p = Profile.Get(_k);
            UiKit.Header(c, Txt.T("state_power"), UiKit.Gold, "ui/Icons/civ/state/overview");
            float legit = Legitimacy.Get(_k);
            UiKit.Stat(c, "ui/Icons/civ/state/legit", Txt.T("state_legitimacy"), Pct(legit), Tone(legit));
            UiKit.Stat(c, "ui/Icons/civ/state/source", Txt.T("state_source"), Txt.T("civ_legit_" + Legitimacy.Source(_k)));
            UiKit.Stat(c, "ui/Icons/civ/state/stability", Txt.T("state_stability"), Pct(p.Stability), Tone(p.Stability));
            UiKit.Stat(c, "ui/Icons/civ/cfg/fog_view", Txt.T("state_known"), Fog.KnownKingdoms(_k).Count().ToString(), UiKit.Cyan);
            Dep d = Diplomacy.DepOf(_k);
            Kingdom lord = Diplomacy.Overlord(_k);
            if (d != null && lord != null)
            {
                UiKit.Header(c, Txt.T("state_dependency"), UiKit.Gold);
                UiKit.Stat(c, "ui/Icons/civ/state/dep", Txt.T("civ_dep_" + d.Kind), G.Name(lord), UiKit.Soft);
                UiKit.Stat(c, null, Txt.T("state_autonomy"), Mathf.RoundToInt(d.Autonomy) + "%", d.Autonomy > 60f ? UiKit.Good : UiKit.Soft);
            }
            List<Kingdom> deps = Diplomacy.DependentsOf(_k);
            if (deps.Count > 0)
            {
                UiKit.Header(c, Txt.T("state_dependents"), UiKit.Gold);
                foreach (Kingdom o in deps.Take(8))
                {
                    Dep od = Diplomacy.DepOf(o);
                    UiKit.Stat(c, "ui/Icons/civ/state/dep", G.Name(o), od != null ? Txt.T("civ_dep_" + od.Kind) : "", UiKit.Soft);
                }
            }
            UiKit.Header(c, Txt.T("state_wars"), UiKit.Gold);
            int wars = 0;
            foreach (global::War w in _k.getWars())
            {
                Kingdom foe = w.isAttacker(_k) ? w.getMainDefender() : w.getMainAttacker();
                if (!G.Ok(foe)) continue;
                string goal = WarGoals.Get(w);
                UiKit.Stat(c, "ui/Icons/civ/state/war", G.Name(foe), goal.Length > 0 ? Txt.T("civ_goal_" + goal) : "", UiKit.Bad);
                if (++wars >= 6) break;
            }
            if (wars == 0) UiKit.Label(c, Txt.T("state_peace"), 18f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            if (Peace.Demilitarized(_k)) UiKit.Stat(c, "ui/Icons/civ/state/war", Txt.T("civ_term_demilitarization"), "", UiKit.Bad);
        }

        private void Relations(Transform c)
        {
            UiKit.Header(c, Txt.T("state_treaties"), UiKit.Gold, "ui/Icons/civ/state/relations");
            List<Treaty> ts = Diplomacy.Of(_k);
            if (ts.Count == 0) UiKit.Label(c, Txt.T("state_no_treaties"), 18f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            foreach (Treaty t in ts.Take(10))
            {
                Kingdom o = World.world.kingdoms.get(t.A == _k.data.id ? t.B : t.A);
                if (!G.Ok(o)) continue;
                string until = t.Until > 0 ? Txt.F("state_until", t.Until) : "";
                UiKit.Stat(c, "ui/Icons/civ/state/treaty", Txt.T("civ_treaty_" + t.Type), G.Name(o) + (until.Length > 0 ? " · " + until : ""), UiKit.Soft);
            }
            UiKit.Header(c, Txt.T("state_known_kingdoms"), UiKit.Gold);
            List<Kingdom> known = Fog.KnownKingdoms(_k).OrderByDescending(o => Opinion(_k, o)).ToList();
            int pages = Math.Max(1, (known.Count + 7) / 8);
            _page = Math.Min(_page, pages - 1);
            foreach (Kingdom o in known.Skip(_page * 8).Take(8))
            {
                RectTransform box = UiKit.Box(c, "Known", 24f);
                UiKit.Panel(box, 0.8f);
                UiKit.Banner(box, o, new Vector2(-UiKit.W / 2f + 13f, 0f), 18f);
                UiKit.Text(box, G.Name(o), new Vector2(-20f, 4f), new Vector2(110f, 10f), 8, TextAnchor.MiddleLeft);
                UiKit.Text(box, Db.EraName(Research.Era(o)) + " · " + Txt.T("civ_p_" + Policies.Id(o, "gov")), new Vector2(-20f, -6f), new Vector2(110f, 9f), 7, TextAnchor.MiddleLeft, UiKit.Dim);
                int op = Opinion(_k, o);
                UiKit.Text(box, (op > 0 ? "+" : "") + op, new Vector2(UiKit.W / 2f - 20f, 0f), new Vector2(34f, 12f), 9, TextAnchor.MiddleRight, op >= 0 ? UiKit.Good : UiKit.Bad);
            }
            UiKit.Pager(c, _page, pages, n =>
            {
                _page = n;
                Render();
            });
        }

        private static int Opinion(Kingdom a, Kingdom b)
        {
            try
            {
                return World.world.diplomacy.getOpinion(a, b).total;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void OrgsTab(Transform c)
        {
            UiKit.Header(c, Txt.T("state_orgs"), UiKit.Gold, "ui/Icons/civ/state/orgs");
            List<Org> mine = Orgs.Of(_k);
            if (mine.Count == 0) UiKit.Label(c, Txt.T("state_no_orgs"), 18f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            foreach (Org o in mine.Take(10))
            {
                OrgType t = Orgs.TypeOf(o);
                Kingdom lead = World.world.kingdoms.get(o.Leader);
                string role = o.Leader == _k.data.id ? Txt.T("state_leader") : G.Ok(lead) ? G.Name(lead) : "";
                UiKit.Stat(c, "ui/Icons/civ/orgcat/" + (t != null ? t.Cat : "political"), Orgs.Name(o), role + " · " + o.Members.Count, o.Leader == _k.data.id ? UiKit.Gold : UiKit.Soft);
            }
            WorldOrder wo = WorldOrders.Of(_k);
            UiKit.Header(c, Txt.T("state_world_order"), UiKit.Gold);
            if (wo == null) UiKit.Label(c, Txt.T("state_no_order"), 18f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            else
            {
                Kingdom lead = World.world.kingdoms.get(wo.Leader);
                UiKit.Stat(c, "ui/Icons/civ/orgcat/order", Txt.T("civ_order_" + wo.Type), G.Ok(lead) ? G.Name(lead) : "", UiKit.Gold);
                UiKit.Paragraph(c, Txt.T("civ_order_" + wo.Type + "_d"), 7, UiKit.Soft);
            }
            foreach (WorldOrder o in WorldOrders.Active) if (o.Outlaws.Contains(_k.data.id)) UiKit.Stat(c, "ui/Icons/civ/state/war", Txt.T("state_outlaw"), Txt.T("civ_order_" + o.Type), UiKit.Bad);
        }

        private void Culture(Transform c)
        {
            UiKit.Header(c, Txt.T("state_festivals"), UiKit.Gold, "ui/Icons/civ/state/culture");
            List<string> fs = Festivals.Adopted(_k);
            if (fs.Count == 0) UiKit.Label(c, Txt.T("state_no_festivals"), 18f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            foreach (string f in fs) UiKit.Stat(c, "ui/Icons/civ/fest/" + f, Txt.T("civ_fest_" + f), "", UiKit.Soft);
            UiKit.Header(c, Txt.T("state_greats"), UiKit.Gold);
            int n = 0;
            foreach (string entry in Store.Set(_k.data, "civ_greats"))
            {
                string[] parts = entry.Split('|');
                Actor a = G.ActorById(parts[0]);
                string role = parts.Length > 1 ? parts[1] : "";
                string name = G.Ok(a) ? a.getName() : parts.Length > 2 ? parts[2] : "?";
                UiKit.Stat(c, "ui/Icons/civ/great/" + role, name, Txt.T("civ_great_" + role), UiKit.Gold);
                n++;
            }
            if (n == 0) UiKit.Label(c, Txt.T("state_no_greats"), 18f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            Dictionary<string, float> dormant = Regression.Dormant(_k);
            if (dormant.Count > 0)
            {
                UiKit.Header(c, Txt.T("state_dormant"), UiKit.Bad);
                foreach (var kv in dormant.OrderByDescending(x => x.Value).Take(8)) UiKit.Stat(c, "ui/Icons/civ/log/i_dormant", Db.TechName(kv.Key), Txt.F("net_year", (int)kv.Value), UiKit.Dim);
            }
        }
    }
}
