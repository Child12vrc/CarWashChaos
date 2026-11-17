using System;
using System.Collections.Generic;
using CarWash.Core;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Server-only kinematic mover (TECH S06): follows a waypoint list at `speed`, turning toward the travel direction,
    /// and waits while a player / vehicle / prop blocks the way ahead. Position is replicated by NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehiclePathMover : MonoBehaviour
    {
        [SerializeField] private float _speed = 6f;
        [SerializeField] private float _turnSpeed = 140f; // deg/s
        [SerializeField] private float _accel = 6f;
        [SerializeField] private float _lookAhead = 1.2f;
        [SerializeField] private float _halfLength = 2.3f;

        readonly List<Vector3> _points = new List<Vector3>();
        int _index;
        float _curSpeed;
        Action _onArrive;
        Rigidbody _rb;
        static readonly RaycastHit[] Hits = new RaycastHit[8];

        public bool Moving => _index < _points.Count;
        public bool Blocked { get; private set; }

        void Awake() { _rb = GetComponent<Rigidbody>(); _rb.isKinematic = true; }

        public void SetHalfLength(float h) => _halfLength = h;

        public void Follow(IList<Vector3> points, Action onArrive)
        {
            _points.Clear();
            foreach (var p in points) _points.Add(new Vector3(p.x, 0f, p.z));
            // skip a first point we are already standing on
            _index = _points.Count > 0 && Flat(_points[0] - transform.position).sqrMagnitude < 0.25f ? 1 : 0;
            _onArrive = onArrive;
            if (!Moving) Arrive();
        }

        public void Stop() { _points.Clear(); _index = 0; _curSpeed = 0f; _onArrive = null; }

        void FixedUpdate()
        {
            if (!Moving) return;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = _rb.position;
            Vector3 target = _points[_index];
            Vector3 to = Flat(target - pos);
            float dist = to.magnitude;
            bool last = _index == _points.Count - 1;

            Blocked = IsBlocked();
            float wanted = Blocked ? 0f : (last ? Mathf.Min(_speed, dist * 1.5f + 0.4f) : _speed);
            _curSpeed = Mathf.MoveTowards(_curSpeed, wanted, (Blocked ? _accel * 3f : _accel) * dt);

            if (dist > 0.05f)
            {
                var desired = Quaternion.LookRotation(to.normalized, Vector3.up);
                _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, desired, _turnSpeed * dt));
            }
            float step = Mathf.Min(_curSpeed * dt, dist);
            // move along the heading (cars don't strafe); snap the last centimetres
            Vector3 fwd = _rb.rotation * Vector3.forward;
            Vector3 move = Vector3.Angle(fwd, to) < 60f ? fwd * step : to.normalized * step * 0.3f;
            _rb.MovePosition(pos + move);

            if (dist <= Mathf.Max(0.15f, step + 0.02f) || (!last && dist < 1.0f))
            {
                _index++;
                if (!Moving) Arrive();
            }
        }

        void Arrive()
        {
            _curSpeed = 0f;
            var cb = _onArrive; _onArrive = null;
            cb?.Invoke();
        }

        bool IsBlocked()
        {
            if (_curSpeed < 0.01f && Flat(_points[_index] - _rb.position).magnitude < 0.3f) return false;
            Vector3 fwd = _rb.rotation * Vector3.forward;
            Vector3 origin = _rb.position + Vector3.up * 0.9f + fwd * (_halfLength - 0.6f);
            float range = _lookAhead + _curSpeed * 0.4f;
            int mask = (1 << Layers.Player) | (1 << Layers.Vehicle) | (1 << Layers.Critter);
            int n = Physics.SphereCastNonAlloc(origin, 0.8f, fwd, Hits, range, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!Hits[i].collider.transform.IsChildOf(transform)) return true;
            return false;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
