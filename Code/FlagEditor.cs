using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class FlagEditor
    {
        public const string KLock = "civ_flag_lock";
        private static readonly string[] Divs = { "plain", "fess2", "pale2", "tri_v", "tri_h", "cross", "nordic", "saltire", "quarterly", "border", "per_bend", "chief" };
        private static readonly List<string> _bg = new List<string>();
        private static readonly List<string> _icon = new List<string>();
        private static readonly HashSet<BannerAsset> _done = new HashSet<BannerAsset>();
        private static bool _built;
        private static bool _quiet;

        public static bool Locked(Kingdom k)
        {
            return G.Ok(k) && Store.Bool(k.data, KLock);
        }

        public static void Lock(Kingdom k, bool on)
        {
            if (!G.Ok(k)) return;
            Store.Put(k.data, KLock, on);
            if (!on && !Flags.Has(k)) Flags.Reform(k, "government");
            Refresh(k);
        }

        public static void Refresh(Kingdom k)
        {
            if (!G.Ok(k)) return;
            try
            {
                World.world.zone_calculator.setDrawnZonesDirty();
            }
            catch (Exception)
            {
            }
        }

        private static void Build()
        {
            if (_built || !FlagArt.Ready) return;
            _built = true;
            for (int s = 0; s < FlagArt.Shapes.Count; s++)
            {
                foreach (string d in Divs)
                {
                    string name = "civ_bg_" + FlagArt.Shapes[s].Id + "_" + d;
                    Sprite sp = FlagArt.ToSprite(FlagArt.EditorBackground(s, d), name);
                    if (sp == null) continue;
                    _bg.Add(name);
                    Cache(name, sp);
                }
            }
            for (int e = 0; e < FlagArt.Emblems.Count; e++)
            {
                string name = "civ_icon_" + FlagArt.Emblems[e].Id;
                Sprite sp = FlagArt.ToSprite(FlagArt.EditorIcon(e), name);
                if (sp == null) continue;
                _icon.Add(name);
                Cache(name, sp);
            }
        }

        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        private static void Cache(string name, Sprite sp)
        {
            _sprites[name] = sp;
        }

        public static void Inject(BannerAsset a, string id)
        {
            if (a == null || string.IsNullOrEmpty(id) || _done.Contains(a)) return;
            Build();
            if (_bg.Count == 0 && _icon.Count == 0) return;
            _done.Add(a);
            string bgRoot = KingdomBannerLibrary.getFullPathBackground(id) + "/";
            string icRoot = KingdomBannerLibrary.getFullPathIcon(id) + "/";
            foreach (string n in _bg)
            {
                string path = bgRoot + n;
                SpriteTextureLoader._cached_sprites[path] = _sprites[n];
                if (a.backgrounds != null && !a.backgrounds.Contains(path)) a.backgrounds.Add(path);
            }
            foreach (string n in _icon)
            {
                string path = icRoot + n;
                SpriteTextureLoader._cached_sprites[path] = _sprites[n];
                if (a.icons != null && !a.icons.Contains(path)) a.icons.Add(path);
            }
        }

        public static void InjectAll()
        {
            try
            {
                if (AssetManager.kingdom_banners_library == null) return;
                foreach (BannerAsset a in AssetManager.kingdom_banners_library.list) Inject(a, a.id);
            }
            catch (Exception e)
            {
                CivMod.Report("flag-inject", e);
            }
        }

        [HarmonyPatch(typeof(KingdomBannerLibrary), nameof(KingdomBannerLibrary.loadNewAssetRuntime))]
        private static class Runtime
        {
            private static void Postfix(string pID, BannerAsset __result)
            {
                try
                {
                    Inject(__result, pID);
                }
                catch (Exception e)
                {
                    CivMod.Report("flag-inject", e);
                }
            }
        }

        [HarmonyPatch(typeof(KingdomCustomizeWindow), nameof(KingdomCustomizeWindow.onBannerChange))]
        private static class Edited
        {
            private static void Postfix(KingdomCustomizeWindow __instance)
            {
                if (_quiet) return;
                try
                {
                    Kingdom k = __instance.meta_object;
                    if (!G.Ok(k) || Locked(k)) return;
                    Store.Put(k.data, KLock, true);
                    Refresh(k);
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch]
        private static class Opened
        {
            private static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(GenericCustomizeWindow<Kingdom, KingdomData, KingdomBanner>), "OnEnable");
            }

            private static void Prefix()
            {
                _quiet = true;
            }

            private static void Postfix()
            {
                _quiet = false;
            }
        }
    }
}
