using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal class WinSandbox : AbstractWindow<WinSandbox>
    {
        public const string Id = "civ_sandbox";
        private const int PerPage = 30;
        private const int Cols = 6;
        private static int _cat;
        private static int _page;
        private static int _era = -1;
        private static string _query = "";
        private static bool _favOnly;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open(int cat)
        {
            _cat = Math.Max(0, Math.Min(Sandbox.Cats.Length - 1, cat));
            _page = 0;
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

        private List<SbTool> List()
        {
            string cat = Sandbox.Cats[_cat];
            IEnumerable<SbTool> q = _favOnly ? Sandbox.Favs().Select(id => Sandbox.ById.ContainsKey(id) ? Sandbox.ById[id] : null).Where(t => t != null) : Sandbox.Tools.Where(t => t.Cat == cat);
            if (_era >= 0) q = q.Where(t => t.Era < 0 || t.Era == _era);
            if (!string.IsNullOrEmpty(_query))
            {
                string s = _query.ToLowerInvariant();
                q = q.Where(t => (t.Name ?? "").ToLowerInvariant().Contains(s) || t.Id.Contains(s));
            }
            return q.ToList();
        }

        private void Render()
        {
            Transform c = ContentTransform;
            UiKit.Clear(c);
            UiKit.Header(c, Txt.T("sb_title"), UiKit.Gold, "ui/Icons/civ/btn/sandbox");
            if (!Sandbox.On)
            {
                UiKit.Label(c, Txt.T("sb_off"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            if (Sandbox.Blocked())
            {
                UiKit.Label(c, Txt.T("sb_blocked"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Bad);
                return;
            }
            UiKit.Tabs(c, Sandbox.Cats.Select(x => "sb_cat_" + x).ToArray(), Sandbox.Cats.Select(x => "ui/Icons/civ/sb/" + x).ToArray(), _favOnly ? -1 : _cat, i =>
            {
                _cat = i;
                _favOnly = false;
                _page = 0;
                Render();
            });
            RectTransform bar = UiKit.Box(c, "Filters", 24f);
            UiKit.Input(bar, _query, new Vector2(84f, 20f), new Vector2(-UiKit.W / 2f + 46f, 0f), s =>
            {
                _query = (s ?? "").Trim();
                _page = 0;
                Render();
            });
            string era = _era < 0 ? Txt.T("sb_all_eras") : Db.EraName(_era);
            UiKit.Btn(bar, "<", null, () =>
            {
                _era = Math.Max(-1, _era - 1);
                _page = 0;
                Render();
            }, new Vector2(18f, 20f), new Vector2(-6f, 0f));
            UiKit.Text(bar, era, new Vector2(30f, 0f), new Vector2(54f, 20f), 7, TextAnchor.MiddleCenter, UiKit.Soft);
            UiKit.Btn(bar, ">", null, () =>
            {
                _era = Math.Min(Db.Eras.Count - 1, _era + 1);
                _page = 0;
                Render();
            }, new Vector2(18f, 20f), new Vector2(66f, 0f));
            UiKit.IconBtn(bar, UiKit.Icon("ui/Icons/civ/sb/star"), () =>
            {
                _favOnly = !_favOnly;
                _page = 0;
                Render();
            }, 20f, new Vector2(UiKit.W / 2f - 14f, 0f), Txt.T("sb_favs"), Txt.T("sb_favs_d"), _favOnly, true);
            List<SbTool> list = List();
            int pages = Math.Max(1, (list.Count + PerPage - 1) / PerPage);
            _page = Math.Min(_page, pages - 1);
            UiKit.Label(c, Txt.F("sb_count", list.Count), 12f, 7, TextAnchor.MiddleCenter, UiKit.Dim);
            var page = list.Skip(_page * PerPage).Take(PerPage).ToList();
            float cell = (UiKit.W - 8f) / Cols;
            for (int r = 0; r * Cols < page.Count; r++)
            {
                RectTransform row = UiKit.Box(c, "Row" + r, cell + 2f);
                for (int i = 0; i < Cols && r * Cols + i < page.Count; i++)
                {
                    SbTool t = page[r * Cols + i];
                    float x = -UiKit.W / 2f + 4f + cell * i + cell / 2f;
                    SimpleButton b = UiKit.IconBtn(row, UiKit.Icon(t.Icon), () => Sandbox.Pick(t), cell - 4f, new Vector2(x, 0f), t.Name, t.Desc + (t.Era >= 0 ? "\n" + Db.EraName(t.Era) : ""), Sandbox.Current == t, true);
                    if (Sandbox.IsFav(t)) UiKit.Img(b.transform, UiKit.Icon("ui/Icons/civ/sb/star"), new Vector2(8f, 8f), new Vector2(cell / 2f - 6f, cell / 2f - 6f));
                    SbTool fav = t;
                    UiKit.IconBtn(row, UiKit.Icon("ui/Icons/civ/sb/star"), () =>
                    {
                        Sandbox.ToggleFav(fav);
                        Render();
                    }, 9f, new Vector2(x - cell / 2f + 6f, cell / 2f - 6f), Txt.T("sb_fav"), null, Sandbox.IsFav(t), false);
                }
            }
            UiKit.Pager(c, _page, pages, p =>
            {
                _page = p;
                Render();
            });
            if (Sandbox.Current != null) UiKit.Label(c, Txt.F("sb_armed", Sandbox.Current.Name), 14f, 8, TextAnchor.MiddleCenter, UiKit.Good);
        }
    }
}
