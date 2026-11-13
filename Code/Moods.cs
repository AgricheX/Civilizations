using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class Moods
    {
        public static readonly Dictionary<string, int> Values = new Dictionary<string, int>
        {
            { "civ_famine", -15 },
            { "civ_pollution", -8 },
            { "civ_cold", -6 },
            { "civ_new_home", 8 },
            { "civ_lost_home", -12 },
            { "civ_viral", 12 },
            { "civ_cancelled", -25 },
            { "civ_liked", 3 },
            { "civ_disliked", -4 },
            { "civ_discovery", 6 },
            { "civ_nuke_fear", -20 },
            { "civ_radiation", -10 },
            { "civ_blackout", -6 },
            { "civ_enslaved", -20 },
            { "civ_freed", 18 },
            { "civ_election_won", 10 },
            { "civ_election_lost", -5 },
            { "civ_revolution", -8 },
            { "civ_space", 10 },
            { "civ_addicted", -3 },
            { "civ_new_clothes", 4 },
            { "civ_train_trip", 5 },
            { "civ_flight", 6 },
            { "civ_festival", 10 },
            { "civ_victory", 8 },
            { "civ_mourning", -6 },
            { "civ_erasure_fear", -20 },
            { "civ_civil_war", -12 },
            { "civ_independence", 12 },
            { "civ_dark_age", -8 },
            { "civ_golden_age", 8 },
            { "civ_plague", -12 },
            { "civ_disaster", -10 },
            { "civ_divine_wrath", -10 },
            { "civ_divine_gift", 8 }
        };

        public static void InitAssets()
        {
            HappinessLibrary lib = AssetManager.happiness_library;
            foreach (var kv in Values)
            {
                if (lib.get(kv.Key) != null) continue;
                var h = new HappinessAsset
                {
                    id = kv.Key,
                    value = kv.Value,
                    path_icon = "ui/Icons/civ/mood/" + kv.Key.Substring(4),
                    dialogs_amount = 2,
                    pot_task_id = kv.Value > 0 ? "happy_laughing" : "swearing",
                    pot_amount = 1,
                    ignored_by_psychopaths = kv.Value < 0,
                    show_change_happiness_effect = true
                };
                lib.add(h);
            }
            for (int i = 0; i < lib.list.Count; i++) lib.list[i].index = i;
        }

        public static void Apply(City c, string id, float share)
        {
            if (c == null || AssetManager.happiness_library.get(id) == null) return;
            foreach (Actor a in c.units)
            {
                if (!G.Ok(a) || !G.Chance(share)) continue;
                a.changeHappiness(id);
            }
        }

        public static void Apply(Actor a, string id)
        {
            if (!G.Ok(a) || AssetManager.happiness_library.get(id) == null) return;
            a.changeHappiness(id);
        }
    }
}
