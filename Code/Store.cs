using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Civilizations
{
    internal static class Store
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Str(BaseSystemData d, string key, string def = "")
        {
            if (d == null) return def;
            string v;
            d.get(key, out v, def);
            return v ?? def;
        }

        public static void Put(BaseSystemData d, string key, string value)
        {
            if (d == null) return;
            d.set(key, value ?? "");
        }

        public static int Int(BaseSystemData d, string key, int def = 0)
        {
            if (d == null) return def;
            int v;
            d.get(key, out v, def);
            return v;
        }

        public static void Put(BaseSystemData d, string key, int value)
        {
            if (d == null) return;
            d.set(key, value);
        }

        public static float Flt(BaseSystemData d, string key, float def = 0f)
        {
            if (d == null) return def;
            float v;
            d.get(key, out v, def);
            return v;
        }

        public static void Put(BaseSystemData d, string key, float value)
        {
            if (d == null) return;
            d.set(key, value);
        }

        public static bool Bool(BaseSystemData d, string key, bool def = false)
        {
            if (d == null) return def;
            bool v;
            d.get(key, out v, def);
            return v;
        }

        public static void Put(BaseSystemData d, string key, bool value)
        {
            if (d == null) return;
            d.set(key, value);
        }

        public static HashSet<string> Set(BaseSystemData d, string key)
        {
            var set = new HashSet<string>();
            string s = Str(d, key);
            if (s.Length == 0) return set;
            foreach (string p in s.Split(','))
            {
                if (p.Length > 0) set.Add(p);
            }
            return set;
        }

        public static void Set(BaseSystemData d, string key, IEnumerable<string> values)
        {
            Put(d, key, string.Join(",", values));
        }

        private static readonly Dictionary<string, Dictionary<string, float>> _parsed = new Dictionary<string, Dictionary<string, float>>();

        public static Dictionary<string, float> Map(BaseSystemData d, string key)
        {
            string s = Str(d, key);
            if (s.Length == 0) return new Dictionary<string, float>();
            Dictionary<string, float> m;
            if (!_parsed.TryGetValue(s, out m))
            {
                if (_parsed.Count > 4096) _parsed.Clear();
                m = ParseMap(s);
                _parsed[s] = m;
            }
            return new Dictionary<string, float>(m);
        }

        public static void Flush()
        {
            if (_parsed.Count > 1024) _parsed.Clear();
        }

        public static void Reset()
        {
            _parsed.Clear();
        }

        public static void Map(BaseSystemData d, string key, Dictionary<string, float> map)
        {
            Put(d, key, WriteMap(map));
        }

        public static Dictionary<string, float> ParseMap(string s)
        {
            var map = new Dictionary<string, float>();
            if (string.IsNullOrEmpty(s)) return map;
            foreach (string p in s.Split(';'))
            {
                int i = p.IndexOf(':');
                if (i <= 0) continue;
                float v;
                if (float.TryParse(p.Substring(i + 1), NumberStyles.Float, Inv, out v)) map[p.Substring(0, i)] = v;
            }
            return map;
        }

        public static string WriteMap(Dictionary<string, float> map)
        {
            var sb = new StringBuilder();
            foreach (var kv in map)
            {
                if (Math.Abs(kv.Value) < 0.0001f) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value.ToString("0.###", Inv));
            }
            return sb.ToString();
        }

        public static float Get(Dictionary<string, float> m, string k)
        {
            float v;
            return m.TryGetValue(k, out v) ? v : 0f;
        }

        public static void Add(Dictionary<string, float> m, string k, float v)
        {
            float c;
            m.TryGetValue(k, out c);
            m[k] = c + v;
        }

        public static SaveCustomData World()
        {
            if (global::World.world == null || global::World.world.map_stats == null) return null;
            MapStats ms = global::World.world.map_stats;
            if (ms.custom_data == null) ms.custom_data = new SaveCustomData();
            return ms.custom_data;
        }
    }
}
