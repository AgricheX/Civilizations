using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ai.behaviours;

namespace Civilizations
{
    internal class BuildDef
    {
        public string Id;
        public string Type;
        public string Tech;
        public string Order;
        public int Limit;
        public int Pop;
        public int Blds;
        public BuildingFundament Fund;
        public ConstructionCost Cost;
        public string[] Mats = new string[0];
        public int[] MatAmounts = new int[0];
        public int Health = 500;
        public bool Smoke;
        public bool Light = true;
        public bool Borders;
        public bool Tower;
        public string Projectile = "arrow";
        public float Scale = 0.25f;
        public string UpgradeTo = "";
        public int Housing;
        public bool Storage;
        public int Books;
        public bool Burnable;
        public bool Variants;
        public int Tier;
    }

    internal static class Buildings
    {
        public static readonly Dictionary<string, BuildDef> Defs = new Dictionary<string, BuildDef>();
        public static readonly Dictionary<string, string> TechOf = new Dictionary<string, string>();
        public static readonly Dictionary<string, KeyValuePair<string[], int[]>> Materials = new Dictionary<string, KeyValuePair<string[], int[]>>();
        public static readonly Dictionary<string, int> HouseTiers = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> MinPop = new Dictionary<string, int>();
        public static readonly HashSet<string> VariantBases = new HashSet<string>();
        public static readonly string[] VisualRoutes = { "steam", "tesla", "diesel", "atom", "bio" };

        private static readonly Dictionary<string, string> OrderTech = new Dictionary<string, string>
        {
            { "order_house_1", "mud_brick" }, { "order_house_2", "masonry" }, { "order_house_3", "timber_framing" },
            { "order_house_4", "humanism" }, { "order_house_5", "humanism" }, { "order_hall_1", "mud_brick" },
            { "order_hall_2", "masonry" }, { "order_windmill_0", "agriculture" }, { "order_windmill_1", "windmill" },
            { "order_docks_0", "dugout_canoe" }, { "order_docks_1", "the_sail" }, { "order_watch_tower", "palisades" },
            { "order_temple", "ancestor_worship" }, { "order_library", "tablet_archives" }, { "order_market", "marketplaces" },
            { "order_barracks", "standing_army" }, { "order_training_dummy", "standing_army" }, { "order_statue", "sculpture" },
            { "order_well", "public_wells" }, { "order_mine", "copper_smelting" }
        };

        public static string BaseId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            foreach (string r in VisualRoutes)
            {
                if (id.EndsWith("_" + r)) return id.Substring(0, id.Length - r.Length - 1);
            }
            return id;
        }

