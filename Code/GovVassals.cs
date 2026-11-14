using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Vassals
    {
        private const string KState = "civ_vstate";
        private const string KLowY = "civ_vlow_y";
        private static bool _busy;
        private static readonly List<KeyValuePair<City, Kingdom>> _pending = new List<KeyValuePair<City, Kingdom>>();
        private static readonly List<KeyValuePair<Kingdom, Kingdom>> _rebels = new List<KeyValuePair<Kingdom, Kingdom>>();

        public static void Register()
        {
            Hub.On(Hub.CityCaptured, e =>
            {
                if (_busy || !Gov.On("civ_gov_vassals") || !G.Ok(e.K) || !G.Ok(e.O) || e.C == null) return;
                if (e.Id == "captured") _pending.Add(new KeyValuePair<City, Kingdom>(e.C, e.K));
                else if (e.Id == "rebellion") _rebels.Add(new KeyValuePair<Kingdom, Kingdom>(e.K, e.O));
            });
        }

        public static void Clear()
        {
            _pending.Clear();
            _rebels.Clear();
        }

        public static void Flush()
        {
            var r = _rebels.ToList();
            _rebels.Clear();
            foreach (var kv in r) if (G.Ok(kv.Key) && G.Ok(kv.Value)) GrantRebel(kv.Key, kv.Value);
            if (_pending.Count == 0) return;
            var l = _pending.ToList();
            _pending.Clear();
            foreach (var kv in l) if (G.Ok(kv.Key) && G.Ok(kv.Value)) Overreach(kv.Value, kv.Key);
        }

        public static float Integration(Kingdom k)
        {
            Dep d = Diplomacy.DepOf(k);
            return d == null ? 0f : G.Clamp(100f - d.Autonomy, 0f, 100f);
        }

        public static int State(Kingdom k)
        {
            if (Diplomacy.DepOf(k) == null) return -1;
            float i = Integration(k);
            return i < 25f ? 0 : i < 50f ? 1 : i < 75f ? 2 : 3;
        }

        public static string StateName(Kingdom k)
        {
            int s = State(k);
            return s < 0 ? "" : Txt.T("civ_vstate_" + s);
        }

        public static string Status(Kingdom k)
        {
            Kingdom lord = Diplomacy.Overlord(k);
            if (!G.Ok(lord)) return "";
            Clan a = k.getKingClan(), b = lord.getKingClan();
            if (a != null && a == b) return "hereditary_province";
            return State(k) >= 2 ? "province" : "vassal";
        }

        public static string BondName(Kingdom k)
        {
            Dep d = Diplomacy.DepOf(k);
            return d == null ? "" : Txt.T("civ_dep_" + d.Kind);
        }

        public static float TributeMult(Kingdom lord)
        {
            if (!G.Ok(lord) || !Gov.On("civ_gov_vassals")) return 1f;
            switch (Laws.Get(lord, "vassal_tribute"))
            {
                case "token_tribute": return 0.5f;
                case "heavy_tribute": return 1.6f;
            }
            return 1f;
        }

        public static void KingdomYear(Kingdom k, KProfile p, int yrs)
        {
            Dep d = Diplomacy.DepOf(k);
            Kingdom lord = Diplomacy.Overlord(k);
            if (d != null && G.Ok(lord)) AsVassal(k, lord, d, p, yrs);
            AsLord(k, p, yrs);
        }

        private static void AsVassal(Kingdom k, Kingdom lord, Dep d, KProfile p, int yrs)
        {
            float drift = 0f;
            switch (Laws.Get(lord, "vassal_policy"))
            {
                case "lenient_rule": drift += 1f; break;
                case "assimilate_rule": drift -= 1.5f; break;
                case "exploit_rule": drift += 1.2f; break;
            }
            switch (Laws.Get(lord, "vassal_tribute"))
            {
                case "token_tribute": drift -= 0.8f; break;
                case "heavy_tribute": drift += 1.5f; break;
            }
            if (Laws.Get(lord, "fealty") == "absolute_fealty") drift -= 0.5f;
            int ph = Statecraft.Phase(lord);
            if (ph == 3) drift += 3f;
            else if (ph == 2) drift += 1f;
            if (Status(k) == "hereditary_province") drift -= 1f;
            d.Autonomy = G.Clamp(d.Autonomy + drift * yrs, 0f, 100f);
            int st = State(k);
            int old = Store.Int(k.data, KState, st);
            if (old != st)
            {
                Store.Put(k.data, KState, st);
                Gov.Log("vassal_state", k, G.Name(k), G.Name(lord), StateName(k), lord);
            }
            if (Integration(k) >= 75f)
            {
                if (Store.Int(k.data, KLowY, 0) == 0) Store.Put(k.data, KLowY, G.Year);
            }
            else Store.Put(k.data, KLowY, 0);
            if (d.Autonomy >= 70f && !Plots.Has(k, "independence", k.data.id) && G.Chance(0.15f * yrs) && !Player.Is(k))
            {
                Plots.Start(k, "independence", k.data.id, lord.data.id, G.Range(2, 4), ph >= 2 ? "weak_lord" : "autonomy");
            }
        }

        private static void AsLord(Kingdom k, KProfile p, int yrs)
        {
            foreach (Kingdom v in Diplomacy.DependentsOf(k))
            {
                int since = Store.Int(v.data, KLowY, 0);
                if (since == 0 || G.Year - since < 10) continue;
                if (Plots.Has(k, "direct_rule", k.data.id) || Player.Is(k)) continue;
                if (G.Chance(0.2f * yrs)) Plots.Start(k, "direct_rule", k.data.id, v.data.id, 3, "integration");
            }
            if (Player.Is(k)) return;
            if (Statecraft.Dynastic(k) && p.Cities >= 5 && !Plots.HasAny(k, "appanage") && G.Chance(0.03f * yrs))
            {
                long royal = Parliament.RoyalId(k);
                if (royal != 0L && Relative(k, royal) != null) Plots.Start(k, "appanage", royal, k.data.id, 2, "royal_kin");
            }
            bool greedy = Laws.Get(k, "foreign") == "expansion" || Politics.Aggression(k) > 0.6f;
            if (p.Era >= 3 && Diplomacy.DepOf(k) == null && !Plots.HasAny(k, "demand_tribute") && G.Chance((greedy ? 0.08f : 0.03f) * yrs))
            {
                Kingdom t = Victim(k, p);
                if (t != null) Plots.Start(k, "demand_tribute", k.data.id, t.data.id, 2, greedy ? "expansion" : "strength");
            }
        }

        private static Kingdom Victim(Kingdom k, KProfile p)
        {
            if (k.capital == null || k.capital.neighbours_kingdoms == null) return null;
            var near = new HashSet<Kingdom>();
            foreach (City c in G.Cities(k)) if (c.neighbours_kingdoms != null) foreach (Kingdom o in c.neighbours_kingdoms) near.Add(o);
            return near.Where(o => G.Ok(o) && o != k && o.isCiv() && Diplomacy.DepOf(o) == null && Diplomacy.Overlord(k) != o && !World.world.wars.isInWarWith(k, o) && !Diplomacy.Forbids(k, o) && Profile.Get(o).Strength * 3f < p.Strength).OrderBy(o => Profile.Get(o).Strength).FirstOrDefault();
        }

        private static Actor Relative(Kingdom k, long clan)
        {
            Clan c = Gov.ClanById(clan);
            if (c == null) return null;
            foreach (Actor a in c.units)
            {
                if (G.Ok(a) && a.kingdom == k && a.isAdult() && !a.isKing() && !a.isCityLeader()) return a;
            }
            return null;
        }

        public static void Independence(Kingdom k, string motive)
        {
            Kingdom lord = Diplomacy.Overlord(k);
            if (!G.Ok(lord)) return;
            Dep d = Diplomacy.DepOf(k);
            if (d == null || d.Autonomy < 55f) return;
            Diplomacy.Release(k, "plot");
            Gov.Log("vassal_revolt", k, G.Name(k), G.Name(lord), Txt.T("civ_motive_" + motive), lord);
            if (Profile.Get(lord).Strength > Profile.Get(k).Strength * 1.2f && !Player.Is(lord) && G.Chance(0.7f)) War(lord, k, "subjugation");
        }

        public static void DemandTribute(Kingdom k, Kingdom t)
        {
            if (!G.Ok(k) || !G.Ok(t) || Diplomacy.DepOf(t) != null || World.world.wars.isInWarWith(k, t)) return;
            float ratio = Profile.Get(t).Strength / Math.Max(1f, Profile.Get(k).Strength);
            bool yields = Player.Is(t) ? false : G.Chance(G.Clamp(0.9f - ratio * 2f, 0.1f, 0.85f));
            if (yields)
            {
                Diplomacy.MakeDependent(t, k, "tribute_demand");
                Gov.Log("tribute_demand", k, G.Name(k), G.Name(t), null, t);
                return;
            }
            Gov.Log("tribute_refused", k, G.Name(t), G.Name(k), null, t);
            War(k, t, "tribute");
        }

        public static global::War War(Kingdom a, Kingdom b, string goal)
        {
            if (!G.Ok(a) || !G.Ok(b) || World.world.wars.isInWarWith(a, b)) return null;
            global::War w;
            Statecraft.Escalating = true;
            try
            {
                w = World.world.diplomacy.startWar(a, b, WarTypeLibrary.normal);
            }
            finally
            {
                Statecraft.Escalating = false;
            }
            if (w != null && goal != null) WarGoals.Set(w, goal);
            return w;
        }

        public static void Appanage(Kingdom k, long clan)
        {
            if (!G.Ok(k) || k.capital == null) return;
            Actor a = Relative(k, clan);
            if (a == null) return;
            City c = G.Cities(k).Where(x => G.Ok(x) && x != k.capital).OrderByDescending(x => G.Dist(x, k.capital)).FirstOrDefault();
            if (c == null) return;
            Kingdom nk = ReleaseAsVassal(c, k, a);
            if (nk != null) Gov.Log("appanage", k, G.Name(k), a.getName(), G.Name(c), nk, c);
        }

        public static void DirectRule(Kingdom k, Kingdom v)
        {
            if (!G.Ok(k) || !G.Ok(v) || Diplomacy.Overlord(v) != k || Integration(v) < 70f) return;
            Annex(k, v);
        }

        public static void Annex(Kingdom k, Kingdom v)
        {
            if (!G.Ok(k) || !G.Ok(v) || k == v) return;
            if (Diplomacy.DepOf(v) != null) Diplomacy.Release(v, "direct_rule");
            _busy = true;
            try
            {
                foreach (City c in G.Cities(v).ToList()) c.joinAnotherKingdom(k, false, false);
            }
            finally
            {
                _busy = false;
            }
            Parliament.Invalidate(k);
            Gov.Log("vassal_annexed", k, G.Name(k), G.Name(v), null, v);
            Hub.Raise(Hub.Integrated, k, v, k.capital, null, "direct_rule");
        }

        public static Kingdom ReleaseAsVassal(City c, Kingdom lord, Actor ruler = null)
        {
            if (!G.Ok(c) || !G.Ok(lord) || c == lord.capital) return null;
            Actor a = ruler ?? Gov.Somebody(c);
            if (a == null) return null;
            Kingdom nk;
            _busy = true;
            try
            {
                if (a.city != c && a.city != null && a.isCityLeader()) a.city.removeLeader();
                nk = c.makeOwnKingdom(a, false, false);
            }
            finally
            {
                _busy = false;
            }
            if (!G.Ok(nk)) return null;
            foreach (global::War w in nk.getWars().ToList()) if (w.isAttacker(lord) || w.isDefender(lord)) World.world.wars.endWar(w, WarWinner.Peace);
            Diplomacy.MakeDependent(nk, lord, "release");
            Parliament.Invalidate(lord);
            return nk;
        }

        private static void Overreach(Kingdom k, City c)
        {
            if (Player.Is(k) || Profile.Get(k).Era < 3 || !G.Ok(c) || c.kingdom != k) return;
            if (k.countCities() <= k.getMaxCities() + 1) return;
            if (!G.Chance(0.5f)) return;
            ReleaseAsVassal(c, k);
        }

        private static void GrantRebel(Kingdom rebel, Kingdom old)
        {
            if (Player.Is(old) || Store.Str(rebel.data, Mandate.KRebelOf).Length > 0) return;
            if (rebel.countCities() > 1) return;
            if (Profile.Get(old).Strength < Profile.Get(rebel).Strength * 3f || !G.Chance(0.35f)) return;
            foreach (global::War w in rebel.getWars().ToList()) if (w.isAttacker(old) || w.isDefender(old)) World.world.wars.endWar(w, WarWinner.Peace);
            Diplomacy.MakeDependent(rebel, old, "granted");
        }

        public static void Make(Kingdom vassal, Kingdom lord)
        {
            Diplomacy.MakeDependent(vassal, lord, "sandbox");
        }

        public static void Incite(Kingdom vassal)
        {
            Dep d = Diplomacy.DepOf(vassal);
            if (d == null) return;
            d.Autonomy = Math.Max(d.Autonomy, 85f);
            Independence(vassal, "incited");
        }
    }
}
