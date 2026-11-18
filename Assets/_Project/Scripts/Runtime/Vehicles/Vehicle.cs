using System;
using System.Collections.Generic;
using CarWash.Data;
using CarWash.Dirt;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// A customer vehicle (TECH S06). Server-authoritative state machine driven by QueueLane / DayManager; clients read
    /// State, BayIndex, timers and dirt %. Doors/trunk (VehicleDoor) and interior trash (TrashItem) hang off it.
    /// </summary>
    [RequireComponent(typeof(VehiclePathMover), typeof(DirtNetSync))]
    public class Vehicle : NetworkBehaviour
    {
        public static readonly Color[] Paints =
        {
            new Color(0.15f, 0.4f, 0.85f), new Color(0.85f, 0.18f, 0.18f), new Color(0.95f, 0.95f, 0.92f),
            new Color(0.2f, 0.65f, 0.3f), new Color(0.98f, 0.78f, 0.2f), new Color(0.12f, 0.12f, 0.14f),
        };

        [SerializeField] private DataRegistry _data;
        [SerializeField] private string _dataId;
        [SerializeField] private Transform _interior, _surprise, _driver;
        /// <summary>GLB vehicles: body-coloured parts outside the dirt body (doors, trunk, mirrors) — renderer + material slot.</summary>
        [SerializeField] private Renderer[] _paintRenderers = Array.Empty<Renderer>();
        [SerializeField] private int[] _paintSlots = Array.Empty<int>();

        public readonly NetworkVariable<VehicleState> State = new NetworkVariable<VehicleState>(VehicleState.Arriving);
        public readonly NetworkVariable<int> BayIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> PaintIndex = new NetworkVariable<int>(-1);
        /// <summary>Server time when the current timed phase started (queue patience or wash timer).</summary>
        public readonly NetworkVariable<float> PhaseStart = new NetworkVariable<float>();

        public VehicleData Data => _data != null ? _data.Get<VehicleData>(_dataId) : null;
        public string DataId => _dataId;
        public DirtNetSync Dirt { get; private set; }
        public VehiclePathMover Mover { get; private set; }
        public Transform InteriorSocket => _interior;
        public Transform SurpriseSocket => _surprise;
        public int QueueSlot { get; set; } = -1;
        public float PhaseElapsed => NetworkManager != null ? (float)NetworkManager.ServerTime.Time - PhaseStart.Value : 0f;
        public IReadOnlyList<VehicleDoor> Doors => _doors;
        public readonly List<TrashItem> Trash = new List<TrashItem>();

        /// <summary>Server: a door/trunk was opened (SurpriseTrigger OnOpen, T3.6).</summary>
        public event Action<Vehicle, VehicleDoor> DoorOpened;
        /// <summary>Server: state changed.</summary>
        public event Action<Vehicle, VehicleState> StateChanged;

        VehicleDoor[] _doors = Array.Empty<VehicleDoor>();

        void Awake()
        {
            Dirt = GetComponent<DirtNetSync>();
            Mover = GetComponent<VehiclePathMover>();
            _doors = GetComponentsInChildren<VehicleDoor>(true);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (PaintIndex.Value < 0) PaintIndex.Value = UnityEngine.Random.Range(0, Paints.Length);
                PhaseStart.Value = (float)NetworkManager.ServerTime.Time;
            }
            // P1: clients only follow NetworkTransform — rigidbody interpolation would fight it (visible stepping)
            if (!IsServer && TryGetComponent<Rigidbody>(out var rb)) rb.interpolation = RigidbodyInterpolation.None;
            PaintIndex.OnValueChanged += (_, v) => ApplyPaint(v);
            ApplyPaint(PaintIndex.Value);
            foreach (var d in _doors) d.Bind(this);
        }

        void ApplyPaint(int i)
        {
            if (i < 0) return;
            var c = Paints[i % Paints.Length];
            Dirt.Painter.SetBaseColor(c);
            if (_paintRenderers.Length == 0) return;
            var mpb = new MaterialPropertyBlock();
            for (int k = 0; k < _paintRenderers.Length && k < _paintSlots.Length; k++)
            {
                var r = _paintRenderers[k];
                if (r == null) continue;
                r.GetPropertyBlock(mpb, _paintSlots[k]);
                mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb, _paintSlots[k]);
            }
        }

        /// <summary>Server, before Spawn: dirt from VehicleData (Day-1 cars have no grease: U05 note).</summary>
        public void ServerConfigure(bool noGrease)
        {
            var d = Data;
            if (d == null) return;
            var dirt = d.dirt;
            if (noGrease) dirt.y = 0f;
            Dirt.ServerConfigure(dirt, d.textureSize, UnityEngine.Random.Range(1f, 999f));
        }

        public void ServerSetState(VehicleState s)
        {
            if (!IsServer || State.Value == s) return;
            State.Value = s;
            PhaseStart.Value = (float)NetworkManager.ServerTime.Time;
            StateChanged?.Invoke(this, s);
        }

        internal void ServerNotifyDoorOpened(VehicleDoor door) => DoorOpened?.Invoke(this, door);

        /// <summary>Doors may be used only while the car is parked (queue or bay).</summary>
        public bool Stationary => (State.Value == VehicleState.Queued || State.Value == VehicleState.Washing || State.Value == VehicleState.Inspecting) && !Mover.Moving;

        /// <summary>Replicated count of interior trash still inside the car.</summary>
        public readonly NetworkVariable<int> TrashLeft = new NetworkVariable<int>();

        /// <summary>Interior trash still inside the car (GDD §5: −2% each). Server counts, clients read TrashLeft.</summary>
        public int TrashRemaining
        {
            get
            {
                if (!IsServer) return TrashLeft.Value;
                int n = 0; foreach (var t in Trash) if (t != null && t.Attached) n++; return n;
            }
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            int n = TrashRemaining;
            if (TrashLeft.Value != n) TrashLeft.Value = n;
        }

        /// <summary>Server: close every door (before driving off).</summary>
        public void ServerCloseDoors() { foreach (var d in _doors) d.ServerSetOpen(false); }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            foreach (var t in Trash) if (t != null && t.Attached && t.IsSpawned) t.NetworkObject.Despawn(true);
        }
    }
}
