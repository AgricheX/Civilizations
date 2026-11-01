using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class CityTechUi
    {
        private const string Root = "civ_city_tech";

        private static readonly Dictionary<CityWindow, Transform> _area = new Dictionary<CityWindow, Transform>();

        private static Transform FindArea(CityWindow w)
        {
            Transform cached;
            if (_area.TryGetValue(w, out cached) && cached != null) return cached;
            foreach (Text x in w.GetComponentsInChildren<Text>(true))
            {
                string s = x.text ?? "";
                if (s.IndexOf("Work In Progress", StringComparison.OrdinalIgnoreCase) < 0 && s.IndexOf("Nothing to see", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Transform el = x.transform;
                foreach (WindowMetaTab tab in w.GetComponentsInChildren<WindowMetaTab>(true))
                {
                    if (tab == null || tab.tab_elements == null) continue;
                    foreach (Transform te in tab.tab_elements)
                    {
                        if (te != null && el.IsChildOf(te))
                        {
                            Transform box = el;
                            while (box.parent != null && box.parent != te) box = box.parent;
                            if (box == el) box = te;
                            _area[w] = box;
                            return box;
                        }
                    }
                }
                Transform p = el.parent;
                if (p != null)
                {
                    _area[w] = p;
                    return p;
                }
            }
            if (CivMod.C != null && CivMod.C.DebugLog) CivMod.Log("city tech: placeholder not found");
            return null;
        }

        public static void Show(CityWindow w)
        {
            City c = SelectedMetas.selected_city;
            Transform area = FindArea(w);
            if (area == null) return;
            Transform old = area.Find(Root);
            bool ours = G.Ok(c) && c.kingdom != null && c.kingdom.isCiv();
            foreach (Transform ch in area)
            {
                if (ch.name == Root) continue;
                ch.gameObject.SetActive(!ours);
            }
            if (old != null)
            {
                old.gameObject.SetActive(false);
                UiKit.Clear(old);
                UnityEngine.Object.Destroy(old.gameObject);
            }
            if (!ours) return;
            RectTransform art = area as RectTransform;
            float visible = art != null ? art.rect.height : 240f;
            ScrollRect wsr = area.GetComponentInParent<ScrollRect>();
            if (art != null && wsr != null && wsr.viewport != null)
            {
                var ac = new Vector3[4];
                var vc = new Vector3[4];
                art.GetWorldCorners(ac);
                wsr.viewport.GetWorldCorners(vc);
                float sy = Mathf.Abs(art.lossyScale.y) < 0.0001f ? 1f : art.lossyScale.y;
                visible = Mathf.Max(160f, (ac[1].y - vc[0].y) / sy - 6f);
            }
            var go = new GameObject(Root, typeof(RectTransform));
            go.transform.SetParent(area, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -2f);
            rt.sizeDelta = new Vector2(UiKit.W + 14f, visible);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            go.AddComponent<RectMask2D>();
            var cgo = new GameObject("Content", typeof(RectTransform));
            cgo.transform.SetParent(go.transform, false);
            RectTransform content = cgo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(-5f, 0f);
            content.sizeDelta = new Vector2(UiKit.W, 10f);
            UiKit.Setup(content);
            Build(content, c);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var sgo = new GameObject("Scrollbar", typeof(RectTransform));
            sgo.transform.SetParent(go.transform, false);
            RectTransform srt = sgo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.sizeDelta = new Vector2(6f, -4f);
            srt.anchoredPosition = new Vector2(-1f, 0f);
            Image track = sgo.AddComponent<Image>();
            track.sprite = UiKit.S("ui/special/windowInnerSliced");
            track.type = Image.Type.Sliced;
            track.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);
            var hgo = new GameObject("Handle", typeof(RectTransform));
            hgo.transform.SetParent(sgo.transform, false);
            RectTransform hrt = hgo.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.sizeDelta = Vector2.zero;
            Image handle = hgo.AddComponent<Image>();
            handle.sprite = UiKit.S("ui/special/windowInnerSliced");
            handle.type = Image.Type.Sliced;
            handle.color = new Color(0.86f, 0.25f, 0.2f, 1f);
            Scrollbar bar = sgo.AddComponent<Scrollbar>();
            bar.handleRect = hrt;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            ScrollRect sr = go.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = rt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 18f;
            sr.verticalScrollbar = bar;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            sr.verticalNormalizedPosition = 1f;
            if (art != null && art.rect.height < visible)
            {
                LayoutElement ale = area.GetComponent<LayoutElement>() ?? area.gameObject.AddComponent<LayoutElement>();
                ale.minHeight = visible;
                ale.preferredHeight = visible;
            }
        }

        private static void Build(Transform p, City c)
        {
            Kingdom k = c.kingdom;
            int era = Research.Era(k);
            EraDef ed = Db.Eras[Math.Max(0, Math.Min(Db.Eras.Count - 1, era))];
            UiKit.Header(p, Txt.T("civ_ct_title") + " · " + k.data.name, null, "ui/Icons/civ/era/" + ed.Id);
            Color ec;
            if (!ColorUtility.TryParseHtmlString(ed.Color, out ec)) ec = UiKit.Gold;
            UiKit.Stat(p, "ui/Icons/civ/era/" + ed.Id, Txt.T("civ_row_era"), Db.EraName(era), ec);
            int cap = Research.Cap(k);
            if (cap >= 0) UiKit.Stat(p, "ui/Icons/civ/cfg/lock_ages", Txt.T("civ_cap_short"), Db.EraName(cap), UiKit.Bad);
            RectTransform eb = UiKit.Box(p, "EraBar", 12f);
            float ef = Math.Min(1f, Research.EraFraction(k, era) / 0.65f);
            UiKit.Bar(eb, ef, ec, new Vector2(UiKit.W - 10f, 10f), Vector2.zero, G.Pct(ef));
            TechDef cur = Research.Current(k);
            UiKit.Stat(p, "ui/Icons/civ/stat/research", Txt.T("civ_row_research"), cur != null ? Db.TechName(cur.Id) : "-", UiKit.Cyan);
            if (cur != null)
            {
                RectTransform rb = UiKit.Box(p, "ResBar", 12f);
                float cost = Math.Max(0.01f, Research.Cost(k, cur));
                float pr = Mathf.Clamp01(Research.Progress(k) / cost);
                UiKit.Bar(rb, pr, UiKit.Cyan, new Vector2(UiKit.W - 10f, 10f), Vector2.zero, G.Pct(pr));
                UiKit.Stat(p, "ui/Icons/civ/stat/research", Txt.T("civ_rate"), Research.Rate(k, cur).ToString("0.00"), UiKit.Soft);
            }
            float total = 0f;
            foreach (City o in G.Cities(k)) total += Roles.ResearchPower(o);
            float mine = Roles.ResearchPower(c);
            UiKit.Stat(p, "ui/Icons/civ/stat/research", Txt.T("civ_ct_share"), total > 0.01f ? G.Pct(mine / total) : "-", UiKit.Good);
            int libs = c.countBuildingsType("type_library");
            int schools = c.countBuildingsType("type_civ_school");
            int unis = c.countBuildingsType("type_civ_university");
            int labs = c.countBuildingsType("type_civ_lab");
            UiKit.Stat(p, null, Txt.T("civ_ct_places"), libs + " / " + schools + " / " + unis + " / " + labs, UiKit.Soft);
            UiKit.Label(p, Txt.T("civ_ct_places_d"), 11f, 7, TextAnchor.MiddleCenter, UiKit.Dim);
            TechSet known = Research.Known(k);
            List<TechDef> list = ed.Techs.Where(t => !t.Rare || known.Has(t)).ToList();
            int have = list.Count(t => known.Has(t));
            UiKit.Label(p, Txt.F("civ_ct_era_techs", have, list.Count), 13f, 8, TextAnchor.MiddleCenter, UiKit.Gold);
            const int per = 11;
            const float size = 17f;
            int shown = Math.Min(list.Count, per * 3);
            var ordered = list.OrderByDescending(t => known.Has(t)).ThenBy(t => t.Index).Take(shown).ToList();
            for (int r = 0; r * per < ordered.Count; r++)
            {
                RectTransform row = UiKit.Box(p, "Row", size + 2f);
                int n = Math.Min(per, ordered.Count - r * per);
                float x0 = -(n - 1) * (size + 2f) / 2f;
                for (int i = 0; i < n; i++)
                {
                    TechDef t = ordered[r * per + i];
                    bool on = known.Has(t);
                    Kingdom kk = k;
                    UiKit.IconBtn(row, UiKit.Icon(t.Icon, "ui/Icons/civ/cat/" + t.Cat), () => WinTech.Open(kk), size, new Vector2(x0 + i * (size + 2f), 0f), Db.TechName(t.Id), TechInfo.Describe(t, k), on, true);
                }
            }
            UiKit.Gap(p, 2f);
            RectTransform br = UiKit.Box(p, "Open", 22f);
            Kingdom k2 = k;
            UiKit.Btn(br, Txt.T("civ_ct_open"), UiKit.Icon("ui/Icons/civ/btn/tech"), () => WinTech.Open(k2), new Vector2(150f, 20f), Vector2.zero);
        }

        [HarmonyPatch(typeof(CityWindow), nameof(CityWindow.startShowingWindow))]
        private static class OnShow
        {
            private static void Postfix(CityWindow __instance)
            {
                CivMod.Safe("city-tech", () => Show(__instance));
            }
        }
    }
}
