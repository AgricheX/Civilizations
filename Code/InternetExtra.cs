using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static partial class Internet
    {
        private const string KCoin = "civ_coin";
        private const string KCoinP = "civ_coin_p";
        private const string KCoinHigh = "civ_coin_hi";
        private const string KCelebs = "civ_net_celebs";
        private const string KRecapYear = "civ_net_recap_y";
        private static readonly string[] CoinNames = { "BitOuro", "DogeRei", "LunaCash", "Moedinha", "SatoX", "PixelCoin", "Trigo$", "MemeToken", "OrbitaCoin", "Kripto" };
        private static readonly string[] Topics = { "farmers", "soldiers", "science", "faith", "gamers", "cooking", "music", "sports", "memes", "history", "fashion", "pets" };
        private static readonly string[] MemeIcons =
        {
            "ui/Icons/civ/mood/liked", "ui/Icons/civ/mood/disliked", "ui/Icons/civ/mood/famine", "ui/Icons/civ/mood/cold", "ui/Icons/civ/mood/new_home",
            "ui/Icons/civ/mood/lost_home", "ui/Icons/civ/mood/viral", "ui/Icons/civ/mood/addicted", "ui/Icons/civ/mood/blackout", "ui/Icons/civ/mood/pollution",
            "ui/Icons/civ/mood/space", "ui/Icons/civ/mood/train_trip", "ui/Icons/civ/mood/flight", "ui/Icons/civ/mood/new_clothes", "ui/Icons/civ/net/ghost",
            "ui/Icons/civ/net/memes_cat", "ui/Icons/civ/net/memes_dog", "ui/Icons/civ/net/memes_skull", "ui/Icons/civ/net/memes_fire", "ui/Icons/civ/net/memes_brain"
        };

        public static string CoinName
        {
            get
            {
                SaveCustomData d = Store.World();
                return d == null ? "" : Store.Str(d, KCoin);
            }
        }

        public static float CoinPrice
        {
            get
            {
                SaveCustomData d = Store.World();
                return d == null ? 0f : Store.Flt(d, KCoinP, 0f);
            }
        }

        private static string MemeIcon()
        {
            return MemeIcons[UnityEngine.Random.Range(0, MemeIcons.Length)];
        }

        private static void Extras(Kingdom k, List<Kingdom> online)
        {
            if (G.Chance(0.6f)) Dms(k);
            if (G.Chance(0.5f)) Community(k);
            if (G.Chance(0.22f)) FakeNews(k);
            if (G.Chance(0.12f) && online.Count > 1) Hack(k, online);
            if (G.Chance(0.1f)) Leak(k);
            if (G.Chance(0.2f)) Collab(k);
            if (G.Chance(0.12f)) Breakup(k);
            if (G.Chance(0.15f)) Pressure(k);
            if (G.Chance(0.2f)) Challenge(k);
        }

        private static void WorldExtras(List<Kingdom> online)
        {
            FactCheck();
            Market(online);
            Stocks();
            Celebs(online);
            Obituaries();
            WorldRecap(online);
        }

        private static NetPost Dm(Actor from, Actor to, string text)
        {
            if (!G.Ok(from) || !G.Ok(to) || from.kingdom == null || !Exists(from.kingdom)) return null;
            NetPost p = Make("person", G.Id(from), from.getName(), Names.Handle(from), from.kingdom, "dm", text);
            p.Dm = true;
            p.ToId = G.Id(to);
            p.ToName = to.getName();
            p.Photo = G.Id(from);
            _feed.Add(p);
            Version++;
            return p;
        }

        private static void Dms(Kingdom k)
        {
            Actor a = RandomUser(k);
            if (a == null) return;
            if (a.hasLover() && G.Ok(a.lover))
            {
                Dm(a, a.lover, Line("net_dm_love", a.lover.getName()));
                if (G.Chance(0.7f)) Dm(a.lover, a, Line("net_dm_love_r", a.getName()));
                return;
            }
            Actor b = RandomUser(k);
            if (b == null || b == a) return;
            if (Followers(b) > 300 && G.Chance(0.5f))
            {
                Dm(a, b, Line("net_dm_fan", b.getName()));
                return;
            }
            Dm(a, b, Line("net_dm", b.getName(), a.city != null ? G.Name(a.city) : "?"));
            if (G.Chance(0.5f)) Dm(b, a, Line("net_dm_r", a.getName()));
        }

        private static string TopicOf(Actor a)
        {
            string role = Roles.Of(a);
            if (a.isWarrior()) return "soldiers";
            if (role == Roles.Scientist) return "science";
            if (a.hasReligion() && G.Chance(0.4f)) return "faith";
            if (role == Roles.Worker) return "farmers";
            return Topics[UnityEngine.Random.Range(0, Topics.Length)];
        }

        private static void Community(Kingdom k)
        {
            Actor a = RandomUser(k);
            if (a == null) return;
            string topic = TopicOf(a);
            string name = topic == "faith" && a.religion != null && a.religion.data != null ? a.religion.data.name : Txt.T("net_comm_" + topic);
            string tag = "c/" + name.Replace(" ", "");
            NetPost p = Person(a, "community", Line("net_comm_post_" + topic, name, a.city != null ? G.Name(a.city) : "?"), 1.3f);
            if (p == null) return;
            p.Community = tag;
            if (topic == "memes") p.Icon = MemeIcon();
        }

        private static void FakeNews(Kingdom k)
        {
            Actor a = RandomUser(k);
            if (a == null) return;
            Kingdom other = G.Pick(G.Kingdoms().Where(x => x != k && x.isCiv()).ToList());
            string target = other != null ? G.Name(other) : G.Name(k);
            string king = k.hasKing() ? k.king.getName() : G.Name(k);
            NetPost p = Person(a, "fake", Line("net_fake", target, king), 2.5f);
            if (p == null) return;
            p.Fake = true;
            p.Likes = (int)(p.Likes * 1.8f);
            p.Shares = (int)(p.Shares * 2.5f) + 3;
            if (G.Chance(0.3f)) Politics.AddStability(k, -0.01f);
        }

        private static void FactCheck()
        {
            foreach (NetPost p in _feed.Where(x => x.Fake && !x.Checked && x.Year >= G.Year - 2).ToList())
            {
                if (!G.Chance(0.6f)) continue;
                p.Checked = true;
                Kingdom k = G.KingdomById(p.Kingdom);
                if (k == null) continue;
                Company media = Companies.Of(k).FirstOrDefault(c => c.Sector == "media" || c.Sector == "tech");
                string text = Line("net_factcheck", p.Author, p.Handle);
                NetPost chk = media != null ? Corporate(media, "factcheck", text) : Official(k, "factcheck", text, false);
                if (chk != null) chk.Tag = "#" + Txt.T("net_fact_tag");
                Actor a = G.ActorById(p.AuthorId);
                if (a != null)
                {
                    Store.Put(a.data, KFollow, Followers(a) * 2 / 3);
                    Store.Put(a.data, KStrikes, Store.Int(a.data, KStrikes, 0) + 1);
                }
            }
        }

        private static void Hack(Kingdom k, List<Kingdom> online)
        {
            Kingdom victim = G.Pick(online.Where(x => x != k).ToList());
            if (victim == null) return;
            bool company = G.Chance(0.5f);
            Company target = company ? G.Pick(Companies.Of(victim)) : null;
            string what = target != null ? target.Name : G.Name(victim);
            if (target != null)
            {
                float loss = Math.Max(0f, target.Cash * UnityEngine.Random.Range(0.03f, 0.12f));
                target.Cash -= loss;
                Corporate(target, "hacked", Line("net_hacked_co", target.Name, G.Short(loss)));
            }
            else
            {
                Official(victim, "hacked", Line("net_hacked_gov", G.Name(victim)), true);
                Politics.AddStability(victim, -0.02f);
            }
            AddRep(victim, -0.04f);
            bool caught = G.Chance(0.4f);
            if (caught)
            {
                Official(victim, "hack_blame", Line("net_hack_blame", G.Name(k), what), true);
                AddRep(k, -0.06f);
            }
            else
            {
                Actor h = RandomUser(k);
                if (h != null) Person(h, "hacker", Line("net_hacker", what), 1.5f);
            }
        }

        private static void Leak(Kingdom k)
        {
            if (!k.hasKing()) return;
            Actor a = RandomUser(k);
            string king = k.king.getName();
            NetPost p = a != null ? Person(a, "leak", Line("net_leak", king, G.Name(k)), 3f) : null;
            if (p == null) return;
            p.Important = true;
            Politics.AddStability(k, -0.02f);
            AddRep(k, -0.03f);
            if (G.Chance(0.5f)) Official(k, "leak_reply", Line("net_leak_reply", G.Name(k)), false);
        }

        private static List<Actor> Influencers(Kingdom k, int min)
        {
            var list = new List<Actor>();
            foreach (City c in G.Cities(k))
            {
                foreach (Actor a in c.units)
                {
                    if (G.Ok(a) && Followers(a) >= min) list.Add(a);
                }
            }
            return list;
        }

        private static void Collab(Kingdom k)
        {
            List<Actor> inf = Influencers(k, 150).OrderByDescending(Followers).Take(6).ToList();
            if (inf.Count < 2) return;
            Actor a = inf[UnityEngine.Random.Range(0, inf.Count)];
            Actor b = inf[UnityEngine.Random.Range(0, inf.Count)];
            if (a == b) return;
            NetPost p = Person(a, "collab", Line("net_collab", Names.Handle(b), b.getName()), 2.5f);
            if (p == null) return;
            int gain = (Followers(a) + Followers(b)) / 12 + 20;
            Store.Put(a.data, KFollow, Followers(a) + gain);
            Store.Put(b.data, KFollow, Followers(b) + gain);
            if (G.Chance(0.5f)) Person(b, "collab", Line("net_collab_r", Names.Handle(a)), 1.5f);
        }

        private static void Breakup(Kingdom k)
        {
            List<Actor> inf = Influencers(k, 300).Where(x => x.hasLover() && G.Ok(x.lover)).ToList();
            Actor a = G.Pick(inf);
            if (a == null) return;
            Actor fan = RandomUser(k);
            if (fan != null && G.Chance(0.6f))
            {
                NetPost rumor = Person(fan, "gossip", Line("net_gossip", a.getName(), a.lover.getName()), 2.5f);
                if (rumor != null) rumor.Tag = "#" + Root(a.getName()) + Root(a.lover.getName());
            }
            else
            {
                Person(a, "breakup", Line("net_breakup", a.lover.getName()), 3f);
                Store.Put(a.data, KFollow, Followers(a) + Followers(a) / 8);
                Moods.Apply(a, "civ_disliked");
            }
        }

        private static string Root(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string w = s.Split(' ')[0];
            return new string(w.Where(char.IsLetter).ToArray());
        }

        private static void Pressure(Kingdom k)
        {
            string axis = G.Pick(new[] { "gov", "ideo", "econ", "xeno" });
            List<PolicyDef> opts = Policies.Options(k, axis).Where(x => x.Id != Policies.Id(k, axis)).ToList();
            PolicyDef want = G.Pick(opts);
            if (want == null) return;
            string tag = "#" + Txt.T("civ_p_" + want.Id).Replace(" ", "").Replace("-", "") + Txt.T("net_now_suffix");
            Actor a = RandomUser(k);
            if (a == null) return;
            NetPost p = Person(a, "pressure", Line("net_pressure", Txt.T("civ_p_" + want.Id), G.Name(k)), 3f);
            if (p == null) return;
            p.Tag = tag;
            int joined = UnityEngine.Random.Range(2, 6);
            for (int i = 0; i < joined; i++)
            {
                Actor b = RandomUser(k);
                if (b == null) continue;
                NetPost q = Person(b, "pressure", Line("net_pressure_join", tag), 1f);
                if (q != null) q.Tag = tag;
            }
            Trend = tag;
            TrendPolicy = want.Id;
            float pull = 0.15f + Math.Max(0f, 0.5f - Politics.Stability(k)) * 0.5f;
            if (G.Chance(pull) && Policies.Unlocked(k, want))
            {
                Policies.Set(k, want.Group, want.Id, "net");
                Official(k, "pressure_win", Line("net_pressure_win", Txt.T("civ_p_" + want.Id), tag), true);
            }
            else if (G.Chance(0.5f))
            {
                Official(k, "pressure_no", Line("net_pressure_no", tag), false);
                Politics.AddStability(k, -0.015f);
            }
        }

        private static void Challenge(Kingdom k)
        {
            Actor a = RandomUser(k);
            if (a == null) return;
            string word = Txt.T("net_challenge_" + UnityEngine.Random.Range(0, 12));
            string tag = "#" + Txt.T("net_challenge_tag") + word.Replace(" ", "");
            NetPost p = Person(a, "challenge", Line("net_challenge_start", tag, word), 2.5f);
            if (p == null) return;
            p.Tag = tag;
            p.Icon = MemeIcon();
            int n = UnityEngine.Random.Range(2, 7);
            for (int i = 0; i < n; i++)
            {
                Kingdom src = G.Chance(0.3f) ? G.Pick(G.Kingdoms().Where(Exists).ToList()) ?? k : k;
                Actor b = RandomUser(src);
                if (b == null) continue;
                NetPost q = Person(b, "challenge", Line("net_challenge_do", tag), 1f);
                if (q == null) continue;
                q.Tag = tag;
                if (G.Chance(0.4f)) q.Icon = MemeIcon();
            }
            if (G.Chance(0.15f))
            {
                Actor hurt = RandomUser(k);
                if (hurt != null) Official(k, "challenge_warn", Line("net_challenge_warn", tag), false);
            }
        }

        private static void Market(List<Kingdom> online)
        {
            SaveCustomData d = Store.World();
            if (d == null || online.Count == 0) return;
            bool modern = online.Any(x => Research.Has(x, "smartphones") || Research.Era(x) >= 13);
            string coin = Store.Str(d, KCoin);
            if (coin.Length == 0)
            {
                if (!modern || !G.Chance(0.35f)) return;
                coin = CoinNames[UnityEngine.Random.Range(0, CoinNames.Length)];
                Store.Put(d, KCoin, coin);
                Store.Put(d, KCoinP, 1f);
                Store.Put(d, KCoinHigh, 1f);
                Kingdom k0 = G.Pick(online);
                Actor dev = k0 != null ? RandomUser(k0) : null;
                if (dev != null) Person(dev, "crypto_launch", Line("net_crypto_launch", coin), 2f);
                return;
            }
            float price = Store.Flt(d, KCoinP, 1f);
            float old = price;
            float r = UnityEngine.Random.value;
            if (r < 0.1f) price *= UnityEngine.Random.Range(2f, 5f);
            else if (r < 0.2f) price *= UnityEngine.Random.Range(0.1f, 0.4f);
            else price *= UnityEngine.Random.Range(0.75f, 1.35f);
            price = Math.Max(0.01f, Math.Min(99999f, price));
            Store.Put(d, KCoinP, price);
            float hi = Math.Max(Store.Flt(d, KCoinHigh, 1f), price);
            Store.Put(d, KCoinHigh, hi);
            float ch = price / Math.Max(0.01f, old) - 1f;
            if (Math.Abs(ch) < 0.25f) return;
            Kingdom k = G.Pick(online);
            Actor a = k != null ? RandomUser(k) : null;
            if (a == null) return;
            string pct = Math.Round(ch * 100f) + "%";
            if (ch > 0)
            {
                NetPost p = Person(a, "crypto_up", Line("net_crypto_up", coin, pct, price.ToString("0.##")), 2f);
                if (p != null) p.Tag = "#" + coin;
                a.data.money += UnityEngine.Random.Range(5, 60);
            }
            else
            {
                NetPost p = Person(a, "crypto_down", Line("net_crypto_down", coin, pct, price.ToString("0.##")), 2f);
                if (p != null) p.Tag = "#" + coin;
                a.data.money = Math.Max(0, a.data.money - UnityEngine.Random.Range(5, 60));
                Moods.Apply(a, "civ_disliked");
            }
        }

        private static void Stocks()
        {
            foreach (Company c in Companies.Ranking(6))
            {
                if (c.History == null || c.History.Count < 2) continue;
                float prev = c.History[c.History.Count - 2];
                float now = c.History[c.History.Count - 1];
                if (prev <= 0f) continue;
                float ch = now / prev - 1f;
                if (Math.Abs(ch) < 0.2f || !G.Chance(0.6f)) continue;
                Kingdom k = G.KingdomById(c.Home);
                if (!Exists(k)) continue;
                Actor a = RandomUser(k);
                if (a == null) continue;
                NetPost p = Person(a, ch > 0 ? "stock_up" : "stock_down", Line(ch > 0 ? "net_stock_up" : "net_stock_down", c.Name, Math.Round(Math.Abs(ch) * 100f) + "%"), 1.5f);
                if (p != null) p.Tag = "$" + Root(c.Name).ToUpperInvariant();
            }
        }

        private static void Celebs(List<Kingdom> online)
        {
            SaveCustomData d = Store.World();
            if (d == null) return;
            HashSet<string> set = Store.Set(d, KCelebs);
            var known = new HashSet<string>(set.Select(x => x.Split('|')[0]));
            foreach (Kingdom k in online)
            {
                foreach (Actor a in Influencers(k, 300))
                {
                    string id = G.Id(a);
                    if (known.Contains(id)) continue;
                    set.Add(id + "|" + a.getName().Replace("|", "") + "|" + Names.Handle(a) + "|" + G.Id(k) + "|" + Followers(a));
                    known.Add(id);
                }
            }
            if (set.Count > 300) set = new HashSet<string>(set.Skip(set.Count - 300));
            Store.Set(d, KCelebs, set);
        }

        private static void Obituaries()
        {
            SaveCustomData d = Store.World();
            if (d == null) return;
            HashSet<string> set = Store.Set(d, KCelebs);
            var keep = new HashSet<string>();
            foreach (string e in set)
            {
                string[] p = e.Split('|');
                if (p.Length < 5)
                {
                    continue;
                }
                if (G.ActorById(p[0]) != null)
                {
                    keep.Add(e);
                    continue;
                }
                Kingdom k = G.KingdomById(p[3]);
                if (k == null || !Exists(k)) continue;
                NetPost post = Official(k, "obituary", Line("net_obituary", p[1], p[2], p[4]), true);
                if (post != null)
                {
                    post.Icon = "ui/Icons/civ/net/memorial";
                    post.Tag = "#RIP" + Root(p[1]);
                }
                int n = UnityEngine.Random.Range(1, 4);
                for (int i = 0; i < n; i++)
                {
                    Actor fan = RandomUser(k);
                    if (fan == null) continue;
                    NetPost q = Person(fan, "rip", Line("net_rip", p[1]), 1f);
                    if (q != null) q.Tag = "#RIP" + Root(p[1]);
                }
            }
            Store.Set(d, KCelebs, keep);
        }

        private static void WorldRecap(List<Kingdom> online)
        {
            SaveCustomData d = Store.World();
            if (d == null || online.Count == 0) return;
            int last = Store.Int(d, KRecapYear, int.MinValue);
            if (last != int.MinValue && G.Year - last < 10) return;
            Store.Put(d, KRecapYear, G.Year);
            if (last == int.MinValue) return;
            List<NetPost> span = _feed.Where(x => x.Year > last && !x.Dm).ToList();
            if (span.Count == 0) return;
            NetPost top = span.OrderByDescending(x => x.Likes + x.Shares * 3).First();
            var tag = span.Where(x => x.Tag.Length > 0).GroupBy(x => x.Tag).OrderByDescending(g => g.Count()).FirstOrDefault();
            var star = span.Where(x => x.Who == "person").GroupBy(x => x.Author).OrderByDescending(g => g.Sum(x => x.Likes)).FirstOrDefault();
            Kingdom best = online.OrderByDescending(Reputation).First();
            string text = Line("net_recap_world", last + 1, G.Year, top.Author, G.Short(top.Likes), tag != null ? tag.Key : "-", star != null ? star.Key : "-", G.Name(best), span.Count);
            NetPost p = Make("system", "recap", Txt.F("net_recap_author", G.Year), "@retro" + G.Year, best, "world_recap", text);
            p.Important = true;
            p.Icon = "ui/Icons/civ/net/recap";
            p.Likes = span.Sum(x => x.Likes) / 20 + 10;
            p.Shares = p.Likes / 4;
            _feed.Add(p);
            Version++;
        }

        private static void Debate(Kingdom k, List<Actor> cands)
        {
            if (!Exists(k) || cands == null || cands.Count < 2) return;
            Actor a = cands[0];
            Actor b = cands[1];
            NetPost p = Official(k, "debate", Line("net_debate", a.getName(), b.getName(), G.Name(k)), true);
            if (p == null) return;
            p.Live = true;
            p.Viewers = G.Pop(k) / 2 + UnityEngine.Random.Range(20, 200);
            Actor win = G.Chance(0.5f) ? a : b;
            Actor lose = win == a ? b : a;
            Store.Put(win.data, KFollow, Followers(win) + 40 + Followers(win) / 10);
            int n = UnityEngine.Random.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                Actor fan = RandomUser(k);
                if (fan == null) continue;
                NetPost q = Person(fan, "debate_react", Line("net_debate_react", win.getName(), lose.getName()), 1f);
                if (q != null) q.Tag = "#" + Txt.T("net_debate_tag") + G.Year % 100;
            }
        }
    }
}
