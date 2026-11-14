using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Civilizations
{
    internal class BillItem
    {
        public string Group;
        public string Id;
        public string Old;
    }

    internal class ClanTie
    {
        public float Politics;
        public float Marriage;
        public float Neighbors;
        public float Memory;
        public float Total { get { return G.Clamp(Politics + Marriage + Neighbors + Memory, -100f, 100f); } }
    }

    internal static class Parliament
    {
        private const string KX = "civ_px";
        private const string KY = "civ_py";
        private const string KMem = "civ_cmem";
        private const string KBloc = "civ_bloc";
        private const string KBill = "civ_bill";
        private const string KBillBy = "civ_bill_by";
        private const string KBillY = "civ_bill_y";
        private const string KNext = "civ_bill_next";
        private const string KCharter = "civ_charter";
        public const string KDecree = "civ_decree_y";

        private class SeatCache
        {
            public int Year;
            public Dictionary<long, int> Seats;
            public Dictionary<long, List<City>> Domains;
        }

        private static readonly Dictionary<long, SeatCache> _seats = new Dictionary<long, SeatCache>();
        private static readonly Dictionary<string, ClanTie> _ties = new Dictionary<string, ClanTie>();
        private static int _tieYear = -1;

        public static void Clear()
        {
            _seats.Clear();
            _ties.Clear();
            _tieYear = -1;
        }

        public static void Register()
        {
            Hub.On(Hub.CityCaptured, e =>
            {
                if (G.Ok(e.K)) _seats.Remove(e.K.data.id);
                if (G.Ok(e.O)) _seats.Remove(e.O.data.id);
            });
        }

        public static bool Active(Kingdom k)
        {
            return G.Ok(k) && k.isCiv() && Gov.On("civ_gov_parliament") && Profile.Get(k).Era >= 1;
        }

        public static int SeatsFor(City c)
        {
            if (!G.Ok(c)) return 0;
            int pop = Math.Max(1, c.getPopulationPeople());
            return Math.Max(1, (int)Math.Round(2.0 * Math.Sqrt(pop)));
        }

        public static long Holder(City c, Kingdom k)
        {
            if (!G.Ok(c)) return 0L;
            if (k != null && c == k.capital)
            {
                Clan royal = k.getKingClan();
                if (royal != null && royal.data != null) return royal.data.id;
            }
            if (G.Ok(c.leader) && c.leader.clan != null && c.leader.clan.data != null) return c.leader.clan.data.id;
            return 0L;
        }

        private static SeatCache Cache(Kingdom k)
        {
            SeatCache s;
            if (_seats.TryGetValue(k.data.id, out s) && s.Year == G.Year) return s;
            s = new SeatCache { Year = G.Year, Seats = new Dictionary<long, int>(), Domains = new Dictionary<long, List<City>>() };
            foreach (City c in G.Cities(k))
            {
                long h = Holder(c, k);
                int n;
                s.Seats.TryGetValue(h, out n);
                s.Seats[h] = n + SeatsFor(c);
                List<City> l;
                if (!s.Domains.TryGetValue(h, out l)) s.Domains[h] = l = new List<City>();
                l.Add(c);
            }
            _seats[k.data.id] = s;
            return s;
        }

        public static Dictionary<long, int> Seats(Kingdom k)
        {
            if (!G.Ok(k)) return new Dictionary<long, int>();
            return Cache(k).Seats;
        }

        public static Dictionary<long, List<City>> Domains(Kingdom k)
        {
            if (!G.Ok(k)) return new Dictionary<long, List<City>>();
            return Cache(k).Domains;
        }

        public static void Invalidate(Kingdom k)
        {
            if (G.Ok(k)) _seats.Remove(k.data.id);
        }

        public static int Total(Kingdom k)
        {
            return Seats(k).Values.Sum();
        }

        public static long RoyalId(Kingdom k)
        {
            if (!G.Ok(k)) return 0L;
            Clan c = k.getKingClan();
            return c != null && c.data != null ? c.data.id : 0L;
        }

        public static float[] Stance(long clan)
        {
            Clan c = Gov.ClanById(clan);
            if (c == null) return new[] { 0f, 0f };
            if (Store.Flt(c.data, KX, 9f) > 8f) Init(c, Clans.HomeOf(c));
            return new[] { Store.Flt(c.data, KX, 0f), Store.Flt(c.data, KY, 0f) };
        }

        private static void Init(Clan c, Kingdom k)
        {
            float[] t = Target(c, k);
            Store.Put(c.data, KX, t[0]);
            Store.Put(c.data, KY, t[1]);
        }

        public static float[] Target(Clan c, Kingdom k)
        {
            float x = 0f, y = 0f, w = 0f;
            Actor chief = c.getChief();
            Opinion o = G.Ok(chief) ? Politics.Get(chief) : null;
            if (o != null)
            {
                float[] a = Laws.Pos("gov", o.Gov), b = Laws.Pos("ideo", o.Ideo), d = Laws.Pos("econ", o.Econ);
                x = a[0] + b[0] * 1.4f + d[0];
                y = a[1] + b[1] * 1.4f + d[1];
                w = 3.4f;
                x = x / w + o.Xeno * 0.25f;
                y = y / w;
            }
            string line = Store.Str(c.data, Clans.KLine);
            if (line == "mil") y -= 0.15f;
            if (line == "econ" || line == "comm") x += 0.15f;
            if (line == "agri") x -= 0.15f;
            if (line == "sci") x += 0.2f;
            if (G.Ok(k))
            {
                long id = c.data.id;
                if (RoyalId(k) == id)
                {
                    float[] gov = Laws.Pos("gov", Policies.Id(k, "gov"));
                    x = x * 0.7f + gov[0] * 0.3f;
                    y -= 0.3f;
                }
                List<City> dom;
                if (Domains(k).TryGetValue(id, out dom) && dom.Count > 0 && k.capital != null)
                {
                    if (dom.Contains(k.capital)) y -= 0.35f;
                    float far = 0f;
                    foreach (City ct in dom) far += Math.Min(1f, G.Dist(ct, k.capital) / 120f);
                    y += far / dom.Count * 0.5f;
                }
            }
            return new[] { G.Clamp(x, -1f, 1f), G.Clamp(y, -1f, 1f) };
        }

        public static float Dist(float[] a, float[] b)
        {
            float dx = a[0] - b[0], dy = a[1] - b[1];
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static string FactionOf(Kingdom k, long clan)
        {
            float[] s = Stance(clan);
            return Gov.Faction(Gov.Band(k), Gov.Quadrant(s[0], s[1]));
        }

        private static string TieKey(long a, long b)
        {
            return a + ":" + b;
        }

        public static ClanTie Tie(long a, long b)
        {
            if (_tieYear != G.Year)
            {
                _ties.Clear();
                _tieYear = G.Year;
            }
            ClanTie t;
            string key = TieKey(a, b);
            if (_ties.TryGetValue(key, out t)) return t;
            t = new ClanTie();
            if (a == b)
            {
                t.Politics = 100f;
                _ties[key] = t;
                return t;
            }
            Clan ca = Gov.ClanById(a), cb = Gov.ClanById(b);
            if (ca == null || cb == null)
            {
                _ties[key] = t;
                return t;
            }
            t.Politics = 25f - Dist(Stance(a), Stance(b)) * 22f;
            int mar = 0;
            foreach (Actor u in ca.units)
            {
                if (!G.Ok(u) || u.lover == null || !G.Ok(u.lover)) continue;
                if (u.lover.clan == cb) mar++;
            }
            if (Store.Str(ca.data, Clans.KPact) == b.ToString()) mar += 2;
            t.Marriage = Math.Min(36f, mar * 12f);
            Kingdom k = Clans.HomeOf(ca);
            if (G.Ok(k))
            {
                var dom = Domains(k);
                List<City> la, lb;
                if (dom.TryGetValue(a, out la) && dom.TryGetValue(b, out lb))
                {
                    int adj = 0;
                    foreach (City x in la) foreach (City y in lb) if (x.neighbours_cities != null && x.neighbours_cities.Contains(y)) adj++;
                    t.Neighbors = -Math.Min(24f, adj * 6f);
                }
            }
            if (Store.Str(ca.data, Clans.KRival) == b.ToString()) t.Memory -= 25f;
            t.Memory += Store.Get(Store.Map(ca.data, KMem), b.ToString());
            _ties[key] = t;
            return t;
        }

        public static float Opinion(long a, long b)
        {
            return Tie(a, b).Total;
        }

        public static void Remember(long a, long b, float v)
        {
            if (a == 0L || b == 0L || a == b) return;
            Clan c = Gov.ClanById(a);
            if (c == null) return;
            Dictionary<string, float> m = Store.Map(c.data, KMem);
            Store.Add(m, b.ToString(), v);
            m[b.ToString()] = G.Clamp(m[b.ToString()], -60f, 60f);
            Store.Map(c.data, KMem, m);
            _ties.Remove(TieKey(a, b));
        }

        private static void Decay(Clan c, int yrs)
        {
            Dictionary<string, float> m = Store.Map(c.data, KMem);
            if (m.Count == 0) return;
            var n = new Dictionary<string, float>();
            float f = (float)Math.Pow(0.88, yrs);
            foreach (var kv in m)
            {
                float v = kv.Value * f;
                if (Math.Abs(v) >= 0.5f) n[kv.Key] = v;
            }
            Store.Map(c.data, KMem, n);
        }

        public static long BlocOf(long clan)
        {
            Clan c = Gov.ClanById(clan);
            if (c == null) return 0L;
            long b;
            return long.TryParse(Store.Str(c.data, KBloc), out b) ? b : 0L;
        }

        public static void SetBloc(long clan, long leader)
        {
            Clan c = Gov.ClanById(clan);
            if (c != null) Store.Put(c.data, KBloc, leader == 0L ? "" : leader.ToString());
        }

        public static List<long> Members(Kingdom k, long leader)
        {
            return Seats(k).Keys.Where(c => c != 0L && (c == leader || BlocOf(c) == leader)).ToList();
        }

        public static List<long> Leaders(Kingdom k)
        {
            return Seats(k).Keys.Where(c => c != 0L && BlocOf(c) == c).ToList();
        }

        public static List<BillItem> Bill(Kingdom k)
        {
            var l = new List<BillItem>();
            if (!G.Ok(k)) return l;
            string s = Store.Str(k.data, KBill);
            if (s.Length == 0) return l;
            foreach (string part in s.Split(';'))
            {
                string[] p = part.Split('=');
                if (p.Length < 2) continue;
                l.Add(new BillItem { Group = p[0], Id = p[1], Old = p.Length > 2 ? p[2] : Laws.Current(k, p[0]) });
            }
            return l;
        }

        public static long Proposer(Kingdom k)
        {
            long v;
            return G.Ok(k) && long.TryParse(Store.Str(k.data, KBillBy), out v) ? v : 0L;
        }

        public static int BillYear(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KBillY, G.Year) : G.Year;
        }

        public static int NextBill(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KNext, 0) : 0;
        }

        public static int Charter(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KCharter, 0) : 0;
        }

        private static void SaveBill(Kingdom k, long by, List<BillItem> items)
        {
            if (items == null || items.Count == 0)
            {
                Store.Put(k.data, KBill, "");
                Store.Put(k.data, KBillBy, "");
                return;
            }
            Store.Put(k.data, KBill, string.Join(";", items.Select(i => i.Group + "=" + i.Id + "=" + i.Old).ToArray()));
            Store.Put(k.data, KBillBy, by.ToString());
            Store.Put(k.data, KBillY, G.Year);
        }

        public static float Gain(float[] stance, string group, string from, string to)
        {
            return Dist(stance, Laws.Pos(group, from)) - Dist(stance, Laws.Pos(group, to));
        }

        public static string Preferred(Kingdom k, float[] stance, string group)
        {
            string best = null;
            float bd = float.MaxValue;
            foreach (string id in Laws.Choices(k, group))
            {
                float d = Dist(stance, Laws.Pos(group, id));
                if (d < bd)
                {
                    bd = d;
                    best = id;
                }
            }
            return best;
        }

        public static float Discontent(Kingdom k, long clan)
        {
            float[] s = Stance(clan);
            float sum = 0f;
            int n = 0;
            foreach (string g in Laws.AllGroups())
            {
                if (g == "transport") continue;
                string cur = Laws.Current(k, g), pref = Preferred(k, s, g);
                if (pref == null) continue;
                sum += Math.Max(0f, Gain(s, g, cur, pref));
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        public static bool VoteOf(Kingdom k, long clan, BillItem item, long proposer)
        {
            if (clan == 0L) return Politics.Satisfaction(k) < 0.5f || G.Ok(k) && Laws.Pos(item.Group, item.Id)[0] > Laws.Pos(item.Group, item.Old)[0];
            float score = Score(k, clan, item, proposer);
            long bloc = BlocOf(clan);
            if (bloc != 0L && bloc != clan && Math.Abs(score) < 0.2f) return VoteOf(k, bloc, item, proposer);
            return score > 0f;
        }

        public static float Score(Kingdom k, long clan, BillItem item, long proposer)
        {
            float s = Gain(Stance(clan), item.Group, item.Old, item.Id);
            if (proposer != 0L) s += Opinion(clan, proposer) / 250f;
            if (clan == proposer) s += 1f;
            if (BlocOf(clan) != 0L && BlocOf(clan) == BlocOf(proposer)) s += 0.15f;
            return s;
        }

        public static int[] Tally(Kingdom k, BillItem item, long proposer)
        {
            int yes = 0, no = 0;
            foreach (var kv in Seats(k))
            {
                if (VoteOf(k, kv.Key, item, proposer)) yes += kv.Value;
                else no += kv.Value;
            }
            return new[] { yes, no };
        }

        public static float Support(Kingdom k, string group, string id)
        {
            if (!Active(k)) return 1f;
            var item = new BillItem { Group = group, Id = id, Old = Laws.Current(k, group) };
            int[] t = Tally(k, item, RoyalId(k));
            int all = t[0] + t[1];
            return all > 0 ? (float)t[0] / all : 1f;
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            Invalidate(k);
            var seats = Seats(k);
            foreach (long id in seats.Keys)
            {
                Clan c = Gov.ClanById(id);
                if (c == null) continue;
                float[] t = Target(c, k);
                float f = Math.Min(1f, 0.25f * yrs);
                if (Store.Flt(c.data, KX, 9f) > 8f) Init(c, k);
                Store.Put(c.data, KX, G.Clamp(Store.Flt(c.data, KX, 0f) * (1f - f) + t[0] * f, -1f, 1f));
                Store.Put(c.data, KY, G.Clamp(Store.Flt(c.data, KY, 0f) * (1f - f) + t[1] * f, -1f, 1f));
                Decay(c, yrs);
                long bloc = BlocOf(id);
                if (bloc != 0L && bloc != id && (!seats.ContainsKey(bloc) || BlocOf(bloc) != bloc)) SetBloc(id, 0L);
            }
            _ties.Clear();
            if (p.Era < 3 || seats.Count == 0) return;
            Blocs(k, seats);
            List<BillItem> bill = Bill(k);
            if (bill.Count > 0)
            {
                if (Player.Is(k) && G.Year - BillYear(k) < 2) return;
                Resolve(k, false);
                return;
            }
            if (Charter(k) > G.Year) return;
            int next = NextBill(k);
            if (next == 0)
            {
                Store.Put(k.data, KNext, G.Year + G.Range(4, 6));
                return;
            }
            if (G.Year >= next) Propose(k);
        }

        private static void Blocs(Kingdom k, Dictionary<long, int> seats)
        {
            var big = seats.Where(x => x.Key != 0L).OrderByDescending(x => x.Value).ToList();
            if (big.Count < 3) return;
            long top = big[0].Key;
            if (BlocOf(top) == 0L) SetBloc(top, top);
            foreach (long leader in Leaders(k))
            {
                if (Plots.Has(k, "win_over", leader)) continue;
                long best = 0L;
                float bs = 20f;
                foreach (var kv in big)
                {
                    if (kv.Key == leader || BlocOf(kv.Key) != 0L) continue;
                    float o = Opinion(kv.Key, leader) - Dist(Stance(kv.Key), Stance(leader)) * 10f;
                    if (o > bs)
                    {
                        bs = o;
                        best = kv.Key;
                    }
                }
                if (best != 0L && G.Chance(0.35f)) Plots.Start(k, "win_over", leader, best, G.Range(1, 3), "politics");
            }
            foreach (var kv in big)
            {
                long b = BlocOf(kv.Key);
                if (b == 0L || b == kv.Key || Plots.Has(k, "leave_bloc", kv.Key)) continue;
                if (Opinion(kv.Key, b) < 0f || Dist(Stance(kv.Key), Stance(b)) > 1.1f) Plots.Start(k, "leave_bloc", kv.Key, b, G.Range(1, 2), Opinion(kv.Key, b) < 0f ? "grudge" : "politics");
            }
            var twin = big.Skip(1).FirstOrDefault(x => BlocOf(x.Key) == 0L && Opinion(x.Key, top) < 0f);
            if (twin.Key != 0L && G.Chance(0.3f)) SetBloc(twin.Key, twin.Key);
        }

        public static void WinOver(Kingdom k, long leader, long target)
        {
            if (Gov.ClanById(leader) == null || Gov.ClanById(target) == null || BlocOf(leader) != leader) return;
            float o = Opinion(target, leader);
            if (!G.Chance(G.Clamp(0.4f + o / 100f, 0.1f, 0.9f))) return;
            SetBloc(target, leader);
            Remember(target, leader, 6f);
            Gov.Log("bloc_joined", k, Gov.ClanName(target), Gov.ClanName(leader), Gov.BlocTerm(k));
        }

        public static void LeaveBloc(Kingdom k, long clan, long leader)
        {
            if (BlocOf(clan) != leader || clan == leader) return;
            SetBloc(clan, 0L);
            Remember(leader, clan, -8f);
            Gov.Log("bloc_left", k, Gov.ClanName(clan), Gov.ClanName(leader), Gov.BlocTerm(k));
        }

        public static bool Propose(Kingdom k)
        {
            var seats = Seats(k);
            long by = 0L;
            float best = 0f;
            foreach (var kv in seats)
            {
                if (kv.Key == 0L) continue;
                float d = Discontent(k, kv.Key) * kv.Value;
                if (d > best)
                {
                    best = d;
                    by = kv.Key;
                }
            }
            Store.Put(k.data, KNext, G.Year + G.Range(4, 6));
            if (by == 0L) return false;
            float[] s = Stance(by);
            var items = new List<KeyValuePair<float, BillItem>>();
            foreach (string g in Laws.AllGroups())
            {
                if (g == "transport" || Laws.GroupLocked(k, g)) continue;
                if (g == "gov" && Policies.Get(k, "gov") != null && Policies.Get(k, "gov").Elections == false && seats[by] < Total(k) / 3) continue;
                string cur = Laws.Current(k, g), pref = Preferred(k, s, g);
                if (pref == null || pref == cur) continue;
                float gain = Gain(s, g, cur, pref);
                if (gain < 0.15f) continue;
                items.Add(new KeyValuePair<float, BillItem>(gain, new BillItem { Group = g, Id = pref, Old = cur }));
            }
            if (items.Count == 0) return false;
            List<BillItem> bill = items.OrderByDescending(x => x.Key).Take(G.Range(1, 3)).Select(x => x.Value).ToList();
            SaveBill(k, by, bill);
            Gov.Log("bill_proposed", k, G.Name(k), Gov.ClanName(by), string.Join(", ", bill.Select(i => Laws.Name(i.Group, i.Id)).ToArray()));
            return true;
        }

        public static void Resolve(Kingdom k, bool force)
        {
            List<BillItem> bill = Bill(k);
            if (bill.Count == 0) return;
            long by = Proposer(k);
            var passed = new List<string>();
            var failed = new List<string>();
            var seats = Seats(k);
            foreach (BillItem it in bill)
            {
                int[] t = Tally(k, it, by);
                bool ok = t[0] > t[1];
                if (ok || force)
                {
                    Laws.Apply(k, it.Group, it.Id, force ? "decree" : "parliament");
                    passed.Add(Laws.Name(it.Group, it.Id));
                }
                else failed.Add(Laws.Name(it.Group, it.Id));
                foreach (long c in seats.Keys)
                {
                    if (c == 0L || c == by) continue;
                    bool yes = VoteOf(k, c, it, by);
                    Remember(by, c, yes ? 3f : -3f);
                    if (force && !yes)
                    {
                        Remember(c, RoyalId(k), -15f);
                        List<City> dom;
                        if (Domains(k).TryGetValue(c, out dom)) foreach (City ct in dom) Store.Put(ct.data, KDecree, G.Year);
                    }
                }
            }
            SaveBill(k, 0L, null);
            if (force)
            {
                Legitimacy.Shock(k, -0.05f);
                Gov.Log("bill_decree", k, G.Name(k), Gov.Title(k), string.Join(", ", passed.ToArray()));
                return;
            }
            if (passed.Count > 0) Gov.Log("bill_passed", k, G.Name(k), Gov.Council(k), string.Join(", ", passed.ToArray()));
            if (failed.Count > 0)
            {
                Gov.Log("bill_failed", k, G.Name(k), Gov.Council(k), string.Join(", ", failed.ToArray()));
                if (passed.Count == 0) Politics.AddStability(k, -0.01f);
            }
        }

        public static void VoteNow(Kingdom k)
        {
            if (Bill(k).Count > 0) Resolve(k, false);
        }

        public static void Decree(Kingdom k)
        {
            if (Bill(k).Count > 0) Resolve(k, true);
        }

        public static void Withdraw(Kingdom k)
        {
            if (Bill(k).Count == 0) return;
            long by = Proposer(k);
            Remember(by, RoyalId(k), -10f);
            SaveBill(k, 0L, null);
            Store.Put(k.data, KNext, G.Year + 2);
            Gov.Log("bill_withdrawn", k, G.Name(k), Gov.ClanName(by));
        }

        public static void LockCharter(Kingdom k, int years)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KCharter, G.Year + years);
            if (Bill(k).Count > 0) SaveBill(k, 0L, null);
            Politics.AddStability(k, -0.03f);
            Gov.Log("charter", k, G.Name(k), Gov.Council(k), years.ToString());
        }

        public static void ProposeNow(Kingdom k)
        {
            if (G.Ok(k) && Bill(k).Count == 0) Propose(k);
        }

        public static int DecreePenalty(City c)
        {
            if (!G.Ok(c)) return 0;
            int y = Store.Int(c.data, KDecree, -999);
            int since = G.Year - y;
            if (since < 0 || since > 10) return 0;
            return -(25 - since * 2);
        }

        public static int ClanLoyalty(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !Active(c.kingdom)) return 0;
            long h = Holder(c, c.kingdom), r = RoyalId(c.kingdom);
            if (h == 0L || r == 0L || h == r) return 0;
            return (int)G.Clamp(Opinion(h, r) / 5f, -12f, 10f);
        }

        public static float RoyalShare(Kingdom k)
        {
            int all = Total(k);
            if (all <= 0) return 0f;
            long r = RoyalId(k);
            int mine = 0;
            foreach (var kv in Seats(k)) if (kv.Key == r || r != 0L && BlocOf(kv.Key) == BlocOf(r) && BlocOf(r) != 0L) mine += kv.Value;
            return (float)mine / all;
        }
    }
}
