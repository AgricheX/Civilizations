using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Soldiers
    {
        private static readonly string[] Duties = { "sol_melee", "sol_ranged", "sol_heavy" };
        private static readonly float[] Weights = { 3.5f, 4f, 2.5f };
        private static float _tick;
        private static List<ActorAsset> _assets;

        public static bool On
        {
            get { return Vehicles.On && (CivMod.C == null || CivMod.C.SoldiersOn); }
        }

        private static List<ActorAsset> Assets()
        {
            if (_assets != null) return _assets;
            _assets = new List<ActorAsset>();
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "soldier") continue;
                ActorAsset a = AssetManager.actor_library.get(d.Id);
                if (a != null) _assets.Add(a);
            }
            return _assets;
        }

        public static List<Actor> In(City c)
        {
            var list = new List<Actor>();
            foreach (ActorAsset a in Assets())
            {
                if (a.units == null) continue;
                foreach (Actor s in a.units) if (G.Ok(s) && s.city == c) list.Add(s);
            }
            return list;
        }

        public static int PerCity
        {
            get
            {
                int n = CivMod.C != null ? CivMod.C.SoldiersPerCity : 20;
                if (CivMod.C != null && CivMod.C.PerfMode) n = Math.Max(1, n / 3);
                return n;
            }
        }

        public static int Cap(Kingdom k)
        {
            int n = Math.Max(20, CivMod.C != null ? CivMod.C.SoldiersPerCity : 20);
            City cap = k != null ? k.capital : null;
            if (cap != null && cap.isAlive()) n = Math.Max(n, cap.countWarriors());
            if (CivMod.C != null && CivMod.C.PerfMode) n = Math.Max(1, n / 3);
            return n;
        }

        private static UnitDef Pick(Kingdom k, string duty, List<Actor> have)
        {
            List<UnitDef> options = Vehicles.Options(k, d => d.Cls == "soldier" && VehicleRoster.Duty(d) == duty);
            if (options.Count == 0) return null;
            int newest = options.Max(d => d.Era);
            var weights = new List<float>();
            foreach (UnitDef d in options)
            {
                int n = 0;
                foreach (Actor a in have) if (a.asset != null && a.asset.id == d.Id) n++;
                weights.Add(VehicleRoster.Weight(d, newest) / (1f + n * 0.5f));
            }
            return options[G.Weighted(weights)];
        }

        public static void CityYear(City c, int yrs, bool war)
        {
            if (!On || !war || c.kingdom == null || !c.kingdom.isCiv()) return;
            WorldTile tile = c.getTile();
            if (tile == null) return;
            List<Actor> have = In(c);
            int cap = Cap(c.kingdom);
            int per = Math.Min(12, 5 * Math.Max(1, yrs));
            var blocked = new HashSet<string>();
            int made = 0;
            while (have.Count < cap && made < per)
            {
                string best = null;
                float score = float.MaxValue;
                for (int i = 0; i < Duties.Length; i++)
                {
                    if (blocked.Contains(Duties[i])) continue;
                    string duty = Duties[i];
                    int n = 0;
                    foreach (Actor a in have)
                    {
                        UnitDef d = Vehicles.Def(a);
                        if (d != null && VehicleRoster.Duty(d) == duty) n++;
                    }
                    float s = (n + 1f) / Weights[i];
                    if (s < score)
                    {
                        score = s;
                        best = duty;
                    }
                }
                if (best == null) return;
                UnitDef pick = Pick(c.kingdom, best, have);
                if (pick == null)
                {
                    blocked.Add(best);
                    continue;
                }
                Actor v = World.world.units.createNewUnit(pick.Id, tile, false, 0f, null, null, false, true);
                if (v == null) return;
                v.setKingdom(c.kingdom);
                v.setCity(c);
                v.setProfession(UnitProfession.Warrior);
                Store.Put(v.data, Vehicles.KRider, "");
                Store.Put(v.data, Vehicles.KConscript, true);
                VehicleNames.Apply(v);
                have.Add(v);
                made++;
            }
        }

        public static bool OnDeath(BaseSimObject pTarget, WorldTile pTile)
        {
            return true;
        }

        private static bool AtWar(Kingdom k)
        {
            return k != null && k.isAlive() && k.isCiv() && World.world.wars.hasWars(k);
        }

        public static void Tick(float dt)
        {
            _tick += dt;
            if (_tick < 2f) return;
            _tick = 0f;
            int budget = 40;
            foreach (UnitDef d in Db.Units)
            {
                if (budget <= 0) return;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null || asset.units.Count == 0) continue;
                bool soldier = d.Cls == "soldier";
                foreach (Actor a in asset.units.ToList())
                {
                    if (budget <= 0) return;
                    if (!G.Ok(a) || a.isFavorite() || a.is_inside_boat || a.isInsideSomething()) continue;
                    if (a.asset.is_boat)
                    {
                        Boat boat = a.getSimpleComponent<Boat>();
                        if (boat != null && boat.hasPassengers()) continue;
                    }
                    if (!soldier && !Store.Bool(a.data, Vehicles.KConscript)) continue;
                    if (soldier && (!On || a.kingdom == null || !a.kingdom.isCiv()))
                    {
                        ActionLibrary.removeUnit(a);
                        budget--;
                        continue;
                    }
                    if (AtWar(a.kingdom)) continue;
                    ActionLibrary.removeUnit(a);
                    budget--;
                }
            }
        }

        [HarmonyLib.HarmonyPatch(typeof(Actor), nameof(Actor.u1_checkInside))]
        private static class SafeInside
        {
            private static bool Prefix(Actor __instance)
            {
                if (__instance.is_inside_boat && (__instance.inside_boat == null || __instance.inside_boat.actor == null || !__instance.inside_boat.actor.isAlive()))
                {
                    __instance.exitBoat();
                    return false;
                }
                return true;
            }
        }

        public static void Clear()
        {
            _assets = null;
        }
    }
}
