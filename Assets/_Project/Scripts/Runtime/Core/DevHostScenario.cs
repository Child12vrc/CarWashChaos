using System.Collections;
using System.Linq;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// Dev host side of the T2.2 2-player check: once a client is connected, 5 customers arrive (3 bays open →
    /// 3 park, 2 wait with a short test patience and leave angry), after 45 s of washing the host presses DONE on all.
    /// Logged as [HOSTSCN].
    /// </summary>
    public static class DevHostScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] " + m);

        public static IEnumerator Run(int minPlayers = 2)
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < minPlayers) yield return null;
            while (QueueLane.Instance == null) yield return null;
            var lane = QueueLane.Instance;
            QueueLane.PatienceOverride = 25f;
            yield return new WaitForSeconds(3f);
            foreach (var id in new[] { "V01", "V02", "V03", "V04", "B01" })
            {
                Vehicles.Vehicle v = null;
                float tw = Time.time;
                while (v == null && Time.time - tw < 60f) { v = lane.ServerArrive(id, noGrease: true); if (v == null) yield return new WaitForSeconds(0.5f); }
                Log($"arrive {id}: {(v != null ? "ok" : "gave up")} queue={lane.QueueCount} waited {Time.time - tw:0.0}s");
                yield return new WaitForSeconds(1f);
            }
            float t0 = Time.time;
            while (lane.Active.Count(v => v.State.Value == VehicleState.Washing) < 3 && Time.time - t0 < 150f) yield return null;
            Log($"washing after {Time.time - t0:0}s: " + string.Join(", ", lane.Active.Select(v => $"{v.DataId}:{v.State.Value}@bay{v.BayIndex.Value + 1}")));
            int angry = 0, finished = 0;
            lane.VehicleAngry += _ => angry++;
            lane.VehicleFinished += _ => finished++;
            yield return new WaitForSeconds(45f);
            Log("pressing DONE on every washing vehicle from now on");
            t0 = Time.time;
            while (lane.Active.Count > 0 && Time.time - t0 < 180f)
            {
                foreach (var v in lane.Active.Where(v => v.State.Value == VehicleState.Washing && v.PhaseElapsed > 3f).ToArray()) lane.ServerFinish(v);
                yield return new WaitForSeconds(0.5f);
            }
            Log($"all vehicles gone={lane.Active.Count == 0} after {Time.time - t0:0}s; finished={finished} angry={angry}");
            QueueLane.PatienceOverride = 0f;
        }
    }
}
