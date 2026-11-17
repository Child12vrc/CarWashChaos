using CarWash.Data;
using CarWash.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// World tool (TECH S03). Lying in the world = physics prop; held = kinematic, every peer poses it in front of the
    /// holder's head (owner: view-model offset, others: chest). Use intents come from PlayerTools via ServerUse.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class ToolBase : NetworkBehaviour, IInteractable
    {
        public const ulong NoHolder = ulong.MaxValue;

        [SerializeField] protected DataRegistry _data;
        [SerializeField] private string _toolId = "T01";
        [SerializeField] private Vector3 _viewOffset = new Vector3(0.28f, -0.25f, 0.55f);
        [SerializeField] private Vector3 _thirdPersonOffset = new Vector3(0.25f, -0.45f, 0.45f);
        [SerializeField] protected Transform _nozzle;

        public readonly NetworkVariable<ulong> Holder = new NetworkVariable<ulong>(NoHolder);
        public readonly NetworkVariable<bool> Using = new NetworkVariable<bool>();

        public ToolData Data => _data != null ? _data.Get<ToolData>(_toolId) : null;
        public string ToolId => _toolId;
        public bool IsHeld => Holder.Value != NoHolder;
        public PlayerNet HolderPlayer { get; private set; }
        public Transform Nozzle => _nozzle != null ? _nozzle : transform;

        protected Rigidbody _rb;
        NetworkTransform _nt;
        Collider[] _colliders;

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _nt = GetComponent<NetworkTransform>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            Holder.OnValueChanged += (_, v) => OnHolderChanged(v);
            OnHolderChanged(Holder.Value);
            Stolen.OnValueChanged += (_, v) => OnStolenChanged(v);
            if (Stolen.Value) OnStolenChanged(true);
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientLeft;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
        }

        void OnClientLeft(ulong clientId)
        {
            if (Holder.Value == clientId) ServerDropAt(transform.position, Vector3.zero);
        }

        void OnHolderChanged(ulong holder)
        {
            HolderPlayer = null;
            if (holder != NoHolder)
                foreach (var p in FindObjectsByType<PlayerNet>(FindObjectsSortMode.None))
                    if (p.OwnerClientId == holder) { HolderPlayer = p; break; }
            bool held = HolderPlayer != null;
            if (_nt) _nt.enabled = !held;
            _rb.isKinematic = held || !IsServer; // non-authority copies are kinematic (NetworkRigidbody convention)
            foreach (var c in _colliders) c.enabled = !held;
            if (!held) OnUseVisual(false);
        }

        // ---------------- stolen by a critter (raccoon, T3.2) ----------------
        /// <summary>Carried off by a critter: no physics, no pick-up; the critter moves it (server) and NetworkTransform syncs it.</summary>
        public readonly NetworkVariable<bool> Stolen = new NetworkVariable<bool>();

        void OnStolenChanged(bool stolen)
        {
            if (HolderPlayer != null) return;
            foreach (var c in _colliders) c.enabled = !stolen;
            if (IsServer) _rb.isKinematic = stolen;
        }

        public bool ServerSteal()
        {
            if (!IsServer || IsHeld || Stolen.Value) return false;
            Stolen.Value = true;
            OnStolenChanged(true);
            return true;
        }

        public void ServerUnsteal(Vector3 pos)
        {
            if (!IsServer || !Stolen.Value) return;
            Stolen.Value = false;
            OnStolenChanged(false);
            ServerDropAt(pos, Vector3.up * 1.5f);
        }

        // ---------------- IInteractable (E = pick up) ----------------
        public string Prompt => $"집어 들기: {(Data != null ? Data.displayName : _toolId)}";
        public bool CanInteract(PlayerNet p) => !IsHeld && !Stolen.Value;
        public void Interact(PlayerNet p) => p.GetComponent<PlayerTools>()?.ServerEquip(this);

        // ---------------- server ----------------
        public bool ServerSetHolder(ulong clientId)
        {
            if (!IsServer) return false;
            if (clientId != NoHolder && IsHeld) return false;
            Holder.Value = clientId;
            if (clientId == NoHolder) Using.Value = false;
            return true;
        }

        public void ServerDropAt(Vector3 pos, Vector3 velocity)
        {
            if (!IsServer) return;
            ServerSetHolder(NoHolder);
            transform.position = pos;
            _rb.isKinematic = false;
            _rb.linearVelocity = velocity;
        }

        /// <summary>
        /// P1 lag compensation: how far behind the holder sees server-driven objects — ½ RTT + the interpolation buffer
        /// (tick latency + extra buffer ticks). 0 for the host's own player.
        /// </summary>
        protected float ViewLagSeconds()
        {
            var nm = NetworkManager;
            if (HolderPlayer == null || HolderPlayer.IsOwner || nm == null) return 0f;
            ulong rttMs = 0;
            try { rttMs = nm.NetworkConfig.NetworkTransport.GetCurrentRtt(HolderPlayer.OwnerClientId); } catch { }
            float tick = 1f / Mathf.Max(1, nm.NetworkConfig.TickRate);
            return Mathf.Min(1f, rttMs * 0.0005f + (Net.NetSession.InterpolationBufferTicks + 1) * tick);
        }

        /// <summary>Owner-side gate (e.g. towel requires a wiping motion).</summary>
        public virtual bool OwnerWantsUse(PlayerInputReader input) => true;

        /// <summary>Server tick while the holder presses Use (dt = tick length).</summary>
        public abstract void ServerUse(Vector3 origin, Vector3 dir, float dt);

        /// <summary>Owner, non-host (P1): local cosmetic prediction of the same use (sprayers paint dirt locally).</summary>
        public virtual void OwnerPredict(Vector3 origin, Vector3 dir, float dt) { }

        /// <summary>All peers: start/stop local spray VFX / audio.</summary>
        protected virtual void OnUseVisual(bool on) { }

        // ---------------- stow (in inventory but not the active slot) ----------------
        public bool Stowed { get; private set; }
        Renderer[] _renderers;

        /// <summary>All peers: hide while stowed in a non-active inventory slot.</summary>
        public void SetStowed(bool stowed)
        {
            Stowed = stowed;
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers) if (r) r.enabled = !stowed;
            if (stowed) OnUseVisual(false);
        }

        // ---------------- pose ----------------
        void LateUpdate()
        {
            if (HolderPlayer == null || Stowed) return;
            var head = HolderPlayer.Look.Head;
            var off = HolderPlayer.IsOwner ? _viewOffset : _thirdPersonOffset;
            transform.SetPositionAndRotation(head.TransformPoint(off), head.rotation);
            OnUseVisual(Using.Value);
        }
    }
}
