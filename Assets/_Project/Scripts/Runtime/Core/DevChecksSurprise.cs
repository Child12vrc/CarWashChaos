using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Critters;
using CarWash.Player;
using CarWash.Vehicles;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// T3.6 client check (pairs with DevSurpriseScenario): opens the driver doors of V01/V04 (OnOpen surprises) and
    /// records every reveal sting seen on this client (critter id, the car it came from, toast).
    /// </summary>
    public static class DevChecksSurprise
    {
        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            var reveals = new List<string>();
            bool shot = false;
            void OnReveal(CritterBase c)
            {
                var car = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).OrderBy(v => (v.transform.position - c.transform.position).sqrMagnitude).FirstOrDefault();
                reveals.Add($"{c.CritterId}({c.DisplayName})←{(car != null ? car.DataId : "?")} toast='{UI.Hud.Instance?.Day?.LastToast}'");
            }
            CritterBase.Revealed += OnReveal;

            var opened = new HashSet<string>();
            float t0 = Time.time;
            while (Time.time - t0 < 330f && reveals.Count < 6) // 4 cars + the boss's 2 slimes
            {
                foreach (var id in new[] { "V01", "V04" })
                {
                    if (opened.Contains(id)) continue;
                    var car = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).FirstOrDefault(v => v.DataId == id && (v.State.Value == VehicleState.Washing || v.State.Value == VehicleState.Queued));
                    if (car == null || car.Doors.Count == 0) continue;
                    var door = car.Doors[0];
                    var doorPoint = door.transform.TransformPoint(new Vector3(0, 0, -0.55f));
                    var standAt = car.transform.TransformPoint(new Vector3(-2.1f, 0f, car.transform.InverseTransformPoint(doorPoint).z - 0.9f));
                    me.Motor.Teleport(new Vector3(standAt.x, 0.05f, standAt.z));
                    yield return new WaitForSeconds(0.4f);
                    me.Look.LookAt(doorPoint);
                    yield return new WaitForSeconds(0.3f);
                    input.sInteract = true;
                    yield return new WaitForSeconds(0.8f);
                    if (door.Open.Value) { opened.Add(id); DevAutopilot.Log($"opened {id} driver door ({car.State.Value})"); }
                    me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f));
                    yield return new WaitForSeconds(1.5f);
                }
                if (!shot && reveals.Count > 0)
                {
                    shot = true;
                    yield return new WaitForSeconds(0.3f);
                    ScreenCapture.CaptureScreenshot("autotest_surprise.png");
                }
                yield return new WaitForSeconds(0.5f);
            }
            CritterBase.Revealed -= OnReveal;
            DevAutopilot.Log($"surprises seen on client: {reveals.Count} → {string.Join(" | ", reveals)} ({Time.time - t0:0}s)");
            var bossToast = UI.Hud.Instance?.Day?.ToastLog.FirstOrDefault(t => t.Contains("특별 손님"));
            DevAutopilot.Log($"boss intro toast: '{bossToast}'");
            input.scripted = false;
        }
    }
}
