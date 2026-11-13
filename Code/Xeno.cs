using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Civilizations
{
    internal static class Xeno
    {
        public const string KStatus = "civ_status";
        public const string Citizen = "";
        public const string Servant = "servant";
        public const string Slave = "slave";
        public const string KAbolished = "civ_abol";

        public static void InitAssets()
        {
            if (AssetManager.trait_groups.get("civ_status") == null)
            {
                AssetManager.trait_groups.add(new ActorTraitGroupAsset { id = "civ_status", name = "civ_trait_group_status", color = "#C9A27A" });
            }
            AddTrait("civ_status_servant");
            AddTrait("civ_status_slave");
        }

        private static void AddTrait(string id)
        {
            if (AssetManager.traits.get(id) != null) return;
            var t = new ActorTrait
            {
                id = id,
                group_id = "civ_status",
                path_icon = "ui/Icons/civ/status/" + id.Substring(11),
                spawn_random_trait_allowed = false,
                needs_to_be_explored = false,
                can_be_given = true,
                can_be_removed = true,
                base_stats = new BaseStats(),
                special_locale_id = "civ_trait_" + id,
                special_locale_description = "civ_trait_" + id + "_d",
                special_locale_description_2 = "civ_trait_" + id + "_d2",
                can_be_in_book = false
            };
            if (id.EndsWith("slave"))
            {
                t.base_stats["damage"] = -6f;
                t.base_stats["diplomacy"] = -20f;
            }
            AssetManager.traits.add(t);
        }

        public static string Status(Actor a)
        {
            if (!G.Ok(a)) return Citizen;
            return Store.Str(a.data, KStatus);
        }

        public static void SetStatus(Actor a, string st)
        {
            if (!G.Ok(a)) return;
            string old = Status(a);
            if (old == st) return;
            Store.Put(a.data, KStatus, st);
            try
            {
                a.removeTrait("civ_status_servant");
                a.removeTrait("civ_status_slave");
                if (st == Servant) a.addTrait("civ_status_servant");
                else if (st == Slave) a.addTrait("civ_status_slave");
            }
            catch (Exception)
            {
            }
            if (st != Citizen && a.isCityLeader() && a.city != null) a.city.removeLeader();
        }

        public static bool CanVote(Actor a)
        {
            string st = Status(a);
            if (st == Slave) return false;
            if (st == Servant && a.kingdom != null && Policies.Id(a.kingdom, "xeno") != "xenophilia") return false;
            return true;
        }

        public static bool CanHoldOffice(Actor a)
        {
            return Status(a) == Citizen;
        }

        public static bool Foreign(Actor a, Kingdom k)
        {
            if (!G.Ok(a) || k == null) return false;
            try
            {
                return a.asset.id != k.getSpecies();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static float ForeignShare(City c)
        {
            if (!G.Ok(c) || c.kingdom == null) return 0f;
            int n = 0, f = 0;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !a.isSapient()) continue;
                n++;
                if (Foreign(a, c.kingdom)) f++;
            }
            return n == 0 ? 0f : (float)f / n;
        }

        public static float SlaveShare(City c)
        {
            if (!G.Ok(c)) return 0f;
            int n = 0, s = 0;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !a.isSapient()) continue;
                n++;
                if (Status(a) == Slave) s++;
            }
            return n == 0 ? 0f : (float)s / n;
        }

        public static float ServantShare(City c)
        {
            if (!G.Ok(c)) return 0f;
            int n = 0, s = 0;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !a.isSapient()) continue;
                n++;
                if (Status(a) == Servant) s++;
            }
            return n == 0 ? 0f : (float)s / n;
        }

        public static float Unrest(Kingdom k)
        {
            float t = 0f;
            int n = 0;
            foreach (City c in G.Cities(k))
            {
                t += SlaveShare(c) * 0.6f + ServantShare(c) * 0.2f;
                n++;
            }
            if (n == 0) return 0f;
            float u = t / n;
            if (Research.HasFeature(k, "abolition")) u *= 1.5f;
            return G.Clamp(u * 0.3f, 0f, 0.25f);
        }

        public static string StatusFor(Kingdom k, Actor a)
        {
            string x = Policies.Id(k, "xeno");
            bool bondage = Research.HasFeature(k, "servitude") && !Store.Bool(k.data, KAbolished);
            if (x == "xenophilia") return Citizen;
            if (x == "xenophobia") return bondage && G.Chance(0.7f) ? Slave : Servant;
            if (bondage && G.Chance(0.2f)) return Slave;
            return G.Chance(0.6f) ? Servant : Citizen;
        }

        public static void OnConquest(City c, Kingdom conqueror, Kingdom loser)
        {
            if (!G.Ok(c) || conqueror == null || !conqueror.isCiv()) return;
            int changed = 0;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !a.isSapient()) continue;
                if (!Foreign(a, conqueror))
                {
                    if (Status(a) != Citizen) SetStatus(a, Citizen);
                    continue;
                }
                string st = StatusFor(conqueror, a);
                SetStatus(a, st);
                changed++;
            }
            if (changed > 0) Internet.OnSubjugation(c, conqueror, loser, Policies.Id(conqueror, "xeno"), changed);
        }

        public static void CityYear(City c, int yrs)
        {
            Kingdom k = c.kingdom;
            if (k == null || !k.isCiv()) return;
            string x = Policies.Id(k, "xeno");
            bool abolished = Store.Bool(k.data, KAbolished);
            Actor rebel = null;
            int slaves = 0;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !a.isSapient()) continue;
                string st = Status(a);
                bool foreign = Foreign(a, k);
                if (!foreign && st != Citizen && (x != "xenophobia" || G.Chance(0.2f * yrs)))
                {
                    SetStatus(a, Citizen);
                    continue;
                }
                if (st == Citizen && foreign && !a.isAdult() && Store.Str(a.data, "civ_status_set").Length == 0)
                {
                    Store.Put(a.data, "civ_status_set", "1");
                    if (x == "xenophobia") SetStatus(a, abolished ? Servant : (G.Chance(0.5f) ? Slave : Servant));
                }
                if (st == Slave)
                {
                    slaves++;
                    if (abolished || x == "xenophilia") SetStatus(a, Servant);
                    else if (rebel == null && a.isAdult() && G.Chance(0.3f)) rebel = a;
                }
                else if (st == Servant)
                {
                    float p = x == "xenophilia" ? 0.25f : x == "xeno_neutral" ? 0.06f : 0.01f;
                    if (G.Chance(p * yrs)) SetStatus(a, Citizen);
                }
            }
            if (!abolished && Research.HasFeature(k, "abolition"))
            {
                string ideo = Policies.Id(k, "ideo");
                bool push = ideo == "humanism" || ideo == "liberalism" || ideo == "socialism" || ideo == "communism" || ideo == "anarchism" || x == "xenophilia";
                if (push && G.Chance(0.05f * yrs) || G.Chance(0.004f * yrs))
                {
                    Store.Put(k.data, KAbolished, true);
                    Internet.OnAbolition(k);
                    Chronicle.Log("abolition", k, G.Name(k));
                }
            }
            float share = SlaveShare(c);
            if (rebel != null && share > 0.3f && Politics.Stability(k) < 0.45f && World.world.world_laws.isEnabled("world_law_rebellions") && G.Chance(0.06f * yrs))
            {
                Revolt(c, rebel);
            }
        }

        private static void Revolt(City c, Actor leader)
        {
            Kingdom old = c.kingdom;
            try
            {
                if (c.leader != null && c.leader != leader) c.removeLeader();
                SetStatus(leader, Citizen);
                c.setLeader(leader, true);
                Kingdom nk = c.makeOwnKingdom(leader, true);
                if (nk == null) return;
                foreach (Actor a in c.units)
                {
                    if (G.Ok(a) && Status(a) != Citizen) SetStatus(a, Citizen);
                }
                Policies.Set(nk, "xeno", "xenophilia", "init");
                Store.Put(nk.data, KAbolished, true);
                Internet.OnSlaveRevolt(c, old, nk, leader);
                Chronicle.Log("slave_revolt", old, G.Name(old), G.Name(c), null, nk, null, c, leader);
            }
            catch (Exception e)
            {
                CivMod.Log("revolt failed " + e.Message);
            }
        }

        [HarmonyPatch(typeof(City), nameof(City.joinAnotherKingdom))]
        private static class Joined
        {
            private static void Prefix(City __instance, out Kingdom __state)
            {
                __state = __instance.kingdom;
            }

            private static void Postfix(City __instance, Kingdom pNewSetKingdom, bool pCaptured, Kingdom __state)
            {
                if (!pCaptured) return;
                CivMod.Safe("xeno-capture", () => OnConquest(__instance, pNewSetKingdom, __state));
            }
        }

        [HarmonyPatch(typeof(BaseSimObject), nameof(BaseSimObject.canAttackTarget))]
        private static class SpareCivilians
        {
            private static void Postfix(BaseSimObject __instance, BaseSimObject pTarget, ref bool __result)
            {
                if (!__result || pTarget == null || !__instance.isActor() || !pTarget.isActor()) return;
                Actor att = __instance.a;
                Actor tgt = pTarget.a;
                if (att.kingdom == null || tgt.kingdom == null || !att.kingdom.isCiv() || !tgt.kingdom.isCiv()) return;
                if (att.kingdom == tgt.kingdom) return;
                if (tgt.isWarrior() || tgt.isKing() || tgt.has_attack_target || Vehicles.IsVehicle(tgt)) return;
                if (!tgt.isSapient()) return;
                if (tgt.isCityLeader() && tgt.city != null && tgt.city.getPopulationPeople() < 4) return;
                __result = false;
            }
        }

        [HarmonyPatch(typeof(City), nameof(City.isWelcomedToJoin))]
        private static class Welcome
        {
            private static void Postfix(City __instance, Actor pActor, ref bool __result)
            {
                if (__instance == null || __instance.kingdom == null || !__instance.kingdom.isCiv() || pActor == null) return;
                string x = Policies.Id(__instance.kingdom, "xeno");
                if (x == "xenophilia" && pActor.isSapient()) __result = true;
                else if (x == "xenophobia" && Foreign(pActor, __instance.kingdom)) __result = false;
            }
        }
    }
}