        public static void InitAssets()
        {
            Infrastructure.InitAssets();
            Defs.Clear();
            int[] hHousing = { 0, 5, 6, 8, 10, 12, 16, 22, 32, 46, 62 };
            int[] hHealth = { 0, 120, 200, 320, 420, 560, 700, 1000, 1500, 2200, 3000 };
            string[][] hMats =
            {
                null, new string[0], new[] { "civ_bricks" }, new[] { "civ_bricks" }, new[] { "civ_bricks", "civ_glass" }, new[] { "civ_bricks", "civ_glass" },
                new[] { "civ_bricks", "civ_steel" }, new[] { "civ_concrete", "civ_steel", "civ_glass" }, new[] { "civ_concrete", "civ_steel", "civ_glass" }, new[] { "civ_concrete", "civ_steel", "civ_glass" },
                new[] { "civ_concrete", "civ_steel", "civ_glass" }
            };
            int[][] hAmt = { null, new int[0], new[] { 2 }, new[] { 4 }, new[] { 4, 1 }, new[] { 5, 2 }, new[] { 8, 2 }, new[] { 8, 3, 2 }, new[] { 12, 5, 4 }, new[] { 16, 8, 8 }, new[] { 20, 10, 12 } };
            int topHouse = Db.Houses.Count > 0 ? Db.Houses.Max(x => x.Level) : 9;
            int topHall = Db.Halls.Count > 0 ? Db.Halls.Max(x => x.Level) : 6;
            foreach (LevelDef h in Db.Houses)
            {
                int lv = h.Level;
                var f = lv <= 2 ? new BuildingFundament(1, 1, 1, 0) : new BuildingFundament(1, 1, 2, 0);
                if (lv >= 7) f = new BuildingFundament(2, 2, 2, 0);
                string up = lv < topHouse ? "civ_house_" + (lv + 1) : "";
                var d = new BuildDef
                {
                    Id = "civ_house_" + lv, Type = "type_house", Tech = h.Tech, Order = "civ_order_house_" + lv, Fund = f,
                    Cost = new ConstructionCost(lv <= 4 ? 4 : 0, 4 + lv * 2, lv >= 6 ? lv - 4 : 0, lv * 4), Housing = hHousing[Math.Min(lv, hHousing.Length - 1)], Health = hHealth[Math.Min(lv, hHealth.Length - 1)],
                    UpgradeTo = up, Mats = hMats[Math.Min(lv, hMats.Length - 1)], MatAmounts = hAmt[Math.Min(lv, hAmt.Length - 1)], Pop = h.MinPop, Variants = lv >= 6, Tier = lv, Burnable = lv <= 4
                };
                Defs[d.Id] = d;
            }
            foreach (LevelDef h in Db.Halls)
            {
                int lv = h.Level;
                Defs["civ_hall_" + lv] = new BuildDef
                {
                    Id = "civ_hall_" + lv, Type = "type_hall", Tech = h.Tech, Order = "civ_order_hall_" + lv, Fund = new BuildingFundament(3, 3, 4, 0),
                    Cost = new ConstructionCost(0, 20, 4, 60), Housing = 10 + lv * 4, Health = 600 + lv * 300, UpgradeTo = lv < topHall ? "civ_hall_" + (lv + 1) : "",
                    Mats = lv >= 5 ? new[] { "civ_concrete", "civ_steel" } : new[] { "civ_bricks" }, MatAmounts = lv >= 5 ? new[] { 10, 4 } : new[] { 8 }, Storage = true, Books = 6,
                    Scale = 0.2f, Variants = lv >= 5, Tier = lv
                };
            }
            N("civ_kiln", "type_civ_kiln", 1, 12, 4, F(1, 1, 1), new ConstructionCost(2, 6), smoke: true, burnable: true);
            N("civ_smelter", "type_civ_smelter", 1, 20, 6, F(1, 1, 2), new ConstructionCost(2, 10, 1), smoke: true);
            N("civ_fortress", "type_civ_fortress", 1, 40, 12, F(2, 2, 2), new ConstructionCost(0, 30, 4, 20), health: 3000, tower: true, upgrade: "civ_castle", borders: true);
            N("civ_castle", "type_civ_fortress", 0, 0, 0, F(2, 2, 3), new ConstructionCost(0, 40, 6, 40), health: 5000, tower: true, upgrade: "civ_military_base");
            N("civ_military_base", "type_civ_fortress", 0, 0, 0, F(2, 2, 2), new ConstructionCost(0, 20, 10, 60), health: 6000, tower: true, projectile: "shotgun_bullet", mats: new[] { "civ_concrete", "civ_steel" }, amounts: new[] { 6, 6 }, variants: true);
            N("civ_aqueduct", "type_civ_aqueduct", 1, 40, 12, F(2, 2, 1), new ConstructionCost(0, 20, 0, 10), light: false);
            N("civ_bank", "type_civ_bank", 1, 60, 18, F(1, 1, 2), new ConstructionCost(0, 15, 2, 60), mats: new[] { "civ_bricks" }, amounts: new[] { 4 });
            N("civ_university", "type_civ_university", 1, 60, 18, F(2, 2, 2), new ConstructionCost(5, 20, 2, 40), mats: new[] { "civ_bricks", "civ_glass" }, amounts: new[] { 6, 2 }, books: 6);
            N("civ_hospital", "type_civ_hospital", 1, 50, 15, F(2, 2, 2), new ConstructionCost(0, 20, 2, 30), mats: new[] { "civ_bricks" }, amounts: new[] { 4 });
            N("civ_school", "type_civ_school", 2, 40, 12, F(1, 1, 2), new ConstructionCost(4, 10, 0, 15), books: 2);
            N("civ_factory", "type_civ_factory", 3, 50, 16, F(2, 2, 2), new ConstructionCost(5, 20, 6, 30), smoke: true, mats: new[] { "civ_bricks", "civ_steel" }, amounts: new[] { 6, 3 }, variants: true);
            N("civ_oil_well", "type_civ_oil_well", 2, 30, 12, F(0, 0, 1), new ConstructionCost(0, 4, 6, 20), smoke: true, mats: new[] { "civ_steel" }, amounts: new[] { 3 });
            N("civ_airfield", "type_civ_airfield", 1, 60, 18, F(3, 3, 2), new ConstructionCost(5, 10, 6, 40));
            N("civ_airport", "type_civ_airport", 1, 100, 28, F(4, 4, 3), new ConstructionCost(0, 30, 10, 100), mats: new[] { "civ_concrete", "civ_steel", "civ_glass" }, amounts: new[] { 12, 6, 4 }, variants: true);
            N("civ_radar", "type_civ_radar", 1, 60, 20, F(0, 0, 1), new ConstructionCost(0, 4, 6, 30), mats: new[] { "civ_electronics" }, amounts: new[] { 2 });
            N("civ_lab", "type_civ_lab", 1, 70, 22, F(1, 1, 2), new ConstructionCost(0, 15, 4, 50), mats: new[] { "civ_glass", "civ_tools" }, amounts: new[] { 4, 2 }, variants: true);
            N("civ_bunker", "type_civ_bunker", 2, 80, 25, F(1, 1, 1), new ConstructionCost(0, 20, 6, 40), health: 8000, light: false, mats: new[] { "civ_concrete", "civ_steel" }, amounts: new[] { 10, 4 });
            N("civ_antiair", "type_civ_antiair", 3, 60, 22, F(0, 0, 1), new ConstructionCost(0, 6, 6, 30), health: 2500, tower: true, projectile: "shotgun_bullet", borders: true, mats: new[] { "civ_steel" }, amounts: new[] { 3 });
            N("civ_nuclear_plant", "type_civ_power", 0, 0, 0, F(3, 3, 3), new ConstructionCost(0, 40, 20, 200), smoke: true, mats: new[] { "civ_concrete", "civ_steel" }, amounts: new[] { 20, 10 });
            N("civ_launchpad", "type_civ_launchpad", 1, 120, 30, F(3, 3, 3), new ConstructionCost(0, 30, 20, 200), mats: new[] { "civ_concrete", "civ_steel", "civ_electronics" }, amounts: new[] { 14, 10, 4 });
            N("civ_silo", "type_civ_silo", 2, 80, 25, F(1, 1, 1), new ConstructionCost(0, 20, 10, 120), health: 6000, light: false, mats: new[] { "civ_concrete", "civ_steel" }, amounts: new[] { 10, 6 });
            N("civ_station", "type_civ_station", 1, 40, 12, F(2, 2, 1), new ConstructionCost(4, 12, 4, 20), mats: new[] { "civ_bricks", "civ_steel" }, amounts: new[] { 4, 2 }, variants: true);
            N("civ_metro", "type_civ_metro", 3, 80, 25, F(0, 0, 1), new ConstructionCost(0, 8, 4, 20), light: true, mats: new[] { "civ_concrete", "civ_steel" }, amounts: new[] { 4, 2 });
            N("civ_company_hq", "type_civ_company_hq", 2, 60, 20, F(1, 1, 2), new ConstructionCost(0, 20, 6, 80), upgrade: "civ_company_hq_2", mats: new[] { "civ_glass", "civ_steel" }, amounts: new[] { 4, 4 }, variants: true);
            N("civ_company_hq_2", "type_civ_company_hq", 0, 0, 0, F(1, 1, 2), new ConstructionCost(0, 30, 10, 120), health: 1600, mats: new[] { "civ_glass", "civ_steel", "civ_concrete" }, amounts: new[] { 8, 6, 6 }, variants: true);
            N("civ_wall_0", "type_civ_wall", 0, 0, 0, F(0, 0, 0), new ConstructionCost(3), health: 400, light: false, burnable: true);
            N("civ_wall_1", "type_civ_wall", 0, 0, 0, F(0, 0, 0), new ConstructionCost(5), health: 800, light: false, burnable: true);
            N("civ_wall_2", "type_civ_wall", 0, 0, 0, F(0, 0, 0), new ConstructionCost(0, 4), health: 1600, light: false);
            N("civ_wall_3", "type_civ_wall", 0, 0, 0, F(0, 0, 0), new ConstructionCost(0, 6, 1), health: 2600, light: false);
            N("civ_wall_4", "type_civ_wall", 0, 0, 0, F(0, 0, 0), new ConstructionCost(0, 8, 2), health: 3600, light: false);
            N("civ_border_wall", "type_civ_wall", 0, 0, 0, F(0, 0, 0), new ConstructionCost(0, 4, 2), health: 5000, light: false, mats: new[] { "civ_concrete" }, amounts: new[] { 1 });
            foreach (PlantDef p in Db.Plants)
            {
                if (Defs.ContainsKey(p.Id)) continue;
                bool big = p.Output >= 40f;
                N(p.Id, "type_civ_power", 0, 0, 0, big ? F(2, 2, 2) : F(1, 1, 2), new ConstructionCost(2, 12, 6, (int)(p.Output * 2)), smoke: p.Pollution > 0f, mats: new[] { "civ_steel" }, amounts: new[] { Math.Max(1, (int)(p.Output / 10f)) });
            }
            foreach (BuildingDef bd in Db.Buildings)
            {
                BuildDef d;
                if (Defs.TryGetValue(bd.Id, out d)) d.Tech = bd.Tech;
            }
            foreach (PlantDef p in Db.Plants) Defs[p.Id].Tech = p.Tech;
            foreach (BuildDef d in Defs.Values.ToList())
            {
                Register(d, d.Id);
                if (!d.Variants) continue;
                VariantBases.Add(d.Id);
                foreach (string r in VisualRoutes) Register(d, d.Id + "_" + r);
            }
            Docks.InitAssets();
            Link();
        }

