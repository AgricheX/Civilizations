using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class NetLife
    {
        private const int Budget = 36;

        public static void Offline(List<Kingdom> online)
        {
            var set = new HashSet<Kingdom>(online);
            var list = G.Kingdoms().Where(k => !set.Contains(k) && k.isCiv()).OrderBy(k => UnityEngine.Random.value).ToList();
            int left = Budget;
            foreach (Kingdom k in list)
            {
                if (left <= 0) break;
                if (!G.Chance(0.65f)) continue;
                left -= Post(k);
            }
        }

        private static string King(Kingdom k)
        {
            return k.hasKing() ? k.king.getName() : G.Name(k);
        }

        private static int Post(Kingdom k)
        {
            string med = NetMedia.Of(k);
            City c = G.Pick(G.Cities(k));
            string city = c != null ? G.Name(c) : G.Name(k);
            string snap = c != null && G.Tile(c) != null ? G.Tile(c).x + "," + G.Tile(c).y : "";
            float r = UnityEngine.Random.value;
            if (r < 0.32f)
            {
                NetPost p = NetHub.Outlet(k, "news", Internet.Line("net_off_news_" + med, city, King(k), G.Name(k)), false);
                if (p != null)
                {
                    p.Topic = "people";
                    if (G.Chance(0.4f))
                    {
                        p.Img = "city";
                        p.Snap = snap;
                    }
                }
                return 1;
            }
            if (r < 0.62f)
            {
                Actor a = Internet.RandomUser(k);
                if (a == null) return 0;
                Opinion o = Politics.Get(a);
                string mood = o == null ? "daily" : o.Sat < 0.35f ? "complain" : o.Sat > 0.7f ? "praise" : "daily";
                if (a.hasReligion() && G.Chance(0.2f)) mood = "faith";
                NetPost p = Internet.Person(a, mood == "complain" ? "opinion" : mood == "faith" ? "faith" : "daily", Internet.Line("net_off_voice_" + mood + "_" + NetMedia.Register(med), city, King(k), G.Name(k)), 0.6f);
                if (p != null && mood == "faith") p.Topic = "faith";
                return 1;
            }
            if (r < 0.8f)
            {
                NetPost p = Internet.Official(k, "decree", Internet.Line("net_off_decree_" + med, King(k), G.Name(k), city), false);
                if (p != null)
                {
                    p.Topic = "politics";
                    p.Img = G.Chance(0.4f) ? "doc_decree" : "";
                    p.Snap = snap;
                }
                return 1;
            }
            if (r < 0.9f)
            {
                Religion rel = k.getReligion();
                if (rel == null || rel.data == null) return 0;
                NetPost p = Internet.Make("religion", rel.data.id.ToString(), rel.data.name, "@" + Names.Root(rel.data.name).ToLowerInvariant(), k, "sermon", Internet.Line("net_off_sermon_" + NetMedia.Register(med), rel.data.name, city));
                p.Verified = true;
                p.Role = "faith";
                p.Topic = "faith";
                Internet.Add(p);
                Internet.Engage(p, k, 0.8f);
                return 1;
            }
            NetPost m = NetHub.Outlet(k, "market", Internet.Line("net_off_market_" + NetMedia.Register(med), city, G.Name(k)), false);
            if (m != null) m.Topic = "economy";
            return 1;
        }
    }
}
