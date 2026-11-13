using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using db;
using HarmonyLib;
using SQLite;
using UnityEngine;

namespace Civilizations
{
    internal static class Chronicle
    {
        public static readonly string[] Groups = { "civ_science", "civ_politics", "civ_economy", "civ_diplomacy", "civ_military", "civ_infrastructure", "civ_society", "civ_cosmos" };
        public static readonly string[] Titles = { "chief", "high_priest", "president", "prime_minister", "doge", "chairman", "general", "director", "ceo", "emperor" };
        public static readonly string[] KingAssets = { "king_new", "king_left", "king_fled_city", "king_fled_capital", "king_dead", "king_killed" };

        private static readonly Dictionary<string, string[]> Types = new Dictionary<string, string[]>
        {
            { "era_up", new[] { "civ_science", "good", "era" } },
            { "era_down", new[] { "civ_science", "bad", "era_down" } },
            { "first_era", new[] { "civ_science", "good", "first" } },
            { "first_tech", new[] { "civ_science", "good", "first" } },
            { "tech_dormant", new[] { "civ_science", "bad", "dormant" } },
            { "tech_recovered", new[] { "civ_science", "good", "recovered" } },
            { "tech_forgotten", new[] { "civ_science", "bad", "forgotten" } },
            { "launch", new[] { "civ_science", "good", "launch" } },
            { "gov_change", new[] { "civ_politics", "neutral", "gov" } },
            { "ideo_change", new[] { "civ_politics", "neutral", "ideo" } },
            { "econ_change", new[] { "civ_politics", "neutral", "econ" } },
            { "coup", new[] { "civ_politics", "bad", "coup" } },
            { "civil_war", new[] { "civ_politics", "bad", "civil_war" } },
            { "legit_crisis", new[] { "civ_politics", "bad", "crisis" } },
            { "revolution", new[] { "civ_politics", "bad", "revolution" } },
            { "election", new[] { "civ_politics", "neutral", "election" } },
            { "flag_reform", new[] { "civ_politics", "neutral", "flag" } },
            { "clan_coup", new[] { "civ_politics", "bad", "coup" } },
            { "stage", new[] { "civ_politics", "good", "stage" } },
            { "treaty", new[] { "civ_diplomacy", "good", "treaty" } },
            { "treaty_broken", new[] { "civ_diplomacy", "bad", "treaty_broken" } },
            { "dependent", new[] { "civ_diplomacy", "neutral", "dependent" } },
            { "independence", new[] { "civ_diplomacy", "good", "independence" } },
            { "integrated", new[] { "civ_diplomacy", "neutral", "integrated" } },
            { "first_contact", new[] { "civ_diplomacy", "neutral", "contact" } },
            { "org_founded", new[] { "civ_diplomacy", "good", "org" } },
            { "org_dissolved", new[] { "civ_diplomacy", "bad", "org_end" } },
            { "org_vote", new[] { "civ_diplomacy", "neutral", "vote" } },
            { "sanctions", new[] { "civ_diplomacy", "bad", "sanctions" } },
            { "order_rise", new[] { "civ_diplomacy", "good", "order" } },
            { "order_fall", new[] { "civ_diplomacy", "bad", "order_end" } },
            { "erasure", new[] { "civ_diplomacy", "bad", "erasure" } },
            { "currency_union", new[] { "civ_economy", "good", "currency" } },
            { "peace_terms", new[] { "wars", "neutral", "peace" } },
            { "nuke", new[] { "civ_military", "bad", "nuke" } },
            { "sack", new[] { "civ_military", "bad", "sack" } },
            { "border_wall", new[] { "civ_military", "neutral", "walls" } },
            { "unique_unit", new[] { "civ_military", "good", "first" } },
            { "cos_first", new[] { "civ_cosmos", "good", "cos_first" } },
            { "cos_colony", new[] { "civ_cosmos", "good", "cos_colony" } },
            { "cos_independence", new[] { "civ_cosmos", "bad", "cos_independence" } },
            { "cos_contact", new[] { "civ_cosmos", "neutral", "cos_contact" } },
            { "cos_battle", new[] { "civ_cosmos", "bad", "cos_battle" } },
            { "cos_mega", new[] { "civ_cosmos", "good", "cos_mega" } },
            { "cos_mega_captured", new[] { "civ_cosmos", "neutral", "mega_captured" } },
            { "cos_mega_ruined", new[] { "civ_cosmos", "bad", "mega_ruined" } },
            { "cos_mega_reformed", new[] { "civ_cosmos", "good", "mega_reformed" } },
            { "cos_kardashev", new[] { "civ_cosmos", "good", "cos_kardashev" } },
            { "cos_invasion", new[] { "civ_cosmos", "bad", "cos_invasion" } },
            { "cos_victory", new[] { "civ_cosmos", "good", "cos_victory" } },
            { "cos_ending", new[] { "civ_cosmos", "neutral", "cos_ending" } },
            { "cos_planet", new[] { "civ_cosmos", "neutral", "cos_planet" } },
            { "cos_away", new[] { "civ_cosmos", "neutral", "cos_away" } },
            { "sb_omen", new[] { "civ_cosmos", "neutral", "sb_omen" } },
            { "player_control", new[] { "civ_politics", "neutral", "player_control" } },
            { "player_victory", new[] { "civ_politics", "good", "player_victory" } },
            { "age_golden", new[] { "civ_society", "good", "age_golden" } },
            { "age_heroic", new[] { "civ_society", "good", "age_heroic" } },
            { "age_dark", new[] { "civ_society", "bad", "age_dark" } },
            { "coalition", new[] { "civ_diplomacy", "bad", "coalition" } },
            { "crisis_start", new[] { "civ_diplomacy", "bad", "crisis_start" } },
            { "crisis_war", new[] { "civ_military", "bad", "crisis_war" } },
            { "crisis_resolved", new[] { "civ_diplomacy", "good", "crisis_resolved" } },
            { "revanche", new[] { "civ_military", "bad", "revanche" } },
            { "parliament_deadlock", new[] { "civ_politics", "bad", "parliament_deadlock" } },
            { "dynasty_renewal", new[] { "civ_politics", "good", "dynasty_renewal" } },
            { "dynasty_peak", new[] { "civ_politics", "good", "dynasty_peak" } },
            { "dynasty_decline", new[] { "civ_politics", "bad", "dynasty_decline" } },
            { "dynasty_collapse", new[] { "civ_politics", "bad", "dynasty_collapse" } },
            { "company_founded", new[] { "civ_economy", "good", "company" } },
            { "company_bankrupt", new[] { "civ_economy", "bad", "bankrupt" } },
            { "nationalized", new[] { "civ_economy", "neutral", "nationalized" } },
            { "merger", new[] { "civ_economy", "neutral", "merger" } },
            { "currency_reform", new[] { "civ_economy", "neutral", "currency" } },
            { "hyperinflation", new[] { "civ_economy", "bad", "inflation" } },
            { "rail_opened", new[] { "civ_infrastructure", "good", "rail" } },
            { "blackout", new[] { "civ_infrastructure", "bad", "blackout" } },
            { "walls", new[] { "civ_infrastructure", "neutral", "walls" } },
            { "festival", new[] { "civ_society", "good", "festival" } },
            { "great_person", new[] { "civ_society", "good", "great" } },
            { "great_person_died", new[] { "civ_society", "neutral", "great_dead" } },
            { "abolition", new[] { "civ_society", "good", "abolition" } },
            { "slave_revolt", new[] { "civ_society", "bad", "revolt" } },
            { "event_good", new[] { "civ_society", "good", "event" } },
            { "event_bad", new[] { "civ_society", "bad", "event_bad" } },
            { "event_econ", new[] { "civ_economy", "neutral", "event_econ" } },
            { "event_sci", new[] { "civ_science", "good", "event_sci" } },
            { "event_mil", new[] { "civ_military", "neutral", "event_mil" } },
            { "event_world", new[] { "civ_society", "bad", "event_world" } }
        };

