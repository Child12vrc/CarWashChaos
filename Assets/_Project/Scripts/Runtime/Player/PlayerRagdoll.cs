using System;
using CarWash.Core;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// P3 server-authoritative ragdoll on the YBot body (+ P4 being grabbed/carried/thrown by another player).
    /// Knockdown → the SERVER simulates the bones (hips detached to world space so the owner-driven root can't drag
    /// them) and streams the pose 20×/s unreliable (hips world pos+rot + 10 bone local rotations, 56 bytes); every
    /// client — the owner included — renders the same pose from a 100 ms interpolation buffer. When it ends the body
    /// blends from the fallen pose back into the animator (procedural get-up, 0.9 s) while the root is placed where the
    /// hips landed. Grabbed: spring-pulled to the grabber's hold point (stays down while held); mashing Jump escapes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PlayerRagdoll : NetworkBehaviour
    {
        public enum Mode : byte { Off, Down, GettingUp }

        /// <summary>Bone collider → its ragdoll (on the server the hips are detached from the player while down).</summary>
        static readonly System.Collections.Generic.Dictionary<Collider, PlayerRagdoll> ByCollider = new System.Collections.Generic.Dictionary<Collider, PlayerRagdoll>();
        public static PlayerRagdoll FromCollider(Collider c) => c != null && ByCollider.TryGetValue(c, out var r) && r != null ? r : null;
        public PlayerNet Net => _net;

        // DownTime 3.5 s (was 2.2: teammates had no time to reach and grab a downed player — P4 check)
        public const float DownTime = 3.5f, GetUpTime = 0.9f, SendRate = 20f, InterpDelay = 0.1f;
        public const float RegrabCooldown = 4f, EscapeWindow = 3f; public const int EscapePresses = 6;

        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody[] _bones = Array.Empty<Rigidbody>(); // [0] hips

        public readonly NetworkVariable<Mode> State = new NetworkVariable<Mode>(Mode.Off);
        public readonly NetworkVariable<ulong> HolderClient = new NetworkVariable<ulong>(ulong.MaxValue);

        public bool IsDown => State.Value == Mode.Down;
        public bool ControlsBody => State.Value != Mode.Off;
        public bool IsHeld => HolderClient.Value != ulong.MaxValue;
        public Transform Hips => _bones.Length > 0 ? _bones[0].transform : transform;
        public int SnapshotsSent { get; private set; }
        public int SnapshotsReceived { get; private set; }
        public float GetUpProgress => State.Value == Mode.GettingUp ? Mathf.Clamp01(_gT) : 0f;

        PlayerNet _net;
        Collider[] _cols;
        Transform _hipsParent;
        Vector3 _hipsLocalPos; Quaternion _hipsLocalRot;

        // server
        float _endAt, _sendT, _escapeStart, _regrabReadyAt, _getUpEndAt;
        int _escapes;
        PlayerNet _holder;

        // client interpolation
        struct Snap { public float t; public Vector3 pos; public Quaternion[] rot; }
        Snap _a, _b; int _snaps;

        // get-up blend
        Vector3 _gHips; Quaternion[] _gWorld; float _gT;

        void Awake()
        {
            _net = GetComponent<PlayerNet>();
            _cols = new Collider[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                _cols[i] = _bones[i].GetComponent<Collider>();
                _bones[i].isKinematic = true;
                if (_cols[i]) _cols[i].enabled = false;
            }
            if (_bones.Length > 0) { _hipsParent = Hips.parent; _hipsLocalPos = Hips.localPosition; _hipsLocalRot = Hips.localRotation; }
            _gWorld = new Quaternion[_bones.Length];
            foreach (var c in _cols) if (c != null) ByCollider[c] = this;
        }

        public override void OnNetworkSpawn() => State.OnValueChanged += (_, m) => ApplyMode(m);

        void ApplyMode(Mode m)
        {
            bool down = m == Mode.Down;
            if (_animator) _animator.enabled = m != Mode.Down;
            for (int i = 0; i < _cols.Length; i++) if (_cols[i]) _cols[i].enabled = down;
            if (TryGetComponent<Net.RemoteVisualSmoother>(out var sm)) sm.enabled = m == Mode.Off;
            if (IsOwner && TryGetComponent<CharacterController>(out var cc)) cc.enabled = m == Mode.Off;
            if (IsOwner && m == Mode.Off && _net != null && _net.Status != null) _net.Status.ClearKnock();
            if (m == Mode.Down) _snaps = 0;
        }

        // ------------------------------------------------------------------ knockdown
        /// <summary>Owner (PlayerMotor knockdown): ask the server to ragdoll us with this velocity.</summary>
        public void OwnerKnocked(Vector3 velocity)
        {
            if (IsServer) ServerKnockDown(velocity, DownTime);
            else KnockRpc(velocity);
        }

        [Rpc(SendTo.Server)]
        void KnockRpc(Vector3 velocity, RpcParams p = default)
        {
            if (p.Receive.SenderClientId == OwnerClientId) ServerKnockDown(velocity, DownTime);
        }

        public void ServerKnockDown(Vector3 velocity, float duration)
        {
            if (!IsServer || _bones.Length == 0) return;
            // launch clamp: ≤ 9 m/s sideways, ≤ 6 m/s up (a 12 m/s shove used to slide a body off the lot)
            var flat = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z), 9f);
            velocity = new Vector3(flat.x, Mathf.Clamp(velocity.y, -6f, 6f), flat.z);
            _endAt = Mathf.Max(_endAt, Time.time + duration);
            if (State.Value == Mode.Down) { foreach (var b in _bones) b.linearVelocity += velocity * 0.5f; return; }
            if (State.Value == Mode.GettingUp) ReattachHips();
            Hips.SetParent(null, true);                       // world space: the owner-moved root must not drag the body
            foreach (var b in _bones)
            {
                b.isKinematic = false;
                b.linearVelocity = velocity;
                b.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f;
            }
            State.Value = Mode.Down;
            ApplyMode(Mode.Down);
            foreach (var b in _bones) b.isKinematic = false;  // ApplyMode keeps colliders; server simulates
            Debug.Log($"[RAGDOLL] client {OwnerClientId} down, v={velocity.magnitude:0.0} m/s");
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) ServerTick();
            if (IsOwner && State.Value == Mode.Down && IsHeld && GetComponent<PlayerInputReader>().JumpPressed) EscapeRpc();
        }

        void ServerTick()
        {
            if (State.Value == Mode.Down)
            {
                _sendT += Time.deltaTime;
                if (_sendT >= 1f / SendRate) { _sendT = 0f; SendPose(); }
                if (IsHeld) _endAt = Mathf.Max(_endAt, Time.time + 1f);
                bool settled = _bones[0].linearVelocity.sqrMagnitude < 0.5f;
                if (Time.time >= _endAt && (settled || Time.time > _endAt + 2f) && !IsHeld) ServerEnd();
            }
            else if (State.Value == Mode.GettingUp && Time.time >= _getUpEndAt) State.Value = Mode.Off;
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || State.Value != Mode.Down || !IsHeld) return;
            if (_holder == null) { ServerRelease(Vector3.zero); return; }
            // P4: spring the hips (and chest) to the grabber's hold point — carried in front of them, dragged if they look down
            var g = _holder.GetComponent<PlayerGrabber>();
            var target = g != null ? g.HoldPoint + Vector3.down * 0.3f : _holder.transform.position + _holder.transform.forward;
            for (int i = 0; i < Mathf.Min(2, _bones.Length); i++)
            {
                var rb = _bones[i];
                var acc = (target - rb.position) * 60f - rb.linearVelocity * 10f;
                rb.AddForce(Vector3.ClampMagnitude(acc, 120f), ForceMode.Acceleration);
            }
            if ((target - _bones[0].position).sqrMagnitude > 16f) ServerRelease(Vector3.zero); // snagged / pulled apart
        }

        void SendPose()
        {
            var rots = new uint[RagdollPose.BoneCount];
            rots[0] = RagdollPose.Pack(Hips.rotation);
            for (int i = 1; i < RagdollPose.BoneCount && i < _bones.Length; i++) rots[i] = RagdollPose.Pack(_bones[i].transform.rotation); // world: identical on every peer
            PoseRpc(Hips.position, rots, (float)NetworkManager.ServerTime.Time);
            SnapshotsSent++;
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void PoseRpc(Vector3 hipsPos, uint[] rots, float serverTime)
        {
            var s = new Snap { t = serverTime, pos = hipsPos, rot = new Quaternion[rots.Length] };
            for (int i = 0; i < rots.Length; i++) s.rot[i] = RagdollPose.Unpack(rots[i]);
            if (_snaps > 0 && serverTime <= _b.t) return;          // old / duplicate
            _a = _snaps > 0 ? _b : s; _b = s; _snaps++;
            SnapshotsReceived++;
        }

        void ServerEnd()
        {
            foreach (var b in _bones) { b.linearVelocity = Vector3.zero; b.angularVelocity = Vector3.zero; b.isKinematic = true; }
            // where to stand up: ground under the hips, facing the way the torso points
            var hp = Hips.position;
            var ground = Physics.Raycast(hp + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, Layers.GroundMask, QueryTriggerInteraction.Ignore) ? hit.point : new Vector3(hp.x, 0f, hp.z);
            var fwd = Vector3.ProjectOnPlane(_bones.Length > 1 ? _bones[1].transform.up : Hips.forward, Vector3.up);
            float yaw = fwd.sqrMagnitude > 0.01f ? Quaternion.LookRotation(fwd.normalized).eulerAngles.y : transform.eulerAngles.y;
            var rots = new uint[RagdollPose.BoneCount];
            rots[0] = RagdollPose.Pack(Hips.rotation);
            for (int i = 1; i < RagdollPose.BoneCount && i < _bones.Length; i++) rots[i] = RagdollPose.Pack(_bones[i].transform.rotation); // world: identical on every peer
            _getUpEndAt = Time.time + GetUpTime;
            State.Value = Mode.GettingUp;
            GetUpRpc(hp, rots, ground, yaw);
            Debug.Log($"[RAGDOLL] client {OwnerClientId} getting up at {ground:F1}");
        }

        [Rpc(SendTo.Everyone)]
        void GetUpRpc(Vector3 hipsPos, uint[] rots, Vector3 ground, float yaw)
        {
            // final fallen pose in world space (all peers end from the same pose)
            ReattachHips();
            var hipsRot = RagdollPose.Unpack(rots[0]);
            var localRots = new Quaternion[_bones.Length];
            for (int i = 1; i < _bones.Length && i < rots.Length; i++) localRots[i] = RagdollPose.Unpack(rots[i]);
            if (IsOwner)
            {
                if (TryGetComponent<CharacterController>(out var cc)) cc.enabled = false;
                transform.SetPositionAndRotation(ground + Vector3.up * 0.02f, Quaternion.Euler(0f, yaw, 0f));
                _net.Look.SetYaw(yaw);
            }
            // apply the fallen pose, record world rotations for the blend
            Hips.SetPositionAndRotation(hipsPos, hipsRot);
            for (int i = 1; i < _bones.Length; i++) _bones[i].transform.rotation = localRots[i];
            _gHips = hipsPos;
            for (int i = 0; i < _bones.Length; i++) _gWorld[i] = _bones[i].transform.rotation;
            _gT = 0f;
            ApplyMode(Mode.GettingUp);
        }

        void ReattachHips()
        {
            if (_bones.Length == 0 || Hips.parent == _hipsParent) return;
            Hips.SetParent(_hipsParent, true);
        }

        // ------------------------------------------------------------------ P4: grabbed by another player
        public bool CanBeGrabbedBy(PlayerNet p) => IsDown && !IsHeld && p != null && p != _net && Time.time >= _regrabReadyAt;

        public bool ServerTryGrab(PlayerNet grabber)
        {
            if (!IsServer || !CanBeGrabbedBy(grabber)) return false;
            _holder = grabber; HolderClient.Value = grabber.OwnerClientId; _escapes = 0; _escapeStart = Time.time;
            Debug.Log($"[RAGDOLL] client {OwnerClientId} grabbed by {grabber.OwnerClientId}");
            return true;
        }

        public void ServerRelease(Vector3 velocity)
        {
            if (!IsServer || !IsHeld) return;
            _holder = null; HolderClient.Value = ulong.MaxValue;
            foreach (var b in _bones) b.linearVelocity += velocity;
            Debug.Log($"[RAGDOLL] client {OwnerClientId} released with {velocity.magnitude:0.0} m/s → hips v {_bones[0].linearVelocity.magnitude:0.0} m/s kinematic={_bones[0].isKinematic}");
            _endAt = Time.time + (velocity.sqrMagnitude > 1f ? 1.8f : 1.0f);
            _regrabReadyAt = Time.time + RegrabCooldown;
        }

        [Rpc(SendTo.Server)]
        void EscapeRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId || !IsHeld) return;
            if (Time.time - _escapeStart > EscapeWindow) { _escapeStart = Time.time; _escapes = 0; }
            if (++_escapes < EscapePresses) return;
            var away = _holder != null ? (Hips.position - _holder.transform.position).normalized : Vector3.zero;
            Debug.Log($"[RAGDOLL] client {OwnerClientId} wriggled free");
            ServerRelease(away * 3f + Vector3.up * 2f);
            _endAt = Time.time; // stand up as soon as it settles
        }

        public int EscapeCount => _escapes;

        // ------------------------------------------------------------------ pose on every peer
        void LateUpdate()
        {
            if (!IsSpawned || _bones.Length == 0) return;
            var m = State.Value;
            if (m == Mode.Down && !IsServer && _snaps > 0)
            {
                float rt = (float)NetworkManager.ServerTime.Time - InterpDelay;
                float span = Mathf.Max(1e-4f, _b.t - _a.t);
                float k = _snaps > 1 ? Mathf.Clamp01((rt - _a.t) / span) : 1f;
                Hips.SetPositionAndRotation(Vector3.Lerp(_a.pos, _b.pos, k), Quaternion.Slerp(_a.rot[0], _b.rot[0], k));
                for (int i = 1; i < _bones.Length && i < _b.rot.Length; i++)
                    _bones[i].transform.rotation = Quaternion.Slerp(_a.rot[i], _b.rot[i], k);
            }
            if (m == Mode.Down && IsOwner)
            {
                // the (owner-authoritative) root follows the body so the camera, name tags and late joiners see the right spot
                var hp = Hips.position;
                var pos = new Vector3(hp.x, transform.position.y, hp.z);
                if (Physics.Raycast(hp + Vector3.up * 0.3f, Vector3.down, out var hit, 3f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
                var world = Hips.position; var wr = Hips.rotation;
                transform.position = pos;
                Hips.SetPositionAndRotation(world, wr); // moving the root must not move the drawn body
            }
            if (m == Mode.GettingUp || (_gT > 0f && _gT < 1f))
            {
                // animator has just written this frame's pose → blend from the fallen pose towards it
                _gT = Mathf.Min(1f, _gT + Time.deltaTime / GetUpTime);
                float e = _gT * _gT * (3f - 2f * _gT);
                var animHips = Hips.position;
                Hips.position = Vector3.Lerp(_gHips, animHips, e);
                for (int i = 0; i < _bones.Length; i++) _bones[i].transform.rotation = Quaternion.Slerp(_gWorld[i], _bones[i].transform.rotation, e);
                if (_gT >= 1f && IsOwner && TryGetComponent<CharacterController>(out var cc) && State.Value != Mode.Down) cc.enabled = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_bones.Length > 0 && Hips != null && Hips.parent != _hipsParent && _hipsParent != null) Hips.SetParent(_hipsParent, true);
        }
    }
}
