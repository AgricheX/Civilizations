using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal class Opinion
    {
        public string Gov = "";
        public string Ideo = "";
        public string Econ = "";
        public float Xeno;
        public float Sat = 0.6f;

        public string Save()
        {
            return Gov + "|" + Ideo + "|" + Econ + "|" + Xeno.ToString("0.##", CultureInfo.InvariantCulture) + "|" + Sat.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static Opinion Load(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            string[] p = s.Split('|');
            if (p.Length < 5) return null;
            var o = new Opinion { Gov = p[0], Ideo = p[1], Econ = p[2] };
            float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out o.Xeno);
            float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out o.Sat);
            return o;
        }
    }

    internal static class Politics
    {
        public const string KStage = "civ_stage";
        public const string KStab = "civ_stab";
        public const string KKing = "civ_king";
        public const string KOp = "civ_op";
        public const string KSup = "civ_sup_";
        public const string KAlt = "civ_alt_";
        public const string KElect = "civ_elect";
        public const string KXenoAvg = "civ_xavg";
        public static readonly string[] Stages = { "tribe", "village", "town", "city_state", "kingdom", "empire", "nation" };
        private static readonly string[] Axes = { "gov", "ideo", "econ" };

        public static string Stage(Kingdom k)
        {
            return k == null || k.data == null ? Stages[0] : Store.Str(k.data, KStage, Stages[0]);
        }

        public static int StageIndex(Kingdom k)
        {
            return Math.Max(0, Array.IndexOf(Stages, Stage(k)));
        }

        public static float Stability(Kingdom k)
        {
            return k == null || k.data == null ? 0.6f : Store.Flt(k.data, KStab, 0.6f);
        }

        public static void AddStability(Kingdom k, float d)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KStab, G.Clamp(Stability(k) + d, 0.02f, 0.98f));
        }

        public static float Support(Kingdom k, string axis)
        {
            return k == null || k.data == null ? 0.6f : Store.Flt(k.data, KSup + axis, 0.6f);
        }

        public static string Alternative(Kingdom k, string axis)
        {
            return k == null || k.data == null ? "" : Store.Str(k.data, KAlt + axis);
        }

        public static string Focus(Kingdom k)
        {
            string gov = Policies.Id(k, "gov");
            string ideo = Policies.Id(k, "ideo");
            if (ideo == "militarism" || gov == "military_junta") return "mil";
            if (gov == "technocracy" || ideo == "technoprogressivism" || ideo == "humanism") return "sci";
            if (gov == "merchant_republic" || gov == "corporatocracy") return "econ";
            if (ideo == "environmentalism") return "med";
            if (gov == "theocracy" || ideo == "religious_zeal") return "soc";
            if (gov == "single_party_state") return "ind";
            return "";
        }

        public static float Aggression(Kingdom k)
        {
            float a = 0f;
            foreach (PolicyDef p in Policies.All(k))
            {
                a += Store.Get(p.Eff, "attack") * 0.01f;
            }
            if (Policies.Id(k, "ideo") == "militarism") a += 0.15f;
            if (Policies.Id(k, "ideo") == "nationalism") a += 0.05f;
            return a;
        }

        public static void Year(Kingdom k, int yrs)
        {
            Policies.Ensure(k);
            UpdateStage(k);
            Opinions(k, yrs);
            Elections(k, yrs);
            Drift(k, yrs);
            UpdateStability(k);
        }

        private static void UpdateStage(Kingdom k)
        {
            int era = Research.Era(k);
            int cities = k.countCities();
            int s = 0;
            if (Research.HasUnlock(k, "stage:village")) s = 1;
            if (Research.HasUnlock(k, "stage:town") && G.Pop(k) >= 20) s = 2;
            if (Research.HasUnlock(k, "stage:kingdom")) s = cities >= 2 ? 4 : 3;
            if (Research.HasUnlock(k, "stage:empire") && cities >= Cities.EmpireCities) s = 5;
            if (era >= 9 && (Research.Has(k, "nationalism") || Research.Has(k, "constitutionalism"))) s = Math.Max(s, 6);
            string ns = Stages[s];
            string old = Stage(k);
            if (old != ns)
            {
                Store.Put(k.data, KStage, ns);
                Internet.OnStage(k, ns);
            }
        }

        public static Opinion Get(Actor a)
        {
            if (!G.Ok(a) || a.kingdom == null || !a.kingdom.isCiv() || !a.isSapient()) return null;
            Opinion o = Opinion.Load(Store.Str(a.data, KOp));
            if (o != null) return o;
            o = Seed(a);
            Store.Put(a.data, KOp, o.Save());
            return o;
        }

        private static Opinion Seed(Actor a)
        {
            Kingdom k = a.kingdom;
            var o = new Opinion
            {
                Gov = Policies.Id(k, "gov"),
                Ideo = Policies.Id(k, "ideo"),
                Econ = Policies.Id(k, "econ"),
                Sat = 0.6f
            };
            string x = Policies.Id(k, "xeno");
            o.Xeno = x == "xenophobia" ? -0.6f : x == "xenophilia" ? 0.6f : 0f;
            if (a.hasCulture())
            {
                if (a.culture.hasTrait("xenophobic")) o.Xeno -= 0.3f;
                if (a.culture.hasTrait("xenophiles")) o.Xeno += 0.3f;
            }
            o.Xeno = G.Clamp(o.Xeno + UnityEngine.Random.Range(-0.25f, 0.25f), -1f, 1f);
            if (G.Chance(0.25f)) o.Gov = PickAlt(k, "gov", a, o);
            if (G.Chance(0.25f)) o.Ideo = PickAlt(k, "ideo", a, o);
            if (G.Chance(0.2f)) o.Econ = PickAlt(k, "econ", a, o);
            return o;
        }

        private static string PickAlt(Kingdom k, string axis, Actor a, Opinion o)
        {
            List<PolicyDef> opts = Policies.Options(k, axis);
            if (opts.Count == 0) return Policies.Id(k, axis);
            var w = new List<float>();
            foreach (PolicyDef p in opts) w.Add(Appeal(a, o, p));
            return opts[G.Weighted(w)].Id;
        }

        private static bool T(Actor a, string trait)
        {
            try
            {
                return a.hasTrait(trait);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static float Appeal(Actor a, Opinion o, PolicyDef p)
        {
            float w = 1f;
            bool poor = a.money < 10;
            bool rich = a.money > 120;
            bool warrior = a.isWarrior();
            bool unhappy = o.Sat < 0.4f;
            Kingdom k = a.kingdom;
            bool war = k != null && World.world.wars.hasWars(k);
            string id = p.Id;
            switch (id)
            {
                case "democracy":
                case "republic":
                case "parliamentary_republic":
                case "presidential_republic":
                    w += unhappy ? 1.5f : 0.4f;
                    if (rich) w += 0.4f;
                    break;
                case "single_party_state":
                case "communism":
                case "socialism":
                case "planned":
                case "welfare":
                    if (poor) w += 1.5f;
                    if (rich) w -= 0.6f;
                    break;
                case "military_junta":
                case "militarism":
                case "authoritarianism":
                case "nationalism":
                    if (warrior) w += 1.4f;
                    if (war) w += 0.8f;
                    break;
                case "laissez_faire":
                case "liberalism":
                case "corporatocracy":
                case "merchant_republic":
                case "mercantilism":
                    if (rich) w += 1.5f;
                    if (Roles.Of(a) == Roles.Merchant) w += 1f;
                    break;
                case "theocracy":
                case "religious_zeal":
                    if (a.hasReligion()) w += 0.8f;
                    break;
                case "technocracy":
                case "technoprogressivism":
                case "humanism":
                    if (Roles.Of(a) == Roles.Scientist) w += 1.5f;
                    break;
                case "environmentalism":
                    if (Pollution.Level(a.city) > 0.4f) w += 1.5f;
                    break;
                case "anarchism":
                    if (unhappy) w += 0.6f;
                    break;
                case "traditionalism":
                case "conservatism":
                    if (a.getAge() > 40) w += 1f;
                    break;
            }
            if (T(a, "greedy") && (id == "laissez_faire" || id == "corporatocracy")) w += 1f;
            if (T(a, "kind") && (id == "welfare" || id == "socialism")) w += 0.8f;
            if (T(a, "ambitious") && (id == "military_junta" || id == "absolute_monarchy")) w += 0.6f;
            if (T(a, "bloodlust") && id == "militarism") w += 1f;
            w += Internet.TrendPull(k, id);
            w += Clans.PolicyPull(a.clan, id);
            return Math.Max(0.05f, w);
        }

        private static void Opinions(Kingdom k, int yrs)
        {
            var tally = new Dictionary<string, Dictionary<string, float>>();
            foreach (string ax in Axes) tally[ax] = new Dictionary<string, float>();
            float xeno = 0f;
            int n = 0;
            float sat = 0f;
            foreach (City c in G.Cities(k))
            {
                int budget = 14;
                bool famine = Economy.IsFamine(c);
                float poll = Pollution.Level(c);
                foreach (Actor a in c.units)
                {
                    if (budget <= 0) break;
                    if (!G.Ok(a) || !a.isAdult() || !a.isSapient()) continue;
                    if (!G.Chance(0.5f)) continue;
                    budget--;
                    Opinion o = Get(a);
                    if (o == null) continue;
                    float target = 0.55f;
                    if (a.isHappy()) target += 0.2f;
                    if (famine) target -= 0.3f;
                    target -= poll * 0.2f;
                    if (World.world.wars.hasWars(k)) target -= 0.1f;
                    string st = Xeno.Status(a);
                    if (st == Xeno.Slave) target -= 0.4f;
                    else if (st == Xeno.Servant) target -= 0.15f;
                    target += (Stability(k) - 0.5f) * 0.3f;
                    o.Sat += (G.Clamp(target, 0f, 1f) - o.Sat) * 0.3f;
                    foreach (string ax in Axes)
                    {
                        string cur = Policies.Id(k, ax);
                        string mine = ax == "gov" ? o.Gov : ax == "ideo" ? o.Ideo : o.Econ;
                        if (o.Sat > 0.65f && G.Chance(0.12f * yrs)) mine = cur;
                        else if (G.Chance((o.Sat < 0.4f ? 0.15f : 0.04f) * yrs)) mine = PickAlt(k, ax, a, o);
                        PolicyDef pd = Db.Policy(mine);
                        if (pd == null || !Policies.Unlocked(k, pd)) mine = cur;
                        if (ax == "gov") o.Gov = mine;
                        else if (ax == "ideo") o.Ideo = mine;
                        else o.Econ = mine;
                        Store.Add(tally[ax], mine, 1f);
                    }
                    string kx = Policies.Id(k, "xeno");
                    float pull = kx == "xenophobia" ? -0.5f : kx == "xenophilia" ? 0.5f : 0f;
                    if (Xeno.ForeignShare(c) > 0.2f) pull += 0.2f;
                    o.Xeno = G.Clamp(o.Xeno + (pull - o.Xeno) * 0.05f + UnityEngine.Random.Range(-0.05f, 0.05f), -1f, 1f);
                    xeno += o.Xeno;
                    sat += o.Sat;
                    n++;
                    Store.Put(a.data, KOp, o.Save());
                }
            }
            if (n == 0) return;
            foreach (string ax in Axes)
            {
                string cur = Policies.Id(k, ax);
                float total = 0f;
                foreach (float v in tally[ax].Values) total += v;
                float support = total > 0f ? Store.Get(tally[ax], cur) / total : 0.6f;
                string alt = "";
                float best = 0f;
                foreach (var kv in tally[ax])
                {
                    if (kv.Key == cur) continue;
                    if (kv.Value > best)
                    {
                        best = kv.Value;
                        alt = kv.Key;
                    }
                }
                Store.Put(k.data, KSup + ax, support);
                Store.Put(k.data, KAlt + ax, alt);
            }
            Store.Put(k.data, KXenoAvg, xeno / n);
            Store.Put(k.data, "civ_sat", sat / n);
        }

        public static float Satisfaction(Kingdom k)
        {
            return k == null || k.data == null ? 0.6f : Store.Flt(k.data, "civ_sat", 0.6f);
        }

        public static float XenoMood(Kingdom k)
        {
            return k == null || k.data == null ? 0f : Store.Flt(k.data, KXenoAvg, 0f);
        }

        private static void Elections(Kingdom k, int yrs)
        {
            PolicyDef gov = Policies.Get(k, "gov");
            if (gov == null || !gov.Elections) return;
            int next = Store.Int(k.data, KElect, 0);
            if (next == 0)
            {
                Store.Put(k.data, KElect, G.Year + G.Range(4, 9));
                return;
            }
            if (G.Year == next - 1) Internet.OnCampaign(k, Candidates(k));
            if (G.Year < next) return;
            Store.Put(k.data, KElect, G.Year + (gov.Id == "tribal_council" ? 10 : G.Range(4, 7)));
            List<Actor> cands = Candidates(k);
            if (cands.Count == 0) return;
            var votes = new float[cands.Count];
            var ops = new Opinion[cands.Count];
            for (int i = 0; i < cands.Count; i++) ops[i] = Get(cands[i]) ?? new Opinion();
            int voters = 0;
            foreach (City c in G.Cities(k))
            {
                int budget = 30;
                foreach (Actor a in c.units)
                {
                    if (budget-- <= 0) break;
                    if (!G.Ok(a) || !a.isAdult()) continue;
                    if (!Xeno.CanVote(a)) continue;
                    Opinion o = Get(a);
                    if (o == null) continue;
                    voters++;
                    var w = new float[cands.Count];
                    for (int i = 0; i < cands.Count; i++)
                    {
                        float s = 0.2f;
                        if (ops[i].Gov == o.Gov) s += 1f;
                        if (ops[i].Ideo == o.Ideo) s += 1.4f;
                        if (ops[i].Econ == o.Econ) s += 1f;
                        s += 1f - Math.Abs(ops[i].Xeno - o.Xeno);
                        if (a.clan != null && a.clan == cands[i].clan) s += 1.5f;
                        if (a.city == cands[i].city) s += 0.4f;
                        if (cands[i].isKing()) s += o.Sat > 0.6f ? 1f : -0.6f;
                        w[i] = s;
                    }
                    votes[G.Weighted(w)] += 1f;
                }
            }
            int win = 0;
            for (int i = 1; i < votes.Length; i++) if (votes[i] > votes[win]) win = i;
            Actor winner = cands[win];
            Internet.OnElection(k, cands, votes, win, voters);
            if (!winner.isKing())
            {
                Actor old = k.king;
                if (G.Ok(old)) k.kingLeftEvent();
                else k.removeKing();
                if (winner.city != null && winner.isCityLeader()) winner.city.removeLeader();
                k.setKing(winner);
            }
            Opinion wo = ops[win];
            if (!string.IsNullOrEmpty(wo.Ideo) && wo.Ideo != Policies.Id(k, "ideo") && Policies.Unlocked(k, Db.Policy(wo.Ideo))) Policies.Set(k, "ideo", wo.Ideo, "election");
            if (!string.IsNullOrEmpty(wo.Econ) && wo.Econ != Policies.Id(k, "econ") && Policies.Unlocked(k, Db.Policy(wo.Econ))) Policies.Set(k, "econ", wo.Econ, "election");
            AddStability(k, 0.05f);
        }

        public static List<Actor> Candidates(Kingdom k)
        {
            var list = new List<Actor>();
            if (k.hasKing() && G.Ok(k.king)) list.Add(k.king);
            var seen = new HashSet<Actor>(list);
            var scored = new List<KeyValuePair<Actor, float>>();
            foreach (City c in G.Cities(k))
            {
                if (G.Ok(c.leader) && !seen.Contains(c.leader) && Xeno.CanHoldOffice(c.leader)) scored.Add(new KeyValuePair<Actor, float>(c.leader, 2f + c.getPopulationPeople() * 0.02f));
            }
            foreach (Clan cl in Clans.Of(k))
            {
                Actor chief = cl.getChief();
                if (G.Ok(chief) && chief.kingdom == k && chief.isAdult() && !seen.Contains(chief) && Xeno.CanHoldOffice(chief)) scored.Add(new KeyValuePair<Actor, float>(chief, Clans.Influence(cl) * 0.05f));
            }
            scored.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in scored)
            {
                if (list.Count >= 3) break;
                if (seen.Add(kv.Key)) list.Add(kv.Key);
            }
            return list;
        }

        private static void Drift(Kingdom k, int yrs)
        {
            string kingId = k.hasKing() ? k.king.data.id.ToString() : "";
            bool newKing = kingId != Store.Str(k.data, KKing);
            Store.Put(k.data, KKing, kingId);
            float stab = Stability(k);
            foreach (string ax in Axes)
            {
                PolicyDef cur = Policies.Get(k, ax);
                if (!Policies.Unlocked(k, cur))
                {
                    PolicyDef best = BestOption(k, ax);
                    if (best != null) Policies.Set(k, ax, best.Id, "forced");
                    continue;
                }
                float sup = Support(k, ax);
                string alt = Alternative(k, ax);
                PolicyDef altP = Db.Policy(alt);
                if (altP == null || !Policies.Unlocked(k, altP)) continue;
                bool elected = ax == "gov" && cur.Elections;
                if (sup < 0.3f && stab < 0.3f && G.Chance(0.12f * yrs))
                {
                    Revolution(k, ax, altP);
                }
                else if (newKing && !elected && G.Chance(0.2f))
                {
                    Policies.Set(k, ax, KingChoice(k, ax) ?? altP.Id, "new_ruler");
                }
                else if (!elected && sup < 0.45f && G.Chance(0.03f * yrs))
                {
                    Policies.Set(k, ax, altP.Id, "reform");
                }
            }
            float xm = XenoMood(k);
            string xs = Policies.Id(k, "xeno");
            if (xs != "xenophobia" && xm < -0.45f && G.Chance(0.05f * yrs)) Policies.Set(k, "xeno", "xenophobia", "mood");
            else if (xs != "xenophilia" && xm > 0.45f && G.Chance(0.05f * yrs)) Policies.Set(k, "xeno", "xenophilia", "mood");
            else if (xs != "xeno_neutral" && Math.Abs(xm) < 0.15f && G.Chance(0.03f * yrs)) Policies.Set(k, "xeno", "xeno_neutral", "mood");
        }

        private static string KingChoice(Kingdom k, string axis)
        {
            if (!k.hasKing()) return null;
            Opinion o = Get(k.king);
            if (o == null) return null;
            string id = axis == "gov" ? o.Gov : axis == "ideo" ? o.Ideo : o.Econ;
            PolicyDef p = Db.Policy(id);
            return p != null && Policies.Unlocked(k, p) ? id : null;
        }

        private static PolicyDef BestOption(Kingdom k, string axis)
        {
            List<PolicyDef> opts = Policies.Options(k, axis);
            if (opts.Count == 0) return null;
            return opts[opts.Count - 1];
        }

        private static void Revolution(Kingdom k, string axis, PolicyDef to)
        {
            PolicyDef from = Policies.Get(k, axis);
            Policies.Set(k, axis, to.Id, "revolution");
            if (axis == "gov")
            {
                Actor leader = null;
                float best = -1f;
                foreach (City c in G.Cities(k))
                {
                    foreach (Actor a in c.units)
                    {
                        if (!G.Ok(a) || !a.isAdult() || a.isKing() || !Xeno.CanHoldOffice(a)) continue;
                        Opinion o = Get(a);
                        if (o == null || o.Gov != to.Id) continue;
                        float s = a.renown + (a.clan != null ? Clans.Influence(a.clan) : 0f) + (a.isCityLeader() ? 20f : 0f);
                        if (s > best)
                        {
                            best = s;
                            leader = a;
                        }
                    }
                }
                if (leader != null)
                {
                    Actor old = k.king;
                    if (G.Ok(old)) k.kingLeftEvent();
                    else k.removeKing();
                    if (leader.city != null && leader.isCityLeader()) leader.city.removeLeader();
                    k.setKing(leader);
                }
            }
            AddStability(k, -0.12f);
            Internet.OnRevolution(k, axis, from != null ? from.Id : "", to.Id);
        }

        public static void ConsiderNewPolicy(Kingdom k, string id)
        {
            PolicyDef p = Db.Policy(id);
            if (p == null || !Policies.Unlocked(k, p)) return;
            PolicyDef cur = Policies.Get(k, p.Group);
            if (cur != null && cur.Id == id) return;
            if (p.Group == "gov" && cur != null && cur.Elections && !p.Elections && Stability(k) > 0.4f) return;
            float chance = 0.25f;
            if (p.Group == "gov" && cur != null && (cur.Id == "tribal_band" || cur.Id == "chiefdom" || cur.Id == "tribal_council")) chance = 0.6f;
            if (G.Chance(chance)) Policies.Set(k, p.Group, id, "reform");
        }

        public static void ChooseTransport(Kingdom k)
        {
            float rail = 1f, road = 1f;
            string econ = Policies.Id(k, "econ");
            string gov = Policies.Id(k, "gov");
            if (econ == "planned" || gov == "single_party_state") rail += 1.5f;
            if (econ == "laissez_faire" || econ == "corporatist") road += 1f;
            if (Policies.Id(k, "ideo") == "environmentalism") rail += 1f;
            int cities = k.countCities();
            if (cities >= 4) rail += 0.6f;
            int mountains = 0, total = 0;
            foreach (City c in G.Cities(k))
            {
                WorldTile t = G.Tile(c);
                if (t == null) continue;
                total++;
                if (t.Type != null && (t.Type.mountains || t.Type.id.Contains("hills"))) mountains++;
            }
            if (total > 0 && mountains * 2 > total) road += 1.2f;
            if (Research.Route(k) == "diesel" || Research.Route(k) == "real") road += 0.5f;
            if (Research.Route(k) == "steam" || Research.Route(k) == "atom") rail += 0.8f;
            float r = UnityEngine.Random.value * (rail + road + 0.6f);
            string pick = r < rail ? "rail_first" : r < rail + road ? "road_first" : "balanced_transport";
            Policies.Set(k, "transport", pick, "init");
        }

        private static void UpdateStability(Kingdom k)
        {
            float target = 0.5f + Bonus.Sum(k, "stability") * 0.004f;
            target = Math.Min(target, 0.85f);
            List<City> cities = G.Cities(k);
            if (cities.Count > 0)
            {
                int happy = 0, famine = 0;
                float poll = 0f;
                int servants = 0;
                foreach (City c in cities)
                {
                    if (c.isHappy()) happy++;
                    if (Economy.IsFamine(c)) famine++;
                    poll += Pollution.Level(c);
                    servants += Roles.Count(c, Roles.Servant);
                }
                target += 0.2f * ((float)happy / cities.Count - 0.5f);
                target -= 0.25f * famine / cities.Count;
                target -= 0.15f * Math.Min(1f, poll / cities.Count);
                target += Math.Min(0.1f, servants * 0.01f);
            }
            target += (Support(k, "gov") - 0.5f) * 0.25f;
            target += (Satisfaction(k) - 0.55f) * 0.3f;
            if (World.world.wars.hasWars(k)) target -= 0.08f;
            target -= Clans.Tension(k);
            target += Internet.Reputation(k) * 0.05f;
            target -= Xeno.Unrest(k);
            target = G.Clamp(target, 0.02f, 0.98f);
            float cur = Stability(k);
            cur += (target - cur) * 0.25f;
            Store.Put(k.data, KStab, cur);
        }

        public static void InitAssets()
        {
            if (AssetManager.loyalty_library.get("civ_government") != null) return;
            AssetManager.loyalty_library.add(new LoyaltyAsset
            {
                id = "civ_government",
                translation_key = "loyalty_civ_government",
                translation_key_negative = "loyalty_civ_government_bad",
                calc = delegate(City c)
                {
                    if (c == null || c.kingdom == null || !c.kingdom.isCiv()) return 0;
                    return (int)((Stability(c.kingdom) - 0.5f) * 40f + Bonus.Sum(c.kingdom, "loyalty") * 0.2f);
                }
            });
            AssetManager.loyalty_library.add(new LoyaltyAsset
            {
                id = "civ_famine",
                translation_key = "loyalty_civ_famine",
                calc = delegate(City c) { return c != null && Economy.IsFamine(c) ? -15 : 0; }
            });
            AssetManager.loyalty_library.add(new LoyaltyAsset
            {
                id = "civ_servants",
                translation_key = "loyalty_civ_servants",
                calc = delegate(City c) { return c == null ? 0 : Math.Min(15, Roles.Count(c, Roles.Servant) * 3); }
            });
            AssetManager.loyalty_library.add(new LoyaltyAsset
            {
                id = "civ_clans",
                translation_key = "loyalty_civ_clans",
                calc = delegate(City c) { return c == null || c.kingdom == null ? 0 : -(int)(Clans.Tension(c.kingdom) * 60f); }
            });
            AssetManager.loyalty_library.add(new LoyaltyAsset
            {
                id = "civ_pollution",
                translation_key = "loyalty_civ_pollution",
                calc = delegate(City c) { return c == null ? 0 : -(int)(Pollution.Level(c) * 12f); }
            });
            AssetManager.loyalty_library.add(new LoyaltyAsset
            {
                id = "civ_slavery",
                translation_key = "loyalty_civ_slavery",
                calc = delegate(City c) { return c == null ? 0 : -(int)(Xeno.SlaveShare(c) * 40f); }
            });
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_ideology",
                translation_key = "opinion_civ_ideology",
                translation_key_negative = "opinion_civ_ideology_bad",
                calc = delegate(Kingdom a, Kingdom b)
                {
                    if (a == null || b == null || !a.isCiv() || !b.isCiv()) return 0;
                    int v = 0;
                    string ia = Policies.Id(a, "ideo"), ib = Policies.Id(b, "ideo");
                    if (ia == ib) v += 15;
                    else if (Hostile(ia, ib)) v -= 20;
                    if (Policies.Id(a, "gov") == Policies.Id(b, "gov")) v += 5;
                    return v;
                }
            });
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_xeno",
                translation_key = "opinion_civ_xeno",
                translation_key_negative = "opinion_civ_xeno_bad",
                calc = delegate(Kingdom a, Kingdom b)
                {
                    if (a == null || b == null || !a.isCiv() || !b.isCiv()) return 0;
                    if (a.getSpecies() == b.getSpecies()) return 0;
                    string x = Policies.Id(a, "xeno");
                    return x == "xenophobia" ? -25 : x == "xenophilia" ? 15 : 0;
                }
            });
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_reputation",
                translation_key = "opinion_civ_reputation",
                translation_key_negative = "opinion_civ_reputation_bad",
                calc = delegate(Kingdom a, Kingdom b) { return b == null ? 0 : (int)(Internet.Reputation(b) * 20f + Bonus.Sum(b, "prestige") * 0.1f); }
            });
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_ambassadors",
                translation_key = "opinion_civ_ambassadors",
                calc = delegate(Kingdom a, Kingdom b) { return Roles.AmbassadorBonus(b, a); }
            });
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_trade",
                translation_key = "opinion_civ_trade",
                calc = delegate(Kingdom a, Kingdom b) { return Economy.TradeOpinion(a, b); }
            });
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_bragging",
                translation_key = "opinion_civ_bragging",
                calc = delegate(Kingdom a, Kingdom b) { return b == null || b.data == null || a == b ? 0 : -(int)Math.Min(30f, Store.Flt(b.data, Internet.KBrag, 0f) * 3f); }
            });
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_nuclear",
                translation_key = "opinion_civ_nuclear",
                calc = delegate(Kingdom a, Kingdom b) { return b != null && War.UsedNukes(b) ? -120 : 0; }
            });
        }

        private static bool Hostile(string a, string b)
        {
            string[,] pairs =
            {
                { "communism", "liberalism" }, { "communism", "conservatism" }, { "communism", "authoritarianism" },
                { "socialism", "authoritarianism" }, { "liberalism", "authoritarianism" }, { "anarchism", "authoritarianism" },
                { "religious_zeal", "humanism" }, { "nationalism", "liberalism" }, { "environmentalism", "militarism" },
                { "traditionalism", "technoprogressivism" }
            };
            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                if ((pairs[i, 0] == a && pairs[i, 1] == b) || (pairs[i, 0] == b && pairs[i, 1] == a)) return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.diceAgressionSuccess))]
        private static class Aggressive
        {
            private static void Postfix(Kingdom __instance, ref bool __result)
            {
                if (__instance == null || __instance.data == null || !__instance.isCiv()) return;
                float a = Aggression(__instance);
                if (__result && a < 0f && G.Chance(-a * 2f)) __result = false;
                else if (!__result && a > 0f && __instance.hasKing() && G.Chance(a * 0.5f)) __result = true;
            }
        }

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getTaxRateLocal))]
        private static class TaxLocal
        {
            private static void Postfix(Kingdom __instance, ref float __result)
            {
                if (__instance == null || __instance.data == null || !__instance.isCiv()) return;
                __result = G.Clamp(__result * Bonus.Mult(__instance, "gold"), 0f, 0.6f);
            }
        }
    }
}
