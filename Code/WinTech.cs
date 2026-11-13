using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NeoModLoader.api;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;

namespace Civilizations
{
    internal static class TechInfo
    {
        public static string Unlock(string u)
        {
            int i = u.IndexOf(':');
            if (i < 0) return u;
            string p = u.Substring(0, i);
            string v = u.Substring(i + 1);
            string name;
            switch (p)
            {
                case "veh":
                case "air":
                case "ship":
                case "sol":
                    name = Txt.T(v);
                    break;
                case "bld":
                    name = Txt.T("building_" + v);
                    break;
                case "cloth":
                case "item":
                case "uni":
                    name = Txt.T("item_" + v);
                    break;
                case "gov":
                case "ideo":
                case "econ":
                    name = Txt.T("civ_p_" + v);
                    break;
                case "form":
                    name = Txt.T("civ_form_" + v);
                    break;
                case "train":
                    name = Txt.T("civ_train_" + v);
                    break;
                case "culture":
                {
                    CultureTrait ct = AssetManager.culture_traits.get(v);
                    name = ct != null ? ct.getTranslatedName() : v;
                    break;
                }
                default:
                    name = Txt.T("civ_" + p + "_" + v);
                    break;
            }
            return Txt.T("civ_unl_" + p) + ": " + name;
        }

        public static string Effects(Dictionary<string, float> eff)
        {
            var sb = new StringBuilder();
            foreach (var kv in eff)
            {
                if (sb.Length > 0) sb.Append('\n');
                bool good = kv.Key == "pollution" ? kv.Value < 0 : kv.Value > 0;
                sb.Append("<color=").Append(good ? "#8CF07A" : "#FF7A6A").Append('>').Append(G.Signed(kv.Value)).Append("</color> ").Append(Txt.T("civ_eff_" + kv.Key));
            }
            return sb.ToString();
        }

        public static string Describe(TechDef t, Kingdom k)
        {
            var sb = new StringBuilder();
            sb.Append("<color=#FFD25C>").Append(Db.EraName(t.Era)).Append("</color>  ·  ").Append(Txt.T("civ_cat_" + t.Cat));
            if (!string.IsNullOrEmpty(t.Route)) sb.Append("  ·  <color=#9ADFFF>").Append(Txt.T("civ_route_" + t.Route)).Append("</color>");
            if (t.Rare) sb.Append("\n<color=#E59AFF>").Append(Txt.T("civ_rare")).Append("</color>");
            if (Research.IsFundamental(t)) sb.Append("\n<color=#FFA040>").Append(Txt.T("civ_fundamental")).Append("</color>");
            string d = "civ_t_" + t.Base + "_d";
            if (Txt.Has(d)) sb.Append("\n\n").Append(Txt.T(d));
            if (t.Eff.Count > 0) sb.Append("\n\n").Append(Effects(t.Eff));
            if (t.Unl != null && t.Unl.Length > 0)
            {
                sb.Append('\n');
                foreach (string u in t.Unl) sb.Append("\n<color=#FFD25C>»</color> ").Append(Unlock(u));
            }
            if (t.Req != null && t.Req.Length > 0)
            {
                sb.Append("\n\n").Append(Txt.T("civ_requires")).Append(' ');
                for (int i = 0; i < t.Req.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    bool has = k != null && Research.Has(k, t.Req[i]);
                    sb.Append("<color=").Append(has ? "#8CF07A" : "#FF7A6A").Append('>').Append(Db.TechName(t.Req[i])).Append("</color>");
                }
            }
            if (k != null)
            {
                sb.Append("\n").Append(Txt.T("civ_cost")).Append(' ').Append(Mathf.RoundToInt(Research.Cost(k, t)));
                int n = Research.ContactsKnowing(k, t);
                if (n > 0) sb.Append("  (").Append(Txt.F("civ_known_by", n)).Append(')');
            }
            return sb.ToString();
        }
    }

    internal class WinTech : AbstractWindow<WinTech>
    {
        public const string Id = "civ_tech";
        private static Kingdom _k;
        private static int _era = -1;
        private static bool _edit;
        private static int _filter;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open(Kingdom k)
        {
            if (k != null) _k = k;
            _era = -1;
            UiKit.Open(Id);
        }

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
        }

        public override void OnNormalEnable()
        {
            if (!G.Ok(_k)) _k = SelectedMetas.selected_kingdom;
            Render();
        }

        private static void Refresh()
        {
            if (Instance != null && Instance.IsOpened) Instance.Render();
        }

        private List<Kingdom> Civs()
        {
            return G.Kingdoms().Where(x => x.isCiv()).OrderByDescending(x => Research.Era(x)).ThenByDescending(x => Research.Known(x).Count).ToList();
        }

