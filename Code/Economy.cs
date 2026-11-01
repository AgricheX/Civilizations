using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ai.behaviours;

namespace Civilizations
{
    internal class Recipe
    {
        public string Out;
        public int OutAmount;
        public string Tech;
        public string[] Buildings;
        public string[][] Inputs;
        public int[] Amounts;
        public int Cap;
        public bool Power;
    }

    internal static class Economy
    {
        public const string KDepot = "civ_depot";
        public const string KFamine = "civ_famine";
        public const string KTrade = "civ_trade";
        public const string KReport = "civ_eco";

        public static readonly List<Recipe> Recipes = new List<Recipe>();
        private static readonly string[] Diets = { "diet_crops", "diet_fish", "diet_flowers", "diet_fruits", "diet_meat", "diet_vegetation" };
        private static readonly Dictionary<string, string> GameplaySprite = new Dictionary<string, string>
        {
            { "civ_textiles", "leather" }, { "civ_clothes", "leather" }, { "civ_coal", "stone" }, { "civ_oil", "bag_default" }, { "civ_fuel", "bag_default" },
            { "civ_steel", "common_metals" }, { "civ_bricks", "stone" }, { "civ_concrete", "stone" }, { "civ_glass", "gems" }, { "civ_paper", "wood" },
            { "civ_tools", "common_metals" }, { "civ_machinery", "common_metals" }, { "civ_electronics", "gems" }, { "civ_plastics", "bag_default" },
            { "civ_chemicals", "bag_default" }, { "civ_medicine", "bag_default" }, { "civ_ammo", "common_metals" }, { "civ_uranium", "gems" },
            { "civ_powder", "bag_default" }, { "civ_canned", "bag_food" }, { "civ_frozen", "bag_food" }, { "civ_biomass", "wood" }
        };

