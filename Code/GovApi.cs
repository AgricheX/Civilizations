using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class GovApi
    {
        public static bool MakeVassal(Kingdom vassal, Kingdom lord)
        {
            if (!G.Ok(vassal) || !G.Ok(lord) || vassal == lord || Diplomacy.Overlord(lord) == vassal) return false;
            Vassals.Make(vassal, lord);
            return Diplomacy.Overlord(vassal) == lord;
        }

        public static Kingdom ReleaseCityAsVassal(City c)
        {
            if (!G.Ok(c) || c.kingdom == null) return null;
            return Vassals.ReleaseAsVassal(c, c.kingdom);
        }

        public static bool InciteIndependence(Kingdom vassal)
        {
            if (!G.Ok(vassal) || Diplomacy.Overlord(vassal) == null) return false;
            Vassals.Incite(vassal);
            return Diplomacy.Overlord(vassal) == null;
        }

        public static void PushCycle(Kingdom k)
        {
            Mandate.Push(k);
        }

        public static bool GreatRebellion(Kingdom k)
        {
            return Mandate.GreatRebellion(k);
        }

        public static bool GeneralCoup(Kingdom k)
        {
            return Mandate.GeneralCoup(k);
        }

        public static Alliance CreateAlliance(Kingdom a, Kingdom b)
        {
            return AllianceLife.Create(a, b, null);
        }

        public static bool DissolveAlliance(Kingdom k)
        {
            Alliance a = AllianceLife.Of(k);
            if (a == null) return false;
            AllianceLife.End(a, "sandbox");
            return true;
        }

        public static Alliance CoalitionAgainst(Kingdom target)
        {
            return AllianceLife.CoalitionAgainst(target);
        }

        public static bool TransferCity(City c, Kingdom to)
        {
            if (!G.Ok(c) || !G.Ok(to) || c.kingdom == to) return false;
            c.joinAnotherKingdom(to, false, false);
            Parliament.Invalidate(to);
            return c.kingdom == to;
        }

        public static void Annex(Kingdom by, Kingdom target)
        {
            Vassals.Annex(by, target);
        }

        public static void ForcePeace(Kingdom a, Kingdom b)
        {
            GovWar.ForcePeace(a, b);
        }

        public static global::War DeclareWar(Kingdom a, Kingdom b)
        {
            return Vassals.War(a, b, null);
        }

        public static void SetMandate(Kingdom k, float v)
        {
            Mandate.Set(k, v);
        }

        public static bool ProposeBill(Kingdom k)
        {
            return G.Ok(k) && Parliament.Propose(k);
        }

        public static void AddReserve(Kingdom k, float gold)
        {
            Treasury.AddReserve(k, gold);
        }

        public static void SetArmament(Kingdom k, int tier)
        {
            ArmyCommand.SetTier(k, tier);
        }
    }
}
