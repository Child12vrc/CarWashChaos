using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Economy;
using CarWash.Player;
using CarWash.Vehicles;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// T2.4 client check (pairs with DevDayScenario on the host): start the day from the office day board (E), press the
    /// bay-1 DONE button (E), watch replicated money/rep/clock + toasts, read every summary panel, continue with Enter
    /// (RequestAdvance) through W1 D1-D5 → week end → bankrupt → retry.
    /// </summary>
    public static class DevChecksDay
    {
        static string Hud() => UI.Hud.Instance != null && UI.Hud.Instance.Day != null
            ? $"hud money='{UI.Hud.Instance.Day.MoneyText}' clock='{UI.Hud.Instance.Day.ClockText}'" : "no hud";

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var inter = me.GetComponent<PlayerInteractor>();
            input.scripted = true;
            float t0 = Time.time;
            while ((DayManager.Instance == null || EconomyState.Instance == null) && Time.time - t0 < 30f) yield return null;
            var dm = DayManager.Instance;
            var eco = EconomyState.Instance;
            if (dm == null) { DevAutopilot.Log("day: no DayManager"); yield break; }
            DevAutopilot.Log($"day: state={dm.State.Value} W{dm.Week.Value}D{dm.DayNum.Value} money={eco.Money.Value} rep={eco.Rep.Value:0.0} {Hud()}");

            // 0) T4.1 lobby: colour change + ready (R/C keys call these)
            int c0 = me.ColorIndex.Value;
            string lobbyBefore = UI.Hud.Instance?.Lobby?.Text;
            me.RequestNextColor();
            me.RequestToggleReady();
            float tr = Time.time;
            while ((!me.Ready.Value || me.ColorIndex.Value == c0) && Time.time - tr < 3f) yield return null;
            yield return new WaitForSeconds(0.2f);
            DevAutopilot.Log($"lobby: before [{lobbyBefore}] → colour {c0}→{me.ColorIndex.Value}, ready={me.Ready.Value}; now [{UI.Hud.Instance?.Lobby?.Text}]");
            ScreenCapture.CaptureScreenshot("autotest_lobby.png");
            // 1) day board → start
            var board = Object.FindObjectsByType<DayBoard>(FindObjectsSortMode.None).FirstOrDefault();
            if (board != null && dm.State.Value == GameState.Lobby)
            {
                me.Motor.Teleport(board.transform.position + new Vector3(0f, -1.55f, 1.8f));
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(board.transform.position);
                yield return new WaitForSeconds(0.4f);
                string prompt = inter.PromptText();
                input.sInteract = true;
                float tw = Time.time;
                while (dm.State.Value != GameState.Day && Time.time - tw < 5f) yield return null;
                DevAutopilot.Log($"day board: prompt='{prompt}' → state={dm.State.Value} after {Time.time - tw:0.0}s; {Hud()}");
            }

            // 2) bay 1 DONE button
            var btn = Object.FindObjectsByType<BayButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.BayIndex == 0);
            if (btn != null)
            {
                me.Motor.Teleport(btn.transform.position + new Vector3(-1.3f, -1.25f, 0f));
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(btn.transform.position);
                float tw = Time.time;
                while (btn.WashingCar == null && Time.time - tw < 40f) { me.Look.LookAt(btn.transform.position); yield return null; }
                var car = btn.WashingCar;
                yield return new WaitForSeconds(3f);
                me.Look.LookAt(btn.transform.position);
                yield return new WaitForSeconds(0.2f);
                string prompt = inter.PromptText();
                int money0 = eco.Money.Value;
                input.sInteract = true;
                yield return new WaitForSeconds(0.6f);
                var stateAfter = car != null ? car.State.Value : VehicleState.Arriving;
                float tp = Time.time;
                while (eco.Money.Value == money0 && Time.time - tp < 6f) yield return null;
                DevAutopilot.Log($"bay1 DONE: car={(car != null ? car.DataId : "none")} prompt='{prompt}' → {stateAfter}; money {money0}→{eco.Money.Value}, rep {eco.Rep.Value:0.00}, toast='{UI.Hud.Instance?.Day?.LastToast}'");
                me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f)); // out of the bay lanes
            }

            // 3) every summary → read, screenshot, continue
            bool sawGameOver = false, sawWeekEnd = false;
            int summaries = 0;
            t0 = Time.time;
            while (Time.time - t0 < 900f)
            {
                var st = dm.State.Value;
                if (st == GameState.Summary || st == GameState.GameOver || st == GameState.WeekEnd)
                {
                    yield return new WaitForSeconds(1.2f);
                    var s = dm.Summary.Value;
                    DevAutopilot.Log($"{st} W{s.week}D{s.day}: served {s.served} refused {s.refused} angry {s.angry} sentAway {s.sentAway} earned {s.earned} rent {s.rent} money {s.moneyStart}→{s.moneyEnd} rep {s.repStart:0.0}→{s.repEnd:0.0} bankrupt={s.bankrupt} | panel: {UI.Hud.Instance?.Day?.SummaryText}");
                    if (st == GameState.Summary && summaries == 0) ScreenCapture.CaptureScreenshot("autotest_summary.png");
                    if (st == GameState.GameOver) { sawGameOver = true; ScreenCapture.CaptureScreenshot("autotest_bankrupt.png"); }
                    if (st == GameState.WeekEnd) sawWeekEnd = true;
                    if (st == GameState.Summary) summaries++;
                    yield return new WaitForSeconds(0.5f);
                    dm.RequestAdvance(); // same as pressing Enter on the panel
                    float tw = Time.time;
                    while (dm.State.Value == st && Time.time - tw < 5f) yield return null;
                    if (sawGameOver && dm.State.Value == GameState.Day) break;
                }
                else if (st == GameState.Day && dm.Week.Value == 1 && dm.DayNum.Value == 2 && dm.Elapsed > 26f && dm.Elapsed < 26.6f)
                {
                    // T2.5: bay cards + patience bar over the queued car
                    var queued = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).FirstOrDefault(v => v.State.Value == VehicleState.Queued);
                    if (queued != null)
                    {
                        me.Motor.Teleport(queued.transform.position + new Vector3(7f, 0.05f, -4f));
                        yield return new WaitForSeconds(0.3f);
                        me.Look.LookAt(queued.transform.position + Vector3.up * 1.5f);
                        yield return new WaitForSeconds(0.4f);
                    }
                    var bays = UI.Hud.Instance.Bays;
                    DevAutopilot.Log($"bay cards: [{bays.CardText(0)}] [{bays.CardText(1)}] [{bays.CardText(2)}]; queued={(queued != null ? queued.DataId : "none")} patience bars visible={bays.VisiblePatienceBars}");
                    ScreenCapture.CaptureScreenshot("autotest_bays.png");
                    yield return new WaitForSeconds(0.7f);
                    if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-hud") >= 0) { input.scripted = false; yield break; } // HUD-only run
                    me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f));
                }
                else if (st == GameState.Day && dm.Elapsed > 10f && dm.Elapsed < 10.6f)
                {
                    DevAutopilot.Log($"W{dm.Week.Value}D{dm.DayNum.Value} running: {Hud()} rep={eco.Rep.Value:0.00} cars={Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).Length}");
                    if (dm.Week.Value == 1 && dm.DayNum.Value == 1) ScreenCapture.CaptureScreenshot("autotest_dayhud.png");
                    yield return new WaitForSeconds(0.7f);
                }
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            DevAutopilot.Log($"day flow: summaries={summaries} weekEnd={sawWeekEnd} bankrupt={sawGameOver} → retry state={dm.State.Value} W{dm.Week.Value}D{dm.DayNum.Value} money={eco.Money.Value} rep={eco.Rep.Value:0.0}");
            input.scripted = false;
        }
    }
}