        private static bool _done;

        public static void Register()
        {
            if (_done) return;
            _done = true;
            foreach (string g in Groups)
            {
                if (AssetManager.history_groups.get(g) != null) continue;
                AssetManager.history_groups.add(new HistoryGroupAsset { id = g, icon_path = "ui/Icons/civ/log/" + g.Substring(4) });
            }
            foreach (var kv in Types)
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
            foreach (string src in KingAssets)
            {
                WorldLogAsset a = AssetManager.world_log_library.get(src);
                if (a == null) continue;
                foreach (string t in Titles)
                {
                    string id = src + "__" + t;
                    if (AssetManager.world_log_library.get(id) != null) continue;
                    AssetManager.world_log_library.add(new WorldLogAsset
                    {
                        id = id,
                        group = a.group,
                        path_icon = a.path_icon,
                        color = a.color,
                        locale_id = "civ_wl_" + src + "_" + t,
                        text_replacer = a.text_replacer
                    });
                }
            }
            Hub.OnAny(OnHub);
        }

        private static void Replace(WorldLogMessage m, ref string text)
        {
            text = text.Replace("$a$", m.getSpecial(1)).Replace("$b$", m.getSpecial(2)).Replace("$c$", m.getSpecial(3));
        }

        public static string Title(Kingdom k)
        {
            if (!G.Ok(k)) return "";
            string gov = Policies.Id(k, "gov");
            switch (gov)
            {
                case "tribal_band":
                case "chiefdom":
                case "tribal_council": return "chief";
                case "theocracy": return "high_priest";
                case "republic":
                case "democracy":
                case "presidential_republic": return "president";
                case "parliamentary_republic": return "prime_minister";
                case "merchant_republic": return "doge";
                case "single_party_state": return "chairman";
                case "military_junta": return "general";
                case "technocracy": return "director";
                case "corporatocracy": return "ceo";
            }
            if (Politics.Stage(k) == "empire" && gov != "constitutional_monarchy") return "emperor";
            return "";
        }

