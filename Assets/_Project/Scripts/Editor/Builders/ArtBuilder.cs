using System.IO;
using System.Linq;
using CarWash.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarWash.Editor
{
    /// <summary>
    /// Wires generated art (docs/ART_IMPORT.md) into engine assets: UiSkin, VFX particle/decal materials, URP Decal feature.
    /// Idempotent; run before prefab/scene builders.
    /// </summary>
    public static class ArtBuilder
    {
        public const string SkinPath = "Assets/_Project/UI/UiSkin.asset";
        public const string MatDir = "Assets/_Project/Art/VFX/Materials";
        const string Ui = "Assets/_Project/Art/UI/";
        const string Fx = "Assets/_Project/Art/VFX/Textures/";

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        [MenuItem("CarWash/Build/Art (skin + VFX materials)")]
        public static void BuildAll()
        {
            ArtImportPostprocessor.ReimportAll();
            BuildSkin();
            foreach (var n in new[] { "fx_water_droplet", "fx_mist_puff", "fx_splash_sheet", "fx_foam_blob", "fx_bubble", "fx_coin", "fx_stun_star" }) Particle(n);
            FoamDecal();
            EnsureDecalFeature();
            AssetDatabase.SaveAssets();
        }

        static Sprite S(string rel) => AssetDatabase.LoadAssetAtPath<Sprite>(Ui + rel + ".png");

        public static UiSkin BuildSkin()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SkinPath));
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(SkinPath);
            if (skin == null) { skin = ScriptableObject.CreateInstance<UiSkin>(); AssetDatabase.CreateAsset(skin, SkinPath); }
            skin.panel = S("Frames/ui_panel"); skin.card = S("Frames/ui_card"); skin.toast = S("Frames/ui_toast");
            skin.btnPrimary = S("Frames/ui_btn_primary"); skin.btnHover = S("Frames/ui_btn_hover");
            skin.btnPressed = S("Frames/ui_btn_pressed"); skin.btnDisabled = S("Frames/ui_btn_disabled");
            skin.crosshair = S("HUD/ui_crosshair"); skin.crosshairHit = S("HUD/ui_crosshair_hit");
            skin.slot = S("HUD/ui_slot"); skin.slotSelected = S("HUD/ui_slot_selected");
            skin.barBg = S("Frames/ui_bar_bg"); skin.barFill = S("Frames/ui_bar_fill");
            skin.barFillWhite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            skin.icons = AssetDatabase.FindAssets("t:Sprite", new[] { Ui + "Icons" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(sp => sp != null).ToArray();
            skin.ResetIconCache();
            if (skin.icons.Length != 34) Debug.LogWarning($"[ArtBuilder] expected 34 icons (docs/ICONS.md), found {skin.icons.Length}");
            EditorUtility.SetDirty(skin);
            var missing = typeof(UiSkin).GetFields().Where(f => f.FieldType == typeof(Sprite) && f.GetValue(skin) == null).Select(f => f.Name).ToArray();
            if (missing.Length > 0) Debug.LogWarning("[ArtBuilder] UiSkin missing sprites: " + string.Join(", ", missing));
            return skin;
        }

        public static UiSkin LoadSkin() => AssetDatabase.LoadAssetAtPath<UiSkin>(SkinPath);

        /// <summary>URP Particles/Unlit, alpha-blended, base map = fx texture (white/gray textures are tinted by Start Color).</summary>
        public static Material Particle(string fx)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/VFX_{fx}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Fx + fx + ".png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material FoamDecal()
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/VFX_fx_foam_decal.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Shader Graphs/Decal")); AssetDatabase.CreateAsset(m, path); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Fx + "fx_foam_decal.png");
            if (m.HasProperty("Base_Map")) m.SetTexture("Base_Map", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Adds a DecalRendererFeature to every URP renderer that lacks one (same steps as the renderer inspector's "Add Renderer Feature").</summary>
        public static void EnsureDecalFeature()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" })) // never touch immutable package assets
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null || data.rendererFeatures.Any(f => f is DecalRendererFeature)) continue;
                var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = "Decal";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                data.SetDirty();
                EditorUtility.SetDirty(data);
                Debug.Log("[ArtBuilder] added Decal renderer feature to " + path);
            }
        }
    }
}
