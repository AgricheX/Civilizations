using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal class GovPlot
    {
        public string Type = "";
        public long Actor;
        public long Target;
        public int Start;
        public int Need;
        public string Motive = "";

        public string Save()
        {
            return Type + "|" + Actor + "|" + Target + "|" + Start + "|" + Need + "|" + Motive;
        }

        public static GovPlot Load(string s)
        {
            string[] p = s.Split('|');
            if (p.Length < 6) return null;
            var g = new GovPlot { Type = p[0], Motive = p[5] };
            long.TryParse(p[1], out g.Actor);
            long.TryParse(p[2], out g.Target);
            int.TryParse(p[3], out g.Start);
            int.TryParse(p[4], out g.Need);
            return g;
        }

        public float Progress { get { return Need <= 0 ? 1f : G.Clamp((G.Year - Start) / (float)Need, 0f, 1f); } }
    }

    internal static class Plots
    {
        private const string KPlots = "civ_plots";
        public static readonly string[] Types = { "independence", "demand_tribute", "peace_talks", "restoration", "appanage", "direct_rule", "win_over", "leave_bloc" };
        private static readonly HashSet<string> ClanPlots = new HashSet<string> { "win_over", "leave_bloc", "restoration", "appanage" };

        public static List<GovPlot> Of(Kingdom k)
        {
            var l = new List<GovPlot>();
            if (!G.Ok(k)) return l;
            string s = Store.Str(k.data, KPlots);
            if (s.Length == 0) return l;
            foreach (string part in s.Split(';'))
            {
                if (part.Length == 0) continue;
                GovPlot p = GovPlot.Load(part);
                if (p != null) l.Add(p);
            }
            return l;
        }

        private static void Save(Kingdom k, List<GovPlot> l)
        {
            Store.Put(k.data, KPlots, string.Join(";", l.Select(p => p.Save()).ToArray()));
        }

        public static bool Has(Kingdom k, string type, long actor)
        {
            return Of(k).Any(p => p.Type == type && p.Actor == actor);
        }

        public static bool HasAny(Kingdom k, string type)
        {
            return Of(k).Any(p => p.Type == type);
        }

        public static void Start(Kingdom k, string type, long actor, long target, int years, string motive)
        {
            if (!G.Ok(k)) return;
            List<GovPlot> l = Of(k);
            if (l.Any(p => p.Type == type && p.Actor == actor)) return;
            if (l.Count >= 8) return;
            l.Add(new GovPlot { Type = type, Actor = actor, Target = target, Start = G.Year, Need = Math.Max(1, years), Motive = motive ?? "" });
            Save(k, l);
            if (!ClanPlots.Contains(type) || type == "restoration" || type == "appanage") Gov.Log("plot_started", k, G.Name(k), Txt.T("civ_plot_" + type), ActorName(type, actor));
        }

        public static void Cancel(Kingdom k, string type, long actor)
        {
            List<GovPlot> l = Of(k);
            if (l.RemoveAll(p => p.Type == type && p.Actor == actor) > 0) Save(k, l);
        }

        public static string ActorName(string type, long id)
        {
            if (ClanPlots.Contains(type)) return Gov.ClanName(id);
            Kingdom k = G.KingdomById(id.ToString());
            return G.Ok(k) ? G.Name(k) : "?";
        }

        public static string TargetName(GovPlot p)
        {
            if (p.Type == "win_over" || p.Type == "leave_bloc") return Gov.ClanName(p.Target);
            if (p.Target == 0L) return "-";
            Kingdom k = G.KingdomById(p.Target.ToString());
            if (G.Ok(k)) return G.Name(k);
            City c = G.CityById(p.Target.ToString());
            return G.Ok(c) ? G.Name(c) : "-";
        }

        public static void KingdomYear(Kingdom k, KProfile pr, int yrs)
        {
            List<GovPlot> l = Of(k);
            if (l.Count == 0) return;
            var done = new List<GovPlot>();
            var keep = new List<GovPlot>();
            foreach (GovPlot p in l)
            {
                if (G.Year - p.Start >= p.Need) done.Add(p);
                else if (G.Year - p.Start > 40 || G.Year < p.Start) continue;
                else keep.Add(p);
            }
            Save(k, keep);
            foreach (GovPlot p in done)
            {
                GovPlot q = p;
                CivMod.Safe("plot-" + p.Type, () => Complete(k, q));
            }
        }

        private static void Complete(Kingdom k, GovPlot p)
        {
            Kingdom t = G.KingdomById(p.Target.ToString());
            switch (p.Type)
            {
                case "win_over":
                    Parliament.WinOver(k, p.Actor, p.Target);
                    return;
                case "leave_bloc":
                    Parliament.LeaveBloc(k, p.Actor, p.Target);
                    return;
                case "independence":
                    Vassals.Independence(k, p.Motive);
                    break;
                case "demand_tribute":
                    Vassals.DemandTribute(k, t);
                    break;
                case "peace_talks":
                    GovWar.TalksBetween(k, t);
                    break;
                case "restoration":
                    Mandate.Restore(k, p.Actor);
                    break;
                case "appanage":
                    Vassals.Appanage(k, p.Actor);
                    break;
                case "direct_rule":
                    Vassals.DirectRule(k, t);
                    break;
            }
            Gov.Log("plot_done", k, G.Name(k), Txt.T("civ_plot_" + p.Type), TargetName(p));
        }
    }
}
