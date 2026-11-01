using System;
using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal static class Waters
    {
        private const int Reach = 2;
        private static readonly Dictionary<TileZone, Kingdom> _owner = new Dictionary<TileZone, Kingdom>();
        private static readonly Dictionary<Kingdom, List<TileZone>> _byKingdom = new Dictionary<Kingdom, List<TileZone>>();
        private static float _timer;
        private static string _sig = "";
        private static GameObject _go;
        private static SpriteRenderer _sr;
        private static Texture2D _tex;
        private static Color32[] _px;
        private static int _w, _h;

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.Waters; }
        }

        public static Kingdom Owner(TileZone z)
        {
            if (z == null) return null;
            if (z.city != null && z.city.kingdom != null) return z.city.kingdom;
            Kingdom k;
            return _owner.TryGetValue(z, out k) ? k : null;
        }

        public static Kingdom Owner(WorldTile t)
        {
            return t != null ? Owner(t.zone) : null;
        }

        public static List<TileZone> Of(Kingdom k)
        {
            List<TileZone> l;
            return k != null && _byKingdom.TryGetValue(k, out l) ? l : new List<TileZone>();
        }

        private static bool Wet(TileZone z)
        {
            int n = 0;
            foreach (WorldTile t in z.tiles) if (t != null && t.Type != null && t.Type.liquid) n++;
            return n >= 16;
        }

        public static void Clear()
        {
            _owner.Clear();
            _byKingdom.Clear();
            _sig = "";
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _go = null;
            _sr = null;
            _tex = null;
        }

        public static void Tick(float dt)
        {
            if (!On || World.world == null || World.world.zone_calculator == null)
            {
                if (_go != null) Clear();
                return;
            }
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = 5f;
            Compute();
        }

        private static void Compute()
        {
            var dist = new Dictionary<TileZone, int>();
            var own = new Dictionary<TileZone, Kingdom>();
            var queue = new Queue<TileZone>();
            List<TileZone> zones = World.world.zone_calculator.zones;
            foreach (TileZone z in zones)
            {
                if (z == null || z.city == null || z.city.kingdom == null || !z.city.kingdom.isCiv()) continue;
                bool coast = false;
                foreach (TileZone n in z.neighbours) if (n != null && n.city == null && Wet(n)) coast = true;
                if (!coast) continue;
                dist[z] = 0;
                own[z] = z.city.kingdom;
                queue.Enqueue(z);
            }
            _owner.Clear();
            while (queue.Count > 0)
            {
                TileZone z = queue.Dequeue();
                int d = dist[z];
                if (d >= Reach) continue;
                Kingdom k = own[z];
                foreach (TileZone n in z.neighbours)
                {
                    if (n == null || n.city != null || dist.ContainsKey(n) || !Wet(n)) continue;
                    dist[n] = d + 1;
                    own[n] = k;
                    _owner[n] = k;
                    queue.Enqueue(n);
                }
            }
            _byKingdom.Clear();
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _owner)
            {
                List<TileZone> l;
                if (!_byKingdom.TryGetValue(kv.Value, out l)) _byKingdom[kv.Value] = l = new List<TileZone>();
                l.Add(kv.Key);
            }
            foreach (var kv in _byKingdom) sb.Append(kv.Key.data.id).Append(':').Append(kv.Value.Count).Append(';');
            string sig = sb.ToString() + MapBox.width;
            if (sig == _sig && _go != null) return;
            _sig = sig;
            Draw();
        }

        private static void Ensure()
        {
            if (_go != null && _w == MapBox.width && _h == MapBox.height) return;
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _w = MapBox.width;
            _h = MapBox.height;
            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Clamp;
            _px = new Color32[_w * _h];
            Sprite s = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, 1f);
            _go = new GameObject("CivWaters");
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _sr = _go.AddComponent<SpriteRenderer>();
            _sr.sprite = s;
            SpriteRenderer zr = World.world.zone_calculator.sprRnd;
            if (zr != null)
            {
                _sr.sortingLayerID = zr.sortingLayerID;
                _sr.sortingOrder = zr.sortingOrder + 1;
                _go.transform.position = new Vector3(0f, 0f, zr.transform.position.z - 0.01f);
            }
            else _go.transform.position = Vector3.zero;
        }

        private static void Draw()
        {
            Ensure();
            Array.Clear(_px, 0, _px.Length);
            foreach (var kv in _owner)
            {
                Color32 c = kv.Value.getColor() != null ? kv.Value.getColor().getColorMain32() : new Color32(255, 255, 255, 255);
                c.a = 200;
                foreach (WorldTile t in kv.Key.tiles)
                {
                    if (t == null || t.Type == null || !t.Type.liquid) continue;
                    bool edge = false;
                    foreach (WorldTile n in t.neighbours)
                    {
                        if (n == null) continue;
                        if (n.zone == kv.Key) continue;
                        Kingdom o = Owner(n);
                        if (o != kv.Value && (n.Type == null || n.Type.liquid)) edge = true;
                    }
                    if (!edge || (t.x + t.y) % 3 == 0) continue;
                    int i = t.y * _w + t.x;
                    if (i >= 0 && i < _px.Length) _px[i] = c;
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }
    }
}
