using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>Server: critter id → prefab; spawns a critter hidden and pops it out (reveal sting). Scene object (Customers).</summary>
    public class CritterSpawner : MonoBehaviour
    {
        public static CritterSpawner Instance { get; private set; }
        [SerializeField] private NetworkObject[] _prefabs;
        readonly Dictionary<string, NetworkObject> _byId = new Dictionary<string, NetworkObject>();

        void Awake()
        {
            Instance = this;
            foreach (var p in _prefabs) if (p != null) _byId[p.GetComponent<CritterBase>().CritterId] = p;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Has(string id) => id != null && _byId.ContainsKey(id);

        public CritterBase ServerSpawnRevealed(string id, Vector3 pos, Quaternion rot, bool fromSurprise)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !_byId.TryGetValue(id, out var prefab)) return null;
            var no = Instantiate(prefab, pos, rot);
            var c = no.GetComponent<CritterBase>();
            c.FromSurprise = fromSurprise;
            c.Home = pos;
            no.Spawn(true);
            c.ServerReveal();
            return c;
        }
    }
}
