using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace Civilizations
{
    internal static class Aircraft
    {
        private static readonly Dictionary<long, KeyValuePair<int, bool>> _enemyAir = new Dictionary<long, KeyValuePair<int, bool>>();

        public static bool HasBase(City c)
        {
            return c.countBuildingsType("type_civ_airfield") > 0 || c.countBuildingsType("type_civ_airport") > 0 || c.countBuildingsType("type_civ_fortress") > 0 && Research.Has(c.kingdom, "powered_flight");
        }

        public static bool KingdomBase(Kingdom k)
        {
            if (k == null) return false;
            foreach (City c in G.Cities(k)) if (HasBase(c)) return true;
            return false;
        }

        private static float _tick;
        private static readonly Dictionary<long, KeyValuePair<WorldTile, float>> _patrol = new Dictionary<long, KeyValuePair<WorldTile, float>>();

        private static City Nearest(Actor a, Kingdom k)
        {
            City best = null;
            float bd = 260f;
            foreach (Kingdom o in Groups.Enemies(k))
            {
                foreach (City c in G.Cities(o))
                {
                    WorldTile t = G.Tile(c);
                    if (t == null) continue;
                    float d = Toolbox.DistTile(a.current_tile, t);
                    if (d < bd)
                    {
                        bd = d;
                        best = c;
                    }
                }
            }
            return best;
        }

        private static WorldTile Base(City c)
        {
            foreach (Building b in c.buildings)
            {
                if (b == null || b.asset == null || !b.isUsable()) continue;
                string ty = b.asset.type;
                if (ty == "type_civ_airport" || ty == "type_civ_airfield") return b.current_tile;
            }
            if (c.kingdom != null)
            {
                foreach (City o in G.Cities(c.kingdom))
                {
                    if (o == c) continue;
                    foreach (Building b in o.buildings)
                    {
                        if (b == null || b.asset == null || !b.isUsable()) continue;
                        if (b.asset.type == "type_civ_airport" || b.asset.type == "type_civ_airfield") return b.current_tile;
                    }
                }
            }
            return G.Tile(c);
        }

        private static string Shape(Kingdom k)
        {
            int era = Research.Era(k);
            if (era < 12) return era < 11 ? "pair" : "vee";
            return "pair";
        }

        private static WorldTile PatrolPoint(City c, Actor leader)
        {
            long id = c.data.id;
            KeyValuePair<WorldTile, float> wp;
            if (_patrol.TryGetValue(id, out wp) && Time.time < wp.Value && wp.Key != null && Toolbox.DistTile(leader.current_tile, wp.Key) > 4f) return wp.Key;
            WorldTile home = G.Tile(c);
            var pool = new List<TileZone>();
            foreach (City o in G.Cities(c.kingdom))
            {
                foreach (TileZone z in o.zones) if (home != null && z.centerTile != null && Toolbox.DistTile(home, z.centerTile) < 60f) pool.Add(z);
            }
            foreach (TileZone z in Waters.Of(c.kingdom)) if (home != null && z.centerTile != null && Toolbox.DistTile(home, z.centerTile) < 60f) pool.Add(z);
            if (pool.Count == 0) return home;
            WorldTile t = pool[UnityEngine.Random.Range(0, pool.Count)].centerTile;
            _patrol[id] = new KeyValuePair<WorldTile, float>(t, Time.time + 20f);
            return t;
        }

        private static readonly Dictionary<Kingdom, List<Actor>> _air = new Dictionary<Kingdom, List<Actor>>();

        private static Actor Intruder(Kingdom k, Actor near)
        {
            Actor best = null;
            float bd = 90f;
            foreach (Kingdom o in Groups.Enemies(k))
            {
                List<Actor> l;
                if (!_air.TryGetValue(o, out l)) continue;
                foreach (Actor u in l)
                {
                    if (!G.Ok(u) || u.current_tile == null) continue;
                    if (Waters.Owner(u.current_tile) != k) continue;
                    float dd = Toolbox.DistTile(near.current_tile, u.current_tile);
                    if (dd < bd)
                    {
                        bd = dd;
                        best = u;
                    }
                }
            }
            return best;
        }

        public static Actor SupportCaptain(Kingdom k)
        {
            Actor best = null;
            int size = 0;
            if (World.world.armies == null) return null;
            foreach (Army a in World.world.armies)
            {
                if (a == null || !a.isAlive() || !a.hasCaptain()) continue;
                Actor cap = a.getCaptain();
                if (cap == null || cap.kingdom != k || cap.current_tile == null) continue;
                if (a.units.Count > size)
                {
                    size = a.units.Count;
                    best = cap;
                }
            }
            return best;
        }

        private static void Strike(Actor a, City target)
        {
            BaseSimObject victim = null;
            UnitDef d = Vehicles.Def(a);
            if (d != null && d.Role == "fighter")
            {
                foreach (Actor u in target.units)
                {
                    if (!G.Ok(u) || !u.isWarrior()) continue;
                    victim = u;
                    if (G.Chance(0.3f)) break;
                }
            }
            if (victim == null && target.buildings.Count > 0)
            {
                Building b = target.buildings.GetRandom();
                if (b != null && b.isAlive()) victim = b;
            }
            if (victim != null && a.canAttackTarget(victim)) a.setAttackTarget(victim);
            else Groups.Send(a, G.Tile(target), false);
        }

        public static void Tick(float dt)
        {
            _tick += dt;
            if (_tick < 2f) return;
            _tick = 0f;
            var byCity = new Dictionary<City, List<Actor>>();
            _air.Clear();
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "air" && d.Cls != "space") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null || asset.units.Count == 0) continue;
                bool ops = d.Role != "scout" && d.Role != "transport" && !Spacecraft.Civil(d);
                foreach (Actor a in asset.units)
                {
                    if (G.Ok(a) && a.kingdom != null && a.current_tile != null)
                    {
                        List<Actor> al;
                        if (!_air.TryGetValue(a.kingdom, out al)) _air[a.kingdom] = al = new List<Actor>();
                        al.Add(a);
                    }
                    if (!ops) continue;
                    if (!G.Ok(a) || a.city == null || a.kingdom == null || !a.kingdom.isCiv() || a.current_tile == null || Carriers.Embarked(a)) continue;
                    List<Actor> l;
                    if (!byCity.TryGetValue(a.city, out l)) byCity[a.city] = l = new List<Actor>();
                    l.Add(a);
                }
            }
            foreach (var kv in byCity)
            {
                City c = kv.Key;
                Kingdom k = c.kingdom;
                List<Actor> all = kv.Value;
                all.Sort((x, y) => x.data.id.CompareTo(y.data.id));
                bool war = World.world.wars.hasWars(k);
                string shape = Shape(k);
                WorldTile home = Base(c);
                var patrol = new List<Actor>();
                var rest = new List<Actor>();
                if (war)
                {
                    Actor raider = all.FirstOrDefault(x => { UnitDef d = Vehicles.Def(x); return d != null && d.Role == "bomber"; }) ?? all[all.Count - 1];
                    City target = Nearest(raider, k);
                    if (target != null && !raider.has_attack_target) Strike(raider, target);
                    var fighters = all.Where(x => x != raider && Vehicles.Def(x).Role == "fighter" && !Vehicles.Def(x).Art.Contains("heli") && Vehicles.Def(x).Base != "civ_drone").ToList();
                    var support = all.Where(x => x != raider && !fighters.Contains(x)).ToList();
                    Actor foe = fighters.Count > 0 ? Intruder(k, fighters[0]) : null;
                    if (foe != null)
                    {
                        foreach (Actor f in fighters) if (f.canAttackTarget(foe)) f.setAttackTarget(foe);
                    }
                    else
                    {
                        for (int i = 0; i < fighters.Count; i++)
                        {
                            if (i < 4) patrol.Add(fighters[i]);
                            else rest.Add(fighters[i]);
                        }
                    }
                    Actor cap = SupportCaptain(k);
                    if (cap != null && support.Count > 0)
                    {
                        Actor lead = support[0];
                        if (cap.has_attack_target && cap.attack_target != null && cap.attack_target.isAlive())
                        {
                            foreach (Actor s in support.Take(4)) if (s.canAttackTarget(cap.attack_target)) s.setAttackTarget(cap.attack_target);
                        }
                        else
                        {
                            Groups.Send(lead, cap.current_tile, false);
                            for (int i = 1; i < Math.Min(4, support.Count); i++) Groups.Follow(support[i], lead, Groups.Offset(shape, i - 1, 2f), false);
                        }
                        rest.AddRange(support.Skip(4));
                    }
                    else rest.AddRange(support);
                }
                else
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (i < 4) patrol.Add(all[i]);
                        else rest.Add(all[i]);
                    }
                }
                if (patrol.Count > 0)
                {
                    Actor lead = patrol[0];
                    Groups.Send(lead, PatrolPoint(c, lead), false);
                    for (int i = 1; i < patrol.Count; i++) Groups.Follow(patrol[i], lead, Groups.Offset(shape, i - 1, 2f), false);
                }
                foreach (Actor r in rest)
                {
                    if (home != null && Toolbox.DistTile(r.current_tile, home) > 8f) Groups.Send(r, home, false);
                }
            }
        }

        public static bool WantsAirfield(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !Research.Has(c.kingdom, "powered_flight")) return false;
            if (c.countBuildingsType("type_civ_airfield") > 0) return false;
            return c.countWarriors() >= 4 || c.getPopulationPeople() >= 40;
        }

        public static int CountAir(Kingdom k)
        {
            int n = 0;
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "air" && d.Cls != "space") continue;
                ActorAsset a = AssetManager.actor_library.get(d.Id);
                if (a == null || a.units == null) continue;
                foreach (Actor v in a.units) if (v != null && v.kingdom == k) n++;
            }
            return n;
        }

        public static bool EnemyAir(Kingdom k)
        {
            KeyValuePair<int, bool> v;
            if (_enemyAir.TryGetValue(k.data.id, out v) && v.Key == G.Year) return v.Value;
            bool any = false;
            foreach (global::War w in World.world.wars.getWars(k))
            {
                if (w == null || w.isRekt()) continue;
                foreach (Kingdom o in w.getOppositeSideKingdom(k))
                {
                    if (o != null && CountAir(o) > 0)
                    {
                        any = true;
                        break;
                    }
                }
                if (any) break;
            }
            _enemyAir[k.data.id] = new KeyValuePair<int, bool>(G.Year, any);
            return any;
        }

        public static void CityYear(City c, int yrs, List<Actor> have)
        {
            Kingdom k = c.kingdom;
            int warriors = c.countWarriors();
            if (Research.Has(k, "hot_air_balloon") && c.getPopulationPeople() >= 60 && Vehicles.Count(have, d => VehicleRoster.Duty(d) == "air_scout") == 0)
            {
                Vehicles.Produce(c, have, "air_scout", 1, Roles.Pilot, true);
            }
            if (!HasBase(c) && !KingdomBase(k)) return;
            bool war = World.world.wars.hasWars(k);
            Vehicles.Produce(c, have, "air_transport", war ? 1 : 0, Roles.Aviator, true);
            Vehicles.Fill(c, have, new[] { "air_fighter", "air_bomber", "air_attack_heli", "air_drone" }, new[] { 4f, 2f, 2f, 2f }, Vehicles.Cap(20, war), Vehicles.PerYear(war, yrs), x => Roles.Aviator, war);
        }

        [HarmonyPatch(typeof(BaseSimObject), nameof(BaseSimObject.canAttackTarget))]
        private static class AntiAir
        {
            private static void Postfix(BaseSimObject __instance, BaseSimObject pTarget, ref bool __result)
            {
                if (!__result || pTarget == null || !pTarget.isActor() || !__instance.isActor()) return;
                UnitDef td = Vehicles.Def(pTarget.a);
                if (td == null || td.Cls != "air" && td.Cls != "space") return;
                UnitDef ad = Vehicles.Def(__instance.a);
                if (ad != null && (ad.Cls == "air" || ad.Cls == "space" || ad.Cls == "aa")) return;
                if (__instance.a.hasRangeAttack()) return;
                __result = false;
            }
        }
    }
}
