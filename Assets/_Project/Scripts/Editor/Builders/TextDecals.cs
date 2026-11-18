using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.10 W2: sharp world text. The live TextMesh text came from the small dynamic font atlas without mipmaps, so road
    /// markings (seen at a grazing angle) and board text broke up / shimmered, and the depth offset let it poke through
    /// meshes. Now every static text is BAKED once: glyphs are copied from the UI font's atlas at a large size into a
    /// texture (Art/Generated/Text, mipmaps + 16× anisotropic + trilinear, uncompressed) and drawn as a URP decal:
    ///  - <see cref="Floor"/>: painted on the ground, stretched along the driving direction like real road lettering;
    ///  - <see cref="Board"/>: projected onto a sign / board surface (cannot z-fight, hidden by anything in front).
    /// </summary>
    public static class TextDecals
    {
        const string Dir = "Assets/_Project/Art/Generated/Text";
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        static string Key(string text, Color c, int px) =>
            "txt_" + System.BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(text + "|" + ColorUtility.ToHtmlStringRGBA(c) + "|" + px))).Replace("-", "").Substring(0, 12).ToLowerInvariant();

        /// <summary>Bakes <paramref name="text"/> (multi-line, centred) into a texture; returns it with its line count.</summary>
        public static Texture2D Bake(string text, Color color, int px, out int lines)
        {
            Directory.CreateDirectory(Dir);
            lines = text.Split('\n').Length;
            string path = $"{Dir}/{Key(text, color, px)}.png";
            if (!File.Exists(path))
            {
                var font = UI.UiFactory.Font;
                font.RequestCharactersInTexture(text.Replace("\n", ""), px, FontStyle.Bold);
                var atlasTex = font.material.mainTexture;
                var rt = RenderTexture.GetTemporary(atlasTex.width, atlasTex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(atlasTex, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var atlas = new Texture2D(atlasTex.width, atlasTex.height, TextureFormat.RGBA32, false, true);
                atlas.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); atlas.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);

                int pad = Mathf.RoundToInt(px * 0.18f), lineH = Mathf.RoundToInt(px * 1.22f);
                var rows = text.Split('\n');
                var widths = rows.Select(r => r.Sum(ch => font.GetCharacterInfo(ch, out var ci, px, FontStyle.Bold) ? ci.advance : px / 3)).ToArray();
                int W = widths.Max() + pad * 2, H = rows.Length * lineH + pad * 2;
                var a = new float[W * H];
                for (int l = 0; l < rows.Length; l++)
                {
                    int x = pad + (widths.Max() - widths[l]) / 2;
                    int baseline = H - pad - l * lineH - Mathf.RoundToInt(px * 0.95f);
                    foreach (char ch in rows[l])
                    {
                        if (!font.GetCharacterInfo(ch, out var ci, px, FontStyle.Bold)) { x += px / 3; continue; }
                        int gw = ci.maxX - ci.minX, gh = ci.maxY - ci.minY;
                        for (int gy = 0; gy < gh; gy++)
                            for (int gx = 0; gx < gw; gx++)
                            {
                                float fx = (gx + 0.5f) / gw, fy = (gy + 0.5f) / gh;
                                var uv = Vector2.Lerp(Vector2.Lerp(ci.uvBottomLeft, ci.uvBottomRight, fx), Vector2.Lerp(ci.uvTopLeft, ci.uvTopRight, fx), fy);
                                var s = atlas.GetPixelBilinear(uv.x, uv.y);
                                float v = Mathf.Max(s.a, s.r);
                                int dx = x + ci.minX + gx, dy = baseline + ci.minY + gy;
                                if (dx < 0 || dy < 0 || dx >= W || dy >= H) continue;
                                a[dy * W + dx] = Mathf.Max(a[dy * W + dx], v);
                            }
                        x += ci.advance;
                    }
                }
                Object.DestroyImmediate(atlas);
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                var px2 = new Color[W * H];
                for (int i = 0; i < px2.Length; i++) px2[i] = new Color(color.r, color.g, color.b, a[i] * color.a);
                tex.SetPixels(px2); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true;
                    ti.anisoLevel = 16; ti.filterMode = FilterMode.Trilinear; ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.npotScale = TextureImporterNPOTScale.None; ti.maxTextureSize = 2048; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(Texture2D tex)
        {
            if (_mats.TryGetValue(tex.name, out var m) && m != null) return m;
            string path = $"{Dir}/Decal_{tex.name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Shader Graphs/Decal")); AssetDatabase.CreateAsset(m, path); }
            if (m.HasProperty("Base_Map")) m.SetTexture("Base_Map", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("Normal_Blend")) m.SetFloat("Normal_Blend", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            _mats[tex.name] = m;
            return m;
        }

        /// <summary>Road lettering: <paramref name="width"/> m across, stretched ×<paramref name="stretch"/> along the driving
        /// direction (<paramref name="yaw"/>: the way the reader drives; the top of the letters points that way).</summary>
        public static GameObject Floor(Transform parent, string text, Vector3 worldPos, float yaw, float width, Color color, float stretch = 2.5f)
        {
            var tex = Bake(text, color, 256, out _);
            var go = new GameObject("FloorText_" + text.Replace("\n", "_")) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(worldPos.x, worldPos.y + 0.3f, worldPos.z), Quaternion.Euler(90f, yaw, 0f));
            var d = go.AddComponent<DecalProjector>();
            d.material = Mat(tex);
            float len = width * tex.height / (float)tex.width * stretch;
            d.size = new Vector3(width, len, 0.7f);
            d.pivot = new Vector3(0f, 0f, 0.35f);
            d.drawDistance = 120f; d.fadeScale = 0.9f;
            return go;
        }

        /// <summary>Text on a board: the projector sits at <paramref name="localPos"/> (on the board's front face) and looks
        /// along local <paramref name="euler"/> +Z into the surface; <paramref name="lineHeight"/> m per line.</summary>
        public static GameObject Board(Transform parent, string text, Vector3 localPos, Vector3 euler, float lineHeight, Color color, float depth = 0.06f)
        {
            int px = text.Length > 24 ? 112 : 160;
            var tex = Bake(text, color, px, out int lines);
            var go = new GameObject("BoardText") { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localEulerAngles = euler;
            var d = go.AddComponent<DecalProjector>();
            d.material = Mat(tex);
            float h = lineHeight * (lines + 0.36f);
            d.size = new Vector3(h * tex.width / tex.height, h, depth);
            d.pivot = Vector3.zero;
            d.drawDistance = 90f; d.fadeScale = 0.9f;
            return go;
        }
    }
}
