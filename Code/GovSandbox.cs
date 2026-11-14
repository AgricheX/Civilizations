using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Civilizations
{
    internal static class GovSandbox
    {
        public static readonly Dictionary<string, Func<Kingdom, bool>> Actions = new Dictionary<string, Func<Kingdom, bool>>
        {
            { "dynasty_new", NewDynasty },
            { "restoration", Restoration },
            { "great_rebellion", Mandate.GreatRebellion },
            { "heir", Heir },
            { "revolution", Revolution },
            { "coup", Mandate.GeneralCoup },
            { "ally_new", k => Ally(k, null) },
            { "ally_betray", Betray },
            { "ally_bloc", k => Ally(k, "ideological_bloc") },
            { "ally_trade", k => Ally(k, "trade_league") },
            { "ally_nuclear", k => Ally(k, "nuclear_umbrella") },
            { "ally_federation", k => Ally(k, "federation") },
            { "ally_dissolve", GovApi.DissolveAlliance },
            { "loyal", Loyal },
            { "gold", Gold },
            { "bankrupt", Bankrupt },
            { "civil_war", CivilWar }
        };

        public static void Register()
        {
            Type t = typeof(GovSandbox).Assembly.GetType("Civilizations.Sandbox");
            if (t == null) return;
            FieldInfo f = t.GetField("Hooks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var hooks = f != null ? f.GetValue(null) as Dictionary<string, Func<Kingdom, bool>> : null;
            if (hooks == null) return;
            foreach (var kv in Actions) hooks[kv.Key] = kv.Value;
        }

        private static bool NewDynasty(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            Mandate.NewDynasty(k);
            return true;
        }

        private static bool Restoration(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            long id;
            if (!long.TryParse(Store.Str(k.data, "civ_old_royal"), out id)) return false;
            Clan c = Gov.ClanById(id);
            if (c == null || !G.Ok(c.getChief()) || c.getChief().kingdom != k) return false;
            Mandate.Crown(k, c.getChief());
            Store.Put(k.data, "civ_old_royal", "");
            Mandate.Set(k, 55f);
            Mandate.SetPhase(k, 0, false);
            Gov.Log("restoration", k, G.Name(k), c.data.name, c.getChief().getName());
            return true;
        }

        private static bool Heir(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            Actor h = WinElections.Succession(k).FirstOrDefault(a => a.isAdult());
            if (h == null) return false;
            Mandate.Crown(k, h);
            return true;
        }

        private static bool Revolution(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            string cur = Policies.Id(k, "gov");
            string alt = Politics.Alternative(k, "gov");
            PolicyDef p = Db.Policy(alt);
            if (p == null || alt == cur || !Policies.Unlocked(k, p))
            {
                p = Policies.Options(k, "gov").Where(x => x.Id != cur).OrderBy(x => UnityEngine.Random.value).FirstOrDefault();
                if (p == null) return false;
            }
            Policies.Set(k, "gov", p.Id, "revolution");
            Politics.AddStability(k, -0.12f);
            Mandate.NewDynasty(k);
            return Policies.Id(k, "gov") == p.Id;
        }

        private static Kingdom Partner(Kingdom k)
        {
            return G.Kingdoms().Where(o => G.Ok(o) && o != k && o.isCiv() && !World.world.wars.isInWarWith(k, o) && Fog.Knows(k, o)).OrderByDescending(o => World.world.diplomacy.getOpinion(k, o).total).FirstOrDefault();
        }

        private static bool Ally(Kingdom k, string type)
        {
            if (!G.Ok(k)) return false;
            Alliance a = AllianceLife.Of(k);
            if (a == null)
            {
                Kingdom o = Partner(k);
                if (o == null) return false;
                a = AllianceLife.Create(k, o, null);
            }
            if (a == null) return false;
            if (type != null) AllianceLife.ForceType(a, type);
            return true;
        }

        private static bool Betray(Kingdom k)
        {
            Alliance a = AllianceLife.Of(k);
            if (a == null) return false;
            Kingdom o = a.kingdoms_list.Where(x => G.Ok(x) && x != k).OrderBy(x => Profile.Get(x).Strength).FirstOrDefault();
            if (o == null) return false;
            AllianceLife.Shift(a, -40f, "betrayal");
            k.allianceLeave(a);
            return Vassals.War(k, o, "conquest") != null;
        }

        private static bool Loyal(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            Mandate.Set(k, Math.Max(Mandate.Get(k), 85f));
            Politics.AddStability(k, 0.3f);
            Legitimacy.Shock(k, 0.2f);
            foreach (City c in G.Cities(k)) Store.Put(c.data, Parliament.KDecree, -999);
            Bonus.Dirty(k);
            return true;
        }

        private static bool Gold(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            float v = 500f * (Gov.Band(k) + 1);
            Treasury.AddReserve(k, v);
            if (k.capital != null) Economy.Give(k.capital, "gold", (int)v);
            return true;
        }

        private static bool Bankrupt(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            Treasury.Bankrupt(k);
            if (k.capital != null) Economy.TakeUpTo(k.capital, "gold", Economy.Amount(k.capital, "gold"));
            return true;
        }

        private static bool CivilWar(Kingdom k)
        {
            if (!G.Ok(k) || Profile.Get(k).Cities < 2) return false;
            int before = G.Cities(k).Count;
            Legitimacy.CivilWar(k, Profile.Get(k));
            return G.Cities(k).Count < before;
        }
    }
}
