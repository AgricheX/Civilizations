using System;
using System.Collections.Generic;
using HarmonyLib;
using ai.behaviours;

namespace Civilizations
{
    internal static class Behaviours
    {
        public static readonly Dictionary<string, CitizenJobAsset> JobByRole = new Dictionary<string, CitizenJobAsset>();

        public static void InitAssets()
        {
            BehaviourTaskActorLibrary tasks = AssetManager.tasks_actor;
            Task(tasks, "civ_study", "ui/Icons/civ/role/scientist", t =>
            {
                t.addBeh(new BehCityActorFindBuilding("type_civ_lab,type_civ_university,type_library,type_hall"));
                t.addBeh(new BehGoToBuildingTarget());
                t.addBeh(new BehStayInBuildingTarget(15f, 30f));
                t.addBeh(new BehExitBuilding());
                t.addBeh(new BehRandomWait(1f, 3f));
            });
            Task(tasks, "civ_factory_work", "ui/Icons/civ/role/worker", t =>
            {
                t.addBeh(new BehCityActorFindBuilding("type_civ_factory,type_civ_workshop,type_civ_smelter,type_civ_kiln"));
                t.addBeh(new BehGoToBuildingTarget());
                t.addBeh(new BehStayInBuildingTarget(20f, 35f));
                t.addBeh(new BehExitBuilding());
                t.addBeh(new BehRandomWait(1f, 2f));
            });
            Task(tasks, "civ_office", "ui/Icons/civ/role/servant", t =>
            {
                t.addBeh(new BehCityActorFindBuilding("type_hall,type_civ_bank,type_civ_mint"));
                t.addBeh(new BehGoToBuildingTarget());
                t.addBeh(new BehStayInBuildingTarget(15f, 25f));
                t.addBeh(new BehExitBuilding());
                t.addBeh(new BehRandomWait(1f, 2f));
            });
            Task(tasks, "civ_engineering", "ui/Icons/civ/role/engineer", t =>
            {
                t.addBeh(new BehCivWorksite());
                t.addBeh(new BehGoToTileTarget());
                t.addBeh(new BehRandomWait(3f, 6f));
                t.addBeh(new BehCivBuildRoad());
            });
            Task(tasks, "civ_trade_trip", "ui/Icons/civ/role/merchant", t =>
            {
                t.addBeh(new BehCivPlanTrade());
                t.addBeh(new BehGoToTileTarget());
                t.addBeh(new BehCivDeliver());
                t.addBeh(new BehCivGoHome());
                t.addBeh(new BehGoToTileTarget());
                t.addBeh(new BehRandomWait(1f, 3f));
            });
            Task(tasks, "civ_embassy", "ui/Icons/civ/role/ambassador", t =>
            {
                t.addBeh(new BehCivPlanEmbassy());
                t.addBeh(new BehGoToTileTarget());
                t.addBeh(new BehRandomWait(8f, 15f));
                t.addBeh(new BehCivEmbassyDone());
                t.addBeh(new BehCivGoHome());
                t.addBeh(new BehGoToTileTarget());
            });
            Task(tasks, "civ_patrol", "ui/Icons/iconSoldier", t =>
            {
                t.addBeh(new BehCityActorGetRandomZoneTile());
                t.addBeh(new BehGoToTileTarget());
                t.addBeh(new BehRandomWait(2f, 5f));
            });

            ActorJobLibrary jobs = AssetManager.job_actor;
            Job(jobs, "civ_scholar", "civ_study");
            Job(jobs, "civ_worker", "civ_factory_work");
            Job(jobs, "civ_servant", "civ_office");
            Job(jobs, "civ_engineer", "civ_engineering");
            Job(jobs, "civ_merchant", "civ_trade_trip");
            Job(jobs, "civ_ambassador", "civ_embassy");
            Job(jobs, "civ_cargo", "civ_trade_trip");
            Job(jobs, "civ_crew", "civ_patrol");

            Citizen(Roles.Sage, "civ_scholar");
            Citizen(Roles.Scientist, "civ_scholar");
            Citizen(Roles.Worker, "civ_worker");
            Citizen(Roles.Servant, "civ_servant");
            Citizen(Roles.Engineer, "civ_engineer");
            Citizen(Roles.Merchant, "civ_merchant");
            Citizen(Roles.Ambassador, "civ_ambassador");
            Citizen(Roles.Driver, "civ_crew");
            Citizen(Roles.Pilot, "civ_crew");
            CitizenJobLibrary lib = AssetManager.citizen_job_library;
            if (lib.list_priority_high != null)
            {
                foreach (CitizenJobAsset j in JobByRole.Values)
                {
                    if (!lib.list_priority_high.Contains(j)) lib.list_priority_high.Add(j);
                }
            }
        }

        private static void Task(BehaviourTaskActorLibrary lib, string id, string icon, Action<BehaviourTaskActor> fill)
        {
            if (lib.get(id) != null) return;
            var t = new BehaviourTaskActor { id = id, locale_key = "task_unit_" + id, cancellable_by_reproduction = true, cancellable_by_socialize = true };
            lib.add(t);
            t.setIcon(icon);
            fill(t);
        }

        private static void Job(ActorJobLibrary lib, string id, string task)
        {
            if (lib.get(id) != null) return;
            var j = new ActorJob { id = id };
            lib.add(j);
            j.addTask(task);
            j.addTask("end_job");
        }

