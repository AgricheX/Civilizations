using System;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal sealed class PillBtn
    {
        public Image Background;
        public Image Icon;
        public Text Label;
    }

    internal static class PhoneKit
    {
        public static readonly Color Bezel = new Color(0.07f, 0.07f, 0.09f, 1f);
        private static Sprite _round;
        private static Sprite _square;

        public static Sprite Round
        {
            get
            {
                if (_round != null) return _round;
                const int n = 16;
                const float r = 5.5f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float cx = x < r ? r : x > n - 1 - r ? n - 1 - r : x;
                        float cy = y < r ? r : y > n - 1 - r ? n - 1 - r : y;
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = Mathf.Clamp01(r + 0.5f - d);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false);
                _round = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
                return _round;
            }
        }

        public static Sprite Square
        {
            get
            {
                if (_square != null) return _square;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                tex.Apply(false);
                _square = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
                return _square;
            }
        }

        public static Image Rect(Transform parent, Color col, Vector2 size, Vector2 pos, bool round = true)
        {
            var go = new GameObject("Rect", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localPosition = new Vector3(pos.x, pos.y, 0f);
            Image img = go.GetComponent<Image>();
            img.sprite = round ? Round : Square;
            img.type = round ? Image.Type.Sliced : Image.Type.Simple;
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        public static Image Screen(RectTransform box, Color screen, float inset, float vgap)
        {
            Image bg = box.gameObject.GetComponent<Image>() ?? box.gameObject.AddComponent<Image>();
            bg.sprite = Square;
            bg.type = Image.Type.Simple;
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;
            if (vgap > 0f)
            {
                var basego = new GameObject("Base", typeof(RectTransform), typeof(Image));
                basego.transform.SetParent(box, false);
                RectTransform brt = basego.GetComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = new Vector2(inset, 0f);
                brt.offsetMax = new Vector2(-inset, 0f);
                Image bimg = basego.GetComponent<Image>();
                bimg.sprite = Square;
                bimg.color = new Color(0.9f, 0.91f, 0.95f);
                bimg.raycastTarget = false;
                inset += 3f;
            }
            var go = new GameObject("Screen", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(box, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, vgap);
            rt.offsetMax = new Vector2(-inset, -vgap);
            Image img = go.GetComponent<Image>();
            img.sprite = vgap > 0f ? Round : Square;
            img.type = vgap > 0f ? Image.Type.Sliced : Image.Type.Simple;
            img.color = screen;
            img.raycastTarget = false;
            return img;
        }

        public static PillBtn Btn(Transform parent, string text, Sprite icon, Action act, Vector2 size, Vector2 pos, Color? bg = null, Color? fg = null)
        {
            var go = new GameObject("Pill", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localPosition = new Vector3(pos.x, pos.y, 0f);
            Image img = go.GetComponent<Image>();
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.color = bg ?? new Color(0.88f, 0.9f, 0.95f);
            Button b = go.GetComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            b.colors = cb;
            if (act != null) b.onClick.AddListener(() => act());
            var pill = new PillBtn { Background = img };
            float tx = 0f;
            if (icon != null)
            {
                float s = Mathf.Min(size.y - 4f, 16f);
                bool alone = string.IsNullOrEmpty(text);
                pill.Icon = UiKit.Img(go.transform, icon, new Vector2(s, s), new Vector2(alone ? 0f : -size.x / 2f + s / 2f + 3f, 0f));
                tx = alone ? 0f : s / 2f + 2f;
            }
            if (!string.IsNullOrEmpty(text))
            {
                Text t = UiKit.Text(go.transform, text, new Vector2(tx, 0f), new Vector2(size.x - 4f - tx * 2f, size.y - 2f), 8, TextAnchor.MiddleCenter, fg ?? new Color(0.13f, 0.15f, 0.2f));
                t.color = fg ?? new Color(0.13f, 0.15f, 0.2f);
                pill.Label = t;
            }
            return pill;
        }

        public static PillBtn IconBtn(Transform parent, Sprite icon, Action act, float size, Vector2 pos, string tipTitle = null, string tipDesc = null, bool on = true, bool frame = true)
        {
            PillBtn p = Btn(parent, null, icon, act, new Vector2(size, size), pos, frame ? new Color(0.88f, 0.9f, 0.95f) : new Color(1f, 1f, 1f, 0f));
            if (p.Icon != null && !on) p.Icon.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            return p;
        }

        public static void Pager(Transform parent, int page, int pages, Action<int> go, Color screen, float width = 176f)
        {
            if (pages <= 1) return;
            RectTransform box = UiKit.Box(parent, "Pager", 24f, width);
            Screen(box, screen, 0f, 0f);
            Btn(box, "‹", null, () => go(Math.Max(0, page - 1)), new Vector2(30f, 18f), new Vector2(-50f, 0f), new Color(0.2f, 0.45f, 0.85f), Color.white);
            Text t = UiKit.Text(box, (page + 1) + " / " + pages, Vector2.zero, new Vector2(60f, 16f), 9, TextAnchor.MiddleCenter, new Color(0.35f, 0.38f, 0.46f));
            t.color = new Color(0.35f, 0.38f, 0.46f);
            Btn(box, "›", null, () => go(Math.Min(pages - 1, page + 1)), new Vector2(30f, 18f), new Vector2(50f, 0f), new Color(0.2f, 0.45f, 0.85f), Color.white);
        }
    }
}
