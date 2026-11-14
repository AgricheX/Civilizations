using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class GovWar
    {
        private const string KTalks = Gov.P + "talks";
        private const string KCapA = Gov.P + "cap_a";
        private const string KCapD = Gov.P + "cap_d";
        private const string KCitA = Gov.P + "cities_a";
        private const string KCitD = Gov.P + "cities_d";
        private const string KDrop = Gov.P + "expect_drop";
        private static readonly Dictionary<long, float> _siegeNext = new Dictionary<long, float>();

        public static bool On(Kingdom k)
        {
            return Gov.On("civ_gov_peace") && GovEra.Has(k, GovEra.Nobles);
        }

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
                if (G.Ok(e.K))
                {
                    if (e.K.capital != null) Store.Put(w.data, KCapA, G.Id(e.K.capital));
                    Store.Put(w.data, KCitA, G.Cities(e.K).Count);
                }
                if (G.Ok(e.O))
                {
                    if (e.O.capital != null) Store.Put(w.data, KCapD, G.Id(e.O.capital));
                    Store.Put(w.data, KCitD, G.Cities(e.O).Count);
                }
            });
        }

        private static float Lost(global::War w, Kingdom k, string key)
        {
            int start = Store.Int(w.data, key, 0);
            if (start <= 0) return 0f;
            return G.Clamp((start - G.Cities(k).Count) / (float)start, 0f, 1f);
        }

        public static float Extra(global::War w)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d)) return 0f;
            float s = (Lost(w, d, KCitD) - Lost(w, a, KCitA)) * 15f;
            City capD = G.CityById(Store.Str(w.data, KCapD));
            City capA = G.CityById(Store.Str(w.data, KCapA));
            if (G.Ok(capD) && capD.kingdom == a) s += 10f;
            if (G.Ok(capA) && capA.kingdom == d) s -= 10f;
            float wa = a.countTotalWarriors(), wd = d.countTotalWarriors();
            s += (wa - wd) / (wa + wd + 10f) * 3f;
            return s;
        }

        private static float Expect(Kingdom k, global::War w, int age)
        {
            float e = 45f;
            Actor r = k.king;
            if (G.Ok(r) && (r.hasTrait("bloodlust") || r.hasTrait("ambitious"))) e += 15f;
            if (Parl.Is(k, "foreign", "expansion")) e += 10f;
            if (Parl.Is(k, "foreign", "peace")) e -= 15f;
            e -= age * 2f;
            e -= (w.isAttacker(k) ? w.getDeadAttackers() : w.getDeadDefenders()) / 25f;
            e -= Store.Flt(w.data, KDrop, 0f);
            return Math.Max(10f, e);
        }

        public static void Consider(global::War w, float s, int age)
        {
            Kingdom a = w.getMainAttacker(), d = w.getMainDefender();
            if (!G.Ok(a) || !G.Ok(d) || age < 3 || !On(a)) return;
            if (w.getAsset() != null && w.getAsset().total_war) return;
            Kingdom ahead = s >= 0f ? a : d, behind = ahead == a ? d : a;
            float gap = Math.Abs(s);
            if (gap < 20f) Store.Put(w.data, KDrop, Store.Flt(w.data, KDrop, 0f) + 4f);
            bool aheadAuto = !Player.Is(ahead) || Player.Auto(ahead, "war");
            bool behindAuto = !Player.Is(behind) || Player.Auto(behind, "war");
            float exp = Expect(ahead, w, age);
            if (aheadAuto && gap >= exp && G.Chance(0.6f))
            {
                Talks(w);
                return;
            }
            float ask = 0.05f + gap / 200f + age * 0.02f;
            if (behindAuto && aheadAuto && G.Chance(Math.Min(0.8f, ask)) && gap >= exp * 0.6f) Talks(w);
        }

        public static void Talks(global::War w)
        {
            if (w == null || w.hasEnded()) return;
            Store.Put(w.data, KTalks, "1");
            World.world.wars.endWar(w, WarWinner.Peace);
        }

        public static void ForcePeace(Kingdom a, Kingdom b)
        {
            global::War w = Statecraft.Find(a, b);
            if (w != null) Talks(w);
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
            int era = Math.Min(GovEra.Of(win), GovEra.Of(lose));
            var terms = new List<string>();
            if (goal == "independence" && (Diplomacy.Overlord(win) == lose || Diplomacy.Overlord(lose) == win || Store.Str(w.data, KTalks) == "1"))
            {
                Kingdom rebel = w.getMainAttacker(), lord = w.getMainDefender();
                if (win == lord && mag >= 80f)
                {
                    Diplomacy.MakeDependent(rebel, lord, "war");
                    terms.Add("submission");
                }
                else
                {
                    if (Diplomacy.Overlord(rebel) == lord) Diplomacy.Release(rebel, "war");
                    terms.Add("independence");
                    if (mag >= 20f && era >= GovEra.Reparations) Reparations(win, lose, 0.2f, terms);
                }
            }
            else if (mag < 20f) terms.Add("white_peace");
            else
            {
                if (era >= GovEra.Reparations) Reparations(win, lose, mag >= 50f ? 0.25f : 0.2f, terms);
                if (mag >= 50f) CedeVassals(win, lose, terms);
                if (mag >= 80f)
                {
                    Kingdom taker = Diplomacy.Overlord(win) ?? win;
                    if (era >= GovEra.Tributaries && Diplomacy.DependentsOf(lose).Count == 0 && taker != lose)
                    {
                        Diplomacy.MakeDependent(lose, taker, "surrender");
                        terms.Add("submission");
                    }
                    else CedeCity(win, lose, terms);
                }
                if (terms.Count == 0) terms.Add("white_peace");
            }
            Diplomacy.Sign(win, lose, era >= 12 ? "armistice" : "truce", 5 + (int)(mag / 10f));
            if (terms.Count == 1 && terms[0] == "white_peace")
            {
                Hub.Raise(Hub.WarEnded, a, d, null, null, "draw", goal);
                Hub.Raise(Hub.PeaceTerms, a, d, d.capital, null, "white_peace", goal);
            }
            else
            {
                Hub.Raise(Hub.WarEnded, win, lose, null, null, win == a ? "Attackers" : "Defenders", goal);
                Hub.Raise(Hub.PeaceTerms, win, lose, lose.capital, null, string.Join(",", terms.ToArray()), goal, mag / 20f);
            }
            Gov.Log("talks", win, G.Name(win), G.Name(lose), string.Join(", ", terms.Select(t => Txt.T("civ_war_term_" + t)).ToArray()), lose);
        }

        private static void Reparations(Kingdom win, Kingdom lose, float share, List<string> terms)
        {
            float got = Treasury.Seize(lose, share);
            if (got < 1f) return;
            Treasury.Receive(win, got, "reparations");
            Treasury.Note(win, Txt.F("civ_treas_note_rep_in", G.Name(lose), (int)got));
            Treasury.Note(lose, Txt.F("civ_treas_note_rep_out", G.Name(win), (int)got));
            terms.Add("reparations");
        }

        private static void CedeVassals(Kingdom win, Kingdom lose, List<string> terms)
        {
            Kingdom taker = Diplomacy.Overlord(win) ?? win;
            int ph = Mandate.Phase(taker);
            if (ph == 2 || ph == 3) return;
            bool any = false;
            Actor r = win.king;
            float skill = G.Ok(r) ? 0.35f + r.diplomacy / 40f + (r.hasTrait("ambitious") ? 0.15f : 0f) : 0.3f;
            foreach (Kingdom v in Diplomacy.DependentsOf(lose).ToList())
            {
                if (v == win || v == taker) continue;
                bool borders = v.capital != null && G.Cities(v).Any(c => c.neighbours_kingdoms != null && (c.neighbours_kingdoms.Contains(taker) || c.neighbours_kingdoms.Contains(win)));
                if (!borders || !G.Chance(skill)) continue;
                Diplomacy.MakeDependent(v, taker, "cession");
                any = true;
            }
            if (any) terms.Add("cede_vassals");
        }

        private static void CedeCity(Kingdom win, Kingdom lose, List<string> terms)
        {
            City c = G.Cities(lose).Where(x => G.Ok(x) && x != lose.capital && x.neighbours_kingdoms != null && x.neighbours_kingdoms.Contains(win)).OrderBy(x => x.getPopulationPeople()).FirstOrDefault();
            if (c == null) return;
            c.joinAnotherKingdom(win, true, false);
            terms.Add("cede_city");
        }

        private static bool Fighter(Actor a)
        {
            return G.Ok(a) && (a.isWarrior() || a.is_army_captain || a.army != null);
        }

        public static void Siege(City c)
        {
            if (!G.Ok(c) || c.being_captured_by == null || c.kingdom == null || !On(c.kingdom)) return;
            Kingdom att = c.being_captured_by;
            if (!G.Ok(att) || att == c.kingdom || !World.world.wars.isInWarWith(att, c.kingdom)) return;
            long id = c.data.id;
            float now = Time.time, next;
            if (_siegeNext.TryGetValue(id, out next) && now < next) return;
            _siegeNext[id] = now + 2f;
            var zones = new HashSet<TileZone>(c.zones);
            var attackers = att.getUnits().Where(a => Fighter(a) && a.current_tile != null && zones.Contains(a.current_tile.zone)).ToList();
            var defenders = c.kingdom.getUnits().Where(a => Fighter(a) && a.current_tile != null && zones.Contains(a.current_tile.zone)).ToList();
            if (defenders.Count > 0)
            {
                foreach (Actor cap in attackers.Where(x => x.is_army_captain && !x.has_attack_target))
                {
                    Actor t = defenders.OrderBy(x => Vector2.SqrMagnitude(cap.current_position - x.current_position)).FirstOrDefault();
                    if (t != null) cap.setAttackTarget(t);
                }
            }
            if (attackers.Count >= 5 && attackers.Count >= defenders.Count * 5)
            {
                Kingdom old = c.kingdom;
                _siegeNext.Remove(id);
                c.finishCapture(att);
                Gov.Log("storm", att, G.Name(c), G.Name(att), G.Name(old), old, c);
            }
        }

        [HarmonyPatch(typeof(City), nameof(City.updateCapture))]
        private static class Storm
        {
            private static void Postfix(City __instance)
            {
                if (__instance == null || __instance.being_captured_by == null) return;
                CivMod.Safe("gov-storm", () => Siege(__instance));
            }
        }
    }
}
