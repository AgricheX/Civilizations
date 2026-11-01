using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal class WinNet : AbstractWindow<WinNet>
    {
        public const string Id = "civ_net";
        private const int PageSize = 7;

        private static readonly string[] Apps = { "pixelgram", "squawk", "pigeon", "torch", "herald", "ballot", "exchange", "calendar", "memorial", "embassy", "army", "chest", "contacts", "settings" };
        private static readonly string[] Basic = { "squawk", "pigeon", "herald", "ballot", "calendar", "memorial", "army", "contacts", "settings" };
        private static readonly string[] Dock = { "pixelgram", "squawk", "pigeon", "herald" };

        private static readonly Color Screen = new Color(0.97f, 0.97f, 0.99f, 1f);
        private static readonly Color Ink = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color InkSoft = new Color(0.35f, 0.38f, 0.46f);
        private static readonly Color Accent = new Color(0.2f, 0.45f, 0.85f);

        private const float PW = 176f;
        private const float PH = 384f;
        private static string _app = "";
        private bool _sized;
        private bool _placed;
        private float _fit = 1f;
        private float _lastScale = -1f;
        private int _stable;
        private RectTransform _phone;
        private static int _page;
        private static Kingdom _view;
        private static bool _global;
        private static bool _babel = true;
        private static int _post = -1;
        private static string _profile = "";
        private int _seen = -1;
        private float _poll;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open()
        {
            if (!Internet.ExistsAnywhere())
            {
                Notify.Show(Txt.T("net_not_yet"));
                return;
            }
            UiKit.Open(Id);
        }

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
            Frameless();
            VerticalLayoutGroup v = ContentTransform.GetComponent<VerticalLayoutGroup>();
            if (v != null)
            {
                v.spacing = 0;
                v.padding = new RectOffset(0, 0, 2, 2);
            }
        }

        private void Grow(float extra)
        {
            RectTransform bg = BackgroundTransform as RectTransform;
            if (bg == null) return;
            bg.sizeDelta = new Vector2(bg.sizeDelta.x, bg.sizeDelta.y + extra);
            RectTransform sv = bg.Find("Scroll View") as RectTransform;
            if (sv != null && Mathf.Approximately(sv.anchorMin.y, sv.anchorMax.y)) sv.sizeDelta = new Vector2(sv.sizeDelta.x, sv.sizeDelta.y + extra);
        }

        private void Frameless()
        {
            Transform content = ContentTransform;
            foreach (Image img in GetComponentsInChildren<Image>(true))
            {
                if (img == null || img.transform.IsChildOf(content)) continue;
                Mask m = img.GetComponent<Mask>();
                if (m != null)
                {
                    m.showMaskGraphic = false;
                    continue;
                }
                img.enabled = false;
            }
            foreach (Text tx in GetComponentsInChildren<Text>(true))
            {
                if (tx != null && !tx.transform.IsChildOf(content)) tx.enabled = false;
            }
            foreach (Button bt in GetComponentsInChildren<Button>(true))
            {
                if (bt != null && !bt.transform.IsChildOf(content)) bt.interactable = false;
            }
        }

        private void Close()
        {
            ScrollWindow sw = GetComponent<ScrollWindow>();
            if (sw != null) sw.clickHide();
        }

        private void Fit()
        {
            if (_sized) return;
            RectTransform vp = ContentTransform.parent as RectTransform;
            if (vp == null) return;
            Canvas.ForceUpdateCanvases();
            float h = vp.rect.height;
            if (h < 10f) return;
            _sized = true;
            float need = PH + 6f - h;
            if (Mathf.Abs(need) > 2f) Grow(need);
        }

        private void Place()
        {
            if (_placed || _phone == null) return;
            RectTransform bg = BackgroundTransform as RectTransform;
            Canvas cv = GetComponentInParent<Canvas>();
            if (bg == null || cv == null) return;
            float sc = bg.lossyScale.y;
            if (sc <= 0.0001f || Mathf.Abs(sc - _lastScale) > 0.0001f)
            {
                _lastScale = sc;
                _stable = 0;
                return;
            }
            if (++_stable < 3) return;
            _placed = true;
            RectTransform root = cv.rootCanvas.transform as RectTransform;
            if (root == null) return;
            Canvas.ForceUpdateCanvases();
            Vector3[] c = new Vector3[4];
            _phone.GetWorldCorners(c);
            float bot = root.InverseTransformPoint(c[0]).y;
            float top = root.InverseTransformPoint(c[1]).y;
            float h = top - bot;
            float half = root.rect.height / 2f;
            float m = 6f;
            float avail = root.rect.height - m * 2f;
            if (h > avail && h > 1f)
            {
                float f = avail / h;
                _fit *= f;
                _phone.localScale = new Vector3(_fit, _fit, 1f);
                float mid = (top + bot) / 2f;
                h *= f;
                top = mid + h / 2f;
                bot = mid - h / 2f;
            }
            float dy = 0f;
            if (top > half - m) dy = half - m - top;
            else if (bot < -half + m) dy = -half + m - bot;
            if (Mathf.Abs(dy) > 0.5f) bg.position += root.TransformVector(new Vector3(0f, dy, 0f));
        }

        public override void OnNormalEnable()
        {
            Frameless();
            Fit();
            _placed = false;
            _stable = 0;
            _lastScale = -1f;
            if (!_global && (!G.Ok(_view) || !Internet.Exists(_view)))
            {
                Kingdom sel = SelectedMetas.selected_kingdom;
                _view = Internet.Exists(sel) ? sel : G.Kingdoms().FirstOrDefault(Internet.Exists);
            }
            Render();
        }

        private void Update()
        {
            if (!IsOpened) return;
            Place();
            _poll += Time.unscaledDeltaTime;
            if (_poll < 1f) return;
            _poll = 0f;
            if (_seen != Internet.Version && (_app == "torch" || _app == "pigeon" || _post >= 0)) Render();
        }

        private void Go(string app)
        {
            _app = app;
            _page = 0;
            _post = -1;
            _profile = "";
            Render();
        }

        private static bool Smart()
        {
            if (_global) return G.Kingdoms().Any(k => Internet.Exists(k) && Research.Has(k, "smartphones"));
            return _view != null && Research.Has(_view, "smartphones");
        }

        private static IEnumerable<string> Available()
        {
            return Smart() ? Apps : Basic;
        }

        private static string Icon(string app)
        {
            return "ui/Icons/civ/app/" + app;
        }

        private void Render()
        {
            _seen = Internet.Version;
            Theme();
            Transform root = ContentTransform;
            UiKit.Clear(root);
            RectTransform phone = UiKit.Box(root, "Phone", PH, PW + 16f);
            _phone = phone;
            phone.localScale = new Vector3(_fit, _fit, 1f);
            PhoneKit.Rect(phone, PhoneKit.Bezel, new Vector2(PW + 16f, PH), Vector2.zero);
            PhoneKit.Rect(phone, new Color(0.2f, 0.2f, 0.24f), new Vector2(44f, 5f), new Vector2(0f, PH / 2f - 7f));
            PhoneKit.Btn(phone, "✖", null, Close, new Vector2(16f, 11f), new Vector2(PW / 2f - 6f, PH / 2f - 7f), new Color(0.75f, 0.22f, 0.22f), Color.white);
            bool home = _app.Length == 0;
            var sgo = new GameObject("Stack", typeof(RectTransform), typeof(Image));
            sgo.transform.SetParent(phone, false);
            RectTransform srt = sgo.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(PW, PH - 28f);
            srt.localPosition = Vector3.zero;
            Image sbg = sgo.GetComponent<Image>();
            sbg.sprite = PhoneKit.Round;
            sbg.type = Image.Type.Sliced;
            sbg.color = home ? Wall : new Color(0.9f, 0.91f, 0.95f);
            sbg.raycastTarget = false;
            VerticalLayoutGroup vl = sgo.AddComponent<VerticalLayoutGroup>();
            vl.childControlHeight = true;
            vl.childControlWidth = false;
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth = false;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.spacing = 0;
            Transform st = sgo.transform;
            if (!G.Loaded || !Internet.ExistsAnywhere())
            {
                UiKit.Label(st, Txt.T("net_not_yet"), 40f, 10, TextAnchor.MiddleCenter, Color.white);
                return;
            }
            Status(st);
            if (home)
            {
                HomeTop(st);
                Transform body = Body(st);
                Home(body);
                DockRow(st);
                HomeBar(st, Wall, new Color(1f, 1f, 1f, 0.75f));
                return;
            }
            AppBar(st);
            Transform c = Body(st);
            if (_post >= 0) Single(c);
            else if (_profile.Length > 0) Profile(c);
            else Content(c);
            HomeBar(st, Screen, new Color(0.2f, 0.22f, 0.28f));
        }

        private Transform Body(Transform st)
        {
            var bgo = new GameObject("Body", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            bgo.transform.SetParent(st, false);
            RectTransform brt = bgo.GetComponent<RectTransform>();
            brt.sizeDelta = new Vector2(PW, 100f);
            Image hit = bgo.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            LayoutElement le = bgo.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.minHeight = 40f;
            var cgo = new GameObject("Feed", typeof(RectTransform));
            cgo.transform.SetParent(bgo.transform, false);
            RectTransform crt = cgo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(PW, 10f);
            UiKit.Setup(crt);
            VerticalLayoutGroup v = crt.GetComponent<VerticalLayoutGroup>();
            v.spacing = 0;
            v.padding = new RectOffset(0, 0, 0, 4);
            ScrollRect sr = bgo.AddComponent<ScrollRect>();
            sr.content = crt;
            sr.viewport = brt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 20f;
            return crt;
        }

        private void HomeBar(Transform st, Color bg, Color pill)
        {
            RectTransform bar = UiKit.Box(st, "HomeBar", 12f, PW);
            PhoneKit.Screen(bar, bg, 0f, 0f);
            PhoneKit.Btn(bar, null, null, () => Go(""), new Vector2(56f, 4f), Vector2.zero, pill);
        }

        private void Content(Transform c)
        {
            switch (_app)
            {
                case "pixelgram": Pixelgram(c); break;
                case "squawk": Squawk(c); break;
                case "pigeon": List(c, Internet.Feed.Where(p => p.Dm && InView(p)).Reverse().ToList()); break;
                case "torch": List(c, Visible().Where(p => p.Live).Reverse().ToList()); break;
                case "herald": List(c, Visible().Where(p => p.Who == "kingdom" || p.Who == "system" || p.Who == "religion").Reverse().ToList()); break;
                case "ballot": List(c, Visible().Where(p => p.Poll != null).Reverse().ToList()); break;
                case "exchange": Exchange(c); break;
                case "calendar": List(c, Visible().Where(p => p.Kind == "birthday" || p.Kind == "city_anniv" || p.Kind == "throwback").Reverse().ToList()); break;
                case "memorial": List(c, Visible().Where(p => p.Kind == "obituary" || p.Kind == "rip").Reverse().ToList()); break;
                case "embassy": Embassy(c); break;
                case "army": ArmyApp(c); break;
                case "chest": List(c, Internet.Feed.Where(p => p.Saved).Reverse().ToList()); break;
                case "contacts": People(c); break;
                case "settings": Settings(c); break;
            }
        }

        private static bool InView(NetPost p)
        {
            return _global || _view == null || p.Kingdom == G.Id(_view);
        }

        private IEnumerable<NetPost> Visible()
        {
            foreach (NetPost p in Internet.Feed)
            {
                if (p.Dm || !InView(p)) continue;
                yield return p;
            }
        }

        private static Text Ink9(Transform box, string s, Vector2 pos, Vector2 size, int font = 9, TextAnchor anchor = TextAnchor.MiddleLeft, Color? col = null)
        {
            Text t = UiKit.Text(box, s, pos, size, font, anchor, col ?? Ink);
            t.color = col ?? Ink;
            return t;
        }

        private static Text InkRange(Transform box, string s, float left, float right, float y, float height, int font = 9, TextAnchor anchor = TextAnchor.MiddleLeft, Color? col = null)
        {
            return Ink9(box, s, new Vector2((left + right - PW) / 2f, y), new Vector2(right - left, height), font, anchor, col);
        }

        private static Text InkParagraph(Transform box, string s, float left, float right, int font = 8, Color? col = null)
        {
            Text t = InkRange(box, s, left, right, 0f, 1f, font, TextAnchor.UpperLeft, col);
            t.resizeTextForBestFit = false;
            t.rectTransform.sizeDelta = new Vector2(right - left, Mathf.Max(font + 2f, Mathf.Ceil(t.preferredHeight) + 2f));
            return t;
        }

        private static void SetHeight(RectTransform box, float height)
        {
            box.sizeDelta = new Vector2(PW, height);
            box.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static Image Sheet(RectTransform box, Color col)
        {
            bool card = box.name == "Post" || box.name == "Comment" || box.name == "Person" || box.name == "Rep" || box.name == "Gen" || box.name == "K" || box.name == "Profile";
            return PhoneKit.Screen(box, col, 0f, card ? 2f : 0f);
        }

        private static Color Wall = new Color(0.16f, 0.33f, 0.62f);
        private static Color WallDeep = new Color(0.1f, 0.2f, 0.42f);

        private static void Theme()
        {
            Wall = new Color(0.16f, 0.33f, 0.62f);
            WallDeep = new Color(0.1f, 0.2f, 0.42f);
            if (_global || !G.Ok(_view) || _view.getColor() == null) return;
            Color32 k = _view.getColor().getColorMain32();
            float h, s, v;
            Color.RGBToHSV(new Color(k.r / 255f, k.g / 255f, k.b / 255f), out h, out s, out v);
            if (s < 0.15f)
            {
                Wall = Color.HSVToRGB(h, 0.08f, 0.38f);
                WallDeep = Color.HSVToRGB(h, 0.08f, 0.24f);
                return;
            }
            s = Mathf.Clamp(s, 0.4f, 0.75f);
            Wall = Color.HSVToRGB(h, s, Mathf.Clamp(v * 0.6f, 0.36f, 0.52f));
            WallDeep = Color.HSVToRGB(h, Mathf.Min(1f, s + 0.1f), Mathf.Clamp(v * 0.4f, 0.22f, 0.32f));
        }

        private static Color Tile(string app)
        {
            switch (app)
            {
                case "pixelgram": return new Color(0.88f, 0.3f, 0.5f);
                case "squawk": return new Color(0.2f, 0.22f, 0.28f);
                case "pigeon": return new Color(0.2f, 0.7f, 0.4f);
                case "torch": return new Color(0.95f, 0.5f, 0.15f);
                case "herald": return new Color(0.82f, 0.22f, 0.22f);
                case "ballot": return new Color(0.25f, 0.5f, 0.9f);
                case "exchange": return new Color(0.1f, 0.6f, 0.55f);
                case "calendar": return new Color(0.96f, 0.96f, 0.98f);
                case "memorial": return new Color(0.45f, 0.47f, 0.52f);
                case "embassy": return new Color(0.35f, 0.3f, 0.75f);
                case "army": return new Color(0.4f, 0.45f, 0.25f);
                case "chest": return new Color(0.6f, 0.42f, 0.22f);
                case "contacts": return new Color(0.15f, 0.65f, 0.8f);
                default: return new Color(0.55f, 0.58f, 0.64f);
            }
        }

        private void Status(Transform c)
        {
            RectTransform box = UiKit.Box(c, "Status", 18f, PW);
            PhoneKit.Screen(box, _app.Length == 0 ? WallDeep : new Color(0.97f, 0.97f, 0.99f), 0f, 0f);
            Color fg = _app.Length == 0 ? Color.white : Ink;
            Ink9(box, Txt.F("net_year", G.Year), new Vector2(-PW / 2f + 28f, 0f), new Vector2(46f, 12f), 7, TextAnchor.MiddleLeft, fg);
            List<Kingdom> online = G.Kingdoms().Where(Internet.Exists).ToList();
            PhoneKit.Btn(box, "‹", null, () => Cycle(online, -1), new Vector2(13f, 12f), new Vector2(-30f, 0f), new Color(1f, 1f, 1f, 0.15f), fg);
            string net = _global || _view == null ? Txt.T("net_global") : G.Name(_view);
            Ink9(box, net, new Vector2(0f, 0f), new Vector2(46f, 12f), 7, TextAnchor.MiddleCenter, fg);
            PhoneKit.Btn(box, "›", null, () => Cycle(online, 1), new Vector2(13f, 12f), new Vector2(30f, 0f), new Color(1f, 1f, 1f, 0.15f), fg);
            bool down = !_global && _view != null && G.Cities(_view).Count > 0 && G.Cities(_view).All(ct => Energy.Blackout(ct));
            Ink9(box, down ? "✖" : (Smart() ? "5G" : "2G"), new Vector2(PW / 2f - 34f, 0f), new Vector2(24f, 12f), 7, TextAnchor.MiddleRight, down ? new Color(1f, 0.4f, 0.35f) : fg);
            PhoneKit.Rect(box, fg, new Vector2(12f, 6f), new Vector2(PW / 2f - 13f, 0f));
            PhoneKit.Rect(box, fg, new Vector2(2f, 3f), new Vector2(PW / 2f - 6f, 0f), false);
        }

        private void Cycle(List<Kingdom> online, int d)
        {
            var opts = new List<Kingdom> { null };
            opts.AddRange(online);
            int i = _global ? 0 : Math.Max(0, opts.IndexOf(_view));
            int n = ((i + d) % opts.Count + opts.Count) % opts.Count;
            _global = opts[n] == null;
            if (!_global) _view = opts[n];
            _page = 0;
            Render();
        }

        private void HomeTop(Transform c)
        {
            RectTransform wall = UiKit.Box(c, "Wall", 30f, PW);
            PhoneKit.Screen(wall, WallDeep, 0f, 0f);
            if (!_global && _view != null) UiKit.Banner(wall, _view, new Vector2(-PW / 2f + 18f, 0f), 18f);
            else UiKit.Img(wall, UiKit.Icon("ui/Icons/civ/btn/net"), new Vector2(18f, 18f), new Vector2(-PW / 2f + 18f, 0f));
            Ink9(wall, _global || _view == null ? Txt.T("net_global") : G.Name(_view), new Vector2(18f, 5f), new Vector2(130f, 12f), 9, TextAnchor.MiddleLeft, Color.white);
            Ink9(wall, Smart() ? Txt.T("net_phone_smart") : Txt.T("net_phone_brick"), new Vector2(18f, -7f), new Vector2(130f, 10f), 7, TextAnchor.MiddleLeft, new Color(0.8f, 0.86f, 1f));
        }

        private const float Step = 40f;

        private void Home(Transform c)
        {
            RectTransform pad = UiKit.Box(c, "Pad", 6f, PW);
            var apps = Available().Where(a => !Dock.Contains(a)).ToList();
            for (int r = 0; r * 4 < apps.Count; r++)
            {
                RectTransform row = UiKit.Box(c, "Apps", 46f, PW);
                int n = Math.Min(4, apps.Count - r * 4);
                for (int i = 0; i < n; i++) AppIcon(row, apps[r * 4 + i], new Vector2(-PW / 2f + 8f + Step / 2f + i * Step, 6f), Color.white);
            }
        }

        private void DockRow(Transform st)
        {
            RectTransform dock = UiKit.Box(st, "Dock", 48f, PW);
            PhoneKit.Screen(dock, Wall, 0f, 0f);
            PhoneKit.Rect(dock, new Color(1f, 1f, 1f, 0.22f), new Vector2(PW - 8f, 44f), Vector2.zero);
            var dk = Dock.Where(a => Available().Contains(a)).ToList();
            for (int i = 0; i < dk.Count; i++) AppIcon(dock, dk[i], new Vector2(-PW / 2f + 8f + Step / 2f + i * Step, 5f), Color.white);
        }

        private void AppIcon(RectTransform row, string app, Vector2 pos, Color label)
        {
            string a = app;
            PillBtn tile = PhoneKit.Btn(row, null, null, () => Go(a), new Vector2(28f, 28f), pos, Tile(app));
            UiKit.Img(tile.Background.transform, UiKit.Icon(Icon(app)), new Vector2(21f, 21f), Vector2.zero);
            Ink9(row, Txt.T("net_app_" + app), new Vector2(pos.x, pos.y - 20f), new Vector2(Step, 9f), 6, TextAnchor.MiddleCenter, label);
        }

        private void AppBar(Transform c)
        {
            RectTransform box = UiKit.Box(c, "AppBar", 26f, PW);
            PhoneKit.Screen(box, new Color(0.97f, 0.97f, 0.99f), 0f, 0f);
            PhoneKit.Btn(box, "‹", null, () =>
            {
                if (_post >= 0) _post = -1;
                else if (_profile.Length > 0) _profile = "";
                else _app = "";
                _page = 0;
                Render();
            }, new Vector2(22f, 18f), new Vector2(-PW / 2f + 18f, 0f), new Color(1f, 1f, 1f, 0f), Accent);
            PhoneKit.Rect(box, Tile(_app), new Vector2(18f, 18f), new Vector2(-PW / 2f + 40f, 0f));
            UiKit.Img(box, UiKit.Icon(Icon(_app)), new Vector2(14f, 14f), new Vector2(-PW / 2f + 40f, 0f));
            InkRange(box, Txt.T("net_app_" + _app), 54f, 136f, 0f, 16f, 11, TextAnchor.MiddleLeft, Ink);
            PhoneKit.Btn(box, null, null, () => Go(""), new Vector2(26f, 6f), new Vector2(PW / 2f - 22f, 0f), new Color(0.75f, 0.78f, 0.85f));
            RectTransform line = UiKit.Box(c, "Line", 1f, PW);
            PhoneKit.Screen(line, new Color(0.85f, 0.87f, 0.92f), 0f, 0f);
        }

        private string Shown(NetPost p)
        {
            if (_babel && !_global && Internet.Foreign(p, _view)) return Internet.Babel(p);
            return p.Text;
        }

        private void List(Transform c, List<NetPost> posts)
        {
            if (posts.Count == 0)
            {
                RectTransform e = UiKit.Box(c, "Empty", 30f, PW);
                Sheet(e, Screen);
                Ink9(e, Txt.T("net_empty"), Vector2.zero, new Vector2(PW - 10f, 20f), 9, TextAnchor.MiddleCenter, InkSoft);
                return;
            }
            int pages = (posts.Count + PageSize - 1) / PageSize;
            _page = Math.Max(0, Math.Min(_page, pages - 1));
            foreach (NetPost p in posts.Skip(_page * PageSize).Take(PageSize)) Card(c, p);
            PhoneKit.Pager(c, _page, pages, n =>
            {
                _page = n;
                Render();
            }, Screen);
        }

        private void Card(Transform c, NetPost p)
        {
            string text = Shown(p);
            float pollH = p.Poll != null ? p.Poll.Count * 14f + 4f : 0f;
            bool photo = NetPhoto.Has(p) && (_app == "pixelgram" || _app == "herald" || _app == "torch" || _app == "calendar" || _app == "memorial" || p.Important);
            float picH = photo ? 76f : p.Icon.Length > 0 ? 40f : 0f;
            bool badge = p.Live && !p.LiveEnded || p.Viral || p.Fake && p.Checked;
            bool extraBadge = p.Fake && p.Checked && (p.Live && !p.LiveEnded || p.Viral);
            float headerH = (p.Geo.Length > 0 ? 38f : 28f) + (extraBadge ? 10f : 0f);
            RectTransform box = UiKit.Box(c, "Post", 1f, PW);
            Color bgc = Screen;
            if (p.Viral) bgc = new Color(1f, 0.95f, 0.82f);
            if (p.Dm) bgc = new Color(0.86f, 0.9f, 1f);
            if (p.Fake && p.Checked) bgc = new Color(1f, 0.84f, 0.84f);
            if (p.Kind == "obituary" || p.Kind == "rip") bgc = new Color(0.86f, 0.86f, 0.88f);
            Sheet(box, bgc);
            Text body = InkParagraph(box, text, 6f, PW - 6f);
            float textH = body.rectTransform.sizeDelta.y;
            float h = headerH + 2f + textH + pollH + picH + 22f;
            SetHeight(box, h);
            float top = h / 2f;
            Avatar(box, p, new Vector2(-PW / 2f + 13f, top - 13f), 0.45f);
            string who = p.Author + (p.Who == "kingdom" ? " ✔" : p.Who == "company" ? " ◆" : p.Who == "religion" ? " ✝" : "");
            InkRange(box, who, 30f, badge ? 112f : PW - 6f, top - 8f, 11f, 9, TextAnchor.MiddleLeft, p.Who == "person" ? Ink : new Color(0.65f, 0.45f, 0.05f));
            string meta = p.Handle + " · " + Txt.F("net_year", p.Year) + (p.Place.Length > 0 ? " · " + p.Place : "");
            if (p.Dm) meta = p.Handle + " → " + p.ToName + " · " + Txt.F("net_year", p.Year);
            else if (p.Community.Length > 0) meta = p.Community + " · " + p.Handle + " · " + Txt.F("net_year", p.Year);
            InkRange(box, meta, 30f, p.Tag.Length > 0 ? 112f : PW - 6f, top - 18f, 9f, 7, TextAnchor.MiddleLeft, InkSoft);
            if (p.Geo.Length > 0) InkRange(box, p.Geo, 6f, PW - 6f, top - 29f - (extraBadge ? 10f : 0f), 8f, 6, TextAnchor.MiddleLeft, InkSoft);
            if (p.Live && !p.LiveEnded) Ink9(box, "<color=#E0302A>● LIVE</color> " + p.Viewers, new Vector2(55f, top - 8f), new Vector2(54f, 10f), 8, TextAnchor.MiddleRight, Ink);
            else if (p.Viral) Ink9(box, Txt.T("net_viral"), new Vector2(55f, top - 8f), new Vector2(54f, 10f), 8, TextAnchor.MiddleRight, new Color(0.9f, 0.45f, 0.1f));
            if (p.Fake && p.Checked) Ink9(box, Txt.T("net_fake_label"), new Vector2(55f, top - (extraBadge ? 28f : 8f)), new Vector2(54f, 10f), 8, TextAnchor.MiddleRight, new Color(0.85f, 0.2f, 0.2f));
            if (p.Tag.Length > 0) Ink9(box, p.Tag, new Vector2(55f, top - 18f), new Vector2(54f, 9f), 7, TextAnchor.MiddleRight, Accent);
            float y = top - headerH - 2f;
            if (photo)
            {
                Sprite snap = NetPhoto.Get(p);
                if (snap != null)
                {
                    Image ph = UiKit.Img(box, snap, new Vector2(PW - 14f, 72f), new Vector2(0f, y - 37f));
                    ph.preserveAspect = false;
                    Actor a = p.Who == "person" ? G.ActorById(p.AuthorId) : null;
                    if (a != null) UiKit.Avatar(box, a, new Vector2(-PW / 2f + 22f, y - 60f), 0.5f);
                    if (p.Icon.Length > 0) UiKit.Img(box, UiKit.Icon(p.Icon), new Vector2(18f, 18f), new Vector2(PW / 2f - 18f, y - 13f));
                }
                y -= picH;
            }
            else if (picH > 0f)
            {
                Sprite pic = UiKit.Icon(p.Icon);
                if (pic != null) UiKit.Img(box, pic, new Vector2(30f, 30f), new Vector2(-PW / 2f + 40f, y - 19f));
                y -= picH;
            }
            body.rectTransform.localPosition = new Vector3(0f, y - textH / 2f, 0f);
            y -= textH;
            if (p.Poll != null) y = Poll(box, p, y);
            float fy = -h / 2f + 11f;
            NetPost pp = p;
            if (p.Kind == "obituary" || p.Kind == "rip")
            {
                PillBtn candle = PhoneKit.Btn(box, p.Candles.ToString(), UiKit.Icon(Icon("memorial")), () =>
                {
                    Internet.Candle(pp);
                    Render();
                }, new Vector2(34f, 15f), new Vector2(-PW / 2f + 23f, fy));
                candle.Background.color = new Color(1f, 0.88f, 0.55f);
            }
            else
            {
                PillBtn like = PhoneKit.Btn(box, (p.Liked ? "♥ " : "♡ ") + G.Short(p.Likes), null, () =>
                {
                    Internet.Like(pp);
                    Render();
                }, new Vector2(34f, 15f), new Vector2(-PW / 2f + 23f, fy));
                like.Background.color = p.Liked ? new Color(1f, 0.6f, 0.65f) : new Color(0.8f, 0.82f, 0.88f);
            }
            PillBtn com = PhoneKit.Btn(box, "✉ " + p.Comments.Count, null, () =>
            {
                _post = pp.Id;
                Render();
            }, new Vector2(28f, 15f), new Vector2(-PW / 2f + 57f, fy));
            com.Background.color = new Color(0.8f, 0.82f, 0.88f);
            Ink9(box, "↻ " + G.Short(p.Shares) + "   ✖ " + G.Short(p.Dislikes), new Vector2(15f, fy), new Vector2(58f, 12f), 7, TextAnchor.MiddleLeft, InkSoft);
            PillBtn save = PhoneKit.Btn(box, p.Saved ? "★" : "☆", null, () =>
            {
                Internet.SaveToggle(pp);
                Render();
            }, new Vector2(16f, 15f), new Vector2(PW / 2f - 33f, fy));
            save.Background.color = p.Saved ? new Color(1f, 0.85f, 0.45f) : new Color(0.8f, 0.82f, 0.88f);
            if (p.Who == "person")
            {
                PhoneKit.Btn(box, "@", null, () =>
                {
                    _profile = pp.AuthorId;
                    _post = -1;
                    Render();
                }, new Vector2(16f, 15f), new Vector2(PW / 2f - 14f, fy));
            }
        }

        private float Poll(RectTransform box, NetPost p, float y)
        {
            int total = Math.Max(1, p.Votes.Sum());
            for (int i = 0; i < p.Poll.Count; i++)
            {
                int idx = i;
                float v = p.Votes[i] / (float)total;
                y -= 14f;
                string label = Txt.T("civ_p_" + p.Poll[i]) + "  " + Mathf.RoundToInt(v * 100f) + "%";
                bool win = p.PollWinner == i;
                float width = p.PollWinner < 0 ? PW - 52f : PW - 12f;
                float x = -PW / 2f + 6f + width / 2f;
                UiKit.Bar(box, v, win ? new Color(1f, 0.8f, 0.3f) : new Color(0.4f, 0.7f, 1f), new Vector2(width, 12f), new Vector2(x, y + 7f), label);
                if (p.PollWinner < 0)
                {
                    NetPost pp = p;
                    PhoneKit.Btn(box, Txt.T("net_vote"), null, () =>
                    {
                        Internet.Vote(pp, idx);
                        Render();
                    }, new Vector2(36f, 12f), new Vector2(PW / 2f - 24f, y + 7f));
                }
            }
            return y - 4f;
        }

        private void Avatar(RectTransform box, NetPost p, Vector2 pos, float scale)
        {
            if (p.Who == "person")
            {
                Actor a = G.ActorById(p.AuthorId);
                if (a != null && UiKit.Avatar(box, a, pos, scale) != null) return;
                UiKit.Img(box, UiKit.Icon("ui/Icons/civ/net/ghost"), new Vector2(20f, 20f), pos);
                return;
            }
            if (p.Who == "kingdom" || p.Who == "religion")
            {
                Kingdom k = G.KingdomById(p.Kingdom);
                if (k != null && p.Who == "kingdom")
                {
                    UiKit.Banner(box, k, pos, 22f);
                    return;
                }
            }
            if (p.Icon.Length > 0 && p.Who != "person")
            {
                UiKit.Img(box, UiKit.Icon(p.Icon), new Vector2(20f, 20f), pos);
                return;
            }
            UiKit.Img(box, UiKit.Icon(p.Who == "company" ? "ui/Icons/civ/net/company" : "ui/Icons/civ/net/ghost"), new Vector2(20f, 20f), pos);
        }

        private void Single(Transform c)
        {
            NetPost p = Internet.Feed.FirstOrDefault(x => x.Id == _post);
            if (p == null)
            {
                RectTransform e = UiKit.Box(c, "Gone", 30f, PW);
                Sheet(e, Screen);
                Ink9(e, Txt.T("net_gone"), Vector2.zero, new Vector2(PW - 10f, 20f), 9, TextAnchor.MiddleCenter, InkSoft);
                return;
            }
            Card(c, p);
            RectTransform head = UiKit.Box(c, "Comments", 18f, PW);
            Sheet(head, new Color(0.82f, 0.87f, 0.95f));
            Ink9(head, Txt.T("net_comments") + " (" + p.Comments.Count + ")", Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
            foreach (NetComment cm in p.Comments.AsEnumerable().Reverse())
            {
                RectTransform box = UiKit.Box(c, "Comment", 1f, PW);
                Sheet(box, new Color(0.95f, 0.96f, 0.99f));
                Text t = InkParagraph(box, cm.Text, 30f, PW - 6f);
                float textH = t.rectTransform.sizeDelta.y;
                float h = 22f + textH;
                SetHeight(box, h);
                Actor a = G.ActorById(cm.AuthorId);
                Vector2 ap = new Vector2(-PW / 2f + 12f, h / 2f - 11f);
                if (a == null || UiKit.Avatar(box, a, ap, 0.38f) == null) UiKit.Img(box, UiKit.Icon("ui/Icons/civ/net/ghost"), new Vector2(16f, 16f), ap);
                Ink9(box, cm.Author + "  <color=#6A7080>" + cm.Handle + "</color>", new Vector2(-8f, h / 2f - 7f), new Vector2(100f, 10f), 8);
                t.rectTransform.localPosition = new Vector3(12f, h / 2f - 16f - textH / 2f, 0f);
                Ink9(box, "♥ " + cm.Likes, new Vector2(PW / 2f - 23f, h / 2f - 7f), new Vector2(34f, 10f), 7, TextAnchor.MiddleRight, InkSoft);
            }
        }

        private void Pixelgram(Transform c)
        {
            var stories = Visible().Where(p => p.Story && p.Who == "person" && p.Year >= G.Year - 2).GroupBy(p => p.AuthorId).Select(g => g.Last()).Reverse().Take(6).ToList();
            if (stories.Count > 0)
            {
                RectTransform row = UiKit.Box(c, "Stories", 38f, PW);
                Sheet(row, Screen);
                for (int i = 0; i < stories.Count; i++)
                {
                    NetPost sp = stories[i];
                    Vector2 pos = new Vector2(-PW / 2f + 18f + i * 34f, 4f);
                    UiKit.Img(row, UiKit.S("ui/special/windowInnerSliced"), new Vector2(28f, 28f), pos, new Color(0.95f, 0.45f, 0.6f));
                    Actor a = G.ActorById(sp.AuthorId);
                    if (a != null) UiKit.Avatar(row, a, pos, 0.42f);
                    PhoneKit.Btn(row, "", null, () =>
                    {
                        _post = sp.Id;
                        Render();
                    }, new Vector2(28f, 28f), pos).Background.color = new Color(1f, 1f, 1f, 0f);
                }
            }
            List(c, Visible().Where(p => p.Who == "person" && (NetPhoto.Has(p) || p.Story)).Reverse().ToList());
        }

        private void Squawk(Transform c)
        {
            if (_page == 0)
            {
                RectTransform box = UiKit.Box(c, "Trend", 40f, PW);
                Sheet(box, new Color(0.84f, 0.9f, 1f));
                Ink9(box, Txt.T("net_trending"), new Vector2(0f, 10f), new Vector2(PW - 10f, 12f), 8, TextAnchor.MiddleCenter, InkSoft);
                string trend = string.IsNullOrEmpty(Internet.Trend) ? "-" : Internet.Trend;
                Ink9(box, trend, new Vector2(0f, -6f), new Vector2(PW - 10f, 16f), 13, TextAnchor.MiddleCenter, Accent);
                var tags = Visible().Where(p => p.Tag.Length > 0).GroupBy(p => p.Tag).Select(g => new { Tag = g.Key, L = g.Sum(x => x.Likes) }).OrderByDescending(x => x.L).Take(4).ToList();
                if (tags.Count > 0)
                {
                    RectTransform tb = UiKit.Box(c, "Tags", 16f, PW);
                    Sheet(tb, Screen);
                    Ink9(tb, string.Join("  ", tags.Select(t => t.Tag).ToArray()), Vector2.zero, new Vector2(PW - 10f, 12f), 8, TextAnchor.MiddleCenter, Accent);
                }
            }
            List(c, Visible().Where(p => !p.Live && p.Who != "religion").Reverse().ToList());
        }

        private void Exchange(Transform c)
        {
            if (Internet.CoinName.Length > 0)
            {
                RectTransform box = UiKit.Box(c, "Coin", 22f, PW);
                Sheet(box, new Color(0.86f, 0.96f, 0.86f));
                InkRange(box, Internet.CoinName, 6f, 106f, 0f, 14f, 10, TextAnchor.MiddleLeft, new Color(0.1f, 0.5f, 0.2f));
                Ink9(box, Internet.CoinPrice.ToString("0.##"), new Vector2(52f, 0f), new Vector2(60f, 14f), 10, TextAnchor.MiddleRight, new Color(0.1f, 0.5f, 0.2f));
            }
            List(c, Visible().Where(p => p.Who == "company").Reverse().ToList());
        }

        private void Embassy(Transform c)
        {
            var list = (_global || _view == null ? G.Kingdoms().Where(Internet.Exists) : new[] { _view }.AsEnumerable()).OrderByDescending(Internet.Reputation).Take(10).ToList();
            foreach (Kingdom k in list)
            {
                float r = Internet.Reputation(k);
                float brag = Store.Flt(k.data, Internet.KBrag, 0f);
                RectTransform box = UiKit.Box(c, "Rep", 58f, PW);
                Sheet(box, Screen);
                string trade = r > 0.3f ? Txt.F("net_rep_trade_up", (int)(r * 20f)) : r < -0.3f ? Txt.F("net_rep_trade_down", (int)(-r * 15f)) : Txt.T("net_rep_trade_none");
                string mig = r > 0.35f ? Txt.T("net_rep_migrants_in") : r < 0f ? Txt.T("net_rep_migrants_out") : Txt.T("net_rep_migrants_none");
                string dip = Txt.F("net_rep_diplomacy", G.Signed(r * 20f));
                string bragT = brag > 1f ? Txt.F("net_rep_brag", (int)Math.Min(30f, brag * 3f)) : "";
                Text t = InkParagraph(box, trade + "\n" + mig + "\n" + dip + (bragT.Length > 0 ? "\n" + bragT : ""), 30f, PW - 6f, 7, InkSoft);
                float textH = t.rectTransform.sizeDelta.y;
                float h = Mathf.Max(58f, 28f + textH);
                SetHeight(box, h);
                UiKit.Banner(box, k, new Vector2(-PW / 2f + 16f, h / 2f - 15f), 22f);
                InkRange(box, G.Name(k), 30f, 118f, h / 2f - 10f, 12f, 9);
                InkRange(box, G.Signed(r * 100f) + "%", 122f, PW - 6f, h / 2f - 10f, 12f, 10, TextAnchor.MiddleRight, r >= 0 ? new Color(0.1f, 0.55f, 0.2f) : new Color(0.75f, 0.15f, 0.15f));
                t.rectTransform.localPosition = new Vector3(12f, h / 2f - 20f - textH / 2f, 0f);
            }
        }

        private sealed class Force
        {
            public int Warriors, Soldiers, Land, Air, Ships, Missiles, Hangar, Empty;
            public int Power { get { return Warriors + Soldiers * 2 + Land * 4 + Air * 5 + Ships * 6 + Missiles; } }
        }

        private static Force Count(Kingdom k)
        {
            var f = new Force();
            foreach (City ct in G.Cities(k)) f.Warriors += ct.countWarriors();
            foreach (UnitDef d in Db.Units)
            {
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor a in asset.units)
                {
                    if (!G.Ok(a) || a.kingdom != k) continue;
                    if (d.Cls == "soldier") f.Soldiers++;
                    else if (d.Cls == "air") f.Air++;
                    else if (d.Cls == "ship")
                    {
                        if (d.Role == "trade") continue;
                        f.Ships++;
                        f.Missiles += Navy.Missiles(a);
                        if (Carriers.IsCarrier(a)) f.Hangar += Store.Str(a.data, "civ_hangar").Split(';').Count(x => x.Length > 0);
                    }
                    else if (Vehicles.Military(d)) f.Land++;
                    if (Ammo.Empty(a)) f.Empty++;
                }
            }
            f.Warriors = Math.Max(0, f.Warriors - f.Soldiers - f.Land - f.Air);
            return f;
        }

        private void Line(Transform c, string icon, string label, string value, Color col)
        {
            RectTransform row = UiKit.Box(c, "Row", 18f, PW);
            Sheet(row, Screen);
            if (icon != null) UiKit.Img(row, UiKit.Icon(icon), new Vector2(12f, 12f), new Vector2(-PW / 2f + 10f, 0f));
            InkRange(row, label, icon != null ? 22f : 6f, 106f, 0f, 12f, 8);
            InkRange(row, value, 110f, PW - 6f, 0f, 12f, 9, TextAnchor.MiddleRight, col);
        }

        private void ArmyApp(Transform c)
        {
            if (_global || _view == null)
            {
                RectTransform head = UiKit.Box(c, "Rank", 18f, PW);
                Sheet(head, new Color(0.82f, 0.87f, 0.95f));
                Ink9(head, Txt.T("net_army_rank"), Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
                int r = 1;
                foreach (var kv in G.Kingdoms().Where(x => x.isCiv()).Select(k => new KeyValuePair<Kingdom, Force>(k, Count(k))).OrderByDescending(x => x.Value.Power).Take(12))
                {
                    RectTransform row = UiKit.Box(c, "K", 28f, PW);
                    Sheet(row, Screen);
                    UiKit.Banner(row, kv.Key, new Vector2(-PW / 2f + 14f, 0f), 18f);
                    InkRange(row, r++ + ". " + G.Name(kv.Key), 30f, 126f, 6f, 12f, 9);
                    Force f = kv.Value;
                    Ink9(row, Txt.F("net_army_short", f.Warriors + f.Soldiers, f.Land, f.Air, f.Ships), new Vector2(12f, -7f), new Vector2(140f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
                    InkRange(row, G.Short(f.Power), 130f, PW - 6f, 6f, 12f, 10, TextAnchor.MiddleRight, Accent);
                }
                return;
            }
            Kingdom kk = _view;
            Force me = Count(kk);
            RectTransform top = UiKit.Box(c, "War", 30f, PW);
            Sheet(top, new Color(0.84f, 0.9f, 1f));
            UiKit.Banner(top, kk, new Vector2(-PW / 2f + 16f, 0f), 22f);
            var enemies = Groups.Enemies(kk).Select(G.Name).Distinct().ToList();
            Ink9(top, Txt.F("net_army_power", G.Short(me.Power)), new Vector2(12f, 7f), new Vector2(140f, 11f), 9, TextAnchor.MiddleLeft, Accent);
            Ink9(top, enemies.Count > 0 ? Txt.F("net_army_war", string.Join(", ", enemies.Take(3).ToArray())) : Txt.T("net_army_peace"), new Vector2(12f, -6f), new Vector2(140f, 10f), 7, TextAnchor.MiddleLeft, enemies.Count > 0 ? new Color(0.75f, 0.15f, 0.15f) : new Color(0.1f, 0.5f, 0.2f));
            Line(c, "ui/Icons/civ/item/civ_w_mg", Txt.T("net_army_warriors"), me.Warriors.ToString(), Ink);
            Line(c, "ui/Icons/civ/cfg/soldiers", Txt.T("net_army_soldiers"), me.Soldiers.ToString(), Ink);
            Line(c, "ui/Icons/civ/unit/tank", Txt.T("net_army_land"), me.Land.ToString(), Ink);
            Line(c, "ui/Icons/civ/unit/fighter", Txt.T("net_army_air"), me.Air.ToString(), Ink);
            Line(c, "ui/Icons/civ/unit/destroyer", Txt.T("net_army_ships"), me.Ships.ToString(), Ink);
            if (me.Hangar > 0) Line(c, "ui/Icons/civ/unit/carrier", Txt.T("net_army_hangar"), me.Hangar.ToString(), Ink);
            if (me.Missiles > 0) Line(c, "ui/Icons/civ/item/civ_w_missile", Txt.T("net_army_missiles"), me.Missiles.ToString(), Ink);
            int nukes = War.Nukes(kk);
            if (nukes > 0) Line(c, "ui/Icons/civ/world/nuke", Txt.T("net_army_nukes"), nukes.ToString(), new Color(0.75f, 0.15f, 0.15f));
            if (me.Empty > 0) Line(c, "ui/Icons/civ/item/civ_w_bolt", Txt.T("net_army_empty"), me.Empty.ToString(), new Color(0.75f, 0.15f, 0.15f));
            FormationDef form = Formations.For(kk);
            if (form != null) Line(c, "ui/Icons/civ/app/army", Txt.T("net_army_formation"), Txt.T("civ_form_" + form.Id), Accent);
            RectTransform gh = UiKit.Box(c, "Gens", 18f, PW);
            Sheet(gh, new Color(0.82f, 0.87f, 0.95f));
            Ink9(gh, Txt.T("net_army_generals"), Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
            int shown = 0;
            if (World.world.armies != null)
            {
                foreach (Army army in World.world.armies)
                {
                    if (army == null || !army.isAlive() || !army.hasCaptain()) continue;
                    Actor cap = army.getCaptain();
                    if (cap == null || cap.kingdom != kk) continue;
                    if (shown++ >= 10) break;
                    RectTransform row = UiKit.Box(c, "Gen", 26f, PW);
                    Sheet(row, Screen);
                    UiKit.Avatar(row, cap, new Vector2(-PW / 2f + 14f, 0f), 0.42f);
                    InkRange(row, cap.getName(), 30f, 146f, 5f, 11f, 9);
                    string style = Formations.Cautious(cap) ? Txt.T("net_army_cautious") : Txt.T("net_army_bold");
                    Ink9(row, style + " · " + Txt.F("net_army_size", army.units.Count), new Vector2(0f, -6f), new Vector2(116f, 9f), 7, TextAnchor.MiddleLeft, InkSoft);
                    Actor cc = cap;
                    PhoneKit.Btn(row, "", UiKit.Icon("ui/Icons/civ/net/people"), () =>
                    {
                        if (G.Ok(cc)) ActionLibrary.openUnitWindow(cc);
                    }, new Vector2(20f, 18f), new Vector2(PW / 2f - 14f, 0f));
                }
            }
            if (shown == 0) List(c, new List<NetPost>());
        }

        private void Settings(Transform c)
        {
            RectTransform head = UiKit.Box(c, "Net", 18f, PW);
            Sheet(head, new Color(0.82f, 0.87f, 0.95f));
            Ink9(head, Txt.T("net_settings_network"), Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
            var opts = new List<Kingdom> { null };
            opts.AddRange(G.Kingdoms().Where(Internet.Exists));
            foreach (Kingdom k in opts)
            {
                RectTransform row = UiKit.Box(c, "Opt", 22f, PW);
                bool on = k == null ? _global : !_global && _view == k;
                Sheet(row, on ? new Color(1f, 0.92f, 0.7f) : Screen);
                if (k != null) UiKit.Banner(row, k, new Vector2(-PW / 2f + 14f, 0f), 16f);
                else UiKit.Img(row, UiKit.Icon("ui/Icons/civ/btn/net"), new Vector2(16f, 16f), new Vector2(-PW / 2f + 14f, 0f));
                Ink9(row, k == null ? Txt.T("net_global") : G.Name(k), new Vector2(0f, 0f), new Vector2(120f, 12f), 9);
                Kingdom kk = k;
                PhoneKit.Btn(row, on ? "✔" : "○", null, () =>
                {
                    _global = kk == null;
                    if (kk != null) _view = kk;
                    Render();
                }, new Vector2(20f, 16f), new Vector2(PW / 2f - 14f, 0f));
            }
            RectTransform b = UiKit.Box(c, "Babel", 24f, PW);
            Sheet(b, Screen);
            InkRange(b, Txt.T("net_babel"), 6f, 148f, 0f, 12f, 9);
            PillBtn bb = PhoneKit.IconBtn(b, UiKit.Icon("ui/Icons/civ/net/babel"), () =>
            {
                _babel = !_babel;
                Render();
            }, 20f, new Vector2(PW / 2f - 14f, 0f), Txt.T("net_babel"), Txt.T("net_babel_d"));
            bb.Background.color = _babel ? new Color(1f, 0.85f, 0.45f) : Color.white;
        }

        private void People(Transform c)
        {
            var people = new List<Actor>();
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!Internet.Exists(k)) continue;
                if (!_global && _view != null && k != _view) continue;
                foreach (City ct in G.Cities(k)) foreach (Actor a in ct.units) if (G.Ok(a) && Internet.Followers(a) > 0) people.Add(a);
            }
            foreach (Actor a in people.OrderByDescending(Internet.Followers).Take(16))
            {
                RectTransform box = UiKit.Box(c, "Person", 28f, PW);
                Sheet(box, Screen);
                UiKit.Avatar(box, a, new Vector2(-PW / 2f + 14f, 0f), 0.45f);
                int f = Internet.Followers(a);
                bool cancelled = Store.Str(a.data, Internet.KCancel) == "cancelled";
                Ink9(box, a.getName() + (cancelled ? " <color=#D03A2A>✖</color>" : "") + (f > 2000 ? " <color=#2A6AD8>✔</color>" : ""), new Vector2(-16f, 6f), new Vector2(80f, 11f), 9);
                Ink9(box, Names.Handle(a) + " · " + Internet.TierLabel(f), new Vector2(-16f, -6f), new Vector2(80f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
                InkRange(box, G.Short(f), 116f, 152f, 0f, 12f, 9, TextAnchor.MiddleRight, Accent);
                Actor aa = a;
                PhoneKit.Btn(box, "@", null, () =>
                {
                    _profile = G.Id(aa);
                    Render();
                }, new Vector2(18f, 18f), new Vector2(PW / 2f - 12f, 0f));
            }
            if (people.Count == 0) List(c, new List<NetPost>());
        }

        private void Profile(Transform c)
        {
            Actor a = G.ActorById(_profile);
            List<NetPost> posts = Internet.Feed.Where(p => p.AuthorId == _profile && p.Who == "person").Reverse().ToList();
            RectTransform box = UiKit.Box(c, "Profile", 58f, PW);
            Sheet(box, new Color(0.84f, 0.9f, 1f));
            string name = a != null ? a.getName() : posts.Count > 0 ? posts[0].Author : "?";
            if (a != null) UiKit.Avatar(box, a, new Vector2(-PW / 2f + 24f, 2f), 0.8f);
            else UiKit.Img(box, UiKit.Icon(Icon("memorial")), new Vector2(28f, 28f), new Vector2(-PW / 2f + 24f, 2f));
            int f = a != null ? Internet.Followers(a) : 0;
            Ink9(box, name + (a == null ? "  ✝" : "") + (f > 2000 ? " <color=#2A6AD8>✔</color>" : ""), new Vector2(11f, 16f), new Vector2(98f, 12f), 10, TextAnchor.MiddleLeft, Accent);
            Ink9(box, (a != null ? Names.Handle(a) : posts.Count > 0 ? posts[0].Handle : "") + " · " + Internet.TierLabel(f), new Vector2(11f, 3f), new Vector2(98f, 10f), 8, TextAnchor.MiddleLeft, InkSoft);
            Ink9(box, Txt.F("net_profile_stats", G.Short(f), posts.Count, G.Short(posts.Sum(p => p.Likes))), new Vector2(22f, -10f), new Vector2(120f, 10f), 8);
            if (a != null)
            {
                string bio = Roles.Of(a);
                Opinion o = Politics.Get(a);
                string ideo = o != null ? Txt.T("civ_p_" + o.Ideo) : "";
                Ink9(box, (bio.Length > 0 ? Txt.T("civ_role_" + bio) + " · " : "") + ideo, new Vector2(22f, -21f), new Vector2(120f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
                Actor aa = a;
                PhoneKit.Btn(box, "", UiKit.Icon("ui/Icons/civ/net/people"), () =>
                {
                    if (G.Ok(aa)) ActionLibrary.openUnitWindow(aa);
                }, new Vector2(20f, 18f), new Vector2(PW / 2f - 14f, 14f));
            }
            else if (posts.Count > 0)
            {
                Ink9(box, Txt.T("net_memorial"), new Vector2(22f, -21f), new Vector2(120f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
            }
            List(c, posts);
        }
    }
}