        private void Cycle(int d)
        {
            List<Kingdom> l = Civs();
            if (l.Count == 0) return;
            int i = Math.Max(0, l.IndexOf(_k));
            _k = l[((i + d) % l.Count + l.Count) % l.Count];
            _era = -1;
            Render();
        }

        private void Render()
        {
            Transform c = ContentTransform;
            UiKit.Clear(c);
            if (!G.Loaded) return;
            if (!G.Ok(_k) || !_k.isCiv())
            {
                List<Kingdom> l = Civs();
                _k = l.Count > 0 ? l[0] : null;
            }
            if (_k == null)
            {
                UiKit.Label(c, Txt.T("civ_no_kingdoms"), 30f, 10, TextAnchor.MiddleCenter, UiKit.Soft);
                return;
            }
            int kEra = Research.Era(_k);
            if (_era < 0) _era = Math.Max(0, kEra);
            _era = Math.Max(0, Math.Min(Db.LastEra, _era));
            UiKit.Selector(c, G.Name(_k), null, () => Cycle(-1), () => Cycle(1));
            Summary(c, kEra);
            UiKit.Selector(c, Db.EraName(_era), EraLock.EraSprite(_era), () =>
            {
                _era = Math.Max(0, _era - 1);
                Render();
            }, () =>
            {
                _era = Math.Min(Db.LastEra, _era + 1);
                Render();
            });
            Tools(c);
            EraDef e = Db.Eras[_era];
            KState s = Research.State(_k);
            foreach (string cat in Db.Cats)
            {
                List<TechDef> list = e.Techs.Where(t => t.Cat == cat && Show(t, s)).ToList();
                if (list.Count == 0) continue;
                int known = list.Count(t => s.Known.Has(t.Index));
                UiKit.Header(c, Txt.T("civ_cat_" + cat) + "  " + known + "/" + list.Count, G.Col(Db.CatColors.ContainsKey(cat) ? Db.CatColors[cat] : "#FFFFFF"), "ui/Icons/civ/cat/" + cat);
                Grid(c, list, s);
            }
            UiKit.Gap(c, 6f);
            UiKit.Paragraph(c, Txt.T(_edit ? "civ_tech_hint_edit" : "civ_tech_hint"), 8, UiKit.Dim);
        }

        private bool Show(TechDef t, KState s)
        {
            if (_filter == 1) return t.Level <= 1;
            if (_filter == 2) return t.Rare;
            if (_filter == 3) return s.Known.Has(t.Index);
            return true;
        }

