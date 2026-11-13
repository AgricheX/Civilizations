using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal class WinControl : AbstractWindow<WinControl>
    {
        public const string Id = "civ_control";
        private static readonly string[] TabKeys = { "civ_ctrl_tab_overview", "civ_ctrl_tab_research", "civ_ctrl_tab_politics", "civ_ctrl_tab_economy", "civ_ctrl_tab_war", "civ_ctrl_tab_space" };
        private static readonly string[] TabIcons = { "ui/Icons/civ/ctrl/overview", "ui/Icons/civ/ctrl/research", "ui/Icons/civ/ctrl/politics", "ui/Icons/civ/ctrl/economy", "ui/Icons/civ/ctrl/war", "ui/Icons/civ/ctrl/space" };
        private static readonly string[] TabLayer = { null, "research", "politics", "economy", "war", "space" };
        private static int _tab;
        private static Kingdom _pick;
        private static int _page;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open(Kingdom k)
        {
            _pick = k;
            _page = 0;
            UiKit.Open(Id);
        }

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
        }

        public override void OnNormalEnable()
        {
            Render();
        }

        private void Render()
        {
            Transform c = ContentTransform;
            UiKit.Clear(c);
            Kingdom k = Player.K;
            if (!G.Ok(k))
            {
                Choose(c);
                return;
            }
            UiKit.Header(c, G.Name(k), UiKit.Gold, "ui/Icons/civ/btn/control");
            UiKit.Tabs(c, TabKeys, TabIcons, _tab, i =>
            {
                _tab = i;
                _page = 0;
                Render();
            });
            string layer = TabLayer[_tab];
            if (layer != null)
            {
                bool auto = Player.Auto(k, layer);
                RectTransform row = UiKit.Box(c, "Auto", 22f);
                UiKit.Btn(row, Txt.T(auto ? "civ_ctrl_auto_on" : "civ_ctrl_auto_off"), null, () =>
                {
                    Player.SetAuto(k, layer, !auto);
                    Bonus.Dirty(k);
                    Render();
                }, new Vector2(UiKit.W - 20f, 20f), Vector2.zero, Txt.T("civ_ctrl_auto"), Txt.T("civ_ctrl_auto_d"), auto ? new Color(0.6f, 0.6f, 0.65f) : new Color(1f, 0.85f, 0.45f));
            }
            switch (_tab)
            {
                case 0: Overview(c, k); break;
                case 1: ResearchTab(c, k); break;
                case 2: PoliticsTab(c, k); break;
                case 3: EconomyTab(c, k); break;
                case 4: WarTab(c, k); break;
                case 5: SpaceTab(c, k); break;
            }
        }

        private void Choose(Transform c)
        {
            UiKit.Header(c, Txt.T("civ_ctrl_title"), UiKit.Gold, "ui/Icons/civ/btn/control");
            UiKit.Paragraph(c, Txt.T("civ_ctrl_intro"), 8, UiKit.Soft);
            Kingdom k = G.Ok(_pick) && _pick.isCiv() ? _pick : SelectedMetas.selected_kingdom;
            if (!G.Ok(k) || !k.isCiv())
            {
                UiKit.Label(c, Txt.T("civ_ctrl_select"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            RectTransform row = UiKit.Box(c, "Pick", 26f);
            UiKit.Banner(row, k, new Vector2(-UiKit.W / 2f + 16f, 0f), 18f);
            UiKit.Btn(row, Txt.F("civ_ctrl_take", G.Name(k)), null, () =>
            {
                Player.Control(k);
                _tab = 0;
                Render();
            }, new Vector2(UiKit.W - 44f, 22f), new Vector2(14f, 0f), null, null, new Color(1f, 0.85f, 0.45f));
        }

        private static int Gold(Kingdom k)
        {
            int g = 0;
            foreach (City c in G.Cities(k)) g += Economy.Amount(c, "gold");
            return g;
        }

        private void Overview(Transform c, Kingdom k)
        {
            UiKit.Stat(c, null, Txt.T("civ_ctrl_gold"), Gold(k).ToString(), UiKit.Gold);
            UiKit.Stat(c, null, Txt.T("civ_ctrl_stability"), Mathf.RoundToInt(Politics.Stability(k) * 100f) + "%");
            UiKit.Stat(c, null, Txt.T("civ_ctrl_legitimacy"), Mathf.RoundToInt(Legitimacy.Get(k) * 100f) + "%");
            UiKit.Stat(c, null, Txt.T("civ_ctrl_era"), Db.EraName(Research.Era(k)), UiKit.Cyan);
            UiKit.Stat(c, null, Txt.T("civ_row_age"), Txt.T("civ_age_" + Sagas.Age(k)), Sagas.Age(k) == "dark" ? UiKit.Bad : UiKit.Gold);
            UiKit.Stat(c, null, Txt.T("civ_ctrl_moments"), Sagas.Moments(k).ToString());
            UiKit.Stat(c, null, Txt.T("civ_ctrl_pop"), k.getPopulationPeople().ToString());
            UiKit.Stat(c, null, Txt.T("civ_ctrl_cities"), G.Cities(k).Count().ToString());
            int wars = 0;
            foreach (global::War w in World.world.wars.getWars(k)) if (w != null && !w.isRekt()) wars++;
            UiKit.Stat(c, null, Txt.T("civ_ctrl_wars"), wars.ToString(), wars > 0 ? UiKit.Bad : UiKit.Good);
            UiKit.Header(c, Txt.T("civ_ctrl_victory_title"), UiKit.Gold);
            foreach (string v in Player.Victories)
            {
                float p = Player.VictoryProgress(k, v);
                bool won = Player.HasWon(v);
                UiKit.Bar(UiKit.Box(c, "Vic", 14f), won ? 1f : p, won ? new Color(1f, 0.8f, 0.3f) : new Color(0.5f, 0.75f, 1f), new Vector2(UiKit.W - 20f, 11f), Vector2.zero, Txt.T("civ_ctrl_vic_" + v) + "  " + (won ? "✓" : Mathf.RoundToInt(p * 100f) + "%"));
            }
            UiKit.Gap(c, 6f);
            RectTransform row = UiKit.Box(c, "Release", 22f);
            UiKit.Btn(row, Txt.T("civ_ctrl_release"), null, () =>
            {
                Player.Release();
                Render();
            }, new Vector2(UiKit.W - 40f, 20f), Vector2.zero, null, null, new Color(1f, 0.55f, 0.5f));
        }

        private void ResearchTab(Transform c, Kingdom k)
        {
            TechDef cur = Research.Current(k);
            UiKit.Stat(c, cur != null ? cur.Icon : null, Txt.T("civ_researching"), cur != null ? Db.TechName(cur.Id) : Txt.T("civ_researching_none"), UiKit.Cyan);
            if (cur != null)
            {
                float rp = Mathf.Clamp01(Research.Progress(k) / Math.Max(1f, Research.Cost(k, cur)));
                UiKit.Bar(UiKit.Box(c, "Prog", 14f), rp, new Color(0.35f, 0.75f, 1f), new Vector2(UiKit.W - 20f, 10f), Vector2.zero, Mathf.RoundToInt(rp * 100f) + "%");
            }
            TechDef q = Db.Tech(Player.Queued(k));
            if (q != null) UiKit.Stat(c, q.Icon, Txt.T("civ_ctrl_next"), Db.TechName(q.Id), UiKit.Gold);
            UiKit.Header(c, Txt.T("civ_ctrl_cards"), UiKit.Gold);
            foreach (TechDef t in Player.Cards(k)) TechRow(c, k, t);
            UiKit.Header(c, Txt.T("civ_ctrl_all_techs"), UiKit.Soft);
            List<TechDef> all = Research.Candidates(k).OrderBy(t => t.Era).ThenBy(t => Research.Cost(k, t)).ToList();
            int per = 10;
            int pages = Math.Max(1, (all.Count + per - 1) / per);
            _page = Math.Min(_page, pages - 1);
            foreach (TechDef t in all.Skip(_page * per).Take(per)) TechRow(c, k, t);
            UiKit.Pager(c, _page, pages, p =>
            {
                _page = p;
                Render();
            });
        }

        private void TechRow(Transform c, Kingdom k, TechDef t)
        {
            RectTransform row = UiKit.Box(c, "Tech", 20f);
            UiKit.Img(row, UiKit.Icon(t.Icon, "ui/Icons/civ/cat/" + t.Cat), new Vector2(16f, 16f), new Vector2(-UiKit.W / 2f + 12f, 0f));
            UiKit.Btn(row, Db.TechName(t.Id) + "  <color=#9AA>" + Mathf.RoundToInt(Research.Cost(k, t)) + "</color>", null, () =>
            {
                Player.Queue(k, t);
                Render();
            }, new Vector2(UiKit.W - 30f, 18f), new Vector2(10f, 0f), Db.TechName(t.Id), TechInfo.Describe(t, k));
        }

        private void PoliticsTab(Transform c, Kingdom k)
        {
            int ph = Statecraft.Phase(k);
            if (ph >= 0) UiKit.Stat(c, null, Txt.T("civ_ctrl_dynasty"), Txt.T("civ_dyn_" + Statecraft.Phases[ph]), ph <= 1 ? UiKit.Good : UiKit.Bad);
            UiKit.Header(c, Txt.T("civ_ctrl_parliament"), UiKit.Gold);
            string[] fs = Statecraft.Factions(Research.Era(k));
            float tot = 0f;
            foreach (string f in fs) tot += Statecraft.Weight(k, f);
            foreach (string f in fs) UiKit.Bar(UiKit.Box(c, "Fac", 13f), Statecraft.Weight(k, f) / Math.Max(0.01f, tot), new Color(0.75f, 0.6f, 1f), new Vector2(UiKit.W - 20f, 10f), Vector2.zero, Txt.T("civ_fac_" + f));
            if (Statecraft.Blocked(k)) UiKit.Label(c, Txt.T("civ_ctrl_deadlock"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Bad);
            string[] lv = Statecraft.LastVote(k).Split('|');
            if (lv.Length == 3) UiKit.Label(c, Txt.F(lv[1] == "1" ? "civ_ctrl_vote_yes" : "civ_ctrl_vote_no", Txt.T("civ_p_" + lv[0]), lv[2]), 14f, 7, TextAnchor.MiddleCenter, lv[1] == "1" ? UiKit.Good : UiKit.Bad);
            foreach (string g in Policies.Groups)
            {
                PolicyDef cur = Policies.Get(k, g);
                UiKit.Header(c, Txt.T("civ_pg_" + g), UiKit.Gold);
                foreach (PolicyDef p in Policies.Options(k, g))
                {
                    bool on = cur != null && cur.Id == p.Id;
                    RectTransform row = UiKit.Box(c, "Pol", 18f);
                    UiKit.Btn(row, (on ? "» " : "") + Txt.T("civ_p_" + p.Id), null, () =>
                    {
                        Policies.Set(k, g, p.Id, "player");
                        Render();
                    }, new Vector2(UiKit.W - 20f, 16f), Vector2.zero, Txt.T("civ_p_" + p.Id), Txt.T("civ_p_" + p.Id + "_d"), on ? new Color(1f, 0.85f, 0.45f) : (Color?)null);
                }
            }
        }

        private void EconomyTab(Transform c, Kingdom k)
        {
            UiKit.Stat(c, null, Txt.T("civ_ctrl_gold"), Gold(k).ToString(), UiKit.Gold);
            int tax = Player.Tax(k);
            UiKit.Selector(c, Txt.T("civ_ctrl_tax_" + tax), null, () =>
            {
                Player.SetTax(k, tax - 1);
                Render();
            }, () =>
            {
                Player.SetTax(k, tax + 1);
                Render();
            });
            UiKit.Paragraph(c, Txt.T("civ_ctrl_tax_d"), 7, UiKit.Dim);
            string b = Player.Budget(k);
            int bi = Math.Max(0, Array.IndexOf(Player.Budgets, b));
            UiKit.Selector(c, Txt.T("civ_ctrl_budget_" + b), null, () =>
            {
                Player.SetBudget(k, Player.Budgets[(bi + Player.Budgets.Length - 1) % Player.Budgets.Length]);
                Render();
            }, () =>
            {
                Player.SetBudget(k, Player.Budgets[(bi + 1) % Player.Budgets.Length]);
                Render();
            });
            UiKit.Paragraph(c, Txt.T("civ_ctrl_budget_d"), 7, UiKit.Dim);
            UiKit.Header(c, Txt.T("civ_ctrl_ledger"), UiKit.Gold);
            float[] l = Ledger.Last(k);
            if (l == null)
            {
                UiKit.Label(c, Txt.T("civ_ctrl_ledger_none"), 14f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            float tin = 0f, tout = 0f;
            for (int i = 0; i < Ledger.Flows.Length; i++)
            {
                float a = l[i * 2], o = l[i * 2 + 1];
                tin += a;
                tout += o;
                if (a <= 0f && o <= 0f) continue;
                UiKit.Stat(c, null, Txt.T("civ_flow_" + Ledger.Flows[i]), "<color=#8E8>+" + Mathf.RoundToInt(a) + "</color>  <color=#E88>-" + Mathf.RoundToInt(o) + "</color>");
            }
            float net = tin - tout;
            UiKit.Stat(c, null, Txt.T("civ_ctrl_net"), (net >= 0f ? "+" : "") + Mathf.RoundToInt(net), net >= 0f ? UiKit.Good : UiKit.Bad);
        }

        private void WarTab(Transform c, Kingdom k)
        {
            string s = Player.Stance(k);
            int si = Math.Max(0, Array.IndexOf(Player.Stances, s));
            UiKit.Selector(c, Txt.T("civ_ctrl_stance_" + s), null, () =>
            {
                Player.SetStance(k, Player.Stances[(si + Player.Stances.Length - 1) % Player.Stances.Length]);
                Render();
            }, () =>
            {
                Player.SetStance(k, Player.Stances[(si + 1) % Player.Stances.Length]);
                Render();
            });
            UiKit.Stat(c, null, Txt.T("civ_ctrl_army"), Mathf.RoundToInt(Player.Strength(k)).ToString());
            float ex = Statecraft.Exhaustion(k), ae = Statecraft.Aggressive(k);
            UiKit.Stat(c, null, Txt.T("civ_ctrl_exhaustion"), Mathf.RoundToInt(ex) + "%", ex > 50f ? UiKit.Bad : UiKit.Soft);
            UiKit.Stat(c, null, Txt.T("civ_ctrl_ae"), Mathf.RoundToInt(ae).ToString(), ae >= 30f ? UiKit.Bad : UiKit.Soft);
            if (Statecraft.Crises(k) > 0) UiKit.Label(c, Txt.F("civ_ctrl_crises", Statecraft.Crises(k)), 14f, 8, TextAnchor.MiddleCenter, UiKit.Bad);
            UiKit.Header(c, Txt.T("civ_ctrl_known"), UiKit.Gold);
            var known = Fog.KnownKingdoms(k).Where(o => G.Ok(o) && o != k && o.isCiv()).ToList();
            if (known.Count == 0) UiKit.Label(c, Txt.T("civ_ctrl_none_known"), 16f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            int per = 12;
            int pages = Math.Max(1, (known.Count + per - 1) / per);
            _page = Math.Min(_page, pages - 1);
            foreach (Kingdom o in known.Skip(_page * per).Take(per))
            {
                Kingdom other = o;
                bool war = World.world.wars.isInWarWith(k, o);
                RectTransform row = UiKit.Box(c, "K", 20f);
                UiKit.Banner(row, o, new Vector2(-UiKit.W / 2f + 10f, 0f), 14f);
                global::War fw = war ? Statecraft.Find(k, o) : null;
                string ws = fw != null ? "  <color=#" + (Statecraft.Score(k, fw) >= 0f ? "8E8" : "E88") + ">" + Mathf.RoundToInt(Statecraft.Score(k, fw)) + "</color>" : "";
                UiKit.Text(row, G.Name(o) + "  <color=#9AA>" + Mathf.RoundToInt(Player.Strength(o)) + "</color>" + ws, new Vector2(-20f, 0f), new Vector2(130f, 16f), 8, TextAnchor.MiddleLeft, war ? UiKit.Bad : UiKit.Soft);
                UiKit.Btn(row, Txt.T(war ? "civ_ctrl_peace" : "civ_ctrl_war"), null, () =>
                {
                    if (war) Player.OfferPeace(other);
                    else Player.DeclareWar(other);
                    Render();
                }, new Vector2(54f, 16f), new Vector2(UiKit.W / 2f - 30f, 0f), null, null, war ? new Color(0.6f, 0.9f, 0.6f) : new Color(1f, 0.55f, 0.5f));
            }
            UiKit.Pager(c, _page, pages, p =>
            {
                _page = p;
                Render();
            });
        }

        private void SpaceTab(Transform c, Kingdom k)
        {
            if (!Cosmos.Ready || Research.Era(k) < 15)
            {
                UiKit.Label(c, Txt.T("civ_ctrl_space_locked"), 30f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            FleetMix f = Spacecraft.Mix(k);
            UiKit.Stat(c, null, Txt.T("cos_fleet"), Mathf.RoundToInt(CosmosWar.Strength(k)) + "  (" + f.Ships + ")");
            UiKit.Stat(c, null, Txt.T("cos_mega_admin"), Megastructures.Used(k) + " / " + Megastructures.Admin(k));
            MegaDef want = Megastructures.Def(Store.Str(k.data, "civ_player_mega"));
            if (want != null) UiKit.Stat(c, "ui/Icons/civ/mega/" + want.Id, Txt.T("civ_ctrl_next"), Txt.T("cos_mega_" + want.Id), UiKit.Gold);
            UiKit.Header(c, Txt.T("civ_ctrl_megas"), UiKit.Gold);
            var list = Megastructures.Defs.Where(d => Research.HasUnlock(k, "mega:" + d.Id)).ToList();
            if (list.Count == 0) UiKit.Label(c, Txt.T("civ_ctrl_no_megas"), 16f, 8, TextAnchor.MiddleCenter, UiKit.Dim);
            int per = 12;
            int pages = Math.Max(1, (list.Count + per - 1) / per);
            _page = Math.Min(_page, pages - 1);
            foreach (MegaDef d in list.Skip(_page * per).Take(per))
            {
                MegaDef md = d;
                int have = Megastructures.Of(k).Count(m => m.Id == d.Id);
                RectTransform row = UiKit.Box(c, "M", 18f);
                UiKit.Btn(row, Txt.T("cos_mega_" + d.Id) + "  <color=#9AA>" + have + "/" + d.Max + "</color>", null, () =>
                {
                    Player.QueueMega(k, md);
                    Render();
                }, new Vector2(UiKit.W - 20f, 16f), Vector2.zero, Txt.T("cos_mega_" + d.Id), Fx(d), have >= d.Max ? new Color(0.5f, 0.5f, 0.55f) : (Color?)null);
            }
            UiKit.Pager(c, _page, pages, p =>
            {
                _page = p;
                Render();
            });
        }

        private static string Fx(MegaDef d)
        {
            return string.Join(", ", d.Fx.Select(kv => Txt.T("civ_eff_" + kv.Key) + " " + (kv.Value > 0 ? "+" : "") + kv.Value.ToString("0")).ToArray()) + "\n" + Txt.F("cos_mega_stage", d.Stages, d.Stages);
        }
    }
}
