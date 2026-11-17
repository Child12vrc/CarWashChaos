using System;
using System.Collections;
using System.Linq;
using CarWash.Dirt;
using CarWash.Economy;
using CarWash.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarWash.Core
{
    /// <summary>
    /// M4 client check (-autotest-net, run on 3 clients with the editor host = 4 players): sees every player and their
    /// distinct colours, lobby ready/colour, tries (and must fail) to change the money, pings (all peers see them),
    /// same dirt % as the server (incl. the T4.3 late-join snapshot), F3 overlay.
    /// </summary>
    public static class DevChecksNet
    {
        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            int want = 4;
            float t0 = Time.time;
            while (Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Length < want && Time.time - t0 < 90f) yield return null;
            var ps = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).OrderBy(p => p.OwnerClientId).ToArray();
            DevAutopilot.Log($"net: sees {ps.Length} players (ids {string.Join(",", ps.Select(p => p.OwnerClientId))}) colours [{string.Join(",", ps.Select(p => p.ColorIndex.Value))}] distinct={ps.Select(p => p.ColorIndex.Value).Distinct().Count() == ps.Length} after {Time.time - t0:0}s");

            // walk a bit so everyone sees everyone move
            me.Motor.Teleport(new Vector3(-6f + me.OwnerClientId * 2.5f, 0.05f, -9f));
            yield return new WaitForSeconds(0.5f);
            var others = ps.Where(p => p != me).ToArray();
            var before = others.Select(p => p.transform.position).ToArray();
            input.sMove = new Vector2(0f, 1f);
            yield return new WaitForSeconds(1.2f);
            input.sMove = Vector2.zero;
            yield return new WaitForSeconds(4f);
            int moved = others.Where((p, i) => (p.transform.position - before[i]).magnitude > 0.5f).Count();
            DevAutopilot.Log($"net: {moved}/{others.Length} other players moved while watching");

            // lobby
            me.RequestToggleReady();
            yield return new WaitForSeconds(1f);
            DevAutopilot.Log($"net lobby: [{UI.Hud.Instance?.Lobby?.Text}]");

            // money is server-only
            var eco = EconomyState.Instance;
            int m0 = eco.Money.Value; string err = "none";
            try { eco.Money.Value = 99999; } catch (Exception e) { err = e.GetType().Name; }
            yield return new WaitForSeconds(1.5f);
            DevAutopilot.Log($"net: client wrote Money=99999 → exception={err}, money now {eco.Money.Value} (was {m0}); server keeps authority={eco.Money.Value == m0 || err != "none"}");

            // ping something (the V04 test car) — every peer gets a marker
            var car = Object.FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None).OrderBy(d => (d.transform.position - DevYard.CarV04).sqrMagnitude).First();
            me.Motor.Teleport(car.transform.position + new Vector3(-3f - me.OwnerClientId * 0.8f, 0.05f, -4f));
            yield return new WaitForSeconds(0.4f);
            me.Look.LookAt(car.transform.position + Vector3.up * 0.8f);
            yield return new WaitForSeconds(0.3f);
            me.GetComponent<PlayerPing>().OwnerPing();
            yield return new WaitForSeconds(2.5f);
            DevAutopilot.Log($"net ping: markers visible here={UI.Hud.Instance?.Pings?.Active} last label='{UI.Hud.Instance?.Pings?.LastLabel}'");
            ScreenCapture.CaptureScreenshot($"autotest_ping_{me.OwnerClientId}.png");

            // dirt: local painter vs server % (late joiners got the history snapshot)
            float tw = Time.time;
            while (Time.time - tw < 8f && (!car.Painter.HasReadback)) yield return null;
            yield return new WaitForSeconds(2f);
            DevAutopilot.Log($"net dirt {car.name}: server {car.CleanPct.Value * 100f:0.0}% local {car.Painter.CleanPct * 100f:0.0}% (Δ {Mathf.Abs(car.CleanPct.Value - car.Painter.CleanPct) * 100f:0.0}) snapshot strokes={car.SnapshotReceived} done={car.SnapshotDone}");

            // F3
            UI.Hud.Instance.DebugView.Toggle();
            yield return new WaitForSeconds(0.6f);
            DevAutopilot.Log($"F3 overlay: visible={UI.Hud.Instance.DebugView.Visible} → {UI.Hud.Instance.DebugView.Text?.Replace("\n", " / ")}");
            ScreenCapture.CaptureScreenshot($"autotest_f3_{me.OwnerClientId}.png");
            yield return new WaitForSeconds(0.5f);
            UI.Hud.Instance.DebugView.Toggle();
            input.scripted = false;
        }
    }
}