        public static void InitAssets()
        {
            ResourceLibrary lib = AssetManager.resources;
            foreach (string id in Db.Resources)
            {
                if (lib.get(id) != null) continue;
                bool food = id == "civ_canned" || id == "civ_frozen";
                ResourceAsset r;
                if (food)
                {
                    r = lib.clone(id, "$TEMPLATE_FOOD$");
                    r.restore_nutrition = id == "civ_canned" ? 60 : 70;
                    r.restore_health = 0.1f;
                    r.restore_happiness = 4;
                    r.tastiness = 0.4f;
                    r.diet = Diets;
                    r.favorite_food_chance = 0.05f;
                }
                else
                {
                    r = new ResourceAsset { id = id, type = ResType.Strategic };
                    lib.add(r);
                }
                r.path_icon = "civ/res/" + Db.ResourceArt[id];
                string sprite;
                r.path_gameplay_sprite = GameplaySprite.TryGetValue(id, out sprite) ? sprite : "bag_default";
                r.storage_max = 300;
                r.maximum = 9999;
                r.supply_bound_give = 60;
                r.supply_bound_take = 10;
                r.trade_bound = 40;
            }
            int order = 0;
            foreach (ResourceAsset r in lib.list)
            {
                r.order = order++;
                if (string.IsNullOrEmpty(r.full_sprite_path)) r.full_sprite_path = "items/resources/" + r.path_gameplay_sprite;
            }
            foreach (string id in Db.Resources)
            {
                ResourceAsset r = lib.get(id);
                if (r == null) continue;
                r.full_sprite_path = "items/resources/" + r.path_gameplay_sprite;
                r.gameplay_sprites = SpriteTextureLoader.getSpriteList(r.full_sprite_path);
                if (r.type == ResType.Strategic && !lib.strategic_resource_assets.Contains(r)) lib.strategic_resource_assets.Add(r);
                if (r.diet == null) continue;
                foreach (SubspeciesTrait st in AssetManager.subspecies_traits.list)
                {
                    if (!st.is_diet_related || st.base_stats_meta == null) continue;
                    foreach (string tag in r.diet)
                    {
                        if (!st.base_stats_meta.hasTag(tag)) continue;
                        List<string> v;
                        if (!lib.diet_food_pools.TryGetValue(st.id, out v))
                        {
                            v = new List<string>();
                            lib.diet_food_pools.Add(st.id, v);
                        }
                        if (!v.Contains(r.id)) v.Add(r.id);
                    }
                }
            }
            foreach (ResourceAsset r in lib.list)
            {
                if (r.id == "wood" || r.id == "stone" || r.id == "common_metals" || r.id == "gold")
                {
                    r.maximum = Math.Max(r.maximum, 4999);
                    r.storage_max = Math.Max(r.storage_max, 200);
                }
            }

            Recipes.Clear();
            Rc("civ_textiles", 2, "weaving", "type_hall,type_civ_factory", 4, false, In("leather,wheat", 1));
            Rc("civ_clothes", 1, "bone_needle", "type_hall,type_civ_factory", 5, false, In("civ_textiles,leather", 1));
            Rc("civ_bricks", 2, "fired_brick", "type_civ_kiln,type_civ_factory", 6, false, In("stone", 2), In("wood,civ_coal", 1));
            Rc("civ_glass", 1, "glassmaking", "type_civ_kiln,type_civ_factory", 3, false, In("stone", 2), In("wood,civ_coal", 1));
            Rc("civ_paper", 2, "paper", "type_hall,type_civ_factory", 4, false, In("wood", 2));
            Rc("civ_tools", 1, "copper_tools", "type_civ_smelter,type_civ_factory", 3, false, In("common_metals,civ_steel", 1), In("wood", 1));
            Rc("civ_steel", 1, "crucible_steel", "type_civ_smelter,type_civ_factory", 3, false, In("common_metals", 2), In("civ_coal,wood", 1));
            Rc("civ_concrete", 2, "roman_concrete", "type_civ_kiln,type_civ_factory", 4, false, In("stone", 2), In("civ_coal,wood", 1));
            Rc("civ_powder", 2, "gunpowder", "type_civ_smelter,type_civ_factory", 4, false, In("civ_coal,wood", 1));
            Rc("civ_ammo", 3, "arquebus", "type_civ_smelter,type_civ_factory", 6, false, In("civ_powder", 1), In("common_metals,civ_steel", 1));
            Rc("civ_machinery", 1, "machine_tools", "type_civ_factory", 3, true, In("civ_steel", 2), In("civ_tools", 1));
            Rc("civ_chemicals", 2, "chemical_industry", "type_civ_factory,type_civ_lab", 4, true, In("civ_coal,civ_oil", 1));
            Rc("civ_fuel", 2, "oil_refining", "type_civ_factory", 8, true, In("civ_oil", 2));
            Rc("civ_fuel", 1, "diesel_engine", "type_civ_factory", 6, true, In("civ_oil", 2));
            Rc("civ_plastics", 2, "plastics", "type_civ_factory", 4, true, In("civ_oil", 1));
            Rc("civ_electronics", 1, "transistor", "type_civ_factory,type_civ_lab", 3, true, In("civ_machinery", 1), In("civ_chemicals,civ_glass", 1));
            Rc("civ_medicine", 2, "apothecaries", "type_civ_hospital,type_civ_lab", 3, false, In("civ_chemicals,wheat", 1));
            Rc("civ_canned", 2, "canning", "type_civ_factory", 8, true, In("@food", 3));
            Rc("civ_frozen", 3, "refrigeration", "type_civ_factory", 8, true, In("@food", 3));
            Rc("civ_biomass", 3, "synthetic_crops", "type_civ_factory,type_civ_lab", 6, true, In("@food", 2));
        }

        private static KeyValuePair<string[], int> In(string ids, int amount)
        {
            return new KeyValuePair<string[], int>(ids.Split(','), amount);
        }

        private static void Rc(string output, int outAmount, string tech, string buildings, int cap, bool power, params KeyValuePair<string[], int>[] inputs)
        {
            Recipes.Add(new Recipe
            {
                Out = output,
                OutAmount = outAmount,
                Tech = tech,
                Buildings = buildings.Split(','),
                Inputs = inputs.Select(x => x.Key).ToArray(),
                Amounts = inputs.Select(x => x.Value).ToArray(),
                Cap = cap,
                Power = power
            });
        }

        public static bool IsFamine(City c)
        {
            return c != null && c.data != null && Store.Bool(c.data, KFamine);
        }

        public static int Amount(City c, string res)
        {
            if (c == null) return 0;
            int n = c.getResourcesAmount(res);
            if (c.kingdom != null && c.kingdom.data != null) n += Math.Max(0, (int)Store.Get(Store.Map(c.kingdom.data, KDepot), res));
            return n;
        }

        public static bool Has(City c, string res, int amount)
        {
            return amount <= 0 || Amount(c, res) >= amount;
        }

        public static bool Take(City c, string res, int amount)
        {
            return Take(c, res, amount, false);
        }

