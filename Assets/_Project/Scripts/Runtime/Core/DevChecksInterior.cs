using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Player;
using CarWash.Vehicles;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.6 D3 check (client: -autotest -autotest-interior; host observes with DevReleaseScenario). Starts the day, waits for the
    /// first customer in bay 1: the car has an Interior, every interior trash item lies inside the cabin (on a seat / the floor),
    /// then opens the driver door with [E] and takes screenshots looking in (and through the windscreen). Logged as
    /// [AUTOTEST] interior:.
    /// </summary>
    public static class DevChecksInterior
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-interior") >= 0;
        static void Log(string m) => DevAutopilot.Log("interior: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            float t0 = Time.time;
            while (DayManager.Instance == null && Time.time - t0 < 30f) yield return null;
            var dm = DayManager.Instance;
            me.RequestToggleReady();
            yield return new WaitForSeconds(1f);
            var board = Object.FindObjectsByType<DayBoard>(FindObjectsSortMode.None).FirstOrDefault();
            float tw = Time.time;
            while (board != null && dm.State.Value == GameState.Lobby && Time.time - tw < 20f)
            {
                me.Motor.Teleport(board.transform.position + new Vector3(0f, -1.55f, 1.8f));
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(board.transform.position);
                yield return new WaitForSeconds(0.4f);
                input.sInteract = true;
                yield return new WaitForSeconds(2f);
            }
            var btn = Object.FindObjectsByType<BayButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.BayIndex == 0);
            tw = Time.time;
            while ((btn == null || btn.WashingCar == null) && Time.time - tw < 90f) yield return null;
            var car = btn != null ? btn.WashingCar : null;
            if (car == null) { Log("FAIL no car arrived"); yield break; }
            yield return new WaitForSeconds(1.5f);

            var interior = car.transform.Find("Interior");
            var rends = interior != null ? interior.GetComponentsInChildren<Renderer>() : new Renderer[0];
            var cabin = rends.Length > 0 ? rends.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds();
            cabin.Expand(0.15f);
            var trash = Object.FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => t.Attached && (t.transform.position - car.transform.position).sqrMagnitude < 36f).ToArray();
            int inside = trash.Count(t => cabin.Contains(t.transform.position));
            Log($"{car.DataId}: interior={(interior != null)} parts={rends.Length} trash {inside}/{trash.Length} inside the cabin " +
                string.Join(" ", trash.Select(t => car.transform.InverseTransformPoint(t.transform.position).ToString("F2"))) + $" → {(interior != null && inside == trash.Length ? "OK" : "FAIL")}");

            // open the driver door and look in
            var door = car.GetComponentsInChildren<VehicleDoor>().FirstOrDefault(d => d.name.Contains("Door_L")) ?? car.GetComponentsInChildren<VehicleDoor>().FirstOrDefault(d => !d.IsTrunk);
            if (door != null)
            {
                var dc = door.GetComponent<Collider>() != null ? door.GetComponent<Collider>().bounds.center : door.transform.position;
                var outward = (dc - car.transform.position); outward.y = 0f; outward = Vector3.Project(outward, car.transform.right).normalized;
                me.Motor.Teleport(new Vector3(dc.x, 0.05f, dc.z) + outward * 1.3f - car.transform.forward * 0.6f);
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(dc);
                yield return new WaitForSeconds(0.3f);
                input.sInteract = true;
                float to = Time.time; while (!door.Open.Value && Time.time - to < 3f) yield return null;
                yield return new WaitForSeconds(0.8f);
                me.Look.LookAt(car.transform.Find("Interior").position + Vector3.up * 0.3f);
                yield return new WaitForSeconds(0.4f);
                ScreenCapture.CaptureScreenshot($"autotest_interior_{car.DataId}_door.png");
                yield return null;
                Log($"{car.DataId}: driver door open={door.Open.Value}, trash visible={trash.Count(t => t.GetComponentInChildren<Renderer>() != null && t.GetComponentInChildren<Renderer>().enabled)}/{trash.Length}");
            }
            // through the windscreen
            var front = car.transform.position + car.transform.forward * 4f + Vector3.up * 0.05f;
            me.Motor.Teleport(front);
            yield return new WaitForSeconds(0.4f);
            me.Look.LookAt(car.transform.position + Vector3.up * 1.0f);
            yield return new WaitForSeconds(0.4f);
            ScreenCapture.CaptureScreenshot($"autotest_interior_{car.DataId}_front.png");
            yield return new WaitForSeconds(0.3f);
            input.scripted = false;
        }
    }
}
