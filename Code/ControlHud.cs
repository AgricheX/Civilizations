using System;
using System.Collections.Generic;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class ControlHud
    {
        private const float Wd = 560f, Ht = 74f;
        private static GameObject _root;
        private static RectTransform _box;
        private static Transform _vanilla;
        private static bool _hidVanilla;
        private static float _next;
        private static long _built = -1;
        private static Text _info;
        private static Text _stats;
        private static SimpleButton _pause;
        private static readonly List<GameObject> _hidden = new List<GameObject>();

        public static bool Shown
        {
            get { return _root != null && _root.activeSelf; }
        }

        private static bool Wanted
        {
            get { return Player.Active && G.Loaded && !CosmosScale.Busy; }
        }

        public static void Tick()
        {
            if (!Wanted)
            {
                Hide();
                return;
            }
            if (!Ensure()) return;
            Kingdom k = Player.K;
            if (k.data.id != _built) Build(k);
            if (!_root.activeSelf) _root.SetActive(true);
            Vanilla(false);
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            CivMod.Safe("control-hud", () => Refresh(k));
        }

        private static void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
            Vanilla(true);
        }

        private static void Vanilla(bool show)
        {
            if (show && !_hidVanilla) return;
            if (!show && _hidVanilla) return;
            try
            {
                if (show)
                {
                    foreach (GameObject g in _hidden) if (g != null) g.SetActive(true);
                    _hidden.Clear();
                    _hidVanilla = false;
                    return;
                }
                if (CanvasMain.instance == null || CanvasMain.instance.canvas_ui == null) return;
                if (_vanilla == null) _vanilla = CanvasMain.instance.canvas_ui.transform.Find("CanvasBottom/BottomElements");
                if (_vanilla != null && _vanilla.gameObject.activeSelf)
                {
                    _vanilla.gameObject.SetActive(false);
                    _hidden.Add(_vanilla.gameObject);
                }
                _hidVanilla = true;
            }
            catch (Exception e)
            {
                CivMod.Report("control-hud-vanilla", e);
                _hidVanilla = !show;
            }
        }

        private static bool Ensure()
        {
            if (_root != null) return true;
            if (CanvasMain.instance == null || CanvasMain.instance.canvas_ui == null) return false;
            _root = new GameObject("CivControlHud", typeof(RectTransform));
            _root.transform.SetParent(CanvasMain.instance.canvas_ui.transform, false);
            RectTransform rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            rt.sizeDelta = new Vector2(Wd, Ht);
            var inner = new GameObject("Box", typeof(RectTransform));
            inner.transform.SetParent(_root.transform, false);
            _box = inner.GetComponent<RectTransform>();
            _box.anchorMin = _box.anchorMax = _box.pivot = new Vector2(0.5f, 0.5f);
            _box.sizeDelta = new Vector2(Wd, Ht);
            _built = -1;
            return true;
        }

        private static void Build(Kingdom k)
        {
            _built = k.data.id;
            UiKit.Clear(_box);
            Image bg = UiKit.Panel(_box);
            bg.color = new Color(1f, 1f, 1f, 0.96f);
            float half = Wd / 2f;
            UiKit.Banner(_box, k, new Vector2(-half + 22f, 12f), 30f);
            _info = UiKit.Text(_box, "", new Vector2(-half + 42f + 110f, 22f), new Vector2(220f, 14f), 10, TextAnchor.MiddleLeft, UiKit.Gold);
            _stats = UiKit.Text(_box, "", new Vector2(-half + 42f + 250f, 6f), new Vector2(500f, 12f), 8, TextAnchor.MiddleLeft, UiKit.Soft);
            float x = -half + 18f;
            float y = -20f;
            const float s = 24f, step = 27f;
            Cmd(ref x, y, s, step, "ui/Icons/civ/ctrl/overview", "civ_ctrl_tab_overview", () => WinControl.OpenTab(0));
            Cmd(ref x, y, s, step, "ui/Icons/civ/ctrl/research", "civ_ctrl_tab_research", () => WinControl.OpenTab(1));
            Cmd(ref x, y, s, step, "ui/Icons/civ/ctrl/politics", "civ_ctrl_tab_politics", () => WinControl.OpenTab(2));
            Cmd(ref x, y, s, step, "ui/Icons/civ/ctrl/economy", "civ_ctrl_tab_economy", () => WinControl.OpenTab(3));
            Cmd(ref x, y, s, step, "ui/Icons/civ/ctrl/war", "civ_ctrl_tab_war", () => WinControl.OpenTab(4));
            Cmd(ref x, y, s, step, "ui/Icons/civ/ctrl/space", "civ_ctrl_tab_space", () => WinControl.OpenTab(5));
            x += 8f;
            Cmd(ref x, y, s, step, "ui/Icons/civ/btn/tech", "civ_btn_tech", () => WinTech.Open(Player.K));
            Cmd(ref x, y, s, step, "ui/Icons/civ/btn/state", "civ_btn_state", () => WinState.Open(Player.K));
            Cmd(ref x, y, s, step, "ui/Icons/civ/btn/econ", "civ_btn_econ", WinEcon.Open);
            Cmd(ref x, y, s, step, "ui/Icons/civ/btn/world", "civ_btn_world", WinWorld.Open);
            Cmd(ref x, y, s, step, "ui/Icons/civ/state/overview", "civ_hud_capital", Focus);
            Cmd(ref x, y, s, step, "ui/Icons/civ/state/relations", "civ_hud_kingdom", () =>
            {
                Kingdom pk = Player.K;
                if (!G.Ok(pk)) return;
                SelectedMetas.selected_kingdom = pk;
                UiKit.Open("kingdom");
            });
            x += 8f;
            _pause = Cmd(ref x, y, s, step, "ui/Icons/civ/hud/pause", "civ_hud_pause", () =>
            {
                Config.paused = !Config.paused;
                _next = 0f;
            });
            Cmd(ref x, y, s, step, "ui/Icons/civ/hud/save", "civ_hud_save", () => UiKit.Open("saves_list"));
            Cmd(ref x, y, s, step, "ui/Icons/civ/btn/config", "civ_hud_settings", () => UiKit.Open("settings"));
            Cmd(ref x, y, s, step, "ui/Icons/civ/hud/quit", "civ_hud_quit", () => UiKit.Open("quit_game"));
            Cmd(ref x, y, s, step, "ui/Icons/civ/hud/release", "civ_hud_release", () =>
            {
                Player.Release();
                Notify.Show(Txt.T("civ_hud_released"));
            });
        }

        private static SimpleButton Cmd(ref float x, float y, float size, float step, string icon, string key, Action a)
        {
            Action act = a;
            SimpleButton b = UiKit.IconBtn(_box, UiKit.Icon(icon), () => CivMod.Safe("hud-cmd", act), size, new Vector2(x, y), Txt.T(key), Txt.Has(key + "_d") ? Txt.T(key + "_d") : null);
            x += step;
            return b;
        }

        private static void Focus()
        {
            Kingdom k = Player.K;
            if (!G.Ok(k) || k.capital == null) return;
            WorldTile t = G.Tile(k.capital);
            if (t == null) return;
            World.world.move_camera.focusOn(t.posV3);
        }

        private static void Refresh(Kingdom k)
        {
            if (_info == null || _stats == null) return;
            int era = Math.Max(0, Research.Era(k));
            _info.text = G.Name(k) + "  ·  " + Db.EraName(era) + "  ·  " + Txt.T("civ_stage_" + Politics.Stage(k));
            int gold = 0;
            foreach (City c in G.Cities(k)) gold += Economy.Amount(c, "gold");
            int wars = 0;
            foreach (global::War w in World.world.wars.getWars(k)) if (w != null && !w.isRekt()) wars++;
            _stats.text = Txt.T("civ_ctrl_gold") + " " + gold
                + "   " + Txt.T("civ_ctrl_pop") + " " + k.getPopulationPeople()
                + "   " + Txt.T("civ_ctrl_cities") + " " + G.Cities(k).Count
                + "   " + Txt.T("civ_ctrl_stability") + " " + Mathf.RoundToInt(Politics.Stability(k) * 100f) + "%"
                + "   " + Txt.T("civ_ctrl_legitimacy") + " " + Mathf.RoundToInt(Legitimacy.Get(k) * 100f) + "%"
                + "   " + Txt.T("civ_ctrl_wars") + " " + wars
                + "   " + Txt.F("civ_hud_year", G.Year);
            if (_pause != null && _pause.Icon != null) _pause.Icon.sprite = UiKit.Icon(Config.paused ? "ui/Icons/civ/hud/play" : "ui/Icons/civ/hud/pause");
        }
    }
}
