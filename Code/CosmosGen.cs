using System;
using System.Collections.Generic;
using System.Text;

namespace Civilizations
{
    internal class CNode
    {
        public string Path = "";
        public int Level;
        public int Index;
        public int Seed;
        public string Name = "";
        public string Sub = "";
        public float X;
        public float Y;
        public float Hab;
        public string[] Res = new string[0];
        public int Count;
        public bool Home;
        public bool Nebula;
    }

    internal static class CosmosGen
    {
        public const int Universe = 9;
        public static readonly string[] LevelIds = { "body", "system", "neighborhood", "bubble", "arm", "galaxy", "group", "cluster", "supercluster", "universe" };
        public static readonly double[] SizeLy = { 0.0002, 0.0016, 30, 1000, 10000, 100000, 1e7, 1.5e7, 5e8, 9.3e10 };
        public static readonly string[] BodyTypes = { "terran", "ocean", "jungle", "swamp", "arid", "barren", "volcanic", "crystal", "ice", "gas_giant", "reef", "abyss", "fungal", "machine", "asteroid" };
        public static readonly string[] StarTypes = { "G", "K", "M", "F", "A", "B", "O", "red_giant", "white_dwarf", "neutron", "black_hole", "binary" };
        public static readonly string[] GalaxyTypes = { "spiral", "barred", "elliptical", "dwarf", "irregular" };
        public static readonly string[] Resources = { "helium3", "comet_ice", "asteroid_metals", "antimatter", "exotic_matter", "stellar_energy", "dark_matter", "neutronium", "vacuum_energy" };

        private static readonly Dictionary<string, CNode> _cache = new Dictionary<string, CNode>();
        public static int Seed = 1;
        public static int HomeBodies;
        public static bool SharedGen;
        public static readonly HashSet<string> Homes = new HashSet<string>();
        public static string HomePath = "";

        public static void Setup(int seed, string home, int homeBodies)
        {
            Setup(seed, home, homeBodies, false, null);
        }

        public static void Setup(int seed, string home, int homeBodies, bool shared, IEnumerable<string> homes)
        {
            Seed = seed;
            HomeBodies = homeBodies;
            SharedGen = shared;
            Homes.Clear();
            if (homes != null) foreach (string h in homes) if (!string.IsNullOrEmpty(h)) Homes.Add(h);
            _cache.Clear();
            HomePath = string.IsNullOrEmpty(home) ? PickHome() : home;
        }

        public static int Depth(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            int n = 1;
            foreach (char c in path) if (c == '.') n++;
            return n;
        }

        public static int LevelOf(string path)
        {
            return Universe - Depth(path);
        }

        public static string ParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int i = path.LastIndexOf('.');
            return i < 0 ? "" : path.Substring(0, i);
        }

        public static string Child(string path, int i)
        {
            return string.IsNullOrEmpty(path) ? i.ToString() : path + "." + i;
        }

        public static string Ancestor(string path, int level)
        {
            string p = path;
            while (p != null && LevelOf(p) < level) p = ParentPath(p);
            return p;
        }

        public static bool Contains(string anc, string path)
        {
            if (anc == null || path == null) return false;
            if (anc.Length == 0) return true;
            return path == anc || path.StartsWith(anc + ".");
        }