        private static string Col(Kingdom k)
        {
            return G.Ok(k) ? Toolbox.colorToHex(k.getColor().getColorText(), false) : "#FFFFFF";
        }

        public static void Log(string type, Kingdom k, string a, string b = null, string c = null, Kingdom kb = null, Kingdom kc = null, City at = null, Actor unit = null, bool hud = true)
        {
            string id = "civ_" + type;
            WorldLogAsset asset = AssetManager.world_log_library.get(id);
            if (asset == null || World.world == null) return;
            var m = new WorldLogMessage(asset, a ?? "", b ?? "", c ?? "")
            {
                color_special_1 = Col(k),
                color_special_2 = kb != null ? Col(kb) : "#FFFFFF",
                color_special_3 = kc != null ? Col(kc) : "#FFFFFF"
            };
            if (G.Ok(k)) m.kingdom = k;
            if (G.Ok(unit) && unit.asset.can_be_inspected) m.unit = unit;
            WorldTile t = at != null ? G.Tile(at) : (G.Ok(k) && k.capital != null ? G.Tile(k.capital) : null);
            if (t != null) m.location = new Vector2(t.x, t.y);
            if (hud && HistoryHud.instance != null) HistoryHud.instance.newHistory(m);
            DBInserter.insertLog(m);
            Trim();
        }

        private static int _since;

        private static void Trim()
        {
            if (++_since < 500) return;
            _since = 0;
            int limit = CivMod.C != null ? CivMod.C.LogLimit : 100000;
            CivMod.Safe("log-trim", () =>
            {
                DBInserter.executeCommands();
                var conn = DBManager.getSyncConnection();
                using (conn.Lock())
                {
                    string table = conn.GetMapping<WorldLogMessage>(CreateFlags.None).TableName;
                    int n = conn.ExecuteScalar<int>("select count(*) from " + table);
                    if (n <= limit) return;
                    conn.Execute("delete from " + table + " where ROWID in (select ROWID from " + table + " order by timestamp ASC, ROWID ASC limit " + (n - limit) + ")");
                }
            });
        }

        private static string K(Kingdom k)
        {
            return G.Ok(k) ? k.name : "?";
        }

