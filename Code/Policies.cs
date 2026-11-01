using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Civilizations
{
    internal static class Policies
    {
        public const string Prefix = "civ_";
        public static readonly string[] Groups = { "gov", "ideo", "econ", "xeno", "transport" };
        private static readonly Dictionary<string, string> GroupColors = new Dictionary<string, string>
        {
            { "gov", "#FFD27A" }, { "ideo", "#FF9AA8" }, { "econ", "#9BE07A" }, { "xeno", "#9AD0FF" }, { "transport", "#D9B98A" }
        };
        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "gov", "tribal_band" }, { "ideo", "traditionalism" }, { "econ", "reciprocity" }, { "xeno", "xeno_neutral" }, { "transport", "balanced_transport" }
        };

        public static string TraitId(string policy)
        {
            return Prefix + policy;
        }

        public static void InitAssets()
        {
            int order = 0;
            foreach (string g in Groups)
            {
                string gid = "civ_" + g;
                if (AssetManager.kingdoms_traits_groups.get(gid) == null)
                {
                    AssetManager.kingdoms_traits_groups.add(new KingdomTraitGroupAsset { id = gid, name = "civ_pg_" + g, color = GroupColors[g] });
                }
            }
            var created = new List<KingdomTrait>();
            foreach (PolicyDef p in Db.Policies)
            {
                string tid = TraitId(p.Id);
                KingdomTrait t = AssetManager.kingdoms_traits.get(tid);
                if (t == null)
                {
                    t = new KingdomTrait
                    {
                        id = tid,
                        group_id = "civ_" + p.Group,
                        spawn_random_trait_allowed = false,
                        needs_to_be_explored = false,
                        special_locale_id = "civ_p_" + p.Id,
                        special_locale_description = "civ_p_" + p.Id + "_d",
                        special_locale_description_2 = "civ_p_" + p.Id + "_d2",
                        path_icon = "ui/Icons/civ/policy/" + p.Id,
                        priority = 1000 - order,
                        base_stats = new BaseStats(),
                        can_be_in_book = false
                    };
                    t.action_on_augmentation_add = OnChange;
                    t.action_on_augmentation_remove = OnChange;
                    AssetManager.kingdoms_traits.add(t);
                }
                order++;
                created.Add(t);
            }
            foreach (KingdomTrait t in created)
            {
                string group = t.group_id;
                t.opposite_list = new List<string>();
                t.opposite_traits = new HashSet<KingdomTrait>();
                foreach (KingdomTrait o in created)
                {
                    if (o == t || o.group_id != group) continue;
                    t.opposite_list.Add(o.id);
                    t.opposite_traits.Add(o);
                }
            }
        }

        private static bool OnChange(NanoObject target, BaseAugmentationAsset trait)
        {
            Kingdom k = target as Kingdom;
            if (k != null) Bonus.Dirty(k);
            return true;
        }

        public static PolicyDef Get(Kingdom k, string group)
        {
            if (k == null || k.data == null) return Db.Policy(Defaults[group]);
            try
            {
                foreach (KingdomTrait t in k.getTraits())
                {
                    if (t == null || t.group_id != "civ_" + group) continue;
                    PolicyDef p = Db.Policy(t.id.Substring(Prefix.Length));
                    if (p != null) return p;
                }
            }
            catch (Exception)
            {
            }
            return Db.Policy(Defaults[group]);
        }

        public static IEnumerable<PolicyDef> All(Kingdom k)
        {
            foreach (string g in Groups) yield return Get(k, g);
        }

        public static string Id(Kingdom k, string group)
        {
            PolicyDef p = Get(k, group);
            return p == null ? Defaults[group] : p.Id;
        }

        public static bool HasExplicit(Kingdom k, string group)
        {
            if (k == null) return false;
            foreach (KingdomTrait t in k.getTraits()) if (t != null && t.group_id == "civ_" + group) return true;
            return false;
        }

        public static void Set(Kingdom k, string group, string id, string reason)
        {
            if (!G.Ok(k)) return;
            PolicyDef p = Db.Policy(id);
            if (p == null || p.Group != group) return;
            PolicyDef old = Get(k, group);
            if (old != null && old.Id == id && HasExplicit(k, group)) return;
            k.addTrait(TraitId(id), true);
            Bonus.Dirty(k);
            if (old != null && old.Id != id && reason != "init") Internet.OnPolicy(k, group, old.Id, id, reason);
        }

        public static bool Unlocked(Kingdom k, PolicyDef p)
        {
            if (p == null) return false;
            if (!string.IsNullOrEmpty(p.Req) && !Research.Has(k, p.Req)) return false;
            if (p.Group == "gov" && Politics.StageIndex(k) < p.Stage) return false;
            return true;
        }

        public static void Ensure(Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            foreach (string g in Groups)
            {
                if (HasExplicit(k, g)) continue;
                string id = Defaults[g];
                if (g == "xeno") id = InitialXeno(k);
                if (g == "gov") id = BestGov(k) ?? id;
                Set(k, g, id, "init");
            }
        }

        private static string InitialXeno(Kingdom k)
        {
            Culture c = k.getCulture();
            if (c != null)
            {
                if (c.hasTrait("xenophobic")) return "xenophobia";
                if (c.hasTrait("xenophiles")) return "xenophilia";
            }
            float r = UnityEngine.Random.value;
            return r < 0.3f ? "xenophobia" : r < 0.8f ? "xeno_neutral" : "xenophilia";
        }

        private static string BestGov(Kingdom k)
        {
            string best = null;
            int bestStage = -1;
            foreach (PolicyDef p in Db.Group("gov"))
            {
                if (!Unlocked(k, p)) continue;
                if (p.Stage > bestStage || (p.Stage == bestStage && G.Chance(0.4f)))
                {
                    best = p.Id;
                    bestStage = p.Stage;
                }
            }
            return best;
        }

        public static void OnTech(Kingdom k, TechDef t)
        {
            foreach (string u in t.Unl)
            {
                if (u.StartsWith("gov:") || u.StartsWith("ideo:") || u.StartsWith("econ:"))
                {
                    Politics.ConsiderNewPolicy(k, u.Substring(u.IndexOf(':') + 1));
                }
            }
            if (t.Id == "railways" && Id(k, "transport") == "balanced_transport") Politics.ChooseTransport(k);
        }

        public static List<PolicyDef> Options(Kingdom k, string group)
        {
            var l = new List<PolicyDef>();
            foreach (PolicyDef p in Db.Group(group)) if (Unlocked(k, p)) l.Add(p);
            return l;
        }

        [HarmonyPatch(typeof(KingdomManager), nameof(KingdomManager.makeNewCivKingdom))]
        private static class NewKingdom
        {
            private static void Postfix(Kingdom __result, Actor pActor)
            {
                CivMod.Safe("policies-new", () =>
                {
                    if (__result == null) return;
                    Ensure(__result);
                });
            }
        }
    }
}
