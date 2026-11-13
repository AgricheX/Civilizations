using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;

namespace Civilizations
{
    internal static class KingdomUi
    {
        private static TooltipDataGetter Tip(string title, string desc)
        {
            string t = UiKit.Dyn(title);
            string d = UiKit.Dyn(desc);
            return () => new TooltipData { tip_name = t, tip_description = d };
        }

        private static KeyValueField Row(StatsRowsContainer box, string id, string value, string color, string icon, string tipTitle, string tipDesc, Action click = null)
        {
            if (box == null) return null;
            KeyValueField f = box.showStatRow(UiKit.Dyn(Txt.T(id)), value, color, MetaType.None, -1L, false, icon, "tip", Tip(tipTitle, tipDesc), true);
            if (click != null && f != null) f.on_click_value = () => click();
            return f;
        }

        private static string PolicyDesc(Kingdom k, string group)
        {
            PolicyDef p = Policies.Get(k, group);
            if (p == null) return "";
            var sb = new StringBuilder();
            string d = "civ_p_" + p.Id + "_d";
            if (Txt.Has(d)) sb.Append(Txt.T(d)).Append("\n\n");
            if (p.Eff.Count > 0) sb.Append(TechInfo.Effects(p.Eff)).Append("\n\n");
            sb.Append(Txt.T("civ_support")).Append(' ').Append(G.Pct(Politics.Support(k, group)));
            string alt = Politics.Alternative(k, group);
            if (!string.IsNullOrEmpty(alt) && alt != p.Id) sb.Append('\n').Append(Txt.T("civ_alternative")).Append(' ').Append(Txt.T("civ_p_" + alt));
            return sb.ToString();
        }

        public static bool Hidden(Kingdom k)
        {
            Kingdom me = Player.K;
            return G.Ok(me) && k != me && !Fog.Knows(me, k);
        }

        public static void Meta(StatsRowsContainer box, Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            if (Hidden(k))
            {
                Row(box, "civ_row_research", "?", "#9AA0A8", "civ/stat/research", Txt.T("civ_row_research"), Txt.T("civ_ctrl_unknown_d"));
                return;
            }
            foreach (string g in new[] { "gov", "ideo", "econ", "xeno", "transport" })
            {
                PolicyDef p = Policies.Get(k, g);
                if (p == null) continue;
                Kingdom kk = k;
                Row(box, "civ_pg_" + g, Txt.T("civ_p_" + p.Id), "#FFD25C", "civ/policy/" + p.Id, Txt.T("civ_p_" + p.Id), PolicyDesc(k, g), () => WinTech.Open(kk));
            }
            int era = Research.Era(k);
            Kingdom k2 = k;
            string eraDesc = Txt.F("civ_era_row_d", Research.Known(k).Count, G.Pct(Math.Min(1f, Research.EraFraction(k, era) / 0.65f)));
            Row(box, "civ_row_era", Db.EraName(era), Db.Eras[Math.Max(0, era)].Color, "civ/era/" + Db.Eras[Math.Max(0, era)].Id, Db.EraName(era), eraDesc, () => WinTech.Open(k2));
            TechDef cur = Research.Current(k);
            Row(box, "civ_row_research", cur != null ? Db.TechName(cur.Id) : "-", "#7AD3E0", "civ/stat/research", Txt.T("civ_row_research"), cur != null ? TechInfo.Describe(cur, k) : "", () => WinTech.Open(k2));
            string route = Research.Route(k);
            if (!string.IsNullOrEmpty(route)) Row(box, "civ_row_route", Txt.T("civ_route_" + route), "#9ADFFF", "civ/route/" + route, Txt.T("civ_route_" + route), Txt.T("civ_route_" + route + "_d"));
            if (Currency.Has(k))
            {
                string cd = Txt.F("civ_currency_row_d", Currency.Rate(k).ToString("0.###"), (Currency.Inflation(k) * 100f).ToString("0.#"));
                Row(box, "civ_row_currency", Currency.Name(k) + " (" + Currency.Symbol(k) + ")", "#8CF07A", "civ/econ/currency", Currency.Name(k), cd, WinEcon.Open);
            }
            Clan cl = Clans.Dominant(k);
            if (cl != null && cl.data != null)
            {
                string cause = Clans.Cause(cl);
                string desc = Txt.F("civ_clan_row_d", G.Pct(Clans.Influence(cl)), string.IsNullOrEmpty(cause) ? "-" : Txt.T("civ_p_" + cause));
                Row(box, "civ_row_clan", cl.data.name, "#E0C85A", "civ/world/clan", cl.data.name, desc);
            }
        }

