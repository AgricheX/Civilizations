using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Statecraft
    {
        private const string KScore = "civ_ws";
        private const string KStart = "civ_ws_start";
        private const string KExh = "civ_exh";
        private const string KAe = "civ_ae";
        private const string KCoal = "civ_coal_y";
        private const string KFail = "civ_vote_fail";
        private const string KBlock = "civ_vote_block";
        private const string KLastVote = "civ_vote_last";
        private const string KDyn = "civ_dyn";
        private const string KDynY = "civ_dyn_y";
        private const string KDynN = "civ_dyn_n";
        private const string KHumil = "civ_humil";
        private const string KHumilY = "civ_humil_y";

        public static readonly string[] Phases = { "renewal", "peak", "decline", "collapse" };
        private static readonly int[] PhaseYears = { 25, 35, 30, 4 };
        public static bool Escalating;

        private class Crisis
        {
            public long A, B;
            public int Start, Ends;
        }

        private static readonly List<Crisis> _crises = new List<Crisis>();

        public static void Clear()
        {
            _crises.Clear();
            Ledger.Clear();
        }

        public static void Register()
        {
            Hub.On(Hub.WarStarted, e =>
            {
                global::War w = Find(e.K, e.O);
                if (w != null) Store.Put(w.data, KStart, G.Year);
            });
            Hub.On(Hub.CityCaptured, e =>
            {
                if (!G.Ok(e.K) || !G.Ok(e.O) || e.Id != "captured") return;
                global::War w = Find(e.K, e.O);
                bool cap = e.C != null && e.O.capital == e.C;
                if (w != null)
                {
                    float d = cap ? 25f : 12f;
                    AddScore(w, w.isAttacker(e.K) ? d : -d);
                }
                AddExh(e.O, cap ? 10f : 4f);
                AddAe(e.K, 3f + Math.Min(4f, Profile.Get(e.K).Cities * 0.2f));
            });
            Hub.On(Hub.PeaceTerms, e =>
            {
                if (!G.Ok(e.K) || !G.Ok(e.O) || e.Id == "white_peace") return;
                if (e.Id.Contains("annexation")) AddAe(e.K, 12f);
                else if (e.Id.Contains("puppet") || e.Id.Contains("vassal") || e.Id.Contains("colony") || e.Id.Contains("protectorate")) AddAe(e.K, 6f);
                Store.Put(e.O.data, KHumil, e.K.data.id.ToString());
                Store.Put(e.O.data, KHumilY, G.Year);
                Legitimacy.Shock(e.O, -0.05f);
            });
        }

        public static global::War Find(Kingdom a, Kingdom b)
        {
            if (!G.Ok(a) || !G.Ok(b)) return null;
            foreach (global::War w in World.world.wars.getWars(a))
            {
                if (w == null || w.data == null || w.hasEnded()) continue;
                if (w.isAttacker(a) && w.isDefender(b) || w.isDefender(a) && w.isAttacker(b)) return w;
            }
            return null;
        }

        private static List<global::War> Wars()
        {
            var seen = new HashSet<long>();
            var l = new List<global::War>();
            foreach (Kingdom k in G.Kingdoms())
            {
                foreach (global::War w in World.world.wars.getWars(k))
                {
                    if (w == null || w.data == null || w.hasEnded() || !w.isAlive()) continue;
                    if (seen.Add(w.data.id)) l.Add(w);
                }
            }
            return l;
        }

        public static float Score(global::War w)
        {
            return w == null || w.data == null ? 0f : Store.Flt(w.data, KScore, 0f);
        }

        public static float Score(Kingdom k, global::War w)
        {
            float s = Score(w);
            return w != null && w.isDefender(k) ? -s : s;
        }

        private static void AddScore(global::War w, float d)
        {
            Store.Put(w.data, KScore, G.Clamp(Score(w) + d, -100f, 100f));
        }

        public static int WarAge(global::War w)
        {
            int s = Store.Int(w.data, KStart, int.MinValue);
            if (s == int.MinValue)
            {
                s = G.Year;
                Store.Put(w.data, KStart, s);
            }
            return G.Year - s;
        }

        public static float Exhaustion(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KExh, 0f) : 0f;
        }

        private static void AddExh(Kingdom k, float d)
        {
            if (!G.Ok(k)) return;
            float o = Exhaustion(k), n = G.Clamp(o + d, 0f, 100f);
            Store.Put(k.data, KExh, n);
            if ((int)(o / 10f) != (int)(n / 10f)) Bonus.Dirty(k);
        }

        public static float Aggressive(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KAe, 0f) : 0f;
        }

        private static void AddAe(Kingdom k, float d)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KAe, G.Clamp(Aggressive(k) + d, 0f, 100f));
        }

        public static float PeaceBonus(Kingdom k, Kingdom o)
        {
            global::War w = Find(k, o);
            if (w == null) return 0f;
            return Score(k, w) / 200f + Exhaustion(o) / 250f;
        }

        public static void Year()
        {
            Ledger.Roll();
            foreach (global::War w in Wars()) CivMod.Safe("warscore", () => WarYear(w));
            CivMod.Safe("crises", Crises);
            CivMod.Safe("coalitions", Coalitions);
        }

        private static void WarYear(global::War w)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d)) return;
            float da = w.getDeadAttackers(), dd = w.getDeadDefenders();
            AddScore(w, G.Clamp((dd - da) / (dd + da + 20f) * 12f, -10f, 10f));
            int age = WarAge(w);
            float s = Score(w);
            foreach (Kingdom k in w.getAttackers()) AddExh(k, 3f + Math.Max(0f, -s) / 25f);
            foreach (Kingdom k in w.getDefenders()) AddExh(k, 3f + Math.Max(0f, s) / 25f);
            if (age < 3) return;
            if (Player.Locks(a, "war") || Player.Locks(d, "war")) return;
            float p = 0.05f + Math.Abs(s) / 250f + (Exhaustion(a) + Exhaustion(d)) / 400f + age * 0.01f;
            if (!G.Chance(Math.Min(0.85f, p))) return;
            WarWinner win = s >= 25f ? WarWinner.Attackers : s <= -25f ? WarWinner.Defenders : WarWinner.Peace;
            World.world.wars.endWar(w, win);
            Diplomacy.Sign(a, d, "truce", 5 + (int)(Math.Abs(s) / 10f));
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!G.Ok(k)) return;
            KProfile p = Profile.Get(k);
            if (!p.AtWar) AddExh(k, -6f * yrs);
            float ex = Exhaustion(k);
            if (ex > 40f) Politics.AddStability(k, -(ex - 40f) * 0.0004f * yrs);
            float ae = Aggressive(k);
            if (ae > 0f) Store.Put(k.data, KAe, Math.Max(0f, ae - 2f * yrs));
            Dynasty(k, p, yrs);
            Revanche(k, p, yrs);
        }

        private static void Coalitions()
        {
            foreach (Kingdom k in G.Kingdoms().ToList())
            {
                if (!G.Ok(k) || !k.isCiv() || Aggressive(k) < 30f) continue;
                if (G.Year - Store.Int(k.data, KCoal, -999) < 15) continue;
                Form(k);
            }
        }

        public static bool Form(Kingdom k)
        {
            Kingdom lord = Diplomacy.Overlord(k);
            var deps = Diplomacy.DependentsOf(k);
            var members = G.Kingdoms().Where(o => G.Ok(o) && o != k && o.isCiv() && o != lord && !deps.Contains(o) && Fog.Knows(o, k) && Diplomacy.Overlord(o) != k).OrderByDescending(o => Profile.Get(o).Strength).Take(6).ToList();
            if (members.Count < 4) return false;
            Store.Put(k.data, KCoal, G.Year);
            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    if (!Diplomacy.Has(members[i], members[j], "defense_pact")) Diplomacy.Sign(members[i], members[j], "defense_pact", 15);
                }
            }
            Store.Put(k.data, KAe, Aggressive(k) * 0.7f);
            Chronicle.Log("coalition", k, G.Name(k), members.Count.ToString(), G.Name(members[0]), members[0]);
            return true;
        }

        public static bool Hold(Kingdom a, Kingdom b)
        {
            if (Escalating || Player.Ordering || !G.Ok(a) || !G.Ok(b)) return false;
            if (Profile.Get(a).Era < 10) return false;
            Crisis c = _crises.FirstOrDefault(x => x.A == a.data.id && x.B == b.data.id || x.A == b.data.id && x.B == a.data.id);
            if (c != null) return true;
            Start(a, b);
            return true;
        }

        public static void Start(Kingdom a, Kingdom b)
        {
            if (!G.Ok(a) || !G.Ok(b)) return;
            if (_crises.Any(x => x.A == a.data.id && x.B == b.data.id)) return;
            _crises.Add(new Crisis { A = a.data.id, B = b.data.id, Start = G.Year, Ends = G.Year + G.Range(1, 4) });
            Chronicle.Log("crisis_start", a, G.Name(a), G.Name(b), null, b);
        }

        public static int Crises(Kingdom k)
        {
            return G.Ok(k) ? _crises.Count(x => x.A == k.data.id || x.B == k.data.id) : 0;
        }

        private static void Crises()
        {
            foreach (Crisis c in _crises.ToList())
            {
                Kingdom a = G.KingdomById(c.A.ToString()), b = G.KingdomById(c.B.ToString());
                if (!G.Ok(a) || !G.Ok(b))
                {
                    _crises.Remove(c);
                    continue;
                }
                if (G.Year < c.Ends) continue;
                _crises.Remove(c);
                KProfile pa = Profile.Get(a), pb = Profile.Get(b);
                float esc = 0.35f + Politics.Aggression(a) * 0.3f + Exhaustion(b) / 300f - Exhaustion(a) / 200f;
                if (Diplomacy.Has(a, b, "trade_treaty")) esc -= 0.1f;
                if (pa.Fam.Contains("democratic") && pb.Fam.Contains("democratic")) esc -= 0.2f;
                if (War.Nukes(b) > 0) esc -= 0.2f;
                if (pa.Strength > pb.Strength * 2f) esc += 0.1f;
                if (G.Chance(G.Clamp(esc, 0.05f, 0.9f)))
                {
                    Escalating = true;
                    try
                    {
                        World.world.diplomacy.startWar(a, b, WarTypeLibrary.normal);
                    }
                    finally
                    {
                        Escalating = false;
                    }
                    Chronicle.Log("crisis_war", a, G.Name(a), G.Name(b), null, b);
                }
                else
                {
                    Diplomacy.Sign(a, b, "non_aggression", 10);
                    Politics.AddStability(a, -0.03f);
                    Politics.AddStability(b, 0.02f);
                    Chronicle.Log("crisis_resolved", a, G.Name(a), G.Name(b), null, b);
                }
            }
        }

        private static void Revanche(Kingdom k, KProfile p, int yrs)
        {
            string s = Store.Str(k.data, KHumil);
            if (s.Length == 0) return;
            int since = G.Year - Store.Int(k.data, KHumilY, G.Year);
            long id;
            Kingdom o = long.TryParse(s, out id) ? G.KingdomById(s) : null;
            if (!G.Ok(o) || since > 60)
            {
                Store.Put(k.data, KHumil, "");
                Bonus.Dirty(k);
                return;
            }
            if (since < 10 || p.AtWar || Player.Locks(k, "war") || Diplomacy.Overlord(k) == o) return;
            if (p.Strength < Profile.Get(o).Strength * 0.9f || !G.Chance(0.04f * yrs)) return;
            Store.Put(k.data, KHumil, "");
            Escalating = true;
            try
            {
                World.world.diplomacy.startWar(k, o, WarTypeLibrary.normal);
            }
            finally
            {
                Escalating = false;
            }
            Chronicle.Log("revanche", k, G.Name(k), G.Name(o), null, o);
        }

        public static bool Humiliated(Kingdom k)
        {
            return G.Ok(k) && Store.Str(k.data, KHumil).Length > 0;
        }

        public static bool Dynastic(Kingdom k)
        {
            KProfile p = Profile.Get(k);
            return p.Fam.Contains("monarchy") || p.Fam.Contains("imperial");
        }

        public static int Phase(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KDyn, -1) : -1;
        }

        private static void Dynasty(Kingdom k, KProfile p, int yrs)
        {
            int ph = Phase(k);
            if (!Dynastic(k))
            {
                if (ph >= 0)
                {
                    Store.Put(k.data, KDyn, -1);
                    Bonus.Dirty(k);
                }
                return;
            }
            if (ph < 0)
            {
                SetPhase(k, 0, false);
                return;
            }
            int len = PhaseYears[ph] + (int)(Math.Abs(k.data.id) % 11) - 5;
            if (ph == 0) Politics.AddStability(k, 0.01f * yrs);
            if (ph == 2) Politics.AddStability(k, -0.01f * yrs);
            bool early = ph == 2 && p.Legit < 0.2f || ph == 1 && p.Stability < 0.15f;
            if (G.Year - Store.Int(k.data, KDynY, G.Year) < len && !early) return;
            if (ph == 3)
            {
                Store.Put(k.data, KDynN, Store.Int(k.data, KDynN, 1) + 1);
                SetPhase(k, 0, true);
                return;
            }
            SetPhase(k, ph + 1, true);
            if (ph + 1 == 3)
            {
                Legitimacy.Shock(k, -0.2f);
                if (G.Chance(0.4f)) Legitimacy.Crisis(k, p, Legitimacy.Get(k));
            }
        }

        public static void SetPhase(Kingdom k, int ph, bool log)
        {
            Store.Put(k.data, KDyn, ph);
            Store.Put(k.data, KDynY, G.Year);
            Bonus.Dirty(k);
            if (log) Chronicle.Log("dynasty_" + Phases[ph], k, G.Name(k), Store.Int(k.data, KDynN, 1).ToString());
        }

        public static void Advance(Kingdom k)
        {
            if (!G.Ok(k)) return;
            int ph = Phase(k);
            if (ph < 0) ph = 0;
            SetPhase(k, (ph + 1) % Phases.Length, true);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!G.Ok(k)) return fx;
            switch (Phase(k))
            {
                case 0:
                    fx["prod"] = 0.03f;
                    break;
                case 1:
                    fx["gold"] = 0.08f;
                    fx["research"] = 0.05f;
                    fx["prod"] = 0.05f;
                    break;
                case 2:
                    fx["gold"] = -0.05f;
                    fx["prod"] = -0.03f;
                    break;
                case 3:
                    fx["gold"] = -0.12f;
                    fx["defense"] = -0.08f;
                    break;
            }
            float ex = (int)(Exhaustion(k) / 10f) * 10f;
            if (ex > 0f)
            {
                Store.Add(fx, "prod", -ex / 500f);
                Store.Add(fx, "attack", -ex / 400f);
            }
            if (Humiliated(k)) Store.Add(fx, "attack", 0.05f);
            return fx;
        }

        public static string[] Factions(int era)
        {
            if (era <= 2) return new[] { "elders", "warriors", "kin" };
            if (era <= 6) return new[] { "nobles", "clergy", "warriors", "peasants" };
            if (era <= 9) return new[] { "nobles", "clergy", "merchants", "guilds", "peasants" };
            if (era <= 14) return new[] { "industrialists", "workers", "military", "clergy", "intellectuals" };
            if (era <= 25) return new[] { "corporations", "scientists", "military", "citizens", "media" };
            return new[] { "syndicates", "synths", "fleet", "citizens", "sages" };
        }

        public static float Weight(Kingdom k, string f)
        {
            KProfile p = Profile.Get(k);
            float w = 1f;
            if ((p.Fam.Contains("military") || p.Fam.Contains("authoritarian")) && (f == "warriors" || f == "military" || f == "fleet")) w *= 2.5f;
            if (p.Fam.Contains("theocratic") && f == "clergy") w *= 3f;
            if (p.Fam.Contains("corporate") && (f == "corporations" || f == "industrialists" || f == "merchants" || f == "syndicates")) w *= 2.5f;
            if (p.Fam.Contains("technocratic") && (f == "scientists" || f == "intellectuals" || f == "sages" || f == "synths")) w *= 2.5f;
            if ((p.Fam.Contains("democratic") || p.Fam.Contains("liberal")) && (f == "peasants" || f == "workers" || f == "citizens" || f == "media")) w *= 2f;
            if ((p.Fam.Contains("monarchy") || p.Fam.Contains("imperial")) && f == "nobles") w *= 2.5f;
            if (p.Fam.Contains("tribal") && (f == "elders" || f == "warriors")) w *= 2f;
            return w;
        }

        private static float Lean(Kingdom k, string f, string id)
        {
            int h = 17;
            foreach (char ch in f + "|" + id) h = h * 31 + ch;
            float s = (Math.Abs(h) % 1000) / 500f - 1f;
            return s * 0.6f + 0.25f + (Politics.Stability(k) - 0.5f) * 0.4f;
        }

        public static float Support(Kingdom k, string id)
        {
            float yes = 0f, all = 0f;
            foreach (string f in Factions(Profile.Get(k).Era))
            {
                float w = Weight(k, f);
                all += w;
                if (Lean(k, f, id) > 0f) yes += w;
            }
            return all > 0f ? yes / all : 1f;
        }

        public static bool Blocked(Kingdom k)
        {
            return G.Ok(k) && (Store.Int(k.data, KBlock, -999) > G.Year || Legitimacy.Get(k) < 0.25f);
        }

        public static string LastVote(Kingdom k)
        {
            return G.Ok(k) ? Store.Str(k.data, KLastVote) : "";
        }

        public static bool Vote(Kingdom k, string group, string id, string reason)
        {
            if (reason == "player" || reason == "init" || reason == "sandbox" || group == "transport") return true;
            if (!G.Ok(k) || Profile.Get(k).Era < 3) return true;
            if (Blocked(k)) return false;
            float s = Support(k, id);
            bool pass = s >= 0.5f;
            Store.Put(k.data, KLastVote, id + "|" + (pass ? "1" : "0") + "|" + G.Year);
            if (pass)
            {
                Store.Put(k.data, KFail, 0);
                return true;
            }
            int n = Store.Int(k.data, KFail, 0) + 1;
            if (n >= 3)
            {
                Store.Put(k.data, KBlock, G.Year + 2);
                n = 0;
                Chronicle.Log("parliament_deadlock", k, G.Name(k), Txt.T("civ_p_" + id));
            }
            Store.Put(k.data, KFail, n);
            return false;
        }

        public static void Deadlock(Kingdom k)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KBlock, G.Year + 2);
            Chronicle.Log("parliament_deadlock", k, G.Name(k), "-");
        }

        public static void ForcePeace(Kingdom k)
        {
            if (!G.Ok(k)) return;
            foreach (global::War w in World.world.wars.getWars(k).ToList())
            {
                if (w == null || w.hasEnded()) continue;
                World.world.wars.endWar(w, WarWinner.Peace);
            }
        }

        public static void ForceCrisis(Kingdom k)
        {
            if (!G.Ok(k)) return;
            Kingdom o = G.Pick(Fog.KnownKingdoms(k).Where(x => G.Ok(x) && x != k && x.isCiv()).ToList());
            if (o != null) Start(k, o);
        }

        public static void SetAe(Kingdom k, float v)
        {
            if (G.Ok(k)) Store.Put(k.data, KAe, v);
        }

        public static void SetExh(Kingdom k, float v)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KExh, v);
            Bonus.Dirty(k);
        }
    }

    internal static class Ledger
    {
        public static readonly string[] Flows = { "production", "trade", "taxes", "diplomacy", "war", "upkeep", "events", "space", "other" };
        public static int Ctx = -1;

        private static readonly Dictionary<string, int> Labels = new Dictionary<string, int>
        {
            { "economy", 0 }, { "roles", 0 }, { "catchup", 0 }, { "houses", 0 },
            { "depot", 1 }, { "companies", 1 }, { "transit", 1 }, { "internet-year", 1 },
            { "currency", 2 }, { "player-year", 2 },
            { "diplomacy-year", 3 }, { "dependent", 3 }, { "diplomacy", 3 }, { "orgs-year", 3 },
            { "hub-peace", 4 }, { "war-year", 4 }, { "warscore", 4 },
            { "vehicles", 5 }, { "walls", 5 },
            { "events-year", 6 }, { "sagas-year", 6 }, { "saga-answer", 6 }, { "festivals", 6 },
            { "cosmos-year", 7 }, { "space", 7 }, { "cos-expand", 7 }, { "cosmos-mission", 7 }
        };

        private static Dictionary<long, float[]> _cur = new Dictionary<long, float[]>();
        private static Dictionary<long, float[]> _last = new Dictionary<long, float[]>();

        public static int Label(string what)
        {
            int f;
            return what != null && Labels.TryGetValue(what, out f) ? f : -1;
        }

        public static void Clear()
        {
            _cur.Clear();
            _last.Clear();
        }

        public static void Roll()
        {
            _last = _cur;
            _cur = new Dictionary<long, float[]>();
        }

        public static void Gold(City c, int amount)
        {
            if (c == null || amount == 0 || c.kingdom == null || c.kingdom.data == null) return;
            float[] a;
            if (!_cur.TryGetValue(c.kingdom.data.id, out a)) _cur[c.kingdom.data.id] = a = new float[Flows.Length * 2];
            int f = Ctx >= 0 ? Ctx : Flows.Length - 1;
            if (amount > 0) a[f * 2] += amount;
            else a[f * 2 + 1] -= amount;
        }

        public static float[] Last(Kingdom k)
        {
            float[] a;
            if (!G.Ok(k)) return null;
            if (_last.TryGetValue(k.data.id, out a)) return a;
            return _cur.TryGetValue(k.data.id, out a) ? a : null;
        }
    }
}
