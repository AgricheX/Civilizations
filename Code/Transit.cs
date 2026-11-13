using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal abstract class Mover
    {
        public GameObject Go;
        public bool Dead;
        public abstract void Step(float dt);

        public virtual void Kill()
        {
            Dead = true;
            if (Go != null) UnityEngine.Object.Destroy(Go);
        }
    }

    internal class TrainRun : Mover
    {
        public RailLink Link;
        public bool Forward;
        public float T;
        public float Speed;
        public List<SpriteRenderer> Parts = new List<SpriteRenderer>();
        public City From;
        public City To;
        public string Cargo;
        public int Amount;
        public int Passengers;

        public override void Kill()
        {
            foreach (SpriteRenderer part in Parts)
            {
                if (part != null) UnityEngine.Object.Destroy(part.gameObject);
            }
            Parts.Clear();
            base.Kill();
        }

        public override void Step(float dt)
        {
            List<WorldTile> path = Rails.TilesOf(Link);
            if (!Link.Done || path.Count < 2 || From == null || !From.isAlive() || To == null || !To.isAlive())
            {
                Kill();
                return;
            }
            T += Speed * dt;
            float len = path.Count - 1;
            if (T >= len)
            {
                Transit.Arrive(this);
                Kill();
                return;
            }
            for (int i = 0; i < Parts.Count; i++)
            {
                float s = T - i * 1.6f;
                if (s < 0f)
                {
                    Parts[i].enabled = false;
                    continue;
                }
                Parts[i].enabled = true;
                Vector2 p, dir;
                Transit.Along(path, Forward ? s : len - s, out p, out dir);
                if (!Forward) dir = -dir;
                Parts[i].transform.position = new Vector3(p.x + 0.5f, p.y + 0.5f, 0f);
                Transit.FaceGround(Parts[i], dir);
            }
        }
    }

    internal class Flight : Mover
    {
        public Vector2 A;
        public Vector2 B;
        public float T;
        public float Duration;
        public float Height;
        public SpriteRenderer Body;
        public SpriteRenderer Shadow;
        public Action Done;
        public bool Arc;
        public bool Smoke;
        public bool Upright;
        private float _smoke;

        public override void Kill()
        {
            if (Shadow != null) UnityEngine.Object.Destroy(Shadow.gameObject);
            Shadow = null;
            base.Kill();
        }

        public override void Step(float dt)
        {
            T += dt / Math.Max(0.1f, Duration);
            if (T >= 1f)
            {
                if (Done != null)
                {
                    try
                    {
                        Done();
                    }
                    catch (Exception e)
                    {
                        CivMod.Log("flight: " + e.Message);
                    }
                }
                Kill();
                return;
            }
            Vector2 p = Vector2.Lerp(A, B, T);
            float h = Arc ? Mathf.Sin(T * Mathf.PI) * Height : Height * Mathf.Clamp01(Math.Min(T, 1f - T) * 8f);
            Vector2 dir = (B - A).normalized;
            Vector2 wp = Globe.Wrap(p);
            Body.transform.position = new Vector3(wp.x, wp.y + h, -0.5f);
            float ang = Mathf.Atan2(dir.y + (Arc ? Mathf.Cos(T * Mathf.PI) * Height / Math.Max(1f, (B - A).magnitude) * 2f : 0f), dir.x) * Mathf.Rad2Deg;
            if (Upright) Transit.FaceGround(Body, dir);
            else Body.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            if (Shadow != null)
            {
                Shadow.transform.position = new Vector3(wp.x, wp.y, 0f);
                Shadow.transform.rotation = Body.transform.rotation;
                Shadow.flipX = Body.flipX;
                Shadow.flipY = Body.flipY;
            }
            if (Smoke)
            {
                _smoke -= dt;
                if (_smoke <= 0f)
                {
                    _smoke = 0.08f;
                    Transit.Puff(new Vector2(wp.x, wp.y + h), 0.8f);
                }
            }
        }
    }

    internal class Launch : Mover
    {
        public Vector2 Pos;
        public float T;
        public SpriteRenderer Body;
        public Action Done;
        private float _smoke;

        public override void Step(float dt)
        {
            T += dt;
            float h = T * T * 6f;
            Body.transform.position = new Vector3(Pos.x + 0.5f, Pos.y + 1f + h, -0.5f);
            _smoke -= dt;
            if (_smoke <= 0f)
            {
                _smoke = 0.05f;
                Transit.Puff(new Vector2(Pos.x + 0.5f + UnityEngine.Random.Range(-0.3f, 0.3f), Pos.y + 0.6f + h), 1.2f);
            }
            if (h > 40f)
            {
                if (Done != null) Done();
                Kill();
            }
        }
    }

    internal class Puff : Mover
    {
        public SpriteRenderer Body;
        public float Life;
        public float Max;
        public float Scale;

        public override void Step(float dt)
        {
            Life += dt;
            if (Life >= Max)
            {
                Kill();
                return;
            }
            float k = Life / Max;
            Body.transform.position += new Vector3(0f, dt * 0.6f, 0f);
            Body.transform.localScale = Vector3.one * Scale * (0.15f + k * 0.25f);
            Color c = Body.color;
            c.a = 0.8f * (1f - k);
            Body.color = c;
        }
    }

    internal class Orbiter : Mover
    {
        public SpriteRenderer Body;
        public float Y;
        public float X;
        public float Speed;
        public string Kingdom;

        public override void Step(float dt)
        {
            X += Speed * dt;
            if (X > MapBox.width) X -= MapBox.width;
            Body.transform.position = new Vector3(X, Y, -0.6f);
            Color c = Body.color;
            c.a = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f + Y);
            Body.color = c;
        }
    }

    internal static class Transit
    {
        private static GameObject _root;
        private static GameObject _lines;
        private static readonly List<Mover> _movers = new List<Mover>();
        private static int _railVersion = -1;
        private static float _railCheck;
        private static float _schedule;
        private static float _powerCheck;
        private static Material _mat;
        private static int _layer;
        private static int _order;
        private static bool _layerReady;
        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, int> _orbiters = new Dictionary<string, int>();

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.TransitVisuals; }
        }

        public static void Clear()
        {
            foreach (Mover m in _movers) m.Kill();
            _movers.Clear();
            _orbiters.Clear();
            if (_lines != null) UnityEngine.Object.Destroy(_lines);
            _lines = null;
            _railVersion = -1;
        }

        private static void Ensure()
        {
            if (_root == null)
            {
                _root = new GameObject("CivTransit");
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }
            if (_mat == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                _mat = sh != null ? new Material(sh) : null;
            }
            if (!_layerReady)
            {
                SpriteRenderer night = null;
                if (WorldAgeEffects.instance != null) WorldAgeEffects.instance.dict_effects.TryGetValue("night", out night);
                if (night != null)
                {
                    _layer = night.sortingLayerID;
                    _order = night.sortingOrder;
                    _layerReady = true;
                }
            }
        }

        public static Sprite Sprite(string id)
        {
            Sprite s;
            if (_sprites.TryGetValue(id, out s)) return s;
            s = SpriteTextureLoader.getSprite("civ/transit/" + id);
            _sprites[id] = s;
            return s;
        }

        private static SpriteRenderer Make(string sprite, int orderOffset, float scale)
        {
            Ensure();
            var go = new GameObject("civ_" + sprite);
            go.transform.SetParent(_root.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite(sprite);
            if (_mat != null) sr.sharedMaterial = _mat;
            sr.sortingLayerID = _layer;
            sr.sortingOrder = _order + orderOffset;
            go.transform.localScale = Vector3.one * scale;
            Mirror.Extra.Add(sr);
            return sr;
        }

        public static void Along(List<WorldTile> path, float s, out Vector2 pos, out Vector2 dir)
        {
            int i = Mathf.Clamp((int)s, 0, path.Count - 2);
            float f = Mathf.Clamp01(s - i);
            WorldTile a = path[i], b = path[i + 1];
            Vector2 pa = new Vector2(a.x, a.y);
            Vector2 pb = Globe.Nearest(new Vector2(b.x, b.y), pa);
            pos = Globe.Wrap(Vector2.Lerp(pa, pb, f));
            dir = (pb - pa).sqrMagnitude > 0f ? (pb - pa).normalized : Vector2.right;
        }

        public static void FaceGround(SpriteRenderer body, Vector2 direction)
        {
            body.flipX = direction.x < 0f;
            body.flipY = false;
            body.transform.rotation = Quaternion.Euler(0f, 0f, GroundFacing.Angle(direction.x, direction.y));
        }

        public static void Frame()
        {
            if (!G.Loaded)
            {
                if (_movers.Count > 0 || _lines != null) Clear();
                return;
            }
            if (!On) return;
            Ensure();
            float dt = World.world.isPaused() ? 0f : World.world.elapsed;
            for (int i = _movers.Count - 1; i >= 0; i--)
            {
                Mover m = _movers[i];
                if (m.Dead)
                {
                    _movers.RemoveAt(i);
                    continue;
                }
                try
                {
                    m.Step(dt);
                }
                catch (Exception)
                {
                    m.Kill();
                }
            }
            _railCheck -= Time.unscaledDeltaTime;
            if (_railCheck <= 0f)
            {
                _railCheck = 2f;
                if (_railVersion != Rails.Version) DrawRails();
            }
            _powerCheck -= Time.unscaledDeltaTime;
            if (_powerCheck <= 0f)
            {
                _powerCheck = 10f;
                DrawPower();
                SyncOrbiters();
            }
            if (dt <= 0f) return;
            _schedule -= dt;
            if (_schedule <= 0f)
            {
                _schedule = 3f;
                Schedule();
            }
        }

        private static LineRenderer Line(Transform parent, string name, Color color, float width, int orderOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            if (_mat != null) lr.sharedMaterial = _mat;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 0;
            lr.useWorldSpace = true;
            lr.sortingLayerID = _layer;
            lr.sortingOrder = _order + orderOffset;
            return lr;
        }

        private static void DrawRails()
        {
            _railVersion = Rails.Version;
            if (_lines != null)
            {
                Transform old = _lines.transform.Find("rails");
                if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            }
            else
            {
                _lines = new GameObject("CivLines");
                _lines.transform.SetParent(_root.transform, false);
            }
            var rails = new GameObject("rails");
            rails.transform.SetParent(_lines.transform, false);
            foreach (RailLink l in Rails.Links)
            {
                List<WorldTile> tiles = Rails.TilesOf(l);
                int n = Math.Min(l.Built, tiles.Count);
                if (n < 2) continue;
                var seg = new List<Vector3>();
                Action flush = () =>
                {
                    if (seg.Count >= 2)
                    {
                        Vector3[] points = seg.ToArray();
                        LineRenderer bed = Line(rails.transform, "bed", new Color(0.33f, 0.27f, 0.22f, 0.95f), 0.9f, -40);
                        bed.positionCount = seg.Count;
                        bed.SetPositions(points);
                        LineRenderer track = Line(rails.transform, "track", RailColor(l.Kind), 0.35f, -39);
                        track.positionCount = seg.Count;
                        track.SetPositions(points);
                    }
                    seg.Clear();
                };
                for (int i = 0; i < n; i++)
                {
                    WorldTile t = tiles[i];
                    if (seg.Count > 0)
                    {
                        Vector3 last = seg[seg.Count - 1];
                        if (Math.Abs(t.x + 0.5f - last.x) > 2f || Math.Abs(t.y + 0.5f - last.y) > 2f) flush();
                    }
                    seg.Add(new Vector3(t.x + 0.5f, t.y + 0.5f, 0f));
                }
                flush();
            }
        }

        private static Color RailColor(string kind)
        {
            switch (kind)
            {
                case "maglev": return new Color(0.75f, 0.95f, 1f, 1f);
                case "bullet": return new Color(0.85f, 0.85f, 0.9f, 1f);
                case "monorail": return new Color(0.95f, 0.95f, 0.95f, 1f);
                case "gyro": return new Color(0.8f, 0.6f, 0.3f, 1f);
                default: return new Color(0.55f, 0.55f, 0.6f, 1f);
            }
        }

        private static WorldTile PowerTile(City c, out bool generator)
        {
            generator = false;
            Building hall = null;
            foreach (Building b in c.buildings)
            {
                if (b == null || b.asset == null || !b.isUsable() || b.isUnderConstruction() || b.current_tile == null) continue;
                if (Energy.PlantOf(b) != null)
                {
                    generator = true;
                    return b.current_tile;
                }
                if (b.asset.type == "type_hall" && (hall == null || b.asset.upgrade_level > hall.asset.upgrade_level)) hall = b;
            }
            return hall != null ? hall.current_tile : null;
        }

        private static long _powerSig = long.MinValue;

        private static void DrawPower()
        {
            var segs = new List<KeyValuePair<WorldTile, WorldTile>>();
            long sig = 17;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (CivMod.C != null && !CivMod.C.EnergyOn || !Energy.Grid(k)) continue;
                var cities = new List<City>();
                var nodes = new Dictionary<City, WorldTile>();
                bool hasGenerator = false;
                foreach (City c in G.Cities(k))
                {
                    bool generator;
                    WorldTile tile = PowerTile(c, out generator);
                    if (tile == null) continue;
                    cities.Add(c);
                    nodes[c] = tile;
                    hasGenerator |= generator;
                }
                if (!hasGenerator || cities.Count < 2) continue;
                var inTree = new List<City> { cities[0] };
                var rest = new List<City>(cities);
                rest.RemoveAt(0);
                while (rest.Count > 0)
                {
                    City ba = null, bb = null;
                    float bd = float.MaxValue;
                    foreach (City a in inTree)
                    {
                        foreach (City b in rest)
                        {
                            float d = G.Dist(nodes[a], nodes[b]);
                            if (d < bd)
                            {
                                bd = d;
                                ba = a;
                                bb = b;
                            }
                        }
                    }
                    if (bb == null) break;
                    rest.Remove(bb);
                    inTree.Add(bb);
                    if (bd > 120f) continue;
                    WorldTile ta = nodes[ba], tb = nodes[bb];
                    segs.Add(new KeyValuePair<WorldTile, WorldTile>(ta, tb));
                    sig = sig * 31 + ta.x * 7919 + ta.y;
                    sig = sig * 31 + tb.x * 7919 + tb.y;
                }
            }
            if (_lines != null && sig == _powerSig && _lines.transform.Find("power") != null) return;
            _powerSig = sig;
            if (_lines == null)
            {
                _lines = new GameObject("CivLines");
                _lines.transform.SetParent(_root.transform, false);
            }
            Transform old = _lines.transform.Find("power");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            var power = new GameObject("power");
            power.transform.SetParent(_lines.transform, false);
            foreach (var seg in segs)
            {
                WorldTile ta = seg.Key, tb = seg.Value;
                Vector2 a2 = new Vector2(ta.x + 0.5f, ta.y + 1.5f);
                Vector2 b2 = Globe.Nearest(new Vector2(tb.x + 0.5f, tb.y + 1.5f), a2);
                float bd = Vector2.Distance(a2, b2);
                int steps = Math.Max(2, (int)(bd / 6f));
                var pts = new List<Vector3>();
                for (int i = 0; i <= steps; i++)
                {
                    Vector2 p = Vector2.Lerp(a2, b2, i / (float)steps);
                    pts.Add(new Vector3(p.x, p.y, 0f));
                    Vector2 w = Globe.Wrap(p);
                    SpriteRenderer pylon = Make("pylon", -30, 0.25f);
                    pylon.transform.SetParent(power.transform, true);
                    pylon.transform.position = new Vector3(w.x, w.y - 1f, 0f);
                }
                bool crosses = Math.Abs(b2.x - (tb.x + 0.5f)) > 1f;
                if (!crosses)
                {
                    LineRenderer lr = Line(power.transform, "wire", new Color(0.12f, 0.12f, 0.14f, 0.8f), 0.08f, -29);
                    lr.positionCount = pts.Count;
                    lr.SetPositions(pts.ToArray());
                }
            }
        }

        private static void Schedule()
        {
            foreach (RailLink l in Rails.Links)
            {
                if (!l.Done || !G.Chance(0.12f)) continue;
                City a = G.CityById(l.A), b = G.CityById(l.B);
                if (a == null || b == null || !Rails.HasStation(a) || !Rails.HasStation(b)) continue;
                if (World.world.wars.isInWarWith(a.kingdom, b.kingdom)) continue;
                if (CountTrains(l) >= 2) continue;
                bool fwd = G.Chance(0.5f);
                SpawnTrain(l, fwd ? a : b, fwd ? b : a, fwd);
            }
            foreach (City c in G.Cities())
            {
                if (c.kingdom == null || !c.kingdom.isCiv()) continue;
                if (c.countBuildingsType("type_civ_airport") > 0 && G.Chance(0.08f)) SpawnAirline(c);
                if (Research.Has(c.kingdom, "trams") && c.getPopulationPeople() > 60 && G.Chance(0.04f)) SpawnTram(c);
            }
        }

        private static int CountTrains(RailLink l)
        {
            int n = 0;
            foreach (Mover m in _movers)
            {
                TrainRun t = m as TrainRun;
                if (t != null && !t.Dead && t.Link == l) n++;
            }
            return n;
        }

        private static void SpawnTrain(RailLink l, City from, City to, bool forward)
        {
            TrainDef def = Rails.ForDeparture(l, from);
            if (def == null) return;
            string fuel = Rails.TrainFuel(def);
            if (fuel.Length > 0 && !Economy.Take(from, fuel, 1)) return;
            float speed = VehicleRoster.Track(l.Kind) == "rail" && l.Kind != "bullet" && def.Speed < 4f ? Math.Min(1.4f, def.Speed) : def.Speed;
            var run = new TrainRun { Link = l, Forward = forward, From = from, To = to, Speed = 2.2f * speed };
            int cars = 3 + (int)(def.Speed);
            for (int i = 0; i < cars; i++) run.Parts.Add(Make(def.Art + (i == 0 ? "_loco" : "_car"), -20, 0.25f));
            string[] goods = Economy.Tradeable();
            string g = G.Pick(goods);
            int have = from.getResourcesAmount(g);
            if (have > 30)
            {
                run.Cargo = g;
                run.Amount = Math.Min(have / 4, 10 * cars);
                from.takeResource(g, run.Amount);
            }
            run.Passengers = Math.Min(from.getPopulationPeople() / 10, cars * 4);
            _movers.Add(run);
        }

        public static void Arrive(TrainRun r)
        {
            if (r.To == null || !r.To.isAlive()) return;
            if (!string.IsNullOrEmpty(r.Cargo) && r.Amount > 0)
            {
                Economy.Give(r.To, r.Cargo, r.Amount);
                if (r.From.kingdom != r.To.kingdom) Economy.RecordTrade(r.From.kingdom, r.To.kingdom, r.Amount / 4f);
                Economy.Give(r.From, "gold", Math.Max(1, r.Amount / 6));
            }
            if (r.Passengers > 0) Moods.Apply(r.To, "civ_train_trip", Math.Min(0.3f, r.Passengers * 0.01f));
        }

        private static void SpawnTram(City c)
        {
            WorldTile center = G.Tile(c);
            if (center == null || c.zones.Count < 2) return;
            TileZone z = G.Pick(c.zones);
            WorldTile end = z.centerTile;
            if (end == null) return;
            var a = new Vector2(center.x + 0.5f, center.y + 0.5f);
            var b = Globe.Nearest(new Vector2(end.x + 0.5f, end.y + 0.5f), a);
            if ((b - a).magnitude < 4f) return;
            SpriteRenderer body = Make("tram", -18, 0.25f);
            var f = new Flight { A = a, B = b, Duration = (b - a).magnitude / 3f, Height = 0f, Body = body, Go = body.gameObject, Upright = true };
            _movers.Add(f);
        }

        private static string PlaneSprite(Kingdom k)
        {
            switch (Research.Route(k))
            {
                case "steam": return "plane_airship";
                case "diesel": return "plane_zeppelin";
                case "tesla": return "plane_eplane";
                case "atom": return "plane_atomic";
                case "bio": return "plane_bio";
            }
            return Research.Has(k, "jet_airliners") ? "plane_jet" : "plane_prop";
        }

        private static void SpawnAirline(City c)
        {
            if (!Research.HasFeature(c.kingdom, "airline")) return;
            City dest = null;
            float bd = 0f;
            foreach (City o in G.Cities())
            {
                if (o == c || o.countBuildingsType("type_civ_airport") == 0 || o.kingdom == null) continue;
                if (World.world.wars.isInWarWith(c.kingdom, o.kingdom)) continue;
                float d = G.Dist(c, o);
                float s = d * UnityEngine.Random.Range(0.5f, 1.5f);
                if (s > bd)
                {
                    bd = s;
                    dest = o;
                }
            }
            if (dest == null) return;
            WorldTile ta = G.Tile(c), tb = G.Tile(dest);
            Vector2 a = new Vector2(ta.x + 0.5f, ta.y + 0.5f);
            Vector2 b = Globe.Nearest(new Vector2(tb.x + 0.5f, tb.y + 0.5f), a);
            string sprite = PlaneSprite(c.kingdom);
            SpriteRenderer body = Make(sprite, 30, 0.25f);
            SpriteRenderer shadow = Make(sprite + "_shadow", -15, 0.25f);
            City from = c;
            var f = new Flight { A = a, B = b, Duration = (b - a).magnitude / 9f, Height = 6f, Body = body, Shadow = shadow, Go = body.gameObject };
            f.Done = () =>
            {
                if (dest.isAlive())
                {
                    Moods.Apply(dest, "civ_flight", 0.05f);
                    if (from.kingdom != dest.kingdom) Economy.RecordTrade(from.kingdom, dest.kingdom, 2f);
                }
            };
            _movers.Add(f);
        }

        public static void LaunchMissile(WorldTile from, WorldTile to, Action done)
        {
            if (!On || from == null || to == null)
            {
                done();
                return;
            }
            Vector2 a = new Vector2(from.x + 0.5f, from.y + 0.5f);
            Vector2 b = Globe.Nearest(new Vector2(to.x + 0.5f, to.y + 0.5f), a);
            SpriteRenderer body = Make("missile", 40, 0.12f);
            var f = new Flight { A = a, B = b, Duration = 4f + (b - a).magnitude / 40f, Height = 25f + (b - a).magnitude * 0.25f, Body = body, Go = body.gameObject, Arc = true, Smoke = true, Done = done };
            _movers.Add(f);
        }

        public static void ConventionalMissile(WorldTile from, WorldTile to, bool hypersonic, Action done)
        {
            if (!On || from == null || to == null)
            {
                done();
                return;
            }
            Vector2 a = new Vector2(from.x + 0.5f, from.y + 0.5f);
            Vector2 b = Globe.Nearest(new Vector2(to.x + 0.5f, to.y + 0.5f), a);
            SpriteRenderer body = Make("missile", 40, hypersonic ? 0.1f : 0.12f);
            float distance = (b - a).magnitude;
            var f = new Flight { A = a, B = b, Duration = 1f + distance / (hypersonic ? 75f : 30f), Height = hypersonic ? 12f : 2f, Body = body, Go = body.gameObject, Arc = hypersonic, Smoke = true, Done = done };
            _movers.Add(f);
        }

        public static void Flyover(WorldTile from, WorldTile to, string sprite, Action done)
        {
            if (!On || from == null || to == null)
            {
                done();
                return;
            }
            Vector2 a = new Vector2(from.x + 0.5f, from.y + 0.5f);
            Vector2 b = Globe.Nearest(new Vector2(to.x + 0.5f, to.y + 0.5f), a);
            SpriteRenderer body = Make("plane_bomber", 30, 0.25f);
            var f = new Flight { A = a, B = b, Duration = 2f + (b - a).magnitude / 12f, Height = 8f, Body = body, Go = body.gameObject, Done = done };
            _movers.Add(f);
        }

        public static void Rocket(WorldTile pad, string route, Action done)
        {
            if (!On || pad == null)
            {
                done();
                return;
            }
            SpriteRenderer body = Make("rocket_" + (string.IsNullOrEmpty(route) ? "real" : route), 40, 0.25f);
            var l = new Launch { Pos = new Vector2(pad.x, pad.y), Body = body, Go = body.gameObject, Done = done };
            _movers.Add(l);
        }

        public static void Puff(Vector2 p, float scale)
        {
            if (_movers.Count > 600) return;
            SpriteRenderer body = Make("smoke", 35, 0.05f);
            body.transform.position = new Vector3(p.x, p.y, -0.4f);
            _movers.Add(new Puff { Body = body, Go = body.gameObject, Max = 1.2f, Scale = scale });
        }

        private static void SyncOrbiters()
        {
            foreach (Kingdom k in G.Kingdoms())
            {
                int want = Math.Min(12, Space.Satellites(k));
                int have;
                _orbiters.TryGetValue(G.Id(k), out have);
                while (have < want)
                {
                    SpriteRenderer body = Make("satellite", 60, 0.25f);
                    body.color = Toolbox.makeColor(G.Hex(k));
                    _movers.Add(new Orbiter { Body = body, Go = body.gameObject, X = UnityEngine.Random.Range(0f, MapBox.width), Y = UnityEngine.Random.Range(MapBox.height * 0.1f, MapBox.height * 0.9f), Speed = UnityEngine.Random.Range(3f, 7f), Kingdom = G.Id(k) });
                    have++;
                }
                _orbiters[G.Id(k)] = have;
            }
        }

        public static float ElectricLoad(City c)
        {
            float l = 0f;
            foreach (RailLink r in Rails.LinksOf(c))
            {
                TrainDef d = Db.Trains.Find(t => t.Id == r.Kind);
                if (d != null && d.Fuel == "electric") l += 3f;
            }
            if (Research.HasFeature(c.kingdom, "metro")) l += c.countBuildingsType("type_civ_metro") * 2f;
            return l;
        }

        public static float Emissions(City c)
        {
            float e = 0f;
            foreach (RailLink r in Rails.LinksOf(c))
            {
                TrainDef d = Db.Trains.Find(t => t.Id == r.Kind);
                if (d != null && (d.Fuel == "coal" || d.Fuel == "fuel")) e += 0.01f;
            }
            if (c.countBuildingsType("type_civ_airport") > 0) e += 0.01f;
            return e;
        }

        public static int WantedDrivers(City c)
        {
            return Rails.LinksOf(c).Count > 0 ? 1 : 0;
        }

        public static int WantedPilots(City c)
        {
            return c.countBuildingsType("type_civ_airport") > 0 ? 2 : 0;
        }

        [HarmonyPatch(typeof(DynamicSprites), nameof(DynamicSprites.getBuildingLight))]
        private static class Blackout
        {
            private static void Postfix(Building pBuilding, ref Sprite __result)
            {
                if (__result == null || pBuilding == null || pBuilding.city == null) return;
                City c = pBuilding.city;
                if (c.kingdom == null || !Research.Has(c.kingdom, "electricity")) return;
                if (Energy.Blackout(c)) __result = null;
            }
        }
    }
}