        private void Summary(Transform c, int kEra)
        {
            RectTransform box = UiKit.Box(c, "Summary", 50f);
            UiKit.Panel(box);
            UiKit.Img(box, EraLock.EraSprite(kEra), new Vector2(28f, 28f), new Vector2(-UiKit.W / 2f + 20f, 6f));
            string route = Research.Route(_k);
            if (!string.IsNullOrEmpty(route)) UiKit.Img(box, UiKit.Icon(WorldPrefs.Icon(route)), new Vector2(14f, 14f), new Vector2(-UiKit.W / 2f + 30f, -6f));
            UiKit.Text(box, Db.EraName(kEra), new Vector2(10f, 15f), new Vector2(150f, 14f), 10, TextAnchor.MiddleLeft, G.Col(Db.Eras[Math.Max(0, kEra)].Color));
            TechDef cur = Research.Current(_k);
            string line = cur != null ? Txt.T("civ_researching") + " " + Db.TechName(cur.Id) : Txt.T("civ_researching_none");
            UiKit.Text(box, line, new Vector2(10f, 2f), new Vector2(150f, 12f), 8, TextAnchor.MiddleLeft, UiKit.Soft);
            float p = cur != null ? Research.Progress(_k) / Math.Max(1f, Research.Cost(_k, cur)) : 0f;
            UiKit.Bar(box, p, new Color(0.35f, 0.75f, 1f), new Vector2(150f, 9f), new Vector2(10f, -10f), Mathf.RoundToInt(p * 100f) + "%");
            string r = Txt.T("civ_rate") + " " + Research.Rate(_k, cur).ToString("0.00") + "  ·  " + Research.Known(_k).Count + " " + Txt.T("civ_techs");
            UiKit.Text(box, r, new Vector2(10f, -20f), new Vector2(150f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Dim);
            if (Research.AtCap(_k)) UiKit.Label(c, Txt.F("civ_cap_reached", Db.EraName(Research.Cap(_k))), 14f, 8, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.45f));
            else if (Research.SettingsCap() >= 0) UiKit.Label(c, Txt.F("civ_cap_settings", Db.EraName(Research.SettingsCap())), 12f, 7, TextAnchor.MiddleCenter, UiKit.Dim);
            Kingdom kk = _k;
            int kc = Research.KingdomCap(kk);
            int wc = Research.WorldCap();
            string kt = kc >= 0 ? Txt.F("civ_cap_k_on", Db.EraName(kc)) : Txt.T("civ_cap_k_off");
            string wt = wc >= 0 ? Txt.F("civ_cap_w_on", Db.EraName(wc)) : Txt.T("civ_cap_w_off");
            RectTransform locks = UiKit.Box(c, "Locks", 24f);
            Color on = new Color(1f, 0.85f, 0.45f);
            SimpleButton kb = UiKit.Btn(locks, kc >= 0 ? Db.EraName(kc) : Txt.T("civ_cap_k"), UiKit.Icon("ui/Icons/civ/cfg/lock_ages"), () =>
            {
                Research.SetKingdomCap(kk, Research.KingdomCap(kk) >= 0 ? -1 : Research.Era(kk));
                Render();
            }, new Vector2(104f, 20f), new Vector2(-53f, 0f), Txt.T("civ_cap_k"), kt + "\n\n" + Txt.T("civ_cap_d"));
            if (kc >= 0) kb.Background.color = on;
            float ww = wc >= 0 ? 64f : 104f;
            SimpleButton wb = UiKit.Btn(locks, wc >= 0 ? Db.EraName(wc) : Txt.T("civ_cap_w"), UiKit.Icon("ui/Icons/civ/btn/world"), () =>
            {
                Research.SetWorldCap(Research.WorldCap() >= 0 ? -1 : Math.Max(0, Research.Era(kk)));
                Render();
            }, new Vector2(ww, 20f), new Vector2(53f, 0f), Txt.T("civ_cap_w"), wt + "\n\n" + Txt.T("civ_cap_d") + "\n\n" + Txt.T("civ_cap_w_pick"));
            if (wc >= 0)
            {
                wb.Background.color = on;
                UiKit.Btn(locks, "-", null, () =>
                {
                    Research.SetWorldCap(Math.Max(0, Research.WorldCap() - 1));
                    Render();
                }, new Vector2(18f, 20f), new Vector2(11f, 0f), Txt.T("civ_cap_w_down"), Txt.T("civ_cap_w_pick"));
                UiKit.Btn(locks, "+", null, () =>
                {
                    Research.SetWorldCap(Math.Min(Db.LastEra, Research.WorldCap() + 1));
                    Render();
                }, new Vector2(18f, 20f), new Vector2(95f, 0f), Txt.T("civ_cap_w_up"), Txt.T("civ_cap_w_pick"));
            }
        }

        private void Tools(Transform c)
        {
            RectTransform box = UiKit.Box(c, "Tools", 24f);
            string[] fk = { "civ_filter_all", "civ_filter_core", "civ_filter_rare", "civ_filter_known" };
            float x = -UiKit.W / 2f + 22f;
            for (int i = 0; i < fk.Length; i++)
            {
                int idx = i;
                SimpleButton b = UiKit.Btn(box, Txt.T(fk[i]), null, () =>
                {
                    _filter = idx;
                    Render();
                }, new Vector2(40f, 20f), new Vector2(x + i * 42f, 0f));
                b.Background.color = _filter == i ? new Color(1f, 0.85f, 0.45f) : new Color(0.75f, 0.75f, 0.8f);
            }
            SimpleButton ed = UiKit.IconBtn(box, UiKit.Icon("ui/Icons/civ/btn/edit"), () =>
            {
                _edit = !_edit;
                Render();
            }, 22f, new Vector2(UiKit.W / 2f - 36f, 0f), Txt.T("civ_tech_edit"), Txt.T("civ_tech_edit_d"));
            ed.Background.color = _edit ? new Color(1f, 0.85f, 0.45f) : Color.white;
            if (_edit)
            {
                RectTransform row = UiKit.Box(c, "EditTools", 24f);
                UiKit.Btn(row, Txt.T("civ_learn_era"), null, () => LearnEra(true), new Vector2(66f, 20f), new Vector2(-70f, 0f));
                UiKit.Btn(row, Txt.T("civ_forget_era"), null, () => LearnEra(false), new Vector2(66f, 20f), new Vector2(0f, 0f));
                UiKit.Btn(row, Txt.T("civ_route_cycle"), UiKit.Icon(WorldPrefs.Icon(string.IsNullOrEmpty(Research.Route(_k)) ? "real" : Research.Route(_k))), CycleRoute, new Vector2(22f, 22f), new Vector2(60f, 0f), Txt.T("civ_route_set"), Txt.T("civ_route_set_d"));
                UiKit.Btn(row, Txt.T("civ_setup_era"), null, () =>
                {
                    Research.SetupEra(_k, _era);
                    Render();
                }, new Vector2(30f, 20f), new Vector2(90f, 0f), Txt.T("civ_setup_era"), Txt.T("civ_setup_era_d"));
                RectTransform row2 = UiKit.Box(c, "EditTools2", 24f);
                Kingdom dk = _k;
                UiKit.Btn(row2, Txt.T("civ_develop"), UiKit.Icon("ui/Icons/civ/btn/econ"), () =>
                {
                    int n = Catchup.Develop(dk);
                    WorldTip.showNowTop(Txt.F("civ_develop_done", n), false);
                    Render();
                }, new Vector2(170f, 20f), Vector2.zero, Txt.T("civ_develop"), Txt.T("civ_develop_d"));
            }
            SimpleButton help = UiKit.IconBtn(box, UiKit.Icon(WorldPrefs.Icon(WorldPrefs.Route)), WinRoute.Open, 22f, new Vector2(UiKit.W / 2f - 12f, 0f), Txt.T("civ_routes Title"), Txt.T("civ_route_world_d"));
        }

