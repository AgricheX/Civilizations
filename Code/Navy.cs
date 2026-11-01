using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class Navy
    {
        private static float _tick;
        private static readonly Dictionary<long, WorldTile> _waypoint = new Dictionary<long, WorldTile>();
        private static readonly Dictionary<long, float> _until = new Dictionary<long, float>();
        private static readonly Dictionary<long, int> _missiles = new Dictionary<long, int>();
        private const float Standoff = 30f;
        private const float MissileRange = 48f;
        private static readonly Dictionary<Kingdom, List<Actor>> _byKingdom = new Dictionary<Kingdom, List<Actor>>();
        private static readonly Dictionary<City, List<Actor>> _byCity = new Dictionary<City, List<Actor>>();
        private static readonly List<Actor> Empty = new List<Actor>();

        private static List<Actor> ShipsOf(Kingdom k)
        {
            List<Actor> l;
            return k != null && _byKingdom.TryGetValue(k, out l) ? l : Empty;
        }

        private static void Collect()
        {
            _byKingdom.Clear();
            _byCity.Clear();
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "ship") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null || asset.units.Count == 0) continue;
                bool trade = d.Role == "trade";
                foreach (Actor s in asset.units)
                {
                    if (!G.Ok(s) || s.current_tile == null || s.kingdom == null) continue;
                    List<Actor> l;
                    if (!_byKingdom.TryGetValue(s.kingdom, out l)) _byKingdom[s.kingdom] = l = new List<Actor>();
                    l.Add(s);
                    if (trade || s.city == null || Groups.Busy(s)) continue;
                    if (!_byCity.TryGetValue(s.city, out l)) _byCity[s.city] = l = new List<Actor>();
                    l.Add(s);
                }
            }
        }

        public static int MissileCap(Actor s)
        {
            if (s == null || s.asset == null) return 0;
            switch (s.asset.id)
            {
                case "civ_missile_destroyer": return 16;
                case "civ_nuclear_sub": return 8;
                case "civ_giant_sub": return 6;
            }
            return 0;
        }

        public static int Missiles(Actor s)
        {
            int cap = MissileCap(s);
            if (cap == 0) return 0;
            int n;
            if (!_missiles.TryGetValue(s.data.id, out n)) _missiles[s.data.id] = n = cap;
            return n;
        }

        private static BaseSimObject LongTarget(Kingdom k, Actor from)
        {
            BaseSimObject best = null;
            float bd = MissileRange + 10f;
            foreach (Kingdom o in Groups.Enemies(k))
            {
                foreach (Actor s in ShipsOf(o))
                {
                    if (!G.Ok(s) || s.current_tile == null) continue;
                    float dd = Toolbox.DistTile(from.current_tile, s.current_tile);
                    if (dd < bd)
                    {
                        bd = dd;
                        best = s;
                    }
                }
                if (best != null) continue;
                foreach (City c in G.Cities(o))
                {
                    WorldTile ct = G.Tile(c);
                    if (ct == null || Toolbox.DistTile(from.current_tile, ct) > bd + 12f) continue;
                    foreach (Building b in c.buildings)
                    {
                        if (b == null || !b.isAlive() || b.current_tile == null) continue;
                        float dd = Toolbox.DistTile(from.current_tile, b.current_tile);
                        if (dd < bd)
                        {
                            bd = dd;
                            best = b;
                        }
                    }
                }
            }
            return best;
        }

        private static void Fire(Actor s, BaseSimObject target)
        {
            int n = Missiles(s);
            if (n <= 0 || target == null || !target.isAlive()) return;
            if (AssetManager.projectiles.get("civ_naval_missile") == null) return;
            Vector3 from = s.current_position;
            Vector3 to = target.current_position;
            World.world.projectiles.spawn(s, target, "civ_naval_missile", from, to, target.getHeight());
            _missiles[s.data.id] = n - 1;
        }

        private static bool Barrage(City c, Kingdom k, List<Actor> fleet)
        {
            var shooters = fleet.Where(x => Missiles(x) > 0).ToList();
            if (shooters.Count == 0) return false;
            Actor lead = shooters[0];
            BaseSimObject target = LongTarget(k, lead);
            if (target == null || target.current_tile == null) return false;
            WorldTile tt = target.current_tile;
            Vector2 away = Groups.Dir(tt, lead.current_tile);
            Vector2 p = new Vector2(tt.x, tt.y) + away * Standoff;
            WorldTile stand = Globe.Tile(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (stand != null && stand.Type != null && stand.Type.liquid) Groups.Send(lead, stand, true);
            Vector2 side = new Vector2(-away.y, away.x);
            int slot = 0;
            foreach (Actor s in fleet)
            {
                if (s != lead)
                {
                    slot++;
                    Vector2 q = p + side * (((slot + 1) / 2) * 3f * (slot % 2 == 0 ? 1f : -1f));
                    WorldTile qt = Globe.Tile(Mathf.RoundToInt(q.x), Mathf.RoundToInt(q.y));
                    if (qt != null && qt.Type != null && qt.Type.liquid) Groups.Send(s, qt, true);
                }
                if (Missiles(s) > 0 && s.current_tile != null && Toolbox.DistTile(s.current_tile, tt) <= MissileRange) Fire(s, target);
            }
            return true;
        }

        private static void Reload(List<Actor> fleet, City c)
        {
            foreach (Actor s in fleet)
            {
                int cap = MissileCap(s);
                if (cap == 0 || s.current_tile == null) continue;
                int n = Missiles(s);
                if (n >= cap) continue;
                if (Waters.Owner(s.current_tile) != c.kingdom) continue;
                _missiles[s.data.id] = Math.Min(cap, n + 2);
            }
        }

        public static void Clear()
        {
            _waypoint.Clear();
            _until.Clear();
            _missiles.Clear();
        }

        private static string Shape(Kingdom k)
        {
            int era = Research.Era(k);
            if (era < 8) return "abreast";
            if (era < 12) return "column";
            return "ring";
        }

        private static List<Actor> Fleet(City c)
        {
            List<Actor> l;
            if (!_byCity.TryGetValue(c, out l)) return new List<Actor>();
            var list = new List<Actor>(l);
            list.Sort((x, y) => y.getMaxHealth().CompareTo(x.getMaxHealth()));
            return list;
        }

        private static WorldTile Patrol(City c, Actor leader)
        {
            long id = c.data.id;
            WorldTile wp;
            float until;
            if (_waypoint.TryGetValue(id, out wp) && _until.TryGetValue(id, out until) && Time.time < until && wp != null && Toolbox.DistTile(leader.current_tile, wp) > 4f) return wp;
            List<TileZone> mine = Waters.Of(c.kingdom);
            WorldTile home = G.Tile(c);
            var near = new List<TileZone>();
            foreach (TileZone z in mine)
            {
                if (home != null && z.centerTile != null && Toolbox.DistTile(home, z.centerTile) < 60f) near.Add(z);
            }
            if (near.Count == 0) near = mine;
            if (near.Count == 0) return null;
            wp = Groups.WaterIn(near[UnityEngine.Random.Range(0, near.Count)]);
            _waypoint[id] = wp;
            _until[id] = Time.time + 25f;
            return wp;
        }

        private static Actor Intruder(Kingdom k, Actor leader)
        {
            Actor best = null;
            float bd = 70f;
            foreach (Kingdom o in Groups.Enemies(k))
            {
                foreach (Actor s in ShipsOf(o))
                {
                    if (!G.Ok(s) || s.current_tile == null) continue;
                    if (Waters.Owner(s.current_tile) != k) continue;
                    float dd = Toolbox.DistTile(leader.current_tile, s.current_tile);
                    if (dd < bd)
                    {
                        bd = dd;
                        best = s;
                    }
                }
            }
            return best;
        }

        private static BaseSimObject RaidTarget(Kingdom k, Actor raider, out WorldTile at)
        {
            at = null;
            BaseSimObject best = null;
            float bd = 300f;
            foreach (Kingdom o in Groups.Enemies(k))
            {
                foreach (City c in G.Cities(o))
                {
                    foreach (Building b in c.buildings)
                    {
                        if (b == null || !b.isAlive() || b.component_docks == null || b.component_docks.tiles_ocean == null || b.component_docks.tiles_ocean.Count == 0) continue;
                        WorldTile t = b.component_docks.tiles_ocean[0];
                        float d = Toolbox.DistTile(raider.current_tile, t);
                        if (d < bd)
                        {
                            bd = d;
                            best = b;
                            at = t;
                        }
                    }
                }
                foreach (TileZone z in Waters.Of(o))
                {
                    if (best != null) break;
                    WorldTile t = Groups.WaterIn(z);
                    if (t == null) continue;
                    float d = Toolbox.DistTile(raider.current_tile, t);
                    if (d < bd)
                    {
                        bd = d;
                        at = t;
                    }
                }
            }
            return best;
        }

        private static Actor Convoy(Kingdom k, Actor leader)
        {
            foreach (Actor s in ShipsOf(k))
            {
                if (!G.Ok(s) || s.current_tile == null || !Groups.Busy(s)) continue;
                if (Toolbox.DistTile(leader.current_tile, s.current_tile) < 50f) return s;
            }
            return null;
        }

        public static void Tick(float dt)
        {
            if (!Vehicles.On) return;
            _tick += dt;
            if (_tick < 2f) return;
            _tick = 0f;
            Collect();
            foreach (City c in _byCity.Keys.ToList())
            {
                if (!G.Ok(c) || c.kingdom == null || !c.kingdom.isCiv()) continue;
                List<Actor> fleet = Fleet(c);
                if (fleet.Count == 0) continue;
                Kingdom k = c.kingdom;
                bool war = World.world.wars.hasWars(k);
                string shape = Shape(k);
                Actor raider = null;
                if (war && fleet.Count >= 2)
                {
                    raider = fleet[fleet.Count - 1];
                    fleet.RemoveAt(fleet.Count - 1);
                    WorldTile at;
                    BaseSimObject t = RaidTarget(k, raider, out at);
                    if (t != null && raider.canAttackTarget(t) && at != null && Toolbox.DistTile(raider.current_tile, at) < 12f) raider.setAttackTarget(t);
                    else if (at != null) Groups.Send(raider, at, true);
                }
                Actor leader = fleet[0];
                if (war && Barrage(c, k, fleet)) continue;
                if (!war) Reload(fleet, c);
                if (war)
                {
                    Actor foe = Intruder(k, leader);
                    if (foe != null)
                    {
                        foreach (Actor s in fleet) if (s.canAttackTarget(foe)) s.setAttackTarget(foe);
                        Groups.Send(leader, foe.current_tile, true);
                        continue;
                    }
                    Actor convoy = Convoy(k, leader);
                    if (convoy != null) Groups.Send(leader, convoy.current_tile, true);
                    else Groups.Send(leader, Patrol(c, leader), true);
                }
                else
                {
                    Groups.Send(leader, Patrol(c, leader), true);
                }
                for (int i = 1; i < fleet.Count; i++) Groups.Follow(fleet[i], leader, Groups.Offset(shape, i - 1, 3f), true);
            }
        }
    }
}
