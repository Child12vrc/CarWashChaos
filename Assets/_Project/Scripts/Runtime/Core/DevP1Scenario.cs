using System.Collections;
using System.Linq;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>P1 host side: keeps customer cars driving in and out (queue → bay → exit) so clients can judge vehicle motion.</summary>
    public static class DevP1Scenario
    {
        public static IEnumerator Run()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 2) yield return null;
            while (QueueLane.Instance == null) yield return null;
            var lane = QueueLane.Instance;
            QueueLane.PatienceOverride = 300f;
            float t0 = Time.time;
            int i = 0;
            string[] ids = { "V01", "V02", "V03" };
            while (Time.time - t0 < 240f)
            {
                if (lane.Active.Count < 3) lane.ServerArrive(ids[i++ % ids.Length], noGrease: true);
                foreach (var v in lane.Active.Where(v => v != null && v.State.Value == VehicleState.Washing && v.PhaseElapsed > 6f).ToArray()) lane.ServerFinish(v);
                yield return new WaitForSeconds(1f);
            }
            QueueLane.PatienceOverride = 0f;
        }
    }
}
