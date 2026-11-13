using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Civilizations
{
    internal class EventDef
    {
        public string Id = "";
        public string Cat = "";
        public int Min;
        public int Max = 99;
        public float W = 1f;
        public int Cd = 30;
        public string Scope = "kingdom";
        public bool Good;
        public List<string> Need = new List<string>();
        public List<string> Any = new List<string>();
        public List<string> Not = new List<string>();
        public List<string> Cond = new List<string>();
        public Dictionary<string, JToken> Fx = new Dictionary<string, JToken>();
        public int Years;
        public bool Big;
    }

    internal static class Events
    {
        private const string KCd = "civ_ev_cd";
        private const string KLast = "civ_ev_last";
        private const string KFx = "civ_ev_fx";
        private const string KGreats = "civ_greats";
        private const string KGreatRole = "civ_great";
        private const string WCd = "civ_wev_cd";
        private const string WUnique = "civ_ev_unique";
        public static readonly List<EventDef> Defs = new List<EventDef>();
        public static readonly Dictionary<string, EventDef> ById = new Dictionary<string, EventDef>();
        public static readonly Dictionary<string, Dictionary<string, float>> Greats = new Dictionary<string, Dictionary<string, float>>();

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.Events; }
        }

        public static void InitData()
        {
            Defs.Clear();
            ById.Clear();
            Greats.Clear();
            try
            {
                string p = Path.Combine(CivMod.Folder, "Data", "events.json");
                if (!File.Exists(p)) return;
                JObject root = JObject.Parse(File.ReadAllText(p));
                foreach (EventDef d in root["events"].ToObject<List<EventDef>>())
                {
                    Defs.Add(d);
                    ById[d.Id] = d;
                }
                foreach (var kv in root["greats"].ToObject<Dictionary<string, Dictionary<string, float>>>()) Greats[kv.Key] = kv.Value;
            }
            catch (Exception e)
            {
                CivMod.Log("events data: " + e.Message);
            }
        }

        public static string Category(string id)
        {
            EventDef d;
            return ById.TryGetValue(id, out d) ? d.Cat : "";
        }

        public static bool Good(string id)
        {
            EventDef d;
            return ById.TryGetValue(id, out d) && d.Good;
        }

        private static float F(EventDef d, string key, float def = 0f)
        {
            JToken t;
            return d.Fx.TryGetValue(key, out t) ? t.Value<float>() : def;
        }

        private static string S(EventDef d, string key)
        {
            JToken t;
            return d.Fx.TryGetValue(key, out t) && t.Type == JTokenType.String ? t.Value<string>() : "";
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var m = new Dictionary<string, float>();
            if (!G.Ok(k)) return m;
            Dictionary<string, float> fx = Store.Map(k.data, KFx);
            foreach (var kv in fx)
            {
                int bar = kv.Key.IndexOf('|');
                if (bar < 0) continue;
                int until;
                if (!int.TryParse(kv.Key.Substring(0, bar), out until) || until < G.Year) continue;
                Store.Add(m, kv.Key.Substring(bar + 1), kv.Value);
            }
            foreach (string entry in Store.Set(k.data, KGreats))
            {
                Actor a = G.ActorById(entry.Split('|')[0]);
                if (!G.Ok(a)) continue;
                Dictionary<string, float> b;
                if (Greats.TryGetValue(Store.Str(a.data, KGreatRole), out b)) foreach (var kv in b) Store.Add(m, kv.Key, kv.Value);
            }
            return m;
        }

        internal static void AddBonus(Kingdom k, Dictionary<string, float> bonus, int years)
        {
            if (bonus == null || bonus.Count == 0 || years <= 0) return;
            Dictionary<string, float> fx = Store.Map(k.data, KFx);
            int until = years >= 9999 ? 999999 : G.Year + years;
            foreach (var kv in fx.Keys.ToList())
            {
                int bar = kv.IndexOf('|');
                int u;
                if (bar < 0 || !int.TryParse(kv.Substring(0, bar), out u) || u < G.Year) fx.Remove(kv);
            }
            foreach (var kv in bonus) Store.Add(fx, until + "|" + kv.Key, kv.Value);
            Store.Map(k.data, KFx, fx);
            Bonus.Dirty(k);
        }

        internal static bool Cond(EventDef d, Kingdom k, KProfile p, out Kingdom other)
        {
            other = null;
            foreach (string c in d.Cond)
            {
                switch (c)
                {
                    case "big": if (p.Cities < 4) return false; break;
                    case "poor": if (p.Gold > p.Cities * 40) return false; break;
                    case "rich": if (p.Gold < p.Cities * 150) return false; break;
                    case "peace": if (p.AtWar) return false; break;
                    case "war": if (!p.AtWar) return false; break;
                    case "stable": if (p.Stability < 0.6f) return false; break;
                    case "unstable": if (p.Stability >= 0.35f && p.Legit >= 0.35f) return false; break;
                    case "king": if (!k.hasKing()) return false; break;
                    case "companies": if (Companies.Of(k).Count < 2) return false; break;
                    case "internet": if (!Internet.Exists(k)) return false; break;
                    case "dormant": if (Regression.Dormant(k).Count == 0) return false; break;
                    case "unique": if (Store.Set(Store.World(), WUnique).Contains(d.Id + ":" + k.data.id)) return false; break;
                    case "foreign":
                        Culture kc = k.getCulture();
                        if (!G.Cities(k).Any(x => x != k.capital && (kc != null && x.getCulture() != null && x.getCulture() != kc || Xeno.ForeignShare(x) > 0.5f))) return false;
                        break;
                    case "rival":
                        other = Fog.KnownKingdoms(k).Where(o => Research.Era(o) >= 11 && Opinion(k, o) < -20).OrderByDescending(o => Profile.Get(o).Strength).FirstOrDefault();
                        if (other == null) return false;
                        break;
                    case "infidel":
                        Religion r = k.getReligion();
                        other = Fog.KnownKingdoms(k).Where(o => o.getReligion() != null && o.getReligion() != r && !Diplomacy.Forbids(k, o) && !World.world.wars.isInWarWith(k, o)).OrderBy(o => Profile.Get(o).Strength).FirstOrDefault();
                        if (other == null || r == null) return false;
                        break;
                    case "neighbor":
                        other = Fog.KnownKingdoms(k).Where(o => G.Ok(o) && o != k && o.isCiv() && !World.world.wars.isInWarWith(k, o)).OrderBy(o => UnityEngine.Random.value).FirstOrDefault();
                        if (other == null) return false;
                        break;
                    case "coast": if (!p.Coastal) return false; break;
                    case "religious": if (!p.Is("religious")) return false; break;
                    case "polluted_world":
                        float sum = 0f;
                        int n = 0;
                        foreach (City x in G.Cities())
                        {
                            sum += Pollution.Level(x);
                            n++;
                        }
                        if (n == 0 || sum / n < 0.35f) return false;
                        break;
                    default:
                        if (c.StartsWith("tech:") && !Research.Has(k, c.Substring(5))) return false;
                        break;
                }
            }
            return true;
        }

        private static int Opinion(Kingdom a, Kingdom b)
        {
            try
            {
                return World.world.diplomacy.getOpinion(a, b).total;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool Fits(EventDef d, KProfile p)
        {
            if (p.Era < d.Min || p.Era > d.Max) return false;
            foreach (string t in d.Need) if (!p.Is(t)) return false;
            foreach (string t in d.Not) if (p.Is(t)) return false;
            if (d.Any.Count > 0 && !d.Any.Any(p.Is)) return false;
            return true;
        }

        public static void Year()
        {
            if (!On || Defs.Count == 0) return;
            List<Kingdom> ks = G.Kingdoms().Where(x => G.Cities(x).Count > 0).ToList();
            if (ks.Count == 0) return;
            WorldYear(ks);
            int rolls = Math.Max(1, Math.Min(6, ks.Count / 6 + 1));
            for (int i = 0; i < rolls; i++)
            {
                Kingdom k = ks[UnityEngine.Random.Range(0, ks.Count)];
                CivMod.Safe("event-roll", () => Roll(k));
            }
            CivMod.Safe("greats-year", () => GreatsYear(ks));
        }

        private static void WorldYear(List<Kingdom> ks)
        {
            SaveCustomData w = Store.World();
            if (w == null) return;
            int top = ks.Max(x => Research.Era(x));
            Dictionary<string, float> cd = Store.ParseMap(Store.Str(w, WCd));
            foreach (EventDef d in Defs)
            {
                if (d.Scope != "world" || top < d.Min || top > d.Max) continue;
                if (G.Year - (int)Store.Get(cd, d.Id) < d.Cd && cd.ContainsKey(d.Id)) continue;
                if (!G.Chance(d.W * 0.01f)) continue;
                Kingdom other;
                if (!Cond(d, ks[0], Profile.Get(ks[0]), out other)) continue;
                cd[d.Id] = G.Year;
                Store.Put(w, WCd, Store.WriteMap(cd));
                Hub.Raise(Hub.Event, null, null, null, null, d.Id, "world", d.Big ? 1f : 0f);
                foreach (Kingdom k in ks)
                {
                    if (Research.Era(k) < d.Min) continue;
                    Hub.Raise(new HubEvent { Type = Hub.Event, K = k, Id = d.Id, Arg = "world_part", Value = 0f, Year = G.Year });
                    Apply(d, k, null, k.capital, true);
                }
                return;
            }
        }

        private static void Roll(Kingdom k)
        {
            if (!G.Ok(k)) return;
            if (G.Year - Store.Int(k.data, KLast, -999) < 4) return;
            KProfile p = Profile.Get(k);
            Dictionary<string, float> cd = Store.Map(k.data, KCd);
            var cands = new List<EventDef>();
            var weights = new List<float>();
            var others = new List<Kingdom>();
            foreach (EventDef d in Defs)
            {
                if (d.Scope == "world" || !Fits(d, p)) continue;
                if (cd.ContainsKey(d.Id) && G.Year - (int)cd[d.Id] < d.Cd) continue;
                Kingdom other;
                if (!Cond(d, k, p, out other)) continue;
                float w = d.W;
                if (d.Good && p.Stability > 0.6f) w *= 1.3f;
                if (!d.Good && p.Stability < 0.35f) w *= 1.3f;
                cands.Add(d);
                weights.Add(w);
                others.Add(other);
            }
            if (cands.Count == 0) return;
            float total = weights.Sum();
            if (!G.Chance(Math.Min(0.6f, total * 0.06f))) return;
            int i = G.Weighted(weights);
            if (i < 0) return;
            EventDef pick = cands[i];
            cd[pick.Id] = G.Year;
            Store.Map(k.data, KCd, cd);
            Store.Put(k.data, KLast, G.Year);
            City city = PickCity(k, pick);
            Apply(pick, k, others[i], city, false);
        }

        private static City PickCity(Kingdom k, EventDef d)
        {
            List<City> cs = G.Cities(k);
            if (cs.Count == 0) return null;
            if (d.Id == "great_flood") return cs.FirstOrDefault(c => c.hasBuildingType("type_docks")) ?? k.capital;
            if (d.Id == "peasant_revolt" || d.Id == "independence_movement") return cs.Where(c => c != k.capital).OrderBy(c => c.getPopulationPeople()).FirstOrDefault() ?? k.capital;
            if (d.Good) return k.capital ?? cs[0];
            return cs[UnityEngine.Random.Range(0, cs.Count)];
        }

        public static void Force(Kingdom k, string id)
        {
            EventDef d;
            if (!G.Ok(k) || !ById.TryGetValue(id, out d)) return;
            Apply(d, k, null, k.capital, false);
        }

        private static void Apply(EventDef d, Kingdom k, Kingdom other, City city, bool part)
        {
            KProfile p = Profile.Get(k);
            List<City> cs = G.Cities(k);
            float stab = F(d, "stability");
            if (stab != 0f) Politics.AddStability(k, stab);
            float legit = F(d, "legit");
            if (legit != 0f) Store.Put(k.data, Legitimacy.KLegit, G.Clamp(Legitimacy.Get(k) + legit, 0f, 1f));
            string mood = S(d, "mood");
            if (mood.Length > 0) foreach (City c in cs) Moods.Apply(c, mood, F(d, "share", 0.3f));
            JToken bt;
            if (d.Fx.TryGetValue("bonus", out bt)) AddBonus(k, bt.ToObject<Dictionary<string, float>>(), Math.Max(1, d.Years));
            float gold = F(d, "gold");
            if (gold > 0f && city != null) Economy.Give(city, "gold", (int)gold);
            string res = S(d, "res");
            if (res.Length > 0 && city != null) Economy.Give(city, res, (int)F(d, "amount", 50f));
            float kill = F(d, "kill");
            if (kill > 0f) Kill(k, city, kill);
            if (F(d, "kill_ruler") > 0f && k.hasKing() && G.Ok(k.king)) k.king.getHit(k.king.getMaxHealth() * 10f + 1000f, true, AttackType.Other, null, false, false, false);
            int free = (int)F(d, "free_tech");
            for (int i = 0; i < free; i++)
            {
                TechDef cur = Research.Current(k);
                if (cur == null) break;
                Research.Learn(k, cur, "event");
            }
            string great = S(d, "great");
            if (great.Length > 0) Great(k, great, city);
            if (F(d, "crisis") > 0f) Legitimacy.Crisis(k, p, Legitimacy.Get(k));
            if (F(d, "secession") > 0f) Legitimacy.Secession(k, p);
            if (F(d, "civil_war") > 0f && p.Cities >= 4) Legitimacy.CivilWar(k, p);
            if (F(d, "coup") > 0f) Coup(k, p);
            if (F(d, "holy_war") > 0f && G.Ok(other) && !Diplomacy.Forbids(k, other)) World.world.diplomacy.startWar(k, other, WarTypeLibrary.normal);
            if (d.Cond.Contains("unique")) Store.Set(Store.World(), WUnique, Store.Set(Store.World(), WUnique).Concat(new[] { d.Id + ":" + k.data.id }));
            Profile.Invalidate(k);
            if (part) return;
            Hub.Raise(Hub.Event, k, other, city, null, d.Id, d.Cat, d.Big ? 1f : 0f);
            string line = Txt.Has("civ_ev_" + d.Id + "_log") ? Txt.F("civ_ev_" + d.Id + "_log", G.Name(k), city != null ? G.Name(city) : G.Name(k), G.Ok(other) ? G.Name(other) : "") : "";
            if (line.Length > 0) Internet.Official(k, "event", line, d.Big);
        }

        internal static void Coup(Kingdom k, KProfile p)
        {
            PolicyDef junta = Db.Policy("military_junta");
            if (p.Gov == "military_junta" || junta == null || !Policies.Unlocked(k, junta)) return;
            Policies.Set(k, "gov", "military_junta", "coup");
            Hub.Raise(Hub.Coup, k, null, k.capital, null, "military_junta", p.Gov);
        }

        internal static void Kill(Kingdom k, City city, float share)
        {
            float mult = 1f;
            if (Research.Has(k, "germ_theory")) mult *= 0.5f;
            if (Research.Has(k, "antibiotics")) mult *= 0.4f;
            if (Research.Has(k, "mass_vaccination")) mult *= 0.5f;
            var targets = new List<City>();
            if (city != null) targets.Add(city);
            foreach (City c in G.Cities(k)) if (c != city && targets.Count < 3 && G.Chance(0.4f)) targets.Add(c);
            int budget = 40;
            foreach (City c in targets)
            {
                foreach (Actor a in c.units.ToList())
                {
                    if (budget <= 0) return;
                    if (!G.Ok(a) || a.isKing() || a.isCityLeader()) continue;
                    if (!G.Chance(share * mult)) continue;
                    a.getHit(a.getMaxHealth() * 10f + 1000f, true, AttackType.Other, null, false, false, false);
                    budget--;
                }
            }
        }

        public static Actor Great(Kingdom k, string role, City city)
        {
            City c = city ?? k.capital;
            if (c == null) return null;
            Actor pick = c.units.Where(a => G.Ok(a) && a.isAdult() && !a.isKing() && !a.isCityLeader() && Store.Str(a.data, KGreatRole).Length == 0).OrderByDescending(a => a.getAge() < 40 ? 1 : 0).FirstOrDefault();
            if (pick == null) return null;
            Store.Put(pick.data, KGreatRole, role);
            HashSet<string> set = Store.Set(k.data, KGreats);
            set.Add(G.Id(pick) + "|" + role + "|" + pick.getName());
            Store.Set(k.data, KGreats, set);
            Bonus.Dirty(k);
            if (role == "explorer") Fog.Explore(k, 6);
            Hub.Raise(Hub.GreatPerson, k, null, c, pick, role, "born");
            return pick;
        }

        public static string GreatRole(Actor a)
        {
            return G.Ok(a) ? Store.Str(a.data, KGreatRole) : "";
        }

        private static void GreatsYear(List<Kingdom> ks)
        {
            foreach (Kingdom k in ks)
            {
                HashSet<string> set = Store.Set(k.data, KGreats);
                if (set.Count == 0) continue;
                bool changed = false;
                foreach (string entry in set.ToList())
                {
                    string[] parts = entry.Split('|');
                    Actor a = G.ActorById(parts[0]);
                    if (G.Ok(a) && a.kingdom == k) continue;
                    set.Remove(entry);
                    changed = true;
                    Hub.Raise(Hub.GreatPerson, k, null, k.capital, null, parts.Length > 1 ? parts[1] : "", parts.Length > 2 ? "died:" + parts[2] : "died");
                }
                if (changed)
                {
                    Store.Set(k.data, KGreats, set);
                    Bonus.Dirty(k);
                }
            }
        }
    }
}
