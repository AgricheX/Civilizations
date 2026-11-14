using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Vassals
    {
        private const string KOrigin = Gov.P + "origin";
        private const string KState = Gov.P + "vstate";
        private const string KTrib = Gov.P + "trib_last";
        public static readonly string[] States = { "resentful", "estranged", "settled", "united" };
        public static bool Busy;
        private static readonly List<KeyValuePair<City, Kingdom>> _taken = new List<KeyValuePair<City, Kingdom>>();
        private static readonly List<KeyValuePair<Kingdom, Kingdom>> _rebels = new List<KeyValuePair<Kingdom, Kingdom>>();

        public static bool On(Kingdom k)
        {
            return G.Ok(k) && Gov.On("civ_gov_vassals") && GovEra.Has(k, GovEra.Tributaries);
        }

        public static void Register()
        {
            Hub.On(Hub.CityCaptured, e =>
            {
                if (Busy || !Gov.On("civ_gov_vassals") || !G.Ok(e.K) || !G.Ok(e.O) || e.C == null) return;
                if (e.Id == "captured") _taken.Add(new KeyValuePair<City, Kingdom>(e.C, e.K));
                else if (e.Id == "rebellion") _rebels.Add(new KeyValuePair<Kingdom, Kingdom>(e.K, e.O));
            });
            Hub.On(Hub.Dependent, e =>
            {
                if (!G.Ok(e.K) || e.Arg == "evolved") return;
                Dep d = Diplomacy.DepOf(e.K);
                if (d == null) return;
                string why = e.Arg ?? "";
                float auto = 65f;
                switch (why)
                {
                    case "surrender":
                    case "peace":
                    case "puppet":
                    case "war":
                        auto = 90f;
                        break;
                    case "protection":
                        auto = 50f;
                        break;
                    case "granted":
                    case "rebel":
                        auto = 40f;
                        break;
                    case "release":
                    case "appanage":
                    case "grant":
                        auto = 12f;
                        break;
                }
                d.Autonomy = auto;
                Store.Put(e.K.data, KOrigin, why);
                Store.Put(e.K.data, KState, State(e.K));
                Treasury.Note(e.O, Txt.F("civ_treas_note_vassal_in", G.Name(e.K)));
            });
            Hub.On(Hub.Independence, e =>
            {
                if (G.Ok(e.O) && G.Ok(e.K)) Treasury.Note(e.O, Txt.F("civ_treas_note_vassal_out", G.Name(e.K)));
            });
        }

        public static void Clear()
        {
            _taken.Clear();
            _rebels.Clear();
        }

        public static float Integration(Kingdom k)
        {
            Dep d = Diplomacy.DepOf(k);
            return d == null ? 0f : G.Clamp(100f - d.Autonomy, 0f, 100f);
        }

        public static void SetIntegration(Kingdom k, float v)
        {
            Dep d = Diplomacy.DepOf(k);
            if (d != null) d.Autonomy = G.Clamp(100f - v, 0f, 100f);
        }

        public static int State(Kingdom k)
        {
            if (Diplomacy.DepOf(k) == null) return -1;
            float i = Integration(k);
            int s = i < 25f ? 0 : i < 50f ? 1 : i < 80f ? 2 : 3;
            if (s == 3 && !GovEra.Has(Diplomacy.Overlord(k), GovEra.Provinces)) s = 2;
            return s;
        }

        public static string StateName(int s)
        {
            return s < 0 ? "" : Txt.T("civ_vas_state_" + States[s]);
        }

        public static string Origin(Kingdom k)
        {
            return G.Ok(k) ? Store.Str(k.data, KOrigin) : "";
        }

        public static string Status(Kingdom k)
        {
            Kingdom lord = Diplomacy.Overlord(k);
            if (!G.Ok(lord)) return "";
            if (State(k) < 3) return "vassal";
            long a = Fam.Royal(k), b = Fam.Royal(lord);
            if (a != 0L && a == b && GovEra.Has(lord, GovEra.Hereditary)) return "hereditary_province";
            return "province";
        }

        public static float TributeMult(Kingdom lord)
        {
            if (!G.Ok(lord) || !Gov.On("civ_gov_vassals") || !Parl.Active(lord) || !GovEra.Has(lord, GovEra.DiplomacyTab)) return 1f;
            switch (Parl.Get(lord, "tribute"))
            {
                case 0: return 0f;
                case 2: return 2f;
                case 3: return 3f;
            }
            return 1f;
        }

        public static void Tribute(Kingdom v, int gold)
        {
            if (G.Ok(v)) Store.Put(v.data, KTrib, gold);
        }

        public static int LastTribute(Kingdom v)
        {
            return G.Ok(v) ? Store.Int(v.data, KTrib, 0) : 0;
        }

        public static int Opinion(Kingdom v, Kingdom lord)
        {
            if (!On(lord) || Diplomacy.Overlord(v) != lord) return 0;
            int o = Mandate.VassalOpinion(lord);
            if (G.Cities(lord).Count < 6) o -= 15;
            if (Parl.Active(lord) && GovEra.Has(lord, GovEra.DiplomacyTab))
            {
                int t = Parl.Get(lord, "tribute");
                o += t == 0 ? 20 : t == 2 ? -30 : t == 3 ? -60 : 0;
                int r = Parl.Get(lord, "vassal_rule");
                if (r == 0) o += 15;
                if (r == 2 && (v.getCulture() != lord.getCulture())) o -= 30;
            }
            return o;
        }

        public static void Flush()
        {
            var r = _rebels.ToList();
            _rebels.Clear();
            foreach (var kv in r) if (G.Ok(kv.Key) && G.Ok(kv.Value)) CivMod.Safe("gov-grant-rebel", () => GrantRebel(kv.Key, kv.Value));
            var l = _taken.ToList();
            _taken.Clear();
            foreach (var kv in l) if (G.Ok(kv.Key) && G.Ok(kv.Value)) CivMod.Safe("gov-grant-conquest", () => GrantConquest(kv.Value, kv.Key));
        }

        private static float LandsMult(Kingdom k)
        {
            if (!Parl.Active(k) || !GovEra.Has(k, GovEra.Fiefs)) return 1f;
            switch (Parl.Get(k, "lands"))
            {
                case 0: return 1.6f;
                case 2: return 0.3f;
            }
            return 1f;
        }

        private static void GrantConquest(Kingdom k, City c)
        {
            if (!On(k) || !GovEra.Has(k, GovEra.Fiefs) || Player.Is(k) || c.kingdom != k || Gov.Form(k) == "military_republic") return;
            if (Diplomacy.Overlord(k) != null || k.countCities() <= k.getMaxCities()) return;
            if (!G.Chance(Math.Min(0.9f, 0.5f * LandsMult(k)))) return;
            Kingdom nk = ReleaseAsVassal(c, k, null, "grant");
            if (nk != null) Gov.Log("grant", k, G.Name(k), G.Name(c), G.Name(nk), nk, c);
        }

        private static void GrantRebel(Kingdom rebel, Kingdom old)
        {
            if (!On(old) || Player.Is(old) || Store.Str(rebel.data, Mandate.KRebelOf).Length > 0 || rebel.countCities() > 1) return;
            if (old.capital != null && rebel.capital != null && old.capital.neighbours_cities != null && old.capital.neighbours_cities.Contains(rebel.capital)) return;
            int ph = Mandate.Phase(old);
            float ch = ph == 1 ? 0.8f : ph == 0 ? 0.5f : ph == 2 ? 0.15f : ph == 3 ? 0f : 0.3f;
            if (!G.Chance(ch * LandsMult(old))) return;
            foreach (global::War w in rebel.getWars().ToList()) if (w.isAttacker(old) || w.isDefender(old)) World.world.wars.endWar(w, WarWinner.Peace);
            Diplomacy.MakeDependent(rebel, old, "granted");
            Gov.Log("grant", old, G.Name(old), G.Name(rebel.capital), G.Name(rebel), rebel, rebel.capital);
        }

        public static Kingdom ReleaseAsVassal(City c, Kingdom lord, Actor ruler, string why)
        {
            if (!G.Ok(c) || !G.Ok(lord) || c == lord.capital || c.kingdom != lord) return null;
            Actor a = ruler ?? Gov.Somebody(c);
            if (a == null) return null;
            Kingdom nk;
            Busy = true;
            try
            {
                if (a.city != c && a.city != null && a.isCityLeader()) a.city.removeLeader();
                nk = c.makeOwnKingdom(a, false, false);
            }
            finally
            {
                Busy = false;
            }
            if (!G.Ok(nk)) return null;
            foreach (global::War w in nk.getWars().ToList()) if (w.isAttacker(lord) || w.isDefender(lord)) World.world.wars.endWar(w, WarWinner.Peace);
            Diplomacy.MakeDependent(nk, lord, why);
            Fam.Invalidate(lord);
            return nk;
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            if (!On(k)) return;
            Kingdom lord = Diplomacy.Overlord(k);
            if (G.Ok(lord)) AsVassal(k, lord, yrs);
            AsLord(k, p, yrs);
        }

        private static void AsVassal(Kingdom k, Kingdom lord, int yrs)
        {
            Dep d = Diplomacy.DepOf(k);
            if (d == null) return;
            float drift = 0f;
            int ph = Mandate.Phase(lord);
            if (ph == 0 || ph == 1) drift -= 1.2f;
            else if (ph == 2) drift += 0.5f + (50f - Math.Min(50f, Mandate.Get(lord))) / 20f;
            else if (ph == 3) drift += 4f;
            if (k.getCulture() == lord.getCulture()) drift -= 0.5f;
            if (Fam.Royal(k) != 0L && Fam.Royal(k) == Fam.Royal(lord)) drift -= 1f;
            if (G.Cities(lord).Count < 6) drift += 2f;
            if (G.Cities(k).Count * 2 >= G.Cities(lord).Count) drift += 1.5f;
            if (Origin(k) == "surrender" || Origin(k) == "peace") drift += 0.4f;
            if (Parl.Active(lord) && GovEra.Has(lord, GovEra.DiplomacyTab))
            {
                int r = Parl.Get(lord, "vassal_rule");
                if (r == 0) drift += 1f;
                if (r == 2)
                {
                    drift -= 1f;
                    Culture cul = lord.capital != null ? lord.capital.getCulture() : null;
                    if (cul != null) foreach (City c in G.Cities(k)) if (c.getCulture() != cul && G.Chance(0.05f * yrs)) c.setCulture(cul);
                }
            }
            if (Parl.Active(k) && GovEra.Has(k, GovEra.DiplomacyTab)) drift += Parl.Get(k, "fealty") == 0 ? 1f : Parl.Get(k, "fealty") == 2 ? -1f : 0f;
            d.Autonomy = G.Clamp(d.Autonomy + drift * yrs, 0f, 100f);
            int st = State(k);
            int old = Store.Int(k.data, KState, st);
            if (old != st)
            {
                Store.Put(k.data, KState, st);
                if (st == 3) Gov.Log("province", k, G.Name(k), G.Name(lord), Gov.FormName(k), lord);
                else Gov.Log("vassal_state", k, G.Name(k), G.Name(lord), StateName(st), lord);
                Bonus.Dirty(k);
            }
            if (Player.Is(k) && !Player.Auto(k, "war")) return;
            float integ = Integration(k);
            bool weak = ph >= 2 || Profile.Get(lord).Strength < Profile.Get(k).Strength * (1f + integ / 50f);
            if (integ < 50f && weak)
            {
                float want = 0.05f * yrs * (1f + (50f - integ) / 50f);
                if (Parl.Active(k) && Parl.Get(k, "fealty") == 0) want *= 1.6f;
                if (lord.getWars().Any(w => w != null && !w.hasEnded())) want *= 1.5f;
                if (G.Chance(want)) Independence(k, true);
            }
        }

        private static void AsLord(Kingdom k, KProfile p, int yrs)
        {
            List<Kingdom> vs = Diplomacy.DependentsOf(k);
            bool auto = !Player.Is(k) || Player.Auto(k, "war");
            int ph = Mandate.Phase(k);
            if (auto && Diplomacy.Overlord(k) == null && ph != 2 && ph != 3 && GovEra.Has(k, GovEra.Provinces))
            {
                foreach (Kingdom v in vs)
                {
                    if (State(v) < 3 || k.countCities() >= k.getMaxCities()) continue;
                    float ch = 0.2f * yrs;
                    if (Parl.Is(k, "lands", "direct")) ch = 0.35f * yrs;
                    if (Parl.Is(k, "lands", "feudal")) ch = 0.08f * yrs;
                    if (Status(v) == "hereditary_province") ch *= 0.5f;
                    if (G.Chance(ch)) DirectRule(k, v);
                    break;
                }
            }
            if (!auto) return;
            if (GovEra.Has(k, GovEra.Fiefs) && !Gov.Republic(k) && k.countCities() > k.getMaxCities() && G.Chance(0.1f * yrs * LandsMult(k))) Appanage(k);
            bool greedy = Parl.Is(k, "foreign", "expansion");
            if (Parl.Is(k, "foreign", "peace") || ph == 2 || ph == 3 || Diplomacy.Overlord(k) != null) return;
            if (G.Chance((greedy ? 0.08f : 0.04f) * yrs * (Gov.Form(k) == "empire" ? 1.5f : 1f)))
            {
                Kingdom t = Victim(k, p);
                if (t != null) Submission(k, t);
            }
        }

        public static float CampStrength(Kingdom k)
        {
            float s = Profile.Get(k).Strength;
            foreach (Kingdom v in Diplomacy.DependentsOf(k)) s += Profile.Get(v).Strength;
            return s;
        }

        public static Kingdom Victim(Kingdom k, KProfile p)
        {
            var near = new HashSet<Kingdom>();
            foreach (City c in G.Cities(k)) if (c.neighbours_kingdoms != null) foreach (Kingdom o in c.neighbours_kingdoms) near.Add(o);
            float mine = CampStrength(k);
            float need = Gov.Form(k) == "empire" ? 2f : 3f;
            return near.Where(o => G.Ok(o) && o != k && o.isCiv() && !Player.Is(o) && Diplomacy.DepOf(o) == null && Diplomacy.DependentsOf(o).Count == 0 && !o.getWars().Any(w => w != null && !w.hasEnded()) && Regard(o, k) >= 0 && Fog.Knows(k, o) && CampOf(o) * need < mine).OrderBy(o => Profile.Get(o).Strength).FirstOrDefault();
        }

        private static float CampOf(Kingdom o)
        {
            float s = CampStrength(o);
            foreach (Kingdom a in Diplomacy.Protectors(o)) if (G.Ok(a)) s += Profile.Get(a).Strength;
            return s;
        }

        private static int Regard(Kingdom a, Kingdom b)
        {
            try
            {
                KingdomOpinion op = World.world.diplomacy.getOpinion(a, b);
                return op != null ? op.total : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void Submission(Kingdom k, Kingdom t)
        {
            if (!G.Ok(k) || !G.Ok(t) || Diplomacy.DepOf(t) != null) return;
            Diplomacy.MakeDependent(t, k, "demand");
            Gov.Log("submission", k, G.Name(t), G.Name(k), null, t);
        }

        public static void Appanage(Kingdom k)
        {
            long royal = Fam.Royal(k);
            Clan c = Gov.ClanById(royal);
            if (c == null || k.capital == null) return;
            var kids = G.Ok(k.king) ? new HashSet<Actor>(k.king.getChildren(true)) : new HashSet<Actor>();
            Actor a = c.units.Where(x => G.Ok(x) && x.kingdom == k && x.isAdult() && !x.isKing()).OrderByDescending(x => kids.Contains(x)).ThenByDescending(x => x.getAge()).FirstOrDefault();
            if (a == null) return;
            City far = G.Cities(k).Where(x => G.Ok(x) && x != k.capital).OrderByDescending(x => G.Dist(x, k.capital)).FirstOrDefault();
            if (far == null) return;
            Kingdom nk = ReleaseAsVassal(far, k, a, "appanage");
            if (nk != null) Gov.Log("appanage", k, G.Name(k), a.getName(), G.Name(far), nk, far);
        }

        public static void DirectRule(Kingdom k, Kingdom v)
        {
            if (!G.Ok(k) || !G.Ok(v) || Diplomacy.Overlord(v) != k) return;
            City c = G.Cities(v).Where(G.Ok).OrderBy(x => k.capital != null ? G.Dist(x, k.capital) : 0f).FirstOrDefault();
            if (c == null) return;
            bool last = G.Cities(v).Count <= 1;
            if (last) Diplomacy.Release(v, "direct_rule");
            Busy = true;
            try
            {
                c.joinAnotherKingdom(k, false, false);
            }
            finally
            {
                Busy = false;
            }
            Fam.Invalidate(k);
            Gov.Log("direct_rule", k, G.Name(k), G.Name(c), G.Name(v), v, c);
            if (last) Hub.Raise(Hub.Integrated, k, v, k.capital, null, "direct_rule");
        }

        public static void Annex(Kingdom k, Kingdom v)
        {
            if (!G.Ok(k) || !G.Ok(v) || k == v) return;
            if (Diplomacy.DepOf(v) != null) Diplomacy.Release(v, "direct_rule");
            Busy = true;
            try
            {
                foreach (City c in G.Cities(v).ToList()) c.joinAnotherKingdom(k, false, false);
            }
            finally
            {
                Busy = false;
            }
            Fam.Invalidate(k);
            Hub.Raise(Hub.Integrated, k, v, k.capital, null, "direct_rule");
        }

        public static void Independence(Kingdom v, bool war)
        {
            Kingdom lord = Diplomacy.Overlord(v);
            if (!G.Ok(lord)) return;
            Diplomacy.Release(v, "plot");
            Gov.Log("independence", v, G.Name(v), G.Name(lord), null, lord);
            if (!war || World.world.wars.isInWarWith(v, lord)) return;
            global::War w;
            Statecraft.Escalating = true;
            try
            {
                w = World.world.diplomacy.startWar(v, lord, WarTypeLibrary.normal);
            }
            finally
            {
                Statecraft.Escalating = false;
            }
            if (w != null) WarGoals.Set(w, "independence");
        }

        public static void Make(Kingdom vassal, Kingdom lord)
        {
            if (!G.Ok(vassal) || !G.Ok(lord) || vassal == lord) return;
            foreach (Kingdom sub in Diplomacy.DependentsOf(vassal).ToList()) Diplomacy.Release(sub, "sandbox");
            Diplomacy.MakeDependent(vassal, lord, "sandbox");
        }
    }
}
