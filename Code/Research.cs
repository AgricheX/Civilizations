using System;
using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal class KState
    {
        public TechSet Known;
        public TechSet Found;
        public int Era = -1;
        public int Floor;
        public string Route = "";
        public Dictionary<string, float> Aff = new Dictionary<string, float>();
    }

    internal static class Research
    {
        public const string KBits = "civ_tb";
        public const string KFound = "civ_tf";
        public const string KFloor = "civ_era_floor";
        public const string KCur = "civ_cur";
        public const string KProg = "civ_prog";
        public const string KRate = "civ_rate";
        public const string KRoute = "civ_route";
        public const string KAff = "civ_aff";
        public const string KOwner = "civ_tk";
        public const string KArchive = "civ_arch";
        public const string KDark = "civ_dark";
        public const string KVer = "civ_tv";
        public const int RouteEra = 10;

        private static readonly Dictionary<long, KState> _cache = new Dictionary<long, KState>();
        private static readonly Dictionary<string, List<KeyValuePair<int, List<TechDef>>>> _levels = new Dictionary<string, List<KeyValuePair<int, List<TechDef>>>>();
        public static readonly Dictionary<string, string> CultureTraitTech = new Dictionary<string, string>();
        public static readonly HashSet<string> KnowledgeTraits = new HashSet<string> { "ancestors_knowledge", "training_potential", "fast_learners", "expertise_exchange", "reading_lovers", "attentive_readers" };

        public static void ClearCache()
        {
            _cache.Clear();
            _levels.Clear();
        }

        public static void Forget(Kingdom k)
        {
            if (k != null && k.data != null) _cache.Remove(k.data.id);
        }

        public static void InitCultureLock()
        {
            CultureTraitTech.Clear();
            foreach (TechDef t in Db.Techs)
            {
                foreach (string u in t.Unl)
                {
                    if (u.StartsWith("culture:")) CultureTraitTech[u.Substring(8)] = t.Id;
                }
            }
            string[] plans =
            {
                "city_layout_pebbles", "city_layout_stone_garden", "city_layout_tile_wobbly_pattern", "city_layout_honeycomb",
                "city_layout_bricks", "city_layout_raindrops", "city_layout_cross", "city_layout_claws",
                "city_layout_architects_eye", "city_layout_madman_labyrinth", "city_layout_titan_footprints", "city_layout_silk_web",
                "city_layout_tile_moonsteps", "city_layout_the_grand_arrangement", "city_layout_iron_weave", "city_layout_monolith_mesh",
                "city_layout_royal_checkers", "city_layout_diamond", "city_layout_parallels", "city_layout_pillars", "city_layout_rings"
            };
            string[] planTechs =
            {
                "mud_brick", "mud_brick", "masonry", "masonry", "the_arch", "the_arch", "architectural_orders",
                "castles", "castles", "gothic_arch", "humanism", "humanism", "neoclassicism", "neoclassicism", "industrial_bricks",
                "industrial_bricks", "reinforced_concrete", "reinforced_concrete", "high_rises", "high_rises", "skyscrapers"
            };
            for (int i = 0; i < plans.Length; i++) CultureTraitTech[plans[i]] = planTechs[i];
            CultureTraitTech["craft_flame_weapon"] = "philosophy";
            CultureTraitTech["craft_ice_weapon"] = "philosophy";
            CultureTraitTech["craft_evil_staff"] = "philosophy";
            CultureTraitTech["craft_white_staff"] = "philosophy";
            CultureTraitTech["craft_necro_staff"] = "philosophy";
            CultureTraitTech["craft_druid_staff"] = "philosophy";
            CultureTraitTech["craft_doctor_staff"] = "hippocratic_medicine";
            CultureTraitTech["craft_shotgun"] = "flintlock";
            CultureTraitTech["craft_blaster"] = "lasers";
            foreach (var kv in CultureTraitTech)
            {
                CultureTrait t = AssetManager.culture_traits.get(kv.Key);
                if (t != null) t.spawn_random_trait_allowed = false;
            }
            foreach (ActorAsset a in AssetManager.actor_library.list)
            {
                if (a.default_culture_traits == null) continue;
                a.default_culture_traits.RemoveAll(id => CultureTraitTech.ContainsKey(id));
            }
        }

        public static KState State(Kingdom k)
        {
            if (k == null || k.data == null) return null;
            KState s;
            if (_cache.TryGetValue(k.data.id, out s)) return s;
            s = new KState();
            int ver = Store.Int(k.data, KVer, 0);
            s.Known = ver == Db.Techs.Count ? TechSet.Load(Store.Str(k.data, KBits)) : new TechSet();
            s.Found = ver == Db.Techs.Count ? TechSet.Load(Store.Str(k.data, KFound)) : new TechSet();
            s.Route = Store.Str(k.data, KRoute);
            s.Aff = Store.Map(k.data, KAff);
            s.Floor = Store.Int(k.data, KFloor, 0);
            _cache[k.data.id] = s;
            if (ver != Db.Techs.Count)
            {
                Store.Put(k.data, KVer, Db.Techs.Count);
                if (ver != 0) Legacy.RebuildTechs(k);
            }
            return s;
        }

        public static TechSet Known(Kingdom k)
        {
            KState s = State(k);
            return s == null ? new TechSet() : s.Known;
        }

        public static bool Has(Kingdom k, string tech)
        {
            if (string.IsNullOrEmpty(tech)) return true;
            if (k == null || !k.isCiv()) return false;
            TechDef t = Db.Tech(tech);
            return t != null && Known(k).Has(t.Index);
        }

        public static bool Has(City c, string tech)
        {
            if (string.IsNullOrEmpty(tech)) return true;
            return c != null && Has(c.kingdom, tech);
        }

        public static bool Has(Actor a, string tech)
        {
            return a != null && Has(a.kingdom, tech);
        }

        public static bool HasUnlock(Kingdom k, string key)
        {
            List<TechDef> l = Db.UnlockedBy(key);
            if (l == null) return false;
            TechSet known = Known(k);
            foreach (TechDef t in l) if (known.Has(t.Index)) return true;
            return false;
        }

        public static bool HasFeature(Kingdom k, string feat)
        {
            return HasUnlock(k, "feat:" + feat);
        }

        public static int Level(Kingdom k, string prefix)
        {
            int best = -1;
            TechSet known = Known(k);
            List<KeyValuePair<int, List<TechDef>>> levels;
            if (!_levels.TryGetValue(prefix, out levels))
            {
                levels = new List<KeyValuePair<int, List<TechDef>>>();
                foreach (var kv in Db.Unlocks)
                {
                    if (!kv.Key.StartsWith(prefix)) continue;
                    int v;
                    if (int.TryParse(kv.Key.Substring(prefix.Length), out v) && v >= 0) levels.Add(new KeyValuePair<int, List<TechDef>>(v, kv.Value));
                }
                _levels[prefix] = levels;
            }
            foreach (var kv in levels)
            {
                if (kv.Key <= best) continue;
                foreach (TechDef t in kv.Value)
                {
                    if (known.Has(t.Index))
                    {
                        best = kv.Key;
                        break;
                    }
                }
            }
            return best;
        }

        public static int Era(Kingdom k)
        {
            KState s = State(k);
            if (s == null) return 0;
            if (s.Era < 0)
            {
                s.Era = ComputeEra(s);
                if (s.Era > s.Floor)
                {
                    s.Floor = s.Era;
                    Store.Put(k.data, KFloor, s.Floor);
                }
            }
            return s.Era;
        }

        public static int Era(City c)
        {
            return c == null ? 0 : Era(c.kingdom);
        }

        public static string Route(Kingdom k)
        {
            KState s = State(k);
            return s == null ? "" : s.Route;
        }

        public static bool RouteMatches(Kingdom k, string route)
        {
            if (string.IsNullOrEmpty(route)) return true;
            KState s = State(k);
            if (s == null) return false;
            if (s.Route == route) return true;
            if (route == "real" && string.IsNullOrEmpty(s.Route)) return false;
            return false;
        }

        public static string VisualRoute(Kingdom k)
        {
            KState s = State(k);
            if (s == null || string.IsNullOrEmpty(s.Route)) return "real";
            return s.Route;
        }

        private static bool CountsForEra(KState s, TechDef t)
        {
            if (!t.IsCore) return false;
            if (string.IsNullOrEmpty(t.Route)) return true;
            if (string.IsNullOrEmpty(s.Route)) return t.Route == "real";
            return t.Route == s.Route || (s.Route == "diesel" && t.Route == "real" && t.Era < 11) || (s.Route == "atom" && t.Route == "real" && t.Era < 12) || (s.Route == "bio" && t.Route == "real" && t.Era < 13);
        }

        public static float EraFraction(KState s, int era)
        {
            if (era < 0 || era > Db.LastEra) return 0f;
            int total = 0, have = 0;
            foreach (TechDef t in Db.Eras[era].Core)
            {
                if (!CountsForEra(s, t)) continue;
                total++;
                if (s.Known.Has(t.Index)) have++;
            }
            return total == 0 ? 1f : (float)have / total;
        }

        public static float EraFraction(Kingdom k, int era)
        {
            KState s = State(k);
            return s == null ? 0f : EraFraction(s, era);
        }

        public static readonly string[][] Fundamentals =
        {
            new[] { "fire_control", "stone_knapping", "gathering", "language" },
            new[] { "bow_and_arrow", "fishing_nets", "wild_grains", "basketry" },
            new[] { "agriculture", "animal_husbandry", "pottery", "weaving" },
            new[] { "copper_smelting", "the_wheel", "writing", "plough" },
            new[] { "bronze_working", "masonry", "law_code", "horse_domestication" },
            new[] { "iron_working", "alphabet", "coinage", "mathematics" },
            new[] { "philosophy", "the_arch", "aqueducts", "gears" },
            new[] { "crucible_steel", "windmill", "universities", "paper" },
            new[] { "printing_press", "telescope", "cannon", "ocean_navigation" },
            new[] { "scientific_method", "chemistry", "vaccination", "paper_money" },
            new[] { "steam_engine", "railways", "factory_system", "telegraph" },
            new[] { "electricity", "reinforced_concrete", "radio", "assembly_line" },
            new[] { "nuclear_fission", "transistor", "antibiotics", "highways" },
            new[] { "rocketry", "microprocessor", "satellites", "personal_computer" },
            new string[0]
        };

        public static bool IsFundamental(TechDef t)
        {
            return t != null && t.Era < Fundamentals.Length && Array.IndexOf(Fundamentals[t.Era], t.Id) >= 0;
        }

        public static bool HasFundamentals(KState s, int era)
        {
            if (era < 0 || era >= Fundamentals.Length) return true;
            foreach (string id in Fundamentals[era])
            {
                TechDef t = Db.Tech(id);
                if (t != null && !s.Known.Has(t.Index)) return false;
            }
            return true;
        }

        public static List<TechDef> MissingFundamentals(Kingdom k, int era)
        {
            var l = new List<TechDef>();
            KState s = State(k);
            if (s == null || era < 0 || era >= Fundamentals.Length) return l;
            foreach (string id in Fundamentals[era])
            {
                TechDef t = Db.Tech(id);
                if (t != null && !s.Known.Has(t.Index)) l.Add(t);
            }
            return l;
        }

        private static void Needed(KState s, TechDef t, HashSet<int> into, int depth)
        {
            if (t == null || depth > 12 || s.Known.Has(t.Index) || !into.Add(t.Index)) return;
            foreach (string r in t.Req) Needed(s, Db.Tech(r), into, depth + 1);
        }

        private static int ComputeEra(KState s)
        {
            int era = 0;
            while (era < Db.LastEra && EraFraction(s, era) >= 0.65f && (era < s.Floor || HasFundamentals(s, era))) era++;
            return Math.Max(era, Math.Min(s.Floor, Db.LastEra));
        }

        public static bool Apex(Kingdom k)
        {
            return Era(k) >= Db.LastEra && EraFraction(k, Db.LastEra) >= 0.95f;
        }

        public const string KCap = "civ_era_cap";

        public static int KingdomCap(Kingdom k)
        {
            return k == null || k.data == null ? -1 : Store.Int(k.data, KCap, -1);
        }

        public static int WorldCap()
        {
            SaveCustomData d = Store.World();
            return d == null ? -1 : Store.Int(d, KCap, -1);
        }

        public static int Cap(Kingdom k)
        {
            int a = KingdomCap(k);
            int b = WorldCap();
            if (a < 0) return b;
            if (b < 0) return a;
            return Math.Min(a, b);
        }

        public static void SetKingdomCap(Kingdom k, int era)
        {
            if (k == null || k.data == null) return;
            Store.Put(k.data, KCap, era);
        }

        public static void SetWorldCap(int era)
        {
            SaveCustomData d = Store.World();
            if (d != null) Store.Put(d, KCap, era);
        }

        public static bool Capped(Kingdom k, TechDef t)
        {
            int cap = Cap(k);
            return cap >= 0 && t != null && t.Era > cap;
        }

        public static bool Available(Kingdom k, KState s, TechDef t, bool ignoreMix = false)
        {
            if (s.Known.Has(t.Index)) return false;
            if (Capped(k, t)) return false;
            int era = s.Era < 0 ? ComputeEra(s) : s.Era;
            if (t.Era > era + 1) return false;
            if (t.Era == era + 1 && EraFraction(s, era) < 0.45f) return false;
            if (t.Rare && !s.Found.Has(t.Index)) return false;
            if (!string.IsNullOrEmpty(t.Route) && !ignoreMix)
            {
                if (string.IsNullOrEmpty(s.Route)) return false;
                if (t.Route != s.Route && !CountsForEra(s, t) && Store.Get(s.Aff, t.Route) < 2.5f) return false;
            }
            foreach (string r in t.Req)
            {
                TechDef p = Db.Tech(r);
                if (p != null && !s.Known.Has(p.Index)) return false;
            }
            return true;
        }

        public static void Learn(Kingdom k, TechDef t, string reason)
        {
            if (!G.Ok(k) || t == null) return;
            if (reason != "editor" && Capped(k, t)) return;
            KState s = State(k);
            int before = Era(k);
            if (!s.Known.Add(t.Index)) return;
            s.Era = -1;
            Save(k, s);
            GrantCulture(k, t.Id);
            foreach (KeyValuePair<string, float> kv in t.Eff)
            {
                if (kv.Key.StartsWith("route_")) Store.Add(s.Aff, kv.Key.Substring(6), kv.Value / 10f);
            }
            Store.Map(k.data, KAff, s.Aff);
            Bonus.Dirty(k);
            int after = Era(k);
            if (after >= RouteEra && string.IsNullOrEmpty(s.Route)) ChooseRoute(k, s, after);
            else if (after > before && after >= RouteEra) ReconsiderRoute(k, s, after);
            if (reason == "research" || reason == "company")
            {
                Internet.OnTech(k, t, reason);
                if (after > before) Internet.OnEra(k, after);
            }
            if (after > before) CivMod.Safe("era-kit", () => Catchup.Kit(k, after));
            Policies.OnTech(k, t);
            CivMod.Log(G.Name(k) + " learned " + t.Id + " (" + reason + ") year " + G.Year);
        }

        public static void Learn(Kingdom k, string id, string reason)
        {
            Learn(k, Db.Tech(id), reason);
        }

        public static void Lose(Kingdom k, TechDef t, string reason)
        {
            if (!G.Ok(k) || t == null) return;
            KState s = State(k);
            if (!s.Known.Remove(t.Index)) return;
            int raw = 0;
            while (raw < Db.LastEra && EraFraction(s, raw) >= 0.65f) raw++;
            if (raw < s.Floor)
            {
                s.Floor = raw;
                Store.Put(k.data, KFloor, raw);
            }
            s.Era = -1;
            Save(k, s);
            Bonus.Dirty(k);
            if (Store.Str(k.data, KCur) == t.Id) Store.Put(k.data, KProg, 0f);
            Internet.OnTechLost(k, t);
        }

        public static void Discover(Kingdom k, TechDef t)
        {
            KState s = State(k);
            if (s == null || !s.Found.Add(t.Index)) return;
            Store.Put(k.data, KFound, s.Found.Save());
            Internet.OnDiscovery(k, t);
        }

        public static void SetAll(Kingdom k, TechSet set)
        {
            KState s = State(k);
            s.Known = set;
            s.Floor = 0;
            Store.Put(k.data, KFloor, 0);
            s.Era = -1;
            Save(k, s);
            Bonus.Dirty(k);
            int era = Era(k);
            if (era >= RouteEra && string.IsNullOrEmpty(s.Route)) ChooseRoute(k, s, era);
            foreach (TechDef t in set.All()) GrantCulture(k, t.Id);
        }

        private static void Save(Kingdom k, KState s)
        {
            string bits = s.Known.Save();
            Store.Put(k.data, KBits, bits);
            Store.Put(k.data, KVer, Db.Techs.Count);
            foreach (City c in G.Cities(k))
            {
                Store.Put(c.data, KBits, bits);
                Store.Put(c.data, KOwner, k.data.id.ToString());
            }
        }

        public static void SetRoute(Kingdom k, string route)
        {
            KState s = State(k);
            if (s == null) return;
            string old = s.Route;
            s.Route = route ?? "";
            s.Era = -1;
            Store.Put(k.data, KRoute, s.Route);
            if (old != s.Route && !string.IsNullOrEmpty(s.Route)) Internet.OnRoute(k, s.Route, old);
            Bonus.Dirty(k);
        }

        private static List<string> RoutesFor(int era)
        {
            var l = new List<string> { "real", "steam", "tesla" };
            if (era >= 11) l.Add("diesel");
            if (era >= 12) l.Add("atom");
            if (era >= 13) l.Add("bio");
            return l;
        }

        private static float RouteWeight(Kingdom k, KState s, string r)
        {
            float w = 1f + Store.Get(s.Aff, r);
            string pref = WorldPrefs.Route;
            if (pref == r) w += 4f;
            if (r == "real" && pref == "real") w += 1f;
            PolicyDef ideo = Policies.Get(k, "ideo");
            if (ideo != null)
            {
                if (ideo.Id == "environmentalism" && r == "bio") w += 1.5f;
                if ((ideo.Id == "communism" || ideo.Id == "authoritarianism") && r == "diesel") w += 1.5f;
                if (ideo.Id == "technoprogressivism" && (r == "atom" || r == "tesla")) w += 1.5f;
                if (ideo.Id == "traditionalism" && r == "steam") w += 1f;
            }
            PolicyDef gov = Policies.Get(k, "gov");
            if (gov != null) w += GovRoute(gov.Id, r);
            w += Companies.RouteSupport(k, r);
            return Math.Max(0.1f, w);
        }

        private static readonly Dictionary<string, Dictionary<string, float>> GovRoutes = new Dictionary<string, Dictionary<string, float>>
        {
            { "monarchy", new Dictionary<string, float> { { "steam", 1f } } },
            { "feudal_monarchy", new Dictionary<string, float> { { "steam", 1.5f } } },
            { "elective_monarchy", new Dictionary<string, float> { { "steam", 1f }, { "real", 0.5f } } },
            { "absolute_monarchy", new Dictionary<string, float> { { "steam", 1.5f }, { "diesel", 1f } } },
            { "constitutional_monarchy", new Dictionary<string, float> { { "steam", 2f }, { "real", 0.5f } } },
            { "theocracy", new Dictionary<string, float> { { "steam", 1f }, { "bio", 0.5f } } },
            { "republic", new Dictionary<string, float> { { "real", 0.5f }, { "tesla", 0.5f } } },
            { "democracy", new Dictionary<string, float> { { "real", 1.5f }, { "tesla", 0.5f } } },
            { "merchant_republic", new Dictionary<string, float> { { "real", 1f }, { "steam", 0.5f } } },
            { "presidential_republic", new Dictionary<string, float> { { "real", 1f }, { "atom", 1.5f } } },
            { "parliamentary_republic", new Dictionary<string, float> { { "real", 1.5f }, { "tesla", 1f } } },
            { "single_party_state", new Dictionary<string, float> { { "diesel", 2f }, { "atom", 1f } } },
            { "military_junta", new Dictionary<string, float> { { "diesel", 2f }, { "atom", 0.5f } } },
            { "technocracy", new Dictionary<string, float> { { "tesla", 2f }, { "atom", 1f }, { "bio", 0.5f } } },
            { "corporatocracy", new Dictionary<string, float> { { "bio", 2f }, { "real", 1f } } },
            { "tribal_council", new Dictionary<string, float> { { "bio", 0.5f } } },
            { "chiefdom", new Dictionary<string, float> { { "steam", 0.5f } } },
        };

        private static float GovRoute(string gov, string r)
        {
            Dictionary<string, float> m;
            float v;
            if (gov != null && GovRoutes.TryGetValue(gov, out m) && m.TryGetValue(r, out v)) return v;
            return 0f;
        }

        private static void ChooseRoute(Kingdom k, KState s, int era)
        {
            List<string> opts = RoutesFor(era);
            var w = new List<float>();
            foreach (string r in opts) w.Add(RouteWeight(k, s, r));
            SetRoute(k, opts[G.Weighted(w)]);
        }

        private static void ReconsiderRoute(Kingdom k, KState s, int era)
        {
            string fresh = era == 11 ? "diesel" : era == 12 ? "atom" : era == 13 ? "bio" : "";
            if (fresh.Length == 0 || s.Route == fresh) return;
            float mine = RouteWeight(k, s, s.Route);
            float other = RouteWeight(k, s, fresh);
            if (G.Chance(0.35f * other / (mine + other))) SetRoute(k, fresh);
        }

        private static void GrantCulture(Kingdom k, string tech)
        {
            foreach (var kv in CultureTraitTech)
            {
                if (kv.Value != tech) continue;
                bool weapon = CultureSync.IsWeapon(kv.Key);
                Culture c = k.getCulture();
                if (c != null && (!weapon || CultureSync.CanAddWeapon(c) && G.Chance(0.5f))) c.addTrait(kv.Key);
                foreach (City city in G.Cities(k))
                {
                    if (city.culture != null && city.culture != c && (!weapon || CultureSync.CanAddWeapon(city.culture) && G.Chance(0.5f))) city.culture.addTrait(kv.Key);
                }
            }
        }

        public static void Year(Kingdom k, int yrs)
        {
            State(k);
            Inherit(k);
            CheckLoss(k, yrs);
            Discoveries(k, yrs);
            Advance(k, yrs);
        }

        private static void Inherit(Kingdom k)
        {
            string me = k.data.id.ToString();
            KState s = State(k);
            var gained = new List<TechDef>();
            bool young = k.getAge() <= 2;
            int myEra = Era(k);
            foreach (City c in G.Cities(k))
            {
                string owner = Store.Str(c.data, KOwner);
                if (owner == me) continue;
                TechSet cityKnown = TechSet.Load(Store.Str(c.data, KBits));
                if (owner.Length > 0 && cityKnown.Count > 0)
                {
                    long prevId;
                    Kingdom prev = null;
                    if (long.TryParse(owner, out prevId)) prev = World.world.kingdoms.get(prevId);
                    bool conquest = prev != null && prev.isAlive() && !young;
                    float chance = 1f;
                    if (conquest)
                    {
                        int libs = c.countBuildingsType("type_library") + c.countBuildingsType("type_civ_university") * 2 + c.countBuildingsType("type_civ_lab") * 2;
                        chance = libs > 0 ? Math.Min(0.6f, 0.15f + 0.1f * libs) : 0.03f;
                    }
                    foreach (TechDef t in cityKnown.All())
                    {
                        if (s.Known.Has(t.Index)) continue;
                        if (conquest && t.Era > myEra + 1) continue;
                        if (Capped(k, t)) continue;
                        if (G.Chance(chance)) gained.Add(t);
                    }
                    if (conquest && gained.Count > 0) Internet.OnTheft(k, prev, gained.Count);
                }
                Store.Put(c.data, KOwner, me);
            }
            if (gained.Count == 0) return;
            foreach (TechDef t in gained) s.Known.Add(t.Index);
            s.Era = -1;
            Save(k, s);
            Bonus.Dirty(k);
            foreach (TechDef t in gained) GrantCulture(k, t.Id);
            if (young && Era(k) >= RouteEra && string.IsNullOrEmpty(s.Route)) ChooseRoute(k, s, Era(k));
        }

        public static void InheritFrom(Kingdom child, Kingdom parent)
        {
            if (!G.Ok(child) || !G.Ok(parent)) return;
            KState ps = State(parent);
            KState cs = State(child);
            cs.Known = new TechSet(ps.Known);
            cs.Found = new TechSet(ps.Found);
            cs.Aff = new Dictionary<string, float>(ps.Aff);
            cs.Era = -1;
            Save(child, cs);
            Store.Put(child.data, KFound, cs.Found.Save());
            Store.Map(child.data, KAff, cs.Aff);
            SetRoute(child, ps.Route);
            Bonus.Dirty(child);
        }

        private static void CheckLoss(Kingdom k, int yrs)
        {
            KState s = State(k);
            if (s.Known.Count == 0) return;
            int archives = 0;
            float power = 0f;
            foreach (City c in G.Cities(k))
            {
                archives += c.countBuildingsType("type_library") + c.countBuildingsType("type_civ_university") + c.countBuildingsType("type_civ_lab");
                power += Roles.ResearchPower(c);
            }
            int last = Store.Int(k.data, KArchive, archives);
            Store.Put(k.data, KArchive, archives);
            if (archives < last && World.world.wars.hasWars(k))
            {
                float p = 0.25f * (last - archives) / Math.Max(1f, last);
                if (archives == 0) p += 0.35f;
                if (G.Chance(p)) DropLatest(k, "archives");
            }
            int dark = Store.Int(k.data, KDark, 0);
            if (power <= 0.01f && archives == 0 && Era(k) >= 3) dark += yrs;
            else dark = Math.Max(0, dark - yrs * 2);
            Store.Put(k.data, KDark, dark);
            if (dark >= 60)
            {
                Store.Put(k.data, KDark, 20);
                if (G.Chance(0.5f)) DropLatest(k, "dark_age");
            }
        }

        private static void DropLatest(Kingdom k, string reason)
        {
            KState s = State(k);
            var needed = new HashSet<string>();
            foreach (TechDef t in s.Known.All()) foreach (string r in t.Req) needed.Add(r);
            TechDef pick = null;
            foreach (TechDef t in s.Known.All())
            {
                if (needed.Contains(t.Id)) continue;
                if (pick == null || t.Era > pick.Era || (t.Era == pick.Era && t.Index > pick.Index)) pick = t;
            }
            if (pick != null) Lose(k, pick, reason);
        }

        private static void Discoveries(Kingdom k, int yrs)
        {
            KState s = State(k);
            int era = Era(k);
            float power = 0f;
            foreach (City c in G.Cities(k)) power += Roles.ResearchPower(c);
            float genius = Mathf.Sqrt(Math.Max(0f, power)) * 0.0015f + Companies.RareResearch(k) * 0.004f;
            if (genius <= 0f) return;
            foreach (TechDef t in Db.Techs)
            {
                if (!t.Rare || t.Era > era + 1 || s.Found.Has(t.Index) || s.Known.Has(t.Index)) continue;
                float p = genius * yrs;
                if (!string.IsNullOrEmpty(t.Route) && t.Route == WorldPrefs.Route) p *= 2f;
                if (t.Era < era) p *= 1.5f;
                if (ContactsKnowing(k, t) > 0) p *= 4f;
                if (G.Chance(p)) Discover(k, t);
            }
        }

        private static void Advance(Kingdom k, int yrs)
        {
            KState s = State(k);
            string cur = Store.Str(k.data, KCur);
            TechDef t = Db.Tech(cur);
            if (t == null || s.Known.Has(t.Index) || !Available(k, s, t, true))
            {
                t = Pick(k, s);
                if (t == null) return;
                Store.Put(k.data, KCur, t.Id);
                Store.Put(k.data, KProg, 0f);
            }
            float rate = Rate(k, t);
            Store.Put(k.data, KRate, rate);
            float pace = CivMod.C != null ? CivMod.C.Pace : 1f;
            float prog = Store.Flt(k.data, KProg, 0f) + rate * yrs * pace;
            float cost = Cost(k, t);
            int guard = 0;
            while (prog >= cost && guard++ < 6)
            {
                prog -= cost;
                Store.Put(k.data, KCur, "");
                Learn(k, t, "research");
                t = Pick(k, s);
                if (t == null)
                {
                    prog = 0f;
                    break;
                }
                Store.Put(k.data, KCur, t.Id);
                cost = Cost(k, t);
            }
            Store.Put(k.data, KProg, prog);
        }

        public static float Cost(Kingdom k, TechDef t)
        {
            float cost = t.Cost;
            int era = Era(k);
            if (t.Era < era) cost *= Math.Max(0.3f, 1f - 0.2f * (era - t.Era));
            int contacts = ContactsKnowing(k, t);
            if (contacts > 0) cost *= contacts >= 3 ? 0.55f : 0.7f;
            return cost;
        }

        public static float Progress(Kingdom k)
        {
            return Store.Flt(k.data, KProg, 0f);
        }

        public static TechDef Current(Kingdom k)
        {
            return Db.Tech(Store.Str(k.data, KCur));
        }

        public static int ContactsKnowing(Kingdom k, TechDef t)
        {
            int n = 0;
            foreach (Kingdom o in Contacts(k))
            {
                KState os = State(o);
                if (os != null && os.Known.Has(t.Index)) n++;
            }
            return n;
        }

        private static readonly Dictionary<long, KeyValuePair<int, HashSet<Kingdom>>> _contacts = new Dictionary<long, KeyValuePair<int, HashSet<Kingdom>>>();

        public static HashSet<Kingdom> Contacts(Kingdom k)
        {
            KeyValuePair<int, HashSet<Kingdom>> cached;
            if (_contacts.TryGetValue(k.data.id, out cached) && cached.Key == G.Year) return cached.Value;
            var set = new HashSet<Kingdom>();
            foreach (City c in G.Cities(k))
            {
                foreach (Kingdom o in c.neighbours_kingdoms)
                {
                    if (o != null && o != k && o.isAlive() && o.isCiv()) set.Add(o);
                }
            }
            foreach (Kingdom o in Economy.TradePartners(k))
            {
                if (o != null && o != k && o.isAlive()) set.Add(o);
            }
            if (Internet.Exists(k))
            {
                foreach (Kingdom o in G.Kingdoms()) if (o != k && Internet.Exists(o)) set.Add(o);
            }
            _contacts[k.data.id] = new KeyValuePair<int, HashSet<Kingdom>>(G.Year, set);
            return set;
        }

        private static TechDef Pick(Kingdom k, KState s)
        {
            var options = new List<TechDef>();
            var weights = new List<float>();
            Dictionary<string, float> focus = Roles.SpecialtyShare(k);
            string govFocus = Politics.Focus(k);
            string clanFocus = Clans.FocusArea(k);
            int era = Era(k);
            bool war = World.world.wars.hasWars(k);
            bool famine = false;
            foreach (City c in G.Cities(k)) if (Economy.IsFamine(c)) famine = true;
            var needed = new HashSet<int>();
            for (int e = 0; e <= Math.Min(Db.LastEra, era); e++)
            {
                if (e >= Fundamentals.Length) break;
                foreach (string id in Fundamentals[e]) Needed(s, Db.Tech(id), needed, 0);
            }
            var avail = new List<TechDef>();
            for (int e = 0; e <= Math.Min(Db.LastEra, era + 1); e++)
            {
                foreach (TechDef t in Db.Eras[e].Techs) if (Available(k, s, t)) avail.Add(t);
            }
            float refCost = 1f;
            if (avail.Count > 0)
            {
                var costs = new List<float>();
                foreach (TechDef t in avail) costs.Add(Cost(k, t));
                costs.Sort();
                refCost = Math.Max(0.01f, costs[costs.Count / 2]);
            }
            foreach (TechDef t in avail)
            {
                {
                    float w = t.IsCore ? 3f : (t.Rare ? 4f : 1f);
                    if (t.Era < era && !t.Rare) w *= 3f + (era - t.Era);
                    if (needed.Contains(t.Index)) w *= 6f;
                    w *= Mathf.Clamp(Mathf.Sqrt(refCost / Math.Max(0.01f, Cost(k, t))), 0.5f, 3f);
                    if (t.Era == era) w *= 1.6f;
                    else if (t.Era < era - 1 && t.IsCore) w *= 2.2f;
                    else if (t.Era > era) w *= 0.7f;
                    if (!string.IsNullOrEmpty(t.Route) && t.Route != s.Route && !CountsForEra(s, t)) w *= 0.12f;
                    w += Store.Get(focus, t.Cat) * 2f;
                    if (t.Cat == govFocus) w += 0.8f;
                    if (t.Cat == clanFocus) w += 0.5f;
                    if (war && (t.Cat == "mil" || t.Cat == "naval" || t.Cat == "air")) w += 1.5f;
                    if (famine && t.Cat == "agri") w += 2f;
                    if (ContactsKnowing(k, t) > 0) w += 1f;
                    options.Add(t);
                    weights.Add(w);
                }
            }
            if (options.Count == 0) return null;
            return options[G.Weighted(weights)];
        }

        public static float Rate(Kingdom k, TechDef t)
        {
            int era = Era(k);
            float power = 0f;
            int libs = 0, unis = 0, labs = 0, schools = 0;
            float famine = 0f;
            int cities = 0;
            foreach (City c in G.Cities(k))
            {
                cities++;
                power += Roles.ResearchPower(c);
                libs += c.countBuildingsType("type_library");
                unis += c.countBuildingsType("type_civ_university");
                labs += c.countBuildingsType("type_civ_lab");
                schools += c.countBuildingsType("type_civ_school");
                if (Economy.IsFamine(c)) famine += 1f;
            }
            float expected = 1f + era * 0.6f;
            float res = power <= 0.01f ? 0.1f : G.Clamp(Mathf.Sqrt(power / expected), 0.15f, 2.2f);
            float b = 1f + 0.08f * Math.Min(libs, 5) + 0.12f * Math.Min(unis, 3) + 0.15f * Math.Min(labs, 3) + 0.05f * Math.Min(schools, 4);
            float eb = 1f + (era >= 3 ? 0.16f : 0f) + (era >= 7 ? 0.24f : 0f) + (era >= 10 ? 0.3f : 0f);
            float rb = G.Clamp(b / eb, 0.6f, 1.6f);
            float kn = 1f;
            Culture cul = k.getCulture();
            if (cul != null)
            {
                int n = 0;
                foreach (string id in KnowledgeTraits) if (cul.hasTrait(id)) n++;
                kn += Math.Min(0.24f, n * 0.06f);
            }
            float bonus = Bonus.Mult(k, "research");
            float eraNorm = 1f / Bonus.Expected(era, "research");
            float stab = 0.7f + 0.5f * Politics.Stability(k);
            float clan = 1f + Clans.ResearchBonus(k);
            float spec = 1f;
            if (t != null)
            {
                Dictionary<string, float> share = Roles.SpecialtyShare(k);
                spec += Math.Min(0.25f, Store.Get(share, t.Cat) * 0.5f);
            }
            float war = World.world.wars.hasWars(k) ? 0.85f : 1f;
            float hunger = cities > 0 ? 1f - 0.4f * (famine / cities) : 1f;
            float comp = 1f + Companies.ResearchBoost(k);
            return G.Clamp(res * rb * kn * bonus * eraNorm * stab * clan * spec * war * hunger * comp, 0.02f, 5f);
        }

        public static void SetupEra(Kingdom k, int era)
        {
            KState s = State(k);
            var set = new TechSet(s.Known);
            foreach (TechDef t in Db.Techs)
            {
                if (t.Era < era && !t.Rare && (string.IsNullOrEmpty(t.Route) || t.Route == "real") && t.Level <= 2) set.Add(t.Index);
            }
            SetAll(k, set);
        }
    }
}
