using UnityEngine;

namespace CarWash.Net
{
    /// <summary>
    /// P1: on remote copies, the visible body glides after the networked root (critically damped, ~70 ms) instead of
    /// copying it every frame — hides the micro-stutter that jitter + packet loss leave in owner-authoritative player
    /// motion. Colliders/hit tests stay on the exact root; large jumps (teleport, respawn) snap.
    /// </summary>
    public class RemoteVisualSmoother : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private float _smoothTime = 0.07f;
        [SerializeField] private float _snapDistance = 3f;

        Vector3 _pos, _vel;
        Vector3 _localOffset;
        bool _init;

        /// <summary>Where the body is drawn this frame (world).</summary>
        public Vector3 VisualPosition => _visual != null ? _visual.position : transform.position;

        public void Bind(Transform visual) { _visual = visual; _init = false; }

        void LateUpdate()
        {
            if (_visual == null) return;
            if (!_init) { _localOffset = _visual.localPosition; _pos = transform.TransformPoint(_localOffset); _init = true; }
            var target = transform.TransformPoint(_localOffset);
            if ((target - _pos).sqrMagnitude > _snapDistance * _snapDistance) { _pos = target; _vel = Vector3.zero; }
            else _pos = Vector3.SmoothDamp(_pos, target, ref _vel, _smoothTime);
            _visual.position = _pos;
        }

        void OnDisable() { if (_visual != null && _init) _visual.localPosition = _localOffset; }
    }
}
