using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class NetComment
    {
        public string Author = "";
        public string Handle = "";
        public string AuthorId = "";
        public string Text = "";
        public int Likes;
    }

    internal class NetPost
    {
        public int Id;
        public int Year;
        public int Month;
        public string Kind = "";
        public string Who = "person";
        public string AuthorId = "";
        public string Author = "";
        public string Handle = "";
        public string Kingdom = "";
        public string KingdomName = "";
        public string Lang = "";
        public string LangName = "";
        public string Text = "";
        public string Place = "";
        public int Likes;
        public int Dislikes;
        public int Shares;
        public List<NetComment> Comments = new List<NetComment>();
        public bool Viral;
        public bool Important;
        public bool Live;
        public bool LiveEnded;
        public int Viewers;
        public List<string> Poll;
        public List<int> Votes;
        public int PollEnds;
        public int PollWinner = -1;
        public bool Liked;
        public bool Saved;
        public string Tag = "";
        public string Photo = "";
        public bool Story;
        public string Icon = "";
        public string Community = "";
        public bool Fake;
        public bool Checked;
        public bool Dm;
        public string ToId = "";
        public string ToName = "";
        public string Snap = "";
        public string Geo = "";
        public int Candles;
        public string Med = "";
        public int Era;
        public string Img = "";
        public int Seed;
        public string Topic = "";
        public bool Verified;
        public string Role = "";
        public bool Rumor;
        public bool Censored;
        public bool Propaganda;
        public string Sponsor = "";
        public bool Flame;
        public string Log = "";
        public string About = "";
        public string Flag = "";
    }

    internal class NetChronicle
    {
        public int Year;
        public string Kingdom = "";
        public string Kind = "";
        public string Text = "";
    }

    internal static partial class Internet
    {
        private const string KFeed = "civ_feed";
        private const string KChron = "civ_chronicle";
        private const string KTrend = "civ_trend";
        public const string KRep = "civ_rep";
        public const string KFollow = "civ_net_f";
        public const string KCancel = "civ_net_cancel";
        public const string KStrikes = "civ_net_strikes";
        public const string KAddict = "civ_net_addict";
        public const string KLastPost = "civ_net_last";

        private static List<NetPost> _feed = new List<NetPost>();
        private static List<NetChronicle> _chron = new List<NetChronicle>();
        private static int _next = 1;
        private static float _tick;
        private static readonly Dictionary<string, int> _variants = new Dictionary<string, int>();
        public static string Trend = "";
        public static string TrendPolicy = "";
        public static int Version;

        public static IList<NetPost> Feed { get { return _feed; } }
        public static IList<NetChronicle> Chronicles { get { return _chron; } }

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.Internet; }
        }

        public static bool Exists(Kingdom k)
        {
            return On && k != null && k.isCiv() && Research.HasFeature(k, "internet");
        }

        public static bool ExistsAnywhere()
        {
            if (!On || World.world == null) return false;
            foreach (Kingdom k in G.Kingdoms()) if (Research.HasFeature(k, "internet")) return true;
            return false;
        }

        public static bool Reach(Kingdom k)
        {
            return On && k != null && k.isAlive() && k.isCiv();
        }

        public static bool ReachAnywhere()
        {
            if (!On || World.world == null) return false;
            return G.Kingdoms().Count > 0;
        }

        public static void Load()
        {
            _feed = new List<NetPost>();
            _chron = new List<NetChronicle>();
            _next = 1;
            SaveCustomData d = Store.World();
            if (d == null) return;
            try
            {
                string s = Store.Str(d, KFeed);
                if (s.Length > 0) _feed = JsonConvert.DeserializeObject<List<NetPost>>(s) ?? new List<NetPost>();
                string c = Store.Str(d, KChron);
                if (c.Length > 0) _chron = JsonConvert.DeserializeObject<List<NetChronicle>>(c) ?? new List<NetChronicle>();
            }
            catch (Exception)
            {
                _feed = new List<NetPost>();
            }
            foreach (NetPost p in _feed) _next = Math.Max(_next, p.Id + 1);
            Trend = Store.Str(d, KTrend);
            NetHub.Load(d);
            Version++;
        }

        public const int FeedCap = 700;
        private const int FeedBatch = 100;

        public static void Trim()
        {
            if (_feed.Count > FeedCap)
            {
                int drop = _feed.Count - FeedCap + FeedBatch;
                var gone = new HashSet<int>();
                foreach (NetPost p in _feed)
                {
                    if (gone.Count >= drop) break;
                    if (!p.Saved && !p.Important) gone.Add(p.Id);
                }
                foreach (NetPost p in _feed)
                {
                    if (gone.Count >= drop) break;
                    if (!p.Saved) gone.Add(p.Id);
                }
                _feed.RemoveAll(p => gone.Contains(p.Id));
                NetPhoto.Forget(gone);
                Version++;
            }
            if (_chron.Count > 400) _chron.RemoveRange(0, _chron.Count - 300);
        }

        public static void Save()
        {
            SaveCustomData d = Store.World();
            if (d == null) return;
            Trim();
            NetHub.Save(d);
            Store.Put(d, KFeed, JsonConvert.SerializeObject(_feed));
            Store.Put(d, KChron, JsonConvert.SerializeObject(_chron));
            Store.Put(d, KTrend, Trend ?? "");
        }

        private static int Variants(string key)
        {
            int n;
            if (_variants.TryGetValue(key, out n)) return n;
            n = 0;
            while (Txt.Has(key + "_" + n)) n++;
            _variants[key] = n;
            return n;
        }

        private static readonly Dictionary<string, List<int>> _recent = new Dictionary<string, List<int>>();

        public static bool HasLine(string key)
        {
            return Variants(key) > 0 || Txt.Has(key);
        }

        public static string Line(string key, params object[] args)
        {
            int n = Variants(key);
            if (n == 0) return Txt.F(key, args);
            if (n == 1) return Txt.F(key + "_0", args);
            List<int> used;
            if (!_recent.TryGetValue(key, out used))
            {
                used = new List<int>();
                _recent[key] = used;
                if (_recent.Count > 600) _recent.Clear();
            }
            int keep = Math.Min(n - 1, 6);
            int pick = UnityEngine.Random.Range(0, n);
            for (int i = 0; i < 8 && used.Contains(pick); i++) pick = UnityEngine.Random.Range(0, n);
            if (used.Contains(pick)) for (int i = 0; i < n; i++) if (!used.Contains(i)) { pick = i; break; }
            used.Add(pick);
            while (used.Count > keep) used.RemoveAt(0);
            return Txt.F(key + "_" + pick, args);
        }

        public static string Pick(params string[] keys)
        {
            foreach (string k in keys) if (HasLine(k)) return k;
            return keys.Length > 0 ? keys[keys.Length - 1] : "";
        }

        public static float Reputation(Kingdom k)
        {
            return k == null || k.data == null ? 0f : Store.Flt(k.data, KRep, 0f);
        }

        public static void AddRep(Kingdom k, float v)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KRep, G.Clamp(Reputation(k) + v, -1f, 1f));
        }

        public static int Followers(Actor a)
        {
            return G.Ok(a) ? Store.Int(a.data, KFollow, 0) : 0;
        }

        public static int Tier(int followers)
        {
            if (followers >= 50000) return 5;
            if (followers >= 8000) return 4;
            if (followers >= 1500) return 3;
            if (followers >= 300) return 2;
            if (followers >= 40) return 1;
            return 0;
        }

        public static string TierLabel(int followers)
        {
            return Txt.T("net_tier_" + Tier(followers));
        }

        public static float TrendPull(Kingdom k, string policy)
        {
            if (string.IsNullOrEmpty(TrendPolicy) || policy != TrendPolicy || !Exists(k)) return 0f;
            return 1.2f;
        }

        private static void Chron(Kingdom k, string kind, string text)
        {
            _chron.Add(new NetChronicle { Year = G.Year, Kingdom = G.Id(k), Kind = kind, Text = text });
            Version++;
        }

        public static NetPost Add(NetPost p)
        {
            if (p == null) return null;
            _feed.Add(p);
            Version++;
            return p;
        }

        public static NetPost Make(string who, string authorId, string author, string handle, Kingdom k, string kind, string text)
        {
            var p = new NetPost
            {
                Id = _next++,
                Year = G.Year,
                Month = Date.getCurrentMonth(),
                Kind = kind,
                Who = who,
                AuthorId = authorId,
                Author = author,
                Handle = handle,
                Kingdom = G.Id(k),
                KingdomName = G.Name(k),
                Text = text,
                Med = NetMedia.Of(k),
                Era = k != null && k.isAlive() ? Research.Era(k) : 0,
                Seed = UnityEngine.Random.Range(1, int.MaxValue),
                Topic = NetMedia.TopicOf(kind)
            };
            if (k != null && k.language != null && k.language.data != null)
            {
                p.Lang = k.language.data.id.ToString();
                p.LangName = k.language.data.name;
            }
            return p;
        }

        public static NetPost Official(Kingdom k, string kind, string text, bool important)
        {
            if (!Reach(k))
            {
                if (k != null) Chron(k, kind, text);
                return null;
            }
            if (NetPress.Hush(k, kind)) return null;
            NetPost p = Make("kingdom", G.Id(k), G.Name(k), Names.Handle(k), k, kind, text);
            p.Important = important;
            p.Verified = true;
            p.Role = "state";
            p.Photo = k.hasKing() ? G.Id(k.king) : "";
            WorldTile cap = k.capital != null ? G.Tile(k.capital) : null;
            if (cap != null && important) p.Snap = cap.x + "," + cap.y;
            if (important && p.Img.Length == 0) p.Img = NetMedia.DefaultImage(kind);
            Geotag(p, cap, k);
            NetPress.Shape(p, k);
            _feed.Add(p);
            React(p, k, 1f + Population(k) * 0.02f);
            Version++;
            return p;
        }

        public static NetPost Person(Actor a, string kind, string text, float boost = 1f)
        {
            if (!G.Ok(a) || a.kingdom == null) return null;
            Kingdom k = a.kingdom;
            if (!Reach(k)) return null;
            bool online = Exists(k);
            if (Store.Str(a.data, KCancel) == "cancelled" && G.Chance(0.7f)) return null;
            NetPost p = Make("person", G.Id(a), a.getName(), Names.Handle(a), k, kind, text);
            p.Photo = G.Id(a);
            p.Place = a.city != null ? G.Name(a.city) : "";
            p.Story = online && G.Chance(0.3f);
            p.Verified = NetHub.IsVerified(a);
            p.Role = NetHub.RoleOf(a);
            if (!string.IsNullOrEmpty(Trend) && G.Chance(0.25f)) p.Tag = Trend;
            Geotag(p, a.current_tile, k);
            if (a.current_tile != null && (G.Chance(online ? 0.45f : 0.2f) || kind.StartsWith("live"))) p.Snap = a.current_tile.x + "," + a.current_tile.y;
            if (p.Snap.Length > 0 && p.Img.Length == 0) p.Img = NetMedia.PersonImage(kind, online);
            NetPress.Shape(p, k);
            _feed.Add(p);
            Store.Put(a.data, KLastPost, G.Year);
            React(p, k, boost * (1f + Followers(a) / 500f));
            Reception(a, p);
            Version++;
            return p;
        }

        public static NetPost Corporate(Company c, string kind, string text)
        {
            Kingdom k = G.KingdomById(c.Home);
            if (!Exists(k))
            {
                Chron(k, kind, text);
                return null;
            }
            NetPost p = Make("company", c.Id.ToString(), c.Name, Names.Handle(c), k, kind, text);
            p.Photo = c.Ceo;
            p.Verified = true;
            p.Role = "company";
            _feed.Add(p);
            React(p, k, 1f + Companies.Tier(c));
            Version++;
            return p;
        }

        private static int Population(Kingdom k)
        {
            return G.Pop(k);
        }

        private static void React(NetPost p, Kingdom k, float reach)
        {
            float audience = Math.Max(3f, Population(k) * 0.4f * reach);
            float appeal = UnityEngine.Random.Range(0.1f, 1f);
            if (p.Kind == "scandal" || p.Kind == "hot_take") appeal += 0.4f;
            p.Likes += (int)(audience * appeal * UnityEngine.Random.Range(0.3f, 1.2f));
            p.Dislikes += (int)(audience * (1f - appeal) * UnityEngine.Random.Range(0.02f, 0.3f));
            p.Shares += (int)(p.Likes * UnityEngine.Random.Range(0f, 0.15f));
            int comments = UnityEngine.Random.Range(0, 4) + (int)Math.Min(4f, reach * 0.5f);
            if (p.Med != "social") comments = Math.Min(comments, p.Med == "broadcast" ? 3 : 2);
            for (int i = 0; i < comments; i++) AddComment(p, k);
            if (p.Censored) p.Comments.Clear();
        }

        public static Actor RandomUser(Kingdom k)
        {
            if (k == null) return null;
            for (int i = 0; i < 6; i++)
            {
                City c = G.Pick(G.Cities(k));
                if (c == null) return null;
                int n = c.units.Count;
                if (n == 0) continue;
                Actor a = c.units[UnityEngine.Random.Range(0, n)];
                if (G.Ok(a) && a.isAdult() && a.isSapient()) return a;
            }
            return null;
        }

        public static void Year()
        {
            if (!On) return;
            var online = G.Kingdoms().Where(Exists).ToList();
            foreach (NetPost p in _feed.ToArray())
            {
                if (p.Year >= G.Year - 1 && !p.LiveEnded)
                {
                    if (p.Live)
                    {
                        p.LiveEnded = true;
                        Actor a = G.ActorById(p.AuthorId);
                        bool ok = G.Chance(0.6f);
                        if (a != null) Person(a, ok ? "live_ok" : "live_fail", Line(ok ? "net_live_ok" : "net_live_fail", p.Viewers));
                    }
                }
                if (p.Poll != null && p.PollWinner < 0 && G.Year >= p.PollEnds) ClosePoll(p);
            }
            CivMod.Safe("net-offline", () => NetLife.Offline(online));
            CivMod.Safe("net-hub-year", NetHub.Year);
            if (online.Count == 0)
            {
                Trim();
                return;
            }
            int per = CivMod.C != null ? CivMod.C.PostsPerYear : 6;
            foreach (Kingdom k in online)
            {
                float smart = Research.Has(k, "smartphones") ? 2f : Research.Has(k, "mobile_phones") ? 1.4f : 1f;
                if (Research.HasFeature(k, "social")) smart *= 1.5f;
                int n = (int)Math.Round(per * smart * Math.Min(3f, 0.5f + G.Pop(k) / 80f));
                for (int i = 0; i < n; i++) DailyPost(k);
                if (G.Chance(0.35f)) Recap(k);
                if (G.Chance(0.3f)) KingdomPoll(k);
                if (G.Chance(0.25f)) Live(k);
                if (G.Chance(0.15f)) Scandal(k);
                Extras(k, online);
                NetPress.Year(k);
                Addiction(k);
                Store.Put(k.data, KRep, Reputation(k) * 0.9f);
            }
            Viral();
            Trends(online);
            Followers(online);
            WorldExtras(online);
            Life(online);
            Trim();
        }

        private static void DailyPost(Kingdom k)
        {
            Actor a = RandomUser(k);
            if (a == null) return;
            Faith(a);
            string kind = PickKind(a);
            string text = Compose(a, kind);
            if (text == null) return;
            NetPost p = Person(a, kind, text);
            if (p != null && kind == "meme") p.Icon = MemeIcon();
            if (p != null && G.Chance(0.05f)) p.Viral = true;
        }

        private static bool Faith(Actor a)
        {
            return a.hasReligion();
        }

        private static string PickKind(Actor a)
        {
            var kinds = new List<string> { "daily", "daily", "food", "weather", "work", "opinion", "meme" };
            if (a.hasLover()) kinds.Add("love");
            if (a.hasReligion()) kinds.Add("faith");
            if (a.isWarrior()) kinds.Add("soldier");
            if (Economy.IsFamine(a.city)) kinds.Add("hunger");
            if (Pollution.Level(a.city) > 0.3f) kinds.Add("pollution");
            if (a.city != null && Energy.Blackout(a.city)) kinds.Add("blackout");
            if (Rails.LinksOf(a.city).Count > 0) kinds.Add("train");
            if (a.city != null && a.city.countBuildingsType("type_civ_airport") > 0) kinds.Add("travel");
            if (Xeno.Status(a) != Xeno.Citizen) kinds.Add("status");
            if (Followers(a) > 300) kinds.Add("brag");
            if (!string.IsNullOrEmpty(Trend)) kinds.Add("trend");
            if (a.money > 200) kinds.Add("rich");
            return G.Pick(kinds);
        }

        private static string Compose(Actor a, string kind)
        {
            string city = a.city != null ? G.Name(a.city) : G.Name(a.kingdom);
            string king = a.kingdom.hasKing() ? a.kingdom.king.getName() : "?";
            switch (kind)
            {
                case "work":
                {
                    string role = Roles.Of(a);
                    if (role.Length == 0) role = a.isWarrior() ? "soldier" : "citizen";
                    return Line("net_work_" + role, city);
                }
                case "opinion":
                {
                    Opinion o = Politics.Get(a);
                    if (o == null) return Line("net_daily", city);
                    string cur = Policies.Id(a.kingdom, "gov");
                    if (o.Gov != cur) return Line("net_oppose", Txt.T("civ_p_" + o.Gov), king);
                    if (o.Sat < 0.4f) return Line("net_complain", king, city);
                    return Line("net_praise", king, Txt.T("civ_p_" + Policies.Id(a.kingdom, "ideo")));
                }
                case "love":
                    return Line("net_love", a.lover != null ? a.lover.getName() : "?");
                case "faith":
                    return Line("net_faith", a.religion != null && a.religion.data != null ? a.religion.data.name : "?");
                case "trend":
                    return Line("net_trend", Trend);
                case "status":
                    return Line(Xeno.Status(a) == Xeno.Slave ? "net_slave" : "net_servant", G.Name(a.kingdom));
                case "brag":
                    return Line("net_brag", Followers(a));
                case "travel":
                    return Line("net_travel", G.Name(G.Pick(G.Cities())));
                case "train":
                    return Line("net_train", city);
                case "rich":
                    return Line("net_rich", a.money);
                default:
                    return Line("net_" + kind, city, king);
            }
        }

        private static void Recap(Kingdom k)
        {
            string era = Db.EraName(Research.Era(k));
            Official(k, "recap", Line("net_recap", G.Name(k), G.Year, era, G.Pop(k), Txt.T("civ_p_" + Policies.Id(k, "gov"))), false);
        }

        private static void KingdomPoll(Kingdom k)
        {
            string axis = G.Pick(new[] { "gov", "ideo", "econ" });
            List<PolicyDef> opts = Policies.Options(k, axis);
            if (opts.Count < 2) return;
            var picks = opts.OrderBy(x => UnityEngine.Random.value).Take(Math.Min(3, opts.Count)).ToList();
            if (!picks.Any(x => x.Id == Policies.Id(k, axis))) picks[0] = Policies.Get(k, axis);
            NetPost p = Official(k, "poll", Line("net_poll_" + axis, G.Name(k)), false);
            if (p == null) return;
            p.Poll = picks.Select(x => x.Id).ToList();
            p.Votes = picks.Select(x => 0).ToList();
            p.PollEnds = G.Year + 1;
            foreach (City c in G.Cities(k))
            {
                int budget = 20;
                foreach (Actor a in c.units)
                {
                    if (budget-- <= 0) break;
                    Opinion o = Politics.Get(a);
                    if (o == null) continue;
                    string fav = axis == "gov" ? o.Gov : axis == "ideo" ? o.Ideo : o.Econ;
                    int idx = p.Poll.IndexOf(fav);
                    if (idx < 0) idx = UnityEngine.Random.Range(0, p.Poll.Count);
                    p.Votes[idx]++;
                }
            }
        }

        public static void Vote(NetPost p, int option)
        {
            if (p == null || p.Poll == null || p.PollWinner >= 0 || option < 0 || option >= p.Votes.Count) return;
            p.Votes[option] += Math.Max(5, p.Votes.Sum() / 4);
            Version++;
        }

        private static void ClosePoll(NetPost p)
        {
            int win = 0;
            for (int i = 1; i < p.Votes.Count; i++) if (p.Votes[i] > p.Votes[win]) win = i;
            p.PollWinner = win;
            Kingdom k = G.KingdomById(p.Kingdom);
            if (k == null) return;
            string id = p.Poll[win];
            PolicyDef pd = Db.Policy(id);
            if (pd != null && pd.Id != Policies.Id(k, pd.Group))
            {
                TrendPolicy = id;
                if (G.Chance(0.25f) && Policies.Unlocked(k, pd)) Policies.Set(k, pd.Group, id, "poll");
            }
        }

        private static void Live(Kingdom k)
        {
            Actor a = RandomUser(k);
            if (a == null) return;
            string kind = a.isWarrior() && World.world.wars.hasWars(k) ? "live_war" : Roles.Of(a) == Roles.Scientist ? "live_lab" : a.isKing() ? "live_king" : "live";
            NetPost p = Person(a, "live", Line("net_" + kind, G.Name(a.city)), 2f);
            if (p == null) return;
            p.Live = true;
            p.Viewers = UnityEngine.Random.Range(10, 50) + Followers(a) / 5;
            p.Important = Followers(a) > 1500;
        }

        private static void Scandal(Kingdom k)
        {
            Actor a = null;
            int best = 0;
            foreach (City c in G.Cities(k))
            {
                foreach (Actor u in c.units)
                {
                    if (!G.Ok(u)) continue;
                    int f = Followers(u) + (u.isKing() ? 500 : 0) + (u.isCityLeader() ? 200 : 0);
                    if (f > best && G.Chance(0.5f))
                    {
                        best = f;
                        a = u;
                    }
                }
            }
            if (a == null) return;
            NetPost p = Person(a, "scandal", Line("net_scandal", a.getName()), 3f);
            if (p == null) return;
            int strikes = Store.Int(a.data, KStrikes, 0) + 1;
            Store.Put(a.data, KStrikes, strikes);
            if (strikes >= 2 && Store.Str(a.data, KCancel).Length == 0)
            {
                Store.Put(a.data, KCancel, "cancelled");
                Moods.Apply(a, "civ_cancelled");
                Person(a, "apology", Line("net_apology"), 2f);
                Store.Put(a.data, KFollow, Followers(a) / 3);
                if (a.isKing()) Politics.AddStability(k, -0.05f);
            }
        }

        private static void Addiction(Kingdom k)
        {
            if (!Research.HasFeature(k, "social")) return;
            foreach (City c in G.Cities(k))
            {
                if (G.Chance(0.3f)) Moods.Apply(c, "civ_addicted", 0.02f);
            }
        }

        private static void Viral()
        {
            foreach (NetPost p in _feed.Where(x => x.Year == G.Year && !x.Viral).OrderByDescending(x => x.Likes + x.Shares * 3).Take(2).ToList())
            {
                if (!G.Chance(0.6f)) continue;
                p.Viral = true;
                p.Likes *= 4;
                p.Shares *= 6;
                Actor a = G.ActorById(p.AuthorId);
                if (a != null)
                {
                    Store.Put(a.data, KFollow, Followers(a) + p.Shares + 50);
                    Moods.Apply(a, "civ_viral");
                    Company sponsor = Companies.Ranking(5).FirstOrDefault(x => x.Sector == "media" || x.Sector == "tech" || x.Sector == "trade");
                    if (sponsor != null && G.Chance(0.4f))
                    {
                        int pay = (int)Math.Min(200f, sponsor.Cash * 0.02f);
                        sponsor.Cash -= pay;
                        a.data.money += pay;
                        Person(a, "sponsor", Line("net_sponsor", sponsor.Name), 1f);
                    }
                }
                Kingdom k = G.KingdomById(p.Kingdom);
                if (k != null) AddRep(k, 0.03f);
            }
        }

        private static readonly string[] TrendWords = { "desafio", "dança", "receita", "meme", "foto", "música", "piada", "moda", "jogo", "frase" };

        private static void Trends(List<Kingdom> online)
        {
            if (G.Chance(0.4f) || string.IsNullOrEmpty(Trend))
            {
                string word = Txt.T("net_trendword_" + UnityEngine.Random.Range(0, 20));
                Trend = "#" + word.Replace(" ", "") + G.Year % 100;
                Kingdom k = G.Pick(online);
                Actor a = k != null ? RandomUser(k) : null;
                if (a != null)
                {
                    NetPost p = Person(a, "trend_start", Line("net_trend_start", Trend), 3f);
                    if (p != null) p.Tag = Trend;
                }
            }
            if (G.Chance(0.2f)) TrendPolicy = "";
        }

        private static void Followers(List<Kingdom> online)
        {
            foreach (NetPost p in _feed.Where(x => x.Year == G.Year && x.Who == "person"))
            {
                Actor a = G.ActorById(p.AuthorId);
                if (a == null) continue;
                int gain = p.Likes / 20 + p.Shares / 4;
                if (a.isKing()) gain += 30;
                Store.Put(a.data, KFollow, Followers(a) + gain);
                if (Store.Str(a.data, KCancel) == "cancelled" && G.Chance(0.3f)) Store.Put(a.data, KCancel, "");
            }
        }

        public static void Tick(float dt)
        {
            if (!On) return;
            _tick += dt;
            if (_tick < 20f) return;
            _tick = 0f;
            foreach (NetPost p in _feed)
            {
                if (p.Live && !p.LiveEnded)
                {
                    p.Viewers += UnityEngine.Random.Range(0, 6);
                    if (G.Chance(0.3f))
                    {
                        Kingdom k = G.KingdomById(p.Kingdom);
                        if (k != null) AddComment(p, k);
                    }
                }
                else if (p.Year >= G.Year - 1 && G.Chance(0.05f))
                {
                    p.Likes += UnityEngine.Random.Range(0, 3);
                }
            }
        }

        public static string Babel(NetPost p)
        {
            if (string.IsNullOrEmpty(p.Lang)) return p.Text;
            int seed = 17;
            foreach (char c in p.Lang) seed = seed * 31 + c;
            var rnd = new System.Random(seed);
            string[] syl = new string[8];
            string cons = "bcdfghjklmnprstvz";
            string vow = "aeiouy";
            for (int i = 0; i < syl.Length; i++) syl[i] = cons[rnd.Next(cons.Length)].ToString() + vow[rnd.Next(vow.Length)] + (rnd.Next(3) == 0 ? cons[rnd.Next(cons.Length)].ToString() : "");
            var sb = new StringBuilder();
            foreach (string w in p.Text.Split(' '))
            {
                if (w.Length == 0) continue;
                if (w.StartsWith("#") || w.StartsWith("@") || char.IsDigit(w[0]) || char.IsUpper(w[0]))
                {
                    sb.Append(w).Append(' ');
                    continue;
                }
                int h = 0;
                foreach (char c in w) h = h * 7 + c;
                int n = Math.Max(1, w.Length / 3);
                for (int i = 0; i < n; i++) sb.Append(syl[Math.Abs(h + i * 13) % syl.Length]);
                sb.Append(' ');
            }
            return sb.ToString().Trim();
        }

        public static bool Foreign(NetPost p, Kingdom viewer)
        {
            if (viewer == null || viewer.language == null || viewer.language.data == null) return !string.IsNullOrEmpty(p.Lang) && p.Lang != "";
            return !string.IsNullOrEmpty(p.Lang) && p.Lang != viewer.language.data.id.ToString();
        }

        public static void Like(NetPost p)
        {
            if (p == null) return;
            p.Liked = !p.Liked;
            p.Likes += p.Liked ? 1 : -1;
            Version++;
        }

        public static void SaveToggle(NetPost p)
        {
            if (p == null) return;
            p.Saved = !p.Saved;
            Version++;
        }

        public static void OnTech(Kingdom k, TechDef t, string reason)
        {
            if (!t.IsCore && !t.Rare) return;
            string name = Db.TechName(t.Id);
            if (reason == "company") return;
            if (Exists(k) && G.Chance(0.5f))
            {
                Actor sci = null;
                foreach (City c in G.Cities(k))
                {
                    foreach (Actor a in Roles.Members(c, Roles.Scientist))
                    {
                        sci = a;
                        break;
                    }
                    if (sci != null) break;
                }
                if (sci != null)
                {
                    Person(sci, "science", Line("net_science", name), 1.5f);
                    return;
                }
            }
            if (!Exists(k) && !t.Rare && !G.Chance(0.25f)) return;
            Official(k, "tech", Line("net_tech", G.Name(k), name), t.Rare);
        }

        public static void OnEra(Kingdom k, int era)
        {
            Official(k, "era", Line("net_era", G.Name(k), Db.EraName(era)), true);
            Notify.Show(Txt.F("net_era_0", G.Name(k), Db.EraName(era)));
        }

        public static void OnTechLost(Kingdom k, TechDef t)
        {
            Official(k, "tech_lost", Line("net_tech_lost", G.Name(k), Db.TechName(t.Id)), false);
        }

        public static void OnDiscovery(Kingdom k, TechDef t)
        {
            Official(k, "discovery", Line("net_discovery", G.Name(k), Db.TechName(t.Id)), true);
        }

        public static void OnTheft(Kingdom k, Kingdom from, int n)
        {
            Official(k, "theft", Line("net_theft", G.Name(k), G.Name(from), n), false);
        }

        public static void OnStage(Kingdom k, string stage)
        {
            Official(k, "stage", Line("net_stage", G.Name(k), Txt.T("civ_stage_" + stage)), true);
        }

        public static void OnRoute(Kingdom k, string route, string old)
        {
            Official(k, "route", Line("net_route", G.Name(k), Txt.T("civ_route_" + route)), true);
        }

        public static void OnPolicy(Kingdom k, string group, string old, string now, string reason)
        {
            Official(k, "policy", Line("net_policy_" + reason, G.Name(k), Txt.T("civ_p_" + old), Txt.T("civ_p_" + now)), group == "gov");
        }

        public static void OnRevolution(Kingdom k, string axis, string from, string to)
        {
            Official(k, "revolution", Line("net_revolution", G.Name(k), Txt.T("civ_p_" + from), Txt.T("civ_p_" + to)), true);
            foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_revolution", 0.2f);
        }

        public static void OnCampaign(Kingdom k, List<Actor> cands)
        {
            foreach (Actor a in cands)
            {
                Opinion o = Politics.Get(a);
                if (o == null) continue;
                Person(a, "campaign", Line("net_campaign", Txt.T("civ_p_" + o.Ideo), Txt.T("civ_p_" + o.Econ)), 2f);
            }
            Debate(k, cands);
        }

        public static void OnElection(Kingdom k, List<Actor> cands, float[] votes, int win, int voters)
        {
            float total = votes.Sum();
            var sb = new StringBuilder();
            for (int i = 0; i < cands.Count; i++)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(cands[i].getName()).Append(' ').Append(total > 0 ? Math.Round(votes[i] * 100f / total) : 0).Append('%');
            }
            Official(k, "election", Line("net_election", G.Name(k), cands[win].getName(), sb.ToString()), true);
            Moods.Apply(cands[win], "civ_election_won");
            for (int i = 0; i < cands.Count; i++) if (i != win) Moods.Apply(cands[i], "civ_election_lost");
        }

        public static void OnRivalry(Clan a, Clan b)
        {
            Kingdom k = Clans.HomeOf(a);
            Official(k, "clan", Line("net_rivalry", a.data.name, b.data.name), false);
        }

        public static void OnPact(Clan a, Clan b)
        {
            Kingdom k = Clans.HomeOf(a);
            Official(k, "clan", Line("net_pact", a.data.name, b.data.name), false);
        }

        public static void OnClanRebellion(Clan c, Kingdom from, Kingdom created)
        {
            Official(from, "rebellion", Line("net_clan_rebellion", c.data.name, G.Name(from), G.Name(created)), true);
        }

        public static void OnCoup(Clan c, Kingdom k, Actor newKing, Actor old)
        {
            Official(k, "coup", Line("net_coup", c.data.name, newKing.getName(), G.Name(k)), true);
        }

        public static void OnFamine(City c)
        {
            Official(c.kingdom, "famine", Line("net_famine", G.Name(c)), false);
        }

        public static void OnSack(City c, Kingdom from, Kingdom by)
        {
            Official(by, "sack", Line("net_sack", G.Name(by), G.Name(c), G.Name(from)), true);
        }

        public static void OnVehicle(Actor v, string id)
        {
            if (G.Chance(0.2f)) Person(v, "vehicle", Line("net_vehicle", Txt.T(id)), 1f);
        }

        public static void OnVehicleLost(Actor v)
        {
        }

        public static void OnEmbassy(Actor a, Kingdom k)
        {
            if (G.Chance(0.3f)) Person(a, "embassy", Line("net_embassy", G.Name(k)), 1f);
        }

        public static void OnNukeBuilt(Kingdom k)
        {
            Official(k, "nuke_built", Line("net_nuke_built", G.Name(k)), true);
        }

        public static void OnNukeLaunched(Kingdom k, City target, bool icbm)
        {
            Official(k, "nuke_launch", Line(icbm ? "net_icbm" : "net_nuke_bomber", G.Name(k), G.Name(target)), true);
        }

        public static void OnNuke(Kingdom k, City target)
        {
            Official(k, "nuke", Line("net_nuke", G.Name(k), target != null ? G.Name(target) : "?"), true);
            AddRep(k, -0.6f);
            Notify.Show(Txt.F("net_nuke_0", G.Name(k), target != null ? G.Name(target) : "?"));
        }

        public static void OnCompanyFounded(Company co, Kingdom k, Actor founder)
        {
            NetPost p = Corporate(co, "company", Line("net_company", co.Name, Txt.T("civ_sector_" + co.Sector), founder.getName()));
            if (p == null) Chron(k, "company", Line("net_company", co.Name, Txt.T("civ_sector_" + co.Sector), founder.getName()));
        }

        public static void OnCeoChange(Company co, Actor ceo)
        {
            Corporate(co, "ceo", Line("net_ceo", co.Name, ceo.getName()));
        }

        public static void OnPatent(Company co, Kingdom k, TechDef t)
        {
            Corporate(co, "patent", Line("net_patent", co.Name, Db.TechName(t.Id)));
        }

        public static void OnBailout(Company co, Kingdom k)
        {
            Official(k, "bailout", Line("net_bailout", G.Name(k), co.Name), false);
        }

        public static void OnNationalized(Company co, Kingdom k)
        {
            Official(k, "nationalized", Line("net_nationalized", G.Name(k), co.Name), true);
        }

        public static void OnPrivatized(Company co, Kingdom k)
        {
            Official(k, "privatized", Line("net_privatized", G.Name(k), co.Name), false);
        }

        public static void OnBranch(Company co, Kingdom k, Kingdom o)
        {
            Corporate(co, "branch", Line("net_branch", co.Name, G.Name(o)));
        }

        public static void OnMerger(Company big, Company small)
        {
            Corporate(big, "merger", Line("net_merger", big.Name, small.Name));
        }

        public static void OnBankrupt(Company co, string reason)
        {
            Kingdom k = G.KingdomById(co.Home);
            Official(k, "bankrupt", Line("net_bankrupt", co.Name), false);
        }

        public static void OnCurrency(Kingdom k, string name)
        {
            Official(k, "currency", Line("net_currency", G.Name(k), name), false);
        }

        public static void OnCurrencyReform(Kingdom k, string old, string now)
        {
            Official(k, "currency", Line("net_currency_reform", G.Name(k), old, now), true);
        }

        public static void OnCurrencyUnion(Kingdom k, Kingdom with)
        {
            Official(k, "currency", Line("net_currency_union", G.Name(k), Currency.Name(with)), false);
        }

        public static void OnHyperinflation(Kingdom k, float infl)
        {
            Official(k, "inflation", Line("net_inflation", G.Name(k), Math.Round(infl * 100f)), false);
        }

        public static void OnSubjugation(City c, Kingdom conqueror, Kingdom loser, string stance, int n)
        {
            Official(conqueror, "conquest", Line("net_conquest_" + stance, G.Name(conqueror), G.Name(c), n), true);
        }

        public static void OnAbolition(Kingdom k)
        {
            Official(k, "abolition", Line("net_abolition", G.Name(k)), true);
            foreach (City c in G.Cities(k)) Moods.Apply(c, "civ_freed", 0.3f);
        }

        public static void OnSlaveRevolt(City c, Kingdom old, Kingdom nk, Actor leader)
        {
            Official(nk, "revolt", Line("net_slave_revolt", leader.getName(), G.Name(c), G.Name(old)), true);
        }

        public static void OnBlackout(City c)
        {
            Official(c.kingdom, "blackout", Line("net_blackout_city", G.Name(c)), false);
        }

        public static void OnRailOpened(City a, City b, string kind)
        {
            if (a == null || b == null) return;
            Official(a.kingdom, "rail", Line("net_rail", G.Name(a), G.Name(b), Txt.T("civ_train_" + kind)), false);
        }

        public static void OnRailSabotage(City a, City b)
        {
            Official(a.kingdom, "rail", Line("net_rail_sabotage", G.Name(a), G.Name(b)), false);
        }

        public static void OnLaunch(Kingdom k, string mission, bool first, Company by)
        {
            string text = Line("net_launch_" + mission, G.Name(k), by != null ? by.Name : G.Name(k));
            if (by != null) Corporate(by, "launch", text);
            else Official(k, "launch", text, first);
            if (first) Notify.Show(Txt.F("net_first_" + mission, G.Name(k)));
        }

        public static void OnWalls(City c, int level)
        {
            if (G.Chance(0.3f)) Official(c.kingdom, "walls", Line("net_walls", G.Name(c)), false);
        }

        public static void OnWallsDown(City c)
        {
            Official(c.kingdom, "walls", Line("net_walls_down", G.Name(c)), false);
        }

        public static void OnBorderWall(Kingdom k, Kingdom o)
        {
            Official(k, "border", Line("net_border_wall", G.Name(k), G.Name(o)), true);
        }
    }
}
