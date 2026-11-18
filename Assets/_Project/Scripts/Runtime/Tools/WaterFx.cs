using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarWash.Tools
{
    /// <summary>
    /// M4.6 D7: water feedback, purely cosmetic and local to every peer (each peer already computes its own spray impact in
    /// SprayVfx, so nothing new goes over the network):
    ///  - static floors / walls: a wet decal (darker, flattened normal) at the impact that dries over ~18 s; a floor spot that
    ///    keeps getting water for 1.5 s turns into a bigger puddle decal that dries over ~45 s;
    ///  - loose props / tools: WetObject darkens them and raises their smoothness, drying over ~15 s.
    /// Cars (own dirt shader), critters (own wet tint) and players are left alone. Light props are already pushed by the
    /// stream on the host (SprayTool.OnRayHit force).
    /// </summary>
    public class WaterFx : MonoBehaviour
    {
        public const float PatchDry = 18f, PuddleDry = 45f, PuddleAfter = 1.5f;
        const int MaxPatches = 48;

        class Patch { public DecalProjector d; public Vector3 p; public float wet, soak; public bool puddle; public Material mat; }

        static WaterFx _i;
        readonly List<Patch> _patches = new List<Patch>();
        Material _wet, _puddle;

        public static int ActivePatches => _i != null ? _i._patches.Count : 0;
        public static int ActivePuddles { get { int n = 0; if (_i != null) foreach (var p in _i._patches) if (p.puddle) n++; return n; } }

        static WaterFx Get(Material wet, Material puddle)
        {
            if (_i == null) _i = new GameObject("WaterFx").AddComponent<WaterFx>();
            if (_i._wet == null) _i._wet = wet;
            if (_i._puddle == null) _i._puddle = puddle;
            return _i;
        }

        /// <summary>Every peer, from SprayVfx: water lands on `c` at `p` (normal `n`) for `dt` seconds.</summary>
        public static void Hit(Collider c, Vector3 p, Vector3 n, float dt, Material wet, Material puddle)
        {
            if (c == null) return;
            int l = c.gameObject.layer;
            if (l == Core.Layers.Vehicle || l == Core.Layers.WashSurface || l == Core.Layers.Critter || l == Core.Layers.Player || l == Core.Layers.Ragdoll) return;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic || l == Core.Layers.Prop || l == Core.Layers.Tool)
            {
                var root = c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.gameObject;
                if (!root.TryGetComponent<WetObject>(out var w)) w = root.AddComponent<WetObject>();
                w.Soak(dt);
                return;
            }
            if (wet == null) return;
            Get(wet, puddle).Surface(p, n, dt, wet); // M4.8: the patch keeps the material it was made with (water / floor foam)
        }

        void Surface(Vector3 p, Vector3 n, float dt, Material mat)
        {
            Patch best = null; float bd = 0.7f * 0.7f;
            foreach (var x in _patches) { if (x.mat != mat) continue; float d = (x.p - p).sqrMagnitude; if (d < bd) { bd = d; best = x; } }
            if (best == null)
            {
                if (_patches.Count >= MaxPatches) { var old = _patches[0]; foreach (var x in _patches) if (x.wet < old.wet) old = x; Remove(old); }
                var go = new GameObject("WetPatch");
                go.transform.SetParent(transform, false);
                var d = go.AddComponent<DecalProjector>();
                d.material = mat;
                d.size = new Vector3(1.1f, 1.1f, 0.6f);
                d.pivot = Vector3.zero;
                go.transform.SetPositionAndRotation(p + n * 0.2f, Quaternion.LookRotation(-n) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
                best = new Patch { d = d, p = p, mat = mat };
                _patches.Add(best);
            }
            best.wet = 1f;
            best.soak += dt;
            if (!best.puddle && mat == _wet && best.soak > PuddleAfter && n.y > 0.7f && _puddle != null)
            {
                best.puddle = true;
                best.d.material = _puddle;
                best.d.size = new Vector3(2.2f, 2.2f, 0.6f);
            }
        }

        void Remove(Patch p) { _patches.Remove(p); if (p.d != null) Destroy(p.d.gameObject); }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int k = _patches.Count - 1; k >= 0; k--)
            {
                var p = _patches[k];
                p.wet -= dt / (p.puddle ? PuddleDry : PatchDry);
                p.soak = Mathf.Max(0f, p.soak - dt * 0.1f);
                if (p.wet <= 0f || p.d == null) { Remove(p); continue; }
                p.d.fadeFactor = Mathf.SmoothStep(0f, 1f, p.wet);
            }
        }
    }

    /// <summary>M4.6 D7: a prop / tool that got wet — darker and glossier for a while (MaterialPropertyBlock, local).</summary>
    public class WetObject : MonoBehaviour
    {
        public const float DryTime = 15f;
        Renderer[] _r;
        Color[][] _base;
        float[][] _smooth;
        MaterialPropertyBlock _mpb;
        public float Wetness { get; private set; }

        void Awake()
        {
            _r = GetComponentsInChildren<Renderer>();
            _base = new Color[_r.Length][]; _smooth = new float[_r.Length][];
            for (int i = 0; i < _r.Length; i++)
            {
                var mats = _r[i].sharedMaterials;
                _base[i] = new Color[mats.Length]; _smooth[i] = new float[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    _base[i][m] = mats[m] != null && mats[m].HasProperty("_BaseColor") ? mats[m].GetColor("_BaseColor") : Color.white;
                    _smooth[i][m] = mats[m] != null && mats[m].HasProperty("_Smoothness") ? mats[m].GetFloat("_Smoothness") : 0.3f;
                }
            }
            _mpb = new MaterialPropertyBlock();
        }

        public void Soak(float dt) { Wetness = Mathf.Min(1f, Wetness + dt * 3f); enabled = true; }

        void Update()
        {
            Wetness -= Time.deltaTime / DryTime;
            if (Wetness <= 0f) { Wetness = 0f; Apply(); enabled = false; return; }
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < _r.Length; i++)
            {
                if (_r[i] == null) continue;
                for (int m = 0; m < _base[i].Length; m++)
                {
                    _r[i].GetPropertyBlock(_mpb, m);
                    var c = _base[i][m]; var dark = c * Mathf.Lerp(1f, 0.62f, Wetness); dark.a = c.a;
                    _mpb.SetColor("_BaseColor", dark);
                    _mpb.SetFloat("_Smoothness", Mathf.Lerp(_smooth[i][m], 0.92f, Wetness));
                    _r[i].SetPropertyBlock(_mpb, m);
                }
            }
        }
    }
}

