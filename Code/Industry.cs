using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class Industry
    {
        private const string KWork = "civ_industry_work";
        private static readonly Dictionary<Kingdom, Demand> _demand = new Dictionary<Kingdom, Demand>();
        private static string _world = "";
        private static int _year = int.MinValue;

        private sealed class Demand
        {
            public int Cities;
            public readonly Dictionary<string, int> Fuel = new Dictionary<string, int>();
        }

        private sealed class Job
        {
            public Recipe Recipe;
            public int Left;
            public int Done;
            public int Target;
            public bool Factory;
        }

        internal static int LocalReserve(string resource)
        {
            switch (resource)
            {
                case "civ_steel": return 16;
                case "civ_concrete": return 20;
                case "civ_bricks": return 12;
                case "civ_glass": return 8;
                case "civ_machinery": return 8;
                case "civ_electronics": return 8;
                case "civ_tools":
                case "civ_textiles":
                case "civ_clothes":
                case "civ_chemicals":
                case "civ_plastics":
                case "civ_paper":
                case "civ_powder":
                case "civ_medicine": return 4;
                case "civ_fuel":
                case "civ_ammo":
                case "civ_biomass": return 8;
                default: return Economy.Reserve(resource);
            }
        }

        private static Demand Needs(Kingdom kingdom)
        {
            if (_world != Sim.WorldId || _year != G.Year)
            {
                _world = Sim.WorldId;
                _year = G.Year;
                _demand.Clear();
            }
            Demand demand;
            if (_demand.TryGetValue(kingdom, out demand)) return demand;
            demand = new Demand();
            foreach (City city in G.Cities(kingdom))
            {
                demand.Cities++;
                foreach (Building building in city.buildings)
                {
                    if (building == null || !building.isUsable() || building.isUnderConstruction()) continue;
                    PlantDef plant = Energy.PlantOf(building);
                    if (plant != null && !string.IsNullOrEmpty(plant.Fuel)) Add(demand.Fuel, plant.Fuel, Energy.FuelUse(plant));
                }
                foreach (Actor actor in city.units)
                {
                    if (!G.Ok(actor)) continue;
                    UnitDef unit = Vehicles.Def(actor);
                    if (unit == null) continue;
                    string resource = unit.Fuel == "coal" ? "civ_coal" : unit.Fuel == "fuel" ? "civ_fuel" :
                        unit.Fuel == "uranium" ? "civ_uranium" : unit.Fuel == "bio" ? "civ_biomass" : null;
                    if (resource != null) Add(demand.Fuel, resource, 1);
                }
            }
            _demand[kingdom] = demand;
            return demand;
        }

        private static void Add(Dictionary<string, int> values, string key, int amount)
        {
            int before;
            values.TryGetValue(key, out before);
            values[key] = before + amount;
        }

        internal static int OperatingNeed(Kingdom kingdom, string resource)
        {
            if (kingdom == null) return 0;
            int need;
            return Needs(kingdom).Fuel.TryGetValue(resource, out need) ? need : 0;
        }

        public static void Process(City city, Kingdom kingdom, int years, Dictionary<string, float> report)
        {
            if (city == null || kingdom == null || years <= 0) return;
            var counts = new Dictionary<string, int>();
            foreach (Building building in city.buildings)
                if (building != null && building.asset != null && building.isUsable() && !building.isUnderConstruction())
                    Add(counts, building.asset.type, 1);
            int factories;
            counts.TryGetValue("type_civ_factory", out factories);
            float efficiency = factories <= 0 ? 0f : G.Clamp(Roles.Count(city, Roles.Worker) / (4f * factories), 0.25f, 1f);
            float power = Energy.ProductionFactor(city);
            float production = Bonus.Relative(kingdom, "prod");
            var stock = new Stock(city);
            var work = Store.Map(city.data, KWork);
            var outputs = new HashSet<string>();
            var jobs = new List<Job>();
            foreach (Recipe recipe in Economy.Recipes)
            {
                if (!Research.Has(kingdom, recipe.Tech) || outputs.Contains(recipe.Out)) continue;
                int buildings = 0;
                bool factory = false;
                foreach (string type in recipe.Buildings)
                {
                    int count;
                    counts.TryGetValue(type, out count);
                    if (type == "type_civ_factory") factory |= count > 0;
                    else buildings += count;
                }
                float capacity = buildings * recipe.Cap * (recipe.Power ? power : 1f);
                if (factory) capacity += factories * recipe.Cap * 3f * efficiency * power;
                capacity *= production * Catchup.Boost(city);
                if (capacity <= 0f) continue;
                outputs.Add(recipe.Out);
                int target = Math.Max(LocalReserve(recipe.Out) * 3, recipe.Cap * 4) * Math.Max(1, Needs(kingdom).Cities);
                target = Math.Max(target, (int)Math.Ceiling(capacity * 3f));
                target = Math.Min(Storage.Cap, Math.Max(target, OperatingNeed(kingdom, recipe.Out) * 3));
                string key = recipe.Tech + "|" + recipe.Out;
                if (stock.Amount(recipe.Out) >= target)
                {
                    work[key] = 0f;
                    continue;
                }
                float availableWork = capacity * years / Math.Max(1, recipe.OutAmount) + G.Clamp(Store.Get(work, key), 0f, 0.999999f);
                int batches = (int)availableWork;
                work[key] = availableWork - batches;
                jobs.Add(new Job { Recipe = recipe, Left = batches, Target = target, Factory = factory });
            }
            if (jobs.Count > 0)
            {
                int first = Math.Abs(G.Year % jobs.Count);
                bool progress;
                do
                {
                    progress = false;
                    for (int i = 0; i < jobs.Count; i++)
                    {
                        Job job = jobs[(first + i) % jobs.Count];
                        Recipe recipe = job.Recipe;
                        if (job.Left <= 0 || stock.Amount(recipe.Out) + recipe.OutAmount > job.Target || !stock.Consume(recipe)) continue;
                        job.Left--;
                        job.Done++;
                        stock.Produce(recipe.Out, recipe.OutAmount);
                        progress = true;
                    }
                    first = (first + 1) % jobs.Count;
                }
                while (progress);
            }
            if (!stock.Commit()) return;
            Store.Map(city.data, KWork, work);
            foreach (Job job in jobs)
            {
                if (job.Done <= 0) continue;
                Store.Add(report, job.Recipe.Out, job.Done * job.Recipe.OutAmount);
                if (job.Factory) Pollution.Emit(city, 0.004f * job.Done * Bonus.Mult(kingdom, "pollution"));
            }
        }

        private sealed class Stock
        {
            private readonly City _city;
            private readonly Kingdom _kingdom;
            private readonly Dictionary<string, float> _depot;
            private readonly Dictionary<string, int> _original = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _amounts = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _made = new Dictionary<string, int>();
            private readonly List<string> _food = new List<string>();

            public Stock(City city)
            {
                _city = city;
                _kingdom = city.kingdom;
                _depot = Economy.Depot(_kingdom);
                foreach (string resource in new[] { "civ_bricks", "civ_concrete", "civ_steel", "civ_glass", "civ_tools", "civ_machinery", "civ_electronics" })
                    Amount(resource);
                foreach (ResourceAsset resource in AssetManager.resources.list)
                    if (resource.food && !resource.id.StartsWith("civ_")) _food.Add(resource.id);
            }

            public int Amount(string resource)
            {
                int amount;
                if (_amounts.TryGetValue(resource, out amount)) return amount;
                int local = _city.getResourcesAmount(resource);
                amount = local + Math.Max(0, (int)Store.Get(_depot, resource));
                _original[resource] = amount;
                _amounts[resource] = amount;
                return amount;
            }

            private int Keep(string resource)
            {
                Amount(resource);
                int native = Economy.Reserve(resource);
                if (native > 0) return native;
                int made;
                _made.TryGetValue(resource, out made);
                return Math.Min(LocalReserve(resource), (_original[resource] + made + 1) / 2);
            }

            private int Available(string resource, Dictionary<string, int> chosen)
            {
                int selected;
                chosen.TryGetValue(resource, out selected);
                return Math.Max(0, Amount(resource) - Keep(resource) - OperatingNeed(_kingdom, resource) - selected);
            }

            private int FoodAvailable(Dictionary<string, int> chosen)
            {
                int total = 0;
                foreach (string resource in _food) total += Available(resource, chosen);
                return total;
            }

            public bool Consume(Recipe recipe)
            {
                var chosen = new Dictionary<string, int>();
                bool food = false;
                for (int i = 0; i < recipe.Inputs.Length; i++)
                {
                    bool found = false;
                    int need = recipe.Amounts[i];
                    foreach (string resource in recipe.Inputs[i])
                    {
                        if (resource == "@food")
                        {
                            if (FoodAvailable(chosen) < need + 10) continue;
                            int left = need;
                            foreach (string item in _food)
                            {
                                int take = Math.Min(left, Available(item, chosen));
                                if (take > 0) Add(chosen, item, take);
                                left -= take;
                                if (left <= 0) break;
                            }
                            food = true;
                            found = left == 0;
                        }
                        else if (Available(resource, chosen) >= need)
                        {
                            Add(chosen, resource, need);
                            found = true;
                        }
                        if (found) break;
                    }
                    if (!found) return false;
                }
                if (food && FoodAvailable(chosen) < 10) return false;
                foreach (var input in chosen) _amounts[input.Key] -= input.Value;
                return true;
            }

            public void Produce(string resource, int amount)
            {
                _amounts[resource] = Amount(resource) + amount;
                Add(_made, resource, amount);
            }

            public bool Commit()
            {
                if (_city.kingdom != _kingdom) return false;
                foreach (var amount in _amounts)
                {
                    int spend = _original[amount.Key] - amount.Value;
                    if (spend > 0 && !Economy.Has(_city, amount.Key, spend)) return false;
                }
                var spent = new Dictionary<string, int>();
                foreach (var amount in _amounts)
                {
                    int spend = _original[amount.Key] - amount.Value;
                    if (spend <= 0) continue;
                    if (!Economy.Take(_city, amount.Key, spend, true))
                    {
                        foreach (var refund in spent) Economy.Give(_city, refund.Key, refund.Value);
                        return false;
                    }
                    spent[amount.Key] = spend;
                }
                foreach (var amount in _amounts)
                {
                    int made = amount.Value - _original[amount.Key];
                    if (made > 0) Economy.Give(_city, amount.Key, made);
                }
                foreach (var amount in _amounts)
                {
                    int keep = Keep(amount.Key);
                    Economy.RefillLocal(_city, amount.Key, keep);
                    Economy.ShareSurplus(_city, amount.Key, keep, OperatingNeed(_kingdom, amount.Key));
                }
                return true;
            }
        }
    }
}
