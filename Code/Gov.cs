using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal static class GovEra
    {
        public const int Council = 0;
        public const int Nobles = 3;
        public const int Mandate = 3;
        public const int Tributaries = 3;
        public const int Ledger = 3;
        public const int Works = 3;
        public const int MilitaryTab = 3;
        public const int DiplomacyTab = 4;
        public const int Reparations = 4;
        public const int Fiefs = 5;
        public const int Provinces = 5;
        public const int Coin = 5;
        public const int Disband = 5;
        public const int Kingdoms = 5;
        public const int Bills = 6;
        public const int Republic = 6;
        public const int Flow = 6;
        public const int Guard = 6;
        public const int Hereditary = 7;
        public const int Merchant = 7;
        public const int Blocs = 9;
        public const int MassVote = 10;
        public const int Junta = 10;
        public const int Future = 15;

        public static int Of(Kingdom k)
        {
            return G.Ok(k) ? Research.Era(k) : 0;
        }

        public static bool Has(Kingdom k, int era)
        {
            return Of(k) >= era;
        }

        public static string Note(int era)
        {
            return Txt.F("civ_gov_locked", Db.EraName(Math.Max(0, Math.Min(Db.LastEra, era))));
        }

        public static int Tier(Kingdom k)
        {
            int e = Of(k);
            if (e < Nobles) return 0;
            if (e < Bills) return 1;
            if (e < Blocs) return 2;
            return 3;
        }
    }

    internal static class Gov
    {
        public const string Group = "Civilizations";
        public const string P = "civ_g2_";
        public const string KForm = P + "form";
        public const string KMil = P + "junta";
        public const string KEmpire = P + "empire";
        private static bool _registered;
        private static readonly Dictionary<string, bool> _cfgCache = new Dictionary<string, bool>();
        private static float _cfgTime = -99f;

        public static readonly string[] CfgIds = { "civ_gov_parliament", "civ_gov_elections", "civ_gov_cycle", "civ_gov_vassals", "civ_gov_treasury", "civ_gov_peace", "civ_gov_armies", "civ_gov_names" };

        private static readonly Dictionary<string, string[]> LogTypes = new Dictionary<string, string[]>
        {
            { "g2_bill", new[] { "civ_politics", "neutral", "log_bill" } },
            { "g2_passed", new[] { "civ_politics", "good", "log_passed" } },
            { "g2_failed", new[] { "civ_politics", "bad", "log_failed" } },
            { "g2_decree", new[] { "civ_politics", "bad", "log_decree" } },
            { "g2_ruler_decides", new[] { "civ_politics", "neutral", "log_decree" } },
            { "g2_bloc", new[] { "civ_politics", "neutral", "log_bloc" } },
            { "g2_election", new[] { "civ_politics", "good", "log_election" } },
            { "g2_republic", new[] { "civ_politics", "neutral", "log_republic" } },
            { "g2_monarchy", new[] { "civ_politics", "neutral", "log_crown" } },
            { "g2_empire", new[] { "civ_politics", "good", "log_empire" } },
            { "g2_coup", new[] { "civ_politics", "bad", "log_coup" } },
            { "g2_junta", new[] { "civ_politics", "bad", "log_coup" } },
            { "g2_phase", new[] { "civ_politics", "neutral", "log_mandate" } },
            { "g2_rebellion", new[] { "civ_military", "bad", "log_rebellion" } },
            { "g2_pacified", new[] { "civ_politics", "neutral", "log_mandate" } },
            { "g2_restoration", new[] { "civ_politics", "good", "log_restoration" } },
            { "g2_restoration_fail", new[] { "civ_politics", "bad", "log_restoration" } },
            { "g2_vassal_state", new[] { "civ_diplomacy", "neutral", "log_vassal" } },
            { "g2_province", new[] { "civ_diplomacy", "good", "log_vassal" } },
            { "g2_direct_rule", new[] { "civ_diplomacy", "neutral", "log_annex" } },
            { "g2_grant", new[] { "civ_politics", "neutral", "log_fief" } },
            { "g2_appanage", new[] { "civ_politics", "neutral", "log_fief" } },
            { "g2_submission", new[] { "civ_diplomacy", "neutral", "log_submit" } },
            { "g2_independence", new[] { "civ_diplomacy", "bad", "log_independence" } },
            { "g2_talks", new[] { "civ_diplomacy", "good", "log_talks" } },
            { "g2_storm", new[] { "civ_military", "neutral", "log_storm" } },
            { "g2_unpaid", new[] { "civ_economy", "bad", "log_unpaid" } },
            { "g2_disband", new[] { "civ_military", "bad", "log_unpaid" } },
            { "g2_works", new[] { "civ_economy", "good", "log_works" } }
        };

        public static bool On(string id)
        {
            if (Time.unscaledTime - _cfgTime > 5f)
            {
                _cfgTime = Time.unscaledTime;
                _cfgCache.Clear();
            }
            bool v;
            if (_cfgCache.TryGetValue(id, out v)) return v;
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
                    path_icon = "ui/Icons/civ/pol/" + kv.Value[2],
                    color = col,
                    text_replacer = Replace
                });
            }
            CivMod.Safe("gov-legacy", GovHooks.Legacy);
            CivMod.Safe("gov-hooks", GovHooks.InitAssets);
            CivMod.Safe("gov-mandate", Mandate.Register);
            CivMod.Safe("gov-vassals", Vassals.Register);
            CivMod.Safe("gov-war", GovWar.Register);
            CivMod.Safe("gov-treasury", Treasury.Register);
            CivMod.Safe("gov-greats", GovGreats.Register);
            CivMod.Safe("gov-sandbox", GovSandbox.Register);
        }

        private static void Replace(WorldLogMessage m, ref string text)
        {
            text = text.Replace("$a$", m.getSpecial(1)).Replace("$b$", m.getSpecial(2)).Replace("$c$", m.getSpecial(3));
        }

        public static void Clear()
        {
            Fam.Clear();
            Vassals.Clear();
            GovWar.Clear();
            GovGuard.Clear();
        }

        public static void Year()
        {
            if (On("civ_gov_vassals")) CivMod.Safe("gov-vassals-flush", Vassals.Flush);
            CivMod.Safe("gov-greats-year", GovGreats.Year);
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            KProfile p = Profile.Get(k);
            CivMod.Safe("gov-legacy-k", () => GovHooks.Strip(k));
            CivMod.Safe("gov-fam-year", () => Fam.KingdomYear(k, yrs));
            CivMod.Safe("gov-parl-year", () => Parl.KingdomYear(k, p, yrs));
            CivMod.Safe("gov-rule-year", () => Rulers.KingdomYear(k, p, yrs));
            if (On("civ_gov_treasury")) CivMod.Safe("gov-treasury-year", () => Treasury.KingdomYear(k, p, yrs));
            if (On("civ_gov_vassals")) CivMod.Safe("gov-vassals-year", () => Vassals.KingdomYear(k, p, yrs));
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (!G.Ok(k) || !k.isCiv()) return fx;
            foreach (var kv in Parl.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            if (On("civ_gov_treasury")) foreach (var kv in Treasury.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            if (On("civ_gov_cycle")) foreach (var kv in Mandate.Effects(k)) Store.Add(fx, kv.Key, kv.Value);
            return fx;
        }

        public static void Log(string type, Kingdom k, string a, string b = null, string c = null, Kingdom kb = null, City at = null)
        {
            Chronicle.Log("g2_" + type, k, a, b, c, kb, null, at);
        }

        public static Clan ClanById(long id)
        {
            if (id <= 0L || World.world == null || World.world.clans == null) return null;
            foreach (Clan c in World.world.clans.list) if (c != null && c.data != null && c.data.id == id && c.isAlive()) return c;
            return null;
        }

        public static Clan ClanOf(Actor a)
        {
            return G.Ok(a) && a.hasClan() ? a.clan : null;
        }

        public static long ClanId(Actor a)
        {
            Clan c = ClanOf(a);
            return c != null && c.data != null ? c.data.id : 0L;
        }

        public static Actor Somebody(City c)
        {
            if (!G.Ok(c)) return null;
            if (G.Ok(c.leader)) return c.leader;
            foreach (Actor a in c.units) if (G.Ok(a) && a.isAdult()) return a;
            foreach (Actor a in c.units) if (G.Ok(a)) return a;
            return null;
        }

        public static bool Republic(Kingdom k)
        {
            if (!G.Ok(k)) return false;
            string f = Store.Str(k.data, KForm);
            if (f == "r") return true;
            if (f == "m") return false;
            if (Store.Int(k.data, KMil, 0) > 0) return true;
            string gov = Policies.Id(k, "gov") ?? "";
            if (Coherence.Is(gov, "monarchy") || Coherence.Is(gov, "theocratic") || Coherence.Is(gov, "imperial")) return false;
            return Coherence.Is(gov, "republic") || Coherence.Is(gov, "democratic") || Coherence.Is(gov, "corporate");
        }

        public static int AllCities(Kingdom k)
        {
            int n = G.Cities(k).Count;
            foreach (Kingdom v in Diplomacy.DependentsOf(k)) n += G.Cities(v).Count;
            return n;
        }

        public static string Form(Kingdom k)
        {
            if (!G.Ok(k)) return "tribe";
            int era = GovEra.Of(k);
            if (era < GovEra.Nobles) return "tribe";
            Kingdom lord = Diplomacy.Overlord(k);
            if (G.Ok(lord))
            {
                string st = Vassals.Status(k);
                if (st == "hereditary_province" || st == "province") return st;
                return Republic(k) ? "dominion" : "duchy";
            }
            bool rep = Republic(k);
            if (era < GovEra.Kingdoms) return rep ? "tribe" : "chiefdom";
            if (rep)
            {
                if (era < GovEra.Republic) return "tribe";
                if (era >= GovEra.Junta && Store.Int(k.data, KMil, 0) > 0) return "military_republic";
                string gov = Policies.Id(k, "gov") ?? "";
                if (era >= GovEra.Merchant && (Coherence.Is(gov, "mercantile") || Coherence.Is(gov, "corporate") || Profile.Get(k).Fam.Contains("mercantile"))) return "merchant_republic";
                return "republic";
            }
            if (Store.Int(k.data, KEmpire, 0) > 0 || AllCities(k) >= 6) return "empire";
            return "kingdom";
        }

        private static string Future(Kingdom k)
        {
            return GovEra.Has(k, GovEra.Future) ? "_fut" : "";
        }

        public static string FormName(Kingdom k)
        {
            return Txt.T("civ_gov_form_" + Form(k) + Future(k));
        }

        public static string Title(Kingdom k)
        {
            string f = Form(k);
            if (f == "republic" && GovEra.Has(k, GovEra.MassVote) && !GovEra.Has(k, GovEra.Future)) return Txt.T("civ_gov_title_president");
            return Txt.T("civ_gov_title_" + f + Future(k));
        }

        public static string RulerName(Kingdom k)
        {
            if (!G.Ok(k) || !k.hasKing() || !G.Ok(k.king)) return Txt.T("civ_gov_vacant");
            return Title(k) + " " + k.king.getName();
        }

        public static string Council(Kingdom k)
        {
            int t = GovEra.Tier(k);
            if (t == 0) return Txt.T("civ_parl_name_elders");
            if (t == 1) return Txt.T("civ_parl_name_nobles");
            return Txt.T("civ_parl_name_" + Parl.ChamberId(k));
        }
    }
}
