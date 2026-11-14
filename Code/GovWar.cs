using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class GovWar
    {
        public const string KTalks = "civ_talks";
        private const string KCapA = "civ_ws_capa";
        private const string KCapD = "civ_ws_capd";
        private static readonly Dictionary<long, float> _siegeNext = new Dictionary<long, float>();

        public static void Clear()
        {
            _siegeNext.Clear();
        }

        public static void Register()
        {
            Hub.On(Hub.WarStarted, e =>
            {
                global::War w = Statecraft.Find(e.K, e.O);
                if (w == null) return;
                if (G.Ok(e.K) && e.K.capital != null) Store.Put(w.data, KCapA, G.Id(e.K.capital));
                if (G.Ok(e.O) && e.O.capital != null) Store.Put(w.data, KCapD, G.Id(e.O.capital));
            });
        }

        public static float Extra(global::War w)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d)) return 0f;
            float s = 0f;
            City capD = G.CityById(Store.Str(w.data, KCapD));
            City capA = G.CityById(Store.Str(w.data, KCapA));
            if (G.Ok(capD) && capD.kingdom == a) s += 8f;
            if (G.Ok(capA) && capA.kingdom == d) s -= 8f;
            float wa = a.countTotalWarriors(), wd = d.countTotalWarriors();
            s += (wa - wd) / (wa + wd + 10f) * 5f;
            return s;
        }

        public static string Breakdown(global::War w)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d)) return "";
            City capD = G.CityById(Store.Str(w.data, KCapD));
            City capA = G.CityById(Store.Str(w.data, KCapA));
            bool heldD = G.Ok(capD) && capD.kingdom == a, heldA = G.Ok(capA) && capA.kingdom == d;
            return Txt.F("civ_ws_breakdown", (int)Statecraft.Score(w), a.countTotalWarriors(), d.countTotalWarriors(), heldD ? Txt.T("civ_yes") : Txt.T("civ_no"), heldA ? Txt.T("civ_yes") : Txt.T("civ_no"));
        }

        public static bool Open(global::War w)
        {
            return w != null && Statecraft.WarAge(w) >= 3;
        }

        public static void Consider(global::War w, float s, int age)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d) || age < 3) return;
            float p = 0.05f + Math.Abs(s) / 250f + (Statecraft.Exhaustion(a) + Statecraft.Exhaustion(d)) / 400f + age * 0.01f;
            if (!G.Chance(Math.Min(0.85f, p))) return;
            Kingdom asker = s >= 0f ? d : a, other = asker == a ? d : a;
            if (Player.Is(asker) && !Player.Auto(asker, "war")) asker = other;
            if (Player.Is(asker) && !Player.Auto(asker, "war")) return;
            if (Plots.Has(asker, "peace_talks", asker.data.id)) return;
            string motive = Statecraft.Exhaustion(asker) > 50f ? "exhaustion" : Math.Abs(s) >= 45f ? "defeat" : "stalemate";
            Plots.Start(asker, "peace_talks", asker.data.id, other.data.id, 1, motive);
        }

        public static void TalksBetween(Kingdom k, Kingdom o)
        {
            global::War w = Statecraft.Find(k, o);
            if (w != null) Talks(w);
        }

        public static void Talks(global::War w)
        {
            if (w == null || w.hasEnded()) return;
            Store.Put(w.data, KTalks, "1");
            World.world.wars.endWar(w, WarWinner.Peace);
        }

        public static bool Pending(global::War w)
        {
            return w != null && w.data != null && Store.Str(w.data, KTalks).Length > 0;
        }

        public static void Apply(global::War w)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d)) return;
            float s = Statecraft.Score(w);
            string goal = WarGoals.Get(w);
            Kingdom win = s >= 0f ? a : d, lose = win == a ? d : a;
            float mag = Math.Abs(s);
            var terms = new List<string>();
            int era = Profile.Get(win).Era;
            if (mag < 20f) terms.Add("white_peace");
            else
            {
                if (mag < 45f) Reparations(win, lose, 0.2f, terms);
                else if (mag < 70f)
                {
                    if (!CedeVassals(win, lose, terms)) CedeCity(win, lose, terms);
                    Reparations(win, lose, 0.1f, terms);
                }
                else
                {
                    if (Profile.Get(lose).Strength < Profile.Get(win).Strength * 1.5f && Diplomacy.DepOf(win) == null && Diplomacy.Overlord(win) != lose)
                    {
                        Diplomacy.MakeDependent(lose, win, "surrender");
                        terms.Add("submission");
                    }
                    else CedeCity(win, lose, terms);
                }
                Demands(win, lose, goal, terms);
            }
            Diplomacy.Sign(win, lose, era >= 12 ? "armistice" : "truce", 5 + (int)(mag / 10f));
            if (mag < 20f)
            {
                Hub.Raise(Hub.WarEnded, a, d, null, null, "draw", goal);
                Hub.Raise(Hub.PeaceTerms, a, d, d.capital, null, "white_peace", goal);
            }
            else
            {
                Hub.Raise(Hub.WarEnded, win, lose, null, null, win == a ? "Attackers" : "Defenders", goal);
                Hub.Raise(Hub.PeaceTerms, win, lose, lose.capital, null, string.Join(",", terms.ToArray()), goal, mag / 20f);
            }
            Gov.Log("peace_talks", win, G.Name(win), G.Name(lose), string.Join(", ", terms.Select(t => Txt.T("civ_term_" + t)).ToArray()), lose);
        }

        private static void Reparations(Kingdom win, Kingdom lose, float share, List<string> terms)
        {
            if (lose.capital == null || win.capital == null) return;
            int gold = (int)(Economy.Amount(lose.capital, "gold") * share);
            if (gold > 0 && Economy.Take(lose.capital, "gold", gold))
            {
                Economy.Give(win.capital, "gold", gold);
                terms.Add("reparations");
            }
        }

        private static bool CedeVassals(Kingdom win, Kingdom lose, List<string> terms)
        {
            var vs = Diplomacy.DependentsOf(lose);
            if (vs.Count == 0 || Diplomacy.DepOf(win) != null) return false;
            foreach (Kingdom v in vs)
            {
                if (v == win) continue;
                Diplomacy.MakeDependent(v, win, "cession");
            }
            terms.Add("cede_vassals");
            return true;
        }

        private static bool CedeCity(Kingdom win, Kingdom lose, List<string> terms)
        {
            City c = G.Cities(lose).Where(x => G.Ok(x) && x != lose.capital && x.neighbours_kingdoms != null && x.neighbours_kingdoms.Contains(win)).OrderBy(x => x.getPopulationPeople()).FirstOrDefault();
            if (c == null) return false;
            c.joinAnotherKingdom(win, true, false);
            if (!terms.Contains("cede_city")) terms.Add("cede_city");
            return true;
        }

        private static void Demands(Kingdom win, Kingdom lose, string goal, List<string> terms)
        {
            switch (goal)
            {
                case "conquest":
                case "colonial":
                    if (!terms.Contains("cede_city")) CedeCity(win, lose, terms);
                    break;
                case "independence":
                    if (Diplomacy.Overlord(win) == lose)
                    {
                        Diplomacy.Release(win, "war");
                        terms.Add("independence");
                    }
                    break;
                case "liberation":
                    if (Diplomacy.Overlord(lose) != null)
                    {
                        Diplomacy.Release(lose, "liberation");
                        terms.Add("liberation");
                    }
                    break;
                case "tribute":
                case "raid":
                    Diplomacy.Sign(lose, win, "tribute", 15);
                    terms.Add("tribute");
                    break;
                case "holy":
                case "ideology":
                    string ideo = Policies.Id(win, "ideo");
                    PolicyDef p = Db.Policy(ideo);
                    if (p != null && Policies.Unlocked(lose, p) && Policies.Id(lose, "ideo") != ideo)
                    {
                        Policies.Set(lose, "ideo", ideo, "forced");
                        terms.Add("ideology");
                    }
                    break;
            }
        }

        public static void ForcePeace(Kingdom a, Kingdom b)
        {
            global::War w = Statecraft.Find(a, b);
            if (w != null) Talks(w);
        }

        private static bool Fighter(Actor a)
        {
            return G.Ok(a) && (a.isWarrior() || a.is_army_captain || a.army != null);
        }

        public static void Siege(City c)
        {
            if (!Gov.On("civ_gov_peace") || !G.Ok(c) || c.being_captured_by == null || c.kingdom == null) return;
            Kingdom att = c.being_captured_by;
            if (!G.Ok(att) || att == c.kingdom || !World.world.wars.isInWarWith(att, c.kingdom)) return;
            long id = c.data.id;
            float now = Time.time, next;
            if (_siegeNext.TryGetValue(id, out next) && now < next) return;
            _siegeNext[id] = now + 2f;
            var zones = new HashSet<TileZone>(c.zones);
            var attackers = new List<Actor>();
            var defenders = new List<Actor>();
            foreach (Actor a in att.getUnits())
            {
                if (!Fighter(a) || a.current_tile == null || !zones.Contains(a.current_tile.zone)) continue;
                attackers.Add(a);
            }
            foreach (Actor a in c.kingdom.getUnits())
            {
                if (!Fighter(a) || a.current_tile == null || !zones.Contains(a.current_tile.zone)) continue;
                defenders.Add(a);
            }
            if (defenders.Count > 0)
            {
                foreach (Actor cap in attackers)
                {
                    if (!cap.is_army_captain || cap.has_attack_target) continue;
                    Actor target = null;
                    float bd = float.MaxValue;
                    foreach (Actor d in defenders)
                    {
                        float dd = Vector2.SqrMagnitude(cap.current_position - d.current_position);
                        if (dd < bd)
                        {
                            bd = dd;
                            target = d;
                        }
                    }
                    if (target != null) cap.setAttackTarget(target);
                }
            }
            if (attackers.Count >= 5 && attackers.Count >= defenders.Count * 5)
            {
                Kingdom old = c.kingdom;
                _siegeNext.Remove(id);
                c.finishCapture(att);
                Gov.Log("siege_fall", att, G.Name(c), G.Name(att), G.Name(old), old, c);
            }
        }

        [HarmonyPatch(typeof(City), nameof(City.updateCapture))]
        private static class SiegePatch
        {
            private static void Postfix(City __instance)
            {
                if (__instance == null || __instance.being_captured_by == null) return;
                CivMod.Safe("gov-siege", () => Siege(__instance));
            }
        }
    }
}
