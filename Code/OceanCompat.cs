using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class OceanCompat
    {
        private const int MinLevel = 5;
        private static bool _done;
        private static int _tries;
        private static float _wait;

        public static bool Active { get; private set; }

        private static readonly float[] Delays = { 10f, 30f, 80f };

        public static void Try()
        {
            if (_done) return;
            _wait += Time.unscaledDeltaTime;
            if (_wait < Delays[_tries]) return;
            _wait = 0f;
            _tries++;
            Type t = Find("OceanOfSouls.SoulReincarnation");
            if (t == null)
            {
                if (_tries >= Delays.Length) _done = true;
                return;
            }
            _done = true;
            MethodInfo m = AccessTools.Method(t, "Apply");
            if (m == null) return;
            try
            {
                var h = new Harmony("agriche.civilizations.oceanofsouls");
                h.Patch(m, new HarmonyMethod(typeof(OceanCompat), nameof(Before)), new HarmonyMethod(typeof(OceanCompat), nameof(After)));
                Active = true;
                CivMod.Log("Ocean of Souls detected: names carry over only from soul level " + MinLevel);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Civilizations] ocean of souls patch: " + e.Message);
            }
        }

        private static Type Find(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = a.GetType(name, false);
                    if (t != null) return t;
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        private static void Before(Actor actor, ref string __state)
        {
            __state = actor != null && actor.data != null ? actor.getName() : null;
        }

        private static void After(Actor actor, bool revive, string __state)
        {
            if (revive || actor == null || actor.data == null || string.IsNullOrEmpty(__state)) return;
            int level;
            actor.data.get("oos_level", out level, 0);
            if (level >= MinLevel) return;
            actor.setName(__state, false);
        }
    }
}
