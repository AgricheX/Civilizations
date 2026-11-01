using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Clans
    {
        public const string KInfl = "civ_infl";
        public const string KMem = "civ_mem";
        public const string KRival = "civ_rival";
        public const string KPact = "civ_pact";
        public const string KLine = "civ_line";
        public const string KKing = "civ_clan_k";
        public const string KCause = "civ_cause";

        private static readonly string[] Lines = { "mil", "build", "med", "agri", "sci", "econ", "ind", "comm" };

        public static List<Clan> All()
        {
            var list = new List<Clan>();
            if (World.world == null || World.world.clans == null) return list;
            foreach (Clan c in World.world.clans.list)
            {
                if (c != null && c.isAlive() && c.data != null) list.Add(c);
            }
            return list;
        }

        public static Kingdom HomeOf(Clan c)
        {
            Actor chief = c.getChief();
            if (G.Ok(chief) && chief.kingdom != null && chief.kingdom.isCiv()) return chief.kingdom;
            var count = new Dictionary<Kingdom, int>();
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || a.kingdom == null || !a.kingdom.isCiv()) continue;
                int n;
                count.TryGetValue(a.kingdom, out n);
                count[a.kingdom] = n + 1;
            }
            Kingdom best = null;
            int bn = 0;
            foreach (var kv in count)
            {
                if (kv.Value > bn)
                {
                    bn = kv.Value;
                    best = kv.Key;
                }
            }
            return best;
        }

        public static List<Clan> Of(Kingdom k)
        {
            var list = new List<Clan>();
            if (k == null || k.data == null) return list;
            string id = k.data.id.ToString();
            foreach (Clan c in All())
            {
                if (Store.Str(c.data, KKing) == id) list.Add(c);
            }
            return list;
        }

        public static Clan Dominant(Kingdom k)
        {
            Clan best = null;
            float bi = -1f;
            foreach (Clan c in Of(k))
            {
                float i = Influence(c);
                if (i > bi)
                {
                    bi = i;
                    best = c;
                }
            }
            return best;
        }

        public static float Influence(Clan c)
        {
            return c == null || c.data == null ? 0f : Store.Flt(c.data, KInfl, 5f);
        }

        public static void AddInfluence(Clan c, float v)
        {
            if (c == null || c.data == null) return;
            Store.Put(c.data, KInfl, G.Clamp(Influence(c) + v, 0f, 100f));
        }

        public static string Cause(Clan c)
        {
            return c == null || c.data == null ? "" : Store.Str(c.data, KCause);
        }

        public static void Year(Clan c, int yrs)
        {
            Kingdom k = HomeOf(c);
            Store.Put(c.data, KKing, k != null ? k.data.id.ToString() : "");
            if (k == null) return;
            float mult = ClanMult(k);
            int members = 0, offices = 0, warriors = 0, money = 0;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a)) continue;
                members++;
                if (a.isKing()) offices += 3;
                else if (a.isCityLeader()) offices += 2;
                if (a.isWarrior()) warriors++;
                money += a.money;
                string role = Roles.Of(a);
                if (role == Roles.Servant || role == Roles.Ambassador || role == Roles.Scientist) offices++;
            }
            if (members == 0) return;
            float infl = Influence(c);
            float gain = (members * 0.08f + offices * 1.2f + Companies.OwnedBy(c) * 1.2f) * mult;
            infl += (gain - infl * 0.04f) * yrs;
            infl = G.Clamp(infl, 0f, 100f);
            Store.Put(c.data, KInfl, infl);
            if (Store.Str(c.data, KLine).Length == 0 || G.Chance(0.01f * yrs))
            {
                string line = warriors > members / 3 ? "mil" : G.Pick(Lines);
                Store.Put(c.data, KLine, line);
            }
            if (Cause(c).Length == 0 || G.Chance(0.02f * yrs))
            {
                Actor chief = c.getChief();
                Opinion o = Politics.Get(chief);
                if (o != null) Store.Put(c.data, KCause, G.Chance(0.5f) ? o.Ideo : (G.Chance(0.5f) ? o.Gov : o.Econ));
            }
            Relations(c, k, yrs);
            Ambition(c, k, infl, yrs);
        }

        public static float ClanMult(Kingdom k)
        {
            float stage = new[] { 1.5f, 1.35f, 1.2f, 1.1f, 1f, 0.95f, 0.7f }[Politics.StageIndex(k)];
            string gov = Policies.Id(k, "gov");
            if (gov == "feudal_monarchy" || gov == "chiefdom") stage *= 1.3f;
            if (gov == "single_party_state" || gov == "technocracy") stage *= 0.6f;
            return stage;
        }

        private static void Relations(Clan c, Kingdom k, int yrs)
        {
            List<Clan> peers = Of(k);
            peers.Remove(c);
            if (peers.Count == 0) return;
            string rival = Store.Str(c.data, KRival);
            string pact = Store.Str(c.data, KPact);
            Clan other = G.Pick(peers);
            if (other == null) return;
            string oid = other.data.id.ToString();
            string mem = Store.Str(c.data, KMem);
            bool betrayed = mem.Contains("betrayal:" + oid);
            bool opposed = Cause(c).Length > 0 && Cause(other).Length > 0 && Cause(c) != Cause(other);
            if (rival.Length == 0 && (betrayed || opposed || Influence(other) > 30f && Influence(c) > 30f) && G.Chance((betrayed ? 0.2f : opposed ? 0.05f : 0.03f) * yrs))
            {
                Store.Put(c.data, KRival, oid);
                Store.Put(other.data, KRival, c.data.id.ToString());
                Internet.OnRivalry(c, other);
            }
            else if (pact.Length == 0 && rival != oid && !betrayed && G.Chance(0.02f * yrs))
            {
                Store.Put(c.data, KPact, oid);
                Store.Put(other.data, KPact, c.data.id.ToString());
                Remember(c, "marriage:" + oid, k);
                Remember(other, "marriage:" + c.data.id, k);
                Internet.OnPact(c, other);
            }
            if (rival.Length > 0 && pact == rival) Store.Put(c.data, KRival, "");
            if (rival.Length > 0 && G.Chance(0.01f * yrs)) Store.Put(c.data, KRival, "");
        }

        private static void Ambition(Clan c, Kingdom k, float infl, int yrs)
        {
            float stab = Politics.Stability(k);
            Actor chief = c.getChief();
            if (!G.Ok(chief) || chief.kingdom != k || !Xeno.CanHoldOffice(chief)) return;
            Clan royal = k.hasKing() ? k.king.clan : null;
            if (royal == c) return;
            PolicyDef gov = Policies.Get(k, "gov");
            if (gov != null && gov.Elections && stab > 0.25f) return;
            float royalInfl = royal != null && royal.data != null ? Influence(royal) : 0f;
            float pressure = (infl - royalInfl) / 100f;
            if (pressure > 0.2f && stab < 0.35f && chief.isAdult() && G.Chance(pressure * (0.4f - stab) * 0.25f * yrs))
            {
                Coup(c, k, chief, royal);
                return;
            }
            if (stab < 0.2f && infl > 40f && World.world.world_laws.isEnabled("world_law_rebellions") && G.Chance(0.02f * yrs))
            {
                City seat = null;
                foreach (Actor a in c.units)
                {
                    if (G.Ok(a) && a.isCityLeader() && a.city != null && a.city.kingdom == k && !a.city.isCapitalCity())
                    {
                        seat = a.city;
                        break;
                    }
                }
                if (seat != null && seat.leader != null)
                {
                    Kingdom nk = seat.makeOwnKingdom(seat.leader, true);
                    if (nk != null)
                    {
                        Remember(c, "rebellion", k);
                        Internet.OnClanRebellion(c, k, nk);
                    }
                }
            }
        }

        private static void Coup(Clan c, Kingdom k, Actor chief, Clan royal)
        {
            Actor old = k.king;
            if (G.Ok(old)) k.kingLeftEvent();
            else k.removeKing();
            if (chief.city != null && chief.isCityLeader()) chief.city.removeLeader();
            k.setKing(chief);
            Store.Put(c.data, KInfl, Math.Max(0f, Influence(c) - 15f));
            if (royal != null && royal.data != null)
            {
                Remember(royal, "betrayal:" + c.data.id, k);
                Store.Put(royal.data, KRival, c.data.id.ToString());
            }
            Remember(c, "crown", k);
            Politics.AddStability(k, -0.15f);
            string cause = Cause(c);
            PolicyDef p = Db.Policy(cause);
            if (p != null && Policies.Unlocked(k, p)) Policies.Set(k, p.Group, p.Id, "coup");
            Internet.OnCoup(c, k, chief, old);
        }

        public static void Remember(Clan c, string what, Kingdom k)
        {
            if (c == null || c.data == null) return;
            string mem = Store.Str(c.data, KMem);
            string entry = what + "@" + G.Year;
            var parts = mem.Length > 0 ? mem.Split('|').ToList() : new List<string>();
            parts.Add(entry);
            while (parts.Count > 12) parts.RemoveAt(0);
            Store.Put(c.data, KMem, string.Join("|", parts));
        }

        public static void OnHero(Actor a, string what)
        {
            if (!G.Ok(a) || a.clan == null || a.clan.data == null) return;
            Remember(a.clan, "hero:" + what, a.kingdom);
            AddInfluence(a.clan, 3f);
        }

        public static float Tension(Kingdom k)
        {
            float t = 0f;
            foreach (Clan c in Of(k))
            {
                if (Store.Str(c.data, KRival).Length > 0) t += Influence(c) / 400f;
            }
            return G.Clamp(t * ClanMult(k), 0f, 0.4f);
        }

        public static float ResearchBonus(Kingdom k)
        {
            float b = 0f;
            foreach (Clan c in Of(k))
            {
                if (Influence(c) > 30f) b += 0.03f;
            }
            return Math.Min(0.15f, b);
        }

        public static string FocusArea(Kingdom k)
        {
            Clan best = Dominant(k);
            return best == null ? "" : Store.Str(best.data, KLine);
        }

        public static float PolicyPull(Clan c, string policy)
        {
            if (c == null || c.data == null) return 0f;
            float i = Influence(c) / 100f;
            float pull = 0f;
            if (Cause(c) == policy) pull += 1.2f * i + 0.3f;
            if (Companies.OwnedBy(c) > 0 && (policy == "laissez_faire" || policy == "corporatocracy" || policy == "liberalism" || policy == "mercantilism")) pull += i;
            if (Store.Str(c.data, KLine) == "mil" && (policy == "military_junta" || policy == "militarism" || policy == "nationalism")) pull += i;
            return pull;
        }
    }
}
