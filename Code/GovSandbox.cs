using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class GovSandbox
    {
        private const string Probe = "kingdom:gov_vote";
        private static Kingdom _lordPick;
        private static City _cityPick;

        public static readonly Dictionary<string, Func<Kingdom, bool>> Actions = new Dictionary<string, Func<Kingdom, bool>>
        {
            { "dynasty_new", k => { Mandate.NewDynasty(k, true); return true; } },
            { "restoration", Restore },
            { "great_rebellion", Mandate.GreatRebellion },
            { "heir", Heir },
            { "revolution", Revolution },
            { "coup", Rulers.Coup },
            { "loyal", Loyal },
            { "gold", Gold },
            { "bankrupt", Bankrupt },
            { "civil_war", k => G.Cities(k).Count >= 3 && Mandate.GreatRebellion(k) }
        };

        public static void Register()
        {
            foreach (var kv in Actions) Sandbox.Hooks[kv.Key] = kv.Value;
        }

        public static void Tools()
        {
            if (Sandbox.Tools.Count == 0 || Sandbox.ById.ContainsKey(Probe)) return;
            K("gov_vote", "sb_vote", k =>
            {
                if (Parl.Current(k) == null) return false;
                Parl.VoteNow(k);
                return true;
            });
            K("gov_decree", "sb_decree", k =>
            {
                if (Parl.Current(k) == null) return false;
                Parl.Decree(k);
                return true;
            });
            K("gov_mandate", "sb_mandate", k =>
            {
                if (!Mandate.Applies(k)) return false;
                Mandate.Push(k);
                return true;
            });
            K("gov_election", "sb_election", k => Gov.Republic(k) && Rulers.Hold(k));
            K("gov_empire", "sb_empire", Rulers.ProclaimEmpire);
            K("gov_vassal", "sb_vassal", k =>
            {
                if (!G.Ok(_lordPick) || _lordPick == k)
                {
                    _lordPick = k;
                    Notify.Show(Txt.F("civ_vas_sb_pick", G.Name(k)));
                    return true;
                }
                Vassals.Make(k, _lordPick);
                _lordPick = null;
                return true;
            });
            K("gov_independence", "sb_independence", k =>
            {
                if (Diplomacy.Overlord(k) == null) return false;
                Vassals.Independence(k, true);
                return true;
            });
            Sandbox.Extra("city", "gov_transfer", Txt.T("civ_gov_sb_transfer"), Txt.T("civ_gov_sb_transfer_d"), "ui/Icons/civ/pol/sb_transfer", -1, t =>
            {
                City c = Sandbox.CityAt(t);
                if (!G.Ok(_cityPick))
                {
                    if (!G.Ok(c)) return false;
                    _cityPick = c;
                    return true;
                }
                Kingdom to = Sandbox.Owner(t);
                City from = _cityPick;
                _cityPick = null;
                if (!G.Ok(to) || from.kingdom == to) return false;
                Vassals.Busy = true;
                try
                {
                    from.joinAnotherKingdom(to, false, false);
                }
                finally
                {
                    Vassals.Busy = false;
                }
                return true;
            });
            Sandbox.Extra("city", "gov_release", Txt.T("civ_gov_sb_release"), Txt.T("civ_gov_sb_release_d"), "ui/Icons/civ/pol/sb_release", -1, t =>
            {
                City c = Sandbox.CityAt(t);
                return G.Ok(c) && G.Ok(c.kingdom) && Vassals.ReleaseAsVassal(c, c.kingdom, null, "release") != null;
            });
            Sandbox.Extra("city", "gov_army", Txt.T("civ_gov_sb_army"), Txt.T("civ_gov_sb_army_d"), "ui/Icons/civ/pol/sb_army", -1, t =>
            {
                City c = Sandbox.CityAt(t);
                if (!G.Ok(c) || c.kingdom == null) return false;
                Kingdom att = Player.Active && Player.K != c.kingdom ? Player.K : G.Kingdoms().Where(x => G.Ok(x) && x != c.kingdom && World.world.wars.isInWarWith(x, c.kingdom)).OrderByDescending(x => x.countTotalWarriors()).FirstOrDefault();
                return GovGuard.SendAll(att, c);
            });
        }

        private static void K(string id, string icon, Func<Kingdom, bool> f)
        {
            Sandbox.Extra("kingdom", id, Txt.T("civ_gov_sb_" + id.Substring(4)), Txt.T("civ_gov_sb_" + id.Substring(4) + "_d"), "ui/Icons/civ/pol/" + icon, -1, t =>
            {
                Kingdom k = Sandbox.Owner(t);
                if (!G.Ok(k) || !k.isCiv()) return false;
                bool ok = false;
                CivMod.Safe("gov-sb-" + id, () => ok = f(k));
                if (ok)
                {
                    Bonus.Dirty(k);
                    Profile.Invalidate(k);
                }
                return ok;
            });
        }

        private static bool Restore(Kingdom k)
        {
            if (!Mandate.Applies(k)) return false;
            if (Mandate.Phase(k) != 2) Mandate.SetPhase(k, 2, false);
            return Mandate.StartRestoration(k);
        }

        private static bool Heir(Kingdom k)
        {
            Actor h = Rulers.Succession(k).FirstOrDefault(a => a.isAdult());
            if (h == null) return false;
            Rulers.Crown(k, h);
            return true;
        }

        private static bool Revolution(Kingdom k)
        {
            if (!GovEra.Has(k, GovEra.Republic)) return false;
            Rulers.SetRegime(k, !Gov.Republic(k), "revolution");
            Politics.AddStability(k, -0.1f);
            if (Gov.Republic(k)) Rulers.Hold(k);
            return true;
        }

        private static bool Loyal(Kingdom k)
        {
            Mandate.Set(k, Math.Max(Mandate.Get(k), 85f));
            Politics.AddStability(k, 0.3f);
            foreach (City c in G.Cities(k)) Store.Put(c.data, Parl.KDecree, 0);
            foreach (long f in Fam.Seats(k).Keys) Fam.Remember(f, Fam.Royal(k), 20f);
            return true;
        }

        private static bool Gold(Kingdom k)
        {
            float v = 300f + GovEra.Of(k) * 40f;
            if (Treasury.Coin(k)) Treasury.Receive(k, v, "grant");
            else if (k.capital != null) Economy.Give(k.capital, "gold", (int)v);
            else return false;
            return true;
        }

        private static bool Bankrupt(Kingdom k)
        {
            Treasury.Bankrupt(k);
            if (k.capital != null) Economy.TakeUpTo(k.capital, "gold", Economy.Amount(k.capital, "gold"));
            return true;
        }
    }
}
