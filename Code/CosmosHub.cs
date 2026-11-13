using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class CosmosHub
    {
        public const string Colonized = "cos_colony";
        public const string Departed = "cos_voyage";
        public const string Freed = "cos_independence";
        public const string Scaled = "cos_scale";
        public const string Met = "cos_contact";
        public const string Battle = "cos_battle";
        public const string Built = "cos_mega";
        public const string Invaded = "cos_invasion";
        public const string KardashevUp = "cos_kardashev";

        private static string NodeName(string path)
        {
            CNode n = CosmosGen.Get(path);
            return n != null ? n.Name : "";
        }

        public static void Colony(Kingdom k, Polity p, bool mission)
        {
            string place = NodeName(p.Seat);
            Cosmos.Log(Txt.F("cos_log_colony", G.Ok(k) ? G.Name(k) : p.Name, p.Name, place));
            Hub.Raise(Colonized, k, null, k != null ? k.capital : null, null, p.Id.ToString(), p.Seat);
            if (!G.Ok(k)) return;
            int spread = Cosmos.Spread(Cosmos.St.Home, p.Seat);
            string first = "colony_" + spread;
            if (Cosmos.First(first, k))
            {
                Chronicle.Log("cos_first", k, G.Name(k), Txt.T("cos_first_" + first), place);
                Firsts.Era(k, Research.Era(k));
            }
            else if (G.Chance(0.35f)) Chronicle.Log("cos_colony", k, G.Name(k), p.Name, place, null, null, null, null, false);
            if (Internet.Exists(k) || Research.Era(k) >= 13) Internet.Official(k, "space", Txt.F("cos_news_colony", G.Name(k), p.Name, place), spread >= 2);
            foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_space", spread >= 2 ? 0.25f : 0.1f);
        }

        public static void Voyage(Kingdom k, Voyage v)
        {
            if (!G.Ok(k) || v.Kind != "colony") return;
            Hub.Raise(Departed, k, null, k.capital, null, v.To, v.Kind, v.Arrive - v.Depart);
            if (G.Chance(0.3f) && (Internet.Exists(k) || Research.Era(k) >= 13)) Internet.Official(k, "space", Txt.F("cos_news_voyage", G.Name(k), NodeName(v.To), v.Arrive - v.Depart), false);
        }

        public static void Independence(Kingdom k, Polity p)
        {
            Cosmos.Log(Txt.F("cos_log_free", p.Name, G.Ok(k) ? G.Name(k) : ""));
            Hub.Raise(Freed, k, null, null, null, p.Id.ToString(), p.Seat);
            if (!G.Ok(k)) return;
            Chronicle.Log("cos_independence", k, p.Name, G.Name(k), NodeName(p.Seat));
            Internet.Official(k, "politics", Txt.F("cos_news_free", p.Name, G.Name(k)), true);
            Legitimacy.Shock(k, -0.08f);
        }

        public static void Primitive(Polity p)
        {
            Cosmos.Log(Txt.F("cos_log_primitive", p.Name, NodeName(p.Seat)));
        }

        public static void ScaleUp(Kingdom k, int scale)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, "civ_cos_scale_y", G.Year);
            Cosmos.Log(Txt.F("cos_log_scale", G.Name(k), Txt.T("cos_scale_" + scale)));
            Hub.Raise(Scaled, k, null, k.capital, null, scale.ToString(), "", scale);
            if (Cosmos.First("scale_" + scale, k)) Chronicle.Log("cos_first", k, G.Name(k), Txt.T("cos_scale_" + scale), "");
            Kardashev.Check(k);
        }

        public static void Contact(Kingdom k, Polity alien, string kind)
        {
            Cosmos.Log(Txt.F("cos_log_contact", G.Ok(k) ? G.Name(k) : "", alien.Name, Txt.T("cos_contact_" + kind)));
            Hub.Raise(Met, k, null, null, null, alien.Id.ToString(), kind);
            if (!G.Ok(k)) return;
            Chronicle.Log("cos_contact", k, G.Name(k), alien.Name, Txt.T("cos_contact_" + kind));
            Internet.Official(k, "world", Txt.F("cos_news_contact", G.Name(k), alien.Name, Txt.T("cos_species_" + alien.Species)), true);
            Festivals.Mark(k, "first_contact");
        }

        public static void Fight(Kingdom k, string a, string b, string place, bool won)
        {
            Cosmos.Log(Txt.F("cos_log_battle", a, b, place, won ? a : b));
            if (G.Ok(k)) Chronicle.Log("cos_battle", k, a, b, place);
        }

        public static void Mega(Kingdom k, string id, string place)
        {
            Cosmos.Log(Txt.F("cos_log_mega", G.Ok(k) ? G.Name(k) : "", Txt.T("cos_mega_" + id), place));
            Hub.Raise(Built, k, null, null, null, id, place);
            if (!G.Ok(k)) return;
            Chronicle.Log("cos_mega", k, G.Name(k), Txt.T("cos_mega_" + id), place);
            Internet.Official(k, "space", Txt.F("cos_news_mega", G.Name(k), Txt.T("cos_mega_" + id), place), true);
            if (Cosmos.First("mega_" + id, k)) Chronicle.Log("cos_first", k, G.Name(k), Txt.T("cos_mega_" + id), place);
        }

        public static void Invasion(Kingdom k, string invader, string target, bool start)
        {
            Cosmos.Log(Txt.F(start ? "cos_log_invasion" : "cos_log_repelled", invader, target));
            Hub.Raise(Invaded, k, null, null, null, invader, target, start ? 1f : 0f);
            if (G.Ok(k)) Chronicle.Log(start ? "cos_invasion" : "cos_victory", k, invader, target, G.Name(k));
        }
    }

    internal static class Kardashev
    {
        public static int Type(Kingdom k)
        {
            if (!G.Ok(k)) return 0;
            int era = Research.Era(k);
            if (era >= Research.PlanetEras + 26) return 4;
            if (era >= Research.PlanetEras + 14) return 3;
            if (era >= Research.PlanetEras + 2) return 2;
            if (era >= 14) return 1;
            return 0;
        }

        public static void Check(Kingdom k)
        {
            int t = Type(k);
            int seen = Store.Int(k.data, "civ_kardashev", 0);
            if (t <= seen) return;
            Store.Put(k.data, "civ_kardashev", t);
            Hub.Raise(CosmosHub.KardashevUp, k, null, k.capital, null, t.ToString(), "", t);
            if (Cosmos.First("kardashev_" + t, k)) Chronicle.Log("cos_kardashev", k, G.Name(k), Txt.T("cos_kardashev_" + t), "");
        }
    }
}
