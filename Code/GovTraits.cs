using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class GovTraits
    {
        private static readonly string[] Regimes = { "feudal", "absolutist", "militarist", "confessional", "bureaucratic", "federal", "expansionist", "isolationist", "mercantile" };
        private static readonly Dictionary<string, string[]> Sets = new Dictionary<string, string[]>();
        private static bool _done;

        public static void InitAssets()
        {
            if (_done) return;
            _done = true;
            Group("civ_tg_phase", "#E8B860", Statecraft.Phases.Select(p => "civ_ph_" + p).ToArray(), "ui/Icons/civ/gov/phase_");
            Group("civ_tg_vassal", "#9AC8E8", new[] { "civ_vs_0", "civ_vs_1", "civ_vs_2", "civ_vs_3" }, "ui/Icons/civ/gov/vassal_");
            Group("civ_tg_regime", "#C8A0E8", Regimes.Select(r => "civ_rgt_" + r).ToArray(), "ui/Icons/civ/gov/regime_");
        }

        private static void Group(string gid, string color, string[] ids, string iconPrefix)
        {
            if (AssetManager.kingdoms_traits_groups.get(gid) == null) AssetManager.kingdoms_traits_groups.add(new KingdomTraitGroupAsset { id = gid, name = gid, color = color });
            var made = new List<KingdomTrait>();
            int i = 0;
            foreach (string id in ids)
            {
                KingdomTrait t = AssetManager.kingdoms_traits.get(id);
                if (t == null)
                {
                    string tail = id.Substring(id.LastIndexOf('_') + 1);
                    t = new KingdomTrait
                    {
                        id = id,
                        group_id = gid,
                        spawn_random_trait_allowed = false,
                        needs_to_be_explored = false,
                        special_locale_id = id,
                        special_locale_description = id + "_d",
                        special_locale_description_2 = id + "_d2",
                        path_icon = iconPrefix + tail,
                        priority = 900 - i,
                        base_stats = new BaseStats(),
                        can_be_in_book = false
                    };
                    AssetManager.kingdoms_traits.add(t);
                }
                made.Add(t);
                i++;
            }
            foreach (KingdomTrait t in made)
            {
                t.opposite_list = new List<string>();
                t.opposite_traits = new HashSet<KingdomTrait>();
                foreach (KingdomTrait o in made)
                {
                    if (o == t) continue;
                    t.opposite_list.Add(o.id);
                    t.opposite_traits.Add(o);
                }
            }
            Sets[gid] = ids;
        }

        private static void Want(Kingdom k, string gid, string id)
        {
            string[] ids;
            if (!Sets.TryGetValue(gid, out ids)) return;
            foreach (string o in ids) if (o != id && k.hasTrait(o)) k.removeTrait(o);
            if (id != null && !k.hasTrait(id)) k.addTrait(id, true);
        }

        public static void Sync(Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            int ph = Statecraft.Phase(k);
            Want(k, "civ_tg_phase", Gov.On("civ_gov_cycle") && ph >= 0 ? "civ_ph_" + Statecraft.Phases[ph] : null);
            int vs = Vassals.State(k);
            Want(k, "civ_tg_vassal", Gov.On("civ_gov_vassals") && vs >= 0 ? "civ_vs_" + vs : null);
            string adj = Laws.RegimeAdj(k);
            Want(k, "civ_tg_regime", adj != null ? "civ_rgt_" + adj : null);
        }
    }
}