        public static void Stats(StatsWindow w, Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv() || Hidden(k)) return;
            StatsRowsContainer box = w.stats_rows_container;
            float st = Politics.Stability(k);
            Row(box, "civ_row_stability", G.Pct(st), st > 0.6f ? "#8CF07A" : st > 0.35f ? "#FFD25C" : "#FF7A6A", "civ/stat/stability", Txt.T("civ_row_stability"), Txt.T("civ_row_stability_d"));
            Row(box, "civ_row_stage", Txt.T("civ_stage_" + Politics.Stage(k)), null, "civ/stat/stage", Txt.T("civ_stage_" + Politics.Stage(k)), Txt.T("civ_stage_" + Politics.Stage(k) + "_d"));
            if (Energy.Started(k))
            {
                float sup = Energy.KingdomSupply(k);
                float dem = Energy.KingdomDemand(k);
                Row(box, "civ_row_energy", G.Short(sup) + " / " + G.Short(dem), sup >= dem ? "#8CF07A" : "#FF7A6A", "civ/econ/energy", Txt.T("civ_row_energy"), Txt.T("civ_row_energy_d"), WinEcon.Open);
            }
            float slaves = 0f;
            float serv = 0f;
            int n = 0;
            foreach (City ct in G.Cities(k))
            {
                slaves += Xeno.SlaveShare(ct);
                serv += Xeno.ServantShare(ct);
                n++;
            }
            if (n > 0)
            {
                slaves /= n;
                serv /= n;
            }
            if (slaves + serv > 0.001f) Row(box, "civ_row_subjects", G.Pct(slaves) + " / " + G.Pct(serv), "#FF9A6A", "civ/stat/subjects", Txt.T("civ_row_subjects"), Txt.T("civ_row_subjects_d"));
            int comps = Companies.Of(k).Count;
            if (comps > 0) Row(box, "civ_row_companies", comps.ToString(), "#FFD25C", "civ/econ/companies", Txt.T("civ_row_companies"), Txt.T("civ_row_companies_d"), WinEcon.Open);
            if (Space.Satellites(k) > 0 || Space.Launches(k) > 0) Row(box, "civ_row_space", Space.Launches(k) + " / " + Space.Satellites(k), "#9A8AFF", "civ/world/space", Txt.T("civ_row_space"), Txt.T("civ_row_space_d"), WinWorld.Open);
            if (Internet.Exists(k)) Row(box, "civ_row_reputation", G.Signed(Internet.Reputation(k) * 100f) + "%", Internet.Reputation(k) >= 0 ? "#8CF07A" : "#FF7A6A", "civ/net/feed", Txt.T("civ_row_reputation"), Txt.T("civ_row_reputation_d"), WinNet.Open);
            if (War.Nukes(k) > 0) Row(box, "civ_row_nukes", War.Nukes(k).ToString(), "#FF5A5A", "civ/world/nuke", Txt.T("civ_row_nukes"), Txt.T("civ_row_nukes_d"));
        }

