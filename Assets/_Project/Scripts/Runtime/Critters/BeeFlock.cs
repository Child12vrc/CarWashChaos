using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>
    /// C02 bee swarm visual (GLB): several C02_bee instances buzzing around the swarm centre — each on its own wobbling orbit,
    /// facing where it flies, wings (Wing_L/R, pivot at the root) flapping. Purely local on every peer: the swarm's position
    /// comes from NetworkTransform and Spread/Die from BeeSwarm's replicated state — nothing per bee is networked.
    /// </summary>
    public class BeeFlock : MonoBehaviour
    {
        [SerializeField] private Transform[] _bees;
        [SerializeField] private float _flapHz = 28f;

        /// <summary>Orbit radius (BeeSwarm: tight 0.55 m ball, 3 m when dispersed by the washer).</summary>
        public float Spread { get; set; } = 0.55f;

        Transform[] _wingL, _wingR;
        Quaternion[] _wingLRest, _wingRRest;
        Vector3[] _axis, _vel;
        float[] _speed, _phase, _rad;
        bool _dead;

        public int Count => _bees != null ? _bees.Length : 0;

        void Awake()
        {
            int n = Count;
            _wingL = new Transform[n]; _wingR = new Transform[n]; _wingLRest = new Quaternion[n]; _wingRRest = new Quaternion[n];
            _axis = new Vector3[n]; _vel = new Vector3[n]; _speed = new float[n]; _phase = new float[n]; _rad = new float[n];
            var rng = new System.Random(GetInstanceID());
            for (int i = 0; i < n; i++)
            {
                _wingL[i] = Find(_bees[i], "Wing_L"); _wingR[i] = Find(_bees[i], "Wing_R");
                if (_wingL[i]) _wingLRest[i] = _wingL[i].localRotation;
                if (_wingR[i]) _wingRRest[i] = _wingR[i].localRotation;
                _axis[i] = new Vector3((float)rng.NextDouble() - 0.5f, 1.2f, (float)rng.NextDouble() - 0.5f).normalized;
                _speed[i] = 1.6f + (float)rng.NextDouble() * 1.6f;
                _phase[i] = (float)rng.NextDouble() * 6.28f;
                _rad[i] = 0.55f + (float)rng.NextDouble() * 0.45f;
            }
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>Smothered by foam: every bee drops to the ground and stops flapping.</summary>
        public void Die() => _dead = true;

        void LateUpdate()
        {
            float dt = Time.deltaTime, t = Time.time;
            for (int i = 0; i < Count; i++)
            {
                var b = _bees[i];
                if (b == null) continue;
                if (_dead)
                {
                    _vel[i] += Physics.gravity * dt;
                    var p = b.position + _vel[i] * dt;
                    if (p.y < 0.03f) { p.y = 0.03f; _vel[i] = Vector3.zero; }
                    b.position = p;
                    continue;
                }
                // wobbling orbit around the swarm centre (local space), radius follows Spread
                _phase[i] += _speed[i] * dt * (Spread > 1f ? 0.6f : 1f);
                float r = Spread * _rad[i];
                var basis = Quaternion.FromToRotation(Vector3.up, _axis[i]);
                var local = basis * new Vector3(Mathf.Cos(_phase[i]) * r, Mathf.Sin(_phase[i] * 2.3f + i) * 0.25f * r, Mathf.Sin(_phase[i]) * r);
                var prev = b.localPosition;
                b.localPosition = Vector3.Lerp(prev, local, 1f - Mathf.Exp(-dt * 8f));
                var fly = b.localPosition - prev;
                if (fly.sqrMagnitude > 1e-6f) b.localRotation = Quaternion.Slerp(b.localRotation, Quaternion.LookRotation(fly.normalized), dt * 10f);
                // wing flap: rotate about the body's forward axis at the wing root, mirrored left / right
                float flap = Mathf.Sin(t * _flapHz * Mathf.PI * 2f + i) * 55f;
                if (_wingL[i]) _wingL[i].localRotation = _wingLRest[i] * Quaternion.Euler(0f, 0f, flap);
                if (_wingR[i]) _wingR[i].localRotation = _wingRRest[i] * Quaternion.Euler(0f, 0f, -flap);
            }
        }
    }
}
