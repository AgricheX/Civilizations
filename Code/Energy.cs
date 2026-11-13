using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class Energy
    {
        public const string KRatio = "civ_pow";
        public const string KSupply = "civ_pow_s";
        public const string KDemand = "civ_pow_d";
        public const string KMix = "civ_pow_mix";

        private static readonly Dictionary<string, float> Consumers = new Dictionary<string, float>
        {
            { "civ_factory", 8f }, { "civ_lab", 6f }, { "civ_university", 3f }, { "civ_hospital", 3f }, { "civ_airport", 10f },
            { "civ_airfield", 3f }, { "civ_launchpad", 15f }, { "civ_metro", 6f }, { "civ_station", 4f }, { "civ_oil_well", 2f },
            { "civ_radar", 3f }, { "civ_antiair", 3f }, { "civ_silo", 2f }, { "civ_bunker", 1f }, { "civ_school", 1f },
            { "civ_company_hq", 3f }, { "civ_company_hq_2", 4f }, { "civ_bank", 1f }, { "civ_military_base", 3f }, { "civ_smelter", 2f }
        };

        private static readonly Dictionary<int, float> HouseDemand = new Dictionary<int, float>
        {
            { 6, 0.4f }, { 7, 1f }, { 8, 1.6f }, { 9, 2.4f }, { 10, 2.8f }
        };

        public static bool Started(Kingdom k)
        {
            return Research.Has(k, "steam_engine");
        }

        public static bool Grid(Kingdom k)
        {
            return Research.Has(k, "power_grid") || Research.HasFeature(k, "internet") && Research.Route(k) == "tesla";
        }

        public static float Ratio(City c)
        {
            if (!G.Ok(c)) return 1f;
            return Store.Flt(c.data, KRatio, 1f);
        }

        public static bool Blackout(City c)
        {
            return G.Ok(c) && Store.Flt(c.data, KDemand, 0f) > 0.5f && Ratio(c) < 0.55f;
        }

        public static float Supply(City c)
        {
            return G.Ok(c) ? Store.Flt(c.data, KSupply, 0f) : 0f;
        }

        public static float Demand(City c)
        {
            return G.Ok(c) ? Store.Flt(c.data, KDemand, 0f) : 0f;
        }

        private static Dictionary<string, PlantDef> _plantByAsset;
        private static readonly Dictionary<long, KeyValuePair<int, string>> _best = new Dictionary<long, KeyValuePair<int, string>>();
        private static readonly Dictionary<long, KeyValuePair<int, bool>> _needs = new Dictionary<long, KeyValuePair<int, bool>>();
        private static readonly Dictionary<long, KeyValuePair<int, City>> _deficit = new Dictionary<long, KeyValuePair<int, City>>();

        public static PlantDef PlantOf(Building b)
        {
            if (b == null || b.asset == null) return null;
            if (_plantByAsset == null) _plantByAsset = new Dictionary<string, PlantDef>();
            PlantDef p;
            if (_plantByAsset.TryGetValue(b.asset.id, out p)) return p;
            string id = Buildings.BaseId(b.asset.id);
            p = null;
            foreach (PlantDef d in Db.Plants) if (d.Id == id) p = d;
            _plantByAsset[b.asset.id] = p;
            return p;
        }

        public static void Clear()
        {
            _best.Clear();
            _needs.Clear();
            _deficit.Clear();
        }

        public static int PlantCount(City c)
        {
            int n = 0;
            foreach (Building b in c.buildings) if (PlantOf(b) != null) n++;
            return n;
        }

        public static float CityDemand(City c)
        {
            float d = 0f;
            foreach (Building b in c.buildings)
            {
                if (b == null || b.asset == null || !b.isUsable()) continue;
                string id = Buildings.BaseId(b.asset.id);
                float v;
                if (Consumers.TryGetValue(id, out v)) d += v;
                int tier = Buildings.HouseTier(b.asset);
                if (HouseDemand.TryGetValue(tier, out v)) d += v;
            }
            Kingdom k = c.kingdom;
            if (k != null && Research.HasFeature(k, "lights")) d += c.getPopulationPeople() * (Research.Has(k, "electricity") ? 0.06f : 0.02f);
            if (k != null && Research.HasFeature(k, "internet")) d += c.getPopulationPeople() * 0.04f;
            d += Transit.ElectricLoad(c);
            return d * (k != null ? 1f / Math.Max(0.5f, Bonus.Mult(k, "energy_eff")) : 1f);
        }

        public static float CityGeneration(City c, int yrs, Dictionary<string, float> mix)
        {
            float s = 0f;
            Kingdom k = c.kingdom;
            float eff = k != null ? Bonus.Mult(k, "prod") : 1f;
            foreach (Building b in c.buildings)
            {
                if (b == null || !b.isUsable() || b.isUnderConstruction()) continue;
                PlantDef p = PlantOf(b);
                if (p == null) continue;
                float output = p.Output * (k != null ? Rarities.PlantMult(k) : 1f);
                if (!string.IsNullOrEmpty(p.Fuel) && (k == null || !Rarities.NoFuel(k)))
                {
                    int need = FuelUse(p) * yrs;
                    int take = Economy.TakeUpTo(c, p.Fuel, need, true);
                    if (take <= 0)
                    {
                        output *= 0.1f;
                    }
                    else
                    {
                        output *= Math.Min(1f, (float)take / need);
                        if (p.Pollution > 0f) Pollution.Emit(c, p.Pollution * 0.01f * output / p.Output * yrs);
                    }
                }
                if (p.Id == "civ_plant_solar") output *= Solar(c);
                if (p.Id == "civ_plant_wind") output *= Wind(c);
                if (p.Id == "civ_plant_hydro") output *= Hydro(c);
                output *= eff;
                s += output;
                Store.Add(mix, p.Id, output);
            }
            return s;
        }

        internal static int FuelUse(PlantDef plant)
        {
            return plant == null || string.IsNullOrEmpty(plant.Fuel) ? 0 : Math.Max(1, (int)(plant.Output / 8f));
        }

        private static float Solar(City c)
        {
            WorldTile t = G.Tile(c);
            if (t == null || t.Type == null) return 0.8f;
            string id = t.Type.id;
            if (id.Contains("sand") || id.Contains("desert") || id.Contains("savanna")) return 1.4f;
            if (id.Contains("snow") || id.Contains("permafrost") || id.Contains("tundra")) return 0.5f;
            return 0.9f;
        }

        private static float Wind(City c)
        {
            WorldTile t = G.Tile(c);
            if (t == null) return 1f;
            int water = 0;
            foreach (WorldTile n in t.neighboursAll) if (n != null && n.Type != null && n.Type.ocean) water++;
            if (t.Type != null && (t.Type.mountains || t.Type.id.Contains("hills"))) return 1.4f;
            return 0.9f + water * 0.08f;
        }

        private static float Hydro(City c)
        {
            return Infrastructure.NearWater(c, 8) ? 1.2f : 0.3f;
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!Started(k)) return;
            List<City> cities = G.Cities(k);
            var supply = new Dictionary<City, float>();
            var demand = new Dictionary<City, float>();
            var mix = new Dictionary<string, float>();
            float ts = 0f, td = 0f;
            foreach (City c in cities)
            {
                float s = CityGeneration(c, yrs, mix);
                float d = CityDemand(c);
                supply[c] = s;
                demand[c] = d;
                ts += s;
                td += d;
            }
            bool grid = Grid(k);
            foreach (City c in cities)
            {
                float s = supply[c];
                float d = demand[c];
                float ratio;
                if (grid) ratio = td <= 0.01f ? 1f : Math.Min(1f, ts * 0.95f / td);
                else ratio = d <= 0.01f ? 1f : Math.Min(1f, s / d);
                bool before = Blackout(c);
                Store.Put(c.data, KSupply, grid ? ts * (d / Math.Max(0.01f, td)) : s);
                Store.Put(c.data, KDemand, d);
                Store.Put(c.data, KRatio, ratio);
                bool now = Blackout(c);
                if (now && !before) Internet.OnBlackout(c);
                if (now && !before && c.kingdom != null && c == c.kingdom.capital) Chronicle.Log("blackout", c.kingdom, G.Name(c.kingdom), G.Name(c), null, null, null, c, null, false);
            }
            Store.Map(k.data, KMix, mix);
            _deficit.Remove(k.data.id);
            foreach (City c in cities)
            {
                _needs.Remove(c.data.id);
                _best.Remove(c.data.id);
            }
            Store.Put(k.data, KSupply, ts);
            Store.Put(k.data, KDemand, td);
        }

        public static float KingdomSupply(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KSupply, 0f) : 0f;
        }

        public static float KingdomDemand(Kingdom k)
        {
            return G.Ok(k) ? Store.Flt(k.data, KDemand, 0f) : 0f;
        }

        public static Dictionary<string, float> Mix(Kingdom k)
        {
            return G.Ok(k) ? Store.Map(k.data, KMix) : new Dictionary<string, float>();
        }

        public static string BestPlant(City c)
        {
            if (!G.Ok(c)) return null;
            KeyValuePair<int, string> v;
            if (_best.TryGetValue(c.data.id, out v) && v.Key == G.Year) return v.Value;
            string r = ComputeBestPlant(c);
            _best[c.data.id] = new KeyValuePair<int, string>(G.Year, r);
            return r;
        }

        private static string ComputeBestPlant(City c)
        {
            Kingdom k = c.kingdom;
            if (k == null) return null;
            string route = Research.Route(k);
            string ideo = Policies.Id(k, "ideo");
            PlantDef best = null;
            float bw = 0f;
            foreach (PlantDef p in Db.Plants)
            {
                if (!Research.Has(k, p.Tech)) continue;
                if (!string.IsNullOrEmpty(p.Route) && p.Route != route) continue;
                float w = p.Output;
                if (!string.IsNullOrEmpty(p.Fuel))
                {
                    int have = Economy.Amount(c, p.Fuel);
                    w *= G.Clamp((float)have / FuelUse(p), 0.1f, 1f);
                }
                if (p.Id == "civ_plant_hydro" && !Infrastructure.NearWater(c, 8)) continue;
                if (p.Id == "civ_plant_solar") w *= Solar(c);
                if (p.Id == "civ_plant_wind") w *= Wind(c);
                if (ideo == "environmentalism") w *= p.Pollution > 0f ? 0.3f : 1.8f;
                if (p.Id == "civ_plant_nuclear" || p.Id == "civ_plant_atomic")
                {
                    if (ideo == "environmentalism") w *= 0.6f;
                    if (War.HasRadiationNear(c)) w *= 0.2f;
                }
                w *= UnityEngine.Random.Range(0.8f, 1.2f);
                if (w > bw)
                {
                    bw = w;
                    best = p;
                }
            }
            return best != null ? best.Id : null;
        }

        public static bool NeedsPlant(City c)
        {
            if (!G.Ok(c) || c.kingdom == null) return false;
            KeyValuePair<int, bool> v;
            if (_needs.TryGetValue(c.data.id, out v) && v.Key == G.Year) return v.Value;
            bool r = ComputeNeedsPlant(c);
            _needs[c.data.id] = new KeyValuePair<int, bool>(G.Year, r);
            return r;
        }

        private static bool ComputeNeedsPlant(City c)
        {
            if (!Started(c.kingdom)) return false;
            float d = Demand(c);
            float s = Supply(c);
            if (d <= 0.5f) return false;
            if (Grid(c.kingdom))
            {
                float kd = KingdomDemand(c.kingdom), ks = KingdomSupply(c.kingdom);
                return ks < kd * 1.1f && c == BiggestDeficit(c.kingdom);
            }
            return s < d * 1.1f;
        }

        private static City BiggestDeficit(Kingdom k)
        {
            KeyValuePair<int, City> v;
            if (_deficit.TryGetValue(k.data.id, out v) && v.Key == G.Year && G.Ok(v.Value)) return v.Value;
            City r = ComputeDeficit(k);
            _deficit[k.data.id] = new KeyValuePair<int, City>(G.Year, r);
            return r;
        }

        private static City ComputeDeficit(Kingdom k)
        {
            City best = null;
            float bd = float.MinValue;
            foreach (City c in G.Cities(k))
            {
                float d = Demand(c) - CityOwnGeneration(c);
                if (d > bd)
                {
                    bd = d;
                    best = c;
                }
            }
            return best;
        }

        private static float CityOwnGeneration(City c)
        {
            float s = 0f;
            foreach (Building b in c.buildings)
            {
                PlantDef p = PlantOf(b);
                if (p != null) s += p.Output;
            }
            return s;
        }

        public static float ProductionFactor(City c)
        {
            if (!G.Ok(c) || Demand(c) <= 0.5f) return 1f;
            return 0.35f + 0.65f * Ratio(c);
        }
    }
}
