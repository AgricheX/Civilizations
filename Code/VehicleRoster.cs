using System;

namespace Civilizations
{
    internal static class VehicleRoster
    {
        public static string Duty(UnitDef d)
        {
            if (d == null) return "";
            if (d.Role == "unique") return "unique";
            switch (d.Base ?? d.Id)
            {
                case "civ_war_elephant": return "elephant";
                case "civ_cart":
                case "civ_truck":
                case "civ_steam_truck":
                case "civ_diesel_truck": return "cargo";
                case "civ_chariot":
                case "civ_horse":
                case "civ_bio_crawler": return "cavalry";
                case "civ_apc":
                case "civ_ifv": return "apc";
                case "civ_drone":
                case "civ_light_drone": return "air_drone";
                case "civ_attack_helicopter":
                case "civ_jp_heli": return "air_attack_heli";
                case "civ_carrier":
                case "civ_jp_carrier": return "ship_carrier";
                case "civ_submarine":
                case "civ_giant_sub": return "ship_submarine";
                case "civ_nuclear_sub": return "ship_nuclear_submarine";
                case "civ_destroyer":
                case "civ_missile_destroyer": return "ship_escort";
            }
            if (d.Cls == "soldier") return "sol_" + d.Role;
            if (d.Cls == "air") return "air_" + d.Role;
            if (d.Cls == "space") return "space_" + d.Role;
            if (d.Cls == "ship") return d.Role == "trade" ? "ship_trade" : d.Role == "fish" ? "ship_fish" : "ship_surface";
            if (d.Cls == "land") return "passenger";
            return d.Cls;
        }

        public static int RetirementEra(UnitDef d)
        {
            if (d == null) return 0;
            if (d.Role == "unique") return Rarities.Retire(d);
            switch (d.Base ?? d.Id)
            {
                case "civ_war_elephant": return 8;
                case "civ_chariot": return 7;
                case "civ_cart":
                case "civ_carriage": return 11;
                case "civ_horse": return 12;
                case "civ_ram":
                case "civ_siege_tower":
                case "civ_ballista":
                case "civ_catapult":
                case "civ_trebuchet":
                case "civ_bombard": return 10;
                case "civ_cannon":
                case "civ_field_gun":
                case "civ_balloon":
                case "civ_biplane": return 12;
                case "civ_fighter": return 13;
                case "civ_steamer": return 12;
                case "civ_fish_canoe": return 7;
                case "civ_fish_sail": return 10;
                case "civ_fish_smack": return 11;
                case "civ_fish_steam": return 13;
            }
            if (d.Cls == "ship" && d.Era < 10) return 11;
            if (d.Id == "civ_ironclad") return 12;
            if (d.Id == "civ_dreadnought") return 14;
            return int.MaxValue;
        }

        public static bool InService(UnitDef d, int era)
        {
            return d != null && era < RetirementEra(d);
        }

        public static bool Relevant(UnitDef d, int era, int newestEra)
        {
            if (!InService(d, era)) return false;
            if (d.Cls == "soldier") return string.IsNullOrEmpty(d.Route) ? d.Era + 1 >= newestEra : true;
            bool olderDesign = d.Cls == "siege" && d.Era < 9 || d.Cls == "ship" || d.Cls == "space";
            return !olderDesign || !string.IsNullOrEmpty(d.Route) || d.Era + 1 >= newestEra;
        }

        public static float Weight(UnitDef d, int newestEra)
        {
            return 1f / (1f + Math.Max(0, newestEra - d.Era) * 0.65f);
        }

        public static float ShipShare(string duty)
        {
            switch (duty)
            {
                case "ship_escort": return 1.4f;
                case "ship_submarine": return 0.8f;
                case "ship_nuclear_submarine": return 0.7f;
                case "ship_carrier": return 0.8f;
                default: return 1f;
            }
        }

        public static string Track(string kind)
        {
            return kind == "maglev" || kind == "monorail" || kind == "gyro" ? kind : "rail";
        }
    }
}
