using HarmonyLib;

namespace Civilizations
{
    internal static class Aliens
    {
        public static bool IsAlien(Actor a)
        {
            if (a == null || a.asset == null) return false;
            if (a.asset.id == "alien") return true;
            ActorAsset s = a.subspecies != null ? a.subspecies.getActorAsset() : null;
            return s != null && s.id == "alien";
        }

        public static void Max(Kingdom k)
        {
            if (k == null || k.data == null) return;
            var set = new TechSet();
            foreach (TechDef t in Db.Techs) set.Add(t.Index);
            Research.SetAll(k, set);
            if (string.IsNullOrEmpty(Research.Route(k))) Research.SetRoute(k, "real");
        }

        [HarmonyPatch(typeof(KingdomManager), nameof(KingdomManager.makeNewCivKingdom))]
        private static class Founded
        {
            private static void Postfix(Actor pActor, Kingdom __result)
            {
                if (__result == null || !IsAlien(pActor)) return;
                CivMod.Safe("aliens", () => Max(__result));
            }
        }
    }
}