        internal static bool Take(City c, string res, int amount, bool sharedFirst)
        {
            if (amount <= 0) return true;
            if (!Has(c, res, amount)) return false;
            int shared = c.kingdom == null ? 0 : Math.Min(amount, Math.Max(0, (int)Store.Get(Depot(c.kingdom), res)));
            int local = Math.Min(c.getResourcesAmount(res), sharedFirst ? amount - shared : amount);
            shared = amount - local;
            if (shared > 0 && c.kingdom != null && c.kingdom.data != null)
            {
                var depot = Store.Map(c.kingdom.data, KDepot);
                Store.Add(depot, res, -shared);
                Store.Map(c.kingdom.data, KDepot, depot);
            }
            if (local > 0) c.takeResource(res, local);
            return true;
        }

        public static int TakeUpTo(City c, string res, int amount)
        {
            return TakeUpTo(c, res, amount, false);
        }

        internal static int TakeUpTo(City c, string res, int amount, bool sharedFirst)
        {
            int have = Math.Max(0, Math.Min(amount, Amount(c, res)));
            if (have > 0 && !Take(c, res, have, sharedFirst)) return 0;
            return have;
        }

        public static void Give(City c, string res, int amount)
        {
            if (c == null || amount <= 0) return;
            int rest = amount - StoreLocal(c, res, amount);
            if (rest > 0 && c.kingdom != null && c.kingdom.data != null)
            {
                var depot = Store.Map(c.kingdom.data, KDepot);
                Store.Add(depot, res, rest);
                Store.Map(c.kingdom.data, KDepot, depot);
            }
            ShareSurplus(c, res, Industry.LocalReserve(res));
        }

        private static int StoreLocal(City c, string res, int amount)
        {
            ResourceAsset asset = AssetManager.resources.get(res);
            if (asset == null || c.stockpiles == null || amount <= 0) return 0;
            int left = amount;
            foreach (Building b in c.stockpiles)
            {
                if (b == null || !b.isUsable() || b.resources == null) continue;
                int before = b.getResourcesAmount(res);
                int capacity = Math.Min(asset.maximum, Storage.BuildingHard(b, asset));
                int accept = Math.Min(left, Math.Max(0, capacity - before));
                if (accept <= 0) continue;
                b.addResources(res, accept);
                int added = Math.Max(0, Math.Min(accept, b.getResourcesAmount(res) - before));
                left -= added;
                if (left <= 0) break;
            }
            if (left < amount) c._storage_version++;
            return amount - left;
        }

        internal static bool SharedResource(string res)
        {
            ResourceAsset asset = AssetManager.resources.get(res);
            return asset != null && res.StartsWith("civ_") && !asset.food;
        }

        internal static void RefillLocal(City c, string res, int minimum)
        {
            if (c.kingdom == null || c.kingdom.data == null) return;
            int missing = minimum - c.getResourcesAmount(res);
            if (missing <= 0) return;
            var depot = Depot(c.kingdom);
            int request = Math.Min(missing, Math.Max(0, (int)Store.Get(depot, res)));
            int moved = StoreLocal(c, res, request);
            if (moved <= 0) return;
            Store.Add(depot, res, -moved);
            Store.Map(c.kingdom.data, KDepot, depot);
        }

        internal static void ShareSurplus(City c, string res, int minimum, int operating = 0)
        {
            if (c.kingdom == null || c.kingdom.data == null || !SharedResource(res)) return;
            var depot = Depot(c.kingdom);
            int local = c.getResourcesAmount(res);
            int shared = Math.Max(0, (int)Store.Get(depot, res));
            int target = Math.Max(minimum, Math.Min(120, (local + shared + 1) / 2));
            int move = Math.Max(0, local - target);
            move = Math.Max(move, Math.Min(Math.Max(0, local - minimum), Math.Max(0, operating - shared)));
            if (move <= 0) return;
            c.takeResource(res, move);
            Store.Add(depot, res, move);
            Store.Map(c.kingdom.data, KDepot, depot);
        }

        private static int FoodTotal(City c)
        {
            try
            {
                return c.countFoodTotal();
            }
            catch (Exception)
            {
                return c.getTotalFood();
            }
        }



