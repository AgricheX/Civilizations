using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class Fog
    {
        public const int Chunk = 16;
        private const string KMap = "civ_fog_map";
        private const string KKnown = "civ_fog_known";
        private const string KInit = "civ_fog_init";
        private static readonly Dictionary<long, HashSet<long>> _known = new Dictionary<long, HashSet<long>>();
        private static readonly Dictionary<long, byte[]> _maps = new Dictionary<long, byte[]>();
        private static int _cw, _ch;
        public static int Version;

        private static GameObject _go;
        private static SpriteRenderer _sr;
        private static Texture2D _tex;
        private static long _drawnFor = -2;
        private static int _drawnVer = -1;

        public static void Clear()
        {
            _known.Clear();
            _maps.Clear();
            _cw = _ch = 0;
            Version++;
            if (_go != null) _go.SetActive(false);
            _drawnFor = -2;
        }

        private static void Dims()
        {
            int w = Math.Max(1, (MapBox.width + Chunk - 1) / Chunk), h = Math.Max(1, (MapBox.height + Chunk - 1) / Chunk);
            if (w == _cw && h == _ch) return;
            _cw = w;
            _ch = h;
            _maps.Clear();
        }

        private static bool Inited(Kingdom k)
        {
            return Store.Int(k.data, KInit, 0) == 1;
        }

        private static byte[] Map(Kingdom k)
        {
            Dims();
            byte[] m;
            if (_maps.TryGetValue(k.data.id, out m)) return m;
            int n = (_cw * _ch + 7) / 8;
            m = new byte[n];
            string s = Store.Str(k.data, KMap);
            if (s.Length > 0)
            {
                try
                {
                    byte[] r = Convert.FromBase64String(s);
                    if (r.Length == n) m = r;
                }
                catch (Exception)
                {
                }
            }
            _maps[k.data.id] = m;
            return m;
        }

        private static HashSet<long> Known(Kingdom k)
        {
            HashSet<long> s;
            if (_known.TryGetValue(k.data.id, out s)) return s;
            s = new HashSet<long>();
            foreach (string x in Store.Set(k.data, KKnown))
            {
                long id;
                if (long.TryParse(x, out id)) s.Add(id);
            }
            _known[k.data.id] = s;
            return s;
        }

        private static void Persist(Kingdom k)
        {
            byte[] m;
            if (_maps.TryGetValue(k.data.id, out m)) Store.Put(k.data, KMap, Convert.ToBase64String(m));
            HashSet<long> s;
            if (_known.TryGetValue(k.data.id, out s)) Store.Set(k.data, KKnown, s.Select(x => x.ToString()));
        }

        public static bool Knows(Kingdom a, Kingdom b)
        {
            if (a == null || b == null || a == b || a.data == null || b.data == null) return true;
            if (!a.isCiv() || !b.isCiv()) return true;
            if (!Inited(a))
            {
                CivMod.Safe("fog-init", () => Update(a, 1, true));
                if (!Inited(a)) return true;
            }
            return Known(a).Contains(b.data.id);
        }

        public static bool KnowsChunk(Kingdom k, int cx, int cy)
        {
            if (k == null || k.data == null) return true;
            byte[] m = Map(k);
            if (cx < 0 || cy < 0 || cx >= _cw || cy >= _ch) return false;
            int i = cy * _cw + cx;
            return (m[i >> 3] & (1 << (i & 7))) != 0;
        }

        public static bool KnowsCity(Kingdom k, City c)
        {
            WorldTile t = G.Tile(c);
            if (t == null) return true;
            return c.kingdom == k || KnowsChunk(k, t.x / Chunk, t.y / Chunk);
        }

        public static IEnumerable<Kingdom> KnownKingdoms(Kingdom k)
        {
            foreach (long id in Known(k).ToList())
            {
                Kingdom o = World.world.kingdoms.get(id);
                if (G.Ok(o)) yield return o;
            }
        }

        private static int Reveal(byte[] m, int cx, int cy, int r)
        {
            int n = 0;
            for (int y = Math.Max(0, cy - r); y <= Math.Min(_ch - 1, cy + r); y++)
            {
                for (int x = Math.Max(0, cx - r); x <= Math.Min(_cw - 1, cx + r); x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r + r) continue;
                    int i = y * _cw + x;
                    int bit = 1 << (i & 7);
                    if ((m[i >> 3] & bit) != 0) continue;
                    m[i >> 3] = (byte)(m[i >> 3] | bit);
                    n++;
                }
            }
            return n;
        }

        private static void RevealAll(byte[] m)
        {
            for (int i = 0; i < m.Length; i++) m[i] = 255;
        }

        public static int Radius(Kingdom k)
        {
            int era = Research.Era(k);
            int r = 2;
            if (era >= 2) r++;
            r += Math.Min(3, Research.Level(k, "cartography"));
            if (era >= 8) r++;
            if (era >= 10) r++;
            if (Research.Level(k, "ocean_navigation") > 0) r++;
            if (era >= 11 && Aircraft.KingdomBase(k)) r += 3;
            if (Research.Level(k, "radio_navigation") > 0) r++;
            r += Rarities.FogBonus(k);
            return r;
        }

        public static void Meet(Kingdom a, Kingdom b, bool news)
        {
            if (!G.Ok(a) || !G.Ok(b) || a == b || !a.isCiv() || !b.isCiv()) return;
            bool na = Known(a).Add(b.data.id);
            bool nb = Known(b).Add(a.data.id);
            if (na || nb) Version++;
            if (news && (na || nb) && Inited(a) && Inited(b)) Hub.Raise(Hub.FirstContact, a, b, a.capital, null, ContactKind(a, b));
        }

        private static string ContactKind(Kingdom a, Kingdom b)
        {
            KProfile pa = Profile.Get(a), pb = Profile.Get(b);
            if (pa.Xeno == "xenophobia" || pb.Xeno == "xenophobia") return "hostile";
            if (pa.Species != pb.Species && pa.Era < 8) return "wary";
            if (pa.Econ.Length > 0 && Coherence.Is(pa.Econ, "mercantile") || Coherence.Is(pb.Econ, "mercantile")) return "trade";
            return "peaceful";
        }

        public static int Explore(Kingdom k, int trips, float chance = 1f)
        {
            if (!G.Ok(k) || k.capital == null) return 0;
            byte[] m = Map(k);
            WorldTile ct = G.Tile(k.capital);
            if (ct == null) return 0;
            int era = Research.Era(k), added = 0;
            for (int i = 0; i < trips; i++)
            {
                if (!G.Chance(chance)) continue;
                float ang = UnityEngine.Random.value * Mathf.PI * 2f;
                int len = Math.Max(3, 3 + era - 6);
                int cx = ct.x / Chunk, cy = ct.y / Chunk;
                for (int s = 1; s <= len; s++) added += Reveal(m, cx + (int)Math.Round(Math.Cos(ang) * s * 1.5), cy + (int)Math.Round(Math.Sin(ang) * s * 1.5), 1);
            }
            if (added > 0) Version++;
            return added;
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            Update(k, yrs, false);
        }

        private static void Update(Kingdom k, int yrs, bool init)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            bool first = !Inited(k);
            byte[] m = Map(k);
            HashSet<long> known = Known(k);
            int era = Research.Era(k);
            bool all = Research.Level(k, "satellites") > 0 || era >= 14;
            int added = 0;
            if (all) RevealAll(m);
            else
            {
                int r = Radius(k);
                foreach (City c in G.Cities(k))
                {
                    WorldTile t = G.Tile(c);
                    if (t == null) continue;
                    added += Reveal(m, t.x / Chunk, t.y / Chunk, r);
                }
                if (era >= 8 && !first) added += Explore(k, Math.Min(3, yrs) * (era >= 9 ? 2 : 1), 0.35f);
                int extra = Rarities.ExtraTrips(k);
                if (extra > 0 && !first) added += Explore(k, extra * Math.Min(3, yrs), 0.5f);
            }
            var newly = new List<Kingdom>();
            foreach (Kingdom o in G.Kingdoms())
            {
                if (o == k || known.Contains(o.data.id)) continue;
                bool meet = false;
                foreach (City c in G.Cities(o))
                {
                    WorldTile t = G.Tile(c);
                    if (t != null && KnowsChunk(k, t.x / Chunk, t.y / Chunk))
                    {
                        meet = true;
                        break;
                    }
                }
                if (!meet && Internet.Exists(k) && Internet.Exists(o)) meet = true;
                if (!meet && (World.world.wars.isInWarWith(k, o) || Diplomacy.Between(k, o).Count > 0 || Diplomacy.Overlord(o) == k || Diplomacy.Overlord(k) == o)) meet = true;
                if (meet) newly.Add(o);
            }
            foreach (Kingdom o in Economy.TradePartners(k)) if (G.Ok(o) && o != k && !known.Contains(o.data.id)) newly.Add(o);
            foreach (City c in G.Cities(k))
            {
                if (c.neighbours_kingdoms == null) continue;
                foreach (Kingdom o in c.neighbours_kingdoms) if (G.Ok(o) && o != k && !known.Contains(o.data.id)) newly.Add(o);
            }
            Store.Put(k.data, KInit, 1);
            foreach (Kingdom o in newly.Distinct()) Meet(k, o, !first && !init);
            if (added > 0 || newly.Count > 0) Version++;
            Persist(k);
        }

        public static void Register()
        {
            Hub.On(Hub.WarStarted, e => Meet(e.K, e.O, false));
            Hub.On(Hub.TreatySigned, e => Meet(e.K, e.O, false));
            Hub.On(Hub.KingdomFounded, e =>
            {
                if (!G.Ok(e.K)) return;
                Kingdom parent = e.O;
                if (!G.Ok(parent) && e.C != null) parent = e.C.kingdom;
                if (!G.Ok(parent) || parent == e.K || !Inited(parent)) return;
                byte[] pm = Map(parent), m = Map(e.K);
                Array.Copy(pm, m, Math.Min(pm.Length, m.Length));
                foreach (long id in Known(parent)) Known(e.K).Add(id);
                Known(e.K).Add(parent.data.id);
                Known(parent).Add(e.K.data.id);
                Store.Put(e.K.data, KInit, 1);
                Persist(e.K);
            });
            Hub.On(Hub.CityCaptured, e =>
            {
                if (G.Ok(e.K) && G.Ok(e.O)) Meet(e.K, e.O, false);
            });
            Hub.On(Hub.KingdomGone, e =>
            {
                if (e.K == null || e.K.data == null) return;
                _known.Remove(e.K.data.id);
                _maps.Remove(e.K.data.id);
                foreach (HashSet<long> s in _known.Values) s.Remove(e.K.data.id);
            });
        }

        public static void Save()
        {
            foreach (Kingdom k in G.Kingdoms()) if (_maps.ContainsKey(k.data.id) || _known.ContainsKey(k.data.id)) Persist(k);
        }

        public static void Frame()
        {
            bool ctrl = Player.Active;
            bool on = ctrl || CivMod.C != null && CivMod.C.FogView;
            Kingdom k = ctrl ? Player.K : on ? SelectedMetas.selected_kingdom : null;
            if (!G.Ok(k) || !k.isCiv())
            {
                if (_go != null && _go.activeSelf) _go.SetActive(false);
                _drawnFor = -2;
                return;
            }
            if (_drawnFor == k.data.id && _drawnVer == Version && _go != null && _go.activeSelf) return;
            Draw(k);
        }

        private static void Draw(Kingdom k)
        {
            Dims();
            if (_tex == null || _tex.width != _cw || _tex.height != _ch || _go == null)
            {
                if (_go != null) UnityEngine.Object.Destroy(_go);
                _tex = new Texture2D(_cw, _ch, TextureFormat.RGBA32, false);
                _tex.filterMode = FilterMode.Bilinear;
                _tex.wrapMode = TextureWrapMode.Clamp;
                float ppu = 1f / Chunk;
                _go = new GameObject("CivFog");
                UnityEngine.Object.DontDestroyOnLoad(_go);
                _sr = _go.AddComponent<SpriteRenderer>();
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _cw, _ch), new Vector2(0.5f, 0.5f), ppu);
                SpriteRenderer night = null;
                if (WorldAgeEffects.instance != null) WorldAgeEffects.instance.dict_effects.TryGetValue("night", out night);
                if (night != null)
                {
                    _sr.sortingLayerID = night.sortingLayerID;
                    _sr.sortingOrder = night.sortingOrder + 1;
                }
                else _sr.sortingOrder = 5001;
                _go.transform.position = new Vector3(_cw * Chunk / 2f, _ch * Chunk / 2f, -6f);
            }
            var px = new Color32[_cw * _ch];
            Color32 dark = new Color32(14, 16, 26, 205), clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < _ch; y++) for (int x = 0; x < _cw; x++) px[y * _cw + x] = KnowsChunk(k, x, y) ? clear : dark;
            _tex.SetPixels32(px);
            _tex.Apply(false);
            _go.SetActive(true);
            _drawnFor = k.data.id;
            _drawnVer = Version;
        }
    }
}