        private static void OnHub(HubEvent e)
        {
            switch (e.Type)
            {
                case Hub.EraChanged:
                    int era;
                    if (int.TryParse(e.Id, out era) && era > 0) Log("era_up", e.K, K(e.K), Db.EraName(era), null, null, null, null, null, false);
                    break;
                case Hub.EraDropped:
                    int er;
                    if (int.TryParse(e.Id, out er)) Log("era_down", e.K, K(e.K), Db.EraName(er));
                    break;
                case Hub.FirstInWorld:
                    if (e.Id == "era")
                    {
                        int fe;
                        if (int.TryParse(e.Arg, out fe)) Log("first_era", e.K, K(e.K), Db.EraName(fe));
                    }
                    else Log("first_tech", e.K, K(e.K), Db.TechName(e.Arg));
                    break;
                case Hub.TechDormant:
                    Log("tech_dormant", e.K, K(e.K), Db.TechName(e.Id), Txt.T("civ_cause_" + e.Arg), null, null, null, null, false);
                    break;
                case Hub.TechRecovered:
                    Log("tech_recovered", e.K, K(e.K), Db.TechName(e.Id), null, null, null, null, null, false);
                    break;
                case Hub.TechLost:
                    if (e.Arg == "forgotten") Log("tech_forgotten", e.K, K(e.K), Db.TechName(e.Id), null, null, null, null, null, false);
                    break;
                case Hub.PolicyChanged:
                    string[] parts = e.Arg.Split('|');
                    string grp = parts.Length > 1 ? parts[1] : "";
                    if (grp == "gov" || grp == "ideo" || grp == "econ") Log(grp + "_change", e.K, K(e.K), Txt.T("civ_p_" + e.Id), parts[0].Length > 0 ? Txt.T("civ_p_" + parts[0]) : "-");
                    break;
                case Hub.Coup:
                    Log("coup", e.K, K(e.K), Txt.T("civ_p_" + e.Id));
                    break;
                case Hub.CivilWar:
                    Log("civil_war", e.K, K(e.K), K(e.O), Txt.T("civ_faction_" + e.Id), e.O, null, e.C);
                    break;
                case Hub.Legitimacy:
                    Log("legit_crisis", e.K, K(e.K), Txt.T("civ_legit_" + e.Arg));
                    break;
                case Hub.TreatySigned:
                    Log("treaty", e.K, K(e.K), K(e.O), Txt.T("civ_treaty_" + e.Id), e.O, null, null, null, false);
                    break;
                case Hub.TreatyBroken:
                    Log("treaty_broken", e.K, K(e.K), K(e.O), Txt.T("civ_treaty_" + e.Id), e.O);
                    break;
                case Hub.Dependent:
                    Log("dependent", e.K, K(e.K), K(e.O), Txt.T("civ_dep_" + e.Id), e.O);
                    break;
                case Hub.Independence:
                    Log("independence", e.K, K(e.K), K(e.O), null, e.O, null, e.C);
                    break;
                case Hub.Integrated:
                    Log("integrated", e.K, K(e.K), K(e.O), null, e.O);
                    break;
                case Hub.FirstContact:
                    Log("first_contact", e.K, K(e.K), K(e.O), Txt.T("civ_contact_" + e.Id), e.O, null, null, null, false);
                    break;
                case Hub.OrgFounded:
                    Log("org_founded", e.K, Txt.T("civ_org_" + e.Id), K(e.K), null, e.K);
                    break;
                case Hub.OrgDissolved:
                    Log("org_dissolved", e.K, Txt.T("civ_org_" + e.Id), null, null, null, null, null, null, false);
                    break;
                case Hub.OrgVote:
                    if (e.Value > 0f) Log("org_vote", e.K, OrgLabel(e.Arg), Txt.T("civ_vote_" + e.Id), G.Ok(e.O) ? K(e.O) : "-", null, e.O);
                    break;
                case Hub.Sanctions:
                    Log("sanctions", e.K, K(e.K), OrgLabel(e.Id), null, null, null, null, null, false);
                    break;
                case Hub.WorldOrder:
                    Log("order_rise", e.K, Txt.T("civ_order_" + e.Id), K(e.K), null, e.K);
                    break;
                case Hub.WorldOrderFell:
                    Log("order_fall", e.K, Txt.T("civ_order_" + e.Id), Txt.T("civ_fall_" + e.Arg));
                    break;
                case Hub.Erasure:
                    Log("erasure", e.K, K(e.K), e.Arg, K(e.O), null, e.O, e.C);
                    break;
                case Hub.PeaceTerms:
                    Log("peace_terms", e.K, K(e.K), K(e.O), Terms(e.Id), e.O, null, null, null, false);
                    break;
                case Hub.NukeDetonated:
                    Log("nuke", e.K, K(e.K), e.C != null ? G.Name(e.C) : K(e.O), null, null, e.O, e.C);
                    break;
                case Hub.Festival:
                    if (e.Value < 1f) break;
                    Log("festival", e.K, Txt.T("civ_fest_" + e.Id), e.C != null ? G.Name(e.C) : K(e.K), K(e.K), null, e.K, e.C, null, false);
                    break;
                case Hub.GreatPerson:
                    bool died = e.Arg.StartsWith("died");
                    string who = G.Ok(e.A) ? e.A.getName() : died && e.Arg.Length > 5 ? e.Arg.Substring(5) : "?";
                    Log(died ? "great_person_died" : "great_person", e.K, who, Txt.T("civ_great_" + e.Id), K(e.K), null, e.K, null, e.A, !died);
                    break;
                case Hub.FlagReform:
                    Log("flag_reform", e.K, K(e.K), null, null, null, null, null, null, false);
                    break;
                case Hub.Event:
                    if (!Txt.Has("civ_ev_" + e.Id + "_log")) break;
                    string cat = Events.Category(e.Id);
                    string type = cat == "econ" ? "event_econ" : cat == "sci" ? "event_sci" : cat == "mil" ? "event_mil" : e.K == null ? "event_world" : Events.Good(e.Id) ? "event_good" : "event_bad";
                    Log(type, e.K, Txt.F("civ_ev_" + e.Id + "_log", K(e.K), e.C != null ? G.Name(e.C) : "", G.Ok(e.O) ? K(e.O) : ""), null, null, null, null, e.C, e.A, e.Value >= 1f);
                    break;
            }
        }

