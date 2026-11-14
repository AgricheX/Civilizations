using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class ArmyCommand
    {
        private const string KTier = "civ_arm_tier";
        public const string KMission = "civ_mission";
        public static readonly string[] Missions = { "standby", "rest", "rescue", "campaign", "border_patrol" };
        private static float _next;
        private static int _cursor;
        private static Vector3 _rdown;
        private static bool _rheld;
        private static GameObject _arrow;
        private static LineRenderer _line;
        private static float _arrowUntil;
        private static Material _mat;

        public static void Clear()
        {
            _next = 0f;
            _cursor = 0;
            if (_arrow != null) _arrow.SetActive(false);
        }

        public static int Tier(Kingdom k)
        {
            return G.Ok(k) ? Store.Int(k.data, KTier, 0) : 0;
        }

        public static int MaxTier(Kingdom k)
        {
            return Math.Min(6, Gov.Band(k) + 1);
        }

        public static float NextCost(Kingdom k)
        {
            return 100f * (Tier(k) + 1) * (1f + Gov.Band(k) * 0.5f);
        }

        public static float Spend(Kingdom k, float res, float keep)
        {
            if (!Gov.On("civ_gov_armies") || !G.Ok(k) || Tier(k) >= MaxTier(k)) return 0f;
            float cost = NextCost(k);
            if (res - keep < cost) return 0f;
            Store.Put(k.data, KTier, Tier(k) + 1);
            Bonus.Dirty(k);
            Gov.Log("armament", k, G.Name(k), Txt.T("civ_armtier_" + Tier(k)));
            return cost;
        }

        public static void SetTier(Kingdom k, int t)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KTier, Math.Max(0, Math.Min(6, t)));
            Bonus.Dirty(k);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            int t = Tier(k);
            if (t > 0)
            {
                fx["attack"] = t * 0.02f;
                fx["defense"] = t * 0.015f;
            }
            return fx;
        }

        public static string Mission(City c)
        {
            if (!G.Ok(c)) return "standby";
            string m = Store.Str(c.data, KMission);
            return m.Length == 0 ? "standby" : m;
        }

        public static void SetMission(City c, string m, bool player)
        {
            if (!G.Ok(c) || Array.IndexOf(Missions, m) < 0) return;
            Store.Put(c.data, KMission, m);
            Store.Put(c.data, KMission + "_p", player);
        }

        public static bool Guard(City c)
        {
            return G.Ok(c) && c.kingdom != null && c == c.kingdom.capital;
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            foreach (City c in G.Cities(k))
            {
                if (Store.Bool(c.data, KMission + "_p", false) && Player.Is(k)) continue;
                string m;
                if (Guard(c)) m = "standby";
                else if (G.Cities(k).Any(x => x.isGettingCaptured())) m = "rescue";
                else if (p.AtWar && c.neighbours_kingdoms != null && c.neighbours_kingdoms.Any(o => World.world.wars.isInWarWith(k, o))) m = "campaign";
                else if (Treasury.Unpaid(k) > 0) m = "rest";
                else m = c.neighbours_kingdoms != null && c.neighbours_kingdoms.Count > 0 ? "border_patrol" : "rest";
                Store.Put(c.data, KMission, m);
            }
        }

        private static WorldTile Target(City c, string m)
        {
            Kingdom k = c.kingdom;
            switch (m)
            {
                case "rescue":
                    City t = G.Cities(k).Where(x => x.isGettingCaptured()).OrderBy(x => G.Dist(x, c)).FirstOrDefault();
                    return t != null ? G.Tile(t) : null;
                case "campaign":
                    City e = null;
                    float bd = float.MaxValue;
                    if (c.neighbours_cities != null)
                    {
                        foreach (City n in c.neighbours_cities)
                        {
                            if (!G.Ok(n) || n.kingdom == null || !World.world.wars.isInWarWith(k, n.kingdom)) continue;
                            float d = G.Dist(n, c);
                            if (d < bd)
                            {
                                bd = d;
                                e = n;
                            }
                        }
                    }
                    return e != null ? G.Tile(e) : null;
                case "border_patrol":
                    if (c.border_zones == null || c.border_zones.Count == 0) return null;
                    TileZone z = c.border_zones.ElementAt(UnityEngine.Random.Range(0, c.border_zones.Count));
                    return z != null ? z.centerTile : null;
            }
            return null;
        }

        private static void Drive()
        {
            if (!G.Loaded || !Gov.On("civ_gov_armies")) return;
            List<City> all = G.Cities();
            if (all.Count == 0) return;
            int n = Math.Min(8, all.Count);
            for (int i = 0; i < n; i++)
            {
                _cursor = (_cursor + 1) % all.Count;
                City c = all[_cursor];
                if (!G.Ok(c) || c.army == null || c.kingdom == null || !c.kingdom.isCiv()) continue;
                string m = Mission(c);
                if (m == "standby" || m == "rest") continue;
                Actor cap = c.army.getCaptain();
                if (!G.Ok(cap) || cap.has_attack_target) continue;
                WorldTile t = Target(c, m);
                if (t == null) continue;
                cap.goTo(t, false, false, false, 0);
            }
        }

        private static void Order()
        {
            if (!Gov.On("civ_gov_armies") || !G.Loaded || World.world == null) return;
            if (Input.GetMouseButtonDown(1))
            {
                _rdown = Input.mousePosition;
                _rheld = true;
            }
            if (!_rheld || !Input.GetMouseButtonUp(1)) return;
            _rheld = false;
            if ((Input.mousePosition - _rdown).sqrMagnitude > 64f) return;
            if (World.world.isOverUI() || SelectedUnit.countSelected() == 0) return;
            WorldTile tile = World.world.getMouseTilePos();
            if (tile == null) return;
            List<Actor> units = SelectedUnit.getAllSelectedList().Where(a => G.Ok(a) && a.kingdom != null && a.kingdom.isCiv()).ToList();
            if (units.Count == 0) return;
            Kingdom k = units[0].kingdom;
            City at = tile.zone != null ? tile.zone.city : null;
            string kind = "camp";
            WorldTile dest = tile;
            if (at != null && at.kingdom != null && at.kingdom != k && World.world.wars.isInWarWith(k, at.kingdom)) kind = "attack";
            else if (at != null && at.kingdom == k)
            {
                kind = "garrison";
                dest = G.Tile(at) ?? tile;
            }
            Vector2 from = Vector2.zero;
            foreach (Actor a in units)
            {
                if (a.kingdom != k) continue;
                from += a.current_position;
                a.cancelAllBeh();
                a.goTo(dest, false, false, false, 0);
                if (kind == "garrison" && a.army == null && at.army != null && a.isWarrior()) a.setArmy(at.army);
            }
            from /= units.Count;
            Arrow(from, new Vector2(dest.x, dest.y), kind == "attack" ? new Color(1f, 0.3f, 0.25f) : kind == "garrison" ? new Color(0.35f, 0.7f, 1f) : new Color(1f, 0.85f, 0.3f));
        }

        private static void Arrow(Vector2 a, Vector2 b, Color col)
        {
            if (_arrow == null)
            {
                _arrow = new GameObject("CivOrderArrow");
                UnityEngine.Object.DontDestroyOnLoad(_arrow);
                _line = _arrow.AddComponent<LineRenderer>();
                Shader sh = Shader.Find("Sprites/Default");
                _mat = sh != null ? new Material(sh) : null;
                if (_mat != null) _line.material = _mat;
                _line.positionCount = 5;
                _line.useWorldSpace = true;
                _line.sortingOrder = 32000;
            }
            _arrow.SetActive(true);
            Vector2 dir = (b - a).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float head = Mathf.Min(4f, (b - a).magnitude * 0.3f);
            Vector2 back = b - dir * head;
            _line.positionCount = 5;
            _line.SetPosition(0, new Vector3(a.x, a.y, -1f));
            _line.SetPosition(1, new Vector3(b.x, b.y, -1f));
            _line.SetPosition(2, new Vector3(back.x + side.x * head * 0.6f, back.y + side.y * head * 0.6f, -1f));
            _line.SetPosition(3, new Vector3(b.x, b.y, -1f));
            _line.SetPosition(4, new Vector3(back.x - side.x * head * 0.6f, back.y - side.y * head * 0.6f, -1f));
            _line.startWidth = 0.6f;
            _line.endWidth = 0.6f;
            _line.startColor = col;
            _line.endColor = col;
            _arrowUntil = Time.unscaledTime + 3f;
        }

        public static void Frame()
        {
            if (_arrow != null && _arrow.activeSelf && Time.unscaledTime > _arrowUntil) _arrow.SetActive(false);
            Order();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1.5f;
            Drive();
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
