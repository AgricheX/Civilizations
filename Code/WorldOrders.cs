using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class OrderDef
    {
        public string Id = "";
        public int MinEra;
        public int MaxEra = 99;
    }

    internal class WorldOrder
    {
        public string Type = "";
        public long Leader;
        public long Second = -1;
        public int Since;
        public float Legit = 0.7f;
        public int Failing;
        public List<long> Members = new List<long>();
        public List<long> Outlaws = new List<long>();
        public List<string> Bans = new List<string>();
        public Dictionary<string, int> Strikes = new Dictionary<string, int>();
        public string Key = "";
    }

    internal static class WorldOrders
    {
        private const string KData = "civ_world_orders";
        private const string KWW = "civ_world_war_year";
        private const string KNukeYear = "civ_nuke_used_year";
        public static readonly Dictionary<string, OrderDef> Defs = new Dictionary<string, OrderDef>();
        private static List<WorldOrder> _list = new List<WorldOrder>();
        private static bool _loaded;

        private static readonly HashSet<string> Governance = new HashSet<string> { "universal_empire", "hegemonic_peace", "world_federation", "technocratic_directorate", "corporate_consortium", "revolutionary_commune" };
        private static readonly HashSet<string> Councils = new HashSet<string> { "concert_of_powers", "league_of_kingdoms", "united_council" };
        private static readonly HashSet<string> Harsh = new HashSet<string> { "universal_empire", "tributary_order", "religious_arbitration", "hegemonic_peace", "revolutionary_commune" };

        public static IEnumerable<WorldOrder> Active
        {
            get
            {
                Ensure();
                return _list;
            }
        }

        public static void InitData(Newtonsoft.Json.Linq.JToken tok)
        {
            Defs.Clear();
            if (tok == null) return;
            foreach (OrderDef d in tok.ToObject<List<OrderDef>>()) Defs[d.Id] = d;
        }

        public static void Clear()
        {
            _loaded = false;
            _list = new List<WorldOrder>();
        }

        private static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            SaveCustomData d = Store.World();
            string s = d != null ? Store.Str(d, KData) : "";
            if (s.Length == 0) return;
            try
            {
                _list = JsonConvert.DeserializeObject<List<WorldOrder>>(s) ?? new List<WorldOrder>();
            }
            catch (Exception)
            {
                _list = new List<WorldOrder>();
            }
        }

        public static void Save()
        {
            if (!_loaded) return;
            SaveCustomData d = Store.World();
            if (d != null) Store.Put(d, KData, JsonConvert.SerializeObject(_list));
        }

        private static Kingdom K(long id)
        {
            Kingdom k = World.world.kingdoms.get(id);
            return G.Ok(k) ? k : null;
        }

        public static WorldOrder Of(Kingdom k)
        {
            if (k == null || k.data == null) return null;
            Ensure();
            foreach (WorldOrder o in _list) if (o.Leader == k.data.id || o.Second == k.data.id || o.Members.Contains(k.data.id)) return o;
            return null;
        }

        public static bool Sanctioned(Kingdom a, Kingdom b)
        {
            Ensure();
            foreach (WorldOrder o in _list)
            {
                bool am = o.Members.Contains(a.data.id) || o.Leader == a.data.id, bm = o.Members.Contains(b.data.id) || o.Leader == b.data.id;
                if (am && o.Outlaws.Contains(b.data.id) || bm && o.Outlaws.Contains(a.data.id)) return true;
            }
            return false;
        }

        public static bool Banned(Kingdom k, string tech)
        {
            WorldOrder o = Of(k);
            return o != null && o.Bans.Contains(tech);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var m = new Dictionary<string, float>();
            Ensure();
            foreach (WorldOrder o in _list)
            {
                bool member = o.Members.Contains(k.data.id) || o.Leader == k.data.id || o.Second == k.data.id;
                if (o.Outlaws.Contains(k.data.id))
                {
                    Store.Add(m, "trade", -10f);
                    Store.Add(m, "stability", -4f);
                    continue;
                }
                if (!member) continue;
                switch (o.Type)
                {
                    case "universal_empire":
                    case "tributary_order":
                        if (o.Leader == k.data.id) Store.Add(m, "prestige", 12f);
                        else Store.Add(m, "trade", 4f);
                        break;
                    case "religious_arbitration": Store.Add(m, "stability", 4f); break;
                    case "concert_of_powers": Store.Add(m, "stability", 3f); break;
                    case "league_of_kingdoms": Store.Add(m, "trade", 3f); break;
                    case "united_council":
                        Store.Add(m, "stability", 4f);
                        Store.Add(m, "trade", 3f);
                        break;
                    case "world_federation":
                        Store.Add(m, "trade", 10f);
                        Store.Add(m, "growth", 3f);
                        break;
                    case "technocratic_directorate": Store.Add(m, "research", 8f); break;
                    case "corporate_consortium":
                        Store.Add(m, "trade", 8f);
                        Store.Add(m, "gold", 4f);
                        break;
                    case "hegemonic_peace":
                        if (o.Leader == k.data.id) Store.Add(m, "prestige", 10f);
                        Store.Add(m, "defense", 4f);
                        break;
                    case "bipolar_blocs":
                        Store.Add(m, "defense", 5f);
                        Store.Add(m, "air", 3f);
                        break;
                    case "revolutionary_commune":
                        Store.Add(m, "prod", 5f);
                        Store.Add(m, "loyalty", 3f);
                        break;
                }
            }
            return m;
        }

        public static void Register()
        {
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_order",
                translation_key = "opinion_civ_order",
                translation_key_negative = "opinion_civ_order_bad",
                calc = delegate(Kingdom a, Kingdom b)
                {
                    if (a == null || b == null || a == b || a.data == null || b.data == null) return 0;
                    Ensure();
                    int v = 0;
                    foreach (WorldOrder o in _list)
                    {
                        bool am = o.Members.Contains(a.data.id) || o.Leader == a.data.id;
                        if (!am) continue;
                        if (o.Outlaws.Contains(b.data.id)) v -= 40;
                        if (o.Type == "bipolar_blocs" && BlocOf(o, a) != BlocOf(o, b) && BlocOf(o, b) != 0) v -= 25;
                    }
                    return v;
                }
            });
            Hub.On(Hub.WarStarted, e =>
            {
                if (e.K == null) return;
                int involved = 0;
                foreach (global::War w in e.K.getWars()) involved = Math.Max(involved, w.data.list_attackers.Count + w.data.list_defenders.Count);
                if (involved >= 5 || Research.Era(e.K) >= 10 && involved >= 4)
                {
                    SaveCustomData d = Store.World();
                    if (d != null) Store.Put(d, KWW, G.Year);
                }
                foreach (WorldOrder o in Active.ToList())
                {
                    if (e.O != null && (o.Members.Contains(e.O.data.id) || o.Leader == e.O.data.id) && (o.Members.Contains(e.K.data.id) || o.Leader == e.K.data.id) && o.Type != "bipolar_blocs") Strike(o, e.K, 1);
                }
            });
            Hub.On(Hub.NukeDetonated, e =>
            {
                SaveCustomData d = Store.World();
                if (d != null) Store.Put(d, KNukeYear, G.Year);
                foreach (WorldOrder o in Active.ToList()) if (e.K != null && o.Leader != e.K.data.id) Strike(o, e.K, 3);
            });
        }

        public static int BlocOf(WorldOrder o, Kingdom k)
        {
            if (o == null || k == null || k.data == null) return 0;
            if (o.Leader == k.data.id) return 1;
            if (o.Second == k.data.id) return 2;
            if (!o.Members.Contains(k.data.id)) return 0;
            Kingdom a = K(o.Leader), b = K(o.Second);
            if (a == null || b == null) return 1;
            int oa = 0, ob = 0;
            try
            {
                oa = World.world.diplomacy.getOpinion(k, a).total;
                ob = World.world.diplomacy.getOpinion(k, b).total;
            }
            catch (Exception)
            {
            }
            return oa >= ob ? 1 : 2;
        }

        private static void Strike(WorldOrder o, Kingdom k, int n)
        {
            if (k == null || k.data == null) return;
            string id = k.data.id.ToString();
            int cur;
            o.Strikes.TryGetValue(id, out cur);
            o.Strikes[id] = cur + n;
        }

        private class World_
        {
            public List<Kingdom> Ks = new List<Kingdom>();
            public float Total;
            public Kingdom Top, Second;
            public float TopShare, SecondShare;
            public int TopEra;
            public float DemShare, RevShare, TechShare;
            public string MainReligion = "";
            public float ReligionShare;
            public bool WorldWar, NukeUsed;
        }

        private static World_ Survey()
        {
            var w = new World_();
            w.Ks = G.Kingdoms().Where(x => x.isCiv() && G.Cities(x).Count > 0).ToList();
            var rel = new Dictionary<string, float>();
            foreach (Kingdom k in w.Ks)
            {
                KProfile p = Profile.Get(k);
                float s = p.Strength;
                w.Total += s;
                if (p.Fam.Contains("democratic")) w.DemShare += s;
                if (p.Fam.Contains("revolutionary")) w.RevShare += s;
                if (p.Fam.Contains("technocratic")) w.TechShare += s;
                if (p.Religion.Length > 0) Store.Add(rel, p.Religion, 1f);
            }
            if (w.Total <= 0f) return w;
            var sorted = w.Ks.OrderByDescending(k => Profile.Get(k).Strength).ToList();
            if (sorted.Count > 0)
            {
                w.Top = sorted[0];
                w.TopShare = Profile.Get(w.Top).Strength / w.Total;
                w.TopEra = Profile.Get(w.Top).Era;
            }
            if (sorted.Count > 1)
            {
                w.Second = sorted[1];
                w.SecondShare = Profile.Get(w.Second).Strength / w.Total;
            }
            w.DemShare /= w.Total;
            w.RevShare /= w.Total;
            w.TechShare /= w.Total;
            if (rel.Count > 0)
            {
                var best = rel.OrderByDescending(kv => kv.Value).First();
                w.MainReligion = best.Key;
                w.ReligionShare = best.Value / Math.Max(1f, w.Ks.Count);
            }
            SaveCustomData d = Store.World();
            w.WorldWar = d != null && G.Year - Store.Int(d, KWW, -9999) < 40;
            w.NukeUsed = d != null && G.Year - Store.Int(d, KNukeYear, -9999) < 40;
            return w;
        }

        private static bool Holds(string id, World_ w, WorldOrder existing)
        {
            if (w.Top == null) return false;
            OrderDef def;
            if (!Defs.TryGetValue(id, out def)) return false;
            int era = w.TopEra;
            if (era < def.MinEra || era > def.MaxEra + (existing != null ? 1 : 0)) return false;
            float slack = existing != null ? 0.8f : 1f;
            KProfile tp = Profile.Get(w.Top);
            switch (id)
            {
                case "universal_empire": return w.TopShare >= 0.45f * slack && (tp.Fam.Contains("monarchy") || tp.Fam.Contains("authoritarian") || tp.Fam.Contains("imperial"));
                case "tributary_order": return w.TopShare >= 0.3f * slack && w.TopShare >= w.SecondShare * 2.5f * slack;
                case "religious_arbitration": return w.ReligionShare >= 0.5f * slack && w.Ks.Count >= 3;
                case "concert_of_powers": return w.Ks.Count(k => Profile.Get(k).Strength / w.Total >= 0.12f) >= 3;
                case "league_of_kingdoms": return w.WorldWar || existing != null;
                case "united_council": return w.WorldWar || w.NukeUsed || existing != null;
                case "world_federation": return w.DemShare >= 0.6f * slack;
                case "technocratic_directorate": return w.TechShare >= 0.3f * slack;
                case "corporate_consortium": return tp.Gov == "corporatocracy" || Companies.Active().Count >= 25;
                case "hegemonic_peace": return w.TopShare >= 0.4f * slack && w.TopShare >= w.SecondShare * 3f * slack;
                case "bipolar_blocs":
                    if (w.Second == null || w.SecondShare < 0.2f * slack || w.TopShare < 0.2f * slack) return false;
                    KProfile sp = Profile.Get(w.Second);
                    return (tp.Fam.Contains("democratic") && (sp.Fam.Contains("authoritarian") || sp.Fam.Contains("revolutionary"))) || (sp.Fam.Contains("democratic") && (tp.Fam.Contains("authoritarian") || tp.Fam.Contains("revolutionary")));
                case "revolutionary_commune": return w.RevShare >= 0.4f * slack;
            }
            return false;
        }

        private static bool Conflicts(string a, string b)
        {
            if (a == b) return true;
            if (Governance.Contains(a) && Governance.Contains(b)) return true;
            if (Councils.Contains(a) && Councils.Contains(b)) return true;
            if ((a == "bipolar_blocs" && (b == "hegemonic_peace" || b == "world_federation" || b == "universal_empire")) || (b == "bipolar_blocs" && (a == "hegemonic_peace" || a == "world_federation" || a == "universal_empire"))) return true;
            if ((a == "universal_empire" && b == "tributary_order") || (b == "universal_empire" && a == "tributary_order")) return true;
            return false;
        }

        public static void Year()
        {
            Ensure();
            if (Defs.Count == 0) return;
            World_ w = Survey();
            if (w.Top == null) return;
            foreach (WorldOrder o in _list.ToList())
            {
                if (K(o.Leader) == null)
                {
                    if (o.Members.Count > 0 && Councils.Contains(o.Type))
                    {
                        o.Leader = o.Members[0];
                    }
                    else
                    {
                        Fall(o, "leader_gone");
                        continue;
                    }
                }
                o.Failing = Holds(o.Type, w, o) ? 0 : o.Failing + 1;
                if (o.Failing >= 10 || o.Legit < 0.12f)
                {
                    string next = o.Type == "league_of_kingdoms" && w.TopEra >= 12 ? "united_council" : o.Type == "universal_empire" ? "concert_of_powers" : "";
                    Fall(o, o.Legit < 0.12f ? "illegitimate" : "conditions");
                    if (next.Length > 0 && Holds(next, w, null)) Rise(next, w);
                    continue;
                }
                Members(o, w);
                CivMod.Safe("order-powers", () => Powers(o, w));
                o.Legit = G.Clamp(o.Legit + 0.01f, 0f, 1f);
            }
            if (!G.Chance(0.25f)) return;
            foreach (OrderDef d in Defs.Values)
            {
                if (_list.Any(o => Conflicts(o.Type, d.Id))) continue;
                if (!Holds(d.Id, w, null)) continue;
                Rise(d.Id, w);
                return;
            }
        }

        private static void Rise(string id, World_ w)
        {
            var o = new WorldOrder { Type = id, Leader = w.Top.data.id, Since = G.Year };
            if (id == "bipolar_blocs" && w.Second != null) o.Second = w.Second.data.id;
            if (id == "religious_arbitration") o.Key = w.MainReligion;
            if (id == "league_of_kingdoms" || id == "united_council" || id == "concert_of_powers")
            {
                Kingdom founder = w.Ks.Where(k => Profile.Get(k).Fam.Contains("democratic")).OrderByDescending(k => Profile.Get(k).Strength).FirstOrDefault();
                if (founder != null) o.Leader = founder.data.id;
            }
            _list.Add(o);
            Members(o, w);
            foreach (long m in o.Members.Concat(new[] { o.Leader, o.Second }))
            {
                Kingdom k = K(m);
                if (k != null) Bonus.Dirty(k);
            }
            Hub.Raise(Hub.WorldOrder, K(o.Leader), K(o.Second), K(o.Leader) != null ? K(o.Leader).capital : null, null, id, "rise");
        }

        private static void Fall(WorldOrder o, string why)
        {
            _list.Remove(o);
            foreach (long m in o.Members.Concat(new[] { o.Leader, o.Second }))
            {
                Kingdom k = K(m);
                if (k != null) Bonus.Dirty(k);
            }
            Hub.Raise(Hub.WorldOrderFell, K(o.Leader), null, null, null, o.Type, why);
        }

        private static void Members(WorldOrder o, World_ w)
        {
            Kingdom lead = K(o.Leader);
            var keep = new List<long>();
            foreach (Kingdom k in w.Ks)
            {
                if (k == lead || (o.Second >= 0 && k.data.id == o.Second) || o.Outlaws.Contains(k.data.id)) continue;
                KProfile p = Profile.Get(k);
                bool join = false;
                switch (o.Type)
                {
                    case "universal_empire":
                    case "tributary_order":
                    case "hegemonic_peace":
                        join = lead != null && (Fog.Knows(lead, k) && (Diplomacy.Overlord(k) == lead || Opinion(k, lead) >= 0));
                        break;
                    case "religious_arbitration": join = p.Religion == o.Key; break;
                    case "concert_of_powers": join = p.Strength / Math.Max(1f, w.Total) >= 0.08f; break;
                    case "league_of_kingdoms": join = p.Era >= 9 && !p.Fam.Contains("tribal"); break;
                    case "united_council": join = p.Era >= 10; break;
                    case "world_federation": join = p.Fam.Contains("democratic") || p.Fam.Contains("liberal"); break;
                    case "technocratic_directorate": join = p.Fam.Contains("technocratic") || p.Era >= 13 && G.Chance(0.3f); break;
                    case "corporate_consortium": join = p.Fam.Contains("corporate") || Companies.Of(k).Count >= 3; break;
                    case "bipolar_blocs": join = p.Era >= 10 && Diplomacy.DepOf(k) == null || Diplomacy.Overlord(k) != null; break;
                    case "revolutionary_commune": join = p.Fam.Contains("revolutionary") || p.Fam.Contains("leftist"); break;
                }
                if (join) keep.Add(k.data.id);
            }
            o.Members = keep;
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

        private static void Powers(WorldOrder o, World_ w)
        {
            Kingdom lead = K(o.Leader);
            if (lead == null) return;
            var members = o.Members.Select(K).Where(x => x != null).ToList();
            members.Add(lead);
            foreach (Kingdom m in members.ToList())
            {
                foreach (global::War war in m.getWars().ToList())
                {
                    Kingdom a = war.getMainAttacker(), d = war.getMainDefender();
                    if (!G.Ok(a) || !G.Ok(d)) continue;
                    bool bothIn = members.Contains(a) && members.Contains(d);
                    if (bothIn && o.Type != "bipolar_blocs" && G.Chance(0.2f))
                    {
                        Strike(o, a, 1);
                        World.world.wars.endWar(war, WarWinner.Peace);
                        Hub.Raise(Hub.OrgVote, lead, a, lead.capital, null, "arbitration", "order:" + o.Type, 1f);
                        continue;
                    }
                    if (members.Contains(d) && !members.Contains(a)) Strike(o, a, 1);
                }
            }
            foreach (var kv in o.Strikes.ToList())
            {
                long id;
                if (!long.TryParse(kv.Key, out id)) continue;
                Kingdom t = K(id);
                if (t == null)
                {
                    o.Strikes.Remove(kv.Key);
                    continue;
                }
                if (kv.Value >= 2 && !o.Outlaws.Contains(id) && t != lead)
                {
                    o.Outlaws.Add(id);
                    o.Members.Remove(id);
                    Bonus.Dirty(t);
                    Hub.Raise(Hub.Sanctions, t, lead, t.capital, null, "order:" + o.Type, kv.Value.ToString());
                }
                if (kv.Value >= 4 && G.Chance(0.25f)) Intervene(o, lead, t, members);
                if (kv.Value >= 6) Erasure(o, lead, t, members);
            }
            if (o.Type == "united_council" || o.Type == "world_federation" || o.Type == "technocratic_directorate")
            {
                if (!o.Bans.Contains("atomic_bomb") && G.Chance(0.05f))
                {
                    o.Bans.Add("atomic_bomb");
                    Hub.Raise(Hub.OrgVote, lead, null, lead.capital, null, "tech_ban", "order:" + o.Type, 1f);
                }
            }
            if (o.Type == "religious_arbitration" && G.Chance(0.05f))
            {
                Kingdom heretic = w.Ks.FirstOrDefault(k => Profile.Get(k).Religion != o.Key && Profile.Get(k).Religion.Length > 0 && Fog.Knows(lead, k));
                if (heretic != null) Strike(o, heretic, 1);
            }
            foreach (long id in o.Outlaws.ToList())
            {
                Kingdom t = K(id);
                if (t == null || G.Chance(0.04f))
                {
                    o.Outlaws.Remove(id);
                    o.Strikes.Remove(id.ToString());
                    if (t != null) Bonus.Dirty(t);
                }
            }
        }

        private static void Intervene(WorldOrder o, Kingdom lead, Kingdom target, List<Kingdom> members)
        {
            if (World.world.wars.isInWarWith(lead, target)) return;
            Hub.Raise(Hub.OrgVote, lead, target, target.capital, null, "intervention", "order:" + o.Type, 1f);
            foreach (Kingdom m in members)
            {
                if (m == target || World.world.wars.isInWarWith(m, target) || Diplomacy.Forbids(m, target)) continue;
                if (m == lead || G.Chance(0.4f)) World.world.diplomacy.startWar(m, target, WarTypeLibrary.normal);
            }
        }

        private static void Erasure(WorldOrder o, Kingdom lead, Kingdom target, List<Kingdom> members)
        {
            if (!Harsh.Contains(o.Type)) return;
            KProfile lp = Profile.Get(lead);
            if (!(lp.Fam.Contains("authoritarian") || lp.Fam.Contains("theocratic") || lp.Fam.Contains("military") || lp.Fam.Contains("revolutionary"))) return;
            if (!G.Chance(0.02f)) return;
            City city = G.Cities(target).OrderBy(c => c.getPopulationPeople()).FirstOrDefault(c => c != target.capital) ?? target.capital;
            if (city == null) return;
            foreach (Building b in city.buildings.ToList())
            {
                if (b != null && b.isAlive()) b.startDestroyBuilding();
            }
            int kill = 0;
            foreach (Actor a in city.units.ToList())
            {
                if (!G.Ok(a) || !G.Chance(0.6f)) continue;
                a.getHit(a.getMaxHealth() * 10f + 1000f, true, AttackType.Other, null, false, false, false);
                kill++;
            }
            o.Legit = Math.Max(0f, o.Legit - 0.4f);
            o.Strikes[target.data.id.ToString()] = 2;
            foreach (Kingdom k in G.Kingdoms()) foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_erasure_fear", 0.15f);
            Hub.Raise(Hub.Erasure, lead, target, city, null, o.Type, G.Name(city), kill);
        }
    }
}
