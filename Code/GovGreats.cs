using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal class GreatRecord
    {
        public string Id = "";
        public string Role = "";
        public string Name = "";
        public int From;
        public int To = -1;
        public int Kills;
        public int Renown;
        public int Level;
        public string Deeds = "";

        public string Save()
        {
            return Id + "|" + Role + "|" + Name.Replace("|", " ") + "|" + From + "|" + To + "|" + Kills + "|" + Renown + "|" + Level + "|" + Deeds.Replace("|", " ").Replace(";", ",");
        }

        public static GreatRecord Load(string s)
        {
            string[] p = s.Split('|');
            if (p.Length < 8) return null;
            var r = new GreatRecord { Id = p[0], Role = p[1], Name = p[2], Deeds = p.Length > 8 ? p[8] : "" };
            int.TryParse(p[3], out r.From);
            int.TryParse(p[4], out r.To);
            int.TryParse(p[5], out r.Kills);
            int.TryParse(p[6], out r.Renown);
            int.TryParse(p[7], out r.Level);
            return r;
        }
    }

    internal static class GovGreats
    {
        private const string KHist = "civ_greats_hist";
        private const string KGreats = "civ_greats";

        public static void Register()
        {
            Hub.On(Hub.GreatPerson, e =>
            {
                if (!G.Ok(e.K) || e.Arg == null || !e.Arg.StartsWith("died")) return;
                Dictionary<string, GreatRecord> h = Load(e.K);
                string name = e.Arg.Length > 5 ? e.Arg.Substring(5) : "";
                GreatRecord r = h.Values.FirstOrDefault(x => x.To < 0 && x.Role == e.Id && (name.Length == 0 || x.Name == name));
                if (r == null) return;
                r.To = G.Year;
                Save(e.K, h);
            });
        }

        public static Dictionary<string, GreatRecord> Load(Kingdom k)
        {
            var d = new Dictionary<string, GreatRecord>();
            if (!G.Ok(k)) return d;
            foreach (string s in Store.Str(k.data, KHist).Split(';'))
            {
                if (s.Length == 0) continue;
                GreatRecord r = GreatRecord.Load(s);
                if (r != null) d[r.Id] = r;
            }
            return d;
        }

        private static void Save(Kingdom k, Dictionary<string, GreatRecord> d)
        {
            var l = d.Values.OrderByDescending(r => r.From).Take(40).Select(r => r.Save()).ToArray();
            Store.Put(k.data, KHist, string.Join(";", l));
        }

        public static GreatRecord Find(Kingdom k, string id)
        {
            GreatRecord r;
            return Load(k).TryGetValue(id, out r) ? r : null;
        }

        public static List<GreatRecord> Dead(Kingdom k)
        {
            return Load(k).Values.Where(r => r.To >= 0).OrderByDescending(r => r.To).ToList();
        }

        public static void Year()
        {
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!G.Ok(k) || !k.isCiv()) continue;
                HashSet<string> cur = Store.Set(k.data, KGreats);
                if (cur.Count == 0) continue;
                Dictionary<string, GreatRecord> h = Load(k);
                bool changed = false;
                foreach (string entry in cur)
                {
                    string[] p = entry.Split('|');
                    Actor a = G.ActorById(p[0]);
                    GreatRecord r;
                    if (!h.TryGetValue(p[0], out r))
                    {
                        r = new GreatRecord { Id = p[0], Role = p.Length > 1 ? p[1] : "", Name = p.Length > 2 ? p[2] : "?", From = G.Year };
                        h[p[0]] = r;
                        changed = true;
                    }
                    if (!G.Ok(a)) continue;
                    r.Name = a.getName();
                    r.Kills = a.data.kills;
                    r.Renown = (int)a.renown;
                    r.Level = a.data.level;
                    var deeds = new List<string>();
                    if (a.isKing()) deeds.Add("ruler");
                    if (a.isCityLeader()) deeds.Add("governor");
                    if (a.is_army_captain) deeds.Add("captain");
                    if (a.data.kills >= 10) deeds.Add("slayer");
                    if (deeds.Count > 0)
                    {
                        HashSet<string> all = new HashSet<string>(r.Deeds.Split(',').Where(x => x.Length > 0));
                        foreach (string dd in deeds) all.Add(dd);
                        r.Deeds = string.Join(",", all.ToArray());
                    }
                    changed = true;
                }
                if (changed) Save(k, h);
            }
        }

        public static string Sheet(GreatRecord r)
        {
            if (r == null) return "";
            string deeds = string.Join(", ", r.Deeds.Split(',').Where(x => x.Length > 0).Select(x => Txt.T("civ_deed_" + x)).ToArray());
            return Txt.F("civ_great_sheet", Txt.T("civ_great_" + r.Role), r.From, r.To >= 0 ? r.To.ToString() : "-", r.Kills, r.Renown, r.Level, deeds.Length > 0 ? deeds : "-");
        }
    }
}
