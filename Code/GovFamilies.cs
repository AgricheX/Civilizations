using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal class SeatTable
    {
        public float Time;
        public int Total;
        public readonly Dictionary<long, int> Seats = new Dictionary<long, int>();
        public readonly Dictionary<long, List<City>> Lands = new Dictionary<long, List<City>>();
    }

    internal static class Fam
    {
        private const string KSx = Gov.P + "sx";
        private const string KSy = Gov.P + "sy";
        private const string KMem = Gov.P + "mem";
        private const string KBloc = Gov.P + "bloc";
        private const string KBlocK = Gov.P + "bloc_k";
        private const string KChief = Gov.P + "chief";
        private static readonly Dictionary<long, SeatTable> _seats = new Dictionary<long, SeatTable>();

        public static readonly string[] Factions = { "loyalists", "landed", "reformers", "federalists" };

        public static void Clear()
        {
            _seats.Clear();
        }

        public static void Invalidate(Kingdom k)
        {
            if (G.Ok(k)) _seats.Remove(k.data.id);
        }

        public static long Royal(Kingdom k)
        {
            if (!G.Ok(k)) return 0L;
            Clan c = k.getKingClan();
            if (c != null && c.data != null) return c.data.id;
            return G.Ok(k.king) ? Gov.ClanId(k.king) : 0L;
        }

        public static int SeatsOf(City c)
        {
            if (!G.Ok(c)) return 0;
            return Math.Max(1, (c.getPopulationPeople() + 2) / 5);
        }

        private static float Reach(Kingdom k)
        {
            float m = 1f;
            if (k.capital == null) return m;
            foreach (City c in G.Cities(k)) m = Math.Max(m, G.Dist(c, k.capital));
            return m;
        }

        public static long Holder(City c, Kingdom k)
        {
            if (!G.Ok(c) || !G.Ok(k)) return 0L;
            long royal = Royal(k);
            if (c == k.capital) return royal;
            long id = Gov.ClanId(c.leader);
            if (id == 0L) return 0L;
            if (id == royal && royal != 0L && !Gov.Republic(k) && GovEra.Has(k, GovEra.Nobles) && k.capital != null && G.Dist(c, k.capital) > Reach(k) * 0.45f) return -royal;
            return id;
        }

        public static SeatTable Table(Kingdom k)
        {
            if (!G.Ok(k)) return new SeatTable();
            SeatTable t;
            if (_seats.TryGetValue(k.data.id, out t) && Time.unscaledTime - t.Time < 4f) return t;
            t = new SeatTable { Time = Time.unscaledTime };
            foreach (City c in G.Cities(k))
            {
                long h = Holder(c, k);
                int n = SeatsOf(c);
                int v;
                t.Seats.TryGetValue(h, out v);
                t.Seats[h] = v + n;
                t.Total += n;
                List<City> l;
                if (!t.Lands.TryGetValue(h, out l)) t.Lands[h] = l = new List<City>();
                l.Add(c);
            }
            _seats[k.data.id] = t;
            return t;
        }

        public static Dictionary<long, int> Seats(Kingdom k)
        {
            return Table(k).Seats;
        }

        public static List<long> Members(Kingdom k)
        {
            return Seats(k).OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
        }

        public static Clan ClanOf(long id)
        {
            return Gov.ClanById(Math.Abs(id));
        }

        public static string Name(long id)
        {
            if (id == 0L) return Txt.T("civ_parl_unaligned");
            Clan c = ClanOf(id);
            string n = c != null && c.data != null ? c.data.name : "?";
            return id < 0L ? n + " " + Txt.T("civ_parl_cadet") : n;
        }

        public static Color Color(long id)
        {
            if (id == 0L) return UiKit.Dim;
            Clan c = ClanOf(id);
            try
            {
                if (c != null) return c.getColor()._color_main;
            }
            catch (Exception)
            {
            }
            return UiKit.Soft;
        }

        public static Vector2 Stance(long id)
        {
            if (id == 0L) return Vector2.zero;
            Clan c = ClanOf(id);
            if (c == null || c.data == null) return Vector2.zero;
            if (Store.Flt(c.data, KSx, -99f) < -50f) Settle(c, Target(c, null), 1f);
            var v = new Vector2(Store.Flt(c.data, KSx, 0f), Store.Flt(c.data, KSy, 0f));
            if (id < 0L) v = new Vector2(Mathf.Clamp(v.x - 0.55f, -1f, 1f), Mathf.Clamp(v.y - 0.2f, -1f, 1f));
            return v;
        }

        private static void Settle(Clan c, Vector2 t, float f)
        {
            float x = Store.Flt(c.data, KSx, -99f), y = Store.Flt(c.data, KSy, t.y);
            if (x < -50f) x = t.x;
            Store.Put(c.data, KSx, Mathf.Clamp(x + (t.x - x) * f, -1f, 1f));
            Store.Put(c.data, KSy, Mathf.Clamp(y + (t.y - y) * f, -1f, 1f));
        }

        public static void SetStance(long id, Vector2 v)
        {
            Clan c = ClanOf(id);
            if (c == null) return;
            Store.Put(c.data, KSx, Mathf.Clamp(v.x, -1f, 1f));
            Store.Put(c.data, KSy, Mathf.Clamp(v.y, -1f, 1f));
        }

        private static Vector2 Target(Clan c, Kingdom k)
        {
            float age = (float)Date.getYearsSince(c.data.created_time);
            float y = 0.6f - Math.Min(1.2f, age / 70f);
            float x = 0f;
            Actor chief = c.getChief();
            if (G.Ok(chief))
            {
                if (chief.getAge() > 50) y -= 0.2f;
                if (chief.getAge() < 25) y += 0.15f;
                if (chief.hasTrait("ambitious")) y += 0.3f;
                if (chief.hasTrait("content") || chief.hasTrait("honest")) y -= 0.2f;
                if (chief.hasTrait("paranoid")) x += 0.3f;
                if (chief.hasTrait("greedy")) x -= 0.15f;
            }
            if (G.Ok(k) && k.capital != null)
            {
                float reach = Reach(k), sum = 0f;
                int n = 0;
                foreach (City ct in G.Cities(k))
                {
                    if (Gov.ClanId(ct.leader) != c.data.id && ct != k.capital) continue;
                    sum += G.Dist(ct, k.capital) / reach;
                    n++;
                }
                float scale = Math.Min(1f, G.Cities(k).Count / 6f);
                if (n > 0) x += (0.5f - sum / n * 1.2f) * scale;
                if (Royal(k) == c.data.id) x += 0.5f;
            }
            return new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
        }

        public static int Faction(long id)
        {
            Vector2 s = Stance(id);
            if (s.y < 0f) return s.x >= 0f ? 0 : 1;
            return s.x >= 0f ? 2 : 3;
        }

        public static string FactionName(Kingdom k, int f)
        {
            if (f < 0) return Txt.T("civ_parl_unaligned");
            string id = Factions[f];
            if (Gov.Republic(k) && (f == 0 || f == 3)) id += "_rep";
            return Txt.T("civ_parl_fac_" + id);
        }

        public static Color FactionColor(int f)
        {
            switch (f)
            {
                case 0: return UiKit.Cyan;
                case 1: return UiKit.Good;
                case 2: return UiKit.Bad;
                case 3: return UiKit.Gold;
            }
            return UiKit.Dim;
        }

        public static float Memory(long a, long b)
        {
            Clan c = ClanOf(a);
            if (c == null || c.data == null) return 0f;
            return Store.Get(Store.Map(c.data, KMem), Math.Abs(b).ToString());
        }

        public static void Remember(long a, long b, float v)
        {
            if (a == 0L || b == 0L || Math.Abs(a) == Math.Abs(b)) return;
            Clan c = ClanOf(a);
            if (c == null || c.data == null) return;
            Dictionary<string, float> m = Store.Map(c.data, KMem);
            string key = Math.Abs(b).ToString();
            m[key] = Mathf.Clamp(Store.Get(m, key) + v, -60f, 60f);
            Store.Map(c.data, KMem, m);
        }

        public static bool Neighbours(Kingdom k, long a, long b)
        {
            SeatTable t = Table(k);
            List<City> la, lb;
            if (!t.Lands.TryGetValue(a, out la) || !t.Lands.TryGetValue(b, out lb)) return false;
            foreach (City c in la)
            {
                if (c.neighbours_cities == null) continue;
                foreach (City d in lb) if (c.neighbours_cities.Contains(d)) return true;
            }
            return false;
        }

        public static float Politics(long a, long b)
        {
            return (1f - Vector2.Distance(Stance(a), Stance(b)) / 1.5f) * 25f;
        }

        public static float Rel(Kingdom k, long a, long b)
        {
            if (a == b) return 100f;
            if (a == 0L || b == 0L) return 0f;
            float r = Politics(a, b) + Memory(a, b);
            if (Neighbours(k, a, b)) r += 8f;
            if (Math.Abs(a) == Math.Abs(b)) r += 30f;
            Clan ca = ClanOf(a), cb = ClanOf(b);
            Actor ha = ca != null ? ca.getChief() : null, hb = cb != null ? cb.getChief() : null;
            if (G.Ok(ha) && G.Ok(hb))
            {
                if (ha.getBestFriend() == hb || hb.getBestFriend() == ha) r += 15f;
                if (G.Ok(ha.lover) && Gov.ClanId(ha.lover) == Math.Abs(b)) r += 15f;
            }
            return r;
        }

        public static long Bloc(Kingdom k, long id)
        {
            if (!GovEra.Has(k, GovEra.Blocs) || id <= 0L) return id;
            Clan c = ClanOf(id);
            if (c == null || c.data == null) return id;
            if (Store.Str(c.data, KBlocK) != k.data.id.ToString()) return id;
            long b;
            if (!long.TryParse(Store.Str(c.data, KBloc), out b) || b == 0L) return id;
            return Seats(k).ContainsKey(b) ? b : id;
        }

        public static List<long> BlocMembers(Kingdom k, long leader)
        {
            return Members(k).Where(x => Bloc(k, x) == leader).ToList();
        }

        private static void SetBloc(Kingdom k, long id, long leader)
        {
            Clan c = ClanOf(id);
            if (c == null || c.data == null) return;
            Store.Put(c.data, KBloc, leader == id ? "" : leader.ToString());
            Store.Put(c.data, KBlocK, k.data.id.ToString());
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            Invalidate(k);
            var seats = Seats(k);
            foreach (long id in seats.Keys)
            {
                if (id <= 0L) continue;
                Clan c = ClanOf(id);
                if (c == null || c.data == null) continue;
                Actor chief = c.getChief();
                string cid = G.Ok(chief) ? G.Id(chief) : "";
                bool fresh = Store.Str(c.data, KChief) != cid;
                Store.Put(c.data, KChief, cid);
                Settle(c, Target(c, k), fresh ? 0.4f : Math.Min(1f, 0.08f * yrs));
                Dictionary<string, float> m = Store.Map(c.data, KMem);
                if (m.Count > 0)
                {
                    foreach (string key in m.Keys.ToList())
                    {
                        float v = m[key];
                        float fade = v > 0f ? 0.1f : 0.04f;
                        if (fresh) v *= 0.66f;
                        v *= (float)Math.Pow(1f - fade, yrs);
                        if (Math.Abs(v) < 0.5f) m.Remove(key);
                        else m[key] = v;
                    }
                    Store.Map(c.data, KMem, m);
                }
            }
            if (GovEra.Has(k, GovEra.Blocs)) Blocs(k, seats);
        }

        private static void Blocs(Kingdom k, Dictionary<long, int> seats)
        {
            List<long> order = seats.Keys.Where(x => x > 0L).OrderBy(x => seats[x]).ToList();
            foreach (long id in order)
            {
                long cur = Bloc(k, id);
                if (cur != id && Rel(k, id, cur) < 5f)
                {
                    SetBloc(k, id, id);
                    Gov.Log("bloc", k, Name(id), Name(cur), Txt.T("civ_parl_bloc_left"));
                    cur = id;
                }
                if (cur != id) continue;
                if (BlocMembers(k, id).Count > 1) continue;
                long best = 0L;
                float bs = 28f;
                foreach (long o in seats.Keys)
                {
                    if (o <= 0L || o == id || seats[o] <= seats[id]) continue;
                    long lead = Bloc(k, o);
                    float r = Rel(k, id, lead);
                    if (r > bs)
                    {
                        bs = r;
                        best = lead;
                    }
                }
                if (best != 0L && G.Chance(0.5f))
                {
                    SetBloc(k, id, best);
                    Gov.Log("bloc", k, Name(id), Name(best), Txt.T("civ_parl_bloc_joined"));
                }
            }
        }
    }
}
