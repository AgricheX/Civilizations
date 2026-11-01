using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Civilizations
{
    internal static class CivProfiler
    {
        private static readonly Dictionary<string, double> _acc = new Dictionary<string, double>();
        private static readonly Dictionary<string, double> _avg = new Dictionary<string, double>();
        private static int _frames;
        private static float _next;
        private static string _text = "";
        private static GUIStyle _style;
        private static bool _on;
        private static float _check;

        public static bool On { get { return _on; } }

        public static long Now()
        {
            long t = Stopwatch.GetTimestamp();
            return t == 0 ? 1 : t;
        }

        public static void Add(string what, long t0)
        {
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            double c;
            _acc.TryGetValue(what, out c);
            _acc[what] = c + ms;
        }

        public static void Frame()
        {
            if (Time.unscaledTime >= _check)
            {
                _check = Time.unscaledTime + 1f;
                _on = CivMod.C != null && CivMod.C.Profiler;
                if (!_on) { _acc.Clear(); _text = ""; }
            }
            if (!_on) return;
            _frames++;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            _avg.Clear();
            foreach (var kv in _acc) _avg[kv.Key] = kv.Value / Mathf.Max(1, _frames);
            _acc.Clear();
            var sb = new StringBuilder();
            double total = _avg.Values.Sum();
            sb.Append("Civilizations  ").Append(total.ToString("0.00")).Append(" ms/frame  FPS ").Append((_frames / 1f).ToString("0")).Append('\n');
            foreach (var kv in _avg.OrderByDescending(x => x.Value).Take(10))
                sb.Append(kv.Value.ToString("0.000")).Append("  ").Append(kv.Key).Append('\n');
            _text = sb.ToString();
            _frames = 0;
        }

        public static void Draw()
        {
            if (!_on || _text.Length == 0) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.UpperLeft;
                _style.fontSize = 12;
                _style.normal.textColor = Color.white;
            }
            GUI.Box(new Rect(10, 60, 300, 190), _text, _style);
        }
    }
}