        private static BuildingFundament F(int l, int r, int t)
        {
            return new BuildingFundament(l, r, t, 0);
        }

        private static void N(string id, string type, int limit, int pop, int blds, BuildingFundament f, ConstructionCost cost, bool smoke = false, bool light = true, bool burnable = false, int health = 800, bool tower = false, string projectile = "arrow", bool borders = false, string upgrade = "", string[] mats = null, int[] amounts = null, int books = 0, bool variants = false)
        {
            Defs[id] = new BuildDef
            {
                Id = id, Type = type, Order = "civ_order_" + id.Substring(4), Limit = limit, Pop = pop, Blds = blds, Fund = f, Cost = cost,
                Smoke = smoke, Light = light, Burnable = burnable, Health = health, Tower = tower, Projectile = projectile, Borders = borders,
                UpgradeTo = upgrade, Mats = mats ?? new string[0], MatAmounts = amounts ?? new int[0], Books = books, Variants = variants
            };
        }

        private static void Register(BuildDef d, string assetId)
        {
            if (AssetManager.buildings.get(assetId) != null) return;
            string from = d.Type == "type_house" ? "house_human_0" : d.Type == "type_hall" ? "hall_human_2" : "$city_colored_building$";
            BuildingAsset b = AssetManager.buildings.clone(assetId, from);
            b.type = d.Type;
            b.group = "civ";
            b.civ_kingdom = string.Empty;
            b.main_path = "buildings/civ/";
            b.sprite_path = "buildings/civ/" + assetId;
            b.sprites_are_initiated = false;
            b.building_sprites = null;
            b.fundament = d.Fund;
            b.cost = d.Cost;
            b.scale_base = new UnityEngine.Vector3(d.Scale, d.Scale, d.Scale);
            if (b.base_stats == null) b.base_stats = new BaseStats();
            b.base_stats["health"] = d.Health;
            b.burnable = d.Burnable;
            b.draw_light_area = d.Light;
            b.draw_light_size = d.Type == "type_house" ? 0.25f : 0.4f;
            b.smoke = d.Smoke;
            b.can_be_upgraded = !string.IsNullOrEmpty(d.UpgradeTo);
            b.upgrade_to = d.UpgradeTo ?? string.Empty;
            b.upgraded_from = string.Empty;
            b.has_sprite_construction = d.Type != "type_house" && d.Type != "type_hall";
            b.has_sprites_ruin = true;
            b.has_sprites_main = true;
            b.storage = d.Storage;
            b.book_slots = d.Books;
            b.build_place_borders = d.Borders;
            b.build_place_single = !d.Borders && d.Type != "type_house" && d.Type != "type_civ_wall";
            b.build_place_batch = d.Type == "type_civ_wall";
            b.build_prefer_replace_house = false;
            b.priority = d.Type == "type_hall" ? 100 : d.Type == "type_house" ? 1 : d.Type == "type_civ_power" ? 60 : 30;
            b.max_houses = d.Type == "type_house" ? 0 : b.max_houses;
            if (d.Housing > 0) b.setHousingSlots(d.Type == "type_house" && d.Tier >= 5 && CivMod.C != null && CivMod.C.PerfMode ? Math.Max(4, d.Housing * 2 / 3) : d.Housing);
            else if (d.Type != "type_hall")
            {
                b.housing_slots = 0;
                b.can_be_living_house = false;
            }
            b.housing_happiness = d.Type == "type_house" ? 8 + d.Housing / 2 : b.housing_happiness;
            b.upgrade_level = d.Type == "type_house" ? 5 + d.Tier : d.Type == "type_hall" ? d.Tier : 0;
            b.tower = d.Tower;
            if (d.Tower)
            {
                b.tower_projectile = d.Projectile;
                b.tower_projectile_amount = d.Type == "type_civ_antiair" ? 3 : 4;
                b.tower_projectile_reload = 2.5f;
                b.tower_attack_buildings = false;
                b.base_stats["damage"] = d.Type == "type_civ_antiair" ? 60f : 40f;
                b.base_stats["targets"] = 2f;
                b.base_stats["area_of_effect"] = 1f;
            }
            b.produce_biome_food = false;
            b.atlas_asset = AssetManager.dynamic_sprites_library.get(b.atlas_id) ?? AssetManager.dynamic_sprites_library.get("buildings");
            b.has_step_action = b.step_action != null;
            b.has_get_map_icon_color = b.get_map_icon_color != null;
            if (!string.IsNullOrEmpty(d.Tech)) TechOf[assetId] = d.Tech;
            if (d.Mats.Length > 0) Materials[assetId] = new KeyValuePair<string[], int[]>(d.Mats, d.MatAmounts);
            if (d.Pop > 0 && (d.Type == "type_house" || d.Type == "type_hall")) MinPop[assetId] = d.Pop;
            if (d.Type == "type_house") HouseTiers[assetId] = d.Tier;
        }

