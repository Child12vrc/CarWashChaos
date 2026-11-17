using System.Collections.Generic;
using CarWash.Core;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>T2.4 networked prefabs: GameDirector (EconomyState + DayManager), BayButton (DONE), DayBoard (start/continue).</summary>
    public static class DayBuilder
    {
        public const string DirectorPath = "Assets/_Project/Prefabs/World/GameDirector.prefab";
        public const string BayButtonPath = "Assets/_Project/Prefabs/World/BayButton.prefab";
        public const string DayBoardPath = "Assets/_Project/Prefabs/World/DayBoard.prefab";

        public static List<GameObject> BuildAll() => new List<GameObject> { BuildDirector(), BuildBayButton(), BuildDayBoard() };

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject BuildDirector()
        {
            var root = new GameObject("GameDirector");
            root.AddComponent<NetworkObject>();
            var eco = root.AddComponent<Economy.EconomyState>();
            var day = root.AddComponent<Day.DayManager>();
            var reg = AssetDatabase.LoadAssetAtPath<Data.DataRegistry>(DataBuilder.RegistryPath);
            Set(eco, "_data", reg);
            Set(day, "_data", reg);
            Set(day, "_coinFx", ArtBuilder.Particle("fx_coin"));
            return PrefabBuilder.Save(root, DirectorPath);
        }

        static GameObject BuildBayButton()
        {
            var root = new GameObject("BayButton");
            root.layer = Layers.Interactable;
            root.AddComponent<NetworkObject>();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.12f, 0.5f, 0.36f); // covers the whole panel face
            var btn = root.AddComponent<Day.BayButton>();
            var face = GrayboxMaterials.Box(root.transform, "Button", new Vector3(-0.02f, 0f, 0f), new Vector3(0.08f, 0.18f, 0.18f),
                GrayboxMaterials.Get("BayButton", new Color(0.8f, 0.15f, 0.15f), 0.6f), collider: false, layer: Layers.Interactable);
            Set(btn, "_lamp", face.GetComponent<Renderer>());
            return PrefabBuilder.Save(root, BayButtonPath);
        }

        static GameObject BuildDayBoard()
        {
            var root = new GameObject("DayBoard");
            root.layer = Layers.Interactable;
            root.AddComponent<NetworkObject>();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(2.4f, 1.2f, 0.25f);
            root.AddComponent<Day.DayBoard>();
            GrayboxMaterials.Box(root.transform, "Sheet", new Vector3(0f, 0f, 0.07f), new Vector3(1.6f, 0.8f, 0.02f),
                GrayboxMaterials.Get("DayBoardSheet", new Color(0.95f, 0.93f, 0.85f)), collider: false, layer: Layers.Interactable);
            return PrefabBuilder.Save(root, DayBoardPath);
        }
    }
}
