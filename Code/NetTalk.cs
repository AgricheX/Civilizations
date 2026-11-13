using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static partial class Internet
    {
        public static void Engage(NetPost p, Kingdom k, float reach)
        {
            if (p != null && k != null) React(p, k, reach);
        }

        public static void Bump()
        {
            Version++;
        }

        private static bool CanHear(Kingdom listener, Kingdom about, string med)
        {
            if (listener == null || about == null || listener == about) return false;
            if (!Fog.Knows(listener, about)) return false;
            if (med == "social") return Exists(listener);
            if (med == "broadcast") return NetMedia.Rank(NetMedia.Of(listener)) >= 4;
            if (listener.capital == null || about.capital == null) return false;
            return G.Dist(listener.capital, about.capital) < 70f;
        }

        private static void AddComment(NetPost p, Kingdom k)
        {
            Kingdom pk = G.KingdomById(p.Kingdom) ?? k;
            Kingdom src = pk ?? k;
            float foreign = p.Med == "social" ? 0.3f : p.Med == "broadcast" ? 0.15f : 0.06f;
            if (pk != null && G.Chance(foreign))
            {
                Kingdom other = G.Pick(G.Kingdoms().Where(x => CanHear(x, pk, p.Med)).ToList());
                if (other != null) src = other;
            }
            if (p.Med == "social" && RelatedComment(p)) return;
            Actor a = RandomUser(src);
            if (a == null) return;
            string mood = Mood(p, a, pk);
            string reg = NetMedia.Register(p.Med);
            string key = Pick("net_c_" + reg + "_" + mood, "net_c_" + mood, "net_c_" + reg + "_neutral", "net_c_neutral");
            p.Comments.Add(new NetComment { Author = a.getName(), Handle = Names.Handle(a), AuthorId = G.Id(a), Text = Line(key, p.Author, pk != null ? G.Name(pk) : "?", a.kingdom != null ? G.Name(a.kingdom) : "?"), Likes = UnityEngine.Random.Range(0, Math.Max(2, p.Likes / 10)) });
            if (mood == "rival" && pk != null && G.Chance(0.35f)) Flame(p, a, pk, reg);
            while (p.Comments.Count > 14) p.Comments.RemoveAt(0);
        }

        private static bool Trait(Actor a, string t)
        {
            try
            {
                return a.hasTrait(t);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Mood(NetPost p, Actor a, Kingdom pk)
        {
            Kingdom mine = a.kingdom;
            bool own = mine != null && pk != null && mine == pk;
            if (!own && mine != null && pk != null)
            {
                if (Groups.Enemies(mine).Contains(pk)) return "rival";
                bool other = mine.getSpecies() != pk.getSpecies();
                string xeno = Policies.Id(mine, "xeno");
                if (other && xeno == "xenophobia") return "foreign";
                if (other && xeno == "xenophilia" && G.Chance(0.6f)) return "xenofriend";
                if (G.Chance(0.35f)) return "curious";
            }
            if (p.Fake && !p.Checked) return G.Chance(0.55f) ? "believe" : "skeptic";
            if (Trait(a, "psychopath") || Trait(a, "evil")) { if (G.Chance(0.45f)) return "troll"; }
            if (Trait(a, "hotheaded") || Trait(a, "bloodlust")) { if (G.Chance(0.4f)) return "angry"; }
            if (Trait(a, "paranoid") && G.Chance(0.45f)) return "conspiracy";
            if ((Trait(a, "wise") || Trait(a, "genius")) && G.Chance(0.35f)) return "wise";
            if (Trait(a, "greedy") && G.Chance(0.35f)) return "greedy";
            if ((Trait(a, "pacifist") || Trait(a, "peaceful") || Trait(a, "content")) && G.Chance(0.35f)) return "kind";
            if (Trait(a, "stupid") && G.Chance(0.35f)) return "dumb";
            if (Trait(a, "honest") && G.Chance(0.25f)) return "honest";
            Opinion o = Politics.Get(a);
            string mood;
            if ((p.Who == "kingdom" || p.Who == "press") && o != null && own)
            {
                string cur = Policies.Id(pk, "gov");
                if (o.Gov != cur && G.Chance(0.3f)) mood = "oppose";
                else mood = o.Sat > 0.6f ? "agree" : o.Sat < 0.35f ? "disagree" : "neutral";
                if (own && (p.Topic == "war") && o.Sat >= 0.35f && G.Chance(0.5f)) mood = "patriot";
            }
            else if (p.Kind == "scandal" || p.Kind == "apology") mood = G.Chance(0.6f) ? "disagree" : "agree";
            else if (p.Topic == "faith" && a.hasReligion() && G.Chance(0.5f)) mood = "pious";
            else if (G.Chance(0.15f)) mood = "troll";
            else if (G.Chance(0.4f)) mood = "agree";
            else if (G.Chance(0.2f)) mood = "laugh";
            else mood = "neutral";
            return pk != null && own ? NetPress.CommentBias(pk, mood) : mood;
        }

        private static void Flame(NetPost p, Actor rival, Kingdom pk, string reg)
        {
            Actor d = RandomUser(pk);
            if (d == null || d == rival) return;
            int n = UnityEngine.Random.Range(2, 6);
            string them = rival.kingdom != null ? G.Name(rival.kingdom) : "?";
            for (int i = 0; i < n; i++)
            {
                Actor w = i % 2 == 0 ? d : rival;
                string key = Pick("net_c_" + reg + "_flame_" + (i % 2 == 0 ? "a" : "b"), "net_c_flame_" + (i % 2 == 0 ? "a" : "b"));
                p.Comments.Add(new NetComment { Author = w.getName(), Handle = Names.Handle(w), AuthorId = G.Id(w), Text = Line(key, G.Name(pk), them), Likes = UnityEngine.Random.Range(0, 20) });
            }
            p.Flame = true;
            if (p.Med == "social" && p.Tag.Length == 0) p.Tag = "#" + Root(G.Name(pk)) + "Vs" + Root(them);
        }
    }
}