        public static void Unit(StatsWindow w, Actor a)
        {
            if (!G.Ok(a) || a.kingdom == null || !a.kingdom.isCiv()) return;
            StatsRowsContainer box = w.stats_rows_container;
            string st = Xeno.Status(a);
            if (!string.IsNullOrEmpty(st) && st != Xeno.Citizen) Row(box, "civ_row_status", Txt.T("civ_status_" + st), "#FF9A6A", "civ/status/" + st, Txt.T("civ_status_" + st), Txt.T("civ_status_" + st + "_d"));
            string role = Roles.Of(a);
            if (role.Length > 0) Row(box, "civ_row_role", Txt.T("civ_role_" + role), "#FFD25C", "civ/role/" + role, Txt.T("civ_role_" + role), Txt.T("civ_role_" + role + "_d"));
            Opinion o = Politics.Get(a);
            if (o != null)
            {
                var mine = new List<KeyValueField>();
                string sat = o.Sat > 0.55f ? "#8CF07A" : o.Sat > 0.35f ? "#FFD25C" : "#FF7A6A";
                mine.Add(Row(box, "civ_row_op_gov", Txt.T("civ_p_" + o.Gov), Same(a.kingdom, "gov", o.Gov), "civ/policy/" + o.Gov, Txt.T("civ_p_" + o.Gov), Txt.T("civ_op_gov_d")));
                mine.Add(Row(box, "civ_row_op_ideo", Txt.T("civ_p_" + o.Ideo), Same(a.kingdom, "ideo", o.Ideo), "civ/policy/" + o.Ideo, Txt.T("civ_p_" + o.Ideo), Txt.T("civ_op_ideo_d")));
                mine.Add(Row(box, "civ_row_op_econ", Txt.T("civ_p_" + o.Econ), Same(a.kingdom, "econ", o.Econ), "civ/policy/" + o.Econ, Txt.T("civ_p_" + o.Econ), Txt.T("civ_op_econ_d")));
                string xk = o.Xeno < -0.33f ? "xenophobia" : o.Xeno > 0.33f ? "xenophilia" : "xeno_neutral";
                mine.Add(Row(box, "civ_row_op_xeno", Txt.T("civ_p_" + xk) + " (" + G.Signed(o.Xeno * 100f) + ")", Same(a.kingdom, "xeno", xk), "civ/policy/" + xk, Txt.T("civ_xeno_axis"), Txt.T("civ_op_xeno_d")));
                mine.Add(Row(box, "civ_row_op_sat", G.Pct(o.Sat), sat, "civ/stat/stability", Txt.T("civ_satisfaction"), Txt.T("civ_op_sat_d")));
                Place(box, "creature_statistics_personality", mine);
            }
            string cloth = Clothes.Wearing(a);
            if (!string.IsNullOrEmpty(cloth)) Row(box, "civ_row_clothes", Txt.T("item_" + cloth), null, "civ/item/" + cloth, Txt.T("item_" + cloth), Txt.F("civ_warmth", Clothes.Warmth(a).ToString("0.#")));
            int mcap = Navy.MissileCap(a);
            if (mcap > 0) Row(box, "civ_row_missiles", Navy.Missiles(a) + "/" + mcap, "#FF9A6A", "civ/item/civ_w_missile", Txt.T("civ_row_missiles"), Txt.T("civ_row_missiles_d"));
            string ammo = Ammo.Label(a);
            if (ammo != null) Row(box, "civ_row_ammo", ammo, Ammo.Empty(a) ? "#FF7A6A" : "#FFD25C", "civ/item/civ_w_mg", Txt.T("civ_row_ammo"), Txt.T("civ_row_ammo_d"));
            int f = Internet.Followers(a);
            if (f > 0) Row(box, "civ_row_followers", G.Short(f), "#9ADFFF", "civ/net/people", Internet.TierLabel(f), Names.Handle(a), WinNet.Open);
        }

        private static string Same(Kingdom k, string group, string id)
        {
            return Policies.Id(k, group) == id ? "#8CF07A" : "#FFD25C";
        }

        private static void Place(StatsRowsContainer box, string afterKey, List<KeyValueField> rows)
        {
            if (box == null) return;
            Transform anchor = null;
            foreach (Transform t in box.transform)
            {
                if (t.gameObject.name == "[KV] " + afterKey)
                {
                    anchor = t;
                    break;
                }
            }
            if (anchor == null) return;
            int idx = anchor.GetSiblingIndex();
            foreach (KeyValueField f in rows)
            {
                if (f == null) continue;
                f.transform.SetSiblingIndex(++idx);
            }
        }

