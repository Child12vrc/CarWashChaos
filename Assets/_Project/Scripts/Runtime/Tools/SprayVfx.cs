using CarWash.Core;
using CarWash.Player;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// Local-only spray visuals + audio (TECH S05 ParticleSystem path). Three systems built in code:
    /// stream (from nozzle), mist (soft puffs at nozzle + impact), splash (impact; optional 4x4 flipbook).
    /// Textures/tints come from docs/ART_IMPORT.md via materials assigned by ToolBuilder.
    /// </summary>
    public class SprayVfx : MonoBehaviour
    {
        [SerializeField] private Material _streamMaterial, _mistMaterial, _splashMaterial;
        [SerializeField] private Color _streamColor = new Color(0.62f, 0.87f, 0.98f, 0.9f);
        [SerializeField] private Color _mistColor = new Color(0.9f, 0.96f, 1f, 0.35f);
        [SerializeField] private Color _splashColor = Color.white;
        [SerializeField] private bool _splashIsSheet = true;
        [SerializeField] private float _streamSize = 0.09f, _mistSize = 0.45f, _splashSize = 0.7f;
        [SerializeField] private float _speed = 22f;
        [SerializeField] private float _pitch = 1f;

        [Tooltip("M4.6 D7: wet patch / puddle decals where the water lands (null = no wetting, e.g. the foam cannon)")]
        [SerializeField] private Material _wetDecal, _puddleDecal;
        ParticleSystem _stream, _mist, _splash;
        AudioSource _audio;
        bool _on;
        float _splashT, _foamFxT;
        [Tooltip("M4.8 F2: floor foam decal where rinsed-off foam lands (washer)")]
        [SerializeField] private Material _foamDecal;
        bool? _isFoamCannon;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        void Awake()
        {
            _stream = Make("Stream", _streamMaterial, _streamColor, _streamSize, 0.35f, _speed, 260f, ParticleSystemShapeType.Cone, 0.4f);
            _mist = Make("Mist", _mistMaterial, _mistColor, _mistSize, 0.8f, 1.2f, 18f, ParticleSystemShapeType.Cone, -0.05f);
            _splash = Make("Splash", _splashMaterial, _splashColor, _splashSize, 0.45f, 0.6f, 0f, ParticleSystemShapeType.Hemisphere, 0f);
            if (_splashIsSheet)
            {
                var ts = _splash.textureSheetAnimation;
                ts.enabled = true; ts.numTilesX = 4; ts.numTilesY = 4;
                ts.animation = ParticleSystemAnimationType.WholeSheet;
                ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            var mistSize = _mist.sizeOverLifetime; mistSize.enabled = true;
            mistSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = NoiseClip();
            _audio.loop = true; _audio.spatialBlend = 1f; _audio.volume = 0.35f; _audio.maxDistance = 25f; _audio.pitch = _pitch;
            _audio.rolloffMode = AudioRolloffMode.Linear;
        }

        ParticleSystem Make(string name, Material mat, Color color, float size, float life, float speed, float rate, ParticleSystemShapeType shapeType, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = life; main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
            main.startColor = color; main.startSpeed = speed; main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity; main.maxParticles = 1200; main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = shapeType; shape.angle = 6f; shape.radius = 0.02f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (mat) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        static AudioClip _noise;
        static AudioClip NoiseClip()
        {
            if (_noise != null) return _noise;
            int rate = 22050, len = rate; var data = new float[len];
            var rng = new System.Random(3); float lp = 0f;
            for (int i = 0; i < len; i++) { float w = (float)(rng.NextDouble() * 2 - 1); lp += (w - lp) * 0.35f; data[i] = lp * 0.8f; }
            _noise = AudioClip.Create("spray_noise", len, 1, rate, false);
            _noise.SetData(data, 0);
            return _noise;
        }

        public void SetActive(bool on, Transform nozzle, float range, float coneDeg, PlayerNet holder)
        {
            if (on != _on)
            {
                _on = on;
                if (on) { _stream.Play(); _mist.Play(); _audio.Play(); } else { _stream.Stop(); _mist.Stop(); _audio.Stop(); }
            }
            if (!on) return;
            var shape = _stream.shape; shape.angle = coneDeg * 0.5f;
            var main = _stream.main; main.startLifetime = range / _speed;

            // cosmetic impact where the stream lands (every peer computes its own)
            var dir = holder != null ? holder.Look.Head.forward : nozzle.forward;
            var origin = holder != null ? holder.Look.Head.position : nozzle.position;
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, range, Layers.SprayMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 p = Vector3.zero, normal = Vector3.up; Collider hitCol = null;
            for (int i = 0; i < n; i++)
            {
                if (holder != null && _hits[i].collider.transform.IsChildOf(holder.transform)) continue;
                if (Vehicles.VehicleHull.Is(_hits[i].collider)) continue; // splash on the exact GLB body
                if (_hits[i].distance < best) { best = _hits[i].distance; p = _hits[i].point; normal = _hits[i].normal; hitCol = _hits[i].collider; }
            }
            // GLB: the stream leaves Socket_Nozzle and converges on the aim point (crosshair hit), not parallel to the aim ray
            var aim = best == float.MaxValue ? origin + dir * range : p;
            var streamRot = (aim - nozzle.position).sqrMagnitude > 0.01f ? Quaternion.LookRotation(aim - nozzle.position) : nozzle.rotation;
            _stream.transform.SetPositionAndRotation(nozzle.position, streamRot);
            _mist.transform.SetPositionAndRotation(nozzle.position, streamRot);
            if (best == float.MaxValue) return;
            _splashT += Time.deltaTime;
            if (_splashT < 0.05f) return;
            _splashT = 0f;
            WaterFx.Hit(hitCol, p, normal, 0.05f, _wetDecal, _puddleDecal); // M4.6 D7 (washer only: foam has no wet decals)
            // M4.8 F1 / F2: foam clumps where the foam cannon lands on a car; foam runoff where water hits a foamed car
            _foamFxT -= 0.05f;
            var car = hitCol != null ? hitCol.GetComponentInParent<Dirt.DirtNetSync>() : null;
            if (car != null)
            {
                var drv = car.GetComponentInParent<Vehicles.CustomerDriver>(); // M4.8 F4: water in the driver's face → annoyed
                if (drv != null && drv.Head != null && (drv.Head.position - p).sqrMagnitude < 1f) drv.Splashed();
            }
            if (car != null && _foamFxT <= 0f)
            {
                _isFoamCannon ??= GetComponentInParent<FoamCannon>() != null;
                if (_isFoamCannon.Value) { FoamFx.Clump(p, normal, _splashMaterial); _foamFxT = 0.15f; }
                else if (car.Painter != null && car.Painter.FoamActive) { FoamFx.Runoff(p, normal, _splashMaterial, _foamDecal); _foamFxT = 0.1f; }
            }
            _splash.transform.SetPositionAndRotation(p + normal * 0.05f, Quaternion.LookRotation(normal));
            _splash.Emit(1);
            var ep = new ParticleSystem.EmitParams { position = p + normal * 0.1f, applyShapeToPosition = false, velocity = normal * 0.6f };
            _mist.Emit(ep, 1);
        }
    }
}
