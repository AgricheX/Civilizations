using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class CosmosWar
    {
        private static readonly string[] Weapons = { "rkv", "nova_bomb", "star_killer", "quasar_cannon", "vacuum_decay" };

        public static float Strength(Kingdom k)
        {
            if (!G.Ok(k)) return 0f;
            int fleet = Math.Max(0, Research.Level(k, "cos:fleet:"));
            float pop = Cosmos.Of(k).Sum(p => p.Pop);
            float era = Math.Max(1f, Research.Era(k) - 12f);
            float s = (1f + fleet) * (1f + fleet) * (1f + (float)Math.Log(1.0 + pop)) * era;
            s *= Bonus.Mult(k, "attack") * 0.5f + Bonus.Mult(k, "naval") * 0.5f;
            return s + Megastructures.Shield(k) + Spacecraft.Strength(k);
        }

        public static float Strength(Polity p)
        {
            if (p == null) return 0f;
            int scale = Research.ScaleOf(p.Era);
            float era = Math.Max(1f, p.Era - 12f);
            return (1f + scale * 1.2f) * (1f + scale * 1.2f) * (1f + (float)Math.Log(1.0 + p.Pop)) * era * (p.Kind == "invader" ? 1.4f : 1f);
        }

        public static string Weapon(Kingdom k)
        {
            string best = null;
            foreach (string w in Weapons) if (Research.HasUnlock(k, "cos:weapon:" + w)) best = w;
            return best;
        }

        private static bool Hostile(Polity a, Kingdom k)
        {
            float op;
            return a.Kind == "invader" || a.Met.Contains(Cosmos.Key(k)) && a.Opinion.TryGetValue(Cosmos.Key(k), out op) && op < -60f;
        }

        public static void Year(int yrs)
        {
            CosState st = Cosmos.St;
            if (st.Battles.Count > 60) st.Battles.RemoveRange(0, st.Battles.Count - 60);
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                List<Polity> mine = Cosmos.Of(k).ToList();
                foreach (Kingdom o in G.Kingdoms())
                {
                    if (o == k || !o.isCiv() || k.data.id > o.data.id || !World.world.wars.isInWarWith(k, o)) continue;
                    if (!mine.Any() && !Cosmos.Of(o).Any()) continue;
                    if (!G.Chance(0.35f * yrs)) continue;
                    Clash(k, o);
                }
                if (!mine.Any()) continue;
                foreach (Polity a in st.Polities.ToList())
                {
                    if (!Cosmos.Foreign(a) || !Hostile(a, k)) continue;
                    if (Cosmos.Remote(a.King) && Universe.Frozen(Cosmos.TagOf(a.King))) continue;
                    if (G.Chance(0.25f * yrs)) Clash(k, a);
                }
            }
        }

        private static Polity Front(IEnumerable<Polity> list, string near)
        {
            Polity best = null;
            int gap = int.MaxValue;
            foreach (Polity p in list)
            {
                if (p.Kind == "outpost" && p.Pop < 0.0005f) continue;
                int g = near != null ? Cosmos.Spread(near, p.Seat) : 0;
                if (g < gap)
                {
                    gap = g;
                    best = p;
                }
            }
            return best;
        }

        private static void Clash(Kingdom a, Kingdom b)
        {
            float sa = Strength(a) * Spacecraft.Edge(a, b), sb = Strength(b) * Spacecraft.Edge(b, a);
            bool aWins = G.Chance(sa / Math.Max(0.01f, sa + sb));
            Kingdom win = aWins ? a : b, lose = aWins ? b : a;
            Polity target = Front(Cosmos.Of(lose), Cosmos.Of(win).Select(p => p.Seat).FirstOrDefault() ?? Cosmos.St.Home);
            if (target == null) return;
            Record(target.Seat, G.Name(a), G.Name(b), G.Name(win));
            Megastructures.War(win, lose, target.Seat);
            if (!Strike(win, lose, target))
            {
                target.King = Cosmos.Key(win);
                target.Loyalty = 0.4f;
                target.Color = G.Hex(win);
                target.Pop *= 0.7f;
                Cosmos.Dirty();
            }
            CosmosHub.Fight(win, G.Name(a), G.Name(b), CosmosGen.Get(target.Seat).Name, true);
        }

        private static void Clash(Kingdom k, Polity a)
        {
            float sk = Strength(k), sa = Strength(a);
            bool kWins = G.Chance(sk / Math.Max(0.01f, sk + sa));
            string place;
            if (kWins)
            {
                place = CosmosGen.Get(a.Seat).Name;
                a.Pop *= 0.75f;
                if (a.Nodes.Count > 1)
                {
                    string lost = a.Nodes[a.Nodes.Count - 1];
                    a.Nodes.Remove(lost);
                    Polity p = Cosmos.NewPolity(k, "colony", lost, CosmosSim.ColonyName(k, CosmosGen.Get(lost)));
                    p.Pop = 0.01f;
                }
                else if (a.Kind == "invader" && a.Pop < 0.2f)
                {
                    Cosmos.St.Polities.Remove(a);
                    Reward(k, a.Name);
                }
                Cosmos.Dirty();
            }
            else
            {
                Polity target = Front(Cosmos.Of(k), a.Seat);
                if (target == null) return;
                place = CosmosGen.Get(target.Seat).Name;
                target.Pop *= 0.5f;
                Megastructures.Raid(k, target.Seat);
                if (target.Pop < 0.01f || a.Kind == "invader")
                {
                    Cosmos.St.Polities.Remove(target);
                    if (!a.Nodes.Contains(target.Seat)) a.Nodes.Add(target.Seat);
                }
                Cosmos.Dirty();
            }
            Record(a.Seat, G.Name(k), a.Name, kWins ? G.Name(k) : a.Name);
            CosmosHub.Fight(k, G.Name(k), a.Name, place, kWins);
        }

        private static bool Strike(Kingdom win, Kingdom lose, Polity target)
        {
            string w = Weapon(win);
            if (w == null || target.Seat == Cosmos.St.Home) return false;
            KProfile p = Profile.Get(win);
            float chance = 0.04f;
            if (p.Fam.Contains("military") || p.Fam.Contains("authoritarian")) chance *= 2f;
            if (p.Ideo == "xenounity" || p.Fam.Contains("liberal")) chance *= 0.5f;
            if (Weapon(lose) != null) chance *= 0.2f;
            if (Orgs.Banned(win, "atomic_bomb")) chance *= 0.3f;
            if (!G.Chance(chance)) return false;
            Cosmos.St.Polities.Remove(target);
            if (!Cosmos.St.Dead.Contains(target.Seat)) Cosmos.St.Dead.Add(target.Seat);
            Cosmos.Dirty();
            string place = CosmosGen.Get(target.Seat).Name;
            Cosmos.Log(Txt.F("cos_log_weapon", G.Name(win), Txt.T("cos_weapon_" + w), place));
            Chronicle.Log("cos_battle", win, G.Name(win), Txt.T("cos_weapon_" + w), place);
            Internet.Official(win, "war", Txt.F("cos_news_weapon", G.Name(win), Txt.T("cos_weapon_" + w), place), true);
            Hub.Raise(Hub.NukeDetonated, win, lose, null, null, w, target.Seat);
            if (Weapon(lose) != null && G.Chance(0.5f))
            {
                Polity back = Front(Cosmos.Of(win), target.Seat);
                if (back != null && back.Seat != Cosmos.St.Home)
                {
                    Cosmos.St.Polities.Remove(back);
                    Cosmos.St.Dead.Add(back.Seat);
                    Chronicle.Log("cos_battle", lose, G.Name(lose), Txt.T("cos_weapon_" + Weapon(lose)), CosmosGen.Get(back.Seat).Name);
                }
            }
            return true;
        }

        private static void Record(string node, string a, string b, string win)
        {
            Cosmos.St.Battles.Add(new Battle { Node = node, Year = Cosmos.Now, A = a, B = b, Win = win });
        }

        public static void Reward(Kingdom k, string foe)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, Space.KPrestige, Space.Prestige(k) + 40f);
            Legitimacy.Shock(k, 0.1f);
            Festivals.Mark(k, "cosmic_victory");
            foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_victory", 0.25f);
            int era = Research.Era(k);
            var pool = Db.Techs.Where(t => t.Rare && t.Era <= Math.Max(era, Research.PlanetEras) + 1 && !Research.Has(k, t.Id)).ToList();
            if (pool.Count > 0) Research.Discover(k, pool[UnityEngine.Random.Range(0, pool.Count)]);
        }

        public static void Arrive(Kingdom k, Voyage v)
        {
        }
    }
}
