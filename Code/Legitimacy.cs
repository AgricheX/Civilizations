using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Legitimacy
    {
        public const string KLegit = "civ_legit";
        private const string KWins = "civ_wins";
        private const string KCrisis = "civ_crisis_y";

        public static float Get(Kingdom k)
        {
            if (!G.Ok(k)) return 0.5f;
            return Store.Flt(k.data, KLegit, 0.6f);
        }

        public static void Shock(Kingdom k, float delta)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KLegit, G.Clamp(Get(k) + delta, 0f, 1f));
        }

        public static string Source(Kingdom k)
        {
            KProfile p = Profile.Get(k);
            if (p.Fam.Contains("tribal")) return "tribal";
            if (p.Fam.Contains("theocratic") && Coherence.Is(p.Gov, "theocratic")) return "faith";
            if (p.Gov == "military_junta") return "victories";
            if (p.Gov == "single_party_state") return "ideology";
            if (p.Gov == "corporatocracy") return "companies";
            if (p.Gov == "technocracy") return "progress";
            if (Coherence.Is(p.Gov, "monarchy")) return "dynasty";
            return "elections";
        }

        public static void Register()
        {
            Hub.On(Hub.WarEnded, e =>
            {
                if (e.Id == "draw" || e.Id == "Peace" || e.Id == "Nobody" || e.Id == "Merged") return;
                if (G.Ok(e.K)) Store.Put(e.K.data, KWins, Store.Flt(e.K.data, KWins, 0f) + 1f);
                if (G.Ok(e.O)) Store.Put(e.O.data, KWins, Store.Flt(e.O.data, KWins, 0f) - 1f);
            });
        }

        private static float Target(Kingdom k, KProfile p)
        {
            float wins = Store.Flt(k.data, KWins, 0f);
            float t;
            switch (Source(k))
            {
                case "tribal":
                    t = 0.6f + (k.hasKing() ? 0.1f : -0.1f);
                    break;
                case "faith":
                    int same = 0;
                    Religion r = k.getReligion();
                    List<City> cs = G.Cities(k);
                    foreach (City c in cs) if (r != null && c.getReligion() == r) same++;
                    t = 0.3f + 0.55f * (cs.Count > 0 ? same / (float)cs.Count : 0f);
                    break;
                case "victories":
                    t = 0.4f + G.Clamp(wins * 0.08f, -0.3f, 0.35f) + (p.AtWar ? 0.05f : 0f);
                    break;
                case "ideology":
                    t = 0.35f + Politics.Support(k, "ideo") * 0.45f;
                    break;
                case "companies":
                    t = 0.35f + Math.Min(0.4f, Companies.Of(k).Count * 0.04f);
                    break;
                case "progress":
                    t = 0.45f + Math.Min(0.3f, Research.Progress(k) > 0f ? 0.15f : 0f) + (p.Era >= 13 ? 0.1f : 0f);
                    break;
                case "dynasty":
                    t = 0.5f + (k.hasKing() ? 0.12f : -0.25f) + G.Clamp(wins * 0.05f, -0.2f, 0.2f);
                    if (k.hasKing() && k.king.clan != null && k.king.clan.units.Count > 2) t += 0.08f;
                    break;
                default:
                    t = 0.5f + (Politics.Support(k, "gov") - 0.5f) * 0.6f + (p.Gold > p.Cities * 60 ? 0.1f : -0.05f);
                    break;
            }
            foreach (City c in G.Cities(k))
            {
                if (Economy.IsFamine(c))
                {
                    t -= 0.12f;
                    break;
                }
            }
            t += (p.Stability - 0.5f) * 0.3f;
            return G.Clamp(t, 0f, 1f);
        }

        public static void Year(Kingdom k, int yrs)
        {
            KProfile p = Profile.Get(k);
            float cur = Get(k);
            float target = Target(k, p);
            float next = cur + (target - cur) * Math.Min(1f, 0.25f * yrs);
            Store.Put(k.data, KLegit, next);
            Store.Put(k.data, KWins, Store.Flt(k.data, KWins, 0f) * (float)Math.Pow(0.92, yrs));
            if (cur >= 0.3f && next < 0.3f) Hub.Raise(Hub.Legitimacy, k, null, k.capital, null, "crisis", Source(k), next);
            if (next >= 0.3f) return;
            if (G.Year - Store.Int(k.data, KCrisis, -999) < 30) return;
            float danger = (0.3f - next) * 2f * yrs;
            if (!G.Chance(danger)) return;
            Store.Put(k.data, KCrisis, G.Year);
            Crisis(k, p, next);
        }

        public static void Crisis(Kingdom k, KProfile p, float legit)
        {
            bool authoritarian = p.Fam.Contains("authoritarian") || p.Fam.Contains("monarchy");
            PolicyDef junta = Db.Policy("military_junta");
            if (authoritarian && p.Gov != "military_junta" && junta != null && Policies.Unlocked(k, junta) && G.Chance(0.5f))
            {
                Policies.Set(k, "gov", "military_junta", "coup");
                Store.Put(k.data, KLegit, 0.45f);
                Hub.Raise(Hub.Coup, k, null, k.capital, null, "military_junta", p.Gov);
                return;
            }
            if (p.Cities >= 4 && legit < 0.2f)
            {
                CivilWar(k, p);
                return;
            }
            Secession(k, p);
        }

        public static void CivilWar(Kingdom k, KProfile p)
        {
            City cap = k.capital;
            List<City> cities = G.Cities(k).Where(c => c != cap && c.hasLeader() && G.Ok(c.leader)).ToList();
            if (cities.Count == 0) return;
            WorldTile ct = G.Tile(cap);
            cities.Sort((a, b) => (ct == null ? 0f : G.Dist(G.Tile(b), ct)).CompareTo(ct == null ? 0f : G.Dist(G.Tile(a), ct)));
            City seed = cities[0];
            Kingdom rebel = seed.makeOwnKingdom(seed.leader, true);
            if (!G.Ok(rebel)) return;
            int take = Math.Max(0, p.Cities / 3 - 1);
            WorldTile st = G.Tile(seed);
            var near = G.Cities(k).Where(c => c != cap && c != seed).OrderBy(c => st == null ? 0f : G.Dist(G.Tile(c), st)).Take(take).ToList();
            foreach (City c in near) c.joinAnotherKingdom(rebel, false, true);
            string faction = p.Fam.Contains("monarchy") ? "pretender" : p.Fam.Contains("democratic") ? "federalists" : p.Fam.Contains("revolutionary") ? "counter_revolution" : "warlords";
            World.world.diplomacy.startWar(k, rebel, WarTypeLibrary.rebellion);
            Hub.Raise(Hub.CivilWar, k, rebel, seed, seed.leader, faction, p.Gov);
        }

        public static void Secession(Kingdom k, KProfile p)
        {
            City cap = k.capital;
            Culture kc = k.getCulture();
            foreach (City c in G.Cities(k))
            {
                if (c == cap || !c.hasLeader() || !G.Ok(c.leader)) continue;
                bool foreign = (kc != null && c.getCulture() != null && c.getCulture() != kc) || Xeno.ForeignShare(c) > 0.5f;
                if (!foreign) continue;
                Kingdom rebel = c.makeOwnKingdom(c.leader, true);
                if (!G.Ok(rebel)) return;
                if (p.Era >= 10 && !p.Fam.Contains("authoritarian")) Hub.Raise(Hub.Independence, rebel, k, c, c.leader, "secession", "peaceful");
                else
                {
                    World.world.diplomacy.startWar(k, rebel, WarTypeLibrary.rebellion);
                    Hub.Raise(Hub.Independence, rebel, k, c, c.leader, "secession", "war");
                }
                return;
            }
        }
    }
}
