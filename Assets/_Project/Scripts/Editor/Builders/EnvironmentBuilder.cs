using System.IO;
using System.Linq;
using CarWash.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarWash.Editor
{
    /// <summary>
    /// P6 map polish for SudsyLot: URP procedural sky + sun + fog, a distant city skyline ring (env_skyline_far,
    /// mirrored tiles, unlit alpha-clip, fades into the fog), a post-processing volume (tonemapping, mild bloom,
    /// vignette, colour), SSAO renderer feature, signs (roadside "Sudsy", bay plates, exit), and physic materials on
    /// the static world (asphalt grip, slippery wet bay floors, metal/brick walls). Art comes from Art/Environment;
    /// anything missing is skipped (graybox stays).
    /// </summary>
    public static class EnvironmentBuilder
    {
        const string Env = "Assets/_Project/Art/Environment/";
        const string MatDir = "Assets/_Project/Art/Materials/Environment";
        public const string PhysDir = "Assets/_Project/Art/Physics";

        public static void Build(Transform env, Vehicles.Bay[] bays)
        {
            Directory.CreateDirectory(MatDir);
            Sky();
            Skyline(env);
            PostVolume(env);
            Signs(env, bays);
            PhysicMaterials(env);
            EnsureSsao();
            UrpShadows();
        }

        // ------------------------------------------------------------------ sky, light, fog
        public const string DaySkyPath = MatDir + "/Sky_Day.mat";

        /// <summary>M4.9 G1/G2: the CarWash/DaySky skybox (DayNightCycle drives a runtime copy); the asset holds the noon look.</summary>
        public static Material DaySky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(DaySkyPath);
            if (sky == null) { sky = new Material(Shader.Find("CarWash/DaySky")); AssetDatabase.CreateAsset(sky, DaySkyPath); }
            sky.SetColor("_Zenith", new Color(0.22f, 0.45f, 0.82f)); sky.SetColor("_Horizon", new Color(0.7f, 0.8f, 0.92f));
            sky.SetVector("_SunDir", (Quaternion.Euler(-42f, 145f, 0f) * Vector3.forward));
            sky.SetFloat("_CloudCover", 0.45f); sky.SetFloat("_CloudSpeed", 0.012f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        static void Sky()
        {
            var sky = DaySky();
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 190f;
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                RenderSettings.sun = sun;
            }
        }

        // ------------------------------------------------------------------ distant city
        static void Skyline(Transform env)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Env + "Backdrop/env_skyline_far.png");
            if (tex == null) return;
            string path = MatDir + "/Skyline.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
            m.SetColor("_BaseColor", new Color(0.82f, 0.86f, 0.95f));
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.4f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);                                     // both sides
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);

            var ring = new GameObject("CitySkyline").transform; ring.SetParent(env);
            const int segments = 14; const float radius = 110f;
            float width = 2f * Mathf.PI * radius / segments * 1.02f;
            float height = width * tex.height / tex.width;
            for (int i = 0; i < segments; i++)
            {
                float ang = (i + 0.5f) * 360f / segments;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Skyline" + i;
                q.transform.SetParent(ring, false);
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                q.transform.position = dir * radius + Vector3.up * (height * 0.5f - 1.5f);
                q.transform.rotation = Quaternion.LookRotation(dir);       // faces outward; Cull Off shows it from the lot
                q.transform.localScale = new Vector3(i % 2 == 0 ? width : -width, height, 1f); // mirrored every other tile (A, A', A…)
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            }
        }

        // ------------------------------------------------------------------ post-processing
        static void PostVolume(Transform env)
        {
            string path = MatDir + "/Lot_PostProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            T Get<T>() where T : VolumeComponent { if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); } return c; }
            var tm = Get<Tonemapping>(); tm.mode.Override(TonemappingMode.Neutral);
            var bloom = Get<Bloom>(); bloom.threshold.Override(1.05f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f);
            var vig = Get<Vignette>(); vig.intensity.Override(0.18f); vig.smoothness.Override(0.4f);
            var ca = Get<ColorAdjustments>(); ca.saturation.Override(8f); ca.contrast.Override(6f);
            var wb = Get<WhiteBalance>(); wb.temperature.Override(4f);
            EditorUtility.SetDirty(profile);
            var go = new GameObject("PostProcess (Global Volume)"); go.transform.SetParent(env);
            var v = go.AddComponent<Volume>(); v.isGlobal = true; v.priority = 0f; v.sharedProfile = profile;
            // the overview camera (before a player spawns) renders post too
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null) { var d = cam.GetUniversalAdditionalCameraData(); d.renderPostProcessing = true; }
        }

        /// <summary>Soft sun shadows out to 70 m, 2 cascades, on the project's own URP assets (never package assets).</summary>
        static void UrpShadows()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a == null) continue;
                a.shadowDistance = 70f;
                a.shadowCascadeCount = 2;
                var so = new SerializedObject(a);
                var soft = so.FindProperty("m_SoftShadowsSupported");
                if (soft != null) soft.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(a);
            }
        }

        static void EnsureSsao()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || data.rendererFeatures.Exists(f => f is ScreenSpaceAmbientOcclusion)) continue;
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, data);
                data.rendererFeatures.Add(ssao);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
        }

        // ------------------------------------------------------------------ signs
        static Material SignMat(string file)
        {
            Directory.CreateDirectory(MatDir);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Env + "Signs/" + file);
            if (tex == null) return null;
            string path = $"{MatDir}/Sign_{Path.GetFileNameWithoutExtension(file)}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex; m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f); m.SetFloat("_Smoothness", 0.3f);
            m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.black); m.EnableKeyword("_EMISSION"); // G1: glows at night
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A lit alpha-clip quad sized from the texture's aspect (height in metres).</summary>
        public static GameObject Sign(Transform parent, string file, Vector3 pos, float yaw, float height)
        {
            var m = SignMat(file);
            if (m == null) return null;
            var tex = (Texture2D)m.mainTexture;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Sign_" + Path.GetFileNameWithoutExtension(file);
            q.transform.SetParent(parent, false);
            q.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw + 180f, 0f)); // quad front faces -Z → turn it to face `yaw`
            q.transform.localScale = new Vector3(height * tex.width / tex.height, height, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = m;
            return q;
        }

        static void Signs(Transform env, Vehicles.Bay[] bays)
        {
            var root = new GameObject("Signs").transform; root.SetParent(env);
            var pole = GrayboxMaterials.Get("SignPole", new Color(0.35f, 0.37f, 0.4f), 0.5f);
            // big roadside sign by the lot entry (west end of the road), facing the road
            GrayboxMaterials.Box(root, "SudsyPole", new Vector3(-27f, 3f, -19.2f), new Vector3(0.25f, 6f, 0.25f), pole);
            Sign(root, "sign_sudsy.png", new Vector3(-27f, 6.2f, -19.05f), 0f, 3.2f);
            NightSpot(root, "SudsyLight", new Vector3(-27f, 4.2f, -17.6f), new Vector3(-27f, 6.2f, -19.05f), 9f, 6f, new Color(1f, 0.9f, 0.75f), 0.4f);
            // plate above each bay entrance (north opening), readable from the feed lane
            foreach (var b in bays)
                Sign(root, $"sign_bay{b.Index + 1}.png", b.transform.position + new Vector3(0f, 3.15f, 5.25f), 0f, 0.75f);
            // exit sign at the exit ramp
            GrayboxMaterials.Box(root, "ExitPole", new Vector3(22.2f, 1.4f, -6.2f), new Vector3(0.12f, 2.8f, 0.12f), pole);
            Sign(root, "sign_exit.png", new Vector3(22.1f, 2.6f, -6.2f), -90f, 0.7f); // faces the cars coming from the west
        }

        // ------------------------------------------------------------------ M4.9 G1 day / night
        /// <summary>A shadowless spot light aimed at <paramref name="at"/> that fades in at night (lamps, signs).</summary>
        public static Light NightSpot(Transform parent, string name, Vector3 pos, Vector3 at, float range, float intensity, Color c, float onAt, float angle = 70f)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(at - pos));
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.range = range; l.intensity = intensity; l.color = c; l.spotAngle = angle; l.innerSpotAngle = angle * 0.55f;
            l.shadows = LightShadows.None; l.enabled = false;
            go.AddComponent<Visual.NightLight>().Init(new[] { l }, onAt);
            return l;
        }

        /// <summary>The local day / night driver + the shared materials that glow at night (lamp heads, signs).</summary>
        public static void DayNight(Transform env)
        {
            var go = new GameObject("DayNightCycle"); go.transform.SetParent(env);
            var dn = go.AddComponent<Visual.DayNightCycle>();
            SceneBuilder.Set(dn, "_sun", RenderSettings.sun);
            SceneBuilder.Set(dn, "_skyAsset", DaySky());
            var far = new System.Collections.Generic.List<Object> { AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Skyline.mat") };
            foreach (var n in new[] { "FarA", "FarB", "FarC", "FarD" }) far.Add(AssetDatabase.LoadAssetAtPath<Material>(SceneryBuilder.MatDir + "/" + n + ".mat"));
            PrefabBuilder.SetArray(dn, "_far", far.Where(m => m != null).ToArray());
            var mats = new System.Collections.Generic.List<Material>(); var cols = new System.Collections.Generic.List<Color>(); var on = new System.Collections.Generic.List<float>();
            void G(Material m, Color c, float o) { if (m == null) return; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); EditorUtility.SetDirty(m); mats.Add(m); cols.Add(c); on.Add(o); }
            G(GrayboxMaterials.Get("Scenery_LampHead", new Color(1f, 0.95f, 0.8f), 0.8f), new Color(1f, 0.85f, 0.6f) * 3f, 0.35f);
            foreach (var guid in AssetDatabase.FindAssets("Sign_ t:Material", new[] { MatDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") != null) G(m, Color.white * 0.9f, 0.45f);
            }
            var so = new SerializedObject(dn);
            void Arr<T>(string p, System.Collections.Generic.List<T> list, System.Action<SerializedProperty, T> set) { var a = so.FindProperty(p); a.arraySize = list.Count; for (int i = 0; i < list.Count; i++) set(a.GetArrayElementAtIndex(i), list[i]); }
            Arr("_glowMats", mats, (p, v) => p.objectReferenceValue = v);
            Arr("_glowCols", cols, (p, v) => p.colorValue = v);
            Arr("_glowOnAt", on, (p, v) => p.floatValue = v);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ physics
        public static PhysicsMaterial Phys(string name, float dynamicF, float staticF, float bounce)
        {
            Directory.CreateDirectory(PhysDir);
            string path = $"{PhysDir}/{name}.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m == null) { m = new PhysicsMaterial(name); AssetDatabase.CreateAsset(m, path); }
            m.dynamicFriction = dynamicF; m.staticFriction = staticF; m.bounciness = bounce;
            m.frictionCombine = PhysicsMaterialCombine.Average; m.bounceCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static PhysicsMaterial Asphalt => Phys("Asphalt", 0.8f, 0.9f, 0.02f);
        public static PhysicsMaterial WetConcrete => Phys("WetConcrete", 0.35f, 0.45f, 0.02f);
        public static PhysicsMaterial Metal => Phys("Metal", 0.4f, 0.5f, 0.12f);
        public static PhysicsMaterial Brick => Phys("Brick", 0.75f, 0.85f, 0.05f);

        static void PhysicMaterials(Transform env)
        {
            foreach (var c in env.GetComponentsInChildren<Collider>(true))
            {
                string n = c.name;
                if (n == "Ground") c.sharedMaterial = Asphalt;
                else if (n == "Floor") c.sharedMaterial = WetConcrete;                    // bay floors: props skid
                else if (n.StartsWith("Wall") || n.StartsWith("Shutter") || n == "Roof" || n.StartsWith("Post") || n.Contains("Pole")) c.sharedMaterial = Metal;
                else if (n == "Office" || n.StartsWith("Fence")) c.sharedMaterial = Brick;
            }
        }
    }
}
