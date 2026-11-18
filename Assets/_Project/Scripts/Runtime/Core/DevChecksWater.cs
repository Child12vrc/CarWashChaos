using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.6 D6 + D7 check (client: -autotest -autotest-release -autotest-water; host observes with DevReleaseScenario).
    /// D7: bay-1 washer on one floor spot → wet patch, then a puddle; on a bucket → it gets wet (darker, glossier) and is pushed;
    /// patches dry. D6: start the day, wash one side of the first customer car, close-up screenshots of the dusty vs. washed
    /// (wet, glossy, sparkling) paint. Logged as [AUTOTEST] water:.
    /// </summary>
    public static class DevChecksWater
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-water") >= 0;
        static void Log(string m) => DevAutopilot.Log("water: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var tools = me.GetComponent<PlayerTools>();
            input.scripted = true;
            yield return new WaitForSeconds(2f);
            var btn = Object.FindObjectsByType<BayButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.BayIndex == 0);
            var washer = Object.FindObjectsByType<PressureWasher>(FindObjectsSortMode.None).Where(w => !w.IsHeld)
                .OrderBy(w => btn != null ? (w.transform.position - btn.transform.position).sqrMagnitude : 0f).FirstOrDefault();
            if (washer == null) { Log("FAIL no washer"); yield break; }
            me.Motor.Teleport(washer.transform.position + new Vector3(1.2f, -washer.transform.position.y + 0.05f, 0f));
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(washer.GetComponent<Collider>().bounds.center);
            yield return new WaitForSeconds(0.3f);
            input.sInteract = true;
            yield return new WaitForSeconds(0.8f);
            Log($"washer equipped={tools.Equipped is PressureWasher}");

            // 1) floor of bay 1: one spot for 3 s → patch → puddle
            var bay = Vehicles.LotPaths.Instance.Bays[0].transform.position;
            var spot = bay + new Vector3(-1.2f, 0f, -2f);
            me.Motor.Teleport(spot + new Vector3(0f, 0.05f, -1.8f));
            yield return new WaitForSeconds(0.3f);
            float t0 = Time.time;
            while (Time.time - t0 < 3f) { me.Look.LookAt(spot); input.sUse = true; yield return null; }
            input.sUse = false;
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(spot);
            yield return new WaitForSeconds(0.3f);
            ScreenCapture.CaptureScreenshot("autotest_water_floor.png");
            Log($"floor: wet patches {WaterFx.ActivePatches}, puddles {WaterFx.ActivePuddles} → {(WaterFx.ActivePatches > 0 && WaterFx.ActivePuddles > 0 ? "OK" : "FAIL")}");

            // 2) a bucket (light prop): wet + pushed
            var bucket = Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None).Where(g => g.name.StartsWith("Bucket"))
                .OrderBy(g => (g.transform.position - bay).sqrMagnitude).FirstOrDefault();
            if (bucket != null)
            {
                // carry it onto open floor in front of bay 1 (it stands in a corner), drop it, step back and spray it
                var grabber = me.GetComponent<PlayerGrabber>();
                for (int s = 0; s < PlayerInventory.SlotCount; s++) if (tools.Inventory.Get(s) == null) { input.sSlot = s; break; } // hands free to grab
                yield return new WaitForSeconds(0.4f);
                var bp = bucket.transform.position;
                me.Motor.Teleport(new Vector3(bp.x, 0.05f, bp.z) + (bay - bp).normalized * 1.4f);
                yield return new WaitForSeconds(0.3f);
                me.Look.LookAt(bucket.GetComponent<Collider>().bounds.center);
                yield return new WaitForSeconds(0.2f);
                input.sGrab = true; yield return new WaitForSeconds(0.3f); input.sGrab = false;
                yield return new WaitForSeconds(0.5f);
                var target = bay + new Vector3(0f, 0.05f, -4f);
                var cur = me.transform.position;
                while ((new Vector3(target.x - cur.x, 0, target.z - cur.z)).magnitude > 0.4f) { cur = Vector3.MoveTowards(cur, target, 0.12f); cur.y = 0.05f; me.Motor.Teleport(cur); yield return new WaitForFixedUpdate(); }
                input.sDrop = true; yield return new WaitForSeconds(1.2f);
                Log($"bucket placed at {bucket.transform.position.ToString("F1")} (held before drop={grabber.Held == null})");
                var b0 = bucket.transform.position;
                var from = b0 + Vector3.back * 2.2f; from.y = 0.05f;
                for (int s = 0; s < PlayerInventory.SlotCount; s++) if (tools.Inventory.Get(s) is PressureWasher) { input.sSlot = s; break; }
                yield return new WaitForSeconds(0.4f);
                me.Motor.Teleport(from);
                yield return new WaitForSeconds(0.3f);
                t0 = Time.time;
                while (Time.time - t0 < 2f) { me.Look.LookAt(bucket.GetComponent<Collider>().bounds.center); input.sUse = true; yield return null; }
                input.sUse = false;
                yield return new WaitForSeconds(0.4f);
                var w = bucket.GetComponent<WetObject>();
                ScreenCapture.CaptureScreenshot("autotest_water_bucket.png");
                Log($"bucket: pushed {Vector3.Distance(b0, bucket.transform.position):0.00} m, wetness {(w != null ? w.Wetness : 0f):0.00} → {(w != null && w.Wetness > 0.5f ? "wet" : "DRY")}");
                yield return new WaitForSeconds(WetObject.DryTime + 1f);
                Log($"bucket after {WetObject.DryTime + 1f:0} s: wetness {(w != null ? w.Wetness : 0f):0.00}; floor patches {WaterFx.ActivePatches} (puddles {WaterFx.ActivePuddles}) — patches dry in {WaterFx.PatchDry} s, puddles in {WaterFx.PuddleDry} s");
            }

            // 3) D6: start the day, wash the left side of the first car, close-ups
            var dm = DayManager.Instance;
            me.RequestToggleReady();
            yield return new WaitForSeconds(1f);
            var board = Object.FindObjectsByType<DayBoard>(FindObjectsSortMode.None).FirstOrDefault();
            float tw = Time.time;
            while (board != null && dm.State.Value == GameState.Lobby && Time.time - tw < 20f)
            {
                // the hose keeps us at bay 1: put the washer away first
                for (int s = 0; s < PlayerInventory.SlotCount; s++) if (tools.Inventory.Get(s) == null) { input.sSlot = s; break; }
                yield return new WaitForSeconds(0.3f);
                me.Motor.Teleport(board.transform.position + new Vector3(0f, -1.55f, 1.8f));
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(board.transform.position);
                yield return new WaitForSeconds(0.3f);
                input.sInteract = true;
                yield return new WaitForSeconds(2f);
            }
            tw = Time.time;
            while ((btn.WashingCar == null) && Time.time - tw < 90f) yield return null;
            var car = btn.WashingCar;
            if (car == null) { Log("FAIL no car"); yield break; }
            for (int s = 0; s < PlayerInventory.SlotCount; s++) if (tools.Inventory.Get(s) is PressureWasher) { input.sSlot = s; break; }
            yield return new WaitForSeconds(0.5f);
            var c = car.transform;
            var side = c.position - c.right * 2.6f; side.y = 0.05f;
            me.Motor.Teleport(side);
            yield return new WaitForSeconds(0.3f);
            ScreenCapture.CaptureScreenshot("autotest_water_car_dirty.png");
            yield return new WaitForSeconds(0.2f);
            t0 = Time.time;
            while (Time.time - t0 < 4f)
            {
                float k = (Time.time - t0) / 4f;
                me.Look.LookAt(c.position + c.forward * Mathf.Lerp(-0.2f, 1.8f, k) + Vector3.up * (0.6f + 0.25f * Mathf.Sin(k * 20f)));
                input.sUse = true; yield return null;
            }
            input.sUse = false;
            me.Motor.Teleport(c.position - c.right * 2.4f + c.forward * 2.8f + Vector3.up * 0.05f);
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(c.position + Vector3.up * 0.6f);
            yield return new WaitForSeconds(0.4f);
            ScreenCapture.CaptureScreenshot("autotest_water_car_half.png");
            Log($"car {car.DataId}: half washed, clean {car.Dirt.CleanPct.Value * 100f:0}% (mud {car.Dirt.Painter.MudPct * 100f:0}%, spots {car.Dirt.Painter.SpotsPct * 100f:0}%)");
            yield return new WaitForSeconds(0.5f);
            input.scripted = false;
        }
    }
}
