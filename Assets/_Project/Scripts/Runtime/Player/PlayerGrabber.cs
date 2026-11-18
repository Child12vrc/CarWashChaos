using CarWash.Core;
using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// Grab / charged throw (TECH S02). F press = grab target; while carrying, hold F to charge, release = throw (short tap = drop). G = drop.
    /// Owner sends intents; server validates and moves the object.
    /// </summary>
    public class PlayerGrabber : NetworkBehaviour
    {
        [SerializeField] private DataRegistry _data;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerMotor _motor;

        readonly NetworkVariable<NetworkObjectReference> _held = new NetworkVariable<NetworkObjectReference>();

        public Grabbable Held { get; private set; }
        public float Charge { get; private set; }
        public bool Charging { get; private set; }
        public Grabbable LookTarget { get; private set; }
        public float HoldStiffness => _data.balance.grab.holdStiffness;
        public Vector3 HoldPoint => _look.Head.position + _look.Head.forward * _data.balance.grab.holdDistance;
        BalanceData.GrabTuning T => _data.balance.grab;

        readonly RaycastHit[] _hits = new RaycastHit[8];
        bool _ignoreNextRelease;

        // ---- P4: carrying a downed player (their ragdoll) ----
        readonly NetworkVariable<NetworkObjectReference> _heldPlayer = new NetworkVariable<NetworkObjectReference>();
        public PlayerRagdoll HeldPlayer { get; private set; }
        public PlayerRagdoll LookPlayer { get; private set; }
        bool HoldingAnything => Held != null || HeldPlayer != null || HeldCritter != null;

        // ---- M4.8 F5: critters by hand (F on a stunned one = pick up; hold F + release = throw; G = put down) ----
        public Critters.CritterBase LookCritter { get; private set; }
        /// <summary>The critter this player carries by hand (not in the net), from the synced CarrierClient.</summary>
        public Critters.CritterBase HeldCritter
        {
            get
            {
                foreach (var c in Critters.CritterBase.All)
                    if (c != null && c.IsCarried && c.CarrierClient.Value == OwnerClientId && !c.CarriedInNet) return c;
                return null;
            }
        }

        Critters.CritterBase FindCritter()
        {
            var ray = _look.AimRay;
            int n = Physics.RaycastNonAlloc(ray, _hits, T.grabRange + 0.8f, 1 << Layers.Critter, QueryTriggerInteraction.Collide);
            Critters.CritterBase best = null; float bestD = float.MaxValue;
            var me = GetComponent<PlayerNet>();
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider.GetComponentInParent<Critters.CritterBase>();
                if (c == null || c.IsCarried || !c.CanInteract(me) || _hits[i].distance >= bestD) continue;
                best = c; bestD = _hits[i].distance;
            }
            return best;
        }

        [Rpc(SendTo.Server)]
        void GrabCritterRpc(NetworkObjectReference target)
        {
            if (!target.TryGet(out var no) || HoldingAnything) return;
            var c = no.GetComponent<Critters.CritterBase>();
            if (c == null || Vector3.Distance(_look.Head.position, c.transform.position) > T.grabRange + 1.6f) return;
            c.ServerTryCarry(GetComponent<PlayerNet>(), null);
        }

        [Rpc(SendTo.Server)]
        void ThrowCritterRpc(float charge)
        {
            var c = HeldCritter;
            if (c == null) return;
            // critters are light: about a prop throw, a little up so they arc and roll
            var v = _motor.Velocity + _look.Head.forward * (2f + charge * T.throwImpulse * 0.55f) + Vector3.up * (1.5f + charge * 2.5f);
            c.ServerThrow(v);
            Debug.Log($"[CRITTER] client {OwnerClientId} {(charge > 0f ? "threw" : "dropped")} {c.CritterId} (charge {charge:0.00}, {v.magnitude:0.0} m/s)");
        }

        public override void OnNetworkSpawn()
        {
            _held.OnValueChanged += (_, v) => Held = v.TryGet(out var no) ? no.GetComponent<Grabbable>() : null;
            _heldPlayer.OnValueChanged += (_, v) => HeldPlayer = v.TryGet(out var no) ? no.GetComponent<PlayerRagdoll>() : null;
        }

        void Update()
        {
            if (IsSpawned && IsServer && HeldPlayer != null && !HeldPlayer.IsHeld) _heldPlayer.Value = default; // they escaped / got released
            if (!IsSpawned || !IsOwner) return;
            _motor.Carrying = HoldingAnything;
            LookTarget = !HoldingAnything ? FindTarget() : null;
            LookPlayer = !HoldingAnything && LookTarget == null ? FindPlayer() : null;
            LookCritter = !HoldingAnything && LookTarget == null && LookPlayer == null ? FindCritter() : null;

            if (!HoldingAnything)
            {
                Charging = false; Charge = 0f;
                if (_input.GrabReleased) _ignoreNextRelease = false; // grab rejected / not yet confirmed
                if (_input.GrabPressed && LookTarget != null && LookTarget.Mass <= T.maxMass)
                {
                    GrabRpc(LookTarget.NetworkObject);
                    _ignoreNextRelease = true;
                }
                else if (_input.GrabPressed && LookPlayer != null)
                {
                    GrabPlayerRpc(LookPlayer.NetworkObject);
                    _ignoreNextRelease = true;
                }
                else if (_input.GrabPressed && LookCritter != null)
                {
                    GrabCritterRpc(LookCritter.NetworkObject);
                    _ignoreNextRelease = true;
                }
                return;
            }

            if (HeldCritter != null)
            {
                if (_input.DropPressed) { ThrowCritterRpc(0f); return; }
                if (_input.GrabPressed && !_ignoreNextRelease) Charging = true;
                if (Charging && _input.GrabHeld) Charge = Mathf.Clamp01(Charge + Time.deltaTime / Mathf.Max(0.05f, T.chargeTime));
                if (_input.GrabReleased)
                {
                    if (_ignoreNextRelease) _ignoreNextRelease = false;
                    else if (Charging) { ThrowCritterRpc(Charge < 0.15f ? 0f : Charge); Charging = false; Charge = 0f; }
                }
                return;
            }

            if (HeldPlayer != null)
            {
                if (_input.DropPressed) { ThrowPlayerRpc(0f); return; }
                if (_input.GrabPressed && !_ignoreNextRelease) Charging = true;
                if (Charging && _input.GrabHeld) Charge = Mathf.Clamp01(Charge + Time.deltaTime / Mathf.Max(0.05f, T.chargeTime));
                if (_input.GrabReleased)
                {
                    if (_ignoreNextRelease) _ignoreNextRelease = false;
                    else if (Charging) { ThrowPlayerRpc(Charge < 0.15f ? 0f : Charge); Charging = false; Charge = 0f; }
                }
                return;
            }

            if (_input.DropPressed) { ThrowRpc(0f); return; }
            if (_input.GrabPressed && !_ignoreNextRelease) Charging = true;
            if (Charging && _input.GrabHeld) Charge = Mathf.Clamp01(Charge + Time.deltaTime / Mathf.Max(0.05f, T.chargeTime));
            if (_input.GrabReleased)
            {
                if (_ignoreNextRelease) _ignoreNextRelease = false;
                else if (Charging) { ThrowRpc(Charge < 0.15f ? 0f : Charge); Charging = false; Charge = 0f; }
            }
        }

        Grabbable FindTarget()
        {
            var ray = _look.AimRay;
            int n = Physics.RaycastNonAlloc(ray, _hits, T.grabRange + 0.5f, Layers.InteractMask | (1 << Layers.Prop), QueryTriggerInteraction.Ignore);
            Grabbable best = null; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var g = _hits[i].collider.GetComponentInParent<Grabbable>();
                if (g == null || g.IsHeld || _hits[i].distance >= bestD) continue;
                best = g; bestD = _hits[i].distance;
            }
            return best;
        }

        PlayerRagdoll FindPlayer()
        {
            var ray = _look.AimRay;
            int n = Physics.RaycastNonAlloc(ray, _hits, T.grabRange + 0.5f, 1 << Layers.Ragdoll, QueryTriggerInteraction.Ignore);
            PlayerRagdoll best = null; float bestD = float.MaxValue;
            var me = GetComponent<PlayerNet>();
            for (int i = 0; i < n; i++)
            {
                var r = PlayerRagdoll.FromCollider(_hits[i].collider);
                if (r == null || !r.CanBeGrabbedBy(me) || _hits[i].distance >= bestD) continue;
                best = r; bestD = _hits[i].distance;
            }
            return best;
        }

        static string NameOf(PlayerRagdoll r)
        {
            var n = r != null ? r.Net : null;
            return n != null ? n.DisplayName : "동료";
        }

        public string PromptText()
        {
            if (HeldPlayer != null) return Charging ? $"던지기 충전 {Mathf.RoundToInt(Charge * 100)}%" : $"[F 길게] 던지기   [G] 내려놓기   ({NameOf(HeldPlayer)} 들고 있음)";
            if (LookPlayer != null) return $"[F] 붙잡기  쓰러진 {NameOf(LookPlayer)}";
            var hc = HeldCritter;
            if (hc != null) return Charging ? $"던지기 충전 {Mathf.RoundToInt(Charge * 100)}%" : $"[F 길게] 던지기   [G] 내려놓기   ({hc.DisplayName} 들고 있음 — 보호소 우리로!)";
            if (LookCritter != null) return $"[F] 잡기  {LookCritter.DisplayName}";
            if (Held != null) return Charging ? $"던지기 충전 {Mathf.RoundToInt(Charge * 100)}%" : $"[F 길게] 던지기   [G] 내려놓기   ({Held.DisplayName})";
            if (LookTarget == null) return null;
            return LookTarget.Mass > T.maxMass ? $"{LookTarget.DisplayName} — 너무 무거움 ({LookTarget.Mass:0}kg)" : $"[F] 집기  {LookTarget.DisplayName} ({LookTarget.Mass:0}kg)";
        }

        // ---------------- server ----------------
        [Rpc(SendTo.Server)]
        void GrabRpc(NetworkObjectReference target)
        {
            if (!target.TryGet(out var no) || Held != null) return;
            var g = no.GetComponent<Grabbable>();
            if (g == null || g.Mass > T.maxMass) return;
            if (Vector3.Distance(_look.Head.position, g.Body.ClosestPointOnBounds(_look.Head.position)) > T.grabRange + 0.75f) return;
            if (g.ServerTryHold(OwnerClientId)) _held.Value = no;
        }

        [Rpc(SendTo.Server)]
        void ThrowRpc(float charge) => ServerDropHeld(charge);

        [Rpc(SendTo.Server)]
        void GrabPlayerRpc(NetworkObjectReference target)
        {
            if (!target.TryGet(out var no) || HoldingAnything) return;
            var r = no.GetComponent<PlayerRagdoll>();
            if (r == null || Vector3.Distance(_look.Head.position, r.Hips.position) > T.grabRange + 1.5f) return;
            if (r.ServerTryGrab(GetComponent<PlayerNet>())) _heldPlayer.Value = no;
        }

        [Rpc(SendTo.Server)]
        void ThrowPlayerRpc(float charge) => ServerDropPlayer(charge);

        /// <summary>Server: let go of a carried player; charge 1 = full throw (people are heavy: 60% of a prop throw).</summary>
        public void ServerDropPlayer(float charge)
        {
            if (HeldPlayer == null) return;
            var v = _motor.Velocity + _look.Head.forward * (charge * T.throwImpulse * 0.6f) + Vector3.up * (charge * 3f);
            HeldPlayer.ServerRelease(v);
            Debug.Log($"[RAGDOLL] client {OwnerClientId} {(charge > 0f ? "threw" : "dropped")} client {HeldPlayer.OwnerClientId} (charge {charge:0.00})");
            _heldPlayer.Value = default;
        }

        /// <summary>Server: release whatever we hold. charge 0 = drop, 1 = full throw.</summary>
        public void ServerDropHeld(float charge)
        {
            if (Held == null) return;
            var g = Held;
            Vector3 v = PropThrowVelocity(_motor.Velocity, _look.Head.forward, charge, T.throwImpulse, g.Mass);
            g.ServerRelease(v);
            _held.Value = default;
        }

        /// <summary>M4.6 B2: props are thrown twice as hard as before (players thrown while downed keep the old force).</summary>
        public const float PropThrowScale = 2f;

        /// <summary>DESIGN-GAP: "throw impulse = charge*14" applied as a velocity change scaled by mass (light props fly far, crates
        /// less), ×PropThrowScale since M4.6.</summary>
        public static Vector3 PropThrowVelocity(Vector3 holderVelocity, Vector3 forward, float charge, float impulse, float mass)
        {
            float massScale = Mathf.Clamp(8f / Mathf.Max(0.1f, mass), 0.3f, 1f);
            return holderVelocity + forward * (charge * impulse * PropThrowScale * massScale);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) { ServerDropHeld(0f); ServerDropPlayer(0f); }
        }
    }
}
