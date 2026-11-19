using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Player;
using CarWash.Vehicles;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarWash.Core
{
    /// <summary>
    /// M4.11: load-independent render metrics from one fixed view (host alone: -autotest -autotest-perfsnap LABEL -time 13).
    /// Fills the three bays (V01 V02 V03) and queues two more (V04 B01), stands at a fixed spot overlooking the bays with the
    /// HUD hidden, then averages 300 frames: SetPass calls, draw calls, batches, triangles, vertices, shadow casters, GC per
    /// frame, CPU main-thread and GPU frame time. Also counts the scene's active renderers / shadow-casting renderers / LOD
    /// groups. Logged as "[PERF] LABEL …" for before / after tables.
    /// </summary>
    public static class DevChecksPerf
    {
        public static string Label
        {
            get
            {
                var a = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(a, "-autotest-perfsnap");
                return i < 0 ? null : i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "now";
            }
        }

        public static readonly Vector3 Eye = new Vector3(0f, 0.05f, -9f), Look = new Vector3(0f, 1.2f, 4f);
        static readonly string[] Cars = { "V01", "V02", "V03", "V04", "B01" };

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            var lane = QueueLane.Instance;
            if (LotState.Instance != null) LotState.Instance.ServerSetOpenCount(3);
            QueueLane.PatienceOverride = 1800f;
            yield return new WaitForSeconds(2f);
            foreach (var id in Cars)
            {
                Vehicle v = null; float t0 = Time.time;
                while (v == null && Time.time - t0 < 30f) { v = lane != null ? lane.ServerArrive(id, noGrease: true) : null; if (v == null) yield return new WaitForSeconds(0.5f); }
                yield return new WaitForSeconds(1f);
            }
            // let the cars drive into the bays / the queue and stop
            float tw = Time.time;
            while (Time.time - tw < 60f && Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).Count(v => v.State.Value == VehicleState.Washing) < 3)
                yield return new WaitForSeconds(1f);
            yield return new WaitForSeconds(4f);

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases) c.enabled = false;
            me.Motor.Teleport(Eye);
            yield return new WaitForSeconds(0.8f);
            me.Look.LookAt(Look);
            yield return new WaitForSeconds(1.5f);

            // uncapped: with vsync the main thread just waits for the 144 Hz display (6.94 ms whatever is drawn)
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return Measure(Label);
            ScreenCapture.CaptureScreenshot($"perfsnap_{Label}.png");
            yield return null;

            // breakdown: the same view with one category switched off at a time (-perf-breakdown)
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perf-breakdown") >= 0)
            {
                var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).ToArray();
                var shadows = sun.Select(l => l.shadows).ToArray();
                foreach (var l in sun) l.shadows = LightShadows.None;
                yield return Measure(Label + "-noShadows");
                for (int i = 0; i < sun.Length; i++) sun[i].shadows = shadows[i];

                var decals = Object.FindObjectsByType<UnityEngine.Rendering.Universal.DecalProjector>(FindObjectsSortMode.None).Where(d => d.enabled).ToArray();
                foreach (var d in decals) d.enabled = false;
                yield return Measure(Label + "-noDecals");
                foreach (var d in decals) d.enabled = true;

                var carRenderers = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).SelectMany(v => v.GetComponentsInChildren<Renderer>()).Where(r => r.enabled).ToArray();
                foreach (var r in carRenderers) r.enabled = false;
                yield return Measure(Label + "-noCars");
                foreach (var r in carRenderers) r.enabled = true;

                var env = GameObject.Find("Environment");
                if (env != null)
                    foreach (Transform group in env.transform)
                    {
                        var rs = group.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                        if (rs.Length < 20) continue;
                        foreach (var r in rs) r.enabled = false;
                        yield return Measure($"{Label}-no{group.name}({rs.Length})");
                        foreach (var r in rs) r.enabled = true;
                    }
            }
            foreach (var c in canvases) if (c) c.enabled = true;
            input.scripted = false;
        }

        static IEnumerator Measure(string label)
        {
            yield return new WaitForSeconds(0.3f);
            var rec = new Dictionary<string, ProfilerRecorder>
            {
                ["setpass"] = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"),
                ["draws"] = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"),
                ["batches"] = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"),
                ["tris"] = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"),
                ["verts"] = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count"),
                ["shadowcasters"] = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count"),
                ["gc"] = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"),
                ["main"] = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15),
                // where the main thread goes (ns)
                ["scripts"] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate"),
                ["late"] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "LateBehaviourUpdate"),
                ["physics"] = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate"),
                ["render"] = ProfilerRecorder.StartNew(ProfilerCategory.Render, "PostLateUpdate.FinishFrameRendering"),
                ["anim"] = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationBegin"),
                ["net"] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "PreUpdate.NetworkUpdate"),
            };
            yield return new WaitForSeconds(0.5f);
            const int frames = 300;
            var sum = rec.Keys.ToDictionary(k => k, k => 0.0);
            double cpu = 0, gpu = 0; int gpuN = 0;
            var timing = new FrameTiming[1];
            for (int f = 0; f < frames; f++)
            {
                yield return null;
                foreach (var kv in rec) sum[kv.Key] += kv.Value.Valid ? kv.Value.LastValue : -1;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    cpu += timing[0].cpuFrameTime;
                    if (timing[0].gpuFrameTime > 0) { gpu += timing[0].gpuFrameTime; gpuN++; }
                }
            }
            foreach (var r in rec.Values) r.Dispose();
            var avg = sum.ToDictionary(kv => kv.Key, kv => kv.Value / frames);

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            int casters = renderers.Count(r => r.shadowCastingMode != ShadowCastingMode.Off);
            int lods = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length;
            int parked = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).Count(v => v.State.Value == VehicleState.Washing);
            DevAutopilot.Log($"[PERF] {label}: setpass {avg["setpass"]:0} | draws {avg["draws"]:0} | batches {avg["batches"]:0} | tris {avg["tris"] / 1000.0:0.0}k | verts {avg["verts"] / 1000.0:0.0}k | " +
                             $"shadow casters {avg["shadowcasters"]:0} | GC {avg["gc"] / 1024.0:0.0} KB/frame | main thread {avg["main"] / 1e6:0.00} ms | " +
                             $"cpu frame {cpu / frames:0.00} ms | gpu frame {(gpuN > 0 ? (gpu / gpuN).ToString("0.00") : "n/a")} ms | " +
                             $"scene: {renderers.Count} renderers, {casters} cast shadows, {lods} LOD groups, {parked} cars in bays");
            string Ms(string k) => avg[k] < 0 ? "n/a" : (avg[k] / 1e6).ToString("0.00");
            DevAutopilot.Log($"[PERF] {label} main thread split: scripts {Ms("scripts")} | late {Ms("late")} | physics {Ms("physics")} | render {Ms("render")} | anim {Ms("anim")} | net {Ms("net")} ms");
        }
    }
}
