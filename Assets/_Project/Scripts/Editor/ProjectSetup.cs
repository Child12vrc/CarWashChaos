// 프로젝트 초기 설정 자동화 스크립트
// 메뉴: Tools > Project Setup > Apply Recommended Settings
// 배치 모드: Unity.exe -batchmode -projectPath <path> -executeMethod ProjectSetup.RunBatch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

public static class ProjectSetup
{
    static readonly string[] Folders =
    {
        "_Project/Art/Models", "_Project/Art/Materials", "_Project/Art/Textures", "_Project/Art/Animations",
        "_Project/Shaders/ShaderGraphs", "_Project/Shaders/SubGraphs", "_Project/Shaders/HLSL",
        "_Project/Scripts/Runtime", "_Project/Scripts/Editor",
        "_Project/Prefabs", "_Project/Scenes", "_Project/Audio", "_Project/VFX", "_Project/Settings",
        "ThirdParty"
    };

    public static void RunBatch()
    {
        try { Apply(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError("[ProjectSetup] FAILED: " + e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Project Setup/Apply Recommended Settings")]
    public static void Apply()
    {
        // 1) 폴더 구조
        foreach (var f in Folders) Directory.CreateDirectory(Path.Combine(Application.dataPath, f));
        AssetDatabase.Refresh();
        Log("Folders created");

        // 2) Player 설정
        var win = BuildTarget.StandaloneWindows64;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetUseDefaultGraphicsAPIs(win, false);
        PlayerSettings.SetGraphicsAPIs(win, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        SetSerialized("ProjectSettings/ProjectSettings.asset", "activeInputHandler", p => p.intValue = 1); // 1 = Input System (New)
        Log("Player settings: Linear, DX12 > DX11, Mono (dev), Input System");

        // 3) Editor 설정: 도메인 리로드 끄기 (빠른 Play Mode 진입)
        SetSerialized("ProjectSettings/EditorSettings.asset", "m_EnterPlayModeOptionsEnabled", p => p.boolValue = true);
        SetSerialized("ProjectSettings/EditorSettings.asset", "m_EnterPlayModeOptions", p => p.intValue = 1); // DisableDomainReload
        SetSerialized("ProjectSettings/EditorSettings.asset", "m_SerializationMode", p => p.intValue = 2);   // Force Text
        Log("Editor settings: Fast Enter Play Mode, Force Text");

        // 4) GPU Resident Drawer 를 위한 BatchRendererGroup 변형 유지
        SetSerializedEnumByName("ProjectSettings/GraphicsSettings.asset", "m_BrgStripping", "Keep");

        // 5) URP 에셋 / 렌더러 설정 (프로젝트 내 모든 URP 에셋)
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            var so = new SerializedObject(asset);
            SetProp(so, "m_GPUResidentDrawerMode", p => p.enumValueIndex = 1); // Instanced Drawing
            SetEnumByName(so, "m_UpscalingFilter", "STP");
            so.ApplyModifiedPropertiesWithoutUndo();
            Log("URP asset updated: " + path);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
            SetEnumByName(so, "m_RenderingMode", "ForwardPlus");
            so.ApplyModifiedPropertiesWithoutUndo();
            Log("Renderer set to Forward+: " + path);
        }

        // 6) 외부 스크립트 에디터: VS Code
        var code = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
        if (File.Exists(code)) { EditorPrefs.SetString("kScriptsDefaultApp", code); Log("External script editor: VS Code"); }

        AssetDatabase.SaveAssets();
        Log("DONE");
    }

    // ---------- helpers ----------
    static void Log(string m) => Debug.Log("[ProjectSetup] " + m);

    static void SetSerialized(string assetPath, string prop, System.Action<SerializedProperty> set)
    {
        var obj = AssetDatabase.LoadAllAssetsAtPath(assetPath).FirstOrDefault();
        if (obj == null) { Debug.LogWarning("[ProjectSetup] Not found: " + assetPath); return; }
        var so = new SerializedObject(obj);
        SetProp(so, prop, set);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedEnumByName(string assetPath, string prop, string contains)
    {
        var obj = AssetDatabase.LoadAllAssetsAtPath(assetPath).FirstOrDefault();
        if (obj == null) return;
        var so = new SerializedObject(obj);
        SetEnumByName(so, prop, contains);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetProp(SerializedObject so, string prop, System.Action<SerializedProperty> set)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[ProjectSetup] Property not found: " + prop); return; }
        set(p);
        Log("Set " + prop);
    }

    static void SetEnumByName(SerializedObject so, string prop, string contains)
    {
        var p = so.FindProperty(prop);
        if (p == null || p.propertyType != SerializedPropertyType.Enum) { Debug.LogWarning("[ProjectSetup] Enum not found: " + prop); return; }
        var names = p.enumNames;
        for (int i = 0; i < names.Length; i++)
            if (names[i].Replace(" ", "").ToLower().Contains(contains.ToLower()))
            { p.enumValueIndex = i; Log($"Set {prop} = {names[i]}"); return; }
        Debug.LogWarning($"[ProjectSetup] Enum value '{contains}' not in {prop}: {string.Join(",", names)}");
    }
}
