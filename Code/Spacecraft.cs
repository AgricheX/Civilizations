using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal class FleetMix
    {
        public int Year = -1;
        public float Total;
        public int Ships;
        public int Escorts;
        public int Capitals;
        public int Science;
        public int Builders;
        public int Colony;
        public int Freight;
        public int Troops;
        public readonly Dictionary<string, float> Fam = new Dictionary<string, float>();
        public readonly Dictionary<string, float> Def = new Dictionary<string, float>();

        public float Share(Dictionary<string, float> m, string key)
        {
            return Total <= 0f ? 0f : Store.Get(m, key) / Total;
        }
    }

    internal static class Spacecraft
    {
        private static readonly HashSet<string> CivilRoles = new HashSet<string> { "construction", "science", "colony", "freight", "troop" };
        private static readonly Dictionary<long, FleetMix> _mix = new Dictionary<long, FleetMix>();

        public static bool Civil(UnitDef d)
        {
            return d != null && d.Cls == "space" && CivilRoles.Contains(d.Role);
        }

        public static bool HasPad(City c)
        {
            return c != null && c.countBuildingsType("type_civ_launchpad") > 0;
        }

        public static void CityYear(City c, int yrs, List<Actor> have, bool war)
        {
            if (!HasPad(c) || c.kingdom == null) return;
            Kingdom k = c.kingdom;
            if (Research.Era(k) < 15) return;
            Vehicles.Produce(c, have, "space_construction", Megastructures.Of(k).Any(m => m.Progress < 1f) ? 2 : 1, Roles.Aviator, false);
            Vehicles.Produce(c, have, "space_science", 1, Roles.Aviator, false);
            Vehicles.Produce(c, have, "space_freight", 1 + c.getPopulationPeople() / 150, Roles.Aviator, false);
            Vehicles.Produce(c, have, "space_colony", Cosmos.Of(k).Any() || Research.Era(k) >= 18 ? 1 : 0, Roles.Aviator, false);
            Vehicles.Produce(c, have, "space_troop", war ? 2 : 0, Roles.Aviator, false);
            Vehicles.Fill(c, have, new[] { "space_escort", "space_line", "space_capital", "space_carrier" }, new[] { 4f, 2f, 1f, 1f }, Vehicles.Cap(12, war), Vehicles.PerYear(war, yrs), x => Roles.Aviator, war);
            if (c == k.capital) Economy.Give(c, "gold", TradeGold(k, yrs));
            Mix(k).Year = -1;
        }

        public static FleetMix Mix(Kingdom k)
        {
            FleetMix m;
            long id = k.data.id;
            if (_mix.TryGetValue(id, out m) && m.Year == G.Year) return m;
            m = new FleetMix { Year = G.Year };
            _mix[id] = m;
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "space") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor a in asset.units)
                {
                    if (!G.Ok(a) || a.kingdom != k) continue;
                    switch (d.Role)
                    {
                        case "science": m.Science++; continue;
                        case "construction": m.Builders++; continue;
                        case "colony": m.Colony++; continue;
                        case "freight": m.Freight++; continue;
                        case "troop": m.Troops++; continue;
                    }
                    float p = Power(d);
                    m.Total += p;
                    m.Ships++;
                    if (d.Role == "escort") m.Escorts++;
                    if (d.Role == "capital") m.Capitals++;
                    if (d.Fam.Length > 0) m.Fam[d.Fam] = Store.Get(m.Fam, d.Fam) + p;
                    if (d.Def.Length > 0) m.Def[d.Def] = Store.Get(m.Def, d.Def) + p;
                }
            }
            return m;
        }

        public static void Forget(Kingdom k)
        {
            if (k != null && k.data != null) _mix.Remove(k.data.id);
        }

        public static float Power(UnitDef d)
        {
            return (float)Math.Sqrt(Math.Max(1f, Store.Get(d.Stats, "health")) * Math.Max(1f, Store.Get(d.Stats, "damage"))) / 100f;
        }

        public static float Strength(Kingdom k)
        {
            FleetMix m = Mix(k);
            return m.Total * 0.25f + m.Troops * 0.5f;
        }

        public static float Edge(Kingdom a, Kingdom b)
        {
            FleetMix x = Mix(a), y = Mix(b);
            if (x.Total <= 0f || y.Total <= 0f) return 1f;
            float e = 0f;
            e += x.Share(x.Fam, "kinetic") * (y.Share(y.Def, "shield") - y.Share(y.Def, "armor"));
            e += x.Share(x.Fam, "energy") * (y.Share(y.Def, "armor") - y.Share(y.Def, "shield"));
            float big = y.Ships > 0 ? (float)y.Capitals / y.Ships : 0f;
            e += x.Share(x.Fam, "missile") * (big - y.Share(y.Def, "pointdef"));
            float bare = y.Ships > 0 ? 1f - Math.Min(1f, y.Escorts * 2f / y.Ships) : 0f;
            e += x.Share(x.Fam, "fighter") * (bare - y.Share(y.Def, "flak") - y.Share(y.Def, "pointdef") * 0.5f);
            return G.Clamp(1f + e * 0.6f, 0.5f, 1.8f);
        }

        public static float ResearchMult(Kingdom k)
        {
            if (Research.Era(k) < 15) return 1f;
            return 1f + Math.Min(0.15f, Mix(k).Science * 0.03f);
        }

        public static float BuildMult(Kingdom k)
        {
            return 1f + Math.Min(0.4f, Mix(k).Builders * 0.08f);
        }

        public static float ColonyMult(Kingdom k)
        {
            return 1f + Math.Min(0.6f, Mix(k).Colony * 0.15f);
        }

        public static int TradeGold(Kingdom k, int yrs)
        {
            return Math.Min(60, Mix(k).Freight * 4) * Math.Max(1, yrs);
        }
    }
}