        private static int _linkedActors;
        private static int _linkedArchs;

        public static void Relink()
        {
            if (AssetManager.actor_library.list.Count == _linkedActors && AssetManager.architecture_library.list.Count == _linkedArchs) return;
            Link();
        }

        public static int HouseTier(BuildingAsset a)
        {
            int t;
            return a != null && HouseTiers.TryGetValue(a.id, out t) ? t : -1;
        }

        private static void Link()
        {
            _linkedActors = AssetManager.actor_library.list.Count;
            _linkedArchs = AssetManager.architecture_library.list.Count;
            foreach (ArchitectureAsset arch in AssetManager.architecture_library.list)
            {
                if (arch.isTemplateAsset() || arch.building_ids_for_construction == null) continue;
                var ids = arch.building_ids_for_construction;
                bool native = ids.ContainsKey("order_tent");
                foreach (var kv in OrderTech)
                {
                    string bid;
                    if (ids.TryGetValue(kv.Key, out bid) && !TechOf.ContainsKey(bid)) TechOf[bid] = kv.Value;
                }
                string h0;
                if (ids.TryGetValue("order_house_0", out h0) && native && !TechOf.ContainsKey(h0)) TechOf[h0] = "pit_houses";
                for (int i = 0; i <= 5; i++)
                {
                    string hid;
                    if (ids.TryGetValue("order_house_" + i, out hid) && !HouseTiers.ContainsKey(hid)) HouseTiers[hid] = native ? Math.Min(5, i + 1) : 1;
                }
                string tent;
                if (ids.TryGetValue("order_tent", out tent) && !HouseTiers.ContainsKey(tent)) HouseTiers[tent] = 0;
                string terminal = null;
                if (ids.ContainsKey("order_house_5")) terminal = ids["order_house_5"];
                else if (ids.ContainsKey("order_house_0")) terminal = ids["order_house_0"];
                if (terminal != null)
                {
                    BuildingAsset t = AssetManager.buildings.get(terminal);
                    if (t != null && (string.IsNullOrEmpty(t.upgrade_to) || !t.can_be_upgraded))
                    {
                        t.can_be_upgraded = true;
                        t.upgrade_to = native ? "civ_house_6" : "civ_house_2";
                    }
                }
                string hallTerm = ids.ContainsKey("order_hall_2") ? ids["order_hall_2"] : ids.ContainsKey("order_hall_0") ? ids["order_hall_0"] : null;
                if (hallTerm != null)
                {
                    BuildingAsset t = AssetManager.buildings.get(hallTerm);
                    if (t != null && (string.IsNullOrEmpty(t.upgrade_to) || !t.can_be_upgraded))
                    {
                        t.can_be_upgraded = true;
                        t.upgrade_to = "civ_hall_3";
                    }
                }
                foreach (BuildDef d in Defs.Values) arch.addBuildingOrderKey(d.Order, d.Id);
            }
            var done = new HashSet<string>();
            foreach (ActorAsset a in AssetManager.actor_library.list)
            {
                if (a == null || string.IsNullOrEmpty(a.build_order_template_id)) continue;
                if (!done.Add(a.build_order_template_id)) continue;
                CityBuildOrderAsset tpl = AssetManager.city_build_orders.get(a.build_order_template_id);
                if (tpl == null) continue;
                Patch(tpl, a.architecture_asset ?? AssetManager.architecture_library.get(a.architecture_id));
            }
        }

