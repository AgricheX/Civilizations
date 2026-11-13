using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal static class WallArt
    {
        private static Action<TopTileType, QuantumSpriteAsset, bool, Material> _draw;
        private static bool _drawTried;
        private static IDictionary _frames;
        private static Type _frameType;

        public static TileSprites Flat(string id, string hex, string edgeHex)
        {
            var ts = new TileSprites();
            Color c = Toolbox.makeColor(hex);
            Color e = Toolbox.makeColor(edgeHex);
            for (int v = 0; v < 3; v++)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        bool dark = (x + y + v) % 3 == 0;
                        tex.SetPixel(x, y, dark ? Color.Lerp(c, e, 0.35f) : c);
                    }
                tex.Apply();
                Sprite s = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                s.name = id + "_" + v;
                ts.addVariation(s, id);
            }
            return ts;
        }

        private static Sprite[] _shield;

        private static Sprite[] Sheet(string look)
        {
            if (look != "civ_shield") return SpriteTextureLoader.getSpriteList("walls/" + look + "/wall_sheet");
            if (_shield != null) return _shield;
            Sprite[] src = SpriteTextureLoader.getSpriteList("walls/wall_iron/wall_sheet");
            if (src == null || src.Length == 0) return null;
            _shield = Tint(src, new Color(0.35f, 0.84f, 1f), 0.78f) ?? src;
            return _shield;
        }

        private static Sprite[] Tint(Sprite[] src, Color tint, float alpha)
        {
            try
            {
                Texture2D atlas = src[0].texture;
                RenderTexture rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(atlas, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var outp = new Sprite[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    Sprite s = src[i];
                    if (s.texture != atlas)
                    {
                        outp[i] = s;
                        continue;
                    }
                    Rect r = s.textureRect;
                    int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
                    Color[] px = tex.GetPixels();
                    for (int p = 0; p < px.Length; p++)
                    {
                        Color c = px[p];
                        if (c.a <= 0.01f) continue;
                        float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                        Color n = Color.Lerp(tint * 0.75f, Color.white, Mathf.Clamp01(l * 0.9f));
                        n.a = c.a * alpha;
                        px[p] = n;
                    }
                    tex.SetPixels(px);
                    tex.Apply();
                    Sprite ns = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(s.pivot.x / w, s.pivot.y / h), s.pixelsPerUnit);
                    ns.name = s.name;
                    outp[i] = ns;
                }
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                return outp;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Prepare(TopTileType t)
        {
            try
            {
                if (_frames == null)
                {
                    FieldInfo f = AccessTools.Field(typeof(WallHelper), "_dictionary");
                    _frames = f != null ? f.GetValue(null) as IDictionary : null;
                    _frameType = AccessTools.TypeByName("WallFrameContainer");
                }
                if (_frames == null || _frameType == null) return false;
                if (_frames.Contains(t.index_id)) return true;
                Sprite[] list = Sheet(Walls.Look(t));
                if (list == null || list.Length == 0) return false;
                object box = Activator.CreateInstance(_frameType, true);
                AccessTools.Field(_frameType, "sprites").SetValue(box, list);
                _frames[t.index_id] = box;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawWalls")]
        private static class DrawOurs
        {
            private static void Postfix(QuantumSpriteAsset pAsset)
            {
                if (!_drawTried)
                {
                    _drawTried = true;
                    MethodInfo m = AccessTools.Method(typeof(QuantumSpriteLibrary), "drawWallType");
                    if (m != null) _draw = (Action<TopTileType, QuantumSpriteAsset, bool, Material>)Delegate.CreateDelegate(typeof(Action<TopTileType, QuantumSpriteAsset, bool, Material>), m, false);
                }
                if (_draw == null || World.world == null) return;
                Material mat = LibraryMaterials.instance != null ? LibraryMaterials.instance.mat_world_object : null;
                if (mat == null) return;
                bool transparent = World.world.getSelectedPowerAsset()?.make_buildings_transparent ?? false;
                foreach (TopTileType t in Walls.Drawn())
                {
                    if (t == null || t.getCurrentTiles().Count == 0) continue;
                    if (!Prepare(t)) continue;
                    try
                    {
                        _draw(t, pAsset, transparent, mat);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        [HarmonyPatch(typeof(WorldTilemap), "getVariation")]
        private static class SafeVariation
        {
            private static bool Prefix(WorldTile pTile, ref UnityEngine.Tilemaps.Tile __result)
            {
                TileTypeBase t = pTile.Type;
                if (t != null && t.sprites != null) return true;
                if (pTile.main_type != null && pTile.main_type.sprites != null)
                {
                    __result = pTile.main_type.sprites.getRandom();
                    return false;
                }
                return true;
            }
        }
    }
}
