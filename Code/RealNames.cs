using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace Civilizations
{
    internal class NameCulture
    {
        public string Id;
        public string[] M, F, MM, FM, Sur, Epi, Syl, CityPre, CitySuf, CityRoot, CountrySuf, ArchM, ArchF;
        public string EpiFmt, Patr, Topo;
        public bool Compound, DoubleSur, SurFirst;
    }

    internal static class RealNames
    {
        public const string KSur = "civ_sur";
        public const string KGiven = "civ_given";
        public const string KRoot = "civ_kroot";
        public const string KSet = "civ_kname";
        public const string KLock = "civ_kname_lock";
        private static List<NameCulture> _list;
        private static bool _busy;

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.RealNames; }
        }

        private static List<NameCulture> List
        {
            get
            {
                if (_list == null) Load();
                return _list;
            }
        }

        private static string[] Arr(JToken t)
        {
            if (t == null || t.Type != JTokenType.Array) return new string[0];
            var r = new List<string>();
            foreach (JToken x in t) r.Add((string)x);
            return r.ToArray();
        }

        private static void Load()
        {
            _list = new List<NameCulture>();
            try
            {
                JObject o = JObject.Parse(File.ReadAllText(Path.Combine(Path.Combine(CivMod.Folder, "Data"), "names.json")));
                JObject cs = (JObject)o["cultures"];
                foreach (JToken id in o["order"])
                {
                    JObject c = (JObject)cs[(string)id];
                    if (c == null) continue;
                    JObject a = c["archaic"] as JObject;
                    _list.Add(new NameCulture
                    {
                        Id = (string)id,
                        M = Arr(c["m"]),
                        F = Arr(c["f"]),
                        MM = Arr(c["mm"]),
                        FM = Arr(c["fm"]),
                        Sur = Arr(c["sur"]),
                        Epi = Arr(c["epi"]),
                        Syl = Arr(c["syl"]),
                        CityPre = Arr(c["city_pre"]),
                        CitySuf = Arr(c["city_suf"]),
                        CityRoot = Arr(c["city_root"]),
                        CountrySuf = Arr(c["country_suf"]),
                        ArchM = a == null ? new string[0] : Arr(a["m"]),
                        ArchF = a == null ? new string[0] : Arr(a["f"]),
                        EpiFmt = (string)c["epi_fmt"] ?? "{0} {1}",
                        Patr = (string)c["patr"] ?? "{0} {1}",
                        Topo = (string)c["topo"] ?? "{0} {1}",
                        Compound = c["compound"] != null && (bool)c["compound"],
                        DoubleSur = c["double_sur"] != null && (bool)c["double_sur"],
                        SurFirst = c["sur_first"] != null && (bool)c["sur_first"]
                    });
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Civilizations] names: " + e.Message);
            }
        }

        private static long Hash(string s)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                foreach (char ch in s) h = (h ^ ch) * 1099511628211L;
                return h & long.MaxValue;
            }
        }

        public static NameCulture Of(Kingdom k)
        {
            if (List.Count == 0) return null;
            string key = "";
            if (k != null)
            {
                if (k.language != null && k.language.data != null) key = "l" + k.language.data.id;
                else if (k.culture != null && k.culture.data != null) key = "c" + k.culture.data.id;
                else if (k.data != null) key = "k" + k.data.id;
            }
            return List[(int)(Hash(key) % List.Count)];
        }

        public static NameCulture Of(Actor a)
        {
            if (List.Count == 0) return null;
            Kingdom k = Home(a);
            if (k != null) return Of(k);
            string key = "";
            if (a != null)
            {
                if (a.language != null && a.language.data != null) key = "l" + a.language.data.id;
                else if (a.culture != null && a.culture.data != null) key = "c" + a.culture.data.id;
                else if (a.asset != null) key = "s" + a.asset.id;
            }
            return List[(int)(Hash(key) % List.Count)];
        }

        public static string GroupLabel(Kingdom k)
        {
            NameCulture c = Of(k);
            return c == null ? "" : Txt.T("civ_ng_" + c.Id);
        }

        private static Kingdom Home(Actor a)
        {
            if (a == null) return null;
            Kingdom k = a.kingdom;
            if ((k == null || !k.isCiv()) && a.city != null) k = a.city.kingdom;
            return k != null && k.isCiv() ? k : null;
        }

        private static int EraOf(Actor a)
        {
            Kingdom k = Home(a);
            if (k != null) return Research.Era(k);
            foreach (Actor p in a.getParents())
            {
                Kingdom pk = Home(p);
                if (pk != null) return Research.Era(pk);
            }
            return 0;
        }

        private static string P(Random r, string[] arr, string def)
        {
            return arr == null || arr.Length == 0 ? def : arr[r.Next(arr.Length)];
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string Syl(Random r, NameCulture c, int min, int max)
        {
            var sb = new StringBuilder();
            int n = r.Next(min, max + 1);
            for (int i = 0; i < n; i++) sb.Append(P(r, c.Syl, "ka"));
            string s = sb.ToString().Replace(" ", "");
            if (s.Length > 10) s = s.Substring(0, 10);
            return Cap(s);
        }

        private static string Given(Random r, NameCulture c, bool fem, int era)
        {
            if (era <= 2) return Syl(r, c, 2, 3);
            if (era <= 6)
            {
                string[] arch = fem ? c.ArchF : c.ArchM;
                if (arch.Length > 0 && r.NextDouble() < 0.8) return P(r, arch, Syl(r, c, 2, 3));
                return Syl(r, c, 2, 3);
            }
            string[] old = fem ? c.F : c.M;
            if (era <= 10) return P(r, old, Syl(r, c, 2, 3));
            string[] neu = fem ? c.FM : c.MM;
            string g = r.NextDouble() < 0.6 ? P(r, neu, P(r, old, "")) : P(r, old, "");
            if (c.Compound && r.NextDouble() < 0.25)
            {
                string g2 = P(r, r.NextDouble() < 0.5 ? old : neu, g);
                if (g2 != g) g = g + " " + g2;
            }
            return g.Length == 0 ? Syl(r, c, 2, 3) : g;
        }

        private static string Join(NameCulture c, string given, string sur)
        {
            if (string.IsNullOrEmpty(sur)) return given;
            return c.SurFirst ? sur + " " + given : given + " " + sur;
        }

        private static string FirstWord(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string w = s.Trim().Split(' ')[0];
            var sb = new StringBuilder();
            foreach (char ch in w) if (char.IsLetter(ch) || ch == '-' || ch == '\'') sb.Append(ch);
            return sb.ToString();
        }

        private static string InheritedSurname(Actor a, Random r, NameCulture c, int era, out string mother)
        {
            mother = "";
            string father = "";
            foreach (Actor p in a.getParents())
            {
                if (p == null || p.data == null) continue;
                string s = Store.Str(p.data, KSur);
                if (s.Length == 0) continue;
                if (p.data.sex == ActorSex.Female) mother = s;
                else if (father.Length == 0) father = s;
            }
            if (father.Length == 0 && mother.Length > 0)
            {
                father = mother;
                mother = "";
            }
            if (father.Length > 0) return father;
            if (a.clan != null && a.clan.data != null && era >= 7 && era <= 9 && r.NextDouble() < 0.5)
            {
                string cl = FirstWord(a.clan.data.name);
                if (cl.Length > 1) return cl;
            }
            return P(r, c.Sur, Syl(r, c, 2, 3));
        }

        private static string Unit(Actor a, long seed, ActorSex sex)
        {
            NameCulture c = Of(a);
            if (c == null) return null;
            var r = new Random((int)(seed ^ (seed >> 32)));
            int era = EraOf(a);
            bool fem = sex == ActorSex.Female;
            string given = Given(r, c, fem, era);
            string mother;
            string sur = InheritedSurname(a, r, c, era, out mother);
            Store.Put(a.data, KSur, sur);
            Store.Put(a.data, KGiven, given);
            if (era <= 4) return given;
            if (era <= 6)
            {
                if (!fem && r.NextDouble() < 0.2) return string.Format(c.EpiFmt, given, P(r, c.Epi, ""));
                return given;
            }
            if (era == 7)
            {
                double x = r.NextDouble();
                if (x < 0.35)
                {
                    string fg = "";
                    foreach (Actor p in a.getParents())
                    {
                        if (p == null || p.data == null || p.data.sex == ActorSex.Female) continue;
                        fg = FirstWord(Store.Str(p.data, KGiven));
                        if (fg.Length == 0) fg = FirstWord(p.getName());
                        break;
                    }
                    if (fg.Length > 0) return string.Format(c.Patr, given, fg);
                }
                else if (x < 0.6 && a.city != null && a.city.data != null)
                {
                    return string.Format(c.Topo, given, FirstWord(a.city.data.name));
                }
                else if (x < 0.8 && !fem)
                {
                    return string.Format(c.EpiFmt, given, P(r, c.Epi, ""));
                }
                else if (x < 1.0 && a.clan != null && a.clan.data != null)
                {
                    string cl = FirstWord(a.clan.data.name);
                    if (cl.Length > 1) return string.Format(c.Topo, given, cl);
                }
                return given;
            }
            if (era >= 11 && c.DoubleSur && mother.Length > 0 && mother != sur && r.NextDouble() < 0.6)
            {
                return Join(c, given, mother + " " + sur);
            }
            return Join(c, given, sur);
        }

        public static string Person(Actor a)
        {
            if (!On || a == null || a.data == null) return null;
            long seed = a.data.id * 2654435761L + G.Year;
            return Unit(a, seed, a.data.sex);
        }

        private static string City(Actor a, long seed)
        {
            NameCulture c = Of(a);
            if (c == null) return null;
            var r = new Random((int)(seed ^ (seed >> 32)) ^ 0x5bd1e995);
            int era = EraOf(a);
            if (era <= 2) return Syl(r, c, 2, 3);
            double x = r.NextDouble();
            if (era <= 6)
            {
                if (x < 0.5) return P(r, c.CityRoot, Syl(r, c, 2, 3));
                return Syl(r, c, 1, 2) + P(r, c.CitySuf, "");
            }
            if (x < 0.3) return P(r, c.CityRoot, "Nova");
            if (x < 0.55) return P(r, c.CityPre, "") + " " + P(r, c.CityRoot, Syl(r, c, 2, 2));
            if (x < 0.8) return FirstWord(P(r, c.CityRoot, Syl(r, c, 2, 2))) + P(r, c.CitySuf, "");
            string founder = FirstWord(Store.Str(a.data, KGiven));
            if (founder.Length == 0) founder = FirstWord(a.getName());
            return P(r, c.CityPre, "") + " " + founder;
        }

        private static string KingdomRoot(Actor a, long seed)
        {
            NameCulture c = Of(a);
            if (c == null) return null;
            var r = new Random((int)(seed ^ (seed >> 32)) ^ 0x2f6b13a7);
            int era = EraOf(a);
            if (era <= 2 || r.NextDouble() < 0.35)
            {
                string s = Syl(r, c, 2, 3);
                return era <= 4 ? s : s + P(r, c.CountrySuf, "");
            }
            string root = FirstWord(P(r, c.CityRoot, Syl(r, c, 2, 2)));
            if (era >= 5 && r.NextDouble() < 0.5)
            {
                string suf = P(r, c.CountrySuf, "");
                string stem = root;
                if (stem.Length > 4 && "aeiouáéíóú".IndexOf(char.ToLowerInvariant(stem[stem.Length - 1])) >= 0) stem = stem.Substring(0, stem.Length - 1);
                return stem + suf;
            }
            return root;
        }

        private static string Family(Actor a, long seed)
        {
            NameCulture c = Of(a);
            if (c == null) return null;
            string sur = Store.Str(a.data, KSur);
            if (sur.Length == 0)
            {
                var r = new Random((int)(seed ^ (seed >> 32)) ^ 0x1b873593);
                string mother;
                sur = InheritedSurname(a, r, c, EraOf(a), out mother);
                Store.Put(a.data, KSur, sur);
            }
            return sur;
        }

        public static string Title(Kingdom k)
        {
            string stage = Politics.Stage(k);
            string gov = Policies.Id(k, "gov");
            int cities = k.countCities();
            switch (gov)
            {
                case "tribal_band":
                case "tribal_council":
                    return "civ_kt_tribe";
                case "chiefdom":
                    return "civ_kt_chiefdom";
                case "theocracy":
                    return "civ_kt_theocracy";
                case "military_junta":
                    return "civ_kt_junta";
                case "single_party_state":
                    return "civ_kt_people";
                case "technocracy":
                    return "civ_kt_techno";
                case "corporatocracy":
                    return "civ_kt_corp";
                case "merchant_republic":
                    return "civ_kt_merchant";
                case "republic":
                case "parliamentary_republic":
                case "democracy":
                    return stage == "empire" ? "civ_kt_federal" : "civ_kt_republic";
                case "presidential_republic":
                    return stage == "nation" && cities >= 6 ? "civ_kt_united" : "civ_kt_republic";
            }
            if (stage == "tribe") return "civ_kt_tribe";
            if (stage == "village" || stage == "town") return "civ_kt_plain";
            if (stage == "city_state") return "civ_kt_citystate";
            if (stage == "empire") return "civ_kt_empire";
            if (gov == "constitutional_monarchy" && stage == "nation" && cities >= 6) return "civ_kt_ukingdom";
            return "civ_kt_kingdom";
        }

        public static void KingdomYear(Kingdom k)
        {
            if (!On || !G.Ok(k) || k.data == null) return;
            if (Store.Int(k.data, KLock, 0) == 1) return;
            string root = Store.Str(k.data, KRoot);
            if (root.Length == 0) return;
            string set = Store.Str(k.data, KSet);
            if (set.Length > 0 && k.data.name != set)
            {
                Store.Put(k.data, KLock, 1);
                return;
            }
            string name = Txt.F(Title(k), root);
            if (name == k.data.name) return;
            Store.Put(k.data, KSet, name);
            _busy = true;
            try
            {
                k.setName(name);
            }
            finally
            {
                _busy = false;
            }
        }

        [HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateName), new[] { typeof(Actor), typeof(MetaType), typeof(long), typeof(ActorSex) })]
        private static class Gen
        {
            private static void Postfix(Actor pActor, MetaType pType, long pSeed, ActorSex pSex, ref string __result)
            {
                if (_busy || !On || pActor == null || pActor.data == null || !pActor.isSapient()) return;
                try
                {
                    string n = null;
                    switch (pType)
                    {
                        case MetaType.Unit:
                            n = Unit(pActor, pSeed, pSex);
                            break;
                        case MetaType.City:
                            n = City(pActor, pSeed);
                            break;
                        case MetaType.Kingdom:
                            n = KingdomRoot(pActor, pSeed);
                            break;
                        case MetaType.Family:
                            n = Family(pActor, pSeed);
                            break;
                    }
                    if (!string.IsNullOrEmpty(n)) __result = n.Trim();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning("[Civilizations] names: " + e.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Kingdom), "newCivKingdom")]
        private static class NewKingdom
        {
            private static void Postfix(Kingdom __instance)
            {
                if (!On || __instance == null || __instance.data == null) return;
                Store.Put(__instance.data, KRoot, __instance.data.name ?? "");
                Store.Put(__instance.data, KSet, __instance.data.name ?? "");
            }
        }
    }
}
