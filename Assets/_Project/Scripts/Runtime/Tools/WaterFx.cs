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

        class Patch { public DecalProjector d; public Vector3 p; public float wet, soak; public bool puddle; }

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
            Get(wet, puddle).Surface(p, n, dt);
        }

        void Surface(Vector3 p, Vector3 n, float dt)
        {
            Patch best = null; float bd = 0.7f * 0.7f;
            foreach (var x in _patches) { float d = (x.p - p).sqrMagnitude; if (d < bd) { bd = d; best = x; } }
            if (best == null)
            {
                if (_patches.Count >= MaxPatches) { var old = _patches[0]; foreach (var x in _patches) if (x.wet < old.wet) old = x; Remove(old); }
                var go = new GameObject("WetPatch");
                go.transform.SetParent(transform, false);
                var d = go.AddComponent<DecalProjector>();
                d.material = _wet;
                d.size = new Vector3(1.1f, 1.1f, 0.6f);
                d.pivot = Vector3.zero;
                go.transform.SetPositionAndRotation(p + n * 0.2f, Quaternion.LookRotation(-n) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
                best = new Patch { d = d, p = p };
                _patches.Add(best);
            }
            best.wet = 1f;
            best.soak += dt;
            if (!best.puddle && best.soak > PuddleAfter && n.y > 0.7f && _puddle != null)
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
