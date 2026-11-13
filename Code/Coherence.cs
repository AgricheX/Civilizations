using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal class Rule
    {
        public int MinEra;
        public int MaxEra = 99;
        public List<string> Need = new List<string>();
        public List<string> Any = new List<string>();
        public List<string> Not = new List<string>();
        public Dictionary<string, float> Favor = new Dictionary<string, float>();
        public string Tech = "";
    }

    internal static class Coherence
    {
        private static readonly Dictionary<string, string[]> FamilyOf = new Dictionary<string, string[]>
        {
            { "tribal_band", new[] { "tribal" } },
            { "chiefdom", new[] { "tribal", "monarchy" } },
            { "tribal_council", new[] { "tribal", "republic" } },
            { "monarchy", new[] { "monarchy" } },
            { "feudal_monarchy", new[] { "monarchy", "feudal" } },
            { "elective_monarchy", new[] { "monarchy", "democratic" } },
            { "absolute_monarchy", new[] { "monarchy", "authoritarian" } },
            { "constitutional_monarchy", new[] { "monarchy", "democratic" } },
            { "theocracy", new[] { "theocratic", "authoritarian" } },
            { "republic", new[] { "republic", "democratic" } },
            { "democracy", new[] { "republic", "democratic" } },
            { "merchant_republic", new[] { "republic", "mercantile" } },
            { "presidential_republic", new[] { "republic", "democratic" } },
            { "parliamentary_republic", new[] { "republic", "democratic" } },
            { "single_party_state", new[] { "authoritarian", "revolutionary" } },
            { "military_junta", new[] { "authoritarian", "military" } },
            { "technocracy", new[] { "technocratic", "authoritarian" } },
            { "corporatocracy", new[] { "corporate", "authoritarian" } },
            { "traditionalism", new[] { "traditional" } },
            { "religious_zeal", new[] { "theocratic", "traditional" } },
            { "legalism", new[] { "traditional", "authoritarian" } },
            { "militarism", new[] { "military" } },
            { "humanism", new[] { "liberal" } },
            { "liberalism", new[] { "liberal", "democratic" } },
            { "conservatism", new[] { "traditional" } },
            { "nationalism", new[] { "nationalist" } },
            { "socialism", new[] { "leftist" } },
            { "communism", new[] { "leftist", "revolutionary" } },
            { "anarchism", new[] { "leftist", "revolutionary" } },
            { "authoritarianism", new[] { "authoritarian" } },
            { "environmentalism", new[] { "green" } },
            { "technoprogressivism", new[] { "technocratic" } },
            { "reciprocity", new[] { "tribal" } },
            { "palace_economy", new[] { "traditional" } },
            { "tributary", new[] { "imperial" } },
            { "manorialism", new[] { "feudal" } },
            { "guild_economy", new[] { "mercantile" } },
            { "mercantilism", new[] { "mercantile", "imperial" } },
            { "laissez_faire", new[] { "corporate", "liberal" } },
            { "planned", new[] { "planned", "leftist" } },
            { "welfare", new[] { "leftist" } },
            { "mixed_economy", new[] { "democratic" } },
            { "corporatist", new[] { "corporate" } },
            { "digital_economy", new[] { "corporate" } },
            { "planetary_federation", new[] { "republic", "democratic", "federal" } },
            { "ai_technocracy", new[] { "technocratic", "authoritarian", "machine" } },
            { "stellar_empire", new[] { "monarchy", "imperial", "authoritarian" } },
            { "interstellar_republic", new[] { "republic", "democratic", "federal" } },
            { "hive_mind", new[] { "collective", "authoritarian" } },
            { "distributed_consciousness", new[] { "collective", "technocratic", "democratic" } },
            { "cosmic_theocracy", new[] { "theocratic", "authoritarian" } },
            { "galactic_corporation", new[] { "corporate", "authoritarian" } },
            { "cosmism", new[] { "imperial", "technocratic" } },
            { "transhumanism", new[] { "technocratic", "liberal" } },
            { "xenounity", new[] { "liberal", "xenophile" } },
            { "post_scarcity", new[] { "planned", "leftist" } },
            { "galactic_market", new[] { "corporate", "mercantile" } },
        };

        private static readonly Dictionary<string, int> Obsolete = new Dictionary<string, int>
        {
            { "tribal_band", 6 }, { "chiefdom", 7 }, { "tribal_council", 8 }, { "monarchy", 12 },
            { "feudal_monarchy", 11 }, { "elective_monarchy", 11 }, { "absolute_monarchy", 12 }, { "merchant_republic", 11 },
            { "legalism", 10 },
            { "reciprocity", 6 }, { "palace_economy", 8 }, { "tributary", 9 }, { "manorialism", 10 }, { "guild_economy", 11 }, { "mercantilism", 12 },
            { "constitutional_monarchy", 21 }, { "theocracy", 27 }, { "military_junta", 24 }, { "single_party_state", 24 }, { "corporatocracy", 27 },
            { "traditionalism", 24 }, { "religious_zeal", 27 }, { "planned", 21 }, { "welfare", 21 }, { "laissez_faire", 24 }, { "mixed_economy", 24 }, { "corporatist", 27 },
            { "planetary_federation", 24 }, { "ai_technocracy", 33 },
        };

        private static readonly Dictionary<string, int> Home = new Dictionary<string, int>();

        public static void Families(string gov, string ideo, string econ, HashSet<string> into)
        {
            Add(gov, into);
            Add(ideo, into);
            Add(econ, into);
        }

        private static void Add(string id, HashSet<string> into)
        {
            string[] f;
            if (!string.IsNullOrEmpty(id) && FamilyOf.TryGetValue(id, out f)) foreach (string x in f) into.Add(x);
        }

        public static bool Is(string policy, string family)
        {
            string[] f;
            return !string.IsNullOrEmpty(policy) && FamilyOf.TryGetValue(policy, out f) && Array.IndexOf(f, family) >= 0;
        }

        public static bool IsObsolete(string policy, int era)
        {
            int o;
            return !string.IsNullOrEmpty(policy) && Obsolete.TryGetValue(policy, out o) && era >= o;
        }

        public static int HomeEra(PolicyDef p)
        {
            if (p == null) return 0;
            int e;
            if (Home.TryGetValue(p.Id, out e)) return e;
            TechDef t = string.IsNullOrEmpty(p.Req) ? null : Db.Tech(p.Req);
            e = t != null ? t.Era : 0;
            Home[p.Id] = e;
            return e;
        }

        public static float EraFit(PolicyDef p, int era)
        {
            if (p == null) return 1f;
            if (IsObsolete(p.Id, era)) return 0.02f;
            int gap = era - HomeEra(p);
            if (gap <= 1) return 1.3f;
            return G.Clamp(1.3f - 0.1f * (gap - 1), 0.35f, 1.3f);
        }

        public static float Weight(KProfile p, Rule r)
        {
            if (p == null || r == null) return 0f;
            if (p.Era < r.MinEra || p.Era > r.MaxEra) return 0f;
            foreach (string t in r.Need) if (!p.Tags.Contains(t)) return 0f;
            foreach (string t in r.Not) if (p.Tags.Contains(t)) return 0f;
            if (r.Any.Count > 0)
            {
                bool ok = false;
                foreach (string t in r.Any)
                {
                    if (p.Tags.Contains(t))
                    {
                        ok = true;
                        break;
                    }
                }
                if (!ok) return 0f;
            }
            float w = 1f;
            foreach (var kv in r.Favor) if (p.Tags.Contains(kv.Key)) w *= kv.Value;
            return w;
        }

        public static bool Allows(Kingdom k, Rule r)
        {
            if (r == null) return true;
            if (!string.IsNullOrEmpty(r.Tech) && !Research.Has(k, r.Tech)) return false;
            return Weight(Profile.Get(k), r) > 0f;
        }
    }
}
