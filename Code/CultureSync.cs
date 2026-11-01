using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class CultureSync
    {
        public const int MaxWeapons = 0;
        private const string KPol = "civ_xs_p";
        private const string KCul = "civ_xs_c";

        [HarmonyLib.HarmonyPatch(typeof(ai.behaviours.CityBehCheckDestruction), nameof(ai.behaviours.CityBehCheckDestruction.execute))]
        private static class NoRaze
        {
            private static bool Prefix(City pCity, ref ai.behaviours.BehResult __result)
            {
                if (pCity == null || !pCity.isGettingCaptured()) return true;
                Kingdom k = pCity.getCapturingKingdom();
                if (k == null || k.isRekt() || !k.isCiv()) return true;
                __result = ai.behaviours.BehResult.Continue;
                return false;
            }
        }

        [HarmonyLib.HarmonyPatch(typeof(Actor), nameof(Actor.hasXenophobic))]
        private static class NoXenoKilling
        {
            private static bool Prefix(Actor __instance, ref bool __result)
            {
                if (__instance == null || __instance.kingdom == null || !__instance.kingdom.isCiv()) return true;
                __result = false;
                return false;
            }
        }

        public static bool IsWeapon(string traitId)
        {
            CultureTrait t = AssetManager.culture_traits.get(traitId);
            return t != null && t.group_id == "weapons";
        }

        public static int WeaponCount(Culture c)
        {
            return c == null ? 0 : c.getTraits().Count(t => t != null && t.group_id == "weapons");
        }

        public static bool CanAddWeapon(Culture c)
        {
            return WeaponCount(c) < MaxWeapons;
        }

        public static void TrimWeapons(Culture c)
        {
            if (c == null) return;
            List<CultureTrait> w = c.getTraits().Where(t => t != null && t.group_id == "weapons").ToList();
            for (int i = MaxWeapons; i < w.Count; i++) c.removeTrait(w[i]);
        }

        private static string CultureState(Culture c)
        {
            if (c.hasTrait("xenophobic")) return "xenophobia";
            if (c.hasTrait("xenophiles")) return "xenophilia";
            return "xeno_neutral";
        }

        private static void ApplyToCulture(Culture c, string pol)
        {
            if (pol == "xenophobia")
            {
                c.removeTrait("xenophiles");
                c.addTrait("xenophobic", true);
            }
            else if (pol == "xenophilia")
            {
                c.removeTrait("xenophobic");
                c.addTrait("xenophiles", true);
            }
            else
            {
                c.removeTrait("xenophobic");
                c.removeTrait("xenophiles");
            }
        }

        public static void Year(Kingdom k)
        {
            if (!G.Ok(k) || k.data == null) return;
            Culture c = k.getCulture();
            if (c == null) return;
            TrimWeapons(c);
            string pol = Policies.Id(k, "xeno");
            if (string.IsNullOrEmpty(pol)) pol = "xeno_neutral";
            string cul = CultureState(c);
            string lastP = Store.Str(k.data, KPol);
            string lastC = Store.Str(k.data, KCul);
            if (pol != cul)
            {
                bool policyMoved = lastP.Length > 0 && pol != lastP;
                bool cultureMoved = lastC.Length > 0 && cul != lastC;
                if (cultureMoved && !policyMoved || lastP.Length == 0 && cul != "xeno_neutral")
                {
                    Policies.Set(k, "xeno", cul, "culture");
                    pol = Policies.Id(k, "xeno");
                }
                else
                {
                    ApplyToCulture(c, pol);
                    cul = CultureState(c);
                }
            }
            Store.Put(k.data, KPol, pol);
            Store.Put(k.data, KCul, cul);
        }
    }
}
