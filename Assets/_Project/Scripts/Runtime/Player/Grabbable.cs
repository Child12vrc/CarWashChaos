using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// Physics object a player can carry (props, tools, critters). Server-authoritative:
    /// the server drives the held Rigidbody toward the holder's hold point (velocity = Δ * stiffness).
    /// Every peer ignores collisions between the object and its holder.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : NetworkBehaviour
    {
        public const ulong NoHolder = ulong.MaxValue;

        [SerializeField] private string _displayName = "물체";
        [SerializeField] private float _maxHoldSpeed = 15f;

        public readonly NetworkVariable<ulong> Holder = new NetworkVariable<ulong>(NoHolder);
        public Rigidbody Body { get; private set; }
        public string DisplayName => _displayName;
        public float Mass => Body.mass;
        public bool IsHeld => Holder.Value != NoHolder;

        Collider[] _colliders;
        PlayerGrabber _holderGrabber;
        CharacterController _ignoredCc;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            Holder.OnValueChanged += OnHolderChanged;
            OnHolderChanged(NoHolder, Holder.Value);
        }

        public override void OnNetworkDespawn() => Holder.OnValueChanged -= OnHolderChanged;

        void OnHolderChanged(ulong _, ulong now)
        {
            SetIgnore(_ignoredCc, false);
            _ignoredCc = null;
            _holderGrabber = null;
            if (now == NoHolder) return;
            foreach (var g in FindObjectsByType<PlayerGrabber>(FindObjectsSortMode.None))
                if (g.OwnerClientId == now) { _holderGrabber = g; break; }
            if (_holderGrabber == null) return;
            _ignoredCc = _holderGrabber.GetComponent<CharacterController>();
            SetIgnore(_ignoredCc, true);
        }

        void SetIgnore(CharacterController cc, bool ignore)
        {
            if (cc == null) return;
            foreach (var c in _colliders) if (c) Physics.IgnoreCollision(c, cc, ignore);
        }

        // ---------------- server ----------------
        public bool ServerTryHold(ulong clientId)
        {
            if (!IsServer || IsHeld) return false;
            Holder.Value = clientId;
            Body.useGravity = false;
            Body.angularDamping = 5f;
            return true;
        }

        public void ServerRelease(Vector3 velocity)
        {
            if (!IsServer || !IsHeld) return;
            Holder.Value = NoHolder;
            Body.useGravity = true;
            Body.angularDamping = 0.05f;
            Body.linearVelocity = velocity;
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsHeld) return;
            if (_holderGrabber == null) { ServerRelease(Vector3.zero); return; }
            Vector3 target = _holderGrabber.HoldPoint;
            Vector3 v = (target - Body.position) * _holderGrabber.HoldStiffness;
            Body.linearVelocity = Vector3.ClampMagnitude(v, _maxHoldSpeed);
            if ((target - Body.position).sqrMagnitude > 9f) ServerRelease(Vector3.zero); // snagged / pulled away
        }
    }
}
