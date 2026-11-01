using System.Collections.Generic;
using NeoModLoader.api;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;

namespace Civilizations
{
    internal class WinRoute : AbstractWindow<WinRoute>
    {
        public const string Id = "civ_routes";

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
            Kingdom lead = EraLock.Leader;
            RectTransform top = UiKit.Box(c, "Leader", 40f);
            UiKit.Panel(top);
            UiKit.Img(top, EraLock.EraSprite(EraLock.LeaderEra), new Vector2(28f, 28f), new Vector2(-UiKit.W / 2f + 20f, 0f));
            UiKit.Text(top, Db.EraName(EraLock.LeaderEra), new Vector2(14f, 8f), new Vector2(170f, 14f), 10, TextAnchor.MiddleLeft, G.Col(Db.Eras[EraLock.LeaderEra].Color));
            UiKit.Bar(top, EraLock.LeaderProgress, new Color(1f, 0.8f, 0.3f), new Vector2(170f, 10f), new Vector2(14f, -8f), (lead != null ? G.Name(lead) : Txt.T("civ_no_leader")) + " " + Mathf.RoundToInt(EraLock.LeaderProgress * 100f) + "%");
            UiKit.Header(c, Txt.T("civ_route_world"), UiKit.Gold);
            UiKit.Paragraph(c, Txt.T("civ_route_world_d"), 8, UiKit.Soft);
            Dictionary<string, int> fol = WorldPrefs.Followers();
            string pref = WorldPrefs.Route;
            foreach (string r in WorldPrefs.Routes)
            {
                string rr = r;
                RectTransform row = UiKit.Box(c, "Route", 46f);
                UiKit.Panel(row, r == pref ? 1f : 0.55f);
                SimpleButton b = UiKit.IconBtn(row, UiKit.Icon(WorldPrefs.Icon(r)), () =>
                {
                    WorldPrefs.Route = rr;
                    Notify.Show(Txt.F("civ_route_pref_set", Txt.T("civ_route_" + rr)));
                    Render();
                }, 36f, new Vector2(-UiKit.W / 2f + 24f, 0f), Txt.T("civ_route_" + r), Txt.T("civ_route_" + r + "_d"));
                b.Background.color = r == pref ? new Color(1f, 0.85f, 0.45f) : Color.white;
                UiKit.Text(row, Txt.T("civ_route_" + r) + (r == pref ? "  ★" : ""), new Vector2(24f, 13f), new Vector2(150f, 14f), 10, TextAnchor.MiddleLeft, r == pref ? UiKit.Gold : Color.white);
                UiKit.Text(row, Txt.T("civ_route_" + r + "_d"), new Vector2(24f, -6f), new Vector2(150f, 24f), 7, TextAnchor.UpperLeft, UiKit.Soft);
                int n;
                fol.TryGetValue(r, out n);
                UiKit.Text(row, n.ToString(), new Vector2(UiKit.W / 2f - 12f, 13f), new Vector2(20f, 12f), 8, TextAnchor.MiddleRight, UiKit.Cyan);
            }
        }
    }
}
