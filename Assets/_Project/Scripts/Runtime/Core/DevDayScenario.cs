using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// T2.4 host side (DevAutopilot.DayMode): compressed days (40 s + 15 s overtime). The client starts Day 1 from the
    /// day board and presses DONE in bay 1; the host presses DONE in the other bays. Clean% is forced by
    /// DayManager.DevCleanOverride (0.9 = paid, day 3 = 0.5 → refused) because the dirt painting itself is covered by
    /// T1.3-T1.5. Runs W1 D1-D5 (boss on D5) → week end → W2 D1 with money forced low → bankrupt → retry.
    /// </summary>
    public static class DevDayScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] " + m);

        public static IEnumerator Run(int minPlayers = 2)
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < minPlayers) yield return null;
            while (DayManager.Instance == null || QueueLane.Instance == null) yield return null;
            var dm = DayManager.Instance;
            var lane = QueueLane.Instance;
            DayManager.DayLengthOverride = 40f;
            DayManager.MaxOvertimeOverride = 15f;
            DayManager.DevCleanOverride = 0.9f;
            QueueLane.PatienceOverride = 30f;
            dm.DayLengthNet.Value = 40f;
            Log($"day scenario ready: state={dm.State.Value} money={dm.Economy.Money.Value} rep={dm.Economy.Rep.Value:0.0}");

            // the client should start the day from the board; fall back after 60 s
            float t0 = Time.time;
            while (dm.State.Value == GameState.Lobby && Time.time - t0 < 60f) yield return null;
            if (dm.State.Value == GameState.Lobby) { Log("client did not start the day → host starts"); dm.ServerAdvance(); }
            else Log($"day started by the client after {Time.time - t0:0}s");

            bool sawBankrupt = false, retried = false;
            float deadline = Time.time + 900f;
            while (Time.time < deadline && !retried)
            {
                var st = dm.State.Value;
                if (st == GameState.Day)
                {
                    DayManager.DevCleanOverride = dm.DayNum.Value == 3 && dm.Week.Value == 1 ? 0.5f : (dm.Week.Value == 2 ? 0.3f : 0.9f);
                    // T2.5 check on W1D2: only bay 1 open and DONE held back → a second car waits in the queue (patience bar)
                    bool hold = dm.Week.Value == 1 && dm.DayNum.Value == 2;
                    int wantOpen = hold ? 1 : 3;
                    var lot = LotState.Instance;
                    if (lot != null && lot.OpenBayMask.Value != (1 << wantOpen) - 1) lot.ServerSetOpenCount(wantOpen);
                    // host presses DONE in bays 2/3 (the client handles bay 1 on day 1; later days the host does all)
                    foreach (var v in lane.Active.Where(v => v != null && v.State.Value == VehicleState.Washing && v.PhaseElapsed > (hold ? 26f : 4f)).ToArray())
                        if (v.BayIndex.Value != 0 || dm.DayNum.Value > 1 || dm.Week.Value > 1 || v.PhaseElapsed > 20f) lane.ServerFinish(v);
                    if (dm.Week.Value == 2 && !sawBankrupt && dm.Economy.Money.Value > 10) dm.Economy.Money.Value = 10; // force the bankrupt path
                }
                else if (st == GameState.GameOver) sawBankrupt = true;
                else if (sawBankrupt && st == GameState.Day) retried = true;
                yield return new WaitForSeconds(0.5f);
            }
            Log($"scenario end: bankrupt seen={sawBankrupt} retried={retried} state={dm.State.Value} W{dm.Week.Value}D{dm.DayNum.Value} money={dm.Economy.Money.Value}");
            DayManager.DevCleanOverride = -1f;
        }
    }
}
