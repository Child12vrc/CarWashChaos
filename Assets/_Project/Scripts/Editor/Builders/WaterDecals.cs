using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.6 D7: procedural wet-patch / puddle decal textures (our own generated assets → Art/Generated/VFX, art PNGs untouched)
    /// and their URP decal materials (Shader Graphs/Decal: darkened base colour with a soft irregular alpha; the decal's flat
    /// normal smooths the surface it covers, which reads as a wet film).
    /// </summary>
    public static class WaterDecals
    {
        const string Dir = "Assets/_Project/Art/Generated/VFX";
        const string MatDir = "Assets/_Project/Art/Materials";

        public static Material Wet() => Mat("VFX_wet_patch", Tex("fx_wet_blob2", new Color(0.05f, 0.06f, 0.08f), 0.72f, 0.3f, 7), 0.6f);
        public static Material Puddle() => Mat("VFX_puddle", Tex("fx_puddle2", new Color(0.03f, 0.05f, 0.08f), 0.9f, 0.1f, 11), 0.95f);

        static Material Mat(string name, Texture2D tex, float normalBlend)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Shader Graphs/Decal")); AssetDatabase.CreateAsset(m, path); }
            if (m.HasProperty("Base_Map")) m.SetTexture("Base_Map", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("Normal_Blend")) m.SetFloat("Normal_Blend", normalBlend);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Irregular blob: rgb = colour, alpha = peak × soft edge (edge width `soft`), outline wobble from a seeded noise.</summary>
        static Texture2D Tex(string name, Color rgb, float peak, float soft, int seed)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.png";
            if (!File.Exists(path))
            {
                const int S = 256;
                var rng = new System.Random(seed);
                var amp = new float[6]; var ph = new float[6];
                for (int k = 0; k < 6; k++) { amp[k] = (float)rng.NextDouble() * 0.12f / (k + 1); ph[k] = (float)rng.NextDouble() * 6.283f; }
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                        float edge = 0.78f;
                        for (int k = 0; k < 6; k++) edge += amp[k] * Mathf.Sin((k + 2) * a + ph[k]);
                        float alpha = peak * Mathf.Clamp01((edge - r) / soft);
                        float grain = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.07f + seed, y * 0.07f);
                        px[y * S + x] = new Color(rgb.r * grain, rgb.g * grain, rgb.b * grain, alpha);
                    }
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
                t.SetPixels32(px); t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
