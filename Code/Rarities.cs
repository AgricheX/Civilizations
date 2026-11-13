using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal class RareBoost
    {
        public string Tech;
        public string[] Cats;
        public string[] Ids;
        public float Mult = 1f;
        public int Lead;
        public int MaxEra = 99;
    }

    internal static class Rarities
    {
        private static readonly List<RareBoost> Boosts = new List<RareBoost>();
        private static readonly Dictionary<string, int> UniqueMax = new Dictionary<string, int>
        {
            { "civ_dromon", 2 }, { "civ_davinci_tank", 2 }, { "civ_davinci_flyer", 2 }, { "civ_turtle_ship", 2 }, { "civ_turtle_sub", 1 },
            { "civ_death_ray", 1 }, { "civ_habbakuk", 1 }, { "civ_avrocar", 2 }
        };
        private static readonly Dictionary<string, int> UniqueRetire = new Dictionary<string, int>
        {
            { "civ_dromon", 9 }, { "civ_davinci_tank", 11 }, { "civ_davinci_flyer", 11 }, { "civ_turtle_ship", 11 }, { "civ_turtle_sub", 12 },
            { "civ_death_ray", 15 }, { "civ_habbakuk", 14 }, { "civ_avrocar", 15 }
        };
        private static readonly Dictionary<long, KeyValuePair<int, List<RareBoost>>> _active = new Dictionary<long, KeyValuePair<int, List<RareBoost>>>();
        private static readonly Dictionary<long, KeyValuePair<int, bool>> _elephants = new Dictionary<long, KeyValuePair<int, bool>>();

        static Rarities()
        {
            B("pyrite_sparks", "sci,craft", null, 0.8f, 0, 3);
            B("returning_boomerang", "mil", null, 0.85f, 0, 5);
            B("vinca_signs", "comm", null, 0.8f, 1, 6);
            B("nebra_sky_disk", null, "astro,calendar,star,sundial,observ,telescope,navigation", 0.6f, 0);
            B("meteoric_iron", "metal", "iron", 0.75f, 1, 5);
            B("phaistos_disc", "comm", null, 0.75f, 0);
            B("wootz_steel", "metal", null, 0.7f, 0);
            B("baghdad_battery", null, "electr,battery,voltaic,galvan", 0.6f, 1);
            B("antikythera_mechanism", null, "astro,gear,clock,calendar,star,observ,telescope,navigation,orbit", 0.6f, 1);
            B("antikythera_mechanism", "sci", null, 0.9f, 0);
            B("aeolipile", null, "steam", 0.6f, 1);
            B("greek_fire", "naval", null, 0.9f, 0);
            B("damascus_steel", "metal", null, 0.8f, 0);
            B("al_jazari_automata", "ind", null, 0.8f, 1);
            B("vinland_voyages", "naval", null, 0.85f, 0);
            B("da_vinci_flying_machine", "air", null, 0.8f, 0);
            B("leyden_jar", null, "electr", 0.7f, 0);
            B("mechanical_turk", "comp", null, 0.8f, 0);
            B("turtle_submarine", null, "submarine", 0.7f, 0);
            B("babbage_engine", "comp", null, 0.6f, 1);
            B("atmospheric_railway", "trans", null, 0.7f, 0);
            B("colorado_springs_lab", null, "electr,radio,wireless,tesla", 0.7f, 0);
            B("dymaxion_car", "trans", null, 0.8f, 0);
            B("flettner_rotor_ship", "naval", null, 0.8f, 0);
            B("project_pluto", "mil", null, 0.85f, 0);
            B("avrocar", "air", null, 0.85f, 0);
            B("ford_nucleon", null, "nuclear,atom,fission,reactor", 0.7f, 0);
            B("project_orion", "space", null, 0.7f, 1);
            B("superconductors", "energy,comp", null, 0.75f, 0);
            B("quantum_internet", "comp,comm", null, 0.7f, 0);
            B("space_elevator", "space", null, 0.6f, 1);
            B("dyson_swarm", "energy", null, 0.8f, 0);
            B("antimatter", "energy", null, 0.7f, 1);
        }

        private static void B(string tech, string cats, string ids, float mult, int lead, int maxEra = 99)
        {
            Boosts.Add(new RareBoost { Tech = tech, Cats = cats != null ? cats.Split(',') : null, Ids = ids != null ? ids.Split(',') : null, Mult = mult, Lead = lead, MaxEra = maxEra });
        }

        public static void Clear()
        {
            _active.Clear();
            _elephants.Clear();
        }

        private static List<RareBoost> Active(Kingdom k)
        {
            if (!G.Ok(k)) return null;
            KeyValuePair<int, List<RareBoost>> c;
            if (_active.TryGetValue(k.data.id, out c) && c.Key == G.Year) return c.Value;
            var l = new List<RareBoost>();
            foreach (RareBoost b in Boosts) if (Research.Has(k, b.Tech)) l.Add(b);
            _active[k.data.id] = new KeyValuePair<int, List<RareBoost>>(G.Year, l);
            return l;
        }

        public static void Dirty(Kingdom k)
        {
            if (k != null && k.data != null) _active.Remove(k.data.id);
        }

        private static bool Matches(RareBoost b, TechDef t)
        {
            if (t.Era > b.MaxEra) return false;
            bool cat = b.Cats == null || Array.IndexOf(b.Cats, t.Cat) >= 0;
            if (!cat) return false;
            if (b.Ids == null) return true;
            foreach (string s in b.Ids) if (t.Id.Contains(s)) return true;
            return false;
        }

        public static float CostMult(Kingdom k, TechDef t)
        {
            List<RareBoost> l = Active(k);
            if (l == null || l.Count == 0 || t == null) return 1f;
            float m = 1f;
            foreach (RareBoost b in l) if (b.Mult < 1f && Matches(b, t)) m *= b.Mult;
            return Math.Max(0.35f, m);
        }

        public static int Lead(Kingdom k, TechDef t)
        {
            List<RareBoost> l = Active(k);
            if (l == null || l.Count == 0 || t == null) return 0;
            int lead = 0;
            foreach (RareBoost b in l) if (b.Lead > lead && Matches(b, t)) lead = b.Lead;
            return lead;
        }

        public static int FogBonus(Kingdom k)
        {
            int n = 0;
            if (Research.Has(k, "wolf_companions")) n += 2;
            if (Research.Has(k, "vinland_voyages")) n += 1;
            return n;
        }

        public static int ExtraTrips(Kingdom k)
        {
            return Research.Has(k, "vinland_voyages") ? 3 : 0;
        }

        public static float FestivalMult(Kingdom k)
        {
            float m = 1f;
            if (Research.Has(k, "mead")) m *= 1.6f;
            if (Research.Has(k, "beer_feasts")) m *= 1.6f;
            return m;
        }

        public static float RateMult(Kingdom k)
        {
            return Research.Has(k, "mind_upload") ? 1.25f : 1f;
        }

        public static bool NoFuel(Kingdom k)
        {
            return Research.Has(k, "cold_fusion");
        }

        public static float PlantMult(Kingdom k)
        {
            return Research.Has(k, "dyson_swarm") ? 1.5f : 1f;
        }

        public static bool IsUnique(UnitDef d)
        {
            return d != null && d.Role == "unique";
        }

        public static int Retire(UnitDef d)
        {
            int e;
            return d != null && UniqueRetire.TryGetValue(d.Id, out e) ? e : int.MaxValue;
        }

        public static bool ElephantLand(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            KeyValuePair<int, bool> c;
            if (_elephants.TryGetValue(k.data.id, out c) && G.Year - c.Key < 10) return c.Value;
            bool ok = false;
            foreach (City city in G.Cities(k))
            {
                foreach (TileZone z in city.zones)
                {
                    if (z == null || z.centerTile == null) continue;
                    BiomeAsset b = z.centerTile.getBiome();
                    if (b == null) continue;
                    string id = b.id ?? "";
                    if (id.Contains("savanna") || id.Contains("jungle"))
                    {
                        ok = true;
                        break;
                    }
                }
                if (ok) break;
            }
            _elephants[k.data.id] = new KeyValuePair<int, bool>(G.Year, ok);
            return ok;
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!Vehicles.On || !G.Ok(k) || !k.isCiv() || k.capital == null) return;
            int era = Research.Era(k);
            foreach (UnitDef d in Db.Units)
            {
                if (!IsUnique(d) || era >= Retire(d) || !Research.Has(k, d.Tech)) continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null) continue;
                int alive = 0;
                if (asset.units != null) foreach (Actor a in asset.units) if (G.Ok(a) && a.kingdom == k) alive++;
                int max;
                if (!UniqueMax.TryGetValue(d.Id, out max)) max = 1;
                if (alive >= max) continue;
                if (!G.Chance(Math.Min(1f, 0.25f * yrs))) continue;
                Actor made = d.Cls == "ship" ? Launch(k, d) : Build(k, d);
                if (made == null) continue;
                Internet.OnVehicle(made, d.Id);
                if (alive == 0 && G.Chance(0.5f)) Chronicle.Log("unique_unit", k, G.Name(k), Txt.T(d.Id), null, null, null, made.city, made, false);
                break;
            }
        }

        private static Actor Build(Kingdom k, UnitDef d)
        {
            City c = k.capital;
            if (!G.Ok(c) || !Vehicles.CanAfford(c, d)) return null;
            Actor v = Vehicles.SpawnFor(c, d.Id);
            if (v != null) Vehicles.PayFor(c, d);
            return v;
        }

        private static Actor Launch(Kingdom k, UnitDef d)
        {
            foreach (City c in G.Cities(k))
            {
                if (!Vehicles.CanAfford(c, d)) continue;
                foreach (Building b in c.buildings)
                {
                    if (b == null || !b.isAlive() || b.component_docks == null || b.component_docks.tiles_ocean == null || b.component_docks.tiles_ocean.Count == 0) continue;
                    WorldTile t = b.component_docks.tiles_ocean.GetRandom();
                    if (t == null || t.region == null || t.region.island == null || !t.region.island.goodForDocks()) continue;
                    Actor ship = World.world.units.createNewUnit(d.Id, t);
                    if (ship == null) return null;
                    b.component_docks.addBoatToDock(ship);
                    if (ship.kingdom != k) ship.setKingdom(k);
                    Store.Put(ship.data, Vehicles.KConscript, true);
                    Vehicles.PayFor(c, d);
                    return ship;
                }
            }
            return null;
        }
    }
}
