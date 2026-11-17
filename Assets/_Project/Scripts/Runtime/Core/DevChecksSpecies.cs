using System.Collections;
using System.Linq;
using CarWash.Critters;
using CarWash.Dirt;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// T3.2-T3.5 client check on the hidden dev-yard critters (revealed one by one through the dev reveal RPC):
    /// raccoon steals a lying tool and stashes it at a hiding spot; possum plays dead, then bites (knockdown);
    /// mud slime re-dirts the V02 test car and splits when soaked; bees sting, foam kills the swarm.
    /// </summary>
    public static class DevChecksSpecies
    {
        static T Find<T>() where T : CritterBase => Object.FindObjectsByType<T>(FindObjectsSortMode.None).FirstOrDefault(c => c.IsSpawned);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;

            // --- T3.2 raccoon
            var raccoon = Find<Raccoon>();
            if (raccoon != null)
            {
                me.Motor.Teleport(DevYard.Origin + new Vector3(-6f, 0.05f, 6f)); // out of its way
                raccoon.DevRequestReveal();
                float t0 = Time.time;
                ToolBase stolen = null;
                while (Time.time - t0 < 25f && stolen == null) { stolen = Object.FindObjectsByType<ToolBase>(FindObjectsSortMode.None).FirstOrDefault(t => t.Stolen.Value); yield return null; }
                float stoleAt = Time.time - t0;
                Vector3 from = stolen != null ? stolen.transform.position : Vector3.zero;
                while (Time.time - t0 < 60f && stolen != null && stolen.Stolen.Value) yield return null;
                DevAutopilot.Log($"raccoon: revealed → stole {(stolen != null ? stolen.ToolId : "nothing")} after {stoleAt:0.0}s at {from}; stashed={(stolen != null && !stolen.Stolen.Value)} at {(stolen != null ? stolen.transform.position.ToString() : "-")} ({Time.time - t0:0}s); raccoon now {raccoon.State.Value} at {raccoon.transform.position}");
            }

            // --- T3.3 possum
            var possum = Find<Possum>();
            if (possum != null)
            {
                possum.DevRequestReveal();
                yield return new WaitForSeconds(1.5f);
                me.Motor.Teleport(possum.transform.position + new Vector3(0f, 0.05f, -6f));
                me.Look.LookAt(possum.transform.position);
                var s0 = possum.State.Value;
                float t0 = Time.time; bool knocked = false; float wokeAt = -1f;
                while (Time.time - t0 < 25f && !knocked)
                {
                    if (wokeAt < 0f && possum.State.Value == CritterState.Chase) wokeAt = Time.time - t0;
                    knocked |= me.Status.IsKnocked;
                    yield return null;
                }
                DevAutopilot.Log($"possum: after reveal {s0} (playing dead), woke+chased at {wokeAt:0.0}s, bit me → knocked={knocked} at {Time.time - t0:0.0}s");
                yield return new WaitForSeconds(2f);
            }

            // --- T3.4 mud slime
            var slime = Find<MudSlime>();
            var car = Object.FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None).OrderBy(s => (s.transform.position - DevYard.Slime).sqrMagnitude).FirstOrDefault();
            if (slime != null && car != null)
            {
                me.Motor.Teleport(DevYard.Origin + new Vector3(-6f, 0.05f, 6f));
                float c0 = car.CleanPct.Value;
                slime.DevRequestReveal();
                float t0 = Time.time;
                while (Time.time - t0 < 14f) yield return null;
                float c1 = car.CleanPct.Value;
                DevAutopilot.Log($"slime: re-dirt test car clean {c0 * 100f:0.0}% → {c1 * 100f:0.0}% in 14s (slime {slime.State.Value})");
                // soak it → split
                yield return new WaitForSeconds(0.3f);
                // washer (40 wetness/s; the 10/s foam cannon cannot out-pace flee + drying), from the slime's open side
                me.Motor.Teleport(new Vector3(DevYard.WasherReel.x + 1.5f, 0.05f, DevYard.WasherReel.z));
                yield return new WaitForSeconds(0.3f);
                yield return DevChecksFoam.Equip<PressureWasher>(me);
                int maxCount = 1, maxGen = 1;
                t0 = Time.time;
                while (Time.time - t0 < 14f)
                {
                    var all = Object.FindObjectsByType<MudSlime>(FindObjectsSortMode.None).Where(m => m.IsSpawned).ToArray();
                    if (all.Length == 0) break;
                    maxCount = Mathf.Max(maxCount, all.Length);
                    maxGen = Mathf.Max(maxGen, all.Max(m => m.Gen.Value));
                    var target = all.OrderBy(m => m.Gen.Value).First();
                    var p = target.transform.position;
                    var nearCar = Object.FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None).OrderBy(s => (s.transform.position - p).sqrMagnitude).First().transform.position;
                    var away = p - nearCar; away.y = 0f; // stand on the open side, not behind the car
                    me.Motor.Teleport(p + away.normalized * 2.5f + new Vector3(0, 0.05f, 0));
                    me.Look.LookAt(p + Vector3.up * 0.25f);
                    input.sUse = true;
                    yield return null;
                }
                input.sUse = false;
                var left = Object.FindObjectsByType<MudSlime>(FindObjectsSortMode.None).Where(m => m.IsSpawned).ToArray();
                DevAutopilot.Log($"slime: soaked → max slimes alive {maxCount}, max generation {maxGen}; now {left.Length} slimes, gens [{string.Join(",", left.Select(m => m.Gen.Value))}], scales [{string.Join(",", left.Select(m => m.transform.localScale.x.ToString("0.00")))}]");
                ScreenCapture.CaptureScreenshot("autotest_slimes.png");
                // select an empty slot (hose would yank us otherwise)
                var inv = me.GetComponent<PlayerTools>().Inventory;
                for (int i = 0; i < PlayerInventory.SlotCount; i++) if (inv.Get(i) == null) { input.sSlot = i; break; }
                { var tl = me.GetComponent<PlayerTools>(); float tw = Time.time; while (tl.Equipped != null && Time.time - tw < 4f) yield return null; } // wait for the server to confirm the empty slot
            }

            // --- T3.5 bees
            var bees = Find<BeeSwarm>();
            if (bees != null)
            {
                yield return DevChecksFoam.Equip<FoamCannon>(me);
                me.Motor.Teleport(bees.transform.position + new Vector3(0f, 0.05f, -5f));
                bees.DevRequestReveal();
                float t0 = Time.time; bool stung = false;
                while (Time.time - t0 < 15f && !stung) { stung |= me.Status.Has(StatusFlags.Stung); yield return null; }
                DevAutopilot.Log($"bees: chased + stung={stung} after {Time.time - t0:0.0}s (state {bees.State.Value})");
                ScreenCapture.CaptureScreenshot("autotest_bees.png");
                t0 = Time.time;
                while (Time.time - t0 < 6f && bees != null && bees.IsSpawned)
                {
                    me.Look.LookAt(bees.transform.position + Vector3.up * 1.2f);
                    input.sUse = true;
                    yield return null;
                }
                input.sUse = false;
                yield return new WaitForSeconds(1f);
                DevAutopilot.Log($"bees: foam → swarm gone={bees == null || !bees.IsSpawned} after {Time.time - t0:0.0}s");
            }
            me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f));
            input.scripted = false;
        }
    }
}