        public static void Year(City c, int yrs)
        {
            Kingdom k = c.kingdom;
            int adults = Math.Max(0, c.status.population_adults);
            var report = new Dictionary<string, float>();
            Extract(c, k, adults, yrs, report);
            Food(c, k, adults, yrs, report);
            Process(c, k, yrs, report);
            Consume(c, k, adults, yrs, report);
            bool famine = c.status.population > 0 && FoodTotal(c) < Math.Max(3, adults / 4) && c.status.hungry > c.status.population / 3;
            bool was = IsFamine(c);
            Store.Put(c.data, KFamine, famine);
            if (famine && !was) Internet.OnFamine(c);
            if (famine) Moods.Apply(c, "civ_famine", 0.5f);
            Store.Map(c.data, KReport, report);
        }

        private static void Extract(City c, Kingdom k, int adults, int yrs, Dictionary<string, float> rep)
        {
            int mines = c.countBuildingsType("type_mine");
            int wells = c.countBuildingsType("type_civ_oil_well");
            float tools = Has(c, "civ_tools", 1) ? 1.25f : 1f;
            tools *= Bonus.Relative(k, "prod");
            tools *= 1f + Xeno.SlaveShare(c) * 0.5f;
            if (Research.Has(k, "electricity")) tools *= 1f + 0.2f * Energy.Ratio(c);
            if (Research.Has(k, "hide_working")) Produce(c, "leather", adults * (Research.Has(k, "animal_husbandry") ? 0.06f : 0.03f), yrs, rep);
            if (Research.Has(k, "coal_mining")) Produce(c, "civ_coal", (mines * 4f + adults * 0.02f) * tools, yrs, rep);
            if (Research.Has(k, "petroleum") || Research.Has(k, "diesel_engine")) Produce(c, "civ_oil", wells * 8f, yrs, rep);
            if (Research.Has(k, "nuclear_fission")) Produce(c, "civ_uranium", mines * 0.6f * tools, yrs, rep);
            if (Research.Has(k, "gas_lighting") && !Research.Has(k, "coal_mining")) Produce(c, "civ_coal", mines * 1f, yrs, rep);
        }

        private static void Food(City c, Kingdom k, int adults, int yrs, Dictionary<string, float> rep)
        {
            float rate = 0.02f;
            if (Research.Has(k, "gathering")) rate += 0.01f;
            if (Research.Has(k, "agriculture")) rate += 0.03f;
            rate *= Bonus.Relative(k, "food");
            rate *= 1f + Xeno.SlaveShare(c) * 0.3f;
            string id = Research.Has(k, "agriculture") ? "bread" : "berries";
            Produce(c, id, adults * rate, yrs, rep);
            if (Research.Has(k, "animal_husbandry")) Produce(c, "meat", adults * 0.025f * Bonus.Relative(k, "food"), yrs, rep);
            if (Research.Has(k, "fishing_nets") && c.hasBuildingType("type_docks")) Produce(c, "fish", adults * 0.035f * Bonus.Relative(k, "food"), yrs, rep);
        }

        private static void Produce(City c, string res, float amount, int yrs, Dictionary<string, float> rep)
        {
            float total = amount * yrs;
            int whole = (int)total;
            if (G.Chance(total - whole)) whole++;
            if (whole <= 0) return;
            Give(c, res, whole);
            Store.Add(rep, res, whole);
        }

        private static void Process(City c, Kingdom k, int yrs, Dictionary<string, float> rep)
        {
            Industry.Process(c, k, yrs, rep);
        }



        internal static int Reserve(string id)
        {
            switch (id)
            {
                case "wood":
                case "stone":
                    return 25;
                case "gold":
                    return 60;
                case "common_metals":
                    return 8;
                case "wheat":
                    return 20;
            }
            return 0;
        }

        private static void Consume(City c, Kingdom k, int adults, int yrs, Dictionary<string, float> rep)
        {
            int fuelNeed = Vehicles.FuelNeed(c, "fuel") * yrs;
            int coalNeed = Vehicles.FuelNeed(c, "coal") * yrs;
            int uraniumNeed = Vehicles.FuelNeed(c, "uranium") * yrs;
            int bioNeed = Vehicles.FuelNeed(c, "bio") * yrs;
            float ok = 1f;
            if (fuelNeed > 0) ok = Math.Min(ok, (float)TakeUpTo(c, "civ_fuel", fuelNeed) / fuelNeed);
            if (coalNeed > 0) ok = Math.Min(ok, (float)TakeUpTo(c, "civ_coal", coalNeed) / coalNeed);
            if (uraniumNeed > 0) ok = Math.Min(ok, (float)TakeUpTo(c, "civ_uranium", uraniumNeed) / uraniumNeed);
            if (bioNeed > 0) ok = Math.Min(ok, (float)TakeUpTo(c, "civ_biomass", bioNeed) / bioNeed);
            if (Vehicles.FuelNeed(c, "electric") > 0) ok = Math.Min(ok, Energy.Ratio(c));
            Store.Put(c.data, "civ_fuel_ok", ok);
            if (Research.Has(k, "apothecaries"))
            {
                int need = Math.Max(1, adults / 30) * yrs;
                int had = TakeUpTo(c, "civ_medicine", need);
                Store.Put(c.data, "civ_med_ok", (float)had / need);
            }
        }