namespace CarWash.Tools
{
    /// <summary>
    /// M4.8 F1 / F2 foam visuals, local on every peer (each peer computes its own spray impact, nothing networked):
    ///  - Clump: the foam cannon leaves a few sticky white blobs on the car that creep down and fade;
    ///  - Runoff: water hitting a foamed car rinses foam off — drips fall to the floor and a white foam patch (decal) spreads
    ///    there, fading like the wet patches (it shares WaterFx's patch list, so it merges with the water on the floor).
    /// </summary>
    public static class FoamFx
    {
        static ParticleSystem _blobs, _drips;
        static Material _blobMat; // the foam cannon's splash material (always seen first: foam must be on the car)
        static float _patchT;
        public static int Clumps { get; private set; }
        public static int Runoffs { get; private set; }

        static ParticleSystem Make(string name, Material mat, float gravity, float life, float sizeMin, float sizeMax, int max)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.startLifetime = life; main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new Color(0.97f, 0.98f, 1f, 0.95f); main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.startSpeed = 0f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>Foam cannon hit a car at p (normal n).</summary>
        public static void Clump(Vector3 p, Vector3 n, Material mat)
        {
            if (mat == null) return;
            _blobMat = mat;
            if (_blobs == null) _blobs = Make("FoamBlobs", mat, 0.012f, 7f, 0.07f, 0.17f, 400);
            for (int i = 0; i < 2; i++)
            {
                var ep = new ParticleSystem.EmitParams { position = p + n * 0.04f + Random.insideUnitSphere * 0.08f, applyShapeToPosition = false, velocity = Vector3.down * Random.Range(0.01f, 0.05f) };
                _blobs.Emit(ep, 1);
            }
            Clumps++;
        }

        /// <summary>Water hit a foamed car at p: foam drips off and lands as a floor patch.</summary>
        public static void Runoff(Vector3 p, Vector3 n, Material mat, Material floorFoam)
        {
            if (_blobMat != null) mat = _blobMat;
            if (mat == null) return;
            if (_drips == null) _drips = Make("FoamDrips", mat, 0.55f, 1.4f, 0.06f, 0.13f, 300);
            var ep = new ParticleSystem.EmitParams { position = p + n * 0.05f, applyShapeToPosition = false, velocity = n * 0.4f + Random.insideUnitSphere * 0.3f };
            _drips.Emit(ep, 2);
            Runoffs++;
            _patchT -= Time.deltaTime;
            if (_patchT > 0f || floorFoam == null) return;
            _patchT = 0.35f;
            var below = p + n * 0.6f; below.y += 0.3f;
            if (Physics.Raycast(below, Vector3.down, out var hit, 4f, Core.Layers.GroundMask & ~(1 << Core.Layers.Vehicle), QueryTriggerInteraction.Ignore))
                WaterFx.Hit(hit.collider, hit.point, hit.normal, 0.3f, floorFoam, null);
        }
    }
}
