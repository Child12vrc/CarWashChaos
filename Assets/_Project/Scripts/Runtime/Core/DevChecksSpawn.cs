using System.Collections;
using System.Linq;
using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.6 B3 check (-autotest -autotest-spawn on the host + up to 3 clients, nobody is moved by a script before the check):
    /// where did I spawn — inside the play area, capsule free, not inside a bay, not boxed in (8 horizontal rays), and can I
    /// actually walk away (2 s of W from the spawn, then 2 s back). Logged as [AUTOTEST] spawn: … → OK / TRAPPED.
    /// </summary>
    public static class DevChecksSpawn
    {
        static void Log(string m) => DevAutopilot.Log("spawn: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            yield return new WaitForSeconds(1.5f);
            var p = me.transform.position;
            bool inArea = PlayArea.Contains(p, 0.3f);
            bool room = Unstuck.HasRoom(p + Vector3.up * 0.02f);
            var bay = Vehicles.LotPaths.Instance != null ? Vehicles.LotPaths.Instance.Bays.FirstOrDefault(b => Mathf.Abs(p.x - b.transform.position.x) < 3f && Mathf.Abs(p.z - b.transform.position.z) < 5f) : null;
            int blocked = 0;
            for (int k = 0; k < 8; k++)
            {
                var d = Quaternion.Euler(0, k * 45f, 0) * Vector3.forward;
                if (Physics.Raycast(p + Vector3.up * 1f, d, 4f, (1 << Layers.Default) | (1 << Layers.PlayBounds) | (1 << Layers.Vehicle), QueryTriggerInteraction.Ignore)) blocked++;
            }
            // walk out: the four directions, keep the best displacement
            float best = 0f;
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                me.Motor.Teleport(p);
                me.Look.SetYaw(yaw);
                yield return new WaitForSeconds(0.2f);
                float t0 = Time.time;
                while (Time.time - t0 < 1.5f) { input.sMove = new Vector2(0f, 1f); yield return null; }
                input.sMove = Vector2.zero;
                best = Mathf.Max(best, Vector3.Distance(p, me.transform.position));
            }
            me.Motor.Teleport(p);
            bool ok = inArea && room && bay == null && blocked < 8 && best > 3f;
            Log($"client {me.OwnerClientId}: spawned at {p.ToString("F2")} inArea={inArea} capsuleFree={room} inBay={(bay != null ? "bay" + (bay.Index + 1) : "no")} blockedDirs={blocked}/8 walkOut={best:0.0}m → {(ok ? "OK" : "TRAPPED")}");
            yield return new WaitForSeconds(1f);
            input.scripted = false;
        }

        /// <summary>Host side: wait for `expected` players, then log everyone's spawn from the server's view.</summary>
        public static IEnumerator Host(int expected)
        {
            var nm = NetworkManager.Singleton;
            float t0 = Time.time;
            while (nm.ConnectedClientsIds.Count < expected && Time.time - t0 < 90f) yield return null;
            yield return new WaitForSeconds(1f);
            foreach (var p in Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).OrderBy(x => x.OwnerClientId))
                Debug.Log($"[HOSTSCN] spawn: server view client {p.OwnerClientId} at {p.transform.position.ToString("F2")} inArea={PlayArea.Contains(p.transform.position, 0.3f)}");
        }
    }
}
