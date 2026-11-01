using System.Collections.Generic;

namespace Civilizations
{
    internal static class WorldPrefs
    {
        private const string KRoute = "civ_route_pref";
        public static readonly string[] Routes = { "real", "steam", "tesla", "diesel", "atom", "bio" };

        public static string Route
        {
            get
            {
                SaveCustomData d = Store.World();
                string r = d == null ? "" : Store.Str(d, KRoute);
                return r.Length == 0 ? "real" : r;
            }
            set
            {
                SaveCustomData d = Store.World();
                if (d == null) return;
                Store.Put(d, KRoute, string.IsNullOrEmpty(value) ? "real" : value);
            }
        }

        public static string Icon(string route)
        {
            return "ui/Icons/civ/route/" + route;
        }

        public static Dictionary<string, int> Followers()
        {
            var m = new Dictionary<string, int>();
            foreach (string r in Routes) m[r] = 0;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                string r = Research.Route(k);
                if (string.IsNullOrEmpty(r)) continue;
                int n;
                m.TryGetValue(r, out n);
                m[r] = n + 1;
            }
            return m;
        }
    }
}