        private static void Patch(CityBuildOrderAsset tpl, ArchitectureAsset arch)
        {
            var have = new HashSet<string>(tpl.list.Select(o => o.id + (o.upgrade ? "/u" : "")));
            Action<string> up = id =>
            {
                if (have.Add(id + "/u")) tpl.addUpgrade(id);
            };
            bool native = arch != null && arch.building_ids_for_construction != null && arch.building_ids_for_construction.ContainsKey("order_tent");
            if (native) up("order_house_5");
            else up("order_house_0");
            if (arch != null && arch.building_ids_for_construction != null && arch.building_ids_for_construction.ContainsKey("order_hall_2")) up("order_hall_2");
            else up("order_hall_0");
            int topHouse = Db.Houses.Count > 0 ? Db.Houses.Max(x => x.Level) : 9;
            int topHall = Db.Halls.Count > 0 ? Db.Halls.Max(x => x.Level) : 6;
            for (int i = 2; i < topHouse; i++) up("civ_order_house_" + i);
            for (int i = 3; i < topHall; i++) up("civ_order_hall_" + i);
            foreach (BuildDef d in Defs.Values)
            {
                if (d.Type == "type_house" || d.Type == "type_hall" || d.Type == "type_civ_wall") continue;
                if (d.Type == "type_civ_power")
                {
                    if (have.Add(d.Order)) tpl.addBuilding(d.Order, 2, 15, 6).requirements_orders = new[] { "order_bonfire" };
                    continue;
                }
                if (d.Limit == 0) continue;
                if (!have.Add(d.Order)) continue;
                BuildOrder o = tpl.addBuilding(d.Order, d.Limit, d.Pop, d.Blds);
                o.requirements_orders = new[] { "order_bonfire" };
            }
            up("civ_order_fortress");
            up("civ_order_castle");
            up("civ_order_company_hq");
            tpl.prepareForAssetGeneration();
        }