        private static void Citizen(string role, string job)
        {
            string id = "civ_job_" + role;
            CitizenJobLibrary lib = AssetManager.citizen_job_library;
            CitizenJobAsset a = lib.get(id);
            if (a == null)
            {
                a = new CitizenJobAsset
                {
                    id = id,
                    priority = 5,
                    common_job = false,
                    ok_for_king = false,
                    ok_for_leader = false,
                    path_icon = "ui/Icons/civ/role/" + role,
                    unit_job_default = job
                };
                string r = role;
                a.should_be_assigned = (Actor x) => Roles.Of(x) == r;
                lib.add(a);
            }
            JobByRole[role] = a;
        }

        public static City Home(Actor a)
        {
            return a == null ? null : a.city;
        }

        [HarmonyPatch(typeof(CityBehCheckCitizenTasks), nameof(CityBehCheckCitizenTasks.execute))]
        private static class Slots
        {
            private static void Postfix(City pCity)
            {
                if (pCity == null || pCity.kingdom == null || !pCity.kingdom.isCiv()) return;
                foreach (var kv in JobByRole)
                {
                    int n = Roles.Count(pCity, kv.Key);
                    if (n > 0) pCity.jobs.addToJob(kv.Value, n);
                }
            }
        }
    }

    public class BehCivPlanTrade : BehaviourActionActor
    {
        public override BehResult execute(Actor a)
        {
            if (a.city == null) return BehResult.Stop;
            int cap = 12;
            UnitDef d = Vehicles.Def(a);
            int cargo = d != null ? (int)Store.Get(d.Stats, "cargo") : 0;
            if (cargo > 0) cap = cargo;
            else if (Research.Has(a.kingdom, "the_wheel")) cap = 16;
            City dest;
            string res;
            int amount;
            if (!Economy.PlanTrip(a, cap, out dest, out res, out amount)) return BehResult.Stop;
            Store.Put(a.data, "civ_dest", dest.data.id.ToString());
            Store.Put(a.data, "civ_cargo_res", res);
            Store.Put(a.data, "civ_cargo_amt", amount);
            WorldTile t = G.Tile(dest);
            if (t == null) return BehResult.Stop;
            a.beh_tile_target = t;
            return BehResult.Continue;
        }
    }

    public class BehCivDeliver : BehaviourActionActor
    {
        public override BehResult execute(Actor a)
        {
            string res = Store.Str(a.data, "civ_cargo_res");
            int amount = Store.Int(a.data, "civ_cargo_amt", 0);
            long id;
            City dest = null;
            if (long.TryParse(Store.Str(a.data, "civ_dest"), out id)) dest = World.world.cities.get(id);
            Store.Put(a.data, "civ_cargo_amt", 0);
            if (amount <= 0 || string.IsNullOrEmpty(res)) return BehResult.Continue;
            WorldTile t = G.Tile(dest);
            if (dest != null && dest.isAlive() && t != null && a.current_tile != null && Toolbox.SquaredDistTile(t, a.current_tile) < 400)
            {
                Economy.Deliver(a, dest, res, amount);
            }
            else if (a.city != null)
            {
                Economy.Give(a.city, res, amount);
            }
            return BehResult.Continue;
        }
    }

    public class BehCivGoHome : BehaviourActionActor
    {
        public override BehResult execute(Actor a)
        {
            WorldTile t = G.Tile(a.city);
            if (t == null) return BehResult.Stop;
            a.beh_tile_target = t;
            return BehResult.Continue;
        }
    }

    public class BehCivPlanEmbassy : BehaviourActionActor
    {
        public override BehResult execute(Actor a)
        {
            long id;
            if (!long.TryParse(Store.Str(a.data, "civ_amb"), out id)) return BehResult.Stop;
            Kingdom k = World.world.kingdoms.get(id);
            if (k == null || !k.isAlive() || k.capital == null) return BehResult.Stop;
            WorldTile t = G.Tile(k.capital);
            if (t == null || a.current_tile == null || !t.isSameIsland(a.current_tile)) return BehResult.Stop;
            a.beh_tile_target = t;
            return BehResult.Continue;
        }
    }

    public class BehCivEmbassyDone : BehaviourActionActor
    {
        public override BehResult execute(Actor a)
        {
            long id;
            if (long.TryParse(Store.Str(a.data, "civ_amb"), out id))
            {
                Kingdom k = World.world.kingdoms.get(id);
                if (k != null && a.kingdom != null) Internet.OnEmbassy(a, k);
            }
            return BehResult.Continue;
        }
    }

    public class BehCivWorksite : BehaviourActionActor
    {
        public override BehResult execute(Actor a)
        {
            City c = a.city;
            if (c == null || !c.hasZones()) return BehResult.Stop;
            TileZone z = null;
            foreach (TileZone b in c.border_zones)
            {
                z = b;
                if (G.Chance(0.3f)) break;
            }
            if (z == null) z = c.zones[UnityEngine.Random.Range(0, c.zones.Count)];
            WorldTile t = z.getRandomTile();
            if (t == null || a.current_tile == null || !t.isSameIsland(a.current_tile)) return BehResult.Stop;
            a.beh_tile_target = t;
            return BehResult.Continue;
        }
    }

    public class BehCivBuildRoad : BehaviourActionActor
    {
        public override BehResult execute(Actor a)
        {
            if (a.city != null) Store.Put(a.city.data, "civ_eng_work", Store.Int(a.city.data, "civ_eng_work", 0) + 1);
            return BehResult.Continue;
        }
    }
}
