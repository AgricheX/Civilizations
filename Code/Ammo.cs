using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class Ammo
    {
        private sealed class Mag
        {
            public string Kind;
            public int Max;
            public int Left;
        }

        private static readonly Dictionary<long, Mag> _mags = new Dictionary<long, Mag>();
        private static float _tick;

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.AmmoOn; }
        }

        private static string KindOf(string projectile)
        {
            switch (projectile)
            {
                case "arrow": return "arrows";
                case "rock": return "stones";
                case "shotgun_bullet":
                case "bullet":
                case "bullet_steel": return "bullets";
                case "cannonball":
                case "boat_cannonball": return "shells";
                case "firebomb": return "bombs";
                case "civ_air_missile": return "missiles";
                case "green_orb":
                case "freeze_orb":
                case "acid_ball":
                case "laser":
                case "plasma_ball": return "battery";
            }
            if (projectile.StartsWith("civ_bullet") || projectile.Contains("bullet")) return "bullets";
            if (projectile.Contains("plasma") || projectile.Contains("laser")) return "battery";
            return null;
        }

        private static int Capacity(Actor a, string kind)
        {
            UnitDef d = Vehicles.Def(a);
            int baseCap;
            switch (kind)
            {
                case "arrows": baseCap = 24; break;
                case "stones": baseCap = 20; break;
                case "bullets": baseCap = 40; break;
                case "shells": baseCap = 20; break;
                case "bombs": baseCap = 8; break;
                case "missiles": baseCap = 4; break;
                case "battery": baseCap = 30; break;
                default: baseCap = 20; break;
            }
            if (d == null) return baseCap;
            if (d.Cls == "ship") return baseCap * 2;
            if (d.Cls == "space") return baseCap * 3;
            if (d.Cls == "air") return kind == "bombs" ? 10 : baseCap * 2;
            if (d.Cls == "soldier") return baseCap + baseCap / 2;
            return baseCap + baseCap / 2;
        }

        private static Mag Get(Actor a)
        {
            if (a == null || a.data == null || a.kingdom == null || !a.kingdom.isCiv()) return null;
            Mag m;
            if (_mags.TryGetValue(a.data.id, out m)) return m;
            if (!a.hasRangeAttack()) return null;
            EquipmentAsset w = a.getWeaponAsset();
            if (w == null || string.IsNullOrEmpty(w.projectile)) return null;
            string kind = KindOf(w.projectile);
            if (kind == null) return null;
            int cap = Capacity(a, kind);
            m = new Mag { Kind = kind, Max = cap, Left = cap };
            _mags[a.data.id] = m;
            return m;
        }

        public static bool Empty(Actor a)
        {
            if (!On || a == null || a.data == null) return false;
            Mag m;
            return _mags.TryGetValue(a.data.id, out m) && m.Left <= 0;
        }

        public static string Label(Actor a)
        {
            Mag m = Get(a);
            if (m == null) return null;
            return m.Left + "/" + m.Max + " " + Txt.T("civ_ammo_" + m.Kind);
        }

        public static void Clear()
        {
            _mags.Clear();
        }

        private static WorldTile Home(Actor a)
        {
            UnitDef d = Vehicles.Def(a);
            City c = a.city;
            if (c == null || !c.isAlive())
            {
                c = null;
                float bd = float.MaxValue;
                foreach (City o in G.Cities(a.kingdom))
                {
                    WorldTile t = G.Tile(o);
                    if (t == null) continue;
                    float dd = Toolbox.DistTile(a.current_tile, t);
                    if (dd < bd)
                    {
                        bd = dd;
                        c = o;
                    }
                }
            }
            if (c == null) return null;
            if (d != null && d.Cls == "ship")
            {
                foreach (Building b in c.buildings)
                {
                    if (b != null && b.isAlive() && b.component_docks != null && b.component_docks.tiles_ocean != null && b.component_docks.tiles_ocean.Count > 0) return b.component_docks.tiles_ocean[0];
                }
                return null;
            }
            return G.Tile(c);
        }

        private static bool AtHome(Actor a, WorldTile home)
        {
            if (a.current_tile == null) return false;
            TileZone z = a.current_tile.zone;
            if (z != null && z.city != null && z.city.kingdom == a.kingdom) return true;
            return home != null && Toolbox.DistTile(a.current_tile, home) < 8f;
        }

        private static int Refill(Actor a, Mag m)
        {
            int step = Math.Max(1, m.Max / 4);
            City c = a.city;
            string res = m.Kind == "arrows" ? "wood" : m.Kind == "stones" ? "stone" : m.Kind == "battery" ? "civ_electronics" : "civ_ammo";
            if (c != null && Economy.Has(c, res, 1))
            {
                Economy.Take(c, res, 1);
                return step;
            }
            if (c != null && res == "civ_ammo" && Economy.Has(c, "common_metals", 1))
            {
                Economy.Take(c, "common_metals", 1);
                return step;
            }
            return Math.Max(1, step / 3);
        }

        public static void Tick(float dt)
        {
            if (!On)
            {
                if (_mags.Count > 0) _mags.Clear();
                return;
            }
            _tick += dt;
            if (_tick < 2f) return;
            _tick = 0f;
            var dead = new List<long>();
            int budget = 300;
            foreach (var kv in _mags)
            {
                Mag m = kv.Value;
                if (m.Left >= m.Max) continue;
                if (budget-- <= 0) break;
                Actor a = World.world.units.get(kv.Key);
                if (!G.Ok(a) || a.kingdom == null)
                {
                    dead.Add(kv.Key);
                    continue;
                }
                WorldTile home = Home(a);
                if (AtHome(a, home))
                {
                    m.Left = Math.Min(m.Max, m.Left + Refill(a, m));
                    continue;
                }
                if (m.Left <= 0 && home != null && a.current_tile != null)
                {
                    a.setAttackTarget(null);
                    a.has_attack_target = false;
                    UnitDef d = Vehicles.Def(a);
                    if (Toolbox.DistTile(a.current_tile, home) > 3f) a.goTo(home, d != null && d.Cls == "ship");
                }
            }
            foreach (long id in dead) _mags.Remove(id);
            if (_mags.Count > 20000) _mags.Clear();
        }

        [HarmonyPatch(typeof(Actor), nameof(Actor.tryToAttack))]
        private static class Shots
        {
            private static bool Prefix(Actor __instance, ref bool __result)
            {
                if (!On) return true;
                Mag m = Get(__instance);
                if (m == null) return true;
                if (m.Left > 0) return true;
                __result = false;
                return false;
            }

            private static void Postfix(Actor __instance, bool __result)
            {
                if (!On || !__result || __instance.data == null) return;
                Mag m;
                if (_mags.TryGetValue(__instance.data.id, out m) && m.Left > 0) m.Left--;
            }
        }
    }
}
