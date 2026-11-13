using System;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class Hud
    {
        private const float Wd = 176f;
        private static float _next;
        private static GameObject _root;
        private static RectTransform _box;
        private static RectTransform _rootRt;
        private const float Scale = 0.62f;

        private static bool Enabled
        {
            get { return CivMod.C != null && CivMod.C.Hud; }
        }

        private static Kingdom Target()
        {
            if (Player.Active) return Player.K;
            Kingdom k = SelectedMetas.selected_kingdom;
            if (G.Ok(k) && k.isCiv()) return k;
            return EraLock.Leader;
        }

        private static bool Ensure()
        {
            if (_root != null) return true;
            if (CanvasMain.instance == null || CanvasMain.instance.canvas_ui == null) return false;
            _root = new GameObject("CivProgressHud", typeof(RectTransform));
            _root.transform.SetParent(CanvasMain.instance.canvas_ui.transform, false);
            _rootRt = _root.GetComponent<RectTransform>();
            _rootRt.anchorMin = new Vector2(1f, 1f);
            _rootRt.anchorMax = new Vector2(1f, 1f);
            _rootRt.pivot = new Vector2(1f, 1f);
            _rootRt.anchoredPosition = new Vector2(-6f, -40f);
            _rootRt.sizeDelta = new Vector2(Wd, 60f);
            _rootRt.localScale = Vector3.one * Scale;
            var inner = new GameObject("Box", typeof(RectTransform));
            inner.transform.SetParent(_root.transform, false);
            _box = inner.GetComponent<RectTransform>();
            _box.anchorMin = new Vector2(0.5f, 0.5f);
            _box.anchorMax = new Vector2(0.5f, 0.5f);
            _box.pivot = new Vector2(0.5f, 0.5f);
            _box.anchoredPosition = Vector2.zero;
            _box.sizeDelta = new Vector2(Wd, 60f);
            _box.localScale = Vector3.one;
            return true;
        }

        private static void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        public static void Tick()
        {
            if (!Enabled || !G.Loaded || ScrollWindow.isWindowActive())
            {
                Hide();
                return;
            }
            if (Time.unscaledTime < _next && _root != null && _root.activeSelf) return;
            _next = Time.unscaledTime + 1f;
            CivMod.Safe("hud", Build);
        }

        private static float Progress(Kingdom k, int era)
        {
            return era >= Db.LastEra ? 1f : Mathf.Clamp01(Research.EraFraction(k, era) / 0.65f);
        }

        private static void Build()
        {
            Kingdom k = Target();
            if (!G.Ok(k) || !Ensure())
            {
                Hide();
                return;
            }
            if (!_root.activeSelf) _root.SetActive(true);
            UiKit.Clear(_box);
            int era = Math.Max(0, Research.Era(k));
            TechDef cur = Research.Current(k);
            int cap = Research.Cap(k);
            Kingdom lead = EraLock.Leader;
            bool showLead = G.Ok(lead) && lead != k;
            float h = 8f + 20f + 26f + 26f + (cap >= 0 ? 13f : 0f) + (showLead ? 13f : 0f) + 6f;
            _box.sizeDelta = new Vector2(Wd, h);
            _rootRt.sizeDelta = new Vector2(Wd, h);
            Image bg = UiKit.Panel(_box);
            bg.color = new Color(1f, 1f, 1f, 0.95f);
            float half = Wd / 2f;
            float y = h / 2f - 8f;

            UiKit.Banner(_box, k, new Vector2(-half + 16f, y - 10f), 18f);
            UiKit.Text(_box, G.Name(k), new Vector2(10f, y - 10f), new Vector2(Wd - 40f, 16f), 10, TextAnchor.MiddleLeft, UiKit.Gold);
            y -= 20f;

            Color ec = G.Col(Db.Eras[era].Color);
            UiKit.Img(_box, EraLock.EraSprite(era), new Vector2(18f, 18f), new Vector2(-half + 16f, y - 13f));
            UiKit.Text(_box, Db.EraName(era), new Vector2(10f, y - 7f), new Vector2(Wd - 40f, 11f), 8, TextAnchor.MiddleLeft, ec);
            float ep = Progress(k, era);
            UiKit.Bar(_box, ep, ec, new Vector2(Wd - 40f, 9f), new Vector2(10f, y - 19f), Mathf.RoundToInt(ep * 100f) + "%");
            y -= 26f;

            Sprite ri = cur != null ? UiKit.Icon(cur.Icon, "ui/Icons/civ/cat/" + cur.Cat) : UiKit.Icon("ui/Icons/civ/unknown");
            UiKit.Img(_box, ri, new Vector2(16f, 16f), new Vector2(-half + 16f, y - 13f));
            string line = cur != null ? Txt.T("civ_researching") + " " + Db.TechName(cur.Id) : Txt.T("civ_researching_none");
            UiKit.Text(_box, line, new Vector2(10f, y - 7f), new Vector2(Wd - 40f, 11f), 8, TextAnchor.MiddleLeft, UiKit.Soft);
            float rp = cur != null ? Mathf.Clamp01(Research.Progress(k) / Math.Max(1f, Research.Cost(k, cur))) : 0f;
            UiKit.Bar(_box, rp, new Color(0.35f, 0.75f, 1f), new Vector2(Wd - 40f, 9f), new Vector2(10f, y - 19f), Mathf.RoundToInt(rp * 100f) + "%");
            y -= 26f;

            if (cap >= 0)
            {
                UiKit.Text(_box, Txt.F("civ_hud_cap", Db.EraName(cap)), new Vector2(0f, y - 6f), new Vector2(Wd - 12f, 11f), 7, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.45f));
                y -= 13f;
            }
            if (showLead)
            {
                int le = Math.Max(0, Research.Era(lead));
                string ll = Txt.T("civ_hud_leader") + " " + G.Name(lead) + " · " + Db.EraName(le) + " " + Mathf.RoundToInt(Progress(lead, le) * 100f) + "%";
                UiKit.Text(_box, ll, new Vector2(0f, y - 6f), new Vector2(Wd - 12f, 11f), 7, TextAnchor.MiddleCenter, UiKit.Dim);
            }
        }
    }
}
