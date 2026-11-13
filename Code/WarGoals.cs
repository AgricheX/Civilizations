using System.Collections.Generic;

namespace Civilizations
{
    internal static class WarGoals
    {
        private const string KGoal = "civ_war_goal";
        private static readonly Dictionary<long, string> _mem = new Dictionary<long, string>();

        public static readonly string[] All = { "raid", "tribute", "conquest", "subjugation", "independence", "holy", "ideology", "trade", "colonial", "succession", "liberation" };

        public static string Choose(Kingdom att, Kingdom def, string type)
        {
            if (type == "rebellion") return Diplomacy.Overlord(att) == def ? "independence" : "independence";
            KProfile a = Profile.Get(att), d = Profile.Get(def);
            if (Diplomacy.Overlord(att) == def) return "independence";
            if (Diplomacy.Overlord(def) != null && a.Fam.Contains("revolutionary")) return "liberation";
            if (a.Era <= 2) return "raid";
            var w = new Dictionary<string, float> { { "conquest", 2f } };
            if (a.Era <= 5) w["tribute"] = 2f;
            if (a.Strength > d.Strength * 2.5f && a.Era >= 3) w["subjugation"] = 1.5f + (a.Fam.Contains("imperial") ? 1.5f : 0f);
            if (a.Fam.Contains("theocratic") && a.Religion != d.Religion && d.Religion.Length > 0 && a.Era >= 5 && a.Era <= 11) w["holy"] = 3f;
            if ((a.Fam.Contains("revolutionary") || a.Fam.Contains("leftist")) && !d.Fam.Contains("leftist") && a.Era >= 10) w["ideology"] = 2.5f;
            if (a.Fam.Contains("democratic") && d.Fam.Contains("authoritarian") && a.Era >= 11) w["ideology"] = 1.2f;
            if ((a.Fam.Contains("mercantile") || a.Fam.Contains("corporate")) && a.Era >= 7) w["trade"] = 2f;
            WorldTile ta = G.Tile(att.capital), td = G.Tile(def.capital);
            if (a.Era >= 8 && a.Era <= 11 && ta != null && td != null && !ta.isSameIsland(td) && a.Strength > d.Strength * 1.5f) w["colonial"] = 2.5f;
            if (Diplomacy.Has(att, def, "royal_marriage") && a.Fam.Contains("monarchy")) w["succession"] = 2f;
            if (a.Fam.Contains("democratic") && a.Era >= 12) w["conquest"] = 0.4f;
            var keys = new List<string>(w.Keys);
            var vals = new List<float>(w.Values);
            return keys[G.Weighted(vals)];
        }

        public static void Set(global::War w, string goal)
        {
            if (w == null || w.data == null) return;
            _mem[w.data.id] = goal;
            Store.Put(w.data, KGoal, goal);
        }

        public static string Get(global::War w)
        {
            if (w == null || w.data == null) return "conquest";
            string g;
            if (_mem.TryGetValue(w.data.id, out g)) return g;
            g = Store.Str(w.data, KGoal, "conquest");
            _mem[w.data.id] = g;
            return g;
        }

        public static void Clear()
        {
            _mem.Clear();
        }
    }
}