        public static string RequiredTech(BuildingAsset b)
        {
            if (b == null) return null;
            string t;
            return TechOf.TryGetValue(b.id, out t) ? t : null;
        }

        public static bool Allowed(City c, BuildingAsset b)
        {
            if (b == null || c == null || c.kingdom == null || !c.kingdom.isCiv()) return true;
            Kingdom k = c.kingdom;
            string tech = RequiredTech(b);
            if (!string.IsNullOrEmpty(tech) && !Research.Has(k, tech)) return false;
            int pop;
            if (MinPop.TryGetValue(b.id, out pop) && c.getPopulationPeople() < pop) return false;
            switch (b.type)
            {
                case "type_civ_airport":
                    return c.zones.Count >= 10 && Research.HasFeature(k, "airline");
                case "type_civ_power":
                    return Energy.NeedsPlant(c) && Energy.BestPlant(c) == BaseId(b.id);
                case "type_civ_station":
                    return Rails.WantsStation(c);
                case "type_civ_metro":
                    return Research.HasFeature(k, "metro") && c.getPopulationPeople() >= 80;
                case "type_civ_company_hq":
                    return Companies.HqWanted(c);
                case "type_civ_launchpad":
                    return Space.WantsPad(c);
                case "type_civ_silo":
                    return War.WantsSilo(c);
                case "type_civ_fortress":
                    return Walls.WantsFortress(c, b);
                case "type_civ_airfield":
                    return Aircraft.WantsAirfield(c);
            }
            return true;
        }

        public static bool HasMaterials(City c, BuildingAsset b)
        {
            KeyValuePair<string[], int[]> m;
            if (b == null || !Materials.TryGetValue(b.id, out m)) return true;
            for (int i = 0; i < m.Key.Length; i++)
            {
                if (!Catchup.Has(c, m.Key[i], m.Value[i])) return false;
            }
            return true;
        }

        public static void TakeMaterials(City c, BuildingAsset b)
        {
            KeyValuePair<string[], int[]> m;
            if (b == null || !Materials.TryGetValue(b.id, out m)) return;
            for (int i = 0; i < m.Key.Length; i++) Catchup.Take(c, m.Key[i], m.Value[i]);
        }

        public static void Restyle(Building b, City c)
        {
            if (b == null || b.asset == null || c == null || c.kingdom == null) return;
            string baseId = BaseId(b.asset.id);
            if (!VariantBases.Contains(baseId)) return;
            string route = Research.VisualRoute(c.kingdom);
            string want = route == "real" ? baseId : baseId + "_" + route;
            if (b.asset.id == want) return;
            BuildingAsset target = AssetManager.buildings.get(want);
            if (target == null) return;
            Swap(b, target);
        }

