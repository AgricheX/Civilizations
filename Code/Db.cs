using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Civilizations
{
    internal class EraDef
    {
        public int Index;
        public string Id;
        public float Years;
        public string Color;
        public readonly List<TechDef> Techs = new List<TechDef>();
        public readonly List<TechDef> Core = new List<TechDef>();
    }

    internal class TechDef
    {
        public int Index;
        public string Id;
        public int Era;
        public string Cat;
        public string Route;
        public bool Rare;
        public int Level;
        public string Base;
        public float Cost;
        public string[] Req;
        public Dictionary<string, float> Eff = new Dictionary<string, float>();
        public string[] Unl;
        public string Icon;
        public bool IsCore { get { return Level == 1 && !Rare; } }
    }

    internal class PolicyDef
    {
        public string Id;
        public string Group;
        public string Req;
        public Dictionary<string, float> Eff = new Dictionary<string, float>();
        public string Succession = "";
        public bool Elections;
        public int Stage;
    }

    internal class FormationDef
    {
        public string Id;
        public string Req;
        public string Shape;
        public int Spacing;
    }

    internal class UnitDef
    {
        public string Id;
        public string Cls;
        public string Role = "";
        public string Route = "";
        public string Fuel = "";
        public int Crew;
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        public string Art;
        public string Tech;
        public int Era;
        public string Base;
    }

    internal class TrainDef
    {
        public string Id;
        public string Route;
        public string Fuel;
        public float Speed;
        public string Art;
        public string Tech;
        public int Era;
    }

    internal class ItemDef
    {
        public string Id;
        public string Kind;
        public string Route;
        public float Warmth;
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        public string Proj = "";
        public string Art;
        public string Tech;
        public int Era;
    }

    internal class BuildingDef
    {
        public string Id;
        public string Tech;
        public string Route;
        public int Size;
        public string Art;
    }

    internal class PlantDef
    {
        public string Id;
        public string Tech;
        public string Route;
        public string Fuel;
        public float Output;
        public float Pollution;
        public string Art;
    }

    internal class LevelDef
    {
        public int Level;
        public string Tech;
        public int MinPop;
    }

    internal static class Db
    {
        public static readonly List<EraDef> Eras = new List<EraDef>();
        public static readonly List<TechDef> Techs = new List<TechDef>();
        public static readonly Dictionary<string, TechDef> TechById = new Dictionary<string, TechDef>();
        public static readonly List<string> Routes = new List<string>();
        public static readonly Dictionary<string, string> RouteColors = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> CatColors = new Dictionary<string, string>();
        public static readonly List<string> Cats = new List<string>();
        public static readonly Dictionary<string, List<TechDef>> Unlocks = new Dictionary<string, List<TechDef>>();
        public static readonly List<PolicyDef> Policies = new List<PolicyDef>();
        public static readonly Dictionary<string, PolicyDef> PolicyById = new Dictionary<string, PolicyDef>();
        public static readonly List<FormationDef> Formations = new List<FormationDef>();
        public static readonly List<UnitDef> Units = new List<UnitDef>();
        public static readonly Dictionary<string, UnitDef> UnitById = new Dictionary<string, UnitDef>();
        public static readonly List<TrainDef> Trains = new List<TrainDef>();
        public static readonly List<ItemDef> Items = new List<ItemDef>();
        public static readonly Dictionary<string, ItemDef> ItemById = new Dictionary<string, ItemDef>();
        public static readonly List<string> Resources = new List<string>();
        public static readonly Dictionary<string, string> ResourceArt = new Dictionary<string, string>();
        public static readonly List<BuildingDef> Buildings = new List<BuildingDef>();
        public static readonly Dictionary<string, BuildingDef> BuildingById = new Dictionary<string, BuildingDef>();
        public static readonly List<LevelDef> Houses = new List<LevelDef>();
        public static readonly List<LevelDef> Halls = new List<LevelDef>();
        public static readonly List<PlantDef> Plants = new List<PlantDef>();
        public static readonly List<KeyValuePair<string, string>> Sectors = new List<KeyValuePair<string, string>>();

        public static int LastEra { get { return Eras.Count - 1; } }

        public static void Init()
        {
            string dir = Path.Combine(CivMod.Folder, "Data");
            LoadTechs(JObject.Parse(File.ReadAllText(Path.Combine(dir, "techs.json"))));
            LoadCatalog(JObject.Parse(File.ReadAllText(Path.Combine(dir, "catalog.json"))));
        }

        private static Dictionary<string, float> Map(JToken t)
        {
            var m = new Dictionary<string, float>();
            if (t == null || t.Type != JTokenType.Object) return m;
            foreach (JProperty p in ((JObject)t).Properties()) m[p.Name] = p.Value.Value<float>();
            return m;
        }

        private static string[] Arr(JToken t)
        {
            if (t == null || t.Type != JTokenType.Array) return new string[0];
            var l = new List<string>();
            foreach (JToken x in (JArray)t) l.Add(x.Value<string>());
            return l.ToArray();
        }

        private static string S(JToken o, string k)
        {
            JToken v = o[k];
            return v == null || v.Type == JTokenType.Null ? "" : v.Value<string>();
        }

        private static void LoadTechs(JObject root)
        {
            Eras.Clear();
            Techs.Clear();
            TechById.Clear();
            Routes.Clear();
            Cats.Clear();
            Unlocks.Clear();
            foreach (JToken e in (JArray)root["eras"])
            {
                Eras.Add(new EraDef { Index = Eras.Count, Id = S(e, "id"), Years = e["years"].Value<float>(), Color = S(e, "color") });
            }
            foreach (JToken r in (JArray)root["routes"])
            {
                Routes.Add(S(r, "id"));
                RouteColors[S(r, "id")] = S(r, "color");
            }
            foreach (JToken c in (JArray)root["cats"])
            {
                Cats.Add(S(c, "id"));
                CatColors[S(c, "id")] = S(c, "color");
            }
            foreach (JToken t in (JArray)root["techs"])
            {
                var d = new TechDef
                {
                    Index = Techs.Count,
                    Id = S(t, "id"),
                    Era = t["era"].Value<int>(),
                    Cat = S(t, "cat"),
                    Route = S(t, "route"),
                    Rare = t["rare"].Value<bool>(),
                    Level = t["lvl"].Value<int>(),
                    Base = S(t, "base"),
                    Cost = t["cost"].Value<float>(),
                    Req = Arr(t["req"]),
                    Eff = Map(t["eff"]),
                    Unl = Arr(t["unl"]),
                    Icon = "ui/Icons/" + S(t, "icon")
                };
                Techs.Add(d);
                TechById[d.Id] = d;
                Eras[d.Era].Techs.Add(d);
                if (d.IsCore) Eras[d.Era].Core.Add(d);
                foreach (string u in d.Unl)
                {
                    List<TechDef> l;
                    if (!Unlocks.TryGetValue(u, out l)) Unlocks[u] = l = new List<TechDef>();
                    l.Add(d);
                }
            }
        }

        private static void LoadCatalog(JObject root)
        {
            Policies.Clear();
            PolicyById.Clear();
            foreach (JToken p in (JArray)root["policies"])
            {
                var d = new PolicyDef { Id = S(p, "id"), Group = S(p, "group"), Req = S(p, "req"), Eff = Map(p["eff"]) };
                if (p["succession"] != null) d.Succession = S(p, "succession");
                if (p["elections"] != null) d.Elections = p["elections"].Value<bool>();
                if (p["stage"] != null) d.Stage = p["stage"].Value<int>();
                Policies.Add(d);
                PolicyById[d.Id] = d;
            }
            Formations.Clear();
            foreach (JToken f in (JArray)root["formations"])
            {
                Formations.Add(new FormationDef { Id = S(f, "id"), Req = S(f, "req"), Shape = S(f, "shape"), Spacing = f["spacing"].Value<int>() });
            }
            Units.Clear();
            UnitById.Clear();
            foreach (JToken u in (JArray)root["units"])
            {
                var d = new UnitDef { Id = S(u, "id"), Cls = S(u, "cls"), Role = S(u, "role"), Route = S(u, "route"), Fuel = S(u, "fuel"), Crew = u["crew"] != null ? u["crew"].Value<int>() : 0, Stats = Map(u["stats"]), Art = S(u, "art"), Tech = S(u, "tech"), Base = S(u, "base") };
                if (string.IsNullOrEmpty(d.Base)) d.Base = d.Id;
                d.Era = EraOf(d.Tech);
                Units.Add(d);
                UnitById[d.Id] = d;
            }
            Trains.Clear();
            foreach (JToken t in (JArray)root["trains"])
            {
                var d = new TrainDef { Id = S(t, "id"), Route = S(t, "route"), Fuel = S(t, "fuel"), Speed = t["speed"].Value<float>(), Art = S(t, "art"), Tech = S(t, "tech") };
                d.Era = EraOf(d.Tech);
                Trains.Add(d);
            }
            Items.Clear();
            ItemById.Clear();
            foreach (JToken i in (JArray)root["items"])
            {
                var d = new ItemDef { Id = S(i, "id"), Kind = S(i, "kind"), Route = S(i, "route"), Stats = Map(i["stats"]), Proj = S(i, "proj"), Art = S(i, "art"), Tech = S(i, "tech") };
                if (i["warmth"] != null) d.Warmth = i["warmth"].Value<float>();
                d.Era = EraOf(d.Tech);
                Items.Add(d);
                ItemById[d.Id] = d;
            }
            Resources.Clear();
            foreach (JToken r in (JArray)root["resources"])
            {
                Resources.Add(S(r, "id"));
                ResourceArt[S(r, "id")] = S(r, "art");
            }
            Buildings.Clear();
            BuildingById.Clear();
            foreach (JToken b in (JArray)root["buildings"])
            {
                var d = new BuildingDef { Id = S(b, "id"), Tech = S(b, "tech"), Route = S(b, "route"), Size = b["size"].Value<int>(), Art = S(b, "art") };
                Buildings.Add(d);
                BuildingById[d.Id] = d;
            }
            Houses.Clear();
            foreach (JToken h in (JArray)root["houses"]) Houses.Add(new LevelDef { Level = h["level"].Value<int>(), Tech = S(h, "tech"), MinPop = h["minpop"].Value<int>() });
            Halls.Clear();
            foreach (JToken h in (JArray)root["halls"]) Halls.Add(new LevelDef { Level = h["level"].Value<int>(), Tech = S(h, "tech") });
            Plants.Clear();
            foreach (JToken p in (JArray)root["plants"]) Plants.Add(new PlantDef { Id = S(p, "id"), Tech = S(p, "tech"), Route = S(p, "route"), Fuel = S(p, "fuel"), Output = p["output"].Value<float>(), Pollution = p["pollution"].Value<float>(), Art = S(p, "art") });
            Sectors.Clear();
            foreach (JToken s in (JArray)root["sectors"]) Sectors.Add(new KeyValuePair<string, string>(S(s, "id"), S(s, "tech")));
        }

        public static int EraOf(string tech)
        {
            TechDef t = Tech(tech);
            return t == null ? 0 : t.Era;
        }

        public static TechDef Tech(string id)
        {
            TechDef t;
            return !string.IsNullOrEmpty(id) && TechById.TryGetValue(id, out t) ? t : null;
        }

        public static PolicyDef Policy(string id)
        {
            PolicyDef p;
            return !string.IsNullOrEmpty(id) && PolicyById.TryGetValue(id, out p) ? p : null;
        }

        public static List<TechDef> UnlockedBy(string key)
        {
            List<TechDef> l;
            return Unlocks.TryGetValue(key, out l) ? l : null;
        }

        public static List<PolicyDef> Group(string group)
        {
            var l = new List<PolicyDef>();
            foreach (PolicyDef p in Policies) if (p.Group == group) l.Add(p);
            return l;
        }

        public static string EraName(int era)
        {
            if (era < 0) return Txt.T("civ_era_none");
            if (era > LastEra) era = LastEra;
            return Txt.T("civ_era_" + Eras[era].Id);
        }

        public static string TechName(string id)
        {
            return Txt.T("civ_t_" + id);
        }
    }
}