        private static string OrgLabel(string key)
        {
            if (key.StartsWith("order:")) return Txt.T("civ_order_" + key.Substring(6));
            int id;
            if (int.TryParse(key, out id))
            {
                Org o = Orgs.All.FirstOrDefault(x => x.Id == id);
                if (o != null) return Orgs.Name(o);
            }
            return Txt.Has("civ_org_" + key) ? Txt.T("civ_org_" + key) : key;
        }

        private static string Terms(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return Txt.T("civ_term_white_peace");
            return string.Join(", ", arg.Split(',').Where(x => x.Length > 0).Select(x => Txt.T("civ_term_" + x)).ToArray());
        }

        public static string KingVariant(WorldLogMessage m)
        {
            Kingdom k = m.kingdom;
            if (!G.Ok(k) && !string.IsNullOrEmpty(m.special1)) k = G.Kingdoms().FirstOrDefault(x => x.name == m.special1);
            string t = Title(k);
            return t.Length == 0 ? null : m.asset_id + "__" + t;
        }

        [HarmonyPatch(typeof(WorldLogMessageExtensions), nameof(WorldLogMessageExtensions.add))]
        private static class Retitle
        {
            private static void Prefix(WorldLogMessage pMessage)
            {
                if (pMessage == null || pMessage.asset_id == null || !_done) return;
                if (Array.IndexOf(KingAssets, pMessage.asset_id) < 0) return;
                try
                {
                    string v = KingVariant(pMessage);
                    if (v != null && AssetManager.world_log_library.get(v) != null) pMessage.asset_id = v;
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(DBGetter), nameof(DBGetter.getWorldLogMessages))]
        private static class Limit
        {
            private static bool Prefix(ref ListPool<WorldLogMessage> __result)
            {
                try
                {
                    int limit = CivMod.C != null ? CivMod.C.LogLimit : 100000;
                    DBInserter.executeCommands();
                    var conn = DBManager.getSyncConnection();
                    using (conn.Lock())
                    {
                        string table = conn.GetMapping<WorldLogMessage>(CreateFlags.None).TableName;
                        __result = conn.QueryPool<WorldLogMessage>("select * from " + table + " order by timestamp DESC, ROWID DESC LIMIT " + limit);
                    }
                    return false;
                }
                catch (Exception e)
                {
                    CivMod.Report("log-limit", e);
                    return true;
                }
            }
        }

