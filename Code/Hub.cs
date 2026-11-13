using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal class HubEvent
    {
        public string Type = "";
        public Kingdom K;
        public Kingdom O;
        public City C;
        public Actor A;
        public string Id = "";
        public string Arg = "";
        public float Value;
        public int Year;
    }

    internal static class Hub
    {
        public const string TechLearned = "tech_learned";
        public const string TechDormant = "tech_dormant";
        public const string TechLost = "tech_lost";
        public const string TechRecovered = "tech_recovered";
        public const string EraChanged = "era_changed";
        public const string EraDropped = "era_dropped";
        public const string PolicyChanged = "policy_changed";
        public const string WarStarted = "war_started";
        public const string WarEnded = "war_ended";
        public const string PeaceTerms = "peace_terms";
        public const string CityFounded = "city_founded";
        public const string CityCaptured = "city_captured";
        public const string KingdomFounded = "kingdom_founded";
        public const string KingdomGone = "kingdom_gone";
        public const string TreatySigned = "treaty_signed";
        public const string TreatyBroken = "treaty_broken";
        public const string Dependent = "dependent";
        public const string Independence = "independence";
        public const string Integrated = "integrated";
        public const string OrgFounded = "org_founded";
        public const string OrgJoined = "org_joined";
        public const string OrgLeft = "org_left";
        public const string OrgDissolved = "org_dissolved";
        public const string OrgVote = "org_vote";
        public const string WorldOrder = "world_order";
        public const string WorldOrderFell = "world_order_fell";
        public const string Erasure = "erasure";
        public const string Festival = "festival";
        public const string Event = "event";
        public const string GreatPerson = "great_person";
        public const string FlagReform = "flag_reform";
        public const string FirstContact = "first_contact";
        public const string Legitimacy = "legitimacy";
        public const string CivilWar = "civil_war";
        public const string Coup = "coup";
        public const string FirstInWorld = "world_first";
        public const string Fireworks = "fireworks";
        public const string NukeDetonated = "nuke_detonated";
        public const string Sanctions = "sanctions";

        private static readonly Dictionary<string, List<Action<HubEvent>>> _subs = new Dictionary<string, List<Action<HubEvent>>>();
        private static readonly List<Action<HubEvent>> _all = new List<Action<HubEvent>>();
        private static int _depth;

        public static void On(string type, Action<HubEvent> handler)
        {
            List<Action<HubEvent>> l;
            if (!_subs.TryGetValue(type, out l)) _subs[type] = l = new List<Action<HubEvent>>();
            if (!l.Contains(handler)) l.Add(handler);
        }

        public static void OnAny(Action<HubEvent> handler)
        {
            if (!_all.Contains(handler)) _all.Add(handler);
        }

        public static void Raise(string type, Kingdom k = null, Kingdom o = null, City c = null, Actor a = null, string id = "", string arg = "", float value = 0f)
        {
            Raise(new HubEvent { Type = type, K = k, O = o, C = c, A = a, Id = id ?? "", Arg = arg ?? "", Value = value, Year = G.Year });
        }

        public static void Raise(HubEvent e)
        {
            if (e == null || _depth > 4) return;
            _depth++;
            try
            {
                List<Action<HubEvent>> l;
                if (_subs.TryGetValue(e.Type, out l))
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        Action<HubEvent> h = l[i];
                        CivMod.Safe("hub-" + e.Type, () => h(e));
                    }
                }
                for (int i = 0; i < _all.Count; i++)
                {
                    Action<HubEvent> h = _all[i];
                    CivMod.Safe("hub-any", () => h(e));
                }
            }
            finally
            {
                _depth--;
            }
        }
    }
}
