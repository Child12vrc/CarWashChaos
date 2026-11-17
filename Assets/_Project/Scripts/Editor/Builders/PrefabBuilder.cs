using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarWash.Core;
using CarWash.Net;
using CarWash.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>Builds networked prefabs from primitives / imported models + the NetworkPrefabsList. Idempotent.</summary>
    public static class PrefabBuilder
    {
        public const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        public const string LotStatePath = "Assets/_Project/Prefabs/World/LotState.prefab";
        public const string ListPath = "Assets/_Project/Prefabs/NetworkPrefabs.asset";
        const string YBotPath = "Assets/_Project/Art/Models/YBot/YBot.fbx";
        const string BodyControllerPath = "Assets/_Project/Art/Animations/TPS_Player.controller";


        [MenuItem("CarWash/Build/Prefabs")]
        public static void BuildAll()
        {
            var player = BuildPlayer();
            var all = new List<GameObject> { player };
            all.AddRange(PropBuilder.BuildAll());
            all.AddRange(ToolBuilder.BuildAll());
            all.AddRange(VehicleBuilder.BuildAll());
            all.Add(BuildLotState());
            all.AddRange(DayBuilder.BuildAll());
            all.AddRange(CritterBuilder.BuildAll());
            BuildList(all);
        }

        public static GameObject BuildPlayer()
        {
            var data = DataBuilder.LoadRegistry();
            var root = new GameObject("Player") { layer = Layers.Player };
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.4f; cc.slopeLimit = 45f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<OwnerNetworkTransform>();
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;

            var input = root.AddComponent<PlayerInputReader>();
            var status = root.AddComponent<PlayerStatus>();
            var motor = root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();
            var net = root.AddComponent<PlayerNet>();
            var body = root.AddComponent<PlayerBody>();
            var grabber = root.AddComponent<PlayerGrabber>();
            var interactor = root.AddComponent<PlayerInteractor>();
            root.AddComponent<PlayerPing>(); // T4.4
            var tools = root.AddComponent<PlayerTools>();
            var inventory = root.AddComponent<PlayerInventory>();

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0, 1.65f, 0);
            var camGo = new GameObject("FPCamera", typeof(Camera), typeof(AudioListener));
            camGo.transform.SetParent(head, false);
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.fieldOfView = 90f; cam.enabled = false;
            camGo.GetComponent<AudioListener>().enabled = false;

            // Third-person body (others see it; owner sees only its shadow)
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(YBotPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Body";
            model.transform.SetParent(root.transform, false);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Player;
            var anim = model.GetComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BodyControllerPath);
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            // P3 ragdoll + P5 comfort camera
            var ragBones = RagdollBuilder.Build(anim);
            var ragdoll = root.AddComponent<PlayerRagdoll>();
            SceneBuilder.Set(ragdoll, "_animator", anim);
            SetArray(ragdoll, "_bones", ragBones.Cast<Object>().ToArray());
            root.AddComponent<PlayerCameraRig>();

            SceneBuilder.Set(motor, "_data", data);
            SceneBuilder.Set(motor, "_input", input);
            SceneBuilder.Set(motor, "_status", status);
            SceneBuilder.Set(look, "_data", data);
            SceneBuilder.Set(look, "_input", input);
            SceneBuilder.Set(look, "_motor", motor);
            SceneBuilder.Set(look, "_head", head);
            SceneBuilder.Set(look, "_camera", cam);
            SceneBuilder.Set(body, "_animator", anim);
            SceneBuilder.Set(body, "_status", status);
            SceneBuilder.Set(grabber, "_data", data);
            SceneBuilder.Set(grabber, "_input", input);
            SceneBuilder.Set(grabber, "_look", look);
            SceneBuilder.Set(grabber, "_motor", motor);
            SceneBuilder.Set(interactor, "_data", data);
            SceneBuilder.Set(interactor, "_input", input);
            SceneBuilder.Set(interactor, "_look", look);
            SceneBuilder.Set(tools, "_input", input);
            SceneBuilder.Set(tools, "_look", look);
            SceneBuilder.Set(tools, "_grabber", grabber);
            SceneBuilder.Set(tools, "_inventory", inventory);
            SetArray(net, "_bodyRenderers", model.GetComponentsInChildren<Renderer>(true).Cast<Object>().ToArray());

            var prefab = Save(root, PlayerPath);
            Debug.Log("[PrefabBuilder] Player prefab built");
            return prefab;
        }

        static GameObject BuildLotState()
        {
            var root = new GameObject("LotState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<Vehicles.LotState>();
            return Save(root, LotStatePath);
        }

        public static GameObject Save(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// P1: every replicated transform uses tick-latency-aware Lerp interpolation + unreliable deltas (no head-of-line
        /// blocking when packets drop; NGO still sends periodic full states) — fixes stepping under jitter/loss.
        /// </summary>
        public static void TuneTransforms(GameObject prefab)
        {
            bool dirty = false;
            foreach (var nt in prefab.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true))
            {
                nt.Interpolate = true;
                nt.PositionInterpolationType = Unity.Netcode.Components.NetworkTransform.InterpolationTypes.SmoothDampening;
                nt.RotationInterpolationType = Unity.Netcode.Components.NetworkTransform.InterpolationTypes.Lerp;
                // P1 measured: server-auth objects (cars, critters, props, tools) are smoothest with reliable deltas (jitter
                // reorders packets; unreliable-sequenced drops the late ones); owner-auth players use unreliable deltas so a
                // lost packet on a bad uplink never freezes them waiting for a resend.
                nt.UseUnreliableDeltas = nt is Net.OwnerNetworkTransform;
                EditorUtility.SetDirty(nt); dirty = true;
            }
            if (dirty) PrefabUtility.SavePrefabAsset(prefab);
        }

        static void BuildList(List<GameObject> prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, ListPath);
            }
            foreach (var p in list.PrefabList.ToList()) list.Remove(p);
            foreach (var go in prefabs.Where(p => p != null).Distinct()) { TuneTransforms(go); list.Add(new NetworkPrefab { Prefab = go }); }
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PrefabBuilder] NetworkPrefabsList: {list.PrefabList.Count} prefabs");
        }

        public static NetworkPrefabsList LoadList() => AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath);

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
