using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class WinSize
    {
        public const float BaseW = 232f, BaseH = 270f;

        private class Snap
        {
            public Vector2 Bg, Sv, SvOff;
            public Vector4 Vp, MaskIn, BarIn, BarBgIn;
            public float MaskX, ContentTop;
            public Vector3 Close, Back, Title, TitleBg;
        }

        private static readonly Dictionary<int, Snap> _snaps = new Dictionary<int, Snap>();

        public static Vector2 Fit(float w, float h)
        {
            float scale = 1f;
            Canvas cv = CanvasMain.instance != null && CanvasMain.instance.canvas_ui != null ? CanvasMain.instance.canvas_ui.GetComponent<Canvas>() : null;
            if (cv != null) scale = Math.Max(0.01f, cv.scaleFactor);
            float maxW = Screen.width / scale - 90f, maxH = Screen.height / scale - 110f;
            return new Vector2(Mathf.Clamp(w, BaseW, Math.Max(BaseW, maxW)), Mathf.Clamp(h, BaseH, Math.Max(BaseH, maxH)));
        }

        private static RectTransform R(Transform t, string path)
        {
            if (t == null) return null;
            return t.Find(path) as RectTransform;
        }

        private static Vector3 Point(RectTransform r, Transform space, float nx, float ny)
        {
            Rect q = r.rect;
            return space.InverseTransformPoint(r.TransformPoint(new Vector3(Mathf.Lerp(q.xMin, q.xMax, nx), Mathf.Lerp(q.yMin, q.yMax, ny), 0f)));
        }

        private static void MoveTo(RectTransform r, Transform space, Vector3 target)
        {
            Vector3 now = Point(r, space, 0.5f, 0.5f);
            r.position += space.TransformPoint(target) - space.TransformPoint(now);
        }

        private static void SizeTo(RectTransform r, Vector2 size)
        {
            r.sizeDelta += size - r.rect.size;
        }

        private static Vector4 Insets(RectTransform child, RectTransform parent)
        {
            Vector3 a = Point(child, parent, 0f, 0f), b = Point(child, parent, 1f, 1f);
            Rect p = parent.rect;
            return new Vector4(a.x - p.xMin, a.y - p.yMin, p.xMax - b.x, p.yMax - b.y);
        }

        private static void Fill(RectTransform child, RectTransform parent, Vector4 i)
        {
            if (child == null || parent == null) return;
            Rect p = parent.rect;
            float w = p.width - i.x - i.z, h = p.height - i.y - i.w;
            SizeTo(child, new Vector2(w, h));
            MoveTo(child, parent, new Vector3(p.xMin + i.x + w / 2f, p.yMin + i.y + h / 2f, 0f));
        }

        private static RectTransform TitleOf(Transform root)
        {
            ScrollWindow sw = root != null ? root.GetComponent<ScrollWindow>() : null;
            return sw != null && sw.titleText != null ? sw.titleText.transform as RectTransform : null;
        }

        private static Snap Take(RectTransform bg, RectTransform root, RectTransform sv, RectTransform content)
        {
            var s = new Snap();
            s.Bg = bg.rect.size;
            s.Sv = sv != null ? sv.rect.size : new Vector2(BaseW, BaseH);
            if (sv != null)
            {
                Vector3 c = Point(sv, bg, 0.5f, 0.5f);
                s.SvOff = new Vector2(c.x - bg.rect.center.x, c.y - bg.rect.center.y);
                RectTransform vp = R(sv, "Viewport");
                if (vp != null)
                {
                    s.Vp = Insets(vp, sv);
                    RectTransform ct = content as RectTransform;
                    if (ct != null) s.ContentTop = vp.rect.yMax - Point(ct, vp, 0.5f, 1f).y;
                }
                RectTransform mask = R(sv, "Scrollbar Vertical Mask");
                if (mask != null)
                {
                    s.MaskIn = Insets(mask, sv);
                    s.MaskX = Point(mask, sv, 0.5f, 0.5f).x - sv.rect.xMax;
                    RectTransform bar = R(mask, "Scrollbar Vertical");
                    if (bar != null)
                    {
                        s.BarIn = Insets(bar, mask);
                        RectTransform bb = R(bar, "Background");
                        if (bb != null) s.BarBgIn = Insets(bb, bar);
                    }
                }
            }
            if (root != null)
            {
                RectTransform close = R(root, "CloseBackground");
                if (close != null) s.Close = Point(close, root, 0.5f, 0.5f) - Point(bg, root, 1f, 1f);
                RectTransform back = R(root, "BackButtonContainer");
                if (back != null) s.Back = Point(back, root, 0.5f, 0.5f) - Point(bg, root, 0f, 1f);
                RectTransform title = TitleOf(root);
                if (title != null && title.parent != null) s.Title = Point(title, title.parent, 0.5f, 0.5f) - Point(bg, title.parent, 0.5f, 1f);
            }
            RectTransform tbg = R(bg, "TitleBackground");
            if (tbg != null) s.TitleBg = Point(tbg, bg, 0.5f, 0.5f) - Point(bg, bg, 0.5f, 1f);
            return s;
        }

        public static Vector2 Set(Transform background, Transform content, float w, float h)
        {
            Vector2 size = Fit(w, h);
            RectTransform bg = background as RectTransform;
            if (bg == null) return size;
            RectTransform root = bg.parent as RectTransform;
            RectTransform sv = R(bg, "Scroll View");
            RectTransform ct = content as RectTransform;
            Snap s;
            int key = bg.GetInstanceID();
            if (!_snaps.TryGetValue(key, out s))
            {
                s = Take(bg, root, sv, ct);
                _snaps[key] = s;
                Dump("before", bg, root, sv, ct);
            }
            float dw = size.x - s.Sv.x, dh = size.y - s.Sv.y;
            ScrollWindow sw = root != null ? root.GetComponent<ScrollWindow>() : null;
            if (sw != null && (dw > 1f || dh > 1f))
            {
                WindowAsset wa = AssetManager.window_library.get(sw.screen_id);
                if (wa != null) wa.window_toolbar_enabled = false;
            }
            Transform space = root != null ? (Transform)root : bg.parent;
            Vector3 center = Point(bg, space, 0.5f, 0.5f);
            SizeTo(bg, new Vector2(s.Bg.x + dw, s.Bg.y + dh));
            MoveTo(bg, space, center);
            if (sv != null)
            {
                SizeTo(sv, size);
                MoveTo(sv, bg, new Vector3(bg.rect.center.x + s.SvOff.x, bg.rect.center.y + s.SvOff.y, 0f));
                RectTransform vp = R(sv, "Viewport");
                if (vp != null)
                {
                    Fill(vp, sv, s.Vp);
                    if (ct != null)
                    {
                        float hgt = ct.rect.height;
                        float cx = vp.InverseTransformPoint(sv.TransformPoint(new Vector3(sv.rect.center.x, 0f, 0f))).x;
                        ct.anchorMin = new Vector2(0.5f, 1f);
                        ct.anchorMax = new Vector2(0.5f, 1f);
                        ct.pivot = new Vector2(0.5f, 1f);
                        ct.sizeDelta = new Vector2(size.x - 18f, hgt);
                        ct.anchoredPosition = new Vector2(cx - vp.rect.center.x, -s.ContentTop);
                    }
                }
                RectTransform mask = R(sv, "Scrollbar Vertical Mask");
                if (mask != null)
                {
                    Rect q = sv.rect;
                    float mh = q.height - s.MaskIn.y - s.MaskIn.w;
                    SizeTo(mask, new Vector2(mask.rect.width, mh));
                    MoveTo(mask, sv, new Vector3(q.xMax + s.MaskX, q.yMin + s.MaskIn.y + mh / 2f, 0f));
                    RectTransform bar = R(mask, "Scrollbar Vertical");
                    if (bar != null)
                    {
                        Fill(bar, mask, s.BarIn);
                        Fill(R(bar, "Background"), bar, s.BarBgIn);
                    }
                }
            }
            RectTransform grad = R(bg, "Scrollgradient");
            if (grad != null) foreach (Graphic g in grad.GetComponentsInChildren<Graphic>(true)) g.enabled = false;
            if (root != null)
            {
                RectTransform close = R(root, "CloseBackground");
                if (close != null) MoveTo(close, root, Point(bg, root, 1f, 1f) + s.Close);
                RectTransform back = R(root, "BackButtonContainer");
                if (back != null) MoveTo(back, root, Point(bg, root, 0f, 1f) + s.Back);
                RectTransform title = TitleOf(root);
                if (title != null && title.parent != null) MoveTo(title, title.parent, Point(bg, title.parent, 0.5f, 1f) + s.Title);
            }
            RectTransform tbg = R(bg, "TitleBackground");
            if (tbg != null) MoveTo(tbg, bg, Point(bg, bg, 0.5f, 1f) + s.TitleBg);
            VerticalLayoutGroup vl = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (vl != null) vl.childAlignment = TextAnchor.UpperCenter;
            Dump("after", bg, root, sv, ct);
            return size;
        }

        private static void Dump(string tag, RectTransform bg, RectTransform root, RectTransform sv, RectTransform ct)
        {
            if (CivMod.C == null || !CivMod.C.DebugLog) return;
            var sb = new StringBuilder();
            sb.Append("[WinSize] ").Append(root != null ? root.name : bg.name).Append(' ').Append(tag);
            Line(sb, "root", root);
            Line(sb, "bg", bg);
            Line(sb, "close", R(root, "CloseBackground"));
            Line(sb, "back", R(root, "BackButtonContainer"));
            Line(sb, "title", TitleOf(root));
            Line(sb, "sv", sv);
            Line(sb, "vp", R(sv, "Viewport"));
            Line(sb, "content", ct);
            Line(sb, "mask", R(sv, "Scrollbar Vertical Mask"));
            CivMod.Log(sb.ToString());
        }

        private static void Line(StringBuilder sb, string name, RectTransform r)
        {
            if (r == null) return;
            sb.Append("\n  ").Append(name)
                .Append(" amin=").Append(r.anchorMin).Append(" amax=").Append(r.anchorMax)
                .Append(" pivot=").Append(r.pivot).Append(" pos=").Append(r.anchoredPosition)
                .Append(" sd=").Append(r.sizeDelta).Append(" rect=").Append(r.rect);
        }
    }
}
