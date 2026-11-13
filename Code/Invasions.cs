using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Invasions
    {
        private const string KLast = "civ_inv_y";
        private static readonly string[][] ByScale =
        {
            new[] { "landing" },
            new[] { "pirates", "miner_revolt", "rogue_ai" },
            new[] { "raiders", "rival_settlers", "replicators" },
            new[] { "raiders", "rival_settlers", "replicators" },
            new[] { "expansionists", "berserkers", "precursor_guardians" },
            new[] { "expansionists", "berserkers", "precursor_guardians" },
            new[] { "devourer_swarm", "extragalactic_fleet" },
            new[] { "devourer_swarm", "extragalactic_fleet" },
            new[] { "dark_forest", "vacuum_entities", "echoes" },
            new[] { "dark_forest", "vacuum_entities", "echoes" },
        };

        public static bool On
        {
            get { return Cosmos.St != null && Cosmos.St.Invasions && (CivMod.C == null || CivMod.C.CosmicInvasions); }
        }

        public static void Year(int yrs)
        {
            if (!On) return;
            CosState st = Cosmos.St;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv() || st.Threats.Any(t => t.King == Cosmos.Key(k))) continue;
                int era = Research.Era(k);
                if (era < 13) continue;
                if (G.Year - Store.Int(k.data, KLast, -9999) < 60) continue;
                int scale = CosmosSim.CachedScale(k);
                if (G.Year - Store.Int(k.data, "civ_cos_scale_y", -9999) < 30) continue;
                bool spacefaring = era >= Research.PlanetEras && Cosmos.Of(k).Any();
                if (!spacefaring && Space.Launches(k) == 0) continue;
                if (!G.Chance(0.012f * yrs)) continue;
                Plan(k, spacefaring ? scale : 0);
            }
            foreach (Threat t in st.Threats.ToList()) Step(t);
        }

        internal static void Plan(Kingdom k, int scale)
        {
            string[] kinds = ByScale[Math.Max(0, Math.Min(ByScale.Length - 1, scale))];
            string kind = kinds[UnityEngine.Random.Range(0, kinds.Length)];
            string target = Cosmos.St.Home;
            if (kind != "landing")
            {
                List<Polity> mine = Cosmos.Of(k).Where(p => p.Grace <= 0).ToList();
                if (mine.Count == 0) return;
                target = mine[UnityEngine.Random.Range(0, mine.Count)].Seat;
            }
            int warn = G.Year + UnityEngine.Random.Range(8, 25);
            var t = new Threat
            {
                King = Cosmos.Key(k), Kind = kind, Target = target, Warn = warn, Scout = warn + UnityEngine.Random.Range(3, 7), Power = CosmosWar.Strength(k) * UnityEngine.Random.Range(0.6f, 1.25f)
            };
            t.Main = t.Scout + UnityEngine.Random.Range(4, 10);
            Cosmos.St.Threats.Add(t);
            Store.Put(k.data, KLast, G.Year);
        }

        private static int Need(string kind)
        {
            switch (kind)
            {
                case "landing":
                case "pirates":
                case "miner_revolt":
                case "rogue_ai": return 1;
                case "raiders":
                case "rival_settlers":
                case "replicators": return 2;
                case "expansionists":
                case "berserkers":
                case "precursor_guardians": return 4;
                case "devourer_swarm":
                case "extragalactic_fleet": return 6;
                default: return 8;
            }
        }

        private static void Step(Threat t)
        {
            if (Cosmos.Remote(t.King)) return;
            Kingdom k = Cosmos.KingOf(t.King);
            if (!G.Ok(k))
            {
                Cosmos.St.Threats.Remove(t);
                return;
            }
            string name = Txt.T("cos_inv_" + t.Kind);
            string place = CosmosGen.Get(t.Target).Name;
            if (!t.Warned && G.Year >= t.Warn - Math.Max(0, Research.Level(k, "cos:detect:") - Need(t.Kind)) * 4)
            {
                t.Warned = true;
                if (Research.Level(k, "cos:detect:") >= Need(t.Kind) - 1 || t.Kind == "landing")
                {
                    Internet.Official(k, "war", Txt.F("cos_news_warning", G.Name(k), name, place), true);
                    Cosmos.Log(Txt.F("cos_log_warning", name, place));
                }
            }
            if (!t.Scouted && G.Year >= t.Scout)
            {
                t.Scouted = true;
                Internet.Official(k, "war", Txt.F("cos_news_scouts", name, place), false);
                CosmosHub.Invasion(k, name, place, true);
            }
            if (G.Year < t.Main) return;
            Cosmos.St.Threats.Remove(t);
            if (t.Kind == "landing")
            {
                Landing(k, t);
                return;
            }
            float def = CosmosWar.Strength(k) * (t.Warned ? 1.25f : 1f);
            bool win = G.Chance(def / Math.Max(0.01f, def + t.Power));
            if (win)
            {
                CosmosHub.Invasion(k, name, place, false);
                CosmosWar.Reward(k, name);
                return;
            }
            Polity colony = Cosmos.OwnerExact(t.Target);
            if (colony != null && colony.King == Cosmos.Key(k))
            {
                colony.Pop *= 0.5f;
                if (colony.Pop < 0.02f || G.Chance(0.4f))
                {
                    Cosmos.St.Polities.Remove(colony);
                    Polity inv = Cosmos.NewPolity(null, "invader", t.Target, name);
                    inv.Species = t.Kind;
                    inv.Era = Math.Min(Db.LastEra, Research.Era(k) + 1);
                    inv.Pop = 0.3f;
                    inv.Color = "#FF4A3A";
                    inv.Met.Add(Cosmos.Key(k));
                    inv.Opinion[Cosmos.Key(k)] = -100f;
                }
                Cosmos.Dirty();
            }
            Chronicle.Log("cos_invasion", k, name, place, G.Name(k));
            foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_mourning", 0.15f);
        }

        private static void Landing(Kingdom k, Threat t)
        {
            City c = k.capital ?? G.Cities(k).FirstOrDefault();
            WorldTile tile = c != null ? G.Tile(c) : null;
            if (tile == null) return;
            int n = Math.Max(3, Math.Min(10, (int)(t.Power / 20f)));
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                WorldTile spot = tile;
                foreach (WorldTile x in tile.getTilesAround(12))
                {
                    if (x != null && x.Type != null && !x.Type.liquid && G.Chance(0.02f))
                    {
                        spot = x;
                        break;
                    }
                }
                if (AssetManager.actor_library.get("UFO") == null) break;
                Actor u = World.world.units.createNewUnit("UFO", spot);
                if (u != null) made++;
            }
            if (made == 0) return;
            Chronicle.Log("cos_invasion", k, Txt.T("cos_inv_landing"), G.Name(c), G.Name(k), null, null, c);
            Internet.Official(k, "war", Txt.F("cos_news_landing", G.Name(c)), true);
            Store.Put(k.data, "civ_landing_y", G.Year);
        }

        public static void Arrive(Voyage v)
        {
        }
    }
}
