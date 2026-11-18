using System.Collections;
using CarWash.Day;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// P7 release check, host side (-autotest -autotest-release on the hosting exe): marks the host ready, shortens the day
    /// so the summary arrives within minutes (the only shortcut — no forced clean %, no dev yard), logs the loop and takes
    /// the host's own screenshots.
    /// </summary>
    public static class DevReleaseScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] release: " + m);

        public static IEnumerator Run()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 2) yield return null;
            while (DayManager.Instance == null) yield return null;
            var dm = DayManager.Instance;
            DayManager.DayLengthOverride = 75f;
            DayManager.MaxOvertimeOverride = 20f;
            dm.DayLengthNet.Value = 75f;
            Log($"2 players, mode={Net.NetSession.Mode} code={Net.NetSession.JoinCode} ip={Net.NetSession.IpAddress}, devYard={(GameObject.Find("DevYard") != null)}");
            yield return new WaitForSeconds(2f);
            ScreenCapture.CaptureScreenshot("autotest_release_host_lobby.png");
            var last = dm.State.Value;
            float deadline = Time.time + 420f;
            while (Time.time < deadline)
            {
                var st = dm.State.Value;
                if (st != last)
                {
                    Log($"state {last} → {st} money={dm.Economy.Money.Value}");
                    if (st == GameState.Summary || st == GameState.WeekEnd || st == GameState.GameOver)
                    {
                        yield return new WaitForSeconds(1.5f);
                        ScreenCapture.CaptureScreenshot("autotest_release_host_summary.png");
                        Log("summary shown on host");
                        yield break;
                    }
                    last = st;
                }
                yield return null;
            }
            Log("timeout");
        }
    }
}
