using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class GovUi
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
                _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                return _white;
            }
        }

        public static Image Square(Transform parent, Vector2 pos, float size, Color col)
        {
            Image img = UiKit.Img(parent, White, new Vector2(size, size), pos, col);
            img.preserveAspect = false;
            return img;
        }

        public static Image Rect(Transform parent, Vector2 pos, Vector2 size, Color col)
        {
            Image img = UiKit.Img(parent, White, size, pos, col);
            img.preserveAspect = false;
            return img;
        }

        public static void Ensure(string id, Action create)
        {
            ScrollWindow w;
            if (ScrollWindow._all_windows != null && ScrollWindow._all_windows.TryGetValue(id, out w) && w != null) return;
            create();
        }

        public static bool CanAct(Kingdom k)
        {
            return G.Ok(k) && (Player.Is(k) || !Player.Active);
        }

        public static void Row(Transform parent, string left, string right, Color? rc = null, Color? sq = null, Action click = null, string tipT = null, string tipD = null)
        {
            RectTransform box = UiKit.Box(parent, "Row", 14f);
            float x0 = -UiKit.W / 2f + 6f;
            if (sq.HasValue)
            {
                Square(box, new Vector2(x0 + 3f, 0f), 7f, sq.Value);
                x0 += 10f;
            }
            if (click != null)
            {
                UiKit.Btn(box, "", null, () => click(), new Vector2(UiKit.W - 6f, 13f), Vector2.zero, tipT, tipD, new Color(1f, 1f, 1f, 0.08f));
            }
            UiKit.Text(box, left, new Vector2(x0 + 60f, 0f), new Vector2(120f, 13f), 8, TextAnchor.MiddleLeft, UiKit.Soft);
            UiKit.Text(box, right, new Vector2(UiKit.W / 2f - 52f, 0f), new Vector2(100f, 13f), 8, TextAnchor.MiddleRight, rc ?? Color.white);
        }

        public static void Buttons(Transform parent, List<KeyValuePair<string, Action>> items, Action after)
        {
            if (items.Count == 0) return;
            for (int start = 0; start < items.Count; start += 3)
            {
                RectTransform box = UiKit.Box(parent, "Btns", 22f);
                int n = Math.Min(3, items.Count - start);
                float bw = (UiKit.W - 8f) / n - 3f;
                for (int i = 0; i < n; i++)
                {
                    var it = items[start + i];
                    float x = -UiKit.W / 2f + 4f + bw / 2f + i * (bw + 3f);
                    UiKit.Btn(box, Txt.T(it.Key), null, () =>
                    {
                        CivMod.Safe("gov-btn", () => it.Value());
                        if (after != null) after();
                    }, new Vector2(bw, 20f), new Vector2(x, 0f), Txt.T(it.Key), Txt.T(it.Key + "_d"));
                }
            }
        }
    }
}
