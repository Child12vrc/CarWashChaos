using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// Drives the third-person Y Bot body animator from observed velocity (works the same on owner and remote copies).
    /// Uses the TPS prototype controller (Speed / MoveX / MoveZ / Crouch) with root motion off.
    /// </summary>
    public class PlayerBody : NetworkBehaviour
    {
        static readonly int HSpeed = Animator.StringToHash("Speed");
        static readonly int HX = Animator.StringToHash("MoveX");
        static readonly int HZ = Animator.StringToHash("MoveZ");
        static readonly int HCrouch = Animator.StringToHash("Crouch");

        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerStatus _status;
        [Tooltip("Body speed (m/s) that matches the walk / run animation strides")]
        [SerializeField] private float _walkAnimSpeed = 1.5f, _runAnimSpeed = 3.9f;

        Vector3 _lastPos;
        Vector3 _vel;
        Vector2 _dir = Vector2.up;

        void Start() => _lastPos = transform.position;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _animator == null) return;
            Vector3 v = (transform.position - _lastPos) / dt;
            _lastPos = transform.position;
            v.y = 0f;
            if (v.magnitude > 20f) v = Vector3.zero; // teleport
            _vel = Vector3.Lerp(_vel, v, 1f - Mathf.Exp(-10f * dt));

            Vector3 local = transform.InverseTransformDirection(_vel);
            float speed = local.magnitude;
            // map real speed → blend level (0 idle, 1 walk, 2 run) and scale playback so feet roughly match
            float level = speed <= _walkAnimSpeed ? speed / _walkAnimSpeed : 1f + Mathf.Clamp01((speed - _walkAnimSpeed) / (_runAnimSpeed - _walkAnimSpeed));
            // inside the walk/run range the blend already matches; faster than the run clip → play it faster
            _animator.speed = speed > _runAnimSpeed ? Mathf.Min(speed / _runAnimSpeed, 1.8f) : 1f;
            if (speed > 0.05f) _dir = new Vector2(local.x, local.z).normalized;

            bool crouch = _status != null && _status.Has(StatusFlags.Crouching);
            _animator.SetFloat(HSpeed, crouch ? Mathf.Min(level, 1f) : level, 0.08f, dt);
            _animator.SetFloat(HX, _dir.x, 0.08f, dt);
            _animator.SetFloat(HZ, _dir.y, 0.08f, dt);
            _animator.SetBool(HCrouch, crouch);
        }
    }
}
