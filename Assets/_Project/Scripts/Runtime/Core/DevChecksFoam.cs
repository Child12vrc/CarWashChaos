using System.Collections;
using System.Linq;
using CarWash.Dirt;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>T1.5 automated check on the greasy V04 test car: grease needs foam, spots after rinse, towel removes spots, floor foam.</summary>
    public static class DevChecksFoam
    {
        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var tools = me.GetComponent<PlayerTools>();
            input.scripted = true;
            var car = Object.FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None).FirstOrDefault(s => s.name.Contains("V04"));
            if (car == null) { DevAutopilot.Log("V04 car missing"); yield break; }
            var p = car.Painter;
            var c = car.transform.position;

            yield return Equip<PressureWasher>(me);
            float g0 = p.GreasePct, m0 = p.MudPct;
            yield return SpraySide(me, input, c, 4f);
            DevAutopilot.Log($"washer WITHOUT foam: grease {g0 * 100f:0.0}%→{p.GreasePct * 100f:0.0}%  mud {m0 * 100f:0.0}%→{p.MudPct * 100f:0.0}%");

            yield return Equip<FoamCannon>(me);
            yield return SpraySide(me, input, c, 3f);
            // floor foam: spray the ground in front of the car
            me.Motor.Teleport(c + new Vector3(0f, 0.05f, -6.5f));
            yield return new WaitForSeconds(0.2f);
            input.sUse = true;
            bool shot = false;
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                me.Look.LookAt(c + new Vector3(Mathf.Sin(t * 4f) * 1.5f, 0f, -4.5f));
                if (!shot && t > 1.2f) { shot = true; ScreenCapture.CaptureScreenshot("autotest_foam.png"); }
                yield return null;
            }
            input.sUse = false;
            yield return new WaitForSeconds(0.5f);
            var patches = Object.FindObjectsByType<FoamPatch>(FindObjectsSortMode.None);
            bool slippery = false;
            if (patches.Length > 0)
            {
                me.Motor.Teleport(patches[0].transform.position + Vector3.up * 0.05f);
                yield return new WaitForSeconds(0.3f);
                slippery = me.Motor.InFoam;
            }
            DevAutopilot.Log($"foam cannon: floor patches={patches.Length}, standing on patch → inFoam={slippery}");

            yield return Equip<PressureWasher>(me);
            float g1 = p.GreasePct;
            yield return SpraySide(me, input, c, 4f);
            DevAutopilot.Log($"washer AFTER foam: grease {g1 * 100f:0.0}%→{p.GreasePct * 100f:0.0}%, spots appeared {p.SpotsPct * 100f:0.0}% (clean {p.CleanPct * 100f:0.0}%)");

            yield return Equip<Towel>(me);
            float s0 = p.SpotsPct;
            yield return SpraySide(me, input, c, 6f, wipe: true, distance: 1.4f);
            DevAutopilot.Log($"towel: spots {s0 * 100f:0.0}%→{p.SpotsPct * 100f:0.0}% (clean {p.CleanPct * 100f:0.0}%), server clean {car.CleanPct.Value * 100f:0.0}%");
            input.scripted = false;
        }

        internal static IEnumerator Equip<T>(PlayerNet me) where T : ToolBase
        {
            var input = me.GetComponent<PlayerInputReader>();
            var tools = me.GetComponent<PlayerTools>();
            if (tools.Equipped is T) yield break;
            var inv = tools.Inventory;
            for (int i = 0; i < PlayerInventory.SlotCount; i++)
                if (inv.Get(i) is T)
                {
                    input.sSlot = i;
                    { float tw = Time.time; while (!(tools.Equipped is T) && Time.time - tw < 4f) yield return null; yield return new WaitForSeconds(0.1f); }
                    DevAutopilot.Log($"select {typeof(T).Name} from slot {i + 1}: {tools.Equipped is T}");
                    yield break;
                }
            // the hose would yank us back toward the reel: put the washer away (select an empty slot) before walking off
            if (tools.Equipped is PressureWasher)
                for (int i = 0; i < PlayerInventory.SlotCount; i++)
                    if (inv.Get(i) == null) { input.sSlot = i; yield return new WaitForSeconds(0.4f); break; }
            var tool = Object.FindObjectsByType<T>(FindObjectsSortMode.None).Where(x => !x.IsHeld)
                .OrderBy(x => (x.transform.position - me.transform.position).sqrMagnitude).FirstOrDefault();
            if (tool == null) { DevAutopilot.Log($"{typeof(T).Name} not found"); yield break; }
            var tp = tool.transform.position;
            me.Motor.Teleport(new Vector3(tp.x, 0.05f, tp.z) + (tp.y > 0.5f ? new Vector3(0, 0, 1.2f) : new Vector3(0, 0, -1.2f)));
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(tool.transform.position);
            yield return new WaitForSeconds(0.25f);
            input.sInteract = true;
            { float tw = Time.time; while (tools.Equipped != tool && Time.time - tw < 4f) yield return null; } // P1: wait for the server round trip, not a fixed time
            DevAutopilot.Log($"equip {typeof(T).Name}: {tools.Equipped == tool}");
        }

        /// <summary>Stand on the car's left side and sweep the side panel.</summary>
        static IEnumerator SpraySide(PlayerNet me, PlayerInputReader input, Vector3 c, float seconds, bool wipe = false, float distance = 2.4f)
        {
            me.Motor.Teleport(c + new Vector3(-distance, 0.05f, 0f));
            yield return new WaitForSeconds(0.25f);
            input.sUse = true;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                me.Look.LookAt(c + new Vector3(-0.95f, 0.55f + Mathf.PingPong(t * 0.8f, 0.6f), Mathf.Sin(t * 1.9f) * 2.1f));
                input.sLook = wipe ? new Vector2(Mathf.Sin(t * 25f) * 12f, 0f) : Vector2.zero;
                yield return null;
            }
            input.sUse = false; input.sLook = Vector2.zero;
            yield return new WaitForSeconds(1.2f); // let readback catch up
        }
    }
}
