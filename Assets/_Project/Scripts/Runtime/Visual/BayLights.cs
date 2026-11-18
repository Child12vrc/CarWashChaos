using System.Collections.Generic;
using UnityEngine;

namespace CarWash.Visual
{
    /// <summary>
    /// M4.9 G7: the ceiling fixtures of one bay (LED bars + real spot lights, no shadows). Local scene object; the bay's
    /// networked wall switch (<see cref="Day.BayLightSwitch"/>) turns it on / off on every peer. Fades in / out quickly.
    /// </summary>
    public class BayLights : MonoBehaviour
    {
        [SerializeField] private int _bay;
        [SerializeField] private Light[] _lights = System.Array.Empty<Light>();
        [SerializeField] private Renderer[] _bars = System.Array.Empty<Renderer>();
        [SerializeField] private Material _onMat, _offMat;

        static readonly Dictionary<int, BayLights> _byBay = new Dictionary<int, BayLights>();
        float[] _full;
        float _k = -1f;
        public bool On { get; set; }
        public int Bay => _bay;
        public float Level => Mathf.Max(0f, _k);

        public static BayLights ForBay(int i) => _byBay.TryGetValue(i, out var b) ? b : null;

        void Awake()
        {
            _full = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) { _full[i] = _lights[i].intensity; _lights[i].enabled = false; }
            Apply(0f);
        }
        void OnEnable() => _byBay[_bay] = this;
        void OnDisable() { if (_byBay.TryGetValue(_bay, out var b) && b == this) _byBay.Remove(_bay); }

        void Update()
        {
            float target = On ? 1f : 0f;
            if (Mathf.Approximately(_k, target)) return;
            Apply(Mathf.MoveTowards(Mathf.Max(0f, _k), target, Time.deltaTime * 6f));
        }

        void Apply(float k)
        {
            _k = k;
            for (int i = 0; i < _lights.Length; i++) { _lights[i].enabled = k > 0.001f; _lights[i].intensity = _full[i] * k; }
            var m = k > 0.5f ? _onMat : _offMat;
            foreach (var r in _bars) if (r != null && m != null && r.sharedMaterial != m) r.sharedMaterial = m;
        }
    }
}
