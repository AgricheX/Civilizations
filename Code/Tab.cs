using System;
using System.Collections.Generic;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.ui;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal class TabGroup
    {
        public string Id;
        public PowersTab Tab;
        public Color Tint;
        public GameObject Line;
        public readonly List<PowerButton> Buttons = new List<PowerButton>();
    }

    internal static class Tab
    {
        public const string GCommunity = "community", GWindows = "windows", GTools = "tools", GFavs = "favs";
        private static bool _done;
        private static float _retry;
        private static PowersTab _main;
        private static PowersTab _sandbox;
        private static readonly Dictionary<string, TabGroup> _groups = new Dictionary<string, TabGroup>();
        private static readonly List<TabGroup> _order = new List<TabGroup>();

        public static bool Ready { get { return _done; } }

        public static PowersTab SandboxTab { get { return _sandbox ?? _main; } }

        public static void Try()
        {
            if (_done) return;
            _retry -= Time.unscaledDeltaTime;
            if (_retry > 0f) return;
            _retry = 2f;
            if (CanvasMain.instance == null) return;
            _done = true;
            try
            {
                WinTech.Create();
                WinNet.Create();
                WinEcon.Create();
                WinWorld.Create();
                WinRoute.Create();
                WinState.Create();
                WinCosmos.Create();
                WinSandbox.Create();
                WinControl.Create();
                WinSaga.Create();
                WinUpdate.Create();
            }
            catch (Exception e)
            {
                Debug.LogError("[Civilizations] windows: " + e);
            }
            try
            {
                _main = Make("civilizations", "civ_tab", "ui/Icons/civ/tab");
                if (_main == null) _main = PowerButtonCreator.GetTab(PowerTabNames.Other);
                if (_main == null) return;
                _sandbox = Make("civ_sandbox", "civ_tab_sandbox", "ui/Icons/civ/btn/sandbox");
                Group(_main, GCommunity, new Color(1f, 0.85f, 0.5f));
                Group(_main, GWindows, new Color(0.65f, 0.8f, 1f));
                Group(_main, GTools, new Color(0.7f, 1f, 0.7f));
                Add(GCommunity, "civ_btn_nexus", "ui/Icons/civ/btn/nexus", () => Application.OpenURL(Updates.NexusHome));
                Add(GCommunity, "civ_btn_discord", "ui/Icons/civ/btn/discord", () => Application.OpenURL(Updates.DiscordUrl));
                Add(GCommunity, "civ_btn_config", "ui/Icons/civ/btn/config", () =>
                {
                    if (CivMod.Instance != null) ModConfigureWindow.ShowWindow(CivMod.Instance.GetConfig());
                });
                Add(GWindows, "civ_btn_tech", "ui/Icons/civ/btn/tech", () => WinTech.Open(SelectedMetas.selected_kingdom));
                Add(GWindows, "civ_btn_state", "ui/Icons/civ/btn/state", () => WinState.Open(SelectedMetas.selected_kingdom));
                Add(GWindows, "civ_btn_econ", "ui/Icons/civ/btn/econ", WinEcon.Open);
                Add(GWindows, "civ_btn_net", "ui/Icons/civ/btn/net", WinNet.Open);
                Add(GWindows, "civ_btn_world", "ui/Icons/civ/btn/world", WinWorld.Open);
                Add(GWindows, "civ_btn_routes", "ui/Icons/civ/btn/routes", WinRoute.Open);
                Add(GWindows, "civ_btn_cosmos", "ui/Icons/civ/btn/cosmos", WinCosmos.Open);
                Add(GWindows, "civ_btn_control", "ui/Icons/civ/btn/control", () => WinControl.Open(SelectedMetas.selected_kingdom));
                try
                {
                    Put(GTools, PowerButtonCreator.CreateGodPowerButton(CosmosScale.PowerId, UiKit.Icon("ui/Icons/civ/btn/enter")));
                }
                catch (Exception e)
                {
                    Debug.LogError("[Civilizations] enter: " + e);
                }
                try
                {
                    Sandbox.Tab = SandboxTab;
                    Sandbox.Build();
                    SandboxTabs.Build(SandboxTab);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Civilizations] sandbox: " + e);
                }
                Relayout();
                Biomes.Ui();
            }
            catch (Exception e)
            {
                Debug.LogError("[Civilizations] tab: " + e);
            }
        }

        private static PowersTab Make(string id, string key, string icon)
        {
            try
            {
                PowersTab tab = TabManager.CreateTab(id, key, key + " Description", UiKit.Icon(icon));
                GameObject entry = GameObject.Find("Button_" + id);
                Transform t = entry != null ? entry.transform.Find("Icon") : null;
                Image img = t != null ? t.GetComponent<Image>() : null;
                if (img != null) img.preserveAspect = true;
                return tab;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Civilizations] tab " + id + ": " + e.Message);
                return null;
            }
        }

        public static void Group(PowersTab tab, string id, Color tint)
        {
            if (tab == null || _groups.ContainsKey(id)) return;
            var g = new TabGroup { Id = id, Tab = tab, Tint = tint };
            _groups[id] = g;
            _order.Add(g);
        }

        public static void Add(string group, string id, string icon, Action a)
        {
            Put(group, PowerButtonCreator.CreateSimpleButton(id, () => a(), UiKit.Icon(icon)));
        }

        public static void Label(string id, string name, string desc)
        {
            try
            {
                LM.AddToCurrentLocale(id, name ?? id);
                LM.AddToCurrentLocale(id + " Description", desc ?? "");
            }
            catch (Exception)
            {
            }
        }

        private static bool First(TabGroup g)
        {
            foreach (TabGroup o in _order)
            {
                if (o.Tab != g.Tab) continue;
                if (o == g) return true;
                if (o.Buttons.Count > 0) return false;
            }
            return true;
        }

        private static int After(TabGroup g)
        {
            int idx = -1;
            bool past = false;
            foreach (TabGroup o in _order)
            {
                if (o.Tab != g.Tab) continue;
                if (o.Line != null) idx = Math.Max(idx, o.Line.transform.GetSiblingIndex());
                foreach (PowerButton b in o.Buttons) if (b != null) idx = Math.Max(idx, b.transform.GetSiblingIndex());
                if (o == g) past = true;
                if (past) break;
            }
            return idx + 1;
        }

        public static void Put(string group, PowerButton b)
        {
            if (b == null) return;
            TabGroup g;
            if (!_groups.TryGetValue(group, out g))
            {
                PowersTab fallback = group == GFavs ? SandboxTab : _main;
                if (fallback == null) return;
                Group(fallback, group, Color.white);
                g = _groups[group];
            }
            if (g.Line == null && g.Buttons.Count == 0 && !First(g)) Line(g);
            int at = After(g);
            PowerButtonCreator.AddButtonToTab(b, g.Tab);
            if (at >= 0 && at < g.Tab.transform.childCount) b.transform.SetSiblingIndex(at);
            g.Buttons.Add(b);
            Image bg = b.GetComponent<Image>();
            if (bg != null) bg.color = Color.Lerp(Color.white, g.Tint, 0.45f);
        }

        private static void Line(TabGroup g)
        {
            try
            {
                GameObject src = ResourcesFinder.FindResource<GameObject>("_line");
                if (src == null) return;
                GameObject line = UnityEngine.Object.Instantiate(src, g.Tab.transform);
                line.name = "_line_civ_" + g.Id;
                Image img = line.GetComponent<Image>();
                if (img != null)
                {
                    img.enabled = true;
                    img.color = Color.Lerp(Color.white, g.Tint, 0.6f);
                }
                line.SetActive(true);
                int at = After(g);
                if (at >= 0 && at < g.Tab.transform.childCount) line.transform.SetSiblingIndex(at);
                g.Line = line;
            }
            catch (Exception)
            {
            }
        }

        public static void Remove(PowerButton b)
        {
            if (b == null) return;
            foreach (TabGroup g in _order)
            {
                if (!g.Buttons.Remove(b)) continue;
                if (g.Tab != null) g.Tab._power_buttons.Remove(b);
                if (g.Buttons.Count == 0 && g.Line != null)
                {
                    g.Line.SetActive(false);
                    UnityEngine.Object.Destroy(g.Line);
                    g.Line = null;
                }
            }
            b.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(b.gameObject);
            Relayout();
        }

        public static void Show(string group, bool on)
        {
            TabGroup g;
            if (!_groups.TryGetValue(group, out g)) return;
            foreach (PowerButton b in g.Buttons) if (b != null) b.gameObject.SetActive(on);
            if (g.Line != null) g.Line.SetActive(on);
        }

        public static void Relayout()
        {
            var tabs = new HashSet<PowersTab>();
            foreach (TabGroup g in _order) if (g.Tab != null) tabs.Add(g.Tab);
            foreach (PowersTab t in tabs)
            {
                try
                {
                    t.sortButtons();
                    t.setNewWidth();
                    t.findNeighbours(true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Civilizations] tab layout: " + e.Message);
                }
            }
        }
    }
}
