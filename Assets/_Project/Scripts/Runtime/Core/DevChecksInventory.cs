using System.Collections;
using System.Linq;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>T1.6 automated check: 4-slot inventory (select / drop / pick up) and the washer hose tether.</summary>
    public static class DevChecksInventory
    {
        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var inv = me.GetComponent<PlayerInventory>();
            input.scripted = true;
            yield return new WaitForSeconds(0.3f);
            DevAutopilot.Log("slots: " + Slots(inv));

            // select every filled slot
            string sel = "";
            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                if (inv.Get(i) == null) continue;
                input.sSlot = i;
                yield return new WaitForSeconds(0.35f);
                sel += $"[{i + 1}→{(inv.Active != null ? inv.Active.ToolId : "-")}] ";
            }
            DevAutopilot.Log("select: " + sel);
            var hud = UI.Hud.Instance;
            if (hud != null)
            {
                DevAutopilot.Log("slot icons: " + string.Join(" ", Enumerable.Range(0, PlayerInventory.SlotCount)
                    .Select(i => $"[{i + 1}:{(inv.Get(i) != null ? inv.Get(i).ToolId : "-")}→{(hud.SlotIcon(i) != null ? hud.SlotIcon(i).name : "none")}]")));
                ScreenCapture.CaptureScreenshot("autotest_slots.png");
                yield return new WaitForSeconds(0.2f);
            }

            // drop the active tool, then pick it back up
            var dropped = inv.Active;
            int slot = inv.ActiveSlot.Value;
            input.sDrop = true;
            yield return new WaitForSeconds(1.2f);
            bool inWorld = dropped != null && !dropped.IsHeld && inv.Get(slot) == null;
            if (dropped != null)
            {
                me.Look.LookAt(dropped.transform.position);
                yield return new WaitForSeconds(0.3f);
                input.sInteract = true;
                yield return new WaitForSeconds(0.6f);
            }
            DevAutopilot.Log($"drop {dropped?.ToolId}: slot emptied & in world={inWorld}; picked up again={(dropped != null && dropped.HolderPlayer == me)}  slots: {Slots(inv)}");

            // hose: select the washer and run away from its reel
            int washerSlot = Enumerable.Range(0, PlayerInventory.SlotCount).FirstOrDefault(i => inv.Get(i) is PressureWasher);
            if (inv.Get(washerSlot) is PressureWasher washer)
            {
                input.sSlot = washerSlot;
                yield return new WaitForSeconds(0.4f);
                var hose = washer.GetComponent<HoseTether>();
                var a = hose.Anchor.Value;
                me.Motor.Teleport(new Vector3(a.x + 0.8f, 0.05f, a.z - 1.5f));
                me.Look.SetYaw(60f); // ENE across the dev yard, south of the test cars
                yield return new WaitForSeconds(0.3f);
                input.sMove = Vector2.up; input.sSprint = true;
                float maxD = 0f;
                for (float t = 0; t < 4f; t += Time.deltaTime)
                {
                    var d = me.transform.position - a; d.y = 0;
                    maxD = Mathf.Max(maxD, d.magnitude);
                    yield return null;
                }
                input.sMove = Vector2.zero; input.sSprint = false;
                yield return new WaitForSeconds(0.5f);
                var end = me.transform.position - a; end.y = 0;
                DevAutopilot.Log($"hose: length {hose.Length:0.0} m, max distance while sprinting away {maxD:0.00} m, after release {end.magnitude:0.00} m");
            }
            else DevAutopilot.Log("hose: washer not in inventory");
            input.scripted = false;
        }

        static string Slots(PlayerInventory inv) =>
            string.Join(" ", Enumerable.Range(0, PlayerInventory.SlotCount).Select(i => $"{i + 1}:{(inv.Get(i) != null ? inv.Get(i).ToolId : "_")}{(inv.ActiveSlot.Value == i ? "*" : "")}"));
    }
}
