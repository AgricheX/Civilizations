using DG.Tweening;
using System;
using System.Collections.Generic;
using NeoModLoader.General;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class UiKit
    {
        public const float W = 214f;
        public static readonly Color Gold = new Color(1f, 0.82f, 0.36f);
        public static readonly Color Soft = new Color(0.78f, 0.82f, 0.9f);
        public static readonly Color Dim = new Color(0.55f, 0.58f, 0.64f);
        public static readonly Color Good = new Color(0.55f, 0.95f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);
        public static readonly Color Cyan = new Color(0.55f, 0.88f, 1f);
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite S(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Sprite s;
            if (_cache.TryGetValue(path, out s) && s != null) return s;
            s = SpriteTextureLoader.getSprite(path);
            if (s != null) _cache[path] = s;
            return s;
        }

        public static Sprite Icon(string path, string fallback = "ui/Icons/civ/unknown")
        {
            return S(path) ?? S(fallback);
        }

        public static void Setup(Transform content)
        {
            VerticalLayoutGroup layout = content.gameObject.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            OT.InitializeNoActionVerticalLayoutGroup(layout);
            layout.spacing = 3;
            layout.padding = new RectOffset(0, 0, 6, 8);
            ContentSizeFitter fitter = content.gameObject.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject go = t.GetChild(i).gameObject;
                foreach (Transform x in go.GetComponentsInChildren<Transform>(true)) x.DOKill();
                go.SetActive(false);
                UnityEngine.Object.Destroy(go);
            }
        }

        public static RectTransform Box(Transform parent, string name, float h, float w = W)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(w, h);
            r.localScale = Vector3.one;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.preferredWidth = w;
            return r;
        }

        public static Image Panel(RectTransform box, float alpha = 1f)
        {
            Image img = box.gameObject.GetComponent<Image>() ?? box.gameObject.AddComponent<Image>();
            img.sprite = S("ui/special/windowInnerSliced");
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
            return img;
        }

        public static Text Text(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize = 9, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.sizeDelta = size;
            r.localPosition = new Vector3(pos.x, pos.y, 0f);
            Text t = go.GetComponent<Text>();
            OT.InitializeCommonText(t);
            t.text = text ?? "";
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Math.Max(5, fontSize - 3);
            t.resizeTextMaxSize = fontSize;
            return t;
        }

        public static Text Label(Transform parent, string text, float h = 14f, int fontSize = 9, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            RectTransform box = Box(parent, "Label", h);
            return Text(box, text, Vector2.zero, new Vector2(W - 6f, h), fontSize, anchor, color);
        }

        public static Text Paragraph(Transform parent, string text, int fontSize = 8, Color? color = null)
        {
            int lines = 1 + (text ?? "").Length / 46;
            foreach (char c in text ?? "") if (c == '\n') lines++;
            float h = Math.Max(12f, lines * (fontSize + 2.5f));
            Text t = Label(parent, text, h, fontSize, TextAnchor.UpperLeft, color);
            t.resizeTextForBestFit = false;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static void Header(Transform parent, string text, Color? color = null, string icon = null)
        {
            RectTransform box = Box(parent, "Header", 18f);
            Image bg = box.gameObject.AddComponent<Image>();
            bg.sprite = S("ui/special/windowBar") ?? S("ui/special/windowInnerSliced");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.8f, 0.8f, 0.85f, 0.9f);
            bg.raycastTarget = false;
            if (icon != null)
            {
                Img(box, Icon(icon), new Vector2(14f, 14f), new Vector2(-W / 2f + 12f, 0f));
                Img(box, Icon(icon), new Vector2(14f, 14f), new Vector2(W / 2f - 12f, 0f));
            }
            Text(box, text, Vector2.zero, new Vector2(W - 44f, 16f), 10, TextAnchor.MiddleCenter, color ?? Gold);
        }

        public static void Gap(Transform parent, float h = 4f)
        {
            Box(parent, "Gap", h);
        }

        public static Image Img(Transform parent, Sprite s, Vector2 size, Vector2 pos, Color? color = null)
        {
            var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.sizeDelta = size;
            r.localPosition = new Vector3(pos.x, pos.y, 0f);
            Image img = go.GetComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.color = color ?? Color.white;
            img.raycastTarget = false;
            if (s == null) img.enabled = false;
            return img;
        }

        public static string Dyn(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string key = "civ_dyn_" + (text.GetHashCode() & 0x7fffffff).ToString("x");
            try
            {
                LM.AddToCurrentLocale(key, text);
            }
            catch (Exception)
            {
            }
            return key;
        }

        public static SimpleButton Btn(Transform parent, string text, Sprite icon, UnityAction act, Vector2 size, Vector2 pos, string tipTitle = null, string tipDesc = null, Color? bg = null)
        {
            SimpleButton b = SimpleButton.Instantiate(parent, false, "Btn");
            TooltipData td = null;
            string tip = null;
            if (!string.IsNullOrEmpty(tipTitle))
            {
                tip = "tip";
                td = new TooltipData { tip_name = Dyn(tipTitle), tip_description = Dyn(tipDesc ?? "") };
            }
            b.Setup(act, icon, text, size, tip, td);
            b.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            b.transform.localScale = Vector3.one;
            if (bg.HasValue) b.Background.color = bg.Value;
            if (b.Text != null) b.Text.supportRichText = true;
            return b;
        }

        public static SimpleButton IconBtn(Transform parent, Sprite icon, UnityAction act, float size, Vector2 pos, string tipTitle, string tipDesc, bool on = true, bool frame = true)
        {
            SimpleButton b = Btn(parent, null, icon, act, new Vector2(size, size), pos, tipTitle, tipDesc);
            if (!frame) b.Background.color = new Color(1f, 1f, 1f, 0f);
            else b.Background.sprite = S("ui/special/button2") ?? b.Background.sprite;
            b.Icon.color = on ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.85f);
            b.Icon.preserveAspect = true;
            return b;
        }

        public static void Bar(Transform parent, float v, Color color, Vector2 size, Vector2 pos, string label = null)
        {
            v = Mathf.Clamp01(v);
            Image back = Img(parent, S("ui/special/windowInnerSliced"), size, pos, new Color(0.15f, 0.15f, 0.2f, 1f));
            back.type = Image.Type.Sliced;
            back.preserveAspect = false;
            back.enabled = true;
            if (v > 0f)
            {
                float w = Math.Max(2f, (size.x - 2f) * v);
                Image fill = Img(parent, S("ui/special/windowInnerSliced"), new Vector2(w, size.y - 2f), new Vector2(pos.x - (size.x - 2f) / 2f + w / 2f, pos.y), color);
                fill.type = Image.Type.Sliced;
                fill.preserveAspect = false;
                fill.enabled = true;
            }
            if (!string.IsNullOrEmpty(label)) Text(parent, label, pos, size, 8, TextAnchor.MiddleCenter);
        }

        public static void Stat(Transform parent, string icon, string name, string value, Color? valueColor = null)
        {
            RectTransform box = Box(parent, "Stat", 15f);
            if (icon != null) Img(box, Icon(icon), new Vector2(12f, 12f), new Vector2(-W / 2f + 9f, 0f));
            Text(box, name, new Vector2(-W / 2f + 18f + 60f, 0f), new Vector2(120f, 14f), 9, TextAnchor.MiddleLeft, Soft);
            Text(box, value, new Vector2(W / 2f - 50f, 0f), new Vector2(96f, 14f), 9, TextAnchor.MiddleRight, valueColor ?? Color.white);
        }

        public static void Tabs(Transform parent, string[] keys, string[] icons, int current, Action<int> pick)
        {
            RectTransform box = Box(parent, "Tabs", 24f);
            int n = keys.Length;
            float bw = Math.Min(40f, (W - 4f) / n - 2f);
            float start = -((bw + 2f) * n - 2f) / 2f + bw / 2f;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                Sprite s = icons != null ? Icon(icons[i]) : null;
                SimpleButton b;
                if (s != null)
                {
                    b = IconBtn(box, s, () => pick(idx), 22f, new Vector2(start + i * (bw + 2f), 0f), Txt.T(keys[i]), null, true, true);
                    b.SetSize(new Vector2(bw, 22f));
                }
                else
                {
                    b = Btn(box, Txt.T(keys[i]), null, () => pick(idx), new Vector2(bw, 20f), new Vector2(start + i * (bw + 2f), 0f));
                }
                b.Background.color = i == current ? new Color(1f, 0.85f, 0.45f) : new Color(0.75f, 0.75f, 0.8f);
            }
        }

        public static void Pager(Transform parent, int page, int pages, Action<int> go)
        {
            if (pages <= 1) return;
            RectTransform box = Box(parent, "Pager", 22f);
            Btn(box, "<", null, () => go(Math.Max(0, page - 1)), new Vector2(26f, 20f), new Vector2(-50f, 0f));
            Text(box, (page + 1) + " / " + pages, Vector2.zero, new Vector2(60f, 18f), 9, TextAnchor.MiddleCenter, Soft);
            Btn(box, ">", null, () => go(Math.Min(pages - 1, page + 1)), new Vector2(26f, 20f), new Vector2(50f, 0f));
        }

        public static RectTransform Selector(Transform parent, string label, Sprite icon, Action prev, Action next, Action click = null)
        {
            RectTransform box = Box(parent, "Selector", 24f);
            Btn(box, "<", null, () => prev(), new Vector2(22f, 22f), new Vector2(-W / 2f + 12f, 0f));
            Btn(box, ">", null, () => next(), new Vector2(22f, 22f), new Vector2(W / 2f - 12f, 0f));
            if (click != null)
            {
                SimpleButton mid = Btn(box, label, null, () => click(), new Vector2(W - 54f, 22f), Vector2.zero);
                mid.Background.sprite = S("ui/special/windowInnerSliced") ?? mid.Background.sprite;
            }
            else
            {
                Panel(Box(box, "Mid", 22f, W - 54f)).raycastTarget = false;
                Text(box, label, new Vector2(icon != null ? 8f : 0f, 0f), new Vector2(W - 80f, 20f), 10, TextAnchor.MiddleCenter, Gold);
            }
            if (icon != null) Img(box, icon, new Vector2(16f, 16f), new Vector2(-W / 2f + 36f, 0f));
            return box;
        }

        public static UiUnitAvatarElement Avatar(Transform parent, Actor a, Vector2 pos, float scale)
        {
            if (!G.Ok(a)) return null;
            try
            {
                UiUnitAvatarElement prefab = Resources.Load<UiUnitAvatarElement>("ui/UnitAvatarElement");
                if (prefab == null) return null;
                UiUnitAvatarElement el = UnityEngine.Object.Instantiate(prefab, parent);
                el.show_banner_kingdom = false;
                el.show_banner_clan = false;
                if (el.kingdomBanner != null) el.kingdomBanner.gameObject.SetActive(false);
                if (el.clanBanner != null) el.clanBanner.gameObject.SetActive(false);
                if (el.unit_type_bg != null) el.unit_type_bg.gameObject.SetActive(false);
                el.transform.localScale = Vector3.one * scale;
                el.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                LayoutElement le = el.GetComponent<LayoutElement>() ?? el.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                el.show(a);
                if (el.GetComponent<Graphic>() == null)
                {
                    Image hit = el.gameObject.AddComponent<Image>();
                    hit.color = new Color(1f, 1f, 1f, 0f);
                }
                Button btn = el.GetComponent<Button>() ?? el.gameObject.AddComponent<Button>();
                Actor target = a;
                btn.onClick.AddListener(() =>
                {
                    if (G.Ok(target)) ActionLibrary.openUnitWindow(target);
                });
                return el;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Banner(Transform parent, Kingdom k, Vector2 pos, float size)
        {
            if (!G.Ok(k)) return;
            try
            {
                KingdomBanner prefab = Resources.Load<KingdomBanner>("ui/PrefabBannerKingdom");
                if (prefab == null) return;
                KingdomBanner b = UnityEngine.Object.Instantiate(prefab, parent);
                b.load(k);
                RectTransform r = b.GetComponent<RectTransform>();
                b.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                b.transform.localScale = Vector3.one * (size / Math.Max(1f, r.sizeDelta.y));
                LayoutElement le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }
            catch (Exception)
            {
            }
        }

        public static void Open(string id)
        {
            ScrollWindow.showWindow(id);
        }
    }
}
