using System;
using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal static class G
    {
        public static bool Loaded
        {
            get
            {
                return World.world != null && Config.game_loaded && World.world.tiles_list != null && World.world.tiles_list.Length > 0 && !SmoothLoader.isLoading() && World.world.map_stats != null;
            }
        }

        public static int Year
        {
            get { return Date.getCurrentYear(); }
        }

        public static bool Pt
        {
            get
            {
                string l = LocalizedTextManager.instance != null ? LocalizedTextManager.instance.language : "en";
                return l == "pt" || l == "br";
            }
        }

        private static List<Kingdom> _kc;
        private static int _kf = -1, _kn = -1;
        private static List<City> _cc;
        private static int _cf = -1, _cn = -1;

        public static List<Kingdom> Kingdoms()
        {
            if (World.world == null || World.world.kingdoms == null) return new List<Kingdom>();
            int n = World.world.kingdoms.list.Count;
            if (_kc != null && _kf == Time.frameCount && _kn == n) return _kc;
            var list = new List<Kingdom>();
            foreach (Kingdom k in World.world.kingdoms.list)
            {
                if (k != null && k.isAlive() && k.isCiv()) list.Add(k);
            }
            _kc = list; _kf = Time.frameCount; _kn = n;
            return list;
        }

        public static List<City> Cities()
        {
            if (World.world == null || World.world.cities == null) return new List<City>();
            int n = World.world.cities.list.Count;
            if (_cc != null && _cf == Time.frameCount && _cn == n) return _cc;
            var list = new List<City>();
            foreach (City c in World.world.cities.list)
            {
                if (c != null && c.isAlive()) list.Add(c);
            }
            _cc = list; _cf = Time.frameCount; _cn = n;
            return list;
        }

        public static void ResetCache()
        {
            _kc = null; _cc = null; _kf = -1; _cf = -1;
        }

        public static List<City> Cities(Kingdom k)
        {
            var list = new List<City>();
            if (k == null) return list;
            foreach (City c in k.cities)
            {
                if (c != null && c.isAlive()) list.Add(c);
            }
            return list;
        }

        public static bool Ok(Actor a)
        {
            return a != null && a.isAlive() && a.data != null;
        }

        public static bool Ok(City c)
        {
            return c != null && c.isAlive() && c.data != null;
        }

        public static bool Ok(Kingdom k)
        {
            return k != null && k.isAlive() && k.data != null;
        }

        public static float Clamp(float v, float a, float b)
        {
            return v < a ? a : (v > b ? b : v);
        }

        public static bool Chance(float p)
        {
            return UnityEngine.Random.value < p;
        }

        public static int Range(int a, int b)
        {
            return UnityEngine.Random.Range(a, b);
        }

        public static T Pick<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) return default(T);
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static int Pop(Kingdom k)
        {
            int n = 0;
            foreach (City c in Cities(k)) n += c.getPopulationPeople();
            return n;
        }

        public static string Name(Kingdom k)
        {
            if (k == null || k.data == null) return "?";
            return k.data.name ?? "?";
        }

        public static string Name(City c)
        {
            if (c == null || c.data == null) return "?";
            return c.data.name ?? "?";
        }

        public static string Name(Actor a)
        {
            if (a == null || a.data == null) return "?";
            return a.getName() ?? "?";
        }

        public static WorldTile Tile(City c)
        {
            if (c == null) return null;
            try
            {
                return c.getTile();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static float Dist(City a, City b)
        {
            WorldTile ta = Tile(a);
            WorldTile tb = Tile(b);
            if (ta == null || tb == null) return 99999f;
            return Dist(ta, tb);
        }

        public static float Dist(WorldTile a, WorldTile b)
        {
            if (a == null || b == null) return 99999f;
            return Mathf.Sqrt(Globe.SqDist(a.x, a.y, b.x, b.y));
        }

        public static float Dist(Vector2 a, Vector2 b)
        {
            return Mathf.Sqrt(Globe.SqDist(a.x, a.y, b.x, b.y));
        }

        public static int Weighted(IList<float> w)
        {
            if (w == null || w.Count == 0) return 0;
            float sum = 0f;
            foreach (float x in w) sum += Math.Max(0f, x);
            if (sum <= 0f) return UnityEngine.Random.Range(0, w.Count);
            float r = UnityEngine.Random.value * sum;
            for (int i = 0; i < w.Count; i++)
            {
                r -= Math.Max(0f, w[i]);
                if (r <= 0f) return i;
            }
            return w.Count - 1;
        }

        public static Kingdom KingdomById(string id)
        {
            long v;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out v)) return null;
            Kingdom k = World.world.kingdoms.get(v);
            return k != null && k.isAlive() ? k : null;
        }

        public static City CityById(string id)
        {
            long v;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out v)) return null;
            City c = World.world.cities.get(v);
            return c != null && c.isAlive() ? c : null;
        }

        public static Actor ActorById(string id)
        {
            long v;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out v)) return null;
            Actor a = World.world.units.get(v);
            return a != null && a.isAlive() ? a : null;
        }

        public static string Id(Kingdom k)
        {
            return k == null || k.data == null ? "" : k.data.id.ToString();
        }

        public static string Id(City c)
        {
            return c == null || c.data == null ? "" : c.data.id.ToString();
        }

        public static string Id(Actor a)
        {
            return a == null || a.data == null ? "" : a.data.id.ToString();
        }

        public static string Hex(Kingdom k)
        {
            try
            {
                return k != null ? k.getColor().color_text : "#FFFFFF";
            }
            catch (Exception)
            {
                return "#FFFFFF";
            }
        }

        public static string Col(string hex, string text)
        {
            return "<color=" + hex + ">" + text + "</color>";
        }

        public static string Pct(float v)
        {
            return Mathf.RoundToInt(v * 100f) + "%";
        }

        public static string Signed(float v)
        {
            return (v >= 0 ? "+" : "") + v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static Color Col(string hex)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return Color.white;
        }

        public static string Short(float v)
        {
            float a = Math.Abs(v);
            if (a >= 1000000f) return (v / 1000000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
            if (a >= 1000f) return (v / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "k";
            return Mathf.RoundToInt(v).ToString();
        }
    }
}
