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
            var sp = _spawnPoints.Length > 0 ? _spawnPoints[index % _spawnPoints.Length] : transform;
            var obj = Instantiate(_playerPrefab, sp.position, sp.rotation);
            obj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[PlayerSpawner] spawned player for client {clientId} at {sp.position}");
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
