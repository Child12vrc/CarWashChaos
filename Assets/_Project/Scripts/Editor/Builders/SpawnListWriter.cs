using System.Collections.Generic;
using CarWash.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>Collects spawn entries in a builder and writes them to a scene NetworkSpawnList.</summary>
    public class SpawnListWriter
    {
        readonly List<(string path, Vector3 pos, Vector3 euler)> _items = new List<(string, Vector3, Vector3)>();

        public void Add(string prefabPath, Vector3 pos, Vector3 euler) => _items.Add((prefabPath, pos, euler));

        public NetworkSpawnList WriteTo(GameObject host)
        {
            var list = host.GetComponent<NetworkSpawnList>();
            if (list == null) list = host.AddComponent<NetworkSpawnList>();
            var so = new SerializedObject(list);
            var arr = so.FindProperty("_entries");
            arr.arraySize = _items.Count;
            for (int i = 0; i < _items.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_items[i].path);
                if (prefab == null) throw new System.Exception("missing prefab " + _items[i].path);
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab.GetComponent<NetworkObject>();
                e.FindPropertyRelative("position").vector3Value = _items[i].pos;
                e.FindPropertyRelative("euler").vector3Value = _items[i].euler;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return list;
        }
    }
}
