using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class OrgType
    {
        public string Id = "";
        public string Cat = "";
        public Rule Rule = new Rule();
        public Dictionary<string, float> Eff = new Dictionary<string, float>();
        public List<string> Votes = new List<string>();
        public List<string> Same = new List<string>();
        public bool Defense;
        public bool Trade;
        public float Share;
        public int Opinion = 15;
        public bool Unique;
        public string Rival = "";
        public string Next = "";
        public bool Hidden;
        public int MinMembers = 2;
        public int MaxMembers = 40;
        public string Icon = "";
    }

    internal class Org
    {
        public int Id;
        public string Type = "";
        public long Founder;
        public long Leader;
        public string Hq = "";
        public int Founded;
        public string Key = "";
        public List<long> Members = new List<long>();
        public List<string> Bans = new List<string>();
        public long Enemy = -1;
        public long Embargo = -1;
        public int LastVote;
        public int Dues = 2;
    }

    internal static class Orgs
    {
        private const string KData = "civ_orgs";
        public static readonly List<OrgType> Types = new List<OrgType>();
        public static readonly Dictionary<string, OrgType> TypeById = new Dictionary<string, OrgType>();
        private static List<Org> _list = new List<Org>();
        private static bool _loaded;
        private static int _next = 1;
        private static readonly Dictionary<long, List<Org>> _byKingdom = new Dictionary<long, List<Org>>();

        public static IEnumerable<Org> All
        {
            get
            {
                Ensure();
                return _list;
            }
        }

        public static void InitData()
        {
            Types.Clear();
            TypeById.Clear();
            try
            {
                string p = Path.Combine(CivMod.Folder, "Data", "orgs.json");
                if (!File.Exists(p)) return;
                var root = JsonConvert.DeserializeObject<Dictionary<string, Newtonsoft.Json.Linq.JToken>>(File.ReadAllText(p));
                foreach (OrgType t in root["orgs"].ToObject<List<OrgType>>())
                {
                    Types.Add(t);
                    TypeById[t.Id] = t;
                }
                WorldOrders.InitData(root["orders"]);
            }
            catch (Exception e)
            {
                CivMod.Log("orgs data: " + e.Message);
            }
        }

        public static void Clear()
        {
            _loaded = false;
            _list = new List<Org>();
            _byKingdom.Clear();
        }

        private static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            SaveCustomData d = Store.World();
            string s = d != null ? Store.Str(d, KData) : "";
            if (s.Length > 0)
            {
                try
                {
                    _list = JsonConvert.DeserializeObject<List<Org>>(s) ?? new List<Org>();
                }
                catch (Exception)
                {
                    _list = new List<Org>();
                }
            }
            foreach (Org o in _list) _next = Math.Max(_next, o.Id + 1);
            Reindex();
        }

        public static void Save()
        {
            if (!_loaded) return;
            SaveCustomData d = Store.World();
            if (d != null) Store.Put(d, KData, JsonConvert.SerializeObject(_list));
        }

        private static void Reindex()
        {
            _byKingdom.Clear();
            foreach (Org o in _list)
            {
                foreach (long m in o.Members)
                {
                    List<Org> l;
                    if (!_byKingdom.TryGetValue(m, out l)) _byKingdom[m] = l = new List<Org>();
                    l.Add(o);
                }
            }
        }

        private static Kingdom K(long id)
        {
            Kingdom k = World.world.kingdoms.get(id);
            return G.Ok(k) ? k : null;
        }

        public static OrgType TypeOf(Org o)
        {
            OrgType t;
            return o != null && TypeById.TryGetValue(o.Type, out t) ? t : null;
        }

        public static List<Org> Of(Kingdom k)
        {
            Ensure();
            List<Org> l;
            return k != null && k.data != null && _byKingdom.TryGetValue(k.data.id, out l) ? l : new List<Org>();
        }

        public static string Name(Org o)
        {
            string baseName = Txt.T("civ_org_" + o.Type);
            Kingdom f = K(o.Founder);
            City hq = G.CityById(o.Hq);
            if (hq != null) return Txt.F("civ_org_named", baseName, G.Name(hq));
            return f != null ? Txt.F("civ_org_named", baseName, G.Name(f)) : baseName;
        }

        public static bool Shares(Kingdom a, Kingdom b, Func<OrgType, bool> filter)
        {
            if (a == null || b == null || a.data == null || b.data == null) return false;
            foreach (Org o in Of(a))
            {
                OrgType t = TypeOf(o);
                if (t != null && filter(t) && o.Members.Contains(b.data.id)) return true;
            }
            return false;
        }

        public static bool Embargoed(Kingdom a, Kingdom b)
        {
            if (a == null || b == null || a.data == null || b.data == null) return false;
            foreach (Org o in Of(a)) if (o.Embargo == b.data.id) return true;
            foreach (Org o in Of(b)) if (o.Embargo == a.data.id) return true;
            return WorldOrders.Sanctioned(a, b);
        }

        public static bool Banned(Kingdom k, string tech)
        {
            foreach (Org o in Of(k)) if (o.Bans.Contains(tech)) return true;
            return WorldOrders.Banned(k, tech);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var m = new Dictionary<string, float>();
            foreach (Org o in Of(k))
            {
                OrgType t = TypeOf(o);
                if (t == null) continue;
                foreach (var kv in t.Eff) Store.Add(m, kv.Key, kv.Value);
            }
            foreach (var kv in WorldOrders.Effects(k)) Store.Add(m, kv.Key, kv.Value);
            return m;
        }

        public static IEnumerable<Kingdom> Allies(Kingdom k)
        {
            var seen = new HashSet<long>();
            foreach (Org o in Of(k))
            {
                OrgType t = TypeOf(o);
                if (t == null || !t.Defense) continue;
                foreach (long m in o.Members)
                {
                    if (m == k.data.id || !seen.Add(m)) continue;
                    Kingdom x = K(m);
                    if (x != null) yield return x;
                }
            }
        }

        public static IEnumerable<Kingdom> ResearchPartners(Kingdom k)
        {
            foreach (Org o in Of(k))
            {
                OrgType t = TypeOf(o);
                if (t == null || t.Share <= 0f) continue;
                foreach (long m in o.Members)
                {
                    if (m == k.data.id) continue;
                    Kingdom x = K(m);
                    if (x != null) yield return x;
                }
            }
        }

        private static string KeyFor(Kingdom k, OrgType t)
        {
            if (t.Same.Count == 0) return "";
            KProfile p = Profile.Get(k);
            var parts = new List<string>();
            foreach (string s in t.Same)
            {
                switch (s)
                {
                    case "religion": parts.Add(p.Religion); break;
                    case "species": parts.Add(p.Species); break;
                    case "bloc": parts.Add(p.Fam.Contains("democratic") ? "west" : p.Fam.Contains("revolutionary") || p.Fam.Contains("leftist") ? "east" : p.Fam.Contains("authoritarian") ? "east" : "west"); break;
                }
            }
            return string.Join("|", parts);
        }

        private static bool Fits(Kingdom k, OrgType t, string key)
        {
            if (!G.Ok(k) || !k.isCiv()) return false;
            if (!Coherence.Allows(k, t.Rule)) return false;
            if (t.Same.Count > 0 && KeyFor(k, t) != key) return false;
            if (t.Same.Contains("religion") && Profile.Get(k).Religion.Length == 0) return false;
            return true;
        }

        private static bool InRival(Kingdom k, OrgType t)
        {
            if (string.IsNullOrEmpty(t.Rival)) return false;
            foreach (Org o in Of(k)) if (o.Type == t.Rival) return true;
            return false;
        }

        private static int Opinion(Kingdom a, Kingdom b)
        {
            try
            {
                KingdomOpinion op = World.world.diplomacy.getOpinion(a, b);
                return op != null ? op.total : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void Join(Org o, Kingdom k)
        {
            if (o.Members.Contains(k.data.id)) return;
            o.Members.Add(k.data.id);
            Reindex();
            Bonus.Dirty(k);
            Hub.Raise(Hub.OrgJoined, k, K(o.Leader), G.CityById(o.Hq), null, o.Type, o.Id.ToString());
        }

        private static void Leave(Org o, Kingdom k, string why)
        {
            if (k == null || !o.Members.Remove(k.data.id)) return;
            Reindex();
            Bonus.Dirty(k);
            Hub.Raise(Hub.OrgLeft, k, K(o.Leader), null, null, o.Type, why);
            if (o.Leader == k.data.id && o.Members.Count > 0) o.Leader = o.Members.OrderByDescending(m => K(m) != null ? Profile.Get(K(m)).Strength : 0f).First();
        }

        private static void Dissolve(Org o, string why)
        {
            _list.Remove(o);
            foreach (long m in o.Members)
            {
                Kingdom k = K(m);
                if (k != null) Bonus.Dirty(k);
            }
            Reindex();
            Hub.Raise(Hub.OrgDissolved, K(o.Leader), null, G.CityById(o.Hq), null, o.Type, why);
        }

        public static void Year()
        {
            Ensure();
            if (Types.Count == 0) return;
            foreach (Org o in _list.ToList()) CivMod.Safe("org-year", () => OrgYear(o));
            CivMod.Safe("org-found", Found);
        }

        private static void OrgYear(Org o)
        {
            OrgType t = TypeOf(o);
            if (t == null)
            {
                _list.Remove(o);
                Reindex();
                return;
            }
            foreach (long m in o.Members.ToList())
            {
                Kingdom k = K(m);
                if (k == null)
                {
                    o.Members.Remove(m);
                    continue;
                }
                if (!Fits(k, t, o.Key) && G.Chance(0.35f)) Leave(o, k, "incoherent");
            }
            Reindex();
            Kingdom leader = K(o.Leader);
            if (leader == null && o.Members.Count > 0)
            {
                o.Leader = o.Members[0];
                leader = K(o.Leader);
            }
            int era = leader != null ? Research.Era(leader) : 0;
            if (era > t.Rule.MaxEra)
            {
                OrgType next = string.IsNullOrEmpty(t.Next) ? null : TypeById.ContainsKey(t.Next) ? TypeById[t.Next] : null;
                if (next != null && leader != null && Fits(leader, next, KeyFor(leader, next)))
                {
                    string old = o.Type;
                    o.Type = next.Id;
                    o.Key = KeyFor(leader, next);
                    Hub.Raise(Hub.OrgFounded, leader, null, G.CityById(o.Hq), null, next.Id, old);
                    return;
                }
                Dissolve(o, "obsolete");
                return;
            }
            if (o.Members.Count < t.MinMembers && G.Year - o.Founded > 3)
            {
                Dissolve(o, "too_few");
                return;
            }
            if (o.Members.Count < t.MaxMembers) Recruit(o, t);
            Dues(o, t);
            if (G.Year - o.LastVote >= 5 && t.Votes.Count > 0 && G.Chance(0.4f)) Vote(o, t);
        }

        private static void Recruit(Org o, OrgType t)
        {
            var seen = new HashSet<long>(o.Members);
            int tries = 0;
            foreach (long m in o.Members.ToList())
            {
                Kingdom mk = K(m);
                if (mk == null) continue;
                foreach (Kingdom c in Research.Contacts(mk))
                {
                    if (tries++ > 12) return;
                    if (c == null || c.data == null || !seen.Add(c.data.id)) continue;
                    if (!Fog.Knows(mk, c) || !Fits(c, t, o.Key) || InRival(c, t)) continue;
                    bool war = false;
                    foreach (long x in o.Members) if (K(x) != null && World.world.wars.isInWarWith(c, K(x))) war = true;
                    if (war) continue;
                    float w = Coherence.Weight(Profile.Get(c), t.Rule);
                    if (Opinion(c, mk) < (t.Defense ? 15 : 0)) continue;
                    if (G.Chance(0.12f * w)) Join(o, c);
                }
            }
        }

        private static void Dues(Org o, OrgType t)
        {
            Kingdom leader = K(o.Leader);
            City hq = G.CityById(o.Hq) ?? (leader != null ? leader.capital : null);
            if (hq == null || o.Dues <= 0) return;
            foreach (long m in o.Members)
            {
                Kingdom k = K(m);
                if (k == null || k.capital == null || k == hq.kingdom) continue;
                int g = Math.Min(o.Dues * 3, Economy.Amount(k.capital, "gold") / 20);
                if (g > 0 && Economy.Take(k.capital, "gold", g)) Economy.Give(hq, "gold", g / 2);
            }
        }

        private static void Found()
        {
            if (_list.Count >= 80) return;
            List<Kingdom> ks = G.Kingdoms().Where(x => x.isCiv()).ToList();
            if (ks.Count < 2) return;
            for (int i = 0; i < 3; i++)
            {
                Kingdom k = G.Pick(ks);
                KProfile p = Profile.Get(k);
                var opts = new List<OrgType>();
                var ws = new List<float>();
                foreach (OrgType t in Types)
                {
                    if (!Coherence.Allows(k, t.Rule)) continue;
                    if (Of(k).Any(o => o.Type == t.Id)) continue;
                    if (t.Unique && _list.Any(o => o.Type == t.Id)) continue;
                    if (_list.Count(o => o.Type == t.Id) >= 4) continue;
                    if (InRival(k, t)) continue;
                    opts.Add(t);
                    ws.Add(Coherence.Weight(p, t.Rule));
                }
                if (opts.Count == 0) continue;
                OrgType pick = opts[G.Weighted(ws)];
                string key = KeyFor(k, pick);
                Kingdom partner = null;
                foreach (Kingdom c in Research.Contacts(k))
                {
                    if (!Fog.Knows(k, c) || !Fits(c, pick, key) || InRival(c, pick) || World.world.wars.isInWarWith(k, c)) continue;
                    if (Opinion(c, k) < 10 && Opinion(k, c) < 10) continue;
                    partner = c;
                    break;
                }
                bool solo = pick.Unique || pick.Cat == "cultural" || pick.Cat == "humanitarian" || pick.Hidden || pick.MinMembers <= 1;
                if (partner == null && !solo) continue;
                if (!G.Chance(0.08f)) continue;
                var org = new Org { Id = _next++, Type = pick.Id, Founder = k.data.id, Leader = k.data.id, Hq = k.capital != null ? G.Id(k.capital) : "", Founded = G.Year, Key = key };
                org.Members.Add(k.data.id);
                if (partner != null) org.Members.Add(partner.data.id);
                _list.Add(org);
                Reindex();
                Bonus.Dirty(k);
                if (partner != null) Bonus.Dirty(partner);
                Hub.Raise(Hub.OrgFounded, k, partner, k.capital, k.king, pick.Id, org.Id.ToString());
                return;
            }
        }

        private static void Vote(Org o, OrgType t)
        {
            o.LastVote = G.Year;
            string v = G.Pick(t.Votes);
            Kingdom leader = K(o.Leader);
            if (leader == null) return;
            Kingdom target = null;
            bool passed = false;
            switch (v)
            {
                case "common_enemy":
                case "intervention":
                    foreach (long m in o.Members)
                    {
                        Kingdom k = K(m);
                        if (k == null) continue;
                        foreach (global::War w in k.getWars())
                        {
                            Kingdom e = w.isAttacker(k) ? w.getMainDefender() : w.getMainAttacker();
                            if (G.Ok(e) && !o.Members.Contains(e.data.id)) target = e;
                        }
                    }
                    if (target != null)
                    {
                        passed = true;
                        o.Enemy = target.data.id;
                        if (v == "intervention" || t.Defense)
                        {
                            foreach (long m in o.Members)
                            {
                                Kingdom k = K(m);
                                if (k == null || World.world.wars.isInWarWith(k, target) || Diplomacy.Forbids(k, target)) continue;
                                if (G.Chance(0.5f)) World.world.diplomacy.startWar(k, target, WarTypeLibrary.normal);
                            }
                        }
                    }
                    break;
                case "embargo":
                    foreach (Kingdom k in G.Kingdoms())
                    {
                        if (!G.Ok(k) || o.Members.Contains(k.data.id)) continue;
                        if (War.UsedNukes(k) || (o.Enemy == k.data.id))
                        {
                            target = k;
                            break;
                        }
                    }
                    if (target != null)
                    {
                        o.Embargo = target.data.id;
                        passed = true;
                        Hub.Raise(Hub.Sanctions, target, leader, null, null, o.Type, o.Id.ToString());
                    }
                    break;
                case "expel":
                case "excommunication":
                    foreach (long m in o.Members)
                    {
                        Kingdom k = K(m);
                        if (k != null && k != leader && Legitimacy.Get(k) < 0.3f) target = k;
                    }
                    if (target != null)
                    {
                        Leave(o, target, v);
                        if (v == "excommunication") Store.Put(target.data, Legitimacy.KLegit, Math.Max(0f, Legitimacy.Get(target) - 0.15f));
                        passed = true;
                    }
                    break;
                case "tech_ban":
                case "arms_limits":
                    string ban = t.Cat == "religious" ? "printing_press" : "atomic_bomb";
                    if (t.Id == "internet_authority") ban = "artificial_intelligence";
                    if (!o.Bans.Contains(ban))
                    {
                        o.Bans.Add(ban);
                        passed = true;
                    }
                    break;
                case "aid":
                    Kingdom rich = null, poor = null;
                    foreach (long m in o.Members)
                    {
                        Kingdom k = K(m);
                        if (k == null || k.capital == null) continue;
                        int g = Economy.Amount(k.capital, "gold");
                        if (rich == null || g > Economy.Amount(rich.capital, "gold")) rich = k;
                        if (poor == null || g < Economy.Amount(poor.capital, "gold")) poor = k;
                    }
                    if (rich != null && poor != null && rich != poor)
                    {
                        int give = Economy.Amount(rich.capital, "gold") / 10;
                        if (give > 0 && Economy.Take(rich.capital, "gold", give)) Economy.Give(poor.capital, "gold", give);
                        target = poor;
                        passed = true;
                    }
                    break;
                case "arbitration":
                case "peace_mission":
                    foreach (long m in o.Members)
                    {
                        Kingdom k = K(m);
                        if (k == null) continue;
                        foreach (global::War w in k.getWars().ToList())
                        {
                            Kingdom e = w.isAttacker(k) ? w.getMainDefender() : w.getMainAttacker();
                            if (!G.Ok(e)) continue;
                            if (v == "arbitration" && !o.Members.Contains(e.data.id)) continue;
                            World.world.wars.endWar(w, WarWinner.Peace);
                            target = e;
                            passed = true;
                            break;
                        }
                        if (passed) break;
                    }
                    break;
                case "dues":
                    o.Dues = G.Range(1, 5);
                    passed = true;
                    break;
                case "admit":
                    Recruit(o, t);
                    passed = true;
                    break;
                case "host":
                    Kingdom h = K(G.Pick(o.Members));
                    if (h != null && h.capital != null)
                    {
                        o.Hq = G.Id(h.capital);
                        target = h;
                        passed = true;
                        foreach (City c in G.Cities(h)) Moods.Apply(c, "civ_festival", 0.15f);
                    }
                    break;
                default:
                    passed = G.Chance(0.6f);
                    break;
            }
            Hub.Raise(Hub.OrgVote, leader, target, G.CityById(o.Hq), null, v, o.Type, passed ? 1f : 0f);
        }

        public static void Register()
        {
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_orgs",
                translation_key = "opinion_civ_orgs",
                translation_key_negative = "opinion_civ_orgs_bad",
                calc = delegate(Kingdom a, Kingdom b)
                {
                    if (a == null || b == null || a == b || a.data == null || b.data == null) return 0;
                    int v = 0;
                    foreach (Org o in Of(a))
                    {
                        OrgType t = TypeOf(o);
                        if (t == null) continue;
                        if (o.Members.Contains(b.data.id)) v += t.Opinion;
                        if (o.Enemy == b.data.id) v -= 25;
                        if (!string.IsNullOrEmpty(t.Rival)) foreach (Org r in Of(b)) if (r.Type == t.Rival) v -= 15;
                    }
                    return Math.Max(-60, Math.Min(60, v));
                }
            });
            Hub.On(Hub.KingdomGone, e =>
            {
                if (e.K == null || e.K.data == null) return;
                foreach (Org o in Of(e.K).ToList()) o.Members.Remove(e.K.data.id);
                Reindex();
            });
        }
    }
}
