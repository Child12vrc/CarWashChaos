using System.IO;
using CarWash.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CarWash.Editor
{
    /// <summary>
    /// TECH S07: NavMeshSurface baked for SudsyLot with agent types Small (r0.3) / Large (r0.8).
    /// Bakes static Default-layer geometry only (vehicles/props are dynamic); bay shutters are excluded and carve at runtime.
    /// </summary>
    public static class LotNavMesh
    {
        public static readonly (string name, float radius, float height)[] Agents = { ("Small", 0.3f, 0.6f), ("Large", 0.8f, 1.5f) };

        public static int AgentTypeId(string name)
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == name) return s.agentTypeID;
            }
            return -1;
        }

        /// <summary>Adds the agent types to ProjectSettings/NavMeshAreas.asset (same data the Navigation window edits).</summary>
        public static void EnsureAgentTypes()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];
            var so = new SerializedObject(asset);
            var settings = so.FindProperty("m_Settings");
            var names = so.FindProperty("m_SettingNames");
            foreach (var (name, radius, height) in Agents)
            {
                if (AgentTypeId(name) >= 0) continue;
                settings.InsertArrayElementAtIndex(settings.arraySize - 1);
                var e = settings.GetArrayElementAtIndex(settings.arraySize - 1);
                e.FindPropertyRelative("agentTypeID").intValue = Random.Range(10000, int.MaxValue);
                e.FindPropertyRelative("agentRadius").floatValue = radius;
                e.FindPropertyRelative("agentHeight").floatValue = height;
                names.InsertArrayElementAtIndex(names.arraySize > 0 ? names.arraySize - 1 : 0);
                names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                Debug.Log($"[NavMesh] added agent type {name} (r={radius})");
            }
            AssetDatabase.SaveAssets();
        }

        public static void Bake(GameObject env, Scene scene)
        {
            EnsureAgentTypes();
            // shutters move at runtime: keep them out of the bake, carve instead
            foreach (var t in env.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Shutter"))
                {
                    t.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                    var obs = t.gameObject.AddComponent<NavMeshObstacle>();
                    obs.carving = true; obs.shape = NavMeshObstacleShape.Box; obs.size = Vector3.one;
                }

            var root = new GameObject("NavMesh");
            string dir = SceneNames.Folder + SceneNames.SudsyLot;
            Directory.CreateDirectory(dir);
            foreach (var (name, _, _) in Agents)
            {
                var surface = root.AddComponent<NavMeshSurface>();
                surface.agentTypeID = AgentTypeId(name);
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = 1 << Layers.Default;
                surface.BuildNavMesh();
                string path = $"{dir}/NavMesh-{name}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(surface.navMeshData, path);
                surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                Debug.Log($"[NavMesh] baked {name} → {path}");
            }
        }
    }
}
