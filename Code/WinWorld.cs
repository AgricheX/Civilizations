using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal class WinWorld : AbstractWindow<WinWorld>
    {
        public const string Id = "civ_world";
        private static readonly string[] TabKeys = { "world_tab_nations", "world_tab_space", "world_tab_chronicle", "world_tab_world" };
        private static readonly string[] TabIcons = { "ui/Icons/civ/world/nations", "ui/Icons/civ/world/space", "ui/Icons/civ/world/chronicle", "ui/Icons/civ/world/globe" };
        private static int _tab;
        private static int _page;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open()
        {
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
            UiKit.Tabs(c, TabKeys, TabIcons, _tab, i =>
            {
                _tab = i;
                _page = 0;
                Render();
            });
            switch (_tab)
            {
                case 0:
                    Nations(c);
                    break;
                case 1:
                    SpaceTab(c);
                    break;
                case 2:
                    ChronicleTab(c);
                    break;
                case 3:
                    WorldTab(c);
                    break;
            }
        }

        private void Nations(Transform c)
        {
            UiKit.Header(c, Txt.T("world_ranking"), UiKit.Gold, "ui/Icons/civ/world/nations");
            List<Kingdom> ks = G.Kingdoms().Where(k => k.isCiv()).OrderByDescending(k => Research.Era(k) + Math.Min(1f, Research.EraFraction(k, Research.Era(k)) / 0.65f) * 0.99f).ToList();
            int pages = Math.Max(1, (ks.Count + 11) / 12);
            _page = Math.Min(_page, pages - 1);
            int rank = _page * 12 + 1;
            foreach (Kingdom k in ks.Skip(_page * 12).Take(12))
            {
                Kingdom kk = k;
                int era = Research.Era(k);
                RectTransform box = UiKit.Box(c, "Nation", 34f);
                UiKit.Panel(box, 0.85f);
                UiKit.Text(box, rank++ + ".", new Vector2(-UiKit.W / 2f + 9f, 0f), new Vector2(16f, 12f), 8, TextAnchor.MiddleCenter, UiKit.Dim);
                UiKit.Banner(box, k, new Vector2(-UiKit.W / 2f + 28f, 0f), 24f);
                UiKit.Text(box, G.Name(k), new Vector2(-4f, 9f), new Vector2(110f, 11f), 9, TextAnchor.MiddleLeft);
                string gov = Txt.T("civ_p_" + Policies.Id(k, "gov"));
                UiKit.Text(box, gov + " · " + Txt.T("civ_stage_" + Politics.Stage(k)), new Vector2(-4f, -1f), new Vector2(110f, 9f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                float prog = era >= Db.LastEra ? 1f : Math.Min(1f, Research.EraFraction(k, era) / 0.65f);
                UiKit.Bar(box, prog, G.Col(Db.Eras[Math.Max(0, era)].Color), new Vector2(110f, 7f), new Vector2(-4f, -11f));
                UiKit.IconBtn(box, EraLock.EraSprite(era), () => WinTech.Open(kk), 24f, new Vector2(UiKit.W / 2f - 36f, 0f), Db.EraName(era), Txt.T("world_open_tech"));
                string route = Research.Route(k);
                if (!string.IsNullOrEmpty(route)) UiKit.Img(box, UiKit.Icon(WorldPrefs.Icon(route)), new Vector2(14f, 14f), new Vector2(UiKit.W / 2f - 12f, 6f));
                if (Internet.Exists(k)) UiKit.Img(box, UiKit.Icon("ui/Icons/civ/net/feed"), new Vector2(12f, 12f), new Vector2(UiKit.W / 2f - 12f, -8f));
            }
            UiKit.Pager(c, _page, pages, n =>
            {
                _page = n;
                Render();
            });
        }

        private void SpaceTab(Transform c)
        {
            UiKit.Header(c, Txt.T("world_space_firsts"), UiKit.Gold, "ui/Icons/civ/world/space");
            Dictionary<string, string> firsts = Space.Firsts();
            foreach (string m in Space.Milestones)
            {
                string who;
                Kingdom k = firsts.TryGetValue(m, out who) ? G.KingdomById(who) : null;
                string name = who == null ? "—" : k != null ? G.Name(k) : Txt.T("world_fallen");
                UiKit.Stat(c, "ui/Icons/civ/space/" + m, Txt.T("world_mission_" + m), name, who == null ? UiKit.Dim : UiKit.Gold);
            }
            UiKit.Header(c, Txt.T("world_space_powers"), UiKit.Gold);
            var ks = G.Kingdoms().Where(k => k.isCiv() && (Space.Launches(k) > 0 || Space.Satellites(k) > 0)).OrderByDescending(Space.Prestige).ToList();
            if (ks.Count == 0) UiKit.Label(c, Txt.T("world_no_space"), 24f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
            foreach (Kingdom k in ks.Take(12))
            {
                RectTransform box = UiKit.Box(c, "Space", 26f);
                UiKit.Panel(box, 0.8f);
                UiKit.Banner(box, k, new Vector2(-UiKit.W / 2f + 14f, 0f), 20f);
                UiKit.Text(box, G.Name(k), new Vector2(-16f, 5f), new Vector2(120f, 11f), 9, TextAnchor.MiddleLeft);
                UiKit.Text(box, Txt.F("world_space_line", Space.Launches(k), Space.Satellites(k)), new Vector2(-16f, -6f), new Vector2(120f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                UiKit.Text(box, Mathf.RoundToInt(Space.Prestige(k)).ToString(), new Vector2(UiKit.W / 2f - 20f, 0f), new Vector2(36f, 12f), 9, TextAnchor.MiddleRight, UiKit.Cyan);
            }
            List<Company> launchers = Companies.Active().Where(x => x.Sector == "space").ToList();
            if (launchers.Count > 0)
            {
                UiKit.Header(c, Txt.T("world_space_companies"), UiKit.Gold);
                foreach (Company co in launchers.OrderByDescending(x => x.Value).Take(6)) UiKit.Stat(c, "ui/Icons/civ/sector/space", co.Name, co.HomeName, UiKit.Soft);
            }
        }

        private void ChronicleTab(Transform c)
        {
            UiKit.Header(c, Txt.T("world_chronicle"), UiKit.Gold, "ui/Icons/civ/world/chronicle");
            var list = new List<KeyValuePair<int, string>>();
            foreach (Chronicle ch in Internet.Chronicles) list.Add(new KeyValuePair<int, string>(ch.Year, ch.Text));
            foreach (NetPost p in Internet.Feed) if (p.Important) list.Add(new KeyValuePair<int, string>(p.Year, p.Text));
            list = list.OrderByDescending(x => x.Key).ToList();
            if (list.Count == 0)
            {
                UiKit.Label(c, Txt.T("world_chronicle_empty"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            int pages = Math.Max(1, (list.Count + 13) / 14);
            _page = Math.Min(_page, pages - 1);
            foreach (var kv in list.Skip(_page * 14).Take(14)) UiKit.Paragraph(c, "<color=#FFD25C>" + Txt.F("net_year", kv.Key) + "</color>  " + kv.Value, 8, UiKit.Soft);
            UiKit.Pager(c, _page, pages, n =>
            {
                _page = n;
                Render();
            });
        }

        private void WorldTab(Transform c)
        {
            UiKit.Header(c, Txt.T("world_overview"), UiKit.Gold, "ui/Icons/civ/world/globe");
            UiKit.Stat(c, "ui/Icons/civ/cfg/globe", Txt.T("world_globe"), Txt.T("civ_globe_mode_" + Globe.Mode));
            UiKit.Stat(c, null, Txt.T("world_size"), MapBox.width + " × " + MapBox.height);
            UiKit.Stat(c, WorldPrefs.Icon(WorldPrefs.Route), Txt.T("civ_route_world"), Txt.T("civ_route_" + WorldPrefs.Route), UiKit.Gold);
            int km = 0;
            foreach (Kingdom k in G.Kingdoms()) km += Rails.CountKm(k);
            UiKit.Stat(c, "ui/Icons/civ/world/rail", Txt.T("world_rails"), km.ToString());
            UiKit.Stat(c, "ui/Icons/civ/status/radiation", Txt.T("world_radiation"), War.RadiationSites.ToString(), War.RadiationSites > 0 ? UiKit.Bad : UiKit.Good);
            int nukes = G.Kingdoms().Sum(k => War.Nukes(k));
            UiKit.Stat(c, "ui/Icons/civ/world/nuke", Txt.T("world_nukes"), nukes.ToString(), nukes > 0 ? UiKit.Bad : UiKit.Soft);
            UiKit.Stat(c, "ui/Icons/civ/net/feed", Txt.T("world_online"), G.Kingdoms().Count(Internet.Exists).ToString(), UiKit.Cyan);
            UiKit.Stat(c, "ui/Icons/civ/econ/companies", Txt.T("world_companies"), Companies.Active().Count.ToString());
            UiKit.Header(c, Txt.T("world_routes"), UiKit.Gold);
            foreach (var kv in WorldPrefs.Followers()) UiKit.Stat(c, WorldPrefs.Icon(kv.Key), Txt.T("civ_route_" + kv.Key), kv.Value.ToString(), kv.Key == WorldPrefs.Route ? UiKit.Gold : UiKit.Soft);
            UiKit.Header(c, Txt.T("world_eras"), UiKit.Gold);
            var byEra = new int[Db.Eras.Count];
            foreach (Kingdom k in G.Kingdoms()) if (k.isCiv()) byEra[Math.Max(0, Research.Era(k))]++;
            for (int i = 0; i < byEra.Length; i++) if (byEra[i] > 0) UiKit.Stat(c, "ui/Icons/civ/era/" + Db.Eras[i].Id, Db.EraName(i), byEra[i].ToString(), G.Col(Db.Eras[i].Color));
        }
    }
}
