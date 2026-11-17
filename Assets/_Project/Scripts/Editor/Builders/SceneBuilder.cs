using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using CarWash.Net;
using CarWash.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarWash.Editor
{
    /// <summary>Scene builders. Menu: CarWash/Build/... Each builder overwrites its scene (idempotent).</summary>
    public static class SceneBuilder
    {
        [MenuItem("CarWash/Build/Configure Project")]
        public static void ConfigureProject() => ProjectConfigurator.Apply();

        [MenuItem("CarWash/Build/Scenes (Bootstrap + MainMenu)")]
        public static void BuildCoreScenes()
        {
            BuildBootstrap();
            BuildMainMenu();
            RegisterBuildScenes();
        }

        public static void BuildBootstrap()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            utp.SetConnectionData("127.0.0.1", NetSession.DefaultPort, "0.0.0.0");
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                ConnectionApproval = true,
                TickRate = 30,
                EnableSceneManagement = true,
            };
            var net = go.AddComponent<NetSession>();
            Set(net, "_manager", nm);

            var audio = new GameObject("AudioService").AddComponent<AudioService>();
            audio.transform.SetParent(go.transform, false);

            var boot = go.AddComponent<GameBootstrap>();
            Set(boot, "_data", DataBuilder.LoadRegistry());
            Set(boot, "_net", net);
            Set(boot, "_audio", audio);
            Set(boot, "_skin", ArtBuilder.LoadSkin());

            var list = PrefabBuilder.LoadList();
            if (list != null) nm.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { list };
            Save(scene, SceneNames.Bootstrap);
        }

        public static void BuildMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cam.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cam.GetComponent<Camera>().backgroundColor = new Color(0.09f, 0.13f, 0.2f);
            new GameObject("MainMenu").AddComponent<MainMenuUI>();
            Save(scene, SceneNames.MainMenu);
        }

        /// <summary>Bootstrap first; game scenes next; other existing scenes (e.g. TPS test map) kept after.</summary>
        public static void RegisterBuildScenes()
        {
            var ordered = new[] { SceneNames.Bootstrap, SceneNames.MainMenu, SceneNames.Lobby, SceneNames.SudsyLot, SceneNames.DirtTest }
                .Select(SceneNames.Path).Where(p => System.IO.File.Exists(p)).ToList();
            var rest = EditorBuildSettings.scenes.Where(s => !ordered.Contains(s.path) && System.IO.File.Exists(s.path)).ToList();
            var list = new List<EditorBuildSettingsScene>(ordered.Select(p => new EditorBuildSettingsScene(p, true)));
            list.AddRange(rest);
            EditorBuildSettings.scenes = list.ToArray();
        }

        internal static void Save(Scene scene, string name)
        {
            System.IO.Directory.CreateDirectory(SceneNames.Folder);
            EditorSceneManager.SaveScene(scene, SceneNames.Path(name));
            Debug.Log("[SceneBuilder] saved " + name);
        }

        internal static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) throw new System.Exception($"{target.GetType().Name}.{field} not found");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
