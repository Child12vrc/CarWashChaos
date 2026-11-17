using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Dirt;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// T3.6 host side (DevAutopilot.SurpriseMode): V01..V04 arrive with forced surprise picks — V01 possum (OnOpen),
    /// V02 bees (OnCleanPct 0.5), V03 mud slime (OnCleanPct 0.3), V04 raccoon (OnOpen). The client opens the doors;
    /// the host "washes" V02/V03 with server clean strokes until their OnCleanPct fires. Logged as [HOSTSCN].
    /// </summary>
    public static class DevSurpriseScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] " + m);

        public static IEnumerator Run(int minPlayers = 2)
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < minPlayers) yield return null;
            while (QueueLane.Instance == null) yield return null;
            var lane = QueueLane.Instance;
            QueueLane.PatienceOverride = 300f;
            var picks = new Dictionary<string, string> { { "V01", "C05" }, { "V02", "C02" }, { "V03", "C03" }, { "V04", "C01" } };
            SurpriseTrigger.DevForcePick = id => picks.TryGetValue(id, out var c) ? c : null;
            yield return new WaitForSeconds(2f);
            var cars = new List<Vehicle>();
            foreach (var id in new[] { "V02", "V03", "V01", "V04" })
            {
                Vehicle v = null; float tw = Time.time;
                while (v == null && Time.time - tw < 60f) { v = lane.ServerArrive(id, noGrease: true); if (v == null) yield return new WaitForSeconds(0.5f); }
                if (v != null) cars.Add(v);
                Log($"surprise arrive {id}: {(v != null ? "ok" : "gave up")}");
                yield return new WaitForSeconds(1f);
            }
            SurpriseTrigger.DevForcePick = null;
            // wash V02/V03 with server strokes (stand-in for players spraying) until their OnCleanPct fires
            float t0 = Time.time;
            while (Time.time - t0 < 180f)
            {
                bool all = true;
                foreach (var v in cars)
                {
                    if (v == null) continue;
                    var st = v.GetComponent<SurpriseTrigger>();
                    if (st.Pending == null || st.Fired) continue;
                    all = false;
                    if (st.Kind == Data.TriggerKind.OnCleanPct && v.State.Value == VehicleState.Washing) DevWash(v);
                }
                if (all) break;
                yield return new WaitForSeconds(0.1f);
            }
            Log("surprises: " + string.Join(", ", cars.Where(v => v != null).Select(v => { var s = v.GetComponent<SurpriseTrigger>(); return $"{v.DataId}:{s.Pending?.critterId}/{s.Kind} fired={s.Fired}"; })) + $" after {Time.time - t0:0}s");
            yield return new WaitForSeconds(8f);
            // T3.7: clear the bays, then the boss (no forced pick — bosses always carry their surprise)
            t0 = Time.time;
            while (lane.Active.Count > 0 && Time.time - t0 < 90f)
            {
                foreach (var v in lane.Active.Where(v => v != null && v.State.Value == VehicleState.Washing).ToArray()) lane.ServerFinish(v);
                yield return new WaitForSeconds(0.5f);
            }
            SurpriseTrigger.DevChanceOverride = 0f; // let arrivals roll outside a running day; the boss overrides to 1
            Vehicle boss = null; float tb = Time.time;
            while (boss == null && Time.time - tb < 30f) { boss = lane.ServerArrive("B01", noGrease: true); if (boss == null) yield return new WaitForSeconds(0.5f); }
            var bs = boss != null ? boss.GetComponent<SurpriseTrigger>() : null;
            Log($"boss arrive B01: {(boss != null ? "ok" : "failed")} armed={bs?.Pending?.critterId} x{bs?.Pending?.count} on {bs?.Kind}");
            t0 = Time.time;
            while (boss != null && bs.Pending != null && !bs.Fired && Time.time - t0 < 120f)
            {
                if (boss.State.Value == VehicleState.Washing) DevWash(boss);
                yield return new WaitForSeconds(0.1f);
            }
            Log($"boss surprise fired={bs?.Fired} after {Time.time - t0:0}s; slimes alive={Object.FindObjectsByType<Critters.MudSlime>(FindObjectsSortMode.None).Length}");
            yield return new WaitForSeconds(10f);
            foreach (var v in lane.Active.Where(v => v != null && v.State.Value == VehicleState.Washing).ToArray()) lane.ServerFinish(v);
            QueueLane.PatienceOverride = 0f;
            SurpriseTrigger.DevChanceOverride = -1f;
        }

        static readonly RaycastHit[] Hits = new RaycastHit[4];

        static void DevWash(Vehicle v)
        {
            var col = v.GetComponentInChildren<Collider>();
            if (col == null) return;
            var b = col.bounds;
            for (int i = 0; i < 6; i++)
            {
                var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                var from = new Vector3(b.center.x, Random.Range(b.min.y + 0.2f, b.max.y), b.center.z) + dir * (b.extents.magnitude + 1f);
                var to = new Vector3(b.center.x, from.y, b.center.z) - from;
                if (col.Raycast(new Ray(from, to.normalized), out var hit, to.magnitude))
                    v.Dirt.ServerWorldStroke(hit.point, 0.4f, new Vector4(1.5f, 1.5f, 0f, 1.5f), StrokeType.Clean);
            }
        }
    }
}
