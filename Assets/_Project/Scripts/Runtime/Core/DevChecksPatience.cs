using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Player;
using CarWash.UI;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.9 G6 check. Host (-autotest -host … -autotest-patience): starts the day with only bay 1 open, 30 s patience, 4 cars →
    /// 1 washes, 3 wait; after 34 s DONE in bay 1 → the head drives in and the rest get +10 s (logged). Client (-autotest-patience):
    /// logs the rings (queue + bay, seconds, colour, WARN, edge, scale), screenshots near / looking away (edge arrows) / at 10 s,
    /// and the '떠남! -평판' popup. Logged as [HOSTSCN] patience: / [AUTOTEST] patience:.
    /// </summary>
    public static class DevChecksPatience
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-patience") >= 0;
        static void Log(string m) => DevAutopilot.Log("patience: " + m);
        static void HLog(string m) => Debug.Log("[HOSTSCN] patience: " + m);

        public static IEnumerator Host()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 2) yield return null;
            while (DayManager.Instance == null || QueueLane.Instance == null || LotState.Instance == null) yield return null;
            var dm = DayManager.Instance; var lane = QueueLane.Instance;
            QueueLane.PatienceOverride = 30f;
            DayManager.DayLengthOverride = 400f;
            yield return new WaitForSeconds(2f);
            foreach (var p in PlayerNet.All) if (p != null) p.Ready.Value = true;
            yield return new WaitForSeconds(0.5f);
            if (dm.State.Value == GameState.Lobby) dm.ServerAdvance();
            while (dm.State.Value != GameState.Day) yield return null;
            LotState.Instance.ServerSetOpenCount(1);
            int angry = 0; lane.VehicleAngry += _ => angry++;
            foreach (var id in new[] { "V01", "V02", "V03", "V04" })
            {
                Vehicle v = null; float tw = Time.time;
                while (v == null && Time.time - tw < 30f) { v = lane.ServerArrive(id, noGrease: true); if (v == null) yield return new WaitForSeconds(0.5f); }
                yield return new WaitForSeconds(1.5f);
            }
            float t0 = Time.time;
            while (lane.Active.Count(x => x.State.Value == VehicleState.Queued) < 2 && Time.time - t0 < 60f) yield return null;
            HLog("queued: " + string.Join(", ", lane.Active.Select(x => $"{x.DataId}:{x.State.Value} {x.PhaseElapsed:0}s")));
            yield return new WaitForSeconds(16f);
            var waiting = lane.Active.Where(x => x.State.Value == VehicleState.Queued).ToArray();
            var before = waiting.ToDictionary(x => x, x => x.PhaseElapsed);
            var bay = lane.Active.FirstOrDefault(x => x.State.Value == VehicleState.Washing);
            if (bay != null) lane.ServerFinish(bay);
            yield return new WaitForSeconds(3f);
            HLog("after DONE: " + string.Join(", ", before.Select(kv => $"{kv.Key.DataId} waited {kv.Value:0.0}s → {(kv.Key != null ? kv.Key.State.Value + " " + kv.Key.PhaseElapsed.ToString("0.0") + "s" : "gone")}")));
            t0 = Time.time;
            while (angry == 0 && Time.time - t0 < 60f) yield return null;
            HLog($"angry leaves {angry} after {Time.time - t0:0}s more");
            yield return new WaitForSeconds(8f);
            QueueLane.PatienceOverride = 0f; DayManager.DayLengthOverride = 0f;
            HLog("DONE");
        }

        public static IEnumerator Client(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            var cards = Object.FindFirstObjectByType<BayCards>();
            float t0 = Time.time;
            Vehicle q = null;
            while (Time.time - t0 < 120f && (q = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsSpawned && v.State.Value == VehicleState.Queued)) == null) yield return null;
            if (q == null || cards == null) { Log("FAIL no queued car / cards"); yield break; }
            yield return new WaitForSeconds(3f);
            // stand 9 m from the queue, looking at it
            var at = q.transform.position + q.transform.right * 9f;
            me.Motor.Teleport(new Vector3(at.x, 0.05f, at.z));
            yield return new WaitForSeconds(0.5f);
            me.Look.LookAt(q.transform.position + Vector3.up * 2f);
            yield return new WaitForSeconds(0.6f);
            Log("near: " + string.Join(" | ", cards.RingInfo));
            ScreenCapture.CaptureScreenshot("autotest_patience_near.png");
            yield return new WaitForSeconds(0.4f);
            // look the other way → edge arrows
            me.Look.LookAt(me.transform.position - (q.transform.position - me.transform.position) + Vector3.up * 1.5f);
            yield return new WaitForSeconds(0.6f);
            Log("away: " + string.Join(" | ", cards.RingInfo));
            ScreenCapture.CaptureScreenshot("autotest_patience_edge.png");
            yield return new WaitForSeconds(0.4f);
            me.Look.LookAt(q.transform.position + Vector3.up * 2f);
            // wait for a warning (≤ 10 s) and the leave popup
            t0 = Time.time; bool shotWarn = false; int popups0 = WorldPopup.Shown;
            while (Time.time - t0 < 90f)
            {
                var warn = cards.RingInfo.FirstOrDefault(s => s.Contains("WARN") && s.Contains("queue"));
                if (warn != null && !shotWarn)
                {
                    shotWarn = true;
                    var wv = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsSpawned && v.State.Value == VehicleState.Queued);
                    if (wv != null) me.Look.LookAt(wv.transform.position + Vector3.up * 2f);
                    yield return new WaitForSeconds(0.3f);
                    Log("warn: " + string.Join(" | ", cards.RingInfo));
                    ScreenCapture.CaptureScreenshot("autotest_patience_warn.png");
                }
                if (WorldPopup.Shown > popups0 && WorldPopup.LastText.Contains("떠남"))
                {
                    yield return new WaitForSeconds(0.25f);
                    ScreenCapture.CaptureScreenshot("autotest_patience_left.png");
                    Log($"popup '{WorldPopup.LastText}' toast '{Hud.Instance?.Day?.LastToast}' → OK");
                    break;
                }
                yield return null;
            }
            Log(shotWarn ? "PASS rings + warning + leave popup" : "FAIL no warning seen");
            input.scripted = false;
        }
    }
}
