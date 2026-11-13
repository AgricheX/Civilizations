using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Ending
    {
        public static bool On
        {
            get { return Cosmos.St != null && Cosmos.St.Ending && !Cosmos.St.Ended; }
        }

        public static void Check()
        {
            if (!On) return;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv() || !Research.HasUnlock(k, "cos:omega")) continue;
                Run(k, Choose(k));
                return;
            }
        }

        public static string Choose(Kingdom k)
        {
            KProfile p = Profile.Get(k);
            if (p.Gov == "cosmic_theocracy" || p.Ideo == "cosmism" || p.Fam.Contains("theocratic")) return "big_bang";
            if (p.Ideo == "transhumanism" || p.Fam.Contains("collective") || p.Fam.Contains("technocratic")) return "transcendence";
            return "heat_death";
        }

        internal static void Run(Kingdom k, string kind)
        {
            CosState st = Cosmos.St;
            st.Ended = true;
            string who = G.Name(k);
            st.Legends.Add(who + "|" + Cosmos.Now + "|" + kind);
            Chronicle.Log("cos_ending", k, who, Txt.T("cos_end_" + kind), "");
            Notify.Show(Txt.F("cos_end_notice_" + kind, who));
            Cosmos.Log(Txt.F("cos_log_end", who, Txt.T("cos_end_" + kind)));
            Hub.Raise(Hub.Event, k, null, k.capital, null, "cos_end_" + kind, kind);
            switch (kind)
            {
                case "transcendence":
                    Transcend(k);
                    break;
                case "heat_death":
                    HeatDeath();
                    Universe.Broadcast("heat_death");
                    break;
                default:
                    BigBang(who);
                    Universe.Broadcast("big_bang");
                    break;
            }
        }

        public static void ApplyLocal(string kind)
        {
            if (kind == "heat_death") LocalHeatDeath();
            else if (kind == "big_bang") LocalBigBang();
            else return;
            Notify.Show(Txt.T("cos_end_remote_" + kind));
            Chronicle.Log("cos_ending", null, Txt.T("cos_world_far"), Txt.T("cos_end_" + kind), "");
        }

        private static void Transcend(Kingdom k)
        {
            foreach (Polity p in Cosmos.Of(k).ToList()) Cosmos.St.Polities.Remove(p);
            Cosmos.Dirty();
            foreach (City c in G.Cities(k))
            {
                foreach (Actor a in c.units.ToList())
                {
                    if (!G.Ok(a) || !G.Chance(0.85f)) continue;
                    ActionLibrary.removeUnit(a);
                }
            }
        }

        private static void HeatDeath()
        {
            Cosmos.St.Polities.Clear();
            Cosmos.St.Voyages.Clear();
            Cosmos.St.Megas.Clear();
            Cosmos.St.Threats.Clear();
            Cosmos.Dirty();
            LocalHeatDeath();
        }

        private static void LocalHeatDeath()
        {
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                var set = new TechSet();
                foreach (TechDef t in Research.Known(k).All()) if (t.Era < Research.PlanetEras) set.Add(t.Index);
                Research.SetAll(k, set);
                Profile.Invalidate(k);
            }
        }

        private static void BigBang(string legend)
        {
            CosState st = Cosmos.St;
            st.Polities.Clear();
            st.Voyages.Clear();
            st.Megas.Clear();
            st.Threats.Clear();
            st.Seeded.Clear();
            st.Ruins.Clear();
            st.Dead.Clear();
            st.Council.Clear();
            st.Firsts.Clear();
            st.Seed = unchecked(st.Seed * 31 + 7);
            st.Origin = "random";
            st.HomeBodies = 0;
            if (Cosmos.Shared) Universe.Rebirth();
            else
            {
                CosmosGen.Setup(st.Seed, "", 0);
                st.Home = CosmosGen.HomePath;
            }
            st.Ended = false;
            Cosmos.Dirty();
            LocalBigBang();
        }

        private static void LocalBigBang()
        {
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                Research.SetAll(k, new TechSet());
                Store.Put(k.data, Research.KFloor, 0);
                Store.Put(k.data, "civ_cos_scale_seen", 0);
                Profile.Invalidate(k);
            }
            CosmosSim.Clear();
        }
    }
}
