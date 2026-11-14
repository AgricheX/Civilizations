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
        private static Kingdom _plateK;
        private static Alliance _plateA;

        public static void InitAssets()
        {
            if (_done) return;
            _done = true;
            Loyalty("civ_mandate", c => Mandate.Loyalty(c));
            Loyalty("civ_decree", c => Gov.On("civ_gov_parliament") ? Parliament.DecreePenalty(c) : 0);
            Loyalty("civ_clan_seat", c => Gov.On("civ_gov_parliament") ? Parliament.ClanLoyalty(c) : 0);
            Loyalty("civ_temples", c => Treasury.TempleLoyalty(c));
            Loyalty("civ_unpaid", c => c != null && c.kingdom != null && Gov.On("civ_gov_treasury") && Treasury.Unpaid(c.kingdom) > 0 ? -10 : 0);
            if (AssetManager.opinion_library.get("civ_alliance_bond") == null)
            {
                AssetManager.opinion_library.add(new OpinionAsset
                {
                    id = "civ_alliance_bond",
                    translation_key = "opinion_civ_alliance_bond",
                    translation_key_negative = "opinion_civ_alliance_bond_bad",
                    calc = delegate(Kingdom a, Kingdom b) { return a == null || b == null || a == b ? 0 : AllianceLife.Opinion(a, b); }
                });
            }
            if (AssetManager.opinion_library.get("civ_vassal_bond") == null)
            {
                AssetManager.opinion_library.add(new OpinionAsset
                {
                    id = "civ_vassal_bond",
                    translation_key = "opinion_civ_vassal_bond",
                    translation_key_negative = "opinion_civ_vassal_bond_bad",
                    calc = delegate(Kingdom a, Kingdom b)
                    {
                        if (a == null || b == null || a == b || !Gov.On("civ_gov_vassals")) return 0;
                        if (Diplomacy.Overlord(a) == b) return (int)((Vassals.Integration(a) - 50f) / 2.5f);
                        if (Diplomacy.Overlord(b) == a) return 5;
                        return 0;
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
            Line(t, "civ_tip_regime", Laws.Regime(k), "#FFD27A");
            if (k.hasKing()) Line(t, "civ_tip_ruler", Gov.RulerName(k));
            if (Gov.On("civ_gov_cycle") && Statecraft.Phase(k) >= 0) Line(t, "civ_tip_mandate", Txt.T("civ_phase_" + Statecraft.Phases[Statecraft.Phase(k)]) + " " + (int)Mandate.Get(k), Mandate.Get(k) >= 50f ? "#8CF07A" : "#FF9A6A");
            if (Parliament.Active(k) && Profile.Get(k).Era >= 3 && Parliament.Total(k) > 0) Line(t, "civ_tip_council", Gov.Council(k) + " " + G.Pct(Parliament.RoyalShare(k)));
            Kingdom lord = Diplomacy.Overlord(k);
            if (G.Ok(lord)) Line(t, "civ_tip_bond", Vassals.BondName(k) + " · " + G.Name(lord) + " · " + (int)Vassals.Integration(k), "#9AC8E8");
            int deps = Diplomacy.DependentsOf(k).Count;
            if (deps > 0) Line(t, "civ_tip_vassals", deps.ToString(), "#9AC8E8");
            Alliance a = AllianceLife.Of(k);
            if (a != null && Gov.On("civ_gov_alliances")) Line(t, "civ_tip_alliance", AllianceLife.Label(a) + " · " + (int)AllianceLife.Cohesion(a));
            if (Gov.On("civ_gov_treasury")) Line(t, "civ_tip_treasury", ((int)Treasury.Reserve(k)).ToString() + (Treasury.Unpaid(k) > 0 ? " !" : ""), Treasury.Unpaid(k) > 0 ? "#FF7A6A" : "#FFD25C");
        }

        public static void CityTip(Tooltip t, City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !c.kingdom.isCiv() || KingdomUi.Hidden(c.kingdom)) return;
            if (Parliament.Active(c.kingdom) && Profile.Get(c.kingdom).Era >= 3)
            {
                long h = Parliament.Holder(c, c.kingdom);
                Line(t, "civ_tip_seats", Parliament.SeatsFor(c) + " · " + Gov.ClanName(h));
                if (h != 0L) Line(t, "civ_tip_faction", Parliament.FactionOf(c.kingdom, h));
                int dp = Parliament.DecreePenalty(c);
                if (dp != 0) Line(t, "civ_tip_decree", dp.ToString(), "#FF7A6A");
            }
            if (Gov.On("civ_gov_treasury"))
            {
                WorksCount w = Treasury.Works(c);
                int n = w.Market + w.Temple + w.Barracks + w.Tower + w.Statue + w.Windmill;
                if (n > 0) Line(t, "civ_tip_works", n.ToString());
            }
        }

        public static void WarTip(Tooltip t, global::War w)
        {
            if (w == null || w.data == null || w.hasEnded()) return;
            float s = Statecraft.Score(w);
            Line(t, "civ_tip_warscore", G.Signed(s), s >= 0 ? "#8CF07A" : "#FF7A6A");
            int age = Statecraft.WarAge(w);
            Line(t, "civ_tip_talks", age >= 3 ? Txt.T("civ_talks_open") : Txt.F("civ_talks_in", 3 - age));
            string goal = WarGoals.Get(w);
            if (!string.IsNullOrEmpty(goal)) Line(t, "civ_tip_goal", Txt.T("civ_goal_" + goal));
        }

        public static void AllianceTip(Tooltip t, Alliance a)
        {
            if (a == null || a.data == null || !Gov.On("civ_gov_alliances")) return;
            Line(t, "civ_tip_altype", AllianceLife.Label(a), "#FFD27A");
            float c = AllianceLife.Cohesion(a);
            Line(t, "civ_tip_cohesion", ((int)c).ToString(), c >= 50f ? "#8CF07A" : c >= 25f ? "#FFD25C" : "#FF7A6A");
            Kingdom lead = AllianceLife.Leader(a);
            if (lead != null) Line(t, "civ_tip_alleader", G.Name(lead));
            string why = AllianceLife.Reason(a);
            if (why.Length > 0) Line(t, "civ_tip_alreason", Txt.T("civ_alwhy_" + why));
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

        [HarmonyPatch(typeof(TooltipLibrary), nameof(TooltipLibrary.showAlliance))]
        private static class TipAlliance
        {
            private static void Postfix(Tooltip pTooltip, TooltipData pData)
            {
                if (pData == null) return;
                CivMod.Safe("tip-alliance", () => AllianceTip(pTooltip, pData.alliance));
            }
        }

        [HarmonyPatch(typeof(NameplateText), nameof(NameplateText.showTextKingdom))]
        private static class PlateKingdom
        {
            private static void Prefix(Kingdom pMetaObject)
            {
                _plateK = pMetaObject;
            }

            private static void Postfix()
            {
                _plateK = null;
            }
        }

        [HarmonyPatch(typeof(NameplateText), nameof(NameplateText.showTextAlliance))]
        private static class PlateAlliance
        {
            private static void Prefix(Alliance pMetaObject)
            {
                _plateA = pMetaObject;
            }

            private static void Postfix()
            {
                _plateA = null;
            }
        }

        [HarmonyPatch(typeof(NameplateText), nameof(NameplateText.setText))]
        private static class PlateText
        {
            private static void Prefix(ref string pNewText)
            {
                try
                {
                    if (_plateK != null && Gov.On("civ_gov_names") && G.Ok(_plateK) && _plateK.isCiv() && !KingdomUi.Hidden(_plateK))
                    {
                        string gov = Txt.T("civ_p_" + Policies.Id(_plateK, "gov"));
                        if (!string.IsNullOrEmpty(gov)) pNewText = pNewText + " · " + gov;
                    }
                    else if (_plateA != null && Gov.On("civ_gov_alliances"))
                    {
                        string l = AllianceLife.Label(_plateA);
                        if (l.Length > 0) pNewText = pNewText + " · " + l;
                    }
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
