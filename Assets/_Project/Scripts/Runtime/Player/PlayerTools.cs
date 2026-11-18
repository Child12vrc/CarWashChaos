using CarWash.Tools;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// Use intents for the active inventory tool (TECH S01/S03). Owner sends UseRpc(origin, dir) at ~30 Hz while LMB is held;
    /// the server runs the tool's hit logic. RMB = nozzle mode, 1-4 = slot, G = drop (when not carrying a prop).
    /// </summary>
    public class PlayerTools : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerGrabber _grabber;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private float _sendRate = 30f;

        public ToolBase Equipped => _inventory != null ? _inventory.Active : null;
        public PlayerInventory Inventory => _inventory;
        float _sendT;
        bool _wasUsing;

        public override void OnNetworkSpawn()
        {
            if (IsOwner && _look != null && _look.Camera != null) _look.Camera.gameObject.AddComponent<FPHands>().Init(this); // M4.6 D8
        }

        void Update()
        {
            if (IsSpawned && IsServer) ServerUseWatchdog();
            if (!IsSpawned || !IsOwner) return;
            var tool = Equipped;
            bool handsFree = _grabber == null || _grabber.Held == null;
            bool using_ = tool != null && handsFree && _input.UseHeld && tool.OwnerWantsUse(_input);
            if (using_)
            {
                _sendT += Time.deltaTime;
                if (!_wasUsing) _burst++; // M4.7: a new spray burst (late unreliable packets of an older one are ignored)
                if (_sendT >= 1f / _sendRate || !_wasUsing)
                {
                    var ray = _look.AimRay;
                    float useDt = Mathf.Min(_sendT, 0.1f);
                    UseRpc(ray.origin, ray.direction, useDt, _burst);
                    if (!IsServer) tool.OwnerPredict(ray.origin, ray.direction.normalized, Mathf.Clamp(useDt, 0.005f, 0.1f)); // P1: instant local paint
                    _sendT = 0f;
                }
            }
            else if (_wasUsing) StopRpc(_burst);
            _wasUsing = using_;

            if (_input.SlotPressed >= 0) SelectRpc(_input.SlotPressed);
            if (tool != null && handsFree && _input.AltPressed) AltRpc();
            if (tool != null && handsFree && _input.DropPressed) DropRpc();
            // M4.6 D8: [Q] swing the held tool (server checks the cooldown and what it hits; everyone sees the arc)
            if (tool != null && handsFree && _input.SwingPressed && Time.time >= _nextSwing)
            {
                _nextSwing = Time.time + SwingCooldown;
                var ray = _look.AimRay;
                SwingRpc(ray.origin, ray.direction);
            }
        }

        public const float SwingCooldown = 0.8f, SwingRange = 1.9f, SwingRadius = 0.45f;
        float _nextSwing, _serverNextSwing;
        /// <summary>Server, dev checks: what the last swing hit ("player c1", "critter C01", "prop Cone", "nothing").</summary>
        public string LastSwingHit { get; private set; } = "";
        static readonly RaycastHit[] SwingHits = new RaycastHit[16];

        [Rpc(SendTo.Server)]
        void SwingRpc(Vector3 origin, Vector3 dir)
        {
            var tool = Equipped;
            if (tool == null || Time.time < _serverNextSwing) return;
            _serverNextSwing = Time.time + SwingCooldown * 0.9f;
            if ((origin - _look.Head.position).sqrMagnitude > 2.25f) origin = _look.Head.position;
            dir = dir.normalized;
            SwingFxRpc();
            LastSwingHit = ServerSwingHit(origin, dir);
            Debug.Log($"[SWING] client {OwnerClientId} swung {tool.ToolId} → {LastSwingHit}");
        }

        [Rpc(SendTo.Everyone)]
        void SwingFxRpc() { Equipped?.PlaySwing(); }

        /// <summary>Server: first thing in a 0.45 m sweep 1.9 m ahead — a player is knocked down, a critter stunned, a prop knocked away.</summary>
        string ServerSwingHit(Vector3 origin, Vector3 dir)
        {
            int mask = (1 << Core.Layers.Player) | (1 << Core.Layers.Critter) | (1 << Core.Layers.Prop) | (1 << Core.Layers.Ragdoll);
            int n = Physics.SphereCastNonAlloc(origin, SwingRadius, dir, SwingHits, SwingRange, mask, QueryTriggerInteraction.Collide);
            System.Array.Sort(SwingHits, 0, n, Comparer);
            for (int i = 0; i < n; i++)
            {
                var col = SwingHits[i].collider;
                if (col.transform.IsChildOf(transform)) continue;
                var flat = new Vector3(dir.x, 0f, dir.z).normalized;
                var p = col.GetComponentInParent<PlayerNet>();
                if (p != null && p != GetComponent<PlayerNet>()) { p.Motor.AddKnockback(flat * 11f + Vector3.up * 3.5f); return "player c" + p.OwnerClientId; }
                var c = col.GetComponentInParent<Critters.CritterBase>();
                if (c != null) { c.ServerAddWetness(c.MaxWetness.Value * 1.1f); return "critter " + c.CritterId + " → " + c.State.Value; }
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic) { rb.AddForce(flat * 6f + Vector3.up * 2.5f, ForceMode.VelocityChange); return "prop " + rb.name; }
            }
            return $"nothing ({n} hits, from {origin.ToString("F1")} dir {dir.ToString("F2")})";
        }

        static readonly System.Collections.Generic.Comparer<RaycastHit> Comparer = System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        // ---------------- server ----------------
        public void ServerEquip(ToolBase tool) => _inventory.ServerAdd(tool);
        public void ServerDrop() => _inventory.ServerDropSlot(_inventory.ActiveSlot.Value);

        // M4.7 N2: continuous spray input is unreliable — under loss a lost packet must not hold back every reliable message
        // queued behind it (Boss Room guidance). Bursts are numbered so a late packet can't restart a burst that the reliable
        // StopRpc already ended; a server watchdog also stops Using after 0.5 s without input.
        ushort _burst, _stoppedBurst;
        float _lastUse;
        public static bool NewerBurst(ushort a, ushort b) => (short)(a - b) > 0;

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void UseRpc(Vector3 origin, Vector3 dir, float dt, ushort burst)
        {
            var tool = Equipped;
            if (tool == null || !NewerBurst(burst, _stoppedBurst)) return;
            _lastUse = Time.time;
            if ((origin - _look.Head.position).sqrMagnitude > 2.25f) origin = _look.Head.position; // aim from own head
            if (!tool.Using.Value) tool.Using.Value = true;
            tool.ServerUse(origin, dir.normalized, Mathf.Clamp(dt, 0.005f, 0.1f));
        }

        [Rpc(SendTo.Server)]
        void StopRpc(ushort burst)
        {
            if (!NewerBurst(_stoppedBurst, burst)) _stoppedBurst = burst;
            if (Equipped != null) Equipped.Using.Value = false;
        }

        void ServerUseWatchdog()
        {
            var tool = Equipped;
            if (tool != null && tool.Using.Value && Time.time - _lastUse > 0.5f) tool.Using.Value = false;
        }

        [Rpc(SendTo.Server)]
        void AltRpc() { (Equipped as SprayTool)?.ServerToggleNozzle(); }

        [Rpc(SendTo.Server)]
        void DropRpc() => ServerDrop();

        [Rpc(SendTo.Server)]
        void SelectRpc(int slot) => _inventory.ServerSelect(slot);
    }
}
