using System;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class DayNight
    {
        private static GameObject _go;
        private static SpriteRenderer _sr;
        private static Texture2D _tex;
        private static Color32[] _px;
        private static int _w;
        private static int _mapW;
        private static int _mapH;
        private static float _redraw;
        private static float _camNight;
        private static bool _active;
        private static Color32[] _next;
        private static int _nf = -1;
        private static float _nv;
        private static int _lf = -1;
        private static bool _lv;
        private const int MaxLights = 400;

        private static bool Perf
        {
            get { return CivMod.C != null && CivMod.C.PerfMode; }
        }

        private static bool LightsAllowed
        {
            get
            {
                if (Perf || CivMod.C != null && !CivMod.C.NightLights) return false;
                return MapBox.isRenderGameplay();
            }
        }

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.DayNight; }
        }

        public static SpriteRenderer Renderer
        {
            get { return _sr; }
        }

        public static float CameraNight
        {
            get { return _active ? _camNight : 0f; }
        }

        public static void Clear()
        {
            _active = false;
            _camNight = 0f;
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _go = null;
            _sr = null;
            _tex = null;
        }

        public static int Bands
        {
            get
            {
                int band = CivMod.C != null ? CivMod.C.BandTiles : 100;
                return Math.Max(1, (int)Math.Round(MapBox.width / (float)band));
            }
        }

        public static float LocalTime(float x)
        {
            float day = (CivMod.C != null ? CivMod.C.DayMinutes : 24) * 60f;
            double t = World.world.getCurWorldTime();
            int n = Bands;
            float band = n <= 1 ? 0f : Mathf.Floor(G.Clamp(x / MapBox.width, 0f, 0.9999f) * n) / n;
            double v = t / day + band + 0.25;
            return (float)(v - Math.Floor(v));
        }

        private static float Sun(float tau)
        {
            return -Mathf.Cos(tau * Mathf.PI * 2f);
        }

        private static float Dark(float tau)
        {
            float s = Sun(tau);
            float strength = CivMod.C != null ? CivMod.C.NightStrength : 0.55f;
            float k = Mathf.InverseLerp(0.15f, -0.3f, s);
            return strength * k * k * (3f - 2f * k);
        }

        private static float Twilight(float tau)
        {
            float s = Mathf.Abs(Sun(tau));
            return s > 0.3f ? 0f : (1f - s / 0.3f) * 0.18f;
        }

        public static void Tick()
        {
            if (!On || World.world == null)
            {
                if (_active) Clear();
                return;
            }
            _active = true;
            Ensure();
            _redraw -= Time.unscaledDeltaTime;
            if (_redraw > 0f) return;
            _redraw = Perf ? 1f : 0.25f;
            Camera cam = World.world.camera;
            float xmin = 0f, xmax = MapBox.width;
            if (cam != null)
            {
                xmin = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;
                xmax = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;
            }
            float minDark = 1f;
            for (int i = 0; i <= 6; i++)
            {
                float x = Mathf.Lerp(xmin, xmax, i / 6f);
                if (x < 0f || x > MapBox.width) continue;
                minDark = Math.Min(minDark, Dark(LocalTime(x)));
            }
            if (minDark > 0.99f) minDark = Dark(LocalTime(MapBox.width * 0.5f));
            _camNight = minDark;
            SpriteRenderer night = null;
            if (WorldAgeEffects.instance != null) WorldAgeEffects.instance.dict_effects.TryGetValue("night", out night);
            bool vanilla = night != null && night.enabled && night.gameObject.activeInHierarchy && night.color.a > 0.02f;
            if (!vanilla) minDark = 0f;
            if (_tex == null) return;
            for (int p = 0; p < _w; p++)
            {
                float x = (p + 0.5f) / _w * MapBox.width;
                float tau = LocalTime(x);
                float extra = Math.Max(0f, Dark(tau) - minDark);
                float tw = Twilight(tau);
                float a = Math.Min(0.85f, extra + tw);
                Color c = Color.Lerp(new Color(0.03f, 0.05f, 0.16f), new Color(0.95f, 0.45f, 0.15f), a <= 0f ? 0f : tw / a);
                c.a = a;
                _next[p] = c;
            }
            bool changed = false;
            for (int p = 0; p < _w; p++)
            {
                Color32 o = _px[p], n = _next[p];
                if (Math.Abs(o.a - n.a) > 1 || Math.Abs(o.r - n.r) > 1 || Math.Abs(o.g - n.g) > 1 || Math.Abs(o.b - n.b) > 1)
                {
                    changed = true;
                    break;
                }
            }
            if (!changed) return;
            Color32[] swap = _px;
            _px = _next;
            _next = swap;
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        private static void Ensure()
        {
            if (_go != null && _mapW == MapBox.width && _mapH == MapBox.height) return;
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _mapW = MapBox.width;
            _mapH = MapBox.height;
            _w = Math.Max(4, Bands * 8);
            _tex = new Texture2D(_w, 1, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Bilinear;
            _tex.wrapMode = TextureWrapMode.Clamp;
            _px = new Color32[_w];
            _next = new Color32[_w];
            for (int i = 0; i < _w; i++) _px[i] = new Color32(0, 0, 0, 255);
            float ppu = _w / (float)_mapW;
            Sprite s = Sprite.Create(_tex, new Rect(0, 0, _w, 1), new Vector2(0.5f, 0.5f), ppu);
            _go = new GameObject("CivDayNight");
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _sr = _go.AddComponent<SpriteRenderer>();
            _sr.sprite = s;
            SpriteRenderer night = null;
            if (WorldAgeEffects.instance != null) WorldAgeEffects.instance.dict_effects.TryGetValue("night", out night);
            if (night != null)
            {
                _sr.sortingLayerID = night.sortingLayerID;
                _sr.sortingOrder = night.sortingOrder - 1;
            }
            else
            {
                _sr.sortingOrder = 5000;
            }
            _go.transform.position = new Vector3(_mapW / 2f, _mapH / 2f, -5f);
            _go.transform.localScale = new Vector3(1f, _mapH * ppu, 1f);
        }

        [HarmonyPatch(typeof(WorldAgeManager), nameof(WorldAgeManager.getNightMod))]
        private static class Night
        {
            private static void Postfix(ref float __result)
            {
                if (!_active) return;
                int f = Time.frameCount;
                if (_nf == f)
                {
                    __result = _nv;
                    return;
                }
                _nf = f;
                _nv = Math.Max(__result, _camNight);
                __result = _nv;
            }
        }

        [HarmonyPatch(typeof(WorldAgeManager), nameof(WorldAgeManager.shouldShowLights))]
        private static class Lights
        {
            private static void Postfix(ref bool __result)
            {
                if (!_active || !__result || _camNight <= 0f) return;
                int f = Time.frameCount;
                if (_lf != f)
                {
                    _lf = f;
                    _lv = LightsAllowed;
                }
                __result = _lv;
            }
        }

        [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawLightAreas")]
        private static class CapLights
        {
            private static void Prefix()
            {
                if (!_active || World.world == null || World.world.stack_effects == null) return;
                var list = World.world.stack_effects.light_blobs;
                if (list != null && list.Count > MaxLights) list.RemoveRange(MaxLights, list.Count - MaxLights);
            }
        }
    }
}
