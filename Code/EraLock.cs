using System;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal static class EraLock
    {
        private static float _scan;
        private static Kingdom _leader;
        private static int _era;
        private static float _progress;

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.LockAges; }
        }

        public static Kingdom Leader { get { return _leader; } }
        public static int LeaderEra { get { return _era; } }
        public static float LeaderProgress { get { return _progress; } }

        public static void Force()
        {
            if (!On || World.world == null || World.world.map_stats == null) return;
            MapStats ms = World.world.map_stats;
            ms.is_world_ages_paused = true;
            ms.current_age_progress = 0f;
            if (ms.world_age_id != "age_hope" && World.world.era_manager != null && WorldAgeLibrary.hope != null)
            {
                ms.world_age_id = "age_hope";
                World.world.era_manager.setCurrentAge(WorldAgeLibrary.hope);
            }
        }

        public static void Tick(float dt)
        {
            if (On)
            {
                MapStats ms = World.world.map_stats;
                if (!ms.is_world_ages_paused) ms.is_world_ages_paused = true;
                if (ms.world_age_id != "age_hope") Force();
            }
            _scan -= dt;
            if (_scan > 0f) return;
            _scan = 2f;
            FindLeader();
            RouteButton();
        }

        private static PowerButton _routeBtn;
        private static int _btnTries;

        private static void RouteButton()
        {
            if (!On) return;
            if (_routeBtn == null)
            {
                if (_btnTries >= 15) return;
                _btnTries++;
                foreach (PowerButton b in Resources.FindObjectsOfTypeAll<PowerButton>())
                {
                    if (b != null && b.open_window_id == "world_ages")
                    {
                        _routeBtn = b;
                        break;
                    }
                }
            }
            if (_routeBtn == null || _routeBtn.icon == null) return;
            Sprite s = SpriteTextureLoader.getSprite(WorldPrefs.Icon(WorldPrefs.Route));
            if (s != null) _routeBtn.icon.sprite = s;
        }

        private static void FindLeader()
        {
            Kingdom best = null;
            float score = -1f;
            foreach (Kingdom k in G.Kingdoms())
            {
                if (!k.isCiv()) continue;
                int e = Research.Era(k);
                float p = Math.Min(1f, Research.EraFraction(k, e) / 0.65f);
                float s = e + p * 0.99f;
                if (s > score)
                {
                    score = s;
                    best = k;
                }
            }
            _leader = best;
            _era = best == null ? 0 : Research.Era(best);
            _progress = best == null ? 0f : Math.Min(1f, Research.EraFraction(best, _era) / 0.65f);
            if (_era >= Db.LastEra) _progress = 1f;
        }

        public static Sprite EraSprite(int era)
        {
            if (era < 0 || era >= Db.Eras.Count) return null;
            return SpriteTextureLoader.getSprite("ui/Icons/civ/era/" + Db.Eras[era].Id);
        }

        [HarmonyPatch(typeof(UiWorldAgeInfo), "Update")]
        private static class Panel
        {
            private static void Postfix(UiWorldAgeInfo __instance)
            {
                if (!On || !Config.game_loaded || World.world == null || Db.Eras.Count == 0) return;
                try
                {
                    Sprite cur = EraSprite(_era);
                    Sprite next = EraSprite(Math.Min(_era + 1, Db.LastEra));
                    if (cur != null)
                    {
                        __instance._icon_age_left.sprite = cur;
                        __instance._icon_age_right.sprite = cur;
                    }
                    if (next != null)
                    {
                        __instance._icon_age_next_left.sprite = next;
                        __instance._icon_age_next_right.sprite = next;
                    }
                    __instance._text_age_title.text = Db.EraName(_era);
                    __instance._text_age_title.color = G.Col(Db.Eras[_era].Color);
                    string who = _leader != null ? G.Name(_leader) : Txt.T("civ_no_leader");
                    StatBar bar = __instance._bar_age;
                    bar.bar.DOKill();
                    bar.bar.sizeDelta = new Vector2(Mathf.Floor(Mathf.Clamp01(_progress) * bar.mask.rect.width), bar.bar.sizeDelta.y);
                    __instance._bar_age.textField.text = who + "  " + Mathf.RoundToInt(_progress * 100f) + "%";
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(WorldAgeManager), "setCurrentAge")]
        private static class LockAge
        {
            private static void Prefix(ref WorldAgeAsset pAsset)
            {
                if (On && WorldAgeLibrary.hope != null) pAsset = WorldAgeLibrary.hope;
            }
        }

        [HarmonyPatch(typeof(WorldAgeManager), nameof(WorldAgeManager.update))]
        private static class Paused
        {
            private static void Prefix()
            {
                if (!On || World.world == null || World.world.map_stats == null) return;
                World.world.map_stats.is_world_ages_paused = true;
            }
        }

        [HarmonyPatch(typeof(ScrollWindow), nameof(ScrollWindow.showWindow), new Type[] { typeof(string), typeof(bool), typeof(bool) })]
        private static class Redirect
        {
            private static bool Prefix(string pWindowID)
            {
                if (!On || pWindowID != "world_ages") return true;
                WinRoute.Open();
                return false;
            }
        }
    }
}
