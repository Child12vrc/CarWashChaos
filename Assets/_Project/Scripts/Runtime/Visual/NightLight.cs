using System.Collections.Generic;
using UnityEngine;

namespace CarWash.Visual
{
    /// <summary>
    /// M4.9 G1: something that lights up at night — street lamps, sign lights, car headlights. Lights fade with
    /// <see cref="DayNightCycle.Night"/> (disabled entirely in daylight, so they cost nothing), never cast shadows.
    /// Shared "glow" materials (lamp heads, signs) get their emission colour scaled once per change for all users.
    /// </summary>
    public class NightLight : MonoBehaviour
    {
        [SerializeField] private Light[] _lights = System.Array.Empty<Light>();
        [SerializeField] private float _onAt = 0.35f; // night level where it switches on (lamps first, signs a bit later…)
        float[] _full;
        float _level = -1f;

        public static readonly List<NightLight> All = new List<NightLight>();
        public static int LitCount { get; private set; }

        /// <summary>Called with the 0..1 level whenever it changes (lens quads etc.).</summary>
        public System.Action<float> OnLevel;

        /// <summary>Builders (edit time) and runtime AddComponent (Awake already ran): sets the lights and captures their full intensity.</summary>
        public void Init(Light[] lights, float onAt) { _lights = lights; _onAt = onAt; Capture(); _level = -1f; }

        void Awake() => Capture();

        void Capture()
        {
            _full = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) { if (_lights[i] == null) continue; _full[i] = _lights[i].intensity; _lights[i].shadows = LightShadows.None; _lights[i].enabled = false; }
        }
        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); if (_level > 0f) LitCount--; _level = -1f; }

        void Update()
        {
            // switch on over ~0.15 of the night ramp after _onAt (so lamps come on one by one in a natural spread)
            float k = Mathf.Clamp01((DayNightCycle.Night - _onAt) / 0.15f);
            if (Mathf.Abs(k - _level) < 0.01f) return;
            if (_level > 0f) LitCount--;
            _level = k;
            if (_level > 0f) LitCount++;
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null) continue;
                _lights[i].enabled = k > 0.001f;
                _lights[i].intensity = _full[i] * k;
            }
            OnLevel?.Invoke(k);
        }

        // ---------------------------------------------------------------- shared glow materials
        static readonly Dictionary<Material, (Color col, float onAt)> _glow = new Dictionary<Material, (Color, float)>();
        static float _glowLevel = -1f;

        /// <summary>Registers a shared material whose emission (<paramref name="hdr"/> at full night) follows the clock.</summary>
        public static void Glow(Material m, Color hdr, float onAt = 0.35f)
        {
            if (m == null || _glow.ContainsKey(m)) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            _glow[m] = (hdr, onAt);
            _glowLevel = -1f;
        }

        /// <summary>DayNightCycle calls this every frame.</summary>
        public static void UpdateGlow(float night)
        {
            if (Mathf.Abs(night - _glowLevel) < 0.01f) return;
            _glowLevel = night;
            foreach (var kv in _glow)
            {
                if (kv.Key == null) continue;
                float k = Mathf.Clamp01((night - kv.Value.onAt) / 0.15f);
                kv.Key.SetColor("_EmissionColor", kv.Value.col * k);
            }
        }

        /// <summary>Back to daylight values (editor: the shared assets must not keep a night glow after play mode).</summary>
        public static void ResetGlow() { UpdateGlow(0f); _glowLevel = -1f; _glow.Clear(); }
    }
}
