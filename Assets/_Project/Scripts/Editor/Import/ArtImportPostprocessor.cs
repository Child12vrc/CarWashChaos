using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// docs/ART_IMPORT.md rules:
    ///  Art/UI/**            → Sprite (Single, Full Rect), alpha is transparency, no mips, bilinear, HQ compression, 100 PPU,
    ///                         spriteBorder (L,B,R,T) read from the ART_IMPORT.md table (the table is the source of truth).
    ///  Art/VFX/Textures/**  → Default, alpha is transparency, mips ON, clamp, sRGB.
    /// PNGs themselves are never modified.
    /// </summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        public const string UiRoot = "Assets/_Project/Art/UI/";
        public const string VfxRoot = "Assets/_Project/Art/VFX/Textures/";
        const string SpecPath = "docs/ART_IMPORT.md";

        static Dictionary<string, Vector4> _borders;

        /// <summary>file name (no extension) → border (x=L, y=B, z=R, w=T), parsed from the markdown table.</summary>
        public static Dictionary<string, Vector4> Borders
        {
            get
            {
                if (_borders != null) return _borders;
                _borders = new Dictionary<string, Vector4>();
                if (!File.Exists(SpecPath)) { Debug.LogWarning("[ArtImport] " + SpecPath + " not found — no 9-slice borders"); return _borders; }
                var row = new Regex(@"^\|\s*Art/[^|]*?/(?<name>[^/|]+)\.png\s*\|[^|]*\|\s*(?<b>[\d\s,]*)\|");
                foreach (var line in File.ReadAllLines(SpecPath))
                {
                    var m = row.Match(line);
                    if (!m.Success) continue;
                    var parts = m.Groups["b"].Value.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 4) continue;
                    _borders[m.Groups["name"].Value] = new Vector4(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                }
                return _borders;
            }
        }

        public static void ResetCache() => _borders = null;

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string path = assetPath.Replace('\\', '/');
            if (path.StartsWith(UiRoot)) ApplyUi(ti, Path.GetFileNameWithoutExtension(path));
            else if (path.StartsWith(VfxRoot)) ApplyVfx(ti);
            else if (path.StartsWith(EnvRoot)) ApplyEnvironment(ti, path);
        }

        public const string EnvRoot = "Assets/_Project/Art/Environment/";

        /// <summary>P6 (docs/ART_IMPORT.md "Environment"): surfaces repeat + aniso 4; backdrop mirror-U/clamp-V; signs alpha.</summary>
        public static void ApplyEnvironment(TextureImporter ti, string path)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            if (path.Contains("/Textures/"))
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                ti.alphaSource = TextureImporterAlphaSource.None;
            }
            else if (path.Contains("/Backdrop/"))
            {
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.wrapModeU = TextureWrapMode.Mirror;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.wrapModeW = TextureWrapMode.Clamp;
            }
            else // Signs
            {
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.anisoLevel = 2;
            }
        }

        public static void ApplyUi(TextureImporter ti, string name)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteBorder = Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
            s.spritePixelsPerUnit = 100f;
            ti.SetTextureSettings(s);
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        public static void ApplyVfx(TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
        }

        /// <summary>Re-import every art texture so the rules (and current table) apply. Menu + CI step.</summary>
        [MenuItem("CarWash/Build/Reimport Art")]
        public static void ReimportAll()
        {
            ResetCache();
            foreach (var root in new[] { UiRoot, VfxRoot, EnvRoot })
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root.TrimEnd('/') }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ArtImport] reimported art; {Borders.Count} 9-slice borders from {SpecPath}");
        }
    }
}