        public static float MedicineLevel(City c)
        {
            return G.Ok(c) ? Store.Flt(c.data, "civ_med_ok", 0f) : 0f;
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            var depot = Store.Map(k.data, KDepot);
            List<City> cities = G.Cities(k);
            foreach (string id in Db.Resources)
            {
                foreach (City c in cities)
                {
                    int have = c.getResourcesAmount(id);
                    if (have > 120)
                    {
                        int move = (have - 120) / 2;
                        c.takeResource(id, move);
                        Store.Add(depot, id, move);
                    }
                }
            }
            var trade = Store.Map(k.data, KTrade);
            foreach (string key in trade.Keys.ToList()) trade[key] *= (float)Math.Pow(0.85, yrs);
            Store.Map(k.data, KTrade, trade);
            Store.Map(k.data, KDepot, depot);
        }

        public static Dictionary<string, float> Depot(Kingdom k)
        {
            return k == null || k.data == null ? new Dictionary<string, float>() : Store.Map(k.data, KDepot);
        }

        public static IEnumerable<Kingdom> TradePartners(Kingdom k)
        {
            if (k == null || k.data == null) yield break;
            foreach (var kv in Store.Map(k.data, KTrade))
            {
                if (kv.Value < 1f) continue;
                long id;
                if (!long.TryParse(kv.Key, out id)) continue;
                Kingdom o = World.world.kingdoms.get(id);
                if (o != null && o.isAlive()) yield return o;
            }
        }

        public static int TradeOpinion(Kingdom a, Kingdom b)
        {
            if (a == null || b == null || a.data == null || b.data == null) return 0;
            float v = Store.Get(Store.Map(a.data, KTrade), b.data.id.ToString());
            return (int)Math.Min(25f, v * 0.5f);
        }

        public static void RecordTrade(Kingdom a, Kingdom b, float value)
        {
            if (a == null || b == null || a == b || a.data == null || b.data == null) return;
            var ma = Store.Map(a.data, KTrade);
            Store.Add(ma, b.data.id.ToString(), value);
            Store.Map(a.data, KTrade, ma);
            var mb = Store.Map(b.data, KTrade);
            Store.Add(mb, a.data.id.ToString(), value);
            Store.Map(b.data, KTrade, mb);
        }

        public static string[] Tradeable()
        {
            var list = new List<string> { "wood", "stone", "common_metals", "leather", "bread", "meat", "fish" };
            list.AddRange(Db.Resources);
            return list.ToArray();
        }

        public static bool PlanTrip(Actor merchant, int cap, out City dest, out string res, out int amount)
        {
            dest = null;
            res = null;
            amount = 0;
            City home = merchant.city;
            if (home == null || home.kingdom == null) return false;
            Kingdom k = home.kingdom;
            var candidates = new List<City>();
            foreach (City c in G.Cities())
            {
                if (c == home || c.kingdom == null || !c.kingdom.isCiv()) continue;
                if (c.kingdom != k && (k.isEnemy(c.kingdom) || World.world.wars.isInWarWith(k, c.kingdom))) continue;
                if (!Route(home, c)) continue;
                candidates.Add(c);
            }
            if (candidates.Count == 0) return false;
            string[] goods = Tradeable();
            float best = 0f;
            for (int i = 0; i < 6; i++)
            {
                City c = G.Pick(candidates);
                string g = G.Pick(goods);
                int mine = Amount(home, g);
                int theirs = Amount(c, g);
                if (mine < 15 || theirs > mine / 2) continue;
                float score = (mine - theirs) * (1f + G.Dist(home, c) / 60f);
                if (score > best)
                {
                    best = score;
                    dest = c;
                    res = g;
                }
            }
            if (dest == null) return false;
            int reserve = Math.Max(Reserve(res), Industry.LocalReserve(res)) + Industry.OperatingNeed(home.kingdom, res);
            amount = Math.Min(Math.Max(0, Amount(home, res) - reserve) / 3, cap);
            if (amount <= 0) return false;
            return Take(home, res, amount);
        }

