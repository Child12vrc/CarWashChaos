using System.Collections;
using System.Linq;
using CarWash.Dirt;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4 host side (DevAutopilot.NetMode): as soon as one client is in, "washes" the dev-yard V04 test car with server
    /// strokes for 15 s (so a client that joins later must get them through the T4.3 snapshot), then logs players,
    /// readiness and money every few seconds. Logged as [HOSTSCN].
    /// </summary>
    public static class DevNetScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] " + m);

        public static IEnumerator Run()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 2) yield return null;
            var car = Object.FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None).OrderBy(d => (d.transform.position - DevYard.CarV04).sqrMagnitude).First();
            float c0 = car.CleanPct.Value;
            float t0 = Time.time;
            while (Time.time - t0 < 15f) { Wash(car); yield return new WaitForSeconds(0.1f); }
            yield return new WaitForSeconds(1.5f);
            Log($"net: washed {car.name} {c0 * 100f:0.0}% → {car.CleanPct.Value * 100f:0.0}% with {car.HistoryCount} strokes; clients={nm.ConnectedClientsIds.Count}");
            for (int i = 0; i < 40; i++)
            {
                var ps = Object.FindObjectsByType<Player.PlayerNet>(FindObjectsSortMode.None);
                Log($"net: players={ps.Length} colours=[{string.Join(",", ps.OrderBy(p => p.OwnerClientId).Select(p => p.ColorIndex.Value))}] ready={ps.Count(p => p.Ready.Value)} money={Economy.EconomyState.Instance?.Money.Value}");
                yield return new WaitForSeconds(5f);
            }
        }

        static void Wash(DirtNetSync car)
        {
            var col = car.GetComponentInChildren<Collider>();
            if (col == null) return;
            var b = col.bounds;
            for (int i = 0; i < 6; i++)
            {
                var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                var from = new Vector3(b.center.x, Random.Range(b.min.y + 0.2f, b.max.y), b.center.z) + dir * (b.extents.magnitude + 1f);
                var to = new Vector3(b.center.x, from.y, b.center.z) - from;
                if (col.Raycast(new Ray(from, to.normalized), out var hit, to.magnitude))
                    car.ServerWorldStroke(hit.point, 0.4f, new Vector4(1.5f, 1.5f, 0f, 1.5f), StrokeType.Clean);
            }
        }
    }
}
