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

        private static readonly string[] Apps = { "pixelgram", "squawk", "pigeon", "torch", "herald", "ballot", "exchange", "trends", "search", "alerts", "calendar", "memorial", "embassy", "army", "chest", "contacts", "settings" };
        private static readonly string[] Basic = { "squawk", "pigeon", "herald", "ballot", "trends", "search", "alerts", "calendar", "memorial", "army", "contacts", "settings" };
        private static readonly string[] Dock = { "pixelgram", "squawk", "pigeon", "herald" };
        private static readonly string[] OldSections = { "herald", "squawk", "trends", "search", "alerts", "memorial", "calendar", "army", "chest", "settings" };
        private static readonly string[] PaperSections = { "herald", "squawk", "exchange", "trends", "search", "alerts", "ballot", "memorial", "calendar", "army", "chest", "settings" };

        private static NetSkin K = NetSkin.For("social");
        private static Color Screen { get { return K.Card; } }
        private static Color Ink { get { return K.Ink; } }
        private static Color InkSoft { get { return K.InkSoft; } }
        private static Color Accent { get { return K.Accent; } }

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
        private static string _acct = "";
        private static string _topic = "";
        private int _seen = -1;
        private float _poll;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open()
        {
            if (!Internet.ReachAnywhere())
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
            if (!_global && (!G.Ok(_view) || !Internet.Reach(_view)))
            {
                Kingdom sel = SelectedMetas.selected_kingdom;
                _view = Internet.Reach(sel) ? sel : G.Kingdoms().FirstOrDefault(Internet.Exists) ?? G.Kingdoms().FirstOrDefault();
            }
            else if (!_global && SelectedMetas.selected_kingdom != null && Internet.Reach(SelectedMetas.selected_kingdom) && SelectedMetas.selected_kingdom != _view)
            {
                _view = SelectedMetas.selected_kingdom;
                _app = "";
                _post = -1;
                _profile = "";
                _acct = "";
            }
            Render();
        }

        private void Update()
        {
            if (!IsOpened) return;
            Place();
            _poll += Time.unscaledDeltaTime;
            if (_poll < 1.5f) return;
            _poll = 0f;
            if (_seen != Internet.Version && (_app == "torch" || _app == "pigeon" || _app == "alerts" || _post >= 0)) Render();
        }

        private void Go(string app)
        {
            _app = app;
            _page = 0;
            _post = -1;
            _profile = "";
            _acct = "";
            if (app == "alerts") NetHub.Seen();
            Render();
        }

        private static string Medium()
        {
            if (_global || _view == null) return NetMedia.Best();
            return NetMedia.Of(_view);
        }

        private static bool Smart()
        {
            if (_global) return G.Kingdoms().Any(k => Internet.Exists(k) && Research.Has(k, "smartphones"));
            return _view != null && Research.Has(_view, "smartphones");
        }

        private static IEnumerable<string> Available()
        {
            if (K.Social) return Smart() ? Apps : Basic;
            return K.Med == "paper" || K.Med == "broadcast" ? PaperSections : OldSections;
        }

        private static string Icon(string app)
        {
            return "ui/Icons/civ/app/" + app;
        }

        private static string AppName(string app)
        {
            if (!K.Social && Txt.Has("net_sec_" + app + "_" + K.Med)) return Txt.T("net_sec_" + app + "_" + K.Med);
            if (!K.Social && Txt.Has("net_sec_" + app)) return Txt.T("net_sec_" + app);
            return Txt.T("net_app_" + app);
        }

        private void Render()
        {
            _seen = Internet.Version;
            Theme();
            K = NetSkin.For(Medium());
            Transform root = ContentTransform;
            UiKit.Clear(root);
            RectTransform phone = UiKit.Box(root, "Phone", PH, PW + 16f);
            _phone = phone;
            phone.localScale = new Vector3(_fit, _fit, 1f);
            Frame(phone);
            bool home = _app.Length == 0;
            var sgo = new GameObject("Stack", typeof(RectTransform), typeof(Image));
            sgo.transform.SetParent(phone, false);
            RectTransform srt = sgo.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(PW, PH - 28f);
            srt.localPosition = Vector3.zero;
            Image sbg = sgo.GetComponent<Image>();
            sbg.sprite = K.Round ? PhoneKit.Round : PhoneKit.Square;
            sbg.type = K.Round ? Image.Type.Sliced : Image.Type.Simple;
            sbg.color = home && K.Social ? Wall : K.Page;
            sbg.raycastTarget = false;
            VerticalLayoutGroup vl = sgo.AddComponent<VerticalLayoutGroup>();
            vl.childControlHeight = true;
            vl.childControlWidth = false;
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth = false;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.spacing = 0;
            Transform st = sgo.transform;
            if (!G.Loaded || !Internet.ReachAnywhere())
            {
                UiKit.Label(st, Txt.T("net_not_yet"), 40f, 10, TextAnchor.MiddleCenter, Color.white);
                return;
            }
            Status(st);
            if (home)
            {
                if (K.Social)
                {
                    HomeTop(st);
                    Transform body = Body(st);
                    Home(body);
                    DockRow(st);
                    HomeBar(st, Wall, new Color(1f, 1f, 1f, 0.75f));
                    return;
                }
                Masthead(st);
                Transform sec = Body(st);
                Sections(sec);
                HomeBar(st, K.Page, K.InkSoft);
                return;
            }
            AppBar(st);
            Transform c = Body(st);
            if (_post >= 0) Single(c);
            else if (_profile.Length > 0) Profile(c);
            else if (_acct.Length > 0) Account(c);
            else Content(c);
            HomeBar(st, K.Social ? Screen : K.Page, K.Social ? new Color(0.2f, 0.22f, 0.28f) : K.InkSoft);
        }

        private void Frame(RectTransform phone)
        {
            switch (K.Med)
            {
                case "scroll":
                    PhoneKit.Rect(phone, K.Bezel, new Vector2(PW + 8f, PH - 10f), Vector2.zero, false);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float y = s * (PH / 2f - 6f);
                        PhoneKit.Rect(phone, new Color(0.36f, 0.22f, 0.1f), new Vector2(PW + 24f, 12f), new Vector2(0f, y));
                        PhoneKit.Rect(phone, new Color(0.55f, 0.36f, 0.18f), new Vector2(PW + 20f, 4f), new Vector2(0f, y + 2f), false);
                        PhoneKit.Rect(phone, new Color(0.8f, 0.62f, 0.25f), new Vector2(10f, 16f), new Vector2(-PW / 2f - 14f, y));
                        PhoneKit.Rect(phone, new Color(0.8f, 0.62f, 0.25f), new Vector2(10f, 16f), new Vector2(PW / 2f + 14f, y));
                    }
                    break;
                case "crier":
                    PhoneKit.Rect(phone, Shade(K.Bezel, 0.7f), new Vector2(PW + 18f, PH + 2f), new Vector2(1f, -1f));
                    PhoneKit.Rect(phone, K.Bezel, new Vector2(PW + 16f, PH), Vector2.zero);
                    for (int i = 0; i < 6; i++) PhoneKit.Rect(phone, Shade(K.Bezel, 0.82f), new Vector2(3f + i % 3, 2f), new Vector2(-PW / 2f + 2f + i * 31f, PH / 2f - 3f - (i % 2) * 2f), false);
                    break;
                case "gazette":
                case "paper":
                    PhoneKit.Rect(phone, K.Bezel, new Vector2(PW + 16f, PH), Vector2.zero, false);
                    PhoneKit.Rect(phone, Shade(K.Page, 0.85f), new Vector2(PW + 4f, PH - 22f), new Vector2(2f, -2f), false);
                    break;
                case "broadcast":
                    PhoneKit.Rect(phone, K.Bezel, new Vector2(PW + 16f, PH), Vector2.zero);
                    PhoneKit.Rect(phone, Shade(K.Bezel, 0.75f), new Vector2(3f, 22f), new Vector2(-20f, PH / 2f + 8f), false);
                    PhoneKit.Rect(phone, Shade(K.Bezel, 0.75f), new Vector2(3f, 22f), new Vector2(20f, PH / 2f + 8f), false);
                    PhoneKit.Rect(phone, new Color(0.85f, 0.78f, 0.6f), new Vector2(7f, 7f), new Vector2(-PW / 2f - 3f, -PH / 2f + 8f));
                    PhoneKit.Rect(phone, new Color(0.85f, 0.78f, 0.6f), new Vector2(7f, 7f), new Vector2(PW / 2f - 10f, -PH / 2f + 8f));
                    break;
                default:
                    PhoneKit.Rect(phone, PhoneKit.Bezel, new Vector2(PW + 16f, PH), Vector2.zero);
                    PhoneKit.Rect(phone, new Color(0.2f, 0.2f, 0.24f), new Vector2(44f, 5f), new Vector2(0f, PH / 2f - 7f));
                    break;
            }
            PhoneKit.Btn(phone, "✖", null, Close, new Vector2(16f, 11f), new Vector2(PW / 2f - 6f, PH / 2f - 7f), new Color(0.75f, 0.22f, 0.22f), Color.white);
        }

        private static Color Shade(Color c, float f)
        {
            return new Color(c.r * f, c.g * f, c.b * f, c.a);
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
                case "herald": List(c, Visible().Where(p => p.Who == "kingdom" || p.Who == "system" || p.Who == "religion" || p.Who == "press" || p.Who == "org").Reverse().ToList()); break;
                case "ballot": List(c, Visible().Where(p => p.Poll != null || p.Kind == "election" || p.Kind == "org_vote").Reverse().ToList()); break;
                case "exchange": Exchange(c); break;
                case "calendar": List(c, Visible().Where(p => p.Kind == "birthday" || p.Kind == "city_anniv" || p.Kind == "throwback" || p.Kind == "festival" || p.Kind == "fireworks").Reverse().ToList()); break;
                case "memorial": List(c, Visible().Where(p => p.Kind == "obituary" || p.Kind == "rip" || p.Kind == "kingdom_gone").Reverse().ToList()); break;
                case "embassy": Embassy(c); break;
                case "army": ArmyApp(c); break;
                case "chest": List(c, Internet.Feed.Where(p => p.Saved).Reverse().ToList()); break;
                case "contacts": People(c); break;
                case "settings": Settings(c); break;
                case "trends": Trends(c); break;
                case "search": Search(c); break;
                case "alerts": Alerts(c); break;
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

        private static Image Sheet(RectTransform box, Color col)
        {
            bool card = box.name == "Post" || box.name == "Comment" || box.name == "Person" || box.name == "Rep" || box.name == "Gen" || box.name == "K" || box.name == "Profile" || box.name == "Sec" || box.name == "Mast";
            if (!K.Round)
            {
                Image img = PhoneKit.Screen(box, col, 0f, 0f);
                if (card)
                {
                    float w = box.sizeDelta.x;
                    PhoneKit.Rect(box, K.Line, new Vector2(w - 8f, 1f), new Vector2(0f, -box.sizeDelta.y / 2f + 0.5f), false);
                }
                return img;
            }
            return PhoneKit.Screen(box, col, 0f, card ? 2f : 0f, K.Page);
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
                case "trends": return new Color(0.98f, 0.62f, 0.2f);
                case "search": return new Color(0.3f, 0.36f, 0.48f);
                case "alerts": return new Color(0.9f, 0.3f, 0.3f);
                default: return new Color(0.55f, 0.58f, 0.64f);
            }
        }

        private void Status(Transform c)
        {
            RectTransform box = UiKit.Box(c, "Status", 18f, PW);
            bool home = _app.Length == 0 && K.Social;
            PhoneKit.Screen(box, home ? WallDeep : K.Head, 0f, 0f);
            Color fg = home ? Color.white : Ink;
            Ink9(box, Txt.F("net_year", G.Year), new Vector2(-PW / 2f + 28f, 0f), new Vector2(46f, 12f), 7, TextAnchor.MiddleLeft, fg);
            List<Kingdom> all = G.Kingdoms().Where(Internet.Reach).ToList();
            PhoneKit.Btn(box, "‹", null, () => Cycle(all, -1), new Vector2(13f, 12f), new Vector2(-30f, 0f), new Color(1f, 1f, 1f, 0.15f), fg);
            string net = _global || _view == null ? Txt.T("net_global") : G.Name(_view);
            Ink9(box, net, new Vector2(0f, 0f), new Vector2(46f, 12f), 7, TextAnchor.MiddleCenter, fg);
            PhoneKit.Btn(box, "›", null, () => Cycle(all, 1), new Vector2(13f, 12f), new Vector2(30f, 0f), new Color(1f, 1f, 1f, 0.15f), fg);
            if (K.Social)
            {
                bool down = !_global && _view != null && G.Cities(_view).Count > 0 && G.Cities(_view).All(ct => Energy.Blackout(ct));
                Ink9(box, down ? "✖" : (Smart() ? "5G" : "2G"), new Vector2(PW / 2f - 34f, 0f), new Vector2(24f, 12f), 7, TextAnchor.MiddleRight, down ? new Color(1f, 0.4f, 0.35f) : fg);
                PhoneKit.Rect(box, fg, new Vector2(12f, 6f), new Vector2(PW / 2f - 13f, 0f));
                PhoneKit.Rect(box, fg, new Vector2(2f, 3f), new Vector2(PW / 2f - 6f, 0f), false);
            }
            else if (K.Med == "broadcast")
            {
                UiKit.Text(box, "<color=#E0302A>●</color> " + Txt.T("net_on_air"), new Vector2(PW / 2f - 26f, 0f), new Vector2(44f, 12f), 7, TextAnchor.MiddleRight, fg);
            }
            if (NetHub.Unseen > 0 && _app != "alerts")
            {
                PillBtn b = PhoneKit.Btn(box, NetHub.Unseen.ToString(), null, () => Go("alerts"), new Vector2(16f, 11f), new Vector2(-PW / 2f + 7f, 0f), new Color(0.88f, 0.22f, 0.2f), Color.white);
                if (b.Label != null) b.Label.fontSize = 7;
            }
        }

        private void Cycle(List<Kingdom> all, int d)
        {
            var opts = new List<Kingdom> { null };
            opts.AddRange(all);
            int i = _global ? 0 : Math.Max(0, opts.IndexOf(_view));
            int n = ((i + d) % opts.Count + opts.Count) % opts.Count;
            _global = opts[n] == null;
            if (!_global) _view = opts[n];
            _page = 0;
            _post = -1;
            _profile = "";
            _acct = "";
            string med = Medium();
            if (_app.Length > 0 && !Available().Contains(_app) && NetSkin.For(med).Social == K.Social) _app = "";
            if (NetSkin.For(med).Social != K.Social) _app = "";
            Render();
        }

        private void HomeTop(Transform c)
        {
            RectTransform wall = UiKit.Box(c, "Wall", 30f, PW);
            PhoneKit.Screen(wall, WallDeep, 0f, 0f);
            if (!_global && _view != null) UiKit.Banner(wall, _view, new Vector2(-PW / 2f + 18f, 0f), 18f);
            else UiKit.Img(wall, UiKit.Icon("ui/Icons/civ/btn/net"), new Vector2(18f, 18f), new Vector2(-PW / 2f + 18f, 0f));
            Ink9(wall, _global || _view == null ? Txt.T("net_global") : G.Name(_view), new Vector2(18f, 5f), new Vector2(130f, 12f), 9, TextAnchor.MiddleLeft, Color.white);
            Ink9(wall, NetName(), new Vector2(18f, -7f), new Vector2(130f, 10f), 7, TextAnchor.MiddleLeft, new Color(0.8f, 0.86f, 1f));
        }

        private string NetName()
        {
            Kingdom k = !_global && G.Ok(_view) ? _view : EraLock.Leader;
            int scale = G.Ok(k) ? CosmosSim.CachedScale(k) : 0;
            if (G.Ok(k) && Research.Era(k) >= Research.PlanetEras)
            {
                if (scale >= 5) return Txt.T("cos_net_universal");
                if (scale >= 2) return Txt.T("cos_net_galactic");
                return Txt.T("cos_net_interplanetary");
            }
            return Smart() ? Txt.T("net_phone_smart") : Txt.T("net_phone_brick");
        }

        private const float Step = 40f;

        private void Home(Transform c)
        {
            UiKit.Box(c, "Pad", 6f, PW);
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
            Ink9(row, AppName(app), new Vector2(pos.x, pos.y - 20f), new Vector2(Step, 9f), 6, TextAnchor.MiddleCenter, label);
            if (app == "alerts" && NetHub.Unseen > 0)
            {
                PillBtn b = PhoneKit.Btn(row, NetHub.Unseen.ToString(), null, () => Go(a), new Vector2(13f, 10f), new Vector2(pos.x + 12f, pos.y + 12f), new Color(0.88f, 0.22f, 0.2f), Color.white);
                if (b.Label != null) b.Label.fontSize = 7;
            }
        }

        private void Masthead(Transform c)
        {
            RectTransform box = UiKit.Box(c, "Mast", 44f, PW);
            Sheet(box, K.Head);
            string title = _global || _view == null ? Txt.T("net_outlet_world_" + K.Med) : NetMedia.Outlet(_view, K.Med);
            if (!_global && _view != null) UiKit.Banner(box, _view, new Vector2(-PW / 2f + 16f, 4f), 22f);
            Ink9(box, title, new Vector2(12f, 8f), new Vector2(PW - 40f, 16f), K.Med == "paper" || K.Med == "gazette" ? 11 : 10, TextAnchor.MiddleCenter, K.Ink);
            Ink9(box, K.Device + " · " + Txt.F("net_year", G.Year), new Vector2(12f, -8f), new Vector2(PW - 40f, 10f), 7, TextAnchor.MiddleCenter, K.InkSoft);
            if (K.Med == "paper" || K.Med == "gazette")
            {
                PhoneKit.Rect(box, K.Ink, new Vector2(PW - 10f, 1f), new Vector2(0f, -15f), false);
                PhoneKit.Rect(box, K.Ink, new Vector2(PW - 10f, 1f), new Vector2(0f, -17f), false);
            }
            if (!_global && _view != null)
            {
                string press = NetPress.Of(_view);
                Ink9(box, Txt.T("net_press_" + press), new Vector2(0f, -20f), new Vector2(PW - 10f, 8f), 6, TextAnchor.MiddleCenter, press == "state" ? new Color(0.75f, 0.18f, 0.15f) : K.InkSoft);
            }
        }

        private void Sections(Transform c)
        {
            NetPost lead = Visible().Where(p => p.Important && !p.Censored).LastOrDefault() ?? Visible().LastOrDefault();
            if (lead != null)
            {
                RectTransform head = UiKit.Box(c, "Comments", 14f, PW);
                Sheet(head, K.Page);
                Ink9(head, Txt.T("net_sec_lead_" + K.Med), Vector2.zero, new Vector2(PW - 10f, 12f), 8, TextAnchor.MiddleCenter, K.Accent);
                Card(c, lead);
            }
            foreach (string app in Available())
            {
                string a = app;
                RectTransform row = UiKit.Box(c, "Sec", 22f, PW);
                Sheet(row, Screen);
                UiKit.Img(row, UiKit.Icon(Icon(app)), new Vector2(14f, 14f), new Vector2(-PW / 2f + 12f, 0f), K.Med == "broadcast" ? Color.white : new Color(0.85f, 0.8f, 0.75f));
                Ink9(row, AppName(app), new Vector2(-PW / 2f + 82f, 0f), new Vector2(120f, 12f), 9, TextAnchor.MiddleLeft, K.Ink);
                string badge = app == "alerts" && NetHub.Unseen > 0 ? NetHub.Unseen.ToString() : "";
                if (badge.Length > 0) Ink9(row, badge, new Vector2(PW / 2f - 32f, 0f), new Vector2(20f, 12f), 8, TextAnchor.MiddleRight, new Color(0.8f, 0.15f, 0.12f));
                PillBtn go = PhoneKit.Btn(row, "›", null, () => Go(a), new Vector2(18f, 16f), new Vector2(PW / 2f - 12f, 0f), K.Pill, K.Ink);
                go.Background.color = K.Pill;
            }
        }

        private void AppBar(Transform c)
        {
            RectTransform box = UiKit.Box(c, "AppBar", 26f, PW);
            PhoneKit.Screen(box, K.Head, 0f, 0f);
            PhoneKit.Btn(box, "‹", null, () =>
            {
                if (_post >= 0) _post = -1;
                else if (_profile.Length > 0) _profile = "";
                else if (_acct.Length > 0) _acct = "";
                else _app = "";
                _page = 0;
                Render();
            }, new Vector2(22f, 18f), new Vector2(-PW / 2f + 18f, 0f), new Color(1f, 1f, 1f, 0f), Accent);
            if (K.Social) PhoneKit.Rect(box, Tile(_app), new Vector2(18f, 18f), new Vector2(-PW / 2f + 40f, 0f));
            UiKit.Img(box, UiKit.Icon(Icon(_app)), new Vector2(14f, 14f), new Vector2(-PW / 2f + 40f, 0f), K.Social || K.Med == "broadcast" ? Color.white : K.Ink);
            Ink9(box, AppName(_app), new Vector2(14f, 0f), new Vector2(120f, 14f), 11, TextAnchor.MiddleLeft, Ink);
            PhoneKit.Btn(box, null, null, () => Go(""), new Vector2(26f, 6f), new Vector2(PW / 2f - 22f, 0f), K.Pill);
            RectTransform line = UiKit.Box(c, "Line", 1f, PW);
            PhoneKit.Screen(line, K.Line, 0f, 0f);
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
                Ink9(e, Txt.T(K.Social ? "net_empty" : "net_empty_" + K.Med), Vector2.zero, new Vector2(PW - 10f, 20f), 9, TextAnchor.MiddleCenter, InkSoft);
                return;
            }
            int pages = (posts.Count + PageSize - 1) / PageSize;
            _page = Math.Max(0, Math.Min(_page, pages - 1));
            foreach (NetPost p in posts.Skip(_page * PageSize).Take(PageSize)) Card(c, p);
            PhoneKit.Pager(c, _page, pages, n =>
            {
                _page = n;
                Render();
            }, K.Page, PW, K.Accent, K.InkSoft);
        }

        private static int Lines(string text, int perLine)
        {
            int lines = 1;
            int run = 0;
            foreach (char ch in text ?? "")
            {
                if (ch == '\n' || ++run > perLine)
                {
                    lines++;
                    run = 0;
                }
            }
            return lines;
        }

        private static string RoleLabel(NetPost p)
        {
            if (p.Role.Length > 0 && Txt.Has("net_role_" + p.Role)) return Txt.T("net_role_" + p.Role);
            return "";
        }

        private static string Badge(NetPost p)
        {
            if (p.Verified) return " <color=#2A6AD8>✔</color>";
            if (p.Who == "company") return " ◆";
            if (p.Who == "religion") return " ✝";
            return "";
        }

        private bool ShowPhoto(NetPost p)
        {
            if (!NetPhoto.Has(p)) return false;
            if (!K.Social) return p.Important || p.Img.Length > 0;
            return _app == "pixelgram" || _app == "herald" || _app == "torch" || _app == "calendar" || _app == "memorial" || _app == "alerts" || _app == "trends" || _app == "search" || p.Important || _post == p.Id || (p.Img.Length > 0 && p.Img != "city");
        }

        private void Card(Transform c, NetPost p)
        {
            string text = p.Censored ? Txt.T("net_censored_text") : Shown(p);
            if (p.Sponsor.Length > 0 && !p.Censored) text += "\n" + Txt.F("net_sponsored_by", p.Sponsor);
            int lines = Lines(text, 33);
            float textH = lines * 10f + 2f;
            float pollH = p.Poll != null ? p.Poll.Count * 14f + 4f : 0f;
            bool photo = ShowPhoto(p);
            float picH = photo ? 94f : p.Icon.Length > 0 ? 40f : 0f;
            float h = 28f + textH + pollH + picH + 18f;
            RectTransform box = UiKit.Box(c, "Post", h, PW);
            Color bgc = Screen;
            if (p.Viral || p.Flame) bgc = K.Hot;
            if (p.Dm) bgc = K.Social ? new Color(0.86f, 0.9f, 1f) : Shade(Screen, 0.95f);
            if (p.Fake && p.Checked) bgc = K.Med == "broadcast" ? new Color(0.36f, 0.18f, 0.18f) : new Color(1f, 0.84f, 0.84f);
            if (p.Kind == "obituary" || p.Kind == "rip") bgc = K.Med == "broadcast" ? new Color(0.22f, 0.22f, 0.24f) : new Color(0.86f, 0.86f, 0.88f);
            Sheet(box, bgc);
            float top = h / 2f;
            Avatar(box, p, new Vector2(-PW / 2f + 13f, top - 13f), 0.45f);
            string who = p.Author + Badge(p);
            bool official = p.Who != "person";
            PillBtn nameBtn = PhoneKit.Btn(box, "", null, () => OpenAuthor(p), new Vector2(120f, 11f), new Vector2(-PW / 2f + 88f, top - 8f), new Color(1f, 1f, 1f, 0f));
            nameBtn.Background.color = new Color(1f, 1f, 1f, 0f);
            Ink9(box, who, new Vector2(-PW / 2f + 88f, top - 8f), new Vector2(120f, 11f), 9, TextAnchor.MiddleLeft, official ? K.Official : Ink);
            string role = RoleLabel(p);
            string meta = p.Handle + " · " + Txt.F("net_year", p.Year) + (p.Place.Length > 0 ? " · " + p.Place : "");
            if (role.Length > 0) meta = role + " · " + Txt.F("net_year", p.Year);
            if (p.Dm) meta = p.Handle + " → " + p.ToName + " · " + Txt.F("net_year", p.Year);
            else if (p.Community.Length > 0) meta = p.Community + " · " + p.Handle + " · " + Txt.F("net_year", p.Year);
            Ink9(box, meta, new Vector2(-PW / 2f + 88f, top - 18f), new Vector2(120f, 9f), 7, TextAnchor.MiddleLeft, InkSoft);
            if (p.Geo.Length > 0) Ink9(box, p.Geo, new Vector2(-PW / 2f + 88f, top - 26f), new Vector2(170f, 8f), 6, TextAnchor.MiddleLeft, InkSoft);
            string label = "";
            Color lc = Accent;
            if (p.Live && !p.LiveEnded)
            {
                UiKit.Text(box, "<color=#E0302A>● LIVE</color> " + p.Viewers, new Vector2(PW / 2f - 30f, top - 8f), new Vector2(56f, 10f), 8, TextAnchor.MiddleRight, Ink);
            }
            else if (p.Censored) { label = Txt.T("net_censored"); lc = new Color(0.8f, 0.15f, 0.12f); }
            else if (p.Fake && p.Checked) { label = Txt.T("net_fake_label"); lc = new Color(0.85f, 0.2f, 0.2f); }
            else if (p.Rumor) { label = Txt.T("net_rumor_label"); lc = new Color(0.75f, 0.5f, 0.1f); }
            else if (p.Propaganda) { label = Txt.T("net_propaganda_label"); lc = new Color(0.75f, 0.18f, 0.15f); }
            else if (p.Sponsor.Length > 0) { label = Txt.T("net_sponsored"); lc = new Color(0.45f, 0.45f, 0.5f); }
            else if (p.Flame) { label = Txt.T("net_flame_label"); lc = new Color(0.9f, 0.4f, 0.1f); }
            else if (p.Viral) { label = Txt.T("net_viral"); lc = new Color(0.9f, 0.45f, 0.1f); }
            if (label.Length > 0) Ink9(box, label, new Vector2(PW / 2f - 30f, top - 8f), new Vector2(56f, 10f), 7, TextAnchor.MiddleRight, lc);
            if (p.Tag.Length > 0) Ink9(box, p.Tag, new Vector2(PW / 2f - 30f, top - 18f), new Vector2(56f, 9f), 7, TextAnchor.MiddleRight, Accent);
            float y = top - 30f;
            if (photo)
            {
                Sprite snap = NetPhoto.Get(p);
                if (snap != null)
                {
                    PhoneKit.Rect(box, K.Social ? new Color(0f, 0f, 0f, 0.15f) : K.Line, new Vector2(164f, 94f), new Vector2(0f, y - 46f), false);
                    Image ph = UiKit.Img(box, snap, new Vector2(160f, 90f), new Vector2(0f, y - 46f));
                    ph.preserveAspect = false;
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
            if (p.Censored)
            {
                for (int i = 0; i < lines; i++) PhoneKit.Rect(box, new Color(0.12f, 0.12f, 0.14f), new Vector2(PW - 20f - (i * 23) % 40, 7f), new Vector2(-((i * 23) % 40) / 2f, y - 5f - i * 10f), false);
            }
            else
            {
                Text body = Ink9(box, text, new Vector2(0f, y - textH / 2f), new Vector2(PW - 12f, textH), 8, TextAnchor.UpperLeft);
                body.resizeTextForBestFit = false;
                body.verticalOverflow = VerticalWrapMode.Overflow;
            }
            y -= textH;
            if (p.Poll != null) y = Poll(box, p, y);
            Footer(box, p, -h / 2f + 9f);
        }

        private void Footer(RectTransform box, NetPost p, float fy)
        {
            NetPost pp = p;
            if (p.Kind == "obituary" || p.Kind == "rip")
            {
                PillBtn candle = PhoneKit.Btn(box, p.Candles.ToString(), UiKit.Icon(Icon("memorial")), () =>
                {
                    Internet.Candle(pp);
                    Render();
                }, new Vector2(46f, 15f), new Vector2(-PW / 2f + 28f, fy));
                candle.Background.color = new Color(1f, 0.88f, 0.55f);
            }
            else
            {
                string lk = K.Social ? (p.Liked ? "♥ " : "♡ ") + G.Short(p.Likes) : K.Like + " " + G.Short(p.Likes);
                PillBtn like = PhoneKit.Btn(box, lk, null, () =>
                {
                    Internet.Like(pp);
                    Render();
                }, new Vector2(46f, 15f), new Vector2(-PW / 2f + 28f, fy), null, K.Ink);
                like.Background.color = p.Liked ? new Color(1f, 0.6f, 0.65f) : K.Pill;
                if (like.Label != null) like.Label.color = p.Liked ? new Color(0.13f, 0.15f, 0.2f) : K.Ink;
            }
            PillBtn com = PhoneKit.Btn(box, (K.Social ? "✉ " : "") + p.Comments.Count, null, () =>
            {
                _post = pp.Id;
                Render();
            }, new Vector2(32f, 15f), new Vector2(-PW / 2f + 70f, fy), null, K.Ink);
            com.Background.color = K.Pill;
            if (com.Label != null) com.Label.color = K.Ink;
            Ink9(box, "↻ " + G.Short(p.Shares), new Vector2(4f, fy), new Vector2(40f, 12f), 7, TextAnchor.MiddleLeft, InkSoft);
            float x = PW / 2f - 12f;
            if (p.Who == "person" && p.AuthorId.Length > 0)
            {
                PhoneKit.Btn(box, "@", null, () =>
                {
                    _profile = pp.AuthorId;
                    _acct = "";
                    _post = -1;
                    Render();
                }, new Vector2(18f, 15f), new Vector2(x, fy), K.Pill, K.Ink);
                x -= 20f;
            }
            PillBtn save = PhoneKit.Btn(box, p.Saved ? "★" : "☆", null, () =>
            {
                Internet.SaveToggle(pp);
                Render();
            }, new Vector2(18f, 15f), new Vector2(x, fy), null, K.Ink);
            save.Background.color = p.Saved ? new Color(1f, 0.85f, 0.45f) : K.Pill;
            x -= 20f;
            if (p.Log.Length > 0)
            {
                PillBtn log = PhoneKit.Btn(box, null, UiKit.Icon("ui/Icons/civ/net/log"), () => NetHub.OpenLogFor(pp), new Vector2(18f, 15f), new Vector2(x, fy), K.Pill);
                log.Background.color = K.Pill;
            }
        }

        private void OpenAuthor(NetPost p)
        {
            if (p.Who == "person")
            {
                if (p.AuthorId.Length == 0) return;
                _profile = p.AuthorId;
                _acct = "";
            }
            else
            {
                _acct = p.Who + "|" + p.AuthorId;
                _profile = "";
            }
            _post = -1;
            _page = 0;
            Render();
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
                UiKit.Bar(box, v, win ? new Color(1f, 0.8f, 0.3f) : new Color(0.4f, 0.7f, 1f), new Vector2(150f, 12f), new Vector2(-20f, y + 7f), label);
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
            if (p.Who == "kingdom" || p.Who == "press")
            {
                Kingdom k = G.KingdomById(p.Kingdom);
                if (k != null)
                {
                    UiKit.Banner(box, k, pos, 22f);
                    if (p.Who == "press") UiKit.Img(box, UiKit.Icon("ui/Icons/civ/net/press"), new Vector2(11f, 11f), pos + new Vector2(7f, -7f));
                    return;
                }
                UiKit.Img(box, UiKit.Icon("ui/Icons/civ/net/press"), new Vector2(20f, 20f), pos);
                return;
            }
            if (p.Who == "org")
            {
                UiKit.Img(box, UiKit.Icon("ui/Icons/civ/net/org"), new Vector2(20f, 20f), pos);
                return;
            }
            if (p.Icon.Length > 0)
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
            Sheet(head, K.Head);
            Ink9(head, K.Talk + " (" + p.Comments.Count + ")", Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
            if (p.Censored)
            {
                RectTransform e = UiKit.Box(c, "Comment", 24f, PW);
                Sheet(e, Screen);
                Ink9(e, Txt.T("net_censored_comments"), Vector2.zero, new Vector2(PW - 10f, 20f), 8, TextAnchor.MiddleCenter, InkSoft);
                return;
            }
            foreach (NetComment cm in p.Comments.AsEnumerable().Reverse())
            {
                int lines = Lines(cm.Text, 28);
                float h = 16f + lines * 10f;
                RectTransform box = UiKit.Box(c, "Comment", h, PW);
                Sheet(box, Shade(Screen, K.Med == "broadcast" ? 0.85f : 0.98f));
                Actor a = G.ActorById(cm.AuthorId);
                Vector2 ap = new Vector2(-PW / 2f + 12f, h / 2f - 11f);
                if (a == null || UiKit.Avatar(box, a, ap, 0.38f) == null) UiKit.Img(box, UiKit.Icon("ui/Icons/civ/net/ghost"), new Vector2(16f, 16f), ap);
                string flag = a != null && a.kingdom != null && G.Id(a.kingdom) != p.Kingdom ? " <color=#B06A20>[" + G.Name(a.kingdom) + "]</color>" : "";
                Ink9(box, cm.Author + (K.Social ? "  <color=#6A7080>" + cm.Handle + "</color>" : "") + flag, new Vector2(14f, h / 2f - 7f), new Vector2(PW - 40f, 10f), 8);
                Text t = Ink9(box, cm.Text, new Vector2(14f, h / 2f - 13f - lines * 5f), new Vector2(PW - 40f, lines * 10f), 8, TextAnchor.UpperLeft);
                t.resizeTextForBestFit = false;
                Ink9(box, (K.Social ? "♥ " : "+") + cm.Likes, new Vector2(PW / 2f - 16f, h / 2f - 7f), new Vector2(30f, 10f), 7, TextAnchor.MiddleRight, InkSoft);
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
            if (_page == 0 && K.Social)
            {
                RectTransform box = UiKit.Box(c, "Trend", 40f, PW);
                Sheet(box, new Color(0.84f, 0.9f, 1f));
                Ink9(box, Txt.T("net_trending"), new Vector2(0f, 10f), new Vector2(PW - 10f, 12f), 8, TextAnchor.MiddleCenter, InkSoft);
                string trend = string.IsNullOrEmpty(Internet.Trend) ? "-" : Internet.Trend;
                Ink9(box, trend, new Vector2(0f, -6f), new Vector2(PW - 10f, 16f), 13, TextAnchor.MiddleCenter, Accent);
            }
            List(c, Visible().Where(p => !p.Live && p.Who != "religion").Reverse().ToList());
        }

        private void Trends(Transform c)
        {
            int since = G.Year - 3;
            var recent = Visible().Where(p => p.Year >= since).ToList();
            RectTransform head = UiKit.Box(c, "Comments", 18f, PW);
            Sheet(head, K.Head);
            Ink9(head, Txt.T("net_trends_tags_" + NetMedia.Register(K.Med)), Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
            var tags = recent.Where(p => p.Tag.Length > 0).GroupBy(p => p.Tag).Select(g => new { Tag = g.Key, N = g.Count(), L = g.Sum(x => x.Likes) }).OrderByDescending(x => x.L + x.N * 50).Take(6).ToList();
            var topics = recent.GroupBy(p => p.Topic).Select(g => new { Topic = g.Key, N = g.Count() }).OrderByDescending(x => x.N).Take(4).ToList();
            int rank = 1;
            foreach (var t in tags)
            {
                RectTransform row = UiKit.Box(c, "K", 20f, PW);
                Sheet(row, Screen);
                Ink9(row, rank++ + ". " + t.Tag, new Vector2(-14f, 0f), new Vector2(PW - 50f, 12f), 9, TextAnchor.MiddleLeft, Accent);
                Ink9(row, Txt.F("net_trends_posts", t.N), new Vector2(PW / 2f - 26f, 0f), new Vector2(48f, 12f), 7, TextAnchor.MiddleRight, InkSoft);
            }
            foreach (var t in topics)
            {
                if (string.IsNullOrEmpty(t.Topic)) continue;
                string topic = t.Topic;
                RectTransform row = UiKit.Box(c, "K", 20f, PW);
                Sheet(row, Screen);
                Ink9(row, rank++ + ". " + Txt.T("net_topic_" + topic), new Vector2(-14f, 0f), new Vector2(PW - 50f, 12f), 9, TextAnchor.MiddleLeft, Ink);
                PhoneKit.Btn(row, "›", null, () =>
                {
                    _topic = topic;
                    _app = "search";
                    _page = 0;
                    Render();
                }, new Vector2(18f, 14f), new Vector2(PW / 2f - 12f, 0f), K.Pill, K.Ink);
            }
            var talked = recent.Where(p => p.About.Length > 0).GroupBy(p => p.About).OrderByDescending(g => g.Count()).Select(g => G.KingdomById(g.Key)).Where(k => k != null).Take(3).ToList();
            if (talked.Count > 0)
            {
                RectTransform tb = UiKit.Box(c, "Comments", 30f, PW);
                Sheet(tb, K.Head);
                Ink9(tb, Txt.T("net_trends_talked"), new Vector2(0f, 7f), new Vector2(PW - 10f, 10f), 7, TextAnchor.MiddleCenter, InkSoft);
                Ink9(tb, string.Join("  ·  ", talked.Select(G.Name).ToArray()), new Vector2(0f, -5f), new Vector2(PW - 10f, 12f), 8, TextAnchor.MiddleCenter, Accent);
            }
            List(c, recent.Where(p => p.Year >= G.Year - 1).OrderByDescending(p => p.Likes + p.Shares * 3 + p.Comments.Count * 20).Take(PageSize * 2).ToList());
        }

        private void Search(Transform c)
        {
            RectTransform head = UiKit.Box(c, "Chips", 64f, PW);
            Sheet(head, K.Head);
            var all = new List<string> { "" };
            all.AddRange(NetMedia.Topics);
            for (int i = 0; i < all.Count; i++)
            {
                string t = all[i];
                int r = i / 4, col = i % 4;
                bool on = _topic == t;
                PillBtn chip = PhoneKit.Btn(head, t.Length == 0 ? Txt.T("net_topic_all") : Txt.T("net_topic_" + t), null, () =>
                {
                    _topic = t;
                    _page = 0;
                    Render();
                }, new Vector2(40f, 13f), new Vector2(-PW / 2f + 23f + col * 43f, 22f - r * 16f), on ? K.Accent : K.Pill, on ? Color.white : K.Ink);
                if (chip.Label != null) chip.Label.fontSize = 7;
            }
            var accounts = Visible().Where(p => p.Verified && p.Who != "person" && (_topic.Length == 0 || p.Topic == _topic)).GroupBy(p => p.Who + "|" + p.AuthorId).Select(g => g.Last()).OrderByDescending(p => p.Likes).Take(4).ToList();
            foreach (NetPost a in accounts)
            {
                NetPost aa = a;
                RectTransform row = UiKit.Box(c, "Person", 24f, PW);
                Sheet(row, Screen);
                Avatar(row, a, new Vector2(-PW / 2f + 13f, 0f), 0.4f);
                Ink9(row, a.Author + Badge(a), new Vector2(-6f, 4f), new Vector2(130f, 11f), 8, TextAnchor.MiddleLeft, K.Official);
                Ink9(row, RoleLabel(a).Length > 0 ? RoleLabel(a) : a.Handle, new Vector2(-6f, -6f), new Vector2(130f, 9f), 7, TextAnchor.MiddleLeft, InkSoft);
                PhoneKit.Btn(row, "›", null, () => OpenAuthor(aa), new Vector2(18f, 16f), new Vector2(PW / 2f - 12f, 0f), K.Pill, K.Ink);
            }
            List(c, Visible().Where(p => _topic.Length == 0 || p.Topic == _topic).Reverse().ToList());
        }

        private void Alerts(Transform c)
        {
            NetHub.Seen();
            var set = new HashSet<int>(NetHub.Alerts);
            List(c, Internet.Feed.Where(p => set.Contains(p.Id) && (_global || _view == null || p.Kingdom == G.Id(_view) || p.About == G.Id(_view) || Fog.Knows(_view, G.KingdomById(p.Kingdom)))).Reverse().ToList());
        }

        private void Exchange(Transform c)
        {
            if (Internet.CoinName.Length > 0 && K.Social)
            {
                RectTransform box = UiKit.Box(c, "Coin", 22f, PW);
                Sheet(box, new Color(0.86f, 0.96f, 0.86f));
                Ink9(box, Internet.CoinName, new Vector2(-30f, 0f), new Vector2(120f, 14f), 10, TextAnchor.MiddleLeft, new Color(0.1f, 0.5f, 0.2f));
                Ink9(box, Internet.CoinPrice.ToString("0.##"), new Vector2(PW / 2f - 40f, 0f), new Vector2(70f, 14f), 10, TextAnchor.MiddleRight, new Color(0.1f, 0.5f, 0.2f));
            }
            List(c, Visible().Where(p => p.Who == "company" || p.Topic == "economy").Reverse().ToList());
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
                UiKit.Banner(box, k, new Vector2(-PW / 2f + 16f, 14f), 22f);
                Ink9(box, G.Name(k), new Vector2(-PW / 2f + 92f, 18f), new Vector2(130f, 12f), 9);
                Ink9(box, G.Signed(r * 100f) + "%", new Vector2(PW / 2f - 30f, 18f), new Vector2(50f, 12f), 10, TextAnchor.MiddleRight, r >= 0 ? new Color(0.1f, 0.55f, 0.2f) : new Color(0.75f, 0.15f, 0.15f));
                string trade = r > 0.3f ? Txt.F("net_rep_trade_up", (int)(r * 20f)) : r < -0.3f ? Txt.F("net_rep_trade_down", (int)(-r * 15f)) : Txt.T("net_rep_trade_none");
                string mig = r > 0.35f ? Txt.T("net_rep_migrants_in") : r < 0f ? Txt.T("net_rep_migrants_out") : Txt.T("net_rep_migrants_none");
                string dip = Txt.F("net_rep_diplomacy", G.Signed(r * 20f));
                string bragT = brag > 1f ? Txt.F("net_rep_brag", (int)Math.Min(30f, brag * 3f)) : "";
                Text t = Ink9(box, trade + "\n" + mig + "\n" + dip + (bragT.Length > 0 ? "\n" + bragT : ""), new Vector2(8f, -8f), new Vector2(PW - 30f, 40f), 7, TextAnchor.UpperLeft, InkSoft);
                t.resizeTextForBestFit = false;
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
                    else if (d.Cls == "air" || d.Cls == "space") f.Air++;
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
            Ink9(row, label, new Vector2(-PW / 2f + 80f, 0f), new Vector2(130f, 12f), 8);
            Ink9(row, value, new Vector2(PW / 2f - 36f, 0f), new Vector2(64f, 12f), 9, TextAnchor.MiddleRight, col);
        }

        private void ArmyApp(Transform c)
        {
            if (_global || _view == null)
            {
                RectTransform head = UiKit.Box(c, "Rank", 18f, PW);
                Sheet(head, K.Head);
                Ink9(head, Txt.T("net_army_rank"), Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
                int r = 1;
                foreach (var kv in G.Kingdoms().Where(x => x.isCiv()).Select(k => new KeyValuePair<Kingdom, Force>(k, Count(k))).OrderByDescending(x => x.Value.Power).Take(12))
                {
                    RectTransform row = UiKit.Box(c, "K", 24f, PW);
                    Sheet(row, Screen);
                    UiKit.Banner(row, kv.Key, new Vector2(-PW / 2f + 14f, 0f), 18f);
                    Ink9(row, r++ + ". " + G.Name(kv.Key), new Vector2(-6f, 4f), new Vector2(140f, 11f), 9);
                    Force f = kv.Value;
                    Ink9(row, Txt.F("net_army_short", f.Warriors + f.Soldiers, f.Land, f.Air, f.Ships), new Vector2(-6f, -6f), new Vector2(140f, 9f), 7, TextAnchor.MiddleLeft, InkSoft);
                    Ink9(row, G.Short(f.Power), new Vector2(PW / 2f - 22f, 0f), new Vector2(40f, 12f), 10, TextAnchor.MiddleRight, Accent);
                }
                return;
            }
            Kingdom kk = _view;
            Force me = Count(kk);
            RectTransform top = UiKit.Box(c, "War", 30f, PW);
            Sheet(top, K.Head);
            UiKit.Banner(top, kk, new Vector2(-PW / 2f + 16f, 0f), 22f);
            var enemies = Groups.Enemies(kk).Select(G.Name).Distinct().ToList();
            Ink9(top, Txt.F("net_army_power", G.Short(me.Power)), new Vector2(10f, 7f), new Vector2(150f, 11f), 9, TextAnchor.MiddleLeft, Accent);
            Ink9(top, enemies.Count > 0 ? Txt.F("net_army_war", string.Join(", ", enemies.Take(3).ToArray())) : Txt.T("net_army_peace"), new Vector2(10f, -6f), new Vector2(150f, 10f), 7, TextAnchor.MiddleLeft, enemies.Count > 0 ? new Color(0.75f, 0.15f, 0.15f) : new Color(0.1f, 0.5f, 0.2f));
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
            Sheet(gh, K.Head);
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
                    Ink9(row, cap.getName(), new Vector2(-8f, 5f), new Vector2(130f, 11f), 9);
                    string style = Formations.Cautious(cap) ? Txt.T("net_army_cautious") : Txt.T("net_army_bold");
                    Ink9(row, style + " · " + Txt.F("net_army_size", army.units.Count), new Vector2(-8f, -6f), new Vector2(130f, 9f), 7, TextAnchor.MiddleLeft, InkSoft);
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
            Sheet(head, K.Head);
            Ink9(head, Txt.T("net_settings_network"), Vector2.zero, new Vector2(PW - 10f, 14f), 9, TextAnchor.MiddleCenter, Accent);
            var opts = new List<Kingdom> { null };
            opts.AddRange(G.Kingdoms().Where(Internet.Reach));
            foreach (Kingdom k in opts)
            {
                RectTransform row = UiKit.Box(c, "Opt", 22f, PW);
                bool on = k == null ? _global : !_global && _view == k;
                Sheet(row, on ? K.Hot : Screen);
                if (k != null) UiKit.Banner(row, k, new Vector2(-PW / 2f + 14f, 0f), 16f);
                else UiKit.Img(row, UiKit.Icon("ui/Icons/civ/btn/net"), new Vector2(16f, 16f), new Vector2(-PW / 2f + 14f, 0f));
                Ink9(row, k == null ? Txt.T("net_global") : G.Name(k), new Vector2(4f, 3f), new Vector2(130f, 11f), 9);
                if (k != null) Ink9(row, Txt.T("net_skin_device_" + NetMedia.Of(k)) + " · " + Txt.T("net_press_" + NetPress.Of(k)), new Vector2(4f, -6f), new Vector2(130f, 8f), 6, TextAnchor.MiddleLeft, InkSoft);
                Kingdom kk = k;
                PhoneKit.Btn(row, on ? "✔" : "○", null, () =>
                {
                    _global = kk == null;
                    if (kk != null) _view = kk;
                    Render();
                }, new Vector2(20f, 16f), new Vector2(PW / 2f - 14f, 0f), K.Pill, K.Ink);
            }
            RectTransform b = UiKit.Box(c, "Babel", 24f, PW);
            Sheet(b, Screen);
            Ink9(b, Txt.T("net_babel"), new Vector2(-10f, 0f), new Vector2(150f, 12f), 9);
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
                if (!Internet.Reach(k)) continue;
                if (!_global && _view != null && k != _view) continue;
                foreach (City ct in G.Cities(k)) foreach (Actor a in ct.units) if (G.Ok(a) && (Internet.Followers(a) > 0 || NetHub.IsVerified(a))) people.Add(a);
            }
            foreach (Actor a in people.OrderByDescending(x => Internet.Followers(x) + (NetHub.IsVerified(x) ? 100000 : 0)).Take(16))
            {
                RectTransform box = UiKit.Box(c, "Person", 28f, PW);
                Sheet(box, Screen);
                UiKit.Avatar(box, a, new Vector2(-PW / 2f + 14f, 0f), 0.45f);
                int f = Internet.Followers(a);
                bool cancelled = Store.Str(a.data, Internet.KCancel) == "cancelled";
                string role = NetHub.RoleOf(a);
                Ink9(box, a.getName() + (cancelled ? " <color=#D03A2A>✖</color>" : "") + (NetHub.IsVerified(a) ? " <color=#2A6AD8>✔</color>" : ""), new Vector2(-10f, 6f), new Vector2(120f, 11f), 9);
                Ink9(box, (role.Length > 0 && Txt.Has("net_role_" + role) ? Txt.T("net_role_" + role) + " · " : Names.Handle(a) + " · ") + Internet.TierLabel(f), new Vector2(-10f, -6f), new Vector2(120f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
                Ink9(box, G.Short(f), new Vector2(PW / 2f - 44f, 0f), new Vector2(40f, 12f), 9, TextAnchor.MiddleRight, Accent);
                Actor aa = a;
                PhoneKit.Btn(box, "@", null, () =>
                {
                    _profile = G.Id(aa);
                    _acct = "";
                    Render();
                }, new Vector2(18f, 18f), new Vector2(PW / 2f - 12f, 0f), K.Pill, K.Ink);
            }
            if (people.Count == 0) List(c, new List<NetPost>());
        }

        private void Profile(Transform c)
        {
            Actor a = G.ActorById(_profile);
            List<NetPost> posts = Internet.Feed.Where(p => p.AuthorId == _profile && p.Who == "person").Reverse().ToList();
            RectTransform box = UiKit.Box(c, "Profile", 58f, PW);
            Sheet(box, K.Head);
            string name = a != null ? a.getName() : posts.Count > 0 ? posts[0].Author : "?";
            if (a != null) UiKit.Avatar(box, a, new Vector2(-PW / 2f + 24f, 2f), 0.8f);
            else UiKit.Img(box, UiKit.Icon(Icon("memorial")), new Vector2(28f, 28f), new Vector2(-PW / 2f + 24f, 2f));
            int f = a != null ? Internet.Followers(a) : 0;
            bool ver = a != null && NetHub.IsVerified(a);
            Ink9(box, name + (a == null ? "  ✝" : "") + (ver ? " <color=#2A6AD8>✔</color>" : ""), new Vector2(20f, 16f), new Vector2(150f, 12f), 10, TextAnchor.MiddleLeft, Accent);
            string role = a != null ? NetHub.RoleOf(a) : "";
            string sub = role.Length > 0 && Txt.Has("net_role_" + role) ? Txt.T("net_role_" + role) : Internet.TierLabel(f);
            Ink9(box, (a != null ? Names.Handle(a) : posts.Count > 0 ? posts[0].Handle : "") + " · " + sub, new Vector2(20f, 3f), new Vector2(150f, 10f), 8, TextAnchor.MiddleLeft, InkSoft);
            Ink9(box, Txt.F("net_profile_stats", G.Short(f), posts.Count, G.Short(posts.Sum(p => p.Likes))), new Vector2(20f, -10f), new Vector2(150f, 10f), 8);
            if (a != null)
            {
                string bio = Roles.Of(a);
                Opinion o = Politics.Get(a);
                string ideo = o != null ? Txt.T("civ_p_" + o.Ideo) : "";
                Ink9(box, (bio.Length > 0 ? Txt.T("civ_role_" + bio) + " · " : "") + ideo, new Vector2(20f, -21f), new Vector2(150f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
                Actor aa = a;
                PhoneKit.Btn(box, "", UiKit.Icon("ui/Icons/civ/net/people"), () =>
                {
                    if (G.Ok(aa)) ActionLibrary.openUnitWindow(aa);
                }, new Vector2(20f, 18f), new Vector2(PW / 2f - 14f, 14f));
            }
            else if (posts.Count > 0)
            {
                Ink9(box, Txt.T("net_memorial"), new Vector2(20f, -21f), new Vector2(150f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
            }
            List(c, posts);
        }

        private void Account(Transform c)
        {
            int i = _acct.IndexOf('|');
            string who = i > 0 ? _acct.Substring(0, i) : "";
            string id = i > 0 ? _acct.Substring(i + 1) : _acct;
            List<NetPost> posts = Internet.Feed.Where(p => p.AuthorId == id && p.Who == who && !p.Dm).Reverse().ToList();
            NetPost first = posts.FirstOrDefault();
            RectTransform box = UiKit.Box(c, "Profile", 58f, PW);
            Sheet(box, K.Head);
            if (first != null) Avatar(box, first, new Vector2(-PW / 2f + 22f, 4f), 0.8f);
            string name = first != null ? first.Author : id;
            Ink9(box, name + (first != null ? Badge(first) : ""), new Vector2(20f, 16f), new Vector2(150f, 12f), 10, TextAnchor.MiddleLeft, K.Official);
            string role = first != null ? RoleLabel(first) : "";
            Ink9(box, (first != null ? first.Handle : "") + (role.Length > 0 ? " · " + role : ""), new Vector2(20f, 3f), new Vector2(150f, 10f), 8, TextAnchor.MiddleLeft, InkSoft);
            Ink9(box, Txt.F("net_account_stats", posts.Count, G.Short(posts.Sum(p => p.Likes)), G.Short(posts.Sum(p => p.Shares))), new Vector2(20f, -10f), new Vector2(150f, 10f), 8);
            Kingdom k = first != null ? G.KingdomById(first.Kingdom) : null;
            if (k != null && (who == "kingdom" || who == "press")) Ink9(box, Txt.T("net_press_" + NetPress.Of(k)) + " · " + Txt.T("civ_p_" + Policies.Id(k, "gov")), new Vector2(20f, -21f), new Vector2(150f, 10f), 7, TextAnchor.MiddleLeft, InkSoft);
            List(c, posts);
        }
    }
}
