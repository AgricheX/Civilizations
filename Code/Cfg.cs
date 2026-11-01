using System;
using NeoModLoader.api;

namespace Civilizations
{
    internal class Cfg
    {
        private readonly ModConfig _cfg;

        public Cfg(ModConfig cfg)
        {
            _cfg = cfg;
        }

        private ModConfigItem Item(string group, string id)
        {
            try
            {
                return _cfg[group][id];
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool B(string g, string id, bool def)
        {
            ModConfigItem i = Item(g, id);
            return i == null ? def : i.BoolVal;
        }

        private int I(string g, string id, int def)
        {
            ModConfigItem i = Item(g, id);
            return i == null ? def : i.IntVal;
        }

        private float F(string g, string id, float def)
        {
            ModConfigItem i = Item(g, id);
            return i == null ? def : i.FloatVal;
        }

        public float Pace { get { return Math.Max(0.05f, F("Civilizations", "civ_pace", 1f)); } }
        public bool ApplyRules { get { return B("Civilizations", "civ_apply_rules", true); } }
        public bool LockAges { get { return B("Civilizations", "civ_lock_ages", true); } }
        public bool Internet { get { return B("Civilizations", "civ_internet", true); } }
        public bool VehiclesOn { get { return B("Civilizations", "civ_vehicles", true); } }
        public bool PollutionOn { get { return B("Civilizations", "civ_pollution", true); } }
        public bool NukesDefault { get { return B("Civilizations", "civ_nukes", false); } }
        public bool SoldiersOn { get { return B("Civilizations", "civ_soldiers", true); } }
        public int SoldiersPerCity { get { return Math.Max(0, I("Civilizations", "civ_soldiers_city", 20)); } }
        public bool AmmoOn { get { return B("Civilizations", "civ_ammo", true); } }
        public bool PerfMode { get { return B("Civilizations", "civ_perf", false); } }
        public bool Profiler { get { return B("Civilizations", "civ_profiler", false); } }
        public bool DebugLog { get { return B("Civilizations", "civ_debug", false); } }
        public bool NightLights { get { return B("CivVisual", "civ_night_lights", true); } }
        public bool EraSkins { get { return B("CivVisual", "civ_era_skins", true); } }
        public bool Hud { get { return B("CivVisual", "civ_hud", false); } }
        public bool Waters { get { return B("CivVisual", "civ_waters", true); } }
        public bool DayNight { get { return B("CivVisual", "civ_daynight", true); } }
        public int DayMinutes { get { return Math.Max(2, I("CivVisual", "civ_day_minutes", 24)); } }
        public int BandTiles { get { return Math.Max(25, I("CivVisual", "civ_band_tiles", 100)); } }
        public float NightStrength { get { return Math.Max(0f, Math.Min(1f, F("CivVisual", "civ_night_strength", 0.55f))); } }
        public int PostsPerYear { get { return Math.Max(0, I("CivVisual", "civ_posts_year", 6)); } }
        public bool TransitVisuals { get { return B("CivVisual", "civ_transit_visuals", true); } }
        public bool RealNames { get { return B("Civilizations", "civ_real_names", true); } }
        public bool CompaniesOn { get { return B("Civilizations", "civ_companies", true); } }
        public bool EnergyOn { get { return B("Civilizations", "civ_energy", true); } }
        public int GlobeMode { get { return Math.Max(0, Math.Min(2, I("CivWorld", "civ_globe", 2))); } }
        public bool CustomSize { get { return B("CivWorld", "civ_custom_size", false); } }
        public int SizeX { get { return Math.Max(1, I("CivWorld", "civ_size_x", 8)); } }
        public int SizeY { get { return Math.Max(1, I("CivWorld", "civ_size_y", 5)); } }
        public bool TinyOn { get { return B("CivCities", "civ_tiny", true); } }
        public int ZoneRange { get { return I("CivCities", "civ_zone_range", 3); } }
        public int Spacing { get { return I("CivCities", "civ_spacing", 1); } }
        public int MinAdults { get { return I("CivCities", "civ_min_adults", 10); } }
        public int MinCities { get { return I("CivCities", "civ_min_cities", 2); } }
        public int MaxCities { get { return I("CivCities", "civ_max_cities", 5); } }
        public int MinGround { get { return I("CivCities", "civ_min_ground", 20); } }
        public int MinIsland { get { return I("CivCities", "civ_min_island", 100); } }
    }
}
