using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal static class Gov
    {
        public const string Group = "Civilizations";
        private static bool _registered;
        private static readonly Dictionary<string, bool> _cfgCache = new Dictionary<string, bool>();
        private static float _cfgTime = -99f;

        private static readonly Dictionary<string, string[]> LogTypes = new Dictionary<string, string[]>
        {
            { "bill_proposed", new[] { "civ_politics", "neutral", "parliament" } },
            { "bill_passed", new[] { "civ_politics", "good", "parliament" } },
            { "bill_failed", new[] { "civ_politics", "bad", "parliament" } },
            { "bill_decree", new[] { "civ_politics", "bad", "decree" } },
            { "bill_withdrawn", new[] { "civ_politics", "neutral", "parliament" } },
            { "charter", new[] { "civ_politics", "neutral", "charter" } },
            { "bloc_joined", new[] { "civ_politics", "neutral", "bloc" } },
            { "bloc_left", new[] { "civ_politics", "neutral", "bloc" } },
            { "mandate_phase", new[] { "civ_politics", "neutral", "mandate" } },
            { "great_rebellion", new[] { "civ_military", "bad", "rebellion" } },
            { "rebellion_spread", new[] { "civ_military", "bad", "rebellion" } },
            { "restoration", new[] { "civ_politics", "good", "restoration" } },
            { "general_coup", new[] { "civ_politics", "bad", "coup" } },
            { "new_dynasty", new[] { "civ_politics", "neutral", "dynasty" } },
            { "vassal_new", new[] { "civ_diplomacy", "neutral", "vassal" } },
            { "vassal_state", new[] { "civ_diplomacy", "neutral", "vassal" } },
            { "vassal_annexed", new[] { "civ_diplomacy", "neutral", "vassal" } },
            { "vassal_revolt", new[] { "civ_diplomacy", "bad", "vassal" } },
            { "appanage", new[] { "civ_politics", "neutral", "vassal" } },
            { "tribute_demand", new[] { "civ_diplomacy", "neutral", "tribute" } },
            { "tribute_refused", new[] { "civ_diplomacy", "bad", "tribute" } },
            { "peace_talks", new[] { "civ_diplomacy", "good", "peace" } },
            { "siege_fall", new[] { "civ_military", "neutral", "siege" } },
            { "treasury_unpaid", new[] { "civ_economy", "bad", "treasury" } },
            { "treasury_works", new[] { "civ_economy", "good", "treasury" } },
            { "armament", new[] { "civ_military", "good", "armament" } },
            { "alliance_formed", new[] { "civ_diplomacy", "good", "alliance" } },
            { "alliance_event", new[] { "civ_diplomacy", "neutral", "alliance" } },
            { "alliance_ended", new[] { "civ_diplomacy", "bad", "alliance" } },
            { "plot_started", new[] { "civ_politics", "neutral", "plot" } },
            { "plot_done", new[] { "civ_politics", "neutral", "plot" } }
        };

        public static readonly string[] CfgIds = { "civ_gov_parliament", "civ_gov_cycle", "civ_gov_vassals", "civ_gov_peace", "civ_gov_treasury", "civ_gov_alliances", "civ_gov_armies", "civ_gov_names" };

        public static bool On(string id)
        {
            if (Time.unscaledTime - _cfgTime > 5f)
            {
                _cfgTime = Time.unscaledTime;
                _cfgCache.Clear();
            }
            bool v;
            if (_cfgCache.TryGetValue(id, out v)) return v;
            v = true;
            try
            {
                ModConfig c = CivMod.Instance.GetConfig();
                v = c[Group][id].BoolVal;
            }
            catch (Exception)
            {
                v = true;
            }
            _cfgCache[id] = v;
            return v;
        }

        public static void Register()
        {
            if (_registered) return;
            _registered = true;
            foreach (var kv in LogTypes)
            {
                string id = "civ_" + kv.Key;
                if (AssetManager.world_log_library.get(id) != null) continue;
                Color col = kv.Value[1] == "good" ? Toolbox.color_log_good : kv.Value[1] == "bad" ? Toolbox.color_log_warning : Toolbox.color_log_neutral;
                AssetManager.world_log_library.add(new WorldLogAsset
                {
                    id = id,
                    group = kv.Value[0],
                    locale_id = "civ_log_" + kv.Key,
                    path_icon = "ui/Icons/civ/log/i_" + kv.Value[2],
                    color = col,
                    text_replacer = Replace
                });
            }
            CivMod.Safe("gov-traits", GovTraits.InitAssets);
            CivMod.Safe("gov-hooks", GovHooks.InitAssets);
            CivMod.Safe("gov-parliament", Parliament.Register);
            CivMod.Safe("gov-mandate", Mandate.Register);
            CivMod.Safe("gov-vassals", Vassals.Register);
            CivMod.Safe("gov-war", GovWar.Register);
            CivMod.Safe("gov-alliances", AllianceLife.Register);
            CivMod.Safe("gov-greats", GovGreats.Register);
            CivMod.Safe("gov-sandbox", GovSandbox.Register);
        }

        private static void Replace(WorldLogMessage m, ref string text)
        {
            text = text.Replace("$a$", m.getSpecial(1)).Replace("$b$", m.getSpecial(2)).Replace("$c$", m.getSpecial(3));
        }

        public static void Clear()
        {
            Parliament.Clear();
            GovWar.Clear();
            ArmyCommand.Clear();
            AllianceLife.Clear();
            Vassals.Clear();
        }

        public static void Year()
        {
            if (On("civ_gov_vassals")) CivMod.Safe("gov-vassals-flush", Vassals.Flush);
            if (On("civ_gov_alliances")) CivMod.Safe("gov-alliances-year", AllianceLife.Year);
            CivMod.Safe("gov-greats-year", GovGreats.Year);
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            KProfile p = Profile.Get(k);
            if (On("civ_gov_parliament")) CivMod.Safe("gov-parliament-year", () => Parliament.KingdomYear(k, p, yrs));
            if (On("civ_gov_treasury")) CivMod.Safe("gov-treasury-year", () => Treasury.KingdomYear(k, p, yrs));
            if (On("civ_gov_vassals")) CivMod.Safe("gov-vassals-year", () => Vassals.KingdomYear(k, p, yrs));
            CivMod.Safe("gov-plots-year", () => Plots.KingdomYear(k, p, yrs));
            if (On("civ_gov_armies")) CivMod.Safe("gov-army-year", () => ArmyCommand.KingdomYear(k, p, yrs));
            CivMod.Safe("gov-traits-year", () => GovTraits.Sync(k));
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!G.Ok(k)) return fx;
            foreach (var kv in Laws.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            if (On("civ_gov_treasury")) foreach (var kv in Treasury.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            if (On("civ_gov_cycle")) foreach (var kv in Mandate.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            if (On("civ_gov_armies")) foreach (var kv in ArmyCommand.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            return fx;
        }

        public static int Band(int era)
        {
            if (era <= 2) return 0;
            if (era <= 6) return 1;
            if (era <= 9) return 2;
            if (era <= 12) return 3;
            if (era <= 17) return 4;
            if (era <= 25) return 5;
            return 6;
        }

        public static int Band(Kingdom k)
        {
            return Band(G.Ok(k) ? Profile.Get(k).Era : 0);
        }

        public static string Council(Kingdom k)
        {
            return Txt.T("civ_council_" + Band(k));
        }

        public static string BlocTerm(Kingdom k)
        {
            return Txt.T("civ_blocterm_" + Band(k));
        }

        public static string Faction(int band, int quadrant)
        {
            return Txt.T("civ_gf_" + band + "_" + quadrant);
        }

        public static int Quadrant(float x, float y)
        {
            return (x < 0f ? 0 : 2) + (y < 0f ? 0 : 1);
        }

        public static string TitleId(Kingdom k)
        {
            if (!G.Ok(k)) return "ruler";
            KProfile p = Profile.Get(k);
            int band = Band(p.Era);
            string t = Chronicle.Title(k);
            if (t == "president" && band == 1) return "consul";
            if (t == "president" && band >= 6) return "archon";
            if (!string.IsNullOrEmpty(t)) return t;
            if (p.Fam.Contains("tribal") || band == 0) return "chief";
            if (p.Fam.Contains("democratic") || p.Fam.Contains("liberal")) return band == 1 ? "consul" : band >= 6 ? "archon" : "president";
            if (p.Fam.Contains("monarchy") || p.Fam.Contains("imperial"))
            {
                if (band >= 5) return "sovereign";
                return Politics.Stage(k) == "empire" ? "emperor" : "king";
            }
            return band == 1 ? "consul" : "ruler";
        }

        public static string Title(Kingdom k)
        {
            return Txt.T("civ_rt_" + TitleId(k));
        }

        public static string RulerName(Kingdom k)
        {
            if (!G.Ok(k) || !k.hasKing() || !G.Ok(k.king)) return "-";
            return Title(k) + " " + k.king.getName();
        }

        public static void Log(string type, Kingdom k, string a, string b = null, string c = null, Kingdom kb = null, City at = null)
        {
            Chronicle.Log(type, k, a, b, c, kb, null, at);
        }

        public static long Id(Clan c)
        {
            return c != null && c.data != null ? c.data.id : 0L;
        }

        public static Clan ClanById(long id)
        {
            if (id == 0L || World.world == null || World.world.clans == null) return null;
            foreach (Clan c in World.world.clans.list) if (c != null && c.data != null && c.data.id == id && c.isAlive()) return c;
            return null;
        }

        public static string ClanName(long id)
        {
            if (id == 0L) return Txt.T("civ_independents");
            Clan c = ClanById(id);
            return c != null ? c.data.name : "?";
        }

        public static Color ClanColor(long id)
        {
            if (id == 0L) return new Color(0.6f, 0.6f, 0.65f);
            Clan c = ClanById(id);
            try
            {
                if (c != null) return c.getColor()._color_main;
            }
            catch (Exception)
            {
            }
            return new Color(0.8f, 0.8f, 0.8f);
        }

        public static Actor Somebody(City c)
        {
            if (!G.Ok(c)) return null;
            if (G.Ok(c.leader)) return c.leader;
            foreach (Actor a in c.units) if (G.Ok(a) && a.isAdult()) return a;
            foreach (Actor a in c.units) if (G.Ok(a)) return a;
            return null;
        }
    }
}
