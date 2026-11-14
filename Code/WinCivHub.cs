using System;
using UnityEngine;

namespace Civilizations
{
    internal class WinCivHub : GovWindow<WinCivHub>
    {
        public const string Id = "civ_hub";

        private static readonly string[][] Entries =
        {
            new[] { "tech", "ui/Icons/civ/btn/tech", "civ_tech Title", "civ_btn_tech_d" },
            new[] { "state", "ui/Icons/civ/btn/state", "civ_state Title", "civ_btn_state_d" },
            new[] { "econ", "ui/Icons/civ/btn/econ", "civ_econ Title", "civ_btn_econ_d" },
            new[] { "net", "ui/Icons/civ/btn/net", "civ_net Title", "civ_btn_net_d" },
            new[] { "control", "ui/Icons/civ/btn/control", "civ_control Title", "civ_btn_control_d" }
        };

        protected override float Wd
        {
            get { return 260f; }
        }

        protected override float Ht
        {
            get { return 260f; }
        }

        protected override string Tab
        {
            get { return "civ"; }
        }

        public static void Open(Kingdom k)
        {
            Show(Id, k, () => CreateAndInit(Id));
        }

        public static void Go(string id, Kingdom k)
        {
            switch (id)
            {
                case "tech": WinTech.Open(k); break;
                case "state": WinState.Open(k); break;
                case "econ": WinEcon.Open(); break;
                case "net": WinNet.Open(); break;
                case "control": WinControl.Open(k); break;
            }
        }

        protected override void Draw(Sheet s, Kingdom k)
        {
            Title(s, k, Txt.T("civ_gov_tab_civ"), null);
            float y = 30f, h = Math.Min(40f, (s.H - y - 4f) / Entries.Length - 4f);
            foreach (string[] e in Entries)
            {
                string id = e[0];
                s.Panel(4f, y, s.W - 8f, h, 0.8f);
                s.Hit(4f, y, s.W - 8f, h, () => Go(id, k), Txt.T(e[2]), Txt.T(e[3]));
                UiKit.Img(s.R, UiKit.Icon(e[1]), new Vector2(h - 10f, h - 10f), s.At(10f, y + 5f, h - 10f, h - 10f));
                s.Text(h + 8f, y + 2f, s.W - h - 16f, h * 0.5f, Txt.T(e[2]), 9, TextAnchor.MiddleLeft, UiKit.Gold);
                s.Text(h + 8f, y + h * 0.5f, s.W - h - 16f, h * 0.5f - 2f, Txt.T(e[3]), 6, TextAnchor.UpperLeft, UiKit.Soft);
                y += h + 4f;
            }
        }
    }
}
