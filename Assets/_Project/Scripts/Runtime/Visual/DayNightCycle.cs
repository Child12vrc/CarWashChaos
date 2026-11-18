using CarWash.Core;
using CarWash.Day;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarWash.Visual
{
    /// <summary>
    /// M4.9 G1 + G2: the business day runs morning → noon → sunset → night. Purely local on every peer: the clock comes from
    /// DayManager's already-synced DayStart / DayLength (no extra traffic), so everybody sees the same sky.
    /// Drives the sun (→ a dim blue moon light after dusk), the CarWash/DaySky skybox (gradient, sun / moon, stars, clouds),
    /// ambient light (SH probe), reflections, fog and two runtime post volumes (dusk warm / night cool) and exposes
    /// <see cref="Night"/> (0 day … 1 night) for lamps, signs, headlights and the bay lights.
    /// Clock: lobby 08:00, the open day 08:00 → 20:30 (night falls in the last fifth), overtime → 22:00, summary = night.
    /// Dev: <c>-time HH.H</c> pins the clock (screenshots).
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private Light _sun;
        [SerializeField] private Material _skyAsset;
        [SerializeField] private Material[] _far = System.Array.Empty<Material>(); // unlit distant city / skyline: darkened at night (restored on destroy)
        [SerializeField] private Material[] _glowMats = System.Array.Empty<Material>();
        [SerializeField] private Color[] _glowCols = System.Array.Empty<Color>();
        [SerializeField] private float[] _glowOnAt = System.Array.Empty<float>();

        public const float OpenHour = 8f, CloseHour = 20.5f, OvertimeEndHour = 22f;

        /// <summary>0 = full day … 1 = full night (lamps etc. switch on from ≈0.35).</summary>
        public static float Night { get; private set; }
        /// <summary>Clock hour (8.0 … 22.0) for the HUD.</summary>
        public static float Hour { get; private set; } = OpenHour;
        public static DayNightCycle Instance { get; private set; }

        Material _sky;
        Color[] _farDay;
        Volume _duskVol, _nightVol;
        float _hour = OpenHour, _forced = -1f, _shTimer;
        Quaternion _sunDay;
        float _sunIntensity = 1.35f;
        Color _sunColor;

        void Awake()
        {
            Instance = this;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++)
                if (a[i] == "-time" && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h)) _forced = h;
        }

        void Start()
        {
            if (_sun == null) _sun = RenderSettings.sun;
            if (_sun != null) { _sunIntensity = _sun.intensity; _sunColor = _sun.color; }
            _farDay = new Color[_far.Length];
            for (int i = 0; i < _far.Length; i++) if (_far[i] != null) _farDay[i] = _far[i].GetColor("_BaseColor");
            if (_skyAsset != null) { _sky = new Material(_skyAsset) { name = "DaySky (runtime)" }; RenderSettings.skybox = _sky; }
            RenderSettings.ambientMode = AmbientMode.Custom;
            _duskVol = MakeVolume("Dusk", new Color(1f, 0.86f, 0.74f), 18f, 4f, 0f);
            _nightVol = MakeVolume("Night", new Color(0.72f, 0.8f, 1f), -14f, -18f, 0.15f);
            for (int i = 0; i < _glowMats.Length; i++)
                NightLight.Glow(_glowMats[i], i < _glowCols.Length ? _glowCols[i] : Color.white, i < _glowOnAt.Length ? _glowOnAt[i] : 0.35f);
            _hour = TargetHour();
            Apply(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Night = 0f; Hour = OpenHour;
            NightLight.ResetGlow();
            if (_skyAsset != null) RenderSettings.skybox = _skyAsset;
            for (int i = 0; _farDay != null && i < _far.Length; i++) if (_far[i] != null) _far[i].SetColor("_BaseColor", _farDay[i]);
        }

        Volume MakeVolume(string n, Color filter, float temp, float sat, float exposure)
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = p.Add<ColorAdjustments>(true); ca.colorFilter.Override(filter); ca.saturation.Override(sat); ca.postExposure.Override(exposure);
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(temp);
            var go = new GameObject("Post_" + n); go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>(); v.isGlobal = true; v.priority = 1f; v.weight = 0f; v.profile = p;
            return v;
        }

        float TargetHour()
        {
            if (_forced >= 0f) return _forced;
            var dm = DayManager.Instance;
            if (dm == null || !dm.IsSpawned) return OpenHour;
            switch (dm.State.Value)
            {
                case GameState.Day:
                    float len = Mathf.Max(1f, dm.DayLength), e = dm.Elapsed;
                    if (e <= len) return OpenHour + (CloseHour - OpenHour) * Mathf.Clamp01(e / len);
                    return Mathf.Min(OvertimeEndHour, CloseHour + (e - len) / 60f * 1.5f); // overtime: 1.5 h per minute
                case GameState.Summary: case GameState.Shop: case GameState.WeekEnd: case GameState.GameOver:
                    return Mathf.Max(_hour, 21f);
                default: return OpenHour;
            }
        }

        void Update()
        {
            float target = TargetHour();
            // the clock only jumps at day changes (summary → next morning): sweep there quickly instead of popping
            _hour = Mathf.Abs(target - _hour) > 0.25f ? Mathf.MoveTowards(_hour, target, Time.deltaTime * 6f) : target;
            Apply(false);
            if (_forced >= 0f && !_logged && Time.timeSinceLevelLoad > 6f)
            {
                _logged = true;
                int bays = 0; foreach (var s in FindObjectsByType<Day.BayLightSwitch>(FindObjectsSortMode.None)) if (s.On.Value) bays++;
                Debug.Log($"[DAYNIGHT] hour {Hour:0.0} night {Night:0.00} sun {(_sun != null ? _sun.intensity : 0f):0.00} lit night-lights {NightLight.LitCount}/{NightLight.All.Count} bay switches on {bays} fog {RenderSettings.fogColor}");
            }
        }
        bool _logged;

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        void Apply(bool force)
        {
            float h = _hour;
            Hour = h;
            // sun: rises 6:00, sets 19:30 (a summer day); elevation in degrees
            float dayT = Mathf.InverseLerp(6f, 19.5f, h);
            float elev = Mathf.Sin(dayT * Mathf.PI) * 62f;
            if (h < 6f || h > 19.5f) elev = -10f;
            float az = Mathf.Lerp(-110f, 110f, dayT) - 35f;
            float night = 1f - Smooth(-6f, 6f, elev);                     // civil twilight band around the horizon
            float dusk = Smooth(25f, 6f, elev) * (1f - Smooth(-2f, -8f, elev)) * (h > 12f ? 1f : 0.6f);
            Night = night;
            NightLight.UpdateGlow(night);

            var sunDir = Quaternion.Euler(-elev, az, 0f) * Vector3.forward;       // direction TO the sun (east → south → west)
            var moonDir = new Vector3(-0.45f, 0.55f, -0.7f).normalized;

            // colours
            var noonZen = new Color(0.22f, 0.45f, 0.82f); var noonHor = new Color(0.7f, 0.8f, 0.92f);
            var duskZen = new Color(0.25f, 0.3f, 0.55f); var duskHor = new Color(1f, 0.6f, 0.38f);
            var nightZen = new Color(0.015f, 0.025f, 0.06f); var nightHor = new Color(0.06f, 0.08f, 0.15f);
            Color zen = Color.Lerp(Color.Lerp(noonZen, duskZen, dusk), nightZen, night);
            Color hor = Color.Lerp(Color.Lerp(noonHor, duskHor, dusk), nightHor, night);
            Color sunCol = Color.Lerp(_sunColor.a > 0 ? _sunColor : new Color(1f, 0.96f, 0.88f), new Color(1f, 0.62f, 0.36f), dusk);

            if (_sun != null)
            {
                if (elev > -2f)
                {
                    _sun.transform.rotation = Quaternion.LookRotation(-sunDir);
                    _sun.color = sunCol;
                    _sun.intensity = _sunIntensity * Smooth(-2f, 14f, elev);
                }
                else
                {   // moonlight: soft blue, keeps shapes readable
                    _sun.transform.rotation = Quaternion.LookRotation(-moonDir);
                    _sun.color = new Color(0.55f, 0.66f, 1f);
                    _sun.intensity = 0.22f * Smooth(-2f, -8f, elev);
                }
                _sun.shadowStrength = Mathf.Lerp(0.85f, 0.5f, night);
            }

            if (_sky != null)
            {
                _sky.SetColor("_Zenith", zen); _sky.SetColor("_Horizon", hor);
                _sky.SetColor("_Ground", Color.Lerp(new Color(0.4f, 0.42f, 0.45f), new Color(0.03f, 0.035f, 0.05f), night));
                _sky.SetColor("_SunColor", sunCol * (1f - night));
                _sky.SetVector("_SunDir", sunDir); _sky.SetVector("_MoonDir", moonDir);
                _sky.SetFloat("_Stars", Smooth(0.55f, 0.95f, night));
                _sky.SetFloat("_Moon", Smooth(0.4f, 0.9f, night));
                _sky.SetColor("_CloudLit", Color.Lerp(Color.Lerp(new Color(1f, 1f, 1f), new Color(1f, 0.72f, 0.55f), dusk), new Color(0.12f, 0.14f, 0.2f), night));
                _sky.SetColor("_CloudShade", Color.Lerp(Color.Lerp(new Color(0.62f, 0.68f, 0.78f), new Color(0.5f, 0.38f, 0.5f), dusk), new Color(0.04f, 0.05f, 0.08f), night));
            }

            RenderSettings.fogColor = Color.Lerp(Color.Lerp(new Color(0.72f, 0.8f, 0.9f), new Color(0.85f, 0.62f, 0.5f), dusk), new Color(0.05f, 0.065f, 0.1f), night);
            for (int i = 0; _farDay != null && i < _far.Length; i++)
                if (_far[i] != null) _far[i].SetColor("_BaseColor", Color.Lerp(Color.Lerp(_farDay[i], _farDay[i] * new Color(1f, 0.78f, 0.68f), dusk), _farDay[i] * new Color(0.1f, 0.12f, 0.18f), night));
            RenderSettings.reflectionIntensity = Mathf.Lerp(1f, 0.2f, night);
            if (_duskVol != null) _duskVol.weight = dusk;
            if (_nightVol != null) _nightVol.weight = night;

            // ambient (SH) ~4×/s — cheap, but not every frame
            _shTimer -= Time.deltaTime;
            if (force || _shTimer <= 0f)
            {
                _shTimer = 0.25f;
                Color sky = Color.Lerp(Color.Lerp(new Color(0.6f, 0.68f, 0.8f), new Color(0.62f, 0.5f, 0.5f), dusk), new Color(0.1f, 0.12f, 0.2f), night);
                Color eq = Color.Lerp(Color.Lerp(new Color(0.55f, 0.57f, 0.6f), new Color(0.6f, 0.45f, 0.38f), dusk), new Color(0.07f, 0.08f, 0.12f), night);
                Color gr = Color.Lerp(new Color(0.33f, 0.32f, 0.3f), new Color(0.04f, 0.04f, 0.05f), night);
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight(eq);
                sh.AddDirectionalLight(Vector3.up, (sky - eq) * 1.2f, 1f);
                sh.AddDirectionalLight(Vector3.down, (gr - eq) * 1.2f, 1f);
                RenderSettings.ambientProbe = sh;
            }
        }
    }
}
