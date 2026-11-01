using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Roles
    {
        public const string KRole = "civ_role";
        public const string KSpec = "civ_spec";
        public const string KCounts = "civ_rc";
        public const string KPower = "civ_rp";

        public const string Sage = "sage";
        public const string Scientist = "scientist";
        public const string Merchant = "merchant";
        public const string Worker = "worker";
        public const string Engineer = "engineer";
        public const string Driver = "driver";
        public const string Pilot = "pilot";
        public const string Artillery = "artillery";
        public const string Aviator = "aviator";
        public const string Sapper = "sapper";
        public const string Servant = "servant";
        public const string Ambassador = "ambassador";

        public static readonly string[] All = { Sage, Scientist, Merchant, Worker, Engineer, Driver, Pilot, Artillery, Aviator, Sapper, Servant, Ambassador };
        public static readonly string[] Military = { Artillery, Aviator, Sapper };
        public static readonly string[] Specialties = { "mil", "build", "med", "agri", "sci", "energy", "ind", "comp", "space", "metal", "trans", "comm" };

        public static void InitAssets()
        {
            if (AssetManager.trait_groups.get("civ_roles") == null)
            {
                AssetManager.trait_groups.add(new ActorTraitGroupAsset { id = "civ_roles", name = "civ_trait_group_roles", color = "#E8C15A" });
            }
            foreach (string r in All)
            {
                string id = "civ_role_" + r;
                if (AssetManager.traits.get(id) != null) continue;
                var t = new ActorTrait();
                t.id = id;
                t.path_icon = "ui/Icons/civ/role/" + r;
                t.special_locale_id = "civ_trait_civ_role_" + r;
                t.special_locale_description = "civ_trait_civ_role_" + r + "_d";
                t.special_locale_description_2 = "civ_trait_civ_role_" + r + "_d2";
                t.can_be_in_book = false;
                t.group_id = "civ_roles";
                t.rarity = Rarity.R1_Rare;
                t.type = TraitType.Other;
                t.spawn_random_trait_allowed = false;
                t.needs_to_be_explored = false;
                t.can_be_given = true;
                t.base_stats = new BaseStats();
                t.base_stats_meta = new BaseStats();
                switch (r)
                {
                    case Sage:
                        t.base_stats["intelligence"] = 2f;
                        break;
                    case Scientist:
                        t.base_stats["intelligence"] = 4f;
                        break;
                    case Merchant:
                        t.base_stats["diplomacy"] = 2f;
                        t.base_stats["speed"] = 3f;
                        break;
                    case Worker:
                        t.base_stats["stamina"] = 10f;
                        break;
                    case Engineer:
                        t.base_stats["intelligence"] = 2f;
                        t.base_stats["stewardship"] = 1f;
                        break;
                    case Servant:
                        t.base_stats["stewardship"] = 3f;
                        break;
                    case Ambassador:
                        t.base_stats["diplomacy"] = 5f;
                        break;
                    case Artillery:
                        t.base_stats["range"] = 2f;
                        t.base_stats["accuracy"] = 0.1f;
                        break;
                    case Aviator:
                        t.base_stats["speed"] = 5f;
                        break;
                    case Sapper:
                        t.base_stats["armor"] = 3f;
                        break;
                    case Driver:
                    case Pilot:
                        t.base_stats["speed"] = 3f;
                        break;
                }
                AssetManager.traits.add(t);
            }
        }

        public static string Of(Actor a)
        {
            return a == null || a.data == null ? "" : Store.Str(a.data, KRole);
        }

        public static string Spec(Actor a)
        {
            return a == null || a.data == null ? "" : Store.Str(a.data, KSpec);
        }

        public static void Set(Actor a, string role)
        {
            if (!G.Ok(a)) return;
            string old = Of(a);
            if (old == role) return;
            if (old.Length > 0)
            {
                ActorTrait ot = AssetManager.traits.get("civ_role_" + old);
                if (ot != null) a.removeTrait(ot);
            }
            Store.Put(a.data, KRole, role ?? "");
            if (!string.IsNullOrEmpty(role))
            {
                a.addTrait("civ_role_" + role);
                if (role == Scientist && Spec(a).Length == 0) Store.Put(a.data, KSpec, PickSpec(a));
            }
            if (a.citizen_job != null && a.citizen_job.id.StartsWith("civ_")) a.endJob();
        }

        private static string PickSpec(Actor a)
        {
            Kingdom k = a.kingdom;
            int era = Research.Era(k);
            var opts = new List<string>();
            foreach (string sp in Specialties)
            {
                if (sp == "comp" && era < 12) continue;
                if (sp == "space" && era < 12) continue;
                if (sp == "energy" && era < 9) continue;
                if (sp == "ind" && era < 9) continue;
                opts.Add(sp);
            }
            return G.Pick(opts) ?? "sci";
        }

        public static int Count(City c, string role)
        {
            if (c == null || c.data == null) return 0;
            return (int)Store.Get(Store.Map(c.data, KCounts), role);
        }

        public static float ResearchPower(City c)
        {
            if (c == null || c.data == null) return 0f;
            return Store.Flt(c.data, KPower, 0f);
        }

        public static List<Actor> Members(City c, string role)
        {
            var list = new List<Actor>();
            foreach (Actor a in c.units)
            {
                if (G.Ok(a) && Of(a) == role) list.Add(a);
            }
            return list;
        }

        public static Dictionary<string, float> SpecialtyShare(Kingdom k)
        {
            var share = new Dictionary<string, float>();
            int total = 0;
            foreach (City c in G.Cities(k))
            {
                var m = Store.Map(c.data, "civ_specs");
                foreach (var kv in m)
                {
                    Store.Add(share, kv.Key, kv.Value);
                    total += (int)kv.Value;
                }
            }
            if (total == 0) return share;
            foreach (string key in share.Keys.ToList()) share[key] = share[key] / total;
            return share;
        }

        public static int AmbassadorBonus(Kingdom from, Kingdom to)
        {
            if (from == null || from.data == null || to == null || to.data == null) return 0;
            string sent = Store.Str(from.data, "civ_amb_to");
            return sent.Contains("," + to.data.id + ",") ? 12 : 0;
        }

        private static int Desired(City c, string role, int era, int pop, int warriors)
        {
            Kingdom k = c.kingdom;
            int libs = c.countBuildingsType("type_library");
            int unis = c.countBuildingsType("type_civ_university");
            int labs = c.countBuildingsType("type_civ_lab");
            switch (role)
            {
                case Sage:
                {
                    if (!Research.Has(k, "oral_tradition")) return pop >= 8 ? 1 : 0;
                    int n = 1 + pop / 30 + era / 4;
                    if (Research.Has(k, "philosophy") && libs > 0) n = Math.Max(0, n - (libs * 3 + unis * 4 + labs * 5));
                    return Math.Min(n, 6);
                }
                case Scientist:
                {
                    if (!Research.Has(k, "philosophy") || libs == 0) return 0;
                    int n = 1 + pop / 25 + era / 3;
                    return Math.Min(n, libs * 3 + unis * 4 + labs * 5);
                }
                case Merchant:
                    if (!Research.Has(k, "barter")) return 0;
                    return Math.Min(6, 1 + pop / 40 + (c.hasBuildingType("type_market") ? 1 : 0));
                case Worker:
                    return Research.Has(k, "factory_system") ? c.countBuildingsType("type_civ_factory") * 4 + c.countBuildingsType("type_civ_smelter") + Energy.PlantCount(c) * 2 : 0;
                case Engineer:
                    return Research.Has(k, "mathematics") ? Math.Min(4, 1 + pop / 60) : 0;
                case Driver:
                    return Research.Has(k, "the_wheel") ? Vehicles.WantedCrew(c, false) + Transit.WantedDrivers(c) : 0;
                case Pilot:
                    return Research.Has(k, "hot_air_balloon") ? Vehicles.WantedCrew(c, true) + Transit.WantedPilots(c) : 0;
                case Artillery:
                    return Research.Has(k, "gunpowder") ? warriors / 6 : 0;
                case Aviator:
                    return Research.Has(k, "powered_flight") ? warriors / 8 : 0;
                case Sapper:
                    return Research.Has(k, "mathematics") && Research.Has(k, "siege_ladders") ? warriors / 10 : 0;
                case Servant:
                    return Research.Has(k, "bureaucracy") && c.hasBuildingType("type_hall") ? Math.Min(5, 1 + pop / 50) : 0;
                case Ambassador:
                    return Research.Has(k, "resident_embassies") && c.isCapitalCity() ? Math.Min(3, Research.Contacts(k).Count) : 0;
            }
            return 0;
        }

        public static void Year(City c, int yrs)
        {
            if (c.kingdom == null || !c.kingdom.isCiv()) return;
            int era = Research.Era(c);
            int pop = c.getPopulationPeople();
            var civil = new List<Actor>();
            var soldiers = new List<Actor>();
            var byRole = new Dictionary<string, List<Actor>>();
            foreach (string r in All) byRole[r] = new List<Actor>();
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !a.isAdult() || a.asset == null || !a.isSapient()) continue;
                string r = Of(a);
                if (r.Length > 0 && byRole.ContainsKey(r))
                {
                    bool military = Array.IndexOf(Military, r) >= 0;
                    if (military != a.isWarrior() || a.isKing() || a.isCityLeader() && r != Servant)
                    {
                        Set(a, "");
                        r = "";
                    }
                    else
                    {
                        byRole[r].Add(a);
                        continue;
                    }
                }
                if (a.isKing() || a.isCityLeader()) continue;
                if (Xeno.Status(a) == Xeno.Slave) continue;
                if (Vehicles.IsVehicle(a)) continue;
                if (a.isWarrior()) soldiers.Add(a);
                else civil.Add(a);
            }
            int warriors = soldiers.Count + byRole[Artillery].Count + byRole[Aviator].Count + byRole[Sapper].Count;
            civil.Sort((x, y) => y.stats["intelligence"].CompareTo(x.stats["intelligence"]));
            var counts = new Dictionary<string, float>();
            foreach (string r in All)
            {
                int want = Desired(c, r, era, pop, warriors);
                List<Actor> have = byRole[r];
                while (have.Count > want)
                {
                    Actor x = have[have.Count - 1];
                    have.RemoveAt(have.Count - 1);
                    Set(x, "");
                    if (x.isWarrior()) soldiers.Add(x);
                    else civil.Add(x);
                }
                List<Actor> pool = Array.IndexOf(Military, r) >= 0 ? soldiers : civil;
                if (r == Sage && byRole[Scientist].Count < Desired(c, Scientist, era, pop, warriors))
                {
                    while (have.Count > 0 && byRole[Scientist].Count < Desired(c, Scientist, era, pop, warriors))
                    {
                        Actor s = have[0];
                        have.RemoveAt(0);
                        Set(s, Scientist);
                        byRole[Scientist].Add(s);
                    }
                }
                while (have.Count < want && pool.Count > 0)
                {
                    Actor x = pool[0];
                    pool.RemoveAt(0);
                    Set(x, r);
                    have.Add(x);
                }
                counts[r] = have.Count;
            }
            Store.Map(c.data, KCounts, counts);
            float power = byRole[Sage].Count;
            float sciW = 2f + (c.hasBuildingType("type_civ_university") ? 1f : 0f) + (c.hasBuildingType("type_civ_lab") ? 1.5f : 0f);
            power += byRole[Scientist].Count * sciW;
            Store.Put(c.data, KPower, power);
            var specs = new Dictionary<string, float>();
            foreach (Actor s in byRole[Scientist]) Store.Add(specs, Spec(s), 1f);
            Store.Map(c.data, "civ_specs", specs);
            if (c.isCapitalCity()) PlanAmbassadors(c, byRole[Ambassador]);
        }

        private static void PlanAmbassadors(City c, List<Actor> ambassadors)
        {
            Kingdom k = c.kingdom;
            var targets = Research.Contacts(k).OrderBy(x => x.data.id).Take(ambassadors.Count).ToList();
            var ids = new List<string>();
            for (int i = 0; i < ambassadors.Count && i < targets.Count; i++)
            {
                Store.Put(ambassadors[i].data, "civ_amb", targets[i].data.id.ToString());
                ids.Add(targets[i].data.id.ToString());
            }
            Store.Put(k.data, "civ_amb_to", "," + string.Join(",", ids) + ",");
        }
    }
}
