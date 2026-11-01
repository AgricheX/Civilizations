using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class Space
    {
        public const string KSats = "civ_sats";
        public const string KLaunches = "civ_launches";
        public const string KPrestige = "civ_space_prestige";
        public const string KFirsts = "civ_space_firsts";
        public static readonly string[] Milestones = { "satellite", "crewed", "moon", "station", "mars", "elevator" };

        public static int Satellites(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KSats, 0) : 0;
        }

        public static int Launches(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KLaunches, 0) : 0;
        }

        public static float Prestige(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KPrestige, 0f) : 0f;
        }

        public static Dictionary<string, string> Firsts()
        {
            var m = new Dictionary<string, string>();
            SaveCustomData d = Store.World();
            if (d == null) return m;
            foreach (string p in Store.Str(d, KFirsts).Split(';'))
            {
                int i = p.IndexOf('=');
                if (i > 0) m[p.Substring(0, i)] = p.Substring(i + 1);
            }
            return m;
        }

        private static void SaveFirsts(Dictionary<string, string> m)
        {
            SaveCustomData d = Store.World();
            if (d == null) return;
            var parts = new List<string>();
            foreach (var kv in m) parts.Add(kv.Key + "=" + kv.Value);
            Store.Put(d, KFirsts, string.Join(";", parts));
        }

        public static City Pad(Kingdom k)
        {
            foreach (City c in G.Cities(k)) if (c.countBuildingsType("type_civ_launchpad") > 0) return c;
            return null;
        }

        public static bool WantsPad(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !Research.HasFeature(c.kingdom, "rocket")) return false;
            if (Pad(c.kingdom) != null) return false;
            if (c.isCapitalCity()) return true;
            foreach (Company co in Companies.SpaceLaunchers(c.kingdom)) if (co.Hq == G.Id(c)) return true;
            return false;
        }

        public static void Year(Kingdom k, int yrs)
        {
            if (!Research.HasFeature(k, "rocket")) return;
            City pad = Pad(k);
            if (pad == null) return;
            float rate = 0.15f;
            List<Company> launchers = Companies.SpaceLaunchers(k);
            rate += launchers.Count * 0.15f;
            if (Research.Has(k, "reusable_rockets")) rate *= 2f;
            if (!Economy.Has(pad, "civ_fuel", 10) && !Economy.Has(pad, "civ_uranium", 2) && Research.Route(k) != "tesla" && Research.Route(k) != "steam") return;
            int n = 0;
            while (n < 3 && G.Chance(Math.Min(0.9f, rate * yrs)))
            {
                n++;
                rate *= 0.5f;
                string mission = Mission(k);
                Company by = launchers.Count > 0 && G.Chance(0.6f) ? G.Pick(launchers) : null;
                Economy.TakeUpTo(pad, "civ_fuel", 10);
                Kingdom kk = k;
                Transit.Rocket(G.Tile(pad), Research.Route(k), () => Complete(kk, mission, by));
            }
        }

        private static string Mission(Kingdom k)
        {
            Dictionary<string, string> firsts = Firsts();
            if (Research.Has(k, "space_elevator") && !firsts.ContainsKey("elevator")) return "elevator";
            if (Research.Has(k, "mars_mission") && G.Chance(0.3f)) return "mars";
            if (Research.Has(k, "space_station") && G.Chance(0.3f)) return "station";
            if (Research.HasFeature(k, "moon") && G.Chance(0.25f)) return "moon";
            if (Research.HasFeature(k, "crewed") && G.Chance(0.3f)) return "crewed";
            return Research.HasFeature(k, "satellite") ? "satellite" : "test";
        }

        private static void Complete(Kingdom k, string mission, Company by)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KLaunches, Launches(k) + 1);
            float prestige = mission == "test" ? 1f : mission == "satellite" ? 2f : mission == "crewed" ? 6f : mission == "moon" ? 15f : mission == "station" ? 8f : mission == "mars" ? 20f : 25f;
            if (mission == "satellite") Store.Put(k.data, KSats, Satellites(k) + 1);
            Dictionary<string, string> firsts = Firsts();
            bool first = mission != "test" && !firsts.ContainsKey(mission);
            if (first)
            {
                firsts[mission] = G.Id(k);
                SaveFirsts(firsts);
                prestige *= 3f;
                foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_space", 0.3f);
            }
            Store.Put(k.data, KPrestige, Prestige(k) + prestige);
            if (by != null) by.Value += prestige * 20f;
            Internet.OnLaunch(k, mission, first, by);
        }

        public static void Decay(Kingdom k)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KPrestige, Prestige(k) * 0.97f);
            int sats = Satellites(k);
            if (sats > 0 && G.Chance(0.05f)) Store.Put(k.data, KSats, sats - 1);
        }
    }
}
