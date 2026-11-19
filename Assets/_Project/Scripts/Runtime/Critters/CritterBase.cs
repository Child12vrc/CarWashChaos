using System;
using System.Collections.Generic;
using CarWash.Data;
using CarWash.Player;
using CarWash.Tools;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CarWash.Critters
{
    /// <summary>
    /// Networked critter (TECH S07). Server: CritterBrain + NavMeshAgent (server-only) + species behaviour (Behave/OnAct);
    /// clients: State/Wetness NetworkVariables → stun stars, wet tint, reveal pop, HUD bars. Water from SprayTool adds
    /// wetness; a stunned (or no-stun-needed) critter can be grabbed with E (escapes when the stun wears off) or netted
    /// with T06 (secure), then carried to the Animal Control cage → captureBonus.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CritterBase : NetworkBehaviour, IInteractable
    {
        public static readonly List<CritterBase> All = new List<CritterBase>();
        /// <summary>Every peer: a critter popped out (reveal sting) / was put in the cage.</summary>
        public static event Action<CritterBase> Revealed, Caged;

        [SerializeField] protected DataRegistry _data;
        [SerializeField] protected string _critterId;
        [SerializeField] private string _agentType = "Small";
        [SerializeField] private bool _startHidden;
        [SerializeField] private ParticleSystem _stunStars;
        [SerializeField] private Transform _visual;

        public readonly NetworkVariable<CritterState> State = new NetworkVariable<CritterState>(CritterState.Hidden);
        public readonly NetworkVariable<float> Wetness = new NetworkVariable<float>();
        public readonly NetworkVariable<float> MaxWetness = new NetworkVariable<float>(100f);
        public readonly NetworkVariable<ulong> CarrierClient = new NetworkVariable<ulong>(ulong.MaxValue);

        public CritterData Data => _data != null ? _data.Get<CritterData>(_critterId) : null;
        public string CritterId => _critterId;
        public string DisplayName => Data != null ? Data.displayName : _critterId;
        public CritterBrain Brain { get; private set; }
        /// <summary>Spawned by a vehicle surprise (removed at day end) — dev-yard critters are not.</summary>
        public bool FromSurprise { get; set; }
        public Vector3 Home { get; set; }
        /// <summary>Server, before Spawn: HP multiplier (mud slime split children = 0.6).</summary>
        public float HpMultiplier { get; set; } = 1f;
        /// <summary>Server, before Spawn: skip the hidden phase (split children, dev spawns).</summary>
        public bool StartRevealed { get; set; }
        public bool IsCarried => State.Value == CritterState.Carried;

        protected NavMeshAgent Agent { get; private set; }
        protected BalanceData Balance => _data.balance;
        PlayerNet _carrier;
        ToolBase _carrierTool;
        float _idleUntil;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        Color[] _baseColors;
        float _popT = -1f;
        Collider[] _colliders;
        CritterAnimator _animator;
        Vector3 _carryRest;

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Agent.enabled = false;
            _animator = GetComponent<CritterAnimator>();
            if (_animator != null && _animator.CarrySocket != null) _carryRest = transform.InverseTransformPoint(_animator.CarrySocket.position);
            _renderers = (_visual != null ? _visual : transform).GetComponentsInChildren<Renderer>();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = BaseColorOf(_renderers[i].sharedMaterial);
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            State.OnValueChanged += OnStateChanged;
            ApplyVisuals();
            if (!IsServer) return;
            var d = Data;
            int players = NetworkManager.ConnectedClientsIds.Count;
            float hp = (d != null ? d.maxWetness : 100f) * CritterBrain.HpScale(players, Balance.critter.hpScalePerPlayer) * HpMultiplier;
            Brain = new CritterBrain(hp, d != null ? d.stunTime : 6f, Balance.critter.wetnessDecay,
                d == null || d.needsStunToCapture, d == null || d.capturable, _startHidden && !StartRevealed);
            Brain.Changed += (_, s) => { State.Value = s; OnBrainChanged(s); };
            MaxWetness.Value = hp;
            State.Value = Brain.State;
            if (Home == Vector3.zero) Home = transform.position;
            SetupAgent();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            State.OnValueChanged -= OnStateChanged;
        }

        void SetupAgent()
        {
            int n = NavMesh.GetSettingsCount();
            for (int i = 0; i < n; i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == _agentType) { Agent.agentTypeID = s.agentTypeID; break; }
            }
            Agent.speed = Data != null ? Data.moveSpeed : 3f;
            Agent.enabled = true;
            // never warp far below (or above) where it stands — v0.9: next to the B02 trailer the nearest mesh was the PlayBounds
            // catch floor 2 m under the lot (now kept out of the bake too)
            var here = transform.position;
            if (NavMesh.SamplePosition(here, out var hit, 3f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - here.y) < 0.6f) Agent.Warp(hit.position);
            else Agent.enabled = false;
        }

        // ------------------------------------------------------------------ server API
        public virtual void ServerAddWetness(float amount)
        {
            if (!IsServer || Brain == null) return;
            Brain.AddWetness(amount);
            Wetness.Value = Brain.Wetness;
        }

        /// <summary>Hidden → pop out (T3.6 SurpriseTrigger).</summary>
        public void ServerReveal()
        {
            if (IsServer && Brain != null && Brain.Reveal()) RevealRpc();
        }

        [Rpc(SendTo.Everyone)]
        void RevealRpc() { _popT = 0f; Revealed?.Invoke(this); }

        /// <summary>Dev builds only: any client may pop a hidden dev-yard critter (automated checks).</summary>
        public void DevRequestReveal() => DevRevealRpc();

        [Rpc(SendTo.Server)]
        void DevRevealRpc()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (Brain != null && Brain.State == CritterState.Hidden) ServerReveal();
        }

        /// <summary>Server: species hook after every state change.</summary>
        protected virtual void OnStateEntered(CritterState s) { }

        /// <summary>All peers: lie on the side (stunned; the possum also while playing dead).</summary>
        protected virtual bool LooksKnockedOut => State.Value == CritterState.Stunned;

        // ---------------- M4.8 F5 / F6: thrown by hand, flies in code (no Rigidbody), wobbles like jelly ----------------
        /// <summary>Carried in a net (true) or by hand (false) — clients need it for the F-throw prompt.</summary>
        public readonly NetworkVariable<bool> InNet = new NetworkVariable<bool>();
        /// <summary>In flight after a throw (every peer rolls the body while it is set).</summary>
        public readonly NetworkVariable<bool> Thrown = new NetworkVariable<bool>();
        public bool CarriedInNet => InNet.Value;
        Vector3 _flightV;
        bool _flying;
        static readonly int WorldMask = (1 << Core.Layers.Default) | (1 << Core.Layers.Vehicle) | (1 << Core.Layers.PlayBounds);

        /// <summary>Server: let go of a hand-carried critter with a velocity — it flies stunned, bounces, rolls, stays dizzy.</summary>
        public void ServerThrow(Vector3 velocity)
        {
            if (!IsServer || Brain == null || !IsCarried || InNet.Value) return;
            ServerLaunch(velocity);
        }

        /// <summary>Server: smacked or thrown — stunned, flies in code, bounces off walls / the ground, rolls, then lies dizzy.</summary>
        public void ServerLaunch(Vector3 velocity)
        {
            if (!IsServer || Brain == null || Brain.IsGone || State.Value == CritterState.Hidden) return;
            Brain.Stun();
            _flightV = Vector3.ClampMagnitude(velocity, 12f);
            _flying = true; Thrown.Value = true;
            if (Agent.enabled) Agent.enabled = false;
            PunchRpc(velocity * 0.12f);
        }

        void ServerFlight(float dt)
        {
            var p = transform.position;
            _flightV.y -= 9.81f * dt;
            var step = _flightV * dt;
            if (step.sqrMagnitude > 1e-6f && Physics.SphereCast(p + Vector3.up * 0.3f, 0.22f, step.normalized, out var wall, step.magnitude, WorldMask, QueryTriggerInteraction.Ignore) && wall.normal.y < 0.5f)
            {
                _flightV = Vector3.Reflect(_flightV, wall.normal) * 0.45f; // bonk off walls / cars
                PunchRpc(wall.normal * Mathf.Min(4f, step.magnitude / dt * 0.25f));
                step = Vector3.zero;
            }
            p += step;
            float ground = Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out var g, 3f, Core.Layers.GroundMask, QueryTriggerInteraction.Ignore) ? g.point.y : 0f;
            if (p.y <= ground)
            {
                p.y = ground;
                if (_flightV.y < -1.5f) { PunchRpc(Vector3.down * Mathf.Min(5f, -_flightV.y * 0.6f)); _flightV.y = -_flightV.y * 0.35f; _flightV.x *= 0.7f; _flightV.z *= 0.7f; }
                else { var h = new Vector3(_flightV.x, 0f, _flightV.z) * Mathf.Max(0f, 1f - 3.5f * dt); _flightV = h; }
                if (_flightV.sqrMagnitude < 0.16f) { _flying = false; Thrown.Value = false; if (Brain.IsFree && !Agent.enabled) SetupAgent(); }
            }
            transform.position = p;
        }

        /// <summary>Every peer: jiggle the body (hit, throw, bounce) — CritterAnimator's spring.</summary>
        [Rpc(SendTo.Everyone)]
        public void PunchRpc(Vector3 impulse) { if (_animator != null) _animator.Punch(impulse); }

        public bool ServerTryCarry(PlayerNet player, ToolBase net)
        {
            if (!IsServer || Brain == null || player == null || _flying || !Brain.TryCarry(net != null)) return false;
            _carrier = player; _carrierTool = net;
            InNet.Value = net != null;
            CarrierClient.Value = player.OwnerClientId;
            if (Agent.enabled) Agent.enabled = false;
            Debug.Log($"[CRITTER] {_critterId} picked up by client {player.OwnerClientId} ({(net != null ? "net" : "hands")})");
            return true;
        }

        public void ServerRelease()
        {
            if (!IsServer || Brain == null || !IsCarried) return;
            Brain.Release();
        }

        public void ServerCapture()
        {
            ulong by = CarrierClient.Value; // reset when the brain leaves Carried
            if (!IsServer || Brain == null || !Brain.Capture()) return;
            int bonus = Balance.critter.captureBonus;
            Day.DayManager.Instance?.ServerCritterCaged(this, bonus);
            Core.GameEvents.RaiseCritterCaptured(this, by);
            Debug.Log($"[CRITTER] {_critterId} caged by client {by} → +${bonus}");
            CagedRpc();
            Invoke(nameof(DespawnSelf), 0.4f);
        }

        [Rpc(SendTo.Everyone)]
        void CagedRpc() => Caged?.Invoke(this);

        /// <summary>Server: still loose at day end.</summary>
        public void ServerEscape()
        {
            if (!IsServer || Brain == null) return;
            Brain.Escape();
            DespawnSelf();
        }

        void DespawnSelf() { if (IsSpawned) NetworkObject.Despawn(true); }

        void OnBrainChanged(CritterState s)
        {
            if (s != CritterState.Carried) { _carrier = null; _carrierTool = null; CarrierClient.Value = ulong.MaxValue; InNet.Value = false; }
            Wetness.Value = Brain.Wetness;
            bool roam = Brain.IsFree;
            if (roam && !Agent.enabled && !_flying) SetupAgent();
            if (!roam && Agent.enabled && Agent.isOnNavMesh) Agent.ResetPath();
            if (s == CritterState.Flee) FleeFromNearest(6f);
            OnStateEntered(s);
        }

        // ------------------------------------------------------------------ server loop
        // ---------------- P1 lag compensation: where was I N seconds ago (server) ----------------
        readonly Vector3[] _histPos = new Vector3[64];
        readonly float[] _histT = new float[64];
        int _histHead, _histCount;

        void RecordHistory()
        {
            _histHead = (_histHead + 1) % _histPos.Length;
            _histPos[_histHead] = transform.position; _histT[_histHead] = Time.time;
            if (_histCount < _histPos.Length) _histCount++;
        }

        /// <summary>Server: position `secondsAgo` ago (clamped to the ~1 s history).</summary>
        public Vector3 PositionAgo(float secondsAgo)
        {
            if (_histCount == 0 || secondsAgo <= 0f) return transform.position;
            float t = Time.time - secondsAgo;
            for (int k = 0; k < _histCount; k++)
            {
                int i = (_histHead - k + _histPos.Length) % _histPos.Length;
                if (_histT[i] <= t)
                {
                    int j = (i + 1) % _histPos.Length;
                    if (k == 0) return _histPos[i];
                    float span = Mathf.Max(1e-4f, _histT[j] - _histT[i]);
                    return Vector3.Lerp(_histPos[i], _histPos[j], (t - _histT[i]) / span);
                }
            }
            return _histPos[(_histHead - (_histCount - 1) + _histPos.Length) % _histPos.Length];
        }

        protected virtual void Update()
        {
            UpdateVisuals();
            if (!IsSpawned || !IsServer || Brain == null || Brain.IsGone) return;
            RecordHistory();
            float dt = Time.deltaTime;
            Brain.Tick(dt);
            if (Mathf.Abs(Wetness.Value - Brain.Wetness) > 0.5f || (Brain.Wetness == 0f && Wetness.Value != 0f)) Wetness.Value = Brain.Wetness;

            if (_flying) { ServerFlight(dt); return; } // M4.8: thrown — no brain movement until it lands
            if (Brain.State == CritterState.Carried)
            {
                if (_carrier == null || (_carrierTool != null && (_carrierTool.HolderPlayer != _carrier || _carrierTool.Stowed))) { Brain.Release(); return; }
                var t = _carrier.transform;
                float reach = _carrierTool != null ? 2.1f : 1.2f;
                transform.position = t.position + Vector3.up * 1.0f + t.forward * reach;
                transform.rotation = Quaternion.LookRotation(-t.forward);
                // GLB: by hand, Socket_Carry (scruff) sits at the hold point; netted, every peer hangs the visual on the hoop instead
                if (_carrierTool == null && _animator != null && _animator.CarrySocket != null) transform.position -= transform.TransformPoint(_carryRest) - transform.position;
                if (AnimalCage.Instance != null && AnimalCage.Instance.Contains(transform.position)) ServerCapture();
                return;
            }
            if (Brain.IsFree && Agent.enabled && Agent.isOnNavMesh) Behave(dt);
        }

        /// <summary>Species behaviour while free (server). Default: idle/wander near home, flee when getting wet.</summary>
        protected virtual void Behave(float dt)
        {
            var d = Data;
            float speed = d != null ? d.moveSpeed : 3f;
            if (Brain.Wetness > MaxWetness.Value * 0.25f && Brain.State != CritterState.Flee) { Brain.TrySet(CritterState.Flee); return; }
            switch (Brain.State)
            {
                case CritterState.Idle:
                    Agent.speed = speed * 0.5f;
                    if (Time.time >= _idleUntil) { Brain.TrySet(CritterState.Wander); GoNear(Home, 4f); }
                    break;
                case CritterState.Wander:
                    if (Arrived) { Brain.TrySet(CritterState.Idle); _idleUntil = Time.time + UnityEngine.Random.Range(1.5f, 3.5f); }
                    break;
                case CritterState.Flee:
                    Agent.speed = speed;
                    if (Arrived || Brain.StateTime > 3f) { Brain.TrySet(CritterState.Idle); _idleUntil = Time.time + 1f; }
                    break;
                default:
                    OnAct(dt);
                    break;
            }
        }

        /// <summary>Species-specific Chase/Steal/Act (≤150 lines each, TECH S07).</summary>
        protected virtual void OnAct(float dt) { Brain.TrySet(CritterState.Idle); }

        protected bool Arrived => !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;

        protected void GoNear(Vector3 centre, float radius)
        {
            var p = centre + UnityEngine.Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(p, out var hit, 2f, Agent.areaMask)) Agent.SetDestination(hit.position);
        }

        protected PlayerNet NearestPlayer(out float distance)
        {
            PlayerNet best = null; distance = float.MaxValue;
            foreach (var p in FindObjectsByType<PlayerNet>(FindObjectsSortMode.None))
            {
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < distance) { distance = d; best = p; }
            }
            distance = Mathf.Sqrt(distance);
            return best;
        }

        protected void FleeFromNearest(float dist)
        {
            if (!Agent.enabled || !Agent.isOnNavMesh) return;
            var p = NearestPlayer(out _);
            var away = p != null ? (transform.position - p.transform.position) : UnityEngine.Random.insideUnitSphere;
            away.y = 0f;
            var target = transform.position + away.normalized * dist;
            // leash: stay within 8 m of home
            if ((target - Home).magnitude > 8f) target = Home + (target - Home).normalized * 8f;
            if (NavMesh.SamplePosition(target, out var hit, 3f, Agent.areaMask)) Agent.SetDestination(hit.position);
        }

        // ------------------------------------------------------------------ interaction (E)
        public string Prompt
        {
            get
            {
                var me = PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerNet>() : null;
                if (IsCarried && me != null && CarrierClient.Value == me.OwnerClientId) return $"{DisplayName} 놓아주기";
                return $"{DisplayName} 붙잡기";
            }
        }

        public bool CanInteract(PlayerNet player)
        {
            var d = Data;
            if (State.Value == CritterState.Carried) return CarrierClient.Value == player.OwnerClientId;
            bool capturable = d == null || d.capturable;
            bool needsStun = d == null || d.needsStunToCapture;
            bool free = State.Value >= CritterState.Idle && State.Value <= CritterState.Act;
            return capturable && (State.Value == CritterState.Stunned || (!needsStun && free));
        }

        public void Interact(PlayerNet player)
        {
            if (IsCarried && CarrierClient.Value == player.OwnerClientId && _carrierTool == null) { ServerRelease(); return; }
            ServerTryCarry(player, null);
        }

        // ------------------------------------------------------------------ visuals (every peer)
        void OnStateChanged(CritterState from, CritterState to) => ApplyVisuals();

        void ApplyVisuals()
        {
            if (_stunStars != null)
            {
                bool on = State.Value == CritterState.Stunned;
                if (on && !_stunStars.isPlaying) _stunStars.Play();
                else if (!on && _stunStars.isPlaying) _stunStars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_visual != null) _visual.gameObject.SetActive(State.Value != CritterState.Hidden);
            _colliders ??= GetComponentsInChildren<Collider>(true);
            foreach (var c in _colliders) c.enabled = State.Value != CritterState.Hidden; // hidden inside a car: nothing to hit or bump into
        }

        static readonly int GltfBaseColor = Shader.PropertyToID("baseColorFactor");

        static Color BaseColorOf(Material m)
        {
            if (m == null) return Color.white;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty(GltfBaseColor)) return m.GetColor(GltfBaseColor);
            return Color.white;
        }

        void UpdateVisuals()
        {
            if (_visual == null) return;
            // reveal pop 0 → 1.25 → 1 over 0.4 s
            float s = 1f;
            if (_popT >= 0f)
            {
                _popT += Time.deltaTime;
                float k = Mathf.Clamp01(_popT / 0.4f);
                s = k < 0.5f ? Mathf.Lerp(0.2f, 1.25f, k * 2f) : Mathf.Lerp(1.25f, 1f, (k - 0.5f) * 2f);
                if (k >= 1f) _popT = -1f;
            }
            _visual.localScale = Vector3.one * s;
            // stunned: tipped over; wet: darker blue-ish tint
            var stunned = LooksKnockedOut;
            if (_animator == null) _visual.localRotation = Quaternion.Slerp(_visual.localRotation, Quaternion.Euler(0f, 0f, stunned ? 80f : 0f), Time.deltaTime * 8f); // GLB: CritterAnimator flips the body
            float wet = MaxWetness.Value > 0f ? Mathf.Clamp01(Wetness.Value / MaxWetness.Value) : 0f;
            _mpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].GetPropertyBlock(_mpb);
                var wetCol = Color.Lerp(_baseColors[i], new Color(0.25f, 0.4f, 0.75f), wet * 0.6f);
                _mpb.SetColor("_BaseColor", wetCol);
                _mpb.SetColor(GltfBaseColor, wetCol); // Hunyuan3D critters keep their glTF PBR material
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }
    }
}
