using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Interior trash (TECH S04 "Interior trash: Rigidbody props spawned at Socket_Interior, tagged Trash").
    /// DESIGN-GAP (graybox bodies are closed shells): items sit just inside the door opening, hidden and untouchable
    /// while the nearest door is closed. Attached items ride with the car (kinematic); grabbing one detaches it.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class TrashItem : NetworkBehaviour
    {
        public readonly NetworkVariable<NetworkObjectReference> Owner = new NetworkVariable<NetworkObjectReference>();
        public readonly NetworkVariable<Vector3> LocalOffset = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<int> DoorIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> AttachedVar = new NetworkVariable<bool>(true);

        public bool Attached => AttachedVar.Value;
        Grabbable _grab;
        Vehicle _vehicle;
        Renderer[] _renderers;
        Collider[] _colliders;

        void Awake()
        {
            _grab = GetComponent<Grabbable>();
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _grab.Holder.OnValueChanged += (_, h) => { if (h != Grabbable.NoHolder) ServerDetach(); };
        }

        /// <summary>Server, before Spawn.</summary>
        public void ServerAttach(Vehicle v, Vector3 localOffset, int doorIndex)
        {
            Owner.Value = v.NetworkObject;
            LocalOffset.Value = localOffset;
            DoorIndex.Value = doorIndex;
            AttachedVar.Value = true;
        }

        void ServerDetach()
        {
            if (!Attached) return;
            AttachedVar.Value = false;
            _grab.Body.isKinematic = false;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (_vehicle == null && Owner.Value.TryGet(out var no)) _vehicle = no.GetComponent<Vehicle>();
            bool attached = Attached && _vehicle != null;
            bool reachable = !attached || DoorOpen();
            foreach (var r in _renderers) r.enabled = reachable;
            foreach (var c in _colliders) c.enabled = reachable;
            if (!attached) return;
            transform.SetPositionAndRotation(_vehicle.transform.TransformPoint(LocalOffset.Value), _vehicle.transform.rotation);
            if (IsServer) _grab.Body.isKinematic = true;
        }

        bool DoorOpen()
        {
            var doors = _vehicle.Doors;
            int i = DoorIndex.Value;
            return i >= 0 && i < doors.Count && doors[i].Open.Value;
        }
    }
}
