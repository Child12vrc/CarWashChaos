using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// Foam on the floor (TECH S03: "static floor → foam decal"): a FoamArea trigger volume + flat visual that lasts
    /// foamDuration seconds. Server-spawned; at most MaxActive exist (oldest removed). Players inside slide (PlayerMotor).
    /// </summary>
    public class FoamPatch : NetworkBehaviour
    {
        public const int MaxActive = 64;
        static readonly List<FoamPatch> Active = new List<FoamPatch>();

        [SerializeField] private Transform _visual;
        [SerializeField] private UnityEngine.Rendering.Universal.DecalProjector _decal;
        public readonly NetworkVariable<float> ExpireAt = new NetworkVariable<float>();
        public readonly NetworkVariable<float> Lifetime = new NetworkVariable<float>(20f);

        public override void OnNetworkSpawn() => Active.Add(this);
        public override void OnNetworkDespawn() => Active.Remove(this);

        double Now => NetworkManager.ServerTime.Time;

        void Update()
        {
            if (!IsSpawned) return;
            float left = ExpireAt.Value - (float)Now;
            // shrink during the last 3 s
            float s = Mathf.Clamp01(left / 3f);
            if (_decal) _decal.fadeFactor = s;
            else if (_visual) _visual.localScale = new Vector3(s, 1f, s);
            if (IsServer && left <= 0f) NetworkObject.Despawn(true);
        }

        /// <summary>Server: true if a patch already covers this point (avoid stacking); refreshes its lifetime.</summary>
        public static bool TryRefreshNear(Vector3 point, float radius)
        {
            foreach (var p in Active)
            {
                if (p == null || !p.IsSpawned) continue;
                var d = p.transform.position - point; d.y = 0f;
                if (d.sqrMagnitude < radius * radius)
                {
                    if (p.IsServer) p.ExpireAt.Value = (float)p.Now + p.Lifetime.Value;
                    return true;
                }
            }
            return false;
        }

        public static void ServerSpawn(FoamPatch prefab, Vector3 point, Vector3 normal, float lifetime)
        {
            if (Active.Count >= MaxActive && Active[0] != null && Active[0].IsSpawned) Active[0].NetworkObject.Despawn(true);
            var p = Instantiate(prefab, point + normal * 0.01f, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            p.Lifetime.Value = lifetime;
            p.ExpireAt.Value = (float)NetworkManager.Singleton.ServerTime.Time + lifetime;
            p.NetworkObject.Spawn(true);
        }
    }
}
