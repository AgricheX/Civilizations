using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Civilizations
{
    internal class UniWorld
    {
        public int Tag;
        public string Id = "";
        public string Name = "";
        public string Home = "";
        public int Seen;
        public int Joined;
        public bool Frozen;
        public bool Lost;
        public float Pop;
        public int Era;
        public string Gov = "";
        public string Ideo = "";
        public string Econ = "";
        public string Species = "";
        public string Color = "#FFFFFF";
        public List<string> Pending = new List<string>();
    }

    internal class UniFile
    {
        public int Ver = 1;
        public int Seed;
        public int NextTag = 1;
        public CosState St;
        public Dictionary<string, UniWorld> Worlds = new Dictionary<string, UniWorld>();
    }

    internal static class Universe
    {
        public const string KTag = "civ_cos_tag";
        private const string FileName = "universe.json";
        private const int Backups = 3;
        public static UniFile U;

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.CosShared; }
        }

        public static string Folder
        {
            get { return Path.Combine(Application.persistentDataPath, "civilizations"); }
        }

        private static string FilePath
        {
            get { return Path.Combine(Folder, FileName); }
        }

        public static UniWorld Me
        {
            get
            {
                UniWorld w;
                return U != null && U.Worlds.TryGetValue(Sim.WorldId, out w) ? w : null;
            }
        }

        public static UniWorld ByTag(int tag)
        {
            if (U == null) return null;
            foreach (UniWorld w in U.Worlds.Values) if (w.Tag == tag) return w;
            return null;
        }

        public static bool Frozen(int tag)
        {
            UniWorld w = ByTag(tag);
            return w == null || w.Frozen || w.Lost;
        }

        private static UniFile Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                UniFile f = JsonConvert.DeserializeObject<UniFile>(File.ReadAllText(path));
                return f != null && f.St != null ? f : null;
            }
            catch (Exception e)
            {
                CivMod.Report("universe-read", e);
                return null;
            }
        }

        private static UniFile ReadAny()
        {
            UniFile f = Read(FilePath);
            for (int i = 1; f == null && i <= Backups; i++) f = Read(FilePath + "." + i);
            return f;
        }

        private static void Write()
        {
            if (U == null) return;
            Directory.CreateDirectory(Folder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(U, Formatting.None, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }));
            if (File.Exists(FilePath))
            {
                for (int i = Backups; i > 1; i--)
                {
                    string a = FilePath + "." + (i - 1), b = FilePath + "." + i;
                    if (File.Exists(b)) File.Delete(b);
                    if (File.Exists(a)) File.Move(a, b);
                }
                string first = FilePath + ".1";
                if (File.Exists(first)) File.Delete(first);
                File.Move(FilePath, first);
            }
            File.Move(tmp, FilePath);
        }

        private static UniFile Fresh(CosState local)
        {
            var f = new UniFile();
            f.St = new CosState();
            f.St.Origin = "random";
            f.St.Seed = local != null && local.Seed != 0 ? local.Seed : Cosmos.NewSeed();
            f.St.Fermi = CivMod.C != null ? CivMod.C.Fermi : 1;
            f.St.Time = 1f;
            f.St.Invasions = CivMod.C == null || CivMod.C.CosmicInvasions;
            f.St.Ending = CivMod.C != null && CivMod.C.CosmicEnding;
            f.Seed = f.St.Seed;
            return f;
        }

        public static bool Load(SaveCustomData d)
        {
            try
            {
                if (string.IsNullOrEmpty(Sim.WorldId)) return false;
                CosState local = LocalState(d);
                if (U == null) U = ReadAny();
                bool fresh = false;
                if (U == null)
                {
                    U = Fresh(local);
                    fresh = true;
                }
                UniWorld me;
                bool joined = false;
                if (!U.Worlds.TryGetValue(Sim.WorldId, out me))
                {
                    me = new UniWorld { Tag = U.NextTag++, Id = Sim.WorldId, Joined = U.St.Clock, Seen = U.St.Clock };
                    U.Worlds[Sim.WorldId] = me;
                    joined = true;
                }
                me.Lost = false;
                Cosmos.Tag = me.Tag;
                Cosmos.Shared = true;
                Cosmos.SetState(U.St);
                CosState st = U.St;
                CosmosGen.Setup(U.Seed, me.Home, 0, true, U.Worlds.Values.Select(w => w.Home));
                if (joined && fresh && local != null && !string.IsNullOrEmpty(local.Home))
                {
                    string kept = Remap(local.Home, local.Home, local.Home);
                    if (kept != null) me.Home = kept;
                }
                if (string.IsNullOrEmpty(me.Home) || st.Dead.Contains(me.Home))
                {
                    me.Home = PickHome(me);
                    CosmosGen.Setup(U.Seed, me.Home, 0, true, U.Worlds.Values.Select(w => w.Home));
                }
                st.Home = me.Home;
                st.Polities.RemoveAll(p => p.Kind == "world" && p.King == Cosmos.KeyOf(me.Tag, 0));
                st.Year = G.Year;
                if (joined && local != null) Adopt(local, me, fresh);
                Away(me);
                Apply(me);
                Cosmos.Dirty();
                return true;
            }
            catch (Exception e)
            {
                CivMod.Report("universe-load", e);
                Cosmos.Tag = 0;
                Cosmos.Shared = false;
                Cosmos.SetState(null);
                return false;
            }
        }

        private static CosState LocalState(SaveCustomData d)
        {
            string s = Store.Str(d, "civ_cosmos");
            if (s.Length == 0) return null;
            try
            {
                CosState st = JsonConvert.DeserializeObject<CosState>(s);
                if (st == null || Store.Int(d, KTag, 0) != 0) return null;
                return st;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string PickHome(UniWorld me)
        {
            var r = new System.Random(unchecked(U.Seed * 31 + me.Tag * 7919));
            bool near = CivMod.C == null || CivMod.C.CosNear;
            UniWorld other = U.Worlds.Values.Where(w => w != me && !string.IsNullOrEmpty(w.Home)).OrderBy(w => w.Tag).FirstOrDefault();
            string region = near && other != null ? CosmosGen.Ancestor(other.Home, 4) : "";
            for (int i = 0; i < 400; i++)
            {
                string reg = i < 200 ? region : "";
                string b = CosmosGen.RandomBody(reg, r);
                if (b == null || !Free(b)) continue;
                CNode n = CosmosGen.Get(b);
                CNode sys = CosmosGen.Get(CosmosGen.ParentPath(b));
                if (n.Sub == "gas_giant" || n.Sub == "asteroid" || sys == null || sys.Sub == "black_hole" || sys.Sub == "neutron") continue;
                if (U.Worlds.Values.Any(w => w != me && w.Home != null && CosmosGen.Contains(CosmosGen.ParentPath(w.Home), b))) continue;
                return b;
            }
            return CosmosGen.RandomBody("", r) ?? "0.0.0.0.0.0.0.0.0";
        }

        private static bool Free(string node)
        {
            if (Cosmos.OwnerExact(node) != null || Cosmos.St.Dead.Contains(node)) return false;
            foreach (UniWorld w in U.Worlds.Values) if (w.Home == node) return false;
            return true;
        }

        private static string Remap(string path, string oldHome, string newHome)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(oldHome)) return null;
            string[] a = path.Split('.'), h = oldHome.Split('.'), nh = newHome.Split('.');
            if (a.Length > h.Length || nh.Length != h.Length) return null;
            int same = 0;
            while (same < a.Length && a[same] == h[same]) same++;
            string cur = "";
            for (int i = 0; i < a.Length; i++)
            {
                CNode n = CosmosGen.Get(cur);
                if (n == null || n.Count <= 0) return null;
                int idx;
                int.TryParse(i < same ? nh[i] : a[i], out idx);
                cur = CosmosGen.Child(cur, Math.Abs(idx) % n.Count);
            }
            return cur;
        }

        private static long Tagged(int tag, long key)
        {
            return key > 0 && Cosmos.TagOf(key) == 0 ? Cosmos.KeyOf(tag, key) : key;
        }

        private static void Adopt(CosState local, UniWorld me, bool all)
        {
            CosState st = U.St;
            var ids = new Dictionary<long, long>();
            foreach (Polity p in local.Polities)
            {
                if (p.Kind == "world" || p.King == 0 && !all) continue;
                string seat = Remap(p.Seat, local.Home, me.Home);
                if (seat == null || seat == me.Home || Cosmos.OwnerExact(seat) != null || st.Dead.Contains(seat)) continue;
                long oldId = p.Id;
                p.Id = st.NextId++;
                ids[oldId] = p.Id;
                p.King = p.King == 0 ? 0 : Cosmos.KeyOf(me.Tag, p.King);
                p.Seat = seat;
                p.Nodes = p.Nodes.Select(n => Remap(n, local.Home, me.Home)).Where(n => n != null && Cosmos.OwnerExact(n) == null).Distinct().ToList();
                if (!p.Nodes.Contains(seat)) p.Nodes.Insert(0, seat);
                if (all)
                {
                    p.Met = p.Met.Select(x => Tagged(me.Tag, x)).ToList();
                    var op = new Dictionary<long, float>();
                    foreach (var kv in p.Opinion) op[Tagged(me.Tag, kv.Key)] = kv.Value;
                    p.Opinion = op;
                }
                else
                {
                    p.Met.Clear();
                    p.Opinion.Clear();
                }
                p.Founded = st.Clock;
                st.Polities.Add(p);
                Cosmos.Dirty();
            }
            foreach (Mega m in local.Megas)
            {
                if (m.King == 0) continue;
                string node = m.Node == local.Home ? me.Home : Remap(m.Node, local.Home, me.Home) ?? CosmosGen.Ancestor(me.Home, CosmosGen.LevelOf(m.Node));
                m.King = Cosmos.KeyOf(me.Tag, m.King);
                m.Node = node;
                m.Polity = ids.ContainsKey(m.Polity) ? ids[m.Polity] : 0;
                st.Megas.Add(m);
            }
            foreach (Threat t in local.Threats)
            {
                if (t.King == 0) continue;
                t.King = Cosmos.KeyOf(me.Tag, t.King);
                t.Target = t.Target == local.Home ? me.Home : Remap(t.Target, local.Home, me.Home) ?? me.Home;
                st.Threats.Add(t);
            }
            foreach (Polity p in st.Polities) if (p.Parent != 0 && ids.ContainsKey(p.Parent)) p.Parent = ids[p.Parent];
            foreach (string f in local.Firsts.Keys) if (!st.Firsts.ContainsKey(f)) st.Firsts[f] = local.Firsts[f];
            if (all)
            {
                foreach (string r in local.Ruins.Select(x => Remap(x, local.Home, me.Home)).Where(x => x != null)) if (!st.Ruins.Contains(r)) st.Ruins.Add(r);
                foreach (string r in local.Dead.Select(x => Remap(x, local.Home, me.Home)).Where(x => x != null && x != me.Home)) if (!st.Dead.Contains(r)) st.Dead.Add(r);
                foreach (string r in local.Seeded.Select(x => Remap(x, local.Home, me.Home)).Where(x => x != null)) if (!st.Seeded.Contains(r)) st.Seeded.Add(r);
                foreach (long c in local.Council) st.Council.Add(c > 0 ? Tagged(me.Tag, c) : ids.ContainsKey(-c) ? -ids[-c] : c);
                st.Legends.AddRange(local.Legends);
                st.Log.AddRange(local.Log.Select(l => { int bar = l.IndexOf('|'); return bar > 0 ? "0" + l.Substring(bar) : l; }));
                st.Ended = local.Ended;
            }
            Cosmos.Log(Txt.F("cos_log_joined", Name(me)));
        }

        private static void Away(UniWorld me)
        {
            CosState st = U.St;
            int years = st.Clock - me.Seen;
            me.Seen = st.Clock;
            if (years <= 0) return;
            int lines = 0;
            foreach (string l in st.Log)
            {
                int bar = l.IndexOf('|');
                int y;
                if (bar > 0 && int.TryParse(l.Substring(0, bar), out y) && y > st.Clock - years) lines++;
            }
            Chronicle.Log("cos_away", null, years.ToString(), Txt.F("cos_away_events", lines), "");
        }

        private static void Apply(UniWorld me)
        {
            if (me.Pending.Count == 0) return;
            foreach (string p in me.Pending.ToList()) CivMod.Safe("universe-pending", () => Ending.ApplyLocal(p));
            me.Pending.Clear();
        }

        public static void Broadcast(string effect)
        {
            if (U == null || !Cosmos.Shared) return;
            foreach (UniWorld w in U.Worlds.Values) if (w.Tag != Cosmos.Tag && !w.Lost) w.Pending.Add(effect);
        }

        private static string Name(UniWorld w)
        {
            return string.IsNullOrEmpty(w.Name) ? Txt.F("cos_world_n", w.Tag) : w.Name;
        }

        private static void Describe(UniWorld me)
        {
            if (World.world != null && World.world.map_stats != null && !string.IsNullOrEmpty(World.world.map_stats.name)) me.Name = World.world.map_stats.name;
            Kingdom top = null;
            float pop = 0f;
            int era = 0;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                int n = G.Pop(k);
                pop += n;
                era = Math.Max(era, Research.Era(k));
                if (top == null || n > G.Pop(top)) top = k;
            }
            me.Pop = pop / 1000f;
            me.Era = era;
            if (G.Ok(top))
            {
                KProfile p = Profile.Get(top);
                me.Gov = p.Gov;
                me.Ideo = p.Ideo;
                me.Econ = p.Econ;
                me.Species = p.Species;
                me.Color = G.Hex(top);
            }
        }

        private static Polity Mirror(UniWorld me)
        {
            return new Polity
            {
                Id = -me.Tag,
                Name = Name(me),
                King = Cosmos.KeyOf(me.Tag, 0),
                Kind = "world",
                Seat = me.Home,
                Nodes = new List<string> { me.Home },
                Pop = Math.Max(0.001f, me.Pop),
                Era = me.Era,
                Gov = me.Gov,
                Ideo = me.Ideo,
                Econ = me.Econ,
                Species = me.Species,
                Color = me.Color,
                Founded = me.Joined
            };
        }

        public static void Save()
        {
            if (U == null || !Cosmos.Shared || Cosmos.St == null) return;
            UniWorld me = Me;
            if (me == null) return;
            me.Seen = U.St.Clock;
            Describe(me);
            Polity mirror = Mirror(me);
            U.St.Polities.RemoveAll(p => p.Kind == "world" && p.King == mirror.King);
            U.St.Polities.Add(mirror);
            try
            {
                Write();
            }
            catch (Exception e)
            {
                CivMod.Report("universe-write", e);
            }
            finally
            {
                U.St.Polities.Remove(mirror);
                Cosmos.Dirty();
            }
        }

        public static List<UniWorld> Worlds()
        {
            return U == null ? new List<UniWorld>() : U.Worlds.Values.OrderBy(w => w.Tag).ToList();
        }

        public static void SetFrozen(UniWorld w, bool frozen)
        {
            if (w == null || w == Me) return;
            w.Frozen = frozen;
        }

        public static int Prune(int years)
        {
            if (U == null) return 0;
            int n = 0;
            foreach (UniWorld w in U.Worlds.Values)
            {
                if (w == Me || w.Lost || U.St.Clock - w.Seen < years) continue;
                w.Lost = true;
                long king = Cosmos.KeyOf(w.Tag, 0);
                foreach (Polity p in U.St.Polities.ToList())
                {
                    if (Cosmos.TagOf(p.King) != w.Tag) continue;
                    if (p.Kind == "world")
                    {
                        U.St.Polities.Remove(p);
                        if (!U.St.Ruins.Contains(p.Seat)) U.St.Ruins.Add(p.Seat);
                        continue;
                    }
                    p.King = 0;
                    p.Kind = "independent";
                }
                U.St.Megas.RemoveAll(m => Cosmos.TagOf(m.King) == w.Tag);
                U.St.Threats.RemoveAll(t => Cosmos.TagOf(t.King) == w.Tag);
                U.St.Voyages.RemoveAll(v => Cosmos.TagOf(v.King) == w.Tag);
                if (!U.St.Ruins.Contains(w.Home)) U.St.Ruins.Add(w.Home);
                Cosmos.Log(Txt.F("cos_log_lost_world", Name(w)));
                n++;
            }
            Cosmos.Dirty();
            return n;
        }

        public static void Reset()
        {
            if (U == null) return;
            UniFile old = U;
            U = Fresh(null);
            UniWorld me;
            if (old.Worlds.TryGetValue(Sim.WorldId, out me))
            {
                me.Tag = U.NextTag++;
                me.Home = "";
                me.Pending.Clear();
                me.Seen = 0;
                me.Joined = 0;
                U.Worlds[me.Id] = me;
            }
            Write();
        }

        public static string Export()
        {
            if (U == null) return null;
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "universe_export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(U, Formatting.Indented));
            return path;
        }

        public static bool Import()
        {
            if (!Directory.Exists(Folder)) return false;
            string last = Directory.GetFiles(Folder, "universe_import*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (last == null) return false;
            UniFile f = Read(last);
            if (f == null) return false;
            U = f;
            Write();
            return true;
        }

        public static void Regen()
        {
            if (U == null) return;
            UniWorld me = Me;
            string home = me != null ? me.Home : U.St.Home;
            CosmosGen.Setup(U.Seed, home, 0, true, U.Worlds.Values.Select(w => w.Home));
        }

        public static void Rebirth()
        {
            if (U == null) return;
            U.Seed = U.St.Seed;
            foreach (UniWorld w in U.Worlds.Values) w.Home = "";
            CosmosGen.Setup(U.Seed, "", 0, true, null);
            UniWorld me = Me;
            if (me == null) return;
            me.Home = PickHome(me);
            CosmosGen.Setup(U.Seed, me.Home, 0, true, new[] { me.Home });
            U.St.Home = me.Home;
        }

        public static void Forget()
        {
            U = null;
        }
    }
}