        private static List<WorldLogMessage> _sorted;
        private static int _shown;
        private const int Page = 100;

        [HarmonyPatch(typeof(WorldLogWindow), "showSortedRoutine")]
        private static class Paged
        {
            private static bool Prefix(WorldLogWindow __instance, ref IEnumerator __result)
            {
                __result = Routine(__instance);
                return false;
            }
        }

        private static IEnumerator Routine(WorldLogWindow w)
        {
            w.clear();
            var list = new List<WorldLogMessage>();
            if (w._messages != null)
            {
                foreach (WorldLogMessage m in w._messages)
                {
                    if (w._selected_groups.Count > 0)
                    {
                        WorldLogAsset a = m.getAsset();
                        if (a == null || string.IsNullOrEmpty(a.group) || !w._selected_groups.Contains(a.group)) continue;
                    }
                    else if (m.getAsset() == null) continue;
                    list.Add(m);
                }
            }
            list.Sort((p1, p2) => p2.timestamp.CompareTo(p1.timestamp));
            _sorted = list;
            _shown = 0;
            yield return null;
            Append(w);
        }

        private static void Append(WorldLogWindow w)
        {
            if (_sorted == null) return;
            int end = Math.Min(_sorted.Count, _shown + Page);
            for (int i = _shown; i < end; i++)
            {
                EmptyLogElement next = w._pool_empty.getNext();
                next.load(_sorted[i]);
                next.setElement(null);
            }
            _shown = end;
        }

        [HarmonyPatch(typeof(WorldLogWindow), "LateUpdate")]
        private static class More
        {
            private static void Postfix(WorldLogWindow __instance)
            {
                if (_sorted == null || _shown >= _sorted.Count || __instance._scroll_rect == null) return;
                if (__instance._scroll_rect.verticalNormalizedPosition > 0.06f) return;
                Append(__instance);
            }
        }

        [HarmonyPatch(typeof(WorldLogWindow), "OnDisable")]
        private static class Closed
        {
            private static void Postfix()
            {
                _sorted = null;
                _shown = 0;
            }
        }

        [HarmonyPatch(typeof(WorldLogWindow), "Awake")]
        private static class Tabs
        {
            private static void Postfix(WorldLogWindow __instance)
            {
                try
                {
                    GridLayoutLayout(__instance);
                }
                catch (Exception e)
                {
                    CivMod.Report("log-tabs", e);
                }
            }
        }

        private static void GridLayoutLayout(WorldLogWindow w)
        {
            int n = AssetManager.history_groups.list.Count;
            if (n <= 8 || w._grid == null) return;
            int cols = (n + 1) / 2;
            UnityEngine.UI.GridLayoutGroup g = w._grid;
            float cw = 198f / cols;
            float ch = g.cellSize.y;
            g.cellSize = new Vector2(cw, ch);
            g.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;
            float add = ch + g.spacing.y;
            RectTransform gr = (RectTransform)g.transform;
            Vector2 size = gr.rect.size;
            Vector2 old = gr.pivot;
            gr.pivot = new Vector2(old.x, 1f);
            gr.anchoredPosition += new Vector2(0f, (1f - old.y) * size.y);
            gr.sizeDelta += new Vector2(0f, add);
            if (w._scroll_rect != null)
            {
                RectTransform sr = (RectTransform)w._scroll_rect.transform;
                sr.offsetMax -= new Vector2(0f, add);
            }
        }
    }
}
