using System;
using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal static class Groups
    {
        public static Vector2 Dir(WorldTile from, WorldTile to)
        {
            if (from == null || to == null) return Vector2.right;
            var d = new Vector2(Globe.DX(from.x, to.x), Globe.DY(from.y, to.y));
            if (d.sqrMagnitude < 0.01f) return Vector2.right;
            return d.normalized;
        }

        public static Vector2 Heading(Actor leader)
        {
            List<WorldTile> path = leader.current_path;
            if (path != null && path.Count > 0) return Dir(leader.current_tile, path[path.Count - 1]);
            if (leader.has_attack_target && leader.attack_target != null) return Dir(leader.current_tile, leader.attack_target.current_tile);
            return Vector2.right;
        }

        public static Vector2 Offset(string shape, int i, float gap)
        {
            int k = i + 1;
            switch (shape)
            {
                case "column":
                    return new Vector2(-k * gap, 0f);
                case "abreast":
                    return new Vector2(0f, (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * gap);
                case "ring":
                {
                    float ang = i * 1.2566f;
                    float r = gap * (1.2f + i / 5);
                    return new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                }
                case "pair":
                    return new Vector2(-((k + 1) / 2) * gap, (k % 2 == 0 ? 1f : -1f) * gap * 0.6f);
                default:
                    return new Vector2(-((k + 1) / 2) * gap, (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * gap);
            }
        }

        public static WorldTile Slot(Actor leader, Vector2 off, bool water)
        {
            if (leader.current_tile == null) return null;
            Vector2 dir = Heading(leader);
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 p = new Vector2(leader.current_tile.x, leader.current_tile.y) + dir * off.x + side * off.y;
            WorldTile t = Globe.Tile(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (t == null || t.Type == null) return null;
            if (water && !t.Type.liquid) return null;
            return t;
        }

        public static void Follow(Actor a, Actor leader, Vector2 off, bool water)
        {
            if (a == null || leader == null || a == leader || a.current_tile == null || Ammo.Empty(a)) return;
            if (a.has_attack_target && a.attack_target != null && a.attack_target.isAlive()) return;
            WorldTile t = Slot(leader, off, water) ?? leader.current_tile;
            if (Toolbox.DistTile(a.current_tile, t) <= 2.5f) return;
            a.goTo(t, water);
        }

        public static void Send(Actor a, WorldTile t, bool water)
        {
            if (a == null || t == null || a.current_tile == null || Ammo.Empty(a)) return;
            if (a.has_attack_target && a.attack_target != null && a.attack_target.isAlive()) return;
            if (Toolbox.DistTile(a.current_tile, t) <= 3f) return;
            a.goTo(t, water);
        }

        public static bool Busy(Actor ship)
        {
            if (ship == null || ship.asset == null || !ship.asset.is_boat) return false;
            Boat b = ship.getSimpleComponent<Boat>();
            return b != null && (b.hasPassengers() || b.taxi_request != null);
        }

        public static WorldTile WaterIn(TileZone z)
        {
            if (z == null) return null;
            WorldTile c = z.centerTile;
            if (c != null && c.Type != null && c.Type.liquid) return c;
            foreach (WorldTile t in z.tiles) if (t != null && t.Type != null && t.Type.liquid) return t;
            return null;
        }

        public static IEnumerable<Kingdom> Enemies(Kingdom k)
        {
            foreach (global::War w in World.world.wars.getWars(k))
            {
                if (w == null || w.isRekt()) continue;
                foreach (Kingdom o in w.getOppositeSideKingdom(k)) if (o != null && o.isAlive()) yield return o;
            }
        }
    }
}
