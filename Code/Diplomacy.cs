using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class Treaty
    {
        public string Type = "";
        public long A;
        public long B;
        public int Since;
        public int Until = -1;
    }

    internal class Dep
    {
        public long Id;
        public long Lord;
        public string Kind = "";
        public float Autonomy = 40f;
        public int Since;
        public int LowYears;
    }

    internal static class Diplomacy
    {
        private const string KTreaties = "civ_treaties";
        private const string KDeps = "civ_deps";
        private static List<Treaty> _treaties = new List<Treaty>();
        private static Dictionary<long, Dep> _deps = new Dictionary<long, Dep>();
        private static bool _loaded;

        public static readonly string[] TreatyTypes = { "truce", "tribute", "royal_marriage", "hostages", "non_aggression", "trade_treaty", "embassy", "defense_pact", "entente", "nuclear_umbrella", "armistice" };
        public static readonly string[] DepKinds = { "tributary", "vassal", "protectorate", "colony", "dominion", "satellite", "associated" };

        private static readonly Dictionary<string, int> TreatyEra = new Dictionary<string, int>
        {
            { "truce", 0 }, { "tribute", 3 }, { "royal_marriage", 3 }, { "hostages", 5 }, { "non_aggression", 6 }, { "trade_treaty", 8 },
            { "embassy", 8 }, { "defense_pact", 6 }, { "entente", 10 }, { "nuclear_umbrella", 12 }, { "armistice", 12 }
        };

        public static IEnumerable<Treaty> Treaties
        {
            get
            {
                Ensure();
                return _treaties;
            }
        }

        public static IEnumerable<Dep> Dependents
        {
            get
            {
                Ensure();
                return _deps.Values;
            }
        }

        public static void Clear()
        {
            _loaded = false;
            _treaties = new List<Treaty>();
            _deps = new Dictionary<long, Dep>();
        }

        private static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            SaveCustomData d = Store.World();
            if (d == null) return;
            try
            {
                string s = Store.Str(d, KTreaties);
                if (s.Length > 0) _treaties = JsonConvert.DeserializeObject<List<Treaty>>(s) ?? new List<Treaty>();
                string t = Store.Str(d, KDeps);
                if (t.Length > 0)
                {
                    var l = JsonConvert.DeserializeObject<List<Dep>>(t) ?? new List<Dep>();
                    foreach (Dep x in l) _deps[x.Id] = x;
                }
            }
            catch (Exception)
            {
                _treaties = new List<Treaty>();
                _deps = new Dictionary<long, Dep>();
            }
        }

        public static void Save()
        {
            if (!_loaded) return;
            SaveCustomData d = Store.World();
            if (d == null) return;
            Store.Put(d, KTreaties, JsonConvert.SerializeObject(_treaties));
            Store.Put(d, KDeps, JsonConvert.SerializeObject(_deps.Values.ToList()));
        }

        private static long Id(Kingdom k)
        {
            return k != null && k.data != null ? k.data.id : -1;
        }

        private static Kingdom K(long id)
        {
            Kingdom k = World.world.kingdoms.get(id);
            return G.Ok(k) ? k : null;
        }

        public static bool Has(Kingdom a, Kingdom b, string type)
        {
            if (a == null || b == null) return false;
            Ensure();
            long x = Id(a), y = Id(b);
            foreach (Treaty t in _treaties)
            {
                if (t.Type != type) continue;
                if ((t.A == x && t.B == y) || (t.A == y && t.B == x)) return true;
            }
            return false;
        }

        public static List<Treaty> Between(Kingdom a, Kingdom b)
        {
            Ensure();
            var l = new List<Treaty>();
            long x = Id(a), y = Id(b);
            foreach (Treaty t in _treaties) if ((t.A == x && t.B == y) || (t.A == y && t.B == x)) l.Add(t);
            return l;
        }

        public static List<Treaty> Of(Kingdom a)
        {
            Ensure();
            long x = Id(a);
            return _treaties.Where(t => t.A == x || t.B == x).ToList();
        }

        public static void Sign(Kingdom a, Kingdom b, string type, int years)
        {
            if (!G.Ok(a) || !G.Ok(b) || a == b) return;
            Ensure();
            if (Has(a, b, type))
            {
                foreach (Treaty t in Between(a, b)) if (t.Type == type && years > 0) t.Until = Math.Max(t.Until, G.Year + years);
                return;
            }
            _treaties.Add(new Treaty { Type = type, A = Id(a), B = Id(b), Since = G.Year, Until = years > 0 ? G.Year + years : -1 });
            Hub.Raise(Hub.TreatySigned, a, b, a.capital, null, type);
        }

        public static void Break(Kingdom a, Kingdom b, string type)
        {
            Ensure();
            long x = Id(a), y = Id(b);
            int n = _treaties.RemoveAll(t => (type == null || t.Type == type) && ((t.A == x && t.B == y) || (t.A == y && t.B == x)));
            if (n > 0) Hub.Raise(Hub.TreatyBroken, a, b, null, null, type ?? "all");
        }

        public static Dep DepOf(Kingdom k)
        {
            if (k == null || k.data == null) return null;
            Ensure();
            Dep d;
            return _deps.TryGetValue(k.data.id, out d) ? d : null;
        }

        public static Kingdom Overlord(Kingdom k)
        {
            Dep d = DepOf(k);
            return d != null ? K(d.Lord) : null;
        }

        public static List<Kingdom> DependentsOf(Kingdom lord)
        {
            Ensure();
            var l = new List<Kingdom>();
            long id = Id(lord);
            foreach (Dep d in _deps.Values)
            {
                if (d.Lord != id) continue;
                Kingdom k = K(d.Id);
                if (k != null) l.Add(k);
            }
            return l;
        }

        public static string KindFor(Kingdom lord, Kingdom dep)
        {
            KProfile p = Profile.Get(lord);
            int era = p.Era;
            bool overseas = false;
            WorldTile a = G.Tile(lord.capital), b = G.Tile(dep.capital);
            if (a != null && b != null && !a.isSameIsland(b)) overseas = true;
            if (era <= 5) return "tributary";
            if (era <= 7) return overseas && era >= 7 ? "colony" : "vassal";
            if (era <= 11) return overseas ? "colony" : (p.Fam.Contains("feudal") || p.Fam.Contains("monarchy") && era <= 8 ? "vassal" : "protectorate");
            if (p.Fam.Contains("authoritarian") || p.Fam.Contains("revolutionary")) return "satellite";
            if (era >= 13 && p.Fam.Contains("democratic")) return "associated";
            return overseas && era <= 12 ? "colony" : "dominion";
        }

        public static void MakeDependent(Kingdom dep, Kingdom lord, string why)
        {
            if (!G.Ok(dep) || !G.Ok(lord) || dep == lord) return;
            if (Overlord(lord) == dep) return;
            Ensure();
            string kind = KindFor(lord, dep);
            _deps[dep.data.id] = new Dep { Id = dep.data.id, Lord = lord.data.id, Kind = kind, Autonomy = 45f, Since = G.Year };
            foreach (Kingdom sub in DependentsOf(dep)) _deps.Remove(sub.data.id);
            if (World.world.wars.isInWarWith(dep, lord))
            {
                foreach (global::War w in dep.getWars().ToList())
                {
                    if (w.isAttacker(lord) || w.isDefender(lord)) World.world.wars.endWar(w, WarWinner.Peace);
                }
            }
            Hub.Raise(Hub.Dependent, dep, lord, dep.capital, null, kind, why);
        }

        public static void Release(Kingdom dep, string why)
        {
            Dep d = DepOf(dep);
            if (d == null) return;
            Kingdom lord = K(d.Lord);
            _deps.Remove(d.Id);
            Hub.Raise(Hub.Independence, dep, lord, dep.capital, null, d.Kind, why);
        }

        public static bool Forbids(Kingdom a, Kingdom b)
        {
            if (a == null || b == null) return false;
            if (Overlord(a) == b || Overlord(b) == a) return true;
            Kingdom la = Overlord(a), lb = Overlord(b);
            if (la != null && la == lb) return true;
            foreach (Treaty t in Between(a, b))
            {
                if (t.Type == "truce" || t.Type == "non_aggression" || t.Type == "defense_pact" || t.Type == "entente" || t.Type == "armistice" || t.Type == "hostages" || t.Type == "nuclear_umbrella") return true;
            }
            return false;
        }

        public static List<Kingdom> Protectors(Kingdom k)
        {
            var l = new List<Kingdom>();
            Kingdom lord = Overlord(k);
            if (lord != null) l.Add(lord);
            foreach (Treaty t in Of(k))
            {
                if (t.Type != "defense_pact" && t.Type != "entente" && t.Type != "nuclear_umbrella") continue;
                Kingdom o = K(t.A == k.data.id ? t.B : t.A);
                if (o != null && !l.Contains(o)) l.Add(o);
            }
            foreach (Kingdom o in Orgs.Allies(k)) if (G.Ok(o) && o != k && !l.Contains(o)) l.Add(o);
            return l;
        }

        public static void Year()
        {
            Ensure();
            int y = G.Year;
            var ended = _treaties.Where(t => t.Until >= 0 && t.Until < y).ToList();
            foreach (Treaty t in ended) _treaties.Remove(t);
            _treaties.RemoveAll(t => K(t.A) == null || K(t.B) == null);
            foreach (long id in _deps.Keys.ToList())
            {
                Dep d = _deps[id];
                Kingdom k = K(d.Id), lord = K(d.Lord);
                if (k == null || lord == null)
                {
                    _deps.Remove(id);
                    continue;
                }
                CivMod.Safe("dependent", () => DepYear(k, lord, d));
            }
            Tribute();
        }

        private static void DepYear(Kingdom k, Kingdom lord, Dep d)
        {
            KProfile pk = Profile.Get(k), pl = Profile.Get(lord);
            float drift = -1.5f;
            WorldTile a = G.Tile(lord.capital), b = G.Tile(k.capital);
            if (a != null && b != null)
            {
                drift += Math.Min(3f, G.Dist(a, b) / 80f);
                if (!a.isSameIsland(b)) drift += 1.5f;
            }
            if (pk.Species != pl.Species) drift += 1.5f;
            if (k.getCulture() != lord.getCulture()) drift += 1f;
            if (pk.Ideo != pl.Ideo) drift += 0.5f;
            if (pl.Legit < 0.35f) drift += 2f;
            if (pl.AtWar && pl.Stability < 0.4f) drift += 1.5f;
            if (pl.Era >= 12 && d.Kind == "colony") drift += 4f;
            if (Policies.Id(lord, "xeno") == "xenophobia" && pk.Species != pl.Species) drift += 1.5f;
            if (pl.Strength > pk.Strength * 4f) drift -= 1f;
            d.Autonomy = G.Clamp(d.Autonomy + drift + UnityEngine.Random.Range(-1f, 1f), 0f, 100f);
            d.LowYears = d.Autonomy < 15f ? d.LowYears + 1 : 0;
            string kind = KindFor(lord, k);
            if (kind != d.Kind && (Array.IndexOf(DepKinds, kind) > Array.IndexOf(DepKinds, d.Kind) || d.Kind == "tributary"))
            {
                if (d.Kind == "colony" && kind != "colony" && d.Autonomy > 55f && pl.Era >= 10) kind = "dominion";
                d.Kind = kind;
                Hub.Raise(Hub.Dependent, k, lord, k.capital, null, kind, "evolved");
            }
            if (d.LowYears >= 30 && a != null && b != null && a.isSameIsland(b) && pk.Cities <= Math.Max(3, pl.Cities / 2))
            {
                _deps.Remove(d.Id);
                foreach (City c in G.Cities(k).ToList()) c.joinAnotherKingdom(lord);
                Hub.Raise(Hub.Integrated, lord, k, lord.capital, null, d.Kind);
                return;
            }
            float indep = d.Autonomy > 80f ? (d.Autonomy - 80f) * 0.006f : 0f;
            if (indep > 0f && G.Chance(indep))
            {
                Release(k, "independence");
                if (pk.Strength * 1.5f < pl.Strength && G.Chance(0.6f)) World.world.diplomacy.startWar(lord, k, WarTypeLibrary.normal);
            }
        }

        private static void Tribute()
        {
            foreach (Dep d in _deps.Values.ToList())
            {
                Kingdom k = K(d.Id), lord = K(d.Lord);
                if (k == null || lord == null || k.capital == null || lord.capital == null) continue;
                float share = d.Kind == "tributary" ? 0.12f : d.Kind == "colony" ? 0.15f : d.Kind == "vassal" ? 0.1f : d.Kind == "associated" ? 0.02f : 0.06f;
                int gold = (int)(Economy.Amount(k.capital, "gold") * share);
                if (gold > 0 && Economy.Take(k.capital, "gold", gold)) Economy.Give(lord.capital, "gold", gold);
            }
            foreach (Treaty t in _treaties)
            {
                if (t.Type != "tribute") continue;
                Kingdom payer = K(t.A), receiver = K(t.B);
                if (payer == null || receiver == null || payer.capital == null || receiver.capital == null) continue;
                int gold = (int)(Economy.Amount(payer.capital, "gold") * 0.1f);
                if (gold > 0 && Economy.Take(payer.capital, "gold", gold)) Economy.Give(receiver.capital, "gold", gold);
            }
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            Ensure();
            KProfile p = Profile.Get(k);
            if (DepOf(k) != null) return;
            int tries = 2;
            foreach (Kingdom o in Research.Contacts(k))
            {
                if (tries-- <= 0) break;
                if (!G.Ok(o) || o == k || !Fog.Knows(k, o)) continue;
                CivMod.Safe("treaty-ai", () => Consider(k, o, p, yrs));
            }
            CivMod.Safe("protection", () => SeekProtection(k, p));
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

        private static void Consider(Kingdom k, Kingdom o, KProfile p, int yrs)
        {
            if (World.world.wars.isInWarWith(k, o)) return;
            KProfile q = Profile.Get(o);
            int era = Math.Min(p.Era, q.Era);
            int op = Math.Min(Opinion(k, o), Opinion(o, k));
            float chance = 0.04f * yrs;
            if (op < 0 && p.Strength > q.Strength * 4f && era >= 3 && !p.Fam.Contains("democratic") && G.Chance(0.05f * yrs) && !q.AtWar)
            {
                if (q.Strength * 3f < p.Strength && G.Chance(0.5f)) MakeDependent(o, k, "demand");
                else if (era >= 3 && !Has(o, k, "tribute")) Sign(o, k, "tribute", 25);
                return;
            }
            if (op < 10 || !G.Chance(chance)) return;
            var options = new List<string>();
            foreach (string t in TreatyTypes)
            {
                if (t == "truce" || t == "armistice" || t == "tribute") continue;
                if (TreatyEra[t] > era || Has(k, o, t)) continue;
                if (t == "royal_marriage" && !(p.Fam.Contains("monarchy") && q.Fam.Contains("monarchy"))) continue;
                if (t == "nuclear_umbrella" && !(War.Nukes(k) > 0 || War.Nukes(o) > 0)) continue;
                if (t == "entente" && op < 40) continue;
                if (t == "defense_pact" && op < 30) continue;
                if (t == "trade_treaty" && Economy.TradeOpinion(k, o) <= 0) continue;
                if (t == "hostages" && op > 30) continue;
                options.Add(t);
            }
            if (options.Count == 0) return;
            string pick = G.Pick(options);
            int years = pick == "royal_marriage" ? 60 : pick == "hostages" ? 20 : pick == "embassy" ? -1 : 40;
            Sign(k, o, pick, years);
        }

        private static void SeekProtection(Kingdom k, KProfile p)
        {
            if (!p.AtWar || p.Era < 3) return;
            float enemy = 0f;
            foreach (global::War w in k.getWars())
            {
                Kingdom e = w.isAttacker(k) ? w.getMainDefender() : w.getMainAttacker();
                if (G.Ok(e)) enemy = Math.Max(enemy, Profile.Get(e).Strength);
            }
            if (enemy < p.Strength * 2.5f || !G.Chance(0.15f)) return;
            Kingdom best = null;
            float bs = 0f;
            foreach (Kingdom o in Research.Contacts(k))
            {
                if (!G.Ok(o) || World.world.wars.isInWarWith(k, o) || DepOf(o) != null || !Fog.Knows(k, o)) continue;
                if (Opinion(o, k) < 0) continue;
                float s = Profile.Get(o).Strength;
                if (s > enemy && s > bs)
                {
                    bs = s;
                    best = o;
                }
            }
            if (best != null) MakeDependent(k, best, "protection");
        }

        public static void Register()
        {
            AssetManager.opinion_library.add(new OpinionAsset
            {
                id = "civ_treaties",
                translation_key = "opinion_civ_treaties",
                translation_key_negative = "opinion_civ_treaties_bad",
                calc = delegate(Kingdom a, Kingdom b)
                {
                    if (a == null || b == null || a == b) return 0;
                    int v = 0;
                    foreach (Treaty t in Between(a, b))
                    {
                        switch (t.Type)
                        {
                            case "royal_marriage": v += 25; break;
                            case "defense_pact": v += 20; break;
                            case "entente": v += 30; break;
                            case "trade_treaty": v += 10; break;
                            case "embassy": v += 8; break;
                            case "non_aggression": v += 8; break;
                            case "nuclear_umbrella": v += 25; break;
                            case "tribute": v += t.A == a.data.id ? -15 : 10; break;
                            case "truce": v += 5; break;
                            case "armistice": v -= 10; break;
                        }
                    }
                    if (Overlord(a) == b) v += 30;
                    if (Overlord(b) == a) v += 20;
                    return v;
                }
            });
            Hub.On(Hub.WarStarted, e =>
            {
                global::War w = null;
                if (e.K == null || e.O == null) return;
                foreach (global::War x in e.K.getWars()) if (x.isAttacker(e.K) && x.isDefender(e.O)) w = x;
                if (w == null) return;
                foreach (Kingdom d in DependentsOf(e.K)) if (!w.isDefender(d)) w.joinAttackers(d);
                foreach (Kingdom d in Protectors(e.O)) if (d != e.K && !w.isAttacker(d) && !Forbids(d, e.K)) w.joinDefenders(d);
                foreach (Kingdom d in DependentsOf(e.O)) if (!w.isAttacker(d)) w.joinDefenders(d);
                foreach (Treaty t in Between(e.K, e.O).ToList())
                {
                    if (t.Type == "royal_marriage" || t.Type == "trade_treaty" || t.Type == "embassy" || t.Type == "non_aggression" || t.Type == "truce" || t.Type == "hostages") Break(e.K, e.O, t.Type);
                }
            });
        }

        [HarmonyPatch(typeof(DiplomacyManager), nameof(DiplomacyManager.startWar))]
        private static class Block
        {
            private static bool Prefix(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pAsset, ref global::War __result)
            {
                if (pAsset == null || pAttacker == null || pDefender == null) return true;
                if (pAsset.id == "rebellion" || pAsset.id == "whisper_of_war" || pAsset.total_war) return true;
                bool block = false;
                CivMod.Safe("war-block", () =>
                {
                    if (Player.Locks(pAttacker, "war") && !Player.Ordering) block = true;
                    else if (DepOf(pAttacker) != null && Overlord(pAttacker) != pDefender) block = true;
                    else if (Forbids(pAttacker, pDefender)) block = !(Profile.Get(pAttacker).Fam.Contains("military") && G.Chance(0.05f));
                    else if (!Fog.Knows(pAttacker, pDefender)) block = true;
                    else if (Statecraft.Hold(pAttacker, pDefender)) block = true;
                });
                if (!block) return true;
                __result = null;
                return false;
            }
        }
    }
}
