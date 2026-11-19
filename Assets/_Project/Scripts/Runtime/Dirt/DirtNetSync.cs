using System.Collections.Generic;
using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Dirt
{
    /// <summary>
    /// TECH S04 Net: server owns the truth. Same seed → identical start dirt everywhere; server strokes are applied
    /// locally and batched to clients at strokeSendRate (10 Hz). cleanPct comes from the server's readback.
    /// Late-join: the server keeps the stroke history and replays it to a joining client (T4.3).
    /// </summary>
    public class DirtNetSync : NetworkBehaviour
    {
        [SerializeField] private DirtPainter _painter;
        [SerializeField] private DataRegistry _data;
        [Tooltip("Used when the spawner did not call ServerConfigure (test cars)")]
        [SerializeField] private Vector3 _defaultDirt = new Vector3(0.9f, 0f, 0f);

        public readonly NetworkVariable<float> Seed = new NetworkVariable<float>();
        public readonly NetworkVariable<float> CleanPct = new NetworkVariable<float>(1f);

        /// <summary>v0.9: from this clean fraction on the car counts (and shows) as 100 %. A faint film the body shader no longer
        /// shows (half-wiped towel edges) kept big cars like the V07 garbage truck at 99.6–99.8 % forever; the server snaps
        /// here, so auto-finish, the HUD, the payout and the dev bots all see the same 100 % (TASKS: follow-up on the film).</summary>
        public const float CompleteAt = 0.995f;
        public static float Snap(float clean) => clean >= CompleteAt ? 1f : clean;
        public readonly NetworkVariable<Vector3> StartDirt = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<int> TextureSize = new NetworkVariable<int>(512);

        public DirtPainter Painter => _painter;
        readonly List<DirtStroke> _outbox = new List<DirtStroke>(256);
        float _sendT;

        /// <summary>Server, before Spawn: choose dirt amounts.</summary>
        public void ServerConfigure(Vector3 dirt, int textureSize, float seed)
        {
            StartDirt.Value = dirt; TextureSize.Value = textureSize; Seed.Value = seed;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && Seed.Value == 0f)
            {
                Seed.Value = Random.Range(1f, 999f);
                if (StartDirt.Value == Vector3.zero) StartDirt.Value = _defaultDirt;
            }
            var d = StartDirt.Value;
            _painter.Init(TextureSize.Value, d.x, d.y, Seed.Value);
            var b = _data != null ? _data.balance.dirt : null;
            if (b != null)
            {
                _painter.Weights = b.weights; _painter.SpotsFraction = b.spotsFraction;
                _painter.FoamDuration = b.foamDuration; _painter.ReadbackInterval = b.readbackInterval;
            }
            if (!IsServer) RequestSnapshotRpc(); // everything washed/re-dirtied before we joined
        }

        /// <summary>Server: paint + queue for clients. objectPos is in the body's object space.</summary>
        public void ServerStroke(in DirtStroke stroke)
        {
            if (!IsServer) return;
            var s = stroke.Quantized(); // M4.7: paint exactly what the clients will decode
            _painter.AddStroke(s);
            if (_outbox.Count < 1024) _outbox.Add(s);
            if (_history.Count < MaxHistory) _history.Add((s, Time.time));
        }

        // ---------------- T4.3 late-join snapshot: the server keeps every stroke since spawn and replays them ----------------
        const int MaxHistory = 60000;   // ≈2.4 MB worst case; a fully washed XL car is ~10-20k strokes
        readonly List<(DirtStroke s, float t)> _history = new List<(DirtStroke, float)>();
        public int HistoryCount => _history.Count;
        /// <summary>Client: snapshot strokes received (dev checks).</summary>
        public int SnapshotReceived { get; private set; }
        public bool SnapshotDone { get; private set; }

        [Rpc(SendTo.Server)]
        void RequestSnapshotRpc(RpcParams p = default)
        {
            ulong to = p.Receive.SenderClientId;
            float foamLife = _data != null ? _data.balance.dirt.foamDuration : 20f;
            var list = new List<DirtStroke>(_history.Count);
            foreach (var (s, t) in _history)
                if (s.type != StrokeType.Foam || Time.time - t < foamLife) list.Add(s); // foam that already faded is skipped
            const int chunk = 64;
            int chunks = Mathf.Max(1, (list.Count + chunk - 1) / chunk);
            for (int c = 0; c < chunks; c++)
            {
                int n = Mathf.Min(chunk, list.Count - c * chunk);
                var arr = new DirtStroke[Mathf.Max(0, n)];
                if (n > 0) list.CopyTo(c * chunk, arr, 0, n);
                SnapshotRpc(arr, c == chunks - 1, RpcTarget.Single(to, RpcTargetUse.Temp));
            }
            if (list.Count > 0) Debug.Log($"[DIRT] snapshot for client {to}: {list.Count} strokes in {chunks} chunks ({name})"); // M4.7/8: no log spam for clean cars
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void SnapshotRpc(DirtStroke[] strokes, bool last, RpcParams p = default)
        {
            foreach (var s in strokes) _painter.AddStroke(s);
            SnapshotReceived += strokes.Length;
            if (last) SnapshotDone = true;
        }

        public void ServerWorldStroke(Vector3 world, float radius, Vector4 strength, StrokeType type, byte source = 0)
        {
            var t = _painter.transform;
            ServerStroke(new DirtStroke(t.InverseTransformPoint(world), radius / Mathf.Max(0.0001f, t.lossyScale.x), strength, type) { source = source });
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            if (_painter.HasReadback)
            {
                float c = Snap(_painter.CleanPct);
                if (Mathf.Abs(CleanPct.Value - c) > 0.0005f) CleanPct.Value = c;
            }
            float rate = _data != null ? _data.balance.dirt.strokeSendRate : 10f;
            _sendT += Time.deltaTime;
            if (_sendT < 1f / rate || _outbox.Count == 0) return;
            _sendT = 0f;
            // chunk to stay well under the MTU-ish reliable fragment sizes
            for (int i = 0; i < _outbox.Count; i += 64)
            {
                int n = Mathf.Min(64, _outbox.Count - i);
                var arr = new DirtStroke[n];
                _outbox.CopyTo(i, arr, 0, n);
                StrokesRpc(arr);
            }
            _outbox.Clear();
        }

        [Rpc(SendTo.NotServer)]
        void StrokesRpc(DirtStroke[] strokes)
        {
            byte me = (byte)Mathf.Min(255, (int)NetworkManager.LocalClientId + 1);
            foreach (var s in strokes)
                if (s.source != me) _painter.AddStroke(s); // own strokes were already painted by prediction
        }

        /// <summary>P1 owner prediction: paint a stroke locally right away (the server's copy is skipped when it echoes back).</summary>
        public void LocalPredict(Vector3 world, float radius, Vector4 strength, StrokeType type)
        {
            if (IsServer) return;
            var t = _painter.transform;
            _painter.AddStroke(new DirtStroke(t.InverseTransformPoint(world), radius / Mathf.Max(0.0001f, t.lossyScale.x), strength, type));
            PredictedStrokes++;
        }

        public int PredictedStrokes { get; private set; }
    }
}
