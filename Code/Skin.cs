using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class Skin
    {
        private enum Mode { Frame, Panel, Primary }

        private static readonly Dictionary<string, Mode> Map = new Dictionary<string, Mode>
        {
            { "windowBig", Mode.Frame }, { "windowBigNoTitle", Mode.Frame }, { "background_tab_content", Mode.Frame }, { "background_tab_content_2", Mode.Frame },
            { "background_tab_content_small", Mode.Frame }, { "ScrollBar_BG", Mode.Frame }, { "windowNamePlate", Mode.Frame }, { "tooltip_rounded_4", Mode.Frame },
            { "tooltipTopNormal", Mode.Frame }, { "background_sidebar", Mode.Frame }, { "close_background_0", Mode.Frame }, { "close_background_1", Mode.Frame },
            { "windowInnerSliced", Mode.Panel }, { "windowInnerSliced12", Mode.Panel }, { "windowInnerSliced14", Mode.Panel }, { "windowInnerSlicedRound", Mode.Panel },
            { "windowInnerSlicedSmall", Mode.Panel }, { "windowInnerSlicedTopRound", Mode.Panel }, { "windowBar", Mode.Panel }, { "special_buttonGray", Mode.Panel },
            { "special_buttonGrayLight", Mode.Panel }, { "button", Mode.Panel }, { "button2", Mode.Panel }, { "buttonMedium", Mode.Panel }, { "buttonLong", Mode.Panel },
            { "tab_button", Mode.Panel }, { "ScrollBar_Handle", Mode.Panel }, { "backgroundTabButton", Mode.Panel },
            { "special_buttonRed", Mode.Primary }, { "buttonSelected", Mode.Primary }, { "buttonSelected_2", Mode.Primary }, { "tab_button_selected", Mode.Primary }
        };

        private static readonly Color32[] Accents =
        {
            new Color32(176, 166, 140, 255),
            new Color32(214, 146, 72, 255),
            new Color32(150, 178, 204, 255),
            new Color32(80, 226, 255, 255),
            new Color32(232, 184, 80, 255)
        };

        private static readonly Color32 Base = new Color32(36, 39, 48, 255);
        private static readonly Color32 PanelBase = new Color32(24, 26, 33, 255);
        private static readonly Color32 Line = new Color32(14, 15, 19, 255);
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Sprite, string> _orig = new Dictionary<Sprite, string>();
        private static readonly Dictionary<string, Sprite> _source = new Dictionary<string, Sprite>();
        private static readonly List<GameObject> _roots = new List<GameObject>();
        private static int _band = -1;
        private static float _next;

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.CivSkin; }
        }

        public static int Band
        {
            get
            {
                if (CivMod.C != null && !CivMod.C.SkinEra) return 4;
                Kingdom k = Player.Active && G.Ok(Player.K) ? Player.K : SelectedMetas.selected_kingdom;
                if (!G.Ok(k)) return 4;
                int era = Math.Max(0, Research.Era(k));
                return era <= 2 ? 0 : era <= 9 ? 1 : era <= 14 ? 2 : 3;
            }
        }

        public static Color Accent
        {
            get { return Accents[Math.Max(0, _band < 0 ? Band : _band)]; }
        }

        public static void Tick()
        {
            if (!On || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            int b = Band;
            if (b == _band) return;
            _band = b;
            for (int i = _roots.Count - 1; i >= 0; i--)
            {
                if (_roots[i] == null) _roots.RemoveAt(i);
                else Apply(_roots[i], false);
            }
        }

        public static Sprite Of(Sprite s)
        {
            if (!On || s == null) return s;
            string name;
            if (!_orig.TryGetValue(s, out name)) name = s.name;
            if (name == "iconClose") return Cross(s);
            Mode m;
            if (!Map.TryGetValue(name, out m)) return s;
            if (!_source.ContainsKey(name) && !_orig.ContainsKey(s)) _source[name] = s;
            Sprite src;
            if (!_source.TryGetValue(name, out src) || src == null) return s;
            if (_band < 0) _band = Band;
            string key = name + "|" + _band;
            Sprite done;
            if (_cache.TryGetValue(key, out done) && done != null) return done;
            try
            {
                done = Make(src, m, Accents[_band]);
            }
            catch (Exception e)
            {
                CivMod.Report("skin-" + name, e);
                done = null;
            }
            if (done == null) return s;
            done.name = name;
            _cache[key] = done;
            _orig[done] = name;
            return done;
        }

        private static Sprite Cross(Sprite src)
        {
            if (_band < 0) _band = Band;
            string key = "iconClose|" + _band;
            Sprite done;
            if (_cache.TryGetValue(key, out done) && done != null) return done;
            const int n = 28;
            var px = new Color32[n * n];
            Color32 acc = Mul(Accents[_band], 1.1f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float a = Math.Abs(x - y), b = Math.Abs(x + y - (n - 1));
                    bool inside = x >= 6 && x <= n - 7 && y >= 6 && y <= n - 7;
                    if (inside && (a <= 1.5f || b <= 1.5f)) px[y * n + x] = acc;
                    else if (x >= 5 && x <= n - 6 && y >= 5 && y <= n - 6 && (a <= 2.5f || b <= 2.5f)) px[y * n + x] = Line;
                }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels32(px);
            tex.Apply(false);
            done = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), src != null ? src.pixelsPerUnit : 1f);
            done.name = "iconClose";
            _cache[key] = done;
            _orig[done] = "iconClose";
            return done;
        }

        public static void Apply(GameObject root, bool track = true)
        {
            if (!On || root == null) return;
            if (track && !_roots.Contains(root)) _roots.Add(root);
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                if (img == null || img.sprite == null) continue;
                Sprite n = Of(img.sprite);
                if (n != img.sprite) img.sprite = n;
            }
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
            {
                if (t == null || t.name != "Title") continue;
                t.color = Accent;
            }
        }

        private static Color32[] Read(Sprite s, out int w, out int h)
        {
            Rect r = s.textureRect;
            w = Mathf.RoundToInt(r.width);
            h = Mathf.RoundToInt(r.height);
            Texture2D tex = s.texture;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            copy.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] px = copy.GetPixels32();
            UnityEngine.Object.Destroy(copy);
            return px;
        }

        private static float Lum(Color32 c)
        {
            return (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
        }

        private static Color32 Mul(Color32 c, float f)
        {
            return new Color32((byte)Mathf.Clamp(c.r * f, 0f, 255f), (byte)Mathf.Clamp(c.g * f, 0f, 255f), (byte)Mathf.Clamp(c.b * f, 0f, 255f), 255);
        }

        private static Color32 Mix(Color32 a, Color32 b, float t)
        {
            return new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);
        }

        private static Sprite Make(Sprite src, Mode mode, Color32 acc)
        {
            int w, h;
            Color32[] p = Read(src, out w, out h);
            var lums = new List<float>();
            foreach (Color32 c in p) if (c.a > 0) lums.Add(Lum(c));
            if (lums.Count == 0) return null;
            lums.Sort();
            float med = Math.Max(0.05f, lums[lums.Count / 2]);
            var o = new Color32[p.Length];
            Func<int, int, bool> solid = (x, y) => x >= 0 && y >= 0 && x < w && y < h && p[y * w + x].a > 128;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c = p[y * w + x];
                    if (c.a == 0) continue;
                    int d = 9;
                    for (int r = 1; r <= 3 && d == 9; r++)
                    {
                        for (int dy = -r; dy <= r && d == 9; dy++)
                        {
                            for (int dx = -r; dx <= r; dx++)
                            {
                                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                                if (!solid(x + dx, y + dy))
                                {
                                    d = r;
                                    break;
                                }
                            }
                        }
                    }
                    float rel = Lum(c) / med;
                    bool red = c.r > c.g + 40 && c.r > c.b + 40;
                    Color32 col;
                    if (mode == Mode.Primary)
                    {
                        col = Mul(Mul(acc, 0.62f), Mathf.Clamp(rel, 0.6f, 1.5f));
                        if (d == 1) col = Mul(acc, 0.35f);
                        else if (d == 2) col = Mix(acc, new Color32(255, 255, 255, 255), 0.35f);
                    }
                    else
                    {
                        Color32 b = mode == Mode.Frame ? Base : PanelBase;
                        col = Mul(b, Mathf.Clamp(Mathf.Pow(rel, 1.2f), 0.55f, 1.7f));
                        if (red) col = Mul(acc, 0.55f);
                        if (d == 1) col = Line;
                        else if (d == 2) col = Mul(acc, 0.8f + 0.2f * Math.Min(1.2f, rel));
                        else if (d == 3 && mode == Mode.Frame) col = Mix(col, acc, 0.18f);
                    }
                    col.a = c.a;
                    o[y * w + x] = col;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(o);
            tex.Apply(false);
            Vector2 pivot = new Vector2(src.pivot.x / Math.Max(1f, src.rect.width), src.pivot.y / Math.Max(1f, src.rect.height));
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect, src.border);
        }

        public static Sprite Interior(int w, int h)
        {
            if (_band < 0) _band = Band;
            string key = "interior|" + _band + "|" + w + "x" + h;
            Sprite s;
            if (_cache.TryGetValue(key, out s) && s != null) return s;
            Color32 acc = Accents[_band];
            var px = new Color32[w * h];
            var top = new Color32(48, 52, 63, 255);
            var bot = new Color32(24, 26, 33, 255);
            var r = new System.Random(3);
            for (int y = 0; y < h; y++)
            {
                float t = 1f - y / (float)Math.Max(1, h - 1);
                for (int x = 0; x < w; x++)
                {
                    float vx = Math.Abs(x / (float)Math.Max(1, w - 1) - 0.5f) * 2f, vy = Math.Abs(y / (float)Math.Max(1, h - 1) - 0.5f) * 2f;
                    Color32 c = Mul(Mix(top, bot, t), 1f - 0.22f * Mathf.Pow(Math.Max(vx, vy), 3f));
                    switch (_band)
                    {
                        case 0:
                            if (r.NextDouble() < 0.07) c = Mul(c, r.NextDouble() < 0.5 ? 0.86f : 1.12f);
                            break;
                        case 1:
                            if ((x + y) % 9 == 0) c = Mix(c, acc, 0.06f);
                            break;
                        case 2:
                            c = Mul(c, 1f + 0.05f * Mathf.Sin(y * 1.7f) + 0.03f * Mathf.Sin(y * 0.37f));
                            break;
                        case 3:
                            if (x % 12 == 0 || y % 12 == 0) c = Mix(c, acc, 0.12f);
                            if (y % 2 == 0) c = Mul(c, 0.94f);
                            break;
                    }
                    int fromTop = h - 1 - y;
                    if (fromTop < 3) c = Mix(c, acc, fromTop == 0 ? 0.35f : fromTop == 1 ? 0.18f : 0.08f);
                    px[y * w + x] = c;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            _cache[key] = s;
            return s;
        }

        public static void Backdrop(ScrollWindow win)
        {
            if (!On || win == null) return;
            Transform bg = win.transform.Find("Background");
            if (bg == null) return;
            Transform old = bg.Find("CivSkinBg");
            RectTransform br = bg as RectTransform;
            if (br == null) return;
            int w = Math.Max(8, Mathf.RoundToInt(br.rect.width) - 24), h = Math.Max(8, Mathf.RoundToInt(br.rect.height) - 52);
            Image img;
            if (old != null) img = old.GetComponent<Image>();
            else
            {
                var go = new GameObject("CivSkinBg", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(bg, false);
                go.transform.SetAsFirstSibling();
                img = go.GetComponent<Image>();
                img.raycastTarget = false;
            }
            RectTransform rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 12f);
            rt.offsetMax = new Vector2(-12f, -40f);
            img.sprite = Interior(Math.Min(512, w), Math.Min(512, h));
            img.type = Image.Type.Simple;
        }

        private static bool Ours(ScrollWindow w)
        {
            if (w == null) return false;
            if (w.screen_id != null && w.screen_id.StartsWith("civ_")) return true;
            foreach (MonoBehaviour m in w.GetComponents<MonoBehaviour>()) if (m != null && m.GetType().Namespace == "Civilizations") return true;
            return false;
        }

        [HarmonyPatch(typeof(ScrollWindow), nameof(ScrollWindow.show))]
        private static class Shown
        {
            private static void Postfix(ScrollWindow __instance)
            {
                if (!On || !Ours(__instance)) return;
                CivMod.Safe("skin-window", () =>
                {
                    Apply(__instance.gameObject);
                    Backdrop(__instance);
                });
            }
        }
    }
}
