using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Profiling;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.7 N1 (dev builds only): measures one peer for a fixed window and logs a [NETSTATS] report.
    ///  - NGO's own metric stream (the same data the Network Profiler / Runtime Net Stats Monitor show): bytes per message type,
    ///    per NetworkObject and per RPC / NetworkVariable, transport bytes / packets, RTT. multiplayer.tools only exposes it
    ///    internally, so the recorder subscribes to MetricEventPublisher.OnMetricsReceived by reflection.
    ///  - Local frame cost: frame-time percentiles, frames over 33 / 50 ms, GC allocated per frame (minus the recorder's own
    ///    allocations), GC collections, log lines per second.
    ///  - What a player sees: for every remotely driven NetworkTransform (players, cars, critters, props, tools) frames where it
    ///    is moving but did not move (stall), direction flips (reversal / rubber band) and jumps over 1.5 m (snap); plus the
    ///    longest gap between frames that received any bytes.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class NetStatsRecorder : MonoBehaviour
    {
        public static NetStatsRecorder Instance { get; private set; }
        public bool Recording { get; private set; }
        public bool Done { get; private set; }
        public string Label = "";

        // --------- NGO metrics ---------
        class Agg { public long bytes; public int count; }
        readonly Dictionary<string, Agg> _byMetric = new Dictionary<string, Agg>();
        readonly Dictionary<string, Agg> _byObject = new Dictionary<string, Agg>();
        readonly Dictionary<string, Agg> _byMessage = new Dictionary<string, Agg>();
        readonly Dictionary<string, double> _counters = new Dictionary<string, double>();
        readonly Dictionary<string, (double sum, double max, int n)> _gauges = new Dictionary<string, (double, double, int)>();
        long _droppedEvents;
        bool _subscribed;
        string _subscribeError;

        // --------- frames ---------
        readonly List<float> _frameMs = new List<float>(8192);
        ProfilerRecorder _gcAlloc;
        long _gcSum, _selfAlloc;
        int _gc0, _logLines, _framesRecv;
        float _lastRecvTime, _maxRecvGap; int _recvGaps200;
        double _bytesThisFrameRecv;

        // --------- remote motion ---------
        class Track { public NetworkTransform nt; public string kind; public Vector3 last, lastD; public float speed; public int zeroRun; public bool init; public string localKind; }
        readonly List<Track> _tracks = new List<Track>();
        readonly Dictionary<string, long[]> _motion = new Dictionary<string, long[]>(); // kind → moving frames, hitch frames, reversals, snaps, speed-jitter ‰ sum, hitches
        float _rescan;
        float _t0, _duration;

        public static NetStatsRecorder Create(string label)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("NetStatsRecorder");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NetStatsRecorder>();
            Instance.Label = label;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-netstats-nometrics") < 0) Instance.Subscribe(); // control run: no reflection handler inside NGO's metric dispatch
            return Instance;
        }

        void Subscribe()
        {
            try
            {
                var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Multiplayer.Tools.MetricEvents.MetricEventPublisher")).FirstOrDefault(x => x != null);
                if (t == null) { _subscribeError = "MetricEventPublisher not found"; return; }
                var ev = t.GetEvent("OnMetricsReceived", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var mi = typeof(NetStatsRecorder).GetMethod(nameof(OnMetrics), BindingFlags.NonPublic | BindingFlags.Instance);
                var d = Delegate.CreateDelegate(ev.EventHandlerType, this, mi);
                ev.GetAddMethod(true).Invoke(null, new object[] { d });
                _subscribed = true;
            }
            catch (Exception e) { _subscribeError = e.GetType().Name + ": " + e.Message; }
        }

        public void Begin(float seconds)
        {
            _duration = seconds; _t0 = Time.unscaledTime; Recording = true; Done = false;
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gc0 = GC.CollectionCount(0);
            _lastRecvTime = Time.unscaledTime;
            Application.logMessageReceived += OnLog;
            Rescan();
            // M4.7 E: -env-off = A/B control in the same build: no dressing / decals, ground back on URP Lit
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-env-off") >= 0)
            {
                var dress = GameObject.Find("Dressing"); if (dress != null) dress.SetActive(false);
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    foreach (var m in r.sharedMaterials) if (m != null && m.shader.name == "CarWash/GroundStochastic" && lit != null) m.shader = lit;
                Label += " env-off";
            }
            // -profilecapture N: also write a Unity Profiler binary log of N frames (dev builds) for the editor to break down
            int pi = Array.IndexOf(Environment.GetCommandLineArgs(), "-profilecapture");
            if (pi >= 0 && Debug.isDebugBuild) StartCoroutine(Capture(pi + 1 < Environment.GetCommandLineArgs().Length && int.TryParse(Environment.GetCommandLineArgs()[pi + 1], out int pf) ? pf : 600));
            Debug.Log($"[NETSTATS] {Label}: recording {seconds:0} s (metrics stream {(_subscribed ? "on" : "OFF: " + _subscribeError)})");
        }

        /// <summary>Path of the profiler capture (.raw) once written.</summary>
        public string CapturePath { get; private set; }

        IEnumerator Capture(int frames)
        {
            yield return new WaitForSeconds(5f);
            string path = System.IO.Path.Combine(Application.dataPath, "..", "profile_" + Label.Replace("#", "_") + ".raw");
            UnityEngine.Profiling.Profiler.maxUsedMemory = 256 * 1024 * 1024;
            UnityEngine.Profiling.Profiler.logFile = path;
            UnityEngine.Profiling.Profiler.enableBinaryLog = true;
            UnityEngine.Profiling.Profiler.enabled = true;
            for (int i = 0; i < frames; i++) yield return null;
            UnityEngine.Profiling.Profiler.enabled = false;
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            UnityEngine.Profiling.Profiler.logFile = "";
            CapturePath = System.IO.Path.GetFullPath(path);
            Debug.Log($"[NETSTATS] {Label}: profiler capture {frames} frames → {CapturePath}");
        }

        void OnLog(string c, string s, LogType t) => _logLines++;

        // ---------------- NGO metric stream (called from NetworkManager's metric dispatch, once per frame) ----------------
        static readonly Dictionary<Type, (PropertyInfo name, PropertyInfo value, PropertyInfo values, PropertyInfo count, PropertyInfo received)> MetricProps = new Dictionary<Type, (PropertyInfo, PropertyInfo, PropertyInfo, PropertyInfo, PropertyInfo)>();
        static readonly Dictionary<Type, (PropertyInfo name, PropertyInfo bytes, PropertyInfo obj, PropertyInfo beh)> EventProps = new Dictionary<Type, (PropertyInfo, PropertyInfo, PropertyInfo, PropertyInfo)>();
        static PropertyInfo _objName;

        static PropertyInfo Find(Type t, string prop)
        {
            var p = t.GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p;
            foreach (var i in t.GetInterfaces()) { p = i.GetProperty(prop); if (p != null) return p; }
            return null;
        }

        void OnMetrics(object collection)
        {
            if (!Recording || collection == null) return;
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                var metrics = Find(collection.GetType(), "Metrics")?.GetValue(collection) as IEnumerable;
                if (metrics == null) return;
                foreach (var m in metrics)
                {
                    var mt = m.GetType();
                    if (!MetricProps.TryGetValue(mt, out var mp))
                        MetricProps[mt] = mp = (Find(mt, "Name"), Find(mt, "Value"), Find(mt, "Values"), Find(mt, "Count"), Find(mt, "NumberOfValuesReceived"));
                    string name = mp.name?.GetValue(m) as string ?? mt.Name;
                    if (mp.values != null)
                    {
                        if (mp.count != null && mp.received != null)
                        {
                            int c = (int)mp.count.GetValue(m), r = (int)mp.received.GetValue(m);
                            if (r > c) _droppedEvents += r - c;
                        }
                        if (!(mp.values.GetValue(m) is IEnumerable vals)) continue;
                        bool recv = name.IndexOf("Received", StringComparison.OrdinalIgnoreCase) >= 0;
                        foreach (var e in vals)
                        {
                            var et = e.GetType();
                            if (!EventProps.TryGetValue(et, out var ep))
                                EventProps[et] = ep = (Find(et, "Name"), Find(et, "BytesCount"), Find(et, "NetworkId"), Find(et, "NetworkBehaviourName"));
                            long bytes = ep.bytes != null ? Convert.ToInt64(ep.bytes.GetValue(e)) : 0;
                            string en = ep.name?.GetValue(e)?.ToString() ?? "";
                            string beh = ep.beh?.GetValue(e)?.ToString() ?? "";
                            string obj = "";
                            if (ep.obj != null)
                            {
                                var id = ep.obj.GetValue(e);
                                _objName ??= Find(id.GetType(), "Name");
                                obj = Clean(_objName?.GetValue(id)?.ToString() ?? "");
                            }
                            Add(_byMetric, name, bytes);
                            if (obj.Length > 0) Add(_byObject, (recv ? "recv " : "sent ") + obj, bytes);
                            Add(_byMessage, $"{name} | {(beh.Length > 0 ? beh + "." : "")}{en}", bytes);
                            if (recv) _bytesThisFrameRecv += bytes;
                        }
                    }
                    else if (mp.value != null)
                    {
                        double v = Convert.ToDouble(mp.value.GetValue(m));
                        if (mt.Name.StartsWith("Gauge"))
                        {
                            _gauges.TryGetValue(name, out var g);
                            _gauges[name] = (g.sum + v, Math.Max(g.max, v), g.n + 1);
                        }
                        else
                        {
                            _counters.TryGetValue(name, out var c); _counters[name] = c + v;
                            if (name.IndexOf("Received", StringComparison.OrdinalIgnoreCase) >= 0) _bytesThisFrameRecv += v;
                        }
                    }
                }
            }
            catch (Exception e) { _subscribeError = e.Message; }
            _selfAlloc += GC.GetAllocatedBytesForCurrentThread() - a0;
        }

        static string Clean(string n)
        {
            n = n.Replace("(Clone)", "");
            int i = n.IndexOf(" (", StringComparison.Ordinal); // "Cone (3)" → "Cone"
            return i > 0 ? n.Substring(0, i) : n;
        }

        static void Add(Dictionary<string, Agg> d, string k, long bytes)
        {
            if (!d.TryGetValue(k, out var a)) d[k] = a = new Agg();
            a.bytes += bytes; a.count++;
        }

        // ---------------- per frame ----------------
        void Rescan()
        {
            _tracks.Clear();
            foreach (var nt in FindObjectsByType<NetworkTransform>(FindObjectsSortMode.None))
            {
                string kind = nt.GetComponent<Player.PlayerNet>() ? "player" : nt.GetComponent<Vehicles.Vehicle>() ? "car" : nt.GetComponent<Critters.CritterBase>() ? "critter"
                    : nt.GetComponent<Tools.ToolBase>() ? "tool" : "prop";
                DevNtTuning.Apply(nt);
                _tracks.Add(new Track { nt = nt, kind = kind });
            }
        }

        void LateUpdate()
        {
            if (!Recording) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            _frameMs.Add(dt * 1000f);
            if (_gcAlloc.Valid) _gcSum += _gcAlloc.LastValue;
            if (_bytesThisFrameRecv > 0) { float gap = now - _lastRecvTime; if (gap > _maxRecvGap) _maxRecvGap = gap; if (gap > 0.2f) _recvGaps200++; _lastRecvTime = now; _framesRecv++; }
            _bytesThisFrameRecv = 0;

            if ((_rescan -= dt) <= 0f) { _rescan = 2f; RescanKeep(); }
            foreach (var t in _tracks)
            {
                if (t.nt == null || !t.nt.isActiveAndEnabled) { t.init = false; continue; }
                var p = t.nt.transform.position;
                if (!t.init) { t.last = p; t.lastD = Vector3.zero; t.speed = 0f; t.zeroRun = 0; t.init = true; continue; }
                var d = p - t.last; t.last = p;
                float mag = d.magnitude, spd = mag / Mathf.Max(0.001f, dt);
                string kind = t.nt.CanCommitToTransform ? (t.localKind ??= "local " + t.kind) : t.kind; // local = driven on this peer (control: how smooth is the source?)
                if (!_motion.TryGetValue(kind, out var c)) _motion[kind] = c = new long[6];
                if (mag > 1.5f) { c[3]++; t.lastD = Vector3.zero; t.speed = 0f; t.zeroRun = 0; continue; }
                if (mag < 0.0005f)
                {
                    if (t.speed > 0.6f) t.zeroRun++; // froze while it was moving: a hitch if it moves again within 6 frames
                    else t.zeroRun = 0;
                    continue;
                }
                // moving this frame
                if (t.zeroRun > 0 && t.zeroRun <= 6) { c[1] += t.zeroRun; c[5]++; }
                t.zeroRun = 0;
                if (spd > 0.6f && t.speed > 0.6f)
                {
                    c[0]++;
                    c[4] += (long)(1000f * Mathf.Abs(spd - t.speed) / Mathf.Max(spd, t.speed)); // per-mille speed change frame to frame
                    if (t.lastD.sqrMagnitude > 1e-6f && Vector3.Dot(d, t.lastD) < -0.3f * mag * t.lastD.magnitude && mag > 0.004f) c[2]++;
                }
                t.speed = spd;
                t.lastD = d;
            }
            if (now - _t0 >= _duration) Finish();
        }

        void RescanKeep()
        {
            var keep = _tracks.Where(t => t.nt != null).ToDictionary(t => t.nt);
            _tracks.Clear();
            foreach (var nt in FindObjectsByType<NetworkTransform>(FindObjectsSortMode.None))
            {
                if (keep.TryGetValue(nt, out var t)) { _tracks.Add(t); continue; }
                string kind = nt.GetComponent<Player.PlayerNet>() ? "player" : nt.GetComponent<Vehicles.Vehicle>() ? "car" : nt.GetComponent<Critters.CritterBase>() ? "critter"
                    : nt.GetComponent<Tools.ToolBase>() ? "tool" : "prop";
                DevNtTuning.Apply(nt);
                _tracks.Add(new Track { nt = nt, kind = kind });
            }
        }

        void Finish()
        {
            Recording = false; Done = true;
            Application.logMessageReceived -= OnLog;
            float secs = Mathf.Max(1f, Time.unscaledTime - _t0);
            var nm = NetworkManager.Singleton;
            var sb = new StringBuilder();
            string role = nm == null ? "?" : nm.IsServer ? "HOST" : "client " + nm.LocalClientId;
            int players = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : FindObjectsByType<Player.PlayerNet>(FindObjectsSortMode.None).Length;
            sb.Append($"[NETSTATS] {Label} {role}: {secs:0} s, {players} players, tick {nm?.NetworkConfig.TickRate}, mode {Net.NetSession.Mode}, netsim {(Net.NetSession.ArgNetSim != null ? string.Join("/", Net.NetSession.ArgNetSim) : "off")}, metrics {(_subscribed ? "on" : "OFF " + _subscribeError)}, dropped metric events {_droppedEvents}\n");
            foreach (var kv in _counters.OrderBy(k => k.Key)) sb.Append($"[NETSTATS]   counter {kv.Key}: {kv.Value / secs:0.0}/s\n");
            foreach (var kv in _gauges.OrderBy(k => k.Key)) sb.Append($"[NETSTATS]   gauge {kv.Key}: avg {kv.Value.sum / Math.Max(1, kv.Value.n):0.###} max {kv.Value.max:0.###}\n");
            foreach (var kv in _byMetric.OrderByDescending(k => k.Value.bytes)) sb.Append($"[NETSTATS]   type {kv.Key}: {kv.Value.bytes / secs:0} B/s, {kv.Value.count / secs:0.0} msg/s\n");
            foreach (var kv in _byObject.OrderByDescending(k => k.Value.bytes).Take(12)) sb.Append($"[NETSTATS]   object {kv.Key}: {kv.Value.bytes / secs:0} B/s, {kv.Value.count / secs:0.0} msg/s\n");
            foreach (var kv in _byMessage.OrderByDescending(k => k.Value.bytes).Take(15)) sb.Append($"[NETSTATS]   message {kv.Key}: {kv.Value.bytes / secs:0} B/s, {kv.Value.count / secs:0.0} msg/s, {kv.Value.bytes / Math.Max(1, kv.Value.count)} B each\n");
            var f = _frameMs.OrderBy(x => x).ToList();
            float P(float q) => f.Count == 0 ? 0f : f[Mathf.Clamp((int)(q * f.Count), 0, f.Count - 1)];
            long gcNet = Math.Max(0, _gcSum - _selfAlloc);
            sb.Append($"[NETSTATS]   frames: {f.Count / secs:0.0} fps, ms p50 {P(0.5f):0.0} p95 {P(0.95f):0.0} p99 {P(0.99f):0.0} max {P(1f):0.0}, >33 ms {f.Count(x => x > 33.4f)}, >50 ms {f.Count(x => x > 50f)}; GC {gcNet / 1024f / Math.Max(1, f.Count):0.0} KB/frame (recorder itself {_selfAlloc / 1024f / Math.Max(1, f.Count):0.0} excluded), collections {GC.CollectionCount(0) - _gc0}; logs {_logLines / secs:0.0}/s\n");
            sb.Append($"[NETSTATS]   receive: frames with data {_framesRecv / secs:0.0}/s, longest gap {_maxRecvGap * 1000f:0} ms, gaps > 200 ms {_recvGaps200}\n");
            foreach (var kv in _motion.OrderBy(k => k.Key))
            {
                var c = kv.Value; float mv = Mathf.Max(1, c[0]);
                sb.Append($"[NETSTATS]   {(kv.Key.StartsWith("local") ? "" : "remote ")}{kv.Key}: moving frames {c[0]}, hitches {c[5] / secs * 60f:0.0}/min ({c[1] * 100f / mv:0.00}% of moving frames frozen), speed jitter {c[4] / mv / 10f:0.0}%, reversals {c[2] * 100f / mv:0.00}%, snaps {c[3]}\n");
            }
            Debug.Log(sb.ToString());
            _gcAlloc.Dispose();
        }

        void OnDestroy() { if (_gcAlloc.Valid) _gcAlloc.Dispose(); }
    }
}
