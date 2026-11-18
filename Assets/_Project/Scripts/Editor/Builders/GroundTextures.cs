using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.7 E1/E3: procedural, tileable textures for surfaces / dressing nobody delivered art for (grass ground, grass-blade and
    /// leaf cards, decals). Written once to Art/Generated/Ground (and /Decals), regenerated when the file is missing.
    /// </summary>
    public static class GroundTextures
    {
        public const string Dir = "Assets/_Project/Art/Generated/Ground";

        static float Hash(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647); h = (h ^ (h >> 13)) * 1274126177; return (h ^ (h >> 16)) / 4294967295f; } }

        /// <summary>Tileable value noise (period = cells).</summary>
        public static float Noise(float x, float y, int cells, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int W(int v) => ((v % cells) + cells) % cells;
            float a = Hash(W(x0), W(y0), seed), b = Hash(W(x0 + 1), W(y0), seed), c = Hash(W(x0), W(y0 + 1), seed), d = Hash(W(x0 + 1), W(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float u, float v, int baseCells, int seed)
        {
            float s = 0f, amp = 0.5f, tot = 0f; int cells = baseCells;
            for (int o = 0; o < 4; o++) { s += Noise(u * cells, v * cells, cells, seed + o) * amp; tot += amp; amp *= 0.5f; cells *= 2; }
            return s / tot;
        }

        static Texture2D Save(string path, Texture2D t, bool linear, bool alpha = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.sRGBTexture = !linear; ti.alphaIsTransparency = alpha; ti.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Grass ground: (albedo, normal, mask) — blades as fine streaks, clover patches, dry spots.</summary>
        public static (Texture2D albedo, Texture2D normal, Texture2D mask) Grass()
        {
            string a = Dir + "/grass_albedo.png", n = Dir + "/grass_albedo_normal.png", m = Dir + "/grass_mask.png";
            if (!File.Exists(a))
            {
                const int S = 512;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
                var green = new Color(0.27f, 0.42f, 0.16f); var light = new Color(0.42f, 0.56f, 0.22f); var dry = new Color(0.55f, 0.5f, 0.28f);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = x / (float)S, v = y / (float)S;
                        float blades = Noise(u * 160f, v * 40f, 160, 3) * 0.6f + Noise(u * 90f, v * 220f, 90, 4) * 0.4f; // streaky
                        float patch = Fbm(u, v, 4, 7), dryness = Mathf.SmoothStep(0.55f, 0.75f, Fbm(u, v, 3, 11));
                        var c = Color.Lerp(green, light, blades * 0.8f + patch * 0.3f);
                        c = Color.Lerp(c, dry, dryness * 0.6f);
                        c *= 0.85f + blades * 0.3f;
                        t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
                    }
                t.Apply();
                Save(a, t, false);
                var mask = new Texture2D(S, S, TextureFormat.RGBA32, false, true);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float bl = Noise(x / (float)S * 160f, y / (float)S * 40f, 160, 3);
                        mask.SetPixel(x, y, new Color(0f, 0.65f + bl * 0.35f, 0f, 0.12f + bl * 0.1f));
                    }
                mask.Apply();
                Save(m, mask, true);
            }
            // normal from the albedo (same Sobel as M4.6 D5)
            if (!File.Exists(n))
            {
                File.WriteAllBytes(n, NormalMapGen.Generate(File.ReadAllBytes(a), 3f).EncodeToPNG());
                AssetDatabase.ImportAsset(n, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(n) is TextureImporter ti) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            }
            return (AssetDatabase.LoadAssetAtPath<Texture2D>(a), AssetDatabase.LoadAssetAtPath<Texture2D>(n), AssetDatabase.LoadAssetAtPath<Texture2D>(m));
        }
    }
}