        private static void Swap(Building b, BuildingAsset target)
        {
            try
            {
                b.makeZoneDirty();
                b.setTemplate(target);
                b.initAnimationData();
                b.updateStats();
                b.fillTiles();
            }
            catch (Exception e)
            {
                CivMod.Log("swap: " + e.Message);
            }
        }

        public static void Year(City c, int yrs)
        {
            Infrastructure.Year(c, yrs);
            int fy = Store.Int(c.data, "civ_fy", 0);
            fy = Economy.IsFamine(c) ? fy + yrs : Math.Max(0, fy - yrs);
            Store.Put(c.data, "civ_fy", fy);
            float chance = 0f;
            if (fy >= 2) chance += 0.08f * yrs;
            if (Energy.Blackout(c)) chance += 0.01f * yrs;
            int downs = 0;
            foreach (Building b in c.buildings.ToList())
            {
                if (b == null || !b.isUsable() || b.asset == null) continue;
                Restyle(b, c);
                if (b.asset.type == "type_docks")
                {
                    BuildingAsset dock = Docks.Wanted(b, c);
                    if (dock != null && dock != b.asset) Swap(b, dock);
                    continue;
                }
                if (b.asset.type != "type_house") continue;
                bool overTech = !Allowed(c, b.asset) && HouseTiers.ContainsKey(b.asset.id);
                float p = chance + (overTech ? 0.05f * yrs : 0f);
                if (p <= 0f || downs >= 3) continue;
                if (G.Chance(p) && Downgrade(b, c)) downs++;
            }
            if (downs > 0) Moods.Apply(c, "civ_lost_home", 0.1f);
        }

        public static void OnSacked(City c)
        {
            int n = 0;
            foreach (Building b in c.buildings.ToList())
            {
                if (b == null || !b.isUsable() || b.asset == null || b.asset.type != "type_house") continue;
                if (G.Chance(0.3f) && Downgrade(b, c)) n++;
                if (n >= 8) break;
            }
        }

        public static BuildingAsset PreviousOf(Building b, City c)
        {
            BuildingAsset a = b.asset;
            if (a == null) return null;
            string id = BaseId(a.id);
            if (id.StartsWith("civ_house_"))
            {
                int n;
                if (int.TryParse(id.Substring(10), out n))
                {
                    if (n == 2) return Arch(c, "order_house_0");
                    if (n == 6 && Arch(c, "order_house_5") != null) return Arch(c, "order_house_5");
                    if (n > 2) return AssetManager.buildings.get("civ_house_" + (n - 1));
                }
                return null;
            }
            if (!string.IsNullOrEmpty(a.upgraded_from)) return AssetManager.buildings.get(a.upgraded_from);
            if (id == "civ_hall_3") return Arch(c, "order_hall_2") ?? Arch(c, "order_hall_0");
            if (id.StartsWith("civ_hall_"))
            {
                int n;
                if (int.TryParse(id.Substring(9), out n) && n > 3) return AssetManager.buildings.get("civ_hall_" + (n - 1));
            }
            return null;
        }

