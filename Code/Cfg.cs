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
        public bool FogView { get { return B("CivVisual", "civ_fog_view", false); } }
        public bool Regression { get { return B("Civilizations", "civ_regression", true); } }
        public bool Apocalypse { get { return B("Civilizations", "civ_apocalypse", true); } }
        public bool Festivals { get { return B("Civilizations", "civ_festivals", true); } }
        public bool Events { get { return B("Civilizations", "civ_events", true); } }
        public int LogLimit
        {
            get
            {
                ModConfigItem i = Item("Civilizations", "civ_log_limit");
                long v;
                if (i == null || i.TextVal == null || !long.TryParse(i.TextVal.Trim(), out v)) return 100000;
                if (v < 100) v = 100;
                if (v > int.MaxValue) v = int.MaxValue;
                return (int)v;
            }
        }
        public bool Sandbox { get { return B("Civilizations", "civ_sandbox", true); } }
        public bool SandboxNotice { get { return B("Civilizations", "civ_sandbox_notice", true); } }
        public float CtrlPlayer { get { return F("Civilizations", "civ_ctrl_player", 0f); } }
        public float CtrlAi { get { return F("Civilizations", "civ_ctrl_ai", 0f); } }
        public bool PauseEvents { get { return B("Civilizations", "civ_pause_events", true); } }
        public bool CheckUpdates { get { return B("Civilizations", "civ_check_updates", true); } }
        public bool CosShared { get { return B("CivCosmos", "civ_cos_shared", true); } }
        public bool CosNear { get { return B("CivCosmos", "civ_cos_near", true); } }
        public float CosmicTime { get { return Math.Max(0f, F("CivCosmos", "civ_cos_time", 1f)); } }
        public int Fermi { get { return Math.Max(0, Math.Min(2, I("CivCosmos", "civ_cos_fermi", 1))); } }
        public bool CosmicInvasions { get { return B("CivCosmos", "civ_cos_invasions", true); } }
        public bool CosmicEnding { get { return B("CivCosmos", "civ_cos_ending", false); } }
        public string CosSeed
        {
            get
            {
                ModConfigItem i = Item("CivCosmos", "civ_cos_seed");
                return i == null || i.TextVal == null ? "" : i.TextVal.Trim();
            }
        }
        public int MaxEra { get { return I("Civilizations", "civ_max_era", 99); } }
        public int MaxScale { get { return I("Civilizations", "civ_max_scale", 99); } }
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
