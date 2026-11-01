using System;

namespace Civilizations
{
    internal static class Pollution
    {
        public const string Key = "civ_poll";

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.PollutionOn; }
        }

        public static float Level(City c)
        {
            if (!On || c == null || c.data == null) return 0f;
            return Store.Flt(c.data, Key, 0f);
        }

        public static void Emit(City c, float v)
        {
            if (!On || c == null || c.data == null) return;
            Kingdom k = c.kingdom;
            if (k != null) v *= Math.Max(0.1f, Bonus.Mult(k, "pollution"));
            if (Research.HasFeature(k, "clean")) v *= 0.4f;
            Store.Put(c.data, Key, G.Clamp(Level(c) + v, 0f, 1f));
        }

        public static void Year(City c, int yrs)
        {
            if (!On) return;
            Kingdom k = c.kingdom;
            float emit = c.countBuildingsType("type_civ_factory") * 0.03f + c.countBuildingsType("type_civ_oil_well") * 0.02f;
            emit += (Vehicles.FuelNeed(c, "fuel") + Vehicles.FuelNeed(c, "coal")) * 0.002f;
            emit += Transit.Emissions(c);
            if (Research.Has(k, "electricity")) emit += c.getPopulationPeople() * 0.0002f;
            Emit(c, emit * yrs);
            float decay = 0.02f;
            if (Policies.Id(k, "ideo") == "environmentalism") decay *= 2f;
            if (Research.Has(k, "recycling")) decay *= 1.5f;
            if (Research.HasFeature(k, "clean")) decay *= 3f;
            float lvl = Math.Max(0f, Level(c) - decay * yrs);
            Store.Put(c.data, Key, lvl);
            if (lvl > 0.25f)
            {
                foreach (City n in c.neighbours_cities)
                {
                    if (n != null && n.isAlive() && n.data != null) Store.Put(n.data, Key, G.Clamp(Level(n) + lvl * 0.02f * yrs, 0f, 1f));
                }
                Moods.Apply(c, "civ_pollution", lvl * 0.3f);
            }
        }
    }
}
