using System;
using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.ui;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class Tab
    {
        public const string GCommunity = "community", GWindows = "windows", GTools = "tools", GFavs = "favs";
        private static bool _done;
        private static bool _own;
        private static PowersTab _tab;
        private static float _retry;

        public static bool Ready { get { return _done; } }

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
                PowersTab tab = null;
                try
                {
                    tab = TabManager.CreateTab("civilizations", "civ_tab", "civ_tab Description", UiKit.Icon("ui/Icons/civ/tab"));
                    GameObject entry = GameObject.Find("Button_civilizations");
                    Transform icon = entry != null ? entry.transform.Find("Icon") : null;
                    Image img = icon != null ? icon.GetComponent<Image>() : null;
                    if (img != null) img.preserveAspect = true;
                }
                catch (Exception)
                {
                    tab = null;
                }
                _own = tab != null;
                if (tab == null) tab = PowerButtonCreator.GetTab(PowerTabNames.Other);
                if (tab == null) return;
                _tab = tab;
                if (_own)
                {
                    try
                    {
                        tab.SetLayout(new List<string> { GCommunity, GWindows, GTools, GFavs });
                    }
                    catch (Exception)
                    {
                        _own = false;
                    }
                }
                Add(GCommunity, "civ_btn_nexus", "ui/Icons/civ/btn/nexus", () => Application.OpenURL(Updates.NexusUrl));
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
                    Sandbox.Tab = tab;
                    Sandbox.Build();
                    Sandbox.Button = PowerButtonCreator.CreateGodPowerButton(Sandbox.PowerId, UiKit.Icon("ui/Icons/civ/btn/sandbox"));
                    Put(GTools, Sandbox.Button);
                    for (int i = 0; i < Sandbox.Cats.Length; i++)
                    {
                        int cat = i;
                        Add(GTools, "civ_btn_sb_" + Sandbox.Cats[i], "ui/Icons/civ/sb/" + Sandbox.Cats[i], () => WinSandbox.Open(cat));
                    }
                    Sandbox.FavButtons();
                }
                catch (Exception e)
                {
                    Debug.LogError("[Civilizations] sandbox: " + e);
                }
                Relayout();
            }
            catch (Exception e)
            {
                Debug.LogError("[Civilizations] tab: " + e);
            }
        }

        private static void Add(string group, string id, string icon, Action a)
        {
            Put(group, PowerButtonCreator.CreateSimpleButton(id, () => a(), UiKit.Icon(icon)));
        }

        public static void Put(string group, PowerButton b)
        {
            if (_tab == null || b == null) return;
            if (_own)
            {
                try
                {
                    _tab.AddPowerButton(group, b);
                    return;
                }
                catch (Exception)
                {
                }
            }
            PowerButtonCreator.AddButtonToTab(b, _tab);
        }

        public static void Remove(PowerButton b)
        {
            if (b == null) return;
            if (_own && _tab != null)
            {
                try
                {
                    object w = AccessTools.Method(typeof(PowersTabExtension), "_getWrappedPowersTab").Invoke(null, new object[] { _tab });
                    var groups = Traverse.Create(w).Field("ButtonGroups").GetValue() as Dictionary<string, List<PowerButton>>;
                    if (groups != null) foreach (List<PowerButton> l in groups.Values) l.Remove(b);
                }
                catch (Exception)
                {
                }
            }
            UnityEngine.Object.Destroy(b.gameObject);
            Relayout();
        }

        public static void Relayout()
        {
            if (!_own || _tab == null) return;
            try
            {
                _tab.UpdateLayout();
            }
            catch (Exception e)
            {
                Debug.LogError("[Civilizations] tab layout: " + e);
            }
        }
    }
}
