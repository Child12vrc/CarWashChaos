// Mixamo Y Bot TPS 캐릭터 자동 세팅
// 메뉴: Tools > TPS > Build All
using System.Collections.Generic;
using System.Linq;
using MyGame.TPS;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class TPSSetup
{
    const string ModelPath = "Assets/_Project/Art/Models/YBot/YBot.fbx";
    const string AnimDir = "Assets/_Project/Art/Animations/Mixamo";
    const string ControllerPath = "Assets/_Project/Art/Animations/TPS_Player.controller";
    const string PrefabPath = "Assets/_Project/Prefabs/TPS_Player.prefab";
    const string ScenePath = "Assets/_Project/Scenes/TPS_TestMap.unity";
    const string MatDir = "Assets/_Project/Art/Materials/TestMap";
    const int PlayerLayer = 6;

    static readonly string[] LoopClips =
    {
        "Idle", "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Run_F", "Run_B", "Run_L", "Run_R",
        "Crouch_Idle", "Crouch_F", "Crouch_B", "Crouch_L", "Crouch_R",
        "Stairs_WalkUp", "Stairs_WalkDown", "Stairs_RunUp", "Stairs_RunDown"
    };
    static readonly string[] OtherClips = { "Jump", "Turn_L90", "Turn_R90" };
    static readonly string[] PhaseSyncClips =
    {
        "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Run_F", "Run_B", "Run_L", "Run_R",
        "Crouch_F", "Crouch_B", "Crouch_L", "Crouch_R",
        "Stairs_WalkUp", "Stairs_WalkDown", "Stairs_RunUp", "Stairs_RunDown"
    };

    // 점프: 앞쪽 준비동작(웅크림)을 잘라 입력 후 바로 뜨도록. (원본 30fps: 발이 0.78s 에 떨어지고 1.30s 착지)
    const float JumpStartSec = 0.62f;
    const float JumpEndSec = 1.62f;

    [MenuItem("Tools/TPS/Build All (Import + Animator + Prefab + Test Map)")]
    public static void BuildAll()
    {
        EnsureLayer();
        ConfigureImports();
        SyncFootPhases();
        BuildController();
        BuildPrefab();
        BuildScene();
        Debug.Log("[TPSSetup] ALL DONE → " + ScenePath);
    }

    // ------------------------------------------------------------------ Layer
    [MenuItem("Tools/TPS/0. Ensure Player Layer")]
    public static void EnsureLayer()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        var p = layers.GetArrayElementAtIndex(PlayerLayer);
        if (p.stringValue != "Player")
        {
            p.stringValue = "Player";
            tm.ApplyModifiedPropertiesWithoutUndo();
        }
        Log("Layer 6 = Player");
    }

    // ------------------------------------------------------------------ Import
    [MenuItem("Tools/TPS/1. Configure Imports")]
    public static void ConfigureImports()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (mi == null) throw new System.Exception("Y Bot model not found: " + ModelPath);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        mi.SaveAndReimport();

        var avatar = LoadAvatar();
        if (avatar == null || !avatar.isHuman || !avatar.isValid) throw new System.Exception("Y Bot humanoid avatar invalid");
        Log("Y Bot avatar OK");

        foreach (var n in LoopClips.Concat(OtherClips)) ConfigureClip(n, avatar, 0f);
    }

    static void ConfigureClip(string name, Avatar avatar, float cycleOffset, bool offsetOnly = false)
    {
        string path = $"{AnimDir}/{name}.fbx";
        var mi = (ModelImporter)AssetImporter.GetAtPath(path);
        if (mi == null) { Debug.LogWarning("[TPSSetup] missing " + path); return; }

        if (!offsetOnly)
        {
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = avatar;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
        }

        var src = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
        if (src.Length == 0) { Debug.LogWarning("[TPSSetup] no clip in " + path); return; }
        var c = src[0];

        bool loop = LoopClips.Contains(name);
        bool isJump = name == "Jump";
        bool isTurn = name.StartsWith("Turn");
        bool isIdle = name.EndsWith("Idle");
        bool isStairs = name.StartsWith("Stairs");

        c.name = name;
        c.loopTime = loop;
        c.loopPose = loop;
        if (isJump)
        {
            c.firstFrame = JumpStartSec * 30f;
            c.lastFrame = JumpEndSec * 30f;
        }
        // 회전: 포즈에 굽기 (방향은 스크립트가 제어). 제자리 회전 클립만 루트 회전 사용
        c.lockRootRotation = !isTurn;
        c.keepOriginalOrientation = true;
        // 수직: 발 기준. 점프 = 높이를 루트모션으로 사용 / 계단 = 오르내림을 포즈에 굽지 않음(루프 때 튀지 않게, 높이는 캡슐이 담당)
        c.lockRootHeightY = !isJump && !isStairs;
        c.keepOriginalPositionY = false;
        c.heightFromFeet = true;
        // 수평: 이동 애니메이션은 루트모션 사용 (발 미끄러짐 방지). Idle/점프는 굽기
        c.lockRootPositionXZ = isIdle || isJump || isTurn;
        c.keepOriginalPositionXZ = false;
        c.cycleOffset = cycleOffset;

        mi.clipAnimations = new[] { c };
        mi.SaveAndReimport();
    }

    // ------------------------------------------------------------------ Foot phase sync
    /// <summary>
    /// 각 이동 클립의 발 위상(오른발-왼발 높이차의 1차 고조파)을 구해서 cycleOffset 으로 정렬한다.
    /// 블렌드트리에서 방향이 섞일 때 서로 다른 발이 겹쳐 꼬이는 현상을 막는다.
    /// </summary>
    [MenuItem("Tools/TPS/2. Sync Foot Phases")]
    public static void SyncFootPhases()
    {
        var avatar = LoadAvatar();
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        try
        {
            var anim = go.GetComponent<Animator>();
            if (!anim) anim = go.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.Rebind();
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);

            var offsets = new Dictionary<string, float>();
            foreach (var name in PhaseSyncClips)
            {
                var clip = LoadClip(name);
                if (!clip) continue;
                clip.SampleAnimation(go, 0f); // warm-up

                const int N = 64;
                double sx = 0, sy = 0, amp = 0;
                for (int i = 0; i < N; i++)
                {
                    clip.SampleAnimation(go, clip.length * i / N);
                    float s = rf.position.y - lf.position.y;
                    double a = 2.0 * Mathf.PI * i / N;
                    sx += s * System.Math.Cos(a);
                    sy += s * System.Math.Sin(a);
                    amp = System.Math.Max(amp, System.Math.Abs(s));
                }

                float phase = (float)(System.Math.Atan2(sy, sx) / (2.0 * Mathf.PI));
                if (phase < 0f) phase += 1f;
                float mag = (float)(System.Math.Sqrt(sx * sx + sy * sy) * 2.0 / N);
                offsets[name] = phase;
                Log($"{name}: len {clip.length:0.00}s, avgSpeed {clip.averageSpeed.magnitude:0.00} m/s, footAmp {amp:0.000}, harmonic {mag:0.000}, phase {phase:0.000}");
            }

            foreach (var kv in offsets) ConfigureClip(kv.Key, avatar, kv.Value, offsetOnly: true);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------------ Animator
    [MenuItem("Tools/TPS/3. Build Animator Controller")]
    public static void BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ac.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        ac.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Aim", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;

        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // 바깥: Speed(0 대기 / 1 걷기 / 2 달리기) 1D 블렌드
        // 안쪽: 방향(MoveX, MoveZ) 2D Simple Directional → 대각선은 인접 두 방향이 50:50 으로 섞임 (대기 모션이 섞여 느려지지 않음)
        var locoState = ac.CreateBlendTreeInController("Locomotion", out BlendTree loco, 0);
        loco.blendType = BlendTreeType.Simple1D;
        loco.blendParameter = "Speed";
        loco.useAutomaticThresholds = false;
        loco.AddChild(LoadClip("Idle"), 0f);
        loco.AddChild(DirTree(ac, loco, "Walk", "Walk_F", "Walk_B", "Walk_L", "Walk_R"), 1f);
        loco.AddChild(DirTree(ac, loco, "Run", "Run_F", "Run_B", "Run_L", "Run_R"), 2f);

        var crouchState = ac.CreateBlendTreeInController("CrouchLocomotion", out BlendTree crouch, 0);
        crouch.blendType = BlendTreeType.Simple1D;
        crouch.blendParameter = "Speed";
        crouch.useAutomaticThresholds = false;
        crouch.AddChild(LoadClip("Crouch_Idle"), 0f);
        crouch.AddChild(DirTree(ac, crouch, "CrouchWalk", "Crouch_F", "Crouch_B", "Crouch_L", "Crouch_R"), 1f);

        var jump = sm.AddState("Jump");
        jump.motion = LoadClip("Jump");
        jump.tag = "Jump";
        jump.writeDefaultValues = true;

        sm.defaultState = locoState;

        // 상태 위치 정리
        var states = sm.states;
        for (int i = 0; i < states.Length; i++)
        {
            var s = states[i];
            if (s.state == locoState) s.position = new Vector3(300, 100);
            else if (s.state == crouchState) s.position = new Vector3(300, 250);
            else if (s.state == jump) s.position = new Vector3(600, 100);
            states[i] = s;
        }
        sm.states = states;

        var t = locoState.AddTransition(crouchState);
        t.hasExitTime = false; t.duration = 0.3f;
        t.AddCondition(AnimatorConditionMode.If, 0, "Crouch");

        t = crouchState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.3f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");

        t = locoState.AddTransition(jump);
        t.hasExitTime = false; t.duration = 0.12f;
        t.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        t = jump.AddTransition(locoState);
        t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.2f;

        var layers = ac.layers;
        layers[0].iKPass = true;
        ac.layers = layers;

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Log("Animator controller built: " + ControllerPath);
    }

    static BlendTree DirTree(AnimatorController ac, BlendTree parent, string name, string f, string b, string l, string r)
    {
        var t = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveZ",
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(t, ac);
        AddChild(t, f, 0, 1);
        AddChild(t, b, 0, -1);
        AddChild(t, l, -1, 0);
        AddChild(t, r, 1, 0);
        return t;
    }

    static void AddChild(BlendTree tree, string clip, float x, float y)
    {
        var c = LoadClip(clip);
        if (!c) { Debug.LogWarning("[TPSSetup] clip not found " + clip); return; }
        tree.AddChild(c, new Vector2(x, y));
    }

    // ------------------------------------------------------------------ Prefab
    [MenuItem("Tools/TPS/4. Build Player Prefab")]
    public static void BuildPrefab()
    {
        var root = new GameObject("TPS_Player") { layer = PlayerLayer };
        var cc = root.AddComponent<CharacterController>();
        cc.radius = 0.3f;
        cc.height = 1.8f;
        cc.skinWidth = 0.03f;
        cc.center = new Vector3(0, 0.9f + cc.skinWidth, 0);
        cc.stepOffset = 0.4f;
        cc.slopeLimit = 50f;
        cc.minMoveDistance = 0f;

        var input = root.AddComponent<TPSInput>();
        var ctrl = root.AddComponent<TPSCharacterController>();

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        model.name = "YBot";
        model.transform.SetParent(root.transform, false);
        SetLayerRecursive(model, PlayerLayer);

        var anim = model.GetComponent<Animator>();
        if (!anim) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        anim.avatar = LoadAvatar();
        anim.applyRootMotion = true;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.updateMode = AnimatorUpdateMode.Normal;

        model.AddComponent<TPSAnimatorBridge>().owner = ctrl;
        model.AddComponent<TPSIK>().owner = ctrl;

        ctrl.animator = anim;
        ctrl.input = input;

        System.IO.Directory.CreateDirectory("Assets/_Project/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Log("Prefab saved: " + PrefabPath);
    }

    // ------------------------------------------------------------------ Test map
    [MenuItem("Tools/TPS/5. Build Test Map Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var light = Object.FindFirstObjectByType<Light>();
        if (light)
        {
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            light.shadows = LightShadows.Soft;
        }

        var mGround = Mat("Ground", new Color(0.42f, 0.44f, 0.46f));
        var mStairs = Mat("Stairs", new Color(0.93f, 0.55f, 0.2f));
        var mPlatform = Mat("Platform", new Color(0.25f, 0.5f, 0.85f));
        var mJump = Mat("JumpBox", new Color(0.3f, 0.7f, 0.35f));
        var mTunnel = Mat("Tunnel", new Color(0.55f, 0.35f, 0.75f));
        var mProp = Mat("Prop", new Color(0.8f, 0.8f, 0.78f));
        var mRamp = Mat("Ramp", new Color(0.85f, 0.75f, 0.3f));

        var env = new GameObject("Environment").transform;

        // 바닥 + 1m 격자 라인 (이동/미끄러짐 확인용)
        Box(env, "Ground", new Vector3(0, -0.5f, 0), new Vector3(80, 1, 80), mGround);
        var mLine = Mat("GridLine", new Color(0.33f, 0.35f, 0.37f));
        var grid = new GameObject("Grid").transform; grid.SetParent(env);
        for (int i = -20; i <= 20; i += 2)
        {
            Box(grid, "LX" + i, new Vector3(i, 0.001f, 0), new Vector3(0.03f, 0.002f, 40f), mLine, collider: false);
            Box(grid, "LZ" + i, new Vector3(0, 0.001f, i), new Vector3(40f, 0.002f, 0.03f), mLine, collider: false);
        }

        // 중앙 플랫폼 (높이 2m) + 서로 다른 단 높이의 계단 3종
        Box(env, "Platform_2m", new Vector3(0, 1f, 12.5f), new Vector3(6, 2, 6), mPlatform);
        Stairs(env, "Stairs_A_20cm", new Vector3(0, 0, 6f), Vector3.forward, 10, 0.2f, 0.35f, 3f, mStairs);
        Stairs(env, "Stairs_B_12.5cm", new Vector3(0, 0, 20.3f), Vector3.back, 16, 0.125f, 0.3f, 3f, mStairs);
        Stairs(env, "Stairs_C_25cm", new Vector3(6.2f, 0, 12.5f), Vector3.left, 8, 0.25f, 0.4f, 3f, mStairs);

        // 경사로 + 상단 블록
        var ramp = Box(env, "Ramp_15deg", Vector3.zero, new Vector3(3, 0.2f, 8), mRamp);
        ramp.transform.rotation = Quaternion.Euler(-15f, 0, 0);
        ramp.transform.position = new Vector3(-10f, Mathf.Sin(15f * Mathf.Deg2Rad) * 4f - 0.1f, 8f);
        float rampTop = Mathf.Sin(15f * Mathf.Deg2Rad) * 8f;
        float rampEndZ = 8f + Mathf.Cos(15f * Mathf.Deg2Rad) * 4f;
        Box(env, "Ramp_Top", new Vector3(-10f, rampTop * 0.5f - 0.05f, rampEndZ + 1.5f), new Vector3(3, rampTop - 0.1f, 3), mPlatform);

        // 점프 테스트용 박스 (높이 0.3 / 0.5 / 0.8)
        Box(env, "JumpBox_0.3", new Vector3(9f, 0.15f, 0f), new Vector3(2, 0.3f, 2), mJump);
        Box(env, "JumpBox_0.5", new Vector3(9f, 0.25f, 3.5f), new Vector3(2, 0.5f, 2), mJump);
        Box(env, "JumpBox_0.8", new Vector3(9f, 0.4f, 7f), new Vector3(2, 0.8f, 2), mJump);

        // 앉아서 통과하는 터널 (천장 1.45m)
        var tunnel = new GameObject("CrouchTunnel_1.45m").transform; tunnel.SetParent(env);
        Box(tunnel, "WallL", new Vector3(-9.6f, 0.8f, -8f), new Vector3(0.4f, 1.6f, 6f), mTunnel);
        Box(tunnel, "WallR", new Vector3(-6.4f, 0.8f, -8f), new Vector3(0.4f, 1.6f, 6f), mTunnel);
        Box(tunnel, "Roof", new Vector3(-8f, 1.6f, -8f), new Vector3(3.6f, 0.3f, 6f), mTunnel);

        // 카메라 충돌/시선 테스트용 기둥과 벽
        for (int i = 0; i < 4; i++)
            Box(env, "Pillar_" + i, new Vector3(-4f + i * 2.7f, 1.5f, -6f), new Vector3(0.6f, 3f, 0.6f), mProp);
        Box(env, "Wall_Back", new Vector3(3f, 1.25f, -12f), new Vector3(10f, 2.5f, 0.4f), mProp);
        Box(env, "Crate_1", new Vector3(5f, 0.5f, -3f), new Vector3(1, 1, 1), mProp);
        Box(env, "Crate_2", new Vector3(-5f, 0.35f, 2f), new Vector3(0.7f, 0.7f, 0.7f), mProp);

        // 플레이어
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.SetPositionAndRotation(new Vector3(0, 0, 0), Quaternion.identity);
        var ctrl = player.GetComponent<TPSCharacterController>();

        // 카메라
        var camGO = Camera.main ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        var rig = camGO.GetComponent<TPSCamera>();
        if (!rig) rig = camGO.AddComponent<TPSCamera>();
        rig.target = player.transform;
        rig.input = player.GetComponent<TPSInput>();
        rig.character = ctrl;
        camGO.transform.position = new Vector3(-0.65f, 2.2f, -3.2f);
        ctrl.cameraRig = rig;

        var hud = new GameObject("HUD").AddComponent<TPSHud>();
        hud.character = ctrl;

        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        Log("Scene saved: " + ScenePath);
    }

    static void Stairs(Transform parent, string name, Vector3 start, Vector3 dir, int steps, float rise, float run, float width, Material m)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent);
        var rot = Quaternion.LookRotation(dir);
        for (int i = 0; i < steps; i++)
        {
            float h = (i + 1) * rise;
            var go = Box(root, "Step_" + i, start + dir * (run * (i + 0.5f)) + Vector3.up * (h * 0.5f), new Vector3(width, h, run), m);
            go.transform.rotation = rot;
        }
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    static Material Mat(string name, Color c)
    {
        System.IO.Directory.CreateDirectory(MatDir);
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.color = c;
        m.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ helpers
    static Avatar LoadAvatar() => AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();

    static AnimationClip LoadClip(string name) =>
        AssetDatabase.LoadAllAssetsAtPath($"{AnimDir}/{name}.fbx").OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
    }

    static void Log(string m) => Debug.Log("[TPSSetup] " + m);
}
