using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class Legacy
    {
        private const string Key = "civ_migrated2";
        private static readonly int[] HouseEra = { 0, 1, 3, 5, 7, 8, 9, 10, 11, 12, 13 };

        public static int EstimateEra(Kingdom k)
        {
            int tier = 0;
            int era = 0;
            foreach (City c in G.Cities(k))
            {
                foreach (Building b in c.buildings)
                {
                    if (b == null || b.asset == null) continue;
                    tier = Math.Max(tier, Buildings.HouseTier(b.asset));
                }
                if (c.hasBuildingType("type_library")) era = Math.Max(era, 3);
                if (c.hasBuildingType("type_barracks")) era = Math.Max(era, 2);
                if (c.hasBuildingType("type_temple")) era = Math.Max(era, 2);
            }
            era = Math.Max(era, HouseEra[Math.Max(0, Math.Min(tier, HouseEra.Length - 1))]);
            int old = Store.Int(k.data, "civ_era", -1);
            if (old > era) era = old;
            return Math.Min(era, Db.Eras.Count - 1);
        }

        public static void RebuildTechs(Kingdom k)
        {
            if (!G.Ok(k)) return;
            int era = EstimateEra(k);
            if (era > 0) Research.SetupEra(k, era);
        }

        public static void Migrate()
        {
            SaveCustomData d = Store.World();
            if (d == null || Store.Bool(d, Key)) return;
            Store.Put(d, Key, true);
            int changed = 0;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv() || Research.Known(k).Count > 0) continue;
                int era = EstimateEra(k);
                if (era <= 0) continue;
                Research.SetupEra(k, era);
                changed++;
            }
            foreach (Kingdom k in G.Kingdoms()) if (k.isCiv()) Policies.Ensure(k);
            if (changed > 0) Notify.Show(Txt.F("legacy_migrated", changed));
        }
    }
}
