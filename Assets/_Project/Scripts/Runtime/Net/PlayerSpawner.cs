using Unity.Netcode;
using UnityEngine;

namespace CarWash.Net
{
    /// <summary>
    /// Server: spawns one player object per connected client at the locker spawn points (GDD §12: mid-day joins spawn at lockers).
    /// Lives in the game scene.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private Transform[] _spawnPoints;

        NetworkManager _nm;

        void Start()
        {
            _nm = NetworkManager.Singleton;
            if (_nm == null || !_nm.IsServer) return;
            _nm.OnClientConnectedCallback += OnClientConnected;
            foreach (var id in _nm.ConnectedClientsIds) SpawnFor(id);
        }

        void OnDestroy()
        {
            if (_nm != null) _nm.OnClientConnectedCallback -= OnClientConnected;
        }

        void OnClientConnected(ulong id) => SpawnFor(id);

        void SpawnFor(ulong clientId)
        {
            if (!_nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject != null) return;
            int index = _nm.ConnectedClientsIds.Count > 0 ? IndexOf(clientId) : 0;
            var (pos, rot) = SafeSpawn(index);
            var obj = Instantiate(_playerPrefab, pos, rot);
            var net = obj.GetComponent<Player.PlayerNet>();
            if (net != null) net.SpawnPos.Value = pos; // M4.6 B3: the owner re-applies it (owner-authoritative transform)
            obj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[PlayerSpawner] spawned player for client {clientId} at {pos}");
        }

        /// <summary>
        /// M4.6 B3: the slot's locker spawn if a player capsule fits there (inside the play area, nothing solid, no other player);
        /// otherwise the next free locker spot, otherwise the nearest safe NavMesh spot (Unstuck) around the lockers.
        /// </summary>
        (Vector3, Quaternion) SafeSpawn(int index)
        {
            if (_spawnPoints.Length == 0) return (transform.position, transform.rotation);
            for (int k = 0; k < _spawnPoints.Length; k++)
            {
                var sp = _spawnPoints[(index + k) % _spawnPoints.Length];
                if (Free(sp.position)) return (sp.position, sp.rotation);
            }
            var first = _spawnPoints[index % _spawnPoints.Length];
            var spot = Player.Unstuck.FindSafeSpot(first.position, first.position);
            Debug.LogWarning($"[PlayerSpawner] every locker spawn blocked → fallback {spot}");
            return (spot, first.rotation);
        }

        static bool Free(Vector3 p)
        {
            if (!Player.Unstuck.HasRoom(p)) return false;
            foreach (var other in FindObjectsByType<Player.PlayerNet>(FindObjectsSortMode.None))
                if ((other.transform.position - p).sqrMagnitude < 0.8f * 0.8f) return false;
            return true;
        }

        int IndexOf(ulong clientId)
        {
            int i = 0;
            foreach (var id in _nm.ConnectedClientsIds) { if (id == clientId) return i; i++; }
            return 0;
        }

        public Transform SpawnPointFor(ulong clientId) => _spawnPoints.Length > 0 ? _spawnPoints[(int)(clientId % (ulong)_spawnPoints.Length)] : transform;
    }
}
