using System;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class WinSize
    {
        public const float BaseW = 232f, BaseH = 270f;

        public static Vector2 Fit(float w, float h)
        {
            float sw = Screen.width, sh = Screen.height;
            CanvasScaler cs = CanvasMain.instance != null && CanvasMain.instance.canvas_ui != null ? CanvasMain.instance.canvas_ui.GetComponent<CanvasScaler>() : null;
            float scale = cs != null && cs.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize ? Math.Max(0.01f, cs.scaleFactor) : 1f;
            Canvas cv = CanvasMain.instance != null && CanvasMain.instance.canvas_ui != null ? CanvasMain.instance.canvas_ui.GetComponent<Canvas>() : null;
            if (cv != null) scale = Math.Max(0.01f, cv.scaleFactor);
            float maxW = sw / scale - 80f, maxH = sh / scale - 90f;
            return new Vector2(Mathf.Clamp(w, BaseW, Math.Max(BaseW, maxW)), Mathf.Clamp(h, BaseH, Math.Max(BaseH, maxH)));
        }

        public static Vector2 Set(Transform background, Transform content, float w, float h)
        {
            Vector2 size = Fit(w, h);
            if (background == null) return size;
            RectTransform bg = background as RectTransform;
            if (bg == null) return size;
            float dw = size.x - BaseW, dh = size.y - BaseH;
            Vector2 cur = bg.sizeDelta;
            if (cur.x <= 0f || cur.y <= 0f) cur = new Vector2(BaseW + 20f, BaseH + 40f);
            if (!_baseBg.HasValue) _baseBg = cur;
            bg.sizeDelta = new Vector2(_baseBg.Value.x + dw, _baseBg.Value.y + dh);
            RectTransform sv = background.Find("Scroll View") as RectTransform;
            if (sv != null)
            {
                sv.sizeDelta = new Vector2(BaseW + dw, BaseH + dh);
                sv.localPosition = new Vector3(0f, -6f, 0f);
                RectTransform vp = sv.Find("Viewport") as RectTransform;
                if (vp != null) vp.localPosition = new Vector3(-(BaseW + dw) / 2f - 15f, (BaseH + dh) / 2f, 0f);
            }
            RectTransform ct = content as RectTransform;
            if (ct != null) ct.sizeDelta = new Vector2(BaseW + dw - 18f, ct.sizeDelta.y);
            VerticalLayoutGroup vl = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (vl != null) vl.childAlignment = TextAnchor.UpperCenter;
            return size;
        }

        private static Vector2? _baseBg;
    }
}
