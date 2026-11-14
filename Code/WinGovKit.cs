using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal class Sheet
    {
        public RectTransform R;
        public float W, H;

        public Vector2 At(float x, float y, float w, float h)
        {
            return new Vector2(-W / 2f + x + w / 2f, H / 2f - y - h / 2f);
        }

        public Text Text(float x, float y, float w, float h, string text, int size = 8, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            return UiKit.Text(R, text, At(x, y, w, h), new Vector2(w, h), size, anchor, color);
        }

        public Image Rect(float x, float y, float w, float h, Color color)
        {
            Image img = UiKit.Img(R, GovKit.White, new Vector2(w, h), At(x, y, w, h), color);
            img.preserveAspect = false;
            return img;
        }

        public Image Icon(float x, float y, float size, string icon, Color? color = null)
        {
            return UiKit.Img(R, UiKit.Icon(GovKit.Pol(icon)), new Vector2(size, size), At(x, y, size, size), color);
        }

        public Image Panel(float x, float y, float w, float h, float alpha = 0.85f)
        {
            Image img = UiKit.Img(R, UiKit.S("ui/special/windowInnerSliced"), new Vector2(w, h), At(x, y, w, h), new Color(1f, 1f, 1f, alpha));
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.enabled = true;
            return img;
        }

        public SimpleButton Btn(float x, float y, float w, float h, string text, Action act, string tipT = null, string tipD = null, bool primary = false)
        {
            SimpleButton b = UiKit.Btn(R, text, null, () => CivMod.Safe("gov-ui", () => act()), new Vector2(w, h), At(x, y, w, h), tipT, tipD, primary ? (Color?)UiKit.Gold : null);
            if (b.Text != null)
            {
                b.Text.fontSize = Math.Min(b.Text.fontSize, 8);
                b.Text.resizeTextForBestFit = true;
                b.Text.resizeTextMinSize = 5;
                b.Text.resizeTextMaxSize = 8;
            }
            return b;
        }

        public SimpleButton IconBtn(float x, float y, float size, string icon, Action act, string tipT, string tipD, bool on = true)
        {
            return UiKit.IconBtn(R, UiKit.Icon(GovKit.Pol(icon)), () => CivMod.Safe("gov-ui", () => act()), size, At(x, y, size, size), tipT, tipD, on);
        }

        public SimpleButton Hit(float x, float y, float w, float h, Action act, string tipT, string tipD)
        {
            SimpleButton b = UiKit.Btn(R, "", null, () =>
            {
                if (act != null) CivMod.Safe("gov-ui", () => act());
            }, new Vector2(w, h), At(x, y, w, h), tipT, tipD, new Color(1f, 1f, 1f, 0.04f));
            return b;
        }

        public void Header(float x, float y, float w, string title, string icon)
        {
            Image bg = UiKit.Img(R, UiKit.S("ui/special/windowBar") ?? UiKit.S("ui/special/windowInnerSliced"), new Vector2(w, 16f), At(x, y, w, 16f), new Color(0.85f, 0.85f, 0.9f, 0.95f));
            bg.type = Image.Type.Sliced;
            bg.preserveAspect = false;
            bg.enabled = true;
            if (icon != null)
            {
                Icon(x + 3f, y + 1f, 14f, icon);
                Icon(x + w - 17f, y + 1f, 14f, icon);
            }
            Text(x + 20f, y, w - 40f, 16f, title, 10, TextAnchor.MiddleCenter, UiKit.Gold);
        }

        public void Bar(float x, float y, float w, float h, float v, Color color, string label = null)
        {
            UiKit.Bar(R, v, color, new Vector2(w, h), At(x, y, w, h), label);
        }

        public void Locked(float x, float y, float w, float h, int era, string what = null)
        {
            Panel(x, y, w, h, 0.5f);
            Icon(x + w / 2f - 7f, y + h / 2f - 16f, 14f, "lock", UiKit.Dim);
            Text(x + 4f, y + h / 2f, w - 8f, Math.Min(26f, h / 2f), (what != null ? what + "\n" : "") + GovEra.Note(era), 7, TextAnchor.UpperCenter, UiKit.Dim);
        }

        public Sheet Sub(float x, float y, float w, float h)
        {
            var go = new GameObject("Sub", typeof(RectTransform));
            go.transform.SetParent(R, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(w, h);
            r.localPosition = At(x, y, w, h);
            return new Sheet { R = r, W = w, H = h };
        }
    }

    internal static class GovKit
    {
        private static Sprite _white;

        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(4, 4);
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                _white = Sprite.Create(tex, new UnityEngine.Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                return _white;
            }
        }

        public static string Pol(string name)
        {
            return name.StartsWith("ui/") ? name : "ui/Icons/civ/pol/" + name;
        }

        public static bool CanAct(Kingdom k)
        {
            return G.Ok(k) && (Player.Is(k) || !Player.Active);
        }

        public static Color Dark(Color c, float f)
        {
            return new Color(c.r * f, c.g * f, c.b * f, 1f);
        }

        public static readonly Color Water = new Color(UiKit.Cyan.r * 0.25f, UiKit.Cyan.g * 0.3f, UiKit.Cyan.b * 0.45f, 1f);
        public static readonly Color Ground = new Color(UiKit.Dim.r * 0.55f, UiKit.Dim.g * 0.55f, UiKit.Dim.b * 0.5f, 1f);

        public static void Hemicycle(Sheet s, float x, float y, float w, float h, List<KeyValuePair<Color, int>> blocks, int total)
        {
            if (total <= 0) return;
            int per = Math.Max(1, (int)Math.Ceiling(total / 240f));
            int n = (int)Math.Ceiling(total / (float)per);
            int rows = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n / 2.6f)), 1, 10);
            float rOuter = Math.Min(w / 2f - 4f, h - 6f), rInner = rOuter * 0.42f;
            var radii = new List<float>();
            for (int i = 0; i < rows; i++) radii.Add(rows == 1 ? rOuter : rInner + (rOuter - rInner) * i / (rows - 1));
            float sum = radii.Sum();
            var slots = new List<KeyValuePair<float, Vector2>>();
            int placed = 0;
            for (int i = 0; i < rows; i++)
            {
                int cnt = i == rows - 1 ? n - placed : Mathf.RoundToInt(n * radii[i] / sum);
                cnt = Math.Max(1, cnt);
                placed += cnt;
                for (int j = 0; j < cnt; j++)
                {
                    float a = Mathf.PI * (cnt == 1 ? 0.5f : 1f - j / (float)(cnt - 1));
                    slots.Add(new KeyValuePair<float, Vector2>(a, new Vector2(Mathf.Cos(a) * radii[i], Mathf.Sin(a) * radii[i])));
                }
            }
            slots = slots.OrderByDescending(p => p.Key).ToList();
            float sq = Mathf.Clamp((rOuter - rInner) / rows * 0.75f, 2f, 9f);
            var colors = new List<Color>();
            foreach (var b in blocks) for (int i = 0; i < (int)Math.Round(b.Value / (float)per); i++) colors.Add(b.Key);
            float cx = x + w / 2f, cy = y + h - 4f;
            for (int i = 0; i < slots.Count; i++)
            {
                Color c = i < colors.Count ? colors[i] : UiKit.Dim;
                Vector2 p = slots[i].Value;
                s.Rect(cx + p.x - sq / 2f, cy - p.y - sq / 2f, sq, sq, c);
            }
        }

        public static void Sankey(Sheet s, float x0, float y0, float x1, float y1, float h0, float h1, Color c)
        {
            if (h0 < 0.5f && h1 < 0.5f) return;
            int steps = Mathf.Clamp((int)((x1 - x0) / 3f), 6, 40);
            float dx = (x1 - x0) / steps;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float e = t * t * (3f - 2f * t);
                float yc = y0 + (y1 - y0) * e;
                float hh = h0 + (h1 - h0) * e;
                s.Rect(x0 + i * dx, yc, dx + 0.6f, Math.Max(1f, hh), c);
            }
        }

        public static Kingdom Pick(Kingdom k)
        {
            if (G.Ok(k)) return k;
            return SelectedMetas.selected_kingdom;
        }

        public static readonly string[] TabIds = { "parliament", "elections", "treasury", "vassals", "mandate" };

        public static int TabEra(string id)
        {
            switch (id)
            {
                case "parliament": return GovEra.Council;
                case "elections": return GovEra.Nobles;
                case "treasury": return GovEra.Ledger;
                case "vassals": return GovEra.Tributaries;
                case "mandate": return GovEra.Mandate;
            }
            return 0;
        }

        public static bool TabOn(string id)
        {
            switch (id)
            {
                case "parliament": return Gov.On("civ_gov_parliament");
                case "elections": return true;
                case "treasury": return Gov.On("civ_gov_treasury");
                case "vassals": return Gov.On("civ_gov_vassals");
                case "mandate": return Gov.On("civ_gov_cycle");
            }
            return true;
        }

        public static bool TabShown(string id, Kingdom k)
        {
            return TabOn(id) && GovEra.Has(k, TabEra(id));
        }

        public static string TabTitle(string id, Kingdom k)
        {
            if (id == "parliament" && G.Ok(k)) return Gov.Council(k);
            return Txt.T("civ_gov_tab_" + id);
        }

        public static void Open(string id, Kingdom k)
        {
            switch (id)
            {
                case "parliament": WinParliament.Open(k); break;
                case "elections": WinElections.Open(k); break;
                case "treasury": WinTreasury.Open(k); break;
                case "vassals": WinVassals.Open(k); break;
                case "mandate": WinMandate.Open(k); break;
                case "civ": WinCivHub.Open(k); break;
            }
        }

        public static void Column(Transform background, string current, Kingdom k)
        {
            if (background == null) return;
            Transform old = background.Find("GovTabs");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            RectTransform bg = background as RectTransform;
            var go = new GameObject("GovTabs", typeof(RectTransform));
            go.transform.SetParent(background, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(24f, bg.rect.height);
            r.localPosition = new Vector3(bg.rect.xMin - 12f, 0f, 0f);
            var col = new Sheet { R = r, W = 24f, H = bg.rect.height };
            float y = 26f;
            Kingdom kk = k;
            foreach (string id in TabIds)
            {
                if (!TabShown(id, k)) continue;
                string tid = id;
                SimpleButton b = col.IconBtn(1f, y, 22f, "tab_" + id, () => Open(tid, kk), TabTitle(id, k), Txt.T("civ_gov_tab_" + id + "_d"));
                if (id == current) b.Background.color = UiKit.Gold;
                y += 24f;
            }
            y += 6f;
            col.IconBtn(1f, y, 22f, "tab_civ", () => Open("civ", kk), Txt.T("civ_gov_tab_civ"), Txt.T("civ_gov_tab_civ_d"));
            y += 24f;
            col.IconBtn(1f, y, 22f, "tab_kingdom", () =>
            {
                if (!G.Ok(kk)) return;
                SelectedMetas.selected_kingdom = kk;
                ScrollWindow.showWindow("kingdom");
            }, Txt.T("civ_gov_tab_kingdom"), Txt.T("civ_gov_tab_kingdom_d"));
            Skin.Apply(go, false);
        }

        public static void Domains(Sheet s, float x, float y, float w, float h, Kingdom k, bool families, City selected, Action<City> pick)
        {
            s.Rect(x, y, w, h, Water);
            var owner = new Dictionary<TileZone, City>();
            foreach (City c in G.Cities(k)) foreach (TileZone z in c.zones) owner[z] = c;
            if (owner.Count == 0 || World.world == null || World.world.zone_calculator == null) return;
            int x0 = owner.Keys.Min(z => z.x), x1 = owner.Keys.Max(z => z.x), y0 = owner.Keys.Min(z => z.y), y1 = owner.Keys.Max(z => z.y);
            float aspect = w / h;
            int zw = x1 - x0 + 5, zh = y1 - y0 + 5;
            if (zw < zh * aspect) zw = Mathf.CeilToInt(zh * aspect);
            else zh = Mathf.CeilToInt(zw / aspect);
            int ox = (x0 + x1) / 2 - zw / 2, oy = (y0 + y1) / 2 - zh / 2;
            const int P = 3;
            int tw = zw * P, th = zh * P;
            if (tw > 1024 || th > 1024) return;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[tw * th];
            var cells = new City[zw * zh];
            Color kc = UiKit.Soft;
            try
            {
                kc = k.getColor()._color_main;
            }
            catch (Exception)
            {
            }
            var colors = new Dictionary<City, Color>();
            foreach (City c in G.Cities(k))
            {
                long f = Fam.Holder(c, k);
                colors[c] = families && f != 0L ? Fam.Color(f) : Dark(kc, 0.8f);
            }
            ZoneCalculator zc = World.world.zone_calculator;
            for (int cy = 0; cy < zh; cy++)
            {
                for (int cx = 0; cx < zw; cx++)
                {
                    TileZone z = zc.getZone(ox + cx, oy + cy);
                    City city = null;
                    Color col;
                    if (z != null && owner.TryGetValue(z, out city)) col = colors[city];
                    else if (z == null) col = Water;
                    else if (z.city != null) col = Color.Lerp(Ground, Dark(UiKit.Soft, 0.5f), 0.4f);
                    else col = z.tiles_with_liquid > z.tiles_with_ground ? Water : Ground;
                    cells[cy * zw + cx] = city;
                    if (city != null && city == selected) col = Color.Lerp(col, Color.white, 0.35f);
                    for (int py = 0; py < P; py++)
                    {
                        for (int pxx = 0; pxx < P; pxx++)
                        {
                            Color cc = col;
                            if (city != null)
                            {
                                bool edge = false;
                                if (pxx == 0 && cx > 0 && cells[cy * zw + cx - 1] != city) edge = true;
                                if (py == 0 && cy > 0 && cells[(cy - 1) * zw + cx] != city) edge = true;
                                if (edge) cc = Dark(col, 0.45f);
                            }
                            px[(cy * P + py) * tw + cx * P + pxx] = cc;
                        }
                    }
                }
            }
            foreach (City c in G.Cities(k))
            {
                WorldTile t = G.Tile(c);
                if (t == null || t.zone == null) continue;
                int cx = t.zone.x - ox, cy = t.zone.y - oy;
                if (cx < 0 || cy < 0 || cx >= zw || cy >= zh) continue;
                int r = c == k.capital ? 2 : 1;
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int pxi = cx * P + 1 + dx, pyi = cy * P + 1 + dy;
                        if (pxi < 0 || pyi < 0 || pxi >= tw || pyi >= th) continue;
                        bool ring = Math.Abs(dx) == 2 || Math.Abs(dy) == 2;
                        px[pyi * tw + pxi] = ring ? UiKit.Gold : Color.white;
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new UnityEngine.Rect(0f, 0f, tw, th), new Vector2(0.5f, 0.5f), 1f);
            Image img = UiKit.Img(s.R, sp, new Vector2(w - 4f, h - 4f), s.At(x + 2f, y + 2f, w - 4f, h - 4f));
            img.preserveAspect = false;
            img.raycastTarget = true;
            Button btn = img.gameObject.AddComponent<Button>();
            RectTransform rt = img.rectTransform;
            btn.onClick.AddListener(() => CivMod.Safe("gov-map", () =>
            {
                Canvas cv = rt.GetComponentInParent<Canvas>();
                Camera cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, cam, out lp)) return;
                int gx = (int)((lp.x / rt.rect.width + 0.5f) * zw), gy = (int)((lp.y / rt.rect.height + 0.5f) * zh);
                if (gx < 0 || gy < 0 || gx >= zw || gy >= zh) return;
                City c = cells[gy * zw + gx];
                if (c != null && pick != null) pick(c);
            }));
        }
    }

    internal abstract class GovWindow<T> : AbstractWindow<T> where T : GovWindow<T>
    {
        protected static Kingdom K;
        protected Vector2 Size;
        private float _tick;
        private string _sig = "";

        protected abstract float Wd { get; }
        protected abstract float Ht { get; }
        protected abstract string Tab { get; }
        protected abstract void Draw(Sheet s, Kingdom k);

        protected virtual string Sig(Kingdom k)
        {
            return G.Year + "|" + G.Cities(k).Count;
        }

        protected static void Show(string id, Kingdom k, Action create)
        {
            K = GovKit.Pick(k);
            ScrollWindow w;
            if (ScrollWindow._all_windows == null || !ScrollWindow._all_windows.TryGetValue(id, out w) || w == null) create();
            UiKit.Open(id);
            if (Instance != null && Instance.gameObject.activeInHierarchy) Instance.Render();
        }

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
            Size = WinSize.Set(BackgroundTransform, ContentTransform, Wd, Ht);
        }

        public override void OnNormalEnable()
        {
            Render();
        }

        private void Update()
        {
            if (!G.Loaded || Time.unscaledTime < _tick) return;
            _tick = Time.unscaledTime + 1f;
            if (!G.Ok(K)) return;
            string s = "";
            CivMod.Safe("gov-win-sig", () => s = Sig(K));
            if (s != _sig) Render();
        }

        public void Render()
        {
            Transform c = ContentTransform;
            if (c == null) return;
            UiKit.Clear(c);
            if (!G.Loaded) return;
            K = GovKit.Pick(K);
            if (!G.Ok(K) || !K.isCiv())
            {
                UiKit.Label(c, Txt.T("state_none"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            Kingdom k = K;
            CivMod.Safe("gov-win-sig", () => _sig = Sig(k));
            CivMod.Safe("gov-win-tabs", () => GovKit.Column(BackgroundTransform, Tab, k));
            float w = Size.x - 18f, h = Size.y - 24f;
            RectTransform box = UiKit.Box(c, "GovSheet", h, w);
            var sheet = new Sheet { R = box, W = w, H = h };
            CivMod.Safe("gov-win-" + Tab, () => Draw(sheet, k));
        }

        protected static void Redraw()
        {
            if (Instance != null && Instance.gameObject.activeInHierarchy) Instance.Render();
        }

        protected static void Done(Kingdom k)
        {
            if (G.Ok(k))
            {
                Bonus.Dirty(k);
                Profile.Invalidate(k);
                Fam.Invalidate(k);
            }
            Redraw();
        }

        protected static void Title(Sheet s, Kingdom k, string sub, string tip)
        {
            UiKit.Banner(s.R, k, s.At(4f, 2f, 22f, 22f), 22f);
            s.Text(30f, 0f, s.W - 60f, 14f, G.Name(k), 11, TextAnchor.MiddleCenter, UiKit.Gold);
            s.Text(30f, 13f, s.W - 60f, 11f, sub, 8, TextAnchor.MiddleCenter, UiKit.Soft);
            if (tip != null) s.Hit(s.W / 2f - 80f, 12f, 160f, 12f, null, sub, tip);
        }
    }
}
