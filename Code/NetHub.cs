using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class NetPending
    {
        public int Due;
        public string To = "";
        public string About = "";
        public string Kind = "";
        public string Text = "";
        public string Img = "";
        public string Snap = "";
        public string Log = "";
        public int FakeId;
    }

    internal static class NetHub
    {
        private const string KPending = "civ_net_pending";
        public const string KVerified = "civ_net_verified";
        public const string KRole = "civ_net_role";
        private static List<NetPending> _pending = new List<NetPending>();
        private static readonly List<int> _alerts = new List<int>();
        private static bool _registered;
        public static int Unseen;
        public static Func<NetPost, bool> OpenLog;

        public static IList<int> Alerts
        {
            get { return _alerts; }
        }

        public static void Register()
        {
            if (_registered) return;
            _registered = true;
            Hub.On(Hub.WarStarted, Safe(WarStarted));
            Hub.On(Hub.WarEnded, Safe(WarEnded));
            Hub.On(Hub.PeaceTerms, Safe(PeaceTerms));
            Hub.On(Hub.CityCaptured, Safe(CityCaptured));
            Hub.On(Hub.CityFounded, Safe(CityFounded));
            Hub.On(Hub.KingdomFounded, Safe(KingdomFounded));
            Hub.On(Hub.KingdomGone, Safe(KingdomGone));
            Hub.On(Hub.TreatySigned, Safe(TreatySigned));
            Hub.On(Hub.TreatyBroken, Safe(TreatyBroken));
            Hub.On(Hub.Dependent, Safe(Dependent));
            Hub.On(Hub.Independence, Safe(Independence));
            Hub.On(Hub.Integrated, Safe(Integrated));
            Hub.On(Hub.OrgFounded, Safe(e => Org(e, "founded")));
            Hub.On(Hub.OrgJoined, Safe(e => Org(e, "joined")));
            Hub.On(Hub.OrgLeft, Safe(e => Org(e, "left")));
            Hub.On(Hub.OrgDissolved, Safe(e => Org(e, "dissolved")));
            Hub.On(Hub.OrgVote, Safe(e => Org(e, "vote")));
            Hub.On(Hub.WorldOrder, Safe(e => WorldOrder(e, false)));
            Hub.On(Hub.WorldOrderFell, Safe(e => WorldOrder(e, true)));
            Hub.On(Hub.Erasure, Safe(Erasure));
            Hub.On(Hub.Festival, Safe(Festival));
            Hub.On(Hub.Fireworks, Safe(Fireworks));
            Hub.On(Hub.Event, Safe(Event));
            Hub.On(Hub.GreatPerson, Safe(GreatPerson));
            Hub.On(Hub.FlagReform, Safe(FlagReform));
            Hub.On(Hub.FirstContact, Safe(FirstContact));
            Hub.On(Hub.Legitimacy, Safe(Legitimacy));
            Hub.On(Hub.CivilWar, Safe(CivilWar));
            Hub.On(Hub.Coup, Safe(Coup));
            Hub.On(Hub.FirstInWorld, Safe(FirstInWorld));
            Hub.On(Hub.TechDormant, Safe(TechDormant));
            Hub.On(Hub.EraDropped, Safe(EraDropped));
            Hub.On(Hub.TechRecovered, Safe(TechRecovered));
        }

        private static Action<HubEvent> Safe(Action<HubEvent> a)
        {
            return e =>
            {
                if (!Internet.On || !G.Loaded) return;
                a(e);
            };
        }

        public static void Clear()
        {
            _pending = new List<NetPending>();
            _alerts.Clear();
            Unseen = 0;
            NetPhoto.Clear();
        }

        public static void Load(SaveCustomData d)
        {
            _pending = new List<NetPending>();
            _alerts.Clear();
            Unseen = 0;
            try
            {
                string s = Store.Str(d, KPending);
                if (s.Length > 0) _pending = JsonConvert.DeserializeObject<List<NetPending>>(s) ?? new List<NetPending>();
            }
            catch (Exception)
            {
                _pending = new List<NetPending>();
            }
        }

        public static void Save(SaveCustomData d)
        {
            if (_pending.Count > 60) _pending.RemoveRange(0, _pending.Count - 60);
            Store.Put(d, KPending, JsonConvert.SerializeObject(_pending));
        }

        public static bool IsVerified(Actor a)
        {
            if (!G.Ok(a)) return false;
            return a.isKing() || a.isCityLeader() || Store.Bool(a.data, KVerified) || Internet.Followers(a) > 2000;
        }

        public static string RoleOf(Actor a)
        {
            if (!G.Ok(a)) return "";
            if (a.isKing()) return "leader";
            string r = Store.Str(a.data, KRole);
            if (r.Length > 0) return r;
            if (a.isCityLeader()) return "mayor";
            return Internet.Followers(a) > 2000 ? "star" : "";
        }

        public static void Alert(NetPost p)
        {
            if (p == null) return;
            _alerts.Add(p.Id);
            if (_alerts.Count > 40) _alerts.RemoveRange(0, _alerts.Count - 40);
            Unseen = Math.Min(99, Unseen + 1);
        }

        public static void Seen()
        {
            Unseen = 0;
        }

        public static void OpenLogFor(NetPost p)
        {
            if (p == null) return;
            if (OpenLog != null)
            {
                try
                {
                    if (OpenLog(p)) return;
                }
                catch (Exception e)
                {
                    CivMod.Report("net-log", e);
                }
            }
            UiKit.Open("world_history");
        }

        private static string LogKey(HubEvent e)
        {
            return e.Type + "|" + e.Year + "|" + G.Id(e.K) + "|" + G.Id(e.O) + "|" + e.Id;
        }

        private static string Snap(City c)
        {
            WorldTile t = G.Ok(c) ? G.Tile(c) : null;
            return t != null ? t.x + "," + t.y : "";
        }

        private static string Snap(Kingdom k)
        {
            return G.Ok(k) && k.capital != null ? Snap(k.capital) : "";
        }

        private static NetPost Post(HubEvent e, Kingdom k, string kind, string text, bool important, string img, string snap)
        {
            if (!Internet.Reach(k)) return null;
            NetPost p = Internet.Official(k, kind, text, important);
            if (p == null) return null;
            p.Kind = kind;
            p.Topic = NetMedia.TopicOf(kind);
            p.Log = LogKey(e);
            if (img != null) p.Img = img;
            if (!string.IsNullOrEmpty(snap)) p.Snap = snap;
            else if (p.Snap.Length == 0) p.Snap = Snap(k);
            if (e.O != null && e.O != k) p.About = G.Id(e.O);
            else if (e.K != null && e.K != k) p.About = G.Id(e.K);
            return p;
        }

        public static NetPost Outlet(Kingdom k, string kind, string text, bool important)
        {
            if (!Internet.Reach(k)) return null;
            string med = NetMedia.Of(k);
            NetPost p = Internet.Make("press", "press:" + G.Id(k), NetMedia.Outlet(k, med), NetMedia.OutletHandle(k), k, kind, text);
            p.Verified = true;
            p.Role = NetPress.Of(k) == "state" ? "state_media" : "press";
            p.Important = important;
            NetPress.Shape(p, k);
            Internet.Add(p);
            Internet.Engage(p, k, important ? 1.5f : 0.8f);
            return p;
        }

        private static NetPost OrgPost(string org, Kingdom k, string kind, string text, bool important)
        {
            if (!G.Ok(k)) k = G.Kingdoms().FirstOrDefault();
            if (!Internet.Reach(k)) return null;
            NetPost p = Internet.Make("org", "org:" + org, org, "@" + Names.Root(org).ToLowerInvariant(), k, kind, text);
            p.Verified = true;
            p.Role = "org";
            p.Important = important;
            p.Topic = "diplomacy";
            Internet.Add(p);
            Internet.Engage(p, k, important ? 2f : 1f);
            return p;
        }

        private static void Leader(Kingdom k, string kind, string text, string img)
        {
            if (!G.Ok(k) || !k.hasKing()) return;
            NetPost p = Internet.Person(k.king, kind, text, 2.5f);
            if (p == null) return;
            p.Verified = true;
            p.Role = "leader";
            if (img != null) p.Img = img;
            if (p.Snap.Length == 0) p.Snap = Snap(k);
        }

        private static void Spread(HubEvent e, Kingdom src, string text, string kind, string img, string snap, int max)
        {
            if (!G.Ok(src)) return;
            var cands = G.Kingdoms().Where(x => x != src && x != e.O && x != e.K && Fog.Knows(x, src)).OrderBy(x => UnityEngine.Random.value).Take(max).ToList();
            foreach (Kingdom x in cands)
            {
                bool near = Near(x, src);
                string reg = NetMedia.Register(NetMedia.Of(x));
                if (near)
                {
                    NetPost p = Outlet(x, kind, Internet.Line("net_foreign_" + reg, G.Name(src), text), false);
                    if (p == null) continue;
                    p.Img = img ?? "";
                    p.Snap = snap ?? "";
                    p.About = G.Id(src);
                    p.Log = LogKey(e);
                    p.Topic = NetMedia.TopicOf(kind);
                    continue;
                }
                int fake = 0;
                if (G.Chance(0.15f))
                {
                    Actor a = Internet.RandomUser(x);
                    NetPost f = a != null ? Internet.Person(a, "fake", Internet.Line("net_rumor_fake_" + reg, G.Name(src)), 1.5f) : null;
                    if (f != null)
                    {
                        f.Fake = true;
                        f.Rumor = true;
                        f.About = G.Id(src);
                        f.Topic = NetMedia.TopicOf(kind);
                        fake = f.Id;
                    }
                }
                int delay = Math.Min(3, 1 + (int)(Dist(x, src) / 120f));
                _pending.Add(new NetPending { Due = G.Year + delay, To = G.Id(x), About = G.Id(src), Kind = kind, Text = text, Img = img ?? "", Snap = snap ?? "", Log = LogKey(e), FakeId = fake });
            }
        }

        private static float Dist(Kingdom a, Kingdom b)
        {
            if (a == null || b == null || a.capital == null || b.capital == null) return 999f;
            return G.Dist(a.capital, b.capital);
        }

        private static bool Near(Kingdom x, Kingdom src)
        {
            string mx = NetMedia.Of(x), ms = NetMedia.Of(src);
            if (NetMedia.Modern(mx) && NetMedia.Modern(ms)) return true;
            if (NetMedia.Rank(mx) >= 3 && NetMedia.Rank(ms) >= 3) return Dist(x, src) < 200f;
            return Dist(x, src) < 70f;
        }

        public static void Year()
        {
            if (_pending.Count == 0) return;
            var due = _pending.Where(p => p.Due <= G.Year).ToList();
            if (due.Count == 0) return;
            _pending.RemoveAll(p => p.Due <= G.Year);
            foreach (NetPending d in due.Take(20))
            {
                Kingdom x = G.KingdomById(d.To);
                if (!Internet.Reach(x)) continue;
                string reg = NetMedia.Register(NetMedia.Of(x));
                Kingdom src = G.KingdomById(d.About);
                string from = src != null ? G.Name(src) : "?";
                NetPost p = Outlet(x, d.Kind, Internet.Line("net_rumor_" + reg, from, d.Text), false);
                if (p == null) continue;
                p.Rumor = true;
                p.Img = d.Img;
                p.Snap = d.Snap;
                p.About = d.About;
                p.Log = d.Log;
                p.Topic = NetMedia.TopicOf(d.Kind);
                if (d.FakeId > 0)
                {
                    NetPost f = Internet.Feed.FirstOrDefault(q => q.Id == d.FakeId);
                    if (f != null)
                    {
                        f.Checked = true;
                        p.Comments.Add(new NetComment { Author = p.Author, Handle = p.Handle, AuthorId = p.AuthorId, Text = Internet.Line("net_rumor_debunk", f.Author), Likes = UnityEngine.Random.Range(2, 40) });
                    }
                }
            }
            Internet.Bump();
        }

        private static string GoalText(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return Txt.T("net_h_goal_none");
            string k = "civ_goal_" + goal;
            return Txt.Has(k) ? Txt.T(k) : goal.Replace("_", " ");
        }

        private static string Named(string prefix, string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            string k = prefix + id;
            if (Txt.Has(k)) return Txt.T(k);
            if (Txt.Has(id)) return Txt.T(id);
            return id.Replace("_", " ");
        }

        private static void WarStarted(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.O)) return;
            string goal = GoalText(e.Arg);
            string snap = e.C != null ? Snap(e.C) : Snap(e.O);
            string text = Internet.Line("net_h_war_start", G.Name(e.K), G.Name(e.O), goal);
            NetPost a = Post(e, e.K, "war_start", text, true, "battle", snap);
            Post(e, e.O, "war_defend", Internet.Line("net_h_war_defend", G.Name(e.O), G.Name(e.K)), true, "battle", snap);
            if (G.Chance(0.6f)) Leader(e.K, "war_start", Internet.Line("net_h_leader_war", G.Name(e.O)), "portrait");
            if (G.Chance(0.4f)) Leader(e.O, "war_defend", Internet.Line("net_h_leader_defend", G.Name(e.K)), "portrait");
            if (a != null && G.Pop(e.K) + G.Pop(e.O) > 150) Alert(a);
            Spread(e, e.K, text, "war_start", "battle", snap, 3);
        }

        private static void WarEnded(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string snap = Snap(e.O ?? e.K);
            if (e.Id == "draw" || !G.Ok(e.O))
            {
                string t = Internet.Line("net_h_war_draw", G.Name(e.K), e.O != null ? G.Name(e.O) : "?");
                Post(e, e.K, "peace", t, true, "doc_treaty", snap);
                if (G.Ok(e.O)) Post(e, e.O, "peace", Internet.Line("net_h_war_draw", G.Name(e.O), G.Name(e.K)), true, "doc_treaty", snap);
                Spread(e, e.K, t, "peace", "doc_treaty", snap, 2);
                return;
            }
            string won = Internet.Line("net_h_war_won", G.Name(e.K), G.Name(e.O));
            NetPost p = Post(e, e.K, "war_end", won, true, "festival", Snap(e.K));
            Post(e, e.O, "war_lost", Internet.Line("net_h_war_lost", G.Name(e.O), G.Name(e.K)), true, "city", Snap(e.O));
            if (G.Chance(0.5f)) Leader(e.K, "war_end", Internet.Line("net_h_leader_victory", G.Name(e.O)), "portrait");
            if (p != null) Alert(p);
            Spread(e, e.K, won, "war_end", "map", snap, 2);
        }

        private static void PeaceTerms(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.O) || e.Id == "white_peace") return;
            var terms = (e.Id ?? "").Split(',').Where(x => x.Length > 0).Select(x => Named("civ_term_", x)).ToArray();
            string list = terms.Length > 0 ? string.Join(", ", terms) : "-";
            Post(e, e.K, "peace", Internet.Line("net_h_peace_terms", G.Name(e.K), G.Name(e.O), list), false, "doc_treaty", Snap(e.O));
        }

        private static void CityCaptured(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.C)) return;
            string city = G.Name(e.C);
            string snap = Snap(e.C);
            string from = e.O != null && e.O.data != null ? G.Name(e.O) : "?";
            switch (e.Id)
            {
                case "captured":
                    Post(e, e.K, "city_taken", Internet.Line("net_h_city_taken", G.Name(e.K), city, from), false, "battle", snap);
                    if (G.Ok(e.O)) Post(e, e.O, "city_lost", Internet.Line("net_h_city_lost", from, city, G.Name(e.K)), true, "battle", snap);
                    break;
                case "rebellion":
                    Post(e, e.K, "rebellion", Internet.Line("net_h_city_rebel", city, from), true, "battle", snap);
                    break;
                default:
                    if (G.Chance(0.5f)) Post(e, e.K, "city_join", Internet.Line("net_h_city_join", city, G.Name(e.K), from), false, "city", snap);
                    break;
            }
        }

        private static void CityFounded(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.C) || !G.Chance(0.35f)) return;
            Post(e, e.K, "city_new", Internet.Line("net_h_city_new", G.Name(e.C), G.Name(e.K)), false, "city", Snap(e.C));
        }

        private static void KingdomFounded(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            bool split = G.Ok(e.O) && e.O != e.K;
            string text = split ? Internet.Line("net_h_kingdom_split", G.Name(e.K), G.Name(e.O)) : Internet.Line("net_h_kingdom_new", G.Name(e.K));
            NetPost p = Post(e, e.K, "kingdom_new", text, true, "flag", Snap(e.K));
            if (p != null) p.Flag = Flags.Code(e.K);
            if (split) Spread(e, e.K, text, "kingdom_new", "map", Snap(e.K), 2);
        }

        private static void KingdomGone(HubEvent e)
        {
            if (e.K == null || e.K.data == null) return;
            string name = G.Name(e.K);
            int n = 0;
            foreach (Kingdom x in G.Kingdoms().OrderBy(q => UnityEngine.Random.value))
            {
                if (x == e.K || !Fog.Knows(x, e.K)) continue;
                NetPost p = Outlet(x, "kingdom_gone", Internet.Line("net_h_kingdom_gone_" + NetMedia.Register(NetMedia.Of(x)), name), false);
                if (p != null)
                {
                    p.Img = "map";
                    p.Snap = Snap(e.K);
                    p.Log = LogKey(e);
                    p.Topic = "world";
                }
                if (++n >= 2) break;
            }
        }

        private static void TreatySigned(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.O)) return;
            string name = Named("civ_treaty_", e.Id);
            string text = Internet.Line("net_h_treaty", G.Name(e.K), G.Name(e.O), name);
            Post(e, e.K, "treaty", text, false, "doc_treaty", Snap(e.K));
            if (G.Chance(0.5f)) Post(e, e.O, "treaty", Internet.Line("net_h_treaty", G.Name(e.O), G.Name(e.K), name), false, "doc_treaty", Snap(e.O));
            if (G.Chance(0.35f)) Leader(e.K, "treaty", Internet.Line("net_h_leader_treaty", G.Name(e.O), name), "portrait");
            if (e.Id == "royal_marriage" || e.Id == "defense_pact" || e.Id == "nuclear_umbrella") Spread(e, e.K, text, "treaty", "doc_treaty", Snap(e.K), 1);
        }

        private static void TreatyBroken(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.O)) return;
            string name = e.Id == "all" ? Txt.T("net_h_all_treaties") : Named("civ_treaty_", e.Id);
            Post(e, e.K, "treaty_broken", Internet.Line("net_h_treaty_broken", G.Name(e.K), G.Name(e.O), name), false, "doc_treaty", Snap(e.K));
            Post(e, e.O, "treaty_broken", Internet.Line("net_h_treaty_betrayed", G.Name(e.O), G.Name(e.K), name), false, "doc_treaty", Snap(e.O));
        }

        private static void Dependent(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.O)) return;
            string kind = Named("civ_dep_", e.Id);
            Post(e, e.O, "dependent_lord", Internet.Line("net_h_dep_lord", G.Name(e.O), G.Name(e.K), kind), true, "map", Snap(e.K));
            Post(e, e.K, "dependent", Internet.Line("net_h_dep_sub", G.Name(e.K), G.Name(e.O), kind), true, "doc_treaty", Snap(e.K));
        }

        private static void Independence(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string lord = G.Ok(e.O) ? G.Name(e.O) : "?";
            string text = Internet.Line("net_h_indep", G.Name(e.K), lord);
            NetPost p = Post(e, e.K, "independence", text, true, "festival", Snap(e.K));
            if (p != null) Alert(p);
            if (G.Ok(e.O)) Post(e, e.O, "independence_lost", Internet.Line("net_h_indep_lord", lord, G.Name(e.K)), false, "map", Snap(e.K));
            if (G.Chance(0.6f)) Leader(e.K, "independence", Internet.Line("net_h_leader_indep", lord), "portrait");
            Spread(e, e.K, text, "independence", "map", Snap(e.K), 2);
        }

        private static void Integrated(HubEvent e)
        {
            if (!G.Ok(e.K) || e.O == null || e.O.data == null) return;
            Post(e, e.K, "integrated", Internet.Line("net_h_integrated", G.Name(e.K), G.Name(e.O)), true, "map", Snap(e.K));
        }

        private static string OrgName(HubEvent e)
        {
            if (!string.IsNullOrEmpty(e.Id) && Txt.Has("civ_org_" + e.Id)) return Txt.T("civ_org_" + e.Id);
            if (!string.IsNullOrEmpty(e.Id)) return e.Id;
            return Txt.T("net_h_org_unknown");
        }

        private static void Org(HubEvent e, string what)
        {
            string org = OrgName(e);
            string k = G.Ok(e.K) ? G.Name(e.K) : "?";
            switch (what)
            {
                case "founded":
                {
                    NetPost p = OrgPost(org, e.K, "org", Internet.Line("net_h_org_founded", org, k), true);
                    if (p != null)
                    {
                        p.Img = "map";
                        p.Snap = Snap(e.K);
                        p.Log = LogKey(e);
                        Alert(p);
                    }
                    if (G.Ok(e.K)) Post(e, e.K, "org", Internet.Line("net_h_org_join", k, org), false, "doc_treaty", Snap(e.K));
                    break;
                }
                case "joined":
                    if (G.Ok(e.K)) Post(e, e.K, "org", Internet.Line("net_h_org_join", k, org), false, "doc_treaty", Snap(e.K));
                    if (G.Chance(0.5f))
                    {
                        NetPost p = OrgPost(org, e.K, "org", Internet.Line("net_h_org_welcome", org, k), false);
                        if (p != null) p.Log = LogKey(e);
                    }
                    break;
                case "left":
                    if (G.Ok(e.K)) Post(e, e.K, "org", Internet.Line("net_h_org_left", k, org), false, "doc_treaty", Snap(e.K));
                    break;
                case "dissolved":
                {
                    NetPost p = OrgPost(org, e.K, "org", Internet.Line("net_h_org_dissolved", org), true);
                    if (p != null) p.Log = LogKey(e);
                    break;
                }
                case "vote":
                {
                    string decision = Named("civ_vote_", e.Arg);
                    NetPost p = OrgPost(org, e.K, "org_vote", Internet.Line("net_h_org_vote", org, decision, k), false);
                    if (p != null)
                    {
                        p.Img = "doc_ballot";
                        p.Log = LogKey(e);
                    }
                    break;
                }
            }
        }

        private static void WorldOrder(HubEvent e, bool fell)
        {
            string name = OrgName(e);
            NetPost p = OrgPost(name, e.K, "world_order", Internet.Line(fell ? "net_h_world_order_fell" : "net_h_world_order", name, G.Ok(e.K) ? G.Name(e.K) : "?"), true);
            if (p == null) return;
            p.Img = "map";
            p.Snap = Snap(e.K);
            p.Log = LogKey(e);
            p.Topic = "world";
            Alert(p);
            Notify.Show(p.Text);
        }

        private static void Erasure(HubEvent e)
        {
            if (e.K == null || e.K.data == null) return;
            Kingdom by = G.Ok(e.O) ? e.O : G.Kingdoms().FirstOrDefault(x => x != e.K);
            NetPost p = Outlet(by, "erasure", Internet.Line("net_h_erasure", G.Name(e.K)), true);
            if (p == null) return;
            p.Img = "map";
            p.Snap = Snap(e.K);
            p.Log = LogKey(e);
            p.Topic = "world";
            Alert(p);
        }

        private static string FestName(HubEvent e)
        {
            if (!string.IsNullOrEmpty(e.Arg) && !e.Arg.Contains("_")) return e.Arg;
            return Named("civ_fest_", e.Id);
        }

        private static void Festival(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            City c = G.Ok(e.C) ? e.C : e.K.capital;
            string name = FestName(e);
            string city = c != null ? G.Name(c) : G.Name(e.K);
            NetPost p = Post(e, e.K, "festival", Internet.Line("net_h_festival", name, city), false, "festival", Snap(c));
            if (p != null) p.Seed = (p.Seed & ~1) | (Research.Era(e.K) >= 9 && G.Chance(0.4f) ? 1 : 0);
            int n = UnityEngine.Random.Range(1, Internet.Exists(e.K) ? 4 : 2);
            for (int i = 0; i < n; i++)
            {
                Actor a = c != null && c.units.Count > 0 ? c.units[UnityEngine.Random.Range(0, c.units.Count)] : Internet.RandomUser(e.K);
                if (!G.Ok(a) || !a.isAdult()) continue;
                NetPost q = Internet.Person(a, "festival", Internet.Line("net_h_fest_person_" + NetMedia.Register(NetMedia.Of(e.K)), name, city), 1.2f);
                if (q == null) continue;
                q.Topic = "culture";
                q.Img = Internet.Exists(e.K) && G.Chance(0.5f) ? "portrait" : "festival";
                q.Snap = Snap(c);
                q.Tag = "#" + Names.Root(name);
            }
        }

        private static void Fireworks(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            City c = G.Ok(e.C) ? e.C : e.K.capital;
            NetPost p = Post(e, e.K, "fireworks", Internet.Line("net_h_fireworks", c != null ? G.Name(c) : G.Name(e.K)), false, "festival", Snap(c));
            if (p != null) p.Seed |= 1;
        }

        private static void Event(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string kind = e.Id != null && e.Id.Contains("wonder") ? "wonder" : "event";
            string city = G.Ok(e.C) ? G.Name(e.C) : G.Name(e.K);
            string text = Internet.HasLine("net_ev_" + e.Id) ? Internet.Line("net_ev_" + e.Id, G.Name(e.K), city, e.Arg) : Internet.Line("net_h_event", G.Name(e.K), Named("civ_ev_", e.Id), city);
            NetPost p = Post(e, e.K, kind, text, kind == "wonder" || e.Value >= 1f, kind == "wonder" ? "city" : null, G.Ok(e.C) ? Snap(e.C) : null);
            if (p == null) return;
            if (p.Img.Length == 0) p.Img = "city";
            if (kind == "wonder") Spread(e, e.K, text, "wonder", "city", p.Snap, 2);
        }

        private static void GreatPerson(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string role = Named("civ_great_", e.Id);
            if (G.Ok(e.A))
            {
                Store.Put(e.A.data, KVerified, true);
                Store.Put(e.A.data, KRole, "great");
                Store.Put(e.A.data, Internet.KFollow, Internet.Followers(e.A) + 500);
                NetPost me = Internet.Person(e.A, "great_person", Internet.Line("net_h_great_self_" + NetMedia.Register(NetMedia.Of(e.K)), role), 2f);
                if (me != null)
                {
                    me.Img = "portrait";
                    me.Log = LogKey(e);
                }
            }
            NetPost p = Post(e, e.K, "great_person", Internet.Line("net_h_great", G.Ok(e.A) ? e.A.getName() : "?", G.Name(e.K), role), false, "portrait", G.Ok(e.A) && e.A.current_tile != null ? e.A.current_tile.x + "," + e.A.current_tile.y : null);
            if (p != null && G.Ok(e.A)) p.Photo = G.Id(e.A);
        }

        private static void FlagReform(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string reason = Flags.Reason(e.K);
            if (reason == "founded" || reason == "converted" || string.IsNullOrEmpty(e.Arg)) return;
            string key = Internet.HasLine("net_h_flag_" + reason) ? "net_h_flag_" + reason : "net_h_flag";
            NetPost p = Post(e, e.K, "flag", Internet.Line(key, G.Name(e.K)), true, "doc_flag", Snap(e.K));
            if (p == null) return;
            p.Flag = e.Id + "#" + e.Arg;
            int n = UnityEngine.Random.Range(1, 4);
            for (int i = 0; i < n; i++)
            {
                Actor a = Internet.RandomUser(e.K);
                if (a == null) continue;
                Opinion o = Politics.Get(a);
                bool like = o == null ? G.Chance(0.6f) : o.Sat > 0.45f;
                p.Comments.Add(new NetComment { Author = a.getName(), Handle = Names.Handle(a), AuthorId = G.Id(a), Text = Internet.Line((like ? "net_c_flag_like_" : "net_c_flag_dislike_") + NetMedia.Register(p.Med)), Likes = UnityEngine.Random.Range(0, 25) });
            }
        }

        private static void FirstContact(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Ok(e.O)) return;
            string sp = "";
            try
            {
                ActorAsset aa = AssetManager.actor_library.get(e.O.getSpecies());
                sp = aa != null ? aa.getTranslatedName() : "";
            }
            catch (Exception)
            {
                sp = "";
            }
            Post(e, e.K, "first_contact", Internet.Line("net_h_contact_" + NetMedia.Register(NetMedia.Of(e.K)), G.Name(e.O), sp), false, "map", Snap(e.O));
        }

        private static void Legitimacy(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string press = NetPress.Of(e.K);
            string leader = e.K.hasKing() ? e.K.king.getName() : G.Name(e.K);
            if (press == "free" || press == "sponsored")
            {
                NetPost p = Outlet(e.K, "legitimacy", Internet.Line("net_h_legit_free", leader, G.Name(e.K)), true);
                if (p != null)
                {
                    p.Img = "portrait";
                    p.Log = LogKey(e);
                    p.Topic = "politics";
                }
                for (int i = 0; i < 2; i++)
                {
                    Actor a = Internet.RandomUser(e.K);
                    if (a != null) Internet.Person(a, "opinion", Internet.Line("net_h_legit_voice", leader), 1.5f);
                }
                return;
            }
            Post(e, e.K, "legitimacy", Internet.Line("net_h_legit", G.Name(e.K), leader), false, "portrait", null);
        }

        private static void CivilWar(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string city = G.Ok(e.C) ? G.Name(e.C) : "?";
            string faction = Named("civ_faction_", e.Id);
            NetPost p = Post(e, e.K, "civil_war", Internet.Line("net_h_civil_war", G.Name(e.K), city, faction), true, "battle", G.Ok(e.C) ? Snap(e.C) : null);
            if (p != null) Alert(p);
            if (G.Ok(e.O)) Post(e, e.O, "rebels", Internet.Line("net_h_rebels", G.Name(e.O), G.Name(e.K)), true, "flag", Snap(e.O));
            Spread(e, e.K, Internet.Line("net_h_civil_war", G.Name(e.K), city, faction), "civil_war", "battle", G.Ok(e.C) ? Snap(e.C) : null, 2);
        }

        private static void Coup(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            NetPost p = Post(e, e.K, "coup", Internet.Line("net_h_coup_" + NetMedia.Register(NetMedia.Of(e.K)), G.Name(e.K), Named("civ_p_", e.Id)), true, "portrait", null);
            if (p != null)
            {
                p.Propaganda = true;
                Alert(p);
            }
        }

        private static void FirstInWorld(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            string text;
            if (e.Id == "era")
            {
                int era;
                int.TryParse(e.Arg, out era);
                text = Internet.Line("net_h_first_era", G.Name(e.K), Db.EraName(era));
            }
            else if (e.Id == "tech") text = Internet.Line("net_h_first_tech", G.Name(e.K), Db.TechName(e.Arg));
            else text = Internet.Line("net_h_first", G.Name(e.K), Named("civ_first_", e.Id));
            NetPost p = Post(e, e.K, "world_first", text, true, e.Id == "era" ? "festival" : "city", null);
            if (p != null) Alert(p);
            Spread(e, e.K, text, "world_first", "city", Snap(e.K), 4);
        }

        private static void TechDormant(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Chance(0.5f)) return;
            Post(e, e.K, "regression", Internet.Line("net_h_dormant", G.Name(e.K), Db.TechName(e.Id)), false, "city", null);
        }

        private static void EraDropped(HubEvent e)
        {
            if (!G.Ok(e.K)) return;
            int after, before;
            int.TryParse(e.Id, out after);
            int.TryParse(e.Arg, out before);
            string text = Internet.Line("net_h_era_drop", G.Name(e.K), Db.EraName(after), Db.EraName(before));
            NetPost p = Post(e, e.K, "regression", text, true, "city", null);
            if (p != null) Alert(p);
            Spread(e, e.K, text, "regression", "city", Snap(e.K), 2);
        }

        private static void TechRecovered(HubEvent e)
        {
            if (!G.Ok(e.K) || !G.Chance(0.5f)) return;
            Post(e, e.K, "tech", Internet.Line("net_h_recovered", G.Name(e.K), Db.TechName(e.Id)), false, null, null);
        }
    }
}
