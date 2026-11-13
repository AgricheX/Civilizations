using System.Collections.Generic;

namespace Civilizations
{
    internal static class Firsts
    {
        private const string KFirsts = "civ_firsts";
        private static HashSet<string> _set;

        public static readonly HashSet<string> Milestones = new HashSet<string>
        {
            "fire_control", "agriculture", "writing", "the_wheel", "the_sail", "bronze_working", "law_code", "iron_working", "coinage", "cartography",
            "philosophy", "democracy", "paper", "gunpowder", "printing_press", "vaccination", "steam_engine", "railways", "telephone", "electricity",
            "combustion_engine", "automobile", "powered_flight", "radio", "nuclear_fission", "atomic_bomb", "television", "rocketry", "satellites", "moon_landing",
            "internet", "artificial_intelligence"
        };

        public static void Clear()
        {
            _set = null;
        }

        private static HashSet<string> Set()
        {
            if (_set != null) return _set;
            SaveCustomData d = Store.World();
            _set = d != null ? Store.Set(d, KFirsts) : new HashSet<string>();
            return _set;
        }

        private static bool Claim(string key)
        {
            HashSet<string> s = Set();
            if (!s.Add(key)) return false;
            SaveCustomData d = Store.World();
            if (d != null) Store.Set(d, KFirsts, s);
            return true;
        }

        public static void Era(Kingdom k, int era)
        {
            if (era <= 0 || !G.Ok(k)) return;
            if (Claim("era:" + era)) Hub.Raise(Hub.FirstInWorld, k, null, k.capital, k.king, "era", era.ToString(), era);
        }

        public static void Register()
        {
            Hub.On(Hub.TechLearned, e =>
            {
                if (!Milestones.Contains(e.Id) || !G.Ok(e.K)) return;
                if (Claim("tech:" + e.Id)) Hub.Raise(Hub.FirstInWorld, e.K, null, e.K.capital, e.K.king, "tech", e.Id);
            });
        }

        public static bool Has(string key)
        {
            return Set().Contains(key);
        }
    }
}
