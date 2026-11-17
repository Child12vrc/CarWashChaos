using System;
using System.Collections.Generic;
using CarWash.Core;
using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Server customer flow (TECH S06): road → queue slot (4) → free open bay → wash → inspect (2 s) → leave.
    /// Queue patience (GDD §8: 45 s) → Angry → leaves from the queue. Pay/refuse/rep are applied by DayManager (T2.4)
    /// via the events below.
    /// </summary>
    public class QueueLane : MonoBehaviour
    {
        public static QueueLane Instance { get; private set; }
        /// <summary>Dev/test override of the queue patience (seconds, 0 = BalanceData).</summary>
        public static float PatienceOverride;

        [SerializeField] private DataRegistry _data;
        [SerializeField] private LotPaths _paths;
        [SerializeField] private VehicleSpawner _spawner;

        readonly List<Vehicle> _queue = new List<Vehicle>();          // index = slot (0 = head)
        readonly List<Vehicle> _active = new List<Vehicle>();
        readonly System.Random _rng = new System.Random();

        public event Action<Vehicle> VehicleParked;    // started washing
        public event Action<Vehicle> VehicleFinished;  // DONE → inspection complete, about to leave (pay here)
        public event Action<Vehicle> VehicleAngry;     // left the queue angry
        public event Action<Vehicle> VehicleGone;      // despawned

        /// <summary>No vehicle within 7 m of the road spawn point (otherwise the arrival waits).</summary>
        public bool SpawnClear
        {
            get
            {
                var p = _paths.Arrival[0].position;
                foreach (var v in _active) if (v != null && (v.transform.position - p).sqrMagnitude < 49f) return false;
                return true;
            }
        }

        public int QueueCount => _queue.Count;
        public int SlotCount => _paths.QueueSlots.Count;
        public bool HasRoom => _queue.Count < SlotCount;
        public IReadOnlyList<Vehicle> Active => _active;
        public float Patience => PatienceOverride > 0f ? PatienceOverride : _data.balance.economy.patience;
        bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Server: a new customer arrives (returns null when the queue is full).</summary>
        public Vehicle ServerArrive(string vehicleId, bool noGrease)
        {
            if (!IsServer || !HasRoom || !SpawnClear) return null;
            var start = _paths.Arrival[0];
            var v = _spawner.ServerSpawn(vehicleId, start.position, Quaternion.LookRotation(Vector3.right), noGrease);
            if (v == null) return null;
            _active.Add(v);
            int slot = _queue.Count;
            _queue.Add(v);
            v.QueueSlot = slot;
            v.ServerSetState(VehicleState.Arriving);
            v.Mover.Follow(_paths.RouteToQueueSlot(slot), () => { if (v.State.Value == VehicleState.Arriving) v.ServerSetState(VehicleState.Queued); });
            // T3.6: surprises only for real customers (running day) unless a dev check forces them
            var dm = Day.DayManager.Instance;
            if ((dm != null && dm.State.Value == GameState.Day) || SurpriseTrigger.DevChanceOverride >= 0f || SurpriseTrigger.DevForcePick != null)
                v.GetComponent<SurpriseTrigger>()?.ServerRoll(dm != null ? dm.SurpriseChance : 0.35f, _rng, _data);
            GameEvents.RaiseVehicleArrived(v);
            return v;
        }

        /// <summary>Server: DONE pressed (or clean 100%) → inspection → leave.</summary>
        public void ServerFinish(Vehicle v)
        {
            if (!IsServer || v == null || v.State.Value != VehicleState.Washing) return;
            v.ServerCloseDoors();
            v.ServerSetState(VehicleState.Inspecting);
        }

        void Update()
        {
            if (!IsServer) return;
            float inspect = _data.balance.economy.inspectTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var v = _active[i];
                if (v == null) { _active.RemoveAt(i); continue; }
                if (v.State.Value == VehicleState.Inspecting && v.PhaseElapsed >= inspect) Leave(v, angry: false);
            }
            // patience in the queue
            for (int i = 0; i < _queue.Count; i++)
            {
                var v = _queue[i];
                if (v.State.Value == VehicleState.Queued && v.PhaseElapsed > Patience) { Leave(v, angry: true); return; }
            }
            // dispatch the head to a free open bay
            if (_queue.Count > 0 && _queue[0].State.Value == VehicleState.Queued && !_queue[0].Mover.Moving)
            {
                var bay = FreeBay();
                if (bay != null) Dispatch(_queue[0], bay);
            }
        }

        Bay FreeBay()
        {
            foreach (var b in _paths.Bays) if (b.IsOpen && b.Occupant == null) return b;
            return null;
        }

        void Dispatch(Vehicle v, Bay bay)
        {
            RemoveFromQueue(v);
            bay.Occupant = v;
            v.BayIndex.Value = bay.Index;
            v.ServerSetState(VehicleState.Parking);
            v.Mover.Follow(_paths.RouteHeadToBay(bay.Index), () =>
            {
                v.ServerSetState(VehicleState.Washing);
                VehicleParked?.Invoke(v);
            });
        }

        void Leave(Vehicle v, bool angry)
        {
            List<Vector3> route;
            if (angry)
            {
                route = _paths.RouteQueueToExit(Mathf.Max(0, v.QueueSlot));
                RemoveFromQueue(v);
                v.ServerSetState(VehicleState.Angry);
                VehicleAngry?.Invoke(v);
            }
            else
            {
                var bay = v.BayIndex.Value >= 0 ? _paths.Bays[v.BayIndex.Value] : null;
                route = _paths.RouteBayToExit(v.BayIndex.Value);
                VehicleFinished?.Invoke(v);
                v.ServerSetState(VehicleState.Leaving);
                if (bay != null) bay.Occupant = null; // next car may enter once this one is out of the way
            }
            v.ServerCloseDoors();
            v.Mover.Follow(route, () => Despawn(v));
        }

        void RemoveFromQueue(Vehicle v)
        {
            int idx = _queue.IndexOf(v);
            if (idx < 0) return;
            _queue.RemoveAt(idx);
            v.QueueSlot = -1;
            // everyone behind moves up one slot
            for (int i = idx; i < _queue.Count; i++)
            {
                var q = _queue[i];
                q.QueueSlot = i;
                var target = _paths.QueueSlots[i].position;
                if (q.State.Value == VehicleState.Queued) q.Mover.Follow(new[] { target }, null);
                else if (q.State.Value == VehicleState.Arriving)
                {
                    // keep driving forward: only the part of the new route that is still ahead of the car
                    var qq = q;
                    var route = LotPaths.TrimRoute(_paths.RouteToQueueSlot(i), q.transform.position);
                    q.Mover.Follow(route, () => { if (qq.State.Value == VehicleState.Arriving) qq.ServerSetState(VehicleState.Queued); });
                }
            }
        }

        void Despawn(Vehicle v)
        {
            _active.Remove(v);
            VehicleGone?.Invoke(v);
            if (v != null && v.IsSpawned) v.NetworkObject.Despawn(true);
        }

        /// <summary>Server: closing time — everyone still arriving/queued drives off (no anger, no rep change).</summary>
        public int ServerDismissQueue()
        {
            if (!IsServer) return 0;
            int n = 0;
            foreach (var v in _queue.ToArray())
            {
                var route = _paths.RouteQueueToExit(Mathf.Max(0, v.QueueSlot));
                RemoveFromQueue(v);
                v.ServerSetState(VehicleState.Leaving);
                v.ServerCloseDoors();
                var vv = v;
                v.Mover.Follow(route, () => Despawn(vv));
                n++;
            }
            return n;
        }

        /// <summary>Vehicles currently parking in / occupying a bay (Parking, Washing, Inspecting).</summary>
        public int BusyBays
        {
            get
            {
                int n = 0;
                foreach (var v in _active)
                    if (v != null && (v.State.Value == VehicleState.Parking || v.State.Value == VehicleState.Washing || v.State.Value == VehicleState.Inspecting)) n++;
                return n;
            }
        }

        /// <summary>Server: remove everything (day end / reset).</summary>
        public void ServerClearAll()
        {
            foreach (var v in _active.ToArray()) Despawn(v);
            _queue.Clear();
            foreach (var b in _paths.Bays) b.Occupant = null;
        }
    }
}
