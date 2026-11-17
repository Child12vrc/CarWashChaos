using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Player;
using CarWash.Vehicles;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// T2.2 client check (pairs with DevHostScenario on the host): vehicles drive road → queue → bays, doors open via E
    /// and replicate, interior trash is revealed and can be pulled out, queue patience → angry leave, DONE → leave + despawn.
    /// </summary>
    public static class DevChecksVehicles
    {
        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var grabber = me.GetComponent<PlayerGrabber>();
            input.scripted = true;
            var seen = new Dictionary<ulong, HashSet<VehicleState>>();
            int maxQueued = 0;
            void Track()
            {
                var all = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None);
                maxQueued = Mathf.Max(maxQueued, all.Count(v => v.State.Value == VehicleState.Queued));
                foreach (var v in all)
                {
                    if (!seen.TryGetValue(v.NetworkObjectId, out var s)) seen[v.NetworkObjectId] = s = new HashSet<VehicleState>();
                    s.Add(v.State.Value);
                }
            }

            float t0 = Time.time;
            Vehicle[] washing = null;
            while (Time.time - t0 < 180f)
            {
                Track();
                washing = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).Where(v => v.State.Value == VehicleState.Washing).ToArray();
                if (washing.Length >= 3) break;
                yield return null;
            }
            DevAutopilot.Log($"vehicles washing={washing.Length} after {Time.time - t0:0}s: " + string.Join(", ", washing.Select(v => $"{v.DataId}@bay{v.BayIndex.Value + 1} facing {v.transform.eulerAngles.y:0}°")));
            var car = washing.FirstOrDefault(v => v.BayIndex.Value == 0) ?? washing.FirstOrDefault();
            if (car != null)
            {
                var door = car.Doors[0];
                int before = car.TrashRemaining;
                var doorPoint = door.transform.TransformPoint(new Vector3(0, 0, -0.55f));
                var standAt = car.transform.TransformPoint(new Vector3(-2.1f, 0f, car.transform.InverseTransformPoint(doorPoint).z - 0.9f));
                me.Motor.Teleport(new Vector3(standAt.x, 0.05f, standAt.z));
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(doorPoint);
                yield return new WaitForSeconds(0.3f);
                string prompt = me.GetComponent<PlayerInteractor>().PromptText();
                input.sInteract = true;
                yield return new WaitForSeconds(0.8f);
                var trash = Object.FindObjectsByType<TrashItem>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.Attached && t.DoorIndex.Value == 0 && t.Owner.Value.TryGet(out var o) && o == car.NetworkObject);
                bool visible = trash != null && trash.GetComponentInChildren<Renderer>().enabled;
                DevAutopilot.Log($"door: prompt='{prompt}' open={door.Open.Value}; trash before={before}, revealed={visible}");
                if (trash != null)
                {
                    me.Look.LookAt(trash.transform.position);
                    yield return new WaitForSeconds(0.3f);
                    input.sGrab = true; yield return new WaitForSeconds(0.3f); input.sGrab = false;
                    yield return new WaitForSeconds(0.6f);
                    bool held = grabber.Held != null && grabber.Held.gameObject == trash.gameObject;
                    input.sDrop = true;
                    yield return new WaitForSeconds(0.6f);
                    DevAutopilot.Log($"trash: grabbed={held}, remaining {before}→{car.TrashRemaining}");
                }
                me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f)); // step out of the way before the car leaves
            }

            t0 = Time.time;
            while (Time.time - t0 < 200f)
            {
                Track();
                if (Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).Length == 0 && Time.time - t0 > 5f) break;
                yield return null;
            }
            int angry = seen.Values.Count(s => s.Contains(VehicleState.Angry));
            int washed = seen.Values.Count(s => s.Contains(VehicleState.Washing) && s.Contains(VehicleState.Leaving));
            DevAutopilot.Log($"flow: vehicles seen={seen.Count}, max queued={maxQueued}, angry-left={angry}, washed+left={washed}, remaining now={Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None).Length} ({Time.time - t0:0}s)");
            input.scripted = false;
        }
    }
}
