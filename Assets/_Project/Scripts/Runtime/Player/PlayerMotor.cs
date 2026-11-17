using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// First-person CharacterController motor (TECH S02). Simulated by the OWNER only; position replicated by OwnerNetworkTransform.
    /// Foam = low friction + weak control (slide). Knockback = external velocity, |v| &gt; knockdownSpeed → Knocked.
    /// Anyone (usually the server) calls AddKnockback; it is forwarded to the owner.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        [SerializeField] private DataRegistry _data;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerStatus _status;
        [SerializeField] private float _standHeight = 1.8f, _crouchHeight = 1.2f;
        [SerializeField] private float _foamControl = 0.25f;

        CharacterController _cc;
        Vector3 _vel, _ext;
        int _foamCount;
        Vector3 _spawnPos;

        public Vector3 Velocity => _vel + _ext;
        public bool Grounded { get; private set; }
        public bool Crouching { get; private set; }
        public bool InFoam => _foamCount > 0;
        public float FoamSlipReduction { get; set; } // U09 (0..1)
        public bool Carrying { get; set; }
        public float EyeHeight => Crouching ? 1.1f : 1.65f;
        BalanceData.PlayerTuning T => _data.balance.player;

        PlayerRagdoll _ragdoll;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _ragdoll = GetComponent<PlayerRagdoll>();
            if (!_input) _input = GetComponent<PlayerInputReader>();
            if (!_status) _status = GetComponent<PlayerStatus>();
        }

        public override void OnNetworkSpawn()
        {
            _spawnPos = transform.position;
            // Non-owners never simulate; disabling the controller avoids it fighting the network transform.
            _cc.enabled = IsOwner;
        }

        public void SetSpawn(Vector3 pos) => _spawnPos = pos;

        public void Teleport(Vector3 pos)
        {
            bool was = _cc.enabled;
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = was;
            _vel = _ext = Vector3.zero;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (_ragdoll != null && _ragdoll.ControlsBody)
            {
                // P3: the ragdoll owns the body — but status timers must keep running (they froze → stuck 1.5 s after getting up)
                _vel = _ext = Vector3.zero;
                _status.OwnerTick(Time.deltaTime, false, false, Carrying);
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var t = T;

            UpdateFoam();
            bool knocked = _status.IsKnocked;
            Crouching = !knocked && _input.Crouch;
            SetHeight(Crouching ? _crouchHeight : _standHeight);

            // --- horizontal ---
            Vector3 h = new Vector3(_vel.x, 0f, _vel.z);
            Vector3 wish = knocked ? Vector3.zero : transform.TransformDirection(new Vector3(_input.Move.x, 0f, _input.Move.y));
            float wishSpeed = (Crouching ? t.crouchSpeed : (_input.Sprint && !Carrying ? t.sprintSpeed : t.walkSpeed)) * _status.SpeedMultiplier(t) * wish.magnitude;
            if (wish.sqrMagnitude > 0.0001f) wish.Normalize();

            if (Grounded)
            {
                float slip = InFoam ? 1f - FoamSlipReduction : 0f;
                float friction = Mathf.Lerp(t.friction, t.foamFriction, slip);
                float accel = t.accel * Mathf.Lerp(1f, _foamControl, slip);
                h = MotorMath.ApplyFriction(h, friction, dt);
                if (wishSpeed > 0f) h = MotorMath.Accelerate(h, wish, wishSpeed, accel, dt);
                if (_input.JumpPressed && !knocked) _vel.y = t.jumpSpeed;
            }
            else if (wishSpeed > 0f)
            {
                h = MotorMath.Accelerate(h, wish, wishSpeed, t.airAccel, dt);
            }

            // --- vertical ---
            if (Grounded && _vel.y < 0f) _vel.y = -2f;
            _vel.y -= t.gravity * dt;
            _vel = new Vector3(h.x, _vel.y, h.z);

            // --- knockback ---
            _ext = MotorMath.DecayKnockback(_ext, t.knockbackDecay, dt);

            if (_tethered)
            {
                var hv = ApplyTether(new Vector3(_vel.x, 0f, _vel.z), dt);
                _vel = new Vector3(hv.x, _vel.y, hv.z);
                _tethered = false; // re-armed every frame by the held tool
            }
            var flags = _cc.Move((_vel + _ext) * dt);
            Grounded = (flags & CollisionFlags.Below) != 0 || _cc.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && _vel.y > 0f) _vel.y = 0f;
            if (Grounded && _ext.y < 0f) _ext.y = 0f;

            if (transform.position.y < t.respawnY) Teleport(_spawnPos);
            _status.OwnerTick(dt, Crouching, InFoam, Carrying);
        }

        void SetHeight(float h)
        {
            if (Mathf.Approximately(_cc.height, h)) return;
            if (h > _cc.height && Physics.SphereCast(transform.position + Vector3.up * (_cc.height - _cc.radius), _cc.radius * 0.9f,
                    Vector3.up, out _, h - _cc.height, ~(1 << Core.Layers.Player), QueryTriggerInteraction.Ignore))
            { Crouching = true; return; } // ceiling: stay crouched
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
        }

        // ---------------------------------------------------------------- hose tether (owner)
        Vector3 _tetherAnchor; float _tetherLength, _tetherPull; Object _tetherOwner; bool _tethered;

        public void SetTether(Vector3 anchor, float length, float pull)
        {
            _tetherAnchor = anchor; _tetherLength = length; _tetherPull = pull; _tethered = true;
        }

        public void ClearTether(Object owner) => _tethered = false;

        /// <summary>Beyond the hose length: cancel outward velocity and spring back toward the reel.</summary>
        Vector3 ApplyTether(Vector3 vel, float dt)
        {
            if (!_tethered) return vel;
            Vector3 d = transform.position - _tetherAnchor; d.y = 0f;
            float excess = d.magnitude - _tetherLength;
            if (excess <= 0f) return vel;
            Vector3 outward = d.normalized;
            float outSpeed = Vector3.Dot(vel, outward);
            if (outSpeed > 0f) vel -= outward * outSpeed;
            vel -= outward * (_tetherPull * excess * dt * 10f);
            return vel;
        }

        // ---------------------------------------------------------------- knockback
        /// <summary>Adds external velocity. Safe to call on any peer; non-owners forward it to the owner.</summary>
        public void AddKnockback(Vector3 v)
        {
            if (IsOwner) ApplyKnockback(v);
            else if (IsServer) KnockbackRpc(v);
        }

        [Rpc(SendTo.Owner)]
        void KnockbackRpc(Vector3 v) => ApplyKnockback(v);

        void ApplyKnockback(Vector3 v)
        {
            _ext += v;
            if (_ext.magnitude > T.knockdownSpeed)
            {
                _status.Knock(T.knockedTime);
                if (_ragdoll != null) _ragdoll.OwnerKnocked(_ext); // P3: fall over as a ragdoll (server simulates)
                Core.GameEvents.RaisePlayerKnocked(OwnerClientId);
            }
        }

        // ---------------------------------------------------------------- foam volumes
        // Overlap query instead of enter/exit counting: teleports and despawning foam never leave a stale count.
        static readonly Collider[] FoamHits = new Collider[4];

        void UpdateFoam()
        {
            Vector3 p = transform.position;
            _foamCount = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.2f, p + Vector3.up * 0.6f, _cc.radius,
                FoamHits, 1 << Core.Layers.FoamArea, QueryTriggerInteraction.Collide);
        }
    }
}
