using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class NetPress
    {
        private static readonly HashSet<string> Bad = new HashSet<string>
        {
            "war_lost", "city_lost", "famine", "blackout", "coup", "legitimacy", "tech_lost", "regression", "bankrupt", "inflation", "hacked", "leak", "scandal",
            "civil_war", "dependent", "independence_lost", "treaty_broken", "hunger", "pollution", "rebellion", "revolt", "sack_victim", "pressure_no", "nuke_hit"
        };

        private static readonly HashSet<string> Critical = new HashSet<string>
        {
            "opinion", "scandal", "leak", "pressure", "fake", "hunger", "blackout", "pollution", "gossip", "hacker", "free_critic", "heresy"
        };

        public static string Of(Kingdom k)
        {
            if (!G.Ok(k)) return "free";
            KProfile p = Profile.Get(k);
            if (p.Fam.Contains("tribal")) return "tribal";
            if (p.Fam.Contains("theocratic")) return "sacred";
            if (p.Fam.Contains("corporate")) return "sponsored";
            if (p.Fam.Contains("authoritarian") && (p.Fam.Contains("revolutionary") || p.Fam.Contains("military") || p.Fam.Contains("technocratic"))) return "state";
            if (p.Fam.Contains("democratic") || p.Fam.Contains("republic")) return "free";
            if (p.Fam.Contains("monarchy")) return "royal";
            return p.Fam.Contains("authoritarian") ? "state" : "free";
        }

        public static bool IsBad(string kind)
        {
            return Bad.Contains(kind);
        }

        public static bool Hush(Kingdom k, string kind)
        {
            if (!Bad.Contains(kind)) return false;
            string press = Of(k);
            if (press == "state") return G.Chance(0.3f);
            if (press == "royal") return G.Chance(0.12f);
            return false;
        }

        public static void Shape(NetPost p, Kingdom k)
        {
            if (p == null || !G.Ok(k)) return;
            string press = Of(k);
            if (p.Who == "kingdom" || p.Who == "press")
            {
                if (!Bad.Contains(p.Kind)) return;
                string frame = press == "state" ? "net_prop_frame" : press == "royal" ? "net_royal_frame" : press == "sacred" ? "net_sacred_frame" : "";
                if (frame.Length == 0) return;
                if (press == "royal" && !G.Chance(0.5f)) return;
                p.Text = Internet.Line(frame + "_" + NetMedia.Register(p.Med), p.Text);
                p.Propaganda = press == "state";
                return;
            }
            if (p.Who != "person") return;
            if (Critical.Contains(p.Kind) || p.Kind == "opinion")
            {
                if (press == "state" && G.Chance(0.55f)) p.Censored = true;
                else if (press == "royal" && G.Chance(0.2f)) p.Censored = true;
                else if (press == "sacred" && (p.Kind == "heresy" || p.Kind == "scandal") && G.Chance(0.35f)) p.Censored = true;
            }
            if (press == "sponsored" && p.Med == "social" && G.Chance(0.12f))
            {
                Company c = G.Pick(Companies.Of(k));
                if (c != null) p.Sponsor = c.Name;
            }
        }

        public static string CommentBias(Kingdom k, string mood)
        {
            string press = Of(k);
            if ((press == "state" || press == "royal") && (mood == "disagree" || mood == "oppose") && G.Chance(press == "state" ? 0.6f : 0.3f)) return "afraid";
            if (press == "sacred" && G.Chance(0.15f)) return "pious";
            return mood;
        }

        public static void Year(Kingdom k)
        {
            if (!G.Ok(k)) return;
            string press = Of(k);
            switch (press)
            {
                case "free":
                    if (G.Chance(0.35f)) Critic(k);
                    break;
                case "state":
                    if (G.Chance(0.45f)) StateMedia(k);
                    break;
                case "sacred":
                    if (G.Chance(0.3f)) Heresy(k);
                    break;
                case "sponsored":
                    if (G.Chance(0.6f)) Ad(k);
                    break;
                case "royal":
                    if (G.Chance(0.3f)) Court(k);
                    break;
            }
        }

        private static string Leader(Kingdom k)
        {
            return k.hasKing() ? k.king.getName() : G.Name(k);
        }

        private static void Critic(Kingdom k)
        {
            Actor a = Internet.RandomUser(k);
            if (a == null) return;
            NetPost p = Internet.Person(a, "free_critic", Internet.Line("net_free_critic", Leader(k), G.Name(k)), 1.6f);
            if (p != null) p.Topic = "politics";
        }

        private static void StateMedia(Kingdom k)
        {
            NetPost p = NetHub.Outlet(k, "state_praise", Internet.Line("net_state_praise_" + NetMedia.Register(NetMedia.Of(k)), Leader(k), G.Name(k)), false);
            if (p == null) return;
            p.Propaganda = true;
            p.Role = "state_media";
            p.Img = "portrait";
            p.Topic = "politics";
        }

        private static void Court(Kingdom k)
        {
            NetPost p = NetHub.Outlet(k, "court", Internet.Line("net_court_" + NetMedia.Register(NetMedia.Of(k)), Leader(k), G.Name(k)), false);
            if (p != null) p.Topic = "politics";
        }

        private static void Heresy(Kingdom k)
        {
            Religion r = k.getReligion();
            string faith = r != null && r.data != null ? r.data.name : G.Name(k);
            Actor a = Internet.RandomUser(k);
            Actor b = Internet.RandomUser(k);
            if (a == null || b == null || a == b) return;
            NetPost p = Internet.Person(a, "heresy", Internet.Line("net_heresy", faith), 1.8f);
            if (p == null) return;
            p.Topic = "faith";
            p.Flame = true;
            int n = UnityEngine.Random.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                Actor w = i % 2 == 0 ? b : a;
                p.Comments.Add(new NetComment { Author = w.getName(), Handle = Names.Handle(w), AuthorId = G.Id(w), Text = Internet.Line(i % 2 == 0 ? "net_heresy_reply_a" : "net_heresy_reply_b", faith), Likes = UnityEngine.Random.Range(0, 30) });
            }
        }

        private static void Ad(Kingdom k)
        {
            Company c = G.Pick(Companies.Of(k));
            if (c == null) return;
            string key = Internet.HasLine("net_ad_" + c.Sector) ? "net_ad_" + c.Sector : "net_ad";
            NetPost p = Internet.Corporate(c, "ad", Internet.Line(key, c.Name, Txt.T("civ_sector_" + c.Sector)));
            if (p == null) return;
            p.Sponsor = c.Name;
            p.Topic = "economy";
        }
    }
}
