using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Player;
using CarWash.Visual;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.9 G1/G7 check (client: -autotest -autotest-release -autotest-lights [-time 20.5]; host -autotest-release, same -time).
    /// Walks to bay 1's wall switch, presses [E] twice and checks the shared state flips (client view of the NetworkVariable)
    /// and the bay's ceiling lights follow; screenshots of the bay lit / dark. Logged as [AUTOTEST] lights:.
    /// </summary>
    public static class DevChecksLights
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-lights") >= 0;
        static void Log(string m) => DevAutopilot.Log("lights: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            float t0 = Time.time;
            BayLightSwitch sw = null;
            while (Time.time - t0 < 30f && (sw = Object.FindObjectsByType<BayLightSwitch>(FindObjectsSortMode.None).FirstOrDefault(s => s.IsSpawned && s.BayIndex == 0)) == null) yield return null;
            if (sw == null) { Log("FAIL no switch for bay 1"); yield break; }
            yield return new WaitForSeconds(2f);
            var bl = BayLights.ForBay(0);
            Log($"start: hour {DayNightCycle.Hour:0.0} night {DayNightCycle.Night:0.00} switch on={sw.On.Value} bay lights level {(bl != null ? bl.Level : -1f):0.00}, night lights lit {NightLight.LitCount}/{NightLight.All.Count}");
            // stand in front of the switch (it faces -X) and look at it
            me.Motor.Teleport(sw.transform.position + new Vector3(-1.3f, -1.5f, 0f));
            yield return new WaitForSeconds(0.5f);
            me.Look.LookAt(sw.transform.position);
            yield return new WaitForSeconds(0.4f);
            bool ok = true;
            for (int k = 0; k < 2; k++)
            {
                bool before = sw.On.Value;
                ScreenCapture.CaptureScreenshot($"autotest_lights_{k}_{(before ? "on" : "off")}.png");
                yield return new WaitForSeconds(0.3f);
                string prompt = me.GetComponent<PlayerInteractor>() != null ? me.GetComponent<PlayerInteractor>().PromptText() : "";
                input.sInteract = true;
                float tw = Time.time; while (sw.On.Value == before && Time.time - tw < 3f) yield return null;
                yield return new WaitForSeconds(0.5f);
                bool flipped = sw.On.Value != before;
                bool follows = bl != null && Mathf.Approximately(bl.Level, sw.On.Value ? 1f : 0f);
                ok &= flipped && follows;
                Log($"press {k + 1}: prompt '{prompt}' switch {before} → {sw.On.Value} in {Time.time - tw:0.00}s, bay lights level {(bl != null ? bl.Level : -1f):0.00} → {(flipped && follows ? "OK" : "FAIL")}");
            }
            // look into the bay for a lit / dark comparison
            me.Motor.Teleport(sw.transform.position + new Vector3(-4f, -1.5f, 3.5f));
            yield return new WaitForSeconds(0.4f);
            me.Look.LookAt(sw.transform.position + new Vector3(-3f, -1f, 6f));
            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot($"autotest_lights_bay_{(sw.On.Value ? "on" : "off")}.png");
            yield return new WaitForSeconds(0.3f);
            Log(ok ? "PASS switch synced + lights follow" : "FAIL");
            input.scripted = false;
        }
    }
}
