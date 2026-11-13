using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Civilizations
{
    internal class SagaOpt
    {
        public Dictionary<string, JToken> Fx = new Dictionary<string, JToken>();
        public Dictionary<string, float> Ai = new Dictionary<string, float>();
        public List<string> Need = new List<string>();
    }

    internal class SagaDef
    {
        public string Id = "";
        public string Cat = "";
        public bool Good;
        public List<string> Cond = new List<string>();
        public float W = 1f;
        public int Cd = 40;
        public bool Big;
        public List<string> Bands = new List<string>();
        public List<SagaOpt> Opts = new List<SagaOpt>();
    }

    internal class SagaBand
    {
        public string Id = "";
        public int Min;
        public int Max;
    }

    internal class SagaCase
    {
        public SagaDef Def;
        public Kingdom K;
        public Kingdom Other;
        public City City;
        public string Band;
        public List<int> Options = new List<int>();
        public bool Paused;
    }

    internal static class Sagas
    {
        private const string KCd = "civ_sg_cd";
        private const string KLast = "civ_sg_last";
        private const string KMoments = "civ_moments";
        private const string KAge = "civ_age";
        private const string KAgeEra = "civ_age_era";
        public static readonly List<SagaDef> Defs = new List<SagaDef>();
        public static readonly List<SagaBand> Bands = new List<SagaBand>();
        public static readonly List<string> Lex = new List<string>();
        public static readonly Queue<SagaCase> Pending = new Queue<SagaCase>();

        public static void InitData()
        {
            Defs.Clear();
            Bands.Clear();
            Lex.Clear();
            try
            {
                string p = Path.Combine(CivMod.Folder, "Data", "sagas.json");
                if (!File.Exists(p)) return;
                JObject root = JObject.Parse(File.ReadAllText(p));
                foreach (JToken b in (JArray)root["bands"]) Bands.Add(new SagaBand { Id = (string)b["id"], Min = (int)b["min"], Max = (int)b["max"] });
                foreach (JToken l in (JArray)root["lex"]) Lex.Add((string)l);
                foreach (JToken s in (JArray)root["sagas"])
                {
                    var d = new SagaDef { Id = (string)s["id"], Cat = (string)s["cat"], Good = (bool)s["good"], W = (float)s["w"], Cd = (int)s["cd"], Big = (bool)s["big"] };
                    d.Cond = s["cond"].ToObject<List<string>>();
                    d.Bands = s["bands"].ToObject<List<string>>();
                    foreach (JToken o in (JArray)s["opts"])
                    {
                        var opt = new SagaOpt();
                        foreach (JProperty kv in ((JObject)o["fx"]).Properties()) opt.Fx[kv.Name] = kv.Value;
                        if (o["ai"] != null) opt.Ai = o["ai"].ToObject<Dictionary<string, float>>();
                        if (o["need"] != null) opt.Need = o["need"].ToObject<List<string>>();
                        d.Opts.Add(opt);
                    }
                    Defs.Add(d);
                }
            }
            catch (Exception e)
            {
                CivMod.Log("sagas data: " + e.Message);
            }
        }

        public static string BandOf(int era)
        {
            foreach (SagaBand b in Bands) if (era >= b.Min && era <= b.Max) return b.Id;
            return Bands.Count > 0 ? Bands[Bands.Count - 1].Id : "";
        }

        public static string Dress(string text, SagaCase c)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (string w in Lex)
            {
                string tag = "[" + w + "]";
                if (text.Contains(tag)) text = text.Replace(tag, Txt.T("civ_lex_" + c.Band + "_" + w));
            }
            text = text.Replace("{0}", G.Name(c.K)).Replace("{1}", c.City != null ? G.Name(c.City) : G.Name(c.K)).Replace("{2}", G.Ok(c.Other) ? G.Name(c.Other) : "");
            return text.Length > 0 ? char.ToUpper(text[0]) + text.Substring(1) : text;
        }

        public static string Plain(SagaDef d)
        {
            string t = Txt.T("civ_sg_" + d.Id + "_t");
            string band = d.Bands.Contains("all") ? "modern" : d.Bands[0];
            foreach (string w in Lex) t = t.Replace("[" + w + "]", Txt.T("civ_lex_" + band + "_" + w));
            t = t.Replace("{0}", "").Replace("{1}", "").Replace("{2}", "").Trim();
            return t.Length > 0 ? char.ToUpper(t[0]) + t.Substring(1) : t;
        }

        public static string Title(SagaCase c)
        {
            return Dress(Txt.T("civ_sg_" + c.Def.Id + "_t"), c);
        }

        public static string Body(SagaCase c)
        {
            string key = "civ_sg_" + c.Def.Id + "_" + c.Band;
            return Dress(Txt.Has(key) ? Txt.T(key) : Txt.T("civ_sg_" + c.Def.Id), c);
        }

        public static string Option(SagaCase c, int i)
        {
            return Dress(Txt.T("civ_sg_" + c.Def.Id + "_o" + (i + 1)), c);
        }

        private static bool Fits(SagaDef d, Kingdom k, KProfile p, string band, out Kingdom other)
        {
            other = null;
            if (!d.Bands.Contains("all") && !d.Bands.Contains(band)) return false;
            var ev = new EventDef { Id = "sg_" + d.Id, Cond = d.Cond };
            return Events.Cond(ev, k, p, out other);
        }

        public static void Year()
        {
            if (!Events.On || Defs.Count == 0) return;
            List<Kingdom> ks = G.Kingdoms().Where(x => G.Cities(x).Count > 0).ToList();
            if (ks.Count == 0) return;
            int rolls = Math.Max(1, Math.Min(8, ks.Count / 4 + 1));
            for (int i = 0; i < rolls; i++)
            {
                Kingdom k = ks[UnityEngine.Random.Range(0, ks.Count)];
                CivMod.Safe("saga-roll", () => Roll(k, 0.35f));
            }
            Kingdom me = Player.K;
            if (G.Ok(me)) CivMod.Safe("saga-player", () => Roll(me, 0.45f));
            foreach (Kingdom k in ks) CivMod.Safe("ages", () => AgeCheck(k));
        }

        private static void Roll(Kingdom k, float chance)
        {
            if (!G.Ok(k) || G.Year - Store.Int(k.data, KLast, -999) < 3) return;
            if (!G.Chance(chance)) return;
            KProfile p = Profile.Get(k);
            string band = BandOf(p.Era);
            Dictionary<string, float> cd = Store.Map(k.data, KCd);
            var cands = new List<SagaDef>();
            var weights = new List<float>();
            var others = new List<Kingdom>();
            foreach (SagaDef d in Defs)
            {
                if (cd.ContainsKey(d.Id) && G.Year - (int)cd[d.Id] < d.Cd) continue;
                Kingdom other;
                if (!Fits(d, k, p, band, out other)) continue;
                float w = d.W;
                if (d.Good && p.Stability > 0.6f) w *= 1.3f;
                if (!d.Good && p.Stability < 0.35f) w *= 1.3f;
                if (!d.Bands.Contains("all")) w *= 1.5f;
                cands.Add(d);
                weights.Add(w);
                others.Add(other);
            }
            if (cands.Count == 0) return;
            int i = G.Weighted(weights);
            if (i < 0) return;
            SagaDef pick = cands[i];
            cd[pick.Id] = G.Year;
            Store.Map(k.data, KCd, cd);
            Store.Put(k.data, KLast, G.Year);
            List<City> cs = G.Cities(k);
            City city = pick.Good ? k.capital ?? cs.FirstOrDefault() : cs.Count > 0 ? cs[UnityEngine.Random.Range(0, cs.Count)] : null;
            var c = new SagaCase { Def = pick, K = k, Other = others[i], City = city, Band = band };
            for (int o = 0; o < pick.Opts.Count; o++) if (pick.Opts[o].Need.All(p.Is)) c.Options.Add(o);
            if (c.Options.Count == 0) return;
            Fire(c);
        }

        public static void Fire(SagaCase c)
        {
            if (Player.Is(c.K))
            {
                Pending.Enqueue(c);
                if (c.Def.Big && CivMod.C != null && CivMod.C.PauseEvents && !Config.paused)
                {
                    Config.paused = true;
                    c.Paused = true;
                }
                WinSaga.Show();
                return;
            }
            int pick = AiPick(c);
            Resolve(c, pick, false);
        }

        public static void Force(Kingdom k, string id)
        {
            SagaDef d = Defs.FirstOrDefault(x => x.Id == id);
            if (d == null || !G.Ok(k)) return;
            Kingdom other;
            Fits(d, k, Profile.Get(k), BandOf(Research.Era(k)), out other);
            var c = new SagaCase { Def = d, K = k, Other = other, City = k.capital, Band = BandOf(Research.Era(k)) };
            for (int o = 0; o < d.Opts.Count; o++) c.Options.Add(o);
            Fire(c);
        }

        private static int AiPick(SagaCase c)
        {
            KProfile p = Profile.Get(c.K);
            var w = new List<float>();
            foreach (int o in c.Options)
            {
                SagaOpt opt = c.Def.Opts[o];
                float v = 1f;
                foreach (var kv in opt.Ai) if (p.Is(kv.Key)) v += kv.Value;
                float gold = F(opt, "gold");
                if (gold < 0f && p.Gold < -gold) v *= 0.2f;
                w.Add(v);
            }
            return c.Options[Math.Max(0, G.Weighted(w))];
        }

        private static float F(SagaOpt o, string key)
        {
            JToken t;
            return o.Fx.TryGetValue(key, out t) && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? t.Value<float>() : 0f;
        }

        public static void Resolve(SagaCase c, int option, bool player)
        {
            Kingdom k = c.K;
            if (!G.Ok(k)) return;
            SagaOpt o = c.Def.Opts[Math.Max(0, Math.Min(c.Def.Opts.Count - 1, option))];
            City city = c.City ?? k.capital;
            float stab = F(o, "stability");
            if (stab != 0f) Politics.AddStability(k, stab);
            float legit = F(o, "legit");
            if (legit != 0f) Store.Put(k.data, Legitimacy.KLegit, G.Clamp(Legitimacy.Get(k) + legit, 0f, 1f));
            float gold = F(o, "gold");
            if (gold > 0f && city != null) Economy.Give(city, "gold", (int)gold);
            else if (gold < 0f) PayGold(k, (int)-gold);
            JToken bt;
            if (o.Fx.TryGetValue("bonus", out bt)) Events.AddBonus(k, bt.ToObject<Dictionary<string, float>>(), Math.Max(1, (int)F(o, "years")));
            float kill = F(o, "kill");
            if (kill > 0f) Events.Kill(k, city, kill);
            float progress = F(o, "progress");
            if (progress > 0f)
            {
                TechDef cur = Research.Current(k);
                if (cur != null) Store.Put(k.data, Research.KProg, Research.Progress(k) + Research.Cost(k, cur) * progress);
            }
            JToken great;
            if (o.Fx.TryGetValue("great", out great)) Events.Great(k, (string)great, city);
            int explore = (int)F(o, "explore");
            if (explore > 0) Fog.Explore(k, explore);
            KProfile p = Profile.Get(k);
            if (F(o, "crisis") > 0f) Legitimacy.Crisis(k, p, Legitimacy.Get(k));
            if (G.Chance(F(o, "coup_chance"))) Events.Coup(k, p);
            if (G.Chance(F(o, "secession_chance")) && p.Cities >= 3) Legitimacy.Secession(k, p);
            if (k.hasKing() && G.Ok(k.king) && G.Chance(F(o, "kill_ruler_chance"))) k.king.getHit(k.king.getMaxHealth() * 10f + 1000f, true, AttackType.Other, null, false, false, false);
            bool war = F(o, "war") > 0f || G.Chance(F(o, "war_chance"));
            if (war && G.Ok(c.Other) && !World.world.wars.isInWarWith(k, c.Other) && !Diplomacy.Forbids(k, c.Other))
            {
                if (player) Player.DeclareWar(c.Other);
                else World.world.diplomacy.startWar(k, c.Other, WarTypeLibrary.normal);
            }
            int moment = (int)F(o, "moment");
            if (moment != 0) Moment(k, moment);
            Profile.Invalidate(k);
            Bonus.Dirty(k);
            Hub.Raise(Hub.Event, k, c.Other, city, null, "sg_" + c.Def.Id, c.Def.Cat, c.Def.Big ? 1f : 0f);
            if (c.Def.Big || player) Internet.Official(k, "event", Txt.F("civ_sg_ai_chose", G.Name(k), Title(c), Option(c, option)), c.Def.Big);
        }

        private static void PayGold(Kingdom k, int amount)
        {
            foreach (City c in G.Cities(k).OrderByDescending(x => Economy.Amount(x, "gold")))
            {
                if (amount <= 0) return;
                int take = Math.Min(amount, Economy.Amount(c, "gold"));
                if (take > 0) Economy.Take(c, "gold", take);
                amount -= take;
            }
            if (amount > 0) Politics.AddStability(k, -Math.Min(0.05f, amount / 4000f));
        }

        public static void Moment(Kingdom k, int v)
        {
            if (!G.Ok(k) || k.data == null) return;
            Store.Put(k.data, KMoments, Store.Int(k.data, KMoments, 0) + v);
        }

        public static int Moments(Kingdom k)
        {
            return Store.Int(k.data, KMoments, 0);
        }

        public static string Age(Kingdom k)
        {
            string a = Store.Str(k.data, KAge);
            return a.Length > 0 ? a : "normal";
        }

        public static void Register()
        {
            Hub.On(Hub.FirstInWorld, e => Moment(e.K, 3));
            Hub.On(Hub.GreatPerson, e =>
            {
                if (e.Arg == "born") Moment(e.K, 2);
            });
            Hub.On(Hub.Festival, e => Moment(e.K, 1));
            Hub.On(Hub.CityFounded, e => Moment(e.K, 1));
            Hub.On(Hub.CityCaptured, e =>
            {
                Moment(e.K, 1);
                Moment(e.O, -2);
            });
            Hub.On(Hub.TechLost, e => Moment(e.K, -2));
            Hub.On(Hub.CivilWar, e => Moment(e.K, -3));
            Hub.On(Hub.Coup, e => Moment(e.K, -2));
            Hub.On(Hub.Independence, e => Moment(e.O, -2));
        }

        private static void AgeCheck(Kingdom k)
        {
            if (!G.Ok(k) || k.data == null) return;
            int era = Research.Era(k);
            int seen = Store.Int(k.data, KAgeEra, -1);
            if (seen < 0)
            {
                Store.Put(k.data, KAgeEra, era);
                return;
            }
            if (era <= seen) return;
            Store.Put(k.data, KAgeEra, era);
            int score = Moments(k);
            Store.Put(k.data, KMoments, 0);
            string old = Age(k);
            string age = score >= 8 ? (old == "dark" ? "heroic" : "golden") : score <= -3 ? "dark" : "normal";
            Store.Put(k.data, KAge, age);
            Dictionary<string, float> fx = null;
            switch (age)
            {
                case "golden": fx = new Dictionary<string, float> { { "stability", 10f }, { "culture", 10f }, { "prod", 5f }, { "loyalty", 5f } }; break;
                case "heroic": fx = new Dictionary<string, float> { { "stability", 15f }, { "culture", 15f }, { "prod", 10f }, { "research", 8f } }; break;
                case "dark": fx = new Dictionary<string, float> { { "stability", -10f }, { "loyalty", -6f }, { "culture", -5f } }; break;
            }
            if (fx == null) return;
            Events.AddBonus(k, fx, 25);
            Chronicle.Log("age_" + age, k, G.Name(k));
            Internet.Official(k, "event", Txt.F("civ_age_news_" + age, G.Name(k)), age != "dark");
        }
    }
}