        private void CycleRoute()
        {
            string cur = Research.Route(_k);
            int i = Array.IndexOf(WorldPrefs.Routes, cur);
            string next = WorldPrefs.Routes[(i + 1) % WorldPrefs.Routes.Length];
            Research.SetRoute(_k, next);
            Render();
        }

        private void LearnEra(bool learn)
        {
            KState s = Research.State(_k);
            var set = new TechSet(s.Known);
            string route = Research.Route(_k);
            foreach (TechDef t in Db.Eras[_era].Techs)
            {
                if (learn)
                {
                    if (t.Rare) continue;
                    if (!string.IsNullOrEmpty(t.Route) && t.Route != route && !(t.Route == "real" && string.IsNullOrEmpty(route))) continue;
                    set.Add(t.Index);
                }
                else set.Remove(t.Index);
            }
            if (!learn)
            {
                foreach (TechDef t in Db.Techs) if (t.Era > _era) set.Remove(t.Index);
            }
            Research.SetAll(_k, set);
            Render();
        }

        private void Toggle(TechDef t)
        {
            if (!G.Ok(_k)) return;
            KState s = Research.State(_k);
            if (!_edit)
            {
                if (!s.Known.Has(t.Index) && Research.Available(_k, s, t, true))
                {
                    Store.Put(_k.data, "civ_cur", t.Id);
                    Store.Put(_k.data, "civ_prog", 0f);
                    Render();
                }
                return;
            }
            if (s.Known.Has(t.Index)) Research.Lose(_k, t, "editor");
            else Research.Learn(_k, t, "editor");
            Render();
        }

        private void Grid(Transform c, List<TechDef> list, KState s)
        {
            const int per = 8;
            const float size = 24f;
            float gap = (UiKit.W - per * size) / (per - 1);
            TechDef cur = Research.Current(_k);
            for (int i = 0; i < list.Count; i += per)
            {
                RectTransform row = UiKit.Box(c, "Row", size + 2f);
                for (int j = 0; j < per && i + j < list.Count; j++)
                {
                    TechDef t = list[i + j];
                    bool known = s.Known.Has(t.Index);
                    bool avail = !known && Research.Available(_k, s, t, true);
                    float x = -UiKit.W / 2f + size / 2f + j * (size + gap);
                    string title = Db.TechName(t.Id);
                    SimpleButton b = UiKit.IconBtn(row, UiKit.Icon(t.Icon, "ui/Icons/civ/cat/" + t.Cat), () => Toggle(t), size, new Vector2(x, 0f), title, TechInfo.Describe(t, _k), known || avail, true);
                    if (known) b.Background.color = Color.white;
                    else if (avail) b.Background.color = new Color(1f, 0.85f, 0.45f);
                    else b.Background.color = new Color(0.45f, 0.45f, 0.5f);
                    if (t.Rare) b.Background.color = known ? new Color(0.95f, 0.75f, 1f) : new Color(0.55f, 0.4f, 0.62f);
                    if (!known && Research.IsFundamental(t)) b.Background.color = avail ? new Color(1f, 0.6f, 0.25f) : new Color(0.6f, 0.38f, 0.2f);
                    if (cur != null && cur.Index == t.Index) b.Background.color = new Color(0.5f, 0.85f, 1f);
                }
            }
        }

        public static void Changed(Kingdom k)
        {
            if (k == null || k == _k) Refresh();
        }
    }
}
