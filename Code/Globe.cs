using System;
using System.Collections.Generic;
using ai;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Civilizations
{
    internal static class Globe
    {
        private static bool _generating;
        private static int _lastW = -1;
        private static int _lastH = -1;
        private static int _lastMode = -1;

        public static int Mode
        {
            get { return CivMod.C == null ? 1 : CivMod.C.GlobeMode; }
        }

        public static bool WrapX
        {
            get { return Mode >= 1 && World.world != null && MapBox.width > 1; }
        }

        public static bool WrapY
        {
            get { return Mode >= 2 && World.world != null && MapBox.height > 1; }
        }

        public static int WrapInt(int v, int max)
        {
            if (max <= 0) return v;
            v %= max;
            return v < 0 ? v + max : v;
        }

        public static float WrapF(float v, int max)
        {
            if (max <= 0) return v;
            v %= max;
            return v < 0f ? v + max : v;
        }

        public static float Delta(float a, float b, int max, bool wrap)
        {
            float d = b - a;
            if (!wrap || max <= 1) return d;
            float half = max * 0.5f;
            if (d > half) d -= max;
            else if (d < -half) d += max;
            return d;
        }

        public static float DX(float a, float b)
        {
            return Delta(a, b, MapBox.width, WrapX);
        }

        public static float DY(float a, float b)
        {
            return Delta(a, b, MapBox.height, WrapY);
        }

        public static float SqDist(float ax, float ay, float bx, float by)
        {
            float dx = DX(ax, bx);
            float dy = DY(ay, by);
            return dx * dx + dy * dy;
        }

        public static Vector2 Nearest(Vector2 p, Vector2 origin)
        {
            return new Vector2(origin.x + DX(origin.x, p.x), origin.y + DY(origin.y, p.y));
        }

        public static Vector2 Wrap(Vector2 p)
        {
            if (WrapX) p.x = WrapF(p.x, MapBox.width);
            if (WrapY) p.y = WrapF(p.y, MapBox.height);
            return p;
        }

        public static WorldTile Tile(int x, int y)
        {
            if (World.world == null || World.world.tiles_map == null) return null;
            int w = MapBox.width, h = MapBox.height;
            if (WrapX) x = WrapInt(x, w);
            if (WrapY) y = WrapInt(y, h);
            if (x < 0 || y < 0 || x >= w || y >= h) return null;
            return World.world.tiles_map[x, y];
        }

        public static void Check()
        {
            if (World.world == null || World.world.tiles_list == null) return;
            if (MapBox.width == _lastW && MapBox.height == _lastH && Mode == _lastMode) return;
            _lastW = MapBox.width;
            _lastH = MapBox.height;
            _lastMode = Mode;
            if (Mode > 0) Reconnect();
        }

        public static void Invalidate()
        {
            _lastW = -1;
        }

        public static void Reconnect()
        {
            if (World.world == null || World.world.tiles_list == null || Mode == 0) return;
            try
            {
                ReconnectTiles();
                ReconnectZones();
                ReconnectChunks();
                World.world.map_chunk_manager.allDirty();
                CivMod.Log("globe reconnected " + MapBox.width + "x" + MapBox.height);
            }
            catch (Exception e)
            {
                Debug.LogError("[Civilizations] globe: " + e);
            }
        }

        private static void ReconnectTiles()
        {
            int w = MapBox.width, h = MapBox.height;
            foreach (WorldTile t in World.world.tiles_list)
            {
                if (t == null) continue;
                bool edgeX = t.x == 0 || t.x == w - 1;
                bool edgeY = t.y == 0 || t.y == h - 1;
                if (!(edgeX && WrapX) && !(edgeY && WrapY)) continue;
                WorldTile l = Tile(t.x - 1, t.y), r = Tile(t.x + 1, t.y), d = Tile(t.x, t.y - 1), u = Tile(t.x, t.y + 1);
                t.tile_left = l;
                t.tile_right = r;
                t.tile_down = d;
                t.tile_up = u;
                var n = new List<WorldTile>();
                var all = new List<WorldTile>();
                foreach (WorldTile x in new[] { l, r, d, u })
                {
                    if (x == null) continue;
                    n.Add(x);
                    all.Add(x);
                }
                foreach (WorldTile x in new[] { Tile(t.x - 1, t.y - 1), Tile(t.x - 1, t.y + 1), Tile(t.x + 1, t.y - 1), Tile(t.x + 1, t.y + 1) })
                {
                    if (x != null) all.Add(x);
                }
                t.neighbours = n.ToArray();
                t.neighboursAll = all.ToArray();
                t.world_edge = n.Count < 4;
            }
        }

        private static TileZone Zone(ZoneCalculator zc, int x, int y)
        {
            if (WrapX) x = WrapInt(x, zc.zones_total_x);
            if (WrapY) y = WrapInt(y, zc.zones_total_y);
            if (x < 0 || y < 0 || x >= zc.zones_total_x || y >= zc.zones_total_y) return null;
            return zc.getZoneUnsafe(x, y);
        }

        private static void ReconnectZones()
        {
            ZoneCalculator zc = World.world.zone_calculator;
            if (zc == null || zc.zones == null) return;
            foreach (TileZone z in zc.zones)
            {
                if (z == null) continue;
                bool edgeX = z.x == 0 || z.x == zc.zones_total_x - 1;
                bool edgeY = z.y == 0 || z.y == zc.zones_total_y - 1;
                if (!(edgeX && WrapX) && !(edgeY && WrapY)) continue;
                TileZone l = Zone(zc, z.x - 1, z.y), r = Zone(zc, z.x + 1, z.y), d = Zone(zc, z.x, z.y - 1), u = Zone(zc, z.x, z.y + 1);
                z.zone_left = l;
                z.zone_right = r;
                z.zone_down = d;
                z.zone_up = u;
                var n = new List<TileZone>();
                var all = new List<TileZone>();
                foreach (TileZone x in new[] { l, r, d, u })
                {
                    if (x == null) continue;
                    n.Add(x);
                    all.Add(x);
                }
                foreach (TileZone x in new[] { Zone(zc, z.x - 1, z.y - 1), Zone(zc, z.x - 1, z.y + 1), Zone(zc, z.x + 1, z.y - 1), Zone(zc, z.x + 1, z.y + 1) })
                {
                    if (x != null) all.Add(x);
                }
                z.neighbours = n.ToArray();
                z.neighbours_all = all.ToArray();
                z.world_edge = n.Count < 4;
            }
        }

        private static void ReconnectChunks()
        {
            MapChunkManager m = World.world.map_chunk_manager;
            if (m == null || m.chunks == null || m._map == null) return;
            int ax = m.amount_x, ay = m._amount_y;
            Func<int, int, MapChunk> get = (x, y) =>
            {
                if (WrapX) x = WrapInt(x, ax);
                if (WrapY) y = WrapInt(y, ay);
                if (x < 0 || y < 0 || x >= ax || y >= ay) return null;
                return m._map[x, y];
            };
            foreach (MapChunk c in m.chunks)
            {
                if (c == null) continue;
                bool edgeX = c.x == 0 || c.x == ax - 1;
                bool edgeY = c.y == 0 || c.y == ay - 1;
                if (!(edgeX && WrapX) && !(edgeY && WrapY)) continue;
                MapChunk l = get(c.x - 1, c.y), r = get(c.x + 1, c.y), d = get(c.x, c.y - 1), u = get(c.x, c.y + 1);
                c.chunk_left = l;
                c.chunk_right = r;
                c.chunk_down = d;
                c.chunk_up = u;
                var n = new List<MapChunk>();
                var all = new List<MapChunk>();
                foreach (MapChunk x in new[] { l, r, d, u })
                {
                    if (x == null) continue;
                    n.Add(x);
                    all.Add(x);
                }
                foreach (MapChunk x in new[] { get(c.x - 1, c.y - 1), get(c.x - 1, c.y + 1), get(c.x + 1, c.y - 1), get(c.x + 1, c.y + 1) })
                {
                    if (x != null) all.Add(x);
                }
                c.neighbours = n.ToArray();
                c.neighbours_all = all.ToArray();
                c.world_edge = n.Count < 4;
            }
            foreach (MapChunk c in m.chunks) if (c != null) c.generateEdgeConnections();
        }

        public static void WrapActor(Actor a)
        {
            if (a == null || (!WrapX && !WrapY)) return;
            Vector2 p = a.current_position;
            Vector2 w = Wrap(p);
            if (w == p) return;
            Vector2 shift = w - p;
            a.current_position = w;
            a.next_step_position += shift;
        }

        private static readonly List<WorldTile> _seam = new List<WorldTile>();

        private static bool Walkable(Actor a, WorldTile t)
        {
            if (t == null || t.Type == null) return false;
            if (a.isFlying()) return true;
            if (a.asset.is_boat) return t.isGoodForBoat();
            if (t.Type.block && !a.ignoresBlocks()) return false;
            if (t.Type.lava) return false;
            if (a.isWaterCreature()) return t.Type.ocean;
            if (t.Type.ocean && a.isDamagedByOcean()) return false;
            return true;
        }

        public static bool SeamPath(Actor a, WorldTile from, WorldTile to)
        {
            if (!WrapX && !WrapY) return false;
            float rawX = Math.Abs(to.x - from.x), rawY = Math.Abs(to.y - from.y);
            float wx = Math.Abs(DX(from.x, to.x)), wy = Math.Abs(DY(from.y, to.y));
            if (wx >= rawX - 0.5f && wy >= rawY - 0.5f) return false;
            float steps = Math.Max(wx, wy);
            int limit = a.asset.is_boat ? 160 : a.isFlying() ? 400 : 48;
            if (steps > limit) return false;
            _seam.Clear();
            WorldTile cur = from;
            var visited = new HashSet<WorldTile> { from };
            int guard = (int)steps + 16;
            while (cur != to && guard-- > 0)
            {
                int sx = Math.Sign(DX(cur.x, to.x)), sy = Math.Sign(DY(cur.y, to.y));
                WorldTile best = null;
                float bd = float.MaxValue;
                foreach (WorldTile n in new[] { Tile(cur.x + sx, cur.y + sy), Tile(cur.x + sx, cur.y), Tile(cur.x, cur.y + sy) })
                {
                    if (n == null || visited.Contains(n) || !Walkable(a, n)) continue;
                    float d = SqDist(n.x, n.y, to.x, to.y);
                    if (d < bd)
                    {
                        bd = d;
                        best = n;
                    }
                }
                if (best == null && cur.neighboursAll != null)
                {
                    foreach (WorldTile n in cur.neighboursAll)
                    {
                        if (n == null || visited.Contains(n) || !Walkable(a, n)) continue;
                        float d = SqDist(n.x, n.y, to.x, to.y);
                        if (d < bd)
                        {
                            bd = d;
                            best = n;
                        }
                    }
                }
                if (best == null) return false;
                visited.Add(best);
                _seam.Add(best);
                cur = best;
            }
            if (cur != to || _seam.Count == 0) return false;
            a.current_path.Clear();
            a.current_path.AddRange(_seam);
            a.current_path_index = 0;
            return true;
        }

        [HarmonyPatch(typeof(MapBox), nameof(MapBox.GetTile), new Type[] { typeof(int), typeof(int) })]
        private static class GetTilePatch
        {
            private static bool Prefix(ref int pX, ref int pY)
            {
                if (WrapX) pX = WrapInt(pX, MapBox.width);
                if (WrapY) pY = WrapInt(pY, MapBox.height);
                return true;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.getTileAt))]
        private static class TileAtPatch
        {
            private static bool Prefix(float pX, float pY, ref WorldTile __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = Tile(Mathf.FloorToInt(pX), Mathf.FloorToInt(pY)) ?? World.world.GetTileSimple(Mathf.Clamp(Mathf.FloorToInt(pX), 0, MapBox.width - 1), Mathf.Clamp(Mathf.FloorToInt(pY), 0, MapBox.height - 1));
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.DistTile))]
        private static class D1
        {
            private static bool Prefix(WorldTile pT1, WorldTile pT2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = Mathf.Sqrt(SqDist(pT1.x, pT1.y, pT2.x, pT2.y));
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.SquaredDistTile))]
        private static class D2
        {
            private static bool Prefix(WorldTile pT1, WorldTile pT2, ref int __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = (int)SqDist(pT1.x, pT1.y, pT2.x, pT2.y);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.DistVec2Float))]
        private static class D3
        {
            private static bool Prefix(Vector2 pT1, Vector2 pT2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = Mathf.Sqrt(SqDist(pT1.x, pT1.y, pT2.x, pT2.y));
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.DistVec3))]
        private static class D4
        {
            private static bool Prefix(Vector3 pT1, Vector3 pT2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = Mathf.Sqrt(SqDist(pT1.x, pT1.y, pT2.x, pT2.y));
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.SquaredDistVec2Float))]
        private static class D5
        {
            private static bool Prefix(Vector2 pT1, Vector2 pT2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = SqDist(pT1.x, pT1.y, pT2.x, pT2.y);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.SquaredDistVec3))]
        private static class D6
        {
            private static bool Prefix(Vector3 pT1, Vector3 pT2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = SqDist(pT1.x, pT1.y, pT2.x, pT2.y);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Dist), new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) })]
        private static class D7
        {
            private static bool Prefix(float x1, float y1, float x2, float y2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = Mathf.Sqrt(SqDist(x1, y1, x2, y2));
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Dist), new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) })]
        private static class D8
        {
            private static bool Prefix(int x1, int y1, int x2, int y2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = Mathf.Sqrt(SqDist(x1, y1, x2, y2));
                return false;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.SquaredDist), new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) })]
        private static class D9
        {
            private static bool Prefix(float x1, float y1, float x2, float y2, ref float __result)
            {
                if (!WrapX && !WrapY) return true;
                __result = SqDist(x1, y1, x2, y2);
                return false;
            }
        }

        [HarmonyPatch(typeof(Actor), "updateMovement")]
        private static class Move
        {
            private static void Prefix(Actor __instance)
            {
                if (!WrapX && !WrapY) return;
                __instance.next_step_position = Nearest(__instance.next_step_position, __instance.current_position);
            }

            private static void Postfix(Actor __instance)
            {
                WrapActor(__instance);
            }
        }

        [HarmonyPatch(typeof(ActorMove), nameof(ActorMove.goTo))]
        private static class GoTo
        {
            private static void Postfix(Actor pActor, WorldTile pTileTarget, ref ExecuteEvent __result)
            {
                if (!WrapX && !WrapY) return;
                if (pActor == null || pTileTarget == null || pActor.current_tile == null) return;
                try
                {
                    if (__result == ExecuteEvent.True && pActor.current_path.Count > 0)
                    {
                        float raw = Math.Abs(pTileTarget.x - pActor.current_tile.x);
                        if (raw < MapBox.width * 0.5f) return;
                    }
                    if (SeamPath(pActor, pActor.current_tile, pTileTarget))
                    {
                        pActor.setTileTarget(pTileTarget);
                        __result = ExecuteEvent.True;
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.start))]
        private static class ProjStart
        {
            private static void Prefix(Vector3 pLaunchPosition, ref Vector3 pTargetPosition)
            {
                if (!WrapX && !WrapY) return;
                Vector2 n = Nearest(pTargetPosition, pLaunchPosition);
                pTargetPosition.x = n.x;
                pTargetPosition.y = n.y;
            }
        }

        [HarmonyPatch(typeof(MoveCamera), "cameraToBounds")]
        private static class CameraBounds
        {
            private static bool Prefix(MoveCamera __instance)
            {
                if (!WrapX && !WrapY) return true;
                Vector3 old = __instance.transform.position;
                Vector3 p = old;
                if (WrapX) p.x = WrapF(p.x, MapBox.width);
                else p.x = Mathf.Clamp(p.x, 0f, MapBox.width);
                if (WrapY) p.y = WrapF(p.y, MapBox.height);
                else p.y = Mathf.Clamp(p.y, 0f, MapBox.height);
                p.z = -0.5f;
                __instance.transform.position = p;
                Vector3 delta = p - old;
                delta.z = 0f;
                if (delta.sqrMagnitude > 0.001f)
                {
                    __instance._origin += delta;
                    __instance._first_touch += delta;
                }
                World.world.nameplate_manager.update();
                return false;
            }
        }

        [HarmonyPatch(typeof(ZoneCamera), nameof(ZoneCamera.update))]
        private static class ZoneVis
        {
            private static void Postfix(ZoneCamera __instance)
            {
                if (!WrapX && !WrapY) return;
                try
                {
                    Mirror.AddVisibleZones(__instance);
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(MapBox), nameof(MapBox.generateNewMap))]
        private static class GenFlag
        {
            private static void Prefix()
            {
                _generating = true;
            }
        }

        [HarmonyPatch(typeof(MapBox), nameof(MapBox.setMapSize))]
        private static class Size
        {
            private static void Prefix(ref int pWidth, ref int pHeight)
            {
                if (!_generating) return;
                _generating = false;
                if (CivMod.C == null || !CivMod.C.CustomSize) return;
                pWidth = Math.Max(1, Math.Min(40, CivMod.C.SizeX));
                pHeight = Math.Max(1, Math.Min(40, CivMod.C.SizeY));
            }

            private static void Postfix()
            {
                Invalidate();
            }
        }

        [HarmonyPatch(typeof(MapBox), "finishingUpLoading")]
        private static class Loaded
        {
            private static void Postfix()
            {
                Invalidate();
                CivMod.Safe("globe-load", Check);
            }
        }
    }

    internal class Mirror : MonoBehaviour
    {
        private static Mirror _inst;
        private static readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private static int _used;
        private static GameObject _poolRoot;
        private static readonly Dictionary<SpriteRenderer, Strips> _sheets = new Dictionary<SpriteRenderer, Strips>();
        private static readonly List<SpriteRenderer> _deadSheets = new List<SpriteRenderer>();
        private static readonly List<Vector2> _offsets = new List<Vector2>(8);
        private static float _scan;
        private static float _extraScan;
        public static readonly List<SpriteRenderer> Extra = new List<SpriteRenderer>();

        private class Strips
        {
            public Sprite Source;
            public readonly List<SpriteRenderer> Parts = new List<SpriteRenderer>();
            public readonly List<Sprite> Sprites = new List<Sprite>();
        }

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("CivMirror");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<Mirror>();
        }

        public static bool Active
        {
            get { return Globe.WrapX || Globe.WrapY; }
        }

        private void LateUpdate()
        {
            _used = 0;
            try
            {
                _extraScan -= Time.unscaledDeltaTime;
                if (_extraScan <= 0f)
                {
                    _extraScan = 1f;
                    PruneExtras();
                }
                if (G.Loaded && Active)
                {
                    AllowZoom();
                    if (TooFar())
                    {
                        HideSheets();
                    }
                    else
                    {
                        HideBorder(true);
                        Sheets();
                        Quantum();
                    }
                }
                else
                {
                    HideSheets();
                }
            }
            catch (Exception e)
            {
                CivMod.Log("mirror: " + e.Message);
            }
            for (int i = _used; i < _pool.Count; i++)
            {
                if (_pool[i] != null && _pool[i].enabled) _pool[i].enabled = false;
            }
        }

        private static bool NearEdge()
        {
            Camera cam = World.world != null ? World.world.camera : null;
            if (cam == null) return true;
            float hh = cam.orthographicSize + 2f;
            float hw = hh * cam.aspect + 2f;
            Vector3 p = cam.transform.position;
            if (Globe.WrapX && (p.x - hw < 0f || p.x + hw > MapBox.width)) return true;
            if (Globe.WrapY && (p.y - hh < 0f || p.y + hh > MapBox.height)) return true;
            return false;
        }

        private static bool TooFar()
        {
            Camera cam = World.world != null ? World.world.camera : null;
            if (cam == null) return false;
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            return Globe.WrapX && w > MapBox.width || Globe.WrapY && h > MapBox.height;
        }

        private static void AllowZoom()
        {
            MoveCamera mc = MoveCamera.instance;
            if (mc == null) return;
            float max = (int)(Mathf.Max(MapBox.width, MapBox.height) * 1.1f);
            if (mc.orthographic_size_max < max) mc.orthographic_size_max = max;
        }

        private static void HideBorder(bool hide)
        {
            MapBox w = World.world;
            if (w == null || w._map_border == null) return;
            SpriteRenderer r = w._map_border.GetComponent<SpriteRenderer>();
            if (r != null && r.enabled == hide) r.enabled = !hide;
        }

        private static void PruneExtras()
        {
            int count = Extra.Count;
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer r = Extra[i];
                if (r == null) continue;
                if (write != i) Extra[write] = r;
                write++;
            }
            if (write < count) Extra.RemoveRange(write, count - write);
        }

        private static List<Vector2> Offsets()
        {
            _offsets.Clear();
            float w = MapBox.width, h = MapBox.height;
            if (Globe.WrapX)
            {
                _offsets.Add(new Vector2(w, 0f));
                _offsets.Add(new Vector2(-w, 0f));
            }
            if (Globe.WrapY)
            {
                _offsets.Add(new Vector2(0f, h));
                _offsets.Add(new Vector2(0f, -h));
            }
            if (Globe.WrapX && Globe.WrapY)
            {
                _offsets.Add(new Vector2(w, h));
                _offsets.Add(new Vector2(-w, h));
                _offsets.Add(new Vector2(w, -h));
                _offsets.Add(new Vector2(-w, -h));
            }
            return _offsets;
        }

        private static Rect View(float margin)
        {
            Camera cam = World.world.camera;
            float hh = cam.orthographicSize;
            float hw = hh * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Rect(c.x - hw - margin, c.y - hh - margin, (hw + margin) * 2f, (hh + margin) * 2f);
        }

        private static void Quantum()
        {
            Rect view = View(6f);
            float w = MapBox.width, h = MapBox.height;
            bool crosses = view.xMin < 0f || view.xMax > w || view.yMin < 0f || view.yMax > h;
            if (!crosses) return;
            List<Vector2> offs = Offsets();
            if (CivMod.C == null || !CivMod.C.PerfMode)
            {
                if (--_candScan <= 0 || _candView.width <= 0f || Mathf.Abs(_candView.center.x - view.center.x) > 8f || Mathf.Abs(_candView.center.y - view.center.y) > 8f || Mathf.Abs(_candView.width - view.width) > 8f)
                {
                    _candScan = 12;
                    _candView = view;
                    RebuildCandidates(View(16f), offs);
                }
                for (int i = 0; i < _cand.Count; i++)
                {
                    SpriteRenderer r = _cand[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.sprite == null) continue;
                    Copy(r, view, offs);
                }
            }
            for (int i = Extra.Count - 1; i >= 0; i--)
            {
                SpriteRenderer r = Extra[i];
                if (r == null)
                {
                    continue;
                }
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.sprite == null) continue;
                Copy(r, view, offs);
            }
        }

        private static readonly List<SpriteRenderer> _cand = new List<SpriteRenderer>(512);
        private static int _candScan;
        private static Rect _candView;

        private static void RebuildCandidates(Rect wide, List<Vector2> offs)
        {
            _cand.Clear();
            foreach (QuantumSpriteAsset a in AssetManager.quantum_sprites.list)
            {
                QuantumSpriteGroupSystem gs = a.group_system;
                if (gs == null) continue;
                QuantumSprite[] all = gs.getAll();
                int n = Math.Min(all.Length, gs._active_index);
                for (int i = 0; i < n; i++)
                {
                    QuantumSprite q = all[i];
                    if (q == null || !q.has_sprite_renderer) continue;
                    SpriteRenderer r = q.sprite_renderer;
                    if (r == null || !r.enabled || r.sprite == null) continue;
                    Vector3 p = r.transform.position;
                    for (int k = 0; k < offs.Count; k++)
                    {
                        float x = p.x + offs[k].x, y = p.y + offs[k].y;
                        if (x < wide.xMin || x > wide.xMax || y < wide.yMin || y > wide.yMax) continue;
                        _cand.Add(r);
                        break;
                    }
                }
            }
        }

        private static void Copy(SpriteRenderer r, Rect view, List<Vector2> offs)
        {
            Transform t = r.transform;
            Vector3 p = t.position;
            for (int k = 0; k < offs.Count; k++)
            {
                float x = p.x + offs[k].x, y = p.y + offs[k].y;
                if (x < view.xMin || x > view.xMax || y < view.yMin || y > view.yMax) continue;
                SpriteRenderer m = Next();
                Transform mt = m.transform;
                mt.position = new Vector3(x, y, p.z);
                mt.rotation = t.rotation;
                mt.localScale = t.lossyScale;
                m.sprite = r.sprite;
                m.color = r.color;
                m.flipX = r.flipX;
                m.flipY = r.flipY;
                if (m.sharedMaterial != r.sharedMaterial) m.sharedMaterial = r.sharedMaterial;
                m.sortingLayerID = r.sortingLayerID;
                m.sortingOrder = r.sortingOrder;
                m.drawMode = r.drawMode;
                if (r.drawMode != SpriteDrawMode.Simple) m.size = r.size;
                m.enabled = true;
            }
        }

        private static SpriteRenderer Next()
        {
            if (_poolRoot == null)
            {
                _poolRoot = new GameObject("CivMirrorSprites");
                DontDestroyOnLoad(_poolRoot);
                _pool.Clear();
            }
            if (_used < _pool.Count && _pool[_used] != null) return _pool[_used++];
            var go = new GameObject("m");
            go.transform.SetParent(_poolRoot.transform, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            if (_used < _pool.Count) _pool[_used] = r;
            else _pool.Add(r);
            _used++;
            return r;
        }

        private static void Sheets()
        {
            _scan -= Time.unscaledDeltaTime;
            if (_scan <= 0f)
            {
                _scan = 15f;
                foreach (MapLayer l in UnityEngine.Object.FindObjectsOfType<MapLayer>())
                {
                    if (l != null && l.sprRnd != null && !_sheets.ContainsKey(l.sprRnd)) _sheets[l.sprRnd] = new Strips();
                }
                SpriteRenderer dn = DayNight.Renderer;
                if (dn != null && !_sheets.ContainsKey(dn)) _sheets[dn] = new Strips();
            }
            _deadSheets.Clear();
            bool edge = NearEdge();
            foreach (var kv in _sheets)
            {
                if (kv.Key == null)
                {
                    _deadSheets.Add(kv.Key);
                    continue;
                }
                if (!edge)
                {
                    foreach (SpriteRenderer p in kv.Value.Parts) if (p != null && p.enabled) p.enabled = false;
                    continue;
                }
                Sync(kv.Key, kv.Value);
            }
            foreach (SpriteRenderer d in _deadSheets)
            {
                ClearParts(_sheets[d]);
                _sheets.Remove(d);
            }
            _deadSheets.Clear();
        }

        private static void HideSheets()
        {
            HideBorder(false);
            foreach (var kv in _sheets)
            {
                foreach (SpriteRenderer p in kv.Value.Parts) if (p != null && p.enabled) p.enabled = false;
            }
        }

        private static void ClearParts(Strips st)
        {
            foreach (SpriteRenderer p in st.Parts) if (p != null) Destroy(p.gameObject);
            foreach (Sprite s in st.Sprites) if (s != null) Destroy(s);
            st.Parts.Clear();
            st.Sprites.Clear();
            st.Source = null;
        }

        private static void Build(SpriteRenderer src, Strips st)
        {
            ClearParts(st);
            st.Source = src.sprite;
            Sprite s = src.sprite;
            if (s == null || s.texture == null) return;
            Rect full = s.rect;
            float ppu = s.pixelsPerUnit;
            Vector2 pivot = s.pivot;
            Vector3 ls = src.transform.lossyScale;
            float sx = Mathf.Abs(ls.x) < 0.0001f ? 1f : ls.x;
            float sy = Mathf.Abs(ls.y) < 0.0001f ? 1f : ls.y;
            float hw = Mathf.Floor(full.width / 2f), hh = Mathf.Floor(full.height / 2f);
            var pieces = new List<KeyValuePair<Rect, Vector2>>();
            float W = MapBox.width, H = MapBox.height;
            var xs = new List<KeyValuePair<Rect, float>>();
            var ys = new List<KeyValuePair<Rect, float>>();
            if (Globe.WrapX)
            {
                if (hw >= 1f)
                {
                    xs.Add(new KeyValuePair<Rect, float>(new Rect(full.x, 0, hw, 0), W));
                    xs.Add(new KeyValuePair<Rect, float>(new Rect(full.x + hw, 0, full.width - hw, 0), -W));
                }
                else
                {
                    xs.Add(new KeyValuePair<Rect, float>(new Rect(full.x, 0, full.width, 0), W));
                    xs.Add(new KeyValuePair<Rect, float>(new Rect(full.x, 0, full.width, 0), -W));
                }
            }
            if (Globe.WrapY)
            {
                if (hh >= 1f)
                {
                    ys.Add(new KeyValuePair<Rect, float>(new Rect(0, full.y, 0, hh), H));
                    ys.Add(new KeyValuePair<Rect, float>(new Rect(0, full.y + hh, 0, full.height - hh), -H));
                }
                else
                {
                    ys.Add(new KeyValuePair<Rect, float>(new Rect(0, full.y, 0, full.height), H));
                    ys.Add(new KeyValuePair<Rect, float>(new Rect(0, full.y, 0, full.height), -H));
                }
            }
            foreach (var x in xs) pieces.Add(new KeyValuePair<Rect, Vector2>(new Rect(x.Key.x, full.y, x.Key.width, full.height), new Vector2(x.Value, 0f)));
            foreach (var y in ys) pieces.Add(new KeyValuePair<Rect, Vector2>(new Rect(full.x, y.Key.y, full.width, y.Key.height), new Vector2(0f, y.Value)));
            foreach (var x in xs)
            {
                foreach (var y in ys) pieces.Add(new KeyValuePair<Rect, Vector2>(new Rect(x.Key.x, y.Key.y, x.Key.width, y.Key.height), new Vector2(x.Value, y.Value)));
            }
            foreach (var pc in pieces)
            {
                if (pc.Key.width < 1f || pc.Key.height < 1f) continue;
                Sprite part = Sprite.Create(s.texture, pc.Key, Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
                st.Sprites.Add(part);
                var go = new GameObject("civ_strip");
                go.transform.SetParent(src.transform, false);
                go.transform.localPosition = new Vector3((pc.Key.x - full.x - pivot.x) / ppu + pc.Value.x / sx, (pc.Key.y - full.y - pivot.y) / ppu + pc.Value.y / sy, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.sprite = part;
                st.Parts.Add(r);
            }
        }

        private static void Sync(SpriteRenderer src, Strips st)
        {
            if (st.Source != src.sprite || st.Parts.Count == 0 || st.Parts.Exists(x => x == null)) Build(src, st);
            bool on = src.enabled && src.gameObject.activeInHierarchy;
            foreach (SpriteRenderer p in st.Parts)
            {
                if (p == null) continue;
                if (p.enabled != on) p.enabled = on;
                if (!on) continue;
                p.color = src.color;
                if (p.sharedMaterial != src.sharedMaterial) p.sharedMaterial = src.sharedMaterial;
                p.sortingLayerID = src.sortingLayerID;
                p.sortingOrder = src.sortingOrder;
            }
        }

        public static void Reset()
        {
            foreach (var kv in _sheets)
            {
                ClearParts(kv.Value);
            }
            _sheets.Clear();
            _deadSheets.Clear();
            _scan = 0f;
            _extraScan = 0f;
        }

        public static void AddVisibleZones(ZoneCamera zc)
        {
            Camera main = World.world.camera;
            if (main == null) return;
            ZoneCalculator calc = World.world.zone_calculator;
            float halfH = main.orthographicSize;
            float halfW = halfH * main.aspect;
            Vector3 cp = main.transform.position;
            float left = cp.x - halfW, right = cp.x + halfW, bottom = cp.y - halfH, top = cp.y + halfH;
            int w = MapBox.width, h = MapBox.height;
            float zw = (float)w / calc.zones_total_x, zh = (float)h / calc.zones_total_y;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(left / zw) - 1);
            int x1 = Mathf.Min(calc.zones_total_x - 1, Mathf.CeilToInt(right / zw) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(bottom / zh) - 1);
            int y1 = Mathf.Min(calc.zones_total_y - 1, Mathf.CeilToInt(top / zh) + 1);
            int wrappedTop = Globe.WrapY && top > h ? Mathf.Min(calc.zones_total_y - 1, Mathf.CeilToInt((top - h) / zh) + 1) : -1;
            int wrappedBottom = Globe.WrapY && bottom < 0f ? Mathf.Max(0, calc.zones_total_y - 1 - Mathf.CeilToInt(-bottom / zh) - 1) : -1;
            AddRows(zc, calc, x0, x1, y0, y1, wrappedTop, wrappedBottom);
            if (Globe.WrapX && right > w) AddRows(zc, calc, 0, Mathf.Min(calc.zones_total_x - 1, Mathf.CeilToInt((right - w) / zw) + 1), y0, y1, wrappedTop, wrappedBottom);
            if (Globe.WrapX && left < 0f) AddRows(zc, calc, Mathf.Max(0, calc.zones_total_x - 1 - Mathf.CeilToInt(-left / zw) - 1), calc.zones_total_x - 1, y0, y1, wrappedTop, wrappedBottom);
        }

        private static void AddRows(ZoneCamera zc, ZoneCalculator calc, int x0, int x1, int y0, int y1, int wrappedTop, int wrappedBottom)
        {
            AddRange(zc, calc, x0, x1, y0, y1);
            if (wrappedTop >= 0) AddRange(zc, calc, x0, x1, 0, wrappedTop);
            if (wrappedBottom >= 0) AddRange(zc, calc, x0, x1, wrappedBottom, calc.zones_total_y - 1);
        }

        private static void AddRange(ZoneCamera zc, ZoneCalculator calc, int x0, int x1, int y0, int y1)
        {
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    TileZone z = calc.getZoneUnsafe(x, y);
                    if (z == null || z.visible) continue;
                    z.visible = true;
                    zc._visible_zones.Add(z);
                    if (zc._set_visible_chunks.Add(z.chunk)) zc._list_visible_chunks.Add(z.chunk);
                }
            }
        }

        [HarmonyPatch(typeof(TilemapExtended), "addToQueueToRedraw")]
        private static class TileMirror
        {
            private static void Prefix(TilemapExtended __instance, out int __state)
            {
                __state = __instance._vec.Count;
            }

            private static void Postfix(TilemapExtended __instance, int __state, Vector3Int pPosition, TileBase pTileGraphics)
            {
                if (!Active || __instance._vec.Count == __state) return;
                int w = MapBox.width, h = MapBox.height;
                pPosition.z = 0;
                int dx = 0, dy = 0;
                if (Globe.WrapX) dx = pPosition.x < w / 2 ? w : -w;
                if (Globe.WrapY) dy = pPosition.y < h / 2 ? h : -h;
                if (dx != 0)
                {
                    __instance._vec.Add(new Vector3Int(pPosition.x + dx, pPosition.y, 0));
                    __instance._tiles.Add(pTileGraphics);
                }
                if (dy != 0)
                {
                    __instance._vec.Add(new Vector3Int(pPosition.x, pPosition.y + dy, 0));
                    __instance._tiles.Add(pTileGraphics);
                }
                if (dx != 0 && dy != 0)
                {
                    __instance._vec.Add(new Vector3Int(pPosition.x + dx, pPosition.y + dy, 0));
                    __instance._tiles.Add(pTileGraphics);
                }
            }
        }
    }
}
