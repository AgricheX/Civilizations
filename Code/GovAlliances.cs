using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class AllianceLife
    {
        private const string KType = "civ_al_type";
        private const string KCoh = "civ_al_coh";
        private const string KWhy = "civ_al_why";
        private const string KLead = "civ_al_lead";
        private const string KSince = "civ_al_since";
        private const string KFoe = "civ_al_foe";
        private const string KLast = "civ_al_last";
        public static readonly string[] Types = { "tribal_pact", "religious_league", "dynastic_union", "coalition", "trade_league", "ideological_bloc", "military_alliance", "nuclear_umbrella", "federation", "galactic_union" };
        private static readonly HashSet<long> _ending = new HashSet<long>();

        public static void Clear()
        {
            _ending.Clear();
        }

        public static List<Alliance> All()
        {
            var l = new List<Alliance>();
            if (World.world == null || World.world.alliances == null) return l;
            foreach (Alliance a in World.world.alliances.list) if (a != null && a.isAlive() && a.data != null) l.Add(a);
            return l;
        }

        public static Alliance Of(Kingdom k)
        {
            return G.Ok(k) && k.hasAlliance() ? k.getAlliance() : null;
        }

        public static string Type(Alliance a)
        {
            if (a == null || a.data == null) return "";
            string t = Store.Str(a.data, KType);
            if (t.Length == 0) Classify(a, null);
            return Store.Str(a.data, KType);
        }

        public static float Cohesion(Alliance a)
        {
            return a == null || a.data == null ? 0f : Store.Flt(a.data, KCoh, 60f);
        }

        public static string Reason(Alliance a)
        {
            return a == null || a.data == null ? "" : Store.Str(a.data, KWhy);
        }

        public static string LastEvent(Alliance a)
        {
            return a == null || a.data == null ? "" : Store.Str(a.data, KLast);
        }

        public static Kingdom Leader(Alliance a)
        {
            if (a == null || a.kingdoms_list == null) return null;
            return a.kingdoms_list.Where(G.Ok).OrderByDescending(k => Profile.Get(k).Strength).FirstOrDefault();
        }

        public static Kingdom Foe(Alliance a)
        {
            return a == null ? null : G.KingdomById(Store.Str(a.data, KFoe));
        }

        public static void Classify(Alliance a, Kingdom foe)
        {
            if (a == null || a.data == null) return;
            List<Kingdom> ms = a.kingdoms_list.Where(G.Ok).ToList();
            if (ms.Count == 0) return;
            int era = ms.Max(k => Profile.Get(k).Era);
            int band = Gov.Band(era);
            string type, why;
            Religion rel = ms[0].getReligion();
            bool sameFaith = rel != null && ms.All(k => k.getReligion() == rel);
            string ideo = Policies.Id(ms[0], "ideo");
            bool sameIdeo = ms.All(k => Policies.Id(k, "ideo") == ideo);
            Clan royal = ms[0].getKingClan();
            bool sameHouse = royal != null && ms.All(k => k.getKingClan() == royal) || ms.Count == 2 && Diplomacy.Has(ms[0], ms[1], "royal_marriage");
            bool trade = ms.Count >= 2 && Diplomacy.Has(ms[0], ms[1], "trade_treaty");
            bool nukes = ms.Any(k => War.Nukes(k) > 0);
            bool demo = ms.All(k => Profile.Get(k).Fam.Contains("democratic"));
            if (band >= 6) { type = "galactic_union"; why = "stars"; }
            else if (foe != null) { type = "coalition"; why = "threat"; }
            else if (band == 0) { type = "tribal_pact"; why = "kinship"; }
            else if (sameHouse && band <= 3) { type = "dynastic_union"; why = "house"; }
            else if (sameFaith && band <= 2) { type = "religious_league"; why = "faith"; }
            else if (band >= 5 && demo) { type = "federation"; why = "union"; }
            else if (nukes && band >= 4) { type = "nuclear_umbrella"; why = "deterrence"; }
            else if (sameIdeo && band >= 3) { type = "ideological_bloc"; why = "ideology"; }
            else if (trade && band >= 2) { type = "trade_league"; why = "trade"; }
            else { type = "military_alliance"; why = "defense"; }
            Store.Put(a.data, KType, type);
            Store.Put(a.data, KWhy, why);
            if (foe != null) Store.Put(a.data, KFoe, foe.data.id.ToString());
            if (Store.Int(a.data, KSince, -1) < 0) Store.Put(a.data, KSince, G.Year);
            if (Store.Flt(a.data, KCoh, -1f) < 0f) Store.Put(a.data, KCoh, 60f);
            Kingdom lead = Leader(a);
            if (lead != null) Store.Put(a.data, KLead, lead.data.id.ToString());
        }

        public static void ForceType(Alliance a, string type)
        {
            if (a == null || a.data == null || Array.IndexOf(Types, type) < 0) return;
            Store.Put(a.data, KType, type);
            Store.Put(a.data, KWhy, type == "ideological_bloc" ? "ideology" : type == "trade_league" ? "trade" : type == "nuclear_umbrella" ? "deterrence" : type == "federation" ? "union" : "defense");
            if (Store.Flt(a.data, KCoh, -1f) < 0f) Store.Put(a.data, KCoh, 60f);
        }

        public static void Shift(Alliance a, float d, string why)
        {
            if (a == null || a.data == null || _ending.Contains(a.data.id)) return;
            float c = G.Clamp(Cohesion(a) + d, 0f, 100f);
            Store.Put(a.data, KCoh, c);
            if (Math.Abs(d) >= 5f)
            {
                Store.Put(a.data, KLast, why + "|" + G.Year);
                Kingdom lead = Leader(a);
                if (lead != null && G.Chance(0.5f)) Gov.Log("alliance_event", lead, a.data.name, Txt.T("civ_alev_" + why), (d > 0 ? "+" : "") + (int)d);
            }
        }

        public static void Register()
        {
            Hub.On(Hub.WarEnded, e =>
            {
                if (!Gov.On("civ_gov_alliances") || !G.Ok(e.K) || e.Id == "draw" || e.Id == "Peace") return;
                Alliance a = Of(e.K);
                if (a == null) return;
                Shift(a, 10f, "victory");
            });
            Hub.On(Hub.WarStarted, e =>
            {
                if (!Gov.On("civ_gov_alliances") || !G.Ok(e.K) || !G.Ok(e.O)) return;
                Alliance a = Of(e.K), b = Of(e.O);
                if (a != null && a == b) Shift(a, -40f, "betrayal");
                if (b != null && b != a) Shift(b, 4f, "rally");
            });
            Hub.On(Hub.PolicyChanged, e =>
            {
                if (!Gov.On("civ_gov_alliances") || !G.Ok(e.K)) return;
                Alliance a = Of(e.K);
                if (a == null) return;
                string[] p = (e.Arg ?? "").Split('|');
                string group = p.Length > 1 ? p[1] : "";
                string reason = p.Length > 2 ? p[2] : "";
                if (group == "gov" && (reason == "revolution" || reason == "coup")) Shift(a, -12f, "revolution");
                else if (group == "ideo")
                {
                    Kingdom lead = Leader(a);
                    if (lead != null && lead != e.K && Policies.Id(lead, "ideo") != e.Id) Shift(a, Type(a) == "ideological_bloc" ? -20f : -6f, "ideology");
                }
            });
            Hub.On(Hub.Coup, e =>
            {
                if (!Gov.On("civ_gov_alliances") || !G.Ok(e.K)) return;
                Shift(Of(e.K), -10f, "coup");
            });
            Hub.On(Hub.CivilWar, e =>
            {
                if (!Gov.On("civ_gov_alliances") || !G.Ok(e.K)) return;
                Shift(Of(e.K), -10f, "rebellion");
            });
            Hub.On(Hub.Legitimacy, e =>
            {
                if (!Gov.On("civ_gov_alliances") || !G.Ok(e.K) || e.Id != "new_dynasty") return;
                Alliance a = Of(e.K);
                if (a == null) return;
                Shift(a, Type(a) == "dynastic_union" ? -20f : -8f, "new_house");
            });
            Hub.On(Hub.Event, e =>
            {
                if (!Gov.On("civ_gov_alliances") || !G.Ok(e.K)) return;
                Alliance a = Of(e.K);
                if (a != null && G.Chance(0.3f)) Shift(a, e.Value < 0f ? -5f : 5f, "saga");
            });
        }

        public static void Year()
        {
            foreach (Alliance a in All())
            {
                Alliance al = a;
                CivMod.Safe("alliance", () => AllianceYear(al));
            }
        }

        private static void AllianceYear(Alliance a)
        {
            if (Store.Str(a.data, KType).Length == 0) Classify(a, null);
            List<Kingdom> ms = a.kingdoms_list.Where(G.Ok).ToList();
            if (ms.Count < 2) return;
            Kingdom lead = Leader(a);
            string old = Store.Str(a.data, KLead);
            if (lead != null && old != lead.data.id.ToString())
            {
                Store.Put(a.data, KLead, lead.data.id.ToString());
                if (old.Length > 0) Shift(a, -5f, "new_leader");
            }
            float d = 0f;
            if (lead != null)
            {
                int ph = Statecraft.Phase(lead);
                if (ph == 2) d -= 3f;
                if (ph == 3) d -= 8f;
                if (Mandate.Rebelling(lead)) d -= 5f;
            }
            foreach (Kingdom m in ms)
            {
                foreach (global::War w in World.world.wars.getWars(m))
                {
                    if (w == null || w.hasEnded() || !w.isDefender(m)) continue;
                    foreach (Kingdom o in ms)
                    {
                        if (o == m || w.isDefender(o) || w.isAttacker(o)) continue;
                        Shift(a, -5f, "no_help");
                    }
                }
            }
            if (Statecraft.Crises(lead) > 0) d += 3f;
            Kingdom foe = Foe(a);
            if (Store.Str(a.data, KWhy) == "threat")
            {
                if (!G.Ok(foe) || Statecraft.Aggressive(foe) < 15f) Shift(a, -10f, "threat_gone");
                else d += 2f;
            }
            string type = Type(a);
            if (type == "religious_league")
            {
                Religion r = ms[0].getReligion();
                if (ms.Any(k => k.getReligion() != r)) d -= 4f;
            }
            if (type == "trade_league" && ms.Count >= 2 && Diplomacy.Has(ms[0], ms[1], "trade_treaty")) d += 2f;
            d += (60f - Cohesion(a)) * 0.05f;
            Shift(a, d, "drift");
            if (Gov.Band(Profile.Get(lead).Era) >= 6 && type != "galactic_union" && G.Chance(0.1f)) Classify(a, foe);
            if (Cohesion(a) <= 0f) End(a, "collapse");
            else if (Cohesion(a) < 15f)
            {
                Kingdom leaver = ms.Where(k => k != lead).OrderBy(k => World.world.diplomacy.getOpinion(k, lead).total).FirstOrDefault();
                if (leaver != null && G.Chance(0.3f))
                {
                    leaver.allianceLeave(a);
                    Gov.Log("alliance_event", leaver, a.data.name, Txt.T("civ_alev_left"), G.Name(leaver));
                }
            }
        }

        public static void End(Alliance a, string why)
        {
            if (a == null || a.data == null || !_ending.Add(a.data.id)) return;
            try
            {
                Kingdom lead = Leader(a);
                string name = a.data.name;
                World.world.alliances.dissolveAlliance(a);
                if (lead != null) Gov.Log("alliance_ended", lead, name, Txt.T("civ_alend_" + why));
            }
            finally
            {
                _ending.Remove(a.data.id);
            }
        }

        public static Alliance Create(Kingdom a, Kingdom b, Kingdom foe)
        {
            if (!G.Ok(a) || !G.Ok(b) || a == b) return null;
            if (a.hasAlliance()) a.allianceLeave(a.getAlliance());
            if (b.hasAlliance()) b.allianceLeave(b.getAlliance());
            Alliance al = World.world.alliances.newAlliance(a, b);
            if (al == null) return null;
            Classify(al, foe);
            Gov.Log("alliance_formed", a, al.data.name, Txt.T("civ_alt_" + Type(al)), G.Name(b), b);
            return al;
        }

        public static Alliance CoalitionAgainst(Kingdom target)
        {
            if (!G.Ok(target)) return null;
            var members = G.Kingdoms().Where(o => G.Ok(o) && o != target && o.isCiv() && Diplomacy.Overlord(o) != target && Fog.Knows(o, target)).OrderByDescending(o => Profile.Get(o).Strength).Take(5).ToList();
            if (members.Count < 2) return null;
            Alliance al = Create(members[0], members[1], target);
            if (al == null) return null;
            for (int i = 2; i < members.Count; i++)
            {
                if (members[i].hasAlliance()) members[i].allianceLeave(members[i].getAlliance());
                al.join(members[i], true, true);
            }
            Classify(al, target);
            return al;
        }

        public static string Label(Alliance a)
        {
            string t = Type(a);
            return t.Length == 0 ? "" : Txt.T("civ_alt_" + t);
        }

        public static int Opinion(Kingdom a, Kingdom b)
        {
            if (!Gov.On("civ_gov_alliances")) return 0;
            Alliance x = Of(a);
            if (x == null || x != Of(b)) return 0;
            return (int)((Cohesion(x) - 40f) / 3f);
        }
    }
}
