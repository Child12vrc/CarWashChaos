using System;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Net
{
    /// <summary>
    /// Server-side spawner for scene content (props, tools, cages...). We never place NetworkObjects in scenes by builder
    /// (their in-scene GlobalObjectIdHash would be stale for builds); instead the scene stores plain spawn entries.
    /// </summary>
    public class NetworkSpawnList : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public NetworkObject prefab;
            public Vector3 position;
            public Vector3 euler;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public event Action<NetworkObject> Spawned;

        void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            foreach (var e in _entries)
            {
                if (e.prefab == null) continue;
                var obj = Instantiate(e.prefab, e.position, Quaternion.Euler(e.euler));
                obj.Spawn(true);
                Spawned?.Invoke(obj);
            }
        }
    }
}
