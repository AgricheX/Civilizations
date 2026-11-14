using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal class LawDef
    {
        public string Id;
        public string Group;
        public float X;
        public float Y;
        public int MinEra;
        public Dictionary<string, float> Eff = new Dictionary<string, float>();
    }

    internal static class Laws
    {
        private const string KLaw = "civ_law_";
        private const string KLock = "civ_law_lock_";

        public static readonly string[] Groups = { "appointments", "land", "taxation", "fiscal", "culture", "levy", "naval", "foreign", "vassal_policy", "vassal_tribute", "fealty" };
        public static readonly List<LawDef> All = new List<LawDef>();
        private static readonly Dictionary<string, LawDef> _byId = new Dictionary<string, LawDef>();

        private static readonly Dictionary<string, float[]> PolicyPos = new Dictionary<string, float[]>
        {
            { "tribal_band", new[] { -0.6f, 0.8f } }, { "chiefdom", new[] { -0.6f, 0.2f } }, { "tribal_council", new[] { -0.3f, 0.6f } },
            { "monarchy", new[] { -0.5f, -0.4f } }, { "theocracy", new[] { -0.9f, -0.3f } }, { "republic", new[] { 0.5f, 0.2f } },
            { "democracy", new[] { 0.8f, 0.3f } }, { "feudal_monarchy", new[] { -0.6f, 0.9f } }, { "elective_monarchy", new[] { -0.1f, 0.5f } },
            { "absolute_monarchy", new[] { -0.7f, -0.9f } }, { "merchant_republic", new[] { 0.4f, 0.6f } }, { "constitutional_monarchy", new[] { 0.3f, -0.1f } },
            { "presidential_republic", new[] { 0.6f, -0.4f } }, { "parliamentary_republic", new[] { 0.7f, 0.2f } }, { "single_party_state", new[] { 0.2f, -0.9f } },
            { "military_junta", new[] { -0.4f, -0.8f } }, { "technocracy", new[] { 0.8f, -0.5f } }, { "corporatocracy", new[] { 0.4f, 0.4f } },
            { "planetary_federation", new[] { 0.6f, 0.8f } }, { "ai_technocracy", new[] { 0.9f, -0.7f } }, { "stellar_empire", new[] { -0.4f, -0.8f } },
            { "interstellar_republic", new[] { 0.7f, 0.4f } }, { "hive_mind", new[] { 0.3f, -1f } }, { "distributed_consciousness", new[] { 0.9f, 0.9f } },
            { "cosmic_theocracy", new[] { -0.9f, -0.4f } }, { "galactic_corporation", new[] { 0.5f, 0.5f } }, { "web_assembly", new[] { 0.8f, 0.7f } },
            { "omega_conclave", new[] { 0f, 0f } },
            { "traditionalism", new[] { -0.9f, 0.2f } }, { "religious_zeal", new[] { -0.8f, -0.2f } }, { "legalism", new[] { -0.2f, -0.8f } },
            { "militarism", new[] { -0.4f, -0.6f } }, { "humanism", new[] { 0.5f, 0.2f } }, { "liberalism", new[] { 0.8f, 0.5f } },
            { "conservatism", new[] { -0.6f, 0.1f } }, { "nationalism", new[] { -0.3f, -0.7f } }, { "socialism", new[] { 0.6f, -0.3f } },
            { "communism", new[] { 0.7f, -0.9f } }, { "anarchism", new[] { 0.9f, 1f } }, { "authoritarianism", new[] { -0.3f, -1f } },
            { "environmentalism", new[] { 0.6f, 0.6f } }, { "technoprogressivism", new[] { 0.9f, -0.2f } }, { "cosmism", new[] { 0.7f, 0f } },
            { "transhumanism", new[] { 1f, 0.1f } }, { "xenounity", new[] { 0.6f, 0.4f } }, { "emulationism", new[] { 0.8f, -0.4f } },
            { "void_faith", new[] { -0.7f, 0.3f } }, { "great_flow", new[] { 0.2f, 0.7f } }, { "hibernation_ethic", new[] { -0.5f, 0.5f } },
            { "reciprocity", new[] { -0.7f, 0.8f } }, { "palace_economy", new[] { -0.5f, -0.8f } }, { "tributary", new[] { -0.4f, -0.3f } },
            { "manorialism", new[] { -0.7f, 0.9f } }, { "guild_economy", new[] { -0.2f, 0.6f } }, { "mercantilism", new[] { -0.1f, -0.6f } },
            { "laissez_faire", new[] { 0.8f, 0.7f } }, { "planned", new[] { 0.4f, -1f } }, { "welfare", new[] { 0.6f, -0.3f } },
            { "mixed_economy", new[] { 0.3f, 0f } }, { "corporatist", new[] { 0f, -0.5f } }, { "digital_economy", new[] { 0.8f, 0.4f } },
            { "post_scarcity", new[] { 1f, 0.2f } }, { "galactic_market", new[] { 0.7f, 0.6f } }, { "asteroid_economy", new[] { 0.3f, 0.8f } },
            { "stellar_energy_economy", new[] { 0.4f, -0.4f } }, { "dark_matter_economy", new[] { 0.5f, -0.2f } }, { "filament_commons", new[] { 0.8f, 0.8f } },
            { "xenophobia", new[] { -0.8f, -0.3f } }, { "xeno_neutral", new[] { 0f, 0f } }, { "xenophilia", new[] { 0.8f, 0.3f } },
            { "road_first", new[] { -0.3f, 0.5f } }, { "balanced_transport", new[] { 0f, 0f } }, { "rail_first", new[] { 0.4f, -0.5f } }
        };

        static Laws()
        {
            Def("hereditary_offices", "appointments", -0.6f, 0.6f, 0, "loyalty", 0.02f);
            Def("royal_appointment", "appointments", -0.4f, -0.8f, 3, "loyalty", -0.01f, "gold", 0.03f);
            Def("merit_exams", "appointments", 0.5f, -0.4f, 6, "research", 0.04f, "gold", 0.02f);
            Def("elected_officials", "appointments", 0.7f, 0.5f, 9, "loyalty", 0.03f, "prod", -0.01f);
            Def("fiefs", "land", -0.6f, 0.9f, 3, "defense", 0.04f, "gold", -0.03f);
            Def("crown_lands", "land", -0.3f, -0.8f, 3, "gold", 0.05f, "loyalty", -0.02f);
            Def("free_holdings", "land", 0.5f, 0.4f, 0, "prod", 0.04f);
            Def("state_lands", "land", 0.4f, -0.7f, 10, "prod", 0.03f, "gold", 0.02f, "loyalty", -0.02f);
            Def("light_tax", "taxation", -0.4f, 0.6f, 0, "loyalty", 0.03f, "gold", -0.04f);
            Def("flat_tax", "taxation", 0f, 0f, 3, "gold", 0.02f);
            Def("progressive_tax", "taxation", 0.7f, -0.2f, 10, "gold", 0.04f, "loyalty", 0.01f);
            Def("heavy_tax", "taxation", 0.2f, -0.8f, 3, "gold", 0.08f, "loyalty", -0.05f);
            Def("army_first", "fiscal", -0.3f, -0.6f, 0, "attack", 0.05f, "prod", -0.02f);
            Def("court_first", "fiscal", -0.6f, -0.2f, 3, "loyalty", 0.02f);
            Def("works_first", "fiscal", 0.5f, 0.3f, 3, "prod", 0.04f, "build", 0.05f);
            Def("reserve_first", "fiscal", 0.1f, -0.3f, 3, "gold", 0.03f);
            Def("assimilation", "culture", -0.4f, -0.7f, 3, "loyalty", 0.02f);
            Def("tolerance", "culture", 0.6f, 0.5f, 3, "trade", 0.04f, "loyalty", 0.01f);
            Def("state_faith", "culture", -0.8f, -0.3f, 3, "loyalty", 0.03f, "research", -0.02f);
            Def("free_thought", "culture", 0.8f, 0.2f, 9, "research", 0.05f, "loyalty", -0.01f);
            Def("light_levy", "levy", 0.4f, 0.5f, 0, "prod", 0.03f, "attack", -0.03f);
            Def("standard_levy", "levy", 0f, 0f, 0);
            Def("heavy_levy", "levy", -0.4f, -0.5f, 3, "attack", 0.05f, "prod", -0.03f);
            Def("total_levy", "levy", -0.2f, -0.9f, 10, "attack", 0.1f, "prod", -0.08f, "loyalty", -0.03f);
            Def("no_navy", "naval", -0.3f, 0.4f, 0);
            Def("coastal_guard", "naval", -0.2f, 0.2f, 3, "naval", 0.04f, "defense", 0.02f);
            Def("merchant_escort", "naval", 0.5f, 0.4f, 6, "trade", 0.05f, "naval", 0.02f);
            Def("blue_water", "naval", 0.1f, -0.6f, 8, "naval", 0.1f, "gold", -0.03f);
            Def("isolation", "foreign", -0.8f, 0.3f, 0, "defense", 0.04f, "trade", -0.05f);
            Def("balance", "foreign", 0.2f, 0f, 3);
            Def("expansion", "foreign", -0.3f, -0.6f, 3, "attack", 0.04f, "loyalty", -0.02f);
            Def("concert", "foreign", 0.6f, 0.4f, 6, "trade", 0.04f);
            Def("lenient_rule", "vassal_policy", 0.4f, 0.8f, 3);
            Def("firm_rule", "vassal_policy", -0.2f, -0.3f, 3);
            Def("assimilate_rule", "vassal_policy", -0.4f, -0.9f, 6);
            Def("exploit_rule", "vassal_policy", -0.5f, -0.6f, 6, "gold", 0.03f);
            Def("token_tribute", "vassal_tribute", 0.4f, 0.7f, 3);
            Def("fair_tribute", "vassal_tribute", 0f, 0f, 3);
            Def("heavy_tribute", "vassal_tribute", -0.4f, -0.6f, 3, "gold", 0.03f);
            Def("loose_fealty", "fealty", 0.3f, 0.9f, 0, "loyalty", 0.02f, "defense", -0.02f);
            Def("sworn_fealty", "fealty", -0.3f, 0.1f, 3);
            Def("absolute_fealty", "fealty", -0.7f, -0.9f, 6, "loyalty", -0.02f, "gold", 0.03f, "defense", 0.03f);
        }

        private static void Def(string id, string group, float x, float y, int era, params object[] eff)
        {
            var d = new LawDef { Id = id, Group = group, X = x, Y = y, MinEra = era };
            for (int i = 0; i + 1 < eff.Length; i += 2) d.Eff[(string)eff[i]] = Convert.ToSingle(eff[i + 1]);
            All.Add(d);
            _byId[id] = d;
        }

        public static LawDef Law(string id)
        {
            LawDef d;
            return id != null && _byId.TryGetValue(id, out d) ? d : null;
        }

        public static bool IsLawGroup(string g)
        {
            return Array.IndexOf(Groups, g) >= 0;
        }

        public static List<LawDef> Options(Kingdom k, string group)
        {
            int era = G.Ok(k) ? Profile.Get(k).Era : 0;
            return All.Where(d => d.Group == group && d.MinEra <= era).ToList();
        }

        public static string Default(Kingdom k, string group)
        {
            int band = Gov.Band(k);
            KProfile p = Profile.Get(k);
            switch (group)
            {
                case "appointments": return band <= 1 ? "hereditary_offices" : p.Fam.Contains("democratic") && band >= 3 ? "elected_officials" : band >= 3 ? "merit_exams" : "royal_appointment";
                case "land": return band == 0 ? "free_holdings" : band <= 2 ? (p.Fam.Contains("monarchy") ? "fiefs" : "crown_lands") : "free_holdings";
                case "taxation": return band == 0 ? "light_tax" : band >= 3 ? "progressive_tax" : "flat_tax";
                case "fiscal": return band == 0 ? "army_first" : "court_first";
                case "culture": return band == 0 ? "tolerance" : p.Fam.Contains("theocratic") ? "state_faith" : band >= 3 ? "tolerance" : "assimilation";
                case "levy": return "standard_levy";
                case "naval": return p.Coastal && band >= 1 ? "coastal_guard" : "no_navy";
                case "foreign": return band == 0 ? "isolation" : "balance";
                case "vassal_policy": return "firm_rule";
                case "vassal_tribute": return "fair_tribute";
                case "fealty": return band <= 2 ? "sworn_fealty" : "loose_fealty";
            }
            return "";
        }

        public static string Get(Kingdom k, string group)
        {
            if (!G.Ok(k)) return "";
            string v = Store.Str(k.data, KLaw + group);
            LawDef d = Law(v);
            if (d != null && d.MinEra <= Profile.Get(k).Era) return v;
            return Default(k, group);
        }

        public static bool Locked(Kingdom k, string group)
        {
            return G.Ok(k) && Store.Str(k.data, KLock + group).Length > 0;
        }

        public static void Unlock(Kingdom k, string group)
        {
            if (G.Ok(k)) Store.Put(k.data, KLock + group, "");
        }

        public static bool Set(Kingdom k, string group, string id, string reason)
        {
            if (!G.Ok(k)) return false;
            LawDef d = Law(id);
            if (d == null || d.Group != group) return false;
            bool own = reason == "player" || reason == "sandbox";
            if (!own && Locked(k, group)) return false;
            string old = Get(k, group);
            Store.Put(k.data, KLaw + group, id);
            if (own) Store.Put(k.data, KLock + group, id);
            if (old != id) Bonus.Dirty(k);
            return true;
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!G.Ok(k) || !k.isCiv() || !Gov.On("civ_gov_parliament")) return fx;
            foreach (string g in Groups)
            {
                LawDef d = Law(Get(k, g));
                if (d == null) continue;
                foreach (var kv in d.Eff) Store.Add(fx, kv.Key, kv.Value);
            }
            return fx;
        }

        public static float[] Pos(string group, string id)
        {
            if (IsLawGroup(group))
            {
                LawDef d = Law(id);
                return d != null ? new[] { d.X, d.Y } : new[] { 0f, 0f };
            }
            float[] p;
            return id != null && PolicyPos.TryGetValue(id, out p) ? p : new[] { 0f, 0f };
        }

        public static string Current(Kingdom k, string group)
        {
            return IsLawGroup(group) ? Get(k, group) : Policies.Id(k, group);
        }

        public static string Name(string group, string id)
        {
            return Txt.T(IsLawGroup(group) ? "civ_law_" + id : "civ_p_" + id);
        }

        public static string GroupName(string group)
        {
            return Txt.T(IsLawGroup(group) ? "civ_lg_" + group : "civ_pg_" + group);
        }

        public static bool GroupLocked(Kingdom k, string group)
        {
            return IsLawGroup(group) ? Locked(k, group) : Policies.Locked(k, group);
        }

        public static IEnumerable<string> Choices(Kingdom k, string group)
        {
            if (IsLawGroup(group))
            {
                foreach (LawDef d in Options(k, group)) yield return d.Id;
                yield break;
            }
            int era = Research.Era(k);
            foreach (PolicyDef p in Policies.Options(k, group))
            {
                if (Coherence.IsObsolete(p.Id, era)) continue;
                yield return p.Id;
            }
        }

        public static void Apply(Kingdom k, string group, string id, string reason)
        {
            if (IsLawGroup(group)) Set(k, group, id, reason);
            else Policies.Set(k, group, id, reason);
        }

        public static IEnumerable<string> AllGroups()
        {
            foreach (string g in Policies.Groups) yield return g;
            foreach (string g in Groups) yield return g;
        }

        public static string Regime(Kingdom k)
        {
            if (!G.Ok(k)) return "";
            string gov = Txt.T("civ_p_" + Policies.Id(k, "gov"));
            string adj = RegimeAdj(k);
            return adj == null ? gov : Txt.F("civ_rg_" + adj, gov);
        }

        public static string RegimeAdj(Kingdom k)
        {
            if (!Gov.On("civ_gov_parliament")) return null;
            string ap = Get(k, "appointments"), land = Get(k, "land"), fe = Get(k, "fealty"), lev = Get(k, "levy"), fis = Get(k, "fiscal"), cul = Get(k, "culture"), fo = Get(k, "foreign"), tax = Get(k, "taxation");
            if (ap == "hereditary_offices" && land == "fiefs") return "feudal";
            if (fe == "absolute_fealty" && ap == "royal_appointment") return "absolutist";
            if ((lev == "heavy_levy" || lev == "total_levy") && fis == "army_first") return "militarist";
            if (cul == "state_faith" && fe != "loose_fealty") return "confessional";
            if (ap == "merit_exams" && land == "state_lands") return "bureaucratic";
            if (fe == "loose_fealty" && land == "free_holdings" && Profile.Get(k).Cities >= 4) return "federal";
            if (fo == "expansion") return "expansionist";
            if (fo == "isolation" && Gov.Band(k) >= 1) return "isolationist";
            if (fo == "concert" && tax != "heavy_tax") return "mercantile";
            return null;
        }
    }
}
