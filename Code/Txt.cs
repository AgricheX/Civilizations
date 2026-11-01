using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Civilizations
{
    internal static class Txt
    {
        private static Dictionary<string, string> _en = new Dictionary<string, string>();
        private static Dictionary<string, string> _pt = new Dictionary<string, string>();
        private static readonly Dictionary<string, Dictionary<string, string>> _other = new Dictionary<string, Dictionary<string, string>>();

        public static void Init()
        {
            _en = Load("en.json");
            _pt = Load("br.json");
            _other.Clear();
        }

        private static Dictionary<string, string> Current()
        {
            string l = LocalizedTextManager.instance != null ? LocalizedTextManager.instance.language : "en";
            if (string.IsNullOrEmpty(l) || l == "en") return null;
            if (l == "pt" || l == "br") return _pt;
            Dictionary<string, string> d;
            if (!_other.TryGetValue(l, out d))
            {
                d = Load(l + ".json");
                _other[l] = d;
            }
            return d;
        }

        private static Dictionary<string, string> Load(string file)
        {
            try
            {
                string path = Path.Combine(CivMod.Folder, "Locales", file);
                if (!File.Exists(path)) return new Dictionary<string, string>();
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
            }
            catch (Exception)
            {
                return new Dictionary<string, string>();
            }
        }

        public static string T(string key)
        {
            string v;
            Dictionary<string, string> cur = Current();
            if (cur != null && cur.TryGetValue(key, out v)) return v;
            if (_en.TryGetValue(key, out v)) return v;
            try
            {
                if (LocalizedTextManager.stringExists(key)) return LocalizedTextManager.getText(key);
            }
            catch (Exception)
            {
            }
            return key;
        }

        public static string F(string key, params object[] args)
        {
            try
            {
                return string.Format(T(key), args);
            }
            catch (Exception)
            {
                return T(key);
            }
        }

        public static bool Has(string key)
        {
            return _en.ContainsKey(key);
        }
    }
}
