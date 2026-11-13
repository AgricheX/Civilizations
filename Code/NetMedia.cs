using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class NetMedia
    {
        public static readonly string[] Order = { "crier", "scroll", "gazette", "paper", "broadcast", "social" };

        public static readonly string[] Topics = { "war", "diplomacy", "politics", "science", "economy", "faith", "culture", "people", "world", "disaster" };

        private static readonly Dictionary<string, string> KindTopic = new Dictionary<string, string>
        {
            { "war_start", "war" }, { "war_defend", "war" }, { "war_end", "war" }, { "war_lost", "war" }, { "peace", "war" }, { "city_taken", "war" }, { "city_lost", "war" }, { "sack", "war" },
            { "conquest", "war" }, { "nuke", "war" }, { "nuke_built", "war" }, { "nuke_launch", "war" }, { "civil_war", "war" }, { "soldier", "war" }, { "live_war", "war" }, { "border", "war" }, { "walls", "war" },
            { "treaty", "diplomacy" }, { "treaty_broken", "diplomacy" }, { "dependent", "diplomacy" }, { "independence", "diplomacy" }, { "integrated", "diplomacy" }, { "first_contact", "diplomacy" },
            { "org", "diplomacy" }, { "org_vote", "diplomacy" }, { "world_order", "diplomacy" }, { "embassy", "diplomacy" }, { "union", "diplomacy" },
            { "policy", "politics" }, { "revolution", "politics" }, { "election", "politics" }, { "campaign", "politics" }, { "debate", "politics" }, { "poll", "politics" }, { "coup", "politics" },
            { "legitimacy", "politics" }, { "flag", "politics" }, { "opinion", "politics" }, { "pressure", "politics" }, { "leak", "politics" }, { "stage", "politics" }, { "clan", "politics" },
            { "tech", "science" }, { "science", "science" }, { "discovery", "science" }, { "era", "science" }, { "tech_lost", "science" }, { "regression", "science" }, { "route", "science" },
            { "launch", "science" }, { "patent", "science" }, { "world_first", "science" }, { "great_person", "people" },
            { "company", "economy" }, { "ceo", "economy" }, { "merger", "economy" }, { "bankrupt", "economy" }, { "bailout", "economy" }, { "currency", "economy" }, { "inflation", "economy" },
            { "stock_up", "economy" }, { "stock_down", "economy" }, { "crypto_up", "economy" }, { "crypto_down", "economy" }, { "ad", "economy" }, { "branch", "economy" }, { "rich", "economy" }, { "work", "economy" },
            { "faith", "faith" }, { "preach", "faith" }, { "heresy", "faith" }, { "sermon", "faith" },
            { "festival", "culture" }, { "fireworks", "culture" }, { "meme", "culture" }, { "trend_start", "culture" }, { "challenge", "culture" }, { "city_anniv", "culture" }, { "wonder", "culture" },
            { "famine", "disaster" }, { "blackout", "disaster" }, { "pollution", "disaster" }, { "hunger", "disaster" }, { "event", "world" }, { "erasure", "world" }, { "kingdom_gone", "world" }, { "kingdom_new", "world" },
            { "obituary", "people" }, { "rip", "people" }, { "birthday", "people" }, { "love", "people" }, { "gossip", "people" }, { "breakup", "people" }, { "scandal", "people" }
        };

        public static string Of(Kingdom k)
        {
            if (k == null || !k.isAlive()) return "crier";
            if (Internet.Exists(k)) return "social";
            return ByEra(Research.Era(k));
        }

        public static string ByEra(int era)
        {
            if (era <= 4) return "crier";
            if (era <= 7) return "scroll";
            if (era <= 9) return "gazette";
            if (era <= 11) return "paper";
            return "broadcast";
        }

        public static int Rank(string m)
        {
            int i = System.Array.IndexOf(Order, m);
            return i < 0 ? 5 : i;
        }

        public static string Best()
        {
            string best = "crier";
            foreach (Kingdom k in G.Kingdoms())
            {
                string m = Of(k);
                if (Rank(m) > Rank(best)) best = m;
            }
            return best;
        }

        public static bool Modern(string m)
        {
            return m == "social" || m == "broadcast";
        }

        public static string Register(string m)
        {
            return m == "social" ? "net" : m == "broadcast" ? "tv" : "old";
        }

        public static string TopicOf(string kind)
        {
            string t;
            if (string.IsNullOrEmpty(kind)) return "people";
            if (KindTopic.TryGetValue(kind, out t)) return t;
            if (kind.StartsWith("net_")) return "people";
            return "people";
        }

        public static string Outlet(Kingdom k, string med)
        {
            string n = G.Name(k);
            return Txt.F("net_outlet_" + (string.IsNullOrEmpty(med) ? Of(k) : med), n);
        }

        public static string OutletHandle(Kingdom k)
        {
            return "@" + Names.Root(G.Name(k)).ToLowerInvariant() + "_news";
        }

        public static string DefaultImage(string kind)
        {
            switch (TopicOf(kind))
            {
                case "war": return "battle";
                case "diplomacy": return "doc_treaty";
                case "politics": return kind == "election" || kind == "poll" ? "doc_ballot" : "portrait";
                case "economy": return "doc_chart";
                case "culture": return "festival";
                case "world": return "map";
                case "disaster": return "city";
                default: return "city";
            }
        }

        public static string PersonImage(string kind, bool online)
        {
            switch (kind)
            {
                case "meme": return "meme";
                case "challenge": return online ? "meme" : "city";
                case "live_war": case "soldier": return "battle";
                case "love": case "brag": case "rich": case "birthday": case "daily": case "food": case "travel": case "collab":
                    return G.Chance(online ? 0.55f : 0.25f) ? "portrait" : "city";
                case "stock_up": case "stock_down": case "crypto_up": case "crypto_down": return "doc_chart";
                default: return "city";
            }
        }
    }
}
