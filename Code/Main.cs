using System;
using System.Collections.Generic;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    public class CivMod : BasicMod<CivMod>
    {
        internal static Cfg C;
        internal static string Folder = "";
        private static bool _assetsDone;

        protected override void OnModLoad()
        {
            Folder = GetDeclaration().FolderPath;
            C = new Cfg(GetConfig());
            Safe("text", Txt.Init);
            Safe("data", Db.Init);
            Safe("resources", Economy.InitAssets);
            Safe("storage", Storage.InitAssets);
            Safe("roles", Roles.InitAssets);
            Safe("xeno", Xeno.InitAssets);
            Safe("statuses", Items.InitStatuses);
            Safe("items", Items.InitAssets);
            Safe("policies", Policies.InitAssets);
            Safe("politics", Politics.InitAssets);
            Safe("buildings", Buildings.InitAssets);
            Safe("units", Units.InitAssets);
            Safe("jobs", Behaviours.InitAssets);
            Safe("moods", Moods.InitAssets);
            Safe("sandbox", Sandbox.InitAssets);
            Safe("biomes", Biomes.InitAssets);
            Safe("cosmos-scale", CosmosScale.InitAssets);
            Safe("culture", Research.InitCultureLock);
            Safe("orgs-data", Orgs.InitData);
            Safe("events-data", Events.InitData);
            Safe("sagas-data", Sagas.InitData);
            Safe("festivals-data", Festivals.InitData);
            Safe("diplomacy", Diplomacy.Register);
            Safe("legitimacy", Legitimacy.Register);
            Safe("firsts", Firsts.Register);
            Safe("orgs", Orgs.Register);
            Safe("world-orders", WorldOrders.Register);
            Safe("fog", Fog.Register);
            Safe("regression", Regression.Register);
            Safe("festivals", Festivals.Register);
            Safe("chronicle", Chronicle.Register);
            Safe("flags", Flags.Register);
            Safe("nethub", NetHub.Register);
            Safe("walls", Walls.Register);
            Safe("player", Player.Register);
            Safe("sagas", Sagas.Register);
            Safe("statecraft", Statecraft.Register);
            PatchAll();
            _assetsDone = true;
            LogInfo("Civilizations loaded: " + Db.Techs.Count + " techs");
        }

        private static void PatchAll()
        {
            var h = new Harmony("agriche.civilizations");
            int ok = 0, bad = 0;
            foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(CivMod).Assembly))
            {
                if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try
                {
                    h.CreateClassProcessor(t).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    bad++;
                    Exception inner = e;
                    while (inner.InnerException != null) inner = inner.InnerException;
                    Debug.LogWarning("[Civilizations] patch skipped " + t.FullName + ": " + inner.Message);
                }
            }
            LogInfo("patches " + ok + " ok, " + bad + " skipped");
        }

        internal static void Safe(string what, Action a)
        {
            if (_off.Count > 0 && _off.Contains(what)) return;
            long t0 = CivProfiler.On ? CivProfiler.Now() : 0;
            int prev = Ledger.Ctx;
            int f = Ledger.Label(what);
            if (f >= 0) Ledger.Ctx = f;
            try
            {
                a();
            }
            catch (Exception e)
            {
                Report(what, e);
            }
            finally
            {
                Ledger.Ctx = prev;
            }
            if (t0 != 0) CivProfiler.Add(what, t0);
        }

        private static readonly Dictionary<string, int> _errors = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _byWhat = new Dictionary<string, int>();
        private static readonly HashSet<string> _off = new HashSet<string>();

        internal static void ResetErrors()
        {
            _byWhat.Clear();
            _off.Clear();
        }

        internal static bool Off(string what)
        {
            return _off.Contains(what);
        }

        private static void Strike(string what)
        {
            if (what.StartsWith("hub-") || what == "statecraft-year") return;
            int n;
            _byWhat.TryGetValue(what, out n);
            _byWhat[what] = ++n;
            if (n < 500 || !_off.Add(what)) return;
            Debug.LogError("[Civilizations] " + what + " disabled after " + n + " errors");
            try
            {
                Notify.Show(Txt.F("civ_sys_off", what));
            }
            catch (Exception)
            {
            }
        }

        internal static void Report(string what, Exception e)
        {
            string key = what + "|" + e.GetType().Name + "|" + e.Message;
            int n;
            _errors.TryGetValue(key, out n);
            _errors[key] = ++n;
            Strike(what);
            if (n == 1) Debug.LogError("[Civilizations] " + what + ": " + e);
            else if (n == 10 || n == 100 || n == 1000 || n % 10000 == 0) Debug.LogError("[Civilizations] " + what + ": same error repeated " + n + " times: " + e.Message);
        }

        internal static void Log(string s)
        {
            if (C != null && C.DebugLog) Debug.Log("[Civilizations] " + s);
        }

        private void Update()
        {
            if (!_assetsDone) return;
            Sim.Update();
            Hud.Tick();
            ControlHud.Tick();
            Safe("fog-view", Fog.Frame);
            CivProfiler.Frame();
        }

        private void OnGUI()
        {
            if (!_assetsDone) return;
            CivProfiler.Draw();
        }
    }
}
