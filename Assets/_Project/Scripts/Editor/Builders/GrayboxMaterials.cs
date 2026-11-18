using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>Flat-color URP Lit materials for graybox builders (MaterialBuilder lite).</summary>
    public static class GrayboxMaterials
    {
        public const string Dir = "Assets/_Project/Art/Materials/Graybox";

        public static Material Get(string name, Color color, float smoothness = 0.2f)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, int layer = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null && TileMeters.TryGetValue(mat, out var tile)) WorldUv(go.GetComponent<MeshFilter>(), size, tile);
            return go;
        }

        // ---- P6: textured surfaces (docs/ART_IMPORT.md Environment) ----
        public const string EnvTex = "Assets/_Project/Art/Environment/Textures/";
        static readonly System.Collections.Generic.Dictionary<Material, float> TileMeters = new System.Collections.Generic.Dictionary<Material, float>();

        // ---- M4.7 A1: PolyHaven CC0 PBR sets (user art, Art/ThirdParty/PolyHaven/<set>/<set>_{diff,nor_gl,arm}_1k.jpg) ----
        public const string PolyHavenDir = "Assets/_Project/Art/ThirdParty/PolyHaven";
        public const string PbrDir = "Assets/_Project/Art/Generated/PBR";
        /// <summary>Old single textures → the PolyHaven set that replaces them (when delivered).</summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> PolyHavenFor = new System.Collections.Generic.Dictionary<string, string>
        {
            { "tex_asphalt.png", "asphalt_02" }, { "tex_concrete_wet.png", "concrete_floor_02" },
            { "tex_brick.png", "red_brick_03" }, { "tex_metal_blue.png", "box_profile_metal_sheet" },
        };

        static string PhFile(string set, string map) => $"{PolyHavenDir}/{set}/{set}_{map}_1k.jpg";
        public static bool HasSet(string set) => File.Exists(PhFile(set, "nor_gl")) && File.Exists(PhFile(set, "arm"));

        /// <summary>
        /// Puts a PolyHaven set on a URP Lit material: diff → _BaseMap (sRGB; skipped when useAlbedo is false, e.g. seat fabric
        /// keeps a plain tint), nor_gl → _BumpMap as-is (OpenGL convention = Unity's), arm (R AO, G roughness, B metal) → a
        /// generated mask (R metal, G AO, A smoothness = 1 − roughness) used as _MetallicGlossMap + _OcclusionMap.
        /// The delivered JPGs are only read (the normal map's import type is set to NormalMap).
        /// </summary>
        public static bool ApplyPbr(Material m, string set, bool useAlbedo = true, float normalScale = 1f, bool greyAlbedo = false)
        {
            if (!HasSet(set)) return false;
            if (useAlbedo && File.Exists(PhFile(set, "diff")))
            {
                var diff = greyAlbedo ? GreyAlbedo(set) : AssetDatabase.LoadAssetAtPath<Texture2D>(PhFile(set, "diff"));
                m.SetTexture("_BaseMap", diff); m.mainTexture = diff;
            }
            else { m.SetTexture("_BaseMap", null); m.mainTexture = null; }
            m.SetTextureScale("_BaseMap", Vector2.one);
            string nrmPath = PhFile(set, "nor_gl");
            if (AssetImporter.GetAtPath(nrmPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath)); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP");
            var mask = PbrMask(set);
            m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", mask); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_Smoothness", 1f); // the map's alpha is the smoothness (URP multiplies it by this)
            m.SetFloat("_Metallic", 1f);
            EditorUtility.SetDirty(m);
            return true;
        }

        /// <summary>Art/Generated/PBR/&lt;set&gt;_grey.png: the albedo as luminance, mean lifted to ~0.8 so a tint sets the colour
        /// (M4.7: the bay walls keep their blue with the red box-profile sheet's ribs and rivets).</summary>
        public static Texture2D GreyAlbedo(string set)
        {
            string src = PhFile(set, "diff"), dst = $"{PbrDir}/{set}_grey.png";
            if (!File.Exists(dst) || File.GetLastWriteTimeUtc(dst) < File.GetLastWriteTimeUtc(src))
            {
                Directory.CreateDirectory(PbrDir);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(src));
                var px = t.GetPixels();
                float mean = 0f; foreach (var c in px) mean += c.grayscale; mean /= Mathf.Max(1, px.Length);
                float k = 0.8f / Mathf.Max(0.05f, mean);
                for (int i = 0; i < px.Length; i++) { float g = Mathf.Clamp01(px[i].grayscale * k); px[i] = new Color(g, g, g, 1f); }
                t.SetPixels(px); t.Apply();
                File.WriteAllBytes(dst, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        /// <summary>Art/Generated/PBR/&lt;set&gt;_mask.png from the ARM map (cached; rebuilt when the ARM file is newer).</summary>
        public static Texture2D PbrMask(string set)
        {
            string src = PhFile(set, "arm"), dst = $"{PbrDir}/{set}_mask.png";
            if (!File.Exists(dst) || File.GetLastWriteTimeUtc(dst) < File.GetLastWriteTimeUtc(src))
            {
                Directory.CreateDirectory(PbrDir);
                var arm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                arm.LoadImage(File.ReadAllBytes(src));
                var px = arm.GetPixels32();
                for (int i = 0; i < px.Length; i++) { var c = px[i]; px[i] = new Color32(c.b, c.r, 0, (byte)(255 - c.g)); }
                var outTex = new Texture2D(arm.width, arm.height, TextureFormat.RGBA32, false, true);
                outTex.SetPixels32(px); outTex.Apply();
                File.WriteAllBytes(dst, outTex.EncodeToPNG());
                Object.DestroyImmediate(arm); Object.DestroyImmediate(outTex);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            }
            if (AssetImporter.GetAtPath(dst) is TextureImporter ti && (ti.sRGBTexture || ti.textureType != TextureImporterType.Default))
            { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        /// <summary>URP Lit + a PolyHaven set; boxes using it get UVs in metres / tileMeters (tileMeters ≤ 0: plain cube UVs —
        /// required for PREFAB content: the per-box UV mesh is not an asset and would be lost when the prefab is saved). Falls
        /// back to a flat colour.</summary>
        public static Material Pbr(string name, Color tint, string set, float tileMeters, bool useAlbedo = true, float smoothness = 0.2f)
        {
            var m = Get(name, tint, smoothness);
            if (!ApplyPbr(m, set, useAlbedo)) return m;
            if (tileMeters > 0f) TileMeters[m] = tileMeters; else TileMeters.Remove(m);
            return m;
        }

        /// <summary>
        /// M4.7 E1: material on CarWash/GroundStochastic (hex-tiled, macro-varied, detail + distance-blended; world-space XZ UVs on
        /// top faces). tileMeters = metres per texture repeat; macro = brightness variation ±; smoothScale multiplies the mask's
        /// smoothness (wet concrete a bit shinier).
        /// </summary>
        public static Material Ground(string name, Color tint, Texture2D albedo, Texture2D normal, Texture2D mask, float tileMeters, float macro = 0.14f, float smoothScale = 1f)
        {
            var m = Get(name, tint);
            var sh = Shader.Find("CarWash/GroundStochastic");
            if (sh == null || albedo == null) return m;
            if (m.shader != sh) m.shader = sh;
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", albedo);
            if (normal != null) m.SetTexture("_BumpMap", normal);
            if (mask != null) m.SetTexture("_MaskMap", mask);
            m.SetFloat("_Tile", tileMeters);
            m.SetFloat("_MacroStrength", macro);
            m.SetFloat("_SmoothnessScale", smoothScale);
            m.enableInstancing = true;
            TileMeters.Remove(m); // world-space UVs: no per-box UV mesh needed
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>URP Lit with a seamless texture; boxes using it get UVs in metres / tileMeters (no stretching).</summary>
        public static Material Textured(string name, Color tint, string texFile, float tileMeters, float smoothness = 0.2f)
        {
            // M4.7 A1: a delivered PolyHaven set wins over the old single texture + generated normal map
            if (PolyHavenFor.TryGetValue(texFile, out var set) && HasSet(set))
            {
                // M4.7 E1: ground sets go on the stochastic ground shader (no visible tiling)
                if (set == "asphalt_02" || set == "concrete_floor_02")
                    return Ground(name, tint, AssetDatabase.LoadAssetAtPath<Texture2D>(PhFile(set, "diff")), AssetDatabase.LoadAssetAtPath<Texture2D>(PhFile(set, "nor_gl")), PbrMask(set), tileMeters,
                        set == "concrete_floor_02" ? 0.10f : 0.14f, set == "concrete_floor_02" ? 1.25f : 1f);
                bool bayBlue = set == "box_profile_metal_sheet" && tint == Color.white; // bay walls: grey sheet tinted the lot's blue
                if (bayBlue) tint = new Color(0.36f, 0.58f, 0.92f);
                var pm = Get(name, tint, smoothness);
                ApplyPbr(pm, set, normalScale: set == "red_brick_03" ? 1.2f : 1f, greyAlbedo: bayBlue);
                TileMeters[pm] = tileMeters;
                return pm;
            }
            var m = Get(name, tint, smoothness);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(EnvTex + texFile);
            if (tex == null) return m;  // art not delivered → stays graybox
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetTextureScale("_BaseMap", Vector2.one);
            // M4.6 D5: lit, bumpy surfaces — normal map (hand-made *_normal.png or generated from the albedo), per-surface metal
            var nrm = NormalMapGen.For(EnvTex + texFile, texFile.Contains("brick") ? 3f : texFile.Contains("metal") ? 2.5f : 1.6f);
            if (nrm != null)
            {
                m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Metallic", texFile.Contains("metal") ? 0.35f : 0f);
            TileMeters[m] = tileMeters;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Cube mesh copy whose per-face UVs are scaled to the face size in metres / tile.</summary>
        static void WorldUv(MeshFilter mf, Vector3 size, float tile)
        {
            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = "CubeWorldUV";
            var n = mesh.normals; var uv = mesh.uv;
            for (int i = 0; i < uv.Length; i++)
            {
                var a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                Vector2 dims = a.x > 0.5f ? new Vector2(size.z, size.y) : a.y > 0.5f ? new Vector2(size.x, size.z) : new Vector2(size.x, size.y);
                uv[i] = Vector2.Scale(uv[i], dims / Mathf.Max(0.01f, tile));
            }
            mesh.uv = uv;
            mf.sharedMesh = mesh;
        }
    }
}
