using System;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.ui;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class Tab
    {
        private static bool _done;
        private static float _retry;

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
                if (tab == null) tab = PowerButtonCreator.GetTab(PowerTabNames.Other);
                if (tab == null) return;
                Add(tab, "civ_btn_tech", "ui/Icons/civ/btn/tech", () => WinTech.Open(SelectedMetas.selected_kingdom));
                Add(tab, "civ_btn_net", "ui/Icons/civ/btn/net", WinNet.Open);
                Add(tab, "civ_btn_econ", "ui/Icons/civ/btn/econ", WinEcon.Open);
                Add(tab, "civ_btn_world", "ui/Icons/civ/btn/world", WinWorld.Open);
                Add(tab, "civ_btn_routes", "ui/Icons/civ/btn/routes", WinRoute.Open);
                Add(tab, "civ_btn_config", "ui/Icons/civ/btn/config", () =>
                {
                    if (CivMod.Instance != null) ModConfigureWindow.ShowWindow(CivMod.Instance.GetConfig());
                });
            }
            catch (Exception e)
            {
                Debug.LogError("[Civilizations] tab: " + e);
            }
        }

        private static void Add(PowersTab tab, string id, string icon, Action a)
        {
            PowerButton b = PowerButtonCreator.CreateSimpleButton(id, () => a(), UiKit.Icon(icon));
            PowerButtonCreator.AddButtonToTab(b, tab);
        }
    }
}
