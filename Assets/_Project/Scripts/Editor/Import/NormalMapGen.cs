using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.6 D5: stand-in normal maps from albedo luminance (tileable Sobel on a slightly blurred height). The source PNG is only
    /// read (never modified); the map goes to Art/Generated/Normals/&lt;name&gt;_n.png, imported as a normal map. Hand-made maps
    /// dropped next to the albedo as &lt;name&gt;_normal.png win (ART_REQUESTS Batch G).
    /// </summary>
    public static class NormalMapGen
    {
        public const string OutDir = "Assets/_Project/Art/Generated/Normals";

        /// <summary>Normal map for an albedo texture asset path (hand-made first, else generated / cached). Null if no source.</summary>
        public static Texture2D For(string albedoPath, float strength = 2f)
        {
            if (string.IsNullOrEmpty(albedoPath) || !File.Exists(albedoPath)) return null;
            string name = Path.GetFileNameWithoutExtension(albedoPath);
            string handMade = Path.Combine(Path.GetDirectoryName(albedoPath), name + "_normal.png").Replace('\\', '/');
            if (File.Exists(handMade)) { MarkNormal(handMade); return AssetDatabase.LoadAssetAtPath<Texture2D>(handMade); }
            string outPath = $"{OutDir}/{name}_n.png";
            if (!File.Exists(outPath) || File.GetLastWriteTimeUtc(outPath) < File.GetLastWriteTimeUtc(albedoPath))
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllBytes(outPath, Generate(File.ReadAllBytes(albedoPath), strength).EncodeToPNG());
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            }
            MarkNormal(outPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static void MarkNormal(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
        }

        public static Texture2D Generate(byte[] png, float strength)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(png);
            int w = src.width, h = src.height;
            var px = src.GetPixels32();
            var height = new float[w * h];
            for (int i = 0; i < px.Length; i++) height[i] = (0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b) / 255f;
            // 3×3 box blur (wrap) so JPEG-ish noise does not turn into speckle
            var blur = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float s = 0f;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) s += height[((y + dy + h) % h) * w + (x + dx + w) % w];
                    blur[y * w + x] = s / 9f;
                }
            float H(int x, int y) => blur[((y + h) % h) * w + (x + w) % w];
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float gx = (H(x + 1, y - 1) + 2 * H(x + 1, y) + H(x + 1, y + 1)) - (H(x - 1, y - 1) + 2 * H(x - 1, y) + H(x - 1, y + 1));
                    float gy = (H(x - 1, y + 1) + 2 * H(x, y + 1) + H(x + 1, y + 1)) - (H(x - 1, y - 1) + 2 * H(x, y - 1) + H(x + 1, y - 1));
                    var n = new Vector3(-gx * strength, -gy * strength, 1f).normalized;
                    outPx[y * w + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx); tex.Apply();
            Object.DestroyImmediate(src);
            return tex;
        }

        [MenuItem("CarWash/Build/Generate Normal Maps")]
        public static void GenerateAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/Environment/Textures" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("_normal.png")) continue;
                For(p);
            }
            Debug.Log("[NormalMapGen] done → " + OutDir);
        }
    }
}
