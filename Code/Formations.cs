using System;
using System.Collections.Generic;
using ai.behaviours;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class Formations
    {
        private sealed class Plan
        {
            public float Time;
            public readonly List<long> Body = new List<long>();
            public readonly List<long> Flank = new List<long>();
            public readonly List<long> Rear = new List<long>();
            public readonly List<long> Front = new List<long>();
            public int Width = 4;
            public int Depth = 2;
            public string Shape = "";
            public float Waited;
        }

        private static readonly Dictionary<long, Plan> _plans = new Dictionary<long, Plan>();
        private static float _tick;
        private static bool _hooked;

        public static FormationDef For(Kingdom k)
        {
            FormationDef best = null;
            if (k == null || !k.isCiv()) return null;
            foreach (FormationDef f in Db.Formations)
            {
                if (!Research.Has(k, f.Req)) continue;
                best = f;
            }
            return best;
        }

        private static int Role(Actor u)
        {
            UnitDef d = Vehicles.Def(u);
            if (d != null)
            {
                if (d.Cls == "soldier") return d.Role == "melee" ? 0 : d.Role == "heavy" ? 1 : 2;
                if (d.Cls == "air" || d.Cls == "space" || d.Cls == "ship") return 9;
                if (d.Cls == "siege" || d.Cls == "aa") return 4;
                if (d.Cls == "tank" && u.kingdom != null && Research.Era(u.kingdom) >= 11) return 5;
                return 3;
            }
            return u.hasRangeAttack() ? 2 : 0;
        }

        private static Plan PlanOf(Army army, FormationDef f)
        {
            long id = army.data.id;
            Plan p;
            if (_plans.TryGetValue(id, out p) && Time.time - p.Time < 3f && p.Shape == f.Shape) return p;
            p = new Plan { Time = Time.time, Shape = f.Shape, Waited = p != null ? p.Waited : 0f };
            var body = new List<KeyValuePair<int, long>>();
            Actor cap = army.getCaptain();
            foreach (Actor u in army.units)
            {
                if (u == null || u.data == null || u == cap) continue;
                int r = Role(u);
                if (r <= 2) body.Add(new KeyValuePair<int, long>(r, u.data.id));
                else if (r == 3) p.Flank.Add(u.data.id);
                else if (r == 4) p.Rear.Add(u.data.id);
                else if (r == 5) p.Front.Add(u.data.id);
            }
            body.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            foreach (var kv in body) p.Body.Add(kv.Value);
            p.Flank.Sort();
            p.Rear.Sort();
            p.Front.Sort();
            int n = Math.Max(1, p.Body.Count);
            int minY = 0, maxY = 0, minX = 0;
            for (int i = 0; i < p.Body.Count; i++)
            {
                Vector2Int o = Offset(f.Shape, i, n, f.Spacing, false);
                minY = Math.Min(minY, o.y);
                maxY = Math.Max(maxY, o.y);
                minX = Math.Min(minX, o.x);
            }
            p.Width = Math.Max(2, Math.Max(-minY, maxY));
            p.Depth = Math.Max(1, -minX);
            _plans[id] = p;
            return p;
        }

        public static Vector2Int Offset(string shape, int i, int n, int spacing, bool ranged)
        {
            spacing = Math.Max(1, spacing);
            switch (shape)
            {
                case "block":
                {
                    int w = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(n * 2)));
                    return new Vector2Int(-(i / w) * spacing, (i % w - w / 2) * spacing);
                }
                case "line_wide":
                {
                    int w = Math.Max(2, (n + 1) / 2);
                    return new Vector2Int(-(i / w) * 2, (i % w - w / 2) * spacing * 2);
                }
                case "line":
                {
                    int w = Math.Max(3, (n + 2) / 3);
                    return new Vector2Int(-(i / w), (i % w - w / 2) * spacing);
                }
                case "checker":
                {
                    int block = i / 6, inBlock = i % 6;
                    int row = block / 3, col = block % 3;
                    int stagger = row % 2 == 1 ? 2 : 0;
                    return new Vector2Int(-row * 4 - inBlock / 3, (col - 1) * 5 + stagger + inBlock % 3 - 1);
                }
                case "wedge":
                {
                    int row = 0, used = 0;
                    while (used + row + 1 <= i)
                    {
                        used += row + 1;
                        row++;
                    }
                    int pos = i - used;
                    return new Vector2Int(-row * spacing, (pos * 2 - row) * spacing);
                }
                case "square":
                {
                    int side = Math.Max(3, (int)Math.Ceiling(n / 4f) + 1);
                    int perim = side * 4 - 4;
                    if (i >= perim) return new Vector2Int(-(side / 2), (i - perim) % Math.Max(1, side - 2) - side / 2 + 1);
                    int s = i / (side - 1), p = i % (side - 1);
                    switch (s)
                    {
                        case 0: return new Vector2Int(0, p - side / 2);
                        case 1: return new Vector2Int(-p, side / 2);
                        case 2: return new Vector2Int(-(side - 1), side / 2 - p);
                        default: return new Vector2Int(-(side - 1) + p, -side / 2);
                    }
                }
                case "squads":
                {
                    int squad = i / 4, m = i % 4;
                    int sw = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, n / 4))));
                    int sr = squad / sw, sc = squad % sw;
                    return new Vector2Int(-sr * 5 - m / 2, (sc - sw / 2) * 5 + m % 2);
                }
                default:
                {
                    float ang = i * 2.399f;
                    float r = 1.5f + (float)Math.Sqrt(i) * 1.3f;
                    return new Vector2Int((int)(Math.Cos(ang) * r), (int)(Math.Sin(ang) * r));
                }
            }
        }

        private static WorldTile Slot(Actor a, Actor captain, Army army)
        {
            FormationDef f = For(a.kingdom);
            if (f == null) return null;
            Plan plan = PlanOf(army, f);
            long me = a.data.id;
            Vector2 off;
            int idx = plan.Body.IndexOf(me);
            if (idx >= 0)
            {
                Vector2Int o = Offset(f.Shape, idx, Math.Max(1, plan.Body.Count), f.Spacing, false);
                off = new Vector2(o.x, o.y);
            }
            else if ((idx = plan.Flank.IndexOf(me)) >= 0)
            {
                float side = idx % 2 == 0 ? 1f : -1f;
                int k = idx / 2;
                off = new Vector2(-(k / 3) * 3, side * (plan.Width + 3 + (k % 3) * 2));
            }
            else if ((idx = plan.Front.IndexOf(me)) >= 0)
            {
                off = new Vector2(3 + (idx / 5) * 3, (idx % 5 - 2) * 3);
            }
            else if ((idx = plan.Rear.IndexOf(me)) >= 0)
            {
                off = new Vector2(-(plan.Depth + 4 + (idx / 5) * 3), (idx % 5 - 2) * 3);
            }
            else return null;
            WorldTile anchor = captain.current_tile;
            Vector2 dir = Vector2.right;
            List<WorldTile> path = captain.current_path;
            if (path != null && path.Count > 0)
            {
                WorldTile end = path[path.Count - 1];
                dir = new Vector2(Globe.DX(anchor.x, end.x), Globe.DY(anchor.y, end.y));
                if (dir.sqrMagnitude > 64f) anchor = path[Math.Min(path.Count - 1, captain.current_path_index + 6)];
            }
            else if (captain.has_attack_target && captain.attack_target != null && captain.attack_target.current_tile != null)
            {
                WorldTile e = captain.attack_target.current_tile;
                dir = new Vector2(Globe.DX(anchor.x, e.x), Globe.DY(anchor.y, e.y));
            }
            else if (captain.city != null && captain.city.target_attack_zone != null && captain.city.target_attack_zone.centerTile != null)
            {
                WorldTile e = captain.city.target_attack_zone.centerTile;
                dir = new Vector2(Globe.DX(anchor.x, e.x), Globe.DY(anchor.y, e.y));
            }
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            dir.Normalize();
            Vector2 sideV = new Vector2(-dir.y, dir.x);
            Vector2 p = new Vector2(anchor.x, anchor.y) + dir * (off.x - 1) + sideV * off.y;
            WorldTile t = Globe.Tile(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (t == null || t.Type == null || t.Type.block || t.Type.liquid && !a.isFlying()) return null;
            if (!t.isSameIsland(a.current_tile) && !a.isFlying()) return null;
            return t;
        }

        private static bool CivFollow(Actor a)
        {
            if (a == null || a.kingdom == null || !a.kingdom.isCiv() || !a.hasCity() || !a.hasArmy() || !a.army.hasCaptain()) return false;
            Actor cap = a.army.getCaptain();
            if (cap == null || cap == a || cap.current_tile == null || a.current_tile == null) return false;
            if (!a.current_tile.isSameIsland(cap.current_tile)) return false;
            if (a.has_attack_target) return false;
            return Toolbox.DistTile(a.current_tile, cap.current_tile) > 2f;
        }

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            DecisionAsset d = AssetManager.decisions_library.get("warrior_army_follow_leader");
            if (d == null || d.action_check_launch == null) return;
            DecisionAction orig = d.action_check_launch;
            d.action_check_launch = a => orig(a) || CivFollow(a);
        }

        public static void Tick(float dt)
        {
            Hook();
            _tick += dt;
            if (_tick < 1f) return;
            _tick = 0f;
            if (World.world == null || World.world.armies == null) return;
            foreach (Army army in World.world.armies)
            {
                if (army == null || !army.isAlive() || !army.hasCaptain()) continue;
                Actor cap = army.getCaptain();
                if (cap == null || cap.kingdom == null || !cap.kingdom.isCiv() || cap.city == null || cap.current_tile == null) continue;
                FormationDef f = For(cap.kingdom);
                if (f == null || army.units.Count < 4) continue;
                Plan plan = PlanOf(army, f);
                TileZone z = cap.city.target_attack_zone;
                if (z == null || z.centerTile == null)
                {
                    plan.Waited = 0f;
                    continue;
                }
                float dist = Toolbox.DistTile(cap.current_tile, z.centerTile);
                if (dist > 40f) plan.Waited = 0f;
                if (dist > 40f || dist < 8f || plan.Waited > 16f) continue;
                int near = 0, total = 0;
                foreach (Actor u in army.units)
                {
                    if (u == null || u == cap || u.current_tile == null) continue;
                    total++;
                    if (Toolbox.DistTile(u.current_tile, cap.current_tile) <= plan.Width + plan.Depth + 4) near++;
                }
                bool formed = total == 0 || near / (float)total >= 0.7f;
                bool support = !Cautious(cap) || SupportReady(cap, dist);
                if (formed && support) continue;
                float limit = Cautious(cap) ? 16f : 8f;
                if (plan.Waited > limit) continue;
                cap.makeWait(1f);
                plan.Waited += 1f;
            }
            if (_plans.Count > 400) _plans.Clear();
        }

        public static bool Cautious(Actor general)
        {
            if (general == null) return false;
            if (general.hasTrait("bloodlust") || general.hasTrait("ambitious") || general.hasTrait("madness")) return false;
            if (general.hasTrait("wise") || general.hasTrait("genius") || general.hasTrait("paranoid")) return true;
            return general.data.id % 2 == 0;
        }

        private static bool SupportReady(Actor general, float distToTarget)
        {
            Kingdom k = general.kingdom;
            if (k == null || Aircraft.CountAir(k) == 0) return true;
            if (Aircraft.SupportCaptain(k) != general) return true;
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "air") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor a in asset.units)
                {
                    if (G.Ok(a) && a.kingdom == k && a.current_tile != null && Toolbox.DistTile(a.current_tile, general.current_tile) < 14f) return true;
                }
            }
            return false;
        }

        public static void Clear()
        {
            _plans.Clear();
        }

        [HarmonyPatch(typeof(BehFindTileNearbyGroupLeader), nameof(BehFindTileNearbyGroupLeader.execute))]
        private static class Patch
        {
            private static bool Prefix(Actor pActor, ref BehResult __result)
            {
                try
                {
                    if (pActor != null && Ammo.Empty(pActor))
                    {
                        __result = BehResult.Stop;
                        return false;
                    }
                    if (pActor == null || !pActor.hasArmy() || !pActor.army.hasCaptain() || pActor.kingdom == null || !pActor.kingdom.isCiv()) return true;
                    Actor captain = pActor.army.getCaptain();
                    if (captain == null || captain == pActor || captain.current_tile == null) return true;
                    WorldTile t = Slot(pActor, captain, pActor.army);
                    if (t == null) return true;
                    pActor.beh_tile_target = t;
                    __result = BehResult.Continue;
                    return false;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }
    }
}
