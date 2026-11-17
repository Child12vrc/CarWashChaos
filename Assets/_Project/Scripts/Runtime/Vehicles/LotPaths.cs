using System.Collections.Generic;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Vehicle waypoints for the lot (TECH S06: road → queue slot → bay stop → exit). Scene object built by LotBuilder;
    /// the server's VehiclePathMover (T2.2) follows these. All positions are ground-level, y = 0.
    /// </summary>
    public class LotPaths : MonoBehaviour
    {
        public static LotPaths Instance { get; private set; }

        [Tooltip("Road spawn → queue lane mouth")] [SerializeField] private Transform[] _arrival;
        [Tooltip("Queue slots, index 0 = head (next to be served)")] [SerializeField] private Transform[] _queueSlots;
        [Tooltip("Feed lane north of the bays, west → east")] [SerializeField] private Transform[] _feedLane;
        [Tooltip("Exit lane south of the bays → road → despawn")] [SerializeField] private Transform[] _exitLane;
        [SerializeField] private Bay[] _bays;

        public IReadOnlyList<Transform> Arrival => _arrival;
        public IReadOnlyList<Transform> QueueSlots => _queueSlots;
        public IReadOnlyList<Bay> Bays => _bays;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Road spawn → tail of the queue → forward through slots up to `slot`.</summary>
        public List<Vector3> RouteToQueueSlot(int slot)
        {
            var r = new List<Vector3>();
            foreach (var t in _arrival) r.Add(t.position);
            for (int i = _queueSlots.Length - 1; i >= slot; i--) r.Add(_queueSlots[i].position);
            return r;
        }

        /// <summary>Queue head → feed lane → bay entry → bay stop.</summary>
        public List<Vector3> RouteHeadToBay(int bayIndex)
        {
            var bay = _bays[bayIndex];
            var r = new List<Vector3> { _queueSlots[0].position };
            float bx = bay.Entry.position.x;
            foreach (var f in _feedLane)
            {
                if (f.position.x > bx + 0.5f) break;
                r.Add(f.position);
            }
            r.Add(new Vector3(bx, 0f, _feedLane[0].position.z));
            r.Add(bay.Entry.position);
            r.Add(bay.Stop.position);
            return r;
        }

        /// <summary>Bay stop → bay exit → exit lane → off-lot despawn point.</summary>
        public List<Vector3> RouteBayToExit(int bayIndex)
        {
            var bay = _bays[bayIndex];
            var r = new List<Vector3> { bay.Stop.position, bay.Exit.position };
            r.Add(new Vector3(bay.Exit.position.x, 0f, _exitLane[0].position.z));
            foreach (var e in _exitLane) if (e.position.x >= bay.Exit.position.x - 0.5f || e.position.z < _exitLane[0].position.z - 0.5f) r.Add(e.position);
            return r;
        }

        /// <summary>Drops the part of a route the vehicle has already driven (nearest segment to `pos`).</summary>
        public static List<Vector3> TrimRoute(List<Vector3> route, Vector3 pos)
        {
            pos.y = 0f;
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i + 1 < route.Count; i++)
            {
                var a = route[i]; var b = route[i + 1];
                var ab = b - a; float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return route.GetRange(best + 1, route.Count - best - 1);
        }

        /// <summary>Angry customer leaving straight from a queue slot.</summary>
        /// <remarks>Pulls out to the west of the queue lane (cars behind would block a reverse), then back to the road westward.</remarks>
        public List<Vector3> RouteQueueToExit(int slot)
        {
            var s = _queueSlots[Mathf.Clamp(slot, 0, _queueSlots.Length - 1)].position;
            float roadZ = _arrival[0].position.z;
            float westX = s.x - 4.5f;
            return new List<Vector3>
            {
                s + new Vector3(-1.5f, 0f, 2.5f),
                new Vector3(westX, 0f, s.z),
                new Vector3(westX, 0f, roadZ + 3f),
                new Vector3(westX - 3f, 0f, roadZ),
                new Vector3(_arrival[0].position.x - 4f, 0f, roadZ),
            };
        }
    }
}
