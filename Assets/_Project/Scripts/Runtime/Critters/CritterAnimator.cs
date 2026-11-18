using CarWash.Tools;
using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>
    /// Code animation for the rig-less GLB critters (docs/ART_IMPORT.md 3D Models: parts split into nodes, pivots at the
    /// joints). Runs on EVERY peer from replicated data only — the root's NetworkTransform motion (speed) and CritterBase.State —
    /// so nothing extra goes over the network.
    ///  Quadruped (raccoon / possum): diagonal leg swing ∝ speed, tail wag, head bob; stunned = flipped belly-up with twitching
    ///    legs; possum playing dead = rolled 90° on its side, legs stiff.
    ///  Slime: Body squash &amp; stretch hop while moving, breathing idle, drips wobble, blinking eyes; stunned = flattened.
    ///  Netted (any peer): the visual hangs with Socket_Carry at the holder's net Socket_Capture.
    /// </summary>
    public class CritterAnimator : MonoBehaviour
    {
        public enum Kind { Quadruped, Slime, Bee }

        [SerializeField] private Kind _kind;
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _body, _head, _tail, _legFL, _legFR, _legBL, _legBR, _eyeL, _eyeR, _carry;
        [SerializeField] private Transform[] _drips;
        [SerializeField] private float _maxSpeed = 4f;

        CritterBase _critter;
        Vector3 _lastPos;
        float _speed, _phase, _flip, _side, _blinkAt, _seed;
        Quaternion _bodyRot, _headRot, _tailRot, _fl, _fr, _bl, _br;
        Vector3 _bodyScale, _bodyPos;
        Vector3[] _dripScale;

        /// <summary>Last computed ground speed (m/s) — exposed for the automated check.</summary>
        public float Speed => _speed;
        /// <summary>0 upright … 1 fully flipped / rolled (stun, play dead).</summary>
        public float Flip => Mathf.Max(_flip, _side);
        public Transform CarrySocket => _carry;

        void Awake()
        {
            _critter = GetComponent<CritterBase>();
            _lastPos = transform.position;
            _seed = Random.value * 10f;
            if (_body) { _bodyRot = _body.localRotation; _bodyScale = _body.localScale; _bodyPos = _body.localPosition; }
            if (_head) _headRot = _head.localRotation;
            if (_tail) _tailRot = _tail.localRotation;
            if (_legFL) _fl = _legFL.localRotation;
            if (_legFR) _fr = _legFR.localRotation;
            if (_legBL) _bl = _legBL.localRotation;
            if (_legBR) _br = _legBR.localRotation;
            if (_drips != null) { _dripScale = new Vector3[_drips.Length]; for (int i = 0; i < _drips.Length; i++) _dripScale[i] = _drips[i].localScale; }
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var d = transform.position - _lastPos; d.y = 0f;
            _lastPos = transform.position;
            _speed = Mathf.Lerp(_speed, Mathf.Min(d.magnitude / dt, 12f), 1f - Mathf.Exp(-dt * 10f));
            var state = _critter != null ? _critter.State.Value : CritterState.Idle;
            bool stunned = state == CritterState.Stunned;
            bool playDead = _critter is Possum && state == CritterState.Act;
            bool carried = state == CritterState.Carried;
            _flip = Mathf.MoveTowards(_flip, stunned ? 1f : 0f, dt * 4f);
            _side = Mathf.MoveTowards(_side, playDead ? 1f : 0f, dt * 5f);
            float move = Mathf.Clamp01(_speed / _maxSpeed);
            float t = Time.time + _seed;

            if (_visual != null) _visual.localPosition = Vector3.zero;
            switch (_kind)
            {
                case Kind.Quadruped: Quadruped(dt, t, move, carried); break;
                case Kind.Slime: Slime(dt, t, move, stunned); break;
            }
            if (carried) HangOnNet();
        }

        void Quadruped(float dt, float t, float move, bool carried)
        {
            // gait: ~2.2 strides per metre, diagonal pairs; carried = dangling paddle
            _phase += (carried ? 9f : _speed * 2.2f * Mathf.PI * 2f / 3f) * dt;
            float amp = carried ? 25f : 38f * move;
            float s = Mathf.Sin(_phase) * amp;
            float twitch = Mathf.Sin(t * 23f) * 12f * _flip;      // stunned: legs kick in the air
            float stiff = 55f * _side;                             // play dead: legs stick straight out
            Leg(_legFL, _fl, s + twitch, -stiff);
            Leg(_legBR, _br, s - twitch, stiff);
            Leg(_legFR, _fr, -s - twitch, stiff);
            Leg(_legBL, _bl, -s + twitch, -stiff);
            if (_tail)
            {
                float wag = Mathf.Sin(t * (move > 0.2f ? 9f : 4f)) * (move > 0.2f ? 30f : 18f) * (1f - _side);
                _tail.localRotation = _tailRot * Quaternion.Euler(-8f * move, wag, 0f);
            }
            if (_head) _head.localRotation = _headRot * Quaternion.Euler(Mathf.Sin(_phase * 2f) * 4f * move + Mathf.Sin(t * 1.3f) * 3f * (1f - move), Mathf.Sin(t * 0.7f) * 10f * (1f - move), 0f);
            if (_body)
            {
                // flip belly-up (stun) / roll 90° on the side (play dead) about the body's own centre; little bob while walking
                float roll = Mathf.SmoothStep(0f, 180f, _flip);
                if (_side > _flip) roll = Mathf.SmoothStep(0f, 90f, _side);
                _body.localRotation = Quaternion.Euler(0f, 0f, roll) * _bodyRot;
                _body.localPosition = _bodyPos + Vector3.up * (Mathf.Abs(Mathf.Sin(_phase)) * 0.025f * move);
            }
        }

        static void Leg(Transform leg, Quaternion rest, float swing, float splay)
        {
            if (leg) leg.localRotation = rest * Quaternion.Euler(swing, 0f, splay);
        }

        void Slime(float dt, float t, float move, bool stunned)
        {
            // hop cycle (~2.2 Hz moving): body squashes on landing, stretches in the air; breathing when idle
            _phase += (move > 0.05f ? 14f : 3f) * dt;
            float k = Mathf.Sin(_phase);
            float hop = Mathf.Abs(k);
            float sy = move > 0.05f ? 1f + (hop - 0.5f) * 0.45f * Mathf.Clamp01(move * 2f) : 1f + k * 0.04f;
            sy = Mathf.Lerp(sy, 0.5f, _flip);                      // stunned: splat flat
            float sxz = 1f / Mathf.Sqrt(Mathf.Max(0.2f, sy));       // keep the volume
            if (_body) _body.localScale = Vector3.Scale(_bodyScale, new Vector3(sxz, sy, sxz));
            if (_visual != null && move > 0.05f && _flip < 0.5f) _visual.localPosition = Vector3.up * hop * 0.22f * Mathf.Clamp01(move * 2f);
            if (_drips != null)
                for (int i = 0; i < _drips.Length; i++)
                    if (_drips[i]) _drips[i].localScale = Vector3.Scale(_dripScale[i], new Vector3(1f, 1f + Mathf.Sin(t * 3f + i * 2.1f) * 0.25f, 1f));
            // blink every few seconds (eyes squash to a line); stunned = squinting
            if (t > _blinkAt + 0.15f && Random.value < dt * 0.4f) _blinkAt = t;
            float lid = t - _blinkAt < 0.15f ? 0.1f : 1f;
            if (_flip > 0.5f) lid = 0.25f;
            if (_eyeL) _eyeL.localScale = new Vector3(1f, lid, 1f);
            if (_eyeR) _eyeR.localScale = new Vector3(1f, lid, 1f);
        }

        /// <summary>Netted: every peer hangs the visual from the holder's net hoop (Socket_Capture), Socket_Carry at its centre.</summary>
        void HangOnNet()
        {
            if (_visual == null || _carry == null || _critter == null) return;
            foreach (var tool in ToolBase.All)
            {
                if (tool is NetPole net && net.Capture != null && tool.Holder.Value == _critter.CarrierClient.Value && !tool.Stowed)
                {
                    _visual.position += net.Capture.position - _carry.position;
                    return;
                }
            }
        }
    }
}
