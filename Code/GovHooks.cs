using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class GovHooks
    {
        private static bool _done;
        private static Kingdom _plate;
        private static readonly List<string> _legacy = new List<string>();

        public static void Legacy()
        {
            var groups = new Dictionary<string, string[]>
            {
                { "civ_tg_phase", new[] { "civ_ph_renewal", "civ_ph_peak", "civ_ph_decline", "civ_ph_collapse" } },
                { "civ_tg_vassal", new[] { "civ_vs_0", "civ_vs_1", "civ_vs_2", "civ_vs_3" } },
                { "civ_tg_regime", new[] { "civ_rgt_feudal", "civ_rgt_absolutist", "civ_rgt_militarist", "civ_rgt_confessional", "civ_rgt_bureaucratic", "civ_rgt_federal", "civ_rgt_expansionist", "civ_rgt_isolationist", "civ_rgt_mercantile" } }
            };
            foreach (var g in groups)
            {
                if (AssetManager.kingdoms_traits_groups.get(g.Key) == null) AssetManager.kingdoms_traits_groups.add(new KingdomTraitGroupAsset { id = g.Key, name = g.Key, color = "#888888" });
                foreach (string id in g.Value)
                {
                    _legacy.Add(id);
                    if (AssetManager.kingdoms_traits.get(id) != null) continue;
                    AssetManager.kingdoms_traits.add(new KingdomTrait
                    {
                        id = id,
                        group_id = g.Key,
                        spawn_random_trait_allowed = false,
                        needs_to_be_explored = false,
                        special_locale_id = id,
                        special_locale_description = id + "_d",
                        special_locale_description_2 = id + "_d2",
                        path_icon = "ui/Icons/civ/pol/legacy",
                        base_stats = new BaseStats(),
                        can_be_in_book = false
                    });
                }
            }
        }

        public static void Strip(Kingdom k)
        {
            foreach (string id in _legacy) if (k.hasTrait(id)) k.removeTrait(id);
        }

        public static void InitAssets()
        {
            if (_done) return;
            _done = true;
            Loyalty("civ_g2_mandate", Mandate.Loyalty);
            Loyalty("civ_g2_policies", Parl.CityLoyalty);
            Loyalty("civ_g2_treasury", Treasury.CityLoyalty);
            if (AssetManager.opinion_library.get("civ_g2_vassal") == null)
            {
                AssetManager.opinion_library.add(new OpinionAsset
                {
                    id = "civ_g2_vassal",
                    translation_key = "opinion_civ_g2_vassal",
                    translation_key_negative = "opinion_civ_g2_vassal_bad",
                    calc = delegate(Kingdom a, Kingdom b)
                    {
                        if (a == null || b == null || a == b) return 0;
                        try
                        {
                            return Vassals.Opinion(a, b);
                        }
                        catch (Exception)
                        {
                            return 0;
                        }
                    }
                });
            }
        }

        private static void Loyalty(string id, Func<City, int> f)
        {
            if (AssetManager.loyalty_library.get(id) != null) return;
            AssetManager.loyalty_library.add(new LoyaltyAsset
            {
                id = id,
                translation_key = "loyalty_" + id,
                translation_key_negative = "loyalty_" + id + "_bad",
                calc = delegate(City c)
                {
                    if (c == null || c.kingdom == null || !c.kingdom.isCiv()) return 0;
                    try
                    {
                        return f(c);
                    }
                    catch (Exception)
                    {
                        return 0;
                    }
                }
            });
        }

        private static void Line(Tooltip t, string key, string value, string color = "#FFFFFF")
        {
            if (t == null || string.IsNullOrEmpty(value)) return;
            t.addLineText(Txt.T(key), value, color, false, false, 21);
        }

        public static void KingdomTip(Tooltip t, Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv() || KingdomUi.Hidden(k)) return;
            Line(t, "civ_gov_tip_form", Gov.FormName(k) + " · " + Parl.Regime(k), "#FFD27A");
            if (k.hasKing()) Line(t, "civ_gov_tip_ruler", Gov.RulerName(k));
            if (Mandate.Active(k)) Line(t, "civ_gov_tip_mandate", Mandate.PhaseName(Mandate.Phase(k)) + " " + (int)Mandate.Get(k), Mandate.Get(k) >= 50f ? "#8CF07A" : "#FF9A6A");
            Kingdom lord = Diplomacy.Overlord(k);
            if (G.Ok(lord)) Line(t, "civ_gov_tip_lord", G.Name(lord) + " · " + Vassals.StateName(Vassals.State(k)), "#9AC8E8");
            int deps = Diplomacy.DependentsOf(k).Count;
            if (deps > 0) Line(t, "civ_gov_tip_vassals", deps.ToString(), "#9AC8E8");
            if (Treasury.Coin(k)) Line(t, "civ_gov_tip_treasury", ((int)Treasury.Gold(k)).ToString() + (Treasury.Owed(k) > 0f ? " !" : ""), Treasury.Owed(k) > 0f ? "#FF7A6A" : "#FFD25C");
        }

        public static void CityTip(Tooltip t, City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !c.kingdom.isCiv() || KingdomUi.Hidden(c.kingdom)) return;
            if (Parl.Active(c.kingdom) && GovEra.Has(c.kingdom, GovEra.Nobles))
            {
                long h = Fam.Holder(c, c.kingdom);
                Line(t, "civ_gov_tip_seats", Fam.SeatsOf(c) + " · " + Fam.Name(h));
            }
            var w = Treasury.Works(c);
            if (w.Count > 0) Line(t, "civ_gov_tip_works", string.Join(", ", w.Select(x => Txt.T("civ_treas_work_" + x.Key) + " ×" + (int)x.Value).ToArray()));
            if (Treasury.LocalUnpaid(c)) Line(t, "civ_gov_tip_unpaid", Txt.T("civ_yes"), "#FF7A6A");
        }

        public static void WarTip(Tooltip t, global::War w)
        {
            if (w == null || w.data == null || w.hasEnded()) return;
            float s = Statecraft.Score(w);
            string lead = Math.Abs(s) < 15f ? Txt.T("civ_war_stalemate") : Txt.F(s > 0 ? "civ_war_att_ahead" : "civ_war_def_ahead", (int)Math.Abs(s));
            Line(t, "civ_gov_tip_score", lead, s >= 0 ? "#8CF07A" : "#FF7A6A");
            int age = Statecraft.WarAge(w);
            Line(t, "civ_gov_tip_talks", age >= 3 ? Txt.T("civ_war_talks_open") : Txt.F("civ_war_talks_in", 3 - age));
        }

        [HarmonyPatch(typeof(TooltipLibrary), nameof(TooltipLibrary.showKingdom))]
        private static class TipKingdom
        {
            private static void Postfix(Tooltip pTooltip, TooltipData pData)
            {
                if (pData == null) return;
                CivMod.Safe("tip-kingdom", () => KingdomTip(pTooltip, pData.kingdom));
            }
        }

        [HarmonyPatch(typeof(TooltipLibrary), nameof(TooltipLibrary.showCity), new[] { typeof(string), typeof(Tooltip), typeof(TooltipData) })]
        private static class TipCity
        {
            private static void Postfix(Tooltip pTooltip, TooltipData pData)
            {
                if (pData == null) return;
                CivMod.Safe("tip-city", () => CityTip(pTooltip, pData.city));
            }
        }

        [HarmonyPatch(typeof(TooltipLibrary), nameof(TooltipLibrary.showWar))]
        private static class TipWar
        {
            private static void Postfix(Tooltip pTooltip, TooltipData pData)
            {
                if (pData == null) return;
                CivMod.Safe("tip-war", () => WarTip(pTooltip, pData.war));
            }
        }

        [HarmonyPatch(typeof(NameplateText), nameof(NameplateText.showTextKingdom))]
        private static class PlateKingdom
        {
            private static void Prefix(Kingdom pMetaObject)
            {
                _plate = pMetaObject;
            }

            private static void Postfix()
            {
                _plate = null;
            }
        }

        [HarmonyPatch(typeof(NameplateText), nameof(NameplateText.setText))]
        private static class PlateText
        {
            private static void Prefix(ref string pNewText)
            {
                try
                {
                    if (_plate != null && Gov.On("civ_gov_names") && G.Ok(_plate) && _plate.isCiv() && !KingdomUi.Hidden(_plate) && GovEra.Has(_plate, GovEra.Nobles)) pNewText = pNewText + " · " + Gov.FormName(_plate);
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(ZoneCalculator), nameof(ZoneCalculator.applyMetaColorsToZone))]
        private static class VassalBorder
        {
            private static void Prefix(ZoneCalculator __instance, TileZone pZone, ref Color32 pColorOutside)
            {
                if (pZone == null || pZone.city == null || __instance._mode_asset == null || __instance._mode_asset.map_mode != MetaType.Kingdom) return;
                Kingdom k = pZone.city.kingdom;
                if (k == null || k.data == null || !Gov.On("civ_gov_vassals")) return;
                try
                {
                    Kingdom lord = Diplomacy.Overlord(k);
                    if (lord == null || lord.getColor() == null) return;
                    pColorOutside = lord.getColor()._color_main_32;
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