        public static BuildingAsset Arch(City c, string order)
        {
            try
            {
                ActorAsset aa = c.getActorAsset();
                ArchitectureAsset arch = aa != null ? aa.architecture_asset : null;
                string id;
                if (arch == null || arch.building_ids_for_construction == null || !arch.building_ids_for_construction.TryGetValue(order, out id)) return null;
                return AssetManager.buildings.get(id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Downgrade(Building b, City c)
        {
            BuildingAsset prev = PreviousOf(b, c);
            if (prev == null || prev == b.asset) return false;
            try
            {
                b.makeZoneDirty();
                b.setTemplate(prev);
                b.initAnimationData();
                b.updateStats();
                b.setMaxHealth();
                b.fillTiles();
                return true;
            }
            catch (Exception e)
            {
                CivMod.Log("downgrade: " + e.Message);
                return false;
            }
        }

        [HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.checkZoneClear))]
        private static class BuildSpace
        {
            private static void Postfix(TileZone pZone, BuildingAsset pAsset, City pCity, ref bool __result)
            {
                if (__result || pZone == null || pAsset == null || !pAsset.city_building || pCity == null || pCity.kingdom == null || !pCity.kingdom.isCiv()) return;
                if (Cities.On)
                {
                    __result = true;
                    return;
                }
                HashSet<Building> buildings = pZone.getHashset(BuildingList.Civs);
                if (buildings == null) return;
                foreach (Building b in buildings)
                {
                    if (b != null && (b.asset == null || b.asset.type != "type_civ_wall")) return;
                }
                __result = true;
            }
        }

        [HarmonyPatch(typeof(BuildOrder), nameof(BuildOrder.getBuildingAsset))]
        private static class VariantUpgrade
        {
            private static void Postfix(BuildOrder __instance, City pCity, string pOrderID, ref BuildingAsset __result)
            {
                if (!__instance.upgrade || !string.IsNullOrEmpty(pOrderID) || __result == null || pCity == null || !VariantBases.Contains(__result.id)) return;
                if (pCity.countBuildingsOfID(__result.id) > 0) return;
                foreach (string route in VisualRoutes)
                {
                    string id = __result.id + "_" + route;
                    if (pCity.countBuildingsOfID(id) <= 0) continue;
                    BuildingAsset variant = AssetManager.buildings.get(id);
                    if (variant == null) continue;
                    __result = variant;
                    return;
                }
            }
        }

        [HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.canUseBuildAsset))]
        private static class Gate
        {
            private static void Postfix(BuildOrder pBuildAsset, City pCity, ref bool __result)
            {
                if (!__result || pCity == null || pCity.kingdom == null || !pCity.kingdom.isCiv()) return;
                try
                {
                    BuildingAsset b = pBuildAsset.getBuildingAsset(pCity);
                    if (b == null)
                    {
                        __result = false;
                        return;
                    }
                    if (pBuildAsset.upgrade) b = AssetManager.buildings.get(b.upgrade_to);
                    if (b == null || !Allowed(pCity, b) || !HasMaterials(pCity, b)) __result = false;
                }
                catch (Exception)
                {
                    __result = false;
                }
            }

            private static Exception Finalizer(Exception __exception, ref bool __result)
            {
                if (__exception is KeyNotFoundException || __exception is NullReferenceException)
                {
                    __result = false;
                    return null;
                }
                return __exception;
            }
        }

        [HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.hasResourcesForBuildAsset))]
        private static class GuardResources
        {
            private static Exception Finalizer(Exception __exception, ref bool __result)
            {
                if (__exception is KeyNotFoundException || __exception is NullReferenceException)
                {
                    __result = false;
                    return null;
                }
                return __exception;
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.canBeUpgraded))]
        private static class UpgradeGate
        {
            private static void Postfix(Building __instance, ref bool __result)
            {
                if (!__result || __instance.asset == null) return;
                City c = __instance.city;
                if (c == null) return;
                BuildingAsset next = AssetManager.buildings.get(__instance.asset.upgrade_to);
                if (next == null || !Allowed(c, next)) __result = false;
            }
        }

        [HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.upgradeBuilding))]
        private static class UpgradeCost
        {
            private static bool Prefix(Building pBuilding, City pCity, ref bool __result)
            {
                if (pBuilding == null || pBuilding.asset == null || pCity == null) return true;
                BuildingAsset next = AssetManager.buildings.get(pBuilding.asset.upgrade_to);
                if (next == null) return true;
                if (!HasMaterials(pCity, next) || !Allowed(pCity, next))
                {
                    __result = false;
                    return false;
                }
                return true;
            }

            private static void Postfix(Building pBuilding, City pCity, bool __result)
            {
                if (!__result || pBuilding == null || pCity == null) return;
                TakeMaterials(pCity, pBuilding.asset);
                if (pBuilding.asset.storage && pBuilding.data.resources == null) pBuilding.data.resources = new CityResources();
                if (pBuilding.asset.book_slots > 0 && pBuilding.data.books == null) pBuilding.data.books = new StorageBooks();
                Restyle(pBuilding, pCity);
                if (pBuilding.asset.type == "type_house" && HouseTiers.ContainsKey(pBuilding.asset.id))
                {
                    foreach (long id in pBuilding.residents)
                    {
                        Actor a = World.world.units.get(id);
                        if (G.Ok(a) && G.Chance(0.5f)) a.changeHappiness("civ_new_home");
                    }
                }
                Catchup.Chain(pBuilding, pCity);
            }
        }

        [HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.tryToBuild))]
        private static class BuildCost
        {
            private static void Postfix(City pCity, BuildingAsset pBuildingAsset, Building __result)
            {
                if (__result == null || pCity == null) return;
                TakeMaterials(pCity, pBuildingAsset);
                Restyle(__result, pCity);
            }
        }
    }
}