        private static int Hash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return h ^ Seed * 374761393;
            }
        }

        private static bool OnHomeChain(string path)
        {
            return Contains(path, HomePath);
        }

        private static string PickHome()
        {
            var r = new Random(Seed * 7919 + 13);
            var sb = new StringBuilder();
            for (int depth = 1; depth <= 8; depth++)
            {
                if (sb.Length > 0) sb.Append('.');
                sb.Append(r.Next(0, Math.Max(1, ChildCount(Universe - depth + 1, r))));
            }
            sb.Append(".2");
            return sb.ToString();
        }

        private static int ChildCount(int level, Random r)
        {
            switch (level)
            {
                case 9: return 12;
                case 8: return 10;
                case 7: return 12;
                case 6: return 10;
                case 5: return 8;
                case 4: return 10;
                case 3: return 12;
                case 2: return 10;
                default: return 0;
            }
        }

        private static bool HomeSystem(string path)
        {
            foreach (string h in Homes) if (Contains(path, h)) return true;
            return false;
        }

        public static void AddHome(string path)
        {
            if (string.IsNullOrEmpty(path) || !Homes.Add(path)) return;
            _cache.Clear();
        }

        public static string RandomBody(string region, Random r)
        {
            CNode n = Get(region ?? "");
            int guard = 0;
            while (n != null && n.Level > 0 && guard++ < 12)
            {
                if (n.Count <= 0) return null;
                n = Get(Child(n.Path, r.Next(0, n.Count)));
            }
            return n != null && n.Level == 0 ? n.Path : null;
        }

        public static CNode Root()
        {
            return Get("");
        }

        public static CNode Home()
        {
            return Get(HomePath);
        }

        public static CNode Get(string path)
        {
            if (path == null) return null;
            CNode n;
            if (_cache.TryGetValue(path, out n)) return n;
            if (_cache.Count > 6000) _cache.Clear();
            n = Make(path);
            _cache[path] = n;
            return n;
        }

        public static List<CNode> Children(CNode n)
        {
            var l = new List<CNode>();
            if (n == null || n.Level <= 0) return l;
            for (int i = 0; i < n.Count; i++) l.Add(Get(Child(n.Path, i)));
            return l;
        }

        private static CNode Make(string path)
        {
            var n = new CNode { Path = path, Level = LevelOf(path), Seed = Hash(path) };
            int.TryParse(path.Length == 0 ? "0" : path.Substring(path.LastIndexOf('.') + 1), out n.Index);
            var r = new Random(n.Seed);
            string parent = ParentPath(path);
            n.Home = path == HomePath;
            n.X = 0.1f + (float)r.NextDouble() * 0.8f;
            n.Y = 0.12f + (float)r.NextDouble() * 0.76f;
            if (n.Level == 0)
            {
                n.Count = 0;
                bool isHome = n.Home || SharedGen && Homes.Contains(path);
                n.Sub = isHome ? "terran" : PickBody(r, n.Index);
                n.Name = BodyName(parent, n.Index, r);
                n.Hab = Habitability(n.Sub, r);
                if (isHome) n.Hab = 1f;
                n.Res = BodyResources(n.Sub, r);
                n.X = 0.12f + 0.06f * n.Index;
                n.Y = (float)r.NextDouble();
                return n;
            }
            if (n.Level == 1)
            {
                bool homeSys = SharedGen ? HomeSystem(path) : OnHomeChain(path);
                n.Count = SharedGen ? 4 + r.Next(0, 9) : homeSys && HomeBodies > 0 ? HomeBodies : homeSys ? Math.Max(4, 5 + r.Next(0, 8)) : 3 + r.Next(0, 9);
                n.Sub = homeSys ? "G" : PickStar(r);
                n.Name = StarName(r);
                if (n.Sub == "black_hole" || n.Sub == "neutron") n.Count = r.Next(0, 3);
                return n;
            }
            n.Count = ChildCount(n.Level, r);
            n.Name = n.Level == 9 ? "" : RegionName(n.Level, r);
            if (n.Level == 5) n.Sub = GalaxyTypes[r.Next(0, GalaxyTypes.Length)];
            if (n.Level == 5 && (n.Sub == "dwarf" || n.Sub == "irregular") && !(SharedGen ? HomeSystem(path) : OnHomeChain(path))) n.Count = Math.Max(3, n.Count / 2);
            n.Nebula = (n.Level == 3 || n.Level == 2) && r.NextDouble() < 0.35;
            if (n.Level == 9) n.Name = "Universe " + Seed.ToString("X");
            return n;
        }

        private static string PickBody(Random r, int index)
        {
            double x = r.NextDouble();
            if (index >= 5 && x < 0.45) return x < 0.3 ? "gas_giant" : "ice";
            if (index <= 1 && x < 0.4) return x < 0.2 ? "volcanic" : "barren";
            string[] pool = { "terran", "ocean", "jungle", "swamp", "arid", "barren", "barren", "volcanic", "crystal", "ice", "gas_giant", "reef", "abyss", "fungal", "machine", "asteroid", "asteroid" };
            return pool[r.Next(0, pool.Length)];
        }

        private static string PickStar(Random r)
        {
            double x = r.NextDouble();
            if (x < 0.55) return "M";
            if (x < 0.68) return "K";
            if (x < 0.76) return "G";
            if (x < 0.81) return "F";
            if (x < 0.84) return "A";
            if (x < 0.85) return "B";
            if (x < 0.853) return "O";
            if (x < 0.88) return "red_giant";
            if (x < 0.94) return "white_dwarf";
            if (x < 0.95) return "neutron";
            if (x < 0.953) return "black_hole";
            return "binary";
        }

        private static float Habitability(string type, Random r)
        {
            float b;
            switch (type)
            {
                case "terran": b = 0.9f; break;
                case "ocean": b = 0.7f; break;
                case "jungle": b = 0.75f; break;
                case "swamp": b = 0.6f; break;
                case "reef": b = 0.55f; break;
                case "arid": b = 0.45f; break;
                case "fungal": b = 0.45f; break;
                case "abyss": b = 0.3f; break;
                case "crystal": b = 0.2f; break;
                case "ice": b = 0.15f; break;
                case "volcanic": b = 0.1f; break;
                case "barren": b = 0.08f; break;
                case "machine": b = 0.25f; break;
                case "asteroid": b = 0.03f; break;
                default: b = 0.02f; break;
            }
            return Math.Max(0f, Math.Min(1f, b + ((float)r.NextDouble() - 0.5f) * 0.2f));
        }

        private static string[] BodyResources(string type, Random r)
        {
            var l = new List<string>();
            if (type == "gas_giant" || type == "barren") l.Add("helium3");
            if (type == "ice" || type == "asteroid") l.Add("comet_ice");
            if (type == "asteroid" || type == "barren" || type == "volcanic") l.Add("asteroid_metals");
            if (type == "crystal" && r.NextDouble() < 0.4) l.Add("exotic_matter");
            if (type == "machine") l.Add("exotic_matter");
            return l.ToArray();
        }

        private static readonly string[] SylA = { "ka", "ve", "lo", "mi", "zar", "tor", "an", "el", "su", "ri", "qua", "nor", "the", "dra", "vo", "ix", "pel", "or", "sa", "ky", "ul", "en", "ba", "ce", "mu", "ro", "zi", "xe" };
        private static readonly string[] SylB = { "n", "s", "th", "x", "r", "l", "on", "is", "ar", "ia", "us", "ei", "um", "os", "ax", "en" };
        private static readonly string[] Greek = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa", "Lambda", "Sigma", "Tau", "Omega" };

        public static string Word(Random r, int min = 2, int max = 3)
        {
            int n = r.Next(min, max + 1);
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.Append(SylA[r.Next(0, SylA.Length)]);
            if (r.NextDouble() < 0.6) sb.Append(SylB[r.Next(0, SylB.Length)]);
            string s = sb.ToString();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string StarName(Random r)
        {
            double x = r.NextDouble();
            if (x < 0.3) return "HD " + r.Next(1000, 99999);
            if (x < 0.5) return Greek[r.Next(0, Greek.Length)] + " " + Word(r, 2, 2);
            if (x < 0.6) return "Gliese " + r.Next(100, 999);
            return Word(r);
        }

        private static string BodyName(string system, int index, Random r)
        {
            CNode s = system != null ? Get(system) : null;
            string star = s != null ? s.Name : "Star";
            if (r.NextDouble() < 0.55) return star + " " + (char)('b' + index);
            return Word(r);
        }

        private static string RegionName(int level, Random r)
        {
            string w = Word(r);
            switch (level)
            {
                case 8: return w + " Supercluster";
                case 7: return w + " Cluster";
                case 6: return w + " Group";
                case 5: return r.NextDouble() < 0.4 ? "NGC " + r.Next(100, 7999) : w;
                case 4: return w + " Arm";
                case 3: return r.NextDouble() < 0.5 ? w + " Nebula" : w + " Bubble";
                case 2: return w + " Cloud";
                default: return w;
            }
        }

        public static double DistanceLy(string a, string b)
        {
            if (a == null || b == null || a == b) return 0;
            string[] pa = a.Split('.'), pb = b.Split('.');
            int i = 0;
            while (i < pa.Length && i < pb.Length && pa[i] == pb[i]) i++;
            string lca = i == 0 ? "" : string.Join(".", pa, 0, i);
            if (i >= pa.Length || i >= pb.Length) return SizeLy[Math.Max(0, LevelOf(lca) - 1)] * 0.5;
            CNode ca = Get(Child(lca, int.Parse(pa[i])));
            CNode cb = Get(Child(lca, int.Parse(pb[i])));
            int lvl = LevelOf(lca);
            double dx = ca.X - cb.X, dy = ca.Y - cb.Y;
            double frac = Math.Sqrt(dx * dx + dy * dy);
            if (lvl == 1) frac = Math.Abs(ca.X - cb.X);
            return Math.Max(SizeLy[Math.Max(0, lvl - 1)], frac * SizeLy[lvl]);
        }

        public static string Describe(CNode n)
        {
            if (n == null) return "";
            if (n.Level == 0) return Txt.T("cos_body_" + n.Sub);
            if (n.Level == 1) return Txt.T("cos_star_" + n.Sub);
            if (n.Level == 5) return Txt.T("cos_galaxy_" + n.Sub);
            return Txt.T("cos_level_" + LevelIds[n.Level]);
        }

        public static string Chain(CNode n)
        {
            var parts = new List<string>();
            string p = n.Path;
            while (p != null)
            {
                CNode x = Get(p);
                if (x.Name.Length > 0) parts.Insert(0, x.Name);
                p = ParentPath(p);
            }
            return string.Join(" › ", parts.ToArray());
        }
    }
}
