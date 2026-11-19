using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarWash.Dirt
{
    /// <summary>
    /// GPU dirt backend (TECH S04). Per-instance RTs: _DirtInit (start dirt + coverage), _CleanAcc (accumulated cleaning),
    /// _FoamMask. Strokes are batched (32 per draw) and rendered in UV2 space in LateUpdate; cleanliness is read back
    /// asynchronously from a 64x64 reduction every readbackInterval seconds (never full-res, never sync).
    /// </summary>
    public class DirtPainter : MonoBehaviour, IDirtBackend
    {
        const int MaxBrushes = 32, EvalSize = 64;
        static readonly int IdBrushes = Shader.PropertyToID("_Brushes"), IdStr = Shader.PropertyToID("_BrushStr"), IdCount = Shader.PropertyToID("_BrushCount");
        static readonly int IdFlip = Shader.PropertyToID("_FlipY");
        static readonly int IdInit = Shader.PropertyToID("_DirtInit"), IdClean = Shader.PropertyToID("_CleanAcc"), IdPrev = Shader.PropertyToID("_CleanPrev"), IdFoam = Shader.PropertyToID("_FoamMask");

        [SerializeField] private Shader _paintShader;
        [SerializeField] private Shader _evalShader;
        [SerializeField] private MeshFilter _body;
        [SerializeField] private Renderer _renderer;
        [Tooltip("M4.11: Body_LOD1 / Body_LOD2 — the same UV2 layout, so they sample the same dirt / clean textures")]
        [SerializeField] private Renderer[] _lodRenderers = new Renderer[0];
        [SerializeField] private Color _baseColor = new Color(0.15f, 0.4f, 0.85f);
        [Tooltip("M4.8 F1: foam shell material (CarWash/FoamShell, GPU instanced) — drawn as N instanced shells of the body only while foam is on it")]
        [SerializeField] private Material _foamShell;
        public const int ShellCount = 6;
        readonly Matrix4x4[] _shellM = new Matrix4x4[ShellCount];
        bool _shellsOn;
        /// <summary>Foam is on this car right now (shells drawn, rinse runoff possible).</summary>
        public bool FoamActive => Time.time - _lastFoamTime < FoamDuration + 1f;
        public bool ShellsOn => _shellsOn;

        public float CleanPct { get; private set; } = 1f;
        public float MudPct { get; private set; }
        public float GreasePct { get; private set; }
        public float SpotsPct { get; private set; }
        public bool HasReadback { get; private set; }
        public bool GreaseNeedsFoam { get; set; } = true;
        public float SpotsFraction { get; set; } = 0.4f;
        public Vector3 Weights { get; set; } = new Vector3(0.5f, 0.3f, 0.2f);
        public float ReadbackInterval { get; set; } = 0.5f;
        public float FoamDuration { get; set; } = 20f;
        public bool Initialized => _clean != null;

        /// <summary>0 = automatic. Set ±1 to force (DirtTest experiments).</summary>
        public static float FlipOverride;
        /// <summary>Clip-space Y sign so that the texel written for UV2 v is the texel sampled at v.</summary>
        public static float FlipSign => FlipOverride != 0f ? FlipOverride : (SystemInfo.graphicsUVStartsAtTop ? -1f : 1f);
        public Mesh Mesh => _body.sharedMesh;
        /// <summary>Dev checks only (texel readback): starting dirt (r mud, g grease, a coverage) and accumulated cleaning (r, g, b wet, a dried).</summary>
        public RenderTexture DevInitRT => _init;
        public RenderTexture DevCleanRT => _clean;

        RenderTexture _init, _clean, _prev, _foam, _eval;
        Material _paintMat, _evalMat;
        CommandBuffer _cmd;
        MaterialPropertyBlock _rendererMpb;
        readonly List<MaterialPropertyBlock> _mpbPool = new List<MaterialPropertyBlock>();
        readonly List<DirtStroke> _pending = new List<DirtStroke>(256);
        readonly Vector4[] _pos = new Vector4[MaxBrushes], _str = new Vector4[MaxBrushes];
        float _readbackT, _lastFoamTime = -999f;
        bool _readbackInFlight;

        /// <summary>Allocates textures and renders the starting dirt. mud/grease 0..1 (VehicleData.dirt).</summary>
        public void Init(int size, float mud, float grease, float seed)
        {
            Release();
            _init = NewRT(size, RenderTextureFormat.ARGB32, "DirtInit");
            _clean = NewRT(size, RenderTextureFormat.ARGBHalf, "CleanAcc");
            _prev = NewRT(size, RenderTextureFormat.ARGBHalf, "CleanPrev");
            _foam = NewRT(Mathf.Min(size, 256), RenderTextureFormat.RHalf, "Foam");
            _eval = NewRT(EvalSize, RenderTextureFormat.ARGBFloat, "DirtEval");
            _eval.filterMode = FilterMode.Point;
            _paintMat = new Material(_paintShader) { hideFlags = HideFlags.HideAndDontSave };
            _evalMat = new Material(_evalShader) { hideFlags = HideFlags.HideAndDontSave };
            _paintMat.SetFloat("_Seed", seed);
            _paintMat.SetVector("_InitDirt", new Vector4(mud, grease, 0, 0));
            _cmd = new CommandBuffer { name = "DirtPainter" };

            _cmd.SetRenderTarget(_clean); _cmd.ClearRenderTarget(false, true, Color.clear);
            _cmd.SetRenderTarget(_prev); _cmd.ClearRenderTarget(false, true, Color.clear);
            _cmd.SetRenderTarget(_foam); _cmd.ClearRenderTarget(false, true, Color.clear);
            _cmd.SetRenderTarget(_init); _cmd.ClearRenderTarget(false, true, Color.clear);
                _cmd.SetGlobalFloat(IdFlip, FlipSign);
            DrawUnwrap(3, null);
            Graphics.ExecuteCommandBuffer(_cmd);
            _cmd.Clear();

            _rendererMpb = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_rendererMpb);
            _rendererMpb.SetTexture(IdInit, _init);
            _rendererMpb.SetTexture(IdClean, _clean);
            _rendererMpb.SetTexture(IdFoam, _foam);
            _rendererMpb.SetColor("_BaseColor", _baseColor);
            ApplyBlock();
            HasReadback = false;
            RequestReadback();
        }

        public void SetBaseColor(Color c)
        {
            _baseColor = c;
            if (_rendererMpb == null) return;
            _rendererMpb.SetColor("_BaseColor", c);
            ApplyBlock();
        }

        void ApplyBlock()
        {
            _renderer.SetPropertyBlock(_rendererMpb);
            if (_lodRenderers != null) foreach (var r in _lodRenderers) if (r != null) r.SetPropertyBlock(_rendererMpb);
        }

        RenderTexture NewRT(int size, RenderTextureFormat f, string n)
        {
            var rt = new RenderTexture(size, size, 0, f) { name = n, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false };
            rt.Create();
            return rt;
        }

        public void AddStroke(in DirtStroke s) { if (_pending.Count < 4096) _pending.Add(s); }

        public void AddWorldStroke(Vector3 worldPos, float worldRadius, Vector4 strength, StrokeType type)
        {
            var t = _body.transform;
            AddStroke(new DirtStroke(t.InverseTransformPoint(worldPos), worldRadius / Mathf.Max(0.0001f, t.lossyScale.x), strength, type));
        }

        void LateUpdate()
        {
            if (_clean == null) return;
            bool any = _pending.Count > 0;
            bool foamActive = Time.time - _lastFoamTime < FoamDuration + 1f;
            SetShells(foamActive);
            if (any || foamActive)
            {
                _cmd.SetGlobalFloat(IdFlip, FlipSign);
                if (foamActive)
                {
                    _cmd.SetRenderTarget(_foam);
                    var mpb = Mpb(0); mpb.SetFloat("_FadeAmount", Time.deltaTime / Mathf.Max(0.1f, FoamDuration));
                    DrawUnwrap(4, mpb);
                }
                int mpbIndex = 1;
                for (int type = 0; type < 3 && any; type++) mpbIndex = FlushType((StrokeType)type, mpbIndex);
                Graphics.ExecuteCommandBuffer(_cmd);
                _cmd.Clear();
                _pending.Clear();
            }
            _readbackT += Time.deltaTime;
            if (_readbackT >= ReadbackInterval) { _readbackT = 0f; RequestReadback(); }
        }

        int FlushType(StrokeType type, int mpbIndex)
        {
            int n = 0;
            for (int i = 0; i <= _pending.Count; i++)
            {
                bool end = i == _pending.Count;
                if (!end && _pending[i].type == type)
                {
                    var s = _pending[i];
                    _pos[n] = new Vector4(s.pos.x, s.pos.y, s.pos.z, s.radius);
                    _str[n] = s.strength;
                    n++;
                    if (type == StrokeType.Foam) _lastFoamTime = Time.time;
                }
                if (n == MaxBrushes || (end && n > 0))
                {
                    var mpb = Mpb(mpbIndex++);
                    mpb.SetVectorArray(IdBrushes, _pos);
                    mpb.SetVectorArray(IdStr, _str);
                    mpb.SetInt(IdCount, n);
                    if (type == StrokeType.Clean)
                    {
                        _cmd.CopyTexture(_clean, _prev);
                        mpb.SetTexture(IdInit, _init); mpb.SetTexture(IdPrev, _prev); mpb.SetTexture(IdFoam, _foam);
                        mpb.SetFloat("_GreaseNeedsFoam", GreaseNeedsFoam ? 1f : 0f);
                        mpb.SetFloat("_SpotsFraction", SpotsFraction);
                        _cmd.SetRenderTarget(_clean);
                        DrawUnwrap(0, mpb);
                        _cmd.SetRenderTarget(_foam);                // M4.8 F2: the same water strokes rinse the foam
                        DrawUnwrap(5, mpb);
                    }
                    else if (type == StrokeType.ReDirt) { _cmd.SetRenderTarget(_clean); DrawUnwrap(1, mpb); }
                    else { _cmd.SetRenderTarget(_foam); DrawUnwrap(2, mpb); }
                    n = 0;
                }
            }
            return mpbIndex;
        }

        /// <summary>M4.8 F1: draw the foam shells only while there is foam (no cost on a dry car).</summary>
        void SetShells(bool on)
        {
            // M4.8 F1: one instanced draw of ShellCount copies of the body (the shader takes the shell index from the instance
            // id). Extra materials on the body renderer itself made the GPU Resident Drawer skip the whole car.
            _shellsOn = on && _foamShell != null && _renderer != null && _renderer.isVisible;
            if (!_shellsOn) return;
            var m = _body.transform.localToWorldMatrix;
            for (int i = 0; i < ShellCount; i++) _shellM[i] = m;
            var b = _renderer.bounds; b.Expand(0.4f);
            var rp = new RenderParams(_foamShell) { matProps = _rendererMpb, worldBounds = b, layer = _renderer.gameObject.layer, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true };
            Graphics.RenderMeshInstanced(rp, _body.sharedMesh, 0, _shellM, ShellCount);
        }

        MaterialPropertyBlock Mpb(int i)
        {
            while (_mpbPool.Count <= i) _mpbPool.Add(new MaterialPropertyBlock());
            _mpbPool[i].Clear();
            return _mpbPool[i];
        }

        void DrawUnwrap(int pass, MaterialPropertyBlock mpb)
        {
            var mesh = _body.sharedMesh;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) _cmd.DrawMesh(mesh, Matrix4x4.identity, _paintMat, sub, pass, mpb);
        }

        void RequestReadback()
        {
            if (_readbackInFlight || _eval == null || !SystemInfo.supportsAsyncGPUReadback) return;
            _evalMat.SetTexture(IdInit, _init);
            _evalMat.SetTexture(IdClean, _clean);
            _evalMat.SetVector("_SrcTexel", new Vector4(1f / _init.width, 0, 0, 0));
            _evalMat.SetInt("_Block", Mathf.Max(1, _init.width / EvalSize));
            _evalMat.SetVector("_Weights", Weights);
            Graphics.Blit(null, _eval, _evalMat);
            _readbackInFlight = true;
            AsyncGPUReadback.Request(_eval, 0, TextureFormat.RGBAFloat, OnReadback);
        }

        void OnReadback(AsyncGPUReadbackRequest req)
        {
            _readbackInFlight = false;
            if (req.hasError || this == null) return;
            NativeArray<Vector4> data = req.GetData<Vector4>();
            double dirt = 0, cov = 0, mud = 0, grease = 0;
            for (int i = 0; i < data.Length; i++) { var v = data[i]; dirt += v.x; cov += v.y; mud += v.z; grease += v.w; }
            if (cov <= 0) return;
            CleanPct = Mathf.Clamp01(1f - (float)(dirt / cov));
            MudPct = (float)(mud / cov);
            GreasePct = (float)(grease / cov);
            SpotsPct = Weights.z > 0f ? Mathf.Max(0f, ((float)(dirt / cov) - Weights.x * MudPct - Weights.y * GreasePct) / Weights.z) : 0f;
            HasReadback = true;
        }

        public byte[] Serialize() => null;       // T4.3 late-join snapshot
        public void Deserialize(byte[] data) { } // T4.3

        void Release()
        {
            foreach (var rt in new[] { _init, _clean, _prev, _foam, _eval }) if (rt != null) { rt.Release(); Kill(rt); }
            _init = _clean = _prev = _foam = _eval = null;
            if (_paintMat) Kill(_paintMat);
            if (_evalMat) Kill(_evalMat);
            _cmd?.Release(); _cmd = null;
        }

        void OnDestroy() => Release();

        static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
