using CarWash.Core;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// P5 motion-sickness-safe camera (owner only). While our body is a ragdoll (knocked down, carried, thrown, getting
    /// up) the first-person camera would spin with the head — instead it eases (0.35 s) into a level third-person
    /// follow camera: orbits the hips at a fixed 20° pitch, yaw from the mouse (never from the body), no roll, no
    /// head bob, no shake, same FOV, position smoothed (SmoothDamp) and pulled in front of walls (sphere cast).
    /// Eases back to first person once standing.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PlayerCameraRig : MonoBehaviour
    {
        public const float BlendTime = 0.35f, Distance = 3.2f, Pitch = 20f, WallMargin = 0.25f;
        const int WallMask = (1 << Layers.Default) | (1 << Layers.Vehicle) | (1 << Layers.Prop);

        PlayerLook _look;
        PlayerRagdoll _rag;
        float _blend;
        Vector3 _pos, _vel, _target;
        bool _init;

        public float Blend => _blend;
        public bool ThirdPerson => _blend > 0.5f;
        /// <summary>Dev metrics: roll of the camera (deg) and distance from it to the nearest wall surface along the boom.</summary>
        public float LastRoll { get; private set; }
        public bool LastClipped { get; private set; }

        void Awake() { _look = GetComponent<PlayerLook>(); _rag = GetComponent<PlayerRagdoll>(); _net = GetComponent<PlayerNet>(); }
        PlayerNet _net;
        bool _bodyShown;

        void LateUpdate()
        {
            if (_look == null || !_look.IsOwner || _rag == null) return;
            bool want = _rag.ControlsBody;
            float dt = Time.deltaTime;
            _blend = Mathf.MoveTowards(_blend, want ? 1f : 0f, dt / BlendTime);
            bool show = _blend > 0.05f;                     // in third person we must see our own body
            if (show != _bodyShown && _net != null) { _net.SetOwnBodyVisible(show); _bodyShown = show; }
            var cam = _look.Camera.transform;
            if (_blend <= 0f) { _init = false; return; }

            var hips = _rag.Hips.position;
            var target = hips + Vector3.up * 0.4f;
            if (!_init) { _target = target; _pos = target - (Quaternion.Euler(Pitch, _look.transform.eulerAngles.y, 0f) * Vector3.forward) * Distance; _vel = Vector3.zero; _init = true; }
            _target = Vector3.Lerp(_target, target, 1f - Mathf.Exp(-10f * dt));   // damp the jitter of a tumbling body

            float yaw = _look.transform.eulerAngles.y;                              // mouse-controlled, never the body's spin
            var dir = Quaternion.Euler(Pitch, yaw, 0f) * Vector3.forward;
            float dist = Distance;
            LastClipped = Physics.SphereCast(_target, WallMargin, -dir, out var hit, Distance, WallMask, QueryTriggerInteraction.Ignore);
            if (LastClipped) dist = Mathf.Max(0.05f, hit.distance - 0.05f);   // P5 fix: a 0.4 m minimum pushed the camera into walls
            var desired = _target - dir * dist;
            // still inside something (body jammed against a wall)? look from straight above instead
            if (Physics.CheckSphere(desired, 0.12f, WallMask, QueryTriggerInteraction.Ignore))
            {
                float up = Physics.SphereCast(_target, WallMargin, Vector3.up, out var upHit, 2.5f, WallMask, QueryTriggerInteraction.Ignore) ? Mathf.Max(0.2f, upHit.distance - 0.1f) : 2.5f;
                desired = _target + Vector3.up * up;
            }
            _pos = Vector3.SmoothDamp(_pos, desired, ref _vel, 0.12f);
            if (LastClipped && (_pos - _target).sqrMagnitude > dist * dist) _pos = desired;     // never sit inside the wall
            // rotation straight from the boom angles: always level, never flips (LookRotation from the head straight down
            // onto the hips snapped 159° in one frame — P5 check)
            var rot = Quaternion.Euler(Pitch, yaw, 0f);

            float e = _blend * _blend * (3f - 2f * _blend);
            var fpPos = _look.Head.position; var fpRot = _look.Head.rotation;
            cam.SetPositionAndRotation(Vector3.Lerp(fpPos, _pos, e), Quaternion.Slerp(fpRot, rot, e));
            LastRoll = Mathf.Abs(Mathf.DeltaAngle(0f, cam.eulerAngles.z));
        }
    }
}