        public static void City(StatsWindow w, City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !c.kingdom.isCiv()) return;
            StatsRowsContainer box = w.stats_rows_container;
            if (Energy.Started(c.kingdom))
            {
                float r = Energy.Ratio(c);
                Row(box, "civ_row_energy", G.Pct(Math.Min(9.99f, r)), r >= 1f ? "#8CF07A" : "#FF7A6A", "civ/econ/energy", Txt.T("civ_row_energy"), Txt.F("civ_city_energy_d", G.Short(Energy.Supply(c)), G.Short(Energy.Demand(c)), Energy.PlantCount(c)));
            }
            float pol = Pollution.Level(c);
            if (pol > 0.01f) Row(box, "civ_row_pollution", G.Pct(pol), pol > 0.5f ? "#FF7A6A" : "#FFD25C", "civ/stat/pollution", Txt.T("civ_row_pollution"), Txt.T("civ_row_pollution_d"));
            int wl = Walls.Level(c.kingdom);
            if (wl > 0) Row(box, "civ_row_walls", Txt.T("civ_walls_" + wl), null, "civ/stat/walls", Txt.T("civ_row_walls"), Txt.T("civ_walls_" + wl + "_d"));
            int rails = Rails.LinksOf(c).Count;
            if (rails > 0) Row(box, "civ_row_rails", rails.ToString(), "#C9A05A", "civ/world/rail", Txt.T("civ_row_rails"), Txt.T("civ_row_rails_d"));
        }

        private static GameObject _btn;

        private static string PathTo(Transform root, Transform t)
        {
            var parts = new List<string>();
            while (t != null && t != root)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return string.Join("/", parts.ToArray());
        }

        private static bool CloneTabs(KingdomWindow w)
        {
            UnityEngine.UI.Image fav = w._favorite_icon;
            if (fav == null) return false;
            UnityEngine.UI.Button src = fav.GetComponentInParent<UnityEngine.UI.Button>();
            if (src == null) return false;
            Transform parent = src.transform.parent;
            RectTransform srt = src.GetComponent<RectTransform>();
            float step = srt.rect.height + 4f;
            float best = float.MaxValue;
            foreach (Transform sib in parent)
            {
                if (sib == src.transform || sib.GetComponent<UnityEngine.UI.Button>() == null) continue;
                float d = sib.localPosition.y - srt.localPosition.y;
                if (d > 1f && d < best) best = d;
            }
            if (best < float.MaxValue) step = best;
            bool layout = parent.GetComponent<UnityEngine.UI.LayoutGroup>() != null;
            string iconPath = fav.transform == src.transform ? "" : PathTo(src.transform, fav.transform);
            GameObject tech = Clone(src, parent, iconPath, "ui/Icons/civ/btn/tech", "civ_tech Title", "civ_btn_tech_d", () => WinTech.Open(SelectedMetas.selected_kingdom));
            GameObject net = Clone(src, parent, iconPath, "ui/Icons/civ/btn/net", "civ_net Title", "civ_btn_net_d", WinNet.Open);
            GameObject state = Clone(src, parent, iconPath, "ui/Icons/civ/btn/state", "civ_state Title", "civ_btn_state_d", () => WinState.Open(SelectedMetas.selected_kingdom));
            GameObject ctrl = Clone(src, parent, iconPath, "ui/Icons/civ/btn/control", "civ_control Title", "civ_btn_control_d", () => WinControl.Open(SelectedMetas.selected_kingdom));
            ctrl.name = "CivControl";
            tech.name = "CivTech";
            net.name = "CivNet";
            state.name = "CivState";
            if (!layout)
            {
                tech.transform.localPosition = srt.localPosition - new Vector3(0f, step, 0f);
                net.transform.localPosition = srt.localPosition - new Vector3(0f, step * 2f, 0f);
                state.transform.localPosition = srt.localPosition - new Vector3(0f, step * 3f, 0f);
                ctrl.transform.localPosition = srt.localPosition - new Vector3(0f, step * 4f, 0f);
            }
            else
            {
                tech.transform.SetAsLastSibling();
                net.transform.SetAsLastSibling();
                state.transform.SetAsLastSibling();
                ctrl.transform.SetAsLastSibling();
            }
            _btn = tech;
            return true;
        }

        private static GameObject Clone(UnityEngine.UI.Button src, Transform parent, string iconPath, string icon, string title, string desc, UnityEngine.Events.UnityAction act)
        {
            GameObject go = UnityEngine.Object.Instantiate(src.gameObject, parent);
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is UnityEngine.UI.Graphic || mb is UnityEngine.UI.Button || mb is TipButton || mb is UnityEngine.UI.LayoutElement || mb is UnityEngine.UI.Mask || mb is UnityEngine.UI.BaseMeshEffect) continue;
                UnityEngine.Object.DestroyImmediate(mb);
            }
            UnityEngine.UI.Button b = go.GetComponent<UnityEngine.UI.Button>();
            b.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            b.onClick.AddListener(act);
            Transform it = string.IsNullOrEmpty(iconPath) ? go.transform : go.transform.Find(iconPath);
            UnityEngine.UI.Image img = it != null ? it.GetComponent<UnityEngine.UI.Image>() : null;
            if (img != null)
            {
                img.sprite = UiKit.Icon(icon);
                img.color = Color.white;
                img.preserveAspect = true;
            }
            TipButton tip = go.GetComponent<TipButton>();
            if (tip != null)
            {
                tip.textOnClick = title;
                tip.textOnClickDescription = desc;
                tip.text_description_2 = string.Empty;
                tip.clickAction = null;
                tip.showOnClick = false;
                tip.setHoverAction(tip.showTooltipDefault);
            }
            go.SetActive(true);
            return go;
        }

        public static void Button(KingdomWindow w)
        {
            if (_btn != null) return;
            try
            {
                if (CloneTabs(w)) return;
            }
            catch (Exception e)
            {
                CivMod.Log("kingdom tabs: " + e.Message);
            }
            try
            {
                Transform bg = w.transform.Find("Background");
                if (bg == null) return;
                RectTransform r = bg.GetComponent<RectTransform>();
                float x = r.rect.width / 2f + 13f;
                float y = r.rect.height / 2f - 30f - 3f * 21f;
                SimpleButton b = UiKit.IconBtn(bg, UiKit.Icon("ui/Icons/civ/btn/tech"), () => WinTech.Open(SelectedMetas.selected_kingdom), 18f, new Vector2(x, y), Txt.T("civ_tech Title"), Txt.T("civ_btn_tech_d"));
                SimpleButton n = UiKit.IconBtn(bg, UiKit.Icon("ui/Icons/civ/btn/net"), WinNet.Open, 18f, new Vector2(x, y - 21f), Txt.T("civ_net Title"), Txt.T("civ_btn_net_d"));
                SimpleButton st = UiKit.IconBtn(bg, UiKit.Icon("ui/Icons/civ/btn/state"), () => WinState.Open(SelectedMetas.selected_kingdom), 18f, new Vector2(x, y - 42f), Txt.T("civ_state Title"), Txt.T("civ_btn_state_d"));
                UiKit.IconBtn(bg, UiKit.Icon("ui/Icons/civ/btn/control"), () => WinControl.Open(SelectedMetas.selected_kingdom), 18f, new Vector2(x, y - 63f), Txt.T("civ_control Title"), Txt.T("civ_btn_control_d"));
                _btn = b.gameObject;
                n.gameObject.name = "CivNet";
                st.gameObject.name = "CivState";
            }
            catch (Exception)
            {
            }
        }

        [HarmonyPatch(typeof(KingdomWindow), nameof(KingdomWindow.showMetaRows))]
        private static class KMeta
        {
            private static void Postfix(KingdomWindow __instance)
            {
                CivMod.Safe("kingdom-meta", () => Meta(__instance.meta_rows_container, SelectedMetas.selected_kingdom));
            }
        }

        [HarmonyPatch(typeof(KingdomWindow), "showStatsRows")]
        private static class KStats
        {
            private static void Postfix(KingdomWindow __instance)
            {
                CivMod.Safe("kingdom-stats", () =>
                {
                    Stats(__instance, SelectedMetas.selected_kingdom);
                    Button(__instance);
                });
            }
        }

        [HarmonyPatch(typeof(UnitWindow), "showStatsRows")]
        private static class UStats
        {
            private static void Postfix(UnitWindow __instance)
            {
                CivMod.Safe("unit-stats", () => Unit(__instance, SelectedUnit.unit));
            }
        }

        [HarmonyPatch(typeof(CityWindow), "showStatsRows")]
        private static class CStats
        {
            private static void Postfix(CityWindow __instance)
            {
                CivMod.Safe("city-stats", () => City(__instance, SelectedMetas.selected_city));
            }
        }
    }
}
