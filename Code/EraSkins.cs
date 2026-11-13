using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace Civilizations
{
    internal static class EraSkins
    {
        private const string Root = "actors/civ_skins/";
        private static readonly Dictionary<string, bool> _exists = new Dictionary<string, bool>();
        private static readonly Dictionary<long, string> _sig = new Dictionary<long, string>();
        private static readonly HashSet<string> _alias = new HashSet<string>();
        private static readonly Dictionary<string, string> _orig = new Dictionary<string, string>();
        private static Dictionary<string, Sprite[]> _lists;

        private static string Alias(string path, ActorAsset asset)
        {
            string id = path + "_" + asset.id;
            if (_alias.Contains(id)) return id;
            if (_lists == null) _lists = AccessTools.Field(typeof(SpriteTextureLoader), "_cached_sprite_list").GetValue(null) as Dictionary<string, Sprite[]>;
            Sprite[] src = SpriteTextureLoader.getSpriteList(path, true);
            if (_lists == null || src == null) return path;
            _lists[id] = src;
            _alias.Add(id);
            return id;
        }

        public static int Epoch(int era)
        {
            if (era <= 3) return 0;
            if (era <= 6) return 1;
            if (era <= 8) return 2;
            if (era == 9) return 3;
            if (era <= 11) return 4;
            return 5;
        }

        public static string Family(Kingdom k)
        {
            switch (Policies.Id(k, "gov"))
            {
                case "theocracy": return "theocracy";
                case "military_junta": return "military";
                case "single_party_state": return "party";
                case "tribal_council":
                case "republic":
                case "democracy":
                case "merchant_republic":
                case "presidential_republic":
                case "parliamentary_republic":
                case "technocracy":
                case "corporatocracy": return "republic";
                default: return "monarchy";
            }
        }

        private static string Kind(Actor a)
        {
            ActorAsset s = a.asset;
            if (s == null || !a.isSapient()) return null;
            if (s.id == "human") return "human";
            if (s.has_advanced_textures) return "advanced";
            if (s.id.StartsWith("civ_")) return "animal";
            return null;
        }

        private static bool Exists(string path)
        {
            bool v;
            if (_exists.TryGetValue(path, out v)) return v;
            Sprite[] list = SpriteTextureLoader.getSpriteList(path, true);
            v = list != null && list.Length > 0;
            _exists[path] = v;
            return v;
        }

        private static string Pick(Actor a)
        {
            if (a.kingdom == null || !a.kingdom.isCiv() || a.isEgg() || Vehicles.IsVehicle(a)) return null;
            string kind = Kind(a);
            if (kind == null) return null;
            int e = Epoch(Research.Era(a.kingdom));
            if (e <= 0) return null;
            string b = Root + kind + "/e" + e + "/";
            string role;
            if (a.isBaby()) role = "child";
            else if (a.isKing()) role = "king_" + Family(a.kingdom);
            else if (a.isCityLeader()) role = "leader_" + Family(a.kingdom);
            else if (a.isWarrior()) role = "warrior";
            else role = a.isSexFemale() ? "female" : "male";
            if (Exists(b + role)) return Alias(b + role, a.asset);
            int u = role.IndexOf('_');
            if (u > 0 && Exists(b + role.Substring(0, u))) return Alias(b + role.Substring(0, u), a.asset);
            return null;
        }

        public static void Year()
        {
            foreach (Kingdom k in G.Kingdoms())
            {
                string sig = Epoch(Research.Era(k)) + "|" + Family(k);
                string old;
                if (_sig.TryGetValue(k.data.id, out old) && old == sig) continue;
                _sig[k.data.id] = sig;
                if (old == null) continue;
                foreach (City c in G.Cities(k))
                {
                    foreach (Actor a in c.units) if (G.Ok(a)) a.dirty_sprite_main = true;
                }
            }
        }

        public static void Clear()
        {
            _sig.Clear();
        }

        [HarmonyPatch(typeof(ActorTextureSubAsset), nameof(ActorTextureSubAsset.getUnitTexturePath))]
        private static class Skin
        {
            private static void Postfix(Actor pActor, ref string __result)
            {
                if (pActor == null || CivMod.C != null && !CivMod.C.EraSkins) return;
                try
                {
                    string p = Pick(pActor);
                    if (p == null) return;
                    if (!_orig.ContainsKey(p) && !string.IsNullOrEmpty(__result) && !__result.StartsWith(Root)) _orig[p] = __result;
                    __result = p;
                }
                catch (System.Exception)
                {
                }
            }
        }

        private static AnimationFrameData Twin(AnimationContainerUnit v, string id)
        {
            AnimationFrameData f;
            if (v.dict_frame_data.TryGetValue(id, out f) && f.show_head) return f;
            string kind = id.StartsWith("swim") ? "swim" : "walk";
            foreach (KeyValuePair<string, AnimationFrameData> kv in v.dict_frame_data)
            {
                if (kv.Value != null && kv.Value.show_head && kv.Key.StartsWith(kind)) return kv.Value;
            }
            foreach (KeyValuePair<string, AnimationFrameData> kv in v.dict_frame_data)
            {
                if (kv.Value != null && kv.Value.show_head) return kv.Value;
            }
            return null;
        }

        [HarmonyPatch(typeof(ActorAnimationLoader), nameof(ActorAnimationLoader.createAnimationContainer))]
        private static class Heads
        {
            private static void Postfix(string pTexturePath, ActorAsset pAsset, SubspeciesTrait pEggAsset, SubspeciesTrait pMutationSkinAsset, AnimationContainerUnit __result)
            {
                if (__result == null || pTexturePath == null || !pTexturePath.StartsWith(Root)) return;
                try
                {
                    string orig;
                    if (!_orig.TryGetValue(pTexturePath, out orig)) return;
                    AnimationContainerUnit v = ActorAnimationLoader.getAnimationContainer(orig, pAsset, pEggAsset, pMutationSkinAsset);
                    if (v == null || v == __result) return;
                    foreach (KeyValuePair<string, AnimationFrameData> kv in __result.dict_frame_data)
                    {
                        AnimationFrameData f = kv.Value;
                        if (f == null || !f.show_head) continue;
                        AnimationFrameData t = Twin(v, kv.Key);
                        if (t == null)
                        {
                            f.show_head = false;
                            continue;
                        }
                        f.pos_head = t.pos_head;
                        f.pos_head_new = t.pos_head_new;
                    }
                }
                catch (System.Exception)
                {
                }
            }
        }
    }
}