        public static bool Route(City a, City b)
        {
            WorldTile ta = G.Tile(a);
            WorldTile tb = G.Tile(b);
            if (ta == null || tb == null) return false;
            if (ta.isSameIsland(tb)) return true;
            Kingdom k = a.kingdom;
            if (Research.HasFeature(k, "airline") && a.hasBuildingType("type_civ_airport") && b.hasBuildingType("type_civ_airport")) return true;
            return Research.Has(k, "the_sail") && a.hasBuildingType("type_docks") && b.hasBuildingType("type_docks");
        }

        public static void Deliver(Actor merchant, City dest, string res, int amount)
        {
            if (dest == null || amount <= 0) return;
            City home = merchant.city;
            Give(dest, res, amount);
            float dist = home != null ? G.Dist(home, dest) : 30f;
            Kingdom hk = home != null ? home.kingdom : merchant.kingdom;
            float roads = 1f + Infrastructure.RoadLevel(home) * 0.08f + (Rails.Connected(home, dest) ? 0.25f : 0f);
            float fx = dest.kingdom != null && hk != null ? Currency.Exchange(hk, dest.kingdom) : 1f;
            fx = G.Clamp(fx, 0.5f, 2f);
            int profit = (int)(amount * (0.3f + dist / 120f) * Bonus.Mult(hk, "trade") * roads / fx);
            if (dest.getResourcesAmount("gold") > profit + 40)
            {
                dest.takeResource("gold", profit);
                if (home != null) Give(home, "gold", profit);
            }
            else if (home != null)
            {
                Give(home, "gold", Math.Max(1, profit / 3));
            }
            merchant.data.money += Math.Max(1, profit / 5);
            if (hk != null && dest.kingdom != null && dest.kingdom != hk) RecordTrade(hk, dest.kingdom, amount / 5f);
            Infrastructure.AddDemand(home, dest, amount);
        }

        [HarmonyPatch(typeof(BehGetResourcesFromMine), "getRandomAssetFromPool")]
        private static class NoMagicOre
        {
            private static void Postfix(ref BuildingAsset __result)
            {
                if (__result == null) return;
                if (__result.id == "mineral_mythril" || __result.id == "mineral_adamantine")
                {
                    __result = AssetManager.buildings.get("mineral_metals") ?? AssetManager.buildings.get("mineral_stone");
                }
            }
        }

        [HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.execute))]
        private static class FamineStopsBuilding
        {
            private static bool Prefix(City pCity, ref BehResult __result)
            {
                if (pCity != null && IsFamine(pCity))
                {
                    __result = BehResult.Continue;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(BabyHelper), nameof(BabyHelper.isMetaLimitsReached))]
        private static class FamineStopsBirths
        {
            private static void Postfix(Actor pActor, ref bool __result)
            {
                if (__result || pActor == null || !pActor.hasCity()) return;
                City c = pActor.city;
                if (IsFamine(c)) __result = true;
                else if (Pollution.Level(c) > 0.3f && G.Chance(Pollution.Level(c) * 0.5f)) __result = true;
            }
        }

        [HarmonyPatch(typeof(City), "finishCapture")]
        private static class Loot
        {
            private static void Prefix(City __instance, Kingdom pNewKingdom)
            {
                try
                {
                    Kingdom old = __instance.kingdom;
                    if (old == null || pNewKingdom == null || old.data == null || pNewKingdom.data == null || old == pNewKingdom) return;
                    var depot = Store.Map(old.data, KDepot);
                    float share = __instance.isCapitalCity() ? 0.5f : 0.15f;
                    var mine = Store.Map(pNewKingdom.data, KDepot);
                    foreach (string key in depot.Keys.ToList())
                    {
                        float take = (float)Math.Floor(depot[key] * share);
                        if (take <= 0f) continue;
                        depot[key] -= take;
                        Store.Add(mine, key, take);
                    }
                    Store.Map(old.data, KDepot, depot);
                    Store.Map(pNewKingdom.data, KDepot, mine);
                    Internet.OnSack(__instance, old, pNewKingdom);
                    Buildings.OnSacked(__instance);
                    if (old.countCities() <= 1) Companies.OnConquest(pNewKingdom, old);
                }
                catch (Exception e)
                {
                    CivMod.Log("loot: " + e.Message);
                }
            }
        }
    }
}
