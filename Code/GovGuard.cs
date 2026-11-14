using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class GovGuard
    {
        private static float _next;
        private static float _flush;
        private static int _cursor;
        private static Vector3 _down;
        private static bool _held;

        public static void Clear()
        {
            _next = 0f;
            _cursor = 0;
        }

        public static bool On(Kingdom k)
        {
            return Gov.On("civ_gov_armies") && GovEra.Has(k, GovEra.Guard);
        }

        private static WorldTile Patrol(City c)
        {
            if (c.border_zones == null || c.border_zones.Count == 0) return null;
            TileZone z = c.border_zones.ElementAt(UnityEngine.Random.Range(0, c.border_zones.Count));
            return z != null ? z.centerTile : null;
        }

        private static WorldTile Target(Kingdom k, City c)
        {
            City threatened = G.Cities(k).Where(x => x.being_captured_by != null).OrderBy(x => G.Dist(x, c)).FirstOrDefault();
            bool capital = c == k.capital;
            if (threatened != null && (capital || G.Dist(threatened, c) < 60f)) return G.Tile(threatened);
            if (capital)
            {
                if (Profile.Get(k).AtWar || !G.Chance(0.4f)) return null;
                City edge = G.Cities(k).Where(x => x != c && x.neighbours_kingdoms != null && x.neighbours_kingdoms.Count > 0).OrderBy(x => UnityEngine.Random.value).FirstOrDefault();
                return edge != null ? Patrol(edge) : null;
            }
            if (Profile.Get(k).AtWar && c.neighbours_cities != null)
            {
                City e = c.neighbours_cities.Where(n => G.Ok(n) && n.kingdom != null && World.world.wars.isInWarWith(k, n.kingdom)).OrderBy(n => G.Dist(n, c)).FirstOrDefault();
                if (e != null) return G.Tile(e);
            }
            return G.Chance(0.2f) ? Patrol(c) : null;
        }

        private static void Drive()
        {
            List<City> all = G.Cities();
            if (all.Count == 0) return;
            int n = Math.Min(6, all.Count);
            for (int i = 0; i < n; i++)
            {
                _cursor = (_cursor + 1) % all.Count;
                City c = all[_cursor];
                if (!G.Ok(c) || c.army == null || c.kingdom == null || !c.kingdom.isCiv() || !On(c.kingdom)) continue;
                if (Player.Is(c.kingdom) && !Player.Auto(c.kingdom, "war")) continue;
                Actor cap = c.army.getCaptain();
                if (!G.Ok(cap) || cap.has_attack_target) continue;
                WorldTile t = Target(c.kingdom, c);
                if (t != null) cap.goTo(t, false, false, false, 0);
            }
        }

        public static void Command(List<Actor> units, WorldTile tile)
        {
            if (units.Count == 0 || tile == null) return;
            Kingdom k = units[0].kingdom;
            City at = tile.zone != null ? tile.zone.city : null;
            WorldTile dest = tile;
            if (at != null && at.kingdom == k) dest = G.Tile(at) ?? tile;
            foreach (Actor a in units)
            {
                if (!G.Ok(a) || a.kingdom != k) continue;
                a.cancelAllBeh();
                a.goTo(dest, false, false, false, 0);
                if (at != null && at.kingdom == k && a.army == null && at.army != null && a.isWarrior()) a.setArmy(at.army);
            }
        }

        public static bool SendAll(Kingdom k, City target)
        {
            if (!G.Ok(k) || !G.Ok(target)) return false;
            WorldTile t = G.Tile(target);
            List<Actor> l = k.getUnits().Where(a => G.Ok(a) && (a.isWarrior() || a.is_army_captain)).ToList();
            if (l.Count == 0 || t == null) return false;
            foreach (Actor a in l)
            {
                a.cancelAllBeh();
                a.goTo(t, false, false, false, 0);
            }
            return true;
        }

        private static void Order()
        {
            if (Input.GetMouseButtonDown(1))
            {
                _down = Input.mousePosition;
                _held = true;
            }
            if (!_held || !Input.GetMouseButtonUp(1)) return;
            _held = false;
            if ((Input.mousePosition - _down).sqrMagnitude > 64f || World.world.isOverUI() || SelectedUnit.countSelected() == 0) return;
            WorldTile tile = World.world.getMouseTilePos();
            if (tile == null) return;
            List<Actor> units = SelectedUnit.getAllSelectedList().Where(a => G.Ok(a) && a.kingdom != null && a.kingdom.isCiv()).ToList();
            if (units.Count == 0 || !Gov.On("civ_gov_armies")) return;
            Command(units, tile);
        }

        public static void Frame()
        {
            if (!G.Loaded || World.world == null) return;
            CivMod.Safe("gov-order", Order);
            CivMod.Safe("gov-sandbox-tools", GovSandbox.Tools);
            if (Time.unscaledTime >= _flush)
            {
                _flush = Time.unscaledTime + 2f;
                if (Gov.On("civ_gov_vassals")) CivMod.Safe("gov-vassals-flush", Vassals.Flush);
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1.5f;
            CivMod.Safe("gov-guard", Drive);
        }

        [HarmonyPatch(typeof(MapBox), nameof(MapBox.Update))]
        private static class Tick
        {
            private static void Postfix()
            {
                try
                {
                    Frame();
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
