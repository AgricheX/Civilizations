using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class Carriers
    {
        public const string KOf = "civ_carrier_of";
        private const string KHangar = "civ_hangar";
        private const string KUp = "civ_air_since";
        private static float _tick;

        public static bool IsCarrier(Actor a)
        {
            return a != null && a.asset != null && (a.asset.id == "civ_carrier" || a.asset.id == "civ_jp_carrier");
        }

        public static int Capacity(Actor c)
        {
            return c.asset.id == "civ_jp_carrier" ? 8 : 6;
        }

        public static bool Embarked(Actor a)
        {
            return a != null && a.data != null && Store.Str(a.data, KOf).Length > 0;
        }

        private static List<string> Hangar(Actor c)
        {
            string s = Store.Str(c.data, KHangar);
            return s.Length == 0 ? new List<string>() : s.Split(';').Where(x => x.Length > 0).ToList();
        }

        private static void Save(Actor c, List<string> h)
        {
            Store.Put(c.data, KHangar, string.Join(";", h.ToArray()));
        }

        private static IEnumerable<Actor> All()
        {
            foreach (string id in new[] { "civ_carrier", "civ_jp_carrier" })
            {
                ActorAsset asset = AssetManager.actor_library.get(id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor a in asset.units.ToList()) if (G.Ok(a) && a.current_tile != null) yield return a;
            }
        }

        private static string PickPlane(Actor carrier, int slot)
        {
            Kingdom k = carrier.kingdom;
            if (k == null) return null;
            if (carrier.asset.id == "civ_jp_carrier" && slot % 4 == 3 && AssetManager.actor_library.get("civ_jp_heli") != null && Research.Has(k, "helicopters")) return "civ_jp_heli";
            string role = slot % 3 == 2 ? "bomber" : "fighter";
            List<UnitDef> opts = Vehicles.Options(k, d => d.Cls == "air" && d.Role == role && !d.Art.Contains("heli") && d.Base != "civ_drone");
            if (opts.Count == 0) opts = Vehicles.Options(k, d => d.Cls == "air" && (d.Role == "fighter" || d.Role == "bomber"));
            if (opts.Count == 0) return null;
            return opts.OrderByDescending(d => d.Era).First().Id;
        }

        public static void CityYear(City c, int yrs)
        {
            foreach (Actor carrier in All())
            {
                if (carrier.city != c) continue;
                List<string> h = Hangar(carrier);
                int flying = Launched(carrier).Count;
                int add = Math.Min(2 * Math.Max(1, yrs), Capacity(carrier) - h.Count - flying);
                for (int i = 0; i < add; i++)
                {
                    string id = PickPlane(carrier, h.Count + flying);
                    UnitDef d;
                    if (id == null || !Db.UnitById.TryGetValue(id, out d) || !Vehicles.CanAfford(c, d)) break;
                    Vehicles.PayFor(c, d);
                    h.Add(id + "|");
                }
                Save(carrier, h);
            }
        }

        private static List<Actor> Launched(Actor carrier)
        {
            var list = new List<Actor>();
            string me = carrier.data.id.ToString();
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "air") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor a in asset.units) if (G.Ok(a) && a.data != null && Store.Str(a.data, KOf) == me) list.Add(a);
            }
            return list;
        }

        private static BaseSimObject Target(Actor carrier)
        {
            Kingdom k = carrier.kingdom;
            if (k == null || !World.world.wars.hasWars(k)) return null;
            BaseSimObject best = null;
            float bd = 32f;
            foreach (Kingdom o in Groups.Enemies(k))
            {
                foreach (UnitDef d in Db.Units)
                {
                    if (d.Cls != "ship" && d.Cls != "air") continue;
                    ActorAsset asset = AssetManager.actor_library.get(d.Id);
                    if (asset == null || asset.units == null) continue;
                    foreach (Actor u in asset.units)
                    {
                        if (!G.Ok(u) || u.kingdom != o || u.current_tile == null) continue;
                        float dd = Toolbox.DistTile(carrier.current_tile, u.current_tile);
                        if (dd < bd)
                        {
                            bd = dd;
                            best = u;
                        }
                    }
                }
                if (best != null) continue;
                foreach (City c in G.Cities(o))
                {
                    WorldTile t = G.Tile(c);
                    if (t == null || Toolbox.DistTile(carrier.current_tile, t) > 40f) continue;
                    foreach (Building b in c.buildings)
                    {
                        if (b == null || !b.isAlive() || b.current_tile == null) continue;
                        float dd = Toolbox.DistTile(carrier.current_tile, b.current_tile);
                        if (dd < 40f && dd < bd + 10f)
                        {
                            best = b;
                            bd = dd;
                        }
                    }
                }
            }
            return best;
        }

        private static Actor Launch(Actor carrier, string entry)
        {
            int i = entry.IndexOf('|');
            string id = i >= 0 ? entry.Substring(0, i) : entry;
            string name = i >= 0 ? entry.Substring(i + 1) : "";
            if (AssetManager.actor_library.get(id) == null) return null;
            Actor p = World.world.units.createNewUnit(id, carrier.current_tile, false, 0f, null, null, false, true);
            if (p == null) return null;
            if (carrier.kingdom != null) p.setKingdom(carrier.kingdom);
            if (carrier.city != null) p.setCity(carrier.city);
            p.setProfession(UnitProfession.Warrior);
            if (name.Length > 0) p.setName(name, false);
            else VehicleNames.Apply(p);
            Store.Put(p.data, Vehicles.KRider, "");
            Store.Put(p.data, KOf, carrier.data.id.ToString());
            Store.Put(p.data, KUp, Time.time);
            return p;
        }

        private static void Land(Actor carrier, Actor p, List<string> h)
        {
            h.Add(p.asset.id + "|" + (p.getName() ?? "").Replace("|", "").Replace(";", ""));
            ActionLibrary.removeUnit(p);
        }

        public static void Tick(float dt)
        {
            _tick += dt;
            if (_tick < 2f) return;
            _tick = 0f;
            var alive = new HashSet<string>();
            foreach (Actor carrier in All())
            {
                alive.Add(carrier.data.id.ToString());
                List<string> h = Hangar(carrier);
                List<Actor> up = Launched(carrier);
                BaseSimObject target = Target(carrier);
                if (target != null && h.Count > 0)
                {
                    foreach (string e in h)
                    {
                        Actor p = Launch(carrier, e);
                        if (p != null) up.Add(p);
                    }
                    h.Clear();
                }
                int ring = 0;
                foreach (Actor p in up)
                {
                    float since = Time.time - Store.Flt(p.data, KUp, Time.time);
                    if (target != null && since < 70f)
                    {
                        if (!p.has_attack_target && p.canAttackTarget(target)) p.setAttackTarget(target);
                        else if (!p.has_attack_target) Groups.Send(p, target.current_tile, false);
                        continue;
                    }
                    if (p.isFavorite())
                    {
                        Groups.Follow(p, carrier, Groups.Offset("ring", ring++, 3f), false);
                        continue;
                    }
                    if (Toolbox.DistTile(p.current_tile, carrier.current_tile) <= 3f) Land(carrier, p, h);
                    else Groups.Send(p, carrier.current_tile, false);
                }
                Save(carrier, h);
            }
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "air") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor a in asset.units)
                {
                    if (!G.Ok(a) || a.data == null) continue;
                    string of = Store.Str(a.data, KOf);
                    if (of.Length > 0 && !alive.Contains(of)) Store.Put(a.data, KOf, "");
                }
            }
        }
    }
}
