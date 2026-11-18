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
        public const string BayLightSwitchPath = "Assets/_Project/Prefabs/World/BayLightSwitch.prefab";

        public static List<GameObject> BuildAll() => new List<GameObject> { BuildDirector(), BuildBayButton(), BuildDayBoard(), BuildBayLightSwitch() };

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

        /// <summary>M4.9 G7: wall light switch (faces -X like the DONE button): plate, rocker, state LED.</summary>
        static GameObject BuildBayLightSwitch()
        {
            var root = new GameObject("BayLightSwitch");
            root.layer = Layers.Interactable;
            root.AddComponent<NetworkObject>();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.1f, 0.3f, 0.22f);
            var sw = root.AddComponent<Day.BayLightSwitch>();
            GrayboxMaterials.Box(root.transform, "Plate", Vector3.zero, new Vector3(0.03f, 0.2f, 0.14f), GrayboxMaterials.Get("SwitchPlate", new Color(0.92f, 0.92f, 0.9f), 0.5f), collider: false, layer: Layers.Interactable);
            var rocker = GrayboxMaterials.Box(root.transform, "Rocker", new Vector3(-0.025f, 0f, 0f), new Vector3(0.025f, 0.11f, 0.07f), GrayboxMaterials.Get("SwitchRocker", new Color(0.8f, 0.8f, 0.78f), 0.4f), collider: false, layer: Layers.Interactable);
            var ledOff = GrayboxMaterials.Get("SwitchLed_Off", new Color(0.2f, 0.12f, 0.1f), 0.6f);
            var ledOn = GrayboxMaterials.Get("SwitchLed_On", new Color(0.4f, 1f, 0.5f), 0.6f);
            ledOn.EnableKeyword("_EMISSION"); ledOn.SetColor("_EmissionColor", new Color(0.3f, 1f, 0.4f) * 2.5f); EditorUtility.SetDirty(ledOn);
            var led = GrayboxMaterials.Box(root.transform, "Led", new Vector3(-0.02f, 0.085f, 0f), new Vector3(0.012f, 0.014f, 0.014f), ledOff, collider: false, layer: Layers.Interactable);
            Set(sw, "_rocker", rocker.transform);
            Set(sw, "_led", led.GetComponent<Renderer>());
            Set(sw, "_ledOn", ledOn);
            Set(sw, "_ledOff", ledOff);
            return PrefabBuilder.Save(root, BayLightSwitchPath);
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
