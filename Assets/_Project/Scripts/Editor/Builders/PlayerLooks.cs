using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarWash.Core;
using CarWash.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarWash.Editor
{
    /// <summary>
    /// docs/ART_IMPORT.md "Player characters": every Quaternius look (Humanoid FBX, Art/ThirdParty/Quaternius/Characters) as a
    /// child of the player's "Body": its own Animator on the shared Mixamo locomotion controller (humanoid retargeting), its own
    /// 11-bone ragdoll (RagdollBuilder, humanoid bone map), and a player-colour outline shell per SkinnedMeshRenderer
    /// (CarWash/PlayerOutline, sharing the bones). Defaults first (P1-P4), then the extras. Falls back to the Y Bot when the
    /// pack is missing.
    /// </summary>
    public static class PlayerLooks
    {
        public const string Root = "Assets/_Project/Art/ThirdParty/Quaternius/Characters";
        public const string ControllerPath = "Assets/_Project/Art/Animations/TPS_Player.controller";
        public const string YBotPath = "Assets/_Project/Art/Models/YBot/YBot.fbx";
        public const string OutlineMatPath = "Assets/_Project/Art/Materials/PlayerOutline.mat";

        /// <summary>id → Korean name in the lobby picker. Order = look index (P1-P4 defaults first).</summary>
        public static readonly (string id, string ko)[] Catalog =
        {
            ("Male_Worker", "작업복 (남)"), ("Female_Worker", "작업복 (여)"), ("Male_Casual", "캐주얼 (남)"), ("Female_Casual", "캐주얼 (여)"),
            ("Male_Casual2", "캐주얼 2 (남)"), ("Male_Punk", "펑크 (남)"), ("Female_Punk", "펑크 (여)"), ("Male_Beach", "해변 (남)"),
            ("Male_Farmer", "농부 (남)"), ("Male_Suit", "정장 (남)"), ("Female_Suit", "정장 (여)"), ("Female_Formal", "드레스 (여)"),
        };

        public static string PathOf(string id) => $"{Root}/{(id.StartsWith("Female") ? "Women" : "Men")}/{id}.fbx";

        public static List<PlayerAppearance.LookEntry> Build(Transform body)
        {
            var list = new List<PlayerAppearance.LookEntry>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            // M4.6 D8: HandIK needs the IK pass on the base layer
            if (controller is UnityEditor.Animations.AnimatorController ac && ac.layers.Length > 0 && !ac.layers[0].iKPass)
            {
                var layers = ac.layers; layers[0].iKPass = true; ac.layers = layers;
                EditorUtility.SetDirty(ac); AssetDatabase.SaveAssets();
            }
            foreach (var (id, ko) in Catalog)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(id));
                if (src == null) continue;
                list.Add(Look(body, src, "Look_" + id, id, ko, controller));
            }
            if (list.Count == 0)
            {
                Debug.LogWarning("[PlayerLooks] no Quaternius characters — Y Bot fallback");
                list.Add(Look(body, AssetDatabase.LoadAssetAtPath<GameObject>(YBotPath), "Look_YBot", "YBot", "Y Bot", controller, outline: false));
            }
            for (int i = 0; i < list.Count; i++) list[i].root.SetActive(i == 0);
            return list;
        }

        static PlayerAppearance.LookEntry Look(Transform body, GameObject src, string name, string id, string ko, RuntimeAnimatorController controller, bool outline = true)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = name;
            model.transform.SetParent(body, false);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Player;
            var anim = model.GetComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var bones = RagdollBuilder.Build(anim);
            var renderers = new List<Renderer>(model.GetComponentsInChildren<Renderer>(true));
            if (outline)
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    renderers.Add(Outline(smr));
            return new PlayerAppearance.LookEntry { id = id, nameKo = ko, root = model, animator = anim, bones = bones, renderers = renderers.ToArray() };
        }

        /// <summary>Inverted-hull copy of a body mesh in the player colour (same bones → follows animation and ragdoll).</summary>
        static Renderer Outline(SkinnedMeshRenderer smr)
        {
            var go = new GameObject(smr.name + "_Outline") { layer = Layers.Player };
            go.transform.SetParent(smr.transform.parent, false);
            go.transform.localPosition = smr.transform.localPosition;
            go.transform.localRotation = smr.transform.localRotation;
            go.transform.localScale = smr.transform.localScale;
            var o = go.AddComponent<SkinnedMeshRenderer>();
            o.sharedMesh = smr.sharedMesh;
            o.bones = smr.bones;
            o.rootBone = smr.rootBone;
            o.localBounds = smr.localBounds;
            var mat = OutlineMaterial();
            o.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : 1)).ToArray();
            o.shadowCastingMode = ShadowCastingMode.Off;
            o.receiveShadows = false;
            o.lightProbeUsage = LightProbeUsage.Off;
            o.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return o;
        }

        public static Material OutlineMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            if (m == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutlineMatPath));
                m = new Material(Shader.Find("CarWash/PlayerOutline")) { name = "PlayerOutline" };
                AssetDatabase.CreateAsset(m, OutlineMatPath);
            }
            m.shader = Shader.Find("CarWash/PlayerOutline");
            m.SetFloat("_Width", 0.018f);
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void Wire(PlayerAppearance appearance, List<PlayerAppearance.LookEntry> looks)
        {
            var so = new SerializedObject(appearance);
            var arr = so.FindProperty("_looks");
            arr.arraySize = looks.Count;
            for (int i = 0; i < looks.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var l = looks[i];
                e.FindPropertyRelative("id").stringValue = l.id;
                e.FindPropertyRelative("nameKo").stringValue = l.nameKo;
                e.FindPropertyRelative("root").objectReferenceValue = l.root;
                e.FindPropertyRelative("animator").objectReferenceValue = l.animator;
                var b = e.FindPropertyRelative("bones"); b.arraySize = l.bones.Length;
                for (int k = 0; k < l.bones.Length; k++) b.GetArrayElementAtIndex(k).objectReferenceValue = l.bones[k];
                var r = e.FindPropertyRelative("renderers"); r.arraySize = l.renderers.Length;
                for (int k = 0; k < l.renderers.Length; k++) r.GetArrayElementAtIndex(k).objectReferenceValue = l.renderers[k];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
