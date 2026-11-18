using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Dirt;
using CarWash.Economy;
using CarWash.Player;
using CarWash.Tools;
using CarWash.Vehicles;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// P7 release check (client, -autotest-release; pairs with DevReleaseScenario on the host). Plays the real loop with
    /// no dev shortcuts: lobby (ready) → day board [E] → take the bay-1 washer → really wash the customer car → DONE →
    /// payment → day summary. Screenshots: autotest_release_lobby/wash/paid/summary.png.
    /// </summary>
    public static class DevChecksRelease
    {
        static void Log(string m) => DevAutopilot.Log("release: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var inter = me.GetComponent<PlayerInteractor>();
            var tools = me.GetComponent<PlayerTools>();
            input.scripted = true;
            float t0 = Time.time;
            while ((DayManager.Instance == null || EconomyState.Instance == null) && Time.time - t0 < 30f) yield return null;
            var dm = DayManager.Instance; var eco = EconomyState.Instance;
            if (dm == null) { Log("FAIL no DayManager"); yield break; }
            Log($"joined: mode={Net.NetSession.Mode} code={Net.NetSession.JoinCode} ip={Net.NetSession.IpAddress} state={dm.State.Value} money={eco.Money.Value} devYard={(GameObject.Find("DevYard") != null)}");

            // 1) lobby: ready
            me.RequestToggleReady();
            float tr = Time.time;
            while (!me.Ready.Value && Time.time - tr < 5f) yield return null;
            yield return new WaitForSeconds(0.5f);
            Log($"lobby: ready={me.Ready.Value} [{UI.Hud.Instance?.Lobby?.Text}] invite=[{UI.Hud.Instance?.Invite?.Text}]");
            ScreenCapture.CaptureScreenshot("autotest_release_lobby.png");
            yield return new WaitForSeconds(0.5f);

            // 2) day board [E]
            var board = Object.FindObjectsByType<DayBoard>(FindObjectsSortMode.None).FirstOrDefault();
            float tw = Time.time;
            while (board != null && dm.State.Value == GameState.Lobby && Time.time - tw < 20f)
            {
                me.Motor.Teleport(board.transform.position + new Vector3(0f, -1.55f, 1.8f));
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(board.transform.position);
                yield return new WaitForSeconds(0.4f);
                string prompt = inter.PromptText();
                input.sInteract = true;
                float tb = Time.time;
                while (dm.State.Value == GameState.Lobby && Time.time - tb < 3f) yield return null;
                Log($"day board: prompt='{prompt}' → state={dm.State.Value}");
            }
            if (dm.State.Value != GameState.Day) { Log($"FAIL day did not start (state={dm.State.Value})"); input.scripted = false; yield break; }

            // 3) take the bay-1 washer
            var btn = Object.FindObjectsByType<BayButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.BayIndex == 0);
            var washer = Object.FindObjectsByType<PressureWasher>(FindObjectsSortMode.None).Where(w => !w.IsHeld)
                .OrderBy(w => btn != null ? (w.transform.position - btn.transform.position).sqrMagnitude : 0f).FirstOrDefault();
            if (washer == null || btn == null) { Log("FAIL no bay-1 washer/button"); input.scripted = false; yield break; }
            me.Motor.Teleport(washer.transform.position + new Vector3(0f, -washer.transform.position.y + 0.05f, -1.3f));
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(washer.transform.position);
            yield return new WaitForSeconds(0.3f);
            input.sInteract = true;
            yield return new WaitForSeconds(0.8f);
            Log($"washer equipped={tools.Equipped == washer}");

            // 4) wait for a customer car in bay 1, then really wash it
            tw = Time.time;
            while (btn.WashingCar == null && Time.time - tw < 60f) yield return null;
            var car = btn.WashingCar;
            if (car == null) { Log("FAIL no car reached bay 1"); input.scripted = false; yield break; }
            var dirt = car.GetComponent<DirtNetSync>();
            yield return new WaitForSeconds(1f);
            float clean0 = dirt != null ? dirt.CleanPct.Value : -1f;
            var c0 = car.transform.position; var right = car.transform.right; var fwd = car.transform.forward;
            bool shot = false;
            foreach (var side in new[] { right * 2.0f, -right * 2.0f, fwd * 3.4f, -fwd * 3.4f, right * 2.0f, -right * 2.0f })
            {
                me.Motor.Teleport(c0 + side + Vector3.up * 0.05f);
                yield return new WaitForSeconds(0.2f);
                input.sUse = true;
                for (float t = 0; t < 4f; t += Time.deltaTime)
                {
                    me.Look.LookAt(c0 - side * 0.35f + new Vector3(0f, 0.3f + Mathf.PingPong(t * 0.8f, 1.1f), 0f) + Vector3.Cross(side.normalized, Vector3.up) * Mathf.Sin(t * 2.1f) * 2.0f);
                    if (!shot && t > 2f) { shot = true; ScreenCapture.CaptureScreenshot("autotest_release_wash.png"); }
                    yield return null;
                }
                input.sUse = false;
            }
            yield return new WaitForSeconds(1f);
            float clean1 = dirt != null ? dirt.CleanPct.Value : -1f;
            Log($"washed {car.DataId}: clean {clean0 * 100f:0}% → {clean1 * 100f:0}% (server)");

            // 5) DONE → inspection → payment
            me.Motor.Teleport(btn.transform.position + new Vector3(-1.3f, -1.25f, 0f));
            yield return new WaitForSeconds(0.4f);
            me.Look.LookAt(btn.transform.position);
            yield return new WaitForSeconds(0.3f);
            string donePrompt = inter.PromptText();
            int money0 = eco.Money.Value;
            input.sInteract = true;
            float tp = Time.time;
            while (eco.Money.Value == money0 && Time.time - tp < 10f && car != null && car.State.Value != VehicleState.Leaving) yield return null;
            yield return new WaitForSeconds(0.6f);
            Log($"DONE prompt='{donePrompt}' → money {money0}→{eco.Money.Value}, rep {eco.Rep.Value:0.00}, toast='{UI.Hud.Instance?.Day?.LastToast}'");
            ScreenCapture.CaptureScreenshot("autotest_release_paid.png");
            me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f));

            // 6) day end → summary (settlement)
            tw = Time.time;
            while (dm.State.Value == GameState.Day && Time.time - tw < 240f) yield return null;
            yield return new WaitForSeconds(1.5f);
            var s = dm.Summary.Value;
            Log($"{dm.State.Value} W{s.week}D{s.day}: served {s.served} refused {s.refused} angry {s.angry} earned {s.earned} rent {s.rent} money {s.moneyStart}→{s.moneyEnd} | panel: {UI.Hud.Instance?.Day?.SummaryText}");
            ScreenCapture.CaptureScreenshot("autotest_release_summary.png");
            yield return new WaitForSeconds(0.5f);
            Log(dm.State.Value == GameState.Summary || dm.State.Value == GameState.WeekEnd || dm.State.Value == GameState.GameOver ? "PASS full loop" : "FAIL no summary");
            input.scripted = false;
        }
    }
}
