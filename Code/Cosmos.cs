using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class Polity
    {
        public long Id;
        public string Name = "";
        public long King;
        public long Parent;
        public string Species = "";
        public int Era;
        public string Gov = "";
        public string Ideo = "";
        public string Econ = "";
        public float Pop;
        public float Loyalty = 1f;
        public string Color = "#FFFFFF";
        public int Founded;
        public string Kind = "colony";
        public string Seat = "";
        public List<string> Nodes = new List<string>();
        public List<long> Met = new List<long>();
        public float Fleet;
        public float Tech;
        public string Stance = "";
        public int Grace;
        public float Pace = 1f;
        public Dictionary<long, float> Opinion = new Dictionary<long, float>();
        public long At;
    }

    internal class Battle
    {
        public string Node = "";
        public int Year;
        public string A = "";
        public string B = "";
        public string Win = "";
    }

    internal class Threat
    {
        public long King;
        public string Kind = "";
        public string Target = "";
        public int Warn;
        public int Scout;
        public int Main;
        public float Power;
        public bool Warned;
        public bool Scouted;
    }

    internal class Voyage
    {
        public long King;
        public long Polity;
        public string From = "";
        public string To = "";
        public int Depart;
        public int Arrive;
        public string Kind = "colony";
        public float Power;
    }

    internal class Mega
    {
        public string Id = "";
        public string Node = "";
        public long King;
        public long Polity;
        public float Progress;
        public bool Done;
        public int Started;
        public bool Ruin;
        public int Stage;
    }

    internal class CosState
    {
        public int Ver = 1;
        public string Origin = "random";
        public int Seed;
        public string Home = "";
        public int HomeBodies;
        public int Clock;
        public int Fermi = 1;
        public float Time = 1f;
        public bool Invasions = true;
        public bool Ending;
        public long NextId = 1;
        public int Year;
        public List<Polity> Polities = new List<Polity>();
        public List<Voyage> Voyages = new List<Voyage>();
        public List<Mega> Megas = new List<Mega>();
        public Dictionary<string, string> Firsts = new Dictionary<string, string>();
        public List<string> Revealed = new List<string>();
        public List<string> Log = new List<string>();
        public bool Ended;
        public List<Battle> Battles = new List<Battle>();
        public List<Threat> Threats = new List<Threat>();
        public List<string> Seeded = new List<string>();
        public List<string> Ruins = new List<string>();
        public List<string> Dead = new List<string>();
        public List<long> Council = new List<long>();
        public List<string> Legends = new List<string>();
    }

    internal static class Cosmos
    {
        private const string KState = "civ_cosmos";
        public const string KScale = "civ_cos_scale";
        public static CosState St;
        private static readonly Dictionary<string, Polity> _owner = new Dictionary<string, Polity>();
        private static bool _ownersDirty = true;

        public static bool Ready { get { return St != null; } }
        public static int Tag;
        public static bool Shared;
        private const int Shift = 40;
        private const long Mask = (1L << 40) - 1;

        public static int Now { get { return Shared && St != null ? St.Clock : G.Year; } }

        public static long Key(Kingdom k)
        {
            return G.Ok(k) ? ((long)Tag << Shift) | (k.data.id & Mask) : 0;
        }

        public static long KeyOf(int tag, long id)
        {
            return ((long)tag << Shift) | (id & Mask);
        }

        public static int TagOf(long key)
        {
            return (int)(key >> Shift);
        }

        public static long IdOf(long key)
        {
            return key & Mask;
        }

        public static bool Remote(long key)
        {
            return key != 0 && TagOf(key) != Tag;
        }

        public static Kingdom KingOf(long key)
        {
            if (key == 0 || TagOf(key) != Tag || World.world == null) return null;
            return World.world.kingdoms.get(key & Mask);
        }

        public static bool Foreign(Polity p)
        {
            return p != null && (p.Kind == "alien" || p.Kind == "invader" || p.Kind == "world" || Remote(p.King));
        }

        public static void Clear()
        {
            St = null;
            _owner.Clear();
            _ownersDirty = true;
        }

        public static void Load()
        {
            Clear();
            CosState kept;
            if (CosmosPlanet.TakeKept(out kept))
            {
                St = kept;
                if (Shared) Universe.Regen();
                else CosmosGen.Setup(St.Seed, St.Home, St.HomeBodies);
                return;
            }
            CosmosPlanet.Abandon();
            Tag = 0;
            Shared = false;
            SaveCustomData d = Store.World();
            if (d == null) return;
            if (Universe.On && Universe.Load(d)) return;
            string s = Store.Str(d, KState);
            if (s.Length > 0)
            {
                try
                {
                    St = JsonConvert.DeserializeObject<CosState>(s);
                }
                catch (Exception e)
                {
                    CivMod.Report("cosmos-load", e);
                    St = null;
                }
            }
            if (St == null) Create(d);
            Migrate();
            Localize(Store.Int(d, Universe.KTag, 0));
            CosmosGen.Setup(St.Seed, St.Home, St.HomeBodies);
            St.Home = CosmosGen.HomePath;
        }

        private static void Create(SaveCustomData d)
        {
            St = new CosState();
            St.Origin = "random";
            St.Seed = NewSeed();
            St.Fermi = CivMod.C != null ? CivMod.C.Fermi : 1;
            St.Time = 1f;
            St.Invasions = CivMod.C == null || CivMod.C.CosmicInvasions;
            St.Ending = CivMod.C != null && CivMod.C.CosmicEnding;
            St.Year = G.Year;
        }

        public static int NewSeed()
        {
            string txt = CivMod.C != null ? CivMod.C.CosSeed : "";
            if (string.IsNullOrEmpty(txt)) return Guid.NewGuid().GetHashCode();
            int v;
            if (int.TryParse(txt, out v)) return v;
            unchecked
            {
                int h = 17;
                foreach (char c in txt) h = h * 31 + c;
                return h;
            }
        }

        public static void Localize(int own)
        {
            if (St == null) return;
            Func<long, long> fix = key => key == 0 ? 0 : TagOf(key) == own ? IdOf(key) : TagOf(key) == 0 ? key : -1;
            foreach (Polity p in St.Polities)
            {
                long v = fix(p.King);
                if (v < 0)
                {
                    p.King = 0;
                    if (p.Kind != "alien" && p.Kind != "invader" && p.Kind != "precursor") p.Kind = "independent";
                }
                else p.King = v;
            }
            St.Polities.RemoveAll(p => p.Kind == "world");
            St.Voyages.RemoveAll(v => fix(v.King) < 0);
            foreach (Voyage v in St.Voyages) v.King = fix(v.King);
            St.Megas.RemoveAll(m => fix(m.King) < 0);
            foreach (Mega m in St.Megas) m.King = fix(m.King);
            St.Threats.RemoveAll(t => fix(t.King) < 0);
            foreach (Threat t in St.Threats) t.King = fix(t.King);
            for (int i = 0; i < St.Council.Count; i++) if (St.Council[i] > 0) St.Council[i] = Math.Max(0, fix(St.Council[i]));
            foreach (Polity p in St.Polities)
            {
                p.Met = p.Met.Select(fix).Where(x => x > 0).ToList();
                var op = new Dictionary<long, float>();
                foreach (var kv in p.Opinion)
                {
                    long v = fix(kv.Key);
                    if (v > 0) op[v] = kv.Value;
                }
                p.Opinion = op;
            }
            Dirty();
        }

        public static void SetState(CosState st)
        {
            St = st;
            _owner.Clear();
            _ownersDirty = true;
        }

        private static void Migrate()
        {
            if (St.Origin != "earth") return;
            St.Origin = "random";
            St.HomeBodies = 14;
            Dirty();
            Notify.Show(Txt.T("cos_origin_migrated"));
        }

        public static void Save()
        {
            if (St == null || CosmosPlanet.Active != null) return;
            if (Shared) Universe.Save();
            if (St.Log.Count > 400) St.Log.RemoveRange(0, St.Log.Count - 400);
            try
            {
                SaveCustomData d = Store.World();
                Store.Put(d, KState, JsonConvert.SerializeObject(St, Formatting.None, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }));
                Store.Put(d, Universe.KTag, Shared ? Tag : 0);
            }
            catch (Exception e)
            {
                CivMod.Report("cosmos-save", e);
            }
        }


        public static float TimeFactor(int scale)
        {
            if (St == null) return 1f;
            float[] auto = { 1f, 1f, 2f, 5f, 10f, 20f, 50f, 100f, 200f, 500f };
            float user = CivMod.C != null ? CivMod.C.CosmicTime : 1f;
            if (user <= 0.01f || St.Time <= 0.01f) return 1f;
            return Math.Max(1f, auto[Math.Max(0, Math.Min(9, scale))] * user * St.Time);
        }

        public static Polity Get(long id)
        {
            if (St == null) return null;
            foreach (Polity p in St.Polities) if (p.Id == id) return p;
            return null;
        }

        public static IEnumerable<Polity> Of(Kingdom k)
        {
            if (St == null || !G.Ok(k)) yield break;
            long id = Key(k);
            foreach (Polity p in St.Polities) if (p.King == id) yield return p;
        }

        public static Polity Owner(string node)
        {
            Owners();
            Polity p;
            string x = node;
            while (x != null)
            {
                if (_owner.TryGetValue(x, out p)) return p;
                x = CosmosGen.ParentPath(x);
            }
            return null;
        }

        public static Polity OwnerExact(string node)
        {
            Owners();
            Polity p;
            return node != null && _owner.TryGetValue(node, out p) ? p : null;
        }

        public static void Dirty()
        {
            _ownersDirty = true;
        }

        private static void Owners()
        {
            if (!_ownersDirty || St == null) return;
            _ownersDirty = false;
            _owner.Clear();
            foreach (Polity p in St.Polities) foreach (string n in p.Nodes) _owner[n] = p;
        }

        public static Polity NewPolity(Kingdom k, string kind, string seat, string name)
        {
            var p = new Polity
            {
                Id = St.NextId++,
                King = G.Ok(k) ? Key(k) : 0,
                Kind = kind,
                Seat = seat,
                Name = name,
                Founded = Now,
                Era = G.Ok(k) ? Research.Era(k) : 0,
                Species = G.Ok(k) ? Profile.Get(k).Species : "",
                Gov = G.Ok(k) ? Profile.Get(k).Gov : "",
                Ideo = G.Ok(k) ? Profile.Get(k).Ideo : "",
                Econ = G.Ok(k) ? Profile.Get(k).Econ : "",
                Color = G.Ok(k) ? G.Hex(k) : "#FFFFFF",
                Grace = 30
            };
            if (!string.IsNullOrEmpty(seat)) p.Nodes.Add(seat);
            St.Polities.Add(p);
            Dirty();
            return p;
        }

        public static int Scale(Kingdom k)
        {
            if (St == null || !G.Ok(k)) return 0;
            int best = 0;
            string home = St.Home;
            foreach (Polity p in Of(k))
            {
                foreach (string n in p.Nodes)
                {
                    int s = Spread(home, n);
                    if (s > best) best = s;
                }
            }
            return best;
        }

        public static int Spread(string home, string node)
        {
            if (string.IsNullOrEmpty(node) || node == home) return 0;
            for (int lvl = 1; lvl <= CosmosGen.Universe; lvl++)
            {
                if (CosmosGen.Contains(CosmosGen.Ancestor(home, lvl), node)) return lvl;
            }
            return CosmosGen.Universe;
        }

        public static void Log(string text)
        {
            if (St == null || string.IsNullOrEmpty(text)) return;
            St.Log.Add(Now + "|" + text);
        }

        public static bool First(string key, Kingdom k)
        {
            if (St == null || St.Firsts.ContainsKey(key)) return false;
            St.Firsts[key] = G.Ok(k) ? G.Id(k) : "";
            return true;
        }

        public static string FindBody(string system, Func<CNode, bool> filter)
        {
            CNode s = CosmosGen.Get(system);
            foreach (CNode b in CosmosGen.Children(s)) if (filter(b) && OwnerExact(b.Path) == null) return b.Path;
            return null;
        }

        public static void OnMission(Kingdom k, string mission)
        {
            if (St == null || !G.Ok(k)) return;
            string sys = CosmosGen.ParentPath(St.Home);
            if (mission == "moon")
            {
                string moon = FindBody(sys, b => b.Path != St.Home && (b.Sub == "barren" || b.Sub == "asteroid"));
                if (moon == null || Of(k).Any(x => x.Kind == "outpost")) return;
                Polity p = NewPolity(k, "outpost", moon, Txt.F("cos_name_base", CosmosGen.Get(moon).Name));
                p.Pop = 0.001f;
                CosmosHub.Colony(k, p, true);
            }
            else if (mission == "station")
            {
                Store.Put(k.data, "civ_cos_station", Store.Int(k.data, "civ_cos_station", 0) + 1);
            }
            else if (mission == "mars")
            {
                string mars = FindBody(sys, b => b.Path != St.Home && (b.Sub == "arid" || b.Sub == "barren"));
                if (mars == null || OwnerExact(mars) != null) return;
                Polity p = NewPolity(k, "outpost", mars, Txt.F("cos_name_base", CosmosGen.Get(mars).Name));
                p.Pop = 0.0005f;
                CosmosHub.Colony(k, p, true);
            }
        }

        public static void Year()
        {
            if (St == null || CosmosPlanet.Active != null) return;
            int yrs = Math.Max(1, Math.Min(10, G.Year - St.Year));
            St.Year = G.Year;
            if (Shared) St.Clock += yrs;
            CosmosSim.Year(yrs);
        }
    }
}
