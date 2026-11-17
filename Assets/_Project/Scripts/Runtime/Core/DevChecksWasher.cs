using System.Collections;
using System.Linq;
using CarWash.Dirt;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>T1.4 automated check: pick up the washer, push cones, clean the test car (logged as [AUTOTEST]).</summary>
    public static class DevChecksWasher
    {
        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var tools = me.GetComponent<PlayerTools>();
            input.scripted = true;
            var washer = Object.FindObjectsByType<PressureWasher>(FindObjectsSortMode.None).Where(w => !w.IsHeld)
                .OrderBy(w => (w.transform.position - DevYard.WasherReel).sqrMagnitude).FirstOrDefault(); // the dev-yard washer
            if (washer == null) { DevAutopilot.Log("washer not found"); yield break; }

            me.Motor.Teleport(washer.transform.position + new Vector3(1.4f, -washer.transform.position.y + 0.05f, 0f));
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(washer.transform.position);
            yield return new WaitForSeconds(0.3f);
            input.sInteract = true;
            yield return new WaitForSeconds(0.6f);
            DevAutopilot.Log($"washer equipped = {tools.Equipped == washer}");
            if (tools.Equipped != washer) { input.scripted = false; yield break; }

            // push the cone row
            var cones = Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None).Where(g => g.name.StartsWith("Cone")).ToArray();
            if (cones.Length > 0)
            {
                var center = cones.Aggregate(Vector3.zero, (a, c) => a + c.transform.position) / cones.Length;
                var start = cones.Select(c => c.transform.position).ToArray();
                me.Motor.Teleport(center + new Vector3(0, -center.y + 0.05f, 3.5f)); // from the north (office is south)
                yield return new WaitForSeconds(0.3f);
                me.Look.LookAt(center);
                input.sUse = true;
                for (float t = 0; t < 2f; t += Time.deltaTime) { me.Look.SetYaw(180f + Mathf.Sin(t * 3f) * 25f); yield return null; }
                input.sUse = false;
                yield return new WaitForSeconds(1f);
                float moved = cones.Select((c, i) => (c.transform.position - start[i]).magnitude).Average();
                DevAutopilot.Log($"cones pushed avg {moved:0.00} m ({cones.Length} cones)");
            }

            // clean the test car: sweep across its side for a few seconds
            var car = Object.FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None).FirstOrDefault(s => s.name.Contains("V02"));
            if (car == null) { DevAutopilot.Log("no dirty car"); input.scripted = false; yield break; }
            yield return new WaitForSeconds(1f);
            float before = car.CleanPct.Value;
            float localBefore = car.Painter.CleanPct;
            var c0 = car.transform.position;
            foreach (var side in new[] { new Vector3(-2.4f, 0, 0), new Vector3(2.4f, 0, 0), new Vector3(0, 0, -4f), new Vector3(0, 0, 4f) })
            {
                me.Motor.Teleport(c0 + side + Vector3.up * 0.05f);
                yield return new WaitForSeconds(0.2f);
                input.sUse = true;
                int onTarget = 0, frames = 0;
                bool shot = false;
                for (float t = 0; t < 4f; t += Time.deltaTime)
                {
                    me.Look.LookAt(c0 + new Vector3(Mathf.Sin(t * 2.3f) * 2.2f, 0.4f + Mathf.PingPong(t * 0.9f, 1.2f), Mathf.Cos(t * 1.7f) * 2.2f));
                    if (UI.Hud.Instance != null) { frames++; if (UI.Hud.Instance.CrosshairOnTarget) onTarget++; }
                    if (!shot && t > 1.5f) { shot = true; ScreenCapture.CaptureScreenshot("autotest_washer.png"); }
                    yield return null;
                }
                DevAutopilot.Log($"crosshair on dirtable target {onTarget}/{frames} frames (side {side})");
                input.sUse = false;
            }
            yield return new WaitForSeconds(1.5f);
            DevAutopilot.Log($"car clean: server {before * 100f:0.0}% → {car.CleanPct.Value * 100f:0.0}%  | local painter {localBefore * 100f:0.0}% → {car.Painter.CleanPct * 100f:0.0}%  (mud left {car.Painter.MudPct * 100f:0.0}%)");
            input.scripted = false;
        }
    }
}
