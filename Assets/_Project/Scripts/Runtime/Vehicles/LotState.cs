using Unity.Netcode;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Server-owned lot state replicated to everyone (spawned from NetworkSpawnList): which bays are open (bitmask).
    /// GDD §11: closed bays have the shutter down; U04 opens Bay 2 / Bay 3.
    /// </summary>
    public class LotState : NetworkBehaviour
    {
        public static LotState Instance { get; private set; }

        public readonly NetworkVariable<int> OpenBayMask = new NetworkVariable<int>(1); // Bay1 open at start

        public override void OnNetworkSpawn() => Instance = this;

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsOpen(int bayIndex) => (OpenBayMask.Value & (1 << bayIndex)) != 0;

        public void ServerSetOpen(int bayIndex, bool open)
        {
            if (!IsServer) return;
            int m = OpenBayMask.Value;
            m = open ? m | (1 << bayIndex) : m & ~(1 << bayIndex);
            OpenBayMask.Value = m;
        }

        /// <summary>Server: open the first n bays (U04 level + 1).</summary>
        public void ServerSetOpenCount(int n)
        {
            if (!IsServer) return;
            OpenBayMask.Value = (1 << Mathf.Clamp(n, 0, 31)) - 1;
        }
    }
}
