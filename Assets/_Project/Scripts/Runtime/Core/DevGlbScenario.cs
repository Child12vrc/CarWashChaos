using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Critters;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// GLB vehicle check, host side (-autotest -autotest-glb on the hosting dev exe). Batch 1: V01/V02/V03 arrive in bays
    /// 1-3 with forced surprises (V01 possum, V02 bees, V03 mud slime); once the client has pressed DONE on all three and they
    /// left, batch 2: V04 (raccoon) and the B01 boss. Every surprise critter's spawn point is checked against its car's
    /// physics hull (must be outside it, on the ground) and logged, then removed after a few seconds so it doesn't stop the
    /// client's wash-to-100% check. Logged as [HOSTSCN] glb:.
    /// </summary>
    public static class DevGlbScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] glb: " + m);

        // one car at a time: only bay 1 has a washer (its hose reaches bay 1 only), so every car is washed there
        static readonly string[][] Batches = CarsArg() ?? new[] { new[] { "V01" }, new[] { "V02" }, new[] { "V03" }, new[] { "V04" }, new[] { "B01" } };

        /// <summary>-glb-cars V01,V02 → one batch with just those (quick re-checks).</summary>
        static string[][] CarsArg()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-glb-cars");
            return i >= 0 && i + 1 < a.Length ? a[i + 1].Split(',').Select(c => new[] { c }).ToArray() : null;
        }
        static readonly Dictionary<string, string> Picks = new Dictionary<string, string> { { "V01", "C05" }, { "V02", "C02" }, { "V03", "C03" }, { "V04", "C01" } };

        public static IEnumerator Run()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 2) yield return null;
            while (QueueLane.Instance == null || LotState.Instance == null) yield return null;
            var lane = QueueLane.Instance;
            QueueLane.PatienceOverride = 1800f;
            LotState.Instance.ServerSetOpenCount(1);
            yield return new WaitForSeconds(2f);

            var all = new List<Vehicle>();
            var seen = new Dictionary<CritterBase, float>();
            int batch = 0;
            var current = new List<Vehicle>();
            float deadline = Time.time + 2400f;
            while (Time.time < deadline)
            {
                // next batch once the previous one has left
                if (current.All(v => v == null || v.State.Value == VehicleState.Leaving || !v.IsSpawned) && batch < Batches.Length)
                {
                    if (current.Count > 0) Log($"batch {batch} done");
                    current.Clear();
                    SurpriseTrigger.DevForcePick = id => Picks.TryGetValue(id, out var c) ? c : null;
                    foreach (var id in Batches[batch])
                    {
                        Vehicle v = null; float tw = Time.time;
                        while (v == null && Time.time - tw < 60f) { v = lane.ServerArrive(id, noGrease: true); if (v == null) yield return new WaitForSeconds(0.5f); }
                        if (v != null) { current.Add(v); all.Add(v); }
                        var st = v != null ? v.GetComponent<SurpriseTrigger>() : null;
                        Log($"arrive {id}: {(v != null ? "ok" : "FAILED")} model={(v != null && v.transform.Find("Model") != null)} surprise={st?.Pending?.critterId} on {st?.Kind}");
                        yield return new WaitForSeconds(1.5f);
                    }
                    SurpriseTrigger.DevForcePick = null;
                    batch++;
                }

                // surprise critters: where did they appear?
                foreach (var c in Object.FindObjectsByType<CritterBase>(FindObjectsSortMode.None))
                {
                    if (!c.IsSpawned || !c.FromSurprise || seen.ContainsKey(c)) continue;
                    seen[c] = Time.time;
                    var p = c.transform.position;
                    var car = all.Where(v => v != null && v.IsSpawned).OrderBy(v => (v.transform.position - p).sqrMagnitude).FirstOrDefault();
                    string where = "no car";
                    bool ok = false;
                    if (car != null)
                    {
                        var hull = car.GetComponentInChildren<VehicleHull>();
                        bool inside = false; float gap = float.MaxValue;
                        if (hull != null)
                            foreach (var col in hull.GetComponents<Collider>())
                            {
                                float d = (col.ClosestPoint(p) - p).magnitude;
                                if (d < 0.001f) inside = true;
                                gap = Mathf.Min(gap, d);
                            }
                        ok = hull != null && !inside && p.y < 0.6f && p.y > -0.05f; // on the ground, not under it
                        where = $"car {car.DataId} insideHull={inside} gapToHull={gap:0.00}m";
                    }
                    Log($"surprise critter {c.name} at {p.ToString("F2")} {where} → {(ok ? "OK" : "BAD")}");
                }
                foreach (var kv in seen.ToArray())
                    // 1 s: the spawn spot is checked; a live raccoon would steal the bot's towel (v0.9: the critters stopped falling
                    // under the lot, so they now play — this check is about washing, critters have their own tests)
                    if (kv.Key != null && kv.Key.IsSpawned && Time.time - kv.Value > 1f) kv.Key.NetworkObject.Despawn(true);
                if (batch >= Batches.Length && current.All(v => v == null || !v.IsSpawned || v.State.Value == VehicleState.Leaving)) { Log("all batches done"); yield break; }
                yield return new WaitForSeconds(0.2f);
            }
            Log("timeout");
        }
    }
}
